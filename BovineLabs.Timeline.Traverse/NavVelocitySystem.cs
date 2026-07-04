using BovineLabs.Core.Jobs;
using BovineLabs.Movement.Data;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.Traverse.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Traverse
{
    /// <summary>
    /// Consumes NavVelocity clips: resolves each active clip's authored velocity to world-space XZ (local
    /// velocities rotate with the agent every frame), blends across clips (remainder weight eases toward zero) and
    /// writes the result straight into <see cref="DesiredVelocity" />. While driving, the agent carries
    /// <see cref="NavManualDrive" /> whose WriteGroup excludes the navigation pipeline's own velocity writer, so
    /// the manual value wins; MoveTo targets and IsPathfinding are untouched and resume the instant the marker is
    /// removed. Agents that leave the blend map (clip end or destroyed mid-active) get the marker removed and their
    /// velocity zeroed.
    /// </summary>
    [UpdateInGroup(typeof(TimelineComponentAnimationGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation)]
    public partial struct NavVelocitySystem : ISystem
    {
        private TrackBlendImpl<float2, NavVelocityAnimated> impl;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            this.impl.OnCreate(ref state);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            this.impl.OnDestroy(ref state);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new ResolveJob
            {
                LocalTransforms = SystemAPI.GetComponentLookup<LocalTransform>(true),
            }.ScheduleParallel();

            var blendData = this.impl.Update(ref state);

            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            state.Dependency = new ApplyJob
                {
                    BlendData = blendData,
                    DesiredVelocities = SystemAPI.GetComponentLookup<DesiredVelocity>(),
                    ManualDrives = SystemAPI.GetComponentLookup<NavManualDrive>(true),
                    ECB = ecb.AsParallelWriter(),
                }
                .ScheduleParallel(blendData, 64, state.Dependency);

            state.Dependency = new RemoveJob
                {
                    BlendData = blendData,
                    ECB = ecb.AsParallelWriter(),
                }
                .ScheduleParallel(state.Dependency);
        }

        /// <summary> Resolves authored (possibly agent-local) velocity to world-space XZ before the blend reads it. </summary>
        [BurstCompile]
        [WithAll(typeof(ClipActive), typeof(TimelineActive))]
        private partial struct ResolveJob : IJobEntity
        {
            [ReadOnly]
            public ComponentLookup<LocalTransform> LocalTransforms;

            private void Execute(ref NavVelocityAnimated animated, in NavVelocityAuthored authored, in TrackBinding binding)
            {
                var velocity = authored.Velocity;
                if (authored.Local && this.LocalTransforms.TryGetComponent(binding.Value, out var transform))
                {
                    velocity = math.rotate(transform.Rotation, velocity);
                }

                animated.Value = velocity.xz;
            }
        }

        [BurstCompile]
        private struct ApplyJob : IJobParallelHashMapDefer
        {
            [ReadOnly]
            public NativeParallelHashMap<Entity, MixData<float2>>.ReadOnly BlendData;

            // Keyed by unique blend-map agents; no two entries alias the same entity.
            [NativeDisableParallelForRestriction]
            public ComponentLookup<DesiredVelocity> DesiredVelocities;

            [ReadOnly]
            public ComponentLookup<NavManualDrive> ManualDrives;

            public EntityCommandBuffer.ParallelWriter ECB;

            public void ExecuteNext(int entryIndex, int jobIndex)
            {
                this.Read(this.BlendData, entryIndex, out var agent, out var mix);

                if (!this.DesiredVelocities.HasComponent(agent))
                {
                    return; // binding is not a movement agent: no-op
                }

                var velocity = JobHelpers.Blend<float2, Float2Mixer>(ref mix, float2.zero);
                this.DesiredVelocities[agent] = new DesiredVelocity { Velocity = velocity };

                if (!this.ManualDrives.HasComponent(agent))
                {
                    this.ECB.AddComponent<NavManualDrive>(jobIndex, agent);
                }
            }
        }

        /// <summary> Hands control back: zero velocity + structural marker removal for agents no longer driven. </summary>
        [BurstCompile]
        [WithAll(typeof(NavManualDrive))]
        private partial struct RemoveJob : IJobEntity
        {
            [ReadOnly]
            public NativeParallelHashMap<Entity, MixData<float2>>.ReadOnly BlendData;

            public EntityCommandBuffer.ParallelWriter ECB;

            private void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndex, ref DesiredVelocity velocity)
            {
                if (this.BlendData.ContainsKey(entity))
                {
                    return;
                }

                velocity.Velocity = float2.zero;
                this.ECB.RemoveComponent<NavManualDrive>(chunkIndex, entity);
            }
        }
    }
}
