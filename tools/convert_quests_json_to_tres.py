#!/usr/bin/env python3
"""One-time converter: JSON quest configs -> individual QuestDef .tres files."""
import json
from pathlib import Path

CONFIG_DIR = Path("data/configs/quests")
SCRIPT_PATH = "res://scripts/player/progression/QuestDef.cs"


def escape_string(value: str) -> str:
    return value.replace("\\", "\\\\").replace('"', '\\"')


def to_string_name(value: str) -> str:
    return f'&"{value}"'


def format_array_string_name(items) -> str:
    if not items:
        return "Array[StringName]([])"
    inner = ", ".join(to_string_name(str(x)) for x in items)
    return f"Array[StringName]([{inner}])"


def format_value(value):
    if isinstance(value, str):
        return to_string_name(value)
    if isinstance(value, bool):
        return "true" if value else "false"
    if isinstance(value, int):
        return str(value)
    if isinstance(value, list):
        if value and isinstance(value[0], dict):
            return format_array_dictionary(value)
        return format_array_string_name(value)
    return f'"{escape_string(str(value))}"'


def format_array_dictionary(items) -> str:
    if not items:
        return "Array[Dictionary]([])"
    entries = []
    for d in items:
        if not isinstance(d, dict):
            continue
        pairs = []
        for k, v in d.items():
            key = to_string_name(str(k))
            val = format_value(v)
            pairs.append(f"{key}: {val}")
        entries.append("{" + ", ".join(pairs) + "}")
    return "Array[Dictionary]([" + ", ".join(entries) + "])"


def quest_to_tres(quest: dict) -> str:
    provider_interaction_id = str(quest.get("provider_interaction_id", ""))
    provider_kind = str(quest.get("provider_kind", ""))
    listing_channels = quest.get("listing_channels")

    # Auto-fill new schema fields from provider_interaction_id when absent.
    if not provider_kind:
        provider_kind = {
            "service_bounty_registry": "service_bounty_registry",
        }.get(provider_interaction_id, "service_contract_board")

    if listing_channels is None:
        listing_channels = (
            ["bounty_registry"]
            if provider_interaction_id == "service_bounty_registry"
            else ["contract_board"]
        )

    lines = [
        '[gd_resource type="Resource" script_class="QuestDef" load_steps=2 format=3]',
        '',
        f'[ext_resource type="Script" path="{SCRIPT_PATH}" id="1_questdef"]',
        '',
        '[resource]',
        'script = ExtResource("1_questdef")',
        f'quest_id = {to_string_name(str(quest["quest_id"]))}',
        f'display_name = "{escape_string(str(quest["display_name"]))}"',
        f'description = "{escape_string(str(quest["description"]))}"',
        f'provider_kind = {to_string_name(provider_kind)}',
        f'provider_interaction_id = {to_string_name(provider_interaction_id)}',
        f'listing_channels = {format_array_string_name(listing_channels)}',
        f'tags = {format_array_string_name(quest.get("tags", []))}',
        f'accept_requirements = {format_array_dictionary(quest.get("accept_requirements", []))}',
        f'accept_dialogue_text = "{escape_string(str(quest.get("accept_dialogue_text", "")))}"',
        f'accept_feedback_success = "{escape_string(str(quest.get("accept_feedback_success", "")))}"',
        f'accept_feedback_failure = "{escape_string(str(quest.get("accept_feedback_failure", "")))}"',
        f'accept_confirmation_text = "{escape_string(str(quest.get("accept_confirmation_text", "")))}"',
        f'objective_defs = {format_array_dictionary(quest["objective_defs"])}',
        f'reward_entries = {format_array_dictionary(quest["reward_entries"])}',
        f'is_repeatable = {"true" if quest.get("is_repeatable", False) else "false"}',
    ]
    return "\n".join(lines) + "\n"


def write_quest(quest: dict) -> None:
    out_path = CONFIG_DIR / f'{quest["quest_id"]}.tres'
    out_path.write_text(quest_to_tres(quest), encoding="utf-8")
    print(f"Wrote {out_path}")


def main() -> None:
    CONFIG_DIR.mkdir(parents=True, exist_ok=True)

    for json_file in sorted(CONFIG_DIR.glob("*.json")):
        with open(json_file, "r", encoding="utf-8") as f:
            data = json.load(f)
        for quest in data.get("quests", []):
            write_quest(quest)

    # Seed quests previously hard-coded in ProgressionContentRegistry.
    seed_quests = [
        {
            "quest_id": "contract_manual_drill",
            "display_name": "训练记录",
            "description": "在训练场完成两次记录，用于验证任务命令与状态推进链。",
            "provider_interaction_id": "service_contract_board",
            "tags": [],
            "accept_requirements": [],
            "objective_defs": [
                {
                    "objective_id": "train_once",
                    "objective_type": "settlement_action",
                    "target_id": "service:training",
                    "target_value": 2,
                },
            ],
            "reward_entries": [
                {"reward_type": "gold", "amount": 30},
            ],
            "is_repeatable": False,
        },
        {
            "quest_id": "contract_settlement_warehouse",
            "display_name": "据点仓储巡查",
            "description": "前往据点服务台完成一次仓储交接。",
            "provider_interaction_id": "service_contract_board",
            "tags": [],
            "accept_requirements": [],
            "objective_defs": [
                {
                    "objective_id": "warehouse_visit",
                    "objective_type": "settlement_action",
                    "target_id": "service:warehouse",
                    "target_value": 1,
                },
            ],
            "reward_entries": [
                {"reward_type": "gold", "amount": 60},
            ],
            "is_repeatable": False,
        },
        {
            "quest_id": "contract_first_hunt",
            "display_name": "首轮狩猎",
            "description": "击败任意一组敌对遭遇，证明队伍已具备外出作战能力。",
            "provider_interaction_id": "service_contract_board",
            "tags": [],
            "accept_requirements": [],
            "objective_defs": [
                {
                    "objective_id": "defeat_enemy_once",
                    "objective_type": "defeat_enemy",
                    "target_id": "",
                    "target_value": 1,
                },
            ],
            "reward_entries": [
                {"reward_type": "gold", "amount": 80},
            ],
            "is_repeatable": False,
        },
        {
            "quest_id": "contract_regional_bounty",
            "display_name": "地区悬赏",
            "description": "由悬赏署单独发放的区域通缉，用来验证多 provider 任务板的过滤边界。",
            "provider_interaction_id": "service_bounty_registry",
            "tags": ["contract", "bounty"],
            "accept_requirements": [],
            "objective_defs": [
                {
                    "objective_id": "defeat_enemy_once",
                    "objective_type": "defeat_enemy",
                    "target_id": "",
                    "target_value": 1,
                },
            ],
            "reward_entries": [
                {"reward_type": "gold", "amount": 120},
            ],
            "is_repeatable": False,
        },
    ]

    for quest in seed_quests:
        write_quest(quest)


if __name__ == "__main__":
    main()
