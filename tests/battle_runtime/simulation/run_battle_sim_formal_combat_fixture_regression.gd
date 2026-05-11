extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const BATTLE_SIM_FORMAL_COMBAT_FIXTURE_SCRIPT = preload("res://scripts/systems/battle/sim/battle_sim_formal_combat_fixture.gd")
const ITEM_CONTENT_REGISTRY_SCRIPT = preload("res://scripts/player/warehouse/item_content_registry.gd")
const PROGRESSION_CONTENT_REGISTRY_SCRIPT = preload("res://scripts/player/progression/progression_content_registry.gd")
const CHARACTER_CREATION_SERVICE_SCRIPT = preload("res://scripts/systems/progression/character_creation_service.gd")
const PROGRESSION_SERVICE_SCRIPT = preload("res://scripts/systems/progression/progression_service.gd")
const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")
const EQUIPMENT_RULES_SCRIPT = preload("res://scripts/player/equipment/equipment_rules.gd")
const UNIT_SKILL_PROGRESS_SCRIPT = preload("res://scripts/player/progression/unit_skill_progress.gd")

const ATTRIBUTE_IDS: Array[StringName] = [
	&"strength",
	&"agility",
	&"constitution",
	&"perception",
	&"intelligence",
	&"willpower",
]

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_default_main_character_gets_reroll_luck()
	_test_selected_main_character_gets_reroll_luck()
	_test_selected_main_character_uses_configured_reroll_count()
	_test_hostile_main_character_option_falls_back_to_first_ally()
	_test_mixed_6v12_rolls_creation_attributes_from_seed()
	_test_mixed_6v12_profession_hp_uses_independent_rank_rolls()
	_test_mixed_6v12_omits_explicit_action_threshold()
	_test_mixed_6v12_elite_archers_get_shooting_specialization()
	_test_mixed_6v12_starts_all_units_at_full_effective_hp()
	_test_mixed_6v12_equips_role_armor()
	_test_formal_fixture_requests_bidirectional_spawn_reachability()

	if _failures.is_empty():
		print("BattleSimFormalCombatFixture regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("BattleSimFormalCombatFixture regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_default_main_character_gets_reroll_luck() -> void:
	var fixture = _build_fixture(BATTLE_SIM_FORMAL_COMBAT_FIXTURE_SCRIPT.ROSTER_MIXED_2S1A)
	var party_state = fixture.get_party_state()

	_assert_eq(party_state.main_character_member_id, &"ally_longsword_01", "默认主角应是第一个友军。")
	_assert_eq(party_state.leader_member_id, &"ally_longsword_01", "默认队长应跟随默认主角。")
	_assert_eq(_get_hidden_luck(fixture, &"ally_longsword_01"), 2, "默认主角应按 reroll_count=0 烘焙 +2 出生幸运。")
	_assert_eq(_get_hidden_luck(fixture, &"ally_longsword_02"), 0, "未选中的友军不应获得出生幸运。")
	_assert_eq(_get_hidden_luck(fixture, &"enemy_longsword_01"), 0, "敌方单位不应获得主角 reroll 出生幸运。")


func _test_selected_main_character_gets_reroll_luck() -> void:
	var fixture = _build_fixture(
		BATTLE_SIM_FORMAL_COMBAT_FIXTURE_SCRIPT.ROSTER_MIXED_6V12,
		{
			BATTLE_SIM_FORMAL_COMBAT_FIXTURE_SCRIPT.ROSTER_OPTION_MAIN_CHARACTER_MEMBER_ID: &"elite_archer_0",
		}
	)
	var party_state = fixture.get_party_state()

	_assert_eq(party_state.main_character_member_id, &"elite_archer_0", "显式选择的友军应成为主角。")
	_assert_eq(party_state.leader_member_id, &"elite_archer_0", "未显式选择队长时，队长应跟随主角。")
	_assert_eq(_get_hidden_luck(fixture, &"elite_archer_0"), 2, "选中的主角应按默认 reroll_count=0 烘焙 +2。")
	_assert_eq(_get_hidden_luck(fixture, &"elite_sword_0"), 0, "默认第一友军被替换后不应获得主角出生幸运。")


func _test_selected_main_character_uses_configured_reroll_count() -> void:
	var fixture = _build_fixture(
		BATTLE_SIM_FORMAL_COMBAT_FIXTURE_SCRIPT.ROSTER_MIXED_6V12,
		{
			BATTLE_SIM_FORMAL_COMBAT_FIXTURE_SCRIPT.ROSTER_OPTION_MAIN_CHARACTER_MEMBER_ID: &"elite_mage_0",
			BATTLE_SIM_FORMAL_COMBAT_FIXTURE_SCRIPT.ROSTER_OPTION_MAIN_CHARACTER_REROLL_COUNT: 10000,
		}
	)

	_assert_eq(_get_hidden_luck(fixture, &"elite_mage_0"), -3, "主角应按传入 reroll_count=10000 烘焙 -3。")
	_assert_eq(_get_hidden_luck(fixture, &"elite_archer_0"), 0, "未选中弓手不应获得出生幸运。")


func _test_hostile_main_character_option_falls_back_to_first_ally() -> void:
	var fixture = _build_fixture(
		BATTLE_SIM_FORMAL_COMBAT_FIXTURE_SCRIPT.ROSTER_MIXED_6V12,
		{
			BATTLE_SIM_FORMAL_COMBAT_FIXTURE_SCRIPT.ROSTER_OPTION_MAIN_CHARACTER_MEMBER_ID: &"hostile_archer_0",
		}
	)
	var party_state = fixture.get_party_state()

	_assert_eq(party_state.main_character_member_id, &"elite_sword_0", "敌方不能被设为主角，应回退第一个友军。")
	_assert_eq(_get_hidden_luck(fixture, &"elite_sword_0"), 2, "回退主角应获得默认 reroll 出生幸运。")
	_assert_eq(_get_hidden_luck(fixture, &"hostile_archer_0"), 0, "被拒绝的敌方配置不应获得出生幸运。")


func _test_mixed_6v12_rolls_creation_attributes_from_seed() -> void:
	var same_seed_options := {
		BATTLE_SIM_FORMAL_COMBAT_FIXTURE_SCRIPT.ROSTER_OPTION_ATTRIBUTE_ROLL_SEED: 24680,
	}
	var fixture_a = _build_fixture(BATTLE_SIM_FORMAL_COMBAT_FIXTURE_SCRIPT.ROSTER_MIXED_6V12, same_seed_options)
	var fixture_b = _build_fixture(BATTLE_SIM_FORMAL_COMBAT_FIXTURE_SCRIPT.ROSTER_MIXED_6V12, same_seed_options)
	var attrs_a := _build_all_member_attribute_map(fixture_a)
	var attrs_b := _build_all_member_attribute_map(fixture_b)
	_assert_eq(attrs_a, attrs_b, "同一 attribute_roll_seed 应稳定复现 6v12 建卡属性。")

	var fixture_c = _build_fixture(
		BATTLE_SIM_FORMAL_COMBAT_FIXTURE_SCRIPT.ROSTER_MIXED_6V12,
		{
			BATTLE_SIM_FORMAL_COMBAT_FIXTURE_SCRIPT.ROSTER_OPTION_ATTRIBUTE_ROLL_SEED: 13579,
		}
	)
	var attrs_c := _build_all_member_attribute_map(fixture_c)
	_assert_true(JSON.stringify(attrs_a) != JSON.stringify(attrs_c), "不同 attribute_roll_seed 应产生不同建卡属性分布。")
	_assert_rolled_attribute_range(fixture_a, &"hostile_sword_0")
	_assert_rolled_attribute_range(fixture_a, &"hostile_archer_0")


func _test_mixed_6v12_profession_hp_uses_independent_rank_rolls() -> void:
	var seed := 24680
	var fixture = _build_fixture(
		BATTLE_SIM_FORMAL_COMBAT_FIXTURE_SCRIPT.ROSTER_MIXED_6V12,
		{
			BATTLE_SIM_FORMAL_COMBAT_FIXTURE_SCRIPT.ROSTER_OPTION_ATTRIBUTE_ROLL_SEED: seed,
		}
	)
	var mage = fixture.get_member_state(&"elite_mage_0")
	if mage == null or mage.progression == null or mage.progression.unit_base_attributes == null:
		_test.fail("缺少 6v12 法师。")
		return
	var attrs = mage.progression.unit_base_attributes
	var constitution := int(attrs.get_attribute_value(UnitBaseAttributes.CONSTITUTION))
	var expected_hp := CHARACTER_CREATION_SERVICE_SCRIPT.calculate_initial_hp_max(constitution)
	var hp_rng := RandomNumberGenerator.new()
	hp_rng.seed = seed + BATTLE_SIM_FORMAL_COMBAT_FIXTURE_SCRIPT.HP_ROLL_SEED_OFFSET
	for _sword_rank in range(4 * 2):
		hp_rng.randi_range(1, 10)
	for _archer_rank in range(2):
		hp_rng.randi_range(1, 8)
	for _mage_rank in range(5):
		var hp_roll := hp_rng.randi_range(1, 6)
		expected_hp += PROGRESSION_SERVICE_SCRIPT.calculate_profession_hit_point_gain(hp_roll, constitution)
	var old_aggregate_hp := CHARACTER_CREATION_SERVICE_SCRIPT.calculate_initial_hp_max(constitution)
	old_aggregate_hp += PROGRESSION_SERVICE_SCRIPT.calculate_profession_hit_point_gain(5, constitution) * 5
	_assert_true(expected_hp != old_aggregate_hp, "测试 seed 应能区分逐 rank 独立掷骰与旧的单 roll 乘 rank 逻辑。")
	_assert_eq(
		attrs.get_attribute_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX),
		expected_hp,
		"6v12 法师职业生命应按每个 rank 独立掷生命骰后累加。"
	)


