#!/usr/bin/env python3

from __future__ import annotations

import argparse
import os
import shutil
import sys
import tempfile
import time
from dataclasses import dataclass
from pathlib import Path


SCRIPT_ROOT = Path(__file__).resolve().parent
if str(SCRIPT_ROOT) not in sys.path:
	sys.path.insert(0, str(SCRIPT_ROOT))

import run_regression_suite as regression_runner


@dataclass(frozen=True)
class E2eStep:
	name: str
	runner_path: str


@dataclass(frozen=True)
class E2eScenario:
	name: str
	description: str
	sandbox_group: str
	steps: tuple[E2eStep, ...]
	deterministic_seed: int | None = None


@dataclass(frozen=True)
class E2eStepResult:
	scenario_name: str
	step_name: str
	runner_path: str
	sandbox_group: str
	user_data_dir: str
	returncode: int
	stdout: str
	stderr: str
	elapsed: float
	output_error_lines: tuple[str, ...] = ()
	lifecycle_fatal_lines: tuple[str, ...] = ()


E2E_SCENARIOS = (
	E2eScenario(
		name="cold_boot",
		description="Boot the production main scene and reach the login screen.",
		sandbox_group="cold_boot",
		steps=(
			E2eStep("cold_boot", "tests/e2e/run_cold_boot_e2e.cs"),
		),
	),
	E2eScenario(
		name="new_and_load",
		description="Create a new game, restart the process, and load the same save.",
		sandbox_group="new_and_load",
		steps=(
			E2eStep("new_game", "tests/e2e/run_new_game_e2e.cs"),
			E2eStep("load_game", "tests/e2e/run_load_game_e2e.cs"),
		),
	),
	E2eScenario(
		name="world_save_round_trip",
		description=(
			"Mutate world state, restart the process, and verify the saved state after loading."
		),
		sandbox_group="world_save_round_trip",
		steps=(
			E2eStep(
				"world_save_mutation",
				"tests/e2e/run_world_save_mutation_e2e.cs",
			),
			E2eStep(
				"world_save_reload",
				"tests/e2e/run_world_save_reload_e2e.cs",
			),
		),
	),
	E2eScenario(
		name="enter_battle",
		description="Move from the world map into a ready battle UI.",
		sandbox_group="enter_battle",
		steps=(
			E2eStep("enter_battle", "tests/e2e/run_enter_battle_e2e.cs"),
		),
	),
	E2eScenario(
		name="battle_round_trip",
		description="Complete a world-map battle and return to a consistent world-map state.",
		sandbox_group="battle_round_trip",
		steps=(
			E2eStep(
				"battle_round_trip",
				"tests/e2e/run_battle_round_trip_e2e.cs",
			),
		),
		deterministic_seed=39208,
	),
)


def build_parser() -> argparse.ArgumentParser:
	parser = argparse.ArgumentParser(
		description="Run application-level Godot E2E scenarios serially in isolated user data.",
	)
	parser.add_argument("--godot", "-Godot", default="godot", help="Godot executable name or path.")
	parser.add_argument(
		"--scenario",
		"-Scenario",
		action="append",
		default=[],
		metavar="NAME",
		help="Run one named scenario. Repeat to select multiple scenarios.",
	)
	parser.add_argument(
		"--pattern",
		"-Pattern",
		default="",
		help="Only include scenarios whose name, group, description, step, or runner path contains this text.",
	)
	parser.add_argument(
		"--list",
		"-List",
		action="store_true",
		dest="list_scenarios",
		help="List matching scenarios without resolving Godot or creating user data.",
	)
	parser.add_argument(
		"--stop-on-failure",
		action="store_true",
		help="Stop before starting another scenario after the first failed step.",
	)
	parser.add_argument(
		"--verbose",
		"-Verbose",
		action="store_true",
		help="Echo child stdout and stderr while each E2E step runs.",
	)
	parser.add_argument(
		"--fail-on-output-error",
		action="store_true",
		help="Fail when Godot prints ERROR, SCRIPT ERROR, FATAL, or an ObjectDB leak warning.",
	)
	parser.add_argument(
		"--lifecycle-correctness",
		action="store_true",
		help="Enable strict lifecycle diagnostics in every child process.",
	)
	parser.add_argument(
		"--test-timeout-seconds",
		type=regression_runner.positive_finite_seconds,
		default=regression_runner.DEFAULT_TEST_TIMEOUT_SECONDS,
		help=(
			"Terminate an individual Godot E2E step after this many seconds. "
			f"Default: {regression_runner.DEFAULT_TEST_TIMEOUT_SECONDS:.0f}."
		),
	)
	parser.add_argument(
		"--user-data-root",
		default="",
		help=(
			"Parent directory for the unique per-run user-data sandbox. "
			"The supplied directory itself is never used as Godot user data."
		),
	)
	parser.add_argument(
		"--keep-user-data",
		action="store_true",
		help="Keep the unique per-run user-data sandbox after a successful run.",
	)
	return parser


