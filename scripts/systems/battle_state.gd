## 文件说明：该脚本属于战斗状态相关的状态数据脚本，集中维护战斗唯一标识、随机种子、阶段等顶层字段。
## 审查重点：重点核对字段默认值、状态流转顺序、跨系统引用关系以及运行时读写时机是否仍然可靠。
## 备注：后续如果增删字段，需要同步检查调用方、状态同步链路以及历史数据兼容处理。

class_name BattleState
extends RefCounted

const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attribute_service.gd")
const BATTLE_TIMELINE_STATE_SCRIPT = preload("res://scripts/systems/battle_timeline_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle_unit_state.gd")
const MIN_ADJACENT_ENEMIES_FOR_ATTACK_DISADVANTAGE := 2
const LOW_HP_ATTACK_DISADVANTAGE_PERCENT := 30
const LOG_ENTRY_LIMIT := 10000
const LOG_TEXT_BYTE_LIMIT := 10 * 1024 * 1024
const STRONG_ATTACK_DISADVANTAGE_STATUS_IDS := {
	&"blinded": true,
	&"fear": true,
	&"feared": true,
	&"frozen": true,
	&"heavy_fatigue": true,
	&"shocked": true,
	&"staggered": true,
	&"stunned": true,
	&"terrified": true,
	&"exhausted": true,
}

## 字段说明：记录战斗唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var battle_id: StringName = &""
## 字段说明：记录随机种子，会参与运行时状态流转、系统协作和存档恢复。
var seed := 0
## 字段说明：记录攻击检定随机游标，供 battle-seeded 命中判定保持单场战斗内稳定可复现。
var attack_roll_nonce := 0
## 字段说明：记录效果骰随机游标，供 battle-seeded 护盾等技能骰值保持单场战斗内稳定可复现。
var effect_roll_nonce := 0
## 字段说明：记录阶段，会参与运行时状态流转、系统协作和存档恢复。
var phase: StringName = &"timeline_running"
## 字段说明：记录地图尺寸，用于布局、碰撞、绘制或程序化生成时的尺寸计算。
var map_size: Vector2i = Vector2i.ZERO
## 字段说明：记录对象在世界地图中的坐标，供探索定位、遭遇生成和存档恢复复用。
var world_coord: Vector2i = Vector2i.ZERO
## 字段说明：记录遭遇锚点唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var encounter_anchor_id: StringName = &""
## 字段说明：记录地形配置档唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var terrain_profile_id: StringName = &"default"
## 字段说明：记录场景层显式注入的 hardship 标签；这里只接受已判定会导致攻击 disadvantage 的标签。
var attack_disadvantage_tags: Array[StringName] = []
## 字段说明：缓存格子集合字典，集中保存可按键查询的运行时数据。
var cells: Dictionary = {}
## 字段说明：缓存同一 (x, y) 下真实堆叠的格子列集合，作为战场垂直结构的 source-of-truth。
var cell_columns: Dictionary = {}
## 字段说明：缓存单位集合字典，集中保存可按键查询的运行时数据。
var units: Dictionary = {}
## 字段说明：保存友方单位标识列表，便于批量遍历、交叉查找和界面展示。
var ally_unit_ids: Array[StringName] = []
## 字段说明：保存敌方单位标识列表，便于批量遍历、交叉查找和界面展示。
var enemy_unit_ids: Array[StringName] = []
## 字段说明：记录时间轴，会参与运行时状态流转、系统协作和存档恢复。
var timeline = BATTLE_TIMELINE_STATE_SCRIPT.new()
## 字段说明：记录激活单位唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var active_unit_id: StringName = &""
## 字段说明：记录胜利方阵营唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var winner_faction_id: StringName = &""
## 字段说明：保存日志条目列表，便于顺序遍历、批量展示、批量运算和整体重建。
var log_entries: Array[String] = []
## 字段说明：保存结构化战报条目，供 headless 快照、剧情订阅与 UI 解释层读取稳定字段。
var report_entries: Array[Dictionary] = []
## 字段说明：缓存晋升队列字典，集中保存可按键查询的运行时数据。
var promotion_queue: Array[Dictionary] = []
## 字段说明：记录模态状态，会参与运行时状态流转、系统协作和存档恢复。
var modal_state: StringName = &""
## 字段说明：缓存运行时边缘面集合，由边服务按需重建，不直接参与存档恢复。
var runtime_edge_faces: Dictionary = {}
## 字段说明：用于标记运行时边缓存是否脏，供边服务延迟重建和跨系统共享。
var runtime_edges_dirty := true
## 字段说明：缓存当前 battle log 文本字节预算，供 ring buffer 按 10 MiB 上限裁剪。
var _log_text_byte_size := 0


