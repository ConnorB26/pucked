public struct PlayCardIntent
{
    public int PlayerId;
    public int InstanceId;
    public int? TargetPlayerId;
}

public struct DrawIntent
{
    public int PlayerId;
}

public struct StartMatchRequest
{
    public (int id, string name, int seat)[] Players;
    public DeckList DeckList;
    public GameConfig GameConfig;
}