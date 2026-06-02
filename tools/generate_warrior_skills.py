#!/usr/bin/env python3
"""Generate warrior skill .tres files from D&D numerical design."""

import json, os, sys

OUTPUT_DIR = os.path.normpath(r"E:\game\magic\data\configs\skills")
DRY_RUN = "--dry" in sys.argv

CURVES = {
    ("basic", 3):       [100, 250, 550],
    ("basic", 5):       [100, 250, 550, 1000, 1600],
    ("intermediate", 7): [160, 400, 900, 1600, 2600, 3800, 5300],
    ("advanced", 9):    [240, 600, 1320, 2400, 3840, 5640, 7800, 10320, 13200],
    ("ultimate", 10):   [360, 900, 1980, 3600, 5760, 8600, 12000, 16000, 21000, 26900],
}

# --- Attribute presets (totals match budget: B=60, I=120, A=180, U=240) ---
# Keys: S=strength, A=agility, C=constitution, W=willpower
ATTR = {
    "melee_B":  {"strength": 40, "constitution": 20},
    "melee_I":  {"strength": 70, "constitution": 50},
    "melee_A":  {"strength": 100, "constitution": 80},
    "melee_U":  {"strength": 150, "constitution": 90},
    "tank_B":   {"constitution": 40, "willpower": 20},
    "tank_I":   {"constitution": 80, "willpower": 40},
    "tank_A":   {"constitution": 120, "willpower": 60},
    "tank_U":   {"constitution": 150, "willpower": 90},
    "qi_B":     {"strength": 20, "willpower": 40},
    "qi_I":     {"strength": 50, "willpower": 70},
    "qi_A":     {"strength": 70, "willpower": 110},
    "qi_U":     {"strength": 100, "willpower": 140},
    "cmd_B":    {"willpower": 40, "strength": 20},
    "cmd_I":    {"willpower": 80, "strength": 40},
    "cmd_A":    {"willpower": 120, "strength": 60},
    "cmd_U":    {"willpower": 160, "strength": 80},
    "agi_B":    {"agility": 40, "strength": 20},
    "agi_I":    {"agility": 70, "strength": 50},
    "agi_A":    {"agility": 110, "strength": 70},
    "agi_U":    {"agility": 140, "strength": 100},
    "ctl_B":    {"strength": 30, "willpower": 30},
    "ctl_I":    {"strength": 60, "willpower": 60},
    "ctl_A":    {"strength": 90, "willpower": 90},
    "ctl_U":    {"strength": 120, "willpower": 120},
    "blood_B":  {"strength": 20, "constitution": 40},
    "blood_I":  {"strength": 40, "constitution": 50, "willpower": 30},
    "blood_A":  {"strength": 60, "constitution": 70, "willpower": 50},
    "blood_U":  {"strength": 100, "constitution": 80, "willpower": 60},
    "mix_SA_B": {"strength": 30, "agility": 30},
    "mix_SW_B": {"strength": 30, "willpower": 30},
    "mix_CW_B": {"constitution": 30, "willpower": 30},
    "mix_SA_I": {"strength": 60, "agility": 60},
    "mix_SW_I": {"strength": 60, "willpower": 60},
    "mix_CW_I": {"constitution": 60, "willpower": 60},
    "mix_SC_I": {"strength": 60, "constitution": 60},
    "mix_SA_A": {"strength": 90, "agility": 90},
    "mix_SW_A": {"strength": 90, "willpower": 90},
}


def fmt_curve(tier, lv):
    arr = CURVES[(tier, lv)]
    return "PackedInt32Array(" + ", ".join(str(x) for x in arr) + ")"


def fmt_dict(d, indent=0):
    """Format a dict for .tres output, with proper nesting."""
    if not d:
        return "{}"
    prefix = " " * indent
    inner = " " * (indent + 4)
    items = []
    for k, v in d.items():
        if isinstance(v, bool):
            items.append(f'{inner}"{k}": {"true" if v else "false"}')
        elif isinstance(v, int):
            items.append(f'{inner}"{k}": {v}')
        elif isinstance(v, float):
            items.append(f'{inner}"{k}": {v}')
        elif isinstance(v, dict):
            items.append(f'{inner}"{k}": {fmt_dict(v, indent + 4)}')
        elif isinstance(v, str) and v.startswith("&"):
            items.append(f'{inner}"{k}": {v}')
        else:
            items.append(f'{inner}"{k}": {v}')
    return "{\n" + ",\n".join(items) + "\n" + prefix + "}"


def fmt_level_overrides(data):
    """Convert Python dict to Godot dict format with int keys."""
    if not data:
        return "{}"
    items = []
    for lv, overrides in sorted(data.items()):
        inner = []
        for k, v in overrides.items():
            if isinstance(v, bool):
                inner.append(f'"{k}": {"true" if v else "false"}')
            elif isinstance(v, int):
                inner.append(f'"{k}": {v}')
            elif isinstance(v, str) and v.startswith("&"):
                inner.append(f'"{k}": {v}')
            else:
                inner.append(f'"{k}": {v}')
        items.append(f'{lv}: {{\n\t\t{", ".join(inner)}\n\t}}')
    return "{\n\t" + ",\n\t".join(items) + "\n}"


def fmt_tags(tags):
    """Format tags array. Always includes warrior and melee."""
    all_tags = ['&"warrior"', '&"melee"']
    for t in tags:
        if t not in ("warrior", "melee"):
            all_tags.append(f'&"{t}"')
    return "Array[StringName]([" + ", ".join(all_tags) + "])"


def build_tres(skill):
    """Build complete .tres file content from skill definition."""
    sid = skill["skill_id"]
    lines = []

    # Header
    lines.append('[gd_resource type="Resource" script_class="SkillDef" format=3]')
    lines.append("")
    lines.append('[ext_resource type="Script" path="res://scripts/player/progression/combat_effect_def.gd" id="3_effect"]')
    lines.append('[ext_resource type="Script" path="res://scripts/player/progression/combat_skill_def.gd" id="4_combat"]')
    lines.append('[ext_resource type="Script" path="res://scripts/player/progression/skill_def.gd" id="5_skill"]')
    lines.append("")

    # Effect sub-resources
    effect_refs = []
    for i, ef in enumerate(skill.get("effects", [])):
        rid = f"Resource_ef{i}"
        effect_refs.append(f'SubResource("{rid}")')
        lines.append(f'[sub_resource type="Resource" id="{rid}"]')
        lines.append('script = ExtResource("3_effect")')
        lines.append(f'effect_type = &"{ef["effect_type"]}"')
        if ef.get("consumed_status_id"):
            lines.append(f'consumed_status_id = &"{ef["consumed_status_id"]}"')
        if ef.get("dice_per_consumed_stack"):
            lines.append(f'dice_per_consumed_stack = {ef["dice_per_consumed_stack"]}')
        if ef.get("dice_sides_per_stack"):
            lines.append(f'dice_sides_per_stack = {ef["dice_sides_per_stack"]}')
        if ef.get("power"):
            lines.append(f'power = {ef["power"]}')
        if "min_level" in ef:
            lines.append(f'min_skill_level = {ef["min_level"]}')
        if "max_level" in ef:
            lines.append(f'max_skill_level = {ef["max_level"]}')
        if ef.get("damage_tag"):
            lines.append(f'damage_tag = &"{ef["damage_tag"]}"')
        if ef.get("status_id"):
            lines.append(f'status_id = &"{ef["status_id"]}"')
        if ef.get("duration_tu"):
            lines.append(f'duration_tu = {ef["duration_tu"]}')
        if ef.get("trigger_event"):
            lines.append(f'trigger_event = &"{ef["trigger_event"]}"')
        if ef.get("target_filter"):
            lines.append(f'effect_target_team_filter = &"{ef["target_filter"]}"')
        if ef.get("trigger_condition"):
            lines.append(f'trigger_condition = &"{ef["trigger_condition"]}"')
        if "trigger_status_id" in ef:
            lines.append(f'trigger_status_id = &"{ef["trigger_status_id"]}"')
        if ef.get("save_ability"):
            lines.append(f'save_ability = &"{ef["save_ability"]}"')
        if ef.get("save_dc"):
            lines.append(f'save_dc = {ef["save_dc"]}')
        if ef.get("save_dc_mode"):
            lines.append(f'save_dc_mode = &"{ef["save_dc_mode"]}"')
        if ef.get("save_failure_status_id"):
            lines.append(f'save_failure_status_id = &"{ef["save_failure_status_id"]}"')
        if ef.get("save_partial_on_success"):
            lines.append(f'save_partial_on_success = true')
        if ef.get("save_tag"):
            lines.append(f'save_tag = &"{ef["save_tag"]}"')
        if ef.get("damage_ratio_percent"):
            lines.append(f'damage_ratio_percent = {ef["damage_ratio_percent"]}')
        if ef.get("fallback_damage_ratio_percent"):
            lines.append(f'fallback_damage_ratio_percent = {ef["fallback_damage_ratio_percent"]}')
        if ef.get("bonus_condition"):
            lines.append(f'bonus_condition = &"{ef["bonus_condition"]}"')
        if ef.get("stack_behavior"):
            lines.append(f'stack_behavior = &"{ef["stack_behavior"]}"')
        if ef.get("stack_limit"):
            lines.append(f'stack_limit = {ef["stack_limit"]}')
        if ef.get("forced_move_mode"):
            lines.append(f'forced_move_mode = &"{ef["forced_move_mode"]}"')
        if ef.get("forced_move_distance"):
            lines.append(f'forced_move_distance = {ef["forced_move_distance"]}')
        if ef.get("terrain_effect_id"):
            lines.append(f'terrain_effect_id = &"{ef["terrain_effect_id"]}"')
        if ef.get("body_size_category"):
            lines.append(f'body_size_category = &"{ef["body_size_category"]}')
        if ef.get("tick_effect_type"):
            lines.append(f'tick_effect_type = &"{ef["tick_effect_type"]}"')
        if ef.get("tick_interval_tu"):
            lines.append(f'tick_interval_tu = {ef["tick_interval_tu"]}')
        if ef.get("height_delta"):
            lines.append(f'height_delta = {ef["height_delta"]}')
        if ef.get("params"):
            lines.append(f'params = {fmt_dict(ef["params"])}')
        lines.append("")

    # Combat sub-resource
    lines.append('[sub_resource type="Resource" id="Resource_combat"]')
    lines.append('script = ExtResource("4_combat")')
    lines.append(f'skill_id = &"{sid}"')

    if skill.get("target_mode"):
        lines.append(f'target_mode = &"{skill["target_mode"]}"')
    if skill.get("target_filter"):
        lines.append(f'target_team_filter = &"{skill["target_filter"]}"')
    lines.append(f'range_value = {skill.get("range_value", 0)}')
    if skill.get("area_pattern"):
        lines.append(f'area_pattern = &"{skill["area_pattern"]}"')
    if skill.get("area_value"):
        lines.append(f'area_value = {skill["area_value"]}')
    if skill.get("requires_los"):
        lines.append(f'requires_los = true')
    lines.append(f'ap_cost = {skill.get("ap_cost", 1)}')
    if skill.get("mp_cost"):
        lines.append(f'mp_cost = {skill["mp_cost"]}')
    if "stamina_cost" in skill:
        lines.append(f'stamina_cost = {skill["stamina_cost"]}')
    if skill.get("aura_cost"):
        lines.append(f'aura_cost = {skill["aura_cost"]}')
    if skill.get("cooldown_tu"):
        lines.append(f'cooldown_tu = {skill["cooldown_tu"]}')
    if skill.get("attack_roll_bonus") is not None:
        lines.append(f'attack_roll_bonus = {skill["attack_roll_bonus"]}')
    if skill.get("level_overrides"):
        lines.append(f'level_overrides = {fmt_level_overrides(skill["level_overrides"])}')
    lines.append(f'mastery_trigger_mode = &"{skill.get("mastery_trigger", "skill_damage_dice_max")}"')
    lines.append(f'mastery_amount_mode = &"{skill.get("mastery_amount", "per_target_rank")}"')
    if skill.get("mastery_low_hp_mult"):
        lines.append(f'mastery_low_hp_bonus_multiplier = {skill["mastery_low_hp_mult"]}')
    if skill.get("mastery_low_hp_pct"):
        lines.append(f'mastery_low_hp_threshold_percent = {skill["mastery_low_hp_pct"]}')
    if skill.get("min_targets"):
        lines.append(f'min_target_count = {skill["min_targets"]}')
    if skill.get("max_targets"):
        lines.append(f'max_target_count = {skill["max_targets"]}')
    if skill.get("target_sel"):
        lines.append(f'target_selection_mode = &"{skill["target_sel"]}"')
    if skill.get("selection_order"):
        lines.append(f'selection_order_mode = &"{skill["selection_order"]}"')
    if skill.get("requires_shield"):
        lines.append('requires_equipped_shield = true')
    if skill.get("required_weapons"):
        rw = ", ".join(f'&"{w}"' for w in skill["required_weapons"])
        lines.append(f'required_weapon_families = Array[StringName]([{rw}])')
    if skill.get("excluded_weapons"):
        ew = ", ".join(f'&"{w}"' for w in skill["excluded_weapons"])
        lines.append(f'excluded_weapon_families = Array[StringName]([{ew}])')
    if skill.get("spell_fate"):
        lines.append(f'spell_fate_mode = &"{skill["spell_fate"]}"')
    if skill.get("spell_crit"):
        lines.append(f'spell_critical_mode = &"{skill["spell_crit"]}"')
    if skill.get("crit_refund"):
        lines.append(f'spell_critical_mp_refund_percent = {skill["crit_refund"]}')
    if skill.get("backlash"):
        lines.append(f'backlash_mode = &"{skill["backlash"]}"')
    if skill.get("area_origin_mode"):
        lines.append(f'area_origin_mode = &"{skill["area_origin_mode"]}"')
    if skill.get("area_direction_mode"):
        lines.append(f'area_direction_mode = &"{skill["area_direction_mode"]}"')

    eff_arr = "Array[ExtResource(\"3_effect\")]([" + ", ".join(effect_refs) + "])"
    if skill.get("passive"):
        lines.append(f'passive_effect_defs = {eff_arr}')
    else:
        lines.append(f'effect_defs = {eff_arr}')
    lines.append("")

    # Resource block
    lines.append("[resource]")
    lines.append('script = ExtResource("5_skill")')
    lines.append(f'skill_id = &"{sid}"')
    lines.append(f'display_name = "{skill["display_name"]}"')
    if skill.get("icon_id"):
        lines.append(f'icon_id = &"{skill["icon_id"]}"')
    if skill.get("description"):
        lines.append(f'description = "{skill["description"]}"')
    if skill.get("skill_type"):
        lines.append(f'skill_type = &"{skill["skill_type"]}"')
    if skill.get("level_desc_template"):
        lines.append(f'level_description_template = "{skill["level_desc_template"]}"')
    if skill.get("level_desc_configs"):
        lines.append(f'level_description_configs = {json.dumps(skill["level_desc_configs"], ensure_ascii=False)}')
    lines.append(f'max_level = {skill["max_level"]}')
    lines.append(f'non_core_max_level = {skill["non_core_max"]}')
    lines.append(f'mastery_curve = {fmt_curve(skill["tier"], skill["max_level"])}')
    lines.append(f'growth_tier = &"{skill["tier"]}"')
    ag = skill["attr_growth"]
    ag_items = ", ".join(f'"{k}": {v}' for k, v in sorted(ag.items()))
    lines.append(f'attribute_growth_progress = {{ {ag_items} }}')
    if skill.get("attr_reqs"):
        ar = ", ".join(f'"{k}": {v}' for k, v in sorted(skill["attr_reqs"].items()))
        lines.append(f'attribute_requirements = {{ {ar} }}')
    if skill.get("skill_reqs"):
        sr = ", ".join(f'"{k}": {v}' for k, v in sorted(skill["skill_reqs"].items()))
        lines.append(f'skill_level_requirements = {{ {sr} }}')
    if skill.get("mastery_sources"):
        ms = ", ".join(f'&"{s}"' for s in skill["mastery_sources"])
        lines.append(f'mastery_sources = Array[StringName]([{ms}])')
    lines.append(f'tags = {fmt_tags(skill.get("tags", []))}')
    lines.append('combat_profile = SubResource("Resource_combat")')
    lines.append("")

    return "\n".join(lines)


