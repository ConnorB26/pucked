# Puck'd - Game Flow & State Machine

This document covers the complete lifecycle of a Puck'd session, from launching the game through match completion, including all branching cases.

## High-Level State Machine

```
[Main Menu] --> [Hosting/Joining] --> [Lobby] --> [In-Game] --> [Game Over] --> [Lobby]
                                        ^                                        |
                                        +----------------------------------------+

[Main Menu] <-- disconnect (client) or close lobby (host)
```

## Phase 1: Main Menu

**Scene:** `MainMenuScene`
**Controller:** `MainMenuController`

### Player Profile
- On `Start()`, loads saved profile from `PlayerPrefs` via `LocalPlayerProfile.LoadOrDefault()`
- Default profile: random name like "Player 472", blue-ish color `(0.2, 0.7, 1.0)`
- Player can edit name and pick color via HSV `ColorPicker`
- Profile is saved to `PlayerPrefs` when hosting or joining

### Host Flow
1. Player clicks **Host**
2. `MainMenuController.OnClickHost()`:
   - Saves profile to `PlayerPrefs`
   - `RelayBootstrap.StartHostWithRelay(maxConnections)`:
     - Initializes Unity Services (if needed)
     - Signs in anonymously (if needed)
     - Creates Relay allocation
     - Configures UTP transport with host relay data
     - Gets join code from Relay
     - Starts `NetworkManager` as host
     - Stores `LastJoinCode` and `MaxConnections` statically
   - Loads lobby scene via `NetworkManager.SceneManager.LoadScene()`

### Join Flow
1. Player enters join code and clicks **Join**
2. `MainMenuController.OnClickJoin()`:
   - Saves profile to `PlayerPrefs`
   - `RelayBootstrap.StartClientWithRelay(joinCode)`:
     - Initializes Unity Services (if needed)
     - Signs in anonymously (if needed)
     - Joins Relay allocation with code
     - Configures UTP transport with client relay data
     - Starts `NetworkManager` as client
   - Client automatically follows host into the current scene (Netcode scene management)

### Error Cases
- **Host fails to create Relay allocation** - Logs error, stays on main menu
- **Client enters invalid join code** - `JoinAllocationAsync` throws, logs error, stays on main menu
- **Unity Services fail to initialize** - `EnsureServicesAsync` throws, caught by caller

## Phase 2: Lobby

**Scene:** `LobbyScene` (loaded by host, clients follow)
**Controllers:** `NetworkLobbyManager` (authority), `LobbyUIController` (display)

### Lobby Phases (server-side enum)
```
WaitingForPlayers --> ReadyUp --> InGame
                       ^            |
                       +---(reset)--+
```

The lobby starts in `ReadyUp` phase.

### Connection Flow

**When host spawns:**
1. `NetworkLobbyManager.OnNetworkSpawn()` (server path):
   - Sets phase to `ReadyUp`
   - Registers `OnClientConnected` / `OnClientDisconnected` callbacks
   - Adds host as first player with placeholder profile
2. `OnNetworkSpawn()` (client path, runs on host too):
   - Loads local profile from `PlayerPrefs`
   - Sends `SubmitProfileRpc` to server with name + color

**When a client connects:**
1. Server `OnClientConnected(clientId)`:
   - If game is in progress (`InGame` phase), **rejects** client via `DisconnectClient()`
   - Otherwise adds client with placeholder profile
2. Client sends `SubmitProfileRpc` with real profile data
3. Server updates profile and broadcasts `LobbyStateSnapshot` to all clients

**When a client disconnects from lobby:**
1. Server `OnClientDisconnected(clientId)`:
   - Removes player from dictionary
   - Broadcasts updated `LobbyStateSnapshot`

### Ready Flow
1. Player clicks **Ready** button
2. `LobbyUIController.OnClickReady()` toggles local state
3. Sends `ToggleReadyRpc(bool)` to server
4. Server updates player's ready flag
5. Server broadcasts `LobbyStateSnapshot`
6. All clients rebuild UI from snapshot:
   - Player rows show name, color swatch, ready/unready status
   - Host's **Start** button enables only when all players are ready

### Start Game Flow
1. Host clicks **Start Game** (only visible to host, only enabled when all ready)
2. `LobbyUIController.OnClickStartGame()` -> `NetworkLobbyManager.HostRequestStartGame()`
3. `RequestStartGameRpc` (owner-only permission) -> `TryStartGameOnServer()`:
   - Validates phase is `ReadyUp`
   - Validates all players are ready
   - Sets phase to `InGame`
   - Copies player profiles to `MatchPlayerRegistry` (static)
   - Finds `NetworkGameManager` in scene and calls `ServerStartGame()`
4. `NetworkGameManager.ServerStartGame()`:
   - Maps each connected clientId to a sequential playerId (0, 1, 2...)
   - Creates `CoreGameManager` and calls `Init(config, resolver, players)`
   - Subscribes to core events (elimination, peek, game over)
   - Sends `AssignPlayerIdRpc` to each client with their playerId
   - Syncs each player's starting hand via `SyncHandRpc`
   - Broadcasts `NotifyTurnChangedRpc` with first player's ID
