using System.Collections.Generic;
using GotA.Simulation;
using NUnit.Framework;

namespace GotA.Tests
{
    /// <summary>P-03: AI state machine + tick order (spec sections 4 and 5).</summary>
    public class AiTickTests
    {
        private const float Tol = 0.001f;
        private const float WaveLimit = 60f;
        private const int MaxTicks = 3500; // > 60 s / 0.02 s, so a Timeout must have fired by then

        private static readonly WeaponConfig Hammer = new WeaponConfig
            { BaseDamage = 24f, AttackInterval = 2.0f, Windup = 0.60f, BaseKnockback = 9.0f, Range = 1.5f };
        private static readonly WeaponConfig GruntAttack = new WeaponConfig
            { BaseDamage = 4f, AttackInterval = 1.2f, Windup = 0.30f, BaseKnockback = 2.5f, Range = 1.1f };
        private static readonly WeaponConfig BruteAttack = new WeaponConfig
            { BaseDamage = 9f, AttackInterval = 2.0f, Windup = 0.70f, BaseKnockback = 7.0f, Range = 1.4f };

        private static readonly UnitStats GladiatorStats = new UnitStats
            { MaxHP = 120, MoveSpeed = 3.0f, KBResist = 0.10f, BodyRadius = 0.40f };
        private static readonly UnitStats GruntStats = new UnitStats
            { MaxHP = 40, MoveSpeed = 2.4f, KBResist = 0f, BodyRadius = 0.40f };
        private static readonly UnitStats BruteStats = new UnitStats
            { MaxHP = 100, MoveSpeed = 1.5f, KBResist = 0.40f, BodyRadius = 0.55f };

        private static ArenaSim NewSim() => new ArenaSim(new ArenaConfig());

        private static SimUnit AddGladiator(ArenaSim sim, float x, float y, WeaponConfig weapon = null)
            => sim.AddUnit(1, ArenaSim.FighterTeam, new SimVec2(x, y), GladiatorStats, weapon ?? Hammer);

        private static SimUnit AddGrunt(ArenaSim sim, int id, float x, float y)
            => sim.AddUnit(id, ArenaSim.EnemyTeam, new SimVec2(x, y), GruntStats, GruntAttack);

        private static SimUnit AddBrute(ArenaSim sim, int id, float x, float y)
            => sim.AddUnit(id, ArenaSim.EnemyTeam, new SimVec2(x, y), BruteStats, BruteAttack);

        private static int RunUntilEnded(ArenaSim sim, int maxTicks = MaxTicks)
        {
            int ticks = 0;
            while (sim.Phase != CombatPhase.Ended && ticks < maxTicks)
            {
                sim.Tick();
                ticks++;
            }
            return ticks;
        }

        /// <summary>Puts a unit mid-Windup so it ends on the next tick (timer 0.02 -> 0).</summary>
        private static void ForceWindupEndingNextTick(SimUnit attacker, SimUnit target)
        {
            attacker.State = AiState.Windup;
            attacker.WindupTimer = 0.02f;
            attacker.LockedTargetId = target.UnitId;
            attacker.TargetId = target.UnitId;
            attacker.Cooldown = 1f;
        }

        // ---------------------------------------------------------------
        // P-03 Definition of Done: 1 Gladiator (Hammer) vs 2 Grunts
        // ---------------------------------------------------------------

        private static ArenaSim BuildDoDScenario()
        {
            ArenaSim sim = NewSim();
            AddGladiator(sim, -3.0f, 0f);
            AddGrunt(sim, 101, 3.0f, 1.0f);
            AddGrunt(sim, 102, 3.0f, -1.0f);
            sim.StartCombat(WaveLimit);
            return sim;
        }

