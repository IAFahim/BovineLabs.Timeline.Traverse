using BovineLabs.Core.Authoring.EntityCommands;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Authoring;
using BovineLabs.Timeline.Traverse.Data;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Traverse.Authoring
{
    public sealed class MoveToClip : DOTSClip, ITimelineClipAsset
    {
        [Header("Destination")]
        [Tooltip(
            "Entity to path toward, resolved from the bound agent's Targets. Set to None to use the World Position below instead.")]
        public Target destination = Target.Target;

        [Tooltip("World-space point to path toward when Destination is None.")]
        public Vector3 worldPosition;

        [Tooltip("Re-resolve and re-write the destination every active frame. Required to chase a moving target.")]
        public bool follow;

        [Header("Lifecycle")]
        [Tooltip(
            "Disable pathfinding when the clip ends (halt / hand control back). Leave off to let the agent keep walking to the last destination.")]
        public bool stopOnExit = true;

        [Header("Query")]
        [Min(0f)] [Tooltip("Half-extents of the navmesh snap search box. Zero uses the agent default.")]
        public float extents;

        [Tooltip("Index into the navmesh query filters for this move.")]
        public byte queryFilterType;

        public override double duration => 1;

        public ClipCaps clipCaps => ClipCaps.None;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            var commands = new BakerCommands(context.Baker, clipEntity);
            commands.AddComponent(new MoveToData
            {
                Destination = destination,
                WorldPosition = worldPosition,
                Follow = follow,
                StopOnExit = stopOnExit,
                Extents = (half)extents,
                QueryFilterType = queryFilterType,
            });

            base.Bake(clipEntity, context);
        }
    }
}
