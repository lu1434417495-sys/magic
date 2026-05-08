## 文件说明：该脚本属于 battle fate 攻击共用规则层，集中承载命中线、封暴击状态与 d20 攻击成功语义。
## 审查重点：重点核对 runtime 与 preview 是否复用同一套 roll 判定口径，避免 crit/fumble 规则再次分叉。
## 备注：该层只承载纯判定 helper；真正的掷骰顺序和效果结算仍分别由 resolver 持有。

class_name BattleFateAttackRules
extends RefCounted

const BATTLE_UNIT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const BattleUnitState = BATTLE_UNIT_STATE_SCRIPT

const NATURAL_HIT_ROLL := 20
const STATUS_BLACK_STAR_BRAND_ELITE: StringName = &"black_star_brand_elite"
const STATUS_CROWN_BREAK_BROKEN_FANG: StringName = &"crown_break_broken_fang"


func does_attack_roll_hit(hit_roll: int, attack_check: Dictionary) -> bool:
	var natural_one_auto_miss := bool(attack_check.get("natural_one_auto_miss", true))
	if natural_one_auto_miss and hit_roll <= 1:
		return false
	var natural_twenty_auto_hit := bool(attack_check.get("natural_twenty_auto_hit", true))
	if natural_twenty_auto_hit and hit_roll >= NATURAL_HIT_ROLL:
		return true
	return hit_roll >= int(attack_check.get("required_roll", 21))


func does_gate_die_crit(crit_gate_roll: int, crit_gate_die: int, crit_locked: bool) -> bool:
	return not crit_locked \
		and crit_gate_die > NATURAL_HIT_ROLL \
		and crit_gate_roll == crit_gate_die


func is_high_threat_crit_roll(
	hit_roll: int,
	crit_locked: bool,
	crit_gate_die: int,
	crit_threshold: int
) -> bool:
	return not crit_locked \
		and crit_gate_die == NATURAL_HIT_ROLL \
		and hit_roll >= crit_threshold


func is_attack_crit_locked(unit_state: BattleUnitState) -> bool:
	return unit_state != null and (
		unit_state.has_status_effect(STATUS_BLACK_STAR_BRAND_ELITE)
		or unit_state.has_status_effect(STATUS_CROWN_BREAK_BROKEN_FANG)
		or _unit_has_status_bool_param(unit_state, &"lock_crit")
	)


func _unit_has_status_bool_param(unit_state: BattleUnitState, param_key: StringName) -> bool:
	if unit_state == null or param_key == &"":
		return false
	for status_id_variant in unit_state.status_effects.keys():
		var status_id := ProgressionDataUtils.to_string_name(status_id_variant)
		var status_entry = unit_state.get_status_effect(status_id)
		if status_entry == null or status_entry.params == null:
			continue
		var params: Dictionary = status_entry.params
		if bool(_get_status_param_string_key(params, param_key, false)):
			return true
	return false


func _get_status_param_string_key(params: Dictionary, param_key: StringName, fallback: Variant) -> Variant:
	if params == null or param_key == &"":
		return fallback
	var param_name := String(param_key)
	if params.has(param_key):
		return params[param_key]
	if params.has(param_name):
		return params[param_name]
	for key_variant in params.keys():
		if ProgressionDataUtils.to_string_name(key_variant) == param_key:
			return params[key_variant]
	return fallback
