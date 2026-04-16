class_name SettlementShopService
extends RefCounted

const DEFAULT_FALLBACK_PRICE := 10

const SHOP_DEFS := {
	"service_basic_supply": {
		"shop_id": "village_basic_supply",
		"title": "临时补给",
		"refresh_interval_steps": 12,
		"guaranteed_items": [
			{"item_id": &"healing_herb", "min_qty": 2, "max_qty": 4, "price_multiplier": 1.0},
			{"item_id": &"travel_ration", "min_qty": 2, "max_qty": 4, "price_multiplier": 1.0},
		],
		"random_pool": [
			{"item_id": &"bandage_roll", "weight": 6, "min_qty": 1, "max_qty": 3, "price_multiplier": 1.0},
			{"item_id": &"torch_bundle", "weight": 5, "min_qty": 1, "max_qty": 3, "price_multiplier": 1.0},
			{"item_id": &"antidote_herb", "weight": 4, "min_qty": 1, "max_qty": 2, "price_multiplier": 1.1},
			{"item_id": &"iron_ore", "weight": 2, "min_qty": 1, "max_qty": 2, "price_multiplier": 1.0},
		],
		"max_random_items": 2,
	},
	"service_local_trade": {
		"shop_id": "town_local_trade",
		"title": "镇集交易",
		"refresh_interval_steps": 10,
		"guaranteed_items": [
			{"item_id": &"healing_herb", "min_qty": 3, "max_qty": 6, "price_multiplier": 0.95},
			{"item_id": &"bandage_roll", "min_qty": 2, "max_qty": 4, "price_multiplier": 1.0},
			{"item_id": &"travel_ration", "min_qty": 2, "max_qty": 5, "price_multiplier": 0.95},
			{"item_id": &"bronze_sword", "min_qty": 1, "max_qty": 1, "price_multiplier": 1.0},
			{"item_id": &"leather_jerkin", "min_qty": 1, "max_qty": 1, "price_multiplier": 1.0},
		],
		"random_pool": [
			{"item_id": &"torch_bundle", "weight": 4, "min_qty": 1, "max_qty": 3, "price_multiplier": 1.0},
			{"item_id": &"antidote_herb", "weight": 4, "min_qty": 1, "max_qty": 3, "price_multiplier": 1.0},
			{"item_id": &"iron_ore", "weight": 3, "min_qty": 2, "max_qty": 4, "price_multiplier": 1.0},
			{"item_id": &"scout_charm", "weight": 2, "min_qty": 1, "max_qty": 1, "price_multiplier": 1.1},
			{"item_id": &"iron_greatsword", "weight": 1, "min_qty": 1, "max_qty": 1, "price_multiplier": 1.15},
		],
		"max_random_items": 4,
	},
	"service_city_market": {
		"shop_id": "city_market",
		"title": "城市市场",
		"refresh_interval_steps": 8,
		"guaranteed_items": [
			{"item_id": &"bronze_sword", "min_qty": 1, "max_qty": 1, "price_multiplier": 0.95},
			{"item_id": &"leather_jerkin", "min_qty": 1, "max_qty": 1, "price_multiplier": 0.95},
			{"item_id": &"scout_charm", "min_qty": 1, "max_qty": 1, "price_multiplier": 1.0},
			{"item_id": &"iron_greatsword", "min_qty": 1, "max_qty": 1, "price_multiplier": 1.0},
			{"item_id": &"antidote_herb", "min_qty": 2, "max_qty": 4, "price_multiplier": 0.95},
		],
		"random_pool": [
			{"item_id": &"bandage_roll", "weight": 5, "min_qty": 2, "max_qty": 4, "price_multiplier": 0.95},
			{"item_id": &"travel_ration", "weight": 4, "min_qty": 2, "max_qty": 5, "price_multiplier": 0.9},
			{"item_id": &"torch_bundle", "weight": 3, "min_qty": 1, "max_qty": 3, "price_multiplier": 0.95},
			{"item_id": &"iron_ore", "weight": 2, "min_qty": 3, "max_qty": 6, "price_multiplier": 0.95},
		],
		"max_random_items": 4,
	},
	"service_military_supply": {
		"shop_id": "capital_military_supply",
		"title": "军需总署",
		"refresh_interval_steps": 6,
		"guaranteed_items": [
			{"item_id": &"iron_greatsword", "min_qty": 1, "max_qty": 1, "price_multiplier": 0.95},
			{"item_id": &"leather_jerkin", "min_qty": 1, "max_qty": 1, "price_multiplier": 0.9},
			{"item_id": &"bandage_roll", "min_qty": 3, "max_qty": 5, "price_multiplier": 0.9},
		],
		"random_pool": [
			{"item_id": &"bronze_sword", "weight": 2, "min_qty": 1, "max_qty": 1, "price_multiplier": 0.9},
			{"item_id": &"scout_charm", "weight": 3, "min_qty": 1, "max_qty": 1, "price_multiplier": 0.95},
			{"item_id": &"antidote_herb", "weight": 5, "min_qty": 2, "max_qty": 4, "price_multiplier": 0.9},
		],
		"max_random_items": 3,
	},
	"service_grand_auction": {
		"shop_id": "metropolis_grand_auction",
		"title": "大拍卖行",
		"refresh_interval_steps": 5,
		"guaranteed_items": [
			{"item_id": &"iron_greatsword", "min_qty": 1, "max_qty": 1, "price_multiplier": 1.1},
			{"item_id": &"scout_charm", "min_qty": 1, "max_qty": 1, "price_multiplier": 1.05},
		],
		"random_pool": [
			{"item_id": &"bronze_sword", "weight": 1, "min_qty": 1, "max_qty": 1, "price_multiplier": 1.0},
			{"item_id": &"leather_jerkin", "weight": 1, "min_qty": 1, "max_qty": 1, "price_multiplier": 1.0},
			{"item_id": &"antidote_herb", "weight": 3, "min_qty": 2, "max_qty": 4, "price_multiplier": 1.0},
			{"item_id": &"torch_bundle", "weight": 2, "min_qty": 2, "max_qty": 4, "price_multiplier": 1.0},
		],
		"max_random_items": 4,
	},
}

