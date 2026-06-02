"""Validate warrior .tres files for correctness."""
import os, re, sys

SKILLS_DIR = r"E:\game\magic\data\configs\skills"
BUDGETS = {"basic": 60, "intermediate": 120, "advanced": 180, "ultimate": 240}
VALID_ATTRS = {"strength", "agility", "constitution", "perception", "intelligence", "willpower"}

issues = []

for fname in sorted(os.listdir(SKILLS_DIR)):
    if not fname.startswith("warrior_") or not fname.endswith(".tres"):
        continue
    fpath = os.path.join(SKILLS_DIR, fname)
    with open(fpath, encoding="utf-8") as f:
        content = f.read()

    # 1. grant_status_id: "combo_stack" -> should be bare string in params (not &"..." in params context)
    #    params dict values can be bare strings. Only top-level StringName fields need &.
    #    But let's check if grant_status_id appears correctly.
    gs_matches = re.findall(r'grant_status_id.*?(combo_stack)', content)
    # This is fine - params use bare strings

    # 2. effect_type should be &"damage" etc (top-level fields with &)
    for m in re.finditer(r'effect_type\s*=\s*"(damage|status|heal)"', content):
        issues.append(f"{fname}: effect_type '{m.group(1)}' missing & prefix")

    # 3. level_overrides keys should be int not string
    for m in re.finditer(r'"(\d+)"\s*:', content):
        # Check context - is this inside level_overrides or level_description_configs?
        # level_overrides uses int keys, level_description_configs uses string keys
        before = content[:m.start()]
        last_override = before.rfind("level_overrides")
        last_config = before.rfind("level_description_configs")
        if last_override > last_config:
            issues.append(f"{fname}: level_overrides has string key '{m.group(1)}' (should be int)")

    # 4. attribute_growth_progress total must match tier budget
    tier_m = re.search(r'growth_tier\s*=\s*&"(\w+)"', content)
    # Find attribute_growth_progress dict
    ag_start = content.find("attribute_growth_progress")
    if ag_start != -1 and tier_m:
        tier = tier_m.group(1)
        ag_section = content[ag_start:ag_start+300]
        values = re.findall(r'"(\w+)":\s*(\d+)', ag_section)
        total = 0
        for k, v in values:
            if k in VALID_ATTRS:
                total += int(v)
        expected = BUDGETS.get(tier, 0)
        if total != expected:
            issues.append(f"{fname}: attr_growth total={total}, expected={expected} for tier={tier}")

    # 5. mastery_curve length must match max_level
    lv_m = re.search(r'max_level\s*=\s*(\d+)', content)
    curve_m = re.search(r'mastery_curve\s*=\s*PackedInt32Array\(([^)]+)\)', content)
    if lv_m and curve_m:
        max_lv = int(lv_m.group(1))
        curve_len = len(re.findall(r'\d+', curve_m.group(1)))
        if curve_len != max_lv:
            issues.append(f"{fname}: max_level={max_lv} but mastery_curve has {curve_len} entries")

    # 6. non_core_max_level relationship
    nc_m = re.search(r'non_core_max_level\s*=\s*(\d+)', content)
    if lv_m and nc_m:
        ncl = int(nc_m.group(1))
        ml = int(lv_m.group(1))
        if ml == 3 and ncl != 3:
            issues.append(f"{fname}: max={ml} but non_core={ncl} (expected 3)")
        elif ml == 5 and ncl != 3:
            issues.append(f"{fname}: max={ml} but non_core={ncl} (expected 3)")
        elif ml == 7 and ncl != 5:
            issues.append(f"{fname}: max={ml} but non_core={ncl} (expected 5)")
        elif ml == 9 and ncl != 7:
            issues.append(f"{fname}: max={ml} but non_core={ncl} (expected 7)")
        elif ml == 10 and ncl != 9:
            issues.append(f"{fname}: max={ml} but non_core={ncl} (expected 9)")

    # 7. growth_tier must match non_core_max_level
    if tier_m and nc_m:
        tier = tier_m.group(1)
        ncl = int(nc_m.group(1))
        expected_tier = {3: "basic", 5: "intermediate", 7: "advanced", 9: "ultimate"}.get(ncl)
        if expected_tier and tier != expected_tier:
            issues.append(f"{fname}: non_core={ncl} but growth_tier={tier} (expected {expected_tier})")

    # 8. stamina_cost should be set or aura_cost should be set for active skills
    if 'stamina_cost = 0' in content and 'aura_cost' not in content and 'skill_type = &"passive"' not in content:
        if 'cooldown_tu = 0' not in content or 'ap_cost = 0' not in content:
            pass  # active skills with 0 stamina but aura_cost is ok
            # But pure stamina=0, aura=0 active skills are suspicious
            # Let's check - some skills only cost AP
            pass

print(f"Scanned {len([f for f in os.listdir(SKILLS_DIR) if f.startswith('warrior_') and f.endswith('.tres')])} warrior .tres files")
print(f"Issues found: {len(issues)}")
for i in issues:
    print(f"  {i}")
if not issues:
    print("  All files pass validation!")
