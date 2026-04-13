class_name WildEncounterGrowthSystem
extends RefCounted

const ENCOUNTER_ANCHOR_DATA_SCRIPT = preload("res://scripts/systems/encounter_anchor_data.gd")


func apply_step_advance(world_data: Dictionary, old_step: int, new_step: int, encounter_rosters: Dictionary) -> bool:
	if world_data == null or encounter_rosters == null or encounter_rosters.is_empty():
		return false
	if new_step <= old_step:
		return false

	var changed := false
	for encounter_variant in world_data.get("encounter_anchors", []):
		var encounter = encounter_variant as ENCOUNTER_ANCHOR_DATA_SCRIPT
		if encounter == null or encounter.encounter_kind != ENCOUNTER_ANCHOR_DATA_SCRIPT.ENCOUNTER_KIND_SETTLEMENT:
			continue
		var roster = encounter_rosters.get(encounter.encounter_profile_id)
		if roster == null:
			continue
		var interval := maxi(int(roster.growth_step_interval), 1)
		var relative_old_step := maxi(old_step - encounter.suppressed_until_step, 0)
		var relative_new_step := maxi(new_step - encounter.suppressed_until_step, 0)
		var old_cycles := int(relative_old_step / interval)
		var new_cycles := int(relative_new_step / interval)
		var stage_gain := maxi(new_cycles - old_cycles, 0)
		if stage_gain <= 0:
			continue
		var max_stage: int = encounter.growth_stage
		if roster.has_method("get_max_stage"):
			max_stage = int(roster.get_max_stage())
		var next_stage := mini(encounter.growth_stage + stage_gain, max_stage)
		if next_stage == encounter.growth_stage:
			continue
		encounter.growth_stage = next_stage
		changed = true
	return changed


func apply_battle_victory(encounter_anchor, world_step: int, encounter_rosters: Dictionary) -> bool:
	var encounter = encounter_anchor as ENCOUNTER_ANCHOR_DATA_SCRIPT
	if encounter == null or encounter.encounter_kind != ENCOUNTER_ANCHOR_DATA_SCRIPT.ENCOUNTER_KIND_SETTLEMENT:
		return false
	if encounter_rosters == null or encounter_rosters.is_empty():
		return false
	var roster = encounter_rosters.get(encounter.encounter_profile_id)
	if roster == null:
		return false

	var initial_stage := maxi(int(roster.initial_stage), 0)
	encounter.growth_stage = maxi(encounter.growth_stage - 1, initial_stage)
	encounter.suppressed_until_step = maxi(
		encounter.suppressed_until_step,
		maxi(world_step, 0) + maxi(int(roster.suppression_steps_on_victory), 0)
	)
	return true
