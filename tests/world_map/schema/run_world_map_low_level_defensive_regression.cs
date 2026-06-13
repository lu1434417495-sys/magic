using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_world_map_low_level_defensive_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        TestGridSystemNoLongerRequiresGodotObjectRegistration();
        TestRuntimeBattleSelectionStateUsesPlainCollections();
        TestBattleRuntimeContentSetupKeepsStrictTypedScope();
        TestBattleRuntimeHelperRegistrationShrinksToActualResourceTypes();
        TestRuntimeCommandLoggerKeepsScopeTypedAndInternal();
        TestRuntimeEncounterCatalogKeepsTypedScope();
        TestRuntimeCommandHandlersNoLongerRequireGodotRegistration();
        TestRuntimeSnapshotBuilderUsesTypedSourceBoundary();
        TestWorldGenerationHelpersNoLongerRequireGodotRegistration();
        TestWorldPresetHelpersNoLongerRequireGodotRegistration();
        TestHeadlessSnapshotHelpersNoLongerRequireGodotRegistration();
        TestUtilityHelpersNoLongerRequireGodotRegistration();
        TestProgressionContentRuleHelpersNoLongerRequireGodotRegistration();
        TestProgressionRuleHelpersNoLongerRequireGodotRegistration();
        TestHeadlessTextCommandResultNoLongerRequiresGlobalClass();
        TestGridFootprintStateUsesPublicBehavior();
        TestGridCellSurfaceKeepsMinimalRuntimeContract();
        TestVisibilityRebuildIgnoresForeignFactionSources();
        TestFogRevealExportLoadKeepsRevealedCells();

        return _test.Finish("World map low-level defensive regression");
    }

    private void TestGridSystemNoLongerRequiresGodotObjectRegistration()
    {
        Type gridType = typeof(WorldMapGridSystem);
        Type cellType = typeof(WorldMapCellData);
        Type fogType = typeof(WorldMapFogSystem);
        Type visionSourceType = typeof(VisionSourceData);
        _test.Eq(
            fogType.GetMethod(
                "RevealDiamond",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(List<Vector2I>),
            "WorldMapFogSystem paid reveal 不应继续返回 Godot Array。"
        );
    }

    private void TestRuntimeBattleSelectionStateUsesPlainCollections()
    {
        Type selectionStateType = typeof(GameRuntimeBattleSelectionState);
        _test.Eq(
            selectionStateType.GetProperty("queued_target_coords")?.PropertyType,
            typeof(List<Vector2I>),
            "GameRuntimeBattleSelectionState 目标坐标队列不应继续使用 Godot Array。"
        );
        _test.Eq(
            selectionStateType.GetProperty("queued_target_unit_ids")?.PropertyType,
            typeof(List<StringName>),
            "GameRuntimeBattleSelectionState 目标单位队列不应继续使用 Godot Array。"
        );
    }

    private void TestBattleRuntimeContentSetupKeepsStrictTypedScope()
    {
        _test.True(
            typeof(BattleDamageResolver).GetMethod(
                "set_hit_resolver",
                new[] { typeof(GodotObject) }
            ) == null,
            "BattleDamageResolver 不应继续保留 GodotObject hit resolver overload。"
        );
        _test.True(
            typeof(FateRuntimeModule).GetMethod(
                "consume_misfortune_skill_cast",
                new[] { typeof(GodotObject), typeof(StringName) }
            ) == null,
            "FateRuntimeModule 不应继续保留 GodotObject misfortune cast overload。"
        );
        _test.True(
            typeof(FateRuntimeModule).GetMethod("Setup") == null
                && typeof(FateRuntimeModule).GetMethod("DisposeRuntime") == null,
            "FateRuntimeModule lifecycle helper 不应继续暴露为 public。"
        );
        _test.Eq(
            typeof(FateRuntimeModule).GetMethod(
                "Setup",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.GetParameters()[0].ParameterType,
            typeof(IBattleRuntimeCharacterGateway),
            "FateRuntimeModule 应继续保留 nonpublic typed setup 入口。"
        );
        _test.True(
            typeof(FortuneService).GetMethod("Setup") == null
                && typeof(FortuneService).GetMethod("Dispose") == null,
            "FortuneService lifecycle helper 不应继续暴露为 public。"
        );
        _test.Eq(
            typeof(FortuneService).GetMethod(
                "Setup",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.GetParameters()[0].ParameterType,
            typeof(IBattleRuntimeCharacterGateway),
            "FortuneService 应继续保留 nonpublic typed setup 入口。"
        );
        _test.True(
            typeof(FortunaGuidanceService).GetMethod("Setup") == null
                && typeof(FortunaGuidanceService).GetMethod("Dispose") == null,
            "FortunaGuidanceService lifecycle helper 不应继续暴露为 public。"
        );
        _test.Eq(
            typeof(FortunaGuidanceService).GetMethod(
                "Setup",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.GetParameters()[0].ParameterType,
            typeof(IBattleRuntimeCharacterGateway),
            "FortunaGuidanceService 应继续保留 nonpublic typed setup 入口。"
        );
        _test.True(
            typeof(LowLuckEventService).GetMethod("Setup") == null
                && typeof(LowLuckEventService).GetMethod("Dispose") == null,
            "LowLuckEventService lifecycle helper 不应继续暴露为 public。"
        );
        _test.Eq(
            typeof(LowLuckEventService).GetMethod(
                "Setup",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.GetParameters()[0].ParameterType,
            typeof(IBattleRuntimeCharacterGateway),
            "LowLuckEventService 应继续保留 nonpublic typed setup 入口。"
        );
        _test.True(
            typeof(MisfortuneGuidanceService).GetMethod("Setup") == null
                && typeof(MisfortuneGuidanceService).GetMethod("Dispose") == null,
            "MisfortuneGuidanceService lifecycle helper 不应继续暴露为 public。"
        );
        _test.Eq(
            typeof(MisfortuneGuidanceService).GetMethod(
                "Setup",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.GetParameters()[0].ParameterType,
            typeof(IBattleRuntimeCharacterGateway),
            "MisfortuneGuidanceService 应继续保留 nonpublic typed setup 入口。"
        );
        _test.True(
            typeof(MisfortuneService).GetMethod("Setup") == null,
            "MisfortuneService setup 不应继续暴露为 public。"
        );
        _test.Eq(
            typeof(MisfortuneService).GetMethod(
                "Setup",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.GetParameters()[0].ParameterType,
            typeof(BattleFateEventBus),
            "MisfortuneService 应继续保留 nonpublic typed setup 入口。"
        );
        _test.True(
            typeof(MisfortuneService).GetMethod("BindFateEventBus") == null,
            "MisfortuneService 不应继续暴露 public 的 fate bus binder。"
        );
        _test.True(
            typeof(MisfortuneService).GetMethod("CanCastBlackStarBrand") == null
                && typeof(MisfortuneService).GetMethod("ConsumeBlackStarBrandCastResult") == null
                && typeof(MisfortuneService).GetMethod("CanCastCrownBreak") == null
                && typeof(MisfortuneService).GetMethod("ConsumeCrownBreakCastResult") == null
                && typeof(MisfortuneService).GetMethod("GetDoomSentenceCastBlockReason") == null
                && typeof(MisfortuneService).GetMethod("CanCastDoomSentence") == null
                && typeof(MisfortuneService).GetMethod("ConsumeDoomSentenceCastResult") == null
                && typeof(MisfortuneService).GetMethod("GetBlackCrownSealCastBlockReason") == null
                && typeof(MisfortuneService).GetMethod("CanCastBlackCrownSeal") == null
                && typeof(MisfortuneService).GetMethod("ConsumeBlackCrownSealCastResult") == null,
            "MisfortuneService 不应继续暴露 self-only 的 misfortune skill helper。"
        );
        _test.True(
            typeof(MisfortuneGuidanceService).GetMethod("BindBattleRuntimeGateway") == null,
            "MisfortuneGuidanceService 不应继续暴露 public 的 runtime gateway binder。"
        );
        _test.True(
            typeof(FateRuntimeModule).GetMethod("GetMemberCalamity") == null
                && typeof(FateRuntimeModule).GetMethod("GetMemberCalamityCap") == null
                && typeof(FateRuntimeModule).GetMethod("GetBlackStarBrandCastCost") == null
                && typeof(FateRuntimeModule).GetMethod("HasMisfortuneReason") == null
                && typeof(FateRuntimeModule).GetMethod("GetMisfortuneSkillCastBlockReason")
                    == null
                && typeof(FateRuntimeModule).GetMethod("ConsumeMisfortuneSkillCastResult")
                    == null,
            "FateRuntimeModule 不应继续暴露 public 的 calamity / misfortune facade。"
        );
        _test.Eq(
            typeof(FateRuntimeModule).GetMethod(
                "ConsumeMisfortuneSkillCastResult",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(MisfortuneSkillCastResult),
            "FateRuntimeModule 应继续保留 nonpublic typed misfortune cast facade。"
        );

        Type resolverType = typeof(BattleRuntimeLootResolver);
        _test.True(
            resolverType.GetMethod("Setup") == null
                && resolverType.GetMethod("Dispose") == null
                && resolverType.GetMethod("CollectDefeatedUnitLoot") == null
                && resolverType.GetMethod("BuildBattleResolutionResult") == null,
            "BattleRuntimeLootResolver lifecycle / result helper 不应继续暴露为 public。"
        );
        _test.Eq(
            resolverType.GetMethod(
                "BuildBattleResolutionResult",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(BattleResolutionResult),
            "BattleRuntimeLootResolver 应继续保留 nonpublic typed battle resolution builder。"
        );
        _test.True(
            typeof(BattleResolutionResult).GetMethod(
                "IsEmpty",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleResolutionResult 不应继续暴露 public 的 empty-state helper。"
        );
        _test.True(
            typeof(BattleResolutionResult).GetMethod("FromDictionary") == null,
            "BattleResolutionResult 不应继续暴露 zero-caller 的 public parse bridge。"
        );
        _test.True(
            typeof(BattleResolutionResult).GetMethod("ToDictionary") == null,
            "BattleResolutionResult 不应继续暴露 zero-caller 的 public projection bridge。"
        );
        _test.True(
            typeof(BattleResolutionResult).GetMethod("HasPendingCharacterRewards") == null,
            "BattleResolutionResult 不应继续暴露 zero-caller 的 reward sugar helper。"
        );
        _test.True(
            typeof(BattleResolutionResult).GetMethod("GetConvertedCalamityShards") == null,
            "BattleResolutionResult 不应继续把同程序集-only 的 calamity shard helper 暴露为 public。"
        );
        _test.Eq(
            typeof(BattleResolutionResult).GetMethod(
                "ToDictionary",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(GDictionary),
            "BattleResolutionResult 应继续保留 nonpublic typed projection。"
        );
        _test.Eq(
            typeof(BattleResolutionResult).GetMethod(
                "GetConvertedCalamityShards",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(int),
            "BattleResolutionResult 应继续保留 nonpublic typed calamity shard helper。"
        );
        _test.True(
            typeof(BattleResolutionResult).GetField(
                "party_resource_commit",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleResolutionResult 不应继续暴露 public 的 party_resource_commit 字典字段。"
        );
        _test.Eq(
            typeof(BattleResolutionResult).GetField(
                "party_resource_commit",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.FieldType,
            typeof(GDictionary),
            "BattleResolutionResult 应继续保留同程序集可见的 party_resource_commit 字典字段。"
        );
        _test.True(
            typeof(BattleResolutionResult).GetField(
                "loot_entries",
                BindingFlags.Public | BindingFlags.Instance
            ) == null
                && typeof(BattleResolutionResult).GetField(
                    "overflow_entries",
                    BindingFlags.Public | BindingFlags.Instance
                ) == null
                && typeof(BattleResolutionResult).GetField(
                    "pending_character_rewards",
                    BindingFlags.Public | BindingFlags.Instance
                ) == null
                && typeof(BattleResolutionResult).GetField(
                    "quest_progress_events",
                    BindingFlags.Public | BindingFlags.Instance
                ) == null
                && typeof(BattleResolutionResult).GetField(
                    "world_mutations",
                    BindingFlags.Public | BindingFlags.Instance
                ) == null,
            "BattleResolutionResult 不应继续暴露 public 的结果集合字段。"
        );
        _test.True(
            typeof(BattleResolutionResult).GetMethod(
                "SetLootEntries",
                BindingFlags.Public | BindingFlags.Instance
            ) == null
                && typeof(BattleResolutionResult).GetMethod(
                    "SetOverflowEntries",
                    BindingFlags.Public | BindingFlags.Instance
                ) == null
                && typeof(BattleResolutionResult).GetMethod(
                    "SetPendingCharacterRewards",
                    BindingFlags.Public | BindingFlags.Instance
                ) == null,
            "BattleResolutionResult 不应继续暴露 public 的结果 GArray setter。"
        );
        _test.Eq(
            typeof(BattleResolutionResult).GetMethod(
                "SetLootEntries",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(void),
            "BattleResolutionResult 应继续保留同程序集可见的 loot entry setter。"
        );
        _test.Eq(
            typeof(BattleResolutionResult).GetMethod(
                "SetOverflowEntries",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(void),
            "BattleResolutionResult 应继续保留同程序集可见的 overflow entry setter。"
        );
        _test.Eq(
            typeof(BattleResolutionResult).GetMethod(
                "SetPendingCharacterRewards",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(void),
            "BattleResolutionResult 应继续保留同程序集可见的 pending reward setter。"
        );
        _test.True(
            typeof(BattleRuntimeModule).GetField(
                "_battle_resolution_result",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleRuntimeModule 不应继续暴露 public 的 battle resolution result 缓存字段。"
        );
        _test.Eq(
            typeof(BattleRuntimeModule).GetField(
                "_battle_resolution_result",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.FieldType,
            typeof(BattleResolutionResult),
            "BattleRuntimeModule 应继续保留同程序集可见的 battle resolution result 缓存字段。"
        );
        _test.True(
            typeof(BattleRuntimeModule).GetMethod(
                "ConsumeBattleResolutionResult",
                BindingFlags.Public | BindingFlags.Instance
            ) == null
                && typeof(BattleSessionFacade).GetMethod(
                    "ConsumeBattleResolutionResult",
                    BindingFlags.Public | BindingFlags.Instance
                ) == null
                && typeof(GameRuntimeFacade).GetMethod(
                    "FinalizeBattleResolution",
                    BindingFlags.Public | BindingFlags.Instance
                ) == null,
            "Battle resolution result bridge 不应继续暴露为 public runtime surface。"
        );
        _test.Eq(
            typeof(BattleRuntimeModule).GetMethod(
                "ConsumeBattleResolutionResult",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(BattleResolutionResult),
            "BattleRuntimeModule 应继续保留同程序集可见的 battle resolution result consume helper。"
        );
        _test.Eq(
            typeof(BattleSessionFacade).GetMethod(
                "ConsumeBattleResolutionResult",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(BattleResolutionResult),
            "BattleSessionFacade 应继续保留同程序集可见的 battle resolution result consume helper。"
        );
        _test.Eq(
            typeof(GameRuntimeFacade).GetMethod(
                "FinalizeBattleResolution",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(bool),
            "GameRuntimeFacade 应继续保留同程序集可见的 battle resolution finalizer。"
        );
        _test.True(
            typeof(FortunaGuidanceService).GetMethod(
                "HandleBattleResolution",
                BindingFlags.Public | BindingFlags.Instance
            ) == null
                && typeof(MisfortuneGuidanceService).GetMethod(
                    "HandleBattleResolution",
                    BindingFlags.Public | BindingFlags.Instance
                ) == null,
            "Fate guidance 不应继续通过 public surface 暴露 BattleResolutionResult 入口。"
        );
        _test.Eq(
            typeof(FortunaGuidanceService).GetMethod(
                "HandleBattleResolution",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(List<StringName>),
            "FortunaGuidanceService 应继续保留同程序集可见的 battle resolution handler。"
        );
        _test.Eq(
            typeof(MisfortuneGuidanceService).GetMethod(
                "HandleBattleResolution",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(List<StringName>),
            "MisfortuneGuidanceService 应继续保留同程序集可见的 battle resolution handler。"
        );
        _test.True(
            typeof(GameSession).GetField(
                "_battle_special_profile_registry",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "GameSession 不应继续暴露 public 的 battle special profile registry field。"
        );
        _test.True(
            typeof(MeteorSwarmCommitResult).GetMethod("AddChangedUnitId") == null
                && typeof(MeteorSwarmCommitResult).GetMethod("AddChangedCoord") == null
                && typeof(MeteorSwarmCommitResult).GetMethod("AddDefeatedUnitId") == null,
            "MeteorSwarmCommitResult 不应继续暴露 public 去重 helper。"
        );
        _test.Eq(
            typeof(MeteorSwarmCommitResult).GetMethod(
                "AddChangedUnitId",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.GetParameters()[0].ParameterType,
            typeof(StringName),
            "MeteorSwarmCommitResult 应继续保留 nonpublic typed changed-unit helper。"
        );
        _test.True(
            typeof(BattleSkillOutcomeCommitter).GetMethod("Setup") == null
                && typeof(BattleSkillOutcomeCommitter).GetMethod("Dispose") == null
                && typeof(BattleSkillOutcomeCommitter).GetMethod("CommitCommonOutcome") == null
                && typeof(BattleSkillOutcomeCommitter).GetMethod("CommitMeteorSwarmResult") == null,
            "BattleSkillOutcomeCommitter 不应继续把 meteor swarm typed commit 入口暴露为 public。"
        );
        _test.Eq(
            typeof(BattleSkillOutcomeCommitter).GetMethod(
                "CommitMeteorSwarmResult",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(bool),
            "BattleSkillOutcomeCommitter 应继续保留 nonpublic typed meteor swarm commit 入口。"
        );
        _test.Eq(
            typeof(BattleSkillOutcomeCommitter).GetMethod(
                "CommitCommonOutcome",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(bool),
            "BattleSkillOutcomeCommitter 应继续保留 nonpublic typed common outcome commit 入口。"
        );
        _test.True(
            typeof(BattleCommonSkillOutcome).GetMethod("AddChangedUnitId") == null
                && typeof(BattleCommonSkillOutcome).GetMethod("AddChangedCoord") == null
                && typeof(BattleCommonSkillOutcome).GetMethod("AddDefeatedUnitId") == null
                && typeof(BattleCommonSkillOutcome).GetMethod("AddTargetResult") == null
                && typeof(BattleCommonSkillOutcome).GetMethod("AddStatusEffectIds") == null,
            "BattleCommonSkillOutcome 不应继续暴露 public owner-only helper。"
        );
        _test.Eq(
            typeof(BattleCommonSkillOutcome).GetMethod(
                "AddTargetResult",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(void),
            "BattleCommonSkillOutcome 应继续保留 nonpublic typed target-result helper。"
        );
        _test.True(
            typeof(BattleContributionLedger).GetMethod("Clear") == null
                && typeof(BattleMetricsCollector).GetMethod("Clear") == null
                && typeof(BattleSkillMasteryService).GetMethod("Clear") == null,
            "Battle runtime internal clear helper 不应继续暴露为 public。"
        );
        _test.True(
            typeof(MeteorSwarmTargetPlan).GetMethod("GetDistanceForUnit") == null
                && typeof(MeteorSwarmTargetPlan).GetMethod("GetPrimaryCoordForUnit") == null
                && typeof(MeteorSwarmTargetPlan).GetMethod("GetRingForCoord") == null,
            "MeteorSwarmTargetPlan 不应继续暴露 public owner-only helper。"
        );
        _test.Eq(
            typeof(MeteorSwarmTargetPlan).GetMethod(
                "GetRingForCoord",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(int),
            "MeteorSwarmTargetPlan 应继续保留 nonpublic typed ring helper。"
        );
        _test.True(
            typeof(MeteorSwarmCastContext).GetMethod("HasDrift") == null,
            "MeteorSwarmCastContext 不应继续暴露 public drift helper。"
        );
        _test.Eq(
            typeof(MeteorSwarmCastContext).GetMethod(
                "HasDrift",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(bool),
            "MeteorSwarmCastContext 应继续保留 nonpublic typed drift helper。"
        );
        _test.True(
            typeof(MeteorSwarmTargetOutcome).GetMethod("AddComponent") == null
                && typeof(MeteorSwarmTargetOutcome).GetMethod("AddStatusEffectId") == null,
            "MeteorSwarmTargetOutcome 不应继续暴露 public owner-only helper。"
        );
        _test.True(
            typeof(MeteorSwarmComponentFact).GetMethod("Clone") == null,
            "MeteorSwarmComponentFact 不应继续暴露 zero-caller 的 clone helper。"
        );
        _test.True(
            typeof(BattleRuntimeModule).GetMethod("_build_unit_metric_entry") == null
                && typeof(BattleRuntimeModule).GetMethod("_ensure_unit_metric_entry") == null
                && typeof(BattleRuntimeModule).GetMethod("_ensure_faction_metric_entry") == null,
            "BattleRuntimeModule 不应继续暴露 zero-caller 的 metrics snake_case bridge。"
        );
        _test.True(
            typeof(BattleRuntimeModule).GetMethod("_resolve_move_path_result") == null
                && typeof(BattleRuntimeModule).GetMethod("_build_unit_skill_damage_preview")
                    == null
                && typeof(BattleRuntimeModule).GetMethod("collect_units_in_coords") == null
                && typeof(BattleRuntimeModule).GetMethod("_validate_unit_skill_targets") == null
                && typeof(BattleRuntimeModule).GetMethod("_get_effect_params") == null
                && typeof(BattleRuntimeModule).GetMethod("_collect_chain_damage_targets")
                    == null
                && typeof(BattleRuntimeModule).GetMethod("_collect_chain_damage_effect_defs")
                    == null
                && typeof(BattleRuntimeModule).GetMethod("_collect_hostile_units_for") == null
                && typeof(BattleRuntimeModule).GetMethod("_collect_units_in_coords") == null
                && typeof(BattleRuntimeModule).GetMethod("_collect_unit_skill_effect_defs")
                    == null
                && typeof(BattleRuntimeModule).GetMethod("_dedupe_effect_defs_by_instance")
                    == null
                && typeof(BattleRuntimeModule).GetMethod("_typed_combat_effect_defs") == null
                && typeof(BattleRuntimeModule).GetMethod("_typed_battle_units") == null
                && typeof(BattleRuntimeModule).GetMethod("_append_damage_preview_line")
                    == null
                && typeof(BattleRuntimeModule).GetMethod(
                    "_build_unit_skill_resolution_preview_lines"
                ) == null
                && typeof(BattleRuntimeModule).GetMethod(
                    "_should_resolve_unit_skill_as_fate_attack"
                ) == null
                && typeof(BattleRuntimeModule).GetMethod("_apply_unit_skill_result") == null
                && typeof(BattleRuntimeModule).GetMethod("_resolve_ground_unit_effect_result")
                    == null
                && typeof(BattleRuntimeModule).GetMethod("_build_unit_shield_result") == null
                && typeof(BattleRuntimeModule).GetMethod("_build_reachable_move_buckets") == null
                && typeof(BattleRuntimeModule).GetMethod("_apply_chain_damage_effects") == null
                && typeof(BattleRuntimeModule).GetMethod("_resolve_chain_damage_radius")
                    == null
                && typeof(BattleRuntimeModule).GetMethod("_collect_adjacent_living_allies")
                    == null,
            "BattleRuntimeModule 不应继续暴露 zero-caller 的 snake_case payload bridge。"
        );
        _test.True(
            typeof(BattleDamagePreviewResult).GetMethod("ToDictionary") == null,
            "BattleDamagePreviewResult 不应继续暴露 public 的 preview projection bridge。"
        );
        _test.Eq(
            typeof(BattleDamagePreviewResult).GetMethod(
                "ToDictionary",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(GDictionary),
            "BattleDamagePreviewResult 应继续保留 nonpublic preview projection bridge。"
        );
        _test.True(
            typeof(AttackPreviewData).GetMethod(
                "SetAttackRollModifierBreakdownPayload",
                new[] { typeof(Godot.Collections.Array) }
            ) == null,
            "AttackPreviewData 不应继续暴露 legacy GArray breakdown setter。"
        );
        _test.True(
            typeof(AttackPreviewData).GetMethod(
                "SetAttackRollModifierBreakdownPayload",
                new[] { typeof(IEnumerable<Godot.Collections.Dictionary>) }
            ) == null,
            "AttackPreviewData 不应继续暴露 zero-caller 的 IEnumerable<Dictionary> breakdown setter。"
        );
        _test.Eq(
            typeof(BattleRuntimeSkillTurnResolver).GetMethod(
                "Setup",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.GetParameters()[0].ParameterType,
            typeof(BattleRuntimeModule),
            "BattleRuntimeSkillTurnResolver 应继续保留 nonpublic typed setup 入口。"
        );
        _test.True(
            typeof(BattleRuntimeSkillTurnResolver).GetMethod(
                "ResolveTurnControlStatusResult",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleRuntimeSkillTurnResolver 不应继续暴露 public 的 typed turn-control resolver。"
        );
        _test.Eq(
            typeof(BattleRuntimeSkillTurnResolver).GetMethod(
                "ResolveTurnControlStatusResult",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(BattleTurnControlStatusResult),
            "BattleRuntimeSkillTurnResolver 应继续保留 nonpublic typed turn-control resolver。"
        );
        _test.True(
            typeof(BattleRuntimeSkillTurnResolver).GetMethod(
                "GetMisfortuneSkillCastBlockReason"
            ) == null,
            "BattleRuntimeSkillTurnResolver 不应继续暴露 public 的 misfortune cast-block facade。"
        );
        _test.Eq(
            typeof(BattleRuntimeSkillTurnResolver).GetMethod(
                "GetMisfortuneSkillCastBlockReason",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(string),
            "BattleRuntimeSkillTurnResolver 应继续保留 nonpublic typed misfortune cast-block facade。"
        );
        _test.True(
            typeof(BattleRuntimeSkillTurnResolver).GetMethod(
                "ConsumeMisfortuneSkillGate"
            ) == null
                && typeof(BattleRuntimeSkillTurnResolver).GetMethod(
                    "GetRacialSkillChargeBlockReason"
                ) == null
                && typeof(BattleRuntimeSkillTurnResolver).GetMethod(
                    "ConsumeRacialSkillCharge"
                ) == null
                && typeof(BattleRuntimeSkillTurnResolver).GetMethod(
                    "GetRacialSkillChargeKey"
                ) == null,
            "BattleRuntimeSkillTurnResolver 不应继续暴露 public 的 self-only misfortune / racial charge helper。"
        );
        _test.True(
            typeof(BattleRuntimeSkillTurnResolver).GetMethod("UnitHasMeleeWeapon") == null
                && typeof(BattleRuntimeSkillTurnResolver).GetMethod("UnitMatchesRequiredWeaponFamilies")
                    == null
                && typeof(BattleRuntimeSkillTurnResolver).GetMethod("UnitHasEquippedShield") == null
                && typeof(BattleRuntimeSkillTurnResolver).GetMethod("RequiresMeleeWeapon") == null
                && typeof(BattleRuntimeSkillTurnResolver).GetMethod(
                    "EffectUsesWeaponPhysicalDamageTag"
                ) == null
                && typeof(BattleRuntimeSkillTurnResolver).GetMethod(
                    "GetSkillCommandBlockReason"
                ) == null
                && typeof(BattleRuntimeSkillTurnResolver).GetMethod("ConsumeSkillCosts") == null
                && typeof(BattleRuntimeSkillTurnResolver).GetMethod("AdvanceUnitCooldowns") == null
                && typeof(BattleRuntimeSkillTurnResolver).GetMethod(
                    "ConsumeTurnCooldownDelta"
                ) == null
                && typeof(BattleRuntimeSkillTurnResolver).GetMethod("AdvanceUnitTurnTimers")
                    == null
                && typeof(BattleRuntimeSkillTurnResolver).GetMethod("GetEffectiveSkillRange")
                    == null
                && typeof(BattleRuntimeSkillTurnResolver).GetMethod("ResolveBaseSkillRange")
                    == null
                && typeof(BattleRuntimeSkillTurnResolver).GetMethod("IsWeaponRangeSkill")
                    == null
                && typeof(BattleRuntimeSkillTurnResolver).GetMethod("GetWeaponAttackRange")
                    == null
                && typeof(BattleRuntimeSkillTurnResolver).GetMethod("SkillHasTag") == null
                && typeof(BattleRuntimeSkillTurnResolver).GetMethod("IsMovementBlocked")
                    == null
                && typeof(BattleRuntimeSkillTurnResolver).GetMethod("HasUnitStatus") == null
                && typeof(BattleRuntimeSkillTurnResolver).GetMethod("ConsumeStatusIfPresent")
                    == null
                && typeof(BattleRuntimeSkillTurnResolver).GetMethod(
                    "IsMainSkillLockedByStatus"
                ) == null,
            "BattleRuntimeSkillTurnResolver 不应继续暴露 public 的 module-only range/status/cost helper。"
        );
        _test.Eq(
            typeof(BattleRuntimeSkillTurnResolver).GetMethod(
                "GetSkillCommandBlockReason",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(string),
            "BattleRuntimeSkillTurnResolver 应继续保留 nonpublic skill command block helper。"
        );
        _test.Eq(
            typeof(BattleRuntimeSkillTurnResolver).GetMethod(
                "GetEffectiveSkillRange",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(int),
            "BattleRuntimeSkillTurnResolver 应继续保留 nonpublic skill range helper。"
        );
        _test.True(
            typeof(BattleChangeEquipmentResolver).GetMethod("setup") == null
                && typeof(BattleChangeEquipmentResolver).GetMethod("dispose") == null
                && typeof(BattleChangeEquipmentResolver).GetMethod("preview_command") == null
                && typeof(BattleChangeEquipmentResolver).GetMethod("handle_command") == null,
            "BattleChangeEquipmentResolver 不应继续暴露 snake_case Godot helper 入口。"
        );
        _test.Eq(
            typeof(BattleChangeEquipmentResolver).GetMethod(
                "Setup",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.GetParameters()[0].ParameterType,
            typeof(BattleRuntimeModule),
            "BattleChangeEquipmentResolver 应继续保留 nonpublic typed setup 入口。"
        );
        _test.True(
            typeof(BattleMovementService).GetMethod("setup") == null
                && typeof(BattleMovementService).GetMethod("dispose") == null
                && typeof(BattleMovementService).GetMethod("_resolve_move_path_result_typed") == null
                && typeof(BattleMovementService).GetMethod("_move_unit_along_validated_path") == null,
            "BattleMovementService 不应继续暴露 snake_case helper 入口。"
        );
        _test.Eq(
            typeof(BattleMovementService).GetMethod(
                "GetUnitReachableMoveCoords",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(IReadOnlyList<Vector2I>),
            "BattleMovementService reachable coords 正式读侧应保持 typed IReadOnlyList<Vector2I>。"
        );
        _test.Eq(
            typeof(BattleMovementService).GetMethod(
                "MoveUnitAlongValidatedPathTyped",
                BindingFlags.NonPublic | BindingFlags.Instance
            )
                ?.GetParameters()[1]
                .ParameterType,
            typeof(IReadOnlyList<Vector2I>),
            "BattleMovementService validated path 执行入口不应继续接收 Godot Array。"
        );
        _test.Eq(
            typeof(BattleMovementService).GetMethod(
                "Setup",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.GetParameters()[0].ParameterType,
            typeof(BattleRuntimeModule),
            "BattleMovementService 应继续保留 nonpublic typed setup 入口。"
        );
        _test.True(
            typeof(BattleGroundEffectService).GetMethod("setup") == null
                && typeof(BattleGroundEffectService).GetMethod("dispose") == null
                && typeof(BattleGroundEffectService).GetMethod("_resolve_ground_spell_control_after_cost") == null
                && typeof(BattleGroundEffectService).GetMethod("_resolve_unit_spell_control_after_cost") == null
                && typeof(BattleGroundEffectService).GetMethod("_apply_unit_shield_effects") == null
                && typeof(BattleGroundEffectService).GetMethod("_build_ground_forced_move_context") == null
                && typeof(BattleGroundEffectService).GetMethod("_build_effect_instance_lookup") == null
                && typeof(BattleGroundEffectService).GetMethod("_append_affected_unit_id") == null
                && typeof(BattleGroundEffectService).GetMethod("_collect_ground_unit_effect_defs") == null
                && typeof(BattleGroundEffectService).GetMethod("_collect_ground_terrain_effect_defs") == null
                && typeof(BattleGroundEffectService).GetMethod("_collect_ground_effect_defs") == null
                && typeof(BattleGroundEffectService).GetMethod("_collect_ground_preview_unit_ids") == null
                && typeof(BattleGroundEffectService).GetMethod("_apply_ground_wind_push_effects") == null
                && typeof(BattleGroundEffectService).GetMethod("_apply_ground_precast_special_effects")
                    == null
                && typeof(BattleGroundEffectService).GetMethod("_apply_ground_relocation") == null
                && typeof(BattleGroundEffectService).GetMethod("_apply_ground_relocation_with_mode")
                    == null
                && typeof(BattleGroundEffectService).GetMethod("_apply_ground_jump_relocation")
                    == null
                && typeof(BattleGroundEffectService).GetMethod("_apply_ground_unit_effects") == null
                && typeof(BattleGroundEffectService).GetMethod("_apply_ground_terrain_effects") == null
                && typeof(BattleGroundEffectService).GetMethod("_validate_ground_skill_command") == null
                && typeof(BattleGroundEffectService).GetMethod("_resolve_ground_unit_effect_result") == null
                && typeof(BattleGroundEffectService).GetMethod("_dedupe_effect_defs_by_instance") == null
                && typeof(BattleGroundEffectService).GetMethod("_build_ground_effect_coords") == null
                && typeof(BattleGroundEffectService).GetMethod("_append_result_report_entry") == null
                && typeof(BattleGroundEffectService).GetMethod("_collect_units_in_coords") == null
                && typeof(BattleGroundEffectService).GetMethod("_append_changed_coords") == null
                && typeof(BattleGroundEffectService).GetMethod("_sort_coords") == null
                && typeof(BattleGroundEffectService).GetMethod("_collect_wind_push_effects") == null
                && typeof(BattleGroundEffectService).GetMethod("_should_resolve_ground_effects_as_attack")
                    == null
                && typeof(BattleGroundEffectService).GetMethod("_reconcile_water_topology") == null
                && typeof(BattleGroundEffectService).GetMethod(
                    "append_result_source_status_effects",
                    new[] { typeof(BattleEventBatch), typeof(BattleUnitState), typeof(GDictionary) }
                ) == null
                && typeof(BattleGroundEffectService).GetMethod(
                    "append_damage_result_log_lines",
                    new[] { typeof(BattleEventBatch), typeof(string), typeof(string), typeof(GDictionary) }
                ) == null
                && typeof(BattleGroundEffectService).GetMethod("_record_vajra_body_mastery_from_incoming_damage") == null
                && typeof(BattleGroundEffectService).GetMethod("_get_edge_authoring_reference") == null
                && typeof(BattleGroundEffectService).GetMethod("_get_edge_clear_feature_kinds") == null
                && typeof(BattleGroundEffectService).GetMethod("_can_edge_clear_remove_feature") == null
                && typeof(BattleGroundEffectService).GetMethod("_collect_wind_push_target_units") == null
                && typeof(BattleGroundEffectService).GetMethod("_try_wind_push_unit_one_step") == null
                && typeof(BattleGroundEffectService).GetMethod("_sort_wind_push_units_near_to_far") == null,
            "BattleGroundEffectService 不应继续暴露旧的 helper 生命周期入口。"
        );
        _test.True(
            typeof(BattleMagicBacklashResolver).GetMethod("should_resolve_spell_control") == null
                && typeof(BattleMagicBacklashResolver).GetMethod("apply_spell_control_after_cost") == null
                && typeof(BattleMagicBacklashResolver).GetMethod("build_ground_backlash_target_coords") == null
                && typeof(BattleMagicBacklashResolver).GetMethod("append_ground_backlash_log") == null,
            "BattleMagicBacklashResolver 不应继续保留 snake_case / Dictionary wrapper surface。"
        );
        _test.True(
            typeof(BattleGroundEffectService).GetMethod(
                "BuildGroundForcedMoveContextResult",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleGroundEffectService forced-move context 入口不应继续保留 public bridge。"
        );
        _test.True(
            typeof(BattleSkillExecutionOrchestrator).GetMethod(
                "ApplyUnitSkillSpecialEffectsResult",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleSkillExecutionOrchestrator 不应继续暴露 public 的 typed special-effect helper。"
        );
        _test.Eq(
            typeof(BattleSkillExecutionOrchestrator).GetMethod(
                "ApplyUnitSkillSpecialEffectsResult",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(BattleSpecialSkillResult),
            "BattleSkillExecutionOrchestrator 应继续保留 nonpublic typed special-effect helper。"
        );
        _test.True(
            typeof(BattleSkillExecutionOrchestrator).GetMethod("_apply_ground_precast_special_effects")
                == null
                && typeof(BattleSkillExecutionOrchestrator).GetMethod("_validate_ground_skill_command")
                    == null
                && typeof(BattleSkillExecutionOrchestrator).GetMethod("_validate_ground_skill_command_result")
                    == null
                && typeof(BattleSkillExecutionOrchestrator).GetMethod("_resolve_ground_cast_variant")
                    == null,
            "BattleSkillExecutionOrchestrator 不应继续暴露 ground validation / precast / cast-variant wrapper。"
        );
        _test.True(
            typeof(BattleSkillExecutionOrchestrator).GetMethod("_resolve_unit_skill_effect_result")
                == null,
            "BattleSkillExecutionOrchestrator 不应继续暴露 public payload wrapper _resolve_unit_skill_effect_result。"
        );
        _test.True(
            typeof(BattleSkillExecutionOrchestrator).GetMethod("_build_ground_effect_coords")
                == null
                && typeof(BattleSkillExecutionOrchestrator).GetMethod("_collect_ground_unit_effect_defs")
                == null
                && typeof(BattleSkillExecutionOrchestrator).GetMethod("_collect_ground_terrain_effect_defs")
                    == null
                && typeof(BattleSkillExecutionOrchestrator).GetMethod("_collect_ground_preview_unit_ids")
                    == null
                && typeof(BattleSkillExecutionOrchestrator).GetMethod("_apply_ground_unit_effects")
                    == null
                && typeof(BattleSkillExecutionOrchestrator).GetMethod("_apply_ground_unit_effects_result")
                    == null
                && typeof(BattleSkillExecutionOrchestrator).GetMethod("_apply_ground_terrain_effects")
                    == null
                && typeof(BattleSkillExecutionOrchestrator).GetMethod("_apply_ground_terrain_effects_result")
                    == null
                && typeof(BattleSkillExecutionOrchestrator).GetMethod(
                    "_build_unit_skill_damage_preview"
                ) == null
                && typeof(BattleSkillExecutionOrchestrator).GetMethod(
                    "_validate_unit_skill_targets"
                ) == null
                && typeof(BattleSkillExecutionOrchestrator).GetMethod(
                    "_apply_unit_skill_special_effects"
                ) == null
                && typeof(BattleSkillExecutionOrchestrator).GetMethod(
                    "_build_random_chain_target_pool"
                ) == null
                && typeof(BattleSkillExecutionOrchestrator).GetMethod(
                    "_collect_chain_damage_targets"
                ) == null
                && typeof(BattleSkillExecutionOrchestrator).GetMethod("_collect_units_in_coords")
                    == null
                && typeof(BattleSkillExecutionOrchestrator).GetMethod("_get_ground_special_effect_validation_message")
                    == null,
            "BattleSkillExecutionOrchestrator 不应继续暴露 ground-effect Godot wrapper surface。"
        );
        _test.True(
            typeof(BattleSkillExecutionOrchestrator).GetMethod("_apply_chain_damage_effects")
                == null
                && typeof(BattleSkillExecutionOrchestrator).GetMethod(
                    "_collect_chain_damage_effect_defs"
                ) == null
                && typeof(BattleSkillExecutionOrchestrator).GetMethod(
                    "_resolve_chain_damage_radius"
                ) == null,
            "BattleSkillExecutionOrchestrator 不应继续暴露 public 的 chain-damage payload helper。"
        );
        _test.True(
            typeof(BattleSkillExecutionOrchestrator).GetMethod("_append_result_report_entry")
                == null
                && typeof(BattleSkillExecutionOrchestrator).GetMethod("_append_report_entry_to_batch")
                    == null,
            "BattleSkillExecutionOrchestrator 不应继续保留 report entry payload wrapper。"
        );
        _test.True(
            typeof(BattleSkillExecutionOrchestrator).GetMethod("summarize_damage_result")
                == null
                && typeof(BattleSkillExecutionOrchestrator).GetMethod("build_damage_absorb_reason_text")
                    == null
                && typeof(BattleSkillExecutionOrchestrator).GetMethod("_apply_equipment_durability_result")
                    == null,
            "BattleSkillExecutionOrchestrator 不应继续保留 damage summary / equipment durability payload wrapper。"
        );
        _test.True(
            typeof(BattleSkillExecutionOrchestrator).GetMethod(
                "append_result_source_status_effects",
                new[] { typeof(BattleEventBatch), typeof(BattleUnitState), typeof(GDictionary) }
            ) == null
                && typeof(BattleSkillExecutionOrchestrator).GetMethod(
                    "append_damage_result_log_lines",
                    new[] { typeof(BattleEventBatch), typeof(string), typeof(string), typeof(GDictionary) }
                ) == null
                && typeof(BattleSkillExecutionOrchestrator).GetMethod("_record_vajra_body_mastery_from_incoming_damage")
                    == null,
            "BattleSkillExecutionOrchestrator 不应继续保留 source-status / damage-log / vajra mastery payload wrapper。"
        );
        _test.True(
            typeof(BattleSkillExecutionOrchestrator).GetMethod("Setup") == null
                && typeof(BattleSkillExecutionOrchestrator).GetMethod("DisposeRuntime") == null,
            "BattleSkillExecutionOrchestrator lifecycle helper 不应继续暴露为 public。"
        );
        _test.Eq(
            typeof(BattleSkillExecutionOrchestrator).GetMethod(
                "Setup",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.GetParameters()[0].ParameterType,
            typeof(BattleRuntimeModule),
            "BattleSkillExecutionOrchestrator 应继续保留 nonpublic typed setup 入口。"
        );
        _test.True(
            typeof(BattleRuntimeModule).GetMethod("summarize_damage_result") == null
                && typeof(BattleRuntimeModule).GetMethod("build_damage_absorb_reason_text")
                    == null,
            "BattleRuntimeModule 不应继续暴露 damage summary runtime wrapper。"
        );
        _test.True(
            typeof(BattleRuntimeModule).GetMethod("HasMisfortuneReason") == null
                && typeof(BattleRuntimeModule).GetMethod("ConsumeMisfortuneSkillCastResult")
                    == null,
            "BattleRuntimeModule 不应继续暴露 public 的 misfortune facade wrapper。"
        );
        _test.Eq(
            typeof(BattleRuntimeModule).GetMethod(
                "ConsumeMisfortuneSkillCastResult",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(MisfortuneSkillCastResult),
            "BattleRuntimeModule 应继续保留 nonpublic typed misfortune cast wrapper。"
        );
        _test.True(
            typeof(BattleRuntimeModule).GetMethod("_build_ground_effect_coords") == null
                && typeof(BattleRuntimeModule).GetMethod("_collect_ground_unit_effect_defs")
                    == null
                && typeof(BattleRuntimeModule).GetMethod("_collect_ground_terrain_effect_defs")
                    == null
                && typeof(BattleRuntimeModule).GetMethod("_collect_ground_effect_defs")
                    == null
                && typeof(BattleRuntimeModule).GetMethod("_collect_ground_preview_unit_ids")
                    == null
                && typeof(BattleRuntimeModule).GetMethod("_apply_ground_precast_special_effects")
                    == null
                && typeof(BattleRuntimeModule).GetMethod("_resolve_ground_cast_variant")
                    == null
                && typeof(BattleRuntimeModule).GetMethod(
                    "_get_ground_special_effect_validation_message"
                ) == null,
            "BattleRuntimeModule 不应继续保留 ground-effect collect/build/validation wrapper。"
        );
        _test.True(
            typeof(BattleSpecialSkillResolver).GetMethod("setup") == null
                && typeof(BattleSpecialSkillResolver).GetMethod("dispose") == null
                && typeof(BattleSpecialSkillResolver).GetMethod("_apply_doom_shift_effect") == null
                && typeof(BattleSpecialSkillResolver).GetMethod("_apply_forced_move_effect") == null,
            "BattleSpecialSkillResolver 不应继续暴露 snake_case helper 入口。"
        );
        _test.True(
            typeof(BattleSpecialSkillResolver).GetMethod("Setup") == null
                && typeof(BattleSpecialSkillResolver).GetMethod("Dispose") == null
                && typeof(BattleSpecialSkillResolver).GetMethod("ApplyDoomShiftEffectResult") != null
                && typeof(BattleSpecialSkillResolver).GetMethod("ApplyForcedMoveEffect") != null,
            "BattleSpecialSkillResolver 只应继续暴露业务 typed helper，不应继续公开 lifecycle。"
        );
        _test.Eq(
            typeof(BattleSpecialSkillResolver).GetMethod(
                "Setup",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.GetParameters()[0].ParameterType,
            typeof(BattleRuntimeModule),
            "BattleSpecialSkillResolver 应继续保留 nonpublic typed setup 入口。"
        );
        _test.True(
            typeof(BattleMeteorSwarmResolver).GetMethod("setup") == null
                && typeof(BattleMeteorSwarmResolver).GetMethod("dispose") == null
                && typeof(BattleMeteorSwarmResolver).GetMethod("populate_preview") == null
                && typeof(BattleMeteorSwarmResolver).GetMethod("build_cast_context") == null
                && typeof(BattleMeteorSwarmResolver).GetMethod("build_preview_facts") == null
                && typeof(BattleMeteorSwarmResolver).GetMethod("build_target_plan") == null
                && typeof(BattleMeteorSwarmResolver).GetMethod("resolve") == null
                && typeof(BattleMeteorSwarmResolver).GetMethod("_build_hostile_terrain_consequence")
                    == null
                && typeof(BattleMeteorSwarmResolver).GetMethod("_collect_component_save_profile_ids")
                    == null
                && typeof(BattleMeteorSwarmResolver).GetMethod("_apply_save_profile_to_damage_effect")
                    == null
                && typeof(BattleMeteorSwarmResolver).GetMethod("_build_component_damage_preview")
                    == null
                && typeof(BattleMeteorSwarmResolver).GetMethod("_populate_unit_distances")
                    == null
                && typeof(BattleMeteorSwarmResolver).GetMethod("_build_plan_signature_for_anchor")
                    == null
                && typeof(BattleMeteorSwarmResolver).GetMethod("_build_plan_signature") == null
                && typeof(BattleMeteorSwarmResolver).GetMethod("_extract_target_coords") == null
                && typeof(BattleMeteorSwarmResolver).GetMethod("_resolve_profile") == null
                && typeof(BattleMeteorSwarmResolver).GetMethod("_unit_covers_coord") == null
                && typeof(BattleMeteorSwarmResolver).GetMethod("_get_unit_max_hp") == null
                && typeof(BattleMeteorSwarmResolver).GetMethod("_build_terrain_summary") == null
                && typeof(BattleMeteorSwarmResolver).GetMethod("_terrain_profile_display_name")
                    == null,
            "BattleMeteorSwarmResolver 不应继续暴露 GDScript-style wrapper / snake_case helper surface。"
        );
        _test.True(
            typeof(BattleMeteorSwarmResolver).GetMethod(
                    "PopulatePreview",
                    BindingFlags.NonPublic | BindingFlags.Instance
                ) != null
                && typeof(BattleMeteorSwarmResolver).GetMethod(
                    "BuildCastContextTyped",
                    BindingFlags.NonPublic | BindingFlags.Instance
                ) != null
                && typeof(BattleMeteorSwarmResolver).GetMethod(
                    "BuildPreviewFacts",
                    BindingFlags.NonPublic | BindingFlags.Instance
                ) != null
                && typeof(BattleMeteorSwarmResolver).GetMethod(
                    "BuildTargetPlanTyped",
                    BindingFlags.NonPublic | BindingFlags.Instance
                ) != null
                && typeof(BattleMeteorSwarmResolver).GetMethod(
                    "ResolveTyped",
                    BindingFlags.NonPublic | BindingFlags.Instance
                ) != null,
            "BattleMeteorSwarmResolver 应继续保留 typed meteor 入口。"
        );
        _test.True(
            typeof(BattleMeteorSwarmResolver).GetMethod("Setup") == null
                && typeof(BattleMeteorSwarmResolver).GetMethod("Dispose") == null,
            "BattleMeteorSwarmResolver lifecycle helper 不应继续暴露为 public。"
        );
        _test.Eq(
            typeof(BattleMeteorSwarmResolver).GetMethod(
                "Setup",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.GetParameters()[0].ParameterType,
            typeof(BattleRuntimeModule),
            "BattleMeteorSwarmResolver 应继续保留 nonpublic typed setup 入口。"
        );
        _test.True(
            typeof(BattleSpecialProfilePreviewFacts).GetMethod("to_dict") == null
                && typeof(BattleSpecialProfilePreviewFacts).GetMethod(
                    "get_friendly_fire_numeric_summary"
                ) == null,
            "BattleSpecialProfilePreviewFacts 不应继续保留 snake_case Godot API。"
        );
        _test.True(
            typeof(BattleSpecialProfilePreviewFacts).GetMethod(
                "ToDictionaryArray",
                BindingFlags.NonPublic | BindingFlags.Static
            ) == null
                && typeof(BattleSpecialProfilePreviewFacts).GetMethod(
                    "ToDictionaryList",
                    BindingFlags.NonPublic | BindingFlags.Static
                ) == null
                && typeof(BattleSpecialProfilePreviewFacts).GetMethod(
                    "ToModifierSpecList",
                    BindingFlags.NonPublic | BindingFlags.Static
                ) == null,
            "BattleSpecialProfilePreviewFacts 不应继续承载无关的 static payload/helper 工具函数。"
        );
        _test.Eq(
            typeof(BattleAiScoreInput).GetProperty("special_profile_preview_facts")?.PropertyType,
            typeof(BattleSpecialProfilePreviewFacts),
            "BattleAiScoreInput.special_profile_preview_facts 应继续保持 typed preview facts。"
        );
        _test.Eq(
            typeof(BattleAiScoreInput).GetProperty("runtime_action_metadata")?.PropertyType,
            typeof(BattleAiScoreRuntimeMetadata),
            "BattleAiScoreInput.runtime_action_metadata 不应继续退回 GDictionary。"
        );
        _test.Eq(
            typeof(BattleAiScoreInput).GetProperty("target_numeric_summary")?.PropertyType,
            typeof(List<MeteorSwarmNumericSummary>),
            "BattleAiScoreInput.target_numeric_summary 不应继续退回 Godot Array。"
        );
        _test.Eq(
            typeof(BattleAiScoreInput).GetProperty("friendly_fire_numeric_summary")?.PropertyType,
            typeof(List<MeteorSwarmNumericSummary>),
            "BattleAiScoreInput.friendly_fire_numeric_summary 不应继续退回 Godot Array。"
        );
        _test.Eq(
            typeof(BattleSpecialProfilePreviewFacts).GetField("friendly_fire_numeric_summary")
                ?.FieldType,
            typeof(List<MeteorSwarmNumericSummary>),
            "BattleSpecialProfilePreviewFacts.friendly_fire_numeric_summary 不应继续退回字典列表。"
        );
        _test.Eq(
            typeof(BattleSpecialProfilePreviewFacts).GetField("terrain_summary")?.FieldType,
            typeof(MeteorSwarmTerrainSummaryFact),
            "BattleSpecialProfilePreviewFacts.terrain_summary 不应继续退回 GDictionary。"
        );
        _test.Eq(
            typeof(MeteorSwarmPreviewFacts).GetField("component_preview")?.FieldType,
            typeof(List<MeteorSwarmComponentFact>),
            "MeteorSwarmPreviewFacts.component_preview 不应继续退回字典列表。"
        );
        _test.Eq(
            typeof(MeteorSwarmTargetOutcome).GetProperty("damage_events")?.PropertyType,
            typeof(List<DamageEventResult>),
            "MeteorSwarmTargetOutcome.damage_events 不应继续退回字典列表。"
        );
        _test.Eq(
            typeof(MeteorSwarmCommitResult).GetProperty("terrain_effects")?.PropertyType,
            typeof(List<MeteorSwarmTerrainEffectFact>),
            "MeteorSwarmCommitResult.terrain_effects 不应继续退回字典列表。"
        );
        _test.Eq(
            typeof(MeteorSwarmNumericSummary).GetField("SaveProfileIds")?.FieldType,
            typeof(List<string>),
            "MeteorSwarmNumericSummary.SaveProfileIds 不应继续退回 Godot Array。"
        );
        _test.Eq(
            typeof(MeteorSwarmNumericSummary).GetField("ComponentBreakdown")?.FieldType,
            typeof(List<MeteorSwarmComponentBreakdownEntry>),
            "MeteorSwarmNumericSummary.ComponentBreakdown 不应继续退回 GDictArray。"
        );
        _test.Eq(
            typeof(MeteorSwarmComponentBreakdownEntry).GetField("SaveEstimate")?.FieldType,
            typeof(BattleDamagePreviewSaveEstimate),
            "MeteorSwarmComponentBreakdownEntry.SaveEstimate 不应继续退回 GDictionary。"
        );
        _test.Eq(
            typeof(MeteorSwarmComponentBreakdownEntry).GetField("WorstSaveEstimate")?.FieldType,
            typeof(BattleDamagePreviewSaveEstimate),
            "MeteorSwarmComponentBreakdownEntry.WorstSaveEstimate 不应继续退回 GDictionary。"
        );
        _test.Eq(
            typeof(MeteorSwarmComponentBreakdownEntry).GetField("HalfSourceLabels")?.FieldType,
            typeof(List<string>),
            "MeteorSwarmComponentBreakdownEntry.HalfSourceLabels 不应继续退回 GArray。"
        );
        _test.Eq(
            typeof(MeteorSwarmComponentBreakdownEntry).GetField("DoubleSourceLabels")?.FieldType,
            typeof(List<string>),
            "MeteorSwarmComponentBreakdownEntry.DoubleSourceLabels 不应继续退回 GArray。"
        );
        _test.Eq(
            typeof(MeteorSwarmComponentBreakdownEntry).GetField("ImmuneSourceLabels")?.FieldType,
            typeof(List<string>),
            "MeteorSwarmComponentBreakdownEntry.ImmuneSourceLabels 不应继续退回 GArray。"
        );
        _test.Eq(
            typeof(MeteorSwarmComponentBreakdownEntry).GetField("FixedMitigationSourceLabels")
                ?.FieldType,
            typeof(List<string>),
            "MeteorSwarmComponentBreakdownEntry.FixedMitigationSourceLabels 不应继续退回 GArray。"
        );
        _test.True(
            typeof(MeteorSwarmComponentBreakdownEntry).GetField("MitigationSources") == null
                && typeof(MeteorSwarmComponentBreakdownEntry).GetField("FixedMitigationSources")
                    == null,
            "MeteorSwarmComponentBreakdownEntry 不应恢复 GArray mitigation source 字段。"
        );
        _test.Eq(
            typeof(MeteorSwarmNumericSummary).GetField("ResistanceTiersByDamageTag")?.FieldType,
            typeof(Dictionary<StringName, StringName>),
            "MeteorSwarmNumericSummary.ResistanceTiersByDamageTag 不应继续退回 GDictionary。"
        );
        _test.Eq(
            typeof(MeteorSwarmNumericSummary).GetField("StatusEffectIds")?.FieldType,
            typeof(List<StringName>),
            "MeteorSwarmNumericSummary.StatusEffectIds 不应继续退回 Godot Array。"
        );
        _test.Eq(
            typeof(BattleAiScoreInput).GetProperty("attack_roll_modifier_breakdown")?.PropertyType,
            typeof(List<BattleAttackRollModifierSpec>),
            "BattleAiScoreInput.attack_roll_modifier_breakdown 不应继续退回 Godot Array。"
        );
        _test.Eq(
            typeof(BattleSpecialProfilePreviewFacts).GetField("attack_roll_modifier_breakdown")
                ?.FieldType,
            typeof(List<BattleAttackRollModifierSpec>),
            "BattleSpecialProfilePreviewFacts.attack_roll_modifier_breakdown 不应继续退回字典列表。"
        );
        _test.Eq(
            typeof(BattleAiScoreInput).GetProperty("high_priority_target_ids")?.PropertyType,
            typeof(List<StringName>),
            "BattleAiScoreInput.high_priority_target_ids 不应继续退回 Godot Array。"
        );
        _test.Eq(
            typeof(BattleAiScoreInput).GetProperty("high_priority_reasons")?.PropertyType,
            typeof(Dictionary<StringName, List<string>>),
            "BattleAiScoreInput.high_priority_reasons 不应继续退回 GDictionary。"
        );
        _test.Eq(
            typeof(BattleAiScoreInput).GetProperty("target_unit_ids")?.PropertyType,
            typeof(List<StringName>),
            "BattleAiScoreInput.target_unit_ids 不应继续退回 Godot Array。"
        );
        _test.Eq(
            typeof(BattleAiScoreInput).GetProperty("target_coords")?.PropertyType,
            typeof(List<Vector2I>),
            "BattleAiScoreInput.target_coords 不应继续退回 Godot Array。"
        );
        _test.Eq(
            typeof(BattleAiScoreInput).GetProperty("random_chain_candidate_unit_ids")?.PropertyType,
            typeof(List<StringName>),
            "BattleAiScoreInput.random_chain_candidate_unit_ids 不应继续退回 Godot Array。"
        );
        _test.Eq(
            typeof(BattleAiScoreInput).GetProperty("estimated_lethal_target_ids")?.PropertyType,
            typeof(List<StringName>),
            "BattleAiScoreInput.estimated_lethal_target_ids 不应继续退回 Godot Array。"
        );
        _test.Eq(
            typeof(BattleAiScoreInput).GetProperty("estimated_lethal_threat_target_ids")?.PropertyType,
            typeof(List<StringName>),
            "BattleAiScoreInput.estimated_lethal_threat_target_ids 不应继续退回 Godot Array。"
        );
        _test.Eq(
            typeof(BattleAiScoreInput).GetProperty("estimated_control_target_ids")?.PropertyType,
            typeof(List<StringName>),
            "BattleAiScoreInput.estimated_control_target_ids 不应继续退回 Godot Array。"
        );
        _test.Eq(
            typeof(BattleAiScoreInput).GetProperty("estimated_control_threat_target_ids")?.PropertyType,
            typeof(List<StringName>),
            "BattleAiScoreInput.estimated_control_threat_target_ids 不应继续退回 Godot Array。"
        );
        _test.Eq(
            typeof(BattleAiScoreInput).GetProperty("pre_action_threat_unit_ids")?.PropertyType,
            typeof(List<StringName>),
            "BattleAiScoreInput.pre_action_threat_unit_ids 不应继续退回 Godot Array。"
        );
        _test.Eq(
            typeof(BattleAiScoreInput).GetProperty("post_action_remaining_threat_unit_ids")?.PropertyType,
            typeof(List<StringName>),
            "BattleAiScoreInput.post_action_remaining_threat_unit_ids 不应继续退回 Godot Array。"
        );
        _test.Eq(
            typeof(BattleAiScoreInput).GetProperty("path_step_hit_counts_by_unit_id")?.PropertyType,
            typeof(Dictionary<StringName, int>),
            "BattleAiScoreInput.path_step_hit_counts_by_unit_id 不应继续退回 GDictionary。"
        );
        _test.Eq(
            typeof(BattleAiScoreInput).GetProperty("save_estimates_by_target_id")?.PropertyType,
            typeof(Dictionary<StringName, List<BattleAiScoreService.DamageSaveEstimate>>),
            "BattleAiScoreInput.save_estimates_by_target_id 不应继续退回 GDictionary。"
        );
        _test.Eq(
            typeof(BattleAiScoreInput).GetProperty("damage_estimates_by_target_id")?.PropertyType,
            typeof(Dictionary<StringName, List<BattleAiScoreService.DamageEstimateBreakdown>>),
            "BattleAiScoreInput.damage_estimates_by_target_id 不应继续退回 GDictionary。"
        );
        _test.Eq(
            typeof(BattleAiScoreInput).GetMethod(
                "ToTraceDictionary",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(Dictionary<string, object>),
            "BattleAiScoreInput AI trace 导出不应继续回退到 scoreInput.ToDictionary() roundtrip。"
        );
        _test.Eq(
            typeof(BattleAiScoreRuntimeMetadata).GetMethod(
                "ToTraceDictionary",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(Dictionary<string, object>),
            "BattleAiScoreRuntimeMetadata trace 导出不应继续回退到 ToDictionary() payload roundtrip。"
        );
        _test.True(
            typeof(BattleAiScoreRuntimeMetadata).GetMethod("FromScoreMetadataDictionary") == null
                && typeof(BattleAiScoreRuntimeMetadata).GetMethod(
                    "FromMetadata",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(IReadOnlyDictionary<string, object>) },
                    null
                ) != null,
            "BattleAiScoreRuntimeMetadata 不应继续保留 GDictionary metadata 解码入口。"
        );
        _test.Eq(
            typeof(BattleAiContext)
                .GetProperty("skill_score_input_callback")
                ?.PropertyType.GetGenericArguments()[4],
            typeof(IReadOnlyList<CombatEffectDef>),
            "BattleAiContext.skill_score_input_callback 应直接接收 typed effect list。"
        );
        _test.Eq(
            typeof(BattleAiContext)
                .GetProperty("skill_score_input_callback")
                ?.PropertyType.GetGenericArguments()[5],
            typeof(IReadOnlyDictionary<string, object>),
            "BattleAiContext.skill_score_input_callback 应直接接收 typed metadata dictionary。"
        );
        _test.Eq(
            typeof(BattleAiContext)
                .GetProperty("action_score_input_callback")
                ?.PropertyType.GetGenericArguments()[6],
            typeof(IReadOnlyDictionary<string, object>),
            "BattleAiContext.action_score_input_callback 应直接接收 typed metadata dictionary。"
        );
        _test.True(
            typeof(BattleAiContext).GetProperty(
                "action_traces",
                BindingFlags.Instance | BindingFlags.Public
            ) == null
                && typeof(BattleAiContext).GetProperty(
                    "mutation_guard_violations",
                    BindingFlags.Instance | BindingFlags.Public
                ) == null
                && typeof(BattleAiContext).GetMethod(
                    "GetActionTracesTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )?.ReturnType == typeof(IReadOnlyList<AiActionTrace>)
                && typeof(BattleAiContext).GetMethod(
                    "GetMutationGuardViolationsTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )?.ReturnType == typeof(IReadOnlyList<string>)
                && typeof(BattleAiContext).GetMethod("get_runtime_actions") == null
                && typeof(BattleAiContext).GetMethod("has_skill_affordance") == null
                && typeof(BattleAiContext).GetMethod("_build_command_dict") == null
                && typeof(BattleAiContext).GetMethod(
                    "BuildTurnTraceTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )?.ReturnType == typeof(BattleAiTurnTraceProjection)
                && typeof(BattleAiTurnTraceProjection).GetProperty(
                    "ScoreInput",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )?.PropertyType == typeof(BattleAiScoreInput),
            "BattleAiContext 不应恢复 dead Godot wrapper，action_traces / mutation_guard_violations 不应再作为 public Godot 投影。"
        );
        _test.True(
            typeof(BattleAiContext).GetField(
                "ai_query_service",
                BindingFlags.Instance | BindingFlags.Public
            ) == null
                && typeof(BattleAiContext).GetProperty(
                    "candidate_evaluator",
                    BindingFlags.Instance | BindingFlags.Public
                ) == null
                && typeof(BattleAiContext).GetMethod(
                    "EvaluateCandidateRequest",
                    BindingFlags.Instance | BindingFlags.Public
                ) == null
                && typeof(EnemyAiAction).GetMethod(
                    "BuildCandidateRequest",
                    BindingFlags.Instance | BindingFlags.Public
                ) == null,
            "Battle AI query / candidate-request / affordance-evaluator 链不应恢复 public 注入点或 public BuildCandidateRequest。"
        );
        _test.True(
            typeof(AiCommandSummary).GetMethod(
                "ToTraceDictionary",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )?.ReturnType == typeof(Dictionary<string, object>)
                && typeof(AiCandidateSummary).GetMethod(
                    "ToTraceDictionary",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )?.ReturnType == typeof(Dictionary<string, object>)
                && typeof(AiActionTrace).GetMethod(
                    "ToTraceDictionary",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )?.ReturnType == typeof(Dictionary<string, object>),
            "Battle AI trace export DTO 应继续先提供 internal typed trace projection，而不是直接在 owner 内拼 Godot dictionary。"
        );
        _test.True(
            typeof(BattleRuntimeModule).GetField(
                    "_ai_turn_traces",
                    BindingFlags.Instance | BindingFlags.Public
                ) == null
                && typeof(BattleRuntimeModule).GetField(
                    "_ai_turn_traces",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )?.FieldType == typeof(List<BattleAiTurnTraceProjection>)
                && typeof(BattleAiTurnTraceProjection).GetProperty(
                    "ScoreInput",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )?.PropertyType == typeof(BattleAiScoreInput)
                && typeof(BattleRuntimeModule).GetMethod(
                    "CollectAiTraceDecisionTargetUnitIds",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )?.ReturnType == typeof(List<StringName>)
                && typeof(BattleRuntimeModule).GetMethod(
                    "BuildAiTraceExecutionResultTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )?.ReturnType == typeof(BattleAiTraceExecutionResultProjection),
            "BattleRuntimeModule AI trace history / enrichment helper 应继续保持 typed C# owner state，不应退回 Array<GDictionary> 业务态。"
        );
        _test.Eq(
            typeof(BattleAiScoreService).GetMethod(
                "BuildSkillScoreInput",
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new[]
                {
                    typeof(IBattleAiScoreContext),
                    typeof(SkillDef),
                    typeof(BattleCommand),
                    typeof(BattlePreview),
                    typeof(IReadOnlyList<CombatEffectDef>),
                    typeof(IReadOnlyDictionary<string, object>),
                },
                null
            )?.GetParameters()[5].ParameterType,
            typeof(IReadOnlyDictionary<string, object>),
            "BattleAiScoreService.BuildSkillScoreInput 应直接消费 typed effect list / metadata dictionary。"
        );
        _test.Eq(
            typeof(BattleAiScoreService).GetMethod(
                "BuildActionScoreInput",
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new[]
                {
                    typeof(IBattleAiScoreContext),
                    typeof(StringName),
                    typeof(string),
                    typeof(StringName),
                    typeof(BattleCommand),
                    typeof(BattlePreview),
                    typeof(IReadOnlyDictionary<string, object>),
                },
                null
            )?.GetParameters()[6].ParameterType,
            typeof(IReadOnlyDictionary<string, object>),
            "BattleAiScoreService.BuildActionScoreInput 应直接消费 typed metadata dictionary。"
        );
        _test.Eq(
            typeof(IBattleAiScoreContext).GetProperty("skill_defs")?.PropertyType,
            typeof(IReadOnlyDictionary<StringName, SkillDef>),
            "IBattleAiScoreContext.skill_defs 不应继续暴露 GDictionary。"
        );
        _test.Eq(
            typeof(BattleSpecialProfilePreviewFacts).GetMethod(
                "ToTraceDictionary",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(Dictionary<string, object>),
            "BattleSpecialProfilePreviewFacts trace 导出不应继续回退到 ToDict() payload roundtrip。"
        );
        _test.Eq(
            typeof(MeteorSwarmNumericSummary).GetMethod(
                "ToTraceDictionary",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(Dictionary<string, object>),
            "MeteorSwarmNumericSummary trace 导出不应继续回退到 ToDictionary() payload roundtrip。"
        );
        _test.Eq(
            typeof(BattleAttackRollModifierSpec).GetMethod(
                "ToTraceDictionary",
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                Type.EmptyTypes,
                null
            )?.ReturnType,
            typeof(Dictionary<string, object>),
            "BattleAttackRollModifierSpec trace 导出不应继续回退到 payload modifier breakdown builder。"
        );
        _test.Eq(
            typeof(BattleAiScoreService.DamageSaveEstimate).GetMethod(
                "ToTraceDictionary",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(Dictionary<string, object>),
            "DamageSaveEstimate trace 导出不应继续回退到 ToDictionary() payload roundtrip。"
        );
        _test.Eq(
            typeof(BattleAiScoreService.DamageEstimateBreakdown).GetMethod(
                "ToTraceDictionary",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(Dictionary<string, object>),
            "DamageEstimateBreakdown trace 导出不应继续回退到 ToDictionary() payload roundtrip。"
        );
        _test.Eq(
            typeof(BattleAiScoreProfile)
                .GetProperty("ActionBaseScoresTyped", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.PropertyType,
            typeof(IReadOnlyDictionary<StringName, int>),
            "BattleAiScoreProfile.action_base_scores 不应继续以 Godot Dictionary 作为 owner 业务态。"
        );
        _test.Eq(
            typeof(BattleAiScoreProfile)
                .GetProperty("BucketPrioritiesTyped", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.PropertyType,
            typeof(IReadOnlyDictionary<StringName, int>),
            "BattleAiScoreProfile.bucket_priorities 不应继续以 Godot Dictionary 作为 owner 业务态。"
        );
        _test.Eq(
            typeof(BattleSimProfileDef).GetField("ai_score_profile")?.FieldType,
            typeof(BattleAiScoreProfile),
            "BattleSimProfileDef.ai_score_profile 不应继续以 GodotObject 作为正式 score profile 资源边界。"
        );
        _test.Eq(
            typeof(BattleSimOverrideApplier)
                .GetMethod("ApplyProfileTyped", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.ReturnType,
            typeof(BattleSimOverrideApplyResult),
            "BattleSimOverrideApplier 应向 C# caller 暴露 typed apply result。"
        );
        _test.True(
            typeof(BattleSimOverrideApplier).GetMethod("apply_profile") == null,
            "BattleSimOverrideApplier 不应继续暴露 apply_profile GDictionary 边界。"
        );
        _test.Eq(
            typeof(BattleSimContentProvider)
                .GetMethod("GetSkillDefsTyped", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.ReturnType,
            typeof(IReadOnlyDictionary<StringName, SkillDef>),
            "BattleSimContentProvider.GetSkillDefsTyped() 不应继续退回 Godot Dictionary。"
        );
        _test.True(
            typeof(BattleSimContentProvider).GetMethod("get_skill_defs") == null
                && typeof(BattleSimContentProvider).GetMethod("get_enemy_templates") == null
                && typeof(BattleSimContentProvider).GetMethod("get_enemy_ai_brains") == null
                && typeof(BattleSimContentProvider).GetMethod("dispose") == null,
            "BattleSimContentProvider 不应继续暴露 snake_case Godot helper surface。"
        );
        _test.Eq(
            typeof(BattleSimRunner).GetMethod("RunScenario")?.ReturnType,
            typeof(BattleSimScenarioReport),
            "BattleSimRunner.RunScenario() 不应继续返回 GDictionary report。"
        );
        _test.Eq(
            typeof(BattleSimRunner).GetMethod("RunScenario")?.GetParameters()[1].ParameterType,
            typeof(IReadOnlyList<BattleSimProfileDef>),
            "BattleSimRunner.RunScenario() 不应继续接收 Godot Array profile 列表。"
        );
        _test.True(
            typeof(BattleSimReportBuilder).GetMethod("build_profile_summary") == null
                && typeof(BattleSimReportBuilder).GetMethod("build_profile_comparisons") == null,
            "BattleSimReportBuilder 不应继续暴露 snake_case GDScript helper。"
        );
        _test.Eq(
            typeof(BattleSimReportBuilder).GetMethod("BuildProfileSummary")?.ReturnType,
            typeof(BattleSimProfileSummary),
            "BattleSimReportBuilder.BuildProfileSummary() 不应继续返回 GDictionary。"
        );
        _test.Eq(
            typeof(BattleSimReportBuilder).GetMethod("BuildProfileComparisons")?.ReturnType,
            typeof(List<BattleSimProfileComparison>),
            "BattleSimReportBuilder.BuildProfileComparisons() 不应继续返回 Godot Array。"
        );
        _test.True(
            typeof(BattleSimTraceSummaryBuilder).GetMethod("has_traces") == null
                && typeof(BattleSimTraceSummaryBuilder).GetMethod("build") == null,
            "BattleSimTraceSummaryBuilder 不应继续暴露 snake_case GDScript helper。"
        );
        _test.True(
            typeof(BattleSimTraceSummaryBuilder).GetMethod(
                "Build",
                new[]
                {
                    typeof(BattleSimScenarioReport),
                    typeof(string),
                    typeof(BattleSimTraceSummaryBuilder.TraceSummaryOptionsData),
                }
            ) != null,
            "BattleSimTraceSummaryBuilder 应继续暴露 typed BattleSimScenarioReport overload。"
        );
        _test.True(
            typeof(BattleSimTraceSummaryBuilder).GetMethod(
                "HasTraces",
                new[] { typeof(GDictionary) }
            ) == null
                && typeof(BattleSimTraceSummaryBuilder).GetMethod(
                    "Build",
                    new[] { typeof(GDictionary), typeof(string), typeof(GDictionary) }
                ) == null,
            "BattleSimTraceSummaryBuilder 不应继续保留 top-level report GDictionary overload。"
        );
        _test.Eq(
            typeof(BattleCommand).GetProperty(
                "EquipmentOccupiedSlotIdsTyped",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.PropertyType,
            typeof(IReadOnlyList<StringName>),
            "BattleCommand occupied-slot runtime 业务态应保持 internal typed list。"
        );
        _test.Eq(
            typeof(BattleCommand).GetProperty(
                "TargetUnitIdsTyped",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.PropertyType,
            typeof(IReadOnlyList<StringName>),
            "BattleCommand target-unit runtime 业务态应保持 internal typed list。"
        );
        _test.Eq(
            typeof(BattleCommand).GetProperty(
                "TargetCoordsTyped",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.PropertyType,
            typeof(IReadOnlyList<Vector2I>),
            "BattleCommand target-coord runtime 业务态应保持 internal typed list。"
        );
        _test.Eq(
            typeof(BattleCommand).GetField("equipment_instance")?.FieldType,
            typeof(EquipmentInstanceState),
            "BattleCommand.equipment_instance 不应继续退回 GDictionary。"
        );
        _test.Eq(
            typeof(BattlePreview).GetProperty(
                "TargetUnitIdsTyped",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.PropertyType,
            typeof(IReadOnlyList<StringName>),
            "BattlePreview target-unit runtime 业务态应保持 internal typed list。"
        );
        _test.Eq(
            typeof(BattlePreview).GetProperty(
                "TargetCoordsTyped",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.PropertyType,
            typeof(IReadOnlyList<Vector2I>),
            "BattlePreview target-coord runtime 业务态应保持 internal typed list。"
        );
        _test.Eq(
            typeof(BattlePreview).GetProperty(
                "RandomChainCandidateUnitIdsTyped",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.PropertyType,
            typeof(IReadOnlyList<StringName>),
            "BattlePreview random-chain candidate runtime 业务态应保持 internal typed list。"
        );
        _test.Eq(
            typeof(BattlePreview).GetProperty(
                "LogLinesTyped",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.PropertyType,
            typeof(IReadOnlyList<string>),
            "BattlePreview log-lines runtime 业务态应保持 internal typed list。"
        );
        _test.Eq(
            typeof(BattlePreview).GetProperty(
                "DamagePreviewTyped",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.PropertyType,
            typeof(BattleDamagePreviewRangeService.SkillDamagePreview?),
            "BattlePreview.damage_preview 不应继续退回 GDictionary 业务态。"
        );
        _test.Eq(
            typeof(BattleEventBatch).GetProperty(
                "ChangedUnitIdsTyped",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.PropertyType,
            typeof(IReadOnlyList<StringName>),
            "BattleEventBatch changed-unit runtime 业务态应保持 internal typed list。"
        );
        _test.Eq(
            typeof(BattleEventBatch).GetProperty(
                "ChangedCoordsTyped",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.PropertyType,
            typeof(IReadOnlyList<Vector2I>),
            "BattleEventBatch changed-coord runtime 业务态应保持 internal typed list。"
        );
        _test.Eq(
            typeof(BattleEventBatch).GetProperty(
                "LogLinesTyped",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.PropertyType,
            typeof(IReadOnlyList<string>),
            "BattleEventBatch log-lines runtime 业务态应保持 internal typed list。"
        );
        _test.Eq(
            typeof(BattleEventBatch).GetProperty(
                "ReportEntriesTyped",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.PropertyType,
            typeof(IReadOnlyList<GDictionary>),
            "BattleEventBatch report-entry runtime 业务态应保持 internal typed list。"
        );
        _test.Eq(
            typeof(BattleEventBatch).GetProperty(
                "ProgressionDeltasTyped",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.PropertyType,
            typeof(IReadOnlyList<CharacterProgressionDelta>),
            "BattleEventBatch progression-delta runtime 业务态应保持 internal typed list。"
        );
        _test.True(
            typeof(BattleEventBatch).GetMethod("clear") == null,
            "BattleEventBatch 不应继续暴露 zero-caller 的 legacy clear helper。"
        );
        _test.Eq(
            typeof(CharacterProgressionDelta).GetProperty(
                "ChangedProfessionIdsTyped",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.PropertyType,
            typeof(IReadOnlyList<StringName>),
            "CharacterProgressionDelta changed-profession runtime 业务态应保持 internal typed list。"
        );
        _test.Eq(
            typeof(UnitProgress).GetProperty(
                "SkillsTyped",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.PropertyType,
            typeof(IReadOnlyDictionary<StringName, UnitSkillProgress>),
            "UnitProgress.skills 业务态应保持 internal typed dictionary。"
        );
        _test.Eq(
            typeof(UnitProgress).GetProperty(
                "PendingProfessionChoicesTyped",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.PropertyType,
            typeof(IReadOnlyList<PendingProfessionChoice>),
            "UnitProgress.pending_profession_choices 业务态应保持 internal typed list。"
        );
        _test.Eq(
            typeof(UnitProgress).GetProperty(
                "KnownKnowledgeIdsTyped",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.PropertyType,
            typeof(IReadOnlyList<StringName>),
            "UnitProgress.known_knowledge_ids 业务态应保持 internal typed list。"
        );
        _test.Eq(
            typeof(UnitProgress).GetProperty(
                "ActiveCoreSkillIdsTyped",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.PropertyType,
            typeof(IReadOnlyList<StringName>),
            "UnitProgress.active_core_skill_ids 业务态应保持 internal typed list。"
        );
        _test.Eq(
            typeof(UnitProgress).GetProperty(
                "AttributeGrowthProgressTyped",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.PropertyType,
            typeof(IReadOnlyDictionary<StringName, int>),
            "UnitProgress.attribute_growth_progress 业务态应保持 internal typed dictionary。"
        );
        _test.Eq(
            typeof(UnitProgress).GetProperty(
                "AchievementProgressTyped",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.PropertyType,
            typeof(IReadOnlyDictionary<StringName, AchievementProgressState>),
            "UnitProgress.achievement_progress 业务态应保持 internal typed dictionary。"
        );
        _test.Eq(
            typeof(UnitProgress).GetProperty(
                "ProfessionsTyped",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.PropertyType,
            typeof(IReadOnlyDictionary<StringName, UnitProfessionProgress>),
            "UnitProgress.professions 业务态应保持 internal typed dictionary。"
        );
        _test.Eq(
            typeof(UnitProgress).GetProperty(
                "BlockedRelearnSkillIdsTyped",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.PropertyType,
            typeof(IReadOnlyList<StringName>),
            "UnitProgress.blocked_relearn_skill_ids 业务态应保持 internal typed list。"
        );
        _test.Eq(
            typeof(UnitProgress).GetProperty(
                "MergedSkillSourceMapTyped",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.PropertyType,
            typeof(IReadOnlyDictionary<StringName, List<StringName>>),
            "UnitProgress.merged_skill_source_map 业务态应保持 internal typed dictionary。"
        );
        _test.Eq(
            typeof(UnitProgress).GetProperty(
                "UnlockedCombatResourceIdsTyped",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.PropertyType,
            typeof(IReadOnlyList<StringName>),
            "UnitProgress.unlocked_combat_resource_ids 业务态应保持 internal typed list。"
        );
        _test.Eq(
            typeof(UnitProgress).GetProperty(
                "LockedLevelTriggerSkillIdsTyped",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.PropertyType,
            typeof(IReadOnlyList<StringName>),
            "UnitProgress.locked_level_trigger_skill_ids 业务态应保持 internal typed list。"
        );
        _test.Eq(
            typeof(SkillDef).GetProperty(
                "TagsTyped",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.PropertyType,
            typeof(IReadOnlyList<StringName>),
            "SkillDef.tags 业务态应保持 internal typed list。"
        );
        _test.Eq(
            typeof(SkillDef).GetProperty(
                "LearnRequirementsTyped",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.PropertyType,
            typeof(IReadOnlyList<StringName>),
            "SkillDef.learn_requirements 业务态应保持 internal typed list。"
        );
        _test.Eq(
            typeof(SkillDef).GetProperty(
                "KnowledgeRequirementsTyped",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.PropertyType,
            typeof(IReadOnlyList<StringName>),
            "SkillDef.knowledge_requirements 业务态应保持 internal typed list。"
        );
        _test.Eq(
            typeof(SkillDef).GetProperty(
                "SkillLevelRequirementsTyped",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.PropertyType,
            typeof(IReadOnlyDictionary<StringName, int>),
            "SkillDef.skill_level_requirements 业务态应保持 internal typed dictionary。"
        );
        _test.Eq(
            typeof(SkillDef).GetProperty(
                "SkillLevelRequirementEntriesTyped",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.PropertyType,
            typeof(IReadOnlyList<SkillDef.IntRequirementEntryData>),
            "SkillDef.skill_level_requirements 校验态应保持 internal typed entry list。"
        );
        _test.Eq(
            typeof(SkillDef).GetProperty(
                "AttributeRequirementsTyped",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.PropertyType,
            typeof(IReadOnlyDictionary<StringName, int>),
            "SkillDef.attribute_requirements 业务态应保持 internal typed dictionary。"
        );
        _test.Eq(
            typeof(SkillDef).GetProperty(
                "AttributeRequirementEntriesTyped",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.PropertyType,
            typeof(IReadOnlyList<SkillDef.IntRequirementEntryData>),
            "SkillDef.attribute_requirements 校验态应保持 internal typed entry list。"
        );
        _test.Eq(
            typeof(SkillDef).GetProperty(
                "AchievementRequirementsTyped",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.PropertyType,
            typeof(IReadOnlyList<StringName>),
            "SkillDef.achievement_requirements 业务态应保持 internal typed list。"
        );
        _test.Eq(
            typeof(SkillDef).GetProperty(
                "UpgradeSourceSkillIdsTyped",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.PropertyType,
            typeof(IReadOnlyList<StringName>),
            "SkillDef.upgrade_source_skill_ids 业务态应保持 internal typed list。"
        );
        _test.Eq(
            typeof(SkillDef).GetProperty(
                "MasterySourcesTyped",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.PropertyType,
            typeof(IReadOnlyList<StringName>),
            "SkillDef.mastery_sources 业务态应保持 internal typed list。"
        );
        _test.Eq(
            typeof(SkillDef).GetProperty(
                "LevelDescriptionConfigsTyped",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.PropertyType,
            typeof(IReadOnlyDictionary<int, Dictionary<string, Variant>>),
            "SkillDef.level_description_configs 业务态应保持 internal typed dictionary。"
        );
        _test.Eq(
            typeof(SkillDef).GetProperty(
                "LevelDescriptionConfigEntriesTyped",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.PropertyType,
            typeof(IReadOnlyList<SkillDef.LevelDescriptionConfigEntryData>),
            "SkillDef.level_description_configs 校验态应保持 internal typed entry list。"
        );
        _test.Eq(
            typeof(SkillDef).GetProperty(
                "AttributeModifiersTyped",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.PropertyType,
            typeof(IReadOnlyList<AttributeModifier>),
            "SkillDef.attribute_modifiers 业务态应保持 internal typed list。"
        );
        _test.Eq(
            typeof(SkillDef).GetProperty(
                "AttributeGrowthProgressTyped",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.PropertyType,
            typeof(IReadOnlyDictionary<StringName, int>),
            "SkillDef.attribute_growth_progress 业务态应保持 internal typed dictionary。"
        );
        _test.Eq(
            typeof(SkillDef).GetProperty(
                "AttributeGrowthProgressEntriesTyped",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.PropertyType,
            typeof(IReadOnlyList<SkillDef.AttributeGrowthProgressEntryData>),
            "SkillDef.attribute_growth_progress 校验态应保持 internal typed entry list。"
        );
        _test.Eq(
            typeof(CharacterProgressionDelta).GetProperty(
                "PendingProfessionChoicesTyped",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.PropertyType,
            typeof(IReadOnlyList<PendingProfessionChoice>),
            "CharacterProgressionDelta pending-choice runtime 业务态应保持 internal typed list。"
        );
        _test.Eq(
            typeof(CharacterProgressionDelta).GetProperty(
                "MasteryChangesTyped",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.PropertyType,
            typeof(IReadOnlyList<CharacterMasteryChangeFact>),
            "CharacterProgressionDelta mastery runtime 业务态应保持 internal typed mastery fact list。"
        );
        _test.Eq(
            typeof(CharacterProgressionDelta).GetProperty(
                "KnowledgeChangesTyped",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.PropertyType,
            typeof(IReadOnlyList<CharacterKnowledgeChangeFact>),
            "CharacterProgressionDelta knowledge runtime 业务态应保持 internal typed knowledge fact list。"
        );
        _test.Eq(
            typeof(CharacterProgressionDelta).GetProperty(
                "AttributeChangesTyped",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.PropertyType,
            typeof(IReadOnlyList<CharacterAttributeChangeFact>),
            "CharacterProgressionDelta attribute runtime 业务态应保持 internal typed attribute fact list。"
        );
        _test.Eq(
            typeof(PendingProfessionChoice).GetProperty(
                "TriggerSkillIdsTyped",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.PropertyType,
            typeof(IReadOnlyList<StringName>),
            "PendingProfessionChoice.trigger_skill_ids 业务态应保持 internal typed list。"
        );
        _test.Eq(
            typeof(PendingProfessionChoice).GetProperty(
                "CandidateProfessionIdsTyped",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.PropertyType,
            typeof(IReadOnlyList<StringName>),
            "PendingProfessionChoice.candidate_profession_ids 业务态应保持 internal typed list。"
        );
        _test.Eq(
            typeof(PendingProfessionChoice).GetProperty(
                "TargetRankMapTyped",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.PropertyType,
            typeof(IReadOnlyDictionary<StringName, int>),
            "PendingProfessionChoice.target_rank_map 业务态应保持 internal typed dictionary。"
        );
        _test.Eq(
            typeof(PendingProfessionChoice).GetProperty(
                "QualifierSkillPoolIdsTyped",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.PropertyType,
            typeof(IReadOnlyList<StringName>),
            "PendingProfessionChoice.qualifier_skill_pool_ids 业务态应保持 internal typed list。"
        );
        _test.Eq(
            typeof(PendingProfessionChoice).GetProperty(
                "AssignableSkillCandidateIdsTyped",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.PropertyType,
            typeof(IReadOnlyList<StringName>),
            "PendingProfessionChoice.assignable_skill_candidate_ids 业务态应保持 internal typed list。"
        );

        Type damageResolverType = typeof(BattleDamageResolver);
        _test.Eq(
            damageResolverType.GetField("_skillDefIndex", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.FieldType,
            typeof(Dictionary<StringName, SkillDef>),
            "BattleDamageResolver skill catalog 应保持 typed Dictionary<StringName,SkillDef> 业务态。"
        );

        _test.True(
            damageResolverType.GetMethod("SetSkillDefs", new[] { typeof(GDictionary) }) == null,
            "BattleDamageResolver 不应继续保留 GDictionary skill catalog setup overload。"
        );
        _test.True(
            damageResolverType.GetMethod(
                "SetSkillDefs",
                new[] { typeof(IReadOnlyDictionary<StringName, SkillDef>) }
            ) == null,
            "BattleDamageResolver 不应继续把 typed skill catalog setup 暴露为 public。"
        );
        _test.Eq(
            damageResolverType.GetMethod(
                "SetSkillDefs",
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new[] { typeof(IReadOnlyDictionary<StringName, SkillDef>) },
                null
            )?.GetParameters()[0].ParameterType,
            typeof(IReadOnlyDictionary<StringName, SkillDef>),
            "BattleDamageResolver 应继续保留 nonpublic typed skill catalog setup。"
        );
        _test.True(
            damageResolverType.GetMethod(
                "resolve_fall_damage",
                new[] { typeof(BattleUnitState), typeof(int) }
            ) == null,
            "BattleDamageResolver 不应继续保留 public 坠落伤害字典入口。"
        );
        _test.Eq(
            damageResolverType.GetMethod(
                "ResolveFallDamageResult",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(AttackEffectResolutionResult),
            "BattleDamageResolver 坠落伤害应只保留 internal typed 结果入口。"
        );

        BattleDamageResolver damageResolver = new();
        StringName skillId = "typed_damage_resolver_skill";
        SkillDef skillDef = new() { skill_id = skillId, display_name = "Typed Damage Resolver Skill" };
        var validSkillDefs = new Dictionary<StringName, SkillDef> { [skillId] = skillDef };
        damageResolver.SetSkillDefs(validSkillDefs);
        _test.True(
            damageResolverType.GetField("_skillDefIndex", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(damageResolver) is Dictionary<StringName, SkillDef> typedSkillIndex
                && typedSkillIndex.ContainsKey(skillId),
            "BattleDamageResolver typed skill catalog setup 应继续写回正式 typed skill index。"
        );
        _test.True(
            typeof(BattleDamageResolver).GetNestedType(
                "TraitTriggerResultSnapshot",
                BindingFlags.NonPublic
            )?.GetMethod("FromDictionary", BindingFlags.Public | BindingFlags.Static) == null,
            "BattleDamageResolver.TraitTriggerResultSnapshot 不应继续暴露 zero-caller 的 public parse bridge。"
        );
        _test.True(
            typeof(BattleDamageResolver).GetNestedType(
                "TraitTriggerResultSnapshot",
                BindingFlags.NonPublic
            )?.GetMethod(
                "FromAttackTraitTriggerResult",
                BindingFlags.Public | BindingFlags.Static
            ) == null,
            "BattleDamageResolver.TraitTriggerResultSnapshot 不应继续暴露 owner-only 的 public typed factory。"
        );
        _test.True(
            typeof(BattleDamageResolver).GetNestedType(
                "DamageApplicationInput",
                BindingFlags.NonPublic
            )?.GetMethod("FromDictionary", BindingFlags.Public | BindingFlags.Static) == null
                && typeof(BattleDamageResolver).GetNestedType(
                    "DamageApplicationInput",
                    BindingFlags.NonPublic
                )?.GetMethod("ReadBool", BindingFlags.Public | BindingFlags.Static) == null
                && typeof(BattleDamageResolver).GetNestedType(
                    "DamageResolutionContext",
                    BindingFlags.NonPublic
                )?.GetMethod("FromDictionary", BindingFlags.Public | BindingFlags.Static) == null
                && typeof(BattleDamageResolver).GetNestedType(
                    "SpellControlCheckContext",
                    BindingFlags.NonPublic
                )?.GetMethod("FromDictionary", BindingFlags.Public | BindingFlags.Static) == null
                && typeof(BattleDamageResolver).GetNestedType(
                    "DamageDiceEventSnapshot",
                    BindingFlags.NonPublic
                )?.GetMethod("FromDictionary", BindingFlags.Public | BindingFlags.Static) == null,
            "BattleDamageResolver 不应继续暴露 file-local 的 nested parse helper。"
        );
        _test.True(
            typeof(BattleDamageResolver).GetNestedType(
                "EquipmentDurabilityDamageEffectResult",
                BindingFlags.NonPublic
            )?.GetMethod("ToDictionary", BindingFlags.Public | BindingFlags.Instance) == null
                && typeof(BattleDamageResolver).GetNestedType(
                    "DamagePreviewSaveEstimate",
                    BindingFlags.NonPublic
                )?.GetMethod("ToDictionary", BindingFlags.Public | BindingFlags.Instance) == null
                && typeof(BattleDamageResolver).GetNestedType(
                    "AppliedDamageResult",
                    BindingFlags.NonPublic
                )?.GetMethod("ToDictionary", BindingFlags.Public | BindingFlags.Instance) == null
                && typeof(BattleDamageResolver).GetNestedType(
                    "DamageOutcomeResult",
                    BindingFlags.NonPublic
                )?.GetMethod("ToDictionary", BindingFlags.Public | BindingFlags.Instance) == null
                && typeof(BattleDamageResolver).GetNestedType(
                    "ExecuteEffectResult",
                    BindingFlags.NonPublic
                )?.GetMethod("ToDictionary", BindingFlags.Public | BindingFlags.Instance) == null
                && typeof(BattleDamageResolver).GetNestedType(
                    "TraitTriggerResultSnapshot",
                    BindingFlags.NonPublic
                )?.GetMethod("ToDictionary", BindingFlags.Public | BindingFlags.Instance) == null
                && typeof(BattleDamageResolver).GetNestedType(
                    "DicePoolRollResult",
                    BindingFlags.NonPublic
                )?.GetMethod("ToDictionary", BindingFlags.Public | BindingFlags.Instance) == null,
            "BattleDamageResolver 不应继续暴露 file-local 的 nested projection helper。"
        );
    }

    private void TestBattleRuntimeHelperRegistrationShrinksToActualResourceTypes()
    {
        _test.True(
            typeof(BattleAttackCheckPolicyService).GetMethod(
                "Setup",
                BindingFlags.Public | BindingFlags.Instance
            ) == null
                && typeof(BattleAttackCheckPolicyService).GetMethod(
                    "Dispose",
                    BindingFlags.Public | BindingFlags.Instance
                ) == null,
            "BattleAttackCheckPolicyService 不应继续暴露 public lifecycle helper。"
        );
        _test.True(
            typeof(BattleMetricsCollector).GetMethod(
                "Setup",
                BindingFlags.Public | BindingFlags.Instance
            ) == null
                && typeof(BattleMetricsCollector).GetMethod(
                    "Dispose",
                    BindingFlags.Public | BindingFlags.Instance
                ) == null,
            "BattleMetricsCollector 不应继续暴露 public 的 lifecycle helper。"
        );
        _test.Eq(
            typeof(BattleMetricsCollector).GetMethod(
                "Setup",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.GetParameters()[0].ParameterType,
            typeof(BattleRuntimeModule),
            "BattleMetricsCollector 应保留同程序集可见的 typed setup 入口。"
        );
        _test.True(
            typeof(BattleMetricsCollector).GetMethod("BuildUnitMetricEntry") == null
                && typeof(BattleMetricsCollector).GetMethod("EnsureUnitMetricEntry") == null
                && typeof(BattleMetricsCollector).GetMethod("EnsureFactionMetricEntry") == null,
            "BattleMetricsCollector 不应继续暴露 public metrics helper。"
        );
        _test.True(
            typeof(BattleUnitState).GetField(
                "ai_blackboard",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleUnitState 不应继续暴露 public ai_blackboard field。"
        );
        _test.True(
            typeof(BattleAiUnitBlackboardSnapshot).GetMethod(
                "FromBlackboard",
                BindingFlags.Public | BindingFlags.Static
            ) == null,
            "BattleAiUnitBlackboardSnapshot 不应继续暴露 public BattleAiBlackboard parse helper。"
        );
        _test.True(
            typeof(BattleChargeResolver).GetMethod(
                "ValidateChargeCommandResult",
                BindingFlags.Public | BindingFlags.Instance
            ) == null
                && typeof(BattleChargeResolver).GetMethod(
                    "BuildChargeStepAoePreviewCoords",
                    BindingFlags.Public | BindingFlags.Instance
                ) == null
                && typeof(BattleChargeResolver).GetMethod(
                    "IsChargeOption",
                    BindingFlags.Public | BindingFlags.Instance
                ) == null,
            "BattleChargeResolver 不应继续暴露 public 的 charge helper。"
        );
        _test.Eq(
            typeof(BattleTimelineDriver).GetMethod(
                "Setup",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.GetParameters()[0].ParameterType,
            typeof(BattleRuntimeModule),
            "BattleTimelineDriver 应保留同程序集可见的 typed setup 入口。"
        );
        _test.Eq(
            typeof(BattleTimelineDriver).GetMethod(
                "AdvanceTimeline",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.GetParameters()[1].ParameterType,
            typeof(BattleEventBatch),
            "BattleTimelineDriver 应保留同程序集可见的 typed timeline 推进入口。"
        );
        _test.True(
            typeof(BattleTerrainGenerator).GetMethod(
                "generate",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { typeof(GDictionary) },
                null
            ) == null
                && typeof(BattleTerrainGenerator).GetMethod(
                    "generate",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    new[] { typeof(GDictionary), typeof(int) },
                    null
                ) == null
                && typeof(BattleTerrainGenerator).GetMethod(
                    "generate",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    new[] { typeof(GDictionary), typeof(int), typeof(GDictionary) },
                    null
                ) == null,
            "BattleTerrainGenerator 不应继续暴露 public 的 GDictionary encounter-context bridge。"
        );
        _test.Eq(
            typeof(BattleTerrainGenerator).GetMethod(
                "GenerateTyped",
                BindingFlags.Public | BindingFlags.Instance
            )?.GetParameters()[0].ParameterType,
            typeof(EncounterAnchorData),
            "BattleTerrainGenerator 正式入口应直接消费 typed EncounterAnchorData。"
        );
        _test.True(
            typeof(BattleTerrainGenerator).GetMethod(
                "_normalize_water_heights",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleTerrainGenerator 不应继续暴露 public 的 water-height normalization wrapper。"
        );
        _test.True(
            typeof(BattleGridService).GetMethod(
                "ApplyHeightDeltaResult",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleGridService 不应继续暴露 public 的高度差 typed 结果 helper。"
        );
        _test.Eq(
            typeof(BattleGridService).GetMethod(
                "ApplyHeightDeltaResult",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(BattleHeightDeltaResult),
            "BattleGridService 高度差结果应只保留 internal typed helper。"
        );
        _test.True(
            typeof(BattleGridService).GetMethod("IsWalkable") == null
                && typeof(BattleGridService).GetMethod("IsWallBlocked") == null
                && typeof(BattleGridService).GetMethod("ApplyHeightDelta") == null
                && typeof(BattleGridService).GetMethod("MoveUnit") == null,
            "BattleGridService 不应继续暴露 public 的 self-only / same-assembly terrain mutation helper。"
        );
        _test.True(
            typeof(BattleGridService).GetMethod(
                "GetUnitAtCoord",
                new[] { typeof(GodotObject), typeof(Vector2I) }
            ) == null
                && typeof(BattleGridService).GetMethod(
                    "GetNeighbors4",
                    new[] { typeof(GodotObject), typeof(Vector2I) }
                ) == null
                && typeof(BattleGridService).GetMethod(
                    "GetAreaCoords",
                    new[] { typeof(GodotObject), typeof(Vector2I), typeof(StringName), typeof(int) }
                ) == null
                && typeof(BattleGridService).GetMethod(
                    "GetAreaCoords",
                    new[]
                    {
                        typeof(GodotObject),
                        typeof(Vector2I),
                        typeof(StringName),
                        typeof(int),
                        typeof(Vector2I),
                    }
                ) == null
                && typeof(BattleGridService).GetMethod(
                    "ResolveUnitMovePath",
                    new[]
                    {
                        typeof(GodotObject),
                        typeof(BattleUnitState),
                        typeof(Vector2I),
                        typeof(Vector2I),
                        typeof(int),
                    }
                ) == null
                && typeof(BattleGridService).GetMethod(
                    "BuildUnitMovePathTree",
                    new[]
                    {
                        typeof(GodotObject),
                        typeof(BattleUnitState),
                        typeof(Vector2I),
                        typeof(int),
                    }
                ) == null
                && typeof(BattleGridService).GetMethod(
                    "EvaluateMove",
                    new[]
                    {
                        typeof(GodotObject),
                        typeof(Vector2I),
                        typeof(Vector2I),
                        typeof(BattleUnitState),
                    }
                ) == null,
            "BattleGridService 不应继续暴露 public 的 GodotObject terrain query overload。"
        );
        _test.Eq(
            typeof(BattleGridService).GetMethod(
                "GetUnitAtCoord",
                new[] { typeof(BattleState), typeof(Vector2I) }
            ),
            null,
            "BattleGridService 不应继续暴露 public 的 typed unit lookup helper。"
        );
        _test.Eq(
            typeof(BattleGridService).GetMethod(
                "GetNeighbors4",
                new[] { typeof(BattleState), typeof(Vector2I) }
            ),
            null,
            "BattleGridService 不应继续暴露 public 的 typed neighbor query helper。"
        );
        _test.Eq(
            typeof(BattleGridService).GetMethod(
                "GetAreaCoords",
                new[] { typeof(BattleState), typeof(Vector2I), typeof(StringName), typeof(int) }
            ),
            null,
            "BattleGridService 不应继续暴露 public 的 typed area query helper。"
        );
        _test.Eq(
            typeof(BattleGridService).GetMethod(
                "GetAreaCoords",
                new[]
                {
                    typeof(BattleState),
                    typeof(Vector2I),
                    typeof(StringName),
                    typeof(int),
                    typeof(Vector2I),
                }
            ),
            null,
            "BattleGridService 不应继续暴露 public 的 typed directional area query helper。"
        );
        _test.Eq(
            typeof(BattleGridService).GetMethod(
                "ResolveUnitMovePath",
                new[]
                {
                    typeof(BattleState),
                    typeof(BattleUnitState),
                    typeof(Vector2I),
                    typeof(Vector2I),
                    typeof(int),
                }
            ),
            null,
            "BattleGridService 不应继续暴露 public 的 path 查询字典入口。"
        );
        _test.Eq(
            typeof(BattleGridService).GetMethod(
                "BuildUnitMovePathTree",
                new[]
                {
                    typeof(BattleState),
                    typeof(BattleUnitState),
                    typeof(Vector2I),
                    typeof(int),
                }
            ),
            null,
            "BattleGridService 不应继续暴露 public 的 path tree 查询字典入口。"
        );
        _test.True(
            typeof(BattleGridService).GetMethod(
                "EvaluateMove",
                new[] { typeof(BattleState), typeof(Vector2I), typeof(Vector2I), typeof(BattleUnitState) }
            ) == null,
            "BattleGridService 不应继续暴露 public 的 move evaluation 字典入口。"
        );
        _test.True(
            typeof(BattleEdgeService).GetMethod(
                "BuildEdgeFacesForCells",
                BindingFlags.Public | BindingFlags.Instance
            ) == null
                && typeof(BattleEdgeService).GetMethod(
                    "GetEdgeFaceFromCache",
                    BindingFlags.Public | BindingFlags.Instance
                ) == null
                && typeof(BattleEdgeService).GetMethod(
                    "IsTraversableInCache",
                    BindingFlags.Public | BindingFlags.Instance
                ) == null,
            "BattleEdgeService 不应继续暴露 public 的 edge-cache GDictionary bridge。"
        );
        _test.Eq(
            typeof(BattleEdgeService).GetMethod(
                "BuildEdgeFacesForCells",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(GDictionary),
            "BattleEdgeService 应继续保留 nonpublic edge-face cache builder。"
        );
        _test.Eq(
            typeof(BattleEdgeService).GetMethod(
                "IsTraversableInCache",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(bool),
            "BattleEdgeService 应继续保留 nonpublic edge-cache traversability helper。"
        );
        _test.Eq(
            typeof(BattleGridService).GetMethod(
                "ApplyHeightDelta",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(bool),
            "BattleGridService 应继续保留 nonpublic ApplyHeightDelta helper。"
        );
        _test.Eq(
            typeof(BattleGridService).GetMethod(
                "MoveUnit",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(bool),
            "BattleGridService 应继续保留 nonpublic MoveUnit helper。"
        );
        _test.True(
            typeof(BattleGridService).GetMethod(
                "SetOccupantsTyped",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleGridService 不应继续暴露 public 的 typed 占位写入 helper。"
        );
        _test.True(
            typeof(BattleGridService).GetMethod(
                "ClearUnitOccupancy",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleGridService 不应继续暴露 public 的 occupancy 清理 helper。"
        );
        _test.True(
            typeof(BattleGridService).GetMethod(
                "GetCellBaseTerrainId",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleGridService 不应继续暴露 public 的 base terrain 查询 helper。"
        );
        _test.Eq(
            typeof(BattleGridService).GetMethod(
                "GetCellBaseTerrainId",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(StringName),
            "BattleGridService 应保留 internal typed 的 base terrain 查询 helper。"
        );
        _test.True(
            typeof(BattleGridService).GetMethod(
                "GetColumnCells",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleGridService 不应继续暴露 zero-caller 的 column cell 查询 helper。"
        );
        _test.True(
            typeof(BattleGridService).GetMethod(
                "HasCell",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleGridService 不应继续暴露 public 的 cell presence helper。"
        );
        _test.True(
            typeof(BattleGridService).GetMethod(
                "GetCellState",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleGridService 不应继续暴露 public 的 cell lookup helper。"
        );
        _test.True(
            typeof(BattleCellState).GetMethod(
                "ToDictionary",
                BindingFlags.Public | BindingFlags.Instance
            ) == null
                && typeof(BattleCellState).GetMethod(
                    "FromDictionary",
                    BindingFlags.Public | BindingFlags.Static
                ) == null,
            "BattleCellState 不应继续暴露 public 的 dictionary parse/projection bridge。"
        );
        _test.Eq(
            typeof(BattleCellState).GetMethod(
                "ToDictionary",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(GDictionary),
            "BattleCellState 应保留同程序集可见的 dictionary projection helper。"
        );
        _test.Eq(
            typeof(BattleCellState).GetMethod(
                "FromDictionary",
                BindingFlags.NonPublic | BindingFlags.Static
            )?.ReturnType,
            typeof(BattleCellState),
            "BattleCellState 应保留同程序集可见的 dictionary parse helper。"
        );
        _test.True(
            typeof(BattleCellState).GetMethod(
                "CloneColumns",
                BindingFlags.Public | BindingFlags.Static
            ) == null
                && typeof(BattleCellState).GetMethod(
                    "BuildStackedCellsFromSurfaceCell",
                    BindingFlags.Public | BindingFlags.Static
                ) == null,
            "BattleCellState 不应继续暴露 public 的 column helper。"
        );
        _test.Eq(
            typeof(BattleCellState).GetMethod(
                "CloneColumns",
                BindingFlags.NonPublic | BindingFlags.Static
            )?.ReturnType,
            typeof(GDictionary),
            "BattleCellState 应保留同程序集可见的 column clone helper。"
        );
        _test.Eq(
            typeof(BattleCellState).GetMethod(
                "BuildStackedCellsFromSurfaceCell",
                BindingFlags.NonPublic | BindingFlags.Static
            )?.ReturnType,
            typeof(Godot.Collections.Array<BattleCellState>),
            "BattleCellState 应保留同程序集可见的 stacked-cell builder。"
        );
        _test.True(
            typeof(BattleEdgeFeatureState).GetMethod(
                "ToDictionary",
                BindingFlags.Public | BindingFlags.Instance
            ) == null
                && typeof(BattleEdgeFeatureState).GetMethod(
                    "FromDictionary",
                    BindingFlags.Public | BindingFlags.Static
                ) == null,
            "BattleEdgeFeatureState 不应继续暴露 public 的 dictionary parse/projection bridge。"
        );
        _test.Eq(
            typeof(BattleEdgeFeatureState).GetMethod(
                "ToDictionary",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(GDictionary),
            "BattleEdgeFeatureState 应保留同程序集可见的 dictionary projection helper。"
        );
        _test.Eq(
            typeof(BattleEdgeFeatureState).GetMethod(
                "FromDictionary",
                BindingFlags.NonPublic | BindingFlags.Static
            )?.ReturnType,
            typeof(BattleEdgeFeatureState),
            "BattleEdgeFeatureState 应保留同程序集可见的 dictionary parse helper。"
        );
        _test.True(
            typeof(BattleTerrainEffectState).GetMethod(
                "ToDictionary",
                BindingFlags.Public | BindingFlags.Instance
            ) == null
                && typeof(BattleTerrainEffectState).GetMethod(
                    "FromDictionary",
                    BindingFlags.Public | BindingFlags.Static
                ) == null,
            "BattleTerrainEffectState 不应继续暴露 public 的 dictionary parse/projection bridge。"
        );
        _test.Eq(
            typeof(BattleTerrainEffectState).GetMethod(
                "ToDictionary",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(GDictionary),
            "BattleTerrainEffectState 应保留同程序集可见的 dictionary projection helper。"
        );
        _test.Eq(
            typeof(BattleTerrainEffectState).GetMethod(
                "FromDictionary",
                BindingFlags.NonPublic | BindingFlags.Static
            )?.ReturnType,
            typeof(BattleTerrainEffectState),
            "BattleTerrainEffectState 应保留同程序集可见的 dictionary parse helper。"
        );
        _test.True(
            typeof(BattleTimelineState).GetMethod(
                "ToDictionary",
                BindingFlags.Public | BindingFlags.Instance
            ) == null
                && typeof(BattleTimelineState).GetMethod(
                    "FromDictionary",
                    BindingFlags.Public | BindingFlags.Static
                ) == null,
            "BattleTimelineState 不应继续暴露 public 的 dictionary parse/projection bridge。"
        );
        _test.Eq(
            typeof(BattleTimelineState).GetMethod(
                "ToDictionary",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(GDictionary),
            "BattleTimelineState 应保留同程序集可见的 dictionary projection helper。"
        );
        _test.Eq(
            typeof(BattleTimelineState).GetMethod(
                "FromDictionary",
                BindingFlags.NonPublic | BindingFlags.Static
            )?.ReturnType,
            typeof(BattleTimelineState),
            "BattleTimelineState 应保留同程序集可见的 dictionary parse helper。"
        );
        _test.True(
            typeof(BattleStatusEffectState).GetMethod(
                "ToDictionary",
                BindingFlags.Public | BindingFlags.Instance
            ) == null
                && typeof(BattleStatusEffectState).GetMethod(
                    "FromDictionary",
                    BindingFlags.Public | BindingFlags.Static
                ) == null,
            "BattleStatusEffectState 不应继续暴露 public 的 dictionary parse/projection bridge。"
        );
        _test.Eq(
            typeof(BattleStatusEffectState).GetMethod(
                "ToDictionary",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(GDictionary),
            "BattleStatusEffectState 应保留同程序集可见的 dictionary projection helper。"
        );
        _test.Eq(
            typeof(BattleStatusEffectState).GetMethod(
                "FromDictionary",
                BindingFlags.NonPublic | BindingFlags.Static
            )?.ReturnType,
            typeof(BattleStatusEffectState),
            "BattleStatusEffectState 应保留同程序集可见的 dictionary parse helper。"
        );
        _test.Eq(
            typeof(BattleGridService).GetMethod(
                "GetCellState",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(BattleCellState),
            "BattleGridService 应保留 internal typed 的 cell lookup helper。"
        );
        _test.True(
            typeof(BattleGridService).GetMethod(
                "IsInside",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleGridService 不应继续暴露 public 的 inside-boundary helper。"
        );
        _test.Eq(
            typeof(BattleGridService).GetMethod(
                "IsInside",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(bool),
            "BattleGridService 应保留 internal typed 的 inside-boundary helper。"
        );
        _test.True(
            typeof(BattleGridService).GetMethod(
                "CanPlaceFootprint",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleGridService 不应继续暴露 public 的 footprint placement helper。"
        );
        _test.True(
            typeof(BattleGridService).GetMethod(
                "CanPlaceUnit",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleGridService 不应继续暴露 public 的 unit placement helper。"
        );
        _test.Eq(
            typeof(BattleGridService).GetMethod(
                "CanPlaceFootprint",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(bool),
            "BattleGridService 应保留同程序集可见的 footprint placement helper。"
        );
        _test.Eq(
            typeof(BattleGridService).GetMethod(
                "CanPlaceUnit",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(bool),
            "BattleGridService 应保留同程序集可见的 unit placement helper。"
        );
        _test.True(
            typeof(BattleGridService).GetMethod(
                "CanTraverse",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleGridService 不应继续暴露 public 的 traverse helper。"
        );
        _test.Eq(
            typeof(BattleGridService).GetMethod(
                "CanTraverse",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(bool),
            "BattleGridService 应保留同程序集可见的 traverse helper。"
        );
        _test.True(
            typeof(BattleGridService).GetMethod(
                "GetEdgeFace",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleGridService 不应继续暴露 public 的 edge-face query helper。"
        );
        _test.Eq(
            typeof(BattleGridService).GetMethod(
                "GetEdgeFace",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(BattleEdgeFaceState),
            "BattleGridService 应保留同程序集可见的 edge-face query helper。"
        );
        _test.True(
            typeof(BattleGridService).GetMethod(
                "CanUnitStepBetweenAnchors",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleGridService 不应继续暴露 public 的 anchor-step helper。"
        );
        _test.Eq(
            typeof(BattleGridService).GetMethod(
                "CanUnitStepBetweenAnchors",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(bool),
            "BattleGridService 应保留同程序集可见的 anchor-step helper。"
        );
        _test.True(
            typeof(BattleGridService).GetMethod(
                "GetUnitMoveCost",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleGridService 不应继续暴露 public 的 unit move cost helper。"
        );
        _test.Eq(
            typeof(BattleGridService).GetMethod(
                "GetUnitMoveCost",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(int),
            "BattleGridService 应保留同程序集可见的 unit move cost helper。"
        );
        _test.Eq(
            typeof(BattleGridService).GetMethod(
                "GetNeighbors4",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(Godot.Collections.Array<Godot.Vector2I>),
            "BattleGridService 应保留同程序集可见的 typed neighbor query helper。"
        );
        _test.True(
            typeof(BattleGridService).GetMethod(
                "GetFootprintCoords",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleGridService 不应继续暴露 public 的 footprint coords helper。"
        );
        _test.Eq(
            typeof(BattleGridService).GetMethod(
                "GetFootprintCoords",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(Godot.Collections.Array<Godot.Vector2I>),
            "BattleGridService 应保留同程序集可见的 footprint coords helper。"
        );
        _test.True(
            typeof(BattleGridService).GetMethod(
                "GetUnitTargetCoords",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleGridService 不应继续暴露 public 的 unit target coords helper。"
        );
        _test.Eq(
            typeof(BattleGridService).GetMethod(
                "GetUnitTargetCoords",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(Godot.Collections.Array<Godot.Vector2I>),
            "BattleGridService 应保留同程序集可见的 unit target coords helper。"
        );
        _test.True(
            typeof(BattleGridService).GetMethod(
                "GetDistance",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleGridService 不应继续暴露 public 的 distance helper。"
        );
        _test.Eq(
            typeof(BattleGridService).GetMethod(
                "GetDistance",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(int),
            "BattleGridService 应保留同程序集可见的 distance helper。"
        );
        _test.True(
            typeof(BattleGridService).GetMethod(
                "GetDistanceFromUnitToCoord",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleGridService 不应继续暴露 public 的 unit-to-coord distance helper。"
        );
        _test.Eq(
            typeof(BattleGridService).GetMethod(
                "GetDistanceFromUnitToCoord",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(int),
            "BattleGridService 应保留同程序集可见的 unit-to-coord distance helper。"
        );
        _test.True(
            typeof(BattleGridService).GetMethod(
                "GetDistanceBetweenUnits",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleGridService 不应继续暴露 public 的 unit-to-unit distance helper。"
        );
        _test.Eq(
            typeof(BattleGridService).GetMethod(
                "GetDistanceBetweenUnits",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(int),
            "BattleGridService 应保留同程序集可见的 unit-to-unit distance helper。"
        );
        _test.True(
            typeof(BattleGridService).GetMethod(
                "GetTerrainDisplayName",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleGridService 不应继续暴露 public 的 terrain display-name helper。"
        );
        _test.Eq(
            typeof(BattleGridService).GetMethod(
                "GetTerrainDisplayName",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(string),
            "BattleGridService 应保留同程序集可见的 terrain display-name helper。"
        );
        _test.True(
            typeof(BattleGridService).GetMethod(
                "GetChebyshevDistance",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleGridService 不应继续暴露 public 的 chebyshev-distance helper。"
        );
        _test.Eq(
            typeof(BattleGridService).GetMethod(
                "GetChebyshevDistance",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(int),
            "BattleGridService 应保留同程序集可见的 chebyshev-distance helper。"
        );
        _test.True(
            typeof(BattleGridService).GetMethod(
                "MoveUnitForce",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleGridService 不应继续暴露 public 的 forced-move helper。"
        );
        _test.Eq(
            typeof(BattleGridService).GetMethod(
                "MoveUnitForce",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(bool),
            "BattleGridService 应保留同程序集可见的 forced-move helper。"
        );
        _test.True(
            typeof(BattleGridService).GetMethod(
                "CanJumpArc",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleGridService 不应继续暴露 public 的 jump-arc helper。"
        );
        _test.Eq(
            typeof(BattleGridService).GetMethod(
                "CanJumpArc",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(bool),
            "BattleGridService 应保留同程序集可见的 jump-arc helper。"
        );
        _test.True(
            typeof(BattleGridService).GetMethod(
                "CanBlinkToCoord",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleGridService 不应继续暴露 public 的 blink-placement helper。"
        );
        _test.Eq(
            typeof(BattleGridService).GetMethod(
                "CanBlinkToCoord",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(bool),
            "BattleGridService 应保留同程序集可见的 blink-placement helper。"
        );
        _test.Eq(
            typeof(BattleGridService).GetMethod(
                "GetUnitAtCoord",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(BattleUnitState),
            "BattleGridService 应保留同程序集可见的 typed unit lookup helper。"
        );
        _test.Eq(
            typeof(BattleGridService).GetMethod(
                "GetAreaCoords",
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new[] { typeof(BattleState), typeof(Vector2I), typeof(StringName), typeof(int) },
                null
            )?.ReturnType,
            typeof(Godot.Collections.Array<Godot.Vector2I>),
            "BattleGridService 应保留同程序集可见的 typed area query helper。"
        );
        _test.Eq(
            typeof(BattleGridService).GetMethod(
                "GetAreaCoords",
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new[]
                {
                    typeof(BattleState),
                    typeof(Vector2I),
                    typeof(StringName),
                    typeof(int),
                    typeof(Vector2I),
                },
                null
            )?.ReturnType,
            typeof(Godot.Collections.Array<Godot.Vector2I>),
            "BattleGridService 应保留同程序集可见的 typed directional area query helper。"
        );
        _test.Eq(
            typeof(BattleGridService).GetMethod(
                "HasCell",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(bool),
            "BattleGridService 应保留 internal typed 的 cell presence helper。"
        );
        _test.True(
            typeof(BattleGridService).GetMethod(
                "GetHeightDifference",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleGridService 不应继续暴露 zero-caller 的高度差 helper。"
        );
        _test.True(
            typeof(BattleGridService).GetMethod(
                "IsHeightPassable",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleGridService 不应继续暴露 zero-caller 的高度可通行 helper。"
        );
        _test.True(
            typeof(BattleGridService).GetMethod(
                "GetMovementCost",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleGridService 不应继续暴露 zero-caller 的移动成本 helper。"
        );
        _test.True(
            typeof(BattleGridService).GetMethod(
                "CanEnterCell",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleGridService 不应继续暴露 public 的单格进入判定 helper。"
        );
        _test.True(
            typeof(BattleGridService).GetMethod(
                "CanUnitEnterCoord",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleGridService 不应继续暴露 zero-caller 的单位单格进入判定 helper。"
        );
        _test.True(
            typeof(BattleGridService).GetMethod(
                "CollectBlockingUnitIds",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleGridService 不应继续暴露 public 的 blocking unit 查询 helper。"
        );
        _test.Eq(
            typeof(BattleGridService).GetMethod(
                "CollectBlockingUnitIds",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(Godot.Collections.Array<Godot.StringName>),
            "BattleGridService 应保留 internal typed 的 blocking unit 查询 helper。"
        );
        _test.True(
            typeof(BattleGridService).GetMethod(
                "RecalculateCell",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleGridService 不应继续暴露 public 的 cell recalc helper。"
        );
        _test.True(
            typeof(BattleGridService).GetMethod(
                "SyncColumnFromSurfaceCell",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleGridService 不应继续暴露 public 的 column sync helper。"
        );
        _test.True(
            typeof(BattleGridService).GetMethod(
                "SetBaseTerrain",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleGridService 不应继续暴露 public 的 terrain mutation helper。"
        );
        _test.True(
            typeof(BattleGridService).GetMethod(
                "SetHeightOffset",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleGridService 不应继续暴露 public 的 height mutation helper。"
        );
        _test.True(
            typeof(BattleGridService).GetMethod(
                "SetEdgeFeature",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleGridService 不应继续暴露 public 的 edge-feature mutation helper。"
        );
        _test.True(
            typeof(BattleGridService).GetMethod(
                "ClearEdgeFeature",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleGridService 不应继续暴露 public 的 edge-feature clear helper。"
        );
        _test.True(
            typeof(BattleGridService).GetMethod(
                "ResolveUnitMovePath",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[]
                {
                    typeof(GodotObject),
                    typeof(BattleUnitState),
                    typeof(Vector2I),
                    typeof(Vector2I),
                    typeof(int),
                    typeof(Func<BattleUnitState, Vector2I, int>),
                },
                null
            ) == null,
            "BattleGridService 不应继续暴露 zero-caller 的 move-path 字典 overload。"
        );
        _test.True(
            typeof(BattleGridService).GetMethod(
                "BuildUnitMovePathTree",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[]
                {
                    typeof(GodotObject),
                    typeof(BattleUnitState),
                    typeof(Vector2I),
                    typeof(int),
                    typeof(Func<BattleUnitState, Vector2I, int>),
                },
                null
            ) == null,
            "BattleGridService 不应继续暴露 zero-caller 的 move-path tree 字典 overload。"
        );
        _test.True(
            typeof(BattleGridService).GetMethod(
                "ResolveUnitMovePathTyped",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleGridService 不应继续暴露 public 的 typed move-path 入口。"
        );
        _test.Eq(
            typeof(BattleGridService).GetMethod(
                "ResolveUnitMovePathTyped",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(BattleMovePathResult),
            "BattleGridService 应保留 internal typed 的 move-path 入口。"
        );
        _test.True(
            typeof(BattleGridService).GetMethod(
                "BuildUnitMovePathTreeTyped",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleGridService 不应继续暴露 public 的 typed move-path tree 入口。"
        );
        _test.Eq(
            typeof(BattleGridService).GetMethod(
                "BuildUnitMovePathTreeTyped",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(BattleMovePathTreeResult),
            "BattleGridService 应保留 internal typed 的 move-path tree 入口。"
        );
        _test.True(
            typeof(BattleGridDistanceService).GetMethod(
                "GetUnitFootprintCoords",
                BindingFlags.Public | BindingFlags.Static
            ) == null,
            "BattleGridDistanceService 不应继续暴露 zero-caller 的 footprint GArray helper。"
        );
        _test.True(
            typeof(BattleGroundEffectService).GetMethod(
                "DedupeEffectDefsByInstance",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleGroundEffectService 不应继续暴露 public 的 effect dedupe GArray wrapper。"
        );
        _test.True(
            typeof(BattleGroundEffectService).GetMethod(
                "DedupeEffectDefsByInstanceTyped",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleGroundEffectService 不应继续暴露 public 的 typed effect dedupe 入口。"
        );
        _test.True(
            typeof(BattleShieldService).GetMethod(
                "ApplyUnitShieldEffectsResult",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleShieldService 不应继续暴露 public 的 typed shield apply 入口。"
        );
        _test.Eq(
            typeof(BattleShieldService).GetMethod(
                "ApplyUnitShieldEffectsResult",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(BattleShieldApplyResult),
            "BattleShieldService 应保留同程序集可见的 typed shield apply 入口。"
        );
        _test.True(
            typeof(BattleBarrierService).GetMethod(
                "Setup",
                BindingFlags.Public | BindingFlags.Instance
            ) == null
                && typeof(BattleBarrierService).GetMethod(
                    "Dispose",
                    BindingFlags.Public | BindingFlags.Instance
                ) == null,
            "BattleBarrierService 不应继续暴露 public 的 lifecycle helper。"
        );
        _test.Eq(
            typeof(BattleBarrierService).GetMethod(
                "Setup",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.GetParameters()[0].ParameterType,
            typeof(BattleRuntimeModule),
            "BattleBarrierService 应保留同程序集可见的 typed setup 入口。"
        );
        _test.True(
            typeof(BattleBarrierInstanceState).GetMethod("FromRuntimeDict") == null
                && typeof(BattleBarrierInstanceState).GetMethod("ToRuntimeDict") == null
                && typeof(BattleBarrierLayerState).GetMethod("FromRuntimeDict") == null
                && typeof(BattleBarrierLayerState).GetMethod("ToRuntimeDict") == null
                && typeof(BattleBarrierOutcomeState).GetMethod("FromRuntimeDict") == null
                && typeof(BattleBarrierOutcomeState).GetMethod("ToRuntimeDict") == null,
            "Barrier typed state 不应继续暴露 public 的 runtime-dict bridge。"
        );
        _test.Eq(
            typeof(BattleBarrierInstanceState).GetMethod(
                "FromRuntimeDict",
                BindingFlags.NonPublic | BindingFlags.Static
            )?.ReturnType,
            typeof(BattleBarrierInstanceState),
            "BattleBarrierInstanceState 应继续保留 nonpublic runtime-dict parse bridge。"
        );
        _test.Eq(
            typeof(BattleBarrierLayerState).GetMethod(
                "ToRuntimeDict",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(GDictionary),
            "BattleBarrierLayerState 应继续保留 nonpublic runtime-dict projection bridge。"
        );
        _test.True(
            typeof(BattleShieldService).GetMethod(
                "BuildUnitShieldResult",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleShieldService 不应继续暴露 public 的 shield result builder。"
        );
        _test.True(
            typeof(BattleShieldService).GetMethod("_build_unit_shield_result") == null,
            "BattleShieldService 不应继续暴露 zero-caller 的 shield payload bridge。"
        );
        _test.True(
            typeof(BattleShieldService).GetMethod(
                "Setup",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleShieldService 不应继续暴露 public 的 runtime setup。"
        );
        _test.True(
            typeof(BattleShieldService).GetMethod(
                "DisposeRuntime",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleShieldService 不应继续暴露 public 的 runtime dispose。"
        );
        _test.True(
            typeof(BattleShieldService).GetMethod(
                "ApplyShieldEffectToTargetResult",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleShieldService 不应继续暴露 public 的 shield effect apply 入口。"
        );
        _test.Eq(
            typeof(BattleShieldService).GetMethod(
                "ApplyShieldEffectToTargetResult",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(BattleShieldApplyResult),
            "BattleShieldService 应保留同程序集可见的 shield effect apply 入口。"
        );
        _test.True(
            typeof(BattleShieldService).GetMethod(
                "ResolveShieldHp",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleShieldService 不应继续暴露 public 的 shield hp 解析 helper。"
        );
        _test.Eq(
            typeof(BattleShieldService).GetMethod(
                "ResolveShieldHp",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(int),
            "BattleShieldService 应保留同程序集可见的 shield hp 解析 helper。"
        );
        _test.True(
            typeof(BattleShieldService).GetMethod(
                "RollShieldHpWithAttributeScaledDice",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleShieldService 不应继续暴露 public 的 attribute-scaled shield dice helper。"
        );
        _test.True(
            typeof(BattleGroundEffectService).GetMethod(
                "ApplyUnitShieldEffectsResult",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleGroundEffectService 不应继续暴露 public 的 ground shield bridge。"
        );
        _test.True(
            typeof(BattleGroundEffectService).GetMethod(
                "Setup",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleGroundEffectService 不应继续暴露 public 的 runtime setup。"
        );
        _test.True(
            typeof(BattleGroundEffectService).GetMethod(
                "Dispose",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleGroundEffectService 不应继续暴露 public 的 runtime dispose。"
        );
        _test.True(
            typeof(BattleGroundEffectService).GetMethod(
                "ResolveGroundSpellControlAfterCostResult",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleGroundEffectService 不应继续暴露 public 的 ground spell-control 入口。"
        );
        _test.Eq(
            typeof(BattleGroundEffectService).GetMethod(
                "ResolveGroundSpellControlAfterCostResult",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(BattleSpellControlResult),
            "BattleGroundEffectService 应保留同程序集可见的 ground spell-control 入口。"
        );
        _test.True(
            typeof(BattleGroundEffectService).GetMethod(
                "ResolveUnitSpellControlAfterCostResult",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleGroundEffectService 不应继续暴露 public 的 unit spell-control 入口。"
        );
        _test.Eq(
            typeof(BattleGroundEffectService).GetMethod(
                "ResolveUnitSpellControlAfterCostResult",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(BattleSpellControlResult),
            "BattleGroundEffectService 应保留同程序集可见的 unit spell-control 入口。"
        );
        _test.Eq(
            typeof(BattleGroundEffectService).GetMethod(
                "ApplyUnitShieldEffectsResult",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.ReturnType,
            typeof(BattleShieldApplyResult),
            "BattleGroundEffectService 应保留同程序集可见的 ground shield bridge。"
        );
        _test.True(
            typeof(BattleGroundEffectService).GetMethod(
                "BuildGroundEffectCoords",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleGroundEffectService 不应继续暴露 public 的 ground effect coord helper。"
        );
        _test.True(
            typeof(BattleGroundEffectService).GetMethod(
                "CollectGroundUnitEffectDefs",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleGroundEffectService 不应继续暴露 public 的 ground unit effect helper。"
        );
        _test.True(
            typeof(BattleGroundEffectService).GetMethod(
                "CollectGroundTerrainEffectDefs",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleGroundEffectService 不应继续暴露 public 的 ground terrain effect helper。"
        );
        _test.True(
            typeof(BattleGroundEffectService).GetMethod(
                "CollectGroundEffectDefs",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleGroundEffectService 不应继续暴露 zero-caller 的 merged ground effect helper。"
        );
        _test.True(
            typeof(BattleGroundEffectService).GetMethod(
                "CollectGroundPreviewUnitIds",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleGroundEffectService 不应继续暴露 public 的 ground preview-unit helper。"
        );
        _test.True(
            typeof(BattleGroundEffectService).GetMethod(
                "BuildGroundForcedMoveContextResult",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleGroundEffectService 不应继续暴露 zero-caller 的 forced-move context bridge。"
        );
        _test.True(
            typeof(BattleGroundEffectService).GetMethod(
                "ResolveGroundUnitEffectResult",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleGroundEffectService 不应继续暴露 public 的 ground unit effect resolver。"
        );
        _test.True(
            typeof(BattleEdgeService).GetMethod(
                "EnsureRuntimeEdgeFaces",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleEdgeService 不应继续暴露 public 的 runtime edge ensure helper。"
        );
        _test.True(
            typeof(BattleEdgeService).GetMethod(
                "RebuildRuntimeEdgeFaces",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleEdgeService 不应继续暴露 zero-caller 的 runtime edge rebuild helper。"
        );
        _test.True(
            typeof(BattleEdgeService).GetMethod(
                "ClearRuntimeEdgeFaces",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleEdgeService 不应继续暴露 zero-caller 的 runtime edge clear helper。"
        );
        _test.True(
            typeof(BattleEdgeService).GetMethod(
                "MarkRuntimeEdgeFacesDirty",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleEdgeService 不应继续暴露 public 的 runtime edge dirty helper。"
        );
        _test.True(
            typeof(BattleEdgeService).GetMethod(
                "GetEdgeFaceByOrigin",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleEdgeService 不应继续暴露 zero-caller 的 edge-origin 查询 helper。"
        );
        _test.True(
            typeof(BattleEdgeService).GetMethod(
                "IsTraversableBetween",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleEdgeService 不应继续暴露 public 的 runtime traversable helper。"
        );
        _test.True(
            typeof(BattleEdgeService).GetMethod(
                "BlocksOccupancyBetween",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleEdgeService 不应继续暴露 public 的 runtime occupancy helper。"
        );
        _test.True(
            typeof(BattleEdgeService).GetMethod(
                "HasFeatureBetween",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleEdgeService 不应继续暴露 public 的 runtime edge-feature helper。"
        );
        _test.True(
            typeof(BattleState).GetMethod(
                "ClearRuntimeEdgeFaces",
                BindingFlags.Public | BindingFlags.Instance
            ) == null,
            "BattleState 不应继续暴露 zero-caller 的 runtime edge clear helper。"
        );
    }

    private void TestRuntimeCommandLoggerKeepsScopeTypedAndInternal()
    {
        Type loggerType = typeof(GameRuntimeCommandLogger);
        _test.True(
            loggerType.GetNestedType("CommandLogScope", BindingFlags.NonPublic) != null,
            "GameRuntimeCommandLogger 应用内部 typed scope 保存 active command 日志状态。"
        );
        _test.True(
            typeof(GameRuntimeFacade).GetField(
                "_active_command_log_scope",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
            ) == null,
            "GameRuntimeFacade 不应继续暴露 active command log Dictionary scope。"
        );
    }

    private void TestRuntimeEncounterCatalogKeepsTypedScope()
    {
        _test.True(
            typeof(GameRuntimeFacade).GetField(
                "_wild_encounter_rosters",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
            ) == null,
            "GameRuntimeFacade 不应继续持有 wild encounter roster Dictionary 业务态缓存。"
        );
        _test.Eq(
            typeof(GameRuntimeFacade).GetField(
                "_wild_encounter_roster_defs",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.FieldType,
            typeof(Dictionary<StringName, WildEncounterRosterDef>),
            "GameRuntimeFacade wild encounter roster 缓存应保持 typed Dictionary<StringName,WildEncounterRosterDef>。"
        );
        _test.True(
            typeof(WildEncounterRosterDef).GetMethod("GetStageUnitEntries") == null,
            "WildEncounterRosterDef 不应继续暴露 Array<Dictionary> stage unit entry wrapper。"
        );
        _test.True(
            typeof(WildEncounterRosterDef).GetMethod(
                "ValidateSchema",
                new[] { typeof(GDictionary) }
            ) == null,
            "WildEncounterRosterDef 不应继续暴露 GDictionary schema wrapper。"
        );
        _test.True(
            typeof(WildEncounterRosterUnitEntryDef).GetMethod("ToDictionary") == null,
            "WildEncounterRosterUnitEntryDef 不应继续暴露 Dictionary projection wrapper。"
        );
        _test.True(
            typeof(EnemyTemplateDef).GetMethod("GetDropEntriesResolved") == null,
            "EnemyTemplateDef 不应继续暴露 Array<Dictionary> drop_entries wrapper。"
        );
        _test.True(
            typeof(DropEntryDef).GetMethod("ToDictionary") == null,
            "DropEntryDef 不应继续暴露 Dictionary projection wrapper。"
        );
        _test.True(
            typeof(EncounterRosterBuilder).GetMethod(
                "Setup",
                new[] { typeof(GDictionary), typeof(GDictionary) }
            ) == null,
            "EncounterRosterBuilder 不应继续暴露 GDictionary setup overload。"
        );
    }

    private void TestRuntimeCommandHandlersNoLongerRequireGodotRegistration()
    {
    }

    private void TestRuntimeSnapshotBuilderUsesTypedSourceBoundary()
    {
        _test.True(
            typeof(IGameRuntimeSnapshotSource).IsAssignableFrom(typeof(GameRuntimeFacade)),
            "GameRuntimeFacade 应通过 typed snapshot source 接口供 SnapshotBuilder 消费。"
        );
        _test.Eq(
            typeof(GameRuntimeSnapshotBuilder)
                .GetMethod("Setup", BindingFlags.Public | BindingFlags.Instance)
                ?.GetParameters()[0]
                .ParameterType,
            typeof(IGameRuntimeSnapshotSource),
            "GameRuntimeSnapshotBuilder.Setup 不应继续绑死具体 GameRuntimeFacade。"
        );
    }

    private void TestWorldGenerationHelpersNoLongerRequireGodotRegistration()
    {
        _test.Eq(
            typeof(WildEncounterGrowthSystem)
                .GetMethod("ApplyStepAdvance")
                ?.GetParameters()[0]
                .ParameterType,
            typeof(IEnumerable<EncounterAnchorData>),
            "WildEncounterGrowthSystem 成长推进入口不应继续接收 world_data Godot Dictionary。"
        );
        _test.Eq(
            typeof(WildEncounterGrowthSystem)
                .GetMethod("ApplyBattleVictory")
                ?.GetParameters()[2]
                .ParameterType,
            typeof(IReadOnlyDictionary<StringName, WildEncounterRosterDef>),
            "WildEncounterGrowthSystem 战后压制入口不应继续接收 roster Godot Dictionary。"
        );
    }

    private void TestWorldPresetHelpersNoLongerRequireGodotRegistration()
    {
        IReadOnlyList<WorldPresetRegistry.WorldPresetInfo> presets =
            WorldPresetRegistry.ListPresetsTyped();
        _test.True(presets.Count > 0, "WorldPresetRegistry typed 目录应继续暴露预设列表。");
        _test.True(
            WorldPresetRegistry.TryGetPresetTyped("test", out var testPreset),
            "WorldPresetRegistry typed 查询应继续找到 test 预设。"
        );
        _test.Eq(
            testPreset?.DisplayName,
            "测试",
            "WorldPresetRegistry typed 查询应保留 test 预设名称。"
        );
        GDictionary projectedTestPreset = WorldPresetRegistry.GetPreset("test");
        _test.Eq(
            projectedTestPreset["display_name"].AsString(),
            testPreset?.DisplayName,
            "WorldPresetRegistry Dictionary 投影应只反映 typed 预设数据。"
        );
    }

    private void TestHeadlessSnapshotHelpersNoLongerRequireGodotRegistration()
    {
        _test.Eq(
            typeof(HeadlessGameTestSession)
                .GetMethod("BuildSnapshotTyped", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.ReturnType,
            typeof(Dictionary<string, object>),
            "HeadlessGameTestSession snapshot 组装应先停留在 typed Dictionary，再在 public build_snapshot() 边界投影。"
        );
        _test.True(
            typeof(HeadlessGameTestSession)
                    .GetMethod(
                        "ResolveBattleBackpackEquipmentInstance",
                        BindingFlags.Instance | BindingFlags.NonPublic
                    )
                    ?.ReturnType != typeof(GDictionary),
            "HeadlessGameTestSession 战斗背包装备实例解析 helper 不应继续返回 GDictionary。"
        );
        _test.True(
            typeof(HeadlessGameTestSession)
                    .GetMethod(
                        "FindLastChangeEquipmentReport",
                        BindingFlags.Static | BindingFlags.NonPublic
                    )
                    ?.ReturnType != typeof(GDictionary),
            "HeadlessGameTestSession change-equipment report helper 不应继续返回 GDictionary。"
        );
        _test.Eq(
            typeof(HeadlessGameTestSession)
                .GetMethod(
                    "BuildBattleStartDiagnostic",
                    BindingFlags.Static | BindingFlags.NonPublic
                )
                ?.GetParameters()[3]
                .ParameterType,
            typeof(IReadOnlyDictionary<string, object>),
            "HeadlessGameTestSession battle-start diagnostic 应直接消费 typed context，不应回读 GDictionary。"
        );
        _test.Eq(
            typeof(HeadlessGameTestSession)
                .GetMethod(
                    "CreateNewGameTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(HeadlessGameTestSession.SessionCommandOutcome),
            "HeadlessGameTestSession new-game helper 应提供 typed outcome，避免 runner 回读 GDictionary。"
        );
        _test.Eq(
            typeof(HeadlessGameTestSession)
                .GetMethod(
                    "LoadGameTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(HeadlessGameTestSession.SessionCommandOutcome),
            "HeadlessGameTestSession load-game helper 应提供 typed outcome，避免 runner 回读 GDictionary。"
        );
        _test.Eq(
            typeof(HeadlessGameTestSession)
                .GetMethod(
                    "EnsureWorldLoadedTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(HeadlessGameTestSession.SessionCommandOutcome),
            "HeadlessGameTestSession world-load gate 应提供 typed outcome，避免 runner 回读 GDictionary。"
        );
        _test.Eq(
            typeof(HeadlessGameTestSession)
                .GetMethod(
                    "SetPartyStorageCapacityTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(HeadlessGameTestSession.SessionCommandOutcome),
            "HeadlessGameTestSession storage-capacity helper 应提供 typed outcome，避免 runner 回读 GDictionary。"
        );
        _test.Eq(
            typeof(HeadlessGameTestSession)
                .GetMethod(
                    "StartBattleByKindTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(HeadlessGameTestSession.SessionCommandOutcome),
            "HeadlessGameTestSession start-battle helper 应提供 typed outcome，避免 runner 回读 GDictionary。"
        );
        _test.Eq(
            typeof(HeadlessGameTestSession)
                .GetMethod(
                    "FinishActiveBattleTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(HeadlessGameTestSession.SessionCommandOutcome),
            "HeadlessGameTestSession finish-battle helper 应提供 typed outcome，避免 runner 回读 GDictionary。"
        );
        _test.Eq(
            typeof(GameRuntimeFacade)
                .GetMethod(
                    "CommandBattleWaitOrResolveTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade battle wait/resolve helper 应提供 typed runtime outcome，避免 session/runner 回读 GDictionary。"
        );
        _test.Eq(
            typeof(GameRuntimeFacade.RuntimeCommandResult)
                .GetProperty("Code", BindingFlags.Instance | BindingFlags.Public)
                ?.PropertyType,
            typeof(GameRuntimeFacade.RuntimeCommandCode),
            "GameRuntimeFacade.RuntimeCommandResult 应提供统一的 enum code。"
        );
        _test.Eq(
            typeof(HeadlessGameTestSession.SessionCommandOutcome)
                .GetProperty("Code", BindingFlags.Instance | BindingFlags.Public)
                ?.PropertyType,
            typeof(GameRuntimeFacade.RuntimeCommandCode),
            "HeadlessGameTestSession.SessionCommandOutcome 应透传统一的 enum code。"
        );
        _test.Eq(
            typeof(GameTextCommandResult)
                .GetField("code", BindingFlags.Instance | BindingFlags.Public)
                ?.FieldType,
            typeof(GameRuntimeFacade.RuntimeCommandCode),
            "GameTextCommandResult 应向外暴露统一的 enum code。"
        );
        _test.Eq(
            typeof(GameTextCommandRunner)
                .GetNestedType("CommandOutcome", BindingFlags.NonPublic)
                ?.GetField("Code", BindingFlags.Instance | BindingFlags.Public)
                ?.FieldType,
            typeof(GameRuntimeFacade.RuntimeCommandCode),
            "GameTextCommandRunner.CommandOutcome 应保留统一的 enum code。"
        );
        _test.True(
            typeof(GameTextCommandRunner)
                .GetNestedType("ExpectationResult", BindingFlags.NonPublic)
                ?.GetField("Message", BindingFlags.Instance | BindingFlags.Public) == null,
            "GameTextCommandRunner.ExpectationResult 不应继续在 owner 内部搬运 message 字符串。"
        );
        _test.Eq(
            typeof(GameRuntimeFacade)
                .GetMethod(
                    "CommandOpenPartyTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade party open helper 应提供 typed runtime outcome，避免 runner 回读 GDictionary。"
        );
        _test.Eq(
            typeof(GameRuntimeFacade)
                .GetMethod(
                    "CommandSelectPartyMemberTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade party select helper 应提供 typed runtime outcome，避免 runner 回读 GDictionary。"
        );
        _test.Eq(
            typeof(GameRuntimeFacade)
                .GetMethod(
                    "CommandSetPartyLeaderTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade party leader helper 应提供 typed runtime outcome，避免 runner 回读 GDictionary。"
        );
        _test.Eq(
            typeof(GameRuntimeFacade)
                .GetMethod(
                    "CommandMoveMemberToActiveTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade party activate helper 应提供 typed runtime outcome，避免 runner 回读 GDictionary。"
        );
        _test.Eq(
            typeof(GameRuntimeFacade)
                .GetMethod(
                    "CommandMoveMemberToReserveTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade party reserve helper 应提供 typed runtime outcome，避免 runner 回读 GDictionary。"
        );
        _test.Eq(
            typeof(GameRuntimeFacade)
                .GetMethod(
                    "CommandOpenPartyWarehouseTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade party warehouse helper 应提供 typed runtime outcome，避免 runner 回读 GDictionary。"
        );
        _test.True(
            typeof(GameRuntimePartyCommandHandler).GetMethod(
                "command_open_party",
                BindingFlags.Instance | BindingFlags.Public
            ) == null
                && typeof(GameRuntimePartyCommandHandler).GetMethod(
                    "CommandOpenParty",
                    BindingFlags.Instance | BindingFlags.Public
                ) == null
                && typeof(GameRuntimePartyCommandHandler).GetMethod(
                    "command_select_party_member",
                    BindingFlags.Instance | BindingFlags.Public
                ) == null
                && typeof(GameRuntimePartyCommandHandler).GetMethod(
                    "CommandSelectPartyMember",
                    BindingFlags.Instance | BindingFlags.Public
                ) == null
                && typeof(GameRuntimePartyCommandHandler).GetMethod(
                    "command_set_party_leader",
                    BindingFlags.Instance | BindingFlags.Public
                ) == null
                && typeof(GameRuntimePartyCommandHandler).GetMethod(
                    "CommandSetPartyLeader",
                    BindingFlags.Instance | BindingFlags.Public
                ) == null
                && typeof(GameRuntimePartyCommandHandler).GetMethod(
                    "command_move_member_to_active",
                    BindingFlags.Instance | BindingFlags.Public
                ) == null
                && typeof(GameRuntimePartyCommandHandler).GetMethod(
                    "CommandMoveMemberToActive",
                    BindingFlags.Instance | BindingFlags.Public
                ) == null
                && typeof(GameRuntimePartyCommandHandler).GetMethod(
                    "command_move_member_to_reserve",
                    BindingFlags.Instance | BindingFlags.Public
                ) == null
                && typeof(GameRuntimePartyCommandHandler).GetMethod(
                    "CommandMoveMemberToReserve",
                    BindingFlags.Instance | BindingFlags.Public
                ) == null
                && typeof(GameRuntimePartyCommandHandler).GetMethod(
                    "command_party_equip_item",
                    BindingFlags.Instance | BindingFlags.Public
                ) == null
                && typeof(GameRuntimePartyCommandHandler).GetMethod(
                    "CommandPartyEquipItem",
                    BindingFlags.Instance | BindingFlags.Public
                ) == null
                && typeof(GameRuntimePartyCommandHandler).GetMethod(
                    "command_party_unequip_item",
                    BindingFlags.Instance | BindingFlags.Public
                ) == null
                && typeof(GameRuntimePartyCommandHandler).GetMethod(
                    "CommandPartyUnequipItem",
                    BindingFlags.Instance | BindingFlags.Public
                ) == null,
            "GameRuntimePartyCommandHandler 不应继续保留 wrapper-only 的 party/equipment dictionary result surface。"
        );
        _test.Eq(
            typeof(GameRuntimeFacade)
                .GetMethod(
                    "CommandPartyEquipItemTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade party equip helper 应提供 typed runtime outcome。"
        );
        _test.Eq(
            typeof(GameRuntimeFacade)
                .GetMethod(
                    "CommandPartyUnequipItemTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade party unequip helper 应提供 typed runtime outcome。"
        );
        _test.True(
            typeof(GameRuntimeWarehouseHandler).GetMethod(
                "command_open_party_warehouse",
                BindingFlags.Instance | BindingFlags.Public
            ) == null
                && typeof(GameRuntimeWarehouseHandler).GetMethod(
                    "CommandOpenPartyWarehouse",
                    BindingFlags.Instance | BindingFlags.Public
                ) == null
                && typeof(GameRuntimeWarehouseHandler).GetMethod(
                    "command_discard_one",
                    BindingFlags.Instance | BindingFlags.Public
                ) == null
                && typeof(GameRuntimeWarehouseHandler).GetMethod(
                    "CommandDiscardOne",
                    BindingFlags.Instance | BindingFlags.Public
                ) == null
                && typeof(GameRuntimeWarehouseHandler).GetMethod(
                    "command_discard_all",
                    BindingFlags.Instance | BindingFlags.Public
                ) == null
                && typeof(GameRuntimeWarehouseHandler).GetMethod(
                    "CommandDiscardAll",
                    BindingFlags.Instance | BindingFlags.Public
                ) == null
                && typeof(GameRuntimeWarehouseHandler).GetMethod(
                    "command_add_item",
                    BindingFlags.Instance | BindingFlags.Public
                ) == null
                && typeof(GameRuntimeWarehouseHandler).GetMethod(
                    "CommandAddItem",
                    BindingFlags.Instance | BindingFlags.Public
                ) == null,
            "GameRuntimeWarehouseHandler 不应继续保留 open/discard/add 的 wrapper-only dictionary surface。"
        );
        _test.True(
            typeof(GameRuntimeWarehouseHandler).GetMethod(
                "command_use_item",
                BindingFlags.Instance | BindingFlags.Public
            ) == null
                && typeof(GameRuntimeWarehouseHandler).GetMethod(
                    "CommandUseItem",
                    BindingFlags.Instance | BindingFlags.Public
                ) == null,
            "GameRuntimeWarehouseHandler 不应继续保留 wrapper-only 的 use-item dictionary surface。"
        );
        _test.Eq(
            typeof(GameRuntimeFacade)
                .GetMethod(
                    "CommandWarehouseAddItemTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade warehouse add helper 应提供 typed runtime outcome。"
        );
        _test.Eq(
            typeof(GameRuntimeWarehouseHandler)
                .GetMethod(
                    "CommandAddItemTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeWarehouseHandler.CommandAddItemTyped() 不应继续返回 Dictionary。"
        );
        _test.Eq(
            typeof(GameRuntimeWarehouseHandler)
                .GetMethod(
                    "CommandDiscardOneTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeWarehouseHandler.CommandDiscardOneTyped() 不应继续返回 Dictionary。"
        );
        _test.Eq(
            typeof(GameRuntimeWarehouseHandler)
                .GetMethod(
                    "CommandDiscardAllTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeWarehouseHandler.CommandDiscardAllTyped() 不应继续返回 Dictionary。"
        );
        _test.True(
            typeof(GameRuntimeQuestCommandHandler).GetMethod(
                "command_accept_quest",
                BindingFlags.Instance | BindingFlags.Public
            ) == null
                && typeof(GameRuntimeQuestCommandHandler).GetMethod(
                    "command_progress_quest",
                    BindingFlags.Instance | BindingFlags.Public
                ) == null
                && typeof(GameRuntimeQuestCommandHandler).GetMethod(
                    "command_complete_quest",
                    BindingFlags.Instance | BindingFlags.Public
                ) == null,
            "GameRuntimeQuestCommandHandler 不应继续保留 accept/progress/complete 的 wrapper-only dictionary surface。"
        );
        _test.Eq(
            typeof(GameRuntimeQuestCommandHandler)
                .GetMethod(
                    "CommandAcceptQuestTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeQuestCommandHandler.CommandAcceptQuestTyped() 不应继续返回 Dictionary。"
        );
        _test.Eq(
            typeof(GameRuntimeQuestCommandHandler)
                .GetMethod(
                    "CommandProgressQuestTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    binder: null,
                    types: new[]
                    {
                        typeof(StringName),
                        typeof(StringName),
                        typeof(int),
                        typeof(QuestProgressCommandPayloadData),
                    },
                    modifiers: null
                )
                ?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeQuestCommandHandler.CommandProgressQuestTyped() 不应继续返回 Dictionary。"
        );
        _test.Eq(
            typeof(GameRuntimeQuestCommandHandler)
                .GetMethod(
                    "CommandCompleteQuestTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeQuestCommandHandler.CommandCompleteQuestTyped() 不应继续返回 Dictionary。"
        );
        _test.Eq(
            typeof(GameRuntimeFacade)
                .GetMethod(
                    "CommandWorldMoveTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade world move helper 应提供 typed runtime outcome，避免 runner 回读 GDictionary。"
        );
        _test.Eq(
            typeof(GameRuntimeFacade)
                .GetMethod(
                    "CommandWorldSelectTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade world select helper 应提供 typed runtime outcome，避免 runner 回读 GDictionary。"
        );
        _test.Eq(
            typeof(GameRuntimeFacade)
                .GetMethod(
                    "CommandOpenSettlementTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    binder: null,
                    types: new[] { typeof(Vector2I) },
                    modifiers: null
                )
                ?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade world open(coord) helper 应提供 typed runtime outcome，避免 runner 回读 GDictionary。"
        );
        _test.Eq(
            typeof(GameRuntimeFacade)
                .GetMethod(
                    "CommandOpenSettlementTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    binder: null,
                    types: System.Type.EmptyTypes,
                    modifiers: null
                )
                ?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade world open() helper 应提供 typed runtime outcome，避免 runner 回读 GDictionary。"
        );
        _test.Eq(
            typeof(GameRuntimeFacade)
                .GetMethod(
                    "CommandWorldInspectTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade world inspect helper 应提供 typed runtime outcome，避免 runner 回读 GDictionary。"
        );
        _test.Eq(
            typeof(GameRuntimeFacade)
                .GetMethod(
                    "SelectWorldCellTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade world click/select helper 应提供 typed runtime outcome，避免 runner 回读 GDictionary。"
        );
        _test.Eq(
            typeof(GameRuntimeFacade)
                .GetMethod(
                    "CommandBattleTickTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade battle tick helper 应提供 typed runtime outcome，避免 runner 回读 GDictionary。"
        );
        _test.Eq(
            typeof(GameRuntimeFacade)
                .GetMethod(
                    "CommandBattleSelectSkillTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade battle select-skill helper 应提供 typed runtime outcome，避免 runner 回读 GDictionary。"
        );
        _test.Eq(
            typeof(GameRuntimeFacade)
                .GetMethod(
                    "CommandBattleCycleVariantTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade battle cycle-variant helper 应提供 typed runtime outcome，避免 runner 回读 GDictionary。"
        );
        _test.Eq(
            typeof(GameRuntimeFacade)
                .GetMethod(
                    "CommandBattleClearSkillTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade battle clear-skill helper 应提供 typed runtime outcome，避免 runner 回读 GDictionary。"
        );
        _test.Eq(
            typeof(GameRuntimeFacade)
                .GetMethod(
                    "CommandBattleMoveToTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade battle move-to helper 应提供 typed runtime outcome，避免 runner 回读 GDictionary。"
        );
        _test.Eq(
            typeof(GameRuntimeFacade)
                .GetMethod(
                    "CommandBattleMoveDirectionTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade battle move-direction helper 应提供 typed runtime outcome，避免 runner 回读 GDictionary。"
        );
        _test.Eq(
            typeof(GameRuntimeFacade)
                .GetMethod(
                    "CommandBattleInspectTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade battle inspect helper 应提供 typed runtime outcome，避免 runner 回读 GDictionary。"
        );
        _test.Eq(
            typeof(BattleSessionFacade)
                .GetMethod(
                    "CommandBattleTickTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "BattleSessionFacade.CommandBattleTickTyped() 不应继续返回 Dictionary。"
        );
        _test.Eq(
            typeof(BattleSessionFacade)
                .GetMethod(
                    "CommandBattleSelectSkillTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "BattleSessionFacade.CommandBattleSelectSkillTyped() 不应继续返回 Dictionary。"
        );
        _test.Eq(
            typeof(GameRuntimeBattleSelection)
                .GetMethod(
                    "SelectBattleSkillSlotTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeBattleSelection.SelectBattleSkillSlotTyped() 不应继续返回 Dictionary。"
        );
        _test.Eq(
            typeof(GameRuntimeBattleSelection)
                .GetMethod(
                    "GetUnlockedCastVariants",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(Godot.Collections.Array<CombatCastVariantDef>),
            "GameRuntimeBattleSelection.GetUnlockedCastVariants() 应直接返回 typed cast variant array。"
        );
        _test.Eq(
            typeof(BattleState)
                .GetMethod(
                    "GetUnitsTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(List<BattleUnitState>),
            "BattleState.GetUnitsTyped() 应作为 GameRuntimeBattleSelection 的 typed unit 枚举面。"
        );
        _test.Eq(
            typeof(BattleState)
                .GetMethod(
                    "TryGetCellTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(bool),
            "BattleState.TryGetCellTyped() 应作为 GameRuntimeBattleSelection 的 typed cell 读取面。"
        );
        _test.Eq(
            typeof(BattleState)
                .GetMethod(
                    "GetCellEntriesTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(List<BattleState.BattleCellEntry>),
            "BattleState.GetCellEntriesTyped() 应作为 GameRuntimeBattleSelection 的 typed cell 枚举面。"
        );
        _test.Eq(
            typeof(BattleUnitState)
                .GetMethod(
                    "GetKnownSkillLevelTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(int),
            "BattleUnitState.GetKnownSkillLevelTyped() 应作为 GameRuntimeBattleSelection 的 typed skill level 读取面。"
        );
        _test.Eq(
            typeof(BattleUnitState)
                .GetMethod(
                    "GetCooldownTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(int),
            "BattleUnitState.GetCooldownTyped() 应作为 GameRuntimeBattleSelection 的 typed cooldown 读取面。"
        );
        _test.Eq(
            typeof(GameRuntimeFacade)
                .GetMethod(
                    "GetBattleSelectionTargetCoordsStateTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(IReadOnlyList<Vector2I>),
            "GameRuntimeFacade.GetBattleSelectionTargetCoordsStateTyped() 应直接暴露 typed target coord queue。"
        );
        _test.Eq(
            typeof(GameRuntimeFacade)
                .GetMethod(
                    "GetBattleSelectionTargetUnitIdsStateTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(IReadOnlyList<StringName>),
            "GameRuntimeFacade.GetBattleSelectionTargetUnitIdsStateTyped() 应直接暴露 typed target unit queue。"
        );
        _test.Eq(
            typeof(GameRuntimeFacade)
                .GetMethod(
                    "GetSkillDefsTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(IReadOnlyDictionary<StringName, SkillDef>),
            "GameRuntimeFacade.GetSkillDefsTyped() 应直接暴露 typed skill defs。"
        );
        _test.True(
            typeof(GameRuntimeFacade).GetMethod("get_skill_defs") == null
                && typeof(GameRuntimeFacade).GetMethod("get_item_defs") == null,
            "GameRuntimeFacade 不应继续暴露公开 skill/item catalog 字典包装。"
        );
        _test.Eq(
            typeof(BattleSessionFacade)
                .GetMethod(
                    "CommandBattleCycleVariantTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "BattleSessionFacade.CommandBattleCycleVariantTyped() 不应继续返回 Dictionary。"
        );
        _test.Eq(
            typeof(BattleSessionFacade)
                .GetMethod(
                    "CommandBattleClearSkillTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "BattleSessionFacade.CommandBattleClearSkillTyped() 不应继续返回 Dictionary。"
        );
        _test.Eq(
            typeof(BattleSessionFacade)
                .GetMethod(
                    "CommandBattleMoveToTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "BattleSessionFacade.CommandBattleMoveToTyped() 不应继续返回 Dictionary。"
        );
        _test.Eq(
            typeof(BattleSessionFacade)
                .GetMethod(
                    "CommandBattleMoveDirectionTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "BattleSessionFacade.CommandBattleMoveDirectionTyped() 不应继续返回 Dictionary。"
        );
        _test.Eq(
            typeof(BattleSessionFacade)
                .GetMethod(
                    "CommandBattleWaitOrResolveTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "BattleSessionFacade.CommandBattleWaitOrResolveTyped() 不应继续返回 Dictionary。"
        );
        _test.Eq(
            typeof(BattleSessionFacade)
                .GetMethod(
                    "ResolveActiveBattleTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "BattleSessionFacade.ResolveActiveBattleTyped() 不应继续返回 Dictionary。"
        );
        _test.Eq(
            typeof(BattleSessionFacade)
                .GetMethod(
                    "CommandBattleInspectTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "BattleSessionFacade.CommandBattleInspectTyped() 不应继续返回 Dictionary。"
        );
        _test.Eq(
            typeof(BattleSessionFacade)
                .GetMethod(
                    "ResetBattleFocusTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "BattleSessionFacade.ResetBattleFocusTyped() 不应继续返回 Dictionary。"
        );
        _test.True(
            typeof(BattleSessionFacade)
                .GetMethod(
                    "reset_battle_focus",
                    BindingFlags.Instance | BindingFlags.Public
                ) == null,
            "BattleSessionFacade 不应继续暴露 reset_battle_focus() 字典包装。"
        );
        string[] removedBattleSessionFacadeWrappers =
        {
            "command_battle_tick",
            "command_battle_select_skill",
            "command_battle_cycle_variant",
            "command_battle_clear_skill",
            "command_battle_move_to",
            "command_battle_move_direction",
            "command_battle_wait_or_resolve",
            "command_battle_inspect",
        };
        foreach (string methodName in removedBattleSessionFacadeWrappers)
        {
            _test.True(
                typeof(BattleSessionFacade)
                    .GetMethod(
                        methodName,
                        BindingFlags.Instance | BindingFlags.Public
                    ) == null,
                $"BattleSessionFacade 不应继续暴露 {methodName}() 字典包装。"
            );
        }
        _test.Eq(
            typeof(GameRuntimeFacade)
                .GetMethod(
                    "CommandConfirmSubmapEntryTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade submap confirm helper 应提供 typed runtime outcome，避免 runner 回读 GDictionary。"
        );
        _test.Eq(
            typeof(GameRuntimeFacade)
                .GetMethod(
                    "CommandCancelSubmapEntryTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade submap cancel helper 应提供 typed runtime outcome，避免 runner 回读 GDictionary。"
        );
        _test.Eq(
            typeof(GameRuntimeFacade)
                .GetMethod(
                    "CommandReturnFromSubmapTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade submap return helper 应提供 typed runtime outcome，避免 runner 回读 GDictionary。"
        );
        _test.Eq(
            typeof(GameRuntimeFacade)
                .GetMethod(
                    "CommandConfirmBattleStartTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade battle confirm helper 应提供 typed runtime outcome，避免 runner 回读 GDictionary。"
        );
        _test.Eq(
            typeof(GameRuntimeFacade)
                .GetMethod(
                    "CommandConfirmPendingRewardTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade reward confirm helper 应提供 typed runtime outcome，避免 runner 回读 GDictionary。"
        );
        _test.Eq(
            typeof(GameRuntimeFacade)
                .GetMethod(
                    "CommandChoosePromotionTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade promotion choose helper 应提供 typed runtime outcome，避免 runner 回读 GDictionary。"
        );
        _test.Eq(
            typeof(GameRuntimeFacade)
                .GetMethod(
                    "CommandCloseActiveModalTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade close-active-modal helper 应提供 typed runtime outcome，避免 runner 回读 GDictionary。"
        );
        _test.Eq(
            typeof(GameRuntimeFacade)
                .GetMethod(
                    "CommandExecuteSettlementActionTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade settlement action helper 应提供 typed runtime outcome，避免 runner 回读 GDictionary。"
        );
        _test.Eq(
            typeof(GameRuntimeFacade)
                .GetMethod(
                    "CommandShopBuyTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade shop buy helper 应提供 typed runtime outcome，避免 runner 回读 GDictionary。"
        );
        _test.Eq(
            typeof(GameRuntimeFacade)
                .GetMethod(
                    "CommandShopSellTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade shop sell helper 应提供 typed runtime outcome，避免 runner 回读 GDictionary。"
        );
        _test.Eq(
            typeof(GameRuntimeFacade)
                .GetMethod(
                    "CommandStagecoachTravelTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeFacade stagecoach travel helper 应提供 typed runtime outcome，避免 runner 回读 GDictionary。"
        );
        _test.Eq(
            typeof(GameRuntimeSettlementCommandHandler)
                .GetMethod(
                    "CommandExecuteSettlementActionRuntimeTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeSettlementCommandHandler.CommandExecuteSettlementActionRuntimeTyped() 不应继续返回 Dictionary。"
        );
        _test.Eq(
            typeof(GameRuntimeSettlementCommandHandler)
                .GetMethod(
                    "CommandShopBuyTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeSettlementCommandHandler.CommandShopBuyTyped() 不应继续返回 Dictionary。"
        );
        _test.Eq(
            typeof(GameRuntimeSettlementCommandHandler)
                .GetMethod(
                    "CommandShopSellTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeSettlementCommandHandler.CommandShopSellTyped() 不应继续返回 Dictionary。"
        );
        _test.Eq(
            typeof(GameRuntimeSettlementCommandHandler)
                .GetMethod(
                    "CommandStagecoachTravelTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(GameRuntimeFacade.RuntimeCommandResult),
            "GameRuntimeSettlementCommandHandler.CommandStagecoachTravelTyped() 不应继续返回 Dictionary。"
        );
        _test.Eq(
            typeof(HeadlessGameTestSession)
                .GetMethod(
                    "ChangeBattleEquipmentTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(HeadlessGameTestSession.SessionCommandOutcome),
            "HeadlessGameTestSession battle-equipment helper 应提供 typed outcome，避免 runner 回读 GDictionary。"
        );
        _test.Eq(
            typeof(HeadlessGameTestSession)
                .GetMethod(
                    "GetWorldEncounterAnchorsTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
                ?.ReturnType,
            typeof(IReadOnlyList<EncounterAnchorData>),
            "HeadlessGameTestSession world-data encounter 读取应先停留在 typed anchor list，不应继续回读 GArray。"
        );
        _test.True(
            typeof(HeadlessGameTestSession).GetMethod(
                "ReadArray",
                BindingFlags.Static | BindingFlags.NonPublic
            ) == null,
            "HeadlessGameTestSession 不应继续保留通用 GArray 读取 helper。"
        );
        _test.True(
            typeof(HeadlessGameTestSession).GetMethod(
                "ReadStringName",
                BindingFlags.Static | BindingFlags.NonPublic
            ) == null,
            "HeadlessGameTestSession 不应继续保留无调用方的 GDictionary StringName 读取 helper。"
        );
        _test.True(
            typeof(HeadlessGameTestSession).GetMethod(
                "ReadString",
                BindingFlags.Static | BindingFlags.NonPublic
            ) == null,
            "HeadlessGameTestSession 不应继续保留仅供本地 report 解析使用的 GDictionary string 读取 helper。"
        );
        _test.True(
            typeof(HeadlessGameTestSession).GetMethod(
                "ResultOk",
                BindingFlags.Static | BindingFlags.NonPublic
            ) == null,
            "HeadlessGameTestSession 不应继续保留仅供本地 report 解析使用的 GDictionary ok 读取 helper。"
        );
        _test.True(
            typeof(HeadlessGameTestSession).GetMethod(
                "ReadExactBool",
                BindingFlags.Static | BindingFlags.NonPublic
            ) == null,
            "HeadlessGameTestSession 不应继续保留仅供本地 report 解析使用的 GDictionary bool 读取 helper。"
        );
        _test.True(
            typeof(HeadlessGameTestSession).GetMethod(
                "SessionOutcomeFromDictionary",
                BindingFlags.Static | BindingFlags.NonPublic
            ) == null,
            "HeadlessGameTestSession 不应继续保留只给 battle wait/resolve 结果回读服务的 GDictionary helper。"
        );
        _test.True(
            typeof(HeadlessGameTestSession).GetMethod(
                "TryRead",
                BindingFlags.Static | BindingFlags.NonPublic
            ) == null,
            "HeadlessGameTestSession 不应继续保留通用 GDictionary key lookup helper。"
        );
        _test.True(
            typeof(HeadlessGameTestSession).GetMethod(
                "change_battle_equipment",
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                types: new[]
                {
                    typeof(StringName),
                    typeof(StringName),
                    typeof(StringName),
                    typeof(StringName),
                    typeof(GDictionary),
                },
                modifiers: null
            ) == null,
            "HeadlessGameTestSession.change_battle_equipment 不应继续保留 GDictionary options overload。"
        );
        _test.True(
            typeof(GameTextSnapshotRenderer).GetMethod("render_snapshot") == null,
            "GameTextSnapshotRenderer 不应继续保留无调用方的实例渲染入口。"
        );
    }

    private void TestUtilityHelpersNoLongerRequireGodotRegistration()
    {
        _test.False(
            System.IO.File.Exists(
                ProjectSettings.GlobalizePath("res://scripts/utils/GodotDictionaryExtensions.cs")
            ),
            "GodotDictionaryExtensions.cs 应被删除，不应继续保留空 Godot Dictionary helper。"
        );
        _test.False(
            System.IO.File.Exists(
                ProjectSettings.GlobalizePath("res://scripts/utils/GodotContentResourceLifetime.cs")
            ),
            "GodotContentResourceLifetime.cs 应被删除，registry 不应继续依赖全局 Resource root helper。"
        );
        _test.False(
            System.IO.File.Exists(
                ProjectSettings.GlobalizePath(
                    "res://scripts/systems/battle/runtime/BattleSpecialProfileCommitAdapter.cs"
                )
            ),
            "BattleSpecialProfileCommitAdapter.cs 应已内联删除，special-profile commit 不应保留独立 adapter。"
        );
        _test.False(
            System.IO.File.Exists(
                ProjectSettings.GlobalizePath(
                    "res://scripts/systems/progression/ProgressionContentBundleAdapter.cs"
                )
            ),
            "ProgressionContentBundleAdapter.cs 应已内联删除，identity bundle 读取不应保留独立 adapter。"
        );

        long seed = TrueRandomSeedService.GenerateSeed();
        _test.True(seed > 0, "TrueRandomSeedService.GenerateSeed() 应继续返回正数 seed。");
        int roll = TrueRandomSeedService.RandiRange(3, 1);
        _test.True(
            roll >= 1 && roll <= 3,
            "TrueRandomSeedService.RandiRange() 应继续规范化上下限并返回范围内结果。"
        );

        _test.Eq(
            typeof(DisplaySettingsService).GetMethod("ListResolutionOptions")?.ReturnType,
            typeof(IReadOnlyList<DisplaySettingsService.ResolutionOption>),
            "DisplaySettingsService 分辨率选项不应继续返回 Godot Array。"
        );
        _test.Eq(
            typeof(DisplaySettingsService).GetMethod("SaveSettings")?.GetParameters()[0].ParameterType,
            typeof(DisplaySettingsService.DisplaySettings),
            "DisplaySettingsService 保存入口不应继续接收 Godot Dictionary。"
        );
        _test.True(
            typeof(DisplaySettingsService).GetMethod("save_settings") == null,
            "DisplaySettingsService 不应继续暴露给 GDScript 调用的旧 save_settings() 入口。"
        );
    }

    private void TestProgressionContentRuleHelpersNoLongerRequireGodotRegistration()
    {
        _test.Eq(
            typeof(ProgressionDataUtils)
                .GetMethod(
                    "sorted_string_keys",
                    BindingFlags.NonPublic | BindingFlags.Static
                )
                ?.ReturnType,
            typeof(List<string>),
            "ProgressionDataUtils.sorted_string_keys() 应返回 C# List<string>，不应继续返回 Godot Array。"
        );
        _test.Eq(
            typeof(ProgressionDataUtils)
                .GetMethod(
                    "to_string_name_int_dictionary",
                    BindingFlags.NonPublic | BindingFlags.Static
                )
                ?.ReturnType,
            typeof(Dictionary<StringName, int>),
            "ProgressionDataUtils.to_string_name_int_dictionary() 应返回 C# Dictionary<StringName,int>。"
        );
        _test.Eq(
            typeof(SkillLevelDescriptionContentRules)
                .GetMethod(nameof(SkillLevelDescriptionContentRules.CollectValidationErrors))
                ?.ReturnType,
            typeof(List<string>),
            "SkillLevelDescriptionContentRules 校验核心应返回 C# List<string>。"
        );
        _test.True(
            typeof(SkillLevelDescriptionContentRules).GetMethod("append_validation_errors") == null,
            "SkillLevelDescriptionContentRules 不应保留 Godot Array 追加式校验入口。"
        );
        _test.True(
            CombatTargetTeamContentRules.IsValidSkillTargetTeamFilter("enemy"),
            "CombatTargetTeamContentRules 应继续接受 enemy target team。"
        );
        _test.True(
            DamageTagContentRules.ToDamageTagKind("negative_energy") == DamageTagKind.NegativeEnergy,
            "DamageTagContentRules 应继续接受 negative_energy damage tag。"
        );
        _test.True(
            BattleSaveContentRules.IsValidSaveTag(BattleSaveContentRules.ToStringName(BattleSaveTagKind.Execute)),
            "BattleSaveContentRules 应继续接受 execute save tag。"
        );
        _test.Eq(
            typeof(SkillLevelDescriptionFormatter)
                .GetMethod(
                    "_collect_level_effect_defs",
                    BindingFlags.NonPublic | BindingFlags.Static
                )
                ?.ReturnType,
            typeof(List<CombatEffectDef>),
            "SkillLevelDescriptionFormatter 内部 effect 收集不应继续返回 Godot Array。"
        );
        _test.Eq(
            typeof(SkillLevelDescriptionFormatter)
                .GetMethod(
                    "RenderTemplate",
                    BindingFlags.NonPublic | BindingFlags.Static
                )
                ?.GetParameters()[1]
                .ParameterType,
            typeof(Dictionary<string, Variant>),
            "SkillLevelDescriptionFormatter 内部模板渲染不应继续消费 Godot Dictionary。"
        );
        _test.Eq(
            AttributeGrowthContentRules.GetTierBudget("basic"),
            60,
            "AttributeGrowthContentRules 应继续返回 basic 成长预算。"
        );
        _test.Eq(
            BodySizeContentRules.GetBodySizeForCategory("large"),
            3,
            "BodySizeContentRules 应继续把 large 映射到 body_size=3。"
        );
        _test.Eq(
            BodySizeContentRules.GetFootprintForCategory("gargantuan"),
            new Vector2I(3, 3),
            "BodySizeContentRules 应继续把 gargantuan 映射到 3x3 footprint。"
        );
        _test.True(
            QuestProviderContentRules.IsSupportedProviderId("service_contract_board"),
            "QuestProviderContentRules 应继续接受任务板 provider。"
        );
        _test.True(
            PendingCharacterRewardContentRules.RequiresSkillTarget(
                PendingCharacterRewardContentRules.ToStringName(PendingCharacterRewardEntryKind.SkillMastery)
            ),
            "PendingCharacterRewardContentRules 应继续识别需要 skill target 的奖励条目。"
        );
        _test.True(
            CombatSkillTargetingContentRules.IsValidAreaPattern("cone"),
            "CombatSkillTargetingContentRules 应继续接受 cone area pattern。"
        );
        _test.True(
            !string.IsNullOrEmpty(
                TraitTriggerContentRules.GetDispatchKey(
                    RaceTraitDef.ToStringName(RaceTraitEffectKind.HalflingLuck),
                    TraitTriggerContentRules.ToStringName(TraitTriggerKind.OnNaturalOne)
                )
            ),
            "TraitTriggerContentRules 应继续为 halfling luck 提供 dispatch key。"
        );
        _test.Eq(
            string.Join(
                ",",
                ProgressionDataUtils.sorted_string_keys(
                    new GDictionary { ["beta"] = 2, ["alpha"] = 1 }
                )
            ),
            "alpha,beta",
            "ProgressionDataUtils.sorted_string_keys() 应继续按 ordinal 文本顺序返回 key。"
        );
        _test.Eq(
            ProgressionDataUtils.sorted_string_keys(null).Count,
            0,
            "ProgressionDataUtils.sorted_string_keys(null) 应返回空 typed list。"
        );
        Dictionary<StringName, int> normalizedIntMap =
            ProgressionDataUtils.to_string_name_int_dictionary(
                new GDictionary { ["alpha"] = 2, [new StringName("beta")] = 3 }
            );
        _test.Eq(
            normalizedIntMap[new StringName("alpha")],
            2,
            "ProgressionDataUtils.to_string_name_int_dictionary() 应规范化 string key。"
        );
        _test.Eq(
            normalizedIntMap[new StringName("beta")],
            3,
            "ProgressionDataUtils.to_string_name_int_dictionary() 应保留 StringName key。"
        );

        List<string> levelDescriptionErrors =
            SkillLevelDescriptionContentRules.CollectValidationErrors(
                "level_description_test",
                new SkillDef
                {
                    max_level = 1,
                    level_description_template = "等级{value}",
                    level_description_configs = new GDictionary
                    {
                        ["0"] = new GDictionary { ["value"] = "0" },
                        ["2"] = "旧格式",
                    },
                }
            );
        _test.True(
            levelDescriptionErrors.Count >= 3,
            "SkillLevelDescriptionContentRules 应继续拒绝非法等级描述配置。"
        );
    }

    private void TestProgressionRuleHelpersNoLongerRequireGodotRegistration()
    {
        _test.Eq(
            typeof(CharacterManagementModule)
                .GetMethod(
                    "_collect_active_stage_advancement_modifiers",
                    BindingFlags.NonPublic | BindingFlags.Instance
                )
                ?.ReturnType,
            typeof(List<StageAdvancementModifier>),
            "CharacterManagementModule 收集 active stage advancement 时不应继续用 Godot Array 作为中间状态。"
        );
        _test.Eq(
            typeof(LevelGrowthEvaluationService)
                .GetField("_skillDefs", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.FieldType,
            typeof(Dictionary<StringName, SkillDef>),
            "LevelGrowthEvaluationService 技能定义缓存不应继续使用 Godot Dictionary。"
        );
        _test.Eq(
            typeof(PracticeGrowthService)
                .GetField("_skillDefs", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.FieldType,
            typeof(Dictionary<StringName, SkillDef>),
            "PracticeGrowthService 技能定义缓存不应继续使用 Godot Dictionary。"
        );
        _test.Eq(
            typeof(PracticeGrowthService)
                .GetField("PracticeTracks", BindingFlags.NonPublic | BindingFlags.Static)
                ?.FieldType,
            typeof(HashSet<StringName>),
            "PracticeGrowthService 功法轨道集合不应继续使用 Godot Array。"
        );
    }

    private void TestHeadlessTextCommandResultNoLongerRequiresGlobalClass()
    {
        Type resultType = typeof(GameTextCommandResult);
        _test.True(
            resultType.GetMethod(
                "AddAssertion",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
            )?.GetParameters().Length == 5,
            "GameTextCommandResult 应通过 typed owner helper 维护 assertion backing。"
        );
        _test.True(
            resultType.GetMethod(
                "SetSnapshot",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
            )?.GetParameters()[0].ParameterType
                == typeof(IReadOnlyDictionary<string, object>),
            "GameTextCommandResult 应通过 typed owner helper 维护 snapshot backing。"
        );
        MethodInfo executeExpect = typeof(GameTextCommandRunner).GetMethod(
            "ExecuteExpect",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        MethodInfo executeCommand = typeof(GameTextCommandRunner).GetMethod(
            "ExecuteCommand",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        MethodInfo ensureWorldContext = typeof(GameTextCommandRunner).GetMethod(
            "EnsureWorldContext",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        MethodInfo missingWorldError = typeof(GameTextCommandRunner).GetMethod(
            "MissingWorldError",
            BindingFlags.Static | BindingFlags.NonPublic
        );
        MethodInfo parseIntArgument = typeof(GameTextCommandRunner).GetMethod(
            "ParseIntArgument",
            BindingFlags.Static | BindingFlags.NonPublic
        );
        MethodInfo parseCoordArgument = typeof(GameTextCommandRunner).GetMethod(
            "ParseCoordArgument",
            BindingFlags.Static | BindingFlags.NonPublic
        );
        MethodInfo parseNamedArgsTyped = typeof(GameTextCommandRunner).GetMethod(
            "ParseNamedArgsTyped",
            BindingFlags.Static | BindingFlags.NonPublic
        );
        _test.Eq(
            executeExpect?.GetParameters()[1].ParameterType,
            typeof(IReadOnlyDictionary<string, object>),
            "GameTextCommandRunner expect 路径不应回读 GDictionary snapshot。"
        );
        _test.True(
            executeExpect?.ReturnType != typeof(GDictionary),
            "GameTextCommandRunner expect 路径不应继续把断言结果退回 GDictionary。"
        );
        _test.True(
            executeCommand?.ReturnType != typeof(GDictionary),
            "GameTextCommandRunner 内部命令分发不应继续返回 GDictionary。"
        );
        _test.True(
            ensureWorldContext?.ReturnType != typeof(GDictionary),
            "GameTextCommandRunner world context gate 不应继续返回 GDictionary。"
        );
        _test.True(
            missingWorldError?.ReturnType != typeof(GDictionary),
            "GameTextCommandRunner missing-world helper 不应继续返回 GDictionary。"
        );
        _test.True(
            parseIntArgument?.ReturnType != typeof(GDictionary),
            "GameTextCommandRunner 内部整数解析 helper 不应继续返回 GDictionary。"
        );
        _test.True(
            parseCoordArgument?.ReturnType != typeof(GDictionary),
            "GameTextCommandRunner 内部坐标解析 helper 不应继续返回 GDictionary。"
        );
        _test.Eq(
            parseNamedArgsTyped?.ReturnType,
            typeof(Dictionary<string, object>),
            "GameTextCommandRunner settlement named-arg helper 应先停留在 typed Dictionary。"
        );
        _test.True(
            typeof(GameTextCommandRunner).GetMethod(
                "ParseNamedArgs",
                BindingFlags.Static | BindingFlags.NonPublic
            ) == null,
            "GameTextCommandRunner 不应继续保留 GDictionary ParseNamedArgs helper。"
        );
    }

    private void TestGridFootprintStateUsesPublicBehavior()
    {
        var gridSystem = new WorldMapGridSystem();
        gridSystem.Setup(new Vector2I(2, 2), new Vector2I(4, 4));

        _test.False(
            gridSystem.RegisterFootprint("", new Vector2I(1, 1), Vector2I.One),
            "空 entity_id 不应注册 footprint。"
        );
        _test.Eq(gridSystem.GetOccupantRoot(new Vector2I(1, 1)), "", "空 entity_id 注册失败后不应占格。");

        _test.True(
            gridSystem.RegisterFootprint("camp", new Vector2I(1, 1), new Vector2I(2, 2)),
            "合法 footprint 应可注册。"
        );
        _test.Eq(gridSystem.GetOccupantRoot(new Vector2I(1, 1)), "camp", "注册后 origin 应暴露占位根。");
        _test.Eq(gridSystem.GetOccupantRoot(new Vector2I(2, 2)), "camp", "注册后 footprint 覆盖格应暴露占位根。");

        _test.False(
            gridSystem.CanPlaceFootprint(new Vector2I(2, 2), Vector2I.One),
            "已有 footprint 的格子不应允许再次占用。"
        );
        _test.False(
            gridSystem.RegisterFootprint("camp", new Vector2I(7, 7), new Vector2I(2, 2)),
            "同一 entity 移动到越界 footprint 应失败。"
        );
        _test.Eq(
            gridSystem.GetOccupantRoot(new Vector2I(1, 1)),
            "camp",
            "同一 entity 移动失败后应恢复原 footprint。"
        );

        gridSystem.ClearFootprint("camp");
        _test.Eq(gridSystem.GetOccupantRoot(new Vector2I(1, 1)), "", "清理 footprint 后 origin 不应继续占格。");
        _test.Eq(gridSystem.GetOccupantRoot(new Vector2I(2, 2)), "", "清理 footprint 后覆盖格不应继续占格。");
    }

    private void TestGridCellSurfaceKeepsMinimalRuntimeContract()
    {
        var gridSystem = new WorldMapGridSystem();
        gridSystem.Setup(new Vector2I(2, 2), new Vector2I(4, 4));
        gridSystem.RegisterFootprint("camp", new Vector2I(5, 6), Vector2I.One);

        WorldMapCellData cell = gridSystem.GetCell(new Vector2I(5, 6));
        _test.True(cell != null, "世界地图格子读取面应继续返回有效格子对象。");
        _test.Eq(cell.coord, new Vector2I(5, 6), "格子读取面应继续暴露正式坐标。");
        _test.Eq(cell.chunk_coord, new Vector2I(1, 1), "格子读取面应继续暴露区块坐标。");
        _test.Eq(cell.occupant_id, "camp", "格子读取面应继续暴露占用者 id。");
        _test.Eq(cell.footprint_root_id, "camp", "格子读取面应继续暴露占位根 id。");
        _test.False(
            typeof(WorldMapCellData).GetMember(
                "terrain_visual_type",
                BindingFlags.Public | BindingFlags.Instance
            ).Length > 0,
            "WorldMapCellData 不应继续暴露未消费的 terrain_visual_type 字段。"
        );
        _test.False(
            typeof(WorldMapGridSystem).GetMethod(
                "get_cells_in_rect",
                BindingFlags.Public | BindingFlags.Instance
            ) != null,
            "WorldMapGridSystem 不应继续保留无调用方的 get_cells_in_rect()。"
        );
    }

    private void TestVisibilityRebuildIgnoresForeignFactionSources()
    {
        var fogSystem = new WorldMapFogSystem();
        fogSystem.Setup(new Vector2I(8, 8));

        var playerSource = new VisionSourceData("scout", new Vector2I(2, 2), 1, "player");
        var hostileSource = new VisionSourceData("raider", new Vector2I(5, 5), 1, "hostile");

        fogSystem.RebuildVisibilityForFaction("player", new[] { playerSource, hostileSource });

        _test.True(
            fogSystem.IsVisible(new Vector2I(2, 2), "player"),
            "玩家阵营的自有视野源应继续正常生效。"
        );
        _test.False(
            fogSystem.IsVisible(new Vector2I(5, 5), "player"),
            "foreign faction 的视野源不应污染当前阵营可见区。"
        );
    }

    private void TestFogRevealExportLoadKeepsRevealedCells()
    {
        var fogSystem = new WorldMapFogSystem();
        fogSystem.Setup(new Vector2I(8, 8));

        List<Vector2I> revealedCoords = fogSystem.RevealDiamond(
            new Vector2I(3, 3),
            1,
            "player"
        );
        _test.True(revealedCoords.Contains(new Vector2I(3, 3)), "迷雾揭示应返回中心格。");

        GDictionary persistedState = fogSystem.ExportPersistentState();
        var restoredFogSystem = new WorldMapFogSystem();
        restoredFogSystem.Setup(new Vector2I(8, 8), persistedState);

        _test.True(
            restoredFogSystem.IsExplored(new Vector2I(3, 3), "player"),
            "持久化恢复后 paid reveal 中心格应保持已探索。"
        );
        _test.False(
            restoredFogSystem.IsVisible(new Vector2I(3, 3), "player"),
            "持久化恢复不应把 paid reveal 误当作当前可见。"
        );

        var distantSource = new VisionSourceData("scout", new Vector2I(7, 7), 0, "player");
        restoredFogSystem.RebuildVisibilityForFaction("player", new[] { distantSource });
        _test.True(
            restoredFogSystem.IsExplored(new Vector2I(3, 3), "player"),
            "后续可见性刷新不应清除已持久化的 paid reveal。"
        );
    }

}