var _rng := RandomNumberGenerator.new()


func build_window_data(
	interaction_script_id: String,
	settlement_record: Dictionary,
	settlement_state: Dictionary,
	item_defs: Dictionary,
	warehouse_service,
	current_gold: int
) -> Dictionary:
	var shop_def: Dictionary = get_shop_def(interaction_script_id)
	if shop_def.is_empty():
		return {}
	var shop_state := _get_or_refresh_shop_state(
		shop_def,
		settlement_record,
		settlement_state,
		item_defs,
		int(settlement_state.get("world_step", 0))
	)
	var buy_entries: Array[Dictionary] = []
	for entry_variant in shop_state.get("current_inventory", []):
		if entry_variant is not Dictionary:
			continue
		var entry_data: Dictionary = entry_variant
		var item_id := ProgressionDataUtils.to_string_name(entry_data.get("item_id", ""))
		var item_def = item_defs.get(item_id)
		if item_def == null:
			continue
		var quantity := maxi(int(entry_data.get("quantity", 0)), 0)
		var unit_price := maxi(int(entry_data.get("unit_price", 0)), 1)
		var can_buy := quantity > 0 and current_gold >= unit_price
		buy_entries.append({
			"item_id": String(item_id),
			"display_name": String(item_def.display_name if not item_def.display_name.is_empty() else item_id),
			"description": String(item_def.description),
			"icon": String(item_def.icon),
			"quantity": quantity,
			"unit_price": unit_price,
			"stock_text": "售罄" if quantity <= 0 else "库存 %d" % quantity,
			"can_buy": can_buy,
			"disabled_reason": "" if can_buy else ("库存不足" if quantity <= 0 else "金币不足"),
		})
	var sell_entries: Array[Dictionary] = []
	if warehouse_service != null:
		for entry_data in warehouse_service.get_inventory_entries():
			if entry_data is not Dictionary:
				continue
			var inventory_entry: Dictionary = entry_data
			var item_id := ProgressionDataUtils.to_string_name(inventory_entry.get("item_id", ""))
			var item_def = item_defs.get(item_id)
			if item_def == null or not bool(item_def.sellable):
				continue
			var total_quantity := maxi(int(inventory_entry.get("total_quantity", inventory_entry.get("quantity", 0))), 0)
			if total_quantity <= 0:
				continue
			sell_entries.append({
				"item_id": String(item_id),
				"display_name": String(inventory_entry.get("display_name", item_id)),
				"description": String(inventory_entry.get("description", "")),
				"icon": String(inventory_entry.get("icon", "")),
				"quantity": total_quantity,
				"unit_price": _resolve_sell_price(item_def),
				"stock_text": "持有 %d" % total_quantity,
				"can_sell": true,
				"disabled_reason": "",
			})
	sell_entries.sort_custom(func(a: Dictionary, b: Dictionary) -> bool:
		return String(a.get("item_id", "")) < String(b.get("item_id", ""))
	)
	return {
		"title": "%s · %s" % [
			String(settlement_record.get("display_name", "据点")),
			String(shop_def.get("title", "交易")),
		],
		"shop_id": String(shop_def.get("shop_id", "")),
		"interaction_script_id": interaction_script_id,
		"settlement_id": String(settlement_record.get("settlement_id", "")),
		"gold": maxi(current_gold, 0),
		"buy_entries": buy_entries,
		"sell_entries": sell_entries,
		"feedback_text": String(settlement_state.get("shop_feedback_text", "")),
	}


