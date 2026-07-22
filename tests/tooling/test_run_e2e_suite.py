from __future__ import annotations

import contextlib
import importlib.util
import io
import shutil
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock


RUNNER_PATH = Path(__file__).resolve().parents[1] / "run_e2e_suite.py"
SPEC = importlib.util.spec_from_file_location("run_e2e_suite_under_test", RUNNER_PATH)
if SPEC is None or SPEC.loader is None:
	raise RuntimeError(f"Unable to load E2E runner from {RUNNER_PATH}")
runner = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = runner
SPEC.loader.exec_module(runner)


class E2eSuiteRunnerTests(unittest.TestCase):
	def test_registry_exposes_five_public_scenarios_and_seven_process_steps(self) -> None:
		self.assertEqual(
			(
				"cold_boot",
				"new_and_load",
				"world_save_round_trip",
				"enter_battle",
				"battle_round_trip",
			),
			tuple(scenario.name for scenario in runner.E2E_SCENARIOS),
		)
		self.assertEqual(
			7,
			sum(len(scenario.steps) for scenario in runner.E2E_SCENARIOS),
		)
		new_and_load = runner.select_scenarios(["new_and_load"], "")[0]
		self.assertEqual("new_and_load", new_and_load.sandbox_group)
		self.assertEqual(
			("new_game", "load_game"),
			tuple(step.name for step in new_and_load.steps),
		)
		self.assertEqual(
			(
				"tests/e2e/run_new_game_e2e.cs",
				"tests/e2e/run_load_game_e2e.cs",
			),
			tuple(step.runner_path for step in new_and_load.steps),
		)

		world_save_round_trip = runner.select_scenarios(["world_save_round_trip"], "")[0]
		self.assertEqual("world_save_round_trip", world_save_round_trip.sandbox_group)
		self.assertEqual(
			("world_save_mutation", "world_save_reload"),
			tuple(step.name for step in world_save_round_trip.steps),
		)
		self.assertEqual(
			(
				"tests/e2e/run_world_save_mutation_e2e.cs",
				"tests/e2e/run_world_save_reload_e2e.cs",
			),
			tuple(step.runner_path for step in world_save_round_trip.steps),
		)

		battle_round_trip = runner.select_scenarios(["battle_round_trip"], "")[0]
		self.assertEqual("battle_round_trip", battle_round_trip.sandbox_group)
		self.assertEqual(39208, battle_round_trip.deterministic_seed)
		self.assertEqual(
			("battle_round_trip",),
			tuple(step.name for step in battle_round_trip.steps),
		)
		self.assertEqual(
			("tests/e2e/run_battle_round_trip_e2e.cs",),
			tuple(step.runner_path for step in battle_round_trip.steps),
		)
		self.assertEqual(
			(),
			runner.validate_runner_paths(
				RUNNER_PATH.parents[1],
				runner.E2E_SCENARIOS,
			),
		)

	def test_parser_accepts_repeated_scenarios_pattern_and_timeout(self) -> None:
		args = runner.build_parser().parse_args(
			[
				"--scenario",
				"new_and_load",
				"--scenario",
				"cold_boot",
				"--pattern",
				"boot",
				"--test-timeout-seconds",
				"42.5",
				"--fail-on-output-error",
				"--lifecycle-correctness",
			]
		)

		self.assertEqual(["new_and_load", "cold_boot"], args.scenario)
		self.assertEqual("boot", args.pattern)
		self.assertEqual(42.5, args.test_timeout_seconds)
		self.assertTrue(args.fail_on_output_error)
		self.assertTrue(args.lifecycle_correctness)

	def test_parser_rejects_non_finite_or_non_positive_timeout(self) -> None:
		for value in ("0", "-1", "nan", "inf", "-inf"):
			with self.subTest(value=value), contextlib.redirect_stderr(io.StringIO()):
				with self.assertRaises(SystemExit):
					runner.build_parser().parse_args([f"--test-timeout-seconds={value}"])

	def test_selection_preserves_requested_order_deduplicates_and_filters_all_fields(self) -> None:
		selected = runner.select_scenarios(
			["enter_battle", "cold_boot", "enter_battle"],
			"run_enter_battle_e2e",
		)
		self.assertEqual(("enter_battle",), tuple(scenario.name for scenario in selected))
		self.assertEqual(
			("new_and_load",),
			tuple(scenario.name for scenario in runner.select_scenarios([], "load_game")),
		)
		with self.assertRaisesRegex(ValueError, "Unknown E2E scenario"):
			runner.select_scenarios(["missing"], "")

	def test_list_mode_needs_neither_godot_nor_user_data(self) -> None:
		output = io.StringIO()
		with (
			mock.patch.object(runner.regression_runner, "resolve_godot_command") as resolve_godot,
			mock.patch.object(runner, "create_isolated_user_data_root") as create_user_data,
			contextlib.redirect_stdout(output),
		):
			returncode = runner.main(["--list", "--scenario", "new_and_load"])

		self.assertEqual(0, returncode)
		resolve_godot.assert_not_called()
		create_user_data.assert_not_called()
		self.assertIn("new_and_load", output.getvalue())
		self.assertIn("run_new_game_e2e.cs", output.getvalue())
		self.assertIn("run_load_game_e2e.cs", output.getvalue())

	def test_user_data_root_is_always_a_unique_child_of_optional_parent(self) -> None:
		with tempfile.TemporaryDirectory() as temp_dir:
			parent = Path(temp_dir)
			first = runner.create_isolated_user_data_root(str(parent))
			second = runner.create_isolated_user_data_root(str(parent))
			try:
				self.assertEqual(parent.resolve(), first.parent)
				self.assertEqual(parent.resolve(), second.parent)
				self.assertNotEqual(first, second)
				self.assertTrue(first.name.startswith("godot-e2e-user-data-"))
				self.assertTrue(second.name.startswith("godot-e2e-user-data-"))
			finally:
				for path in (first, second):
					if path.exists():
						shutil.rmtree(path)

	def test_group_env_supports_xdg_and_windows_appdata_without_mutating_base(self) -> None:
		base_env = {"UNCHANGED": "value"}
		step = runner.E2eStep("phase", "tests/e2e/run_phase.cs")
		with tempfile.TemporaryDirectory() as temp_dir:
			run_root = Path(temp_dir)
			with mock.patch.object(runner.regression_runner.os, "name", "posix"):
				linux_env, linux_group = runner.prepare_sandbox_group_env(
					base_env,
					run_root,
					"shared",
					step,
					"scenario",
					False,
				)
			self.assertEqual(str(linux_group / "xdg_data"), linux_env["XDG_DATA_HOME"])
			self.assertEqual(str(linux_group / "xdg_config"), linux_env["XDG_CONFIG_HOME"])
			self.assertEqual(str(linux_group / "xdg_cache"), linux_env["XDG_CACHE_HOME"])
			self.assertEqual("1", linux_env["MAGIC_E2E_ISOLATED_USER_DATA"])
			self.assertEqual(str(linux_group.resolve()), linux_env["MAGIC_E2E_USER_DATA_ROOT"])

			with mock.patch.object(runner.regression_runner.os, "name", "nt"):
				windows_env, windows_group = runner.prepare_sandbox_group_env(
					base_env,
					run_root,
					"windows",
					step,
					"scenario",
					True,
				)
			self.assertEqual(
				str(windows_group / "AppData" / "Roaming"),
				windows_env["APPDATA"],
			)
			self.assertEqual(
				str(windows_group / "AppData" / "Local"),
				windows_env["LOCALAPPDATA"],
			)
			self.assertEqual("1", windows_env["MAGIC_LIFECYCLE_STRICT"])
			self.assertEqual("1", windows_env["MAGIC_LIFECYCLE_TRACE"])
			self.assertEqual("1", windows_env["MAGIC_E2E_ISOLATED_USER_DATA"])
			self.assertEqual(
				str(windows_group.resolve()),
				windows_env["MAGIC_E2E_USER_DATA_ROOT"],
			)
		self.assertEqual({"UNCHANGED": "value"}, base_env)

	def test_battle_round_trip_enables_only_its_declared_deterministic_seed(self) -> None:
		battle = runner.select_scenarios(["battle_round_trip"], "")[0]
		cold_boot = runner.select_scenarios(["cold_boot"], "")[0]
		with tempfile.TemporaryDirectory() as temp_dir:
			run_root = Path(temp_dir)
			battle_env, _ = runner.prepare_sandbox_group_env(
				{},
				run_root,
				battle.sandbox_group,
				battle.steps[0],
				battle.name,
				False,
				battle.deterministic_seed,
			)
			cold_boot_env, _ = runner.prepare_sandbox_group_env(
				{"MAGIC_E2E_RANDOM_SEED": "stale-parent-value"},
				run_root,
				cold_boot.sandbox_group,
				cold_boot.steps[0],
				cold_boot.name,
				False,
				cold_boot.deterministic_seed,
			)

		self.assertEqual("39208", battle_env["MAGIC_E2E_RANDOM_SEED"])
		self.assertNotIn("MAGIC_E2E_RANDOM_SEED", cold_boot_env)

	def test_new_and_load_steps_run_serially_in_the_same_sandbox_group(self) -> None:
		calls: list[tuple[str, dict[str, str]]] = []

		def completed_process(
			_godot: str,
			_repo_root: Path,
			test_path: str,
			env: dict[str, str],
			_realtime: bool,
			_timeout: float,
		):
			calls.append((test_path, dict(env)))
			return 0, "", "", ()

		scenario = runner.select_scenarios(["new_and_load"], "")[0]
		with tempfile.TemporaryDirectory() as temp_dir, mock.patch.object(
			runner.regression_runner,
			"run_godot_process",
			side_effect=completed_process,
		):
			with contextlib.redirect_stdout(io.StringIO()):
				results = runner.run_scenarios(
					"godot",
					Path.cwd(),
					(scenario,),
					Path(temp_dir),
					True,
					30.0,
					False,
					False,
					False,
					{"BASE": "1"},
				)

		self.assertEqual(
			[
				"tests/e2e/run_new_game_e2e.cs",
				"tests/e2e/run_load_game_e2e.cs",
			],
			[path for path, _env in calls],
		)
		self.assertEqual(2, len(results))
		self.assertEqual(calls[0][1]["XDG_DATA_HOME"], calls[1][1]["XDG_DATA_HOME"])
		self.assertEqual(calls[0][1].get("APPDATA"), calls[1][1].get("APPDATA"))
		self.assertEqual("new_game", calls[0][1]["MAGIC_E2E_STEP"])
		self.assertEqual("load_game", calls[1][1]["MAGIC_E2E_STEP"])
		self.assertTrue(all(result.returncode == 0 for result in results))

	def test_world_save_round_trip_steps_run_serially_in_the_same_sandbox_group(self) -> None:
		calls: list[tuple[str, dict[str, str]]] = []

		def completed_process(
			_godot: str,
			_repo_root: Path,
			test_path: str,
			env: dict[str, str],
			_realtime: bool,
			_timeout: float,
		):
			calls.append((test_path, dict(env)))
			return 0, "", "", ()

		scenario = runner.select_scenarios(["world_save_round_trip"], "")[0]
		with tempfile.TemporaryDirectory() as temp_dir, mock.patch.object(
			runner.regression_runner,
			"run_godot_process",
			side_effect=completed_process,
		):
			with contextlib.redirect_stdout(io.StringIO()):
				results = runner.run_scenarios(
					"godot",
					Path.cwd(),
					(scenario,),
					Path(temp_dir),
					True,
					30.0,
					False,
					False,
					False,
					{"BASE": "1"},
				)

		self.assertEqual(
			[
				"tests/e2e/run_world_save_mutation_e2e.cs",
				"tests/e2e/run_world_save_reload_e2e.cs",
			],
			[path for path, _env in calls],
		)
		self.assertEqual(2, len(results))
		self.assertEqual(calls[0][1]["XDG_DATA_HOME"], calls[1][1]["XDG_DATA_HOME"])
		self.assertEqual(calls[0][1].get("APPDATA"), calls[1][1].get("APPDATA"))
		self.assertEqual("world_save_mutation", calls[0][1]["MAGIC_E2E_STEP"])
		self.assertEqual("world_save_reload", calls[1][1]["MAGIC_E2E_STEP"])
		self.assertTrue(all(result.returncode == 0 for result in results))

	def test_generic_output_gate_is_optional_but_lifecycle_fatal_is_always_enforced(self) -> None:
		scenario = runner.select_scenarios(["cold_boot"], "")[0]
		step = scenario.steps[0]
		with tempfile.TemporaryDirectory() as temp_dir:
			run_root = Path(temp_dir)
			generic_marker = ("stderr: ERROR: broken scene",)
			with mock.patch.object(
				runner.regression_runner,
				"run_godot_process",
				return_value=(0, "", "ERROR: broken scene\n", generic_marker),
			):
				permissive = runner.run_e2e_step(
					"godot", Path.cwd(), scenario, step, run_root, False, 30, False, False
				)
				strict = runner.run_e2e_step(
					"godot", Path.cwd(), scenario, step, run_root, True, 30, False, False
				)

			with mock.patch.object(
				runner.regression_runner,
				"run_godot_process",
				return_value=(0, "", "GodotObject.Finalize()\n", ()),
			):
				lifecycle = runner.run_e2e_step(
					"godot", Path.cwd(), scenario, step, run_root, False, 30, False, False
				)

		self.assertEqual(0, permissive.returncode)
		self.assertEqual(1, strict.returncode)
		self.assertEqual(generic_marker, strict.output_error_lines)
		self.assertEqual(1, lifecycle.returncode)
		self.assertEqual(
			("stderr: GodotObject.Finalize()",),
			lifecycle.lifecycle_fatal_lines,
		)

	def test_failure_skips_dependent_steps_and_can_continue_with_next_scenario(self) -> None:
		calls: list[str] = []

		def process_result(_godot, _repo, test_path, _env, _realtime, _timeout):
			calls.append(test_path)
			if test_path.endswith("run_new_game_e2e.cs"):
				return 7, "", "", ()
			return 0, "", "", ()

		scenarios = runner.select_scenarios(["new_and_load", "cold_boot"], "")
		with tempfile.TemporaryDirectory() as temp_dir, mock.patch.object(
			runner.regression_runner,
			"run_godot_process",
			side_effect=process_result,
		), contextlib.redirect_stdout(io.StringIO()):
			results = runner.run_scenarios(
				"godot",
				Path.cwd(),
				scenarios,
				Path(temp_dir),
				False,
				30,
				False,
				False,
				False,
			)

		self.assertEqual(
			["tests/e2e/run_new_game_e2e.cs", "tests/e2e/run_cold_boot_e2e.cs"],
			calls,
		)
		self.assertEqual((7, 0), tuple(result.returncode for result in results))

	def test_ordinary_regression_discovery_always_excludes_e2e(self) -> None:
		self.assertTrue(
			runner.regression_runner.should_skip_test(
				"tests/e2e/run_cold_boot_e2e.cs",
				"e2e",
				True,
				True,
			)
		)
		self.assertFalse(
			runner.regression_runner.should_skip_test(
				"tests/runtime/run_regular_regression.cs",
				"",
				False,
				False,
			)
		)


if __name__ == "__main__":
	unittest.main()
