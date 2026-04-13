## 文件说明：该脚本属于标签条件相关的业务脚本，集中维护标签、数量、技能状态等顶层字段。
## 审查重点：重点核对字段命名、默认值、配置含义以及它们与存档结构、规则判定之间的对应关系。
## 备注：后续如果调整字段语义，需要同步检查资源配置、序列化逻辑和所有读取方。

class_name TagRequirement
extends Resource

const SKILL_STATE_LEARNED: StringName = &"learned"
const SKILL_STATE_CORE: StringName = &"core"
const SKILL_STATE_CORE_MAX: StringName = &"core_max"

const ORIGIN_FILTER_ANY: StringName = &"any"
const ORIGIN_FILTER_UNMERGED_ONLY: StringName = &"unmerged_only"
const ORIGIN_FILTER_MERGED_ONLY: StringName = &"merged_only"

const SELECTION_ROLE_ASSIGNED_CORE: StringName = &"assigned_core"
const SELECTION_ROLE_QUALIFIER: StringName = &"qualifier"

## 字段说明：在编辑器中暴露标签配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var tag: StringName = &""
## 字段说明：在编辑器中暴露数量参数，便于直接调整生成数量、奖励数量或容量规模。
@export var count := 1
## 字段说明：在编辑器中暴露技能状态配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var skill_state: StringName = SKILL_STATE_CORE_MAX
## 字段说明：在编辑器中暴露来源过滤配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var origin_filter: StringName = ORIGIN_FILTER_ANY
## 字段说明：在编辑器中暴露选择结果角色定位配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var selection_role: StringName = SELECTION_ROLE_ASSIGNED_CORE


func get_normalized_skill_state() -> StringName:
	match skill_state:
		SKILL_STATE_LEARNED, SKILL_STATE_CORE, SKILL_STATE_CORE_MAX:
			return skill_state
		_:
			return SKILL_STATE_CORE_MAX


func get_normalized_origin_filter() -> StringName:
	match origin_filter:
		ORIGIN_FILTER_ANY, ORIGIN_FILTER_UNMERGED_ONLY, ORIGIN_FILTER_MERGED_ONLY:
			return origin_filter
		_:
			return ORIGIN_FILTER_ANY


func get_normalized_selection_role() -> StringName:
	match selection_role:
		SELECTION_ROLE_QUALIFIER, SELECTION_ROLE_ASSIGNED_CORE:
			return selection_role
		_:
			return SELECTION_ROLE_ASSIGNED_CORE
