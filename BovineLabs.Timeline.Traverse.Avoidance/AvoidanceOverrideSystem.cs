using BovineLabs.Avoidance.Data;
using BovineLabs.Timeline.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace BovineLabs.Timeline.Traverse.Avoidance
{
    /// <summary>
    /// Consumes AvoidanceOverride clips: while active, overrides the selected fields of the bound agent's
    /// <see cref="ORCAAgent" />, capturing the full original struct on the enter edge and restoring only the
    /// overridden fields on exit. Restores are applied before captures so a same-frame exit/enter pair on one
    /// agent captures the true baseline, never the outgoing clip's override (deterministic across worlds,
    /// mirrors SteeringFlagsSystem.ApplyJob).
    /// </summary>
    [UpdateInGroup(typeof(TimelineComponentAnimationGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation)]
    public partial struct AvoidanceOverrideSystem : ISystem
    {
        private struct OverrideCommand
        {
            public Entity Clip;
            public Entity Agent;
            public AvoidanceOverrideData Data;
            public bool Capture;
            public bool Restore;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var commands = new NativeQueue<OverrideCommand>(state.WorldUpdateAllocator);

            state.Dependency = new ActiveJob
            {
                Commands = commands.AsParallelWriter(),
            }.ScheduleParallel(state.Dependency);

            state.Dependency = new ExitJob
            {
                Commands = commands.AsParallelWriter(),
            }.ScheduleParallel(state.Dependency);

            state.Dependency = new ApplyJob
            {
                Commands = commands,
                Agents = SystemAPI.GetComponentLookup<ORCAAgent>(false),
                States = SystemAPI.GetComponentLookup<AvoidanceOverrideState>(false),
            }.Schedule(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(ClipActive))]
        [WithPresent(typeof(ClipActivePrevious))]
        private partial struct ActiveJob : IJobEntity
        {
            public NativeQueue<OverrideCommand>.ParallelWriter Commands;

            private void Execute(Entity entity, in TrackBinding binding, in AvoidanceOverrideData data,
                EnabledRefRO<ClipActivePrevious> activePrev)
            {
                if (binding.Value == Entity.Null)
                {
                    return;
                }

                this.Commands.Enqueue(new OverrideCommand
                {
                    Clip = entity,
                    Agent = binding.Value,
                    Data = data,
                    Capture = !activePrev.ValueRO,
                });
            }
        }

        [BurstCompile]
        [WithDisabled(typeof(ClipActive))]
        [WithAll(typeof(ClipActivePrevious))]
        private partial struct ExitJob : IJobEntity
        {
            public NativeQueue<OverrideCommand>.ParallelWriter Commands;

            private void Execute(Entity entity, in TrackBinding binding, in AvoidanceOverrideData data)
            {
                if (!data.RestoreOnExit || binding.Value == Entity.Null)
                {
                    return;
                }

                this.Commands.Enqueue(new OverrideCommand
                {
                    Clip = entity,
                    Agent = binding.Value,
                    Data = data,
                    Restore = true,
                });
            }
        }

        [BurstCompile]
        private struct ApplyJob : IJob
        {
            public NativeQueue<OverrideCommand> Commands;
            public ComponentLookup<ORCAAgent> Agents;
            public ComponentLookup<AvoidanceOverrideState> States;

            public void Execute()
            {
                // Pass 1 = restores, pass 2 = captures + applies (see the system summary).
                var applies = new NativeList<OverrideCommand>(this.Commands.Count, Allocator.Temp);

                while (this.Commands.TryDequeue(out var c))
                {
                    if (!c.Restore)
                    {
                        applies.Add(c);
                        continue;
                    }

                    if (!this.States.HasComponent(c.Clip) || !this.Agents.HasComponent(c.Agent))
                    {
                        continue;
                    }

                    var clipState = this.States[c.Clip];
                    if (!clipState.Captured)
                    {
                        continue;
                    }

                    // Restore only the fields this clip overrode; other fields keep external changes.
                    var agent = this.Agents[c.Agent];
                    ApplyFields(ref agent, c.Data, clipState.Original);
                    this.Agents[c.Agent] = agent;

                    clipState.Captured = false;
                    this.States[c.Clip] = clipState;
                }

                for (var i = 0; i < applies.Length; i++)
                {
                    var c = applies[i];
                    if (!this.Agents.HasComponent(c.Agent))
                    {
                        continue;
                    }

                    var agent = this.Agents[c.Agent];

                    if (c.Capture && this.States.HasComponent(c.Clip))
                    {
                        var clipState = this.States[c.Clip];
                        clipState.Original = agent;
                        clipState.Captured = true;
                        this.States[c.Clip] = clipState;
                    }

                    var updated = agent;
                    ApplyFields(ref updated, c.Data, new ORCAAgent
                    {
                        ManuallyControlled = c.Data.ManuallyControlled,
                        Layer = c.Data.Layer,
                        CollidesWith = c.Data.CollidesWith,
                        Priority = c.Data.Priority,
                    });

                    if (updated.ManuallyControlled != agent.ManuallyControlled || updated.Layer != agent.Layer ||
                        updated.CollidesWith != agent.CollidesWith || updated.Priority != agent.Priority)
                    {
                        this.Agents[c.Agent] = updated;
                    }
                }
            }

            /// <summary> Copies the fields selected by <paramref name="data" />'s override flags from <paramref name="source" /> into <paramref name="agent" />. </summary>
            private static void ApplyFields(ref ORCAAgent agent, in AvoidanceOverrideData data, in ORCAAgent source)
            {
                if (data.OverrideManuallyControlled)
                {
                    agent.ManuallyControlled = source.ManuallyControlled;
                }

                if (data.OverrideLayer)
                {
                    agent.Layer = source.Layer;
                }

                if (data.OverrideCollidesWith)
                {
                    agent.CollidesWith = source.CollidesWith;
                }

                if (data.OverridePriority)
                {
                    agent.Priority = source.Priority;
                }
            }
        }
    }
}
