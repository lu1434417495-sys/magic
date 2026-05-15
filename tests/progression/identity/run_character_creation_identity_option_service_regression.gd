extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const AgeProfileDef = preload("res://scripts/player/progression/age_profile_def.gd")
const AgeStageRule = preload("res://scripts/player/progression/age_stage_rule.gd")
const CharacterCreationIdentityOptionService = preload("res://scripts/systems/progression/character_creation_identity_option_service.gd")
const RaceDef = preload("res://scripts/player/progression/race_def.gd")
const SubraceDef = preload("res://scripts/player/progression/subrace_def.gd")

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


class IdentityOptionFixtureRegistry:
	extends RefCounted

	var race_defs: Dictionary = {}
	var subrace_defs: Dictionary = {}
	var age_profile_defs: Dictionary = {}
	var race_trait_defs: Dictionary = {}

	func get_race_defs() -> Dictionary:
		return race_defs.duplicate()

	func get_subrace_defs() -> Dictionary:
		return subrace_defs.duplicate()

	func get_age_profile_defs() -> Dictionary:
		return age_profile_defs.duplicate()

	func get_race_trait_defs() -> Dictionary:
		return race_trait_defs.duplicate()


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_collect_subraces_uses_only_bidirectional_edges()
	_test_collect_subraces_filters_missing_and_orders_legal_candidates()
	_test_choose_subrace_handles_default_and_stale_selection()
	_test_race_without_legal_subrace_has_no_fallback()
	_test_string_key_content_source_is_supported()
	_test_collect_creation_races_requires_legal_pair()
	_test_is_valid_creation_pair_rejects_parent_only_and_wrong_parent()
	_test.finish(self, "Character creation identity option service regression")


func _test_collect_subraces_uses_only_bidirectional_edges() -> void:
	var registry := _make_fixture_registry()
	var ids: Array[StringName] = CharacterCreationIdentityOptionService.collect_subrace_ids_for_race(registry, &"human")

	_assert_true(ids.has(&"common_human"), "合法 common_human 应进入 human 建卡候选。")
	_assert_true(ids.has(&"noble_human"), "合法 noble_human 应进入 human 建卡候选。")
	_assert_true(not ids.has(&"parent_only_human"), "parent_race_id 指向 human 但未被 race.subrace_ids 列出的 subrace 不得进入候选。")
	_assert_true(not ids.has(&"wrong_parent"), "race.subrace_ids 列出但 parent_race_id 不匹配的 subrace 不得进入候选。")


func _test_collect_subraces_filters_missing_and_orders_legal_candidates() -> void:
	var registry := _make_fixture_registry()
	var ids: Array[StringName] = CharacterCreationIdentityOptionService.collect_subrace_ids_for_race(registry, &"human")
	_assert_eq(ids, [&"common_human", &"noble_human"], "候选应过滤 missing/wrong-parent，并保持稳定字典序。")


func _test_choose_subrace_handles_default_and_stale_selection() -> void:
	var registry := _make_fixture_registry()

	var default_choice: StringName = CharacterCreationIdentityOptionService.choose_subrace_id(registry, &"human", &"")
	_assert_eq(default_choice, &"common_human", "合法 default_subrace_id 应优先成为选择。")

	var explicit_legal: StringName = CharacterCreationIdentityOptionService.choose_subrace_id(registry, &"human", &"noble_human")
	_assert_eq(explicit_legal, &"noble_human", "当前选择仍合法时应保留。")

	var stale_choice: StringName = CharacterCreationIdentityOptionService.choose_subrace_id(registry, &"human", &"parent_only_human")
	_assert_eq(stale_choice, &"common_human", "stale parent-only subrace 必须被纠正为合法候选。")

	var invalid_default_choice: StringName = CharacterCreationIdentityOptionService.choose_subrace_id(registry, &"invalid_default_race", &"")
	_assert_eq(invalid_default_choice, &"valid_for_invalid_default", "default_subrace_id 非法时应选择第一个合法候选。")


func _test_race_without_legal_subrace_has_no_fallback() -> void:
	var registry := _make_fixture_registry()
	var ids: Array[StringName] = CharacterCreationIdentityOptionService.collect_subrace_ids_for_race(registry, &"orphan_race")
	var choice: StringName = CharacterCreationIdentityOptionService.choose_subrace_id(registry, &"orphan_race", &"")

	_assert_true(ids.is_empty(), "无合法 subrace 的 race 应返回空候选，不扫描 parent_race fallback。")
	_assert_eq(choice, &"", "无合法 subrace 的 race 不应产生默认选择。")


func _test_string_key_content_source_is_supported() -> void:
	var registry := _make_fixture_registry()
	var ids: Array[StringName] = CharacterCreationIdentityOptionService.collect_subrace_ids_for_race(registry, &"string_key_race")
	var race_choice: StringName = CharacterCreationIdentityOptionService.choose_race_id(registry, &"", &"string_key_race")
	var subrace_choice: StringName = CharacterCreationIdentityOptionService.choose_subrace_id(registry, &"string_key_race", &"")

	_assert_eq(ids, [&"string_key_subrace"], "String 字典 key 的 race/subrace 也应被候选服务解析为 StringName。")
	_assert_eq(race_choice, &"string_key_race", "String key race 可作为合法默认 race。")
	_assert_eq(subrace_choice, &"string_key_subrace", "String key subrace 可作为合法默认 subrace。")


