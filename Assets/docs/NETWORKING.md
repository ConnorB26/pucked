# Puck'd - Networking Layer

## Overview

The networking layer bridges Unity's Netcode for GameObjects with the pure C# gameplay layer. It handles connection management, lobby lifecycle, and game state synchronization via RPCs and snapshot structs. The host acts as the authoritative server.

**Namespace:** `Networking`, `Networking.Snapshots`

## Files

| File | Type | Purpose |
|------|------|---------|
| `RelayBootstrap.cs` | Static class | Unity Relay setup for host and client |
| `NetworkLobbyManager.cs` | NetworkBehaviour | Pre-game lobby lifecycle |
| `NetworkGameManager.cs` | NetworkBehaviour | Game RPC bridge over CoreGameManager |
| `NetworkDisconnectHandler.cs` | MonoBehaviour | Client disconnect -> main menu |
| `PlayerProfileData.cs` | Struct + static helpers | Player identity and persistence |
| `Snapshots/HandSnapshot.cs` | INetworkSerializable | Player hand state for sync |
| `Snapshots/LobbyStateSnapshot.cs` | INetworkSerializable | Full lobby state for sync |
| `Snapshots/PeekSnapshot.cs` | INetworkSerializable | Peek card results for sync |

## RelayBootstrap

**File:** `Networking/RelayBootstrap.cs`

Static utility class that initializes Unity Services, authenticates anonymously, and configures the Relay + UTP transport.

### Static Properties
- `LastJoinCode` - The most recent Relay join code (set after hosting or joining)
- `MaxConnections` - Max clients set when hosting (used by lobby for display)

### Key Methods

**`StartHostWithRelay(int maxConnections) -> Task<string>`**
1. Calls `EnsureServicesAsync()` (init + anonymous auth)
2. Creates Relay allocation via `RelayService.Instance.CreateAllocationAsync()`
3. Configures `UnityTransport` component with host relay data
4. Gets join code from Relay
5. Starts `NetworkManager.Singleton` as host
6. Returns join code (or null on failure)

**`StartClientWithRelay(string joinCode) -> Task<bool>`**
1. Calls `EnsureServicesAsync()`
2. Joins Relay allocation via `RelayService.Instance.JoinAllocationAsync()`
3. Configures `UnityTransport` with client relay data
4. Starts `NetworkManager.Singleton` as client
5. Returns success boolean

**`EnsureServicesAsync()`** - Idempotent helper that initializes Unity Services and signs in anonymously if not already done.

### Notes
- No error handling UI - failures only log to console
- `MaxConnections` defaults to 0 if not set (lobby falls back to 4)
- The join code is stored statically and accessed by `LobbyUIController` to display it

## NetworkLobbyManager

**File:** `Networking/NetworkLobbyManager.cs`

Server-authoritative lobby manager that tracks connected players, their profiles, and ready states. Transitions between lobby phases and signals the game manager to start matches.

### Lobby Phases
```csharp
enum LobbyPhase { WaitingForPlayers, ReadyUp, InGame }
```
Starts in `ReadyUp`. Transitions to `InGame` when host starts game. Resets to `ReadyUp` after game ends.

### Server-Side State
- `Dictionary<ulong, LobbyPlayer> _players` - All connected players keyed by clientId
- `LobbyPhase _phase` - Current lobby phase
- Each `LobbyPlayer` holds: clientId, `PlayerProfileData` (name + color), isReady flag

### Events
- `OnLobbySnapshotReceived(LobbyStateSnapshot)` - Fired on all clients when lobby state changes. UI subscribes to this.

### RPCs: Client -> Server

| RPC | Permission | Purpose |
|-----|-----------|---------|
| `SubmitProfileRpc(name, colorHtml)` | Everyone | Update player profile on server |
| `ToggleReadyRpc(bool)` | Everyone | Set player ready/unready |
| `RequestStartGameRpc()` | Owner | Host-only: attempt to start game |

### RPCs: Server -> Clients

| RPC | Target | Purpose |
|-----|--------|---------|
| `SyncLobbyStateRpc(LobbyStateSnapshot)` | NotServer | Full lobby state update |

The server also calls `ApplyLobbySnapshotLocal()` directly (not via RPC) to update its own UI.

### Connection Handling

**Client connects:**
- If phase is `InGame`, immediately disconnected via `DisconnectClient()`
- Otherwise, added with placeholder profile; real profile arrives via `SubmitProfileRpc`

**Client disconnects:**
- Removed from `_players` dictionary
- Lobby snapshot broadcast to remaining players

### Game Start Flow
`TryStartGameOnServer()`:
1. Validates phase == `ReadyUp`
2. Validates all players ready
3. Phase -> `InGame`
4. Copies all profiles to `MatchPlayerRegistry` (static)
5. Finds `NetworkGameManager` in scene, calls `ServerStartGame()`
6. Broadcasts lobby snapshot

### Lobby Reset
`ServerResetLobby()`:
- Phase -> `ReadyUp`
- All ready flags -> false
- Clears `MatchPlayerRegistry`
- Broadcasts snapshot

### Suggestions for Improvement
- **Min player validation** - `TryStartGameOnServer()` doesn't enforce a minimum player count (could start with 1 player)
- **Kick player** - No mechanism for host to remove a player
- **Late join** - Players connecting during `InGame` are rejected; could support spectator mode
- **Profile validation** - No sanitization of display names (length, characters)
- **Connection approval** - Could use Netcode's connection approval to check max players before accepting

