# Development-only text command protocol over the headless runtime.
# Keep command coverage aligned with automation needs, not player UX.
class_name GameTextCommandRunner
extends RefCounted

const HEADLESS_GAME_TEST_SESSION_SCRIPT = preload("res://scripts/systems/game_runtime/headless/headless_game_test_session.gd")
const GAME_TEXT_COMMAND_RESULT_SCRIPT = preload("res://scripts/systems/game_runtime/headless/game_text_command_result.gd")

var _session = HEADLESS_GAME_TEST_SESSION_SCRIPT.new()


func initialize() -> void:
	await _session.initialize()


func get_session():
	return _session


func dispose(clear_persisted_game: bool = false) -> void:
	if _session != null:
		await _session.dispose(clear_persisted_game)
	_session = null


func execute_line(command_text: String):
	var result = GAME_TEXT_COMMAND_RESULT_SCRIPT.new()
	result.command_text = command_text.strip_edges()
	if result.command_text.is_empty() or result.command_text.begins_with("#"):
		result.skipped = true
		return result

	var tokens := _tokenize(result.command_text)
	if tokens.is_empty():
		result.skipped = true
		return result

	if tokens[0] == "expect":
		await _finalize_expect_result(result, tokens)
		return result

	var command_result: Dictionary = await _execute_command(tokens)
	await _session.settle_frames()
	result.ok = bool(command_result.get("ok", false))
	result.message = String(command_result.get("message", ""))
	result.snapshot = _session.build_snapshot()
	result.human_log = "%s %s" % ["OK" if result.ok else "ERR", result.command_text]
	result.snapshot_text = _session.build_text_snapshot()
	return result


func _finalize_expect_result(result, tokens: Array[String]) -> void:
	result.snapshot = _session.build_snapshot()
	var assertion_result: Dictionary = _execute_expect(tokens, result.snapshot)
	result.ok = bool(assertion_result.get("ok", false))
	result.message = String(assertion_result.get("message", ""))
	result.assertions.append(assertion_result)
	result.snapshot_text = _session.build_text_snapshot()


func _execute_command(tokens: Array[String]) -> Dictionary:
	match tokens[0]:
		"help":
			return {
				"ok": true,
				"message":
				"Commands: preset/save/game/world/submap/party/quest/settlement/shop/stagecoach/warehouse/battle/reward/promotion/close/snapshot/expect",
			}
		"preset":
			return await _execute_preset_command(tokens)
		"save":
			return await _execute_save_command(tokens)
		"game":
			return await _execute_game_command(tokens)
		"world":
			return await _execute_world_command(tokens)
		"submap":
			return await _execute_submap_command(tokens)
		"party":
			return await _execute_party_command(tokens)
		"quest":
			return await _execute_quest_command(tokens)
		"settlement":
			return await _execute_settlement_command(tokens)
		"shop":
			return await _execute_shop_command(tokens)
		"stagecoach":
			return await _execute_stagecoach_command(tokens)
		"warehouse":
			return await _execute_warehouse_command(tokens)
		"battle":
			return await _execute_battle_command(tokens)
		"reward":
			return await _execute_reward_command(tokens)
		"promotion":
			return await _execute_promotion_command(tokens)
		"close":
			return await _execute_close_command(tokens)
		"snapshot":
			return {
				"ok": true,
				"message": "Snapshot generated.",
			}
		_:
			return {
				"ok": false,
				"message": "未知命令域 %s。" % tokens[0],
			}


func _execute_preset_command(tokens: Array[String]) -> Dictionary:
	if tokens.size() < 2 or tokens[1] != "list":
		return {
			"ok": false,
			"message": "用法: preset list",
		}
	var presets: Array = _session.list_presets()
	return {
		"ok": true,
		"message": "Listed %d presets." % presets.size(),
	}


func _execute_save_command(tokens: Array[String]) -> Dictionary:
	if tokens.size() < 2 or tokens[1] != "list":
		return {
			"ok": false,
			"message": "用法: save list",
		}
	var save_slots: Array[Dictionary] = await _session.list_save_slots()
	return {
		"ok": true,
		"message": "Listed %d saves." % save_slots.size(),
	}


func _execute_game_command(tokens: Array[String]) -> Dictionary:
	if tokens.size() < 3:
		return {
			"ok": false,
			"message": "用法: game new <preset_id> | game load <save_id>",
		}
	match tokens[1]:
		"new":
			return await _session.create_new_game(StringName(tokens[2]))
		"load":
			return await _session.load_game(tokens[2])
		_:
			return {
				"ok": false,
				"message": "未知 game 子命令 %s。" % tokens[1],
			}


