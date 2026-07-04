using BovineLabs.Movement.Data;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.Traverse.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace BovineLabs.Timeline.Traverse
{
    /// <summary>
    /// Consumes SteeringFlags clips: while active, replaces the bound agent's <see cref="CrowdAgent.UpdateFlags" />,
    /// capturing the original on the enter edge and restoring it on exit. Restores are applied before captures so a
    /// same-frame exit/enter pair on one agent captures the true baseline, never the outgoing clip's override
    /// (deterministic across worlds, mirrors <see cref="NavigationSystem" />.ApplyJob).
    /// </summary>
    [UpdateInGroup(typeof(TimelineComponentAnimationGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation)]
    public partial struct SteeringFlagsSystem : ISystem
    {
        private struct FlagCommand
        {
            public Entity Clip;
            public Entity Agent;
            public byte Flags;
            public bool Capture;
            public bool Restore;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var commands = new NativeQueue<FlagCommand>(state.WorldUpdateAllocator);

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
                CrowdAgents = SystemAPI.GetComponentLookup<CrowdAgent>(false),
                States = SystemAPI.GetComponentLookup<SteeringFlagsState>(false),
            }.Schedule(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(ClipActive))]
        [WithPresent(typeof(ClipActivePrevious))]
        private partial struct ActiveJob : IJobEntity
        {
            public NativeQueue<FlagCommand>.ParallelWriter Commands;

            private void Execute(Entity entity, in TrackBinding binding, in SteeringFlagsData data,
                EnabledRefRO<ClipActivePrevious> activePrev)
            {
                if (binding.Value == Entity.Null)
                {
                    return;
                }

                this.Commands.Enqueue(new FlagCommand
                {
                    Clip = entity,
                    Agent = binding.Value,
                    Flags = data.Flags,
                    Capture = !activePrev.ValueRO,
                });
            }
        }

        [BurstCompile]
        [WithDisabled(typeof(ClipActive))]
        [WithAll(typeof(ClipActivePrevious))]
        private partial struct ExitJob : IJobEntity
        {
            public NativeQueue<FlagCommand>.ParallelWriter Commands;

            private void Execute(Entity entity, in TrackBinding binding, in SteeringFlagsData data)
            {
                if (!data.RestoreOnExit || binding.Value == Entity.Null)
                {
                    return;
                }

                this.Commands.Enqueue(new FlagCommand
                {
                    Clip = entity,
                    Agent = binding.Value,
                    Restore = true,
                });
            }
        }

        [BurstCompile]
        private struct ApplyJob : IJob
        {
            public NativeQueue<FlagCommand> Commands;
            public ComponentLookup<CrowdAgent> CrowdAgents;
            public ComponentLookup<SteeringFlagsState> States;

            public void Execute()
            {
                // Pass 1 = restores, pass 2 = captures + applies (see the system summary).
                var applies = new NativeList<FlagCommand>(this.Commands.Count, Allocator.Temp);

                while (this.Commands.TryDequeue(out var c))
                {
                    if (!c.Restore)
                    {
                        applies.Add(c);
                        continue;
                    }

                    if (!this.States.HasComponent(c.Clip) || !this.CrowdAgents.HasComponent(c.Agent))
                    {
                        continue;
                    }

                    var clipState = this.States[c.Clip];
                    if (!clipState.Captured)
                    {
                        continue;
                    }

                    var crowd = this.CrowdAgents[c.Agent];
                    crowd.UpdateFlags = (UpdateFlags)clipState.Original;
                    this.CrowdAgents[c.Agent] = crowd;

                    clipState.Captured = false;
                    this.States[c.Clip] = clipState;
                }

                for (var i = 0; i < applies.Length; i++)
                {
                    var c = applies[i];
                    if (!this.CrowdAgents.HasComponent(c.Agent))
                    {
                        continue;
                    }

                    var crowd = this.CrowdAgents[c.Agent];

                    if (c.Capture && this.States.HasComponent(c.Clip))
                    {
                        var clipState = this.States[c.Clip];
                        clipState.Original = (byte)crowd.UpdateFlags;
                        clipState.Captured = true;
                        this.States[c.Clip] = clipState;
                    }

                    if (crowd.UpdateFlags != (UpdateFlags)c.Flags)
                    {
                        crowd.UpdateFlags = (UpdateFlags)c.Flags;
                        this.CrowdAgents[c.Agent] = crowd;
                    }
                }
            }
        }
    }
}
