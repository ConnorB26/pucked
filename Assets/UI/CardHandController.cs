using System.Collections.Generic;
using System.Linq;
using Cards;
using Networking;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// Spawns/destroys CardUI instances to mirror the local player's hand.
    /// Subscribes to GameEvents for hand updates and turn changes.
    ///
    /// Lives on the CardHandArea panel inside the HUD. Since the HUD panel
    /// starts inactive, OnEnable catches up with any state that arrived
    /// before this component was activated.
    /// </summary>
    public class CardHandController : MonoBehaviour
    {
        [SerializeField] private CardUI cardPrefab;
        [SerializeField] private Transform handParent;
        [SerializeField] private TargetSelectorController targetSelector;

        private readonly List<CardUI> _cards = new();
        // Maps each spawned CardUI to its category so we can block non-playable cards.
        private readonly Dictionary<CardUI, CardCategory> _cardCategories = new();
        private NetworkGameManager _networkGameManager;

        #region Unity Lifecycle

        private void Start()
        {
            _networkGameManager = FindFirstObjectByType<NetworkGameManager>();
        }

        private void OnEnable()
        {
            GameEvents.OnLocalHandUpdated += RebuildHand;
            GameEvents.OnTurnChanged += HandleTurnChanged;

            // Catch up: hand data may have arrived before this panel was active.
            if (LocalGameState.Hand.Count > 0)
                RebuildHand();
        }

        private void OnDisable()
        {
            GameEvents.OnLocalHandUpdated -= RebuildHand;
            GameEvents.OnTurnChanged -= HandleTurnChanged;
        }

        #endregion

        #region Hand Rebuild

        private void RebuildHand()
        {
            foreach (var card in _cards.Where(card => card != null))
                Destroy(card.gameObject);

            _cards.Clear();
            _cardCategories.Clear();

            foreach (var data in LocalGameState.Hand)
            {
                var cardUI = Instantiate(cardPrefab, handParent);
                cardUI.Initialize(data, OnCardClicked);
                _cards.Add(cardUI);
                _cardCategories[cardUI] = data.Category;
            }

            UpdateInteractability();
        }

        #endregion

        #region Interactability

        private void HandleTurnChanged(int currentPlayerId) => UpdateInteractability();

        private void UpdateInteractability()
        {
            var isMyTurn = LocalGameState.IsMyTurn;

            foreach (var card in _cards.Where(card => card != null))
            {
                // GoalieSave is auto-triggered on a Puck'd draw — never manually playable.
                var category = _cardCategories.GetValueOrDefault(card);
                var playable = isMyTurn && category != CardCategory.GoalieSave;
                card.SetInteractable(playable);
            }
        }

        #endregion

        #region Card Click

        private void OnCardClicked(int instanceId)
        {
            if (!LocalGameState.IsMyTurn) return;

            if (_networkGameManager == null)
            {
                Debug.LogWarning("[CardHandController] No NetworkGameManager found.");
                return;
            }

            // Look up the category from live hand data (always in sync after each hand sync RPC).
            var handEntry = LocalGameState.Hand.Find(h => h.InstanceId == instanceId);

            if (RequiresTarget(handEntry.Category) && targetSelector != null)
            {
                targetSelector.Show(targetId => _networkGameManager.RequestPlayCard(instanceId, targetId));
                return;
            }

            _networkGameManager.RequestPlayCard(instanceId);
        }

        private static bool RequiresTarget(CardCategory category) => category == CardCategory.Attack;

        #endregion
    }
}