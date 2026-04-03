# your_godot_game

Starter structure for a Godot project organized by feature area.

## Layout

- `prompts/`: reusable prompt templates for feature and bugfix work.
- `scenes/`: game scenes grouped by domain.
- `scripts/`: scripts grouped by domain.
- `assets/`: sprites, audio, fonts, and effects.
- `data/`: configs and save data.
- `project.godot`: existing Godot project entry file.

## Notes

- Keep gameplay code close to its owning feature folder.
- Use `scenes/common/` and `scripts/utils/` for shared pieces only.
- Avoid manual edits inside `.godot/` because Godot regenerates it.
