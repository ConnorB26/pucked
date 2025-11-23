using System.Linq;
using UnityEngine;

public class LobbyHost : MonoBehaviour
{
    [Header("Config")] public int defaultMaxPlayers = 4;
    public Color[] colorPalette = { Color.red, Color.blue, Color.green, Color.yellow, Color.magenta, Color.cyan };

    [Header("Plugs")] public MatchHost matchHost; // assign in scene (your existing MatchHost)
    public DeckList deckList; // host picks in UI
    public GameConfig gameConfig; // host picks in UI

    private IHostAdapter _tx;
    private LobbyState _state = new();
    private int _nextPlayerId = 1;

    // === Wiring ===
    public void BindTransport(IHostAdapter tx)
    {
        _tx = tx;
    }

    // === Intents entrypoint ===
    public void ReceiveIntent(object intent)
    {
        switch (intent)
        {
            case HostLobbyIntent create: HandleHostLobby(create.profile, create.requestedMaxPlayers); break;
            case JoinLobbyIntent join: HandleJoin(join.lobbyCode, join.profile); break;
            case LeaveLobbyIntent leave: HandleLeave(leave.playerId); break;
            case ToggleReadyIntent r: HandleReady(r.playerId, r.isReady); break;
            case UpdateProfileIntent up: HandleUpdateProfile(up.playerId, up.profile); break;
            case StartMatchIntent sm: HandleStartMatch(sm.playerId); break;
            case RestartLobbyIntent rs: HandleRestart(rs.playerId); break;
            case CloseLobbyIntent cl: HandleClose(cl.playerId); break;
            case KickPlayerIntent kp: HandleKick(kp.hostPlayerId, kp.targetPlayerId); break;

            case PlayCardIntent pc:
                if (_state.phase == LobbyPhase.InMatch && matchHost) matchHost.ReceiveIntent(pc);
                break;

            case DrawIntent di:
                if (_state.phase == LobbyPhase.InMatch && matchHost) matchHost.ReceiveIntent(di);
                break;
        }
    }

    // === Core handlers ===

    void HandleHostLobby(PlayerProfile profile, int requestedMax)
    {
        if (!string.IsNullOrEmpty(_state.lobbyCode))
            return; // already hosting

        _state = new LobbyState
        {
            lobbyCode = GenerateCode(),
            phase = LobbyPhase.Waiting,
            maxPlayers = Mathf.Clamp(requestedMax > 0 ? requestedMax : defaultMaxPlayers, 2, 8)
        };
        _tx?.Broadcast(new LobbyCodeCreated { lobbyCode = _state.lobbyCode });

        var hostProfile = profile ?? new PlayerProfile { displayName = "Player" };
        InternalJoin(hostProfile, isHost: true);
        Notify($"Lobby created. Code: {_state.lobbyCode}");
        Snapshot();
    }

    void HandleJoin(string code, PlayerProfile profile)
    {
        if (_state.phase != LobbyPhase.Waiting)
        {
            NotifyTo(profile?.displayName, "Cannot join — match already started.");
            return;
        }

        if (string.IsNullOrEmpty(code) || code != _state.lobbyCode) return;
        if (_state.players.Count >= _state.maxPlayers)
        {
            NotifyTo(profile?.displayName, "Lobby full.");
            return;
        }

        InternalJoin(profile ?? new PlayerProfile { displayName = "Player" }, isHost: false);
        Snapshot();
    }

    void HandleLeave(int playerId)
    {
        var lp = _state.players.Find(p => p.playerId == playerId);
        if (lp == null) return;

        var wasHost = lp.isHost;

        // If leaving mid-match
        if (_state.phase == LobbyPhase.InMatch)
        {
            // If it's the host leaving during the match → close the lobby entirely.
            if (wasHost)
            {
                Notify($"{lp.profile.displayName} (host) left the match. Closing lobby...");
                InternalClose();
                return; // we're done; lobby is closed
            }

            // Non-host leaves mid-match → reclaim their cards and eliminate (as before)
            if (matchHost != null) matchHost.ReclaimPlayerMidMatch(playerId);
        }

        // Remove the player from the lobby list
        _state.players.Remove(lp);
        Notify($"{lp.profile.displayName} left the lobby.");

        // If host left in Waiting/PostMatch, promote first remaining seat to host
        if (_state.phase is LobbyPhase.Waiting or LobbyPhase.PostMatch && wasHost && _state.players.Count > 0)
            _state.players.OrderBy(p => p.seatIndex).First().isHost = true;

        // If lobby empty, auto close
        if (_state.players.Count == 0)
        {
            InternalClose();
            return;
        }

        Snapshot();
    }

    void HandleReady(int playerId, bool isReady)
    {
        if (_state.phase != LobbyPhase.Waiting) return;
        var lp = _state.players.Find(p => p.playerId == playerId);
        if (lp == null) return;
        lp.isReady = isReady;
        Notify($"{lp.profile.displayName} is {(isReady ? "ready" : "not ready")}.");
        Snapshot();
    }

    void HandleUpdateProfile(int playerId, PlayerProfile profile)
    {
        if (_state.phase != LobbyPhase.Waiting) return;
        var lp = _state.players.Find(p => p.playerId == playerId);
        if (lp == null) return;
        lp.profile = profile;
        lp.assignedColor = AssignUniqueColor(profile?.preferredColor, lp.playerId);
        Snapshot();
    }

