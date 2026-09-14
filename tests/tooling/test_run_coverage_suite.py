from __future__ import annotations

import contextlib
import importlib.util
import io
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock


COVERAGE_RUNNER_PATH = (
	Path(__file__).resolve().parents[1]
	/ "run_coverage_suite.py"
)
SPEC = importlib.util.spec_from_file_location(
	"run_coverage_suite_under_test",
	COVERAGE_RUNNER_PATH,
)
if SPEC is None or SPEC.loader is None:
	raise RuntimeError(
		f"Unable to load coverage runner from {COVERAGE_RUNNER_PATH}"
	)
coverage_runner = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = coverage_runner
SPEC.loader.exec_module(coverage_runner)


class CoverageSuiteTests(unittest.TestCase):
	def test_summary_reports_production_lines_without_fabricating_branches(
		self,
	) -> None:
		with tempfile.TemporaryDirectory() as temp_dir:
			repo_root = Path(temp_dir)
			report_path = repo_root / "coverage.xml"
			report_path.write_text(
				"""<?xml version="1.0" encoding="utf-8"?>
<coverage lines-covered="3" lines-valid="4" line-rate="0.75" branch-rate="1">
  <packages><package name="magic"><classes>
    <class name="Runtime" filename="C:\\repo\\scripts\\Runtime.cs" />
  </classes></package></packages>
</coverage>
""",
				encoding="utf-8",
			)
			output = io.StringIO()
			with contextlib.redirect_stdout(output):
				summary = coverage_runner.parse_coverage_summary(
					report_path,
					repo_root,
				)

		self.assertEqual((3, 4, None, None), summary)
		self.assertIn("Production line coverage: 75.00% (3/4)", output.getvalue())
		self.assertIn("branch coverage: unavailable", output.getvalue())

	def test_summary_rejects_test_sources_in_denominator(self) -> None:
		with tempfile.TemporaryDirectory() as temp_dir:
			repo_root = Path(temp_dir)
			report_path = repo_root / "coverage.xml"
			report_path.write_text(
				"""<?xml version="1.0" encoding="utf-8"?>
<coverage lines-covered="1" lines-valid="1">
  <packages><package name="magic"><classes>
    <class name="Fixture" filename="C:\\repo\\tests\\Fixture.cs" />
  </classes></package></packages>
</coverage>
""",
				encoding="utf-8",
			)
			with self.assertRaisesRegex(
				RuntimeError,
				"excluded test/tool/generated sources",
			):
				coverage_runner.parse_coverage_summary(
					report_path,
					repo_root,
				)

	def test_failed_regression_still_shutdowns_and_uninstruments(self) -> None:
		with tempfile.TemporaryDirectory() as temp_dir:
			repo_root = Path(temp_dir)
			assembly_path = (
				repo_root
				/ ".godot"
				/ "mono"
				/ "temp"
				/ "bin"
				/ "Debug"
				/ "magic.dll"
			)
			assembly_path.parent.mkdir(parents=True)
			assembly_path.write_bytes(b"fixture")
			settings_path = repo_root / "coverage.runsettings"
			settings_path.write_text("<Configuration />", encoding="utf-8")
			output_path = repo_root / "coverage.xml"
			commands: list[list[str]] = []

			def record(command: list[str], _repo_root: Path) -> None:
				commands.append(command)

			with (
				mock.patch.object(
					coverage_runner,
					"run_checked",
					side_effect=record,
				),
				mock.patch.object(
					coverage_runner.subprocess,
					"run",
					return_value=mock.Mock(returncode=7),
				),
				self.assertRaises(SystemExit) as exit_context,
			):
				coverage_runner.run_coverage_suite(
					repo_root,
					settings_path,
					output_path,
					"4",
					[],
				)

		self.assertEqual(7, exit_context.exception.code)
		self.assertEqual(
			["instrument", "collect", "shutdown", "uninstrument"],
			[
				next(
					token
					for token in (
						"instrument",
						"collect",
						"shutdown",
						"uninstrument",
					)
					if token in command
				)
				for command in commands
			],
		)


if __name__ == "__main__":
	unittest.main()
