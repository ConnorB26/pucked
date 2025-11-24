using Networking;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class GameUIController : MonoBehaviour
    {
        public static GameUIController Instance;

        [Header("References")] [SerializeField]
        private GameObject gameOverPanel;

        [SerializeField] private TMP_Text winnerText;
        [SerializeField] private Button rematchButton;
        [SerializeField] private Button closeLobbyButton;
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        private void Awake()
        {
            Instance = this;
            gameOverPanel.SetActive(false);
        }

        public void ShowGameOver(bool localPlayerWon, PlayerProfileData winnerProfile, bool isHost)
        {
            gameOverPanel.SetActive(true);

            winnerText.text = localPlayerWon ? "<b>You won!</b>" : $"<b>Player {winnerProfile.displayName} wins!</b>";

            // Host sees both buttons; clients see none.
            rematchButton.gameObject.SetActive(isHost);
            closeLobbyButton.gameObject.SetActive(isHost);
        }

        public void OnClickRematch()
        {
            if (!NetworkManager.Singleton.IsServer) return;

            // Calls lobby reset (clears ready states & returns to lobby)
            var lobby = FindFirstObjectByType<NetworkLobbyManager>();
            lobby?.ServerResetLobby();

            gameOverPanel.SetActive(false);
        }

        public void OnClickCloseLobby()
        {
            if (!NetworkManager.Singleton.IsServer) return;

            NetworkManager.Singleton.Shutdown();

            if (!string.IsNullOrEmpty(mainMenuSceneName))
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(mainMenuSceneName);
            }
        }
    }
}