func _execute_world_command(tokens: Array[String]) -> Dictionary:
	var ensure_result: Dictionary = await _ensure_world_context()
	if not bool(ensure_result.get("ok", false)):
		return ensure_result
	var runtime = _session.get_runtime_facade()
	if runtime == null:
		return _missing_world_error()
	if tokens.size() < 2:
		return {
			"ok": false,
			"message": "用法: world move/select/open/inspect ...",
		}
	match tokens[1]:
		"move":
			if tokens.size() < 3:
				return {
					"ok": false,
					"message": "用法: world move <up|down|left|right> [count]",
				}
			var direction := _parse_direction(tokens[2])
			var count := 1
			if tokens.size() >= 4:
				var count_result := _parse_int_argument(tokens[3], "移动次数")
				if not bool(count_result.get("ok", false)):
					return count_result
				count = int(count_result.get("value", 1))
			return runtime.command_world_move(direction, count)
		"select":
			if tokens.size() < 4:
				return {
					"ok": false,
					"message": "用法: world select <x> <y>",
				}
			var coord_result := _parse_coord_argument(tokens[2], tokens[3], "世界坐标")
			if not bool(coord_result.get("ok", false)):
				return coord_result
			return runtime.command_world_select(coord_result.get("value", Vector2i.ZERO))
		"open":
			if tokens.size() >= 4:
				var coord_result := _parse_coord_argument(tokens[2], tokens[3], "聚落坐标")
				if not bool(coord_result.get("ok", false)):
					return coord_result
				return runtime.command_open_settlement(coord_result.get("value", Vector2i.ZERO))
			return runtime.command_open_settlement()
		"inspect":
			if tokens.size() < 4:
				return {
					"ok": false,
					"message": "用法: world inspect <x> <y>",
				}
			var coord_result := _parse_coord_argument(tokens[2], tokens[3], "世界坐标")
			if not bool(coord_result.get("ok", false)):
				return coord_result
			return runtime.command_world_inspect(coord_result.get("value", Vector2i.ZERO))
		"click":
			if tokens.size() < 4:
				return {
					"ok": false,
					"message": "用法: world click <x> <y>",
				}
			var coord_result := _parse_coord_argument(tokens[2], tokens[3], "世界坐标")
			if not bool(coord_result.get("ok", false)):
				return coord_result
			return runtime.select_world_cell(coord_result.get("value", Vector2i.ZERO))
		_:
			return {
				"ok": false,
				"message": "未知 world 子命令 %s。" % tokens[1],
			}


func _execute_submap_command(tokens: Array[String]) -> Dictionary:
	var ensure_result: Dictionary = await _ensure_world_context()
	if not bool(ensure_result.get("ok", false)):
		return ensure_result
	var runtime = _session.get_runtime_facade()
	if runtime == null:
		return _missing_world_error()
	if tokens.size() < 2:
		return {
			"ok": false,
			"message": "用法: submap confirm|cancel|return",
		}
	match tokens[1]:
		"confirm":
			return runtime.command_confirm_submap_entry()
		"cancel":
			return runtime.command_cancel_submap_entry()
		"return":
			return runtime.command_return_from_submap()
		_:
			return {
				"ok": false,
				"message": "未知 submap 子命令 %s。" % tokens[1],
			}


