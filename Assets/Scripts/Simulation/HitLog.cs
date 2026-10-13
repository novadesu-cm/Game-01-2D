namespace GotA.Simulation
{
    /// <summary>How the target of a logged hit died inside its measurement window.</summary>
    public enum HitTargetDeath
    {
        None = 0,
        HP = 1,
        RingOut = 2
    }

    /// <summary>
    /// P-06 / D-08: one finished per-hit record. Measurement only - nothing here is read by the simulation.
    /// </summary>
    public sealed class HitLogEntry
    {
        /// <summary>1-based tick of the wave in which the hit resolved.</summary>
        public int WaveTick;

        /// <summary>1-based tick of the whole run (set by RunManager; equals WaveTick for a bare ArenaSim).</summary>
        public int RunTick;

        public int WaveNumber;
        public int AttackerId;
        public int TargetId;

        /// <summary>ActualDamage of the hit (min(FinalDamage, target HP)).</summary>
        public int Damage;

        /// <summary>D-08 knockback distance: sum of |KbVel| * dt over the window.</summary>
        public float KbDistance;

        public HitTargetDeath TargetDied;
    }

    /// <summary>
    /// Open D-08 measurement for one hit. KbVel is a private measurement vector:
    /// it never reads or writes any unit's Position or Velocity.
    /// </summary>
    internal sealed class KbRecord
    {
        public HitLogEntry Entry;
        public SimUnit Target;
        public SimVec2 KbVel;
        public int TicksDone;
    }
}
