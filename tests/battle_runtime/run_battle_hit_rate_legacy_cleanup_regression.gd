extends SceneTree

const BattleHitResolver = preload("res://scripts/systems/battle/rules/battle_hit_resolver.gd")
const BattleHudAdapter = preload("res://scripts/ui/battle_hud_adapter.gd")
const BattleRepeatAttackResolver = preload("res://scripts/systems/battle/runtime/battle_repeat_attack_resolver.gd")
const BattleAiScoreService = preload("res://scripts/systems/battle/ai/battle_ai_score_service.gd")

var _failures: Array[String] = []


class FakeHitResolver:
	extends RefCounted

	var attack_check: Dictionary = {}

	func _init(input_attack_check: Dictionary) -> void:
		attack_check = input_attack_check

	func build_fate_aware_repeat_attack_stage_hit_check(_battle_state, _active_unit, _target_unit, _skill_def, _repeat_attack_effect, _stage_index: int) -> Dictionary:
		return attack_check.duplicate(true)


class FakeDamageResolver:
	extends RefCounted

	func resolve_attack_effects(_source_unit, _target_unit, _stage_effects: Array, _attack_check: Dictionary, _attack_context: Dictionary = {}) -> Dictionary:
		return {
			"attack_success": false,
			"attack_resolution": &"miss",
			"hit_rate_percent": 87,
			"resolution_text": "legacy 87%",
		}


class FakeRuntime:
	extends RefCounted

	var hit_resolver: FakeHitResolver
	var damage_resolver := FakeDamageResolver.new()

	func _init(input_attack_check: Dictionary) -> void:
		hit_resolver = FakeHitResolver.new(input_attack_check)

	func get_hit_resolver():
		return hit_resolver

	func get_damage_resolver():
		return damage_resolver

	func get_state():
		return null


class FakePreview:
	extends RefCounted

	var hit_preview: Dictionary = {}

	func _init(input_hit_preview: Dictionary) -> void:
		hit_preview = input_hit_preview.duplicate(true)


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_hit_resolver_preview_requires_success_rate()
	_test_repeat_attack_result_requires_success_rate()
	_test_hud_badge_requires_success_rate()
	_test_ai_score_service_requires_success_rate()
	if _failures.is_empty():
		print("Battle hit_rate legacy cleanup regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Battle hit_rate legacy cleanup regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_hit_resolver_preview_requires_success_rate() -> void:
	var resolver := BattleHitResolver.new()
	var legacy_only_check := {
		"hit_rate_percent": 87,
		"base_hit_rate_percent": 0,
		"required_roll": 4,
	}
	var plain_legacy_text := resolver.format_attack_check_preview(legacy_only_check)
	_assert_true(
		plain_legacy_text.begins_with("0%") and not plain_legacy_text.contains("87"),
		"plain attack preview must ignore legacy-only hit_rate_percent."
	)
	var fate_legacy_text := resolver._format_fate_aware_attack_check_preview(legacy_only_check)
	_assert_true(
		fate_legacy_text.begins_with("0%") and not fate_legacy_text.contains("87"),
		"fate-aware attack preview must ignore legacy-only hit_rate_percent."
	)

	var formal_check := legacy_only_check.duplicate(true)
	formal_check["success_rate_percent"] = 42
	_assert_true(
		resolver._format_fate_aware_attack_check_preview(formal_check).begins_with("42%"),
		"fate-aware attack preview must use formal success_rate_percent."
	)


func _test_repeat_attack_result_requires_success_rate() -> void:
	var resolver := BattleRepeatAttackResolver.new()
	var legacy_runtime := FakeRuntime.new({
		"hit_rate_percent": 87,
		"required_roll": 4,
	})
	resolver.setup(legacy_runtime)
	var legacy_result: Dictionary = resolver._resolve_repeat_attack_stage_result(null, null, null, null, 0, [])
	_assert_eq(
		int(legacy_result.get("success_rate_percent", -1)),
		0,
		"repeat attack result must not derive success_rate_percent from legacy-only hit_rate_percent."
	)
	_assert_eq(
		int(legacy_result.get("hit_rate_percent", -1)),
		0,
		"repeat attack result legacy hit_rate_percent alias must mirror formal success rate."
	)
	_assert_false(
		String(legacy_result.get("resolution_text", "")).contains("87"),
		"repeat attack resolution text must not display legacy-only hit_rate_percent."
	)
	resolver.dispose()

	var formal_runtime := FakeRuntime.new({
		"hit_rate_percent": 87,
		"success_rate_percent": 42,
		"required_roll": 4,
	})
	resolver.setup(formal_runtime)
	var formal_result: Dictionary = resolver._resolve_repeat_attack_stage_result(null, null, null, null, 0, [])
	_assert_eq(
		int(formal_result.get("success_rate_percent", -1)),
		42,
		"repeat attack result must use formal success_rate_percent."
	)
	_assert_eq(
		String(formal_result.get("resolution_text", "")),
		"42%",
		"repeat attack resolution fallback text must use formal success_rate_percent."
	)
	resolver.dispose()


func _test_hud_badge_requires_success_rate() -> void:
	var adapter := BattleHudAdapter.new()
	_assert_eq(
		adapter._build_selected_skill_hit_badge_text({"hit_rate_percent": 87}),
		"",
		"HUD hit badge must ignore legacy-only hit_rate_percent."
	)
	_assert_eq(
		adapter._build_selected_skill_hit_badge_text({"stage_hit_rates": [87]}),
		"",
		"HUD hit badge must ignore legacy-only stage_hit_rates."
	)
	var formal_badge := String(adapter._build_selected_skill_hit_badge_text({"success_rate_percent": 42, "hit_rate_percent": 87}))
	_assert_true(
		formal_badge.contains("42%") and not formal_badge.contains("87"),
		"HUD hit badge must use formal success_rate_percent."
	)
	var formal_stage_badge := String(adapter._build_selected_skill_hit_badge_text({"stage_success_rates": [43]}))
	_assert_true(
		formal_stage_badge.contains("43%"),
		"HUD hit badge may use formal stage_success_rates."
	)


func _test_ai_score_service_requires_success_rate() -> void:
	var score_service := BattleAiScoreService.new()
	_assert_eq(
		score_service._resolve_estimated_hit_rate_percent(FakePreview.new({"hit_rate_percent": 87})),
		100,
		"AI score estimated_hit_rate_percent must ignore legacy-only hit_rate_percent."
	)
	_assert_eq(
		score_service._resolve_estimated_hit_rate_percent(FakePreview.new({"stage_hit_rates": [87]})),
		100,
		"AI score estimated_hit_rate_percent must ignore legacy-only stage_hit_rates."
	)
	_assert_eq(
		score_service._resolve_estimated_hit_rate_percent(FakePreview.new({"success_rate_percent": 42, "hit_rate_percent": 87})),
		42,
		"AI score estimated_hit_rate_percent must use formal success_rate_percent."
	)
	_assert_eq(
		score_service._resolve_estimated_hit_rate_percent(FakePreview.new({"stage_success_rates": [40, 60], "stage_hit_rates": [87]})),
		50,
		"AI score estimated_hit_rate_percent must use formal stage_success_rates."
	)


func _assert_eq(actual, expected, message: String) -> void:
	if actual == expected:
		return
	_failures.append("%s expected=%s actual=%s" % [message, str(expected), str(actual)])


func _assert_false(value: bool, message: String) -> void:
	if not value:
		return
	_failures.append(message)


func _assert_true(value: bool, message: String) -> void:
	if value:
		return
	_failures.append(message)
