using System.Collections.Generic;
using System.Linq;

public sealed class AttackEffect : IEffect
{
    public const int ExtraTurns = 2;

    public IEnumerable<int> GetLegalTargets(GameState s, int sourcePlayerId)
        => Enumerable.Empty<int>(); // affects next player implicitly

    public void Resolve(GameState s, int sourcePlayerId, int? targetPlayerId, int instanceId,
        System.Action<GameEvent> emit)
    {
        // End the source turn without drawing and add debt for next active player.
        s.AttackDebt += ExtraTurns;
        // Note: actual "end turn" will be handled by rules after effect resolution.
    }
}

public sealed class SkipEffect : IEffect
{
    public IEnumerable<int> GetLegalTargets(GameState s, int sourcePlayerId) => Enumerable.Empty<int>();

    public void Resolve(GameState s, int sourcePlayerId, int? targetPlayerId, int instanceId,
        System.Action<GameEvent> emit)
    {
        if (s.AttackDebt > 0) s.AttackDebt -= 1; // consumes one owed draw
        else s.EndTurnNoDraw = true; // mark to end turn now without drawing
    }
}

public sealed class PeekEffect : IEffect
{
    public int Count = 3;

    public IEnumerable<int> GetLegalTargets(GameState s, int sourcePlayerId) => Enumerable.Empty<int>();

    public void Resolve(GameState s, int sourcePlayerId, int? targetPlayerId, int instanceId,
        System.Action<GameEvent> emit)
    {
        var top = s.DrawPile.Take(Count).ToArray();
        emit(new PeekResult(sourcePlayerId, top));
    }
}

public sealed class ShuffleEffect : IEffect
{
    public IEnumerable<int> GetLegalTargets(GameState s, int sourcePlayerId) => Enumerable.Empty<int>();

    public void Resolve(GameState s, int sourcePlayerId, int? targetPlayerId, int instanceId,
        System.Action<GameEvent> emit)
    {
        // Rebuild stack in random order (drawpile only)
        var arr = s.DrawPile.ToArray();
        s.DrawPile.Clear();
        // Fisher-Yates
        for (var i = arr.Length - 1; i > 0; --i)
        {
            var j = s.Rng.Next(i + 1);
            (arr[i], arr[j]) = (arr[j], arr[i]);
        }

        for (var i = arr.Length - 1; i >= 0; --i) s.DrawPile.Push(arr[i]);
        emit(new DeckShuffled());
    }
}