using GotA.Simulation;
using NUnit.Framework;

namespace GotA.Tests
{
    public class HitResolverTests
    {
        private const float Tol = 0.001f;

        private static readonly HitRules Rules = new HitRules();

        private static readonly WeaponConfig Hammer = new WeaponConfig
            { BaseDamage = 24f, AttackInterval = 2.0f, Windup = 0.60f, BaseKnockback = 9.0f, Range = 1.5f };
        private static readonly WeaponConfig Daggers = new WeaponConfig
            { BaseDamage = 5f, AttackInterval = 0.4f, Windup = 0.08f, BaseKnockback = 1.0f, Range = 1.0f };
        private static readonly WeaponConfig GruntAttack = new WeaponConfig
            { BaseDamage = 4f, AttackInterval = 1.2f, Windup = 0.30f, BaseKnockback = 2.5f, Range = 1.1f };

        private static ArenaSim NewSim() => new ArenaSim(new ArenaConfig());

        private static SimUnit Gladiator(ArenaSim sim, WeaponConfig weapon, TraitType trait, int hp = 120)
        {
            SimUnit u = sim.AddUnit(1, 0, new SimVec2(-1f, 0f), 0.40f);
            u.MaxHP = 120;
            u.HP = hp;
            u.KBResist = 0.10f;
            u.Weapon = weapon;
            u.Trait = trait;
            return u;
        }

        private static SimUnit Grunt(ArenaSim sim)
        {
            SimUnit u = sim.AddUnit(101, 1, new SimVec2(0f, 0f), 0.40f);
            u.MaxHP = 40;
            u.HP = 40;
            u.KBResist = 0f;
            u.Weapon = GruntAttack;
            return u;
        }

        [Test] // Test 1
        public void GladiatorHammer_HitsGrunt()
        {
            ArenaSim sim = NewSim();
            SimUnit g = Gladiator(sim, Hammer, TraitType.None);
            SimUnit e = Grunt(sim);

            HitResult r = HitResolver.Resolve(sim, g, e, Rules);

            Assert.AreEqual(24, r.FinalDamage);
            Assert.AreEqual(9.0f, r.Impulse, Tol);
            Assert.AreEqual(0.46f, r.Stagger, Tol);
            Assert.AreEqual(0.46f, e.StaggerTimer, Tol);
            Assert.AreEqual(16, e.HP);
        }

        [Test] // Test 2
        public void GladiatorHammerLifesteal_HealsSix()
        {
            ArenaSim sim = NewSim();
            SimUnit g = Gladiator(sim, Hammer, TraitType.Lifesteal, hp: 100);
            SimUnit e = Grunt(sim);

            HitResult r = HitResolver.Resolve(sim, g, e, Rules);

            Assert.AreEqual(6, r.Heal);
            Assert.AreEqual(106, g.HP);
        }

        [Test] // Test 3
        public void GladiatorDaggers_HitsGrunt()
        {
            ArenaSim sim = NewSim();
            SimUnit g = Gladiator(sim, Daggers, TraitType.None);
            SimUnit e = Grunt(sim);

            HitResult r = HitResolver.Resolve(sim, g, e, Rules);

            Assert.AreEqual(5, r.FinalDamage);
            Assert.AreEqual(1.0f, r.Impulse, Tol);
            Assert.AreEqual(0.14f, r.Stagger, Tol);
        }

        [Test] // Test 4
        public void GruntHitsGladiatorThickSkin()
        {
            ArenaSim sim = NewSim();
            SimUnit g = Gladiator(sim, Hammer, TraitType.ThickSkin);
            SimUnit e = Grunt(sim);

            HitResult r = HitResolver.Resolve(sim, e, g, Rules);

            Assert.AreEqual(3, r.FinalDamage);
            Assert.AreEqual(2.25f, r.Impulse, Tol);
            Assert.AreEqual(0.19f, r.Stagger, Tol);
            Assert.AreEqual(117, g.HP);
        }

        [Test] // Test 5: x = 0.4 after Thick Skin -> Final 1 (EX-6)
        public void MinimumDamageIsOne()
        {
            ArenaSim sim = NewSim();
            SimUnit g = Gladiator(sim, Hammer, TraitType.ThickSkin);
            SimUnit e = Grunt(sim);
            e.Weapon = new WeaponConfig { BaseDamage = 0.4f, BaseKnockback = 2.5f };

            HitResult r = HitResolver.Resolve(sim, e, g, Rules);

            Assert.AreEqual(1, r.FinalDamage);
        }

        [Test] // Test 6: attacker with HP 0 gets no Lifesteal
        public void DeadAttackerGetsNoLifesteal()
        {
            ArenaSim sim = NewSim();
            SimUnit g = Gladiator(sim, Hammer, TraitType.Lifesteal, hp: 0);
            SimUnit e = Grunt(sim);

            HitResult r = HitResolver.Resolve(sim, g, e, Rules);

            Assert.AreEqual(0, r.Heal);
            Assert.AreEqual(0, g.HP);
        }

        [Test] // Test 7: dist < 0.001 -> direction = attacker facing
        public void CoincidentUnits_UseAttackerFacing()
        {
            ArenaSim sim = NewSim();
            SimUnit g = Gladiator(sim, Hammer, TraitType.None);
            SimUnit e = Grunt(sim);
            g.Position = e.Position;
            g.Facing = -1;

            HitResolver.Resolve(sim, g, e, Rules);

            Assert.AreEqual(-9.0f, e.Velocity.x, Tol);
            Assert.AreEqual(0f, e.Velocity.y, Tol);
        }
    }
}
