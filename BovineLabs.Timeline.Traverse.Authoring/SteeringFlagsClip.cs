using BovineLabs.Core.Authoring.EntityCommands;
using BovineLabs.Movement.Data;
using BovineLabs.Timeline.Authoring;
using BovineLabs.Timeline.Traverse.Data;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Traverse.Authoring
{
    /// <summary>
    /// While active, replaces the agent's crowd steering flags (turn anticipation / path optimization),
    /// restoring the captured original on exit.
    /// </summary>
    public sealed class SteeringFlagsClip : DOTSClip, ITimelineClipAsset
    {
        [Header("Steering Flags")]
        [Tooltip(
            "Crowd steering flags applied while the clip is active (replaces the agent's UpdateFlags). Anticipate Turns smooths corner approach; Optimize Vis raycast-shortcuts the path; Optimize Topo re-optimizes the corridor through complex areas.")]
        public UpdateFlags updateFlags =
            UpdateFlags.CrowdAnticipateTurns | UpdateFlags.CrowdOptimizeVis | UpdateFlags.CrowdOptimizeTopo;

        [Header("Lifecycle")]
        [Tooltip("Restore the agent's original flags when the clip ends.")]
        public bool restoreOnExit = true;

        public override double duration => 1;

        public ClipCaps clipCaps => ClipCaps.None;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            var commands = new BakerCommands(context.Baker, clipEntity);
            commands.AddComponent(new SteeringFlagsData
            {
                Flags = (byte)updateFlags,
                RestoreOnExit = restoreOnExit,
            });
            commands.AddComponent(default(SteeringFlagsState));

            base.Bake(clipEntity, context);
        }
    }
}
