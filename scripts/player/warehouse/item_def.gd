## 文件说明：该脚本属于物品定义相关的定义资源脚本，集中维护物品唯一标识、显示名称、描述等顶层字段。
## 审查重点：重点核对字段命名、默认值、配置含义以及它们与存档结构、规则判定之间的对应关系。
## 备注：后续如果调整字段语义，需要同步检查资源配置、序列化逻辑和所有读取方。

class_name ItemDef
extends Resource

const EQUIPMENT_RULES_SCRIPT = preload("res://scripts/player/equipment/equipment_rules.gd")

const ITEM_CATEGORY_MISC: StringName = &"misc"
const ITEM_CATEGORY_EQUIPMENT: StringName = &"equipment"
const ITEM_CATEGORY_SKILL_BOOK: StringName = &"skill_book"

## 字段说明：在编辑器中暴露物品唯一标识配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var item_id: StringName = &""
## 字段说明：用于界面展示的名称文本，主要服务于玩家阅读和调试观察，不直接参与数值判定。
@export var display_name: String = ""
## 字段说明：用于界面说明的描述文本，帮助玩家或策划理解该对象的用途与限制。
@export_multiline var description: String = ""
## 字段说明：在编辑器中配置图标，运行时会据此加载场景、资源、配置文件或存档模板。
@export_file("*.png", "*.svg", "*.webp", "*.jpg", "*.jpeg") var icon: String = ""
## 字段说明：在编辑器中暴露是否可堆叠配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var is_stackable := true
## 字段说明：在编辑器中暴露最大堆叠参数，用于限制该对象可达到的上限并控制成长或容量边界。
@export_range(1, 9999, 1) var max_stack := 99
## 字段说明：记录物品分类，用于区分普通素材、可装备道具等行为分支。
@export var item_category: StringName = ITEM_CATEGORY_MISC
## 字段说明：当物品可装备时，声明允许进入的装备槽位列表。
@export var equipment_slot_ids: Array[String] = []
## 字段说明：当物品被装备时，提供附加到角色属性结算链路中的修正器。
@export var attribute_modifiers: Array[AttributeModifier] = []
## 字段说明：当物品属于技能书时，声明被使用后授予的技能标识。
@export var granted_skill_id: StringName = &""


func get_effective_max_stack() -> int:
	if not is_stackable:
		return 1
	return maxi(int(max_stack), 1)


func has_equipment_category() -> bool:
	return item_category == ITEM_CATEGORY_EQUIPMENT


func get_equipment_slot_ids() -> Array[StringName]:
	return EQUIPMENT_RULES_SCRIPT.normalize_slot_ids(equipment_slot_ids)


func is_equipment() -> bool:
	return has_equipment_category() and not get_equipment_slot_ids().is_empty()


func is_skill_book() -> bool:
	return item_category == ITEM_CATEGORY_SKILL_BOOK and granted_skill_id != &""


func get_attribute_modifiers() -> Array[AttributeModifier]:
	return attribute_modifiers.duplicate()
