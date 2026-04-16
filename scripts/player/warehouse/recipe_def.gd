## 文件说明：该脚本属于配方定义相关的 schema 资源脚本，集中维护配方唯一标识、输入输出和过滤标签等顶层字段。
## 审查重点：重点核对字段命名、默认值、配置含义以及它们与后续合成、任务和存档结构的对应关系。
## 备注：该脚本当前只提供最小 schema，后续若扩展配方执行器再补行为方法即可。

class_name RecipeDef
extends Resource

## 字段说明：记录配方唯一标识，作为查表、序列化和跨系统引用时使用的主键。
@export var recipe_id: StringName = &""
## 字段说明：用于界面展示的名称文本，主要服务于玩家阅读和调试观察，不直接参与数值判定。
@export var display_name: String = ""
## 字段说明：用于界面说明的描述文本，帮助玩家或策划理解该配方的用途与限制。
@export_multiline var description: String = ""
## 字段说明：记录输入物品标识顺序，供后续合成条件、展示和校验使用。
@export var input_item_ids: Array[StringName] = []
## 字段说明：记录输入物品数量顺序，与 input_item_ids 一一对应。
@export var input_item_quantities: PackedInt32Array = PackedInt32Array()
## 字段说明：记录输出物品标识，供后续合成产物和校验使用。
@export var output_item_id: StringName = &""
## 字段说明：记录输出物品数量，便于表达批量合成结果。
@export_range(1, 9999, 1) var output_quantity := 1
## 字段说明：记录配方所需的设施标签，供后续 service / facility 过滤逻辑引用。
@export var required_facility_tags: Array[StringName] = []
## 字段说明：记录失败原因文本，供后续执行器和 UI 直接复用。
@export var failure_reason: String = ""
