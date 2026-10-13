using GotA.Simulation;
using NUnit.Framework;

namespace GotA.Tests
{
    /// <summary>P-06 fix: D-08 measurement-only KbVel and the per-hit log entries.</summary>
    public class KnockbackLogTests
    {
        private static readonly HitRules Rules = new HitRules();

        private static readonly WeaponConfig Fists = new WeaponConfig
            { BaseDamage = 8f, AttackInterval = 1.0f, Windup = 0.20f, BaseKnockback = 3.0f, Range = 1.1f };
        private static readonly WeaponConfig Hammer = new WeaponConfig
            { BaseDamage = 24f, AttackInterval = 2.0f, Windup = 0.60f, BaseKnockback = 9.0f, Range = 1.5f };
        private static readonly WeaponConfig Daggers = new WeaponConfig
            { BaseDamage = 5f, AttackInterval = 0.4f, Windup = 0.08f, BaseKnockback = 1.0f, Range = 1.0f };
        private static readonly WeaponConfig GruntAttack = new WeaponConfig
            { BaseDamage = 4f, AttackInterval = 1.2f, Windup = 0.30f, BaseKnockback = 2.5f, Range = 1.1f };

        private static UnitStats Stats(int hp, float speed, float kbResist, float radius) =>
            new UnitStats { MaxHP = hp, MoveSpeed = speed, KBResist = kbResist, BodyRadius = radius };

        private static ArenaSim NewSim(bool measure)
        {
            var sim = new ArenaSim(new ArenaConfig());
            sim.MeasureKnockback = measure;
            return sim;
        }

        /// <summary>Gladiator at (ax, 0) hits a Grunt at (tx, 0) once; sandbox phase (no AI).</summary>
        private static ArenaSim SingleHitSim(WeaponConfig weapon, float ax = -1f, float tx = 0f, int gruntHp = 1000)
        {
            ArenaSim sim = NewSim(true);
            SimUnit g = sim.AddUnit(1, ArenaSim.FighterTeam, new SimVec2(ax, 0f), Stats(120, 3f, 0.10f, 0.40f), weapon);
            SimUnit e = sim.AddUnit(101, ArenaSim.EnemyTeam, new SimVec2(tx, 0f), Stats(gruntHp, 2.4f, 0f, 0.40f), GruntAttack);
            HitResolver.Resolve(sim, g, e, Rules);
            return sim;
        }

        /// <summary>Expected D-08 distance: KbVel = impulse; 50 times { *= 1/(1+4*0.02); d += |KbVel|*0.02 }.</summary>
        private static float ExpectedDistance(float impulse)
        {
            float v = impulse, d = 0f, drag = 1f / (1f + 4f * 0.02f);
            for (int i = 0; i < ArenaSim.KbWindowTicks; i++) { v *= drag; d += v * 0.02f; }
            return d;
        }

        // ---- formula and window -----------------------------------------------------------------

        [Test]
        public void Distance_FollowsD08Formula_AndRowIsWrittenAfterExactly50Ticks()
        {
            ArenaSim sim = SingleHitSim(Hammer); // impulse 9 * (1 - 0) = 9

            for (int i = 0; i < ArenaSim.KbWindowTicks - 1; i++) sim.Tick();
            Assert.AreEqual(0, sim.HitLog.Count, "window still open after 49 ticks");

            sim.Tick();
            Assert.AreEqual(1, sim.HitLog.Count, "row written when the 50-tick window ends");

            HitLogEntry e = sim.HitLog[0];
            Assert.AreEqual(ExpectedDistance(9f), e.KbDistance, 1e-4f);
            Assert.AreEqual(1, e.AttackerId);
            Assert.AreEqual(101, e.TargetId);
            Assert.AreEqual(24, e.Damage);
            Assert.AreEqual(1, e.WaveTick);
            Assert.AreEqual(HitTargetDeath.None, e.TargetDied);
        }

