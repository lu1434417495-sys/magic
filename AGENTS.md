# Project Agents Guide

## Scope

This repository is organized by gameplay domain. Keep scenes, scripts, assets,
and data in the matching top-level folders.

## Structure Rules

- Put entry scenes in `scenes/main/`.
- Put player content in `scenes/player/` and `scripts/player/`.
- Put enemy content in `scenes/enemies/` and `scripts/enemies/`.
- Put reusable UI in `scenes/ui/` and `scripts/ui/`.
- Put cross-cutting game logic in `scripts/systems/`.
- Put shared helpers in `scripts/utils/`.
- Put reusable scenes in `scenes/common/`.
- Put configuration data in `data/configs/`.
- Keep generated Godot metadata inside `.godot/` untouched.

## Workflow

- Add new features from a prompt file in `prompts/` when possible.
- Keep scene and script naming aligned by feature.
- Prefer small, domain-scoped changes over large unrelated edits.
