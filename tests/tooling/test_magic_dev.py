from __future__ import annotations

import contextlib
import importlib.util
import io
import json
import sys
import tempfile
import unittest
from pathlib import Path


CLI_PATH = Path(__file__).resolve().parents[2] / "tools" / "magic_dev.py"
SPEC = importlib.util.spec_from_file_location("magic_dev_under_test", CLI_PATH)
if SPEC is None or SPEC.loader is None:
	raise RuntimeError(f"Unable to import {CLI_PATH}")
magic_dev = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = magic_dev
SPEC.loader.exec_module(magic_dev)


class FakeExecutor:
	def __init__(self, overrides: dict[str, tuple[int, str, str]] | None = None) -> None:
		self.overrides = overrides or {}
		self.calls: list[tuple[str, ...]] = []

	def __call__(self, command, _repo_root: Path, _capture: bool, dry_run: bool):
		argv = tuple(command)
		self.calls.append(argv)
		if dry_run:
			return magic_dev.ProcessResult(argv, None, 0.0, planned=True)
		key = self._key(argv)
		returncode, stdout, stderr = self.overrides.get(key, (0, self._default_stdout(key), ""))
		return magic_dev.ProcessResult(argv, returncode, 0.01, stdout, stderr)

	@staticmethod
	def _key(argv: tuple[str, ...]) -> str:
		if "--list" in argv:
			return "discover"
		if len(argv) >= 2 and argv[0] == "git":
			if "status" in argv:
				return "status"
			if "rev-list" in argv:
				return "ahead"
			if "rev-parse" in argv and "HEAD" in argv:
				return "head"
			if "rev-parse" in argv:
				return "upstream"
			if "ls-files" in argv:
				return "untracked"
			if "--cached" in argv and "--name-only" in argv:
				return "changed_index"
			if "--name-only" in argv:
				return "changed_worktree"
			if "--cached" in argv:
				return "diff_index"
			return "diff_worktree"
		if len(argv) >= 2 and argv[1] == "build":
			return "build"
		if argv and argv[0].endswith("python") and argv[-1].endswith(".py"):
			return "python_test"
		return "regression"

	@staticmethod
	def _default_stdout(key: str) -> str:
		return {
			"status": "## codex/test...origin/codex/test [ahead 2]\n M tools/magic_dev.py\n?? tests/tooling/test_magic_dev.py\n",
			"head": "abc123\n",
			"upstream": "origin/codex/test\n",
			"ahead": "2\t0\n",
			"discover": "tests/example/run_example_regression.cs\nTotal: 1\n",
		}.get(key, "")


