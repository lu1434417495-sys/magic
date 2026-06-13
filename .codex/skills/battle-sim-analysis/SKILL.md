---
name: battle-sim-analysis
description: Run and analyze battle simulations for this Godot repository with a low-token workflow. Use when the task is to run a battle simulation (e.g. the 6v12 mixed mirror smoke test), or to analyze simulation reports, diagnose AI traces, compare profiles, hand results to GPT Pro or Claude, or decide whether a balance issue comes from skill numbers, AI action parameters, or AI scoring.
---

# Battle Simulation: Run & Analysis

Use this skill to run a battle simulation and/or analyze its outputs. When analyzing, do not start by reading the full report and full trace dump together.

## Running a Simulation

The 6-vs-12 mixed mirror is the **canonical baseline**: a small high-level elite player squad vs a larger low-level hostile force, formal-character fixtures, 30x18 canyon terrain, `manual_policy=wait`, `max_iterations=3000`. It is the standard smoke test for any battle / skill / AI change, and the reference point every other scenario is compared against. (Despite "Mirror" in the name, the two sides are intentionally asymmetric.)

Current composition (`BattleSimFormalCombatFixture._build_mixed_6v12_roster()`):
- Player (6, elite, high skill levels): 4× Elite Sword (warrior r2, `steel_longsword`; `charge` L7, `warrior_heavy_strike` L5), 1× Elite Archer (archer r2, `ash_longbow`; `archer_aimed_shot` L3, `archer_multishot` L7), 1× Elite Mage (mage r5, no weapon, MP max 1000; `mage_fireball` / `mage_cone_of_cold` / `mage_blink` / `mage_gust_of_wind` / `mage_chain_lightning` all L7).
- Hostile (12, normal, low skill levels): 6× Hostile Sword (warrior r2, `steel_longsword`; `charge` L1, `warrior_heavy_strike` L1), 6× Hostile Archer (no profession r0, `ash_longbow`; `archer_aimed_shot` L1, `archer_multishot` L1).

**Baseline is immutable — do not modify it.** Treat these as read-only fixtures and never edit them to make a run pass or to change the matchup:
- Scenario: `data/configs/battle_sim/scenarios/mixed_6v12_mirror_simulation.tres`
- Runner: `tests/battle_runtime/benchmarks/RunMixed6v12MirrorAnalysis.cs`
- Roster: `BattleSimFormalCombatFixture._build_mixed_6v12_roster()` in `scripts/systems/battle/sim/BattleSimFormalCombatFixture.cs`

You may pass runtime env knobs (e.g. `COUNT`, `START_SEED`, `SEEDS`) for a given invocation, but the roster composition, map, and runner logic stay fixed. New or variant simulations must be added as **separate** scenarios/rosters that reference this baseline for comparison — never by changing the 6v12 baseline.

