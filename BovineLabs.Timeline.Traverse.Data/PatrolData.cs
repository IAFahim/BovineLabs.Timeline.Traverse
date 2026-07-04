using Unity.Entities;
using Unity.Mathematics;

namespace BovineLabs.Timeline.Traverse.Data
{
    /// <summary> How a patrol route continues after its last waypoint. </summary>
    public enum PatrolMode : byte
    {
        /// <summary> Walk the route once and stop at the last waypoint. </summary>
        Once,

        /// <summary> Wrap from the last waypoint back to the first. </summary>
        Loop,

        /// <summary> Reverse direction at each end of the route. </summary>
        PingPong,
    }

    /// <summary> Patrol progress phase. </summary>
    public enum PatrolPhase : byte
    {
        Moving,
        Waiting,
        Done,
    }

    /// <summary> One waypoint of a <see cref="PatrolData" /> route, baked onto the clip entity. </summary>
    [InternalBufferCapacity(0)]
    public struct PatrolWaypoint : IBufferElementData
    {
        /// <summary> World-space waypoint position. </summary>
        public float3 Position;

        /// <summary> Seconds to stand at this waypoint before moving on. 0 = no stop. </summary>
        public float Wait;
    }

    /// <summary>
    /// Clip payload: walk the bound agent along the clip's <see cref="PatrolWaypoint" /> buffer.
    /// Arrival is a deterministic distance check against ArriveRadius — NOT the vendor IsPathfinding
    /// auto-disable, which is avoidance-gated and also fires on request failure.
    /// </summary>
    public struct PatrolData : IComponentData
    {
        /// <summary> Once / Loop / PingPong route continuation. </summary>
        public PatrolMode Mode;

        /// <summary> How close (metres) counts as reaching a waypoint. </summary>
        public float ArriveRadius;

        /// <summary> Disable IsPathfinding when the clip ends. </summary>
        public bool StopOnExit;

        /// <summary> Half-extents of the navmesh snap search box. Zero uses the agent default. </summary>
        public half Extents;

        /// <summary> Index into NavMeshWorlds.Filters. </summary>
        public byte QueryFilterType;
    }

    /// <summary> Per-clip runtime patrol state; re-armed on each activation edge. </summary>
    public struct PatrolState : IComponentData
    {
        public int Index;
        public sbyte Direction;
        public float WaitTimer;
        public PatrolPhase Phase;

        /// <summary> The current waypoint's nav target has been written (targets are written on advance, not per frame). </summary>
        public bool Started;
    }
}
