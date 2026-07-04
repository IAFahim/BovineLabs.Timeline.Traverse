using BovineLabs.Avoidance.Data;
using BovineLabs.Core.Authoring.EntityCommands;
using BovineLabs.Timeline.Authoring;
using BovineLabs.Timeline.Traverse.Avoidance;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Traverse.Avoidance.Authoring
{
    /// <summary>
    /// While active, overrides selected fields of the agent's ORCA avoidance settings
    /// (<see cref="ORCAAgent" />), restoring the captured originals on exit. Only fields with their
    /// Override toggle enabled are touched; everything else keeps live gameplay values.
    /// </summary>
    public sealed class AvoidanceOverrideClip : DOTSClip, ITimelineClipAsset
    {
        [Header("Manual Control")]
        [Tooltip("Override whether the agent is manually controlled while the clip is active.")]
        public bool overrideManuallyControlled;

        [Tooltip("When true, the avoidance system will not move the agent (timeline/cutscene has full control).")]
        public bool manuallyControlled = true;

        [Header("Layers")]
        [Tooltip("Override the layer the agent belongs to while the clip is active.")]
        public bool overrideLayer;

        [Tooltip("The layer the agent belongs to for determining collisions.")]
        [AvoidanceLayer(true)]
        public byte layer = 1 << 0;

        [Tooltip("Override the layers this agent collides with while the clip is active.")]
        public bool overrideCollidesWith;

        [Tooltip("The layers this agent collides with for determining collisions.")]
        [AvoidanceLayer(true)]
        public byte collidesWith = byte.MaxValue;

        [Header("Priority")]
        [Tooltip("Override the agent's avoidance priority while the clip is active.")]
        public bool overridePriority;

        [Tooltip("Priority value for collision avoidance (0-1). Higher values give more priority in collision resolution.")]
        [Range(0, 1)]
        public float priority = 0.5f;

        [Header("Lifecycle")]
        [Tooltip("Restore the agent's original values for the overridden fields when the clip ends.")]
        public bool restoreOnExit = true;

        public override double duration => 1;

        public ClipCaps clipCaps => ClipCaps.None;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            var commands = new BakerCommands(context.Baker, clipEntity);
            commands.AddComponent(new AvoidanceOverrideData
            {
                OverrideManuallyControlled = overrideManuallyControlled,
                ManuallyControlled = manuallyControlled,
                OverrideLayer = overrideLayer,
                Layer = layer,
                OverrideCollidesWith = overrideCollidesWith,
                CollidesWith = collidesWith,
                OverridePriority = overridePriority,
                Priority = (byte)(math.clamp(priority, 0, 1) * byte.MaxValue),
                RestoreOnExit = restoreOnExit,
            });
            commands.AddComponent(default(AvoidanceOverrideState));

            base.Bake(clipEntity, context);
        }
    }
}
