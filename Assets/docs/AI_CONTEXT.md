# Puck'd - AI Context Guide

Quick-reference for AI models working on this codebase. Read this first, then dive into specific docs as needed.

## What Is This Project?

A multiplayer hockey-themed card game built in Unity, similar to Exploding Kittens. 2-4+ players draw cards, play action cards, and try to avoid the "Puck'd" elimination card. Last player standing wins. Uses Unity Netcode for GameObjects with Unity Relay for peer-to-peer networking (host is authoritative).

## Project Status: ~70% Complete

**Working:** Networking (host/join via Relay), lobby (profiles, ready, start), full server-side game logic (deck building, dealing, card play, effects, turns, elimination, game over), game-over UI with rematch.

**Not working / Missing:**
- No in-game UI (players can't see or play their cards)
- Puck'd cards are played from hand, not drawn from deck (fundamental game flow difference from Exploding Kittens)
- No reactive card play (can't play Goalie Save in response to elimination)
- No discard pile recycling when deck empties
- Players can only play 1 card per turn (should be able to play multiple before drawing)
- No "end turn / draw" button (draw is automatic after every play)

## Architecture At A Glance

```
UI (MonoBehaviours) -> Networking (NetworkBehaviours, RPCs) -> Gameplay (pure C#) -> Cards/Effects (ScriptableObjects)
```

- **Gameplay layer has ZERO Unity.Netcode dependency** - keep it this way
- **Server-authoritative**: all state in CoreGameManager, clients request via RPC, receive snapshots
- **ScriptableObject data model**: cards, effects, decks, configs are all assets
- **Effect stack (LIFO)**: effects queue on a stack and resolve last-in-first-out

## Key Files to Know

| File | What It Does |
|------|-------------|
| `Gameplay/CoreGameManager.cs` | THE game. Init, PlayCard, HandleEndOfTurn, CheckForGameOver. Server-only. |
| `Gameplay/GameActionExecutor.cs` | Applies resolved actions (elimination, extra turns, peek, etc.) to state |
| `Gameplay/TurnManager.cs` | Turn rotation, extra turns from attacks, skip |
| `Gameplay/DeckManager.cs` | Draw pile, discard pile, shuffle, deck building from definition |
| `Networking/NetworkGameManager.cs` | RPC bridge. Maps clientId<->playerId. Validates plays, syncs hands. |
| `Networking/NetworkLobbyManager.cs` | Lobby lifecycle: connect, profile, ready, start, reset |
| `Effects/EffectResolver.cs` | Stack-based effect resolution -> GameAction list |
| `Cards/DeckDefinition.cs` | Data asset: deck composition rules with weighted save variants |
| `UI/GameUIController.cs` | Game-over panel only. Singleton. |

## How a Card Play Works (Server-Side)

1. Client sends `RequestPlayCardRpc(cardInstanceId)` to server
2. `NetworkGameManager` validates: correct player, correct turn, card exists in hand
3. Calls `CoreGameManager.PlayCard(playerId, cardInstanceId)`
4. Card removed from hand, `EffectContext` created
5. Card's effects pushed onto `EffectResolver` stack
6. `ResolveAll()` pops stack -> `List<GameAction>`
7. `GameActionExecutor.ApplyActions()` mutates game state
8. `HandleEndOfTurn()`: advance turn, draw cards for new player, check game over
9. Server syncs all hands and broadcasts turn change via RPCs

## Namespaces -> Folders

| Namespace | Folder | Contents |
|-----------|--------|----------|
| `Actions` | Actions/ | ActionType enum, GameAction struct |
| `Cards` | Cards/ | CardCategory, CardDefinition, CardInstance, DeckDefinition |
| `Effects.Base` | Effects/Base/ | CardEffect (abstract), EffectContext, EffectResult |
| `Effects.Implementations` | Effects/Implementations/ | 7 concrete effects |
| `Effects` | Effects/ | EffectResolver, PendingEffect |
| `Gameplay` | Gameplay/ | CoreGameManager, all game logic classes |
| `Networking` | Networking/ | NetworkGameManager, lobby, relay, profiles |
| `Networking.Snapshots` | Networking/Snapshots/ | INetworkSerializable snapshot structs |
| `UI` | UI/ | All MonoBehaviour UI controllers |
| `Utility` | Utility/ | SerializableStringList |

## Common Patterns

- **ScriptableObject assets** for all game data (cards, effects, decks, configs)
- **Events/delegates** for communication between layers (not Unity Events)
- **Snapshot pattern** for network sync (not continuous NetworkVariables)
- **GameContext** as dependency container passed to gameplay components
- **RPC validation** on server before executing any game action
- **Phase enums** for state machines (GamePhase, LobbyPhase)

## Gotchas

1. **CoreGameManager is not a MonoBehaviour** - it's a plain class, created with `new`. Don't try to `GetComponent` or `FindObjectOfType` on it.
2. **EffectResolver IS a MonoBehaviour** - it must exist in the scene and be referenced in the inspector on NetworkGameManager.
3. **Extra turns are attributed to current player**, not next. `AddExtraTurnsForNextPlayer` is misleading - the extra turns are consumed by whoever `EndTurn()` would advance to.
4. **SyncHandRpc data is received but not stored** - clients get hand data but there's no client-side state management to hold it. This is the main gap for building the game UI.
5. **NotifyTurnChangedRpc is SendTo.NotServer** - the host doesn't receive its own turn notifications. The host needs to track turns locally too.
6. **Played cards vanish** - `CoreGameManager.PlayCard()` removes from hand but doesn't call `DeckManager.Discard()`. Cards are lost.
7. **CardPlayRequest struct is unused** - the actual play flow uses `RequestPlayCardRpc(int cardInstanceId)` directly.
8. **EffectResult struct is unused** - the resolver builds GameActions directly without using EffectResult.
9. **LobbyPhase.WaitingForPlayers is never set** - the lobby starts in ReadyUp and never transitions to WaitingForPlayers.

## Documentation Index

| Document | Contents |
|----------|----------|
| [ARCHITECTURE.md](ARCHITECTURE.md) | Layer overview, tech stack, file structure, what's missing |
| [GAME_FLOW.md](GAME_FLOW.md) | Full lifecycle from main menu through game over, all cases |
| [NETWORKING.md](NETWORKING.md) | Relay, lobby, game RPCs, snapshots, profiles |
| [GAMEPLAY.md](GAMEPLAY.md) | Core game logic, turns, deck management, action execution |
| [CARDS_AND_EFFECTS.md](CARDS_AND_EFFECTS.md) | Card system, effect stack, all 7 effects, deck definition |
| [UI_LAYER.md](UI_LAYER.md) | Main menu, lobby UI, game-over, what UI is missing |
| [EDITOR_TOOLS.md](EDITOR_TOOLS.md) | Custom inspectors, asset workflows, creating new effects |