# ============================================================
# Skill Data - defined compactly
# ============================================================

def W(dice_sides, tiers=None, tag="physical_slash"):
    """Weapon-dice damage effect generator. Auto-grants combo_stack on hit."""
    if tiers is None:
        tiers = [(1, 4, 0, 2), (1, 6, 3, -1)]
    effects = []
    for dc, ds, lo, hi in tiers:
        effects.append({
            "effect_type": "damage", "min_level": lo, "max_level": hi,
            "damage_tag": tag,
            "params": {"add_weapon_dice": True, "requires_weapon": True,
                       "use_weapon_physical_damage_tag": True,
                       "dice_count": dc, "dice_sides": ds,
                       "grant_status_id": "combo_stack",
                       "grant_status_power": 1,
                       "grant_status_duration_tu": 180,
                       "grant_status_stack_limit": 20}
        })
    return effects

def Wn(dice_sides, tiers=None, tag="physical_slash"):
    """Non-combo weapon dice damage (no grant_status on hit)."""
    if tiers is None:
        tiers = [(1, 4, 0, 2), (1, 6, 3, -1)]
    effects = []
    for dc, ds, lo, hi in tiers:
        effects.append({
            "effect_type": "damage", "min_level": lo, "max_level": hi,
            "damage_tag": tag,
            "params": {"add_weapon_dice": True, "requires_weapon": True,
                       "use_weapon_physical_damage_tag": True,
                       "dice_count": dc, "dice_sides": ds}
        })
    return effects

def PW(power, dice_c=1, dice_s=6, tag="force", lo=0, hi=-1):
    """Non-weapon damage (flat power + dice)."""
    return [{"effect_type": "damage", "power": power, "min_level": lo, "max_level": hi,
             "damage_tag": tag, "params": {"dice_count": dice_c, "dice_sides": dice_s,
             "add_weapon_dice": False}}]

def ST(status_id, dur=40, power=1, lo=0, hi=-1, trigger="", tfilt="", **kw):
    """Status effect."""
    e = {"effect_type": "status", "status_id": status_id, "min_level": lo,
         "max_level": hi, "duration_tu": dur}
    if power > 1: e["power"] = power
    if trigger: e["trigger_event"] = trigger
    if tfilt: e["target_filter"] = tfilt
    e.update(kw)
    return e

def skill(sid, name, lv, nc, tier, stam, cd, atk, effects, attr_key, tags,
          rng=1, area_p=None, area_v=0, aura=0, desc="", **extra):
    """Define a skill."""
    return {
        "skill_id": f"warrior_{sid}", "display_name": name,
        "max_level": lv, "non_core_max": nc, "tier": tier,
        "stamina_cost": stam, "cooldown_tu": cd,
        "attack_roll_bonus": atk, "effects": effects,
        "attr_growth": ATTR[attr_key], "tags": tags,
        "range_value": rng, "area_pattern": area_p, "area_value": area_v,
        "aura_cost": aura, "description": desc, **extra
    }


# ===== ALL SKILL DATA =====
ALL_SKILLS = []

def add(*args, **kw):
    ALL_SKILLS.append(skill(*args, **kw))

# ==================== 旧版钢心流派 Iron Heart ====================
add("double_strike", "双重打击", 7, 5, "intermediate", 25, 0, 0,
    W(6, [(1,4,0,3),(1,6,4,-1)]), "melee_I", ["output"], 1,
    desc="连续两次近战攻击，每段独立命中检定。", level_overrides={1: {"attack_roll_bonus": 1}, 5: {"stamina_cost": 20}})

add("overhead_chop", "下劈", 7, 5, "intermediate", 30, 10, 1,
    W(6, [(1,6,0,3),(1,8,4,-1)]), "melee_I", ["output"], 1,
    desc="重劈目标，受控目标额外受伤。",
    level_overrides={4: {"stamina_cost": 24}})

add("armor_piercing_strike", "破军斩", 9, 7, "advanced", 32, 20, 0,
    W(6, [(1,6,0,3),(1,8,4,6),(2,6,7,-1)]), "melee_A", ["output","breaker"], 1,
    desc="无视目标部分防御。", level_overrides={3: {"attack_roll_bonus": 1}, 7: {"stamina_cost": 25}})

add("iron_charge", "铁心突击", 5, 3, "basic", 32, 20, 0,
    W(6, [(1,6,0,2),(1,8,3,-1)]), "mix_SA_B", ["mobility"], 2,
    desc="冲向目标邻格攻击，路径无阻伤害提升。", level_overrides={2: {"stamina_cost": 28}, 4: {"stamina_cost": 25}})

add("blade_wall_stance", "刃墙架势", 5, 3, "basic", 25, 80, 0,
    [ST("guarding", 60)], "tank_B", ["defense"], 0,
    desc="防御架势，正面减伤并可在首次被攻时反击。", level_overrides={3: {"stamina_cost": 18}})

add("counter_slash", "反击斩", 5, 3, "basic", 18, 20, 0,
    W(6, [(1,4,0,2),(1,6,3,-1)]), "mix_SA_B", ["defense"], 1,
    desc="本回合首次被近战攻击后自动反击。", level_overrides={3: {"stamina_cost": 12}})

add("weapon_disarm", "武器拆解", 7, 5, "intermediate", 28, 80, -1,
    W(6, [(1,4,0,3),(1,6,4,-1)]) + [ST("hex_of_frailty", 40, 2)],
    "ctl_I", ["control"], 1,
    desc="攻击目标武器使其暂时缴械。", level_overrides={3: {"stamina_cost": 22}})

add("steady_pressure", "稳步压迫", 5, 3, "basic", 22, 80, 0,
    [], "melee_B", ["mobility"], 0,
    desc="2回合移动后仍可攻击。", level_overrides={3: {"stamina_cost": 16}})

add("iron_torrent_slash", "铁流斩", 7, 5, "intermediate", 38, 20, -1,
    W(6, [(1,4,0,3),(1,6,4,-1)]), "melee_I", ["aoe"], 1, "line", 2,
    desc="对前方横排两格敌人两段横斩。", level_overrides={4: {"stamina_cost": 30}})

add("fearless_advance", "无畏前压", 3, 3, "basic", 20, 80, 0,
    [], "tank_B", ["mobility"], 0,
    desc="2回合进入威胁区不触发借机反击。", level_overrides={2: {"stamina_cost": 14}})

# 020 万刃归一 — 消耗 combo_stack 的终式
ALL_SKILLS.append({
    "skill_id": "warrior_myriad_blades_unity", "display_name": "万刃归一",
    "max_level": 10, "non_core_max": 9, "tier": "ultimate",
    "stamina_cost": 0, "cooldown_tu": 120,
    "attack_roll_bonus": 0, "effects": [
        {"effect_type": "damage", "min_level": 0, "max_level": 5,
         "damage_tag": "physical_slash",
         "consumed_status_id": "combo_stack",
         "dice_per_consumed_stack": 1, "dice_sides_per_stack": 6,
         "params": {"add_weapon_dice": True, "requires_weapon": True,
                    "use_weapon_physical_damage_tag": True,
                    "dice_count": 2, "dice_sides": 8}},
        {"effect_type": "damage", "min_level": 6, "max_level": -1,
         "damage_tag": "physical_slash",
         "consumed_status_id": "combo_stack",
         "dice_per_consumed_stack": 1, "dice_sides_per_stack": 6,
         "params": {"add_weapon_dice": True, "requires_weapon": True,
                    "use_weapon_physical_damage_tag": True,
                    "dice_count": 3, "dice_sides": 8}},
    ],
    "attr_growth": ATTR["melee_U"], "tags": ["finisher"],
    "range_value": 1, "aura_cost": 60,
    "description": "消耗全部连击层数，每层追加1d6伤害的终极一击。流派终式。",
    "level_overrides": {8: {"attack_roll_bonus": 2}},
})

# ==================== 明镜心流派 Diamond Mind ====================
add("moment_strike", "瞬息一击", 7, 5, "intermediate", 28, 20, 2,
    W(6, [(1,8,0,3),(2,6,4,-1)]), "melee_I", ["output"], 1,
    desc="未移动则伤害大幅提升。", level_overrides={3: {"stamina_cost": 22}})

add("calm_step", "静心步", 3, 3, "basic", 16, 20, 0,
    [], "agi_B", ["mobility"], 0,
    desc="移动1格并清除减速/锁足。", level_overrides={1: {"stamina_cost": 13}, 2: {"stamina_cost": 10}})

add("mind_severing_slash", "断念斩", 7, 5, "intermediate", 28, 80, 1,
    W(6, [(1,4,0,3),(1,8,4,-1)]), "mix_SW_I", ["control"], 1,
    desc="命中后使目标下次技能失败。", level_overrides={3: {"stamina_cost": 22}})

add("precision_combo", "精准连击", 5, 3, "basic", 20, 80, 0,
    [], "agi_B", ["combo"], 0,
    desc="本回合追击命中惩罚大幅降低。", level_overrides={3: {"stamina_cost": 14}})

add("predictive_block", "预判格挡", 3, 3, "basic", 20, 40, 0,
    [], "tank_B", ["defense"], 0,
    desc="指定方向降低伤害。", level_overrides={2: {"stamina_cost": 14}})

add("intimidating_gaze", "破胆凝视", 5, 3, "basic", 0, 80, 0,
    [ST("hex_of_frailty", 40, 1, save_ability="willpower", save_dc=12,
        save_failure_status_id="hex_of_frailty", tfilt="enemy")],
    "ctl_B", ["control"], 2, aura=15,
    desc="斗气凝视降低目标命中率。", level_overrides={3: {"aura_cost": 12}})

add("mirror_stab", "明镜刺", 7, 5, "intermediate", 22, 10, 3,
    W(6, [(1,4,0,3),(1,6,4,-1)]), "mix_SA_I", ["output"], 1,
    desc="忽略目标闪避增益的精准刺击。", level_overrides={5: {"attack_roll_bonus": 4}, 3: {"stamina_cost": 16}})

add("flaw_read", "破绽读取", 5, 3, "basic", 22, 40, 0,
    [ST("hex_of_frailty", 60, 1, tfilt="enemy")], "mix_SW_B", ["debuff"], 3,
    desc="降低目标闪避率。", level_overrides={3: {"stamina_cost": 16}, 4: {"power": 2, "status_id": "hex_of_frailty"}})

add("mind_eye_counter", "心眼反制", 5, 3, "basic", 22, 80, 0,
    [], "tank_B", ["defense"], 0,
    desc="本回合受远程攻击时命中检定降低。", level_overrides={3: {"stamina_cost": 16}})

add("mirror_water", "明镜止水", 7, 5, "intermediate", 0, 80, 0,
    [], "qi_I", ["defense"], 0, aura=25,
    desc="清除负面状态并免疫控制。", level_overrides={3: {"aura_cost": 20}})

add("weak_point_mark", "弱点标记", 3, 3, "basic", 22, 60, 0,
    [], "mix_SW_B", ["mark"], 3,
    desc="目标被暴击率提升。", level_overrides={2: {"stamina_cost": 16}})

add("one_inch_advantage", "一寸先机", 5, 3, "basic", 16, 80, 0,
    [], "agi_B", ["mobility"], 0,
    desc="下回合行动顺序提前。", level_overrides={3: {"stamina_cost": 10}})

add("stance_read", "看破架势", 5, 3, "basic", 18, 80, 0,
    [], "mix_SW_B", ["buff"], 0,
    desc="2回合对隐身目标命中提升。", level_overrides={3: {"stamina_cost": 12}})

add("will_penetrate", "意志穿透", 7, 5, "intermediate", 0, 40, 1,
    W(6, [(1,8,0,3),(2,6,4,-1)]) + [
        {"effect_type": "damage", "min_level": 0, "max_level": -1,
         "damage_tag": "force", "damage_ratio_percent": 140,
         "bonus_condition": "target_has_shield",
         "params": {"dice_count": 1, "dice_sides": 8}}
    ], "qi_I", ["breaker"], 1, aura=22,
    desc="对护盾目标额外造成伤害。", level_overrides={3: {"aura_cost": 18}})

add("moment_counter", "刹那反击", 7, 5, "intermediate", 22, 80, 2,
    W(6, [(1,4,0,3),(1,6,4,-1)]), "mix_SA_I", ["defense"], 1,
    desc="闪避近战后自动反击。", level_overrides={3: {"stamina_cost": 16}})

add("focus_ray_slash", "凝神射线斩", 7, 5, "intermediate", 0, 40, 1,
    PW(10, 1, 8), "qi_I", ["ranged"], 4, "line", 4, 25,
    desc="不受掩体轻微遮挡的斗气远程攻击。", level_overrides={3: {"aura_cost": 20}})

add("fate_toss", "命运一掷", 9, 7, "advanced", 0, 80, -2,
    PW(22, 1, 10), "ctl_A", ["finisher"], 1, aura=35,
    desc="高风险高回报，未命中返还一半斗气。", level_overrides={5: {"aura_cost": 28}})

add("perfect_rhythm", "完美节奏", 7, 5, "intermediate", 0, 120, 0,
    [], "qi_I", ["combo"], 0, aura=35,
    desc="2回合每次命中叠加连击层。", level_overrides={3: {"aura_cost": 28}})

add("heart_sword_pure", "心剑无尘", 9, 7, "advanced", 0, 120, 4,
    PW(18, 2, 8), "qi_A", ["finisher"], 1, aura=60,
    desc="无视闪避增益与反击，清除目标1增益。", level_overrides={5: {"aura_cost": 50}})

# ==================== 白鸦军略 White Raven ====================
add("war_banner", "战旗", 7, 5, "intermediate", 30, 100, 0,
    [ST("attack_roll_bonus_up", 60, 1, 0, -1, tfilt="ally"),
     ST("damage_reduction_up", 60, 1, 0, -1, tfilt="ally")],
    "cmd_I", ["support","team"], 0, "radius", 2,
    desc="提升周围友军攻防。", level_overrides={3: {"stamina_cost": 24}})

add("tactical_order", "战术指令", 3, 3, "basic", 16, 40, 0,
    [], "cmd_B", ["buff","team"], 3,
    desc="强化指定友军下次攻击的命中与伤害。", level_overrides={2: {"stamina_cost": 10}})

add("flank_order", "夹击号令", 5, 3, "basic", 22, 60, 0,
    [], "cmd_B", ["buff","team"], 3,
    desc="夹击时伤害提升25%。", level_overrides={3: {"stamina_cost": 16}})

add("hold_line_order", "守线命令", 5, 3, "basic", 22, 60, 0,
    [ST("damage_reduction_up", 60, 1, tfilt="ally")],
    "cmd_B", ["defense","team"], 0, "radius", 2,
    desc="周围友军减伤并免疫击退。", level_overrides={3: {"stamina_cost": 16}})

add("rotate_position", "轮换阵位", 3, 3, "basic", 22, 40, 0,
    [], "agi_B", ["mobility","team"], 2,
    desc="与目标友军交换位置。", level_overrides={2: {"stamina_cost": 16}})

add("charge_horn", "冲锋号角", 7, 5, "intermediate", 28, 100, 0,
    [], "cmd_I", ["buff","team"], 0, "radius", 3,
    desc="友军下次移动+1，近战伤害提升。", level_overrides={3: {"stamina_cost": 22}})

