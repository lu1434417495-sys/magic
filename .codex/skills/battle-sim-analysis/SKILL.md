---
name: battle-sim-analysis
description: Run and analyze battle simulations for this Godot repository with a low-token workflow. Use when the task is to run a battle simulation (e.g. the 6v12 mixed mirror smoke test), or to analyze simulation reports, diagnose AI traces, compare profiles, hand results to GPT Pro or Claude, or decide whether a balance issue comes from skill numbers, AI action parameters, or AI scoring. Also covers the automated GPU surrogate auto-tuning / self-evolving loop for AI scoring weights (validate_scenario, run_gpu_tuning_formal, gpu_search, promote_gate) — when to run each script and how to parse its output.
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

## Development Loop

The iteration cycle for any battle / skill / AI balance change. The battle is fully random and non-reproducible (see run notes), so every step relies on aggregate statistics over a large run count — never on replaying a seed or comparing single runs.

1. **Establish the baseline.** Run the 6v12 baseline with a large `COUNT` (≥ 20) and record the reference aggregates: `win_rate`, per-role damage share, skill `attempts` / `successes` / `success_rate`, deaths by role, `avg_iterations`, `ended_count`. This is your before-state. Never edit the baseline fixtures to shift it.

2. **One hypothesis, one axis.** Decide whether the change targets `skill numbers`, `AI action parameters`, or `AI scoring` (the classification in the Workflow below). Change exactly one axis. Apply it as a separate profile/scenario or an isolated data/code edit on a non-baseline path — never by touching the immutable 6v12 baseline.

3. **Re-run at the same scale.** Run the candidate with the same scenario and the same large `COUNT` (≥ 20). You cannot seed-match, so the only thing making the comparison valid is sample size and that exactly one axis changed.

4. **Compare aggregates, not runs.** Diff candidate vs baseline on the recorded aggregates (`win_rate` delta, role damage share, `success_rate`, deaths by role). Use the analysis Workflow below to build the compact packet and confirm the change moved the intended axis without side effects. Treat `n < 20` as directional only; do not conclude from one run.

5. **Decide: keep / revert / iterate.** If it overshoots or has side effects, revert and try a smaller single-axis change, preserving the existing growth rhythm (see Balance Heuristics). Only consider a second axis once the packet shows the first is insufficient.

6. **Validate correctness.** The simulation gives balance/behavior signal, not a correctness guarantee. Run `dotnet build` and the relevant non-simulation regression runners for the systems you touched (skills / AI / runtime) before considering the change done.

7. **Record the result.** Note before/after aggregates and the exact fields changed. Update `docs/design/project_context_units.md` only if ownership boundaries or read-sets changed — never for balance-number tweaks or simulation-only findings.

## Automated GPU Surrogate Tuning (self-evolving)

Use this **instead of** the manual Development Loop when optimizing **many `AI scoring` weights at once** (the high-dim `BattleAiScoreProfile`, ~15–70 params). It is a closed loop: CPU battles produce ground-truth samples → a GPU surrogate learns `(genome → objective)` → GPU search proposes better genomes → real battles gate them. Do **not** use it for single-axis `skill numbers` tweaks (use the Development Loop) and never point it at the immutable 6v12 baseline (94% ceiling = no gradient).

Current AI score implementation: `docs/design/battle/ai_score_parameters.md`. Canonical runbook: `tools/battle_sim_tuner/PHASE1_WORKFLOW.md`. All scripts run from `tools/`. CPU venv `battle_sim_tuner/.venv/bin/python`; GPU venv `/home/luchaoli/venvs/cuda-op/bin/python` (needs `torch` + `cma`).

**Prereqs (when to do them):**
- The score params must be wired into the engine with **neutral defaults** (weight 0 / non-triggering), so an untuned profile is byte-identical to today. Confirm with `dotnet build` + the `tests/battle_runtime/ai/run_battle_ai_score_*_regression.cs` suite passing unchanged.
- You need a **resource/HP-pressured, resolving, ~50% scenario** (e.g. `attrition_sustain_2v2`). The baseline and lopsided arenas leave the new params as free-drift.
- Decide the **effective dimension** up front: drop params whose mechanic the roster never triggers (see "Setting up gradient" → Layer 1) via `--drop-params` on both sampling and search. Fewer dims ⇒ fewer genomes needed.

**Step 0 — validate the scenario (CPU). Call before any tuning on a new/edited scenario.**
```
battle_sim_tuner/.venv/bin/python -m battle_sim_tuner.validate_scenario \
    --scenario res://data/configs/battle_sim/scenarios/attrition_sustain_2v2.tres --workers 8
```
Parse: needs verdict `resolves: YES` (stalemate ≤ 0.2) **and** `balanced: YES` (|win−0.5| ≤ 0.15) with `n ≥ 20`. If NO, fix the roster (HP/AC/aggression/map) and re-validate — a stalemating or lopsided arena cannot tune anything.

