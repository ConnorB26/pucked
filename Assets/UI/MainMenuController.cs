using Networking;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UI
{
    public class MainMenuController : MonoBehaviour
    {
        [Header("Relay")] [SerializeField] private int maxConnections = 4;

        [Header("Scenes")] [SerializeField] private string lobbySceneName = "LobbyScene";

        [Header("UI")] [SerializeField] private TMP_InputField nameInput;
        [SerializeField] private Image colorPreview;
        [SerializeField] private TMP_InputField joinCodeInput;

        private PlayerProfileData _localProfile;

        private void Start()
        {
            // Load saved profile and populate UI
            _localProfile = LocalPlayerProfile.LoadOrDefault();

            if (nameInput != null)
                nameInput.text = _localProfile.displayName;

            if (colorPreview != null)
                colorPreview.color = _localProfile.color;
        }

        public void OnNameChanged(string newName)
        {
            _localProfile.displayName = newName;
        }

        public void OnColorChanged(Color newColor)
        {
            _localProfile.color = newColor;
            if (colorPreview != null)
                colorPreview.color = newColor;
        }

        public async void OnClickHost()
        {
            SaveProfile();

            var code = await RelayBootstrap.StartHostWithRelay(maxConnections);
            if (string.IsNullOrEmpty(code))
            {
                Debug.LogError("Failed to host.");
                return;
            }

            Debug.Log($"Host join code: {code}");

            // Once host networking is up, move everyone (host+clients) into the lobby scene.
            NetworkManager.Singleton.SceneManager.LoadScene(
                lobbySceneName,
                LoadSceneMode.Single);
        }

        public async void OnClickJoin()
        {
            SaveProfile();

            var code = joinCodeInput != null ? joinCodeInput.text.Trim() : "";
            if (string.IsNullOrEmpty(code))
                return;

            var ok = await RelayBootstrap.StartClientWithRelay(code);
            if (!ok)
            {
                Debug.LogError("Failed to join.");
            }

            // Client will automatically follow the host into whatever scene the host is in,
            // thanks to NetworkSceneManager. No manual scene change here.
        }

        private void SaveProfile()
        {
            LocalPlayerProfile.Save(_localProfile);
        }
    }
}