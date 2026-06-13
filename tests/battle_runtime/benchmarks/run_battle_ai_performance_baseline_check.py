#!/usr/bin/env python3

from __future__ import annotations

import argparse
import os
import shutil
import subprocess
import sys
import time
from pathlib import Path


DEFAULT_TOLERANCE_PCT = 20.0
DEFAULT_ABSOLUTE_TOLERANCE_USEC = 500
DEFAULT_MIN_METRIC_CALL_COUNT = 100
DEFAULT_MIN_PERCENTILE_CALL_COUNT = 100
DEFAULT_COMPARE_METRICS = "avg_usec,p95_usec"


def build_parser() -> argparse.ArgumentParser:
	parser = argparse.ArgumentParser(
		description="Run the full-battle guard-off AI performance baseline regression check."
	)
	parser.add_argument("--godot", default="godot", help="Godot executable name or path.")
	parser.add_argument(
		"--output",
		default="",
		help="Snapshot path for the current run. Defaults to tmp/ai_baseline_check_current_<timestamp>.json.",
	)
	parser.add_argument(
		"--tolerance-pct",
		type=float,
		default=DEFAULT_TOLERANCE_PCT,
		help="Allowed regression percentage for profiler-layer avg/p50/p95 metrics.",
	)
	parser.add_argument(
		"--absolute-tolerance-usec",
		type=int,
		default=DEFAULT_ABSOLUTE_TOLERANCE_USEC,
		help="Allowed absolute increase before percentage regression is considered.",
	)
	parser.add_argument(
		"--compare-metrics",
		default=DEFAULT_COMPARE_METRICS,
		help="Comma-separated profiler metrics to compare.",
	)
	parser.add_argument(
		"--min-metric-call-count",
		type=int,
		default=DEFAULT_MIN_METRIC_CALL_COUNT,
		help="Skip all metrics when baseline or current layer call_count is below this value.",
	)
	parser.add_argument(
		"--min-percentile-call-count",
		type=int,
		default=DEFAULT_MIN_PERCENTILE_CALL_COUNT,
		help="Skip percentile metrics when baseline or current layer call_count is below this value.",
	)
	parser.add_argument(
		"--repeat-count",
		type=int,
		default=2,
		help="Total runs per scenario. The first run is warmup; remaining runs are measured.",
	)
	parser.add_argument(
		"--scenarios",
		default="",
		help="Optional comma-separated scenario ids. Defaults to all baseline scenarios.",
	)
	parser.add_argument(
		"--guard-on",
		action="store_true",
		help="Enable the AI mutation guard for diagnostics. The performance baseline default is guard-off.",
	)
	return parser


def resolve_executable(command: str) -> str | None:
	candidate = Path(command)
	if candidate.exists():
		return str(candidate)
	return shutil.which(command)


def default_output_path(repo_root: Path) -> Path:
	timestamp = time.strftime("%Y%m%d_%H%M%S")
	return repo_root / "tmp" / f"ai_baseline_check_current_{timestamp}.json"


def main() -> int:
	args = build_parser().parse_args()
	try:
		sys.stdout.reconfigure(encoding="utf-8", line_buffering=True)
	except AttributeError:
		pass

	repo_root = Path(__file__).resolve().parents[3]
	godot_command = resolve_executable(args.godot)
	if godot_command is None:
		print(f"[AI_BASELINE_CHECK] Godot executable not found: {args.godot}", file=sys.stderr)
		return 1

	output_path = Path(args.output) if args.output else default_output_path(repo_root)
	if not output_path.is_absolute():
		output_path = repo_root / output_path
	output_path.parent.mkdir(parents=True, exist_ok=True)

	env = os.environ.copy()
	env["AI_BASELINE_OUTPUT_FILE"] = str(output_path)
	env["AI_BASELINE_REPEAT_COUNT"] = str(max(args.repeat_count, 2))
	env["AI_BASELINE_MAX_ITERATIONS"] = "0"
	env["AI_BASELINE_REQUIRE_COMPLETED"] = "true"
	env["AI_BASELINE_MUTATION_GUARD_ENABLED"] = "true" if args.guard_on else "false"
	env["AI_BASELINE_TOLERANCE_PCT"] = f"{args.tolerance_pct:g}"
	env["AI_BASELINE_ABSOLUTE_TOLERANCE_USEC"] = str(max(args.absolute_tolerance_usec, 0))
	env["AI_BASELINE_COMPARE_METRICS"] = args.compare_metrics
	env["AI_BASELINE_MIN_METRIC_CALL_COUNT"] = str(max(args.min_metric_call_count, 0))
	env["AI_BASELINE_MIN_PERCENTILE_CALL_COUNT"] = str(
		max(args.min_percentile_call_count, 0)
	)
	env.pop("AI_BASELINE_UPDATE", None)
	env.pop("UPDATE_BASELINE", None)
	if args.scenarios:
		env["AI_BASELINE_SCENARIOS"] = args.scenarios
	else:
		env.pop("AI_BASELINE_SCENARIOS", None)
		env.pop("BASELINE_SCENARIOS", None)

	benchmark_script = "tests/battle_runtime/benchmarks/run_battle_ai_performance_baseline.cs"
	command = [
		godot_command,
		"--headless",
		"--path",
		str(repo_root),
		"--script",
		benchmark_script,
	]
	print(
		"[AI_BASELINE_CHECK] "
		f"tolerance={args.tolerance_pct:g}% guard={'on' if args.guard_on else 'off'} "
		f"absolute_tolerance={max(args.absolute_tolerance_usec, 0)}us "
		f"min_metric_call_count={max(args.min_metric_call_count, 0)} "
		f"min_percentile_call_count={max(args.min_percentile_call_count, 0)} "
		f"metrics={args.compare_metrics} repeat={max(args.repeat_count, 2)} output={output_path}"
	)
	result = subprocess.run(command, cwd=repo_root, env=env)
	if result.returncode != 0:
		print(
			"[AI_BASELINE_CHECK] FAILED: current run regressed beyond tolerance or did not complete. "
			f"See snapshot: {output_path}",
			file=sys.stderr,
		)
		return result.returncode

	print(f"[AI_BASELINE_CHECK] PASS: no profiler-layer regression over {args.tolerance_pct:g}%.")
	print(f"[AI_BASELINE_CHECK] Snapshot: {output_path}")
	return 0


if __name__ == "__main__":
	raise SystemExit(main())
