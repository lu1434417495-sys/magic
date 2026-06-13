using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_battle_ai_trace_summary_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestTraceSummaryTypesArePlainCSharpDtos();
            TestScoreInputTraceProjectionUsesTypedState();
            TestCommandSummaryCopiesBattleCommandAndProjectsToDictionary();
            TestBattleCommandOccupiedSlotsUseTypedBackingList();
            TestBattleCommandTargetCoordsUseTypedBackingList();
            TestBattleCommandTargetUnitIdsUseTypedBackingList();
            TestPendingProfessionChoiceUsesTypedBackingCollections();
            TestUnitProgressPendingProfessionChoicesUseTypedBackingList();
            TestUnitProgressSkillsUseTypedBackingDictionary();
            TestUnitProgressStringNameCollectionsUseTypedBackingLists();
            TestUnitProgressMergedSkillSourceMapUsesTypedBackingDictionary();
            TestUnitProgressAttributeGrowthUsesTypedBackingDictionary();
            TestUnitProgressAchievementProgressUsesTypedBackingDictionary();
            TestUnitProgressProfessionsUseTypedBackingDictionary();
            TestCharacterProgressionDeltaUsesTypedBackingCollections();
            TestBattleEventBatchChangedUnitIdsUseTypedBackingList();
            TestBattleEventBatchChangedCoordsUseTypedBackingList();
            TestBattleEventBatchReportEntriesUseTypedBackingList();
            TestBattleEventBatchProgressionDeltasUseTypedBackingList();
            TestBattlePreviewTargetUnitIdsUseTypedBackingList();
            TestBattlePreviewTargetCoordsUseTypedBackingList();
            TestBattlePreviewRandomChainCandidatesUseTypedBackingList();
            TestBattlePreviewLogLinesUseTypedBackingList();
            TestBattleEventBatchLogLinesUseTypedBackingList();
            TestBattlePreviewDamagePreviewUsesTypedBackingPayload();
            TestBattlePreviewDamagePreviewSetterDecodesProjectedPayload();
            TestBattleRuntimeModuleAiTurnTracesUseTypedBackingProjection();
            TestActionTraceProjectsStableDictionaryShape();
            TestEnemyAiActionHelperUsesTypedTraceState();
            TestWaitActionActiveRestUsesTypedProfile();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }

        Quit(_test.Finish("Battle AI trace summary regression"));
    }

    private void TestTraceSummaryTypesArePlainCSharpDtos()
    {
        AssertPlainDto(typeof(AiCommandSummary), "AiCommandSummary");
        AssertPlainDto(typeof(AiCandidateSummary), "AiCandidateSummary");
        AssertPlainDto(typeof(AiActionTrace), "AiActionTrace");
        AssertPlainDto(typeof(BattleAiTurnTraceProjection), "BattleAiTurnTraceProjection");
        AssertPlainDto(typeof(BattleAiTraceTransitionProjection), "BattleAiTraceTransitionProjection");
        AssertPlainDto(typeof(BattleAiTraceTransitionConditionProjection), "BattleAiTraceTransitionConditionProjection");
        AssertPlainDto(typeof(BattleAiTraceUnitSnapshotProjection), "BattleAiTraceUnitSnapshotProjection");
        AssertPlainDto(typeof(BattleAiTraceUnitResultProjection), "BattleAiTraceUnitResultProjection");
        AssertPlainDto(typeof(BattleAiTraceExecutionResultProjection), "BattleAiTraceExecutionResultProjection");
        AssertPublicApiDoesNotExposeGodotTypes(typeof(AiCommandSummary));
        AssertPublicApiDoesNotExposeGodotTypes(typeof(AiCandidateSummary));
        AssertPublicApiDoesNotExposeGodotTypes(typeof(AiActionTrace));
        AssertPublicApiDoesNotExposeGodotTypes(typeof(BattleAiTurnTraceProjection));
        AssertPublicApiDoesNotExposeGodotTypes(typeof(BattleAiTraceTransitionProjection));
        AssertPublicApiDoesNotExposeGodotTypes(typeof(BattleAiTraceTransitionConditionProjection));
        AssertPublicApiDoesNotExposeGodotTypes(typeof(BattleAiTraceUnitSnapshotProjection));
        AssertPublicApiDoesNotExposeGodotTypes(typeof(BattleAiTraceUnitResultProjection));
        AssertPublicApiDoesNotExposeGodotTypes(typeof(BattleAiTraceExecutionResultProjection));
        AssertPublicPropertiesDoNotExposeGodotTypes(typeof(AiCommandSummary));
        AssertPublicPropertiesDoNotExposeGodotTypes(typeof(AiCandidateSummary));
        AssertPublicPropertiesDoNotExposeGodotTypes(typeof(AiActionTrace));
        AssertPublicPropertiesDoNotExposeGodotTypes(typeof(BattleAiTurnTraceProjection));
        AssertPublicPropertiesDoNotExposeGodotTypes(typeof(BattleAiTraceTransitionProjection));
        AssertPublicPropertiesDoNotExposeGodotTypes(typeof(BattleAiTraceTransitionConditionProjection));
        AssertPublicPropertiesDoNotExposeGodotTypes(typeof(BattleAiTraceUnitSnapshotProjection));
        AssertPublicPropertiesDoNotExposeGodotTypes(typeof(BattleAiTraceUnitResultProjection));
        AssertPublicPropertiesDoNotExposeGodotTypes(typeof(BattleAiTraceExecutionResultProjection));

        _test.True(
            typeof(AiCommandSummary).GetMethod("FromCommand") != null
                && typeof(AiCommandSummary).GetMethod("from_command") == null,
            "AiCommandSummary should expose FromCommand() and not keep from_command()."
        );
        _test.True(
            typeof(AiActionTrace).GetMethod("IsEmpty") != null
                && typeof(AiActionTrace).GetMethod("is_empty") == null,
            "AiActionTrace should expose IsEmpty() and not keep is_empty()."
        );
        _test.True(
            typeof(AiActionTrace).GetMethod("to_dict") == null
                && typeof(AiCandidateSummary).GetMethod("to_dict") == null
                && typeof(AiCommandSummary).GetMethod("to_dict") == null,
            "AI trace summary DTOs should not keep GDScript-style ToDictionary() API."
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
            "AI trace summary DTOs 应继续先走 internal typed trace projection，再在边界投影到 Godot dictionary。"
        );
    }

    private void TestScoreInputTraceProjectionUsesTypedState()
    {
        MethodInfo traceProjection = typeof(BattleAiScoreInput).GetMethod(
            "ToTraceDictionary",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        _test.True(
            traceProjection != null
                && traceProjection.ReturnType == typeof(Dictionary<string, object>),
            "BattleAiScoreInput 应继续提供 internal typed trace projection。"
        );
        _test.True(
            typeof(BattleAiScoreRuntimeMetadata).GetMethod(
                "ToTraceDictionary",
                BindingFlags.Instance | BindingFlags.NonPublic
            )?.ReturnType == typeof(Dictionary<string, object>),
            "BattleAiScoreRuntimeMetadata 应继续直接提供 typed trace projection。"
        );
        _test.True(
            typeof(BattleSpecialProfilePreviewFacts).GetMethod(
                "ToTraceDictionary",
                BindingFlags.Instance | BindingFlags.NonPublic
            )?.ReturnType == typeof(Dictionary<string, object>),
            "BattleSpecialProfilePreviewFacts 应继续直接提供 typed trace projection。"
        );
        _test.True(
            typeof(MeteorSwarmNumericSummary).GetMethod(
                "ToTraceDictionary",
                BindingFlags.Instance | BindingFlags.NonPublic
            )?.ReturnType == typeof(Dictionary<string, object>),
            "MeteorSwarmNumericSummary 应继续直接提供 typed trace projection。"
        );
        _test.True(
            typeof(BattleAttackRollModifierSpec).GetMethod(
                "ToTraceDictionary",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null
            )?.ReturnType == typeof(Dictionary<string, object>),
            "BattleAttackRollModifierSpec 应继续直接提供 typed trace projection。"
        );
        _test.True(
            typeof(BattleAiScoreService.DamageSaveEstimate).GetMethod(
                "ToTraceDictionary",
                BindingFlags.Instance | BindingFlags.NonPublic
            )?.ReturnType == typeof(Dictionary<string, object>),
            "DamageSaveEstimate 应继续直接提供 typed trace projection。"
        );
        _test.True(
            typeof(BattleAiScoreService.DamageEstimateBreakdown).GetMethod(
                "ToTraceDictionary",
                BindingFlags.Instance | BindingFlags.NonPublic
            )?.ReturnType == typeof(Dictionary<string, object>),
            "DamageEstimateBreakdown 应继续直接提供 typed trace projection。"
        );

        var scoreInput = new BattleAiScoreInput
        {
            action_kind = "skill",
            action_label = "trace projection",
            action_intent = "offense",
            score_bucket_id = "meteor",
            total_score = 17,
            runtime_action_metadata = new BattleAiScoreRuntimeMetadata
            {
                generated = true,
                skill_id = "meteor_swarm",
                action_id = "cast_meteor",
            },
        };
        scoreInput.target_unit_ids.Add("enemy_a");
        scoreInput.high_priority_reasons["enemy_a"] = new List<string> { "focus_fire" };

        Dictionary<string, object> tracePayload = scoreInput.ToTraceDictionary();
        _test.Eq(
            tracePayload["skill_id"] as string,
            "meteor_swarm",
            "typed trace projection should resolve skill_id from typed metadata."
        );
        _test.True(
            tracePayload["target_unit_ids"] is List<StringName> targetIds && targetIds.Count == 1,
            "typed trace projection should preserve typed target unit lists."
        );
        _test.True(
            tracePayload["high_priority_reasons"]
                is Dictionary<string, object> highPriorityReasons
                && highPriorityReasons.ContainsKey("enemy_a"),
            "typed trace projection should preserve typed high-priority reason maps."
        );

        _test.True(
            typeof(BattleAiContext).GetMethod(
                    "BuildTurnTraceTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )?.ReturnType == typeof(BattleAiTurnTraceProjection)
                && typeof(BattleAiTurnTraceProjection).GetProperty(
                    "ScoreInput",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )?.PropertyType == typeof(BattleAiScoreInput)
                && typeof(BattleAiTurnTraceProjection).GetMethod(
                    "ToTraceDictionary",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )?.ReturnType == typeof(Dictionary<string, object>),
            "BattleAiContext turn trace 导出应继续先组装 typed projection，再在 public Godot API 边界投影。"
        );
    }

    private void TestCommandSummaryCopiesBattleCommandAndProjectsToDictionary()
    {
        var command = new BattleCommand
        {
            command_type = "skill",
            unit_id = "caster",
            skill_id = "bolt",
            skill_variant_id = "wide",
            target_unit_id = "target",
            target_coord = new Vector2I(3, 4),
        };
        command.SetTargetUnitIds(new[] { new StringName("target"), new StringName("support") });
        command.SetTargetCoords(new[] { new Vector2I(3, 4), new Vector2I(4, 4) });

        AiCommandSummary summary = AiCommandSummary.FromCommand(command);
        command.target_unit_ids.Add("late_mutation");
        command.target_coords.Add(new Vector2I(9, 9));

        _test.Eq(summary.CommandType, "skill", "CommandType should copy command_type.");
        _test.Eq(summary.UnitId, "caster", "UnitId should copy unit_id.");
        _test.Eq(summary.TargetUnitIds.Count, 2, "TargetUnitIds should be copied into a C# list.");
        _test.Eq(summary.TargetCoords.Count, 2, "TargetCoords should be copied into a C# list.");

        Godot.Collections.Dictionary payload = summary.ToDictionary();
        _test.Eq(payload["command_type"].AsString(), "skill", "Projection should include command_type.");
        _test.Eq(
            payload["target_unit_ids"].AsGodotArray().Count,
            2,
            "Projection should preserve copied target unit ids."
        );
        _test.Eq(
            payload["target_coords"].AsGodotArray().Count,
            2,
            "Projection should preserve copied target coords."
        );
    }

    private void TestBattleCommandOccupiedSlotsUseTypedBackingList()
    {
        _test.True(
            typeof(BattleCommand).GetProperty("EquipmentOccupiedSlotIdsTyped", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.PropertyType == typeof(IReadOnlyList<StringName>),
            "BattleCommand 应继续通过 internal typed occupied-slot list 承载 runtime 业务态。"
        );

        var command = new BattleCommand();
        command.equipment_occupied_slot_ids = new Godot.Collections.Array<StringName>
        {
            "main_hand",
        };

        Godot.Collections.Array<StringName> projected = command.equipment_occupied_slot_ids;
        projected.Add("off_hand");

        _test.Eq(
            command.equipment_occupied_slot_ids.Count,
            1,
            "BattleCommand.equipment_occupied_slot_ids public Godot property 应保持边界投影，不应泄漏可变内部数组引用。"
        );
        _test.Eq(
            command.EquipmentOccupiedSlotIdsTyped.Count,
            1,
            "BattleCommand occupied-slot runtime 业务态应保持 typed list。"
        );
        _test.Eq(
            command.EquipmentOccupiedSlotIdsTyped[0],
            new StringName("main_hand"),
            "BattleCommand occupied-slot typed backing 应保留正式 StringName 值。"
        );
    }

    private void TestBattleCommandTargetCoordsUseTypedBackingList()
    {
        _test.True(
            typeof(BattleCommand).GetProperty("TargetCoordsTyped", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.PropertyType == typeof(IReadOnlyList<Vector2I>),
            "BattleCommand target_coords 应继续通过 internal typed list 承载 runtime 业务态。"
        );

        var command = new BattleCommand();
        command.target_coords = new Godot.Collections.Array<Vector2I>
        {
            new Vector2I(3, 4),
            new Vector2I(4, 4),
        };

        Godot.Collections.Array<Vector2I> projected = command.target_coords;
        projected.Add(new Vector2I(9, 9));

        _test.Eq(
            command.target_coords.Count,
            2,
            "BattleCommand.target_coords public Godot property 应保持边界投影，不应泄漏可变内部数组引用。"
        );
        _test.Eq(
            command.TargetCoordsTyped.Count,
            2,
            "BattleCommand target-coord runtime 业务态应保持 typed list。"
        );
        _test.Eq(
            command.TargetCoordsTyped[0],
            new Vector2I(3, 4),
            "BattleCommand target-coord typed backing 应保留正式 Vector2I 值。"
        );
    }

    private void TestBattleCommandTargetUnitIdsUseTypedBackingList()
    {
        _test.True(
            typeof(BattleCommand).GetProperty("TargetUnitIdsTyped", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.PropertyType == typeof(IReadOnlyList<StringName>),
            "BattleCommand target_unit_ids 应继续通过 internal typed list 承载 runtime 业务态。"
        );

        var command = new BattleCommand();
        command.target_unit_ids = new Godot.Collections.Array<StringName>
        {
            "enemy_a",
            "enemy_b",
        };

        Godot.Collections.Array<StringName> projected = command.target_unit_ids;
        projected.Add("enemy_c");

        _test.Eq(
            command.target_unit_ids.Count,
            2,
            "BattleCommand.target_unit_ids public Godot property 应保持边界投影，不应泄漏可变内部数组引用。"
        );
        _test.Eq(
            command.TargetUnitIdsTyped.Count,
            2,
            "BattleCommand target-unit runtime 业务态应保持 typed list。"
        );
        _test.Eq(
            command.TargetUnitIdsTyped[0],
            new StringName("enemy_a"),
            "BattleCommand target-unit typed backing 应保留正式 StringName 值。"
        );
    }

    private void TestCharacterProgressionDeltaUsesTypedBackingCollections()
    {
        _test.True(
            typeof(CharacterProgressionDelta).GetProperty(
                "ChangedProfessionIdsTyped",
                BindingFlags.Instance | BindingFlags.NonPublic
            )?.PropertyType == typeof(IReadOnlyList<StringName>),
            "CharacterProgressionDelta.changed_profession_ids 应继续通过 internal typed list 承载 runtime 业务态。"
        );
        _test.True(
            typeof(CharacterProgressionDelta).GetProperty(
                "PendingProfessionChoicesTyped",
                BindingFlags.Instance | BindingFlags.NonPublic
            )?.PropertyType == typeof(IReadOnlyList<PendingProfessionChoice>),
            "CharacterProgressionDelta.pending_profession_choices 应继续通过 internal typed list 承载 runtime 业务态。"
        );
        _test.True(
            typeof(CharacterProgressionDelta).GetProperty(
                "MasteryChangesTyped",
                BindingFlags.Instance | BindingFlags.NonPublic
            )?.PropertyType == typeof(IReadOnlyList<CharacterMasteryChangeFact>),
            "CharacterProgressionDelta.mastery_changes 应继续通过 internal typed mastery fact list 承载 runtime 业务态。"
        );
        _test.True(
            typeof(CharacterProgressionDelta).GetProperty(
                "KnowledgeChangesTyped",
                BindingFlags.Instance | BindingFlags.NonPublic
            )?.PropertyType == typeof(IReadOnlyList<CharacterKnowledgeChangeFact>),
            "CharacterProgressionDelta.knowledge_changes 应继续通过 internal typed knowledge fact list 承载 runtime 业务态。"
        );
        _test.True(
            typeof(CharacterProgressionDelta).GetProperty(
                "AttributeChangesTyped",
                BindingFlags.Instance | BindingFlags.NonPublic
            )?.PropertyType == typeof(IReadOnlyList<CharacterAttributeChangeFact>),
            "CharacterProgressionDelta.attribute_changes 应继续通过 internal typed attribute fact list 承载 runtime 业务态。"
        );

        var delta = new CharacterProgressionDelta { member_id = new StringName("hero_a") };
        var pendingChoice = new PendingProfessionChoice();
        pendingChoice.candidate_profession_ids = new Godot.Collections.Array<StringName> { "warrior" };
        pendingChoice.SetTargetRank("warrior", 1);
        var masteryChange = new CharacterMasteryChangeFact(
            "slash",
            "Slash",
            3,
            "battle",
            "Battle",
            "first hit"
        );
        var attributeChange = new CharacterAttributeChangeFact(
            "agility",
            "Agility",
            1,
            "growth",
            progressDelta: 60
        );

        delta.AddChangedProfessionId(new StringName("warrior"));
        delta.AddPendingProfessionChoice(pendingChoice);
        delta.AddMasteryChange(masteryChange);
        delta.AddAttributeChange(attributeChange);

        delta.changed_profession_ids.Add(new StringName("rogue"));
        delta.pending_profession_choices.Add(new PendingProfessionChoice());
        delta.mastery_changes.Add(new GDictionary { ["skill_id"] = "bow" });
        delta.attribute_changes.Add(new GDictionary { ["attribute_id"] = "strength" });

        _test.Eq(
            delta.changed_profession_ids.Count,
            1,
            "CharacterProgressionDelta.changed_profession_ids public Godot property 应保持边界投影。"
        );
        _test.Eq(
            delta.pending_profession_choices.Count,
            1,
            "CharacterProgressionDelta.pending_profession_choices public Godot property 应保持边界投影。"
        );
        _test.Eq(
            delta.mastery_changes.Count,
            1,
            "CharacterProgressionDelta.mastery_changes public Godot property 应保持边界投影。"
        );
        _test.Eq(
            delta.attribute_changes.Count,
            1,
            "CharacterProgressionDelta.attribute_changes public Godot property 应保持边界投影。"
        );
        _test.Eq(
            delta.ChangedProfessionIdsTyped.Count,
            1,
            "CharacterProgressionDelta changed-profession runtime 业务态应保持 typed list。"
        );
        _test.Eq(
            delta.PendingProfessionChoicesTyped.Count,
            1,
            "CharacterProgressionDelta pending-choice runtime 业务态应保持 typed list。"
        );
        _test.Eq(
            delta.MasteryChangesTyped.Count,
            1,
            "CharacterProgressionDelta mastery runtime 业务态应保持 typed list。"
        );
        _test.Eq(
            delta.AttributeChangesTyped.Count,
            1,
            "CharacterProgressionDelta attribute runtime 业务态应保持 typed list。"
        );
        _test.Eq(
            delta.MasteryChangesTyped[0].MasteryAmount,
            3,
            "CharacterProgressionDelta mastery typed backing 应保留 typed payload。"
        );
        _test.Eq(
            delta.AttributeChangesTyped[0].Delta,
            1,
            "CharacterProgressionDelta attribute typed backing 应保留 typed payload。"
        );

    }

    private void TestUnitProgressPendingProfessionChoicesUseTypedBackingList()
    {
        _test.True(
            typeof(UnitProgress).GetProperty(
                "PendingProfessionChoicesTyped",
                BindingFlags.Instance | BindingFlags.NonPublic
            )?.PropertyType == typeof(IReadOnlyList<PendingProfessionChoice>),
            "UnitProgress.pending_profession_choices 应继续通过 internal typed list 承载 runtime 业务态。"
        );

        var progress = new UnitProgress();
        var choice = new PendingProfessionChoice();
        choice.AddCandidateProfessionId("warrior");
        choice.SetTargetRank("warrior", 2);
        progress.AddPendingProfessionChoice(choice);

        var projectedChoices = progress.pending_profession_choices;
        projectedChoices.Add(new PendingProfessionChoice());
        projectedChoices[0].SetTargetRank("warrior", 9);

        _test.Eq(
            progress.pending_profession_choices.Count,
            1,
            "UnitProgress.pending_profession_choices public Godot property 应保持边界投影。"
        );
        _test.Eq(
            progress.PendingProfessionChoicesTyped.Count,
            1,
            "UnitProgress pending-choice runtime 业务态应保持 typed list。"
        );
        _test.True(
            progress.PendingProfessionChoicesTyped[0].TryGetTargetRank("warrior", out int targetRank)
                && targetRank == 2,
            "UnitProgress pending-choice typed backing 应 deep copy 正式 PendingProfessionChoice 状态。"
        );

    }

    private void TestUnitProgressSkillsUseTypedBackingDictionary()
    {
        _test.True(
            typeof(UnitProgress).GetProperty(
                "SkillsTyped",
                BindingFlags.Instance | BindingFlags.NonPublic
            )?.PropertyType == typeof(IReadOnlyDictionary<StringName, UnitSkillProgress>),
            "UnitProgress.skills 应继续通过 internal typed dictionary 承载 runtime 业务态。"
        );

        var progress = new UnitProgress();
        progress.SetSkillProgress(
            new UnitSkillProgress
            {
                skill_id = "slash",
                is_learned = true,
                skill_level = 2,
            }
        );

        GDictionary projected = progress.skills;
        UnitSkillProgress projectedSkill = projected["slash"].AsGodotObject() as UnitSkillProgress;
        projectedSkill.skill_level = 9;
        projected["slash"] = projectedSkill;

        _test.True(
            progress.SkillsTyped.TryGetValue(
                new StringName("slash"),
                out UnitSkillProgress stored
            ) && stored.skill_level == 2,
            "UnitProgress skill runtime 业务态应保持 typed dictionary，并隔离 public Godot projection 的嵌套对象变更。"
        );
        _test.Eq(
            progress.GetSkillProgress("slash")?.skill_level ?? 0,
            2,
            "UnitProgress.skills public Godot property 应保持边界投影。"
        );

    }

    private void TestUnitProgressStringNameCollectionsUseTypedBackingLists()
    {
        _test.True(
            typeof(UnitProgress).GetProperty(
                "KnownKnowledgeIdsTyped",
                BindingFlags.Instance | BindingFlags.NonPublic
            )?.PropertyType == typeof(IReadOnlyList<StringName>),
            "UnitProgress.known_knowledge_ids 应继续通过 internal typed list 承载 runtime 业务态。"
        );
        _test.True(
            typeof(UnitProgress).GetProperty(
                "ActiveCoreSkillIdsTyped",
                BindingFlags.Instance | BindingFlags.NonPublic
            )?.PropertyType == typeof(IReadOnlyList<StringName>),
            "UnitProgress.active_core_skill_ids 应继续通过 internal typed list 承载 runtime 业务态。"
        );
        _test.True(
            typeof(UnitProgress).GetProperty(
                "BlockedRelearnSkillIdsTyped",
                BindingFlags.Instance | BindingFlags.NonPublic
            )?.PropertyType == typeof(IReadOnlyList<StringName>),
            "UnitProgress.blocked_relearn_skill_ids 应继续通过 internal typed list 承载 runtime 业务态。"
        );
        _test.True(
            typeof(UnitProgress).GetProperty(
                "UnlockedCombatResourceIdsTyped",
                BindingFlags.Instance | BindingFlags.NonPublic
            )?.PropertyType == typeof(IReadOnlyList<StringName>),
            "UnitProgress.unlocked_combat_resource_ids 应继续通过 internal typed list 承载 runtime 业务态。"
        );
        _test.True(
            typeof(UnitProgress).GetProperty(
                "LockedLevelTriggerSkillIdsTyped",
                BindingFlags.Instance | BindingFlags.NonPublic
            )?.PropertyType == typeof(IReadOnlyList<StringName>),
            "UnitProgress.locked_level_trigger_skill_ids 应继续通过 internal typed list 承载 runtime 业务态。"
        );

        var progress = new UnitProgress();
        progress.SetKnownKnowledgeIds(new[] { new StringName("lore") });
        progress.SetActiveCoreSkillIds(new[] { new StringName("slash") });
        progress.SetBlockedRelearnSkillIds(new[] { new StringName("old_focus") });
        progress.SetUnlockedCombatResourceIds(
            new[] { CombatResourceIds.ToStringName(CombatResourceIdKind.Hp), CombatResourceIds.ToStringName(CombatResourceIdKind.Stamina) }
        );
        progress.SetLockedLevelTriggerSkillIds(new[] { new StringName("vajra_body") });

        var projectedKnowledgeIds = progress.known_knowledge_ids;
        var projectedActiveCoreIds = progress.active_core_skill_ids;
        var projectedBlockedIds = progress.blocked_relearn_skill_ids;
        var projectedResourceIds = progress.unlocked_combat_resource_ids;
        var projectedLockedIds = progress.locked_level_trigger_skill_ids;

        projectedKnowledgeIds.Add("alchemy");
        projectedActiveCoreIds.Add("fireball");
        projectedBlockedIds.Add("legacy_skill");
        projectedResourceIds.Add("aura");
        projectedLockedIds.Add("new_lock");

        _test.Eq(progress.known_knowledge_ids.Count, 1, "UnitProgress.known_knowledge_ids public property 应保持边界投影。");
        _test.Eq(progress.active_core_skill_ids.Count, 1, "UnitProgress.active_core_skill_ids public property 应保持边界投影。");
        _test.Eq(progress.blocked_relearn_skill_ids.Count, 1, "UnitProgress.blocked_relearn_skill_ids public property 应保持边界投影。");
        _test.Eq(progress.unlocked_combat_resource_ids.Count, 2, "UnitProgress.unlocked_combat_resource_ids public property 应保持边界投影。");
        _test.Eq(progress.locked_level_trigger_skill_ids.Count, 1, "UnitProgress.locked_level_trigger_skill_ids public property 应保持边界投影。");
        _test.Eq(progress.KnownKnowledgeIdsTyped.Count, 1, "UnitProgress known-knowledge runtime 业务态应保持 typed list。");
        _test.Eq(progress.ActiveCoreSkillIdsTyped.Count, 1, "UnitProgress active-core runtime 业务态应保持 typed list。");
        _test.Eq(progress.BlockedRelearnSkillIdsTyped.Count, 1, "UnitProgress blocked-relearn runtime 业务态应保持 typed list。");
        _test.Eq(progress.UnlockedCombatResourceIdsTyped.Count, 2, "UnitProgress unlocked-resource runtime 业务态应保持 typed list。");
        _test.Eq(progress.LockedLevelTriggerSkillIdsTyped.Count, 1, "UnitProgress locked-trigger runtime 业务态应保持 typed list。");

    }

    private void TestUnitProgressMergedSkillSourceMapUsesTypedBackingDictionary()
    {
        _test.True(
            typeof(UnitProgress).GetProperty(
                "MergedSkillSourceMapTyped",
                BindingFlags.Instance | BindingFlags.NonPublic
            )?.PropertyType == typeof(IReadOnlyDictionary<StringName, List<StringName>>),
            "UnitProgress.merged_skill_source_map 应继续通过 internal typed dictionary 承载 runtime 业务态。"
        );

        var progress = new UnitProgress();
        progress.RememberMergeSources(
            new StringName("saint_blade_combo"),
            new Godot.Collections.Array<StringName> { "slash", "guard" }
        );

        GDictionary projected = progress.merged_skill_source_map;
        projected["saint_blade_combo"] = new Godot.Collections.Array<StringName> { "late_mutation" };

        _test.True(
            progress.MergedSkillSourceMapTyped.TryGetValue(
                new StringName("saint_blade_combo"),
                out List<StringName> sourceIds
            ) && sourceIds.Count == 2,
            "UnitProgress merged-skill-source runtime 业务态应保持 typed dictionary。"
        );
        _test.Eq(
            progress.GetMergedSourceSkillIdsTyped(new StringName("saint_blade_combo")).Count,
            2,
            "UnitProgress.merged_skill_source_map public Godot property 应保持边界投影。"
        );

    }

    private void TestUnitProgressAttributeGrowthUsesTypedBackingDictionary()
    {
        _test.True(
            typeof(UnitProgress).GetProperty(
                "AttributeGrowthProgressTyped",
                BindingFlags.Instance | BindingFlags.NonPublic
            )?.PropertyType == typeof(IReadOnlyDictionary<StringName, int>),
            "UnitProgress.attribute_growth_progress 应继续通过 internal typed dictionary 承载 runtime 业务态。"
        );

        var progress = new UnitProgress();
        progress.SetAttributeGrowthProgressAmount(new StringName("agility"), 90);

        GDictionary projected = progress.attribute_growth_progress;
        projected["agility"] = 5;

        _test.True(
            progress.TryGetAttributeGrowthProgressAmount(new StringName("agility"), out int amount)
                && amount == 90,
            "UnitProgress attribute-growth runtime 业务态应保持 typed dictionary。"
        );
        _test.Eq(
            progress.attribute_growth_progress["agility"].AsInt32(),
            90,
            "UnitProgress.attribute_growth_progress public Godot property 应保持边界投影。"
        );

    }

    private void TestUnitProgressAchievementProgressUsesTypedBackingDictionary()
    {
        _test.True(
            typeof(UnitProgress).GetProperty(
                "AchievementProgressTyped",
                BindingFlags.Instance | BindingFlags.NonPublic
            )?.PropertyType == typeof(IReadOnlyDictionary<StringName, AchievementProgressState>),
            "UnitProgress.achievement_progress 应继续通过 internal typed dictionary 承载 runtime 业务态。"
        );

        var progress = new UnitProgress();
        progress.SetAchievementProgressState(
            new AchievementProgressState
            {
                achievement_id = "battle_won_first",
                current_value = 1,
            }
        );

        GDictionary projected = progress.achievement_progress;
        projected["battle_won_first"] = new AchievementProgressState
        {
            achievement_id = "battle_won_first",
            current_value = 9,
        };

        _test.True(
            progress.AchievementProgressTyped.TryGetValue(
                new StringName("battle_won_first"),
                out AchievementProgressState stored
            ) && stored.current_value == 1,
            "UnitProgress achievement runtime 业务态应保持 typed dictionary。"
        );
        _test.Eq(
            progress.GetAchievementProgressState("battle_won_first")?.current_value ?? 0,
            1,
            "UnitProgress.achievement_progress public Godot property 应保持边界投影。"
        );

    }

    private void TestUnitProgressProfessionsUseTypedBackingDictionary()
    {
        _test.True(
            typeof(UnitProgress).GetProperty(
                "ProfessionsTyped",
                BindingFlags.Instance | BindingFlags.NonPublic
            )?.PropertyType == typeof(IReadOnlyDictionary<StringName, UnitProfessionProgress>),
            "UnitProgress.professions 应继续通过 internal typed dictionary 承载 runtime 业务态。"
        );

        var progress = new UnitProgress();
        progress.SetProfessionProgress(
            new UnitProfessionProgress
            {
                profession_id = "warrior",
                rank = 1,
                is_active = true,
            }
        );

        GDictionary projected = progress.professions;
        UnitProfessionProgress projectedProfession =
            projected["warrior"].AsGodotObject() as UnitProfessionProgress;
        projectedProfession.rank = 9;
        projected["warrior"] = projectedProfession;

        _test.True(
            progress.ProfessionsTyped.TryGetValue(
                new StringName("warrior"),
                out UnitProfessionProgress stored
            ) && stored.rank == 1,
            "UnitProgress profession runtime 业务态应保持 typed dictionary，并隔离 public Godot projection 的嵌套对象变更。"
        );
        _test.Eq(
            progress.GetProfessionProgress("warrior")?.rank ?? 0,
            1,
            "UnitProgress.professions public Godot property 应保持边界投影。"
        );

    }

    private void TestPendingProfessionChoiceUsesTypedBackingCollections()
    {
        _test.True(
            typeof(PendingProfessionChoice).GetProperty(
                "TriggerSkillIdsTyped",
                BindingFlags.Instance | BindingFlags.NonPublic
            )?.PropertyType == typeof(IReadOnlyList<StringName>),
            "PendingProfessionChoice.trigger_skill_ids 应继续通过 internal typed list 承载 runtime 业务态。"
        );
        _test.True(
            typeof(PendingProfessionChoice).GetProperty(
                "CandidateProfessionIdsTyped",
                BindingFlags.Instance | BindingFlags.NonPublic
            )?.PropertyType == typeof(IReadOnlyList<StringName>),
            "PendingProfessionChoice.candidate_profession_ids 应继续通过 internal typed list 承载 runtime 业务态。"
        );
        _test.True(
            typeof(PendingProfessionChoice).GetProperty(
                "TargetRankMapTyped",
                BindingFlags.Instance | BindingFlags.NonPublic
            )?.PropertyType == typeof(IReadOnlyDictionary<StringName, int>),
            "PendingProfessionChoice.target_rank_map 应继续通过 internal typed dictionary 承载 runtime 业务态。"
        );
        _test.True(
            typeof(PendingProfessionChoice).GetProperty(
                "QualifierSkillPoolIdsTyped",
                BindingFlags.Instance | BindingFlags.NonPublic
            )?.PropertyType == typeof(IReadOnlyList<StringName>),
            "PendingProfessionChoice.qualifier_skill_pool_ids 应继续通过 internal typed list 承载 runtime 业务态。"
        );
        _test.True(
            typeof(PendingProfessionChoice).GetProperty(
                "AssignableSkillCandidateIdsTyped",
                BindingFlags.Instance | BindingFlags.NonPublic
            )?.PropertyType == typeof(IReadOnlyList<StringName>),
            "PendingProfessionChoice.assignable_skill_candidate_ids 应继续通过 internal typed list 承载 runtime 业务态。"
        );

        var choice = new PendingProfessionChoice
        {
            required_qualifier_count = 1,
            required_assigned_core_count = 1,
        };
        choice.SetTriggerSkillIds(new[] { new StringName("trigger_slash") });
        choice.AddCandidateProfessionId("warrior");
        choice.SetTargetRank("warrior", 2);
        choice.AddQualifierSkillPoolId("bless");
        choice.AddAssignableSkillCandidateId("slash");

        var projectedTriggerIds = choice.trigger_skill_ids;
        var projectedCandidateIds = choice.candidate_profession_ids;
        var projectedTargetRanks = choice.target_rank_map;
        var projectedQualifierIds = choice.qualifier_skill_pool_ids;
        var projectedAssignableIds = choice.assignable_skill_candidate_ids;

        projectedTriggerIds.Add("trigger_guard");
        projectedCandidateIds.Add("mage");
        projectedTargetRanks["warrior"] = 9;
        projectedTargetRanks["cleric"] = 1;
        projectedQualifierIds.Add("shield");
        projectedAssignableIds.Add("fireball");

        _test.Eq(
            choice.trigger_skill_ids.Count,
            1,
            "PendingProfessionChoice.trigger_skill_ids public Godot property 应保持边界投影。"
        );
        _test.Eq(
            choice.candidate_profession_ids.Count,
            1,
            "PendingProfessionChoice.candidate_profession_ids public Godot property 应保持边界投影。"
        );
        _test.Eq(
            choice.target_rank_map.Count,
            1,
            "PendingProfessionChoice.target_rank_map public Godot property 应保持边界投影。"
        );
        _test.Eq(
            choice.qualifier_skill_pool_ids.Count,
            1,
            "PendingProfessionChoice.qualifier_skill_pool_ids public Godot property 应保持边界投影。"
        );
        _test.Eq(
            choice.assignable_skill_candidate_ids.Count,
            1,
            "PendingProfessionChoice.assignable_skill_candidate_ids public Godot property 应保持边界投影。"
        );
        _test.Eq(
            choice.TriggerSkillIdsTyped.Count,
            1,
            "PendingProfessionChoice trigger-skill runtime 业务态应保持 typed list。"
        );
        _test.Eq(
            choice.CandidateProfessionIdsTyped.Count,
            1,
            "PendingProfessionChoice candidate-profession runtime 业务态应保持 typed list。"
        );
        _test.Eq(
            choice.QualifierSkillPoolIdsTyped.Count,
            1,
            "PendingProfessionChoice qualifier-skill-pool runtime 业务态应保持 typed list。"
        );
        _test.Eq(
            choice.AssignableSkillCandidateIdsTyped.Count,
            1,
            "PendingProfessionChoice assignable-skill runtime 业务态应保持 typed list。"
        );
        _test.True(
            choice.TryGetTargetRank("warrior", out int targetRank) && targetRank == 2,
            "PendingProfessionChoice target-rank runtime 业务态应保持 typed dictionary。"
        );

    }

    private void TestBattlePreviewRandomChainCandidatesUseTypedBackingList()
    {
        _test.True(
            typeof(BattlePreview).GetProperty("RandomChainCandidateUnitIdsTyped", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.PropertyType == typeof(IReadOnlyList<StringName>),
            "BattlePreview random-chain candidate pool 应继续通过 internal typed list 承载 runtime 业务态。"
        );

        var preview = new BattlePreview();
        preview.SetRandomChainCandidateUnitIds(
            new[] { new StringName("enemy_a"), new StringName("enemy_b") }
        );

        Godot.Collections.Array<StringName> projected = preview.random_chain_candidate_unit_ids;
        projected.Add("enemy_c");

        _test.Eq(
            preview.random_chain_candidate_unit_ids.Count,
            2,
            "BattlePreview.random_chain_candidate_unit_ids public Godot property 应保持边界投影，不应泄漏可变内部数组引用。"
        );
        _test.Eq(
            preview.RandomChainCandidateUnitIdsTyped.Count,
            2,
            "BattlePreview random-chain candidate runtime 业务态应保持 typed list。"
        );
        _test.Eq(
            preview.RandomChainCandidateUnitIdsTyped[0],
            new StringName("enemy_a"),
            "BattlePreview random-chain candidate typed backing 应保留正式 StringName 值。"
        );
    }

    private void TestBattlePreviewTargetUnitIdsUseTypedBackingList()
    {
        _test.True(
            typeof(BattlePreview).GetProperty("TargetUnitIdsTyped", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.PropertyType == typeof(IReadOnlyList<StringName>),
            "BattlePreview target_unit_ids 应继续通过 internal typed list 承载 runtime 业务态。"
        );

        var preview = new BattlePreview();
        preview.SetTargetUnitIds(
            new[] { new StringName("enemy_a"), new StringName("enemy_b") }
        );

        Godot.Collections.Array<StringName> projected = preview.target_unit_ids;
        projected.Add("enemy_c");

        _test.Eq(
            preview.target_unit_ids.Count,
            2,
            "BattlePreview.target_unit_ids public Godot property 应保持边界投影，不应泄漏可变内部数组引用。"
        );
        _test.Eq(
            preview.TargetUnitIdsTyped.Count,
            2,
            "BattlePreview target-unit runtime 业务态应保持 typed list。"
        );
        _test.Eq(
            preview.TargetUnitIdsTyped[0],
            new StringName("enemy_a"),
            "BattlePreview target-unit typed backing 应保留正式 StringName 值。"
        );
    }

    private void TestBattleEventBatchChangedUnitIdsUseTypedBackingList()
    {
        _test.True(
            typeof(BattleEventBatch).GetProperty(
                "ChangedUnitIdsTyped",
                BindingFlags.Instance | BindingFlags.NonPublic
            )?.PropertyType == typeof(IReadOnlyList<StringName>),
            "BattleEventBatch.changed_unit_ids 应继续通过 internal typed list 承载 runtime 业务态。"
        );

        var batch = new BattleEventBatch();
        batch.AddChangedUnitId(new StringName("unit_a"));
        batch.AddChangedUnitId(new StringName("unit_b"));

        Godot.Collections.Array<StringName> projected = batch.changed_unit_ids;
        projected.Add(new StringName("unit_c"));

        _test.Eq(
            batch.changed_unit_ids.Count,
            2,
            "BattleEventBatch.changed_unit_ids public Godot property 应保持边界投影，不应泄漏可变内部数组引用。"
        );
        _test.Eq(
            batch.ChangedUnitIdsTyped.Count,
            2,
            "BattleEventBatch changed-unit runtime 业务态应保持 typed list。"
        );
        _test.Eq(
            batch.ChangedUnitIdsTyped[0],
            new StringName("unit_a"),
            "BattleEventBatch changed-unit typed backing 应保留正式 StringName 值。"
        );

    }

    private void TestBattleEventBatchChangedCoordsUseTypedBackingList()
    {
        _test.True(
            typeof(BattleEventBatch).GetProperty(
                "ChangedCoordsTyped",
                BindingFlags.Instance | BindingFlags.NonPublic
            )?.PropertyType == typeof(IReadOnlyList<Vector2I>),
            "BattleEventBatch.changed_coords 应继续通过 internal typed list 承载 runtime 业务态。"
        );

        var batch = new BattleEventBatch();
        batch.AddChangedCoord(new Vector2I(1, 2));
        batch.AddChangedCoord(new Vector2I(3, 4));

        Godot.Collections.Array<Vector2I> projected = batch.changed_coords;
        projected.Add(new Vector2I(5, 6));

        _test.Eq(
            batch.changed_coords.Count,
            2,
            "BattleEventBatch.changed_coords public Godot property 应保持边界投影，不应泄漏可变内部数组引用。"
        );
        _test.Eq(
            batch.ChangedCoordsTyped.Count,
            2,
            "BattleEventBatch changed-coords runtime 业务态应保持 typed list。"
        );
        _test.Eq(
            batch.ChangedCoordsTyped[0],
            new Vector2I(1, 2),
            "BattleEventBatch changed-coords typed backing 应保留正式 Vector2I 值。"
        );

    }

    private void TestBattleEventBatchReportEntriesUseTypedBackingList()
    {
        _test.True(
            typeof(BattleEventBatch).GetProperty(
                "ReportEntriesTyped",
                BindingFlags.Instance | BindingFlags.NonPublic
            )?.PropertyType == typeof(IReadOnlyList<GDictionary>),
            "BattleEventBatch.report_entries 应继续通过 internal typed list 承载 runtime 业务态。"
        );

        var batch = new BattleEventBatch();
        var firstEntry = new GDictionary { ["type"] = "damage", ["text"] = "hit a" };
        batch.AddReportEntry(firstEntry);

        GArray projected = batch.report_entries;
        projected.Add(new GDictionary { ["type"] = "damage", ["text"] = "hit b" });
        firstEntry["text"] = "mutated";

        _test.Eq(
            batch.report_entries.Count,
            1,
            "BattleEventBatch.report_entries public Godot property 应保持边界投影，不应泄漏可变内部数组引用。"
        );
        _test.Eq(
            batch.ReportEntriesTyped.Count,
            1,
            "BattleEventBatch report-entry runtime 业务态应保持 typed list。"
        );
        _test.Eq(
            batch.ReportEntriesTyped[0]["text"].ToString(),
            "hit a",
            "BattleEventBatch report-entry typed backing 应 deep copy 正式 payload。"
        );

    }

    private void TestBattleEventBatchProgressionDeltasUseTypedBackingList()
    {
        _test.True(
            typeof(BattleEventBatch).GetProperty(
                "ProgressionDeltasTyped",
                BindingFlags.Instance | BindingFlags.NonPublic
            )?.PropertyType == typeof(IReadOnlyList<CharacterProgressionDelta>),
            "BattleEventBatch.progression_deltas 应继续通过 internal typed list 承载 runtime 业务态。"
        );

        var batch = new BattleEventBatch();
        var firstDelta = new CharacterProgressionDelta { member_id = new StringName("hero_a") };
        var secondDelta = new CharacterProgressionDelta { member_id = new StringName("hero_b") };
        batch.AddProgressionDelta(firstDelta);
        batch.AddProgressionDelta(secondDelta);

        GArray projected = batch.progression_deltas;
        projected.Add(new CharacterProgressionDelta { member_id = new StringName("hero_c") });

        _test.Eq(
            batch.progression_deltas.Count,
            2,
            "BattleEventBatch.progression_deltas public Godot property 应保持边界投影，不应泄漏可变内部数组引用。"
        );
        _test.Eq(
            batch.ProgressionDeltasTyped.Count,
            2,
            "BattleEventBatch progression-delta runtime 业务态应保持 typed list。"
        );
        _test.True(
            ReferenceEquals(batch.ProgressionDeltasTyped[0], firstDelta),
            "BattleEventBatch progression-delta typed backing 应保留正式 delta 对象引用。"
        );

    }

    private void TestBattlePreviewTargetCoordsUseTypedBackingList()
    {
        _test.True(
            typeof(BattlePreview).GetProperty("TargetCoordsTyped", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.PropertyType == typeof(IReadOnlyList<Vector2I>),
            "BattlePreview target_coords 应继续通过 internal typed list 承载 runtime 业务态。"
        );

        var preview = new BattlePreview();
        preview.SetTargetCoords(new[] { new Vector2I(2, 3), new Vector2I(2, 4) });

        Godot.Collections.Array<Vector2I> projected = preview.target_coords;
        projected.Add(new Vector2I(9, 9));

        _test.Eq(
            preview.target_coords.Count,
            2,
            "BattlePreview.target_coords public Godot property 应保持边界投影，不应泄漏可变内部数组引用。"
        );
        _test.Eq(
            preview.TargetCoordsTyped.Count,
            2,
            "BattlePreview target-coord runtime 业务态应保持 typed list。"
        );
        _test.Eq(
            preview.TargetCoordsTyped[0],
            new Vector2I(2, 3),
            "BattlePreview target-coord typed backing 应保留正式 Vector2I 值。"
        );
    }

    private void TestBattlePreviewDamagePreviewUsesTypedBackingPayload()
    {
        _test.True(
            typeof(BattlePreview).GetProperty(
                "DamagePreviewTyped",
                BindingFlags.Instance | BindingFlags.NonPublic
            )?.PropertyType == typeof(BattleDamagePreviewRangeService.SkillDamagePreview?),
            "BattlePreview.damage_preview 应继续通过 internal typed damage preview 承载 runtime 业务态。"
        );

        var preview = new BattlePreview();
        preview.SetDamagePreview(
            new BattleDamagePreviewRangeService.SkillDamagePreview(
                true,
                2,
                10,
                Array.Empty<BattleDamagePreviewRangeService.DamageEffectRange>()
            )
        );

        Godot.Collections.Dictionary projected = preview.damage_preview;
        projected["summary_text"] = "伤害 999";

        _test.Eq(
            preview.damage_preview.GetValueOrDefault("summary_text", "").AsString(),
            "伤害 2-10",
            "BattlePreview.damage_preview public Godot property 应保持边界投影，不应泄漏可变内部字典引用。"
        );
        _test.True(
            preview.DamagePreviewTyped.HasValue
                && preview.DamagePreviewTyped.Value.MinDamage == 2
                && preview.DamagePreviewTyped.Value.MaxDamage == 10,
            "BattlePreview damage preview runtime 业务态应保持 typed payload。"
        );
    }

    private void TestBattlePreviewDamagePreviewSetterDecodesProjectedPayload()
    {
        var preview = new BattlePreview();
        preview.damage_preview = new GDictionary
        {
            ["has_damage"] = true,
            ["min_damage"] = 4,
            ["max_damage"] = 9,
        };

        _test.True(
            preview.DamagePreviewTyped.HasValue,
            "BattlePreview.damage_preview setter 应继续解码成 internal typed payload。"
        );
        if (preview.DamagePreviewTyped.HasValue)
        {
            _test.True(preview.DamagePreviewTyped.Value.HasDamage, "BattlePreview damage preview setter 应保留 has_damage。");
            _test.Eq(preview.DamagePreviewTyped.Value.MinDamage, 4, "BattlePreview damage preview setter 应保留 min_damage。");
            _test.Eq(preview.DamagePreviewTyped.Value.MaxDamage, 9, "BattlePreview damage preview setter 应保留 max_damage。");
        }
    }

    private void TestBattlePreviewLogLinesUseTypedBackingList()
    {
        _test.True(
            typeof(BattlePreview).GetProperty(
                "LogLinesTyped",
                BindingFlags.Instance | BindingFlags.NonPublic
            )?.PropertyType == typeof(IReadOnlyList<string>),
            "BattlePreview.log_lines 应继续通过 internal typed list 承载 runtime 业务态。"
        );

        var preview = new BattlePreview();
        preview.AddLogLine("第一条");
        preview.AddLogLine("第二条");

        Godot.Collections.Array projected = preview.log_lines;
        projected.Add("第三条");

        _test.Eq(
            preview.log_lines.Count,
            2,
            "BattlePreview.log_lines public Godot property 应保持边界投影，不应泄漏可变内部数组引用。"
        );
        _test.Eq(
            preview.LogLinesTyped.Count,
            2,
            "BattlePreview log-lines runtime 业务态应保持 typed list。"
        );
        _test.Eq(
            preview.LogLinesTyped[0],
            "第一条",
            "BattlePreview log-lines typed backing 应保留正式 string 值。"
        );
    }

    private void TestBattleEventBatchLogLinesUseTypedBackingList()
    {
        _test.True(
            typeof(BattleEventBatch).GetProperty(
                "LogLinesTyped",
                BindingFlags.Instance | BindingFlags.NonPublic
            )?.PropertyType == typeof(IReadOnlyList<string>),
            "BattleEventBatch.log_lines 应继续通过 internal typed list 承载 runtime 业务态。"
        );

        var batch = new BattleEventBatch();
        batch.AddLogLine("第一条");
        batch.AddLogLine("第二条");

        Godot.Collections.Array<string> projected = batch.log_lines;
        projected.Add("第三条");

        _test.Eq(
            batch.log_lines.Count,
            2,
            "BattleEventBatch.log_lines public Godot property 应保持边界投影，不应泄漏可变内部数组引用。"
        );
        _test.Eq(
            batch.LogLinesTyped.Count,
            2,
            "BattleEventBatch log-lines runtime 业务态应保持 typed list。"
        );
        _test.Eq(
            batch.LogLinesTyped[0],
            "第一条",
            "BattleEventBatch log-lines typed backing 应保留正式 string 值。"
        );

    }

    private void TestBattleRuntimeModuleAiTurnTracesUseTypedBackingProjection()
    {
        FieldInfo traceField = typeof(BattleRuntimeModule).GetField(
            "_ai_turn_traces",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        _test.True(
            typeof(BattleRuntimeModule).GetField(
                "_ai_turn_traces",
                BindingFlags.Instance | BindingFlags.Public
            ) == null
                && traceField?.FieldType == typeof(List<BattleAiTurnTraceProjection>),
            "BattleRuntimeModule AI turn traces 应继续以 private typed C# list 承载 owner 业务态。"
        );

        var runtime = new BattleRuntimeModule();
        var traceEntries = traceField?.GetValue(runtime) as List<BattleAiTurnTraceProjection>;
        _test.True(traceEntries != null, "BattleRuntimeModule private typed trace store should be readable in regression.");
        traceEntries?.Add(
            new BattleAiTurnTraceProjection
            {
                ActionId = "typed_wait",
                ExecutionResult = new BattleAiTraceExecutionResultProjection
                {
                    LogLines = new List<string> { "line_a" },
                },
            }
        );

        Godot.Collections.Array<GDictionary> projected = runtime.GetAiTurnTraces();
        _test.Eq(projected.Count, 1, "BattleRuntimeModule should project typed AI traces to one Godot entry.");
        GDictionary projectedFirst = projected[0];
        projectedFirst["action_id"] = "mutated";
        projected.Add(new GDictionary { ["action_id"] = "injected" });

        Godot.Collections.Array<GDictionary> projectedAgain = runtime.GetAiTurnTraces();
        _test.Eq(projectedAgain.Count, 1, "BattleRuntimeModule.GetAiTurnTraces() 应保持边界投影，不应泄漏可变内部数组引用。");
        _test.Eq(
            projectedAgain[0]["action_id"].AsString(),
            "typed_wait",
            "BattleRuntimeModule AI trace public Godot projection 应隔离调用方 mutation。"
        );

        _test.True(
            typeof(BattleRuntimeModule).GetField(
                    "_ai_turn_traces",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )?.FieldType == typeof(List<BattleAiTurnTraceProjection>)
                && typeof(BattleAiTurnTraceProjection).GetProperty(
                    "ScoreInput",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )?.PropertyType == typeof(BattleAiScoreInput)
                && typeof(BattleRuntimeModule).GetMethod(
                    "BuildAiTraceExecutionResultTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )?.ReturnType == typeof(BattleAiTraceExecutionResultProjection)
                && typeof(BattleRuntimeModule).GetMethod(
                    "BuildAiTraceUnitSnapshotMapTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )?.ReturnType == typeof(Dictionary<StringName, BattleAiTraceUnitSnapshotProjection>),
            "BattleRuntimeModule AI trace enrichment 不应继续把 Array<GDictionary> / public snake_case helper 当 owner 业务态。"
        );
    }

    private void TestActionTraceProjectsStableDictionaryShape()
    {
        var command = new AiCommandSummary(
            "skill",
            "caster",
            "bolt",
            "",
            "target",
            new[] { new StringName("target") },
            new Vector2I(2, 2),
            new[] { new Vector2I(2, 2) }
        );
        var candidate = new AiCandidateSummary(
            "bolt@target",
            command,
            42,
            new Dictionary<string, object>
            {
                ["score_bucket_id"] = "offense",
                ["target_ids"] = new[] { new StringName("target") },
            }
        );
        var trace = new AiActionTrace(
            "trace_1",
            "cast_bolt",
            "offense",
            new Dictionary<string, object>
            {
                ["generated"] = true,
                ["position"] = new Vector2I(1, 2),
            }
        )
        {
            EvaluationCount = 3,
            CandidateCount = 1,
            BestReasonText = "best",
            BestCommand = command,
            Chosen = true,
            ChosenReasonText = "selected",
            GateRejected = true,
            GateRejectionReason = "unsafe",
        };
        trace.BlockReasons["blocked"] = 2;
        trace.TopCandidates.Add(candidate);
        trace.CandidateTraceCounters["evaluated"] = 3;

        _test.True(!trace.IsEmpty(), "Trace with trace_id should not be empty.");
        Godot.Collections.Dictionary payload = trace.ToDictionary();
        _test.Eq(payload["trace_id"].AsString(), "trace_1", "Trace projection should include trace_id.");
        _test.Eq(payload["action_id"].AsString(), "cast_bolt", "Trace projection should include action_id.");
        _test.Eq(payload["evaluation_count"].AsInt32(), 3, "Trace projection should include evaluation_count.");
        _test.Eq(payload["candidate_count"].AsInt32(), 1, "Trace projection should include candidate_count.");
        _test.Eq(
            payload["top_candidates"].AsGodotArray().Count,
            1,
            "Trace projection should include candidate summaries."
        );
        _test.Eq(
            payload["gate_rejection_reason"].AsString(),
            "unsafe",
            "Trace projection should include gate rejection reason."
        );
    }

    private void TestEnemyAiActionHelperUsesTypedTraceState()
    {
        Type helperType = typeof(EnemyAiActionHelper);
        _test.True(helperType.IsAbstract && helperType.IsSealed, "EnemyAiActionHelper should be a static helper.");
        _test.Eq(
            helperType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).Length,
            0,
            "EnemyAiActionHelper should not expose public GDScript-style helper API."
        );
        _test.True(
            helperType.GetMethod("BeginActionTrace", BindingFlags.NonPublic | BindingFlags.Static)
                    ?.GetParameters()[3]
                    .ParameterType
                == typeof(IReadOnlyDictionary<string, object>)
                && helperType.GetMethod(
                        "BuildCandidateSummary",
                        BindingFlags.NonPublic | BindingFlags.Static
                    )?.GetParameters()[3]
                    .ParameterType
                    == typeof(IReadOnlyDictionary<string, object>),
            "EnemyAiActionHelper trace/candidate metadata surface 应继续直接接收 typed dictionary。"
        );
        _test.True(
            typeof(EnemyAiAction)
                    .GetMethod("_begin_action_trace", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetParameters()[1]
                    .ParameterType == typeof(IReadOnlyDictionary<string, object>)
                && typeof(EnemyAiAction)
                    .GetMethod("_build_candidate_summary", BindingFlags.Static | BindingFlags.NonPublic)
                    ?.GetParameters()[3]
                    .ParameterType == typeof(IReadOnlyDictionary<string, object>),
            "EnemyAiAction trace/candidate wrapper 不应继续暴露 GDictionary metadata surface。"
        );

        var context = new BattleAiContext { trace_enabled = true };
        AiActionTrace trace = EnemyAiActionHelper.BeginActionTrace(
            "wait_action",
            "idle",
            context,
            new Dictionary<string, object>(StringComparer.Ordinal) { ["generated"] = true }
        );
        EnemyAiActionHelper.TraceCountIncrement(trace, "evaluation_count", 2);
        EnemyAiActionHelper.TraceAddBlockReason(trace, "blocked_by_test");

        var command = new BattleCommand
        {
            command_type = BattleTypedNames.ToStringName(BattleCommandKind.Wait),
            unit_id = "actor",
        };
        var scoreInput = new BattleAiScoreInput
        {
            total_score = 12,
            score_bucket_id = "idle",
        };
        AiCandidateSummary candidate = EnemyAiActionHelper.BuildCandidateSummary(
            "wait",
            command,
            scoreInput,
            new Dictionary<string, object>(StringComparer.Ordinal) { ["source"] = "test" }
        );
        EnemyAiActionHelper.TraceOfferCandidate(trace, candidate);
        BattleAiDecision decision = EnemyAiActionHelper.CreateScoredDecision(
            "wait_action",
            "idle",
            command,
            scoreInput,
            "selected"
        );

        StringName traceId = EnemyAiActionHelper.FinalizeActionTrace(context, trace, decision);

        _test.Eq(traceId.ToString(), "wait_action_1", "Helper should allocate a typed trace id.");
        _test.Eq(decision.action_trace_id.ToString(), "wait_action_1", "Finalization should write the decision trace id.");
        IReadOnlyList<AiActionTrace> actionTraces = context.GetActionTracesTyped();
        _test.Eq(actionTraces.Count, 1, "Context should expose one typed trace.");

        AiActionTrace payload = actionTraces[0];
        _test.Eq(payload.EvaluationCount, 2, "Typed trace should keep evaluation count.");
        _test.Eq(payload.BlockedCount, 1, "Typed trace should keep block count.");
        _test.Eq(payload.CandidateCount, 1, "Typed trace should keep candidate count.");
        _test.Eq(
            ReadBool(payload.Metadata, "generated"),
            true,
            "Typed trace should preserve metadata."
        );
        _test.Eq(
            ReadInt(payload.BestScoreInput, "total_score", 0),
            12,
            "Typed trace should preserve best score input."
        );
    }

    private void TestWaitActionActiveRestUsesTypedProfile()
    {
        Type profileType = typeof(WaitAction).GetNestedType(
            "ActiveRestProfile",
            BindingFlags.NonPublic
        );
        _test.True(profileType != null && profileType.IsSealed, "WaitAction active-rest profile should be a private sealed C# type.");
        MethodInfo profileFactory = typeof(WaitAction).GetMethod(
            "_build_active_rest_profile",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        _test.True(
            profileFactory != null && profileFactory.ReturnType == profileType,
            "WaitAction should build a typed active-rest profile instead of a Godot Dictionary."
        );
        if (profileType != null)
        {
            foreach (FieldInfo field in profileType.GetFields(
                         BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly
                     ))
            {
                _test.True(
                    !IsForbiddenPublicApiType(field.FieldType),
                    $"WaitAction active-rest profile field {field.Name} should not use Godot Dictionary/Array/Variant."
                );
            }
        }

        var unit = new BattleUnitState
        {
            unit_id = "wait_actor",
            display_name = "Wait Actor",
            faction_id = "hostile",
            action_threshold = 5,
            current_stamina = 1,
            stamina_recovery_progress = 0,
        };
        unit.attribute_snapshot = new AttributeSnapshot();
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.StaminaMax), 10);
        unit.attribute_snapshot.SetValue(UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Constitution), 10);

        var basicAttack = new SkillDef
        {
            skill_id = "basic_attack",
            combat_profile = new CombatSkillDef
            {
                skill_id = "basic_attack",
                stamina_cost = 4,
                target_mode = "unit",
            },
        };

        var context = new BattleAiContext
        {
            trace_enabled = true,
            unit_state = unit,
            action_score_input_callback = (
                _,
                actionKind,
                actionLabel,
                scoreBucketId,
                command,
                preview,
                metadata
            ) =>
                new BattleAiScoreInput
                {
                    action_kind = actionKind,
                    action_label = actionLabel,
                    score_bucket_id = scoreBucketId,
                    command = command,
                    runtime_action_metadata =
                        BattleAiScoreRuntimeMetadata.FromMetadata(metadata),
                    total_score = ReadInt(metadata, "action_base_score", 0),
                },
        };
        context.SetSkillDefs(
            new Dictionary<StringName, SkillDef>
            {
                [new StringName("basic_attack")] = basicAttack,
            }
        );

        var action = new WaitAction
        {
            action_id = "wait_active_rest",
            active_rest_action_base_score = 17,
        };

        BattleAiDecision decision = action.Decide(context);
        _test.True(decision != null, "WaitAction should return an active-rest decision.");
        _test.True(
            decision?.score_input?.runtime_action_metadata?.HasActiveRest == true
                && decision.score_input.runtime_action_metadata.active_rest,
            "Active-rest score metadata should still project active_rest at the score boundary."
        );
        _test.Eq(
            decision?.score_input?.runtime_action_metadata?.HasActionBaseScore == true
                ? decision.score_input.runtime_action_metadata.action_base_score
                : -1,
            17,
            "Active-rest score metadata should preserve action_base_score."
        );
        IReadOnlyList<AiActionTrace> actionTraces = context.GetActionTracesTyped();
        _test.Eq(actionTraces.Count, 1, "WaitAction should emit one action trace.");
        if (actionTraces.Count > 0)
        {
            AiActionTrace payload = actionTraces[0];
            IReadOnlyDictionary<string, object> metadata = payload.Metadata;
            _test.True(
                ReadBool(metadata, "active_rest"),
                "WaitAction trace metadata should keep active_rest."
            );
            _test.Eq(
                ReadInt(metadata, "projected_rest_stamina", -1),
                5,
                "WaitAction trace metadata should preserve projected rest stamina."
            );
            _test.Eq(
                ReadInt(payload.BestScoreInput, "total_score", -1),
                17,
                "WaitAction candidate summary should keep active-rest score."
            );
        }
    }

    private void AssertPlainDto(Type type, string typeName)
    {
        _test.True(type.IsSealed, $"{typeName} should be sealed.");
        _test.Eq(
            type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Length,
            0,
            $"{typeName} should expose typed properties/lists, not public mutable fields."
        );
    }

    private void AssertPublicApiDoesNotExposeGodotTypes(Type type)
    {
        foreach (MethodInfo method in type.GetMethods(
                     BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly
                 ))
        {
            _test.True(
                !IsForbiddenPublicApiType(method.ReturnType),
                $"{type.Name}.{method.Name} should not return Godot Dictionary/Array/Variant."
            );
            foreach (ParameterInfo parameter in method.GetParameters())
            {
                _test.True(
                    !IsForbiddenPublicApiType(parameter.ParameterType),
                    $"{type.Name}.{method.Name}({parameter.Name}) should not accept Godot Dictionary/Array/Variant."
                );
            }
        }
    }

    private void AssertPublicPropertiesDoNotExposeGodotTypes(Type type)
    {
        foreach (PropertyInfo property in type.GetProperties(
                     BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly
                 ))
        {
            _test.True(
                !IsForbiddenPublicApiType(property.PropertyType),
                $"{type.Name}.{property.Name} should not expose Godot Dictionary/Array/Variant."
            );
        }
    }

    private static bool IsForbiddenPublicApiType(Type type)
    {
        if (type == typeof(Variant))
        {
            return true;
        }
        string typeName = type.FullName ?? "";
        return typeName.StartsWith("Godot.Collections.Dictionary", StringComparison.Ordinal)
            || typeName.StartsWith("Godot.Collections.Array", StringComparison.Ordinal);
    }

    private static bool ReadBool(
        IReadOnlyDictionary<string, object> dictionary,
        string key
    )
    {
        if (dictionary == null || string.IsNullOrEmpty(key) || !dictionary.TryGetValue(key, out object value))
        {
            return false;
        }
        return value is bool boolValue && boolValue;
    }

    private static int ReadInt(
        IReadOnlyDictionary<string, object> dictionary,
        string key,
        int fallback
    )
    {
        if (dictionary == null || string.IsNullOrEmpty(key) || !dictionary.TryGetValue(key, out object value))
        {
            return fallback;
        }
        return value switch
        {
            int intValue => intValue,
            long longValue => (int)longValue,
            float floatValue => (int)floatValue,
            double doubleValue => (int)doubleValue,
            _ => fallback,
        };
    }

}
