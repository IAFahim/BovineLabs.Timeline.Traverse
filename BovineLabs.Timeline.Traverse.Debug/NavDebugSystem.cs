// <copyright file="NavDebugSystem.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

#if UNITY_EDITOR || BL_DEBUG
namespace BovineLabs.Timeline.Traverse.Debug
{
    using BovineLabs.Core;
    using BovineLabs.Core.ConfigVars;
    using BovineLabs.Core.Extensions;
    using BovineLabs.Core.Iterators;
    using BovineLabs.Movement.Data;
    using BovineLabs.Quill;
    using BovineLabs.Reaction.Data.Core;
    using BovineLabs.Timeline.Core.Debug;
    using BovineLabs.Timeline.Data;
    using BovineLabs.Timeline.Traverse.Data;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;
    using Unity.Transforms;
    using UnityEngine;

    /// <summary> ConfigVar toggle for the Traverse nav clip drawer. </summary>
    [Configurable]
    public static class NavDebugSystemConfig
    {
        /// <summary> Enable the Traverse navigation debug drawer. </summary>
        [ConfigVar("bovinelabs.timeline.traverse.debug.draw", false,
            "Draw active Traverse navigation clips (MoveTo/Steer/Patrol/NavVelocity) via Quill.")]
        public static readonly SharedStatic<bool> Enabled =
            SharedStatic<bool>.GetOrCreate<Tags.Enabled>();

        private struct Tags
        {
            public struct Enabled
            {
            }
        }
    }

    /// <summary>
    /// Quill gizmos for active Traverse clips: MoveTo destination sphere + line + stop-distance ring,
    /// Steer/NavVelocity arrows, Patrol route polyline with waypoint indices and arrive-radius circles.
    /// Zero cost while the drawer is disabled; detail LODs via <see cref="TimelineDebugTier" />.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation |
                       WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(DebugSystemGroup))]
    public partial struct NavDebugSystem : ISystem
    {
        private UnsafeComponentLookup<LocalToWorld> ltwLookup;
        private UnsafeComponentLookup<CrowdAgentData> crowdLookup;
#if BL_ESSENCE
        private UnsafeComponentLookup<CrowdAgentOutput> outputLookup;
#endif

        /// <inheritdoc />
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DrawSystem.Singleton>();
            this.ltwLookup = state.GetUnsafeComponentLookup<LocalToWorld>(true);
            this.crowdLookup = state.GetUnsafeComponentLookup<CrowdAgentData>(true);
#if BL_ESSENCE
            this.outputLookup = state.GetUnsafeComponentLookup<CrowdAgentOutput>(true);
#endif
        }

        /// <inheritdoc />
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!TimelineDebugUtility.TryGetDrawer<NavDebugSystem>(
                    ref state, NavDebugSystemConfig.Enabled.Data, out var drawer, out var viewer, out var hasViewer))
            {
                return;
            }

            this.ltwLookup.Update(ref state);
            this.crowdLookup.Update(ref state);
#if BL_ESSENCE
            this.outputLookup.Update(ref state);
