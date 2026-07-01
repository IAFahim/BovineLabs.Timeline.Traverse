using BovineLabs.Reaction.Data.Core;
using Unity.Entities;
using Unity.Mathematics;

namespace BovineLabs.Timeline.Traverse.Data
{
    /// <summary>
    /// Clip payload: path the bound agent toward a world point or a resolved target entity.
    /// Consumed by NavigationSystem, which writes CrowdAgentData.TargetPosition and enables IsPathfinding.
    /// </summary>
    public struct MoveToData : IComponentData
    {
        /// <summary> Destination resolved from the bound agent's Targets. Set to None to use WorldPosition instead. </summary>
        public Target Destination;

        /// <summary> World-space destination, used when Destination == None. </summary>
        public float3 WorldPosition;

        /// <summary> Re-resolve and re-write the destination every active frame. Required to chase a moving target (re-write bumps the change version so the corridor re-plans). </summary>
        public bool Follow;

        /// <summary> Disable IsPathfinding when the clip ends. Off lets the agent keep walking to the last destination. </summary>
        public bool StopOnExit;

        /// <summary> Half-extents of the navmesh snap search box. Zero uses the agent default. </summary>
        public half Extents;

        /// <summary> Index into NavMeshWorlds.Filters for this move's query. </summary>
        public byte QueryFilterType;
    }

    /// <summary>
    /// Clip payload: steer the bound agent in a world-space direction (velocity mode, no corridor goal).
    /// Re-written every active frame. Magnitude scales speed.
    /// </summary>
    public struct SteerData : IComponentData
    {
        /// <summary> World-space steering direction. If not normalized, magnitude scales speed. </summary>
        public float3 Direction;

        /// <summary> Disable IsPathfinding when the clip ends. </summary>
        public bool StopOnExit;

        /// <summary> Index into NavMeshWorlds.Filters. </summary>
        public byte QueryFilterType;
    }

    /// <summary>
    /// Clip payload: halt the bound agent — disables IsPathfinding on enter.
    /// </summary>
    public struct StopData : IComponentData
    {
    }
}
