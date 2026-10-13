using GotA.Simulation;
using NUnit.Framework;

namespace GotA.Tests
{
    public class RingOutTests
    {
        private const float Tolerance = 0.001f;

        private static ArenaSim NewSim() => new ArenaSim(new ArenaConfig());

        // 20 ring-out decisions; all 20 must be correct (P-01 DoD).
        // Rule: distance(pos,(0,0)) > 6.0 -> dead. Exactly 6.0 = alive.
        [TestCase(0f, 0f, true)]            //  1 center
        [TestCase(1f, 1f, true)]            //  2
        [TestCase(-3f, 0f, true)]           //  3 fighter spawn
        [TestCase(3f, -1f, true)]           //  4 enemy spawn
        [TestCase(5.99f, 0f, true)]         //  5 just inside (+x)
        [TestCase(-5.99f, 0f, true)]        //  6 just inside (-x)
        [TestCase(0f, 5.99f, true)]         //  7 just inside (+y)
        [TestCase(0f, -5.99f, true)]        //  8 just inside (-y)
        [TestCase(6.0f, 0f, true)]          //  9 exactly on the edge (+x)
        [TestCase(-6.0f, 0f, true)]         // 10 exactly on the edge (-x)
        [TestCase(0f, 6.0f, true)]          // 11 exactly on the edge (+y)
        [TestCase(0f, -6.0f, true)]         // 12 exactly on the edge (-y)
        [TestCase(4.2f, 4.2f, true)]        // 13 diagonal inside (5.94)
        [TestCase(6.01f, 0f, false)]        // 14 just outside (+x)
        [TestCase(-6.01f, 0f, false)]       // 15 just outside (-x)
        [TestCase(0f, 6.01f, false)]        // 16 just outside (+y)
        [TestCase(0f, -6.01f, false)]       // 17 just outside (-y)
        [TestCase(4.3f, 4.3f, false)]       // 18 diagonal outside (6.08)
        [TestCase(-4.3f, 4.3f, false)]      // 19 diagonal outside
        [TestCase(10f, -10f, false)]        // 20 far outside
        public void RingOut_DecidesCorrectly(float x, float y, bool expectedAlive)
        {
            ArenaSim sim = NewSim();
            SimUnit unit = sim.AddUnit(1, 0, new SimVec2(x, y), 0.4f);

            sim.CheckRingOut();

            Assert.AreEqual(expectedAlive, unit.IsAlive);
            Assert.AreEqual(expectedAlive ? DeathCause.None : DeathCause.RingOut, unit.DeathCause);
        }

        [Test]
        public void RingOut_IgnoresUnitBodyRadius()
        {
            ArenaSim sim = NewSim();
            // Center is inside; the body would overlap the edge if radius counted.
            SimUnit unit = sim.AddUnit(1, 0, new SimVec2(5.9f, 0f), 0.55f);

            sim.CheckRingOut();

            Assert.IsTrue(unit.IsAlive);
        }

        [Test]
        public void RingOut_HappensThroughImpulseAndDragSlide()
        {
            ArenaSim sim = NewSim();
            // Impulse 8 slides about 2.0 u: 4.5 -> ~6.5 is outside, 3.0 -> ~5.0 stays inside.
            SimUnit outUnit = sim.AddUnit(1, 0, new SimVec2(4.5f, 0f), 0.4f);
            SimUnit inUnit = sim.AddUnit(2, 1, new SimVec2(-3.0f, 0f), 0.4f);
            sim.ApplyImpulse(outUnit, new SimVec2(1f, 0f), 8f);
            sim.ApplyImpulse(inUnit, new SimVec2(1f, 0f), 8f);

            for (int i = 0; i < 300; i++) sim.Tick();

            Assert.IsFalse(outUnit.IsAlive);
            Assert.AreEqual(DeathCause.RingOut, outUnit.DeathCause);
            Assert.IsTrue(inUnit.IsAlive);
            Assert.AreEqual(DeathCause.None, inUnit.DeathCause);
        }

        [Test]
        public void RingOut_DeadUnitStaysDeadWithRingOutCause()
        {
            ArenaSim sim = NewSim();
            SimUnit unit = sim.AddUnit(1, 0, new SimVec2(7f, 0f), 0.4f);
            sim.Tick();
            Assert.IsFalse(unit.IsAlive);

            // Drift back inside: death is permanent.
            unit.Position = new SimVec2(0f, 0f);
            sim.Tick();

            Assert.IsFalse(unit.IsAlive);
            Assert.AreEqual(DeathCause.RingOut, unit.DeathCause);
        }

        [Test]
        public void Arena_UsesConfiguredRadiusOfSix()
        {
            Assert.AreEqual(6.0f, new ArenaConfig().ArenaRadius, Tolerance);
            Assert.AreEqual(0.02f, new ArenaConfig().FixedDt, Tolerance);
        }
    }
}
