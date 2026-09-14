from __future__ import annotations

import contextlib
import importlib.util
import io
import json
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock


CLI_PATH = Path(__file__).resolve().parents[2] / "tools" / "dev_check.py"
SPEC = importlib.util.spec_from_file_location("dev_check_under_test", CLI_PATH)
if SPEC is None or SPEC.loader is None:
	raise RuntimeError(f"Unable to load dev check CLI from {CLI_PATH}")
dev_check = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = dev_check
SPEC.loader.exec_module(dev_check)


class FakeCommandRunner:
	def __init__(self, overrides: dict[str, dev_check.StepResult] | None = None) -> None:
		self.overrides = overrides or {}
		self.calls: list[dev_check.CommandSpec] = []

	def __call__(self, spec: dev_check.CommandSpec, _repo_root: Path, dry_run: bool) -> dev_check.StepResult:
		self.calls.append(spec)
		if dry_run:
			return dev_check.StepResult(spec.name, spec.argv, "planned", None, 0.0)
		if spec.name in self.overrides:
			return self.overrides[spec.name]
		stdout = ""
		if spec.name == "checkout":
			stdout = "## codex/test...origin/codex/test [ahead 1]\n M scripts/example.cs\n?? notes.txt\n"
		elif spec.name == "discover":
			stdout = "tests/example/run_example_regression.cs\nTotal: 1\n"
		return dev_check.StepResult(spec.name, spec.argv, "passed", 0, 0.01, stdout_excerpt=stdout)


