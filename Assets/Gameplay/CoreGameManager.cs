using System;
using System.Collections.Generic;
using System.Linq;
using Cards;
using Effects;
using Effects.Base;
using UnityEngine;

namespace Gameplay
{
    /// <summary>
    /// Pure core game orchestration for a single match.
    /// Server/host only. No Unity networking types here.
    /// Responsibilities:
    ///  - Initialize deck, turn manager, runtime player state.
    ///  - Deal starting hands (including reserved "saves").
    ///  - Orchestrate playing cards -> resolving effects -> applying actions.
    ///  - Handle end-of-turn draws, player leaves, and last-player-standing win condition.
    /// </summary>
    public class CoreGameManager
    {
        // Current lifecycle phase of this match.
        public GamePhase Phase { get; private set; } = GamePhase.None;

        // Core collaborators (set during Init).
        private DeckManager _deckManager;
        private TurnManager _turnManager;
        private GameContext _context;
        private GameActionExecutor _actionExecutor;
        private EffectResolver _effectResolver;
        private GameConfig _config;

        // Runtime players (shallow reference to game context players).
        public List<PlayerRuntime> Players { get; private set; }

        // Saves reserved by DeckManager to guarantee one-per-player at start when possible.
        private List<CardInstance> _startingSaves;

        // Events re-exposed for networking / UI
        public event Action<int> PlayerEliminated;
        public event Action<int, List<CardDefinition>> PeekRequested;
        public event Action<int> GameOver;

        // Convenient access to current TurnManager via context (when initialized).
        public TurnManager TurnManager => _context?.TurnManager;

        /// <summary>
        /// Initialize the core game manager with config, effect resolver, and players.
        /// Builds the deck, prepares the turn manager and deals starting hands.
        /// </summary>
        public void Init(GameConfig config,
            EffectResolver resolver,
            List<PlayerRuntime> players)
        {
            // Basic validation
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));

            _config = config;
            _effectResolver = resolver;
            Players = players ?? throw new ArgumentNullException(nameof(players));

            Phase = GamePhase.Setup;

            // Build deck and collect reserved "saves" for starting hands.
            _deckManager = new DeckManager();
            _deckManager.InitializeFromDeckDefinition(
                config.deckDefinition,
                players.Count,
                !config.disableShuffle,
                out _startingSaves);

            // Setup turn manager and context, then action executor that operates on the context.
            _turnManager = new TurnManager(Players);
            _context = new GameContext(config, _deckManager, _turnManager, Players);
            _actionExecutor = new GameActionExecutor(_context);

            // Bridge internal executor events outward (safe null invocation).
            _actionExecutor.OnPlayerEliminated += id => PlayerEliminated?.Invoke(id);
            _actionExecutor.OnPeekRequested += (pid, cards) => PeekRequested?.Invoke(pid, cards);

