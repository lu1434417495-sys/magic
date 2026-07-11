using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using Godot;
using GArray = Godot.Collections.Array;
using GBattleUnitArray = System.Collections.Generic.List<BattleUnitState>;
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

internal enum BattleStartContextReferenceRole
{
    OwnedByStartScope = 0,
    BorrowedForSynchronousStart = 1,
}

internal readonly struct BattleDefeatHandlingOptions
{
    internal readonly bool CollectLoot;
    internal readonly bool RecordEnemyDefeatedAchievement;
    internal readonly bool CheckBattleEnd;
    internal readonly BattleKillProvenance KillProvenance;

    internal BattleDefeatHandlingOptions(
        bool collectLoot = true,
        bool recordEnemyDefeatedAchievement = false,
        bool checkBattleEnd = true,
        BattleKillProvenance killProvenance = default
    )
    {
        CollectLoot = collectLoot;
        RecordEnemyDefeatedAchievement = recordEnemyDefeatedAchievement;
        CheckBattleEnd = checkBattleEnd;
        KillProvenance = killProvenance;
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

internal sealed class BattleEndResult
{
    internal bool Ok { get; init; } = true;
    internal string ErrorCode { get; init; } = "";
    internal int FlushError { get; init; } = (int)Error.Ok;
    internal ContingencyConsumedCommitResult ContingencyConsumedResult { get; init; }
    internal BattleResourceCommitResult ResourceCommitResult { get; init; }

    internal static BattleEndResult Success() => new();

    internal static BattleEndResult ContingencyConsumedFailure(
        ContingencyConsumedCommitResult result
    ) =>
        new()
        {
            Ok = false,
            ErrorCode = "contingency_consumed_commit_failed",
            ContingencyConsumedResult = result,
        };

    internal static BattleEndResult ResourceCommitFailure(
        BattleResourceCommitResult result
    ) =>
        new()
        {
            Ok = false,
            ErrorCode = "battle_resource_commit_failed",
            ResourceCommitResult = result,
        };

    internal static BattleEndResult FlushFailure(int flushError) =>
        new()
        {
            Ok = false,
            ErrorCode = "battle_writeback_flush_failed",
            FlushError = flushError,
        };
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
    internal BattleSpawnReachabilityResult ReachabilityResult { get; init; }

    public bool IsEmpty =>
        string.IsNullOrEmpty(Reason)
        && AllyUnitCount < 0
        && EnemyUnitCount < 0
        && PlacementAttempt < 0
        && AllySpawnCount < 0
        && EnemySpawnCount < 0
        && PlacementAttempts < 0
        && ReachabilityResult == null;

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
            ReachabilityResult = ReadReachabilityResult(source),
        };
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

    private static BattleSpawnReachabilityResult ReadReachabilityResult(GDictionary source)
    {
        if (!source.ContainsKey("reachability"))
            return null;
        Variant value = source["reachability"];
        if (value.VariantType != Variant.Type.Dictionary)
            return null;
        GDictionary reachability = value.AsGodotDictionary();
        return reachability.Count > 0
            ? BattleSpawnReachabilityProjection.ParseResultPayload(reachability)
            : null;
    }

    private static string ReadString(GDictionary source, string key)
    {
        if (source == null || string.IsNullOrEmpty(key) || !source.ContainsKey(key))
            return "";
        return source[key].ToString();
    }
}

public sealed partial class BattleRuntimeModule : IDisposable
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

    private readonly Dictionary<StringName, SkillDefinition> _skillDefinitionIndex = new();
    private readonly Dictionary<StringName, EnemyTemplateDef> _enemyTemplateIndex = new();
    private readonly Dictionary<StringName, EnemyAiBrainDef> _enemyAiBrainIndex = new();
    private readonly Dictionary<StringName, ItemDef> _itemDefIndex = new();
    private readonly Dictionary<StringName, TraitDef> _traitDefIndex = new();
    private readonly Dictionary<StringName, EquipmentAbilityBindingDefinition> _equipmentAbilityBindingIndex = new();
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
    internal BattleDelayedAreaEffectSystem _delayed_area_effect_system = new();
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
    private readonly BattleRuntimeServices _runtime_services = new();
    internal BattleGroundEffectService _ground_effect_service => _runtime_services.GroundEffects;
    internal BattleSpecialSkillResolver _special_skill_resolver => _runtime_services.SpecialSkills;
    internal BattleMovementService _movement_service => _runtime_services.Movement;
    internal BattleContingencySystem _contingency_system => _runtime_services.Contingencies;
    internal BattleLayeredBarrierService _layered_barrier_service = new();
    internal BattleTimelineDriver _timeline_driver = new();
    internal BattleSkillExecutionOrchestrator _skill_orchestrator = new();
    internal BattleCastingTimeService _casting_time_service = new();
    internal TraitTriggerHooks _trait_trigger_hooks = new();
    internal readonly Dictionary<string, object> _special_profile_registry_snapshot = new(
        StringComparer.Ordinal
    );
    private IBattleSpecialProfileView _special_profile_view = BattleSpecialProfileRuntimeView.Empty;
	internal BattleSpecialProfileGate _special_profile_gate;
	internal BattleMeteorSwarmResolver _meteor_swarm_resolver;
    internal BattleAttackCheckPolicyService _attack_check_policy_service = new();
    internal BattleEquipmentAbilityRuntimeService _equipment_ability_runtime_service = new();
    internal BattleSkillOutcomeCommitter _skill_outcome_committer = new();
    private readonly Dictionary<StringName, BattleRatingMemberStats> _battleRatingStatsByMemberId = new();
    private readonly List<PendingCharacterReward> _pendingPostBattleCharacterRewards = new();
    private readonly Stack<BattleEffectOrigin> _effectOriginStack = new();
    internal List<BattleLootEntry> _active_loot_entries = new();
    internal HashSet<StringName> _looted_defeated_unit_ids = new();
    internal BattleResolutionResult _battle_resolution_result;
    public bool _battle_resolution_result_consumed;
    public int _terrain_effect_nonce;
    public bool _ai_trace_enabled;
    private readonly List<BattleAiTurnTraceProjection> _ai_turn_traces = new();
    private int _contingencySourceEventOrdinal;
    internal Dictionary<StringName, BattleAiRuntimeActionPlan> _ai_action_plans_by_unit_id = new();
    private readonly Func<BattleUnitState, Vector2I, int> _ai_move_cost_callback;
    private readonly Func<BattleCommand, BattlePreview> _ai_preview_command_callback;
    private readonly Func<
        BattleAiContext,
        SkillDefinition,
        BattleCommand,
        BattlePreview,
        IReadOnlyList<CombatEffectDefinition>,
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
    private readonly Func<BattleUnitState, SkillDefinition, BattleSkillCastBlockReasonKind>
        _ai_skill_cast_block_reason_callback;
    internal BattleMetricsState _battle_metrics = new();
    private BattleStartFailureSnapshot _last_start_failure = new();
    internal BattleCalamityStore calamity_by_member_id = new();
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
        IReadOnlyDictionary<StringName, SkillDefinition> skill_definitions = null,
        IReadOnlyDictionary<StringName, EnemyTemplateDef> enemy_templates = null,
        IReadOnlyDictionary<StringName, EnemyAiBrainDef> enemy_ai_brains = null,
        EncounterRosterBuilder encounter_builder = null,
        EquipmentDropService equipment_drop_service = null,
        IReadOnlyDictionary<StringName, ItemDef> item_defs = null,
        BattleTerrainGenerator terrain_generator = null,
        Func<StringName> equipment_instance_id_allocator = null,
        GDictionary battle_special_profile_registry_snapshot = null,
        ISkillCatalog skill_catalog = null,
        IBattleSpecialProfileView battle_special_profile_view = null,
        IReadOnlyDictionary<StringName, TraitDef> trait_defs = null,
        IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> equipment_ability_bindings = null
    )
    {
        BeginContentCatalogRebind();
        _characterGateway = character_gateway;
        _skillCatalog = skill_catalog;
        IReadOnlyDictionary<StringName, SkillDefinition> catalogSkillDefinitions =
            _skillCatalog?.GetSkillDefinitionsTyped();
        IReadOnlyDictionary<StringName, SkillDefinition> resolvedSkillDefinitions =
            skill_definitions
            ?? catalogSkillDefinitions;
        ApplySkillDefinitionsTyped(resolvedSkillDefinitions);
        ReplaceSpecialProfileRegistrySnapshot(battle_special_profile_registry_snapshot);
        _special_profile_view = battle_special_profile_view ?? BattleSpecialProfileRuntimeView.Empty;
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
        ApplyTraitDefsTyped(trait_defs);
        ApplyEquipmentAbilityBindingsTyped(equipment_ability_bindings);

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

        ClearAiActionPlans();
        ClearLastStartFailure();
        _ai_service.Setup(_enemyAiBrainIndex, _damage_resolver);
        _terrain_effect_system.Setup(this);
        _delayed_area_effect_system.Setup(this);
        _attack_check_policy_service ??= new BattleAttackCheckPolicyService();
        _attack_check_policy_service.Setup(this, _hit_resolver, _terrain_effect_system);
        _equipment_ability_runtime_service ??= new BattleEquipmentAbilityRuntimeService();
        _equipment_ability_runtime_service.Setup(this, _damage_resolver);
        _damage_resolver?.SetEquipmentAbilityRuntimeService(_equipment_ability_runtime_service);
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
        _runtime_services.SetupRuntimeSidecars(this);
        _layered_barrier_service.Setup(this);
        _timeline_driver.Setup(this);
        _skill_orchestrator.Setup(this);
        _casting_time_service.Setup(this);
        _setup_special_profile_runtime();
        CompleteContentCatalogRebind();
    }

    internal void _setup_special_profile_runtime()
    {
        _special_profile_gate ??= new BattleSpecialProfileGate();
        using GodotProjectionLease<GDictionary> specialProfileRegistryLease =
            RuntimePlainPayload.ProjectDictionaryLease(
                _special_profile_registry_snapshot,
                "battle-runtime-special-profile-setup",
                LifetimeDomain.Battle,
                "BattleRuntimeModule.special_profile_registry_snapshot"
            );
        GDictionary specialProfileRegistrySnapshot = specialProfileRegistryLease.Value;
        _special_profile_gate.Setup(specialProfileRegistrySnapshot);
        _skill_outcome_committer ??= new BattleSkillOutcomeCommitter();
        _skill_outcome_committer.Setup(this);

        _meteor_swarm_resolver?.Dispose();
        _meteor_swarm_resolver = null;
        using GDictionary profiles = GetDict(specialProfileRegistrySnapshot, "profiles");
        using GDictionary meteorProfileSnapshot = GetDict(profiles, "meteor_swarm");
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
        return StartBattleCore(
            encounter_anchor,
            seed,
            context,
            BattleStartContextReferenceRole.OwnedByStartScope
        );
    }

    internal BattleState StartBattleBorrowingContext(
        EncounterAnchorData encounterAnchor,
        long seed,
        GDictionary borrowedContext
    )
    {
        ArgumentNullException.ThrowIfNull(borrowedContext);
        ValidateBorrowedStartContext(borrowedContext);
        return StartBattleCore(
            encounterAnchor,
            seed,
            borrowedContext,
            BattleStartContextReferenceRole.BorrowedForSynchronousStart
        );
    }

    private static void ValidateBorrowedStartContext(GDictionary context)
    {
        ValidateBorrowedArrayField(context, "battle_party");
        ValidateBorrowedArrayField(context, "enemy_units");
    }

    private static void ValidateBorrowedArrayField(GDictionary context, string key)
    {
        if (
            context.ContainsKey(key)
            && context[key].VariantType != Variant.Type.Array
        )
        {
            throw new InvalidOperationException(
                $"Borrowed battle start context field '{key}' must be an Array."
            );
        }
    }

    private BattleState StartBattleCore(
        EncounterAnchorData encounter_anchor,
        long seed,
        GDictionary context,
        BattleStartContextReferenceRole contextRole
    )
    {
        using var contextScope = new GodotTransientResourceScope("BattleRuntimeModule.StartBattle");
        if (contextRole == BattleStartContextReferenceRole.OwnedByStartScope)
            context = contextScope.OwnWrapper(context ?? new GDictionary(), "context");
        else if (context == null)
            throw new ArgumentNullException(nameof(context));
        ClearLastStartFailure();
        _ensure_sidecars_ready();
        var partyState =
            _characterGateway != null ? _characterGateway.GetPartyState() : null;
        GBattleUnitArray allyUnits = ToBattleUnitArray(
            _unit_factory.BuildAllyUnits(
                partyState,
                context,
                contextScope,
                BattleStartContextReferenceRole.BorrowedForSynchronousStart
            )
        );
        if (allyUnits.Count == 0)
        {
            allyUnits = ToBattleUnitArray(
                _unit_factory.BuildAllyUnits(
                    null,
                    context,
                    contextScope,
                    BattleStartContextReferenceRole.BorrowedForSynchronousStart
                )
            );
        }

        GBattleUnitArray enemyUnits = new();
        _active_loot_entries.Clear();
        _looted_defeated_unit_ids.Clear();
        ClearAiActionPlans();
        calamity_by_member_id.Clear();

        bool hasExplicitEnemyUnits = false;
        if (context.ContainsKey("enemy_units"))
        {
            using GArray explicitEnemyUnits = GetArray(context, "enemy_units");
            hasExplicitEnemyUnits = explicitEnemyUnits.Count > 0;
        }
        BattleStartOptions startOptions = BattleStartOptions.FromContext(
            context,
            !hasExplicitEnemyUnits
        );
        if (hasExplicitEnemyUnits)
        {
            enemyUnits = ToBattleUnitArray(
                _unit_factory.BuildEnemyUnits(
                    encounter_anchor,
                    context,
                    contextScope,
                    BattleStartContextReferenceRole.BorrowedForSynchronousStart
                )
            );
        }
        else if (_encounter_builder != null)
        {
            enemyUnits = ToBattleUnitArray(
                _encounter_builder.BuildEnemyUnitsFromDefinitionsTyped(
                    encounter_anchor,
                    GetSkillDefinitionIndexTyped(),
                    GetEnemyTemplateIndexTyped(),
                    GetEnemyAiBrainIndexTyped(),
                    GetItemDefIndexTyped(),
                    GetTraitDefIndexTyped(),
                    GetEquipmentAbilityBindingIndexTyped()
                )
            );
        }

        if (
            !ValidateBattleUnitsForStart(allyUnits, "ally")
            || !ValidateBattleUnitsForStart(enemyUnits, "enemy")
        )
        {
            ClearRuntimeBattleStateReference();
            ClearAiActionPlans();
            _last_start_failure = new BattleStartFailureSnapshot
            {
                Reason = "invalid_start_units",
                AllyUnitCount = allyUnits?.Count ?? 0,
                EnemyUnitCount = enemyUnits?.Count ?? 0,
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
                context,
                contextScope
            );
            if (terrainData.Count == 0)
            {
                continue;
            }
            StringName terrainProfileId = _resolve_formal_terrain_profile_id(terrainData);
            if (IsEmpty(terrainProfileId))
            {
                continue;
            }

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
            _state.ReplaceEnvironmentSnapshot(
                BattleEnvironmentSnapshot.FromBattleStartContext(context, terrainProfileId)
            );
            using (GDictionary cells = GetDict(terrainData, "cells"))
            {
                _state.SetCellsFromDictionary(
                    cells,
                    duplicateCells: false,
                    rebuildColumns: !terrainData.ContainsKey("cell_columns")
                );
            }
            if (terrainData.ContainsKey("cell_columns"))
            {
                using GDictionary cellColumns = GetDict(terrainData, "cell_columns");
                _state.ReplaceCellColumnsPayload(cellColumns);
            }
            _state.SetPartyBackpackView(_get_party_backpack_state(partyState) as WarehouseState);
            _state.timeline.tu_per_tick = _resolve_timeline_tu_per_tick(context);

            using GArray allySpawnCoords = GetArray(terrainData, "ally_spawns");
            using GArray enemySpawnCoords = GetArray(terrainData, "enemy_spawns");
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
            if (!PlaceUnitsTyped(allyUnits, ToVector2IList(allySpawnCoords), true, allySpawnSide))
            {
                ClearRuntimeBattleStateReference();
                continue;
            }
            if (!PlaceUnitsTyped(enemyUnits, ToVector2IList(enemySpawnCoords), false, enemySpawnSide))
            {
                ClearRuntimeBattleStateReference();
                continue;
            }
            _initialize_unit_trait_hooks();
            if (startOptions.ValidateSpawnReachability)
            {
                BattleSpawnReachabilityResult reachability =
                    _spawn_reachability_service.ValidateStateTyped(
                        _state,
                        _grid_service,
                        _skillDefinitionIndex,
                        new BattleSpawnReachabilityOptions(
                            startOptions.ValidateBidirectionalSpawnReachability
                        )
                    );
                if (!reachability.Valid)
                {
                    _last_start_failure = new BattleStartFailureSnapshot
                    {
                        Reason = "spawn_reachability",
                        PlacementAttempt = placementAttempt,
                        TerrainSeed = terrainSeed,
                        AllySpawnCount = allySpawnCoords.Count,
                        EnemySpawnCount = enemySpawnCoords.Count,
                        ReachabilityResult = reachability,
                    };
                    ClearRuntimeBattleStateReference();
                    ClearAiActionPlans();
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
            _contingency_system.ResetForBattle(partyState, _state);
            _initialize_battle_metrics();
            return _state;
        }

        ClearRuntimeBattleStateReference();
        ClearAiActionPlans();
        if (_last_start_failure.IsEmpty)
        {
            _last_start_failure = new BattleStartFailureSnapshot
            {
                Reason = "placement_exhausted",
                PlacementAttempts = BATTLE_START_PLACEMENT_MAX_ATTEMPTS,
            };
        }
        return new BattleState();
    }

    internal BattleStartFailureSnapshot GetLastStartFailureSnapshot() =>
        _last_start_failure ?? new BattleStartFailureSnapshot();

    private void ClearLastStartFailure()
    {
        _last_start_failure = new BattleStartFailureSnapshot();
    }

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
        ClearAiActionPlans();
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
                GetSkillDefinitionIndexTyped()
            );
            if (actionPlan != null)
                _ai_action_plans_by_unit_id[unitState.unit_id] = actionPlan;
        }
    }

    private void ClearAiActionPlans()
    {
        List<BattleAiRuntimeActionPlan> plans = new(_ai_action_plans_by_unit_id.Values);
        _ai_action_plans_by_unit_id.Clear();
        Exception firstFailure = null;
        foreach (BattleAiRuntimeActionPlan plan in plans)
        {
            RunTeardownStep(ref firstFailure, () => plan?.Dispose());
        }
        if (firstFailure != null)
        {
            ExceptionDispatchInfo.Capture(firstFailure).Throw();
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
            GetSkillDefinitionIndexTyped()
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
        _runtime_services.BindAiHelperServicesForDecision(
            new BattleAiHelperBindingContext(
                _state,
                _grid_service,
                unit_state,
                GetSkillDefinitionIndexTyped(),
                _skillCatalog,
                _ai_service.GetScoreService(),
                _get_ai_move_query_cost,
                _ai_query_action_score_input_callback,
                _ai_movement_blocked_callback,
                _ai_move_cost_callback,
                _ai_preview_command_callback,
                _ai_skill_score_input_callback,
                _ai_action_score_input_callback,
                _ai_skill_cast_block_reason_callback
            ),
            ai_context
        );
    }

    internal BattleAiContext _prepare_ai_context_for_decision(BattleUnitState activeUnit)
    {
        _ai_action_plans_by_unit_id.TryGetValue(
            activeUnit.unit_id,
            out BattleAiRuntimeActionPlan actionPlan
        );
        return _runtime_services.PrepareAiContextForDecision(
            new BattleAiDecisionContextSetup(
                _state,
                activeUnit,
                _grid_service,
                actionPlan,
                GetSkillDefinitionIndexTyped(),
                _ai_trace_enabled,
                _skillCatalog,
                _ai_move_cost_callback,
                _ai_preview_command_callback,
                _ai_skill_score_input_callback,
                _ai_action_score_input_callback,
                _ai_skill_cast_block_reason_callback
            )
        );
    }

    private BattleAiScoreInput BuildAiSkillScoreInput(
        BattleAiContext context,
        SkillDefinition skillDefinition,
        BattleCommand command,
        BattlePreview preview,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        IReadOnlyDictionary<string, object> metadata
    )
    {
        return _ai_service.GetScoreService()
            .BuildSkillScoreInput(
                context,
                skillDefinition,
                command,
                preview,
                effectDefinitions ?? System.Array.Empty<CombatEffectDefinition>(),
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
        return _runtime_services.BuildActionScoreInput(
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
                using (new BattleAiTraceSpan("advance:ensure_ai_action_plan"))
                {
                    _ensure_ai_action_plan_for_unit(activeUnit);
                }

                BattleAiContext aiContext = null;
                BattleAiDecision decision = null;
                try
                {
                    using (new BattleAiTraceSpan("advance:create_ai_context"))
                    {
                        aiContext = _prepare_ai_context_for_decision(activeUnit);
                    }
                    using (new BattleAiTraceSpan("advance:bind_ai_helpers"))
                    {
                        _bind_ai_helper_services_for_decision(activeUnit, aiContext);
                    }

                    BattleAiDecisionResult decisionResult;
                    using (new BattleAiTraceSpan("advance:choose_command"))
                    {
                        decisionResult = _ai_service.ChooseCommand(aiContext, _ai_trace_enabled);
                    }
                    decision = decisionResult?.Decision;
                    if (decision != null && decision.command != null)
                    {
                        string aiLine =
                            $"AI[{decision.brain_id}/{decision.state_id}/{decision.action_id}] {decision.reason_text}";
                        using (new BattleAiTraceSpan("advance:append_ai_log"))
                        {
                            _state.AppendLogEntry(aiLine);
                        }
                        BattleAiTurnTraceProjection aiTurnTrace = decisionResult?.TurnTrace;
                        Dictionary<StringName, BattleAiTraceUnitSnapshotProjection> snapshotsBefore =
                            new();
                        List<StringName> decisionTargetUnitIds = new();
                        using (new BattleAiTraceSpan("advance:ai_decision_commit"))
                        {
                            if (_ai_trace_enabled && aiTurnTrace != null)
                            {
                                decisionTargetUnitIds = CollectAiTraceDecisionTargetUnitIds(
                                    decision,
                                    aiTurnTrace
                                );
                                snapshotsBefore = BuildAiTraceUnitSnapshotMapTyped();
                                aiTurnTrace.DecisionTargetSnapshots =
                                    BuildAiTraceSnapshotsForUnitIdsTyped(
                                        decisionTargetUnitIds,
                                        snapshotsBefore
                                    );
                            }
                        }
                        BattleEventBatch decisionBatch;
                        using (new BattleAiTraceSpan("advance:issue_ai_command"))
                        {
                            decisionBatch = IssueCommand(decision.command);
                        }
                        using (new BattleAiTraceSpan("advance:ai_trace_after_command"))
                        {
                            if (_ai_trace_enabled && aiTurnTrace != null)
                            {
                                aiTurnTrace.ExecutionResult = BuildAiTraceExecutionResultTyped(
                                    decision,
                                    decisionBatch,
                                    snapshotsBefore,
                                    decisionTargetUnitIds
                                );
                                _ai_turn_traces.Add(aiTurnTrace);
                            }
                            using (new BattleAiTraceSpan("advance:prepend_ai_batch_log"))
                            {
                                if (decisionBatch != null)
                                    decisionBatch.InsertLogLine(0, aiLine);
                            }
                        }
                        return decisionBatch;
                    }
                }
                finally
                {
                    decision?.ClearOwnedRuntimeReferences();
                    _runtime_services.ClearRuntimeBindings();
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
        using BattleAiTraceSpan trace = new("preview:move");
        if (_movement_service.IsMovementBlocked(activeUnit))
        {
            preview.AddLogLine($"{activeUnit.DisplayName} 当前被限制移动。");
            return;
        }

        BattleMovePathResult moveResult;
        using (new BattleAiTraceSpan("preview:move.resolve_path_result"))
        {
            moveResult = _movement_service.ResolveMovePathResultTyped(
                activeUnit,
                command.target_coord
            );
        }

        using (new BattleAiTraceSpan("preview:move.build_preview"))
        {
            ApplyMovePreviewResult(activeUnit, command, preview, moveResult);
        }
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
        using BattleAiTraceSpan trace = new("preview:skill");
        if (activeUnit.TurnCastingExhausted)
        {
            preview.AddLogLine("本次施法准备失败后只能移动、等待或取消读条。");
            return;
        }
        BattleSkillAccessResult accessResult = ValidateSkillCommandEntryAccess(
            command,
            BattleSkillAvailabilityConsumer.PreviewExecution
        );
        if (!accessResult.Allowed)
        {
            preview.AddLogLine(accessResult.Message);
            return;
        }
        _preview_skill_command(activeUnit, command, preview);
    }

    private BattleSkillAccessResult ValidateSkillCommandEntryAccess(
        BattleCommand command,
        BattleSkillAvailabilityConsumer consumer
    )
    {
        if (command == null)
        {
            return BattleSkillAccessResult.Deny("missing_command", "技能命令无效。");
        }
        if (_state == null || !_state.TryGetUnitTyped(command.unit_id, out BattleUnitState unit))
        {
            return BattleSkillAccessResult.Deny("missing_unit", "当前单位无效。");
        }
        BattleSkillAvailabilityService service = new(
            _skillCatalog,
            _skillDefinitionIndex,
            _equipmentAbilityBindingIndex,
            _itemDefIndex
        );
        return service.ValidateSkillEntryAccess(
            new BattleSkillAvailabilityQuery
            {
                User = unit,
                Consumer = consumer,
                IncludeEquipmentSkills = true,
                WorldStep = GetBattleWorldStep(),
                BattleState = _state,
            },
            command.skill_entry_id,
            command.skill_id
        );
    }

    internal int ResolveSkillCommandEntryLevel(
        BattleCommand command,
        BattleSkillAvailabilityConsumer consumer,
        int fallback = 0
    )
    {
        BattleSkillAccessResult accessResult = ValidateSkillCommandEntryAccess(command, consumer);
        return accessResult.Allowed && accessResult.Entry != null
            ? accessResult.Entry.SkillLevel
            : fallback;
    }

    internal int GetBattleWorldStep() =>
        _state?.GetEnvironmentSnapshot()?.WorldStep ?? -1;

    internal bool CommitEquipmentSkillUsageIfNeeded(
        BattleUnitState unit,
        BattleCommand command,
        BattleEventBatch batch = null,
        BattleEquipmentSkillUseOutcome skillOutcome = null
    )
    {
        if (unit == null || command == null)
            return false;
        BattleSkillAvailabilityService service = new(
            _skillCatalog,
            _skillDefinitionIndex,
            _equipmentAbilityBindingIndex,
            _itemDefIndex
        );
        BattleSkillAccessResult accessResult = service.ValidateSkillEntryAccess(
            new BattleSkillAvailabilityQuery
            {
                User = unit,
                Consumer = BattleSkillAvailabilityConsumer.PreviewExecution,
                IncludeKnownSkills = false,
                IncludeEquipmentSkills = true,
                WorldStep = GetBattleWorldStep(),
                BattleState = _state,
            },
            command.skill_entry_id,
            command.skill_id
        );
        if (!accessResult.Allowed)
            return false;

        bool committed = EquipmentAbilityUsageRuntime.TryCommitUsage(
            unit,
            accessResult.Entry,
            GetBattleWorldStep()
        );
        bool triggered = _equipment_ability_runtime_service?.ResolveGrantedSkillUsed(
            new BattleEquipmentAbilityGrantedSkillUsedContext
            {
                SourceUnit = unit,
                TargetUnit = ResolveCommandPrimaryTargetUnit(command),
                BattleState = _state,
                Batch = batch,
                BindingId = accessResult.Entry.EquipmentBindingId,
                GrantedActionId = accessResult.Entry.EquipmentGrantedActionId,
                SkillId = accessResult.Entry.EntryRef.SkillId,
                SkillEntryId = accessResult.Entry.EntryRef.SkillEntryId,
                SkillOutcome = skillOutcome ?? BattleEquipmentSkillUseOutcome.Empty,
            }
        ) == true;
        if (committed || triggered)
            batch?.AddChangedUnitId(unit.unit_id);
        return committed || triggered;
    }

    private BattleUnitState ResolveCommandPrimaryTargetUnit(BattleCommand command)
    {
        if (_state == null || command == null)
            return null;
        StringName targetUnitId = ProgressionDataUtils.to_string_name(command.target_unit_id);
        if (targetUnitId != "" && _state.TryGetUnitTyped(targetUnitId, out BattleUnitState target))
            return target;
        foreach (StringName candidateId in command.TargetUnitIdsTyped ?? Array.Empty<StringName>())
        {
            targetUnitId = ProgressionDataUtils.to_string_name(candidateId);
            if (targetUnitId != "" && _state.TryGetUnitTyped(targetUnitId, out target))
                return target;
        }
        return null;
    }

    private static void PreviewWaitCommand(BattleUnitReadView activeUnit, BattlePreview preview)
    {
        using BattleAiTraceSpan trace = new("preview:wait");
        preview.allowed = true;
        preview.AddLogLine($"{activeUnit.DisplayName} 可以结束行动。");
    }

    private void PreviewChangeEquipmentCommand(
        BattleUnitReadView activeUnit,
        BattleCommand command,
        BattlePreview preview
    )
    {
        using BattleAiTraceSpan trace = new("preview:change_equipment");
        if (activeUnit.TurnCastingExhausted)
        {
            preview.AddLogLine("本次施法准备失败后只能移动、等待或取消读条。");
            return;
        }
        _preview_change_equipment_command(activeUnit, command, preview);
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
        try
        {
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
        finally
        {
            DisposeBattlePreview(preview);
        }
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
            IReadOnlyDictionary<string, object> reportEntry = batch.ReportEntriesTyped[i];
            if (reportEntry.Count > 0)
                _state.AddReportEntry(reportEntry);
        }
    }

    internal void AppendResultReportEntry(
        BattleEventBatch batch,
        AttackEffectResolutionResult result
    )
    {
        if (batch == null)
            return;
        IReadOnlyDictionary<string, object> reportEntry = result.HasReportEntry
            ? BattleReportEntryPayload.BuildPlainPayload(result.ReportEntry)
            : BuildAutoCastEffectResultReport(result);
        if (reportEntry.Count > 0)
            _append_report_entry_to_batch(batch, reportEntry);
    }

    private IReadOnlyDictionary<string, object> BuildAutoCastEffectResultReport(
        AttackEffectResolutionResult result
    )
    {
        BattleEffectOrigin origin = CurrentEffectOrigin;
        if (
            origin?.OriginKind != new StringName("contingency_auto_cast")
            || !result.Applied
        )
            return new Dictionary<string, object>(StringComparer.Ordinal);
        Dictionary<string, object> payload = AttackEffectResolutionPlainPayload.Build(result);
        payload["entry_kind"] = "effect_result";
        return payload;
    }

    internal void _append_report_entry_to_batch(
        BattleEventBatch batch,
        IReadOnlyDictionary<string, object> report_entry
    )
    {
        if (batch == null || report_entry == null || report_entry.Count == 0)
            return;
        var detachedReportEntry = new Dictionary<string, object>(
            report_entry,
            StringComparer.Ordinal
        );
        AttachCurrentEffectOrigin(detachedReportEntry);
        batch.AddReportEntry(detachedReportEntry);
        string entryText =
            detachedReportEntry.TryGetValue("text", out object textValue)
                ? textValue?.ToString()?.StripEdges() ?? ""
                : "";
        if (!string.IsNullOrEmpty(entryText))
            batch.AddLogLine(entryText);
    }

    private IDisposable PushEffectOrigin(BattleEffectOrigin origin)
    {
        _effectOriginStack.Push(origin ?? BattleEffectOrigin.PlayerCommand());
        return new EffectOriginScope(this);
    }

    private BattleEffectOrigin CurrentEffectOrigin =>
        _effectOriginStack.Count > 0 ? _effectOriginStack.Peek() : BattleEffectOrigin.PlayerCommand();

    private void AttachCurrentEffectOrigin(Dictionary<string, object> reportEntry)
    {
        if (reportEntry == null || reportEntry.Count == 0)
            return;
        reportEntry["effect_origin"] = CurrentEffectOrigin.ToPlainDictionary();
    }

    private void PopEffectOrigin()
    {
        if (_effectOriginStack.Count > 0)
            _effectOriginStack.Pop();
    }

    private sealed class EffectOriginScope : IDisposable
    {
        private BattleRuntimeModule _runtime;

        internal EffectOriginScope(BattleRuntimeModule runtime)
        {
            _runtime = runtime;
        }

        public void Dispose()
        {
            _runtime?.PopEffectOrigin();
            _runtime = null;
        }
    }

    public BattleEventBatch SubmitPromotionChoice(
        StringName member_id,
        StringName profession_id,
        PromotionSelectionData selection
    )
    {
        _ensure_sidecars_ready();
        BattleEventBatch batch = _new_batch();
        if (_state == null || _characterGateway == null)
            return batch;
        CharacterProgressionDelta delta = _characterGateway.PromoteProfession(
            member_id,
            profession_id,
            selection ?? PromotionSelectionData.Empty
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

    internal void SetupStateForTests(BattleState state)
    {
        if (state == null)
            _contingency_system.ClearBattleState();
        if (!ReferenceEquals(_state, state))
            _delayed_area_effect_system.Clear();
        _state = state;
        if (_state != null)
        {
            _ensure_sidecars_ready();
            _contingency_system.ResetForBattle(_characterGateway?.GetPartyState(), _state);
        }
    }

    internal BattleContingencySystem GetContingencySystemTyped()
    {
        _ensure_sidecars_ready();
        return _contingency_system;
    }

    internal BattleEffectOrigin CurrentEffectOriginForContingency =>
        CurrentEffectOrigin ?? BattleEffectOrigin.PlayerCommand();

    internal StringName AllocateContingencySourceEventId(StringName prefix)
    {
        _contingencySourceEventOrdinal += 1;
        string normalizedPrefix = ProgressionDataUtils.to_string_name(prefix).ToString();
        if (string.IsNullOrEmpty(normalizedPrefix))
            normalizedPrefix = "battle_fact";
        return new StringName($"{normalizedPrefix}:{_contingencySourceEventOrdinal}");
    }

    internal void EmitContingencyHpAndStatusHooks(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        int previousHp,
        IReadOnlyList<StringName> statusIds,
        StringName sourceEventId
    )
    {
        _ensure_sidecars_ready();
        if (targetUnit == null)
            return;
        BattleEffectOrigin origin = CurrentEffectOriginForContingency;
        Vector2I sourceCell = sourceUnit?.coord ?? new Vector2I(-1, -1);
        Vector2I targetCell = targetUnit.coord;
        int maxHp = Math.Max(
            targetUnit.attribute_snapshot?.GetValue(AttributeService.HP_MAX) ?? targetUnit.current_hp,
            1
        );
        if (targetUnit.current_hp != previousHp)
        {
            _contingency_system.OnHookFact(
                ContingencyHookFact.HpChanged(
                    sourceEventId,
                    sourceUnit?.unit_id ?? "",
                    targetUnit.unit_id,
                    previousHp,
                    targetUnit.current_hp,
                    maxHp,
                    origin,
                    sourceCell,
                    targetCell
                )
            );
        }
        if (statusIds != null && statusIds.Count > 0)
        {
            _contingency_system.OnHookFact(
                ContingencyHookFact.StatusApplied(
                    sourceEventId,
                    sourceUnit?.unit_id ?? "",
                    targetUnit.unit_id,
                    statusIds,
                    origin,
                    sourceCell,
                    targetCell
                )
            );
        }
    }

    internal void EmitContingencySpellAffected(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        IReadOnlyList<StringName> affectedUnitIds,
        StringName sourceEventId,
        IReadOnlyList<Vector2I> areaCells = null
    )
    {
        _ensure_sidecars_ready();
        _contingency_system.OnHookFact(
            ContingencyHookFact.SpellAffected(
                sourceEventId,
                sourceUnit?.unit_id ?? "",
                targetUnit?.unit_id ?? "",
                affectedUnitIds ?? Array.Empty<StringName>(),
                CurrentEffectOriginForContingency,
                sourceUnit?.coord ?? new Vector2I(-1, -1),
                targetUnit?.coord ?? new Vector2I(-1, -1),
                areaCells
            )
        );
    }

    internal void EmitContingencyPositionChanged(
        BattleUnitState unitState,
        Vector2I previousCoord,
        Vector2I currentCoord,
        StringName sourceEventId
    )
    {
        _ensure_sidecars_ready();
        if (unitState == null || previousCoord == currentCoord)
            return;
        _contingency_system.OnHookFact(
            ContingencyHookFact.PositionChanged(
                sourceEventId,
                unitState.unit_id,
                previousCoord,
                currentCoord,
                CurrentEffectOriginForContingency
            )
        );
    }

    internal bool ExecuteAutoCast(AutoCastRequest request, BattleEventBatch batch)
    {
        _ensure_sidecars_ready();
        if (request?.IsValid != true || _state == null)
            return false;
        if (!IsContingencyAutoCastSourcePlayerLearned(request))
            return false;
        using IDisposable originScope = PushEffectOrigin(BattleEffectOrigin.AutoCast(request));
        return _skill_orchestrator.ExecuteAutoCast(request, batch ?? _new_batch());
    }

    internal bool IsContingencyAutoCastSourcePlayerLearned(AutoCastRequest request)
    {
        if (request == null || request.OwnerMemberId == "" || request.SourceSkillId == "")
            return false;
        PartyMemberState memberState = _characterGateway
            ?.GetPartyState()
            ?.GetMemberState(request.OwnerMemberId);
        UnitSkillProgress progress = memberState?.progression?.GetSkillProgress(
            request.SourceSkillId
        );
        return progress != null
            && progress.is_learned
            && progress.skill_level > 0
            && progress.GrantedSourceTypeKind == UnitSkillGrantSourceType.Player;
    }

    internal IReadOnlyList<ContingencyTargetResolutionResult> ResolveContingencyStoredSpellTargetsForRelease(
        ContingencyReleaseContext context,
        ContingencyFrozenTriggerFacts facts
    )
    {
        _ensure_sidecars_ready();
        return _contingency_system.ResolveStoredSpellTargetsForRelease(context, facts);
    }

    internal void OnBattleConfirmed(BattleEventBatch batch = null)
    {
        _ensure_sidecars_ready();
        BattleEventBatch targetBatch = batch ?? _new_batch();
        _contingency_system.OnBattleConfirmed(targetBatch);
        _contingency_system.ExecuteQueuedReleaseContexts(
            ContingencyFrozenTriggerFacts.Empty,
            targetBatch
        );
        if (batch == null)
            _append_batch_logs_to_state(targetBatch);
    }

    internal void OnOwnerTurnStarted(BattleUnitState ownerUnit, BattleEventBatch batch = null)
    {
        _ensure_sidecars_ready();
        _contingency_system.OnOwnerTurnStarted(ownerUnit, batch);
    }

    internal int ExecuteQueuedContingencyReleases(
        ContingencyFrozenTriggerFacts facts,
        BattleEventBatch batch
    )
    {
        _ensure_sidecars_ready();
        return _contingency_system.ExecuteQueuedReleaseContexts(
            facts ?? ContingencyFrozenTriggerFacts.Empty,
            batch
        );
    }

    internal int ExecuteNextSequentialContingencyAutoCastForOwner(
        BattleUnitState ownerUnit,
        BattleEventBatch batch
    )
    {
        _ensure_sidecars_ready();
        return _contingency_system.ExecuteNextSequentialAutoCastForOwner(
            ownerUnit?.unit_id ?? "",
            batch
        );
    }

    internal void RefreshBattleUnitForContingencyOverlay(BattleUnitState unitState)
    {
        if (unitState == null)
            return;
        _ensure_sidecars_ready();
        _unit_factory.RefreshBattleUnit(unitState);
    }

    internal IReadOnlyDictionary<StringName, int> GetCalamityByMemberIdSnapshot() =>
        _fate_runtime != null
            ? _fate_runtime.GetCalamityByMemberIdSnapshot()
            : calamity_by_member_id.Snapshot();

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
        SkillDefinition skillDefinition
    ) =>
        _skill_turn_resolver.GetSkillCastBlockReason(active_unit, skillDefinition);

    internal string GetSkillCastBlockMessage(
        BattleUnitState active_unit,
        SkillDefinition skillDefinition
    ) =>
        _skill_turn_resolver.FormatSkillCastBlockReason(
            active_unit,
            skillDefinition,
            skillDefinition != null
                ? GetSkillCastBlockReason(active_unit, skillDefinition)
                : BattleSkillCastBlockReasonKind.InvalidSkillOrTarget
        );

    internal string GetSkillCastBlockMessage(BattleUnitState active_unit, StringName skill_id)
    {
        SkillDefinition skillDefinition = GetSkillDefinitionTyped(skill_id);
        return _skill_turn_resolver.FormatSkillCastBlockReason(
            active_unit,
            skillDefinition,
            skillDefinition != null
                ? GetSkillCastBlockReason(active_unit, skillDefinition)
                : BattleSkillCastBlockReasonKind.InvalidSkillOrTarget
        );
    }

    public bool IsUnitGuardLocked(BattleUnitState unit_state) =>
        _has_status(unit_state, STATUS_BLACK_STAR_BRAND_NORMAL)
        || _skill_turn_resolver.HasGuardLockStatus(unit_state);

    public bool IsUnitCounterattackLocked(BattleUnitState unit_state) =>
        _has_status(unit_state, STATUS_BLACK_STAR_BRAND_NORMAL)
        || _has_status(unit_state, STATUS_CROWN_BREAK_BROKEN_HAND)
        || _skill_turn_resolver.HasCounterattackLockStatus(unit_state);

    public bool IsUnitFollowUpLocked(BattleUnitState unit_state) =>
        _has_status(unit_state, STATUS_CROWN_BREAK_BROKEN_HAND);

    internal void _ensure_sidecars_ready()
    {
        _terrain_effect_system.Setup(this);
        _delayed_area_effect_system.Setup(this);
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
        _runtime_services.SetupRuntimeSidecars(this);
        _layered_barrier_service.Setup(this);
        _timeline_driver.Setup(this);
        _skill_orchestrator.Setup(this);
        _casting_time_service.Setup(this);
        _contingency_system.Setup(this);
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

    internal BattleEndResult EndBattle(BattleEndOptions options)
    {
        if (_state == null)
            return BattleEndResult.Success();
        if (_characterGateway != null && options.CommitProgression)
        {
            // Phase 1: validate every commit read-only before mutating any member,
            // so a mid-loop failure cannot leave earlier members half-committed.
            foreach (StringName allyUnitId in _state.ally_unit_ids)
            {
                _state.TryGetUnitTyped(allyUnitId, out BattleUnitState unitState);
                if (unitState == null || !unitState.is_alive)
                    continue;
                ContingencyConsumedCommitResult validationResult =
                    ValidateContingencyConsumedSetupsForBattleUnit(unitState);
                if (!validationResult.Ok)
                    return BattleEndResult.ContingencyConsumedFailure(validationResult);
            }
            foreach (StringName allyUnitId in _state.ally_unit_ids)
            {
                _state.TryGetUnitTyped(allyUnitId, out BattleUnitState unitState);
                if (unitState == null)
                    continue;
                if (unitState.is_alive)
                {
                    ContingencyConsumedCommitResult contingencyResult =
                        CommitContingencyConsumedSetupsForBattleUnit(unitState);
                    if (!contingencyResult.Ok)
                        return BattleEndResult.ContingencyConsumedFailure(contingencyResult);
                    BattleResourceCommitResult resourceResult =
                        _characterGateway.CommitBattleResources(
                        unitState.source_member_id,
                        unitState.current_hp,
                        unitState.current_mp,
                        unitState.current_aura
                    );
                    if (resourceResult == null)
                        return BattleEndResult.ResourceCommitFailure(
                            BattleResourceCommitResult.Failure(
                                "battle_resource_commit_missing_result",
                                unitState.source_member_id
                            )
                        );
                    if (!resourceResult.Ok)
                        return BattleEndResult.ResourceCommitFailure(resourceResult);
                }
                else
                    _characterGateway.CommitBattleDeath(unitState.source_member_id);
            }
            int flushError = _characterGateway.FlushAfterBattle();
            if (flushError != (int)Error.Ok)
                return BattleEndResult.FlushFailure(flushError);
        }
        if (
            _battle_resolution_result == null
            && !_battle_resolution_result_consumed
            && _state.PhaseKind == BattlePhaseKind.BattleEnded
        )
            _battle_resolution_result = _build_battle_resolution_result();
        return BattleEndResult.Success();
    }

    private ContingencyConsumedCommitResult ValidateContingencyConsumedSetupsForBattleUnit(
        BattleUnitState unitState
    )
    {
        IReadOnlyList<StringName> consumedSetupIds =
            unitState?.GetConsumedContingencySetupIdsTyped() ?? Array.Empty<StringName>();
        if (consumedSetupIds.Count == 0)
            return ContingencyConsumedCommitResult.Success(
                unitState?.source_member_id ?? "",
                0
            );
        return _characterGateway.ValidateContingencyConsumedSetups(
            unitState.source_member_id,
            consumedSetupIds
        );
    }

    private ContingencyConsumedCommitResult CommitContingencyConsumedSetupsForBattleUnit(
        BattleUnitState unitState
    )
    {
        IReadOnlyList<StringName> consumedSetupIds =
            unitState?.GetConsumedContingencySetupIdsTyped() ?? Array.Empty<StringName>();
        if (consumedSetupIds.Count == 0)
            return ContingencyConsumedCommitResult.Success(
                unitState?.source_member_id ?? "",
                0
            );
        return _characterGateway.CommitContingencyConsumedSetups(
            unitState.source_member_id,
            consumedSetupIds
        );
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
        _runtime_services.SetupRuntimeSidecars(this);
        _layered_barrier_service.Setup(this);
        _timeline_driver.Setup(this);
        _skill_orchestrator.Setup(this);
        _casting_time_service.Setup(this);
        _equipment_ability_runtime_service ??= new BattleEquipmentAbilityRuntimeService();
        _equipment_ability_runtime_service.Setup(this, _damage_resolver);
        _damage_resolver?.SetEquipmentAbilityRuntimeService(_equipment_ability_runtime_service);
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

    internal BattleEquipmentAbilityRuntimeService GetEquipmentAbilityRuntimeService()
    {
        _equipment_ability_runtime_service ??= new BattleEquipmentAbilityRuntimeService();
        _equipment_ability_runtime_service.Setup(this, _damage_resolver);
        _damage_resolver?.SetEquipmentAbilityRuntimeService(_equipment_ability_runtime_service);
        return _equipment_ability_runtime_service;
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

    internal void ConfigureOwnedTerrainGeneratorForTests(BattleTerrainGenerator terrainGenerator)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        SetTerrainGenerator(terrainGenerator, true);
    }

    internal bool HasAiRuntimeBorrowers =>
        _ai_action_plans_by_unit_id.Count != 0 || _runtime_services.HasAiRuntimeBindings;

    internal bool HasRuntimeSidecarBindings => _runtime_services.HasRuntimeSidecarBindings;

    internal bool IsDisposed => _disposed;

    private BattleTerrainGenerator EnsureTerrainGenerator()
    {
        if (_disposed)
            return _terrainGenerator;
        if (_terrainGenerator == null)
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
        if (shouldDispose && terrainGenerator != null)
            terrainGenerator.Dispose();
    }

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

    internal void AppendReportEntry(
        BattleEventBatch batch,
        IReadOnlyDictionary<string, object> report_entry
    ) =>
        _append_report_entry_to_batch(batch, report_entry);

    internal void ClearDefeatedUnit(BattleUnitState unit_state, BattleEventBatch batch = null) =>
        _clear_defeated_unit(unit_state, batch);

    internal GVector2IArray sort_coords(GArray target_coords) => _sort_coords(target_coords);

    internal GVector2IArray sort_coords(GVector2IArray target_coords) => _sort_coords(target_coords);

    internal void MarkAppliedStatusesForTurnTiming(
        BattleUnitState target_unit,
        GArray status_effect_ids
    )
    {
        _initialize_applied_status_timeline_ticks(target_unit, status_effect_ids);
        _fate_runtime?.HandleAppliedStatuses(target_unit, status_effect_ids);
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
        if (statusEffectIds == null)
            return normalized;
        foreach (var statusIdValue in statusEffectIds)
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
        if (statusEffectIds == null)
            return normalized;
        foreach (StringName statusIdValue in statusEffectIds)
        {
            StringName statusId = ProgressionDataUtils.to_string_name(statusIdValue);
            if (statusId == "" || normalized.Contains(statusId))
                continue;
            normalized.Add(statusId);
        }
        return normalized;
    }

    internal bool IsUnitValidForEffect(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        StringName target_filter
    ) => _is_unit_valid_for_effect(source_unit, target_unit, target_filter);

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
        using GArray sourceStatusValues = GetArray(result, "source_status_effect_ids");
        GStringNameArray sourceStatusIds = NormalizeStatusIdArray(sourceStatusValues);
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

    internal void _record_turn_started(BattleUnitState unit_state, BattleEventBatch batch = null)
    {
        _ensure_sidecars_ready();
        _metrics_collector.RecordTurnStarted(unit_state);
        _contingency_system.OnOwnerTurnStarted(unit_state, batch);
        if (batch == null)
            return;
        _contingency_system.ExecuteQueuedReleaseContexts(
            new ContingencyFrozenTriggerFacts
            {
                TriggerSourceUnitId = unit_state?.unit_id ?? "",
                TriggerTargetUnitId = unit_state?.unit_id ?? "",
                TriggerSourceCell = unit_state?.coord ?? new Vector2I(-1, -1),
                TriggerTargetCell = unit_state?.coord ?? new Vector2I(-1, -1),
                TriggerCell = unit_state?.coord ?? new Vector2I(-1, -1),
            },
            batch
        );
        _contingency_system.ExecuteNextSequentialAutoCastForOwner(
            unit_state?.unit_id ?? "",
            batch
        );
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

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        DisposeManagedRuntime();
    }

    public void dispose()
    {
        Dispose();
    }

    private void DisposeManagedRuntime()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        Exception firstFailure = null;

        // Phase 1: release the most-derived decision borrowers before any service,
        // content catalog, state graph, or owned native resource can disappear.
        RunTeardownStep(ref firstFailure, _runtime_services.ClearRuntimeBindings);
        RunTeardownStep(ref firstFailure, ClearAiActionPlans);
        RunTeardownStep(ref firstFailure, _ai_turn_traces.Clear);
        RunTeardownStep(ref firstFailure, _contingency_system.ClearBattleState);

        // Phase 2: dispose AI and runtime sidecars while their borrowed inputs still exist.
        RunTeardownStep(ref firstFailure, () => _ai_service?.Dispose());
        RunTeardownStep(ref firstFailure, _runtime_services.Dispose);
        RunTeardownStep(ref firstFailure, () => _terrain_effect_system?.Dispose());
        RunTeardownStep(ref firstFailure, () => _delayed_area_effect_system?.Dispose());
        RunTeardownStep(ref firstFailure, () => _battle_rating_system?.DisposeRuntime());
        RunTeardownStep(ref firstFailure, () => _unit_factory?.DisposeRuntime());
        RunTeardownStep(ref firstFailure, () => _charge_resolver?.DisposeRuntime());
        RunTeardownStep(ref firstFailure, () => _repeat_attack_resolver?.DisposeRuntime());
        RunTeardownStep(ref firstFailure, () => _skill_resolution_rules?.Dispose());
        RunTeardownStep(ref firstFailure, () => _change_equipment_resolver?.Dispose());
        RunTeardownStep(ref firstFailure, () => _loot_resolver?.Dispose());
        RunTeardownStep(ref firstFailure, () => _skill_turn_resolver?.DisposeRuntime());
        RunTeardownStep(ref firstFailure, () => _metrics_collector?.Dispose());
        RunTeardownStep(ref firstFailure, () => _shield_service?.DisposeRuntime());
        RunTeardownStep(ref firstFailure, () => _layered_barrier_service?.Dispose());
        RunTeardownStep(ref firstFailure, () => _timeline_driver?.Dispose());
        RunTeardownStep(ref firstFailure, () => _skill_orchestrator?.DisposeRuntime());
        RunTeardownStep(ref firstFailure, () => _casting_time_service?.Dispose());
        RunTeardownStep(ref firstFailure, () => _meteor_swarm_resolver?.Dispose());
        RunTeardownStep(ref firstFailure, () => _attack_check_policy_service?.Dispose());
        RunTeardownStep(ref firstFailure, () => _equipment_ability_runtime_service?.Dispose());
        RunTeardownStep(ref firstFailure, () => _skill_outcome_committer?.Dispose());
        RunTeardownStep(ref firstFailure, () => _skill_mastery_service?.Dispose());
        RunTeardownStep(ref firstFailure, () => _fate_runtime?.DisposeRuntime());
        RunTeardownStep(ref firstFailure, () => _damage_resolver?.Dispose());
        RunTeardownStep(ref firstFailure, () => _hit_resolver?.Dispose());

        _meteor_swarm_resolver = null;
        _attack_check_policy_service = null;
        _equipment_ability_runtime_service = null;
        _skill_outcome_committer = null;
        _damage_resolver = null;
        _hit_resolver = null;

        // Phase 3: clear plain runtime results and every process-content borrower/index.
        _battleRatingStatsByMemberId.Clear();
        _pendingPostBattleCharacterRewards.Clear();
        _active_loot_entries.Clear();
        _looted_defeated_unit_ids.Clear();
        _effectOriginStack.Clear();
        _battle_metrics.Clear();
        calamity_by_member_id.Clear();
        _battle_resolution_result = null;
        _battle_resolution_result_consumed = false;
        _terrain_effect_nonce = 0;
        _ai_trace_enabled = false;
        _last_start_failure = new BattleStartFailureSnapshot();
        RunTeardownStep(ref firstFailure, ClearContentCatalogBorrowers);

        // Phase 4: release state/topology after all of its borrowers are gone.
        RunTeardownStep(ref firstFailure, ClearRuntimeBattleStateReference);

        // Phase 5: owned battle-native resources close last. DisposeOwnedTerrainGenerator
        // drops the field before invoking user-overridable Dispose, so an exception cannot
        // resurrect or retain the closed owner on a second Dispose call.
        RunTeardownStep(ref firstFailure, DisposeOwnedTerrainGenerator);

        if (firstFailure != null)
        {
            ExceptionDispatchInfo.Capture(firstFailure).Throw();
        }
    }

    private static void RunTeardownStep(ref Exception firstFailure, Action action)
    {
        try
        {
            action?.Invoke();
        }
        catch (Exception exception)
        {
            firstFailure ??= exception;
        }
    }

    private void ClearRuntimeBattleStateReference()
    {
        BattleState state = _state;
        _state = null;
        if (state != null)
        {
            Exception firstFailure = null;
            RunTeardownStep(ref firstFailure, state.ClearBattleTopology);
            RunTeardownStep(ref firstFailure, state.ally_unit_ids.Clear);
            RunTeardownStep(ref firstFailure, state.enemy_unit_ids.Clear);
            RunTeardownStep(ref firstFailure, () => state.timeline?.ready_unit_ids.Clear());
            if (firstFailure != null)
            {
                ExceptionDispatchInfo.Capture(firstFailure).Throw();
            }
        }
    }

    private static void DisposeBattlePreview(BattlePreview preview)
    {
        if (preview == null)
            return;
        preview.hit_preview = null;
    }

    internal bool _place_units(
        GArray units,
        GArray spawn_coords,
        bool is_ally,
        StringName spawn_side = default
    ) => PlaceUnitsTyped(
        ToBattleUnitArray(units),
        ToVector2IList(spawn_coords),
        is_ally,
        spawn_side
    );

    internal bool PlaceUnitsForTestsTyped(
        IReadOnlyList<BattleUnitState> units,
        IReadOnlyList<Vector2I> spawnCoords,
        bool isAlly,
        StringName spawnSide = default
    ) => PlaceUnitsTyped(units, spawnCoords, isAlly, spawnSide);

    private bool PlaceUnitsTyped(
        IReadOnlyList<BattleUnitState> units,
        IReadOnlyList<Vector2I> spawnCoordValues,
        bool is_ally,
        StringName spawn_side = default
    )
    {
        units ??= Array.Empty<BattleUnitState>();
        spawnCoordValues ??= Array.Empty<Vector2I>();
        var placedUnits = new GBattleUnitArray();
        for (int index = 0; index < units.Count; index++)
        {
            BattleUnitState unitState = units[index];
            if (unitState == null)
                continue;
            unitState.RefreshFootprint();
            var preferredCoords = new List<Vector2I>();
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
        IReadOnlyList<Vector2I> preferred_coords,
        StringName spawn_side = default
    )
    {
        if (_state == null || unit_state == null)
            return new Vector2I(-1, -1);
        preferred_coords ??= Array.Empty<Vector2I>();
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
        IReadOnlyList<Vector2I> preferred_coords
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
        foreach (Vector2I coord in ToVector2IList(spawn_coords))
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

    internal BattleUnitSkillTargetAffordance GetUnitSkillTargetAffordance(
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant = null,
        bool require_ap = true
    )
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator.GetUnitSkillTargetAffordance(
            active_unit,
            target_unit,
            skillDefinition,
            castVariant,
            require_ap
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

    internal List<Vector2I> _get_line_coords(Vector2I from, Vector2I to)
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
        SkillDefinition skillDefinition,
        IEnumerable<CombatEffectDefinition> effectDefinitions,
        BattleEventBatch batch
    )
    {
        _ensure_sidecars_ready();
        _special_skill_resolver.ApplyOnKillGainResourcesEffects(
            source_unit,
            defeated_unit,
            skillDefinition,
            effectDefinitions ?? Array.Empty<CombatEffectDefinition>(),
            batch
        );
    }

    public BattleSpecialSkillResult ApplyUnitSkillSpecialEffectsResult(
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDefinition skill_definition,
        CombatCastVariantDefinition cast_variant,
        IEnumerable<CombatEffectDefinition> effect_definitions,
        BattleEventBatch batch,
        BattleForcedMoveContext forced_move_context = default
    )
    {
        _ensure_sidecars_ready();
        return _special_skill_resolver.ApplyUnitSkillSpecialEffectsResult(
            active_unit,
            target_unit,
            skill_definition,
            cast_variant,
            effect_definitions ?? Array.Empty<CombatEffectDefinition>(),
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
        bool lock_guard = false,
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
            lock_guard,
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
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant = null
    )
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._get_unit_skill_target_validation_message(
            active_unit,
            target_unit,
            skillDefinition,
            castVariant
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
        SkillDefinition skillDefinition,
        AttackEffectResolutionResult result,
        BattleEventBatch batch = null
    )
    {
        _ensure_sidecars_ready();
        _special_skill_resolver.RecordVajraBodyMasteryFromIncomingDamageTyped(
            sourceUnit,
            targetUnit,
            skillDefinition,
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

    internal BattleSpellControlResult _resolve_ground_spell_control_after_cost_result(
        BattleUnitState active_unit,
        SkillDefinition skillDefinition,
        int spent_mp,
        BattleEventBatch batch
    )
    {
        _ensure_sidecars_ready();
        return _ground_effect_service.ResolveGroundSpellControlAfterCostResult(
            active_unit,
            skillDefinition,
            spent_mp,
            batch
        );
    }

    internal BattleSpellControlResult _resolve_unit_spell_control_after_cost_result(
        BattleUnitState active_unit,
        SkillDefinition skillDefinition,
        BattleEventBatch batch
    )
    {
        _ensure_sidecars_ready();
        return _ground_effect_service.ResolveUnitSpellControlAfterCostResult(
            active_unit,
            skillDefinition,
            batch
        );
    }

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
            _collect_defeated_unit_loot(
                unit_state,
                source_unit,
                batch,
                options.KillProvenance
            );
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
        List<Vector2I> previousCoords = new(unit_state.occupied_coords);
        unit_state.MarkDead();
        _clear_equipment_target_marks_for_defeated_unit(unit_state, batch);
        _grid_service.ClearUnitOccupancy(_state, unit_state);
        _append_changed_coords_typed(batch, previousCoords);
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
        _clear_equipment_target_marks_for_defeated_unit(unit_state, batch);
        _handle_adjacent_ally_defeat(unit_state);
        _handle_low_luck_relic_ally_defeat(unit_state, batch);
        List<Vector2I> previousCoords = new(unit_state.occupied_coords);
        _grid_service.ClearUnitOccupancy(_state, unit_state);
        _append_changed_coords_typed(batch, previousCoords);
        _append_changed_unit_id(batch, unit_state.unit_id);
    }

    private void _clear_equipment_target_marks_for_defeated_unit(
        BattleUnitState unit_state,
        BattleEventBatch batch
    )
    {
        IReadOnlyList<StringName> changedUnitIds =
            _equipment_ability_runtime_service?.ClearTargetMarksForDefeatedUnit(_state, unit_state)
            ?? Array.Empty<StringName>();
        foreach (StringName changedUnitId in changedUnitIds)
            _append_changed_unit_id(batch, changedUnitId);
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
        foreach (
            IReadOnlyDictionary<string, object> reportEntry in
            source_batch.ReportEntriesTyped
        )
        {
            target_batch.AddReportEntry(reportEntry);
        }
    }

    internal GVector2IArray _sort_coords(GArray target_coords)
    {
        var coords = ToVector2IList(target_coords);
        coords.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
        return new Vector2IList(coords).ToGodotArray();
    }

    internal GVector2IArray _sort_coords(GVector2IArray target_coords)
    {
        var coords = new List<Vector2I>(
            target_coords != null
                ? (IEnumerable<Vector2I>)target_coords
                : Array.Empty<Vector2I>()
        );
        coords.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
        return new Vector2IList(coords).ToGodotArray();
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
        var coords = new List<Vector2I>();
        if (values == null)
        {
            return new Vector2IList(coords).ToGodotArray();
        }
        foreach (Variant rawCoord in values.Keys)
        {
            coords.Add(rawCoord.AsVector2I());
        }
        return new Vector2IList(coords).ToGodotArray();
    }

    internal int _get_unit_skill_level(BattleUnitState unit_state, StringName skill_id)
    {
        _ensure_sidecars_ready();
        return _skill_orchestrator._get_unit_skill_level(unit_state, skill_id);
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
        SkillDefinition skillDefinition
    ) =>
        _skill_turn_resolver.GetSkillCastBlockReason(active_unit, skillDefinition);

    internal string _get_skill_cast_block_message(
        BattleUnitState active_unit,
        SkillDefinition skillDefinition
    ) =>
        _skill_turn_resolver.FormatSkillCastBlockReason(
            active_unit,
            skillDefinition,
            _get_skill_cast_block_reason(active_unit, skillDefinition)
        );

    internal bool _unit_has_melee_weapon(BattleUnitState active_unit) =>
        _skill_turn_resolver.UnitHasMeleeWeapon(active_unit);

    internal bool _requires_melee_weapon(SkillDefinition skillDefinition) =>
        _skill_turn_resolver.RequiresMeleeWeapon(skillDefinition);

    internal string _get_skill_command_block_reason(
        BattleUnitState active_unit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant
    ) => _skill_turn_resolver.GetSkillCommandBlockReason(active_unit, skillDefinition, castVariant);

    internal string _get_skill_command_block_reason(
        BattleUnitReadView active_unit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant
    ) => _skill_turn_resolver.GetSkillCommandBlockReason(active_unit, skillDefinition, castVariant);

    internal bool _consume_skill_costs(
        BattleUnitState active_unit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant = null,
        BattleEventBatch batch = null
    ) => _skill_turn_resolver.ConsumeSkillCosts(active_unit, skillDefinition, castVariant, batch);

    internal CombatSkillResourceCosts _get_effective_skill_resource_costs(
        BattleUnitState active_unit,
        SkillDefinition skillDefinition
    ) => _skill_turn_resolver.GetEffectiveSkillResourceCosts(active_unit, skillDefinition);

    internal string _get_black_contract_push_variant_block_reason(
        BattleUnitState active_unit,
        CombatCastVariantDefinition castVariant
    )
    {
        BattleSkillCastBlockReasonKind blockReason =
            _skill_turn_resolver.GetBlackContractPushVariantBlockReason(
            active_unit,
            castVariant
        );
        return _skill_turn_resolver.FormatSkillCastBlockReason(
            active_unit,
            null,
            blockReason,
            castVariant
        );
    }

    internal bool _consume_black_contract_push_cast(
        BattleUnitState active_unit,
        CombatCastVariantDefinition castVariant,
        BattleEventBatch batch = null
    ) => _skill_turn_resolver.ConsumeBlackContractPushCast(active_unit, castVariant, batch);

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

    internal int _get_effective_skill_range(
        BattleUnitState active_unit,
        SkillDefinition skillDefinition
    ) =>
        _skill_turn_resolver.GetEffectiveSkillRange(active_unit, skillDefinition);

    internal int _get_effective_skill_range(
        BattleUnitReadView active_unit,
        SkillDefinition skillDefinition
    ) =>
        _skill_turn_resolver.GetEffectiveSkillRange(active_unit, skillDefinition);

    internal int _resolve_base_skill_range(
        BattleUnitState active_unit,
        SkillDefinition skillDefinition
    ) =>
        _skill_turn_resolver.ResolveBaseSkillRange(active_unit, skillDefinition);

    internal bool _is_weapon_range_skill(SkillDefinition skillDefinition) =>
        _skill_turn_resolver.IsWeaponRangeSkill(skillDefinition);

    internal int _get_weapon_attack_range(BattleUnitState active_unit) =>
        _skill_turn_resolver.GetWeaponAttackRange(active_unit);

    internal bool _skill_has_tag(SkillDefinition skillDefinition, StringName expected_tag) =>
        _skill_turn_resolver.SkillHasTag(skillDefinition, expected_tag);

    internal bool _is_movement_blocked(BattleUnitState unit_state) =>
        _skill_turn_resolver.IsMovementBlocked(unit_state);

    internal bool _has_status(BattleUnitState unit_state, StringName status_id) =>
        _skill_turn_resolver.HasUnitStatus(unit_state, status_id);

    internal void _consume_status_if_present(
        BattleUnitState unit_state,
        StringName status_id,
        BattleEventBatch batch = null
    ) => _skill_turn_resolver.ConsumeStatusIfPresent(unit_state, status_id, batch);

    internal int _count_debuff_statuses(BattleUnitState unit_state) =>
        _skill_turn_resolver.CountDebuffStatuses(unit_state);

    internal bool _status_counts_as_debuff(
        StringName status_id,
        BattleStatusEffectState status_entry
    ) => _skill_turn_resolver.StatusCountsAsDebuff(status_id, status_entry);

    internal bool _has_counterattack_lock_status(BattleUnitState unit_state) =>
        _skill_turn_resolver.HasCounterattackLockStatus(unit_state);

    internal bool _has_guard_lock_status(BattleUnitState unit_state) =>
        _skill_turn_resolver.HasGuardLockStatus(unit_state);

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
        _damage_resolver.SetSkillDefinitions(GetSkillDefinitionIndexTyped());
        _damage_resolver.SetHitResolver(_hit_resolver);
        _damage_resolver.SetDamageApplicationHook(_contingency_system);
        _damage_resolver.SetEquipmentAbilityRuntimeService(_equipment_ability_runtime_service);
    }

    private static bool IsEmpty(StringName value) => value == default || value == (StringName)"";

}
