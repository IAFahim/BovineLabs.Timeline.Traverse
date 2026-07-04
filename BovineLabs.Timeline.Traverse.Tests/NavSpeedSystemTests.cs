#if BL_ESSENCE
using BovineLabs.Core.Iterators;
using BovineLabs.Essence.Data;
using BovineLabs.Movement.Data;
using BovineLabs.Testing;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.Traverse.Data;
using NUnit.Framework;
using Unity.Entities;

namespace BovineLabs.Timeline.Traverse.Tests
{
    public class NavSpeedSystemTests : ECSTestsFixture
    {
        private static readonly StatKey MoveSpeedStat = 1;
        private static readonly StatKey AccelerationStat = 2;
        private static readonly StatKey RotationSpeedStat = 3;

        private EndSimulationEntityCommandBufferSystem ecbSystem;

        public override void Setup()
        {
            base.Setup();

            this.ecbSystem = this.World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();

            var config = this.Manager.CreateEntity();
            this.Manager.AddComponentData(config, new MovementStatsConfig
            {
                MoveSpeedStat = MoveSpeedStat,
                AccelerationStat = AccelerationStat,
                RotationSpeedStat = RotationSpeedStat,
            });
        }

        [Test]
        public void Apply_AddsMultiplicativeModifiersForNonIdentityFieldsOnly()
        {
            var agent = this.CreateAgent(MoveSpeedStat, AccelerationStat, RotationSpeedStat);
            this.CreateSpeedClip(agent, new NavSpeedMultipliers { Speed = 2f, Accel = 0.5f, Turn = 1f });

            this.RunSystem();

            var modifiers = this.Manager.GetBuffer<StatModifiers>(agent);
            Assert.AreEqual(2, modifiers.Length); // Turn == identity: emits nothing

            Assert.IsTrue(this.TryFindModifier(agent, MoveSpeedStat, out var speed));
            Assert.AreEqual(StatModifyType.Multiplicative, speed.ModifyType);
            Assert.AreEqual(1f, speed.ValueFloat, 1e-4f); // multiplier 2 => +100%

            Assert.IsTrue(this.TryFindModifier(agent, AccelerationStat, out var accel));
            Assert.AreEqual(-0.5f, accel.ValueFloat, 1e-4f); // multiplier 0.5 => -50%

            Assert.IsTrue(this.Manager.IsComponentEnabled<StatChanged>(agent));
            Assert.IsTrue(this.Manager.HasComponent<NavSpeedApplied>(agent));
            Assert.IsTrue(this.Manager.IsComponentEnabled<NavSpeedApplied>(agent));
        }

        [Test]
        public void Reapply_ClearsBySourceFirst_NeverStacks()
        {
            var agent = this.CreateAgent(MoveSpeedStat, AccelerationStat, RotationSpeedStat);
            this.CreateSpeedClip(agent, new NavSpeedMultipliers { Speed = 2f, Accel = 1f, Turn = 1f });

            this.RunSystem();
            this.RunSystem(); // continuing frame: clear-by-source then re-add

            Assert.AreEqual(1, this.Manager.GetBuffer<StatModifiers>(agent).Length);
        }

        [Test]
        public void UnseededStat_EmitsNoModifier()
        {
            var agent = this.CreateAgent(MoveSpeedStat); // acceleration never seeded
            this.CreateSpeedClip(agent, new NavSpeedMultipliers { Speed = 2f, Accel = 2f, Turn = 1f });

            this.RunSystem();

            var modifiers = this.Manager.GetBuffer<StatModifiers>(agent);
            Assert.AreEqual(1, modifiers.Length);
            Assert.IsTrue(this.TryFindModifier(agent, MoveSpeedStat, out _));
            Assert.IsFalse(this.TryFindModifier(agent, AccelerationStat, out _)); // skipped, movement never frozen
        }

        [Test]
        public void WeightedClip_BlendsTowardIdentity()
        {
            var agent = this.CreateAgent(MoveSpeedStat, AccelerationStat, RotationSpeedStat);
            this.CreateSpeedClip(agent, new NavSpeedMultipliers { Speed = 2f, Accel = 1f, Turn = 1f }, weight: 0.5f);

            this.RunSystem();

            // 2 @ weight 0.5 + identity remainder => effective multiplier 1.5.
            Assert.IsTrue(this.TryFindModifier(agent, MoveSpeedStat, out var speed));
            Assert.AreEqual(0.5f, speed.ValueFloat, 1e-4f);
        }

