namespace GotA.Client
{
    /// <summary>
    /// Best Pot of the current play session (not persisted). Feed it the run's FinalPot:
    /// Extract / Clear W3 = full Pot, Timeout = floor(Pot x 0.5), Defeat = 0 (never a new best).
    /// Plain C#, no UnityEngine.
    /// </summary>
    public sealed class BestPotTracker
    {
        public int BestPot { get; private set; }

        /// <summary>Returns true if <paramref name="finalPot"/> is a new best.</summary>
        public bool Submit(int finalPot)
        {
            if (finalPot <= BestPot) return false;
            BestPot = finalPot;
            return true;
        }

        public void Reset() => BestPot = 0;
    }
}
