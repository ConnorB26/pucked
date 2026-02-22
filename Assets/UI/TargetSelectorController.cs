using System;
using System.Collections.Generic;
using System.Linq;
using Networking;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// Shows a panel listing every alive opponent so the local player can pick a target
    /// before a card requiring targeting (e.g. Attack) is confirmed.
    ///
    /// Setup in the Inspector:
    ///   - panel         : the root GameObject of this selector (starts hidden)
    ///   - buttonContainer : a VerticalLayoutGroup (or similar) that holds spawned buttons
    ///   - playerButtonPrefab : a Button prefab that has a TMP_Text child for the player name
    ///   - cancelButton  : a plain Button wired to Hide()
    ///
    /// CardHandController calls Show(...) when a targeting card is clicked and Hide()
    /// if the turn changes before a target is chosen.
    /// </summary>
    public class TargetSelectorController : MonoBehaviour
    {
        [Header("References")] [SerializeField]
        private GameObject panel;

        [SerializeField] private Transform buttonContainer;
        [SerializeField] private Button playerButtonPrefab;
        [SerializeField] private Button cancelButton;

        private Action<int> _onTargetSelected;
        private readonly List<Button> _spawnedButtons = new();

        #region Unity Lifecycle

        private void Awake()
        {
            if (panel != null)
                panel.SetActive(false);

            if (cancelButton != null)
                cancelButton.onClick.AddListener(Hide);
        }

        private void OnEnable()
        {
            GameEvents.OnTurnChanged += HandleTurnChanged;
        }

        private void OnDisable()
        {
            GameEvents.OnTurnChanged -= HandleTurnChanged;
        }

        #endregion

        /// <summary>
        /// Opens the target panel and populates it with buttons for each alive opponent.
        /// onTargetSelected is invoked once the player picks a target; the panel then hides.
        /// </summary>
        public void Show(Action<int> onTargetSelected)
        {
            _onTargetSelected = onTargetSelected;

            ClearButtons();

            foreach (var (playerId, info) in LocalGameState.Players)
            {
                if (playerId == LocalGameState.LocalPlayerId) continue;
                if (info.IsEliminated) continue;

                var btn = Instantiate(playerButtonPrefab, buttonContainer);

                var label = btn.GetComponentInChildren<TMP_Text>();
                if (label != null)
                {
                    label.text = info.DisplayName;
                    label.color = info.Color;
                }

                var capturedId = playerId;
                btn.onClick.AddListener(() => SelectTarget(capturedId));
                _spawnedButtons.Add(btn);
            }

            if (panel != null)
                panel.SetActive(true);
        }

        public void Hide()
        {
            if (panel != null)
                panel.SetActive(false);

            _onTargetSelected = null;
        }

        private void SelectTarget(int targetPlayerId)
        {
            var callback = _onTargetSelected;
            Hide();
            callback?.Invoke(targetPlayerId);
        }

        private void ClearButtons()
        {
            foreach (var btn in _spawnedButtons.Where(btn => btn != null)) Destroy(btn.gameObject);
            _spawnedButtons.Clear();
        }

        // If the turn changes while the panel is open (e.g. host disconnects), close it.
        private void HandleTurnChanged(int currentPlayerId)
        {
            if (panel != null && panel.activeSelf && currentPlayerId != LocalGameState.LocalPlayerId)
                Hide();
        }
    }
}