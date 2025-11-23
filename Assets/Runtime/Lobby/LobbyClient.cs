using UnityEngine;

public class LobbyClient : MonoBehaviour
{
    [Header("Local")] public int LocalPlayerId = -1; // set after join (in real net: set by Welcome event)

    private IClientAdapter _tx;

    public void BindTransport(IClientAdapter tx)
    {
        _tx = tx;
    }

    // === UI actions ===
    public void HostLobby(PlayerProfile profile, int maxPlayers) =>
        _tx?.SendIntent(new HostLobbyIntent { profile = profile, requestedMaxPlayers = maxPlayers }, this);

    public void JoinLobby(string code, PlayerProfile profile) =>
        _tx?.SendIntent(new JoinLobbyIntent { lobbyCode = code, profile = profile }, this);

    public void LeaveLobby() =>
        _tx?.SendIntent(new LeaveLobbyIntent { playerId = LocalPlayerId }, this);

    public void ToggleReady(bool ready) =>
        _tx?.SendIntent(new ToggleReadyIntent { playerId = LocalPlayerId, isReady = ready }, this);

    public void UpdateProfile(PlayerProfile profile) =>
        _tx?.SendIntent(new UpdateProfileIntent { playerId = LocalPlayerId, profile = profile }, this);

    public void StartMatch() =>
        _tx?.SendIntent(new StartMatchIntent { playerId = LocalPlayerId }, this);

    public void RestartLobby() =>
        _tx?.SendIntent(new RestartLobbyIntent { playerId = LocalPlayerId }, this);

    public void CloseLobby() =>
        _tx?.SendIntent(new CloseLobbyIntent { playerId = LocalPlayerId }, this);

    // === Host → Client events ===
    public void ReceiveEvent(object evt)
    {
        switch (evt)
        {
            case WelcomeAssigned w:
                LocalPlayerId = w.playerId;
                Debug.Log($"[Client] Assigned playerId={LocalPlayerId}");

                var mc = GetComponent<MatchClient>();
                if (mc) mc.LocalPlayerId = LocalPlayerId;
                break;

            case LobbyCodeCreated c:
                Debug.Log($"[Client {LocalPlayerId}] Lobby code: {c.lobbyCode}");
                break;

            case LobbySnapshot s:
                Debug.Log($"[Client {LocalPlayerId}] Lobby phase: {s.state.phase}, players: {s.state.players.Count}");
                break;

            case LobbyNotification n:
                Debug.Log($"[Client {LocalPlayerId}] {n.message}");
                break;

            case LobbyClosed:
                Debug.Log($"[Client {LocalPlayerId}] Lobby closed.");
                break;
        }
    }
}