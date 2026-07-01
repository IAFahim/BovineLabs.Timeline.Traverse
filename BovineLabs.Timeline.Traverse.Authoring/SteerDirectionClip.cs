using BovineLabs.Core.Authoring.EntityCommands;
using BovineLabs.Timeline.Authoring;
using BovineLabs.Timeline.Traverse.Data;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Traverse.Authoring
{
    public sealed class SteerDirectionClip : DOTSClip, ITimelineClipAsset
    {
        [Header("Steering")]
        [Tooltip(
            "World-space direction to steer in while active. If not normalized, the magnitude scales the agent's speed. Re-applied every active frame.")]
        public Vector3 direction = Vector3.forward;

        [Header("Lifecycle")]
        [Tooltip("Disable pathfinding when the clip ends.")]
        public bool stopOnExit = true;

        [Header("Query")]
        [Tooltip("Index into the navmesh query filters.")]
        public byte queryFilterType;

        public override double duration => 1;

        public ClipCaps clipCaps => ClipCaps.None;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            var commands = new BakerCommands(context.Baker, clipEntity);
            commands.AddComponent(new SteerData
            {
                Direction = direction,
                StopOnExit = stopOnExit,
                QueryFilterType = queryFilterType,
            });

            base.Bake(clipEntity, context);
        }
    }
}
