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

        private readonly ArenaConfig _config;
        private readonly float _dragFactor;
        private readonly List<SimUnit> _units = new List<SimUnit>(); // always sorted by UnitId

        public ArenaSim(ArenaConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            config.Validate();
            _config = config;
            _dragFactor = 1f / (1f + config.Drag * config.FixedDt);
        }

        public ArenaConfig Config => _config;

        /// <summary>Number of completed Tick() calls.</summary>
        public int TickCount { get; private set; }

        /// <summary>Units in ascending UnitId order.</summary>
        public IReadOnlyList<SimUnit> Units => _units;

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
        /// One fixed step. P-01 subset of the tick order (spec section 5):
        /// 1) ring-out check, 7) physics step. Steps 2-6 are added by later tasks.
        /// </summary>
        public void Tick()
        {
            CheckRingOut();
            PhysicsStep();
            TickCount++;
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
                u.Position = u.Position + u.Velocity * dt;
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
