## 文件说明：该脚本属于聚落重铸服务相关的服务脚本，集中处理配方筛选、设施标签校验和仓库原子换料逻辑。
## 审查重点：重点核对配方选择、材料校验、仓库原子提交和 SettlementServiceResult 的 canonical 字段是否保持稳定。
## 备注：当前实现通用 forge / `service_master_reforge` 的最小配方闭环，不承载装备耐久、正式锻造面板或经济调优。

class_name SettlementForgeService
extends RefCounted

const RECIPE_CONTENT_REGISTRY_SCRIPT = preload("res://scripts/player/warehouse/recipe_content_registry.gd")
const SETTLEMENT_SERVICE_RESULT_SCRIPT = preload("res://scripts/systems/settlement_service_result.gd")

const MASTER_REFORGE_INTERACTION_ID := "service_master_reforge"
const GENERIC_FORGE_INTERACTION_IDS := {
	"service_repair_gear": true,
}

var _recipe_registry = RECIPE_CONTENT_REGISTRY_SCRIPT.new()


func is_supported_interaction(interaction_script_id: String) -> bool:
	var normalized_interaction_id := interaction_script_id.strip_edges()
	return normalized_interaction_id == MASTER_REFORGE_INTERACTION_ID or GENERIC_FORGE_INTERACTION_IDS.has(normalized_interaction_id)


func has_available_recipe(
	settlement: Dictionary,
	payload: Dictionary,
	item_defs: Dictionary,
	recipe_defs: Dictionary = {}
) -> bool:
	var resolved_recipe_defs := _resolve_recipe_defs(item_defs, recipe_defs)
	if resolved_recipe_defs.is_empty():
		return false
	return _resolve_recipe(settlement, payload, resolved_recipe_defs, null) != null


func has_available_master_reforge_recipe(
	settlement: Dictionary,
	payload: Dictionary,
	item_defs: Dictionary,
	recipe_defs: Dictionary = {}
) -> bool:
	return has_available_recipe(settlement, payload, item_defs, recipe_defs)


func execute_recipe(
	settlement: Dictionary,
	payload: Dictionary,
	item_defs: Dictionary,
	recipe_defs: Dictionary,
	warehouse_service,
	party_state,
	quest_progress_events: Array = []
) -> Dictionary:
	var service_profile := _resolve_service_profile(payload)
	if warehouse_service == null or party_state == null:
		return _build_result(false, "当前工坊服务尚未准备完成。", quest_progress_events)

	var resolved_recipe_defs := _resolve_recipe_defs(item_defs, recipe_defs)
	if resolved_recipe_defs.is_empty():
		return _build_result(false, "当前配方配置缺失，暂时无法执行。", quest_progress_events)

	var recipe = _resolve_recipe(settlement, payload, resolved_recipe_defs, warehouse_service)
	if recipe == null:
		return _build_result(false, String(service_profile.get("no_recipe_message", "当前工坊没有可执行的配方。")), quest_progress_events)

	var input_validation := _validate_recipe_items(recipe, item_defs)
	if not bool(input_validation.get("ok", false)):
		return _build_result(false, String(input_validation.get("message", "当前配方引用了无效物品。")), quest_progress_events)

	var withdrawal_items := _expand_input_items(recipe)
	var deposit_items := _build_repeated_item_array(recipe.output_item_id, int(recipe.output_quantity))
	var preview_result: Dictionary = warehouse_service.preview_batch_swap(withdrawal_items, deposit_items)
	if not bool(preview_result.get("allowed", false)):
		return _build_result(
			false,
			_build_failed_forge_message(recipe, item_defs, warehouse_service, preview_result, payload),
			quest_progress_events
		)

	var commit_result: Dictionary = warehouse_service.commit_batch_swap(withdrawal_items, deposit_items)
	if not bool(commit_result.get("allowed", false)):
		return _build_result(
			false,
			_build_failed_forge_message(recipe, item_defs, warehouse_service, commit_result, payload),
			quest_progress_events
		)

	var output_item_def = item_defs.get(recipe.output_item_id)
	var message := _build_success_message(recipe, item_defs, settlement, payload, output_item_def)
	return _build_result(
		true,
		message,
		quest_progress_events,
		true,
		{
			"recipe_id": String(recipe.recipe_id),
			"removed_entries": _build_recipe_entry_variants(recipe.input_item_ids, recipe.input_item_quantities),
			"added_entries": _build_recipe_entry_variants([recipe.output_item_id], PackedInt32Array([int(recipe.output_quantity)])),
		},
		{
			"recipe_id": String(recipe.recipe_id),
			"facility_tags": _build_facility_tags(settlement, payload),
			"output_item_id": String(recipe.output_item_id),
			"output_quantity": int(recipe.output_quantity),
		}
	)


