extends SceneTree

const CharacterManagementModule = preload("res://scripts/systems/progression/character_management_module.gd")
const MisfortuneBlackOmenService = preload("res://scripts/systems/progression/misfortune_black_omen_service.gd")
const EquipmentRules = preload("res://scripts/player/equipment/equipment_rules.gd")
const EquipmentInstanceState = preload("res://scripts/player/warehouse/equipment_instance_state.gd")
const PartyMemberState = preload("res://scripts/player/progression/party_member_state.gd")
const PartyState = preload("res://scripts/player/progression/party_state.gd")
const ItemDef = preload("res://scripts/player/warehouse/item_def.gd")

const HERO_ID: StringName = &"hero"

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_cursed_relic_elite_or_boss_victory_hook_grants_doom_mark()
	_test_boss_curse_survival_victory_hook_grants_doom_mark()

	if _failures.is_empty():
		print("Misfortune black omen regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Misfortune black omen regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_cursed_relic_elite_or_boss_victory_hook_grants_doom_mark() -> void:
	var context := _build_context_with_cursed_relic()
	var service: MisfortuneBlackOmenService = context.get("service") as MisfortuneBlackOmenService
	var manager: CharacterManagementModule = context.get("manager") as CharacterManagementModule
	if service == null or manager == null:
		_assert_true(false, "cursed relic hook 前置构建失败。")
		return

	var result := service.try_run_hook(
		MisfortuneBlackOmenService.HOOK_CURSED_RELIC_ELITE_OR_BOSS_VICTORY,
		{
			"member_id": HERO_ID,
			"winner_faction_id": "player",
			"defeated_elite_or_boss": true,
		}
	)

	_assert_true(bool(result.get("ok", false)), "诅咒遗物 hook 应完成受控评估。")
	_assert_true(bool(result.get("conditions_met", false)), "携带诅咒遗物并击败 elite/boss 时应满足黑兆条件。")
	_assert_true(bool(result.get("granted", false)), "满足诅咒遗物黑兆条件时应直接写入 doom_marked。")
	_assert_eq(_get_doom_marked_value(manager), 1, "诅咒遗物黑兆 hook 应把 doom_marked 写成 1。")


func _test_boss_curse_survival_victory_hook_grants_doom_mark() -> void:
	var context := _build_context_without_relic()
	var service: MisfortuneBlackOmenService = context.get("service") as MisfortuneBlackOmenService
	var manager: CharacterManagementModule = context.get("manager") as CharacterManagementModule
	if service == null or manager == null:
		_assert_true(false, "boss curse hook 前置构建失败。")
		return

	var result := service.try_run_hook(
		MisfortuneBlackOmenService.HOOK_BOSS_CURSE_SURVIVAL_VICTORY,
		{
			"member_id": HERO_ID,
			"winner_faction_id": "player",
			"boss_encounter": true,
			"member_survived": true,
			"boss_curse_status_ids": [&"black_star_brand"],
		}
	)

	_assert_true(bool(result.get("ok", false)), "boss curse hook 应完成受控评估。")
	_assert_true(bool(result.get("conditions_met", false)), "boss 专属诅咒下存活获胜时应满足黑兆条件。")
	_assert_true(bool(result.get("granted", false)), "满足 boss curse 黑兆条件时应直接写入 doom_marked。")
	_assert_eq(_get_doom_marked_value(manager), 1, "boss curse 黑兆 hook 应把 doom_marked 写成 1。")


func _build_context_with_cursed_relic() -> Dictionary:
	var cursed_relic := _build_item_def(
		&"cursed_black_crown_shard",
		[&"cursed", &"relic"],
		[EquipmentRules.ACCESSORY_1]
	)
	var context := _build_context({cursed_relic.item_id: cursed_relic})
	var member_state: PartyMemberState = context.get("member_state") as PartyMemberState
	if member_state != null:
		member_state.equipment_state.set_equipped_entry(
			EquipmentRules.ACCESSORY_1,
			cursed_relic.item_id,
			[EquipmentRules.ACCESSORY_1],
			EquipmentInstanceState.create(cursed_relic.item_id, &"eq_black_omen_cursed_relic")
		)
	return context


func _build_context_without_relic() -> Dictionary:
	return _build_context({})


func _build_context(item_defs: Dictionary) -> Dictionary:
	var party_state := PartyState.new()
	party_state.leader_member_id = HERO_ID
	party_state.main_character_member_id = HERO_ID
	party_state.active_member_ids = [HERO_ID]

	var member_state := PartyMemberState.new()
	member_state.member_id = HERO_ID
	member_state.display_name = "Hero"
	member_state.progression.unit_id = HERO_ID
	member_state.progression.display_name = "Hero"
	member_state.progression.character_level = 20
	member_state.progression.unit_base_attributes.set_attribute_value(MisfortuneBlackOmenService.DOOM_MARKED_STAT_ID, 0)
	party_state.set_member_state(member_state)

	var manager := CharacterManagementModule.new()
	manager.setup(party_state, {}, {}, {})

	var service := MisfortuneBlackOmenService.new()
	service.setup(manager, item_defs)

	return {
		"party_state": party_state,
		"member_state": member_state,
		"manager": manager,
		"service": service,
	}


func _build_item_def(item_id: StringName, tags: Array[StringName], slot_ids: Array[StringName]) -> ItemDef:
	var item_def := ItemDef.new()
	item_def.item_id = item_id
	item_def.display_name = String(item_id)
	item_def.item_category = ItemDef.ITEM_CATEGORY_EQUIPMENT
	item_def.is_stackable = false
	item_def.max_stack = 1
	item_def.equipment_type_id = ItemDef.EQUIPMENT_TYPE_ACCESSORY
	item_def.equipment_slot_ids = []
	for slot_id in slot_ids:
		item_def.equipment_slot_ids.append(String(slot_id))
	item_def.tags = tags.duplicate()
	return item_def


func _get_doom_marked_value(manager: CharacterManagementModule) -> int:
	var member_state: PartyMemberState = manager.get_member_state(HERO_ID)
	if member_state == null or member_state.progression == null or member_state.progression.unit_base_attributes == null:
		return 0
	return member_state.progression.unit_base_attributes.get_attribute_value(MisfortuneBlackOmenService.DOOM_MARKED_STAT_ID)


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
