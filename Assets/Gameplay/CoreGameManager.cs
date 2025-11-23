using System;
using System.Collections.Generic;
using Cards;
using Effects;
using Effects.Base;
using UnityEngine;

namespace Gameplay
{
    /// <summary>
    /// Pure core game orchestration for a single match.
    /// Server/host only. No Unity networking types here.
    /// </summary>
    public class CoreGameManager
    {
        public GamePhase Phase { get; private set; } = GamePhase.None;

        private List<PlayerRuntime> _players;
        private DeckManager _deckManager;
        private TurnManager _turnManager;
        private GameContext _context;
        private GameActionExecutor _actionExecutor;
        private EffectResolver _effectResolver;
        private GameConfig _config;

        // Saves reserved by DeckManager to guarantee 1-per-player at start (as in EK Defuses).
        private List<CardInstance> _startingSaves;

        // Events re-exposed for networking / UI
        public event Action<int> PlayerEliminated;
        public event Action<int, List<CardDefinition>> PeekRequested;

        public void Init(GameConfig config,
            EffectResolver resolver,
            List<PlayerRuntime> players)
        {
            _config = config;
            _effectResolver = resolver;
            _players = players;

            Phase = GamePhase.Setup;

            // Build deck from DeckDefinition using player count,
            // and get back the list of starting save instances (one per player when possible).
            _deckManager = new DeckManager();
            _deckManager.InitializeFromDeckDefinition(
                config.deckDefinition,
                players.Count,
                !config.disableShuffle,
                out _startingSaves);

            _turnManager = new TurnManager(Players);
            _context = new GameContext(config, _deckManager, _turnManager, Players);
            _actionExecutor = new GameActionExecutor(_context);

            // Bridge internal executor events outward
            _actionExecutor.OnPlayerEliminated += id => PlayerEliminated?.Invoke(id);
            _actionExecutor.OnPeekRequested += (pid, cards) => PeekRequested?.Invoke(pid, cards);

            DealStartingHands();
            Phase = GamePhase.InGame;
        }

        /// <summary>
        /// Deal starting hands to all players:
        /// - First, give each player one reserved save card if available.
        /// - Then, draw from the deck until each player reaches startingHandSize.
        /// </summary>
        private void DealStartingHands()
        {
            if (_config.startingHandSize <= 0) return;

            // 1) Give each player one reserved save (if DeckManager generated enough).
            var saveIndex = 0;
            if (_startingSaves is { Count: > 0 })
            {
                foreach (var p in Players)
                {
                    if (saveIndex >= _startingSaves.Count)
                        break;

                    p.hand.Add(_startingSaves[saveIndex]);
                    saveIndex++;
                }
            }

            // 2) Top up each player's hand from the deck until startingHandSize.
            foreach (var p in Players)
            {
                while (p.hand.Count < _config.startingHandSize)
                {
                    var maybeInstance = _deckManager.DrawTop();
                    if (maybeInstance == null)
                        break;

                    p.hand.Add(maybeInstance.Value); // PlayerRuntime.hand is List<CardInstance>
                }
            }
        }

        /// <summary>
        /// Play a card by its instance ID, from the specified player's hand.
        /// Called only on the server/host (from NetworkGameManager).
        /// </summary>
        public void PlayCard(int playerId, int cardInstanceId)
        {
            if (Phase != GamePhase.InGame)
                return;

            var player = _context.GetPlayer(playerId);
            if (player == null || player.isEliminated)
                return;

            // Find the card instance in this player's hand
            var index = player.hand.FindIndex(ci => ci.InstanceId == cardInstanceId);
            if (index < 0)
                return; // player doesn't own this card (desync or cheat attempt)

            var instance = player.hand[index];
            var cardDef = instance.Definition;

            // Remove from hand before resolving
            player.hand.RemoveAt(index);

            var effectCtx = new EffectContext
            {
                OwnerPlayerId = playerId,
                TargetPlayerId = 0, // can be changed by targetable effects later
                CardId = instance.InstanceId // use instance ID as the "cardId" in context
            };

            _effectResolver.QueueEffects(cardDef.effects, effectCtx);
            var actions = _effectResolver.ResolveAll();
            _actionExecutor.ApplyActions(actions);

            HandleEndOfTurn();
        }

        private void HandleEndOfTurn()
        {
            _context.turnManager.EndTurn();

            if (_config.drawAtEndOfTurn)
            {
                var currentPlayer = _context.GetPlayer(_context.turnManager.CurrentPlayerId);
                for (var i = 0; i < _config.drawPerTurn; i++)
                {
                    var maybeInstance = _context.deckManager.DrawTop();
                    if (maybeInstance == null) break;

                    currentPlayer.hand.Add(maybeInstance.Value);
                }
            }

            CheckForGameOver();
        }

        public void HandlePlayerLeft(int playerId)
        {
            if (Phase != GamePhase.InGame)
                return;

            var player = _context.GetPlayer(playerId);
            if (player == null || player.isEliminated)
                return;

            // Return their cards to the deck and shuffle.
            if (player.hand.Count > 0)
            {
                _deckManager.ReturnCardsToDrawAndShuffle(player.hand);
                player.hand.Clear();
            }

            player.isEliminated = true;
            _turnManager.OnPlayerEliminated(playerId);
            PlayerEliminated?.Invoke(playerId);

            // Re-use last-player-standing win check
            CheckForGameOver();
        }

        private void CheckForGameOver()
        {
            if (!_config.lastPlayerStandingWins)
                return;

            var aliveCount = 0;
            PlayerRuntime winner = null;
            foreach (var p in _context.players)
            {
                if (!p.isEliminated)
                {
                    aliveCount++;
                    winner = p;
                }
            }

            if (aliveCount <= 1)
            {
                Phase = GamePhase.GameOver;
                // TODO: notify outer layer about winner (e.g., event or callback)
                Debug.Log("[CoreGameManager] Game over due to last player standing (disconnect/elimination).");
            }
        }

        public TurnManager TurnManager => _context.turnManager;
        public List<PlayerRuntime> Players => _players;
    }
}