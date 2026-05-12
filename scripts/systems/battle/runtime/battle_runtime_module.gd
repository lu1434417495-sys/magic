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
const BATTLE_ATTACK_CHECK_POLICY_SERVICE_SCRIPT = preload("res://scripts/systems/battle/rules/battle_attack_check_policy_service.gd")
const BATTLE_STATUS_SEMANTIC_TABLE_SCRIPT = preload("res://scripts/systems/battle/rules/battle_status_semantic_table.gd")
const BATTLE_AI_SERVICE_SCRIPT = preload("res://scripts/systems/battle/ai/battle_ai_service.gd")
const BATTLE_AI_DECISION_SCRIPT = preload("res://scripts/systems/battle/ai/battle_ai_decision.gd")
const BATTLE_AI_CONTEXT_SCRIPT = preload("res://scripts/systems/battle/ai/battle_ai_context.gd")
const BATTLE_AI_ACTION_ASSEMBLER_SCRIPT = preload("res://scripts/systems/battle/ai/battle_ai_action_assembler.gd")
const BATTLE_TERRAIN_EFFECT_SYSTEM_SCRIPT = preload("res://scripts/systems/battle/terrain/battle_terrain_effect_system.gd")
const BATTLE_RATING_SYSTEM_SCRIPT = preload("res://scripts/systems/battle/runtime/battle_rating_system.gd")
const BATTLE_UNIT_FACTORY_SCRIPT = preload("res://scripts/systems/battle/runtime/battle_unit_factory.gd")
const BATTLE_CHARGE_RESOLVER_SCRIPT = preload("res://scripts/systems/battle/runtime/battle_charge_resolver.gd")
const BATTLE_REPEAT_ATTACK_RESOLVER_SCRIPT = preload("res://scripts/systems/battle/runtime/battle_repeat_attack_resolver.gd")
const BATTLE_MAGIC_BACKLASH_RESOLVER_SCRIPT = preload("res://scripts/systems/battle/runtime/battle_magic_backlash_resolver.gd")
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
const BATTLE_METRICS_COLLECTOR_SCRIPT = preload("res://scripts/systems/battle/runtime/battle_metrics_collector.gd")
const BATTLE_SHIELD_SERVICE_SCRIPT = preload("res://scripts/systems/battle/runtime/battle_shield_service.gd")
const BATTLE_GROUND_EFFECT_SERVICE_SCRIPT = preload("res://scripts/systems/battle/runtime/battle_ground_effect_service.gd")
const BATTLE_SPECIAL_SKILL_RESOLVER_SCRIPT = preload("res://scripts/systems/battle/runtime/battle_special_skill_resolver.gd")
const BATTLE_MOVEMENT_SERVICE_SCRIPT = preload("res://scripts/systems/battle/runtime/battle_movement_service.gd")
const BATTLE_LAYERED_BARRIER_SERVICE_SCRIPT = preload("res://scripts/systems/battle/runtime/battle_layered_barrier_service.gd")
const BATTLE_TIMELINE_DRIVER_SCRIPT = preload("res://scripts/systems/battle/runtime/battle_timeline_driver.gd")
const BATTLE_SKILL_EXECUTION_ORCHESTRATOR_SCRIPT = preload("res://scripts/systems/battle/runtime/battle_skill_execution_orchestrator.gd")
const BATTLE_SPECIAL_PROFILE_GATE_SCRIPT = preload("res://scripts/systems/battle/runtime/battle_special_profile_gate.gd")
const BATTLE_METEOR_SWARM_RESOLVER_SCRIPT = preload("res://scripts/systems/battle/runtime/battle_meteor_swarm_resolver.gd")
const BATTLE_SKILL_OUTCOME_COMMITTER_SCRIPT = preload("res://scripts/systems/battle/runtime/battle_skill_outcome_committer.gd")
const BATTLE_SPECIAL_PROFILE_COMMIT_ADAPTER_SCRIPT = preload("res://scripts/systems/battle/runtime/battle_special_profile_commit_adapter.gd")
const TRAIT_TRIGGER_HOOKS_SCRIPT = preload("res://scripts/systems/battle/runtime/trait_trigger_hooks.gd")
const ENCOUNTER_ROSTER_BUILDER_SCRIPT = preload("res://scripts/systems/world/encounter_roster_builder.gd")
const BATTLE_RESOLUTION_RESULT_SCRIPT = preload("res://scripts/systems/battle/core/battle_resolution_result.gd")
const EQUIPMENT_DROP_SERVICE_SCRIPT = preload("res://scripts/systems/inventory/equipment_drop_service.gd")
const EQUIPMENT_RULES_SCRIPT = preload("res://scripts/player/equipment/equipment_rules.gd")
const FATE_RUNTIME_MODULE_SCRIPT = preload("res://scripts/systems/battle/fate/fate_runtime_module.gd")
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
const BODY_SIZE_RULES_SCRIPT = preload("res://scripts/systems/progression/body_size_rules.gd")
const CombatCastVariantDef = preload("res://scripts/player/progression/combat_cast_variant_def.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")
const BodySizeRules = BODY_SIZE_RULES_SCRIPT
const REPEAT_ATTACK_EFFECT_TYPE: StringName = &"repeat_attack_until_fail"
const BODY_SIZE_CATEGORY_OVERRIDE_EFFECT_TYPE: StringName = &"body_size_category_override"
const CHAIN_DAMAGE_EFFECT_TYPE: StringName = &"chain_damage"
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
const BLACK_CROWN_SEAL_SKILL_ID: StringName = MISFORTUNE_SERVICE_SCRIPT.BLACK_CROWN_SEAL_SKILL_ID
const BLACK_STAR_BRAND_SKILL_ID: StringName = MISFORTUNE_SERVICE_SCRIPT.BLACK_STAR_BRAND_SKILL_ID
const CROWN_BREAK_SKILL_ID: StringName = MISFORTUNE_SERVICE_SCRIPT.CROWN_BREAK_SKILL_ID
const DOOM_SENTENCE_SKILL_ID: StringName = MISFORTUNE_SERVICE_SCRIPT.DOOM_SENTENCE_SKILL_ID
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
const STAMINA_RECOVERY_PROGRESS_BASE := 11
const STAMINA_RECOVERY_PROGRESS_DENOMINATOR := 10
const STAMINA_RESTING_RECOVERY_MULTIPLIER := 2
const DEFAULT_TICK_INTERVAL_SECONDS := 1.0
const BATTLE_START_PLACEMENT_MAX_ATTEMPTS := 8
const BATTLE_START_TERRAIN_RETRY_SEED_STEP := 7919
const CHANGE_EQUIPMENT_AP_COST := BATTLE_CHANGE_EQUIPMENT_RESOLVER_SCRIPT.CHANGE_EQUIPMENT_AP_COST
const STATUS_PARAM_BODY_SIZE_CATEGORY_OVERRIDE := "body_size_category_override"
const STATUS_PARAM_PREVIOUS_BODY_SIZE_CATEGORY := "previous_body_size_category"
const SPAWN_SIDE_NEAR_LONG_EDGE: StringName = &"near_long_edge"
const SPAWN_SIDE_FAR_LONG_EDGE: StringName = &"far_long_edge"

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
var _ai_action_assembler = BATTLE_AI_ACTION_ASSEMBLER_SCRIPT.new()
var _terrain_effect_system = BATTLE_TERRAIN_EFFECT_SYSTEM_SCRIPT.new()
var _battle_rating_system = BATTLE_RATING_SYSTEM_SCRIPT.new()
var _unit_factory = BATTLE_UNIT_FACTORY_SCRIPT.new()
var _charge_resolver = BATTLE_CHARGE_RESOLVER_SCRIPT.new()
var _repeat_attack_resolver = BATTLE_REPEAT_ATTACK_RESOLVER_SCRIPT.new()
var _magic_backlash_resolver = BATTLE_MAGIC_BACKLASH_RESOLVER_SCRIPT.new()
var _report_formatter: BattleReportFormatter = BATTLE_REPORT_FORMATTER_SCRIPT.new()
var _skill_resolution_rules = BATTLE_SKILL_RESOLUTION_RULES_SCRIPT.new()
var _skill_mastery_service = BATTLE_SKILL_MASTERY_SERVICE_SCRIPT.new()
var _terrain_topology_service = BATTLE_TERRAIN_TOPOLOGY_SERVICE_SCRIPT.new()
var _target_collection_service = BATTLE_TARGET_COLLECTION_SERVICE_SCRIPT.new()
var _spawn_reachability_service = BATTLE_SPAWN_REACHABILITY_SERVICE_SCRIPT.new()
var _equipment_drop_service = EQUIPMENT_DROP_SERVICE_SCRIPT.new()
var _equipment_instance_id_allocator: Callable = Callable()
var _fate_runtime = FATE_RUNTIME_MODULE_SCRIPT.new()
var _change_equipment_resolver = BATTLE_CHANGE_EQUIPMENT_RESOLVER_SCRIPT.new()
var _loot_resolver = BATTLE_RUNTIME_LOOT_RESOLVER_SCRIPT.new()
var _skill_turn_resolver = BATTLE_SKILL_TURN_RESOLVER_SCRIPT.new()
var _metrics_collector = BATTLE_METRICS_COLLECTOR_SCRIPT.new()
var _shield_service = BATTLE_SHIELD_SERVICE_SCRIPT.new()
var _ground_effect_service = BATTLE_GROUND_EFFECT_SERVICE_SCRIPT.new()
var _special_skill_resolver = BATTLE_SPECIAL_SKILL_RESOLVER_SCRIPT.new()
var _movement_service = BATTLE_MOVEMENT_SERVICE_SCRIPT.new()
var _layered_barrier_service = BATTLE_LAYERED_BARRIER_SERVICE_SCRIPT.new()
var _timeline_driver = BATTLE_TIMELINE_DRIVER_SCRIPT.new()
var _skill_orchestrator = BATTLE_SKILL_EXECUTION_ORCHESTRATOR_SCRIPT.new()
var _trait_trigger_hooks = TRAIT_TRIGGER_HOOKS_SCRIPT.new()
var _special_profile_registry_snapshot: Dictionary = {}
var _special_profile_gate = null
var _meteor_swarm_resolver = null
var _attack_check_policy_service = BATTLE_ATTACK_CHECK_POLICY_SERVICE_SCRIPT.new()
var _skill_preview_service = null
var _skill_outcome_committer = BATTLE_SKILL_OUTCOME_COMMITTER_SCRIPT.new()
var _special_profile_commit_adapter = BATTLE_SPECIAL_PROFILE_COMMIT_ADAPTER_SCRIPT.new()
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
var _ai_action_plans_by_unit_id: Dictionary = {}
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
	terrain_generator: Object = null,
	equipment_instance_id_allocator: Callable = Callable(),
	battle_special_profile_registry_snapshot: Dictionary = {}
) -> void:
	_character_gateway = character_gateway
	_skill_defs = skill_defs if skill_defs != null else {}
	_special_profile_registry_snapshot = battle_special_profile_registry_snapshot.duplicate(true) if battle_special_profile_registry_snapshot != null else {}
	if _damage_resolver != null and _damage_resolver.has_method("set_skill_defs"):
		_damage_resolver.set_skill_defs(_skill_defs)
	if _damage_resolver != null and _damage_resolver.has_method("set_hit_resolver"):
		_damage_resolver.set_hit_resolver(_hit_resolver)
	_item_defs = item_defs if item_defs != null else {}
	if _item_defs.is_empty() and _character_gateway != null and _character_gateway.has_method("get_item_defs"):
		var gateway_item_defs = _character_gateway.call("get_item_defs")
		if gateway_item_defs is Dictionary:
			_item_defs = gateway_item_defs
	_enemy_templates = enemy_templates if enemy_templates != null else {}
	_enemy_ai_brains = enemy_ai_brains if enemy_ai_brains != null else {}
	_encounter_builder = encounter_builder if encounter_builder != null else ENCOUNTER_ROSTER_BUILDER_SCRIPT.new()
	_equipment_drop_service = equipment_drop_service if equipment_drop_service != null else EQUIPMENT_DROP_SERVICE_SCRIPT.new()
	_equipment_instance_id_allocator = equipment_instance_id_allocator
	if terrain_generator != null:
		_terrain_generator = terrain_generator
	_ai_action_plans_by_unit_id.clear()
	_ai_service.setup(_enemy_ai_brains, _damage_resolver)
	_terrain_effect_system.setup(self)
	if _attack_check_policy_service == null:
		_attack_check_policy_service = BATTLE_ATTACK_CHECK_POLICY_SERVICE_SCRIPT.new()
	_attack_check_policy_service.setup(self, _hit_resolver, _terrain_effect_system)
	if _skill_outcome_committer == null:
		_skill_outcome_committer = BATTLE_SKILL_OUTCOME_COMMITTER_SCRIPT.new()
	_skill_outcome_committer.setup(self)
	if _special_profile_commit_adapter == null:
		_special_profile_commit_adapter = BATTLE_SPECIAL_PROFILE_COMMIT_ADAPTER_SCRIPT.new()
	_special_profile_commit_adapter.setup(self, _skill_outcome_committer)
	_battle_rating_system.setup(self, _skill_mastery_service)
	_unit_factory.setup(self)
	_charge_resolver.setup(self, _skill_mastery_service)
	_repeat_attack_resolver.setup(self, _skill_mastery_service)
	_skill_mastery_service.clear()
	_fate_runtime.setup(_character_gateway, get_fate_event_bus(), self, Callable(self, "_find_unit_by_member_id"))
	_change_equipment_resolver.setup(self)
	_loot_resolver.setup(self)
	_skill_turn_resolver.setup(self)
	_metrics_collector.setup(self)
	_shield_service.setup(self)
	_ground_effect_service.setup(self)
	_special_skill_resolver.setup(self)
	_movement_service.setup(self)
	_layered_barrier_service.setup(self)
	_timeline_driver.setup(self)
	_skill_orchestrator.setup(self)
	_setup_special_profile_runtime()