func buy(
	interaction_script_id: String,
	settlement_record: Dictionary,
	settlement_state: Dictionary,
	item_defs: Dictionary,
	warehouse_service,
	party_state,
	item_id: StringName,
	quantity: int
) -> Dictionary:
	var shop_def: Dictionary = get_shop_def(interaction_script_id)
	if shop_def.is_empty():
		return {
			"success": false,
			"message": "当前据点没有可交易的商店。",
		}
	if warehouse_service == null or party_state == null:
		return {
			"success": false,
			"message": "商店服务尚未准备完成。",
		}
	var requested_quantity := maxi(quantity, 0)
	if requested_quantity <= 0:
		return {
			"success": false,
			"message": "购买数量必须大于 0。",
		}
	var shop_state := _get_or_refresh_shop_state(
		shop_def,
		settlement_record,
		settlement_state,
		item_defs,
		int(settlement_state.get("world_step", 0))
	)
	var normalized_item_id := ProgressionDataUtils.to_string_name(item_id)
	var stock_entry := _find_inventory_entry(shop_state, normalized_item_id)
	if stock_entry.is_empty():
		return {
			"success": false,
			"message": "当前商店没有该商品。",
		}
	var available_quantity := maxi(int(stock_entry.get("quantity", 0)), 0)
	if available_quantity <= 0:
		return {
			"success": false,
			"message": "该商品当前已售罄。",
		}
	var actual_quantity := mini(requested_quantity, available_quantity)
	var unit_price := maxi(int(stock_entry.get("unit_price", 0)), 1)
	var total_cost := unit_price * actual_quantity
	if party_state.has_method("can_afford") and not party_state.can_afford(total_cost):
		return {
			"success": false,
			"message": "金币不足，无法购买 %s。" % String(normalized_item_id),
		}
	var preview: Dictionary = warehouse_service.preview_add_item(normalized_item_id, actual_quantity)
	if int(preview.get("remaining_quantity", 0)) > 0:
		return {
			"success": false,
			"message": "共享仓库空间不足，无法购买该商品。",
		}
	var add_result: Dictionary = warehouse_service.add_item(normalized_item_id, actual_quantity)
	var added_quantity := int(add_result.get("added_quantity", 0))
	if added_quantity <= 0:
		return {
			"success": false,
			"message": "当前无法将商品放入共享仓库。",
		}
	if party_state.has_method("spend_gold"):
		party_state.spend_gold(unit_price * added_quantity)
	else:
		party_state.gold = maxi(int(party_state.gold) - unit_price * added_quantity, 0)
	_consume_shop_stock(shop_state, normalized_item_id, added_quantity)
	settlement_state["shop_feedback_text"] = "购入 %d 件 %s，花费 %d 金。" % [
		added_quantity,
		String(normalized_item_id),
		unit_price * added_quantity,
	]
	return {
		"success": true,
		"message": String(settlement_state.get("shop_feedback_text", "")),
		"gold_delta": -(unit_price * added_quantity),
		"item_id": String(normalized_item_id),
		"quantity": added_quantity,
	}


