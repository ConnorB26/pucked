# Puck'd - UI Layer

## Overview

The UI layer handles all player-facing screens: main menu with profile editing, lobby player list, and game-over display. It uses Unity's uGUI system with TextMeshPro for text rendering.

**Namespace:** `UI`

**Current state:** The main menu and lobby UI are functional. The in-game UI is limited to a game-over panel. There is no card hand display, turn indicator, card play interaction, peek overlay, or elimination animation.

## Files

| File | Type | Purpose |
|------|------|---------|
| `MainMenuController.cs` | MonoBehaviour | Main menu: profile editing, host/join |
| `LobbyUIController.cs` | MonoBehaviour | Lobby: player list, ready, start |
| `LobbyPlayerRowUI.cs` | MonoBehaviour | Single player row in lobby list |
| `GameUIController.cs` | MonoBehaviour (singleton) | Game-over panel |
| `ColorPicker.cs` | MonoBehaviour | HSV color picker for profile |

## MainMenuController

**File:** `UI/MainMenuController.cs`
**Scene:** `MainMenuScene`

Controls the main menu with two panels: the main menu (host/join buttons) and the profile editor (name + color).

### Inspector References
| Field | Type | Purpose |
|-------|------|---------|
| `maxConnections` | int (default 4) | Max clients for Relay allocation |
| `lobbySceneName` | string | Scene to load after hosting |
| `nameInput` | TMP_InputField | Player name input |
| `colorPreview` | Image | Color swatch preview |
| `colorPicker` | ColorPicker | HSV color picker |
| `joinCodeInput` | TMP_InputField | Relay join code input |
| `mainMenuPanel` | GameObject | Main menu panel |
| `editProfilePanel` | GameObject | Profile editor panel |

### Flow

**Start:**
1. Shows main menu panel, hides profile editor
2. Loads saved profile from `LocalPlayerProfile`
3. Populates name input and color picker from saved data
4. Subscribes to color picker's `OnColorChanged` event

**Profile Editing:**
- `ShowEditProfile()` - Resets editor to saved values, swaps panels
- `OnNameChanged(string)` - Updates in-memory profile name
- `OnColorChanged(Color)` - Updates in-memory profile color + preview swatch
- `OnRandomColorClicked()` - Generates random RGB color
- `SaveProfile()` - Persists to PlayerPrefs
- `ResetProfileEditor()` - Reloads saved data into UI fields

**Hosting:**
1. Saves profile
2. `RelayBootstrap.StartHostWithRelay(maxConnections)` (async)
3. On success: loads lobby scene via NetworkManager scene management
4. On failure: logs error, stays on menu

**Joining:**
1. Saves profile
2. Reads join code from input field
3. `RelayBootstrap.StartClientWithRelay(code)` (async)
4. On success: client follows host to current scene automatically
5. On failure: logs error

