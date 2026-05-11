extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const GameRuntimeCharacterInfoBuilder = preload("res://scripts/systems/game_runtime/game_runtime_character_info_builder.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


class FakeCharacterManagement:
	extends RefCounted

	func get_identity_summary_for_member(member_id: StringName) -> Dictionary:
		if member_id != &"hero":
			return {}
		return {
			"race_label": "Human",
			"subrace_label": "High Human",
			"age_years": 24,
			"natural_age_stage_label": "Adult",
			"effective_age_stage_label": "Dragon Awakened",
			"body_size": 2,
			"body_size_category": "medium",
			"bloodline_label": "Titan",
			"bloodline_stage_label": "Awakened",
			"ascension_label": "Dragon",
			"ascension_stage_label": "Awakened",
			"damage_resistances": {&"fire": &"half"},
			"save_advantage_tags": [&"charm"],
			"trait_summary": ["Human ambition", "Dragon stage"],
			"racial_skill_lines": ["Dragon Breath（Dragon，per battle 1）"],
		}


class FakeRuntime:
	extends RefCounted

	var character_management = FakeCharacterManagement.new()

	func get_character_management():
		return character_management

	func format_coord(coord: Vector2i) -> String:
		return "(%d,%d)" % [coord.x, coord.y]

	func _get_skill_display_name(skill_id: StringName) -> String:
		return String(skill_id)


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_battle_character_info_includes_identity_section()

	if _failures.is_empty():
		print("Character info identity regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Character info identity regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_battle_character_info_includes_identity_section() -> void:
	var builder := GameRuntimeCharacterInfoBuilder.new()
	var runtime := FakeRuntime.new()
	builder.setup(runtime)
	var unit := BattleUnitState.new()
	unit.source_member_id = &"hero"
	unit.coord = Vector2i(2, 3)
	unit.current_hp = 10
	unit.current_mp = 2
	unit.attribute_snapshot.set_value(&"hp_max", 20)
	unit.attribute_snapshot.set_value(&"mp_max", 5)

	var sections := builder.build_battle_character_info_sections(unit, "战斗单位", "玩家")
	var identity_section := _find_section(sections, "身份与特性")
	_assert_true(not identity_section.is_empty(), "战斗人物信息应包含身份与特性 section。")
	var entries: Array = identity_section.get("entries", [])
	_assert_true(_has_pair_entry(entries, "种族", "Human"), "身份 section 应显示 race。")
	_assert_true(_has_pair_entry(entries, "亚种", "High Human"), "身份 section 应显示 subrace。")
	_assert_true(_has_pair_entry(entries, "有效阶段", "Dragon Awakened"), "身份 section 应显示 effective stage。")
	_assert_true(_has_pair_entry(entries, "血脉", "Titan · Awakened"), "身份 section 应显示 bloodline/stage。")
	_assert_true(_has_pair_entry(entries, "升华", "Dragon · Awakened"), "身份 section 应显示 ascension/stage。")
	_assert_true(_has_text_entry(entries, "特性：Dragon stage"), "身份 section 应显示 trait summary。")
	_assert_true(_has_text_entry(entries, "种族法术：Dragon Breath（Dragon，per battle 1）"), "身份 section 应显示 racial skill。")


func _find_section(sections: Array[Dictionary], title: String) -> Dictionary:
	for section in sections:
		if String(section.get("title", "")) == title:
			return section
	return {}


func _has_pair_entry(entries: Array, label: String, value: String) -> bool:
	for entry_variant in entries:
		var entry := entry_variant as Dictionary
		if entry == null:
			continue
		if String(entry.get("label", "")) == label and String(entry.get("value", "")) == value:
			return true
	return false


func _has_text_entry(entries: Array, text: String) -> bool:
	for entry_variant in entries:
		var entry := entry_variant as Dictionary
		if entry == null:
			continue
		if String(entry.get("text", "")) == text:
			return true
	return false


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_test.fail(message)
