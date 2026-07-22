#!/usr/bin/env python3

from __future__ import annotations

import argparse
import concurrent.futures
import math
import os
import re
import signal
import shutil
import subprocess
import sys
import tempfile
import threading
import time
from dataclasses import dataclass
from pathlib import Path


SLOW_TEST_SECONDS = 30.0
DEFAULT_TEST_TIMEOUT_SECONDS = 180.0
TEST_TIMEOUT_EXIT_CODE = 124
OUTPUT_ERROR_PREFIXES = (
	"ERROR:",
	"SCRIPT ERROR:",
	"FATAL:",
)
LEAKED_UNSAFE_REFERENCE_PREFIX = "ERROR: Leaked unsafe reference to object:"
OBJECTDB_LEAK_PREFIX = "WARNING: ObjectDB instances leaked at exit"
RESOURCE_LEAK_PATTERN = re.compile(r"^ERROR:\s+\d+\s+resources still in use at exit\b")
LIFECYCLE_FATAL_MARKERS = (
	"gchandle.is_released",
	"GodotObject.Finalize",
	"Handle is not initialized",
	"godotsharp_variant_destroy",
)
LIFECYCLE_SHUTDOWN_REPORT_PREFIX = "[lifecycle] shutdown-report "
LIFECYCLE_LEGACY_DEBT_PATTERN = re.compile(r"\blegacy_debt=(?P<count>\d+)\b")


@dataclass(frozen=True)
class TestRunResult:
	index: int
	total: int
	test_path: str
	returncode: int
	stdout: str
	stderr: str
	elapsed: float
	user_data_dir: str = ""
	output_error_lines: tuple[str, ...] = ()
	lifecycle_fatal_lines: tuple[str, ...] = ()


def build_parser() -> argparse.ArgumentParser:
	parser = argparse.ArgumentParser(description="Run Godot headless regression scripts.")
	parser.add_argument("--godot", "-Godot", default="godot", help="Godot executable name or path.")
	parser.add_argument("--pattern", "-Pattern", default="", help="Only run tests whose repo path contains this text.")
	parser.add_argument("--list", "-List", action="store_true", dest="list_tests", help="List matching tests without running them.")
	parser.add_argument("--stop-on-failure", "-StopOnFailure", action="store_true", help="Stop after the first failing test.")
	parser.add_argument("--include-simulation", "-IncludeSimulation", action="store_true", help="Include battle simulation tests.")
	parser.add_argument("--include-benchmarks", "-IncludeBenchmarks", action="store_true", help="Include benchmark and analysis scripts.")
	parser.add_argument(
		"--verbose",
		"-Verbose",
		action="store_true",
		help="Print stdout/stderr in real time for --jobs 1; with --jobs > 1, print captured output after each test completes.",
	)
	parser.add_argument("--offset", type=int, default=0, help="Skip the first N tests (0-based).")
	parser.add_argument("--limit", type=int, default=0, help="Run at most N tests.")
	parser.add_argument("--log-file", type=str, default="", help="Append output to this file instead of stdout.")
	parser.add_argument("--jobs", "-j", default="1", help="Number of tests to run in parallel, or 'auto'. Default: 1.")
	parser.add_argument(
		"--lifecycle-correctness",
		action="store_true",
		help=(
			"Run with strict lifecycle diagnostics, fatal post-exit GodotSharp marker "
			"detection, and zero legacy debt."
		),
	)
	parser.add_argument(
		"--user-data-root",
		type=str,
		default="",
		help="Directory for per-test Godot user data when --jobs > 1. Defaults to a temporary directory.",
	)
	parser.add_argument(
		"--keep-user-data",
		action="store_true",
		help="Keep the temporary per-test user data directory created for --jobs > 1. Failed parallel runs keep it automatically.",
	)
	parser.add_argument(
		"--fail-on-output-error",
		action="store_true",
		help="Treat Godot ERROR/SCRIPT ERROR/FATAL output as a failed test even when Godot exits 0.",
	)
	parser.add_argument(
		"--test-timeout-seconds",
		type=positive_finite_seconds,
		default=DEFAULT_TEST_TIMEOUT_SECONDS,
		help=f"Terminate an individual Godot test after this many seconds. Default: {DEFAULT_TEST_TIMEOUT_SECONDS:.0f}.",
	)
	return parser


def positive_finite_seconds(value: str) -> float:
	try:
		seconds = float(value)
	except ValueError as exc:
		raise argparse.ArgumentTypeError("must be a finite number greater than 0") from exc
	if not math.isfinite(seconds) or seconds <= 0:
		raise argparse.ArgumentTypeError("must be a finite number greater than 0")
	return seconds


