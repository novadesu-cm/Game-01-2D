namespace GotA.Simulation
{
    public enum DeathCause
    {
        None = 0,
        RingOut = 1,
        HP = 2
    }

    /// <summary>
    /// Minimal simulation body for P-01 (position, velocity, collision radius,
    /// alive state). HP, AI and combat state are added by later tasks.
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
