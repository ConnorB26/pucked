using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class LobbyPlayerRowUI : MonoBehaviour
    {
        [Header("UI References")] [SerializeField]
        private TMP_Text nameText;

        [SerializeField] private Image colorSwatch;
        [SerializeField] private TMP_Text readyText;
        [SerializeField] private GameObject localPlayerMarker;

        public ulong ClientId { get; private set; }

        #region Public API

        public void Initialize(ulong clientId, string playerName, Color color, bool isReady, bool isLocal)
        {
            ClientId = clientId;

            if (nameText != null)
                nameText.text = playerName;

            if (colorSwatch != null)
                colorSwatch.color = color;

            SetReady(isReady);

            if (localPlayerMarker != null)
                localPlayerMarker.SetActive(isLocal);
        }

        public void SetReady(bool isReady)
        {
            if (readyText == null)
                return;

            readyText.text = isReady ? "Ready" : "Not Ready";
            readyText.color = isReady ? Color.green : Color.red;
        }

        #endregion
    }
}