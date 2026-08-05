#!/usr/bin/env python3
"""Build a deterministic, read-only inventory for battle AI changes."""

from __future__ import annotations

import argparse
import json
import subprocess
import sys
from collections import OrderedDict
from pathlib import Path
from typing import Iterable


SURFACES: "OrderedDict[str, tuple[str, ...]]" = OrderedDict(
    [
        (
            "authoring",
            (
                "scripts/enemies/actions/*.cs",
                "scripts/enemies/*Ai*.cs",
                "data/configs/enemies/brains/*.tres",
                "data/configs/enemies/templates/*.tres",
            ),
        ),
        (
            "definitions",
            (
                "scripts/enemies/definitions/*Ai*.cs",
                "scripts/enemies/definitions/*Action*.cs",
                "scripts/enemies/definitions/*SkillCompatibility*.cs",
            ),
        ),
        (
            "assembly_and_decision",
            (
                "scripts/systems/battle/ai/*Assembler*.cs",
                "scripts/systems/battle/ai/*RuntimeAction*.cs",
                "scripts/systems/battle/ai/*Decision*.cs",
                "scripts/systems/battle/ai/*SafetyGate*.cs",
                "scripts/systems/battle/ai/*FailurePolicy*.cs",
                "scripts/systems/battle/ai/BattleAiService.cs",
            ),
        ),
        (
            "evaluators_and_queries",
            (
                "scripts/systems/battle/ai/*Evaluator*.cs",
                "scripts/systems/battle/ai/*Query*.cs",
                "scripts/systems/battle/ai/*Candidate*.cs",
                "scripts/systems/battle/ai/*Affordance*.cs",
                "scripts/systems/battle/ai/*Objective*.cs",
            ),
        ),
        (
            "scoring_and_trace",
            (
                "scripts/systems/battle/ai/*Score*.cs",
                "scripts/systems/battle/ai/*Trace*.cs",
                "scripts/enemies/definitions/BattleAiScoreProfileDefinition.cs",
                "docs/design/battle/ai_score_parameters.md",
                "tools/battle_sim_tuner/search_space.py",
            ),
        ),
        (
            "canonical_preview",
            (
                "scripts/systems/battle/runtime/BattleCommandPreviewService.cs",
                "scripts/systems/battle/runtime/BattleSkillPreviewService.cs",
                "scripts/systems/battle/core/BattlePreview*.cs",
                "scripts/systems/battle/rules/*Preview*.cs",
                "docs/design/battle/skill_runtime.md",
            ),
        ),
        (
            "mutation_and_lifetime",
            (
                "scripts/systems/battle/ai/*Mutation*.cs",
                "scripts/systems/battle/ai/*PayloadGuard*.cs",
                "scripts/systems/battle/ai/*DecisionResult*.cs",
                "tests/battle_runtime/ai/*mutation*.cs",
                "tests/battle_runtime/ai/*lifetime*.cs",
            ),
        ),
        (
            "path_and_performance",
            (
                "scripts/systems/battle/runtime/BattleMovementQueryService.cs",
                "scripts/systems/battle/runtime/BattleRuntimeServices.cs",
                "scripts/systems/battle/runtime/BattleRuntimeModule.cs",
                "tests/battle_runtime/runtime/*movement_query*.cs",
                "tests/battle_runtime/benchmarks/*ai*performance*.cs",
            ),
        ),
        (
            "regressions",
            (
                "tests/battle_runtime/ai/*.cs",
                "tests/progression/schema/*enemy*ai*.cs",
                "tests/runtime/validation/*ai*.cs",
            ),
        ),
        (
            "context",
            (
                "docs/design/project_context_units.md",
                "docs/design/battle/ai_score_parameters.md",
                "docs/design/battle/skill_runtime.md",
            ),
        ),
    ]
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Inventory battle AI authoring, runtime, preview, mutation, path, and "
            "test surfaces without modifying the repository."
        )
    )
    parser.add_argument(
        "--root",
        default=".",
        help="Repository root containing magic.csproj (default: current directory).",
    )
    parser.add_argument(
        "--term",
        action="append",
        default=[],
        help=(
            "Keep files whose path or text contains this term. Repeat for OR matching. "
            "Omit to include the complete surface."
        ),
    )
    parser.add_argument(
        "--changed-only",
        action="store_true",
        help="Keep only tracked or untracked files reported by git status.",
    )
    parser.add_argument(
        "--format",
        choices=("markdown", "json"),
        default="markdown",
        help="Output format (default: markdown).",
    )
    return parser.parse_args()


