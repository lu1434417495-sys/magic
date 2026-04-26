## 文件说明：该脚本属于战斗单位状态相关的状态数据脚本，集中维护单位唯一标识、来源成员唯一标识、显示名称等顶层字段。
## 审查重点：重点核对字段默认值、状态流转顺序、跨系统引用关系以及运行时读写时机是否仍然可靠。
## 备注：后续如果增删字段，需要同步检查调用方、状态同步链路以及历史数据兼容处理。

class_name BattleUnitState
extends RefCounted

const BATTLE_UNIT_STATE_SCRIPT = preload("res://scripts/systems/battle_unit_state.gd")
const BATTLE_STATUS_EFFECT_STATE_SCRIPT = preload("res://scripts/systems/battle_status_effect_state.gd")
const AttributeSnapshot = preload("res://scripts/player/progression/attribute_snapshot.gd")
const BattleStatusEffectState = preload("res://scripts/systems/battle_status_effect_state.gd")
const DEFAULT_MOVE_POINTS_PER_TURN := 2
const DEFAULT_ACTION_THRESHOLD := 120
const WEAPON_PROFILE_KIND_NONE: StringName = &"none"
const WEAPON_PROFILE_KIND_UNARMED: StringName = &"unarmed"
const WEAPON_PROFILE_KIND_NATURAL: StringName = &"natural"
const WEAPON_PROFILE_KIND_EQUIPPED: StringName = &"equipped"
const WEAPON_GRIP_NONE: StringName = &"none"
const WEAPON_GRIP_ONE_HANDED: StringName = &"one_handed"
const WEAPON_GRIP_TWO_HANDED: StringName = &"two_handed"

