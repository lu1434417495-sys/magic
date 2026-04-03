# Login Screen

## Goal

Create a player-facing login screen as the game's entry scene, with a clear
"Start Game" option.

## Requirements

- The project opens into a centered login screen.
- The screen shows the game title and a visible "开始游戏" button.
- Clicking the button transitions into a playable next scene placeholder.
- The interface provides clear visual feedback before scene transition.
- If the target scene path is missing or invalid, the UI shows an error state
  instead of failing silently.

## Files To Touch

- `project.godot`
- `scenes/main/login_screen.tscn`
- `scenes/main/game_placeholder.tscn`
- `scripts/ui/login_screen.gd`

## Acceptance Checks

- The project launches into the login screen.
- The "开始游戏" option is visible and clickable in the main scene.
- Clicking the button switches into the placeholder game scene.
- Invalid scene configuration is handled with visible feedback.
