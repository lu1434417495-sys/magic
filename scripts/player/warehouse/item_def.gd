## 文件说明：该脚本属于物品定义相关的定义资源脚本，集中维护物品唯一标识、显示名称、描述等顶层字段。
## 审查重点：重点核对字段命名、默认值、配置含义以及它们与存档结构、规则判定之间的对应关系。
## 备注：后续如果调整字段语义，需要同步检查资源配置、序列化逻辑和所有读取方。

class_name ItemDef
extends Resource

const EQUIPMENT_RULES_SCRIPT = preload("res://scripts/player/equipment/equipment_rules.gd")

const ITEM_CATEGORY_MISC: StringName = &"misc"
const ITEM_CATEGORY_EQUIPMENT: StringName = &"equipment"
const ITEM_CATEGORY_SKILL_BOOK: StringName = &"skill_book"

const EQUIPMENT_TYPE_WEAPON: StringName = &"weapon"
const EQUIPMENT_TYPE_ARMOR: StringName = &"armor"
const EQUIPMENT_TYPE_ACCESSORY: StringName = &"accessory"
const DAMAGE_TAG_PHYSICAL_SLASH: StringName = &"physical_slash"
const DAMAGE_TAG_PHYSICAL_PIERCE: StringName = &"physical_pierce"
const DAMAGE_TAG_PHYSICAL_BLUNT: StringName = &"physical_blunt"

## 字段说明：在编辑器中暴露物品唯一标识配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var item_id: StringName = &""
## 字段说明：当此 ItemDef 引用模板时填写模板 item_id；空表示该资源不依赖任何模板。
## 模板继承在 ItemContentRegistry 注册阶段一次性合并，运行时拿到的始终是已合并的 ItemDef。
@export var base_item_id: StringName = &""
## 字段说明：用于界面展示的名称文本，主要服务于玩家阅读和调试观察，不直接参与数值判定。
@export var display_name: String = ""
## 字段说明：用于界面说明的描述文本，帮助玩家或策划理解该对象的用途与限制。
@export_multiline var description: String = ""
## 字段说明：在编辑器中配置图标，运行时会据此加载场景、资源、配置文件或存档模板。
@export_file("*.png", "*.svg", "*.webp", "*.jpg", "*.jpeg") var icon: String = ""
## 字段说明：在编辑器中暴露是否可堆叠配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var is_stackable := true
## 字段说明：在编辑器中暴露基础价格配置，便于据点商店、出售和价格计算统一读取。
@export_range(0, 999999, 1) var base_price := 0
## 字段说明：在编辑器中暴露基础购买价格配置；未填写时回退到 base_price，保持旧资源默认行为。
@export_range(0, 999999, 1) var buy_price := 0
## 字段说明：在编辑器中暴露基础出售价格配置；未填写时回退到 base_price 的半价逻辑，保持旧资源默认行为。
@export_range(0, 999999, 1) var sell_price := 0
## 字段说明：在编辑器中暴露是否可出售配置，便于根据物品类型控制据点商店流转。
@export var sellable := true
## 字段说明：在编辑器中暴露最大堆叠参数，用于限制该对象可达到的上限并控制成长或容量边界。
@export_range(1, 9999, 1) var max_stack := 99
## 字段说明：记录物品分类，用于区分普通素材、可装备道具等行为分支。
## 默认 &""（未填）：注册阶段会让模板继承生效；运行时通过 get_item_category_normalized() 视作 misc。
## 显式写 misc / equipment / skill_book 时覆盖模板，且和现有语义保持一致。
@export var item_category: StringName = &""
## 字段说明：记录物品标签集合，供后续配方、任务和筛选逻辑引用。
@export var tags: Array[StringName] = []
## 字段说明：记录物品所属的合成分组，供后续配方过滤和内容分桶使用。
@export var crafting_groups: Array[StringName] = []
## 字段说明：记录物品所属的任务分组，供后续任务过滤和内容分桶使用。
@export var quest_groups: Array[StringName] = []
## 字段说明：当物品可装备时，声明允许进入的装备槽位列表。
@export var equipment_slot_ids: Array[String] = []
## 字段说明：当物品被装备时，提供附加到角色属性结算链路中的修正器。
@export var attribute_modifiers: Array[AttributeModifier] = []
## 字段说明：当物品属于技能书时，声明被使用后授予的技能标识。
@export var granted_skill_id: StringName = &""
## 字段说明：装备后真实占用的所有槽位；非空时 equipment_slot_ids 只能声明 1 个入口槽（双手武器场景）。
## 空列表表示只占用被点击的入口槽本身。
@export var occupied_slot_ids: Array[String] = []
## 字段说明：装备资格要求；非空时换装前必须通过 check() 校验。
@export var equip_requirement: Resource = null
## 字段说明：装备大类标识，用于候选过滤与文案显示，不参与核心规则判定。
@export var equipment_type_id: StringName = &""
## 字段说明：武器装备后的攻击范围；武器技能射程从该值读取，不再从技能资源单独配置。
@export_range(0, 99, 1) var weapon_attack_range := 0
## 字段说明：近战武器造成的唯一物理伤害类型；武器近战技能会在战斗中实时读取该值覆盖技能默认伤害类型。
@export var weapon_physical_damage_tag: StringName = &""


