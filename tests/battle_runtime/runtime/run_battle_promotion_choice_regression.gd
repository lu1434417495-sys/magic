extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const BattleRuntimeModule = preload("res://scripts/systems/battle/runtime/battle_runtime_module.gd")
const BattleState = preload("res://scripts/systems/battle/core/battle_state.gd")
const BattleTimelineState = preload("res://scripts/systems/battle/core/battle_timeline_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const CharacterProgressionDelta = preload("res://scripts/systems/progression/character_progression_delta.gd")

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_failed_promotion_keeps_modal_and_timeline_frozen()
	_test_successful_promotion_clears_modal_and_unfreezes_timeline()

	if _failures.is_empty():
		print("Battle promotion choice regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Battle promotion choice regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_failed_promotion_keeps_modal_and_timeline_frozen() -> void:
	var gateway := _FakeCharacterGateway.new()
	gateway.delta_to_return = _build_delta(&"hero", &"", false)
	var runtime := _build_runtime(gateway)
	var state: BattleState = runtime.get_state()

	var batch = runtime.submit_promotion_choice(&"hero", &"warrior", {})
	_assert_true(batch.progression_deltas.is_empty(), "Failed promotion must not append a progression delta.")
	_assert_eq(String(state.modal_state), "promotion_choice", "Failed promotion must keep the battle modal open.")
	_assert_true(state.timeline.frozen, "Failed promotion must keep the battle timeline frozen.")
	_assert_true(batch.modal_requested, "Failed promotion should request a modal refresh.")
	_assert_true(not _contains_text(batch.log_lines, "completed promotion"), "Failed promotion must not log success.")
	_assert_eq(gateway.promote_calls, 1, "Promotion submit should still reach the character gateway once.")


func _test_successful_promotion_clears_modal_and_unfreezes_timeline() -> void:
	var gateway := _FakeCharacterGateway.new()
	gateway.delta_to_return = _build_delta(&"hero", &"warrior", false)
	var runtime := _build_runtime(gateway)
	var state: BattleState = runtime.get_state()

	var batch = runtime.submit_promotion_choice(&"hero", &"warrior", {})
	_assert_eq(batch.progression_deltas.size(), 1, "Successful promotion must append exactly one progression delta.")
	_assert_eq(String(state.modal_state), "", "Successful promotion should clear the battle modal.")
	_assert_true(not state.timeline.frozen, "Successful promotion should unfreeze the battle timeline.")
	_assert_true(batch.changed_unit_ids.has(&"unit_hero"), "Successful promotion should refresh the promoted battle unit.")
	_assert_true(_contains_text(batch.log_lines, "完成职业晋升"), "Successful promotion should log completion.")


func _build_runtime(gateway: _FakeCharacterGateway) -> BattleRuntimeModule:
	var runtime := BattleRuntimeModule.new()
	runtime.setup(gateway, {}, {}, {})
	var state := BattleState.new()
	state.timeline = BattleTimelineState.new()
	state.timeline.frozen = true
	state.modal_state = &"promotion_choice"
	var unit := BattleUnitState.new()
	unit.unit_id = &"unit_hero"
	unit.source_member_id = &"hero"
	unit.display_name = "Hero"
	state.units[unit.unit_id] = unit
	runtime._state = state
	return runtime


func _build_delta(member_id: StringName, changed_profession_id: StringName, needs_follow_up: bool) -> CharacterProgressionDelta:
	var delta := CharacterProgressionDelta.new()
	delta.member_id = member_id
	delta.needs_promotion_modal = needs_follow_up
	if changed_profession_id != &"":
		delta.changed_profession_ids.append(changed_profession_id)
	return delta


func _contains_text(values: Array, needle: String) -> bool:
	for value in values:
		if String(value).contains(needle):
			return true
	return false


class _FakeCharacterGateway extends RefCounted:
	var delta_to_return: CharacterProgressionDelta = null
	var promote_calls := 0


	func promote_profession(_member_id: StringName, _profession_id: StringName, _selection: Dictionary):
		promote_calls += 1
		return delta_to_return


	func get_member_state(_member_id: StringName):
		return null


	func get_item_defs() -> Dictionary:
		return {}


func _assert_true(condition: bool, message: String) -> void:
	if condition:
		return
	_test.fail(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual == expected:
		return
	_test.fail("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
