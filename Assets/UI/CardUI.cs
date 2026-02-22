using System;
using Cards;
using Networking;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// Individual card in the player's hand. Displays name, category,
    /// and tints background by category. Calls back with instanceId on click.
    /// Attach to the card prefab root (which should have a Button + Image).
    /// </summary>
    public class CardUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TMP_Text cardNameText;
        [SerializeField] private TMP_Text categoryText;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Button button;

        private int _instanceId;
        private Action<int> _onClickCallback;

        /// <summary>
        /// Called by CardHandController when spawning this card.
        /// </summary>
        public void Initialize(ClientCardData data, Action<int> onClickCallback)
        {
            _instanceId = data.InstanceId;
            _onClickCallback = onClickCallback;

            if (cardNameText != null)
                cardNameText.text = data.Name;

            if (categoryText != null)
                categoryText.text = GetCategoryDisplayName(data.Category);

            if (backgroundImage != null)
                backgroundImage.color = GetCategoryColor(data.Category);

            if (button != null)
                button.onClick.AddListener(OnClick);
        }

        public void SetInteractable(bool interactable)
        {
            if (button != null)
                button.interactable = interactable;
        }

        private void OnClick()
        {
            _onClickCallback?.Invoke(_instanceId);
        }

        private void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveAllListeners();
        }

        #region Category Display Helpers

        private static string GetCategoryDisplayName(CardCategory category)
        {
            return category switch
            {
                CardCategory.Puckd => "PUCK'D",
                CardCategory.GoalieSave => "GOALIE SAVE",
                CardCategory.Cancel => "CANCEL",
                CardCategory.Attack => "ATTACK",
                CardCategory.Skip => "SKIP",
                CardCategory.Peek => "PEEK",
                CardCategory.Shuffle => "SHUFFLE",
                _ => category.ToString().ToUpper()
            };
        }

        /// <summary>
        /// Returns a background tint color based on card category.
        /// These are intentionally muted so white text is readable.
        /// Adjust to taste or replace with per-card artwork.
        /// </summary>
        private static Color GetCategoryColor(CardCategory category)
        {
            return category switch
            {
                CardCategory.Puckd => new Color(0.75f, 0.15f, 0.15f),       // red
                CardCategory.GoalieSave => new Color(0.15f, 0.6f, 0.25f),   // green
                CardCategory.Cancel => new Color(0.7f, 0.5f, 0.15f),        // amber
                CardCategory.Attack => new Color(0.8f, 0.35f, 0.1f),        // orange
                CardCategory.Skip => new Color(0.2f, 0.45f, 0.75f),         // blue
                CardCategory.Peek => new Color(0.5f, 0.3f, 0.65f),          // purple
                CardCategory.Shuffle => new Color(0.3f, 0.55f, 0.55f),      // teal
                _ => new Color(0.4f, 0.4f, 0.4f)                            // gray
            };
        }

        #endregion
    }
}
