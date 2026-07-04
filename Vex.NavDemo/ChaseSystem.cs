namespace Vex.NavDemo
{
    using BovineLabs.Movement.Data;
    using Unity.Entities;
    using Unity.Mathematics;
    using Unity.Transforms;

    /// <summary>
    /// Demo driver: the Traverse 2-field contract aimed at a moving entity. Re-writes CrowdAgentData.TargetPosition
    /// toward the chased entity every frame (MoveRequestSystem's change-version filter requires the per-frame rewrite
    /// to track a moving target) and halts inside StopDistance so arrival doesn't disable/re-enable jitter.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation)]
    public partial struct ChaseSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var targetLtw = SystemAPI.GetComponentLookup<LocalToWorld>(true);

            foreach (var (chase, transform, agentData, pathfinding) in SystemAPI
                         .Query<RefRO<ChaseTarget>, RefRO<LocalTransform>, RefRW<CrowdAgentData>, EnabledRefRW<IsPathfinding>>()
                         .WithPresent<IsPathfinding>())
            {
                if (!targetLtw.TryGetComponent(chase.ValueRO.Value, out var target))
                {
                    continue;
                }

                var stop = chase.ValueRO.StopDistance;
                if (math.distancesq(transform.ValueRO.Position, target.Position) <= stop * stop)
                {
                    pathfinding.ValueRW = false;
                    continue;
                }

                agentData.ValueRW.TargetPosition = target.Position; // setter also clears IsDirection
                pathfinding.ValueRW = true;
            }
        }
    }
}