def repository_root(value: str) -> Path:
    root = Path(value).resolve()
    if not (root / "magic.csproj").is_file():
        raise ValueError(f"repository root does not contain magic.csproj: {root}")
    return root


def relative_posix(path: Path, root: Path) -> str:
    return path.relative_to(root).as_posix()


def expand_patterns(root: Path, patterns: Iterable[str]) -> list[Path]:
    paths: set[Path] = set()
    for pattern in patterns:
        paths.update(path for path in root.glob(pattern) if path.is_file())
    return sorted(paths, key=lambda item: relative_posix(item, root).casefold())


def matches_terms(path: Path, root: Path, terms: list[str]) -> bool:
    if not terms:
        return True
    lowered_terms = [term.casefold() for term in terms]
    rel = relative_posix(path, root).casefold()
    if any(term in rel for term in lowered_terms):
        return True
    try:
        text = path.read_text(encoding="utf-8", errors="ignore").casefold()
    except OSError:
        return False
    return any(term in text for term in lowered_terms)


def git_status(root: Path) -> dict[str, str]:
    process = subprocess.run(
        ["git", "status", "--porcelain=v1", "--untracked-files=all"],
        cwd=root,
        check=False,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
    )
    if process.returncode != 0:
        message = process.stderr.strip() or "unknown git status failure"
        raise RuntimeError(message)

    result: dict[str, str] = {}
    for line in process.stdout.splitlines():
        if len(line) < 4:
            continue
        status = line[:2]
        raw_path = line[3:]
        if " -> " in raw_path:
            raw_path = raw_path.rsplit(" -> ", 1)[1]
        normalized = raw_path.strip('"').replace("\\", "/")
        result[normalized] = status
    return result


def build_packet(
    root: Path, terms: list[str], changed_only: bool
) -> OrderedDict[str, object]:
    status_by_path = git_status(root)
    categories: "OrderedDict[str, list[dict[str, str]]]" = OrderedDict()

    for category, patterns in SURFACES.items():
        entries: list[dict[str, str]] = []
        for path in expand_patterns(root, patterns):
            rel = relative_posix(path, root)
            if not matches_terms(path, root, terms):
                continue
            status = status_by_path.get(rel, "")
            if changed_only and not status:
                continue
            entries.append({"path": rel, "status": status})
        categories[category] = entries

    unique_paths = {
        entry["path"] for entries in categories.values() for entry in entries
    }
    return OrderedDict(
        [
            ("repository_root", root.as_posix()),
            ("terms", terms),
            ("changed_only", changed_only),
            (
                "inventory_scope",
                "Lexical scope seed only; expand direct owners, shared types, and tests manually.",
            ),
            ("unique_file_count", len(unique_paths)),
            ("categories", categories),
        ]
    )


def render_markdown(packet: OrderedDict[str, object]) -> str:
    terms = packet["terms"]
    lines = [
        "# Battle AI change packet",
        "",
        f"- Repository: `{packet['repository_root']}`",
        f"- Terms: {', '.join(f'`{term}`' for term in terms) if terms else '(all)'}",
        f"- Changed only: `{'yes' if packet['changed_only'] else 'no'}`",
        f"- Scope: {packet['inventory_scope']}",
        f"- Unique files: `{packet['unique_file_count']}`",
    ]
    categories = packet["categories"]
    assert isinstance(categories, OrderedDict)
    for category, entries in categories.items():
        lines.extend(["", f"## {category} ({len(entries)})", ""])
        if not entries:
            lines.append("- (none)")
            continue
        for entry in entries:
            suffix = f" [{entry['status']}]" if entry["status"] else ""
            lines.append(f"- `{entry['path']}`{suffix}")
    return "\n".join(lines)


def main() -> int:
    args = parse_args()
    try:
        root = repository_root(args.root)
        packet = build_packet(root, args.term, args.changed_only)
    except (OSError, RuntimeError, ValueError) as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 2

    if args.format == "json":
        print(json.dumps(packet, ensure_ascii=False, indent=2))
    else:
        print(render_markdown(packet))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