## 字段说明：记录单位唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var unit_id: StringName = &""
## 字段说明：记录来源成员唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var source_member_id: StringName = &""
## 字段说明：记录敌方模板唯一标识，供战斗内按击杀生成掉落、任务统计与日志归因使用。
var enemy_template_id: StringName = &""
## 字段说明：用于界面展示的名称文本，主要服务于玩家阅读和调试观察，不直接参与数值判定。
var display_name: String = ""
## 字段说明：记录阵营唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var faction_id: StringName = &""
## 字段说明：记录控制模式，用于在不同处理分支之间切换规则或交互方式。
var control_mode: StringName = &"manual"
## 字段说明：记录单位绑定的 AI brain 标识，供战斗运行时选择状态机与 action 集合。
var ai_brain_id: StringName = &""
## 字段说明：记录单位当前 AI 状态标识，供同一场战斗内持续保留战术状态。
var ai_state_id: StringName = &""
## 字段说明：缓存 AI 临时黑板字典，供单场战斗内的决策链路共享运行时上下文。
var ai_blackboard: Dictionary = {}
## 字段说明：记录对象当前使用的网格坐标，供绘制、寻路或占位计算使用。
var coord: Vector2i = Vector2i.ZERO
## 字段说明：记录体型尺寸，用于布局、碰撞、绘制或程序化生成时的尺寸计算。
var body_size := 1
## 字段说明：记录占位尺寸，用于布局、碰撞、绘制或程序化生成时的尺寸计算。
var footprint_size: Vector2i = Vector2i.ONE
## 字段说明：保存占用坐标列表，供范围判定、占位刷新、批量渲染或目标选择复用。
var occupied_coords: Array[Vector2i] = []
## 字段说明：用于标记当前是否处于存活状态，避免在不合适的时机重复触发流程，会参与运行时状态流转、系统协作和存档恢复。
var is_alive := true
## 字段说明：缓存属性快照实例，会参与运行时状态流转、系统协作和存档恢复。
var attribute_snapshot: AttributeSnapshot = AttributeSnapshot.new()
## 字段说明：记录当前生命值，会参与运行时状态流转、系统协作和存档恢复。
var current_hp := 0
## 字段说明：记录当前法力值，会参与运行时状态流转、系统协作和存档恢复。
var current_mp := 0
## 字段说明：记录当前体力值，会参与运行时状态流转、系统协作和存档恢复。
var current_stamina := 0
## 字段说明：记录当前斗气值，会参与运行时状态流转、系统协作和存档恢复。
var current_aura := 0
## 字段说明：记录当前行动点，会参与运行时状态流转、系统协作和存档恢复。
var current_ap := 0
## 字段说明：记录当前剩余行动点，仅供普通移动链路消耗，不再与技能 AP 共用预算。
var current_move_points := DEFAULT_MOVE_POINTS_PER_TURN
## 字段说明：记录当前剩余护盾值，供伤害在进入生命前优先吸收。
var current_shield_hp := 0
## 字段说明：记录当前护盾池的原始最大值，供刷新、日志与调试使用。
var shield_max_hp := 0
## 字段说明：记录当前护盾剩余持续时间，单位沿用 battle timeline 的 TU。
var shield_duration := -1
## 字段说明：记录当前护盾家族键，用于同类护盾刷新与互斥判断。
var shield_family: StringName = &""
## 字段说明：记录当前护盾来源单位，供日志和后续扩展使用。
var shield_source_unit_id: StringName = &""
## 字段说明：记录当前护盾来源技能，供日志和后续扩展使用。
var shield_source_skill_id: StringName = &""
## 字段说明：为护盾扩展表现或附加参数预留字典，M1 不承载核心数值。
var shield_params: Dictionary = {}
## 字段说明：记录当前回合免费移动额度，用于承接击杀刷新等临时机动收益。
var current_free_move_points := 0
## 字段说明：保存行动进度，便于顺序遍历、批量展示、批量运算和整体重建。
var action_progress := 0
## 字段说明：记录该单位进入行动队列所需的 TU 阈值。
var action_threshold := DEFAULT_ACTION_THRESHOLD
## 字段说明：保存已知激活技能标识列表，便于批量遍历、交叉查找和界面展示。
var known_active_skill_ids: Array[StringName] = []
## 字段说明：按键缓存已知技能等级映射表，便于在较多对象中快速定位目标并减少重复遍历。
var known_skill_level_map: Dictionary = {}
## 字段说明：记录单位移动标签，供战斗网格规则按地形动态修正通行性与移动消耗。
var movement_tags: Array[StringName] = []
## 字段说明：记录武器投影来源，区分无武器、空手、天生武器和装备武器。
var weapon_profile_kind: StringName = WEAPON_PROFILE_KIND_NONE
## 字段说明：记录当前装备武器的物品标识；空手、天生武器或无武器时保持为空。
var weapon_item_id: StringName = &""
## 字段说明：记录当前 weapon profile 的类型标识，例如 shortsword / greatsword / unarmed。
var weapon_profile_type_id: StringName = &""
## 字段说明：记录当前武器使用的握法，供后续根据一手骰 / 双手骰选择伤害模板。
var weapon_current_grip: StringName = WEAPON_GRIP_NONE
## 字段说明：记录当前武器攻击范围，不再依赖 attribute snapshot 中的旧 weapon_attack_range 字段。
var weapon_attack_range := 0
## 字段说明：记录当前武器的一手伤害骰投影，格式为 dice_count / dice_sides / flat_bonus。
var weapon_one_handed_dice: Dictionary = {}
## 字段说明：记录当前武器的双手伤害骰投影，格式为 dice_count / dice_sides / flat_bonus。
var weapon_two_handed_dice: Dictionary = {}
## 字段说明：记录当前武器是否具备 versatile 属性，用于后续根据握法选择骰面。
var weapon_is_versatile := false
## 字段说明：记录当前主手武器的唯一物理伤害类型，供武器近战技能在结算时实时覆盖伤害标签。
var weapon_physical_damage_tag: StringName = &""
## 字段说明：缓存冷却表字典，集中保存可按键查询的运行时数据。
var cooldowns: Dictionary = {}
## 字段说明：记录该单位上一次进入行动窗口时的时间轴 TU，用于把 cooldown_tu 的正式递减锚定到 battle timeline。
var last_turn_tu := -1
## 字段说明：缓存状态效果集合字典，内部 value 使用 BattleStatusEffectState。
var status_effects: Dictionary = {}
## 字段说明：缓存连击态字典，集中保存可按键查询的运行时数据。
var combo_state: Dictionary = {}


