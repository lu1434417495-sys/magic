# Task 1 Report: Phantasmal Kill Schema Registration

## Files Changed

- `scripts/systems/battle/_interop/BattleTypedEnums.cs`
- `scripts/player/progression/SkillContentRegistry.cs`
- `tests/progression/schema/run_phantasmal_kill_schema_regression.cs`
- `.superpowers/sdd/task-1-report.md`

## Commits

- `feat: register phantasmal kill schema`

## RED Verification

Command:

```bash
godot --headless -s res://tests/progression/schema/run_phantasmal_kill_schema_regression.cs
```

Initial result before rebuilding the C# assembly:

- Exit code: 1
- Summary: Godot could not instantiate the newly added C# runner because the compiled assembly did not yet contain the class.

Test-only compile command:

```bash
dotnet build magic.csproj
```

Compile result:

- Exit code: 0
- Summary: build succeeded with 0 warnings and 0 errors.

Rerun RED command:

```bash
godot --headless -s res://tests/progression/schema/run_phantasmal_kill_schema_regression.cs
```

Expected RED result:

- Exit code: 1
- Summary: `Phantasmal Kill schema regression: FAIL (1)`
- Expected failure text: `uses unsupported effect_type graded_save_execute`

## GREEN Verification

Command:

```bash
godot --headless -s res://tests/progression/schema/run_phantasmal_kill_schema_regression.cs
```

Result:

- Exit code: 0
- Summary: `Phantasmal Kill schema regression: PASS`

Command:

```bash
dotnet build magic.csproj
```

Result:

- Exit code: 0
- Summary: build succeeded with 0 warnings and 0 errors.

Additional check:

```bash
git diff --check
```

Result:

- Exit code: 0
- Summary: no whitespace errors.

## Self-Review Notes

- The regression runner was added before production changes and produced the expected unsupported `graded_save_execute` RED failure after the assembly was rebuilt.
- `BattleTypedEnums.cs` now registers `GradedSaveExecute`, maps it to and from `graded_save_execute`, and classifies it as AI-offensive plus unit-payload. `IsGroundPayloadEffect(...)` was left unchanged.
- `SkillContentRegistry.cs` now validates the graded-save execute shape, exact params whitelist, strict int thresholds/dice/durations, required save fields, and Phantasmal Kill level-description coverage for levels `0` through `9`.
- No formal `mage_phantasmal_kill.tres` resource load test was added; that remains Task 5 per the brief.
- No `docs/design/project_context_units.md` update was needed because this task did not change runtime ownership boundaries or recommended read sets.

## Follow-up Fix: Phantasmal Kill Binding Profile Validation

Added a focused schema case that mutates the valid `mage_phantasmal_kill` helper into invalid combat-profile binding shapes and expects rejection for non-ground `target_mode`, non-`any` `target_team_filter`, non-`single_coord` `target_selection_mode`, non-`square` `area_pattern`, `area_value != 3`, and non-empty `special_resolution_profile_id`.

RED:

```bash
dotnet build magic.csproj
godot --headless -s res://tests/progression/schema/run_phantasmal_kill_schema_regression.cs
```

- Build exit code: 0, required to load the test-only runner change.
- Godot exit code: 1.
- Summary: `Phantasmal Kill schema regression: FAIL (6)`; each failure was a missing `combat_profile.*` binding-shape validation error.

GREEN:

```bash
dotnet build magic.csproj
godot --headless -s res://tests/progression/schema/run_phantasmal_kill_schema_regression.cs
dotnet build magic.csproj
```

- Initial build exit code: 0, 0 warnings, 0 errors.
- Godot exit code: 0, summary `Phantasmal Kill schema regression: PASS`.
- Final build exit code: 0, 0 warnings, 0 errors.