        [Test]
        public void DoD_GladiatorHammerVs2Grunts_BattleResolvesAutomatically()
        {
            ArenaSim sim = BuildDoDScenario();

            int ticks = RunUntilEnded(sim);

            Assert.AreEqual(CombatPhase.Ended, sim.Phase, "Battle must finish by itself.");
            Assert.AreNotEqual(CombatOutcome.None, sim.Outcome);
            Assert.Less(ticks, MaxTicks);
            Assert.AreEqual(ticks, sim.TickCount);

            SimUnit gladiator = sim.GetUnit(1);
            SimUnit g1 = sim.GetUnit(101);
            SimUnit g2 = sim.GetUnit(102);

            TestContext.WriteLine("DoD result: outcome=" + sim.Outcome + ", ticks=" + ticks
                + " (" + (ticks * sim.Config.FixedDt) + " s), gladiator HP=" + gladiator.HP
                + ", grunt101 alive=" + g1.IsAlive + " cause=" + g1.DeathCause
                + ", grunt102 alive=" + g2.IsAlive + " cause=" + g2.DeathCause);

            // Every unit is either alive, or dead with a recorded cause.
            foreach (SimUnit u in sim.Units)
            {
                if (u.IsAlive)
                {
                    Assert.AreEqual(DeathCause.None, u.DeathCause);
                    Assert.AreEqual(AiState.Idle, u.State);
                }
                else
                {
                    Assert.AreNotEqual(DeathCause.None, u.DeathCause, "Unit " + u.UnitId);
                    Assert.AreEqual(AiState.Dead, u.State);
                    if (u.DeathCause == DeathCause.HP)
                    {
                        Assert.LessOrEqual(u.HP, 0);
                        Assert.AreNotEqual(0, u.LastAttackerId, "HP death must record the last attacker.");
                    }
                }
                Assert.AreEqual(0f, u.Velocity.x, Tol);
                Assert.AreEqual(0f, u.Velocity.y, Tol);
            }

            // Outcome is consistent with who is alive.
            switch (sim.Outcome)
            {
                case CombatOutcome.Cleared:
                    Assert.IsTrue(gladiator.IsAlive);
                    Assert.IsFalse(g1.IsAlive);
                    Assert.IsFalse(g2.IsAlive);
                    break;
                case CombatOutcome.Defeat:
                    Assert.IsFalse(gladiator.IsAlive);
                    break;
                case CombatOutcome.Timeout:
                    Assert.IsTrue(gladiator.IsAlive);
                    Assert.IsTrue(g1.IsAlive || g2.IsAlive);
                    Assert.GreaterOrEqual(ticks, 2999);
                    break;
            }
        }

        [Test]
        public void DoD_Battle_ActuallyFights_UnitsTakeDamageAndDie()
        {
            ArenaSim sim = BuildDoDScenario();
            RunUntilEnded(sim);

            // Guards against a vacuous run: somebody lost HP or left the arena.
            bool anyoneHurtOrDead = false;
            foreach (SimUnit u in sim.Units)
                if (u.HP < u.MaxHP || !u.IsAlive) anyoneHurtOrDead = true;
            Assert.IsTrue(anyoneHurtOrDead);
        }

        [Test]
        public void DoD_SameScenarioTwice_IsDeterministic()
        {
            ArenaSim a = BuildDoDScenario();
            ArenaSim b = BuildDoDScenario();

            for (int tick = 0; tick < MaxTicks; tick++)
            {
                a.Tick();
                b.Tick();
                for (int i = 0; i < a.Units.Count; i++)
                {
                    Assert.AreEqual(a.Units[i].Position.x, b.Units[i].Position.x, Tol, "tick " + tick);
                    Assert.AreEqual(a.Units[i].Position.y, b.Units[i].Position.y, Tol, "tick " + tick);
                    Assert.AreEqual(a.Units[i].HP, b.Units[i].HP, "tick " + tick);
                }
            }
            Assert.AreEqual(a.Outcome, b.Outcome);
            Assert.AreEqual(a.TickCount, b.TickCount);
        }

