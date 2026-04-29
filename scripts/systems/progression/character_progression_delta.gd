## 文件说明：该脚本属于角色成长增量相关的业务脚本，集中维护成员唯一标识、已升级技能标识列表、授予技能标识列表等顶层字段。
## 审查重点：重点核对字段默认值、状态流转顺序、跨系统引用关系以及运行时读写时机是否仍然可靠。
## 备注：后续如果增删字段，需要同步检查调用方、状态同步链路以及历史数据兼容处理。

class_name CharacterProgressionDelta
extends RefCounted

## 字段说明：记录成员唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var member_id: StringName = &""
## 字段说明：保存已升级技能标识列表，便于批量遍历、交叉查找和界面展示。
var leveled_skill_ids: Array[StringName] = []
## 字段说明：保存授予技能标识列表，便于批量遍历、交叉查找和界面展示。
var granted_skill_ids: Array[StringName] = []
## 字段说明：保存已变化职业标识列表，便于批量遍历、交叉查找和界面展示。
var changed_profession_ids: Array[StringName] = []
## 字段说明：记录角色等级相关，会参与运行时状态流转、系统协作和存档恢复。
var character_level_before := 0
## 字段说明：记录角色等级之后，会参与运行时状态流转、系统协作和存档恢复。
var character_level_after := 0
## 字段说明：保存待处理职业候选项，便于顺序遍历、批量展示、批量运算和整体重建。
var pending_profession_choices: Array[PendingProfessionChoice] = []
## 字段说明：用于标记后续流程是否需要补做晋升模态，从而延后昂贵或依赖性较强的操作，会参与运行时状态流转、系统协作和存档恢复。
var needs_promotion_modal := false
## 字段说明：缓存熟练度变更列表字典，集中保存可按键查询的运行时数据。
var mastery_changes: Array[Dictionary] = []
## 字段说明：保存已解锁成就标识列表，便于批量遍历、交叉查找和界面展示。
var unlocked_achievement_ids: Array[StringName] = []
## 字段说明：缓存知识变更列表字典，集中保存可按键查询的运行时数据。
var knowledge_changes: Array[Dictionary] = []
## 字段说明：缓存属性变更列表字典，集中保存可按键查询的运行时数据。
var attribute_changes: Array[Dictionary] = []
