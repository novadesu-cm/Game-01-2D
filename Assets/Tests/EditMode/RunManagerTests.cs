using System;
using System.Collections.Generic;
using GotA.Simulation;
using NUnit.Framework;

namespace GotA.Tests
{
    /// <summary>P-04: waves, Pot, Extract/Continue, Timeout, Defeat.</summary>
    public class RunManagerTests
    {
        private const int TimeoutTicks = 3000; // 60 s / 0.02 s
        private const int SafetyTickLimit = 4000;

        private static readonly WeaponConfig Hammer = new WeaponConfig
            { BaseDamage = 24f, AttackInterval = 2.0f, Windup = 0.60f, BaseKnockback = 9.0f, Range = 1.5f };
        private static readonly WeaponConfig GruntAttack = new WeaponConfig
            { BaseDamage = 4f, AttackInterval = 1.2f, Windup = 0.30f, BaseKnockback = 2.5f, Range = 1.1f };

        private static UnitStats Gladiator(int maxHp = 120, float moveSpeed = 3.0f) => new UnitStats
            { MaxHP = maxHp, MoveSpeed = moveSpeed, KBResist = 0.10f, BodyRadius = 0.40f };

        private static UnitStats Grunt(int maxHp, float moveSpeed = 2.4f) => new UnitStats
            { MaxHP = maxHp, MoveSpeed = moveSpeed, KBResist = 0f, BodyRadius = 0.40f };

        /// <summary>Enemy right next to the fighter (spawn -3,0) that dies to one Hammer hit and tickles the fighter.</summary>
        private static WaveConfig EasyWave(int reward) => new WaveConfig
        {
            PotReward = reward,
            Enemies = { new EnemySpawnConfig { Stats = Grunt(1), Weapon = GruntAttack, Position = new SimVec2(-2.0f, 0f) } }
        };

        /// <summary>Enemy that never dies and kills a 1-HP fighter immediately.</summary>
        private static WaveConfig DeadlyWave(int reward) => new WaveConfig
        {
            PotReward = reward,
            Enemies = { new EnemySpawnConfig { Stats = Grunt(100000), Weapon = GruntAttack, Position = new SimVec2(-2.0f, 0f) } }
        };

        /// <summary>Nobody can reach anybody (fighter speed must be 0): the wave can only end by Timeout.</summary>
        private static WaveConfig StalemateWave(int reward) => new WaveConfig
        {
            PotReward = reward,
            Enemies = { new EnemySpawnConfig { Stats = Grunt(40, moveSpeed: 0f), Weapon = GruntAttack, Position = new SimVec2(3.0f, 0f) } }
        };

        private static RunConfig MakeConfig(UnitStats fighter, params WaveConfig[] waves)
        {
            var cfg = new RunConfig
            {
                Fighter = new FighterConfig { Stats = fighter, Weapon = Hammer, SpawnPosition = new SimVec2(-3.0f, 0f) }
            };
            cfg.Waves.AddRange(waves);
            return cfg;
        }

        private static RunManager NewRun(RunConfig cfg)
        {
            var run = new RunManager(cfg);
            run.Start();
            return run;
        }

        /// <summary>Ticks until the current wave is over (state leaves InWave). Returns ticks used.</summary>
        private static int TickWaveToEnd(RunManager run)
        {
            int ticks = 0;
            while (run.State == RunState.InWave && ticks < SafetyTickLimit)
            {
                run.Tick();
                ticks++;
            }
            Assert.AreNotEqual(RunState.InWave, run.State, "Wave did not finish within " + SafetyTickLimit + " ticks.");
            return ticks;
        }

        // ---------------------------------------------------------------
        // P-04 Definition of Done: the 4 run scenarios
        // ---------------------------------------------------------------

        [Test]
        public void DoD_1_Extract_AfterWave1_KeepsFullPot()
        {
            RunManager run = NewRun(MakeConfig(Gladiator(), EasyWave(30), EasyWave(50), EasyWave(100)));
            Assert.AreEqual(RunState.InWave, run.State);
            Assert.AreEqual(1, run.WaveNumber);

            TickWaveToEnd(run);

            Assert.AreEqual(RunState.AwaitingDecision, run.State);
            Assert.AreEqual(30, run.Pot);

            run.Extract();

            Assert.AreEqual(RunState.Ended, run.State);
            Assert.AreEqual(RunEndReason.Extracted, run.EndReason);
            Assert.AreEqual(30, run.FinalPot, "Extract keeps the full Pot.");
            Assert.AreEqual(1, run.WaveResults.Count);
        }

