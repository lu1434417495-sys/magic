extends SceneTree

const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")
const BATTLE_EVENT_BATCH_SCRIPT = preload("res://scripts/systems/battle/core/battle_event_batch.gd")
const BATTLE_FATE_EVENT_BUS_SCRIPT = preload("res://scripts/systems/battle/fate/battle_fate_event_bus.gd")
const BATTLE_RESOLUTION_RESULT_SCRIPT = preload("res://scripts/systems/battle/core/battle_resolution_result.gd")
const BATTLE_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_state.gd")
const BATTLE_STATUS_EFFECT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_status_effect_state.gd")
const BATTLE_TIMELINE_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_timeline_state.gd")
const BATTLE_UNIT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const BATTLE_CELL_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_cell_state.gd")
const CHARACTER_MANAGEMENT_MODULE_SCRIPT = preload("res://scripts/systems/progression/character_management_module.gd")
const COMBAT_EFFECT_DEF_SCRIPT = preload("res://scripts/player/progression/combat_effect_def.gd")
const EQUIPMENT_RULES_SCRIPT = preload("res://scripts/player/equipment/equipment_rules.gd")
const EQUIPMENT_INSTANCE_STATE_SCRIPT = preload("res://scripts/player/warehouse/equipment_instance_state.gd")
const GAME_RUNTIME_SETTLEMENT_COMMAND_HANDLER_SCRIPT = preload("res://scripts/systems/game_runtime/game_runtime_settlement_command_handler.gd")
const ITEM_CONTENT_REGISTRY_SCRIPT = preload("res://scripts/player/warehouse/item_content_registry.gd")
const LOW_LUCK_EVENT_SERVICE_SCRIPT = preload("res://scripts/systems/battle/fate/low_luck_event_service.gd")
const LOW_LUCK_RELIC_RULES_SCRIPT = preload("res://scripts/systems/fate/low_luck_relic_rules.gd")
const MISFORTUNE_BLACK_OMEN_SERVICE_SCRIPT = preload("res://scripts/systems/progression/misfortune_black_omen_service.gd")
const PARTY_MEMBER_STATE_SCRIPT = preload("res://scripts/player/progression/party_member_state.gd")
const PARTY_STATE_SCRIPT = preload("res://scripts/player/progression/party_state.gd")
const ATTRIBUTE_SNAPSHOT_SCRIPT = preload("res://scripts/player/progression/attribute_snapshot.gd")
const BATTLE_DAMAGE_RESOLVER_SCRIPT = preload("res://scripts/systems/battle/rules/battle_damage_resolver.gd")
const BATTLE_RUNTIME_MODULE_SCRIPT = preload("res://scripts/systems/battle/runtime/battle_runtime_module.gd")
const PROGRESSION_DATA_UTILS_SCRIPT = preload("res://scripts/player/progression/progression_data_utils.gd")

const BattleEventBatch = BATTLE_EVENT_BATCH_SCRIPT
const BattleFateEventBus = BATTLE_FATE_EVENT_BUS_SCRIPT
const BattleResolutionResult = BATTLE_RESOLUTION_RESULT_SCRIPT
const BattleState = BATTLE_STATE_SCRIPT
const BattleStatusEffectState = BATTLE_STATUS_EFFECT_STATE_SCRIPT
const BattleTimelineState = BATTLE_TIMELINE_STATE_SCRIPT
const BattleUnitState = BATTLE_UNIT_STATE_SCRIPT
const BattleCellState = BATTLE_CELL_STATE_SCRIPT
const CharacterManagementModule = CHARACTER_MANAGEMENT_MODULE_SCRIPT
const CombatEffectDef = COMBAT_EFFECT_DEF_SCRIPT
const EquipmentRules = EQUIPMENT_RULES_SCRIPT
const EquipmentInstanceState = EQUIPMENT_INSTANCE_STATE_SCRIPT
const GameRuntimeSettlementCommandHandler = GAME_RUNTIME_SETTLEMENT_COMMAND_HANDLER_SCRIPT
const ItemContentRegistry = ITEM_CONTENT_REGISTRY_SCRIPT
const LowLuckEventService = LOW_LUCK_EVENT_SERVICE_SCRIPT
const LowLuckRelicRules = LOW_LUCK_RELIC_RULES_SCRIPT
const MisfortuneBlackOmenService = MISFORTUNE_BLACK_OMEN_SERVICE_SCRIPT
const PartyMemberState = PARTY_MEMBER_STATE_SCRIPT
const PartyState = PARTY_STATE_SCRIPT
const AttributeSnapshot = ATTRIBUTE_SNAPSHOT_SCRIPT
const BattleDamageResolver = BATTLE_DAMAGE_RESOLVER_SCRIPT
const BattleRuntimeModule = BATTLE_RUNTIME_MODULE_SCRIPT
const ProgressionDataUtils = PROGRESSION_DATA_UTILS_SCRIPT

