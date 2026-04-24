extends SceneTree

const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attribute_service.gd")
const BATTLE_DAMAGE_RESOLVER_SCRIPT = preload("res://scripts/systems/battle_damage_resolver.gd")
const BATTLE_FATE_EVENT_BUS_SCRIPT = preload("res://scripts/systems/battle_fate_event_bus.gd")
const BATTLE_REPORT_FORMATTER_SCRIPT = preload("res://scripts/systems/battle_report_formatter.gd")
const BATTLE_STATE_SCRIPT = preload("res://scripts/systems/battle_state.gd")
const BATTLE_UNIT_STATE_SCRIPT = preload("res://scripts/systems/battle_unit_state.gd")
const COMBAT_EFFECT_DEF_SCRIPT = preload("res://scripts/player/progression/combat_effect_def.gd")
const UNIT_BASE_ATTRIBUTES_SCRIPT = preload("res://scripts/player/progression/unit_base_attributes.gd")

const BASE_TARGET_HP := 30
const EXPECTED_DAMAGE := 10


class StubRng:
	extends RefCounted

	var _rolls: Array[int] = []
	var call_count := 0


	func _init(rolls: Array[int] = []) -> void:
		_rolls = rolls.duplicate()


	func randi_range(min_value: int, max_value: int) -> int:
		if call_count >= _rolls.size():
			call_count += 1
			return min_value
		var roll := clampi(int(_rolls[call_count]), min_value, max_value)
		call_count += 1
		return roll


class EventRecorder:
	extends RefCounted

	var events: Array[Dictionary] = []


	func _on_event(event_type: StringName, payload: Dictionary) -> void:
		var luck_snapshot_variant = payload.get("luck_snapshot", null)
		var luck_snapshot: Dictionary = {}
		if luck_snapshot_variant is Dictionary:
			luck_snapshot = luck_snapshot_variant
		events.append({
			"event_type": event_type,
			"payload": payload,
			"payload_read_only": payload.is_read_only(),
			"luck_snapshot_read_only": luck_snapshot.is_read_only() if not luck_snapshot.is_empty() else false,
		})


