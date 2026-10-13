using System;
using UnityEngine;

namespace GotA.Client
{
    /// <summary>Serializable mirror of GotA.Simulation.UnitStats (spec section 2).</summary>
    [Serializable]
    public sealed class UnitStatsData
    {
        [Min(1)] public int maxHP = 120;
        [Min(0f)] public float moveSpeed = 3.0f;
        public float dmgMult = 1.0f;
        public float atkSpdMult = 1.0f;
        public float kbMult = 1.0f;
        [Range(0f, 1f)] public float kbResist = 0.10f;
        [Min(0f)] public float bodyRadius = 0.40f;
    }

    /// <summary>Designer-editable enemy type (Grunt, Brute): stats + attack weapon.</summary>
    [CreateAssetMenu(fileName = "Enemy_", menuName = "GotA/Enemy Config")]
    public sealed class EnemyConfigSO : ScriptableObject
    {
        public string displayName = "Enemy";
        public UnitStatsData stats = new UnitStatsData { maxHP = 40, moveSpeed = 2.4f, kbResist = 0f };
        public WeaponConfigSO weapon;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
    }
}
