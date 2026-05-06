extends SceneTree

const CHARACTER_CREATION_WINDOW_SCENE = preload("res://scenes/ui/character_creation_window.tscn")
const BODY_SIZE_RULES_SCRIPT = preload("res://scripts/systems/progression/body_size_rules.gd")
const BodySizeRules = BODY_SIZE_RULES_SCRIPT

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await _test_official_identity_pool_is_selectable()
	await _test_identity_payload_uses_registry_defaults_and_body_size_rules()
	await _test_human_versatility_preview_and_confirm_payload()

	if _failures.is_empty():
		print("Character creation identity regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Character creation identity regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_official_identity_pool_is_selectable() -> void:
	var window = CHARACTER_CREATION_WINDOW_SCENE.instantiate()
	root.add_child(window)
	await process_frame

	window.show_window()
	await process_frame

	_assert_eq(window.race_option_button.get_item_count(), 11, "建卡 UI 应暴露 11 个正式种族。")
	var dragonborn_index: int = window._find_option_index_by_metadata(window.race_option_button, &"dragonborn")
	_assert_true(dragonborn_index >= 0, "建卡 UI 应能选择 Dragonborn。")
	if dragonborn_index >= 0:
		window._on_race_option_selected(dragonborn_index)
		await process_frame
		_assert_eq(window.subrace_option_button.get_item_count(), 10, "Dragonborn 应暴露 10 个正式亚种。")
		_assert_true(
			window._find_option_index_by_metadata(window.subrace_option_button, &"red_dragonborn") >= 0,
			"Dragonborn 亚种列表应包含 Red Dragonborn。"
		)
		var dragonborn_payload: Dictionary = window._build_selected_identity_payload()
		_assert_eq(dragonborn_payload.get("subrace_id"), &"black_dragonborn", "Dragonborn 默认亚种应来自 RaceDef.default_subrace_id。")
		_assert_eq(dragonborn_payload.get("body_size_category"), &"medium", "Dragonborn 建卡体型应从正式 race 内容派生。")

	var halfling_index: int = window._find_option_index_by_metadata(window.race_option_button, &"halfling")
	_assert_true(halfling_index >= 0, "建卡 UI 应能选择 Halfling。")
	if halfling_index >= 0:
		window._on_race_option_selected(halfling_index)
		await process_frame
		var halfling_payload: Dictionary = window._build_selected_identity_payload()
		_assert_eq(halfling_payload.get("body_size_category"), &"small", "Halfling 建卡体型应从 race category 派生为 small。")
		_assert_eq(
			halfling_payload.get("body_size"),
			BodySizeRules.get_body_size_for_category(&"small"),
			"Halfling body_size int 应由 BodySizeRules 从 small 派生。"
		)

	window.queue_free()
	await process_frame


func _test_identity_payload_uses_registry_defaults_and_body_size_rules() -> void:
	var window = CHARACTER_CREATION_WINDOW_SCENE.instantiate()
	root.add_child(window)
	await process_frame

	window.show_window()
	window.name_input.text = "身份测试者"
	window._on_name_confirmed()
	await process_frame
	_set_uniform_attributes(window, 10)

	var identity_payload: Dictionary = window._build_selected_identity_payload()
	_assert_eq(identity_payload.get("race_id"), &"human", "默认 race_id 应来自正式 RaceDef。")
	_assert_eq(identity_payload.get("subrace_id"), &"common_human", "默认 subrace_id 应来自 RaceDef.default_subrace_id。")
	_assert_eq(identity_payload.get("age_profile_id"), &"human_age_profile", "默认 age_profile_id 应来自 RaceDef。")
	_assert_eq(identity_payload.get("natural_age_stage_id"), &"adult", "默认建卡阶段应优先使用 adult。")
	_assert_eq(identity_payload.get("age_years"), 24, "adult 默认年龄应来自 AgeProfile.default_age_by_stage。")
	_assert_eq(identity_payload.get("body_size_category"), &"medium", "体型 category 应从 race/subrace 身份内容派生。")
	_assert_eq(
		identity_payload.get("body_size"),
		BodySizeRules.get_body_size_for_category(&"medium"),
		"body_size int 应由 BodySizeRules 从 category 派生。"
	)

	var young_adult_index: int = window._find_option_index_by_metadata(window.age_stage_option_button, &"young_adult")
	_assert_true(young_adult_index >= 0, "建卡年龄阶段应包含正式 AgeProfile 的 young_adult。")
	window._on_age_stage_option_selected(young_adult_index)
	identity_payload = window._build_selected_identity_payload()
	_assert_eq(identity_payload.get("natural_age_stage_id"), &"young_adult", "选择 young_adult 后 payload 应同步 natural stage。")
	_assert_eq(identity_payload.get("effective_age_stage_id"), &"young_adult", "选择 young_adult 后 payload 应同步 effective stage。")
	_assert_eq(identity_payload.get("age_years"), 18, "young_adult 默认年龄应来自 AgeProfile.default_age_by_stage。")
	_assert_eq(identity_payload.get("biological_age_years"), 18, "biological_age_years 应与建卡年龄同步。")

	window.queue_free()
	await process_frame


func _test_human_versatility_preview_and_confirm_payload() -> void:
	var window = CHARACTER_CREATION_WINDOW_SCENE.instantiate()
	root.add_child(window)
	await process_frame

	window.show_window()
	window.name_input.text = "适应者"
	window._on_name_confirmed()
	await process_frame
	_set_uniform_attributes(window, 10)

	var perception_index: int = window._find_option_index_by_metadata(window.versatility_option_button, UnitBaseAttributes.PERCEPTION)
	_assert_true(perception_index >= 0, "Human Versatility 应允许选择感知。")
	window._on_versatility_option_selected(perception_index)
	var identity_payload: Dictionary = window._build_selected_identity_payload()
	_assert_eq(identity_payload.get("versatility_pick"), UnitBaseAttributes.PERCEPTION, "Human Versatility 选择应进入 payload。")
	_assert_true(
		String(window._build_attribute_preview_text()).contains("感知：10 -> 11"),
		"属性预览应显示 base 值和 Human Versatility 后的最终值。"
	)

	var emitted_payloads: Array[Dictionary] = []
	window.character_confirmed.connect(func(payload: Dictionary) -> void:
		emitted_payloads.append(payload)
	)
	window._on_confirm_pressed()
	await process_frame
	_assert_eq(emitted_payloads.size(), 1, "确认应发出一次 character_confirmed。")
	if not emitted_payloads.is_empty():
		var payload := emitted_payloads[0]
		_assert_eq(payload.get("display_name"), "适应者", "确认 payload 应保留姓名。")
		_assert_eq(payload.get("race_id"), &"human", "确认 payload 应包含 registry race_id。")
		_assert_eq(payload.get("body_size"), BodySizeRules.get_body_size_for_category(&"medium"), "确认 payload 应包含 BodySizeRules 派生体型。")
		_assert_eq(payload.get("versatility_pick"), UnitBaseAttributes.PERCEPTION, "确认 payload 应保留 Human Versatility 选择。")

	window.queue_free()
	await process_frame


func _set_uniform_attributes(window, value: int) -> void:
	window._rolled_attributes = {
		UnitBaseAttributes.STRENGTH: value,
		UnitBaseAttributes.AGILITY: value,
		UnitBaseAttributes.CONSTITUTION: value,
		UnitBaseAttributes.PERCEPTION: value,
		UnitBaseAttributes.INTELLIGENCE: value,
		UnitBaseAttributes.WILLPOWER: value,
	}


func _assert_true(value: bool, message: String) -> void:
	if not value:
		_failures.append(message)


func _assert_eq(actual: Variant, expected: Variant, message: String) -> void:
	if actual != expected:
		_failures.append("%s expected=%s actual=%s" % [message, str(expected), str(actual)])
