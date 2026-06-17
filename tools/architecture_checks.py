#!/usr/bin/env python3
"""Report architecture-boundary risks in the repository.

The script is intentionally read-only. By default it prints a baseline report and
exits 0 so it can be introduced before the existing debt is cleaned up. Use
`--fail-on-findings` once the baseline has been reviewed and a whitelist exists.
"""

from __future__ import annotations

import argparse
import re
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable


REPO_ROOT = Path(__file__).resolve().parents[1]

SKIP_DIRS = {
    ".git",
    ".godot",
    ".tmp",
    ".tmp_tuner",
    ".ralph",
    ".mindfs",
    "__pycache__",
}

CS_DYNAMIC_CALL_RE = re.compile(r"\.(?:Call|Set)\s*\(")
TO_DICTIONARY_RE = re.compile(r"\bToDictionary\s*\(")
GDICTIONARY_FIELD_RE = re.compile(
    r"\b(?:public|internal|private|protected)\s+"
    r"(?:readonly\s+)?"
    r"(?:GDictionary|Godot\.Collections\.Dictionary)\s+"
    r"[A-Za-z_][A-Za-z0-9_]*\s*(?:=|;)"
)
INTERNAL_TEST_WRITE_RE = re.compile(
    r"\b[A-Za-z_][A-Za-z0-9_]*\._[A-Za-z0-9_]+\s*="
)


@dataclass(frozen=True)
class Finding:
    category: str
    path: Path
    line_number: int
    text: str

    def format(self) -> str:
        rel = self.path.relative_to(REPO_ROOT)
        return f"{rel}:{self.line_number}: {self.text.strip()}"


def iter_files(root: Path, pattern: str) -> Iterable[Path]:
    for path in root.rglob(pattern):
        if any(part in SKIP_DIRS for part in path.parts):
            continue
        if path.is_file():
            yield path


def scan_file(path: Path, category: str, pattern: re.Pattern[str]) -> list[Finding]:
    findings: list[Finding] = []
    try:
        lines = path.read_text(encoding="utf-8").splitlines()
    except UnicodeDecodeError:
        lines = path.read_text(encoding="utf-8", errors="replace").splitlines()
    for index, line in enumerate(lines, start=1):
        if pattern.search(line):
            findings.append(Finding(category, path, index, line))
    return findings


def is_allowed_to_dictionary_projection(path: Path) -> bool:
    rel = path.relative_to(REPO_ROOT).as_posix()
    allowed_fragments = (
        "/ui/",
        "/presentation/",
        "/persistence/",
        "/headless/",
        "Projection",
        "Payload",
        "Snapshot",
        "Renderer",
        "Trace",
        "ContentRegistry",
        "Def.cs",
        "State.cs",
    )
    return any(fragment in rel for fragment in allowed_fragments)


def collect_findings() -> dict[str, list[Finding]]:
    scripts_cs = list(iter_files(REPO_ROOT / "scripts", "*.cs"))
    tests_cs = list(iter_files(REPO_ROOT / "tests", "*.cs"))

    findings: dict[str, list[Finding]] = {
        "dynamic_call_or_set": [],
        "to_dictionary_all": [],
        "to_dictionary_core_review": [],
        "gdictionary_core_field": [],
        "test_internal_field_write": [],
    }

    for path in scripts_cs:
        findings["dynamic_call_or_set"].extend(
            scan_file(path, "dynamic_call_or_set", CS_DYNAMIC_CALL_RE)
        )
        to_dictionary = scan_file(path, "to_dictionary_all", TO_DICTIONARY_RE)
        findings["to_dictionary_all"].extend(to_dictionary)
        if not is_allowed_to_dictionary_projection(path):
            findings["to_dictionary_core_review"].extend(
                Finding("to_dictionary_core_review", item.path, item.line_number, item.text)
                for item in to_dictionary
            )
        findings["gdictionary_core_field"].extend(
            scan_file(path, "gdictionary_core_field", GDICTIONARY_FIELD_RE)
        )

    for path in tests_cs:
        findings["test_internal_field_write"].extend(
            scan_file(path, "test_internal_field_write", INTERNAL_TEST_WRITE_RE)
        )

    return findings


def print_section(title: str, items: list[Finding], max_results: int) -> None:
    print(f"\n## {title}")
    print(f"count: {len(items)}")
    if not items:
        return
    for finding in items[:max_results]:
        print(f"- {finding.format()}")
    remaining = len(items) - max_results
    if remaining > 0:
        print(f"- ... {remaining} more")


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(
        description="Report architecture-boundary risks without modifying files."
    )
    parser.add_argument(
        "--max-results",
        type=int,
        default=80,
        help="maximum findings printed per section",
    )
    parser.add_argument(
        "--fail-on-findings",
        action="store_true",
        help="exit non-zero if any finding is reported",
    )
    args = parser.parse_args(argv)

    findings = collect_findings()

    print("# Architecture Checks")
    print(f"repo: {REPO_ROOT}")
    print("mode: report-only" if not args.fail_on_findings else "mode: fail-on-findings")

    print_section(
        "scripts dynamic .Call/.Set usage",
        findings["dynamic_call_or_set"],
        args.max_results,
    )
    print_section(
        "scripts ToDictionary usage outside common projection paths",
        findings["to_dictionary_core_review"],
        args.max_results,
    )
    print_section(
        "scripts GDictionary fields",
        findings["gdictionary_core_field"],
        args.max_results,
    )
    print_section(
        "tests internal field writes",
        findings["test_internal_field_write"],
        args.max_results,
    )

    total_findings = sum(
        len(findings[key])
        for key in (
            "dynamic_call_or_set",
            "to_dictionary_core_review",
            "gdictionary_core_field",
            "test_internal_field_write",
        )
    )
    print(f"\nTotal review findings: {total_findings}")

    if args.fail_on_findings and total_findings:
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
