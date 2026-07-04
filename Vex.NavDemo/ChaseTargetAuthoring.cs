#if UNITY_EDITOR
namespace Vex.NavDemo.Authoring
{
    using Unity.Entities;
    using UnityEngine;

    /// <summary> Put on a Traverse agent alongside MoveAgentAuthoring; bakes the entity the agent chases. </summary>
    public class ChaseTargetAuthoring : MonoBehaviour
    {
        [Tooltip("Scene object the agent will keep pathing toward. Must be in the same SubScene.")]
        public GameObject Target;

        [Tooltip("Stop pathing when within this distance of the target.")]
        public float StopDistance = 1.5f;

        private class Baker : Baker<ChaseTargetAuthoring>
        {
            public override void Bake(ChaseTargetAuthoring authoring)
            {
                var entity = this.GetEntity(TransformUsageFlags.Dynamic);
                this.AddComponent(entity, new ChaseTarget
                {
                    Value = this.GetEntity(authoring.Target, TransformUsageFlags.Dynamic),
                    StopDistance = authoring.StopDistance,
                });
            }
        }
    }
}
#endif
