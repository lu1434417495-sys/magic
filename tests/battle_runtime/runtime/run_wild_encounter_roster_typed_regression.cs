using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_wild_encounter_roster_typed_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestTypedStageSelectionUsesNearestDeclaredStage();
        TestSchemaValidationUsesTypedTemplateIdBoundary();
        TestEncounterRosterBuilderBuildsMixedMistHollowUnits();
        RequestTestExit(_test.Finish("Wild encounter roster typed regression"));
    }

    private void TestTypedStageSelectionUsesNearestDeclaredStage()
    {
        WildEncounterRosterDefinition roster = new(
            "typed_stage_roster",
            "Typed Stage Roster",
            0,
            1,
            0,
            new[]
            {
                BuildDefinitionStage(
                0,
                    new WildEncounterRosterUnitEntryDefinition("wolf", 1, "荒狼前锋")
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
        }

        IReadOnlyList<WildEncounterRosterUnitEntryDefinition> stageThreeEntries =
            roster.GetStageUnitEntries(3);
        _test.Eq(stageThreeEntries.Count, 1, "growth_stage=3 时应命中最近的已声明 stage 2。");
        if (stageThreeEntries.Count > 0)
        {
            _test.Eq(stageThreeEntries[0].TemplateId.ToString(), "wolf_shaman", "stage 2 template_id 应被 typed 读取。");
            _test.Eq(stageThreeEntries[0].Count, 2, "stage 2 count 应被 typed 读取。");
            _test.Eq(stageThreeEntries[0].DisplayName, "织咒者", "stage 2 display_name 应被 typed 读取。");
        }
    }

    private void TestSchemaValidationUsesTypedTemplateIdBoundary()
    {
        using WildEncounterRosterDef roster = new()
        {
            profile_id = "schema_roster",
            display_name = "Schema Roster",
            initial_stage = 0,
        };
        roster.stages.Add(
            BuildStage(
                0,
                new WildEncounterRosterUnitEntryDef
                {
                    template_id = "wolf",
                    count = 1,
                }
            )
        );

        GStringArray typedErrors = roster.ValidateSchemaTyped(
            new HashSet<StringName> { "wolf" }
        );
        _test.Eq(typedErrors.Count, 0, $"typed ValidateSchemaTyped() 应接受正式 template id set。 errors={FormatErrors(typedErrors)}");

        GStringArray missingTemplateErrors = roster.ValidateSchemaTyped(
            new HashSet<StringName>()
        );
        _test.True(
            missingTemplateErrors.Count > 0,
            $"typed ValidateSchemaTyped() 应直接报告缺失 template。 errors={FormatErrors(missingTemplateErrors)}"
        );
    }

    private void TestEncounterRosterBuilderBuildsMixedMistHollowUnits()
    {
        using GameSession gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        using EncounterRosterBuilder builder = new();
        builder.Setup(
            gameSession.GetEncounterRosterDefinitions(),
            gameSession.GetEnemyTemplateDefinitions()
        );

        EncounterAnchorData encounterAnchor = new()
        {
            entity_id = "mist_hollow_stage2",
            display_name = "雾沼伏猎群",
            world_coord = new Vector2I(8, 8),
            faction_id = "hostile",
            enemy_roster_template_id = "mist_beast",
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

    private static bool UnitHasSkillOnTemplate(GArray enemyUnits, StringName templateId, StringName skillId)
    {
        foreach (BattleUnitState unit in BattleUnits(enemyUnits))
        {
            if (unit.enemy_template_id == templateId && unit.known_active_skill_ids.Contains(skillId))
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
