using BovineLabs.Reaction.Data.Core;
using Unity.Entities;
using Unity.Mathematics;

namespace BovineLabs.Timeline.Traverse.Data
{
    /// <summary> What navigation does after a warp. </summary>
    public enum WarpResume : byte
    {
        /// <summary> Stop navigating after the warp. </summary>
        Halt,

        /// <summary> Re-issue the agent's current nav target so the corridor replans from the new position. </summary>
        ResumeCurrentTarget,
    }

    /// <summary>
    /// Clip payload: teleport the bound agent on the clip's enter edge. Retries across active frames until the
    /// destination (and face target) resolve, then fires exactly once per activation.
    /// Consumed by NavigationSystem, which writes LocalTransform (and optionally zeroes PhysicsVelocity).
    /// </summary>
    public struct WarpData : IComponentData
    {
        /// <summary> Entity to warp to, resolved from the bound agent's Targets. None uses WorldPosition. </summary>
        public Target Destination;

        /// <summary> World-space warp point, used when Destination == None. </summary>
        public float3 WorldPosition;

        /// <summary> Face a direction/target after warping. </summary>
        public bool Reorient;

        /// <summary> Whom to face after the warp. None uses FaceDirection. </summary>
        public Target FaceTarget;

        /// <summary> World-space facing, used when FaceTarget == None. </summary>
        public float3 FaceDirection;

        /// <summary> Halt, or replan to the current nav target from the new position. </summary>
        public WarpResume Resume;

        /// <summary> Zero PhysicsVelocity so momentum doesn't carry through the warp (physics agents). </summary>
        public bool ZeroVelocity;
    }

    /// <summary> Per-clip one-shot latch for <see cref="WarpData" />; re-armed on each activation edge. </summary>
    public struct WarpState : IComponentData
    {
        public bool Fired;
    }
}
