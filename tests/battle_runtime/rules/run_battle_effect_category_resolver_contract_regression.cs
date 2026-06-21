using System;
using Godot;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_battle_effect_category_resolver_contract_regression : SceneTree
{
    private readonly TestHarness _test = new();
    private readonly System.Collections.Generic.List<GodotObject> _ownedGodotObjects = new();

    public override void _Initialize()
    {
        try
        {
            TestResolverTypeIsPlainStaticCSharp();
            TestCategoryFieldsAreFormalSchema();
            TestResolverUsesExplicitDeliveryAndEffectCategories();
            TestResolverIgnoresLegacyParamsBarrierCategories();
            TestResolverDoesNotGuessFromSkillIdOrTags();
        }
        finally
        {
            DisposeOwned();
            GodotSharpCleanup.CollectPendingFinalizers();
        }

        Quit(_test.Finish("Battle effect category resolver contract regression"));
    }

    private void TestResolverTypeIsPlainStaticCSharp()
    {
        Type resolverType = typeof(BattleEffectCategoryResolver);
        _test.True(
            resolverType.IsAbstract && resolverType.IsSealed,
            "效果类别 resolver 应是 plain static C# class。"
        );
    }

    private void TestCategoryFieldsAreFormalSchema()
    {
        var combatProfile = TrackOwned(new CombatSkillDef());
        var effect = TrackOwned(new CombatEffectDef());

        _test.True(
            combatProfile.delivery_categories != null,
            "CombatSkillDef 必须暴露 delivery_categories 作为正式投送类别 schema。"
        );
        _test.True(
            effect.effect_categories != null,
            "CombatEffectDef 必须暴露 effect_categories 作为正式效果类别 schema。"
        );
    }

    private void TestResolverUsesExplicitDeliveryAndEffectCategories()
    {
        SkillDef skill = BuildSkill(
            "contract_explicit_categories",
            new[] { new StringName("spell"), new StringName("projectile") }
        );
        var effect = TrackOwned(new CombatEffectDef());
        effect.effect_categories.Add(new StringName("force_effect"));
        effect.effect_categories.Add(new StringName("mental_attack"));

        var categories = BattleEffectCategoryResolver.ResolveCategories(
            skill,
            new[] { effect }
        );

        _test.True(
            ContainsCategory(categories, "spell"),
            "Resolver 必须包含 explicit delivery category spell。"
        );
        _test.True(
            ContainsCategory(categories, "projectile"),
            "Resolver 必须包含 explicit delivery category projectile。"
        );
        _test.True(
            ContainsCategory(categories, "force_effect"),
            "Resolver 必须包含 explicit effect category force_effect。"
        );
        _test.True(
            ContainsCategory(categories, "mental_attack"),
            "Resolver 必须包含 explicit effect category mental_attack。"
        );
    }

    private void TestResolverIgnoresLegacyParamsBarrierCategories()
    {
        SkillDef skill = BuildSkill("contract_legacy_params", Array.Empty<StringName>());
        var effect = TrackOwned(new CombatEffectDef());
        effect.@params = new Godot.Collections.Dictionary
        {
            ["barrier_categories"] = new Godot.Collections.Array<StringName>
            {
                new("spell"),
                new("force_effect"),
            },
        };

        var categories = BattleEffectCategoryResolver.ResolveCategories(skill, new[] { effect });

        _test.False(
            ContainsCategory(categories, "spell"),
            "Resolver 不应读取 legacy params.barrier_categories。"
        );
        _test.False(
            ContainsCategory(categories, "force_effect"),
            "Resolver 不应读取 legacy params.barrier_categories。"
        );
    }

    private void TestResolverDoesNotGuessFromSkillIdOrTags()
    {
        var skill = TrackOwned(new SkillDef
        {
            skill_id = "mage_arcane_missile_detect_breath",
            display_name = "Misleading Contract Skill",
            combat_profile = new CombatSkillDef(),
        });
        skill.SetTags(
            new[]
            {
                new StringName("mage"),
                new StringName("magic"),
                new StringName("missile"),
                new StringName("breath"),
                new StringName("psychic"),
            }
        );

        var categories = BattleEffectCategoryResolver.ResolveCategories(
            skill,
            Array.Empty<CombatEffectDef>()
        );

        _test.False(
            ContainsCategory(categories, "magical_missile"),
            "Resolver 不应从 skill_id 文本推断 magical_missile。"
        );
        _test.False(
            ContainsCategory(categories, "detection"),
            "Resolver 不应从 skill_id 文本推断 detection。"
        );
        _test.False(
            ContainsCategory(categories, "breath_weapon"),
            "Resolver 不应从 tags 推断 breath_weapon。"
        );
        _test.False(
            ContainsCategory(categories, "mental_attack"),
            "Resolver 不应从 tags 推断 mental_attack。"
        );
    }

    private SkillDef BuildSkill(StringName skillId, StringName[] deliveryCategories)
    {
        var combatProfile = new CombatSkillDef { skill_id = skillId };
        foreach (StringName category in deliveryCategories)
        {
            combatProfile.delivery_categories.Add(category);
        }

        return TrackOwned(new SkillDef
        {
            skill_id = skillId,
            display_name = skillId.ToString(),
            combat_profile = combatProfile,
        });
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

    private T TrackOwned<T>(T value)
        where T : GodotObject
    {
        if (value != null)
            _ownedGodotObjects.Add(value);
        return value;
    }

    private void DisposeOwned()
    {
        for (int index = _ownedGodotObjects.Count - 1; index >= 0; index--)
            BattleTestFixture.DisposeFixtureObject(_ownedGodotObjects[index]);
        _ownedGodotObjects.Clear();
    }

}