func _setup_special_profile_runtime() -> void:
	if _special_profile_gate == null:
		_special_profile_gate = BATTLE_SPECIAL_PROFILE_GATE_SCRIPT.new()
	_special_profile_gate.setup(_special_profile_registry_snapshot)
	if _skill_outcome_committer == null:
		_skill_outcome_committer = BATTLE_SKILL_OUTCOME_COMMITTER_SCRIPT.new()
	_skill_outcome_committer.setup(self)
	if _special_profile_commit_adapter == null:
		_special_profile_commit_adapter = BATTLE_SPECIAL_PROFILE_COMMIT_ADAPTER_SCRIPT.new()
	_special_profile_commit_adapter.setup(self, _skill_outcome_committer)

	_meteor_swarm_resolver = null
	var profiles: Variant = _special_profile_registry_snapshot.get("profiles", {})
	if profiles is not Dictionary:
		return
	var meteor_profile_snapshot: Variant = (profiles as Dictionary).get("meteor_swarm", {})
	if meteor_profile_snapshot is not Dictionary:
		return
	if String((meteor_profile_snapshot as Dictionary).get("runtime_resolver_id", "")) != "meteor_swarm":
		return
	_meteor_swarm_resolver = BATTLE_METEOR_SWARM_RESOLVER_SCRIPT.new()
	_meteor_swarm_resolver.setup(self, _attack_check_policy_service)


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
	_ai_action_plans_by_unit_id.clear()
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

		var encounter_anchor_id := ProgressionDataUtils.to_string_name(encounter_anchor.entity_id if encounter_anchor != null else context.get("encounter_anchor_id", ""))
		var battle_id_prefix := String(encounter_anchor_id) if encounter_anchor_id != &"" else "battle"
		var encounter_display_name := String(encounter_anchor.display_name) if encounter_anchor != null else ""
		if encounter_display_name.is_empty():
			encounter_display_name = String(context.get("encounter_display_name", "未知遭遇"))
		_state = BATTLE_STATE_SCRIPT.new()
		_state.battle_id = ProgressionDataUtils.to_string_name("%s_%d" % [battle_id_prefix, seed])
		_state.seed = seed
		_state.set_party_backpack_view(_get_party_backpack_state(party_state))
		_state.map_size = terrain_data.get("map_size", Vector2i.ZERO)
		_state.world_coord = context.get("world_coord", encounter_anchor.world_coord if encounter_anchor != null else Vector2i.ZERO)
		_state.encounter_anchor_id = encounter_anchor_id
		_state.terrain_profile_id = terrain_profile_id
		_state.cells = terrain_data.get("cells", {})
		_state.cell_columns = terrain_data.get("cell_columns", BattleCellState.build_columns_from_surface_cells(_state.cells))
		_state.timeline = BATTLE_TIMELINE_STATE_SCRIPT.new()
		_state.timeline.tu_per_tick = _resolve_timeline_tu_per_tick(context)

		var ally_spawn_coords: Array = terrain_data.get("ally_spawns", [])
		var enemy_spawn_coords: Array = terrain_data.get("enemy_spawns", [])
		var ally_spawn_side: StringName = &""
		var enemy_spawn_side: StringName = &""
		if bool(context.get("enforce_opposing_spawn_sides", false)):
			ally_spawn_side = _resolve_spawn_side_from_coords(ally_spawn_coords)
			enemy_spawn_side = _resolve_spawn_side_from_coords(enemy_spawn_coords)
			if ally_spawn_side == &"" and enemy_spawn_side != &"":
				ally_spawn_side = _get_opposite_spawn_side(enemy_spawn_side)
			if enemy_spawn_side == &"" and ally_spawn_side != &"":
				enemy_spawn_side = _get_opposite_spawn_side(ally_spawn_side)
			if ally_spawn_side != &"" and enemy_spawn_side == ally_spawn_side:
				enemy_spawn_side = _get_opposite_spawn_side(ally_spawn_side)
		if not _place_units(ally_units, ally_spawn_coords, true, ally_spawn_side):
			_state = null
			continue
		if not _place_units(enemy_units, enemy_spawn_coords, false, enemy_spawn_side):
			_state = null
			continue
		_initialize_unit_trait_hooks()
		if validate_spawn_reachability:
			var spawn_reachability_options := {
				"validate_player_to_enemy": bool(context.get("validate_bidirectional_spawn_reachability", false)),
			}
			var spawn_reachability := _spawn_reachability_service.validate_state(_state, _grid_service, _skill_defs, spawn_reachability_options)
			if not bool(spawn_reachability.get("valid", false)):
				_state = null
				_ai_action_plans_by_unit_id.clear()
				continue

		_initialize_unit_action_thresholds()
		_build_ai_action_plans()
		_state.phase = &"timeline_running"
		_state.active_unit_id = &""
		_state.winner_faction_id = &""
		_state.modal_state = &""
		_state.attack_roll_nonce = 0
		_state.reset_log_entries(["战斗开始：%s" % encounter_display_name])
		_battle_rating_system.initialize_battle_rating_stats()
		_fate_runtime.begin_battle(calamity_by_member_id)
		_terrain_effect_nonce = 0
		_battle_resolution_result = null
		_battle_resolution_result_consumed = false
		_ai_turn_traces.clear()
		_initialize_battle_metrics()
		return _state

	_state = null
	_ai_action_plans_by_unit_id.clear()
	return BATTLE_STATE_SCRIPT.new()


func _build_ai_action_plans() -> void:
	_ai_action_plans_by_unit_id.clear()
	if _state == null or _ai_action_assembler == null:
		return
	for unit_variant in _state.units.values():
		var unit_state := unit_variant as BattleUnitState
		if unit_state == null or unit_state.control_mode == &"manual" or unit_state.ai_brain_id == &"":
			continue
		var brain = _enemy_ai_brains.get(unit_state.ai_brain_id)
		if brain == null:
			continue
		var action_plan: Dictionary = _ai_action_assembler.build_unit_action_plan(unit_state, brain, _skill_defs)
		if action_plan.is_empty():
			continue
		_ai_action_plans_by_unit_id[unit_state.unit_id] = action_plan


func _resolve_formal_terrain_profile_id(terrain_data: Dictionary) -> StringName:
	if not terrain_data.has("terrain_profile_id"):
		return &""
	var terrain_profile_variant = terrain_data["terrain_profile_id"]
	if terrain_profile_variant is not String and terrain_profile_variant is not StringName:
		return &""
	return ProgressionDataUtils.to_string_name(terrain_profile_variant)


func advance(tick_count: int) -> BattleEventBatch:
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
		if active_unit == null or not active_unit.is_alive:
			_end_active_turn(batch)
			return batch
		var madness_ai_control := _skill_turn_resolver.is_turn_ai_override_active(active_unit)
		if active_unit != null and active_unit.is_alive and (active_unit.control_mode != &"manual" or madness_ai_control):
			if madness_ai_control and active_unit.ai_brain_id == &"":
				var madness_command = _skill_turn_resolver.build_madness_fallback_command(active_unit)
				if madness_command != null:
					return issue_command(madness_command)
			var ai_context := BATTLE_AI_CONTEXT_SCRIPT.new()
			ai_context.state = _state
			ai_context.unit_state = active_unit
			ai_context.grid_service = _grid_service
			ai_context.skill_defs = _skill_defs
			ai_context.preview_callback = Callable(self, "preview_command")
			ai_context.skill_score_input_callback = Callable(_ai_service, "build_skill_score_input")
			ai_context.action_score_input_callback = Callable(_ai_service, "build_action_score_input")
			ai_context.runtime_actions_by_state = _ai_action_plans_by_unit_id.get(active_unit.unit_id, {})
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
				var ai_turn_trace := {}
				var ai_trace_unit_snapshots_before := {}
				var ai_trace_decision_target_unit_ids: Array[StringName] = []
				if _ai_trace_enabled:
					ai_turn_trace = ai_context.build_turn_trace(decision)
					ai_trace_decision_target_unit_ids = _collect_ai_trace_decision_target_unit_ids(decision, ai_turn_trace)
					ai_trace_unit_snapshots_before = _build_ai_trace_unit_snapshot_map()
					ai_turn_trace["decision_target_snapshots"] = _build_ai_trace_snapshots_for_unit_ids(
						ai_trace_decision_target_unit_ids,
						ai_trace_unit_snapshots_before
					)
				var decision_command: BattleCommand = decision.command
				var decision_batch := issue_command(decision_command)
				if _ai_trace_enabled:
					ai_turn_trace["execution_result"] = _build_ai_trace_execution_result(
						decision,
						decision_batch,
						ai_trace_unit_snapshots_before,
						ai_trace_decision_target_unit_ids
					)
					_ai_turn_traces.append(ai_turn_trace)
				if decision_batch != null:
					decision_batch.log_lines.insert(0, ai_line)
				return decision_batch
		return batch

	if tick_count > 0:
		_timeline_driver.advance_timeline(tick_count, batch)
		if _check_battle_end(batch):
			return batch

	if _state.phase == &"timeline_running":
		_activate_next_ready_unit(batch)

	return batch