func sell(
	interaction_script_id: String,
	settlement_record: Dictionary,
	settlement_state: Dictionary,
	item_defs: Dictionary,
	warehouse_service,
	party_state,
	item_id: StringName,
	quantity: int
) -> Dictionary:
	var shop_def: Dictionary = get_shop_def(interaction_script_id)
	if shop_def.is_empty():
		return {
			"success": false,
			"message": "当前据点没有可交易的商店。",
		}
	if warehouse_service == null or party_state == null:
		return {
			"success": false,
			"message": "商店服务尚未准备完成。",
		}
	var requested_quantity := maxi(quantity, 0)
	if requested_quantity <= 0:
		return {
			"success": false,
			"message": "出售数量必须大于 0。",
		}
	var normalized_item_id := ProgressionDataUtils.to_string_name(item_id)
	var item_def = item_defs.get(normalized_item_id)
	if item_def == null:
		return {
			"success": false,
			"message": "未找到该物品的定义。",
		}
	if not item_def.sellable:
		return {
			"success": false,
			"message": "%s 当前不能出售。" % String(item_def.display_name if not item_def.display_name.is_empty() else normalized_item_id),
		}
	var owned_quantity: int = warehouse_service.count_item(normalized_item_id)
	if owned_quantity <= 0:
		return {
			"success": false,
			"message": "共享仓库中没有该物品。",
		}
	var actual_quantity := mini(requested_quantity, owned_quantity)
	var remove_result: Dictionary = warehouse_service.remove_item(normalized_item_id, actual_quantity)
	var removed_quantity := int(remove_result.get("removed_quantity", 0))
	if removed_quantity <= 0:
		return {
			"success": false,
			"message": "当前无法出售该物品。",
		}
	var total_gain := _resolve_sell_price(item_def) * removed_quantity
	if party_state.has_method("add_gold"):
		party_state.add_gold(total_gain)
	else:
		party_state.gold = maxi(int(party_state.gold) + total_gain, 0)
	settlement_state["shop_feedback_text"] = "售出 %d 件 %s，获得 %d 金。" % [
		removed_quantity,
		String(item_def.display_name if not item_def.display_name.is_empty() else normalized_item_id),
		total_gain,
	]
	return {
		"success": true,
		"message": String(settlement_state.get("shop_feedback_text", "")),
		"gold_delta": total_gain,
		"item_id": String(normalized_item_id),
		"quantity": removed_quantity,
	}


func get_shop_def(interaction_script_id: String) -> Dictionary:
	return SHOP_DEFS.get(interaction_script_id, {}).duplicate(true)


func _get_or_refresh_shop_state(
	shop_def: Dictionary,
	settlement_record: Dictionary,
	settlement_state: Dictionary,
	item_defs: Dictionary,
	current_world_step: int
) -> Dictionary:
	var shop_states_variant = settlement_state.get("shop_states", {})
	var shop_states: Dictionary = shop_states_variant if shop_states_variant is Dictionary else {}
	var shop_id := String(shop_def.get("shop_id", ""))
	var stored_state_variant = shop_states.get(shop_id, {})
	var shop_state: Dictionary = stored_state_variant.duplicate(true) if stored_state_variant is Dictionary else {}
	var refresh_interval := maxi(int(shop_def.get("refresh_interval_steps", 0)), 0)
	var needs_refresh := shop_state.is_empty() or (refresh_interval > 0 and current_world_step - int(shop_state.get("last_refresh_step", -refresh_interval)) >= refresh_interval)
	if needs_refresh:
		shop_state = _generate_shop_state(shop_def, settlement_record, item_defs, current_world_step)
		shop_states[shop_id] = shop_state.duplicate(true)
		settlement_state["shop_states"] = shop_states
	settlement_state["shop_last_refresh_step"] = int(shop_state.get("last_refresh_step", 0))
	return shop_state


func _generate_shop_state(
	shop_def: Dictionary,
	settlement_record: Dictionary,
	item_defs: Dictionary,
	current_world_step: int
) -> Dictionary:
	var shop_id := String(shop_def.get("shop_id", ""))
	var settlement_id := String(settlement_record.get("settlement_id", ""))
	var seed := absi(hash("%s:%s:%d" % [settlement_id, shop_id, current_world_step]))
	_rng.seed = seed
	var inventory: Array[Dictionary] = []
	for entry_variant in shop_def.get("guaranteed_items", []):
		var built_entry := _build_shop_entry(entry_variant, item_defs)
		if not built_entry.is_empty():
			inventory.append(built_entry)
	var random_pool: Array = shop_def.get("random_pool", []).duplicate(true)
	var max_random_items := maxi(int(shop_def.get("max_random_items", 0)), 0)
	for _index in range(max_random_items):
		var picked_entry := _pick_weighted_random_entry(random_pool)
		if picked_entry.is_empty():
			break
		var built_entry := _build_shop_entry(picked_entry, item_defs)
		if not built_entry.is_empty():
			_merge_shop_entry(inventory, built_entry)
	return {
		"shop_id": shop_id,
		"current_inventory": inventory,
		"seed": seed,
		"last_refresh_step": current_world_step,
	}


