#!/usr/bin/env python3
"""Summarize a Cobertura report without mutating the repository."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET
from datetime import datetime, timezone
from pathlib import Path


CONDITION_RE = re.compile(r"\((\d+)\s*/\s*(\d+)\)")


def normalize_path(value: str) -> str:
    return value.replace("\\", "/").lstrip("./").lower()


def matches_filters(filename: str, includes: list[str], excludes: list[str]) -> bool:
    normalized = normalize_path(filename)
    include_ok = not includes or any(
        normalized.startswith(normalize_path(prefix)) for prefix in includes
    )
    excluded = any(normalized.startswith(normalize_path(prefix)) for prefix in excludes)
    return include_ok and not excluded


def git_facts(repo_root: Path | None) -> dict[str, object] | None:
    if repo_root is None:
        return None

    def run(*args: str) -> str:
        result = subprocess.run(
            ["git", "-C", str(repo_root), *args],
            check=True,
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
        )
        return result.stdout.strip()

    try:
        status = run("status", "--short")
        return {
            "repo_root": str(repo_root.resolve()),
            "head": run("rev-parse", "HEAD"),
            "branch": run("branch", "--show-current"),
            "dirty": bool(status),
            "status_line_count": len(status.splitlines()) if status else 0,
        }
    except (OSError, subprocess.CalledProcessError) as exc:
        return {"repo_root": str(repo_root), "error": str(exc)}


def summarize(
    report_path: Path,
    includes: list[str],
    excludes: list[str],
    repo_root: Path | None,
    show_files: bool,
) -> dict[str, object]:
    data = report_path.read_bytes()
    root = ET.fromstring(data)
    classes = root.findall(".//class")

    files: list[dict[str, object]] = []
    total_lines = covered_lines = 0
    total_branches = covered_branches = 0
    branch_detail_available = False

    for class_node in classes:
        filename = class_node.attrib.get("filename", "")
        if not filename or not matches_filters(filename, includes, excludes):
            continue

        file_lines = file_covered = 0
        file_branches = file_branches_covered = 0
        for line in class_node.findall("./lines/line"):
            file_lines += 1
            if int(line.attrib.get("hits", "0")) > 0:
                file_covered += 1
            condition = line.attrib.get("condition-coverage", "")
            match = CONDITION_RE.search(condition)
            if match:
                branch_detail_available = True
                file_branches_covered += int(match.group(1))
                file_branches += int(match.group(2))

        total_lines += file_lines
        covered_lines += file_covered
        total_branches += file_branches
        covered_branches += file_branches_covered
        files.append(
            {
                "filename": filename,
                "lines_covered": file_covered,
                "lines_valid": file_lines,
                "line_rate": (file_covered / file_lines) if file_lines else None,
                "branches_covered": file_branches_covered if branch_detail_available else None,
                "branches_valid": file_branches if branch_detail_available else None,
            }
        )

    if not files:
        raise ValueError("No Cobertura class matched the requested path filters.")

    stat = report_path.stat()
    result: dict[str, object] = {
        "report": {
            "path": str(report_path.resolve()),
            "sha256": hashlib.sha256(data).hexdigest(),
            "modified_utc": datetime.fromtimestamp(
                stat.st_mtime, tz=timezone.utc
            ).isoformat(),
            "coverage_timestamp": root.attrib.get("timestamp"),
            "sources": [node.text or "" for node in root.findall("./sources/source")],
        },
        "filters": {
            "include_prefixes": includes,
            "exclude_prefixes": excludes,
        },
        "totals": {
            "files": len(files),
            "lines_covered": covered_lines,
            "lines_valid": total_lines,
            "line_rate": covered_lines / total_lines if total_lines else None,
            "branches_covered": covered_branches if branch_detail_available else None,
            "branches_valid": total_branches if branch_detail_available else None,
            "branch_rate": (
                covered_branches / total_branches
                if branch_detail_available and total_branches
                else None
            ),
        },
        "current_checkout": git_facts(repo_root),
        "provenance_warning": (
            "Current checkout facts do not prove which source produced this report. "
            "Record the instrumented checkout and commit separately."
        ),
    }
    if show_files:
        result["files"] = sorted(files, key=lambda item: str(item["filename"]).lower())
    return result


def run_self_test() -> int:
    xml = """<?xml version="1.0"?>
<coverage timestamp="123">
  <sources><source>C:/repo</source></sources>
  <packages><package><classes>
    <class filename="scripts/systems/battle/a.cs"><lines>
      <line number="1" hits="1"/>
      <line number="2" hits="0" branch="true" condition-coverage="50% (1/2)"/>
    </lines></class>
    <class filename="tests/b.cs"><lines><line number="1" hits="1"/></lines></class>
  </classes></package></packages>
</coverage>"""
    path: Path | None = None
    try:
        with tempfile.NamedTemporaryFile("w", suffix=".xml", delete=False, encoding="utf-8") as handle:
            handle.write(xml)
            path = Path(handle.name)
        result = summarize(path, ["scripts/systems/battle/"], [], None, False)
        totals = result["totals"]
        assert totals["files"] == 1
        assert totals["lines_covered"] == 1
        assert totals["lines_valid"] == 2
        assert totals["branches_covered"] == 1
        assert totals["branches_valid"] == 2
        print("self-test: PASS")
        return 0
    finally:
        if path is not None:
            path.unlink(missing_ok=True)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("report", nargs="?", type=Path)
    parser.add_argument("--include-prefix", action="append", default=[])
    parser.add_argument("--exclude-prefix", action="append", default=[])
    parser.add_argument("--repo-root", type=Path)
    parser.add_argument("--show-files", action="store_true")
    parser.add_argument("--self-test", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    if args.self_test:
        return run_self_test()
    if args.report is None:
        print("error: report path is required unless --self-test is used", file=sys.stderr)
        return 2
    try:
        result = summarize(
            args.report,
            args.include_prefix,
            args.exclude_prefix,
            args.repo_root,
            args.show_files,
        )
    except (OSError, ET.ParseError, ValueError) as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 2
    json.dump(result, sys.stdout, ensure_ascii=False, indent=2)
    print()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
