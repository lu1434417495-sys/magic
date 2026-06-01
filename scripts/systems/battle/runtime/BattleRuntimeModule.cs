using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GBattleUnitArray = Godot.Collections.Array<BattleUnitState>;
using GCombatEffectArray = Godot.Collections.Array<CombatEffectDef>;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

internal static class BattleRuntimeDictionaryOptions
{
    internal static bool ReadBool(GDictionary source, string key, bool fallback = false)
    {
        if (source == null || string.IsNullOrEmpty(key))
        {
            return fallback;
        }
        if (source.ContainsKey(key))
        {
            return source[key].AsBool();
        }
        StringName stringNameKey = new(key);
        if (source.ContainsKey(stringNameKey))
        {
            return source[stringNameKey].AsBool();
        }
        return fallback;
    }
}

internal readonly struct BattleDefeatHandlingOptions
{
    internal readonly bool CollectLoot;
    internal readonly bool RecordEnemyDefeatedAchievement;
    internal readonly bool CheckBattleEnd;

    internal BattleDefeatHandlingOptions(
        bool collectLoot = true,
        bool recordEnemyDefeatedAchievement = false,
        bool checkBattleEnd = true
    )
    {
        CollectLoot = collectLoot;
        RecordEnemyDefeatedAchievement = recordEnemyDefeatedAchievement;
        CheckBattleEnd = checkBattleEnd;
    }

    internal static BattleDefeatHandlingOptions FromDictionary(GDictionary options)
    {
        options ??= new GDictionary();
        return new BattleDefeatHandlingOptions(
            BattleRuntimeDictionaryOptions.ReadBool(options, "collect_loot", true),
            BattleRuntimeDictionaryOptions.ReadBool(
                options,
                "record_enemy_defeated_achievement"
            ),
            BattleRuntimeDictionaryOptions.ReadBool(options, "check_battle_end", true)
        );
    }
}

internal readonly struct BattleStartOptions
{
    internal readonly bool ValidateSpawnReachability;
    internal readonly bool EnforceOpposingSpawnSides;
    internal readonly bool ValidateBidirectionalSpawnReachability;

    private BattleStartOptions(
        bool validateSpawnReachability,
        bool enforceOpposingSpawnSides,
        bool validateBidirectionalSpawnReachability
    )
    {
        ValidateSpawnReachability = validateSpawnReachability;
        EnforceOpposingSpawnSides = enforceOpposingSpawnSides;
        ValidateBidirectionalSpawnReachability = validateBidirectionalSpawnReachability;
    }

    internal static BattleStartOptions FromContext(
        GDictionary context,
        bool validateSpawnReachabilityDefault
    )
    {
        context ??= new GDictionary();
        return new BattleStartOptions(
            BattleRuntimeDictionaryOptions.ReadBool(
                context,
                "validate_spawn_reachability",
                validateSpawnReachabilityDefault
            ),
            BattleRuntimeDictionaryOptions.ReadBool(context, "enforce_opposing_spawn_sides"),
            BattleRuntimeDictionaryOptions.ReadBool(
                context,
                "validate_bidirectional_spawn_reachability"
            )
        );
    }
}

internal readonly struct BattleEndOptions
{
    internal readonly bool CommitProgression;

    internal BattleEndOptions(bool commitProgression = false)
    {
        CommitProgression = commitProgression;
    }

    internal static BattleEndOptions FromDictionary(GDictionary options)
    {
        options ??= new GDictionary();
        return new BattleEndOptions(
            BattleRuntimeDictionaryOptions.ReadBool(options, "commit_progression")
        );
    }
}

[GlobalClass]
public partial class BattleRuntimeModule : RefCounted
{
    private static readonly StringName REPEAT_ATTACK_EFFECT_TYPE = "repeat_attack_until_fail";
    private static readonly StringName BODY_SIZE_CATEGORY_OVERRIDE_EFFECT_TYPE =
        "body_size_category_override";
    private static readonly StringName CHAIN_DAMAGE_EFFECT_TYPE = "chain_damage";
    private static readonly StringName TERRAIN_EFFECT_STATUS = "status";
    private const int MIN_BATTLE_SURFACE_HEIGHT = 4;
    private static readonly StringName STATUS_BLACK_STAR_BRAND_NORMAL = "black_star_brand_normal";
    private static readonly StringName STATUS_CROWN_BREAK_BROKEN_HAND = "crown_break_broken_hand";
    private static readonly StringName STATUS_BLACK_STAR_BRAND_ELITE = "black_star_brand_elite";
    private static readonly StringName STATUS_CROWN_BREAK_BROKEN_FANG = "crown_break_broken_fang";
    private static readonly StringName STATUS_CROWN_BREAK_BLINDED_EYE = "crown_break_blinded_eye";
    private static readonly StringName STATUS_DOOM_SENTENCE_VERDICT = "doom_sentence_verdict";
    private static readonly StringName PHASE_BATTLE_ENDED = "battle_ended";
    private static readonly StringName BLACK_STAR_BRAND_SKILL_ID = "black_star_brand";
    private static readonly StringName MISSTEP_TO_SCHEME_SKILL_ID = "misstep_to_scheme";
    private static readonly StringName BLACK_CONTRACT_PUSH_SKILL_ID = "black_contract_push";
    private static readonly StringName DOOM_SHIFT_SKILL_ID = "doom_shift";
    private static readonly StringName BLACK_CROWN_SEAL_SKILL_ID = "black_crown_seal";
    private static readonly StringName CROWN_BREAK_SKILL_ID = "crown_break";
    private static readonly StringName DOOM_SENTENCE_SKILL_ID = "doom_sentence";
    private static readonly StringName FORTUNE_MARK_TARGET_STAT_ID = "fortune_mark_target";
    private static readonly StringName BOSS_TARGET_STAT_ID = "boss_target";
    private const int BATTLE_START_PLACEMENT_MAX_ATTEMPTS = 8;
    private const int BATTLE_START_TERRAIN_RETRY_SEED_STEP = 7919;
    public static readonly StringName SPAWN_SIDE_NEAR_LONG_EDGE_VALUE = "near_long_edge";
    public static readonly StringName SPAWN_SIDE_FAR_LONG_EDGE_VALUE = "far_long_edge";

    public static StringName SPAWN_SIDE_NEAR_LONG_EDGE() => SPAWN_SIDE_NEAR_LONG_EDGE_VALUE;

    public static StringName SPAWN_SIDE_FAR_LONG_EDGE() => SPAWN_SIDE_FAR_LONG_EDGE_VALUE;

    private IBattleRuntimeCharacterGateway _characterGateway;

    public GDictionary _skill_defs = new();
    private readonly Dictionary<StringName, SkillDef> _skillDefIndex = new();
    private readonly Dictionary<StringName, EnemyAiBrainDef> _enemyAiBrainIndex = new();
    public GDictionary _item_defs = new();
    public GDictionary _enemy_templates = new();
    public GDictionary _enemy_ai_brains = new();
    public EncounterRosterBuilder _encounter_builder = new EncounterRosterBuilder();
    public BattleState _state;
    public BattleGridService _grid_service = new();
    public BattleTerrainGenerator _terrain_generator = new BattleTerrainGenerator();
    public BattleDamageResolver _damage_resolver = new();
    public BattleHitResolver _hit_resolver = new();
    public BattleAiService _ai_service = new();
    public BattleAiActionAssembler _ai_action_assembler = new();
    public BattleTerrainEffectSystem _terrain_effect_system = new();
    public BattleRatingSystem _battle_rating_system = new();
    public BattleUnitFactory _unit_factory = new();
    public BattleChargeResolver _charge_resolver = new();
    public BattleRepeatAttackResolver _repeat_attack_resolver = new();
    public BattleMagicBacklashResolver _magic_backlash_resolver = new();
    public BattleReportFormatter _report_formatter = new();
    public BattleSkillResolutionRules _skill_resolution_rules = new();
    public BattleSkillMasteryService _skill_mastery_service = new();
    public BattleTerrainTopologyService _terrain_topology_service = new();
    public BattleTargetCollectionService _target_collection_service = new();
    public BattleSpawnReachabilityService _spawn_reachability_service = new();
    public EquipmentDropService _equipment_drop_service = new();
    public Func<StringName> _equipment_instance_id_allocator;
    public FateRuntimeModule _fate_runtime = new();
    public BattleChangeEquipmentResolver _change_equipment_resolver = new();
    public BattleRuntimeLootResolver _loot_resolver = new();
    public BattleRuntimeSkillTurnResolver _skill_turn_resolver = new();
    public BattleMetricsCollector _metrics_collector = new();
    public BattleShieldService _shield_service = new();
    public BattleGroundEffectService _ground_effect_service = new();
    public BattleSpecialSkillResolver _special_skill_resolver = new();
    public BattleMovementService _movement_service = new();
    public BattleLayeredBarrierService _layered_barrier_service = new();
    public BattleTimelineDriver _timeline_driver = new();
    public BattleSkillExecutionOrchestrator _skill_orchestrator = new();
    public TraitTriggerHooks _trait_trigger_hooks = new();
    public GDictionary _special_profile_registry_snapshot = new();
    public BattleSpecialProfileGate _special_profile_gate;
    public BattleMeteorSwarmResolver _meteor_swarm_resolver;
    public BattleAttackCheckPolicyService _attack_check_policy_service = new();
    public BattleSkillOutcomeCommitter _skill_outcome_committer = new();
    public BattleSpecialProfileCommitAdapter _special_profile_commit_adapter = new();
    private readonly Dictionary<StringName, BattleRatingMemberStats> _battleRatingStatsByMemberId = new();
    public GArray _pending_post_battle_character_rewards = new();
    public GArray _active_loot_entries = new();
    public GDictionary _looted_defeated_unit_ids = new();
    public BattleResolutionResult _battle_resolution_result;
    public bool _battle_resolution_result_consumed;
    public int _terrain_effect_nonce;
    public bool _ai_trace_enabled;
    public Godot.Collections.Array<GDictionary> _ai_turn_traces = new();
    public Dictionary<StringName, BattleAiRuntimeActionPlan> _ai_action_plans_by_unit_id = new();
    private readonly BattleMovementQueryService _ai_movement_query_service = new();
    private readonly BattleAiScoreContextAdapter _ai_score_context_adapter = new();
    private readonly BattleAiQueryService _ai_query_service = new();
    private readonly BattleAiCandidateEvaluationService _ai_candidate_evaluation_service = new();
    public GDictionary _battle_metrics = new();
    public GDictionary _last_start_failure = new();
    public GDictionary calamity_by_member_id = new();

    public void setup(
        IBattleRuntimeCharacterGateway character_gateway = null,
        GDictionary skill_defs = null,
        GDictionary enemy_templates = null,
        GDictionary enemy_ai_brains = null,
        EncounterRosterBuilder encounter_builder = null,
        EquipmentDropService equipment_drop_service = null,
        GDictionary item_defs = null,
        BattleTerrainGenerator terrain_generator = null,
        Func<StringName> equipment_instance_id_allocator = null,
        GDictionary battle_special_profile_registry_snapshot = null
    )
    {
        _characterGateway = character_gateway;
        _skill_defs = skill_defs ?? new GDictionary();
        RebuildSkillDefIndex();
        _special_profile_registry_snapshot =
            battle_special_profile_registry_snapshot?.Duplicate(true) ?? new GDictionary();
        BindDamageResolver();

        _item_defs = item_defs ?? new GDictionary();
        if (
            _item_defs.Count == 0
            && _characterGateway != null
        )
        {
            _item_defs = _characterGateway.get_item_defs() ?? new GDictionary();
        }

        _enemy_templates = enemy_templates ?? new GDictionary();
        _enemy_ai_brains = enemy_ai_brains ?? new GDictionary();
        RebuildEnemyAiBrainIndex();
        _encounter_builder = encounter_builder ?? new EncounterRosterBuilder();
        _equipment_drop_service = equipment_drop_service ?? new EquipmentDropService();
        _equipment_instance_id_allocator = equipment_instance_id_allocator;
        if (terrain_generator != null)
            _terrain_generator = terrain_generator;

        _ai_action_plans_by_unit_id.Clear();
        _last_start_failure.Clear();
        _ai_service.setup(_enemy_ai_brains, _damage_resolver);
        _terrain_effect_system.setup(this);
        _attack_check_policy_service ??= new BattleAttackCheckPolicyService();
        _attack_check_policy_service.setup(this, _hit_resolver, _terrain_effect_system);
        _skill_outcome_committer ??= new BattleSkillOutcomeCommitter();
        _skill_outcome_committer.setup(this);
        _special_profile_commit_adapter ??= new BattleSpecialProfileCommitAdapter();
        _special_profile_commit_adapter.setup(this, _skill_outcome_committer);
        _battle_rating_system.setup(this, _skill_mastery_service);
        _unit_factory.setup(this);
        _charge_resolver.setup(this, _skill_mastery_service);
        _repeat_attack_resolver.setup(this, _skill_mastery_service);
        _skill_mastery_service.clear();
        _fate_runtime.setup(
            _characterGateway,
            get_fate_event_bus(),
            this,
            _find_unit_by_member_id
        );
        _change_equipment_resolver.setup(this);
        _loot_resolver.Setup(this);
        _skill_turn_resolver.setup(this);
        _metrics_collector.setup(this);
        _shield_service.setup(this);
        _ground_effect_service.setup(this);
        _special_skill_resolver.setup(this);
        _movement_service.setup(this);
        _layered_barrier_service.Setup(this);
        _timeline_driver.Setup(this);
        _skill_orchestrator.setup(this);
        _setup_special_profile_runtime();
    }

    public void _setup_special_profile_runtime()
    {
        _special_profile_gate ??= new BattleSpecialProfileGate();
        _special_profile_gate.setup(_special_profile_registry_snapshot);
        _skill_outcome_committer ??= new BattleSkillOutcomeCommitter();
        _skill_outcome_committer.setup(this);
        _special_profile_commit_adapter ??= new BattleSpecialProfileCommitAdapter();
        _special_profile_commit_adapter.setup(this, _skill_outcome_committer);

        _meteor_swarm_resolver = null;
        GDictionary profiles = GetDict(_special_profile_registry_snapshot, "profiles");
        GDictionary meteorProfileSnapshot = GetDict(profiles, "meteor_swarm");
        if (GetString(meteorProfileSnapshot, "runtime_resolver_id") != "meteor_swarm")
            return;
        _meteor_swarm_resolver = new BattleMeteorSwarmResolver();
        _meteor_swarm_resolver.setup(this, _attack_check_policy_service);
    }

    public BattleState start_battle(
        EncounterAnchorData encounter_anchor,
        int seed,
        GDictionary context = null
    )
    {
        context ??= new GDictionary();
        _last_start_failure.Clear();
        _ensure_sidecars_ready();
        var partyState =
            _characterGateway != null ? _characterGateway.get_party_state() : null;
        GBattleUnitArray allyUnits = ToBattleUnitArray(
            _unit_factory.build_ally_units(partyState, context)
        );
        if (allyUnits.Count == 0)
        {
            allyUnits = ToBattleUnitArray(_unit_factory.build_ally_units(null, context));
        }

        GBattleUnitArray enemyUnits = new();
        GDictionary enemyBuildContext = context.Duplicate(true);
        enemyBuildContext["battle_seed"] = seed;
        enemyBuildContext["skill_defs"] = _skill_defs;
        enemyBuildContext["enemy_templates"] = _enemy_templates;
        enemyBuildContext["enemy_ai_brains"] = _enemy_ai_brains;
        _active_loot_entries.Clear();
        _looted_defeated_unit_ids.Clear();
        _ai_action_plans_by_unit_id.Clear();
        calamity_by_member_id.Clear();

        bool hasExplicitEnemyUnits =
            enemyBuildContext.ContainsKey("enemy_units")
            && GetArray(enemyBuildContext, "enemy_units").Count > 0;
        BattleStartOptions startOptions = BattleStartOptions.FromContext(
            context,
            !hasExplicitEnemyUnits
        );
        if (hasExplicitEnemyUnits)
        {
            enemyUnits = ToBattleUnitArray(
                _unit_factory.build_enemy_units(encounter_anchor, enemyBuildContext)
            );
        }
        else if (_encounter_builder != null)
        {
            enemyUnits = ToBattleUnitArray(
                _encounter_builder.build_enemy_units(encounter_anchor, enemyBuildContext)
            );
        }

        if (
            !ValidateBattleUnitsForStart(allyUnits, "ally")
            || !ValidateBattleUnitsForStart(enemyUnits, "enemy")
        )
        {
            _state = null;
            _ai_action_plans_by_unit_id.Clear();
            _last_start_failure = new GDictionary
            {
                ["reason"] = "invalid_start_units",
                ["ally_unit_count"] = allyUnits?.Count ?? 0,
                ["enemy_unit_count"] = enemyUnits?.Count ?? 0,
            };
            return new BattleState();
        }

        for (
            int placementAttempt = 0;
            placementAttempt < BATTLE_START_PLACEMENT_MAX_ATTEMPTS;
            placementAttempt++
        )
        {
            int terrainSeed = seed + placementAttempt * BATTLE_START_TERRAIN_RETRY_SEED_STEP;
            GDictionary terrainData = _unit_factory.build_terrain_data(
                encounter_anchor,
                terrainSeed,
                context
            );
            if (terrainData.Count == 0)
                continue;
            StringName terrainProfileId = _resolve_formal_terrain_profile_id(terrainData);
            if (IsEmpty(terrainProfileId))
                continue;

            StringName encounterAnchorId =
                encounter_anchor != null
                    ? ProgressionDataUtils.to_string_name(encounter_anchor.entity_id)
                    : GetStringName(context, "encounter_anchor_id");
            string battleIdPrefix = !IsEmpty(encounterAnchorId)
                ? encounterAnchorId.ToString()
                : "battle";
            string encounterDisplayName =
                encounter_anchor != null ? encounter_anchor.display_name : "";
            if (string.IsNullOrEmpty(encounterDisplayName))
                encounterDisplayName = GetString(context, "encounter_display_name", "未知遭遇");

            _state = new BattleState
            {
                battle_id = ProgressionDataUtils.to_string_name($"{battleIdPrefix}_{seed}"),
                seed = seed,
                map_size = GetVector2I(terrainData, "map_size", Vector2I.Zero),
                world_coord = context.ContainsKey("world_coord")
                    ? GetVector2I(context, "world_coord", Vector2I.Zero)
                    : (encounter_anchor != null ? encounter_anchor.world_coord : Vector2I.Zero),
                encounter_anchor_id = encounterAnchorId,
                terrain_profile_id = terrainProfileId,
                cells = GetDict(terrainData, "cells"),
                cell_columns = terrainData.ContainsKey("cell_columns")
                    ? GetDict(terrainData, "cell_columns")
                    : BattleCellState.build_columns_from_surface_cells(
                        GetDict(terrainData, "cells")
                    ),
                timeline = new BattleTimelineState(),
            };
            _state.set_party_backpack_view(_get_party_backpack_state(partyState) as WarehouseState);
            _state.timeline.tu_per_tick = _resolve_timeline_tu_per_tick(context);

            GArray allySpawnCoords = GetArray(terrainData, "ally_spawns");
            GArray enemySpawnCoords = GetArray(terrainData, "enemy_spawns");
            StringName allySpawnSide = "";
            StringName enemySpawnSide = "";
            if (startOptions.EnforceOpposingSpawnSides)
            {
                allySpawnSide = _resolve_spawn_side_from_coords(allySpawnCoords);
                enemySpawnSide = _resolve_spawn_side_from_coords(enemySpawnCoords);
                if (IsEmpty(allySpawnSide) && !IsEmpty(enemySpawnSide))
                    allySpawnSide = _get_opposite_spawn_side(enemySpawnSide);
                if (IsEmpty(enemySpawnSide) && !IsEmpty(allySpawnSide))
                    enemySpawnSide = _get_opposite_spawn_side(allySpawnSide);
                if (!IsEmpty(allySpawnSide) && enemySpawnSide == allySpawnSide)
                    enemySpawnSide = _get_opposite_spawn_side(allySpawnSide);
            }
            if (!PlaceUnitsTyped(allyUnits, ToVector2IArray(allySpawnCoords), true, allySpawnSide))
            {
                _state = null;
                continue;
            }
            if (!PlaceUnitsTyped(enemyUnits, ToVector2IArray(enemySpawnCoords), false, enemySpawnSide))
            {
                _state = null;
                continue;
            }
            _initialize_unit_trait_hooks();
            if (startOptions.ValidateSpawnReachability)
            {
                BattleSpawnReachabilityResult reachability =
                    _spawn_reachability_service.ValidateStateTyped(
                        _state,
                        _grid_service,
                        _skill_defs,
                        new BattleSpawnReachabilityOptions(
                            startOptions.ValidateBidirectionalSpawnReachability
                        )
                    );
                if (!reachability.Valid)
                {
                    _last_start_failure = new GDictionary
                    {
                        ["reason"] = "spawn_reachability",
                        ["placement_attempt"] = placementAttempt,
                        ["terrain_seed"] = terrainSeed,
                        ["ally_spawn_count"] = allySpawnCoords.Count,
                        ["enemy_spawn_count"] = enemySpawnCoords.Count,
                        ["reachability"] = reachability.ToDictionary(),
                    };
                    _state = null;
                    _ai_action_plans_by_unit_id.Clear();
                    continue;
                }
            }

            _initialize_unit_action_thresholds();
            _build_ai_action_plans();
            _state.phase = "timeline_running";
            _state.active_unit_id = "";
            _state.winner_faction_id = "";
            _state.modal_state = "";
            _state.attack_roll_nonce = 0;
            _state.reset_log_entries(new GStringArray { $"战斗开始：{encounterDisplayName}" });
            _battle_rating_system.initialize_battle_rating_stats();
            _fate_runtime.begin_battle(calamity_by_member_id);
            _terrain_effect_nonce = 0;
            _battle_resolution_result = null;
            _battle_resolution_result_consumed = false;
            _ai_turn_traces.Clear();
            _initialize_battle_metrics();
            return _state;
        }

        _state = null;
        _ai_action_plans_by_unit_id.Clear();
        if (_last_start_failure.Count == 0)
        {
            _last_start_failure = new GDictionary
            {
                ["reason"] = "placement_exhausted",
                ["placement_attempts"] = BATTLE_START_PLACEMENT_MAX_ATTEMPTS,
            };
        }
        return new BattleState();
    }