func _use_discrete_timeline_ticks() -> bool:
	_ensure_sidecars_ready()
	return _timeline_driver._use_discrete_timeline_ticks()


func _apply_timeline_step(batch: BattleEventBatch, tu_delta: int) -> void:
	_ensure_sidecars_ready()
	_timeline_driver._apply_timeline_step(batch, tu_delta)


func _resolve_timeline_status_phase(batch: BattleEventBatch, tu_delta: int) -> void:
	_ensure_sidecars_ready()
	_timeline_driver._resolve_timeline_status_phase(batch, tu_delta)


func _collect_timeline_ready_units(batch: BattleEventBatch, tu_delta: int) -> void:
	_ensure_sidecars_ready()
	_timeline_driver._collect_timeline_ready_units(batch, tu_delta)


func _apply_stamina_recovery(unit_state: BattleUnitState, tu_delta: int) -> bool:
	_ensure_sidecars_ready()
	return _timeline_driver._apply_stamina_recovery(unit_state, tu_delta)


func _get_unit_constitution(unit_state: BattleUnitState) -> int:
	_ensure_sidecars_ready()
	return _timeline_driver._get_unit_constitution(unit_state)


func _apply_stamina_recovery_percent_bonus(unit_state: BattleUnitState, base_progress_gain: int) -> int:
	_ensure_sidecars_ready()
	return _timeline_driver._apply_stamina_recovery_percent_bonus(unit_state, base_progress_gain)


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
				preview.log_lines.append("移动可执行，距离消耗 %d 点移动力，执行后锁定剩余移动力。" % move_cost)
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
	return _fate_runtime.get_calamity_by_member_id() if _fate_runtime != null else ProgressionDataUtils.to_string_name_int_map(calamity_by_member_id).duplicate(true)


func get_member_calamity(member_id: StringName) -> int:
	return _fate_runtime.get_member_calamity(member_id) if _fate_runtime != null else 0


func get_member_calamity_cap(member_id: StringName) -> int:
	return _fate_runtime.get_member_calamity_cap(member_id) if _fate_runtime != null else MISFORTUNE_SERVICE_SCRIPT.BASE_CALAMITY_CAP


func get_black_star_brand_cast_cost(member_id: StringName) -> int:
	return _fate_runtime.get_black_star_brand_cast_cost(member_id) if _fate_runtime != null else MISFORTUNE_SERVICE_SCRIPT.BLACK_STAR_BRAND_REPEAT_CALAMITY_COST


func has_misfortune_reason(member_id: StringName, reason_id: StringName) -> bool:
	return _fate_runtime.has_misfortune_reason(member_id, reason_id) if _fate_runtime != null else false


func get_fate_runtime():
	return _fate_runtime


func get_misfortune_skill_cast_block_reason(active_unit: BattleUnitState, skill_id: StringName) -> String:
	if _fate_runtime == null:
		return MISFORTUNE_SERVICE_SCRIPT.get_skill_sidecar_missing_message(skill_id)
	return _fate_runtime.get_misfortune_skill_cast_block_reason(active_unit, skill_id)


func consume_misfortune_skill_cast(active_unit: BattleUnitState, skill_id: StringName) -> Dictionary:
	if _fate_runtime == null:
		return {
			"ok": false,
			"message": MISFORTUNE_SERVICE_SCRIPT.get_skill_sidecar_missing_message(skill_id),
		}
	return _fate_runtime.consume_misfortune_skill_cast(active_unit, skill_id)


func handle_misfortune_trigger(reason_id: StringName, payload: Dictionary = {}) -> Variant:
	return _fate_runtime.handle_misfortune_trigger(reason_id, payload) if _fate_runtime != null else {}


func handle_fate_battle_resolution(battle_state: BattleState, battle_resolution_result) -> Dictionary:
	return _fate_runtime.handle_battle_resolution(battle_state, battle_resolution_result) if _fate_runtime != null else {}


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
	return _fate_runtime.handle_member_boss_phase_changed(member_id, phase_id) if _fate_runtime != null else {}


func _ensure_sidecars_ready() -> void:
	_terrain_effect_system.setup(self)
	if _attack_check_policy_service == null:
		_attack_check_policy_service = BATTLE_ATTACK_CHECK_POLICY_SERVICE_SCRIPT.new()
	_attack_check_policy_service.setup(self, _hit_resolver, _terrain_effect_system)
	if _skill_outcome_committer == null:
		_skill_outcome_committer = BATTLE_SKILL_OUTCOME_COMMITTER_SCRIPT.new()
	_skill_outcome_committer.setup(self)
	if _special_profile_commit_adapter == null:
		_special_profile_commit_adapter = BATTLE_SPECIAL_PROFILE_COMMIT_ADAPTER_SCRIPT.new()
	_special_profile_commit_adapter.setup(self, _skill_outcome_committer)
	_battle_rating_system.setup(self, _skill_mastery_service)
	_unit_factory.setup(self)
	_charge_resolver.setup(self, _skill_mastery_service)
	_repeat_attack_resolver.setup(self, _skill_mastery_service)
	_change_equipment_resolver.setup(self)
	_loot_resolver.setup(self)
	_skill_turn_resolver.setup(self)
	_metrics_collector.setup(self)
	_shield_service.setup(self)
	_ground_effect_service.setup(self)
	_special_skill_resolver.setup(self)
	_movement_service.setup(self)
	_layered_barrier_service.setup(self)
	_timeline_driver.setup(self)
	_skill_orchestrator.setup(self)


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
	_ensure_sidecars_ready()
	return _movement_service.get_unit_reachable_move_coords(unit_state)


func end_battle(result: Dictionary = {}) -> void:
	if _state == null:
		return
	if _character_gateway != null and bool(result.get("commit_progression", false)):
		for ally_unit_id in _state.ally_unit_ids:
			var unit_state := _state.units.get(ally_unit_id) as BattleUnitState
			if unit_state == null:
				continue
			if unit_state.is_alive:
				_character_gateway.commit_battle_resources(
					unit_state.source_member_id,
					unit_state.current_hp,
					unit_state.current_mp,
					unit_state.current_aura
				)
			else:
				_character_gateway.commit_battle_death(unit_state.source_member_id)
		_character_gateway.flush_after_battle()
	if _battle_resolution_result == null and not _battle_resolution_result_consumed and _state.phase == &"battle_ended":
		_battle_resolution_result = _build_battle_resolution_result()


func get_battle_resolution_result():
	if _battle_resolution_result_consumed:
		return null
	if _battle_resolution_result == null and _state != null and _state.phase == &"battle_ended":
		_battle_resolution_result = _build_battle_resolution_result()
	return _battle_resolution_result


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


func allocate_equipment_instance_id() -> StringName:
	if not _equipment_instance_id_allocator.is_valid():
		return &""
	return ProgressionDataUtils.to_string_name(_equipment_instance_id_allocator.call())


func get_damage_resolver():
	return _damage_resolver


func configure_damage_resolver_for_tests(damage_resolver: BattleDamageResolver) -> void:
	_damage_resolver = damage_resolver if damage_resolver != null else BATTLE_DAMAGE_RESOLVER_SCRIPT.new()
	if _damage_resolver != null and _damage_resolver.has_method("set_skill_defs"):
		_damage_resolver.set_skill_defs(_skill_defs)
	if _damage_resolver != null and _damage_resolver.has_method("set_hit_resolver"):
		_damage_resolver.set_hit_resolver(_hit_resolver)
	if _ai_service != null:
		_ai_service.setup(_enemy_ai_brains, _damage_resolver)
	if _fate_runtime != null:
		_fate_runtime.setup(_character_gateway, get_fate_event_bus(), self, Callable(self, "_find_unit_by_member_id"))
	_change_equipment_resolver.setup(self)
	_loot_resolver.setup(self)
	_skill_turn_resolver.setup(self)
	_metrics_collector.setup(self)
	_shield_service.setup(self)
	_ground_effect_service.setup(self)
	_special_skill_resolver.setup(self)
	_movement_service.setup(self)
	_layered_barrier_service.setup(self)
	_timeline_driver.setup(self)
	_skill_orchestrator.setup(self)


func get_fate_event_bus():
	return _damage_resolver.get_fate_event_bus() if _damage_resolver != null else null


func get_hit_resolver():
	return _hit_resolver


func get_attack_check_policy_service():
	if _attack_check_policy_service == null:
		_attack_check_policy_service = BATTLE_ATTACK_CHECK_POLICY_SERVICE_SCRIPT.new()
	_attack_check_policy_service.setup(self, _hit_resolver, _terrain_effect_system)
	return _attack_check_policy_service


func configure_hit_resolver_for_tests(hit_resolver: BattleHitResolver) -> void:
	_hit_resolver = hit_resolver if hit_resolver != null else BATTLE_HIT_RESOLVER_SCRIPT.new()
	if _damage_resolver != null and _damage_resolver.has_method("set_hit_resolver"):
		_damage_resolver.set_hit_resolver(_hit_resolver)
	if _attack_check_policy_service != null and _attack_check_policy_service.has_method("setup"):
		_attack_check_policy_service.setup(self, _hit_resolver, _terrain_effect_system)
	if _meteor_swarm_resolver != null and _meteor_swarm_resolver.has_method("setup"):
		_meteor_swarm_resolver.setup(self, _attack_check_policy_service)
	if _skill_outcome_committer != null and _skill_outcome_committer.has_method("setup"):
		_skill_outcome_committer.setup(self)
	if _special_profile_commit_adapter != null and _special_profile_commit_adapter.has_method("setup"):
		_special_profile_commit_adapter.setup(self, _skill_outcome_committer)


func get_terrain_generator():
	return _terrain_generator


func get_skill_defs() -> Dictionary:
	return _skill_defs


func get_special_profile_registry_snapshot() -> Dictionary:
	return _special_profile_registry_snapshot.duplicate(true)


func _has_special_profile(skill_def: SkillDef, profile_id: StringName) -> bool:
	if skill_def == null or skill_def.combat_profile == null:
		return false
	return skill_def.combat_profile.special_resolution_profile_id == profile_id


func _append_special_profile_gate_block(batch: BattleEventBatch, gate_result) -> void:
	if batch == null:
		return
	var message := "该禁咒配置未通过校验，暂时无法施放。"
	if gate_result != null and gate_result.get("player_message") != null:
		message = String(gate_result.player_message)
	if message.is_empty():
		message = "该禁咒配置未通过校验，暂时无法施放。"
	batch.log_lines.append(message)


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


