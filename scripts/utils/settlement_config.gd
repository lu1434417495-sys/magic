class_name SettlementConfig
extends Resource

enum SettlementTier {
	VILLAGE,
	TOWN,
	CITY,
	CAPITAL,
	WORLD_STRONGHOLD,
}

@export var settlement_id: String = ""
@export var display_name: String = ""
@export_enum("Village", "Town", "City", "Capital", "World Stronghold") var tier: int = SettlementTier.VILLAGE
@export var facility_slots: Array = []
@export var guaranteed_facility_ids: Array[String] = []
@export var optional_facility_pool: Array = []
@export_range(0, 16, 1) var max_optional_facilities: int = 0


func get_footprint_size() -> Vector2i:
	match tier:
		SettlementTier.VILLAGE:
			return Vector2i.ONE
		SettlementTier.TOWN:
			return Vector2i(2, 2)
		SettlementTier.CITY:
			return Vector2i(2, 2)
		SettlementTier.CAPITAL:
			return Vector2i(3, 3)
		SettlementTier.WORLD_STRONGHOLD:
			return Vector2i(4, 4)
		_:
			return Vector2i.ONE


func get_tier_name() -> String:
	match tier:
		SettlementTier.VILLAGE:
			return "村"
		SettlementTier.TOWN:
			return "镇"
		SettlementTier.CITY:
			return "城市"
		SettlementTier.CAPITAL:
			return "主城"
		SettlementTier.WORLD_STRONGHOLD:
			return "世界据点"
		_:
			return "未知"