#endif

            var dependency = new DrawMoveToJob
            {
                Drawer = drawer,
                Viewer = viewer,
                HasViewer = hasViewer,
                LtwLookup = this.ltwLookup,
                CrowdLookup = this.crowdLookup,
#if BL_ESSENCE
                OutputLookup = this.outputLookup,
#endif
            }.Schedule(state.Dependency);

            dependency = new DrawSteerJob
            {
                Drawer = drawer,
                Viewer = viewer,
                HasViewer = hasViewer,
                LtwLookup = this.ltwLookup,
            }.Schedule(dependency);

            dependency = new DrawNavVelocityJob
            {
                Drawer = drawer,
                Viewer = viewer,
                HasViewer = hasViewer,
                LtwLookup = this.ltwLookup,
            }.Schedule(dependency);

            state.Dependency = new DrawPatrolJob
            {
                Drawer = drawer,
                Viewer = viewer,
                HasViewer = hasViewer,
                LtwLookup = this.ltwLookup,
            }.Schedule(dependency);
        }

        /// <summary> MoveTo: destination sphere, agent→destination line, stop-distance ring, halt state. </summary>
        [BurstCompile]
        [WithAll(typeof(ClipActive))]
        private partial struct DrawMoveToJob : IJobEntity
        {
            private static readonly Color DestColor = new(0.2f, 0.9f, 0.3f, 0.9f);
            private static readonly Color HaltColor = new(0.6f, 0.6f, 0.6f, 0.9f);
            private static readonly Color RingColor = new(1.0f, 0.6f, 0.1f, 0.7f);

            public Drawer Drawer;
            public float3 Viewer;
            public bool HasViewer;
            [ReadOnly]
            public UnsafeComponentLookup<LocalToWorld> LtwLookup;
            [ReadOnly]
            public UnsafeComponentLookup<CrowdAgentData> CrowdLookup;
#if BL_ESSENCE
            [ReadOnly]
            public UnsafeComponentLookup<CrowdAgentOutput> OutputLookup;
#endif

            private void Execute(in TrackBinding binding, in MoveToData data, in MoveToState moveState)
            {
                if (binding.Value == Entity.Null || !this.LtwLookup.TryGetComponent(binding.Value, out var ltw))
                {
                    return;
                }

                var agentPos = ltw.Position;

                // Only draw a destination that is actually in effect: the latched point for a delivered
                // non-follow move, the live corridor goal for everything else with a crowd agent, or the
                // authored world point as a last resort. An unresolved entity target draws nothing.
                float3 dest;
                if (!data.Follow && moveState.Delivered)
                {
                    dest = moveState.Destination;
                }
                else if (this.CrowdLookup.TryGetComponent(binding.Value, out var crowd))
                {
                    dest = crowd.TargetPosition;
                }
                else if (data.Destination == Target.None)
                {
                    dest = data.WorldPosition;
                }
                else
                {
                    return;
                }

                var color = moveState.Halted ? HaltColor : DestColor;
                var lift = new float3(0f, 0.05f, 0f);

                this.Drawer.Sphere(dest, 0.2f, 12, color);
                this.Drawer.Line(agentPos + lift, dest + lift, color);

                if (data.StopDistance > 0f)
                {
                    this.Drawer.Circle(dest + lift, math.up() * data.StopDistance, RingColor);
                }

                var tier = TimelineDebugTier.Resolve(agentPos, this.Viewer, this.HasViewer);
                if (tier >= DebugTier.Mid)
                {
                    this.Drawer.Text32(
                        dest + new float3(0f, 0.4f, 0f),
                        moveState.Halted ? (FixedString32Bytes)"MoveTo HALT" : (FixedString32Bytes)"MoveTo",
                        TimelineDebugColors.Label, 12f);
                }

#if BL_ESSENCE
                if (tier == DebugTier.Close && this.OutputLookup.TryGetComponent(binding.Value, out var output))
                {
                    var text = new FixedString128Bytes();
                    text.Append(output.Speed);
                    text.Append((FixedString32Bytes)" m/s");
                    this.Drawer.Text128(agentPos + new float3(0f, 2.2f, 0f), text, TimelineDebugColors.Label, 11f);
                }
#endif
            }
        }

        /// <summary> Steer: world-space direction arrow from the agent. </summary>
        [BurstCompile]
        [WithAll(typeof(ClipActive))]
        private partial struct DrawSteerJob : IJobEntity
        {
            private static readonly Color SteerColor = new(1.0f, 0.9f, 0.1f, 0.9f);

            public Drawer Drawer;
            public float3 Viewer;
            public bool HasViewer;
            [ReadOnly]
            public UnsafeComponentLookup<LocalToWorld> LtwLookup;

            private void Execute(in TrackBinding binding, in SteerData data)
            {
                if (binding.Value == Entity.Null || !this.LtwLookup.TryGetComponent(binding.Value, out var ltw))
                {
                    return;
                }

                var origin = ltw.Position + new float3(0f, 0.5f, 0f);
                this.Drawer.Arrow(origin, data.Direction, SteerColor);

                if (TimelineDebugTier.Resolve(ltw.Position, this.Viewer, this.HasViewer) >= DebugTier.Mid)
                {
                    this.Drawer.Text32(origin + new float3(0f, 0.3f, 0f), "Steer", TimelineDebugColors.Label, 12f);
                }
            }
        }

        /// <summary> NavVelocity: resolved world-space XZ velocity arrow from the agent. </summary>
        [BurstCompile]
        [WithAll(typeof(ClipActive))]
        private partial struct DrawNavVelocityJob : IJobEntity
        {
            private static readonly Color VelocityColor = new(0.0f, 1.0f, 1.0f, 0.9f);

            public Drawer Drawer;
            public float3 Viewer;
            public bool HasViewer;
            [ReadOnly]
            public UnsafeComponentLookup<LocalToWorld> LtwLookup;

            private void Execute(in TrackBinding binding, in NavVelocityAnimated animated)
            {
                if (binding.Value == Entity.Null || !this.LtwLookup.TryGetComponent(binding.Value, out var ltw))
                {
                    return;
                }

                var velocity = animated.Value;
                if (math.lengthsq(velocity) < 1e-6f)
                {
                    return;
                }

                var origin = ltw.Position + new float3(0f, 1f, 0f);
                this.Drawer.Arrow(origin, new float3(velocity.x, 0f, velocity.y) * 0.5f, VelocityColor);

                if (TimelineDebugTier.Resolve(ltw.Position, this.Viewer, this.HasViewer) >= DebugTier.Mid)
                {
                    this.Drawer.Text32(origin + new float3(0f, 0.3f, 0f), "NavVel", TimelineDebugColors.Label, 12f);
                }
            }
        }

        /// <summary> Patrol: route polyline, waypoint indices, arrive-radius circles, phase readout. </summary>
        [BurstCompile]
        [WithAll(typeof(ClipActive))]
        private partial struct DrawPatrolJob : IJobEntity
        {
            private static readonly Color RouteColor = new(0.1f, 0.85f, 0.75f, 0.7f);
            private static readonly Color CurrentColor = new(1.0f, 0.9f, 0.1f, 0.9f);
            private static readonly Color RingColor = new(0.1f, 0.85f, 0.75f, 0.35f);

            public Drawer Drawer;
            public float3 Viewer;
            public bool HasViewer;
            [ReadOnly]
            public UnsafeComponentLookup<LocalToWorld> LtwLookup;

            private void Execute(
                in TrackBinding binding, in PatrolData data, in PatrolState patrolState,
                DynamicBuffer<PatrolWaypoint> waypoints)
            {
                if (waypoints.Length == 0 || binding.Value == Entity.Null ||
                    !this.LtwLookup.TryGetComponent(binding.Value, out var ltw))
                {
                    return;
                }

                var lift = new float3(0f, 0.05f, 0f);

                for (var i = 1; i < waypoints.Length; i++)
                {
                    this.Drawer.Line(waypoints[i - 1].Position + lift, waypoints[i].Position + lift, RouteColor);
                }

                if (data.Mode == PatrolMode.Loop && waypoints.Length > 2)
                {
                    this.Drawer.Line(
                        waypoints[waypoints.Length - 1].Position + lift, waypoints[0].Position + lift, RouteColor);
                }

                var tier = TimelineDebugTier.Resolve(ltw.Position, this.Viewer, this.HasViewer);

                for (var i = 0; i < waypoints.Length; i++)
                {
                    var pos = waypoints[i].Position;
                    var isCurrent = i == patrolState.Index && patrolState.Phase != PatrolPhase.Done;

                    this.Drawer.Point(pos + lift, isCurrent ? 0.15f : 0.08f, isCurrent ? CurrentColor : RouteColor);

                    if (data.ArriveRadius > 0f && tier >= DebugTier.Mid)
                    {
                        this.Drawer.Circle(pos + lift, math.up() * data.ArriveRadius, RingColor);
                    }

                    if (tier == DebugTier.Close)
                    {
                        var label = new FixedString32Bytes();
                        label.Append(i);
                        if (waypoints[i].Wait > 0f)
                        {
                            label.Append((FixedString32Bytes)" w");
                            label.Append(waypoints[i].Wait);
                        }

                        this.Drawer.Text32(pos + new float3(0f, 0.35f, 0f), label, TimelineDebugColors.Label, 11f);
                    }
                }

                if (tier >= DebugTier.Mid)
                {
                    var text = new FixedString128Bytes();
                    text.Append((FixedString32Bytes)"Patrol ");
                    switch (patrolState.Phase)
                    {
                        case PatrolPhase.Moving:
                            text.Append((FixedString32Bytes)"Moving ");
                            break;
                        case PatrolPhase.Waiting:
                            text.Append((FixedString32Bytes)"Waiting ");
                            break;
                        case PatrolPhase.Done:
                            text.Append((FixedString32Bytes)"Done ");
                            break;
                    }

                    text.Append(patrolState.Index);
                    text.Append('/');
                    text.Append(waypoints.Length);
                    this.Drawer.Text128(ltw.Position + new float3(0f, 1.8f, 0f), text, TimelineDebugColors.Label, 12f);
                }
            }
        }
    }
}
#endif
