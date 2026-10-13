using System;
using System.Collections.Generic;

namespace GotA.Simulation
{
    /// <summary>
    /// P-01: circular arena, custom deterministic physics, ring-out.
    /// Pure C# - no UnityEngine. Fixed step (ArenaConfig.FixedDt), no randomness,
    /// no wall-clock input: identical inputs always give identical state.
    /// </summary>
    public sealed class ArenaSim
    {
        // Numerical guard for coincident bodies (not a gameplay tuning value).
        private const float MinSeparationDistance = 1e-6f;

        public const int FighterTeam = 0;
        public const int EnemyTeam = 1;

        // Numerical guard so float timers that should land on 0 do (not a tuning value).
        private const float TimerEpsilon = 1e-4f;

        private readonly ArenaConfig _config;
        private readonly HitRules _hitRules;
        private readonly AiRules _aiRules;
        private readonly float _dragFactor;
        private readonly List<SimUnit> _units = new List<SimUnit>(); // always sorted by UnitId

        /// <summary>D-08: length of the knockback measurement window of one hit, in ticks.</summary>
        public const int KbWindowTicks = 50;

        // P-06 / D-08 measurement only: never read by gameplay code.
        private readonly List<KbRecord> _openKb = new List<KbRecord>();
        private readonly List<HitLogEntry> _hitLog = new List<HitLogEntry>();

        public ArenaSim(ArenaConfig config, HitRules hitRules = null, AiRules aiRules = null)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            config.Validate();
            _config = config;
            _hitRules = hitRules ?? new HitRules();
            _aiRules = aiRules ?? new AiRules();
            _dragFactor = 1f / (1f + config.Drag * config.FixedDt);
        }

        public ArenaConfig Config => _config;

        public HitRules HitRules => _hitRules;

        public AiRules AiRules => _aiRules;

        /// <summary>Sandbox until StartCombat; Active while fighting; Ended after an outcome.</summary>
        public CombatPhase Phase { get; private set; }

        /// <summary>Result of the wave. None until the phase becomes Ended.</summary>
        public CombatOutcome Outcome { get; private set; }

        /// <summary>Seconds left in the wave (counts down in tick step 2).</summary>
        public float WaveTimer { get; private set; }

        /// <summary>Number of completed Tick() calls.</summary>
        public int TickCount { get; private set; }

        /// <summary>Units in ascending UnitId order.</summary>
        public IReadOnlyList<SimUnit> Units => _units;

        /// <summary>
        /// P-06: when false no per-hit measurement is made (hit log stays empty).
        /// The simulation result is identical either way.
        /// </summary>
        public bool MeasureKnockback { get; set; } = true;

        /// <summary>Per-hit records whose window has ended, in the order the windows closed.</summary>
        public IReadOnlyList<HitLogEntry> HitLog => _hitLog;

        public SimUnit AddUnit(int unitId, int team, SimVec2 position, float bodyRadius)
        {
            if (bodyRadius < 0f) throw new ArgumentOutOfRangeException(nameof(bodyRadius));

            int index = 0;
            while (index < _units.Count && _units[index].UnitId < unitId) index++;
            if (index < _units.Count && _units[index].UnitId == unitId)
                throw new ArgumentException("Duplicate UnitId " + unitId, nameof(unitId));

            var unit = new SimUnit(unitId, team, position, bodyRadius);
            _units.Insert(index, unit);
            return unit;
        }

        /// <summary>Adds a unit from stat/weapon DTOs (full HP, AI not started until StartCombat).</summary>
        public SimUnit AddUnit(int unitId, int team, SimVec2 position, UnitStats stats,
                               WeaponConfig weapon, TraitType trait = TraitType.None)
        {
            if (stats == null) throw new ArgumentNullException(nameof(stats));
            SimUnit u = AddUnit(unitId, team, position, stats.BodyRadius);
            u.MaxHP = stats.MaxHP;
            u.HP = stats.MaxHP;
            u.MoveSpeed = stats.MoveSpeed;
            u.DmgMult = stats.DmgMult;
            u.AtkSpdMult = stats.AtkSpdMult;
            u.KBMult = stats.KBMult;
            u.KBResist = stats.KBResist;
            u.Weapon = weapon;
            u.Trait = trait;
            return u;
        }