        [Test]
        public void ClipEnd_ClearsModifiersAndDisablesMarker()
        {
            var agent = this.CreateAgent(MoveSpeedStat, AccelerationStat, RotationSpeedStat);
            var clip = this.CreateSpeedClip(agent, new NavSpeedMultipliers { Speed = 2f, Accel = 0.5f, Turn = 3f });

            this.RunSystem();
            Assert.AreNotEqual(0, this.Manager.GetBuffer<StatModifiers>(agent).Length);

            this.Manager.SetComponentEnabled<StatChanged>(agent, false); // settle so we can observe the re-flag
            this.Manager.SetComponentEnabled<ClipActive>(clip, false); // clip ends

            this.RunSystem();

            Assert.AreEqual(0, this.Manager.GetBuffer<StatModifiers>(agent).Length);
            Assert.IsFalse(this.Manager.IsComponentEnabled<NavSpeedApplied>(agent));
            Assert.IsTrue(this.Manager.IsComponentEnabled<StatChanged>(agent)); // recalculation requested
        }

        [Test]
        public void ClipDestroyedMidActive_StillClearsModifiers()
        {
            var agent = this.CreateAgent(MoveSpeedStat, AccelerationStat, RotationSpeedStat);
            var clip = this.CreateSpeedClip(agent, new NavSpeedMultipliers { Speed = 2f, Accel = 1f, Turn = 1f });

            this.RunSystem();
            this.Manager.DestroyEntity(clip);
            this.RunSystem();

            Assert.AreEqual(0, this.Manager.GetBuffer<StatModifiers>(agent).Length);
            Assert.IsFalse(this.Manager.IsComponentEnabled<NavSpeedApplied>(agent));
        }

        [Test]
        public void BindingWithoutStatPipeline_DoesNotThrow()
        {
            var agent = this.Manager.CreateEntity(); // no stats, no modifiers buffer
            this.CreateSpeedClip(agent, new NavSpeedMultipliers { Speed = 2f, Accel = 1f, Turn = 1f });

            Assert.DoesNotThrow(this.RunSystem);
        }

        [Test]
        public void NullBinding_DoesNotThrow()
        {
            this.CreateSpeedClip(Entity.Null, new NavSpeedMultipliers { Speed = 2f, Accel = 1f, Turn = 1f });

            Assert.DoesNotThrow(this.RunSystem);
        }

        private Entity CreateAgent(params StatKey[] seededStats)
        {
            var agent = this.Manager.CreateEntity();
            this.Manager.AddBuffer<StatModifiers>(agent);
            this.Manager.AddComponent<StatChanged>(agent);
            this.Manager.SetComponentEnabled<StatChanged>(agent, false);

            var stats = this.Manager.AddBuffer<Stat>(agent);
            stats.Initialize();
            var map = stats.AsMap();
            foreach (var key in seededStats)
            {
                map.Add(key, StatValue.Default);
            }

            return agent;
        }

        private Entity CreateSpeedClip(Entity agent, NavSpeedMultipliers multipliers, float? weight = null)
        {
            var clip = this.Manager.CreateEntity();
            this.Manager.AddComponentData(clip, new TrackBinding { Value = agent });
            this.Manager.AddComponentData(clip, new NavSpeedAnimated { Value = multipliers });
            this.Manager.AddComponent<TimelineActive>(clip);
            this.Manager.AddComponent<ClipActive>(clip);

            if (weight.HasValue)
            {
                this.Manager.AddComponentData(clip, new ClipWeight { Value = weight.Value });
            }

            return clip;
        }

        private bool TryFindModifier(Entity agent, StatKey key, out StatModifier modifier)
        {
            var modifiers = this.Manager.GetBuffer<StatModifiers>(agent);
            foreach (var element in modifiers)
            {
                if (element.Value.Type == key)
                {
                    modifier = element.Value;
                    return true;
                }
            }

            modifier = default;
            return false;
        }

        private void RunSystem()
        {
            this.World.GetOrCreateSystem<NavSpeedSystem>().Update(this.WorldUnmanaged);
            this.Manager.CompleteAllTrackedJobs();
            this.ecbSystem.Update(); // play back NavSpeedApplied add
        }
    }
}
#endif