var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_fate_attack_flow_cross_cases()
	_test_attack_flow_uses_battle_state_for_seeded_rolls_and_disadvantage()
	_test_fate_attack_events_and_payload_contract()
	_test_attack_report_entry_generation()

	if _failures.is_empty():
		print("Battle damage resolver fate attack regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Battle damage resolver fate attack regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_fate_attack_flow_cross_cases() -> void:
	var resolver = BATTLE_DAMAGE_RESOLVER_SCRIPT.new()
	var cases := [
		{
			"label": "+7 normal uses high-end threat crit",
			"hidden_luck": 2,
			"faith_luck": 5,
			"is_disadvantage": false,
			"required_roll": 12,
			"rng_rolls": [16],
			"expected_resolution": BATTLE_DAMAGE_RESOLVER_SCRIPT.ATTACK_RESOLUTION_CRITICAL_HIT,
			"expected_crit_die": 20,
			"expected_crit_threshold": 16,
			"expected_fumble_low_end": 1,
			"expected_hit_roll": 16,
			"expected_crit_gate_roll": 0,
			"expected_call_count": 1,
			"expected_target_hp": BASE_TARGET_HP - EXPECTED_DAMAGE,
		},
		{
			"label": "+7 disadvantage keeps lower d20 as ordinary hit",
			"hidden_luck": 2,
			"faith_luck": 5,
			"is_disadvantage": true,
			"required_roll": 4,
			"rng_rolls": [20, 4],
			"expected_resolution": BATTLE_DAMAGE_RESOLVER_SCRIPT.ATTACK_RESOLUTION_HIT,
			"expected_crit_die": 20,
			"expected_crit_threshold": 16,
			"expected_fumble_low_end": 1,
			"expected_hit_roll": 4,
			"expected_crit_gate_roll": 0,
			"expected_call_count": 2,
			"expected_target_hp": BASE_TARGET_HP - EXPECTED_DAMAGE,
		},
		{
			"label": "+2 normal uses threshold 18 for crit",
			"hidden_luck": 2,
			"faith_luck": 0,
			"is_disadvantage": false,
			"required_roll": 18,
			"rng_rolls": [18],
			"expected_resolution": BATTLE_DAMAGE_RESOLVER_SCRIPT.ATTACK_RESOLUTION_CRITICAL_HIT,
			"expected_crit_die": 20,
			"expected_crit_threshold": 18,
			"expected_fumble_low_end": 1,
			"expected_hit_roll": 18,
			"expected_crit_gate_roll": 0,
			"expected_call_count": 1,
			"expected_target_hp": BASE_TARGET_HP - EXPECTED_DAMAGE,
		},
		{
			"label": "+2 normal high-threat crit bypasses required roll",
			"hidden_luck": 2,
			"faith_luck": 0,
			"is_disadvantage": false,
			"required_roll": 19,
			"rng_rolls": [18],
			"expected_resolution": BATTLE_DAMAGE_RESOLVER_SCRIPT.ATTACK_RESOLUTION_CRITICAL_HIT,
			"expected_crit_die": 20,
			"expected_crit_threshold": 18,
			"expected_fumble_low_end": 1,
			"expected_hit_roll": 18,
			"expected_crit_gate_roll": 0,
			"expected_call_count": 1,
			"expected_target_hp": BASE_TARGET_HP - EXPECTED_DAMAGE,
		},
		{
			"label": "+2 disadvantage drops below threat zone",
			"hidden_luck": 2,
			"faith_luck": 0,
			"is_disadvantage": true,
			"required_roll": 17,
			"rng_rolls": [19, 17],
			"expected_resolution": BATTLE_DAMAGE_RESOLVER_SCRIPT.ATTACK_RESOLUTION_HIT,
			"expected_crit_die": 20,
			"expected_crit_threshold": 18,
			"expected_fumble_low_end": 1,
			"expected_hit_roll": 17,
			"expected_crit_gate_roll": 0,
			"expected_call_count": 2,
			"expected_target_hp": BASE_TARGET_HP - EXPECTED_DAMAGE,
		},
		{
			"label": "0 normal only crits on 20",
			"hidden_luck": 0,
			"faith_luck": 0,
			"is_disadvantage": false,
			"required_roll": 20,
			"rng_rolls": [20],
			"expected_resolution": BATTLE_DAMAGE_RESOLVER_SCRIPT.ATTACK_RESOLUTION_CRITICAL_HIT,
			"expected_crit_die": 20,
			"expected_crit_threshold": 20,
			"expected_fumble_low_end": 1,
			"expected_hit_roll": 20,
			"expected_crit_gate_roll": 0,
			"expected_call_count": 1,
			"expected_target_hp": BASE_TARGET_HP - EXPECTED_DAMAGE,
		},
		{
			"label": "0 disadvantage misses after low d20",
			"hidden_luck": 0,
			"faith_luck": 0,
			"is_disadvantage": true,
			"required_roll": 6,
			"rng_rolls": [20, 5],
			"expected_resolution": BATTLE_DAMAGE_RESOLVER_SCRIPT.ATTACK_RESOLUTION_MISS,
			"expected_crit_die": 20,
			"expected_crit_threshold": 20,
			"expected_fumble_low_end": 1,
			"expected_hit_roll": 5,
			"expected_crit_gate_roll": 0,
			"expected_call_count": 2,
			"expected_target_hp": BASE_TARGET_HP,
		},
		{
			"label": "-4 normal keeps natural 20 as ordinary hit when gate die is larger",
			"hidden_luck": -4,
			"faith_luck": 0,
			"is_disadvantage": false,
			"required_roll": 25,
			"rng_rolls": [1, 20],
			"expected_resolution": BATTLE_DAMAGE_RESOLVER_SCRIPT.ATTACK_RESOLUTION_HIT,
			"expected_crit_die": 40,
			"expected_crit_threshold": 20,
			"expected_fumble_low_end": 1,
			"expected_hit_roll": 20,
			"expected_crit_gate_roll": 1,
			"expected_call_count": 2,
			"expected_target_hp": BASE_TARGET_HP - EXPECTED_DAMAGE,
		},
		{
			"label": "-4 disadvantage can still crit only via gate roll",
			"hidden_luck": -4,
			"faith_luck": 0,
			"is_disadvantage": true,
			"required_roll": 25,
			"rng_rolls": [40, 40],
			"expected_resolution": BATTLE_DAMAGE_RESOLVER_SCRIPT.ATTACK_RESOLUTION_CRITICAL_HIT,
			"expected_crit_die": 40,
			"expected_crit_threshold": 20,
			"expected_fumble_low_end": 1,
			"expected_hit_roll": 0,
			"expected_crit_gate_roll": 40,
			"expected_call_count": 2,
			"expected_target_hp": BASE_TARGET_HP - EXPECTED_DAMAGE,
		},
		{
			"label": "-5 normal fumble stops before AC check",
			"hidden_luck": -5,
			"faith_luck": 0,
			"is_disadvantage": false,
			"required_roll": 2,
			"rng_rolls": [1, 2],
			"expected_resolution": BATTLE_DAMAGE_RESOLVER_SCRIPT.ATTACK_RESOLUTION_CRITICAL_FAIL,
			"expected_crit_die": 80,
			"expected_crit_threshold": 20,
			"expected_fumble_low_end": 2,
			"expected_hit_roll": 2,
			"expected_crit_gate_roll": 1,
			"expected_call_count": 2,
			"expected_target_hp": BASE_TARGET_HP,
		},
		{
			"label": "-5 disadvantage applies mercy only to gate die",
			"hidden_luck": -5,
			"faith_luck": 0,
			"is_disadvantage": true,
			"required_roll": 2,
			"rng_rolls": [1, 1, 18, 2],
			"expected_resolution": BATTLE_DAMAGE_RESOLVER_SCRIPT.ATTACK_RESOLUTION_CRITICAL_FAIL,
			"expected_crit_die": 40,
			"expected_crit_threshold": 20,
			"expected_fumble_low_end": 2,
			"expected_hit_roll": 2,
			"expected_crit_gate_roll": 1,
			"expected_call_count": 4,
			"expected_target_hp": BASE_TARGET_HP,
		},
		{
			"label": "-6 normal expands fumble range to 3",
			"hidden_luck": -6,
			"faith_luck": 0,
			"is_disadvantage": false,
			"required_roll": 3,
			"rng_rolls": [1, 3],
			"expected_resolution": BATTLE_DAMAGE_RESOLVER_SCRIPT.ATTACK_RESOLUTION_CRITICAL_FAIL,
			"expected_crit_die": 160,
			"expected_crit_threshold": 20,
			"expected_fumble_low_end": 3,
			"expected_hit_roll": 3,
			"expected_crit_gate_roll": 1,
			"expected_call_count": 2,
			"expected_target_hp": BASE_TARGET_HP,
		},
		{
			"label": "-6 disadvantage keeps mercy at one gate step only",
			"hidden_luck": -6,
			"faith_luck": 0,
			"is_disadvantage": true,
			"required_roll": 3,
			"rng_rolls": [1, 1, 17, 3],
			"expected_resolution": BATTLE_DAMAGE_RESOLVER_SCRIPT.ATTACK_RESOLUTION_CRITICAL_FAIL,
			"expected_crit_die": 80,
			"expected_crit_threshold": 20,
			"expected_fumble_low_end": 3,
			"expected_hit_roll": 3,
			"expected_crit_gate_roll": 1,
			"expected_call_count": 4,
			"expected_target_hp": BASE_TARGET_HP,
		},
	]
	for case_data in cases:
		_run_cross_case(resolver, case_data)


func _test_attack_flow_uses_battle_state_for_seeded_rolls_and_disadvantage() -> void:
	var resolver = BATTLE_DAMAGE_RESOLVER_SCRIPT.new()
	var first_state := BATTLE_STATE_SCRIPT.new()
	first_state.battle_id = &"fate_attack_seeded"
	first_state.seed = 20260422
	var second_state := BATTLE_STATE_SCRIPT.new()
	second_state.battle_id = &"fate_attack_seeded"
	second_state.seed = 20260422
	var first_attacker := _build_unit(&"seeded_attacker_a", 2, 5, 9, 30)
	var second_attacker := _build_unit(&"seeded_attacker_b", 2, 5, 9, 30)
	var first_target := _build_unit(&"seeded_target_a", 0, 0, BASE_TARGET_HP, BASE_TARGET_HP)
	var second_target := _build_unit(&"seeded_target_b", 0, 0, BASE_TARGET_HP, BASE_TARGET_HP)
	_add_units_to_state(first_state, first_attacker, first_target)
	_add_units_to_state(second_state, second_attacker, second_target)
	var attack_check := _build_attack_check(6)
	var damage_effect: Variant = _build_damage_effect()

	var first_result := resolver.resolve_attack_effects(
		first_attacker,
		first_target,
		[damage_effect],
		attack_check,
		{"battle_state": first_state}
	)
	var second_result := resolver.resolve_attack_effects(
		second_attacker,
		second_target,
		[damage_effect],
		attack_check,
		{"battle_state": second_state}
	)

	_assert_true(bool(first_result.get("is_disadvantage", false)), "battle_state 推导的低血 hardship 应触发 attack disadvantage。")
	_assert_eq(first_result.get("attack_resolution", &""), second_result.get("attack_resolution", &""), "相同 battle_id/seed 的攻击结算类型应稳定复现。")
	_assert_eq(int(first_result.get("hit_roll", 0)), int(second_result.get("hit_roll", 0)), "相同 battle_id/seed 的命中骰应稳定复现。")
	_assert_eq(first_state.attack_roll_nonce, 2, "crit_gate_die==20 的劣势攻击应只消耗 2 次 d20 RNG。")
	_assert_eq(second_state.attack_roll_nonce, 2, "重复 battle_state 夹具也应消耗同样的 RNG 次数。")


func _test_fate_attack_events_and_payload_contract() -> void:
	var resolver = BATTLE_DAMAGE_RESOLVER_SCRIPT.new()
	var recorder := EventRecorder.new()
	resolver.get_fate_event_bus().event_dispatched.connect(recorder._on_event)

	var cases := [
		{
			"label": "critical fail emits critical_fail event",
			"event_type": BATTLE_FATE_EVENT_BUS_SCRIPT.EVENT_CRITICAL_FAIL,
			"hidden_luck": -5,
			"faith_luck": 0,
			"is_disadvantage": false,
			"required_roll": 2,
			"rng_rolls": [1, 2],
		},
		{
			"label": "disadvantage gate crit emits critical_success_under_disadvantage",
			"event_type": BATTLE_FATE_EVENT_BUS_SCRIPT.EVENT_CRITICAL_SUCCESS_UNDER_DISADVANTAGE,
			"hidden_luck": -4,
			"faith_luck": 0,
			"is_disadvantage": true,
			"required_roll": 25,
			"rng_rolls": [40, 40],
		},
		{
			"label": "ordinary miss emits ordinary_miss event",
			"event_type": BATTLE_FATE_EVENT_BUS_SCRIPT.EVENT_ORDINARY_MISS,
			"hidden_luck": 0,
			"faith_luck": 0,
			"is_disadvantage": true,
			"required_roll": 6,
			"rng_rolls": [20, 5],
		},
		{
			"label": "disadvantage ordinary hit emits hardship_survival",
			"event_type": BATTLE_FATE_EVENT_BUS_SCRIPT.EVENT_HARDSHIP_SURVIVAL,
			"hidden_luck": 2,
			"faith_luck": 5,
			"is_disadvantage": true,
			"required_roll": 4,
			"rng_rolls": [20, 4],
		},
		{
			"label": "high-threat crit bypasses threshold and still emits crit event",
			"event_type": BATTLE_FATE_EVENT_BUS_SCRIPT.EVENT_HIGH_THREAT_CRITICAL_HIT,
			"hidden_luck": 2,
			"faith_luck": 0,
			"is_disadvantage": false,
			"required_roll": 19,
			"rng_rolls": [18],
		},
	]
	for case_data in cases:
		_run_fate_event_case(resolver, recorder, case_data)


func _test_attack_report_entry_generation() -> void:
	var resolver = BATTLE_DAMAGE_RESOLVER_SCRIPT.new()
	var cases := [
		{
			"label": "gate die crit report",
			"hidden_luck": -4,
			"faith_luck": 0,
			"is_disadvantage": false,
			"required_roll": 25,
			"rng_rolls": [40],
			"expected_reason_id": BATTLE_REPORT_FORMATTER_SCRIPT.REASON_CRITICAL_SUCCESS_GATE_DIE,
			"expected_critical_source": "gate_die",
			"expected_event_tags": [],
			"expected_text_fragments": ["门骰", "d40=40/40"],
		},
		{
			"label": "disadvantage gate die crit report",
			"hidden_luck": -4,
			"faith_luck": 0,
			"is_disadvantage": true,
			"required_roll": 25,
			"rng_rolls": [40, 40],
			"expected_reason_id": BATTLE_REPORT_FORMATTER_SCRIPT.REASON_CRITICAL_SUCCESS_GATE_DIE,
			"expected_critical_source": "gate_die",
			"expected_event_tags": ["critical_success_under_disadvantage"],
			"expected_text_fragments": ["门骰", "critical_success_under_disadvantage"],
		},
		{
			"label": "high threat crit report",
			"hidden_luck": -4,
			"faith_luck": 3,
			"is_disadvantage": false,
			"required_roll": 18,
			"rng_rolls": [19],
			"expected_reason_id": BATTLE_REPORT_FORMATTER_SCRIPT.REASON_CRITICAL_SUCCESS_HIGH_THREAT,
			"expected_critical_source": "high_threat",
			"expected_event_tags": ["high_threat_critical_hit"],
			"expected_text_fragments": ["高位大成功区 19-20", "高位威胁"],
		},
		{
			"label": "high threat crit report bypasses threshold",
			"hidden_luck": 2,
			"faith_luck": 0,
			"is_disadvantage": false,
			"required_roll": 19,
			"rng_rolls": [18],
			"expected_reason_id": BATTLE_REPORT_FORMATTER_SCRIPT.REASON_CRITICAL_SUCCESS_HIGH_THREAT,
			"expected_critical_source": "high_threat",
			"expected_event_tags": ["high_threat_critical_hit"],
			"expected_text_fragments": ["高位大成功区 18-20", "高位威胁"],
		},
		{
			"label": "natural 20 ordinary hit report",
			"hidden_luck": -4,
			"faith_luck": 0,
			"is_disadvantage": false,
			"required_roll": 25,
			"rng_rolls": [1, 20],
			"expected_reason_id": BATTLE_REPORT_FORMATTER_SCRIPT.REASON_ORDINARY_HIT_GATE_DIE_PENDING,
			"expected_critical_source": "",
			"expected_event_tags": [],
			"expected_text_fragments": ["d20=20", "d40"],
		},
		{
			"label": "fumble critical fail report",
			"hidden_luck": -5,
			"faith_luck": 0,
			"is_disadvantage": false,
			"required_roll": 2,
			"rng_rolls": [1, 2],
			"expected_reason_id": BATTLE_REPORT_FORMATTER_SCRIPT.REASON_CRITICAL_FAIL_FUMBLE_BAND,
			"expected_critical_source": "",
			"expected_event_tags": ["critical_fail"],
			"expected_text_fragments": ["d20=2", "1-2", "大失败"],
		},
		{
			"label": "ordinary miss threshold report",
			"hidden_luck": 0,
			"faith_luck": 0,
			"is_disadvantage": true,
			"required_roll": 6,
			"rng_rolls": [20, 5],
			"expected_reason_id": BATTLE_REPORT_FORMATTER_SCRIPT.REASON_ORDINARY_MISS_THRESHOLD,
			"expected_critical_source": "",
			"expected_event_tags": ["ordinary_miss"],
			"expected_text_fragments": ["d20=5", "命中线 6", "普通 miss"],
		},
	]
	for case_data in cases:
		_run_attack_report_case(resolver, case_data)


func _run_cross_case(resolver, case_data: Dictionary) -> void:
	var label := String(case_data.get("label", ""))
	var attacker := _build_unit(
		StringName("attacker_%s" % label.hash()),
		int(case_data.get("hidden_luck", 0)),
		int(case_data.get("faith_luck", 0))
	)
	var target := _build_unit(
		StringName("target_%s" % label.hash()),
		0,
		0,
		BASE_TARGET_HP,
		BASE_TARGET_HP
	)
	var attack_check := _build_attack_check(int(case_data.get("required_roll", 21)))
	var rng := StubRng.new(_to_int_array(case_data.get("rng_rolls", [])))
	var result: Dictionary = resolver.resolve_attack_effects(
		attacker,
		target,
		[_build_damage_effect()],
		attack_check,
		{
			"is_disadvantage": bool(case_data.get("is_disadvantage", false)),
			"rng": rng,
		}
	)

	_assert_eq(result.get("attack_resolution", &""), case_data.get("expected_resolution", &""), "%s：attack_resolution 错误。" % label)
	_assert_eq(int(result.get("effective_luck", 0)), int(case_data.get("hidden_luck", 0)) + int(case_data.get("faith_luck", 0)), "%s：effective_luck 错误。" % label)
	_assert_eq(int(result.get("crit_gate_die", 0)), int(case_data.get("expected_crit_die", 0)), "%s：crit_gate_die 错误。" % label)
	_assert_eq(int(result.get("crit_threshold", 0)), int(case_data.get("expected_crit_threshold", 0)), "%s：crit_threshold 错误。" % label)
	_assert_eq(int(result.get("fumble_low_end", 0)), int(case_data.get("expected_fumble_low_end", 0)), "%s：fumble_low_end 错误。" % label)
	_assert_eq(int(result.get("crit_gate_roll", 0)), int(case_data.get("expected_crit_gate_roll", 0)), "%s：crit_gate_roll 错误。" % label)
	_assert_eq(int(result.get("hit_roll", 0)), int(case_data.get("expected_hit_roll", 0)), "%s：hit_roll 错误。" % label)
	_assert_eq(rng.call_count, int(case_data.get("expected_call_count", 0)), "%s：骰子消耗顺序错误。" % label)
	_assert_eq(target.current_hp, int(case_data.get("expected_target_hp", BASE_TARGET_HP)), "%s：目标 HP 结果错误。" % label)

	var expected_resolution = case_data.get("expected_resolution", &"")
	_assert_eq(bool(result.get("critical_hit", false)), expected_resolution == BATTLE_DAMAGE_RESOLVER_SCRIPT.ATTACK_RESOLUTION_CRITICAL_HIT, "%s：critical_hit 标记错误。" % label)
	_assert_eq(bool(result.get("critical_fail", false)), expected_resolution == BATTLE_DAMAGE_RESOLVER_SCRIPT.ATTACK_RESOLUTION_CRITICAL_FAIL, "%s：critical_fail 标记错误。" % label)
	_assert_eq(bool(result.get("ordinary_miss", false)), expected_resolution == BATTLE_DAMAGE_RESOLVER_SCRIPT.ATTACK_RESOLUTION_MISS, "%s：ordinary_miss 标记错误。" % label)
	_assert_eq(
		bool(result.get("attack_success", false)),
		expected_resolution == BATTLE_DAMAGE_RESOLVER_SCRIPT.ATTACK_RESOLUTION_HIT or expected_resolution == BATTLE_DAMAGE_RESOLVER_SCRIPT.ATTACK_RESOLUTION_CRITICAL_HIT,
		"%s：attack_success 标记错误。" % label
	)

	if case_data.has("expected_critical_source"):
		var expected_critical_source := String(case_data.get("expected_critical_source", ""))
		_assert_eq(
			String(result.get("critical_source", "")),
			expected_critical_source,
			"%s：critical_source 标记错误。" % label
		)

	if case_data.has("expected_event_tags"):
		var expected_event_tags = case_data.get("expected_event_tags", [])
		_assert_eq(
			result.get("fate_event_tags", []),
			expected_event_tags,
			"%s：fate_event_tags 标记错误。" % label
		)


func _run_fate_event_case(resolver, recorder: EventRecorder, case_data: Dictionary) -> void:
	var label := String(case_data.get("label", ""))
	recorder.events.clear()
	var attacker := _build_unit(
		StringName("event_attacker_%s" % label.hash()),
		int(case_data.get("hidden_luck", 0)),
		int(case_data.get("faith_luck", 0))
	)
	var defender := _build_unit(
		StringName("event_defender_%s" % label.hash()),
		0,
		0,
		BASE_TARGET_HP,
		BASE_TARGET_HP
	)
	var result: Dictionary = resolver.resolve_attack_effects(
		attacker,
		defender,
		[_build_damage_effect()],
		_build_attack_check(int(case_data.get("required_roll", 21))),
		{
			"is_disadvantage": bool(case_data.get("is_disadvantage", false)),
			"rng": StubRng.new(_to_int_array(case_data.get("rng_rolls", []))),
		}
	)

	_assert_eq(recorder.events.size(), 1, "%s：应只派发一个 focused 命运事件。" % label)
	if recorder.events.is_empty():
		return
	var event_record: Dictionary = recorder.events[0]
	var payload_variant = event_record.get("payload", null)
	_assert_true(payload_variant is Dictionary, "%s：事件 payload 应为 Dictionary。" % label)
	if payload_variant is not Dictionary:
		return
	var payload: Dictionary = payload_variant
	_assert_eq(event_record.get("event_type", &""), case_data.get("event_type", &""), "%s：事件类型错误。" % label)
	_assert_true(bool(event_record.get("payload_read_only", false)), "%s：payload 应为只读快照。" % label)
	_assert_true(bool(event_record.get("luck_snapshot_read_only", false)), "%s：luck_snapshot 应为只读快照。" % label)
	_assert_eq(payload.get("attacker_id", &""), attacker.unit_id, "%s：payload.attacker_id 错误。" % label)
	_assert_eq(payload.get("defender_id", &""), defender.unit_id, "%s：payload.defender_id 错误。" % label)
	_assert_eq(payload.get("battle_id", &""), &"", "%s：未传 battle_state 时 payload.battle_id 应回退为空。" % label)
	_assert_true(
		not bool(payload.get("defender_is_elite_or_boss", false)),
		"%s：默认测试夹具不应把普通敌人标记为 elite/boss。" % label
	)
	_assert_eq(bool(payload.get("is_disadvantage", false)), bool(case_data.get("is_disadvantage", false)), "%s：payload.is_disadvantage 错误。" % label)
	_assert_eq(int(payload.get("crit_gate_die", 0)), int(result.get("crit_gate_die", 0)), "%s：payload.crit_gate_die 错误。" % label)
	_assert_eq(int(payload.get("hit_roll", 0)), int(result.get("hit_roll", 0)), "%s：payload.hit_roll 错误。" % label)
	_assert_eq(payload.get("attack_resolution", &""), result.get("attack_resolution", &""), "%s：payload.attack_resolution 错误。" % label)
	_assert_true(not payload.has("attacker_unit"), "%s：payload 不应暴露可变 attacker 对象。" % label)
	_assert_true(not payload.has("defender_unit"), "%s：payload 不应暴露可变 defender 对象。" % label)
	var luck_snapshot_variant = payload.get("luck_snapshot", null)
	_assert_true(luck_snapshot_variant is Dictionary, "%s：payload.luck_snapshot 应为 Dictionary。" % label)
	if luck_snapshot_variant is not Dictionary:
		return
	var luck_snapshot: Dictionary = luck_snapshot_variant
	_assert_eq(int(luck_snapshot.get("hidden_luck_at_birth", 0)), int(case_data.get("hidden_luck", 0)), "%s：luck_snapshot.hidden_luck_at_birth 错误。" % label)
	_assert_eq(int(luck_snapshot.get("faith_luck_bonus", 0)), int(case_data.get("faith_luck", 0)), "%s：luck_snapshot.faith_luck_bonus 错误。" % label)
	_assert_eq(int(luck_snapshot.get("effective_luck", 0)), int(result.get("effective_luck", 0)), "%s：luck_snapshot.effective_luck 错误。" % label)
	_assert_eq(int(luck_snapshot.get("fumble_low_end", 0)), int(result.get("fumble_low_end", 0)), "%s：luck_snapshot.fumble_low_end 错误。" % label)
	_assert_eq(int(luck_snapshot.get("crit_threshold", 0)), int(result.get("crit_threshold", 0)), "%s：luck_snapshot.crit_threshold 错误。" % label)


func _run_attack_report_case(resolver, case_data: Dictionary) -> void:
	var label := String(case_data.get("label", ""))
	var attacker := _build_unit(
		StringName("report_attacker_%s" % label.hash()),
		int(case_data.get("hidden_luck", 0)),
		int(case_data.get("faith_luck", 0))
	)
	var defender := _build_unit(
		StringName("report_defender_%s" % label.hash()),
		0,
		0,
		BASE_TARGET_HP,
		BASE_TARGET_HP
	)
	var result: Dictionary = resolver.resolve_attack_effects(
		attacker,
		defender,
		[_build_damage_effect()],
		_build_attack_check(int(case_data.get("required_roll", 21))),
		{
			"is_disadvantage": bool(case_data.get("is_disadvantage", false)),
			"rng": StubRng.new(_to_int_array(case_data.get("rng_rolls", []))),
		}
	)
	var report_entry_variant = result.get("report_entry", null)
	_assert_true(report_entry_variant is Dictionary, "%s：攻击结果应暴露 report_entry。" % label)
	if report_entry_variant is not Dictionary:
		return
	var report_entry: Dictionary = report_entry_variant
	_assert_eq(
		String(report_entry.get("entry_type", "")),
		String(BATTLE_REPORT_FORMATTER_SCRIPT.ENTRY_TYPE_FATE_ATTACK),
		"%s：report_entry.entry_type 错误。" % label
	)
	_assert_eq(
		String(report_entry.get("reason_id", "")),
		String(case_data.get("expected_reason_id", &"")),
		"%s：report_entry.reason_id 错误。" % label
	)
	_assert_eq(
		String(report_entry.get("critical_source", "")),
		String(case_data.get("expected_critical_source", "")),
		"%s：report_entry.critical_source 错误。" % label
	)
	_assert_eq(
		report_entry.get("event_tags", []),
		case_data.get("expected_event_tags", []),
		"%s：report_entry.event_tags 错误。" % label
	)
	_assert_eq(
		String(report_entry.get("attacker_id", "")),
		String(attacker.unit_id),
		"%s：report_entry.attacker_id 错误。" % label
	)
	_assert_eq(
		String(report_entry.get("defender_id", "")),
		String(defender.unit_id),
		"%s：report_entry.defender_id 错误。" % label
	)
	var text := String(report_entry.get("text", ""))
	for fragment_variant in case_data.get("expected_text_fragments", []):
		var fragment := String(fragment_variant)
		_assert_true(text.contains(fragment), "%s：report_entry.text 应包含 `%s`。 actual=%s" % [label, fragment, text])


func _build_unit(
	unit_id: StringName,
	hidden_luck_at_birth: int,
	faith_luck_bonus: int,
	current_hp: int = BASE_TARGET_HP,
	max_hp: int = BASE_TARGET_HP
) -> BattleUnitState:
	var unit := BATTLE_UNIT_STATE_SCRIPT.new()
	unit.unit_id = unit_id
	unit.display_name = String(unit_id)
	unit.faction_id = &"player"
	unit.current_hp = current_hp
	unit.current_ap = 2
	unit.is_alive = true
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX, max_hp)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 0)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 0)
	unit.attribute_snapshot.set_value(UNIT_BASE_ATTRIBUTES_SCRIPT.HIDDEN_LUCK_AT_BIRTH, hidden_luck_at_birth)
	unit.attribute_snapshot.set_value(UNIT_BASE_ATTRIBUTES_SCRIPT.FAITH_LUCK_BONUS, faith_luck_bonus)
	return unit