        /// <summary>
        /// Begins a wave: every unit with a weapon starts in Chase (Cooldown 0, no target),
        /// the wave timer is set, and Tick() switches to the full spec section 5 order.
        /// </summary>
        public void StartCombat(float waveTimeLimit)
        {
            if (!(waveTimeLimit > 0f)) throw new ArgumentOutOfRangeException(nameof(waveTimeLimit));

            Phase = CombatPhase.Active;
            Outcome = CombatOutcome.None;
            WaveTimer = waveTimeLimit;

            for (int i = 0; i < _units.Count; i++)
            {
                SimUnit u = _units[i];
                if (!u.IsAlive || u.Weapon == null) continue;
                u.State = AiState.Chase;
                u.Cooldown = 0f;
                u.WindupTimer = 0f;
                u.RetargetTimer = 0f;
                u.TargetId = 0;
                u.LockedTargetId = 0;
            }
        }

        public SimUnit GetUnit(int unitId)
        {
            for (int i = 0; i < _units.Count; i++)
                if (_units[i].UnitId == unitId) return _units[i];
            return null;
        }

        /// <summary>Impulse (mass 1): vel += dir * impulse. Caller passes a normalized dir.</summary>
        public void ApplyImpulse(SimUnit unit, SimVec2 dir, float impulse)
        {
            if (unit == null) throw new ArgumentNullException(nameof(unit));
            unit.Velocity = unit.Velocity + dir * impulse;
        }

        /// <summary>
        /// One fixed step. Sandbox phase: ring-out check + physics only (P-01).
        /// Active phase, spec section 5 order:
        /// 1) ring-out, 2) timers, 3) windup hits (ascending attacker UnitId),
        /// 4) death marking, 5) outcome check (stop immediately), 6) AI update, 7) physics.
        /// Ended phase: nothing happens.
        /// </summary>
        public void Tick()
        {
            if (Phase == CombatPhase.Ended) return;

            CheckRingOut();                                   // 1

            if (Phase == CombatPhase.Active)
            {
                StepTimers();                                 // 2
                ResolveWindups();                             // 3
                MarkDeaths();                                 // 4

                if (CheckOutcome())                           // 5
                {
                    TickCount++;
                    StepKbMeasurement(false);                 // P-06 measurement only
                    FinishKbMeasurement();
                    return;                                   // physics off on the outcome tick
                }

                for (int i = 0; i < _units.Count; i++)        // 6
                    UnitAi.Update(this, _units[i]);
            }

            PhysicsStep();                                    // 7
            TickCount++;
            StepKbMeasurement(true);                          // P-06 measurement only
        }

        // ------------------------------------------------------------------
        // P-06 / D-08 knockback measurement. Reads unit alive/death state, never
        // writes any simulation state; KbVel is separate from Position/Velocity.
        // ------------------------------------------------------------------

        /// <summary>Called by HitResolver after a hit resolved (step 8 impulse and direction known).</summary>
        internal void NotifyHit(SimUnit attacker, SimUnit target, int actualDamage, SimVec2 dir, float impulse)
        {
            if (!MeasureKnockback) return;

            var entry = new HitLogEntry
            {
                WaveTick = TickCount + 1,
                RunTick = TickCount + 1,
                AttackerId = attacker.UnitId,
                TargetId = target.UnitId,
                Damage = actualDamage,
                TargetDied = HitTargetDeath.None
            };

            // A killing hit (HP 0) is marked dead in step 4 of this same tick: it closes with distance 0.
            _openKb.Add(new KbRecord
            {
                Entry = entry,
                Target = target,
                KbVel = target.HP > 0 ? dir * impulse : SimVec2.Zero
            });
        }