const HERO_ID: StringName = &"hero"
const ALLY_ID: StringName = &"ally"

var _failures: Array[String] = []


class SettlementRuntimeStub:
	extends RefCounted

	var party_state: PartyState = null
	var snapshots: Dictionary = {}


	func _init(party_state_value: PartyState, snapshot_map: Dictionary) -> void:
		party_state = party_state_value
		snapshots = snapshot_map.duplicate(true)


	func get_party_state():
		return party_state


	func get_member_attribute_snapshot(member_id: StringName):
		return snapshots.get(member_id, snapshots.get(String(member_id), null))


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_item_resources_surface_equipment_flags()
	_test_fixed_reward_pool_uses_low_luck_fixed_loot_path()
	_test_black_star_wedge_first_hit_ignores_guard_and_applies_exposed()
	_test_blood_debt_shawl_low_hp_reduction_ally_down_ap_and_recovery_penalty()
	_test_dead_road_lantern_reveals_hidden_paths_and_grants_black_omen_mark()

	if _failures.is_empty():
		print("Low luck relic regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Low luck relic regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_item_resources_surface_equipment_flags() -> void:
	var item_defs = _load_low_luck_item_defs()
	var cases = [
		{"item_id": LowLuckRelicRules.ITEM_REVERSE_FATE_AMULET, "attribute_id": LowLuckRelicRules.ATTR_REVERSE_FATE_AMULET},
		{"item_id": LowLuckRelicRules.ITEM_BLACK_STAR_WEDGE, "attribute_id": LowLuckRelicRules.ATTR_BLACK_STAR_WEDGE},
		{"item_id": LowLuckRelicRules.ITEM_BLOOD_DEBT_SHAWL, "attribute_id": LowLuckRelicRules.ATTR_BLOOD_DEBT_SHAWL},
		{"item_id": LowLuckRelicRules.ITEM_DEAD_ROAD_LANTERN, "attribute_id": LowLuckRelicRules.ATTR_DEAD_ROAD_LANTERN},
	]
	for case_data in cases:
		var item_id = ProgressionDataUtils.to_string_name(case_data.get("item_id", ""))
		var attribute_id = ProgressionDataUtils.to_string_name(case_data.get("attribute_id", ""))
		_assert_true(item_defs.has(item_id), "ItemContentRegistry 应加载 %s。" % String(item_id))
		if not item_defs.has(item_id):
			continue
		var snapshot = _build_equipped_member_snapshot(item_defs, item_id)
		_assert_true(
			snapshot != null and int(snapshot.get_value(attribute_id)) > 0,
			"%s 装备后应把 %s 写入属性快照。" % [String(item_id), String(attribute_id)]
		)


func _test_fixed_reward_pool_uses_low_luck_fixed_loot_path() -> void:
	var reverse_context = _build_low_luck_context(-5, true)
	var reverse_bus: BattleFateEventBus = reverse_context.get("bus") as BattleFateEventBus
	var reverse_service: LowLuckEventService = reverse_context.get("service") as LowLuckEventService
	reverse_bus.dispatch(
		BATTLE_FATE_EVENT_BUS_SCRIPT.EVENT_CRITICAL_FAIL,
		_build_critical_fail_payload(&"reverse_reward_battle", -5)
	)
	var reverse_result = reverse_service.handle_battle_resolution(
		_build_reward_battle_state(&"reverse_reward_battle", true, true, false),
		_build_battle_resolution_result(&"reverse_reward_battle")
	)
	_assert_fixed_loot_entry(
		reverse_result.get("loot_entries", []),
		LowLuckRelicRules.ITEM_REVERSE_FATE_AMULET,
		"逆命护符应只通过 low_luck fixed loot 路径发放。"
	)

	var wedge_context = _build_low_luck_context(-5, true)
	var wedge_bus: BattleFateEventBus = wedge_context.get("bus") as BattleFateEventBus
	var wedge_service: LowLuckEventService = wedge_context.get("service") as LowLuckEventService
	wedge_bus.dispatch(
		BATTLE_FATE_EVENT_BUS_SCRIPT.EVENT_HARDSHIP_SURVIVAL,
		_build_hardship_payload(&"wedge_reward_battle", -5)
	)
	var wedge_result = wedge_service.handle_battle_resolution(
		_build_reward_battle_state(&"wedge_reward_battle", true, true, false),
		_build_battle_resolution_result(&"wedge_reward_battle")
	)
	_assert_fixed_loot_entry(
		wedge_result.get("loot_entries", []),
		LowLuckRelicRules.ITEM_BLACK_STAR_WEDGE,
		"黑星楔钉应只通过 low_luck fixed loot 路径发放。"
	)

	var shawl_context = _build_low_luck_context(-5, true)
	var shawl_service: LowLuckEventService = shawl_context.get("service") as LowLuckEventService
	var shawl_result = shawl_service.handle_battle_resolution(
		_build_reward_battle_state(&"shawl_reward_battle", true, false, true),
		_build_battle_resolution_result(&"shawl_reward_battle")
	)
	_assert_fixed_loot_entry(
		shawl_result.get("loot_entries", []),
		LowLuckRelicRules.ITEM_BLOOD_DEBT_SHAWL,
		"血债披肩应只通过 low_luck fixed loot 路径发放。"
	)

	var lantern_context = _build_low_luck_context(-5, true)
	var lantern_bus: BattleFateEventBus = lantern_context.get("bus") as BattleFateEventBus
	var lantern_service: LowLuckEventService = lantern_context.get("service") as LowLuckEventService
	lantern_bus.dispatch(
		BATTLE_FATE_EVENT_BUS_SCRIPT.EVENT_HARDSHIP_SURVIVAL,
		_build_hardship_payload(&"lantern_reward_battle", -5)
	)
	lantern_bus.dispatch(
		BATTLE_FATE_EVENT_BUS_SCRIPT.EVENT_CRITICAL_FAIL,
		_build_critical_fail_payload(&"lantern_reward_battle", -5)
	)
	var lantern_result = lantern_service.handle_battle_resolution(
		_build_reward_battle_state(&"lantern_reward_battle", true, false, false),
		_build_battle_resolution_result(&"lantern_reward_battle")
	)
	_assert_fixed_loot_entry(
		lantern_result.get("loot_entries", []),
		LowLuckRelicRules.ITEM_DEAD_ROAD_LANTERN,
		"亡途灯笼应只通过 low_luck fixed loot 路径发放。"
	)


func _test_black_star_wedge_first_hit_ignores_guard_and_applies_exposed() -> void:
	var resolver = BattleDamageResolver.new()
	var baseline_source = _build_battle_unit("基准楔钉者", &"player")
	var baseline_target = _build_guarded_target("守住的敌人")
	var baseline_damage_result = resolver.resolve_effects(baseline_source, baseline_target, [_build_damage_effect(18)])
	var baseline_damage = int(baseline_damage_result.get("damage", 0))

	var wedge_source = _build_battle_unit("楔钉者", &"player")
	wedge_source.attribute_snapshot.set_value(LowLuckRelicRules.ATTR_BLACK_STAR_WEDGE, 1)
	var wedge_target = _build_guarded_target("守住的敌人")
	var wedge_result = resolver.resolve_effects(wedge_source, wedge_target, [_build_damage_effect(18)])
	var wedge_event = _extract_first_damage_event(wedge_result)
	_assert_true(
		int(wedge_event.get("guard_ignore_applied", 0)) > 0,
		"黑星楔钉的首击应记录 guard_ignore_applied。 event=%s" % [str(wedge_event)]
	)
	_assert_true(
		int(wedge_result.get("damage", 0)) > baseline_damage,
		"黑星楔钉首击应比未装备时打出更高伤害。 baseline=%d actual=%d" % [
			baseline_damage,
			int(wedge_result.get("damage", 0)),
		]
	)
	_assert_true(
		wedge_source.has_status_effect(LowLuckRelicRules.STATUS_BLACK_STAR_WEDGE_EXPOSED),
		"黑星楔钉未击杀目标时应给佩戴者挂上 1 回合破绽。"
	)

	var enemy_attacker = _build_battle_unit("报复者", &"enemy")
	var normal_target = _build_battle_unit("普通持有者", &"player")
	var exposed_target = BattleUnitState.from_dict(wedge_source.to_dict()) as BattleUnitState
	var normal_incoming = int(resolver.resolve_effects(enemy_attacker, normal_target, [_build_damage_effect(16)]).get("damage", 0))
	var exposed_incoming = int(resolver.resolve_effects(enemy_attacker, exposed_target, [_build_damage_effect(16)]).get("damage", 0))
	_assert_true(
		exposed_incoming > normal_incoming,
		"黑星楔钉代价应让佩戴者承受更高的后续伤害。 normal=%d exposed=%d" % [
			normal_incoming,
			exposed_incoming,
		]
	)


func _test_blood_debt_shawl_low_hp_reduction_ally_down_ap_and_recovery_penalty() -> void:
	var resolver = BattleDamageResolver.new()
	var enemy_attacker = _build_battle_unit("压迫者", &"enemy")
	var baseline_target = _build_battle_unit("普通目标", &"player", 100, 35)
	var shawl_target = _build_battle_unit("披肩目标", &"player", 100, 35)
	shawl_target.attribute_snapshot.set_value(LowLuckRelicRules.ATTR_BLOOD_DEBT_SHAWL, 1)
	var normal_damage = int(resolver.resolve_effects(enemy_attacker, baseline_target, [_build_damage_effect(20)]).get("damage", 0))
	var reduced_damage = int(resolver.resolve_effects(enemy_attacker, shawl_target, [_build_damage_effect(20)]).get("damage", 0))
	_assert_true(
		reduced_damage < normal_damage,
		"血债披肩在低血时应降低承伤。 normal=%d reduced=%d" % [normal_damage, reduced_damage]
	)

	var runtime = BattleRuntimeModule.new()
	runtime.setup(null, {}, {}, {})
	var state = _build_runtime_state(&"blood_debt_runtime")
	var wearer = _build_battle_unit("披肩佩戴者", &"player", 100, 80, HERO_ID)
	wearer.attribute_snapshot.set_value(LowLuckRelicRules.ATTR_BLOOD_DEBT_SHAWL, 1)
	wearer.current_ap = 1
	var fallen_ally = _build_battle_unit("倒地队友", &"player", 100, 0, ALLY_ID)
	fallen_ally.is_alive = false
	var enemy = _build_battle_unit("敌人", &"enemy", 100, 0, &"enemy")
	_add_unit_to_state(state, wearer)
	_add_unit_to_state(state, fallen_ally)
	_add_unit_to_state(state, enemy)
	var ally_unit_ids: Array[StringName] = [wearer.unit_id, fallen_ally.unit_id]
	var enemy_unit_ids: Array[StringName] = [enemy.unit_id]
	state.ally_unit_ids = ally_unit_ids
	state.enemy_unit_ids = enemy_unit_ids
	runtime._state = state
	var death_batch = BattleEventBatch.new()
	runtime.clear_defeated_unit(fallen_ally, death_batch)
	_assert_eq(wearer.current_ap, 2, "血债披肩应在队友倒地时返还 1 点行动点。")

	var party_state = PartyState.new()
	party_state.leader_member_id = HERO_ID
	party_state.main_character_member_id = HERO_ID
	party_state.active_member_ids = [HERO_ID]
	var member_state = PartyMemberState.new()
	member_state.member_id = HERO_ID
	member_state.display_name = "Hero"
	member_state.progression.unit_id = HERO_ID
	member_state.progression.display_name = "Hero"
	member_state.current_hp = 20
	member_state.current_mp = 10
	party_state.set_member_state(member_state)
	var restore_snapshot = AttributeSnapshot.new()
	restore_snapshot.set_value(&"hp_max", 100)
	restore_snapshot.set_value(&"mp_max", 30)
	restore_snapshot.set_value(LowLuckRelicRules.ATTR_BLOOD_DEBT_SHAWL, 1)
	var runtime_stub = SettlementRuntimeStub.new(party_state, {HERO_ID: restore_snapshot})
	var settlement_handler = GameRuntimeSettlementCommandHandler.new()
	settlement_handler.setup(runtime_stub)
	settlement_handler._restore_party_resources(1.0, true)
	_assert_eq(member_state.current_hp, 60, "血债披肩代价应把 full restore 的 HP 恢复量减半。")
	_assert_eq(member_state.current_mp, 20, "血债披肩代价应把 full restore 的 MP 恢复量减半。")


func _test_dead_road_lantern_reveals_hidden_paths_and_grants_black_omen_mark() -> void:
	var lantern_snapshot = AttributeSnapshot.new()
	lantern_snapshot.set_value(LowLuckRelicRules.ATTR_DEAD_ROAD_LANTERN, 1)
	_assert_true(
		LowLuckRelicRules.should_reveal_hidden_path(lantern_snapshot, [
			LowLuckRelicRules.PATH_TAG_HIDDEN_TRAP,
			LowLuckRelicRules.PATH_TAG_BLACK_OMEN,
		]),
		"亡途灯笼应把隐藏陷阱和黑兆路径都标记为可见。"
	)

	var item_defs = _load_low_luck_item_defs()
	var party_state = PartyState.new()
	party_state.leader_member_id = HERO_ID
	party_state.main_character_member_id = HERO_ID
	party_state.active_member_ids = [HERO_ID]
	var member_state = PartyMemberState.new()
	member_state.member_id = HERO_ID
	member_state.display_name = "Hero"
	member_state.progression.unit_id = HERO_ID
	member_state.progression.display_name = "Hero"
	member_state.progression.unit_base_attributes.set_attribute_value(MisfortuneBlackOmenService.DOOM_MARKED_STAT_ID, 0)
	member_state.equipment_state.set_equipped_entry(
		EquipmentRules.ACCESSORY_1,
		LowLuckRelicRules.ITEM_DEAD_ROAD_LANTERN,
		_build_slot_array(EquipmentRules.ACCESSORY_1),
		EquipmentInstanceState.create(
			LowLuckRelicRules.ITEM_DEAD_ROAD_LANTERN,
			&"eq_dead_road_lantern_black_omen"
		)
	)
	party_state.set_member_state(member_state)
	var manager = CharacterManagementModule.new()
	manager.setup(party_state, {}, {}, {}, item_defs)
	var black_omen_service = MisfortuneBlackOmenService.new()
	black_omen_service.setup(manager, item_defs)
	var black_omen_result = black_omen_service.try_run_hook(
		MisfortuneBlackOmenService.HOOK_DEAD_ROAD_LANTERN_BLACK_OMEN_PATH,
		{
			"member_id": HERO_ID,
			"path_tags": [LowLuckRelicRules.PATH_TAG_BLACK_OMEN],
		}
	)
	_assert_true(bool(black_omen_result.get("ok", false)), "亡途灯笼黑兆 hook 应完成受控评估。")
	_assert_true(bool(black_omen_result.get("conditions_met", false)), "亡途灯笼遇到黑兆路径时应满足黑兆条件。")
	_assert_true(bool(black_omen_result.get("granted", false)), "亡途灯笼代价应把佩戴者卷入黑兆。")
	_assert_eq(
		member_state.progression.unit_base_attributes.get_attribute_value(MisfortuneBlackOmenService.DOOM_MARKED_STAT_ID),
		1,
		"亡途灯笼黑兆 hook 应把 doom_marked 写成 1。"
	)


func _load_low_luck_item_defs() -> Dictionary:
	var registry = ItemContentRegistry.new()
	return registry.get_item_defs()


func _build_equipped_member_snapshot(item_defs: Dictionary, item_id: StringName):
	var party_state = PartyState.new()
	party_state.leader_member_id = HERO_ID
	party_state.main_character_member_id = HERO_ID
	party_state.active_member_ids = [HERO_ID]
	var member_state = PartyMemberState.new()
	member_state.member_id = HERO_ID
	member_state.display_name = "Hero"
	member_state.progression.unit_id = HERO_ID
	member_state.progression.display_name = "Hero"
	var slot_id = EquipmentRules.ACCESSORY_2 if item_id == LowLuckRelicRules.ITEM_BLOOD_DEBT_SHAWL else EquipmentRules.ACCESSORY_1
	member_state.equipment_state.set_equipped_entry(
		slot_id,
		item_id,
		_build_slot_array(slot_id),
		EquipmentInstanceState.create(
			item_id,
			ProgressionDataUtils.to_string_name("eq_snapshot_%s" % String(item_id))
		)
	)
	party_state.set_member_state(member_state)
	var manager = CharacterManagementModule.new()
	manager.setup(party_state, {}, {}, {}, item_defs)
	return manager.get_member_attribute_snapshot(HERO_ID)


func _build_slot_array(slot_id: StringName) -> Array[StringName]:
	var slot_ids: Array[StringName] = []
	slot_ids.append(slot_id)
	return slot_ids


func _build_low_luck_context(hidden_luck_at_birth: int, include_ally: bool = false) -> Dictionary:
	var party_state = PartyState.new()
	party_state.leader_member_id = HERO_ID
	party_state.main_character_member_id = HERO_ID
	party_state.active_member_ids = [HERO_ID]
	if include_ally:
		party_state.active_member_ids.append(ALLY_ID)
	party_state.set_member_state(_build_member_state(HERO_ID, hidden_luck_at_birth))
	if include_ally:
		party_state.set_member_state(_build_member_state(ALLY_ID, 0))
	var manager = CharacterManagementModule.new()
	manager.setup(party_state, {}, {}, {})
	var bus = BattleFateEventBus.new()
	var service = LowLuckEventService.new()
	service.setup(manager, bus)
	return {
		"party_state": party_state,
		"manager": manager,
		"bus": bus,
		"service": service,
	}


func _build_member_state(member_id: StringName, hidden_luck_at_birth: int) -> PartyMemberState:
	var member_state = PartyMemberState.new()
	member_state.member_id = member_id
	member_state.display_name = String(member_id)
	member_state.progression.unit_id = member_id
	member_state.progression.display_name = String(member_id)
	member_state.progression.character_level = 10
	member_state.progression.unit_base_attributes.set_attribute_value(&"hidden_luck_at_birth", hidden_luck_at_birth)
	return member_state


func _build_reward_battle_state(
	battle_id: StringName,
	hero_alive: bool,
	enemy_is_elite: bool,
	ally_dead: bool
) -> BattleState:
	var battle_state = BattleState.new()
	battle_state.battle_id = battle_id
	var hero = _build_battle_unit("Hero", &"player", 100, 100, HERO_ID)
	hero.unit_id = &"hero_unit"
	hero.is_alive = hero_alive
	_add_unit_to_state(battle_state, hero)
	battle_state.ally_unit_ids.append(hero.unit_id)
	if ally_dead:
		var ally = _build_battle_unit("Ally", &"player", 100, 0, ALLY_ID)
		ally.unit_id = &"ally_unit"
		ally.is_alive = false
		_add_unit_to_state(battle_state, ally)
		battle_state.ally_unit_ids.append(ally.unit_id)
	var enemy = _build_battle_unit("Enemy", &"enemy", 100, 0, &"enemy_member")
	enemy.unit_id = &"enemy_unit"
	enemy.is_alive = false
	if enemy_is_elite:
		enemy.attribute_snapshot.set_value(&"fortune_mark_target", 1)
	_add_unit_to_state(battle_state, enemy)
	battle_state.enemy_unit_ids.append(enemy.unit_id)
	return battle_state


func _build_battle_resolution_result(battle_id: StringName) -> BattleResolutionResult:
	var result = BattleResolutionResult.new()
	result.battle_id = battle_id
	result.winner_faction_id = &"player"
	return result


func _build_hardship_payload(battle_id: StringName, hidden_luck_at_birth: int) -> Dictionary:
	return {
		"battle_id": battle_id,
		"attacker_member_id": HERO_ID,
		"attacker_low_hp_hardship": true,
		"attacker_strong_attack_debuff_ids": [&"staggered"],
		"luck_snapshot": {
			"hidden_luck_at_birth": hidden_luck_at_birth,
		},
	}


func _build_critical_fail_payload(battle_id: StringName, hidden_luck_at_birth: int) -> Dictionary:
	return {
		"battle_id": battle_id,
		"attacker_member_id": HERO_ID,
		"luck_snapshot": {
			"hidden_luck_at_birth": hidden_luck_at_birth,
		},
	}


func _build_battle_unit(
	display_name: String,
	faction_id: StringName,
	hp_max: int = 100,
	current_hp: int = 100,
	source_member_id: StringName = &""
) -> BattleUnitState:
	var unit = BattleUnitState.new()
	unit.unit_id = ProgressionDataUtils.to_string_name(display_name)
	unit.display_name = display_name
	unit.faction_id = faction_id
	unit.source_member_id = source_member_id
	unit.is_alive = current_hp > 0
	unit.current_hp = current_hp
	unit.current_mp = 0
	unit.current_ap = 1
	unit.set_anchor_coord(Vector2i.ZERO)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX, hp_max)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.MP_MAX, 30)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 20)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 0)
	return unit


