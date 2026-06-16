"""GPU surrogate search: make each CPU battle's data count more.

Replaces the one-shot `rank_candidates` (sample a pool, score once) with a much
stronger GPU-resident search so the 5090D actually works and squeezes more out of
the expensive CPU observations:

  1. ENSEMBLE surrogate  - K nets (bootstrap) trained on the real observations.
  2. PESSIMISTIC objective - acquire on  mean(preds) - kappa * std(preds)  so the
     search is pulled toward high predicted reward AND data-supported regions
     (ensemble disagreement = extrapolation = penalised). This is the main defence
     against "surrogate gaming".
  3. CMA-ES search (pycma) - covariance + step-size adaptation (beats diagonal CEM
     on correlated/ill-conditioned landscapes); each generation's whole population
     is scored in ONE batched GPU forward pass through the ensemble.
  4. GRADIENT polish - the surrogate is differentiable, so the CMA top candidates
     are refined with Adam ascent on the pessimistic objective (CMA/CEM throw the
     gradient away). Many restarts handle multimodality.

Outputs ranked.json + champion_score_profile.tres. These are PREDICTIONS - gate
them with promote_gate.py (real battles) before adopting.

Requires CUDA + `cma` in the runtime venv. Does NOT run on import.

    /home/luchaoli/venvs/cuda-op/bin/python -m battle_sim_tuner.gpu_search \
        --observations tools/battle_sim_tuner/dataset/samples.jsonl \
        --scenario attrition_sustain_2v2 --faction player \
        --output-dir ../.tmp_tuner/gpu_search_attrition
"""

from __future__ import annotations

import argparse
import json
import os

from .evaluator import DATASET_PATH


def _load_rows(path: str, *, scenario: str, faction: str) -> list[dict]:
    rows = []
    with open(path, encoding="utf-8") as fh:
        for line in fh:
            line = line.strip()
            if not line:
                continue
            try:
                row = json.loads(line)
            except json.JSONDecodeError:
                continue  # tolerate a torn line from a concurrent writer
            if faction and row.get("faction") not in (None, faction):
                continue
            if scenario and scenario not in str(row.get("scenario", "")):
                continue
            if "genome" in row and "objective" in row:
                rows.append(row)
    return rows