add("retreat_cover", "撤退掩护", 5, 3, "basic", 24, 80, 0,
    [], "cmd_B", ["defense","team"], 0, "radius", 2,
    desc="友军本回合脱离不触发反击。", level_overrides={3: {"stamina_cost": 18}})

add("hold_step", "压阵步", 3, 3, "basic", 20, 40, 0,
    [], "mix_CW_B", ["defense","team"], 0,
    desc="相邻友军防御提升，自身命中提升。", level_overrides={2: {"stamina_cost": 14}})

add("courage_resonance", "勇气共鸣", 7, 5, "intermediate", 0, 100, 0,
    [ST("attack_roll_bonus_up", 60, 1, tfilt="ally")],
    "cmd_I", ["support","team"], 0, "radius", 2, 25,
    desc="清除友军恐惧并提升攻击。", level_overrides={3: {"aura_cost": 20}})

add("vanguard_designate", "先锋指定", 3, 3, "basic", 18, 60, 0,
    [], "mix_CW_B", ["buff","team"], 3,
    desc="目标进入威胁区时大幅减伤。", level_overrides={2: {"stamina_cost": 12}})

add("line_advance", "战线推进", 7, 5, "intermediate", 28, 120, 0,
    [], "cmd_I", ["mobility","team"], 0, "radius", 2,
    desc="最多2名友军向前移动1格。", level_overrides={3: {"stamina_cost": 22}})

add("guard_order", "护卫命令", 7, 5, "intermediate", 22, 80, 0,
    [], "mix_CW_I", ["defense","team"], 3,
    desc="最近战士替指定友军分担伤害。", level_overrides={3: {"stamina_cost": 16}})

add("break_formation_order", "破阵号令", 7, 5, "intermediate", 28, 100, 0,
    [], "cmd_I", ["buff","team"], 0, "radius", 2,
    desc="友军对护盾目标伤害提升。", level_overrides={3: {"stamina_cost": 22}})

add("counter_flag", "反攻旗帜", 9, 7, "advanced", 0, 120, 0,
    [], "cmd_A", ["support","team"], 1, "radius", 2, 28,
    desc="旗帜周围友军被攻击后下次伤害提升。", level_overrides={5: {"aura_cost": 22}})

add("first_ascend_order", "先登令", 5, 3, "basic", 24, 60, 0,
    [], "cmd_B", ["buff","team"], 3,
    desc="击杀后返还1格移动。", level_overrides={3: {"stamina_cost": 16}})

add("encircle_tactic", "合围战法", 7, 5, "intermediate", 24, 100, 0,
    [], "ctl_I", ["control","team"], 3,
    desc="相邻2友军时目标移动归零。", level_overrides={3: {"stamina_cost": 18}})

add("white_raven_decree", "白鸦大令", 9, 7, "advanced", 0, 200, 0,
    [], "cmd_A", ["support","team"], 0, "radius", 3, 60,
    desc="友军首技能CD-1，命中提升。", level_overrides={5: {"aura_cost": 50}})

add("hundred_weapons_chorus", "百兵齐鸣", 10, 9, "ultimate", 0, 200, 0,
    W(6, [(1,4,0,5),(1,6,6,-1)]),
    "cmd_U", ["finisher","team"], 0, "radius", 3, 80,
    desc="3-4名友军协同攻击同一目标。", level_overrides={5: {"aura_cost": 65}})

# ==================== 誓魂守护 Devoted Spirit ====================
add("shield_wall_aoe", "护盾墙", 7, 5, "intermediate", 35, 100, 0,
    [ST("damage_reduction_up", 60, 2, tfilt="ally")],
    "tank_I", ["defense","team"], 0, "radius", 1,
    desc="周围友军大幅减伤。", level_overrides={3: {"stamina_cost": 25}})

add("combat_recovery_heal", "战斗回复", 7, 5, "intermediate", 28, 100, 0,
    [{"effect_type": "heal", "min_level": 0, "max_level": -1, "power": 2,
      "params": {"dice_count": 1, "dice_sides": 8, "con_mod_heal": True}}],
    "tank_I", ["recovery"], 0,
    desc="恢复自身HP。", level_overrides={3: {"stamina_cost": 18}})

add("iron_will_immune", "坚定意志", 7, 5, "intermediate", 22, 120, 0,
    [], "tank_I", ["defense"], 0,
    desc="长时间免疫控制效果。", level_overrides={3: {"stamina_cost": 16}})

add("guardian_ring", "守护之环", 7, 5, "intermediate", 28, 100, 0,
    [], "tank_I", ["defense","team"], 0, "radius", 1,
    desc="替周围友军分担伤害。", level_overrides={3: {"stamina_cost": 22}})

add("holy_oath_strike", "圣誓打击", 7, 5, "intermediate", 22, 20, 0,
    W(6, [(1,4,0,3),(1,6,4,-1)], "radiant"),
    "mix_SC_I", ["support","team"], 1,
    desc="命中治疗最低血量友军。", level_overrides={3: {"stamina_cost": 16}})

add("oath_chain", "誓约锁链", 7, 5, "intermediate", 28, 80, 0,
    [], "mix_CW_I", ["control","defense"], 2,
    desc="目标攻击他人时受到反伤。", level_overrides={3: {"stamina_cost": 22}})

add("hold_the_line", "坚守阵线", 5, 3, "basic", 28, 100, 0,
    [], "tank_B", ["terrain","defense"], 0, "line", 3,
    desc="前方一排成为困难地形，穿越需额外移动。", level_overrides={3: {"stamina_cost": 22}})

add("endurance_stance", "抗压姿态", 5, 3, "basic", 22, 80, 0,
    [], "tank_B", ["buff"], 0,
    desc="每次受击攻击力提升。", level_overrides={3: {"stamina_cost": 16}})

add("oath_body_guard", "誓魂护体", 7, 5, "intermediate", 0, 80, 0,
    [ST("damage_reduction_up", 60, 1)],
    "mix_CW_I", ["defense"], 0, aura=25,
    desc="减伤并反弹部分伤害。", level_overrides={3: {"aura_cost": 20}})

add("purify_roar", "净化怒吼", 7, 5, "intermediate", 0, 100, 0,
    [ST("attack_roll_bonus_up", 60, 1, tfilt="ally")],
    "mix_CW_I", ["support","team"], 0, "radius", 2, 28,
    desc="清除友军减益并提升命中。", level_overrides={3: {"aura_cost": 22}})

add("revenge_oath", "复仇宣言", 3, 3, "basic", 20, 60, 0,
    [], "mix_CW_B", ["mark"], 3,
    desc="标记目标，伤及友军后受到额外伤害。", level_overrides={2: {"stamina_cost": 14}})

add("iron_wall_oath", "铁壁誓言", 9, 7, "advanced", 28, 100, 0,
    [ST("damage_reduction_up", 60, 2)],
    "tank_A", ["defense"], 0,
    desc="不能移动但大幅减伤并免疫击退。", level_overrides={5: {"stamina_cost": 22}})

add("shelter_swap", "庇护换位", 7, 5, "intermediate", 28, 80, 0,
    [], "tank_I", ["mobility","team"], 3,
    desc="与濒死友军换位并为其加护盾。", level_overrides={3: {"stamina_cost": 22}})

add("holy_shield_impact", "圣盾冲击", 7, 5, "intermediate", 28, 40, 0,
    W(6, [(1,4,0,3),(1,6,4,-1)], "physical_blunt") + [ST("staggered", 40)],
    "mix_SC_I", ["control","shield"], 1,
    desc="盾击目标使其眩晕，持盾命中提升。", requires_shield=True,
    level_overrides={3: {"attack_roll_bonus": 1}, 5: {"attack_roll_bonus": 2}})

add("unbending_rise", "不屈复起", 7, 5, "intermediate", 0, 120, 0,
    [{"effect_type": "status", "status_id": "death_ward", "trigger_condition": "battle_start",
      "params": {"source_skill_id": "warrior_unbending_rise"}},
     {"effect_type": "heal_fatal", "trigger_condition": "on_fatal_damage",
      "trigger_status_id": "death_ward",
      "params": {"base_heal": 8, "con_mod_base": 2, "con_mod_per_2_levels": 1, "heal_per_level": 4}},
     {"effect_type": "erase_status", "trigger_condition": "on_fatal_damage",
      "trigger_status_id": "death_ward"}
    ],
    "tank_I", ["survival"], 0, aura=30, passive=True, skill_type="passive",
    desc="濒死时保留1HP并获得格挡。", level_overrides={3: {"aura_cost": 24}})

add("guardian_judgment", "守护审判", 9, 7, "advanced", 0, 100, 0,
    W(6, [(1,8,0,4),(2,6,5,-1)], "radiant"),
    "ctl_A", ["output"], 1, aura=35,
    desc="自身减伤层数越高伤害越高。", level_overrides={5: {"aura_cost": 28}})

add("oath_domain", "誓魂领域", 9, 7, "advanced", 0, 200, 0,
    [ST("damage_reduction_up", 60, 1, tfilt="ally"),
     ST("hex_of_frailty", 60, 1, tfilt="enemy")],
    "tank_A", ["support","team"], 0, "radius", 2, 60,
    desc="友军减伤、敌军攻击降低。", level_overrides={5: {"aura_cost": 50}})

add("unbreakable_fortress", "不灭堡垒", 10, 9, "ultimate", 0, 300, 0,
    [ST("death_ward", 40, 1, tfilt="ally")],
    "tank_U", ["survival","team"], 0, "radius", 1, 80,
    desc="周围友军1回合内不会被击杀。", level_overrides={5: {"aura_cost": 65}})

# ==================== 岩龙流派 Stone Dragon ====================
add("earth_shake_aoe", "震地", 7, 5, "intermediate", 30, 40, 0,
    W(6, [(1,4,0,3),(1,8,4,-1)], "physical_blunt") + [ST("slow", 40, 1, 0, -1, tfilt="enemy")],
    "melee_I", ["aoe","control"], 0, "radius", 2,
    desc="范围震击减速周围敌人。", level_overrides={3: {"stamina_cost": 24}})

add("heavy_pressure_slash", "重压斩", 7, 5, "intermediate", 30, 40, -1,
    W(6, [(1,8,0,3),(2,6,4,-1)], "physical_blunt") + [ST("slow", 40, 1)],
    "melee_I", ["control"], 1,
    desc="沉重一击减速目标。", level_overrides={3: {"stamina_cost": 24}})

add("armor_break_hammer", "破甲锤击", 7, 5, "intermediate", 28, 40, 0,
    W(6, [(1,6,0,3),(1,8,4,-1)], "physical_blunt") + [ST("armor_break", 60, 1)],
    "melee_I", ["debuff"], 1,
    desc="锤击破甲降低目标防御。", level_overrides={3: {"stamina_cost": 22}})

add("crack_ground_impact", "裂地冲击", 7, 5, "intermediate", 35, 80, -1,
    W(6, [(1,6,0,3),(1,8,4,-1)], "physical_blunt"),
    "melee_I", ["aoe","terrain"], 3, "line", 3,
    desc="直线地面裂缝，穿越需额外移动。", level_overrides={3: {"stamina_cost": 28}})

add("stone_skin_stance", "岩肤架势", 5, 3, "basic", 22, 80, 0,
    [ST("damage_reduction_up", 60, 1)],
    "tank_B", ["defense"], 0,
    desc="物理减伤但自身减速。", level_overrides={3: {"stamina_cost": 16}})

add("avalanche_shove", "山崩推击", 3, 3, "basic", 22, 10, 0,
    W(6, [(1,4,0,-1)], "physical_blunt") + [
        {"effect_type": "status", "forced_move_mode": "knockback", "forced_move_distance": 2}
    ],
    "melee_B", ["control","displacement"], 1,
    desc="击退目标2格。", level_overrides={2: {"stamina_cost": 16}})

add("earth_vein_bind", "地脉锁足", 5, 3, "basic", 0, 80, -1,
    [ST("rooted", 40, 1, tfilt="enemy", save_ability="willpower", save_dc=10)],
    "mix_SW_B", ["control"], 3, aura=18,
    desc="令目标移动归零。", level_overrides={3: {"aura_cost": 14}})

add("stone_anchor_step", "岩锚步", 3, 3, "basic", 18, 40, 0,
    [], "tank_B", ["defense"], 0,
    desc="免疫击退和拉拽。", level_overrides={2: {"stamina_cost": 12}})

add("bone_crush_shock", "碎骨震击", 7, 5, "intermediate", 30, 80, -1,
    W(6, [(1,8,0,3),(2,6,4,-1)], "physical_blunt") + [ST("hex_of_frailty", 40, 1)],
    "melee_I", ["control"], 1,
    desc="削弱目标攻击力。", level_overrides={3: {"stamina_cost": 24}})

add("earth_rebound", "大地反弹", 5, 3, "basic", 22, 80, 0,
    [], "tank_B", ["defense"], 0,
    desc="受近战后反弹60%伤害并减速。", level_overrides={3: {"stamina_cost": 16}})

add("tread_crack", "踏裂", 5, 3, "basic", 24, 80, 0,
    [], "melee_B", ["mobility","terrain"], 0,
    desc="移动2格，经过格变为困难地形。", level_overrides={3: {"stamina_cost": 18}})

add("stone_pillar_rise", "石柱突起", 5, 3, "basic", 0, 100, 0,
    [], "mix_SW_B", ["terrain","control"], 3, aura=22,
    desc="生成1格可摧毁障碍。", level_overrides={3: {"aura_cost": 18}})

add("giant_rock_armor", "巨岩护甲", 7, 5, "intermediate", 0, 120, 0,
    [], "tank_I", ["defense"], 0, aura=28,
    desc="获得30%最大HP护盾。", level_overrides={3: {"aura_cost": 22}})

add("ground_roar_combo", "地鸣连击", 9, 7, "advanced", 38, 80, -1,
    W(6, [(1,6,0,3),(1,8,4,6),(2,6,7,-1)], "physical_blunt"),
    "melee_A", ["aoe","control"], 1, "radius", 1,
    desc="十字范围两段连击，二段后移目标行动顺序。", level_overrides={4: {"stamina_cost": 30}})

add("armor_shatter_fury", "崩甲怒劈", 9, 7, "advanced", 42, 100, -2,
    W(6, [(1,10,0,4),(2,6,5,7),(3,6,8,-1)], "physical_blunt"),
    "melee_A", ["breaker","output"], 1,
    desc="移除目标防御增益。", level_overrides={5: {"stamina_cost": 35}})

add("stone_dragon_tail", "岩龙摆尾", 7, 5, "intermediate", 32, 80, -1,
    W(6, [(1,6,0,3),(1,8,4,-1)], "physical_blunt") + [
        {"effect_type": "status", "forced_move_mode": "knockback", "forced_move_distance": 1}
    ],
    "melee_I", ["aoe","displacement"], 0, "radius", 1,
    desc="击退周围所有敌人1格。", level_overrides={3: {"stamina_cost": 26}})

add("mountain_suppression", "山岳镇压", 9, 7, "advanced", 0, 100, -2,
    W(6, [(1,8,0,4),(2,6,5,7),(2,8,8,-1)], "physical_blunt") + [
        ST("slow", 60, 2)
    ],
    "ctl_A", ["control"], 2, aura=35,
    desc="击倒目标使其下回合移动减半且不能反击。", level_overrides={5: {"aura_cost": 28}})

add("cliff_heavy_strike", "断崖重击", 9, 7, "advanced", 32, 80, -1,
    W(6, [(1,10,0,4),(2,6,5,7),(2,8,8,-1)], "physical_blunt"),
    "melee_A", ["output"], 1,
    desc="目标身后障碍/边缘额外50%伤害。", level_overrides={5: {"stamina_cost": 26}})

add("earth_pulse_domain", "大地脉冲", 9, 7, "advanced", 0, 160, 0,
    PW(8, 1, 8, "thunder") + [
        ST("slow", 60, 1, tfilt="enemy"),
    ],
    "ctl_A", ["aoe","control","team"], 0, "radius", 3, 55,
    desc="大范围减速敌军，友军免疫减速。", level_overrides={5: {"aura_cost": 45}})