        [Test]
        public void Brute_AlsoFightsAndBattleEnds()
        {
            ArenaSim sim = NewSim();
            AddGladiator(sim, -3.0f, 0f);
            AddGrunt(sim, 101, 2.5f, 1.0f);
            AddBrute(sim, 102, 3.5f, -1.0f);
            sim.StartCombat(WaveLimit);

            RunUntilEnded(sim);

            Assert.AreEqual(CombatPhase.Ended, sim.Phase);
            Assert.AreNotEqual(CombatOutcome.None, sim.Outcome);
        }

        // ---------------------------------------------------------------
        // AI behaviour
        // ---------------------------------------------------------------

        [Test]
        public void StartCombat_UnitsStartInChaseWithNoCooldown()
        {
            ArenaSim sim = BuildDoDScenario();

            foreach (SimUnit u in sim.Units)
            {
                Assert.AreEqual(AiState.Chase, u.State);
                Assert.AreEqual(0f, u.Cooldown, Tol);
            }
            Assert.AreEqual(CombatPhase.Active, sim.Phase);
        }

        [Test]
        public void Chase_MovesTowardNearestEnemy_AndFacesIt()
        {
            ArenaSim sim = NewSim();
            SimUnit g = AddGladiator(sim, -3f, 0f);
            AddGrunt(sim, 101, 3f, 0f);
            sim.StartCombat(WaveLimit);
            g.Facing = -1;

            float before = SimVec2.Distance(g.Position, sim.GetUnit(101).Position);
            for (int i = 0; i < 20; i++) sim.Tick();
            float after = SimVec2.Distance(g.Position, sim.GetUnit(101).Position);

            Assert.Less(after, before);
            Assert.AreEqual(101, g.TargetId);
            Assert.AreEqual(1, g.Facing);
        }

        [Test]
        public void Targeting_PicksNearest_TieGoesToLowerHpThenLowerId()
        {
            // Nearest wins.
            ArenaSim sim = NewSim();
            SimUnit g = AddGladiator(sim, 0f, 0f);
            AddGrunt(sim, 101, 5f, 0f);
            AddGrunt(sim, 102, 2f, 0f);
            sim.StartCombat(WaveLimit);
            sim.Tick();
            Assert.AreEqual(102, g.TargetId);

            // Tie (within 0.1): lower current HP wins.
            sim = NewSim();
            g = AddGladiator(sim, 0f, 0f);
            AddGrunt(sim, 101, 3f, 0f);
            SimUnit weaker = AddGrunt(sim, 102, 0f, 3.05f);
            weaker.HP = 10;
            sim.StartCombat(WaveLimit);
            sim.Tick();
            Assert.AreEqual(102, g.TargetId);

            // Tie and equal HP: lower UnitID wins.
            sim = NewSim();
            g = AddGladiator(sim, 0f, 0f);
            AddGrunt(sim, 101, 3f, 0f);
            AddGrunt(sim, 102, 0f, 3.05f);
            sim.StartCombat(WaveLimit);
            sim.Tick();
            Assert.AreEqual(101, g.TargetId);
        }

        [Test]
        public void Retarget_SwitchesOnlyIfNewTargetIsAtLeastHalfUnitCloser()
        {
            ArenaSim sim = NewSim();
            SimUnit g = AddGladiator(sim, 0f, 0f);
            SimUnit far = AddGrunt(sim, 101, 5f, 0f);
            SimUnit other = AddGrunt(sim, 102, -5.2f, 0f);
            sim.StartCombat(WaveLimit);
            far.Weapon = null;   // inert targets: only the gladiator's AI runs
            other.Weapon = null;
            sim.Tick();
            Assert.AreEqual(101, g.TargetId);

            // 0.2 closer only: no switch at the next retarget.
            other.Position = new SimVec2(-4.8f, 0f);
            for (int i = 0; i < 30; i++) { g.Position = SimVec2.Zero; g.Velocity = SimVec2.Zero; sim.Tick(); }
            Assert.AreEqual(101, g.TargetId);

            // 0.7 closer: switch.
            other.Position = new SimVec2(-4.3f, 0f);
            for (int i = 0; i < 30; i++) { g.Position = SimVec2.Zero; g.Velocity = SimVec2.Zero; sim.Tick(); }
            Assert.AreEqual(102, g.TargetId);
        }

