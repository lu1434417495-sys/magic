## 文件说明：该脚本属于战斗棋盘场景装饰物目录相关的工具脚本，主要封装当前领域所需的辅助逻辑。
## 审查重点：重点核对字段命名、默认值、配置含义以及它们与存档结构、规则判定之间的对应关系。
## 备注：后续如果调整字段语义，需要同步检查资源配置、序列化逻辑和所有读取方。

class_name BattleBoardPropCatalog
extends RefCounted

const PROP_SPIKE_BARRICADE := &"spike_barricade"
const PROP_OBJECTIVE_MARKER := &"objective_marker"
const PROP_TENT := &"tent"
const PROP_TORCH := &"torch"


static func is_supported(prop_id: StringName) -> bool:
	return (
		prop_id == PROP_SPIKE_BARRICADE
		or prop_id == PROP_OBJECTIVE_MARKER
		or prop_id == PROP_TENT
		or prop_id == PROP_TORCH
	)


static func requires_interaction_shape(prop_id: StringName) -> bool:
	return prop_id == PROP_OBJECTIVE_MARKER


static func get_sort_priority(prop_id: StringName) -> int:
	match prop_id:
		PROP_SPIKE_BARRICADE:
			return 0
		PROP_TORCH:
			return 1
		PROP_TENT:
			return 2
		PROP_OBJECTIVE_MARKER:
			return 3
		_:
			return 0
