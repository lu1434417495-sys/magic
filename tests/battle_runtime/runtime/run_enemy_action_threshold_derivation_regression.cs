using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

// 敌人行动阈值单一真源回归。
// 设计来源：docs/design/battle/action_cadence.md §二
//
// 改前 EncounterRosterBuilder 直接把模板的 action_threshold 字段写进单位，绕过 AttributeSnapshot；
// 40 个模板手写阈值与自己写的 agility 冲突（pearson = -0.788），等于同一个意图被计价两次。
// P1 废除了该字段，敌人与角色改走同一张派生表。这条用例钉住两件事：
//   1. 敌人阈值确实来自 agility 派生，且落进 attribute_snapshot（不是旁路写入）；
//   2. 同 agility 的敌人与玩家角色得到相同阈值——作者要调快慢只能调 agility。
public partial class run_enemy_action_threshold_derivation_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestEnemyThresholdIsDerivedFromTemplateAgility();
        TestEnemyAndCharacterShareTheSameLadder();
        RequestTestExit(_test.Finish("Enemy action threshold derivation regression"));
    }

    private void TestEnemyThresholdIsDerivedFromTemplateAgility()
    {
        // 覆盖现存 40 个模板用到的全部档位（agility 6~17 => 调整值 -2 ~ +3）。
        int[] agilityValues = { 6, 8, 10, 12, 14, 16 };
        for (int index = 0; index < agilityValues.Length; index++)
        {
            int agility = agilityValues[index];
            BattleUnitState unit = BuildEnemyUnit($"threshold_probe_{agility}", agility);
            if (unit == null)
            {
                _test.Fail($"敌人模板 fixture 未能生成单位：agility={agility}");
                continue;
            }
            int expected = ActionCadenceContentRules.ResolveActionThreshold(
                AttributeSnapshot.CalculateScoreModifier(agility)
            );
            _test.Eq(
                unit.GetActionThresholdTyped(),
                expected,
                $"敌人 action_threshold 应由 agility 派生：agility={agility}"
            );
            _test.Eq(
                unit.attribute_snapshot?.GetValue(AttributeService.ACTION_THRESHOLD) ?? -1,
                expected,
                $"敌人阈值必须经过 attribute_snapshot，不得旁路写入：agility={agility}"
            );
        }
    }

    // 同一个 agility，敌人与玩家角色必须落在同一档——这是"单一真源"的可观测形式。
    private void TestEnemyAndCharacterShareTheSameLadder()
    {
        int[] agilityValues = { 8, 12, 16 };
        for (int index = 0; index < agilityValues.Length; index++)
        {
            int agility = agilityValues[index];
            BattleUnitState enemy = BuildEnemyUnit($"parity_enemy_{agility}", agility);
            BattleUnitState character = BuildCharacterUnit($"parity_character_{agility}", agility);
            if (enemy == null || character == null)
            {
                _test.Fail($"parity fixture 未能生成单位：agility={agility}");
                continue;
            }
            _test.Eq(
                enemy.GetActionThresholdTyped(),
                character.GetActionThresholdTyped(),
                $"同 agility 的敌人与角色应得到相同行动阈值：agility={agility}"
            );
        }
    }

    private static BattleUnitState BuildCharacterUnit(StringName unitId, int agility)
    {
        var spec = new BattleSimUnitSpec
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            current_hp = 30,
            current_ap = 2,
            base_attributes = BaseAttributes(agility),
        };
        return spec.ToDefinition("player", "manual").CreateRuntimeState();
    }

    private BattleUnitState BuildEnemyUnit(StringName templateId, int agility)
    {
        using GameSession gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        using EncounterRosterBuilder builder = new();

        var template = new EnemyTemplateDef
        {
            template_id = templateId,
            display_name = templateId.ToString(),
            brain_id = "",
            cognition_kind = "sapient",
            enemy_count = 1,
            body_size = BattleUnitState.BodySizeMedium,
            skill_ids = new GStringNameArray(),
            base_attribute_overrides = BaseAttributes(agility),
        };
        var itemDefinitions = new Dictionary<StringName, ItemDefinition>();
        var enemyTemplates = new Dictionary<StringName, EnemyTemplateDefinition>
        {
            [template.template_id] = template.ToDefinition(itemDefinitions),
        };

        StringName encounterProfileId = $"{templateId}_encounter";
        SetupSingleTemplateEncounter(builder, encounterProfileId, template.template_id);

        using GodotProjectionLease<GArray> enemyUnitsLease =
            builder.BuildEnemyUnitsFromDefinitionsLease(
                BuildEncounterAnchor(encounterProfileId, template.template_id),
                new Dictionary<StringName, SkillDefinition>(),
                enemyTemplates,
                new Dictionary<StringName, EnemyAiBrainDefinition>(),
                itemDefinitions
            );
        GArray enemyUnits = enemyUnitsLease.Value;
        if (enemyUnits.Count == 0)
            return null;
        return BattleUnitState.TryReadUnitPayload(enemyUnits[0], out BattleUnitState parsed)
            ? parsed
            : null;
    }

    private static GDictionary BaseAttributes(int agility) =>
        new()
        {
            ["strength"] = 10,
            ["agility"] = agility,
            ["constitution"] = 10,
            ["perception"] = 10,
            ["intelligence"] = 10,
            ["willpower"] = 10,
        };

    private static EncounterAnchorData BuildEncounterAnchor(
        StringName encounterId,
        StringName templateId
    ) =>
        new()
        {
            entity_id = encounterId,
            display_name = templateId.ToString(),
            world_coord = new Vector2I(3, 3),
            faction_id = "hostile",
            region_tag = "typed_tests",
            vision_range = 2,
            encounter_kind = EncounterAnchorData.ToStringName(EncounterAnchorKind.Single),
            encounter_profile_id = encounterId,
            growth_stage = 0,
            suppressed_until_step = 0,
        };

    private static void SetupSingleTemplateEncounter(
        EncounterRosterBuilder builder,
        StringName encounterProfileId,
        StringName templateId
    )
    {
        StringName rosterProfileId = $"{encounterProfileId}_roster";
        WildEncounterRosterDefinition roster = new(
            rosterProfileId,
            templateId.ToString(),
            0,
            1,
            new[]
            {
                new WildEncounterRosterStageDefinition(
                    0,
                    new[]
                    {
                        new WildEncounterRosterUnitEntryDefinition(
                            templateId,
                            1,
                            templateId.ToString()
                        ),
                    }
                ),
            }
        );
        BattleEncounterDefinition encounter = new(
            encounterProfileId,
            templateId.ToString(),
            rosterProfileId,
            BattleEliminationObjectiveDefinition.Instance,
            new BattleEncounterWorldResolutionDefinition(
                BattleWorldResolutionMode.Clear,
                BattleWorldResolutionMode.Preserve,
                BattleWorldResolutionMode.Preserve,
                0
            )
        );
        builder.Setup(
            new Dictionary<StringName, BattleEncounterDefinition>
            {
                [encounterProfileId] = encounter,
            },
            new Dictionary<StringName, WildEncounterRosterDefinition>
            {
                [rosterProfileId] = roster,
            },
            new Dictionary<StringName, EnemyTemplateDefinition>()
        );
    }
}
