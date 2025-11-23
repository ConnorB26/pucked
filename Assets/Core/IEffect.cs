using System.Collections.Generic;

public interface IEffect
{
    // Return the set of legal target playerIds for this effect (empty if none)
    IEnumerable<int> GetLegalTargets(GameState s, int sourcePlayerId);

    // Apply the effect. The engine has already removed the card from hand & discarded it.
    void Resolve(GameState s, int sourcePlayerId, int? targetPlayerId, int instanceId, System.Action<GameEvent> emit);
}