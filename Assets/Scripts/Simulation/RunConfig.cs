using System;
using System.Collections.Generic;

namespace GotA.Simulation
{
    /// <summary>One enemy in a wave: stats + weapon + spawn point (spec sections 2 and 6).</summary>
    public sealed class EnemySpawnConfig
    {
        public UnitStats Stats;
        public WeaponConfig Weapon;
        public SimVec2 Position;
    }

    /// <summary>
    /// Pure C# DTO for one wave: enemy composition (listed order = UnitID order, starting at
    /// RunConfig.FirstEnemyUnitId) and the Pot reward added when the wave is cleared.
    /// A ScriptableObject can fill this later.
    /// </summary>
    public sealed class WaveConfig
    {
        public List<EnemySpawnConfig> Enemies = new List<EnemySpawnConfig>();
        public int PotReward;
    }

    /// <summary>The player's Gladiator for the run (stats, weapon, trait, spawn point).</summary>
    public sealed class FighterConfig
    {
        public UnitStats Stats;
        public WeaponConfig Weapon;
        public TraitType Trait = TraitType.None;
        public SimVec2 SpawnPosition;
    }

    /// <summary>
    /// Pure C# DTO for a whole run (spec sections 0, 5, 6). Contains no logic besides validation.
    /// </summary>
    public sealed class RunConfig
    {
        public FighterConfig Fighter;

        /// <summary>Waves in play order. A run has at most 3 (prototype), but the logic only uses the list.</summary>
        public List<WaveConfig> Waves = new List<WaveConfig>();

        /// <summary>Timeout limit per wave in seconds (60 s = 3000 ticks at 0.02 s).</summary>
        public float WaveTimeLimitSeconds = 60f;

        /// <summary>On Timeout the final Pot is floor(Pot * this).</summary>
        public float TimeoutPotMultiplier = 0.5f;

        public int FighterUnitId = 1;
        public int FirstEnemyUnitId = 101;

        public ArenaConfig Arena = new ArenaConfig();
        public HitRules HitRules = new HitRules();
        public AiRules AiRules = new AiRules();

        public void Validate()
        {
            if (Fighter == null || Fighter.Stats == null || Fighter.Weapon == null)
                throw new InvalidOperationException("RunConfig.Fighter needs Stats and Weapon.");
            if (Waves == null || Waves.Count == 0)
                throw new InvalidOperationException("RunConfig needs at least one wave.");
            if (!(WaveTimeLimitSeconds > 0f))
                throw new ArgumentOutOfRangeException(nameof(WaveTimeLimitSeconds));
            if (TimeoutPotMultiplier < 0f)
                throw new ArgumentOutOfRangeException(nameof(TimeoutPotMultiplier));
            if (Arena == null || HitRules == null || AiRules == null)
                throw new InvalidOperationException("Arena, HitRules and AiRules must not be null.");
            Arena.Validate();

            for (int w = 0; w < Waves.Count; w++)
            {
                WaveConfig wave = Waves[w];
                if (wave == null || wave.Enemies == null || wave.Enemies.Count == 0)
                    throw new InvalidOperationException("Wave " + (w + 1) + " needs at least one enemy.");
                if (wave.PotReward < 0)
                    throw new InvalidOperationException("Wave " + (w + 1) + " PotReward must be >= 0.");
                for (int e = 0; e < wave.Enemies.Count; e++)
                {
                    EnemySpawnConfig enemy = wave.Enemies[e];
                    if (enemy == null || enemy.Stats == null || enemy.Weapon == null)
                        throw new InvalidOperationException("Wave " + (w + 1) + " enemy " + e + " needs Stats and Weapon.");
                }
            }
        }
    }
}