func _collect_ai_trace_decision_target_unit_ids(decision: BattleAiDecision, turn_trace: Dictionary) -> Array[StringName]:
	var unit_ids: Array[StringName] = []
	if decision != null and decision.command != null:
		_add_ai_trace_unit_id(unit_ids, decision.command.target_unit_id)
		_add_ai_trace_unit_ids(unit_ids, decision.command.target_unit_ids)
	_append_ai_trace_score_target_unit_ids(unit_ids, turn_trace.get("score_input", {}))
	return unit_ids


func _append_ai_trace_score_target_unit_ids(unit_ids: Array[StringName], score_value: Variant) -> void:
	if score_value is not Dictionary:
		return
	var score: Dictionary = score_value
	_add_ai_trace_unit_ids(unit_ids, score.get("target_unit_ids", []))
	_add_ai_trace_unit_id(unit_ids, score.get("target_unit_id", ""))


func _add_ai_trace_unit_ids(unit_ids: Array[StringName], raw_unit_ids: Variant) -> void:
	if raw_unit_ids is Array:
		for raw_unit_id in raw_unit_ids:
			_add_ai_trace_unit_id(unit_ids, raw_unit_id)
		return
	_add_ai_trace_unit_id(unit_ids, raw_unit_ids)


func _add_ai_trace_unit_id(unit_ids: Array[StringName], raw_unit_id: Variant) -> void:
	var unit_id := ProgressionDataUtils.to_string_name(raw_unit_id)
	if unit_id == &"" or unit_ids.has(unit_id):
		return
	unit_ids.append(unit_id)


func _build_ai_trace_unit_snapshot_map() -> Dictionary:
	var snapshots: Dictionary = {}
	if _state == null:
		return snapshots
	for raw_unit_id in _state.units.keys():
		var unit_state := _state.units.get(raw_unit_id) as BattleUnitState
		if unit_state == null:
			continue
		snapshots[String(unit_state.unit_id)] = _build_ai_trace_unit_snapshot(unit_state)
	return snapshots


func _build_ai_trace_unit_snapshot(unit_state: BattleUnitState) -> Dictionary:
	if unit_state == null:
		return {}
	var hp_max := 0
	var mp_max := 0
	var stamina_max := 0
	var aura_max := 0
	if unit_state.attribute_snapshot != null:
		hp_max = int(unit_state.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX))
		mp_max = int(unit_state.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.MP_MAX))
		stamina_max = int(unit_state.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.STAMINA_MAX))
		aura_max = int(unit_state.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.AURA_MAX))
	return {
		"unit_id": String(unit_state.unit_id),
		"display_name": String(unit_state.display_name),
		"faction_id": String(unit_state.faction_id),
		"coord": _format_ai_trace_coord(unit_state.coord),
		"alive": bool(unit_state.is_alive),
		"hp": int(unit_state.current_hp),
		"hp_max": maxi(hp_max, 1),
		"mp": int(unit_state.current_mp),
		"mp_max": maxi(mp_max, 0),
		"stamina": int(unit_state.current_stamina),
		"stamina_max": maxi(stamina_max, 0),
		"aura": int(unit_state.current_aura),
		"aura_max": maxi(aura_max, 0),
		"ap": int(unit_state.current_ap),
		"move_points": int(unit_state.current_move_points),
		"shield_hp": int(unit_state.current_shield_hp),
		"shield_max_hp": int(unit_state.shield_max_hp),
	}


func _build_ai_trace_snapshots_for_unit_ids(unit_ids: Array[StringName], snapshot_map: Dictionary) -> Array[Dictionary]:
	var snapshots: Array[Dictionary] = []
	for unit_id in unit_ids:
		var key := String(unit_id)
		if not snapshot_map.has(key):
			continue
		var snapshot_variant = snapshot_map.get(key, {})
		if snapshot_variant is Dictionary:
			snapshots.append((snapshot_variant as Dictionary).duplicate(true))
	return snapshots


func _build_ai_trace_execution_result(
	decision: BattleAiDecision,
	decision_batch: BattleEventBatch,
	unit_snapshots_before: Dictionary,
	decision_target_unit_ids: Array[StringName]
) -> Dictionary:
	var unit_snapshots_after := _build_ai_trace_unit_snapshot_map()
	var tracked_unit_ids: Array[StringName] = []
	for unit_id in decision_target_unit_ids:
		_add_ai_trace_unit_id(tracked_unit_ids, unit_id)
	if decision != null and decision.command != null:
		_add_ai_trace_unit_id(tracked_unit_ids, decision.command.unit_id)
	if decision_batch != null:
		_add_ai_trace_unit_ids(tracked_unit_ids, decision_batch.changed_unit_ids)
	var command := decision.command if decision != null else null
	return {
		"command_type": String(command.command_type) if command != null else "",
		"skill_id": String(command.skill_id) if command != null else "",
		"skill_variant_id": String(command.skill_variant_id) if command != null else "",
		"changed_unit_ids": _ai_trace_stringify_unit_ids(decision_batch.changed_unit_ids if decision_batch != null else []),
		"tracked_unit_ids": _ai_trace_stringify_unit_ids(tracked_unit_ids),
		"unit_results": _build_ai_trace_unit_results(tracked_unit_ids, unit_snapshots_before, unit_snapshots_after),
		"log_lines": decision_batch.log_lines.duplicate(true) if decision_batch != null else [],
		"report_entries": decision_batch.report_entries.duplicate(true) if decision_batch != null else [],
	}


func _build_ai_trace_unit_results(
	unit_ids: Array[StringName],
	unit_snapshots_before: Dictionary,
	unit_snapshots_after: Dictionary
) -> Array[Dictionary]:
	var results: Array[Dictionary] = []
	for unit_id in unit_ids:
		var key := String(unit_id)
		var before: Dictionary = unit_snapshots_before.get(key, {}) if unit_snapshots_before.get(key, {}) is Dictionary else {}
		var after: Dictionary = unit_snapshots_after.get(key, {}) if unit_snapshots_after.get(key, {}) is Dictionary else {}
		if before.is_empty() and after.is_empty():
			continue
		var hp_before := int(before.get("hp", after.get("hp", 0)))
		var hp_after := int(after.get("hp", hp_before))
		var shield_before := int(before.get("shield_hp", after.get("shield_hp", 0)))
		var shield_after := int(after.get("shield_hp", shield_before))
		var before_alive := bool(before.get("alive", false))
		var after_alive := bool(after.get("alive", before_alive))
		var coord_before := String(before.get("coord", after.get("coord", "")))
		var coord_after := String(after.get("coord", coord_before))
		results.append({
			"unit_id": key,
			"before": before,
			"after": after,
			"hp_delta": hp_after - hp_before,
			"hp_damage": maxi(hp_before - hp_after, 0),
			"hp_healing": maxi(hp_after - hp_before, 0),
			"shield_delta": shield_after - shield_before,
			"shield_damage": maxi(shield_before - shield_after, 0),
			"shield_restored": maxi(shield_after - shield_before, 0),
			"killed": before_alive and not after_alive,
			"revived": not before_alive and after_alive,
			"moved": coord_before != coord_after,
		})
	return results


func _ai_trace_stringify_unit_ids(unit_ids: Variant) -> Array[String]:
	var results: Array[String] = []
	if unit_ids is not Array:
		return results
	for raw_unit_id in unit_ids:
		var unit_id := ProgressionDataUtils.to_string_name(raw_unit_id)
		if unit_id == &"":
			continue
		results.append(String(unit_id))
	return results


func _format_ai_trace_coord(coord: Vector2i) -> String:
	return "(%d, %d)" % [coord.x, coord.y]


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
	if _fate_runtime != null:
		_fate_runtime.handle_applied_statuses(target_unit, status_effect_ids)


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
	_ensure_sidecars_ready()
	_metrics_collector._initialize_battle_metrics()


func _build_unit_metric_entry(unit_state: BattleUnitState) -> Dictionary:
	_ensure_sidecars_ready()
	return _metrics_collector._build_unit_metric_entry(unit_state)


func _ensure_unit_metric_entry(unit_state: BattleUnitState) -> Dictionary:
	_ensure_sidecars_ready()
	return _metrics_collector._ensure_unit_metric_entry(unit_state)


func _ensure_faction_metric_entry(faction_id: StringName) -> Dictionary:
	_ensure_sidecars_ready()
	return _metrics_collector._ensure_faction_metric_entry(faction_id)


func _record_turn_started(unit_state: BattleUnitState) -> void:
	_ensure_sidecars_ready()
	_metrics_collector._record_turn_started(unit_state)


func _record_action_issued(unit_state: BattleUnitState, command_type: StringName, ap_cost: int = 0) -> void:
	_ensure_sidecars_ready()
	_metrics_collector._record_action_issued(unit_state, command_type, ap_cost)


func _record_skill_attempt(unit_state: BattleUnitState, skill_id: StringName) -> void:
	_ensure_sidecars_ready()
	_metrics_collector._record_skill_attempt(unit_state, skill_id)


func _record_skill_success(unit_state: BattleUnitState, skill_id: StringName) -> void:
	_ensure_sidecars_ready()
	_metrics_collector._record_skill_success(unit_state, skill_id)


func _record_effect_metrics(
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	damage: int,
	healing: int,
	kill_count: int
) -> void:
	_ensure_sidecars_ready()
	_metrics_collector._record_effect_metrics(source_unit, target_unit, damage, healing, kill_count)


func _record_unit_defeated(unit_state: BattleUnitState) -> void:
	_ensure_sidecars_ready()
	_metrics_collector._record_unit_defeated(unit_state)


func _increment_metric_count(metric_map: Dictionary, key: String, delta: int) -> void:
	_ensure_sidecars_ready()
	_metrics_collector._increment_metric_count(metric_map, key, delta)


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
	if _metrics_collector != null:
		_metrics_collector.dispose()
	if _shield_service != null:
		_shield_service.dispose()
	if _ground_effect_service != null:
		_ground_effect_service.dispose()
	if _special_skill_resolver != null:
		_special_skill_resolver.dispose()
	if _movement_service != null:
		_movement_service.dispose()
	if _layered_barrier_service != null:
		_layered_barrier_service.dispose()
	if _timeline_driver != null:
		_timeline_driver.dispose()
	if _skill_orchestrator != null:
		_skill_orchestrator.dispose()
	if _meteor_swarm_resolver != null and _meteor_swarm_resolver.has_method("dispose"):
		_meteor_swarm_resolver.dispose()
	if _attack_check_policy_service != null and _attack_check_policy_service.has_method("dispose"):
		_attack_check_policy_service.dispose()
	if _skill_outcome_committer != null and _skill_outcome_committer.has_method("dispose"):
		_skill_outcome_committer.dispose()
	if _special_profile_commit_adapter != null and _special_profile_commit_adapter.has_method("dispose"):
		_special_profile_commit_adapter.dispose()
	_meteor_swarm_resolver = null
	_special_profile_gate = null
	_attack_check_policy_service = null
	_skill_preview_service = null
	_skill_outcome_committer = null
	_special_profile_commit_adapter = null
	if _skill_mastery_service != null:
		_skill_mastery_service.clear()
	if _fate_runtime != null:
		_fate_runtime.dispose()
	_battle_rating_stats.clear()
	_pending_post_battle_character_rewards.clear()
	_active_loot_entries.clear()
	_looted_defeated_unit_ids.clear()
	_ai_turn_traces.clear()
	_ai_action_plans_by_unit_id.clear()
	_battle_metrics.clear()
	calamity_by_member_id.clear()
	_battle_resolution_result = null
	_battle_resolution_result_consumed = false
	_terrain_effect_nonce = 0
	_ai_trace_enabled = false
	_character_gateway = null
	_skill_defs = {}
	_special_profile_registry_snapshot = {}
	_item_defs = {}
	_enemy_templates = {}
	_enemy_ai_brains = {}
	_encounter_builder = null
	_equipment_drop_service = null
	_equipment_instance_id_allocator = Callable()
	if _state != null:
		_state.cells.clear()
		_state.units.clear()
		_state.ally_unit_ids.clear()
		_state.enemy_unit_ids.clear()
		if _state.timeline != null:
			_state.timeline.ready_unit_ids.clear()
	_state = null


