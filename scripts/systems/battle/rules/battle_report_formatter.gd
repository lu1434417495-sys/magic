## 文件说明：该脚本属于战斗战报解释层相关的辅助脚本，负责把攻击结算与关键技能事件翻译成稳定、可订阅的战报条目。
## 审查重点：重点核对 reason_id / event_tags 等稳定字段是否和命运事件 payload 对齐，以及文案是否能解释门骰、高位威胁与大失败区间。
## 备注：该脚本只产出 battle-local report entry，不负责派发事件，也不改写战斗真相源。

class_name BattleReportFormatter
extends RefCounted

const ProgressionDataUtils = preload("res://scripts/player/progression/progression_data_utils.gd")
const BattleEventBatch = preload("res://scripts/systems/battle/core/battle_event_batch.gd")
const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")
const BATTLE_EXECUTION_RULES_SCRIPT = preload("res://scripts/systems/battle/rules/battle_execution_rules.gd")

const ENTRY_TYPE_FATE_ATTACK: StringName = &"fate_attack_resolution"
const ENTRY_TYPE_SKILL_EVENT: StringName = &"battle_skill_event"
const ENTRY_TYPE_METEOR_SWARM_IMPACT: StringName = &"meteor_swarm_impact_summary"

const REASON_CRITICAL_SUCCESS_GATE_DIE: StringName = &"critical_success_gate_die"
const REASON_CRITICAL_SUCCESS_HIGH_THREAT: StringName = &"critical_success_high_threat"
const REASON_ORDINARY_HIT_GATE_DIE_PENDING: StringName = &"ordinary_hit_gate_die_pending"
const REASON_CRITICAL_FAIL_FUMBLE_BAND: StringName = &"critical_fail_fumble_band"
const REASON_ORDINARY_MISS_THRESHOLD: StringName = &"ordinary_miss_threshold"
const REASON_ORDINARY_MISS_FUMBLE_DOWNGRADED: StringName = &"ordinary_miss_fumble_downgraded"
const REASON_DOOM_SENTENCE_APPLIED: StringName = &"doom_sentence_applied"

const TAG_DOOM_SENTENCE: StringName = &"doom_sentence"
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


func format_meteor_swarm_summary(entry: Dictionary) -> Array[String]:
	var lines: Array[String] = []
	if ProgressionDataUtils.to_string_name(entry.get("entry_type", "")) != ENTRY_TYPE_METEOR_SWARM_IMPACT:
		return lines
	var terrain_summary = entry.get("terrain_summary", {})
	var terrain_payload: Dictionary = terrain_summary if terrain_summary is Dictionary else {}
	lines.append("陨星雨覆盖 %d 格，波及 %d 个单位，造成 %d 点总伤害；留下陨坑 %d 格、碎石 %d 格、尘土 %d 格。" % [
		int(terrain_payload.get("affected_coord_count", 0)),
		int(entry.get("target_count", 0)),
		int(entry.get("total_damage", 0)),
		int(terrain_payload.get("crater_count", 0)),
		int(terrain_payload.get("rubble_count", 0)),
		int(terrain_payload.get("dust_count", 0)),
	])
	return lines


