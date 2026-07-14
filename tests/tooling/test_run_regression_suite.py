from __future__ import annotations

import contextlib
import dataclasses
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
	def test_ci_imports_resources_and_runs_one_strict_full_suite(self) -> None:
		workflow = WORKFLOW_PATH.read_text(encoding="utf-8")

		self.assertIn("timeout-minutes: 120", workflow)
		self.assertIn("--headless --import --quit --path .", workflow)
		self.assertEqual(1, workflow.count("python tests/run_regression_suite.py"))
		self.assertIn("--jobs 16", workflow)
		self.assertIn("--test-timeout-seconds 180", workflow)
		self.assertIn("--fail-on-output-error", workflow)
		self.assertIn("--lifecycle-correctness", workflow)
		self.assertNotIn("--finalizer-crash-retries", workflow)
		self.assertNotIn("--stop-on-failure", workflow)
		self.assertNotIn("Run lifecycle correctness gate", workflow)
		self.assertNotIn("run_runtime_lifecycle_boundary_regression.cs", workflow)

	def test_parser_rejects_removed_finalizer_retry_option(self) -> None:
		with contextlib.redirect_stderr(io.StringIO()), self.assertRaises(SystemExit):
			runner.build_parser().parse_args(["--finalizer-crash-retries", "1"])

	def test_runner_source_has_no_retry_or_shutdown_exemption_path(self) -> None:
		source = RUNNER_PATH.read_text(encoding="utf-8")

		for forbidden in (
			"--finalizer-crash-retries",
			"finalizer_crash_retries",
			"finalizer_retries",
			"borrowed_resource_shutdown",
			"ObjectDB_leak_exempt",
			"suppressed_leaked_unsafe",
		):
			with self.subTest(forbidden=forbidden):
				self.assertNotIn(forbidden, source)

		for required in (
			"LEAKED_UNSAFE_REFERENCE_PREFIX",
			"OBJECTDB_LEAK_PREFIX",
			"RESOURCE_LEAK_PATTERN",
			"LIFECYCLE_FATAL_MARKERS",
		):
			with self.subTest(required=required):
				self.assertIn(required, source)

	def test_parser_accepts_strict_output_and_timeout_options(self) -> None:
		args = runner.build_parser().parse_args(
			[
				"--lifecycle-correctness",
				"--fail-on-output-error",
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

		self.assertTrue(args.lifecycle_correctness)
		self.assertTrue(args.fail_on_output_error)
		self.assertEqual(
			("battle_runtime/ai", "7", 4, 9, 73.0),
			(args.pattern, args.jobs, args.offset, args.limit, args.test_timeout_seconds),
		)

	def test_parser_rejects_non_finite_or_non_positive_timeout(self) -> None:
		for value in ("0", "-1", "nan", "inf", "-inf"):
			with self.subTest(value=value), contextlib.redirect_stderr(io.StringIO()):
				with self.assertRaises(SystemExit):
					runner.build_parser().parse_args([f"--test-timeout-seconds={value}"])

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

	def test_godot_process_retains_shutdown_leak_output(self) -> None:
		class CompletedProcess:
			pid = 12345
			stdout = io.StringIO(
				"ERROR: Leaked unsafe reference to object:\n"
				"   at: finalize (modules/mono/csharp_script.cpp:177)\n"
			)
			stderr = io.StringIO(
				"WARNING: ObjectDB instances leaked at exit\n"
				"ERROR: 2 resources still in use at exit\n"
			)

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
			)

		self.assertEqual(0, returncode)
		self.assertIn("Leaked unsafe reference to object", stdout)
		self.assertIn("at: finalize", stdout)
		self.assertIn("ObjectDB instances leaked at exit", stderr)
		self.assertIn("resources still in use at exit", stderr)
		self.assertNotIn("suppressed", stdout + stderr)
		self.assertEqual(3, len(errors))

	def test_run_one_invokes_godot_exactly_once_for_every_outcome(self) -> None:
		cases = (
			("pass", (0, "", "", ()), False, 0),
			("nonzero", (17, "", "", ()), False, 17),
			(
				"crash",
				(-6, "", "gchandle.is_released() GodotObject.Finalize()\n", ()),
				False,
				-6,
			),
			("timeout", (124, "", "[runner] test timed out\n", ()), False, 124),
			(
				"output_failure",
				(0, "", "ERROR: broken resource\n", ("stderr: ERROR: broken resource",)),
				True,
				1,
			),
		)
		for label, process_result, fail_on_output_error, expected_returncode in cases:
			with self.subTest(label=label), mock.patch.object(
				runner,
				"run_godot_process",
				return_value=process_result,
			) as run_process:
				result = runner.run_one_test(
					"godot",
					Path.cwd(),
					f"tests/fake/run_{label}.cs",
					1,
					1,
					False,
					None,
					fail_on_output_error,
					45.0,
				)

			self.assertEqual(expected_returncode, result.returncode)
			run_process.assert_called_once()

	def test_shutdown_leak_markers_fail_even_when_process_exits_zero(self) -> None:
		markers = (
			"ERROR: Leaked unsafe reference to object:",
			"WARNING: ObjectDB instances leaked at exit",
			"ERROR: 2 resources still in use at exit",
		)
		for marker in markers:
			with self.subTest(marker=marker), mock.patch.object(
				runner,
				"run_godot_process",
				return_value=(0, "", marker + "\n", ()),
			):
				result = runner.run_one_test(
					"godot",
					Path.cwd(),
					"tests/fake/run_leak_regression.cs",
					1,
					1,
					False,
					None,
					False,
					45.0,
				)

			self.assertEqual(1, result.returncode)
			self.assertEqual((f"stderr: {marker}",), result.lifecycle_fatal_lines)

	def test_finalizer_markers_fail_even_when_process_exits_zero(self) -> None:
		markers = (
			"gchandle.is_released",
			"GodotObject.Finalize",
			"Handle is not initialized",
			"godotsharp_variant_destroy",
		)
		for marker in markers:
			with self.subTest(marker=marker), mock.patch.object(
				runner,
				"run_godot_process",
				return_value=(0, "", f"fatal detail: {marker}\n", ()),
			):
				result = runner.run_one_test(
					"godot",
					Path.cwd(),
					"tests/fake/run_finalizer_regression.cs",
					1,
					1,
					False,
					None,
					False,
					45.0,
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
				False,
				45.0,
			)

		self.assertEqual(17, result.returncode)
		self.assertEqual(("stderr: GodotObject.Finalize()",), result.lifecycle_fatal_lines)

	def test_lifecycle_shutdown_report_enforces_zero_legacy_debt(self) -> None:
		clean = (
			"[lifecycle] shutdown-report reason=test requested=0 effective=0 "
			"phase=QuitRequested barrier_skipped=False duplicates=0 failures=0 legacy_debt=0"
		)
		dirty = clean[:-1] + "2"

		self.assertEqual((), runner.find_lifecycle_fatal_lines(clean, ""))
		self.assertEqual((f"stdout: {dirty}",), runner.find_lifecycle_fatal_lines(dirty, ""))

	def test_generic_output_errors_require_fail_on_output_error(self) -> None:
		marker = ("stderr: ERROR: broken resource",)
		for enabled, expected in ((False, 0), (True, 1)):
			with self.subTest(enabled=enabled), mock.patch.object(
				runner,
				"run_godot_process",
				return_value=(0, "", "ERROR: broken resource\n", marker),
			):
				result = runner.run_one_test(
					"godot",
					Path.cwd(),
					"tests/fake/run_output_error_regression.cs",
					1,
					1,
					False,
					None,
					enabled,
					45.0,
				)

			self.assertEqual(expected, result.returncode)
			self.assertEqual(marker if enabled else (), result.output_error_lines)

	def test_output_error_detection_finds_generic_and_shutdown_errors(self) -> None:
		lines = runner.find_output_error_lines(
			"SCRIPT ERROR: invalid call\nWARNING: ObjectDB instances leaked at exit\n",
			"ERROR: Leaked unsafe reference to object:\n"
			"ERROR: 2 resources still in use at exit\n"
			"FATAL: unrecoverable\n",
		)

		self.assertEqual(
			(
				"stdout: SCRIPT ERROR: invalid call",
				"stdout: WARNING: ObjectDB instances leaked at exit",
				"stderr: ERROR: Leaked unsafe reference to object:",
				"stderr: ERROR: 2 resources still in use at exit",
				"stderr: FATAL: unrecoverable",
			),
			lines,
		)

	def test_run_one_forces_lifecycle_env_for_serial_and_isolated_data(self) -> None:
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
			result = runner.run_one_test(
				"godot",
				Path.cwd(),
				"tests/fake/run_parallel.cs",
				1,
				1,
				False,
				Path(temp_dir),
				False,
				45.0,
				base_env,
				True,
			)
		isolated_env = run_process.call_args.args[3]
		self.assertEqual("1", isolated_env["MAGIC_LIFECYCLE_STRICT"])
		self.assertEqual("1", isolated_env["MAGIC_LIFECYCLE_TRACE"])
		self.assertIn("XDG_DATA_HOME", isolated_env)
		self.assertNotIn("attempt_", isolated_env["XDG_DATA_HOME"])
		self.assertIn(result.user_data_dir, isolated_env["XDG_DATA_HOME"])
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
				stdout=("raw lifecycle output\n" if "parallel" in args[2] else ""),
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
					False,
					45.0,
					child_env,
					True,
				)
		self.assertEqual(child_env, run_one.call_args.args[9])
		self.assertTrue(run_one.call_args.args[10])
		self.assertIn("raw lifecycle output", parallel_output.getvalue())

	def test_run_result_and_printed_summary_have_no_retry_count(self) -> None:
		field_names = {field.name for field in dataclasses.fields(runner.TestRunResult)}
		self.assertNotIn("finalizer_crash_retries", field_names)
		result = runner.TestRunResult(
			index=1,
			total=1,
			test_path="tests/fake/run_pass.cs",
			returncode=0,
			stdout="",
			stderr="",
			elapsed=0.01,
		)
		output = io.StringIO()
		with contextlib.redirect_stdout(output):
			runner.print_test_result(result, show_output=False)
		self.assertNotIn("retry", output.getvalue().lower())


if __name__ == "__main__":
	unittest.main()