func _place_units(units: Array, spawn_coords: Array, is_ally: bool, spawn_side: StringName = &"") -> bool:
	var placed_units: Array[BattleUnitState] = []
	for index in range(units.size()):
		var unit_state := units[index] as BattleUnitState
		if unit_state == null:
			continue
		unit_state.refresh_footprint()
		var preferred_coords: Array[Vector2i] = []
		if index < spawn_coords.size():
			preferred_coords.append(spawn_coords[index])
		for spawn_coord in spawn_coords:
			var coord: Vector2i = spawn_coord
			if not preferred_coords.has(coord):
				preferred_coords.append(coord)
		var placement_coord := _find_spawn_anchor(unit_state, preferred_coords, spawn_side)
		if placement_coord == Vector2i(-1, -1):
			_clear_spawn_placed_units(placed_units, is_ally)
			return false
		if not _place_spawn_unit_at_anchor(unit_state, placement_coord):
			_clear_spawn_placed_units(placed_units, is_ally)
			return false
		if is_ally:
			_state.ally_unit_ids.append(unit_state.unit_id)
		else:
			_state.enemy_unit_ids.append(unit_state.unit_id)
		placed_units.append(unit_state)
	return true


func _clear_spawn_placed_units(placed_units: Array[BattleUnitState], is_ally: bool) -> void:
	if _state == null:
		return
	for unit_state in placed_units:
		if unit_state == null:
			continue
		_grid_service.clear_unit_occupancy(_state, unit_state)
		_state.units.erase(unit_state.unit_id)
		if is_ally:
			_state.ally_unit_ids.erase(unit_state.unit_id)
		else:
			_state.enemy_unit_ids.erase(unit_state.unit_id)


func _place_spawn_unit_at_anchor(unit_state: BattleUnitState, coord: Vector2i) -> bool:
	if _state == null or unit_state == null:
		return false
	if not _can_place_spawn_anchor(unit_state, coord):
		return false
	unit_state.set_anchor_coord(coord)
	_state.units[unit_state.unit_id] = unit_state
	_grid_service.set_occupants(_state, unit_state.occupied_coords, unit_state.unit_id)
	return true


func _find_spawn_anchor(unit_state: BattleUnitState, preferred_coords: Array[Vector2i], spawn_side: StringName = &"") -> Vector2i:
	if _state == null or unit_state == null:
		return Vector2i(-1, -1)
	var best_coord := Vector2i(-1, -1)
	var best_score := -2147483647
	for preferred_index in range(preferred_coords.size()):
		var coord: Vector2i = preferred_coords[preferred_index]
		if not _can_place_spawn_anchor(unit_state, coord, spawn_side):
			continue
		var score := _score_spawn_anchor(unit_state, coord, preferred_index)
		if score > best_score:
			best_score = score
			best_coord = coord
	if best_coord != Vector2i(-1, -1):
		return best_coord
	for preferred_coord in preferred_coords:
		var coord: Vector2i = preferred_coord
		if _can_place_spawn_anchor(unit_state, coord, spawn_side):
			return coord
	for y in range(_state.map_size.y):
		for x in range(_state.map_size.x):
			var coord := Vector2i(x, y)
			if _can_place_spawn_anchor(unit_state, coord, spawn_side):
				return coord
	return Vector2i(-1, -1)


func _can_place_spawn_anchor(unit_state: BattleUnitState, coord: Vector2i, spawn_side: StringName = &"") -> bool:
	if _state == null or unit_state == null:
		return false
	if not _grid_service.can_place_footprint(_state, coord, unit_state.footprint_size, unit_state.unit_id):
		return false
	if spawn_side != &"" and not _footprint_matches_spawn_side(unit_state, coord, spawn_side):
		return false
	for footprint_coord in _grid_service.get_unit_target_coords(unit_state, coord):
		var cell := _grid_service.get_cell(_state, footprint_coord)
		if cell == null or BattleTerrainRules.is_water_terrain(cell.base_terrain):
			return false
	return true


func _resolve_spawn_side_from_coords(spawn_coords: Array) -> StringName:
	if _state == null or _get_long_edge_side_extent() <= 1:
		return &""
	var near_count := 0
	var far_count := 0
	for coord_variant in spawn_coords:
		if coord_variant is not Vector2i:
			continue
		var coord: Vector2i = coord_variant
		if _coord_matches_spawn_side(coord, SPAWN_SIDE_NEAR_LONG_EDGE):
			near_count += 1
		elif _coord_matches_spawn_side(coord, SPAWN_SIDE_FAR_LONG_EDGE):
			far_count += 1
	if near_count == 0 and far_count == 0:
		return &""
	return SPAWN_SIDE_NEAR_LONG_EDGE if near_count >= far_count else SPAWN_SIDE_FAR_LONG_EDGE


func _get_opposite_spawn_side(spawn_side: StringName) -> StringName:
	match spawn_side:
		SPAWN_SIDE_NEAR_LONG_EDGE:
			return SPAWN_SIDE_FAR_LONG_EDGE
		SPAWN_SIDE_FAR_LONG_EDGE:
			return SPAWN_SIDE_NEAR_LONG_EDGE
		_:
			return &""


func _footprint_matches_spawn_side(unit_state: BattleUnitState, coord: Vector2i, spawn_side: StringName) -> bool:
	if _state == null or unit_state == null:
		return false
	for footprint_coord in _grid_service.get_unit_target_coords(unit_state, coord):
		if not _coord_matches_spawn_side(footprint_coord, spawn_side):
			return false
	return true


func _coord_matches_spawn_side(coord: Vector2i, spawn_side: StringName) -> bool:
	if _state == null or _get_long_edge_side_extent() <= 1:
		return true
	var side_value := _get_long_edge_side_axis_value(coord)
	var split_value := int(floor(float(_get_long_edge_side_extent()) * 0.5))
	match spawn_side:
		SPAWN_SIDE_NEAR_LONG_EDGE:
			return side_value < split_value
		SPAWN_SIDE_FAR_LONG_EDGE:
			return side_value >= split_value
		_:
			return true


func _get_long_edge_side_axis_value(coord: Vector2i) -> int:
	if _state == null:
		return 0
	return coord.y if _state.map_size.x >= _state.map_size.y else coord.x


func _get_long_edge_side_extent() -> int:
	if _state == null:
		return 0
	return _state.map_size.y if _state.map_size.x >= _state.map_size.y else _state.map_size.x


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
	_ensure_sidecars_ready()
	return _movement_service._get_move_cost_for_unit_target(unit_state, target_coord, allow_quickstep_bonus)


func _get_move_cost_for_unit_target_without_quickstep(
	unit_state: BattleUnitState,
	target_coord: Vector2i
) -> int:
	_ensure_sidecars_ready()
	return _movement_service._get_move_cost_for_unit_target_without_quickstep(unit_state, target_coord)


func _get_move_path_cost(unit_state: BattleUnitState, anchor_path: Array[Vector2i]) -> int:
	_ensure_sidecars_ready()
	return _movement_service._get_move_path_cost(unit_state, anchor_path)


func _get_status_move_cost_delta(unit_state: BattleUnitState) -> int:
	_ensure_sidecars_ready()
	return _movement_service._get_status_move_cost_delta(unit_state)


func _resolve_move_path_result(active_unit: BattleUnitState, target_coord: Vector2i) -> Dictionary:
	_ensure_sidecars_ready()
	return _movement_service._resolve_move_path_result(active_unit, target_coord)


func _get_available_move_points(unit_state: BattleUnitState) -> int:
	_ensure_sidecars_ready()
	return _movement_service._get_available_move_points(unit_state)


func _is_normal_movement_locked(unit_state: BattleUnitState) -> bool:
	_ensure_sidecars_ready()
	return _movement_service._is_normal_movement_locked(unit_state)


func _handle_move_command(active_unit: BattleUnitState, command: BattleCommand, batch: BattleEventBatch) -> void:
	_ensure_sidecars_ready()
	_movement_service._handle_move_command(active_unit, command, batch)


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
	_ensure_sidecars_ready()
	return _movement_service._move_unit_along_validated_path(active_unit, anchor_path, target_coord, batch)


func _handle_skill_command(active_unit: BattleUnitState, command: BattleCommand, batch: BattleEventBatch) -> void:
	_ensure_sidecars_ready()
	_skill_orchestrator._handle_skill_command(active_unit, command, batch)


func _preview_skill_command(active_unit: BattleUnitState, command: BattleCommand, preview: BattlePreview) -> void:
	_ensure_sidecars_ready()
	_skill_orchestrator._preview_skill_command(active_unit, command, preview)


func _preview_unit_skill_command(
	active_unit: BattleUnitState,
	command: BattleCommand,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	preview: BattlePreview
) -> void:
	_ensure_sidecars_ready()
	_skill_orchestrator._preview_unit_skill_command(active_unit, command, skill_def, cast_variant, preview)


func _preview_ground_skill_command(
	active_unit: BattleUnitState,
	command: BattleCommand,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	preview: BattlePreview
) -> void:
	_ensure_sidecars_ready()
	_skill_orchestrator._preview_ground_skill_command(active_unit, command, skill_def, cast_variant, preview)


func _build_unit_skill_hit_preview(
	active_unit: BattleUnitState,
	target_units: Array,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef
) -> Dictionary:
	_ensure_sidecars_ready()
	return _skill_orchestrator._build_unit_skill_hit_preview(active_unit, target_units, skill_def, cast_variant)


func _build_unit_skill_damage_preview(
	active_unit: BattleUnitState,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef
) -> Dictionary:
	_ensure_sidecars_ready()
	return _skill_orchestrator._build_unit_skill_damage_preview(active_unit, skill_def, cast_variant)


func _append_damage_preview_line(preview: BattlePreview) -> void:
	_ensure_sidecars_ready()
	_skill_orchestrator._append_damage_preview_line(preview)


func summarize_damage_result(result: Dictionary) -> Dictionary:
	_ensure_sidecars_ready()
	return _skill_orchestrator.summarize_damage_result(result)


func build_damage_absorb_reason_text(summary: Dictionary) -> String:
	_ensure_sidecars_ready()
	return _skill_orchestrator.build_damage_absorb_reason_text(summary)


func append_damage_result_log_lines(
	batch: BattleEventBatch,
	subject_label: String,
	target_display_name: String,
	result: Dictionary
) -> void:
	_ensure_sidecars_ready()
	_skill_orchestrator.append_damage_result_log_lines(batch, subject_label, target_display_name, result)