def resolve_godot_command(command: str) -> str | None:
	candidate = Path(command)
	if candidate.exists():
		return str(candidate)
	return shutil.which(command)


def build_child_process_env(
	base_env: dict[str, str],
	lifecycle_correctness: bool,
) -> dict[str, str]:
	env = dict(base_env)
	if lifecycle_correctness:
		env["MAGIC_LIFECYCLE_STRICT"] = "1"
		env["MAGIC_LIFECYCLE_TRACE"] = "1"
	return env


def get_repo_path(repo_root: Path, path: Path) -> str:
	return path.resolve().relative_to(repo_root.resolve()).as_posix()


def should_skip_test(repo_path: str, pattern: str, include_simulation: bool, include_benchmarks: bool) -> bool:
	lower_path = repo_path.lower()
	if "/tools/" in lower_path:
		return True
	if "/e2e/" in lower_path:
		return True
	if not include_simulation and "/simulation/" in lower_path:
		return True
	if not include_benchmarks and (
		"/benchmarks/" in lower_path
		or lower_path.endswith("benchmark.cs")
		or lower_path.endswith("analysis.cs")
	):
		return True
	if pattern and pattern.lower() not in lower_path:
		return True
	return False


def resolve_jobs(value: str, total: int) -> int:
	normalized = str(value).strip().lower()
	if normalized == "auto":
		return max(1, min(os.cpu_count() or 1, total))
	try:
		jobs = int(normalized)
	except ValueError as exc:
		raise ValueError("--jobs must be a positive integer or 'auto'.") from exc
	if jobs < 1:
		raise ValueError("--jobs must be >= 1.")
	return max(1, min(jobs, total))


def sanitize_test_path(test_path: str) -> str:
	result = "".join(c if c.isalnum() else "_" for c in test_path)
	return result.strip("_")[:120] or "test"


def prepare_user_data_env(base_env: dict[str, str], user_data_dir: Path) -> dict[str, str]:
	env = dict(base_env)
	xdg_data = user_data_dir / "xdg_data"
	xdg_config = user_data_dir / "xdg_config"
	xdg_cache = user_data_dir / "xdg_cache"
	xdg_data.mkdir(parents=True, exist_ok=True)
	xdg_config.mkdir(parents=True, exist_ok=True)
	xdg_cache.mkdir(parents=True, exist_ok=True)
	env["XDG_DATA_HOME"] = str(xdg_data)
	env["XDG_CONFIG_HOME"] = str(xdg_config)
	env["XDG_CACHE_HOME"] = str(xdg_cache)
	if os.name == "nt":
		appdata = user_data_dir / "AppData" / "Roaming"
		local_appdata = user_data_dir / "AppData" / "Local"
		appdata.mkdir(parents=True, exist_ok=True)
		local_appdata.mkdir(parents=True, exist_ok=True)
		env["APPDATA"] = str(appdata)
		env["LOCALAPPDATA"] = str(local_appdata)
	return env


def find_output_error_lines(stdout: str, stderr: str, max_lines: int = 20) -> tuple[str, ...]:
	matches: list[str] = []
	for stream_name, text in (("stdout", stdout or ""), ("stderr", stderr or "")):
		for line in text.splitlines():
			normalized = line.lstrip("\ufeff").strip()
			if not (
				normalized.startswith(OUTPUT_ERROR_PREFIXES)
				or normalized.startswith(OBJECTDB_LEAK_PREFIX)
			):
				continue
			matches.append(f"{stream_name}: {line}")
			if len(matches) >= max_lines:
				return tuple(matches)
	return tuple(matches)


def find_lifecycle_fatal_lines(
	stdout: str,
	stderr: str,
	max_lines: int = 20,
) -> tuple[str, ...]:
	matches: list[str] = []
	for stream_name, text in (("stdout", stdout or ""), ("stderr", stderr or "")):
		for line in text.splitlines():
			normalized = line.lstrip("\ufeff").strip()
			has_fatal_marker = any(marker in line for marker in LIFECYCLE_FATAL_MARKERS)
			has_shutdown_leak = (
				normalized.startswith(LEAKED_UNSAFE_REFERENCE_PREFIX)
				or normalized.startswith(OBJECTDB_LEAK_PREFIX)
				or RESOURCE_LEAK_PATTERN.search(normalized) is not None
			)
			legacy_debt_match = (
				LIFECYCLE_LEGACY_DEBT_PATTERN.search(line)
				if LIFECYCLE_SHUTDOWN_REPORT_PREFIX in line
				else None
			)
			has_legacy_debt = (
				legacy_debt_match is not None
				and int(legacy_debt_match.group("count")) != 0
			)
			if not has_fatal_marker and not has_shutdown_leak and not has_legacy_debt:
				continue
			matches.append(f"{stream_name}: {line}")
			if len(matches) >= max_lines:
				return tuple(matches)
	return tuple(matches)


