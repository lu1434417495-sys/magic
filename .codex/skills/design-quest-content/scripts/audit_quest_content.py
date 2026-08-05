#!/usr/bin/env python3
"""Inventory authored quest rewards and emit manual-review candidates."""

from __future__ import annotations

import argparse
import json
import re
import sys
from collections import Counter
from pathlib import Path


SCALAR_PATTERNS = {
    "quest_id": re.compile(r'^quest_id\s*=\s*&?"([^"]*)"', re.MULTILINE),
    "provider_kind": re.compile(r'^provider_kind\s*=\s*&?"([^"]*)"', re.MULTILINE),
    "provider_interaction_id": re.compile(
        r'^provider_interaction_id\s*=\s*&?"([^"]*)"', re.MULTILINE
    ),
    "failure_policy": re.compile(r'^failure_policy\s*=\s*&?"([^"]*)"', re.MULTILINE),
    "skill_id": re.compile(r'^skill_id\s*=\s*&?"([^"]*)"', re.MULTILINE),
    "growth_tier": re.compile(r'^growth_tier\s*=\s*&?"([^"]*)"', re.MULTILINE),
    "learn_source": re.compile(r'^learn_source\s*=\s*&?"([^"]*)"', re.MULTILINE),
}
ARRAY_LINE = re.compile(r"^([a-z_]+)\s*=\s*Array[^\n]*$", re.MULTILINE)
STRING_NAME = re.compile(r'&"([^"]+)"')
OBJECTIVE_TYPE = re.compile(r'&"objective_type":\s*&"([^"]+)"')
REWARD_TYPE = re.compile(r'&"reward_type":\s*&"([^"]+)"')
MEMBER_ID = re.compile(r'&"member_id":\s*&"([^"]+)"')
PENDING_ENTRY = re.compile(
    r'&"entry_type":\s*&"([^"]+)".*?'
    r'&"target_id":\s*&"([^"]+)".*?'
    r'&"amount":\s*(-?\d+)'
)


def scalar(text: str, key: str) -> str:
    match = SCALAR_PATTERNS[key].search(text)
    return match.group(1) if match else ""


def array_values(text: str, field: str) -> list[str]:
    for match in ARRAY_LINE.finditer(text):
        if match.group(1) == field:
            return STRING_NAME.findall(match.group(0))
    return []


def parse_skill(path: Path) -> tuple[str, dict[str, object]] | None:
    text = path.read_text(encoding="utf-8")
    skill_id = scalar(text, "skill_id")
    if not skill_id:
        return None
    return skill_id, {
        "path": str(path),
        "growth_tier": scalar(text, "growth_tier"),
        "learn_source": scalar(text, "learn_source"),
        "tags": array_values(text, "tags"),
    }


def parse_quest(path: Path, skills: dict[str, dict[str, object]]) -> dict[str, object]:
    text = path.read_text(encoding="utf-8")
    quest_id = scalar(text, "quest_id") or path.stem
    provider_kind = scalar(text, "provider_kind")
    listing_channels = array_values(text, "listing_channels")
    reward_types = REWARD_TYPE.findall(text)
    objective_types = OBJECTIVE_TYPE.findall(text)
    member_ids = MEMBER_ID.findall(text)
    pending_entries = [
        {"entry_type": kind, "target_id": target, "amount": int(amount)}
        for kind, target, amount in PENDING_ENTRY.findall(text)
    ]
    review: list[str] = []

    expected_channel = {
        "npc": "npc_offer",
        "service_contract_board": "contract_board",
        "service_bounty_registry": "bounty_registry",
    }.get(provider_kind)
    if expected_channel and expected_channel not in listing_channels:
        review.append(
            f"provider {provider_kind} does not list expected channel {expected_channel}"
        )
    if not provider_kind:
        review.append("missing provider_kind")

    for entry in pending_entries:
        if entry["entry_type"] not in {"skill_unlock", "skill_mastery"}:
            continue
        target_id = str(entry["target_id"])
        skill = skills.get(target_id)
        if skill is None:
            review.append(f"skill reward target not found: {target_id}")
            continue
        if entry["entry_type"] == "skill_unlock":
            if skill["learn_source"] == "internal":
                review.append(f"skill_unlock targets internal skill: {target_id}")
            if not skill["growth_tier"]:
                review.append(f"skill_unlock target has no growth_tier: {target_id}")

    if "pending_character_reward" in reward_types and not member_ids:
        review.append("pending_character_reward has no member_id")

    return {
        "path": str(path),
        "quest_id": quest_id,
        "provider_kind": provider_kind,
        "provider_interaction_id": scalar(text, "provider_interaction_id"),
        "listing_channels": listing_channels,
        "failure_policy": scalar(text, "failure_policy"),
        "objective_types": objective_types,
        "reward_types": reward_types,
        "member_ids": member_ids,
        "pending_entries": pending_entries,
        "manual_review": review,
    }