add("stone_dragon_fall", "岩龙坠星", 10, 9, "ultimate", 0, 200, -2,
    PW(22, 2, 8, "physical_blunt") + [
        ST("staggered", 60, 2, tfilt="enemy"),
        ST("slow", 60, 1, tfilt="enemy"),
    ],
    "melee_U", ["aoe","control","terrain"], 3, "radius", 1, 80,
    desc="中心击倒，外围减速，生成困难地形。", level_overrides={5: {"attack_roll_bonus": 0}, 8: {"aura_cost": 65}})

# ==================== 虎爪流派 Tiger Claw ====================
add("frenzy_mode", "狂暴", 7, 5, "intermediate", 22, 120, 0,
    [ST("attack_up", 60, 2)],
    "melee_I", ["buff"], 0,
    desc="攻击大幅提升但防御降低。", level_overrides={3: {"stamina_cost": 16}})

add("leap_slash", "跳斩", 7, 5, "intermediate", 30, 40, 0,
    W(6, [(1,6,0,3),(1,8,4,-1)]),
    "mix_SA_I", ["mobility","aoe"], 2, "radius", 1,
    desc="跳跃落点范围攻击。", level_overrides={3: {"stamina_cost": 24}})

add("rip_slash", "撕裂斩", 7, 5, "intermediate", 28, 40, 0,
    W(6, [(1,6,0,3),(1,8,4,-1)]) + [ST("burning", 60, 1)],
    "melee_I", ["output","dot"], 1,
    desc="造成流血持续伤害。", level_overrides={3: {"stamina_cost": 22}})

add("chain_slashes", "连斩", 7, 5, "intermediate", 30, 10, 0,
    W(6, [(1,4,0,3),(1,6,4,-1)]),
    "melee_I", ["combo"], 1,
    desc="多段命中连斩。", level_overrides={3: {"stamina_cost": 24}, 5: {"attack_roll_bonus": 1}})

add("tiger_leap_assault", "虎跃突袭", 7, 5, "intermediate", 32, 80, 0,
    W(6, [(1,8,0,3),(2,6,4,-1)]),
    "mix_SA_I", ["mobility","output"], 3,
    desc="从高地/侧面发动伤害提升。", level_overrides={3: {"stamina_cost": 26}})

add("bloodthirst", "嗜血", 5, 3, "basic", 22, 80, 0,
    [], "melee_B", ["buff"], 0,
    desc="2回合攻击吸血20%→30%。", level_overrides={3: {"stamina_cost": 16}})

add("beastly_snarl", "猛兽怒吼", 3, 3, "basic", 24, 80, 0,
    [ST("hex_of_frailty", 40, 1, tfilt="enemy")],
    "mix_SW_B", ["control"], 0, "radius", 1,
    desc="周围敌人命中降低。", level_overrides={2: {"stamina_cost": 18}})

add("twin_claw_rip", "双爪开膛", 7, 5, "intermediate", 30, 40, -1,
    W(6, [(1,4,0,3),(1,8,4,-1)]),
    "melee_I", ["output"], 1,
    desc="对流血目标额外伤害。", level_overrides={3: {"stamina_cost": 24}})

add("flip_slash", "空翻斩", 7, 5, "intermediate", 35, 80, -1,
    W(6, [(1,6,0,3),(1,8,4,-1)]),
    "mix_SA_I", ["mobility","aoe"], 2, "radius", 1,
    desc="可越过1格障碍的跳斩。", level_overrides={3: {"stamina_cost": 28}})

add("wild_pursuit", "野性追击", 5, 3, "basic", 20, 80, 0,
    [], "agi_B", ["mobility"], 0,
    desc="本回合击杀后可移动2格。", level_overrides={3: {"stamina_cost": 14}})

add("throat_slit", "裂喉打击", 5, 3, "basic", 28, 80, -1,
    W(6, [(1,4,0,-1)]), "mix_SW_B", ["control"], 1,
    desc="目标不能使用战吼/咏唱类技能。", level_overrides={3: {"stamina_cost": 22}})

add("blood_mark", "血纹标记", 3, 3, "basic", 18, 80, 0,
    [], "mix_SW_B", ["mark"], 2,
    desc="目标受到流血伤害+50%。", level_overrides={2: {"stamina_cost": 12}})

add("frenzy_claw_dance", "狂爪旋舞", 9, 7, "advanced", 40, 80, -1,
    W(6, [(1,4,0,3),(1,6,4,6),(2,6,7,-1)]),
    "melee_A", ["aoe"], 0, "radius", 1,
    desc="对流血目标命中+10%的范围多段攻击。", level_overrides={4: {"stamina_cost": 32}})

add("pounce", "扑杀", 7, 5, "intermediate", 24, 40, 0,
    W(6, [(1,8,0,3),(2,6,4,-1)]),
    "mix_SA_I", ["output"], 2,
    desc="目标HP<50%时伤害+30%。", level_overrides={3: {"stamina_cost": 18}})

add("bestial_dodge", "兽性闪避", 5, 3, "basic", 20, 40, 0,
    [], "agi_B", ["defense","mobility"], 0,
    desc="闪避提升并可横移1格。", level_overrides={3: {"stamina_cost": 14}})

add("rage_stack_claws", "怒意叠爪", 7, 5, "intermediate", 0, 100, 0,
    [], "mix_SW_I", ["combo"], 0, aura=25,
    desc="2回合每次命中伤害+5%最多6层。", level_overrides={3: {"aura_cost": 20}})

add("crippling_strike_limb", "断肢猛击", 7, 5, "intermediate", 32, 80, -1,
    W(6, [(1,6,0,3),(1,8,4,-1)], "physical_blunt"),
    "melee_I", ["control"], 1,
    desc="目标不能使用位移技能。", level_overrides={3: {"stamina_cost": 26}})

add("blood_rain_chain", "血雨连环", 9, 7, "advanced", 0, 120, -1,
    W(6, [(1,4,0,3),(1,6,4,6),(2,6,7,-1)]),
    "melee_A", ["combo","aoe"], 1, aura=40,
    desc="击杀可跳相邻敌人继续攻击。", level_overrides={5: {"aura_cost": 32}})

add("tiger_god_possession", "虎神附体", 9, 7, "advanced", 0, 160, 0,
    [ST("attack_up", 60, 2)],
    "melee_A", ["buff"], 0, aura=55,
    desc="攻击+20%，移动+1，流血伤害+50%。", level_overrides={5: {"aura_cost": 45}})

add("hundred_crack_tiger", "百裂虎噬", 10, 9, "ultimate", 0, 200, 0,
    W(6, [(1,4,0,5),(1,6,6,7),(1,8,8,-1)]),
    "melee_U", ["combo","finisher"], 1, aura=80,
    desc="8段连击，每段命中后递减，流血目标不降。", level_overrides={5: {"attack_roll_bonus": 1}, 8: {"aura_cost": 65}})

# ==================== 影手流派 Shadow Hand ====================
add("shadow_strike", "影袭", 7, 5, "intermediate", 30, 80, 0,
    W(6, [(1,8,0,3),(2,6,4,-1)], "physical_pierce"),
    "mix_SA_I", ["mobility","output"], 3,
    desc="瞬移至目标背后并攻击。", level_overrides={3: {"stamina_cost": 24}, 5: {"attack_roll_bonus": 1}})

add("afterimage_step", "残影步", 7, 5, "intermediate", 0, 100, 0,
    [], "mix_SA_I", ["mobility"], 0, aura=25,
    desc="移动后原格留幻影，敌进入受伤。", level_overrides={3: {"aura_cost": 20}})

add("shadow_dagger_throw", "影刃投掷", 5, 3, "basic", 18, 10, 0,
    PW(6, 1, 6, "physical_pierce"),
    "agi_B", ["ranged"], 3,
    desc="有暗影标记时附加流血。", level_overrides={3: {"stamina_cost": 12}})

add("shadow_chain_bind", "阴影锁链", 7, 5, "intermediate", 0, 80, -1,
    [ST("rooted", 40, 1, tfilt="enemy")],
    "qi_I", ["control"], 3, aura=25,
    desc="定住敌人，暗影地形命中提升。", level_overrides={3: {"aura_cost": 20}})

add("phantom_decoy", "幻影诱饵", 5, 3, "basic", 0, 100, 0,
    [], "mix_SW_B", ["control"], 2, aura=22,
    desc="召唤1HP诱饵吸引一次攻击。", level_overrides={3: {"aura_cost": 18}})

add("shadow_hide_stance", "影遁架势", 7, 5, "intermediate", 22, 100, 0,
    [], "mix_SA_I", ["buff"], 0,
    desc="不攻击则隐匿，下击命中提升。", level_overrides={3: {"stamina_cost": 16}})

add("black_blade_soul_cut", "黑刃削魂", 7, 5, "intermediate", 0, 40, 0,
    W(6, [(1,4,0,3),(1,8,4,-1)], "negative_energy"),
    "qi_I", ["debuff"], 1, aura=18,
    desc="目标受治疗效果-50%。", level_overrides={3: {"aura_cost": 14}})

add("dark_step_swap", "暗步换位", 5, 3, "basic", 22, 80, 0,
    [], "agi_B", ["mobility","team"], 3,
    desc="与友军或幻影交换位置。", level_overrides={3: {"stamina_cost": 16}})

add("nightfall_pressure", "夜幕压迫", 7, 5, "intermediate", 0, 120, 0,
    [ST("hex_of_frailty", 60, 1, tfilt="enemy")],
    "qi_I", ["debuff","aoe"], 3, "radius", 1, 35,
    desc="区域内敌人命中降低。", level_overrides={3: {"aura_cost": 28}})

add("shadow_sever", "断影斩", 9, 7, "advanced", 0, 80, 0,
    W(6, [(1,6,0,3),(2,6,4,-1)], "force"),
    "ctl_A", ["output","control"], 1, aura=25,
    desc="移除目标闪避/隐身类增益。", level_overrides={5: {"aura_cost": 20}})

add("shadow_stitch", "影缝", 5, 3, "basic", 22, 40, 0,
    W(6, [(1,4,0,-1)], "negative_energy"),
    "mix_SW_B", ["control"], 2,
    desc="目标不能转向1回合。", level_overrides={3: {"stamina_cost": 16}})

add("blood_shadow_recycle", "血影回收", 7, 5, "intermediate", 0, 100, 0,
    [], "mix_SA_I", ["mobility"], 0, aura=25,
    desc="击杀后恢复斗气+移动+1。", level_overrides={3: {"aura_cost": 20}})

add("phantom_through", "幽影穿身", 9, 7, "advanced", 0, 100, -1,
    W(6, [(1,8,0,3),(2,6,4,-1)], "negative_energy"),
    "mix_SA_A", ["mobility","output"], 4, "line", 4, 35,
    desc="穿过路径敌人到末端，不能穿墙。", level_overrides={5: {"aura_cost": 28}})

# ==================== 落日柔术 Setting Sun ====================
add("shift_back", "后撤步", 5, 3, "basic", 16, 10, 0,
    [], "agi_B", ["defense","mobility"], 0,
    desc="后退2格并提升闪避。", level_overrides={3: {"stamina_cost": 10}})

add("slide_step_mv", "滑步", 3, 3, "basic", 14, 5, 0,
    [], "agi_B", ["mobility"], 0,
    desc="横移2格不触发反击。", level_overrides={2: {"stamina_cost": 8}})

add("leverage_throw", "借力摔", 7, 5, "intermediate", 22, 40, 0,
    W(6, [(1,4,0,3),(1,8,4,-1)], "physical_blunt") + [
        {"effect_type": "status", "forced_move_mode": "reposition", "forced_move_distance": 1}
    ],
    "mix_SA_I", ["control","displacement"], 1,
    desc="将目标摔到另一侧空格。", level_overrides={3: {"stamina_cost": 16}})

add("deflect_stance", "卸劲架势", 5, 3, "basic", 20, 80, 0,
    [ST("damage_reduction_up", 40, 2)],
    "tank_B", ["defense"], 0,
    desc="下次近战伤害减半并可横移。", level_overrides={3: {"stamina_cost": 14}})

add("redirect_charge", "引导冲锋", 7, 5, "intermediate", 24, 80, 0,
    [], "mix_SA_I", ["control","defense"], 0,
    desc="敌方冲锋时使其冲过身后。", level_overrides={3: {"stamina_cost": 18}})

add("spin_throw_mv", "转身投", 7, 5, "intermediate", 24, 80, -1,
    W(6, [(1,4,0,3),(1,8,4,-1)], "physical_blunt") + [
        {"effect_type": "status", "forced_move_mode": "knockback", "forced_move_distance": 1}
    ],
    "mix_SA_I", ["control","displacement"], 1,
    desc="击退1格并使目标转向。", level_overrides={3: {"stamina_cost": 18}})

add("four_ounce_deflect", "四两拨千斤", 9, 7, "advanced", 24, 100, 0,
    W(6, [(1,6,0,3),(2,6,4,-1)]),
    "mix_SA_A", ["defense"], 1,
    desc="受大伤时减半并反击。", level_overrides={5: {"stamina_cost": 18}})

add("step_drag", "牵制步", 5, 3, "basic", 18, 40, 0,
    W(6, [(1,4,0,-1)]),
    "agi_B", ["control","mobility"], 1,
    desc="攻击后与目标一同移动1格。", level_overrides={3: {"stamina_cost": 12}})

add("throw_chain", "摔击连环", 7, 5, "intermediate", 30, 80, -1,
    W(6, [(1,4,0,3),(1,8,4,-1)], "physical_blunt") + [
        {"effect_type": "status", "forced_move_mode": "knockback", "forced_move_distance": 1}
    ],
    "mix_SA_I", ["control","combo"], 1,
    desc="二段击退1格。", level_overrides={3: {"stamina_cost": 24}})

add("sunset_counter_throw", "落日反扔", 7, 5, "intermediate", 22, 80, 0,
    W(6, [(1,4,0,3),(1,6,4,-1)]),
    "mix_SA_I", ["defense"], 1,
    desc="闪避近战后将攻击者推至邻格。", level_overrides={3: {"stamina_cost": 16}})

add("over_shoulder", "借势越肩", 5, 3, "basic", 20, 40, 0,
    W(6, [(1,4,0,-1)]),
    "agi_B", ["mobility"], 1,
    desc="越过目标到其身后。", level_overrides={3: {"stamina_cost": 14}})

add("opening_bait", "空门诱导", 5, 3, "basic", 18, 80, 0,
    [], "mix_SW_B", ["control"], 2,
    desc="诱导AI/牵制目标。", level_overrides={3: {"stamina_cost": 12}})

add("push_hand_block", "推手封路", 5, 3, "basic", 20, 40, 0,
    W(6, [(1,4,0,-1)], "physical_blunt"),
    "melee_B", ["control"], 1,
    desc="目标不能穿过你的威胁区。", level_overrides={3: {"stamina_cost": 14}})

add("circle_step", "回环步", 3, 3, "basic", 18, 40, 0,
    [], "agi_B", ["mobility"], 0,
    desc="移到邻敌侧面+闪避提升。", level_overrides={2: {"stamina_cost": 12}})

add("armor_strip_grapple", "卸甲擒拿", 7, 5, "intermediate", 28, 80, -2,
    [], "ctl_I", ["control"], 1,
    desc="目标防御姿态失效且不能反击。", level_overrides={3: {"stamina_cost": 22}})

add("borrow_enemy_crash", "借敌撞敌", 7, 5, "intermediate", 30, 80, -1,
    W(6, [(1,4,0,3),(1,8,4,-1)], "physical_blunt"),
    "melee_I", ["control","aoe"], 1,
    desc="推动目标1格，撞到敌人双方受伤。", level_overrides={3: {"stamina_cost": 24}})

