using System;
using System.Collections.Generic;
using System.Linq;

public sealed class GameRules
{
    #region Fields & Ctor

    private readonly EffectRegistry _effects;
    public readonly GameState State;

    public event Action<GameEvent> OnEvent;

    public GameRules(EffectRegistry effects, int? seed = null)
    {
        _effects = effects;
        State = new GameState(seed);
    }

    #endregion


    #region Initialization

    public void InitializeMatch(
        IEnumerable<CardDef> catalog,
        IEnumerable<(string defId, int copies)> deckList,
        IEnumerable<(int playerId, string name, int seat)> players,
        GameConfigDTO cfg)
    {
        // Catalog
        foreach (var d in catalog) State.Catalog[d.DefId] = d;

        // Build instances + draw pile
        var nextInstanceId = 1;
        var temp = new List<int>();
        foreach (var (defId, copies) in deckList)
        {
            for (var i = 0; i < copies; i++)
            {
                var inst = new CardInstance(nextInstanceId++, defId);
                State.Instances[inst.InstanceId] = inst;
                temp.Add(inst.InstanceId);
            }
        }

        // Shuffle initial deck (deterministic by seed if provided)
        for (var i = temp.Count - 1; i > 0; --i)
        {
            var j = State.Rng.Next(i + 1);
            (temp[i], temp[j]) = (temp[j], temp[i]);
        }

        for (var i = temp.Count - 1; i >= 0; --i) State.DrawPile.Push(temp[i]);

        // Players
        State.Players.Clear();
        foreach (var p in players) State.Players.Add(new PlayerState(p.playerId, p.name, p.seat));

        // Deal starting hands + inject starting saves (does not consume the deck)
        foreach (var p in State.Players)
        {
            for (var i = 0; i < cfg.StartingHandSize; i++) DrawToHandOrReportEmpty(p);
            if (cfg.StartingSaveCards > 0)
            {
                var saveDef = State.Catalog.Values.FirstOrDefault(d => d.Type == CardType.Save);
                if (saveDef != null)
                {
                    for (var s = 0; s < cfg.StartingSaveCards; s++)
                    {
                        var inst = new CardInstance(State.Instances.Keys.DefaultIfEmpty(0).Max() + 1, saveDef.DefId);
                        State.Instances[inst.InstanceId] = inst;
                        p.Hand.Add(inst.InstanceId);
                    }
                }
            }
        }

        State.CurrentPlayerIndex = 0;
        State.AttackDebt = 0;
        State.EndTurnNoDraw = false;
        State.GameOver = false;

        Emit(new TurnStarted(State.CurrentPlayer.PlayerId, State.AttackDebt));
    }

    #endregion


    #region Queries

    public IEnumerable<int> GetLegalTargetsForCard(int playerId, int instanceId)
    {
        var p = State.Players.First(x => x.PlayerId == playerId);
        if (!p.Hand.Contains(instanceId)) return Enumerable.Empty<int>();

        var type = State.DefOf(instanceId).Type;
        var effect = _effects.Find(type);
        if (effect == null) return Enumerable.Empty<int>();
        return effect.GetLegalTargets(State, playerId);
    }

    #endregion


    #region Public Actions

    /// <summary>Play a card from hand. If the effect ends the turn, no draw step follows.</summary>
    public bool PlayCard(int playerId, int instanceId, int? targetPlayerId = null)
    {
        if (State.GameOver) return false;

        var p = State.CurrentPlayer;
        if (p.PlayerId != playerId || p.Eliminated) return false;
        if (!p.Hand.Contains(instanceId)) return false;

        var def = State.DefOf(instanceId);
        if (def.Type == CardType.Puckd || def.Type == CardType.Save) return false; // not played from hand

        // Target validation
        var effect = _effects.Find(def.Type);
        if (effect == null) return false;

        var legal = effect.GetLegalTargets(State, playerId).ToList();
        if (legal.Count > 0 && (!targetPlayerId.HasValue || !legal.Contains(targetPlayerId.Value)))
            return false;

        // Remove from hand, discard, emit
        p.Hand.Remove(instanceId);
        State.Discard.Add(instanceId);
        Emit(new CardPlayed(playerId, instanceId));

        // (Reaction window for Cancel would hook here.)

        // Resolve effect
        effect.Resolve(State, playerId, targetPlayerId, instanceId, Emit);

        // Check if the effect requested ending the turn without a draw (Skip)
        var endWithNoDraw = State.EndTurnNoDraw;
        if (endWithNoDraw) State.EndTurnNoDraw = false; // clear flag

        // Attack or explicit no-draw ends the turn immediately
        if (def.Type == CardType.Attack || endWithNoDraw)
        {
            AdvanceToNextPlayer(startedByEffect: true);
        }

        return true;
    }