func _test_mixed_6v12_omits_explicit_action_threshold() -> void:
	var fixture = _build_fixture(BATTLE_SIM_FORMAL_COMBAT_FIXTURE_SCRIPT.ROSTER_MIXED_6V12)
	var party_state = fixture.get_party_state()
	if party_state == null:
		_test.fail("缺少 fixture party_state。")
		return
	for member_id in party_state.member_states.keys():
		var member_state = fixture.get_member_state(member_id)
		if member_state == null or member_state.progression == null or member_state.progression.unit_base_attributes == null:
			_test.fail("缺少成员属性：%s" % String(member_id))
			continue
		var base_attributes = member_state.progression.unit_base_attributes
		_assert_true(
			not base_attributes.custom_stats.has(ATTRIBUTE_SERVICE_SCRIPT.ACTION_THRESHOLD),
			"6v12 不应显式写入 action_threshold：%s" % String(member_id)
		)
		var attribute_service = ATTRIBUTE_SERVICE_SCRIPT.new()
		attribute_service.setup(member_state.progression)
		_assert_eq(
			attribute_service.get_total_value(ATTRIBUTE_SERVICE_SCRIPT.ACTION_THRESHOLD),
			ATTRIBUTE_SERVICE_SCRIPT.DEFAULT_CHARACTER_ACTION_THRESHOLD,
			"6v12 应回落到角色默认 action_threshold：%s" % String(member_id)
		)