add("sunset_waltz", "落日圆舞", 9, 7, "advanced", 35, 100, -1,
    W(6, [(1,6,0,3),(2,6,4,-1)]),
    "mix_SA_A", ["aoe","mobility"], 0, "radius", 1,
    desc="每命中1人可移动1格。", level_overrides={5: {"stamina_cost": 28}})

add("reverse_blade_drain", "反刃引流", 9, 7, "advanced", 0, 100, 0,
    [], "ctl_A", ["defense"], 0, aura=28,
    desc="首近战50%伤害转移给相邻敌人。", level_overrides={5: {"aura_cost": 22}})

add("centerless_step", "无重心步法", 9, 7, "advanced", 0, 160, 0,
    [], "agi_A", ["defense","mobility"], 0, aura=45,
    desc="2回合免疫击退/拉拽+闪避提升。", level_overrides={5: {"aura_cost": 38}})

add("balance_reversal", "天秤反转", 10, 9, "ultimate", 0, 200, 0,
    [ST("staggered", 40, 2)],
    "agi_U", ["control","mobility"], 1, aura=80,
    desc="换位+眩晕，体型大则命中惩罚。", level_overrides={5: {"attack_roll_bonus": 2}, 8: {"aura_cost": 65}})

# ==================== 炎风流派 Desert Wind ====================
add("wind_blade_slash", "剑气斩", 7, 5, "intermediate", 0, 10, -1,
    PW(8, 1, 8), "qi_I", ["ranged","qi"], 3, "line", 3, 18,
    desc="斗气远程基础技。", level_overrides={3: {"aura_cost": 15}})

add("qi_dash_mv", "斗气冲刺", 7, 5, "intermediate", 0, 40, -1,
    PW(8, 1, 8, "fire"), "qi_I", ["mobility","qi"], 4, "line", 4, 22,
    desc="穿过路径敌人。", level_overrides={3: {"aura_cost": 18}})

add("flame_blade_aura", "炎刃附体", 7, 5, "intermediate", 0, 80, 0,
    [], "qi_I", ["buff","fire"], 0, aura=22,
    desc="2回合近战附加燃烧。", level_overrides={3: {"aura_cost": 18}})

add("flame_arc_sweep_aoe", "焰弧横扫", 7, 5, "intermediate", 0, 40, -1,
    PW(8, 1, 8, "fire") + [ST("burning", 40, 1, tfilt="enemy")],
    "qi_I", ["fire","aoe"], 1, "cone", 3, 18,
    desc="扇形火焰燃烧敌人。", level_overrides={3: {"aura_cost": 14}})

add("heat_wave_step", "热浪步", 3, 3, "basic", 0, 40, 0,
    [], "mix_SW_B", ["mobility","fire"], 0, aura=15,
    desc="移动2格，起点留下热浪。", level_overrides={2: {"aura_cost": 12}})

add("gale_slash", "烈风斩", 7, 5, "intermediate", 0, 40, -1,
    PW(8, 1, 8, "fire"), "qi_I", ["fire","aoe","displacement"], 3, "cone", 3, 22,
    desc="扇形火焰击退1格。", level_overrides={3: {"aura_cost": 18}})

add("spark_burst", "火星爆", 7, 5, "intermediate", 0, 80, -1,
    PW(10, 1, 8, "fire"), "qi_I", ["fire","aoe"], 3, "radius", 1, 28,
    desc="燃烧目标额外+30%伤害。", level_overrides={3: {"aura_cost": 22}})

add("burn_feet", "灼足", 5, 3, "basic", 0, 40, 0,
    PW(4, 1, 6, "fire") + [ST("slow", 40, 2)],
    "qi_B", ["control","fire"], 3, aura=15,
    desc="减速，移动则额外受伤。", level_overrides={3: {"aura_cost": 12}})

add("wind_ember", "风中残火", 7, 5, "intermediate", 0, 100, 0,
    [], "qi_I", ["defense","fire"], 0, aura=28,
    desc="闪避提升+闪避后对攻击者燃烧。", level_overrides={3: {"aura_cost": 22}})

add("blinding_flash", "炽光断视", 5, 3, "basic", 0, 80, -1,
    [ST("hex_of_frailty", 40, 2, tfilt="enemy")],
    "qi_B", ["control","radiant"], 2, "radius", 1, 20,
    desc="降低敌方命中。", level_overrides={3: {"aura_cost": 16}})

add("flame_cyclone", "烈焰回旋", 9, 7, "advanced", 0, 80, -1,
    PW(8, 1, 8, "fire"), "qi_A", ["fire","aoe"], 0, "radius", 1, 30,
    desc="周围2段火焰伤害。", level_overrides={5: {"attack_roll_bonus": 0}, 7: {"aura_cost": 24}})

add("wind_pressure_thrust", "风压突刺", 7, 5, "intermediate", 0, 40, -1,
    PW(8, 1, 8, "fire") + [ST("slow", 40, 1, tfilt="enemy")],
    "qi_I", ["fire","ranged"], 4, "line", 4, 25,
    desc="首目标击退1格，余下减速。", level_overrides={3: {"aura_cost": 20}})

add("burn_armor_qi", "焚甲斗气", 7, 5, "intermediate", 0, 80, 0,
    W(6, [(1,6,0,3),(1,8,4,-1)], "fire") + [
        ST("armor_break", 60, 1), ST("burning", 60, 1)
    ],
    "qi_I", ["fire","debuff"], 1, aura=22,
    desc="降低防御并附加燃烧。", level_overrides={3: {"aura_cost": 18}})

add("flame_wind_curtain", "炎风护幕", 7, 5, "intermediate", 0, 100, 0,
    [], "qi_I", ["defense","fire"], 0, "radius", 1, 28,
    desc="远程攻击命中降低+敌进入区域受伤害。", level_overrides={3: {"aura_cost": 22}})

add("sand_fire_trap_skill", "沙火陷阱", 7, 5, "intermediate", 0, 80, 0,
    PW(10, 1, 10, "fire"),
    "qi_I", ["fire","trap"], 3, aura=22,
    desc="敌进入受到火焰伤害并燃烧。", level_overrides={3: {"aura_cost": 18}})

add("crimson_wind_step", "赤风连步", 7, 5, "intermediate", 0, 100, 0,
    [], "qi_I", ["mobility","fire"], 0, aura=35,
    desc="本回合击杀可再移动最多3格。", level_overrides={3: {"aura_cost": 28}})

add("prairie_fire_domain", "燎原号令", 9, 7, "advanced", 0, 120, 0,
    PW(6, 1, 8, "fire"), "qi_A", ["fire","aoe","terrain"], 3, "radius", 2, 45,
    desc="区域持续燃烧2回合。", level_overrides={5: {"aura_cost": 38}})

add("crimson_lotus_dance", "红莲战舞", 9, 7, "advanced", 0, 160, 0,
    [], "qi_A", ["buff","fire"], 0, aura=55,
    desc="2回合AOE范围+1但有友军伤害。", level_overrides={5: {"aura_cost": 48}})

add("burning_dragon_tornado", "焚天龙卷", 10, 9, "ultimate", 0, 200, -2,
    PW(20, 2, 8, "fire") + [ST("burning", 80, 2, tfilt="enemy")],
    "qi_U", ["fire","aoe","finisher"], 4, "radius", 2, 80,
    desc="拉向中心1格并燃烧3回合。", level_overrides={5: {"attack_roll_bonus": 0}, 8: {"aura_cost": 65}})

# ==================== 真龙斗气 Original/Dragon Qi ====================
add("dragon_qi_slash", "斗气斩", 5, 3, "basic", 0, 10, -1,
    PW(6, 1, 8), "qi_B", ["ranged","qi"], 3, "line", 3, 14,
    desc="基础斗气远程攻击。", level_overrides={3: {"aura_cost": 10}})

add("qi_burst_aoe", "斗气爆裂", 7, 5, "intermediate", 0, 40, -1,
    PW(10, 1, 8), "qi_I", ["qi","aoe"], 2, "radius", 1, 22,
    desc="斗气爆裂范围伤害，中心额外+10%。", level_overrides={3: {"aura_cost": 18}})

add("qi_shockwave_push", "斗气冲击波", 7, 5, "intermediate", 0, 40, -1,
    PW(8, 1, 8), "qi_I", ["qi","aoe","displacement"], 3, "cone", 3, 22,
    desc="扇形斗气冲击并击退。", level_overrides={3: {"aura_cost": 18}})

add("qi_empower_strike", "斗气强化攻击", 7, 5, "intermediate", 0, 20, 0,
    [], "qi_I", ["buff","qi"], 0, aura=22,
    desc="下一击伤害+50%附带穿透。", level_overrides={3: {"aura_cost": 18}})

add("qi_chain_combo", "斗气连击", 7, 5, "intermediate", 0, 20, 0,
    PW(6, 1, 6), "qi_I", ["qi","combo"], 1, aura=22,
    desc="两段斗气连击。", level_overrides={3: {"aura_cost": 18}})

add("qi_awaken", "斗气觉醒", 9, 7, "advanced", 0, 120, 0,
    [], "qi_A", ["buff","qi"], 0, aura=55,
    desc="2回合技能伤害+20%射程+1。", level_overrides={5: {"aura_cost": 45}})

add("dragon_soul_stack", "龙魂叠印", 7, 5, "intermediate", 0, 80, 0,
    [], "qi_I", ["buff","qi"], 0, aura=18,
    desc="2回合每次命中+1龙魂最多5层。", level_overrides={3: {"aura_cost": 14}})

add("dragon_scale_guard", "龙鳞护体", 7, 5, "intermediate", 0, 100, 0,
    [ST("damage_reduction_up", 60, 1)],
    "tank_I", ["defense","qi"], 0, aura=35,
    desc="减伤25%并免疫暴击。", level_overrides={3: {"aura_cost": 28}})

add("dragon_fang_thrust_skill", "龙牙突", 7, 5, "intermediate", 0, 80, 0,
    PW(12, 1, 8, "physical_pierce"),
    "melee_I", ["qi","ranged"], 2, "line", 2, 28,
    desc="穿透第一目标攻击第二目标。", level_overrides={3: {"aura_cost": 22}})

add("dragon_tail_formation", "龙尾扫阵", 7, 5, "intermediate", 0, 80, 0,
    PW(8, 1, 8, "physical_blunt") + [
        {"effect_type": "status", "forced_move_mode": "knockback", "forced_move_distance": 1}
    ],
    "melee_I", ["qi","aoe","displacement"], 0, "radius", 1, 28,
    desc="击退周围敌人1格，撞墙眩晕。", level_overrides={3: {"aura_cost": 22}})

add("blue_dragon_pierce", "苍龙贯日", 9, 7, "advanced", 0, 100, -2,
    PW(14, 1, 10), "qi_A", ["qi","ranged"], 5, "line", 5, 45,
    desc="长距离穿透，每穿透一个目标伤害递减。", level_overrides={5: {"aura_cost": 38}})

add("dragon_earth_rend", "裂地龙斩", 9, 7, "advanced", 0, 100, -2,
    PW(14, 1, 10), "qi_A", ["qi","terrain","aoe"], 3, "line", 3, 50,
    desc="路径变困难地形2回合。", level_overrides={5: {"aura_cost": 42}})

add("holy_dragon_rush", "圣龙冲阵", 9, 7, "advanced", 0, 100, -1,
    PW(10, 1, 8, "radiant"), "qi_A", ["qi","charge","tank"], 4, "line", 4, 45,
    desc="冲锋后获得护盾并挑衅路径敌人。", level_overrides={5: {"aura_cost": 38}})

add("thousand_blade_dragon", "千刃龙舞", 9, 7, "advanced", 0, 100, 0,
    PW(4, 1, 6), "qi_A", ["qi","combo","finisher"], 1, aura=50,
    desc="6段连击，击杀溢出转移相邻敌人。", level_overrides={5: {"aura_cost": 42}})

add("dragon_shake_heaven", "龙震九天", 9, 7, "advanced", 0, 160, -2,
    PW(10, 1, 8, "thunder") + [
        ST("staggered", 40, 2, tfilt="enemy"),
        ST("slow", 40, 1, tfilt="enemy"),
    ],
    "qi_A", ["qi","aoe","control"], 0, "radius", 2, 55,
    desc="中心眩晕+外围减速的大范围龙震。", level_overrides={5: {"aura_cost": 48}})

add("dragon_soul_burst", "龙魂爆发", 9, 7, "advanced", 0, 200, 0,
    [], "qi_A", ["buff","qi"], 0, aura=60,
    desc="消耗龙魂，每层下次斗气技能+15%伤害/+3命中。", level_overrides={5: {"aura_cost": 50}})

add("reverse_scale_counter", "逆鳞反击", 9, 7, "advanced", 0, 160, 0,
    PW(16, 1, 10), "qi_A", ["qi","defense"], 0, aura=38,
    desc="致命伤时保留1HP并反击120%。", level_overrides={5: {"aura_cost": 30}})

add("dragon_form_stance", "龙化战姿", 10, 9, "ultimate", 0, 200, 0,
    [], "blood_U", ["buff","qi"], 0, aura=70,
    desc="3回合移动+1/近战附带30%斗气波/免疫恐惧。", level_overrides={5: {"aura_cost": 55}})

add("myriad_dragon_return", "万龙归墟", 10, 9, "ultimate", 0, 300, -3,
    PW(26, 2, 10), "qi_U", ["qi","aoe","finisher"], 4, "radius", 2, 100,
    desc="消耗所有斗气，每20斗气附加额外debuff层数。", level_overrides={5: {"attack_roll_bonus": 0}, 8: {"aura_cost": 80}})

# ==================== 新版铁潮战法 Iron Tide ====================
add("shoulder_guard_ram", "肩甲撞击", 3, 3, "basic", 16, 10, 0,
    W(6, [(1,4,0,-1)], "physical_blunt"), "melee_B", ["control"], 1,
    desc="肩甲撞击使目标随机转向。", level_overrides={2: {"stamina_cost": 12}})

add("iron_boot_block_road", "铁靴封路", 7, 5, "intermediate", 24, 60, 0,
    [], "melee_I", ["terrain","control"], 0,
    desc="选择相邻2格为威胁格，敌离开时受反击。", level_overrides={3: {"stamina_cost": 18}})

add("blade_wall_stroll", "刃墙步", 5, 3, "basic", 20, 20, 0,
    [ST("hex_of_frailty", 40, 1, tfilt="enemy")],
    "agi_B", ["mobility","control"], 0,
    desc="移动路径相邻敌人命中降低。", level_overrides={3: {"stamina_cost": 14}})

add("shoulder_brake", "压肩制动", 5, 3, "basic", 16, 20, 0,
    W(6, [(1,4,0,-1)], "physical_blunt"), "melee_B", ["control"], 1,
    desc="目标下次位移技能射程-2。", level_overrides={3: {"stamina_cost": 10}})

add("iron_tide_march", "铁潮号步", 7, 5, "intermediate", 22, 60, 0,
    [ST("attack_roll_bonus_up", 60, 1, tfilt="ally")],
    "cmd_I", ["buff","team"], 0,
    desc="自身和相邻友军移动后命中提升。", level_overrides={3: {"stamina_cost": 16}})

add("short_ram_gate", "短距破门", 7, 5, "intermediate", 24, 20, 2,
    W(6, [(1,6,0,3),(1,8,4,-1)], "physical_blunt"), "melee_I", ["output","breaker"], 1,
    desc="对障碍300%伤害，移除掩体加成。", level_overrides={3: {"stamina_cost": 18}})

add("step_cut_intercept", "裂步截断", 7, 5, "intermediate", 18, 20, 0,
    W(6, [(1,4,0,3),(1,6,4,-1)]), "mix_SA_I", ["defense","control"], 0,
    desc="警戒：敌进入前方1格自动攻击并终止移动。", level_overrides={3: {"stamina_cost": 14}})

