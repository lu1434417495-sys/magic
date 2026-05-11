#!/usr/bin/env python3

from __future__ import annotations

import argparse
import shutil
import subprocess
import sys
from pathlib import Path


def build_parser() -> argparse.ArgumentParser:
	parser = argparse.ArgumentParser(description="Run Godot headless regression scripts.")
	parser.add_argument("--godot", "-Godot", default="godot", help="Godot executable name or path.")
	parser.add_argument("--pattern", "-Pattern", default="", help="Only run tests whose repo path contains this text.")
	parser.add_argument("--list", "-List", action="store_true", dest="list_tests", help="List matching tests without running them.")
	parser.add_argument("--stop-on-failure", "-StopOnFailure", action="store_true", help="Stop after the first failing test.")
	parser.add_argument("--include-simulation", "-IncludeSimulation", action="store_true", help="Include battle simulation tests.")
	parser.add_argument("--include-benchmarks", "-IncludeBenchmarks", action="store_true", help="Include benchmark and analysis scripts.")
	return parser


def resolve_godot_command(command: str) -> str | None:
	candidate = Path(command)
	if candidate.exists():
		return str(candidate)
	return shutil.which(command)


def get_repo_path(repo_root: Path, path: Path) -> str:
	return path.resolve().relative_to(repo_root.resolve()).as_posix()


def should_skip_test(repo_path: str, pattern: str, include_simulation: bool, include_benchmarks: bool) -> bool:
	lower_path = repo_path.lower()
	if "/tools/" in lower_path:
		return True
	if not include_simulation and "/simulation/" in lower_path:
		return True
	if not include_benchmarks and (
		"/benchmarks/" in lower_path
		or lower_path.endswith("benchmark.gd")
		or lower_path.endswith("analysis.gd")
	):
		return True
	if pattern and pattern.lower() not in lower_path:
		return True
	return False


def main() -> int:
	args = build_parser().parse_args()
	script_root = Path(__file__).resolve().parent
	repo_root = script_root.parent
	godot_command = resolve_godot_command(args.godot)
	if godot_command is None:
		print(f"Godot executable not found: {args.godot}", file=sys.stderr)
		return 1

	tests_root = repo_root / "tests"
	tests = sorted(
		get_repo_path(repo_root, path)
		for path in tests_root.rglob("run_*.gd")
		if path.is_file()
	)
	tests = [
		path
		for path in tests
		if not should_skip_test(path, args.pattern, args.include_simulation, args.include_benchmarks)
	]

	if args.list_tests:
		for test_path in tests:
			print(test_path)
		print(f"Total: {len(tests)}")
		return 0

	if not tests:
		print("No matching regression tests found.", file=sys.stderr)
		return 1

	failed_tests: list[tuple[str, int]] = []
	passed_count = 0
	for test_path in tests:
		print(f"[RUN] {test_path}", flush=True)
		result = subprocess.run([godot_command, "--headless", "--script", test_path], cwd=repo_root)
		if result.returncode == 0:
			passed_count += 1
			print(f"[PASS] {test_path}", flush=True)
			continue

		failed_tests.append((test_path, result.returncode))
		print(f"[FAIL] {test_path} exit={result.returncode}", flush=True)
		if args.stop_on_failure:
			break

	print()
	print(f"Passed: {passed_count}")
	print(f"Failed: {len(failed_tests)}")

	if failed_tests:
		print("Failed tests:")
		for test_path, exit_code in failed_tests:
			print(f"- {test_path} exit={exit_code}")
		return 1

	return 0


if __name__ == "__main__":
	raise SystemExit(main())
