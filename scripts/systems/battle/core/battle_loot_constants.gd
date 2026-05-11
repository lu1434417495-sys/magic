class_name BattleLootConstants
extends RefCounted

const DROP_TYPE_ITEM: StringName = &"item"
const DROP_TYPE_RANDOM_EQUIPMENT: StringName = &"random_equipment"
const DROP_TYPE_EQUIPMENT_INSTANCE: StringName = &"equipment_instance"

const SOURCE_KIND_ENEMY_UNIT: StringName = &"enemy_unit"
const SOURCE_KIND_CALAMITY_CONVERSION: StringName = &"calamity_conversion"
const SOURCE_KIND_FATE_STATUS_DROP: StringName = &"fate_status_drop"
const SOURCE_KIND_LOW_LUCK_EVENT: StringName = &"low_luck_event"

const SOURCE_ID_ORDINARY_BATTLE: StringName = &"ordinary_battle"
const SOURCE_ID_ELITE_BOSS_BATTLE: StringName = &"elite_boss_battle"

const ITEM_CALAMITY_SHARD: StringName = &"calamity_shard"
const ITEM_BLACK_CROWN_CORE: StringName = &"black_crown_core"

const ORDINARY_BATTLE_CALAMITY_SHARD_CHAPTER_CAP := 4
const CALAMITY_SHARD_CHAPTER_FLAG_PREFIX := "calamity_shard_chapter_slot_"