add("heavy_armor_spin", "重甲回旋", 7, 5, "intermediate", 30, 60, -1,
    W(6, [(1,4,0,3),(1,6,4,-1)]), "melee_I", ["aoe"], 0, "radius", 1,
    desc="周围范围伤害，每命中1敌获得斗气。", level_overrides={3: {"stamina_cost": 24}})

add("repel_blade_road", "逼退剑路", 5, 3, "basic", 20, 20, 0,
    W(6, [(1,4,0,-1)]), "melee_B", ["control","displacement"], 1, "line", 2,
    desc="侧向推移前方敌人。", level_overrides={3: {"stamina_cost": 14}})

add("iron_tide_finisher", "铁潮终式", 10, 9, "ultimate", 32, 120, 0,
    W(6, [(1,8,0,5),(2,6,6,-1)], "physical_blunt"), "melee_U", ["aoe","finisher"], 2, "radius", 2, 60,
    desc="推进区域将所有敌人推向外侧。", level_overrides={5: {"attack_roll_bonus": 2}, 8: {"stamina_cost": 25}})

# ==================== 新版枪林封域 Spear Forest ====================
add("three_inch_repel", "三寸拒敌", 5, 3, "basic", 16, 5, 0,
    W(6, [(1,4,0,-1)], "physical_pierce"), "melee_B", ["control"], 2,
    desc="目标继续接近则受额外伤害。", level_overrides={3: {"stamina_cost": 10}})

add("spear_tip_border", "枪尖划界", 5, 3, "basic", 20, 40, 0,
    [], "melee_B", ["terrain","control"], 2, "line", 3,
    desc="直线警戒线，敌人穿越受伤。", level_overrides={3: {"stamina_cost": 14}})

add("tilt_dismount", "斜挑落马", 7, 5, "intermediate", 24, 80, 0,
    W(6, [(1,6,0,3),(1,8,4,-1)], "physical_pierce"), "mix_SA_I", ["control"], 2,
    desc="对移动过2+格目标造成额外伤害并移除剩余移动。", level_overrides={3: {"stamina_cost": 18}})

add("long_handle_turn", "长柄拨转", 5, 3, "basic", 18, 20, 0,
    W(6, [(1,4,0,-1)], "physical_blunt"), "agi_B", ["control","displacement"], 1,
    desc="将相邻敌人拨到另一侧。", level_overrides={3: {"stamina_cost": 12}})

add("spear_tail_ankle", "枪尾扫踝", 3, 3, "basic", 14, 5, 0,
    W(6, [(1,4,0,-1)], "physical_blunt") + [ST("slow", 40, 1)],
    "melee_B", ["control"], 1,
    desc="减速近身敌人。", level_overrides={2: {"stamina_cost": 8}})

add("goose_formation_charge", "雁行突列", 7, 5, "intermediate", 28, 60, 0,
    W(6, [(1,6,0,3),(1,8,4,-1)], "physical_pierce"), "mix_SA_I", ["mobility","output"], 3,
    desc="沿斜线突进绕到侧方。", level_overrides={3: {"stamina_cost": 22}, 5: {"attack_roll_bonus": 1}})

add("spear_forest_still", "枪林静立", 5, 3, "basic", 0, 60, 0,
    [], "tank_B", ["defense"], 0,
    desc="未移动时近战攻击者对己命中降低。", level_overrides={3: {"stamina_cost": 0}})

add("pierce_formation_draw", "穿阵引势", 7, 5, "intermediate", 26, 60, -1,
    W(6, [(1,4,0,3),(1,6,4,-1)], "physical_pierce"), "melee_I", ["output","recovery"], 2, "line", 4,
    desc="每命中一名敌人恢复体力。", level_overrides={3: {"stamina_cost": 20}})

add("flag_tip_break_order", "挑旗断令", 7, 5, "intermediate", 24, 60, 0,
    W(6, [(1,4,0,3),(1,6,4,-1)], "physical_pierce"), "mix_SW_I", ["control"], 2,
    desc="目标有光环时移除1个低级增益。", level_overrides={3: {"stamina_cost": 18}})

add("thicket_double_poke", "林隙连点", 7, 5, "intermediate", 26, 20, 0,
    W(6, [(1,4,0,3),(1,6,4,-1)], "physical_pierce"), "mix_SA_I", ["output"], 2,
    desc="选择两个不同目标各造成伤害。", level_overrides={3: {"stamina_cost": 20}})

add("throat_seal_step", "封喉定步", 5, 3, "basic", 22, 60, 0,
    W(6, [(1,4,0,-1)], "physical_pierce"), "mix_SW_B", ["control"], 2,
    desc="目标不能进行借机攻击。", level_overrides={3: {"stamina_cost": 16}})

add("pole_shift_force", "绕杆卸力", 5, 3, "basic", 16, 20, 0,
    [], "agi_B", ["defense"], 0,
    desc="下次近战伤害降低，攻击者行动顺序后移。", level_overrides={3: {"stamina_cost": 10}})

add("snake_path_spear", "蛇路进枪", 7, 5, "intermediate", 28, 60, 0,
    W(6, [(1,6,0,3),(1,8,4,-1)], "physical_pierce"), "mix_SA_I", ["mobility","output"], 3,
    desc="折线移动最多3格后攻击。", level_overrides={3: {"stamina_cost": 22}})

add("spear_gate_open", "枪阵空门", 5, 3, "basic", 20, 20, 0,
    W(6, [(1,4,0,-1)], "physical_pierce"), "mix_SW_B", ["buff","team"], 2,
    desc="标记空门，下名友军近战射程+1。", level_overrides={3: {"stamina_cost": 14}})

add("hundred_spear_formless", "百枪无形", 10, 9, "ultimate", 0, 160, 0,
    PW(6, 1, 6, "physical_pierce"), "agi_U", ["aoe","finisher"], 3, "cone", 5, 70,
    desc="扇形5次随机枪影打击。", level_overrides={5: {"attack_roll_bonus": 1}, 8: {"aura_cost": 55}})

add("goose_return_stab", "雁行回刺", 5, 3, "basic", 18, 20, 0,
    PW(8, 1, 6, "physical_pierce"), "mix_SA_B", ["defense","output"], 2,
    desc="受远程攻击后反击。", level_overrides={3: {"stamina_cost": 12}})

add("calm_draw_spear", "静息拔枪", 5, 3, "basic", 0, 60, 0,
    [], "agi_B", ["buff"], 0,
    desc="未移动下回合首次攻击射程+1暴击+20%。", level_overrides={3: {"stamina_cost": 0}})

add("spear_forest_press", "枪林压阵", 9, 7, "advanced", 0, 80, 0,
    [ST("burning", 60, 1, tfilt="enemy")],
    "qi_A", ["control","terrain"], 3, "radius", 1, 35,
    desc="区域敌人行动结束时若未移动则受伤。", level_overrides={5: {"aura_cost": 28}})

# ==================== 新版裂岩重兵 Stone Splitter ====================
add("crack_step_smash", "裂阶砸击", 7, 5, "intermediate", 24, 40, 0,
    W(6, [(1,6,0,3),(1,8,4,-1)], "physical_blunt"), "melee_I", ["output","terrain"], 1,
    desc="目标格变为碎石地形。", level_overrides={3: {"stamina_cost": 18}})

add("beam_break_fall", "断梁重落", 9, 7, "advanced", 32, 60, -1,
    W(6, [(1,10,0,4),(2,6,5,7),(2,8,8,-1)], "physical_blunt"), "melee_A", ["output"], 1,
    desc="高风险斩杀，未命中自身行动顺序后移。", level_overrides={5: {"stamina_cost": 26}})

add("gravel_blind", "石屑盲击", 5, 3, "basic", 20, 40, 0,
    W(6, [(1,4,0,-1)], "physical_blunt"), "melee_B", ["control","aoe"], 1, "cone", 3,
    desc="扇形碎石降低远程命中。", level_overrides={3: {"stamina_cost": 14}})

add("wrist_shock_steal", "震腕夺势", 5, 3, "basic", 18, 40, 0,
    W(6, [(1,4,0,-1)], "physical_blunt"), "mix_SW_B", ["control"], 1,
    desc="降低目标斗气获取50%。", level_overrides={3: {"stamina_cost": 12}})

add("heavy_weapon_root", "重兵扎根", 5, 3, "basic", 0, 60, 0,
    [], "tank_B", ["defense"], 0,
    desc="不能被强制位移，每回合首击伤害+20%。", level_overrides={3: {"stamina_cost": 0}})

add("shatter_armor_echo", "碎甲回音", 7, 5, "intermediate", 28, 60, 0,
    W(6, [(1,6,0,3),(1,8,4,-1)], "physical_blunt"), "melee_I", ["output","debuff"], 1,
    desc="已有破防目标额外震荡伤害。", level_overrides={3: {"stamina_cost": 22}})

add("beam_waist_block", "横梁拦腰", 5, 3, "basic", 24, 40, 0,
    W(6, [(1,4,0,-1)], "physical_blunt"), "melee_B", ["control","aoe"], 1, "line", 3,
    desc="横排攻击降低目标射程。", level_overrides={3: {"stamina_cost": 18}})

add("falling_mountain_intimidate", "坠山威吓", 7, 5, "intermediate", 28, 80, 0,
    [ST("hex_of_frailty", 40, 1, save_ability="willpower", save_dc=12, tfilt="enemy")],
    "ctl_I", ["control","aoe"], 0, "radius", 2,
    desc="周围敌人意志检定，失败者伤害降低。", level_overrides={3: {"stamina_cost": 22}})

add("anvil_pincer", "铁砧夹击", 5, 3, "basic", 20, 20, 0,
    W(6, [(1,4,0,-1)], "physical_blunt"), "mix_SW_B", ["control","team"], 1,
    desc="另一侧有友军时目标眩晕抗性降低。", level_overrides={3: {"stamina_cost": 14}})

add("rock_hum_charge", "岩鸣蓄震", 5, 3, "basic", 0, 60, 0,
    [], "melee_B", ["buff"], 0,
    desc="储存震波，下次重武器攻击命中时溅射。", level_overrides={3: {"stamina_cost": 0}})

add("shatter_formation_drag", "碎阵拖拽", 7, 5, "intermediate", 26, 60, 0,
    W(6, [(1,4,0,3),(1,8,4,-1)], "physical_blunt"), "melee_I", ["control","displacement"], 1,
    desc="拖到身前并使护卫状态失效。", level_overrides={3: {"stamina_cost": 20}})

add("thick_blade_cover", "厚刃遮身", 5, 3, "basic", 18, 40, 0,
    [], "tank_B", ["defense"], 0,
    desc="正面大幅减伤，侧背无减免。", level_overrides={3: {"stamina_cost": 12}})

add("shock_foot_crack_ring", "震足裂圈", 7, 5, "intermediate", 26, 60, 0,
    [], "melee_I", ["control","terrain"], 0, "radius", 1,
    desc="敌人离开裂圈时受到伤害。", level_overrides={3: {"stamina_cost": 20}})

add("giant_blade_sweep_shadow", "巨兵扫影", 9, 7, "advanced", 38, 80, -1,
    W(6, [(1,8,0,3),(2,6,4,6),(2,8,7,-1)], "physical_blunt"), "melee_A", ["aoe","output"], 1, "cone", 4,
    desc="半圆4格大范围AOE，每多命中1人命中-1。", level_overrides={5: {"stamina_cost": 30}})

add("gate_break_aftershock", "破门余震", 9, 7, "advanced", 35, 80, 0,
    W(6, [(1,8,0,4),(2,6,5,-1)], "physical_blunt"), "melee_A", ["output","aoe"], 1, "radius", 1,
    desc="摧毁障碍时周围敌人受伤。", level_overrides={5: {"stamina_cost": 28}})

add("mountain_roar_soul_break", "山吼断魂", 7, 5, "intermediate", 0, 80, -1,
    PW(8, 1, 8, "thunder"), "qi_I", ["control"], 2, "radius", 1, 40,
    desc="目标无法获得士气增益。", level_overrides={3: {"aura_cost": 32}})

add("linger_force_crush", "余力横压", 3, 3, "basic", 16, 5, 0,
    W(6, [(1,4,0,-1)], "physical_blunt") + [ST("slow", 40, 1)],
    "melee_B", ["control"], 1,
    desc="本回合未移动则附加迟缓。", level_overrides={2: {"stamina_cost": 10}})

# ==================== 新版星刃决斗 Star Blade ====================
add("reverse_wrist_blade", "反腕剔刃", 5, 3, "basic", 20, 60, 0,
    W(6, [(1,4,0,-1)], "physical_slash"), "mix_SA_B", ["control"], 1,
    desc="目标下次反击伤害-50%。", level_overrides={3: {"stamina_cost": 14}})

add("step_star_swap", "踏星换位", 5, 3, "basic", 18, 20, 0,
    [ST("staggered", 40, 1)],
    "agi_B", ["mobility","control"], 1,
    desc="与相邻敌人互换位置。", level_overrides={3: {"stamina_cost": 12}})

add("calm_draw_blade", "静息拔刀", 5, 3, "basic", 0, 60, 0,
    [], "agi_B", ["buff"], 0,
    desc="未移动下回合首次近战射程+1暴击+20%。", level_overrides={3: {"stamina_cost": 0}})

add("sever_light_hand", "断光截手", 7, 5, "intermediate", 22, 60, 0,
    W(6, [(1,4,0,3),(1,8,4,-1)], "physical_slash"), "mix_SW_I", ["control"], 1,
    desc="目标下次技能消耗+5体力。", level_overrides={3: {"stamina_cost": 16}})

add("reverse_star_evade", "逆星回避", 5, 3, "basic", 16, 20, 0,
    [], "agi_B", ["defense","mobility"], 0,
    desc="受近战后可移动1格。", level_overrides={3: {"stamina_cost": 10}})

add("twin_moon_cross", "双月交错", 7, 5, "intermediate", 26, 20, 0,
    W(6, [(1,4,0,3),(1,6,4,-1)], "physical_slash"), "mix_SA_I", ["output","aoe"], 1,
    desc="对两个相邻目标各造成伤害。", level_overrides={3: {"stamina_cost": 20}})

add("silver_line_seal_throat", "银线封喉", 7, 5, "intermediate", 26, 80, 3,
    W(6, [(1,4,0,3),(1,8,4,-1)], "physical_slash"), "mix_SW_I", ["control"], 1,
    desc="沉默近战武技。", level_overrides={3: {"stamina_cost": 20}})

add("clear_blade_read", "明刃识破", 5, 3, "basic", 18, 60, 0,
    [], "mix_SW_B", ["defense"], 0,
    desc="攻击者命中低于80%时其攻击额外降低。", level_overrides={3: {"stamina_cost": 12}})

add("falling_star_break_step", "落星断步", 5, 3, "basic", 20, 20, 0,
    W(6, [(1,4,0,-1)], "physical_pierce"), "mix_SA_B", ["control"], 1,
    desc="目标已移动则下回合移动-1。", level_overrides={3: {"stamina_cost": 14}})

add("instant_three_judgment", "刹那三判", 7, 5, "intermediate", 30, 80, 0,
    W(6, [(1,4,0,3),(1,6,4,-1)], "physical_slash"), "mix_SA_I", ["combo"], 1,
    desc="3次判定连击。", level_overrides={3: {"stamina_cost": 24}})

add("mirror_blade_reflect", "镜刃反光", 5, 3, "basic", 0, 60, 0,
    [], "mix_SW_B", ["defense"], 0, aura=20,
    desc="本回合首远程物理伤害减半。", level_overrides={3: {"aura_cost": 16}})

add("star_blade_final_judge", "星刃终裁", 10, 9, "ultimate", 0, 160, 0,
    W(6, [(2,8,0,5),(3,8,6,-1)], "physical_slash"), "agi_U", ["finisher"], 1, aura=65,
    desc="3+debuff造成320%伤害，否则附加2随机减益。", level_overrides={5: {"attack_roll_bonus": 1}, 8: {"aura_cost": 50}})