func _execute_party_command(tokens: Array[String]) -> Dictionary:
	var ensure_result: Dictionary = await _ensure_world_context()
	if not bool(ensure_result.get("ok", false)):
		return ensure_result
	var runtime = _session.get_runtime_facade()
	if runtime == null:
		return _missing_world_error()
	if tokens.size() < 2:
		return {
			"ok": false,
			"message": "用法: party open/select/leader/activate/reserve/equip/unequip/warehouse ...",
		}
	match tokens[1]:
		"open":
			return runtime.command_open_party()
		"select":
			if tokens.size() < 3:
				return {
					"ok": false,
					"message": "用法: party select <member_id>",
				}
			return runtime.command_select_party_member(StringName(tokens[2]))
		"leader":
			if tokens.size() < 3:
				return {
					"ok": false,
					"message": "用法: party leader <member_id>",
				}
			return runtime.command_set_party_leader(StringName(tokens[2]))
		"activate":
			if tokens.size() < 3:
				return {
					"ok": false,
					"message": "用法: party activate <member_id>",
				}
			return runtime.command_move_member_to_active(StringName(tokens[2]))
		"reserve":
			if tokens.size() < 3:
				return {
					"ok": false,
					"message": "用法: party reserve <member_id>",
				}
			return runtime.command_move_member_to_reserve(StringName(tokens[2]))
		"equip":
			if tokens.size() < 4:
				return {
					"ok": false,
					"message": "用法: party equip <member_id> <item_id> [slot_id]",
				}
			var slot_id := StringName(tokens[4]) if tokens.size() >= 5 else &""
			return runtime.command_party_equip_item(StringName(tokens[2]), StringName(tokens[3]), slot_id)
		"unequip":
			if tokens.size() < 4:
				return {
					"ok": false,
					"message": "用法: party unequip <member_id> <slot_id>",
				}
			return runtime.command_party_unequip_item(StringName(tokens[2]), StringName(tokens[3]))
		"warehouse":
			return runtime.command_open_party_warehouse()
		_:
			return {
				"ok": false,
				"message": "未知 party 子命令 %s。" % tokens[1],
			}


func _execute_quest_command(tokens: Array[String]) -> Dictionary:
	var ensure_result: Dictionary = await _ensure_world_context()
	if not bool(ensure_result.get("ok", false)):
		return ensure_result
	var runtime = _session.get_runtime_facade()
	if runtime == null:
		return _missing_world_error()
	if tokens.size() < 2:
		return {
			"ok": false,
			"message": "用法: quest accept|progress|complete <quest_id> ...",
		}
	match tokens[1]:
		"accept":
			if tokens.size() < 3:
				return {
					"ok": false,
					"message": "用法: quest accept <quest_id>",
				}
			return runtime.command_accept_quest(StringName(tokens[2]))
		"progress":
			if tokens.size() < 5:
				return {
					"ok": false,
					"message": "用法: quest progress <quest_id> <objective_id> <amount> [key=value ...]",
				}
			var amount_result := _parse_int_argument(tokens[4], "任务进度增量")
			if not bool(amount_result.get("ok", false)):
				return amount_result
			return runtime.command_progress_quest(
				StringName(tokens[2]),
				StringName(tokens[3]),
				int(amount_result.get("value", 0)),
				_parse_named_args(tokens, 5)
			)
		"complete":
			if tokens.size() < 3:
				return {
					"ok": false,
					"message": "用法: quest complete <quest_id>",
				}
			return runtime.command_complete_quest(StringName(tokens[2]))
		_:
			return {
				"ok": false,
				"message": "未知 quest 子命令 %s。" % tokens[1],
			}


func _execute_settlement_command(tokens: Array[String]) -> Dictionary:
	var ensure_result: Dictionary = await _ensure_world_context()
	if not bool(ensure_result.get("ok", false)):
		return ensure_result
	var runtime = _session.get_runtime_facade()
	if runtime == null:
		return _missing_world_error()
	if tokens.size() < 3 or tokens[1] != "action":
		return {
			"ok": false,
			"message": "用法: settlement action <action_id> [key=value ...]",
		}
	return runtime.command_execute_settlement_action(tokens[2], _parse_named_args(tokens, 3))


func _execute_shop_command(tokens: Array[String]) -> Dictionary:
	var ensure_result: Dictionary = await _ensure_world_context()
	if not bool(ensure_result.get("ok", false)):
		return ensure_result
	var runtime = _session.get_runtime_facade()
	if runtime == null:
		return _missing_world_error()
	if tokens.size() < 3:
		return {
			"ok": false,
			"message": "用法: shop buy|sell <item_id> [quantity]",
		}
	var quantity := 1
	if tokens.size() >= 4:
		var quantity_result := _parse_int_argument(tokens[3], "商品数量")
		if not bool(quantity_result.get("ok", false)):
			return quantity_result
		quantity = int(quantity_result.get("value", 1))
	match tokens[1]:
		"buy":
			return runtime.command_shop_buy(StringName(tokens[2]), quantity)
		"sell":
			return runtime.command_shop_sell(StringName(tokens[2]), quantity)
		_:
			return {
				"ok": false,
				"message": "未知 shop 子命令 %s。" % tokens[1],
			}