**Step 1 — accumulate samples (CPU-heavy, GPU bursts). Call to grow the dataset.**
```
/home/luchaoli/venvs/cuda-op/bin/python -m battle_sim_tuner.run_gpu_tuning_formal \
    --scenario res://.../attrition_sustain_2v2.tres --faction player \
    --observation-candidates 64 --observation-total-workers 32 \
    --observation-workers-per-candidate 4 --observation-count-per-worker 3 \
    --active-learning-rounds 2 --verify-top-k 4 --output-dir ../.tmp_tuner/phase1_attrition
```
Add `--drop-params mage` (or a comma list) for mage-free rosters so the zero-gradient dims are not sampled. Bump `--observation-workers-per-candidate × --observation-count-per-worker` to ≥12 (default 4) so observation labels are not pure noise in high dim.
Every evaluation also appends to the **central cross-run store** `tools/battle_sim_tuner/dataset/samples.jsonl` (`evaluator.record_sample`, flock-safe). Parse: the run's stdout prints per-stage best `objective`, plus a `[before]`/`[after]` central-store summary line (genomes / battles / best_objective for this scenario+faction) for tracking convergence across rounds. The durable artifacts are `<output-dir>/observations.jsonl` (this run) and the growing central store (all runs). **Resume = just re-run this script:** the central store is append-only and `observations.jsonl` is rewritten per chunk, so completed samples survive a crash and the next invocation accumulates on top — no separate CMA checkpoint is needed because the surrogate retrains from the durable store each round.

**Step 2 — GPU search (this is what loads the 5090D). Call after enough samples (≥ ~16, more is better).**
```
/home/luchaoli/venvs/cuda-op/bin/python -m battle_sim_tuner.gpu_search \
    --observations tools/battle_sim_tuner/dataset/samples.jsonl \
    --scenario attrition_sustain_2v2 --faction player \
    --ensemble-size 8 --kappa 1.0 --cma-popsize 128 --cma-generations 300 \
    --restarts 3 --polish-steps 300 --top-k 16 --output-dir ../.tmp_tuner/gpu_search_attrition
```
Use the **same `--drop-params`** here as in sampling so the search space matches (the new section "Setting up gradient" covers `--sigma0`/popsize/`--kappa`/`--polish-lr` tuning per dimension).
Ensemble surrogate + **pessimistic objective `mean − κ·std`** (anti surrogate-gaming) + CMA-ES (GPU-batched eval) + gradient polish. Self-contained (trains its own ensemble), so it replaces steps 3+4B below. To load the GPU harder, raise `--cma-popsize / --cma-generations / --ensemble-size / --restarts`. Parse `<output-dir>/ranked.json`: each entry has `acq` (pessimistic score — rank by this), `pred_mean`, `pred_std` (high `pred_std` = low-data extrapolation, distrust), `genome`. Top entry is exported to `champion_score_profile.tres`. **These are predictions, not results.**
- *Simpler alternative (4B):* `train_surrogate_from_central` (single net) then `rank_and_export` (one-shot rank). Weaker; prefer `gpu_search`.

**Step 3 — promotion gate (CPU, mandatory). Call before adopting any champion.**
```
battle_sim_tuner/.venv/bin/python -m battle_sim_tuner.promote_gate \
    --candidate ../.tmp_tuner/gpu_search_attrition/ranked.json \
    --scenario res://.../attrition_sustain_2v2.tres --runs 200 --workers 16
```
Real-battle A/B of champion vs default. `--runs` is the **high-R final estimate** (default 200; collected in waves of `--workers` processes and pooled, so parallelism is bounded while R stays high — per-side SE ~`sqrt(28/R)` ≈ 0.37 at 200 vs ~1.0 at the search-time R~24). Parse: **exit code 0 = PROMOTE, 1 = REJECT**; verdict requires `Δobj ≥ margin`, no loss/stalemate regression, and `n ≥ 0.75·--runs` (a failed-wave shortfall fails loudly instead of passing at n=20). The pooled gate result is also recorded once to the central store (highest-quality sample). Only on PROMOTE adopt `champion_score_profile.tres`. On REJECT, return to Step 1 (more samples / raise `--kappa`) — the surrogate found a blind spot.

**Guardrails:** the surrogate confidently games a noisy/misspecified objective — the pessimistic term + the real-battle gate are the defense, never adopt straight from `ranked.json`. The system is CPU-bound (battles); GPU is idle unless `gpu_search` is cranked. Keep one CMA/search run ≤ ~30 effective params; tune param groups in phases (freeze the rest at neutral defaults). Never tune on, or edit, the immutable 6v12 baseline.

### Setting up gradient (do this before trusting any tuning run)

The whole loop only optimizes what carries **gradient** — a parameter the battle data shows actually moving the objective. CMA-ES and the gradient-polish both run on the *surrogate*, so the surrogate can only learn gradient that exists in the samples. Set it up in three layers, in this order.