class MagicDevTests(unittest.TestCase):
	def test_capabilities_are_machine_readable_and_complete(self) -> None:
		stdout = io.StringIO()
		with contextlib.redirect_stdout(stdout):
			returncode = magic_dev.main(["capabilities", "--json"])
		payload = json.loads(stdout.getvalue())

		self.assertEqual(0, returncode)
		self.assertEqual(
			{"status", "affected", "test", "verify", "full"},
			set(payload["operations"]),
		)
		self.assertTrue(all(value["supported"] for value in payload["operations"].values()))

	def test_status_parses_checkout_counts_and_diff_hygiene(self) -> None:
		status, _steps = magic_dev.collect_status(executor=FakeExecutor())

		self.assertEqual("abc123", status["head"])
		self.assertEqual("origin/codex/test", status["upstream"])
		self.assertEqual(2, status["ahead"])
		self.assertEqual(1, len(status["unstaged"]))
		self.assertEqual(1, len(status["untracked"]))
		self.assertTrue(status["diff_worktree_ok"])
		self.assertEqual((), status["embedded_conflict_markers"])

	def test_conflict_marker_scan_reports_path_line_and_marker(self) -> None:
		with tempfile.TemporaryDirectory() as temp_dir:
			root = Path(temp_dir)
			path = root / "owner.cs"
			path.write_text("before\n<<<<<<< current\nafter\n", encoding="utf-8")

			findings = magic_dev.scan_conflict_markers(root, ("owner.cs",))

		self.assertEqual(
			({"path": "owner.cs", "line": 2, "marker": "<<<<<<<"},),
			findings,
		)

	def test_conflict_marker_scan_skips_generated_battle_sim_output(self) -> None:
		with tempfile.TemporaryDirectory() as temp_dir:
			root = Path(temp_dir)
			path = root / ".tmp_battle_sim" / "report.json"
			path.parent.mkdir()
			path.write_text("<<<<<<< generated\n", encoding="utf-8")

			findings = magic_dev.scan_conflict_markers(root, (".tmp_battle_sim/report.json",))

		self.assertEqual((), findings)

	def test_affected_routes_cli_to_its_exact_python_test(self) -> None:
		selection = magic_dev.select_affected(("tools/magic_dev.py", "tools/README.md", "AGENTS.md"))

		self.assertEqual("focused", selection.mode)
		self.assertEqual(("tests/tooling/test_magic_dev.py",), tuple(target.path for target in selection.targets))
		self.assertEqual("python", selection.targets[0].kind)
		self.assertEqual(2, len(selection.ignored_paths))

	def test_affected_routes_battle_source_and_changed_test_without_full(self) -> None:
		selection = magic_dev.select_affected((
			"scripts/systems/battle/rules/BattleHitResolver.cs",
			"tests/battle_runtime/runtime/run_hit_regression.cs",
		))

		self.assertEqual("focused", selection.mode)
		self.assertEqual(("tests/battle_runtime",), tuple(target.path for target in selection.targets))

	def test_affected_excludes_opt_in_test_classes(self) -> None:
		selection = magic_dev.select_affected((
			"tests/e2e/run_login_e2e.cs",
			"tests/battle_runtime/simulation/run_balance_regression.cs",
			"tests/battle_runtime/benchmarks/run_path_benchmark.cs",
		))

		self.assertEqual("none", selection.mode)
		self.assertEqual(3, len(selection.ignored_paths))

	def test_unmapped_source_escalates_to_full(self) -> None:
		selection = magic_dev.select_affected(("addons/custom/owner.cs",))

		self.assertEqual("full", selection.mode)
		self.assertIn("unmapped source", selection.full_reasons[0])

	def test_path_normalization_rejects_escape(self) -> None:
		with self.assertRaises(ValueError):
			magic_dev.normalized_repo_path("../outside.cs")

	def test_path_normalization_preserves_leading_dot_directory(self) -> None:
		self.assertEqual(
			".tmp_battle_sim/report.json",
			magic_dev.normalized_repo_path(".tmp_battle_sim/report.json"),
		)

	def test_python_tooling_test_does_not_build(self) -> None:
		executor = FakeExecutor()
		stdout = io.StringIO()
		with contextlib.redirect_stdout(stdout):
			returncode = magic_dev.main(
				["test", "--path", "tests/tooling/test_magic_dev.py"],
				executor=executor,
			)

		self.assertEqual(0, returncode)
		self.assertEqual(1, len(executor.calls))
		self.assertTrue(executor.calls[0][-1].endswith("test_magic_dev.py"))

	def test_python_test_directory_runs_each_tooling_test_without_build(self) -> None:
		executor = FakeExecutor()
		with contextlib.redirect_stdout(io.StringIO()):
			returncode = magic_dev.main(
				["test", "--path", "tests/tooling"],
				executor=executor,
			)

		self.assertEqual(0, returncode)
		self.assertGreaterEqual(len(executor.calls), 2)
		self.assertTrue(all(call[-1].endswith(".py") for call in executor.calls))

	def test_godot_test_discovers_builds_then_runs_strict(self) -> None:
		executor = FakeExecutor()
		stdout = io.StringIO()
		with contextlib.redirect_stdout(stdout):
			returncode = magic_dev.main(
				["test", "--path", "tests/battle_runtime"],
				executor=executor,
			)

		self.assertEqual(0, returncode)
		self.assertEqual(3, len(executor.calls))
		self.assertIn("--list", executor.calls[0])
		self.assertIn("build", executor.calls[1])
		self.assertIn("--fail-on-output-error", executor.calls[2])
		self.assertIn("--lifecycle-correctness", executor.calls[2])
		self.assertNotIn("--include-simulation", executor.calls[2])
		self.assertNotIn("--include-benchmarks", executor.calls[2])

	def test_discovery_without_matches_prevents_build_and_execution(self) -> None:
		executor = FakeExecutor({"discover": (0, "Total: 0\n", "")})
		stderr = io.StringIO()
		with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(stderr):
			returncode = magic_dev.main(
				["test", "--path", "tests/battle_runtime"],
				executor=executor,
			)

		self.assertEqual(1, returncode)
		self.assertEqual(1, len(executor.calls))
		self.assertIn("No routine Godot regressions matched", stderr.getvalue())

	def test_default_jobs_has_safe_bounds(self) -> None:
		self.assertEqual(1, magic_dev.default_jobs(1))
		self.assertEqual(4, magic_dev.default_jobs(8))
		self.assertEqual(16, magic_dev.default_jobs(128))


if __name__ == "__main__":
	unittest.main()
