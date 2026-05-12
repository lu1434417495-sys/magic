class_name BattleSpecialProfileGate
extends RefCounted

const BattleSpecialProfileGateResult = preload("res://scripts/systems/battle/core/battle_special_profile_gate_result.gd")
const BattleCommand = preload("res://scripts/systems/battle/core/battle_command.gd")
const BattleState = preload("res://scripts/systems/battle/core/battle_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")

const PLAYER_BLOCK_MESSAGE := "该禁咒配置未通过校验，暂时无法施放。"

var _registry_snapshot: Dictionary = {}


func setup(registry_snapshot: Dictionary) -> void:
	_registry_snapshot = registry_snapshot.duplicate(true) if registry_snapshot != null else {}


func preflight_skill(skill_def: SkillDef, battle_state: BattleState) -> BattleSpecialProfileGateResult:
	return _evaluate_skill(skill_def, battle_state)


func preview_skill(
	skill_def: SkillDef,
	command: BattleCommand,
	active_unit: BattleUnitState,
	battle_state: BattleState
) -> BattleSpecialProfileGateResult:
	return _evaluate_skill(skill_def, battle_state, command, active_unit)


func can_execute_skill(
	skill_def: SkillDef,
	command: BattleCommand,
	active_unit: BattleUnitState,
	battle_state: BattleState
) -> BattleSpecialProfileGateResult:
	return _evaluate_skill(skill_def, battle_state, command, active_unit)


func _evaluate_skill(
	skill_def: SkillDef,
	battle_state: BattleState,
	command: BattleCommand = null,
	active_unit: BattleUnitState = null
) -> BattleSpecialProfileGateResult:
	var result := BattleSpecialProfileGateResult.new()
	if skill_def == null or skill_def.combat_profile == null:
		return _block(result, &"", &"", &"missing_skill", "Missing skill definition.", {})
	result.skill_id = skill_def.skill_id
	result.profile_id = skill_def.combat_profile.special_resolution_profile_id
	if result.profile_id == &"":
		result.allowed = true
		return result
	if not bool(_registry_snapshot.get("ok", false)):
		return _block(result, result.profile_id, result.skill_id, &"content_invalid", PLAYER_BLOCK_MESSAGE, {
			"errors": _registry_snapshot.get("errors", []),
		})
	var profile_id_by_skill_id: Variant = _registry_snapshot.get("profile_id_by_skill_id", {})
	if profile_id_by_skill_id is not Dictionary:
		return _block(result, result.profile_id, result.skill_id, &"missing_profile_index", PLAYER_BLOCK_MESSAGE, {})
	if String((profile_id_by_skill_id as Dictionary).get(String(result.skill_id), "")) != String(result.profile_id):
		return _block(result, result.profile_id, result.skill_id, &"skill_not_owned", PLAYER_BLOCK_MESSAGE, {})
	var profiles: Variant = _registry_snapshot.get("profiles", {})
	if profiles is not Dictionary or not (profiles as Dictionary).has(String(result.profile_id)):
		return _block(result, result.profile_id, result.skill_id, &"profile_missing", PLAYER_BLOCK_MESSAGE, {})
	var profile_snapshot: Variant = (profiles as Dictionary).get(String(result.profile_id), {})
	if profile_snapshot is not Dictionary:
		return _block(result, result.profile_id, result.skill_id, &"profile_invalid", PLAYER_BLOCK_MESSAGE, {})
	if String((profile_snapshot as Dictionary).get("runtime_resolver_id", "")) != String(result.profile_id):
		return _block(result, result.profile_id, result.skill_id, &"resolver_mismatch", PLAYER_BLOCK_MESSAGE, profile_snapshot as Dictionary)
	if battle_state == null:
		return _block(result, result.profile_id, result.skill_id, &"missing_battle_state", PLAYER_BLOCK_MESSAGE, {})
	if command == null and active_unit == null:
		result.allowed = true
		return result
	result.allowed = true
	return result


func _block(
	result: BattleSpecialProfileGateResult,
	profile_id: StringName,
	skill_id: StringName,
	block_code: StringName,
	player_message: String,
	debug_details: Dictionary
) -> BattleSpecialProfileGateResult:
	result.allowed = false
	result.profile_id = profile_id
	result.skill_id = skill_id
	result.block_code = block_code
	result.player_message = player_message
	result.debug_details = debug_details.duplicate(true)
	return result
