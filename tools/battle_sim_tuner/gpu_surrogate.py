"""Mandatory-CUDA surrogate model for BattleAiScoreProfile search.

The real battle simulation stays in Godot/C# on CPU. This module gives the
tuning pipeline a GPU-owned stage: learn a small value model from real simulation
observations, then score many candidate genomes on CUDA before selecting a small
set for expensive Godot verification.
"""

from __future__ import annotations

import argparse
import json
import os
import random
from dataclasses import dataclass
from typing import Any, Mapping, Sequence

from .search_space import SCORE_DEFAULTS, score_weight_space


def _load_torch():
    try:
        import torch
        import torch.nn as nn
    except ModuleNotFoundError as exc:
        raise RuntimeError(
            "PyTorch is required for battle_sim_tuner GPU surrogate training. "
            "Use /home/luchaoli/venvs/cuda-op/bin/python."
        ) from exc
    return torch, nn


def require_cuda() -> str:
    torch, _ = _load_torch()
    if not torch.cuda.is_available():
        raise RuntimeError(
            "CUDA is required for battle_sim_tuner. "
            "Use the CUDA-enabled environment at /home/luchaoli/venvs/cuda-op."
        )
    return torch.cuda.get_device_name(0)


def _read_jsonl(path: str) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    with open(path, encoding="utf-8") as fh:
        for line in fh:
            line = line.strip()
            if not line:
                continue
            rows.append(json.loads(line))
    return rows


def _genome(row: Mapping[str, Any]) -> Mapping[str, Any]:
    value = row.get("genome")
    if not isinstance(value, Mapping):
        raise ValueError("Observation row is missing object field 'genome'.")
    return value


def _target(row: Mapping[str, Any], target_key: str) -> float:
    if target_key in row:
        return float(row[target_key])
    fitness = row.get("fitness")
    if isinstance(fitness, Mapping) and target_key in fitness:
        return float(fitness[target_key])
    raise ValueError(f"Observation row is missing target '{target_key}'.")


def _matrix(rows: Sequence[Mapping[str, Any]], specs, target_key: str):
    import numpy as np

    x = []
    y = []
    for row in rows:
        genome = _genome(row)
        x.append([float(spec.clamp(genome.get(spec.name, SCORE_DEFAULTS[spec.name]))) for spec in specs])
        y.append(_target(row, target_key))
    return np.asarray(x, dtype="float32"), np.asarray(y, dtype="float32").reshape((-1, 1))


def _bounds(specs):
    import numpy as np

    lo = np.asarray([spec.lo for spec in specs], dtype="float32")
    hi = np.asarray([spec.hi for spec in specs], dtype="float32")
    scale = np.maximum(hi - lo, 1.0)
    return lo, scale


class _SurrogateNet:
    @staticmethod
    def build(input_dim: int):
        _, nn = _load_torch()
        return nn.Sequential(
            nn.Linear(input_dim, 128),
            nn.GELU(),
            nn.Linear(128, 128),
            nn.GELU(),
            nn.Linear(128, 1),
        )


@dataclass(frozen=True)
class SurrogateTrainResult:
    model_path: str
    metadata_path: str
    observations: int
    final_loss: float
    device_name: str


def train_surrogate(
    observation_jsonl: str,
    *,
    output_dir: str,
    faction: str = "player",
    target_key: str = "objective",
    epochs: int = 512,
    batch_size: int = 256,
    learning_rate: float = 1e-3,
    seed: int = 0,
) -> SurrogateTrainResult:
    torch, _ = _load_torch()
    device_name = require_cuda()
    device = torch.device("cuda")
    specs = score_weight_space(faction)
    rows = _read_jsonl(observation_jsonl)
    if len(rows) < 4:
        raise ValueError("Need at least 4 observation rows to train the surrogate.")

    x_np, y_np = _matrix(rows, specs, target_key)
    lo, scale = _bounds(specs)
    x_np = (x_np - lo) / scale

    torch.manual_seed(seed)
    random.seed(seed)
    x = torch.from_numpy(x_np).to(device)
    y = torch.from_numpy(y_np).to(device)
    model = _SurrogateNet.build(x.shape[1]).to(device)
    optimizer = torch.optim.AdamW(model.parameters(), lr=learning_rate)
    loss_fn = torch.nn.MSELoss()

    final_loss = 0.0
    for _ in range(max(1, epochs)):
        order = torch.randperm(x.shape[0], device=device)
        for start in range(0, x.shape[0], max(1, batch_size)):
            idx = order[start : start + batch_size]
            pred = model(x[idx])
            loss = loss_fn(pred, y[idx])
            optimizer.zero_grad(set_to_none=True)
            loss.backward()
            optimizer.step()
            final_loss = float(loss.detach().cpu())

    os.makedirs(output_dir, exist_ok=True)
    model_path = os.path.join(output_dir, "score_profile_surrogate.pt")
    metadata_path = os.path.join(output_dir, "score_profile_surrogate_metadata.json")
    torch.save(model.state_dict(), model_path)
    with open(metadata_path, "w", encoding="utf-8") as fh:
        json.dump(
            {
                "faction": faction,
                "target_key": target_key,
                "fields": [spec.name for spec in specs],
                "lo": lo.tolist(),
                "scale": scale.tolist(),
                "observations": len(rows),
                "final_loss": final_loss,
                "device_name": device_name,
            },
            fh,
            indent=2,
            sort_keys=True,
        )
    return SurrogateTrainResult(model_path, metadata_path, len(rows), final_loss, device_name)


