using System;
using Godot;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_battle_effect_category_resolver_contract_regression : SceneTree
{
    private readonly GStringArray _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        TestResolverTypeIsPlainStaticCSharp();
        TestCategoryFieldsAreFormalSchema();
        TestResolverUsesExplicitDeliveryAndEffectCategories();
        TestResolverIgnoresLegacyParamsBarrierCategories();
        TestResolverDoesNotGuessFromSkillIdOrTags();

        if (_failures.Count == 0)
        {
            GD.Print("Battle effect category resolver contract regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Battle effect category resolver contract regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestResolverTypeIsPlainStaticCSharp()
    {
        Type resolverType = typeof(BattleEffectCategoryResolver);
        AssertTrue(
            resolverType.IsAbstract && resolverType.IsSealed,
            "效果类别 resolver 应是 plain static C# class。"
        );
        AssertFalse(
            typeof(RefCounted).IsAssignableFrom(resolverType),
            "效果类别 resolver 不应继承 RefCounted。"
        );
        AssertFalse(
            HasAttributeNamed(resolverType, "GlobalClassAttribute"),
            "效果类别 resolver 不应注册 GlobalClass。"
        );
    }

    private void TestCategoryFieldsAreFormalSchema()
    {
        var combatProfile = new CombatSkillDef();
        var effect = new CombatEffectDef();

        AssertNotNull(
            combatProfile.delivery_categories,
            "CombatSkillDef 必须暴露 delivery_categories 作为正式投送类别 schema。"
        );
        AssertNotNull(
            effect.effect_categories,
            "CombatEffectDef 必须暴露 effect_categories 作为正式效果类别 schema。"
        );
    }

    private void TestResolverUsesExplicitDeliveryAndEffectCategories()
    {
        SkillDef skill = BuildSkill(
            "contract_explicit_categories",
            new[] { new StringName("spell"), new StringName("projectile") }
        );
        var effect = new CombatEffectDef();
        effect.effect_categories.Add(new StringName("force_effect"));
        effect.effect_categories.Add(new StringName("mental_attack"));

        var categories = BattleEffectCategoryResolver.ResolveCategories(
            skill,
            new[] { effect }
        );

        AssertTrue(
            ContainsCategory(categories, "spell"),
            "Resolver 必须包含 explicit delivery category spell。"
        );
        AssertTrue(
            ContainsCategory(categories, "projectile"),
            "Resolver 必须包含 explicit delivery category projectile。"
        );
        AssertTrue(
            ContainsCategory(categories, "force_effect"),
            "Resolver 必须包含 explicit effect category force_effect。"
        );
        AssertTrue(
            ContainsCategory(categories, "mental_attack"),
            "Resolver 必须包含 explicit effect category mental_attack。"
        );
    }

    private void TestResolverIgnoresLegacyParamsBarrierCategories()
    {
        SkillDef skill = BuildSkill("contract_legacy_params", Array.Empty<StringName>());
        var effect = new CombatEffectDef();
        effect.@params = new Godot.Collections.Dictionary
        {
            ["barrier_categories"] = new Godot.Collections.Array<StringName>
            {
                new("spell"),
                new("force_effect"),
            },
        };

        var categories = BattleEffectCategoryResolver.ResolveCategories(skill, new[] { effect });

        AssertFalse(
            ContainsCategory(categories, "spell"),
            "Resolver 不应读取 legacy params.barrier_categories。"
        );
        AssertFalse(
            ContainsCategory(categories, "force_effect"),
            "Resolver 不应读取 legacy params.barrier_categories。"
        );
    }

    private void TestResolverDoesNotGuessFromSkillIdOrTags()
    {
        var skill = new SkillDef
        {
            skill_id = "mage_arcane_missile_detect_breath",
            display_name = "Misleading Contract Skill",
            combat_profile = new CombatSkillDef(),
        };
        skill.tags.Add("mage");
        skill.tags.Add("magic");
        skill.tags.Add("missile");
        skill.tags.Add("breath");
        skill.tags.Add("psychic");

        var categories = BattleEffectCategoryResolver.ResolveCategories(
            skill,
            Array.Empty<CombatEffectDef>()
        );

        AssertFalse(
            ContainsCategory(categories, "magical_missile"),
            "Resolver 不应从 skill_id 文本推断 magical_missile。"
        );
        AssertFalse(
            ContainsCategory(categories, "detection"),
            "Resolver 不应从 skill_id 文本推断 detection。"
        );
        AssertFalse(
            ContainsCategory(categories, "breath_weapon"),
            "Resolver 不应从 tags 推断 breath_weapon。"
        );
        AssertFalse(
            ContainsCategory(categories, "mental_attack"),
            "Resolver 不应从 tags 推断 mental_attack。"
        );
    }

    private static SkillDef BuildSkill(StringName skillId, StringName[] deliveryCategories)
    {
        var combatProfile = new CombatSkillDef { skill_id = skillId };
        foreach (StringName category in deliveryCategories)
        {
            combatProfile.delivery_categories.Add(category);
        }

        return new SkillDef
        {
            skill_id = skillId,
            display_name = skillId.ToString(),
            combat_profile = combatProfile,
        };
    }

    private static bool ContainsCategory(
        System.Collections.Generic.IEnumerable<StringName> categories,
        StringName expected
    )
    {
        foreach (StringName category in categories)
        {
            if (category == expected)
            {
                return true;
            }
        }
        return false;
    }

    private static bool HasAttributeNamed(Type type, string attributeTypeName)
    {
        foreach (object attribute in type.GetCustomAttributes(false))
        {
            if (attribute.GetType().Name == attributeTypeName)
            {
                return true;
            }
        }
        return false;
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
    }

    private void AssertFalse(bool condition, string message)
    {
        AssertTrue(!condition, message);
    }

    private void AssertNotNull(object value, string message)
    {
        AssertTrue(value != null, message);
    }
}
