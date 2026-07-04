#if BL_ESSENCE
using BovineLabs.Core.Iterators;
using BovineLabs.Core.Jobs;
using BovineLabs.Essence.Data;
using BovineLabs.Movement.Data;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.Traverse.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace BovineLabs.Timeline.Traverse
{
    /// <summary>
    /// Consumes NavSpeed clips: blends Speed/Accel/Turn multipliers across active clips (falling back to identity
    /// for any weight not covered, so ease handles ramp the effect in and out) and applies them as
    /// <see cref="StatModifyType.Multiplicative" /> modifiers (ValueFloat = multiplier - 1) on the bound agent's
    /// <see cref="StatModifiers" /> buffer. All modifiers are keyed by a system-owned source entity: each apply is
    /// clear-by-source then re-add, so blends never stack. Agents that leave the blend map — natural clip end OR a
    /// clip destroyed mid-active — are caught via the <see cref="NavSpeedApplied" /> marker and get their modifiers
    /// cleared. Stats the agent never seeded are skipped so a missing schema entry can't freeze movement at 0.
    /// </summary>
    [UpdateInGroup(typeof(TimelineComponentAnimationGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation)]
    public partial struct NavSpeedSystem : ISystem
    {
        /// <summary> Identity threshold: multipliers within this of 1 emit no modifier. </summary>
        private const float IdentityEpsilon = 1e-4f;

        private TrackBlendImpl<NavSpeedMultipliers, NavSpeedAnimated> impl;
        private Entity source;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            this.impl.OnCreate(ref state);
            this.source = state.EntityManager.CreateEntity();
            state.RequireForUpdate<MovementStatsConfig>();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            this.impl.OnDestroy(ref state);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var blendData = this.impl.Update(ref state);

            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            state.Dependency = new ApplyJob
                {
                    BlendData = blendData,
                    Config = SystemAPI.GetSingleton<MovementStatsConfig>(),
                    Source = this.source,
                    Stats = SystemAPI.GetBufferLookup<Stat>(true),
                    Modifiers = SystemAPI.GetBufferLookup<StatModifiers>(),
                    StatChangeds = SystemAPI.GetComponentLookup<StatChanged>(),
                    Applieds = SystemAPI.GetComponentLookup<NavSpeedApplied>(),
                    ECB = ecb.AsParallelWriter(),
                }
                .ScheduleParallel(blendData, 64, state.Dependency);

            state.Dependency = new RemoveJob
                {
                    BlendData = blendData,
                    Source = this.source,
                }
                .Schedule(state.Dependency);
        }

        private static bool TryAddModifier(
            ref DynamicBuffer<StatModifiers> modifiers, in DynamicBuffer<Stat> stats, bool hasStats, Entity source,
            StatKey key, float multiplier)
        {
            if (math.abs(multiplier - 1f) <= IdentityEpsilon)
            {
                return false; // identity: emit nothing
            }

            // Unseeded stat: a Multiplicative modifier would multiply nothing (or a stale 0) — skip, never freeze.
            if (!hasStats || !stats.AsMap().TryGetValue(key, out _))
            {
                return false;
            }

            modifiers.Add(new StatModifiers
            {
                SourceEntity = source,
                Value = new StatModifier
                {
                    Type = key,
                    ModifyType = StatModifyType.Multiplicative,
                    ValueFloat = multiplier - 1f,
                },
            });

            return true;
        }

        private static bool ClearBySource(ref DynamicBuffer<StatModifiers> modifiers, Entity source)
        {
            var removed = false;
            for (var i = modifiers.Length - 1; i >= 0; i--)
            {
                if (modifiers[i].SourceEntity == source)
                {
                    modifiers.RemoveAtSwapBack(i);
                    removed = true;
                }
            }

            return removed;
        }

        [BurstCompile]
        private struct ApplyJob : IJobParallelHashMapDefer
        {
            [ReadOnly]
            public NativeParallelHashMap<Entity, MixData<NavSpeedMultipliers>>.ReadOnly BlendData;

            public MovementStatsConfig Config;
            public Entity Source;

            [ReadOnly]
            public BufferLookup<Stat> Stats;

            // Keyed by unique blend-map agents; no two entries alias the same entity.
            [NativeDisableParallelForRestriction]
            public BufferLookup<StatModifiers> Modifiers;

            [NativeDisableParallelForRestriction]
            public ComponentLookup<StatChanged> StatChangeds;

            [NativeDisableParallelForRestriction]
            public ComponentLookup<NavSpeedApplied> Applieds;

            public EntityCommandBuffer.ParallelWriter ECB;

            public void ExecuteNext(int entryIndex, int jobIndex)
            {
                this.Read(this.BlendData, entryIndex, out var agent, out var mix);

                if (!this.Modifiers.TryGetBuffer(agent, out var modifiers))
                {
                    return; // binding has no stat pipeline: no-op
                }

                var mults = JobHelpers.Blend<NavSpeedMultipliers, NavSpeedMixer>(ref mix, NavSpeedMultipliers.Identity);

                var changed = ClearBySource(ref modifiers, this.Source);
                var hasStats = this.Stats.TryGetBuffer(agent, out var stats);

                changed |= TryAddModifier(ref modifiers, stats, hasStats, this.Source, this.Config.MoveSpeedStat, mults.Speed);
                changed |= TryAddModifier(ref modifiers, stats, hasStats, this.Source, this.Config.AccelerationStat, mults.Accel);
                changed |= TryAddModifier(ref modifiers, stats, hasStats, this.Source, this.Config.RotationSpeedStat, mults.Turn);

                if (changed && this.StatChangeds.HasComponent(agent))
                {
                    this.StatChangeds.SetComponentEnabled(agent, true);
                }

                if (this.Applieds.HasComponent(agent))
                {
                    this.Applieds.SetComponentEnabled(agent, true);
                }
                else
                {
                    this.ECB.AddComponent<NavSpeedApplied>(jobIndex, agent); // added enabled
                }
            }
        }

        /// <summary> Clears modifiers from agents that left the blend map (clip ended or was destroyed mid-active). </summary>
        [BurstCompile]
        [WithPresent(typeof(StatChanged))]
        private partial struct RemoveJob : IJobEntity
        {
            [ReadOnly]
            public NativeParallelHashMap<Entity, MixData<NavSpeedMultipliers>>.ReadOnly BlendData;

            public Entity Source;

            private void Execute(
                Entity entity, ref DynamicBuffer<StatModifiers> modifiers, EnabledRefRW<NavSpeedApplied> applied,
                EnabledRefRW<StatChanged> statChanged)
            {
                if (this.BlendData.ContainsKey(entity))
                {
                    return;
                }

                if (ClearBySource(ref modifiers, this.Source))
                {
                    statChanged.ValueRW = true;
                }

                applied.ValueRW = false;
            }
        }
    }
}
#endif