        [Test]
        public void InRange_StartsWindup_LocksTarget_SetsCooldown()
        {
            ArenaSim sim = NewSim();
            SimUnit g = AddGladiator(sim, 0f, 0f);
            AddGrunt(sim, 101, 1.0f, 0f); // inside Hammer range 1.5
            sim.StartCombat(WaveLimit);

            sim.Tick();

            Assert.AreEqual(AiState.Windup, g.State);
            Assert.AreEqual(101, g.LockedTargetId);
            Assert.AreEqual(Hammer.Windup, g.WindupTimer, Tol);
            Assert.AreEqual(Hammer.AttackInterval, g.Cooldown, Tol);
            Assert.AreEqual(0f, g.Velocity.x, Tol);
        }

        [Test]
        public void AtkSpdMult_ScalesWindupAndInterval()
        {
            ArenaSim sim = NewSim();
            SimUnit g = AddGladiator(sim, 0f, 0f);
            g.AtkSpdMult = 2f;
            AddGrunt(sim, 101, 1.0f, 0f);
            sim.StartCombat(WaveLimit);

            sim.Tick();

            Assert.AreEqual(Hammer.Windup / 2f, g.WindupTimer, Tol);
            Assert.AreEqual(Hammer.AttackInterval / 2f, g.Cooldown, Tol);
        }

        [Test]
        public void Windup_EndsInHit_ThenWaitsInReadyWhileCooldownTicks()
        {
            ArenaSim sim = NewSim();
            SimUnit g = AddGladiator(sim, 0f, 0f);
            SimUnit e = AddGrunt(sim, 101, 1.0f, 0f);
            e.Weapon = null; // keep the grunt passive
            sim.StartCombat(WaveLimit);

            // 1 tick to start the Windup + 30 ticks (0.6 s) to finish it.
            for (int i = 0; i < 31; i++) sim.Tick();

            Assert.AreEqual(40 - 24, e.HP);
            Assert.Greater(e.StaggerTimer, 0f);
            // Windup ended -> Chase, and in the same tick the target is in range with Cooldown > 0 -> Ready.
            Assert.AreEqual(AiState.Ready, g.State);
            Assert.Greater(g.Cooldown, 0f);
            Assert.Less(g.Cooldown, Hammer.AttackInterval);
        }

        [Test]
        public void Windup_MissesWhenTargetIsBeyondRangePlusSlack()
        {
            ArenaSim sim = NewSim();
            SimUnit g = AddGladiator(sim, 0f, 0f);
            SimUnit e = AddGrunt(sim, 101, 1.0f, 0f);
            e.Weapon = null;
            sim.StartCombat(WaveLimit);
            sim.Tick(); // Windup starts
            Assert.AreEqual(AiState.Windup, g.State);

            e.Position = new SimVec2(3.0f, 0f); // beyond 1.5 + 0.3
            for (int i = 0; i < 30; i++) sim.Tick();

            Assert.AreEqual(40, e.HP);
        }

        [Test]
        public void Stagger_BlocksAiMovementAndWindup_ButNotTimers()
        {
            ArenaSim sim = NewSim();
            SimUnit g = AddGladiator(sim, -3f, 0f);
            AddGrunt(sim, 101, 3f, 0f);
            sim.StartCombat(WaveLimit);
            g.StaggerTimer = 0.1f; // 5 ticks

            for (int i = 0; i < 4; i++) sim.Tick();
            Assert.AreEqual(0f, g.Velocity.x, Tol, "AI must not set velocity while staggered.");
            Assert.AreEqual(-3f, g.Position.x, Tol);

            for (int i = 0; i < 3; i++) sim.Tick();
            Assert.AreEqual(0f, g.StaggerTimer, Tol);
            Assert.Greater(g.Position.x, -3f, "Chase resumes after stagger.");
        }