func _test_formal_fixture_requests_bidirectional_spawn_reachability() -> void:
	var fixture = _build_fixture(BATTLE_SIM_FORMAL_COMBAT_FIXTURE_SCRIPT.ROSTER_MIXED_6V12)
	var context: Dictionary = fixture.build_runtime_context(null, {})
	_assert_true(bool(context.get("validate_spawn_reachability", false)), "formal combat fixture 模拟应开启出生可达性验证。")
	_assert_true(bool(context.get("validate_bidirectional_spawn_reachability", false)), "formal combat fixture 模拟应开启玩家/敌方双向可攻击验证。")
	_assert_true(bool(context.get("enforce_opposing_spawn_sides", false)), "formal combat fixture 模拟应开启玩家/敌方对侧出生约束。")


func _test_mixed_6v12_elite_archers_get_shooting_specialization() -> void:
	var fixture = _build_fixture(BATTLE_SIM_FORMAL_COMBAT_FIXTURE_SCRIPT.ROSTER_MIXED_6V12)
	for member_id in [&"elite_archer_0"]:
		var member_state = fixture.get_member_state(member_id)
		var skill_progress = member_state.progression.get_skill_progress(&"archer_shooting_specialization") if member_state != null and member_state.progression != null else null
		_assert_true(
			skill_progress != null and skill_progress.is_learned,
			"6v12 精英弓手应通过弓箭手职业获得射击专精：%s" % String(member_id)
		)
		if skill_progress == null:
			continue
		_assert_eq(skill_progress.skill_level, 0, "射击专精应以 0 级进入模拟：%s" % String(member_id))
		_assert_eq(skill_progress.granted_source_type, UNIT_SKILL_PROGRESS_SCRIPT.GRANTED_SOURCE_PROFESSION, "射击专精来源类型应为职业：%s" % String(member_id))
		_assert_eq(skill_progress.granted_source_id, &"archer", "射击专精来源职业应为 archer：%s" % String(member_id))

	var hostile = fixture.get_member_state(&"hostile_archer_0")
	var hostile_progress = hostile.progression.get_skill_progress(&"archer_shooting_specialization") if hostile != null and hostile.progression != null else null
	_assert_true(hostile_progress == null or not hostile_progress.is_learned, "0级 hostile 弓手不应获得射击专精。")