    public GDictionary get_last_start_failure() => _last_start_failure.Duplicate(true);

    public bool _validate_battle_units_for_start(GArray units, string side_label) =>
        ValidateBattleUnitsForStart(ToBattleUnitArray(units), side_label);

    private static bool ValidateBattleUnitsForStart(GBattleUnitArray units, string side_label)
    {
        if (units == null || units.Count == 0)
        {
            GameLog.Error($"BattleRuntimeModule cannot start battle: {side_label} units are empty.", "battle.runtime.empty_side", "battle");
            return false;
        }
        foreach (BattleUnitState unitState in units)
        {
            if (unitState == null)
            {
                GameLog.Error(
                    $"BattleRuntimeModule cannot start battle: {side_label} unit payload is invalid.",
                    "battle.runtime.invalid_unit_payload",
                    "battle"
                );
                return false;
            }
            if (unitState.attribute_snapshot == null)
            {
                GameLog.Error(
                    $"BattleRuntimeModule cannot start battle: {side_label} unit {unitState.unit_id} is missing attribute_snapshot.",
                    "battle.runtime.missing_snapshot",
                    "battle"
                );
                return false;
            }
            if (
                !unitState
                    .attribute_snapshot.has_value(AttributeService.ARMOR_CLASS_ID())
            )
            {
                GameLog.Error(
                    $"BattleRuntimeModule cannot start battle: {side_label} unit {unitState.unit_id} is missing armor_class.",
                    "battle.runtime.missing_armor_class",
                    "battle"
                );
                return false;
            }
        }
        return true;
    }

    public void _build_ai_action_plans()
    {
        _ai_action_plans_by_unit_id.Clear();
        if (_state == null || _ai_action_assembler == null)
            return;
        foreach (BattleUnitState unitState in _state.GetUnitsTyped())
        {
            if (
                unitState == null
                || unitState.control_mode == (StringName)"manual"
                || IsEmpty(unitState.ai_brain_id)
            )
                continue;
            EnemyAiBrainDef brain = GetEnemyAiBrainTyped(unitState.ai_brain_id);
            if (brain == null)
                continue;
            BattleAiRuntimeActionPlan actionPlan = _ai_action_assembler.build_unit_action_plan(
                unitState,
                brain,
                _skill_defs
            );
            if (actionPlan != null)
                _ai_action_plans_by_unit_id[unitState.unit_id] = actionPlan;
        }
    }

    public void _ensure_ai_action_plan_for_unit(BattleUnitState unit_state)
    {
        if (unit_state == null || _ai_action_assembler == null)
            return;
        if (_ai_action_plans_by_unit_id.ContainsKey(unit_state.unit_id))
            return;
        if (unit_state.control_mode == (StringName)"manual" || IsEmpty(unit_state.ai_brain_id))
            return;
        EnemyAiBrainDef brain = GetEnemyAiBrainTyped(unit_state.ai_brain_id);
        if (brain == null)
            return;
        BattleAiRuntimeActionPlan actionPlan = _ai_action_assembler.build_unit_action_plan(
            unit_state,
            brain,
            _skill_defs
        );
        if (actionPlan != null)
            _ai_action_plans_by_unit_id[unit_state.unit_id] = actionPlan;
    }

    public void _bind_ai_helper_services_for_decision(
        BattleUnitState unit_state,
        BattleAiContext ai_context
    )
    {
        if (unit_state == null || ai_context == null || _state == null || _grid_service == null)
            return;
        ai_context.move_cost_callback ??= (unitState, targetCoord) =>
            _get_move_cost_for_unit_target(unitState, targetCoord);
        ai_context.skill_score_input_callback ??=
            (context, skillDef, command, preview, effectDefs, metadata) =>
                _ai_service.get_score_service()
                    .build_skill_score_input(
                        context,
                        skillDef,
                        command,
                        preview,
                        effectDefs ?? new GArray(),
                        metadata ?? new GDictionary()
                    );
        ai_context.action_score_input_callback ??=
            (context, actionKind, actionLabel, scoreBucketId, command, preview, metadata) =>
                _ai_service.get_score_service()
                    .build_action_score_input(
                        context,
                        actionKind,
                        actionLabel,
                        scoreBucketId,
                        command,
                        preview,
                        metadata ?? new GDictionary()
                    );
        var movementQuery = _ai_movement_query_service;
        movementQuery.setup(_state, _grid_service, _get_ai_move_query_cost);
        var scoreAdapter = _ai_score_context_adapter;
        scoreAdapter.setup(
            _ai_service.get_score_service(),
            _state,
            unit_state,
            _grid_service,
            _skill_defs
        );
        var query = _ai_query_service;
        query.setup(
            _state,
            _grid_service,
            unit_state.unit_id,
            _skill_defs,
            (service, actionKind, actionLabel, scoreBucketId, command, preview, metadata) =>
                scoreAdapter.build_action_score_input(
                    service,
                    actionKind,
                    actionLabel,
                    scoreBucketId,
                    command,
                    preview,
                    metadata
                ),
            (service, skillId, command, preview, effectDefs, metadata) =>
                scoreAdapter.build_skill_score_input(
                    service,
                    skillId,
                    command,
                    preview,
                    effectDefs,
                    metadata
                ),
            movementQuery,
            unitId =>
            {
                _state.TryGetUnitTyped(unitId, out BattleUnitState candidate);
                return candidate != null && _is_movement_blocked(candidate);
            }
        );
        var candidateEvaluator = _ai_candidate_evaluation_service;
        candidateEvaluator.setup(_ai_service.get_score_service());
        ai_context.ai_query_service = query;
        ai_context.candidate_evaluator = candidateEvaluator;
    }

    public int _get_ai_move_query_cost(StringName unit_id, Vector2I _from_coord, Vector2I to_coord)
    {
        if (_state == null)
            return 1;
        _state.TryGetUnitTyped(unit_id, out BattleUnitState unitState);
        return unitState == null ? 1 : _get_move_cost_for_unit_target(unitState, to_coord);
    }

    public StringName _resolve_formal_terrain_profile_id(GDictionary terrain_data)
    {
        if (terrain_data == null || !terrain_data.ContainsKey("terrain_profile_id"))
            return "";
        return GetStringName(terrain_data, "terrain_profile_id");
    }

    public BattleEventBatch advance(int tick_count)
    {
        _ensure_sidecars_ready();
        BattleEventBatch batch = _new_batch();
        if (_state == null || _state.phase == (StringName)"battle_ended")
            return batch;
        if (!IsEmpty(_state.modal_state))
            return batch;
        if (_state.timeline != null && _state.timeline.frozen)
            return batch;

        if (_state.phase == (StringName)"unit_acting")
        {
            _state.TryGetUnitTyped(_state.active_unit_id, out BattleUnitState activeUnit);
            if (activeUnit == null || !activeUnit.is_alive)
            {
                _end_active_turn(batch);
                return batch;
            }
            bool madnessAiControl = _skill_turn_resolver.is_turn_ai_override_active(activeUnit);
            if (
                activeUnit.is_alive
                && (activeUnit.control_mode != (StringName)"manual" || madnessAiControl)
            )
            {
                if (madnessAiControl && IsEmpty(activeUnit.ai_brain_id))
                {
                    var madnessCommand =
                        _skill_turn_resolver.build_madness_fallback_command(activeUnit);
                    if (madnessCommand != null)
                        return issue_command(madnessCommand);
                }
                AiTraceRecorder.enter("advance:ensure_ai_action_plan");
                _ensure_ai_action_plan_for_unit(activeUnit);
                AiTraceRecorder.exit("advance:ensure_ai_action_plan");

                AiTraceRecorder.enter("advance:create_ai_context");
                var aiContext = new BattleAiContext
                {
                    state = _state,
                    unit_state = activeUnit,
                    grid_service = _grid_service,
                    skill_defs = _skill_defs,
                    move_cost_callback = (unitState, targetCoord) =>
                        _get_move_cost_for_unit_target(unitState, targetCoord),
                    runtime_action_plan = _ai_action_plans_by_unit_id.TryGetValue(
                        activeUnit.unit_id,
                        out BattleAiRuntimeActionPlan actionPlan
                    )
                        ? actionPlan
                        : null,
                    trace_enabled = _ai_trace_enabled,
                };
                AiTraceRecorder.exit("advance:create_ai_context");
                AiTraceRecorder.enter("advance:bind_ai_helpers");
                _bind_ai_helper_services_for_decision(activeUnit, aiContext);
                AiTraceRecorder.exit("advance:bind_ai_helpers");
                AiTraceRecorder.enter("advance:choose_command");
                BattleAiDecision decision = _ai_service.choose_command(aiContext);
                AiTraceRecorder.exit("advance:choose_command");
                if (decision != null && decision.command != null)
                {
                    AiTraceRecorder.enter("advance:ai_decision_commit");
                    string aiLine =
                        $"AI[{decision.brain_id}/{decision.state_id}/{decision.action_id}] {decision.reason_text}";
                    AiTraceRecorder.enter("advance:append_ai_log");
                    _state.append_log_entry(aiLine);
                    AiTraceRecorder.exit("advance:append_ai_log");
                    GDictionary aiTurnTrace = new();
                    GDictionary snapshotsBefore = new();
                    GStringNameArray decisionTargetUnitIds = new();
                    if (_ai_trace_enabled)
                    {
                        aiTurnTrace = aiContext.build_turn_trace(decision);
                        decisionTargetUnitIds = _collect_ai_trace_decision_target_unit_ids(
                            decision,
                            aiTurnTrace
                        );
                        snapshotsBefore = _build_ai_trace_unit_snapshot_map();
                        aiTurnTrace["decision_target_snapshots"] =
                            _build_ai_trace_snapshots_for_unit_ids(
                                decisionTargetUnitIds,
                                snapshotsBefore
                            );
                    }
                    AiTraceRecorder.exit("advance:ai_decision_commit");
                    AiTraceRecorder.enter("advance:issue_ai_command");
                    BattleEventBatch decisionBatch;
                    try
                    {
                        decisionBatch = issue_command(decision.command);
                    }
                    finally
                    {
                        AiTraceRecorder.exit("advance:issue_ai_command");
                    }
                    AiTraceRecorder.enter("advance:ai_trace_after_command");
                    if (_ai_trace_enabled)
                    {
                        aiTurnTrace["execution_result"] = _build_ai_trace_execution_result(
                            decision,
                            decisionBatch,
                            snapshotsBefore,
                            decisionTargetUnitIds
                        );
                        _ai_turn_traces.Add(aiTurnTrace);
                    }
                    AiTraceRecorder.enter("advance:prepend_ai_batch_log");
                    if (decisionBatch != null)
                        decisionBatch.log_lines.Insert(0, aiLine);
                    AiTraceRecorder.exit("advance:prepend_ai_batch_log");
                    AiTraceRecorder.exit("advance:ai_trace_after_command");
                    return decisionBatch;
                }
            }
            return batch;
        }

        if (tick_count > 0)
        {
            _timeline_driver.AdvanceTimeline(tick_count, batch);
            if (_check_battle_end(batch))
                return batch;
        }

        if (_state.phase == (StringName)"timeline_running")
            _activate_next_ready_unit(batch);
        return batch;
    }

    public bool _use_discrete_timeline_ticks()
    {
        _ensure_sidecars_ready();
        return _timeline_driver.UseDiscreteTimelineTicks();
    }

    public void _apply_timeline_step(BattleEventBatch batch, int tu_delta)
    {
        _ensure_sidecars_ready();
        _timeline_driver.ApplyTimelineStep(batch, tu_delta);
    }

    public void _resolve_timeline_status_phase(BattleEventBatch batch, int tu_delta)
    {
        _ensure_sidecars_ready();
        _timeline_driver.ResolveTimelineStatusPhase(batch, tu_delta);
    }

    public void _collect_timeline_ready_units(BattleEventBatch batch, int tu_delta)
    {
        _ensure_sidecars_ready();
        _timeline_driver.CollectTimelineReadyUnits(batch, tu_delta);
    }

    public bool _apply_stamina_recovery(BattleUnitState unit_state, int tu_delta)
    {
        _ensure_sidecars_ready();
        return _timeline_driver.ApplyStaminaRecovery(unit_state, tu_delta);
    }

    public int _get_unit_constitution(BattleUnitState unit_state)
    {
        _ensure_sidecars_ready();
        return _timeline_driver._GetUnitConstitution(unit_state);
    }

    public int _apply_stamina_recovery_percent_bonus(
        BattleUnitState unit_state,
        int base_progress_gain
    )
    {
        _ensure_sidecars_ready();
        return _timeline_driver._ApplyStaminaRecoveryPercentBonus(unit_state, base_progress_gain);
    }

    public BattlePreview preview_command(BattleCommand command)
    {
        _ensure_sidecars_ready();
        var preview = new BattlePreview();
        if (!CanPreviewCommand(command))
            return preview;
        if (!IsEmpty(_state.modal_state))
        {
            preview.log_lines.Add(_get_battle_interaction_block_message());
            return preview;
        }

        BattleUnitState activeUnit = ResolvePreviewActiveUnit(command);
        if (activeUnit == null || !activeUnit.is_alive)
            return preview;

        if (command.is_move())
            PreviewMoveCommand(activeUnit, command, preview);
        else if (command.is_skill())
            PreviewSkillCommand(activeUnit, command, preview);
        else if (command.is_wait())
            PreviewWaitCommand(activeUnit, preview);
        else if (command.is_change_equipment())
            PreviewChangeEquipmentCommand(activeUnit, command, preview);
        else
            PreviewUnknownCommand(preview);
        return preview;
    }

    private bool CanPreviewCommand(BattleCommand command)
    {
        return _state != null && command != null && _state.phase != PHASE_BATTLE_ENDED;
    }

    private BattleUnitState ResolvePreviewActiveUnit(BattleCommand command)
    {
        return command != null && _state.TryGetUnitTyped(command.unit_id, out BattleUnitState unit)
            ? unit
            : null;
    }

    private void PreviewMoveCommand(
        BattleUnitState activeUnit,
        BattleCommand command,
        BattlePreview preview
    )
    {
        AiTraceRecorder.enter("preview:move");
        if (_is_movement_blocked(activeUnit))
        {
            preview.log_lines.Add($"{activeUnit.display_name} 当前被限制移动。");
            AiTraceRecorder.exit("preview:move");
            return;
        }

        AiTraceRecorder.enter("preview:move.resolve_path_result");
        BattleMovePathResult moveResult = _movement_service._resolve_move_path_result_typed(
            activeUnit,
            command.target_coord
        );
        AiTraceRecorder.exit("preview:move.resolve_path_result");

        AiTraceRecorder.enter("preview:move.build_preview");
        ApplyMovePreviewResult(activeUnit, command, preview, moveResult);
        AiTraceRecorder.exit("preview:move.build_preview");
        AiTraceRecorder.exit("preview:move");
    }

    private void ApplyMovePreviewResult(
        BattleUnitState activeUnit,
        BattleCommand command,
        BattlePreview preview,
        BattleMovePathResult moveResult
    )
    {
        if (!moveResult.Allowed)
        {
            preview.log_lines.Add(
                string.IsNullOrEmpty(moveResult.Message) ? "该移动不可执行。" : moveResult.Message
            );
            return;
        }

        preview.allowed = true;
        preview.move_cost = moveResult.Cost;
        preview.resolved_anchor_coord = command.target_coord;
        preview.log_lines.Add(
            $"移动可执行，距离消耗 {moveResult.Cost} 点移动力，执行后锁定剩余移动力。"
        );
        AddPreviewFootprintCoords(preview, activeUnit, command.target_coord);
    }

    private void AddPreviewFootprintCoords(
        BattlePreview preview,
        BattleUnitState activeUnit,
        Vector2I anchorCoord
    )
    {
        foreach (Vector2I targetCoord in _grid_service.get_unit_target_coords(activeUnit, anchorCoord))
            preview.target_coords.Add(targetCoord);
    }

    private void PreviewSkillCommand(
        BattleUnitState activeUnit,
        BattleCommand command,
        BattlePreview preview
    )
    {
        AiTraceRecorder.enter("preview:skill");
        _preview_skill_command(activeUnit, command, preview);
        AiTraceRecorder.exit("preview:skill");
    }

    private static void PreviewWaitCommand(BattleUnitState activeUnit, BattlePreview preview)
    {
        AiTraceRecorder.enter("preview:wait");
        preview.allowed = true;
        preview.log_lines.Add($"{activeUnit.display_name} 可以结束行动。");
        AiTraceRecorder.exit("preview:wait");
    }

    private void PreviewChangeEquipmentCommand(
        BattleUnitState activeUnit,
        BattleCommand command,
        BattlePreview preview
    )
    {
        AiTraceRecorder.enter("preview:change_equipment");
        _preview_change_equipment_command(activeUnit, command, preview);
        AiTraceRecorder.exit("preview:change_equipment");
    }

    private static void PreviewUnknownCommand(BattlePreview preview)
    {
        preview.log_lines.Add("未知命令类型。");
    }