        [Test]
        public void FastKnockbackVelocity_BlocksAiEvenWithoutStaggerTimer()
        {
            ArenaSim sim = NewSim();
            SimUnit g = AddGladiator(sim, -3f, 0f);
            AddGrunt(sim, 101, 3f, 0f);
            sim.StartCombat(WaveLimit);
            g.Velocity = new SimVec2(-8f, 0f); // |vel| > MoveSpeed

            sim.Tick();

            // Only drag acted on it; the AI did not overwrite the knockback velocity.
            Assert.Less(g.Velocity.x, -5f);
        }

        [Test]
        public void StartedWindup_IsNotCancelledByStagger()
        {
            ArenaSim sim = NewSim();
            SimUnit g = AddGladiator(sim, 0f, 0f);
            AddGrunt(sim, 101, 1.0f, 0f).Weapon = null;
            sim.StartCombat(WaveLimit);
            sim.Tick();
            Assert.AreEqual(AiState.Windup, g.State);

            g.StaggerTimer = 0.2f;
            sim.Tick();

            Assert.AreEqual(AiState.Windup, g.State);
        }

        [Test]
        public void TargetDiesDuringWindup_CancelsWithCooldownZero()
        {
            ArenaSim sim = NewSim();
            SimUnit g = AddGladiator(sim, 0f, 0f);
            SimUnit victim = AddGrunt(sim, 101, 1.0f, 0f);
            SimUnit other = AddGrunt(sim, 102, 5.0f, 0f);
            victim.Weapon = null;
            other.Weapon = null;
            sim.StartCombat(WaveLimit);
            sim.Tick();
            Assert.AreEqual(AiState.Windup, g.State);
            Assert.Greater(g.Cooldown, 0f);

            victim.HP = 0; // dies at step 4 of the next tick
            sim.Tick();

            Assert.IsFalse(victim.IsAlive);
            Assert.AreEqual(AiState.Chase, g.State);
            Assert.AreEqual(0f, g.Cooldown, Tol);
            Assert.AreEqual(0, g.TargetId);

            sim.Tick(); // retargets
            Assert.AreEqual(102, g.TargetId);
        }

        // ---------------------------------------------------------------
        // Tick order
        // ---------------------------------------------------------------

        [Test]
        public void TickOrder_DeathMarking_HpZeroBecomesDeadWithHpCause()
        {
            ArenaSim sim = NewSim();
            SimUnit g = AddGladiator(sim, 0f, 0f);
            SimUnit e = AddGrunt(sim, 101, 3f, 0f);
            AddGrunt(sim, 102, -3f, 0f);
            sim.StartCombat(WaveLimit);
            e.HP = 0;
            e.LastAttackerId = 1;

            sim.Tick();

            Assert.IsFalse(e.IsAlive);
            Assert.AreEqual(DeathCause.HP, e.DeathCause);
            Assert.AreEqual(1, e.LastAttackerId);
            Assert.AreEqual(AiState.Dead, e.State);
            Assert.IsTrue(g.IsAlive);
        }

        [Test]
        public void TickOrder_AttackerKilledEarlierSameTick_StillHits_WithoutLifesteal()
        {
            ArenaSim sim = NewSim();
            SimUnit fighter = AddGladiator(sim, 0f, 0f); // UnitID 1 resolves first
            fighter.Weapon = new WeaponConfig { BaseDamage = 100f, AttackInterval = 1f, Windup = 0.1f, BaseKnockback = 1f, Range = 1.5f };
            SimUnit enemy = sim.AddUnit(101, ArenaSim.EnemyTeam, new SimVec2(1f, 0f), GruntStats, GruntAttack, TraitType.Lifesteal);
            sim.StartCombat(WaveLimit);
            ForceWindupEndingNextTick(fighter, enemy);
            ForceWindupEndingNextTick(enemy, fighter);
            enemy.HP = 5;
            int fighterHpBefore = fighter.HP;

            sim.Tick();

            Assert.AreEqual(fighterHpBefore - 4, fighter.HP, "The dead-this-tick enemy still hits.");
            Assert.AreEqual(0, enemy.HP, "No Lifesteal for an attacker already at HP 0.");
            Assert.IsFalse(enemy.IsAlive);
            Assert.AreEqual(DeathCause.HP, enemy.DeathCause);
            Assert.AreEqual(CombatOutcome.Cleared, sim.Outcome);
        }

