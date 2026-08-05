# GPU Surrogate Tuning

Use this mode for many AI scoring weights. Use manual one-axis iteration for skill numbers or a small number of action parameters.

## Authority

Read the current:

- `tools/battle_sim_tuner/PHASE1_WORKFLOW.md`
- tuner CLI `--help`
- score-parameter design document
- scenario and promotion-gate source

Those files own current command flags, thresholds, environments, parameter groups, and output schemas. Do not preserve machine-specific Python or GPU paths in this skill.

## Closed Loop

1. **Validate scenario on real CPU battles.**
   - Require a resolving, sufficiently balanced operating point and adequate sample size according to the current runbook.
   - Fix a separate tuning scenario, not the canonical baseline.

2. **Choose effective dimensions.**
   - Remove parameters whose mechanics never fire in the roster.
   - Use the same dropped/frozen dimensions in sampling and search.
   - Measure sensitivity; do not infer it only from parameter names.

3. **Accumulate durable observations.**
   - Use real battles as ground truth.
   - Keep enough battles per genome to reduce label noise.
   - Preserve the central append-only dataset and scenario/faction identity.

4. **Search on the GPU surrogate.**
   - Prefer uncertainty-aware acquisition rather than predicted mean alone.
   - Treat high predictive uncertainty as extrapolation risk.
   - Search only after sufficient coverage for the effective dimension.

5. **Run the real-battle promotion gate.**
   - A ranked surrogate candidate is a prediction, not a result.
   - Adopt only after the current mandatory CPU A/B gate returns its promotion verdict with adequate completed samples.

6. **Feed rejected finalists back into sampling.**
   - A rejection is evidence of a surrogate blind spot or insufficient margin, not permission to bypass the gate.

## Guardrails

- Keep neutral defaults behaviorally identical before tuning.
- Do not tune parameters for absent mechanics.
- Do not adopt directly from predicted rankings.
- Do not reduce the real-battle gate merely to make promotion easier.
- Keep dataset/report provenance, scenario, faction, source commit, and parameter schema together.
- Run focused AI score and runtime regressions after adopting a profile.