        [Test]
        public void DoD_2_ClearAllThreeWaves_EndsAutomaticallyWithFullPot()
        {
            RunManager run = NewRun(MakeConfig(Gladiator(), EasyWave(30), EasyWave(50), EasyWave(100)));

            TickWaveToEnd(run);
            Assert.AreEqual(RunState.AwaitingDecision, run.State);
            Assert.AreEqual(30, run.Pot);
            run.Continue();
            Assert.AreEqual(RunState.InWave, run.State);
            Assert.AreEqual(2, run.WaveNumber);

            TickWaveToEnd(run);
            Assert.AreEqual(RunState.AwaitingDecision, run.State);
            Assert.AreEqual(80, run.Pot);
            run.Continue();
            Assert.AreEqual(3, run.WaveNumber);

            TickWaveToEnd(run);

            // No decision after the last wave: the run ends by itself.
            Assert.AreEqual(RunState.Ended, run.State);
            Assert.AreEqual(RunEndReason.ClearedAllWaves, run.EndReason);
            Assert.AreEqual(180, run.FinalPot, "Clear W3 keeps the full Pot (30 + 50 + 100).");
            Assert.AreEqual(3, run.WaveResults.Count);
            foreach (WaveResult r in run.WaveResults)
                Assert.AreEqual(CombatOutcome.Cleared, r.Outcome);
        }

        [Test]
        public void DoD_3_Timeout_After3000Ticks_HalvesPotWithFloor()
        {
            // Fighter speed 0: wave 1 is won in place, wave 2 can never be won or lost.
            RunManager run = NewRun(MakeConfig(Gladiator(moveSpeed: 0f), EasyWave(30), StalemateWave(50)));
            TickWaveToEnd(run);
            Assert.AreEqual(30, run.Pot);
            run.Continue();
            Assert.AreEqual(RunState.InWave, run.State);
            Assert.AreEqual(TimeoutTicks, run.WaveTimeLimitTicks);

            for (int i = 0; i < TimeoutTicks - 1; i++) run.Tick();
            Assert.AreEqual(RunState.InWave, run.State, "Still running at tick 2999.");
            Assert.AreEqual(TimeoutTicks - 1, run.WaveTicks);

            run.Tick(); // tick 3000

            Assert.AreEqual(TimeoutTicks, run.WaveTicks);
            Assert.AreEqual(RunState.Ended, run.State);
            Assert.AreEqual(RunEndReason.Timeout, run.EndReason);
            Assert.AreEqual(CombatOutcome.Timeout, run.WaveResults[run.WaveResults.Count - 1].Outcome);
            Assert.AreEqual((int)Math.Floor(30 * 0.5), run.FinalPot);
            Assert.AreEqual(15, run.FinalPot);
        }

        [Test]
        public void DoD_4_Defeat_EndsRunWithPotZero()
        {
            // Fighter with 1 HP is killed by the first enemy hit in wave 1.
            RunManager run = NewRun(MakeConfig(Gladiator(maxHp: 1), DeadlyWave(30), EasyWave(50)));

            TickWaveToEnd(run);

            Assert.AreEqual(RunState.Ended, run.State);
            Assert.AreEqual(RunEndReason.Defeat, run.EndReason);
            Assert.AreEqual(CombatOutcome.Defeat, run.WaveResults[0].Outcome);
            Assert.AreEqual(0, run.FighterHp);
            Assert.AreEqual(0, run.FinalPot);
        }

        // ---------------------------------------------------------------
        // Extra coverage around the DoD
        // ---------------------------------------------------------------

        [Test]
        public void Defeat_InLaterWave_LosesTheEarnedPot()
        {
            RunManager run = NewRun(MakeConfig(Gladiator(), EasyWave(30), DeadlyWave(50), EasyWave(100)));
            TickWaveToEnd(run);
            Assert.AreEqual(30, run.Pot);
            run.Continue();

            run.Sim.GetUnit(1).HP = 0; // the Gladiator dies
            TickWaveToEnd(run);

            Assert.AreEqual(RunEndReason.Defeat, run.EndReason);
            Assert.AreEqual(0, run.FinalPot);
        }

        [Test]
        public void Timeout_InWave1_HasNothingToHalve()
        {
            RunManager run = NewRun(MakeConfig(Gladiator(moveSpeed: 0f), StalemateWave(30)));

            int ticks = TickWaveToEnd(run);

            Assert.AreEqual(TimeoutTicks, ticks);
            Assert.AreEqual(RunEndReason.Timeout, run.EndReason);
            Assert.AreEqual(0, run.FinalPot);
        }

