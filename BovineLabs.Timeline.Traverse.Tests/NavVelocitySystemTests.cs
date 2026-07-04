using BovineLabs.Movement.Data;
using BovineLabs.Testing;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.Traverse.Data;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Traverse.Tests
{
    public class NavVelocitySystemTests : ECSTestsFixture
    {
        private EndSimulationEntityCommandBufferSystem ecbSystem;

        public override void Setup()
        {
            base.Setup();
            this.ecbSystem = this.World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
        }

        [Test]
        public void WorldVelocity_WritesDesiredVelocityAndAddsMarker()
        {
            var agent = this.CreateAgent();
            this.CreateVelocityClip(agent, new float3(2f, 0f, 3f), local: false);

            this.RunSystem();

            var velocity = this.Manager.GetComponentData<DesiredVelocity>(agent).Velocity;
            Assert.AreEqual(2f, velocity.x, 1e-4f);
            Assert.AreEqual(3f, velocity.y, 1e-4f); // xz swizzle: y holds world Z

            Assert.IsTrue(this.Manager.HasComponent<NavManualDrive>(agent));
        }

        [Test]
        public void LocalVelocity_RotatesWithAgentFacing()
        {
            // Agent yawed 90°: local forward (0,0,1) now points down world +X.
            var agent = this.CreateAgent(quaternion.RotateY(math.radians(90f)));
            this.CreateVelocityClip(agent, new float3(0f, 0f, 2f), local: true);

            this.RunSystem();

            var velocity = this.Manager.GetComponentData<DesiredVelocity>(agent).Velocity;
            Assert.AreEqual(2f, velocity.x, 1e-4f);
            Assert.AreEqual(0f, velocity.y, 1e-4f);
        }

        [Test]
        public void WeightedClip_EasesTowardZero()
        {
            var agent = this.CreateAgent();
            this.CreateVelocityClip(agent, new float3(2f, 0f, 0f), local: false, weight: 0.5f);

            this.RunSystem();

            // 2 m/s @ weight 0.5, remainder eases toward zero => 1 m/s.
            var velocity = this.Manager.GetComponentData<DesiredVelocity>(agent).Velocity;
            Assert.AreEqual(1f, velocity.x, 1e-4f);
            Assert.AreEqual(0f, velocity.y, 1e-4f);
        }

        [Test]
        public void ClipEnd_ZeroesVelocityAndRemovesMarker()
        {
            var agent = this.CreateAgent();
            var clip = this.CreateVelocityClip(agent, new float3(2f, 0f, 0f), local: false);

            this.RunSystem();
            Assert.IsTrue(this.Manager.HasComponent<NavManualDrive>(agent));

            this.Manager.SetComponentEnabled<ClipActive>(clip, false); // clip ends

            this.RunSystem();

            var velocity = this.Manager.GetComponentData<DesiredVelocity>(agent).Velocity;
            Assert.AreEqual(0f, velocity.x);
            Assert.AreEqual(0f, velocity.y);
            Assert.IsFalse(this.Manager.HasComponent<NavManualDrive>(agent)); // control handed back
        }

        [Test]
        public void ClipDestroyedMidActive_StillHandsControlBack()
        {
            var agent = this.CreateAgent();
            var clip = this.CreateVelocityClip(agent, new float3(2f, 0f, 0f), local: false);

            this.RunSystem();
            this.Manager.DestroyEntity(clip);
            this.RunSystem();

            Assert.IsFalse(this.Manager.HasComponent<NavManualDrive>(agent));
            Assert.AreEqual(0f, this.Manager.GetComponentData<DesiredVelocity>(agent).Velocity.x);
        }

        [Test]
        public void BindingWithoutDesiredVelocity_DoesNotThrow()
        {
            var agent = this.Manager.CreateEntity(); // not a movement agent
            this.CreateVelocityClip(agent, new float3(2f, 0f, 0f), local: false);

            Assert.DoesNotThrow(this.RunSystem);
        }

        [Test]
        public void NullBinding_DoesNotThrow()
        {
            this.CreateVelocityClip(Entity.Null, new float3(2f, 0f, 0f), local: false);

            Assert.DoesNotThrow(this.RunSystem);
        }

        private Entity CreateAgent(quaternion? rotation = null)
        {
            var agent = this.Manager.CreateEntity();
            this.Manager.AddComponent<DesiredVelocity>(agent);
            this.Manager.AddComponentData(agent, LocalTransform.FromPositionRotation(float3.zero, rotation ?? quaternion.identity));
            return agent;
        }

        private Entity CreateVelocityClip(Entity agent, float3 velocity, bool local, float? weight = null)
        {
            var clip = this.Manager.CreateEntity();
            this.Manager.AddComponentData(clip, new TrackBinding { Value = agent });
            this.Manager.AddComponentData(clip, new NavVelocityAuthored { Velocity = velocity, Local = local });
            this.Manager.AddComponent<NavVelocityAnimated>(clip);
            this.Manager.AddComponent<TimelineActive>(clip);
            this.Manager.AddComponent<ClipActive>(clip);

            if (weight.HasValue)
            {
                this.Manager.AddComponentData(clip, new ClipWeight { Value = weight.Value });
            }

            return clip;
        }

        private void RunSystem()
        {
            this.World.GetOrCreateSystem<NavVelocitySystem>().Update(this.WorldUnmanaged);
            this.Manager.CompleteAllTrackedJobs();
            this.ecbSystem.Update(); // play back NavManualDrive add/remove
        }
    }
}