    public BattleEventBatch issue_command(BattleCommand command)
    {
        _ensure_sidecars_ready();
        var batch = _new_batch();
        if (_state == null || command == null)
            return batch;
        if (_state.phase != (StringName)"unit_acting")
            return batch;
        if (!IsEmpty(_state.modal_state))
        {
            batch.log_lines.Add(_get_battle_interaction_block_message());
            return batch;
        }

        _state.TryGetUnitTyped(_state.active_unit_id, out BattleUnitState activeUnit);
        if (activeUnit == null || !activeUnit.is_alive)
            return batch;
        if (activeUnit.unit_id != command.unit_id)
        {
            if (command.command_type == BattleCommand.TYPE_CHANGE_EQUIPMENT())
            {
                BattleChangeEquipmentResult validation = _change_equipment_resolver.BuildChangeEquipmentResult(
                    false,
                    "target_not_self",
                    "只能为当前行动单位自己换装。",
                    command
                );
                validation.TargetUnitId = command.unit_id;
                _change_equipment_resolver.AppendChangeEquipmentReport(batch, activeUnit, validation, false);
                _append_batch_logs_to_state(batch);
            }
            return batch;
        }
        _ensure_unit_turn_anchor(activeUnit);
        if (
            command.command_type == BattleCommand.TYPE_SKILL()
            && _should_block_skill_issue_from_preview(command, batch)
        )
        {
            _append_batch_logs_to_state(batch);
            return batch;
        }

        if (command.command_type == BattleCommand.TYPE_MOVE())
            _handle_move_command(activeUnit, command, batch);
        else if (command.command_type == BattleCommand.TYPE_SKILL())
            _handle_skill_command(activeUnit, command, batch);
        else if (command.command_type == BattleCommand.TYPE_WAIT())
        {
            _record_action_issued(activeUnit, BattleCommand.TYPE_WAIT());
            batch.log_lines.Add($"{activeUnit.display_name} 结束行动。");
        }
        else if (command.command_type == BattleCommand.TYPE_CHANGE_EQUIPMENT())
            _handle_change_equipment_command(activeUnit, command, batch);
        else
            return batch;

        _append_batch_logs_to_state(batch);
        int flushedLogCount = batch.log_lines.Count;
        int flushedReportCount = batch.report_entries.Count;

        if (!IsEmpty(_state.modal_state))
        {
            batch.modal_requested = true;
            return batch;
        }

        if (_check_battle_end(batch))
        {
            _append_batch_logs_to_state_from(batch, flushedLogCount, flushedReportCount);
            return batch;
        }

        if (
            activeUnit.current_ap <= 0
            || !activeUnit.is_alive
            || command.command_type == BattleCommand.TYPE_WAIT()
        )
        {
            _end_active_turn(batch);
            _append_batch_logs_to_state_from(batch, flushedLogCount, flushedReportCount);
        }

        return batch;
    }

    public string _get_battle_interaction_block_message()
    {
        if (_state == null)
            return "当前无法操作。";
        if (_state.modal_state == (StringName)"start_confirm")
            return "战斗尚未开始，确认后才能操作。";
        if (_state.modal_state == (StringName)"promotion_choice")
            return "当前处于晋升选择中，无法操作。";
        return "当前有待处理的战斗流程，暂时无法操作。";
    }

    public bool _should_block_skill_issue_from_preview(
        BattleCommand command,
        BattleEventBatch batch
    )
    {
        BattlePreview preview = preview_command(command);
        if (preview != null && preview.allowed)
            return false;
        if (preview != null)
        {
            foreach (var logLine in preview.log_lines)
                batch.log_lines.Add(logLine.ToString());
        }
        if (batch.log_lines.Count == 0)
            batch.log_lines.Add("技能或目标无效。");
        return true;
    }

    public void _append_batch_logs_to_state(BattleEventBatch batch) =>
        _append_batch_logs_to_state_from(batch);

    public void _append_batch_logs_to_state_from(
        BattleEventBatch batch,
        int log_start_index = 0,
        int report_start_index = 0
    )
    {
        if (_state == null || batch == null)
            return;
        int safeLogStart = Math.Clamp(log_start_index, 0, batch.log_lines.Count);
        for (int i = safeLogStart; i < batch.log_lines.Count; i++)
            _state.append_log_entry(batch.log_lines[i]);
        int safeReportStart = Math.Clamp(report_start_index, 0, batch.report_entries.Count);
        for (int i = safeReportStart; i < batch.report_entries.Count; i++)
        {
            GDictionary reportEntry = batch.report_entries[i].AsGodotDictionary();
            if (reportEntry.Count > 0)
                _state.report_entries.Add(reportEntry.Duplicate(true));
        }
    }

    public void _append_result_report_entry(BattleEventBatch batch, GDictionary result)
    {
        if (batch == null || result == null || result.Count == 0)
            return;
        GDictionary reportEntry = GetDict(result, "report_entry");
        if (reportEntry.Count > 0)
            _append_report_entry_to_batch(batch, reportEntry);
    }

    internal void append_result_report_entry(
        BattleEventBatch batch,
        AttackEffectResolutionResult result
    )
    {
        if (batch == null || !result.HasReportEntry)
            return;
        GDictionary reportEntry = BattleReportEntryPayload.BuildGodotPayload(result.ReportEntry);
        if (reportEntry.Count > 0)
            _append_report_entry_to_batch(batch, reportEntry);
    }

    public void _append_report_entry_to_batch(BattleEventBatch batch, GDictionary report_entry)
    {
        if (batch == null || report_entry == null || report_entry.Count == 0)
            return;
        batch.report_entries.Add(report_entry.Duplicate(true));
        string entryText = GetString(report_entry, "text").StripEdges();
        if (!string.IsNullOrEmpty(entryText))
            batch.log_lines.Add(entryText);
    }

    public BattleEventBatch submit_promotion_choice(
        StringName member_id,
        StringName profession_id,
        GDictionary selection
    )
    {
        _ensure_sidecars_ready();
        BattleEventBatch batch = _new_batch();
        if (_state == null || _characterGateway == null)
            return batch;
        CharacterProgressionDelta delta = _characterGateway.promote_profession(
            member_id,
            profession_id,
            selection ?? new GDictionary()
        );
        if (!_promotion_delta_applied(delta, member_id, profession_id))
        {
            _keep_promotion_choice_modal_open(batch, "晋升提交无效，当前选择仍需确认。");
            return batch;
        }
        batch.progression_deltas.Add(delta);
        BattleUnitState unitState = _find_unit_by_member_id(member_id);
        if (unitState != null)
        {
            _unit_factory.refresh_battle_unit(unitState);
            batch.changed_unit_ids.Add(unitState.unit_id);
            batch.log_lines.Add($"{unitState.display_name} 完成职业晋升。");
        }
        if (delta.needs_promotion_modal)
        {
            _keep_promotion_choice_modal_open(
                batch,
                $"{(unitState != null ? unitState.display_name : member_id.ToString())} 触发职业晋升选择。"
            );
        }
        else
        {
            _state.modal_state = "";
            if (_state.timeline != null)
                _state.timeline.frozen = false;
        }
        return batch;
    }

    public void _keep_promotion_choice_modal_open(BattleEventBatch batch, string message = "")
    {
        if (_state == null)
            return;
        _state.modal_state = "promotion_choice";
        if (_state.timeline != null)
            _state.timeline.frozen = true;
        if (batch != null)
        {
            batch.modal_requested = true;
            if (!string.IsNullOrEmpty(message))
                batch.log_lines.Add(message);
        }
    }

    public bool _promotion_delta_applied(
        CharacterProgressionDelta delta,
        StringName member_id,
        StringName profession_id
    )
    {
        if (delta == null)
            return false;
        if (delta.member_id != member_id)
            return false;
        if (delta.needs_promotion_modal)
            return true;
        return delta.changed_profession_ids.Contains(profession_id);
    }

    public BattleState get_state() => _state;

    public GDictionary get_calamity_by_member_id() =>
        _fate_runtime != null
            ? _fate_runtime.get_calamity_by_member_id()
            : ProgressionDataUtils.to_string_name_int_map(calamity_by_member_id).Duplicate(true);

    public int get_member_calamity(StringName member_id) =>
        _fate_runtime != null ? _fate_runtime.get_member_calamity(member_id) : 0;

    public int get_member_calamity_cap(StringName member_id) =>
        _fate_runtime != null ? _fate_runtime.get_member_calamity_cap(member_id) : 3;

    public int get_black_star_brand_cast_cost(StringName member_id) =>
        _fate_runtime != null ? _fate_runtime.get_black_star_brand_cast_cost(member_id) : 1;

    public bool has_misfortune_reason(StringName member_id, StringName reason_id) =>
        _fate_runtime != null && _fate_runtime.has_misfortune_reason(member_id, reason_id);

    public FateRuntimeModule get_fate_runtime() => _fate_runtime;

    public string get_misfortune_skill_cast_block_reason(
        BattleUnitState active_unit,
        StringName skill_id
    ) =>
        _fate_runtime == null
            ? MisfortuneService.get_skill_sidecar_missing_message(skill_id)
            : _fate_runtime.get_misfortune_skill_cast_block_reason(active_unit, skill_id);

    public MisfortuneSkillCastResult consume_misfortune_skill_cast_result(
        BattleUnitState active_unit,
        StringName skill_id
    ) =>
        _fate_runtime == null
            ? MisfortuneSkillCastResult.Failure(
                MisfortuneService.get_skill_sidecar_missing_message(skill_id)
            )
            : _fate_runtime.consume_misfortune_skill_cast_result(active_unit, skill_id);

    public GDictionary consume_misfortune_skill_cast(
        BattleUnitState active_unit,
        StringName skill_id
    ) => consume_misfortune_skill_cast_result(active_unit, skill_id).ToDictionary();

    public GDictionary handle_misfortune_trigger(StringName reason_id, GDictionary payload = null) =>
        _fate_runtime != null
            ? _fate_runtime.handle_misfortune_trigger(reason_id, payload ?? new GDictionary())
            : new GDictionary();

    public GDictionary handle_fate_battle_resolution(
        BattleState battle_state,
        BattleResolutionResult battle_resolution_result
    ) =>
        _fate_runtime != null
            ? _fate_runtime.handle_battle_resolution(battle_state, battle_resolution_result)
            : new GDictionary();

    public string get_skill_cast_block_reason(BattleUnitState active_unit, SkillDef skill_def) =>
        _get_skill_cast_block_reason(active_unit, skill_def);

    public bool is_unit_guard_locked(BattleUnitState unit_state) =>
        _has_status(unit_state, STATUS_BLACK_STAR_BRAND_NORMAL);

    public bool is_unit_counterattack_locked(BattleUnitState unit_state) =>
        _has_status(unit_state, STATUS_BLACK_STAR_BRAND_NORMAL)
        || _has_status(unit_state, STATUS_CROWN_BREAK_BROKEN_HAND)
        || _skill_turn_resolver.has_counterattack_lock_status(unit_state);

    public bool is_unit_follow_up_locked(BattleUnitState unit_state) =>
        _has_status(unit_state, STATUS_CROWN_BREAK_BROKEN_HAND);

    public GDictionary notify_member_boss_phase_changed(
        StringName member_id,
        StringName phase_id = default
    ) =>
        _fate_runtime != null
            ? _fate_runtime.handle_member_boss_phase_changed(member_id, phase_id)
            : new GDictionary();

    public void _ensure_sidecars_ready()
    {
        _terrain_effect_system.setup(this);
        _attack_check_policy_service ??= new BattleAttackCheckPolicyService();
        _attack_check_policy_service.setup(this, _hit_resolver, _terrain_effect_system);
        _skill_outcome_committer ??= new BattleSkillOutcomeCommitter();
        _skill_outcome_committer.setup(this);
        _special_profile_commit_adapter ??= new BattleSpecialProfileCommitAdapter();
        _special_profile_commit_adapter.setup(this, _skill_outcome_committer);
        _battle_rating_system.setup(this, _skill_mastery_service);
        _unit_factory.setup(this);
        _charge_resolver.setup(this, _skill_mastery_service);
        _repeat_attack_resolver.setup(this, _skill_mastery_service);
        _change_equipment_resolver.setup(this);
        _loot_resolver.Setup(this);
        _skill_turn_resolver.setup(this);
        _metrics_collector.setup(this);
        _shield_service.setup(this);
        _ground_effect_service.setup(this);
        _special_skill_resolver.setup(this);
        _movement_service.setup(this);
        _layered_barrier_service.Setup(this);
        _timeline_driver.Setup(this);
        _skill_orchestrator.setup(this);
    }

    public WarehouseState _get_party_backpack_state(PartyState party_state)
    {
        return party_state?.warehouse_state;
    }

    public bool is_battle_active() => _state != null && _state.phase != (StringName)"battle_ended";

    public GVector2IArray get_unit_reachable_move_coords(BattleUnitState unit_state)
    {
        _ensure_sidecars_ready();
        return _movement_service.get_unit_reachable_move_coords(unit_state);
    }

    public void end_battle(GDictionary result = null)
    {
        EndBattle(BattleEndOptions.FromDictionary(result));
    }

    internal void EndBattle(BattleEndOptions options)
    {
        if (_state == null)
            return;
        if (_characterGateway != null && options.CommitProgression)
        {
            foreach (StringName allyUnitId in _state.ally_unit_ids)
            {
                _state.TryGetUnitTyped(allyUnitId, out BattleUnitState unitState);
                if (unitState == null)
                    continue;
                if (unitState.is_alive)
                    _characterGateway.commit_battle_resources(
                        unitState.source_member_id,
                        unitState.current_hp,
                        unitState.current_mp,
                        unitState.current_aura
                    );
                else
                    _characterGateway.commit_battle_death(unitState.source_member_id);
            }
            _characterGateway.flush_after_battle();
        }
        if (
            _battle_resolution_result == null
            && !_battle_resolution_result_consumed
            && _state.phase == (StringName)"battle_ended"
        )
            _battle_resolution_result = _build_battle_resolution_result();
    }

    public BattleResolutionResult get_battle_resolution_result()
    {
        if (_battle_resolution_result_consumed)
            return null;
        if (
            _battle_resolution_result == null
            && _state != null
            && _state.phase == (StringName)"battle_ended"
        )
            _battle_resolution_result = _build_battle_resolution_result();
        return _battle_resolution_result;
    }

    public BattleResolutionResult consume_battle_resolution_result()
    {
        if (_battle_resolution_result_consumed)
            return null;
        BattleResolutionResult result = _battle_resolution_result;
        if (result == null && _state != null && _state.phase == (StringName)"battle_ended")
            result = _build_battle_resolution_result();
        if (result != null)
        {
            _pending_post_battle_character_rewards.Clear();
            _active_loot_entries.Clear();
            _looted_defeated_unit_ids.Clear();
        }
        _battle_resolution_result = null;
        _battle_resolution_result_consumed = true;
        return result;
    }

    public BattleGridService get_grid_service() => _grid_service;

    internal IBattleRuntimeCharacterGateway GetCharacterGatewayTyped() => _characterGateway;

    public StringName allocate_equipment_instance_id()
    {
        if (_equipment_instance_id_allocator == null)
            return "";
        return ProgressionDataUtils.to_string_name(_equipment_instance_id_allocator.Invoke());
    }

    public BattleDamageResolver get_damage_resolver() => _damage_resolver;

    public void configure_damage_resolver_for_tests(BattleDamageResolver damage_resolver)
    {
        _damage_resolver = damage_resolver ?? new BattleDamageResolver();
        BindDamageResolver();
        if (_ai_service != null)
            _ai_service.setup(_enemy_ai_brains, _damage_resolver);
        if (_fate_runtime != null)
            _fate_runtime.setup(
                _characterGateway,
                get_fate_event_bus(),
                this,
                _find_unit_by_member_id
            );
        _change_equipment_resolver.setup(this);
        _loot_resolver.Setup(this);
        _skill_turn_resolver.setup(this);
        _metrics_collector.setup(this);
        _shield_service.setup(this);
        _ground_effect_service.setup(this);
        _special_skill_resolver.setup(this);
        _movement_service.setup(this);
        _layered_barrier_service.Setup(this);
        _timeline_driver.Setup(this);
        _skill_orchestrator.setup(this);
    }

    public BattleFateEventBus get_fate_event_bus() =>
        _damage_resolver != null ? _damage_resolver.get_fate_event_bus() : null;

    public BattleHitResolver get_hit_resolver() => _hit_resolver;

    public BattleAttackCheckPolicyService get_attack_check_policy_service()
    {
        _attack_check_policy_service ??= new BattleAttackCheckPolicyService();
        _attack_check_policy_service.setup(this, _hit_resolver, _terrain_effect_system);
        return _attack_check_policy_service;
    }

    public void configure_hit_resolver_for_tests(BattleHitResolver hit_resolver)
    {
        _hit_resolver = hit_resolver ?? new BattleHitResolver();
        if (_damage_resolver != null)
            _damage_resolver.set_hit_resolver(_hit_resolver);
        _attack_check_policy_service?.setup(this, _hit_resolver, _terrain_effect_system);
        _meteor_swarm_resolver?.setup(this, _attack_check_policy_service);
        _skill_outcome_committer?.setup(this);
        _special_profile_commit_adapter?.setup(this, _skill_outcome_committer);
    }

    public BattleTerrainGenerator get_terrain_generator() => _terrain_generator;

    public GDictionary get_skill_defs() => _skill_defs;

    public SkillDef get_skill_def_typed(StringName skill_id)
    {
        if (IsEmpty(skill_id))
        {
            return null;
        }
        return _skillDefIndex.TryGetValue(skill_id, out SkillDef skillDef) ? skillDef : null;
    }

    private void RebuildSkillDefIndex()
    {
        _skillDefIndex.Clear();
        if (_skill_defs == null)
        {
            return;
        }
        foreach (var key in _skill_defs.Keys)
        {
            SkillDef skillDef = _skill_defs[key].As<SkillDef>();
            if (skillDef == null)
            {
                continue;
            }
            StringName keySkillId = ProgressionDataUtils.to_string_name(key);
            if (!IsEmpty(keySkillId) && !_skillDefIndex.ContainsKey(keySkillId))
            {
                _skillDefIndex[keySkillId] = skillDef;
            }
            StringName defSkillId = ProgressionDataUtils.to_string_name(skillDef.skill_id);
            if (!IsEmpty(defSkillId) && !_skillDefIndex.ContainsKey(defSkillId))
            {
                _skillDefIndex[defSkillId] = skillDef;
            }
        }
    }

    private void RebuildEnemyAiBrainIndex()
    {
        _enemyAiBrainIndex.Clear();
        if (_enemy_ai_brains == null)
        {
            return;
        }
        foreach (var key in _enemy_ai_brains.Keys)
        {
            EnemyAiBrainDef brain = _enemy_ai_brains[key].As<EnemyAiBrainDef>();
            if (brain == null)
            {
                continue;
            }
            StringName keyBrainId = ProgressionDataUtils.to_string_name(key);
            if (!IsEmpty(keyBrainId) && !_enemyAiBrainIndex.ContainsKey(keyBrainId))
            {
                _enemyAiBrainIndex[keyBrainId] = brain;
            }
            StringName defBrainId = ProgressionDataUtils.to_string_name(brain.brain_id);
            if (!IsEmpty(defBrainId) && !_enemyAiBrainIndex.ContainsKey(defBrainId))
            {
                _enemyAiBrainIndex[defBrainId] = brain;
            }
        }
    }

    private EnemyAiBrainDef GetEnemyAiBrainTyped(StringName brainId)
    {
        if (IsEmpty(brainId))
        {
            return null;
        }
        return _enemyAiBrainIndex.TryGetValue(brainId, out EnemyAiBrainDef brain)
            ? brain
            : null;
    }

    public GDictionary get_special_profile_registry_snapshot() =>
        _special_profile_registry_snapshot.Duplicate(true);

    public bool _has_special_profile(SkillDef skill_def, StringName profile_id) =>
        skill_def?.combat_profile != null
        && skill_def.combat_profile.special_resolution_profile_id == profile_id;