def terminate_process_tree(process: subprocess.Popen[str]) -> None:
	if os.name == "nt":
		try:
			subprocess.run(
				["taskkill", "/PID", str(process.pid), "/T", "/F"],
				stdout=subprocess.DEVNULL,
				stderr=subprocess.DEVNULL,
				check=False,
				timeout=10,
			)
		except (OSError, subprocess.TimeoutExpired):
			pass
		try:
			process.wait(timeout=3)
		except subprocess.TimeoutExpired:
			try:
				process.kill()
			except OSError:
				pass
			try:
				process.wait(timeout=3)
			except subprocess.TimeoutExpired:
				pass
		return

	try:
		os.killpg(process.pid, signal.SIGTERM)
	except (OSError, ProcessLookupError):
		pass
	try:
		process.wait(timeout=3)
	except subprocess.TimeoutExpired:
		pass
	try:
		os.killpg(process.pid, signal.SIGKILL)
	except (OSError, ProcessLookupError):
		pass
	try:
		process.wait(timeout=3)
	except subprocess.TimeoutExpired:
		pass


def join_output_threads(
	stdout_thread: threading.Thread,
	stderr_thread: threading.Thread,
	timeout_seconds: float = 5.0,
) -> bool:
	stdout_thread.join(timeout=timeout_seconds)
	stderr_thread.join(timeout=timeout_seconds)
	stalled = stdout_thread.is_alive() or stderr_thread.is_alive()
	return stalled


def run_godot_process(
	godot_command: str,
	repo_root: Path,
	test_path: str,
	env: dict[str, str] | None,
	realtime_output: bool,
	test_timeout_seconds: float,
) -> tuple[int, str, str, tuple[str, ...]]:
	process = subprocess.Popen(
		[godot_command, "--headless", "--script", test_path],
		cwd=repo_root,
		env=env,
		stdout=subprocess.PIPE,
		stderr=subprocess.PIPE,
		text=True,
		encoding="utf-8",
		errors="replace",
		bufsize=1,
		start_new_session=os.name != "nt",
	)
	stdout_result: dict[str, object] = {}
	stderr_result: dict[str, object] = {}

	def collect_stream(stream, sink: dict[str, object], echo: bool) -> None:
		lines: list[str] = []
		try:
			for line in stream:
				lines.append(line)
				if echo:
					print(line, end="", flush=True)
		except (OSError, ValueError):
			pass
		finally:
			try:
				stream.close()
			except Exception:
				pass
		sink["text"] = "".join(lines)

	stdout_thread = threading.Thread(
		target=collect_stream,
		args=(process.stdout, stdout_result, realtime_output),
		daemon=True,
	)
	stderr_thread = threading.Thread(
		target=collect_stream,
		args=(process.stderr, stderr_result, realtime_output),
		daemon=True,
	)
	stdout_thread.start()
	stderr_thread.start()
	timed_out = False
	try:
		returncode = process.wait(timeout=test_timeout_seconds)
	except subprocess.TimeoutExpired:
		timed_out = True
		terminate_process_tree(process)
		returncode = TEST_TIMEOUT_EXIT_CODE
	output_threads_stalled = join_output_threads(stdout_thread, stderr_thread)

	stdout = str(stdout_result.get("text", ""))
	stderr = str(stderr_result.get("text", ""))
	if timed_out:
		timeout_text = f"[runner] test timed out after {test_timeout_seconds:g} seconds.\n"
		stderr += timeout_text
		if realtime_output:
			print(timeout_text, end="", file=sys.stderr, flush=True)
	if output_threads_stalled:
		stderr += "[runner] output reader threads did not stop cleanly after process exit.\n"
	return returncode, stdout, stderr, find_output_error_lines(stdout, stderr)


