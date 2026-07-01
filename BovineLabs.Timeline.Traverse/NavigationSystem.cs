using BovineLabs.Movement.Data;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.Traverse.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Traverse
{
    /// <summary>
    /// Consumes the Navigation track clips (<see cref="MoveToData" />, <see cref="SteerData" />, <see cref="StopData" />)
    /// and drives the bound Traverse agent by writing <see cref="CrowdAgentData" /> and toggling <see cref="IsPathfinding" />.
    /// Gathers per-clip commands in parallel, then applies them on a single thread (last write per agent wins).
    /// </summary>
    [UpdateInGroup(typeof(TimelineComponentAnimationGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation)]
    public partial struct NavigationSystem : ISystem
    {
        private struct NavCommand
        {
            public Entity Agent;
            public float3 Value;
            public half Extents;
            public byte FilterType;
            public bool IsDirection;
            public bool SetTarget;
            public bool SetEnable;
            public bool Enable;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var commands = new NativeQueue<NavCommand>(state.WorldUpdateAllocator);

            state.Dependency = new MoveToJob
            {
                Commands = commands.AsParallelWriter(),
                TargetsLookup = SystemAPI.GetComponentLookup<Targets>(true),
                LtwLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true),
            }.ScheduleParallel(state.Dependency);

            state.Dependency = new MoveToExitJob
            {
                Commands = commands.AsParallelWriter(),
            }.ScheduleParallel(state.Dependency);

            state.Dependency = new SteerJob
            {
                Commands = commands.AsParallelWriter(),
            }.ScheduleParallel(state.Dependency);

            state.Dependency = new SteerExitJob
            {
                Commands = commands.AsParallelWriter(),
            }.ScheduleParallel(state.Dependency);

            state.Dependency = new StopJob
            {
                Commands = commands.AsParallelWriter(),
            }.ScheduleParallel(state.Dependency);

            state.Dependency = new ApplyJob
            {
                Commands = commands,
                AgentData = SystemAPI.GetComponentLookup<CrowdAgentData>(false),
                Pathfinding = SystemAPI.GetComponentLookup<IsPathfinding>(false),
            }.Schedule(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(ClipActive))]
        [WithPresent(typeof(ClipActivePrevious))] // enter frame has ClipActivePrevious disabled; match it anyway and read the state below.
        private partial struct MoveToJob : IJobEntity
        {
            public NativeQueue<NavCommand>.ParallelWriter Commands;

            [ReadOnly] public ComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public ComponentLookup<LocalToWorld> LtwLookup;

            private void Execute(in TrackBinding binding, in MoveToData data, EnabledRefRO<ClipActivePrevious> activePrev)
            {
                var agent = binding.Value;
                if (agent == Entity.Null)
                {
                    return;
                }

                // Write once on enter; re-write every frame only when following a (moving) target.
                var isFirstFrame = !activePrev.ValueRO;
                if (!isFirstFrame && !data.Follow)
                {
                    return;
                }

                float3 pos;
                if (data.Destination == Target.None)
                {
                    pos = data.WorldPosition;
                }
                else
                {
                    if (!TargetsLookup.TryGetComponent(agent, out var targets))
                    {
                        return;
                    }

                    var dest = targets.Get(data.Destination, agent);
                    if (dest == Entity.Null || !LtwLookup.TryGetComponent(dest, out var ltw))
                    {
                        return;
                    }

                    pos = ltw.Position;
                }

                this.Commands.Enqueue(new NavCommand
                {
                    Agent = agent,
                    Value = pos,
                    Extents = data.Extents,
                    FilterType = data.QueryFilterType,
                    IsDirection = false,
                    SetTarget = true,
                    SetEnable = true,
                    Enable = true,
                });
            }
        }

        [BurstCompile]
        [WithDisabled(typeof(ClipActive))]
        [WithAll(typeof(ClipActivePrevious))]
        private partial struct MoveToExitJob : IJobEntity
        {
            public NativeQueue<NavCommand>.ParallelWriter Commands;

            private void Execute(in TrackBinding binding, in MoveToData data)
            {
                if (!data.StopOnExit || binding.Value == Entity.Null)
                {
                    return;
                }

                this.Commands.Enqueue(new NavCommand { Agent = binding.Value, SetEnable = true, Enable = false });
            }
        }

        [BurstCompile]
        [WithAll(typeof(ClipActive))]
        private partial struct SteerJob : IJobEntity
        {
            public NativeQueue<NavCommand>.ParallelWriter Commands;

            private void Execute(in TrackBinding binding, in SteerData data)
            {
                if (binding.Value == Entity.Null)
                {
                    return;
                }

                this.Commands.Enqueue(new NavCommand
                {
                    Agent = binding.Value,
                    Value = data.Direction,
                    FilterType = data.QueryFilterType,
                    IsDirection = true,
                    SetTarget = true,
                    SetEnable = true,
                    Enable = true,
                });
            }
        }

        [BurstCompile]
        [WithDisabled(typeof(ClipActive))]
        [WithAll(typeof(ClipActivePrevious))]
        private partial struct SteerExitJob : IJobEntity
        {
            public NativeQueue<NavCommand>.ParallelWriter Commands;

            private void Execute(in TrackBinding binding, in SteerData data)
            {
                if (!data.StopOnExit || binding.Value == Entity.Null)
                {
                    return;
                }

                this.Commands.Enqueue(new NavCommand { Agent = binding.Value, SetEnable = true, Enable = false });
            }
        }

        [BurstCompile]
        [WithAll(typeof(ClipActive), typeof(StopData))]
        [WithPresent(typeof(ClipActivePrevious))] // enter frame has ClipActivePrevious disabled; match it anyway and read the state below.
        private partial struct StopJob : IJobEntity
        {
            public NativeQueue<NavCommand>.ParallelWriter Commands;

            private void Execute(in TrackBinding binding, EnabledRefRO<ClipActivePrevious> activePrev)
            {
                // Fire once, on the frame the clip becomes active.
                if (binding.Value == Entity.Null || activePrev.ValueRO)
                {
                    return;
                }

                this.Commands.Enqueue(new NavCommand { Agent = binding.Value, SetEnable = true, Enable = false });
            }
        }

        [BurstCompile]
        private struct ApplyJob : IJob
        {
            public NativeQueue<NavCommand> Commands;
            public ComponentLookup<CrowdAgentData> AgentData;
            public ComponentLookup<IsPathfinding> Pathfinding;

            public void Execute()
            {
                while (this.Commands.TryDequeue(out var c))
                {
                    if (c.SetTarget && this.AgentData.HasComponent(c.Agent))
                    {
                        var d = this.AgentData[c.Agent];
                        if (c.IsDirection)
                        {
                            d.TargetDirection = c.Value;
                        }
                        else
                        {
                            d.TargetPosition = c.Value;
                            d.TargetPositionExtents = c.Extents;
                            d.QueryFilterType = c.FilterType;
                        }

                        this.AgentData[c.Agent] = d;
                    }

                    if (c.SetEnable && this.Pathfinding.HasComponent(c.Agent))
                    {
                        this.Pathfinding.SetComponentEnabled(c.Agent, c.Enable);
                    }
                }
            }
        }
    }
}