func _build_guarded_target(display_name: String) -> BattleUnitState:
	var target = _build_battle_unit(display_name, &"enemy", 120, 120)
	var guard_status = BattleStatusEffectState.new()
	guard_status.status_id = &"test_guard"
	guard_status.power = 1
	guard_status.stacks = 1
	guard_status.duration = 60
	guard_status.params = {"guard_block": 4}
	target.set_status_effect(guard_status)
	return target


func _build_damage_effect(power: int) -> CombatEffectDef:
	var effect = CombatEffectDef.new()
	effect.effect_type = &"damage"
	effect.power = power
	return effect


func _build_runtime_state(battle_id: StringName) -> BattleState:
	var state = BattleState.new()
	state.battle_id = battle_id
	state.phase = &"unit_acting"
	state.map_size = Vector2i(3, 3)
	state.timeline = BattleTimelineState.new()
	for y in range(state.map_size.y):
		for x in range(state.map_size.x):
			var cell = BattleCellState.new()
			cell.coord = Vector2i(x, y)
			cell.base_terrain = BattleCellState.TERRAIN_LAND
			cell.base_height = 4
			cell.recalculate_runtime_values()
			state.cells[cell.coord] = cell
	state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells)
	return state


func _add_unit_to_state(state: BattleState, unit: BattleUnitState) -> void:
	unit.refresh_footprint()
	state.units[unit.unit_id] = unit


