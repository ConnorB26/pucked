using System.Collections;
using Cards;
using Networking;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// Main in-game HUD. Shows turn indicator, notifications for card plays,
    /// eliminations, and peek results.
    ///
    /// Subscribes to GameEvents — no direct references to network managers needed.
    ///
    /// Lives on a root GameObject that is ALWAYS active (so it can receive events).
    /// Toggles the hudPanel child on/off for visibility.
    /// </summary>
    public class GameHUDController : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject hudPanel;

        [Header("Text")]
        [SerializeField] private TMP_Text turnIndicatorText;
        [SerializeField] private TMP_Text notificationText;

        [Header("Buttons")]
        [SerializeField] private Button endTurnButton;

        private Coroutine _notificationCoroutine;
        private NetworkGameManager _networkGameManager;
        private bool _wasMyTurn;

        #region Unity Lifecycle

        private void Awake()
        {
            if (hudPanel != null)
                hudPanel.SetActive(false);

            if (notificationText != null)
                notificationText.text = "";

            if (endTurnButton != null)
            {
                endTurnButton.onClick.AddListener(OnClickEndTurn);
                endTurnButton.interactable = false;
            }
        }

        private void OnEnable()
        {
            GameEvents.OnGameStarted += ShowHUD;
            GameEvents.OnGameEnded += HideHUD;
            GameEvents.OnTurnChanged += HandleTurnChanged;
            GameEvents.OnPlayerEliminated += HandlePlayerEliminated;
            GameEvents.OnPeekResult += HandlePeekResult;
            GameEvents.OnCardPlayed += HandleCardPlayed;
            GameEvents.OnGoalieSaveUsed += HandleGoalieSaveUsed;
        }

        private void OnDisable()
        {
            GameEvents.OnGameStarted -= ShowHUD;
            GameEvents.OnGameEnded -= HideHUD;
            GameEvents.OnTurnChanged -= HandleTurnChanged;
            GameEvents.OnPlayerEliminated -= HandlePlayerEliminated;
            GameEvents.OnPeekResult -= HandlePeekResult;
            GameEvents.OnCardPlayed -= HandleCardPlayed;
            GameEvents.OnGoalieSaveUsed -= HandleGoalieSaveUsed;
        }

        #endregion

        #region Show / Hide

        private void ShowHUD()
        {
            if (hudPanel != null)
                hudPanel.SetActive(true);

            // Catch up with state that arrived before the panel was activated.
            if (LocalGameState.CurrentTurnPlayerId >= 0)
                HandleTurnChanged(LocalGameState.CurrentTurnPlayerId);

            if (notificationText != null)
                notificationText.text = "";
        }

        private void HideHUD()
        {
            if (hudPanel != null)
                hudPanel.SetActive(false);

            if (endTurnButton != null)
                endTurnButton.interactable = false;
        }

        #endregion

        #region Turn Indicator

        private void HandleTurnChanged(int currentPlayerId)
        {
            var isMyTurn = currentPlayerId == LocalGameState.LocalPlayerId;

            if (turnIndicatorText != null)
            {
                if (isMyTurn)
                {
                    turnIndicatorText.text = "YOUR TURN";
                    turnIndicatorText.color = new Color(0.2f, 0.9f, 0.3f);
                }
                else
                {
                    var name = LocalGameState.GetPlayerName(currentPlayerId);
                    turnIndicatorText.text = $"Waiting for {name}...";
                    turnIndicatorText.color = Color.white;
                }
            }

            if (endTurnButton != null)
                endTurnButton.interactable = isMyTurn;

            // Show feedback when your turn ends so it's clear End Turn registered.
            if (_wasMyTurn && !isMyTurn)
                ShowNotification("Turn ended.", 1.5f);

            _wasMyTurn = isMyTurn;
        }

        #endregion

        #region Notifications

        private void HandlePlayerEliminated(int playerId)
        {
            var msg = playerId == LocalGameState.LocalPlayerId
                ? "You have been eliminated!"
                : $"{LocalGameState.GetPlayerName(playerId)} has been eliminated!";
            ShowNotification(msg, 3f);
        }

        private void HandlePeekResult(string[] cardNames)
        {
            if (cardNames == null || cardNames.Length == 0)
            {
                ShowNotification("Deck is empty!", 3f);
                return;
            }

            ShowNotification($"Top of deck: {string.Join(", ", cardNames)}", 5f);
        }

        private void HandleCardPlayed(int playerId, string cardName, CardCategory category)
        {
            // Don't notify about your own plays — you already know.
            if (playerId == LocalGameState.LocalPlayerId) return;

            var name = LocalGameState.GetPlayerName(playerId);
            ShowNotification($"{name} played {cardName}!", 2.5f);
        }

        private void HandleGoalieSaveUsed(int playerId)
        {
            var msg = playerId == LocalGameState.LocalPlayerId
                ? "Your Goalie Save blocked the Puck'd card!"
                : $"{LocalGameState.GetPlayerName(playerId)}'s Goalie Save blocked a Puck'd card!";
            ShowNotification(msg, 3f);
        }

        private void ShowNotification(string message, float duration)
        {
            if (notificationText == null) return;

            if (_notificationCoroutine != null)
                StopCoroutine(_notificationCoroutine);

            _notificationCoroutine = StartCoroutine(NotificationCoroutine(message, duration));
        }

        private IEnumerator NotificationCoroutine(string message, float duration)
        {
            notificationText.text = message;
            yield return new WaitForSeconds(duration);
            notificationText.text = "";
            _notificationCoroutine = null;
        }

        #endregion

        #region End Turn

        private void OnClickEndTurn()
        {
            if (!LocalGameState.IsMyTurn) return;

            // Lazy-find so we don't rely on Awake() timing relative to network spawn.
            if (_networkGameManager == null)
                _networkGameManager = FindFirstObjectByType<NetworkGameManager>();

            if (_networkGameManager == null)
            {
                Debug.LogWarning("[GameHUDController] No NetworkGameManager found for End Turn.");
                return;
            }

            _networkGameManager.RequestEndTurn();
        }

        #endregion
    }
}
