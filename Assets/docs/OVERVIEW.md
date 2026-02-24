# Puck'd

Puck'd is a multiplayer hockey-themed card game built in Unity, inspired by Exploding Kittens. Players draw cards, play action cards to affect opponents, and try to avoid the "Puck'd" elimination card. The last player standing wins.

## Tech Stack

- **Engine:** Unity 6000.x (URP)
- **Networking:** Netcode for GameObjects + Unity Relay + Unity Transport (UTP)
- **UI:** Unity UI (uGUI) with TextMeshPro
- **Data:** ScriptableObjects for all game data (cards, effects, decks, config)

## Project Structure

```
Assets/
├── Actions/          ActionType enum + GameAction struct
├── Cards/            CardDefinition, CardInstance, DeckDefinition + card/effect assets
├── Effects/          CardEffect base, EffectResolver, 7 effect implementations
├── Gameplay/         Pure C# game logic (CoreGameManager, TurnManager, DeckManager)
├── Networking/       Relay setup, lobby management, game RPCs, client state
├── UI/               MonoBehaviour UI controllers for menus, lobby, and gameplay
├── Editor/           Custom inspectors for cards, decks, and effects
├── Scenes/           MainMenuScene + GameScene
├── Utility/          Helper types (SerializableStringList)
└── docs/             Documentation
```

## Architecture Layers

### Gameplay Layer

Pure C# with zero Netcode dependency. All game logic runs server-side only.

| Class | Role |
|---|---|
| `CoreGameManager` | Central orchestrator. Handles init, card plays, turn endings, eliminations, and game-over checks. Created with `new` (not a MonoBehaviour). |
| `TurnManager` | Turn order, extra turns from attacks, skip logic, elimination handling. |
| `DeckManager` | Draw/discard pile management. Builds deck from `DeckDefinition`, handles shuffling. |
| `GameActionExecutor` | Applies resolved `GameAction` list to game state (skip, attack, eliminate, etc.). |
| `PlayerRuntime` | Per-player state: hand, elimination status, pending extra turns. |
| `GameContext` | Dependency container passed through the gameplay layer. |
| `GameConfig` | ScriptableObject with match rules (starting hand size, draw count, etc.). |

### Card & Effect System

Data-driven card system using ScriptableObjects.

**Card Types:**
| Card | Effect | Description |
|---|---|---|
| Puck'd | Elimination | Drawn from deck, eliminates the player (like Exploding Kitten) |
| Goalie Save | Prevent Elimination | Auto-consumed when drawing Puck'd (like Defuse) |
| Attack | Extra Turns | Forces next player to take extra turns |
| Skip | Skip Turn | Skips the current player's turn |
| Peek | Peek | Look at the top N cards of the deck |
| Shuffle | Shuffle | Shuffles the draw pile |
| Cancel | Cancel Last Effect | Negates the previous effect on the stack (like Nope) |

**Resolution:** Effects use a stack-based (LIFO) resolver. When a card is played, its effects are queued onto the stack, resolved into `GameAction` structs, then applied by the executor.

**Deck Composition:** Defined by `DeckDefinition` asset. Goalie Save count scales with player count using a configurable ratio. Puck'd cards = playerCount - 1 (one fewer than players so one player survives).

### Networking Layer

Host-authoritative peer-to-peer using Unity Relay for NAT traversal.

| Class | Role |
|---|---|
| `RelayBootstrap` | Initializes Unity Services, creates/joins Relay allocations, configures transport. |
| `NetworkLobbyManager` | Lobby lifecycle: profile submission, ready state, game start. Broadcasts `LobbyStateSnapshot`. |
| `NetworkGameManager` | RPC bridge between clients and `CoreGameManager`. Validates requests, syncs state via RPCs. |
| `LocalGameState` | Static client-side state populated by RPCs. Tracks hand, turn, players, and game status. |
| `PlayerProfileData` | Player name + color, persisted to PlayerPrefs. |

**Snapshots** (INetworkSerializable):
- `HandSnapshot` — Card instance IDs, names, and categories for a player's hand
- `LobbyStateSnapshot` — All lobby state (players, ready flags, phase)
- `PeekSnapshot` — Top cards of deck (sent only to the peeking player)

### UI Layer

MonoBehaviour controllers that subscribe to `GameEvents` (static event bus) and read from `LocalGameState`.

| Class | Role |
|---|---|
| `MainMenuController` | Profile editing (name + color), host/join buttons. |
| `LobbyUIController` | Player list with ready toggles, start button (host only). |
| `GameUIController` | Game-over panel with winner display, rematch/close buttons. |
| `GameHUDController` | In-game HUD with turn indicator and end-turn button. |
| `CardHandController` | Card hand display and interaction. |
| `CardUI` | Individual card rendering. |
| `TargetSelectorController` | Player targeting for directed effects. |

### Editor Layer

Custom inspectors for content authoring.

| Class | Role |
|---|---|
| `CardEditor` | Add/create/remove effects on cards, clone card as variation. |
| `DeckDefinitionEditor` | Deck composition editing with validation and expected count preview. |
| `EffectEditor` | Create cards from effects, duplicate effects. |
| `ScriptableObjectSearchPopup` | Generic reusable asset picker window. |

## Status

The core gameplay, networking, and lobby systems are fully functional. The remaining work is UI implementation for in-game card interaction and minor feature enhancements.