5. Lobby broadcasts updated snapshot (phase = InGame)

## Phase 3: In-Game

**Authority:** `CoreGameManager` (server-only, pure C#)
**Network Bridge:** `NetworkGameManager` (RPCs)

### Game Initialization (Server-Side)

`CoreGameManager.Init()`:
1. Phase -> `Setup`
2. Creates `DeckManager` and builds deck from `DeckDefinition`:
   - Adds all non-save category cards (count defined per card in definition)
   - Generates save cards: `playerCount + floor(playerCount * extraSavesPerPlayerRatio)`
   - Save variants chosen by weighted random selection
   - Reserves 1 save per player for starting hands
   - Remaining saves shuffled into draw pile (Fisher-Yates)
3. Creates `TurnManager` with player list (starts at player 0)
4. Creates `GameContext` (bundles config + deck + turn + players)
5. Creates `GameActionExecutor` (reads/writes via GameContext)
6. Wires executor events -> core events -> network RPCs
7. Deals starting hands:
   - Each player gets 1 reserved save card
   - Then draws from deck until hand reaches `startingHandSize` (default 5)
8. Phase -> `InGame`

### Turn Structure

**Current implementation:** Players play one card per turn, then the turn advances and the *next* player draws.

```
[Player's Turn]
    |
    +--> Player plays a card (RequestPlayCardRpc)
    |        |
    |        +--> Server validates (correct player, correct turn, card in hand)
    |        +--> CoreGameManager.PlayCard():
    |                1. Remove card from hand
    |                2. Create EffectContext (owner, target=0, cardId)
    |                3. Queue effects on EffectResolver stack
    |                4. ResolveAll() -> list of GameActions
    |                5. GameActionExecutor.ApplyActions()
    |                6. HandleEndOfTurn()
    |
    +--> HandleEndOfTurn():
            1. TurnManager.EndTurn() (advance or consume extra turn)
            2. If drawAtEndOfTurn: new current player draws drawPerTurn cards
            3. CheckForGameOver()
```

**Note:** The current implementation has a design gap - players play a card and *then* the turn ends. In Exploding Kittens, the typical flow is: play any number of cards on your turn, then draw to end your turn. The draw is the dangerous action (you might draw the elimination card). Currently, the draw happens automatically after the turn advances, not as a player-initiated action.

### Card Play Cases

Each card category triggers different effects:

#### Puck'd (Elimination Card)
```
Player plays Puck'd
  -> EliminationEffect -> RequestElimination action
  -> GameActionExecutor.HandleEliminationRequest():
       - Victim = target (or self if target=0)
       - Mark player.IsEliminated = true
       - TurnManager.OnPlayerEliminated() (advance if current)
       - If discardHandOnElimination: discard hand to discard pile
       - Fire OnPlayerEliminated event
  -> Network: PlayerEliminatedRpc broadcast
  -> CheckForGameOver()
```

**Current gap:** Puck'd cards are currently played from hand like any other card. In Exploding Kittens, the elimination card is drawn from the deck, not played from hand. This is a fundamental game flow difference that needs addressing.

#### Goalie Save (Prevent Elimination)
```
Player plays Goalie Save
  -> PreventEliminationEffect -> PreventElimination action
  -> GameActionExecutor: logs but takes no state action
  -> Turn advances normally
```

**Current gap:** PreventElimination is a no-op in the executor. The intended flow is reactive: when a player draws a Puck'd card, they should have a window to play a Goalie Save to prevent elimination. This reactive card play system does not exist yet.

#### Cancel (Nope)
```
Player plays Cancel
  -> CancelEffect -> CancelLastEffect action
  -> EffectResolver: removes last action from output list
  -> Remaining actions applied normally
```

**Note:** Cancel currently only works within the same card's effect stack. Cross-turn cancellation (canceling another player's card play) is not implemented.

#### Attack
```
Player plays Attack
  -> AttackEffect -> ForceExtraTurns action (default: 2)
  -> GameActionExecutor: TurnManager.AddExtraTurnsForNextPlayer(2)
  -> On next EndTurn(), extra turns are consumed before advancing
```

**Behavior:** When the next player's turn ends, `_pendingExtraTurns` decrements. While > 0, the same player takes another turn instead of advancing. Once extra turns are exhausted, normal rotation resumes.

#### Skip
```
Player plays Skip
  -> SkipEffect -> SkipTurn action
  -> GameActionExecutor: TurnManager.SkipCurrentPlayer()
  -> Immediately advances to next alive player
```

#### Peek
```
Player plays Peek
  -> PeekEffect -> PeekCards action (default: 3)
  -> GameActionExecutor.HandlePeek():
       - DeckManager.PeekTop(3) -> top 3 CardInstances
       - Converts to CardDefinitions
       - Fires OnPeekRequested event
  -> Network: PeekResultRpc sent ONLY to peeking player's client
```