func _build_shop_entry(entry_variant: Variant, item_defs: Dictionary) -> Dictionary:
	if entry_variant is not Dictionary:
		return {}
	var source: Dictionary = entry_variant
	var item_id := ProgressionDataUtils.to_string_name(source.get("item_id", ""))
	var item_def = item_defs.get(item_id)
	if item_id == &"" or item_def == null:
		return {}
	var min_qty := maxi(int(source.get("min_qty", 1)), 1)
	var max_qty := maxi(int(source.get("max_qty", min_qty)), min_qty)
	var quantity := _rng.randi_range(min_qty, max_qty)
	return {
		"item_id": String(item_id),
		"quantity": quantity,
		"unit_price": _resolve_buy_price(item_def, float(source.get("price_multiplier", 1.0))),
		"sold_out": false,
	}


func _merge_shop_entry(inventory: Array[Dictionary], built_entry: Dictionary) -> void:
	var item_id := String(built_entry.get("item_id", ""))
	for index in range(inventory.size()):
		var existing_entry: Dictionary = inventory[index]
		if String(existing_entry.get("item_id", "")) != item_id:
			continue
		existing_entry["quantity"] = int(existing_entry.get("quantity", 0)) + int(built_entry.get("quantity", 0))
		inventory[index] = existing_entry
		return
	inventory.append(built_entry)


func _pick_weighted_random_entry(pool: Array) -> Dictionary:
	var total_weight := 0.0
	for entry_variant in pool:
		if entry_variant is not Dictionary:
			continue
		total_weight += maxf(float((entry_variant as Dictionary).get("weight", 0.0)), 0.0)
	if total_weight <= 0.0:
		return {}
	var roll := _rng.randf_range(0.0, total_weight)
	var cursor := 0.0
	for entry_variant in pool:
		if entry_variant is not Dictionary:
			continue
		var entry: Dictionary = entry_variant
		cursor += maxf(float(entry.get("weight", 0.0)), 0.0)
		if roll <= cursor:
			return entry
	return {}


func _resolve_buy_price(item_def, price_multiplier: float) -> int:
	if item_def != null and item_def.has_method("get_buy_price"):
		var buy_price := int(item_def.get_buy_price(price_multiplier))
		if buy_price > 0:
			return buy_price
	return maxi(int(round(float(DEFAULT_FALLBACK_PRICE) * maxf(price_multiplier, 0.1))), 1)


func _resolve_sell_price(item_def) -> int:
	if item_def != null and item_def.has_method("get_sell_price"):
		var sell_price := int(item_def.get_sell_price())
		if sell_price > 0:
			return sell_price
	return maxi(int(floor(float(DEFAULT_FALLBACK_PRICE) * 0.5)), 1)


func _find_inventory_entry(shop_state: Dictionary, item_id: StringName) -> Dictionary:
	for entry_variant in shop_state.get("current_inventory", []):
		if entry_variant is not Dictionary:
			continue
		var entry_data: Dictionary = entry_variant
		if ProgressionDataUtils.to_string_name(entry_data.get("item_id", "")) == item_id:
			return entry_data
	return {}


func _consume_shop_stock(shop_state: Dictionary, item_id: StringName, quantity: int) -> void:
	var inventory: Array = shop_state.get("current_inventory", [])
	for index in range(inventory.size()):
		var entry_variant = inventory[index]
		if entry_variant is not Dictionary:
			continue
		var entry_data: Dictionary = entry_variant
		if ProgressionDataUtils.to_string_name(entry_data.get("item_id", "")) != item_id:
			continue
		var remaining_quantity := maxi(int(entry_data.get("quantity", 0)) - maxi(quantity, 0), 0)
		entry_data["quantity"] = remaining_quantity
		entry_data["sold_out"] = remaining_quantity <= 0
		inventory[index] = entry_data
		break
	shop_state["current_inventory"] = inventory
