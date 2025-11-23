using System;
using System.Collections.Generic;
using System.Linq;

public sealed class GameState
{
    public readonly Dictionary<string, CardDef> Catalog = new(); // DefId -> CardDef
    public readonly Dictionary<int, CardInstance> Instances = new(); // InstanceId -> Instance
    public readonly List<PlayerState> Players = new();
    public readonly Stack<int> DrawPile = new(); // InstanceIds
    public readonly List<int> Discard = new(); // InstanceIds (top = last)
    public bool EndTurnNoDraw = false; // used by Skip to end turn w/o draw
    public int CurrentPlayerIndex = 0;
    public int AttackDebt = 0; // turns owed by current player
    public bool GameOver = false;

    // RNG: deterministic per match when seeded
    public readonly Random Rng;

    public GameState(int? seed = null)
    {
        Rng = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    public PlayerState CurrentPlayer => Players[CurrentPlayerIndex];

    public CardDef DefOf(int instanceId) => Catalog[Instances[instanceId].DefId];

    public IEnumerable<PlayerState> ActiveOpponentsOf(PlayerState p) =>
        Players.Where(x => !x.Eliminated && x.PlayerId != p.PlayerId);

    public int NextActivePlayerIndex()
    {
        if (Players.All(p => p.Eliminated)) return CurrentPlayerIndex;
        var idx = CurrentPlayerIndex;
        do
        {
            idx = (idx + 1) % Players.Count;
        } while (Players[idx].Eliminated);

        return idx;
    }
}