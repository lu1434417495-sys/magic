---
name: battle-sim-analysis
description: Analyze battle simulation outputs for this Godot repository with a low-token workflow. Use when the task involves simulation reports, AI trace diagnosis, comparing profiles, handing results to GPT Pro or Claude, or deciding whether a balance issue comes from skill numbers, AI action parameters, or AI scoring.
---

# Battle Sim Analysis

Use this skill when the task is to analyze battle simulation outputs or prepare them for another model. Do not start by reading the full report and full trace dump together.

## Workflow

1. Rebuild the repo context first.
- Read [../../../docs/design/project_context_units.md](../../../docs/design/project_context_units.md).
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
- Identify extreme seeds or outlier runs and name what changed: wipeout, zero-death win, early focus-fire collapse, or timeout. Use these to discuss variance, not as the main conclusion.
- Treat `manual_policy=wait` and scripted mixed mirrors as controlled balance probes, not evidence of player-facing AI intelligence.

7. Only after the compact packet points to a concrete axis, load owning resources.
- Skill-side issues: load the relevant `data/configs/skills/*.tres`.
- Brain or action issues: load the relevant `data/configs/enemies/brains/*.tres` and `scripts/enemies/actions/*.gd`.
- Score issues: load `scripts/systems/battle_ai_score_profile.gd`, `battle_ai_score_service.gd`, and `battle_ai_service.gd`.

8. Keep the output structured.
- Lead with the main deltas from `comparisons`.
- Name whether the issue is mostly `skill numbers`, `AI action parameters`, or `AI scoring`.
- Recommend a small next patch and name the exact fields to adjust.
- Name which output fields should be checked in the next run.

## Balance Heuristics

- If a side wins with worse total damage but better deaths/kills, suspect focus fire, target access, or body-blocking rather than raw DPS.
- If archers or another role contribute about two thirds or more of total damage while melee units mostly absorb damage, call out role skew before buffing the winning side globally.
- If a skill has low usage and low success, inspect both AI action conditions and hit numbers. Do not assume damage needs a buff until usage is high enough to matter.
- If a skill is meant to fix a failing role, prefer a conservative single-axis buff first: hit bonus, stamina, cooldown, trigger chance, or damage dice, not several at once.
- Preserve the existing growth rhythm unless the user explicitly asks for a new curve. For example, if an old skill curve is `0-3: -1`, `4: 0`, `5: +1`, changing level 0 to `+1` should usually become `0-3: +1`, `4: +2`, `5: +3`, not `0:+1 ... 5:+6`.
- When variance is high across seeds, recommend reducing all-or-nothing outcomes before making large faction buffs: lower complete miss frequency, smooth dodge/block formulas, or improve target access consistency.
- When sample size is small but action is needed, phrase the patch as an experiment and define the next-run checks: win rate, damage share by role, skill attempts/successes, deaths by role, and extreme seed count.

## Rules

- Remember that `manual_policy=wait` means manual-side units are dummies. Do not use these runs to claim AI quality against an intelligent player.
- Verify which profile is baseline before reasoning from `comparisons`. Baseline is always `profile_entries[0]`; prefer a `00_baseline_*` profile_id in scripted runs.
- Filter or explicitly flag `battle_ended=false` runs before interpreting win-rate conclusions.
- Treat `score_input.estimated_*` as AI-side estimates, not realized combat results. Cross-check with `faction_metric_totals`, `skill_success_counts`, and completed-run outcomes.
- Prefer at least 20 seeds per profile before treating small deltas as stable.
- Remember that `top_candidates` are truncated to 5 entries per action, so dense target spaces may hide lower-ranked options.
- Do not feed the original full `report.json` and full `turn_traces.jsonl` to another model together unless the compact packet was not enough.
- Do not infer a balance conclusion from one weird trace; confirm it with summary-level deltas first.
- Do not mix multiple change axes in one recommendation unless the packet shows the first axis is insufficient.
