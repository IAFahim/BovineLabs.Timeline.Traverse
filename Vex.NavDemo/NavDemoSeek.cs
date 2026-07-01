namespace Vex.NavDemo
{
    using Unity.Entities;
    using Unity.Mathematics;

    /// <summary> Demo: the fixed world point this agent should keep pathing toward. </summary>
    public struct NavDemoSeek : IComponentData
    {
        public float3 Target;
    }
}
