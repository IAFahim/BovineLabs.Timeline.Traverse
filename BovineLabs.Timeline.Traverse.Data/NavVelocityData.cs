using BovineLabs.Movement.Data;
using BovineLabs.Timeline.Data;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Properties;

namespace BovineLabs.Timeline.Traverse.Data
{
    /// <summary>
    /// Raw authored clip velocity for a NavVelocity clip, resolved to a world-space XZ value into
    /// <see cref="NavVelocityAnimated" /> every frame by NavVelocitySystem (local velocities rotate with the agent).
    /// </summary>
    public struct NavVelocityAuthored : IComponentData
    {
        /// <summary> Velocity in m/s. Y is ignored; movement is on the XZ ground plane. </summary>
        public float3 Velocity;

        /// <summary> Interpret <see cref="Velocity" /> relative to the agent's current facing instead of world axes. </summary>
        public bool Local;
    }

    /// <summary> Resolved world-space XZ velocity on a NavVelocity clip entity, consumed by the blend. </summary>
    public struct NavVelocityAnimated : IAnimatedComponent<float2>
    {
        /// <summary> Gets or sets the resolved world-space XZ velocity. </summary>
        [CreateProperty]
        public float2 Value { get; set; }
    }

    /// <summary>
    /// Marker for agents whose <see cref="DesiredVelocity" /> is being manually driven by the timeline. The
    /// <see cref="WriteGroupAttribute" /> excludes the navigation pipeline's velocity writer while present, so the
    /// timeline value survives the frame; removal hands control straight back (MoveTo target / IsPathfinding are
    /// untouched and resume the instant it ends). Added/removed structurally via ECB — WriteGroup matching is
    /// archetype-based, so an enableable component would NOT work here.
    /// </summary>
    [WriteGroup(typeof(DesiredVelocity))]
    public struct NavManualDrive : IComponentData
    {
    }
}