    void HandleStartMatch(int playerId)
    {
        if (_state.phase != LobbyPhase.Waiting) return;
        var host = _state.players.FirstOrDefault(p => p.isHost);
        if (host == null || host.playerId != playerId) return;

        if (_state.players.Count < 2)
        {
            Notify("Need at least 2 players.");
            return;
        }

        if (!_state.players.TrueForAll(p => p.isReady))
        {
            Notify("All players must be ready.");
            return;
        }

        if (matchHost == null || deckList == null || gameConfig == null)
        {
            Notify("MatchHost/DeckList/GameConfig not set.");
            return;
        }

        var players = _state.players.OrderBy(p => p.seatIndex)
            .Select(p => (p.playerId, p.profile.displayName, p.seatIndex)).ToArray();

        // Start game
        matchHost.StartMatch(new StartMatchRequest { Players = players, DeckList = deckList, GameConfig = gameConfig });
        _state.phase = LobbyPhase.InMatch;
        Notify("Match starting!");
        Snapshot();

        // Subscribe for end-of-game to transition phase
        matchHost.OnMatchEnded = winnerId =>
        {
            _state.phase = LobbyPhase.PostMatch;
            Notify($"Match ended. Winner: Player {winnerId}.");
            Snapshot();
        };
    }

    void HandleRestart(int playerId)
    {
        if (_state.phase != LobbyPhase.PostMatch) return;
        var host = _state.players.FirstOrDefault(p => p.isHost);
        if (host == null || host.playerId != playerId) return;

        // Reset ready state to false; move to Waiting
        foreach (var p in _state.players) p.isReady = false;
        _state.phase = LobbyPhase.Waiting;
        Notify("Lobby reset. Ready up to start a new match.");
        Snapshot();
    }

    void HandleClose(int playerId)
    {
        var host = _state.players.FirstOrDefault(p => p.isHost);
        if (host == null || host.playerId != playerId) return;
        InternalClose();
    }

    void HandleKick(int hostPlayerId, int targetPlayerId)
    {
        var host = _state.players.FirstOrDefault(p => p.isHost);
        if (host == null || host.playerId != hostPlayerId) return;
        if (_state.phase != LobbyPhase.Waiting) return;

        var target = _state.players.Find(p => p.playerId == targetPlayerId);
        if (target == null) return;
        _state.players.Remove(target);
        Notify($"{target.profile.displayName} was removed by host.");
        Snapshot();
    }

    // === Helpers ===

    void InternalJoin(PlayerProfile profile, bool isHost)
    {
        var playerId = _nextPlayerId++;
        var seat = FirstFreeSeat();
        var color = AssignUniqueColor(profile?.preferredColor, playerId);

        _state.players.Add(new LobbyPlayer
        {
            playerId = playerId,
            seatIndex = seat,
            profile = profile,
            assignedColor = color,
            isReady = false,
            isHost = isHost
        });

        Notify($"{profile.displayName} {(isHost ? "(host)" : "")} joined.");

        // Tell this client their authoritative playerId
        _tx?.SendTo(playerId, new WelcomeAssigned { playerId = playerId });

        // Still send a full snapshot for UI
        _tx?.SendTo(playerId, new LobbySnapshot { state = CloneState() });
    }

    int FirstFreeSeat()
    {
        for (var i = 0; i < _state.maxPlayers; i++)
            if (_state.players.All(p => p.seatIndex != i))
                return i;
        return _state.players.Count;
    }

    Color AssignUniqueColor(Color? preferred, int seed)
    {
        var used = _state.players.Select(p => p.assignedColor).ToList();

        // Accept preferred if not too close
        if (preferred.HasValue && !IsTooClose(preferred.Value, used))
            return preferred.Value;

        // Palette pass
        foreach (var c in colorPalette)
            if (!IsTooClose(c, used))
                return c;

        // Fallback hash-based color
        var h = seed * 0.6180339f % 1f;
        return Color.HSVToRGB(h, 0.65f, 0.95f);
    }

    bool IsTooClose(Color candidate, System.Collections.Generic.List<Color> used)
    {
        foreach (var u in used)
        {
            Color.RGBToHSV(candidate, out var h1, out var s1, out var v1);
            Color.RGBToHSV(u, out var h2, out var s2, out var v2);
            if (Mathf.Abs(h1 - h2) < 0.08f && Mathf.Abs(s1 - s2) < 0.2f && Mathf.Abs(v1 - v2) < 0.2f)
                return true;
        }

        return false;
    }

    void InternalClose()
    {
        _state.phase = LobbyPhase.Closed;
        Notify("Lobby closed.");
        Snapshot();
        _tx?.Broadcast(new LobbyClosed());
        _state.lobbyCode = null;
        _state.players.Clear();
    }

    void Snapshot() => _tx?.Broadcast(new LobbySnapshot { state = CloneState() });
    void Notify(string msg) => _tx?.Broadcast(new LobbyNotification { message = msg });

    void NotifyTo(string name, string msg) =>
        _tx?.Broadcast(new LobbyNotification { message = $"[{name ?? "Player"}] {msg}" });

    LobbyState CloneState()
    {
        // shallow copy safe for UI
        return new LobbyState
        {
            lobbyCode = _state.lobbyCode,
            phase = _state.phase,
            maxPlayers = _state.maxPlayers,
            players = _state.players.Select(p => new LobbyPlayer
            {
                playerId = p.playerId,
                seatIndex = p.seatIndex,
                profile = p.profile, // pointer ok for display
                assignedColor = p.assignedColor,
                isReady = p.isReady,
                isHost = p.isHost
            }).ToList()
        };
    }
    
    private static string GenerateCode(int length = 6)
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no 0/O/1/I
        System.Text.StringBuilder sb = new(length);
        var rng = new System.Random();
        for (var i = 0; i < length; i++) sb.Append(alphabet[rng.Next(alphabet.Length)]);
        return sb.ToString();
    }
}