func _test_collect_creation_races_requires_legal_pair() -> void:
	var registry := _make_fixture_registry()
	var ids: Array[StringName] = CharacterCreationIdentityOptionService.collect_creation_race_ids(registry)

	_assert_true(ids.has(&"human"), "human 有合法 subrace，应进入 race 候选。")
	_assert_true(ids.has(&"invalid_default_race"), "default 非法但存在合法 subrace 的 race 仍应进入候选。")
	_assert_true(ids.has(&"string_key_race"), "String key race 有合法 pair，应进入 race 候选。")
	_assert_true(not ids.has(&"orphan_race"), "无合法 subrace 的 race 不应进入建卡 race 候选。")


func _test_is_valid_creation_pair_rejects_parent_only_and_wrong_parent() -> void:
	var registry := _make_fixture_registry()

	_assert_true(
		CharacterCreationIdentityOptionService.is_valid_creation_race_subrace_pair(registry, &"human", &"common_human"),
		"合法 race/subrace pair 应通过。"
	)
	_assert_true(
		not CharacterCreationIdentityOptionService.is_valid_creation_race_subrace_pair(registry, &"human", &"parent_only_human"),
		"parent-only subrace 不得作为合法 pair。"
	)
	_assert_true(
		not CharacterCreationIdentityOptionService.is_valid_creation_race_subrace_pair(registry, &"human", &"wrong_parent"),
		"parent mismatch subrace 不得作为合法 pair。"
	)


func _make_fixture_registry() -> IdentityOptionFixtureRegistry:
	var registry := IdentityOptionFixtureRegistry.new()
	var human := _make_race(&"human", &"common_human", [&"common_human", &"noble_human", &"wrong_parent", &"missing_subrace"])
	var orphan_race := _make_race(&"orphan_race", &"", [])
	var invalid_default_race := _make_race(&"invalid_default_race", &"parent_only_invalid_default", [&"valid_for_invalid_default"])
	var string_key_race := _make_race(&"string_key_race", &"string_key_subrace", [&"string_key_subrace"])

	registry.race_defs = {
		human.race_id: human,
		orphan_race.race_id: orphan_race,
		invalid_default_race.race_id: invalid_default_race,
		"string_key_race": string_key_race,
	}
	registry.subrace_defs = {
		&"common_human": _make_subrace(&"common_human", &"human"),
		&"noble_human": _make_subrace(&"noble_human", &"human"),
		&"parent_only_human": _make_subrace(&"parent_only_human", &"human"),
		&"wrong_parent": _make_subrace(&"wrong_parent", &"elf"),
		&"parent_only_invalid_default": _make_subrace(&"parent_only_invalid_default", &"invalid_default_race"),
		&"valid_for_invalid_default": _make_subrace(&"valid_for_invalid_default", &"invalid_default_race"),
		"string_key_subrace": _make_subrace(&"string_key_subrace", &"string_key_race"),
	}
	registry.age_profile_defs = {&"human_age_profile": _make_age_profile(&"human_age_profile", &"human")}
	return registry


func _make_race(race_id: StringName, default_subrace_id: StringName, subrace_ids: Array) -> RaceDef:
	var race := RaceDef.new()
	race.race_id = race_id
	race.display_name = String(race_id)
	race.age_profile_id = &"human_age_profile"
	race.default_subrace_id = default_subrace_id
	race.subrace_ids = _typed_string_names(subrace_ids)
	race.body_size_category = &"medium"
	return race


func _make_subrace(subrace_id: StringName, parent_race_id: StringName) -> SubraceDef:
	var subrace := SubraceDef.new()
	subrace.subrace_id = subrace_id
	subrace.parent_race_id = parent_race_id
	subrace.display_name = String(subrace_id)
	return subrace


func _make_age_profile(profile_id: StringName, race_id: StringName) -> AgeProfileDef:
	var age_profile := AgeProfileDef.new()
	age_profile.profile_id = profile_id
	age_profile.race_id = race_id
	age_profile.creation_stage_ids = _typed_string_names([&"adult"])
	age_profile.default_age_by_stage = {"adult": 24}
	var adult := AgeStageRule.new()
	adult.stage_id = &"adult"
	adult.display_name = "Adult"
	adult.selectable_in_creation = true
	var stage_rules: Array[AgeStageRule] = [adult]
	age_profile.stage_rules = stage_rules
	return age_profile


func _typed_string_names(values: Array) -> Array[StringName]:
	var result: Array[StringName] = []
	for value in values:
		result.append(StringName(String(value)))
	return result


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_test.fail(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_test.fail("%s | actual=%s expected=%s" % [message, var_to_str(actual), var_to_str(expected)])