**Layer 1 — make the params have gradient (signal; highest leverage).**
- *Operating point near 50%.* `win_rate`'s gradient w.r.t. a weight is largest near 50% and flattens toward 0%/100% (logistic plateau). Always run `validate_scenario` first and require `balanced: YES` (`|win−0.5| ≤ 0.15`) **and** `resolves: YES` (stalemate ≤ 0.2), `n ≥ 20`. A lopsided arena (either direction) has the params on a flat plateau — no gradient, nothing to tune. Fix the **roster** (HP/AC/aggression/map, never the immutable baseline) until the default genome sits at ~0.4–0.55.
- *Mechanism must fire.* A weight whose mechanic is absent in the roster is permanently zero-gradient free-drift. **Drop those dims** with `--drop-params` (named group or comma list) on `run_gpu_tuning_formal` (sampling) **and** `gpu_search` (search). Dropped params stay at shipped (neutral) defaults, so frozen ≠ changed; `promote_gate` needs no flag (it fills missing keys from `SCORE_DEFAULTS`). The `mage` group (`--drop-params mage`, 12 params: MP reserve/cost + meteor band + chain-lightning) is for mage-free rosters like `mixed_6v12_two_archer` (4 sword + 2 archer). NOTE: `aura_*` is a warrior/archer ultimate resource, **not** mage — but it (plus `heal_weight`, `control_weight`/`ground_control`, `status*`, `shield_absorbed`, `threat_healer_bias`) is *also* zero-gradient in a roster that equips none of those skills; drop it too or tune it in a scenario that triggers it. Drop-groups live in `tools/battle_sim_tuner/search_space.py` (`MAGE_PARAMS` / `DROP_GROUPS` / `resolve_drop_params`); add new groups there.
- *Measure, don't guess.* A cheap |corr(param, objective)| over `dataset/samples.jsonl` reliably flags **near-zero-gradient** params (≈0 ⇒ drop). It does **not** prove causation the other way: CMA samples are a correlated trajectory (collinear), so high |corr| is inflated — confirm real per-param sensitivity via the surrogate's `pred_std` and OAT perturbation, not corr.

**Layer 2 — CMA-ES exploration on the surrogate (free of battle cost, so explore wide).** `gpu_search` knobs: `--sigma0` is the initial step in normalized space (each dim scaled by `(val−lo)/range`); search starts at the best observed genome, so this is *refinement* — use ~0.15 (≈15% of each dim's range; 0.25 for wider re-exploration). For a high free-dim count (e.g. ~59 after `mage` drop) raise `--cma-popsize` 192–256, `--cma-generations` 400, `--restarts` 4 (multi-modal, escape local optima). CMA adapts its covariance after `sigma0`, so you only set the initial radius.

**Layer 3 — gradient polish (the literal gradient step).** After CMA, `gpu_search` does gradient ascent on the top-`--polish-top-m` candidates: `--polish-steps` (~300 is enough; surrogate is smooth) and `--polish-lr` (normalized-space LR; use ~1e-2 in high dim / high `kappa` to avoid overshooting out of the data support). Critically, polish climbs `acq = pred_mean − κ·pred_std`, **not** `pred_mean` — the uncertainty term pulls the gradient back toward data-dense regions, which is the defense against the surrogate's extrapolation hallucinations. In sparse high-dim, set `--kappa 1.5` and distrust any `ranked.json` entry with high `pred_std` (gradient ran into a no-data zone).

### Sizing the sample budget

Total battle cost = (number of distinct genomes `G`) × (battles per genome `n`). The three independent budgets all matter:
- **`n` (battles per genome) controls measurement noise.** The objective (`6·win − 6·loss − 2·stale + 0.5·net_kills − …`) has per-genome SE ≈ `sqrt(~28/n)`: ≈1.05 at n=24, ≈0.57 at n=80. If that SE approaches the *useful spread between genomes* (often only ~0.5 wide once outliers are dropped), single-point ranking is impossible and you must rely on surrogate pooling. Keep observation `n` modest (12–16; default `count_per_worker×workers_per_candidate=4` is too low in high dim) and put precision into the **gate**: `promote_gate` finalists need `n ≥ 200`, not the `n ≥ 20` floor.
- **`G` (distinct genomes) controls surrogate coverage**, and scales with *effective* dimension `d`: budget ≈ 20–30·`d`. Dropping zero-gradient dims (Layer 1) is what makes `d` — and therefore `G` — small. A nominal 71-dim space at ~30·d needs ~2000 genomes; dropping `mage` → 59 dims → ~1770; aggressive inert-drop → ~40 dims → ~1200.
- CMA samples are autocorrelated, so a stored count of `N` rows carries **less** than `N` independent design points — budget above the naive `30·d`.

## Workflow

1. Rebuild the repo context first.
- Read [../../../docs/design/project_context_units.md](../../../docs/design/project_context_units.md) as the battle-side architecture loading index.
- Use it to pick the owning context units before loading code. Usually this means CU-15 and CU-16, with CU-17, CU-19, CU-20, or CU-21 only when the packet points there.
- Do not treat it as the source of balance values, AI parameters, or trace-level behavior.
- Read [../../../docs/design/battle/balance_simulation.md](../../../docs/design/battle/balance_simulation.md).

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
