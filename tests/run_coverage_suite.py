#!/usr/bin/env python3

from __future__ import annotations

import argparse
import subprocess
import sys
import uuid
import xml.etree.ElementTree as ET
from pathlib import Path


DEFAULT_JOBS = "8"
DEFAULT_OUTPUT = "artifacts/coverage/coverage.cobertura.xml"


def build_parser() -> argparse.ArgumentParser:
	parser = argparse.ArgumentParser(
		description=(
			"Build the Godot C# project, run the headless regression suite "
			"under Microsoft dotnet-coverage, and print measured production "
			"line coverage."
		)
	)
	parser.add_argument(
		"--jobs",
		"-j",
		default=DEFAULT_JOBS,
		help=(
			"Parallel Godot jobs passed to run_regression_suite.py. "
			f"Default: {DEFAULT_JOBS}."
		),
	)
	parser.add_argument(
		"--output",
		default=DEFAULT_OUTPUT,
		help=f"Cobertura output path. Default: {DEFAULT_OUTPUT}.",
	)
	parser.add_argument(
		"runner_args",
		nargs=argparse.REMAINDER,
		help=(
			"Additional arguments for run_regression_suite.py. "
			"Prefix them with '--', for example: "
			"-- --pattern counterattack."
		),
	)
	return parser


def run_checked(command: list[str], repo_root: Path) -> None:
	print("+ " + subprocess.list2cmdline(command), flush=True)
	completed = subprocess.run(command, cwd=repo_root, check=False)
	if completed.returncode != 0:
		raise SystemExit(completed.returncode)


def run_coverage_suite(
	repo_root: Path,
	settings_path: Path,
	output_path: Path,
	jobs: str,
	runner_args: list[str],
) -> None:
	session_id = f"magic-coverage-{uuid.uuid4().hex}"
	assembly_path = (
		repo_root
		/ ".godot"
		/ "mono"
		/ "temp"
		/ "bin"
		/ "Debug"
		/ "magic.dll"
	)
	if not assembly_path.is_file():
		raise RuntimeError(
			f"built Godot assembly was not found: {assembly_path}"
		)
	instrument_command = [
		"dotnet",
		"tool",
		"run",
		"dotnet-coverage",
		"instrument",
		str(assembly_path),
		"--settings",
		str(settings_path),
		"--session-id",
		session_id,
		"--nologo",
	]
	server_command = [
		"dotnet",
		"tool",
		"run",
		"dotnet-coverage",
		"collect",
		"--settings",
		str(settings_path),
		"--session-id",
		session_id,
		"--server-mode",
		"--background",
		"--output",
		str(output_path),
		"--output-format",
		"cobertura",
		"--nologo",
	]
	shutdown_command = [
		"dotnet",
		"tool",
		"run",
		"dotnet-coverage",
		"shutdown",
		session_id,
		"--nologo",
	]
	uninstrument_command = [
		"dotnet",
		"tool",
		"run",
		"dotnet-coverage",
		"uninstrument",
		str(assembly_path),
		"--settings",
		str(settings_path),
		"--nologo",
	]
	regression_command = [
		sys.executable,
		str(repo_root / "tests" / "run_regression_suite.py"),
		"--jobs",
		str(jobs),
		*runner_args,
	]
	run_checked(instrument_command, repo_root)
	try:
		run_checked(server_command, repo_root)
		try:
			print(
				"+ " + subprocess.list2cmdline(regression_command),
				flush=True,
			)
			regression = subprocess.run(
				regression_command,
				cwd=repo_root,
				check=False,
			)
		finally:
			run_checked(shutdown_command, repo_root)
		if regression.returncode != 0:
			raise SystemExit(regression.returncode)
	finally:
		run_checked(uninstrument_command, repo_root)


def parse_coverage_summary(
	report_path: Path,
	repo_root: Path,
) -> tuple[int, int, int | None, int | None]:
	if not report_path.is_file():
		raise RuntimeError(f"coverage report was not created: {report_path}")
	root = ET.parse(report_path).getroot()
	if root.tag != "coverage":
		raise RuntimeError(
			f"unexpected coverage root element: {root.tag}"
		)
	try:
		lines_valid = int(root.attrib["lines-valid"])
		lines_covered = int(root.attrib["lines-covered"])
	except (KeyError, ValueError) as exc:
		raise RuntimeError(
			"coverage report is missing Cobertura line counters"
		) from exc
	try:
		branches_valid = int(root.attrib["branches-valid"])
		branches_covered = int(root.attrib["branches-covered"])
	except (KeyError, ValueError):
		branches_valid = None
		branches_covered = None
	if lines_valid <= 0:
		raise RuntimeError(
			"coverage report contains no production sequence points"
		)

	invalid_sources: list[str] = []
	for class_node in root.findall(".//class"):
		filename = class_node.attrib.get("filename", "")
		normalized = filename.replace("\\", "/").lower()
		if (
			"/tests/" in f"/{normalized.lstrip('/')}"
			or "/tools/" in f"/{normalized.lstrip('/')}"
			or "/.godot/" in f"/{normalized.lstrip('/')}"
		):
			invalid_sources.append(filename)
	if invalid_sources:
		sample = ", ".join(invalid_sources[:5])
		raise RuntimeError(
			"coverage denominator contains excluded test/tool/generated "
			f"sources: {sample}"
		)

	try:
		report_label = report_path.relative_to(repo_root)
	except ValueError:
		report_label = report_path
	line_rate = 100.0 * lines_covered / lines_valid
	print(f"Coverage report: {report_label}", flush=True)
	print(
		f"Production line coverage: {line_rate:.2f}% "
		f"({lines_covered}/{lines_valid})",
		flush=True,
	)
	if (
		branches_valid is not None
		and branches_covered is not None
		and branches_valid > 0
	):
		branch_rate = 100.0 * branches_covered / branches_valid
		print(
			f"Production branch coverage: {branch_rate:.2f}% "
			f"({branches_covered}/{branches_valid})",
			flush=True,
		)
	else:
		print(
			"Production branch coverage: unavailable "
			"(collector emitted no branch counters)",
			flush=True,
		)
	return (
		lines_covered,
		lines_valid,
		branches_covered,
		branches_valid,
	)


def main() -> int:
	args = build_parser().parse_args()
	repo_root = Path(__file__).resolve().parents[1]
	settings_path = repo_root / "tests" / "coverage.runsettings"
	output_path = Path(args.output)
	if not output_path.is_absolute():
		output_path = repo_root / output_path
	output_path.parent.mkdir(parents=True, exist_ok=True)
	if output_path.exists():
		output_path.unlink()

	run_checked(["dotnet", "tool", "restore"], repo_root)
	run_checked(["dotnet", "build", "magic.csproj"], repo_root)

	runner_args = list(args.runner_args)
	if runner_args and runner_args[0] == "--":
		runner_args = runner_args[1:]
	run_coverage_suite(
		repo_root,
		settings_path,
		output_path,
		str(args.jobs),
		runner_args,
	)
	parse_coverage_summary(output_path, repo_root)
	return 0


if __name__ == "__main__":
	try:
		raise SystemExit(main())
	except RuntimeError as error:
		print(f"coverage error: {error}", file=sys.stderr)
		raise SystemExit(1)
