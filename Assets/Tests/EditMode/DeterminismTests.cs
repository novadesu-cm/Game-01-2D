using System;
using System.Collections.Generic;
using GotA.Simulation;
using NUnit.Framework;

namespace GotA.Tests
{
    public class DeterminismTests
    {
        private const int TickCountToRun = 500;
        private const float Tolerance = 0.001f;

        /// <summary>
        /// Builds a scene and drives it with System.Random(seed) only. Records every
        /// unit's position and alive flag after every tick.
        /// </summary>
        private static List<float[]> Run(int seed)
        {
            var rng = new Random(seed);
            var sim = new ArenaSim(new ArenaConfig());

            // Some units spawn close together so collisions occur.
            for (int i = 0; i < 8; i++)
            {
                float angle = (float)(rng.NextDouble() * 2.0 * Math.PI);
                float radius = (float)(rng.NextDouble() * 4.0);
                var pos = new SimVec2(
                    (float)Math.Cos(angle) * radius,
                    (float)Math.Sin(angle) * radius);
                sim.AddUnit(i == 0 ? 1 : 100 + i, i == 0 ? 0 : 1, pos, i % 2 == 0 ? 0.40f : 0.55f);
            }

            var frames = new List<float[]>(TickCountToRun);
            for (int tick = 0; tick < TickCountToRun; tick++)
            {
                if (tick % 25 == 0)
                {
                    foreach (SimUnit u in sim.Units)
                    {
                        float a = (float)(rng.NextDouble() * 2.0 * Math.PI);
                        float strength = (float)(rng.NextDouble() * 10.0);
                        sim.ApplyImpulse(u, new SimVec2((float)Math.Cos(a), (float)Math.Sin(a)), strength);
                    }
                }

                sim.Tick();

                var frame = new float[sim.Units.Count * 3];
                for (int i = 0; i < sim.Units.Count; i++)
                {
                    frame[i * 3] = sim.Units[i].Position.x;
                    frame[i * 3 + 1] = sim.Units[i].Position.y;
                    frame[i * 3 + 2] = sim.Units[i].IsAlive ? 1f : 0f;
                }
                frames.Add(frame);
            }
            return frames;
        }

        [TestCase(1)]
        [TestCase(42)]
        [TestCase(12345)]
        public void SameSeedTwice_PositionsIdenticalEveryTick(int seed)
        {
            List<float[]> first = Run(seed);
            List<float[]> second = Run(seed);

            Assert.AreEqual(TickCountToRun, first.Count);
            Assert.AreEqual(first.Count, second.Count);
            for (int tick = 0; tick < first.Count; tick++)
            {
                Assert.AreEqual(first[tick].Length, second[tick].Length);
                for (int k = 0; k < first[tick].Length; k++)
                    Assert.AreEqual(first[tick][k], second[tick][k], Tolerance,
                        "seed " + seed + " tick " + tick + " value " + k);
            }
        }

        [Test]
        public void DifferentSeeds_ProduceDifferentRuns()
        {
            List<float[]> a = Run(1);
            List<float[]> b = Run(2);

            bool anyDifference = false;
            for (int k = 0; k < a[TickCountToRun - 1].Length && !anyDifference; k++)
                anyDifference = Math.Abs(a[TickCountToRun - 1][k] - b[TickCountToRun - 1][k]) > Tolerance;

            Assert.IsTrue(anyDifference, "Different seeds should not give identical results.");
        }

        [Test]
        public void ScenarioIsNonTrivial_SomeUnitsRingOut()
        {
            // Guards against a vacuous determinism test.
            bool anyRingOut = false;
            for (int seed = 1; seed <= 20 && !anyRingOut; seed++)
            {
                float[] last = Run(seed)[TickCountToRun - 1];
                for (int i = 2; i < last.Length; i += 3)
                    if (last[i] == 0f) anyRingOut = true;
            }
            Assert.IsTrue(anyRingOut);
        }
    }
}