        [Test]
        public void HammerHit_GivesAbout9xTheDistanceOfADaggersHit()
        {
            ArenaSim hammer = SingleHitSim(Hammer);
            ArenaSim daggers = SingleHitSim(Daggers);
            for (int i = 0; i < ArenaSim.KbWindowTicks; i++) { hammer.Tick(); daggers.Tick(); }

            float hammerDist = hammer.HitLog[0].KbDistance;
            float daggersDist = daggers.HitLog[0].KbDistance;

            Assert.Greater(daggersDist, 0f);
            Assert.AreEqual(9f, hammerDist / daggersDist, 0.05f);
        }

        [Test]
        public void TargetRingOut_InsideWindow_ClosesRowEarly_AsRingOut()
        {
            // Target stands near the edge; the hammer throws it out of the arena.
            ArenaSim sim = SingleHitSim(Hammer, ax: 5.0f, tx: 5.9f);
            for (int i = 0; i < ArenaSim.KbWindowTicks; i++) sim.Tick();

            Assert.AreEqual(1, sim.HitLog.Count);
            HitLogEntry e = sim.HitLog[0];
            Assert.AreEqual(HitTargetDeath.RingOut, e.TargetDied);
            Assert.Less(e.KbDistance, ExpectedDistance(9f), "measurement stops when the target dies");
            Assert.Greater(e.KbDistance, 0f);
        }

        [Test]
        public void KillingHit_TargetDiesByHp_RowClosesWithHpAndNoDistance()
        {
            ArenaSim sim = NewSim(true);
            sim.AddUnit(1, ArenaSim.FighterTeam, new SimVec2(-0.5f, 0f), Stats(120, 3f, 0.10f, 0.40f), Fists);
            sim.AddUnit(101, ArenaSim.EnemyTeam, new SimVec2(0f, 0f), Stats(3, 2.4f, 0f, 0.40f), GruntAttack);
            sim.StartCombat(60f);

            for (int i = 0; i < 200 && sim.HitLog.Count == 0; i++) sim.Tick();

            Assert.AreEqual(1, sim.HitLog.Count);
            HitLogEntry e = sim.HitLog[0];
            Assert.AreEqual(1, e.AttackerId);
            Assert.AreEqual(HitTargetDeath.HP, e.TargetDied);
            Assert.AreEqual(3, e.Damage, "ActualDamage is capped by the target's remaining HP");
            Assert.AreEqual(0f, e.KbDistance, 1e-6f);
        }

        [Test]
        public void WaveEnd_CompletesOpenWindowsOnKbVelAlone()
        {
            // 0.3 s wave limit: the fists hit lands, then Timeout ends the wave inside the 50-tick window.
            ArenaSim sim = NewSim(true);
            sim.AddUnit(1, ArenaSim.FighterTeam, new SimVec2(-0.5f, 0f), Stats(120, 3f, 0.10f, 0.40f), Fists);
            sim.AddUnit(101, ArenaSim.EnemyTeam, new SimVec2(0f, 0f), Stats(1000, 2.4f, 0f, 0.40f), GruntAttack);
            sim.StartCombat(0.3f);

            for (int i = 0; i < 100 && sim.Phase != CombatPhase.Ended; i++) sim.Tick();

            Assert.AreEqual(CombatPhase.Ended, sim.Phase);
            HitLogEntry fighterHit = null;
            foreach (HitLogEntry e in sim.HitLog) if (e.AttackerId == 1) fighterHit = e;

            Assert.IsNotNull(fighterHit, "the fighter's hit is flushed when the wave ends");
            Assert.AreEqual(ExpectedDistance(3f), fighterHit.KbDistance, 1e-4f);
            Assert.AreEqual(HitTargetDeath.None, fighterHit.TargetDied);
        }

        [Test]
        public void LoggingOff_KeepsHitLogEmpty()
        {
            ArenaSim sim = NewSim(false);
            SimUnit g = sim.AddUnit(1, ArenaSim.FighterTeam, new SimVec2(-1f, 0f), Stats(120, 3f, 0.10f, 0.40f), Hammer);
            SimUnit e = sim.AddUnit(101, ArenaSim.EnemyTeam, new SimVec2(0f, 0f), Stats(1000, 2.4f, 0f, 0.40f), GruntAttack);
            HitResolver.Resolve(sim, g, e, Rules);
            for (int i = 0; i < 60; i++) sim.Tick();

            Assert.AreEqual(0, sim.HitLog.Count);
        }

