using System;
using System.Collections.Generic;
using System.Linq;
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
        return source.ContainsKey(key) ? source[key].AsBool() : fallback;
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

}

public sealed class BattleStartFailureSnapshot
{
    public string Reason { get; init; } = "";
    public int AllyUnitCount { get; init; } = -1;
    public int EnemyUnitCount { get; init; } = -1;
    public int PlacementAttempt { get; init; } = -1;
    public long TerrainSeed { get; init; } = 0;
    public int AllySpawnCount { get; init; } = -1;
    public int EnemySpawnCount { get; init; } = -1;
    public int PlacementAttempts { get; init; } = -1;
    private GDictionary _reachabilityPayload = new();

    public bool IsEmpty =>
        string.IsNullOrEmpty(Reason)
        && AllyUnitCount < 0
        && EnemyUnitCount < 0
        && PlacementAttempt < 0
        && AllySpawnCount < 0
        && EnemySpawnCount < 0
        && PlacementAttempts < 0
        && _reachabilityPayload.Count == 0;

    internal static BattleStartFailureSnapshot FromDictionary(GDictionary source)
    {
        if (source == null || source.Count == 0)
            return new BattleStartFailureSnapshot();
        return new BattleStartFailureSnapshot
        {
            Reason = ReadString(source, "reason"),
            AllyUnitCount = ReadOptionalInt(source, "ally_unit_count"),
            EnemyUnitCount = ReadOptionalInt(source, "enemy_unit_count"),
            PlacementAttempt = ReadOptionalInt(source, "placement_attempt"),
            TerrainSeed = ReadOptionalLong(source, "terrain_seed", 0),
            AllySpawnCount = ReadOptionalInt(source, "ally_spawn_count"),
            EnemySpawnCount = ReadOptionalInt(source, "enemy_spawn_count"),
            PlacementAttempts = ReadOptionalInt(source, "placement_attempts"),
            _reachabilityPayload = ReadReachabilityPayload(source),
        };
    }

    internal GDictionary ToDictionary()
    {
        var result = new GDictionary();
        if (!string.IsNullOrEmpty(Reason))
            result["reason"] = Reason;
        if (AllyUnitCount >= 0)
            result["ally_unit_count"] = AllyUnitCount;
        if (EnemyUnitCount >= 0)
            result["enemy_unit_count"] = EnemyUnitCount;
        if (PlacementAttempt >= 0)
            result["placement_attempt"] = PlacementAttempt;
        if (TerrainSeed != 0)
            result["terrain_seed"] = TerrainSeed;
        if (AllySpawnCount >= 0)
            result["ally_spawn_count"] = AllySpawnCount;
        if (EnemySpawnCount >= 0)
            result["enemy_spawn_count"] = EnemySpawnCount;
        if (PlacementAttempts >= 0)
            result["placement_attempts"] = PlacementAttempts;
        if (_reachabilityPayload.Count > 0)
            result["reachability"] = _reachabilityPayload.Duplicate(true);
        return result;
    }