    public void _append_special_profile_gate_block(
        BattleEventBatch batch,
        BattleSpecialProfileGateResult gate_result
    )
    {
        if (batch == null)
            return;
        string message = "该禁咒配置未通过校验，暂时无法施放。";
        if (gate_result != null && !string.IsNullOrEmpty(gate_result.player_message))
            message = gate_result.player_message;
        if (string.IsNullOrEmpty(message))
            message = "该禁咒配置未通过校验，暂时无法施放。";
        batch.log_lines.Add(message);
    }

    public GDictionary get_item_defs() => _item_defs;

    public int get_min_battle_surface_height() => MIN_BATTLE_SURFACE_HEIGHT;

    internal Dictionary<StringName, BattleRatingMemberStats> GetBattleRatingStatsTyped() =>
        _battleRatingStatsByMemberId;

    public GDictionary get_battle_rating_stats()
    {
        var snapshot = new GDictionary();
        foreach (KeyValuePair<StringName, BattleRatingMemberStats> entry in _battleRatingStatsByMemberId)
        {
            if (entry.Value != null)
            {
                snapshot[entry.Key] = entry.Value.ToDictionary();
            }
        }
        return snapshot;
    }

    public void set_battle_rating_stats(GDictionary stats)
    {
        _battleRatingStatsByMemberId.Clear();
        if (stats == null)
        {
            return;
        }
        foreach (var memberKey in stats.Keys)
        {
            BattleRatingMemberStats memberStats = BattleRatingMemberStats.FromDictionary(
                stats[memberKey].AsGodotDictionary()
            );
            if (memberStats == null)
            {
                continue;
            }
            if (IsEmpty(memberStats.member_id))
            {
                memberStats.member_id = ProgressionDataUtils.to_string_name(memberKey);
            }
            if (!IsEmpty(memberStats.member_id))
            {
                _battleRatingStatsByMemberId[memberStats.member_id] = memberStats;
            }
        }
    }

    public BattleRatingSystem get_battle_rating_system() => _battle_rating_system;

    public GArray get_pending_post_battle_character_rewards() =>
        _pending_post_battle_character_rewards;

    public void set_ai_trace_enabled(bool enabled)
    {
        _ai_trace_enabled = enabled;
        if (!enabled)
            _ai_turn_traces.Clear();
    }

    public Godot.Collections.Array<GDictionary> get_ai_turn_traces() => _ai_turn_traces;

    public void clear_ai_turn_traces() => _ai_turn_traces.Clear();

    public GStringNameArray _collect_ai_trace_decision_target_unit_ids(
        BattleAiDecision decision,
        GDictionary turn_trace
    )
    {
        var unitIds = new GStringNameArray();
        if (decision != null && decision.command != null)
        {
            _add_ai_trace_unit_id(unitIds, decision.command.target_unit_id);
            _add_ai_trace_unit_ids(unitIds, ToUntypedArray(decision.command.target_unit_ids));
        }
        _append_ai_trace_score_target_unit_ids(
            unitIds,
            GetDict(turn_trace, "score_input")
        );
        return unitIds;
    }

    public void _append_ai_trace_score_target_unit_ids(
        GStringNameArray unit_ids,
        GDictionary score
    )
    {
        if (score == null)
            return;
        _add_ai_trace_unit_ids(unit_ids, GetArray(score, "target_unit_ids"));
        _add_ai_trace_unit_id(
            unit_ids,
            GetStringName(score, "target_unit_id")
        );
    }

    public void _add_ai_trace_unit_ids(GStringNameArray unit_ids, GArray raw_unit_ids)
    {
        if (raw_unit_ids == null)
        {
            return;
        }
        foreach (var rawUnitId in raw_unit_ids)
            _add_ai_trace_unit_id(unit_ids, ProgressionDataUtils.to_string_name(rawUnitId));
    }

    public void _add_ai_trace_unit_id(GStringNameArray unit_ids, StringName raw_unit_id)
    {
        StringName unitId = ProgressionDataUtils.to_string_name(raw_unit_id);
        if (IsEmpty(unitId) || unit_ids.Contains(unitId))
            return;
        unit_ids.Add(unitId);
    }

    public GDictionary _build_ai_trace_unit_snapshot_map()
    {
        GDictionary snapshots = new();
        if (_state == null)
            return snapshots;
        foreach (BattleUnitState unitState in _state.GetUnitsTyped())
        {
            if (unitState == null)
                continue;
            snapshots[unitState.unit_id.ToString()] = _build_ai_trace_unit_snapshot(unitState);
        }
        return snapshots;
    }

    public GDictionary _build_ai_trace_unit_snapshot(BattleUnitState unit_state)
    {
        if (unit_state == null)
            return new GDictionary();
        int hpMax = 0;
        int mpMax = 0;
        int staminaMax = 0;
        int auraMax = 0;
        if (unit_state.attribute_snapshot != null)
        {
            hpMax = unit_state
                .attribute_snapshot.get_value(AttributeService.HP_MAX_ID());
            mpMax = unit_state
                .attribute_snapshot.get_value(AttributeService.MP_MAX_ID());
            staminaMax = unit_state
                .attribute_snapshot.get_value(AttributeService.STAMINA_MAX_ID());
            auraMax = unit_state
                .attribute_snapshot.get_value(AttributeService.AURA_MAX_ID());
        }
        return new GDictionary
        {
            ["unit_id"] = unit_state.unit_id.ToString(),
            ["display_name"] = unit_state.display_name,
            ["faction_id"] = unit_state.faction_id.ToString(),
            ["coord"] = _format_ai_trace_coord(unit_state.coord),
            ["alive"] = unit_state.is_alive,
            ["hp"] = unit_state.current_hp,
            ["hp_max"] = Math.Max(hpMax, 1),
            ["mp"] = unit_state.current_mp,
            ["mp_max"] = Math.Max(mpMax, 0),
            ["stamina"] = unit_state.current_stamina,
            ["stamina_max"] = Math.Max(staminaMax, 0),
            ["aura"] = unit_state.current_aura,
            ["aura_max"] = Math.Max(auraMax, 0),
            ["ap"] = unit_state.current_ap,
            ["move_points"] = unit_state.current_move_points,
            ["shield_hp"] = unit_state.current_shield_hp,
            ["shield_max_hp"] = unit_state.shield_max_hp,
        };
    }

    public Godot.Collections.Array<GDictionary> _build_ai_trace_snapshots_for_unit_ids(
        GStringNameArray unit_ids,
        GDictionary snapshot_map
    )
    {
        var snapshots = new Godot.Collections.Array<GDictionary>();
        foreach (StringName unitId in unit_ids)
        {
            string key = unitId.ToString();
            GDictionary snapshot = GetDict(snapshot_map, key);
            if (snapshot.Count > 0)
                snapshots.Add(snapshot.Duplicate(true));
        }
        return snapshots;
    }

    public GDictionary _build_ai_trace_execution_result(
        BattleAiDecision decision,
        BattleEventBatch decision_batch,
        GDictionary unit_snapshots_before,
        GStringNameArray decision_target_unit_ids
    )
    {
        GDictionary unitSnapshotsAfter = _build_ai_trace_unit_snapshot_map();
        var trackedUnitIds = new GStringNameArray();
        foreach (StringName unitId in decision_target_unit_ids)
            _add_ai_trace_unit_id(trackedUnitIds, unitId);
        if (decision?.command != null)
            _add_ai_trace_unit_id(trackedUnitIds, decision.command.unit_id);
        if (decision_batch != null)
            _add_ai_trace_unit_ids(trackedUnitIds, ToUntypedArray(decision_batch.changed_unit_ids));
        BattleCommand command = decision?.command;
        return new GDictionary
        {
            ["command_type"] = command != null ? command.command_type.ToString() : "",
            ["skill_id"] = command != null ? command.skill_id.ToString() : "",
            ["skill_variant_id"] = command != null ? command.skill_variant_id.ToString() : "",
            ["changed_unit_ids"] = _ai_trace_stringify_unit_ids(
                decision_batch != null ? decision_batch.changed_unit_ids : new GStringNameArray()
            ),
            ["tracked_unit_ids"] = _ai_trace_stringify_unit_ids(trackedUnitIds),
            ["unit_results"] = _build_ai_trace_unit_results(
                trackedUnitIds,
                unit_snapshots_before,
                unitSnapshotsAfter
            ),
            ["log_lines"] =
                decision_batch != null ? decision_batch.log_lines.Duplicate() : new GStringArray(),
            ["report_entries"] =
                decision_batch != null
                    ? decision_batch.report_entries.Duplicate(true)
                    : new GArray(),
        };
    }

    public Godot.Collections.Array<GDictionary> _build_ai_trace_unit_results(
        GStringNameArray unit_ids,
        GDictionary unit_snapshots_before,
        GDictionary unit_snapshots_after
    )
    {
        var results = new Godot.Collections.Array<GDictionary>();
        foreach (StringName unitId in unit_ids)
        {
            string key = unitId.ToString();
            GDictionary before = GetDict(unit_snapshots_before, key);
            GDictionary after = GetDict(unit_snapshots_after, key);
            if (before.Count == 0 && after.Count == 0)
                continue;
            int hpBefore = GetInt(before, "hp", GetInt(after, "hp", 0));
            int hpAfter = GetInt(after, "hp", hpBefore);
            int shieldBefore = GetInt(before, "shield_hp", GetInt(after, "shield_hp", 0));
            int shieldAfter = GetInt(after, "shield_hp", shieldBefore);
            bool beforeAlive = BattleRuntimeDictionaryOptions.ReadBool(before, "alive");
            bool afterAlive = BattleRuntimeDictionaryOptions.ReadBool(after, "alive", beforeAlive);
            string coordBefore = GetString(before, "coord", GetString(after, "coord", ""));
            string coordAfter = GetString(after, "coord", coordBefore);
            results.Add(
                new GDictionary
                {
                    ["unit_id"] = key,
                    ["before"] = before,
                    ["after"] = after,
                    ["hp_delta"] = hpAfter - hpBefore,
                    ["hp_damage"] = Math.Max(hpBefore - hpAfter, 0),
                    ["hp_healing"] = Math.Max(hpAfter - hpBefore, 0),
                    ["shield_delta"] = shieldAfter - shieldBefore,
                    ["shield_damage"] = Math.Max(shieldBefore - shieldAfter, 0),
                    ["shield_restored"] = Math.Max(shieldAfter - shieldBefore, 0),
                    ["killed"] = beforeAlive && !afterAlive,
                    ["revived"] = !beforeAlive && afterAlive,
                    ["moved"] = coordBefore != coordAfter,
                }
            );
        }
        return results;
    }

    public GStringArray _ai_trace_stringify_unit_ids(GStringNameArray unit_ids)
    {
        var results = new GStringArray();
        if (unit_ids == null)
            return results;
        foreach (StringName rawUnitId in unit_ids)
        {
            StringName unitId = ProgressionDataUtils.to_string_name(rawUnitId);
            if (!IsEmpty(unitId))
                results.Add(unitId.ToString());
        }
        return results;
    }

    public string _format_ai_trace_coord(Vector2I coord) => $"({coord.X}, {coord.Y})";

    public GDictionary get_battle_metrics() => _battle_metrics;

    public void set_ai_score_profile(BattleAiScoreProfile profile) =>
        _ai_service.set_score_profile(profile);

    public BattleAiScoreProfile get_ai_score_profile() => _ai_service.get_score_profile();

    public int get_terrain_effect_nonce() => _terrain_effect_nonce;

    public int increment_terrain_effect_nonce() => ++_terrain_effect_nonce;

    public BattleEventBatch new_batch() => _new_batch();

    public void merge_batch(BattleEventBatch target_batch, BattleEventBatch source_batch) =>
        _merge_batch(target_batch, source_batch);

    public void append_changed_coord(BattleEventBatch batch, Vector2I coord) =>
        _append_changed_coord(batch, coord);

    public void append_changed_coords(BattleEventBatch batch, GVector2IArray coords) =>
        _append_changed_coords(batch, ToUntypedArray(coords));

    public void append_changed_unit_id(BattleEventBatch batch, StringName unit_id) =>
        _append_changed_unit_id(batch, unit_id);

    public void append_changed_unit_coords(BattleEventBatch batch, BattleUnitState unit_state) =>
        _append_changed_unit_coords(batch, unit_state);

    public void append_batch_log(BattleEventBatch batch, string message) =>
        _append_batch_log(batch, message);

    public void append_result_report_entry(BattleEventBatch batch, GDictionary result) =>
        _append_result_report_entry(batch, result);

    public void append_report_entry(BattleEventBatch batch, GDictionary report_entry) =>
        _append_report_entry_to_batch(batch, report_entry);

    public void clear_defeated_unit(BattleUnitState unit_state, BattleEventBatch batch = null) =>
        _clear_defeated_unit(unit_state, batch);

    public GVector2IArray sort_coords(GArray target_coords) => _sort_coords(target_coords);

    public string format_skill_variant_label(
        SkillDef skill_def,
        CombatCastVariantDef cast_variant
    ) => _format_skill_variant_label(skill_def, cast_variant);

    public void mark_applied_statuses_for_turn_timing(
        BattleUnitState target_unit,
        GArray status_effect_ids
    )
    {
        _initialize_applied_status_timeline_ticks(target_unit, status_effect_ids);
        _fate_runtime?.handle_applied_statuses(target_unit, status_effect_ids ?? new GArray());
    }

    internal void mark_applied_statuses_for_turn_timing(
        BattleUnitState target_unit,
        GStringNameArray status_effect_ids
    )
    {
        GStringNameArray normalizedStatusIds = NormalizeStatusIdArray(status_effect_ids);
        _initialize_applied_status_timeline_ticks(target_unit, ToUntypedStatusIdArray(normalizedStatusIds));
        _fate_runtime?.handle_applied_statuses(target_unit, ToUntypedStatusIdArray(normalizedStatusIds));
    }

    public void _initialize_applied_status_timeline_ticks(
        BattleUnitState target_unit,
        GArray status_effect_ids
    )
    {
        if (target_unit == null)
            return;
        GStringNameArray normalizedStatusIds = NormalizeStatusIdArray(status_effect_ids);
        if (normalizedStatusIds.Count == 0)
            return;
        int currentTu = _state?.timeline != null ? _state.timeline.current_tu : 0;
        foreach (StringName statusId in normalizedStatusIds)
        {
            BattleStatusEffectState statusEntry = target_unit.get_status_effect(statusId);
            if (statusEntry == null || statusEntry.tick_interval_tu <= 0)
                continue;
            if (statusEntry.next_tick_at_tu <= currentTu)
            {
                statusEntry.next_tick_at_tu = currentTu + statusEntry.tick_interval_tu;
                target_unit.set_status_effect(statusEntry);
            }
        }
    }

    private static GStringNameArray NormalizeStatusIdArray(GArray statusEffectIds)
    {
        GStringNameArray normalized = new();
        foreach (var statusIdValue in statusEffectIds ?? new GArray())
        {
            StringName statusId = ProgressionDataUtils.to_string_name(statusIdValue);
            if (statusId == "" || normalized.Contains(statusId))
                continue;
            normalized.Add(statusId);
        }
        return normalized;
    }

    private static GStringNameArray NormalizeStatusIdArray(GStringNameArray statusEffectIds)
    {
        GStringNameArray normalized = new();
        foreach (StringName statusIdValue in statusEffectIds ?? new GStringNameArray())
        {
            StringName statusId = ProgressionDataUtils.to_string_name(statusIdValue);
            if (statusId == "" || normalized.Contains(statusId))
                continue;
            normalized.Add(statusId);
        }
        return normalized;
    }

    private static GArray ToUntypedStatusIdArray(GStringNameArray statusEffectIds)
    {
        GArray result = new();
        foreach (StringName statusId in statusEffectIds ?? new GStringNameArray())
            result.Add(statusId);
        return result;
    }

    public StringName resolve_effect_target_filter(
        SkillDef skill_def,
        CombatEffectDef effect_def
    ) => _resolve_effect_target_filter(skill_def, effect_def);

    public bool is_unit_valid_for_effect(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        StringName target_filter
    ) => _is_unit_valid_for_effect(source_unit, target_unit, target_filter);

    public bool is_unit_effect(CombatEffectDef effect_def) => _is_unit_effect(effect_def);

    public GBattleUnitArray collect_units_in_coords(GVector2IArray effect_coords) =>
        _collect_units_in_coords(effect_coords);

    public int get_unit_skill_level(BattleUnitState unit_state, StringName skill_id) =>
        _get_unit_skill_level(unit_state, skill_id);

    public void record_enemy_defeated_achievement(
        BattleUnitState active_unit,
        BattleUnitState target_unit
    ) => _battle_rating_system.record_enemy_defeated_achievement(active_unit, target_unit);

    public void record_skill_effect_result(
        BattleUnitState source_unit,
        int damage,
        int healing,
        int kill_count
    )
    {
        _battle_rating_system.record_skill_effect_result(source_unit, damage, healing, kill_count);
        if (source_unit == null)
            return;
        GDictionary sourceEntry = _ensure_unit_metric_entry(source_unit);
        GDictionary factionEntry = _ensure_faction_metric_entry(source_unit.faction_id);
        sourceEntry["total_damage_done"] =
            GetInt(sourceEntry, "total_damage_done") + Math.Max(damage, 0);
        sourceEntry["total_healing_done"] =
            GetInt(sourceEntry, "total_healing_done") + Math.Max(healing, 0);
        sourceEntry["kill_count"] = GetInt(sourceEntry, "kill_count") + Math.Max(kill_count, 0);
        factionEntry["total_damage_done"] =
            GetInt(factionEntry, "total_damage_done") + Math.Max(damage, 0);
        factionEntry["total_healing_done"] =
            GetInt(factionEntry, "total_healing_done") + Math.Max(healing, 0);
        factionEntry["kill_count"] = GetInt(factionEntry, "kill_count") + Math.Max(kill_count, 0);
    }

    public void record_battle_contribution_result(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        int damage,
        int healing,
        bool caused_defeat,
        StringName origin_kind,
        StringName skill_id
    )
    {
        _battle_rating_system.record_contribution_from_units(
            source_unit,
            target_unit,
            damage,
            healing,
            caused_defeat,
            origin_kind,
            skill_id
        );
    }

    public void record_contribution_from_units(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        int damage,
        int healing,
        bool caused_defeat,
        StringName origin_kind,
        StringName skill_id
    ) =>
        record_battle_contribution_result(
            source_unit,
            target_unit,
            damage,
            healing,
            caused_defeat,
            origin_kind,
            skill_id
        );

    public void append_result_source_status_effects(
        BattleEventBatch batch,
        BattleUnitState source_unit,
        GDictionary result
    )
    {
        if (source_unit == null || result == null || result.Count == 0)
            return;
        GStringNameArray sourceStatusIds = NormalizeStatusIdArray(
            GetArray(result, "source_status_effect_ids")
        );
        if (sourceStatusIds.Count == 0)
            return;
        mark_applied_statuses_for_turn_timing(source_unit, ToUntypedStatusIdArray(sourceStatusIds));
        _append_changed_unit_id(batch, source_unit.unit_id);
        foreach (StringName statusId in sourceStatusIds)
            batch.log_lines.Add($"{source_unit.display_name} 获得状态 {statusId}。");
    }

    internal void append_result_source_status_effects(
        BattleEventBatch batch,
        BattleUnitState source_unit,
        AttackEffectResolutionResult result
    )
    {
        if (source_unit == null)
            return;
        GStringNameArray sourceStatusIds = NormalizeStatusIdArray(result.SourceStatusEffectIds);
        if (sourceStatusIds.Count == 0)
            return;
        mark_applied_statuses_for_turn_timing(source_unit, sourceStatusIds);
        _append_changed_unit_id(batch, source_unit.unit_id);
        foreach (StringName statusId in sourceStatusIds)
            batch.log_lines.Add($"{source_unit.display_name} 获得状态 {statusId}。");
    }

    public void _initialize_battle_metrics()
    {
        _ensure_sidecars_ready();
        _metrics_collector._initialize_battle_metrics();
    }

