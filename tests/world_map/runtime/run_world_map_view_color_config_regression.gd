extends SceneTree

const GameSessionScript = preload("res://scripts/systems/game_session.gd")
const WorldMapScene = preload("res://scenes/main/world_map.tscn")
const SettlementConfig = preload("res://scripts/utils/settlement_config.gd")

const TEST_CONFIG_PATH := "res://data/configs/world_map/test_world_map_config.tres"

var _failures: Array[String] = []
var _game_session = null


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await _ensure_game_session()
	await _reset_session()
	await _test_world_map_scene_exposes_default_view_palette()
	await _cleanup()

	if _failures.is_empty():
		print("World map view color config regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("World map view color config regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_world_map_scene_exposes_default_view_palette() -> void:
	var create_error := int(_game_session.start_new_game(TEST_CONFIG_PATH))
	_assert_eq(create_error, OK, "world_map view color 回归前置：应能成功创建测试世界。")
	if create_error != OK:
		return

	var world_map := WorldMapScene.instantiate()
	root.add_child(world_map)
	await process_frame
	await process_frame

	var world_map_view = world_map.get_node("MapViewport/WorldMapView")
	_assert_true(world_map_view != null, "world_map.tscn 应继续提供 WorldMapView 节点。")
	_assert_true(world_map_view != null and world_map_view.visible, "世界态初始化后 WorldMapView 应保持可见。")
	_assert_true(world_map_view != null and world_map_view.size.x > 0.0 and world_map_view.size.y > 0.0, "WorldMapView 应在场景中获得有效尺寸。")

	if world_map_view != null:
		world_map._render_from_runtime(true)
		await process_frame

		var expected_colors := {
			"selection_outline_color": Color(0.98, 0.9, 0.42, 0.95),
			"world_event_marker_fill_color": Color(0.95, 0.78, 0.28, 0.96),
			"world_event_marker_outline_color": Color(0.25, 0.11, 0.02, 1.0),
			"world_event_marker_center_color": Color(0.32, 0.06, 0.02, 1.0),
			"encounter_marker_outer_color": Color(0.87, 0.28, 0.23, 0.95),
			"encounter_marker_inner_color": Color(0.15, 0.02, 0.02, 0.95),
			"npc_marker_body_color": Color(0.42, 0.77, 0.87, 0.95),
			"npc_marker_head_color": Color(0.88, 0.94, 0.98, 1.0),
			"village_tier_color": Color(0.57, 0.75, 0.43, 1.0),
			"town_tier_color": Color(0.51, 0.7, 0.84, 1.0),
			"city_tier_color": Color(0.78, 0.63, 0.42, 1.0),
			"capital_tier_color": Color(0.74, 0.48, 0.76, 1.0),
			"world_stronghold_tier_color": Color(0.9, 0.43, 0.31, 1.0),
			"metropolis_tier_color": Color(0.95, 0.82, 0.45, 1.0),
			"fallback_tier_color": Color(0.5, 0.5, 0.5, 1.0),
		}

		for property_name in expected_colors.keys():
			_assert_true(
				_property_list_has_name(world_map_view, property_name),
				"WorldMapView 应把 %s 暴露为可配置导出字段。" % property_name
			)
			_assert_eq(
				world_map_view.get(property_name),
				expected_colors[property_name],
				"WorldMapView 的 %s 默认值应继续贴近当前主线视觉。" % property_name
			)

		_assert_true(
			_property_list_has_name(world_map_view, "village_settlement_texture"),
			"WorldMapView 应把 village_settlement_texture 暴露为可配置导出字段。"
		)
		var village_texture := world_map_view.get("village_settlement_texture") as Texture2D
		_assert_true(village_texture != null, "world_map.tscn 应绑定村级据点贴图。")
		if village_texture != null:
			_assert_eq(
				village_texture.resource_path,
				"res://assets/main/basic_map/village.png",
				"world_map.tscn 的村级据点贴图应指向重命名后的 village.png。"
			)

		var tier_to_property := {
			SettlementConfig.SettlementTier.VILLAGE: "village_tier_color",
			SettlementConfig.SettlementTier.TOWN: "town_tier_color",
			SettlementConfig.SettlementTier.CITY: "city_tier_color",
			SettlementConfig.SettlementTier.CAPITAL: "capital_tier_color",
			SettlementConfig.SettlementTier.WORLD_STRONGHOLD: "world_stronghold_tier_color",
			SettlementConfig.SettlementTier.METROPOLIS: "metropolis_tier_color",
		}
		for tier in tier_to_property.keys():
			var property_name: String = tier_to_property[tier]
			_assert_eq(
				world_map_view._get_settlement_color(tier),
				world_map_view.get(property_name),
				"tier %s 应通过导出字段提供颜色，而不是回退到 draw 内硬编码。" % str(tier)
			)

	world_map.queue_free()
	await process_frame


func _ensure_game_session() -> void:
	_game_session = root.get_node_or_null("GameSession")
	if _game_session != null:
		return

	_game_session = GameSessionScript.new()
	_game_session.name = "GameSession"
	root.add_child(_game_session)
	await process_frame


func _reset_session() -> void:
	if _game_session == null:
		return
	_game_session.clear_persisted_game()
	await process_frame


func _cleanup() -> void:
	if _game_session == null:
		return
	_game_session.clear_persisted_game()
	await process_frame


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])


func _property_list_has_name(instance: Object, property_name: String) -> bool:
	if instance == null:
		return false

	for property_info in instance.get_property_list():
		if String(property_info.get("name", "")) == property_name:
			return true
	return false
