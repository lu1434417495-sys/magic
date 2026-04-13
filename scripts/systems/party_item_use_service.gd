## 文件说明：该脚本属于队伍物品使用服务相关的服务脚本，集中处理共享仓库内可使用物品的校验与结算。
## 审查重点：重点核对技能书消耗顺序、失败分支是否会误吞库存，以及角色与仓库状态的归属边界。
## 备注：当前仅实现技能书，后续新增可使用道具时应继续复用该服务而不是把规则散落到 UI。

class_name PartyItemUseService
extends RefCounted

const PARTY_STATE_SCRIPT = preload("res://scripts/player/progression/party_state.gd")

var _party_state = PARTY_STATE_SCRIPT.new()
var _item_defs: Dictionary = {}
var _skill_defs: Dictionary = {}
var _warehouse_service: PartyWarehouseService = null
var _character_management: CharacterManagementModule = null


func setup(
	party_state,
	item_defs: Dictionary,
	skill_defs: Dictionary,
	warehouse_service: PartyWarehouseService,
	character_management: CharacterManagementModule
) -> void:
	_party_state = party_state if party_state != null else PARTY_STATE_SCRIPT.new()
	_item_defs = item_defs if item_defs != null else {}
	_skill_defs = skill_defs if skill_defs != null else {}
	_warehouse_service = warehouse_service
	_character_management = character_management


func use_item(item_id: StringName, member_id: StringName) -> Dictionary:
	var normalized_item_id := ProgressionDataUtils.to_string_name(item_id)
	var normalized_member_id := ProgressionDataUtils.to_string_name(member_id)
	var result := {
		"success": false,
		"reason": &"invalid_request",
		"item_id": normalized_item_id,
		"member_id": normalized_member_id,
		"skill_id": StringName(""),
		"consumed_quantity": 0,
	}
	if normalized_item_id == &"" or normalized_member_id == &"":
		return result
	if _party_state == null or _warehouse_service == null or _character_management == null:
		result["reason"] = &"service_unavailable"
		return result

	var item_def: ItemDef = _item_defs.get(normalized_item_id) as ItemDef
	if item_def == null:
		result["reason"] = &"missing_item_def"
		return result
	if not item_def.is_skill_book():
		result["reason"] = &"item_not_usable"
		return result

	var member_state = _party_state.get_member_state(normalized_member_id)
	if member_state == null or member_state.progression == null:
		result["reason"] = &"missing_member"
		return result
	if _warehouse_service.count_item(normalized_item_id) <= 0:
		result["reason"] = &"missing_inventory"
		return result

	var skill_id: StringName = item_def.granted_skill_id
	var skill_def: SkillDef = _skill_defs.get(skill_id) as SkillDef
	result["skill_id"] = skill_id
	if skill_def == null:
		result["reason"] = &"missing_skill_def"
		return result
	if not _character_management.learn_skill(normalized_member_id, skill_id):
		result["reason"] = &"learn_failed"
		return result

	var remove_result := _warehouse_service.remove_item(normalized_item_id, 1)
	var removed_quantity := int(remove_result.get("removed_quantity", 0))
	if removed_quantity <= 0:
		result["reason"] = &"consume_failed"
		return result

	result["success"] = true
	result["reason"] = &"ok"
	result["consumed_quantity"] = removed_quantity
	return result
