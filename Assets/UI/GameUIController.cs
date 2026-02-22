using Networking;
using Networking.Snapshots;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// Controls the game-over panel: shows winner text, rematch / close buttons.
    ///
    /// Subscribes to GameEvents — no external code calls into this class.
    ///   - OnGameOver:      show the game-over panel with winner info.
    ///   - OnLobbyUpdated:  when lobby returns to ReadyUp (rematch), hide panel & reset state.
    ///
    /// The host sees Rematch / Close Lobby buttons; clients see neither.
    /// </summary>
    public class GameUIController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private TMP_Text winnerText;
        [SerializeField] private Button rematchButton;
        [SerializeField] private Button closeLobbyButton;
        [SerializeField] private string mainMenuSceneName = "MainMenuScene";

        #region Unity Lifecycle

        private void Awake()
        {
            if (gameOverPanel != null)
                gameOverPanel.SetActive(false);
        }

        private void OnEnable()
        {
            GameEvents.OnGameOver += HandleGameOver;
            GameEvents.OnLobbyUpdated += HandleLobbyUpdated;
        }

        private void OnDisable()
        {
            GameEvents.OnGameOver -= HandleGameOver;
            GameEvents.OnLobbyUpdated -= HandleLobbyUpdated;
        }

        #endregion

        #region Event Handlers

        private void HandleGameOver(int winnerPlayerId, PlayerProfileData winnerProfile)
        {
            if (gameOverPanel == null) return;

            gameOverPanel.SetActive(true);

            var localWon = winnerPlayerId == LocalGameState.LocalPlayerId;

            if (winnerText != null)
                winnerText.text = localWon
                    ? "<b>You won!</b>"
                    : $"<b>{winnerProfile.displayName} wins!</b>";

            // Host sees both buttons; clients see none.
            var isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

            if (rematchButton != null)
                rematchButton.gameObject.SetActive(isHost);

            if (closeLobbyButton != null)
                closeLobbyButton.gameObject.SetActive(isHost);
        }

        /// <summary>
        /// When the lobby phase returns to ReadyUp (host clicked Rematch),
        /// hide the game-over panel and reset client-side game state.
        /// </summary>
        private void HandleLobbyUpdated(LobbyStateSnapshot snapshot)
        {
            var phase = (NetworkLobbyManager.LobbyPhase)snapshot.phase;

            if (phase != NetworkLobbyManager.LobbyPhase.ReadyUp) return;

            // Lobby is back in ReadyUp — clear the game-over panel.
            if (gameOverPanel != null && gameOverPanel.activeSelf)
            {
                gameOverPanel.SetActive(false);
                LocalGameState.Reset();
            }
        }

        #endregion

        #region Button Handlers (wired in Inspector)

        /// <summary>
        /// Host clicks Rematch. Resets lobby to ReadyUp phase, which
        /// triggers a LobbyUpdated snapshot → HandleLobbyUpdated hides this panel.
        /// </summary>
        public void OnClickRematch()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

            var lobby = FindFirstObjectByType<NetworkLobbyManager>();
            lobby?.ServerResetLobby();
        }

        /// <summary>
        /// Host clicks Close Lobby. Shuts down networking and returns to main menu.
        /// </summary>
        public void OnClickCloseLobby()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

            NetworkManager.Singleton.Shutdown();

            if (!string.IsNullOrEmpty(mainMenuSceneName))
                UnityEngine.SceneManagement.SceneManager.LoadScene(mainMenuSceneName);
        }

        #endregion
    }
}