func get_effective_max_stack() -> int:
	if not is_stackable:
		return 1
	return maxi(int(max_stack), 1)


func get_base_price() -> int:
	return maxi(int(base_price), 0)


func get_buy_price(price_multiplier: float = 1.0) -> int:
	var resolved_buy_price := int(buy_price)
	if resolved_buy_price <= 0:
		resolved_buy_price = get_base_price()
	return maxi(int(round(float(resolved_buy_price) * maxf(price_multiplier, 0.0))), 0)


func get_sell_price(price_multiplier: float = 0.5) -> int:
	if not sellable:
		return 0
	var resolved_sell_price := int(sell_price)
	if resolved_sell_price <= 0:
		resolved_sell_price = int(round(float(get_base_price()) * 0.5))
	return maxi(int(round(float(resolved_sell_price) * (maxf(price_multiplier, 0.0) / 0.5))), 0)


func get_tags() -> Array[StringName]:
	return _normalize_string_name_list(tags)


func get_crafting_groups() -> Array[StringName]:
	return _normalize_string_name_list(crafting_groups)


func get_quest_groups() -> Array[StringName]:
	return _normalize_string_name_list(quest_groups)


## 归一化读取 item_category：&"" 视作 misc，其余原样返回。
## 所有判定（has_equipment_category / is_skill_book / 注册校验 / UI payload）一律走该口径，
## 避免"未填实例"被静默归到 misc 而导致装备校验整段被跳过的旧 bug。
func get_item_category_normalized() -> StringName:
	if item_category == &"":
		return ITEM_CATEGORY_MISC
	return item_category


func has_equipment_category() -> bool:
	return get_item_category_normalized() == ITEM_CATEGORY_EQUIPMENT


func get_equipment_slot_ids() -> Array[StringName]:
	return EQUIPMENT_RULES_SCRIPT.normalize_slot_ids(equipment_slot_ids)


func is_equipment() -> bool:
	return has_equipment_category() and not get_equipment_slot_ids().is_empty()


func get_equipment_type_id_normalized() -> StringName:
	var normalized := ProgressionDataUtils.to_string_name(equipment_type_id)
	if get_valid_equipment_type_ids().has(normalized):
		return normalized
	return &""


func has_valid_equipment_type() -> bool:
	return get_equipment_type_id_normalized() != &""


func is_weapon() -> bool:
	return get_equipment_type_id_normalized() == EQUIPMENT_TYPE_WEAPON


func get_weapon_physical_damage_tag() -> StringName:
	var normalized := ProgressionDataUtils.to_string_name(weapon_physical_damage_tag)
	if is_weapon() and get_valid_weapon_physical_damage_tags().has(normalized):
		return normalized
	return &""


func is_armor() -> bool:
	return get_equipment_type_id_normalized() == EQUIPMENT_TYPE_ARMOR


func is_accessory() -> bool:
	return get_equipment_type_id_normalized() == EQUIPMENT_TYPE_ACCESSORY


func is_skill_book() -> bool:
	return get_item_category_normalized() == ITEM_CATEGORY_SKILL_BOOK and granted_skill_id != &""


func get_attribute_modifiers() -> Array[AttributeModifier]:
	var modifiers: Array[AttributeModifier] = attribute_modifiers.duplicate()
	if is_weapon() and weapon_attack_range > 0:
		var range_modifier := AttributeModifier.new()
		range_modifier.attribute_id = &"weapon_attack_range"
		range_modifier.mode = AttributeModifier.MODE_FLAT
		range_modifier.value = weapon_attack_range
		range_modifier.source_type = &"equipment"
		range_modifier.source_id = item_id
		modifiers.append(range_modifier)
	return modifiers


static func get_valid_equipment_type_ids() -> Array[StringName]:
	return [
		EQUIPMENT_TYPE_WEAPON,
		EQUIPMENT_TYPE_ARMOR,
		EQUIPMENT_TYPE_ACCESSORY,
	]


static func get_valid_weapon_physical_damage_tags() -> Array[StringName]:
	return [
		DAMAGE_TAG_PHYSICAL_SLASH,
		DAMAGE_TAG_PHYSICAL_PIERCE,
		DAMAGE_TAG_PHYSICAL_BLUNT,
	]


## 返回装备到 entry_slot_id 时实际占用的槽位集合。
## 若 occupied_slot_ids 非空，则使用显式声明（如双手武器）；否则只占入口槽本身。
func get_final_occupied_slot_ids(entry_slot_id: StringName) -> Array[StringName]:
	if not occupied_slot_ids.is_empty():
		return EQUIPMENT_RULES_SCRIPT.normalize_slot_ids(occupied_slot_ids)
	var norm := ProgressionDataUtils.to_string_name(entry_slot_id)
	if EQUIPMENT_RULES_SCRIPT.is_valid_slot(norm):
		var result: Array[StringName] = [norm]
		return result
	return []


func _normalize_string_name_list(values: Array) -> Array[StringName]:
	var normalized_values: Array[StringName] = []
	for raw_value in values:
		var normalized_value := ProgressionDataUtils.to_string_name(raw_value)
		if normalized_value == &"":
			continue
		normalized_values.append(normalized_value)
	return normalized_values
