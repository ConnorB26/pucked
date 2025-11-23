#region Intents (Client → Host)

public struct HostLobbyIntent              { public PlayerProfile profile; public int requestedMaxPlayers; } // creates lobby + code
public struct JoinLobbyIntent              { public string lobbyCode; public PlayerProfile profile; }
public struct LeaveLobbyIntent             { public int playerId; }
public struct ToggleReadyIntent            { public int playerId; public bool isReady; }
public struct UpdateProfileIntent          { public int playerId; public PlayerProfile profile; }
public struct StartMatchIntent             { public int playerId; }   // must be host
public struct RestartLobbyIntent           { public int playerId; }   // host: return to Waiting
public struct CloseLobbyIntent             { public int playerId; }   // host: close lobby
public struct KickPlayerIntent             { public int hostPlayerId; public int targetPlayerId; } // optional, host only

#endregion

#region Events (Host → Clients)

public class LobbyCodeCreated              { public string lobbyCode; }
public class LobbySnapshot                 { public LobbyState state; }
public class LobbyNotification             { public string message; } // “Connor joined”, “Ashley left”, etc.
public class LobbyClosed                   { }
public class WelcomeAssigned               { public int playerId; }

#endregion