func execute_master_reforge(
	settlement: Dictionary,
	payload: Dictionary,
	item_defs: Dictionary,
	recipe_defs: Dictionary,
	warehouse_service,
	party_state,
	quest_progress_events: Array = []
) -> Dictionary:
	return execute_recipe(settlement, payload, item_defs, recipe_defs, warehouse_service, party_state, quest_progress_events)


func build_window_data(
	interaction_script_id: String,
	settlement_record: Dictionary,
	payload: Dictionary,
	item_defs: Dictionary,
	recipe_defs: Dictionary,
	warehouse_service,
	feedback_text: String = ""
) -> Dictionary:
	var service_profile := _resolve_service_profile(payload, interaction_script_id)
	var resolved_recipe_defs := _resolve_recipe_defs(item_defs, recipe_defs)
	var recipe_entries := _build_recipe_window_entries(
		settlement_record,
		payload,
		item_defs,
		resolved_recipe_defs,
		warehouse_service,
		interaction_script_id
	)
	var facility_name := String(payload.get("facility_name", service_profile.get("default_facility_name", "工坊")))
	var settlement_name := String(settlement_record.get("display_name", "据点"))
	var summary_text := String(service_profile.get("summary_text", "选择一个配方后即可消耗材料并将结果原子写入共享仓库。"))
	if recipe_entries.is_empty():
		summary_text = String(service_profile.get("empty_summary_text", "当前没有可用的配方。"))
	return {
		"title": "%s · %s" % [settlement_name, String(service_profile.get("title_suffix", "工坊"))],
		"meta": "工坊：%s  |  规则：消耗材料并原子写入共享仓库。" % facility_name,
		"summary_text": summary_text,
		"state_summary_text": String(payload.get("state_summary_text", "")),
		"feedback_text": feedback_text if not feedback_text.is_empty() else String(service_profile.get("default_feedback_text", "选择一条配方后即可执行配方操作。")),
		"settlement_id": String(settlement_record.get("settlement_id", "")),
		"interaction_script_id": interaction_script_id,
		"action_id": String(payload.get("action_id", service_profile.get("action_id", _build_default_action_id(interaction_script_id)))),
		"facility_id": String(payload.get("facility_id", "")),
		"facility_name": facility_name,
		"npc_id": String(payload.get("npc_id", "")),
		"npc_name": String(payload.get("npc_name", "")),
		"service_type": String(payload.get("service_type", service_profile.get("service_type", "工坊"))),
		"panel_kind": "forge",
		"confirm_label": String(service_profile.get("confirm_label", "确认")),
		"cancel_label": "返回",
		"show_member_selector": false,
		"allow_empty_entries": true,
		"entries": recipe_entries,
	}


func _resolve_recipe(settlement: Dictionary, payload: Dictionary, recipe_defs: Dictionary, warehouse_service):
	if recipe_defs.is_empty():
		return null

	var requested_recipe_id := ProgressionDataUtils.to_string_name(payload.get("recipe_id", ""))
	if requested_recipe_id != &"":
		var requested_recipe = recipe_defs.get(requested_recipe_id)
		if requested_recipe == null:
			return null
		return requested_recipe if _recipe_matches_facility(requested_recipe, settlement, payload) else null

	var matched_recipes: Array = []
	for recipe_variant in recipe_defs.values():
		var recipe = recipe_variant
		if recipe == null:
			continue
		if not _recipe_matches_facility(recipe, settlement, payload):
			continue
		matched_recipes.append(recipe)

	if matched_recipes.is_empty():
		return null
	if warehouse_service == null:
		return matched_recipes[0]

	for recipe in matched_recipes:
		if _can_fulfill_recipe_inputs(recipe, warehouse_service):
			return recipe
	return matched_recipes[0]


