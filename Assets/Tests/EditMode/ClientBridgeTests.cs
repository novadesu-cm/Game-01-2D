using GotA.Client;
using GotA.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace GotA.Tests
{
    /// <summary>P-05: ScriptableObject -> DTO bridge, Best Pot rules, run summary text.</summary>
    public class ClientBridgeTests
    {
        private static WeaponConfigSO MakeWeapon(float dmg, float interval, float windup, float kb, float range)
        {
            var so = ScriptableObject.CreateInstance<WeaponConfigSO>();
            so.baseDamage = dmg; so.attackInterval = interval; so.windup = windup;
            so.baseKnockback = kb; so.range = range;
            return so;
        }

        private static TraitConfigSO MakeTrait(TraitType type, float magnitude)
        {
            var so = ScriptableObject.CreateInstance<TraitConfigSO>();
            so.trait = type; so.magnitude = magnitude;
            return so;
        }

        private static RunSetupSO MakeSetup()
        {
            var weapon = MakeWeapon(4, 1.2f, 0.3f, 2.5f, 1.1f);
            var enemy = ScriptableObject.CreateInstance<EnemyConfigSO>();
            enemy.weapon = weapon;

            var setup = ScriptableObject.CreateInstance<RunSetupSO>();
            var wave = new WaveSetup { potReward = 30 };
            wave.enemies.Add(new EnemySpawnSetup { enemy = enemy, position = new Vector2(3f, 1f) });
            setup.waves.Add(wave);
            return setup;
        }

        [Test]
        public void WeaponConverter_CopiesAllFields()
        {
            WeaponConfig dto = DtoConverter.ToDto(MakeWeapon(24f, 2.0f, 0.6f, 9.0f, 1.5f));
            Assert.AreEqual(24f, dto.BaseDamage, 0.001f);
            Assert.AreEqual(2.0f, dto.AttackInterval, 0.001f);
            Assert.AreEqual(0.6f, dto.Windup, 0.001f);
            Assert.AreEqual(9.0f, dto.BaseKnockback, 0.001f);
            Assert.AreEqual(1.5f, dto.Range, 0.001f);
        }

        [Test]
        public void TraitConverter_MapsTypeAndWritesMagnitudeToHitRules()
        {
            Assert.AreEqual(TraitType.None, DtoConverter.ToDto((TraitConfigSO)null));

            var rules = new HitRules();
            DtoConverter.ApplyTrait(rules, MakeTrait(TraitType.Lifesteal, 0.5f));
            DtoConverter.ApplyTrait(rules, MakeTrait(TraitType.ThickSkin, 0.6f));
            Assert.AreEqual(0.5f, rules.LifestealRatio, 0.001f);
            Assert.AreEqual(0.6f, rules.ThickSkinDamageMult, 0.001f);
        }

        [Test]
        public void BuildRunConfig_SameSeedGivesSamePositions_AndStaysInsideJitter()
        {
            RunSetupSO setup = MakeSetup();
            WeaponConfigSO weapon = MakeWeapon(8, 1f, 0.2f, 3f, 1.1f);

            RunConfig a = DtoConverter.BuildRunConfig(setup, weapon, null, 42);
            RunConfig b = DtoConverter.BuildRunConfig(setup, weapon, null, 42);

            SimVec2 pa = a.Waves[0].Enemies[0].Position;
            SimVec2 pb = b.Waves[0].Enemies[0].Position;
            Assert.AreEqual(pa.x, pb.x);
            Assert.AreEqual(pa.y, pb.y);
            Assert.AreEqual(3f, pa.x, setup.spawnJitter + 0.0001f);
            Assert.AreEqual(1f, pa.y, setup.spawnJitter + 0.0001f);
            Assert.AreEqual(TraitType.None, a.Fighter.Trait);
        }

        [Test]
        public void BestPot_OnlyRisesAndDefeatNeverCounts()
        {
            var tracker = new BestPotTracker();
            Assert.IsTrue(tracker.Submit(80));
            Assert.IsFalse(tracker.Submit(0));   // Defeat
            Assert.IsFalse(tracker.Submit(30));
            Assert.IsFalse(tracker.Submit(80));  // equal is not a new best
            Assert.IsTrue(tracker.Submit(90));   // e.g. Timeout floor(180 * 0.5)
            Assert.AreEqual(90, tracker.BestPot);
        }

        [Test]
        public void RunSummary_Defeat_DescribesCause()
        {
            // Fighter at 1 HP, one tough enemy next to it -> Defeat by HP.
            RunSetupSO setup = MakeSetup();
            setup.fighterStats.maxHP = 1;
            setup.fighterSpawn = new Vector2(0f, 0f);
            setup.waves[0].enemies[0].position = new Vector2(0.8f, 0f);
            setup.waves[0].enemies[0].enemy.stats.maxHP = 100000;
            setup.spawnJitter = 0f;

            var run = new RunManager(DtoConverter.BuildRunConfig(setup, MakeWeapon(1, 1f, 0.2f, 0f, 1.1f), null, 1));
            run.Start();
            for (int i = 0; i < 4000 && run.State == RunState.InWave; i++) run.Tick();

            Assert.AreEqual(RunState.Ended, run.State);
            RunSummary summary = RunSummary.Build(run, id => "Grunt");
            Assert.AreEqual(RunEndReason.Defeat, summary.Reason);
            Assert.AreEqual(0, summary.FinalPot);
            StringAssert.Contains("Killed by Grunt", summary.CauseLine);
        }
    }
}
