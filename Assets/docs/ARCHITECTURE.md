# Puck'd - Architecture Overview

Puck'd is a multiplayer hockey-themed card game built in Unity, inspired by Exploding Kittens. Players draw cards, play action cards, and try to avoid the "Puck'd" elimination card. Last player standing wins.

## Technology Stack

- **Unity** (6000.x) with **Netcode for GameObjects** for multiplayer
- **Unity Relay** for NAT traversal / matchmaking (no dedicated server)
- **Unity Transport (UTP)** as the network transport layer
- **TextMeshPro** for UI text rendering

## Layer Architecture

The codebase is organized into six layers with clear responsibilities:

```
+------------------------------------------------------+
|                    UI Layer                           |
|  MainMenuController, LobbyUIController,              |
|  GameUIController, ColorPicker, LobbyPlayerRowUI     |
+------------------------------------------------------+
          |                          |
          v                          v
+-------------------------+  +-------------------------+
|   Networking Layer      |  |   Editor Layer          |
|   NetworkLobbyManager   |  |   CardEditor            |
|   NetworkGameManager    |  |   DeckDefinitionEditor  |
|   RelayBootstrap        |  |   EffectEditor          |
|   Snapshots, Profiles   |  |   ScriptableObjSearch   |
+-------------------------+  +-------------------------+
          |
          v
+------------------------------------------------------+
|                 Gameplay Layer                        |
|  CoreGameManager, GameActionExecutor,                |
|  TurnManager, DeckManager, GameContext               |
+------------------------------------------------------+
          |
          v
+------------------------------------------------------+
|              Card & Effect System                     |
|  CardDefinition, DeckDefinition, CardInstance,       |
|  CardEffect (base), EffectResolver,                  |
|  7 Effect implementations                            |
+------------------------------------------------------+
          |
          v
+------------------------------------------------------+
|              Actions / Data Layer                     |
|  ActionType, GameAction, CardCategory,               |
|  EffectContext, PendingEffect                         |
+------------------------------------------------------+
```

### Layer Responsibilities

