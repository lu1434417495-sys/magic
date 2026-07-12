using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_promotion_selection_typed_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestSelectionDataNormalizesEquivalentValues();
        TestSelectionDataRoundTripsPlainPayload();
        TestSelectionDataDoesNotExposeLegacyPayloadProjection();
        TestProgressionServiceDoesNotExposeDictionarySelectionOverload();
        TestProfessionPromotionConsumesTypedSelection();

        RequestTestExit(_test.Finish("Promotion selection typed regression"));
    }

    private void TestSelectionDataRoundTripsPlainPayload()
    {
        IReadOnlyDictionary<string, object> payload = new Dictionary<string, object>(
            StringComparer.Ordinal
        )
        {
            [PromotionSelectionData.AssignedCoreSkillIdsKey] = new List<object>
            {
                "slash",
                "guard",
            },
            [PromotionSelectionData.TriggerSkillIdsKey] = new List<object> { "focus" },
        };

        PromotionSelectionData selection = PromotionSelectionData.FromPlainPayload(payload);

        _test.True(
            selection.HasAssignedCoreSkillIds,
            "Plain promotion selection 应保留 assigned core skill 字段存在性。"
        );
        _test.False(
            selection.HasQualifierSkillIds,
            "Plain promotion selection 不应把缺失 qualifier 字段当成显式空选择。"
        );
        _test.True(
            selection.HasTriggerSkillIds,
            "Plain promotion selection 应保留 trigger skill 字段存在性。"
        );
        _test.Eq(
            selection.AssignedCoreSkillIds[0],
            new StringName("slash"),
            "Plain promotion selection 应按稳定顺序读取 assigned core skill。"
        );
        _test.Eq(
            selection.TriggerSkillIds[0],
            new StringName("focus"),
            "Plain promotion selection 应直接解析 trigger skill。"
        );

        IReadOnlyDictionary<string, object> roundTripPayload = selection.ToPlainPayload();
        _test.True(
            roundTripPayload[PromotionSelectionData.AssignedCoreSkillIdsKey]
                is IReadOnlyList<object>,
            "Promotion selection plain payload 的 nested skill ids 应保持 CLR list。"
        );
        PromotionSelectionData restored = PromotionSelectionData.FromPlainPayload(
            roundTripPayload
        );
        _test.True(
            restored.SelectionEquals(selection),
            "Promotion selection 应通过递归 plain payload 保持字段存在性与稳定顺序。"
        );
    }

    private void TestSelectionDataDoesNotExposeLegacyPayloadProjection()
    {
        _test.True(
            typeof(PromotionSelectionData).GetMethod("ToPayloadProjection", Type.EmptyTypes)
                == null,
            "PromotionSelectionData 不应继续暴露无 lease 所有权的 Godot payload projection。"
        );
    }

    private void TestSelectionDataNormalizesEquivalentValues()
    {
        PromotionSelectionData selection = new(
            assignedCoreSkillIds: new object[]
            {
                "slash",
                new StringName("slash"),
                Variant.From("guard"),
                "",
            },
            qualifierSkillIds: new object[]
            {
                new StringName("guard"),
                "guard",
                "focus",
            },
            triggerSkillIds: new object[]
            {
                Variant.From(new StringName("slash")),
                "slash",
            }
        );

        _test.Eq(
            selection.AssignedCoreSkillIds.Count,
            2,
            "PromotionSelectionData 应去重并忽略空 assigned core skill id。"
        );
        _test.Eq(
            selection.AssignedCoreSkillIds[0],
            new StringName("slash"),
            "PromotionSelectionData 应保留 assigned core skill 的稳定顺序。"
        );
        _test.Eq(
            selection.AssignedCoreSkillIds[1],
            new StringName("guard"),
            "PromotionSelectionData 应把 string/StringName/Variant 归一到同一 StringName 值。"
        );
        _test.Eq(
            selection.QualifierSkillIds.Count,
            2,
            "PromotionSelectionData 应去重 qualifier skill id。"
        );
        _test.Eq(
            selection.TriggerSkillIds.Count,
            1,
            "PromotionSelectionData 应去重 trigger skill id。"
        );
    }

    private void TestProgressionServiceDoesNotExposeDictionarySelectionOverload()
    {
        var weakOverload = typeof(ProgressionService).GetMethod(
            nameof(ProgressionService.PromoteProfession),
            new[] { typeof(StringName), typeof(GDictionary) }
        );
        _test.True(
            weakOverload == null,
            "ProgressionService.PromoteProfession 不应再暴露 GDictionary selection 正式入口。"
        );
    }

    private void TestProfessionPromotionConsumesTypedSelection()
    {
        StringName triggerSkillId = "slash";
        UnitProgress progress = BuildReadyProgress(triggerSkillId);
        SkillDefinition triggerSkill = TestSkillDefinitionProjection.BuildSkill(
            triggerSkillId,
            displayName: "Slash",
            maxLevel: 1
        );
        ProfessionDef profession = new()
        {
            profession_id = "warrior",
            display_name = "Warrior",
            is_initial_profession = true,
            max_rank = 1,
            hit_die_sides = 1,
        };
        ProgressionService service = new();
        service.SetupDefinitions(
            progress,
            new System.Collections.Generic.Dictionary<StringName, SkillDefinition>
            {
                [triggerSkill.SkillId] = triggerSkill,
            },
            new System.Collections.Generic.Dictionary<StringName, ProfessionDefinition>
            {
                [profession.profession_id] =
                    TestProgressionDefinitionProjection.Profession(profession),
            }
        );

        int previousHpMax = progress.unit_base_attributes.GetAttributeValue("hp_max");
        bool promoted = service.PromoteProfession(
            profession.profession_id,
            new PromotionSelectionData(triggerSkillIds: new object[] { triggerSkillId })
        );

        _test.True(promoted, "typed promotion selection 应允许有效转职晋升。");
        _test.Eq(
            progress.GetProfessionProgress(profession.profession_id)?.rank ?? 0,
            1,
            "typed promotion selection 应写入职业 rank。"
        );
        _test.Eq(
            progress.unit_base_attributes.GetAttributeValue("hp_max"),
            previousHpMax + 1,
            "typed promotion selection 不应依赖 hp_roll_override 字典后门来固定 HP 增量。"
        );
    }

    private static UnitProgress BuildReadyProgress(StringName triggerSkillId)
    {
        UnitProgress progress = new()
        {
            unit_id = "hero",
            display_name = "Hero",
            active_level_trigger_core_skill_id = triggerSkillId,
        };
        progress.unit_base_attributes.SetAttributeValue("hp_max", 20);
        progress.unit_base_attributes.SetAttributeValue(
            UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Constitution),
            10
        );
        progress.SetSkillProgress(
            new UnitSkillProgress
            {
                skill_id = triggerSkillId,
                is_learned = true,
                is_core = true,
                skill_level = 1,
            }
        );
        return progress;
    }
}
