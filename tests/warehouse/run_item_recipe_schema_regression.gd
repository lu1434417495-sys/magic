extends SceneTree

const GameSessionScript = preload("res://scripts/systems/game_session.gd")
const ItemDef = preload("res://scripts/player/warehouse/item_def.gd")
const RecipeDef = preload("res://scripts/player/warehouse/recipe_def.gd")
const RecipeContentRegistry = preload("res://scripts/player/warehouse/recipe_content_registry.gd")
const ItemContentRegistry = preload("res://scripts/player/warehouse/item_content_registry.gd")
const SettlementShopService = preload("res://scripts/systems/settlement_shop_service.gd")

const LEGACY_BRONZE_SWORD_PATH := "res://data/configs/items/bronze_sword.tres"

var _failures: Array[String] = []


class MockWarehouseService:
	extends RefCounted

	var _entries: Array[Dictionary] = []

	func set_inventory_entries(entries: Array[Dictionary]) -> void:
		_entries = entries.duplicate(true)

	func get_inventory_entries() -> Array[Dictionary]:
		return _entries.duplicate(true)


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_item_schema_defaults_and_accessors()
	_test_shop_pricing_uses_item_accessors()
	_test_recipe_schema_defaults_and_fields()
	_test_recipe_registry_and_game_session_cache()

	if _failures.is_empty():
		print("Item/recipe schema regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Item/recipe schema regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_item_schema_defaults_and_accessors() -> void:
	var legacy_bronze_sword: ItemDef = load(LEGACY_BRONZE_SWORD_PATH) as ItemDef
	_assert_true(legacy_bronze_sword != null, "旧物品资源应能正常加载。")
	if legacy_bronze_sword == null:
		return
	_assert_eq(int(legacy_bronze_sword.buy_price), 0, "旧物品资源默认 buy_price 应为 0。")
	_assert_eq(int(legacy_bronze_sword.sell_price), 0, "旧物品资源默认 sell_price 应为 0。")
	_assert_eq(legacy_bronze_sword.get_buy_price(), 120, "旧物品资源的默认购买价应继续回退到 base_price。")
	_assert_eq(legacy_bronze_sword.get_sell_price(), 60, "旧物品资源的默认出售价应继续保持半价逻辑。")

	var item_def := ItemDef.new()
	_assert_true(item_def.tags.is_empty(), "新建 ItemDef 的 tags 应默认为空。")
	_assert_true(item_def.crafting_groups.is_empty(), "新建 ItemDef 的 crafting_groups 应默认为空。")
	_assert_true(item_def.quest_groups.is_empty(), "新建 ItemDef 的 quest_groups 应默认为空。")

	item_def.base_price = 120
	item_def.buy_price = 150
	item_def.sell_price = 80
	item_def.tags = [&"forgeable", &"weapon"]
	item_def.crafting_groups = [&"forge", &"weapon"]
	item_def.quest_groups = [&"quest_reward", &"weapon_drop"]

	_assert_eq(item_def.get_buy_price(), 150, "显式 buy_price 应优先于 base_price。")
	_assert_eq(item_def.get_buy_price(0.5), 75, "buy_price 应按商店倍率缩放。")
	_assert_eq(item_def.get_sell_price(), 80, "显式 sell_price 应优先于 base_price。")
	_assert_eq(item_def.get_sell_price(0.25), 40, "sell_price 应按商店倍率缩放并保持旧默认半价基线。")
	_assert_eq(item_def.get_tags(), [&"forgeable", &"weapon"], "get_tags() 应返回规范化标签列表。")
	_assert_eq(item_def.get_crafting_groups(), [&"forge", &"weapon"], "get_crafting_groups() 应返回规范化分组列表。")
	_assert_eq(item_def.get_quest_groups(), [&"quest_reward", &"weapon_drop"], "get_quest_groups() 应返回规范化任务分组列表。")

	var tags_copy := item_def.get_tags()
	tags_copy[0] = &"mutated"
	_assert_eq(item_def.tags[0], &"forgeable", "get_tags() 不应暴露底层数组引用。")


func _test_shop_pricing_uses_item_accessors() -> void:
	var legacy_bronze_sword: ItemDef = load(LEGACY_BRONZE_SWORD_PATH) as ItemDef
	_assert_true(legacy_bronze_sword != null, "商店定价回归需要加载青铜短剑资源。")
	if legacy_bronze_sword == null:
		return

	var warehouse_service := MockWarehouseService.new()
	warehouse_service.set_inventory_entries([
		{
			"item_id": "bronze_sword",
			"display_name": legacy_bronze_sword.display_name,
			"description": legacy_bronze_sword.description,
			"icon": legacy_bronze_sword.icon,
			"total_quantity": 2,
		}
	])

	var settlement_record := {
		"settlement_id": "schema_test_settlement",
		"display_name": "Schema Test",
	}

	var shop_service := SettlementShopService.new()
	var legacy_window_data := shop_service.build_window_data(
		"service_local_trade",
		settlement_record,
		{"world_step": 0},
		{&"bronze_sword": legacy_bronze_sword},
		warehouse_service,
		999
	)
	var legacy_buy_entry := _find_entry(legacy_window_data.get("buy_entries", []), "bronze_sword")
	var legacy_sell_entry := _find_entry(legacy_window_data.get("sell_entries", []), "bronze_sword")
	_assert_true(not legacy_buy_entry.is_empty(), "旧物品资源应出现在买入条目中。")
	_assert_true(not legacy_sell_entry.is_empty(), "旧物品资源应出现在卖出条目中。")
	_assert_eq(int(legacy_buy_entry.get("unit_price", -1)), 120, "商店买价应继续使用 ItemDef 默认购买价。")
	_assert_eq(int(legacy_sell_entry.get("unit_price", -1)), 60, "商店卖价应继续使用 ItemDef 默认出售价。")

	var custom_bronze_sword := legacy_bronze_sword.duplicate(true) as ItemDef
	_assert_true(custom_bronze_sword != null, "ItemDef 复制后应仍然是 ItemDef。")
	if custom_bronze_sword == null:
		return
	custom_bronze_sword.buy_price = 150
	custom_bronze_sword.sell_price = 80

	var custom_window_data := shop_service.build_window_data(
		"service_local_trade",
		settlement_record,
		{"world_step": 0},
		{&"bronze_sword": custom_bronze_sword},
		warehouse_service,
		999
	)
	var custom_buy_entry := _find_entry(custom_window_data.get("buy_entries", []), "bronze_sword")
	var custom_sell_entry := _find_entry(custom_window_data.get("sell_entries", []), "bronze_sword")
	_assert_true(not custom_buy_entry.is_empty(), "显式买价物品应出现在买入条目中。")
	_assert_true(not custom_sell_entry.is_empty(), "显式卖价物品应出现在卖出条目中。")
	_assert_eq(int(custom_buy_entry.get("unit_price", -1)), 150, "商店买价应读取 ItemDef.buy_price。")
	_assert_eq(int(custom_sell_entry.get("unit_price", -1)), 80, "商店卖价应读取 ItemDef.sell_price。")


func _test_recipe_schema_defaults_and_fields() -> void:
	var recipe_def := RecipeDef.new()
	_assert_true(recipe_def.recipe_id == &"", "新建 RecipeDef 的 recipe_id 应默认为空。")
	_assert_true(recipe_def.input_item_ids.is_empty(), "新建 RecipeDef 的 input_item_ids 应默认为空。")
	_assert_true(recipe_def.input_item_quantities.is_empty(), "新建 RecipeDef 的 input_item_quantities 应默认为空。")
	_assert_true(recipe_def.required_facility_tags.is_empty(), "新建 RecipeDef 的 required_facility_tags 应默认为空。")
	_assert_true(recipe_def.failure_reason.is_empty(), "新建 RecipeDef 的 failure_reason 应默认为空。")
	_assert_eq(int(recipe_def.output_quantity), 1, "新建 RecipeDef 的 output_quantity 应默认为 1。")

	recipe_def.recipe_id = &"repair_bronze_sword"
	recipe_def.display_name = "修复青铜短剑"
	recipe_def.description = "消耗材料修复一件武器。"
	recipe_def.input_item_ids = [&"bronze_sword", &"iron_ore"]
	recipe_def.input_item_quantities = PackedInt32Array([1, 2])
	recipe_def.output_item_id = &"bronze_sword"
	recipe_def.output_quantity = 1
	recipe_def.required_facility_tags = [&"forge"]
	recipe_def.failure_reason = "需要熔炉。"

	_assert_eq(recipe_def.recipe_id, &"repair_bronze_sword", "RecipeDef 应保留配方标识。")
	_assert_eq(recipe_def.display_name, "修复青铜短剑", "RecipeDef 应保留展示名称。")
	_assert_eq(recipe_def.input_item_ids, [&"bronze_sword", &"iron_ore"], "RecipeDef 应保留输入物品列表。")
	_assert_eq(int(recipe_def.input_item_quantities.size()), 2, "RecipeDef 应保留输入物品数量列表。")
	_assert_eq(int(recipe_def.input_item_quantities[0]), 1, "RecipeDef 第一个输入数量应正确。")
	_assert_eq(int(recipe_def.input_item_quantities[1]), 2, "RecipeDef 第二个输入数量应正确。")
	_assert_eq(recipe_def.output_item_id, &"bronze_sword", "RecipeDef 应保留输出物品标识。")
	_assert_eq(int(recipe_def.output_quantity), 1, "RecipeDef 应保留输出数量。")
	_assert_eq(recipe_def.required_facility_tags, [&"forge"], "RecipeDef 应保留设施标签。")
	_assert_eq(recipe_def.failure_reason, "需要熔炉。", "RecipeDef 应保留失败原因文本。")


func _test_recipe_registry_and_game_session_cache() -> void:
	var item_registry := ItemContentRegistry.new()
	var recipe_registry := RecipeContentRegistry.new(item_registry.get_item_defs())
	var recipe_defs := recipe_registry.get_recipe_defs()
	var recipe_def = recipe_defs.get(&"master_reforge_iron_greatsword") as RecipeDef
	_assert_true(recipe_def != null, "RecipeContentRegistry 应扫描到大师重铸配方。")
	if recipe_def != null:
		_assert_eq(recipe_def.output_item_id, &"iron_greatsword", "重铸配方应指向铁制大剑产出。")
		_assert_eq(recipe_def.required_facility_tags, [&"master_reforge"], "重铸配方应保留大师工坊标签。")
	_assert_true(recipe_registry.validate().is_empty(), "RecipeContentRegistry 当前不应报告校验错误。")

	var game_session = GameSessionScript.new()
	var session_recipe_defs := game_session.get_recipe_defs()
	_assert_true(session_recipe_defs.has(&"master_reforge_iron_greatsword"), "GameSession 应缓存 recipe_defs。")
	var session_recipe = session_recipe_defs.get(&"master_reforge_iron_greatsword") as RecipeDef
	_assert_true(session_recipe != null, "GameSession 缓存中的配方应仍为 RecipeDef。")
	if session_recipe != null:
		_assert_eq(session_recipe.output_item_id, &"iron_greatsword", "GameSession 缓存应保留重铸产出。")
	game_session.free()


func _find_entry(entries: Array, item_id: String) -> Dictionary:
	for entry_variant in entries:
		if entry_variant is not Dictionary:
			continue
		var entry: Dictionary = entry_variant
		if String(entry.get("item_id", "")) == item_id:
			return entry
	return {}


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
