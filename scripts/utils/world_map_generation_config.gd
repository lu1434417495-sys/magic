## 文件说明：该脚本属于世界地图生成配置相关的配置资源脚本，集中维护随机种子、世界尺寸区块集合、区块尺寸等顶层字段。
## 审查重点：重点核对字段命名、默认值、配置含义以及它们与存档结构、规则判定之间的对应关系。
## 备注：后续如果调整字段语义，需要同步检查资源配置、序列化逻辑和所有读取方。

class_name WorldMapGenerationConfig
extends Resource

const SETTLEMENT_CONFIG_SCRIPT = preload("res://scripts/utils/settlement_config.gd")

## 字段说明：在编辑器中暴露随机种子配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var seed := 20260403
## 字段说明：在编辑器中暴露世界尺寸区块集合参数，便于直接调整尺寸、范围、间距或视图表现。
@export var world_size_in_chunks: Vector2i = Vector2i(2, 2)
## 字段说明：在编辑器中暴露区块尺寸参数，便于直接调整尺寸、范围、间距或视图表现。
@export var chunk_size: Vector2i = Vector2i(8, 8)
## 字段说明：在编辑器中暴露玩家起始坐标配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var player_start_coord: Vector2i = Vector2i(1, 1)
## 字段说明：在编辑器中暴露玩家视野范围参数，便于直接调整尺寸、范围、间距或视图表现。
@export var player_vision_range := 4
## 字段说明：在编辑器中暴露程序化生成启用开关，便于决定当前世界是否走程序化生成流程。
@export var procedural_generation_enabled := false
## 字段说明：在编辑器中暴露程序化村庄数量参数，便于直接调整生成数量、奖励数量或容量规模。
@export_range(1, 256, 1) var procedural_village_count := 1
## 字段说明：在编辑器中暴露程序化城镇数量参数，便于直接调整生成数量、奖励数量或容量规模。
@export_range(0, 128, 1) var procedural_town_count := 1
## 字段说明：在编辑器中暴露程序化城市数量参数，便于直接调整生成数量、奖励数量或容量规模。
@export_range(0, 64, 1) var procedural_city_count := 1
## 字段说明：在编辑器中暴露程序化都城数量参数，便于直接调整生成数量、奖励数量或容量规模。
@export_range(0, 32, 1) var procedural_capital_count := 1
## 字段说明：在编辑器中暴露程序化世界要塞数量参数，便于直接调整生成数量、奖励数量或容量规模。
@export_range(0, 16, 1) var procedural_world_stronghold_count := 1
## 字段说明：在编辑器中暴露程序化大都会数量参数，便于直接调整生成数量、奖励数量或容量规模。
@export_range(0, 16, 1) var procedural_metropolis_count := 0
## 字段说明：在编辑器中暴露村庄间距格子集合参数，便于直接调整尺寸、范围、间距或视图表现。
@export_range(1, 512, 1) var village_spacing_cells := 80
## 字段说明：在编辑器中暴露城镇间距格子集合参数，便于直接调整尺寸、范围、间距或视图表现。
@export_range(1, 512, 1) var town_spacing_cells := 110
## 字段说明：在编辑器中暴露城市间距格子集合参数，便于直接调整尺寸、范围、间距或视图表现。
@export_range(1, 512, 1) var city_spacing_cells := 150
## 字段说明：在编辑器中暴露都城间距格子集合参数，便于直接调整尺寸、范围、间距或视图表现。
@export_range(1, 512, 1) var capital_spacing_cells := 220
## 字段说明：在编辑器中暴露世界要塞间距格子集合参数，便于直接调整尺寸、范围、间距或视图表现。
@export_range(1, 512, 1) var world_stronghold_spacing_cells := 280
## 字段说明：在编辑器中暴露大都会间距格子集合参数，便于直接调整尺寸、范围、间距或视图表现。
@export_range(1, 512, 1) var metropolis_spacing_cells := 340
## 字段说明：在编辑器中暴露保底起始野外遭遇参数，便于直接调整生成数量、奖励数量或容量规模。
@export var guarantee_starting_wild_encounter := false
## 字段说明：在编辑器中暴露起始野外生成最小距离配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export_range(1, 64, 1) var starting_wild_spawn_min_distance := 3
## 字段说明：在编辑器中暴露起始野外生成最大距离配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export_range(1, 64, 1) var starting_wild_spawn_max_distance := 4
## 字段说明：在编辑器中暴露聚落资源库配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var settlement_library: Array = []
## 字段说明：在编辑器中暴露设施资源库配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var facility_library: Array = []
## 字段说明：在编辑器中暴露聚落相关配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var settlement_distribution: Array = []
## 字段说明：在编辑器中暴露野外怪物相关配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
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
		SETTLEMENT_CONFIG_SCRIPT.SettlementTier.METROPOLIS:
			return max(procedural_metropolis_count, 0)
		_:
			return 0


func get_settlement_spacing_cells(tier: int) -> int:
	match tier:
		SETTLEMENT_CONFIG_SCRIPT.SettlementTier.VILLAGE:
			return village_spacing_cells
		SETTLEMENT_CONFIG_SCRIPT.SettlementTier.TOWN:
			return town_spacing_cells
		SETTLEMENT_CONFIG_SCRIPT.SettlementTier.CITY:
			return city_spacing_cells
		SETTLEMENT_CONFIG_SCRIPT.SettlementTier.CAPITAL:
			return capital_spacing_cells
		SETTLEMENT_CONFIG_SCRIPT.SettlementTier.WORLD_STRONGHOLD:
			return world_stronghold_spacing_cells
		SETTLEMENT_CONFIG_SCRIPT.SettlementTier.METROPOLIS:
			return metropolis_spacing_cells
		_:
			return 64
