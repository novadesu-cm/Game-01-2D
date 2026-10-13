using System;

namespace GotA.Simulation
{
    /// <summary>
    /// AI state machine (spec section 4), shared by Gladiator, Grunt and Brute:
    /// they differ only by data (stats, weapon). States: Frozen, Chase, Ready,
    /// Windup, Dead, Idle. No Retreat. Pure C#, no UnityEngine.
    /// Windup resolution (the hit itself) is done by ArenaSim in tick step 3.
    /// </summary>
    internal static class UnitAi
    {
        public static void Update(ArenaSim sim, SimUnit u)
        {
            if (!u.IsAlive || u.Weapon == null) return;
            if (u.State == AiState.Idle || u.State == AiState.Frozen || u.State == AiState.Dead) return;

            // Stagger: AI must not set velocity and must not start a Windup.
            bool staggered = u.StaggerTimer > 0f || u.Velocity.Magnitude > u.MoveSpeed;

            if (u.State == AiState.Windup)
            {
                // Stagger does not cancel a started Windup. No retargeting during Windup.
                SimUnit locked = sim.GetUnit(u.LockedTargetId);
                if (locked == null || !locked.IsAlive)
                {
                    CancelWindup(u);
                    return;
                }
                Face(u, locked);
                if (!staggered) u.Velocity = SimVec2.Zero; // stand
                return;
            }

            UpdateTarget(sim, u);
            SimUnit target = sim.GetUnit(u.TargetId);
            if (target == null || !target.IsAlive || target.Team == u.Team) return;

            Face(u, target);
            if (staggered) return;

            // At most two transitions per tick (Chase -> Ready, Ready -> Chase).
            for (int guard = 0; guard < 3; guard++)
            {
                float dist = SimVec2.Distance(u.Position, target.Position);
                float range = u.Weapon.Range;

                if (u.State == AiState.Chase)
                {
                    if (dist <= range)
                    {
                        u.State = AiState.Ready;
                        continue;
                    }
                    u.Velocity = (target.Position - u.Position).Normalized * u.MoveSpeed;
                    return;
                }

                if (u.State == AiState.Ready)
                {
                    if (dist > range)
                    {
                        u.State = AiState.Chase;
                        continue;
                    }
                    u.Velocity = SimVec2.Zero;
                    if (u.Cooldown <= 0f) StartWindup(u);
                    return;
                }

                return;
            }
        }

        /// <summary>Windup enter: stand, lock target, timer = EffWindup, Cooldown = EffInterval.</summary>
        private static void StartWindup(SimUnit u)
        {
            u.State = AiState.Windup;
            u.WindupTimer = u.EffWindup;
            u.Cooldown = u.EffInterval;
            u.LockedTargetId = u.TargetId;
            u.Velocity = SimVec2.Zero;
        }

        /// <summary>Target died during Windup: cancel, Cooldown = 0, retarget next tick.</summary>
        private static void CancelWindup(SimUnit u)
        {
            u.State = AiState.Chase;
            u.Cooldown = 0f;
            u.WindupTimer = 0f;
            u.LockedTargetId = 0;
            u.TargetId = 0;
        }

        private static void Face(SimUnit u, SimUnit target)
        {
            if (target.Position.x > u.Position.x) u.Facing = 1;
            else if (target.Position.x < u.Position.x) u.Facing = -1;
        }

        /// <summary>
        /// Nearest living enemy. Retarget when none, target dead, or every RetargetInterval
        /// (switch only if the new one is at least RetargetSwitchMargin closer).
        /// </summary>
        private static void UpdateTarget(ArenaSim sim, SimUnit u)
        {
            AiRules rules = sim.AiRules;
            SimUnit current = sim.GetUnit(u.TargetId);
            bool currentValid = current != null && current.IsAlive && current.Team != u.Team;

            if (!currentValid)
            {
                SimUnit best = FindBest(sim, u);
                u.TargetId = best != null ? best.UnitId : 0;
                u.RetargetTimer = rules.RetargetInterval;
                return;
            }

            if (u.RetargetTimer > 0f) return;
            u.RetargetTimer = rules.RetargetInterval;

            SimUnit candidate = FindBest(sim, u);
            if (candidate == null || candidate.UnitId == current.UnitId) return;

            float currentDist = SimVec2.Distance(u.Position, current.Position);
            float candidateDist = SimVec2.Distance(u.Position, candidate.Position);
            if (candidateDist <= currentDist - rules.RetargetSwitchMargin)
                u.TargetId = candidate.UnitId;
        }

        /// <summary>Nearest living enemy; tie (within TieDistance): lower current HP, then lower UnitID.</summary>
        private static SimUnit FindBest(ArenaSim sim, SimUnit u)
        {
            float tie = sim.AiRules.TieDistance;
            SimUnit best = null;
            float bestDist = 0f;

            // Units are iterated in ascending UnitId, so on a full tie the earlier (lower) id is kept.
            for (int i = 0; i < sim.Units.Count; i++)
            {
                SimUnit c = sim.Units[i];
                if (!c.IsAlive || c.Team == u.Team) continue;

                float d = SimVec2.Distance(u.Position, c.Position);
                if (best == null)
                {
                    best = c;
                    bestDist = d;
                    continue;
                }

                if (d < bestDist - tie)
                {
                    best = c;
                    bestDist = d;
                }
                else if (d <= bestDist + tie && c.HP < best.HP)
                {
                    best = c;
                    bestDist = d;
                }
            }
            return best;
        }
    }
}