func _build_unit_skill_resolution_preview_lines(
	active_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef
) -> Array[String]:
	_ensure_sidecars_ready()
	return _skill_orchestrator._build_unit_skill_resolution_preview_lines(active_unit, target_unit, skill_def, cast_variant)


func _build_skill_log_subject_label(
	source_unit: BattleUnitState,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef = null
) -> String:
	_ensure_sidecars_ready()
	return _skill_orchestrator._build_skill_log_subject_label(source_unit, skill_def, cast_variant)


func _handle_unit_skill_command(
	active_unit: BattleUnitState,
	command: BattleCommand,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	batch: BattleEventBatch
) -> bool:
	_ensure_sidecars_ready()
	return _skill_orchestrator._handle_unit_skill_command(active_unit, command, skill_def, cast_variant, batch)


func _should_route_skill_command_to_unit_targeting(skill_def: SkillDef, command: BattleCommand) -> bool:
	_ensure_sidecars_ready()
	return _skill_orchestrator._should_route_skill_command_to_unit_targeting(skill_def, command)


func _validate_unit_skill_targets(
	active_unit: BattleUnitState,
	command: BattleCommand,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef = null
) -> Dictionary:
	_ensure_sidecars_ready()
	return _skill_orchestrator._validate_unit_skill_targets(active_unit, command, skill_def, cast_variant)


func _normalize_target_unit_ids(command: BattleCommand) -> Array[StringName]:
	_ensure_sidecars_ready()
	return _skill_orchestrator._normalize_target_unit_ids(command)


func _sort_target_unit_ids_for_execution(target_unit_ids: Array[StringName]) -> Array[StringName]:
	_ensure_sidecars_ready()
	return _skill_orchestrator._sort_target_unit_ids_for_execution(target_unit_ids)


func _is_multi_unit_skill(skill_def: SkillDef) -> bool:
	_ensure_sidecars_ready()
	return _skill_orchestrator._is_multi_unit_skill(skill_def)


func _can_skill_target_unit(active_unit: BattleUnitState, target_unit: BattleUnitState, skill_def: SkillDef, require_ap: bool = true) -> bool:
	_ensure_sidecars_ready()
	return _skill_orchestrator._can_skill_target_unit(active_unit, target_unit, skill_def, require_ap)


func _resolve_unit_skill_effect_result(
	active_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	effect_defs: Array[CombatEffectDef]
) -> Dictionary:
	_ensure_sidecars_ready()
	return _skill_orchestrator._resolve_unit_skill_effect_result(active_unit, target_unit, skill_def, effect_defs)


func _should_resolve_unit_skill_as_fate_attack(
	active_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	effect_defs: Array[CombatEffectDef]
) -> bool:
	_ensure_sidecars_ready()
	return _skill_orchestrator._should_resolve_unit_skill_as_fate_attack(active_unit, target_unit, skill_def, effect_defs)


func _apply_unit_skill_result(
	active_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	effect_defs: Array[CombatEffectDef],
	batch: BattleEventBatch,
	spell_control_context: Dictionary = {}
) -> bool:
	_ensure_sidecars_ready()
	return _skill_orchestrator._apply_unit_skill_result(active_unit, target_unit, skill_def, cast_variant, effect_defs, batch, spell_control_context)


func _apply_chain_damage_effects(
	source_unit: BattleUnitState,
	primary_target: BattleUnitState,
	skill_def: SkillDef,
	effect_defs: Array[CombatEffectDef],
	primary_result: Dictionary,
	batch: BattleEventBatch,
	skill_subject: String,
	spell_control_context: Dictionary = {}
) -> void:
	_ensure_sidecars_ready()
	_skill_orchestrator._apply_chain_damage_effects(source_unit, primary_target, skill_def, effect_defs, primary_result, batch, skill_subject, spell_control_context)


func _collect_chain_damage_effect_defs(effect_defs: Array[CombatEffectDef]) -> Array[CombatEffectDef]:
	_ensure_sidecars_ready()
	return _skill_orchestrator._collect_chain_damage_effect_defs(effect_defs)


func _get_effect_params(effect_def: CombatEffectDef) -> Dictionary:
	_ensure_sidecars_ready()
	return _skill_orchestrator._get_effect_params(effect_def)


func _build_chain_target_effect_defs(
	effect_defs: Array[CombatEffectDef],
	chain_effect: CombatEffectDef
) -> Array[CombatEffectDef]:
	_ensure_sidecars_ready()
	return _skill_orchestrator._build_chain_target_effect_defs(effect_defs, chain_effect)


func _collect_chain_damage_targets(
	source_unit: BattleUnitState,
	primary_target: BattleUnitState,
	skill_def: SkillDef,
	chain_effect: CombatEffectDef,
	spell_control_context: Dictionary = {}
) -> Array[BattleUnitState]:
	_ensure_sidecars_ready()
	return _skill_orchestrator._collect_chain_damage_targets(source_unit, primary_target, skill_def, chain_effect, spell_control_context)


func _resolve_chain_damage_radius(primary_target: BattleUnitState, chain_effect: CombatEffectDef, spell_control_context: Dictionary = {}) -> int:
	_ensure_sidecars_ready()
	return _skill_orchestrator._resolve_chain_damage_radius(primary_target, chain_effect, spell_control_context)


func _unit_stands_on_terrain_effect(unit_state: BattleUnitState, terrain_effect_id: StringName) -> bool:
	_ensure_sidecars_ready()
	return _skill_orchestrator._unit_stands_on_terrain_effect(unit_state, terrain_effect_id)


func _is_unit_in_chain_radius(
	primary_target: BattleUnitState,
	candidate: BattleUnitState,
	radius: int,
	chain_effect: CombatEffectDef
) -> bool:
	_ensure_sidecars_ready()
	return _skill_orchestrator._is_unit_in_chain_radius(primary_target, candidate, radius, chain_effect)


func _is_within_chain_radius(primary_target: BattleUnitState, candidate: BattleUnitState, max_radius: int) -> bool:
	_ensure_sidecars_ready()
	return _skill_orchestrator._is_within_chain_radius(primary_target, candidate, max_radius)


func _is_chain_height_valid(from_unit: BattleUnitState, to_unit: BattleUnitState) -> bool:
	_ensure_sidecars_ready()
	return _skill_orchestrator._is_chain_height_valid(from_unit, to_unit)


func _get_line_coords(from: Vector2i, to: Vector2i) -> Array[Vector2i]:
	_ensure_sidecars_ready()
	return _skill_orchestrator._get_line_coords(from, to)


func _is_chain_path_clear(source_unit: BattleUnitState, target_unit: BattleUnitState) -> bool:
	_ensure_sidecars_ready()
	return _skill_orchestrator._is_chain_path_clear(source_unit, target_unit)


func _apply_on_kill_gain_resources_effects(
	source_unit: BattleUnitState,
	defeated_unit: BattleUnitState,
	skill_def: SkillDef,
	effect_defs: Array[CombatEffectDef],
	batch: BattleEventBatch
) -> void:
	_ensure_sidecars_ready()
	_special_skill_resolver._apply_on_kill_gain_resources_effects(source_unit, defeated_unit, skill_def, effect_defs, batch)


func _apply_unit_skill_special_effects(
	active_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	effect_defs: Array[CombatEffectDef],
	batch: BattleEventBatch,
	forced_move_context: Dictionary = {}
) -> Dictionary:
	_ensure_sidecars_ready()
	return _special_skill_resolver._apply_unit_skill_special_effects(active_unit, target_unit, skill_def, cast_variant, effect_defs, batch, forced_move_context)


func _apply_doom_shift_effect(
	active_unit: BattleUnitState,
	target_unit: BattleUnitState,
	batch: BattleEventBatch
) -> Dictionary:
	_ensure_sidecars_ready()
	return _special_skill_resolver._apply_doom_shift_effect(active_unit, target_unit, batch)


func _swap_unit_positions(
	first_unit: BattleUnitState,
	second_unit: BattleUnitState,
	batch: BattleEventBatch
) -> bool:
	_ensure_sidecars_ready()
	return _special_skill_resolver._swap_unit_positions(first_unit, second_unit, batch)


func _apply_black_star_brand_effect(
	active_unit: BattleUnitState,
	target_unit: BattleUnitState
) -> Dictionary:
	_ensure_sidecars_ready()
	return _special_skill_resolver._apply_black_star_brand_effect(active_unit, target_unit)


func _set_runtime_status_effect(
	unit_state: BattleUnitState,
	status_id: StringName,
	duration_tu: int,
	source_unit_id: StringName = &"",
	power: int = 1,
	params: Dictionary = {}
) -> void:
	_ensure_sidecars_ready()
	_special_skill_resolver._set_runtime_status_effect(unit_state, status_id, duration_tu, source_unit_id, power, params)


func _clear_black_star_brand_statuses(unit_state: BattleUnitState) -> void:
	_ensure_sidecars_ready()
	_special_skill_resolver._clear_black_star_brand_statuses(unit_state)


func _is_black_star_brand_elite_target(unit_state: BattleUnitState) -> bool:
	_ensure_sidecars_ready()
	return _special_skill_resolver._is_black_star_brand_elite_target(unit_state)


func _is_elite_or_boss_target(unit_state: BattleUnitState) -> bool:
	_ensure_sidecars_ready()
	return _special_skill_resolver._is_elite_or_boss_target(unit_state)


func _is_boss_target(unit_state: BattleUnitState) -> bool:
	_ensure_sidecars_ready()
	return _special_skill_resolver._is_boss_target(unit_state)


func _is_black_star_brand_skill(skill_id: StringName) -> bool:
	_ensure_sidecars_ready()
	return _special_skill_resolver._is_black_star_brand_skill(skill_id)


func _is_black_contract_push_skill(skill_id: StringName) -> bool:
	_ensure_sidecars_ready()
	return _special_skill_resolver._is_black_contract_push_skill(skill_id)


func _is_doom_shift_skill(skill_id: StringName) -> bool:
	_ensure_sidecars_ready()
	return _special_skill_resolver._is_doom_shift_skill(skill_id)


func _is_black_crown_seal_skill(skill_id: StringName) -> bool:
	_ensure_sidecars_ready()
	return _special_skill_resolver._is_black_crown_seal_skill(skill_id)


func _clear_crown_break_seal_statuses(unit_state: BattleUnitState) -> void:
	_ensure_sidecars_ready()
	_special_skill_resolver._clear_crown_break_seal_statuses(unit_state)


func _is_crown_break_target_eligible(active_unit: BattleUnitState, target_unit: BattleUnitState) -> bool:
	_ensure_sidecars_ready()
	return _special_skill_resolver._is_crown_break_target_eligible(active_unit, target_unit)


func _is_crown_break_skill(skill_id: StringName) -> bool:
	_ensure_sidecars_ready()
	return _special_skill_resolver._is_crown_break_skill(skill_id)


func _is_doom_sentence_target_eligible(active_unit: BattleUnitState, target_unit: BattleUnitState) -> bool:
	_ensure_sidecars_ready()
	return _special_skill_resolver._is_doom_sentence_target_eligible(active_unit, target_unit)


