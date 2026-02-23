using Networking;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UI
{
    /// <summary>Manages the main menu: player profile editing, and host/join flow via RelayBootstrap.</summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Relay")] [SerializeField] private int maxConnections = 4;

        [Header("Scenes")] [SerializeField] private string lobbySceneName = "LobbyScene";

        [Header("Profile UI")] [SerializeField]
        private TMP_InputField nameInput;

        [SerializeField] private Image colorPreview;
        [SerializeField] private ColorPicker colorPicker;

        [Header("Join UI")] [SerializeField] private TMP_InputField joinCodeInput;

        [Header("Panels")] [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject editProfilePanel;

        private PlayerProfileData _localProfile;

        private void Start()
        {
            ShowMainMenu();
            ResetProfileEditor();

            if (nameInput != null)
                nameInput.text = _localProfile.displayName;

            if (colorPicker != null)
            {
                colorPicker.SetColor(_localProfile.color, notify: false);
                colorPicker.OnColorChanged.AddListener(OnColorChanged);
            }

            if (colorPreview != null)
                colorPreview.color = _localProfile.color;
        }

        #region Panel Navigation

        public void ShowMainMenu()
        {
            if (mainMenuPanel != null)
                mainMenuPanel.SetActive(true);
            if (editProfilePanel != null)
                editProfilePanel.SetActive(false);
        }

        public void ShowEditProfile()
        {
            ResetProfileEditor();
            
            if (mainMenuPanel != null)
                mainMenuPanel.SetActive(false);
            if (editProfilePanel != null)
                editProfilePanel.SetActive(true);
        }

        #endregion

        #region Profile Editing

        public void OnNameChanged(string newName)
        {
            _localProfile.displayName = newName;
        }

        /// <summary>
        /// A central place to update the profile color and preview.
        /// You can call this from sliders, a randomize button, or any custom color picker.
        /// </summary>
        public void OnColorChanged(Color newColor)
        {
            _localProfile.color = newColor;

            if (colorPreview != null)
                colorPreview.color = newColor;
        }

        /// <summary>
        /// Optional: hook a "Random Color" button to this.
        /// </summary>
        public void OnRandomColorClicked()
        {
            var randomColor = new Color(
                Random.value,
                Random.value,
                Random.value
            );

            OnColorChanged(randomColor);
        }

        public void SaveProfile()
        {
            LocalPlayerProfile.Save(_localProfile);
        }
        
        private void ResetProfileEditor()
        {
            var saved = LocalPlayerProfile.LoadOrDefault();
            _localProfile = saved;

            if (nameInput != null)
                nameInput.text = saved.displayName;

            if (colorPicker != null)
                colorPicker.SetColor(saved.color, notify: false);

            if (colorPreview != null)
                colorPreview.color = saved.color;
        }

        #endregion

        #region Hosting / Joining

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

            // NetworkManager scene load moves all connected clients (including host) into the lobby scene.
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

        }

        #endregion
    }
}