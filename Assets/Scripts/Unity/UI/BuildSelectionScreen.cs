using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GotA.Client.UI
{
    /// <summary>
    /// Build Selection: pick 1 Weapon and 1 Trait freely (no budget), shows Best Pot, Start button.
    /// Option buttons are created at runtime from the lists on GameSessionManager.
    /// </summary>
    public sealed class BuildSelectionScreen : SessionScreen
    {
        [SerializeField] private Transform weaponContainer;
        [SerializeField] private Transform traitContainer;
        [Tooltip("Any Button with a child Text; cloned once per weapon / trait.")]
        [SerializeField] private Button optionButtonPrefab;
        [SerializeField] private Button startButton;
        [SerializeField] private Text bestPotText;

        private readonly List<Text> _weaponLabels = new List<Text>();
        private readonly List<Text> _traitLabels = new List<Text>();
        private bool _built;

        protected override bool IsVisibleIn(SessionPhase phase) => phase == SessionPhase.BuildSelect;

        protected override void OnShown()
        {
            EnsureBuilt();
            Refresh();
        }

        private void EnsureBuilt()
        {
            if (_built) return;
            _built = true;

            if (startButton != null) startButton.onClick.AddListener(session.StartRun);

            var weapons = session.Weapons;
            for (int i = 0; weapons != null && i < weapons.Length; i++)
            {
                int index = i; // closure copy
                _weaponLabels.Add(CreateOption(weaponContainer, () =>
                {
                    session.SelectWeapon(index);
                    Refresh();
                }));
            }

            var traits = session.Traits;
            for (int i = 0; traits != null && i < traits.Length; i++)
            {
                int index = i;
                _traitLabels.Add(CreateOption(traitContainer, () =>
                {
                    session.SelectTrait(index);
                    Refresh();
                }));
            }
        }

        private Text CreateOption(Transform parent, UnityEngine.Events.UnityAction onClick)
        {
            Button button = Instantiate(optionButtonPrefab, parent);
            button.onClick.AddListener(onClick);
            return button.GetComponentInChildren<Text>();
        }

        private void Refresh()
        {
            if (bestPotText != null) bestPotText.text = "Best Pot: " + session.BestPot;

            var weapons = session.Weapons;
            for (int i = 0; i < _weaponLabels.Count; i++)
                SetLabel(_weaponLabels[i], weapons[i] != null ? weapons[i].Summary : "-", i == session.WeaponIndex);

            var traits = session.Traits;
            for (int i = 0; i < _traitLabels.Count; i++)
                SetLabel(_traitLabels[i], traits[i] != null ? traits[i].Summary : "-", i == session.TraitIndex);
        }

        private static void SetLabel(Text label, string text, bool selected)
        {
            if (label != null) label.text = (selected ? "[x] " : "[  ] ") + text;
        }

        private void OnDestroy()
        {
            if (startButton != null && session != null) startButton.onClick.RemoveListener(session.StartRun);
        }
    }
}