func _list_matching_recipes(settlement: Dictionary, payload: Dictionary, recipe_defs: Dictionary) -> Array:
	var matched_recipes: Array = []
	for recipe_id_str in ProgressionDataUtils.sorted_string_keys(recipe_defs):
		var recipe = recipe_defs.get(StringName(recipe_id_str))
		if recipe == null:
			recipe = recipe_defs.get(recipe_id_str)
		if recipe == null:
			continue
		if not _recipe_matches_facility(recipe, settlement, payload):
			continue
		matched_recipes.append(recipe)
	return matched_recipes


func _resolve_recipe_defs(item_defs: Dictionary, recipe_defs: Dictionary = {}) -> Dictionary:
	if recipe_defs != null and not recipe_defs.is_empty():
		return recipe_defs
	_recipe_registry.setup(item_defs)
	if not _recipe_registry.validate().is_empty():
		return {}
	return _recipe_registry.get_recipe_defs()


func _build_recipe_window_entries(
	settlement: Dictionary,
	payload: Dictionary,
	item_defs: Dictionary,
	recipe_defs: Dictionary,
	warehouse_service,
	interaction_script_id: String
) -> Array[Dictionary]:
	var entries: Array[Dictionary] = []
	for recipe in _list_matching_recipes(settlement, payload, recipe_defs):
		entries.append(_build_recipe_window_entry(recipe, settlement, payload, item_defs, warehouse_service, interaction_script_id))
	return entries


func _build_recipe_window_entry(
	recipe,
	settlement: Dictionary,
	payload: Dictionary,
	item_defs: Dictionary,
	warehouse_service,
	interaction_script_id: String
) -> Dictionary:
	var output_summary := _build_item_label(recipe.output_item_id, item_defs, int(recipe.output_quantity), item_defs.get(recipe.output_item_id))
	var material_summary := _build_recipe_input_summary(recipe, item_defs)
	var state_label := "状态：可重铸"
	var disabled_reason := ""
	var is_enabled := true
	var service_profile := _resolve_service_profile(payload, interaction_script_id)

	if warehouse_service == null:
		is_enabled = false
		state_label = "状态：不可用"
		disabled_reason = "共享仓库服务尚未准备完成。"
	elif not _can_fulfill_recipe_inputs(recipe, warehouse_service):
		is_enabled = false
		state_label = "状态：材料不足"
		disabled_reason = "缺少材料：%s。" % "、".join(_build_missing_input_entries(recipe, item_defs, warehouse_service))
	else:
		var preview_result: Dictionary = warehouse_service.preview_batch_swap(
			_expand_input_items(recipe),
			_build_repeated_item_array(recipe.output_item_id, int(recipe.output_quantity))
		)
		if not bool(preview_result.get("allowed", false)):
			is_enabled = false
			state_label = "状态：无法写入"
			disabled_reason = _build_failed_forge_message(recipe, item_defs, warehouse_service, preview_result, payload)

	var details_text := String(recipe.description)
	if details_text.is_empty():
		details_text = "消耗 %s，可%s %s。" % [
			material_summary,
			String(service_profile.get("recipe_action_phrase", "制作为")),
			output_summary,
		]
	else:
		details_text += "\n消耗：%s\n产出：%s" % [material_summary, output_summary]
	var facility_tags := _build_facility_tags(settlement, payload)
	if not facility_tags.is_empty():
		details_text += "\n设施标签：%s" % " / ".join(PackedStringArray(facility_tags))

	return {
		"entry_id": "recipe:%s" % String(recipe.recipe_id),
		"recipe_id": String(recipe.recipe_id),
		"display_name": String(recipe.display_name if not String(recipe.display_name).is_empty() else recipe.recipe_id),
		"summary_text": "%s -> %s" % [material_summary, output_summary],
		"details_text": details_text,
		"state_label": state_label,
		"cost_label": "材料：%s" % material_summary,
		"is_enabled": is_enabled,
		"disabled_reason": disabled_reason,
		"interaction_script_id": interaction_script_id,
	}


