# Puck'd - Card & Effect System

## Overview

The card and effect system uses a modular, data-driven architecture. Cards are ScriptableObject assets that reference one or more effect assets. Effects are also ScriptableObjects that define what happens when a card is played. At runtime, effects are converted to `PendingEffect` structs and processed through a stack-based resolver.

**Namespaces:** `Cards`, `Effects`, `Effects.Base`, `Effects.Implementations`, `Actions`

## Architecture

```
CardDefinition (ScriptableObject asset)
    |
    +-- cardName, category, artwork, description
    +-- effects: List<CardEffect>
            |
            +-- CardEffect (abstract ScriptableObject asset)
                    |
                    +-- CreateRuntimeEffect(EffectContext) -> PendingEffect
                            |
                            +-- PendingEffect (struct)
                                    |
                                    +-- ActionType, INTPayload, Context
                                            |
                                            +-- EffectResolver.ResolveAll() -> List<GameAction>
                                                    |
                                                    +-- GameActionExecutor.ApplyActions()
```

## Cards

### CardCategory

**File:** `Cards/CardCategory.cs`

```csharp
enum CardCategory
{
    Puckd,       // Elimination card (like Exploding Kitten)
    GoalieSave,  // Prevent elimination (like Defuse)
    Cancel,      // Cancel another effect (like Nope)
    Attack,      // Force next player to take extra turns
    Skip,        // Skip current turn
    Peek,        // Look at top cards of deck
    Shuffle      // Shuffle the deck
}
```

Each category maps 1:1 to an effect type in the current implementation, but the system supports cards with multiple or different effects.

### CardDefinition

**File:** `Cards/CardDefinition.cs`

ScriptableObject asset representing a card template.

| Field | Type | Purpose |
|-------|------|---------|
| `cardName` | string | Display name |
| `category` | CardCategory | Card type classification |
| `artwork` | Sprite | Card art (for UI rendering) |
| `effects` | List\<CardEffect> | Modular effects this card triggers |
| `description` | string | Flavor/rules text |
| `variationIndex` | int | Distinguishes variants of the same card type |

**Key design:** A card can have multiple effects. When played, all effects are pushed onto the resolver stack. This allows combo cards (e.g., a card that both peeks and shuffles).

### CardInstance

**File:** `Cards/CardInstance.cs`

Lightweight runtime struct representing a specific copy of a card in play.

| Field | Type | Purpose |
|-------|------|---------|
| `InstanceId` | int | Unique per match, assigned by DeckManager |
| `Definition` | CardDefinition | Reference to the card template |

Instance IDs are used in network RPCs to identify specific cards in a player's hand without transmitting full card data.

### CardPlayRequest

**File:** `Cards/CardPlayRequest.cs`

Simple struct for passing play requests:
| Field | Type |
|-------|------|
| `CardId` | int |
| `PlayerId` | int |

