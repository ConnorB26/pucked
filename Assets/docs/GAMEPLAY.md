# Puck'd - Gameplay Layer

## Overview

The gameplay layer contains all core game logic. It is intentionally free of Unity networking types (`Unity.Netcode`), making it testable and reusable. Only `EffectResolver` is a MonoBehaviour (it needs to exist in the scene); all other classes are pure C#.

**Namespace:** `Gameplay`

## Files

| File | Type | Purpose |
|------|------|---------|
| `CoreGameManager.cs` | Class | Central game orchestration (server-only) |
| `GameActionExecutor.cs` | Class | Applies resolved actions to game state |
| `TurnManager.cs` | Class | Turn order, skips, and extra turns |
| `DeckManager.cs` | Class | Draw pile, discard pile, shuffling |
| `GameContext.cs` | Class | Dependency container for game components |
| `GameConfig.cs` | ScriptableObject | Match configuration |
| `PlayerRuntime.cs` | Class | Per-player runtime state |
| `GamePhase.cs` | Enum | None, Setup, InGame, GameOver |

## CoreGameManager

**File:** `Gameplay/CoreGameManager.cs`

The central orchestrator for a single match. Runs exclusively on the server/host. Has no dependency on `Unity.Netcode`.

### Lifecycle
```
new CoreGameManager()
  -> Init(config, resolver, players)
       Phase: None -> Setup -> InGame
  -> PlayCard() / HandlePlayerLeft() (during game)
  -> CheckForGameOver()
       Phase: InGame -> GameOver
```

### Properties
- `Phase` (GamePhase) - Current match lifecycle phase
- `Players` (List\<PlayerRuntime>) - All players in the match
- `TurnManager` - Convenience property from GameContext

### Events
| Event | Signature | When |
|-------|-----------|------|
| `PlayerEliminated` | `Action<int>` (playerId) | A player is eliminated (from play or disconnect) |
| `PeekRequested` | `Action<int, List<CardDefinition>>` (playerId, cards) | A player peeks at top deck cards |
| `GameOver` | `Action<int>` (winnerId) | Game ends, winner determined |

### Init(config, resolver, players)
1. Validates arguments (throws on null)
2. Creates `DeckManager` and initializes from `config.deckDefinition`
3. Saves reserved starting saves
4. Creates `TurnManager` and `GameContext`
5. Creates `GameActionExecutor` and wires its events to this class's events
6. Calls `DealStartingHands()`
7. Phase -> `InGame`

### DealStartingHands()
1. Gives each player one reserved save card (if available)
2. Draws from deck until each player reaches `startingHandSize`
3. If deck runs out mid-deal, remaining players get fewer cards

### PlayCard(playerId, cardInstanceId)
1. Guards: phase must be `InGame`, player must exist and not be eliminated
2. Finds card in player's hand by instance ID
3. Removes card from hand
4. Creates `EffectContext` with owner=playerId, target=0, cardId=instanceId
5. `EffectResolver.QueueEffects()` pushes card's effects onto stack
6. `EffectResolver.ResolveAll()` processes stack -> list of `GameAction`
7. `GameActionExecutor.ApplyActions()` mutates game state
8. `HandleEndOfTurn()` advances turn and draws

### HandleEndOfTurn()
1. `TurnManager.EndTurn()` - advances turn (or consumes extra turn)
2. If `drawAtEndOfTurn`: the *new* current player draws `drawPerTurn` cards
3. `CheckForGameOver()`

### HandlePlayerLeft(playerId)
1. Guard: phase `InGame`, player exists, not already eliminated
2. Returns player's hand cards to draw pile and shuffles
3. Marks eliminated
4. `TurnManager.OnPlayerEliminated()`
5. Fires `PlayerEliminated` event
6. `CheckForGameOver()`

### CheckForGameOver()
- Only acts if `lastPlayerStandingWins` is enabled
- Counts alive players
- If <= 1: Phase -> `GameOver`, fires event with winner ID (or -1)

