# Manual Simulation Analysis

## Compact Packet First

Prefer the repository packet builder:

```powershell
python tools/build_battle_sim_analysis_packet.py --report <report.json> --include-baseline-traces
```

Reduce trace limits when the packet remains large. Read in this order:

1. summary for the model
2. analysis brief
3. focused traces
4. full report or trace dump only for unresolved questions

## Aggregate Comparison

Record the same metrics for baseline and candidate:

- completed sample size and stability
- win/loss/draw or current outcome categories
- iterations/time and unfinished-run categories
- dealt/taken damage, kills, deaths, and role contribution
- skill attempts, successes, and success rate
- action selection and score components when available

High usage is not the same as high impact. A side winning with lower damage but better kills/deaths may indicate focus fire, access, or body blocking rather than raw DPS.

## Root-Cause Axis

- **Skill numbers**: usage and realized output move together; inspect cost, accuracy, dice, cooldown, range, or gates.
- **AI action parameters**: candidate generation/selection changes without distorted shared score math; inspect action definitions and evaluator inputs.
- **AI scoring**: the wrong candidate wins because score components or priorities distort value; inspect the typed score profile and score service.

Keep one hypothesis and one changed axis per iteration.

## Decision Loop

1. Establish a sufficiently large baseline.
2. Make one candidate change outside the canonical baseline.
3. Re-run at the same scale and configuration.
4. Compare aggregates and variance.
5. Keep, revert, or make a smaller iteration.
6. Run focused correctness regressions.
7. Record exact fields changed and the next-run checks.

Do not infer a balance conclusion from one dramatic trace or a non-replayable single run.