func summarize_damage_result(result: Dictionary) -> Dictionary:
	var absorb_labels: Array[String] = []
	var half_source_labels: Array[String] = []
	var double_source_labels: Array[String] = []
	var immune_source_labels: Array[String] = []
	var fixed_mitigation_source_labels: Array[String] = []
	var summary := {
		"damage": int(result.get("damage", 0)),
		"healing": int(result.get("healing", 0)),
		"shield_absorbed": int(result.get("shield_absorbed", 0)),
		"shield_broken": bool(result.get("shield_broken", false)),
		"has_damage_event": false,
		"any_immune": false,
		"any_half": false,
		"any_double": false,
		"fixed_mitigation_total": 0,
		"absorb_labels": absorb_labels,
		"half_source_labels": half_source_labels,
		"double_source_labels": double_source_labels,
		"immune_source_labels": immune_source_labels,
		"fixed_mitigation_source_labels": fixed_mitigation_source_labels,
		"absorb_reason_text": "",
		"fixed_mitigation_source_text": "",
	}
	var damage_events = result.get("damage_events", [])
	if damage_events is Array:
		for event_variant in damage_events:
			if event_variant is not Dictionary:
				continue
			var event := event_variant as Dictionary
			summary["has_damage_event"] = true
			summary["fixed_mitigation_total"] = int(summary.get("fixed_mitigation_total", 0)) + int(event.get("fixed_mitigation_total", 0))
			match ProgressionDataUtils.to_string_name(event.get("mitigation_tier", "")):
				&"immune":
					summary["any_immune"] = true
				&"half":
					summary["any_half"] = true
				&"double":
					summary["any_double"] = true
			_append_damage_mitigation_source_labels(event.get("mitigation_sources", []), half_source_labels, double_source_labels, immune_source_labels)
			_append_damage_fixed_source_labels(event.get("fixed_mitigation_sources", []), fixed_mitigation_source_labels)
			if int(event.get("buff_reduction", 0)) > 0 \
				or int(event.get("passive_reduction", 0)) > 0 \
				or int(event.get("content_dr", 0)) > 0:
				_append_unique_damage_absorb_label(absorb_labels, "减伤")
			if int(event.get("stance_reduction", 0)) > 0 or int(event.get("guard_block", 0)) > 0:
				_append_unique_damage_absorb_label(absorb_labels, "格挡")
	summary["absorb_reason_text"] = build_damage_absorb_reason_text(summary)
	summary["fixed_mitigation_source_text"] = _format_damage_source_labels(fixed_mitigation_source_labels)
	return summary


func build_damage_absorb_reason_text(summary: Dictionary) -> String:
	if bool(summary.get("any_immune", false)):
		return _format_damage_source_labels(summary.get("immune_source_labels", []), "免疫")
	var labels: PackedStringArray = []
	if bool(summary.get("any_half", false)):
		var half_source_text := _format_damage_source_labels(summary.get("half_source_labels", []))
		labels.append(half_source_text if not half_source_text.is_empty() else "减半")
	var absorb_labels = summary.get("absorb_labels", [])
	if absorb_labels is Array:
		if _format_damage_source_labels(summary.get("fixed_mitigation_source_labels", [])).is_empty():
			for label_variant in absorb_labels:
				var label := String(label_variant)
				if label.is_empty():
					continue
				labels.append(label)
	var fixed_source_text := _format_damage_source_labels(summary.get("fixed_mitigation_source_labels", []))
	if not fixed_source_text.is_empty():
		labels.append(fixed_source_text)
	if labels.is_empty():
		return "防护"
	return "、".join(labels)


