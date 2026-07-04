using Unity.Entities;

namespace BovineLabs.Timeline.Traverse.Data
{
    /// <summary>
    /// Clip payload: while active, replace the agent's CrowdAgent.UpdateFlags steering flags, restoring the
    /// captured original on exit. Stored as a raw byte so this Data assembly stays independent of
    /// BovineLabs.Movement.Data (mirrors QueryFilterType).
    /// </summary>
    public struct SteeringFlagsData : IComponentData
    {
        /// <summary> Replacement UpdateFlags bits applied while the clip is active. </summary>
        public byte Flags;

        /// <summary> Restore the agent's original flags on clip end. </summary>
        public bool RestoreOnExit;
    }

    /// <summary> Per-clip capture of the agent's pre-override flags; captured on each activation edge. </summary>
    public struct SteeringFlagsState : IComponentData
    {
        public bool Captured;
        public byte Original;
    }
}