func _recipe_matches_facility(recipe, settlement: Dictionary, payload: Dictionary) -> bool:
	var required_tags: Array = recipe.required_facility_tags
	if required_tags.is_empty():
		return true
	var available_tags := _build_facility_tag_set(settlement, payload)
	for raw_tag in required_tags:
		var normalized_tag := ProgressionDataUtils.to_string_name(raw_tag)
		if normalized_tag == &"":
			continue
		if not available_tags.has(normalized_tag):
			return false
	return true


func _build_facility_tag_set(settlement: Dictionary, payload: Dictionary) -> Dictionary:
	var tags: Dictionary = {}
	var facility := _resolve_facility(settlement, payload)
	for raw_tag in _build_facility_tags(settlement, payload):
		var normalized_tag := ProgressionDataUtils.to_string_name(raw_tag)
		if normalized_tag == &"":
			continue
		tags[normalized_tag] = true
	if not facility.is_empty():
		var facility_template_id := ProgressionDataUtils.to_string_name(_resolve_facility_template_id(facility))
		if facility_template_id != &"":
			tags[facility_template_id] = true
	return tags


func _build_facility_tags(settlement: Dictionary, payload: Dictionary) -> Array[String]:
	var tags: Array[String] = []
	var facility := _resolve_facility(settlement, payload)
	var interaction_script_id := String(payload.get("interaction_script_id", ""))
	var push_tag = func(raw_value) -> void:
		var raw_text := String(raw_value)
		if raw_text.is_empty():
			return
		if tags.has(raw_text):
			return
		tags.append(raw_text)

	push_tag.call(interaction_script_id)
	push_tag.call(payload.get("service_type", ""))

	if not facility.is_empty():
		push_tag.call(_resolve_facility_template_id(facility))
		push_tag.call(facility.get("category", ""))
		push_tag.call(facility.get("interaction_type", ""))
		push_tag.call(facility.get("slot_tag", ""))
		var category := String(facility.get("category", ""))
		var interaction_type := String(facility.get("interaction_type", ""))
		if interaction_type == "craft" or category == "craft" or category == "support":
			push_tag.call("forge")
			push_tag.call("craft")

	if is_supported_interaction(interaction_script_id):
		push_tag.call("forge")
		push_tag.call("craft")

	if _is_master_reforge_interaction(interaction_script_id):
		push_tag.call("master_reforge")
	return tags


func _resolve_facility(settlement: Dictionary, payload: Dictionary) -> Dictionary:
	var target_facility_id := String(payload.get("facility_id", ""))
	var target_facility_template_id := String(payload.get("facility_template_id", "")).strip_edges()
	for facility_variant in settlement.get("facilities", []):
		if facility_variant is not Dictionary:
			continue
		var facility: Dictionary = facility_variant
		if not target_facility_id.is_empty() and String(facility.get("facility_id", "")) == target_facility_id:
			return facility
		if not target_facility_template_id.is_empty() and _resolve_facility_template_id(facility) == target_facility_template_id:
			return facility
	return {}


func _resolve_facility_template_id(facility: Dictionary) -> String:
	if facility.is_empty():
		return ""
	return String(facility.get("template_id", facility.get("facility_id", ""))).strip_edges()


func _can_fulfill_recipe_inputs(recipe, warehouse_service) -> bool:
	for input_index in range(recipe.input_item_ids.size()):
		var item_id := ProgressionDataUtils.to_string_name(recipe.input_item_ids[input_index])
		var required_quantity := int(recipe.input_item_quantities[input_index])
		if warehouse_service.count_item(item_id) < required_quantity:
			return false
	return true


func _validate_recipe_items(recipe, item_defs: Dictionary) -> Dictionary:
	for input_item_id in recipe.input_item_ids:
		var normalized_input := ProgressionDataUtils.to_string_name(input_item_id)
		if normalized_input == &"" or not item_defs.has(normalized_input):
			return {
				"ok": false,
				"message": "配方 %s 引用了缺失的输入物品 %s。" % [String(recipe.recipe_id), String(normalized_input)],
			}
	if recipe.output_item_id == &"" or not item_defs.has(recipe.output_item_id):
		return {
			"ok": false,
			"message": "配方 %s 引用了缺失的产出物品 %s。" % [String(recipe.recipe_id), String(recipe.output_item_id)],
		}
	return {"ok": true}