func _init() -> void:
	refresh_footprint()


func set_anchor_coord(anchor_coord: Vector2i) -> void:
	coord = anchor_coord
	refresh_footprint()


func refresh_footprint() -> void:
	footprint_size = get_footprint_size_for_body_size(body_size)
	occupied_coords = []
	for y in range(footprint_size.y):
		for x in range(footprint_size.x):
			occupied_coords.append(coord + Vector2i(x, y))


func occupies_coord(target_coord: Vector2i) -> bool:
	return occupied_coords.has(target_coord)


func has_movement_tag(tag: StringName) -> bool:
	return movement_tags.has(tag)


func has_status_effect(status_id: StringName) -> bool:
	return get_status_effect(status_id) != null


func has_shield() -> bool:
	return current_shield_hp > 0 and shield_max_hp > 0 and shield_duration > 0


func get_aura_max() -> int:
	return attribute_snapshot.get_value(&"aura_max") if attribute_snapshot != null else 0


func clear_shield() -> void:
	current_shield_hp = 0
	shield_max_hp = 0
	shield_duration = -1
	shield_family = &""
	shield_source_unit_id = &""
	shield_source_skill_id = &""
	shield_params = {}


func normalize_shield_state() -> void:
	if current_shield_hp <= 0 or shield_max_hp <= 0 or shield_duration <= 0:
		clear_shield()
		return
	shield_max_hp = maxi(shield_max_hp, 1)
	current_shield_hp = clampi(current_shield_hp, 0, shield_max_hp)
	if current_shield_hp <= 0:
		clear_shield()


func clear_weapon_projection() -> void:
	weapon_profile_kind = WEAPON_PROFILE_KIND_NONE
	weapon_item_id = &""
	weapon_profile_type_id = &""
	weapon_current_grip = WEAPON_GRIP_NONE
	weapon_attack_range = 0
	weapon_one_handed_dice = {}
	weapon_two_handed_dice = {}
	weapon_is_versatile = false
	weapon_physical_damage_tag = &""


func set_unarmed_weapon_projection(
	damage_tag: StringName = &"physical_blunt",
	dice: Dictionary = {"dice_count": 1, "dice_sides": 1, "flat_bonus": 0},
	attack_range: int = 1
) -> void:
	apply_weapon_projection({
		"weapon_profile_kind": String(WEAPON_PROFILE_KIND_UNARMED),
		"weapon_profile_type_id": "unarmed",
		"weapon_current_grip": String(WEAPON_GRIP_ONE_HANDED),
		"weapon_attack_range": attack_range,
		"weapon_one_handed_dice": dice,
		"weapon_physical_damage_tag": String(damage_tag),
	})


func set_natural_weapon_projection(
	profile_type_id: StringName,
	damage_tag: StringName,
	attack_range: int,
	dice: Dictionary = {}
) -> void:
	apply_weapon_projection({
		"weapon_profile_kind": String(WEAPON_PROFILE_KIND_NATURAL),
		"weapon_profile_type_id": String(profile_type_id),
		"weapon_current_grip": String(WEAPON_GRIP_ONE_HANDED if attack_range > 0 else WEAPON_GRIP_NONE),
		"weapon_attack_range": attack_range,
		"weapon_one_handed_dice": dice,
		"weapon_physical_damage_tag": String(damage_tag),
	})


func apply_weapon_projection(projection: Dictionary) -> void:
	if projection.is_empty():
		clear_weapon_projection()
		return
	weapon_profile_kind = _normalize_weapon_profile_kind(projection.get("weapon_profile_kind", WEAPON_PROFILE_KIND_NONE))
	weapon_item_id = ProgressionDataUtils.to_string_name(projection.get("weapon_item_id", ""))
	weapon_profile_type_id = ProgressionDataUtils.to_string_name(projection.get("weapon_profile_type_id", ""))
	weapon_current_grip = _normalize_weapon_grip(projection.get("weapon_current_grip", WEAPON_GRIP_NONE))
	weapon_attack_range = maxi(int(projection.get("weapon_attack_range", 0)), 0)
	weapon_one_handed_dice = _normalize_weapon_dice(projection.get("weapon_one_handed_dice", {}))
	weapon_two_handed_dice = _normalize_weapon_dice(projection.get("weapon_two_handed_dice", {}))
	weapon_is_versatile = bool(projection.get("weapon_is_versatile", false))
	weapon_physical_damage_tag = ProgressionDataUtils.to_string_name(projection.get("weapon_physical_damage_tag", ""))
	if weapon_profile_kind == WEAPON_PROFILE_KIND_NONE:
		clear_weapon_projection()
		return
	if weapon_attack_range <= 0:
		weapon_current_grip = WEAPON_GRIP_NONE


