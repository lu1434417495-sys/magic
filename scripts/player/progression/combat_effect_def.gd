## 文件说明：该脚本属于战斗效果定义相关的定义资源脚本，集中维护效果类型、跳点效果类型、强度等顶层字段。
## 审查重点：重点核对字段命名、默认值、配置含义以及它们与存档结构、规则判定之间的对应关系。
## 备注：后续如果调整字段语义，需要同步检查资源配置、序列化逻辑和所有读取方。

class_name CombatEffectDef
extends Resource

## 常量说明：跳跃技能保底弧高占比的最小值；schema 校验在 mode=jump 时强制 jump_arc_ratio >= 此值。
const MIN_JUMP_ARC_RATIO := 0.15

## 字段说明：在编辑器中暴露效果类型配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var effect_type: StringName = &""
## 字段说明：在编辑器中暴露跳点效果类型配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var tick_effect_type: StringName = &""
## 字段说明：在编辑器中暴露强度配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var power := 0
## 字段说明：效果生效所需的最低技能等级；0 表示技能学会后的初始等级即可生效。
@export var min_skill_level := 0
## 字段说明：效果生效的最高技能等级；-1 表示没有上限。
@export var max_skill_level := -1
## 字段说明：在编辑器中暴露伤害倍率百分比配置，便于定义相对基准伤害而非固定强度。
@export var damage_ratio_percent := 100
## 字段说明：在编辑器中暴露伤害标签配置，便于命中后选择伤害分类、减伤和日志语义。
@export var damage_tag: StringName = &""
## 字段说明：在编辑器中暴露效果目标队伍过滤配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var effect_target_team_filter: StringName = &""
## 字段说明：在编辑器中暴露状态唯一标识配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var status_id: StringName = &""
## 字段说明：在编辑器中暴露地形效果唯一标识配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var terrain_effect_id: StringName = &""
## 字段说明：在编辑器中暴露地形替换配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var terrain_replace_to: StringName = &""
## 字段说明：在编辑器中暴露高度增量配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var height_delta := 0
## 字段说明：在编辑器中暴露临时体型分类配置，供战斗内持续性体型覆盖效果使用。
@export var body_size_category: StringName = &""
## 字段说明：在编辑器中暴露强制位移模式配置，用于表达击退、拉拽、跳斩等位移效果。
@export var forced_move_mode: StringName = &""
## 字段说明：在编辑器中暴露强制位移距离配置；mode=jump 时复用为 max_range 硬上限（0 = 不设上限）。
@export var forced_move_distance := 0
## 字段说明：跳跃 budget 的基础值，不依赖 STR；策划用于设定"小白也能用"的最小跳跃量。
@export var jump_base_budget := 0
## 字段说明：跳跃 budget 的 STR 系数；effective STR × scale 加到 budget 上。
@export var jump_str_scale := 0.0
## 字段说明：跳跃 budget 中保底弧高的占比；mode=jump 时校验 ≥ MIN_JUMP_ARC_RATIO。
@export var jump_arc_ratio := 0.0
## 字段说明：跳跃距离换算系数；range_budget × multiplier 得到格数上限（高度成本固定 1:1）。
@export var jump_range_multiplier := 1
## 字段说明：在编辑器中暴露持续时间TU参数，便于直接调整尺寸、范围、间距或视图表现。
@export var duration_tu := 0
## 字段说明：在编辑器中暴露跳点间隔TU配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var tick_interval_tu := 0
## 字段说明：在编辑器中暴露堆叠行为配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var stack_behavior: StringName = &"refresh"
## 字段说明：在编辑器中暴露叠层上限配置，便于后续状态或特效链按统一规则约束堆叠。
@export var stack_limit := 0
## 字段说明：在编辑器中暴露额外条件标识配置，便于定义低血、潮湿、护甲破碎等条件增益。
@export var bonus_condition: StringName = &""
## 字段说明：在编辑器中暴露触发事件标识配置，便于描述命中后、击杀后等触发型效果。
@export var trigger_event: StringName = &""
## 字段说明：被动效果的触发条件；空表示主动施放即时生效，"battle_start" 表示战斗开始时触发，"on_fatal_damage" 表示受致命伤害时触发。
@export var trigger_condition: StringName = &""
## 字段说明：触发前需要拥有的状态唯一标识；配合 trigger_condition 使用，如 on_fatal_damage 时需要先拥有 death_ward。
@export var trigger_status_id: StringName = &""
## 字段说明：效果生效前目标需要通过的豁免 DC；0 表示不做豁免。
@export var save_dc := 0
## 字段说明：豁免 DC 来源；static 使用 save_dc，caster_spell 使用 8 + 施法属性调整值 + 法术熟练加值。
@export var save_dc_mode: StringName = &"static"
## 字段说明：动态法术 DC 使用的施法者属性；caster_spell 模式下通常为 intelligence。
@export var save_dc_source_ability: StringName = &""
## 字段说明：豁免使用的基础属性 id。
@export var save_ability: StringName = &""
## 字段说明：带豁免的状态效果在失败时应用的状态；为空时使用 status_id。
@export var save_failure_status_id: StringName = &""
## 字段说明：伤害效果豁免成功时是否保留半伤。
@export var save_partial_on_success := false
## 字段说明：豁免标签，用于 advantage / disadvantage / immunity 查找。
@export var save_tag: StringName = &""
## 字段说明：在编辑器中暴露参数配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var params: Dictionary = {}


func duplicate_for_runtime() -> CombatEffectDef:
	var copy := duplicate(true) as CombatEffectDef
	if copy == null:
		return null
	if copy.params == null:
		copy.params = {}
	else:
		copy.params = copy.params.duplicate(true)
	return copy