func _build_failed_forge_message(recipe, item_defs: Dictionary, warehouse_service, warehouse_result: Dictionary, payload: Dictionary = {}) -> String:
	var service_profile := _resolve_service_profile(payload)
	var error_code := String(warehouse_result.get("error_code", ""))
	if error_code == "warehouse_blocked_swap":
		return String(service_profile.get("blocked_output_message", "共享仓库空间不足，无法放入配方成品。"))
	if error_code == "warehouse_missing_item":
		var missing_items := _build_missing_input_entries(recipe, item_defs, warehouse_service)
		if not missing_items.is_empty():
			return "%s%s。" % [String(service_profile.get("missing_material_prefix", "缺少配方材料：")), "、".join(missing_items)]
	return recipe.failure_reason if not recipe.failure_reason.is_empty() else String(service_profile.get("fallback_failure_message", "当前无法完成该配方。"))


func _build_missing_input_entries(recipe, item_defs: Dictionary, warehouse_service) -> Array[String]:
	var missing_entries: Array[String] = []
	for input_index in range(recipe.input_item_ids.size()):
		var item_id := ProgressionDataUtils.to_string_name(recipe.input_item_ids[input_index])
		var required_quantity := int(recipe.input_item_quantities[input_index])
		var owned_quantity: int = warehouse_service.count_item(item_id)
		if owned_quantity >= required_quantity:
			continue
		var shortage: int = required_quantity - owned_quantity
		missing_entries.append(_build_item_label(item_id, item_defs, shortage, item_defs.get(item_id)))
	return missing_entries


func _expand_input_items(recipe) -> Array[StringName]:
	var item_ids: Array[StringName] = []
	for input_index in range(recipe.input_item_ids.size()):
		var item_id := ProgressionDataUtils.to_string_name(recipe.input_item_ids[input_index])
		var quantity := int(recipe.input_item_quantities[input_index])
		item_ids.append_array(_build_repeated_item_array(item_id, quantity))
	return item_ids


func _build_repeated_item_array(item_id: StringName, quantity: int) -> Array[StringName]:
	var item_ids: Array[StringName] = []
	var resolved_quantity := maxi(quantity, 0)
	for _index in range(resolved_quantity):
		item_ids.append(item_id)
	return item_ids


func _build_recipe_input_summary(recipe, item_defs: Dictionary) -> String:
	var parts: Array[String] = []
	for input_index in range(recipe.input_item_ids.size()):
		var item_id := ProgressionDataUtils.to_string_name(recipe.input_item_ids[input_index])
		var quantity := int(recipe.input_item_quantities[input_index])
		parts.append(_build_item_label(item_id, item_defs, quantity, item_defs.get(item_id)))
	return "、".join(parts)


func _build_recipe_entry_variants(item_ids: Array, quantities: PackedInt32Array) -> Array[Dictionary]:
	var entries: Array[Dictionary] = []
	for entry_index in range(item_ids.size()):
		var item_id := ProgressionDataUtils.to_string_name(item_ids[entry_index])
		var quantity := int(quantities[entry_index]) if entry_index < quantities.size() else 0
		if item_id == &"" or quantity <= 0:
			continue
		entries.append({
			"item_id": String(item_id),
			"quantity": quantity,
		})
	return entries


func _build_item_label(item_id: StringName, item_defs: Dictionary, quantity: int, item_def = null) -> String:
	var display_name := String(item_id)
	if item_def == null:
		item_def = item_defs.get(item_id) as ItemDef
	if item_def != null and not item_def.display_name.is_empty():
		display_name = item_def.display_name
	return "%d 件 %s" % [maxi(quantity, 0), display_name]