func _execute_stagecoach_command(tokens: Array[String]) -> Dictionary:
	var ensure_result: Dictionary = await _ensure_world_context()
	if not bool(ensure_result.get("ok", false)):
		return ensure_result
	var runtime = _session.get_runtime_facade()
	if runtime == null:
		return _missing_world_error()
	if tokens.size() < 3 or tokens[1] != "travel":
		return {
			"ok": false,
			"message": "用法: stagecoach travel <settlement_id>",
		}
	return runtime.command_stagecoach_travel(tokens[2])


func _execute_warehouse_command(tokens: Array[String]) -> Dictionary:
	var ensure_result: Dictionary = await _ensure_world_context()
	if not bool(ensure_result.get("ok", false)):
		return ensure_result
	var runtime = _session.get_runtime_facade()
	if runtime == null:
		return _missing_world_error()
	if tokens.size() < 3:
		return {
			"ok": false,
			"message": "用法: warehouse add <item_id> <quantity> | warehouse use <item_id> [member_id] | warehouse discard-one|discard-all <item_id> | warehouse capacity <value>",
		}
	match tokens[1]:
		"add":
			if tokens.size() < 4:
				return {
					"ok": false,
					"message": "用法: warehouse add <item_id> <quantity>",
				}
			var quantity_result := _parse_int_argument(tokens[3], "仓库数量")
			if not bool(quantity_result.get("ok", false)):
				return quantity_result
			return runtime.command_warehouse_add_item(StringName(tokens[2]), int(quantity_result.get("value", 0)))
		"use":
			var member_id := StringName(tokens[3]) if tokens.size() >= 4 else &""
			return runtime.command_warehouse_use_item(StringName(tokens[2]), member_id)
		"capacity":
			if tokens.size() < 3:
				return {
					"ok": false,
					"message": "用法: warehouse capacity <value>",
				}
			var capacity_result := _parse_int_argument(tokens[2], "仓库容量")
			if not bool(capacity_result.get("ok", false)):
				return capacity_result
			return await _session.set_party_storage_capacity(int(capacity_result.get("value", 0)))
		"discard-one":
			return runtime.command_warehouse_discard_one(StringName(tokens[2]))
		"discard-all":
			return runtime.command_warehouse_discard_all(StringName(tokens[2]))
		_:
			return {
				"ok": false,
				"message": "未知 warehouse 子命令 %s。" % tokens[1],
			}


