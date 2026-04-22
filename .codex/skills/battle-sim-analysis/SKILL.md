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

6. Only after the compact packet points to a concrete axis, load owning resources.
- Skill-side issues: load the relevant `data/configs/skills/*.tres`.
- Brain or action issues: load the relevant `data/configs/enemies/brains/*.tres` and `scripts/enemies/actions/*.gd`.
- Score issues: load `scripts/systems/battle_ai_score_profile.gd`, `battle_ai_score_service.gd`, and `battle_ai_service.gd`.

7. Keep the output structured.
- Lead with the main deltas from `comparisons`.
- Name whether the issue is mostly `skill numbers`, `AI action parameters`, or `AI scoring`.
- Recommend a small next patch and name the exact fields to adjust.
- Name which output fields should be checked in the next run.

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
