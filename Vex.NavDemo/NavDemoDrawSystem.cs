#if UNITY_EDITOR || BL_DEBUG
namespace Vex.NavDemo
{
    using BovineLabs.Core.ConfigVars;
    using BovineLabs.Movement.Data;
    using BovineLabs.Quill;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;
    using Unity.Transforms;
    using UnityEngine;

    /// <summary>
    /// Quill debug for the nav demo agent — modelled on CombatResolutionDebugSystem. Toggle the
    /// "Vex.NavDemo.NavDemoDrawSystem" drawer in the BovineLabs debug toolbar (or ConfigVar navdemo.draw).
    /// Draws: target sphere, a tall pole over the agent, agent→target arrow, velocity arrow, state text.
    /// </summary>
    [Configurable]
    public static class NavDemoDraw
    {
        [ConfigVar("navdemo.draw", true, "Draw the nav demo agent, its target and velocity via Quill.")]
        public static readonly Unity.Burst.SharedStatic<bool> Enabled =
            Unity.Burst.SharedStatic<bool>.GetOrCreate<EnabledTag>();

        private struct EnabledTag
        {
        }
    }

    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation |
                       WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(BovineLabs.Core.DebugSystemGroup))]
    public partial struct NavDemoDrawSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DrawSystem.Singleton>();
            state.RequireForUpdate<NavDemoSeek>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!NavDemoDraw.Enabled.Data)
            {
                return;
            }

            var drawer = SystemAPI.GetSingleton<DrawSystem.Singleton>().CreateDrawer<NavDemoDrawSystem>();

            state.Dependency = new DrawJob { Drawer = drawer }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        private partial struct DrawJob : IJobEntity
        {
            public Drawer Drawer;

            private void Execute(in NavDemoSeek seek, in CrowdAgentData agent, in CrowdAgentOutput output,
                in LocalTransform transform, EnabledRefRO<IsPathfinding> pathfinding)
            {
                var pos = transform.Position;
                var target = seek.Target;
                var moving = pathfinding.ValueRO;

                // Target: green sphere + ground circle so it reads at any camera angle.
                this.Drawer.Sphere(target, 0.5f, 16, Color.green);
                this.Drawer.Circle(target, math.up(), Color.green);

                // Agent: a tall cyan pole so it pops out of the scene clutter, plus a foot marker.
                this.Drawer.Line(pos, pos + new float3(0f, 2.5f, 0f), Color.cyan);
                this.Drawer.Sphere(pos + new float3(0f, 0.25f, 0f), 0.3f, 12, Color.cyan);

                // Path intent: yellow arrow toward the target while pathfinding, grey when idle/arrived.
                this.Drawer.Arrow(pos + new float3(0f, 0.5f, 0f), target - pos, moving ? Color.yellow : Color.grey);

                // Actual velocity: magenta arrow (scaled so a 3.5 u/s walk is clearly visible).
                this.Drawer.Arrow(pos + new float3(0f, 1f, 0f), output.Velocity * 0.5f, Color.magenta);

                var s = default(Unity.Collections.FixedString128Bytes);
                s.Append(moving ? (Unity.Collections.FixedString32Bytes)"PATHING " : (Unity.Collections.FixedString32Bytes)"IDLE ");
                s.Append((int)math.round(output.Speed * 100f));
                s.Append((Unity.Collections.FixedString32Bytes)" cm/s  d=");
                s.Append((int)math.round(math.distance(pos, target) * 100f));
                this.Drawer.Text128(pos + new float3(0f, 2.8f, 0f), s, Color.white, 14f);
            }
        }
    }
}
#endif
