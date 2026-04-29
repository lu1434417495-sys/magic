extends SceneTree
const GameRuntimeFacade = preload("res://scripts/systems/game_runtime/game_runtime_facade.gd")
const GameRuntimeBattleSelection = preload("res://scripts/systems/game_runtime/game_runtime_battle_selection.gd")
const BattleState = preload("res://scripts/systems/battle/core/battle_state.gd")
const BattleCellState = preload("res://scripts/systems/battle/core/battle_cell_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const BattleTimelineState = preload("res://scripts/systems/battle/core/battle_timeline_state.gd")
const ProgressionContentRegistry = preload("res://scripts/player/progression/progression_content_registry.gd")

func _initialize() -> void:
	print("init")
	call_deferred("_run")

func _run() -> void:
	print("run")
	var registry = ProgressionContentRegistry.new()
	var facade = GameRuntimeFacade.new()
	facade._battle_selection = GameRuntimeBattleSelection.new()
	facade._battle_selection_state.reset_for_battle_end()
	facade._battle_selection.setup(facade)
	facade._battle_state = BattleState.new()
	facade._battle_state.phase = &"unit_acting"
	facade._battle_state.map_size = Vector2i(3, 1)
	facade._battle_state.timeline = BattleTimelineState.new()
	for x in range(3):
		var cell = BattleCellState.new()
		cell.coord = Vector2i(x,0)
		cell.base_terrain = &"land"
		cell.base_height = 4
		cell.recalculate_runtime_values()
		facade._battle_state.cells[cell.coord] = cell
	facade._battle_runtime.setup(null, registry.get_skill_defs(), {}, {})
	facade._battle_runtime._state = facade._battle_state
	facade._battle_selected_coord = Vector2i(0,0)
	var player = BattleUnitState.new()
	player.unit_id = &"player"
	player.display_name = "P"
	player.faction_id = &"player"
	player.current_ap = 2
	player.current_aura = 0
	player.known_active_skill_ids = [&"saint_blade_combo"]
	player.known_skill_level_map = {&"saint_blade_combo":1}
	player.attribute_snapshot.set_value(&"action_points", 2)
	player.attribute_snapshot.set_value(&"aura_max", 8)
	player.set_anchor_coord(Vector2i(0,0))
	var enemy = BattleUnitState.new()
	enemy.unit_id = &"enemy"
	enemy.display_name = "E"
	enemy.faction_id = &"hostile"
	enemy.current_ap = 1
	enemy.attribute_snapshot.set_value(&"action_points", 1)
	enemy.set_anchor_coord(Vector2i(1,0))
	facade._battle_state.units[player.unit_id] = player
	facade._battle_state.units[enemy.unit_id] = enemy
	facade._battle_state.ally_unit_ids = [player.unit_id]
	facade._battle_state.enemy_unit_ids = [enemy.unit_id]
	facade._battle_state.active_unit_id = player.unit_id
	facade._selected_battle_skill_id = &"saint_blade_combo"
	print("coords", facade.get_battle_overlay_target_coords())
	quit(0)
