#if UNITY_EDITOR
namespace Vex.NavDemo.Authoring
{
    using Unity.Entities;
    using UnityEngine;

    /// <summary> Put on a Traverse agent alongside MoveAgentAuthoring; bakes the fixed seek destination. </summary>
    public class NavDemoTargetAuthoring : MonoBehaviour
    {
        [Tooltip("World-space point the agent will path to.")]
        public Vector3 Target;

        private class Baker : Baker<NavDemoTargetAuthoring>
        {
            public override void Bake(NavDemoTargetAuthoring authoring)
            {
                var entity = this.GetEntity(TransformUsageFlags.Dynamic);
                this.AddComponent(entity, new NavDemoSeek { Target = authoring.Target });
            }
        }
    }
}
#endif