def audit(repo_root: Path) -> dict[str, object]:
    quest_dir = repo_root / "data" / "configs" / "quests"
    skill_dir = repo_root / "data" / "configs" / "skills"
    if not quest_dir.is_dir():
        raise ValueError(f"quest directory not found: {quest_dir}")
    if not skill_dir.is_dir():
        raise ValueError(f"skill directory not found: {skill_dir}")

    skills: dict[str, dict[str, object]] = {}
    duplicate_skill_ids: list[str] = []
    for path in sorted(skill_dir.rglob("*.tres")):
        parsed = parse_skill(path)
        if parsed is None:
            continue
        skill_id, facts = parsed
        if skill_id in skills:
            duplicate_skill_ids.append(skill_id)
        skills[skill_id] = facts

    quests = [
        parse_quest(path, skills)
        for path in sorted(quest_dir.rglob("*.tres"))
    ]
    provider_counts = Counter(str(item["provider_kind"]) for item in quests)
    reward_counts = Counter(
        reward for item in quests for reward in item["reward_types"]
    )
    objective_counts = Counter(
        objective for item in quests for objective in item["objective_types"]
    )
    flagged = [item["quest_id"] for item in quests if item["manual_review"]]
    return {
        "repo_root": str(repo_root.resolve()),
        "summary": {
            "quest_count": len(quests),
            "indexed_skill_count": len(skills),
            "duplicate_skill_ids": sorted(set(duplicate_skill_ids)),
            "provider_counts": dict(sorted(provider_counts.items())),
            "objective_type_counts": dict(sorted(objective_counts.items())),
            "reward_type_counts": dict(sorted(reward_counts.items())),
            "manual_review_count": len(flagged),
            "manual_review_quest_ids": flagged,
        },
        "quests": quests,
        "notice": (
            "Static inventory only. Confirm every candidate with QuestContentValidator, "
            "current runtime owners, and focused regressions before editing."
        ),
    }


def run_self_test() -> int:
    skill_text = 'skill_id = &"guard"\ngrowth_tier = &"basic"\nlearn_source = &"normal"\n'
    quest_text = (
        'quest_id = &"q"\nprovider_kind = &"npc"\n'
        'listing_channels = Array[StringName]([&"npc_offer"])\n'
        'reward_entries = Array[Dictionary]([{&"reward_type": &"pending_character_reward", '
        '&"member_id": &"hero", &"entries": Array[Dictionary]([{&"entry_type": '
        '&"skill_unlock", &"target_id": &"guard", &"amount": 1}])}])\n'
    )
    assert scalar(skill_text, "skill_id") == "guard"
    assert array_values(quest_text, "listing_channels") == ["npc_offer"]
    assert REWARD_TYPE.findall(quest_text) == ["pending_character_reward"]
    assert PENDING_ENTRY.findall(quest_text) == [("skill_unlock", "guard", "1")]
    print("self-test: PASS")
    return 0


def parse_args() -> argparse.Namespace:
    default_root = Path(__file__).resolve().parents[4]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=Path, default=default_root)
    parser.add_argument(
        "--summary-only",
        action="store_true",
        help="Print only repository facts and manual-review counts.",
    )
    parser.add_argument("--self-test", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    if args.self_test:
        return run_self_test()
    try:
        result = audit(args.repo_root)
    except (OSError, UnicodeError, ValueError) as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 2
    output = result
    if args.summary_only:
        output = {
            "repo_root": result["repo_root"],
            "summary": result["summary"],
            "notice": result["notice"],
        }
    json.dump(output, sys.stdout, ensure_ascii=False, indent=2)
    print()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
