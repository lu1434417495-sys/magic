class_name LowLuckRelicRules
extends RefCounted

const ITEM_REVERSE_FATE_AMULET: StringName = &"reverse_fate_amulet"
const ITEM_BLACK_STAR_WEDGE: StringName = &"black_star_wedge"
const ITEM_BLOOD_DEBT_SHAWL: StringName = &"blood_debt_shawl"
const ITEM_DEAD_ROAD_LANTERN: StringName = &"dead_road_lantern"

const ATTR_REVERSE_FATE_AMULET: StringName = &"low_luck_reverse_fate_amulet"
const ATTR_BLACK_STAR_WEDGE: StringName = &"low_luck_black_star_wedge"
const ATTR_BLOOD_DEBT_SHAWL: StringName = &"low_luck_blood_debt_shawl"
const ATTR_DEAD_ROAD_LANTERN: StringName = &"low_luck_dead_road_lantern"

const STATUS_REVERSE_FATE_WEAKENED: StringName = &"low_luck_reverse_fate_weakened"
const STATUS_BLACK_STAR_WEDGE_EXPOSED: StringName = &"low_luck_black_star_wedge_exposed"

const BATTLE_FLAG_REVERSE_FATE_USED := "low_luck_reverse_fate_used"
const BATTLE_FLAG_BLACK_STAR_WEDGE_USED := "low_luck_black_star_wedge_used"

const REVERSE_FATE_DURATION_TU := 120
const REVERSE_FATE_DAMAGE_MULTIPLIER := 0.75
const BLACK_STAR_WEDGE_GUARD_IGNORE_FLAT := 4
const BLACK_STAR_WEDGE_EXPOSED_DURATION_TU := 60
const BLACK_STAR_WEDGE_EXPOSED_INCOMING_DAMAGE_MULTIPLIER := 1.25
const BLOOD_DEBT_LOW_HP_THRESHOLD_RATIO := 0.5
const BLOOD_DEBT_DAMAGE_MULTIPLIER := 0.75
const BLOOD_DEBT_RECOVERY_MULTIPLIER := 0.5
const BLOOD_DEBT_ALLY_DOWN_AP_GAIN := 1

const PATH_TAG_HIDDEN_TRAP: StringName = &"hidden_trap"
const PATH_TAG_BLACK_MARKET: StringName = &"black_market"
const PATH_TAG_BLACK_OMEN: StringName = &"black_omen"
const PATH_TAG_HIDDEN_PATH: StringName = &"hidden_path"

const VISIBLE_PATH_TAGS: Array[StringName] = [
	PATH_TAG_HIDDEN_TRAP,
	PATH_TAG_BLACK_MARKET,
	PATH_TAG_BLACK_OMEN,
	PATH_TAG_HIDDEN_PATH,
]


static func snapshot_has_flag(attribute_snapshot, attribute_id: StringName) -> bool:
	return attribute_snapshot != null and attribute_id != &"" and int(attribute_snapshot.get_value(attribute_id)) > 0


static func unit_has_flag(unit_state, attribute_id: StringName) -> bool:
	if unit_state == null:
		return false
	return snapshot_has_flag(unit_state.attribute_snapshot, attribute_id)


static func normalize_path_tags(path_tags_variant: Variant) -> Array[StringName]:
	var normalized_tags: Array[StringName] = []
	if path_tags_variant is not Array:
		return normalized_tags
	for path_tag_variant in path_tags_variant:
		var path_tag := ProgressionDataUtils.to_string_name(path_tag_variant)
		if path_tag == &"" or normalized_tags.has(path_tag):
			continue
		normalized_tags.append(path_tag)
	return normalized_tags


static func should_reveal_hidden_path(attribute_snapshot, path_tags_variant: Variant) -> bool:
	if not snapshot_has_flag(attribute_snapshot, ATTR_DEAD_ROAD_LANTERN):
		return false
	for path_tag in normalize_path_tags(path_tags_variant):
		if VISIBLE_PATH_TAGS.has(path_tag):
			return true
	return false


static func member_has_item(item_defs: Dictionary, member_state, item_id: StringName) -> bool:
	if member_state == null or item_id == &"" or member_state.equipment_state == null:
		return false
	if not (member_state.equipment_state is Object and member_state.equipment_state.has_method("get_entry_slot_ids")):
		return false
	for entry_slot_id in ProgressionDataUtils.to_string_name_array(member_state.equipment_state.get_entry_slot_ids()):
		var equipped_item_id := ProgressionDataUtils.to_string_name(
			member_state.equipment_state.get_equipped_item_id(entry_slot_id)
		)
		if equipped_item_id == item_id:
			return true
	return false
