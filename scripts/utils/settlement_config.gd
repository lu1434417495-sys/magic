## 文件说明：该脚本属于聚落配置相关的配置资源脚本，集中维护聚落唯一标识、显示名称、等级层级等顶层字段。
## 审查重点：重点核对字段命名、默认值、配置含义以及它们与存档结构、规则判定之间的对应关系。
## 备注：后续如果调整字段语义，需要同步检查资源配置、序列化逻辑和所有读取方。

class_name SettlementConfig
extends Resource

enum SettlementTier {
	VILLAGE,
	TOWN,
	CITY,
	CAPITAL,
	WORLD_STRONGHOLD,
	METROPOLIS,
}

## 字段说明：在编辑器中暴露聚落唯一标识配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var settlement_id: String = ""
## 字段说明：用于界面展示的名称文本，主要服务于玩家阅读和调试观察，不直接参与数值判定。
@export var display_name: String = ""
## 字段说明：在编辑器中暴露等级层级配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export_enum("Village", "Town", "City", "Capital", "World Stronghold", "Metropolis") var tier: int = SettlementTier.VILLAGE
## 字段说明：在编辑器中暴露设施槽位集合配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var facility_slots: Array = []
## 字段说明：在编辑器中暴露保底设施标识列表配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var guaranteed_facility_ids: Array[String] = []
## 字段说明：在编辑器中暴露可选设施池配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var optional_facility_pool: Array = []
## 字段说明：在编辑器中暴露最大可选设施集合参数，用于限制该对象可达到的上限并控制成长或容量边界。
@export_range(0, 16, 1) var max_optional_facilities: int = 0


func get_template_id() -> String:
	return settlement_id.strip_edges()


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
		SettlementTier.METROPOLIS:
			return Vector2i(5, 5)
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
		SettlementTier.METROPOLIS:
			return "都会"
		_:
			return "未知"
