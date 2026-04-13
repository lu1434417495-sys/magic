## 文件说明：该脚本属于聚落相关规则相关的工具脚本，集中维护聚落唯一标识、偏好来源、阵营唯一标识等顶层字段。
## 审查重点：重点核对字段命名、默认值、配置含义以及它们与存档结构、规则判定之间的对应关系。
## 备注：后续如果调整字段语义，需要同步检查资源配置、序列化逻辑和所有读取方。

class_name SettlementDistributionRule
extends Resource

## 字段说明：在编辑器中暴露聚落唯一标识配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var settlement_id: String = ""
## 字段说明：在编辑器中暴露偏好来源配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var preferred_origin: Vector2i = Vector2i.ZERO
## 字段说明：在编辑器中暴露阵营唯一标识配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var faction_id: String = "neutral"
