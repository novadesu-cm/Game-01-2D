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

    /// <summary>
    /// Simulation body (position, velocity, collision radius, alive state) plus
    /// the combat data needed by Hit Resolution (P-02). AI and combat state
    /// machines are added by later tasks.
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
    }
}
