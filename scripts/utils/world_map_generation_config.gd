class_name WorldMapGenerationConfig
extends Resource

const SETTLEMENT_CONFIG_SCRIPT = preload("res://scripts/utils/settlement_config.gd")

@export var seed := 20260403
@export var world_size_in_chunks: Vector2i = Vector2i(2, 2)
@export var chunk_size: Vector2i = Vector2i(8, 8)
@export var player_start_coord: Vector2i = Vector2i(1, 1)
@export var player_vision_range := 4
@export var procedural_generation_enabled := false
@export_range(1, 256, 1) var procedural_village_count := 1
@export_range(0, 128, 1) var procedural_town_count := 1
@export_range(0, 64, 1) var procedural_city_count := 1
@export_range(0, 32, 1) var procedural_capital_count := 1
@export_range(0, 16, 1) var procedural_world_stronghold_count := 1
@export var settlement_library: Array = []
@export var facility_library: Array = []
@export var settlement_distribution: Array = []
@export var wild_monster_distribution: Array = []


func get_world_size_cells() -> Vector2i:
	return Vector2i(
		world_size_in_chunks.x * chunk_size.x,
		world_size_in_chunks.y * chunk_size.y
	)


func get_target_settlement_count(tier: int) -> int:
	match tier:
		SETTLEMENT_CONFIG_SCRIPT.SettlementTier.VILLAGE:
			return max(procedural_village_count, 1)
		SETTLEMENT_CONFIG_SCRIPT.SettlementTier.TOWN:
			return max(procedural_town_count, 0)
		SETTLEMENT_CONFIG_SCRIPT.SettlementTier.CITY:
			return max(procedural_city_count, 0)
		SETTLEMENT_CONFIG_SCRIPT.SettlementTier.CAPITAL:
			return max(procedural_capital_count, 0)
		SETTLEMENT_CONFIG_SCRIPT.SettlementTier.WORLD_STRONGHOLD:
			return max(procedural_world_stronghold_count, 0)
		_:
			return 0


func get_settlement_spacing_cells(tier: int) -> int:
	match tier:
		SETTLEMENT_CONFIG_SCRIPT.SettlementTier.VILLAGE:
			return 80
		SETTLEMENT_CONFIG_SCRIPT.SettlementTier.TOWN:
			return 110
		SETTLEMENT_CONFIG_SCRIPT.SettlementTier.CITY:
			return 150
		SETTLEMENT_CONFIG_SCRIPT.SettlementTier.CAPITAL:
			return 220
		SETTLEMENT_CONFIG_SCRIPT.SettlementTier.WORLD_STRONGHOLD:
			return 280
		_:
			return 64