func append_damage_result_log_lines(
	batch: BattleEventBatch,
	subject_label: String,
	target_display_name: String,
	result: Dictionary
) -> void:
	if batch == null:
		return
	var summary := summarize_damage_result(result)
	if not bool(summary.get("has_damage_event", false)):
		return
	var damage := int(summary.get("damage", 0))
	var shield_absorbed := int(summary.get("shield_absorbed", 0))
	var fixed_mitigation_total := int(summary.get("fixed_mitigation_total", 0))
	if damage > 0:
		var damage_line := "%s 对 %s 造成 %d 点伤害" % [subject_label, target_display_name, damage]
		damage_line += _format_damage_tier_log_suffix(summary)
		batch.log_lines.append("%s。" % damage_line)
		if fixed_mitigation_total > 0:
			var fixed_source_text := String(summary.get("fixed_mitigation_source_text", ""))
			if fixed_source_text.is_empty():
				fixed_source_text = String(summary.get("absorb_reason_text", "防护"))
			batch.log_lines.append("%s 的 %s 吸收了 %d 点伤害。" % [
				target_display_name,
				fixed_source_text,
				fixed_mitigation_total,
			])
		if shield_absorbed > 0:
			batch.log_lines.append("%s 的护盾吸收了 %d 点伤害。" % [target_display_name, shield_absorbed])
	else:
		if bool(summary.get("any_immune", false)):
			var immune_source_text := _format_damage_source_labels(summary.get("immune_source_labels", []))
			if immune_source_text.is_empty():
				batch.log_lines.append("%s 命中 %s，但其免疫该伤害。" % [subject_label, target_display_name])
			else:
				batch.log_lines.append("%s 命中 %s，但其因 %s 免疫该伤害。" % [
					subject_label,
					target_display_name,
					immune_source_text,
				])
		elif shield_absorbed > 0:
			batch.log_lines.append("%s 命中 %s，但被护盾吸收了 %d 点伤害。" % [
				subject_label,
				target_display_name,
				shield_absorbed,
			])
		else:
			batch.log_lines.append("%s 命中 %s，但被 %s 完全吸收。" % [
				subject_label,
				target_display_name,
				String(summary.get("absorb_reason_text", "防护")),
			])
	if bool(summary.get("shield_broken", false)):
		batch.log_lines.append("%s 的护盾被击碎。" % target_display_name)


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


func _append_damage_mitigation_source_labels(
	sources,
	half_source_labels: Array[String],
	double_source_labels: Array[String],
	immune_source_labels: Array[String]
) -> void:
	if sources is not Array:
		return
	for source_variant in sources:
		if source_variant is not Dictionary:
			continue
		var source := source_variant as Dictionary
		var source_label := _format_damage_source_label(source)
		if source_label.is_empty():
			continue
		match ProgressionDataUtils.to_string_name(source.get("tier", "")):
			&"half":
				_append_unique_damage_absorb_label(half_source_labels, source_label)
			&"double":
				_append_unique_damage_absorb_label(double_source_labels, source_label)
			&"immune":
				_append_unique_damage_absorb_label(immune_source_labels, source_label)


func _append_damage_fixed_source_labels(sources, fixed_source_labels: Array[String]) -> void:
	if sources is not Array:
		return
	for source_variant in sources:
		if source_variant is not Dictionary:
			continue
		var source_label := _format_damage_source_label(source_variant as Dictionary)
		if source_label.is_empty():
			continue
		_append_unique_damage_absorb_label(fixed_source_labels, source_label)


func _format_damage_source_label(source: Dictionary) -> String:
	var status_id := String(source.get("status_id", ""))
	if not status_id.is_empty():
		return status_id
	return String(source.get("type", ""))


func _format_damage_source_labels(labels_variant, fallback: String = "") -> String:
	if labels_variant is not Array:
		return fallback
	var labels := PackedStringArray()
	for label_variant in labels_variant:
		var label := String(label_variant)
		if label.is_empty() or labels.has(label):
			continue
		labels.append(label)
	if labels.is_empty():
		return fallback
	return "、".join(labels)


func _format_damage_tier_log_suffix(summary: Dictionary) -> String:
	if bool(summary.get("any_double", false)):
		var double_source_text := _format_damage_source_labels(summary.get("double_source_labels", []))
		if not double_source_text.is_empty():
			return "（因 %s 触发易伤）" % double_source_text
		return "（触发易伤）"
	if bool(summary.get("any_half", false)):
		var half_source_text := _format_damage_source_labels(summary.get("half_source_labels", []))
		if not half_source_text.is_empty():
			return "（因 %s 减半后结算）" % half_source_text
		return "（减半后结算）"
	return ""


func _append_unique_damage_absorb_label(absorb_labels: Array[String], label: String) -> void:
	if label.is_empty() or absorb_labels.has(label):
		return
	absorb_labels.append(label)


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
	return BATTLE_EXECUTION_RULES_SCRIPT.is_elite_or_boss_target(unit_state)
