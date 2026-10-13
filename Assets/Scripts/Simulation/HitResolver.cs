using System;

namespace GotA.Simulation
{
    /// <summary>Pure C# weapon DTO (spec section 2). Enemies use this too.</summary>
    public sealed class WeaponConfig
    {
        public float BaseDamage;
        public float AttackInterval;
        public float Windup;
        public float BaseKnockback;
        public float Range;
    }

    /// <summary>Hit-resolution tuning values (pending test). Never hard-coded in logic.</summary>
    public sealed class HitRules
    {
        public float ThickSkinDamageMult = 0.75f;
        public float LifestealRatio = 0.25f;
        public float StaggerBase = 0.1f;
        public float StaggerPerImpulse = 0.04f;
        public float MinDirectionDistance = 0.001f;
    }

    public readonly struct HitResult
    {
        public readonly int FinalDamage;
        public readonly int ActualDamage;
        public readonly int Heal;
        public readonly float Impulse;
        public readonly float Stagger;      // stagger value computed for this hit
        public readonly bool DefenderDied;  // HP reached 0 (death marking itself is P-03)

        public HitResult(int finalDamage, int actualDamage, int heal,
                         float impulse, float stagger, bool defenderDied)
        {
            FinalDamage = finalDamage;
            ActualDamage = actualDamage;
            Heal = heal;
            Impulse = impulse;
            Stagger = stagger;
            DefenderDied = defenderDied;
        }
    }

    /// <summary>
    /// Spec section 3: Hit Resolution, exact order. A = attacker, D = defender.
    /// Pure C#: System.Math / MathF only, no UnityEngine.
    /// </summary>
    public static class HitResolver
    {
        public static HitResult Resolve(ArenaSim sim, SimUnit attacker, SimUnit defender, HitRules rules)
        {
            if (sim == null) throw new ArgumentNullException(nameof(sim));
            if (attacker == null) throw new ArgumentNullException(nameof(attacker));
            if (defender == null) throw new ArgumentNullException(nameof(defender));
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            if (attacker.Weapon == null) throw new InvalidOperationException("Attacker has no weapon.");

            // 1. x = A.BaseDamage * A.DmgMult
            float x = attacker.Weapon.BaseDamage * attacker.DmgMult;

            // 2. (Spear - not in prototype, hook kept)
            // 3. (Berserker - not in prototype, hook kept)

            // 4. Thick Skin
            if (defender.Trait == TraitType.ThickSkin)
                x *= rules.ThickSkinDamageMult;

            // 5. FinalDamage = max(1, RoundHalfUp(x))
            int finalDamage = Math.Max(1, (int)Math.Round(x, MidpointRounding.AwayFromZero));

            // 6. ActualDamage = min(Final, D.HP); D.HP -= Actual
            int actualDamage = Math.Min(finalDamage, defender.HP);
            defender.HP -= actualDamage;
            defender.LastAttackerId = attacker.UnitId;

            // 7. Lifesteal: only if A.HP > 0
            int heal = 0;
            if (attacker.Trait == TraitType.Lifesteal && attacker.HP > 0)
            {
                heal = (int)MathF.Floor(actualDamage * rules.LifestealRatio);
                attacker.HP = Math.Min(attacker.MaxHP, attacker.HP + heal);
            }

            // 8. Impulse = BaseKnockback * KBMult * (1 - KBResist); Dir = normalize(D - A)
            float impulse = attacker.Weapon.BaseKnockback * attacker.KBMult * (1f - defender.KBResist);
            SimVec2 delta = defender.Position - attacker.Position;
            SimVec2 dir = delta.Magnitude < rules.MinDirectionDistance
                ? new SimVec2(attacker.Facing >= 0 ? 1f : -1f, 0f)
                : delta.Normalized;
            sim.ApplyImpulse(defender, dir, impulse); // applied even if D.HP = 0 (visual only)

            // 9. D.StaggerTimer = max(D.StaggerTimer, 0.1 + 0.04 * Impulse)
            float stagger = rules.StaggerBase + rules.StaggerPerImpulse * impulse;
            defender.StaggerTimer = MathF.Max(defender.StaggerTimer, stagger);

            return new HitResult(finalDamage, actualDamage, heal, impulse, stagger, defender.HP <= 0);
        }
    }
}
