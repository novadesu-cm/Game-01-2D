using System;
using GotA.Simulation;
using NUnit.Framework;

namespace GotA.Tests
{
    public class ArenaPhysicsTests
    {
        private const float Tolerance = 0.001f;

        private static ArenaSim NewSim() => new ArenaSim(new ArenaConfig());

        [Test]
        public void Drag_SingleStep_MatchesFormula()
        {
            ArenaSim sim = NewSim();
            SimUnit u = sim.AddUnit(1, 0, SimVec2.Zero, 0.4f);
            sim.ApplyImpulse(u, new SimVec2(1f, 0f), 8f);

            sim.PhysicsStep();

            float expectedVel = 8f / (1f + 4f * 0.02f);
            Assert.AreEqual(expectedVel, u.Velocity.x, Tolerance);
            Assert.AreEqual(0f, u.Velocity.y, Tolerance);
            Assert.AreEqual(expectedVel * 0.02f, u.Position.x, Tolerance);
        }

        [Test]
        public void Impulse_AddsToVelocity()
        {
            ArenaSim sim = NewSim();
            SimUnit u = sim.AddUnit(1, 0, SimVec2.Zero, 0.4f);

            sim.ApplyImpulse(u, new SimVec2(0f, 1f), 3f);
            sim.ApplyImpulse(u, new SimVec2(1f, 0f), 2f);

            Assert.AreEqual(2f, u.Velocity.x, Tolerance);
            Assert.AreEqual(3f, u.Velocity.y, Tolerance);
        }

        [Test]
        public void Impulse8_SlidesAboutTwoUnits()
        {
            ArenaSim sim = NewSim();
            SimUnit u = sim.AddUnit(1, 0, SimVec2.Zero, 0.4f);
            sim.ApplyImpulse(u, new SimVec2(1f, 0f), 8f);

            for (int i = 0; i < 1000; i++) sim.PhysicsStep();

            Assert.AreEqual(2.0f, u.Position.x, Tolerance);
            Assert.AreEqual(0.0f, u.Position.y, Tolerance);
            Assert.AreEqual(0.0f, u.Velocity.Magnitude, Tolerance);
        }

        [Test]
        public void NoImpulse_UnitDoesNotMove()
        {
            ArenaSim sim = NewSim();
            SimUnit u = sim.AddUnit(1, 0, new SimVec2(1f, 2f), 0.4f);

            for (int i = 0; i < 50; i++) sim.Tick();

            Assert.AreEqual(1f, u.Position.x, Tolerance);
            Assert.AreEqual(2f, u.Position.y, Tolerance);
        }

        [Test]
        public void Collision_OverlappingUnitsAreSeparatedFiftyFifty()
        {
            ArenaSim sim = NewSim();
            SimUnit a = sim.AddUnit(1, 0, new SimVec2(-0.3f, 0f), 0.4f);
            SimUnit b = sim.AddUnit(101, 1, new SimVec2(0.3f, 0f), 0.4f);

            sim.PhysicsStep();

            // overlap = 0.8 - 0.6 = 0.2 -> each moves 0.1
            Assert.AreEqual(-0.4f, a.Position.x, Tolerance);
            Assert.AreEqual(0.4f, b.Position.x, Tolerance);
            Assert.AreEqual(0.8f, SimVec2.Distance(a.Position, b.Position), Tolerance);
        }

        [Test]
        public void Collision_UsesEachUnitsOwnBodyRadius()
        {
            ArenaSim sim = NewSim();
            SimUnit a = sim.AddUnit(1, 0, new SimVec2(0f, 0f), 0.40f);
            SimUnit b = sim.AddUnit(101, 1, new SimVec2(0f, 0.5f), 0.55f);

            sim.PhysicsStep();

            Assert.AreEqual(0.95f, SimVec2.Distance(a.Position, b.Position), Tolerance);
            Assert.AreEqual(-0.225f, a.Position.y, Tolerance);
            Assert.AreEqual(0.725f, b.Position.y, Tolerance);
        }