func get_weapon_attack_range() -> int:
	return maxi(int(weapon_attack_range), 0)


func get_status_effect(status_id: StringName):
	var normalized := ProgressionDataUtils.to_string_name(status_id)
	if normalized == &"" or not status_effects.has(normalized):
		return null
	var effect_variant: Variant = status_effects.get(normalized)
	var effect_state: Variant = effect_variant if effect_variant is BattleStatusEffectState else null
	if effect_state != null and not effect_state.is_empty():
		return effect_state
	effect_state = BATTLE_STATUS_EFFECT_STATE_SCRIPT.from_dict(effect_variant, normalized)
	if effect_state == null or effect_state.is_empty():
		status_effects.erase(normalized)
		return null
	status_effects[normalized] = effect_state
	return effect_state


func set_status_effect(effect_state: BattleStatusEffectState) -> void:
	if effect_state == null or effect_state.is_empty():
		return
	status_effects[effect_state.status_id] = effect_state


func erase_status_effect(status_id: StringName) -> void:
	var normalized := ProgressionDataUtils.to_string_name(status_id)
	if normalized != &"":
		status_effects.erase(normalized)


static func get_footprint_size_for_body_size(size_value: int) -> Vector2i:
	return Vector2i(2, 2) if maxi(size_value, 1) >= 3 else Vector2i.ONE


func to_dict() -> Dictionary:
	refresh_footprint()
	normalize_shield_state()
	apply_weapon_projection(_build_current_weapon_projection_payload())
	var status_payloads: Dictionary = {}
	for status_id_str in ProgressionDataUtils.sorted_string_keys(status_effects):
		var status_id := StringName(status_id_str)
		var effect_state = get_status_effect(status_id)
		if effect_state == null:
			continue
		status_payloads[status_id_str] = effect_state.to_dict()
	return {
		"unit_id": String(unit_id),
		"source_member_id": String(source_member_id),
		"enemy_template_id": String(enemy_template_id),
		"display_name": display_name,
		"faction_id": String(faction_id),
		"control_mode": String(control_mode),
		"ai_brain_id": String(ai_brain_id),
		"ai_state_id": String(ai_state_id),
		"ai_blackboard": ai_blackboard.duplicate(true),
		"coord": coord,
		"body_size": body_size,
		"footprint_size": footprint_size,
		"occupied_coords": occupied_coords.duplicate(),
		"is_alive": is_alive,
		"attribute_snapshot": attribute_snapshot.to_dict() if attribute_snapshot != null else {},
		"current_hp": current_hp,
		"current_mp": current_mp,
		"current_stamina": current_stamina,
		"current_aura": current_aura,
		"aura_max": get_aura_max(),
		"current_ap": current_ap,
		"current_move_points": current_move_points,
		"current_shield_hp": current_shield_hp,
		"shield_max_hp": shield_max_hp,
		"shield_duration": shield_duration,
		"shield_family": String(shield_family),
		"shield_source_unit_id": String(shield_source_unit_id),
		"shield_source_skill_id": String(shield_source_skill_id),
		"shield_params": shield_params.duplicate(true),
		"current_free_move_points": current_free_move_points,
		"action_progress": action_progress,
		"action_threshold": action_threshold,
		"known_active_skill_ids": _string_name_array_to_strings(known_active_skill_ids),
		"known_skill_level_map": ProgressionDataUtils.string_name_int_map_to_string_dict(known_skill_level_map),
		"movement_tags": _string_name_array_to_strings(movement_tags),
		"weapon_profile_kind": String(weapon_profile_kind),
		"weapon_item_id": String(weapon_item_id),
		"weapon_profile_type_id": String(weapon_profile_type_id),
		"weapon_current_grip": String(weapon_current_grip),
		"weapon_attack_range": weapon_attack_range,
		"weapon_one_handed_dice": weapon_one_handed_dice.duplicate(true),
		"weapon_two_handed_dice": weapon_two_handed_dice.duplicate(true),
		"weapon_is_versatile": weapon_is_versatile,
		"weapon_physical_damage_tag": String(weapon_physical_damage_tag),
		"cooldowns": cooldowns.duplicate(true),
		"last_turn_tu": last_turn_tu,
		"status_effects": status_payloads,
		"combo_state": combo_state.duplicate(true),
	}


