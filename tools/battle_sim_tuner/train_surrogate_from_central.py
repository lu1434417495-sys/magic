"""Step-1 follow-up: train the GPU surrogate from the CENTRAL accumulating dataset.

The GPU pipeline (run_gpu_tuning_formal) trains from a per-run observations.jsonl that
is wiped each run. evaluator.record_sample also appends every evaluation to one central
append-only store (BST_DATASET, default tools/battle_sim_tuner/dataset/samples.jsonl),
so the surrogate can learn from the FULL cross-run history instead of a single run.

This module filters the central store to one scenario+faction (the store mixes
scenarios), writes a filtered view, and trains gpu_surrogate.train_surrogate on it.
Requires CUDA (train_surrogate calls require_cuda). Does NOT run on import.

    battle_sim_tuner/.venv/bin/python -m battle_sim_tuner.train_surrogate_from_central \
        --scenario attrition_sustain_2v2 --faction player \
        --output-dir ../.tmp_tuner/surrogate_attrition
"""

from __future__ import annotations

import argparse
import json
import os
import tempfile

from .evaluator import DATASET_PATH


def _filter_rows(src: str, *, scenario: str, faction: str) -> list[dict]:
    rows = []
    with open(src, encoding="utf-8") as fh:
        for line in fh:
            line = line.strip()
            if not line:
                continue
            try:
                row = json.loads(line)
            except json.JSONDecodeError:
                continue  # tolerate a torn line from a concurrent writer
            if faction and row.get("faction") != faction:
                continue
            if scenario and scenario not in str(row.get("scenario", "")):
                continue
            if "genome" not in row or "objective" not in row:
                continue  # skip legacy/incomplete rows
            rows.append(row)
    return rows


def main() -> None:
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--dataset", default=DATASET_PATH, help="central JSONL store")
    p.add_argument("--scenario", default="attrition_sustain_2v2",
                   help="substring filter on the row's scenario path")
    p.add_argument("--faction", default="player")
    p.add_argument("--output-dir", required=True)
    p.add_argument("--target-key", default="objective")
    p.add_argument("--epochs", type=int, default=512)
    p.add_argument("--min-rows", type=int, default=16)
    args = p.parse_args()

    if not os.path.exists(args.dataset):
        raise SystemExit(f"central dataset not found: {args.dataset}")

    rows = _filter_rows(args.dataset, scenario=args.scenario, faction=args.faction)
    print(f"central store: {args.dataset}")
    print(f"matched rows (scenario~={args.scenario!r}, faction={args.faction}): {len(rows)}")
    if len(rows) < args.min_rows:
        raise SystemExit(
            f"only {len(rows)} rows (< --min-rows {args.min_rows}); accumulate more samples "
            f"by running the tuning pipeline on this scenario first."
        )

    # Import torch-backed surrogate lazily so --help works without CUDA.
    from .gpu_surrogate import train_surrogate

    os.makedirs(args.output_dir, exist_ok=True)
    fd, filtered = tempfile.mkstemp(prefix="central_filtered_", suffix=".jsonl", dir=args.output_dir)
    with os.fdopen(fd, "w", encoding="utf-8") as fh:
        for row in rows:
            fh.write(json.dumps(row, sort_keys=True) + "\n")

    result = train_surrogate(
        filtered,
        output_dir=args.output_dir,
        faction=args.faction,
        target_key=args.target_key,
        epochs=args.epochs,
    )
    print("\ntrained surrogate:")
    print(f"  rows={result.observations}  final_loss={result.final_loss:.5f}")
    print(f"  device={result.device_name}")
    print(f"  model={result.model_path}")
    print(f"  metadata={result.metadata_path}")
    print(f"  filtered view kept at {filtered}")


if __name__ == "__main__":
    main()
