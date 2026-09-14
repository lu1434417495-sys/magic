#!/usr/bin/env python3
"""Run repeatable local validation for the Magic repository.

The CLI composes existing repository entry points. It does not reimplement
Godot regression discovery, output gates, or lifecycle checks.
"""

from __future__ import annotations

import argparse
import json
import math
import os
import shlex
import subprocess
import sys
import time
from dataclasses import asdict, dataclass, field
from datetime import datetime, timezone
from pathlib import Path
from typing import Callable, Sequence


REPO_ROOT = Path(__file__).resolve().parents[1]
REGRESSION_RUNNER = REPO_ROOT / "tests" / "run_regression_suite.py"
DEFAULT_TEST_TIMEOUT_SECONDS = 180.0
MAX_DEFAULT_JOBS = 16
OUTPUT_EXCERPT_LIMIT = 4000


@dataclass(frozen=True)
class CommandSpec:
	name: str
	argv: tuple[str, ...]
	capture_output: bool = False


@dataclass(frozen=True)
class StepResult:
	name: str
	command: tuple[str, ...]
	status: str
	returncode: int | None
	duration_seconds: float
	stdout_excerpt: str = ""
	stderr_excerpt: str = ""
	note: str = ""
	observations: dict[str, object] = field(default_factory=dict)


CommandRunner = Callable[[CommandSpec, Path, bool], StepResult]


def positive_finite_seconds(value: str) -> float:
	try:
		seconds = float(value)
	except ValueError as exc:
		raise argparse.ArgumentTypeError("must be a finite number greater than 0") from exc
	if not math.isfinite(seconds) or seconds <= 0:
		raise argparse.ArgumentTypeError("must be a finite number greater than 0")
	return seconds


