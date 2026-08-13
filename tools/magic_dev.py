#!/usr/bin/env python3
"""Canonical repository CLI for inspection, focused validation, and routine tests.

The implementation deliberately invokes subprocesses without a shell so Codex and
developers do not need PowerShell pipelines for normal repository workflows.
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
from pathlib import Path, PurePosixPath
from typing import Callable, Sequence


CLI_VERSION = "1.0"
SCHEMA_VERSION = 1
REPO_ROOT = Path(__file__).resolve().parents[1]
REGRESSION_RUNNER = Path("tests/run_regression_suite.py")
DEFAULT_TIMEOUT_SECONDS = 180.0
MAX_DEFAULT_JOBS = 16


@dataclass(frozen=True)
class ProcessResult:
	command: tuple[str, ...]
	returncode: int | None
	duration_seconds: float
	stdout: str = ""
	stderr: str = ""
	planned: bool = False


@dataclass(frozen=True)
class TestTarget:
	path: str
	reasons: tuple[str, ...]
	kind: str = "godot"


@dataclass(frozen=True)
class AffectedSelection:
	mode: str
	changed_paths: tuple[str, ...]
	targets: tuple[TestTarget, ...]
	ignored_paths: tuple[str, ...]
	full_reasons: tuple[str, ...]


@dataclass
class WorkflowReport:
	operation: str
	result: str = "PASS"
	steps: list[dict[str, object]] = field(default_factory=list)
	metadata: dict[str, object] = field(default_factory=dict)


Executor = Callable[[Sequence[str], Path, bool, bool], ProcessResult]


ROUTES: tuple[tuple[str, tuple[str, ...]], ...] = (
	("scripts/systems/battle/", ("tests/battle_runtime",)),
	("scripts/enemies/", ("tests/battle_runtime/ai",)),
	("scripts/player/equipment/", ("tests/equipment", "tests/battle_runtime/runtime")),
	("scripts/player/progression/", ("tests/progression", "tests/battle_runtime/skills")),
	("scripts/systems/content/skills/", ("tests/progression", "tests/battle_runtime/skills")),
	("scripts/systems/world/", ("tests/world_map",)),
	("scripts/systems/game_runtime/headless/", ("tests/text_runtime",)),
	("scripts/systems/game_runtime/", ("tests/runtime", "tests/text_runtime", "tests/battle_runtime")),
	("scripts/ui/", ("tests/runtime", "tests/battle_runtime")),
	("data/configs/skills/", ("tests/progression", "tests/battle_runtime/skills")),
	("data/configs/enemies/", ("tests/battle_runtime/ai",)),
	("data/configs/items/", ("tests/equipment", "tests/warehouse")),
	("data/configs/equipment/", ("tests/equipment", "tests/battle_runtime/runtime")),
)

EXACT_ROUTES: dict[str, tuple[str, ...]] = {
	"tools/magic_dev.py": ("tests/tooling/test_magic_dev.py",),
	"tools/dev_check.py": ("tests/tooling/test_dev_check.py",),
}

FULL_EXACT = {
	"magic.csproj",
	"project.godot",
	"tests/run_regression_suite.py",
	"tests/tooling/test_run_regression_suite.py",
}

IGNORED_PREFIXES = (
	".tmp_battle_sim/",
	"docs/",
	"prompts/",
	"example/",
	".vscode/",
	".godot/",
)

SOURCE_SUFFIXES = {".cs", ".gd", ".py", ".tres", ".tscn", ".csproj"}
UNMERGED_CODES = {"DD", "AU", "UD", "UA", "DU", "AA", "UU"}
CONFLICT_SCAN_SKIP_PREFIXES = (".tmp_battle_sim/", ".godot/", ".vscode/", "tests/tmp/")


def positive_seconds(value: str) -> float:
	try:
		parsed = float(value)
	except ValueError as exc:
		raise argparse.ArgumentTypeError("must be a finite number greater than 0") from exc
	if not math.isfinite(parsed) or parsed <= 0:
		raise argparse.ArgumentTypeError("must be a finite number greater than 0")
	return parsed


def default_jobs(cpu_count: int | None = None) -> int:
	count = cpu_count if cpu_count is not None else (os.cpu_count() or 1)
	return max(1, min(MAX_DEFAULT_JOBS, (count + 1) // 2))


def format_command(command: Sequence[str]) -> str:
	return shlex.join(str(value) for value in command)


def run_process(command: Sequence[str], repo_root: Path, capture: bool, dry_run: bool) -> ProcessResult:
	argv = tuple(str(value) for value in command)
	print(f"[{'PLAN' if dry_run else 'RUN'}] {format_command(argv)}", file=sys.stderr, flush=True)
	if dry_run:
		return ProcessResult(argv, None, 0.0, planned=True)
	started = time.monotonic()
	try:
		completed = subprocess.run(
			argv,
			cwd=repo_root,
			text=True,
			encoding="utf-8",
			errors="replace",
			capture_output=capture,
			check=False,
		)
	except OSError as exc:
		return ProcessResult(argv, 127, time.monotonic() - started, stderr=str(exc))
	return ProcessResult(
		argv,
		completed.returncode,
		time.monotonic() - started,
		completed.stdout or "",
		completed.stderr or "",
	)


def capabilities_payload() -> dict[str, object]:
	return {
		"schema_version": SCHEMA_VERSION,
		"cli_version": CLI_VERSION,
		"cli": "tools/magic_dev.py",
		"operations": {
			"status": {"supported": True, "command": "python tools/magic_dev.py status"},
			"affected": {"supported": True, "command": "python tools/magic_dev.py affected"},
			"test": {
				"supported": True,
				"command": "python tools/magic_dev.py test --path <path>",
				"scope": "routine Godot regressions and Python tooling tests; E2E remains explicit",
			},
			"verify": {"supported": True, "command": "python tools/magic_dev.py verify"},
			"full": {
				"supported": True,
				"command": "python tools/magic_dev.py full",
				"scope": "routine suite only; simulation, benchmark, analysis, and E2E excluded",
			},
		},
	}


def normalized_repo_path(value: str, repo_root: Path = REPO_ROOT) -> str:
	text = value.strip().replace("\\", "/")
	if text.startswith("res://"):
		text = text[6:]
	path = Path(text)
	if path.is_absolute():
		try:
			path = path.resolve().relative_to(repo_root.resolve())
		except ValueError as exc:
			raise ValueError(f"path is outside repository: {value}") from exc
	normalized = PurePosixPath(path.as_posix())
	if normalized.is_absolute() or ".." in normalized.parts:
		raise ValueError(f"path is outside repository: {value}")
	result = normalized.as_posix()
	return result[2:] if result.startswith("./") else result


def parse_status_output(output: str) -> dict[str, object]:
	lines = [line for line in output.splitlines() if line]
	branch = "unknown"
	entries = lines
	if lines and lines[0].startswith("## "):
		branch = lines[0][3:]
		entries = lines[1:]
	staged: list[str] = []
	unstaged: list[str] = []
	untracked: list[str] = []
	unmerged: list[str] = []
	for line in entries:
		code = line[:2]
		path = line[3:] if len(line) > 3 else ""
		if code == "??":
			untracked.append(path)
			continue
		if code in UNMERGED_CODES:
			unmerged.append(path)
		if code[0] != " ":
			staged.append(path)
		if code[1] != " ":
			unstaged.append(path)
	return {
		"branch": branch,
		"staged": staged,
		"unstaged": unstaged,
		"untracked": untracked,
		"unmerged": unmerged,
		"dirty": bool(entries),
	}


def scan_conflict_markers(repo_root: Path, paths: Sequence[str]) -> tuple[dict[str, object], ...]:
	findings: list[dict[str, object]] = []
	markers = ("<<<<<<<", "=======", ">>>>>>>")
	for raw_path in sorted(set(paths)):
		path_text = raw_path.split(" -> ")[-1].strip('"')
		path_key = path_text.replace("\\", "/")
		if any(path_key.startswith(prefix) for prefix in CONFLICT_SCAN_SKIP_PREFIXES):
			continue
		path = repo_root / Path(path_text)
		if not path.is_file():
			continue
		try:
			content = path.read_bytes()
		except OSError:
			continue
		if b"\0" in content:
			continue
		for line_number, line in enumerate(content.decode("utf-8", errors="replace").splitlines(), 1):
			stripped = line.lstrip()
			marker = next((value for value in markers if stripped.startswith(value)), None)
			if marker:
				findings.append({"path": path_key, "line": line_number, "marker": marker})
	return tuple(findings)


def _run_capture(executor: Executor, command: Sequence[str], repo_root: Path) -> ProcessResult:
	return executor(command, repo_root, True, False)


def collect_status(repo_root: Path = REPO_ROOT, executor: Executor = run_process) -> tuple[dict[str, object], list[ProcessResult]]:
	commands = (
		("status", ("git", "--no-optional-locks", "status", "--short", "--branch", "--untracked-files=all")),
		("head", ("git", "--no-optional-locks", "rev-parse", "HEAD")),
		("upstream", ("git", "--no-optional-locks", "rev-parse", "--abbrev-ref", "--symbolic-full-name", "@{upstream}")),
		("ahead_behind", ("git", "--no-optional-locks", "rev-list", "--left-right", "--count", "HEAD...@{upstream}")),
		("diff_worktree", ("git", "--no-optional-locks", "diff", "--check")),
		("diff_index", ("git", "--no-optional-locks", "diff", "--cached", "--check")),
	)
	results: dict[str, ProcessResult] = {}
	for name, command in commands:
		results[name] = _run_capture(executor, command, repo_root)
	status_result = results["status"]
	parsed = parse_status_output(status_result.stdout) if status_result.returncode == 0 else {
		"branch": "unknown", "staged": [], "unstaged": [], "untracked": [], "unmerged": [], "dirty": False
	}
	upstream = results["upstream"].stdout.strip() if results["upstream"].returncode == 0 else None
	ahead = behind = None
	if upstream and results["ahead_behind"].returncode == 0:
		parts = results["ahead_behind"].stdout.split()
		if len(parts) == 2 and all(part.isdigit() for part in parts):
			ahead, behind = int(parts[0]), int(parts[1])
	payload = {
		"repo_root": str(repo_root),
		**parsed,
		"head": results["head"].stdout.strip() if results["head"].returncode == 0 else None,
		"upstream": upstream,
		"ahead": ahead,
		"behind": behind,
		"diff_worktree_ok": results["diff_worktree"].returncode == 0,
		"diff_index_ok": results["diff_index"].returncode == 0,
		"command_errors": [name for name, result in results.items() if result.returncode not in {0, None} and not (name in {"upstream", "ahead_behind"} and upstream is None)],
	}
	changed_paths = tuple(parsed["staged"] + parsed["unstaged"] + parsed["untracked"])
	payload["embedded_conflict_markers"] = scan_conflict_markers(repo_root, changed_paths)
	return payload, list(results.values())


def _null_paths(output: str) -> list[str]:
	return [value.replace("\\", "/") for value in output.split("\0") if value]


def collect_changed_paths(
	repo_root: Path = REPO_ROOT,
	executor: Executor = run_process,
	base: str = "",
) -> tuple[tuple[str, ...], list[ProcessResult]]:
	commands: list[tuple[str, ...]] = []
	if base:
		resolve = _run_capture(
			executor,
			("git", "--no-optional-locks", "rev-parse", "--verify", "--end-of-options", f"{base}^{{commit}}"),
			repo_root,
		)
		if resolve.returncode != 0:
			return (), [resolve]
		commands.append(("git", "--no-optional-locks", "diff", "--name-only", "-z", f"{resolve.stdout.strip()}...HEAD", "--"))
	commands.extend(
		(
			("git", "--no-optional-locks", "diff", "--name-only", "-z"),
			("git", "--no-optional-locks", "diff", "--cached", "--name-only", "-z"),
			("git", "--no-optional-locks", "ls-files", "--others", "--exclude-standard", "-z"),
		)
	)
	results: list[ProcessResult] = []
	paths: set[str] = set()
	for command in commands:
		result = _run_capture(executor, command, repo_root)
		results.append(result)
		if result.returncode == 0:
			paths.update(_null_paths(result.stdout))
	return tuple(sorted(paths)), results


def _target_kind(path: str) -> str:
	return "python" if path.endswith(".py") else "godot"


def _is_direct_test(path: str) -> bool:
	name = PurePosixPath(path).name
	if path.startswith("tests/") and name.startswith("test_") and name.endswith(".py"):
		return True
	return path.startswith("tests/") and name.startswith("run_") and PurePosixPath(path).suffix in {".cs", ".gd"}


def _is_opt_in_test(path: str) -> bool:
	lower = path.lower()
	return (
		lower.startswith("tests/e2e/")
		or "/simulation/" in lower
		or "/benchmarks/" in lower
		or "/tools/" in lower
		or lower.endswith("benchmark.cs")
		or lower.endswith("analysis.cs")
	)


def compact_targets(targets: dict[str, set[str]]) -> dict[str, set[str]]:
	paths = sorted(targets, key=lambda item: (len(PurePosixPath(item).parts), item))
	kept: dict[str, set[str]] = {}
	for path in paths:
		covered_by = next((parent for parent in kept if path != parent and path.startswith(parent.rstrip("/") + "/")), None)
		if covered_by and _target_kind(path) == _target_kind(covered_by):
			kept[covered_by].update(targets[path])
			continue
		kept[path] = set(targets[path])
	return kept


def select_affected(paths: Sequence[str]) -> AffectedSelection:
	targets: dict[str, set[str]] = {}
	ignored: list[str] = []
	full_reasons: list[str] = []
	for raw_path in paths:
		path = raw_path.replace("\\", "/")
		if path.startswith("./"):
			path = path[2:]
		if _is_opt_in_test(path):
			ignored.append(path)
			continue
		if path in FULL_EXACT or path.startswith("tests/shared/"):
			full_reasons.append(f"global validation owner changed: {path}")
			continue
		if _is_direct_test(path):
			targets.setdefault(path, set()).add(f"changed test: {path}")
			continue
		if path in EXACT_ROUTES:
			for target in EXACT_ROUTES[path]:
				targets.setdefault(target, set()).add(f"tool owner changed: {path}")
			continue
		if path == "tools/README.md" or path == "AGENTS.md" or any(path.startswith(prefix) for prefix in IGNORED_PREFIXES):
			ignored.append(path)
			continue
		matched = False
		for prefix, routed_targets in ROUTES:
			if not path.startswith(prefix):
				continue
			matched = True
			for target in routed_targets:
				targets.setdefault(target, set()).add(f"{prefix} changed: {path}")
			break
		if matched:
			continue
		if PurePosixPath(path).suffix.lower() in SOURCE_SUFFIXES:
			full_reasons.append(f"unmapped source or content path: {path}")
		else:
			ignored.append(path)
	compacted = compact_targets(targets)
	mode = "full" if full_reasons else "focused" if compacted else "none"
	return AffectedSelection(
		mode=mode,
		changed_paths=tuple(sorted(set(path.replace("\\", "/") for path in paths))),
		targets=tuple(
			TestTarget(path, tuple(sorted(reasons)), _target_kind(path))
			for path, reasons in sorted(compacted.items())
		),
		ignored_paths=tuple(sorted(set(ignored))),
		full_reasons=tuple(sorted(set(full_reasons))),
	)


def build_command(args: argparse.Namespace) -> tuple[str, ...]:
	command = [args.dotnet, "build", "magic.csproj", "--nologo"]
	if not args.restore:
		command.append("--no-restore")
	return tuple(command)


def regression_command(args: argparse.Namespace, pattern: str = "", list_only: bool = False) -> tuple[str, ...]:
	command = [args.python, str(REGRESSION_RUNNER), "--godot", args.godot]
	if pattern:
		command.extend(("--pattern", pattern))
	if getattr(args, "include_simulation", False):
		command.append("--include-simulation")
	if getattr(args, "include_benchmarks", False):
		command.append("--include-benchmarks")
	if list_only:
		command.append("--list")
	else:
		command.extend((
			"--jobs", str(args.jobs),
			"--test-timeout-seconds", str(args.test_timeout_seconds),
			"--fail-on-output-error",
			"--lifecycle-correctness",
		))
	return tuple(command)


def parse_discovered_total(output: str) -> int | None:
	for line in reversed(output.splitlines()):
		if line.startswith("Total:"):
			try:
				return int(line.split(":", 1)[1].strip())
			except ValueError:
				return None
	return None


def step_payload(name: str, result: ProcessResult) -> dict[str, object]:
	return {
		"name": name,
		"command": list(result.command),
		"returncode": result.returncode,
		"duration_seconds": round(result.duration_seconds, 3),
		"planned": result.planned,
	}


def _print_failure(result: ProcessResult) -> None:
	if result.returncode in {0, None}:
		return
	if result.stdout:
		print(result.stdout.rstrip(), file=sys.stderr)
	if result.stderr:
		print(result.stderr.rstrip(), file=sys.stderr)


def run_named_step(
	report: WorkflowReport,
	name: str,
	command: Sequence[str],
	args: argparse.Namespace,
	executor: Executor,
	*,
	capture: bool = False,
) -> ProcessResult:
	result = executor(command, REPO_ROOT, capture, args.dry_run)
	report.steps.append(step_payload(name, result))
	_print_failure(result)
	if result.returncode not in {0, None}:
		report.result = "FAIL"
	return result


def validate_test_path(value: str) -> str:
	path = normalized_repo_path(value)
	if not path.startswith("tests/"):
		raise ValueError("test path must be under tests/")
	if path.startswith("tests/e2e/") or path == "tests/e2e":
		raise ValueError("E2E is not part of the routine CLI; use tests/run_e2e_suite.py explicitly")
	resolved = REPO_ROOT / Path(path)
	if not resolved.exists():
		raise ValueError(f"test path does not exist: {path}")
	if resolved.is_file() and resolved.suffix == ".py" and not resolved.name.startswith("test_"):
		raise ValueError("Python test files must be named test_*.py")
	return path


def python_tests_for_target(target: str) -> tuple[str, ...]:
	resolved = REPO_ROOT / Path(target)
	if resolved.is_file():
		return (target,) if resolved.suffix == ".py" else ()
	python_tests = tuple(
		path.relative_to(REPO_ROOT).as_posix()
		for path in sorted(resolved.rglob("test_*.py"))
		if "__pycache__" not in path.parts
	)
	godot_tests = tuple(resolved.rglob("run_*.cs")) + tuple(resolved.rglob("run_*.gd"))
	if python_tests and godot_tests:
		raise ValueError("test directory mixes Python and Godot runners; select a narrower --path")
	return python_tests


def run_python_test(report: WorkflowReport, target: str, args: argparse.Namespace, executor: Executor) -> None:
	result = run_named_step(report, f"python:{target}", (args.python, target), args, executor)
	if result.returncode not in {0, None}:
		report.result = "FAIL"


def discover_godot_target(report: WorkflowReport, target: str, args: argparse.Namespace, executor: Executor) -> bool:
	result = run_named_step(
		report,
		f"discover:{target}",
		regression_command(args, target, list_only=True),
		args,
		executor,
		capture=True,
	)
	if result.planned:
		return True
	count = parse_discovered_total(result.stdout)
	if result.returncode != 0 or count is None or count < 1:
		if result.returncode == 0:
			print(f"No routine Godot regressions matched: {target}", file=sys.stderr)
		report.result = "FAIL"
		return False
	return True


def run_godot_targets(report: WorkflowReport, targets: Sequence[str], args: argparse.Namespace, executor: Executor) -> None:
	valid = [target for target in targets if discover_godot_target(report, target, args, executor)]
	if len(valid) != len(targets):
		return
	if not getattr(args, "no_build", False):
		build = run_named_step(report, "build", build_command(args), args, executor)
		if build.returncode not in {0, None}:
			return
	for target in valid:
		result = run_named_step(
			report,
			f"regression:{target}",
			regression_command(args, target),
			args,
			executor,
		)
		if result.returncode not in {0, None}:
			return


def status_is_valid(status: dict[str, object]) -> bool:
	return (
		not status["command_errors"]
		and status["diff_worktree_ok"]
		and status["diff_index_ok"]
		and not status["unmerged"]
		and not status["embedded_conflict_markers"]
	)


def run_full_workflow(args: argparse.Namespace, executor: Executor = run_process) -> WorkflowReport:
	report = WorkflowReport("full")
	status, status_steps = collect_status(REPO_ROOT, executor)
	report.metadata["status"] = status
	report.steps.extend(step_payload(f"status:{index}", step) for index, step in enumerate(status_steps, 1))
	if not status_is_valid(status):
		report.result = "FAIL"
		return report
	discovery = run_named_step(report, "discover:full", regression_command(args, list_only=True), args, executor, capture=True)
	if not discovery.planned:
		count = parse_discovered_total(discovery.stdout)
		report.metadata["discovered_tests"] = count
		if discovery.returncode != 0 or count is None or count < 1:
			report.result = "FAIL"
			return report
	build = run_named_step(report, "build", build_command(args), args, executor)
	if build.returncode not in {0, None}:
		return report
	run_named_step(report, "regression:full", regression_command(args), args, executor)
	return report


def run_verify_workflow(args: argparse.Namespace, executor: Executor = run_process) -> WorkflowReport:
	report = WorkflowReport("verify")
	status, status_steps = collect_status(REPO_ROOT, executor)
	report.metadata["status"] = status
	report.steps.extend(step_payload(f"status:{index}", step) for index, step in enumerate(status_steps, 1))
	if not status_is_valid(status):
		report.result = "FAIL"
		return report
	changed, change_steps = collect_changed_paths(REPO_ROOT, executor, args.base)
	report.steps.extend(step_payload(f"affected:{index}", step) for index, step in enumerate(change_steps, 1))
	if any(step.returncode != 0 for step in change_steps):
		report.result = "FAIL"
		return report
	selection = select_affected(changed)
	report.metadata["affected"] = asdict(selection)
	if selection.mode == "full":
		discovery = run_named_step(report, "discover:full", regression_command(args, list_only=True), args, executor, capture=True)
		if not discovery.planned:
			count = parse_discovered_total(discovery.stdout)
			report.metadata["discovered_tests"] = count
			if discovery.returncode != 0 or count is None or count < 1:
				report.result = "FAIL"
				return report
		build = run_named_step(report, "build", build_command(args), args, executor)
		if build.returncode in {0, None}:
			run_named_step(report, "regression:full", regression_command(args), args, executor)
		return report
	python_targets = [target.path for target in selection.targets if target.kind == "python"]
	godot_targets = [target.path for target in selection.targets if target.kind == "godot"]
	for target in python_targets:
		run_python_test(report, target, args, executor)
		if report.result == "FAIL":
			return report
	if godot_targets:
		run_godot_targets(report, godot_targets, args, executor)
	return report


def write_report(path_value: str, report: WorkflowReport) -> Path:
	path = Path(path_value)
	if not path.is_absolute():
		path = REPO_ROOT / path
	path.parent.mkdir(parents=True, exist_ok=True)
	payload = {
		"schema_version": SCHEMA_VERSION,
		"generated_at_utc": datetime.now(timezone.utc).isoformat(),
		"repo_root": str(REPO_ROOT),
		**asdict(report),
	}
	path.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
	return path


def print_status(status: dict[str, object]) -> None:
	print("Magic repository status")
	print(f"repo: {status['repo_root']}")
	print(f"branch: {status['branch']}")
	print(f"head: {status['head']}")
	print(f"upstream: {status['upstream'] or 'none'}")
	print(f"ahead/behind: {status['ahead']}/{status['behind']}")
	print(f"staged: {len(status['staged'])}")
	print(f"unstaged: {len(status['unstaged'])}")
	print(f"untracked: {len(status['untracked'])}")
	print(f"unmerged: {len(status['unmerged'])}")
	print(f"embedded_conflict_markers: {len(status['embedded_conflict_markers'])}")
	print(f"diff_check: worktree={'PASS' if status['diff_worktree_ok'] else 'FAIL'} index={'PASS' if status['diff_index_ok'] else 'FAIL'}")
	print(f"result: {'OK' if status_is_valid(status) else 'ATTENTION'}")


def print_affected(selection: AffectedSelection) -> None:
	print("Magic affected validation")
	print(f"changed_paths: {len(selection.changed_paths)}")
	print(f"selection: {selection.mode}")
	for target in selection.targets:
		print(f"- {target.path} [{target.kind}]")
	for reason in selection.full_reasons:
		print(f"- FULL: {reason}")
	print(f"ignored_paths: {len(selection.ignored_paths)}")


def print_workflow(report: WorkflowReport) -> None:
	print(f"\nMagic {report.operation} summary")
	for step in report.steps:
		code = "planned" if step["planned"] else f"exit={step['returncode']}"
		print(f"- {step['name']}: {code} {step['duration_seconds']:.3f}s")
	print("scope: current_filesystem")
	print("ci: not_checked")
	print("simulation: excluded")
	print("benchmark: excluded")
	print("e2e: excluded")
	print(f"result: {report.result}")


def add_execution_options(parser: argparse.ArgumentParser) -> None:
	parser.add_argument("--python", default=sys.executable)
	parser.add_argument("--dotnet", default="dotnet")
	parser.add_argument("--godot", default="godot")
	parser.add_argument("--jobs", default=str(default_jobs()))
	parser.add_argument("--test-timeout-seconds", type=positive_seconds, default=DEFAULT_TIMEOUT_SECONDS)
	parser.add_argument("--restore", action="store_true", help="Allow dotnet restore; default build uses --no-restore.")
	parser.add_argument("--dry-run", action="store_true")
	parser.add_argument("--json-report", default="")


def build_parser() -> argparse.ArgumentParser:
	parser = argparse.ArgumentParser(description="Canonical Magic repository development CLI.")
	subparsers = parser.add_subparsers(dest="operation", required=True)
	capabilities = subparsers.add_parser("capabilities", help="Describe supported operations.")
	capabilities.add_argument("--json", action="store_true")
	status = subparsers.add_parser("status", help="Inspect checkout and diff hygiene.")
	status.add_argument("--json", action="store_true")
	affected = subparsers.add_parser("affected", help="Select validation targets from local changes.")
	affected.add_argument("--base", default="", help="Also include committed changes from BASE...HEAD.")
	affected.add_argument("--json", action="store_true")
	test = subparsers.add_parser("test", help="Run one test file or test directory.")
	test.add_argument("--path", required=True)
	test.add_argument("--no-build", action="store_true")
	test.add_argument("--include-simulation", action="store_true")
	test.add_argument("--include-benchmarks", action="store_true")
	add_execution_options(test)
	verify = subparsers.add_parser("verify", help="Validate the current affected scope.")
	verify.add_argument("--base", default="")
	add_execution_options(verify)
	full = subparsers.add_parser("full", help="Run build and the routine full regression suite.")
	add_execution_options(full)
	return parser


def main(argv: Sequence[str] | None = None, executor: Executor = run_process) -> int:
	args = build_parser().parse_args(argv)
	if args.operation == "capabilities":
		payload = capabilities_payload()
		print(json.dumps(payload, ensure_ascii=False, indent=2) if args.json else "\n".join(
			f"{name}: supported={details['supported']}" for name, details in payload["operations"].items()
		))
		return 0
	if args.operation == "status":
		status, steps = collect_status(REPO_ROOT, executor)
		if args.json:
			print(json.dumps(status, ensure_ascii=False, indent=2))
		else:
			print_status(status)
		return 1 if any(step.returncode not in {0, None} for step in steps[:2]) else 0
	if args.operation == "affected":
		paths, steps = collect_changed_paths(REPO_ROOT, executor, args.base)
		if any(step.returncode != 0 for step in steps):
			for step in steps:
				_print_failure(step)
			return 1
		selection = select_affected(paths)
		if args.json:
			print(json.dumps(asdict(selection), ensure_ascii=False, indent=2))
		else:
			print_affected(selection)
		return 0
	if args.operation == "test":
		try:
			target = validate_test_path(args.path)
			python_targets = python_tests_for_target(target)
		except ValueError as exc:
			print(f"magic_dev: {exc}", file=sys.stderr)
			return 2
		report = WorkflowReport("test", metadata={"target": target})
		if python_targets:
			for python_target in python_targets:
				run_python_test(report, python_target, args, executor)
				if report.result == "FAIL":
					break
		else:
			run_godot_targets(report, (target,), args, executor)
	elif args.operation == "verify":
		report = run_verify_workflow(args, executor)
	else:
		report = run_full_workflow(args, executor)
	print_workflow(report)
	if args.json_report:
		print(f"json_report: {write_report(args.json_report, report)}")
	return 0 if report.result == "PASS" else 1


if __name__ == "__main__":
	raise SystemExit(main())