Currently unused in the actual card play flow (the network layer uses `RequestPlayCardRpc` with just the instance ID and derives the player from the sender's client ID).

## Deck Definition System

### DeckDefinition

**File:** `Cards/DeckDefinition.cs`

ScriptableObject asset that defines how to build a deck for a match. Separates cards into two groups: save cards (special rules) and non-save cards (fixed counts).

#### Save Card Rules
| Field | Type | Purpose |
|-------|------|---------|
| `saveCategory` | CardCategory | Which category counts as a "save" (default: GoalieSave) |
| `extraSavesPerPlayerRatio` | float | Extra saves = floor(playerCount * ratio). Default 0.5 |
| `saveVariants` | List\<SaveVariant> | Available save card variations with weights |

**Total saves formula:** `playerCount + floor(playerCount * extraSavesPerPlayerRatio)`

Example with 4 players and ratio 0.5:
- Total saves = 4 + floor(4 * 0.5) = 4 + 2 = 6
- 4 reserved (1 per player for starting hands)
- 2 shuffled into draw pile

#### Non-Save Cards
| Field | Type | Purpose |
|-------|------|---------|
| `categories` | List\<CategoryEntry> | Card categories with specific cards and counts |

Each `CategoryEntry` has:
- `category` (CardCategory) - for organizational grouping
- `cards` (List\<CardSlot>) - specific CardDefinition + count pairs

Each `CardSlot` has:
- `card` (CardDefinition) - reference to the card asset
- `count` (int) - how many copies in the deck

#### Save Variant Weighting

`SaveVariant` has:
- `card` (CardDefinition) - a specific save card variant
- `weight` (float, 0-100) - percentage chance when generating saves

When generating saves, weights are normalized as relative values. If you have two variants with weights 60 and 40, they have 60% and 40% chance respectively. The 100% cap is advisory (the editor warns but the runtime normalizes).

### Utility Methods

| Method | Returns | Purpose |
|--------|---------|---------|
| `GetExpectedSaveCount(playerCount)` | int | Total saves for given player count |
| `TotalBaseCardCount` | int | Sum of all non-save card counts |
| `TotalSaveWeight` | float | Sum of valid variant weights |
| `EnumerateBaseCardCounts()` | IEnumerable<(CardDefinition, int)> | Yields (card, count) for non-save cards |
| `GetExpectedTotalCardCount(playerCount)` | int | Total deck size for player count |

## Effects

### CardEffect (Base)

**File:** `Effects/Base/CardEffect.cs`

Abstract ScriptableObject that all effect types inherit from.

| Member | Type | Purpose |
|--------|------|---------|
| `description` | string (TextArea) | Human-readable effect description |
| `CreateRuntimeEffect(EffectContext)` | abstract -> PendingEffect | Converts to runtime effect |

Effects are data assets that know their action type and parameters. They don't execute game logic directly.

### EffectContext

**File:** `Effects/Base/EffectContext.cs`

Struct carrying context about who played what on whom.

| Field | Type | Purpose |
|-------|------|---------|
| `OwnerPlayerId` | int | Player who played the card |
| `TargetPlayerId` | int | Target player (0 = none/self) |
| `CardId` | int | CardInstance ID that triggered this |

### PendingEffect

**File:** `Effects/PendingEffect.cs`

Struct representing an effect queued for resolution on the stack.

| Field | Type | Purpose |
|-------|------|---------|
| `Effect` | CardEffect | Reference to the effect asset |
| `Context` | EffectContext | Who/what/whom |
| `ActionType` | ActionType | What kind of game action this produces |
| `INTPayload` | int | Numeric parameter (extra turns, peek count, etc.) |

### EffectResult

**File:** `Effects/Base/EffectResult.cs`

Simple wrapper struct containing a `GameAction`. Currently unused in the codebase (the resolver builds GameActions directly).

## Effect Implementations

All implementations live in `Effects/Implementations/` and follow the same pattern: inherit from `CardEffect`, return a `PendingEffect` with the appropriate `ActionType` and payload.

### AttackEffect

**File:** `Effects/Implementations/AttackEffect.cs`
**Menu:** `Puckd/Effects/Attack`

| Field | Default | Purpose |
|-------|---------|---------|
| `extraTurns` | 2 | Extra turns forced on next player |

Returns `PendingEffect` with `ActionType.ForceExtraTurns` and `INTPayload = extraTurns`.

### CancelEffect

**File:** `Effects/Implementations/CancelEffect.cs`
**Menu:** `Puckd/Effects/Cancel`

No configurable fields. Returns `PendingEffect` with `ActionType.CancelLastEffect`.

When resolved, the resolver removes the most recent action from the output list. This simulates a "Nope" card.

### EliminationEffect

**File:** `Effects/Implementations/EliminationEffect.cs`
**Menu:** `Puckd/Effects/Elimination`

No configurable fields. Returns `PendingEffect` with `ActionType.RequestElimination`.

### PeekEffect

**File:** `Effects/Implementations/PeekEffect.cs`
**Menu:** `Puckd/Effects/Peek`

| Field | Default | Purpose |
|-------|---------|---------|
| `peekAmount` | 3 | Number of cards to peek at |

Returns `PendingEffect` with `ActionType.PeekCards` and `INTPayload = peekAmount`.

### PreventEliminationEffect

**File:** `Effects/Implementations/PreventEliminationEffect.cs`
**Menu:** `Puckd/Effects/Prevent Elimination`

No configurable fields. Returns `PendingEffect` with `ActionType.PreventElimination`.

**Current status:** This effect resolves to an action, but `GameActionExecutor` treats `PreventElimination` as a no-op. The intended reactive play system (play Goalie Save in response to elimination) is not yet implemented.

### ShuffleEffect

**File:** `Effects/Implementations/ShuffleEffect.cs`
**Menu:** `Puckd/Effects/Shuffle`

No configurable fields. Returns `PendingEffect` with `ActionType.ShuffleDeck`.

### SkipEffect

**File:** `Effects/Implementations/SkipEffect.cs`
**Menu:** `Puckd/Effects/Skip`

No configurable fields. Returns `PendingEffect` with `ActionType.SkipTurn`.

## EffectResolver

**File:** `Effects/EffectResolver.cs`

MonoBehaviour that manages a stack of `PendingEffect` instances and resolves them into `GameAction` output. Server-only.

### Methods

**`QueueEffects(List<CardEffect> effects, EffectContext context)`**
- Pushes all effects onto the stack (FIFO order, so first effect in list is last to resolve)
- Each effect is converted via `CreateRuntimeEffect(context)`

**`ResolveAll() -> List<GameAction>`**
- Pops effects from stack one at a time (LIFO resolution)
- For `CancelLastEffect`: removes the last action from the output list
- For everything else: creates a `GameAction` and adds to output
- Returns the final list of actions to apply

### Stack Behavior

Effects are pushed in list order, so for a card with effects [A, B, C]:
- Stack after push: bottom [A, B, C] top
- Resolution order: C first, then B, then A
- Output list: [C_action, B_action, A_action]

If C is a `CancelLastEffect`, it has nothing to cancel (output is empty at that point). This means cancel effects should be placed strategically in the effects list.

**Cross-card interaction:** The stack is cleared after each `ResolveAll()` call. There is no cross-turn effect stacking. A Cancel card played on your turn only cancels effects from the same card play (which doesn't make practical sense for single-effect cards). True cross-player cancellation (like Nope in Exploding Kittens) requires a reactive play system.

## Actions

### ActionType

**File:** `Actions/ActionType.cs`

```csharp
enum ActionType
{
    RequestElimination,   // Player should be eliminated
    PreventElimination,   // Cancel an elimination (not implemented)
    CancelLastEffect,     // Remove last resolved action (handled in resolver)
    ForceExtraTurns,      // Next player takes extra turns
    SkipTurn,             // Current player's turn is skipped
    PeekCards,            // View top N cards of draw pile
    ShuffleDeck           // Shuffle the draw pile
}
```

### GameAction

**File:** `Actions/GameAction.cs`

Immutable struct representing a resolved game action.

| Field | Type | Purpose |
|-------|------|---------|
| `Type` | ActionType | What kind of action |
| `Value` | int | Numeric payload (extra turns, peek count) |
| `Context` | EffectContext | Who played what on whom |

## Suggestions for Improvement

### Reactive Card System
The biggest missing piece. Needed for:
- Playing Goalie Save in response to drawing a Puck'd card
- Playing Cancel in response to another player's card
- Any future "interrupt" style cards

Possible approach:
1. When an eliminable action is about to apply, pause execution
2. Broadcast "response window" to affected player(s)
3. Wait for a response (play a save/cancel) or timeout
4. Resume execution with or without the response card's effects

### Targeting System
Currently `EffectContext.TargetPlayerId` is always 0 (self/none). Attack effects affect "the next player" implicitly through TurnManager. For directed attacks (choose who to attack), need:
- Target selection UI on the client
- Target validation on the server
- Passing target in the play request RPC

### Multi-Effect Card Interactions
The stack model supports cards with multiple effects, but interactions between effects from the same card aren't well-defined. For example, a card with [Peek, Shuffle] would peek first, then shuffle - the peek is wasted. Effect ordering within a card matters and should be documented per-card.

### New Effect Types to Consider
- **Steal** - Take a random card from another player's hand
- **Give** - Force another player to take a card from your hand
- **Double** - Next effect happens twice
- **Conditional** - Effect only triggers if condition is met
- **Combo** - Play N cards of same type for a special effect (like collecting cats in Exploding Kittens)