    public GDictionary _build_unit_metric_entry(BattleUnitState unit_state)
    {
        _ensure_sidecars_ready();
        return _metrics_collector._build_unit_metric_entry(unit_state);
    }

    public GDictionary _ensure_unit_metric_entry(BattleUnitState unit_state)
    {
        _ensure_sidecars_ready();
        return _metrics_collector._ensure_unit_metric_entry(unit_state);
    }

    public GDictionary _ensure_faction_metric_entry(StringName faction_id)
    {
        _ensure_sidecars_ready();
        return _metrics_collector._ensure_faction_metric_entry(faction_id);
    }

    public void _record_turn_started(BattleUnitState unit_state)
    {
        _ensure_sidecars_ready();
        _metrics_collector._record_turn_started(unit_state);
    }

    public void _record_action_issued(
        BattleUnitState unit_state,
        StringName command_type,
        int ap_cost = 0
    )
    {
        _ensure_sidecars_ready();
        _metrics_collector._record_action_issued(unit_state, command_type, ap_cost);
    }

    public void _record_skill_attempt(BattleUnitState unit_state, StringName skill_id)
    {
        _ensure_sidecars_ready();
        _metrics_collector._record_skill_attempt(unit_state, skill_id);
    }

    public void _record_skill_success(BattleUnitState unit_state, StringName skill_id)
    {
        _ensure_sidecars_ready();
        _metrics_collector._record_skill_success(unit_state, skill_id);
    }

    public void _record_effect_metrics(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        int damage,
        int healing,
        int kill_count
    )
    {
        _ensure_sidecars_ready();
        _metrics_collector._record_effect_metrics(
            source_unit,
            target_unit,
            damage,
            healing,
            kill_count
        );
    }

    public void _record_unit_defeated(BattleUnitState unit_state)
    {
        _ensure_sidecars_ready();
        _metrics_collector._record_unit_defeated(unit_state);
    }

    public void _increment_metric_count(GDictionary metric_map, string key, int delta)
    {
        _ensure_sidecars_ready();
        _metrics_collector._increment_metric_count(metric_map, key, delta);
    }

    public void dispose()
    {
        _terrain_effect_system?.dispose();
        _battle_rating_system?.dispose();
        _unit_factory?.dispose();
        _charge_resolver?.dispose();
        _repeat_attack_resolver?.dispose();
        _change_equipment_resolver?.dispose();
        _loot_resolver?.Dispose();
        _skill_turn_resolver?.dispose();
        _metrics_collector?.dispose();
        _shield_service?.dispose();
        _ground_effect_service?.dispose();
        _special_skill_resolver?.dispose();
        _movement_service?.dispose();
        _layered_barrier_service?.Dispose();
        _timeline_driver?.Dispose();
        _skill_orchestrator?.dispose();
        _meteor_swarm_resolver?.dispose();
        _attack_check_policy_service?.dispose();
        _skill_outcome_committer?.dispose();
        _special_profile_commit_adapter?.dispose();
        _meteor_swarm_resolver = null;
        _special_profile_gate = null;
        _attack_check_policy_service = null;
        _skill_outcome_committer = null;
        _special_profile_commit_adapter = null;
        _skill_mastery_service?.clear();
        _fate_runtime?.dispose();
        _battleRatingStatsByMemberId.Clear();
        _pending_post_battle_character_rewards.Clear();
        _active_loot_entries.Clear();
        _looted_defeated_unit_ids.Clear();
        _ai_turn_traces.Clear();
        _ai_action_plans_by_unit_id.Clear();
        _battle_metrics.Clear();
        calamity_by_member_id.Clear();
        _battle_resolution_result = null;
        _battle_resolution_result_consumed = false;
        _terrain_effect_nonce = 0;
        _ai_trace_enabled = false;
        _characterGateway = null;
        _skill_defs = new GDictionary();
        _skillDefIndex.Clear();
        _enemyAiBrainIndex.Clear();
        _special_profile_registry_snapshot = new GDictionary();
        _item_defs = new GDictionary();
        _enemy_templates = new GDictionary();
        _enemy_ai_brains = new GDictionary();
        _encounter_builder = null;
        _equipment_drop_service = null;
        _equipment_instance_id_allocator = null;
        if (_state != null)
        {
            _state.cells.Clear();
            _state.units.Clear();
            _state.ally_unit_ids.Clear();
            _state.enemy_unit_ids.Clear();
            _state.timeline?.ready_unit_ids.Clear();
        }
        _state = null;
    }

    public bool _place_units(
        GArray units,
        GArray spawn_coords,
        bool is_ally,
        StringName spawn_side = default
    ) => PlaceUnitsTyped(
        ToBattleUnitArray(units),
        ToVector2IArray(spawn_coords),
        is_ally,
        spawn_side
    );

    private bool PlaceUnitsTyped(
        GBattleUnitArray units,
        GVector2IArray spawnCoordValues,
        bool is_ally,
        StringName spawn_side = default
    )
    {
        var placedUnits = new GBattleUnitArray();
        for (int index = 0; index < units.Count; index++)
        {
            BattleUnitState unitState = units[index];
            if (unitState == null)
                continue;
            unitState.refresh_footprint();
            var preferredCoords = new GVector2IArray();
            if (index < spawnCoordValues.Count)
                preferredCoords.Add(spawnCoordValues[index]);
            foreach (Vector2I coord in spawnCoordValues)
            {
                if (!preferredCoords.Contains(coord))
                    preferredCoords.Add(coord);
            }
            Vector2I placementCoord = _find_spawn_anchor(unitState, preferredCoords, spawn_side);
            if (placementCoord == new Vector2I(-1, -1))
            {
                _clear_spawn_placed_units(placedUnits, is_ally);
                return false;
            }
            if (!_place_spawn_unit_at_anchor(unitState, placementCoord))
            {
                _clear_spawn_placed_units(placedUnits, is_ally);
                return false;
            }
            if (is_ally)
                _state.ally_unit_ids.Add(unitState.unit_id);
            else
                _state.enemy_unit_ids.Add(unitState.unit_id);
            placedUnits.Add(unitState);
        }
        return true;
    }

    public bool _place_units(GArray units, GArray spawn_coords, bool is_ally) =>
        _place_units(units, spawn_coords, is_ally, "");

    public void _clear_spawn_placed_units(GBattleUnitArray placed_units, bool is_ally)
    {
        if (_state == null)
            return;
        foreach (BattleUnitState unitState in placed_units)
        {
            if (unitState == null)
                continue;
            _grid_service.clear_unit_occupancy(_state, unitState);
            _state.units.Remove(unitState.unit_id);
            if (is_ally)
                _state.ally_unit_ids.Remove(unitState.unit_id);
            else
                _state.enemy_unit_ids.Remove(unitState.unit_id);
        }
    }

    public bool _place_spawn_unit_at_anchor(BattleUnitState unit_state, Vector2I coord)
    {
        if (_state == null || unit_state == null)
            return false;
        if (!_can_place_spawn_anchor(unit_state, coord))
            return false;
        unit_state.set_anchor_coord(coord);
        _state.units[unit_state.unit_id] = unit_state;
        _grid_service.set_occupants(
            _state,
            ToUntypedArray(unit_state.occupied_coords),
            unit_state.unit_id
        );
        return true;
    }

    public Vector2I _find_spawn_anchor(
        BattleUnitState unit_state,
        GVector2IArray preferred_coords,
        StringName spawn_side = default
    )
    {
        if (_state == null || unit_state == null)
            return new Vector2I(-1, -1);
        Vector2I bestCoord = new(-1, -1);
        int bestScore = int.MinValue + 1;
        for (int preferredIndex = 0; preferredIndex < preferred_coords.Count; preferredIndex++)
        {
            Vector2I coord = preferred_coords[preferredIndex];
            if (!_can_place_spawn_anchor(unit_state, coord, spawn_side))
                continue;
            int score = _score_spawn_anchor(unit_state, coord, preferredIndex);
            if (score > bestScore)
            {
                bestScore = score;
                bestCoord = coord;
            }
        }
        if (bestCoord != new Vector2I(-1, -1))
            return bestCoord;
        foreach (Vector2I coord in preferred_coords)
        {
            if (_can_place_spawn_anchor(unit_state, coord, spawn_side))
                return coord;
        }
        for (int y = 0; y < _state.map_size.Y; y++)
        {
            for (int x = 0; x < _state.map_size.X; x++)
            {
                var coord = new Vector2I(x, y);
                if (_can_place_spawn_anchor(unit_state, coord, spawn_side))
                    return coord;
            }
        }
        return new Vector2I(-1, -1);
    }

    public Vector2I _find_spawn_anchor(
        BattleUnitState unit_state,
        GVector2IArray preferred_coords
    ) => _find_spawn_anchor(unit_state, preferred_coords, "");

    public bool _can_place_spawn_anchor(
        BattleUnitState unit_state,
        Vector2I coord,
        StringName spawn_side = default
    )
    {
        if (_state == null || unit_state == null)
            return false;
        if (
            !_grid_service.can_place_footprint(
                _state,
                coord,
                unit_state.footprint_size,
                unit_state.unit_id,
                unit_state
            )
        )
            return false;
        if (!IsEmpty(spawn_side) && !_footprint_matches_spawn_side(unit_state, coord, spawn_side))
            return false;
        foreach (Vector2I footprintCoord in _grid_service.get_unit_target_coords(unit_state, coord))
        {
            BattleCellState cell = _grid_service.get_cell(_state, footprintCoord);
            if (cell == null || BattleTerrainRules.is_water_terrain(cell.base_terrain))
                return false;
        }
        return true;
    }

    public StringName _resolve_spawn_side_from_coords(GArray spawn_coords)
    {
        if (_state == null || _get_long_edge_side_extent() <= 1)
            return "";
        int nearCount = 0;
        int farCount = 0;
        foreach (Vector2I coord in ToVector2IArray(spawn_coords))
        {
            if (_coord_matches_spawn_side(coord, SPAWN_SIDE_NEAR_LONG_EDGE_VALUE))
                nearCount++;
            else if (_coord_matches_spawn_side(coord, SPAWN_SIDE_FAR_LONG_EDGE_VALUE))
                farCount++;
        }
        if (nearCount == 0 && farCount == 0)
            return "";
        return nearCount >= farCount
            ? SPAWN_SIDE_NEAR_LONG_EDGE_VALUE
            : SPAWN_SIDE_FAR_LONG_EDGE_VALUE;
    }

    public StringName _get_opposite_spawn_side(StringName spawn_side)
    {
        if (spawn_side == SPAWN_SIDE_NEAR_LONG_EDGE_VALUE)
            return SPAWN_SIDE_FAR_LONG_EDGE_VALUE;
        if (spawn_side == SPAWN_SIDE_FAR_LONG_EDGE_VALUE)
            return SPAWN_SIDE_NEAR_LONG_EDGE_VALUE;
        return "";
    }

    public bool _footprint_matches_spawn_side(
        BattleUnitState unit_state,
        Vector2I coord,
        StringName spawn_side
    )
    {
        if (_state == null || unit_state == null)
            return false;
        foreach (Vector2I footprintCoord in _grid_service.get_unit_target_coords(unit_state, coord))
        {
            if (!_coord_matches_spawn_side(footprintCoord, spawn_side))
                return false;
        }
        return true;
    }

    public bool _coord_matches_spawn_side(Vector2I coord, StringName spawn_side)
    {
        if (_state == null || _get_long_edge_side_extent() <= 1)
            return true;
        int sideValue = _get_long_edge_side_axis_value(coord);
        int splitValue = Mathf.FloorToInt(_get_long_edge_side_extent() * 0.5f);
        if (spawn_side == SPAWN_SIDE_NEAR_LONG_EDGE_VALUE)
            return sideValue < splitValue;
        if (spawn_side == SPAWN_SIDE_FAR_LONG_EDGE_VALUE)
            return sideValue >= splitValue;
        return true;
    }

    public int _get_long_edge_side_axis_value(Vector2I coord) =>
        _state == null ? 0 : (_state.map_size.X >= _state.map_size.Y ? coord.Y : coord.X);

    public int _get_long_edge_side_extent() =>
        _state == null
            ? 0
            : (_state.map_size.X >= _state.map_size.Y ? _state.map_size.Y : _state.map_size.X);

    public int _score_spawn_anchor(BattleUnitState unit_state, Vector2I coord, int preferred_index)
    {
        int mobilityScore = _count_spawn_anchor_reachable_coords(unit_state, coord);
        int edgeClearance = _get_spawn_anchor_edge_clearance(unit_state, coord);
        int centerBias = _get_spawn_anchor_center_bias(unit_state, coord);
        return mobilityScore * 100 + edgeClearance * 18 + centerBias * 4 - preferred_index;
    }

    public int _count_spawn_anchor_reachable_coords(
        BattleUnitState unit_state,
        Vector2I start_coord
    )
    {
        if (_state == null || unit_state == null)
            return 0;
        int moveBudget = Math.Min(Math.Max(unit_state.current_move_points, 0), 4);
        if (moveBudget <= 0)
            moveBudget = 1;
        var bestCosts = new Dictionary<Vector2I, int> { [start_coord] = 0 };
        var frontier = new List<Vector2I> { start_coord };
        int frontierIndex = 0;
        while (frontierIndex < frontier.Count)
        {
            Vector2I currentCoord = frontier[frontierIndex++];
            int spentCost = bestCosts[currentCoord];
            foreach (Vector2I neighborCoord in _grid_service.get_neighbors_4(_state, currentCoord))
            {
                if (
                    !_grid_service.can_unit_step_between_anchors(
                        _state,
                        unit_state,
                        currentCoord,
                        neighborCoord
                    )
                )
                    continue;
                int nextCost =
                    spentCost + _grid_service.get_unit_move_cost(_state, unit_state, neighborCoord);
                if (nextCost > moveBudget)
                    continue;
                if (
                    bestCosts.TryGetValue(neighborCoord, out int existingCost)
                    && nextCost >= existingCost
                )
                    continue;
                bestCosts[neighborCoord] = nextCost;
                frontier.Add(neighborCoord);
            }
        }
        return bestCosts.Count - 1;
    }

    public int _get_spawn_anchor_edge_clearance(BattleUnitState unit_state, Vector2I coord)
    {
        if (_state == null || unit_state == null)
            return 0;
        Vector2I footprint = unit_state.footprint_size;
        int left = coord.X;
        int top = coord.Y;
        int right = _state.map_size.X - (coord.X + footprint.X);
        int bottom = _state.map_size.Y - (coord.Y + footprint.Y);
        return Math.Min(Math.Min(left, right), Math.Min(top, bottom));
    }

    public int _get_spawn_anchor_center_bias(BattleUnitState unit_state, Vector2I coord)
    {
        if (_state == null || unit_state == null)
            return 0;
        Vector2I footprint = unit_state.footprint_size;
        float centerX = (_state.map_size.X - footprint.X) * 0.5f;
        float centerY = (_state.map_size.Y - footprint.Y) * 0.5f;
        float distance = Mathf.Abs(coord.X - centerX) + Mathf.Abs(coord.Y - centerY);
        return -Mathf.RoundToInt(distance * 10.0f);
    }

    public int _get_move_cost_for_unit_target(BattleUnitState unit_state, Vector2I target_coord)
    {
        _ensure_sidecars_ready();
        return _movement_service._get_move_cost_for_unit_target(unit_state, target_coord);
    }

    public int _get_move_path_cost(BattleUnitState unit_state, GVector2IArray anchor_path)
    {
        _ensure_sidecars_ready();
        return _movement_service._get_move_path_cost(unit_state, ToUntypedArray(anchor_path));
    }

    public int _get_status_move_cost_delta(BattleUnitState unit_state)
    {
        _ensure_sidecars_ready();
        return _movement_service._get_status_move_cost_delta(unit_state);
    }

    public GDictionary _resolve_move_path_result(BattleUnitState active_unit, Vector2I target_coord)
    {
        _ensure_sidecars_ready();
        return _movement_service._resolve_move_path_result(active_unit, target_coord);
    }

    public int _get_available_move_points(BattleUnitState unit_state)
    {
        _ensure_sidecars_ready();
        return _movement_service._get_available_move_points(unit_state);
    }

    public bool _is_normal_movement_locked(BattleUnitState unit_state)
    {
        _ensure_sidecars_ready();
        return _movement_service._is_normal_movement_locked(unit_state);
    }

    public void _handle_move_command(
        BattleUnitState active_unit,
        BattleCommand command,
        BattleEventBatch batch
    )
    {
        _ensure_sidecars_ready();
        _movement_service._handle_move_command(active_unit, command, batch);
    }

    public void _preview_change_equipment_command(
        BattleUnitState active_unit,
        BattleCommand command,
        BattlePreview preview
    ) => _change_equipment_resolver.preview_command(active_unit, command, preview);

    public void _handle_change_equipment_command(
        BattleUnitState active_unit,
        BattleCommand command,
        BattleEventBatch batch
    ) => _change_equipment_resolver.handle_command(active_unit, command, batch);

    public int _get_unit_hp_max(BattleUnitState unit_state) =>
        _change_equipment_resolver.get_unit_hp_max(unit_state);

    public int _get_unit_stamina_max(BattleUnitState unit_state) =>
        _change_equipment_resolver.get_unit_stamina_max(unit_state);

    public bool _move_unit_along_validated_path(
        BattleUnitState active_unit,
        GVector2IArray anchor_path,
        Vector2I target_coord,
        BattleEventBatch batch
    )
    {
        _ensure_sidecars_ready();
        return _movement_service._move_unit_along_validated_path(
            active_unit,
            ToUntypedArray(anchor_path),
            target_coord,
            batch
        );
    }

    public void _handle_skill_command(
        BattleUnitState active_unit,
        BattleCommand command,
        BattleEventBatch batch
    )
    {
        _ensure_sidecars_ready();
        _skill_orchestrator._handle_skill_command(active_unit, command, batch);
    }

    public void _preview_skill_command(
        BattleUnitState active_unit,
        BattleCommand command,
        BattlePreview preview
    )
    {
        _ensure_sidecars_ready();
        _skill_orchestrator._preview_skill_command(active_unit, command, preview);
    }

    public void _preview_unit_skill_command(
        BattleUnitState active_unit,
        BattleCommand command,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        BattlePreview preview
    )
    {
        _ensure_sidecars_ready();
        _skill_orchestrator._preview_unit_skill_command(
            active_unit,
            command,
            skill_def,
            cast_variant,
            preview
        );
    }

    public void _preview_ground_skill_command(
        BattleUnitState active_unit,
        BattleCommand command,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        BattlePreview preview
    )
    {
        _ensure_sidecars_ready();
        _skill_orchestrator._preview_ground_skill_command(
            active_unit,
            command,
            skill_def,
            cast_variant,
            preview
        );
    }

