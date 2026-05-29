#!/usr/bin/env python3

from __future__ import annotations

import argparse
import shutil
import subprocess
import sys
import time
from pathlib import Path


def build_parser() -> argparse.ArgumentParser:
	parser = argparse.ArgumentParser(description="Run Godot headless regression scripts.")
	parser.add_argument("--godot", "-Godot", default="godot", help="Godot executable name or path.")
	parser.add_argument("--pattern", "-Pattern", default="", help="Only run tests whose repo path contains this text.")
	parser.add_argument("--list", "-List", action="store_true", dest="list_tests", help="List matching tests without running them.")
	parser.add_argument("--stop-on-failure", "-StopOnFailure", action="store_true", help="Stop after the first failing test.")
	parser.add_argument("--include-simulation", "-IncludeSimulation", action="store_true", help="Include battle simulation tests.")
	parser.add_argument("--include-benchmarks", "-IncludeBenchmarks", action="store_true", help="Include benchmark and analysis scripts.")
	parser.add_argument("--verbose", "-Verbose", action="store_true", help="Print test stdout/stderr in real time instead of capturing it.")
	parser.add_argument("--offset", type=int, default=0, help="Skip the first N tests (0-based).")
	parser.add_argument("--limit", type=int, default=0, help="Run at most N tests.")
	parser.add_argument("--log-file", type=str, default="", help="Append output to this file instead of stdout.")
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
	if args.log_file:
		log_path = Path(args.log_file)
		log_file = open(log_path, "a", encoding="utf-8")
		sys.stdout = log_file
		# Ensure stderr also goes to the log file so errors are captured
		sys.stderr = log_file
		# Reconfigure stdout to use utf-8 and not buffer lines too aggressively
		sys.stdout.reconfigure(encoding="utf-8", line_buffering=True)
	else:
		# Reconfigure stdout to UTF-8 on Windows to avoid GBK encoding issues
		try:
			sys.stdout.reconfigure(encoding="utf-8", line_buffering=True)
		except AttributeError:
			pass
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

	if args.offset:
		tests = tests[args.offset:]
	if args.limit:
		tests = tests[:args.limit]

	failed_tests: list[tuple[str, int, str, str]] = []
	passed_count = 0
	total = len(tests)
	for i, test_path in enumerate(tests, 1):
		print(f"\n[{i}/{total}] [RUN] {test_path}", flush=True)
		start = time.perf_counter()
		if args.verbose:
			result = subprocess.run([godot_command, "--headless", "--script", test_path], cwd=repo_root)
			stdout = ""
			stderr = ""
		else:
			result = subprocess.run(
				[godot_command, "--headless", "--script", test_path],
				cwd=repo_root,
				capture_output=True,
				text=True,
				encoding="utf-8",
				errors="replace",
			)
			stdout = result.stdout
			stderr = result.stderr
		elapsed = time.perf_counter() - start
		if result.returncode == 0:
			passed_count += 1
			print(f"[{i}/{total}] [DONE] {test_path} - 成功 ({elapsed:.2f}s)", flush=True)
			continue

		failed_tests.append((test_path, result.returncode, stdout, stderr))
		print(f"[{i}/{total}] [DONE] {test_path} - 失败 exit={result.returncode} ({elapsed:.2f}s)", flush=True)
		if not args.verbose and stdout:
			print("--- stdout ---", flush=True)
			print(stdout, flush=True)
		if not args.verbose and stderr:
			print("--- stderr ---", flush=True)
			print(stderr, flush=True)
		if args.stop_on_failure:
			break

	print()
	print(f"Passed: {passed_count}")
	print(f"Failed: {len(failed_tests)}")

	if failed_tests:
		print("Failed tests:")
		for test_path, exit_code, _stdout, _stderr in failed_tests:
			print(f"- {test_path} exit={exit_code}")
		return 1

	return 0


if __name__ == "__main__":
	raise SystemExit(main())