func _is_black_crown_seal_target_eligible(active_unit: BattleUnitState, target_unit: BattleUnitState) -> bool:
	_ensure_sidecars_ready()
	return _special_skill_resolver._is_black_crown_seal_target_eligible(active_unit, target_unit)


func _is_doom_sentence_skill(skill_id: StringName) -> bool:
	_ensure_sidecars_ready()
	return _special_skill_resolver._is_doom_sentence_skill(skill_id)


func _get_unit_skill_target_validation_message(
	active_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef = null
) -> String:
	_ensure_sidecars_ready()
	return _skill_orchestrator._get_unit_skill_target_validation_message(active_unit, target_unit, skill_def, cast_variant)


func _get_body_size_category_override_validation_message(
	active_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef = null
) -> String:
	_ensure_sidecars_ready()
	return _skill_orchestrator._get_body_size_category_override_validation_message(active_unit, target_unit, skill_def, cast_variant)


func _skill_grants_guarding(skill_def: SkillDef) -> bool:
	_ensure_sidecars_ready()
	return _skill_orchestrator._skill_grants_guarding(skill_def)


func _apply_forced_move_effect(
	source_unit: BattleUnitState,
	unit_state: BattleUnitState,
	effect_def: CombatEffectDef,
	batch: BattleEventBatch,
	forced_move_context: Dictionary = {}
) -> int:
	_ensure_sidecars_ready()
	return _special_skill_resolver._apply_forced_move_effect(source_unit, unit_state, effect_def, batch, forced_move_context)


func _apply_body_size_category_override_effect(
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	effect_def: CombatEffectDef,
	batch: BattleEventBatch
) -> Dictionary:
	_ensure_sidecars_ready()
	return _special_skill_resolver._apply_body_size_category_override_effect(source_unit, target_unit, effect_def, batch)


func _blocks_enemy_forced_move(source_unit: BattleUnitState, target_unit: BattleUnitState) -> bool:
	_ensure_sidecars_ready()
	return _special_skill_resolver._blocks_enemy_forced_move(source_unit, target_unit)


func _record_vajra_body_mastery_from_incoming_damage(
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	result: Dictionary,
	batch: BattleEventBatch = null
) -> void:
	_ensure_sidecars_ready()
	_special_skill_resolver._record_vajra_body_mastery_from_incoming_damage(source_unit, target_unit, skill_def, result, batch)


func _pick_forced_move_coord(
	unit_state: BattleUnitState,
	mode: StringName,
	source_unit: BattleUnitState = null,
	forced_move_context: Dictionary = {}
) -> Vector2i:
	_ensure_sidecars_ready()
	return _special_skill_resolver._pick_forced_move_coord(unit_state, mode, source_unit, forced_move_context)


func _score_forced_move_coord(
	unit_state: BattleUnitState,
	candidate_coord: Vector2i,
	mode: StringName,
	source_unit: BattleUnitState = null,
	forced_move_context: Dictionary = {}
) -> int:
	_ensure_sidecars_ready()
	return _special_skill_resolver._score_forced_move_coord(unit_state, candidate_coord, mode, source_unit, forced_move_context)


func _collect_hostile_units_for(unit_state: BattleUnitState) -> Array[BattleUnitState]:
	_ensure_sidecars_ready()
	return _special_skill_resolver._collect_hostile_units_for(unit_state)


func _collect_unit_skill_effect_defs(
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	active_unit: BattleUnitState = null
) -> Array[CombatEffectDef]:
	_ensure_sidecars_ready()
	return _skill_orchestrator._collect_unit_skill_effect_defs(skill_def, cast_variant, active_unit)


func _handle_ground_skill_command(
	active_unit: BattleUnitState,
	command: BattleCommand,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	batch: BattleEventBatch
) -> bool:
	_ensure_sidecars_ready()
	return _skill_orchestrator._handle_ground_skill_command(active_unit, command, skill_def, cast_variant, batch)


func _resolve_ground_spell_control_after_cost(
	active_unit: BattleUnitState,
	skill_def: SkillDef,
	spent_mp: int,
	batch: BattleEventBatch
) -> Dictionary:
	_ensure_sidecars_ready()
	return _ground_effect_service._resolve_ground_spell_control_after_cost(active_unit, skill_def, spent_mp, batch)


func _resolve_unit_spell_control_after_cost(
	active_unit: BattleUnitState,
	skill_def: SkillDef,
	batch: BattleEventBatch
) -> Dictionary:
	_ensure_sidecars_ready()
	return _ground_effect_service._resolve_unit_spell_control_after_cost(active_unit, skill_def, batch)


func _apply_ground_precast_special_effects(
	active_unit: BattleUnitState,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	target_coords: Array[Vector2i],
	batch: BattleEventBatch
) -> bool:
	_ensure_sidecars_ready()
	return _ground_effect_service._apply_ground_precast_special_effects(active_unit, skill_def, cast_variant, target_coords, batch)


func _apply_ground_jump_relocation(
	active_unit: BattleUnitState,
	target_coords: Array[Vector2i],
	batch: BattleEventBatch
) -> bool:
	_ensure_sidecars_ready()
	return _ground_effect_service._apply_ground_jump_relocation(active_unit, target_coords, batch)


func _get_ground_jump_effect_def(skill_def: SkillDef, cast_variant: CombatCastVariantDef) -> CombatEffectDef:
	_ensure_sidecars_ready()
	return _ground_effect_service._get_ground_jump_effect_def(skill_def, cast_variant)


func _is_ground_jump_effect(effect_def: CombatEffectDef) -> bool:
	_ensure_sidecars_ready()
	return _ground_effect_service._is_ground_jump_effect(effect_def)


func _get_effect_forced_move_mode(effect_def: CombatEffectDef) -> StringName:
	_ensure_sidecars_ready()
	return _ground_effect_service._get_effect_forced_move_mode(effect_def)


func _build_ground_effect_coords(
	skill_def: SkillDef,
	target_coords: Array,
	source_coord: Vector2i = Vector2i(-1, -1),
	active_unit: BattleUnitState = null,
	cast_variant = null
) -> Array[Vector2i]:
	_ensure_sidecars_ready()
	return _ground_effect_service._build_ground_effect_coords(skill_def, target_coords, source_coord, active_unit, cast_variant)

func _collect_ground_unit_effect_defs(
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	active_unit: BattleUnitState = null
) -> Array[CombatEffectDef]:
	_ensure_sidecars_ready()
	return _ground_effect_service._collect_ground_unit_effect_defs(skill_def, cast_variant, active_unit)


func _collect_ground_terrain_effect_defs(
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	active_unit: BattleUnitState = null
) -> Array[CombatEffectDef]:
	_ensure_sidecars_ready()
	return _ground_effect_service._collect_ground_terrain_effect_defs(skill_def, cast_variant, active_unit)


func _collect_ground_effect_defs(
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	active_unit: BattleUnitState = null
) -> Array[CombatEffectDef]:
	_ensure_sidecars_ready()
	return _ground_effect_service._collect_ground_effect_defs(skill_def, cast_variant, active_unit)


func _collect_ground_preview_unit_ids(
	source_unit: BattleUnitState,
	skill_def: SkillDef,
	effect_defs: Array[CombatEffectDef],
	effect_coords: Array[Vector2i]
) -> Array[StringName]:
	_ensure_sidecars_ready()
	return _ground_effect_service._collect_ground_preview_unit_ids(source_unit, skill_def, effect_defs, effect_coords)


func _apply_ground_unit_effects(
	source_unit: BattleUnitState,
	skill_def: SkillDef,
	effect_defs: Array[CombatEffectDef],
	effect_coords: Array[Vector2i],
	batch: BattleEventBatch,
	target_coords: Array[Vector2i] = []
) -> Dictionary:
	_ensure_sidecars_ready()
	return _ground_effect_service._apply_ground_unit_effects(source_unit, skill_def, effect_defs, effect_coords, batch, target_coords)


func _resolve_ground_unit_effect_result(
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	effect_defs: Array[CombatEffectDef]
) -> Dictionary:
	_ensure_sidecars_ready()
	return _ground_effect_service._resolve_ground_unit_effect_result(source_unit, target_unit, skill_def, effect_defs)


func _should_resolve_ground_effects_as_attack(effect_defs: Array[CombatEffectDef]) -> bool:
	_ensure_sidecars_ready()
	return _ground_effect_service._should_resolve_ground_effects_as_attack(effect_defs)


func _dedupe_effect_defs_by_instance(effect_defs: Array[CombatEffectDef]) -> Array[CombatEffectDef]:
	_ensure_sidecars_ready()
	return _ground_effect_service._dedupe_effect_defs_by_instance(effect_defs)


func _apply_ground_terrain_effects(
	source_unit: BattleUnitState,
	skill_def: SkillDef,
	effect_defs: Array[CombatEffectDef],
	effect_coords: Array[Vector2i],
	batch: BattleEventBatch
) -> Dictionary:
	_ensure_sidecars_ready()
	return _ground_effect_service._apply_ground_terrain_effects(source_unit, skill_def, effect_defs, effect_coords, batch)


func _apply_ground_cell_effect(
	source_unit: BattleUnitState,
	skill_def: SkillDef,
	target_coord: Vector2i,
	effect_def: CombatEffectDef,
	batch: BattleEventBatch
) -> bool:
	_ensure_sidecars_ready()
	return _ground_effect_service._apply_ground_cell_effect(source_unit, skill_def, target_coord, effect_def, batch)


func _reconcile_water_topology(effect_coords: Array[Vector2i], batch: BattleEventBatch) -> bool:
	_ensure_sidecars_ready()
	return _ground_effect_service._reconcile_water_topology(effect_coords, batch)


func _collect_units_in_coords(effect_coords: Array[Vector2i]) -> Array[BattleUnitState]:
	_ensure_sidecars_ready()
	return _skill_orchestrator._collect_units_in_coords(effect_coords)


func _is_unit_effect(effect_def: CombatEffectDef) -> bool:
	_ensure_sidecars_ready()
	return _skill_orchestrator._is_unit_effect(effect_def)


func _apply_unit_shield_effects(
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	effect_defs: Array[CombatEffectDef],
	shield_roll_context: Dictionary = {}
) -> Dictionary:
	_ensure_sidecars_ready()
	return _shield_service._apply_unit_shield_effects(source_unit, target_unit, skill_def, effect_defs, shield_roll_context)


func _apply_shield_effect_to_target(
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	effect_def: CombatEffectDef,
	shield_roll_context: Dictionary = {}
) -> Dictionary:
	_ensure_sidecars_ready()
	return _shield_service._apply_shield_effect_to_target(source_unit, target_unit, skill_def, effect_def, shield_roll_context)


func _write_unit_shield(
	target_unit: BattleUnitState,
	shield_hp: int,
	shield_duration: int,
	shield_family: StringName,
	shield_source_unit_id: StringName,
	shield_source_skill_id: StringName,
	shield_params: Dictionary
) -> void:
	_ensure_sidecars_ready()
	_shield_service._write_unit_shield(target_unit, shield_hp, shield_duration, shield_family, shield_source_unit_id, shield_source_skill_id, shield_params)