func _execute_battle_command(tokens: Array[String]) -> Dictionary:
	var ensure_result: Dictionary = await _ensure_world_context()
	if not bool(ensure_result.get("ok", false)):
		return ensure_result
	var runtime = _session.get_runtime_facade()
	if runtime == null:
		return _missing_world_error()
	if tokens.size() < 2:
		return {
			"ok": false,
			"message": "用法: battle start/confirm/tick/skill/variant/move/equip/unequip/wait/inspect/finish ...",
		}
	match tokens[1]:
		"start":
			if tokens.size() < 3:
				return {
					"ok": false,
					"message": "用法: battle start <settlement|single>",
				}
			return await _session.start_battle_by_kind(StringName(tokens[2]))
		"confirm":
			return runtime.command_confirm_battle_start()
		"tick":
			if tokens.size() < 3:
				return {
					"ok": false,
					"message": "用法: battle tick <seconds>",
				}
			var seconds_result := _parse_float_argument(tokens[2], "战斗推进秒数")
			if not bool(seconds_result.get("ok", false)):
				return seconds_result
			return runtime.command_battle_tick(float(seconds_result.get("value", 0.0)))
		"skill":
			if tokens.size() < 3:
				return {
					"ok": false,
					"message": "用法: battle skill <slot>",
				}
			var slot_result := _parse_int_argument(tokens[2], "技能栏位")
			if not bool(slot_result.get("ok", false)):
				return slot_result
			return runtime.command_battle_select_skill(int(slot_result.get("value", 0)) - 1)
		"variant":
			if tokens.size() < 3:
				return {
					"ok": false,
					"message": "用法: battle variant <next|prev>",
				}
			return runtime.command_battle_cycle_variant(1 if tokens[2] == "next" else -1)
		"move":
			if tokens.size() == 3:
				return runtime.command_battle_move_direction(_parse_direction(tokens[2]))
			if tokens.size() >= 4:
				var coord_result := _parse_coord_argument(tokens[2], tokens[3], "战斗坐标")
				if not bool(coord_result.get("ok", false)):
					return coord_result
				return runtime.command_battle_move_to(coord_result.get("value", Vector2i.ZERO))
			return {
				"ok": false,
				"message": "用法: battle move <up|down|left|right> | battle move <x> <y>",
			}
		"equip":
			if tokens.size() < 4:
				return {
					"ok": false,
					"message": "用法: battle equip <slot_id> <item_id> [instance_id=<instance_id>]",
				}
			var equip_args := _parse_named_args(tokens, 4)
			return await _session.change_battle_equipment(
				&"equip",
				StringName(tokens[2]),
				StringName(tokens[3]),
				StringName(String(equip_args.get("instance_id", ""))),
				equip_args
			)
		"unequip":
			if tokens.size() < 3:
				return {
					"ok": false,
					"message": "用法: battle unequip <slot_id> [instance_id=<instance_id>]",
				}
			var unequip_args_start := 3
			var unequip_instance_id := ""
			if tokens.size() >= 4 and tokens[3].find("=") < 0:
				unequip_instance_id = tokens[3]
				unequip_args_start = 4
			var unequip_args := _parse_named_args(tokens, unequip_args_start)
			if unequip_instance_id.is_empty():
				unequip_instance_id = String(unequip_args.get("instance_id", ""))
			return await _session.change_battle_equipment(
				&"unequip",
				StringName(tokens[2]),
				&"",
				StringName(unequip_instance_id),
				unequip_args
			)
		"wait":
			return runtime.command_battle_wait_or_resolve()
		"inspect":
			if tokens.size() < 4:
				return {
					"ok": false,
					"message": "用法: battle inspect <x> <y>",
				}
			var coord_result := _parse_coord_argument(tokens[2], tokens[3], "战斗坐标")
			if not bool(coord_result.get("ok", false)):
				return coord_result
			return runtime.command_battle_inspect(coord_result.get("value", Vector2i.ZERO))
		"finish":
			if tokens.size() < 3:
				return {
					"ok": false,
					"message": "用法: battle finish <player|hostile>",
				}
			return await _session.finish_active_battle(StringName(tokens[2]))
		"clear":
			return runtime.command_battle_clear_skill()
		_:
			return {
				"ok": false,
				"message": "未知 battle 子命令 %s。" % tokens[1],
			}


func _execute_reward_command(tokens: Array[String]) -> Dictionary:
	var ensure_result: Dictionary = await _ensure_world_context()
	if not bool(ensure_result.get("ok", false)):
		return ensure_result
	var runtime = _session.get_runtime_facade()
	if runtime == null:
		return _missing_world_error()
	if tokens.size() < 2 or tokens[1] != "confirm":
		return {
			"ok": false,
			"message": "用法: reward confirm",
		}
	return runtime.command_confirm_pending_reward()


func _execute_promotion_command(tokens: Array[String]) -> Dictionary:
	var ensure_result: Dictionary = await _ensure_world_context()
	if not bool(ensure_result.get("ok", false)):
		return ensure_result
	var runtime = _session.get_runtime_facade()
	if runtime == null:
		return _missing_world_error()
	if tokens.size() < 3 or tokens[1] != "choose":
		return {
			"ok": false,
			"message": "用法: promotion choose <profession_id>",
		}
	return runtime.command_choose_promotion(StringName(tokens[2]))


func _execute_close_command(tokens: Array[String]) -> Dictionary:
	var ensure_result: Dictionary = await _ensure_world_context()
	if not bool(ensure_result.get("ok", false)):
		return ensure_result
	var runtime = _session.get_runtime_facade()
	if runtime == null:
		return _missing_world_error()
	return runtime.command_close_active_modal()


