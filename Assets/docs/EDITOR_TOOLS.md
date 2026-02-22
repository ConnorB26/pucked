# Puck'd - Editor Tools & Data Layer

## Overview

The editor layer provides custom Unity Inspector UIs for creating and managing card, deck, and effect assets. These tools make the game data-driven, allowing cards to be designed without code changes.

**Namespace:** `Editor` (Unity Editor only, not included in builds)

## Files

| File | Type | Purpose |
|------|------|---------|
| `CardEditor.cs` | CustomEditor | Inspector for CardDefinition assets |
| `DeckDefinitionEditor.cs` | CustomEditor | Inspector for DeckDefinition assets |
| `EffectEditor.cs` | CustomEditor | Inspector for CardEffect assets |
| `ScriptableObjectSearchPopup.cs` | EditorWindow | Reusable asset search popup |

## CardEditor

**File:** `Editor/CardEditor.cs`
**Target:** `CardDefinition`

Custom inspector that displays card properties and provides tools for managing effects.

### Display
- Card name, category, artwork (Sprite), description, variation index
- Reorderable effects list showing each attached CardEffect

### Actions

**Add Existing Effect:**
- Opens `EffectSearchWindow` (ScriptableObjectSearchPopup for CardEffect type)
- Search and select from existing effect assets in the project
- Selected effect is added to the card's effects list

**Create New Effect:**
- Opens `EffectTypeWindow` listing all concrete CardEffect subclasses
- Select a type to create a new effect asset
- Saved in the same folder as the card asset with name `{CardName}_{EffectType}`
- Automatically added to the card's effects list

**Clone as Variation:**
- Duplicates the card asset with `_Var` suffix appended to filename
- Increments `variationIndex`
- Copies all effects by reference (same effect assets, not cloned)
- Opens the new asset in the Inspector

**Remove Effect:**
- X button on each effect in the list
- Removes from the card's effects list (does not delete the asset)

## DeckDefinitionEditor

**File:** `Editor/DeckDefinitionEditor.cs`
**Target:** `DeckDefinition`

Custom inspector for deck composition rules with validation and preview calculations.

### Display Sections

**Header:**
- Deck name and description

**Save Card Rules:**
- Save category dropdown
- Extra saves per player ratio slider
- Example save counts for 2, 3, 4, and 5 players
- Save variants list with card reference + weight percentage
- Total weight display with warning if > 100%
- "Add Save Variant" button

**Non-Save Categories:**
- List of CategoryEntry items, each expandable
- Each category shows its CardCategory type and contained CardSlot entries
- Each CardSlot shows card reference + count
- Per-category total count
- Warning if a slot references a card matching the save category

**Footer:**
- "Add Category" button (creates empty)
- "Add Missing Categories" button (scaffolds all CardCategory enum values not already present)
- Deck totals: base cards + save examples for 2-5 players

### Validation
- Warns if save variant weights exceed 100%
- Warns if non-save category contains cards matching the save category
- Displays expected card counts per player count

## EffectEditor (EffectEditorFull)

**File:** `Editor/EffectEditor.cs`
**Target:** `CardEffect` (and all subclasses)

Custom inspector for effect assets with creation and duplication tools.

### Display
- Effect type name (derived from class name)
- Description field
- All serialized fields from the concrete subclass (auto-detected via `SerializedObject`)

### Actions

**Create Card Using This Effect:**
- Creates a new `CardDefinition` asset in the same folder
- Sets the card's effects list to contain this effect
- Names the card after the effect
- Opens the new card in the Inspector

**Duplicate Effect:**
- Copies the effect asset with `_Copy` suffix
- Opens the new asset in the Inspector

**Create New Effect:**
- Opens type picker window listing all CardEffect subclasses
- Creates a new effect asset of the selected type

## ScriptableObjectSearchPopup

**File:** `Editor/ScriptableObjectSearchPopup.cs`

Generic reusable editor window for searching ScriptableObject assets by type.

### Usage
```csharp
ScriptableObjectSearchPopup.Show<T>(callback);
```

### Features
- Searches all assets of type T in the project
- Case-insensitive text filtering
- Displays asset names in a scrollable list
- Calls callback with selected asset
- Used by CardEditor for "Add Existing Effect" workflow

## Data Asset Workflows

### Creating a New Card
1. **Right-click** in Project > Create > Puckd > Card
2. Set name, category, artwork, description
3. Add effects via Inspector:
   - "Add Existing Effect" to reuse an effect
   - "Create New Effect" to make a new one

### Creating a New Effect
1. **Right-click** in Project > Create > Puckd > Effects > [Type]
2. Configure effect-specific parameters (e.g., `extraTurns` for Attack)
3. Optionally: use "Create Card Using This Effect" to auto-generate a card

### Creating a New Deck
1. **Right-click** in Project > Create > Puckd > Deck > Deck Definition
2. Configure save card rules:
   - Set save category (usually GoalieSave)
   - Set extra saves ratio
   - Add save variants with weights
3. Configure non-save categories:
   - Add categories
   - Add card slots with counts per category
4. Review totals for different player counts

### Creating Card Variations
1. Open a card asset in the Inspector
2. Click "Clone as Variation"
3. The new variant has the same effects but a different variationIndex
4. Modify as needed (different artwork, name, etc.)
5. Add the variant to save variants in DeckDefinition with a weight

## Asset Organization Recommendations

```
Assets/
  Data/
    Cards/
      Puckd/
        PuckdCard.asset
        PuckdCard_Var.asset
      GoalieSave/
        GoalieSave.asset
        MiracleSave.asset
      Attack/
        BodyCheck.asset
      Skip/
        LineChange.asset
      Peek/
        ScoutReport.asset
      Shuffle/
        IceResurface.asset
      Cancel/
        OffsidesCall.asset
    Effects/
      AttackEffect.asset
      CancelEffect.asset
      EliminationEffect.asset
      PeekEffect.asset
      PreventEliminationEffect.asset
      ShuffleEffect.asset
      SkipEffect.asset
    Decks/
      StandardDeck.asset
    Configs/
      StandardConfig.asset
```

## Creating New Effect Types

To add a new effect type:

1. Create a new class inheriting from `CardEffect` in `Effects/Implementations/`
2. Add `[CreateAssetMenu(menuName = "Puckd/Effects/YourEffect")]`
3. Add any configurable fields (serialized)
4. Implement `CreateRuntimeEffect()` returning a `PendingEffect`
5. If needed, add a new `ActionType` enum value
6. Handle the new ActionType in `GameActionExecutor.Apply()`
7. Create an effect asset instance and attach it to a card

Example:
```csharp
[CreateAssetMenu(menuName = "Puckd/Effects/Steal")]
public class StealEffect : CardEffect
{
    public override PendingEffect CreateRuntimeEffect(EffectContext context)
    {
        return new PendingEffect
        {
            Context = context,
            Effect = this,
            ActionType = ActionType.StealCard, // new enum value
            INTPayload = 1
        };
    }
}
```
