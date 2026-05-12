## 文件说明：该脚本属于战斗预览相关的业务脚本，集中维护允许、日志文本行、目标单位标识列表等顶层字段。
## 审查重点：重点核对字段默认值、状态流转顺序、跨系统引用关系以及运行时读写时机是否仍然可靠。
## 备注：后续如果增删字段，需要同步检查调用方、状态同步链路以及历史数据兼容处理。

class_name BattlePreview
extends RefCounted

const BattleSpecialProfileGateResult = preload("res://scripts/systems/battle/core/battle_special_profile_gate_result.gd")
const BattleSpecialProfilePreviewFacts = preload("res://scripts/systems/battle/core/battle_special_profile_preview_facts.gd")

## 字段说明：用于标记允许当前是否成立或生效，供脚本后续分支判断使用，会参与运行时状态流转、系统协作和存档恢复。
var allowed := false
## 字段说明：保存日志文本行，便于顺序遍历、批量展示、批量运算和整体重建。
var log_lines: Array[String] = []
## 字段说明：保存目标单位标识列表，便于批量遍历、交叉查找和界面展示。
var target_unit_ids: Array[StringName] = []
## 字段说明：保存目标坐标列表，供范围判定、占位刷新、批量渲染或目标选择复用。
var target_coords: Array[Vector2i] = []
## 字段说明：保存正式 preview 解析出的施法者实际停点；仅对会移动施法者的技能生效，供 AI 评分与测试复用同一运行时口径。
var resolved_anchor_coord: Vector2i = Vector2i(-1, -1)
## 字段说明：保存移动类指令的正式行动点消耗，供 AI 评分、simulation report 与 headless 回归复用。
var move_cost := 0
## 字段说明：保存当前技能的命中预览摘要，便于 HUD、snapshot 与测试复用同一套解析结果。
var hit_preview: Dictionary = {}
## 字段说明：保存当前技能的非暴击基础伤害范围预览；只来自武器骰、技能骰、dice_bonus 与 power，不走正式伤害结算。
var damage_preview: Dictionary = {}
## 字段说明：保存特殊技能 profile 的内容/运行时门禁结果，供 HUD、headless 和执行链保持同一阻断原因。
var special_profile_gate_result: BattleSpecialProfileGateResult = null
## 字段说明：保存特殊技能 profile 的 typed preview facts，供 HUD、AI 和战报预览消费，不读取旧 effect_defs。
var special_profile_preview_facts: BattleSpecialProfilePreviewFacts = null
