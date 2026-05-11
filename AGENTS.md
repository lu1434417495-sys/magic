# Repository Guidelines

## Project Structure & Module Organization
This is a Godot 4.6 project. Core scenes live in `scenes/`, gameplay code in `scripts/`, art and audio in `assets/`, and data resources in `data/`. Entry points are under `scenes/main/`, reusable UI under `scenes/ui/` and `scripts/ui/`, shared scene fragments under `scenes/common/`, and cross-system logic under `scripts/systems/`. Player progression and inventory code live in `scripts/player/`; enemy content lives in `scripts/enemies/`. Put new regression scripts in the matching `tests/<domain>/` folder.

## Design Context Workflow
Before writing a design plan or changing code, read `docs/design/project_context_units.md`. Use it as the repository context map for loading related scenes, scripts, data, and tests before making changes. After any code change, update `docs/design/project_context_units.md` if the affected runtime relationships, ownership boundaries, or recommended read sets have changed.

## Build, Test, and Development Commands
Run the game from the project root:

```bash
godot --path . scenes/main/login_screen.tscn
```

Run focused headless regressions with Godot scripts:

```bash
python tests/run_regression_suite.py
godot --headless --script tests/battle_runtime/runtime/run_battle_runtime_smoke.gd
godot --headless --script tests/battle_runtime/rendering/run_battle_board_regression.gd
godot --headless --script tests/progression/core/run_progression_tests.gd
godot --headless --script tests/warehouse/run_party_warehouse_regression.gd
```

There is no separate build or lint step; runtime parsing in Godot is the validation baseline.

## Coding Style & Naming Conventions
Follow existing GDScript style: tabs for indentation, `snake_case` for files, functions, and variables, and `PascalCase` for scene-facing node/class names such as `GameSession`. Keep gameplay state in plain data containers and put behavior in services or runtime modules. Prefer typed fields (`var value: Type`) when practical. Avoid manual edits inside `.godot/`; Godot regenerates that directory.

## Testing Guidelines
Tests are standalone `.gd` runners named `run_*_regression.gd`, `run_*_smoke.gd`, or similar. Add tests beside the system you changed, and run the narrowest relevant scripts before opening a PR. UI or battle-layout work should include a regression script when feasible and a screenshot when behavior is visual.

Battle simulation and balance runners are not part of the routine full regression suite. Do not include numeric simulation entry points such as `tests/battle_runtime/simulation/run_battle_simulation_regression.gd`, `tests/battle_runtime/simulation/run_battle_ai_vs_ai_simulation_regression.gd`, or `tests/battle_runtime/simulation/run_battle_balance_simulation.gd` in a normal "run all tests" pass unless the user explicitly asks for battle simulation or balance analysis.

## Commit & Pull Request Guidelines
Recent history uses short subjects with Conventional Commit prefixes, for example `feat:` and `chore:`. Keep using that pattern: `feat: add warehouse item stacking`, `fix: preserve battle save lock`. Pull requests should include a brief summary, affected scenes/scripts, test commands run, and screenshots for UI changes. Call out save-format or serialization impacts explicitly.

## Configuration & Safety Notes
`project.godot` defines the main scene and the `GameSession` autoload. Treat save/load changes and serialization version bumps as high risk. Do not commit transient workspace state, generated `.godot` edits, or local save artifacts unless the change intentionally updates tracked fixtures.

## Compatibility Policy
Do not add compatibility logic, legacy aliases, fallback migrations, or old payload/schema support without confirming with the user first. When asking whether compatibility should be preserved, explicitly explain why the compatibility path may be needed and what concrete problems or breakages will happen if compatibility is not added.
