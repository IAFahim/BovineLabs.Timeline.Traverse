#if BL_ESSENCE
using BovineLabs.Timeline.Data;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Properties;

namespace BovineLabs.Timeline.Traverse.Data
{
    /// <summary>
    /// Speed/acceleration/turn multipliers applied to the bound agent's movement stats while a NavSpeed clip is
    /// active. 1 = no change. Blended across overlapping clips and eased toward identity by clip ease handles.
    /// </summary>
    public struct NavSpeedMultipliers
    {
        /// <summary> Multiplier on the agent's move speed stat. </summary>
        public float Speed;

        /// <summary> Multiplier on the agent's acceleration stat. </summary>
        public float Accel;

        /// <summary> Multiplier on the agent's rotation speed stat. </summary>
        public float Turn;

        /// <summary> The no-op value all blends fall back to when clip weights don't sum to 1. </summary>
        public static NavSpeedMultipliers Identity => new() { Speed = 1f, Accel = 1f, Turn = 1f };
    }

    /// <summary> Animated multiplier state on a NavSpeed clip entity, consumed by NavSpeedSystem's blend. </summary>
    public struct NavSpeedAnimated : IAnimatedComponent<NavSpeedMultipliers>
    {
        /// <summary> Gets or sets the authored multipliers for this clip. </summary>
        [CreateProperty]
        public NavSpeedMultipliers Value { get; set; }
    }

    /// <summary> Per-field lerp/add so multipliers blend independently. </summary>
    public readonly struct NavSpeedMixer : IMixer<NavSpeedMultipliers>
    {
        /// <inheritdoc />
        public NavSpeedMultipliers Lerp(in NavSpeedMultipliers a, in NavSpeedMultipliers b, in float s)
        {
            return new NavSpeedMultipliers
            {
                Speed = math.lerp(a.Speed, b.Speed, s),
                Accel = math.lerp(a.Accel, b.Accel, s),
                Turn = math.lerp(a.Turn, b.Turn, s),
            };
        }

        /// <inheritdoc />
        public NavSpeedMultipliers Add(in NavSpeedMultipliers a, in NavSpeedMultipliers b)
        {
            return new NavSpeedMultipliers
            {
                Speed = a.Speed + b.Speed,
                Accel = a.Accel + b.Accel,
                Turn = a.Turn + b.Turn,
            };
        }
    }

    /// <summary>
    /// Enableable marker on agents that currently carry NavSpeed stat modifiers. Added (enabled) by
    /// NavSpeedSystem on first application; disabled once the agent leaves the blend map and its modifiers are
    /// cleared. Covers both natural clip end and a clip destroyed mid-active.
    /// </summary>
    public struct NavSpeedApplied : IComponentData, IEnableableComponent
    {
    }
}
#endif
