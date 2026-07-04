using System;
using BovineLabs.Core.Authoring.EntityCommands;
using BovineLabs.Timeline.Authoring;
using BovineLabs.Timeline.Traverse.Data;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Traverse.Authoring
{
    /// <summary>
    /// Walks the bound agent along a waypoint route while active — Once / Loop / PingPong, with an optional
    /// per-waypoint wait. Arrival is a distance check against Arrive Radius.
    /// </summary>
    public sealed class PatrolClip : DOTSClip, ITimelineClipAsset
    {
        [Serializable]
        public struct Waypoint
        {
            [Tooltip("World-space position of this waypoint.")]
            public Vector3 position;

            [Min(0f)]
            [Tooltip("Seconds to stand at this waypoint before moving on. 0 = no stop.")]
            public float wait;
        }

        [Header("Route")]
        [Tooltip("World-space waypoints, visited in order.")]
        public Waypoint[] waypoints = Array.Empty<Waypoint>();

        [Tooltip("Once walks the route and stops at the last waypoint. Loop wraps back to the first. Ping Pong reverses at each end.")]
        public PatrolMode mode = PatrolMode.Loop;

        [Min(0.05f)]
        [Tooltip("How close (metres) counts as reaching a waypoint.")]
        public float arriveRadius = 0.5f;

        [Header("Lifecycle")]
        [Tooltip("Disable pathfinding when the clip ends (halt / hand control back).")]
        public bool stopOnExit = true;

        [Header("Query")]
        [Min(0f)] [Tooltip("Half-extents of the navmesh snap search box. Zero uses the agent default.")]
        public float extents;

        [Tooltip("Index into the navmesh query filters for this route.")]
        public byte queryFilterType;

        public override double duration => 1;

        public ClipCaps clipCaps => ClipCaps.None;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            var commands = new BakerCommands(context.Baker, clipEntity);
            commands.AddComponent(new PatrolData
            {
                Mode = mode,
                ArriveRadius = arriveRadius,
                StopOnExit = stopOnExit,
                Extents = (half)extents,
                QueryFilterType = queryFilterType,
            });
            commands.AddComponent(default(PatrolState));

            var buffer = commands.AddBuffer<PatrolWaypoint>();
            foreach (var waypoint in waypoints)
            {
                buffer.Add(new PatrolWaypoint { Position = waypoint.position, Wait = waypoint.wait });
            }

            base.Bake(clipEntity, context);
        }
    }
}