    /// <summary>Draws a card (or consumes debt when deck is empty), then advances turn based on AttackDebt.</summary>
    public bool Draw(int playerId)
    {
        if (State.GameOver) return false;
        if (State.CurrentPlayer.PlayerId != playerId) return false;

        var outcome = DrawToHandOrReportEmpty(State.CurrentPlayer);

        if (outcome == DrawOutcome.DeckEmpty)
        {
            // Sudden-death: no draw occurs. If under debt, still consume ONE owed draw.
            Emit(new DrawSkippedDeckEmpty(playerId));

            if (State.AttackDebt > 0)
            {
                State.AttackDebt -= 1;
                if (State.AttackDebt > 0)
                {
                    Emit(new TurnStarted(State.CurrentPlayer.PlayerId, State.AttackDebt));
                }
                else
                {
                    AdvanceToNextPlayer();
                }
            }
            else
            {
                AdvanceToNextPlayer();
            }

            return true;
        }

        if (outcome == DrawOutcome.DrewPuckdEliminated)
        {
            // Elimination already emitted; if game continues, pass the turn.
            if (!State.GameOver) AdvanceToNextPlayer();
            return true;
        }

        // Drew a normal card or saved a Puck’d
        if (State.AttackDebt > 0)
        {
            State.AttackDebt -= 1;
            if (State.AttackDebt > 0)
                Emit(new TurnStarted(State.CurrentPlayer.PlayerId, State.AttackDebt));
            else
                AdvanceToNextPlayer();
        }
        else
        {
            AdvanceToNextPlayer();
        }

        return true;
    }

    #endregion


    #region Internals: Draw Helpers

    public enum DrawOutcome
    {
        DrewNormal, // non-Puck’d to hand
        DrewPuckdSaved, // Save consumed; Puck’d reinserted
        DrewPuckdEliminated, // player knocked out
        DeckEmpty // nothing to draw
    }

    private DrawOutcome DrawToHandOrReportEmpty(PlayerState p)
    {
        if (State.DrawPile.Count == 0)
            return DrawOutcome.DeckEmpty;

        var iid = State.DrawPile.Pop();
        var def = State.DefOf(iid);

        if (def.Type == CardType.Puckd)
        {
            Emit(new CardDrawn(p.PlayerId, iid, puckd: true));

            var save = p.Hand.FirstOrDefault(inst => State.DefOf(inst).Type == CardType.Save);
            if (save != 0)
            {
                // Consume save
                p.Hand.Remove(save);
                State.Discard.Add(save);

                // Reinsert Puck’d at random position
                var idx = State.Rng.Next(0, State.DrawPile.Count + 1);
                var temp = State.DrawPile.ToList();
                temp.Insert(idx, iid);
                State.DrawPile.Clear();
                for (var i = temp.Count - 1; i >= 0; --i) State.DrawPile.Push(temp[i]);

                return DrawOutcome.DrewPuckdSaved;
            }
            else
            {
                p.Eliminated = true;
                Emit(new PlayerEliminated(p.PlayerId));
                CheckForGameOver();
                return DrawOutcome.DrewPuckdEliminated;
            }
        }
        else
        {
            p.Hand.Add(iid);
            Emit(new CardDrawn(p.PlayerId, iid, puckd: false));
            return DrawOutcome.DrewNormal;
        }
    }

    #endregion


    #region Internals: Turn & Win

    private void AdvanceToNextPlayer(bool startedByEffect = false)
    {
        if (State.GameOver) return;

        State.CurrentPlayerIndex = State.NextActivePlayerIndex();
        Emit(new TurnStarted(State.CurrentPlayer.PlayerId, State.AttackDebt));
    }

    private void CheckForGameOver()
    {
        var alive = State.Players.Where(p => !p.Eliminated).ToList();
        if (alive.Count > 1) return;
        
        State.GameOver = true;
        var winner = alive.FirstOrDefault();
        Emit(new GameEnded(winner?.PlayerId ?? -1));
    }

    #endregion


    #region Internals: Events

    public void Emit(GameEvent e) => OnEvent?.Invoke(e);

    #endregion
}