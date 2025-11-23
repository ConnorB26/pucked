using System.Linq;
using UnityEngine;

public class MatchHost : MonoBehaviour
{
    private GameRules _rules;
    private IHostAdapter _hostTx;
    private readonly EffectRegistry _effects = new();

    public System.Action<int> OnMatchEnded; // winnerId

    // Call this when wiring the transport
    public void BindTransport(IHostAdapter hostTx)
    {
        _hostTx = hostTx;
    }

    void Awake()
    {
        // Register core effects -> keeps logic centralized and testable
        _effects.Register(CardType.Attack, new AttackEffect());
        _effects.Register(CardType.Skip, new SkipEffect());
        _effects.Register(CardType.Peek, new PeekEffect());
        _effects.Register(CardType.Shuffle, new ShuffleEffect());
        // Puck’d / Save are handled in Draw (not played from hand) in GameRules.
    }

    public void StartMatch(StartMatchRequest req)
    {
        var catalog = CatalogAdapter.BuildCatalogFromDeck(req.DeckList);
        var tuples = CatalogAdapter.BuildDeckTuples(req.DeckList);
        var cfgDto = CatalogAdapter.ToDto(req.GameConfig);

        _rules = new GameRules(_effects, cfgDto.UseFixedSeed ? cfgDto.Seed : null);
        _rules.OnEvent += ForwardEventToClients; // forward every core event
        _rules.InitializeMatch(
            catalog,
            tuples,
            req.Players.Select(p => (p.id, p.name, p.seat)),
            cfgDto
        );
        // Core will immediately emit TurnStarted
    }

    public void ReclaimPlayerMidMatch(int playerId)
    {
        var ps = _rules?.State.Players.FirstOrDefault(p => p.PlayerId == playerId);
        if (ps == null || ps.Eliminated) return;

        // Move hand back to draw pile
        var hand = ps.Hand.ToList();
        ps.Hand.Clear();

        var arr = _rules.State.DrawPile.ToList();
        arr.AddRange(hand);

        // Shuffle
        for (var i = arr.Count - 1; i > 0; --i)
        {
            var j = _rules.State.Rng.Next(i + 1);
            (arr[i], arr[j]) = (arr[j], arr[i]);
        }

        _rules.State.DrawPile.Clear();
        for (var i = arr.Count - 1; i >= 0; --i) _rules.State.DrawPile.Push(arr[i]);

        // Eliminate player & inform clients
        ps.Eliminated = true;
        _rules.Emit(new PlayerEliminated(playerId));
    }

    // Client intents arrive here via transport adapter
    public void ReceiveIntent(object intent)
    {
        switch (intent)
        {
            case PlayCardIntent pc:
                _rules.PlayCard(pc.PlayerId, pc.InstanceId,
                    pc.TargetPlayerId); // validates + emits events
                break;
            case DrawIntent di:
                _rules.Draw(di
                    .PlayerId); // applies sudden-death on empty deck, elimination, debt, etc.
                break;
        }
    }

    private void ForwardEventToClients(GameEvent e)
    {
        switch (e)
        {
            case PeekResult pr:
                _hostTx?.SendTo(pr.PlayerId, e);
                break;
            default:
                _hostTx?.Broadcast(e);
                break;
        }

        // notify lobby when match ends
        if (e is GameEnded ge)
            OnMatchEnded?.Invoke(ge.WinnerPlayerId);
    }
}