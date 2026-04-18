## 文件说明：该脚本属于世界地图格子数据相关的数据对象脚本，集中维护坐标、区块坐标、占用标识等顶层字段。
## 审查重点：重点核对字段命名、默认值、配置含义以及它们与存档结构、规则判定之间的对应关系。
## 备注：后续如果调整字段语义，需要同步检查资源配置、序列化逻辑和所有读取方。

class_name WorldMapCellData
extends RefCounted

## 字段说明：记录对象当前使用的网格坐标，供绘制、寻路或占位计算使用。
var coord: Vector2i
## 字段说明：记录区块坐标，用于定位对象、绘制内容或执行网格计算。
var chunk_coord: Vector2i
## 字段说明：记录占用者唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var occupant_id: String = ""
## 字段说明：记录占位根节点唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var footprint_root_id: String = ""


func _init(
	cell_coord: Vector2i = Vector2i.ZERO,
	cell_chunk_coord: Vector2i = Vector2i.ZERO
) -> void:
	coord = cell_coord
	chunk_coord = cell_chunk_coord
