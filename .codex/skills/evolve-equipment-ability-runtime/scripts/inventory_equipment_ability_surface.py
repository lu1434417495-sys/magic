#!/usr/bin/env python3
"""Inventory the typed equipment-ability surface without modifying the repo."""

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
            "authoring_and_abi",
            ("scripts/player/progression/equipment_abilities/*.cs",),
        ),
        (
            "content_packs",
            ("data/configs/equipment_abilities/*.tres",),
        ),
        (
            "validation_and_definition_projection",
            (
                "scripts/player/progression/equipment_abilities/*Validator*.cs",
                "scripts/player/progression/equipment_abilities/*Registry*.cs",
                "scripts/player/progression/equipment_abilities/*Definition*.cs",
                "scripts/player/progression/equipment_abilities/*HandlerSpec*.cs",
                "scripts/player/progression/equipment_abilities/*DeclarationCatalog*.cs",
            ),
        ),
        (
            "snapshot_and_catalog",
            (
                "scripts/systems/content/ContentSnapshot.cs",
                "scripts/systems/content/ContentSnapshotBuilder.cs",
                "scripts/systems/content/GameContentCatalog.cs",
            ),
        ),
        (
            "battle_projection_and_state",
            (
                "scripts/systems/battle/core/BattleEquipment*.cs",
                "scripts/systems/battle/core/BattleUnitEquipment*.cs",
                "scripts/systems/battle/runtime/BattleEquipmentAbilityProjectionService.cs",
                "scripts/systems/battle/runtime/BattleUnitFactory.cs",
                "scripts/systems/world/EncounterRosterBuilder.cs",
            ),
        ),
        (
            "runtime_dispatch_and_resolvers",
            (
                "scripts/systems/battle/runtime/BattleEquipment*.cs",
                "scripts/systems/battle/runtime/EquipmentAbilityUsageRuntime.cs",
            ),
        ),
        (
            "ports_and_canonical_services",
            (
                "scripts/systems/battle/rules/IBattleEquipment*.cs",
                "scripts/systems/battle/rules/BattleAttackCheckPolicyService*.cs",
                "scripts/systems/battle/rules/BattleDamageResolver*.cs",
                "scripts/systems/battle/runtime/BattleSkillExecutionOrchestrator*.cs",
                "scripts/systems/battle/runtime/BattleRuntimeModule*.cs",
            ),
        ),
        (
            "state_save_and_writeback",
            (
                "scripts/player/equipment/Equipment*State.cs",
                "scripts/player/warehouse/EquipmentInstanceState.cs",
                "scripts/player/progression/PartyState.SaveSnapshot.cs",
                "scripts/systems/progression/CharacterBattleWritebackService.cs",
                "scripts/systems/persistence/SaveSerializer.cs",
                "scripts/systems/persistence/SaveSchemaVersions.cs",
            ),
        ),
        (
            "cross_path_consumers",
            (
                "scripts/systems/battle/runtime/BattleSkillAvailabilityService.cs",
                "scripts/systems/battle/ai/*Mutation*.cs",
                "scripts/systems/battle/ai/*Skill*Evaluator*.cs",
                "scripts/systems/battle/presentation/BattleHud*.cs",
                "scripts/systems/battle/sim/BattleSim*Unit*.cs",
                "scripts/systems/game_runtime/headless/*Snapshot*.cs",
            ),
        ),
        (
            "regressions",
            (
                "tests/progression/schema/*equipment_ability*.cs",
                "tests/battle_runtime/**/*equipment*.cs",
                "tests/battle_runtime/**/*weapon_ability*.cs",
                "tests/battle_runtime/state_schema/*battle_unit_state*.cs",
                "tests/runtime/persistence/*equipment*.cs",
            ),
        ),
        (
            "context",
            (
                "docs/design/project_context_units.md",
                "docs/design/battle/equipment_ability_runtime.md",
                "docs/design/battle/skill_runtime.md",
                "docs/design/battle/weapon_dice_and_equipment.md",
            ),
        ),
    ]
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Inventory equipment-ability authoring, typed definitions, projection, "
            "runtime, ports, persistence, consumers, and regressions."
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


def build_inventory(
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


def render_markdown(inventory: OrderedDict[str, object]) -> str:
    terms = inventory["terms"]
    lines = [
        "# Equipment ability surface inventory",
        "",
        f"- Repository: `{inventory['repository_root']}`",
        f"- Terms: {', '.join(f'`{term}`' for term in terms) if terms else '(all)'}",
        f"- Changed only: `{'yes' if inventory['changed_only'] else 'no'}`",
        f"- Scope: {inventory['inventory_scope']}",
        f"- Unique files: `{inventory['unique_file_count']}`",
    ]
    categories = inventory["categories"]
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
        inventory = build_inventory(root, args.term, args.changed_only)
    except (OSError, RuntimeError, ValueError) as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 2

    if args.format == "json":
        print(json.dumps(inventory, ensure_ascii=False, indent=2))
    else:
        print(render_markdown(inventory))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