static func from_dict(data: Dictionary):
	var unit_state = BATTLE_UNIT_STATE_SCRIPT.new()
	unit_state.unit_id = StringName(String(data.get("unit_id", "")))
	unit_state.source_member_id = StringName(String(data.get("source_member_id", "")))
	unit_state.enemy_template_id = StringName(String(data.get("enemy_template_id", "")))
	unit_state.display_name = String(data.get("display_name", ""))
	unit_state.faction_id = StringName(String(data.get("faction_id", "")))
	unit_state.control_mode = StringName(String(data.get("control_mode", "manual")))
	unit_state.ai_brain_id = StringName(String(data.get("ai_brain_id", "")))
	unit_state.ai_state_id = StringName(String(data.get("ai_state_id", "")))
	unit_state.ai_blackboard = data.get("ai_blackboard", {}).duplicate(true)
	unit_state.coord = data.get("coord", Vector2i.ZERO)
	unit_state.body_size = maxi(int(data.get("body_size", 1)), 1)
	unit_state.is_alive = bool(data.get("is_alive", true))
	unit_state.attribute_snapshot = _attribute_snapshot_from_dict(data.get("attribute_snapshot", {}))
	unit_state.current_hp = int(data.get("current_hp", 0))
	unit_state.current_mp = int(data.get("current_mp", 0))
	unit_state.current_stamina = int(data.get("current_stamina", 0))
	unit_state.current_aura = int(data.get("current_aura", 0))
	if data.has("aura_max") and unit_state.attribute_snapshot != null:
		unit_state.attribute_snapshot.set_value(&"aura_max", maxi(int(data.get("aura_max", 0)), 0))
	unit_state.current_ap = int(data.get("current_ap", 0))
	unit_state.current_move_points = clampi(
		int(data.get("current_move_points", DEFAULT_MOVE_POINTS_PER_TURN)),
		0,
		DEFAULT_MOVE_POINTS_PER_TURN
	)
	unit_state.current_shield_hp = int(data.get("current_shield_hp", 0))
	unit_state.shield_max_hp = int(data.get("shield_max_hp", 0))
	unit_state.shield_duration = int(data.get("shield_duration", -1))
	unit_state.shield_family = StringName(String(data.get("shield_family", "")))
	unit_state.shield_source_unit_id = StringName(String(data.get("shield_source_unit_id", "")))
	unit_state.shield_source_skill_id = StringName(String(data.get("shield_source_skill_id", "")))
	unit_state.shield_params = data.get("shield_params", {}).duplicate(true) if data.get("shield_params", {}) is Dictionary else {}
	unit_state.current_free_move_points = int(data.get("current_free_move_points", 0))
	unit_state.action_progress = int(data.get("action_progress", 0))
	unit_state.action_threshold = int(data.get("action_threshold", DEFAULT_ACTION_THRESHOLD))
	unit_state.known_active_skill_ids = _strings_to_string_name_array(data.get("known_active_skill_ids", []))
	unit_state.known_skill_level_map = ProgressionDataUtils.to_string_name_int_map(data.get("known_skill_level_map", {}))
	unit_state.movement_tags = _strings_to_string_name_array(data.get("movement_tags", []))
	unit_state.apply_weapon_projection({
		"weapon_profile_kind": data.get("weapon_profile_kind", WEAPON_PROFILE_KIND_NONE),
		"weapon_item_id": data.get("weapon_item_id", ""),
		"weapon_profile_type_id": data.get("weapon_profile_type_id", ""),
		"weapon_current_grip": data.get("weapon_current_grip", WEAPON_GRIP_NONE),
		"weapon_attack_range": data.get("weapon_attack_range", 0),
		"weapon_one_handed_dice": data.get("weapon_one_handed_dice", {}),
		"weapon_two_handed_dice": data.get("weapon_two_handed_dice", {}),
		"weapon_is_versatile": data.get("weapon_is_versatile", false),
		"weapon_physical_damage_tag": data.get("weapon_physical_damage_tag", ""),
	})
	unit_state.cooldowns = data.get("cooldowns", {}).duplicate(true)
	unit_state.last_turn_tu = int(data.get("last_turn_tu", -1))
	unit_state.status_effects = _status_effects_from_dict(data.get("status_effects", {}))
	unit_state.combo_state = data.get("combo_state", {}).duplicate(true)
	unit_state.normalize_shield_state()
	unit_state.refresh_footprint()
	return unit_state


