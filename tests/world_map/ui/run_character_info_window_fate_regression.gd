extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const CHARACTER_INFO_WINDOW_SCENE = preload("res://scenes/ui/character_info_window.tscn")

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await _test_character_info_window_renders_fate_section_happy_path()
	await _test_character_info_window_rejects_bad_section_schema()
	await _test_character_info_window_rejects_bad_fate_payload()

	if _failures.is_empty():
		print("CharacterInfoWindow fate regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("CharacterInfoWindow fate regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_character_info_window_renders_fate_section_happy_path() -> void:
	var window = CHARACTER_INFO_WINDOW_SCENE.instantiate()
	root.add_child(window)
	await process_frame

	window.show_character({
		"display_name": "黑冠见证者",
		"meta_label": "战斗单位  |  玩家前排",
		"sections": [
			{
				"title": "基础概览",
				"entries": [
					{
						"label": "职业",
						"value": "见厄者",
					},
				],
			},
		],
		"fate": {
			"hidden_luck_at_birth": 7,
			"faith_luck_bonus": -13,
			"effective_luck": -6,
			"fortune_marked": 1,
			"doom_marked": 1,
			"doom_authority": 4,
			"has_misfortune": true,
		},
		"status_label": "战斗单位",
	})
	await process_frame

	_assert_eq(window.sections_container.get_child_count(), 2, "显式 sections + fate payload 应渲染为两个段落。")
	var rendered_texts := _collect_label_texts(window.sections_container)
	_assert_true(rendered_texts.has("命运"), "happy path 应追加命运段落标题。")
	_assert_true(rendered_texts.has("生来暗运："), "happy path 应渲染生来暗运标签。")
	_assert_true(rendered_texts.has("+7"), "happy path 应按原值显示 hidden_luck_at_birth=+7。")
	_assert_true(rendered_texts.has("信仰赐运："), "happy path 应渲染信仰赐运标签。")
	_assert_true(rendered_texts.has("-13"), "happy path 应渲染 faith_luck_bonus。")
	_assert_true(rendered_texts.has("有效运势："), "happy path 应渲染有效运势标签。")
	_assert_true(rendered_texts.has("-6"), "happy path 应渲染 effective_luck=-6。")
	_assert_true(rendered_texts.has("1（已获福印）"), "happy path 应渲染 fortune_marked。")
	_assert_true(rendered_texts.has("1（已见黑兆）"), "happy path 应渲染 doom_marked。")
	_assert_true(rendered_texts.has("厄权："), "已入 Misfortune 时应显示 doom_authority 标签。")
	_assert_true(rendered_texts.has("4 级"), "已入 Misfortune 时应显示 doom_authority 值。")
	_assert_true(
		rendered_texts.has("生来暗运已处于极端正运档，界面会按原值保留该刻印。"),
		"hidden_luck_at_birth=+7 时应给出极端正运提示。"
	)
	_assert_true(
		rendered_texts.has("有效运势已压到 -6 下限：大失败区间会扩到 1-3；若处于劣势，命运的怜悯仍只回拉一档暴击门。"),
		"effective_luck=-6 时应给出下限提示。"
	)

	window.queue_free()
	await process_frame


func _test_character_info_window_rejects_bad_section_schema() -> void:
	var window = CHARACTER_INFO_WINDOW_SCENE.instantiate()
	root.add_child(window)
	await process_frame

	var missing_sections := _make_valid_character_info_payload()
	missing_sections.erase("sections")
	window.show_character(missing_sections)
	await process_frame
	_assert_true(not window.visible, "缺少 sections 的人物信息 payload 应拒绝。")
	_assert_eq(window.sections_container.get_child_count(), 0, "缺少 sections 时不应渲染默认 section。")

	var legacy_top_level := _make_valid_character_info_payload({
		"type_label": "世界 NPC",
	})
	window.show_character(legacy_top_level)
	await process_frame
	_assert_true(not window.visible, "含旧 type_label 的人物信息 payload 应拒绝。")
	_assert_eq(window.sections_container.get_child_count(), 0, "旧 top-level 字段不应被忽略后继续渲染。")

	var legacy_section_shape := _make_valid_character_info_payload({
		"sections": [
			{
				"title": "旧段落",
				"entries": [
					{
						"text": "正式 text entry。",
					},
				],
				"body": "旧 body 字段。",
				"rows": [
					{
						"text": "旧 rows 字段。",
					},
				],
				"lines": [
					"旧 lines 字段。",
				],
			},
		],
	})
	window.show_character(legacy_section_shape)
	await process_frame
	_assert_true(not window.visible, "section 含旧 body/rows/lines 字段时应拒绝整份 payload。")
	_assert_eq(window.sections_container.get_child_count(), 0, "旧 body/rows/lines 字段不应被忽略后继续渲染。")

	var value_only_entry := _make_valid_character_info_payload({
		"sections": [
			{
				"title": "装备摘要",
				"entries": [
					{
						"value": "塔盾",
					},
				],
			},
		],
	})
	window.show_character(value_only_entry)
	await process_frame
	_assert_true(not window.visible, "value-only entry 不属于当前 schema，应拒绝整份 payload。")
	_assert_eq(window.sections_container.get_child_count(), 0, "value-only entry 不应被转换成 text entry。")

	var mixed_entry_shape := _make_valid_character_info_payload({
		"sections": [
			{
				"title": "基础概览",
				"entries": [
					{
						"label": "职业",
						"value": "旅人",
						"text": "旧混合条目。",
					},
				],
			},
		],
	})
	window.show_character(mixed_entry_shape)
	await process_frame
	_assert_true(not window.visible, "entry 只能是 {label,value} 或 {text}，混合字段应拒绝。")
	_assert_eq(window.sections_container.get_child_count(), 0, "混合 entry 不应按 label/value 局部渲染。")

	window.queue_free()
	await process_frame


func _test_character_info_window_rejects_bad_fate_payload() -> void:
	var window = CHARACTER_INFO_WINDOW_SCENE.instantiate()
	root.add_child(window)
	await process_frame

	window.show_character(_make_valid_character_info_payload({
		"fate": null,
	}))
	await process_frame

	_assert_true(not window.visible, "显式 null fate payload 应拒绝整个人物信息 payload。")
	_assert_eq(window.sections_container.get_child_count(), 0, "显式 null fate 不应被当成缺省 fate 渲染基础 section。")

	window.show_character({
		"display_name": "未命名旅人",
		"meta_label": "战斗单位",
		"sections": [
			{
				"title": "基础概览",
				"entries": [
					{
						"label": "职业",
						"value": "旅人",
					},
				],
			},
		],
		"fate": {
			"hidden_luck_at_birth": -6,
		},
		"status_label": "",
	})
	await process_frame

	_assert_true(not window.visible, "缺字段 fate payload 应拒绝整个人物信息 payload。")
	_assert_eq(window.sections_container.get_child_count(), 0, "缺字段 fate payload 不应保留基础 section。")

	window.show_character({
		"display_name": "字符串运势旅人",
		"meta_label": "战斗单位",
		"sections": [
			{
				"title": "基础概览",
				"entries": [
					{
						"label": "职业",
						"value": "旅人",
					},
				],
			},
		],
		"fate": {
			"hidden_luck_at_birth": -6,
			"faith_luck_bonus": 0,
			"effective_luck": "-6",
			"fortune_marked": 0,
			"doom_marked": 0,
			"doom_authority": 0,
			"has_misfortune": false,
		},
		"status_label": "",
	})
	await process_frame

	_assert_true(not window.visible, "错类型 fate payload 不应被字符串转 int 后展示。")
	_assert_eq(window.sections_container.get_child_count(), 0, "错类型 fate payload 不应渲染任何 section。")

	window.queue_free()
	await process_frame


func _make_valid_character_info_payload(overrides: Dictionary = {}) -> Dictionary:
	var data := {
		"display_name": "严格旅人",
		"meta_label": "战斗单位",
		"sections": [
			{
				"title": "基础概览",
				"entries": [
					{
						"label": "职业",
						"value": "旅人",
					},
				],
			},
		],
		"status_label": "",
	}
	for key in overrides.keys():
		data[key] = overrides[key]
	return data


func _collect_label_texts(node: Node) -> Array[String]:
	var texts: Array[String] = []
	if node is Label:
		texts.append((node as Label).text)
	for child in node.get_children():
		texts.append_array(_collect_label_texts(child))
	return texts


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_test.fail(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_test.fail("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