## NetworkGameManager

**File:** `Networking/NetworkGameManager.cs`

Network-facing wrapper around `CoreGameManager`. Manages the clientId <-> playerId mapping, sends RPCs for game events, and validates client requests.

### Fields
- `GameConfig gameConfig` - Inspector reference to game config asset
- `EffectResolver effectResolver` - Inspector reference to effect resolver in scene
- `CoreGameManager _core` - Server-side game instance (created at game start)
- `Dictionary<ulong, int> _clientIdToPlayerId` - Forward mapping
- `Dictionary<int, ulong> _playerIdToClientId` - Reverse mapping
- `int LocalPlayerId` - Client-side: this client's player ID

### RPCs: Client -> Server

| RPC | Purpose |
|-----|---------|
| `RequestPlayCardRpc(int cardInstanceId)` | Client requests to play a card |

**Validation in RequestPlayCardRpc:**
1. Sender's clientId must be in the mapping
2. Sender's playerId must match `TurnManager.CurrentPlayerId`
3. Card instance ID must exist in player's hand
4. Then calls `_core.PlayCard(playerId, cardInstanceId)`
5. After play: `SyncAllHands()` + `NotifyTurnChangedRpc()`

### RPCs: Server -> Clients

| RPC | Target | Purpose |
|-----|--------|---------|
| `AssignPlayerIdRpc(int)` | Single client | Tell client their player ID |
| `SyncHandRpc(HandSnapshot)` | Single client | Sync a player's hand |
| `NotifyTurnChangedRpc(int)` | NotServer | Broadcast whose turn it is |
| `PlayerEliminatedRpc(int)` | NotServer | Broadcast player elimination |
| `PeekResultRpc(PeekSnapshot)` | Single client | Send peek results to peeking player |
| `GameOverRpc(int)` | Everyone | Broadcast game over + winner |

### Client Disconnect Handling
When a client disconnects during a game:
1. Removes from mapping dictionaries
2. Calls `_core.HandlePlayerLeft(playerId)` (returns cards, marks eliminated)
3. Syncs remaining hands
4. Broadcasts `PlayerEliminatedRpc`
5. If game still going: broadcasts turn change
6. If game ended: logs TODO

### Game Over Handling
`OnCoreGameOver(winnerPlayerId)`:
1. Broadcasts `GameOverRpc` to everyone
2. Calls `ServerEndGame()` (cleanup)
3. Finds `NetworkLobbyManager` and calls `ServerResetLobby()`

### Suggestions for Improvement
- **Client-side hand state** - `SyncHandRpc` receives data but doesn't store it anywhere accessible to UI. Need a client-side hand state manager.
- **Turn notification on host** - `NotifyTurnChangedRpc` is `SendTo.NotServer`, so the host doesn't receive it. Host needs local turn tracking too.
- **Redundant validation** - `RequestPlayCardRpc` validates hand ownership, then `CoreGameManager.PlayCard()` validates again. Could simplify.
- **Error feedback** - When a client's play request is rejected (wrong turn, invalid card), no feedback is sent back. Should send an error RPC.
- **Game over from disconnect** - The TODO at line 93-95 needs implementation (game over UI on clients when game ends due to disconnect).

## Snapshot Structs

All snapshots implement `INetworkSerializable` for Netcode RPC transport.

### HandSnapshot
```
playerId: int
instanceIds: int[]     - Unique card instance IDs
names: SerializableStringList  - Card display names
categories: int[]      - CardCategory enum values as ints
```
Sent to individual clients after card plays to sync their hand state.

### LobbyStateSnapshot
```
phase: int             - LobbyPhase enum as int
maxPlayers: int
clientIds: ulong[]
names: SerializableStringList
colors: SerializableStringList  - HTML color strings
readyFlags: bool[]
```
Broadcast to all clients on any lobby state change.

### PeekSnapshot
```
playerId: int
names: SerializableStringList
categories: int[]
```
Sent only to the peeking player when they play a Peek card.

### SerializableStringList (Utility)
Wraps `FixedString128Bytes[]` for efficient network serialization of string arrays. Used by all snapshots that need to transmit string data.

## PlayerProfileData

**File:** `Networking/PlayerProfileData.cs`

### PlayerProfileData (struct)
Simple value type with `displayName` (string) and `color` (Color).

### LocalPlayerProfile (static class)
Saves/loads profile to `PlayerPrefs` using keys prefixed with `Puckd_Profile_`. Provides `LoadOrDefault()` and `Save()`.

### MatchPlayerRegistry (static class)
In-memory dictionary mapping `clientId -> PlayerProfileData`. Populated by `NetworkLobbyManager` when game starts. Read by `NetworkGameManager` when constructing game-over UI. Cleared on lobby reset.

**Data flow:** PlayerPrefs -> LocalPlayerProfile -> SubmitProfileRpc -> NetworkLobbyManager._players -> MatchPlayerRegistry -> GameOverRpc handler

## NetworkDisconnectHandler

**File:** `Networking/NetworkDisconnectHandler.cs`

Simple MonoBehaviour that listens for the local client's disconnect event. If the local client (non-server) disconnects, loads the main menu scene. This handles cases where the host shuts down or the connection drops.

### Suggestion
- Consider consolidating with `NetworkLobbyManager`'s disconnect handling to avoid overlapping callbacks
- Should handle host disconnect more gracefully (currently host disconnecting just drops clients to main menu without explanation)