def run_one_test(
	godot_command: str,
	repo_root: Path,
	test_path: str,
	index: int,
	total: int,
	realtime_output: bool,
	user_data_root: Path | None,
	fail_on_output_error: bool,
	test_timeout_seconds: float,
	child_process_env: dict[str, str] | None = None,
	lifecycle_correctness: bool = False,
) -> TestRunResult:
	start = time.perf_counter()
	user_data_dir = ""
	if user_data_root is not None:
		test_user_data_dir = user_data_root / f"{index:04d}_{sanitize_test_path(test_path)}"
		user_data_dir = str(test_user_data_dir)
	stdout = ""
	stderr = ""
	base_env = child_process_env if child_process_env is not None else os.environ
	env = build_child_process_env(base_env, lifecycle_correctness)
	if user_data_root is not None:
		env = prepare_user_data_env(base_env, Path(user_data_dir))
		env = build_child_process_env(env, lifecycle_correctness)
	returncode, stdout, stderr, detected_output_error_lines = run_godot_process(
		godot_command,
		repo_root,
		test_path,
		env,
		realtime_output,
		test_timeout_seconds,
	)
	lifecycle_fatal_lines = find_lifecycle_fatal_lines(stdout, stderr)
	output_error_lines = detected_output_error_lines if fail_on_output_error else ()
	if returncode == 0 and (lifecycle_fatal_lines or output_error_lines):
		returncode = 1
	elapsed = time.perf_counter() - start
	return TestRunResult(
		index=index,
		total=total,
		test_path=test_path,
		returncode=returncode,
		stdout=stdout,
		stderr=stderr,
		elapsed=elapsed,
		user_data_dir=user_data_dir,
		output_error_lines=output_error_lines,
		lifecycle_fatal_lines=lifecycle_fatal_lines,
	)


def print_test_result(result: TestRunResult, show_output: bool) -> None:
	if result.elapsed > SLOW_TEST_SECONDS:
		print(
			f"[{result.index}/{result.total}] [SLOW] {result.test_path} - "
			f"执行耗时 {result.elapsed:.2f}s，超过 {SLOW_TEST_SECONDS:.0f}s 阈值",
			flush=True,
		)
	if result.returncode == 0:
		print(
			f"[{result.index}/{result.total}] [DONE] {result.test_path} - "
			f"成功 ({result.elapsed:.2f}s)",
			flush=True,
		)
	else:
		print(
			f"[{result.index}/{result.total}] [DONE] {result.test_path} - "
			f"失败 exit={result.returncode} ({result.elapsed:.2f}s)",
			flush=True,
		)
	if result.output_error_lines:
		print("--- output error markers ---", flush=True)
		for line in result.output_error_lines:
			print(line, flush=True)
	if result.lifecycle_fatal_lines:
		print("--- lifecycle fatal markers ---", flush=True)
		for line in result.lifecycle_fatal_lines:
			print(line, flush=True)
	if show_output and result.stdout:
		print("--- stdout ---", flush=True)
		print(result.stdout, flush=True)
	if show_output and result.stderr:
		print("--- stderr ---", flush=True)
		print(result.stderr, flush=True)


def create_user_data_root(args: argparse.Namespace) -> tuple[Path, bool]:
	if args.user_data_root:
		root = Path(args.user_data_root).resolve()
		root.mkdir(parents=True, exist_ok=True)
		return root, False
	return Path(tempfile.mkdtemp(prefix="godot-regression-user-data-")), not args.keep_user_data


def run_tests_serial(
	godot_command: str,
	repo_root: Path,
	tests: list[str],
	verbose: bool,
	stop_on_failure: bool,
	fail_on_output_error: bool,
	test_timeout_seconds: float,
	child_process_env: dict[str, str] | None = None,
	lifecycle_correctness: bool = False,
) -> tuple[int, list[TestRunResult]]:
	failed_tests: list[TestRunResult] = []
	passed_count = 0
	total = len(tests)
	for index, test_path in enumerate(tests, 1):
		print(f"\n[{index}/{total}] [RUN] {test_path}", flush=True)
		result = run_one_test(
			godot_command,
			repo_root,
			test_path,
			index,
			total,
			# Echo serial child output live while retaining it for strict marker
			# classification after the process exits.
			realtime_output=True,
			user_data_root=None,
			fail_on_output_error=fail_on_output_error,
			test_timeout_seconds=test_timeout_seconds,
			child_process_env=child_process_env,
			lifecycle_correctness=lifecycle_correctness,
		)
		if result.returncode == 0:
			passed_count += 1
			print_test_result(result, show_output=False)
			continue
		failed_tests.append(result)
		print_test_result(result, show_output=not verbose)
		if stop_on_failure:
			break
	return passed_count, failed_tests


