# Battle Sim Tuner

Offline tools for tuning battle AI configuration from real Godot simulations.

## Boundary

- Godot/C# remains the authority for battle rules and validation.
- Tuning runs many isolated headless Godot processes on CPU.
- Python search/training code proposes `BattleAiScoreProfile` values.
- CUDA is mandatory for the tuning entry points. Use `/home/luchaoli/venvs/cuda-op/bin/python`.
- The durable output is a `.tres` resource that can be committed as game config.
- The shipped game does not need Python, CMA-ES, PyTorch, or a model to use a tuned profile.

## Current Pipeline

1. Evaluate candidate score-profile genomes by rendering a temporary
   `BattleSimProfileDef` override into `res://.tmp_tuner/`.
2. Run `RunMixed6v12MirrorAnalysis.cs` in isolated worker environments.
3. Aggregate win rate, stalemate rate, iterations, deaths, and damage into `Fitness`.
4. Record every real simulation candidate into `.tmp_tuner/p6_observations.jsonl`.
5. Train a small CUDA surrogate model from those observations and use it to
   rank large candidate pools before expensive Godot verification.
6. Search scalar `BattleAiScoreProfile` fields with CMA-ES in `tune_6v12.py`.
7. Re-evaluate the champion against default weights.
8. Export:
   - `.tmp_tuner/p6_champion_result.json`
   - `.tmp_tuner/p6_champion_score_profile.tres`

## Commands

From the repository root:

```bash
/home/luchaoli/venvs/cuda-op/bin/python -m tools.battle_sim_tuner.export_score_profile \
  --input-json .tmp_tuner/p6_champion_result.json \
  --output data/configs/battle_ai_score_profiles/p6_champion_score_profile.tres
```

From `tools/` with the CUDA environment:

```bash
/home/luchaoli/venvs/cuda-op/bin/python -m battle_sim_tuner.tune_6v12
```

Run a short real bridge sample that evaluates real Godot battles, trains the
CUDA surrogate, ranks candidates on GPU, and exports a top-ranked score profile:

```bash
cd tools
/home/luchaoli/venvs/cuda-op/bin/python -m battle_sim_tuner.run_gpu_bridge_sample
```

Run the formal GPU-assisted tuning loop. This evaluates real Godot observations,
trains the CUDA surrogate, ranks a large candidate pool on GPU, then verifies the
GPU top candidates back in Godot before exporting the verified best profile:

```bash
cd tools
/home/luchaoli/venvs/cuda-op/bin/python -m battle_sim_tuner.run_gpu_tuning_formal
```

Train and use the mandatory-CUDA surrogate after a tuning pass has written
observations:

```bash
/home/luchaoli/venvs/cuda-op/bin/python -m battle_sim_tuner.gpu_surrogate train \
  --observations ../.tmp_tuner/p6_observations.jsonl \
  --output-dir ../.tmp_tuner/surrogate \
  --faction player

/home/luchaoli/venvs/cuda-op/bin/python -m battle_sim_tuner.gpu_surrogate rank \
  --model ../.tmp_tuner/surrogate/score_profile_surrogate.pt \
  --metadata ../.tmp_tuner/surrogate/score_profile_surrogate_metadata.json \
  --count 100000 \
  --top-k 32 \
  --output-json ../.tmp_tuner/surrogate/ranked_candidates.json
```

## Safe Tuning Surface

The first supported game-config artifact is a standalone `BattleAiScoreProfile`.
The exporter accepts only known score-profile fields and rejects action/brain
patch fields. Keep wider brain/action mutation as simulation-only until it has a
separate schema, validator, and human review path.

## Runtime Hookup

`EnemyAiBrainDef` can reference a `BattleAiScoreProfile` resource. During AI
decision scoring, the resolver order is:

1. simulation per-faction override profile
2. brain-owned score profile
3. default score profile

This keeps battle simulation able to tune one faction against a fixed baseline,
while shipped enemy brain config can directly use the exported `.tres`.