def default_jobs(cpu_count: int | None = None) -> int:
	logical_cpus = cpu_count if cpu_count is not None else (os.cpu_count() or 1)
	return max(1, min(MAX_DEFAULT_JOBS, (logical_cpus + 1) // 2))


def add_common_options(parser: argparse.ArgumentParser) -> None:
	parser.add_argument("--python", default=sys.executable, help="Python executable used for repository runners.")
	parser.add_argument("--dotnet", default="dotnet", help="dotnet executable name or path.")
	parser.add_argument("--godot", default="godot", help="Godot executable name or path.")
	parser.add_argument(
		"--jobs",
		default=str(default_jobs()),
		help=f"Regression worker count. Default: half the logical CPUs, capped at {MAX_DEFAULT_JOBS}.",
	)
	parser.add_argument(
		"--test-timeout-seconds",
		type=positive_finite_seconds,
		default=DEFAULT_TEST_TIMEOUT_SECONDS,
		help=f"Per-test timeout passed to the regression runner. Default: {DEFAULT_TEST_TIMEOUT_SECONDS:.0f}.",
	)
	parser.add_argument(
		"--restore",
		action="store_true",
		help="Allow dotnet build to restore packages. The default build uses --no-restore.",
	)
	parser.add_argument("--dry-run", action="store_true", help="Print the planned commands without executing them.")
	parser.add_argument(
		"--json-report",
		default="",
		help="Write a machine-readable summary to this path. Relative paths resolve from the repository root.",
	)


def add_regression_options(parser: argparse.ArgumentParser) -> None:
	parser.add_argument(
		"--include-simulation",
		action="store_true",
		help="Explicitly include BattleSim and numeric simulation runners.",
	)
	parser.add_argument(
		"--include-benchmarks",
		action="store_true",
		help="Explicitly include benchmark and analysis runners.",
	)


def build_parser() -> argparse.ArgumentParser:
	parser = argparse.ArgumentParser(
		description="Run consistent checkout, build, and regression validation for the Magic repository."
	)
	common = argparse.ArgumentParser(add_help=False)
	add_common_options(common)
	subparsers = parser.add_subparsers(dest="mode", metavar="MODE")

	subparsers.add_parser(
		"inspect",
		parents=[common],
		help="Inspect checkout hygiene and routine regression discovery without building or running tests.",
	)
	subparsers.add_parser(
		"quick",
		parents=[common],
		help="Inspect, discover routine regressions, and build without running Godot tests.",
	)
	focused = subparsers.add_parser(
		"focused",
		parents=[common],
		help="Run the standard checks plus regressions matching one repository path fragment.",
	)
	focused.add_argument(
		"--pattern",
		required=True,
		help="Repository path fragment passed to tests/run_regression_suite.py --pattern.",
	)
	add_regression_options(focused)
	full = subparsers.add_parser(
		"full",
		parents=[common],
		help="Run the standard checks plus the full routine regression suite.",
	)
	add_regression_options(full)
	return parser


def parse_args(argv: Sequence[str] | None = None) -> argparse.Namespace:
	values = list(sys.argv[1:] if argv is None else argv)
	if not values:
		values = ["quick"]
	elif values[0] not in {"inspect", "quick", "focused", "full", "-h", "--help"}:
		values.insert(0, "quick")
	return build_parser().parse_args(values)


def format_command(argv: Sequence[str]) -> str:
	return shlex.join(argv)


def excerpt(text: str) -> str:
	if len(text) <= OUTPUT_EXCERPT_LIMIT:
		return text
	return text[:OUTPUT_EXCERPT_LIMIT] + "\n... output truncated ..."


def run_command(spec: CommandSpec, repo_root: Path, dry_run: bool) -> StepResult:
	print(f"[{ 'PLAN' if dry_run else 'RUN' }] {spec.name}: {format_command(spec.argv)}", flush=True)
	if dry_run:
		return StepResult(spec.name, spec.argv, "planned", None, 0.0)

	started = time.monotonic()
	try:
		completed = subprocess.run(
			list(spec.argv),
			cwd=repo_root,
			text=True,
			encoding="utf-8",
			errors="replace",
			capture_output=spec.capture_output,
			check=False,
		)
	except OSError as exc:
		duration = time.monotonic() - started
		return StepResult(
			spec.name,
			spec.argv,
			"failed",
			127,
			duration,
			stderr_excerpt=str(exc),
			note="unable to start command",
		)

	duration = time.monotonic() - started
	status = "passed" if completed.returncode == 0 else "failed"
	stdout = completed.stdout or ""
	observations: dict[str, object] = {}
	if spec.name == "discover":
		observations["discovered_tests"] = parse_discovered_total(stdout)
	elif spec.name == "checkout":
		observations["checkout"] = summarize_git_status(stdout)
	return StepResult(
		spec.name,
		spec.argv,
		status,
		completed.returncode,
		duration,
		stdout_excerpt=excerpt(stdout),
		stderr_excerpt=excerpt(completed.stderr or ""),
		observations=observations,
	)


def skipped_step(name: str, note: str) -> StepResult:
	return StepResult(name, (), "skipped", None, 0.0, note=note)


def parse_discovered_total(output: str) -> int | None:
	for line in reversed(output.splitlines()):
		if not line.startswith("Total:"):
			continue
		try:
			return int(line.split(":", 1)[1].strip())
		except ValueError:
			return None
	return None


def summarize_git_status(output: str) -> dict[str, object]:
	lines = [line for line in output.splitlines() if line]
	branch = lines[0][3:] if lines and lines[0].startswith("## ") else "unknown"
	entries = lines[1:] if lines and lines[0].startswith("## ") else lines
	staged = 0
	unstaged = 0
	untracked = 0
	for line in entries:
		status = line[:2]
		if status == "??":
			untracked += 1
			continue
		if status and status[0] not in {" ", "?"}:
			staged += 1
		if len(status) > 1 and status[1] not in {" ", "?"}:
			unstaged += 1
	return {
		"branch": branch,
		"staged": staged,
		"unstaged": unstaged,
		"untracked": untracked,
		"dirty": bool(entries),
	}


def build_regression_args(args: argparse.Namespace, *, list_only: bool) -> tuple[str, ...]:
	command: list[str] = [
		args.python,
		str(REGRESSION_RUNNER.relative_to(REPO_ROOT)),
		"--godot",
		args.godot,
	]
	if getattr(args, "pattern", ""):
		command.extend(("--pattern", args.pattern))
	if getattr(args, "include_simulation", False):
		command.append("--include-simulation")
	if getattr(args, "include_benchmarks", False):
		command.append("--include-benchmarks")
	if list_only:
		command.append("--list")
	else:
		command.extend(
			(
				"--jobs",
				str(args.jobs),
				"--test-timeout-seconds",
				str(args.test_timeout_seconds),
				"--fail-on-output-error",
				"--lifecycle-correctness",
			)
		)
	return tuple(command)


def build_dotnet_args(args: argparse.Namespace) -> tuple[str, ...]:
	command = [args.dotnet, "build", "magic.csproj", "--nologo"]
	if not args.restore:
		command.append("--no-restore")
	return tuple(command)


def run_workflow(args: argparse.Namespace, command_runner: CommandRunner = run_command) -> tuple[list[StepResult], dict[str, object]]:
	steps: list[StepResult] = []
	status_result = command_runner(
		CommandSpec(
			"checkout",
			("git", "--no-optional-locks", "status", "--short", "--branch"),
			capture_output=True,
		),
		REPO_ROOT,
		args.dry_run,
	)
	steps.append(status_result)
	checkout_value = status_result.observations.get("checkout")
	checkout = (
		dict(checkout_value)
		if status_result.status == "passed" and isinstance(checkout_value, dict)
		else summarize_git_status(status_result.stdout_excerpt)
		if status_result.status == "passed"
		else {}
	)

	for name, command in (
		("diff_worktree", ("git", "--no-optional-locks", "diff", "--check")),
		("diff_index", ("git", "--no-optional-locks", "diff", "--cached", "--check")),
	):
		steps.append(command_runner(CommandSpec(name, command, capture_output=True), REPO_ROOT, args.dry_run))

	discovery_result = command_runner(
		CommandSpec("discover", build_regression_args(args, list_only=True), capture_output=True),
		REPO_ROOT,
		args.dry_run,
	)
	if discovery_result.status == "passed":
		discovered_total_value = discovery_result.observations.get("discovered_tests")
		discovered_total = (
			int(discovered_total_value)
			if isinstance(discovered_total_value, int)
			else parse_discovered_total(discovery_result.stdout_excerpt)
		)
		if discovered_total is None or discovered_total < 1:
			discovery_result = StepResult(
				discovery_result.name,
				discovery_result.command,
				"failed",
				1,
				discovery_result.duration_seconds,
				discovery_result.stdout_excerpt,
				discovery_result.stderr_excerpt,
				note="regression discovery did not report at least one matching test",
				observations={"discovered_tests": discovered_total},
			)
	else:
		discovered_total = None
	steps.append(discovery_result)

	build_result: StepResult | None = None
	if args.mode in {"quick", "focused", "full"}:
		build_result = command_runner(
			CommandSpec("build", build_dotnet_args(args), capture_output=False),
			REPO_ROOT,
			args.dry_run,
		)
		steps.append(build_result)

	if args.mode in {"focused", "full"}:
		if discovery_result.status == "failed":
			steps.append(skipped_step("regression", "discovery failed"))
		elif build_result is not None and build_result.status == "failed":
			steps.append(skipped_step("regression", "build failed"))
		else:
			steps.append(
				command_runner(
					CommandSpec("regression", build_regression_args(args, list_only=False), capture_output=False),
					REPO_ROOT,
					args.dry_run,
				)
			)

	metadata: dict[str, object] = {
		"checkout": checkout,
		"discovered_tests": discovered_total,
		"scope": "current_filesystem",
		"ci": "not_checked",
		"simulation_included": bool(getattr(args, "include_simulation", False)),
		"benchmarks_included": bool(getattr(args, "include_benchmarks", False)),
		"e2e_included": False,
	}
	return steps, metadata


def overall_status(steps: Sequence[StepResult], dry_run: bool) -> str:
	if dry_run:
		return "DRY_RUN"
	if any(step.status in {"failed", "skipped"} for step in steps):
		return "FAIL"
	return "PASS"


def print_captured_failure(step: StepResult) -> None:
	if step.status != "failed":
		return
	if step.stdout_excerpt:
		print(step.stdout_excerpt.rstrip(), file=sys.stderr)
	if step.stderr_excerpt:
		print(step.stderr_excerpt.rstrip(), file=sys.stderr)


def print_summary(mode: str, steps: Sequence[StepResult], metadata: dict[str, object], status: str) -> None:
	checkout = metadata.get("checkout") or {}
	print("\n== Magic Dev Check Summary ==")
	print(f"mode: {mode}")
	for step in steps:
		code = "" if step.returncode is None else f" exit={step.returncode}"
		note = "" if not step.note else f" ({step.note})"
		print(f"- {step.name}: {step.status}{code} {step.duration_seconds:.2f}s{note}")
	if checkout:
		print(
			"checkout: "
			f"{checkout.get('branch', 'unknown')} "
			f"staged={checkout.get('staged', 0)} "
			f"unstaged={checkout.get('unstaged', 0)} "
			f"untracked={checkout.get('untracked', 0)}"
		)
	print(f"discovered_tests: {metadata.get('discovered_tests')}")
	print(f"scope: {metadata['scope']}")
	print(f"ci: {metadata['ci']}")
	print(f"result: {status}")


def write_json_report(path_value: str, mode: str, steps: Sequence[StepResult], metadata: dict[str, object], status: str) -> Path:
	path = Path(path_value)
	if not path.is_absolute():
		path = REPO_ROOT / path
	path.parent.mkdir(parents=True, exist_ok=True)
	payload = {
		"schema_version": 1,
		"generated_at_utc": datetime.now(timezone.utc).isoformat(),
		"repo_root": str(REPO_ROOT),
		"mode": mode,
		"result": status,
		**metadata,
		"steps": [asdict(step) for step in steps],
	}
	path.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
	return path


def main(argv: Sequence[str] | None = None, command_runner: CommandRunner = run_command) -> int:
	args = parse_args(argv)
	print(f"Magic dev check: mode={args.mode} repo={REPO_ROOT}")
	steps, metadata = run_workflow(args, command_runner)
	for step in steps:
		print_captured_failure(step)
	status = overall_status(steps, args.dry_run)
	print_summary(args.mode, steps, metadata, status)
	if args.json_report:
		report_path = write_json_report(args.json_report, args.mode, steps, metadata, status)
		print(f"json_report: {report_path}")
	return 0 if status in {"PASS", "DRY_RUN"} else 1


if __name__ == "__main__":
	raise SystemExit(main())
