using UnityEngine;

public class LocalLoopbackBootstrap : MonoBehaviour
{
    public LobbyHost lobbyHost;
    public MatchHost matchHost;
    public LobbyClient[] lobbyClients;
    public MatchClient[] matchClients;

    private LoopbackAdapter _adapter;

    void Awake()
    {
        _adapter = new LoopbackAdapter();

        // Bind host-side services
        lobbyHost.BindTransport(_adapter);
        _adapter.BindHost(lobbyHost); // loopback uses LobbyHost as the server-side receiver

        matchHost.BindTransport(_adapter); // so MatchHost can broadcast events to clients

        // Register clients
        foreach (var lc in lobbyClients)
        {
            lc.BindTransport(_adapter);
            _adapter.RegisterClient(lc);
        }

        foreach (var mc in matchClients)
        {
            mc.BindTransport(_adapter);
            _adapter.RegisterMatchClient(mc);
        }
    }
}