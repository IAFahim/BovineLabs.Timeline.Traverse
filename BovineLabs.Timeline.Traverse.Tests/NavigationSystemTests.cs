using BovineLabs.Movement.Data;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Testing;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.Traverse.Data;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
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

        private void CreateMoveToClip(Entity agent, MoveToData data, bool firstFrame, bool active = true)
        {
            var clip = Manager.CreateEntity();
            Manager.AddComponentData(clip, new TrackBinding { Value = agent });
            Manager.AddComponentData(clip, data);
            AddClipState(clip, firstFrame, active);
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
    }
}