def _sample_candidates(specs, count: int, seed: int) -> list[dict[str, int]]:
    rng = random.Random(seed)
    candidates = []
    baseline = {spec.name: int(SCORE_DEFAULTS[spec.name]) for spec in specs}
    candidates.append(baseline)
    while len(candidates) < count:
        candidates.append({spec.name: spec.clamp(rng.uniform(spec.lo, spec.hi)) for spec in specs})
    return candidates


def rank_candidates(
    model_path: str,
    metadata_path: str,
    *,
    count: int,
    top_k: int,
    output_json: str,
    seed: int = 1,
) -> list[dict[str, Any]]:
    torch, _ = _load_torch()
    require_cuda()
    device = torch.device("cuda")
    with open(metadata_path, encoding="utf-8") as fh:
        metadata = json.load(fh)
    faction = metadata["faction"]
    specs = score_weight_space(faction)
    lo = torch.tensor(metadata["lo"], dtype=torch.float32, device=device)
    scale = torch.tensor(metadata["scale"], dtype=torch.float32, device=device)
    model = _SurrogateNet.build(len(specs)).to(device)
    model.load_state_dict(torch.load(model_path, map_location=device))
    model.eval()

    candidates = _sample_candidates(specs, max(1, count), seed)
    x = torch.tensor(
        [[float(candidate[spec.name]) for spec in specs] for candidate in candidates],
        dtype=torch.float32,
        device=device,
    )
    with torch.no_grad():
        scores = model((x - lo) / scale).squeeze(1).detach().cpu().tolist()
    ranked = sorted(
        (
            {
                "predicted_objective": float(score),
                "genome": candidate,
            }
            for candidate, score in zip(candidates, scores)
        ),
        key=lambda item: item["predicted_objective"],
        reverse=True,
    )[: max(1, top_k)]
    os.makedirs(os.path.dirname(os.path.abspath(output_json)), exist_ok=True)
    with open(output_json, "w", encoding="utf-8") as fh:
        json.dump(ranked, fh, indent=2, sort_keys=True)
    return ranked


def main() -> None:
    parser = argparse.ArgumentParser(description="Train and use mandatory-CUDA score profile surrogate.")
    sub = parser.add_subparsers(dest="command", required=True)

    train = sub.add_parser("train")
    train.add_argument("--observations", required=True)
    train.add_argument("--output-dir", required=True)
    train.add_argument("--faction", default="player")
    train.add_argument("--target-key", default="objective")
    train.add_argument("--epochs", type=int, default=512)

    rank = sub.add_parser("rank")
    rank.add_argument("--model", required=True)
    rank.add_argument("--metadata", required=True)
    rank.add_argument("--count", type=int, default=10000)
    rank.add_argument("--top-k", type=int, default=16)
    rank.add_argument("--output-json", required=True)
    args = parser.parse_args()

    if args.command == "train":
        result = train_surrogate(
            args.observations,
            output_dir=args.output_dir,
            faction=args.faction,
            target_key=args.target_key,
            epochs=args.epochs,
        )
        print(
            f"trained model={result.model_path} metadata={result.metadata_path} "
            f"observations={result.observations} loss={result.final_loss:.6f} "
            f"device={result.device_name}"
        )
    elif args.command == "rank":
        ranked = rank_candidates(
            args.model,
            args.metadata,
            count=args.count,
            top_k=args.top_k,
            output_json=args.output_json,
        )
        print(f"wrote {args.output_json} top_k={len(ranked)}")


if __name__ == "__main__":
    main()