add("duel_declare_name", "决斗宣名", 7, 5, "intermediate", 18, 60, 0,
    [], "cmd_I", ["control"], 3,
    desc="单挑宣言，互相增伤15%对外减伤10%。", level_overrides={3: {"stamina_cost": 12}})

# ==================== 新版盾环护卫 Shield Ring ====================
add("ring_shield_move_defend", "环盾移防", 7, 5, "intermediate", 18, 20, 0,
    [ST("damage_reduction_up", 40, 1, tfilt="ally")],
    "tank_I", ["defense","team","mobility"], 2,
    desc="移动到友军邻格并提升其防御。", level_overrides={3: {"stamina_cost": 12}})

add("shield_edge_cut_arrow", "盾缘截箭", 5, 3, "basic", 20, 20, 0,
    [], "tank_B", ["defense","team"], 3,
    desc="降低指定友军受到的远程物理伤害。", level_overrides={3: {"stamina_cost": 14}})

add("joint_shield_close", "合盾闭阵", 7, 5, "intermediate", 30, 80, 0,
    [ST("damage_reduction_up", 60, 1, tfilt="ally")],
    "tank_I", ["defense","team"], 0, "radius", 1,
    desc="相邻2+友军全体正面减伤。", level_overrides={3: {"stamina_cost": 24}})

add("shield_back_push_ally", "盾背推送", 3, 3, "basic", 14, 5, 0,
    [], "tank_B", ["mobility","team"], 1,
    desc="将相邻友军推送1格不触发反击。", level_overrides={2: {"stamina_cost": 8}})

add("round_shield_trip", "圆盾绊足", 5, 3, "basic", 16, 20, 0,
    W(6, [(1,4,0,-1)], "physical_blunt"), "melee_B", ["control"], 1,
    desc="目标下次离开相邻格消耗额外移动。", level_overrides={3: {"stamina_cost": 10}})

add("shield_face_recoil", "盾面反震", 7, 5, "intermediate", 20, 60, 0,
    [], "tank_I", ["defense"], 0,
    desc="1回合内首近战攻击者承受50%反震。", level_overrides={3: {"stamina_cost": 14}})

add("calm_shield_focus", "静盾凝心", 5, 3, "basic", 0, 60, 0,
    [], "tank_B", ["defense"], 0,
    desc="不移动时魔法抗性+25%并清除1轻度减益。", level_overrides={3: {"stamina_cost": 0}})

add("guard_formation_call_back", "护阵回呼", 7, 5, "intermediate", 26, 80, 0,
    [], "cmd_I", ["mobility","team"], 3,
    desc="将友军向自己方向移动最多2格。", level_overrides={3: {"stamina_cost": 20}})

add("shield_lamp_reveal", "盾灯照破", 7, 5, "intermediate", 20, 60, 0,
    [], "mix_CW_I", ["control","support"], 0, "radius", 2,
    desc="揭示隐匿敌人并降低其闪避。", level_overrides={3: {"stamina_cost": 14}})

add("crisis_shield_protect", "危急架护", 9, 7, "advanced", 28, 80, 0,
    [], "tank_A", ["defense","team"], 3,
    desc="友军可能被击杀时替其承受50%伤害。", level_overrides={5: {"stamina_cost": 22}})

add("rampart_step", "壁垒步伐", 7, 5, "intermediate", 18, 60, 0,
    [ST("damage_reduction_up", 40, 1, tfilt="ally")],
    "tank_I", ["defense","team"], 0,
    desc="移动-1但结束移动后相邻友军防御提升。", level_overrides={3: {"stamina_cost": 12}})

add("shield_ring_pressure", "盾环压迫", 7, 5, "intermediate", 24, 60, 0,
    [ST("hex_of_frailty", 40, 1, tfilt="enemy")],
    "tank_I", ["control","aoe"], 0, "radius", 1,
    desc="周围敌人命中降低，攻击非你目标额外降低。", level_overrides={3: {"stamina_cost": 18}})

add("rescue_sidestep", "救援侧身", 5, 3, "basic", 20, 20, 0,
    [], "tank_B", ["mobility","team"], 2,
    desc="与受威胁友军换位并获得闪避。", level_overrides={3: {"stamina_cost": 14}})

add("shield_ridge_shatter", "盾棱碎击", 7, 5, "intermediate", 22, 20, 0,
    W(6, [(1,4,0,3),(1,8,4,-1)], "physical_blunt"), "melee_I", ["control","breaker"], 1,
    desc="目标维持姿态时打断并额外造成30%伤害。", level_overrides={3: {"stamina_cost": 16}})

add("shelter_afterglow", "庇护余辉", 7, 5, "intermediate", 0, 80, 0,
    [{"effect_type": "heal", "power": 2, "params": {"dice_count": 2, "dice_sides": 6, "con_mod_heal": True}}],
    "tank_I", ["heal","defense","team"], 2, aura=28,
    desc="友军恢复HP并获得下次受伤害降低。", level_overrides={3: {"aura_cost": 22}})

add("circle_formation_final_oath", "圆阵终誓", 10, 9, "ultimate", 0, 160, 0,
    [], "tank_U", ["defense","team","finisher"], 0, "radius", 2, 70,
    desc="范围内友军获得伤害护盾，破裂时对相邻敌人造成伤害。", level_overrides={5: {"aura_cost": 55}})

add("hidden_blade_under_shield", "盾下藏锋", 5, 3, "basic", 18, 20, 0,
    [], "melee_B", ["buff"], 0,
    desc="成功减免伤害后下次攻击+35%伤害。", level_overrides={3: {"stamina_cost": 12}})

# ==================== 新版双刃逐影 Twin Blade ====================
add("twin_shadow_shift", "双影错身", 7, 5, "intermediate", 20, 20, 0,
    W(6, [(1,4,0,3),(1,6,4,-1)], "physical_pierce"), "mix_SA_I", ["mobility","output"], 2,
    desc="移动到侧方并造成伤害。", level_overrides={3: {"stamina_cost": 14}})

add("short_blade_stitch", "短刃缝针", 5, 3, "basic", 22, 5, 0,
    W(6, [(1,4,0,-1)], "physical_pierce"), "mix_SA_B", ["output","combo"], 1,
    desc="45%×3连击，适合破护盾。", level_overrides={3: {"stamina_cost": 16}})

add("shadow_nail_remain", "影钉留身", 7, 5, "intermediate", 24, 60, -1,
    W(6, [(1,4,0,3),(1,8,4,-1)], "physical_pierce"), "mix_SA_I", ["control"], 2,
    desc="投出影钉，目标离开原格再受伤害。", level_overrides={3: {"stamina_cost": 18}})

add("refract_light_retreat", "折光退切", 7, 5, "intermediate", 20, 20, 0,
    W(6, [(1,4,0,3),(1,6,4,-1)], "physical_slash"), "mix_SA_I", ["mobility","output"], 1,
    desc="攻击后后退1格，未命中则下次CD-1。", level_overrides={3: {"stamina_cost": 14}})

add("twin_blade_rotate", "双刃轮换", 3, 3, "basic", 0, 20, 0,
    [], "agi_B", ["buff","combo"], 0,
    desc="下两次攻击主副手轮换。", level_overrides={2: {"stamina_cost": 0}})

add("blade_pick_gap", "刃下拾隙", 5, 3, "basic", 14, 5, 1,
    W(6, [(1,4,0,-1)]), "mix_SA_B", ["output"], 1,
    desc="对被友军命中过的目标造成115%伤害。", level_overrides={3: {"stamina_cost": 8}})

add("shadow_rope_step", "影绳牵步", 7, 5, "intermediate", 26, 60, 0,
    W(6, [(1,4,0,3),(1,6,4,-1)], "physical_pierce"), "mix_SA_I", ["mobility","output"], 3,
    desc="命中后移到相邻格，目标不能借机攻击。", level_overrides={3: {"stamina_cost": 20}})

add("breath_stop_short_thrust", "断息短刺", 7, 5, "intermediate", 22, 60, 0,
    W(6, [(1,4,0,3),(1,6,4,-1)], "physical_pierce"), "mix_SW_I", ["control"], 1,
    desc="目标下次施放技能前需额外消耗体力。", level_overrides={3: {"stamina_cost": 16}})

add("thin_blade_strip_armor", "薄刃拆甲", 7, 5, "intermediate", 20, 20, 0,
    W(6, [(1,4,0,3),(1,6,4,-1)], "physical_pierce"), "mix_SA_I", ["debuff"], 1,
    desc="叠拆甲最多3层，3层时目标防御大幅降低。", level_overrides={3: {"stamina_cost": 14}})

add("short_hop_tendon_cut", "短跳割筋", 7, 5, "intermediate", 26, 60, 0,
    W(6, [(1,4,0,3),(1,8,4,-1)], "physical_slash") + [ST("slow", 40, 1)],
    "mix_SA_I", ["mobility","control"], 2,
    desc="跳到相邻格并减速目标。", level_overrides={3: {"stamina_cost": 20}})

add("phantom_blade_bait", "虚刃诱反", 5, 3, "basic", 18, 20, 0,
    W(6, [(1,4,0,-1)], "physical_pierce"), "mix_SA_B", ["control"], 1,
    desc="诱发目标反击，反击失败则失衡。", level_overrides={3: {"stamina_cost": 12}})

add("twin_blade_chain_plan", "双刃连环计", 7, 5, "intermediate", 30, 40, 0,
    W(6, [(1,4,0,3),(1,6,4,-1)], "physical_slash"), "mix_SA_I", ["combo","output"], 1,
    desc="两击全命中可移动1格。", level_overrides={3: {"stamina_cost": 24}})

add("shadow_split_blade_throw", "影裂投刃", 7, 5, "intermediate", 18, 40, 0,
    PW(6, 1, 6, "physical_pierce"), "agi_I", ["ranged"], 3,
    desc="目标背后有友军时命中+15%。", level_overrides={3: {"stamina_cost": 12}})

add("white_blade_vanish", "白刃消失", 7, 5, "intermediate", 0, 80, 0,
    [], "agi_I", ["defense","mobility"], 0, aura=25,
    desc="受攻击前可闪到相邻空格。", level_overrides={3: {"aura_cost": 20}})

add("hundred_shadow_final_dance", "百影终舞", 10, 9, "ultimate", 0, 160, 0,
    W(6, [(1,4,0,5),(1,6,6,-1)], "physical_slash"), "agi_U", ["aoe","finisher"], 3, "radius", 2, 75,
    desc="在最多4名敌人间跳跃攻击。", level_overrides={5: {"attack_roll_bonus": 1}, 8: {"aura_cost": 60}})

add("phantom_walk_mark", "幽步标记", 5, 3, "basic", 16, 40, 0,
    [], "agi_B", ["mark"], 3,
    desc="标记目标，你向其移动消耗-1。", level_overrides={3: {"stamina_cost": 10}})

# ==================== 新版雷砧锤术 Thunder Anvil ====================
add("thunder_reverberate_armor", "回响破甲", 7, 5, "intermediate", 24, 20, 0,
    W(6, [(1,4,0,3),(1,8,4,-1)], "physical_blunt") + [ST("armor_break", 60, 1)],
    "melee_I", ["debuff"], 1,
    desc="目标上回合被命中过时附加破甲。", level_overrides={3: {"stamina_cost": 18}})

add("thunder_anvil_tuning", "雷砧定音", 7, 5, "intermediate", 0, 60, 0,
    [], "mix_SW_I", ["buff","control"], 0,
    desc="2回合每回合首次锤命中后目标命中-8%。", level_overrides={3: {"stamina_cost": 0}})

add("deep_bell_fall", "沉钟坠击", 9, 7, "advanced", 30, 60, -1,
    W(6, [(1,10,0,4),(2,6,5,7),(2,8,8,-1)], "physical_blunt"), "melee_A", ["output"], 1,
    desc="目标行动顺序晚于你则额外+25%伤害。", level_overrides={5: {"stamina_cost": 24}})

add("skull_reverb_shock", "震颅余波", 7, 5, "intermediate", 26, 60, 0,
    W(6, [(1,6,0,3),(1,8,4,-1)], "physical_blunt") + [ST("hex_of_frailty", 40, 1, tfilt="enemy")],
    "melee_I", ["aoe","control"], 1, "radius", 1,
    desc="主目标+周围伤害并附加命中下降。", level_overrides={3: {"stamina_cost": 20}})

add("knee_break_hammer", "断膝锤点", 5, 3, "basic", 22, 60, 0,
    W(6, [(1,4,0,-1)], "physical_blunt"), "melee_B", ["control"], 1,
    desc="目标不能攀爬/跳跃。", level_overrides={3: {"stamina_cost": 16}})

add("war_drum_reverse_echo", "战鼓逆响", 7, 5, "intermediate", 0, 80, 0,
    [], "cmd_I", ["control","team"], 0, "radius", 2, 28,
    desc="友军行动顺序提前，敌军后移。", level_overrides={3: {"aura_cost": 22}})

add("thunder_vein_shatter_ground", "雷纹碎地", 9, 7, "advanced", 30, 80, 0,
    PW(10, 1, 8, "lightning"), "qi_A", ["terrain","aoe"], 2, "radius", 1,
    desc="区域变成带电地，进入者受伤。", level_overrides={5: {"stamina_cost": 24}})

add("hammer_handle_recoil", "击槌反震", 5, 3, "basic", 20, 40, 0,
    [], "tank_B", ["defense"], 0,
    desc="本回合受近战攻击者承受其伤害的25%。", level_overrides={3: {"stamina_cost": 14}})

add("bone_ring_chase_judge", "骨鸣追判", 5, 3, "basic", 18, 5, 0,
    W(6, [(1,8,0,-1)], "physical_blunt"), "melee_B", ["output"], 1,
    desc="对拥有行动延后/减速的目标伤害提升。", level_overrides={3: {"stamina_cost": 12}})

add("break_momentum_knock", "破势敲击", 7, 5, "intermediate", 22, 20, 0,
    W(6, [(1,4,0,3),(1,8,4,-1)], "physical_blunt"), "mix_SW_I", ["control"], 1,
    desc="减少目标10→15斗气。", level_overrides={3: {"stamina_cost": 16}})

add("thunder_anvil_horizontal_seal", "雷砧横印", 9, 7, "advanced", 32, 80, -1,
    W(6, [(1,6,0,3),(1,8,4,6),(2,6,7,-1)], "physical_blunt"), "melee_A", ["aoe","control"], 0, "line", 3,
    desc="横排3格攻击，敌人反击率-20%。", level_overrides={5: {"stamina_cost": 26}})

add("muffled_thunder_crush", "闷雷压顶", 9, 7, "advanced", 0, 80, -1,
    PW(10, 1, 8, "thunder") + [ST("staggered", 40, 2)],
    "qi_A", ["control"], 3, aura=40,
    desc="目标正在蓄力/姿态时打断并眩晕。", level_overrides={5: {"attack_roll_bonus": 0}, 7: {"aura_cost": 32}})

add("hammer_heart_law_guard", "锤心守律", 7, 5, "intermediate", 18, 60, 0,
    [], "mix_CW_I", ["defense"], 0,
    desc="受控制时50%概率改为行动顺序后移。", level_overrides={3: {"stamina_cost": 12}})

add("nine_echo_final_hammer", "九响终槌", 10, 9, "ultimate", 0, 160, 0,
    PW(4, 1, 6, "thunder"), "ctl_U", ["aoe","finisher"], 2, "radius", 2, 80,
    desc="9道震响随机落点，同目标3次后附加眩晕抗性降低。", level_overrides={5: {"attack_roll_bonus": 1}, 8: {"aura_cost": 65}})

add("static_tow", "静电牵引", 7, 5, "intermediate", 22, 60, 0,
    [], "qi_I", ["control","displacement"], 3,
    desc="把目标向带电格方向移动1格。", level_overrides={3: {"stamina_cost": 16}})

