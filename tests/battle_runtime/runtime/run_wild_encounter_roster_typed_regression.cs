using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_wild_encounter_roster_typed_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestTypedStageSelectionUsesNearestDeclaredStage();
        TestSchemaValidationUsesTypedTemplateIdBoundary();
        TestEncounterRosterBuilderBuildsMixedMistHollowUnits();
        TestEncounterRosterBuilderBuildsOfficialWolfStageTwoUnits();
        TestEncounterRosterBuilderProjectsActorIdWithoutReplacingUnitId();
        TestEncounterRosterBuilderBuildsBattleOnlyScenarioActor();
        RequestTestExit(_test.Finish("Wild encounter roster typed regression"));
    }

    private void TestEncounterRosterBuilderBuildsBattleOnlyScenarioActor()
    {
        using GameSession gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        using EncounterRosterBuilder builder = new();
        builder.Setup(
            gameSession.GetBattleEncounterDefinitions(),
            gameSession.GetEncounterRosterDefinitions(),
            gameSession.GetEnemyTemplateDefinitions()
        );
        EncounterAnchorData encounterAnchor = new()
        {
            entity_id = "escort_scenario_actor_anchor",
            display_name = "护送内容探针",
            world_coord = new Vector2I(8, 8),
            faction_id = "hostile",
            region_tag = "south_wilds",
            encounter_profile_id = "mist_hollow_escort",
            growth_stage = 0,
        };

        IReadOnlyList<BattleScenarioActorSpawnRequest> requests =
            builder.BuildScenarioActorUnitsFromDefinitions(
                encounterAnchor,
                gameSession.GetContentCatalogTyped().GetSkillDefinitionsTyped(),
                gameSession.GetEnemyTemplateDefinitions(),
                gameSession.GetEnemyAiBrainDefinitions(),
                gameSession.GetItemDefsTyped()
            );

        _test.Eq(requests.Count, 1, "正式护送遭遇应构建一个场景 NPC。");
        if (requests.Count == 0)
            return;
        BattleScenarioActorSpawnRequest request = requests[0];
        _test.Eq(
            request.Unit.encounter_actor_id.ToString(),
            "refugee_guide",
            "场景 NPC 应保留稳定 actor_id。"
        );
        _test.Eq(
            request.Unit.source_member_id.ToString(),
            "",
            "场景 NPC 不得伪装为持久队员。"
        );
        _test.Eq(
            request.Unit.faction_id.ToString(),
            "player",
            "护送 NPC 应加入玩家友方阵营。"
        );
        _test.Eq(
            request.Unit.ControlModeKind,
            BattleUnitControlMode.Ai,
            "护送 NPC 应由目标 AI 自动控制。"
        );
        _test.Eq(
            request.SpawnZoneId.ToString(),
            "west_entry",
            "护送 NPC 应携带正式入口区。"
        );
        _test.Eq(
            request.SpawnEdge,
            BattleMapEdge.Left,
            "护送 NPC 入口应解析为左侧边缘。"
        );
    }

    private void TestTypedStageSelectionUsesNearestDeclaredStage()
    {
        WildEncounterRosterDefinition roster = new(
            "typed_stage_roster",
            "Typed Stage Roster",
            0,
            1,
            new[]
            {
                BuildDefinitionStage(
                0,
                    new WildEncounterRosterUnitEntryDefinition(
                        "wolf",
                        1,
                        "荒狼前锋",
                        "pack_leader"
                    )
                ),
                BuildDefinitionStage(
                2,
                    new WildEncounterRosterUnitEntryDefinition("wolf_shaman", 2, "织咒者")
                ),
            }
        );

        IReadOnlyList<WildEncounterRosterUnitEntryDefinition> stageOneEntries =
            roster.GetStageUnitEntries(1);
        _test.Eq(stageOneEntries.Count, 1, "growth_stage=1 时应命中最近的已声明 stage 0。");
        if (stageOneEntries.Count > 0)
        {
            _test.Eq(stageOneEntries[0].TemplateId.ToString(), "wolf", "stage 0 template_id 应被 typed 读取。");
            _test.Eq(stageOneEntries[0].Count, 1, "stage 0 count 应被 typed 读取。");
            _test.Eq(stageOneEntries[0].DisplayName, "荒狼前锋", "stage 0 display_name 应被 typed 读取。");
            _test.Eq(
                stageOneEntries[0].ActorId.ToString(),
                "pack_leader",
                "stage 0 actor_id 应被 typed 读取。"
            );
        }

        IReadOnlyList<WildEncounterRosterUnitEntryDefinition> stageThreeEntries =
            roster.GetStageUnitEntries(3);
        _test.Eq(stageThreeEntries.Count, 1, "growth_stage=3 时应命中最近的已声明 stage 2。");
        if (stageThreeEntries.Count > 0)
        {
            _test.Eq(stageThreeEntries[0].TemplateId.ToString(), "wolf_shaman", "stage 2 template_id 应被 typed 读取。");
            _test.Eq(stageThreeEntries[0].Count, 2, "stage 2 count 应被 typed 读取。");
            _test.Eq(stageThreeEntries[0].DisplayName, "织咒者", "stage 2 display_name 应被 typed 读取。");
            _test.Eq(
                stageThreeEntries[0].ActorId?.ToString() ?? "<null>",
                "",
                "省略 actor_id 的 managed roster definition 应规范化为空 ID，而不是 null。"
            );
        }
    }

    private void TestSchemaValidationUsesTypedTemplateIdBoundary()
    {
        using WildEncounterRosterUnitEntryDef actorEntry = new()
        {
            template_id = "wolf",
            actor_id = "pack_leader",
            count = 1,
        };
        using WildEncounterRosterDef roster = new()
        {
            profile_id = "schema_roster",
            display_name = "Schema Roster",
            initial_stage = 0,
        };
        roster.stages.Add(
            BuildStage(
                0,
                actorEntry,
                new WildEncounterRosterUnitEntryDef
                {
                    template_id = "wolf",
                    count = 2,
                }
            )
        );
        roster.stages.Add(
            BuildStage(
                1,
                new WildEncounterRosterUnitEntryDef
                {
                    template_id = "wolf",
                    actor_id = "pack_leader",
                    count = 1,
                }
            )
        );

        var knownTemplateIds = new HashSet<StringName> { "wolf" };
        GStringArray typedErrors = roster.ValidateSchemaTyped(knownTemplateIds);
        _test.Eq(typedErrors.Count, 0, $"typed ValidateSchemaTyped() 应接受正式 template id set。 errors={FormatErrors(typedErrors)}");
        WildEncounterRosterDefinition definition = roster.ToDefinition();
        _test.Eq(
            definition.GetStageUnitEntries(0)[0].ActorId.ToString(),
            "pack_leader",
            "authoring actor_id 应进入不可变 roster definition。"
        );

        actorEntry.count = 2;
        GStringArray repeatedActorErrors = roster.ValidateSchemaTyped(knownTemplateIds);
        _test.True(
            ContainsError(repeatedActorErrors, "actor_id pack_leader requires count == 1"),
            $"非空 actor_id 不应允许 count != 1。 errors={FormatErrors(repeatedActorErrors)}"
        );
        actorEntry.count = 1;

        roster.stages[0].unit_entries.Add(
            new WildEncounterRosterUnitEntryDef
            {
                template_id = "wolf",
                actor_id = "pack_leader",
                count = 1,
            }
        );
        GStringArray duplicateActorErrors = roster.ValidateSchemaTyped(knownTemplateIds);
        _test.True(
            ContainsError(duplicateActorErrors, "duplicate actor_id pack_leader"),
            $"同一 stage 不应声明重复 actor_id。 errors={FormatErrors(duplicateActorErrors)}"
        );

        GStringArray missingTemplateErrors = roster.ValidateSchemaTyped(
            new HashSet<StringName>()
        );
        _test.True(
            missingTemplateErrors.Count > 0,
            $"typed ValidateSchemaTyped() 应直接报告缺失 template。 errors={FormatErrors(missingTemplateErrors)}"
        );
    }

    private void TestEncounterRosterBuilderProjectsActorIdWithoutReplacingUnitId()
    {
        using EnemyTemplateDef actorTemplateResource = new()
        {
            template_id = "actor_projection_leader",
            display_name = "投影首领",
            enemy_count = 1,
            body_size = BattleUnitState.BodySizeMedium,
            action_threshold = BattleUnitState.DefaultActionThreshold,
            cognition_kind = "sapient",
            target_rank = "boss",
        };
        using EnemyTemplateDef guardTemplateResource = new()
        {
            template_id = "actor_projection_guard",
            display_name = "投影护卫",
            enemy_count = 1,
            body_size = BattleUnitState.BodySizeMedium,
            action_threshold = BattleUnitState.DefaultActionThreshold,
            cognition_kind = "sapient",
        };
        var itemDefinitions = new Dictionary<StringName, ItemDefinition>();
        var enemyTemplates = new Dictionary<StringName, EnemyTemplateDefinition>
        {
            [actorTemplateResource.template_id] =
                actorTemplateResource.ToDefinition(itemDefinitions),
            [guardTemplateResource.template_id] =
                guardTemplateResource.ToDefinition(itemDefinitions),
        };
        StringName encounterId = "actor_projection_encounter";
        StringName rosterId = "actor_projection_roster";
        WildEncounterRosterDefinition roster = new(
            rosterId,
            "Actor Projection Roster",
            0,
            1,
            new[]
            {
                BuildDefinitionStage(
                    0,
                    new WildEncounterRosterUnitEntryDefinition(
                        actorTemplateResource.template_id,
                        1,
                        "投影首领",
                        "pack_leader"
                    ),
                    new WildEncounterRosterUnitEntryDefinition(
                        guardTemplateResource.template_id,
                        2,
                        "投影护卫"
                    )
                ),
            }
        );
        BattleEncounterDefinition encounter = new(
            encounterId,
            "Actor Projection Encounter",
            rosterId,
            BattleEliminationObjectiveDefinition.Instance,
            new BattleEncounterWorldResolutionDefinition(
                BattleWorldResolutionMode.Clear,
                BattleWorldResolutionMode.Preserve,
                BattleWorldResolutionMode.Preserve,
                0
            )
        );
        using EncounterRosterBuilder builder = new();
        builder.Setup(
            new Dictionary<StringName, BattleEncounterDefinition>
            {
                [encounterId] = encounter,
            },
            new Dictionary<StringName, WildEncounterRosterDefinition>
            {
                [rosterId] = roster,
            },
            enemyTemplates
        );

        EncounterAnchorData encounterAnchor = new()
        {
            entity_id = "actor_projection_anchor",
            display_name = "首领遭遇",
            world_coord = new Vector2I(8, 8),
            faction_id = "hostile",
            region_tag = "south_wilds",
            vision_range = 2,
            encounter_profile_id = encounterId,
            growth_stage = 0,
        };
        using GodotProjectionLease<GArray> enemyUnitsLease = builder.BuildEnemyUnitsLease(
            encounterAnchor,
            new Dictionary<StringName, SkillDefinition>(),
            enemyTemplates,
            new Dictionary<StringName, EnemyAiBrainDefinition>(),
            itemDefinitions
        );
        GArray enemyUnits = enemyUnitsLease.Value;

        _test.Eq(enemyUnits.Count, 3, "actor projection roster 应构建一个首领和两个护卫。");
        BattleUnitState actor = FindUnitWithActorId(enemyUnits, "pack_leader");
        _test.True(actor != null, "具名 roster entry 应投影 encounter_actor_id。");
        _test.Eq(
            actor?.unit_id.ToString() ?? "",
            "actor_projection_anchor_01",
            "actor_id 不应替换既有 encounter unit_id 生成规则。"
        );
        _test.Eq(
            actor?.encounter_actor_id.ToString() ?? "",
            "pack_leader",
            "首领单位应保留稳定的 encounter_actor_id。"
        );
        _test.Eq(
            CountUnitsWithActorId(enemyUnits, ""),
            2,
            "未声明 actor_id 的群体条目应继续投影空 encounter_actor_id。"
        );
    }

    private void TestEncounterRosterBuilderBuildsMixedMistHollowUnits()
    {
        using GameSession gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        using EncounterRosterBuilder builder = new();
        builder.Setup(
            gameSession.GetBattleEncounterDefinitions(),
            gameSession.GetEncounterRosterDefinitions(),
            gameSession.GetEnemyTemplateDefinitions()
        );

        EncounterAnchorData encounterAnchor = new()
        {
            entity_id = "mist_hollow_stage2",
            display_name = "雾沼伏猎群",
            world_coord = new Vector2I(8, 8),
            faction_id = "hostile",
            region_tag = "south_wilds",
            vision_range = 2,
            encounter_profile_id = "mist_hollow",
            growth_stage = 2,
        };
        using GodotProjectionLease<GArray> enemyUnitsLease = builder.BuildEnemyUnitsLease(
            encounterAnchor,
            gameSession.GetContentCatalogTyped().GetSkillDefinitionsTyped(),
            gameSession.GetEnemyTemplateDefinitions(),
            gameSession.GetEnemyAiBrainDefinitions(),
            gameSession.GetItemDefsTyped()
        );
        GArray enemyUnits = enemyUnitsLease.Value;

        _test.Eq(enemyUnits.Count, 5, "mist_hollow 第 2 阶段应构建 5 个敌方单位。");
        _test.Eq(
            CountUnitsWithTemplateId(enemyUnits, "mist_beast"),
            2,
            "mist_hollow 第 2 阶段应包含 2 个雾沼异兽。"
        );
        _test.Eq(
            CountUnitsWithTemplateId(enemyUnits, "mist_harrier"),
            2,
            "mist_hollow 第 2 阶段应包含 2 个雾沼猎压者。"
        );
        _test.Eq(
            CountUnitsWithTemplateId(enemyUnits, "mist_weaver"),
            1,
            "mist_hollow 第 2 阶段应包含 1 个雾沼织咒者。"
        );
        _test.Eq(
            CountUnitsWithBrain(enemyUnits, "ranged_suppressor"),
            2,
            "雾沼猎压者应使用 ranged_suppressor brain。"
        );
        _test.Eq(
            CountUnitsWithBrain(enemyUnits, "healer_controller"),
            1,
            "雾沼织咒者应使用 healer_controller brain。"
        );
        _test.True(
            UnitHasSkillOnTemplate(enemyUnits, "mist_weaver", "mage_glacial_prison"),
            "雾沼织咒者应携带控制技能 mage_glacial_prison。"
        );
    }

    private void TestEncounterRosterBuilderBuildsOfficialWolfStageTwoUnits()
    {
        using GameSession gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        using EncounterRosterBuilder builder = new();
        builder.Setup(
            gameSession.GetBattleEncounterDefinitions(),
            gameSession.GetEncounterRosterDefinitions(),
            gameSession.GetEnemyTemplateDefinitions()
        );

        EncounterAnchorData encounterAnchor = new()
        {
            entity_id = "wolf_wilds_stage2",
            display_name = "荒狼群",
            world_coord = new Vector2I(8, 8),
            faction_id = "hostile",
            region_tag = "south_wilds",
            vision_range = 2,
            encounter_profile_id = "wolf_wilds",
            growth_stage = 2,
        };
        using GodotProjectionLease<GArray> enemyUnitsLease = builder.BuildEnemyUnitsLease(
            encounterAnchor,
            gameSession.GetContentCatalogTyped().GetSkillDefinitionsTyped(),
            gameSession.GetEnemyTemplateDefinitions(),
            gameSession.GetEnemyAiBrainDefinitions(),
            gameSession.GetItemDefsTyped()
        );
        GArray enemyUnits = enemyUnitsLease.Value;

        _test.Eq(enemyUnits.Count, 5, "wolf_wilds 第 2 阶段应构建五个敌方单位。");
        _test.Eq(
            CountUnitsWithTemplateId(enemyUnits, "wolf_pack"),
            3,
            "wolf_wilds 第 2 阶段应包含三只常规狼。"
        );
        _test.Eq(
            CountUnitsWithTemplateId(enemyUnits, "worg"),
            2,
            "wolf_wilds 第 2 阶段应包含两只座狼。"
        );
        _test.True(
            UnitHasSkillOnTemplate(enemyUnits, "wolf_pack", "basic_attack"),
            "第 2 阶段的常规狼应携带基础攻击。"
        );
        _test.False(
            UnitHasSkillOnTemplate(enemyUnits, "wolf_pack", "charge"),
            "第 2 阶段的常规狼不应携带冲锋。"
        );
        _test.True(
            UnitHasSkillOnTemplate(enemyUnits, "worg", "basic_attack"),
            "第 2 阶段的座狼应携带基础攻击。"
        );
        _test.True(
            UnitHasSkillOnTemplate(enemyUnits, "worg", "charge"),
            "第 2 阶段的座狼应携带冲锋。"
        );
    }

    private static WildEncounterRosterStageDef BuildStage(
        int stage,
        params WildEncounterRosterUnitEntryDef[] unitEntries
    )
    {
        WildEncounterRosterStageDef stageDef = new()
        {
            stage = stage,
        };
        foreach (WildEncounterRosterUnitEntryDef unitEntry in unitEntries)
        {
            stageDef.unit_entries.Add(unitEntry);
        }
        return stageDef;
    }

    private static WildEncounterRosterStageDefinition BuildDefinitionStage(
        int stage,
        params WildEncounterRosterUnitEntryDefinition[] unitEntries
    ) => new(stage, unitEntries);

    private static string FormatErrors(IEnumerable<string> errors)
    {
        List<string> values = new();
        foreach (string error in errors)
        {
            values.Add(error);
        }
        return values.Count == 0 ? "[]" : $"[{string.Join(" | ", values)}]";
    }

    private static bool ContainsError(IEnumerable<string> errors, string fragment)
    {
        foreach (string error in errors)
        {
            if ((error ?? "").Contains(fragment))
            {
                return true;
            }
        }
        return false;
    }

    private static int CountUnitsWithTemplateId(GArray enemyUnits, StringName templateId)
    {
        int total = 0;
        foreach (BattleUnitState unit in BattleUnits(enemyUnits))
        {
            if (unit.enemy_template_id == templateId)
            {
                total += 1;
            }
        }
        return total;
    }

    private static int CountUnitsWithBrain(GArray enemyUnits, StringName brainId)
    {
        int total = 0;
        foreach (BattleUnitState unit in BattleUnits(enemyUnits))
        {
            if (unit.ai_brain_id == brainId)
            {
                total += 1;
            }
        }
        return total;
    }

    private static int CountUnitsWithActorId(GArray enemyUnits, StringName actorId)
    {
        int total = 0;
        foreach (BattleUnitState unit in BattleUnits(enemyUnits))
        {
            if (unit.encounter_actor_id == actorId)
            {
                total += 1;
            }
        }
        return total;
    }

    private static BattleUnitState FindUnitWithActorId(GArray enemyUnits, StringName actorId)
    {
        foreach (BattleUnitState unit in BattleUnits(enemyUnits))
        {
            if (unit.encounter_actor_id == actorId)
            {
                return unit;
            }
        }
        return null;
    }

    private static bool UnitHasSkillOnTemplate(GArray enemyUnits, StringName templateId, StringName skillId)
    {
        foreach (BattleUnitState unit in BattleUnits(enemyUnits))
        {
            if (unit.enemy_template_id == templateId && unit.KnowsActiveSkill(skillId))
            {
                return true;
            }
        }
        return false;
    }

    private static IEnumerable<BattleUnitState> BattleUnits(GArray enemyUnits)
    {
        if (enemyUnits == null)
        {
            yield break;
        }
        foreach (object unitValue in enemyUnits)
        {
            if (TryAsBattleUnitState(unitValue, out BattleUnitState unit))
            {
                yield return unit;
            }
        }
    }

    private static bool TryAsBattleUnitState(object rawValue, out BattleUnitState value)
    {
        return BattleUnitState.TryReadUnitPayload(rawValue, out value);
    }

}