func _build_success_message(recipe, item_defs: Dictionary, settlement: Dictionary, payload: Dictionary, output_item_def = null) -> String:
	var service_profile := _resolve_service_profile(payload)
	var input_summary := _build_recipe_input_summary(recipe, item_defs)
	var output_summary := _build_item_label(recipe.output_item_id, item_defs, int(recipe.output_quantity), output_item_def)
	if _is_master_reforge_interaction(String(payload.get("interaction_script_id", ""))):
		return "大师工坊已将 %s 重铸为 %s。" % [input_summary, output_summary]
	var actor_label := String(payload.get("npc_name", ""))
	if actor_label.is_empty():
		actor_label = String(payload.get("facility_name", ""))
	if actor_label.is_empty():
		actor_label = String(settlement.get("display_name", service_profile.get("default_facility_name", "工坊")))
	return "%s 已将 %s %s %s。" % [
		actor_label,
		input_summary,
		String(service_profile.get("recipe_action_phrase", "制作为")),
		output_summary,
	]


func _resolve_service_profile(payload: Dictionary, interaction_script_id: String = "") -> Dictionary:
	var resolved_interaction_id := interaction_script_id.strip_edges()
	if resolved_interaction_id.is_empty():
		resolved_interaction_id = String(payload.get("interaction_script_id", "")).strip_edges()
	if _is_master_reforge_interaction(resolved_interaction_id):
		return {
			"title_suffix": "大师重铸",
			"summary_text": "选择一个配方后即可消耗材料并将结果原子写入共享仓库。",
			"empty_summary_text": "当前没有可用的重铸配方。",
			"default_feedback_text": "选择一条配方后即可执行重铸。",
			"confirm_label": "重铸",
			"service_type": "重铸",
			"recipe_action_phrase": "重铸为",
			"no_recipe_message": "当前大师工坊没有可执行的重铸配方。",
			"fallback_failure_message": "当前无法完成该重铸。",
			"blocked_output_message": "共享仓库空间不足，无法放入重铸成品。",
			"missing_material_prefix": "缺少重铸材料：",
			"default_facility_name": "大师工坊",
			"action_id": "service:master_reforge",
		}
	var generic_title_suffix := String(payload.get("service_type", "锻造")).strip_edges()
	if generic_title_suffix.is_empty():
		generic_title_suffix = "锻造"
	return {
		"title_suffix": generic_title_suffix,
		"summary_text": "选择一个配方后即可消耗材料并将结果原子写入共享仓库。",
		"empty_summary_text": "当前没有可用的锻造配方。",
		"default_feedback_text": "选择一条配方后即可执行%s。" % generic_title_suffix,
		"confirm_label": generic_title_suffix,
		"service_type": generic_title_suffix,
		"recipe_action_phrase": "打造为",
		"no_recipe_message": "当前工坊没有可执行的锻造配方。",
		"fallback_failure_message": "当前无法完成该锻造。",
		"blocked_output_message": "共享仓库空间不足，无法放入锻造成品。",
		"missing_material_prefix": "缺少配方材料：",
		"default_facility_name": "工坊",
		"action_id": _build_default_action_id(resolved_interaction_id),
	}


func _build_default_action_id(interaction_script_id: String) -> String:
	var normalized_interaction_id := interaction_script_id.strip_edges()
	if normalized_interaction_id.begins_with("service_"):
		return "service:%s" % normalized_interaction_id.trim_prefix("service_")
	return "service:master_reforge"


func _is_master_reforge_interaction(interaction_script_id: String) -> bool:
	return interaction_script_id.strip_edges() == MASTER_REFORGE_INTERACTION_ID


func _build_result(
	success: bool,
	message: String,
	quest_progress_events: Array,
	persist_party_state: bool = false,
	inventory_delta: Dictionary = {},
	service_side_effects: Dictionary = {}
) -> Dictionary:
	var result := SETTLEMENT_SERVICE_RESULT_SCRIPT.new()
	result.success = success
	result.message = message
	result.persist_party_state = persist_party_state
	result.inventory_delta = inventory_delta.duplicate(true) if inventory_delta is Dictionary else {}
	result.quest_progress_events = _duplicate_dictionary_array(quest_progress_events)
	result.service_side_effects = service_side_effects.duplicate(true) if service_side_effects is Dictionary else {}
	return result.to_dictionary()


func _duplicate_dictionary_array(value) -> Array[Dictionary]:
	var result: Array[Dictionary] = []
	if value is not Array:
		return result
	for entry_variant in value:
		if entry_variant is Dictionary:
			result.append((entry_variant as Dictionary).duplicate(true))
	return result
