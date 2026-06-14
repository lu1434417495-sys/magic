using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_skill_requirements_typed_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestRequirementSchemaValidation();
        TestOfficialSkillResourcesExposeTypedRequirementsAndSources();

        Quit(_test.Finish("Skill requirement typed regression"));
    }

    private void TestRequirementSchemaValidation()
    {
        SkillDef prerequisite = BuildSkill("charge");
        SkillDef validReference = BuildSkill("warrior_spin_slash");
        SkillDef badLearnRequirement = BuildSkill("invalid_learn_requirement");
        badLearnRequirement.learn_requirements = new Godot.Collections.Array<StringName>
        {
            "missing_skill",
        };

        SkillDef badSkillLevelRequirement = BuildSkill("invalid_skill_level_requirement");
        badSkillLevelRequirement.skill_level_requirements = new GDictionary { ["charge"] = "5" };

        SkillDef badAttributeRequirement = BuildSkill("invalid_attribute_requirement");
        badAttributeRequirement.attribute_requirements = new GDictionary { ["hp_max"] = 1 };

        SkillDef badUpgradeSources = BuildSkill("invalid_upgrade_sources");
        badUpgradeSources.unlock_mode = "composite_upgrade";
        badUpgradeSources.upgrade_source_skill_ids = new Godot.Collections.Array<StringName>
        {
            "missing_upgrade_skill",
        };

        GStringArray errors = CollectValidationErrors(
            prerequisite,
            validReference,
            badLearnRequirement,
            badSkillLevelRequirement,
            badAttributeRequirement,
            badUpgradeSources
        );

        _test.True(
            errors.Count >= 4,
            "非法 skill requirement fixture 应保持非法。"
        );
    }

    private void TestOfficialSkillResourcesExposeTypedRequirementsAndSources()
    {
        ProgressionContentRegistry registry = new();
        IReadOnlyDictionary<StringName, SkillDef> skillDefs = registry.GetSkillDefsTyped();

        _test.True(
            skillDefs.TryGetValue("warrior_whirlwind_slash", out SkillDef whirlwind)
                && whirlwind != null,
            "ProgressionContentRegistry 应暴露正式旋风斩升级资源。"
        );
        _test.True(
            skillDefs.TryGetValue("saint_blade_combo", out SkillDef saintBladeCombo)
                && saintBladeCombo != null,
            "ProgressionContentRegistry 应暴露正式圣刃连段资源。"
        );
        _test.True(
            skillDefs.TryGetValue("vajra_body", out SkillDef vajraBody) && vajraBody != null,
            "ProgressionContentRegistry 应暴露正式金刚不坏资源。"
        );
        if (whirlwind == null || saintBladeCombo == null || vajraBody == null)
            return;

        _test.True(
            HasStringName(whirlwind.LearnRequirementsTyped, "charge")
                && HasStringName(whirlwind.LearnRequirementsTyped, "warrior_spin_slash"),
            "旋风斩应通过 typed learn_requirements 暴露前置技能。"
        );
        _test.Eq(
            whirlwind.SkillLevelRequirementsTyped["charge"],
            5,
            "旋风斩应通过 typed skill_level_requirements 暴露冲锋等级前置。"
        );
        _test.Eq(
            whirlwind.SkillLevelRequirementsTyped["warrior_spin_slash"],
            5,
            "旋风斩应通过 typed skill_level_requirements 暴露回旋斩等级前置。"
        );
        _test.True(
            HasStringName(saintBladeCombo.KnowledgeRequirementsTyped, "compania_family_legacy"),
            "圣刃连段应通过 typed knowledge_requirements 暴露知识前置。"
        );
        _test.True(
            HasStringName(saintBladeCombo.AchievementRequirementsTyped, "six_hit_combo"),
            "圣刃连段应通过 typed achievement_requirements 暴露成就前置。"
        );
        _test.True(
            HasStringName(saintBladeCombo.UpgradeSourceSkillIdsTyped, "warrior_combo_strike")
                && HasStringName(
                    saintBladeCombo.UpgradeSourceSkillIdsTyped,
                    "warrior_aura_slash"
                ),
            "圣刃连段应通过 typed upgrade_source_skill_ids 暴露复合来源。"
        );
        _test.True(
            HasStringName(vajraBody.MasterySourcesTyped, "heavy_hit_taken")
                && HasStringName(vajraBody.MasterySourcesTyped, "max_damage_die_taken")
                && HasStringName(vajraBody.MasterySourcesTyped, "elite_or_boss_damage_taken"),
            "金刚不坏应通过 typed mastery_sources 暴露正式受击熟练来源。"
        );
    }

    private static SkillDef BuildSkill(StringName skillId)
    {
        return new SkillDef
        {
            skill_id = skillId,
            display_name = skillId.ToString(),
            learn_source = "book",
            max_level = 1,
            mastery_curve = new[] { 10 },
        };
    }

    private static GStringArray CollectValidationErrors(params SkillDef[] skillDefs)
    {
        GDictionary indexedSkillDefs = new();
        foreach (SkillDef skillDef in skillDefs)
        {
            if (skillDef != null && skillDef.skill_id != "")
                indexedSkillDefs[skillDef.skill_id] = skillDef;
        }

        ProgressionContentRegistry registry = new();
        registry.ReplaceValidationSources(
            new GDictionary
            {
                ["skill_defs"] = indexedSkillDefs,
                ["profession_defs"] = new GDictionary(),
                ["achievement_defs"] = new GDictionary(),
                ["quest_defs"] = new GDictionary(),
                ["race_defs"] = new GDictionary(),
                ["subrace_defs"] = new GDictionary(),
                ["race_trait_defs"] = new GDictionary(),
                ["age_profile_defs"] = new GDictionary(),
                ["bloodline_defs"] = new GDictionary(),
                ["bloodline_stage_defs"] = new GDictionary(),
                ["ascension_defs"] = new GDictionary(),
                ["ascension_stage_defs"] = new GDictionary(),
                ["stage_advancement_defs"] = new GDictionary(),
            }
        );
        return registry.CollectValidationErrors();
    }

    private static bool HasStringName(IReadOnlyList<StringName> values, StringName target)
    {
        foreach (StringName value in values)
        {
            if (value == target)
                return true;
        }
        return false;
    }
}