### Suggestions for Improvement
- **Error display** - Failures only log to console. Should show error messages in UI.
- **Loading state** - No visual feedback during async Relay operations. Should disable buttons and show spinner.
- **Input validation** - No validation on join code format or name length.
- **Back button** - Profile editor should have a cancel/back button (currently `ShowMainMenu()` exists but isn't wired to a button).

## LobbyUIController

**File:** `UI/LobbyUIController.cs`
**Scene:** `LobbyScene`

Displays lobby state and provides ready/start interactions. Subscribes to `NetworkLobbyManager.OnLobbySnapshotReceived` to rebuild UI on every state change.

### Inspector References
| Field | Type | Purpose |
|-------|------|---------|
| `lobbyManager` | NetworkLobbyManager | Lobby manager reference (auto-found if null) |
| `joinCodeText` | TMP_Text | Displays "Code: XXXX" |
| `statusText` | TMP_Text | Phase status text |
| `playerCountText` | TMP_Text | "Players: N / M" |
| `playerListParent` | Transform | Parent for player row instances |
| `playerRowPrefab` | LobbyPlayerRowUI | Prefab for player rows |
| `readyButton` | Button | Ready/Unready toggle |
| `readyButtonLabel` | TMP_Text | Ready button text |
| `startButton` | Button | Start game (host only) |

### State
- `_rows` - Dictionary\<ulong, LobbyPlayerRowUI> mapping clientId to row instances
- `_localIsReady` - Tracks local ready state for button label
- `_localClientId` - This client's ID from NetworkManager

### Snapshot Handling (`HandleLobbySnapshot`)
1. Updates status text based on lobby phase
2. Updates player count text
3. For each player in snapshot:
   - Creates or updates a `LobbyPlayerRowUI` instance
   - Sets name, color, ready status, local player marker
4. Destroys rows for players no longer in snapshot
5. Updates ready button label ("Ready" / "Unready")
6. Updates start button:
   - Only visible to host
   - Only interactable when all players are ready

### Button Handlers
- **Ready** - Toggles `_localIsReady`, calls `lobbyManager.SetLocalReady()`
- **Start Game** - Calls `lobbyManager.HostRequestStartGame()`

### Suggestions for Improvement
- **Join code copy button** - Should be easy to copy the join code
- **Leave lobby button** - Clients have no way to leave without closing the app
- **Chat** - Simple text chat in lobby would improve the experience
- **Transition to game** - When phase changes to InGame, lobby UI should transition to game UI. Currently the lobby UI stays visible.

## LobbyPlayerRowUI

**File:** `UI/LobbyPlayerRowUI.cs`

Prefab component for a single player row in the lobby list.

### Inspector References
| Field | Type | Purpose |
|-------|------|---------|
| `nameText` | TMP_Text | Player display name |
| `colorImage` | Image | Color swatch |
| `readyText` | TMP_Text | "Ready" / "Not Ready" |
| `localMarker` | GameObject | "(You)" indicator |

### Methods
- `Initialize(clientId, name, color, isReady, isLocal)` - Configures all display fields
- `SetReady(bool)` - Updates ready text and color (green for ready, red for not ready)

## GameUIController

**File:** `UI/GameUIController.cs`
**Pattern:** Singleton via `Instance` static field (set in Awake)

Minimal game-over UI controller. Shows a panel with winner information and host-only action buttons.

### Inspector References
| Field | Type | Purpose |
|-------|------|---------|
| `gameOverPanel` | GameObject | Panel shown on game over |
| `winnerText` | TMP_Text | Winner announcement text |
| `rematchButton` | Button | Rematch button (host only) |
| `closeLobbyButton` | Button | Close lobby button (host only) |
| `mainMenuSceneName` | string | Scene name for returning to main menu |

### Methods

**`ShowGameOver(bool localPlayerWon, PlayerProfileData winnerProfile, bool isHost)`**
- Activates game over panel
- Sets winner text: "You won!" or "Player X wins!"
- Shows Rematch and Close Lobby buttons only for host

**`OnClickRematch()`**
- Server-only guard
- Finds `NetworkLobbyManager`, calls `ServerResetLobby()`
- Hides game over panel

**`OnClickCloseLobby()`**
- Server-only guard
- Shuts down NetworkManager (disconnects everyone)
- Loads main menu scene locally

### Suggestions for Improvement
- **Client buttons** - Clients see no buttons after game over. Should at least have a "Leave" button.
- **Elimination animation** - No visual when a player is eliminated during the game.
- **Winner color** - Winner text doesn't use the winner's profile color.
- **Non-singleton** - The singleton pattern is fragile. Consider using a service locator or event system.

## ColorPicker

**File:** `UI/ColorPicker.cs`

Full HSV color picker component with pointer/drag interaction and hex code input synchronization.

### Inspector References
| Field | Type | Purpose |
|-------|------|---------|
| `paletteImage` | RawImage | Displays generated HSV palette |
| `hexInputField` | TMP_InputField | Hex color code input (#RRGGBB) |
| `selector` | RectTransform | Movable selection indicator |

### Features
- Generates a 256x256 HSV palette texture (hue on X, saturation+value on Y)
- Pointer down and drag to select color
- Bidirectional hex input sync
- `OnColorChanged` UnityEvent\<Color> for external listeners
- `SetColor(Color, notify)` - Programmatic color setting

### Interaction
1. Pointer down or drag on palette
2. Screen position -> local rect position -> UV coordinates
3. UV -> HSV -> RGB color
4. Updates selector position, hex field, fires event

## Missing UI Components

The following UI components are needed for a playable game but do not exist yet:

### Card Hand Display
- Show the player's cards at the bottom of the screen
- Card art, name, category
- Selectable (tap/click to play)
- Disabled when not player's turn
- Update on `SyncHandRpc` receipt

### Turn Indicator
- Show whose turn it is (name + color)
- Highlight when it's the local player's turn
- Update on `NotifyTurnChangedRpc` receipt

### Card Play Confirmation
- When player selects a card, confirm before playing
- For targeted cards, show target selection

### Peek Overlay
- When `PeekResultRpc` arrives, show the peeked cards
- Dismissible overlay with card details

### Elimination Announcement
- When `PlayerEliminatedRpc` arrives, show who was eliminated
- Timed notification or dismissible popup

### Draw Pile Info
- Show number of cards remaining in draw pile
- Visual indicator for draw pile

### Discard Pile
- Show last played card / discard pile top
- Optional: view full discard pile

### End Turn / Draw Button
- Player action to end their turn (draw a card)
- This is critical for the Exploding Kittens flow where drawing is the dangerous action