func _build_unit_shield_result(target_unit: BattleUnitState, applied: bool) -> Dictionary:
	_ensure_sidecars_ready()
	return _shield_service._build_unit_shield_result(target_unit, applied)


func _resolve_shield_hp(effect_def: CombatEffectDef, shield_roll_context: Dictionary = {}) -> int:
	_ensure_sidecars_ready()
	return _shield_service._resolve_shield_hp(effect_def, shield_roll_context)


func _roll_shield_hp(effect_def: CombatEffectDef) -> int:
	_ensure_sidecars_ready()
	return _shield_service._roll_shield_hp(effect_def)


func _has_shield_dice_config(effect_def: CombatEffectDef) -> bool:
	_ensure_sidecars_ready()
	return _shield_service._has_shield_dice_config(effect_def)


func _get_shield_roll_cache_key(effect_def: CombatEffectDef) -> int:
	_ensure_sidecars_ready()
	return _shield_service._get_shield_roll_cache_key(effect_def)


func _roll_battle_effect_die(dice_sides: int) -> int:
	_ensure_sidecars_ready()
	return _shield_service._roll_battle_effect_die(dice_sides)


func _resolve_shield_duration_tu(effect_def: CombatEffectDef) -> int:
	_ensure_sidecars_ready()
	return _shield_service._resolve_shield_duration_tu(effect_def)


func _resolve_shield_family(skill_def: SkillDef, effect_def: CombatEffectDef) -> StringName:
	_ensure_sidecars_ready()
	return _shield_service._resolve_shield_family(skill_def, effect_def)


func _is_terrain_effect(effect_def: CombatEffectDef) -> bool:
	_ensure_sidecars_ready()
	return _skill_orchestrator._is_terrain_effect(effect_def)


func _resolve_effect_target_filter(skill_def: SkillDef, effect_def: CombatEffectDef) -> StringName:
	_ensure_sidecars_ready()
	return _skill_orchestrator._resolve_effect_target_filter(skill_def, effect_def)


func _is_unit_valid_for_effect(
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	target_team_filter: StringName
) -> bool:
	_ensure_sidecars_ready()
	return _skill_orchestrator._is_unit_valid_for_effect(source_unit, target_unit, target_team_filter)


func _build_terrain_effect_instance_id(effect_id: StringName) -> StringName:
	_ensure_sidecars_ready()
	return _ground_effect_service._build_terrain_effect_instance_id(effect_id)


func _get_terrain_effect_display_name(effect_def: CombatEffectDef) -> String:
	_ensure_sidecars_ready()
	return _ground_effect_service._get_terrain_effect_display_name(effect_def)


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
	_ensure_sidecars_ready()
	return _skill_orchestrator._resolve_ground_cast_variant(skill_def, active_unit, command)


func _resolve_unit_cast_variant(
	skill_def: SkillDef,
	active_unit: BattleUnitState,
	command: BattleCommand
) -> CombatCastVariantDef:
	_ensure_sidecars_ready()
	return _skill_orchestrator._resolve_unit_cast_variant(skill_def, active_unit, command)


func _get_cast_variant_target_mode(skill_def: SkillDef, cast_variant: CombatCastVariantDef) -> StringName:
	_ensure_sidecars_ready()
	return _skill_orchestrator._get_cast_variant_target_mode(skill_def, cast_variant)


func _build_implicit_ground_cast_variant(skill_def: SkillDef) -> CombatCastVariantDef:
	_ensure_sidecars_ready()
	return _skill_orchestrator._build_implicit_ground_cast_variant(skill_def)


func _validate_ground_skill_command(
	active_unit: BattleUnitState,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	command: BattleCommand
) -> Dictionary:
	_ensure_sidecars_ready()
	return _ground_effect_service._validate_ground_skill_command(active_unit, skill_def, cast_variant, command)


func _get_ground_special_effect_validation_message(
	active_unit: BattleUnitState,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	target_coords: Array[Vector2i]
) -> String:
	_ensure_sidecars_ready()
	return _ground_effect_service._get_ground_special_effect_validation_message(active_unit, skill_def, cast_variant, target_coords)


func _validate_target_coords_shape(footprint_pattern: StringName, target_coords: Array[Vector2i]) -> bool:
	_ensure_sidecars_ready()
	return _ground_effect_service._validate_target_coords_shape(footprint_pattern, target_coords)


func _normalize_target_coords(command: BattleCommand) -> Array[Vector2i]:
	_ensure_sidecars_ready()
	return _ground_effect_service._normalize_target_coords(command)


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


func handle_unit_defeated_by_runtime_effect(
	unit_state: BattleUnitState,
	source_unit: BattleUnitState,
	batch: BattleEventBatch,
	log_line: String = "",
	options: Dictionary = {}
) -> void:
	if unit_state == null:
		return
	if bool(options.get("collect_loot", true)):
		_collect_defeated_unit_loot(unit_state, source_unit)
	_clear_defeated_unit(unit_state, batch)
	_record_unit_defeated(unit_state)
	if bool(options.get("record_enemy_defeated_achievement", false)):
		_battle_rating_system.record_enemy_defeated_achievement(source_unit, unit_state)
	if not log_line.is_empty() and batch != null:
		batch.log_lines.append(log_line)
	if bool(options.get("check_battle_end", true)):
		_check_battle_end(batch)


func remove_summoned_unit_from_battle(unit_state: BattleUnitState, batch: BattleEventBatch, log_line: String = "") -> void:
	if _state == null or unit_state == null:
		return
	var previous_coords := unit_state.occupied_coords.duplicate()
	unit_state.is_alive = false
	_grid_service.clear_unit_occupancy(_state, unit_state)
	_append_changed_coords(batch, previous_coords)
	_append_changed_unit_id(batch, unit_state.unit_id)
	if not log_line.is_empty() and batch != null:
		batch.log_lines.append(log_line)
	_record_unit_defeated(unit_state)
	_check_battle_end(batch)


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


func _normalize_unit_action_threshold(action_threshold: int) -> int:
	_ensure_sidecars_ready()
	return _timeline_driver._normalize_unit_action_threshold(action_threshold)


func _initialize_unit_action_thresholds() -> void:
	_ensure_sidecars_ready()
	_timeline_driver._initialize_unit_action_thresholds()


func _initialize_unit_trait_hooks() -> void:
	_ensure_sidecars_ready()
	_timeline_driver._initialize_unit_trait_hooks()


func _resolve_unit_action_threshold(unit_state: BattleUnitState) -> int:
	_ensure_sidecars_ready()
	return _timeline_driver._resolve_unit_action_threshold(unit_state)


func _resolve_timeline_tu_per_tick(context: Dictionary) -> int:
	_ensure_sidecars_ready()
	return _timeline_driver._resolve_timeline_tu_per_tick(context)


func _collect_dict_vector2i_keys(values: Dictionary) -> Array[Vector2i]:
	_ensure_sidecars_ready()
	return _movement_service._collect_dict_vector2i_keys(values)


func _build_reachable_move_buckets(max_move_points: int) -> Array:
	_ensure_sidecars_ready()
	return _movement_service._build_reachable_move_buckets(max_move_points)


func _build_reachable_move_state_key(coord: Vector2i, has_quickstep_bonus: bool) -> String:
	_ensure_sidecars_ready()
	return _movement_service._build_reachable_move_state_key(coord, has_quickstep_bonus)


func _get_unit_skill_level(unit_state: BattleUnitState, skill_id: StringName) -> int:
	_ensure_sidecars_ready()
	return _skill_orchestrator._get_unit_skill_level(unit_state, skill_id)


func _format_skill_variant_label(skill_def: SkillDef, cast_variant: CombatCastVariantDef) -> String:
	_ensure_sidecars_ready()
	return _skill_orchestrator._format_skill_variant_label(skill_def, cast_variant)


func _check_battle_end(batch: BattleEventBatch) -> bool:
	_ensure_sidecars_ready()
	return _timeline_driver._check_battle_end(batch)


func _count_living_units(unit_ids: Array[StringName]) -> int:
	_ensure_sidecars_ready()
	return _timeline_driver._count_living_units(unit_ids)


func _end_active_turn(batch: BattleEventBatch) -> void:
	_ensure_sidecars_ready()
	_timeline_driver._end_active_turn(batch)


func _handle_adjacent_ally_defeat(defeated_unit: BattleUnitState) -> void:
	_ensure_sidecars_ready()
	_special_skill_resolver._handle_adjacent_ally_defeat(defeated_unit)


func _handle_low_luck_relic_ally_defeat(defeated_unit: BattleUnitState, batch: BattleEventBatch = null) -> void:
	_ensure_sidecars_ready()
	_special_skill_resolver._handle_low_luck_relic_ally_defeat(defeated_unit, batch)


func _collect_adjacent_living_allies(defeated_unit: BattleUnitState) -> Array[BattleUnitState]:
	_ensure_sidecars_ready()
	return _special_skill_resolver._collect_adjacent_living_allies(defeated_unit)


func _are_units_adjacent(first_unit: BattleUnitState, second_unit: BattleUnitState) -> bool:
	_ensure_sidecars_ready()
	return _special_skill_resolver._are_units_adjacent(first_unit, second_unit)


func _activate_next_ready_unit(batch: BattleEventBatch) -> void:
	_ensure_sidecars_ready()
	_timeline_driver._activate_next_ready_unit(batch)


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


func _advance_unit_status_durations(unit_state: BattleUnitState, elapsed_tu: int, batch: BattleEventBatch = null) -> bool:
	return _skill_turn_resolver.advance_unit_status_durations(unit_state, elapsed_tu, batch)


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
	if _skill_turn_resolver != null:
		_skill_turn_resolver.clear_turn_ai_override(unit_state)


func _find_unit_by_member_id(member_id: StringName) -> BattleUnitState:
	for unit_state_data in _state.units.values():
		var unit_state := unit_state_data as BattleUnitState
		if unit_state != null and unit_state.source_member_id == member_id:
			return unit_state
	return null


func _sort_ready_unit_ids_by_action_priority() -> void:
	_ensure_sidecars_ready()
	_timeline_driver._sort_ready_unit_ids_by_action_priority()


func _is_left_ready_unit_higher_priority(left_unit_id: StringName, right_unit_id: StringName) -> bool:
	_ensure_sidecars_ready()
	return _timeline_driver._is_left_ready_unit_higher_priority(left_unit_id, right_unit_id)


func _get_unit_turn_order_attribute(unit_state: BattleUnitState, attribute_id: StringName) -> int:
	_ensure_sidecars_ready()
	return _timeline_driver._get_unit_turn_order_attribute(unit_state, attribute_id)


func _get_unit_turn_order_action_points(unit_state: BattleUnitState) -> int:
	_ensure_sidecars_ready()
	return _timeline_driver._get_unit_turn_order_action_points(unit_state)


func _get_units_in_order() -> Array[StringName]:
	_ensure_sidecars_ready()
	return _timeline_driver._get_units_in_order()


func _new_batch() -> BattleEventBatch:
	return BATTLE_EVENT_BATCH_SCRIPT.new()


func _build_battle_resolution_result():
	return _loot_resolver.build_battle_resolution_result()

func _roll_hit_rate(hit_rate_percent: int) -> Dictionary:
	return _hit_resolver.roll_hit_rate(_state, hit_rate_percent)
