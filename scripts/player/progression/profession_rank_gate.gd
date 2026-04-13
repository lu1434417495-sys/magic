## 文件说明：该脚本属于职业阶位门槛相关的业务脚本，集中维护职业唯一标识、最小阶位、校验模式等顶层字段。
## 审查重点：重点核对字段命名、默认值、配置含义以及它们与存档结构、规则判定之间的对应关系。
## 备注：后续如果调整字段语义，需要同步检查资源配置、序列化逻辑和所有读取方。

class_name ProfessionRankGate
extends Resource

## 字段说明：在编辑器中暴露职业唯一标识配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var profession_id: StringName = &""
## 字段说明：在编辑器中暴露最小阶位参数，用于定义该对象生效或生成时的下限条件。
@export var min_rank := 1
## 字段说明：在编辑器中暴露校验模式配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var check_mode: StringName = &"historical"