class DevCheckCliTests(unittest.TestCase):
	def test_no_mode_defaults_to_quick(self) -> None:
		args = dev_check.parse_args([])

		self.assertEqual("quick", args.mode)
		self.assertFalse(args.restore)
		self.assertEqual(180.0, args.test_timeout_seconds)

	def test_default_jobs_uses_half_cpu_with_safe_bounds(self) -> None:
		self.assertEqual(1, dev_check.default_jobs(1))
		self.assertEqual(4, dev_check.default_jobs(8))
		self.assertEqual(16, dev_check.default_jobs(128))

	def test_discovery_count_uses_full_output_before_report_truncation(self) -> None:
		listing = "".join(f"tests/example/run_{index}_regression.cs\n" for index in range(500))
		listing += "Total: 500\n"
		completed = dev_check.subprocess.CompletedProcess(
			args=["python", "tests/run_regression_suite.py", "--list"],
			returncode=0,
			stdout=listing,
			stderr="",
		)
		spec = dev_check.CommandSpec(
			"discover",
			("python", "tests/run_regression_suite.py", "--list"),
			capture_output=True,
		)
		with mock.patch.object(dev_check.subprocess, "run", return_value=completed):
			with contextlib.redirect_stdout(io.StringIO()):
				result = dev_check.run_command(spec, dev_check.REPO_ROOT, False)

		self.assertEqual(500, result.observations["discovered_tests"])
		self.assertIn("output truncated", result.stdout_excerpt)
		self.assertNotIn("Total: 500", result.stdout_excerpt)

	def test_checkout_counts_use_full_output_before_report_truncation(self) -> None:
		status_output = "## codex/test...origin/codex/test\n"
		status_output += "".join(f" M scripts/file_{index}.cs\n" for index in range(300))
		status_output += "".join(f"?? notes/{index}.txt\n" for index in range(25))
		completed = dev_check.subprocess.CompletedProcess(
			args=["git", "status"],
			returncode=0,
			stdout=status_output,
			stderr="",
		)
		spec = dev_check.CommandSpec("checkout", ("git", "status"), capture_output=True)
		with mock.patch.object(dev_check.subprocess, "run", return_value=completed):
			with contextlib.redirect_stdout(io.StringIO()):
				result = dev_check.run_command(spec, dev_check.REPO_ROOT, False)

		self.assertEqual(300, result.observations["checkout"]["unstaged"])
		self.assertEqual(25, result.observations["checkout"]["untracked"])
		self.assertIn("output truncated", result.stdout_excerpt)

	def test_focused_requires_a_pattern(self) -> None:
		with contextlib.redirect_stderr(io.StringIO()), self.assertRaises(SystemExit):
			dev_check.parse_args(["focused"])

	def test_quick_builds_but_does_not_run_regressions(self) -> None:
		runner = FakeCommandRunner()
		args = dev_check.parse_args(["quick"])

		steps, metadata = dev_check.run_workflow(args, runner)

		self.assertEqual(
			["checkout", "diff_worktree", "diff_index", "discover", "build"],
			[step.name for step in steps],
		)
		self.assertEqual(1, metadata["discovered_tests"])
		self.assertEqual(
			{"branch": "codex/test...origin/codex/test [ahead 1]", "staged": 0, "unstaged": 1, "untracked": 1, "dirty": True},
			metadata["checkout"],
		)

	def test_focused_regression_uses_strict_routine_defaults(self) -> None:
		runner = FakeCommandRunner()
		args = dev_check.parse_args(["focused", "--pattern", "tests/battle_runtime/skills"])

		steps, metadata = dev_check.run_workflow(args, runner)

		regression = next(call for call in runner.calls if call.name == "regression")
		self.assertIn("--pattern", regression.argv)
		self.assertIn("tests/battle_runtime/skills", regression.argv)
		self.assertIn("--fail-on-output-error", regression.argv)
		self.assertIn("--lifecycle-correctness", regression.argv)
		self.assertNotIn("--include-simulation", regression.argv)
		self.assertNotIn("--include-benchmarks", regression.argv)
		self.assertFalse(metadata["simulation_included"])
		self.assertEqual("passed", steps[-1].status)

	def test_opt_in_simulation_is_forwarded_to_discovery_and_execution(self) -> None:
		runner = FakeCommandRunner()
		args = dev_check.parse_args(
			["focused", "--pattern", "tests/battle_runtime/simulation", "--include-simulation"]
		)

		_dev_steps, metadata = dev_check.run_workflow(args, runner)

		discovery = next(call for call in runner.calls if call.name == "discover")
		regression = next(call for call in runner.calls if call.name == "regression")
		self.assertIn("--include-simulation", discovery.argv)
		self.assertIn("--include-simulation", regression.argv)
		self.assertTrue(metadata["simulation_included"])

	def test_build_failure_skips_regression_and_fails_workflow(self) -> None:
		build_failure = dev_check.StepResult("build", ("dotnet", "build"), "failed", 1, 0.1)
		runner = FakeCommandRunner({"build": build_failure})
		args = dev_check.parse_args(["full"])

		steps, _metadata = dev_check.run_workflow(args, runner)

		self.assertNotIn("regression", [call.name for call in runner.calls])
		self.assertEqual("skipped", steps[-1].status)
		self.assertEqual("FAIL", dev_check.overall_status(steps, False))

	def test_zero_matching_tests_skips_regression(self) -> None:
		zero_discovery = dev_check.StepResult(
			"discover",
			("python", "tests/run_regression_suite.py", "--list"),
			"passed",
			0,
			0.1,
			stdout_excerpt="Total: 0\n",
		)
		runner = FakeCommandRunner({"discover": zero_discovery})
		args = dev_check.parse_args(["focused", "--pattern", "does/not/exist"])

		steps, metadata = dev_check.run_workflow(args, runner)

		self.assertEqual(0, metadata["discovered_tests"])
		self.assertEqual("failed", next(step for step in steps if step.name == "discover").status)
		self.assertEqual("skipped", steps[-1].status)

	def test_json_report_preserves_validation_scope_and_ci_boundary(self) -> None:
		runner = FakeCommandRunner()
		with tempfile.TemporaryDirectory() as temp_dir:
			report_path = Path(temp_dir) / "dev-check.json"
			with contextlib.redirect_stdout(io.StringIO()):
				returncode = dev_check.main(
					["inspect", "--json-report", str(report_path)],
					command_runner=runner,
				)
			payload = json.loads(report_path.read_text(encoding="utf-8"))

		self.assertEqual(0, returncode)
		self.assertEqual("PASS", payload["result"])
		self.assertEqual("current_filesystem", payload["scope"])
		self.assertEqual("not_checked", payload["ci"])
		self.assertFalse(payload["e2e_included"])
		self.assertNotIn("build", [step["name"] for step in payload["steps"]])


if __name__ == "__main__":
	unittest.main()