1. Build first (the runners are C#; Godot loads the compiled assembly):
```bash
dotnet build magic.csproj -nologo -clp:ErrorsOnly
```
If the build fails, stop and report the errors — do not run the sim.

2. Run the 6v12 mirror (default `COUNT` is 10; use `COUNT=1` for a single run):
```bash
COUNT=1 godot --headless -s res://tests/battle_runtime/benchmarks/RunMixed6v12MirrorAnalysis.cs
```
A clean run exits `0` and prints a JSON report (or writes it to `OUTPUT_FILE`) ending with a `trace_summary_file` line. Report these top-level fields: `timed_out` (must be `false`), `win_rate` (`player`/`hostile`/`draw`), `completed_run_count` vs `requested_run_count`, `ended_count` (battle actually finished), `avg_iterations`, and the `trace_summary_file` path. Per-run details (incl. each `winner_faction_id`) are under `runs[]`.

Run notes:
- **The battle is fully random and NOT reproducible.** Combat resolution (hit rolls, damage dice, saves, etc.) comes from `TrueRandomSeedService` (crypto randomness in `scripts/utils/TrueRandomSeedService.cs`), independent of any seed. The same seed will **not** reproduce a battle outcome, so win/loss varies on every run — that is by design, not a regression. `START_SEED` / `SEEDS` only affect setup-side generation (roster attribute rolls, spawn placement), never the fight. Do **not** try to "lock" a result or do seed-matched A/B comparisons — compare only via aggregate statistics over a large run count (`COUNT` ≥ 20+).
- When grepping the log for `error`/`fail`, ignore trace field names like `damage_on_save_failure` or `save_failure_probability_basis_points` — those are data, not exceptions. Real failures show as `SCRIPT ERROR` / stack traces or a non-zero exit code.
- Optional env knobs: `COUNT`, `START_SEED`, `SEEDS`, `PROGRESS`, `TRACE_AI`, `SIM_TIMEOUT_SECONDS`, `OUTPUT_FILE`, `AI_MUTATION_GUARD`, `VALIDATE_SPAWN_REACHABILITY`, `VALIDATE_BIDIRECTIONAL_SPAWN_REACHABILITY`, `AI_PROFILE` (+ `AI_PROFILE_TOP_N` / `AI_PROFILE_SORT` / `AI_PROFILE_FILTER`), and roster overrides `MAIN_CHARACTER_MEMBER_ID` / `LEADER_MEMBER_ID` / `MAIN_CHARACTER_REROLL_COUNT` / `ATTRIBUTE_ROLL_SEED`.
- Other scenarios live in `data/configs/battle_sim/scenarios/*.tres` and run via `BattleSimRunner` / the benchmark runners under `tests/battle_runtime/`.
- These numeric simulation / balance runners are NOT part of the routine regression suite (see `AGENTS.md`); only run them on explicit request.

Once you have a report, continue with the analysis workflow below.

## Workflow

1. Rebuild the repo context first.
- Read [../../../docs/design/project_context_units.md](../../../docs/design/project_context_units.md) as the battle-side architecture loading index.
- Use it to pick the owning context units before loading code. Usually this means CU-15 and CU-16, with CU-17, CU-19, CU-20, or CU-21 only when the packet points there.
- Do not treat it as the source of balance values, AI parameters, or trace-level behavior.
- Read [../../../docs/design/battle_balance_simulation.md](../../../docs/design/battle_balance_simulation.md).

2. Locate the simulation outputs.
- Use the user-provided `report.json` when available.
- If the user only gives a scenario id or output directory, locate the newest report under `user://simulation_reports/<scenario_id>/`.
- Assume the full report may already contain embedded `ai_turn_traces`.

3. Build the compact analysis packet before doing diagnosis.
- Run:
```bash
python tools/build_battle_sim_analysis_packet.py --report <report.json> --include-baseline-traces
```
- If the trace packet is still too large, rerun with smaller limits such as:
```bash
python tools/build_battle_sim_analysis_packet.py --report <report.json> --include-baseline-traces --max-focus-traces 12 --max-traces-per-profile 4
```

4. Read files in this order.
- `summary_for_llm.json`
- `analysis_brief.md`
- `focus_traces.jsonl`
- Original full `report.json` or full `turn_traces.jsonl` only if the compact packet is insufficient

5. Classify the likely root cause before proposing changes.
- Treat it as a `skill numbers` issue when usage, win rate, and output all move in the same direction.
- Treat it as an `AI action parameter` issue when action selection shifts but score inputs do not show obviously distorted value math.
- Treat it as an `AI scoring` issue when the wrong action wins because `score_bucket_priority`, `total_score`, `resource_cost_score`, or `position_objective_score` are skewed.

6. For balance reports, add a compact battlefield decomposition before recommending edits.
- State sample size and stability first. Flag `n < 20` as directional evidence, not a stable win-rate conclusion.
- Compare win/loss, average iterations, deaths/kills, dealt/taken damage, and completed-run count before skill details.
- Break down skill use as `attempts/successes/success_rate` per faction; separate high usage from high impact.
- Break down unit or role contribution when present: damage share, death share, kills, and damage taken. Check whether a faction is really winning through all units or only through a small subset such as archers.
- Identify outlier runs and name what changed: wipeout, zero-death win, early focus-fire collapse, or timeout. Use these to discuss variance, not as the main conclusion (and not as a reproducible case — runs cannot be replayed).
- Treat `manual_policy=wait` and scripted mixed mirrors as controlled balance probes, not evidence of player-facing AI intelligence.

7. Only after the compact packet points to a concrete axis, load owning resources.
- Use `project_context_units.md` to keep the read set narrow instead of opening unrelated battle, world, or progression modules.
- Skill-side issues: load the relevant `data/configs/skills/*.tres`.
- Brain or action issues: load the relevant `data/configs/enemies/brains/*.tres` and `scripts/enemies/actions/*.cs`.
- Score issues: load `scripts/systems/battle/ai/BattleAiScoreProfile.cs`, `scripts/systems/battle/ai/BattleAiScoreService.*.cs`, and `scripts/systems/battle/ai/BattleAiService.cs`.

8. Keep the output structured.
- Lead with the main deltas from `comparisons`.
- Name whether the issue is mostly `skill numbers`, `AI action parameters`, or `AI scoring`.
- Recommend a small next patch and name the exact fields to adjust.
- Name which output fields should be checked in the next run.
- If the recommended fix changes battle ownership boundaries or recommended read-sets, note that `docs/design/project_context_units.md` should be updated. Do not use that file to record balance-number tweaks or simulation-only findings.

## Balance Heuristics

- If a side wins with worse total damage but better deaths/kills, suspect focus fire, target access, or body-blocking rather than raw DPS.
- If archers or another role contribute about two thirds or more of total damage while melee units mostly absorb damage, call out role skew before buffing the winning side globally.
- If a skill has low usage and low success, inspect both AI action conditions and hit numbers. Do not assume damage needs a buff until usage is high enough to matter.
- If a skill is meant to fix a failing role, prefer a conservative single-axis buff first: hit bonus, stamina, cooldown, trigger chance, or damage dice, not several at once.
- Preserve the existing growth rhythm unless the user explicitly asks for a new curve. For example, if an old skill curve is `0-3: -1`, `4: 0`, `5: +1`, changing level 0 to `+1` should usually become `0-3: +1`, `4: +2`, `5: +3`, not `0:+1 ... 5:+6`.
- When variance is high across seeds, recommend reducing all-or-nothing outcomes before making large faction buffs: lower complete miss frequency, smooth dodge/block formulas, or improve target access consistency.
- When sample size is small but action is needed, phrase the patch as an experiment and define the next-run checks: win rate, damage share by role, skill attempts/successes, deaths by role, and outlier-run count.

## Rules

- The 6v12 mixed mirror is the fixed baseline. Never edit `mixed_6v12_mirror_simulation.tres`, `RunMixed6v12MirrorAnalysis.cs`, or `BattleSimFormalCombatFixture._build_mixed_6v12_roster()` — not to fix a failing run, not to tweak the matchup. Add new scenarios/rosters as separate fixtures that reference this baseline instead.
- Battle resolution is fully random and non-reproducible (combat rolls use `TrueRandomSeedService`, not the run seed). There is no "same seed" run. Never reason from a single run or compare two runs as if seeds made them comparable — only aggregate stats over many runs (`COUNT` ≥ 20+) are meaningful.
- Remember that `manual_policy=wait` means manual-side units are dummies. Do not use these runs to claim AI quality against an intelligent player.
- Verify which profile is baseline before reasoning from `comparisons`. Baseline is always `profile_entries[0]`; prefer a `00_baseline_*` profile_id in scripted runs.
- Filter or explicitly flag `battle_ended=false` runs before interpreting win-rate conclusions.
- Treat `score_input.estimated_*` as AI-side estimates, not realized combat results. Cross-check with `faction_metric_totals`, `skill_success_counts`, and completed-run outcomes.
- Prefer at least 20 runs per profile before treating small deltas as stable (runs are independent random samples, not reproducible seeds).
- Remember that `top_candidates` are truncated to 5 entries per action, so dense target spaces may hide lower-ranked options.
- Do not feed the original full `report.json` and full `turn_traces.jsonl` to another model together unless the compact packet was not enough.
- Do not infer a balance conclusion from one weird trace; confirm it with summary-level deltas first.
- Do not mix multiple change axes in one recommendation unless the packet shows the first axis is insufficient.
