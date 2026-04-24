## 文件说明：该脚本属于战斗战报解释层相关的辅助脚本，负责把攻击结算与关键技能事件翻译成稳定、可订阅的战报条目。
## 审查重点：重点核对 reason_id / event_tags 等稳定字段是否和命运事件 payload 对齐，以及文案是否能解释门骰、高位威胁与大失败区间。
## 备注：该脚本只产出 battle-local report entry，不负责派发事件，也不改写战斗真相源。

class_name BattleReportFormatter
extends RefCounted

const ProgressionDataUtils = preload("res://scripts/player/progression/progression_data_utils.gd")
const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attribute_service.gd")

const ENTRY_TYPE_FATE_ATTACK: StringName = &"fate_attack_resolution"
const ENTRY_TYPE_SKILL_EVENT: StringName = &"battle_skill_event"

const REASON_CRITICAL_SUCCESS_GATE_DIE: StringName = &"critical_success_gate_die"
const REASON_CRITICAL_SUCCESS_HIGH_THREAT: StringName = &"critical_success_high_threat"
const REASON_ORDINARY_HIT_GATE_DIE_PENDING: StringName = &"ordinary_hit_gate_die_pending"
const REASON_CRITICAL_FAIL_FUMBLE_BAND: StringName = &"critical_fail_fumble_band"
const REASON_ORDINARY_MISS_THRESHOLD: StringName = &"ordinary_miss_threshold"
const REASON_ORDINARY_MISS_FUMBLE_DOWNGRADED: StringName = &"ordinary_miss_fumble_downgraded"
const REASON_DOOM_SENTENCE_APPLIED: StringName = &"doom_sentence_applied"

const TAG_DOOM_SENTENCE: StringName = &"doom_sentence"
const FORTUNE_MARK_TARGET_STAT_ID: StringName = &"fortune_mark_target"


func build_attack_report_entry(attacker, defender, attack_result: Dictionary) -> Dictionary:
	if attack_result.is_empty():
		return {}
	var reason_id := _resolve_attack_reason_id(attack_result)
	if reason_id == &"":
		return {}
	var event_tags := _normalize_string_name_array(attack_result.get("fate_event_tags", []))
	var entry := {
		"entry_type": String(ENTRY_TYPE_FATE_ATTACK),
		"reason_id": String(reason_id),
		"text": "",
		"event_tags": ProgressionDataUtils.string_name_array_to_string_array(event_tags),
		"attacker_id": String(attacker.unit_id) if attacker != null else "",
		"attacker_member_id": String(attacker.source_member_id) if attacker != null else "",
		"attacker_name": String(attacker.display_name) if attacker != null else "",
		"defender_id": String(defender.unit_id) if defender != null else "",
		"defender_member_id": String(defender.source_member_id) if defender != null else "",
		"defender_name": String(defender.display_name) if defender != null else "",
		"defender_is_elite_or_boss": _is_elite_or_boss(defender),
		"attack_resolution": String(ProgressionDataUtils.to_string_name(attack_result.get("attack_resolution", ""))),
		"critical_source": String(ProgressionDataUtils.to_string_name(attack_result.get("critical_source", ""))),
		"is_disadvantage": bool(attack_result.get("is_disadvantage", false)),
		"crit_gate_die": int(attack_result.get("crit_gate_die", 0)),
		"crit_gate_roll": int(attack_result.get("crit_gate_roll", 0)),
		"hit_roll": int(attack_result.get("hit_roll", 0)),
		"required_roll": int(attack_result.get("required_roll", 0)),
		"display_required_roll": int(attack_result.get("display_required_roll", 0)),
		"luck_snapshot": {
			"hidden_luck_at_birth": int(attack_result.get("hidden_luck_at_birth", 0)),
			"faith_luck_bonus": int(attack_result.get("faith_luck_bonus", 0)),
			"effective_luck": int(attack_result.get("effective_luck", 0)),
			"fumble_low_end": int(attack_result.get("fumble_low_end", 0)),
			"crit_threshold": int(attack_result.get("crit_threshold", 0)),
		},
	}
	entry["text"] = _build_attack_report_text(entry)
	return entry


func build_skill_event_entry(attacker, defender, skill_id: StringName, reason_id: StringName, event_tags: Array[StringName]) -> Dictionary:
	var normalized_reason_id := ProgressionDataUtils.to_string_name(reason_id)
	if normalized_reason_id == &"":
		return {}
	var normalized_skill_id := ProgressionDataUtils.to_string_name(skill_id)
	var normalized_tags := _normalize_string_name_array(event_tags)
	var entry := {
		"entry_type": String(ENTRY_TYPE_SKILL_EVENT),
		"reason_id": String(normalized_reason_id),
		"text": "",
		"event_tags": ProgressionDataUtils.string_name_array_to_string_array(normalized_tags),
		"skill_id": String(normalized_skill_id),
		"attacker_id": String(attacker.unit_id) if attacker != null else "",
		"attacker_member_id": String(attacker.source_member_id) if attacker != null else "",
		"attacker_name": String(attacker.display_name) if attacker != null else "",
		"defender_id": String(defender.unit_id) if defender != null else "",
		"defender_member_id": String(defender.source_member_id) if defender != null else "",
		"defender_name": String(defender.display_name) if defender != null else "",
		"defender_is_elite_or_boss": _is_elite_or_boss(defender),
	}
	entry["text"] = _build_skill_event_text(entry)
	return entry