        /// <summary>
        /// End of every tick: close windows of dead targets (cause HP / RingOut), otherwise advance
        /// KbVel by one drag step (only on ticks where physics ran) and close after KbWindowTicks.
        /// </summary>
        private void StepKbMeasurement(bool physicsRan)
        {
            if (_openKb.Count == 0) return;

            float dt = _config.FixedDt;
            int write = 0;
            for (int i = 0; i < _openKb.Count; i++)
            {
                KbRecord r = _openKb[i];

                if (!r.Target.IsAlive)
                {
                    r.Entry.TargetDied = r.Target.DeathCause == DeathCause.RingOut
                        ? HitTargetDeath.RingOut : HitTargetDeath.HP;
                    _hitLog.Add(r.Entry);
                    continue;
                }

                if (physicsRan)
                {
                    r.KbVel = r.KbVel * _dragFactor;
                    r.Entry.KbDistance += r.KbVel.Magnitude * dt;
                    r.TicksDone++;
                    if (r.TicksDone >= KbWindowTicks)
                    {
                        _hitLog.Add(r.Entry);
                        continue;
                    }
                }

                _openKb[write++] = r;
            }
            _openKb.RemoveRange(write, _openKb.Count - write);
        }

        /// <summary>
        /// The wave ended (outcome): physics is off, so the remaining window ticks are completed
        /// analytically on KbVel alone, for targets still alive. The measurement stays the pure D-08 value.
        /// </summary>
        private void FinishKbMeasurement()
        {
            float dt = _config.FixedDt;
            for (int i = 0; i < _openKb.Count; i++)
            {
                KbRecord r = _openKb[i];
                while (r.TicksDone < KbWindowTicks)
                {
                    r.KbVel = r.KbVel * _dragFactor;
                    r.Entry.KbDistance += r.KbVel.Magnitude * dt;
                    r.TicksDone++;
                }
                _hitLog.Add(r.Entry);
            }
            _openKb.Clear();
        }

        private static float Decay(float timer, float dt)
        {
            float t = timer - dt;
            return t <= TimerEpsilon ? 0f : t;
        }

        /// <summary>Step 2: Cooldown, Stagger, Windup, Retarget, WaveTimer all -dt.</summary>
        private void StepTimers()
        {
            float dt = _config.FixedDt;
            for (int i = 0; i < _units.Count; i++)
            {
                SimUnit u = _units[i];
                if (!u.IsAlive) continue;
                u.Cooldown = Decay(u.Cooldown, dt);
                u.StaggerTimer = Decay(u.StaggerTimer, dt);
                u.RetargetTimer = Decay(u.RetargetTimer, dt);
                if (u.State == AiState.Windup) u.WindupTimer = Decay(u.WindupTimer, dt);
            }
            WaveTimer = Decay(WaveTimer, dt);
        }

        /// <summary>
        /// Step 3: every Windup that ends this tick resolves, ascending attacker UnitId.
        /// An attacker killed earlier this tick still hits (death is marked in step 4,
        /// and Lifesteal needs HP > 0). An attacker ring-outed in step 1 is not alive and does not hit.
        /// </summary>
        private void ResolveWindups()
        {
            for (int i = 0; i < _units.Count; i++)
            {
                SimUnit a = _units[i];
                if (!a.IsAlive || a.State != AiState.Windup || a.WindupTimer > 0f) continue;

                SimUnit target = GetUnit(a.LockedTargetId);
                if (target == null || !target.IsAlive)
                {
                    // Target died during the Windup: cancel, retarget next tick.
                    a.Cooldown = 0f;
                    a.TargetId = 0;
                }
                else if (SimVec2.Distance(a.Position, target.Position) <= a.Weapon.Range + _aiRules.WindupRangeSlack)
                {
                    HitResolver.Resolve(this, a, target, _hitRules);
                }
                // else: miss.

                a.LockedTargetId = 0;
                a.State = AiState.Chase;
            }
        }

