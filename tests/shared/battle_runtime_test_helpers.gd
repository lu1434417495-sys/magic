extends RefCounted

const StubDamageResolvers = preload("res://tests/shared/stub_damage_resolvers.gd")
const StubHitResolvers = preload("res://tests/shared/stub_hit_resolvers.gd")
const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")
const ATTRIBUTE_SNAPSHOT_SCRIPT = preload("res://scripts/player/progression/attribute_snapshot.gd")
const UNIT_BASE_ATTRIBUTES_SCRIPT = preload("res://scripts/player/progression/unit_base_attributes.gd")


## 给 fixture 单位补齐 6 维基础属性（默认 10，modifier=0），并按产线公式派生
## AC = BASE_ARMOR_CLASS + agility_mod，与 BattleUnitFactory._resolve_snapshot_armor_class 对齐。
## 直接写 attribute_snapshot 不走 AttributeService 派生管线的 fixture 必须显式调用本方法，
## 否则 BattleHitResolver 在 has_value(ARMOR_CLASS)=false 时 push_error 拒绝构造命中检定。
## 已经显式设过的属性会保留原值——caller 可在调用前先 set_value 覆盖默认 10。
static func seed_base_attributes_and_derive_ac(unit) -> void:
	if unit == null:
		return
	seed_attribute_snapshot_base_attributes_and_ac(unit.attribute_snapshot)


## seed_base_attributes_and_derive_ac 的 snapshot 版本：直接对 AttributeSnapshot 操作，
## 用于那些只构造 snapshot、不通过 unit 包装的 fixture（如 misfortune service 测试）。
static func seed_attribute_snapshot_base_attributes_and_ac(snapshot) -> void:
	if snapshot == null:
		return
	for attribute_id in UNIT_BASE_ATTRIBUTES_SCRIPT.BASE_ATTRIBUTE_IDS:
		if not snapshot.has_value(attribute_id):
			snapshot.set_value(attribute_id, 10)
	if not snapshot.has_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS):
		var agility_modifier := ATTRIBUTE_SNAPSHOT_SCRIPT.calculate_score_modifier(
			int(snapshot.get_value(UNIT_BASE_ATTRIBUTES_SCRIPT.AGILITY))
		)
		snapshot.set_value(
			ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS,
			clampi(ATTRIBUTE_SERVICE_SCRIPT.BASE_ARMOR_CLASS + agility_modifier, 1, 99)
		)


## 把单个 unit 登记到 state.units + 对应 faction 列表。
## 纯注册，不调用 grid_service.place_unit——需要那一步的 caller 自己再调
## （多数 fixture 在拿到 runtime/grid_service 之后才能 place，分开更灵活）。
static func register_unit_in_state(state, unit, is_enemy: bool) -> void:
	if state == null or unit == null:
		return
	state.units[unit.unit_id] = unit
	if is_enemy:
		state.enemy_unit_ids.append(unit.unit_id)
	else:
		state.ally_unit_ids.append(unit.unit_id)


static func configure_fixed_combat(runtime) -> void:
	if runtime == null:
		return
	if runtime.has_method("configure_hit_resolver_for_tests"):
		runtime.configure_hit_resolver_for_tests(StubHitResolvers.FixedHitResolver.new())
	if runtime.has_method("configure_damage_resolver_for_tests"):
		var damage_resolver := StubDamageResolvers.FixedSuccessOneDamageResolver.new()
		if runtime.has_method("get_skill_defs") and damage_resolver.has_method("set_skill_defs"):
			var skill_defs = runtime.get_skill_defs()
			if skill_defs is Dictionary:
				damage_resolver.set_skill_defs(skill_defs)
		runtime.configure_damage_resolver_for_tests(damage_resolver)


static func configure_fixed_combat_for_facade(facade) -> void:
	if facade == null:
		return
	if facade is Object and facade.has_method("get_battle_runtime"):
		configure_fixed_combat(facade.get_battle_runtime())
		return
	if facade is Object:
		configure_fixed_combat(facade.get("_battle_runtime"))
