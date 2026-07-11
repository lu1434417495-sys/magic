from __future__ import annotations

import contextlib
import importlib.util
import io
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock


RUNNER_PATH = Path(__file__).resolve().parents[1] / "run_regression_suite.py"
WORKFLOW_PATH = Path(__file__).resolve().parents[2] / ".github" / "workflows" / "ci.yml"
SPEC = importlib.util.spec_from_file_location("run_regression_suite_under_test", RUNNER_PATH)
if SPEC is None or SPEC.loader is None:
	raise RuntimeError(f"Unable to load regression runner from {RUNNER_PATH}")
runner = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = runner
SPEC.loader.exec_module(runner)


class RegressionSuiteOutputGateTests(unittest.TestCase):
	def test_ci_imports_resources_and_runs_full_suite(self) -> None:
		workflow = WORKFLOW_PATH.read_text(encoding="utf-8")

		self.assertIn("timeout-minutes: 120", workflow)
		self.assertIn("--headless --import --quit --path .", workflow)
		self.assertIn("python tests/run_regression_suite.py", workflow)
		self.assertIn("--stop-on-failure", workflow)
		self.assertIn("--finalizer-crash-retries 1", workflow)
		self.assertIn("--test-timeout-seconds 180", workflow)
		self.assertIn("--fail-on-output-error", workflow)

	def test_ci_runs_lifecycle_correctness_gate_between_import_and_full_suite(self) -> None:
		workflow = WORKFLOW_PATH.read_text(encoding="utf-8")
		import_position = workflow.index("- name: Import Godot resources")
		lifecycle_position = workflow.index("- name: Run lifecycle correctness gate")
		full_suite_position = workflow.index("- name: Run full regression suite")

		self.assertLess(import_position, lifecycle_position)
		self.assertLess(lifecycle_position, full_suite_position)
		lifecycle_step = workflow[lifecycle_position:full_suite_position]
		self.assertIn('MAGIC_LIFECYCLE_STRICT: "1"', lifecycle_step)
		self.assertIn('MAGIC_LIFECYCLE_TRACE: "1"', lifecycle_step)
		self.assertIn("--pattern runtime/lifecycle", lifecycle_step)
		self.assertIn("--jobs 1", lifecycle_step)
		self.assertIn("--stop-on-failure", lifecycle_step)
		self.assertIn("--finalizer-crash-retries 0", lifecycle_step)
		self.assertIn("--test-timeout-seconds 180", lifecycle_step)
		self.assertIn("--fail-on-output-error", lifecycle_step)
		self.assertIn("--lifecycle-correctness", lifecycle_step)
		self.assertIn("run_runtime_lifecycle_boundary_regression.cs", lifecycle_step)

	def test_parser_accepts_lifecycle_correctness(self) -> None:
		args = runner.build_parser().parse_args(["--lifecycle-correctness"])

		self.assertTrue(args.lifecycle_correctness)

	def test_lifecycle_correctness_defaults_retries_to_zero(self) -> None:
		self.assertEqual(0, runner.resolve_finalizer_crash_retries(None, True))
		self.assertEqual(0, runner.resolve_finalizer_crash_retries(0, True))

	def test_lifecycle_correctness_rejects_every_explicit_nonzero_retry(self) -> None:
		for retries in (-3, -1, 1, 3):
			with self.subTest(retries=retries), self.assertRaises(ValueError):
				runner.resolve_finalizer_crash_retries(retries, True)

	def test_lifecycle_correctness_forces_child_env_without_mutating_source(self) -> None:
		base_env = {
			"MAGIC_LIFECYCLE_STRICT": "0",
			"MAGIC_LIFECYCLE_TRACE": "0",
			"UNCHANGED": "value",
		}

		child_env = runner.build_child_process_env(base_env, True)

		self.assertEqual("0", base_env["MAGIC_LIFECYCLE_STRICT"])
		self.assertEqual("0", base_env["MAGIC_LIFECYCLE_TRACE"])
		self.assertEqual("1", child_env["MAGIC_LIFECYCLE_STRICT"])
		self.assertEqual("1", child_env["MAGIC_LIFECYCLE_TRACE"])
		self.assertEqual("value", child_env["UNCHANGED"])
		self.assertIsNot(base_env, child_env)

	def test_lifecycle_profile_preserves_caller_selection_and_timeout(self) -> None:
		args = runner.build_parser().parse_args(
			[
				"--lifecycle-correctness",
				"--pattern",
				"battle_runtime/ai",
				"--jobs",
				"7",
				"--offset",
				"4",
				"--limit",
				"9",
				"--test-timeout-seconds",
				"73",
			]
		)
		selection = (args.pattern, args.jobs, args.offset, args.limit, args.test_timeout_seconds)

		runner.resolve_finalizer_crash_retries(
			args.finalizer_crash_retries,
			args.lifecycle_correctness,
		)
		runner.build_child_process_env({}, args.lifecycle_correctness)

		self.assertEqual(
			("battle_runtime/ai", "7", 4, 9, 73.0),
			selection,
		)
		self.assertEqual(selection, (args.pattern, args.jobs, args.offset, args.limit, args.test_timeout_seconds))

	def test_parser_accepts_fail_on_output_error(self) -> None:
		args = runner.build_parser().parse_args(["--fail-on-output-error"])

		self.assertTrue(args.fail_on_output_error)

	def test_parser_accepts_per_test_timeout(self) -> None:
		args = runner.build_parser().parse_args(["--test-timeout-seconds", "45"])

		self.assertEqual(45.0, args.test_timeout_seconds)

	def test_parser_rejects_non_finite_or_non_positive_timeout(self) -> None:
		for value in ("0", "-1", "nan", "inf", "-inf"):
			with self.subTest(value=value), contextlib.redirect_stderr(io.StringIO()):
				with self.assertRaises(SystemExit):
					runner.build_parser().parse_args([f"--test-timeout-seconds={value}"])

	def test_hung_godot_process_is_terminated_and_reported(self) -> None:
		class HungProcess:
			pid = 12345
			stdout = io.StringIO("")
			stderr = io.StringIO("")

			def wait(self, timeout=None):
				if timeout is not None:
					raise subprocess.TimeoutExpired("godot", timeout)
				return -9

		with (
			mock.patch.object(runner.subprocess, "Popen", return_value=HungProcess()),
			mock.patch.object(runner, "terminate_process_tree") as terminate,
		):
			returncode, _stdout, stderr, _errors = runner.run_godot_process(
				"godot",
				Path.cwd(),
				"tests/fake/run_hung_regression.cs",
				None,
				False,
				45.0,
			)

		self.assertEqual(124, returncode)
		self.assertIn("timed out after 45", stderr)
		terminate.assert_called_once()

	def test_lifecycle_process_output_retains_unsafe_reference_baseline(self) -> None:
		class CompletedProcess:
			pid = 12345
			stdout = io.StringIO(
				"ERROR: Leaked unsafe reference to object:\n"
				"   at: finalize (modules/mono/csharp_script.cpp:177)\n"
			)
			stderr = io.StringIO("")

			def wait(self, timeout=None):
				return 0

		with mock.patch.object(runner.subprocess, "Popen", return_value=CompletedProcess()):
			returncode, stdout, stderr, errors = runner.run_godot_process(
				"godot",
				Path.cwd(),
				"tests/fake/run_lifecycle_regression.cs",
				{},
				False,
				45.0,
				retain_lifecycle_output=True,
			)

		self.assertEqual(0, returncode)
		self.assertIn("Leaked unsafe reference to object", stdout)
		self.assertIn("at: finalize", stdout)
		self.assertNotIn("suppressed", stdout)
		self.assertEqual("", stderr)
		self.assertEqual((), errors)

	def test_lifecycle_fatal_markers_fail_even_when_process_exits_zero(self) -> None:
		for marker in runner.LIFECYCLE_FATAL_MARKERS:
			with self.subTest(marker=marker), mock.patch.object(
				runner,
				"run_godot_process",
				return_value=(0, "", f"fatal detail: {marker}\n", ()),
			):
				result = runner.run_one_test(
					"godot",
					Path.cwd(),
					"tests/fake/run_lifecycle_regression.cs",
					1,
					1,
					False,
					None,
					0,
					False,
					45.0,
					{},
					True,
				)

			self.assertEqual(1, result.returncode)
			self.assertEqual((f"stderr: fatal detail: {marker}",), result.lifecycle_fatal_lines)

	def test_lifecycle_fatal_marker_preserves_nonzero_child_exit(self) -> None:
		with mock.patch.object(
			runner,
			"run_godot_process",
			return_value=(17, "", "GodotObject.Finalize()\n", ()),
		):
			result = runner.run_one_test(
				"godot",
				Path.cwd(),
				"tests/fake/run_lifecycle_regression.cs",
				1,
				1,
				False,
				None,
				0,
				False,
				45.0,
				{},
				True,
			)

		self.assertEqual(17, result.returncode)
		self.assertEqual(("stderr: GodotObject.Finalize()",), result.lifecycle_fatal_lines)

	def test_run_one_test_forces_lifecycle_env_for_serial_and_isolated_data(self) -> None:
		base_env = {
			"MAGIC_LIFECYCLE_STRICT": "0",
			"MAGIC_LIFECYCLE_TRACE": "0",
		}
		with mock.patch.object(
			runner,
			"run_godot_process",
			return_value=(0, "", "", ()),
		) as run_process:
			runner.run_one_test(
				"godot",
				Path.cwd(),
				"tests/fake/run_serial.cs",
				1,
				1,
				False,
				None,
				0,
				False,
				45.0,
				base_env,
				True,
			)
		serial_env = run_process.call_args.args[3]
		self.assertEqual("1", serial_env["MAGIC_LIFECYCLE_STRICT"])
		self.assertEqual("1", serial_env["MAGIC_LIFECYCLE_TRACE"])

		with tempfile.TemporaryDirectory() as temp_dir, mock.patch.object(
			runner,
			"run_godot_process",
			return_value=(0, "", "", ()),
		) as run_process:
			runner.run_one_test(
				"godot",
				Path.cwd(),
				"tests/fake/run_parallel.cs",
				1,
				1,
				False,
				Path(temp_dir),
				0,
				False,
				45.0,
				base_env,
				True,
			)
		isolated_env = run_process.call_args.args[3]
		self.assertEqual("1", isolated_env["MAGIC_LIFECYCLE_STRICT"])
		self.assertEqual("1", isolated_env["MAGIC_LIFECYCLE_TRACE"])
		self.assertIn("XDG_DATA_HOME", isolated_env)
		self.assertEqual("0", base_env["MAGIC_LIFECYCLE_STRICT"])
		self.assertEqual("0", base_env["MAGIC_LIFECYCLE_TRACE"])

	def test_lifecycle_environment_reaches_serial_and_parallel_dispatch(self) -> None:
		child_env = runner.build_child_process_env({"BASE": "1"}, True)

		def completed_result(*args, **kwargs):
			return runner.TestRunResult(
				index=args[3],
				total=args[4],
				test_path=args[2],
				returncode=0,
				stdout=("raw lifecycle baseline\n" if "parallel" in args[2] else ""),
				stderr="",
				elapsed=0.01,
			)

		with (
			mock.patch.object(runner, "run_one_test", side_effect=completed_result) as run_one,
			contextlib.redirect_stdout(io.StringIO()),
		):
			runner.run_tests_serial(
				"godot",
				Path.cwd(),
				["tests/fake/run_serial.cs"],
				False,
				False,
				0,
				False,
				45.0,
				child_env,
				True,
			)
		self.assertEqual(child_env, run_one.call_args.kwargs["child_process_env"])
		self.assertTrue(run_one.call_args.kwargs["lifecycle_correctness"])

		parallel_output = io.StringIO()
		with tempfile.TemporaryDirectory() as temp_dir:
			with (
				mock.patch.object(runner, "run_one_test", side_effect=completed_result) as run_one,
				contextlib.redirect_stdout(parallel_output),
			):
				runner.run_tests_parallel(
					"godot",
					Path.cwd(),
					["tests/fake/run_parallel.cs"],
					1,
					False,
					False,
					Path(temp_dir),
					0,
					False,
					45.0,
					child_env,
					True,
				)
		self.assertEqual(child_env, run_one.call_args.args[10])
		self.assertTrue(run_one.call_args.args[11])
		self.assertIn("raw lifecycle baseline", parallel_output.getvalue())

	def test_run_one_test_promotes_output_error_to_failure(self) -> None:
		marker = ("stderr: ERROR: broken resource",)
		with mock.patch.object(
			runner,
			"run_godot_process",
			return_value=(0, "", "", marker),
		):
			result = runner.run_one_test(
				"godot",
				Path.cwd(),
				"tests/fake/run_output_error_regression.cs",
				1,
				1,
				False,
				None,
				0,
				True,
				45.0,
			)

		self.assertEqual(1, result.returncode)
		self.assertEqual(marker, result.output_error_lines)

	def test_output_error_detection_finds_real_godot_errors(self) -> None:
		lines = runner.find_output_error_lines(
			"SCRIPT ERROR: invalid call\n",
			"ERROR: broken resource\nFATAL: unrecoverable\n",
		)

		self.assertEqual(
			(
				"stdout: SCRIPT ERROR: invalid call",
				"stderr: ERROR: broken resource",
				"stderr: FATAL: unrecoverable",
			),
			lines,
		)

	def test_borrowed_resource_shutdown_noise_is_exempt(self) -> None:
		borrowed_shutdown = (
			"ERROR: Leaked unsafe reference to object:\n"
			"ERROR: 2 resources still in use at exit\n"
		)

		self.assertEqual((), runner.find_output_error_lines("", borrowed_shutdown))
		self.assertEqual(
			("stderr: ERROR: 2 resources still in use at exit",),
			runner.find_output_error_lines(
				"",
				"ERROR: 2 resources still in use at exit\n",
			),
		)

	def test_objectdb_shutdown_pair_is_exempt(self) -> None:
		shutdown_report = (
			"WARNING: ObjectDB instances leaked at exit\n"
			"ERROR: 2 resources still in use at exit\n"
		)

		self.assertEqual((), runner.find_output_error_lines("", shutdown_report))


if __name__ == "__main__":
	unittest.main()