def run_tests_parallel(
	godot_command: str,
	repo_root: Path,
	tests: list[str],
	jobs: int,
	verbose: bool,
	stop_on_failure: bool,
	user_data_root: Path,
	fail_on_output_error: bool,
	test_timeout_seconds: float,
	child_process_env: dict[str, str] | None = None,
	lifecycle_correctness: bool = False,
) -> tuple[int, list[TestRunResult]]:
	failed_tests: list[TestRunResult] = []
	passed_count = 0
	total = len(tests)
	next_index = 0
	stop_submitting = False
	stop_notice_printed = False

	def submit_next(executor: concurrent.futures.Executor, active: dict[concurrent.futures.Future[TestRunResult], str]) -> None:
		nonlocal next_index
		if next_index >= total:
			return
		test_path = tests[next_index]
		index = next_index + 1
		next_index += 1
		print(f"\n[{index}/{total}] [RUN] {test_path}", flush=True)
		future = executor.submit(
			run_one_test,
			godot_command,
			repo_root,
			test_path,
			index,
			total,
			False,
			user_data_root,
			fail_on_output_error,
			test_timeout_seconds,
			child_process_env,
			lifecycle_correctness,
		)
		active[future] = test_path

	with concurrent.futures.ThreadPoolExecutor(max_workers=jobs) as executor:
		active: dict[concurrent.futures.Future[TestRunResult], str] = {}
		for _ in range(min(jobs, total)):
			submit_next(executor, active)
		while active:
			done, _pending = concurrent.futures.wait(
				active,
				return_when=concurrent.futures.FIRST_COMPLETED,
			)
			for future in done:
				active.pop(future, None)
				result = future.result()
				if result.returncode == 0:
					passed_count += 1
					print_test_result(
						result,
						show_output=verbose or lifecycle_correctness,
					)
				else:
					failed_tests.append(result)
					print_test_result(result, show_output=True)
					if stop_on_failure:
						stop_submitting = True
				if not stop_submitting:
					submit_next(executor, active)
			if stop_submitting and active and not stop_notice_printed:
				print(
					f"[STOP] --stop-on-failure requested; waiting for {len(active)} already running test(s).",
					flush=True,
				)
				stop_notice_printed = True
	return passed_count, failed_tests


def main() -> int:
	parser = build_parser()
	args = parser.parse_args()
	child_process_env = build_child_process_env(
		dict(os.environ),
		args.lifecycle_correctness,
	)
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
		for path in tests_root.rglob("run_*.cs")
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

	total = len(tests)
	try:
		jobs = resolve_jobs(args.jobs, total)
	except ValueError as exc:
		print(str(exc), file=sys.stderr)
		return 1

	if jobs == 1:
		passed_count, failed_tests = run_tests_serial(
			godot_command,
			repo_root,
			tests,
			args.verbose,
			args.stop_on_failure,
			args.fail_on_output_error,
			args.test_timeout_seconds,
			child_process_env,
			args.lifecycle_correctness,
		)
	else:
		user_data_root, cleanup_user_data_root = create_user_data_root(args)
		print(f"Running {total} test(s) with {jobs} parallel job(s).", flush=True)
		print(f"Per-test Godot user data root: {user_data_root}", flush=True)
		if args.verbose:
			print("[INFO] --verbose with --jobs prints captured output after each test completes.", flush=True)
		failed_tests = []
		try:
			passed_count, failed_tests = run_tests_parallel(
				godot_command,
				repo_root,
				tests,
				jobs,
				args.verbose,
				args.stop_on_failure,
				user_data_root,
				args.fail_on_output_error,
				args.test_timeout_seconds,
				child_process_env,
				args.lifecycle_correctness,
			)
		finally:
			if cleanup_user_data_root and not failed_tests:
				shutil.rmtree(user_data_root, ignore_errors=True)
			else:
				print(f"Kept per-test Godot user data root: {user_data_root}", flush=True)

	print()
	print(f"Passed: {passed_count}")
	print(f"Failed: {len(failed_tests)}")

	if failed_tests:
		print("Failed tests:")
		for result in sorted(failed_tests, key=lambda item: item.index):
			suffix = f" user_data={result.user_data_dir}" if result.user_data_dir else ""
			output_error_suffix = (
				f" output_errors={len(result.output_error_lines)}"
				if result.output_error_lines
				else ""
			)
			lifecycle_fatal_suffix = (
				f" lifecycle_fatals={len(result.lifecycle_fatal_lines)}"
				if result.lifecycle_fatal_lines
				else ""
			)
			print(
				f"- {result.test_path} exit={result.returncode}{suffix}"
				f"{output_error_suffix}{lifecycle_fatal_suffix}"
			)
		return 1

	return 0


if __name__ == "__main__":
	raise SystemExit(main())