        [Test]
        public void Collision_TouchingButNotOverlappingDoesNothing()
        {
            ArenaSim sim = NewSim();
            SimUnit a = sim.AddUnit(1, 0, new SimVec2(0f, 0f), 0.4f);
            SimUnit b = sim.AddUnit(2, 0, new SimVec2(0.8f, 0f), 0.4f);

            sim.PhysicsStep();

            Assert.AreEqual(0f, a.Position.x, Tolerance);
            Assert.AreEqual(0.8f, b.Position.x, Tolerance);
        }

        [Test]
        public void Collision_DeadUnitsAreIgnored()
        {
            ArenaSim sim = NewSim();
            SimUnit alive = sim.AddUnit(1, 0, new SimVec2(0f, 0f), 0.4f);
            SimUnit dead = sim.AddUnit(2, 1, new SimVec2(9f, 0f), 0.4f);
            // Kill via ring-out, then move it on top of the living unit.
            sim.CheckRingOut();
            Assert.IsFalse(dead.IsAlive);
            dead.Position = new SimVec2(0.1f, 0f);

            sim.PhysicsStep();

            Assert.AreEqual(0f, alive.Position.x, Tolerance);
            Assert.AreEqual(0.1f, dead.Position.x, Tolerance);
        }

        [Test]
        public void Collision_CoincidentUnitsSeparateDeterministically()
        {
            ArenaSim sim = NewSim();
            SimUnit a = sim.AddUnit(1, 0, new SimVec2(1f, 1f), 0.4f);
            SimUnit b = sim.AddUnit(2, 1, new SimVec2(1f, 1f), 0.4f);

            sim.PhysicsStep();

            Assert.AreEqual(0.8f, SimVec2.Distance(a.Position, b.Position), Tolerance);
            Assert.Less(a.Position.x, b.Position.x); // lower UnitId goes to -x
            Assert.AreEqual(1f, a.Position.y, Tolerance);
            Assert.AreEqual(1f, b.Position.y, Tolerance);
        }

        [Test]
        public void Units_AreKeptInUnitIdOrder_AndDuplicatesRejected()
        {
            ArenaSim sim = NewSim();
            sim.AddUnit(103, 1, SimVec2.Zero, 0.4f);
            sim.AddUnit(1, 0, SimVec2.Zero, 0.4f);
            sim.AddUnit(101, 1, SimVec2.Zero, 0.4f);

            Assert.AreEqual(1, sim.Units[0].UnitId);
            Assert.AreEqual(101, sim.Units[1].UnitId);
            Assert.AreEqual(103, sim.Units[2].UnitId);
            Assert.Throws<ArgumentException>(() => sim.AddUnit(101, 1, SimVec2.Zero, 0.4f));
        }

        [Test]
        public void Tick_AdvancesTickCount()
        {
            ArenaSim sim = NewSim();
            for (int i = 0; i < 5; i++) sim.Tick();
            Assert.AreEqual(5, sim.TickCount);
        }

        [Test]
        public void SimVec2_Helpers()
        {
            var v = new SimVec2(3f, 4f);
            Assert.AreEqual(5f, v.Magnitude, Tolerance);
            Assert.AreEqual(0.6f, v.Normalized.x, Tolerance);
            Assert.AreEqual(0.8f, v.Normalized.y, Tolerance);
            Assert.AreEqual(SimVec2.Zero, SimVec2.Zero.Normalized);
            Assert.AreEqual(5f, SimVec2.Distance(new SimVec2(1f, 1f), new SimVec2(4f, 5f)), Tolerance);
        }

        [Test]
        public void SimulationAssembly_DoesNotReferenceUnityEngine()
        {
            foreach (var name in typeof(ArenaSim).Assembly.GetReferencedAssemblies())
                Assert.IsFalse(name.Name.StartsWith("UnityEngine"), "GotA.Simulation references " + name.Name);
        }
    }
}