        [Test]
        public void Timeout_OddPot_IsFloored()
        {
            RunManager run = NewRun(MakeConfig(Gladiator(moveSpeed: 0f), EasyWave(31), StalemateWave(50)));
            TickWaveToEnd(run);
            run.Continue();

            TickWaveToEnd(run);

            Assert.AreEqual(RunEndReason.Timeout, run.EndReason);
            Assert.AreEqual(15, run.FinalPot, "floor(31 * 0.5) = 15");
        }

        [Test]
        public void Hp_DoesNotRecoverBetweenWaves()
        {
            RunManager run = NewRun(MakeConfig(Gladiator(), EasyWave(30), EasyWave(50)));
            Assert.AreEqual(120, run.Sim.GetUnit(1).HP, "Wave 1 starts at MaxHP.");

            TickWaveToEnd(run);
            int hpAfterWave1 = run.WaveResults[0].FighterHpAtEnd;
            Assert.Less(hpAfterWave1, 120, "The enemy must have landed a hit for this test to mean anything.");
            Assert.AreEqual(hpAfterWave1, run.FighterHp);

            run.Continue();

            Assert.AreEqual(hpAfterWave1, run.Sim.GetUnit(1).HP, "No regen: wave 2 starts with the wave 1 HP.");
        }

        [Test]
        public void NewWave_ResetsFighterPositionAndSpawnsConfiguredEnemies()
        {
            RunManager run = NewRun(MakeConfig(Gladiator(), EasyWave(30), EasyWave(50)));
            TickWaveToEnd(run);
            run.Continue();

            SimUnit fighter = run.Sim.GetUnit(1);
            Assert.AreEqual(-3.0f, fighter.Position.x, 0.001f);
            Assert.AreEqual(0f, fighter.Position.y, 0.001f);
            Assert.AreEqual(2, run.Sim.Units.Count);
            SimUnit enemy = run.Sim.GetUnit(101);
            Assert.IsNotNull(enemy);
            Assert.AreEqual(ArenaSim.EnemyTeam, enemy.Team);
            Assert.AreEqual(0, run.WaveTicks);
        }

        [Test]
        public void Extract_IsOnlyAllowedBetweenWaves()
        {
            RunManager run = NewRun(MakeConfig(Gladiator(), EasyWave(30), EasyWave(50)));

            Assert.Throws<InvalidOperationException>(() => run.Extract());
            Assert.Throws<InvalidOperationException>(() => run.Continue());

            TickWaveToEnd(run);
            run.Continue();
            TickWaveToEnd(run); // last wave cleared -> Ended
            Assert.AreEqual(RunState.Ended, run.State);
            Assert.Throws<InvalidOperationException>(() => run.Extract());
            Assert.Throws<InvalidOperationException>(() => run.Continue());
        }

        [Test]
        public void Tick_DoesNothingWhileWaitingForDecisionOrAfterEnd()
        {
            RunManager run = NewRun(MakeConfig(Gladiator(), EasyWave(30), EasyWave(50)));
            TickWaveToEnd(run);
            int total = run.TotalTicks;

            run.Tick();
            Assert.AreEqual(total, run.TotalTicks);

            run.Extract();
            run.Tick();
            Assert.AreEqual(total, run.TotalTicks);
            Assert.AreEqual(30, run.FinalPot);
        }

        [Test]
        public void Start_CannotBeCalledTwice()
        {
            RunManager run = NewRun(MakeConfig(Gladiator(), EasyWave(30)));
            Assert.Throws<InvalidOperationException>(() => run.Start());
        }

        [Test]
        public void Config_IsValidated()
        {
            Assert.Throws<InvalidOperationException>(() => new RunManager(new RunConfig()));

            RunConfig noWaves = MakeConfig(Gladiator());
            Assert.Throws<InvalidOperationException>(() => new RunManager(noWaves));
        }

        [Test]
        public void SameRunTwice_IsDeterministic()
        {
            RunManager a = NewRun(MakeConfig(Gladiator(), EasyWave(30), EasyWave(50)));
            RunManager b = NewRun(MakeConfig(Gladiator(), EasyWave(30), EasyWave(50)));

            int ticksA = TickWaveToEnd(a);
            int ticksB = TickWaveToEnd(b);

            Assert.AreEqual(ticksA, ticksB);
            Assert.AreEqual(a.FighterHp, b.FighterHp);
            Assert.AreEqual(a.Pot, b.Pot);
        }
    }
}