def main() -> None:
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--observations", default=DATASET_PATH, help="JSONL with genome+objective rows")
    p.add_argument("--scenario", default="", help="substring filter on row scenario (optional)")
    p.add_argument("--faction", default="player")
    p.add_argument("--output-dir", required=True)
    p.add_argument("--target-key", default="objective")
    p.add_argument("--min-rows", type=int, default=16)
    # ensemble
    p.add_argument("--ensemble-size", type=int, default=8)
    p.add_argument("--epochs", type=int, default=400)
    p.add_argument("--lr", type=float, default=1e-3)
    # pessimism
    p.add_argument("--kappa", type=float, default=1.0, help="penalty on ensemble std (uncertainty)")
    # cma
    p.add_argument("--cma-popsize", type=int, default=128)
    p.add_argument("--cma-generations", type=int, default=300)
    p.add_argument("--restarts", type=int, default=3)
    p.add_argument("--sigma0", type=float, default=0.2)
    # gradient polish
    p.add_argument("--polish-top-m", type=int, default=16)
    p.add_argument("--polish-steps", type=int, default=300)
    p.add_argument("--polish-lr", type=float, default=2e-2)
    # output
    p.add_argument("--top-k", type=int, default=16)
    p.add_argument("--seed", type=int, default=0)
    p.add_argument("--free-params", default="",
                   help="comma-separated param names, or 'phase1' (the 15 A+B params) or 'all'; "
                        "dims not listed are frozen at --base (phase tuning).")
    p.add_argument("--base", default="",
                   help="genome json for the frozen dims (default: shipped SCORE_DEFAULTS).")
    args = p.parse_args()

    rows = _load_rows(args.observations, scenario=args.scenario, faction=args.faction)
    print(f"observations: {args.observations}")
    print(f"matched rows (scenario~={args.scenario!r}, faction={args.faction}): {len(rows)}", flush=True)
    if len(rows) < args.min_rows:
        raise SystemExit(f"only {len(rows)} rows (< --min-rows {args.min_rows}); accumulate more first.")

    # Heavy imports lazily so --help works without CUDA / cma / torch.
    import numpy as np

    try:
        import cma
    except ImportError as exc:  # pragma: no cover
        raise SystemExit("missing `cma`; pip install cma into this venv.") from exc

    from .export_score_profile import write_score_profile_tres
    from .gpu_surrogate import _bounds, _load_torch, _matrix, _SurrogateNet, require_cuda
    from .search_space import score_weight_space

    torch, nn = _load_torch()
    device_name = require_cuda()
    device = torch.device("cuda")
    print(f"device: {device_name}", flush=True)

    specs = score_weight_space(args.faction)
    dim = len(specs)
    x_np, y_np = _matrix(rows, specs, args.target_key)        # raw genomes, targets
    lo_np, scale_np = _bounds(specs)                          # per-param lo, scale(=max(hi-lo,1))
    lo = torch.tensor(lo_np, device=device)
    scale = torch.tensor(scale_np, device=device)
    # net inputs are normalised the same way train_surrogate normalises them.
    xn = torch.tensor((x_np - lo_np) / scale_np, device=device)
    y = torch.tensor(y_np, device=device)

    # ----- 1. train the ensemble (bootstrap for disagreement) ------------------
    torch.manual_seed(args.seed)
    rng = np.random.default_rng(args.seed)
    nets = []
    n = xn.shape[0]
    for k in range(max(1, args.ensemble_size)):
        net = _SurrogateNet.build(dim).to(device)
        opt = torch.optim.AdamW(net.parameters(), lr=args.lr)
        loss_fn = nn.MSELoss()
        idx_boot = torch.tensor(rng.integers(0, n, size=n), device=device)
        xb, yb = xn[idx_boot], y[idx_boot]
        net.train()
        for _ in range(max(1, args.epochs)):
            opt.zero_grad()
            loss = loss_fn(net(xb), yb)
            loss.backward()
            opt.step()
        net.eval()
        for param in net.parameters():
            param.requires_grad_(False)
        nets.append(net)
    print(f"trained ensemble: K={len(nets)}, rows={n}, dim={dim}", flush=True)

    # ----- pessimistic acquisition on GPU --------------------------------------
    def acq_from_norm(zn):
        """zn: [N, dim] already in net-normalised space. Returns (acq, mean, std)."""
        preds = torch.stack([net(zn).squeeze(-1) for net in nets], dim=0)  # [K, N]
        mean = preds.mean(dim=0)
        std = preds.std(dim=0)
        return mean - args.kappa * std, mean, std

    # CMA searches in net-normalised z-space, bounded to the data box [0, (hi-lo)/scale].
    hi_z = ((torch.tensor(lo_np + scale_np, device=device) - lo) / scale)  # ~1.0 per dim
    hi_z_np = hi_z.detach().cpu().numpy()

    def score_norm_batch(z_list):
        zt = torch.tensor(np.asarray(z_list, dtype="float32"), device=device)
        zt = torch.clamp(zt, torch.zeros_like(hi_z), hi_z)
        with torch.no_grad():
            acq, _, _ = acq_from_norm(zt)
        return acq.detach().cpu().numpy()

    # ----- free-dim subset: search only --free-params, freeze the rest ----------
    PHASE1_AB = [
        "survival_margin_gain_weight", "post_action_threat_damage_weight",
        "post_action_threat_count_weight", "lethal_survival_risk_penalty",
        "incoming_threat_relief_weight",
        "mp_reserve_floor_bp", "mp_reserve_pressure_weight", "mp_reserve_breach_penalty",
        "stamina_reserve_floor_bp", "stamina_reserve_pressure_weight",
        "stamina_reserve_breach_penalty", "aura_reserve_floor_bp",
        "aura_reserve_pressure_weight", "aura_reserve_breach_penalty",
        "resource_conservation_weight",
    ]
    fp = args.free_params.strip().lower()
    if fp in ("", "all"):
        free_names = [s.name for s in specs]
    elif fp == "phase1":
        free_names = PHASE1_AB
    else:
        free_names = [x.strip() for x in args.free_params.split(",") if x.strip()]
    name_to_idx = {s.name: i for i, s in enumerate(specs)}
    free_idx = [name_to_idx[nm] for nm in free_names if nm in name_to_idx]
    if not free_idx:
        raise SystemExit("no valid --free-params matched the score profile params")
    fdim = len(free_idx)
    free_idx_t = torch.tensor(free_idx, device=device, dtype=torch.long)

    # base genome for the FROZEN dims (defaults unless --base), net-normalised.
    if args.base:
        with open(args.base, encoding="utf-8") as fh:
            base_raw = json.load(fh)
    else:
        from .search_space import SCORE_DEFAULTS
        base_raw = dict(SCORE_DEFAULTS)
    from .search_space import SCORE_DEFAULTS as _DEF
    base_vec = np.asarray(
        [float(base_raw.get(s.name, _DEF[s.name])) for s in specs], dtype="float32"
    )
    base_z = torch.tensor((base_vec - lo_np) / scale_np, device=device)  # [D]
    hi_free = torch.tensor(hi_z_np[free_idx], device=device)
    hi_free_np = hi_z_np[free_idx]
    zeros_free = torch.zeros_like(hi_free)

    def expand_free(zf):  # zf: [N, fdim] (net-norm) -> full [N, D]; differentiable in zf
        full = base_z.unsqueeze(0).repeat(zf.shape[0], 1)
        return full.scatter(1, free_idx_t.unsqueeze(0).expand(zf.shape[0], -1), zf)

    def score_free(zf_list):
        zt = torch.clamp(torch.tensor(np.asarray(zf_list, dtype="float32"), device=device),
                         zeros_free, hi_free)
        with torch.no_grad():
            acq, _, _ = acq_from_norm(torch.clamp(expand_free(zt), torch.zeros_like(hi_z), hi_z))
        return acq.detach().cpu().numpy()

    # x0 over free dims: best observed genome's free dims (others frozen at base).
    best_row = max(range(n), key=lambda i: float(y_np[i][0]))
    x0 = ((x_np[best_row] - lo_np) / scale_np)[free_idx].astype("float64")
    print(f"searching {fdim} free dims of {dim} (rest frozen at base).", flush=True)

    # ----- 2/3. CMA-ES restarts over free dims, GPU-batched population scoring --
    pool_free = []
    for r in range(max(1, args.restarts)):
        es = cma.CMAEvolutionStrategy(
            list(x0),
            args.sigma0,
            {
                "bounds": [list(np.zeros(fdim)), list(hi_free_np)],
                "popsize": args.cma_popsize,
                "maxiter": args.cma_generations,
                "seed": args.seed + r + 1,
                "verbose": -9,
            },
        )
        gen = 0
        while not es.stop():
            sols = es.ask()
            acq = score_free(sols)                   # one batched GPU forward / generation
            es.tell(sols, [-float(a) for a in acq])  # cma minimises
            pool_free.extend(sols)
            gen += 1
        pool_free.append(list(es.result.xbest))
        print(f"  cma restart {r}: gens={gen} best_acq={-es.result.fbest:+.4f}", flush=True)

    # ----- 4. gradient polish of the top-M over free dims ----------------------
    pool_free_arr = np.clip(np.asarray(pool_free, dtype="float32"), 0.0, hi_free_np)
    top_m_idx = np.argsort(-score_free(pool_free_arr))[: max(1, args.polish_top_m)]
    zf = torch.tensor(pool_free_arr[top_m_idx], device=device, requires_grad=True)
    gopt = torch.optim.Adam([zf], lr=args.polish_lr)
    for _ in range(max(0, args.polish_steps)):
        gopt.zero_grad()
        zc = torch.clamp(zf, zeros_free, hi_free)
        acq, _, _ = acq_from_norm(torch.clamp(expand_free(zc), torch.zeros_like(hi_z), hi_z))
        (-acq.sum()).backward()
        gopt.step()
        with torch.no_grad():
            zf.clamp_(zeros_free, hi_free)
    polished_free = zf.detach().cpu().numpy()

    # ----- expand free candidates to full genomes, rank, export ----------------
    all_free = np.unique(
        np.round(np.concatenate([pool_free_arr, np.clip(polished_free, 0.0, hi_free_np)], axis=0), 5),
        axis=0,
    )
    with torch.no_grad():
        full = torch.clamp(expand_free(torch.tensor(all_free, device=device)),
                           torch.zeros_like(hi_z), hi_z)
        acq, mean, std = acq_from_norm(full)
    all_z = full.cpu().numpy()
    acq_np, mean_np, std_np = acq.cpu().numpy(), mean.cpu().numpy(), std.cpu().numpy()
    order = np.argsort(-acq_np)[: max(1, args.top_k)]

    def z_to_genome(zrow):
        raw = lo_np + zrow * scale_np
        return {spec.name: spec.clamp(float(raw[i])) for i, spec in enumerate(specs)}

    ranked = [
        {
            "acq": float(acq_np[i]),
            "pred_mean": float(mean_np[i]),
            "pred_std": float(std_np[i]),
            "predicted_objective": float(mean_np[i]),  # alias for downstream consumers
            "genome": z_to_genome(all_z[i]),
        }
        for i in order
    ]

    os.makedirs(args.output_dir, exist_ok=True)
    ranked_json = os.path.join(args.output_dir, "ranked.json")
    with open(ranked_json, "w", encoding="utf-8") as fh:
        json.dump(ranked, fh, indent=2, sort_keys=True)
    champion_tres = os.path.join(args.output_dir, "champion_score_profile.tres")
    write_score_profile_tres(champion_tres, ranked[0]["genome"])

    top = ranked[0]
    print(f"\nranked {len(all_z)} unique candidates -> top {len(ranked)} ({ranked_json})")
    print(f"champion acq={top['acq']:+.4f}  pred_mean={top['pred_mean']:+.4f}  pred_std={top['pred_std']:.4f}")
    print(f"exported: {champion_tres}")
    print("NEXT: gate with promote_gate.py (real battles) before adopting.")


if __name__ == "__main__":
    main()