            // Deal initial hands and move to in-game phase.
            DealStartingHands();
            Phase = GamePhase.InGame;
        }

        /// <summary>
        /// Deal starting hands to all players:
        /// 1) Give each player one reserved save (from _startingSaves) if available.
        /// 2) Draw from the deck until each player's hand reaches startingHandSize.
        /// </summary>
        private void DealStartingHands()
        {
            if (_config == null || _config.startingHandSize <= 0) return;

            // 1) Give each player one reserved save (if DeckManager generated enough).
            if (_startingSaves != null && _startingSaves.Count > 0)
            {
                var saveIndex = 0;
                foreach (var player in Players)
                {
                    if (saveIndex >= _startingSaves.Count) break;
                    player.Hand.Add(_startingSaves[saveIndex]);
                    saveIndex++;
                }
            }

            // 2) Top up each player's hand from the deck until startingHandSize.
            foreach (var player in Players)
            {
                while (player.Hand.Count < _config.startingHandSize)
                {
                    var maybeInstance = _deckManager.DrawTop();
                    if (maybeInstance == null) break;
                    player.Hand.Add(maybeInstance.Value);
                }
            }
        }

        /// <summary>
        /// Play a card from the given player's hand identified by cardInstanceId.
        /// This is server/host-only logic. It:
        ///  - Validates ownership and game state
        ///  - Removes the card from hand
        ///  - Queues and resolves effects using the EffectResolver
        ///  - Applies resulting actions via GameActionExecutor
        ///  - Advances end-of-turn handling
        /// </summary>
        public void PlayCard(int playerId, int cardInstanceId)
        {
            if (Phase != GamePhase.InGame) return;

            var player = _context.GetPlayer(playerId);
            if (player == null)
            {
                Debug.LogWarning($"[CoreGameManager] PlayCard: invalid playerId={playerId}");
                return;
            }

            if (player.IsEliminated) return;

            // Find the card instance in this player's hand.
            var index = player.Hand.FindIndex(ci => ci.InstanceId == cardInstanceId);
            if (index < 0)
            {
                // Could be a desync or hook attempt; ignore silently but log for diagnostics.
                Debug.LogWarning(
                    $"[CoreGameManager] PlayCard: player {playerId} does not own cardInstance {cardInstanceId}");
                return;
            }

            var instance = player.Hand[index];
            var cardDef = instance.Definition;

            // Remove from hand before resolving to avoid re-entrancy issues.
            player.Hand.RemoveAt(index);

            // Prepare effect context. Targeting logic may update TargetPlayerId later.
            var effectCtx = new EffectContext
            {
                OwnerPlayerId = playerId,
                TargetPlayerId = 0,
                CardId = instance.InstanceId
            };

            // Queue & resolve effects, then apply the resulting actions.
            _effectResolver.QueueEffects(cardDef.effects, effectCtx);
            var actions = _effectResolver.ResolveAll();
            _actionExecutor.ApplyActions(actions);

            HandleEndOfTurn();
        }

        /// <summary>
        /// Common end-of-turn handling:
        ///  - Advance turn to next player
        ///  - Optionally draw cards at end of turn according to config
        ///  - Check for last-player-standing game over condition
        /// </summary>
        private void HandleEndOfTurn()
        {
            _context.TurnManager.EndTurn();

            if (_config.drawAtEndOfTurn)
            {
                var currentPlayer = _context.GetPlayer(_context.TurnManager.CurrentPlayerId);
                if (currentPlayer != null)
                {
                    for (var i = 0; i < _config.drawPerTurn; i++)
                    {
                        var maybeInstance = _context.DeckManager.DrawTop();
                        if (maybeInstance == null) break;
                        currentPlayer.Hand.Add(maybeInstance.Value);
                    }
                }
            }

            CheckForGameOver();
        }

        /// <summary>
        /// Handle a player leaving mid-game: return their cards to the deck, mark eliminated,
        /// notify turn manager and external listeners, and check for game over.
        /// </summary>
        public void HandlePlayerLeft(int playerId)
        {
            if (Phase != GamePhase.InGame) return;

            var player = _context.GetPlayer(playerId);
            if (player == null || player.IsEliminated) return;

            // Return their hand to the deck and shuffle to avoid card loss.
            if (player.Hand.Count > 0)
            {
                _deckManager.ReturnCardsToDrawAndShuffle(player.Hand);
                player.Hand.Clear();
            }

            player.IsEliminated = true;
            _turnManager.OnPlayerEliminated(playerId);
            PlayerEliminated?.Invoke(playerId);

            // Re-use last-player-standing win check after elimination.
            CheckForGameOver();
        }

        /// <summary>
        /// If the config specifies last-player-standing wins, check the remaining alive players.
        /// If only one or zero remain, end the game and invoke GameOver with the winner id or -1.
        /// </summary>
        private void CheckForGameOver()
        {
            if (_config == null || !_config.lastPlayerStandingWins) return;

            // Count alive players; capture the (last) survivor if present.
            var alivePlayers = _context.Players.Where(p => !p.IsEliminated).ToList();
            if (alivePlayers.Count > 1) return;

            Phase = GamePhase.GameOver;

            var winnerId = alivePlayers.FirstOrDefault()?.PlayerId ?? -1;
            Debug.Log($"[CoreGameManager] Game over due to last player standing. Winner playerId={winnerId}");
            GameOver?.Invoke(winnerId);
        }
    }
}