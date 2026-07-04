namespace Vex.NavDemo
{
    using Unity.Entities;

    /// <summary> Demo: the entity this agent should keep chasing across the navmesh. </summary>
    public struct ChaseTarget : IComponentData
    {
        public Entity Value;

        /// <summary> Stop pathing when within this distance of the target (prevents arrival jitter). </summary>
        public float StopDistance;
    }
}