    private static int ReadOptionalInt(GDictionary source, string key, int missingValue = -1)
    {
        if (!source.ContainsKey(key))
            return missingValue;
        Variant value = source[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : missingValue;
    }

    private static long ReadOptionalLong(GDictionary source, string key, long missingValue = -1)
    {
        if (!source.ContainsKey(key))
            return missingValue;
        Variant value = source[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt64() : missingValue;
    }

    private static GDictionary ReadReachabilityPayload(GDictionary source)
    {
        if (!source.ContainsKey("reachability"))
            return new GDictionary();
        Variant value = source["reachability"];
        if (value.VariantType != Variant.Type.Dictionary)
            return new GDictionary();
        GDictionary reachability = value.AsGodotDictionary();
        return reachability.Count > 0 ? reachability.Duplicate(true) : new GDictionary();
    }

    private static string ReadString(GDictionary source, string key)
    {
        if (source == null || string.IsNullOrEmpty(key) || !source.ContainsKey(key))
            return "";
        return source[key].ToString();
    }
}

public partial class BattleRuntimeModule : RefCounted
{
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
    internal static readonly StringName SPAWN_SIDE_NEAR_LONG_EDGE_VALUE = "near_long_edge";
    internal static readonly StringName SPAWN_SIDE_FAR_LONG_EDGE_VALUE = "far_long_edge";

    private IBattleRuntimeCharacterGateway _characterGateway;
    private ISkillCatalog _skillCatalog;

    private readonly Dictionary<StringName, SkillDef> _skillDefIndex = new();
    private readonly Dictionary<StringName, EnemyTemplateDef> _enemyTemplateIndex = new();
    private readonly Dictionary<StringName, EnemyAiBrainDef> _enemyAiBrainIndex = new();
    private readonly Dictionary<StringName, ItemDef> _itemDefIndex = new();
    internal GDictionary _item_defs = new();
    internal GDictionary _enemy_templates = new();
    internal GDictionary _enemy_ai_brains = new();
    internal EncounterRosterBuilder _encounter_builder = new EncounterRosterBuilder();
    public BattleState _state;
    public BattleGridService _grid_service = new();
    private BattleTerrainGenerator _terrainGenerator;
    private bool _ownsTerrainGenerator;
    public BattleTerrainGenerator _terrain_generator
    {
        get => EnsureTerrainGenerator();
        set => SetTerrainGenerator(value, false);
    }
	internal BattleDamageResolver _damage_resolver = new();
	internal BattleHitResolver _hit_resolver = new();
    internal BattleAiService _ai_service = new();
    private readonly BattleAiActionAssembler _ai_action_assembler = new();
    internal BattleTerrainEffectSystem _terrain_effect_system = new();
    internal BattleRatingSystem _battle_rating_system = new();
    internal BattleUnitFactory _unit_factory = new();
    internal BattleChargeResolver _charge_resolver = new();
    internal BattleRepeatAttackResolver _repeat_attack_resolver = new();
    internal BattleMagicBacklashResolver _magic_backlash_resolver = new();
    internal BattleReportFormatter _report_formatter = new();
    internal BattleSkillResolutionRules _skill_resolution_rules = new();
    internal BattleSkillMasteryService _skill_mastery_service = new();
    internal BattleTerrainTopologyService _terrain_topology_service = new();
    internal BattleTargetCollectionService _target_collection_service = new();
    internal BattleSpawnReachabilityService _spawn_reachability_service = new();
    internal EquipmentDropService _equipment_drop_service = new();
    public Func<StringName> _equipment_instance_id_allocator;
    internal FateRuntimeModule _fate_runtime = new();
    internal BattleChangeEquipmentResolver _change_equipment_resolver = new();
    internal BattleRuntimeLootResolver _loot_resolver = new();
    internal BattleRuntimeSkillTurnResolver _skill_turn_resolver = new();
    internal BattleMetricsCollector _metrics_collector = new();
    internal BattleShieldService _shield_service = new();
    internal BattleGroundEffectService _ground_effect_service = new();
    internal BattleSpecialSkillResolver _special_skill_resolver = new();
    internal BattleMovementService _movement_service = new();
    internal BattleLayeredBarrierService _layered_barrier_service = new();
    internal BattleTimelineDriver _timeline_driver = new();
    internal BattleSkillExecutionOrchestrator _skill_orchestrator = new();
    internal BattleCastingTimeService _casting_time_service = new();
    internal TraitTriggerHooks _trait_trigger_hooks = new();
    internal GDictionary _special_profile_registry_snapshot = new();
	internal BattleSpecialProfileGate _special_profile_gate;
	internal BattleMeteorSwarmResolver _meteor_swarm_resolver;
    internal BattleAttackCheckPolicyService _attack_check_policy_service = new();
    internal BattleSkillOutcomeCommitter _skill_outcome_committer = new();
    private readonly Dictionary<StringName, BattleRatingMemberStats> _battleRatingStatsByMemberId = new();
    private readonly List<PendingCharacterReward> _pendingPostBattleCharacterRewards = new();
    internal List<BattleLootEntry> _active_loot_entries = new();
    internal GDictionary _looted_defeated_unit_ids = new();
    internal BattleResolutionResult _battle_resolution_result;
    public bool _battle_resolution_result_consumed;
    public int _terrain_effect_nonce;
    public bool _ai_trace_enabled;
    private readonly List<BattleAiTurnTraceProjection> _ai_turn_traces = new();
    internal Dictionary<StringName, BattleAiRuntimeActionPlan> _ai_action_plans_by_unit_id = new();
    private readonly BattleMovementQueryService _ai_movement_query_service = new();
    private readonly BattleAiScoreContextAdapter _ai_score_context_adapter = new();
    private readonly BattleAiQueryService _ai_query_service = new();
    private readonly BattleAiCandidateEvaluationService _ai_candidate_evaluation_service = new();
    private readonly BattleAiContext _ai_decision_context = new();
    private readonly Func<BattleUnitState, Vector2I, int> _ai_move_cost_callback;
    private readonly Func<BattleCommand, BattlePreview> _ai_preview_command_callback;
    private readonly Func<
        BattleAiContext,
        SkillDef,
        BattleCommand,
        BattlePreview,
        IReadOnlyList<CombatEffectDef>,
        IReadOnlyDictionary<string, object>,
        BattleAiScoreInput
    > _ai_skill_score_input_callback;
    private readonly Func<
        BattleAiContext,
        StringName,
        string,
        StringName,
        BattleCommand,
        BattlePreview,
        IReadOnlyDictionary<string, object>,
        BattleAiScoreInput
    > _ai_action_score_input_callback;
    private readonly Func<
        BattleAiQueryService,
        StringName,
        string,
        StringName,
        BattleCommand,
        BattlePreview,
        IReadOnlyDictionary<string, object>,
        BattleAiScoreInput
    > _ai_query_action_score_input_callback;
    private readonly Func<StringName, bool> _ai_movement_blocked_callback;
    private readonly Func<BattleUnitState, SkillDef, BattleSkillCastBlockReasonKind>
        _ai_skill_cast_block_reason_callback;
    internal BattleMetricsState _battle_metrics = new();
    internal GDictionary _last_start_failure = new();
    internal GDictionary calamity_by_member_id = new();
    private bool _disposed;

    public BattleRuntimeModule()
    {
        SetTerrainGenerator(new BattleTerrainGenerator(), true);
        _ai_move_cost_callback = _get_move_cost_for_unit_target;
        _ai_preview_command_callback = PreviewCommand;
        _ai_skill_score_input_callback = BuildAiSkillScoreInput;
        _ai_action_score_input_callback = BuildAiActionScoreInput;
        _ai_query_action_score_input_callback = BuildAiQueryActionScoreInput;
        _ai_movement_blocked_callback = IsAiMovementBlocked;
        _ai_skill_cast_block_reason_callback = GetSkillCastBlockReason;
    }

    public void setup(
        IBattleRuntimeCharacterGateway character_gateway = null,
        IReadOnlyDictionary<StringName, SkillDef> skill_defs = null,
        IReadOnlyDictionary<StringName, EnemyTemplateDef> enemy_templates = null,
        IReadOnlyDictionary<StringName, EnemyAiBrainDef> enemy_ai_brains = null,
        EncounterRosterBuilder encounter_builder = null,
        EquipmentDropService equipment_drop_service = null,
        IReadOnlyDictionary<StringName, ItemDef> item_defs = null,
        BattleTerrainGenerator terrain_generator = null,
        Func<StringName> equipment_instance_id_allocator = null,
        GDictionary battle_special_profile_registry_snapshot = null,
        ISkillCatalog skill_catalog = null
    )
    {
        _characterGateway = character_gateway;
        _skillCatalog = skill_catalog;
        ApplySkillDefsTyped(skill_defs);
        _special_profile_registry_snapshot =
            battle_special_profile_registry_snapshot?.Duplicate(true) ?? new GDictionary();
        BindDamageResolver();

        IReadOnlyDictionary<StringName, ItemDef> resolvedItemDefs = item_defs;
        if (
            (resolvedItemDefs == null || resolvedItemDefs.Count == 0)
            && _characterGateway != null
        )
        {
            resolvedItemDefs = _characterGateway.GetItemDefsTyped();
        }
        ApplyItemDefsTyped(resolvedItemDefs);

        ApplyEnemyTemplatesTyped(enemy_templates);
        ApplyEnemyAiBrainsTyped(enemy_ai_brains);
        FinishSetup(
            encounter_builder,
            equipment_drop_service,
            terrain_generator,
            equipment_instance_id_allocator
        );
    }

    private void FinishSetup(
        EncounterRosterBuilder encounter_builder,
        EquipmentDropService equipment_drop_service,
        BattleTerrainGenerator terrain_generator,
        Func<StringName> equipment_instance_id_allocator
    )
    {
        _encounter_builder = encounter_builder ?? new EncounterRosterBuilder();
        _equipment_drop_service = equipment_drop_service ?? new EquipmentDropService();
        _equipment_instance_id_allocator = equipment_instance_id_allocator;
        if (terrain_generator != null)
            SetTerrainGenerator(terrain_generator, false);

        _ai_action_plans_by_unit_id.Clear();
        _last_start_failure.Clear();
        _ai_service.Setup(_enemyAiBrainIndex, _damage_resolver);
        _terrain_effect_system.Setup(this);
        _attack_check_policy_service ??= new BattleAttackCheckPolicyService();
        _attack_check_policy_service.Setup(this, _hit_resolver, _terrain_effect_system);
        _skill_outcome_committer ??= new BattleSkillOutcomeCommitter();
        _skill_outcome_committer.Setup(this);
        _battle_rating_system.Setup(this, _skill_mastery_service);
         _unit_factory.Setup(this);
        _charge_resolver.Setup(this, _skill_mastery_service);
        _repeat_attack_resolver.Setup(this, _skill_mastery_service);
        _skill_mastery_service.Clear();
        _fate_runtime.Setup(
            _characterGateway,
            GetFateEventBus(),
            this,
            _find_unit_by_member_id
        );
        _change_equipment_resolver.Setup(this);
        _loot_resolver.Setup(this);
        _skill_turn_resolver.Setup(this);
        _metrics_collector.Setup(this);
        _shield_service.Setup(this);
        _ground_effect_service.Setup(this);
        _special_skill_resolver.Setup(this);
        _movement_service.Setup(this);
        _layered_barrier_service.Setup(this);
        _timeline_driver.Setup(this);
        _skill_orchestrator.Setup(this);
        _casting_time_service.Setup(this);
        _setup_special_profile_runtime();
    }

    internal void _setup_special_profile_runtime()
    {
        _special_profile_gate ??= new BattleSpecialProfileGate();
        _special_profile_gate.Setup(_special_profile_registry_snapshot);
        _skill_outcome_committer ??= new BattleSkillOutcomeCommitter();
        _skill_outcome_committer.Setup(this);

        _meteor_swarm_resolver = null;
        GDictionary profiles = GetDict(_special_profile_registry_snapshot, "profiles");
        GDictionary meteorProfileSnapshot = GetDict(profiles, "meteor_swarm");
        if (GetString(meteorProfileSnapshot, "runtime_resolver_id") != "meteor_swarm")
            return;
        _meteor_swarm_resolver = new BattleMeteorSwarmResolver();
        _meteor_swarm_resolver.Setup(this, _attack_check_policy_service);
    }

    public BattleState StartBattle(
        EncounterAnchorData encounter_anchor,
        long seed,
        GDictionary context = null
    )
    {
        context ??= new GDictionary();
        _last_start_failure.Clear();
        _ensure_sidecars_ready();
        var partyState =
            _characterGateway != null ? _characterGateway.GetPartyState() : null;
        GBattleUnitArray allyUnits = ToBattleUnitArray(
            _unit_factory.BuildAllyUnits(partyState, context)
        );
        if (allyUnits.Count == 0)
        {
            allyUnits = ToBattleUnitArray(_unit_factory.BuildAllyUnits(null, context));
        }

        GBattleUnitArray enemyUnits = new();
        _active_loot_entries.Clear();
        _looted_defeated_unit_ids.Clear();
        _ai_action_plans_by_unit_id.Clear();
        calamity_by_member_id.Clear();

        bool hasExplicitEnemyUnits =
            context.ContainsKey("enemy_units")
            && GetArray(context, "enemy_units").Count > 0;
        BattleStartOptions startOptions = BattleStartOptions.FromContext(
            context,
            !hasExplicitEnemyUnits
        );
        if (hasExplicitEnemyUnits)
        {
            enemyUnits = ToBattleUnitArray(
                _unit_factory.BuildEnemyUnits(encounter_anchor, context)
            );
        }
        else if (_encounter_builder != null)
        {
            enemyUnits = ToBattleUnitArray(
                _encounter_builder.BuildEnemyUnitsTyped(
                    encounter_anchor,
                    GetSkillDefIndexTyped(),
                    GetEnemyTemplateIndexTyped(),
                    GetEnemyAiBrainIndexTyped(),
                    GetItemDefIndexTyped()
                )
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
            long terrainSeed = seed + placementAttempt * BATTLE_START_TERRAIN_RETRY_SEED_STEP;
            GDictionary terrainData = _unit_factory.BuildTerrainData(
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
                timeline = new BattleTimelineState(),
            };
            _state.SetCellsFromDictionary(
                GetDict(terrainData, "cells"),
                duplicateCells: false,
                rebuildColumns: !terrainData.ContainsKey("cell_columns")
            );
            if (terrainData.ContainsKey("cell_columns"))
                _state.cell_columns = GetDict(terrainData, "cell_columns");
            _state.SetPartyBackpackView(_get_party_backpack_state(partyState) as WarehouseState);
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
                        _skillDefIndex,
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
            _state.PhaseKind = BattlePhaseKind.TimelineRunning;
            _state.active_unit_id = "";
            _state.winner_faction_id = "";
            _state.ModalStateKind = BattleModalStateKind.None;
            _state.attack_roll_nonce = 0;
            _state.ResetLogEntries(new GStringArray { $"战斗开始：{encounterDisplayName}" });
            _battle_rating_system.InitializeBattleRatingStats();
            _fate_runtime.BeginBattle(calamity_by_member_id);
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

    internal BattleStartFailureSnapshot GetLastStartFailureSnapshot() =>
        BattleStartFailureSnapshot.FromDictionary(_last_start_failure);

    internal bool _validate_battle_units_for_start(GArray units, string side_label) =>
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
                    .attribute_snapshot.HasValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass))
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

    internal void _build_ai_action_plans()
    {
        _ai_action_plans_by_unit_id.Clear();
        if (_state == null || _ai_action_assembler == null)
            return;
        foreach (BattleUnitState unitState in _state.GetUnitsTyped())
        {
            if (
                unitState == null
                || unitState.ControlModeKind == BattleUnitControlMode.Manual
                || IsEmpty(unitState.ai_brain_id)
            )
                continue;
            EnemyAiBrainDef brain = GetEnemyAiBrainTyped(unitState.ai_brain_id);
            if (brain == null)
                continue;
            BattleAiRuntimeActionPlan actionPlan = _ai_action_assembler.BuildUnitActionPlan(
                unitState,
                brain,
                GetSkillDefIndexTyped()
            );
            if (actionPlan != null)
                _ai_action_plans_by_unit_id[unitState.unit_id] = actionPlan;
        }
    }

    internal void _ensure_ai_action_plan_for_unit(BattleUnitState unit_state)
    {
        if (unit_state == null || _ai_action_assembler == null)
            return;
        if (_ai_action_plans_by_unit_id.ContainsKey(unit_state.unit_id))
            return;
        if (unit_state.ControlModeKind == BattleUnitControlMode.Manual || IsEmpty(unit_state.ai_brain_id))
            return;
        EnemyAiBrainDef brain = GetEnemyAiBrainTyped(unit_state.ai_brain_id);
        if (brain == null)
            return;
        BattleAiRuntimeActionPlan actionPlan = _ai_action_assembler.BuildUnitActionPlan(
            unit_state,
            brain,
            GetSkillDefIndexTyped()
        );
        if (actionPlan != null)
            _ai_action_plans_by_unit_id[unit_state.unit_id] = actionPlan;
    }

    internal void _bind_ai_helper_services_for_decision(
        BattleUnitState unit_state,
        BattleAiContext ai_context
    )
    {
        if (unit_state == null || ai_context == null || _state == null || _grid_service == null)
            return;
        ai_context.move_cost_callback = _ai_move_cost_callback;
        ai_context.preview_command_callback = _ai_preview_command_callback;
        ai_context.skill_score_input_callback = _ai_skill_score_input_callback;
        ai_context.action_score_input_callback = _ai_action_score_input_callback;
        ai_context.skill_cast_block_reason_callback = _ai_skill_cast_block_reason_callback;
        var movementQuery = _ai_movement_query_service;
        AiTraceRecorder.Enter("bind_ai_helpers:movement_query_setup");
        movementQuery.Setup(_state, _grid_service, _get_ai_move_query_cost);
        AiTraceRecorder.Exit("bind_ai_helpers:movement_query_setup");
        var scoreAdapter = _ai_score_context_adapter;
        AiTraceRecorder.Enter("bind_ai_helpers:score_adapter_setup");
        scoreAdapter.Setup(
            _ai_service.GetScoreService(),
            _state,
            unit_state,
            _grid_service,
            GetSkillDefIndexTyped(),
            _skillCatalog
        );
        AiTraceRecorder.Exit("bind_ai_helpers:score_adapter_setup");
        var query = _ai_query_service;
        AiTraceRecorder.Enter("bind_ai_helpers:query_setup");
        query.Setup(
            _state,
            _grid_service,
            unit_state.unit_id,
            GetSkillDefIndexTyped(),
            _ai_query_action_score_input_callback,
            movementQuery,
            _ai_movement_blocked_callback,
            _skillCatalog
        );
        AiTraceRecorder.Exit("bind_ai_helpers:query_setup");
        var candidateEvaluator = _ai_candidate_evaluation_service;
        AiTraceRecorder.Enter("bind_ai_helpers:candidate_setup");
        candidateEvaluator.Setup(_ai_service.GetScoreService());
        AiTraceRecorder.Exit("bind_ai_helpers:candidate_setup");
        ai_context.ai_query_service = query;
        ai_context.candidate_evaluator = candidateEvaluator;
    }

    private BattleAiContext _prepare_ai_context_for_decision(BattleUnitState activeUnit)
    {
        _ai_action_plans_by_unit_id.TryGetValue(
            activeUnit.unit_id,
            out BattleAiRuntimeActionPlan actionPlan
        );
        _ai_decision_context.ResetForDecision(
            _state,
            activeUnit,
            _grid_service,
            actionPlan,
            GetSkillDefIndexTyped(),
            _ai_trace_enabled,
            _skillCatalog
        );
        _ai_decision_context.move_cost_callback = _ai_move_cost_callback;
        _ai_decision_context.preview_command_callback = _ai_preview_command_callback;
        _ai_decision_context.skill_score_input_callback = _ai_skill_score_input_callback;
        _ai_decision_context.action_score_input_callback = _ai_action_score_input_callback;
        _ai_decision_context.skill_cast_block_reason_callback = _ai_skill_cast_block_reason_callback;
        return _ai_decision_context;
    }

    private BattleAiScoreInput BuildAiSkillScoreInput(
        BattleAiContext context,
        SkillDef skillDef,
        BattleCommand command,
        BattlePreview preview,
        IReadOnlyList<CombatEffectDef> effectDefs,
        IReadOnlyDictionary<string, object> metadata
    )
    {
        return _ai_service.GetScoreService()
            .BuildSkillScoreInput(
                context,
                skillDef,
                command,
                preview,
                effectDefs ?? System.Array.Empty<CombatEffectDef>(),
                metadata
            );
    }

    private BattleAiScoreInput BuildAiActionScoreInput(
        BattleAiContext context,
        StringName actionKind,
        string actionLabel,
        StringName scoreBucketId,
        BattleCommand command,
        BattlePreview preview,
        IReadOnlyDictionary<string, object> metadata
    )
    {
        return _ai_service.GetScoreService()
            .BuildActionScoreInput(
                context,
                actionKind,
                actionLabel,
                scoreBucketId,
                command,
                preview,
                metadata
            );
    }

    private BattleAiScoreInput BuildAiQueryActionScoreInput(
        BattleAiQueryService service,
        StringName actionKind,
        string actionLabel,
        StringName scoreBucketId,
        BattleCommand command,
        BattlePreview preview,
        IReadOnlyDictionary<string, object> metadata
    )
    {
        return _ai_score_context_adapter.BuildActionScoreInput(
            service,
            actionKind,
            actionLabel,
            scoreBucketId,
            command,
            preview,
            metadata
        );
    }

    private bool IsAiMovementBlocked(StringName unitId)
    {
        _state.TryGetUnitTyped(unitId, out BattleUnitState candidate);
        return candidate != null && _is_movement_blocked(candidate);
    }

    internal int _get_ai_move_query_cost(StringName unit_id, Vector2I _from_coord, Vector2I to_coord)
    {
        if (_state == null)
            return 1;
        _state.TryGetUnitTyped(unit_id, out BattleUnitState unitState);
        return unitState == null ? 1 : _get_move_cost_for_unit_target(unitState, to_coord);
    }

    internal StringName _resolve_formal_terrain_profile_id(GDictionary terrain_data)
    {
        if (terrain_data == null || !terrain_data.ContainsKey("terrain_profile_id"))
            return "";
        return GetStringName(terrain_data, "terrain_profile_id");
    }

    public BattleEventBatch advance(int tick_count)
    {
        _ensure_sidecars_ready();
        BattleEventBatch batch = _new_batch();
        if (_state == null || _state.PhaseKind == BattlePhaseKind.BattleEnded)
            return batch;
        if (_state.ModalStateKind != BattleModalStateKind.None)
            return batch;
        if (_state.timeline != null && _state.timeline.frozen)
            return batch;

        if (_state.PhaseKind == BattlePhaseKind.UnitActing)
        {
            _state.TryGetUnitTyped(_state.active_unit_id, out BattleUnitState activeUnit);
            if (activeUnit == null || !activeUnit.is_alive)
            {
                _end_active_turn(batch);
                return batch;
            }
            bool madnessAiControl = _skill_turn_resolver.IsTurnAiOverrideActive(activeUnit);
            if (
                activeUnit.is_alive
                && (activeUnit.ControlModeKind != BattleUnitControlMode.Manual || madnessAiControl)
            )
            {
                if (madnessAiControl && IsEmpty(activeUnit.ai_brain_id))
                {
                    var madnessCommand =
                        _skill_turn_resolver.BuildMadnessFallbackCommand(activeUnit);
                    if (madnessCommand != null)
                        return IssueCommand(madnessCommand);
                }
                AiTraceRecorder.Enter("advance:ensure_ai_action_plan");
                _ensure_ai_action_plan_for_unit(activeUnit);
                AiTraceRecorder.Exit("advance:ensure_ai_action_plan");

                AiTraceRecorder.Enter("advance:create_ai_context");
                BattleAiContext aiContext = _prepare_ai_context_for_decision(activeUnit);
                AiTraceRecorder.Exit("advance:create_ai_context");
                AiTraceRecorder.Enter("advance:bind_ai_helpers");
                _bind_ai_helper_services_for_decision(activeUnit, aiContext);
                AiTraceRecorder.Exit("advance:bind_ai_helpers");
                AiTraceRecorder.Enter("advance:choose_command");
                BattleAiDecision decision = _ai_service.ChooseCommand(aiContext);
                AiTraceRecorder.Exit("advance:choose_command");
                if (decision != null && decision.command != null)
                {
                    AiTraceRecorder.Enter("advance:ai_decision_commit");
                    string aiLine =
                        $"AI[{decision.brain_id}/{decision.state_id}/{decision.action_id}] {decision.reason_text}";
                    AiTraceRecorder.Enter("advance:append_ai_log");
                    _state.AppendLogEntry(aiLine);
                    AiTraceRecorder.Exit("advance:append_ai_log");
                    BattleAiTurnTraceProjection aiTurnTrace = null;
                    Dictionary<StringName, BattleAiTraceUnitSnapshotProjection> snapshotsBefore =
                        new();
                    List<StringName> decisionTargetUnitIds = new();
                    if (_ai_trace_enabled)
                    {
                        aiTurnTrace = aiContext.BuildTurnTraceTyped(decision);
                        decisionTargetUnitIds = CollectAiTraceDecisionTargetUnitIds(
                            decision,
                            aiTurnTrace
                        );
                        snapshotsBefore = BuildAiTraceUnitSnapshotMapTyped();
                        aiTurnTrace.DecisionTargetSnapshots = BuildAiTraceSnapshotsForUnitIdsTyped(
                            decisionTargetUnitIds,
                            snapshotsBefore
                        );
                    }
                    AiTraceRecorder.Exit("advance:ai_decision_commit");
                    AiTraceRecorder.Enter("advance:issue_ai_command");
                    BattleEventBatch decisionBatch;
                    try
                    {
                        decisionBatch = IssueCommand(decision.command);
                    }
                    finally
                    {
                        AiTraceRecorder.Exit("advance:issue_ai_command");
                    }
                    AiTraceRecorder.Enter("advance:ai_trace_after_command");
                    if (_ai_trace_enabled)
                    {
                        aiTurnTrace.ExecutionResult = BuildAiTraceExecutionResultTyped(
                            decision,
                            decisionBatch,
                            snapshotsBefore,
                            decisionTargetUnitIds
                        );
                        _ai_turn_traces.Add(aiTurnTrace);
                    }
                    AiTraceRecorder.Enter("advance:prepend_ai_batch_log");
                    if (decisionBatch != null)
                        decisionBatch.InsertLogLine(0, aiLine);
                    AiTraceRecorder.Exit("advance:prepend_ai_batch_log");
                    AiTraceRecorder.Exit("advance:ai_trace_after_command");
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

        if (_state.PhaseKind == BattlePhaseKind.TimelineRunning)
            _activate_next_ready_unit(batch);
        return batch;
    }

    internal bool _use_discrete_timeline_ticks()
    {
        _ensure_sidecars_ready();
        return _timeline_driver.UseDiscreteTimelineTicks();
    }

    internal void _apply_timeline_step(BattleEventBatch batch, int tu_delta)
    {
        _ensure_sidecars_ready();
        _timeline_driver.ApplyTimelineStep(batch, tu_delta);
    }

    internal void _resolve_timeline_status_phase(BattleEventBatch batch, int tu_delta)
    {
        _ensure_sidecars_ready();
        _timeline_driver.ResolveTimelineStatusPhase(batch, tu_delta);
    }

    internal void _collect_timeline_ready_units(BattleEventBatch batch, int tu_delta)
    {
        _ensure_sidecars_ready();
        _timeline_driver.CollectTimelineReadyUnits(batch, tu_delta);
    }

    internal bool _apply_stamina_recovery(BattleUnitState unit_state, int tu_delta)
    {
        _ensure_sidecars_ready();
        return _timeline_driver.ApplyStaminaRecovery(unit_state, tu_delta);
    }

    internal int _get_unit_constitution(BattleUnitState unit_state)
    {
        _ensure_sidecars_ready();
        return _timeline_driver.GetUnitConstitution(unit_state);
    }

    internal int _apply_stamina_recovery_percent_bonus(
        BattleUnitState unit_state,
        int base_progress_gain
    )
    {
        _ensure_sidecars_ready();
        return _timeline_driver.ApplyStaminaRecoveryPercentBonus(unit_state, base_progress_gain);
    }

    public BattlePreview PreviewCommand(BattleCommand command)
    {
        _ensure_sidecars_ready();
        var preview = new BattlePreview();
        if (!CanPreviewCommand(command))
            return preview;
        if (_state.ModalStateKind != BattleModalStateKind.None)
        {
            preview.AddLogLine(_get_battle_interaction_block_message());
            return preview;
        }
        if (command.IsCancelCast())
        {
            _casting_time_service.PreviewCancelCast(command, preview);
            return preview;
        }

        BattleUnitReadView activeUnit = ResolvePreviewActiveUnit(command);
        if (!activeUnit.IsValid || !activeUnit.IsAlive)
            return preview;

        if (command.IsMove())
            PreviewMoveCommand(activeUnit, command, preview);
        else if (command.IsSkill())
            PreviewSkillCommand(activeUnit, command, preview);
        else if (command.IsWait())
            PreviewWaitCommand(activeUnit, preview);
        else if (command.IsChangeEquipment())
            PreviewChangeEquipmentCommand(activeUnit, command, preview);
        else
            PreviewUnknownCommand(preview);
        return preview;
    }

    private bool CanPreviewCommand(BattleCommand command)
    {
        return _state != null && command != null && _state.PhaseKind != BattlePhaseKind.BattleEnded;
    }

    private BattleUnitReadView ResolvePreviewActiveUnit(BattleCommand command)
    {
        return command != null ? _state.AsReadView().GetUnit(command.unit_id) : default;
    }

    private void PreviewMoveCommand(
        BattleUnitReadView activeUnit,
        BattleCommand command,
        BattlePreview preview
    )
    {
        AiTraceRecorder.Enter("preview:move");
        if (_movement_service.IsMovementBlocked(activeUnit))
        {
            preview.AddLogLine($"{activeUnit.DisplayName} 当前被限制移动。");
            AiTraceRecorder.Exit("preview:move");
            return;
        }

        AiTraceRecorder.Enter("preview:move.resolve_path_result");
        BattleMovePathResult moveResult = _movement_service.ResolveMovePathResultTyped(
            activeUnit,
            command.target_coord
        );
        AiTraceRecorder.Exit("preview:move.resolve_path_result");

        AiTraceRecorder.Enter("preview:move.build_preview");
        ApplyMovePreviewResult(activeUnit, command, preview, moveResult);
        AiTraceRecorder.Exit("preview:move.build_preview");
        AiTraceRecorder.Exit("preview:move");
    }

    private void ApplyMovePreviewResult(
        BattleUnitReadView activeUnit,
        BattleCommand command,
        BattlePreview preview,
        BattleMovePathResult moveResult
    )
    {
        if (!moveResult.Allowed)
        {
            preview.AddLogLine(
                string.IsNullOrEmpty(moveResult.Message) ? "该移动不可执行。" : moveResult.Message
            );
            return;
        }

        preview.allowed = true;
        preview.move_cost = moveResult.Cost;
        preview.resolved_anchor_coord = command.target_coord;
        preview.AddLogLine(
            $"移动可执行，距离消耗 {moveResult.Cost} 点移动力，执行后锁定剩余移动力。"
        );
        AddPreviewFootprintCoords(preview, activeUnit, command.target_coord);
    }

    private void AddPreviewFootprintCoords(
        BattlePreview preview,
        BattleUnitReadView activeUnit,
        Vector2I anchorCoord
    )
    {
        foreach (Vector2I targetCoord in _grid_service.GetUnitTargetCoords(activeUnit, anchorCoord))
            preview.AddTargetCoord(targetCoord);
    }

    private void PreviewSkillCommand(
        BattleUnitReadView activeUnit,
        BattleCommand command,
        BattlePreview preview
    )
    {
        AiTraceRecorder.Enter("preview:skill");
        if (activeUnit.TurnCastingExhausted)
        {
            preview.AddLogLine("本次施法准备失败后只能移动、等待或取消读条。");
            AiTraceRecorder.Exit("preview:skill");
            return;
        }
        _preview_skill_command(activeUnit, command, preview);
        AiTraceRecorder.Exit("preview:skill");
    }

    private static void PreviewWaitCommand(BattleUnitReadView activeUnit, BattlePreview preview)
    {
        AiTraceRecorder.Enter("preview:wait");
        preview.allowed = true;
        preview.AddLogLine($"{activeUnit.DisplayName} 可以结束行动。");
        AiTraceRecorder.Exit("preview:wait");
    }

    private void PreviewChangeEquipmentCommand(
        BattleUnitReadView activeUnit,
        BattleCommand command,
        BattlePreview preview
    )
    {
        AiTraceRecorder.Enter("preview:change_equipment");
        if (activeUnit.TurnCastingExhausted)
        {
            preview.AddLogLine("本次施法准备失败后只能移动、等待或取消读条。");
            AiTraceRecorder.Exit("preview:change_equipment");
            return;
        }
        _preview_change_equipment_command(activeUnit, command, preview);
        AiTraceRecorder.Exit("preview:change_equipment");
    }

    private static void PreviewUnknownCommand(BattlePreview preview)
    {
        preview.AddLogLine("未知命令类型。");
    }

    public BattleEventBatch IssueCommand(BattleCommand command)
    {
        _ensure_sidecars_ready();
        var batch = _new_batch();
        if (_state == null || command == null)
            return batch;
        if (_state.PhaseKind == BattlePhaseKind.BattleEnded)
            return batch;
        if (_state.ModalStateKind != BattleModalStateKind.None)
        {
            batch.AddLogLine(_get_battle_interaction_block_message());
            return batch;
        }
        if (command.CommandKind == BattleCommandKind.CancelCast)
        {
            _casting_time_service.HandleCancelCast(command, batch);
            _append_batch_logs_to_state(batch);
            return batch;
        }
        if (_state.PhaseKind != BattlePhaseKind.UnitActing)
            return batch;

        _state.TryGetUnitTyped(_state.active_unit_id, out BattleUnitState activeUnit);
        if (activeUnit == null || !activeUnit.is_alive)
            return batch;
        if (activeUnit.unit_id != command.unit_id)
        {
            if (command.CommandKind == BattleCommandKind.ChangeEquipment)
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
            activeUnit.turn_casting_exhausted
            && (
                command.CommandKind == BattleCommandKind.Skill
                || command.CommandKind == BattleCommandKind.ChangeEquipment
            )
        )
        {
            batch.AddLogLine("本次施法准备失败后只能移动、等待或取消读条。");
            _append_batch_logs_to_state(batch);
            return batch;
        }
        if (
            command.CommandKind == BattleCommandKind.Skill
            && _should_block_skill_issue_from_preview(command, batch)
        )
        {
            _append_batch_logs_to_state(batch);
            return batch;
        }

        if (command.CommandKind == BattleCommandKind.Move)
            _handle_move_command(activeUnit, command, batch);
        else if (command.CommandKind == BattleCommandKind.Skill)
            _handle_skill_command(activeUnit, command, batch);
        else if (command.CommandKind == BattleCommandKind.Wait)
        {
            _record_action_issued(activeUnit, BattleTypedNames.ToStringName(BattleCommandKind.Wait));
            batch.AddLogLine($"{activeUnit.display_name} 结束行动。");
        }
        else if (command.CommandKind == BattleCommandKind.ChangeEquipment)
            _handle_change_equipment_command(activeUnit, command, batch);
        else
            return batch;

        _casting_time_service.ReconcilePendingCasts(batch);
        _append_batch_logs_to_state(batch);
        int flushedLogCount = batch.LogLinesTyped.Count;
        int flushedReportCount = batch.ReportEntriesTyped.Count;

        if (_state.ModalStateKind != BattleModalStateKind.None)
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
            || command.CommandKind == BattleCommandKind.Wait
        )
        {
            _end_active_turn(batch);
            _append_batch_logs_to_state_from(batch, flushedLogCount, flushedReportCount);
        }

        return batch;
    }

    internal string _get_battle_interaction_block_message()
    {
        if (_state == null)
            return "当前无法操作。";
        return _state.ModalStateKind switch
        {
            BattleModalStateKind.StartConfirm => "战斗尚未开始，确认后才能操作。",
            BattleModalStateKind.PromotionChoice => "当前处于晋升选择中，无法操作。",
            _ => "当前有待处理的战斗流程，暂时无法操作。",
        };
    }

    internal bool _should_block_skill_issue_from_preview(
        BattleCommand command,
        BattleEventBatch batch
    )
    {
        BattlePreview preview = PreviewCommand(command);
        if (preview != null && preview.allowed)
            return false;
        if (preview != null)
        {
            foreach (string logLine in preview.LogLinesTyped)
                batch.AddLogLine(logLine);
        }
        if (batch.LogLinesTyped.Count == 0)
            batch.AddLogLine("技能或目标无效。");
        return true;
    }

    internal void _append_batch_logs_to_state(BattleEventBatch batch) =>
        _append_batch_logs_to_state_from(batch);

    internal void _append_batch_logs_to_state_from(
        BattleEventBatch batch,
        int log_start_index = 0,
        int report_start_index = 0
    )
    {
        if (_state == null || batch == null)
            return;
        int safeLogStart = Math.Clamp(log_start_index, 0, batch.LogLinesTyped.Count);
        for (int i = safeLogStart; i < batch.LogLinesTyped.Count; i++)
            _state.AppendLogEntry(batch.LogLinesTyped[i]);
        int safeReportStart = Math.Clamp(report_start_index, 0, batch.ReportEntriesTyped.Count);
        for (int i = safeReportStart; i < batch.ReportEntriesTyped.Count; i++)
        {
            GDictionary reportEntry = batch.ReportEntriesTyped[i];
            if (reportEntry.Count > 0)
                _state.report_entries.Add(reportEntry.Duplicate(true));
        }
    }

    internal void _append_result_report_entry(BattleEventBatch batch, GDictionary result)
    {
        if (batch == null || result == null || result.Count == 0)
            return;
        GDictionary reportEntry = GetDict(result, "report_entry");
        if (reportEntry.Count > 0)
            _append_report_entry_to_batch(batch, reportEntry);
    }

    internal void AppendResultReportEntry(
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

    internal void _append_report_entry_to_batch(BattleEventBatch batch, GDictionary report_entry)
    {
        if (batch == null || report_entry == null || report_entry.Count == 0)
            return;
        batch.AddReportEntry(report_entry);
        string entryText = GetString(report_entry, "text").StripEdges();
        if (!string.IsNullOrEmpty(entryText))
            batch.AddLogLine(entryText);
    }

    public BattleEventBatch SubmitPromotionChoice(
        StringName member_id,
        StringName profession_id,
        GDictionary selection
    )
    {
        _ensure_sidecars_ready();
        BattleEventBatch batch = _new_batch();
        if (_state == null || _characterGateway == null)
            return batch;
        CharacterProgressionDelta delta = _characterGateway.PromoteProfession(
            member_id,
            profession_id,
            selection ?? new GDictionary()
        );
        if (!_promotion_delta_applied(delta, member_id, profession_id))
        {
            _keep_promotion_choice_modal_open(batch, "晋升提交无效，当前选择仍需确认。");
            return batch;
        }
        batch.AddProgressionDelta(delta);
        BattleUnitState unitState = _find_unit_by_member_id(member_id);
        if (unitState != null)
        {
            _unit_factory.RefreshBattleUnit(unitState);
            batch.AddChangedUnitId(unitState.unit_id);
            batch.AddLogLine($"{unitState.display_name} 完成职业晋升。");
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
            _state.ModalStateKind = BattleModalStateKind.None;
            if (_state.timeline != null)
                _state.timeline.frozen = false;
        }
        return batch;
    }

    internal void _keep_promotion_choice_modal_open(BattleEventBatch batch, string message = "")
    {
        if (_state == null)
            return;
        _state.ModalStateKind = BattleModalStateKind.PromotionChoice;
        if (_state.timeline != null)
            _state.timeline.frozen = true;
        if (batch != null)
        {
            batch.modal_requested = true;
            if (!string.IsNullOrEmpty(message))
                batch.AddLogLine(message);
        }
    }

    internal bool _promotion_delta_applied(
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
        return delta.HasChangedProfessionId(profession_id);
    }

    public BattleState GetState() => _state;

    internal IReadOnlyDictionary<StringName, int> GetCalamityByMemberIdSnapshot() =>
        _fate_runtime != null
            ? _fate_runtime.GetCalamityByMemberIdSnapshot()
            : BuildCalamityByMemberIdSnapshot(calamity_by_member_id);

    internal int GetMemberCalamity(StringName member_id) =>
        _fate_runtime != null ? _fate_runtime.GetMemberCalamity(member_id) : 0;

    internal int GetMemberCalamityCap(StringName member_id) =>
        _fate_runtime != null ? _fate_runtime.GetMemberCalamityCap(member_id) : 3;

    internal int GetBlackStarBrandCastCost(StringName member_id) =>
        _fate_runtime != null ? _fate_runtime.GetBlackStarBrandCastCost(member_id) : 1;

    internal bool HasMisfortuneReason(StringName member_id, StringName reason_id) =>
        _fate_runtime != null && _fate_runtime.HasMisfortuneReason(member_id, reason_id);

    internal FateRuntimeModule GetFateRuntime() => _fate_runtime;

    internal string GetMisfortuneSkillCastBlockReason(
        BattleUnitState active_unit,
        StringName skill_id
    ) =>
        _fate_runtime == null
            ? MisfortuneService.GetSkillSidecarMissingMessage(skill_id)
            : _fate_runtime.GetMisfortuneSkillCastBlockReason(active_unit, skill_id);

    private static Dictionary<StringName, int> BuildCalamityByMemberIdSnapshot(
        GDictionary source
    )
    {
        var result = new Dictionary<StringName, int>();
        if (source == null)
            return result;
        foreach (var entry in ProgressionDataUtils.to_string_name_int_dictionary(source))
        {
            var memberId = entry.Key;
            if (memberId == "")
                continue;
            int value = Mathf.Max(entry.Value, 0);
            if (value > 0)
                result[memberId] = value;
        }
        return result;
    }

    internal MisfortuneSkillCastResult ConsumeMisfortuneSkillCastResult(
        BattleUnitState active_unit,
        StringName skill_id
    ) =>
        _fate_runtime == null
            ? MisfortuneSkillCastResult.Failure(
                MisfortuneService.GetSkillSidecarMissingMessage(skill_id)
            )
            : _fate_runtime.ConsumeMisfortuneSkillCastResult(active_unit, skill_id);

    internal BattleSkillCastBlockReasonKind GetSkillCastBlockReason(
        BattleUnitState active_unit,
        SkillDef skill_def
    ) =>
        _get_skill_cast_block_reason(active_unit, skill_def);

    internal string GetSkillCastBlockMessage(BattleUnitState active_unit, SkillDef skill_def) =>
        _skill_turn_resolver.FormatSkillCastBlockReason(
            active_unit,
            skill_def,
            GetSkillCastBlockReason(active_unit, skill_def)
        );

    public bool IsUnitGuardLocked(BattleUnitState unit_state) =>
        _has_status(unit_state, STATUS_BLACK_STAR_BRAND_NORMAL);

    public bool IsUnitCounterattackLocked(BattleUnitState unit_state) =>
        _has_status(unit_state, STATUS_BLACK_STAR_BRAND_NORMAL)
        || _has_status(unit_state, STATUS_CROWN_BREAK_BROKEN_HAND)
        || _skill_turn_resolver.HasCounterattackLockStatus(unit_state);

    public bool IsUnitFollowUpLocked(BattleUnitState unit_state) =>
        _has_status(unit_state, STATUS_CROWN_BREAK_BROKEN_HAND);

    internal void _ensure_sidecars_ready()
    {
        _terrain_effect_system.Setup(this);
        _attack_check_policy_service ??= new BattleAttackCheckPolicyService();
        _attack_check_policy_service.Setup(this, _hit_resolver, _terrain_effect_system);
        _skill_outcome_committer ??= new BattleSkillOutcomeCommitter();
        _skill_outcome_committer.Setup(this);
        _battle_rating_system.Setup(this, _skill_mastery_service);
         _unit_factory.Setup(this);
        _charge_resolver.Setup(this, _skill_mastery_service);
        _repeat_attack_resolver.Setup(this, _skill_mastery_service);
        _change_equipment_resolver.Setup(this);
        _loot_resolver.Setup(this);
        _skill_turn_resolver.Setup(this);
        _metrics_collector.Setup(this);
        _shield_service.Setup(this);
        _ground_effect_service.Setup(this);
        _special_skill_resolver.Setup(this);
        _movement_service.Setup(this);
        _layered_barrier_service.Setup(this);
        _timeline_driver.Setup(this);
        _skill_orchestrator.Setup(this);
        _casting_time_service.Setup(this);
    }

    internal WarehouseState _get_party_backpack_state(PartyState party_state)
    {
        return party_state?.warehouse_state;
    }

    public bool IsBattleActive() =>
        _state != null && _state.PhaseKind != BattlePhaseKind.BattleEnded;

    internal IReadOnlyList<Vector2I> GetUnitReachableMoveCoordsTyped(BattleUnitState unit_state)
    {
        _ensure_sidecars_ready();
        return _movement_service.GetUnitReachableMoveCoords(unit_state);
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
                    _characterGateway.CommitBattleResources(
                        unitState.source_member_id,
                        unitState.current_hp,
                        unitState.current_mp,
                        unitState.current_aura
                    );
                else
                    _characterGateway.CommitBattleDeath(unitState.source_member_id);
            }
            _characterGateway.FlushAfterBattle();
        }
        if (
            _battle_resolution_result == null
            && !_battle_resolution_result_consumed
            && _state.PhaseKind == BattlePhaseKind.BattleEnded
        )
            _battle_resolution_result = _build_battle_resolution_result();
    }

    internal BattleResolutionResult GetBattleResolutionResult()
    {
        if (_battle_resolution_result_consumed)
            return null;
        if (
            _battle_resolution_result == null
            && _state != null
            && _state.PhaseKind == BattlePhaseKind.BattleEnded
        )
            _battle_resolution_result = _build_battle_resolution_result();
        return _battle_resolution_result;
    }

    internal BattleResolutionResult ConsumeBattleResolutionResult()
    {
        if (_battle_resolution_result_consumed)
            return null;
        BattleResolutionResult result = _battle_resolution_result;
        if (result == null && _state != null && _state.PhaseKind == BattlePhaseKind.BattleEnded)
            result = _build_battle_resolution_result();
        if (result != null)
        {
            _pendingPostBattleCharacterRewards.Clear();
            _active_loot_entries.Clear();
            _looted_defeated_unit_ids.Clear();
        }
        _battle_resolution_result = null;
        _battle_resolution_result_consumed = true;
        return result;
    }

    public BattleGridService GetGridService() => _grid_service;

    internal IBattleRuntimeCharacterGateway GetCharacterGatewayTyped() => _characterGateway;

    public StringName AllocateEquipmentInstanceId()
    {
        if (_equipment_instance_id_allocator == null)
            return "";
        return ProgressionDataUtils.to_string_name(_equipment_instance_id_allocator.Invoke());
    }

    public BattleDamageResolver GetDamageResolver() => _damage_resolver;

    public void ConfigureDamageResolverForTests(BattleDamageResolver damage_resolver)
    {
        _damage_resolver = damage_resolver ?? new BattleDamageResolver();
        BindDamageResolver();
        if (_ai_service != null)
            _ai_service.Setup(_enemyAiBrainIndex, _damage_resolver);
        if (_fate_runtime != null)
            _fate_runtime.Setup(
                _characterGateway,
                GetFateEventBus(),
                this,
                _find_unit_by_member_id
            );
        _change_equipment_resolver.Setup(this);
        _loot_resolver.Setup(this);
        _skill_turn_resolver.Setup(this);
        _metrics_collector.Setup(this);
        _shield_service.Setup(this);
        _ground_effect_service.Setup(this);
        _special_skill_resolver.Setup(this);
        _movement_service.Setup(this);
        _layered_barrier_service.Setup(this);
        _timeline_driver.Setup(this);
        _skill_orchestrator.Setup(this);
        _casting_time_service.Setup(this);
    }

    internal BattleFateEventBus GetFateEventBus() =>
        _damage_resolver != null ? _damage_resolver.GetFateEventBus() : null;

    public BattleHitResolver GetHitResolver() => _hit_resolver;

    internal BattleAttackCheckPolicyService GetAttackCheckPolicyService()
    {
        _attack_check_policy_service ??= new BattleAttackCheckPolicyService();
        _attack_check_policy_service.Setup(this, _hit_resolver, _terrain_effect_system);
        return _attack_check_policy_service;
    }

    public void ConfigureHitResolverForTests(BattleHitResolver hit_resolver)
    {
        _hit_resolver = hit_resolver ?? new BattleHitResolver();
        if (_damage_resolver != null)
            _damage_resolver.SetHitResolver(_hit_resolver);
        _attack_check_policy_service?.Setup(this, _hit_resolver, _terrain_effect_system);
        _meteor_swarm_resolver?.Setup(this, _attack_check_policy_service);
        _skill_outcome_committer?.Setup(this);
    }

    internal BattleTerrainGenerator GetTerrainGenerator() => EnsureTerrainGenerator();

    private BattleTerrainGenerator EnsureTerrainGenerator()
    {
        if (_disposed)
            return _terrainGenerator;
        if (_terrainGenerator == null || !GodotObject.IsInstanceValid(_terrainGenerator))
            SetTerrainGenerator(new BattleTerrainGenerator(), true);
        return _terrainGenerator;
    }

    private void SetTerrainGenerator(
        BattleTerrainGenerator terrainGenerator,
        bool ownsTerrainGenerator
    )
    {
        if (ReferenceEquals(_terrainGenerator, terrainGenerator))
            return;

        DisposeOwnedTerrainGenerator();
        _terrainGenerator = terrainGenerator;
        _ownsTerrainGenerator = ownsTerrainGenerator && terrainGenerator != null;
    }

    private void DisposeOwnedTerrainGenerator()
    {
        BattleTerrainGenerator terrainGenerator = _terrainGenerator;
        bool shouldDispose = _ownsTerrainGenerator;
        _terrainGenerator = null;
        _ownsTerrainGenerator = false;
        if (shouldDispose && terrainGenerator != null && GodotObject.IsInstanceValid(terrainGenerator))
            terrainGenerator.Dispose();
    }

    public SkillDef GetSkillDefTyped(StringName skill_id)
    {
        if (IsEmpty(skill_id))
        {
            return null;
        }
        return _skillDefIndex.TryGetValue(skill_id, out SkillDef skillDef) ? skillDef : null;
    }

    internal IReadOnlyDictionary<StringName, SkillDef> GetSkillDefIndexTyped() => _skillDefIndex;

    internal void SyncContentCatalogsTyped(
        IReadOnlyDictionary<StringName, SkillDef> skillDefs,
        IReadOnlyDictionary<StringName, ItemDef> itemDefs
    )
    {
        ApplySkillDefsTyped(skillDefs);
        ApplyItemDefsTyped(itemDefs);
    }

    internal IReadOnlyDictionary<StringName, EnemyTemplateDef> GetEnemyTemplateIndexTyped() =>
        _enemyTemplateIndex;

    internal EnemyTemplateDef GetEnemyTemplateTyped(StringName templateId)
    {
        if (IsEmpty(templateId))
        {
            return null;
        }
        return _enemyTemplateIndex.TryGetValue(templateId, out EnemyTemplateDef template)
            ? template
            : null;
    }

    internal void ReplaceEnemyTemplatesTyped(
        IReadOnlyDictionary<StringName, EnemyTemplateDef> enemyTemplates
    )
    {
        ApplyEnemyTemplatesTyped(enemyTemplates);
    }

    private void ApplySkillDefsTyped(IReadOnlyDictionary<StringName, SkillDef> skillDefs)
    {
        _ai_decision_context.ClearRuntimeBindings();
        _skillDefIndex.Clear();
        if (skillDefs == null || skillDefs.Count == 0)
        {
            return;
        }
        foreach ((StringName skillId, SkillDef skillDef) in skillDefs)
        {
            if (skillId == "" || skillDef == null || skillDef.skill_id == "")
            {
                continue;
            }
            _skillDefIndex[skillDef.skill_id] = skillDef;
        }
    }

    private void ApplyEnemyTemplatesTyped(
        IReadOnlyDictionary<StringName, EnemyTemplateDef> enemyTemplates
    )
    {
        _enemyTemplateIndex.Clear();
        if (enemyTemplates == null || enemyTemplates.Count == 0)
        {
            _enemy_templates = new GDictionary();
            return;
        }
        foreach ((StringName templateId, EnemyTemplateDef template) in enemyTemplates)
        {
            if (templateId == "" || template == null || template.template_id == "")
            {
                continue;
            }
            _enemyTemplateIndex[template.template_id] = template;
        }
        _enemy_templates = ProjectEnemyTemplateIndex(_enemyTemplateIndex);
    }

    private void ApplyEnemyAiBrainsTyped(
        IReadOnlyDictionary<StringName, EnemyAiBrainDef> enemyAiBrains
    )
    {
        _enemyAiBrainIndex.Clear();
        if (enemyAiBrains == null || enemyAiBrains.Count == 0)
        {
            _enemy_ai_brains = new GDictionary();
            return;
        }
        foreach ((StringName brainId, EnemyAiBrainDef brain) in enemyAiBrains)
        {
            if (brainId == "" || brain == null || brain.brain_id == "")
            {
                continue;
            }
            _enemyAiBrainIndex[brain.brain_id] = brain;
        }
        _enemy_ai_brains = ProjectEnemyAiBrainIndex(_enemyAiBrainIndex);
    }

    private void ApplyItemDefsTyped(IReadOnlyDictionary<StringName, ItemDef> itemDefs)
    {
        _itemDefIndex.Clear();
        if (itemDefs == null || itemDefs.Count == 0)
        {
            _item_defs = new GDictionary();
            return;
        }
        foreach ((StringName itemId, ItemDef itemDef) in itemDefs)
        {
            if (itemId == "" || itemDef == null || itemDef.item_id == "")
            {
                continue;
            }
            _itemDefIndex[itemDef.item_id] = itemDef;
        }
        _item_defs = ProjectItemDefIndex(_itemDefIndex);
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

    internal IReadOnlyDictionary<StringName, EnemyAiBrainDef> GetEnemyAiBrainIndexTyped() =>
        _enemyAiBrainIndex;

    internal IReadOnlyDictionary<StringName, ItemDef> GetItemDefIndexTyped() => _itemDefIndex;

    private static GDictionary ProjectEnemyTemplateIndex(
        IReadOnlyDictionary<StringName, EnemyTemplateDef> values
    )
    {
        var result = new GDictionary();
        if (values == null)
        {
            return result;
        }
        foreach ((StringName key, EnemyTemplateDef value) in values)
        {
            if (key == "" || value == null)
            {
                continue;
            }
            result[key] = value;
        }
        return result;
    }

    private static GDictionary ProjectEnemyAiBrainIndex(
        IReadOnlyDictionary<StringName, EnemyAiBrainDef> values
    )
    {
        var result = new GDictionary();
        if (values == null)
        {
            return result;
        }
        foreach ((StringName key, EnemyAiBrainDef value) in values)
        {
            if (key == "" || value == null)
            {
                continue;
            }
            result[key] = value;
        }
        return result;
    }

    private static GDictionary ProjectItemDefIndex(IReadOnlyDictionary<StringName, ItemDef> values)
    {
        var result = new GDictionary();
        if (values == null)
        {
            return result;
        }
        foreach ((StringName key, ItemDef value) in values)
        {
            if (key == "" || value == null)
            {
                continue;
            }
            result[key] = value;
        }
        return result;
    }

    internal bool _has_special_profile(SkillDef skill_def, StringName profile_id) =>
        skill_def?.combat_profile != null
        && skill_def.combat_profile.special_resolution_profile_id == profile_id;

    internal void _append_special_profile_gate_block(
        BattleEventBatch batch,
        BattleSpecialProfileGateResult gate_result
    )
    {
        if (batch == null)
            return;
        string message = "该禁咒配置未通过校验，暂时无法施放。";
        if (gate_result != null && !string.IsNullOrEmpty(gate_result.PlayerMessage))
            message = gate_result.PlayerMessage;
        if (string.IsNullOrEmpty(message))
            message = "该禁咒配置未通过校验，暂时无法施放。";
        batch.AddLogLine(message);
    }

    internal Dictionary<StringName, ItemDef> BuildItemDefIndexSnapshotTyped()
    {
        return new Dictionary<StringName, ItemDef>(_itemDefIndex);
    }

    internal int GetMinBattleSurfaceHeight() => MIN_BATTLE_SURFACE_HEIGHT;

    internal Dictionary<StringName, BattleRatingMemberStats> GetBattleRatingStatsTyped() =>
        _battleRatingStatsByMemberId;

    internal GDictionary get_battle_rating_stats()
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

    internal BattleRatingSystem GetBattleRatingSystem() => _battle_rating_system;

    internal List<PendingCharacterReward> GetPendingPostBattleCharacterRewards() =>
        _pendingPostBattleCharacterRewards;

    internal int GetDoomSentenceRefundCalamityTotal()
    {
        _ensure_sidecars_ready();
        return _loot_resolver?.GetDoomSentenceRefundCalamityTotal() ?? 0;
    }

    internal void SetAiTraceEnabled(bool enabled)
    {
        _ai_trace_enabled = enabled;
        if (!enabled)
            _ai_turn_traces.Clear();
    }

    internal Godot.Collections.Array<GDictionary> GetAiTurnTraces()
    {
        var result = new Godot.Collections.Array<GDictionary>();
        foreach (BattleAiTurnTraceProjection entry in _ai_turn_traces)
        {
            result.Add(TraceDictionaryProjection.ToDictionary(entry.ToTraceDictionary()));
        }
        return result;
    }

    internal IReadOnlyList<BattleAiTurnTraceProjection> GetAiTurnTracesTyped() => _ai_turn_traces;

    internal void ClearAiTurnTraces() => _ai_turn_traces.Clear();

    private List<StringName> CollectAiTraceDecisionTargetUnitIds(
        BattleAiDecision decision,
        BattleAiTurnTraceProjection turn_trace
    )
    {
        var unitIds = new List<StringName>();
        if (decision != null && decision.command != null)
        {
            _add_ai_trace_unit_id(unitIds, decision.command.target_unit_id);
            foreach (StringName unitId in decision.command.TargetUnitIdsTyped)
            {
                _add_ai_trace_unit_id(unitIds, unitId);
            }
        }
        _append_ai_trace_score_target_unit_ids(
            unitIds,
            turn_trace?.ScoreInput
        );
        return unitIds;
    }

    private void _append_ai_trace_score_target_unit_ids(
        List<StringName> unit_ids,
        BattleAiScoreInput score
    )
    {
        if (score == null)
            return;
        _add_ai_trace_unit_ids(unit_ids, score.target_unit_ids);
    }

    private void _add_ai_trace_unit_ids(
        List<StringName> unit_ids,
        IEnumerable<StringName> raw_unit_ids
    )
    {
        if (raw_unit_ids == null)
        {
            return;
        }
        foreach (StringName rawUnitId in raw_unit_ids)
            _add_ai_trace_unit_id(unit_ids, rawUnitId);
    }

    private void _add_ai_trace_unit_id(List<StringName> unit_ids, StringName raw_unit_id)
    {
        StringName unitId = ProgressionDataUtils.to_string_name(raw_unit_id);
        if (IsEmpty(unitId) || unit_ids.Contains(unitId))
            return;
        unit_ids.Add(unitId);
    }

    private Dictionary<StringName, BattleAiTraceUnitSnapshotProjection> BuildAiTraceUnitSnapshotMapTyped()
    {
        var snapshots = new Dictionary<StringName, BattleAiTraceUnitSnapshotProjection>();
        if (_state == null)
            return snapshots;
        foreach (BattleUnitState unitState in _state.GetUnitsTyped())
        {
            if (unitState == null)
                continue;
            StringName unitId = ProgressionDataUtils.to_string_name(unitState.unit_id);
            if (!IsEmpty(unitId))
            {
                snapshots[unitId] = BuildAiTraceUnitSnapshotTyped(unitState);
            }
        }
        return snapshots;
    }

    private BattleAiTraceUnitSnapshotProjection BuildAiTraceUnitSnapshotTyped(BattleUnitState unit_state)
    {
        if (unit_state == null)
            return new BattleAiTraceUnitSnapshotProjection();
        int hpMax = 0;
        int mpMax = 0;
        int staminaMax = 0;
        int auraMax = 0;
        if (unit_state.attribute_snapshot != null)
        {
            hpMax = unit_state
                .attribute_snapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.HpMax));
            mpMax = unit_state
                .attribute_snapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.MpMax));
            staminaMax = unit_state
                .attribute_snapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.StaminaMax));
            auraMax = unit_state
                .attribute_snapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.AuraMax));
        }
        return new BattleAiTraceUnitSnapshotProjection
        {
            UnitId = unit_state.unit_id.ToString(),
            DisplayName = unit_state.display_name,
            FactionId = unit_state.faction_id.ToString(),
            Coord = _format_ai_trace_coord(unit_state.coord),
            Alive = unit_state.is_alive,
            Hp = unit_state.current_hp,
            HpMax = Math.Max(hpMax, 1),
            Mp = unit_state.current_mp,
            MpMax = Math.Max(mpMax, 0),
            Stamina = unit_state.current_stamina,
            StaminaMax = Math.Max(staminaMax, 0),
            Aura = unit_state.current_aura,
            AuraMax = Math.Max(auraMax, 0),
            Ap = unit_state.current_ap,
            MovePoints = unit_state.current_move_points,
            ShieldHp = unit_state.current_shield_hp,
            ShieldMaxHp = unit_state.shield_max_hp,
        };
    }

    private List<BattleAiTraceUnitSnapshotProjection> BuildAiTraceSnapshotsForUnitIdsTyped(
        IEnumerable<StringName> unit_ids,
        IReadOnlyDictionary<StringName, BattleAiTraceUnitSnapshotProjection> snapshot_map
    )
    {
        var snapshots = new List<BattleAiTraceUnitSnapshotProjection>();
        if (unit_ids == null || snapshot_map == null)
        {
            return snapshots;
        }
        foreach (StringName rawUnitId in unit_ids)
        {
            StringName unitId = ProgressionDataUtils.to_string_name(rawUnitId);
            if (IsEmpty(unitId))
            {
                continue;
            }
            if (snapshot_map.TryGetValue(unitId, out BattleAiTraceUnitSnapshotProjection snapshot))
            {
                snapshots.Add(CloneAiTraceUnitSnapshot(snapshot));
            }
        }
        return snapshots;
    }

    private BattleAiTraceExecutionResultProjection BuildAiTraceExecutionResultTyped(
        BattleAiDecision decision,
        BattleEventBatch decision_batch,
        IReadOnlyDictionary<StringName, BattleAiTraceUnitSnapshotProjection> unit_snapshots_before,
        IEnumerable<StringName> decision_target_unit_ids
    )
    {
        Dictionary<StringName, BattleAiTraceUnitSnapshotProjection> unitSnapshotsAfter =
            BuildAiTraceUnitSnapshotMapTyped();
        var trackedUnitIds = new List<StringName>();
        foreach (StringName unitId in decision_target_unit_ids ?? Array.Empty<StringName>())
            _add_ai_trace_unit_id(trackedUnitIds, unitId);
        if (decision?.command != null)
            _add_ai_trace_unit_id(trackedUnitIds, decision.command.unit_id);
        if (decision_batch != null)
            _add_ai_trace_unit_ids(trackedUnitIds, decision_batch.ChangedUnitIdsTyped);
        BattleCommand command = decision?.command;
        return new BattleAiTraceExecutionResultProjection
        {
            CommandType = command != null ? command.command_type.ToString() : "",
            SkillId = command != null ? command.skill_id.ToString() : "",
            SkillVariantId = command != null ? command.skill_variant_id.ToString() : "",
            ChangedUnitIds = _ai_trace_stringify_unit_ids(
                decision_batch != null
                    ? decision_batch.ChangedUnitIdsTyped
                    : Array.Empty<StringName>()
            ),
            TrackedUnitIds = _ai_trace_stringify_unit_ids(trackedUnitIds),
            UnitResults = _build_ai_trace_unit_results(
                trackedUnitIds,
                unit_snapshots_before,
                unitSnapshotsAfter
            ),
            LogLines =
                decision_batch != null ? BuildStringArray(decision_batch.LogLinesTyped) : new List<string>(),
            ReportEntries =
                decision_batch != null
                    ? BuildReportEntriesProjection(decision_batch.ReportEntriesTyped)
                    : new List<Dictionary<string, object>>(),
        };
    }

    private List<BattleAiTraceUnitResultProjection> _build_ai_trace_unit_results(
        IEnumerable<StringName> unit_ids,
        IReadOnlyDictionary<StringName, BattleAiTraceUnitSnapshotProjection> unit_snapshots_before,
        IReadOnlyDictionary<StringName, BattleAiTraceUnitSnapshotProjection> unit_snapshots_after
    )
    {
        var results = new List<BattleAiTraceUnitResultProjection>();
        foreach (StringName rawUnitId in unit_ids ?? Array.Empty<StringName>())
        {
            StringName unitId = ProgressionDataUtils.to_string_name(rawUnitId);
            if (IsEmpty(unitId))
            {
                continue;
            }
            BattleAiTraceUnitSnapshotProjection before = null;
            BattleAiTraceUnitSnapshotProjection after = null;
            unit_snapshots_before?.TryGetValue(unitId, out before);
            unit_snapshots_after?.TryGetValue(unitId, out after);
            before ??= new BattleAiTraceUnitSnapshotProjection();
            after ??= new BattleAiTraceUnitSnapshotProjection();
            if (IsEmptySnapshot(before) && IsEmptySnapshot(after))
                continue;
            int hpBefore = before.Hp != 0 ? before.Hp : after.Hp;
            int hpAfter = after.Hp != 0 || before.Hp == 0 ? after.Hp : hpBefore;
            int shieldBefore = before.ShieldHp != 0 ? before.ShieldHp : after.ShieldHp;
            int shieldAfter = after.ShieldHp != 0 || before.ShieldHp == 0 ? after.ShieldHp : shieldBefore;
            bool beforeAlive = before.Alive;
            bool afterAlive = after.Alive || !beforeAlive ? after.Alive : beforeAlive;
            string coordBefore = !string.IsNullOrEmpty(before.Coord) ? before.Coord : after.Coord;
            string coordAfter = !string.IsNullOrEmpty(after.Coord) ? after.Coord : coordBefore;
            results.Add(
                new BattleAiTraceUnitResultProjection
                {
                    UnitId = unitId.ToString(),
                    Before = CloneAiTraceUnitSnapshot(before),
                    After = CloneAiTraceUnitSnapshot(after),
                    HpDelta = hpAfter - hpBefore,
                    HpDamage = Math.Max(hpBefore - hpAfter, 0),
                    HpHealing = Math.Max(hpAfter - hpBefore, 0),
                    ShieldDelta = shieldAfter - shieldBefore,
                    ShieldDamage = Math.Max(shieldBefore - shieldAfter, 0),
                    ShieldRestored = Math.Max(shieldAfter - shieldBefore, 0),
                    Killed = beforeAlive && !afterAlive,
                    Revived = !beforeAlive && afterAlive,
                    Moved = coordBefore != coordAfter,
                }
            );
        }
        return results;
    }

    private List<string> _ai_trace_stringify_unit_ids(IEnumerable<StringName> unit_ids)
    {
        var results = new List<string>();
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

    private static List<string> BuildStringArray(IEnumerable<string> values)
    {
        var results = new List<string>();
        if (values == null)
            return results;
        foreach (string value in values)
            results.Add(value ?? "");
        return results;
    }

    private static List<IReadOnlyDictionary<string, object>> BuildReportEntriesProjection(
        IEnumerable<GDictionary> reportEntries
    )
    {
        var result = new List<IReadOnlyDictionary<string, object>>();
        foreach (GDictionary reportEntry in reportEntries ?? Array.Empty<GDictionary>())
        {
            result.Add(TraceDictionaryProjection.FromDictionary(reportEntry));
        }
        return result;
    }

    private static bool IsEmptySnapshot(BattleAiTraceUnitSnapshotProjection snapshot)
    {
        return snapshot == null
            || string.IsNullOrEmpty(snapshot.UnitId)
                && string.IsNullOrEmpty(snapshot.DisplayName)
                && string.IsNullOrEmpty(snapshot.FactionId)
                && string.IsNullOrEmpty(snapshot.Coord)
                && snapshot.Hp == 0
                && snapshot.HpMax == 0
                && snapshot.Mp == 0
                && snapshot.MpMax == 0
                && snapshot.Stamina == 0
                && snapshot.StaminaMax == 0
                && snapshot.Aura == 0
                && snapshot.AuraMax == 0
                && snapshot.Ap == 0
                && snapshot.MovePoints == 0
                && snapshot.ShieldHp == 0
                && snapshot.ShieldMaxHp == 0
                && !snapshot.Alive;
    }

    private static BattleAiTraceUnitSnapshotProjection CloneAiTraceUnitSnapshot(
        BattleAiTraceUnitSnapshotProjection snapshot
    )
    {
        if (snapshot == null)
        {
            return new BattleAiTraceUnitSnapshotProjection();
        }
        return new BattleAiTraceUnitSnapshotProjection
        {
            UnitId = snapshot.UnitId,
            DisplayName = snapshot.DisplayName,
            FactionId = snapshot.FactionId,
            Coord = snapshot.Coord,
            Alive = snapshot.Alive,
            Hp = snapshot.Hp,
            HpMax = snapshot.HpMax,
            Mp = snapshot.Mp,
            MpMax = snapshot.MpMax,
            Stamina = snapshot.Stamina,
            StaminaMax = snapshot.StaminaMax,
            Aura = snapshot.Aura,
            AuraMax = snapshot.AuraMax,
            Ap = snapshot.Ap,
            MovePoints = snapshot.MovePoints,
            ShieldHp = snapshot.ShieldHp,
            ShieldMaxHp = snapshot.ShieldMaxHp,
        };
    }


    internal string _format_ai_trace_coord(Vector2I coord) => $"({coord.X}, {coord.Y})";

    internal BattleMetricsState GetBattleMetricsTyped() => _battle_metrics ?? new BattleMetricsState();

    internal void SetAiScoreProfile(BattleAiScoreProfile profile) =>
        _ai_service.SetScoreProfile(profile);

    internal void SetFactionAiScoreProfiles(
        System.Collections.Generic.IReadOnlyDictionary<StringName, BattleAiScoreProfile> profiles
    ) => _ai_service.SetFactionScoreProfiles(profiles);

    internal BattleAiScoreProfile GetAiScoreProfile() => _ai_service.GetScoreProfile();

    internal int GetTerrainEffectNonce() => _terrain_effect_nonce;

    internal int IncrementTerrainEffectNonce() => ++_terrain_effect_nonce;

    internal BattleEventBatch new_batch() => _new_batch();

    internal void MergeBatch(BattleEventBatch target_batch, BattleEventBatch source_batch) =>
        _merge_batch(target_batch, source_batch);

    internal void AppendChangedCoord(BattleEventBatch batch, Vector2I coord) =>
        _append_changed_coord(batch, coord);

    internal void AppendChangedCoords(BattleEventBatch batch, GVector2IArray coords) =>
        _append_changed_coords(batch, coords);

    internal void AppendChangedUnitId(BattleEventBatch batch, StringName unit_id) =>
        _append_changed_unit_id(batch, unit_id);

    internal void AppendChangedUnitCoords(BattleEventBatch batch, BattleUnitState unit_state) =>
        _append_changed_unit_coords(batch, unit_state);

    internal void AppendBatchLog(BattleEventBatch batch, string message) =>
        _append_batch_log(batch, message);

    internal void AppendResultReportEntry(BattleEventBatch batch, GDictionary result) =>
        _append_result_report_entry(batch, result);

    internal void AppendReportEntry(BattleEventBatch batch, GDictionary report_entry) =>
        _append_report_entry_to_batch(batch, report_entry);

    internal void ClearDefeatedUnit(BattleUnitState unit_state, BattleEventBatch batch = null) =>
        _clear_defeated_unit(unit_state, batch);

    internal GVector2IArray sort_coords(GArray target_coords) => _sort_coords(target_coords);

    internal GVector2IArray sort_coords(GVector2IArray target_coords) => _sort_coords(target_coords);

    internal string FormatSkillVariantLabel(
        SkillDef skill_def,
        CombatCastVariantDef cast_variant
    ) => _format_skill_variant_label(skill_def, cast_variant);

    internal void MarkAppliedStatusesForTurnTiming(
        BattleUnitState target_unit,
        GArray status_effect_ids
    )
    {
        _initialize_applied_status_timeline_ticks(target_unit, status_effect_ids);
        _fate_runtime?.HandleAppliedStatuses(target_unit, status_effect_ids ?? new GArray());
    }

    internal void MarkAppliedStatusesForTurnTiming(
        BattleUnitState target_unit,
        GStringNameArray status_effect_ids
    )
    {
        GStringNameArray normalizedStatusIds = NormalizeStatusIdArray(status_effect_ids);
        _initialize_applied_status_timeline_ticks(target_unit, normalizedStatusIds);
        _fate_runtime?.HandleAppliedStatuses(target_unit, normalizedStatusIds);
    }

    internal void _initialize_applied_status_timeline_ticks(
        BattleUnitState target_unit,
        GArray status_effect_ids
    )
    {
        _initialize_applied_status_timeline_ticks(
            target_unit,
            NormalizeStatusIdArray(status_effect_ids)
        );
    }

    internal void _initialize_applied_status_timeline_ticks(
        BattleUnitState target_unit,
        GStringNameArray status_effect_ids
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
            BattleStatusEffectState statusEntry = target_unit.GetStatusEffect(statusId);
            if (statusEntry == null || statusEntry.tick_interval_tu <= 0)
                continue;
            if (statusEntry.next_tick_at_tu <= currentTu)
            {
                statusEntry.next_tick_at_tu = currentTu + statusEntry.tick_interval_tu;
                target_unit.SetStatusEffect(statusEntry);
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

    internal StringName ResolveEffectTargetFilter(
        SkillDef skill_def,
        CombatEffectDef effect_def
    ) => _resolve_effect_target_filter(skill_def, effect_def);

    internal bool IsUnitValidForEffect(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        StringName target_filter
    ) => _is_unit_valid_for_effect(source_unit, target_unit, target_filter);

    internal bool is_unit_effect(CombatEffectDef effect_def) => _is_unit_effect(effect_def);

    internal int GetUnitSkillLevel(BattleUnitState unit_state, StringName skill_id) =>
        _get_unit_skill_level(unit_state, skill_id);

    internal void RecordEnemyDefeatedAchievement(
        BattleUnitState active_unit,
        BattleUnitState target_unit
    ) => _battle_rating_system.RecordEnemyDefeatedAchievement(active_unit, target_unit);

    internal void RecordSkillEffectResult(
        BattleUnitState source_unit,
        int damage,
        int healing,
        int kill_count
    )
    {
        _battle_rating_system.RecordSkillEffectResult(source_unit, damage, healing, kill_count);
        if (source_unit == null)
            return;
        _metrics_collector.RecordSkillEffectResult(source_unit, damage, healing, kill_count);
    }

    public void RecordBattleContributionResult(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        int damage,
        int healing,
        bool causedDefeat,
        StringName originKind,
        StringName skillId
    )
    {
        _battle_rating_system.RecordContributionFromUnits(
            source_unit,
            target_unit,
            damage,
            healing,
            causedDefeat,
            originKind,
            skillId
        );
    }

    internal void AppendResultSourceStatusEffects(
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
        MarkAppliedStatusesForTurnTiming(source_unit, sourceStatusIds);
        _append_changed_unit_id(batch, source_unit.unit_id);
        foreach (StringName statusId in sourceStatusIds)
            batch.AddLogLine($"{source_unit.display_name} 获得状态 {statusId}。");
    }

    internal void AppendResultSourceStatusEffects(
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
        MarkAppliedStatusesForTurnTiming(source_unit, sourceStatusIds);
        _append_changed_unit_id(batch, source_unit.unit_id);
        foreach (StringName statusId in sourceStatusIds)
            batch.AddLogLine($"{source_unit.display_name} 获得状态 {statusId}。");
    }

    internal void _initialize_battle_metrics()
    {
        _ensure_sidecars_ready();
        _metrics_collector.InitializeBattleMetrics();
    }

    internal void _record_turn_started(BattleUnitState unit_state)
    {
        _ensure_sidecars_ready();
        _metrics_collector.RecordTurnStarted(unit_state);
    }

    internal void _record_action_issued(
        BattleUnitState unit_state,
        StringName command_type,
        int ap_cost = 0
    )
    {
        _ensure_sidecars_ready();
        _metrics_collector.RecordActionIssued(unit_state, command_type, ap_cost);
    }

    internal void _record_skill_attempt(BattleUnitState unit_state, StringName skill_id)
    {
        _ensure_sidecars_ready();
        _metrics_collector.RecordSkillAttempt(unit_state, skill_id);
    }

    internal void _record_skill_success(BattleUnitState unit_state, StringName skill_id)
    {
        _ensure_sidecars_ready();
        _metrics_collector.RecordSkillSuccess(unit_state, skill_id);
    }

    internal void _record_effect_metrics(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        int damage,
        int healing,
        int kill_count
    )
    {
        _ensure_sidecars_ready();
        _metrics_collector.RecordEffectMetrics(
            source_unit,
            target_unit,
            damage,
            healing,
            kill_count
        );
    }

    internal void _record_unit_defeated(BattleUnitState unit_state)
    {
        _ensure_sidecars_ready();
        _metrics_collector.RecordUnitDefeated(unit_state);
    }

    public new void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public void dispose()
    {
        Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeManagedRuntime();
        }
        base.Dispose(disposing);
    }

    private void DisposeManagedRuntime()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        DisposeOwned(_terrain_effect_system, system => system.Dispose());
        DisposeOwned(_battle_rating_system, system => system.DisposeRuntime());
        DisposeOwned(_unit_factory, factory => factory.DisposeRuntime());
        DisposeOwned(_charge_resolver, resolver => resolver.DisposeRuntime());
        DisposeOwned(_repeat_attack_resolver, resolver => resolver.DisposeRuntime());
        _change_equipment_resolver?.Dispose();
        _loot_resolver?.Dispose();
        DisposeOwned(_skill_turn_resolver, resolver => resolver.DisposeRuntime());
        _metrics_collector?.Dispose();
        DisposeOwned(_shield_service, service => service.DisposeRuntime());
        _ground_effect_service?.Dispose();
        DisposeOwnedTerrainGenerator();
        _special_skill_resolver?.Dispose();
        _movement_service?.Dispose();
        _layered_barrier_service?.Dispose();
        _timeline_driver?.Dispose();
        DisposeOwned(_skill_orchestrator, orchestrator => orchestrator.DisposeRuntime());
        _casting_time_service?.Dispose();
        _meteor_swarm_resolver?.Dispose();
        _attack_check_policy_service?.Dispose();
        _skill_outcome_committer?.Dispose();
        _meteor_swarm_resolver = null;
        _special_profile_gate = null;
        _attack_check_policy_service = null;
        _skill_outcome_committer = null;
        _skill_mastery_service?.Clear();
        if (_skill_mastery_service != null && GodotObject.IsInstanceValid(_skill_mastery_service))
        {
            _skill_mastery_service.Dispose();
        }
        DisposeOwned(_fate_runtime, runtime => runtime.DisposeRuntime());
        _battleRatingStatsByMemberId.Clear();
        _pendingPostBattleCharacterRewards.Clear();
        _active_loot_entries.Clear();
        _looted_defeated_unit_ids.Clear();
        _ai_turn_traces.Clear();
        _ai_action_plans_by_unit_id.Clear();
        _ai_decision_context.ClearRuntimeBindings();
        _battle_metrics.Clear();
        calamity_by_member_id.Clear();
        _battle_resolution_result = null;
        _battle_resolution_result_consumed = false;
        _terrain_effect_nonce = 0;
        _ai_trace_enabled = false;
        _characterGateway = null;
        _skillDefIndex.Clear();
        _itemDefIndex.Clear();
        _enemyTemplateIndex.Clear();
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
            _state.ClearBattleTopology();
            _state.ally_unit_ids.Clear();
            _state.enemy_unit_ids.Clear();
            _state.timeline?.ready_unit_ids.Clear();
        }
        _state = null;
    }

    private static void DisposeOwned<T>(T owned, Action<T> cleanup)
        where T : GodotObject
    {
        if (owned == null || !GodotObject.IsInstanceValid(owned))
        {
            return;
        }

        GC.SuppressFinalize(owned);
        cleanup?.Invoke(owned);
        if (GodotObject.IsInstanceValid(owned))
        {
            owned.Dispose();
        }
    }

    internal bool _place_units(
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
            unitState.RefreshFootprint();
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

    internal bool _place_units(GArray units, GArray spawn_coords, bool is_ally) =>
        _place_units(units, spawn_coords, is_ally, "");

    internal void _clear_spawn_placed_units(GBattleUnitArray placed_units, bool is_ally)
    {
        if (_state == null)
            return;
        foreach (BattleUnitState unitState in placed_units)
        {
            if (unitState == null)
                continue;
            _grid_service.ClearUnitOccupancy(_state, unitState);
            _state.RemoveUnit(unitState.unit_id);
            if (is_ally)
                _state.ally_unit_ids.Remove(unitState.unit_id);
            else
                _state.enemy_unit_ids.Remove(unitState.unit_id);
        }
    }

    internal bool _place_spawn_unit_at_anchor(BattleUnitState unit_state, Vector2I coord)
    {
        if (_state == null || unit_state == null)
            return false;
        if (!_can_place_spawn_anchor(unit_state, coord))
            return false;
        unit_state.SetAnchorCoord(coord);
        _state.SetUnit(unit_state);
        _grid_service.SetOccupantsTyped(_state, unit_state.occupied_coords, unit_state.unit_id);
        return true;
    }

    internal Vector2I _find_spawn_anchor(
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

    internal Vector2I _find_spawn_anchor(
        BattleUnitState unit_state,
        GVector2IArray preferred_coords
    ) => _find_spawn_anchor(unit_state, preferred_coords, "");

    internal bool _can_place_spawn_anchor(
        BattleUnitState unit_state,
        Vector2I coord,
        StringName spawn_side = default
    )
    {
        if (_state == null || unit_state == null)
            return false;
        if (
            !_grid_service.CanPlaceFootprint(
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
        foreach (Vector2I footprintCoord in _grid_service.GetUnitTargetCoords(unit_state, coord))
        {
            BattleCellState cell = _grid_service.GetCellState(_state, footprintCoord);
            if (cell == null || BattleTerrainRules.IsWaterTerrain(cell.base_terrain))
                return false;
        }
        return true;
    }

    internal StringName _resolve_spawn_side_from_coords(GArray spawn_coords)
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

    internal StringName _get_opposite_spawn_side(StringName spawn_side)
    {
        if (spawn_side == SPAWN_SIDE_NEAR_LONG_EDGE_VALUE)
            return SPAWN_SIDE_FAR_LONG_EDGE_VALUE;
        if (spawn_side == SPAWN_SIDE_FAR_LONG_EDGE_VALUE)
            return SPAWN_SIDE_NEAR_LONG_EDGE_VALUE;
        return "";
    }

    internal bool _footprint_matches_spawn_side(
        BattleUnitState unit_state,
        Vector2I coord,
        StringName spawn_side
    )
    {
        if (_state == null || unit_state == null)
            return false;
        foreach (Vector2I footprintCoord in _grid_service.GetUnitTargetCoords(unit_state, coord))
        {
            if (!_coord_matches_spawn_side(footprintCoord, spawn_side))
                return false;
        }
        return true;
    }

    internal bool _coord_matches_spawn_side(Vector2I coord, StringName spawn_side)
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

    internal int _get_long_edge_side_axis_value(Vector2I coord) =>
        _state == null ? 0 : (_state.map_size.X >= _state.map_size.Y ? coord.Y : coord.X);

    internal int _get_long_edge_side_extent() =>
        _state == null
            ? 0
            : (_state.map_size.X >= _state.map_size.Y ? _state.map_size.Y : _state.map_size.X);

    internal int _score_spawn_anchor(BattleUnitState unit_state, Vector2I coord, int preferred_index)
    {
        int mobilityScore = _count_spawn_anchor_reachable_coords(unit_state, coord);
        int edgeClearance = _get_spawn_anchor_edge_clearance(unit_state, coord);
        int centerBias = _get_spawn_anchor_center_bias(unit_state, coord);
        return mobilityScore * 100 + edgeClearance * 18 + centerBias * 4 - preferred_index;
    }

    internal int _count_spawn_anchor_reachable_coords(
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
            foreach (Vector2I neighborCoord in _grid_service.GetNeighbors4(_state, currentCoord))
            {
                if (
                    !_grid_service.CanUnitStepBetweenAnchors(
                        _state,
                        unit_state,
                        currentCoord,
                        neighborCoord
                    )
                )
                    continue;
                int nextCost =
                    spentCost + _grid_service.GetUnitMoveCost(_state, unit_state, neighborCoord);
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

    internal int _get_spawn_anchor_edge_clearance(BattleUnitState unit_state, Vector2I coord)
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

    internal int _get_spawn_anchor_center_bias(BattleUnitState unit_state, Vector2I coord)
    {
        if (_state == null || unit_state == null)
            return 0;
        Vector2I footprint = unit_state.footprint_size;
        float centerX = (_state.map_size.X - footprint.X) * 0.5f;
        float centerY = (_state.map_size.Y - footprint.Y) * 0.5f;
        float distance = Mathf.Abs(coord.X - centerX) + Mathf.Abs(coord.Y - centerY);
        return -Mathf.RoundToInt(distance * 10.0f);
    }

    internal int _get_move_cost_for_unit_target(BattleUnitState unit_state, Vector2I target_coord)
    {
        _ensure_sidecars_ready();
        return _movement_service.GetMoveCostForUnitTarget(unit_state, target_coord);
    }

    internal int _get_move_path_cost(BattleUnitState unit_state, GVector2IArray anchor_path)
    {
        _ensure_sidecars_ready();
        return _movement_service.GetMovePathCost(unit_state, ToVector2IList(anchor_path));
    }

    internal int _get_status_move_cost_delta(BattleUnitState unit_state)
    {
        _ensure_sidecars_ready();
        return _movement_service.GetStatusMoveCostDelta(unit_state);
    }

    internal int _get_available_move_points(BattleUnitState unit_state)
    {
        _ensure_sidecars_ready();
        return _movement_service.GetAvailableMovePoints(unit_state);
    }

    internal bool _is_normal_movement_locked(BattleUnitState unit_state)
    {
        _ensure_sidecars_ready();
        return _movement_service.IsNormalMovementLocked(unit_state);
    }

    internal void _handle_move_command(
        BattleUnitState active_unit,
        BattleCommand command,
        BattleEventBatch batch
    )
    {
        _ensure_sidecars_ready();
        _movement_service.HandleMoveCommand(active_unit, command, batch);
    }

    internal void _preview_change_equipment_command(
        BattleUnitReadView active_unit,
        BattleCommand command,
        BattlePreview preview
    ) => _change_equipment_resolver.PreviewCommand(active_unit, command, preview);

    internal void _handle_change_equipment_command(
        BattleUnitState active_unit,
        BattleCommand command,
        BattleEventBatch batch
    ) => _change_equipment_resolver.HandleCommand(active_unit, command, batch);

    internal int _get_unit_hp_max(BattleUnitState unit_state) =>
        _change_equipment_resolver.GetUnitHpMax(unit_state);

    internal int _get_unit_stamina_max(BattleUnitState unit_state) =>
        _change_equipment_resolver.GetUnitStaminaMax(unit_state);

    internal bool _move_unit_along_validated_path(
        BattleUnitState active_unit,
        GVector2IArray anchor_path,
        Vector2I target_coord,
        BattleEventBatch batch
    )
    {
        _ensure_sidecars_ready();
        return _movement_service.MoveUnitAlongValidatedPathTyped(
            active_unit,
            ToVector2IList(anchor_path),
            target_coord,
            batch
        ).ReachedTarget;
    }

    internal void _handle_skill_command(
        BattleUnitState active_unit,
        BattleCommand command,
        BattleEventBatch batch
    )
    {
        _ensure_sidecars_ready();
        _skill_orchestrator._handle_skill_command(active_unit, command, batch);
    }

    internal void _preview_skill_command(
        BattleUnitReadView active_unit,
        BattleCommand command,
        BattlePreview preview
    )
    {
        _ensure_sidecars_ready();
        _skill_orchestrator._preview_skill_command(active_unit, command, preview);
    }

    internal void _preview_unit_skill_command(
        BattleUnitReadView active_unit,
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

    internal void _preview_ground_skill_command(
        BattleUnitReadView active_unit,
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

    internal AttackPreviewData _build_unit_skill_hit_preview(
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

    internal void AppendDamageResultLogLines(
        BattleEventBatch batch,
        string subject_label,
        string target_display_name,
        GDictionary result
    )
    {
        if (result == null || result.Count == 0)
        {
            return;
        }
        _ensure_sidecars_ready();
        AttackEffectResolutionResult typedResult =
            AttackEffectResolutionResultReader.ReadResolverResult(result, new AttackCheckInput());
        _skill_orchestrator.AppendDamageResultLogLines(
            batch,
            subject_label,
            target_display_name,
            typedResult
        );
    }

    internal void AppendDamageResultLogLines(
        BattleEventBatch batch,
        string subject_label,
        string target_display_name,
        AttackEffectResolutionResult result
    )
    {
        _ensure_sidecars_ready();
        _skill_orchestrator.AppendDamageResultLogLines(
            batch,
            subject_label,
            target_display_name,
            result
        );
    }

    internal string _build_skill_log_subject_label(
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

    internal bool _handle_unit_skill_command(
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

    internal bool _should_route_skill_command_to_unit_targeting(
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

    internal BattleUnitSkillValidationResult _validate_unit_skill_targets_result(
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

    internal GStringNameArray _normalize_target_unit_ids(BattleCommand command)
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._normalize_target_unit_ids(command, false);
    }

    internal GStringNameArray _sort_target_unit_ids_for_execution(GStringNameArray target_unit_ids)
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._sort_target_unit_ids_for_execution(target_unit_ids);
    }

    internal bool _is_multi_unit_skill(SkillDef skill_def)
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._is_multi_unit_skill(skill_def);
    }

    internal bool _can_skill_target_unit(
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

    internal bool _unit_stands_on_terrain_effect(
        BattleUnitState unit_state,
        StringName terrain_effect_id
    )
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._unit_stands_on_terrain_effect(unit_state, terrain_effect_id);
    }

    internal bool _is_unit_in_chain_radius(
        BattleUnitState primary_target,
        BattleUnitState candidate,
        int radius,
        CombatEffectDef chain_effect
    )
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._is_within_chain_radius(primary_target, candidate, radius);
    }

    internal bool _is_within_chain_radius(
        BattleUnitState primary_target,
        BattleUnitState candidate,
        int max_radius
    )
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._is_within_chain_radius(primary_target, candidate, max_radius);
    }

    internal bool _is_chain_height_valid(BattleUnitState from_unit, BattleUnitState to_unit)
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._is_chain_path_clear(from_unit, to_unit);
    }

    internal GVector2IArray _get_line_coords(Vector2I from, Vector2I to)
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._get_line_coords(from, to);
    }

    internal bool _is_chain_path_clear(BattleUnitState source_unit, BattleUnitState target_unit)
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._is_chain_path_clear(source_unit, target_unit);
    }

    internal void _apply_on_kill_gain_resources_effects(
        BattleUnitState source_unit,
        BattleUnitState defeated_unit,
        SkillDef skill_def,
        GCombatEffectArray effect_defs,
        BattleEventBatch batch
    )
    {
        _ensure_sidecars_ready();
        _special_skill_resolver.ApplyOnKillGainResourcesEffects(
            source_unit,
            defeated_unit,
            skill_def,
            effect_defs ?? new GCombatEffectArray(),
            batch
        );
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

    internal bool _swap_unit_positions(
        BattleUnitState first_unit,
        BattleUnitState second_unit,
        BattleEventBatch batch
    )
    {
        _ensure_sidecars_ready();
        return _special_skill_resolver.SwapUnitPositions(first_unit, second_unit, batch);
    }

    internal void _set_runtime_status_effect(
        BattleUnitState unit_state,
        StringName status_id,
        int duration_tu,
        StringName source_unit_id = default,
        int power = 1,
        GDictionary @params = null,
        StringName source_profile_id = default,
        StringName source_layer_id = default,
        StringName source_skill_id = default,
        int? source_skill_level = null,
        int self_save_dc = 0,
        StringName self_save_ability = default,
        StringName self_save_tag = default,
        int? self_save_roll_override = null,
        int save_bonus = 0,
        int control_save_bonus = 0,
        int range_bonus = 0,
        int passive_reduction = 0,
        int content_dr = 0,
        int guard_block = 0,
        IReadOnlyList<StringName> save_advantage_tags = null,
        IReadOnlyList<StringName> save_disadvantage_tags = null,
        IReadOnlyList<StringName> save_immunity_tags = null,
        IReadOnlyList<StringName> save_tags = null,
        int? heal_multiplier_percent = null,
        int? shield_gain_multiplier_percent = null,
        double? incoming_damage_multiplier = null,
        double? outgoing_damage_multiplier = null,
        StringName damage_tag = default,
        IReadOnlyList<StringName> damage_tags = null,
        StringName damage_category = default,
        int attack_roll_penalty = -1,
        bool undispellable = false,
        bool dispellable_magic = false,
        bool dispellable_harmful_magic = false,
        bool dispellable_beneficial_magic = false,
        StringName mitigation_tier = default,
        StringName dr_bypass_tag = default,
        bool counts_as_debuff_override = false,
        bool counts_as_debuff = false,
        bool forced_move_immune = false,
        bool lock_counterattack = false,
        bool lock_dodge_bonus = false,
        bool lock_crit = false,
        int main_skill_lock_other_debuff_count = 0,
        StringName stack_behavior = default,
        int stack_limit = 0,
        StringName body_size_category_override = default,
        StringName previous_body_size_category = default
    )
    {
        _ensure_sidecars_ready();
        _special_skill_resolver.SetRuntimeStatusEffect(
            unit_state,
            status_id,
            duration_tu,
            source_unit_id,
            power,
            @params,
            source_profile_id,
            source_layer_id,
            source_skill_id,
            source_skill_level,
            self_save_dc,
            self_save_ability,
            self_save_tag,
            self_save_roll_override,
            save_bonus,
            control_save_bonus,
            range_bonus,
            passive_reduction,
            content_dr,
            guard_block,
            save_advantage_tags,
            save_disadvantage_tags,
            save_immunity_tags,
            save_tags,
            heal_multiplier_percent,
            shield_gain_multiplier_percent,
            incoming_damage_multiplier,
            outgoing_damage_multiplier,
            damage_tag,
            damage_tags,
            damage_category,
            attack_roll_penalty,
            undispellable,
            dispellable_magic,
            dispellable_harmful_magic,
            dispellable_beneficial_magic,
            mitigation_tier,
            dr_bypass_tag,
            counts_as_debuff_override,
            counts_as_debuff,
            forced_move_immune,
            lock_counterattack,
            lock_dodge_bonus,
            lock_crit,
            main_skill_lock_other_debuff_count,
            stack_behavior,
            stack_limit,
            body_size_category_override,
            previous_body_size_category
        );
    }

    internal void _set_runtime_debuff_status_effect(
        BattleUnitState unit_state,
        StringName status_id,
        int duration_tu,
        StringName source_unit_id = default,
        int power = 1,
        GDictionary @params = null
    )
    {
        _ensure_sidecars_ready();
        _special_skill_resolver.SetRuntimeDebuffStatusEffect(
            unit_state,
            status_id,
            duration_tu,
            source_unit_id,
            power,
            @params
        );
    }

    internal void _set_runtime_body_size_override_status_effect(
        BattleUnitState unit_state,
        StringName status_id,
        int duration_tu,
        StringName source_unit_id = default,
        int power = 1,
        GDictionary @params = null,
        StringName body_size_category_override = default,
        StringName previous_body_size_category = default
    )
    {
        _ensure_sidecars_ready();
        _special_skill_resolver.SetRuntimeBodySizeOverrideStatusEffect(
            unit_state,
            status_id,
            duration_tu,
            source_unit_id,
            power,
            @params,
            body_size_category_override,
            previous_body_size_category
        );
    }

    internal void _set_runtime_source_status_effect(
        BattleUnitState unit_state,
        StringName status_id,
        int duration_tu,
        StringName source_unit_id,
        int power = 1,
        GDictionary @params = null
    )
    {
        _ensure_sidecars_ready();
        _special_skill_resolver.SetRuntimeSourceStatusEffect(
            unit_state,
            status_id,
            duration_tu,
            source_unit_id,
            power,
            @params
        );
    }

    internal void _set_runtime_barrier_status_effect(
        BattleUnitState unit_state,
        StringName status_id,
        StringName source_unit_id,
        StringName source_profile_id,
        StringName source_layer_id,
        int self_save_dc,
        StringName self_save_ability,
        StringName self_save_tag,
        int power = 1,
        GDictionary @params = null
    )
    {
        _ensure_sidecars_ready();
        _special_skill_resolver.SetRuntimeBarrierStatusEffect(
            unit_state,
            status_id,
            source_unit_id,
            source_profile_id,
            source_layer_id,
            self_save_dc,
            self_save_ability,
            self_save_tag,
            power,
            @params
        );
    }

    internal void _clear_black_star_brand_statuses(BattleUnitState unit_state)
    {
        _ensure_sidecars_ready();
        _special_skill_resolver.ClearBlackStarBrandStatuses(unit_state);
    }

    internal bool _is_black_star_brand_elite_target(BattleUnitState unit_state)
    {
        _ensure_sidecars_ready();
        return _special_skill_resolver.IsBlackStarBrandEliteTarget(unit_state);
    }

    internal bool _is_elite_or_boss_target(BattleUnitState unit_state)
    {
        _ensure_sidecars_ready();
        return _special_skill_resolver.IsEliteOrBossTarget(unit_state);
    }

    internal bool _is_boss_target(BattleUnitState unit_state)
    {
        _ensure_sidecars_ready();
        return _special_skill_resolver.IsBossTarget(unit_state);
    }

    internal bool _is_black_star_brand_skill(StringName skill_id)
    {
        _ensure_sidecars_ready();
        return _special_skill_resolver.IsBlackStarBrandSkill(skill_id);
    }

    internal bool _is_black_contract_push_skill(StringName skill_id)
    {
        _ensure_sidecars_ready();
        return _special_skill_resolver.IsBlackContractPushSkill(skill_id);
    }

    internal bool _is_doom_shift_skill(StringName skill_id)
    {
        _ensure_sidecars_ready();
        return _special_skill_resolver.IsDoomShiftSkill(skill_id);
    }

    internal bool _is_black_crown_seal_skill(StringName skill_id)
    {
        _ensure_sidecars_ready();
        return _special_skill_resolver.IsBlackCrownSealSkill(skill_id);
    }

    internal void _clear_crown_break_seal_statuses(BattleUnitState unit_state)
    {
        _ensure_sidecars_ready();
        _special_skill_resolver.ClearCrownBreakSealStatuses(unit_state);
    }

    internal bool _is_crown_break_target_eligible(
        BattleUnitState active_unit,
        BattleUnitState target_unit
    )
    {
        _ensure_sidecars_ready();
        return _special_skill_resolver.IsCrownBreakTargetEligible(active_unit, target_unit);
    }

    internal bool _is_crown_break_skill(StringName skill_id)
    {
        _ensure_sidecars_ready();
        return _special_skill_resolver.IsCrownBreakSkill(skill_id);
    }

    internal bool _is_doom_sentence_target_eligible(
        BattleUnitState active_unit,
        BattleUnitState target_unit
    )
    {
        _ensure_sidecars_ready();
        return _special_skill_resolver.IsDoomSentenceTargetEligible(active_unit, target_unit);
    }

    internal bool _is_black_crown_seal_target_eligible(
        BattleUnitState active_unit,
        BattleUnitState target_unit
    )
    {
        _ensure_sidecars_ready();
        return _special_skill_resolver.IsBlackCrownSealTargetEligible(
            active_unit,
            target_unit
        );
    }

    internal bool _is_doom_sentence_skill(StringName skill_id)
    {
        _ensure_sidecars_ready();
        return _special_skill_resolver.IsDoomSentenceSkill(skill_id);
    }

    internal string _get_unit_skill_target_validation_message(
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

    internal string _get_body_size_category_override_validation_message(
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

    internal bool _skill_grants_guarding(SkillDef skill_def)
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._skill_grants_guarding(skill_def);
    }

    internal int _apply_forced_move_effect(
        BattleUnitState source_unit,
        BattleUnitState unit_state,
        CombatEffectDef effect_def,
        BattleEventBatch batch,
        BattleForcedMoveContext forced_move_context = default
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

    internal bool _blocks_enemy_forced_move(BattleUnitState source_unit, BattleUnitState target_unit)
    {
        _ensure_sidecars_ready();
        return _special_skill_resolver.BlocksEnemyForcedMove(source_unit, target_unit);
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

    internal Vector2I _pick_forced_move_coord(
        BattleUnitState unit_state,
        BattleForcedMoveMode mode,
        BattleUnitState source_unit = null,
        BattleForcedMoveContext forced_move_context = default
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

    internal Vector2I PickForcedMoveCoord(
        BattleUnitState unit_state,
        BattleForcedMoveMode mode,
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

    internal int _score_forced_move_coord(
        BattleUnitState unit_state,
        Vector2I candidate_coord,
        BattleForcedMoveMode mode,
        BattleUnitState source_unit = null,
        BattleForcedMoveContext forced_move_context = default
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

    internal int ScoreForcedMoveCoord(
        BattleUnitState unit_state,
        Vector2I candidate_coord,
        BattleForcedMoveMode mode,
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

    internal bool _handle_ground_skill_command(
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

    internal BattleSpellControlResult _resolve_ground_spell_control_after_cost_result(
        BattleUnitState active_unit,
        SkillDef skill_def,
        int spent_mp,
        BattleEventBatch batch
    )
    {
        _ensure_sidecars_ready();
        return _ground_effect_service.ResolveGroundSpellControlAfterCostResult(
            active_unit,
            skill_def,
            spent_mp,
            batch
        );
    }

    internal BattleSpellControlResult _resolve_unit_spell_control_after_cost_result(
        BattleUnitState active_unit,
        SkillDef skill_def,
        BattleEventBatch batch
    )
    {
        _ensure_sidecars_ready();
        return _ground_effect_service.ResolveUnitSpellControlAfterCostResult(
            active_unit,
            skill_def,
            batch
        );
    }

    internal bool ApplyGroundPrecastSpecialEffectsTyped(
        BattleUnitState active_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        IReadOnlyList<Vector2I> target_coords,
        BattleEventBatch batch
    )
    {
        _ensure_sidecars_ready();
        return _ground_effect_service.ApplyGroundPrecastSpecialEffects(
            active_unit,
            skill_def,
            cast_variant,
            target_coords ?? Array.Empty<Vector2I>(),
            batch
        );
    }

    internal bool ApplyGroundJumpRelocationTyped(
        BattleUnitState active_unit,
        IReadOnlyList<Vector2I> target_coords,
        BattleEventBatch batch
    )
    {
        _ensure_sidecars_ready();
        return _ground_effect_service.ApplyGroundJumpRelocation(
            active_unit,
            target_coords ?? Array.Empty<Vector2I>(),
            batch
        );
    }

    internal CombatEffectDef _get_ground_jump_effect_def(
        SkillDef skill_def,
        CombatCastVariantDef cast_variant
    )
    {
        _ensure_sidecars_ready();
        return _ground_effect_service._get_ground_jump_effect_def(skill_def, cast_variant)
            as CombatEffectDef;
    }

    internal bool _is_ground_jump_effect(CombatEffectDef effect_def)
    {
        _ensure_sidecars_ready();
        return _ground_effect_service._is_ground_jump_effect(effect_def);
    }

    internal IReadOnlyList<Vector2I> BuildGroundEffectCoordsTyped(
        SkillDef skill_def,
        IReadOnlyList<Vector2I> target_coords,
        Vector2I source_coord = default,
        BattleUnitState active_unit = null,
        CombatCastVariantDef cast_variant = null
    )
    {
        _ensure_sidecars_ready();
        if (source_coord == default)
            source_coord = new Vector2I(-1, -1);
        return _ground_effect_service.BuildGroundEffectCoords(
            skill_def,
            target_coords ?? Array.Empty<Vector2I>(),
            source_coord,
            active_unit,
            cast_variant
        );
    }

    internal IReadOnlyList<Vector2I> BuildGroundEffectCoordsTyped(
        SkillDef skill_def,
        IReadOnlyList<Vector2I> target_coords,
        Vector2I source_coord,
        BattleUnitReadView active_unit,
        CombatCastVariantDef cast_variant = null
    )
    {
        _ensure_sidecars_ready();
        if (source_coord == default)
            source_coord = new Vector2I(-1, -1);
        return _ground_effect_service.BuildGroundEffectCoords(
            skill_def,
            target_coords ?? Array.Empty<Vector2I>(),
            source_coord,
            active_unit,
            cast_variant
        );
    }

    internal IReadOnlyList<CombatEffectDef> CollectGroundUnitEffectDefsTyped(
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        BattleUnitState active_unit = null
    )
    {
        _ensure_sidecars_ready();
        return _ground_effect_service.CollectGroundUnitEffectDefs(
            skill_def,
            cast_variant,
            active_unit
        );
    }

    internal IReadOnlyList<CombatEffectDef> CollectGroundUnitEffectDefsTyped(
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        BattleUnitReadView active_unit
    )
    {
        _ensure_sidecars_ready();
        return _skill_resolution_rules != null
            ? _skill_resolution_rules.CollectGroundUnitEffectDefs(
                skill_def,
                cast_variant,
                active_unit
            )
            : Array.Empty<CombatEffectDef>();
    }

    internal IReadOnlyList<CombatEffectDef> CollectGroundTerrainEffectDefsTyped(
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        BattleUnitState active_unit = null
    )
    {
        _ensure_sidecars_ready();
        return _ground_effect_service.CollectGroundTerrainEffectDefs(
            skill_def,
            cast_variant,
            active_unit
        );
    }

    internal IReadOnlyList<CombatEffectDef> CollectGroundTerrainEffectDefsTyped(
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        BattleUnitReadView active_unit
    )
    {
        _ensure_sidecars_ready();
        return _skill_resolution_rules != null
            ? _skill_resolution_rules.CollectGroundTerrainEffectDefs(
                skill_def,
                cast_variant,
                active_unit
            )
            : Array.Empty<CombatEffectDef>();
    }

    internal IReadOnlyList<StringName> CollectGroundPreviewUnitIdsTyped(
        BattleUnitState source_unit,
        SkillDef skill_def,
        IReadOnlyList<CombatEffectDef> effect_defs,
        IReadOnlyList<Vector2I> effect_coords
    )
    {
        _ensure_sidecars_ready();
        return _ground_effect_service.CollectGroundPreviewUnitIds(
            source_unit,
            skill_def,
            effect_defs ?? Array.Empty<CombatEffectDef>(),
            effect_coords ?? Array.Empty<Vector2I>()
        );
    }

    internal IReadOnlyList<StringName> CollectGroundPreviewUnitIdsTyped(
        BattleUnitReadView source_unit,
        SkillDef skill_def,
        IReadOnlyList<CombatEffectDef> effect_defs,
        IReadOnlyList<Vector2I> effect_coords
    )
    {
        _ensure_sidecars_ready();
        return _ground_effect_service.CollectGroundPreviewUnitIds(
            source_unit,
            skill_def,
            effect_defs ?? Array.Empty<CombatEffectDef>(),
            effect_coords ?? Array.Empty<Vector2I>()
        );
    }

    internal BattleGroundUnitEffectsResult ApplyGroundUnitEffectsResultTyped(
        BattleUnitState source_unit,
        SkillDef skill_def,
        IReadOnlyList<CombatEffectDef> effect_defs,
        IReadOnlyList<Vector2I> effect_coords,
        BattleEventBatch batch,
        IReadOnlyList<Vector2I> target_coords = null
    )
    {
        _ensure_sidecars_ready();
        return _ground_effect_service._apply_ground_unit_effects_result(
            source_unit,
            skill_def,
            effect_defs ?? Array.Empty<CombatEffectDef>(),
            effect_coords ?? Array.Empty<Vector2I>(),
            batch,
            target_coords ?? Array.Empty<Vector2I>()
        );
    }

    internal BattleGroundUnitEffectsResult _apply_ground_unit_effects_result(
        BattleUnitState source_unit,
        SkillDef skill_def,
        GCombatEffectArray effect_defs,
        GVector2IArray effect_coords,
        BattleEventBatch batch,
        GVector2IArray target_coords = null
    )
    {
        return ApplyGroundUnitEffectsResultTyped(
            source_unit,
            skill_def,
            ToCombatEffectDefList(effect_defs),
            ToVector2IList(effect_coords),
            batch,
            ToVector2IList(target_coords ?? new GVector2IArray())
        );
    }

    internal bool _should_resolve_ground_effects_as_attack(GCombatEffectArray effect_defs)
    {
        _ensure_sidecars_ready();
        return BattleGroundEffectService.ShouldResolveGroundEffectsAsAttack(
            ToCombatEffectDefList(effect_defs)
        );
    }

    internal BattleGroundTerrainEffectsResult ApplyGroundTerrainEffectsResultTyped(
        BattleUnitState source_unit,
        SkillDef skill_def,
        IReadOnlyList<CombatEffectDef> effect_defs,
        IReadOnlyList<Vector2I> effect_coords,
        BattleEventBatch batch
    )
    {
        _ensure_sidecars_ready();
        return _ground_effect_service._apply_ground_terrain_effects_result(
            source_unit,
            skill_def,
            effect_defs ?? Array.Empty<CombatEffectDef>(),
            effect_coords ?? Array.Empty<Vector2I>(),
            batch
        );
    }

    internal BattleGroundTerrainEffectsResult _apply_ground_terrain_effects_result(
        BattleUnitState source_unit,
        SkillDef skill_def,
        GCombatEffectArray effect_defs,
        GVector2IArray effect_coords,
        BattleEventBatch batch
    )
    {
        return ApplyGroundTerrainEffectsResultTyped(
            source_unit,
            skill_def,
            ToCombatEffectDefList(effect_defs),
            ToVector2IList(effect_coords),
            batch
        );
    }

    internal bool _apply_ground_cell_effect(
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

    internal bool _reconcile_water_topology(GVector2IArray effect_coords, BattleEventBatch batch)
    {
        _ensure_sidecars_ready();
        return _ground_effect_service.ReconcileWaterTopology(ToVector2IList(effect_coords), batch);
    }

    internal bool _is_unit_effect(CombatEffectDef effect_def)
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._is_unit_effect(effect_def);
    }

    internal BattleShieldApplyResult ApplyUnitShieldEffectsResult(
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

    internal BattleShieldApplyResult ApplyShieldEffectToTargetResult(
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

    internal void _write_unit_shield(
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

    internal int _resolve_shield_hp(
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

    internal int _roll_shield_hp(CombatEffectDef effect_def)
    {
        _ensure_sidecars_ready();
        return _shield_service._roll_shield_hp(effect_def);
    }

    internal bool _has_shield_dice_config(CombatEffectDef effect_def)
    {
        _ensure_sidecars_ready();
        return _shield_service._has_shield_dice_config(effect_def);
    }

    internal int _get_shield_roll_cache_key(CombatEffectDef effect_def)
    {
        _ensure_sidecars_ready();
        return (int)_shield_service._get_shield_roll_cache_key(effect_def);
    }

    internal int _roll_battle_effect_die(int dice_sides)
    {
        _ensure_sidecars_ready();
        return _shield_service._roll_battle_effect_die(dice_sides);
    }

    internal int _resolve_shield_duration_tu(CombatEffectDef effect_def)
    {
        _ensure_sidecars_ready();
        return _shield_service._resolve_shield_duration_tu(effect_def);
    }

    internal StringName _resolve_shield_family(SkillDef skill_def, CombatEffectDef effect_def)
    {
        _ensure_sidecars_ready();
        return _shield_service._resolve_shield_family(skill_def, effect_def);
    }

    internal bool _is_terrain_effect(CombatEffectDef effect_def)
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._is_terrain_effect(effect_def);
    }

    internal StringName _resolve_effect_target_filter(SkillDef skill_def, CombatEffectDef effect_def)
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._resolve_effect_target_filter(skill_def, effect_def);
    }

    internal bool _is_unit_valid_for_effect(
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

    internal bool _is_unit_valid_for_effect(
        BattleUnitReadView source_unit,
        BattleUnitReadView target_unit,
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

    internal StringName _build_terrain_effect_instance_id(StringName effect_id)
    {
        _ensure_sidecars_ready();
        return _ground_effect_service._build_terrain_effect_instance_id(effect_id);
    }

    internal string _get_terrain_effect_display_name(CombatEffectDef effect_def)
    {
        _ensure_sidecars_ready();
        return _ground_effect_service._get_terrain_effect_display_name(effect_def);
    }

    internal void _append_batch_log(BattleEventBatch batch, string message)
    {
        if (batch == null || string.IsNullOrEmpty(message))
            return;
        batch.AddLogLine(message);
        _state?.AppendLogEntry(message);
    }

    internal void _grant_skill_mastery_if_needed(
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
        _battle_rating_system.RecordSkillSuccess(active_unit, skillId);
        _characterGateway.RecordAchievementEvent(
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
        CharacterProgressionDelta delta = _characterGateway.GrantBattleMastery(
            active_unit.source_member_id,
            masterySkillId,
            masteryAmount
        );
        _append_progression_delta_to_batch(active_unit, delta, batch);
    }

    internal void _apply_skill_mastery_grant(
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
            _characterGateway.RecordAchievementEvent(
                grant.MemberId,
                "near_death_unbroken_manual",
                1,
                "",
                new GDictionary()
            );
        CharacterProgressionDelta delta = _characterGateway.GrantSkillMasteryFromSource(
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

    internal void _flush_last_stand_mastery_records(BattleEventBatch batch)
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

    internal void _append_progression_delta_to_batch(
        BattleUnitState unit_state,
        CharacterProgressionDelta delta,
        BattleEventBatch batch
    )
    {
        if (unit_state == null || delta == null)
            return;
        if (_progression_delta_is_empty(delta))
            return;
        batch?.AddProgressionDelta(delta);
        _unit_factory.RefreshKnownSkills(unit_state);
        if (delta.needs_promotion_modal)
        {
            if (_state == null)
                return;
            _state.ModalStateKind = BattleModalStateKind.PromotionChoice;
            if (_state.timeline != null)
                _state.timeline.frozen = true;
            if (batch != null)
            {
                batch.modal_requested = true;
                batch.AddLogLine($"{unit_state.display_name} 触发职业晋升选择。");
            }
        }
    }

    internal bool _progression_delta_is_empty(CharacterProgressionDelta delta)
    {
        if (delta == null)
            return true;
        return delta.MasteryChangesTyped.Count == 0
            && delta.LeveledSkillIdsTyped.Count == 0
            && delta.GrantedSkillIdsTyped.Count == 0
            && delta.ChangedProfessionIdsTyped.Count == 0
            && delta.KnowledgeChangesTyped.Count == 0
            && delta.AttributeChangesTyped.Count == 0
            && delta.UnlockedAchievementIdsTyped.Count == 0
            && !delta.needs_promotion_modal;
    }

    internal CombatCastVariantDef ResolveGroundCastVariantTyped(
        SkillDef skill_def,
        BattleUnitState active_unit,
        BattleCommand command
    )
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator.ResolveGroundCastVariant(skill_def, active_unit, command)
            as CombatCastVariantDef;
    }

    internal CombatCastVariantDef ResolveGroundCastVariantTyped(
        SkillDef skill_def,
        BattleUnitReadView active_unit,
        BattleCommand command
    )
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator.ResolveGroundCastVariant(skill_def, active_unit, command)
            as CombatCastVariantDef;
    }

    internal CombatCastVariantDef _resolve_unit_cast_variant(
        SkillDef skill_def,
        BattleUnitState active_unit,
        BattleCommand command
    )
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._resolve_unit_cast_variant(skill_def, active_unit, command)
            as CombatCastVariantDef;
    }

    internal CombatCastVariantDef _build_implicit_ground_cast_variant(SkillDef skill_def)
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._build_implicit_ground_cast_variant(skill_def);
    }

    internal BattleGroundSkillValidationResult ValidateGroundSkillCommandResultTyped(
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

    internal BattleGroundSkillValidationResult ValidateGroundSkillCommandResultTyped(
        BattleUnitReadView active_unit,
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

    internal BattleGroundSkillValidationResult _validate_ground_skill_command_result(
        BattleUnitState active_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        BattleCommand command
    )
    {
        return ValidateGroundSkillCommandResultTyped(
            active_unit,
            skill_def,
            cast_variant,
            command
        );
    }

    internal string GetGroundSpecialEffectValidationMessageTyped(
        BattleUnitState active_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        IReadOnlyList<Vector2I> target_coords
    )
    {
        _ensure_sidecars_ready();
        return _ground_effect_service.GetGroundSpecialEffectValidationMessage(
            active_unit,
            skill_def,
            cast_variant,
            target_coords ?? Array.Empty<Vector2I>()
        );
    }

    internal string GetGroundSpecialEffectValidationMessageTyped(
        BattleUnitReadView active_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        IReadOnlyList<Vector2I> target_coords
    )
    {
        _ensure_sidecars_ready();
        return _ground_effect_service.GetGroundSpecialEffectValidationMessage(
            active_unit,
            skill_def,
            cast_variant,
            target_coords ?? Array.Empty<Vector2I>()
        );
    }

    internal bool _validate_target_coords_shape(
        StringName footprint_pattern,
        GVector2IArray target_coords
    )
    {
        _ensure_sidecars_ready();
        return _ground_effect_service._validate_target_coords_shape(
            CombatCastVariantDef.ToFootprintPattern(footprint_pattern),
            target_coords
        );
    }

    internal GVector2IArray _normalize_target_coords(BattleCommand command)
    {
        _ensure_sidecars_ready();
        return _ground_effect_service._normalize_target_coords(command);
    }

    internal void _append_changed_coord(BattleEventBatch batch, Vector2I coord)
    {
        if (batch == null || batch.ContainsChangedCoord(coord))
            return;
        batch.AddChangedCoord(coord);
    }

    internal void _append_changed_coords(BattleEventBatch batch, GArray coords)
    {
        foreach (Vector2I coord in ToVector2IArray(coords))
        {
            _append_changed_coord(batch, coord);
        }
    }

    internal void _append_changed_coords(BattleEventBatch batch, GVector2IArray coords)
    {
        if (coords == null)
            return;
        foreach (Vector2I coord in coords)
        {
            _append_changed_coord(batch, coord);
        }
    }

    internal void _append_changed_unit_id(BattleEventBatch batch, StringName unit_id)
    {
        if (batch == null || IsEmpty(unit_id) || batch.ContainsChangedUnitId(unit_id))
            return;
        batch.AddChangedUnitId(unit_id);
    }

    internal void _append_changed_unit_coords(BattleEventBatch batch, BattleUnitState unit_state)
    {
        if (unit_state == null)
            return;
        unit_state.RefreshFootprint();
        _append_changed_coords(batch, unit_state.occupied_coords);
    }

    internal void _collect_defeated_unit_loot(
        BattleUnitState unit_state,
        BattleUnitState killer_unit = null
    ) => _loot_resolver.CollectDefeatedUnitLoot(unit_state, killer_unit);

    internal void HandleUnitDefeatedByRuntimeEffect(
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
            _battle_rating_system.RecordEnemyDefeatedAchievement(source_unit, unit_state);
        if (!string.IsNullOrEmpty(log_line) && batch != null)
            batch.AddLogLine(log_line);
        if (options.CheckBattleEnd)
            _check_battle_end(batch);
    }

    internal void RemoveSummonedUnitFromBattle(
        BattleUnitState unit_state,
        BattleEventBatch batch,
        string log_line = ""
    )
    {
        if (_state == null || unit_state == null)
            return;
        GVector2IArray previousCoords = DuplicateCoordArray(unit_state.occupied_coords);
        unit_state.MarkDead();
        _grid_service.ClearUnitOccupancy(_state, unit_state);
        _append_changed_coords(batch, previousCoords);
        _append_changed_unit_id(batch, unit_state.unit_id);
        if (!string.IsNullOrEmpty(log_line) && batch != null)
            batch.AddLogLine(log_line);
        _record_unit_defeated(unit_state);
        _check_battle_end(batch);
    }

    internal void _clear_defeated_unit(BattleUnitState unit_state, BattleEventBatch batch = null)
    {
        if (_state == null || unit_state == null)
            return;
        _handle_adjacent_ally_defeat(unit_state);
        _handle_low_luck_relic_ally_defeat(unit_state, batch);
        GVector2IArray previousCoords = DuplicateCoordArray(unit_state.occupied_coords);
        _grid_service.ClearUnitOccupancy(_state, unit_state);
        _append_changed_coords(batch, previousCoords);
        _append_changed_unit_id(batch, unit_state.unit_id);
    }

    internal void _merge_batch(BattleEventBatch target_batch, BattleEventBatch source_batch)
    {
        if (target_batch == null || source_batch == null)
            return;
        foreach (Vector2I coord in source_batch.ChangedCoordsTyped)
            _append_changed_coord(target_batch, coord);
        foreach (StringName unitId in source_batch.ChangedUnitIdsTyped)
            _append_changed_unit_id(target_batch, unitId);
        foreach (string logLine in source_batch.LogLinesTyped)
            target_batch.AddLogLine(logLine);
        foreach (GDictionary reportEntry in source_batch.ReportEntriesTyped)
        {
            target_batch.AddReportEntry(reportEntry);
        }
    }

    internal GVector2IArray _sort_coords(GArray target_coords)
    {
        var sortedCoords = ToVector2IArray(target_coords);
        return _sort_coords(sortedCoords);
    }

    internal GVector2IArray _sort_coords(GVector2IArray target_coords)
    {
        var coords = new List<Vector2I>();
        foreach (Vector2I coord in target_coords ?? new GVector2IArray())
            coords.Add(coord);
        coords.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
        var result = new GVector2IArray();
        foreach (Vector2I coord in coords)
            result.Add(coord);
        return result;
    }

    internal int _normalize_unit_action_threshold(int action_threshold)
    {
        _ensure_sidecars_ready();
        return _timeline_driver.NormalizeUnitActionThreshold(action_threshold);
    }

    internal void _initialize_unit_action_thresholds()
    {
        _ensure_sidecars_ready();
        _timeline_driver.InitializeUnitActionThresholds();
    }

    internal void _initialize_unit_trait_hooks()
    {
        _ensure_sidecars_ready();
        _timeline_driver.InitializeUnitTraitHooks();
    }

    internal int _resolve_unit_action_threshold(BattleUnitState unit_state)
    {
        _ensure_sidecars_ready();
        return _timeline_driver.ResolveUnitActionThreshold(unit_state);
    }

    internal int _resolve_timeline_tu_per_tick(GDictionary context)
    {
        _ensure_sidecars_ready();
        return _timeline_driver.ResolveTimelineTuPerTick(context);
    }

    internal GVector2IArray _collect_dict_vector2i_keys(GDictionary values)
    {
        _ensure_sidecars_ready();
        var coords = new GVector2IArray();
        if (values == null)
        {
            return coords;
        }
        foreach (Variant rawCoord in values.Keys)
        {
            coords.Add(rawCoord.AsVector2I());
        }
        return coords;
    }

    internal int _get_unit_skill_level(BattleUnitState unit_state, StringName skill_id)
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._get_unit_skill_level(unit_state, skill_id);
    }

    internal string _format_skill_variant_label(SkillDef skill_def, CombatCastVariantDef cast_variant)
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._format_skill_variant_label(skill_def, cast_variant);
    }

    internal bool _check_battle_end(BattleEventBatch batch)
    {
        _ensure_sidecars_ready();
        return _timeline_driver.CheckBattleEnd(batch);
    }

    internal int _count_living_units(GStringNameArray unit_ids)
    {
        _ensure_sidecars_ready();
        return _timeline_driver.CountLivingUnits(unit_ids);
    }

    internal void _end_active_turn(BattleEventBatch batch)
    {
        _ensure_sidecars_ready();
        _timeline_driver.EndActiveTurn(batch);
    }

    internal void _handle_adjacent_ally_defeat(BattleUnitState defeated_unit)
    {
        _ensure_sidecars_ready();
        _special_skill_resolver.HandleAdjacentAllyDefeat(defeated_unit);
    }

    internal void _handle_low_luck_relic_ally_defeat(
        BattleUnitState defeated_unit,
        BattleEventBatch batch = null
    )
    {
        _ensure_sidecars_ready();
        _special_skill_resolver.HandleLowLuckRelicAllyDefeat(defeated_unit, batch);
    }

    internal bool _are_units_adjacent(BattleUnitState first_unit, BattleUnitState second_unit)
    {
        _ensure_sidecars_ready();
        return _special_skill_resolver.AreUnitsAdjacent(first_unit, second_unit);
    }

    internal void _activate_next_ready_unit(BattleEventBatch batch)
    {
        _ensure_sidecars_ready();
        _timeline_driver.ActivateNextReadyUnit(batch);
    }

    internal BattleSkillCastBlockReasonKind _get_skill_cast_block_reason(
        BattleUnitState active_unit,
        SkillDef skill_def
    ) =>
        _skill_turn_resolver.GetSkillCastBlockReason(active_unit, skill_def);

    internal string _get_skill_cast_block_message(BattleUnitState active_unit, SkillDef skill_def) =>
        _skill_turn_resolver.FormatSkillCastBlockReason(
            active_unit,
            skill_def,
            _get_skill_cast_block_reason(active_unit, skill_def)
        );

    internal bool _unit_has_melee_weapon(BattleUnitState active_unit) =>
        _skill_turn_resolver.UnitHasMeleeWeapon(active_unit);

    internal bool _requires_melee_weapon(SkillDef skill_def) =>
        _skill_turn_resolver.RequiresMeleeWeapon(skill_def);

    internal bool _effect_uses_weapon_physical_damage_tag(CombatEffectDef effect_def) =>
        _skill_turn_resolver.EffectUsesWeaponPhysicalDamageTag(effect_def);

    internal string _get_skill_command_block_reason(
        BattleUnitState active_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant
    ) => _skill_turn_resolver.GetSkillCommandBlockReason(active_unit, skill_def, cast_variant);

    internal string _get_skill_command_block_reason(
        BattleUnitReadView active_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant
    ) => _skill_turn_resolver.GetSkillCommandBlockReason(active_unit, skill_def, cast_variant);

    internal bool _consume_skill_costs(
        BattleUnitState active_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant = null,
        BattleEventBatch batch = null
    ) => _skill_turn_resolver.ConsumeSkillCosts(active_unit, skill_def, cast_variant, batch);

    internal CombatSkillResourceCosts _get_effective_skill_resource_costs(
        BattleUnitState active_unit,
        SkillDef skill_def
    ) => _skill_turn_resolver.GetEffectiveSkillResourceCosts(active_unit, skill_def);

    internal string _get_black_contract_push_variant_block_reason(
        BattleUnitState active_unit,
        CombatCastVariantDef cast_variant
    )
    {
        BattleSkillCastBlockReasonKind blockReason =
            _skill_turn_resolver.GetBlackContractPushVariantBlockReason(
            active_unit,
            cast_variant
        );
        return _skill_turn_resolver.FormatSkillCastBlockReason(
            active_unit,
            null,
            blockReason,
            cast_variant
        );
    }

    internal bool _consume_black_contract_push_cast(
        BattleUnitState active_unit,
        CombatCastVariantDef cast_variant,
        BattleEventBatch batch = null
    ) => _skill_turn_resolver.ConsumeBlackContractPushCast(active_unit, cast_variant, batch);

    internal void _ensure_unit_turn_anchor(BattleUnitState unit_state) =>
        _skill_turn_resolver.EnsureUnitTurnAnchor(unit_state);

    internal bool _advance_unit_cooldowns(BattleUnitState unit_state, int cooldown_delta) =>
        _skill_turn_resolver.AdvanceUnitCooldowns(unit_state, cooldown_delta);

    internal bool _consume_turn_cooldown_delta(BattleUnitState unit_state) =>
        _skill_turn_resolver.ConsumeTurnCooldownDelta(unit_state);

    internal void _advance_unit_turn_timers(BattleUnitState unit_state, BattleEventBatch batch) =>
        _skill_turn_resolver.AdvanceUnitTurnTimers(unit_state, batch);

    internal BattleStatusTickResult _apply_turn_start_statuses_result(
        BattleUnitState unit_state,
        BattleEventBatch batch
    ) => _skill_turn_resolver.ApplyTurnStartStatusesResult(unit_state, batch);

    internal BattleStatusTickResult _apply_unit_status_periodic_ticks_result(
        BattleUnitState unit_state,
        int elapsed_tu,
        BattleEventBatch batch
    ) => _skill_turn_resolver.ApplyUnitStatusPeriodicTicksResult(unit_state, elapsed_tu, batch);

    internal bool _advance_unit_status_durations(
        BattleUnitState unit_state,
        int elapsed_tu,
        BattleEventBatch batch = null
    ) => _skill_turn_resolver.AdvanceUnitStatusDurations(unit_state, elapsed_tu, batch);

    internal int _get_effective_skill_range(BattleUnitState active_unit, SkillDef skill_def) =>
        _skill_turn_resolver.GetEffectiveSkillRange(active_unit, skill_def);

    internal int _get_effective_skill_range(BattleUnitReadView active_unit, SkillDef skill_def) =>
        _skill_turn_resolver.GetEffectiveSkillRange(active_unit, skill_def);

    internal int _resolve_base_skill_range(BattleUnitState active_unit, SkillDef skill_def) =>
        _skill_turn_resolver.ResolveBaseSkillRange(active_unit, skill_def);

    internal bool _is_weapon_range_skill(SkillDef skill_def) =>
        _skill_turn_resolver.IsWeaponRangeSkill(skill_def);

    internal int _get_weapon_attack_range(BattleUnitState active_unit) =>
        _skill_turn_resolver.GetWeaponAttackRange(active_unit);

    internal bool _skill_has_tag(SkillDef skill_def, StringName expected_tag) =>
        _skill_turn_resolver.SkillHasTag(skill_def, expected_tag);

    internal bool _is_movement_blocked(BattleUnitState unit_state) =>
        _skill_turn_resolver.IsMovementBlocked(unit_state);

    internal bool _has_status(BattleUnitState unit_state, StringName status_id) =>
        _skill_turn_resolver.HasUnitStatus(unit_state, status_id);

    internal void _consume_status_if_present(
        BattleUnitState unit_state,
        StringName status_id,
        BattleEventBatch batch = null
    ) => _skill_turn_resolver.ConsumeStatusIfPresent(unit_state, status_id, batch);

    internal bool _is_main_skill_locked_by_status(BattleUnitState active_unit, SkillDef skill_def) =>
        _skill_turn_resolver.IsMainSkillLockedByStatus(active_unit, skill_def);

    internal int _count_debuff_statuses(BattleUnitState unit_state) =>
        _skill_turn_resolver.CountDebuffStatuses(unit_state);

    internal bool _status_counts_as_debuff(
        StringName status_id,
        BattleStatusEffectState status_entry
    ) => _skill_turn_resolver.StatusCountsAsDebuff(status_id, status_entry);

    internal bool _has_counterattack_lock_status(BattleUnitState unit_state) =>
        _skill_turn_resolver.HasCounterattackLockStatus(unit_state);

    internal int _get_main_skill_lock_other_debuff_count(BattleUnitState unit_state) =>
        _skill_turn_resolver.GetMainSkillLockOtherDebuffCount(unit_state);

    internal void _prepare_ai_turn(BattleUnitState unit_state)
    {
        if (unit_state == null)
            return;
        unit_state.ai_blackboard.SetInt(
            "turn_started_tu",
            _state?.timeline != null ? _state.timeline.current_tu : 0
        );
        unit_state.ai_blackboard.SetInt("turn_decision_count", 0);
        EnemyAiBrainDef brain = GetEnemyAiBrainTyped(unit_state.ai_brain_id);
        if (brain != null && !brain.HasState(unit_state.ai_state_id))
            unit_state.ai_state_id = brain.default_state_id;
    }

    internal void _cleanup_ai_turn(BattleUnitState unit_state)
    {
        if (unit_state == null)
            return;
        unit_state.ai_blackboard.Remove("turn_started_tu");
        unit_state.ai_blackboard.Remove("turn_decision_count");
        _skill_turn_resolver?.ClearTurnAiOverride(unit_state);
    }

    internal BattleUnitState _find_unit_by_member_id(StringName member_id)
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

    internal void _sort_ready_unit_ids_by_action_priority()
    {
        _ensure_sidecars_ready();
        _timeline_driver.SortReadyUnitIdsByActionPriority();
    }

    internal bool _is_left_ready_unit_higher_priority(
        StringName left_unit_id,
        StringName right_unit_id
    )
    {
        _ensure_sidecars_ready();
        return _timeline_driver.IsLeftReadyUnitHigherPriority(left_unit_id, right_unit_id);
    }

    internal int _get_unit_turn_order_attribute(BattleUnitState unit_state, StringName attribute_id)
    {
        _ensure_sidecars_ready();
        return _timeline_driver.GetUnitTurnOrderAttribute(unit_state, attribute_id);
    }

    internal int _get_unit_turn_order_action_points(BattleUnitState unit_state)
    {
        _ensure_sidecars_ready();
        return _timeline_driver.GetUnitTurnOrderActionPoints(unit_state);
    }

    internal GStringNameArray _get_units_in_order()
    {
        _ensure_sidecars_ready();
        return _timeline_driver.GetUnitsInOrder();
    }

    internal BattleEventBatch _new_batch() => new();

    internal BattleResolutionResult _build_battle_resolution_result() =>
        _loot_resolver.BuildBattleResolutionResult();

    internal AttackRollResult _roll_hit_rate(int hit_rate_percent) =>
        _hit_resolver.RollHitRate(_state, hit_rate_percent);

    private void BindDamageResolver()
    {
        if (_damage_resolver == null)
            return;
        _damage_resolver.SetSkillDefs(GetSkillDefIndexTyped());
        _damage_resolver.SetHitResolver(_hit_resolver);
    }

    private static bool IsEmpty(StringName value) => value == default || value == (StringName)"";

    private static GDictionary GetDict(GDictionary dict, string key)
    {
        if (dict == null || string.IsNullOrEmpty(key))
            return new GDictionary();
        if (dict.ContainsKey(key))
            return dict[key].AsGodotDictionary();
        return new GDictionary();
    }

    private static GArray GetArray(GDictionary dict, string key)
    {
        if (dict == null || string.IsNullOrEmpty(key))
            return new GArray();
        if (dict.ContainsKey(key))
            return dict[key].AsGodotArray();
        return new GArray();
    }

    private static string GetString(GDictionary dict, string key, string fallback = "")
    {
        if (dict == null || string.IsNullOrEmpty(key))
            return fallback;
        string text = "";
        if (dict.ContainsKey(key))
            text = dict[key].ToString();
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
        return fallback;
    }

    private static Vector2I GetVector2I(GDictionary dict, string key, Vector2I fallback)
    {
        if (dict == null || string.IsNullOrEmpty(key))
            return fallback;
        if (dict.ContainsKey(key))
            return dict[key].AsVector2I();
        return fallback;
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

    private static GVector2IArray ToVector2IArray(IEnumerable<Vector2I> values)
    {
        var result = new GVector2IArray();
        if (values == null)
            return result;
        foreach (Vector2I value in values)
        {
            result.Add(value);
        }
        return result;
    }

    private static GVector2IArray DuplicateCoordArray(IEnumerable<Vector2I> values) =>
        ToVector2IArray(values);

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

    private static GBattleUnitArray ToBattleUnitArray(IEnumerable<BattleUnitState> values)
    {
        var result = new GBattleUnitArray();
        if (values == null)
            return result;
        foreach (BattleUnitState unitState in values)
        {
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

    private static GCombatEffectArray ToCombatEffectArray(
        IEnumerable<CombatEffectDef> values
    )
    {
        var result = new GCombatEffectArray();
        if (values == null)
            return result;
        foreach (CombatEffectDef effectDef in values)
        {
            if (effectDef != null)
                result.Add(effectDef);
        }
        return result;
    }

    private static GStringNameArray ToStringNameArray(
        IEnumerable<StringName> values
    )
    {
        var result = new GStringNameArray();
        if (values == null)
            return result;
        foreach (StringName value in values)
            result.Add(value);
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

    private static List<Vector2I> ToVector2IList(GArray values)
    {
        var result = new List<Vector2I>();
        if (values == null)
            return result;
        foreach (var value in values)
            result.Add(value.AsVector2I());
        return result;
    }

    private static List<Vector2I> ToVector2IList(GVector2IArray values)
    {
        var result = new List<Vector2I>();
        if (values == null)
            return result;
        foreach (Vector2I value in values)
            result.Add(value);
        return result;
    }

    private static List<CombatEffectDef> ToCombatEffectDefList(GCombatEffectArray values)
    {
        var result = new List<CombatEffectDef>();
        if (values == null)
            return result;
        foreach (CombatEffectDef value in values)
        {
            if (value != null)
                result.Add(value);
        }
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
