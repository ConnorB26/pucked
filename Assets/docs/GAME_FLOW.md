# Game Flow

## Connection & Lobby

### Hosting

1. Player enters a name, picks a color, and presses **Host**.
2. `RelayBootstrap.StartHostWithRelay()` initializes Unity Services, authenticates anonymously, creates a Relay allocation, and starts the NetworkManager as host.
3. A join code is generated and displayed. The game scene loads and the lobby UI appears.

### Joining

1. Player enters a name, picks a color, enters the join code, and presses **Join**.
2. `RelayBootstrap.StartClientWithRelay()` joins the Relay allocation using the code and starts the NetworkManager as client.
3. The client follows the host to the current scene automatically.

### Lobby Sync

When a client connects, they submit their profile via `SubmitProfileRpc` (client -> server). The server stores it and broadcasts a `LobbyStateSnapshot` to all clients whenever lobby state changes (join, leave, ready toggle). Each client rebuilds the player list UI from the snapshot.

### Starting the Game

1. Host presses **Start Game** -> `RequestStartGameRpc`.
2. Server validates all players are ready.
3. Server maps each clientId to a sequential playerId (0, 1, 2...).
4. Server creates `CoreGameManager` (plain C#, not a MonoBehaviour) and calls `Init()`:
   - Builds the deck from `DeckDefinition` (Goalie Save count scales with player count, Puck'd count = players - 1).
   - Deals starting hands: each player gets 1 reserved Goalie Save, then draws to `startingHandSize`. Puck'd cards are skipped during the deal (returned to deck, shuffled in after).
5. Server sends initial sync RPCs to all clients in order:
   - `AssignPlayerIdRpc` -- tells each client their playerId
   - `SyncHandRpc` -- each player's starting hand
   - `RegisterPlayerRpc` -- all player names and colors
   - `NotifyTurnChangedRpc` -- who goes first
   - `GameReadyRpc` -- sent last, signals the UI to show the game HUD

## Turn Structure

A turn has two phases: an optional play phase and a mandatory draw phase.

```
[Player's Turn Begins]
    |
    +-- Play Phase (optional, repeatable)
    |     Player plays a card -> server resolves effects
    |     Player can play multiple cards before ending turn
    |
    +-- Draw Phase (mandatory)
          Player clicks End Turn -> draws from deck
          Drawing may trigger Puck'd elimination
          Turn advances to next player
```

### Playing a Card

1. **Client** sends `RequestPlayCardRpc(cardInstanceId, targetPlayerId)` to server.
2. **Server** validates:
   - Game is in the `InGame` phase.
   - It is this player's turn.
   - The card exists in the player's hand.
3. **Server** calls `CoreGameManager.PlayCard()`:
   - Removes the card from the player's hand.
   - Creates an `EffectContext` (source player, target player, game context).
   - Queues the card's effects onto the `EffectResolver` stack.
   - `ResolveAll()` pops effects LIFO and produces a list of `GameAction` structs.
   - `GameActionExecutor.ApplyActions()` executes each action against game state.
4. **Server** broadcasts results:
   - `CardPlayedRpc(playerId, cardName, category)` -- notifies all clients.
   - `SyncHandRpc` -- updates the playing client's hand.
   - Action-specific RPCs (e.g., `PeekResultRpc` for Peek, `NotifyTurnChangedRpc` if Skip/Attack changed the turn).
5. **Server** checks for game over after every action.

### Ending a Turn (Drawing)

1. **Client** sends `RequestEndTurnRpc()` to server.
2. **Server** validates it is this player's turn.
3. **Server** calls `CoreGameManager.PlayerEndTurn()`:
   - Draws `drawPerTurn` cards (default 1) from the deck.
   - **If Puck'd is drawn:**
     - Check if player has a Goalie Save in hand.
     - **Yes:** Auto-consume the Goalie Save (remove from hand, discard), return Puck'd to a random position in the deck. Broadcast `GoalieSaveUsedRpc`.
     - **No:** Player is eliminated. Broadcast `PlayerEliminatedRpc`.
   - **If normal card drawn:** Add to player's hand.
   - Advance turn to next player.
4. **Server** syncs:
   - `SyncHandRpc` -- updated hand (card drawn or Goalie Save consumed).
   - `NotifyTurnChangedRpc` -- next player's turn.

## Card Effects in Detail

### Attack

Adds extra turns to the **next** player. When the current player ends their turn, the next player must take multiple consecutive turns (each ending with a draw). Extra turns decrement one at a time. The attacking player's turn ends immediately via skip.

### Skip

Ends the current player's turn without drawing. If the player has pending extra turns, the skip consumes one extra turn instead of advancing to the next player.

### Peek

Sends the top N card names/categories from the deck to the requesting player only via `PeekResultRpc`. Does not end the turn.

### Shuffle

Shuffles the draw pile (Fisher-Yates). Does not end the turn.

### Cancel

Removes the most recently resolved effect from the action list during stack resolution. Only works within the same card's effect stack.

### Goalie Save

Not manually playable. Auto-consumed reactively when a player draws a Puck'd card. The Puck'd card is returned to the deck at a random position and the deck is reshuffled.

### Puck'd

Not manually playable. Exists only in the draw pile. When drawn, triggers the Goalie Save check described above.

## Effect Resolution

Effects use a stack-based LIFO resolver:

1. A card's effects are queued onto the stack in list order.
2. `ResolveAll()` pops each effect and calls `Resolve(context)`, producing `GameAction` structs.
3. `CancelLastEffect` (from Cancel cards) removes the most recent action from the output list.
4. The final list of `GameAction` structs is passed to `GameActionExecutor.ApplyActions()`.

Action types: `SkipCurrentPlayer`, `AddExtraTurns`, `RequestElimination`, `PreventElimination`, `PeekAtDeck`, `ShuffleDeck`, `CancelLastEffect`.

## Client-Side State

Clients never run game logic. All state comes from server RPCs.

`LocalGameState` (static) holds:
- `LocalPlayerId` -- this client's player ID
- `CurrentTurnPlayerId` -- whose turn it is
- `Hand` -- list of cards (instanceId, name, category)
- `Players` -- dictionary of all players (name, color, elimination status)
- `IsMyTurn` / `IsGameActive` -- derived properties

`GameEvents` (static event bus) fires events that UI controllers subscribe to:
- `OnGameStarted` -- initial sync complete, show HUD
- `OnLocalHandUpdated` -- hand changed
- `OnTurnChanged(playerId)` -- turn advanced
- `OnCardPlayed(playerId, cardName, category)` -- any player played a card
- `OnPeekResult(cardNames[])` -- peek results received
- `OnPlayerEliminated(playerId)` -- player eliminated
- `OnGoalieSaveUsed(playerId)` -- Goalie Save blocked a Puck'd
- `OnGameOver(winnerId, winnerProfile)` -- game ended

**UI pattern:** RPC handler -> update `LocalGameState` -> fire `GameEvent` -> UI controller reacts.

## Sequence Diagram: Card Play

```
Client                    NetworkGameManager (Server)      CoreGameManager
  |                              |                              |
  |--RequestPlayCardRpc(cardId)->|                              |
  |                              |--validate turn & hand------->|
  |                              |      PlayCard(playerId, id)  |
  |                              |         remove from hand     |
  |                              |         queue effects        |
  |                              |         resolve all          |
  |                              |         apply actions        |
  |                              |<----events (elim, peek, etc)-|
  |<---CardPlayedRpc------------|                              |
  |<---SyncHandRpc--------------|                              |
  |<---NotifyTurnChangedRpc-----|  (if turn changed)           |
  |<---PeekResultRpc------------|  (if peek, targeted)         |
  |                              |                              |
  |--RequestEndTurnRpc---------->|                              |
  |                              |      PlayerEndTurn()         |
  |                              |         draw cards           |
  |                              |         check Puck'd         |
  |                              |         advance turn         |
  |<---SyncHandRpc--------------|                              |
  |<---NotifyTurnChangedRpc-----|                              |
  |<---PlayerEliminatedRpc------|  (if Puck'd drawn, no save)  |
  |<---GameOverRpc--------------|  (if 1 player left)          |
```

## Elimination & Game Over

When a player is eliminated:
1. `PlayerRuntime.isEliminated` is set to true.
2. `TurnManager.OnPlayerEliminated()` removes them from the turn order.
3. If the eliminated player was the current turn holder, the turn advances.
4. `PlayerEliminatedRpc` is broadcast to all clients.
5. Game-over check: if only 1 player remains, `GameOverRpc(winnerId)` is broadcast and the game-over panel shows the winner.

## Rematch & Disconnect

- **Rematch:** Host clicks Rematch on the game-over panel. Server resets `CoreGameManager`, re-deals hands, and re-syncs all clients. Lobby returns to `ReadyUp` phase.
- **Close Lobby:** Host clicks Close Lobby, `NetworkManager.Shutdown()` disconnects everyone, all clients return to main menu.
- **Disconnect:** `NetworkDisconnectHandler` detects client disconnects. If mid-game, the player is eliminated, their cards return to the deck, and the turn advances if needed.