func _resolve_attack_reason_id(attack_result: Dictionary) -> StringName:
	var attack_resolution := ProgressionDataUtils.to_string_name(attack_result.get("attack_resolution", ""))
	var critical_source := ProgressionDataUtils.to_string_name(attack_result.get("critical_source", ""))
	var crit_gate_die := int(attack_result.get("crit_gate_die", 0))
	var hit_roll := int(attack_result.get("hit_roll", 0))
	if attack_resolution == &"critical_hit":
		if critical_source == &"high_threat":
			return REASON_CRITICAL_SUCCESS_HIGH_THREAT
		if critical_source == &"gate_die":
			return REASON_CRITICAL_SUCCESS_GATE_DIE
	if attack_resolution == &"hit" and hit_roll >= 20 and crit_gate_die > 20:
		return REASON_ORDINARY_HIT_GATE_DIE_PENDING
	if attack_resolution == &"miss" and bool(attack_result.get("reverse_fate_downgraded", false)):
		return REASON_ORDINARY_MISS_FUMBLE_DOWNGRADED
	if attack_resolution == &"critical_fail":
		return REASON_CRITICAL_FAIL_FUMBLE_BAND
	if attack_resolution == &"miss":
		return REASON_ORDINARY_MISS_THRESHOLD
	return &""


func _build_attack_report_text(entry: Dictionary) -> String:
	var reason_id := ProgressionDataUtils.to_string_name(entry.get("reason_id", ""))
	var crit_gate_die := int(entry.get("crit_gate_die", 0))
	var crit_gate_roll := int(entry.get("crit_gate_roll", 0))
	var hit_roll := int(entry.get("hit_roll", 0))
	var luck_snapshot_variant = entry.get("luck_snapshot", {})
	var luck_snapshot: Dictionary = luck_snapshot_variant if luck_snapshot_variant is Dictionary else {}
	var fumble_low_end := int(luck_snapshot.get("fumble_low_end", 0))
	var crit_threshold := int(luck_snapshot.get("crit_threshold", 0))
	var text := ""
	match reason_id:
		REASON_CRITICAL_SUCCESS_GATE_DIE:
			text = "命运判定：先掷大成功门骰 d%d=%d/%d，这次大成功来自门骰。" % [
				crit_gate_die,
				crit_gate_roll,
				crit_gate_die,
			]
		REASON_CRITICAL_SUCCESS_HIGH_THREAT:
			text = "命运判定：命中骰 d20=%d 落入高位大成功区 %d-20，这次大成功来自高位威胁。" % [
				hit_roll,
				crit_threshold,
			]
		REASON_ORDINARY_HIT_GATE_DIE_PENDING:
			text = "命运判定：d20=%d 仍只是普通命中；当前大成功门骰为 d%d，必须先中过门骰。" % [
				hit_roll,
				crit_gate_die,
			]
		REASON_CRITICAL_FAIL_FUMBLE_BAND:
			text = "命运判定：d20=%d 落入大失败区间 1-%d，直接判定为大失败。" % [
				hit_roll,
				fumble_low_end,
			]
		REASON_ORDINARY_MISS_FUMBLE_DOWNGRADED:
			text = "命运判定：d20=%d 落入大失败区间 1-%d，但被逆命护符降级为普通 miss。" % [
				hit_roll,
				fumble_low_end,
			]
		REASON_ORDINARY_MISS_THRESHOLD:
			var display_required_roll := int(entry.get("display_required_roll", 0))
			if display_required_roll <= 0:
				display_required_roll = int(entry.get("required_roll", 0))
			var fumble_text := "1-%d" % fumble_low_end if fumble_low_end > 1 else "1"
			text = "命运判定：命中骰 d20=%d 未达到命中线 %d，也不在大失败区 %s，因此只是普通 miss。" % [
				hit_roll,
				display_required_roll,
				fumble_text,
			]
		_:
			text = ""
	return _append_event_tag_suffix(text, entry.get("event_tags", []))


func _build_skill_event_text(entry: Dictionary) -> String:
	var reason_id := ProgressionDataUtils.to_string_name(entry.get("reason_id", ""))
	var attacker_name := String(entry.get("attacker_name", "")).strip_edges()
	var defender_name := String(entry.get("defender_name", "")).strip_edges()
	var actor_label := attacker_name if not attacker_name.is_empty() else "该单位"
	var target_label := defender_name if not defender_name.is_empty() else "目标"
	var text := ""
	match reason_id:
		REASON_DOOM_SENTENCE_APPLIED:
			text = "%s 对 %s 落下厄命宣判。" % [actor_label, target_label]
		_:
			text = ""
	return _append_event_tag_suffix(text, entry.get("event_tags", []))


func _append_event_tag_suffix(text: String, event_tags_variant) -> String:
	var event_tags := _normalize_string_name_array(event_tags_variant)
	if text.is_empty() or event_tags.is_empty():
		return text
	return "%s 事件标签：%s。" % [
		text,
		", ".join(PackedStringArray(ProgressionDataUtils.string_name_array_to_string_array(event_tags))),
	]


func _normalize_string_name_array(values: Variant) -> Array[StringName]:
	var result: Array[StringName] = []
	if values is not Array:
		return result
	for value in values:
		var normalized := ProgressionDataUtils.to_string_name(value)
		if normalized == &"" or result.has(normalized):
			continue
		result.append(normalized)
	return result


func _is_elite_or_boss(unit_state) -> bool:
	if unit_state == null or unit_state.attribute_snapshot == null:
		return false
	return int(unit_state.attribute_snapshot.get_value(FORTUNE_MARK_TARGET_STAT_ID)) > 0