        [Test]
        public void TickOrder_RingOutAttackerInStepOne_DoesNotHit()
        {
            ArenaSim sim = NewSim();
            SimUnit fighter = AddGladiator(sim, 0f, 0f);
            SimUnit enemy = AddGrunt(sim, 101, 6.5f, 0f); // outside the ring
            AddGrunt(sim, 102, -3f, 0f).Weapon = null;
            sim.StartCombat(WaveLimit);
            ForceWindupEndingNextTick(enemy, fighter);
            enemy.Position = new SimVec2(6.5f, 0f);

            sim.Tick();

            Assert.IsFalse(enemy.IsAlive);
            Assert.AreEqual(DeathCause.RingOut, enemy.DeathCause);
            Assert.AreEqual(fighter.MaxHP, fighter.HP);
        }

        [Test]
        public void Outcome_DefeatBeatsCleared_WhenEveryoneDiesTogether()
        {
            ArenaSim sim = NewSim();
            SimUnit fighter = AddGladiator(sim, 0f, 0f);
            SimUnit enemy = AddGrunt(sim, 101, 1f, 0f);
            sim.StartCombat(WaveLimit);
            ForceWindupEndingNextTick(fighter, enemy);
            ForceWindupEndingNextTick(enemy, fighter);
            fighter.HP = 1;
            enemy.HP = 1;

            sim.Tick();

            Assert.IsFalse(fighter.IsAlive);
            Assert.IsFalse(enemy.IsAlive);
            Assert.AreEqual(CombatOutcome.Defeat, sim.Outcome);
        }

        [Test]
        public void Outcome_Cleared_StopsImmediately_AllUnitsIdleAndStill()
        {
            ArenaSim sim = NewSim();
            SimUnit fighter = AddGladiator(sim, 0f, 0f);
            SimUnit enemy = AddGrunt(sim, 101, 3f, 0f);
            sim.StartCombat(WaveLimit);
            enemy.HP = 0;
            fighter.Velocity = new SimVec2(2f, 0f);

            sim.Tick();

            Assert.AreEqual(CombatOutcome.Cleared, sim.Outcome);
            Assert.AreEqual(CombatPhase.Ended, sim.Phase);
            Assert.AreEqual(AiState.Idle, fighter.State);
            Assert.AreEqual(0f, fighter.Velocity.x, Tol);
            Assert.AreEqual(0f, fighter.Position.x, Tol, "Physics is off on the outcome tick.");

            int ticks = sim.TickCount;
            sim.Tick();
            Assert.AreEqual(ticks, sim.TickCount, "No simulation after the outcome.");
        }

        [Test]
        public void Outcome_Timeout_WhenWaveTimerRunsOut()
        {
            ArenaSim sim = NewSim();
            AddGladiator(sim, -5f, 0f);
            AddGrunt(sim, 101, 5f, 0f);
            sim.StartCombat(0.1f); // 5 ticks

            for (int i = 0; i < 4; i++) sim.Tick();
            Assert.AreEqual(CombatPhase.Active, sim.Phase);
            sim.Tick();

            Assert.AreEqual(CombatOutcome.Timeout, sim.Outcome);
            foreach (SimUnit u in sim.Units)
            {
                Assert.AreEqual(AiState.Idle, u.State);
                Assert.AreEqual(0f, u.Velocity.x, Tol);
            }
        }

        [Test]
        public void SandboxPhase_StillOnlyDoesRingOutAndPhysics()
        {
            ArenaSim sim = NewSim();
            SimUnit u = AddGladiator(sim, 0f, 0f);
            u.HP = 0; // would die in Active phase, must not in Sandbox

            sim.Tick();

            Assert.AreEqual(CombatPhase.Sandbox, sim.Phase);
            Assert.IsTrue(u.IsAlive);
        }
    }
}