func _test_mixed_6v12_starts_all_units_at_full_effective_hp() -> void:
	var fixture = _build_fixture(BATTLE_SIM_FORMAL_COMBAT_FIXTURE_SCRIPT.ROSTER_MIXED_6V12)
	var party_state = fixture.get_party_state()
	if party_state == null:
		_test.fail("缺少 fixture party_state。")
		return
	for member_id_variant in party_state.member_states.keys():
		var member_id := StringName(String(member_id_variant))
		var member_state = fixture.get_member_state(member_id)
		if member_state == null:
			_test.fail("缺少成员：%s" % String(member_id))
			continue
		var snapshot = fixture.get_member_attribute_snapshot_for_equipment_view(member_id, member_state.equipment_state)
		var hp_max := maxi(int(snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX)), 1)
		_assert_eq(member_state.current_hp, hp_max, "6v12 开战前应按装备与职业被动后的有效生命上限补满：%s" % String(member_id))

	var elite_sword = fixture.get_member_state(&"elite_sword_0")
	if elite_sword != null:
		var elite_snapshot = fixture.get_member_attribute_snapshot_for_equipment_view(&"elite_sword_0", elite_sword.equipment_state)
		_assert_eq(
			elite_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.CHARACTER_HP_MAX_PERCENT_BONUS),
			20,
			"精英剑士有效生命上限应包含强健的 20% 人物生命加成。"
		)
		elite_sword.current_hp = 1
		fixture.build_runtime_context(null, {})
		_assert_eq(
			elite_sword.current_hp,
			maxi(int(elite_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX)), 1),
			"build_runtime_context 开战前应重新把模拟单位补到有效生命上限。"
		)


func _test_mixed_6v12_equips_role_armor() -> void:
	var fixture = _build_fixture(BATTLE_SIM_FORMAL_COMBAT_FIXTURE_SCRIPT.ROSTER_MIXED_6V12)
	for index in range(4):
		var elite_sword_member_id := StringName("elite_sword_%d" % index)
		_assert_equipped_item(fixture, elite_sword_member_id, EQUIPMENT_RULES_SCRIPT.MAIN_HAND, &"steel_longsword", "6v12 精英剑士应装备长剑：%s" % String(elite_sword_member_id))
		_assert_equipped_item(fixture, elite_sword_member_id, EQUIPMENT_RULES_SCRIPT.BODY, &"iron_scale_mail", "6v12 精英剑士应装备中甲：%s" % String(elite_sword_member_id))
	for index in range(1):
		var elite_archer_member_id := StringName("elite_archer_%d" % index)
		_assert_equipped_item(fixture, elite_archer_member_id, EQUIPMENT_RULES_SCRIPT.MAIN_HAND, &"ash_longbow", "6v12 精英弓手应装备长弓：%s" % String(elite_archer_member_id))
		_assert_equipped_item(fixture, elite_archer_member_id, EQUIPMENT_RULES_SCRIPT.BODY, &"leather_jerkin", "6v12 精英弓手应装备皮甲：%s" % String(elite_archer_member_id))
	_assert_no_equipped_item(fixture, &"elite_mage_0", EQUIPMENT_RULES_SCRIPT.MAIN_HAND, "6v12 精英法师不应装备武器。")
	_assert_equipped_item(fixture, &"elite_mage_0", EQUIPMENT_RULES_SCRIPT.BODY, &"leather_jerkin", "6v12 精英法师应装备皮甲。")
	for index in range(8):
		var hostile_sword_member_id := StringName("hostile_sword_%d" % index)
		_assert_equipped_item(fixture, hostile_sword_member_id, EQUIPMENT_RULES_SCRIPT.MAIN_HAND, &"steel_longsword", "6v12 敌方剑士应装备长剑：%s" % String(hostile_sword_member_id))
		_assert_equipped_item(fixture, hostile_sword_member_id, EQUIPMENT_RULES_SCRIPT.BODY, &"iron_scale_mail", "6v12 敌方剑士应装备中甲：%s" % String(hostile_sword_member_id))
	for index in range(4):
		var hostile_archer_member_id := StringName("hostile_archer_%d" % index)
		_assert_equipped_item(fixture, hostile_archer_member_id, EQUIPMENT_RULES_SCRIPT.MAIN_HAND, &"ash_longbow", "6v12 敌方弓手应装备长弓：%s" % String(hostile_archer_member_id))
		_assert_equipped_item(fixture, hostile_archer_member_id, EQUIPMENT_RULES_SCRIPT.BODY, &"leather_jerkin", "6v12 敌方弓手应装备皮甲：%s" % String(hostile_archer_member_id))


