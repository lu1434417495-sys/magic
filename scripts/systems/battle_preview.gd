## 文件说明：该脚本属于战斗预览相关的业务脚本，集中维护允许、日志文本行、目标单位标识列表等顶层字段。
## 审查重点：重点核对字段默认值、状态流转顺序、跨系统引用关系以及运行时读写时机是否仍然可靠。
## 备注：后续如果增删字段，需要同步检查调用方、状态同步链路以及历史数据兼容处理。

class_name BattlePreview
extends RefCounted

## 字段说明：用于标记允许当前是否成立或生效，供脚本后续分支判断使用，会参与运行时状态流转、系统协作和存档恢复。
var allowed := false
## 字段说明：保存日志文本行，便于顺序遍历、批量展示、批量运算和整体重建。
var log_lines: Array[String] = []
## 字段说明：保存目标单位标识列表，便于批量遍历、交叉查找和界面展示。
var target_unit_ids: Array[StringName] = []
## 字段说明：保存目标坐标列表，供范围判定、占位刷新、批量渲染或目标选择复用。
var target_coords: Array[Vector2i] = []
## 字段说明：保存当前技能的命中预览摘要，便于 HUD、snapshot 与测试复用同一套解析结果。
var hit_preview: Dictionary = {}
