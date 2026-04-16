## 文件说明：该脚本属于战斗地形规则相关的服务脚本，集中维护地形类型、默认通行性和移动标签修正。
## 审查重点：重点核对 terrain id 兼容映射、默认规则与单位移动标签之间的优先级是否稳定。
## 备注：这里是纯规则表，不持有 battle state，也不直接修改运行时对象。

class_name BattleTerrainRules
extends RefCounted

const TERRAIN_LAND := &"land"
const TERRAIN_FOREST := &"forest"
const TERRAIN_WATER := &"water"
const TERRAIN_SHALLOW_WATER := &"shallow_water"
const TERRAIN_FLOWING_WATER := &"flowing_water"
const TERRAIN_DEEP_WATER := &"deep_water"
const TERRAIN_MUD := &"mud"
const TERRAIN_SPIKE := &"spike"

const TAG_WADE := &"wade"
const TAG_AMPHIBIOUS := &"amphibious"
const TAG_FLY := &"fly"

const STILL_FLOW := Vector2i.ZERO


static func normalize_terrain_id(terrain_id: StringName) -> StringName:
	match terrain_id:
		&"", TERRAIN_LAND:
			return TERRAIN_LAND
		_:
			return terrain_id


static func is_water_terrain(terrain_id: StringName) -> bool:
	match normalize_terrain_id(terrain_id):
		TERRAIN_SHALLOW_WATER, TERRAIN_FLOWING_WATER, TERRAIN_DEEP_WATER:
			return true
		_:
			return false


static func get_global_passable(terrain_id: StringName) -> bool:
	return normalize_terrain_id(terrain_id) != TERRAIN_DEEP_WATER


static func get_base_move_cost(terrain_id: StringName) -> int:
	match normalize_terrain_id(terrain_id):
		TERRAIN_MUD, TERRAIN_SPIKE, TERRAIN_SHALLOW_WATER:
			return 2
		TERRAIN_FLOWING_WATER:
			return 3
		TERRAIN_DEEP_WATER:
			return 2
		_:
			return 1


static func can_unit_enter_terrain(terrain_id: StringName, movement_tags: Array[StringName] = []) -> bool:
	if has_movement_tag(movement_tags, TAG_FLY):
		return true
	match normalize_terrain_id(terrain_id):
		TERRAIN_DEEP_WATER:
			return has_movement_tag(movement_tags, TAG_AMPHIBIOUS)
		_:
			return true


static func get_unit_move_cost(terrain_id: StringName, movement_tags: Array[StringName] = []) -> int:
	var normalized := normalize_terrain_id(terrain_id)
	if has_movement_tag(movement_tags, TAG_FLY) and is_water_terrain(normalized):
		return 1

	match normalized:
		TERRAIN_SHALLOW_WATER:
			if has_movement_tag(movement_tags, TAG_AMPHIBIOUS) or has_movement_tag(movement_tags, TAG_WADE):
				return 1
			return 2
		TERRAIN_FLOWING_WATER:
			if has_movement_tag(movement_tags, TAG_AMPHIBIOUS):
				return 1
			if has_movement_tag(movement_tags, TAG_WADE):
				return 2
			return 3
		TERRAIN_DEEP_WATER:
			return 2 if has_movement_tag(movement_tags, TAG_AMPHIBIOUS) else 999999
		_:
			return get_base_move_cost(normalized)


static func get_display_name(terrain_id: StringName) -> String:
	match normalize_terrain_id(terrain_id):
		TERRAIN_LAND:
			return "陆地"
		TERRAIN_FOREST:
			return "森林"
		TERRAIN_SHALLOW_WATER:
			return "浅水"
		TERRAIN_FLOWING_WATER:
			return "流水"
		TERRAIN_DEEP_WATER:
			return "深水"
		TERRAIN_MUD:
			return "泥沼"
		TERRAIN_SPIKE:
			return "地刺"
		_:
			return String(terrain_id)


static func can_host_tent(terrain_id: StringName) -> bool:
	var normalized := normalize_terrain_id(terrain_id)
	return normalized == TERRAIN_LAND or normalized == TERRAIN_FOREST


static func can_host_torch(terrain_id: StringName) -> bool:
	var normalized := normalize_terrain_id(terrain_id)
	return not is_water_terrain(normalized) and normalized != TERRAIN_SPIKE


static func is_safe_terrain(terrain_id: StringName) -> bool:
	var normalized := normalize_terrain_id(terrain_id)
	return normalized == TERRAIN_LAND or normalized == TERRAIN_FOREST


static func has_movement_tag(movement_tags: Array[StringName], tag: StringName) -> bool:
	return movement_tags.has(tag)
