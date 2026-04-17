# Text Runtime

This folder contains the headless text command chain used during development.

Scope:
- Drive `GameSession + GameRuntimeFacade` without scene UI.
- Provide a stable text protocol for automation, debugging, and agent tooling.
- Exercise cross-system flows such as new game, movement, warehouse, rewards, and battle.

Non-scope:
- It is not the main startup flow.
- It is not a formal player-facing text UI.
- It should not become the source of truth for world or battle rules.

Entry points:
- `run_text_command_repl.gd`: local interactive debugging.
- `run_text_command_script.gd`: run a scenario file.
- `run_text_command_regression.gd`: end-to-end regression coverage.

Typical commands:
```powershell
godot --headless --script tests/text_runtime/run_text_command_script.gd
godot --headless --script tests/text_runtime/run_text_command_regression.gd
godot --headless --script tests/text_runtime/run_text_command_script.gd -- res://tests/text_runtime/scenarios/contract_board_accept.txt
godot --headless --script tests/text_runtime/run_text_command_script.gd -- res://tests/text_runtime/scenarios/battle_loot_commit.txt
godot --headless --script tests/text_runtime/run_text_command_script.gd -- res://tests/text_runtime/scenarios/battle_loot_overflow.txt
```

Automation-only helpers used by scenarios:
- `battle start <settlement|single>`
- `battle finish <player|hostile>`
- `warehouse capacity <value>`
- `expect warehouse <item_id> == <quantity>`