def scenario_matches(scenario: E2eScenario, pattern: str) -> bool:
	needle = pattern.strip().lower()
	if not needle:
		return True
	haystacks = (
		scenario.name,
		scenario.description,
		scenario.sandbox_group,
		*(step.name for step in scenario.steps),
		*(step.runner_path for step in scenario.steps),
	)
	return any(needle in value.lower() for value in haystacks)


def select_scenarios(
	requested_names: list[str] | tuple[str, ...],
	pattern: str,
) -> tuple[E2eScenario, ...]:
	by_name = {scenario.name: scenario for scenario in E2E_SCENARIOS}
	if requested_names:
		unknown = sorted({name for name in requested_names if name not in by_name})
		if unknown:
			available = ", ".join(by_name)
			raise ValueError(
				f"Unknown E2E scenario(s): {', '.join(unknown)}. Available: {available}."
			)
		selected: list[E2eScenario] = []
		seen: set[str] = set()
		for name in requested_names:
			if name in seen:
				continue
			seen.add(name)
			selected.append(by_name[name])
	else:
		selected = list(E2E_SCENARIOS)
	return tuple(scenario for scenario in selected if scenario_matches(scenario, pattern))


def print_scenario_list(scenarios: tuple[E2eScenario, ...]) -> None:
	for scenario in scenarios:
		print(f"{scenario.name}: {scenario.description}")
		for step in scenario.steps:
			print(f"  - {step.name}: {step.runner_path}")
	print(f"Total: {len(scenarios)} scenario(s)")


def create_isolated_user_data_root(parent: str = "") -> Path:
	parent_path: Path | None = None
	if parent:
		parent_path = Path(parent).resolve()
		parent_path.mkdir(parents=True, exist_ok=True)
	return Path(
		tempfile.mkdtemp(
			prefix="godot-e2e-user-data-",
			dir=str(parent_path) if parent_path is not None else None,
		)
	)


def prepare_sandbox_group_env(
	base_env: dict[str, str],
	run_user_data_root: Path,
	sandbox_group: str,
	step: E2eStep,
	scenario_name: str,
	lifecycle_correctness: bool,
	deterministic_seed: int | None = None,
) -> tuple[dict[str, str], Path]:
	group_dir = run_user_data_root / regression_runner.sanitize_test_path(sandbox_group)
	env = regression_runner.prepare_user_data_env(base_env, group_dir)
	env = regression_runner.build_child_process_env(env, lifecycle_correctness)
	env["MAGIC_E2E_SCENARIO"] = scenario_name
	env["MAGIC_E2E_STEP"] = step.name
	env["MAGIC_E2E_SANDBOX_GROUP"] = sandbox_group
	env["MAGIC_E2E_ISOLATED_USER_DATA"] = "1"
	env["MAGIC_E2E_USER_DATA_ROOT"] = str(group_dir.resolve())
	env.pop("MAGIC_E2E_RANDOM_SEED", None)
	if deterministic_seed is not None:
		env["MAGIC_E2E_RANDOM_SEED"] = str(deterministic_seed)
	return env, group_dir


def run_e2e_step(
	godot_command: str,
	repo_root: Path,
	scenario: E2eScenario,
	step: E2eStep,
	run_user_data_root: Path,
	fail_on_output_error: bool,
	test_timeout_seconds: float,
	verbose: bool,
	lifecycle_correctness: bool,
	base_env: dict[str, str] | None = None,
) -> E2eStepResult:
	env, group_dir = prepare_sandbox_group_env(
		base_env if base_env is not None else dict(os.environ),
		run_user_data_root,
		scenario.sandbox_group,
		step,
		scenario.name,
		lifecycle_correctness,
		scenario.deterministic_seed,
	)
	start = time.perf_counter()
	returncode, stdout, stderr, detected_output_errors = regression_runner.run_godot_process(
		godot_command,
		repo_root,
		step.runner_path,
		env,
		verbose,
		test_timeout_seconds,
	)
	lifecycle_fatal_lines = regression_runner.find_lifecycle_fatal_lines(stdout, stderr)
	output_error_lines = detected_output_errors if fail_on_output_error else ()
	if returncode == 0 and (output_error_lines or lifecycle_fatal_lines):
		returncode = 1
	return E2eStepResult(
		scenario_name=scenario.name,
		step_name=step.name,
		runner_path=step.runner_path,
		sandbox_group=scenario.sandbox_group,
		user_data_dir=str(group_dir),
		returncode=returncode,
		stdout=stdout,
		stderr=stderr,
		elapsed=time.perf_counter() - start,
		output_error_lines=output_error_lines,
		lifecycle_fatal_lines=lifecycle_fatal_lines,
	)