# ==================== 新版苍焰斗气 Azure Flame ====================
add("vein_burn_circulate", "燃脉运转", 7, 5, "intermediate", 0, 60, 0,
    [], "qi_I", ["buff","qi"], 0,
    desc="2回合近战额外获取斗气但受治疗降低。", level_overrides={3: {"stamina_cost": 0}})

add("qi_blade_return", "气刃折返", 7, 5, "intermediate", 0, 20, -1,
    PW(6, 1, 8), "qi_I", ["ranged","qi"], 3, "line", 3, 20,
    desc="只命中1人时气刃折返再造成40%伤害。", level_overrides={3: {"aura_cost": 16}, 5: {"attack_roll_bonus": 0}})

add("azure_flame_mark", "苍焰印记", 7, 5, "intermediate", 0, 20, 0,
    [], "qi_I", ["mark"], 4, aura=15,
    desc="标记2回合，对其斗气技能伤害+20%。", level_overrides={3: {"aura_cost": 12}})

add("flame_step_light_body", "焰步轻身", 5, 3, "basic", 0, 60, 0,
    [], "qi_B", ["mobility"], 0, aura=20,
    desc="本回合移动+1且可穿越友军。", level_overrides={3: {"aura_cost": 16}})

add("inner_breath_closed_loop", "内息闭环", 7, 5, "intermediate", 0, 60, 0,
    [], "qi_I", ["recovery"], 0,
    desc="放弃攻击恢复20体力并获得10斗气。", level_overrides={3: {"stamina_cost": 0}})

add("qi_bolt_snipe", "斗气裂矢", 7, 5, "intermediate", 0, 40, 0,
    PW(10, 1, 8), "qi_I", ["ranged","qi"], 5, aura=25,
    desc="距离每增加1格命中-5%。", level_overrides={3: {"aura_cost": 22}})

add("burn_breath_press", "灼息压阵", 9, 7, "advanced", 0, 80, 0,
    [ST("burning", 60, 1, tfilt="enemy")],
    "qi_A", ["control","fire","terrain"], 3, "radius", 1, 35,
    desc="区域敌人行动结束时若未移动则受伤。", level_overrides={5: {"aura_cost": 28}})

add("qi_corridor", "斗气回廊", 9, 7, "advanced", 0, 80, 0,
    [], "qi_A", ["recovery","terrain","team"], 3, "line", 3, 28,
    desc="友军经过恢复体力，敌人经过损失体力。", level_overrides={5: {"aura_cost": 22}})

add("azure_flame_guard", "苍焰护腕", 5, 3, "basic", 0, 20, 0,
    [], "qi_B", ["defense"], 0, aura=18,
    desc="下次受近战伤害-25%，攻击者受30%斗气伤害。", level_overrides={3: {"aura_cost": 14}})

add("burn_soul_short_oath", "燃魂短誓", 9, 7, "advanced", 0, 60, 0,
    [], "qi_A", ["buff"], 0, aura=12,
    desc="2回合伤害+20%但每回合结束损失5%HP。", level_overrides={5: {"aura_cost": 8}})

add("flame_line_slice_formation", "焰线切阵", 9, 7, "advanced", 0, 60, -1,
    PW(8, 1, 8, "fire"), "qi_A", ["fire","aoe","control"], 4, "line", 3, 30,
    desc="横线攻击减少目标防御增益效果。", level_overrides={5: {"aura_cost": 25}})

add("qi_drag_star", "斗气牵星", 7, 5, "intermediate", 0, 60, 0,
    PW(4, 1, 6), "qi_I", ["control","displacement"], 4, aura=25,
    desc="将目标向自己方向拉1格。", level_overrides={3: {"aura_cost": 20}})

add("azure_flame_condense_core", "苍焰凝核", 5, 3, "basic", 0, 60, 0,
    [], "qi_B", ["buff"], 0,
    desc="蓄1枚气核最多2枚，每枚下次斗气消耗-10。", level_overrides={3: {"stamina_cost": 0}})

add("flame_breath_seal_domain", "焰息断域", 9, 7, "advanced", 0, 80, -1,
    PW(8, 1, 8, "fire"), "qi_A", ["fire","control","terrain"], 3, "radius", 1, 38,
    desc="区域禁止隐匿和传送类位移。", level_overrides={5: {"aura_cost": 30}})

add("azure_flame_chain_stars", "苍焰连星", 10, 9, "ultimate", 0, 80, 0,
    PW(6, 1, 6), "qi_U", ["qi","ranged","finisher"], 4, aura=48,
    desc="对最多3个不同目标各造成80%斗气伤害。", level_overrides={5: {"attack_roll_bonus": 1}, 8: {"aura_cost": 38}})

# ==================== 新版龙血战纹 Dragon Blood ====================
add("scale_crack_open", "鳞纹开裂", 7, 5, "intermediate", 0, 60, 0,
    [ST("attack_up", 60, 2)],
    "blood_I", ["buff"], 0,
    desc="牺牲8%HP获得攻击和控制抗性提升。", level_overrides={3: {"stamina_cost": 0}})

add("dragon_breath_short", "龙息短吐", 7, 5, "intermediate", 0, 60, -1,
    PW(8, 1, 8, "fire"), "blood_I", ["fire","aoe"], 3, "cone", 3, 28,
    desc="锥形龙息附加灼鳞反伤效果。", level_overrides={3: {"aura_cost": 25}})

add("reverse_scale_fury_step", "逆鳞怒步", 7, 5, "intermediate", 20, 60, 0,
    [], "blood_I", ["mobility","buff"], 0,
    desc="本回合受过伤则移动+2伤害+10%。", level_overrides={3: {"stamina_cost": 14}})

add("dragon_claw_rend_mark", "龙爪裂印", 7, 5, "intermediate", 22, 20, 0,
    W(6, [(1,4,0,3),(1,6,4,-1)], "physical_slash"), "blood_I", ["output","mark"], 1,
    desc="叠裂印最多3层，3层时目标受100%龙血伤害。", level_overrides={3: {"stamina_cost": 16}})

add("blood_vein_guard_heart", "血纹护心", 7, 5, "intermediate", 0, 80, 0,
    [], "blood_I", ["defense","survival"], 0, aura=22,
    desc="致命伤时保留1HP但下回合不能用终式。", level_overrides={3: {"aura_cost": 18}})

add("dragon_vein_step_ring", "龙脉踏响", 7, 5, "intermediate", 24, 60, 0,
    W(6, [(1,4,0,3),(1,6,4,-1)], "physical_blunt"), "blood_I", ["aoe","buff"], 0, "radius", 1,
    desc="每命中1人叠龙脉最多3层提升斗气回复。", level_overrides={3: {"stamina_cost": 18}})

add("dragon_eye_lock_soul", "龙瞳锁魂", 7, 5, "intermediate", 0, 60, 0,
    [], "blood_I", ["control","mark"], 4, aura=28,
    desc="目标被锁定，对你的命中降低你对其命中提升。", level_overrides={3: {"aura_cost": 22}})

add("broken_horn_headbutt", "碎角顶撞", 7, 5, "intermediate", 26, 60, 0,
    W(6, [(1,6,0,3),(1,8,4,-1)], "physical_blunt"), "blood_I", ["mobility","control"], 2,
    desc="突进最多2格击退目标，自身反冲5%HP。", level_overrides={3: {"stamina_cost": 20}})

add("dragon_blood_boil", "龙血沸腾", 9, 7, "advanced", 0, 80, 0,
    [], "blood_A", ["buff"], 0,
    desc="2回合每损失10%HP获得斗气但受治疗-15%。", level_overrides={5: {"stamina_cost": 0}})

add("scale_rain_counter", "鳞雨反击", 7, 5, "intermediate", 0, 60, 0,
    PW(6, 1, 8, "physical_pierce"), "blood_I", ["defense","ranged"], 3, aura=22,
    desc="受远程攻击后反射鳞雨。", level_overrides={3: {"aura_cost": 18}})

add("blood_vein_oath_kill", "血纹誓杀", 9, 7, "advanced", 0, 80, 0,
    [], "blood_A", ["mark","buff"], 5, aura=32,
    desc="标记2回合，击杀恢复大量斗气。", level_overrides={5: {"aura_cost": 25}})

add("red_scale_low_crouch", "赤鳞低伏", 5, 3, "basic", 14, 20, 0,
    [], "blood_B", ["defense"], 0,
    desc="本回合远程命中你-20%但移动-1。", level_overrides={3: {"stamina_cost": 8}})

add("dragon_bone_shock_shatter", "龙骨震裂", 9, 7, "advanced", 28, 60, 0,
    W(6, [(1,8,0,4),(2,6,5,-1)], "physical_blunt") + [ST("armor_break", 60, 1)],
    "blood_A", ["output","debuff"], 1,
    desc="HP低于50%时额外破防。", level_overrides={5: {"stamina_cost": 22}})

add("blood_wing_leap_skill", "血翼跃迁", 9, 7, "advanced", 0, 120, 0,
    W(6, [(1,6,0,4),(1,8,5,-1)], "physical_slash"), "blood_A", ["mobility","aoe"], 4, "radius", 1, 38,
    desc="跳到4格内空地，对落点相邻敌人造成伤害。", level_overrides={5: {"aura_cost": 30}})

add("dragon_vein_resonance", "龙纹共鸣", 9, 7, "advanced", 0, 80, 0,
    [], "blood_A", ["buff","team"], 0, "radius", 2, 32,
    desc="友军获得斗气获取+20%但受治疗-5%。", level_overrides={5: {"aura_cost": 25}})

add("reverse_scale_roar", "逆鳞咆哮", 9, 7, "advanced", 0, 80, 0,
    [ST("hex_of_frailty", 40, 2, save_ability="willpower", save_dc=14, save_failure_status_id="taunted", tfilt="enemy")],
    "blood_A", ["control","aoe"], 0, "radius", 2, 42,
    desc="敌意志检定失败则只能攻击你。", level_overrides={5: {"aura_cost": 35}})

add("undying_dragon_scar", "不灭龙痕", 10, 9, "ultimate", 0, 120, 0,
    [], "blood_U", ["survival","heal"], 0,
    desc="清除1减益并获得减益抗性，低血额外恢复。", level_overrides={5: {"stamina_cost": 0}})

# ==================== 新版军势统御 Army Command ====================
add("front_row_swap_defense", "前列换防", 5, 3, "basic", 20, 20, 0,
    [], "cmd_B", ["mobility","team"], 3,
    desc="令两名相邻友军交换位置。", level_overrides={3: {"stamina_cost": 14}})

add("spear_shield_advance", "矛盾齐进", 7, 5, "intermediate", 24, 60, 0,
    [ST("damage_reduction_up", 40, 1, tfilt="ally")],
    "cmd_I", ["buff","team"], 0, "radius", 2,
    desc="持盾/长兵友军移动后防御提升。", level_overrides={3: {"stamina_cost": 18}})

add("rear_fill_position", "后队补位", 5, 3, "basic", 20, 20, 0,
    [], "cmd_B", ["mobility","team"], 4,
    desc="目标友军向掩体或友军方向移动1格。", level_overrides={3: {"stamina_cost": 14}})

add("reject_horse_order", "拒马口令", 5, 3, "basic", 24, 60, 0,
    [], "cmd_B", ["terrain","control"], 4,
    desc="指定2格成为拒马区，骑乘单位通过停步。", level_overrides={3: {"stamina_cost": 18}})

add("army_drum_short_order", "军鼓短令", 7, 5, "intermediate", 28, 80, 0,
    [], "cmd_I", ["buff","team"], 0, "radius", 3,
    desc="最低行动顺序友军提前。", level_overrides={3: {"stamina_cost": 22}})

add("surround_order", "包围令", 7, 5, "intermediate", 22, 60, 0,
    [], "cmd_I", ["buff","team"], 4,
    desc="相邻2+友军时所有友军对其伤害+15%。", level_overrides={3: {"stamina_cost": 16}})

add("scatter_formation", "散阵号", 7, 5, "intermediate", 24, 60, 0,
    [ST("damage_reduction_up", 40, 1, tfilt="ally")],
    "mix_CW_I", ["defense","team"], 0, "radius", 3,
    desc="友军分散则AOE减伤20%。", level_overrides={3: {"stamina_cost": 18}})

add("return_to_flag_step", "归旗步", 7, 5, "intermediate", 18, 60, 0,
    [], "cmd_I", ["mobility","team","recovery"], 5,
    desc="目标向自己移动最多2格，到达则恢复体力。", level_overrides={3: {"stamina_cost": 12}})

add("formation_eye_fortify", "阵眼加固", 7, 5, "intermediate", 24, 80, 0,
    [], "cmd_I", ["terrain","defense","team"], 3,
    desc="指定一格为阵眼，友军站上防御大幅提升。", level_overrides={3: {"stamina_cost": 18}})

add("feint_retreat_order", "佯退令", 9, 7, "advanced", 22, 60, 0,
    [ST("hex_of_frailty", 40, 2, tfilt="enemy")],
    "cmd_A", ["control","team"], 4,
    desc="友军后退1格，原格诱敌标记。", level_overrides={5: {"stamina_cost": 16}})

add("read_formation_insight", "破阵读势", 5, 3, "basic", 14, 20, 0,
    [], "cmd_B", ["support"], 5,
    desc="显示目标可用技能倾向。", level_overrides={3: {"stamina_cost": 8}})

add("defend_rotation_recover", "守备轮换", 7, 5, "intermediate", 28, 80, 0,
    [], "cmd_I", ["recovery","team"], 0, "radius", 2,
    desc="最多2友军恢复体力并清除迟缓。", level_overrides={3: {"stamina_cost": 22}})

add("split_team_lure_kill", "裂队诱杀", 9, 7, "advanced", 24, 60, 0,
    [], "cmd_A", ["control","mark"], 5,
    desc="标记敌人，离开友军范围则受借机打击。", level_overrides={5: {"stamina_cost": 18}})

add("vanguard_pardon_skill", "先锋赦令", 9, 7, "advanced", 28, 80, 0,
    [ST("attack_up", 40, 2, tfilt="ally")],
    "cmd_A", ["buff","team"], 4,
    desc="目标伤害+25%但防御-15%。", level_overrides={5: {"stamina_cost": 22}})

add("rearguard_iron_law", "后卫铁律", 7, 5, "intermediate", 22, 60, 0,
    [ST("damage_reduction_up", 40, 2, tfilt="ally")],
    "cmd_I", ["defense","team"], 4,
    desc="目标未移动则下次受伤害-35%。", level_overrides={3: {"stamina_cost": 16}})

add("army_converge_flow", "军势归流", 10, 9, "ultimate", 0, 160, 0,
    [], "cmd_U", ["support","team","finisher"], 0, "radius", 4, 70,
    desc="范围友军清除1轻度减益并可各移动1格不触发反击。", level_overrides={5: {"aura_cost": 55}})

print(f"\n{'='*60}")
print(f"Total skills defined: {len(ALL_SKILLS)}")
print(f"Output directory: {OUTPUT_DIR}")
print(f"Mode: {'DRY RUN' if DRY_RUN else 'GENERATE'}")
print(f"{'='*60}\n")

created = 0
skipped = 0
errors = 0

for s in ALL_SKILLS:
    fname = s["skill_id"] + ".tres"
    fpath = os.path.join(OUTPUT_DIR, fname)

    if os.path.exists(fpath):
        skipped += 1
        continue

    try:
        content = build_tres(s)
        if DRY_RUN:
            print(f"  [DRY] {fname}")
        else:
            with open(fpath, "w", encoding="utf-8", newline="\n") as f:
                f.write(content)
        created += 1
    except Exception as e:
        print(f"  ERROR: {fname} - {e}")
        errors += 1

print(f"\nComplete: created={created}, skipped={skipped}, errors={errors}")