        // ---- KbVel never touches Pos/Vel -------------------------------------------------------------

        private static ArenaSim FightSim(bool measure)
        {
            // Strong units so nobody dies and the fight keeps going for the whole 500 ticks.
            ArenaSim sim = NewSim(measure);
            sim.AddUnit(1, ArenaSim.FighterTeam, new SimVec2(-3f, 0f), Stats(5000, 3f, 0.10f, 0.40f), Hammer);
            sim.AddUnit(101, ArenaSim.EnemyTeam, new SimVec2(3f, 1f), Stats(5000, 2.4f, 0f, 0.40f), GruntAttack);
            sim.AddUnit(102, ArenaSim.EnemyTeam, new SimVec2(3f, -1f), Stats(5000, 2.4f, 0f, 0.40f), GruntAttack);
            sim.StartCombat(60f);
            return sim;
        }

        [Test]
        public void KbVel_DoesNotChangePosOrVel_LoggingOnVsOff_500Ticks()
        {
            ArenaSim on = FightSim(true);
            ArenaSim off = FightSim(false);

            for (int tick = 0; tick < 500; tick++)
            {
                on.Tick();
                off.Tick();

                Assert.AreEqual(off.Units.Count, on.Units.Count);
                for (int i = 0; i < on.Units.Count; i++)
                {
                    SimUnit a = on.Units[i], b = off.Units[i];
                    string where = "tick " + tick + " unit " + a.UnitId;
                    Assert.AreEqual(b.Position.x, a.Position.x, where + " pos.x");
                    Assert.AreEqual(b.Position.y, a.Position.y, where + " pos.y");
                    Assert.AreEqual(b.Velocity.x, a.Velocity.x, where + " vel.x");
                    Assert.AreEqual(b.Velocity.y, a.Velocity.y, where + " vel.y");
                    Assert.AreEqual(b.HP, a.HP, where + " hp");
                    Assert.AreEqual(b.IsAlive, a.IsAlive, where + " alive");
                    Assert.AreEqual(b.State, a.State, where + " ai state");
                }
            }

            Assert.Greater(on.HitLog.Count, 0, "scenario must contain hits, otherwise the test proves nothing");
            Assert.AreEqual(0, off.HitLog.Count);
        }

        [Test]
        public void RunManager_SameSeed_LoggingOnVsOff_GivesIdenticalRun()
        {
            RunConfig MakeConfig(bool logHits)
            {
                var cfg = new RunConfig
                {
                    LogHits = logHits,
                    Fighter = new FighterConfig
                    {
                        Stats = Stats(120, 3f, 0.10f, 0.40f),
                        Weapon = Hammer,
                        SpawnPosition = new SimVec2(-3f, 0f)
                    }
                };
                var wave = new WaveConfig { PotReward = 30 };
                wave.Enemies.Add(new EnemySpawnConfig { Stats = Stats(40, 2.4f, 0f, 0.40f), Weapon = GruntAttack, Position = new SimVec2(3f, 1f) });
                wave.Enemies.Add(new EnemySpawnConfig { Stats = Stats(40, 2.4f, 0f, 0.40f), Weapon = GruntAttack, Position = new SimVec2(3f, -1f) });
                cfg.Waves.Add(wave);
                return cfg;
            }

            var a = new RunManager(MakeConfig(true));
            var b = new RunManager(MakeConfig(false));
            a.Start();
            b.Start();

            int guard = 0;
            while (a.State == RunState.InWave && guard++ < 5000)
            {
                a.Tick();
                b.Tick();
                for (int i = 0; i < a.Sim.Units.Count; i++)
                {
                    Assert.AreEqual(b.Sim.Units[i].Position.x, a.Sim.Units[i].Position.x);
                    Assert.AreEqual(b.Sim.Units[i].Position.y, a.Sim.Units[i].Position.y);
                }
            }

            Assert.AreEqual(b.State, a.State);
            Assert.AreEqual(b.TotalTicks, a.TotalTicks);
            Assert.AreEqual(b.EndReason, a.EndReason);
            Assert.Greater(a.HitLog.Count, 0);
            Assert.AreEqual(0, b.HitLog.Count);
        }
    }
}
