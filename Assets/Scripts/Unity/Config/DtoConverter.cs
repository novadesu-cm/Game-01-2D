using System;
using GotA.Simulation;
using UnityEngine;

namespace GotA.Client
{
    /// <summary>
    /// One-way bridge: ScriptableObjects (Unity) -> pure C# DTOs (GotA.Simulation).
    /// The simulation never sees UnityEngine types; this is the only place that converts.
    /// </summary>
    public static class DtoConverter
    {
        public static SimVec2 ToSim(Vector2 v) => new SimVec2(v.x, v.y);

        public static WeaponConfig ToDto(WeaponConfigSO so)
        {
            if (so == null) throw new ArgumentNullException(nameof(so));
            return new WeaponConfig
            {
                BaseDamage = so.baseDamage,
                AttackInterval = so.attackInterval,
                Windup = so.windup,
                BaseKnockback = so.baseKnockback,
                Range = so.range
            };
        }

        public static UnitStats ToDto(UnitStatsData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            return new UnitStats
            {
                MaxHP = data.maxHP,
                MoveSpeed = data.moveSpeed,
                DmgMult = data.dmgMult,
                AtkSpdMult = data.atkSpdMult,
                KBMult = data.kbMult,
                KBResist = data.kbResist,
                BodyRadius = data.bodyRadius
            };
        }

        /// <summary>A null trait means "no trait".</summary>
        public static TraitType ToDto(TraitConfigSO so) => so == null ? TraitType.None : so.trait;

        /// <summary>Writes the trait's tuning value into the matching HitRules field.</summary>
        public static void ApplyTrait(HitRules rules, TraitConfigSO so)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            if (so == null) return;

            switch (so.trait)
            {
                case TraitType.Lifesteal: rules.LifestealRatio = so.magnitude; break;
                case TraitType.ThickSkin: rules.ThickSkinDamageMult = so.magnitude; break;
            }
        }

        /// <summary>
        /// Builds the full RunConfig DTO for one run. Enemy spawn points get a seeded offset of
        /// +/- RunSetupSO.spawnJitter per axis (D-03); same seed gives the same run.
        /// </summary>
        public static RunConfig BuildRunConfig(RunSetupSO setup, WeaponConfigSO weapon, TraitConfigSO trait, int seed)
        {
            if (setup == null) throw new ArgumentNullException(nameof(setup));
            if (weapon == null) throw new ArgumentNullException(nameof(weapon));

            var config = new RunConfig
            {
                WaveTimeLimitSeconds = setup.waveTimeLimitSeconds,
                TimeoutPotMultiplier = setup.timeoutPotMultiplier,
                Arena = new ArenaConfig
                {
                    ArenaRadius = setup.arenaRadius,
                    FixedDt = setup.fixedDt,
                    Drag = setup.drag
                },
                Fighter = new FighterConfig
                {
                    Stats = ToDto(setup.fighterStats),
                    Weapon = ToDto(weapon),
                    Trait = ToDto(trait),
                    SpawnPosition = ToSim(setup.fighterSpawn)
                }
            };
            ApplyTrait(config.HitRules, trait);

            var rng = new System.Random(seed);
            for (int w = 0; w < setup.waves.Count; w++)
            {
                WaveSetup waveSetup = setup.waves[w];
                var wave = new WaveConfig { PotReward = waveSetup.potReward };

                for (int e = 0; e < waveSetup.enemies.Count; e++)
                {
                    EnemySpawnSetup spawn = waveSetup.enemies[e];
                    if (spawn == null || spawn.enemy == null || spawn.enemy.weapon == null)
                        throw new InvalidOperationException(
                            "RunSetup wave " + (w + 1) + " enemy " + e + " needs an EnemyConfigSO with a weapon.");

                    float jx = (float)(rng.NextDouble() * 2.0 - 1.0) * setup.spawnJitter;
                    float jy = (float)(rng.NextDouble() * 2.0 - 1.0) * setup.spawnJitter;

                    wave.Enemies.Add(new EnemySpawnConfig
                    {
                        Stats = ToDto(spawn.enemy.stats),
                        Weapon = ToDto(spawn.enemy.weapon),
                        Position = new SimVec2(spawn.position.x + jx, spawn.position.y + jy)
                    });
                }
                config.Waves.Add(wave);
            }

            config.Validate();
            return config;
        }
    }
}
