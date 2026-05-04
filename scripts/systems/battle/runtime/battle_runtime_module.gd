## 文件说明：该脚本属于战斗运行时模块相关的模块脚本，集中维护角色网关、技能定义集合、敌方模板集合等顶层字段。
## 审查重点：重点核对字段默认值、状态流转顺序、跨系统引用关系以及运行时读写时机是否仍然可靠。
## 备注：后续如果增删字段，需要同步检查调用方、状态同步链路以及历史数据兼容处理。

class_name BattleRuntimeModule
extends "res://scripts/systems/battle/runtime/battle_unit_factory_runtime.gd"

const BATTLE_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_state.gd")
const BATTLE_TIMELINE_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_timeline_state.gd")
const BATTLE_EVENT_BATCH_SCRIPT = preload("res://scripts/systems/battle/core/battle_event_batch.gd")
const BATTLE_PREVIEW_SCRIPT = preload("res://scripts/systems/battle/core/battle_preview.gd")
const BATTLE_CELL_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_cell_state.gd")
const BATTLE_TERRAIN_EFFECT_STATE_SCRIPT = preload("res://scripts/systems/battle/terrain/battle_terrain_effect_state.gd")
const BATTLE_GRID_SERVICE_SCRIPT = preload("res://scripts/systems/battle/terrain/battle_grid_service.gd")
const BATTLE_TERRAIN_GENERATOR_SCRIPT = preload("res://scripts/systems/battle/terrain/battle_terrain_generator.gd")
const BATTLE_DAMAGE_RESOLVER_SCRIPT = preload("res://scripts/systems/battle/rules/battle_damage_resolver.gd")
const BATTLE_DAMAGE_PREVIEW_RANGE_SERVICE_SCRIPT = preload("res://scripts/systems/battle/rules/battle_damage_preview_range_service.gd")
const BATTLE_HIT_RESOLVER_SCRIPT = preload("res://scripts/systems/battle/rules/battle_hit_resolver.gd")
const BATTLE_STATUS_SEMANTIC_TABLE_SCRIPT = preload("res://scripts/systems/battle/rules/battle_status_semantic_table.gd")
const BATTLE_AI_SERVICE_SCRIPT = preload("res://scripts/systems/battle/ai/battle_ai_service.gd")
const BATTLE_AI_DECISION_SCRIPT = preload("res://scripts/systems/battle/ai/battle_ai_decision.gd")
const BATTLE_AI_CONTEXT_SCRIPT = preload("res://scripts/systems/battle/ai/battle_ai_context.gd")
const BATTLE_TERRAIN_EFFECT_SYSTEM_SCRIPT = preload("res://scripts/systems/battle/terrain/battle_terrain_effect_system.gd")
const BATTLE_RATING_SYSTEM_SCRIPT = preload("res://scripts/systems/battle/runtime/battle_rating_system.gd")
const BATTLE_UNIT_FACTORY_SCRIPT = preload("res://scripts/systems/battle/runtime/battle_unit_factory.gd")
const BATTLE_CHARGE_RESOLVER_SCRIPT = preload("res://scripts/systems/battle/runtime/battle_charge_resolver.gd")
const BATTLE_REPEAT_ATTACK_RESOLVER_SCRIPT = preload("res://scripts/systems/battle/runtime/battle_repeat_attack_resolver.gd")
const BATTLE_REPORT_FORMATTER_SCRIPT = preload("res://scripts/systems/battle/rules/battle_report_formatter.gd")
const BATTLE_SKILL_RESOLUTION_RULES_SCRIPT = preload("res://scripts/systems/battle/rules/battle_skill_resolution_rules.gd")
const BATTLE_SKILL_MASTERY_SERVICE_SCRIPT = preload("res://scripts/systems/battle/runtime/battle_skill_mastery_service.gd")
const BATTLE_TERRAIN_TOPOLOGY_SERVICE_SCRIPT = preload("res://scripts/systems/battle/terrain/battle_terrain_topology_service.gd")
const BATTLE_TARGET_COLLECTION_SERVICE_SCRIPT = preload("res://scripts/systems/battle/runtime/battle_target_collection_service.gd")
const BATTLE_SPAWN_REACHABILITY_SERVICE_SCRIPT = preload("res://scripts/systems/battle/runtime/battle_spawn_reachability_service.gd")
const BATTLE_RANGE_SERVICE_SCRIPT = preload("res://scripts/systems/battle/rules/battle_range_service.gd")
const BATTLE_CHANGE_EQUIPMENT_RESOLVER_SCRIPT = preload("res://scripts/systems/battle/runtime/battle_change_equipment_resolver.gd")
const BATTLE_RUNTIME_LOOT_RESOLVER_SCRIPT = preload("res://scripts/systems/battle/runtime/battle_runtime_loot_resolver.gd")
const BATTLE_SKILL_TURN_RESOLVER_SCRIPT = preload("res://scripts/systems/battle/runtime/battle_skill_turn_resolver.gd")
const ENCOUNTER_ROSTER_BUILDER_SCRIPT = preload("res://scripts/systems/world/encounter_roster_builder.gd")
const BATTLE_RESOLUTION_RESULT_SCRIPT = preload("res://scripts/systems/battle/core/battle_resolution_result.gd")
const EQUIPMENT_DROP_SERVICE_SCRIPT = preload("res://scripts/systems/inventory/equipment_drop_service.gd")
const EQUIPMENT_RULES_SCRIPT = preload("res://scripts/player/equipment/equipment_rules.gd")
const FORTUNE_SERVICE_SCRIPT = preload("res://scripts/systems/battle/fate/fortune_service.gd")
const LOW_LUCK_RELIC_RULES_SCRIPT = preload("res://scripts/systems/fate/low_luck_relic_rules.gd")
const MISFORTUNE_SERVICE_SCRIPT = preload("res://scripts/systems/battle/fate/misfortune_service.gd")
const TRUE_RANDOM_SEED_SERVICE_SCRIPT = preload("res://scripts/utils/true_random_seed_service.gd")
const BattleState = preload("res://scripts/systems/battle/core/battle_state.gd")
const BattleTimelineState = preload("res://scripts/systems/battle/core/battle_timeline_state.gd")
const BattleEventBatch = preload("res://scripts/systems/battle/core/battle_event_batch.gd")
const BattlePreview = preload("res://scripts/systems/battle/core/battle_preview.gd")
const BattleCellState = preload("res://scripts/systems/battle/core/battle_cell_state.gd")
const BattleGridService = preload("res://scripts/systems/battle/terrain/battle_grid_service.gd")
const BattleDamageResolver = preload("res://scripts/systems/battle/rules/battle_damage_resolver.gd")
const BattleHitResolver = preload("res://scripts/systems/battle/rules/battle_hit_resolver.gd")
const BattleStatusSemanticTable = preload("res://scripts/systems/battle/rules/battle_status_semantic_table.gd")
const BattleAiService = preload("res://scripts/systems/battle/ai/battle_ai_service.gd")
const BattleAiDecision = preload("res://scripts/systems/battle/ai/battle_ai_decision.gd")
const BattleAiContext = preload("res://scripts/systems/battle/ai/battle_ai_context.gd")
const BattleCommand = preload("res://scripts/systems/battle/core/battle_command.gd")
const BattleReportFormatter = preload("res://scripts/systems/battle/rules/battle_report_formatter.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const BattleStatusEffectState = preload("res://scripts/systems/battle/core/battle_status_effect_state.gd")
const BattleTerrainRules = preload("res://scripts/systems/battle/terrain/battle_terrain_rules.gd")
const UNIT_BASE_ATTRIBUTES_SCRIPT = preload("res://scripts/player/progression/unit_base_attributes.gd")
const EquipmentInstanceState = preload("res://scripts/player/warehouse/equipment_instance_state.gd")
const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")
const CombatCastVariantDef = preload("res://scripts/player/progression/combat_cast_variant_def.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")
const REPEAT_ATTACK_EFFECT_TYPE: StringName = &"repeat_attack_until_fail"
const TERRAIN_EFFECT_STATUS: StringName = &"status"
const MIN_BATTLE_SURFACE_HEIGHT := 4
const STATUS_PINNED: StringName = &"pinned"
const STATUS_ROOTED: StringName = &"rooted"
const STATUS_TENDON_CUT: StringName = &"tendon_cut"
const STATUS_STAGGERED: StringName = &"staggered"
const STATUS_TAUNTED: StringName = &"taunted"
const STATUS_MARKED: StringName = &"marked"
const STATUS_ARCHER_RANGE_UP: StringName = &"archer_range_up"
const STATUS_ARCHER_QUICKSTEP: StringName = &"archer_quickstep"
const STATUS_GUARDING: StringName = &"guarding"
const STATUS_VAJRA_BODY: StringName = &"vajra_body"
const STATUS_BLACK_STAR_BRAND_NORMAL: StringName = &"black_star_brand_normal"
const STATUS_BLACK_STAR_BRAND_ELITE: StringName = &"black_star_brand_elite"
const STATUS_BLACK_STAR_BRAND_ELITE_GUARD_WINDOW: StringName = &"black_star_brand_elite_guard_window"
const STATUS_CROWN_BREAK_BROKEN_FANG: StringName = &"crown_break_broken_fang"
const STATUS_CROWN_BREAK_BROKEN_HAND: StringName = &"crown_break_broken_hand"
const STATUS_CROWN_BREAK_BLINDED_EYE: StringName = &"crown_break_blinded_eye"
const STATUS_DOOM_SENTENCE_VERDICT: StringName = &"doom_sentence_verdict"
const STATUS_BLACK_CROWN_SEAL_COUNTERATTACK: StringName = &"black_crown_seal_counterattack"
const STATUS_BLACK_CROWN_SEAL_CRIT: StringName = &"black_crown_seal_crit"
const MISSTEP_TO_SCHEME_SKILL_ID: StringName = &"misstep_to_scheme"
const BLACK_CONTRACT_PUSH_SKILL_ID: StringName = &"black_contract_push"
const DOOM_SHIFT_SKILL_ID: StringName = &"doom_shift"
const BLACK_CROWN_SEAL_SKILL_ID: StringName = &"black_crown_seal"
const BLACK_STAR_BRAND_SKILL_ID: StringName = &"black_star_brand"
const CROWN_BREAK_SKILL_ID: StringName = &"crown_break"
const DOOM_SENTENCE_SKILL_ID: StringName = &"doom_sentence"
const BLACK_CONTRACT_PUSH_VARIANT_BLOOD: StringName = &"blood_tithe"
const BLACK_CONTRACT_PUSH_VARIANT_GUARD: StringName = &"guard_tithe"
const BLACK_CONTRACT_PUSH_VARIANT_ACTION: StringName = &"action_tithe"
const BLACK_CROWN_SEAL_VARIANT_COUNTERATTACK: StringName = &"counterattack_lock"
const BLACK_CROWN_SEAL_VARIANT_CRIT: StringName = &"crit_lock"
const BLACK_STAR_BRAND_DURATION_TU := 60
const CROWN_BREAK_DURATION_TU := 60
const BLACK_CROWN_SEAL_DURATION_TU := 60
const DOOM_SHIFT_SELF_DEBUFF_DURATION_TU := 60
const BLACK_CONTRACT_PUSH_HP_COST := 10
const FORTUNE_MARK_TARGET_STAT_ID: StringName = &"fortune_mark_target"
const BOSS_TARGET_STAT_ID: StringName = &"boss_target"
const CALAMITY_SHARD_ITEM_ID: StringName = &"calamity_shard"
const BLACK_CROWN_CORE_ITEM_ID: StringName = &"black_crown_core"
const LOOT_DROP_TYPE_ITEM: StringName = &"item"
const LOOT_DROP_TYPE_RANDOM_EQUIPMENT: StringName = &"random_equipment"
const LOOT_DROP_TYPE_EQUIPMENT_INSTANCE: StringName = &"equipment_instance"
const LOOT_SOURCE_KIND_ENEMY_UNIT: StringName = &"enemy_unit"
const LOOT_SOURCE_KIND_CALAMITY_CONVERSION: StringName = &"calamity_conversion"
const LOOT_SOURCE_KIND_FATE_STATUS_DROP: StringName = &"fate_status_drop"
const LOOT_SOURCE_ID_ORDINARY_BATTLE: StringName = &"ordinary_battle"
const LOOT_SOURCE_ID_ELITE_BOSS_BATTLE: StringName = &"elite_boss_battle"
const CALAMITY_PER_SHARD := 2
const DOOM_SENTENCE_REFUND_CALAMITY := 5
const DEBUFF_STATUS_IDS := {
	&"armor_break": true,
	&"black_star_brand_elite": true,
	&"black_star_brand_normal": true,
	&"burning": true,
	&"crown_break_blinded_eye": true,
	&"crown_break_broken_fang": true,
	&"crown_break_broken_hand": true,
	&"frozen": true,
	&"hex_of_frailty": true,
	&"marked": true,
	&"pinned": true,
	&"rooted": true,
	&"shocked": true,
	&"slow": true,
	&"staggered": true,
	&"taunted": true,
	&"tendon_cut": true,
}
const REPEAT_ATTACK_STAGE_GUARD := 32
const TU_GRANULARITY := 5
const STAMINA_RECOVERY_PROGRESS_BASE := 5
const STAMINA_RECOVERY_PROGRESS_DENOMINATOR := 10
const STAMINA_RESTING_RECOVERY_MULTIPLIER := 2
const DEFAULT_TICK_INTERVAL_SECONDS := 1.0
const BATTLE_START_PLACEMENT_MAX_ATTEMPTS := 8
const BATTLE_START_TERRAIN_RETRY_SEED_STEP := 7919
const CHANGE_EQUIPMENT_AP_COST := BATTLE_CHANGE_EQUIPMENT_RESOLVER_SCRIPT.CHANGE_EQUIPMENT_AP_COST

## 字段说明：缓存角色网关实例，会参与运行时状态流转、系统协作和存档恢复。
var _character_gateway: Object = null
## 字段说明：缓存技能定义集合字典，集中保存可按键查询的运行时数据。
var _skill_defs: Dictionary = {}
## 字段说明：缓存物品定义集合字典，用于战斗内换装自动解析槽位联动。
var _item_defs: Dictionary = {}
## 字段说明：缓存敌方模板集合字典，集中保存可按键查询的运行时数据。
var _enemy_templates: Dictionary = {}
## 字段说明：缓存敌方 AI brain 集合字典，集中保存可按键查询的运行时数据。
var _enemy_ai_brains: Dictionary = {}
## 字段说明：记录遭遇构建器，会参与运行时状态流转、系统协作和存档恢复。
var _encounter_builder = ENCOUNTER_ROSTER_BUILDER_SCRIPT.new()
## 字段说明：缓存状态对象实例，会参与运行时状态流转、系统协作和存档恢复。
var _state: BattleState = null
## 字段说明：记录网格服务，会参与运行时状态流转、系统协作和存档恢复。
var _grid_service := BATTLE_GRID_SERVICE_SCRIPT.new()
## 字段说明：记录地形生成器，会参与运行时状态流转、系统协作和存档恢复。
var _terrain_generator: Object = BATTLE_TERRAIN_GENERATOR_SCRIPT.new()
## 字段说明：记录伤害解析器，会参与运行时状态流转、系统协作和存档恢复。
var _damage_resolver := BATTLE_DAMAGE_RESOLVER_SCRIPT.new()
## 字段说明：记录命中解析器，会参与运行时状态流转、系统协作和真随机掷骰。
var _hit_resolver: BattleHitResolver = BATTLE_HIT_RESOLVER_SCRIPT.new()
## 字段说明：记录自动决策服务，会参与运行时状态流转、系统协作和存档恢复。
var _ai_service: BattleAiService = BATTLE_AI_SERVICE_SCRIPT.new()
var _terrain_effect_system = BATTLE_TERRAIN_EFFECT_SYSTEM_SCRIPT.new()
var _battle_rating_system = BATTLE_RATING_SYSTEM_SCRIPT.new()
var _unit_factory = BATTLE_UNIT_FACTORY_SCRIPT.new()
var _charge_resolver = BATTLE_CHARGE_RESOLVER_SCRIPT.new()
var _repeat_attack_resolver = BATTLE_REPEAT_ATTACK_RESOLVER_SCRIPT.new()
var _report_formatter: BattleReportFormatter = BATTLE_REPORT_FORMATTER_SCRIPT.new()
var _skill_resolution_rules = BATTLE_SKILL_RESOLUTION_RULES_SCRIPT.new()
var _skill_mastery_service = BATTLE_SKILL_MASTERY_SERVICE_SCRIPT.new()
var _terrain_topology_service = BATTLE_TERRAIN_TOPOLOGY_SERVICE_SCRIPT.new()
var _target_collection_service = BATTLE_TARGET_COLLECTION_SERVICE_SCRIPT.new()
var _spawn_reachability_service = BATTLE_SPAWN_REACHABILITY_SERVICE_SCRIPT.new()
var _equipment_drop_service = EQUIPMENT_DROP_SERVICE_SCRIPT.new()
var _fortune_service = FORTUNE_SERVICE_SCRIPT.new()
var _misfortune_service = MISFORTUNE_SERVICE_SCRIPT.new()
var _change_equipment_resolver = BATTLE_CHANGE_EQUIPMENT_RESOLVER_SCRIPT.new()
var _loot_resolver = BATTLE_RUNTIME_LOOT_RESOLVER_SCRIPT.new()
var _skill_turn_resolver = BATTLE_SKILL_TURN_RESOLVER_SCRIPT.new()
## 字段说明：缓存战斗评分统计字典，集中保存可按键查询的运行时数据。
var _battle_rating_stats: Dictionary = {}
## 字段说明：保存待处理后置战斗角色奖励列表，便于顺序遍历、批量展示、批量运算和整体重建。
var _pending_post_battle_character_rewards: Array = []
## 字段说明：缓存当前战斗的正式掉落条目，供 canonical battle resolution result 直接消费。
var _active_loot_entries: Array = []
## 字段说明：记录已完成掉落结算的敌方单位，避免同一击杀在多条收尾分支中重复入表。
var _looted_defeated_unit_ids: Dictionary = {}
## 字段说明：缓存战斗结算结果，便于结算完成后由 session facade 统一消费。
var _battle_resolution_result = null
## 字段说明：记录战斗结算结果是否已经被消费，避免重复重建与重复提交。
var _battle_resolution_result_consumed := false
## 字段说明：记录地形效果序号，会参与运行时状态流转、系统协作和存档恢复。
var _terrain_effect_nonce := 0
## 字段说明：控制是否记录 AI 每回合候选动作与最终选择的结构化 trace。
var _ai_trace_enabled := false
## 字段说明：保存 AI 每回合的结构化 trace，供 simulation report 与数值分析复用。
var _ai_turn_traces: Array[Dictionary] = []
## 字段说明：缓存整场战斗的通用统计，覆盖全部单位与阵营，而不只服务于手动角色评分。
var _battle_metrics: Dictionary = {}
## 字段说明：记录 battle-local calamity 资源，按成员聚合并作为后续 Misfortune 技能燃料。
var calamity_by_member_id: Dictionary = {}


func setup(
	character_gateway: Object = null,
	skill_defs: Dictionary = {},
	enemy_templates: Dictionary = {},
	enemy_ai_brains: Dictionary = {},
	encounter_builder: Object = null,
	equipment_drop_service: Variant = null,
	item_defs: Dictionary = {},
	terrain_generator: Object = null
) -> void:
	_character_gateway = character_gateway
	_skill_defs = skill_defs if skill_defs != null else {}
	if _damage_resolver != null and _damage_resolver.has_method("set_skill_defs"):
		_damage_resolver.set_skill_defs(_skill_defs)
	_item_defs = item_defs if item_defs != null else {}
	if _item_defs.is_empty() and _character_gateway != null and _character_gateway.has_method("get_item_defs"):
		var gateway_item_defs = _character_gateway.call("get_item_defs")
		if gateway_item_defs is Dictionary:
			_item_defs = gateway_item_defs
	_enemy_templates = enemy_templates if enemy_templates != null else {}
	_enemy_ai_brains = enemy_ai_brains if enemy_ai_brains != null else {}
	_encounter_builder = encounter_builder if encounter_builder != null else ENCOUNTER_ROSTER_BUILDER_SCRIPT.new()
	_equipment_drop_service = equipment_drop_service if equipment_drop_service != null else EQUIPMENT_DROP_SERVICE_SCRIPT.new()
	if terrain_generator != null:
		_terrain_generator = terrain_generator
	_ai_service.setup(_enemy_ai_brains, _damage_resolver)
	_terrain_effect_system.setup(self)
	_battle_rating_system.setup(self, _skill_mastery_service)
	_unit_factory.setup(self)
	_charge_resolver.setup(self, _skill_mastery_service)
	_repeat_attack_resolver.setup(self, _skill_mastery_service)
	_skill_mastery_service.clear()
	_fortune_service.setup(_character_gateway, get_fate_event_bus())
	_misfortune_service.setup(get_fate_event_bus(), Callable(self, "_find_unit_by_member_id"))
	_change_equipment_resolver.setup(self)
	_loot_resolver.setup(self)
	_skill_turn_resolver.setup(self)


func start_battle(
	encounter_anchor,
	seed: int,
	context: Dictionary = {}
) -> BattleState:
	_ensure_sidecars_ready()
	var party_state = _character_gateway.get_party_state() if _character_gateway != null and _character_gateway.has_method("get_party_state") else null
	var ally_units: Array = _unit_factory.build_ally_units(party_state, context)
	if ally_units.is_empty():
		ally_units = _unit_factory.build_ally_units(null, context)

	var enemy_units: Array = []
	var enemy_build_context := context.duplicate(true)
	enemy_build_context["battle_seed"] = seed
	enemy_build_context["skill_defs"] = _skill_defs
	enemy_build_context["enemy_templates"] = _enemy_templates
	enemy_build_context["enemy_ai_brains"] = _enemy_ai_brains
	_active_loot_entries.clear()
	_looted_defeated_unit_ids.clear()
	calamity_by_member_id.clear()
	var has_explicit_enemy_units := enemy_build_context.has("enemy_units") \
		and enemy_build_context.get("enemy_units", []) is Array \
		and not (enemy_build_context.get("enemy_units", []) as Array).is_empty()
	var validate_spawn_reachability := bool(context.get("validate_spawn_reachability", not has_explicit_enemy_units))
	if _encounter_builder != null and not has_explicit_enemy_units:
		enemy_units = _encounter_builder.build_enemy_units(encounter_anchor, enemy_build_context)
	if enemy_units.is_empty():
		enemy_units = _unit_factory.build_enemy_units(encounter_anchor, enemy_build_context)
	for placement_attempt in range(BATTLE_START_PLACEMENT_MAX_ATTEMPTS):
		var terrain_seed := seed + placement_attempt * BATTLE_START_TERRAIN_RETRY_SEED_STEP
		var terrain_data := _unit_factory.build_terrain_data(encounter_anchor, terrain_seed, context)
		if terrain_data.is_empty():
			continue
		var terrain_profile_id := _resolve_formal_terrain_profile_id(terrain_data)
		if terrain_profile_id == &"":
			continue

		_state = BATTLE_STATE_SCRIPT.new()
		_state.battle_id = ProgressionDataUtils.to_string_name("%s_%d" % [String(encounter_anchor.entity_id), seed])
		_state.seed = seed
		_state.set_party_backpack_view(_get_party_backpack_state(party_state))
		_state.map_size = terrain_data.get("map_size", Vector2i.ZERO)
		_state.world_coord = context.get("world_coord", encounter_anchor.world_coord if encounter_anchor != null else Vector2i.ZERO)
		_state.encounter_anchor_id = ProgressionDataUtils.to_string_name(encounter_anchor.entity_id if encounter_anchor != null else "")
		_state.terrain_profile_id = terrain_profile_id
		_state.cells = terrain_data.get("cells", {})
		_state.cell_columns = terrain_data.get("cell_columns", BattleCellState.build_columns_from_surface_cells(_state.cells))
		_state.timeline = BATTLE_TIMELINE_STATE_SCRIPT.new()
		_state.timeline.units_per_second = _resolve_timeline_units_per_second(context)
		_state.timeline.tick_interval_seconds = _resolve_timeline_tick_interval_seconds(context)
		_state.timeline.tu_per_tick = _resolve_timeline_tu_per_tick(context)
		_state.timeline.delta_remainder = 0.0

		_place_units(ally_units, terrain_data.get("ally_spawns", []), true)
		_place_units(enemy_units, terrain_data.get("enemy_spawns", []), false)
		if validate_spawn_reachability:
			var spawn_reachability := _spawn_reachability_service.validate_state(_state, _grid_service, _skill_defs)
			if not bool(spawn_reachability.get("valid", false)):
				_state = null
				continue

		_initialize_unit_action_thresholds()
		_state.phase = &"timeline_running"
		_state.active_unit_id = &""
		_state.winner_faction_id = &""
		_state.modal_state = &""
		_state.attack_roll_nonce = 0
		_state.reset_log_entries(["战斗开始：%s" % encounter_anchor.display_name])
		_battle_rating_system.initialize_battle_rating_stats()
		_misfortune_service.begin_battle(calamity_by_member_id)
		_terrain_effect_nonce = 0
		_battle_resolution_result = null
		_battle_resolution_result_consumed = false
		_ai_turn_traces.clear()
		_initialize_battle_metrics()
		return _state

	_state = null
	return BATTLE_STATE_SCRIPT.new()


func _resolve_formal_terrain_profile_id(terrain_data: Dictionary) -> StringName:
	if not terrain_data.has("terrain_profile_id"):
		return &""
	var terrain_profile_variant = terrain_data["terrain_profile_id"]
	if terrain_profile_variant is not String and terrain_profile_variant is not StringName:
		return &""
	return ProgressionDataUtils.to_string_name(terrain_profile_variant)


func advance(delta_seconds: float) -> BattleEventBatch:
	_ensure_sidecars_ready()
	var batch := _new_batch()
	if _state == null or _state.phase == &"battle_ended":
		return batch
	if _state.modal_state != &"":
		return batch
	if _state.timeline != null and _state.timeline.frozen:
		return batch

	if _state.phase == &"unit_acting":
		var active_unit := _state.units.get(_state.active_unit_id) as BattleUnitState
		if active_unit != null and active_unit.is_alive and active_unit.control_mode != &"manual":
			var ai_context := BATTLE_AI_CONTEXT_SCRIPT.new()
			ai_context.state = _state
			ai_context.unit_state = active_unit
			ai_context.grid_service = _grid_service
			ai_context.skill_defs = _skill_defs
			ai_context.preview_callback = Callable(self, "preview_command")
			ai_context.skill_score_input_callback = Callable(_ai_service, "build_skill_score_input")
			ai_context.action_score_input_callback = Callable(_ai_service, "build_action_score_input")
			ai_context.trace_enabled = _ai_trace_enabled
			var decision: BattleAiDecision = _ai_service.choose_command(ai_context)
			if decision != null and decision.command != null:
				var ai_line := "AI[%s/%s/%s] %s" % [
					String(decision.brain_id),
					String(decision.state_id),
					String(decision.action_id),
					decision.reason_text,
				]
				_state.append_log_entry(ai_line)
				if _ai_trace_enabled:
					_ai_turn_traces.append(ai_context.build_turn_trace(decision))
				var decision_command: BattleCommand = decision.command
				var decision_batch := issue_command(decision_command)
				if decision_batch != null:
					decision_batch.log_lines.insert(0, ai_line)
				return decision_batch
		return batch

	if delta_seconds > 0.0:
		if _use_discrete_timeline_ticks():
			_state.timeline.delta_remainder += delta_seconds
			while _state.timeline.delta_remainder >= _state.timeline.tick_interval_seconds:
				_state.timeline.delta_remainder -= _state.timeline.tick_interval_seconds
				_apply_timeline_step(batch, _state.timeline.tick_interval_seconds, _state.timeline.tu_per_tick)
				if _check_battle_end(batch):
					return batch
		else:
			_apply_continuous_timeline_seconds(batch, delta_seconds)
			if _check_battle_end(batch):
				return batch

	if _state.phase == &"timeline_running":
		_activate_next_ready_unit(batch)

	return batch


func _use_discrete_timeline_ticks() -> bool:
	return _state != null \
		and _state.timeline != null \
		and _state.timeline.tick_interval_seconds > 0.0 \
		and _state.timeline.tu_per_tick > 0


func _apply_timeline_step(batch: BattleEventBatch, _delta_seconds: float, tu_delta: int) -> void:
	if _state == null or _state.timeline == null:
		return
	if tu_delta > 0 and tu_delta % TU_GRANULARITY != 0:
		push_error("Battle timeline can only advance in %d TU steps, got %d." % [TU_GRANULARITY, tu_delta])
		return
	if tu_delta > 0:
		_state.timeline.current_tu += tu_delta
	for unit_id in _get_units_in_order():
		var unit_state := _state.units.get(unit_id) as BattleUnitState
		if unit_state == null or not unit_state.is_alive:
			continue
		if tu_delta > 0 and _apply_stamina_recovery(unit_state, tu_delta):
			_append_changed_unit_id(batch, unit_state.unit_id)
		var status_tick_result := _apply_unit_status_periodic_ticks(unit_state, tu_delta, batch) if tu_delta > 0 else {}
		if bool(status_tick_result.get("changed", false)):
			_append_changed_unit_id(batch, unit_state.unit_id)
		if not unit_state.is_alive:
			var defeat_source_unit_id := ProgressionDataUtils.to_string_name(status_tick_result.get("defeat_source_unit_id", ""))
			var defeat_source_unit = _state.units.get(defeat_source_unit_id) as BattleUnitState if defeat_source_unit_id != &"" else null
			_collect_defeated_unit_loot(unit_state, defeat_source_unit)
			_clear_defeated_unit(unit_state, batch)
			batch.log_lines.append("%s 因持续效果倒下。" % unit_state.display_name)
			continue
		if tu_delta > 0 and _advance_unit_status_durations(unit_state, tu_delta):
			_append_changed_unit_id(batch, unit_state.unit_id)
		if not unit_state.is_alive:
			continue
		if tu_delta > 0:
			unit_state.action_progress += tu_delta
		var action_threshold := _resolve_unit_action_threshold(unit_state)
		while unit_state.action_progress >= action_threshold:
			unit_state.action_progress -= action_threshold
			if not _state.timeline.ready_unit_ids.has(unit_id):
				_state.timeline.ready_unit_ids.append(unit_id)
	_terrain_effect_system.process_timed_terrain_effects(batch)


func _apply_continuous_timeline_seconds(batch: BattleEventBatch, delta_seconds: float) -> void:
	if _state == null or _state.timeline == null or delta_seconds <= 0.0:
		return
	_state.timeline.delta_remainder += delta_seconds
	var units_per_second := maxi(int(_state.timeline.units_per_second), TU_GRANULARITY)
	if units_per_second % TU_GRANULARITY != 0:
		push_error("timeline.units_per_second must be a multiple of %d, got %d." % [TU_GRANULARITY, units_per_second])
		units_per_second = TU_GRANULARITY
	var pending_tu := int(floor((_state.timeline.delta_remainder * float(units_per_second)) / float(TU_GRANULARITY))) * TU_GRANULARITY
	if pending_tu <= 0:
		return
	var consumed_seconds := float(pending_tu) / float(units_per_second)
	_state.timeline.delta_remainder = maxf(_state.timeline.delta_remainder - consumed_seconds, 0.0)
	_apply_timeline_step(batch, consumed_seconds, pending_tu)


func _apply_stamina_recovery(unit_state: BattleUnitState, tu_delta: int) -> bool:
	if unit_state == null or tu_delta <= 0:
		return false
	var tick_count := int(tu_delta / TU_GRANULARITY)
	if tick_count <= 0:
		return false
	var stamina_max := _get_unit_stamina_max(unit_state)
	if stamina_max <= 0:
		if unit_state.current_stamina != 0 or unit_state.stamina_recovery_progress != 0:
			unit_state.current_stamina = 0
			unit_state.stamina_recovery_progress = 0
			return true
		return false

	var changed := false
	if unit_state.current_stamina >= stamina_max:
		if unit_state.current_stamina != stamina_max or unit_state.stamina_recovery_progress != 0:
			unit_state.current_stamina = stamina_max
			unit_state.stamina_recovery_progress = 0
			changed = true
		return changed

	var constitution := _get_unit_constitution(unit_state)
	var progress_gain_per_tick := STAMINA_RECOVERY_PROGRESS_BASE + constitution
	if unit_state.is_resting:
		progress_gain_per_tick *= STAMINA_RESTING_RECOVERY_MULTIPLIER

	for _tick_index in range(tick_count):
		unit_state.stamina_recovery_progress += progress_gain_per_tick
		var recovered := int(unit_state.stamina_recovery_progress / STAMINA_RECOVERY_PROGRESS_DENOMINATOR)
		if recovered <= 0:
			continue
		unit_state.current_stamina = mini(unit_state.current_stamina + recovered, stamina_max)
		unit_state.stamina_recovery_progress %= STAMINA_RECOVERY_PROGRESS_DENOMINATOR
		changed = true
		if unit_state.current_stamina >= stamina_max:
			unit_state.current_stamina = stamina_max
			unit_state.stamina_recovery_progress = 0
			break

	return changed


func _get_unit_constitution(unit_state: BattleUnitState) -> int:
	if unit_state == null or unit_state.attribute_snapshot == null:
		return 0
	return maxi(unit_state.attribute_snapshot.get_value(UNIT_BASE_ATTRIBUTES_SCRIPT.CONSTITUTION), 0)


func preview_command(command: BattleCommand) -> BattlePreview:
	_ensure_sidecars_ready()
	var preview := BATTLE_PREVIEW_SCRIPT.new()
	if _state == null or command == null or _state.phase == &"battle_ended":
		return preview
	if _state.modal_state != &"":
		preview.log_lines.append(_get_battle_interaction_block_message())
		return preview

	var active_unit := _state.units.get(command.unit_id) as BattleUnitState
	if active_unit == null or not active_unit.is_alive:
		return preview

	match command.command_type:
		BattleCommand.TYPE_MOVE:
			if _is_movement_blocked(active_unit):
				preview.log_lines.append("%s 当前被限制移动。" % active_unit.display_name)
				return preview
			var move_result := _resolve_move_path_result(active_unit, command.target_coord)
			if bool(move_result.get("allowed", false)):
				preview.allowed = true
				var move_cost := int(move_result.get("cost", 0))
				preview.move_cost = move_cost
				preview.resolved_anchor_coord = command.target_coord
				preview.log_lines.append("移动可执行，消耗 %d 点行动点。" % move_cost)
				for target_coord in _grid_service.get_unit_target_coords(active_unit, command.target_coord):
					preview.target_coords.append(target_coord)
			else:
				preview.log_lines.append(String(move_result.get("message", "该移动不可执行。")))
		BattleCommand.TYPE_SKILL:
			_preview_skill_command(active_unit, command, preview)
		BattleCommand.TYPE_WAIT:
			preview.allowed = true
			preview.log_lines.append("%s 可以结束行动。" % active_unit.display_name)
		BattleCommand.TYPE_CHANGE_EQUIPMENT:
			_preview_change_equipment_command(active_unit, command, preview)
		_:
			preview.log_lines.append("未知命令类型。")
	return preview


func issue_command(command: BattleCommand) -> BattleEventBatch:
	_ensure_sidecars_ready()
	var batch := _new_batch()
	if _state == null or command == null:
		return batch
	if _state.phase != &"unit_acting":
		return batch
	if _state.modal_state != &"":
		batch.log_lines.append(_get_battle_interaction_block_message())
		return batch

	var active_unit := _state.units.get(_state.active_unit_id) as BattleUnitState
	if active_unit == null or not active_unit.is_alive:
		return batch
	if active_unit.unit_id != command.unit_id:
		if command.command_type == BattleCommand.TYPE_CHANGE_EQUIPMENT:
			var validation := _build_change_equipment_result(
				false,
				"target_not_self",
				"只能为当前行动单位自己换装。",
				command
			)
			validation["target_unit_id"] = String(command.unit_id)
			_append_change_equipment_report(batch, active_unit, validation, false)
			_append_batch_logs_to_state(batch)
		return batch
	_ensure_unit_turn_anchor(active_unit)
	if command.command_type == BattleCommand.TYPE_SKILL and _should_block_skill_issue_from_preview(command, batch):
		_append_batch_logs_to_state(batch)
		return batch

	match command.command_type:
		BattleCommand.TYPE_MOVE:
			_handle_move_command(active_unit, command, batch)
		BattleCommand.TYPE_SKILL:
			_handle_skill_command(active_unit, command, batch)
		BattleCommand.TYPE_WAIT:
			_record_action_issued(active_unit, BattleCommand.TYPE_WAIT)
			batch.log_lines.append("%s 结束行动。" % active_unit.display_name)
		BattleCommand.TYPE_CHANGE_EQUIPMENT:
			_handle_change_equipment_command(active_unit, command, batch)
		_:
			return batch

	_append_batch_logs_to_state(batch)
	var flushed_log_count := batch.log_lines.size()
	var flushed_report_count := batch.report_entries.size()

	if _state.modal_state != &"":
		batch.modal_requested = true
		return batch

	if _check_battle_end(batch):
		_append_batch_logs_to_state_from(batch, flushed_log_count, flushed_report_count)
		return batch

	if active_unit.current_ap <= 0 or not active_unit.is_alive or command.command_type == BattleCommand.TYPE_WAIT:
		_end_active_turn(batch)
		_append_batch_logs_to_state_from(batch, flushed_log_count, flushed_report_count)

	return batch


func _get_battle_interaction_block_message() -> String:
	if _state == null:
		return "当前无法操作。"
	match StringName(_state.modal_state):
		&"start_confirm":
			return "战斗尚未开始，确认后才能操作。"
		&"promotion_choice":
			return "当前处于晋升选择中，无法操作。"
		_:
			return "当前有待处理的战斗流程，暂时无法操作。"


func _should_block_skill_issue_from_preview(command: BattleCommand, batch: BattleEventBatch) -> bool:
	var preview := preview_command(command)
	if preview != null and preview.allowed:
		return false
	if preview != null:
		for log_line in preview.log_lines:
			batch.log_lines.append(log_line)
	if batch.log_lines.is_empty():
		batch.log_lines.append("技能或目标无效。")
	return true


func _append_batch_logs_to_state(batch: BattleEventBatch) -> void:
	_append_batch_logs_to_state_from(batch)


func _append_batch_logs_to_state_from(
	batch: BattleEventBatch,
	log_start_index: int = 0,
	report_start_index: int = 0
) -> void:
	if _state == null or batch == null:
		return
	var safe_log_start := clampi(log_start_index, 0, batch.log_lines.size())
	for log_index in range(safe_log_start, batch.log_lines.size()):
		_state.append_log_entry(String(batch.log_lines[log_index]))
	var safe_report_start := clampi(report_start_index, 0, batch.report_entries.size())
	for report_index in range(safe_report_start, batch.report_entries.size()):
		var report_entry_variant = batch.report_entries[report_index]
		if report_entry_variant is not Dictionary:
			continue
		_state.report_entries.append((report_entry_variant as Dictionary).duplicate(true))


func _append_result_report_entry(batch: BattleEventBatch, result: Dictionary) -> void:
	if batch == null or result.is_empty():
		return
	var report_entry_variant = result.get("report_entry", {})
	if report_entry_variant is not Dictionary:
		return
	_append_report_entry_to_batch(batch, report_entry_variant as Dictionary)


func _append_report_entry_to_batch(batch: BattleEventBatch, report_entry: Dictionary) -> void:
	if batch == null or report_entry.is_empty():
		return
	batch.report_entries.append(report_entry.duplicate(true))
	var entry_text := String(report_entry.get("text", "")).strip_edges()
	if not entry_text.is_empty():
		batch.log_lines.append(entry_text)


func submit_promotion_choice(
	member_id: StringName,
	profession_id: StringName,
	selection: Dictionary
) -> BattleEventBatch:
	_ensure_sidecars_ready()
	var batch := _new_batch()
	if _state == null or _character_gateway == null:
		return batch
	var delta = _character_gateway.promote_profession(member_id, profession_id, selection)
	batch.progression_deltas.append(delta)
	var unit_state := _find_unit_by_member_id(member_id)
	if unit_state != null:
		_unit_factory.refresh_battle_unit(unit_state)
		batch.changed_unit_ids.append(unit_state.unit_id)
		batch.log_lines.append("%s 完成职业晋升。" % unit_state.display_name)
	_state.modal_state = &""
	_state.timeline.frozen = false
	return batch


func get_state() -> BattleState:
	return _state


func get_calamity_by_member_id() -> Dictionary:
	return ProgressionDataUtils.to_string_name_int_map(calamity_by_member_id).duplicate(true)


func get_member_calamity(member_id: StringName) -> int:
	return _misfortune_service.get_member_calamity(member_id) if _misfortune_service != null else 0


func get_member_calamity_cap(member_id: StringName) -> int:
	return _misfortune_service.get_member_calamity_cap(member_id) if _misfortune_service != null else 3


func get_black_star_brand_cast_cost(member_id: StringName) -> int:
	return _misfortune_service.get_black_star_brand_calamity_cost(member_id) if _misfortune_service != null else 1


func has_misfortune_reason(member_id: StringName, reason_id: StringName) -> bool:
	return _misfortune_service.has_triggered_reason(member_id, reason_id) if _misfortune_service != null else false


func get_skill_cast_block_reason(active_unit: BattleUnitState, skill_def: SkillDef) -> String:
	return _get_skill_cast_block_reason(active_unit, skill_def)


func is_unit_guard_locked(unit_state: BattleUnitState) -> bool:
	return _has_status(unit_state, STATUS_BLACK_STAR_BRAND_NORMAL)


func is_unit_counterattack_locked(unit_state: BattleUnitState) -> bool:
	return _has_status(unit_state, STATUS_BLACK_STAR_BRAND_NORMAL) \
		or _has_status(unit_state, STATUS_CROWN_BREAK_BROKEN_HAND) \
		or _has_status_param_bool(unit_state, &"lock_counterattack")


func is_unit_follow_up_locked(unit_state: BattleUnitState) -> bool:
	return _has_status(unit_state, STATUS_CROWN_BREAK_BROKEN_HAND)


func notify_member_boss_phase_changed(member_id: StringName, phase_id: StringName = &"") -> Dictionary:
	if _misfortune_service == null:
		return {}
	var unit_state := _find_unit_by_member_id(member_id)
	if unit_state == null:
		return {}
	return _misfortune_service.handle_boss_phase_changed(unit_state, phase_id)


func _ensure_sidecars_ready() -> void:
	_terrain_effect_system.setup(self)
	_battle_rating_system.setup(self, _skill_mastery_service)
	_unit_factory.setup(self)
	_charge_resolver.setup(self, _skill_mastery_service)
	_repeat_attack_resolver.setup(self, _skill_mastery_service)
	_change_equipment_resolver.setup(self)
	_loot_resolver.setup(self)
	_skill_turn_resolver.setup(self)


func _get_party_backpack_state(party_state):
	if party_state == null or not (party_state is Object):
		return null
	var backpack_state = party_state.get("warehouse_state")
	if backpack_state != null and backpack_state.has_method("duplicate_state"):
		return backpack_state
	return null


func is_battle_active() -> bool:
	return _state != null and _state.phase != &"battle_ended"


func get_unit_reachable_move_coords(unit_state: BattleUnitState) -> Array[Vector2i]:
	if _state == null or unit_state == null or not unit_state.is_alive:
		return []
	if _is_movement_blocked(unit_state):
		return []

	var origin := unit_state.coord
	var max_move_points := maxi(int(unit_state.current_move_points), 0)
	var origin_has_quickstep_bonus := _has_status(unit_state, STATUS_ARCHER_QUICKSTEP)
	var best_state_costs := {
		_build_reachable_move_state_key(origin, origin_has_quickstep_bonus): 0,
	}
	var best_coord_costs := {
		origin: 0,
	}
	var buckets := _build_reachable_move_buckets(max_move_points)
	buckets[0].append({
		"coord": origin,
		"spent_cost": 0,
		"has_quickstep_bonus": origin_has_quickstep_bonus,
	})
	for current_cost in range(max_move_points + 1):
		var bucket_index := 0
		while bucket_index < buckets[current_cost].size():
			var frontier_entry: Dictionary = buckets[current_cost][bucket_index]
			bucket_index += 1
			var current_coord: Vector2i = frontier_entry.get("coord", origin)
			var spent_cost := int(frontier_entry.get("spent_cost", current_cost))
			var has_quickstep_bonus := bool(frontier_entry.get("has_quickstep_bonus", false))
			var current_state_key := _build_reachable_move_state_key(current_coord, has_quickstep_bonus)
			if spent_cost != current_cost:
				continue
			if spent_cost != int(best_state_costs.get(current_state_key, 2147483647)):
				continue
			for neighbor_coord in _grid_service.get_neighbors_4(_state, current_coord):
				if not _grid_service.can_unit_step_between_anchors(_state, unit_state, current_coord, neighbor_coord):
					continue
				var move_cost := _get_move_cost_for_unit_target(unit_state, neighbor_coord, has_quickstep_bonus)
				var next_cost := spent_cost + move_cost
				if next_cost > max_move_points:
					continue
				var next_has_quickstep_bonus := false
				var next_state_key := _build_reachable_move_state_key(neighbor_coord, next_has_quickstep_bonus)
				var best_state_cost := int(best_state_costs.get(next_state_key, 2147483647))
				if next_cost >= best_state_cost:
					continue
				best_state_costs[next_state_key] = next_cost
				best_coord_costs[neighbor_coord] = mini(int(best_coord_costs.get(neighbor_coord, 2147483647)), next_cost)
				buckets[next_cost].append({
					"coord": neighbor_coord,
					"spent_cost": next_cost,
					"has_quickstep_bonus": next_has_quickstep_bonus,
				})

	best_coord_costs.erase(origin)
	return _sort_coords(_collect_dict_vector2i_keys(best_coord_costs))


func end_battle(result: Dictionary = {}) -> void:
	if _state == null:
		return
	if _character_gateway != null and bool(result.get("commit_progression", false)):
		for ally_unit_id in _state.ally_unit_ids:
			var unit_state := _state.units.get(ally_unit_id) as BattleUnitState
			if unit_state == null:
				continue
			if unit_state.is_alive:
				_character_gateway.commit_battle_resources(unit_state.source_member_id, unit_state.current_hp, unit_state.current_mp)
			else:
				_character_gateway.commit_battle_death(unit_state.source_member_id)
		_character_gateway.flush_after_battle()
	if _battle_resolution_result == null and not _battle_resolution_result_consumed and _state.phase == &"battle_ended":
		_battle_resolution_result = _build_battle_resolution_result()


func get_battle_resolution_result():
	return _battle_resolution_result if not _battle_resolution_result_consumed else null


func consume_battle_resolution_result():
	if _battle_resolution_result_consumed:
		return null
	var resolution_result = _battle_resolution_result
	if resolution_result == null and _state != null and _state.phase == &"battle_ended":
		resolution_result = _build_battle_resolution_result()
	if resolution_result != null:
		_pending_post_battle_character_rewards.clear()
		_active_loot_entries.clear()
		_looted_defeated_unit_ids.clear()
	_battle_resolution_result = null
	_battle_resolution_result_consumed = true
	return resolution_result


func get_grid_service():
	return _grid_service


func get_character_gateway():
	return _character_gateway


func get_damage_resolver():
	return _damage_resolver


func configure_damage_resolver_for_tests(damage_resolver: BattleDamageResolver) -> void:
	_damage_resolver = damage_resolver if damage_resolver != null else BATTLE_DAMAGE_RESOLVER_SCRIPT.new()
	if _ai_service != null:
		_ai_service.setup(_enemy_ai_brains, _damage_resolver)
	if _fortune_service != null:
		_fortune_service.setup(_character_gateway, get_fate_event_bus())
	if _misfortune_service != null:
		_misfortune_service.setup(get_fate_event_bus(), Callable(self, "_find_unit_by_member_id"))
	_change_equipment_resolver.setup(self)
	_loot_resolver.setup(self)
	_skill_turn_resolver.setup(self)


func get_fate_event_bus():
	return _damage_resolver.get_fate_event_bus() if _damage_resolver != null else null


func get_hit_resolver():
	return _hit_resolver


func configure_hit_resolver_for_tests(hit_resolver: BattleHitResolver) -> void:
	_hit_resolver = hit_resolver if hit_resolver != null else BATTLE_HIT_RESOLVER_SCRIPT.new()


func get_terrain_generator():
	return _terrain_generator


func get_skill_defs() -> Dictionary:
	return _skill_defs


func get_item_defs() -> Dictionary:
	return _item_defs


func get_min_battle_surface_height() -> int:
	return MIN_BATTLE_SURFACE_HEIGHT


func get_battle_rating_stats() -> Dictionary:
	return _battle_rating_stats


func get_battle_rating_system():
	return _battle_rating_system


func get_pending_post_battle_character_rewards() -> Array:
	return _pending_post_battle_character_rewards


func set_ai_trace_enabled(enabled: bool) -> void:
	_ai_trace_enabled = enabled
	if not enabled:
		_ai_turn_traces.clear()


func get_ai_turn_traces() -> Array[Dictionary]:
	return _ai_turn_traces


func clear_ai_turn_traces() -> void:
	_ai_turn_traces.clear()


func get_battle_metrics() -> Dictionary:
	return _battle_metrics


func set_ai_score_profile(profile) -> void:
	_ai_service.set_score_profile(profile)


func get_ai_score_profile():
	return _ai_service.get_score_profile()


func get_terrain_effect_nonce() -> int:
	return _terrain_effect_nonce


func increment_terrain_effect_nonce() -> int:
	_terrain_effect_nonce += 1
	return _terrain_effect_nonce


func new_batch() -> BattleEventBatch:
	return _new_batch()


func merge_batch(target_batch: BattleEventBatch, source_batch: BattleEventBatch) -> void:
	_merge_batch(target_batch, source_batch)


func append_changed_coord(batch: BattleEventBatch, coord: Vector2i) -> void:
	_append_changed_coord(batch, coord)


func append_changed_coords(batch: BattleEventBatch, coords: Array[Vector2i]) -> void:
	_append_changed_coords(batch, coords)


func append_changed_unit_id(batch: BattleEventBatch, unit_id: StringName) -> void:
	_append_changed_unit_id(batch, unit_id)


func append_changed_unit_coords(batch: BattleEventBatch, unit_state: BattleUnitState) -> void:
	_append_changed_unit_coords(batch, unit_state)


func append_batch_log(batch: BattleEventBatch, message: String) -> void:
	_append_batch_log(batch, message)


func append_result_report_entry(batch: BattleEventBatch, result: Dictionary) -> void:
	_append_result_report_entry(batch, result)


func clear_defeated_unit(unit_state: BattleUnitState, batch: BattleEventBatch = null) -> void:
	_clear_defeated_unit(unit_state, batch)


func sort_coords(target_coords: Variant) -> Array[Vector2i]:
	return _sort_coords(target_coords)


func format_skill_variant_label(skill_def: SkillDef, cast_variant: CombatCastVariantDef) -> String:
	return _format_skill_variant_label(skill_def, cast_variant)


func mark_applied_statuses_for_turn_timing(target_unit: BattleUnitState, status_effect_ids: Variant) -> void:
	_initialize_applied_status_timeline_ticks(target_unit, status_effect_ids)
	if _misfortune_service == null:
		return
	_misfortune_service.handle_applied_statuses(target_unit, status_effect_ids)


func _initialize_applied_status_timeline_ticks(target_unit: BattleUnitState, status_effect_ids: Variant) -> void:
	if target_unit == null:
		return
	var normalized_status_ids := ProgressionDataUtils.to_string_name_array(status_effect_ids)
	if normalized_status_ids.is_empty():
		return
	var current_tu := int(_state.timeline.current_tu) if _state != null and _state.timeline != null else 0
	for status_id in normalized_status_ids:
		var status_entry = target_unit.get_status_effect(status_id)
		if status_entry == null or status_entry.tick_interval_tu <= 0:
			continue
		if status_entry.next_tick_at_tu <= current_tu:
			status_entry.next_tick_at_tu = current_tu + status_entry.tick_interval_tu
			target_unit.set_status_effect(status_entry)


func resolve_effect_target_filter(skill_def: SkillDef, effect_def: CombatEffectDef) -> StringName:
	return _resolve_effect_target_filter(skill_def, effect_def)


func is_unit_valid_for_effect(source_unit: BattleUnitState, target_unit: BattleUnitState, target_filter: StringName) -> bool:
	return _is_unit_valid_for_effect(source_unit, target_unit, target_filter)


func is_unit_effect(effect_def: CombatEffectDef) -> bool:
	return _is_unit_effect(effect_def)


func collect_units_in_coords(effect_coords: Array[Vector2i]) -> Array[BattleUnitState]:
	return _collect_units_in_coords(effect_coords)


func get_unit_skill_level(unit_state: BattleUnitState, skill_id: StringName) -> int:
	return _get_unit_skill_level(unit_state, skill_id)


func record_enemy_defeated_achievement(active_unit: BattleUnitState, target_unit: BattleUnitState) -> void:
	_battle_rating_system.record_enemy_defeated_achievement(active_unit, target_unit)


func record_skill_effect_result(source_unit: BattleUnitState, damage: int, healing: int, kill_count: int) -> void:
	_battle_rating_system.record_skill_effect_result(source_unit, damage, healing, kill_count)
	if source_unit == null:
		return
	var source_entry := _ensure_unit_metric_entry(source_unit)
	var faction_entry := _ensure_faction_metric_entry(source_unit.faction_id)
	source_entry["total_damage_done"] = int(source_entry.get("total_damage_done", 0)) + maxi(damage, 0)
	source_entry["total_healing_done"] = int(source_entry.get("total_healing_done", 0)) + maxi(healing, 0)
	source_entry["kill_count"] = int(source_entry.get("kill_count", 0)) + maxi(kill_count, 0)
	faction_entry["total_damage_done"] = int(faction_entry.get("total_damage_done", 0)) + maxi(damage, 0)
	faction_entry["total_healing_done"] = int(faction_entry.get("total_healing_done", 0)) + maxi(healing, 0)
	faction_entry["kill_count"] = int(faction_entry.get("kill_count", 0)) + maxi(kill_count, 0)


func append_result_source_status_effects(batch: BattleEventBatch, source_unit: BattleUnitState, result: Dictionary) -> void:
	if source_unit == null or result.is_empty():
		return
	var source_status_ids := ProgressionDataUtils.to_string_name_array(result.get("source_status_effect_ids", []))
	if source_status_ids.is_empty():
		return
	mark_applied_statuses_for_turn_timing(source_unit, source_status_ids)
	_append_changed_unit_id(batch, source_unit.unit_id)
	for status_id in source_status_ids:
		batch.log_lines.append("%s 获得状态 %s。" % [source_unit.display_name, String(status_id)])


func _initialize_battle_metrics() -> void:
	_battle_metrics = {
		"battle_id": String(_state.battle_id) if _state != null else "",
		"seed": int(_state.seed) if _state != null else 0,
		"units": {},
		"factions": {},
	}
	if _state == null:
		return
	for unit_variant in _state.units.values():
		var unit_state := unit_variant as BattleUnitState
		if unit_state == null:
			continue
		var unit_entry := _build_unit_metric_entry(unit_state)
		_battle_metrics["units"][String(unit_state.unit_id)] = unit_entry
		var faction_entry := _ensure_faction_metric_entry(unit_state.faction_id)
		faction_entry["unit_count"] = int(faction_entry.get("unit_count", 0)) + 1


func _build_unit_metric_entry(unit_state: BattleUnitState) -> Dictionary:
	return {
		"unit_id": String(unit_state.unit_id),
		"display_name": unit_state.display_name,
		"faction_id": String(unit_state.faction_id),
		"control_mode": String(unit_state.control_mode),
		"source_member_id": String(unit_state.source_member_id),
		"turn_count": 0,
		"action_counts": {"move": 0, "skill": 0, "wait": 0},
		"skill_attempt_counts": {},
		"skill_success_counts": {},
		"successful_skill_count": 0,
		"total_damage_done": 0,
		"total_healing_done": 0,
		"total_damage_taken": 0,
		"total_healing_received": 0,
		"kill_count": 0,
		"death_count": 0,
	}


func _ensure_unit_metric_entry(unit_state: BattleUnitState) -> Dictionary:
	if _battle_metrics.is_empty() or unit_state == null:
		return {}
	var units: Dictionary = _battle_metrics.get("units", {})
	var unit_key := String(unit_state.unit_id)
	if not units.has(unit_key):
		units[unit_key] = _build_unit_metric_entry(unit_state)
		_battle_metrics["units"] = units
	return units.get(unit_key, {})


func _ensure_faction_metric_entry(faction_id: StringName) -> Dictionary:
	if _battle_metrics.is_empty():
		return {}
	var factions: Dictionary = _battle_metrics.get("factions", {})
	var faction_key := String(faction_id)
	if not factions.has(faction_key):
		factions[faction_key] = {
			"faction_id": faction_key,
			"unit_count": 0,
			"turn_count": 0,
			"action_counts": {"move": 0, "skill": 0, "wait": 0},
			"skill_attempt_counts": {},
			"skill_success_counts": {},
			"successful_skill_count": 0,
			"total_damage_done": 0,
			"total_healing_done": 0,
			"total_damage_taken": 0,
			"total_healing_received": 0,
			"kill_count": 0,
			"death_count": 0,
		}
		_battle_metrics["factions"] = factions
	return factions.get(faction_key, {})


func _record_turn_started(unit_state: BattleUnitState) -> void:
	var unit_entry := _ensure_unit_metric_entry(unit_state)
	if unit_entry.is_empty():
		return
	unit_entry["turn_count"] = int(unit_entry.get("turn_count", 0)) + 1
	var faction_entry := _ensure_faction_metric_entry(unit_state.faction_id)
	faction_entry["turn_count"] = int(faction_entry.get("turn_count", 0)) + 1


func _record_action_issued(unit_state: BattleUnitState, command_type: StringName) -> void:
	if unit_state != null and command_type != BattleCommand.TYPE_WAIT:
		unit_state.has_taken_action_this_turn = true
		unit_state.is_resting = false
	var command_key := String(command_type)
	if command_key.is_empty():
		return
	var unit_entry := _ensure_unit_metric_entry(unit_state)
	if unit_entry.is_empty():
		return
	_increment_metric_count(unit_entry.get("action_counts", {}), command_key, 1)
	var faction_entry := _ensure_faction_metric_entry(unit_state.faction_id)
	_increment_metric_count(faction_entry.get("action_counts", {}), command_key, 1)


func _record_skill_attempt(unit_state: BattleUnitState, skill_id: StringName) -> void:
	var skill_key := String(skill_id)
	if skill_key.is_empty():
		return
	var unit_entry := _ensure_unit_metric_entry(unit_state)
	if unit_entry.is_empty():
		return
	_increment_metric_count(unit_entry.get("skill_attempt_counts", {}), skill_key, 1)
	var faction_entry := _ensure_faction_metric_entry(unit_state.faction_id)
	_increment_metric_count(faction_entry.get("skill_attempt_counts", {}), skill_key, 1)


func _record_skill_success(unit_state: BattleUnitState, skill_id: StringName) -> void:
	var skill_key := String(skill_id)
	if skill_key.is_empty():
		return
	var unit_entry := _ensure_unit_metric_entry(unit_state)
	if unit_entry.is_empty():
		return
	_increment_metric_count(unit_entry.get("skill_success_counts", {}), skill_key, 1)
	unit_entry["successful_skill_count"] = int(unit_entry.get("successful_skill_count", 0)) + 1
	var faction_entry := _ensure_faction_metric_entry(unit_state.faction_id)
	_increment_metric_count(faction_entry.get("skill_success_counts", {}), skill_key, 1)
	faction_entry["successful_skill_count"] = int(faction_entry.get("successful_skill_count", 0)) + 1


func _record_effect_metrics(
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	damage: int,
	healing: int,
	kill_count: int
) -> void:
	if source_unit == null or target_unit == null:
		return
	var source_entry := _ensure_unit_metric_entry(source_unit)
	var target_entry := _ensure_unit_metric_entry(target_unit)
	var source_faction_entry := _ensure_faction_metric_entry(source_unit.faction_id)
	var target_faction_entry := _ensure_faction_metric_entry(target_unit.faction_id)
	if damage > 0:
		source_entry["total_damage_done"] = int(source_entry.get("total_damage_done", 0)) + damage
		target_entry["total_damage_taken"] = int(target_entry.get("total_damage_taken", 0)) + damage
		source_faction_entry["total_damage_done"] = int(source_faction_entry.get("total_damage_done", 0)) + damage
		target_faction_entry["total_damage_taken"] = int(target_faction_entry.get("total_damage_taken", 0)) + damage
	if healing > 0:
		source_entry["total_healing_done"] = int(source_entry.get("total_healing_done", 0)) + healing
		target_entry["total_healing_received"] = int(target_entry.get("total_healing_received", 0)) + healing
		source_faction_entry["total_healing_done"] = int(source_faction_entry.get("total_healing_done", 0)) + healing
		target_faction_entry["total_healing_received"] = int(target_faction_entry.get("total_healing_received", 0)) + healing
	if kill_count > 0:
		source_entry["kill_count"] = int(source_entry.get("kill_count", 0)) + kill_count
		source_faction_entry["kill_count"] = int(source_faction_entry.get("kill_count", 0)) + kill_count


func _record_unit_defeated(unit_state: BattleUnitState) -> void:
	var unit_entry := _ensure_unit_metric_entry(unit_state)
	if unit_entry.is_empty():
		return
	unit_entry["death_count"] = int(unit_entry.get("death_count", 0)) + 1
	var faction_entry := _ensure_faction_metric_entry(unit_state.faction_id)
	faction_entry["death_count"] = int(faction_entry.get("death_count", 0)) + 1


func _increment_metric_count(metric_map: Dictionary, key: String, delta: int) -> void:
	metric_map[key] = int(metric_map.get(key, 0)) + delta


func dispose() -> void:
	if _terrain_effect_system != null:
		_terrain_effect_system.dispose()
	if _battle_rating_system != null:
		_battle_rating_system.dispose()
	if _unit_factory != null:
		_unit_factory.dispose()
	if _charge_resolver != null:
		_charge_resolver.dispose()
	if _repeat_attack_resolver != null:
		_repeat_attack_resolver.dispose()
	if _change_equipment_resolver != null:
		_change_equipment_resolver.dispose()
	if _loot_resolver != null:
		_loot_resolver.dispose()
	if _skill_turn_resolver != null:
		_skill_turn_resolver.dispose()
	if _skill_mastery_service != null:
		_skill_mastery_service.clear()
	if _fortune_service != null:
		_fortune_service.dispose()
	if _misfortune_service != null:
		_misfortune_service.dispose()
	_battle_rating_stats.clear()
	_pending_post_battle_character_rewards.clear()
	_active_loot_entries.clear()
	_looted_defeated_unit_ids.clear()
	_ai_turn_traces.clear()
	_battle_metrics.clear()
	calamity_by_member_id.clear()
	_battle_resolution_result = null
	_battle_resolution_result_consumed = false
	_terrain_effect_nonce = 0
	_ai_trace_enabled = false
	_character_gateway = null
	_skill_defs = {}
	_item_defs = {}
	_enemy_templates = {}
	_enemy_ai_brains = {}
	_encounter_builder = null
	_equipment_drop_service = null
	if _state != null:
		_state.cells.clear()
		_state.units.clear()
		_state.ally_unit_ids.clear()
		_state.enemy_unit_ids.clear()
		if _state.timeline != null:
			_state.timeline.ready_unit_ids.clear()
	_state = null


func _place_units(units: Array, spawn_coords: Array, is_ally: bool) -> void:
	for index in range(units.size()):
		var unit_state := units[index] as BattleUnitState
		if unit_state == null:
			continue
		unit_state.refresh_footprint()
		_state.units[unit_state.unit_id] = unit_state
		var preferred_coords: Array[Vector2i] = []
		if index < spawn_coords.size():
			preferred_coords.append(spawn_coords[index])
		for spawn_coord in spawn_coords:
			var coord: Vector2i = spawn_coord
			if not preferred_coords.has(coord):
				preferred_coords.append(coord)
		var placement_coord := _find_spawn_anchor(unit_state, preferred_coords)
		if placement_coord == Vector2i(-1, -1):
			_state.units.erase(unit_state.unit_id)
			continue
		_grid_service.place_unit(_state, unit_state, placement_coord, true)
		if is_ally:
			_state.ally_unit_ids.append(unit_state.unit_id)
		else:
			_state.enemy_unit_ids.append(unit_state.unit_id)


func _find_spawn_anchor(unit_state: BattleUnitState, preferred_coords: Array[Vector2i]) -> Vector2i:
	if _state == null or unit_state == null:
		return Vector2i(-1, -1)
	var best_coord := Vector2i(-1, -1)
	var best_score := -2147483647
	for preferred_index in range(preferred_coords.size()):
		var coord: Vector2i = preferred_coords[preferred_index]
		if not _can_place_spawn_anchor(unit_state, coord):
			continue
		var score := _score_spawn_anchor(unit_state, coord, preferred_index)
		if score > best_score:
			best_score = score
			best_coord = coord
	if best_coord != Vector2i(-1, -1):
		return best_coord
	for preferred_coord in preferred_coords:
		var coord: Vector2i = preferred_coord
		if _can_place_spawn_anchor(unit_state, coord):
			return coord
	for y in range(_state.map_size.y):
		for x in range(_state.map_size.x):
			var coord := Vector2i(x, y)
			if _can_place_spawn_anchor(unit_state, coord):
				return coord
	return Vector2i(-1, -1)


func _can_place_spawn_anchor(unit_state: BattleUnitState, coord: Vector2i) -> bool:
	if _state == null or unit_state == null:
		return false
	if not _grid_service.can_place_footprint(_state, coord, unit_state.footprint_size, unit_state.unit_id):
		return false
	for footprint_coord in _grid_service.get_unit_target_coords(unit_state, coord):
		var cell := _grid_service.get_cell(_state, footprint_coord)
		if cell == null or BattleTerrainRules.is_water_terrain(cell.base_terrain):
			return false
	return true


func _score_spawn_anchor(unit_state: BattleUnitState, coord: Vector2i, preferred_index: int) -> int:
	var mobility_score := _count_spawn_anchor_reachable_coords(unit_state, coord)
	var edge_clearance := _get_spawn_anchor_edge_clearance(unit_state, coord)
	var center_bias := _get_spawn_anchor_center_bias(unit_state, coord)
	return mobility_score * 100 + edge_clearance * 18 + center_bias * 4 - preferred_index


func _count_spawn_anchor_reachable_coords(unit_state: BattleUnitState, start_coord: Vector2i) -> int:
	if _state == null or unit_state == null:
		return 0
	var move_budget := mini(maxi(int(unit_state.current_move_points), 0), 4)
	if move_budget <= 0:
		move_budget = 1
	var best_costs := {
		start_coord: 0,
	}
	var frontier: Array[Vector2i] = [start_coord]
	var frontier_index := 0
	while frontier_index < frontier.size():
		var current_coord := frontier[frontier_index]
		frontier_index += 1
		var spent_cost := int(best_costs.get(current_coord, 0))
		for neighbor_coord in _grid_service.get_neighbors_4(_state, current_coord):
			if not _grid_service.can_unit_step_between_anchors(_state, unit_state, current_coord, neighbor_coord):
				continue
			var move_cost := _grid_service.get_unit_move_cost(_state, unit_state, neighbor_coord)
			var next_cost := spent_cost + move_cost
			if next_cost > move_budget:
				continue
			if next_cost >= int(best_costs.get(neighbor_coord, 2147483647)):
				continue
			best_costs[neighbor_coord] = next_cost
			frontier.append(neighbor_coord)
	return best_costs.size() - 1


func _get_spawn_anchor_edge_clearance(unit_state: BattleUnitState, coord: Vector2i) -> int:
	if _state == null or unit_state == null:
		return 0
	var footprint := unit_state.footprint_size
	var left_clearance := coord.x
	var top_clearance := coord.y
	var right_clearance := _state.map_size.x - (coord.x + footprint.x)
	var bottom_clearance := _state.map_size.y - (coord.y + footprint.y)
	return mini(mini(left_clearance, right_clearance), mini(top_clearance, bottom_clearance))


func _get_spawn_anchor_center_bias(unit_state: BattleUnitState, coord: Vector2i) -> int:
	if _state == null or unit_state == null:
		return 0
	var footprint := unit_state.footprint_size
	var center_x := float(_state.map_size.x - footprint.x) * 0.5
	var center_y := float(_state.map_size.y - footprint.y) * 0.5
	var distance := absf(float(coord.x) - center_x) + absf(float(coord.y) - center_y)
	return -int(round(distance * 10.0))


func _get_move_cost_for_unit_target(
	unit_state: BattleUnitState,
	target_coord: Vector2i,
	allow_quickstep_bonus: bool = true
) -> int:
	if _state == null or unit_state == null:
		return 1
	var move_cost := _grid_service.get_unit_move_cost(_state, unit_state, target_coord)
	move_cost += _get_status_move_cost_delta(unit_state)
	if allow_quickstep_bonus and _has_status(unit_state, STATUS_ARCHER_QUICKSTEP):
		move_cost = maxi(move_cost - 1, 0)
	return move_cost


func _get_move_path_cost(unit_state: BattleUnitState, anchor_path: Array[Vector2i]) -> int:
	if unit_state == null or anchor_path.size() <= 1:
		return 0
	var total_cost := 0
	var allow_quickstep_bonus := _has_status(unit_state, STATUS_ARCHER_QUICKSTEP)
	for path_index in range(1, anchor_path.size()):
		total_cost += _get_move_cost_for_unit_target(unit_state, anchor_path[path_index], allow_quickstep_bonus)
		allow_quickstep_bonus = false
	return total_cost


func _get_status_move_cost_delta(unit_state: BattleUnitState) -> int:
	if unit_state == null:
		return 0
	var total_delta := 0
	for status_id_str in ProgressionDataUtils.sorted_string_keys(unit_state.status_effects):
		var status_entry = unit_state.get_status_effect(StringName(status_id_str))
		total_delta += BATTLE_STATUS_SEMANTIC_TABLE_SCRIPT.get_move_cost_delta(status_entry)
	return maxi(total_delta, 0)


func _resolve_move_path_result(active_unit: BattleUnitState, target_coord: Vector2i) -> Dictionary:
	if _state == null or active_unit == null:
		return {
			"allowed": false,
			"cost": 0,
			"path": [],
			"message": "当前单位数据不可用。",
		}
	var first_step_cost_discount := 1 if _has_status(active_unit, STATUS_ARCHER_QUICKSTEP) else 0
	var move_result := _grid_service.resolve_unit_move_path(
		_state,
		active_unit,
		active_unit.coord,
		target_coord,
		maxi(int(active_unit.current_move_points), 0),
		first_step_cost_discount
	)
	var anchor_path: Array[Vector2i] = []
	var path_variant = move_result.get("path", [])
	if path_variant is Array:
		for coord_variant in path_variant:
			if coord_variant is Vector2i:
				anchor_path.append(coord_variant)
	if anchor_path.size() > 1:
		var semantic_cost := _get_move_path_cost(active_unit, anchor_path)
		move_result["cost"] = semantic_cost
		if semantic_cost > maxi(int(active_unit.current_move_points), 0):
			move_result["allowed"] = false
			move_result["message"] = "行动点不足，无法移动。"
	return move_result


func _handle_move_command(active_unit: BattleUnitState, command: BattleCommand, batch: BattleEventBatch) -> void:
	if _is_movement_blocked(active_unit):
		batch.log_lines.append("%s 当前被限制移动。" % active_unit.display_name)
		return
	var target_coord := command.target_coord
	var move_result := _resolve_move_path_result(active_unit, target_coord)
	if not bool(move_result.get("allowed", false)):
		batch.log_lines.append(String(move_result.get("message", "该移动不可执行。")))
		return
	var target_cell := _grid_service.get_cell(_state, target_coord)
	if target_cell == null:
		return
	var move_cost := int(move_result.get("cost", 0))
	var anchor_path: Array[Vector2i] = []
	for coord_variant in move_result.get("path", []):
		if coord_variant is Vector2i:
			anchor_path.append(coord_variant)

	var previous_anchor := active_unit.coord
	var previous_coords := active_unit.occupied_coords.duplicate()
	if _move_unit_along_validated_path(active_unit, anchor_path, target_coord, batch):
		active_unit.current_move_points = maxi(active_unit.current_move_points - move_cost, 0)
		_consume_status_if_present(active_unit, STATUS_ARCHER_QUICKSTEP, batch)
		_record_action_issued(active_unit, BattleCommand.TYPE_MOVE)
		batch.changed_unit_ids.append(active_unit.unit_id)
		_append_changed_coords(batch, previous_coords)
		_append_changed_unit_coords(batch, active_unit)
		var terrain_name := _grid_service.get_terrain_display_name(String(target_cell.base_terrain)) if target_cell != null else "地格"
		batch.log_lines.append("%s 从 (%d, %d) 移动到 (%d, %d)，消耗 %d 点行动点。%s。" % [
			active_unit.display_name,
			previous_anchor.x,
			previous_anchor.y,
			target_coord.x,
			target_coord.y,
			move_cost,
			terrain_name,
		])
	else:
		batch.log_lines.append("%s 的移动落点已失效，无法执行。" % active_unit.display_name)


func _preview_change_equipment_command(active_unit: BattleUnitState, command: BattleCommand, preview: BattlePreview) -> void:
	_change_equipment_resolver.preview_command(active_unit, command, preview)


func _handle_change_equipment_command(active_unit: BattleUnitState, command: BattleCommand, batch: BattleEventBatch) -> void:
	_change_equipment_resolver.handle_command(active_unit, command, batch)


func _get_unit_hp_max(unit_state: BattleUnitState) -> int:
	return _change_equipment_resolver.get_unit_hp_max(unit_state)


func _get_unit_stamina_max(unit_state: BattleUnitState) -> int:
	return _change_equipment_resolver.get_unit_stamina_max(unit_state)


func _build_change_equipment_result(
	allowed: bool,
	error_code: String,
	message: String,
	command: BattleCommand
) -> Dictionary:
	return _change_equipment_resolver.build_result(allowed, error_code, message, command)


func _append_change_equipment_report(
	batch: BattleEventBatch,
	active_unit: BattleUnitState,
	result: Dictionary,
	success: bool
) -> void:
	_change_equipment_resolver.append_report(batch, active_unit, result, success)

func _move_unit_along_validated_path(
	active_unit: BattleUnitState,
	anchor_path: Array[Vector2i],
	target_coord: Vector2i,
	batch: BattleEventBatch
) -> bool:
	if active_unit == null:
		return false
	if anchor_path.is_empty():
		return false
	if anchor_path[0] != active_unit.coord or anchor_path[anchor_path.size() - 1] != target_coord:
		return false
	if anchor_path.size() == 1:
		return active_unit.coord == target_coord
	for path_index in range(1, anchor_path.size()):
		var next_coord := anchor_path[path_index]
		if not _grid_service.can_unit_step_between_anchors(_state, active_unit, active_unit.coord, next_coord):
			if batch != null:
				batch.log_lines.append("%s 的移动路径第 %d 步已不可通行。" % [active_unit.display_name, path_index])
			return false
		if not _grid_service.move_unit(_state, active_unit, next_coord):
			if batch != null:
				batch.log_lines.append("%s 的移动路径第 %d 步执行失败。" % [active_unit.display_name, path_index])
			return false
	return active_unit.coord == target_coord


func _handle_skill_command(active_unit: BattleUnitState, command: BattleCommand, batch: BattleEventBatch) -> void:
	var skill_def := _skill_defs.get(command.skill_id) as SkillDef
	if skill_def == null or skill_def.combat_profile == null:
		return
	var unit_cast_variant := _resolve_unit_cast_variant(skill_def, active_unit, command)
	var ground_cast_variant := _resolve_ground_cast_variant(skill_def, active_unit, command)
	var command_cast_variant := ground_cast_variant if ground_cast_variant != null else unit_cast_variant
	var unit_execution_cast_variant := unit_cast_variant if unit_cast_variant != null else ground_cast_variant
	var block_reason := _get_skill_command_block_reason(active_unit, skill_def, command_cast_variant)
	if not block_reason.is_empty():
		batch.log_lines.append(block_reason)
		return

	_record_skill_attempt(active_unit, command.skill_id)
	_record_action_issued(active_unit, BattleCommand.TYPE_SKILL)
	_skill_mastery_service.clear()
	var applied := false
	if _should_route_skill_command_to_unit_targeting(skill_def, command):
		applied = _handle_unit_skill_command(active_unit, command, skill_def, unit_execution_cast_variant, batch)
	else:
		if ground_cast_variant != null:
			applied = _handle_ground_skill_command(active_unit, command, skill_def, ground_cast_variant, batch)
		else:
			applied = _handle_unit_skill_command(active_unit, command, skill_def, null, batch)

	if applied:
		_grant_skill_mastery_if_needed(active_unit, skill_def, batch)
	_skill_mastery_service.clear()


func _preview_skill_command(active_unit: BattleUnitState, command: BattleCommand, preview: BattlePreview) -> void:
	var skill_def := _skill_defs.get(command.skill_id) as SkillDef
	if skill_def == null or skill_def.combat_profile == null:
		preview.log_lines.append("技能或目标无效。")
		return
	var unit_cast_variant := _resolve_unit_cast_variant(skill_def, active_unit, command)
	var ground_cast_variant := _resolve_ground_cast_variant(skill_def, active_unit, command)
	var unit_execution_cast_variant := unit_cast_variant if unit_cast_variant != null else ground_cast_variant

	if _should_route_skill_command_to_unit_targeting(skill_def, command):
		_preview_unit_skill_command(active_unit, command, skill_def, unit_execution_cast_variant, preview)
		return

	if ground_cast_variant != null:
		_preview_ground_skill_command(active_unit, command, skill_def, ground_cast_variant, preview)
		return

	_preview_unit_skill_command(active_unit, command, skill_def, null, preview)


func _preview_unit_skill_command(
	active_unit: BattleUnitState,
	command: BattleCommand,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	preview: BattlePreview
) -> void:
	var block_reason := _get_skill_command_block_reason(active_unit, skill_def, cast_variant)
	if not block_reason.is_empty():
		preview.log_lines.append(block_reason)
		return

	var validation := _validate_unit_skill_targets(active_unit, command, skill_def, cast_variant)
	preview.allowed = bool(validation.get("allowed", false))
	preview.target_unit_ids.clear()
	for target_unit_id_variant in validation.get("target_unit_ids", []):
		preview.target_unit_ids.append(ProgressionDataUtils.to_string_name(target_unit_id_variant))
	preview.target_coords.clear()
	for preview_coord_variant in validation.get("preview_coords", []):
		if preview_coord_variant is Vector2i:
			preview.target_coords.append(preview_coord_variant)
	if preview.allowed:
		var target_units := validation.get("target_units", []) as Array
		preview.hit_preview = _build_unit_skill_hit_preview(active_unit, target_units, skill_def, cast_variant)
		preview.damage_preview = _build_unit_skill_damage_preview(active_unit, skill_def, cast_variant)
		var skill_label := _format_skill_variant_label(skill_def, cast_variant)
		if target_units.size() == 1:
			var target_unit := target_units[0] as BattleUnitState
			if target_unit != null:
				preview.log_lines.append("%s 可对 %s 使用 %s。" % [active_unit.display_name, target_unit.display_name, skill_label])
				if not preview.hit_preview.is_empty():
					preview.log_lines.append(String(preview.hit_preview.get("summary_text", "")))
				_append_damage_preview_line(preview)
				return
		preview.log_lines.append("%s 可对 %d 个单位使用 %s。" % [
			active_unit.display_name,
			preview.target_unit_ids.size(),
			skill_label,
		])
		if not preview.hit_preview.is_empty():
			preview.log_lines.append(String(preview.hit_preview.get("summary_text", "")))
		_append_damage_preview_line(preview)
		return
	preview.log_lines.append(String(validation.get("message", "技能或目标无效。")))


func _preview_ground_skill_command(
	active_unit: BattleUnitState,
	command: BattleCommand,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	preview: BattlePreview
) -> void:
	var block_reason := _get_skill_command_block_reason(active_unit, skill_def, cast_variant)
	if not block_reason.is_empty():
		preview.log_lines.append(block_reason)
		return
	var validation := _validate_ground_skill_command(active_unit, skill_def, cast_variant, command)
	preview.target_coords.clear()
	var preview_coords: Array[Vector2i] = validation.get(
		"preview_coords",
		_build_ground_effect_coords(skill_def, validation.get("target_coords", []), active_unit.coord if active_unit != null else Vector2i(-1, -1), active_unit)
	)
	preview.resolved_anchor_coord = validation.get("resolved_anchor_coord", Vector2i(-1, -1))
	if bool(validation.get("allowed", false)):
		var path_step_aoe_effect := _charge_resolver.get_charge_path_step_aoe_effect_def(cast_variant, skill_def, active_unit)
		if path_step_aoe_effect != null:
			preview_coords = _charge_resolver.build_charge_step_aoe_preview_coords(
				active_unit,
				validation.get("direction", Vector2i.ZERO),
				int(validation.get("distance", 0)),
				path_step_aoe_effect
			)
	for target_coord in preview_coords:
		preview.target_coords.append(target_coord)
	preview.target_unit_ids = _collect_ground_preview_unit_ids(
		active_unit,
		skill_def,
		_collect_ground_unit_effect_defs(skill_def, cast_variant, active_unit),
		preview.target_coords
	)
	if bool(validation.get("allowed", false)):
		var path_step_aoe_effect := _charge_resolver.get_charge_path_step_aoe_effect_def(cast_variant, skill_def, active_unit)
		if path_step_aoe_effect != null:
			var path_step_target_filter := _resolve_effect_target_filter(skill_def, path_step_aoe_effect)
			for target_unit in _collect_units_in_coords(preview.target_coords):
				if not _is_unit_valid_for_effect(active_unit, target_unit, path_step_target_filter):
					continue
				if preview.target_unit_ids.has(target_unit.unit_id):
					continue
				preview.target_unit_ids.append(target_unit.unit_id)
	preview.allowed = bool(validation.get("allowed", false))
	if preview.allowed:
		preview.log_lines.append("%s 可使用 %s，预计影响 %d 个地格、%d 个单位。" % [
			active_unit.display_name,
			_format_skill_variant_label(skill_def, cast_variant),
			preview.target_coords.size(),
			preview.target_unit_ids.size(),
		])
	else:
		preview.log_lines.append(String(validation.get("message", "地面技能目标无效。")))


func _build_unit_skill_hit_preview(
	active_unit: BattleUnitState,
	target_units: Array,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef
) -> Dictionary:
	if active_unit == null or skill_def == null or target_units.size() != 1:
		return {}
	var target_unit := target_units[0] as BattleUnitState
	if target_unit == null:
		return {}
	var effect_defs := _collect_unit_skill_effect_defs(skill_def, cast_variant, active_unit)
	var repeat_attack_effect := _repeat_attack_resolver.get_repeat_attack_effect_def(
		effect_defs
	)
	if repeat_attack_effect == null:
		if not _skill_resolution_rules.should_resolve_unit_skill_as_fate_attack(
			active_unit,
			target_unit,
			skill_def,
			effect_defs
		):
			return {}
		return _hit_resolver.build_skill_attack_preview(
			_state,
			active_unit,
			target_unit,
			skill_def,
			_skill_resolution_rules.is_force_hit_no_crit_skill(skill_def)
		)
	return _hit_resolver.build_repeat_attack_preview(_state, active_unit, target_unit, skill_def, repeat_attack_effect)


func _build_unit_skill_damage_preview(
	active_unit: BattleUnitState,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef
) -> Dictionary:
	if active_unit == null or skill_def == null:
		return {}
	var effect_defs := _collect_unit_skill_effect_defs(skill_def, cast_variant, active_unit)
	return BATTLE_DAMAGE_PREVIEW_RANGE_SERVICE_SCRIPT.build_skill_damage_preview(active_unit, effect_defs)


func _append_damage_preview_line(preview: BattlePreview) -> void:
	if preview == null or preview.damage_preview.is_empty():
		return
	var damage_preview_text := String(preview.damage_preview.get("summary_text", ""))
	if damage_preview_text.is_empty():
		return
	preview.log_lines.append(damage_preview_text)


func summarize_damage_result(result: Dictionary) -> Dictionary:
	return _report_formatter.summarize_damage_result(result)


func build_damage_absorb_reason_text(summary: Dictionary) -> String:
	return _report_formatter.build_damage_absorb_reason_text(summary)


func append_damage_result_log_lines(
	batch: BattleEventBatch,
	subject_label: String,
	target_display_name: String,
	result: Dictionary
) -> void:
	_report_formatter.append_damage_result_log_lines(batch, subject_label, target_display_name, result)


func _build_unit_skill_resolution_preview_lines(
	active_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef
) -> Array[String]:
	var lines: Array[String] = []
	if active_unit == null or target_unit == null or skill_def == null:
		return lines
	var damage_preview := _build_unit_skill_damage_preview(active_unit, skill_def, cast_variant)
	var damage_preview_text := String(damage_preview.get("summary_text", ""))
	if not damage_preview_text.is_empty():
		lines.append(damage_preview_text)
	return lines


func _build_skill_log_subject_label(
	source_unit: BattleUnitState,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef = null
) -> String:
	var actor_label := source_unit.display_name if source_unit != null and not source_unit.display_name.is_empty() else "未知单位"
	var skill_label := _format_skill_variant_label(skill_def, cast_variant)
	if skill_label.is_empty() and skill_def != null:
		skill_label = skill_def.display_name
	if skill_label.is_empty():
		skill_label = "技能"
	return "%s 使用 %s" % [actor_label, skill_label]


func _handle_unit_skill_command(
	active_unit: BattleUnitState,
	command: BattleCommand,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	batch: BattleEventBatch
) -> bool:
	var validation := _validate_unit_skill_targets(active_unit, command, skill_def, cast_variant)
	if not bool(validation.get("allowed", false)):
		return false

	var target_units := validation.get("target_units", []) as Array
	if target_units.is_empty():
		return false

	if not _consume_skill_costs(active_unit, skill_def, cast_variant, batch):
		return false
	_append_changed_unit_id(batch, active_unit.unit_id)
	var applied := false
	var effect_defs := _collect_unit_skill_effect_defs(skill_def, cast_variant, active_unit)
	var repeat_attack_effect := _repeat_attack_resolver.get_repeat_attack_effect_def(effect_defs)
	for target_unit_variant in target_units:
		var target_unit := target_unit_variant as BattleUnitState
		if target_unit == null:
			continue
		if repeat_attack_effect != null:
			if _repeat_attack_resolver.apply_repeat_attack_skill_result(active_unit, target_unit, skill_def, effect_defs, repeat_attack_effect, batch):
				applied = true
			continue
		if _apply_unit_skill_result(active_unit, target_unit, skill_def, cast_variant, effect_defs, batch):
			applied = true
	return applied


func _should_route_skill_command_to_unit_targeting(skill_def: SkillDef, command: BattleCommand) -> bool:
	return _skill_resolution_rules.should_route_skill_command_to_unit_targeting(
		skill_def,
		_normalize_target_unit_ids(command)
	)


func _validate_unit_skill_targets(
	active_unit: BattleUnitState,
	command: BattleCommand,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef = null
) -> Dictionary:
	var result := {
		"allowed": false,
		"message": "技能或目标无效。",
		"target_unit_ids": [],
		"target_units": [],
		"preview_coords": [],
	}
	if _state == null or active_unit == null or command == null or skill_def == null or skill_def.combat_profile == null:
		return result

	var target_unit_ids := _normalize_target_unit_ids(command)
	var skill_level := _get_unit_skill_level(active_unit, skill_def.skill_id)
	var min_target_count := 1
	var max_target_count := 1
	if _is_multi_unit_skill(skill_def):
		min_target_count = maxi(int(skill_def.combat_profile.min_target_count), 1)
		max_target_count = maxi(int(skill_def.combat_profile.get_effective_max_target_count(skill_level)), min_target_count)
	if target_unit_ids.is_empty():
		return result
	if target_unit_ids.size() < min_target_count:
		result.message = "至少需要选择 %d 个单位目标。" % min_target_count
		return result
	if target_unit_ids.size() > max_target_count:
		result.message = "最多只能选择 %d 个单位目标。" % max_target_count
		return result
	if not _is_multi_unit_skill(skill_def) and target_unit_ids.size() != 1:
		result.message = "当前技能只允许选择 1 个单位目标。"
		return result
	if StringName(skill_def.combat_profile.selection_order_mode) != &"manual":
		target_unit_ids = _sort_target_unit_ids_for_execution(target_unit_ids)

	var target_units: Array = []
	for target_unit_id in target_unit_ids:
		var target_unit := _state.units.get(target_unit_id) as BattleUnitState
		var special_validation_message := _get_unit_skill_target_validation_message(active_unit, target_unit, skill_def, cast_variant)
		if not special_validation_message.is_empty():
			result.message = special_validation_message
			return result
		if target_unit == null or not _can_skill_target_unit(active_unit, target_unit, skill_def):
			result.message = "技能目标超出范围或不满足筛选条件。"
			return result
		target_units.append(target_unit)

	result.allowed = true
	result.message = ""
	result.target_unit_ids = target_unit_ids
	result.target_units = target_units
	var collected_target_coords := _target_collection_service.collect_combat_profile_target_coords(
		_state,
		_grid_service,
		active_unit.coord if active_unit != null else Vector2i(-1, -1),
		skill_def.combat_profile,
		[],
		active_unit,
		target_units,
		skill_level
	)
	result.preview_coords = _sort_coords(collected_target_coords.get("target_coords", []))
	return result


func _normalize_target_unit_ids(command: BattleCommand) -> Array[StringName]:
	var target_unit_ids: Array[StringName] = []
	if command == null:
		return target_unit_ids
	var seen_ids: Dictionary = {}
	var single_target_id := ProgressionDataUtils.to_string_name(command.target_unit_id)
	if single_target_id != &"":
		seen_ids[single_target_id] = true
		target_unit_ids.append(single_target_id)
	for target_unit_id_variant in command.target_unit_ids:
		var target_unit_id := ProgressionDataUtils.to_string_name(target_unit_id_variant)
		if target_unit_id == &"" or seen_ids.has(target_unit_id):
			continue
		seen_ids[target_unit_id] = true
		target_unit_ids.append(target_unit_id)
	return target_unit_ids


func _sort_target_unit_ids_for_execution(target_unit_ids: Array[StringName]) -> Array[StringName]:
	var sorted_ids := target_unit_ids.duplicate()
	if _state == null:
		return sorted_ids
	sorted_ids.sort_custom(func(a: StringName, b: StringName) -> bool:
		var unit_a := _state.units.get(a) as BattleUnitState
		var unit_b := _state.units.get(b) as BattleUnitState
		if unit_a == null or unit_b == null:
			return String(a) < String(b)
		return unit_a.coord.y < unit_b.coord.y \
			or (unit_a.coord.y == unit_b.coord.y and (unit_a.coord.x < unit_b.coord.x \
			or (unit_a.coord.x == unit_b.coord.x and String(a) < String(b))))
	)
	return sorted_ids


func _is_multi_unit_skill(skill_def: SkillDef) -> bool:
	return skill_def != null \
		and skill_def.combat_profile != null \
		and StringName(skill_def.combat_profile.target_selection_mode) == &"multi_unit"


func _can_skill_target_unit(active_unit: BattleUnitState, target_unit: BattleUnitState, skill_def: SkillDef) -> bool:
	if active_unit == null or target_unit == null or skill_def == null or skill_def.combat_profile == null:
		return false
	var costs := _get_effective_skill_costs(active_unit, skill_def)
	if active_unit.current_ap < int(costs.get("ap_cost", skill_def.combat_profile.ap_cost)):
		return false
	if not _is_unit_valid_for_effect(active_unit, target_unit, skill_def.combat_profile.target_team_filter):
		return false
	if not _get_unit_skill_target_validation_message(active_unit, target_unit, skill_def, null).is_empty():
		return false
	active_unit.refresh_footprint()
	target_unit.refresh_footprint()
	return _grid_service.get_distance_between_units(active_unit, target_unit) <= _get_effective_skill_range(active_unit, skill_def)


func _resolve_unit_skill_effect_result(
	active_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	effect_defs: Array[CombatEffectDef]
) -> Dictionary:
	if _should_resolve_unit_skill_as_fate_attack(active_unit, target_unit, skill_def, effect_defs):
		var attack_check := _hit_resolver.build_skill_attack_check(active_unit, target_unit, skill_def)
		var attack_context := {
			"battle_state": _state,
		}
		if _skill_resolution_rules.is_force_hit_no_crit_skill(skill_def):
			attack_context["force_hit_no_crit"] = true
		var result := _damage_resolver.resolve_attack_effects(
			active_unit,
			target_unit,
			effect_defs,
			attack_check,
			attack_context
		)
		if _skill_resolution_rules.is_force_hit_no_crit_skill(skill_def):
			result["custom_log_lines"] = [
				"黑契推进压低了命运摆幅：这次攻击必定命中，且不会触发暴击。",
			]
		return result
	return _damage_resolver.resolve_effects(active_unit, target_unit, effect_defs) if not effect_defs.is_empty() else _damage_resolver.resolve_skill(active_unit, target_unit, skill_def)


func _should_resolve_unit_skill_as_fate_attack(
	active_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	effect_defs: Array[CombatEffectDef]
) -> bool:
	return _skill_resolution_rules.should_resolve_unit_skill_as_fate_attack(
		active_unit,
		target_unit,
		skill_def,
		effect_defs
	)


func _apply_unit_skill_result(
	active_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	effect_defs: Array[CombatEffectDef],
	batch: BattleEventBatch
) -> bool:
	var result := _resolve_unit_skill_effect_result(active_unit, target_unit, skill_def, effect_defs)
	_skill_mastery_service.record_target_result(active_unit, target_unit, skill_def, result, effect_defs)
	_flush_last_stand_mastery_records(batch)
	var guard_mastery_grant := _skill_mastery_service.build_guard_mastery_grant_from_incoming_hit(
		active_unit,
		target_unit,
		effect_defs,
		result,
		_skill_defs
	)
	var shield_roll_context := {}
	var shield_result := _apply_unit_shield_effects(
		active_unit,
		target_unit,
		skill_def,
		effect_defs,
		shield_roll_context
	)
	mark_applied_statuses_for_turn_timing(target_unit, result.get("status_effect_ids", []))
	_append_changed_unit_id(batch, target_unit.unit_id)
	_append_changed_unit_coords(batch, target_unit)
	append_result_source_status_effects(batch, active_unit, result)
	var special_result := _apply_unit_skill_special_effects(active_unit, target_unit, skill_def, cast_variant, effect_defs, batch)
	mark_applied_statuses_for_turn_timing(target_unit, special_result.get("status_effect_ids", []))
	var applied := bool(result.get("applied", false)) \
		or bool(shield_result.get("applied", false)) \
		or bool(special_result.get("applied", false))
	if not applied:
		_append_result_report_entry(batch, result)
		for custom_line_variant in result.get("custom_log_lines", []):
			var custom_line := String(custom_line_variant)
			if not custom_line.is_empty():
				batch.log_lines.append(custom_line)
		for special_line_variant in special_result.get("log_lines", []):
			var special_line := String(special_line_variant)
			if not special_line.is_empty():
				batch.log_lines.append(special_line)
		return false

	var skill_label := _format_skill_variant_label(skill_def, cast_variant)
	var skill_subject := _build_skill_log_subject_label(active_unit, skill_def, cast_variant)
	var damage := int(result.get("damage", 0))
	var healing := int(result.get("healing", 0))
	var moved_steps := int(special_result.get("moved_steps", 0))
	_record_vajra_body_mastery_from_incoming_damage(active_unit, target_unit, skill_def, result, batch)
	if moved_steps > 0:
		batch.log_lines.append("%s 使用 %s，向更安全位置移动 %d 格。" % [
			active_unit.display_name,
			skill_label,
			moved_steps,
		])
	append_damage_result_log_lines(
		batch,
		skill_subject,
		target_unit.display_name,
		result
	)
	_append_result_report_entry(batch, result)
	if _is_doom_sentence_skill(skill_def.skill_id):
		_append_report_entry_to_batch(
			batch,
			_report_formatter.build_skill_event_entry(
				active_unit,
				target_unit,
				skill_def.skill_id,
				BATTLE_REPORT_FORMATTER_SCRIPT.REASON_DOOM_SENTENCE_APPLIED,
				[BATTLE_REPORT_FORMATTER_SCRIPT.TAG_DOOM_SENTENCE]
			)
		)
	if healing > 0:
		batch.log_lines.append("%s 为 %s 恢复 %d 点生命。" % [
			skill_subject,
			target_unit.display_name,
			healing,
		])
	if bool(shield_result.get("applied", false)):
		batch.log_lines.append("%s 使 %s 的护盾值变为 %d。" % [
			skill_subject,
			target_unit.display_name,
			int(shield_result.get("current_shield_hp", 0)),
		])
	for status_id in result.get("status_effect_ids", []):
		batch.log_lines.append("%s 获得状态 %s。" % [target_unit.display_name, String(status_id)])
	for custom_line_variant in result.get("custom_log_lines", []):
		var custom_line := String(custom_line_variant)
		if not custom_line.is_empty():
			batch.log_lines.append(custom_line)
	for special_line_variant in special_result.get("log_lines", []):
		var special_line := String(special_line_variant)
		if not special_line.is_empty():
			batch.log_lines.append(special_line)
	var terrain_effect_ids: Array = result.get("terrain_effect_ids", [])
	if not terrain_effect_ids.is_empty():
		for terrain_effect_id in terrain_effect_ids:
			var target_cell := _grid_service.get_cell(_state, target_unit.coord)
			if target_cell != null and not target_cell.terrain_effect_ids.has(terrain_effect_id):
				target_cell.terrain_effect_ids.append(terrain_effect_id)
				_append_changed_coord(batch, target_unit.coord)
				batch.log_lines.append("%s 使 %s 所在的地格附加效果 %s。" % [
					skill_subject,
					target_unit.display_name,
					String(terrain_effect_id),
				])
	var height_delta := int(result.get("height_delta", 0))
	var target_coord := target_unit.coord
	var target_cell_before := _grid_service.get_cell(_state, target_coord)
	var before_height := int(target_cell_before.current_height) if target_cell_before != null else 0
	if height_delta != 0 and _grid_service.apply_height_delta(_state, target_coord, height_delta):
		_append_changed_coord(batch, target_coord)
		var target_cell_after := _grid_service.get_cell(_state, target_coord)
		var after_height := int(target_cell_after.current_height) if target_cell_after != null else before_height + height_delta
		batch.log_lines.append("%s 使 (%d, %d) 的高度由 %d 变为 %d。" % [
			skill_subject,
			target_coord.x,
			target_coord.y,
			before_height,
			after_height,
		])
	if not target_unit.is_alive:
		_collect_defeated_unit_loot(target_unit, active_unit)
		_clear_defeated_unit(target_unit, batch)
		batch.log_lines.append("%s 被击倒。" % target_unit.display_name)
		_battle_rating_system.record_enemy_defeated_achievement(active_unit, target_unit)
		_record_unit_defeated(target_unit)
	if active_unit != null and target_unit != null:
		_record_effect_metrics(active_unit, target_unit, damage, healing, 1 if not target_unit.is_alive else 0)
	_battle_rating_system.record_skill_effect_result(active_unit, damage, healing, 1 if not target_unit.is_alive else 0)
	_apply_skill_mastery_grant(target_unit, guard_mastery_grant, batch)
	return true


func _apply_unit_skill_special_effects(
	active_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	effect_defs: Array[CombatEffectDef],
	batch: BattleEventBatch
) -> Dictionary:
	var result := {
		"applied": false,
		"moved_steps": 0,
		"status_effect_ids": [],
		"log_lines": [],
	}
	if active_unit == null or skill_def == null:
		return result
	if _is_black_star_brand_skill(skill_def.skill_id):
		return _apply_black_star_brand_effect(active_unit, target_unit)
	if _is_doom_shift_skill(skill_def.skill_id):
		return _apply_doom_shift_effect(active_unit, target_unit, batch)
	if effect_defs.is_empty():
		return result

	for effect_def in effect_defs:
		if effect_def == null:
			continue
		if effect_def.effect_type != &"forced_move":
			continue
		var move_target := target_unit if target_unit != null else active_unit
		var moved_steps := _apply_forced_move_effect(active_unit, move_target, effect_def, batch)
		if moved_steps > 0:
			result["applied"] = true
			result["moved_steps"] = maxi(int(result.get("moved_steps", 0)), moved_steps)
	return result


func _apply_doom_shift_effect(
	active_unit: BattleUnitState,
	target_unit: BattleUnitState,
	batch: BattleEventBatch
) -> Dictionary:
	var result := {
		"applied": false,
		"moved_steps": 0,
		"status_effect_ids": [],
		"log_lines": [],
	}
	if _state == null or active_unit == null or target_unit == null:
		return result
	if target_unit.unit_id == active_unit.unit_id:
		return result
	if not _swap_unit_positions(active_unit, target_unit, batch):
		return result
	_set_runtime_status_effect(
		active_unit,
		STATUS_MARKED,
		DOOM_SHIFT_SELF_DEBUFF_DURATION_TU,
		active_unit.unit_id,
		1,
		{"counts_as_debuff": true}
	)
	_append_changed_unit_id(batch, active_unit.unit_id)
	result["applied"] = true
	result["log_lines"] = [
		"%s 先承受 marked，再与 %s 交换位置。" % [active_unit.display_name, target_unit.display_name],
	]
	return result


func _swap_unit_positions(
	first_unit: BattleUnitState,
	second_unit: BattleUnitState,
	batch: BattleEventBatch
) -> bool:
	if _state == null or first_unit == null or second_unit == null:
		return false
	if first_unit.unit_id == second_unit.unit_id:
		return false
	var first_previous_coords := first_unit.occupied_coords.duplicate()
	var second_previous_coords := second_unit.occupied_coords.duplicate()
	var first_coord := first_unit.coord
	var second_coord := second_unit.coord
	_grid_service.clear_unit_occupancy(_state, first_unit)
	_grid_service.clear_unit_occupancy(_state, second_unit)
	var can_swap := _grid_service.can_place_unit(_state, first_unit, second_coord, true) \
		and _grid_service.can_place_unit(_state, second_unit, first_coord, true)
	if not can_swap:
		_grid_service.set_occupants(_state, first_previous_coords, first_unit.unit_id)
		_grid_service.set_occupants(_state, second_previous_coords, second_unit.unit_id)
		return false
	_grid_service.place_unit(_state, first_unit, second_coord, true)
	_grid_service.place_unit(_state, second_unit, first_coord, true)
	_append_changed_coords(batch, first_previous_coords)
	_append_changed_coords(batch, second_previous_coords)
	_append_changed_unit_coords(batch, first_unit)
	_append_changed_unit_coords(batch, second_unit)
	_append_changed_unit_id(batch, first_unit.unit_id)
	_append_changed_unit_id(batch, second_unit.unit_id)
	return true


func _apply_black_star_brand_effect(
	active_unit: BattleUnitState,
	target_unit: BattleUnitState
) -> Dictionary:
	var result := {
		"applied": false,
		"moved_steps": 0,
		"status_effect_ids": [],
		"log_lines": [],
	}
	if active_unit == null or target_unit == null:
		return result
	_clear_black_star_brand_statuses(target_unit)
	if _is_black_star_brand_elite_target(target_unit):
		_set_runtime_status_effect(target_unit, STATUS_BLACK_STAR_BRAND_ELITE, BLACK_STAR_BRAND_DURATION_TU, active_unit.unit_id)
		_set_runtime_status_effect(
			target_unit,
			STATUS_BLACK_STAR_BRAND_ELITE_GUARD_WINDOW,
			BLACK_STAR_BRAND_DURATION_TU,
			active_unit.unit_id
		)
		result["status_effect_ids"] = [STATUS_BLACK_STAR_BRAND_ELITE, STATUS_BLACK_STAR_BRAND_ELITE_GUARD_WINDOW]
		result["log_lines"] = [
			"%s 被施加黑星烙印：暴击失效、命中下降，且第一次受击会被穿透部分格挡。" % target_unit.display_name,
		]
	else:
		_set_runtime_status_effect(target_unit, STATUS_BLACK_STAR_BRAND_NORMAL, BLACK_STAR_BRAND_DURATION_TU, active_unit.unit_id)
		target_unit.erase_status_effect(STATUS_GUARDING)
		result["status_effect_ids"] = [STATUS_BLACK_STAR_BRAND_NORMAL]
		result["log_lines"] = [
			"%s 被施加黑星烙印：无法反击，且无法进入格挡。" % target_unit.display_name,
		]
	result["applied"] = true
	return result


func _set_runtime_status_effect(
	unit_state: BattleUnitState,
	status_id: StringName,
	duration_tu: int,
	source_unit_id: StringName = &"",
	power: int = 1,
	params: Dictionary = {}
) -> void:
	if unit_state == null or status_id == &"":
		return
	var status_entry := BattleStatusEffectState.new()
	status_entry.status_id = status_id
	status_entry.source_unit_id = source_unit_id
	status_entry.power = maxi(power, 1)
	status_entry.stacks = 1
	status_entry.duration = maxi(duration_tu, -1)
	status_entry.params = params.duplicate(true)
	unit_state.set_status_effect(status_entry)


func _clear_black_star_brand_statuses(unit_state: BattleUnitState) -> void:
	if unit_state == null:
		return
	unit_state.erase_status_effect(STATUS_BLACK_STAR_BRAND_NORMAL)
	unit_state.erase_status_effect(STATUS_BLACK_STAR_BRAND_ELITE)
	unit_state.erase_status_effect(STATUS_BLACK_STAR_BRAND_ELITE_GUARD_WINDOW)


func _is_black_star_brand_elite_target(unit_state: BattleUnitState) -> bool:
	return _is_elite_or_boss_target(unit_state)


func _is_elite_or_boss_target(unit_state: BattleUnitState) -> bool:
	return unit_state != null \
		and unit_state.attribute_snapshot != null \
		and int(unit_state.attribute_snapshot.get_value(FORTUNE_MARK_TARGET_STAT_ID)) > 0


func _is_boss_target(unit_state: BattleUnitState) -> bool:
	return unit_state != null \
		and unit_state.attribute_snapshot != null \
		and (
			int(unit_state.attribute_snapshot.get_value(BOSS_TARGET_STAT_ID)) > 0
			or int(unit_state.attribute_snapshot.get_value(FORTUNE_MARK_TARGET_STAT_ID)) > 1
		)


func _is_black_star_brand_skill(skill_id: StringName) -> bool:
	return ProgressionDataUtils.to_string_name(skill_id) == BLACK_STAR_BRAND_SKILL_ID


func _is_black_contract_push_skill(skill_id: StringName) -> bool:
	return ProgressionDataUtils.to_string_name(skill_id) == BLACK_CONTRACT_PUSH_SKILL_ID


func _is_doom_shift_skill(skill_id: StringName) -> bool:
	return ProgressionDataUtils.to_string_name(skill_id) == DOOM_SHIFT_SKILL_ID


func _is_black_crown_seal_skill(skill_id: StringName) -> bool:
	return ProgressionDataUtils.to_string_name(skill_id) == BLACK_CROWN_SEAL_SKILL_ID


func _clear_crown_break_seal_statuses(unit_state: BattleUnitState) -> void:
	if unit_state == null:
		return
	unit_state.erase_status_effect(STATUS_CROWN_BREAK_BROKEN_FANG)
	unit_state.erase_status_effect(STATUS_CROWN_BREAK_BROKEN_HAND)
	unit_state.erase_status_effect(STATUS_CROWN_BREAK_BLINDED_EYE)


func _is_crown_break_target_eligible(active_unit: BattleUnitState, target_unit: BattleUnitState) -> bool:
	return target_unit != null \
		and _is_unit_valid_for_effect(active_unit, target_unit, &"enemy") \
		and target_unit.has_status_effect(STATUS_BLACK_STAR_BRAND_ELITE)


func _is_crown_break_skill(skill_id: StringName) -> bool:
	return ProgressionDataUtils.to_string_name(skill_id) == CROWN_BREAK_SKILL_ID


func _is_doom_sentence_target_eligible(active_unit: BattleUnitState, target_unit: BattleUnitState) -> bool:
	return target_unit != null \
		and _is_unit_valid_for_effect(active_unit, target_unit, &"enemy") \
		and _is_elite_or_boss_target(target_unit)


func _is_black_crown_seal_target_eligible(active_unit: BattleUnitState, target_unit: BattleUnitState) -> bool:
	return target_unit != null \
		and _is_unit_valid_for_effect(active_unit, target_unit, &"enemy") \
		and _is_boss_target(target_unit)


func _is_doom_sentence_skill(skill_id: StringName) -> bool:
	return ProgressionDataUtils.to_string_name(skill_id) == DOOM_SENTENCE_SKILL_ID


func _get_unit_skill_target_validation_message(
	active_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef = null
) -> String:
	if _is_black_crown_seal_skill(skill_def.skill_id) and not _is_black_crown_seal_target_eligible(active_unit, target_unit):
		return "黑冠封印只能对 boss 施放。"
	if _is_doom_shift_skill(skill_def.skill_id):
		if target_unit == null or active_unit == null:
			return "断命换位的目标无效。"
		if target_unit.unit_id == active_unit.unit_id:
			return "断命换位不能以自己为目标。"
	if _is_crown_break_skill(skill_def.skill_id) and not _is_crown_break_target_eligible(active_unit, target_unit):
		return "折冠只能对已被黑星烙印的 elite / boss 施放。"
	if _is_doom_sentence_skill(skill_def.skill_id) and not _is_doom_sentence_target_eligible(active_unit, target_unit):
		return "厄命宣判只能对 elite / boss 施放。"
	return ""


func _skill_grants_guarding(skill_def: SkillDef) -> bool:
	if skill_def == null or skill_def.combat_profile == null:
		return false
	for effect_def_variant in _collect_unit_skill_effect_defs(skill_def, null):
		var effect_def := effect_def_variant as CombatEffectDef
		if effect_def == null:
			continue
		if effect_def.effect_type in [&"status", &"apply_status"] and effect_def.status_id == STATUS_GUARDING:
			return true
	for cast_variant in skill_def.combat_profile.cast_variants:
		if cast_variant == null:
			continue
		for effect_def_variant in cast_variant.effect_defs:
			var effect_def := effect_def_variant as CombatEffectDef
			if effect_def == null:
				continue
			if effect_def.effect_type in [&"status", &"apply_status"] and effect_def.status_id == STATUS_GUARDING:
				return true
	return false


func _apply_forced_move_effect(
	source_unit: BattleUnitState,
	unit_state: BattleUnitState,
	effect_def: CombatEffectDef,
	batch: BattleEventBatch
) -> int:
	if _state == null or unit_state == null or effect_def == null:
		return 0
	var move_distance := maxi(int(effect_def.forced_move_distance), 0)
	if move_distance <= 0:
		return 0
	if _blocks_enemy_forced_move(source_unit, unit_state):
		if batch != null:
			batch.log_lines.append("%s 稳如金刚，未被强制位移。" % unit_state.display_name)
		return 0

	var mode := effect_def.forced_move_mode
	if mode == &"":
		return 0
	if mode == &"jump":
		# 跳跃位移由 _apply_ground_jump_relocation 在 precast 阶段处理（含 can_jump_arc 校验）；
		# 这里不做逐格推动，避免落地后再被推一格。
		return 0

	var moved_steps := 0
	for _step in range(move_distance):
		var next_coord := _pick_forced_move_coord(unit_state, mode)
		if next_coord == Vector2i(-1, -1) or next_coord == unit_state.coord:
			break
		if not _grid_service.can_traverse(_state, unit_state.coord, next_coord, unit_state):
			break
		var previous_coords := unit_state.occupied_coords.duplicate()
		if not _grid_service.move_unit(_state, unit_state, next_coord):
			break
		moved_steps += 1
		_append_changed_coords(batch, previous_coords)
		_append_changed_unit_coords(batch, unit_state)
		_append_changed_unit_id(batch, unit_state.unit_id)
	return moved_steps


func _blocks_enemy_forced_move(source_unit: BattleUnitState, target_unit: BattleUnitState) -> bool:
	if source_unit == null or target_unit == null:
		return false
	if source_unit.unit_id == target_unit.unit_id:
		return false
	if String(source_unit.faction_id) == String(target_unit.faction_id):
		return false
	var status_entry = target_unit.get_status_effect(STATUS_VAJRA_BODY)
	if status_entry == null or status_entry.params == null:
		return false
	return bool(status_entry.params.get("forced_move_immune", false))


func _record_vajra_body_mastery_from_incoming_damage(
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	result: Dictionary,
	batch: BattleEventBatch = null
) -> void:
	var grant := _skill_mastery_service.build_vajra_body_mastery_grant(
		source_unit,
		target_unit,
		skill_def,
		result,
		_skill_defs
	)
	_apply_skill_mastery_grant(target_unit, grant, batch)


func _pick_forced_move_coord(unit_state: BattleUnitState, mode: StringName) -> Vector2i:
	if _state == null or unit_state == null:
		return Vector2i(-1, -1)
	unit_state.refresh_footprint()
	var best_coord := Vector2i(-1, -1)
	var best_score := -999999
	for direction in [Vector2i.UP, Vector2i.LEFT, Vector2i.RIGHT, Vector2i.DOWN]:
		var candidate_coord: Vector2i = unit_state.coord + direction
		if not _grid_service.can_traverse(_state, unit_state.coord, candidate_coord, unit_state):
			continue
		var candidate_score := _score_forced_move_coord(unit_state, candidate_coord, mode)
		if candidate_score > best_score or (candidate_score == best_score and (best_coord == Vector2i(-1, -1) or candidate_coord.y < best_coord.y or (candidate_coord.y == best_coord.y and candidate_coord.x < best_coord.x))):
			best_score = candidate_score
			best_coord = candidate_coord
	return best_coord


func _score_forced_move_coord(unit_state: BattleUnitState, candidate_coord: Vector2i, mode: StringName) -> int:
	if _state == null or unit_state == null:
		return -999999
	var hostile_units := _collect_hostile_units_for(unit_state)
	var closest_hostile_distance := 0
	if not hostile_units.is_empty():
		closest_hostile_distance = 999999
		for hostile_unit in hostile_units:
			closest_hostile_distance = mini(closest_hostile_distance, _grid_service.get_distance(candidate_coord, hostile_unit.coord))
	var score := closest_hostile_distance * 100
	score -= _grid_service.get_distance(unit_state.coord, candidate_coord) * 10
	score -= candidate_coord.y * 2 + candidate_coord.x
	if mode == &"evasive":
		score += 5
	return score


func _collect_hostile_units_for(unit_state: BattleUnitState) -> Array[BattleUnitState]:
	var hostile_units: Array[BattleUnitState] = []
	if _state == null or unit_state == null:
		return hostile_units
	for other_unit_variant in _state.units.values():
		var other_unit := other_unit_variant as BattleUnitState
		if other_unit == null or other_unit.unit_id == unit_state.unit_id or not other_unit.is_alive:
			continue
		if String(other_unit.faction_id) == String(unit_state.faction_id):
			continue
		hostile_units.append(other_unit)
	return hostile_units


func _collect_unit_skill_effect_defs(
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	active_unit: BattleUnitState = null
) -> Array[CombatEffectDef]:
	return _skill_resolution_rules.collect_unit_skill_effect_defs(skill_def, cast_variant, active_unit)


func _handle_ground_skill_command(
	active_unit: BattleUnitState,
	command: BattleCommand,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	batch: BattleEventBatch
) -> bool:
	var validation := _validate_ground_skill_command(active_unit, skill_def, cast_variant, command)
	if not bool(validation.get("allowed", false)):
		return false

	var target_coords: Array[Vector2i] = []
	for target_coord_variant in validation.get("target_coords", []):
		if target_coord_variant is Vector2i:
			target_coords.append(target_coord_variant)
	var precast_validation_message := _get_ground_special_effect_validation_message(
		active_unit,
		skill_def,
		cast_variant,
		target_coords
	)
	if not precast_validation_message.is_empty():
		batch.log_lines.append(precast_validation_message)
		return false

	if not _consume_skill_costs(active_unit, skill_def, null, batch):
		return false
	_append_changed_unit_id(batch, active_unit.unit_id)
	if _charge_resolver.is_charge_variant(cast_variant):
		return _charge_resolver.handle_charge_skill_command(active_unit, skill_def, cast_variant, validation, batch)
	if not _apply_ground_precast_special_effects(active_unit, skill_def, cast_variant, target_coords, batch):
		return false

	var effect_coords := _build_ground_effect_coords(skill_def, target_coords, active_unit.coord if active_unit != null else Vector2i(-1, -1), active_unit)
	var unit_result := _apply_ground_unit_effects(
		active_unit,
		skill_def,
		_collect_ground_unit_effect_defs(skill_def, cast_variant, active_unit),
		effect_coords,
		batch
	)
	var terrain_result := _apply_ground_terrain_effects(
		active_unit,
		skill_def,
		_collect_ground_terrain_effect_defs(skill_def, cast_variant, active_unit),
		effect_coords,
		batch
	)
	var applied := bool(unit_result.get("applied", false)) or bool(terrain_result.get("applied", false))

	if applied:
		batch.log_lines.append("%s 使用 %s，影响了 %d 个地格、%d 个单位。" % [
			active_unit.display_name,
			_format_skill_variant_label(skill_def, cast_variant),
			effect_coords.size(),
			int(unit_result.get("affected_unit_count", 0)),
		])
	return applied


func _apply_ground_precast_special_effects(
	active_unit: BattleUnitState,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	target_coords: Array[Vector2i],
	batch: BattleEventBatch
) -> bool:
	if _get_ground_jump_effect_def(skill_def, cast_variant) == null:
		return true
	return _apply_ground_jump_relocation(active_unit, target_coords, batch)


func _apply_ground_jump_relocation(
	active_unit: BattleUnitState,
	target_coords: Array[Vector2i],
	batch: BattleEventBatch
) -> bool:
	if _state == null or active_unit == null or target_coords.is_empty():
		return false

	var landing_coord := target_coords[0]
	if active_unit.coord == landing_coord:
		return true

	var previous_anchor := active_unit.coord
	var previous_coords := active_unit.occupied_coords.duplicate()
	if not _grid_service.move_unit_force(_state, active_unit, landing_coord):
		return false

	_append_changed_coords(batch, previous_coords)
	_append_changed_unit_coords(batch, active_unit)
	_append_changed_unit_id(batch, active_unit.unit_id)
	batch.log_lines.append("%s 从 (%d, %d) 跳至 (%d, %d)。" % [
		active_unit.display_name,
		previous_anchor.x,
		previous_anchor.y,
		landing_coord.x,
		landing_coord.y,
	])
	return true


func _get_ground_jump_effect_def(skill_def: SkillDef, cast_variant: CombatCastVariantDef) -> CombatEffectDef:
	if cast_variant != null:
		for effect_def in cast_variant.effect_defs:
			if _is_ground_jump_effect(effect_def):
				return effect_def
	if skill_def != null and skill_def.combat_profile != null:
		for effect_def in skill_def.combat_profile.effect_defs:
			if _is_ground_jump_effect(effect_def):
				return effect_def
	return null


func _is_ground_jump_effect(effect_def: CombatEffectDef) -> bool:
	return effect_def != null \
		and effect_def.effect_type == &"forced_move" \
		and _get_effect_forced_move_mode(effect_def) == &"jump"


func _get_effect_forced_move_mode(effect_def: CombatEffectDef) -> StringName:
	if effect_def == null:
		return &""
	if effect_def.forced_move_mode != &"":
		return effect_def.forced_move_mode
	return &""


func _build_ground_effect_coords(
	skill_def: SkillDef,
	target_coords: Array,
	source_coord: Vector2i = Vector2i(-1, -1),
	active_unit: BattleUnitState = null
) -> Array[Vector2i]:
	var normalized_target_coords: Array[Vector2i] = []
	for target_coord in target_coords:
		normalized_target_coords.append(target_coord)
	if _state == null or skill_def == null or skill_def.combat_profile == null:
		return _sort_coords(normalized_target_coords)
	var skill_level := _get_unit_skill_level(active_unit, skill_def.skill_id)
	var collected_target_coords := _target_collection_service.collect_combat_profile_target_coords(
		_state,
		_grid_service,
		source_coord,
		skill_def.combat_profile,
		normalized_target_coords,
		null,
		[],
		skill_level
	)
	if bool(collected_target_coords.get("handled", false)):
		return _sort_coords(collected_target_coords.get("target_coords", []))
	return _sort_coords(normalized_target_coords)

func _collect_ground_unit_effect_defs(
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	active_unit: BattleUnitState = null
) -> Array[CombatEffectDef]:
	return _skill_resolution_rules.collect_ground_unit_effect_defs(skill_def, cast_variant, active_unit)


func _collect_ground_terrain_effect_defs(
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	active_unit: BattleUnitState = null
) -> Array[CombatEffectDef]:
	return _skill_resolution_rules.collect_ground_terrain_effect_defs(skill_def, cast_variant, active_unit)


func _collect_ground_effect_defs(
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	active_unit: BattleUnitState = null
) -> Array[CombatEffectDef]:
	return _skill_resolution_rules.collect_ground_effect_defs(skill_def, cast_variant, active_unit)


func _collect_ground_preview_unit_ids(
	source_unit: BattleUnitState,
	skill_def: SkillDef,
	effect_defs: Array[CombatEffectDef],
	effect_coords: Array[Vector2i]
) -> Array[StringName]:
	var target_unit_ids: Array[StringName] = []
	for target_unit in _collect_units_in_coords(effect_coords):
		for effect_def in effect_defs:
			if _is_unit_valid_for_effect(source_unit, target_unit, _resolve_effect_target_filter(skill_def, effect_def)):
				target_unit_ids.append(target_unit.unit_id)
				break
	return target_unit_ids


func _apply_ground_unit_effects(
	source_unit: BattleUnitState,
	skill_def: SkillDef,
	effect_defs: Array[CombatEffectDef],
	effect_coords: Array[Vector2i],
	batch: BattleEventBatch
) -> Dictionary:
	var applied := false
	var total_damage := 0
	var total_healing := 0
	var total_kill_count := 0
	var affected_unit_count := 0
	var shield_roll_context := {}

	for target_unit in _collect_units_in_coords(effect_coords):
		var applicable_effects: Array[CombatEffectDef] = []
		for effect_def in effect_defs:
			if _is_unit_valid_for_effect(source_unit, target_unit, _resolve_effect_target_filter(skill_def, effect_def)):
				applicable_effects.append(effect_def)
		if applicable_effects.is_empty():
			continue

		var result := _resolve_ground_unit_effect_result(source_unit, target_unit, skill_def, applicable_effects)
		_skill_mastery_service.record_target_result(source_unit, target_unit, skill_def, result, applicable_effects)
		var shield_result := _apply_unit_shield_effects(
			source_unit,
			target_unit,
			skill_def,
			applicable_effects,
			shield_roll_context
		)
		_record_vajra_body_mastery_from_incoming_damage(source_unit, target_unit, skill_def, result, batch)
		mark_applied_statuses_for_turn_timing(target_unit, result.get("status_effect_ids", []))
		var attack_resolved := result.has("attack_success")
		var attack_hit := attack_resolved and bool(result.get("attack_success", false))
		var unit_applied := bool(result.get("applied", false)) or bool(shield_result.get("applied", false)) or attack_hit
		if not unit_applied:
			if attack_resolved:
				_append_result_report_entry(batch, result)
			continue

		applied = true
		affected_unit_count += 1
		_append_changed_unit_id(batch, source_unit.unit_id if source_unit != null else &"")
		_append_changed_unit_id(batch, target_unit.unit_id)
		_append_changed_unit_coords(batch, target_unit)
		append_result_source_status_effects(batch, source_unit, result)

		var damage := int(result.get("damage", 0))
		var healing := int(result.get("healing", 0))
		total_damage += damage
		total_healing += healing
		append_damage_result_log_lines(
			batch,
			_build_skill_log_subject_label(source_unit, skill_def),
			target_unit.display_name,
			result
		)
		if attack_resolved and not bool(result.get("applied", false)):
			_append_result_report_entry(batch, result)
		if healing > 0:
			batch.log_lines.append("%s 为 %s 恢复 %d 点生命。" % [
				_build_skill_log_subject_label(source_unit, skill_def),
				target_unit.display_name,
				healing,
			])
		if bool(shield_result.get("applied", false)):
			batch.log_lines.append("%s 使 %s 的护盾值变为 %d。" % [
				_build_skill_log_subject_label(source_unit, skill_def),
				target_unit.display_name,
				int(shield_result.get("current_shield_hp", 0)),
			])
		for status_id in result.get("status_effect_ids", []):
			batch.log_lines.append("%s 获得状态 %s。" % [target_unit.display_name, String(status_id)])

		if not target_unit.is_alive:
			total_kill_count += 1
			_collect_defeated_unit_loot(target_unit, source_unit)
			_clear_defeated_unit(target_unit, batch)
			batch.log_lines.append("%s 被击倒。" % target_unit.display_name)
			_battle_rating_system.record_enemy_defeated_achievement(source_unit, target_unit)
			_record_unit_defeated(target_unit)
		if source_unit != null and target_unit != null:
			_record_effect_metrics(source_unit, target_unit, damage, healing, 1 if not target_unit.is_alive else 0)

	_flush_last_stand_mastery_records(batch)
	if applied and source_unit != null:
		_battle_rating_system.record_skill_effect_result(source_unit, total_damage, total_healing, total_kill_count)
	return {
		"applied": applied,
		"affected_unit_count": affected_unit_count,
		"damage": total_damage,
		"healing": total_healing,
		"kill_count": total_kill_count,
	}


func _resolve_ground_unit_effect_result(
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	effect_defs: Array[CombatEffectDef]
) -> Dictionary:
	if _should_resolve_ground_effects_as_attack(effect_defs):
		var attack_effect_defs := _dedupe_effect_defs_by_instance(effect_defs)
		var attack_check := _hit_resolver.build_skill_attack_check(source_unit, target_unit, skill_def)
		return _damage_resolver.resolve_attack_effects(
			source_unit,
			target_unit,
			attack_effect_defs,
			attack_check,
			{"battle_state": _state}
		)
	return _damage_resolver.resolve_effects(source_unit, target_unit, effect_defs)


func _should_resolve_ground_effects_as_attack(effect_defs: Array[CombatEffectDef]) -> bool:
	for effect_def in effect_defs:
		if effect_def == null or effect_def.params == null:
			continue
		if bool(effect_def.params.get("resolve_as_weapon_attack", false)):
			return true
	return false


func _dedupe_effect_defs_by_instance(effect_defs: Array[CombatEffectDef]) -> Array[CombatEffectDef]:
	var deduped: Array[CombatEffectDef] = []
	var seen: Dictionary = {}
	for effect_def in effect_defs:
		if effect_def == null:
			continue
		var instance_id := effect_def.get_instance_id()
		if seen.has(instance_id):
			continue
		seen[instance_id] = true
		deduped.append(effect_def)
	return deduped


func _apply_ground_terrain_effects(
	source_unit: BattleUnitState,
	skill_def: SkillDef,
	effect_defs: Array[CombatEffectDef],
	effect_coords: Array[Vector2i],
	batch: BattleEventBatch
) -> Dictionary:
	var applied := false
	var requires_topology_reconcile := false

	for effect_def in effect_defs:
		if effect_def == null:
			continue
		match effect_def.effect_type:
			&"terrain", &"terrain_replace", &"terrain_replace_to", &"height", &"height_delta":
				requires_topology_reconcile = true
				for effect_coord in effect_coords:
					if _apply_ground_cell_effect(source_unit, skill_def, effect_coord, effect_def, batch):
						applied = true
			&"terrain_effect":
				if effect_def.duration_tu > 0 and effect_def.tick_interval_tu > 0:
					var field_instance_id := _build_terrain_effect_instance_id(effect_def.terrain_effect_id)
					var applied_coord_count := 0
					for effect_coord in effect_coords:
						if _terrain_effect_system.upsert_timed_terrain_effect(effect_coord, source_unit, skill_def, effect_def, field_instance_id):
							applied = true
							applied_coord_count += 1
							_append_changed_coord(batch, effect_coord)
					if applied_coord_count > 0:
						batch.log_lines.append("%s 在 %d 个地格留下 %s。" % [
							_build_skill_log_subject_label(source_unit, skill_def),
							applied_coord_count,
							_get_terrain_effect_display_name(effect_def),
						])
				elif effect_def.terrain_effect_id != &"":
					var tagged_coord_count := 0
					for effect_coord in effect_coords:
						var cell := _grid_service.get_cell(_state, effect_coord)
						if cell == null or cell.terrain_effect_ids.has(effect_def.terrain_effect_id):
							continue
						cell.terrain_effect_ids.append(effect_def.terrain_effect_id)
						_append_changed_coord(batch, effect_coord)
						tagged_coord_count += 1
						applied = true
					if tagged_coord_count > 0:
						batch.log_lines.append("%s 使 %d 个地格附加效果 %s。" % [
							_build_skill_log_subject_label(source_unit, skill_def),
							tagged_coord_count,
							_get_terrain_effect_display_name(effect_def),
						])
			_:
				pass

	if requires_topology_reconcile and _reconcile_water_topology(effect_coords, batch):
		applied = true
	return {"applied": applied}


func _apply_ground_cell_effect(
	source_unit: BattleUnitState,
	skill_def: SkillDef,
	target_coord: Vector2i,
	effect_def: CombatEffectDef,
	batch: BattleEventBatch
) -> bool:
	var cell := _grid_service.get_cell(_state, target_coord)
	if cell == null:
		return false

	var cell_applied := false
	var before_terrain := cell.base_terrain
	var before_height := int(cell.current_height)
	var occupant_unit := _state.units.get(cell.occupant_unit_id) as BattleUnitState if cell.occupant_unit_id != &"" else null

	match effect_def.effect_type:
		&"terrain", &"terrain_replace", &"terrain_replace_to":
			if effect_def.terrain_replace_to != &"" and cell.base_terrain != effect_def.terrain_replace_to:
				if _grid_service.set_base_terrain(_state, target_coord, effect_def.terrain_replace_to):
					cell_applied = true
		&"height", &"height_delta":
			if effect_def.height_delta != 0:
				var height_result := _grid_service.apply_height_delta_result(_state, target_coord, int(effect_def.height_delta))
				if bool(height_result.get("changed", false)):
					cell_applied = true
		_:
			pass

	var after_height := int(cell.current_height)
	if before_terrain != cell.base_terrain or before_height != after_height:
		_append_changed_coord(batch, target_coord)
	if before_terrain != cell.base_terrain:
		batch.log_lines.append("%s 使 (%d, %d) 的地形由 %s 变为 %s。" % [
			_build_skill_log_subject_label(source_unit, skill_def),
			target_coord.x,
			target_coord.y,
			_grid_service.get_terrain_display_name(String(before_terrain)),
			_grid_service.get_terrain_display_name(String(cell.base_terrain)),
		])
	if before_height != after_height:
		batch.log_lines.append("%s 使 (%d, %d) 的高度由 %d 变为 %d。" % [
			_build_skill_log_subject_label(source_unit, skill_def),
			target_coord.x,
			target_coord.y,
			before_height,
			after_height,
		])

	if occupant_unit != null and occupant_unit.is_alive and after_height < before_height:
		var fall_layers := before_height - after_height
		var fall_result := _damage_resolver.resolve_fall_damage(occupant_unit, fall_layers)
		var fall_damage := int(fall_result.get("damage", 0))
		var shield_absorbed := int(fall_result.get("shield_absorbed", 0))
		if fall_damage > 0 or shield_absorbed > 0:
			cell_applied = true
			_append_changed_coord(batch, target_coord)
			_append_changed_unit_id(batch, occupant_unit.unit_id)
			if fall_damage > 0:
				batch.log_lines.append("%s 使 (%d, %d) 的高度下降 %d 层，导致 %s 坠落并受到 %d 点伤害。" % [
					_build_skill_log_subject_label(source_unit, skill_def),
					target_coord.x,
					target_coord.y,
					fall_layers,
					occupant_unit.display_name,
					fall_damage,
				])
				if shield_absorbed > 0:
					batch.log_lines.append("%s 的护盾吸收了 %d 点坠落伤害。" % [
						occupant_unit.display_name,
						shield_absorbed,
					])
			else:
				batch.log_lines.append("%s 使 (%d, %d) 的高度下降 %d 层，导致 %s 坠落，但被护盾吸收了 %d 点坠落伤害。" % [
					_build_skill_log_subject_label(source_unit, skill_def),
					target_coord.x,
					target_coord.y,
					fall_layers,
					occupant_unit.display_name,
					shield_absorbed,
				])
			if bool(fall_result.get("shield_broken", false)):
				batch.log_lines.append("%s 的护盾被击碎。" % occupant_unit.display_name)
			if not occupant_unit.is_alive:
				_collect_defeated_unit_loot(occupant_unit, source_unit)
				_clear_defeated_unit(occupant_unit, batch)
				batch.log_lines.append("%s 被击倒。" % occupant_unit.display_name)

	_flush_last_stand_mastery_records(batch)
	return cell_applied


func _reconcile_water_topology(effect_coords: Array[Vector2i], batch: BattleEventBatch) -> bool:
	if _state == null or _state.map_size == Vector2i.ZERO or effect_coords.is_empty():
		return false

	var changes: Array[Dictionary] = _terrain_topology_service.reclassify_water_terrain_near_coords(
		_state.cells,
		_state.map_size,
		effect_coords
	)
	var applied := false
	for change in changes:
		var coord: Vector2i = change.get("coord", Vector2i.ZERO)
		var cell: BattleCellState = _grid_service.get_cell(_state, coord)
		if cell == null:
			continue
		var before_terrain: StringName = cell.base_terrain
		var before_flow_direction: Vector2i = cell.flow_direction
		var after_terrain: StringName = change.get("after_terrain", before_terrain)
		var after_flow_direction: Vector2i = change.get("after_flow_direction", before_flow_direction)
		if before_terrain != after_terrain:
			_grid_service.set_base_terrain(_state, coord, after_terrain)
			cell = _grid_service.get_cell(_state, coord)
			if cell == null:
				continue
		if cell.flow_direction != after_flow_direction:
			cell.flow_direction = after_flow_direction
			_grid_service.recalculate_cell(cell)
			_grid_service.sync_column_from_surface_cell(_state, coord)
		if before_terrain != cell.base_terrain or before_flow_direction != cell.flow_direction:
			applied = true
			_append_changed_coord(batch, coord)
		if before_terrain != cell.base_terrain:
			batch.log_lines.append("相邻水域在 (%d, %d) 重分类为 %s。" % [
				coord.x,
				coord.y,
				_grid_service.get_terrain_display_name(String(cell.base_terrain)),
			])
	return applied


func _collect_units_in_coords(effect_coords: Array[Vector2i]) -> Array[BattleUnitState]:
	var units: Array[BattleUnitState] = []
	var seen_unit_ids: Dictionary = {}
	for effect_coord in effect_coords:
		var target_unit := _grid_service.get_unit_at_coord(_state, effect_coord)
		if target_unit == null or not target_unit.is_alive or seen_unit_ids.has(target_unit.unit_id):
			continue
		seen_unit_ids[target_unit.unit_id] = true
		units.append(target_unit)
	return units


func _is_unit_effect(effect_def: CombatEffectDef) -> bool:
	if effect_def == null:
		return false
	return effect_def.effect_type == &"damage" \
		or effect_def.effect_type == &"heal" \
		or effect_def.effect_type == &"shield" \
		or effect_def.effect_type == &"status" \
		or effect_def.effect_type == &"apply_status"


func _apply_unit_shield_effects(
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	effect_defs: Array[CombatEffectDef],
	shield_roll_context: Dictionary = {}
) -> Dictionary:
	var result := {
		"applied": false,
		"current_shield_hp": 0,
		"shield_max_hp": 0,
		"shield_duration": -1,
		"shield_family": &"",
	}
	if target_unit == null or effect_defs.is_empty():
		return result

	for effect_def in effect_defs:
		if effect_def == null or effect_def.effect_type != &"shield":
			continue
		var shield_apply_result := _apply_shield_effect_to_target(
			source_unit,
			target_unit,
			skill_def,
			effect_def,
			shield_roll_context
		)
		if not bool(shield_apply_result.get("applied", false)):
			continue
		result = shield_apply_result
	return result


func _apply_shield_effect_to_target(
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	effect_def: CombatEffectDef,
	shield_roll_context: Dictionary = {}
) -> Dictionary:
	var result := {
		"applied": false,
		"current_shield_hp": int(target_unit.current_shield_hp) if target_unit != null else 0,
		"shield_max_hp": int(target_unit.shield_max_hp) if target_unit != null else 0,
		"shield_duration": int(target_unit.shield_duration) if target_unit != null else -1,
		"shield_family": target_unit.shield_family if target_unit != null else &"",
	}
	if target_unit == null or effect_def == null:
		return result

	var shield_hp := _resolve_shield_hp(effect_def, shield_roll_context)
	if shield_hp <= 0:
		return result
	var shield_duration := _resolve_shield_duration_tu(effect_def)
	if shield_duration <= 0:
		return result
	var shield_family := _resolve_shield_family(skill_def, effect_def)
	var shield_params := effect_def.params.duplicate(true) if effect_def.params != null else {}
	shield_params["resolved_shield_hp"] = shield_hp
	var shield_source_unit_id := source_unit.unit_id if source_unit != null else &""
	var shield_source_skill_id := skill_def.skill_id if skill_def != null else &""

	target_unit.normalize_shield_state()
	if not target_unit.has_shield():
		_write_unit_shield(
			target_unit,
			shield_hp,
			shield_duration,
			shield_family,
			shield_source_unit_id,
			shield_source_skill_id,
			shield_params
		)
		return _build_unit_shield_result(target_unit, true)

	if target_unit.shield_family == shield_family:
		target_unit.shield_max_hp = maxi(target_unit.shield_max_hp, shield_hp)
		target_unit.current_shield_hp = maxi(target_unit.current_shield_hp, shield_hp)
		target_unit.shield_duration = maxi(target_unit.shield_duration, shield_duration)
		target_unit.shield_source_unit_id = shield_source_unit_id
		target_unit.shield_source_skill_id = shield_source_skill_id
		target_unit.shield_params = shield_params.duplicate(true)
		target_unit.normalize_shield_state()
		return _build_unit_shield_result(target_unit, true)

	var should_replace := false
	if shield_hp > target_unit.current_shield_hp:
		should_replace = true
	elif shield_hp == target_unit.current_shield_hp:
		if shield_duration > target_unit.shield_duration:
			should_replace = true
		elif shield_duration == target_unit.shield_duration:
			should_replace = true

	if not should_replace:
		return result

	_write_unit_shield(
		target_unit,
		shield_hp,
		shield_duration,
		shield_family,
		shield_source_unit_id,
		shield_source_skill_id,
		shield_params
	)
	return _build_unit_shield_result(target_unit, true)


func _write_unit_shield(
	target_unit: BattleUnitState,
	shield_hp: int,
	shield_duration: int,
	shield_family: StringName,
	shield_source_unit_id: StringName,
	shield_source_skill_id: StringName,
	shield_params: Dictionary
) -> void:
	if target_unit == null:
		return
	target_unit.current_shield_hp = maxi(shield_hp, 0)
	target_unit.shield_max_hp = maxi(shield_hp, 0)
	target_unit.shield_duration = shield_duration
	target_unit.shield_family = shield_family
	target_unit.shield_source_unit_id = shield_source_unit_id
	target_unit.shield_source_skill_id = shield_source_skill_id
	target_unit.shield_params = shield_params.duplicate(true)
	target_unit.normalize_shield_state()


func _build_unit_shield_result(target_unit: BattleUnitState, applied: bool) -> Dictionary:
	return {
		"applied": applied,
		"current_shield_hp": int(target_unit.current_shield_hp) if target_unit != null else 0,
		"shield_max_hp": int(target_unit.shield_max_hp) if target_unit != null else 0,
		"shield_duration": int(target_unit.shield_duration) if target_unit != null else -1,
		"shield_family": target_unit.shield_family if target_unit != null else &"",
	}


func _resolve_shield_hp(effect_def: CombatEffectDef, shield_roll_context: Dictionary = {}) -> int:
	if effect_def == null:
		return 0
	var fallback_shield_hp := maxi(int(effect_def.power), 0)
	if not _has_shield_dice_config(effect_def):
		return fallback_shield_hp
	var cache_key := _get_shield_roll_cache_key(effect_def)
	if shield_roll_context.has(cache_key):
		return maxi(int(shield_roll_context.get(cache_key, fallback_shield_hp)), 0)
	var rolled_shield_hp := _roll_shield_hp(effect_def)
	shield_roll_context[cache_key] = rolled_shield_hp
	return maxi(rolled_shield_hp, 0)


func _roll_shield_hp(effect_def: CombatEffectDef) -> int:
	var shield_hp := maxi(int(effect_def.power), 0)
	if effect_def == null or effect_def.params == null:
		return shield_hp
	var dice_count := maxi(int(effect_def.params.get("dice_count", 0)), 0)
	var dice_sides := maxi(int(effect_def.params.get("dice_sides", 0)), 0)
	if dice_count <= 0 or dice_sides <= 0:
		return shield_hp
	shield_hp += int(effect_def.params.get("dice_bonus", 0))
	for _roll_index in range(dice_count):
		shield_hp += _roll_battle_effect_die(dice_sides)
	return maxi(shield_hp, 0)


func _has_shield_dice_config(effect_def: CombatEffectDef) -> bool:
	return effect_def != null \
		and effect_def.params != null \
		and int(effect_def.params.get("dice_count", 0)) > 0 \
		and int(effect_def.params.get("dice_sides", 0)) > 0


func _get_shield_roll_cache_key(effect_def: CombatEffectDef) -> int:
	return effect_def.get_instance_id() if effect_def != null else 0


func _roll_battle_effect_die(dice_sides: int) -> int:
	if dice_sides <= 0:
		return 0
	if _state == null:
		return 1

	return int(TRUE_RANDOM_SEED_SERVICE_SCRIPT.randi_range(1, dice_sides))


func _resolve_shield_duration_tu(effect_def: CombatEffectDef) -> int:
	if effect_def == null:
		return 0
	if int(effect_def.duration_tu) > 0:
		return int(effect_def.duration_tu)
	if effect_def.params == null:
		return 0
	if effect_def.params.has("duration_tu"):
		return maxi(int(effect_def.params.get("duration_tu", 0)), 0)
	return 0


func _resolve_shield_family(skill_def: SkillDef, effect_def: CombatEffectDef) -> StringName:
	if effect_def != null and effect_def.params != null:
		var explicit_family := ProgressionDataUtils.to_string_name(effect_def.params.get("shield_family", ""))
		if explicit_family != &"":
			return explicit_family
		explicit_family = ProgressionDataUtils.to_string_name(effect_def.params.get("family", ""))
		if explicit_family != &"":
			return explicit_family
	if skill_def != null and skill_def.skill_id != &"":
		return skill_def.skill_id
	return &"shield"


func _is_terrain_effect(effect_def: CombatEffectDef) -> bool:
	if effect_def == null:
		return false
	return effect_def.effect_type == &"terrain" \
		or effect_def.effect_type == &"terrain_replace" \
		or effect_def.effect_type == &"terrain_replace_to" \
		or effect_def.effect_type == &"height" \
		or effect_def.effect_type == &"height_delta" \
		or effect_def.effect_type == &"terrain_effect"


func _resolve_effect_target_filter(skill_def: SkillDef, effect_def: CombatEffectDef) -> StringName:
	if effect_def != null and effect_def.effect_target_team_filter != &"":
		return effect_def.effect_target_team_filter
	if skill_def != null and skill_def.combat_profile != null:
		return skill_def.combat_profile.target_team_filter
	return &"any"


func _is_unit_valid_for_effect(
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	target_team_filter: StringName
) -> bool:
	if target_unit == null or not target_unit.is_alive:
		return false
	match target_team_filter:
		&"", &"any":
			return true
		&"self":
			return source_unit != null and target_unit.unit_id == source_unit.unit_id
		&"ally", &"friendly":
			return source_unit != null and target_unit.faction_id == source_unit.faction_id
		&"enemy", &"hostile":
			return source_unit != null and target_unit.faction_id != source_unit.faction_id
		_:
			return true


func _build_terrain_effect_instance_id(effect_id: StringName) -> StringName:
	_terrain_effect_nonce += 1
	return StringName("%s_%d_%d" % [
		String(effect_id),
		int(_state.timeline.current_tu) if _state != null and _state.timeline != null else 0,
		_terrain_effect_nonce,
	])


func _get_terrain_effect_display_name(effect_def: CombatEffectDef) -> String:
	if effect_def != null and effect_def.params.has("display_name"):
		return String(effect_def.params.get("display_name", ""))
	return String(effect_def.terrain_effect_id) if effect_def != null else "地格效果"


func _append_batch_log(batch: BattleEventBatch, message: String) -> void:
	if batch == null or message.is_empty():
		return
	batch.log_lines.append(message)
	if _state != null:
		_state.append_log_entry(message)


func _grant_skill_mastery_if_needed(active_unit: BattleUnitState, skill_def: SkillDef, batch: BattleEventBatch) -> void:
	if skill_def == null:
		return
	var skill_id := skill_def.skill_id
	_record_skill_success(active_unit, skill_id)
	if active_unit.source_member_id == &"" or _character_gateway == null:
		return

	_battle_rating_system.record_skill_success(active_unit, skill_id)
	_character_gateway.record_achievement_event(active_unit.source_member_id, &"skill_used", 1, skill_id)
	var mastery_amount := _skill_mastery_service.resolve_active_skill_mastery_amount()
	if mastery_amount <= 0:
		return
	var delta = _character_gateway.grant_battle_mastery(active_unit.source_member_id, skill_id, mastery_amount)
	_append_progression_delta_to_batch(active_unit, delta, batch)


func _apply_skill_mastery_grant(unit_state: BattleUnitState, grant: Dictionary, batch: BattleEventBatch) -> void:
	if grant.is_empty() or _character_gateway == null:
		return
	var member_id := ProgressionDataUtils.to_string_name(grant.get("member_id", ""))
	var skill_id := ProgressionDataUtils.to_string_name(grant.get("skill_id", ""))
	var source_type := ProgressionDataUtils.to_string_name(grant.get("source_type", ""))
	var amount := int(grant.get("amount", 0))
	if member_id == &"" or skill_id == &"" or source_type == &"" or amount <= 0:
		return
	if bool(grant.get("record_near_death_unbroken_manual", false)):
		_character_gateway.record_achievement_event(
			member_id,
			&"near_death_unbroken_manual",
			1
		)
	var delta = _character_gateway.grant_skill_mastery_from_source(
		member_id,
		skill_id,
		amount,
		source_type,
		String(grant.get("source_label", "")),
		String(grant.get("reason_text", "")),
		bool(grant.get("allow_unlocks", true))
	)
	_append_progression_delta_to_batch(unit_state, delta, batch)


func _flush_last_stand_mastery_records(batch: BattleEventBatch) -> void:
	if _damage_resolver == null:
		return
	var records := _damage_resolver.get_and_clear_last_stand_mastery_records()
	for record in records:
		var member_id := ProgressionDataUtils.to_string_name(record.get("member_id", ""))
		var unit_state := _find_unit_by_member_id(member_id) if member_id != &"" else null
		_apply_skill_mastery_grant(unit_state, record, batch)


func _append_progression_delta_to_batch(unit_state: BattleUnitState, delta, batch: BattleEventBatch) -> void:
	if unit_state == null or delta == null:
		return
	if _progression_delta_is_empty(delta):
		return
	if batch != null:
		batch.progression_deltas.append(delta)
	_unit_factory.refresh_known_skills(unit_state)
	if delta.needs_promotion_modal:
		if _state == null:
			return
		_state.modal_state = &"promotion_choice"
		_state.timeline.frozen = true
		if batch != null:
			batch.modal_requested = true
			batch.log_lines.append("%s 触发职业晋升选择。" % unit_state.display_name)


func _progression_delta_is_empty(delta) -> bool:
	if delta == null:
		return true
	return delta.mastery_changes.is_empty() \
		and delta.leveled_skill_ids.is_empty() \
		and delta.granted_skill_ids.is_empty() \
		and delta.changed_profession_ids.is_empty() \
		and delta.knowledge_changes.is_empty() \
		and delta.attribute_changes.is_empty() \
		and delta.unlocked_achievement_ids.is_empty() \
		and not delta.needs_promotion_modal


func _resolve_ground_cast_variant(
	skill_def: SkillDef,
	active_unit: BattleUnitState,
	command: BattleCommand
) -> CombatCastVariantDef:
	return _skill_resolution_rules.resolve_ground_cast_variant(
		skill_def,
		active_unit,
		command.skill_variant_id if command != null else &""
	)


func _resolve_unit_cast_variant(
	skill_def: SkillDef,
	active_unit: BattleUnitState,
	command: BattleCommand
) -> CombatCastVariantDef:
	return _skill_resolution_rules.resolve_unit_cast_variant(
		skill_def,
		active_unit,
		command.skill_variant_id if command != null else &""
	)


func _get_cast_variant_target_mode(skill_def: SkillDef, cast_variant: CombatCastVariantDef) -> StringName:
	return _skill_resolution_rules.get_cast_variant_target_mode(skill_def, cast_variant)


func _build_implicit_ground_cast_variant(skill_def: SkillDef) -> CombatCastVariantDef:
	var cast_variant := CombatCastVariantDef.new()
	cast_variant.variant_id = &""
	cast_variant.display_name = ""
	cast_variant.target_mode = &"ground"
	cast_variant.footprint_pattern = &"single"
	cast_variant.required_coord_count = 1
	cast_variant.effect_defs = skill_def.combat_profile.effect_defs.duplicate()
	return cast_variant


func _validate_ground_skill_command(
	active_unit: BattleUnitState,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	command: BattleCommand
) -> Dictionary:
	var normalized_coords := _normalize_target_coords(command)
	var result := {
		"allowed": false,
		"message": "地面技能目标无效。",
		"target_coords": normalized_coords,
	}
	if _state == null or active_unit == null or skill_def == null or skill_def.combat_profile == null or cast_variant == null:
		return result
	if cast_variant.target_mode != &"ground":
		result.message = "该技能形态不是地面施法。"
		return result
	var block_reason := _get_skill_cast_block_reason(active_unit, skill_def)
	if not block_reason.is_empty():
		result.message = block_reason
		return result
	if normalized_coords.size() != int(cast_variant.required_coord_count):
		result.message = "该技能形态需要选择 %d 个地格。" % int(cast_variant.required_coord_count)
		return result
	if _charge_resolver.is_charge_variant(cast_variant):
		return _charge_resolver.validate_charge_command(active_unit, skill_def, cast_variant, normalized_coords, result)

	var jump_effect_def := _get_ground_jump_effect_def(skill_def, cast_variant)
	var effective_skill_range := _get_effective_skill_range(active_unit, skill_def)
	var seen_coords: Dictionary = {}
	for target_coord in normalized_coords:
		var coord: Vector2i = target_coord
		if seen_coords.has(coord):
			result.message = "同一地格不能重复选择。"
			return result
		seen_coords[coord] = true
		if not _grid_service.is_inside(_state, coord):
			result.message = "存在超出战场范围的目标地格。"
			return result
		var target_distance: int = _grid_service.get_chebyshev_distance(active_unit.coord, coord) \
			if jump_effect_def != null else _grid_service.get_distance_from_unit_to_coord(active_unit, coord)
		if target_distance > effective_skill_range:
			result.message = "目标地格超出技能施放距离。"
			return result
		var cell := _grid_service.get_cell(_state, coord)
		if cell == null:
			result.message = "目标地格数据不可用。"
			return result
		if not cast_variant.allowed_base_terrains.is_empty():
			var normalized_allowed := false
			var normalized_cell_terrain := BattleTerrainRules.normalize_terrain_id(cell.base_terrain)
			for allowed_terrain in cast_variant.allowed_base_terrains:
				if BattleTerrainRules.normalize_terrain_id(allowed_terrain) == normalized_cell_terrain:
					normalized_allowed = true
					break
			if not normalized_allowed:
				result.message = "目标地格地形不符合该技能形态的要求。"
				return result
		if _is_crown_break_skill(skill_def.skill_id):
			var target_unit := _grid_service.get_unit_at_coord(_state, coord)
			if not _is_crown_break_target_eligible(active_unit, target_unit):
				result.message = "折冠只能对已被黑星烙印的 elite / boss 施放。"
				return result

	if not _validate_target_coords_shape(cast_variant.footprint_pattern, normalized_coords):
		result.message = "目标地格排布不符合该技能形态。"
		return result

	var sorted_target_coords := _sort_coords(normalized_coords)
	var special_validation_message := _get_ground_special_effect_validation_message(
		active_unit,
		skill_def,
		cast_variant,
		sorted_target_coords
	)
	if not special_validation_message.is_empty():
		result.message = special_validation_message
		return result

	result.target_coords = sorted_target_coords
	result.allowed = true
	result.message = "可施放。"
	return result


func _get_ground_special_effect_validation_message(
	active_unit: BattleUnitState,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	target_coords: Array[Vector2i]
) -> String:
	var jump_effect_def := _get_ground_jump_effect_def(skill_def, cast_variant)
	if jump_effect_def == null:
		return ""
	if active_unit == null or _state == null:
		return "跳跃落点无效。"
	if _is_movement_blocked(active_unit):
		return "当前状态下无法跳跃移动。"
	if target_coords.is_empty():
		return "跳跃落点无效。"

	var landing_coord := target_coords[0]
	if not _grid_service.can_jump_arc(_state, active_unit, landing_coord, jump_effect_def):
		return "目标地格无法作为跳跃落点。"
	return ""


func _validate_target_coords_shape(footprint_pattern: StringName, target_coords: Array[Vector2i]) -> bool:
	match footprint_pattern:
		&"single":
			return target_coords.size() == 1
		&"line2":
			if target_coords.size() != 2:
				return false
			var first := target_coords[0]
			var second := target_coords[1]
			return (first.x == second.x and absi(first.y - second.y) == 1) \
				or (first.y == second.y and absi(first.x - second.x) == 1)
		&"square2":
			if target_coords.size() != 4:
				return false
			var min_x := target_coords[0].x
			var max_x := target_coords[0].x
			var min_y := target_coords[0].y
			var max_y := target_coords[0].y
			var coord_set: Dictionary = {}
			for coord in target_coords:
				min_x = mini(min_x, coord.x)
				max_x = maxi(max_x, coord.x)
				min_y = mini(min_y, coord.y)
				max_y = maxi(max_y, coord.y)
				coord_set[coord] = true
			if max_x - min_x != 1 or max_y - min_y != 1:
				return false
			for x in range(min_x, max_x + 1):
				for y in range(min_y, max_y + 1):
					if not coord_set.has(Vector2i(x, y)):
						return false
			return true
		&"unordered":
			return not target_coords.is_empty()
		_:
			return false


func _normalize_target_coords(command: BattleCommand) -> Array[Vector2i]:
	var coords: Array[Vector2i] = []
	if command == null:
		return coords
	for target_coord in command.target_coords:
		coords.append(target_coord)
	if coords.is_empty() and command.target_coord != Vector2i(-1, -1):
		coords.append(command.target_coord)
	return coords


func _append_changed_coord(batch: BattleEventBatch, coord: Vector2i) -> void:
	if batch == null:
		return
	if batch.changed_coords.has(coord):
		return
	batch.changed_coords.append(coord)


func _append_changed_coords(batch: BattleEventBatch, coords: Array[Vector2i]) -> void:
	for coord in coords:
		_append_changed_coord(batch, coord)


func _append_changed_unit_id(batch: BattleEventBatch, unit_id: StringName) -> void:
	if batch == null or unit_id == &"":
		return
	if batch.changed_unit_ids.has(unit_id):
		return
	batch.changed_unit_ids.append(unit_id)


func _append_changed_unit_coords(batch: BattleEventBatch, unit_state: BattleUnitState) -> void:
	if unit_state == null:
		return
	unit_state.refresh_footprint()
	_append_changed_coords(batch, unit_state.occupied_coords)


func _collect_defeated_unit_loot(unit_state: BattleUnitState, killer_unit: BattleUnitState = null) -> void:
	_loot_resolver.collect_defeated_unit_loot(unit_state, killer_unit)

func _clear_defeated_unit(unit_state: BattleUnitState, batch: BattleEventBatch = null) -> void:
	if _state == null or unit_state == null:
		return
	_handle_adjacent_ally_defeat(unit_state)
	_handle_low_luck_relic_ally_defeat(unit_state, batch)
	var previous_coords := unit_state.occupied_coords.duplicate()
	_grid_service.clear_unit_occupancy(_state, unit_state)
	_append_changed_coords(batch, previous_coords)
	_append_changed_unit_id(batch, unit_state.unit_id)


func _merge_batch(target_batch: BattleEventBatch, source_batch: BattleEventBatch) -> void:
	if target_batch == null or source_batch == null:
		return
	for coord in source_batch.changed_coords:
		_append_changed_coord(target_batch, coord)
	for unit_id in source_batch.changed_unit_ids:
		_append_changed_unit_id(target_batch, unit_id)
	for log_line in source_batch.log_lines:
		target_batch.log_lines.append(log_line)
	for report_entry_variant in source_batch.report_entries:
		if report_entry_variant is not Dictionary:
			continue
		target_batch.report_entries.append((report_entry_variant as Dictionary).duplicate(true))


func _sort_coords(target_coords: Variant) -> Array[Vector2i]:
	var sorted_coords: Array[Vector2i] = []
	if target_coords is Array:
		for coord_variant in target_coords:
			if coord_variant is Vector2i:
				sorted_coords.append(coord_variant)
	sorted_coords.sort_custom(func(a: Vector2i, b: Vector2i) -> bool:
		return a.y < b.y or (a.y == b.y and a.x < b.x)
	)
	return sorted_coords


func _resolve_timeline_units_per_second(context: Dictionary) -> int:
	var units_per_second := int(context.get("units_per_second", TU_GRANULARITY))
	if units_per_second <= 0:
		return TU_GRANULARITY
	if units_per_second % TU_GRANULARITY != 0:
		push_error("timeline.units_per_second must be a multiple of %d, got %d." % [TU_GRANULARITY, units_per_second])
		return TU_GRANULARITY
	return units_per_second


func _normalize_unit_action_threshold(action_threshold: int) -> int:
	if action_threshold <= 0:
		push_error("Battle unit action_threshold must be positive, got %d." % [action_threshold])
		return BattleUnitState.DEFAULT_ACTION_THRESHOLD
	if action_threshold % TU_GRANULARITY != 0:
		push_error("Battle unit action_threshold must be a multiple of %d, got %d." % [TU_GRANULARITY, action_threshold])
		return BattleUnitState.DEFAULT_ACTION_THRESHOLD
	return action_threshold


func _initialize_unit_action_thresholds() -> void:
	if _state == null or _state.units == null:
		return
	for unit_variant in _state.units.values():
		_resolve_unit_action_threshold(unit_variant as BattleUnitState)


func _resolve_unit_action_threshold(unit_state: BattleUnitState) -> int:
	if unit_state == null:
		return BattleUnitState.DEFAULT_ACTION_THRESHOLD
	var threshold := int(unit_state.action_threshold)
	if threshold <= 0:
		threshold = BattleUnitState.DEFAULT_ACTION_THRESHOLD
		unit_state.action_threshold = threshold
	var normalized_threshold := _normalize_unit_action_threshold(threshold)
	if normalized_threshold != threshold:
		unit_state.action_threshold = normalized_threshold
	return normalized_threshold


func _resolve_timeline_tick_interval_seconds(context: Dictionary) -> float:
	var tick_interval_seconds := float(context.get("tick_interval_seconds", DEFAULT_TICK_INTERVAL_SECONDS))
	return tick_interval_seconds if tick_interval_seconds > 0.0 else DEFAULT_TICK_INTERVAL_SECONDS


func _resolve_timeline_tu_per_tick(context: Dictionary) -> int:
	var tu_per_tick := int(context.get("tu_per_tick", TU_GRANULARITY))
	if tu_per_tick <= 0:
		return TU_GRANULARITY
	if tu_per_tick % TU_GRANULARITY != 0:
		push_error("timeline.tu_per_tick must be a multiple of %d, got %d." % [TU_GRANULARITY, tu_per_tick])
		return TU_GRANULARITY
	return tu_per_tick


func _collect_dict_vector2i_keys(values: Dictionary) -> Array[Vector2i]:
	var coords: Array[Vector2i] = []
	for coord_variant in values.keys():
		if coord_variant is Vector2i:
			coords.append(coord_variant)
	return coords


func _build_reachable_move_buckets(max_move_points: int) -> Array:
	var bucket_count := maxi(max_move_points, 0) + 1
	var buckets: Array = []
	buckets.resize(bucket_count)
	for bucket_index in range(bucket_count):
		buckets[bucket_index] = []
	return buckets


func _build_reachable_move_state_key(coord: Vector2i, has_quickstep_bonus: bool) -> String:
	return "%d:%d:%d" % [coord.x, coord.y, 1 if has_quickstep_bonus else 0]


func _get_unit_skill_level(unit_state: BattleUnitState, skill_id: StringName) -> int:
	if unit_state == null or skill_id == &"":
		return 0
	if unit_state.known_skill_level_map.has(skill_id):
		return int(unit_state.known_skill_level_map.get(skill_id, 0))
	return 1 if unit_state.known_active_skill_ids.has(skill_id) else 0


func _format_skill_variant_label(skill_def: SkillDef, cast_variant: CombatCastVariantDef) -> String:
	if skill_def == null:
		return ""
	if cast_variant == null or cast_variant.display_name.is_empty():
		return skill_def.display_name
	return "%s·%s" % [skill_def.display_name, cast_variant.display_name]


func _check_battle_end(batch: BattleEventBatch) -> bool:
	var living_allies := _count_living_units(_state.ally_unit_ids)
	var living_enemies := _count_living_units(_state.enemy_unit_ids)
	if living_allies > 0 and living_enemies > 0:
		return false

	_state.phase = &"battle_ended"
	if living_allies <= 0 and living_enemies <= 0:
		_state.winner_faction_id = &"draw"
	elif living_allies > 0:
		_state.winner_faction_id = &"player"
	else:
		_state.winner_faction_id = &"hostile"
	_state.active_unit_id = &""
	_state.timeline.ready_unit_ids.clear()
	_state.timeline.frozen = true
	_battle_rating_system.record_battle_won_achievements()
	_battle_rating_system.finalize_battle_rating_rewards()
	if _battle_resolution_result == null:
		_battle_resolution_result = _build_battle_resolution_result()
	_battle_resolution_result_consumed = false
	batch.phase_changed = true
	batch.battle_ended = true
	batch.log_lines.append("战斗结束，胜利方：%s。" % String(_state.winner_faction_id))
	return true


func _count_living_units(unit_ids: Array[StringName]) -> int:
	var count := 0
	for unit_id in unit_ids:
		var unit_state := _state.units.get(unit_id) as BattleUnitState
		if unit_state != null and unit_state.is_alive:
			count += 1
	return count


func _end_active_turn(batch: BattleEventBatch) -> void:
	var active_unit := _state.units.get(_state.active_unit_id) as BattleUnitState
	if active_unit != null and active_unit.is_alive and not active_unit.has_taken_action_this_turn:
		active_unit.is_resting = true
		_append_changed_unit_id(batch, active_unit.unit_id)
	if active_unit != null and _misfortune_service != null:
		_misfortune_service.handle_low_hp_turn_end(active_unit)
	if active_unit != null and active_unit.control_mode != &"manual":
		_cleanup_ai_turn(active_unit)
	_state.phase = &"timeline_running"
	_state.active_unit_id = &""
	batch.phase_changed = true


func _handle_adjacent_ally_defeat(defeated_unit: BattleUnitState) -> void:
	if _state == null or _misfortune_service == null or defeated_unit == null:
		return
	if defeated_unit.is_alive or defeated_unit.source_member_id == &"":
		return
	var adjacent_allies := _collect_adjacent_living_allies(defeated_unit)
	if adjacent_allies.is_empty():
		return
	_misfortune_service.handle_adjacent_ally_defeat(defeated_unit, adjacent_allies)


func _handle_low_luck_relic_ally_defeat(defeated_unit: BattleUnitState, batch: BattleEventBatch = null) -> void:
	if _state == null or defeated_unit == null or defeated_unit.is_alive:
		return
	for unit_variant in _state.units.values():
		var candidate := unit_variant as BattleUnitState
		if candidate == null or not candidate.is_alive:
			continue
		if candidate.unit_id == defeated_unit.unit_id:
			continue
		if candidate.faction_id != defeated_unit.faction_id:
			continue
		if not LOW_LUCK_RELIC_RULES_SCRIPT.unit_has_flag(candidate, LOW_LUCK_RELIC_RULES_SCRIPT.ATTR_BLOOD_DEBT_SHAWL):
			continue
		candidate.current_ap += LOW_LUCK_RELIC_RULES_SCRIPT.BLOOD_DEBT_ALLY_DOWN_AP_GAIN
		_append_changed_unit_id(batch, candidate.unit_id)
		if batch != null:
			batch.log_lines.append("%s 目睹队友倒地，血债披肩返还 %d 点行动点。" % [
				candidate.display_name,
				LOW_LUCK_RELIC_RULES_SCRIPT.BLOOD_DEBT_ALLY_DOWN_AP_GAIN,
			])


func _collect_adjacent_living_allies(defeated_unit: BattleUnitState) -> Array[BattleUnitState]:
	var adjacent_allies: Array[BattleUnitState] = []
	if defeated_unit == null:
		return adjacent_allies
	defeated_unit.refresh_footprint()
	for unit_variant in _state.units.values():
		var candidate := unit_variant as BattleUnitState
		if candidate == null or not candidate.is_alive:
			continue
		if candidate.unit_id == defeated_unit.unit_id:
			continue
		if candidate.faction_id != defeated_unit.faction_id or candidate.source_member_id == &"":
			continue
		candidate.refresh_footprint()
		if _are_units_adjacent(candidate, defeated_unit):
			adjacent_allies.append(candidate)
	return adjacent_allies


func _are_units_adjacent(first_unit: BattleUnitState, second_unit: BattleUnitState) -> bool:
	if first_unit == null or second_unit == null:
		return false
	for first_coord in first_unit.occupied_coords:
		for second_coord in second_unit.occupied_coords:
			if absi(first_coord.x - second_coord.x) + absi(first_coord.y - second_coord.y) == 1:
				return true
	return false


func _activate_next_ready_unit(batch: BattleEventBatch) -> void:
	while not _state.timeline.ready_unit_ids.is_empty():
		var next_unit_id: StringName = _state.timeline.ready_unit_ids.pop_front()
		var unit_state := _state.units.get(next_unit_id) as BattleUnitState
		if unit_state == null or not unit_state.is_alive:
			continue
		_state.phase = &"unit_acting"
		_state.active_unit_id = next_unit_id
		unit_state.has_taken_action_this_turn = false
		_advance_unit_turn_timers(unit_state, batch)
		_record_turn_started(unit_state)
		var action_points := 1
		if unit_state.attribute_snapshot != null:
			action_points = maxi(unit_state.attribute_snapshot.get_value(&"action_points"), 1)
		unit_state.current_ap = action_points
		unit_state.current_move_points = BattleUnitState.DEFAULT_MOVE_POINTS_PER_TURN
		var turn_start_result := _apply_turn_start_statuses(unit_state, batch)
		if not unit_state.is_alive:
			var defeat_source_unit_id := ProgressionDataUtils.to_string_name(turn_start_result.get("defeat_source_unit_id", ""))
			var defeat_source_unit = _state.units.get(defeat_source_unit_id) as BattleUnitState if defeat_source_unit_id != &"" else null
			_collect_defeated_unit_loot(unit_state, defeat_source_unit)
			_clear_defeated_unit(unit_state, batch)
			_state.phase = &"timeline_running"
			_state.active_unit_id = &""
			batch.phase_changed = true
			batch.changed_unit_ids.append(next_unit_id)
			batch.log_lines.append("%s 因持续效果倒下。" % unit_state.display_name)
			_state.append_log_entry(String(batch.log_lines[-1]))
			if _check_battle_end(batch):
				return
			continue
		if unit_state.control_mode != &"manual":
			_prepare_ai_turn(unit_state)
		batch.phase_changed = true
		batch.changed_unit_ids.append(next_unit_id)
		batch.log_lines.append("轮到 %s 行动。" % unit_state.display_name)
		_state.append_log_entry(String(batch.log_lines[-1]))
		return


func _get_skill_cast_block_reason(active_unit: BattleUnitState, skill_def: SkillDef) -> String:
	return _skill_turn_resolver.get_skill_cast_block_reason(active_unit, skill_def)


func _unit_has_melee_weapon(active_unit: BattleUnitState) -> bool:
	return _skill_turn_resolver.unit_has_melee_weapon(active_unit)


func _requires_melee_weapon(skill_def: SkillDef) -> bool:
	return _skill_turn_resolver.requires_melee_weapon(skill_def)


func _effect_uses_weapon_physical_damage_tag(effect_def: CombatEffectDef) -> bool:
	return _skill_turn_resolver.effect_uses_weapon_physical_damage_tag(effect_def)


func _get_skill_command_block_reason(
	active_unit: BattleUnitState,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef
) -> String:
	return _skill_turn_resolver.get_skill_command_block_reason(active_unit, skill_def, cast_variant)


func _consume_skill_costs(
	active_unit: BattleUnitState,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef = null,
	batch: BattleEventBatch = null
) -> bool:
	return _skill_turn_resolver.consume_skill_costs(active_unit, skill_def, cast_variant, batch)


func _get_effective_skill_costs(active_unit: BattleUnitState, skill_def: SkillDef) -> Dictionary:
	return _skill_turn_resolver.get_effective_skill_costs(active_unit, skill_def)


func _get_black_contract_push_variant_block_reason(
	active_unit: BattleUnitState,
	cast_variant: CombatCastVariantDef
) -> String:
	return _skill_turn_resolver.get_black_contract_push_variant_block_reason(active_unit, cast_variant)


func _consume_black_contract_push_cast(
	active_unit: BattleUnitState,
	cast_variant: CombatCastVariantDef,
	batch: BattleEventBatch = null
) -> bool:
	return _skill_turn_resolver.consume_black_contract_push_cast(active_unit, cast_variant, batch)


func _ensure_unit_turn_anchor(unit_state: BattleUnitState) -> void:
	_skill_turn_resolver.ensure_unit_turn_anchor(unit_state)


func _advance_unit_cooldowns(unit_state: BattleUnitState, cooldown_delta: int) -> bool:
	return _skill_turn_resolver.advance_unit_cooldowns(unit_state, cooldown_delta)


func _consume_turn_cooldown_delta(unit_state: BattleUnitState) -> bool:
	return _skill_turn_resolver.consume_turn_cooldown_delta(unit_state)


func _advance_unit_turn_timers(unit_state: BattleUnitState, batch: BattleEventBatch) -> void:
	_skill_turn_resolver.advance_unit_turn_timers(unit_state, batch)


func _apply_turn_start_statuses(unit_state: BattleUnitState, batch: BattleEventBatch) -> Dictionary:
	return _skill_turn_resolver.apply_turn_start_statuses(unit_state, batch)


func _apply_unit_status_periodic_ticks(
	unit_state: BattleUnitState,
	elapsed_tu: int,
	batch: BattleEventBatch
) -> Dictionary:
	return _skill_turn_resolver.apply_unit_status_periodic_ticks(unit_state, elapsed_tu, batch)


func _advance_unit_status_durations(unit_state: BattleUnitState, elapsed_tu: int) -> bool:
	return _skill_turn_resolver.advance_unit_status_durations(unit_state, elapsed_tu)


func _get_effective_skill_range(active_unit: BattleUnitState, skill_def: SkillDef) -> int:
	return _skill_turn_resolver.get_effective_skill_range(active_unit, skill_def)


func _resolve_base_skill_range(active_unit: BattleUnitState, skill_def: SkillDef) -> int:
	return _skill_turn_resolver.resolve_base_skill_range(active_unit, skill_def)


func _is_weapon_range_skill(skill_def: SkillDef) -> bool:
	return _skill_turn_resolver.is_weapon_range_skill(skill_def)


func _get_weapon_attack_range(active_unit: BattleUnitState) -> int:
	return _skill_turn_resolver.get_weapon_attack_range(active_unit)


func _skill_has_tag(skill_def: SkillDef, expected_tag: StringName) -> bool:
	return _skill_turn_resolver.skill_has_tag(skill_def, expected_tag)


func _is_movement_blocked(unit_state: BattleUnitState) -> bool:
	return _skill_turn_resolver.is_movement_blocked(unit_state)


func _has_status(unit_state: BattleUnitState, status_id: StringName) -> bool:
	return _skill_turn_resolver.has_status(unit_state, status_id)


func _consume_status_if_present(unit_state: BattleUnitState, status_id: StringName, batch: BattleEventBatch = null) -> void:
	_skill_turn_resolver.consume_status_if_present(unit_state, status_id, batch)


func _is_main_skill_locked_by_status(active_unit: BattleUnitState, skill_def: SkillDef) -> bool:
	return _skill_turn_resolver.is_main_skill_locked_by_status(active_unit, skill_def)


func _count_debuff_statuses(unit_state: BattleUnitState) -> int:
	return _skill_turn_resolver.count_debuff_statuses(unit_state)


func _status_counts_as_debuff(status_id: StringName, status_entry: BattleStatusEffectState) -> bool:
	return _skill_turn_resolver.status_counts_as_debuff(status_id, status_entry)


func _has_status_param_bool(unit_state: BattleUnitState, param_key: StringName) -> bool:
	return _skill_turn_resolver.has_status_param_bool(unit_state, param_key)


func _get_status_param_max_int(unit_state: BattleUnitState, param_key: StringName) -> int:
	return _skill_turn_resolver.get_status_param_max_int(unit_state, param_key)

func _prepare_ai_turn(unit_state: BattleUnitState) -> void:
	if unit_state == null:
		return
	unit_state.ai_blackboard["turn_started_tu"] = int(_state.timeline.current_tu) if _state != null and _state.timeline != null else 0
	unit_state.ai_blackboard["turn_decision_count"] = 0
	var brain = _enemy_ai_brains.get(unit_state.ai_brain_id)
	if brain != null and not brain.has_state(unit_state.ai_state_id):
		unit_state.ai_state_id = brain.default_state_id


func _cleanup_ai_turn(unit_state: BattleUnitState) -> void:
	if unit_state == null:
		return
	unit_state.ai_blackboard.erase("turn_started_tu")
	unit_state.ai_blackboard.erase("turn_decision_count")


func _find_unit_by_member_id(member_id: StringName) -> BattleUnitState:
	for unit_state_data in _state.units.values():
		var unit_state := unit_state_data as BattleUnitState
		if unit_state != null and unit_state.source_member_id == member_id:
			return unit_state
	return null


func _get_units_in_order() -> Array[StringName]:
	var ordered_ids: Array[StringName] = []
	for unit_id_str in ProgressionDataUtils.sorted_string_keys(_state.units):
		ordered_ids.append(StringName(unit_id_str))
	return ordered_ids


func _new_batch() -> BattleEventBatch:
	return BATTLE_EVENT_BATCH_SCRIPT.new()


func _build_battle_resolution_result():
	return _loot_resolver.build_battle_resolution_result()

func _roll_hit_rate(hit_rate_percent: int) -> Dictionary:
	return _hit_resolver.roll_hit_rate(_state, hit_rate_percent)