| Layer | Current State | Goal State |
|-------|--------------|------------|
| **UI** | Main menu, lobby UI, and game-over panel work. No in-game card hand UI, no turn indicator, no peek display, no elimination animations. | Full game HUD: card hand display, turn indicator, peek overlay, elimination/cancel animations, card play interactions. |
| **Networking** | Lobby lifecycle (join/leave/ready/start) is complete. Game RPCs for hand sync, turn changes, elimination, peek, and game-over all exist. Hand sync data arrives on clients but is not consumed by UI. | Client-side hand state management that feeds into the game HUD. Reconnection support. Spectator mode. |
| **Gameplay** | Core game loop is fully functional server-side: deck building, dealing, card play, effect resolution, turn management, elimination, game-over detection. | Reactive card plays (playing GoalieSave in response to Puck'd), targeting for attacks, discard pile recycling when draw pile empties. |
| **Card & Effect** | 7 card categories and 7 matching effects implemented. Deck definition system with weighted save variants. Cards are ScriptableObject assets. | More card types, combo effects, conditional effects. Targeting system for directed attacks. |
| **Actions/Data** | All core action types defined. Structs are lightweight and serializable. | Stable - unlikely to need changes unless new action types are added. |
| **Editor** | Custom inspectors for cards, decks, and effects with search, clone, and create workflows. | Stable - extends naturally as new effect types are added. |

## Key Architectural Decisions

### Server-Authoritative Design
All game state mutations happen on the server (host). Clients send requests via RPCs and receive state updates via snapshots. The `CoreGameManager` class has zero networking dependencies - it is a pure C# class that could be unit tested independently.

### ScriptableObject Data Model
Cards, effects, decks, and game configs are all ScriptableObject assets. This means:
- Game designers can create/edit cards in the Unity Inspector without code changes
- Cards are composed of modular effects (a card can have multiple effects)
- Deck composition rules are data-driven with weighted variant selection

### Effect Stack (LIFO)
Effects are processed using a stack, similar to Magic: The Gathering's stack. When a card is played, its effects are pushed onto a stack and resolved last-in-first-out. The `CancelLastEffect` action removes the most recent resolved action, simulating a "Nope" card.

### Snapshot-Based Network Sync
Rather than using NetworkVariables or continuous sync, the game uses explicit snapshot structs (`HandSnapshot`, `LobbyStateSnapshot`, `PeekSnapshot`) sent via RPCs at specific moments (card play, turn change, lobby update). This is bandwidth-efficient for a turn-based game.

### No Scene Separation for Lobby vs Game
The lobby and game exist in the same scene. `NetworkLobbyManager` and `NetworkGameManager` are both NetworkBehaviours in the lobby scene. When the game starts, the lobby transitions its phase to `InGame` and the game manager takes over. When the game ends, the lobby resets to `ReadyUp`.

## Project File Structure

```
Assets/
  Actions/
    ActionType.cs          - Enum: 7 game action types
    GameAction.cs          - Struct: action + value + context
  Cards/
    CardCategory.cs        - Enum: 7 card categories
    CardDefinition.cs      - ScriptableObject: card template
    CardInstance.cs         - Struct: runtime card with unique ID
    CardPlayRequest.cs     - Struct: card play request data
    DeckDefinition.cs      - ScriptableObject: deck composition rules
  Effects/
    Base/
      CardEffect.cs        - Abstract ScriptableObject: effect template
      EffectContext.cs      - Struct: who played what on whom
      EffectResult.cs       - Struct: wrapper for GameAction result
    Implementations/
      AttackEffect.cs      - Forces extra turns on next player
      CancelEffect.cs      - Cancels the last resolved effect
      EliminationEffect.cs - Requests player elimination
      PeekEffect.cs        - Peek at top N cards of draw pile
      PreventEliminationEffect.cs - Prevents an elimination
      ShuffleEffect.cs     - Shuffles the draw pile
      SkipEffect.cs        - Skips the current player's turn
    EffectResolver.cs      - MonoBehaviour: stack-based effect resolution
    PendingEffect.cs       - Struct: effect queued for resolution
  Gameplay/
    CoreGameManager.cs     - Pure C#: server-side game orchestration
    DeckManager.cs         - Pure C#: draw/discard pile management
    GameActionExecutor.cs  - Pure C#: applies actions to game state
    GameConfig.cs          - ScriptableObject: match configuration
    GameContext.cs          - Pure C#: dependency container
    GamePhase.cs           - Enum: None, Setup, InGame, GameOver
    PlayerRuntime.cs       - Pure C#: per-player runtime state
    TurnManager.cs         - Pure C#: turn order and advancement
  Networking/
    NetworkDisconnectHandler.cs - MonoBehaviour: client disconnect -> main menu
    NetworkGameManager.cs  - NetworkBehaviour: game RPC bridge
    NetworkLobbyManager.cs - NetworkBehaviour: lobby lifecycle
    PlayerProfileData.cs   - Struct + static helpers: player identity
    RelayBootstrap.cs      - Static: Unity Relay setup
    Snapshots/
      HandSnapshot.cs      - INetworkSerializable: player hand state
      LobbyStateSnapshot.cs - INetworkSerializable: full lobby state
      PeekSnapshot.cs      - INetworkSerializable: peek card results
  UI/
    ColorPicker.cs         - MonoBehaviour: HSV color picker
    GameUIController.cs    - MonoBehaviour: game-over panel (singleton)
    LobbyPlayerRowUI.cs    - MonoBehaviour: single lobby player row
    LobbyUIController.cs   - MonoBehaviour: lobby screen
    MainMenuController.cs  - MonoBehaviour: main menu + profile editing
  Editor/
    CardEditor.cs          - Custom inspector for CardDefinition
    DeckDefinitionEditor.cs - Custom inspector for DeckDefinition
    EffectEditor.cs        - Custom inspector for CardEffect
    ScriptableObjectSearchPopup.cs - Reusable asset search window
  Utility/
    SerializableStringList.cs - Network-serializable string array wrapper
```

## Dependency Graph (Namespaces)

```
UI -----> Networking (for profile data, lobby manager, relay bootstrap)
UI -----> Unity.Netcode (for host/client checks)

Networking -----> Gameplay (CoreGameManager, GamePhase, PlayerRuntime)
Networking -----> Cards (CardDefinition)
Networking -----> Effects (EffectResolver)
Networking -----> Networking.Snapshots
Networking -----> Utility

Gameplay -----> Cards (CardDefinition, CardInstance, DeckDefinition)
Gameplay -----> Effects (EffectResolver, CardEffect)
Gameplay -----> Effects.Base (EffectContext)
Gameplay -----> Actions (GameAction, ActionType)

Effects -----> Effects.Base
Effects -----> Actions (ActionType)

Actions -----> Effects.Base (EffectContext - for GameAction.Context)

Cards -----> Effects.Base (CardEffect - for CardDefinition.effects)
```

Note: `CoreGameManager` (Gameplay) has **zero** dependency on `Unity.Netcode`. This is intentional and should be preserved - it keeps the core game logic testable and reusable.

## What's Missing (Current vs Complete)

### Critical Path to Playable
1. **In-game card hand UI** - Players cannot see or interact with their cards
2. **Card play interaction** - No way for players to select and play a card from their hand
3. **Turn indicator UI** - No visual showing whose turn it is
4. **Draw card trigger** - The end-of-turn draw happens server-side but clients have no "end turn" / "draw" button
5. **Puck'd card draw trigger** - Drawing a Puck'd card from the deck should trigger elimination (currently only happens when played)

### Important but Not Blocking
6. **Reactive card play** - GoalieSave should be playable in response to drawing a Puck'd, not proactively
7. **Peek results display** - PeekSnapshot arrives on clients but nothing renders it
8. **Elimination announcement** - PlayerEliminatedRpc logs but has no UI
9. **Card targeting** - Attack effects currently lack a target selection mechanism
10. **Discard pile recycling** - When draw pile empties, nothing happens (should shuffle discard into draw)

### Nice to Have
11. **Reconnection support** - Disconnected players are eliminated; no rejoin
12. **Spectator mode** - Eliminated players see nothing
13. **Animation/VFX** - No card play animations, elimination effects, etc.
14. **Sound** - No audio system
15. **Card art** - CardDefinition supports Sprite artwork but likely placeholder or missing
