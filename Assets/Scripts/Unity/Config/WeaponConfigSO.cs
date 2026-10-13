using UnityEngine;

namespace GotA.Client
{
    /// <summary>
    /// Designer-editable weapon (spec section 2). Values are "pending test".
    /// Used for the player's weapons and for enemy attacks. Converted to the pure C#
    /// GotA.Simulation.WeaponConfig by DtoConverter.
    /// </summary>
    [CreateAssetMenu(fileName = "Weapon_", menuName = "GotA/Weapon Config")]
    public sealed class WeaponConfigSO : ScriptableObject
    {
        public string displayName = "Weapon";

        [Min(0f)] public float baseDamage = 8f;
        [Min(0.01f)] public float attackInterval = 1.0f;
        [Min(0f)] public float windup = 0.2f;
        [Min(0f)] public float baseKnockback = 3.0f;
        [Min(0f)] public float range = 1.1f;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;

        /// <summary>One-line stat summary for the Build Selection UI.</summary>
        public string Summary =>
            DisplayName + "  (Dmg " + baseDamage + ", " + attackInterval.ToString("0.0#") +
            "s, KB " + baseKnockback + ", Range " + range + ")";
    }
}
