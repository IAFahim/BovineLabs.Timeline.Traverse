using BovineLabs.Avoidance.Data;
using Unity.Entities;

namespace BovineLabs.Timeline.Traverse.Avoidance
{
    /// <summary>
    /// Clip payload: while active, override selected fields of the agent's <see cref="ORCAAgent" />, restoring the
    /// captured originals on exit. Lives in its own assembly (mirroring BovineLabs.Avoidance.Data's
    /// defineConstraints) so the main Traverse runtime stays valid in NO_AVOIDANCE client builds.
    /// </summary>
    public struct AvoidanceOverrideData : IComponentData
    {
        /// <summary> Override <see cref="ORCAAgent.ManuallyControlled" /> while active. </summary>
        public bool OverrideManuallyControlled;

        /// <summary> Replacement manual-control state. </summary>
        public bool ManuallyControlled;

        /// <summary> Override <see cref="ORCAAgent.Layer" /> while active. </summary>
        public bool OverrideLayer;

        /// <summary> Replacement layer bits. </summary>
        public byte Layer;

        /// <summary> Override <see cref="ORCAAgent.CollidesWith" /> while active. </summary>
        public bool OverrideCollidesWith;

        /// <summary> Replacement collides-with mask. </summary>
        public byte CollidesWith;

        /// <summary> Override <see cref="ORCAAgent.Priority" /> while active. </summary>
        public bool OverridePriority;

        /// <summary> Replacement priority (0-255). </summary>
        public byte Priority;

        /// <summary> Restore the agent's original values on clip end. </summary>
        public bool RestoreOnExit;
    }

    /// <summary>
    /// Per-clip capture of the agent's pre-override <see cref="ORCAAgent" />; captured on each activation edge.
    /// Restore writes back only the fields the clip overrides.
    /// </summary>
    public struct AvoidanceOverrideState : IComponentData
    {
        public bool Captured;
        public ORCAAgent Original;
    }
}