func _execute_expect(tokens: Array[String], snapshot: Dictionary) -> Dictionary:
	if tokens.size() < 3:
		return {
			"ok": false,
			"message": "用法: expect status/window/field/list ...",
			"summary": "invalid expect",
			"actual": "",
			"expected": "",
		}
	match tokens[1]:
		"status":
			if tokens.size() < 4 or tokens[2] != "contains":
				return _expect_error("expect status contains <text>", "", "")
			var status_text := String(snapshot.get("status", {}).get("text", ""))
			var expected_text := _join_tokens(tokens, 3)
			if status_text.contains(expected_text):
				return _expect_ok("status contains %s" % expected_text, status_text, expected_text)
			return _expect_error("status contains %s" % expected_text, status_text, expected_text)
		"window":
			if tokens.size() < 4 or tokens[2] != "==":
				return _expect_error("expect window == <id>", "", "")
			var actual_window := String(snapshot.get("modal", {}).get("id", ""))
			var expected_window := tokens[3]
			if actual_window == expected_window:
				return _expect_ok("window == %s" % expected_window, actual_window, expected_window)
			return _expect_error("window == %s" % expected_window, actual_window, expected_window)
		"field":
			if tokens.size() < 5 or tokens[3] != "==":
				return _expect_error("expect field <path> == <value>", "", "")
			var actual_field = _resolve_path(snapshot, tokens[2])
			if not actual_field.get("ok", false):
				return _expect_error(String(actual_field.get("message", "")), "", tokens[4])
			var expected_value = _parse_scalar(_join_tokens(tokens, 4))
			if _values_equal(actual_field.get("value"), expected_value):
				return _expect_ok(
					"field %s == %s" % [tokens[2], str(expected_value)],
					_stringify_value(actual_field.get("value")),
					_stringify_value(expected_value)
				)
			return _expect_error(
				"field %s == %s" % [tokens[2], str(expected_value)],
				_stringify_value(actual_field.get("value")),
				_stringify_value(expected_value)
			)
		"list":
			if tokens.size() < 5 or tokens[3] != "contains":
				return _expect_error("expect list <path> contains <value>", "", "")
			var actual_list = _resolve_path(snapshot, tokens[2])
			if not actual_list.get("ok", false):
				return _expect_error(String(actual_list.get("message", "")), "", tokens[4])
			var list_value = actual_list.get("value")
			if list_value is not Array:
				return _expect_error("path %s is not a list" % tokens[2], _stringify_value(list_value), tokens[4])
			var expected_item = _parse_scalar(_join_tokens(tokens, 4))
			for item in list_value:
				if _values_equal(item, expected_item):
					return _expect_ok(
						"list %s contains %s" % [tokens[2], String(expected_item)],
						_stringify_value(list_value),
						_stringify_value(expected_item)
					)
			return _expect_error(
				"list %s contains %s" % [tokens[2], String(expected_item)],
				_stringify_value(list_value),
				_stringify_value(expected_item)
			)
		"warehouse":
			if tokens.size() < 5 or tokens[3] != "==":
				return _expect_error("expect warehouse <item_id> == <quantity>", "", "")
			var item_id := String(tokens[2])
			var expected_quantity_result := _parse_int_argument(_join_tokens(tokens, 4), "期望仓库数量")
			if not bool(expected_quantity_result.get("ok", false)):
				return _expect_error(String(expected_quantity_result.get("message", "")), "", _join_tokens(tokens, 4))
			var expected_quantity := int(expected_quantity_result.get("value", 0))
			var actual_quantity := _get_warehouse_item_total(snapshot, item_id)
			if actual_quantity == expected_quantity:
				return _expect_ok(
					"warehouse %s == %d" % [item_id, expected_quantity],
					str(actual_quantity),
					str(expected_quantity)
				)
			return _expect_error(
				"warehouse %s == %d" % [item_id, expected_quantity],
				str(actual_quantity),
				str(expected_quantity)
			)
		_:
			return _expect_error("unknown expect target %s" % tokens[1], "", "")


func _ensure_world_context() -> Dictionary:
	var ensure_result: Dictionary = await _session.ensure_world_loaded()
	if bool(ensure_result.get("ok", false)):
		return ensure_result
	return ensure_result


func _missing_world_error() -> Dictionary:
	return {
		"ok": false,
		"message": "当前世界地图不可用。",
	}


func _tokenize(line: String) -> Array[String]:
	var tokens: Array[String] = []
	var current := ""
	var in_quotes := false
	var escaping := false
	for index in range(line.length()):
		var ch := line.substr(index, 1)
		if escaping:
			current += ch
			escaping = false
			continue
		if in_quotes and ch == "\\":
			escaping = true
			continue
		if ch == "\"":
			in_quotes = not in_quotes
			continue
		if not in_quotes and (ch == " " or ch == "\t"):
			if not current.is_empty():
				tokens.append(current)
				current = ""
			continue
		current += ch
	if not current.is_empty():
		tokens.append(current)
	return tokens


func _parse_direction(token: String) -> Vector2i:
	match token.to_lower():
		"up":
			return Vector2i.UP
		"down":
			return Vector2i.DOWN
		"left":
			return Vector2i.LEFT
		"right":
			return Vector2i.RIGHT
		_:
			return Vector2i.ZERO