func _extract_first_damage_event(result: Dictionary) -> Dictionary:
	var damage_events_variant: Variant = result.get("damage_events", [])
	if damage_events_variant is not Array or (damage_events_variant as Array).is_empty():
		return {}
	if (damage_events_variant as Array)[0] is Dictionary:
		return (damage_events_variant as Array)[0] as Dictionary
	return {}


func _assert_fixed_loot_entry(loot_entries_variant: Variant, item_id: StringName, message: String) -> void:
	var found_entry = _find_loot_entry(loot_entries_variant, item_id)
	_assert_true(not found_entry.is_empty(), message)
	if found_entry.is_empty():
		return
	_assert_eq(String(found_entry.get("drop_type", "")), "item", "%s | fixed low luck 奖励必须是 drop_type=item。" % message)
	_assert_eq(String(found_entry.get("drop_source_kind", "")), "low_luck_event", "%s | fixed low luck 奖励必须写成 drop_source_kind=low_luck_event。" % message)


func _find_loot_entry(loot_entries_variant: Variant, item_id: StringName) -> Dictionary:
	if loot_entries_variant is not Array:
		return {}
	for loot_entry_variant in loot_entries_variant:
		if loot_entry_variant is not Dictionary:
			continue
		var loot_entry = loot_entry_variant as Dictionary
		if ProgressionDataUtils.to_string_name(loot_entry.get("item_id", "")) == item_id:
			return loot_entry
	return {}


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
