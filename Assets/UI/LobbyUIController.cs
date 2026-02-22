using System.Collections.Generic;
using System.Linq;
using Networking;
using Networking.Snapshots;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// Controls the Lobby UI: join code, status, player rows, ready/start buttons.
    /// Subscribes to GameEvents.OnLobbyUpdated (no direct event coupling to NetworkLobbyManager).
    /// Still holds a reference to NetworkLobbyManager for calling command methods
    /// (SetLocalReady, HostRequestStartGame).
    ///
    /// Automatically hides the lobby panel when the game starts (phase = InGame)
    /// and shows it again when the lobby resets to ReadyUp.
    /// </summary>
    public class LobbyUIController : MonoBehaviour
    {
        #region Serialized references

        [Header("References")]
        [SerializeField] private NetworkLobbyManager lobbyManager;

        [Header("Panels")]
        [Tooltip("Root panel for all lobby UI. Hidden during gameplay.")]
        [SerializeField] private GameObject lobbyRootPanel;

        [Header("Top Info")]
        [SerializeField] private TMP_Text joinCodeText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text playerCountText;

        [Header("Player List")]
        [SerializeField] private Transform playerListParent;
        [SerializeField] private LobbyPlayerRowUI playerRowPrefab;

        [Header("Buttons")]
        [SerializeField] private Button readyButton;
        [SerializeField] private TMP_Text readyButtonLabel;
        [SerializeField] private Button startButton;

        #endregion

        private readonly Dictionary<ulong, LobbyPlayerRowUI> _rows = new();

        private bool _localIsReady;
        private ulong _localClientId;

        #region Unity lifecycle

        private void Awake()
        {
            if (lobbyManager == null)
                lobbyManager = FindFirstObjectByType<NetworkLobbyManager>();

            _localClientId = NetworkManager.Singleton != null
                ? NetworkManager.Singleton.LocalClientId
                : 0;

            if (readyButton != null)
                readyButton.onClick.AddListener(OnClickReady);

            if (startButton != null)
                startButton.onClick.AddListener(OnClickStartGame);

            if (statusText != null)
                statusText.text = "Status: Connecting...";

            if (!string.IsNullOrEmpty(RelayBootstrap.LastJoinCode))
                SetJoinCode(RelayBootstrap.LastJoinCode);
        }

        private void OnEnable()
        {
            GameEvents.OnLobbyUpdated += HandleLobbySnapshot;
        }

        private void OnDisable()
        {
            GameEvents.OnLobbyUpdated -= HandleLobbySnapshot;
        }

        #endregion

        #region Public API

        public void SetJoinCode(string code)
        {
            if (joinCodeText != null)
                joinCodeText.text = $"Code: {code}";
        }

        #endregion

        #region Event handlers

        private void HandleLobbySnapshot(LobbyStateSnapshot snapshot)
        {
            var phase = (NetworkLobbyManager.LobbyPhase)snapshot.phase;

            // ---- Panel visibility based on lobby phase ----
            if (lobbyRootPanel != null)
                lobbyRootPanel.SetActive(phase != NetworkLobbyManager.LobbyPhase.InGame);

            // Update status text
            if (statusText != null)
            {
                var phaseText = phase switch
                {
                    NetworkLobbyManager.LobbyPhase.ReadyUp => "Status: Waiting for players...",
                    NetworkLobbyManager.LobbyPhase.WaitingForPlayers => "Status: Waiting for players...",
                    NetworkLobbyManager.LobbyPhase.InGame => "Status: Game in progress",
                    _ => "Status: Unknown"
                };
                statusText.text = phaseText;
            }

            // Rebuild / update all player rows
            var clientIds = snapshot.clientIds;
            var names = snapshot.names.ToStringArray();
            var colors = snapshot.colors.ToStringArray();
            var readyFlags = snapshot.readyFlags;

            if (playerCountText != null)
                playerCountText.text = $"Players: {clientIds.Length} / {snapshot.maxPlayers}";

            var seenIds = new HashSet<ulong>();

            for (var i = 0; i < clientIds.Length; i++)
            {
                var clientId = clientIds[i];
                seenIds.Add(clientId);

                var playerName = names[i];
                var colorHtml = colors[i];
                var isReady = readyFlags[i];

                if (!ColorUtility.TryParseHtmlString("#" + colorHtml, out var color))
                    color = Color.white;

                var isLocal = clientId == _localClientId;

                if (!_rows.TryGetValue(clientId, out var row))
                {
                    row = Instantiate(playerRowPrefab, playerListParent);
                    _rows[clientId] = row;
                }

                row.Initialize(clientId, playerName, color, isReady, isLocal);

                if (isLocal)
                    _localIsReady = isReady;
            }

            // Remove rows for players no longer present
            var toRemove = new List<ulong>();
            foreach (var kvp in _rows.Where(kvp => !seenIds.Contains(kvp.Key)))
            {
                Destroy(kvp.Value.gameObject);
                toRemove.Add(kvp.Key);
            }

            foreach (var id in toRemove)
                _rows.Remove(id);

            UpdateReadyButtonLabel();
            UpdateStartButtonState(snapshot);
        }

        #endregion

        #region Button handlers

        private void OnClickReady()
        {
            if (lobbyManager == null) return;

            _localIsReady = !_localIsReady;
            lobbyManager.SetLocalReady(_localIsReady);
            UpdateReadyButtonLabel();
        }

        private void OnClickStartGame()
        {
            if (lobbyManager == null) return;

            lobbyManager.HostRequestStartGame();
        }

        #endregion

        #region UI helpers

        private void UpdateReadyButtonLabel()
        {
            if (readyButtonLabel == null) return;

            readyButtonLabel.text = _localIsReady ? "Unready" : "Ready";
        }

        private void UpdateStartButtonState(LobbyStateSnapshot snapshot)
        {
            if (startButton == null) return;

            var isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
            startButton.gameObject.SetActive(isHost);

            if (!isHost) return;

            var readyFlags = snapshot.readyFlags;
            var allReady = readyFlags.Length > 0 && readyFlags.All(t => t);
            startButton.interactable = allReady;
        }

        #endregion
    }
}
