using GotA.Simulation;
using UnityEngine;

namespace GotA.Client
{
    /// <summary>
    /// Designer-editable trait (spec section 2). <see cref="trait"/> picks the simulation rule,
    /// <see cref="magnitude"/> is its tuning value:
    /// Lifesteal = heal ratio (0.25), ThickSkin = damage-taken multiplier (0.75), None = unused.
    /// DtoConverter writes the magnitude into the simulation's HitRules.
    /// </summary>
    [CreateAssetMenu(fileName = "Trait_", menuName = "GotA/Trait Config")]
    public sealed class TraitConfigSO : ScriptableObject
    {
        public string displayName = "Trait";
        public TraitType trait = TraitType.None;
        public float magnitude;

        [TextArea] public string description;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;

        public string Summary =>
            string.IsNullOrEmpty(description) ? DisplayName : DisplayName + "  (" + description + ")";
    }
}