static func _attribute_snapshot_from_dict(data: Variant) -> AttributeSnapshot:
	var snapshot = AttributeSnapshot.new()
	if data is Dictionary:
		for key in data.keys():
			snapshot.set_value(StringName(String(key)), int(data[key]))
	return snapshot


static func _status_effects_from_dict(data: Variant) -> Dictionary:
	var results: Dictionary = {}
	if data is not Dictionary:
		return results
	for status_key in data.keys():
		var status_id := ProgressionDataUtils.to_string_name(status_key)
		var effect_state = BATTLE_STATUS_EFFECT_STATE_SCRIPT.from_dict(data.get(status_key), status_id)
		if effect_state == null or effect_state.is_empty():
			continue
		results[effect_state.status_id] = effect_state
	return results


func _build_current_weapon_projection_payload() -> Dictionary:
	return {
		"weapon_profile_kind": weapon_profile_kind,
		"weapon_item_id": weapon_item_id,
		"weapon_profile_type_id": weapon_profile_type_id,
		"weapon_current_grip": weapon_current_grip,
		"weapon_attack_range": weapon_attack_range,
		"weapon_one_handed_dice": weapon_one_handed_dice,
		"weapon_two_handed_dice": weapon_two_handed_dice,
		"weapon_is_versatile": weapon_is_versatile,
		"weapon_physical_damage_tag": weapon_physical_damage_tag,
	}


static func _normalize_weapon_profile_kind(value: Variant) -> StringName:
	var normalized := ProgressionDataUtils.to_string_name(value)
	match normalized:
		WEAPON_PROFILE_KIND_UNARMED, WEAPON_PROFILE_KIND_NATURAL, WEAPON_PROFILE_KIND_EQUIPPED:
			return normalized
		_:
			return WEAPON_PROFILE_KIND_NONE


static func _normalize_weapon_grip(value: Variant) -> StringName:
	var normalized := ProgressionDataUtils.to_string_name(value)
	match normalized:
		WEAPON_GRIP_ONE_HANDED, WEAPON_GRIP_TWO_HANDED:
			return normalized
		_:
			return WEAPON_GRIP_NONE


static func _normalize_weapon_dice(value: Variant) -> Dictionary:
	if value is not Dictionary:
		return {}
	var dice_data := value as Dictionary
	var dice_count := int(dice_data.get("dice_count", 0))
	var dice_sides := int(dice_data.get("dice_sides", 0))
	if dice_count <= 0 or dice_sides <= 0:
		return {}
	return {
		"dice_count": dice_count,
		"dice_sides": dice_sides,
		"flat_bonus": int(dice_data.get("flat_bonus", 0)),
	}


static func _string_name_array_to_strings(values: Array[StringName]) -> Array[String]:
	var results: Array[String] = []
	for value in values:
		results.append(String(value))
	return results


static func _strings_to_string_name_array(values: Variant) -> Array[StringName]:
	var results: Array[StringName] = []
	if values is Array:
		for value in values:
			results.append(StringName(String(value)))
	return results
