from __future__ import annotations

import contextlib
import importlib.util
import io
import subprocess
import sys
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
