namespace GotA.Simulation
{
    public enum DeathCause
    {
        None = 0,
        RingOut = 1,
        HP = 2
    }

    public enum TraitType
    {
        None = 0,
        Lifesteal = 1,
        ThickSkin = 2
    }

    /// <summary>AI states (spec section 4). No Retreat in the prototype.</summary>
    public enum AiState
    {
        Idle = 0,
        Frozen = 1,
        Chase = 2,
        Ready = 3,
        Windup = 4,
        Dead = 5
    }

    /// <summary>
    /// Simulation body (position, velocity, collision radius, alive state) plus
    /// the combat and AI data used by Hit Resolution (P-02) and the AI/tick (P-03).
    /// </summary>
    public sealed class SimUnit
    {
        public int UnitId { get; }
        public int Team { get; }
        public float BodyRadius { get; }

        public SimVec2 Position;
        public SimVec2 Velocity;

        public bool IsAlive { get; internal set; }
        public DeathCause DeathCause { get; internal set; }

        // --- P-02 combat data (filled from config DTOs by the caller) ---
        public int MaxHP;
        public int HP;
        public float DmgMult = 1f;
        public float KBMult = 1f;
        public float KBResist;
        public TraitType Trait = TraitType.None;
        public WeaponConfig Weapon;

        public float StaggerTimer;

        /// <summary>+1 = facing +x, -1 = facing -x.</summary>
        public int Facing = 1;

        /// <summary>UnitId of the last attacker that hit this unit (0 = none).</summary>
        public int LastAttackerId;

        // --- P-06 telemetry (read-only for gameplay; never influences the simulation) ---

        /// <summary>Hits this unit has received (HitResolver increments it).</summary>
        public int HitsTaken { get; internal set; }

        /// <summary>
        /// Distance travelled while knocked back (Stagger active, faster than MoveSpeed, or dead and sliding).
        /// Accumulated by ArenaSim.PhysicsStep from the velocity step, so it is the real displacement
        /// before collision correction.
        /// </summary>
        public float KnockbackDistance { get; internal set; }

        // --- P-03 AI data ---
        public float MoveSpeed;
        public float AtkSpdMult = 1f;
        public AiState State = AiState.Idle;
        public float Cooldown;
        public float WindupTimer;
        public float RetargetTimer;

        /// <summary>Current chase target (0 = none).</summary>
        public int TargetId;

        /// <summary>Target locked when the current Windup started (0 = none).</summary>
        public int LockedTargetId;

        public SimUnit(int unitId, int team, SimVec2 position, float bodyRadius)
        {
            UnitId = unitId;
            Team = team;
            Position = position;
            Velocity = SimVec2.Zero;
            BodyRadius = bodyRadius;
            IsAlive = true;
            DeathCause = DeathCause.None;
        }

        /// <summary>EffInterval = AttackInterval / AtkSpdMult.</summary>
        public float EffInterval => Weapon.AttackInterval / AtkSpdMult;

        /// <summary>EffWindup = Windup / AtkSpdMult.</summary>
        public float EffWindup => Weapon.Windup / AtkSpdMult;
    }
}
