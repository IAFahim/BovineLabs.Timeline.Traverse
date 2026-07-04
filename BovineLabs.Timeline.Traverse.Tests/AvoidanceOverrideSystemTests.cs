using BovineLabs.Avoidance.Data;
using BovineLabs.Testing;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.Traverse.Avoidance;
using NUnit.Framework;
using Unity.Entities;

namespace BovineLabs.Timeline.Traverse.Tests
{
    public class AvoidanceOverrideSystemTests : ECSTestsFixture
    {
        private static readonly ORCAAgent Baseline = new()
        {
            ManuallyControlled = false,
            Layer = 1 << 0,
            CollidesWith = byte.MaxValue,
            Priority = 100,
        };

        [Test]
        public void EnterEdge_CapturesOriginalAndAppliesOverriddenFieldsOnly()
        {
            var agent = CreateAgent(Baseline);
            var clip = CreateOverrideClip(agent, new AvoidanceOverrideData
            {
                OverrideManuallyControlled = true,
                ManuallyControlled = true,
                OverridePriority = true,
                Priority = 255,
                RestoreOnExit = true,
            }, firstFrame: true);

            RunSystem();

            var orca = Manager.GetComponentData<ORCAAgent>(agent);
            Assert.IsTrue(orca.ManuallyControlled);
            Assert.AreEqual(255, orca.Priority);

            // Non-overridden fields untouched.
            Assert.AreEqual(Baseline.Layer, orca.Layer);
            Assert.AreEqual(Baseline.CollidesWith, orca.CollidesWith);

            var state = Manager.GetComponentData<AvoidanceOverrideState>(clip);
            Assert.IsTrue(state.Captured);
            Assert.AreEqual(Baseline, state.Original);
        }

        [Test]
        public void ExitEdge_RestoresOnlyOverriddenFields_KeepsExternalChanges()
        {
            var agent = CreateAgent(Baseline);
            var clip = CreateOverrideClip(agent, new AvoidanceOverrideData
            {
                OverridePriority = true,
                Priority = 255,
                RestoreOnExit = true,
            }, firstFrame: false, active: false); // exit edge: active off, previous on

            // Clip was previously applied: agent runs the override, state holds the capture.
            Manager.SetComponentData(clip, new AvoidanceOverrideState { Captured = true, Original = Baseline });
            var live = Baseline;
            live.Priority = 255;
            live.Layer = 1 << 3; // external gameplay change while the clip was active
            Manager.SetComponentData(agent, live);

            RunSystem();

            var orca = Manager.GetComponentData<ORCAAgent>(agent);
            Assert.AreEqual(Baseline.Priority, orca.Priority); // restored
            Assert.AreEqual(1 << 3, orca.Layer); // external change preserved

            Assert.IsFalse(Manager.GetComponentData<AvoidanceOverrideState>(clip).Captured);
        }

        [Test]
        public void ExitEdge_RestoreOnExitDisabled_LeavesOverrideApplied()
        {
            var agent = CreateAgent(Baseline);
            var clip = CreateOverrideClip(agent, new AvoidanceOverrideData
            {
                OverridePriority = true,
                Priority = 255,
                RestoreOnExit = false,
            }, firstFrame: false, active: false);

            Manager.SetComponentData(clip, new AvoidanceOverrideState { Captured = true, Original = Baseline });
            var live = Baseline;
            live.Priority = 255;
            Manager.SetComponentData(agent, live);

            RunSystem();

            Assert.AreEqual(255, Manager.GetComponentData<ORCAAgent>(agent).Priority);
        }

        [Test]
        public void ContinuingFrame_ReappliesWithoutRecapturing()
        {
            var agent = CreateAgent(Baseline);
            var clip = CreateOverrideClip(agent, new AvoidanceOverrideData
            {
                OverridePriority = true,
                Priority = 255,
                RestoreOnExit = true,
            }, firstFrame: false); // mid-clip frame

            Manager.SetComponentData(clip, new AvoidanceOverrideState { Captured = true, Original = Baseline });
            var live = Baseline;
            live.Priority = 42; // something external stomped the override
            Manager.SetComponentData(agent, live);

            RunSystem();

            Assert.AreEqual(255, Manager.GetComponentData<ORCAAgent>(agent).Priority);

            // Capture must not be refreshed mid-clip — the original baseline survives.
            var state = Manager.GetComponentData<AvoidanceOverrideState>(clip);
            Assert.IsTrue(state.Captured);
            Assert.AreEqual(Baseline, state.Original);
        }

        [Test]
        public void SameFrameHandoff_NewClipCapturesBaseline_NotOutgoingOverride()
        {
            var agent = CreateAgent(Baseline);

            // Outgoing clip A: overrode Priority, exiting this frame.
            var clipA = CreateOverrideClip(agent, new AvoidanceOverrideData
            {
                OverridePriority = true,
                Priority = 255,
                RestoreOnExit = true,
            }, firstFrame: false, active: false);
            Manager.SetComponentData(clipA, new AvoidanceOverrideState { Captured = true, Original = Baseline });
            var live = Baseline;
            live.Priority = 255;
            Manager.SetComponentData(agent, live);

            // Incoming clip B: overrides Priority differently, entering this frame.
            var clipB = CreateOverrideClip(agent, new AvoidanceOverrideData
            {
                OverridePriority = true,
                Priority = 10,
                RestoreOnExit = true,
            }, firstFrame: true);

            RunSystem();

            // B applied on top of the restored baseline...
            Assert.AreEqual(10, Manager.GetComponentData<ORCAAgent>(agent).Priority);

            // ...and captured the true baseline, never A's override.
            var stateB = Manager.GetComponentData<AvoidanceOverrideState>(clipB);
            Assert.IsTrue(stateB.Captured);
            Assert.AreEqual(Baseline.Priority, stateB.Original.Priority);
        }

        [Test]
        public void NullBinding_DoesNotThrow()
        {
            CreateOverrideClip(Entity.Null, new AvoidanceOverrideData
            {
                OverridePriority = true,
                Priority = 255,
                RestoreOnExit = true,
            }, firstFrame: true);

            Assert.DoesNotThrow(RunSystem);
        }

        [Test]
        public void AgentWithoutORCAAgent_DoesNotThrow()
        {
            var agent = Manager.CreateEntity(); // no ORCAAgent component
            CreateOverrideClip(agent, new AvoidanceOverrideData
            {
                OverrideLayer = true,
                Layer = 1 << 2,
                RestoreOnExit = true,
            }, firstFrame: true);

            Assert.DoesNotThrow(RunSystem);
        }

        private Entity CreateAgent(ORCAAgent orca)
        {
            var entity = Manager.CreateEntity();
            Manager.AddComponentData(entity, orca);
            return entity;
        }

        private Entity CreateOverrideClip(Entity agent, AvoidanceOverrideData data, bool firstFrame, bool active = true)
        {
            var clip = Manager.CreateEntity();
            Manager.AddComponentData(clip, new TrackBinding { Value = agent });
            Manager.AddComponentData(clip, data);
            Manager.AddComponent<AvoidanceOverrideState>(clip);

            Manager.AddComponent<ClipActive>(clip);
            Manager.SetComponentEnabled<ClipActive>(clip, active);
            Manager.AddComponent<ClipActivePrevious>(clip);

            // firstFrame == enter edge => ClipActivePrevious disabled this frame.
            Manager.SetComponentEnabled<ClipActivePrevious>(clip, !firstFrame);
            return clip;
        }

        private void RunSystem()
        {
            World.GetOrCreateSystem<AvoidanceOverrideSystem>().Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();
        }
    }
}
