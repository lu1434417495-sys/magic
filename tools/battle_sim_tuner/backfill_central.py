"""Backfill / normalize existing observation samples into the central store.

Salvages real (genome -> objective) ground truth that already exists on disk into the
central accumulating dataset (evaluator.DATASET_PATH) in the aligned schema, so
gpu_search / train_surrogate_from_central can consume the full history.

It normalizes each row to: {ts, scenario, faction, genome, objective, fitness, source}.
  - faction  : row['faction'] or legacy row['win_faction'] or --faction-default
  - objective: row['objective'] or recomputed from the fitness payload with the shared
               objective.score_fitness formula (net_kills = enemy_deaths - own_deaths)
Rows without a genome, and without either an objective or a usable fitness payload, are
dropped. Already-present rows (same scenario+faction+genome+objective) are skipped.

APPEND-ONLY by default (flock-safe), so it is safe to run while a tuner is appending to
the same file. Use --rewrite only when no tuner is writing (it rebuilds the file and
drops unsalvageable rows).

    battle_sim_tuner/.venv/bin/python -m battle_sim_tuner.backfill_central --dry-run
    battle_sim_tuner/.venv/bin/python -m battle_sim_tuner.backfill_central
"""

from __future__ import annotations

import argparse
import fcntl
import glob
import hashlib
import json
import os
import time
from types import SimpleNamespace

from .evaluator import DATASET_PATH, REPO_ROOT
from .objective import DEFAULT_MAX_ITERATIONS, score_fitness


def _objective_from_row(row: dict) -> float | None:
    obj = row.get("objective")
    if obj is not None:
        return float(obj)
    fit = row.get("fitness")
    if not isinstance(fit, dict):
        return None
    try:
        net_kills = fit.get("net_kills")
        if net_kills is None:
            net_kills = float(fit.get("enemy_deaths", 0.0)) - float(fit.get("own_deaths", 0.0))
        ns = SimpleNamespace(
            win_rate=float(fit["win_rate"]),
            loss_rate=float(fit["loss_rate"]),
            stalemate_rate=float(fit["stalemate_rate"]),
            avg_iterations=float(fit["avg_iterations"]),
            net_kills=float(net_kills),
        )
    except (KeyError, TypeError, ValueError):
        return None
    return float(score_fitness(ns, DEFAULT_MAX_ITERATIONS))


def _normalize(row: dict, *, source: str, scenario_default: str, faction_default: str) -> dict | None:
    genome = row.get("genome")
    if not isinstance(genome, dict) or not genome:
        return None
    objective = _objective_from_row(row)
    if objective is None:
        return None
    faction = row.get("faction") or row.get("win_faction") or faction_default
    scenario = row.get("scenario") or scenario_default
    return {
        "ts": row.get("ts", time.time()),
        "scenario": scenario,
        "faction": faction,
        "genome": genome,
        "objective": objective,
        "fitness": row.get("fitness", {}),
        "source": source,
    }


def _key(rec: dict) -> str:
    payload = {
        "scenario": rec["scenario"],
        "faction": rec["faction"],
        "genome": rec["genome"],
        "objective": round(float(rec["objective"]), 6),
    }
    return hashlib.sha1(json.dumps(payload, sort_keys=True).encode()).hexdigest()


def _iter_jsonl(path: str):
    try:
        with open(path, encoding="utf-8") as fh:
            for line in fh:
                line = line.strip()
                if not line:
                    continue
                try:
                    yield json.loads(line)
                except json.JSONDecodeError:
                    continue
    except FileNotFoundError:
        return


def main() -> None:
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument(
        "--inputs", nargs="*",
        default=[os.path.join(REPO_ROOT, ".tmp_tuner", "**", "observations.jsonl")],
        help="glob(s) for source observations.jsonl (default: all under .tmp_tuner/)",
    )
    p.add_argument("--dataset", default=DATASET_PATH, help="central store to append into")
    p.add_argument("--scenario-default", default="")
    p.add_argument("--faction-default", default="player")
    p.add_argument("--dry-run", action="store_true")
    p.add_argument(
        "--rewrite", action="store_true",
        help="rebuild the central file (normalize existing rows + drop unsalvageable). "
             "Only with NO tuner writing concurrently.",
    )
    args = p.parse_args()

    sources: list[str] = []
    for pattern in args.inputs:
        sources.extend(sorted(glob.glob(pattern, recursive=True)))
    sources = [s for s in sources if os.path.abspath(s) != os.path.abspath(args.dataset)]
    print(f"source files: {len(sources)}")
    for s in sources:
        print(f"  {s}")

    # Existing central rows: index by key (for dedup), and normalize them too when --rewrite.
    existing_keys: set[str] = set()
    existing_norm: list[dict] = []
    for row in _iter_jsonl(args.dataset):
        rec = _normalize(row, source=args.dataset, scenario_default=args.scenario_default,
                         faction_default=args.faction_default)
        if rec is not None:
            existing_keys.add(_key(rec))
            existing_norm.append(rec)

    # Normalize all source rows, dedup against existing + within this batch.
    new_recs: list[dict] = []
    seen = set(existing_keys)
    salvaged = dropped = 0
    for src in sources:
        for row in _iter_jsonl(src):
            rec = _normalize(row, source=src, scenario_default=args.scenario_default,
                             faction_default=args.faction_default)
            if rec is None:
                dropped += 1
                continue
            k = _key(rec)
            if k in seen:
                continue
            seen.add(k)
            new_recs.append(rec)
            salvaged += 1

    by_scn = {}
    for rec in new_recs:
        by_scn[(rec["faction"], rec["scenario"])] = by_scn.get((rec["faction"], rec["scenario"]), 0) + 1
    print(f"\nexisting usable central rows: {len(existing_norm)}")
    print(f"new salvaged rows: {salvaged}  (dropped unsalvageable: {dropped})")
    print(f"new by (faction, scenario): {by_scn}")

    if args.dry_run:
        print("\n[dry-run] no write.")
        return

    os.makedirs(os.path.dirname(args.dataset), exist_ok=True)
    if args.rewrite:
        merged = existing_norm + new_recs
        tmp = args.dataset + ".tmp"
        with open(tmp, "w", encoding="utf-8") as fh:
            for rec in merged:
                fh.write(json.dumps(rec, ensure_ascii=False, sort_keys=True) + "\n")
        os.replace(tmp, args.dataset)
        print(f"\nREWROTE {args.dataset}: {len(merged)} usable rows (drops legacy/torn rows).")
    else:
        with open(args.dataset, "a", encoding="utf-8") as fh:
            fcntl.flock(fh.fileno(), fcntl.LOCK_EX)
            try:
                for rec in new_recs:
                    fh.write(json.dumps(rec, ensure_ascii=False, sort_keys=True) + "\n")
                fh.flush()
            finally:
                fcntl.flock(fh.fileno(), fcntl.LOCK_UN)
        print(f"\nAPPENDED {len(new_recs)} rows to {args.dataset} (append-only, flock-safe).")


if __name__ == "__main__":
    main()