func _add_units_to_state(state, attacker: BattleUnitState, target: BattleUnitState) -> void:
	attacker.faction_id = &"player"
	target.faction_id = &"enemy"
	attacker.set_anchor_coord(Vector2i(1, 1))
	target.set_anchor_coord(Vector2i(3, 1))
	state.units[attacker.unit_id] = attacker
	state.units[target.unit_id] = target
	state.ally_unit_ids.clear()
	state.enemy_unit_ids.clear()
	state.ally_unit_ids.append(attacker.unit_id)
	state.enemy_unit_ids.append(target.unit_id)


func _build_damage_effect():
	var damage_effect: Variant = COMBAT_EFFECT_DEF_SCRIPT.new()
	damage_effect.effect_type = &"damage"
	damage_effect.power = EXPECTED_DAMAGE
	return damage_effect


func _build_attack_check(required_roll: int) -> Dictionary:
	var attack_check := {
		"required_roll": required_roll,
		"display_required_roll": clampi(required_roll, 2, 20),
		"hit_rate_percent": 0,
		"natural_one_auto_miss": true,
		"natural_twenty_auto_hit": true,
	}
	attack_check["hit_rate_percent"] = _count_attack_check_successes(required_roll) * 5
	return attack_check


func _count_attack_check_successes(required_roll: int) -> int:
	var success_count := 0
	for roll in range(1, 21):
		if roll == 1:
			continue
		if roll == 20 or roll >= required_roll:
			success_count += 1
	return success_count


func _to_int_array(values: Variant) -> Array[int]:
	var results: Array[int] = []
	if values is not Array:
		return results
	for value in values:
		results.append(int(value))
	return results


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