func _parse_coord(x_token: String, y_token: String) -> Vector2i:
	return Vector2i(int(_parse_scalar(x_token)), int(_parse_scalar(y_token)))


func _parse_int_argument(token: String, label: String) -> Dictionary:
	var value = _parse_scalar(token)
	if value is int:
		return {
			"ok": true,
			"value": value,
		}
	return {
		"ok": false,
		"message": "%s 必须是整数，收到 %s。" % [label, token],
	}


func _parse_float_argument(token: String, label: String) -> Dictionary:
	var value = _parse_scalar(token)
	if value is int or value is float:
		return {
			"ok": true,
			"value": float(value),
		}
	return {
		"ok": false,
		"message": "%s 必须是数值，收到 %s。" % [label, token],
	}


func _parse_coord_argument(x_token: String, y_token: String, label: String) -> Dictionary:
	var x_result := _parse_int_argument(x_token, "%s X" % label)
	if not bool(x_result.get("ok", false)):
		return x_result
	var y_result := _parse_int_argument(y_token, "%s Y" % label)
	if not bool(y_result.get("ok", false)):
		return y_result
	return {
		"ok": true,
		"value": Vector2i(int(x_result.get("value", 0)), int(y_result.get("value", 0))),
	}


func _parse_named_args(tokens: Array[String], start_index: int) -> Dictionary:
	var result: Dictionary = {}
	for index in range(start_index, tokens.size()):
		var token := tokens[index]
		var equals_index := token.find("=")
		if equals_index <= 0:
			continue
		var key := token.substr(0, equals_index)
		var value_text := token.substr(equals_index + 1)
		result[key] = _parse_scalar(value_text)
	return result


func _parse_scalar(token: String):
	var normalized := token.strip_edges()
	if normalized == "true":
		return true
	if normalized == "false":
		return false
	if normalized.is_valid_int():
		return int(normalized)
	if normalized.is_valid_float():
		return float(normalized)
	return normalized


func _resolve_path(root, path: String) -> Dictionary:
	var current = root
	for segment in path.split("."):
		if current is Dictionary:
			if not current.has(segment):
				return {
					"ok": false,
					"message": "path %s is missing at %s" % [path, segment],
				}
			current = current.get(segment)
			continue
		if current is Array:
			if not segment.is_valid_int():
				return {
					"ok": false,
					"message": "path %s expected numeric index at %s" % [path, segment],
				}
			var array_index := int(segment)
			if array_index < 0 or array_index >= current.size():
				return {
					"ok": false,
					"message": "path %s index out of range at %s" % [path, segment],
				}
			current = current[array_index]
			continue
		return {
			"ok": false,
			"message": "path %s cannot descend into %s" % [path, segment],
		}
	return {
		"ok": true,
		"value": current,
	}


func _values_equal(actual, expected) -> bool:
	if actual is Dictionary or actual is Array or expected is Dictionary or expected is Array:
		return JSON.stringify(actual) == JSON.stringify(expected)
	return actual == expected


func _stringify_value(value) -> String:
	if value is Dictionary or value is Array:
		return JSON.stringify(value)
	return str(value)


func _join_tokens(tokens: Array[String], start_index: int) -> String:
	var parts: Array[String] = []
	for index in range(start_index, tokens.size()):
		parts.append(tokens[index])
	return " ".join(PackedStringArray(parts))


func _get_warehouse_item_total(snapshot: Dictionary, item_id: String) -> int:
	var entries_variant = snapshot.get("warehouse", {}).get("window_data", {}).get("entries", [])
	if entries_variant is not Array:
		return 0
	for entry_variant in entries_variant:
		if entry_variant is not Dictionary:
			continue
		var entry: Dictionary = entry_variant
		if String(entry.get("item_id", "")) != item_id:
			continue
		return int(entry.get("total_quantity", 0))
	return 0


func _expect_ok(summary: String, actual: String, expected: String) -> Dictionary:
	return {
		"ok": true,
		"message": "Expectation passed.",
		"summary": summary,
		"actual": actual,
		"expected": expected,
	}


func _expect_error(summary: String, actual: String, expected: String) -> Dictionary:
	return {
		"ok": false,
		"message": "Expectation failed.",
		"summary": summary,
		"actual": actual,
		"expected": expected,
	}
