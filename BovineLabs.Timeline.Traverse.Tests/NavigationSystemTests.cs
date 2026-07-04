using BovineLabs.Movement.Data;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Testing;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.Traverse.Data;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Traverse.Tests
{
    public class NavigationSystemTests : ECSTestsFixture
    {
        [Test]
        public void MoveTo_WorldPosition_WritesTargetAndEnablesPathfinding()
        {
            var agent = CreateAgent(pathfinding: false);
            CreateMoveToClip(agent, new MoveToData
            {
                Destination = Target.None,
                WorldPosition = new float3(5f, 0f, 5f),
                QueryFilterType = 3,
                StopOnExit = true,
            }, firstFrame: true);

            RunSystem();

            var data = Manager.GetComponentData<CrowdAgentData>(agent);
            Assert.IsFalse(data.IsDirection);
            Assert.AreEqual(new float3(5f, 0f, 5f), data.TargetPosition);
            Assert.AreEqual(3, data.QueryFilterType);
            Assert.IsTrue(Manager.IsComponentEnabled<IsPathfinding>(agent));
        }

        [Test]
        public void MoveTo_TargetEntity_ResolvesLocalToWorldPosition()
        {
            var agent = CreateAgent(pathfinding: false);
            var destination = CreatePoint(new float3(3f, 0f, -2f));
            Manager.SetComponentData(agent, new Targets { Target = destination });

            CreateMoveToClip(agent, new MoveToData { Destination = Target.Target, StopOnExit = true }, firstFrame: true);

            RunSystem();

            var data = Manager.GetComponentData<CrowdAgentData>(agent);
            Assert.IsFalse(data.IsDirection);
            Assert.AreEqual(new float3(3f, 0f, -2f), data.TargetPosition);
            Assert.IsTrue(Manager.IsComponentEnabled<IsPathfinding>(agent));
        }

        [Test]
        public void MoveTo_FollowInsideStopDistance_DisablesPathfinding()
        {
            var agent = CreateAgent(pathfinding: true);
            Manager.AddComponentData(agent, new LocalToWorld { Value = float4x4.Translate(new float3(4f, 0f, 4f)) });
            var destination = CreatePoint(new float3(5f, 0f, 5f)); // ~1.41m away
            Manager.SetComponentData(agent, new Targets { Target = destination });

            CreateMoveToClip(agent, new MoveToData
            {
                Destination = Target.Target,
                Follow = true,
                StopDistance = 2f,
            }, firstFrame: false);

            RunSystem();

            Assert.IsFalse(Manager.IsComponentEnabled<IsPathfinding>(agent));
        }

        [Test]
        public void MoveTo_FollowOutsideStopDistance_WritesTargetAndEnablesPathfinding()
        {
            var agent = CreateAgent(pathfinding: false);
            Manager.AddComponentData(agent, new LocalToWorld { Value = float4x4.Translate(new float3(-5f, 0f, -5f)) });
            var destination = CreatePoint(new float3(5f, 0f, 5f));
            Manager.SetComponentData(agent, new Targets { Target = destination });

            CreateMoveToClip(agent, new MoveToData
            {
                Destination = Target.Target,
                Follow = true,
                StopDistance = 2f,
            }, firstFrame: false);

            RunSystem();

            var data = Manager.GetComponentData<CrowdAgentData>(agent);
            Assert.AreEqual(new float3(5f, 0f, 5f), data.TargetPosition);
            Assert.IsTrue(Manager.IsComponentEnabled<IsPathfinding>(agent));
        }

        [Test]
        public void MoveTo_StopOnExit_DisablesPathfinding()
        {
            var agent = CreateAgent(pathfinding: true);
            CreateMoveToClip(agent, new MoveToData { Destination = Target.None, StopOnExit = true }, firstFrame: false, active: false);

            RunSystem();

            Assert.IsFalse(Manager.IsComponentEnabled<IsPathfinding>(agent));
        }

        [Test]
        public void MoveTo_NoStopOnExit_LeavesPathfindingEnabled()
        {
            var agent = CreateAgent(pathfinding: true);
            CreateMoveToClip(agent, new MoveToData { Destination = Target.None, StopOnExit = false }, firstFrame: false, active: false);

            RunSystem();

            Assert.IsTrue(Manager.IsComponentEnabled<IsPathfinding>(agent));
        }

        [Test]
        public void Steer_WritesDirectionModeAndEnablesPathfinding()
        {
            var agent = CreateAgent(pathfinding: false);
            var clip = Manager.CreateEntity();
            Manager.AddComponentData(clip, new TrackBinding { Value = agent });
            Manager.AddComponentData(clip, new SteerData { Direction = new float3(0f, 0f, 1f) });
            AddClipState(clip, firstFrame: true, active: true);

            RunSystem();

            var data = Manager.GetComponentData<CrowdAgentData>(agent);
            Assert.IsTrue(data.IsDirection);
            Assert.AreEqual(new float3(0f, 0f, 1f), data.TargetDirection);
            Assert.IsTrue(Manager.IsComponentEnabled<IsPathfinding>(agent));
        }

        [Test]
        public void Stop_DisablesPathfindingOnEnter()
        {
            var agent = CreateAgent(pathfinding: true);
            var clip = Manager.CreateEntity();
            Manager.AddComponentData(clip, new TrackBinding { Value = agent });
            Manager.AddComponentData(clip, default(StopData));
            AddClipState(clip, firstFrame: true, active: true);

            RunSystem();

            Assert.IsFalse(Manager.IsComponentEnabled<IsPathfinding>(agent));
        }

        [Test]
        public void NonAgentBinding_IsSilentNoOp()
        {
            // Bound entity has none of the agent components — must not throw.
            var notAnAgent = Manager.CreateEntity();
            CreateMoveToClip(notAnAgent, new MoveToData { Destination = Target.None, WorldPosition = new float3(1f, 2f, 3f) }, firstFrame: true);

            Assert.DoesNotThrow(RunSystem);
        }

        [Test]
        public void MoveTo_NonFollowInsideStopDistance_HaltsWithoutDelivering()
        {
            var agent = CreateAgent(pathfinding: true);
            Manager.AddComponentData(agent, new LocalToWorld { Value = float4x4.Translate(new float3(4f, 0f, 4f)) });

            CreateMoveToClip(agent, new MoveToData
            {
                Destination = Target.None,
                WorldPosition = new float3(5f, 0f, 5f), // ~1.41m away
                StopDistance = 2f,
            }, firstFrame: true);

            RunSystem();

            Assert.IsFalse(Manager.IsComponentEnabled<IsPathfinding>(agent));
            Assert.AreEqual(default(float3), Manager.GetComponentData<CrowdAgentData>(agent).TargetPosition);
        }

        [Test]
        public void MoveTo_NonFollowStopDistance_LatchesDestinationAndHaltsOnArrival()
        {
            var agent = CreateAgent(pathfinding: false);
            Manager.AddComponentData(agent, new LocalToWorld { Value = float4x4.Translate(new float3(-20f, 0f, 0f)) });
            var destination = CreatePoint(new float3(5f, 0f, 5f));
            Manager.SetComponentData(agent, new Targets { Target = destination });

            var clip = CreateMoveToClip(agent, new MoveToData
            {
                Destination = Target.Target,
                StopDistance = 2f,
            }, firstFrame: true);

            RunSystem();

            // Delivered on enter.
            Assert.AreEqual(new float3(5f, 0f, 5f), Manager.GetComponentData<CrowdAgentData>(agent).TargetPosition);
            Assert.IsTrue(Manager.IsComponentEnabled<IsPathfinding>(agent));

            // Later target movement must NOT re-route the latched non-follow move.
            Manager.SetComponentData(destination, new LocalToWorld { Value = float4x4.Translate(new float3(50f, 0f, 50f)) });
            Manager.SetComponentEnabled<ClipActivePrevious>(clip, true);
            RunSystem();
            Assert.AreEqual(new float3(5f, 0f, 5f), Manager.GetComponentData<CrowdAgentData>(agent).TargetPosition);
            Assert.IsTrue(Manager.IsComponentEnabled<IsPathfinding>(agent));

            // Arriving inside the radius of the LATCHED destination halts…
            Manager.SetComponentData(agent, new LocalToWorld { Value = float4x4.Translate(new float3(4.5f, 0f, 4.5f)) });
            RunSystem();
            Assert.IsFalse(Manager.IsComponentEnabled<IsPathfinding>(agent));

            // …and stays halted for the rest of the activation, even if the agent is displaced.
            Manager.SetComponentData(agent, new LocalToWorld { Value = float4x4.Translate(new float3(-20f, 0f, 0f)) });
            RunSystem();
            Assert.IsFalse(Manager.IsComponentEnabled<IsPathfinding>(agent));
        }

        [Test]
        public void Warp_WorldPosition_TeleportsAndHaltsOnEnter()
        {
            var agent = CreateAgent(pathfinding: true);
            Manager.AddComponentData(agent, LocalTransform.Identity);

            CreateWarpClip(agent, new WarpData { Destination = Target.None, WorldPosition = new float3(10f, 0f, -3f) }, firstFrame: true);

            RunSystem();

            Assert.AreEqual(new float3(10f, 0f, -3f), Manager.GetComponentData<LocalTransform>(agent).Position);
            Assert.IsFalse(Manager.IsComponentEnabled<IsPathfinding>(agent));
        }

        [Test]
        public void Warp_ResumeCurrentTarget_KeepsPathfindingEnabled()
        {
            var agent = CreateAgent(pathfinding: false);
            Manager.AddComponentData(agent, LocalTransform.Identity);

            CreateWarpClip(agent, new WarpData
            {
                Destination = Target.None,
                WorldPosition = new float3(1f, 0f, 1f),
                Resume = WarpResume.ResumeCurrentTarget,
            }, firstFrame: true);

            RunSystem();

            Assert.IsTrue(Manager.IsComponentEnabled<IsPathfinding>(agent));
        }

        [Test]
        public void Warp_Reorient_FacesDirection()
        {
            var agent = CreateAgent(pathfinding: true);
            Manager.AddComponentData(agent, LocalTransform.Identity);

            CreateWarpClip(agent, new WarpData
            {
                Destination = Target.None,
                WorldPosition = float3.zero,
                Reorient = true,
                FaceTarget = Target.None,
                FaceDirection = new float3(1f, 0f, 0f),
            }, firstFrame: true);

            RunSystem();

            var expected = quaternion.LookRotationSafe(new float3(1f, 0f, 0f), math.up());
            Assert.AreEqual(expected, Manager.GetComponentData<LocalTransform>(agent).Rotation);
        }

        [Test]
        public void Warp_ZeroVelocity_ClearsPhysicsMomentum()
        {
            var agent = CreateAgent(pathfinding: true);
            Manager.AddComponentData(agent, LocalTransform.Identity);
            Manager.AddComponentData(agent, new PhysicsVelocity { Linear = new float3(1f, 2f, 3f), Angular = new float3(4f, 5f, 6f) });

            CreateWarpClip(agent, new WarpData
            {
                Destination = Target.None,
                WorldPosition = new float3(2f, 0f, 2f),
                ZeroVelocity = true,
            }, firstFrame: true);

            RunSystem();

            var velocity = Manager.GetComponentData<PhysicsVelocity>(agent);
            Assert.AreEqual(float3.zero, velocity.Linear);
            Assert.AreEqual(float3.zero, velocity.Angular);
        }

        [Test]
        public void Warp_FiresOncePerActivation()
        {
            var agent = CreateAgent(pathfinding: true);
            Manager.AddComponentData(agent, LocalTransform.Identity);

            var clip = CreateWarpClip(agent, new WarpData { Destination = Target.None, WorldPosition = new float3(10f, 0f, 10f) }, firstFrame: true);

            RunSystem();
            Assert.AreEqual(new float3(10f, 0f, 10f), Manager.GetComponentData<LocalTransform>(agent).Position);

            // Later active frames must not re-teleport.
            Manager.SetComponentData(agent, LocalTransform.FromPosition(new float3(-1f, 0f, -1f)));
            Manager.SetComponentEnabled<ClipActivePrevious>(clip, true);
            RunSystem();
            Assert.AreEqual(new float3(-1f, 0f, -1f), Manager.GetComponentData<LocalTransform>(agent).Position);
        }

        [Test]
        public void Warp_UnresolvedTarget_RetriesUntilResolved()
        {
            var agent = CreateAgent(pathfinding: true);
            Manager.AddComponentData(agent, LocalTransform.Identity);

            var clip = CreateWarpClip(agent, new WarpData { Destination = Target.Target }, firstFrame: true);

            RunSystem();
            Assert.AreEqual(float3.zero, Manager.GetComponentData<LocalTransform>(agent).Position); // no teleport yet

            // The target resolves on a later frame → the warp still fires.
            var destination = CreatePoint(new float3(7f, 0f, 7f));
            Manager.SetComponentData(agent, new Targets { Target = destination });
            Manager.SetComponentEnabled<ClipActivePrevious>(clip, true);
            RunSystem();
            Assert.AreEqual(new float3(7f, 0f, 7f), Manager.GetComponentData<LocalTransform>(agent).Position);
        }

        [Test]
        public void Patrol_Enter_WritesFirstWaypointAndEnablesPathfinding()
        {
            var agent = CreateAgent(pathfinding: false);
            Manager.AddComponentData(agent, new LocalToWorld { Value = float4x4.Translate(float3.zero) });

            CreatePatrolClip(agent, new PatrolData { Mode = PatrolMode.Loop, ArriveRadius = 0.5f, StopOnExit = true },
                new[]
                {
                    new PatrolWaypoint { Position = new float3(5f, 0f, 0f) },
                    new PatrolWaypoint { Position = new float3(5f, 0f, 5f) },
                }, firstFrame: true);

            RunSystem();

            Assert.AreEqual(new float3(5f, 0f, 0f), Manager.GetComponentData<CrowdAgentData>(agent).TargetPosition);
            Assert.IsTrue(Manager.IsComponentEnabled<IsPathfinding>(agent));
        }

        [Test]
        public void Patrol_Arrival_AdvancesToNextWaypoint()
        {
            var agent = CreateAgent(pathfinding: false);
            Manager.AddComponentData(agent, new LocalToWorld { Value = float4x4.Translate(float3.zero) });

            var clip = CreatePatrolClip(agent, new PatrolData { Mode = PatrolMode.Loop, ArriveRadius = 0.5f },
                new[]
                {
                    new PatrolWaypoint { Position = new float3(5f, 0f, 0f) },
                    new PatrolWaypoint { Position = new float3(5f, 0f, 5f) },
                }, firstFrame: true);

            RunSystem(); // issues wp0

            Manager.SetComponentData(agent, new LocalToWorld { Value = float4x4.Translate(new float3(5f, 0f, 0.2f)) });
            Manager.SetComponentEnabled<ClipActivePrevious>(clip, true);
            RunSystem(); // arrived at wp0 → advance

            Assert.AreEqual(new float3(5f, 0f, 5f), Manager.GetComponentData<CrowdAgentData>(agent).TargetPosition);
            Assert.IsTrue(Manager.IsComponentEnabled<IsPathfinding>(agent));
        }

        [Test]
        public void Patrol_OnceMode_StopsAtLastWaypoint()
        {
            var agent = CreateAgent(pathfinding: false);
            Manager.AddComponentData(agent, new LocalToWorld { Value = float4x4.Translate(float3.zero) });

            var clip = CreatePatrolClip(agent, new PatrolData { Mode = PatrolMode.Once, ArriveRadius = 0.5f },
                new[] { new PatrolWaypoint { Position = new float3(2f, 0f, 0f) } }, firstFrame: true);

            RunSystem(); // issues wp0
            Manager.SetComponentData(agent, new LocalToWorld { Value = float4x4.Translate(new float3(2f, 0f, 0f)) });
            Manager.SetComponentEnabled<ClipActivePrevious>(clip, true);
            RunSystem(); // arrived at the last waypoint → Done + halt

            Assert.IsFalse(Manager.IsComponentEnabled<IsPathfinding>(agent));

            // And stays done on later frames.
            RunSystem();
            Assert.IsFalse(Manager.IsComponentEnabled<IsPathfinding>(agent));
        }

        [Test]
        public void Patrol_WaitAtWaypoint_HaltsWithoutAdvancing()
        {
            var agent = CreateAgent(pathfinding: false);
            Manager.AddComponentData(agent, new LocalToWorld { Value = float4x4.Translate(float3.zero) });

            var clip = CreatePatrolClip(agent, new PatrolData { Mode = PatrolMode.Loop, ArriveRadius = 0.5f },
                new[]
                {
                    new PatrolWaypoint { Position = new float3(2f, 0f, 0f), Wait = 5f },
                    new PatrolWaypoint { Position = new float3(4f, 0f, 0f) },
                }, firstFrame: true);

            RunSystem(); // issues wp0
            Manager.SetComponentData(agent, new LocalToWorld { Value = float4x4.Translate(new float3(2f, 0f, 0f)) });
            Manager.SetComponentEnabled<ClipActivePrevious>(clip, true);
            RunSystem(); // arrived → Waiting, halted

            Assert.IsFalse(Manager.IsComponentEnabled<IsPathfinding>(agent));
            Assert.AreEqual(new float3(2f, 0f, 0f), Manager.GetComponentData<CrowdAgentData>(agent).TargetPosition); // not advanced
        }

        [Test]
        public void Patrol_StopOnExit_DisablesPathfinding()
        {
            var agent = CreateAgent(pathfinding: true);
            CreatePatrolClip(agent, new PatrolData { Mode = PatrolMode.Loop, ArriveRadius = 0.5f, StopOnExit = true },
                new[] { new PatrolWaypoint { Position = new float3(2f, 0f, 0f) } }, firstFrame: false, active: false);

            RunSystem();

            Assert.IsFalse(Manager.IsComponentEnabled<IsPathfinding>(agent));
        }

        [Test]
        public void SteeringFlags_AppliesOverrideAndCapturesOriginal()
        {
            var agent = CreateAgent(pathfinding: true);
            Manager.AddComponentData(agent, new CrowdAgent { UpdateFlags = UpdateFlags.CrowdAnticipateTurns });

            var clip = CreateSteeringClip(agent, new SteeringFlagsData
            {
                Flags = (byte)UpdateFlags.CrowdOptimizeVis,
                RestoreOnExit = true,
            }, firstFrame: true);

            RunSteeringSystem();

            Assert.AreEqual(UpdateFlags.CrowdOptimizeVis, Manager.GetComponentData<CrowdAgent>(agent).UpdateFlags);
            var state = Manager.GetComponentData<SteeringFlagsState>(clip);
            Assert.IsTrue(state.Captured);
            Assert.AreEqual((byte)UpdateFlags.CrowdAnticipateTurns, state.Original);
        }

        [Test]
        public void SteeringFlags_RestoresOnExit()
        {
            var agent = CreateAgent(pathfinding: true);
            var original = UpdateFlags.CrowdAnticipateTurns | UpdateFlags.CrowdOptimizeTopo;
            Manager.AddComponentData(agent, new CrowdAgent { UpdateFlags = original });

            var clip = CreateSteeringClip(agent, new SteeringFlagsData
            {
                Flags = (byte)UpdateFlags.None,
                RestoreOnExit = true,
            }, firstFrame: true);

            RunSteeringSystem();
            Assert.AreEqual(UpdateFlags.None, Manager.GetComponentData<CrowdAgent>(agent).UpdateFlags);

            Manager.SetComponentEnabled<ClipActive>(clip, false);
            Manager.SetComponentEnabled<ClipActivePrevious>(clip, true);
            RunSteeringSystem();

            Assert.AreEqual(original, Manager.GetComponentData<CrowdAgent>(agent).UpdateFlags);
            Assert.IsFalse(Manager.GetComponentData<SteeringFlagsState>(clip).Captured);
        }

        [Test]
        public void SteeringFlags_NoRestoreOnExit_KeepsOverride()
        {
            var agent = CreateAgent(pathfinding: true);
            Manager.AddComponentData(agent, new CrowdAgent { UpdateFlags = UpdateFlags.CrowdAnticipateTurns });

            var clip = CreateSteeringClip(agent, new SteeringFlagsData
            {
                Flags = (byte)UpdateFlags.CrowdOptimizeTopo,
                RestoreOnExit = false,
            }, firstFrame: true);

            RunSteeringSystem();
            Manager.SetComponentEnabled<ClipActive>(clip, false);
            Manager.SetComponentEnabled<ClipActivePrevious>(clip, true);
            RunSteeringSystem();

            Assert.AreEqual(UpdateFlags.CrowdOptimizeTopo, Manager.GetComponentData<CrowdAgent>(agent).UpdateFlags);
        }

        private Entity CreateAgent(bool pathfinding)
        {
            var entity = Manager.CreateEntity();
            Manager.AddComponentData(entity, default(CrowdAgentData));
            Manager.AddComponentData(entity, default(Targets));
            Manager.AddComponent<IsPathfinding>(entity);
            Manager.SetComponentEnabled<IsPathfinding>(entity, pathfinding);
            return entity;
        }

        private Entity CreatePoint(float3 position)
        {
            var entity = Manager.CreateEntity();
            Manager.AddComponentData(entity, new LocalToWorld { Value = float4x4.Translate(position) });
            return entity;
        }

        private Entity CreateMoveToClip(Entity agent, MoveToData data, bool firstFrame, bool active = true)
        {
            var clip = Manager.CreateEntity();
            Manager.AddComponentData(clip, new TrackBinding { Value = agent });
            Manager.AddComponentData(clip, data);
            Manager.AddComponent<MoveToState>(clip);
            AddClipState(clip, firstFrame, active);
            return clip;
        }

        private Entity CreateWarpClip(Entity agent, WarpData data, bool firstFrame, bool active = true)
        {
            var clip = Manager.CreateEntity();
            Manager.AddComponentData(clip, new TrackBinding { Value = agent });
            Manager.AddComponentData(clip, data);
            Manager.AddComponent<WarpState>(clip);
            AddClipState(clip, firstFrame, active);
            return clip;
        }

        private Entity CreatePatrolClip(Entity agent, PatrolData data, PatrolWaypoint[] waypoints, bool firstFrame, bool active = true)
        {
            var clip = Manager.CreateEntity();
            Manager.AddComponentData(clip, new TrackBinding { Value = agent });
            Manager.AddComponentData(clip, data);
            Manager.AddComponent<PatrolState>(clip);
            AddClipState(clip, firstFrame, active);

            var buffer = Manager.AddBuffer<PatrolWaypoint>(clip);
            foreach (var waypoint in waypoints)
            {
                buffer.Add(waypoint);
            }

            return clip;
        }

        private Entity CreateSteeringClip(Entity agent, SteeringFlagsData data, bool firstFrame, bool active = true)
        {
            var clip = Manager.CreateEntity();
            Manager.AddComponentData(clip, new TrackBinding { Value = agent });
            Manager.AddComponentData(clip, data);
            Manager.AddComponent<SteeringFlagsState>(clip);
            AddClipState(clip, firstFrame, active);
            return clip;
        }

        private void AddClipState(Entity clip, bool firstFrame, bool active)
        {
            Manager.AddComponent<ClipActive>(clip);
            Manager.SetComponentEnabled<ClipActive>(clip, active);
            Manager.AddComponent<ClipActivePrevious>(clip);

            // firstFrame == enter edge => ClipActivePrevious disabled this frame.
            Manager.SetComponentEnabled<ClipActivePrevious>(clip, !firstFrame);
        }

        private void RunSystem()
        {
            World.GetOrCreateSystem<NavigationSystem>().Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();
        }

        private void RunSteeringSystem()
        {
            World.GetOrCreateSystem<SteeringFlagsSystem>().Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();
        }
    }
}