def print_step_result(result: E2eStepResult, show_output: bool) -> None:
	status = "PASS" if result.returncode == 0 else f"FAIL exit={result.returncode}"
	print(
		f"[{status}] {result.scenario_name}/{result.step_name} "
		f"({result.elapsed:.2f}s)",
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


def run_scenarios(
	godot_command: str,
	repo_root: Path,
	scenarios: tuple[E2eScenario, ...],
	run_user_data_root: Path,
	fail_on_output_error: bool,
	test_timeout_seconds: float,
	verbose: bool,
	lifecycle_correctness: bool,
	stop_on_failure: bool,
	base_env: dict[str, str] | None = None,
) -> list[E2eStepResult]:
	results: list[E2eStepResult] = []
	total_steps = sum(len(scenario.steps) for scenario in scenarios)
	step_index = 0
	for scenario in scenarios:
		scenario_failed = False
		for step in scenario.steps:
			step_index += 1
			print(
				f"\n[{step_index}/{total_steps}] [RUN] {scenario.name}/{step.name} "
				f"({step.runner_path})",
				flush=True,
			)
			result = run_e2e_step(
				godot_command,
				repo_root,
				scenario,
				step,
				run_user_data_root,
				fail_on_output_error,
				test_timeout_seconds,
				verbose,
				lifecycle_correctness,
				base_env,
			)
			results.append(result)
			print_step_result(result, show_output=not verbose and result.returncode != 0)
			if result.returncode != 0:
				scenario_failed = True
				break
		if scenario_failed and stop_on_failure:
			break
	return results


def validate_runner_paths(repo_root: Path, scenarios: tuple[E2eScenario, ...]) -> tuple[str, ...]:
	missing = []
	for scenario in scenarios:
		for step in scenario.steps:
			if not (repo_root / step.runner_path).is_file():
				missing.append(step.runner_path)
	return tuple(missing)


def main(argv: list[str] | None = None) -> int:
	parser = build_parser()
	args = parser.parse_args(argv)
	try:
		scenarios = select_scenarios(args.scenario, args.pattern)
	except ValueError as exc:
		parser.error(str(exc))

	if args.list_scenarios:
		print_scenario_list(scenarios)
		return 0
	if not scenarios:
		print("No matching E2E scenarios found.", file=sys.stderr)
		return 1

	repo_root = SCRIPT_ROOT.parent
	missing_paths = validate_runner_paths(repo_root, scenarios)
	if missing_paths:
		for path in missing_paths:
			print(f"E2E runner not found: {path}", file=sys.stderr)
		return 1

	godot_command = regression_runner.resolve_godot_command(args.godot)
	if godot_command is None:
		print(f"Godot executable not found: {args.godot}", file=sys.stderr)
		return 1

	run_user_data_root = create_isolated_user_data_root(args.user_data_root)
	print(f"Isolated Godot user-data root: {run_user_data_root}", flush=True)
	results: list[E2eStepResult] = []
	unexpected_failure = False
	try:
		results = run_scenarios(
			godot_command,
			repo_root,
			scenarios,
			run_user_data_root,
			args.fail_on_output_error,
			args.test_timeout_seconds,
			args.verbose,
			args.lifecycle_correctness,
			args.stop_on_failure,
		)
	except Exception:
		unexpected_failure = True
		raise
	finally:
		failed = unexpected_failure or any(result.returncode != 0 for result in results)
		if args.keep_user_data or failed:
			print(f"Kept isolated Godot user-data root: {run_user_data_root}", flush=True)
		else:
			shutil.rmtree(run_user_data_root, ignore_errors=True)

	failed_results = [result for result in results if result.returncode != 0]
	print()
	print(f"Passed steps: {len(results) - len(failed_results)}")
	print(f"Failed steps: {len(failed_results)}")
	if failed_results:
		print("Failed E2E steps:")
		for result in failed_results:
			print(
				f"- {result.scenario_name}/{result.step_name} "
				f"exit={result.returncode} user_data={result.user_data_dir}"
			)
		return 1
	return 0


if __name__ == "__main__":
	raise SystemExit(main())
