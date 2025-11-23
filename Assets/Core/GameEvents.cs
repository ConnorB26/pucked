public abstract class GameEvent
{
}

public sealed class TurnStarted : GameEvent
{
    public readonly int PlayerId;
    public readonly int AttackDebt;

    public TurnStarted(int playerId, int debt)
    {
        PlayerId = playerId;
        AttackDebt = debt;
    }
}

public sealed class CardPlayed : GameEvent
{
    public readonly int PlayerId;
    public readonly int InstanceId;

    public CardPlayed(int playerId, int instanceId)
    {
        PlayerId = playerId;
        InstanceId = instanceId;
    }
}

public sealed class CardDrawn : GameEvent
{
    public readonly int PlayerId;
    public readonly int InstanceId;
    public readonly bool IsPuckd;

    public CardDrawn(int playerId, int instanceId, bool puckd)
    {
        PlayerId = playerId;
        InstanceId = instanceId;
        IsPuckd = puckd;
    }
}

public sealed class PlayerEliminated : GameEvent
{
    public readonly int PlayerId;

    public PlayerEliminated(int playerId)
    {
        PlayerId = playerId;
    }
}

public sealed class PeekResult : GameEvent
{
    public readonly int PlayerId;
    public readonly int[] InstanceIds; // top->down

    public PeekResult(int playerId, int[] ids)
    {
        PlayerId = playerId;
        InstanceIds = ids;
    }
}

public sealed class DeckShuffled : GameEvent
{
}

public sealed class GameEnded : GameEvent
{
    public readonly int WinnerPlayerId; // -1 if none

    public GameEnded(int winnerId)
    {
        WinnerPlayerId = winnerId;
    }
}

public sealed class DrawSkippedDeckEmpty : GameEvent
{
    public readonly int PlayerId;

    public DrawSkippedDeckEmpty(int playerId)
    {
        PlayerId = playerId;
    }
}