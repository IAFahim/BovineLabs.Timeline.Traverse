#if BL_ESSENCE
using BovineLabs.Core.Authoring.EntityCommands;
using BovineLabs.Timeline.Authoring;
using BovineLabs.Timeline.Traverse.Data;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Traverse.Authoring
{
    /// <summary>
    /// While active, multiplies the bound agent's movement stats (Multiplicative modifiers, never stacking with
    /// itself). Stat keys are resolved from the MovementStatsConfig singleton at runtime — no schema setup here.
    /// Overlapping clips blend; ease-in/out handles ramp the multipliers from/to 1.
    /// </summary>
    public sealed class NavSpeedClip : DOTSClip, ITimelineClipAsset
    {
        [Header("Multipliers")]
        [Min(0f)]
        [Tooltip("Move speed multiplier while active (1 = unchanged). Ease handles ramp it in/out from 1.")]
        public float speedMultiplier = 1f;

        [Min(0f)]
        [Tooltip("Acceleration multiplier while active (1 = unchanged). Ease handles ramp it in/out from 1.")]
        public float accelMultiplier = 1f;

        [Min(0f)]
        [Tooltip("Turn (rotation speed) multiplier while active (1 = unchanged). Ease handles ramp it in/out from 1.")]
        public float turnMultiplier = 1f;

        public override double duration => 1;

        public ClipCaps clipCaps => ClipCaps.Blending;

        /// <inheritdoc />
        public override void Bake(Entity clipEntity, BakingContext context)
        {
            var commands = new BakerCommands(context.Baker, clipEntity);
            commands.AddComponent(new NavSpeedAnimated
            {
                Value = new NavSpeedMultipliers
                {
                    Speed = this.speedMultiplier,
                    Accel = this.accelMultiplier,
                    Turn = this.turnMultiplier,
                },
            });

            base.Bake(clipEntity, context);
        }
    }
}
#endif