### Suggestions for Improvement
- **Draw-to-end-turn model** - Currently, playing a card auto-ends the turn. Exploding Kittens allows playing multiple cards then drawing to end. Need a separate "end turn" / "draw" action.
- **Puck'd-from-deck trigger** - Drawing a Puck'd card from the deck should trigger the elimination flow with a response window for Goalie Save. Currently Puck'd only works when played from hand.
- **Reactive card plays** - Need a system where specific cards can be played in response to events (Goalie Save in response to elimination, Cancel in response to another player's card).
- **Discard pile recycling** - When draw pile empties, should shuffle discard pile into draw pile. Currently the draw just returns null.
- **Multi-card turns** - Allow playing multiple cards in a single turn before ending/drawing.

## GameActionExecutor

**File:** `Gameplay/GameActionExecutor.cs`

Takes a list of resolved `GameAction` instances and applies them to the `GameContext`. Exposes events for UI/network hooks.

### Events
| Event | Signature | When |
|-------|-----------|------|
| `OnPeekRequested` | `PeekHandler(int playerId, List<CardDefinition> cards)` | Peek action resolved |
| `OnPlayerEliminated` | `EliminationHandler(int playerId)` | Player eliminated |

### Action Handling

| ActionType | Behavior |
|-----------|----------|
| `RequestElimination` | Marks player eliminated, optionally discards hand, fires event |
| `PreventElimination` | No-op (logged). Intended to be handled in resolver/stack logic |
| `ForceExtraTurns` | `TurnManager.AddExtraTurnsForNextPlayer(value)` |
| `SkipTurn` | `TurnManager.SkipCurrentPlayer()` |
| `PeekCards` | `DeckManager.PeekTop(value)`, converts to definitions, fires event |
| `ShuffleDeck` | `DeckManager.Shuffle()` |
| `CancelLastEffect` | Logged as unhandled (handled in EffectResolver before reaching here) |

### HandleEliminationRequest(ownerId, targetId)
- If target is 0, victim = owner (self-elimination, like Exploding Kittens)
- Guards against already-eliminated or null player
- Sets `IsEliminated = true`
- Calls `TurnManager.OnPlayerEliminated()`
- If `discardHandOnElimination`: discards entire hand via `DeckManager.DiscardMany()`
- Fires `OnPlayerEliminated` event

### Suggestions for Improvement
- **PreventElimination** should do something (cancel a pending elimination). Currently it's a no-op because the reactive system doesn't exist.
- **Action ordering** - Actions are applied sequentially. If RequestElimination and PreventElimination both appear in the list, they should interact. Currently they don't.
- **Discard played cards** - Played cards are removed from hand but not added to discard pile. They vanish.

## TurnManager

**File:** `Gameplay/TurnManager.cs`

Manages turn order with support for extra turns (attacks) and eliminated player skipping.

### State
- `_players` - Reference to player list (same object as CoreGameManager's)
- `_currentIndex` - Index into player list for current turn
- `_pendingExtraTurns` - Counter for attack-granted extra turns

### Properties
- `CurrentPlayerId` - The playerId of the current turn's player

### Methods

**`EndTurn()`**
- If `_pendingExtraTurns > 0`: decrements, same player goes again
- Otherwise: `AdvanceToNextAlivePlayer()`

**`SkipCurrentPlayer()`**
- Immediately advances to next alive player (ignores extra turns)

**`AddExtraTurnsForNextPlayer(int turns)`**
- Adds to `_pendingExtraTurns` counter
- These are consumed by the current player on subsequent `EndTurn()` calls

**`OnPlayerEliminated(int playerId)`**
- Finds player by ID, marks as eliminated
- If that player was current, advances to next alive player

**`AdvanceToNextAlivePlayer()`**
- Circular scan: `(_currentIndex + 1) % count`
- Skips eliminated players
- If no alive players found after full loop: logs warning

### Suggestions for Improvement
- **Extra turn attribution** - `_pendingExtraTurns` is consumed by the *current* player after an attack is played. But `AddExtraTurnsForNextPlayer` implies they're for the *next* player. This works because `EndTurn()` is called after the attack is applied, but the naming is misleading. The extra turns are actually consumed by whoever `EndTurn()` would advance to.
- **Turn history** - No record of past turns. Could be useful for UI and debugging.
- **Skip interaction with extra turns** - Playing Skip immediately advances, but doesn't clear pending extra turns. Behavior when Skip and Attack interact should be defined.

## DeckManager

**File:** `Gameplay/DeckManager.cs`

Manages draw and discard piles using `CardInstance` structs. Builds the deck from a `DeckDefinition` asset.

### State
- `_drawPile` - List\<CardInstance>, top of deck = end of list
- `_discardPile` - List\<CardInstance>
- `_rng` - System.Random for shuffling and variant selection
- `_nextInstanceId` - Auto-incrementing unique ID per card instance

### Initialization

`InitializeFromDeckDefinition(definition, playerCount, shuffle, out startingSaves)`:
1. Clears both piles, resets ID counter
2. **Non-save cards:** Iterates `definition.EnumerateBaseCardCounts()`, creates instances for each (card, count) pair. Skips cards whose category matches `saveCategory`.
3. **Save cards:** Calls `GenerateSaveInstances()`:
   - Count = `playerCount + floor(playerCount * extraSavesPerPlayerRatio)`
   - For each save: picks a variant using weighted random (`PickVariant()`)
   - Normalizes weights as relative values (ignores the "must sum to 100" constraint)
4. **Reserves** up to 1 save per player in `startingSaves` output
5. **Remaining saves** go into draw pile
6. **Shuffles** draw pile if `shuffle` parameter is true

### Core Operations

| Method | Behavior |
|--------|----------|
| `Shuffle()` | Fisher-Yates shuffle on draw pile |
| `DrawTop()` | Removes and returns last element (top of deck). Returns null if empty. |
| `PeekTop(count)` | Returns top N cards without removing them. |
| `Discard(instance)` | Adds to discard pile |
| `DiscardMany(instances)` | Adds collection to discard pile |
| `ReturnCardsToDrawAndShuffle(instances)` | Adds cards back to draw pile and shuffles. Used for disconnected players. |

### Instance ID Generation
Each `CardInstance` gets a unique ID via `_nextInstanceId++`. IDs are unique within a match but not across matches. The network layer uses these IDs to reference specific cards in RPCs.

### Weighted Variant Selection
`PickVariant()` uses cumulative weight comparison:
1. Roll random double in [0, totalWeight)
2. Accumulate weights until roll <= accumulated
3. Return that variant

### Suggestions for Improvement
- **Discard pile recycling** - When `DrawTop()` returns null (empty deck), should shuffle discard pile into draw pile. Currently the game just stops drawing.
- **Played cards not discarded** - `CoreGameManager.PlayCard()` removes cards from hand but doesn't call `DeckManager.Discard()`. Played cards disappear from the game.
- **Deterministic RNG** - `System.Random` is initialized with default seed. For replay systems or testing, should accept a seed parameter.
- **Thread safety** - Not thread-safe, but this is fine since Unity is single-threaded and only the server accesses it.

## GameConfig

**File:** `Gameplay/GameConfig.cs`

ScriptableObject asset configuring match rules. Referenced by `NetworkGameManager` in the inspector.

### Fields
| Field | Default | Purpose |
|-------|---------|---------|
| `startingHandSize` | 5 | Cards dealt to each player at start |
| `deckDefinition` | (asset ref) | Which deck definition to build from |
| `drawAtEndOfTurn` | true | Whether to auto-draw after turn |
| `drawPerTurn` | 1 | Cards drawn per turn end |
| `lastPlayerStandingWins` | true | Win condition: last alive player |
| `discardHandOnElimination` | true | Whether eliminated player's cards go to discard |
| `disableShuffle` | false | Debug: keeps deck in defined order |

## GameContext

**File:** `Gameplay/GameContext.cs`

Simple dependency container passed to components that need access to shared game state. Holds readonly references to Config, DeckManager, TurnManager, and Players.

### Helper
- `GetPlayer(int playerId)` - Finds player by ID using `List.Find()`. Returns null if not found.

### Suggestion
- `GetPlayer()` does a linear scan. For larger player counts, a dictionary would be more efficient, though for 2-6 players the current approach is fine.

## PlayerRuntime

**File:** `Gameplay/PlayerRuntime.cs`

Minimal per-player runtime state.

### Fields
| Field | Type | Purpose |
|-------|------|---------|
| `PlayerId` | int (readonly) | Unique player identifier for this match |
| `IsEliminated` | bool | Whether player has been eliminated |
| `Hand` | List\<CardInstance> | Cards currently in player's hand |

### Suggestion
- Could track additional state: total cards played, turns taken, etc. for scoring/stats
- `IsEliminated` is mutable from multiple places (GameActionExecutor, TurnManager, CoreGameManager). Consider centralizing elimination logic.

## GamePhase

**File:** `Gameplay/GamePhase.cs`

```csharp
enum GamePhase { None, Setup, InGame, GameOver }
```

Tracks the lifecycle of a `CoreGameManager` instance. Transitions are managed by `CoreGameManager` only.
