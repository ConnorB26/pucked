using System.Collections.Generic;

public sealed class LoopbackAdapter : IHostAdapter, IClientAdapter
{
    private readonly List<LobbyClient> _lobbyClients = new();
    private readonly List<MatchClient> _matchClients = new();

    // Map playerId <-> LobbyClient, and track who asked to join next
    private readonly Dictionary<int, LobbyClient> _byPlayerId = new();
    private readonly Queue<LobbyClient> _pendingJoiners = new();
    private LobbyHost _host;

    public void BindHost(LobbyHost host)
    {
        _host = host;
    }

    public void RegisterClient(LobbyClient c)
    {
        if (!_lobbyClients.Contains(c)) _lobbyClients.Add(c);
    }

    public void UnregisterClient(LobbyClient c)
    {
        _lobbyClients.Remove(c);
    }

    public void RegisterMatchClient(MatchClient c)
    {
        if (!_matchClients.Contains(c)) _matchClients.Add(c);
    }

    public void UnregisterMatchClient(MatchClient c)
    {
        _matchClients.Remove(c);
    }

    // === Clients -> Host ===
    public void SendIntent(object intent, LobbyClient sender)
    {
        // If this intent is "join" or "host", remember who asked so we can bind WelcomeAssigned.
        if (intent is HostLobbyIntent or JoinLobbyIntent)
            _pendingJoiners.Enqueue(sender);

        _host?.ReceiveIntent(intent);
    }
    
    public void SendIntent(object intent)
    {
        _host?.ReceiveIntent(intent);
    }

    // === Host -> Clients (broadcast/private) ===
    public void Broadcast(object evt)
    {
        foreach (var lc in _lobbyClients) lc.ReceiveEvent(evt);
        foreach (var mc in _matchClients) mc.ReceiveEvent(evt);
    }

    public void SendTo(int playerId, object evt)
    {
        // Special-case the very first WelcomeAssigned: bind playerId -> the joiner who asked.
        if (evt is WelcomeAssigned wa && !_byPlayerId.ContainsKey(wa.playerId))
        {
            if (_pendingJoiners.Count > 0)
            {
                var client = _pendingJoiners.Dequeue();
                _byPlayerId[wa.playerId] = client; // remember the route
                client.ReceiveEvent(evt); // deliver Welcome
                return;
            }
        }

        // Normal routing (after we know this playerId)
        if (_byPlayerId.TryGetValue(playerId, out var lobbyClient))
            lobbyClient.ReceiveEvent(evt);

        // Also deliver to the matching MatchClient if it exists
        foreach (var mc in _matchClients)
            if (mc.LocalPlayerId == playerId)
                mc.ReceiveEvent(evt);
    }
}