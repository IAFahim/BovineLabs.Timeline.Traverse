using BovineLabs.Movement.Data;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.Traverse.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Traverse
{
    /// <summary>
    /// Consumes the Navigation track clips (<see cref="MoveToData" />, <see cref="SteerData" />, <see cref="StopData" />,
    /// <see cref="WarpData" />, <see cref="PatrolData" />) and drives the bound Traverse agent by writing
    /// <see cref="CrowdAgentData" />, toggling <see cref="IsPathfinding" />, and (warp) writing LocalTransform /
    /// zeroing PhysicsVelocity. Gathers per-clip commands in parallel, then applies them on a single thread
    /// (last write per agent wins).
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

            // Warp extras.
            public float3 Position;
            public quaternion Rotation;
            public bool SetPosition;
            public bool SetRotation;
            public bool ZeroVelocity;
            public bool ReissueTarget;
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

            state.Dependency = new WarpJob
            {
                Commands = commands.AsParallelWriter(),
                TargetsLookup = SystemAPI.GetComponentLookup<Targets>(true),
                LtwLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true),
            }.ScheduleParallel(state.Dependency);

            state.Dependency = new PatrolJob
            {
                Commands = commands.AsParallelWriter(),
                LtwLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true),
                DeltaTime = SystemAPI.Time.DeltaTime,
            }.ScheduleParallel(state.Dependency);

            state.Dependency = new PatrolExitJob
            {
                Commands = commands.AsParallelWriter(),
            }.ScheduleParallel(state.Dependency);

            state.Dependency = new ApplyJob
            {
                Commands = commands,
                AgentData = SystemAPI.GetComponentLookup<CrowdAgentData>(false),
                Pathfinding = SystemAPI.GetComponentLookup<IsPathfinding>(false),
                Transforms = SystemAPI.GetComponentLookup<LocalTransform>(false),
                Velocities = SystemAPI.GetComponentLookup<PhysicsVelocity>(false),
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

            private void Execute(in TrackBinding binding, in MoveToData data, ref MoveToState state,
                EnabledRefRO<ClipActivePrevious> activePrev)
            {
                var agent = binding.Value;
                if (agent == Entity.Null)
                {
                    return;
                }

                // Re-arm on the activation edge so a re-entry re-delivers.
                var isFirstFrame = !activePrev.ValueRO;
                if (isFirstFrame)
                {
                    state.Delivered = false;
                    state.Halted = false;
                }

                // A non-follow move that already stopped short stays halted for the rest of this activation.
                if (state.Halted)
                {
                    return;
                }

                // Non-Follow without a stop distance: deliver once, but RETRY across active frames until the target
                // resolves (a single enter-frame miss no longer loses the move). Follow: re-write every frame to
                // chase a moving target. Non-follow WITH a stop distance keeps distance-checking the latched
                // destination until it halts.
                if (!data.Follow && state.Delivered && data.StopDistance <= 0)
                {
                    return;
                }

                float3 pos;
                if (!data.Follow && state.Delivered)
                {
                    // Delivered non-follow: check against the latched destination so later target movement
                    // can't re-route a move that was already issued.
                    pos = state.Destination;
                }
                else if (data.Destination == Target.None)
                {
                    pos = data.WorldPosition;
                }
                else
                {
                    if (!TargetsLookup.TryGetComponent(agent, out var targets))
                    {
                        return; // unresolved this frame; Delivered stays false → retry next frame
                    }

                    var dest = targets.Get(data.Destination, agent);
                    if (dest == Entity.Null || !LtwLookup.TryGetComponent(dest, out var ltw))
                    {
                        return; // unresolved this frame; Delivered stays false → retry next frame
                    }

                    pos = ltw.Position;
                }

                if (!data.Follow)
                {
                    state.Destination = pos;
                }

                // Stop-distance: hold while inside the radius. A follow resumes when the destination moves back
                // out of range (per-frame re-evaluation); a non-follow move latches Halted and stays stopped.
                if (data.StopDistance > 0 &&
                    this.LtwLookup.TryGetComponent(agent, out var agentLtw) &&
                    math.distancesq(agentLtw.Position, pos) <= data.StopDistance * data.StopDistance)
                {
                    this.Commands.Enqueue(new NavCommand { Agent = agent, SetEnable = true, Enable = false });
                    if (!data.Follow)
                    {
                        state.Halted = true;
                    }

                    return;
                }

                if (data.Follow || !state.Delivered)
                {
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

                    if (!data.Follow)
                    {
                        state.Delivered = true;
                    }
                }
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
        [WithAll(typeof(ClipActive))]
        [WithPresent(typeof(ClipActivePrevious))]
        private partial struct WarpJob : IJobEntity
        {
            public NativeQueue<NavCommand>.ParallelWriter Commands;

            [ReadOnly]
            public ComponentLookup<Targets> TargetsLookup;

            [ReadOnly]
            public ComponentLookup<LocalToWorld> LtwLookup;

            private void Execute(in TrackBinding binding, in WarpData data, ref WarpState state,
                EnabledRefRO<ClipActivePrevious> activePrev)
            {
                var agent = binding.Value;
                if (agent == Entity.Null)
                {
                    return;
                }

                // Re-arm on the activation edge so a re-entry warps again.
                if (!activePrev.ValueRO)
                {
                    state.Fired = false;
                }

                if (state.Fired)
                {
                    return;
                }

                // Resolve the warp point; unresolved → retry next active frame (a single enter-frame
                // miss must not silently drop the teleport).
                float3 pos;
                if (data.Destination == Target.None)
                {
                    pos = data.WorldPosition;
                }
                else if (!this.TryResolve(agent, data.Destination, out pos))
                {
                    return;
                }

                var rotation = quaternion.identity;
                if (data.Reorient)
                {
                    float3 direction;
                    if (data.FaceTarget == Target.None)
                    {
                        direction = data.FaceDirection;
                    }
                    else
                    {
                        if (!this.TryResolve(agent, data.FaceTarget, out var facePos))
                        {
                            return; // retry until BOTH the destination and the face target resolve
                        }

                        direction = facePos - pos;
                    }

                    direction.y = 0f;
                    rotation = quaternion.LookRotationSafe(direction, math.up());
                }

                var resume = data.Resume == WarpResume.ResumeCurrentTarget;
                this.Commands.Enqueue(new NavCommand
                {
                    Agent = agent,
                    SetPosition = true,
                    Position = pos,
                    SetRotation = data.Reorient,
                    Rotation = rotation,
                    ZeroVelocity = data.ZeroVelocity,
                    SetEnable = true,
                    Enable = resume,
                    ReissueTarget = resume,
                });

                state.Fired = true;
            }

            private bool TryResolve(Entity agent, Target target, out float3 position)
            {
                position = default;
                if (!this.TargetsLookup.TryGetComponent(agent, out var targets))
                {
                    return false;
                }

                var entity = targets.Get(target, agent);
                if (entity == Entity.Null || !this.LtwLookup.TryGetComponent(entity, out var ltw))
                {
                    return false;
                }

                position = ltw.Position;
                return true;
            }
        }

        [BurstCompile]
        [WithAll(typeof(ClipActive))]
        [WithPresent(typeof(ClipActivePrevious))]
        private partial struct PatrolJob : IJobEntity
        {
            public NativeQueue<NavCommand>.ParallelWriter Commands;

            [ReadOnly]
            public ComponentLookup<LocalToWorld> LtwLookup;

            public float DeltaTime;

            private void Execute(in TrackBinding binding, in PatrolData data, ref PatrolState state,
                [ReadOnly] DynamicBuffer<PatrolWaypoint> waypoints, EnabledRefRO<ClipActivePrevious> activePrev)
            {
                var agent = binding.Value;
                if (agent == Entity.Null || waypoints.Length == 0)
                {
                    return;
                }

                // Re-arm on the activation edge so a re-entry restarts the route from the top.
                if (!activePrev.ValueRO)
                {
                    state = default;
                    state.Direction = 1;
                }

                if (state.Phase == PatrolPhase.Done)
                {
                    return;
                }

                state.Index = math.clamp(state.Index, 0, waypoints.Length - 1);

                if (state.Phase == PatrolPhase.Waiting)
                {
                    state.WaitTimer -= this.DeltaTime;
                    if (state.WaitTimer > 0f)
                    {
                        return;
                    }

                    state.Phase = PatrolPhase.Moving;
                    if (!TryAdvance(ref state, data.Mode, waypoints.Length))
                    {
                        state.Phase = PatrolPhase.Done;
                        this.Commands.Enqueue(new NavCommand { Agent = agent, SetEnable = true, Enable = false });
                        return;
                    }

                    state.Started = false; // re-issue below (pathfinding was disabled for the wait)
                }
                else if (state.Started &&
                         this.LtwLookup.TryGetComponent(agent, out var agentLtw) &&
                         math.distancesq(agentLtw.Position, waypoints[state.Index].Position) <=
                         data.ArriveRadius * data.ArriveRadius)
                {
                    // Arrived. Deterministic distance check — the vendor IsPathfinding auto-disable is
                    // avoidance-gated and also fires on path failure, so it is not used as the arrival signal.
                    var wait = waypoints[state.Index].Wait;
                    if (wait > 0f)
                    {
                        state.Phase = PatrolPhase.Waiting;
                        state.WaitTimer = wait;
                        this.Commands.Enqueue(new NavCommand { Agent = agent, SetEnable = true, Enable = false });
                        return;
                    }

                    if (!TryAdvance(ref state, data.Mode, waypoints.Length))
                    {
                        state.Phase = PatrolPhase.Done;
                        this.Commands.Enqueue(new NavCommand { Agent = agent, SetEnable = true, Enable = false });
                        return;
                    }

                    state.Started = false; // re-issue below
                }

                if (!state.Started)
                {
                    // Targets are written on advance only — a per-frame re-write would bump CrowdAgentData's
                    // change version and force a corridor replan every frame.
                    this.Commands.Enqueue(new NavCommand
                    {
                        Agent = agent,
                        Value = waypoints[state.Index].Position,
                        Extents = data.Extents,
                        FilterType = data.QueryFilterType,
                        IsDirection = false,
                        SetTarget = true,
                        SetEnable = true,
                        Enable = true,
                    });

                    state.Started = true;
                }
            }

            private static bool TryAdvance(ref PatrolState state, PatrolMode mode, int count)
            {
                switch (mode)
                {
                    case PatrolMode.Once:
                        if (state.Index >= count - 1)
                        {
                            return false;
                        }

                        state.Index += 1;
                        return true;

                    case PatrolMode.Loop:
                        state.Index = (state.Index + 1) % count;
                        return true;

                    default: // PingPong
                        if (count == 1)
                        {
                            return true;
                        }

                        var next = state.Index + state.Direction;
                        if (next < 0 || next >= count)
                        {
                            state.Direction = (sbyte)(-state.Direction);
                            next = state.Index + state.Direction;
                        }

                        state.Index = next;
                        return true;
                }
            }
        }

        [BurstCompile]
        [WithDisabled(typeof(ClipActive))]
        [WithAll(typeof(ClipActivePrevious))]
        private partial struct PatrolExitJob : IJobEntity
        {
            public NativeQueue<NavCommand>.ParallelWriter Commands;

            private void Execute(in TrackBinding binding, in PatrolData data)
            {
                if (!data.StopOnExit || binding.Value == Entity.Null)
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
            public ComponentLookup<LocalTransform> Transforms;
            public ComponentLookup<PhysicsVelocity> Velocities;

            public void Execute()
            {
                // Two passes so an enter (Enable=true) always beats a same-frame exit (Enable=false)
                // on the same agent. Back-to-back MoveTo/Steer clips would otherwise freeze the agent:
                // the outgoing clip's disable is enqueued after (and under FIFO applied after) the
                // incoming clip's enable, and cross-agent enqueue order is nondeterministic across
                // Client/Server worlds. Deferring enables to pass 2 makes the resolution deterministic.
                var enables = new NativeList<Entity>(this.Commands.Count, Allocator.Temp);

                while (this.Commands.TryDequeue(out var c))
                {
                    if (c.SetTarget && this.AgentData.HasComponent(c.Agent))
                    {
                        var d = this.AgentData[c.Agent];
                        if (c.IsDirection)
                        {
                            d.TargetDirection = c.Value;
                            d.QueryFilterType = c.FilterType;
                        }
                        else
                        {
                            d.TargetPosition = c.Value;
                            d.TargetPositionExtents = c.Extents;
                            d.QueryFilterType = c.FilterType;
                        }

                        this.AgentData[c.Agent] = d;
                    }

                    if (c.SetPosition && this.Transforms.HasComponent(c.Agent))
                    {
                        var transform = this.Transforms[c.Agent];
                        transform.Position = c.Position;
                        if (c.SetRotation)
                        {
                            transform.Rotation = c.Rotation;
                        }

                        this.Transforms[c.Agent] = transform;
                    }

                    if (c.ZeroVelocity && this.Velocities.HasComponent(c.Agent))
                    {
                        this.Velocities[c.Agent] = default;
                    }

                    if (c.ReissueTarget && this.AgentData.HasComponent(c.Agent))
                    {
                        // A same-value write still bumps the chunk change version, forcing the vendor
                        // move-request system (change-filtered) to replan from the warped position.
                        var d = this.AgentData[c.Agent];
                        this.AgentData[c.Agent] = d;
                    }

                    if (c.SetEnable && this.Pathfinding.HasComponent(c.Agent))
                    {
                        if (c.Enable)
                        {
                            enables.Add(c.Agent);
                        }
                        else
                        {
                            this.Pathfinding.SetComponentEnabled(c.Agent, false);
                        }
                    }
                }

                for (var i = 0; i < enables.Length; i++)
                {
                    this.Pathfinding.SetComponentEnabled(enables[i], true);
                }
            }
        }
    }
}
