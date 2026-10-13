using System.Collections.Generic;
using System.IO;
using System.Linq;
using GotA.Client;
using GotA.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace GotA.Tests
{
    /// <summary>P-06: debug log CSV, seed, and the 20-seed variation DoD test.</summary>
    public class RunLogTests
    {
        // ---- helpers: spec.md prototype data, built in memory ----------------------------------

        private static WeaponConfigSO MakeWeapon(string name, float dmg, float interval, float windup, float kb, float range)
        {
            var so = ScriptableObject.CreateInstance<WeaponConfigSO>();
            so.displayName = name;
            so.baseDamage = dmg; so.attackInterval = interval; so.windup = windup;
            so.baseKnockback = kb; so.range = range;
            return so;
        }

        private static EnemyConfigSO MakeEnemy(string name, int hp, float speed, float kbResist, float radius, WeaponConfigSO weapon)
        {
            var so = ScriptableObject.CreateInstance<EnemyConfigSO>();
            so.displayName = name;
            so.stats = new UnitStatsData { maxHP = hp, moveSpeed = speed, kbResist = kbResist, bodyRadius = radius };
            so.weapon = weapon;
            return so;
        }

        private static WaveSetup Wave(int reward, params (EnemyConfigSO enemy, float x, float y)[] spawns)
        {
            var wave = new WaveSetup { potReward = reward };
            foreach (var s in spawns)
                wave.enemies.Add(new EnemySpawnSetup { enemy = s.enemy, position = new Vector2(s.x, s.y) });
            return wave;
        }

        private static RunSetupSO MakeSpecSetup()
        {
            EnemyConfigSO grunt = MakeEnemy("Grunt", 40, 2.4f, 0f, 0.40f, MakeWeapon("Grunt Attack", 4, 1.2f, 0.30f, 2.5f, 1.1f));
            EnemyConfigSO brute = MakeEnemy("Brute", 100, 1.5f, 0.40f, 0.55f, MakeWeapon("Brute Attack", 9, 2.0f, 0.70f, 7.0f, 1.4f));

            var setup = ScriptableObject.CreateInstance<RunSetupSO>(); // Gladiator + arena defaults = spec values
            setup.waves = new List<WaveSetup>
            {
                Wave(30, (grunt, 3.0f, 1.0f), (grunt, 3.0f, -1.0f)),
                Wave(50, (grunt, 2.5f, 1.0f), (brute, 3.5f, -1.0f)),
                Wave(100, (grunt, 2.5f, 1.5f), (grunt, 2.5f, -1.5f), (brute, 3.5f, 0.0f))
            };
            return setup;
        }

        /// <summary>Plays a whole run, always choosing Continue, and returns it (ended).</summary>
        private static RunManager PlayRun(RunSetupSO setup, WeaponConfigSO weapon, TraitConfigSO trait, int seed)
        {
            var run = new RunManager(DtoConverter.BuildRunConfig(setup, weapon, trait, seed));
            run.Start();

            int guard = 0;
            while (run.State != RunState.Ended && guard++ < 20000)
            {
                if (run.State == RunState.InWave) run.Tick();
                else run.Continue();
            }
            Assert.AreEqual(RunState.Ended, run.State, "run did not finish");
            return run;
        }

        private static string Fingerprint(RunManager run)
        {
            SimUnit fighter = run.Sim.GetUnit(run.Config.FighterUnitId);
            float kb = 0f;
            foreach (HitLogEntry e in run.HitLog) kb += e.KbDistance;
            return run.EndReason + "|" + run.WaveNumber + "|" + run.TotalTicks + "|" + fighter.HP + "|" +
                   fighter.Position.x.ToString("R") + "," + fighter.Position.y.ToString("R") + "|" +
                   kb.ToString("R") + "|" + run.HitLog.Count;
        }

        // ---- P-06 DoD ---------------------------------------------------------------------------

        [Test]
        public void SameBuild_20Seeds_OutcomesAreNotAllIdentical()
        {
            RunSetupSO setup = MakeSpecSetup();
            WeaponConfigSO fists = MakeWeapon("Fists", 8, 1.0f, 0.20f, 3.0f, 1.1f);

            var fingerprints = new List<string>();
            var ticks = new HashSet<int>();
            var positions = new HashSet<string>();
            for (int seed = 1; seed <= 20; seed++)
            {
                RunManager run = PlayRun(setup, fists, null, seed);
                fingerprints.Add(Fingerprint(run));
                ticks.Add(run.TotalTicks);
                SimUnit f = run.Sim.GetUnit(run.Config.FighterUnitId);
                positions.Add(f.Position.x.ToString("R") + "," + f.Position.y.ToString("R"));
            }

            int distinct = fingerprints.Distinct().Count();
            Debug.Log("P-06 20 seeds (Fists): " + distinct + " distinct outcomes, " + ticks.Count +
                      " distinct tick totals, " + positions.Count + " distinct final fighter positions.");

            Assert.AreEqual(20, fingerprints.Count);
            Assert.Greater(distinct, 1, "all 20 seeds produced the identical outcome: spawn offsets have no effect");
        }

        [Test]
        public void SameSeed_TwiceGivesIdenticalOutcome()
        {
            RunSetupSO setup = MakeSpecSetup();
            WeaponConfigSO hammer = MakeWeapon("Giant Hammer", 24, 2.0f, 0.60f, 9.0f, 1.5f);

            Assert.AreEqual(Fingerprint(PlayRun(setup, hammer, null, 777)),
                            Fingerprint(PlayRun(setup, hammer, null, 777)));
        }

        // ---- Metrics ---------------------------------------------------------------------------

        [Test]
        public void HitLog_HasOneEntryPerHit_WithBothTeams_AndAscendingRunTicks()
        {
            RunManager run = PlayRun(MakeSpecSetup(), MakeWeapon("Giant Hammer", 24, 2.0f, 0.60f, 9.0f, 1.5f), null, 5);
            Assert.Greater(run.HitLog.Count, 0);

            bool fighterHit = false, enemyHit = false;
            foreach (HitLogEntry e in run.HitLog)
            {
                if (e.AttackerId == run.Config.FighterUnitId) fighterHit = true; else enemyHit = true;
                Assert.GreaterOrEqual(e.KbDistance, 0f);
                Assert.GreaterOrEqual(e.RunTick, 1);
                Assert.LessOrEqual(e.RunTick, run.TotalTicks);
            }
            Assert.IsTrue(fighterHit, "Fighter hits are logged");
            Assert.IsTrue(enemyHit, "enemy hits are logged too (excluded only from weapon stats)");
        }

        [Test]
        public void Recorder_BuildsHitRows_RunRow_AndDecisionRows()
        {
            RunSetupSO setup = MakeSpecSetup();
            WeaponConfigSO hammer = MakeWeapon("Giant Hammer", 24, 2.0f, 0.60f, 9.0f, 1.5f);
            RunManager run = PlayRun(setup, hammer, null, 9);

            var recorder = new RunLogRecorder(9, "Giant Hammer", null);
            recorder.BeginDecision(1, 10f);
            recorder.EndDecision(true, 13.5f);   // Continue after 3.5 s
            recorder.BeginDecision(2, 20f);
            recorder.EndDecision(false, 21f);    // Extract after 1.0 s
            recorder.EndDecision(true, 99f);     // no open decision: ignored

            var summary = new RunSummary { Reason = run.EndReason, FinalPot = run.FinalPot };
            RunLogRecord rec = recorder.Finish(run, summary, 90);

            Assert.AreEqual(9, rec.Seed);
            Assert.AreEqual(90, rec.BestPot);
            Assert.AreEqual(2, rec.Decisions.Count);
            Assert.AreEqual(run.HitLog.Count, rec.Hits.Count, "one row per hit");

            foreach (HitRow h in rec.Hits)
                Assert.AreEqual(h.AttackerId == run.Config.FighterUnitId ? "Giant Hammer" : "Enemy", h.Weapon);

            string[] decisionRows = { RunLogCsv.ToDecisionRow(9, rec.Decisions[0]), RunLogCsv.ToDecisionRow(9, rec.Decisions[1]) };
            Assert.AreEqual("9,1,Continue,3.500", decisionRows[0]);
            Assert.AreEqual("9,2,Extract,1.000", decisionRows[1]);
        }

        [Test]
        public void Recorder_RunRow_HasDeathCauseLastAttackerAndRingOutCount()
        {
            RunSetupSO setup = MakeSpecSetup();
            setup.fighterStats.maxHP = 1; // dies in wave 1
            RunManager run = PlayRun(setup, MakeWeapon("Fists", 8, 1.0f, 0.20f, 3.0f, 1.1f), null, 3);

            RunLogRecord rec = new RunLogRecorder(3, "Fists", "None").Finish(run, new RunSummary(), 0);
            SimUnit fighter = run.Sim.GetUnit(run.Config.FighterUnitId);

            Assert.AreEqual(HitTargetDeath.HP, rec.FighterDeath);
            Assert.AreEqual(fighter.LastAttackerId, rec.LastAttackerId);
            Assert.GreaterOrEqual(rec.LastAttackerId, 101);

            string[] cells = RunLogCsv.ToRow(rec).Split(',');
            Assert.AreEqual("HP", cells[System.Array.IndexOf(RunLogCsv.Columns, "death_cause")]);
            Assert.AreEqual(rec.LastAttackerId.ToString(), cells[System.Array.IndexOf(RunLogCsv.Columns, "last_attacker_id")]);
            StringAssert.StartsWith("Fists:", cells[System.Array.IndexOf(RunLogCsv.Columns, "ringouts_by_weapon")]);
        }

        // ---- CSV file ---------------------------------------------------------------------------

        [Test]
        public void Csv_HasAllRequiredColumns()
        {
            foreach (string required in new[] { "seed", "build_weapon", "build_trait", "wave_results", "death_cause",
                                                "last_attacker_id", "ringouts_by_weapon", "run_duration_s", "final_pot", "best_pot" })
                StringAssert.Contains(required, RunLogCsv.Header());

            Assert.AreEqual("seed,tick,attacker_id,target_id,weapon,damage,kb_distance,target_died", RunLogCsv.HitHeader());
            Assert.AreEqual("seed,wave,choice,decision_time_s", RunLogCsv.DecisionHeader());
        }

        [Test]
        public void Csv_HitRow_FormatsAllFields()
        {
            var hit = new HitRow(120, 1, 101, "Giant Hammer", 24, 2.1964f, HitTargetDeath.RingOut);
            Assert.AreEqual("42,120,1,101,Giant Hammer,24,2.196,RingOut", RunLogCsv.ToHitRow(42, hit));
        }

        [Test]
        public void Csv_EscapesCommasQuotesAndLineBreaks()
        {
            Assert.AreEqual("plain", RunLogCsv.Escape("plain"));
            Assert.AreEqual("\"a,b\"", RunLogCsv.Escape("a,b"));
            Assert.AreEqual("\"say \"\"hi\"\"\"", RunLogCsv.Escape("say \"hi\""));
            Assert.AreEqual("a b", RunLogCsv.Escape("a\nb"));
        }

        [Test]
        public void Csv_Append_WritesHeaderOnceAndOneRowPerRun_WithSeed()
        {
            RunSetupSO setup = MakeSpecSetup();
            WeaponConfigSO fists = MakeWeapon("Fists", 8, 1.0f, 0.20f, 3.0f, 1.1f);
            string dir = Path.Combine(Path.GetTempPath(), "gota_p06_" + System.Guid.NewGuid().ToString("N"));
            string path = Path.Combine(dir, "sub", "run_log.csv");
            string hitPath = Path.Combine(dir, "sub", "hit_log.csv");

            try
            {
                int hitRows = 0;
                foreach (int seed in new[] { 111, 222 })
                {
                    RunManager run = PlayRun(setup, fists, null, seed);
                    RunLogRecord rec = new RunLogRecorder(seed, "Fists", "Trait, with comma")
                        .Finish(run, new RunSummary { Reason = run.EndReason }, 0);
                    RunLogCsv.Append(path, rec);
                    RunLogCsv.AppendHits(hitPath, rec);
                    hitRows += rec.Hits.Count;
                }

                string[] lines = File.ReadAllLines(path);
                Assert.AreEqual(3, lines.Length, "header + 2 rows");
                Assert.AreEqual(RunLogCsv.Header(), lines[0]);
                StringAssert.StartsWith("111,", lines[1]);
                StringAssert.StartsWith("222,", lines[2]);
                StringAssert.Contains("\"Trait, with comma\"", lines[1]);

                string[] hitLines = File.ReadAllLines(hitPath);
                Assert.AreEqual(1 + hitRows, hitLines.Length, "hit header once + one row per hit");
                Assert.AreEqual(RunLogCsv.HitHeader(), hitLines[0]);
                StringAssert.StartsWith("111,", hitLines[1]);
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }
    }
}