func _build_fixture(roster_id: StringName, roster_options: Dictionary = {}):
	var progression_registry = PROGRESSION_CONTENT_REGISTRY_SCRIPT.new()
	var item_registry = ITEM_CONTENT_REGISTRY_SCRIPT.new()
	var fixture = BATTLE_SIM_FORMAL_COMBAT_FIXTURE_SCRIPT.new()
	fixture.setup_content({
		"skill_defs": progression_registry.get_skill_defs(),
		"profession_defs": progression_registry.get_profession_defs(),
		"achievement_defs": progression_registry.get_achievement_defs(),
		"item_defs": item_registry.get_item_defs(),
		"progression_content_bundle": progression_registry.get_bundle(),
	})
	_assert_true(fixture.build_roster(roster_id, roster_options), "fixture 应能构建 roster=%s。" % String(roster_id))
	return fixture


func _get_hidden_luck(fixture, member_id: StringName) -> int:
	var member_state = fixture.get_member_state(member_id)
	if member_state == null:
		_test.fail("缺少成员：%s" % String(member_id))
		return 0
	return int(member_state.get_hidden_luck_at_birth())


func _build_all_member_attribute_map(fixture) -> Dictionary:
	var result := {}
	var party_state = fixture.get_party_state()
	if party_state == null:
		_test.fail("缺少 fixture party_state。")
		return result
	var member_ids: Array[String] = []
	for member_id_variant in party_state.member_states.keys():
		member_ids.append(String(member_id_variant))
	member_ids.sort()
	for member_id_text in member_ids:
		var member_id := StringName(member_id_text)
		result[member_id_text] = _get_base_attributes(fixture, member_id)
	return result


func _get_base_attributes(fixture, member_id: StringName) -> Dictionary:
	var member_state = fixture.get_member_state(member_id)
	if member_state == null or member_state.progression == null or member_state.progression.unit_base_attributes == null:
		_test.fail("缺少成员属性：%s" % String(member_id))
		return {}
	var attrs = member_state.progression.unit_base_attributes
	var result := {}
	for attribute_id in ATTRIBUTE_IDS:
		result[String(attribute_id)] = int(attrs.get_attribute_value(attribute_id))
	return result


func _assert_rolled_attribute_range(fixture, member_id: StringName) -> void:
	var attrs := _get_base_attributes(fixture, member_id)
	for attribute_id in ATTRIBUTE_IDS:
		var value := int(attrs.get(String(attribute_id), 0))
		_assert_true(
			value >= 4 and value <= 14,
			"%s 的 %s 应来自 5D3-1 建卡骰子范围。actual=%d" % [String(member_id), String(attribute_id), value]
		)


func _assert_equipped_item(fixture, member_id: StringName, slot_id: StringName, expected_item_id: StringName, message: String) -> void:
	var member_state = fixture.get_member_state(member_id)
	if member_state == null:
		_test.fail("缺少成员：%s" % String(member_id))
		return
	if member_state.equipment_state == null:
		_test.fail("成员缺少装备状态：%s" % String(member_id))
		return
	_assert_eq(member_state.equipment_state.get_equipped_item_id(slot_id), expected_item_id, message)


func _assert_no_equipped_item(fixture, member_id: StringName, slot_id: StringName, message: String) -> void:
	var member_state = fixture.get_member_state(member_id)
	if member_state == null:
		_test.fail("缺少成员：%s" % String(member_id))
		return
	if member_state.equipment_state == null:
		return
	_assert_eq(member_state.equipment_state.get_equipped_item_id(slot_id), &"", message)


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_test.fail(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_test.fail("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
