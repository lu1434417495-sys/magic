## 文件说明：该脚本属于视野来源数据相关的数据对象脚本，集中维护来源唯一标识、中心、范围等顶层字段。
## 审查重点：重点核对字段命名、默认值、配置含义以及它们与存档结构、规则判定之间的对应关系。
## 备注：后续如果调整字段语义，需要同步检查资源配置、序列化逻辑和所有读取方。

class_name VisionSourceData
extends RefCounted

## 字段说明：记录来源唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var source_id: String
## 字段说明：缓存中心实例，供多个系统共享读取并保持统一约定。
var center: Vector2i
## 字段说明：记录范围，供多个系统共享读取并保持统一约定。
var range: int
## 字段说明：记录阵营唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var faction_id: String


func _init(
	source_identifier: String = "",
	source_center: Vector2i = Vector2i.ZERO,
	source_range: int = 0,
	source_faction_id: String = ""
) -> void:
	source_id = source_identifier
	center = source_center
	range = source_range
	faction_id = source_faction_id
