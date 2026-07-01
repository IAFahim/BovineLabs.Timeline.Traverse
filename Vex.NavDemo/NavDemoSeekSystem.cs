namespace Vex.NavDemo
{
    using BovineLabs.Movement.Data;
    using Unity.Entities;

    /// <summary>
    /// Demo driver: the Traverse 2-field contract aimed at a fixed point. Whenever an agent isn't pathfinding
    /// (start, or after it arrived and Traverse auto-disabled IsPathfinding) we re-point it at the target and
    /// re-enable pathfinding. This is the shipped WanderSystem pattern with a fixed destination instead of a random one.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation)]
    public partial struct NavDemoSeekSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (seek, agentData, pathfinding) in SystemAPI
                         .Query<RefRO<NavDemoSeek>, RefRW<CrowdAgentData>, EnabledRefRW<IsPathfinding>>()
                         .WithPresent<IsPathfinding>())
            {
                if (pathfinding.ValueRO)
                {
                    continue; // already heading there
                }

                agentData.ValueRW.TargetPosition = seek.ValueRO.Target;
                pathfinding.ValueRW = true;
            }
        }
    }
}
