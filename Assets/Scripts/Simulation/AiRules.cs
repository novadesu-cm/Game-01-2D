namespace GotA.Simulation
{
    /// <summary>Pure C# stat DTO for one unit type (spec section 2). Filled from data, never hard-coded in logic.</summary>
    public sealed class UnitStats
    {
        public int MaxHP;
        public float MoveSpeed;
        public float DmgMult = 1f;
        public float AtkSpdMult = 1f;
        public float KBMult = 1f;
        public float KBResist;
        public float BodyRadius;
    }

    /// <summary>AI and combat-flow tuning values (spec section 4/5, pending test).</summary>
    public sealed class AiRules
    {
        /// <summary>Seconds between retarget checks.</summary>
        public float RetargetInterval = 0.5f;

        /// <summary>A new target replaces the current one only if it is at least this much closer.</summary>
        public float RetargetSwitchMargin = 0.5f;

        /// <summary>Distances within this are a tie (lower HP, then lower UnitID wins).</summary>
        public float TieDistance = 0.1f;

        /// <summary>A Windup still hits if the target is within Range + this slack when it ends.</summary>
        public float WindupRangeSlack = 0.3f;
    }

    /// <summary>Why/how a wave stopped (spec section 5 outcome order).</summary>
    public enum CombatOutcome
    {
        None = 0,
        Defeat = 1,
        Cleared = 2,
        Timeout = 3
    }

    /// <summary>
    /// Sandbox: legacy P-01 stepping (ring-out + physics only, no combat).
    /// Active: full spec section 5 tick order. Ended: an outcome was reached; Tick does nothing.
    /// </summary>
    public enum CombatPhase
    {
        Sandbox = 0,
        Active = 1,
        Ended = 2
    }
}
