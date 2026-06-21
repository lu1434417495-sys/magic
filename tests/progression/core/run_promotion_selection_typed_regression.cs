using System;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_promotion_selection_typed_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestSelectionDataNormalizesEquivalentValues();
        TestProgressionServiceDoesNotExposeDictionarySelectionOverload();
        TestProfessionPromotionConsumesTypedSelection();

        GodotSharpCleanup.CollectPendingFinalizers();
        Quit(_test.Finish("Promotion selection typed regression"));
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
        SkillDef triggerSkill = new()
        {
            skill_id = triggerSkillId,
            display_name = "Slash",
            max_level = 1,
        };
        ProfessionDef profession = new()
        {
            profession_id = "warrior",
            display_name = "Warrior",
            is_initial_profession = true,
            max_rank = 1,
            hit_die_sides = 1,
        };
        ProgressionService service = new();
        service.Setup(
            progress,
            new System.Collections.Generic.Dictionary<StringName, SkillDef>
            {
                [triggerSkill.skill_id] = triggerSkill,
            },
            new System.Collections.Generic.Dictionary<StringName, ProfessionDef>
            {
                [profession.profession_id] = profession,
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