    public AttackPreviewData _build_unit_skill_hit_preview(
        BattleUnitState active_unit,
        GArray target_units,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant
    )
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._build_unit_skill_hit_preview(
            active_unit,
            target_units,
            skill_def,
            cast_variant
        );
    }

    public GDictionary _build_unit_skill_damage_preview(
        BattleUnitState active_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant
    )
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._build_unit_skill_damage_preview(
            active_unit,
            skill_def,
            cast_variant
        );
    }

    public void _append_damage_preview_line(BattlePreview preview)
    {
        _ensure_sidecars_ready();
        _skill_orchestrator._append_damage_preview_line(preview);
    }

    public GDictionary summarize_damage_result(GDictionary result)
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator.summarize_damage_result(result);
    }

    public string build_damage_absorb_reason_text(GDictionary summary)
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator.build_damage_absorb_reason_text(summary);
    }

    public void append_damage_result_log_lines(
        BattleEventBatch batch,
        string subject_label,
        string target_display_name,
        GDictionary result
    )
    {
        _ensure_sidecars_ready();
        _skill_orchestrator.append_damage_result_log_lines(
            batch,
            subject_label,
            target_display_name,
            result
        );
    }

    internal void append_damage_result_log_lines(
        BattleEventBatch batch,
        string subject_label,
        string target_display_name,
        AttackEffectResolutionResult result
    )
    {
        _ensure_sidecars_ready();
        _skill_orchestrator.append_damage_result_log_lines(
            batch,
            subject_label,
            target_display_name,
            result
        );
    }

    public GStringArray _build_unit_skill_resolution_preview_lines(
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant
    )
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._build_unit_skill_resolution_preview_lines(
            active_unit,
            target_unit,
            skill_def,
            cast_variant
        );
    }

    public string _build_skill_log_subject_label(
        BattleUnitState source_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant = null
    )
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._build_skill_log_subject_label(
            source_unit,
            skill_def,
            cast_variant
        );
    }

    public bool _handle_unit_skill_command(
        BattleUnitState active_unit,
        BattleCommand command,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        BattleEventBatch batch
    )
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._handle_unit_skill_command(
            active_unit,
            command,
            skill_def,
            cast_variant,
            batch
        );
    }

    public bool _should_route_skill_command_to_unit_targeting(
        SkillDef skill_def,
        BattleCommand command
    )
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._should_route_skill_command_to_unit_targeting(
            skill_def,
            command
        );
    }

    public GDictionary _validate_unit_skill_targets(
        BattleUnitState active_unit,
        BattleCommand command,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant = null
    )
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._validate_unit_skill_targets(
            active_unit,
            command,
            skill_def,
            cast_variant
        );
    }

    public BattleUnitSkillValidationResult _validate_unit_skill_targets_result(
        BattleUnitState active_unit,
        BattleCommand command,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant = null
    )
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._validate_unit_skill_targets_result(
            active_unit,
            command,
            skill_def,
            cast_variant
        );
    }

    public GStringNameArray _normalize_target_unit_ids(BattleCommand command)
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._normalize_target_unit_ids(command, false);
    }

    public GStringNameArray _sort_target_unit_ids_for_execution(GStringNameArray target_unit_ids)
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._sort_target_unit_ids_for_execution(target_unit_ids);
    }

    public bool _is_multi_unit_skill(SkillDef skill_def)
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._is_multi_unit_skill(skill_def);
    }

    public bool _can_skill_target_unit(
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        bool require_ap = true,
        CombatCastVariantDef cast_variant = null
    )
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._can_skill_target_unit(
            active_unit,
            target_unit,
            skill_def,
            require_ap,
            cast_variant
        );
    }

    public GDictionary _resolve_unit_skill_effect_result(
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        GCombatEffectArray effect_defs
    )
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._resolve_unit_skill_effect_result(
            active_unit,
            target_unit,
            skill_def,
            effect_defs
        );
    }

    public bool _should_resolve_unit_skill_as_fate_attack(
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        GCombatEffectArray effect_defs
    )
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._should_resolve_unit_skill_as_fate_attack(
            active_unit,
            target_unit,
            skill_def,
            effect_defs
        );
    }

    public bool _apply_unit_skill_result(
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        GCombatEffectArray effect_defs,
        BattleEventBatch batch,
        GDictionary spell_control_context = null
    )
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._apply_unit_skill_result(
            active_unit,
            target_unit,
            skill_def,
            cast_variant,
            effect_defs,
            batch,
            BattleSpellControlResult.FromDictionary(spell_control_context)
        );
    }

    public void _apply_chain_damage_effects(
        BattleUnitState source_unit,
        BattleUnitState primary_target,
        SkillDef skill_def,
        GCombatEffectArray effect_defs,
        GDictionary primary_result,
        BattleEventBatch batch,
        string skill_subject,
        GDictionary spell_control_context = null
    )
    {
        _ensure_sidecars_ready();
        _skill_orchestrator._apply_chain_damage_effects(
            source_unit,
            primary_target,
            skill_def,
            effect_defs,
            primary_result,
            batch,
            skill_subject,
            BattleSpellControlResult.FromDictionary(spell_control_context)
        );
    }

    public GCombatEffectArray _collect_chain_damage_effect_defs(GCombatEffectArray effect_defs)
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._collect_chain_damage_effect_defs(effect_defs);
    }

    public GDictionary _get_effect_params(CombatEffectDef effect_def)
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._get_effect_params(effect_def);
    }

    public GCombatEffectArray _build_chain_target_effect_defs(
        GCombatEffectArray effect_defs,
        CombatEffectDef chain_effect
    )
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._build_chain_target_effect_defs(effect_defs, chain_effect);
    }

    public GBattleUnitArray _collect_chain_damage_targets(
        BattleUnitState source_unit,
        BattleUnitState primary_target,
        SkillDef skill_def,
        CombatEffectDef chain_effect,
        GDictionary spell_control_context = null
    )
    {
        _ensure_sidecars_ready();
        return _typed_battle_units(
            _skill_orchestrator._collect_chain_damage_targets(
                source_unit,
                primary_target,
                skill_def,
                chain_effect,
                BattleSpellControlResult.FromDictionary(spell_control_context)
            )
        );
    }

    public int _resolve_chain_damage_radius(
        BattleUnitState primary_target,
        CombatEffectDef chain_effect,
        GDictionary spell_control_context = null
    )
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._resolve_chain_damage_radius(
            primary_target,
            chain_effect,
            BattleSpellControlResult.FromDictionary(spell_control_context)
        );
    }

    public bool _unit_stands_on_terrain_effect(
        BattleUnitState unit_state,
        StringName terrain_effect_id
    )
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._unit_stands_on_terrain_effect(unit_state, terrain_effect_id);
    }

    public bool _is_unit_in_chain_radius(
        BattleUnitState primary_target,
        BattleUnitState candidate,
        int radius,
        CombatEffectDef chain_effect
    )
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._is_within_chain_radius(primary_target, candidate, radius);
    }

    public bool _is_within_chain_radius(
        BattleUnitState primary_target,
        BattleUnitState candidate,
        int max_radius
    )
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._is_within_chain_radius(primary_target, candidate, max_radius);
    }

    public bool _is_chain_height_valid(BattleUnitState from_unit, BattleUnitState to_unit)
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._is_chain_path_clear(from_unit, to_unit);
    }

    public GVector2IArray _get_line_coords(Vector2I from, Vector2I to)
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._get_line_coords(from, to);
    }

    public bool _is_chain_path_clear(BattleUnitState source_unit, BattleUnitState target_unit)
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._is_chain_path_clear(source_unit, target_unit);
    }

    public void _apply_on_kill_gain_resources_effects(
        BattleUnitState source_unit,
        BattleUnitState defeated_unit,
        SkillDef skill_def,
        GCombatEffectArray effect_defs,
        BattleEventBatch batch
    )
    {
        _ensure_sidecars_ready();
        _special_skill_resolver._apply_on_kill_gain_resources_effects(
            source_unit,
            defeated_unit,
            skill_def,
            effect_defs ?? new GCombatEffectArray(),
            batch
        );
    }

    public GDictionary _apply_unit_skill_special_effects(
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        GCombatEffectArray effect_defs,
        BattleEventBatch batch,
        BattleForcedMoveContext forced_move_context = default
    )
    {
        return ApplyUnitSkillSpecialEffectsResult(
                active_unit,
                target_unit,
                skill_def,
                cast_variant,
                effect_defs,
                batch,
                forced_move_context
            )
            .ToDictionary();
    }

    public BattleSpecialSkillResult ApplyUnitSkillSpecialEffectsResult(
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        GCombatEffectArray effect_defs,
        BattleEventBatch batch,
        BattleForcedMoveContext forced_move_context = default
    )
    {
        _ensure_sidecars_ready();
        return _special_skill_resolver.ApplyUnitSkillSpecialEffectsResult(
            active_unit,
            target_unit,
            skill_def,
            cast_variant,
            effect_defs ?? new GCombatEffectArray(),
            batch,
            forced_move_context
        );
    }

    public GDictionary _apply_doom_shift_effect(
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        BattleEventBatch batch
    )
    {
        _ensure_sidecars_ready();
        return _special_skill_resolver._apply_doom_shift_effect(active_unit, target_unit, batch);
    }

    public bool _swap_unit_positions(
        BattleUnitState first_unit,
        BattleUnitState second_unit,
        BattleEventBatch batch
    )
    {
        _ensure_sidecars_ready();
        return _special_skill_resolver._swap_unit_positions(first_unit, second_unit, batch);
    }

    public GDictionary _apply_black_star_brand_effect(
        BattleUnitState active_unit,
        BattleUnitState target_unit
    )
    {
        _ensure_sidecars_ready();
        return _special_skill_resolver._apply_black_star_brand_effect(active_unit, target_unit);
    }

    public void _set_runtime_status_effect(
        BattleUnitState unit_state,
        StringName status_id,
        int duration_tu,
        StringName source_unit_id = default,
        int power = 1,
        GDictionary @params = null,
        bool counts_as_debuff_override = false,
        bool counts_as_debuff = false,
        bool lock_counterattack = false,
        int main_skill_lock_other_debuff_count = 0
    )
    {
        _ensure_sidecars_ready();
        _special_skill_resolver._set_runtime_status_effect(
            unit_state,
            status_id,
            duration_tu,
            source_unit_id,
            power,
            @params ?? new GDictionary(),
            counts_as_debuff_override,
            counts_as_debuff,
            lock_counterattack,
            main_skill_lock_other_debuff_count
        );
    }

    public void _clear_black_star_brand_statuses(BattleUnitState unit_state)
    {
        _ensure_sidecars_ready();
        _special_skill_resolver._clear_black_star_brand_statuses(unit_state);
    }

    public bool _is_black_star_brand_elite_target(BattleUnitState unit_state)
    {
        _ensure_sidecars_ready();
        return _special_skill_resolver._is_black_star_brand_elite_target(unit_state);
    }

    public bool _is_elite_or_boss_target(BattleUnitState unit_state)
    {
        _ensure_sidecars_ready();
        return _special_skill_resolver._is_elite_or_boss_target(unit_state);
    }

    public bool _is_boss_target(BattleUnitState unit_state)
    {
        _ensure_sidecars_ready();
        return _special_skill_resolver._is_boss_target(unit_state);
    }

    public bool _is_black_star_brand_skill(StringName skill_id)
    {
        _ensure_sidecars_ready();
        return _special_skill_resolver._is_black_star_brand_skill(skill_id);
    }

    public bool _is_black_contract_push_skill(StringName skill_id)
    {
        _ensure_sidecars_ready();
        return _special_skill_resolver._is_black_contract_push_skill(skill_id);
    }

    public bool _is_doom_shift_skill(StringName skill_id)
    {
        _ensure_sidecars_ready();
        return _special_skill_resolver._is_doom_shift_skill(skill_id);
    }

    public bool _is_black_crown_seal_skill(StringName skill_id)
    {
        _ensure_sidecars_ready();
        return _special_skill_resolver._is_black_crown_seal_skill(skill_id);
    }

    public void _clear_crown_break_seal_statuses(BattleUnitState unit_state)
    {
        _ensure_sidecars_ready();
        _special_skill_resolver._clear_crown_break_seal_statuses(unit_state);
    }

    public bool _is_crown_break_target_eligible(
        BattleUnitState active_unit,
        BattleUnitState target_unit
    )
    {
        _ensure_sidecars_ready();
        return _special_skill_resolver._is_crown_break_target_eligible(active_unit, target_unit);
    }

    public bool _is_crown_break_skill(StringName skill_id)
    {
        _ensure_sidecars_ready();
        return _special_skill_resolver._is_crown_break_skill(skill_id);
    }

    public bool _is_doom_sentence_target_eligible(
        BattleUnitState active_unit,
        BattleUnitState target_unit
    )
    {
        _ensure_sidecars_ready();
        return _special_skill_resolver._is_doom_sentence_target_eligible(active_unit, target_unit);
    }

    public bool _is_black_crown_seal_target_eligible(
        BattleUnitState active_unit,
        BattleUnitState target_unit
    )
    {
        _ensure_sidecars_ready();
        return _special_skill_resolver._is_black_crown_seal_target_eligible(
            active_unit,
            target_unit
        );
    }

    public bool _is_doom_sentence_skill(StringName skill_id)
    {
        _ensure_sidecars_ready();
        return _special_skill_resolver._is_doom_sentence_skill(skill_id);
    }

    public string _get_unit_skill_target_validation_message(
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant = null
    )
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._get_unit_skill_target_validation_message(
            active_unit,
            target_unit,
            skill_def,
            cast_variant
        );
    }

    public string _get_body_size_category_override_validation_message(
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant = null
    )
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._get_body_size_category_override_validation_message(
            active_unit,
            target_unit,
            skill_def,
            cast_variant
        );
    }

    public bool _skill_grants_guarding(SkillDef skill_def)
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._skill_grants_guarding(skill_def);
    }

    public int _apply_forced_move_effect(
        BattleUnitState source_unit,
        BattleUnitState unit_state,
        CombatEffectDef effect_def,
        BattleEventBatch batch,
        BattleForcedMoveContext forced_move_context = default
    )
    {
        _ensure_sidecars_ready();
        return _special_skill_resolver._apply_forced_move_effect(
            source_unit,
            unit_state,
            effect_def,
            batch,
            forced_move_context
        );
    }

    public int ApplyForcedMoveEffect(
        BattleUnitState source_unit,
        BattleUnitState unit_state,
        CombatEffectDef effect_def,
        BattleEventBatch batch,
        BattleForcedMoveContext forced_move_context
    )
    {
        _ensure_sidecars_ready();
        return _special_skill_resolver.ApplyForcedMoveEffect(
            source_unit,
            unit_state,
            effect_def,
            batch,
            forced_move_context
        );
    }

    public GDictionary _apply_body_size_category_override_effect(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        CombatEffectDef effect_def,
        BattleEventBatch batch
    )
    {
        _ensure_sidecars_ready();
        return _special_skill_resolver._apply_body_size_category_override_effect(
            source_unit,
            target_unit,
            effect_def,
            batch
        );
    }

    public bool _blocks_enemy_forced_move(BattleUnitState source_unit, BattleUnitState target_unit)
    {
        _ensure_sidecars_ready();
        return _special_skill_resolver._blocks_enemy_forced_move(source_unit, target_unit);
    }

    public void _record_vajra_body_mastery_from_incoming_damage(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        GDictionary result,
        BattleEventBatch batch = null
    )
    {
        _ensure_sidecars_ready();
        _special_skill_resolver._record_vajra_body_mastery_from_incoming_damage(
            source_unit,
            target_unit,
            skill_def,
            result,
            batch
        );
    }

    internal void RecordVajraBodyMasteryFromIncomingDamageTyped(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        SkillDef skillDef,
        AttackEffectResolutionResult result,
        BattleEventBatch batch = null
    )
    {
        _ensure_sidecars_ready();
        _special_skill_resolver.RecordVajraBodyMasteryFromIncomingDamageTyped(
            sourceUnit,
            targetUnit,
            skillDef,
            result,
            batch
        );
    }

    public Vector2I _pick_forced_move_coord(
        BattleUnitState unit_state,
        StringName mode,
        BattleUnitState source_unit = null,
        BattleForcedMoveContext forced_move_context = default
    )
    {
        _ensure_sidecars_ready();
        return _special_skill_resolver._pick_forced_move_coord(
            unit_state,
            mode,
            source_unit,
            forced_move_context
        );
    }

    public Vector2I PickForcedMoveCoord(
        BattleUnitState unit_state,
        StringName mode,
        BattleUnitState source_unit,
        BattleForcedMoveContext forced_move_context
    )
    {
        _ensure_sidecars_ready();
        return _special_skill_resolver.PickForcedMoveCoord(
            unit_state,
            mode,
            source_unit,
            forced_move_context
        );
    }

    public int _score_forced_move_coord(
        BattleUnitState unit_state,
        Vector2I candidate_coord,
        StringName mode,
        BattleUnitState source_unit = null,
        BattleForcedMoveContext forced_move_context = default
    )
    {
        _ensure_sidecars_ready();
        return _special_skill_resolver._score_forced_move_coord(
            unit_state,
            candidate_coord,
            mode,
            source_unit,
            forced_move_context
        );
    }

    public int ScoreForcedMoveCoord(
        BattleUnitState unit_state,
        Vector2I candidate_coord,
        StringName mode,
        BattleUnitState source_unit,
        BattleForcedMoveContext forced_move_context
    )
    {
        _ensure_sidecars_ready();
        return _special_skill_resolver.ScoreForcedMoveCoord(
            unit_state,
            candidate_coord,
            mode,
            source_unit,
            forced_move_context
        );
    }

    public GBattleUnitArray _collect_hostile_units_for(BattleUnitState unit_state)
    {
        _ensure_sidecars_ready();
        var result = new GBattleUnitArray();
        foreach (BattleUnitState hostileUnit in _special_skill_resolver.CollectHostileUnitsFor(unit_state))
        {
            result.Add(hostileUnit);
        }
        return result;
    }

    public GCombatEffectArray _collect_unit_skill_effect_defs(
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        BattleUnitState active_unit = null
    )
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._collect_unit_skill_effect_defs(
            skill_def,
            cast_variant,
            active_unit
        );
    }

    public bool _handle_ground_skill_command(
        BattleUnitState active_unit,
        BattleCommand command,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        BattleEventBatch batch
    )
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._handle_ground_skill_command(
            active_unit,
            command,
            skill_def,
            cast_variant,
            batch
        );
    }

    public GDictionary _resolve_ground_spell_control_after_cost(
        BattleUnitState active_unit,
        SkillDef skill_def,
        int spent_mp,
        BattleEventBatch batch
    )
    {
        _ensure_sidecars_ready();
        return _ground_effect_service._resolve_ground_spell_control_after_cost(
            active_unit,
            skill_def,
            spent_mp,
            batch
        );
    }

    public BattleSpellControlResult _resolve_ground_spell_control_after_cost_result(
        BattleUnitState active_unit,
        SkillDef skill_def,
        int spent_mp,
        BattleEventBatch batch
    )
    {
        _ensure_sidecars_ready();
        return _ground_effect_service._resolve_ground_spell_control_after_cost_result(
            active_unit,
            skill_def,
            spent_mp,
            batch
        );
    }

    public GDictionary _resolve_unit_spell_control_after_cost(
        BattleUnitState active_unit,
        SkillDef skill_def,
        BattleEventBatch batch
    )
    {
        _ensure_sidecars_ready();
        return _ground_effect_service._resolve_unit_spell_control_after_cost(
            active_unit,
            skill_def,
            batch
        );
    }

    public BattleSpellControlResult _resolve_unit_spell_control_after_cost_result(
        BattleUnitState active_unit,
        SkillDef skill_def,
        BattleEventBatch batch
    )
    {
        _ensure_sidecars_ready();
        return _ground_effect_service._resolve_unit_spell_control_after_cost_result(
            active_unit,
            skill_def,
            batch
        );
    }

    public bool _apply_ground_precast_special_effects(
        BattleUnitState active_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        GVector2IArray target_coords,
        BattleEventBatch batch
    )
    {
        _ensure_sidecars_ready();
        return _ground_effect_service._apply_ground_precast_special_effects(
            active_unit,
            skill_def,
            cast_variant,
            ToUntypedArray(target_coords),
            batch
        );
    }

    public bool _apply_ground_jump_relocation(
        BattleUnitState active_unit,
        GVector2IArray target_coords,
        BattleEventBatch batch
    )
    {
        _ensure_sidecars_ready();
        return _ground_effect_service._apply_ground_jump_relocation(
            active_unit,
            ToUntypedArray(target_coords),
            batch
        );
    }

    public CombatEffectDef _get_ground_jump_effect_def(
        SkillDef skill_def,
        CombatCastVariantDef cast_variant
    )
    {
        _ensure_sidecars_ready();
        return _ground_effect_service._get_ground_jump_effect_def(skill_def, cast_variant)
            as CombatEffectDef;
    }

    public bool _is_ground_jump_effect(CombatEffectDef effect_def)
    {
        _ensure_sidecars_ready();
        return _ground_effect_service._is_ground_jump_effect(effect_def);
    }

    public StringName _get_effect_forced_move_mode(CombatEffectDef effect_def)
    {
        _ensure_sidecars_ready();
        return _ground_effect_service._get_effect_forced_move_mode(effect_def);
    }

    public GVector2IArray _build_ground_effect_coords(
        SkillDef skill_def,
        GArray target_coords,
        Vector2I source_coord = default,
        BattleUnitState active_unit = null,
        CombatCastVariantDef cast_variant = null
    )
    {
        _ensure_sidecars_ready();
        if (source_coord == default)
            source_coord = new Vector2I(-1, -1);
        return ToVector2IArray(
            _ground_effect_service._build_ground_effect_coords(
                skill_def,
                target_coords,
                source_coord,
                active_unit,
                cast_variant
            )
        );
    }

    public GCombatEffectArray _collect_ground_unit_effect_defs(
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        BattleUnitState active_unit = null
    )
    {
        _ensure_sidecars_ready();
        return _typed_combat_effect_defs(
            _ground_effect_service._collect_ground_unit_effect_defs(
                skill_def,
                cast_variant,
                active_unit
            )
        );
    }

    public GCombatEffectArray _collect_ground_terrain_effect_defs(
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        BattleUnitState active_unit = null
    )
    {
        _ensure_sidecars_ready();
        return _typed_combat_effect_defs(
            _ground_effect_service._collect_ground_terrain_effect_defs(
                skill_def,
                cast_variant,
                active_unit
            )
        );
    }

    public GCombatEffectArray _collect_ground_effect_defs(
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        BattleUnitState active_unit = null
    )
    {
        _ensure_sidecars_ready();
        return _typed_combat_effect_defs(
            _ground_effect_service._collect_ground_effect_defs(skill_def, cast_variant, active_unit)
        );
    }

    public GStringNameArray _collect_ground_preview_unit_ids(
        BattleUnitState source_unit,
        SkillDef skill_def,
        GCombatEffectArray effect_defs,
        GVector2IArray effect_coords
    )
    {
        _ensure_sidecars_ready();
        return _ground_effect_service._collect_ground_preview_unit_ids(
            source_unit,
            skill_def,
            ToUntypedArray(effect_defs),
            ToUntypedArray(effect_coords)
        );
    }

    public GDictionary _apply_ground_unit_effects(
        BattleUnitState source_unit,
        SkillDef skill_def,
        GCombatEffectArray effect_defs,
        GVector2IArray effect_coords,
        BattleEventBatch batch,
        GVector2IArray target_coords = null
    )
    {
        _ensure_sidecars_ready();
        return _ground_effect_service._apply_ground_unit_effects(
            source_unit,
            skill_def,
            ToUntypedArray(effect_defs),
            ToUntypedArray(effect_coords),
            batch,
            ToUntypedArray(target_coords ?? new GVector2IArray())
        );
    }

    public BattleGroundUnitEffectsResult _apply_ground_unit_effects_result(
        BattleUnitState source_unit,
        SkillDef skill_def,
        GCombatEffectArray effect_defs,
        GVector2IArray effect_coords,
        BattleEventBatch batch,
        GVector2IArray target_coords = null
    )
    {
        _ensure_sidecars_ready();
        return _ground_effect_service._apply_ground_unit_effects_result(
            source_unit,
            skill_def,
            ToUntypedArray(effect_defs),
            ToUntypedArray(effect_coords),
            batch,
            ToUntypedArray(target_coords ?? new GVector2IArray())
        );
    }

    public GDictionary _resolve_ground_unit_effect_result(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        GCombatEffectArray effect_defs
    )
    {
        _ensure_sidecars_ready();
        return _ground_effect_service._resolve_ground_unit_effect_result(
            source_unit,
            target_unit,
            skill_def,
            ToUntypedArray(effect_defs)
        );
    }

    public bool _should_resolve_ground_effects_as_attack(GCombatEffectArray effect_defs)
    {
        _ensure_sidecars_ready();
        return _ground_effect_service._should_resolve_ground_effects_as_attack(
            ToUntypedArray(effect_defs)
        );
    }

    public GCombatEffectArray _dedupe_effect_defs_by_instance(GCombatEffectArray effect_defs)
    {
        _ensure_sidecars_ready();
        return _typed_combat_effect_defs(
            _ground_effect_service._dedupe_effect_defs_by_instance(ToUntypedArray(effect_defs))
        );
    }

    public GDictionary _apply_ground_terrain_effects(
        BattleUnitState source_unit,
        SkillDef skill_def,
        GCombatEffectArray effect_defs,
        GVector2IArray effect_coords,
        BattleEventBatch batch
    )
    {
        _ensure_sidecars_ready();
        return _ground_effect_service._apply_ground_terrain_effects(
            source_unit,
            skill_def,
            ToUntypedArray(effect_defs),
            ToUntypedArray(effect_coords),
            batch
        );
    }

    public BattleGroundTerrainEffectsResult _apply_ground_terrain_effects_result(
        BattleUnitState source_unit,
        SkillDef skill_def,
        GCombatEffectArray effect_defs,
        GVector2IArray effect_coords,
        BattleEventBatch batch
    )
    {
        _ensure_sidecars_ready();
        return _ground_effect_service._apply_ground_terrain_effects_result(
            source_unit,
            skill_def,
            ToUntypedArray(effect_defs),
            ToUntypedArray(effect_coords),
            batch
        );
    }

    public bool _apply_ground_cell_effect(
        BattleUnitState source_unit,
        SkillDef skill_def,
        Vector2I target_coord,
        CombatEffectDef effect_def,
        BattleEventBatch batch
    )
    {
        _ensure_sidecars_ready();
        return _ground_effect_service._apply_ground_cell_effect(
            source_unit,
            skill_def,
            target_coord,
            effect_def,
            batch
        );
    }

    public bool _reconcile_water_topology(GVector2IArray effect_coords, BattleEventBatch batch)
    {
        _ensure_sidecars_ready();
        return _ground_effect_service._reconcile_water_topology(
            ToUntypedArray(effect_coords),
            batch
        );
    }

    public GBattleUnitArray _collect_units_in_coords(GVector2IArray effect_coords)
    {
        _ensure_sidecars_ready();
        return _typed_battle_units(_skill_orchestrator._collect_units_in_coords(effect_coords));
    }

    public GCombatEffectArray _typed_combat_effect_defs(GArray raw_values)
        => ToCombatEffectArray(raw_values);

    public GBattleUnitArray _typed_battle_units(GArray raw_values)
        => ToBattleUnitArray(raw_values);

    public bool _is_unit_effect(CombatEffectDef effect_def)
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._is_unit_effect(effect_def);
    }

    public GDictionary _apply_unit_shield_effects(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        GCombatEffectArray effect_defs,
        GDictionary shield_roll_context = null
    )
    {
        Dictionary<long, int> rollContext = BattleShieldService.ReadRollContext(
            shield_roll_context
        );
        BattleShieldApplyResult result = ApplyUnitShieldEffectsResult(
            source_unit,
            target_unit,
            skill_def,
            effect_defs,
            rollContext
        );
        BattleShieldService.WriteRollContext(shield_roll_context, rollContext);
        return result.ToDictionary();
    }

    public BattleShieldApplyResult ApplyUnitShieldEffectsResult(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        GCombatEffectArray effect_defs,
        Dictionary<long, int> shield_roll_context = null
    )
    {
        _ensure_sidecars_ready();
        return _shield_service.ApplyUnitShieldEffectsResult(
            source_unit,
            target_unit,
            skill_def,
            effect_defs ?? new GCombatEffectArray(),
            shield_roll_context ?? new Dictionary<long, int>()
        );
    }

    public GDictionary _apply_shield_effect_to_target(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        CombatEffectDef effect_def,
        GDictionary shield_roll_context = null
    )
    {
        Dictionary<long, int> rollContext = BattleShieldService.ReadRollContext(
            shield_roll_context
        );
        BattleShieldApplyResult result = ApplyShieldEffectToTargetResult(
            source_unit,
            target_unit,
            skill_def,
            effect_def,
            rollContext
        );
        BattleShieldService.WriteRollContext(shield_roll_context, rollContext);
        return result.ToDictionary();
    }

    public BattleShieldApplyResult ApplyShieldEffectToTargetResult(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        CombatEffectDef effect_def,
        Dictionary<long, int> shield_roll_context = null
    )
    {
        _ensure_sidecars_ready();
        return _shield_service.ApplyShieldEffectToTargetResult(
            source_unit,
            target_unit,
            skill_def,
            effect_def,
            shield_roll_context ?? new Dictionary<long, int>()
        );
    }

    public void _write_unit_shield(
        BattleUnitState target_unit,
        int shield_hp,
        int shield_duration,
        StringName shield_family,
        StringName shield_source_unit_id,
        StringName shield_source_skill_id
    )
    {
        _ensure_sidecars_ready();
        _shield_service._write_unit_shield(
            target_unit,
            shield_hp,
            shield_duration,
            shield_family,
            shield_source_unit_id,
            shield_source_skill_id
        );
    }

    public GDictionary _build_unit_shield_result(BattleUnitState target_unit, bool applied)
    {
        _ensure_sidecars_ready();
        return _shield_service._build_unit_shield_result(target_unit, applied);
    }

    public int _resolve_shield_hp(
        CombatEffectDef effect_def,
        GDictionary shield_roll_context = null
    )
    {
        _ensure_sidecars_ready();
        Dictionary<long, int> rollContext = BattleShieldService.ReadRollContext(
            shield_roll_context
        );
        int shieldHp = _shield_service.ResolveShieldHp(
            null,
            effect_def,
            rollContext
        );
        BattleShieldService.WriteRollContext(shield_roll_context, rollContext);
        return shieldHp;
    }

    public int _roll_shield_hp(CombatEffectDef effect_def)
    {
        _ensure_sidecars_ready();
        return _shield_service._roll_shield_hp(effect_def);
    }

    public bool _has_shield_dice_config(CombatEffectDef effect_def)
    {
        _ensure_sidecars_ready();
        return _shield_service._has_shield_dice_config(effect_def);
    }

    public int _get_shield_roll_cache_key(CombatEffectDef effect_def)
    {
        _ensure_sidecars_ready();
        return (int)_shield_service._get_shield_roll_cache_key(effect_def);
    }

    public int _roll_battle_effect_die(int dice_sides)
    {
        _ensure_sidecars_ready();
        return _shield_service._roll_battle_effect_die(dice_sides);
    }

    public int _resolve_shield_duration_tu(CombatEffectDef effect_def)
    {
        _ensure_sidecars_ready();
        return _shield_service._resolve_shield_duration_tu(effect_def);
    }

    public StringName _resolve_shield_family(SkillDef skill_def, CombatEffectDef effect_def)
    {
        _ensure_sidecars_ready();
        return _shield_service._resolve_shield_family(skill_def, effect_def);
    }

    public bool _is_terrain_effect(CombatEffectDef effect_def)
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._is_terrain_effect(effect_def);
    }

    public StringName _resolve_effect_target_filter(SkillDef skill_def, CombatEffectDef effect_def)
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._resolve_effect_target_filter(skill_def, effect_def);
    }

    public bool _is_unit_valid_for_effect(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        StringName target_team_filter
    )
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._is_unit_valid_for_effect(
            source_unit,
            target_unit,
            target_team_filter
        );
    }

    public StringName _build_terrain_effect_instance_id(StringName effect_id)
    {
        _ensure_sidecars_ready();
        return _ground_effect_service._build_terrain_effect_instance_id(effect_id);
    }

    public string _get_terrain_effect_display_name(CombatEffectDef effect_def)
    {
        _ensure_sidecars_ready();
        return _ground_effect_service._get_terrain_effect_display_name(effect_def);
    }

    public void _append_batch_log(BattleEventBatch batch, string message)
    {
        if (batch == null || string.IsNullOrEmpty(message))
            return;
        batch.log_lines.Add(message);
        _state?.append_log_entry(message);
    }

    public void _grant_skill_mastery_if_needed(
        BattleUnitState active_unit,
        SkillDef skill_def,
        BattleEventBatch batch
    )
    {
        if (skill_def == null)
            return;
        StringName skillId = skill_def.skill_id;
        _record_skill_success(active_unit, skillId);
        if (
            active_unit == null
            || IsEmpty(active_unit.source_member_id)
            || _characterGateway == null
        )
            return;
        _battle_rating_system.record_skill_success(active_unit, skillId);
        _characterGateway.record_achievement_event(
            active_unit.source_member_id,
            "skill_used",
            1,
            skillId,
            new GDictionary()
        );
        int masteryAmount = _skill_mastery_service.ResolveActiveSkillMasteryAmount();
        if (masteryAmount <= 0)
            return;
        StringName masterySkillId = _skill_mastery_service.ResolveMasteryRewardSkillId(
            active_unit,
            skillId
        );
        CharacterProgressionDelta delta = _characterGateway.grant_battle_mastery(
            active_unit.source_member_id,
            masterySkillId,
            masteryAmount
        );
        _append_progression_delta_to_batch(active_unit, delta, batch);
    }

    public void _apply_skill_mastery_grant(
        BattleUnitState unit_state,
        GDictionary grant,
        BattleEventBatch batch
    )
    {
        ApplySkillMasteryGrantTyped(
            unit_state,
            BattleSkillMasteryGrant.FromDictionary(grant),
            batch
        );
    }

    internal void ApplySkillMasteryGrantTyped(
        BattleUnitState unitState,
        BattleSkillMasteryGrant grant,
        BattleEventBatch batch
    )
    {
        if (grant?.IsValid != true || _characterGateway == null)
            return;
        if (grant.RecordNearDeathUnbrokenManual)
            _characterGateway.record_achievement_event(
                grant.MemberId,
                "near_death_unbroken_manual",
                1,
                "",
                new GDictionary()
            );
        CharacterProgressionDelta delta = _characterGateway.grant_skill_mastery_from_source(
            grant.MemberId,
            grant.SkillId,
            grant.Amount,
            grant.SourceType,
            grant.SourceLabel,
            grant.ReasonText,
            grant.AllowUnlocks
        );
        _append_progression_delta_to_batch(unitState, delta, batch);
    }

    public void _flush_last_stand_mastery_records(BattleEventBatch batch)
    {
        if (_damage_resolver == null)
            return;
        List<BattleSkillMasteryGrant> records =
            _damage_resolver.GetAndClearLastStandMasteryRecordsTyped();
        foreach (BattleSkillMasteryGrant record in records)
        {
            StringName memberId = record?.MemberId ?? "";
            BattleUnitState unitState = !IsEmpty(memberId)
                ? _find_unit_by_member_id(memberId)
                : null;
            ApplySkillMasteryGrantTyped(unitState, record, batch);
        }
    }

    public void _append_progression_delta_to_batch(
        BattleUnitState unit_state,
        CharacterProgressionDelta delta,
        BattleEventBatch batch
    )
    {
        if (unit_state == null || delta == null)
            return;
        if (_progression_delta_is_empty(delta))
            return;
        batch?.progression_deltas.Add(delta);
        _unit_factory.refresh_known_skills(unit_state);
        if (delta.needs_promotion_modal)
        {
            if (_state == null)
                return;
            _state.modal_state = "promotion_choice";
            if (_state.timeline != null)
                _state.timeline.frozen = true;
            if (batch != null)
            {
                batch.modal_requested = true;
                batch.log_lines.Add($"{unit_state.display_name} 触发职业晋升选择。");
            }
        }
    }

    public bool _progression_delta_is_empty(CharacterProgressionDelta delta)
    {
        if (delta == null)
            return true;
        return delta.mastery_changes.Count == 0
            && delta.leveled_skill_ids.Count == 0
            && delta.granted_skill_ids.Count == 0
            && delta.changed_profession_ids.Count == 0
            && delta.knowledge_changes.Count == 0
            && delta.attribute_changes.Count == 0
            && delta.unlocked_achievement_ids.Count == 0
            && !delta.needs_promotion_modal;
    }

    public CombatCastVariantDef _resolve_ground_cast_variant(
        SkillDef skill_def,
        BattleUnitState active_unit,
        BattleCommand command
    )
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._resolve_ground_cast_variant(skill_def, active_unit, command)
            as CombatCastVariantDef;
    }

    public CombatCastVariantDef _resolve_unit_cast_variant(
        SkillDef skill_def,
        BattleUnitState active_unit,
        BattleCommand command
    )
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._resolve_unit_cast_variant(skill_def, active_unit, command)
            as CombatCastVariantDef;
    }

    public StringName _get_cast_variant_target_mode(
        SkillDef skill_def,
        CombatCastVariantDef cast_variant
    )
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._get_cast_variant_target_mode(skill_def, cast_variant);
    }

    public CombatCastVariantDef _build_implicit_ground_cast_variant(SkillDef skill_def)
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._build_implicit_ground_cast_variant(skill_def);
    }

    public GDictionary _validate_ground_skill_command(
        BattleUnitState active_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        BattleCommand command
    )
    {
        _ensure_sidecars_ready();
        return _ground_effect_service._validate_ground_skill_command(
            active_unit,
            skill_def,
            cast_variant,
            command
        );
    }

    public BattleGroundSkillValidationResult _validate_ground_skill_command_result(
        BattleUnitState active_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        BattleCommand command
    )
    {
        _ensure_sidecars_ready();
        return _ground_effect_service._validate_ground_skill_command_result(
            active_unit,
            skill_def,
            cast_variant,
            command
        );
    }

    public string _get_ground_special_effect_validation_message(
        BattleUnitState active_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        GVector2IArray target_coords
    )
    {
        _ensure_sidecars_ready();
        return _ground_effect_service._get_ground_special_effect_validation_message(
            active_unit,
            skill_def,
            cast_variant,
            ToUntypedArray(target_coords)
        );
    }

    public bool _validate_target_coords_shape(
        StringName footprint_pattern,
        GVector2IArray target_coords
    )
    {
        _ensure_sidecars_ready();
        return _ground_effect_service._validate_target_coords_shape(
            footprint_pattern,
            target_coords
        );
    }

    public GVector2IArray _normalize_target_coords(BattleCommand command)
    {
        _ensure_sidecars_ready();
        return _ground_effect_service._normalize_target_coords(command);
    }

    public void _append_changed_coord(BattleEventBatch batch, Vector2I coord)
    {
        if (batch == null || batch.changed_coords.Contains(coord))
            return;
        batch.changed_coords.Add(coord);
    }

    public void _append_changed_coords(BattleEventBatch batch, GArray coords)
    {
        foreach (Vector2I coord in ToVector2IArray(coords))
        {
            _append_changed_coord(batch, coord);
        }
    }

    public void _append_changed_coords(BattleEventBatch batch, GVector2IArray coords)
    {
        if (coords == null)
            return;
        foreach (Vector2I coord in coords)
        {
            _append_changed_coord(batch, coord);
        }
    }

    public void _append_changed_unit_id(BattleEventBatch batch, StringName unit_id)
    {
        if (batch == null || IsEmpty(unit_id) || batch.changed_unit_ids.Contains(unit_id))
            return;
        batch.changed_unit_ids.Add(unit_id);
    }

    public void _append_changed_unit_coords(BattleEventBatch batch, BattleUnitState unit_state)
    {
        if (unit_state == null)
            return;
        unit_state.refresh_footprint();
        _append_changed_coords(batch, ToUntypedArray(unit_state.occupied_coords));
    }

    public void _collect_defeated_unit_loot(
        BattleUnitState unit_state,
        BattleUnitState killer_unit = null
    ) => _loot_resolver.CollectDefeatedUnitLoot(unit_state, killer_unit);

    public void handle_unit_defeated_by_runtime_effect(
        BattleUnitState unit_state,
        BattleUnitState source_unit,
        BattleEventBatch batch,
        string log_line = "",
        GDictionary options = null
    )
    {
        handle_unit_defeated_by_runtime_effect(
            unit_state,
            source_unit,
            batch,
            log_line,
            BattleDefeatHandlingOptions.FromDictionary(options)
        );
    }

    internal void handle_unit_defeated_by_runtime_effect(
        BattleUnitState unit_state,
        BattleUnitState source_unit,
        BattleEventBatch batch,
        string log_line,
        BattleDefeatHandlingOptions options
    )
    {
        if (unit_state == null)
            return;
        if (options.CollectLoot)
            _collect_defeated_unit_loot(unit_state, source_unit);
        _clear_defeated_unit(unit_state, batch);
        _record_unit_defeated(unit_state);
        if (options.RecordEnemyDefeatedAchievement)
            _battle_rating_system.record_enemy_defeated_achievement(source_unit, unit_state);
        if (!string.IsNullOrEmpty(log_line) && batch != null)
            batch.log_lines.Add(log_line);
        if (options.CheckBattleEnd)
            _check_battle_end(batch);
    }

    public void remove_summoned_unit_from_battle(
        BattleUnitState unit_state,
        BattleEventBatch batch,
        string log_line = ""
    )
    {
        if (_state == null || unit_state == null)
            return;
        GArray previousCoords = ToUntypedArray(unit_state.occupied_coords).Duplicate();
        unit_state.is_alive = false;
        _grid_service.clear_unit_occupancy(_state, unit_state);
        _append_changed_coords(batch, previousCoords);
        _append_changed_unit_id(batch, unit_state.unit_id);
        if (!string.IsNullOrEmpty(log_line) && batch != null)
            batch.log_lines.Add(log_line);
        _record_unit_defeated(unit_state);
        _check_battle_end(batch);
    }

    public void _clear_defeated_unit(BattleUnitState unit_state, BattleEventBatch batch = null)
    {
        if (_state == null || unit_state == null)
            return;
        _handle_adjacent_ally_defeat(unit_state);
        _handle_low_luck_relic_ally_defeat(unit_state, batch);
        GArray previousCoords = ToUntypedArray(unit_state.occupied_coords).Duplicate();
        _grid_service.clear_unit_occupancy(_state, unit_state);
        _append_changed_coords(batch, previousCoords);
        _append_changed_unit_id(batch, unit_state.unit_id);
    }

    public void _merge_batch(BattleEventBatch target_batch, BattleEventBatch source_batch)
    {
        if (target_batch == null || source_batch == null)
            return;
        foreach (Vector2I coord in source_batch.changed_coords)
            _append_changed_coord(target_batch, coord);
        foreach (StringName unitId in source_batch.changed_unit_ids)
            _append_changed_unit_id(target_batch, unitId);
        foreach (string logLine in source_batch.log_lines)
            target_batch.log_lines.Add(logLine);
        foreach (GDictionary reportEntry in ReadDictionaryItems(source_batch.report_entries))
        {
            target_batch.report_entries.Add(reportEntry.Duplicate(true));
        }
    }

    public GVector2IArray _sort_coords(GArray target_coords)
    {
        var sortedCoords = ToVector2IArray(target_coords);
        var coords = new List<Vector2I>();
        foreach (Vector2I coord in sortedCoords)
            coords.Add(coord);
        coords.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
        var result = new GVector2IArray();
        foreach (Vector2I coord in coords)
            result.Add(coord);
        return result;
    }

    public int _normalize_unit_action_threshold(int action_threshold)
    {
        _ensure_sidecars_ready();
        return _timeline_driver.NormalizeUnitActionThreshold(action_threshold);
    }

    public void _initialize_unit_action_thresholds()
    {
        _ensure_sidecars_ready();
        _timeline_driver.InitializeUnitActionThresholds();
    }

    public void _initialize_unit_trait_hooks()
    {
        _ensure_sidecars_ready();
        _timeline_driver.InitializeUnitTraitHooks();
    }

    public int _resolve_unit_action_threshold(BattleUnitState unit_state)
    {
        _ensure_sidecars_ready();
        return _timeline_driver.ResolveUnitActionThreshold(unit_state);
    }

    public int _resolve_timeline_tu_per_tick(GDictionary context)
    {
        _ensure_sidecars_ready();
        return _timeline_driver.ResolveTimelineTuPerTick(context);
    }

    public GVector2IArray _collect_dict_vector2i_keys(GDictionary values)
    {
        _ensure_sidecars_ready();
        return _movement_service._collect_dict_vector2i_keys(values);
    }

    public GArray _build_reachable_move_buckets(int max_move_points)
    {
        _ensure_sidecars_ready();
        return _movement_service._build_reachable_move_buckets(max_move_points);
    }

    public int _get_unit_skill_level(BattleUnitState unit_state, StringName skill_id)
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._get_unit_skill_level(unit_state, skill_id);
    }

    public string _format_skill_variant_label(SkillDef skill_def, CombatCastVariantDef cast_variant)
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._format_skill_variant_label(skill_def, cast_variant);
    }

    public bool _check_battle_end(BattleEventBatch batch)
    {
        _ensure_sidecars_ready();
        return _timeline_driver.CheckBattleEnd(batch);
    }

    public int _count_living_units(GStringNameArray unit_ids)
    {
        _ensure_sidecars_ready();
        return _timeline_driver.CountLivingUnits(unit_ids);
    }

    public void _end_active_turn(BattleEventBatch batch)
    {
        _ensure_sidecars_ready();
        _timeline_driver.EndActiveTurn(batch);
    }

    public void _handle_adjacent_ally_defeat(BattleUnitState defeated_unit)
    {
        _ensure_sidecars_ready();
        _special_skill_resolver._handle_adjacent_ally_defeat(defeated_unit);
    }

    public void _handle_low_luck_relic_ally_defeat(
        BattleUnitState defeated_unit,
        BattleEventBatch batch = null
    )
    {
        _ensure_sidecars_ready();
        _special_skill_resolver._handle_low_luck_relic_ally_defeat(defeated_unit, batch);
    }

    public GBattleUnitArray _collect_adjacent_living_allies(BattleUnitState defeated_unit)
    {
        _ensure_sidecars_ready();
        return _typed_battle_units(
            _special_skill_resolver._collect_adjacent_living_allies(defeated_unit)
        );
    }

    public bool _are_units_adjacent(BattleUnitState first_unit, BattleUnitState second_unit)
    {
        _ensure_sidecars_ready();
        return _special_skill_resolver._are_units_adjacent(first_unit, second_unit);
    }

    public void _activate_next_ready_unit(BattleEventBatch batch)
    {
        _ensure_sidecars_ready();
        _timeline_driver.ActivateNextReadyUnit(batch);
    }

    public string _get_skill_cast_block_reason(BattleUnitState active_unit, SkillDef skill_def) =>
        _skill_turn_resolver.get_skill_cast_block_reason(active_unit, skill_def);

    public bool _unit_has_melee_weapon(BattleUnitState active_unit) =>
        _skill_turn_resolver.unit_has_melee_weapon(active_unit);

    public bool _requires_melee_weapon(SkillDef skill_def) =>
        _skill_turn_resolver.requires_melee_weapon(skill_def);

    public bool _effect_uses_weapon_physical_damage_tag(CombatEffectDef effect_def) =>
        _skill_turn_resolver.effect_uses_weapon_physical_damage_tag(effect_def);

    public string _get_skill_command_block_reason(
        BattleUnitState active_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant
    ) => _skill_turn_resolver.get_skill_command_block_reason(active_unit, skill_def, cast_variant);

    public bool _consume_skill_costs(
        BattleUnitState active_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant = null,
        BattleEventBatch batch = null
    ) => _skill_turn_resolver.consume_skill_costs(active_unit, skill_def, cast_variant, batch);

    public GDictionary _get_effective_skill_costs(
        BattleUnitState active_unit,
        SkillDef skill_def
    ) => _skill_turn_resolver.get_effective_skill_costs(active_unit, skill_def);

    public CombatSkillResourceCosts _get_effective_skill_resource_costs(
        BattleUnitState active_unit,
        SkillDef skill_def
    ) => _skill_turn_resolver.get_effective_skill_resource_costs(active_unit, skill_def);

    public string _get_black_contract_push_variant_block_reason(
        BattleUnitState active_unit,
        CombatCastVariantDef cast_variant
    ) =>
        _skill_turn_resolver.get_black_contract_push_variant_block_reason(
            active_unit,
            cast_variant
        );

    public bool _consume_black_contract_push_cast(
        BattleUnitState active_unit,
        CombatCastVariantDef cast_variant,
        BattleEventBatch batch = null
    ) => _skill_turn_resolver.consume_black_contract_push_cast(active_unit, cast_variant, batch);

    public void _ensure_unit_turn_anchor(BattleUnitState unit_state) =>
        _skill_turn_resolver.ensure_unit_turn_anchor(unit_state);

    public bool _advance_unit_cooldowns(BattleUnitState unit_state, int cooldown_delta) =>
        _skill_turn_resolver.advance_unit_cooldowns(unit_state, cooldown_delta);

    public bool _consume_turn_cooldown_delta(BattleUnitState unit_state) =>
        _skill_turn_resolver.consume_turn_cooldown_delta(unit_state);

    public void _advance_unit_turn_timers(BattleUnitState unit_state, BattleEventBatch batch) =>
        _skill_turn_resolver.advance_unit_turn_timers(unit_state, batch);

    public GDictionary _apply_turn_start_statuses(
        BattleUnitState unit_state,
        BattleEventBatch batch
    ) => _skill_turn_resolver.apply_turn_start_statuses(unit_state, batch);

    public BattleStatusTickResult _apply_turn_start_statuses_result(
        BattleUnitState unit_state,
        BattleEventBatch batch
    ) => _skill_turn_resolver.ApplyTurnStartStatusesResult(unit_state, batch);

    public GDictionary _apply_unit_status_periodic_ticks(
        BattleUnitState unit_state,
        int elapsed_tu,
        BattleEventBatch batch
    ) => _skill_turn_resolver.apply_unit_status_periodic_ticks(unit_state, elapsed_tu, batch);

    public BattleStatusTickResult _apply_unit_status_periodic_ticks_result(
        BattleUnitState unit_state,
        int elapsed_tu,
        BattleEventBatch batch
    ) => _skill_turn_resolver.ApplyUnitStatusPeriodicTicksResult(unit_state, elapsed_tu, batch);

    public bool _advance_unit_status_durations(
        BattleUnitState unit_state,
        int elapsed_tu,
        BattleEventBatch batch = null
    ) => _skill_turn_resolver.advance_unit_status_durations(unit_state, elapsed_tu, batch);

    public int _get_effective_skill_range(BattleUnitState active_unit, SkillDef skill_def) =>
        _skill_turn_resolver.get_effective_skill_range(active_unit, skill_def);

    public int _resolve_base_skill_range(BattleUnitState active_unit, SkillDef skill_def) =>
        _skill_turn_resolver.resolve_base_skill_range(active_unit, skill_def);

    public bool _is_weapon_range_skill(SkillDef skill_def) =>
        _skill_turn_resolver.is_weapon_range_skill(skill_def);

    public int _get_weapon_attack_range(BattleUnitState active_unit) =>
        _skill_turn_resolver.get_weapon_attack_range(active_unit);

    public bool _skill_has_tag(SkillDef skill_def, StringName expected_tag) =>
        _skill_turn_resolver.skill_has_tag(skill_def, expected_tag);

    public bool _is_movement_blocked(BattleUnitState unit_state) =>
        _skill_turn_resolver.is_movement_blocked(unit_state);

    public bool _has_status(BattleUnitState unit_state, StringName status_id) =>
        _skill_turn_resolver.has_status(unit_state, status_id);

    public void _consume_status_if_present(
        BattleUnitState unit_state,
        StringName status_id,
        BattleEventBatch batch = null
    ) => _skill_turn_resolver.consume_status_if_present(unit_state, status_id, batch);

    public bool _is_main_skill_locked_by_status(BattleUnitState active_unit, SkillDef skill_def) =>
        _skill_turn_resolver.is_main_skill_locked_by_status(active_unit, skill_def);

    public int _count_debuff_statuses(BattleUnitState unit_state) =>
        _skill_turn_resolver.count_debuff_statuses(unit_state);

    public bool _status_counts_as_debuff(
        StringName status_id,
        BattleStatusEffectState status_entry
    ) => _skill_turn_resolver.status_counts_as_debuff(status_id, status_entry);

    public bool _has_counterattack_lock_status(BattleUnitState unit_state) =>
        _skill_turn_resolver.has_counterattack_lock_status(unit_state);

    public int _get_main_skill_lock_other_debuff_count(BattleUnitState unit_state) =>
        _skill_turn_resolver.get_main_skill_lock_other_debuff_count(unit_state);

    public void _prepare_ai_turn(BattleUnitState unit_state)
    {
        if (unit_state == null)
            return;
        unit_state.ai_blackboard.set_int(
            "turn_started_tu",
            _state?.timeline != null ? _state.timeline.current_tu : 0
        );
        unit_state.ai_blackboard.set_int("turn_decision_count", 0);
        EnemyAiBrainDef brain = GetEnemyAiBrainTyped(unit_state.ai_brain_id);
        if (brain != null && !brain.has_state(unit_state.ai_state_id))
            unit_state.ai_state_id = brain.default_state_id;
    }

    public void _cleanup_ai_turn(BattleUnitState unit_state)
    {
        if (unit_state == null)
            return;
        unit_state.ai_blackboard.Remove("turn_started_tu");
        unit_state.ai_blackboard.Remove("turn_decision_count");
        _skill_turn_resolver?.clear_turn_ai_override(unit_state);
    }

    public BattleUnitState _find_unit_by_member_id(StringName member_id)
    {
        if (_state == null)
            return null;
        foreach (BattleUnitState unitState in _state.GetUnitsTyped())
        {
            if (unitState != null && unitState.source_member_id == member_id)
                return unitState;
        }
        return null;
    }

    public void _sort_ready_unit_ids_by_action_priority()
    {
        _ensure_sidecars_ready();
        _timeline_driver.SortReadyUnitIdsByActionPriority();
    }

    public bool _is_left_ready_unit_higher_priority(
        StringName left_unit_id,
        StringName right_unit_id
    )
    {
        _ensure_sidecars_ready();
        return _timeline_driver.IsLeftReadyUnitHigherPriority(left_unit_id, right_unit_id);
    }

    public int _get_unit_turn_order_attribute(BattleUnitState unit_state, StringName attribute_id)
    {
        _ensure_sidecars_ready();
        return _timeline_driver._GetUnitTurnOrderAttribute(unit_state, attribute_id);
    }

    public int _get_unit_turn_order_action_points(BattleUnitState unit_state)
    {
        _ensure_sidecars_ready();
        return _timeline_driver._GetUnitTurnOrderActionPoints(unit_state);
    }

    public GStringNameArray _get_units_in_order()
    {
        _ensure_sidecars_ready();
        return _timeline_driver.GetUnitsInOrder();
    }

    public BattleEventBatch _new_batch() => new();

    public BattleResolutionResult _build_battle_resolution_result() =>
        _loot_resolver.BuildBattleResolutionResult();

    public AttackRollResult _roll_hit_rate(int hit_rate_percent) =>
        _hit_resolver.roll_hit_rate(_state, hit_rate_percent);

    private void BindDamageResolver()
    {
        if (_damage_resolver == null)
            return;
        if (_damage_resolver.HasMethod("set_skill_defs"))
            _damage_resolver.set_skill_defs(_skill_defs);
        if (_damage_resolver.HasMethod("set_hit_resolver"))
            _damage_resolver.set_hit_resolver(_hit_resolver);
    }

    private static bool IsEmpty(StringName value) => value == default || value == (StringName)"";

    private static GDictionary GetDict(GDictionary dict, string key)
    {
        if (dict == null || string.IsNullOrEmpty(key))
            return new GDictionary();
        if (dict.ContainsKey(key))
            return dict[key].AsGodotDictionary();
        StringName stringNameKey = new(key);
        return dict.ContainsKey(stringNameKey)
            ? dict[stringNameKey].AsGodotDictionary()
            : new GDictionary();
    }

    private static GArray GetArray(GDictionary dict, string key)
    {
        if (dict == null || string.IsNullOrEmpty(key))
            return new GArray();
        if (dict.ContainsKey(key))
            return dict[key].AsGodotArray();
        StringName stringNameKey = new(key);
        return dict.ContainsKey(stringNameKey)
            ? dict[stringNameKey].AsGodotArray()
            : new GArray();
    }

    private static string GetString(GDictionary dict, string key, string fallback = "")
    {
        if (dict == null || string.IsNullOrEmpty(key))
            return fallback;
        string text = "";
        if (dict.ContainsKey(key))
            text = dict[key].ToString();
        else
        {
            StringName stringNameKey = new(key);
            if (dict.ContainsKey(stringNameKey))
                text = dict[stringNameKey].ToString();
        }
        return string.IsNullOrEmpty(text) ? fallback : text;
    }

    private static StringName GetStringName(
        GDictionary dict,
        string key,
        StringName fallback = default
    )
    {
        string text = GetString(dict, key);
        StringName parsed = !string.IsNullOrEmpty(text) ? new StringName(text) : "";
        return IsEmpty(parsed) ? fallback : parsed;
    }

    private static int GetInt(GDictionary dict, string key, int fallback = 0)
    {
        if (dict == null || string.IsNullOrEmpty(key))
            return fallback;
        if (dict.ContainsKey(key))
            return dict[key].AsInt32();
        StringName stringNameKey = new(key);
        return dict.ContainsKey(stringNameKey)
            ? dict[stringNameKey].AsInt32()
            : fallback;
    }

    private static Vector2I GetVector2I(GDictionary dict, string key, Vector2I fallback)
    {
        if (dict == null || string.IsNullOrEmpty(key))
            return fallback;
        if (dict.ContainsKey(key))
            return dict[key].AsVector2I();
        StringName stringNameKey = new(key);
        return dict.ContainsKey(stringNameKey)
            ? dict[stringNameKey].AsVector2I()
            : fallback;
    }

    private static IEnumerable<GDictionary> ReadDictionaryItems(GArray values)
    {
        if (values == null)
            yield break;
        foreach (var value in values)
        {
            yield return value.AsGodotDictionary();
        }
    }

    private static GVector2IArray ToVector2IArray(GArray values)
    {
        var result = new GVector2IArray();
        if (values == null)
            return result;
        foreach (var value in values)
        {
            result.Add(value.AsVector2I());
        }
        return result;
    }

    private static GBattleUnitArray ToBattleUnitArray(GArray values)
    {
        var result = new GBattleUnitArray();
        if (values == null)
            return result;
        foreach (var value in values)
        {
            BattleUnitState unitState = value.As<BattleUnitState>();
            if (unitState != null)
                result.Add(unitState);
        }
        return result;
    }

    private static GCombatEffectArray ToCombatEffectArray(GArray values)
    {
        var result = new GCombatEffectArray();
        if (values == null)
            return result;
        foreach (var value in values)
        {
            CombatEffectDef effectDef = value.As<CombatEffectDef>();
            if (effectDef != null)
                result.Add(effectDef);
        }
        return result;
    }

    private static GArray ToUntypedArray(GVector2IArray values)
    {
        var result = new GArray();
        if (values == null)
            return result;
        foreach (Vector2I value in values)
            result.Add(value);
        return result;
    }

    private static GArray ToUntypedArray(GStringNameArray values)
    {
        var result = new GArray();
        if (values == null)
            return result;
        foreach (StringName value in values)
            result.Add(value);
        return result;
    }

    private static GArray ToUntypedArray(GCombatEffectArray values)
    {
        var result = new GArray();
        if (values == null)
            return result;
        foreach (CombatEffectDef value in values)
            result.Add(value);
        return result;
    }

    private static GArray ToUntypedArray(GBattleUnitArray values)
    {
        var result = new GArray();
        if (values == null)
            return result;
        foreach (BattleUnitState value in values)
            result.Add(value);
        return result;
    }
}