        /// <summary>Step 4: HP &lt;= 0 -> dead (cause HP; last attacker was recorded by the hit).</summary>
        private void MarkDeaths()
        {
            for (int i = 0; i < _units.Count; i++)
            {
                SimUnit u = _units[i];
                if (!u.IsAlive || u.HP > 0) continue;
                u.IsAlive = false;
                u.DeathCause = DeathCause.HP;
                u.State = AiState.Dead;
            }
        }

        /// <summary>
        /// Step 5. Order: 1) Defeat (no living fighter, even if all enemies died too),
        /// 2) Cleared, 3) Timeout. On an outcome all units go Idle, velocity 0.
        /// </summary>
        private bool CheckOutcome()
        {
            bool fighterAlive = false;
            bool enemyAlive = false;
            for (int i = 0; i < _units.Count; i++)
            {
                SimUnit u = _units[i];
                if (!u.IsAlive) continue;
                if (u.Team == FighterTeam) fighterAlive = true;
                else enemyAlive = true;
            }

            CombatOutcome outcome = CombatOutcome.None;
            if (!fighterAlive) outcome = CombatOutcome.Defeat;
            else if (!enemyAlive) outcome = CombatOutcome.Cleared;
            else if (WaveTimer <= 0f) outcome = CombatOutcome.Timeout;

            if (outcome == CombatOutcome.None) return false;

            Outcome = outcome;
            Phase = CombatPhase.Ended;
            for (int i = 0; i < _units.Count; i++)
            {
                SimUnit u = _units[i];
                u.Velocity = SimVec2.Zero;
                u.State = u.IsAlive ? AiState.Idle : AiState.Dead;
            }
            return true;
        }

        /// <summary>
        /// Ring-out: distance(pos,(0,0)) > ArenaRadius -> dead (RingOut).
        /// Exactly ArenaRadius = alive. Unit radius is NOT used.
        /// </summary>
        public void CheckRingOut()
        {
            for (int i = 0; i < _units.Count; i++)
            {
                SimUnit u = _units[i];
                if (!u.IsAlive) continue;
                if (u.Position.Magnitude > _config.ArenaRadius)
                {
                    u.IsAlive = false;
                    u.DeathCause = DeathCause.RingOut;
                    u.State = AiState.Dead;
                }
            }
        }

        /// <summary>
        /// Custom physics step: drag, integration, then collision.
        /// vel *= 1/(1 + Drag*dt); pos += vel*dt. Dead units keep sliding
        /// (visual only) but take no part in collision.
        /// </summary>
        public void PhysicsStep()
        {
            float dt = _config.FixedDt;
            for (int i = 0; i < _units.Count; i++)
            {
                SimUnit u = _units[i];
                u.Velocity = u.Velocity * _dragFactor;
                SimVec2 step = u.Velocity * dt;
                u.Position = u.Position + step;
            }

            ResolveCollisions();
        }

        /// <summary>
        /// Circle bodies of all living units (both teams) are pushed apart,
        /// pairs resolved in UnitID order, correction split 50/50.
        /// </summary>
        private void ResolveCollisions()
        {
            for (int i = 0; i < _units.Count; i++)
            {
                SimUnit a = _units[i];
                if (!a.IsAlive) continue;

                for (int j = i + 1; j < _units.Count; j++)
                {
                    SimUnit b = _units[j];
                    if (!b.IsAlive) continue;

                    SimVec2 delta = b.Position - a.Position;
                    float dist = delta.Magnitude;
                    float overlap = a.BodyRadius + b.BodyRadius - dist;
                    if (overlap <= 0f) continue;

                    // Coincident centers: fixed axis so the result stays deterministic.
                    SimVec2 normal = dist < MinSeparationDistance
                        ? new SimVec2(1f, 0f)
                        : delta / dist;

                    SimVec2 push = normal * (overlap * 0.5f);
                    a.Position = a.Position - push;
                    b.Position = b.Position + push;
                }
            }
        }
    }
}
