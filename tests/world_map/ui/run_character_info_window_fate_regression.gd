extends SceneTree

const CHARACTER_INFO_WINDOW_SCENE = preload("res://scenes/ui/character_info_window.tscn")

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await _test_character_info_window_renders_fate_section_happy_path()
	await _test_character_info_window_fate_section_falls_back_for_missing_fields()

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


func _test_character_info_window_fate_section_falls_back_for_missing_fields() -> void:
	var window = CHARACTER_INFO_WINDOW_SCENE.instantiate()
	root.add_child(window)
	await process_frame

	window.show_character({
		"display_name": "未命名旅人",
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
	})
	await process_frame

	_assert_eq(window.sections_container.get_child_count(), 2, "缺字段的 fate payload 仍应补出命运段落。")
	var rendered_texts := _collect_label_texts(window.sections_container)
	_assert_true(rendered_texts.has("命运"), "缺字段 fallback 仍应渲染命运标题。")
	_assert_true(rendered_texts.has("-6"), "缺少 effective_luck 时应从 hidden_luck_at_birth 回退出 -6。")
	_assert_true(rendered_texts.has("0"), "缺少 faith_luck_bonus 时应回退为 0。")
	_assert_true(rendered_texts.has("0（未获福印）"), "缺少 fortune_marked 时应回退为未获福印。")
	_assert_true(rendered_texts.has("0（未见黑兆）"), "缺少 doom_marked 时应回退为未见黑兆。")
	_assert_true(not rendered_texts.has("厄权："), "未入 Misfortune 时不应显示 doom_authority。")
	_assert_true(
		rendered_texts.has("生来暗运已压到最深坏运档，这类角色更容易撞进命运事件的极端分支。"),
		"hidden_luck_at_birth=-6 时应显示最深坏运提示。"
	)
	_assert_true(
		rendered_texts.has("有效运势已压到 -6 下限：大失败区间会扩到 1-3；若处于劣势，命运的怜悯仍只回拉一档暴击门。"),
		"缺少 effective_luck 字段时仍应补出 -6 下限提示。"
	)

	window.queue_free()
	await process_frame


func _collect_label_texts(node: Node) -> Array[String]:
	var texts: Array[String] = []
	if node is Label:
		texts.append((node as Label).text)
	for child in node.get_children():
		texts.append_array(_collect_label_texts(child))
	return texts


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