func reset_log_entries(entries: Array[String]) -> void:
	log_entries.clear()
	_log_text_byte_size = 0
	for entry in entries:
		append_log_entry(String(entry))


func clear_log_entries() -> void:
	log_entries.clear()
	_log_text_byte_size = 0


func append_log_entry(entry: String) -> void:
	var normalized_entry := entry.strip_edges()
	if normalized_entry.is_empty():
		return
	log_entries.append(normalized_entry)
	_log_text_byte_size += _estimate_log_text_bytes(normalized_entry)
	_trim_log_entries()


func get_log_text_byte_size() -> int:
	return _log_text_byte_size


func get_log_budget_summary_text() -> String:
	return "%d 条 / %.2f MiB" % [
		log_entries.size(),
		float(_log_text_byte_size) / (1024.0 * 1024.0),
	]


func is_attack_disadvantage(attacker: BattleUnitState, defender: BattleUnitState = null) -> bool:
	if attacker == null or not bool(attacker.is_alive):
		return false
	if defender == attacker:
		return false
	if not attack_disadvantage_tags.is_empty():
		return true
	if _count_adjacent_enemy_units(attacker) >= MIN_ADJACENT_ENEMIES_FOR_ATTACK_DISADVANTAGE:
		return true
	if _is_low_hp_hardship(attacker):
		return true
	return _has_strong_attack_debuff(attacker)


func is_empty() -> bool:
	return battle_id == &"" and cells.is_empty() and units.is_empty() and ally_unit_ids.is_empty() and enemy_unit_ids.is_empty()


func mark_runtime_edges_dirty() -> void:
	runtime_edges_dirty = true


func clear_runtime_edge_faces() -> void:
	runtime_edge_faces.clear()
	runtime_edges_dirty = true


func _trim_log_entries() -> void:
	while log_entries.size() > LOG_ENTRY_LIMIT or _log_text_byte_size > LOG_TEXT_BYTE_LIMIT:
		if log_entries.is_empty():
			_log_text_byte_size = 0
			return
		var removed_entry := String(log_entries[0])
		log_entries.remove_at(0)
		_log_text_byte_size = maxi(_log_text_byte_size - _estimate_log_text_bytes(removed_entry), 0)


func _estimate_log_text_bytes(entry: String) -> int:
	return entry.to_utf8_buffer().size() + 1


func _count_adjacent_enemy_units(attacker: BattleUnitState) -> int:
	if attacker == null:
		return 0
	attacker.refresh_footprint()
	var adjacent_enemy_ids: Dictionary = {}
	for unit_variant in units.values():
		var candidate := unit_variant as BattleUnitState
		if not _is_enemy_unit(attacker, candidate):
			continue
		candidate.refresh_footprint()
		if _are_units_adjacent(attacker, candidate):
			adjacent_enemy_ids[candidate.unit_id] = true
	return adjacent_enemy_ids.size()


func _is_enemy_unit(attacker: BattleUnitState, candidate: BattleUnitState) -> bool:
	if attacker == null or candidate == null:
		return false
	if candidate == attacker or candidate.unit_id == attacker.unit_id:
		return false
	if not bool(candidate.is_alive):
		return false
	var attacker_faction := attacker.faction_id
	var candidate_faction := candidate.faction_id
	if attacker_faction == candidate_faction:
		return false
	return true


func _are_units_adjacent(first_unit: BattleUnitState, second_unit: BattleUnitState) -> bool:
	if first_unit == null or second_unit == null:
		return false
	for first_coord in first_unit.occupied_coords:
		for second_coord in second_unit.occupied_coords:
			if absi(first_coord.x - second_coord.x) + absi(first_coord.y - second_coord.y) == 1:
				return true
	return false


func _is_low_hp_hardship(attacker: BattleUnitState) -> bool:
	if attacker == null or attacker.attribute_snapshot == null:
		return false
	var max_hp := maxi(int(attacker.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX)), 0)
	if max_hp <= 0:
		return false
	return int(attacker.current_hp) * 100 <= max_hp * LOW_HP_ATTACK_DISADVANTAGE_PERCENT


func _has_strong_attack_debuff(attacker: BattleUnitState) -> bool:
	if attacker == null:
		return false
	for status_id in STRONG_ATTACK_DISADVANTAGE_STATUS_IDS.keys():
		if attacker.has_status_effect(StringName(status_id)):
			return true
	return false