#### Shuffle
```
Player plays Shuffle
  -> ShuffleEffect -> ShuffleDeck action
  -> GameActionExecutor: DeckManager.Shuffle() (Fisher-Yates)
```

### Turn Advancement Logic

`TurnManager.EndTurn()`:
```
if _pendingExtraTurns > 0:
    _pendingExtraTurns--
    return (same player goes again)
else:
    AdvanceToNextAlivePlayer()
        -> circular scan: (_currentIndex + 1) % count
        -> skip eliminated players
        -> if all eliminated: log warning (shouldn't reach here)
```

### Player Disconnection (Mid-Game)

1. `NetworkManager.OnClientDisconnectCallback` fires on server
2. `NetworkGameManager.OnClientDisconnected(clientId)`:
   - Looks up playerId from clientId map
   - Removes from both mapping dictionaries
   - Calls `CoreGameManager.HandlePlayerLeft(playerId)`:
     - Returns player's hand cards to draw pile and shuffles
     - Marks player eliminated
     - `TurnManager.OnPlayerEliminated()` (advance if current)
     - Fires `PlayerEliminated` event
     - `CheckForGameOver()`
   - Syncs remaining players' hands
   - Broadcasts `PlayerEliminatedRpc`
   - If game still in progress: broadcasts turn change
   - If game over: logs (TODO: proper handling)

### Game Over Detection

`CoreGameManager.CheckForGameOver()`:
- Only runs if `lastPlayerStandingWins` is enabled
- Counts non-eliminated players
- If <= 1 alive:
  - Phase -> `GameOver`
  - Winner = last alive player's ID (or -1 if none)
  - Fires `GameOver` event

### Network: Game Over Flow

1. `NetworkGameManager.OnCoreGameOver(winnerPlayerId)`:
   - Sends `GameOverRpc(winnerPlayerId)` to **everyone** (including host)
   - Calls `ServerEndGame()` (unsubscribes events, clears mappings, resets state)
   - Finds `NetworkLobbyManager` and calls `ServerResetLobby()`
2. `GameOverRpc` on each client:
   - Determines if local player won
   - Looks up winner's profile from `MatchPlayerRegistry`
   - Shows `GameUIController.ShowGameOver(isWinner, winnerProfile, isHost)`

## Phase 4: Game Over / Post-Game

**Controller:** `GameUIController` (singleton)

### Display
- Game over panel shows "You won!" or "Player X wins!"
- Host sees **Rematch** and **Close Lobby** buttons
- Clients see nothing (no buttons)

### Rematch Flow
1. Host clicks **Rematch**
2. `GameUIController.OnClickRematch()`:
   - Finds `NetworkLobbyManager`, calls `ServerResetLobby()`
   - Hides game over panel
3. `ServerResetLobby()`:
   - Phase -> `ReadyUp`
   - All players' ready flags -> false
   - Clears `MatchPlayerRegistry`
   - Broadcasts `LobbyStateSnapshot`
4. Lobby UI rebuilds, players can ready up again

### Close Lobby Flow
1. Host clicks **Close Lobby**
2. `GameUIController.OnClickCloseLobby()`:
   - `NetworkManager.Singleton.Shutdown()` (disconnects all clients)
   - Loads main menu scene
3. On each client: `NetworkDisconnectHandler` detects disconnect, loads main menu scene

## State Diagram Summary

```
CoreGameManager.Phase:
  None ----[Init()]----> Setup ----[DealStartingHands()]----> InGame
  InGame --[CheckForGameOver(), 1 player left]-------------> GameOver

NetworkLobbyManager.LobbyPhase:
  ReadyUp ----[TryStartGameOnServer()]----> InGame
  InGame  ----[ServerResetLobby()]-------->  ReadyUp

GameUIController:
  Hidden  ----[ShowGameOver()]----> Visible
  Visible ----[OnClickRematch()]-> Hidden (lobby resets)
  Visible ----[OnClickCloseLobby()]-> Main Menu (network shutdown)
```

## Sequence Diagram: Full Card Play

```
Client                    Server (NetworkGameManager)       CoreGameManager
  |                              |                              |
  |--RequestPlayCardRpc(cardId)->|                              |
  |                              |--validate turn & hand------->|
  |                              |                              |
  |                              |      PlayCard(playerId, cardId)
  |                              |                              |
  |                              |         remove from hand     |
  |                              |         queue effects        |
  |                              |         resolve all          |
  |                              |         apply actions        |
  |                              |         handle end of turn   |
  |                              |         (advance turn, draw) |
  |                              |         check game over      |
  |                              |                              |
  |                              |<----events (elim, peek, etc)-|
  |                              |                              |
  |<---SyncHandRpc (all players)-|                              |
  |<---NotifyTurnChangedRpc------|                              |
  |<---PlayerEliminatedRpc-------|  (if applicable)             |
  |<---PeekResultRpc------------|  (if applicable, targeted)   |
  |<---GameOverRpc--------------|  (if applicable)             |
```
