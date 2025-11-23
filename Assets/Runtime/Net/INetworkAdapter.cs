public interface IHostAdapter
{
    void Broadcast(object evt);
    void SendTo(int playerId, object evt);

    void RegisterClient(LobbyClient client);
    void UnregisterClient(LobbyClient client);

    void RegisterMatchClient(MatchClient client);
    void UnregisterMatchClient(MatchClient client);
}

public interface IClientAdapter
{
    void SendIntent(object intent, LobbyClient sender);
    void SendIntent(object intent);
    void BindHost(LobbyHost host);
}