namespace GotA.Simulation
{
    /// <summary>
    /// Pure C# config DTO for arena + physics (spec section 1). The Unity side
    /// will later fill this from a ScriptableObject; the defaults below mirror
    /// the "pending test" values in spec.md so the simulation is usable and
    /// testable on its own. Logic code never contains these numbers.
    /// </summary>
    public sealed class ArenaConfig
    {
        /// <summary>Arena circle radius, centered at (0,0). No walls.</summary>
        public float ArenaRadius = 6.0f;

        /// <summary>Fixed simulation step in seconds.</summary>
        public float FixedDt = 0.02f;

        /// <summary>Linear drag. Each tick: vel *= 1 / (1 + Drag * FixedDt).</summary>
        public float Drag = 4.0f;

        public void Validate()
        {
            if (!(ArenaRadius > 0f))
                throw new System.ArgumentOutOfRangeException(nameof(ArenaRadius), "ArenaRadius must be > 0.");
            if (!(FixedDt > 0f))
                throw new System.ArgumentOutOfRangeException(nameof(FixedDt), "FixedDt must be > 0.");
            if (!(Drag >= 0f))
                throw new System.ArgumentOutOfRangeException(nameof(Drag), "Drag must be >= 0.");
        }
    }
}
