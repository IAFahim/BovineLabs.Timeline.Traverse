using BovineLabs.Core.Authoring.EntityCommands;
using BovineLabs.Timeline.Authoring;
using BovineLabs.Timeline.Traverse.Data;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Traverse.Authoring
{
    /// <summary>
    /// While active, drives the agent's desired velocity directly (crowd avoidance still applies). Wins over any
    /// MoveTo/patrol steering for its duration; navigation resumes the instant it ends. Overlapping clips blend;
    /// ease handles ramp velocity from/to zero.
    /// </summary>
    public sealed class NavVelocityClip : DOTSClip, ITimelineClipAsset
    {
        [Header("Velocity")]
        [Tooltip("Velocity in m/s. Y is ignored — movement is on the XZ ground plane.")]
        public Vector3 velocity = new(0f, 0f, 3.5f);

        [Tooltip("Interpret the velocity relative to the agent's current facing (updates every frame) instead of world axes.")]
        public bool local = true;

        public override double duration => 1;

        public ClipCaps clipCaps => ClipCaps.Blending;

        /// <inheritdoc />
        public override void Bake(Entity clipEntity, BakingContext context)
        {
            var commands = new BakerCommands(context.Baker, clipEntity);
            commands.AddComponent(new NavVelocityAuthored
            {
                Velocity = this.velocity,
                Local = this.local,
            });
            commands.AddComponent(default(NavVelocityAnimated));

            base.Bake(clipEntity, context);
        }
    }
}
