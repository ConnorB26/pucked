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
    /// Server-only match orchestrator. Pure C# — no Netcode dependency.
    /// Manages deck, turns, card plays, draws, elimination, and game-over detection.
    /// </summary>
    public class CoreGameManager
    {
        #region Fields

        private DeckManager _deckManager;
        private TurnManager _turnManager;
        private GameContext _context;
        private GameActionExecutor _actionExecutor;
        private EffectResolver _effectResolver;
        private GameConfig _config;
        private List<CardInstance> _startingSaves;

        #endregion

        #region Properties

        public GamePhase Phase { get; private set; } = GamePhase.None;
        public List<PlayerRuntime> Players { get; private set; }
        public TurnManager TurnManager => _context?.TurnManager;

        #endregion

        #region Events

        public event Action<int> PlayerEliminated;
        public event Action<int, List<CardDefinition>> PeekRequested;
        public event Action<int> GameOver;
        /// <summary>Fired when a Goalie Save auto-blocks a Puck'd draw.</summary>
        public event Action<int> GoalieSaveUsed;

        #endregion

        #region Initialization

        /// <summary>Builds deck, deals starting hands, and transitions to InGame.</summary>
        public void Init(GameConfig config, EffectResolver resolver, List<PlayerRuntime> players)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _effectResolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            Players = players ?? throw new ArgumentNullException(nameof(players));

            Phase = GamePhase.Setup;

            _deckManager = new DeckManager();
            _deckManager.InitializeFromDeckDefinition(
                config.deckDefinition, players.Count, !config.disableShuffle, out _startingSaves);

            _turnManager = new TurnManager(Players);
            _context = new GameContext(config, _deckManager, _turnManager, Players);
            _actionExecutor = new GameActionExecutor(_context);

            _actionExecutor.OnPlayerEliminated += id => PlayerEliminated?.Invoke(id);
            _actionExecutor.OnPeekRequested += (pid, cards) => PeekRequested?.Invoke(pid, cards);

            DealStartingHands();
            Phase = GamePhase.InGame;
        }

        /// <summary>Gives each player one reserved save, then fills to startingHandSize (skipping Puck'd).</summary>
        private void DealStartingHands()
        {
            if (_config == null || _config.startingHandSize <= 0) return;

            if (_startingSaves is { Count: > 0 })
            {
                var saveIndex = 0;
                foreach (var player in Players)
                {
                    if (saveIndex >= _startingSaves.Count) break;
                    player.Hand.Add(_startingSaves[saveIndex]);
                    saveIndex++;
                }
            }

            foreach (var player in Players)
            {
                var skippedPuckd = new List<CardInstance>();

                while (player.Hand.Count < _config.startingHandSize)
                {
                    if (_deckManager.DrawCount == 0) break;
                    var maybeInstance = _deckManager.DrawTop();
                    if (maybeInstance == null) break;

                    var inst = maybeInstance.Value;
                    if (inst.Definition?.category == CardCategory.Puckd)
                    {
                        skippedPuckd.Add(inst);
                        continue;
                    }

                    player.Hand.Add(inst);
                }

                if (skippedPuckd.Count > 0)
                    _deckManager.ReturnCardsToDrawAndShuffle(skippedPuckd);
            }
        }

        #endregion

        #region Card Play

        /// <summary>Validates, removes card from hand, resolves effects, and applies actions.</summary>
        public void PlayCard(int playerId, int cardInstanceId, int targetPlayerId = 0)
        {
            if (Phase != GamePhase.InGame) return;

            var player = _context.GetPlayer(playerId);
            if (player == null || player.IsEliminated) return;

            var index = player.Hand.FindIndex(ci => ci.InstanceId == cardInstanceId);
            if (index < 0) return;

            var instance = player.Hand[index];
            var cardDef = instance.Definition;

            // GoalieSave and Puck'd are never manually playable.
            if (cardDef != null && (cardDef.category == CardCategory.GoalieSave
                                    || cardDef.category == CardCategory.Puckd))
                return;

            player.Hand.RemoveAt(index);

            var effectCtx = new EffectContext
            {
                OwnerPlayerId = playerId,
                TargetPlayerId = targetPlayerId,
                CardId = instance.InstanceId
            };

            _effectResolver.QueueEffects(cardDef.effects, effectCtx);
            var actions = _effectResolver.ResolveAll();
            _actionExecutor.ApplyActions(actions);

            // Playing a card does NOT end the turn. Skip/Attack advance via TurnManager directly.
            CheckForGameOver();
        }

        #endregion

        #region End Turn & Drawing

        /// <summary>Draws owed cards (normal + extra from attacks), handles Puck'd/GoalieSave, then advances turn.</summary>
        public void PlayerEndTurn(int playerId)
        {
            if (Phase != GamePhase.InGame) return;
            if (_context.TurnManager.CurrentPlayerId != playerId) return;

            var player = _context.GetPlayer(playerId);
            if (player == null || player.IsEliminated) return;

            if (_config.drawAtEndOfTurn)
            {
                var totalDraws = (1 + player.PendingExtraTurns) * _config.drawPerTurn;
                player.PendingExtraTurns = 0;

                for (var i = 0; i < totalDraws; i++)
                {
                    var drawn = _context.DeckManager.DrawTop();
                    if (!drawn.HasValue) break;

                    var instance = drawn.Value;
                    var def = instance.Definition;

                    if (def != null && def.category == CardCategory.Puckd)
                    {
                        var saveIdx = player.Hand.FindIndex(
                            ci => ci.Definition?.category == CardCategory.GoalieSave);

                        if (saveIdx >= 0)
                        {
                            // Auto-consume GoalieSave; return Puck'd to deck.
                            var saveCard = player.Hand[saveIdx];
                            player.Hand.RemoveAt(saveIdx);
                            _deckManager.Discard(saveCard);
                            _deckManager.ReturnCardsToDrawAndShuffle(new[] { instance });
                            GoalieSaveUsed?.Invoke(playerId);
                            break;
                        }

                        // No save — eliminate via effect pipeline.
                        var effectCtx = new EffectContext
                        {
                            OwnerPlayerId = playerId,
                            TargetPlayerId = playerId,
                            CardId = instance.InstanceId
                        };
                        _effectResolver.QueueEffects(def.effects, effectCtx);
                        var actions = _effectResolver.ResolveAll();
                        _actionExecutor.ApplyActions(actions);
                        break;
                    }

                    player.Hand.Add(instance);
                }
            }

            // Elimination already advances the turn via OnPlayerEliminated.
            if (!player.IsEliminated)
                _context.TurnManager.EndTurn();

            CheckForGameOver();
        }

        #endregion

        #region Player Leave & Game Over

        /// <summary>Returns a leaving player's hand to the deck, marks eliminated, checks game over.</summary>
        public void HandlePlayerLeft(int playerId)
        {
            if (Phase != GamePhase.InGame) return;

            var player = _context.GetPlayer(playerId);
            if (player == null || player.IsEliminated) return;

            if (player.Hand.Count > 0)
            {
                _deckManager.ReturnCardsToDrawAndShuffle(player.Hand);
                player.Hand.Clear();
            }

            player.IsEliminated = true;
            _turnManager.OnPlayerEliminated(playerId);
            PlayerEliminated?.Invoke(playerId);
            CheckForGameOver();
        }

        /// <summary>Ends the game when one or zero players remain alive.</summary>
        private void CheckForGameOver()
        {
            if (_config == null || !_config.lastPlayerStandingWins) return;

            var alivePlayers = _context.Players.Where(p => !p.IsEliminated).ToList();
            if (alivePlayers.Count > 1) return;

            Phase = GamePhase.GameOver;
            var winnerId = alivePlayers.FirstOrDefault()?.PlayerId ?? -1;
            GameOver?.Invoke(winnerId);
        }

        #endregion
    }
}
