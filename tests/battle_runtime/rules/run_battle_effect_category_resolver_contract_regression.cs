using System;
using Godot;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_battle_effect_category_resolver_contract_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestResolverTypeIsPlainStaticCSharp();
        TestCategoryFieldsAreFormalSchema();
        TestResolverUsesExplicitDeliveryAndEffectCategories();
        TestResolverIgnoresLegacyParamsBarrierCategories();
        TestResolverDoesNotGuessFromSkillIdOrTags();

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
        var combatProfile = TestResourceOwnership.Own(
            new CombatSkillDef(),
            "BattleEffectCategoryResolverContract.combat-profile"
        );
        var effect = TestResourceOwnership.Own(
            new CombatEffectDef(),
            "BattleEffectCategoryResolverContract.effect"
        );

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
        SkillDefinition skill = BuildSkill(
            "contract_explicit_categories",
            new[] { new StringName("spell"), new StringName("projectile") }
        );
        var effect = new CombatEffectDefinition(
            effectType: default,
            effectTargetTeamFilter: default,
            statusId: default,
            saveFailureStatusId: default,
            terrainEffectId: default,
            terrainReplaceTo: default,
            heightDelta: 0,
            requiresWeapon: false,
            addWeaponDice: false,
            preventRepeatTarget: false,
            forcedMoveMode: default,
            minSkillLevel: 0,
            maxSkillLevel: -1,
            damageTag: default,
            damageRatioPercent: 100,
            preResistanceDamageMultiplier: 1.0,
            bonusCondition: default,
            hpRatioThresholdPercent: 0,
            damageCategory: default,
            drBypassTag: default,
            diceCount: 0,
            diceSides: 0,
            diceBonus: 0,
            bonusDamageDiceCount: 0,
            bonusDamageDiceSides: 0,
            bonusDamageDiceBonus: 0,
            saveDc: 0,
            saveDcMode: default,
            saveDcSourceAbility: default,
            saveAbility: default,
            savePartialOnSuccess: false,
            saveTag: default,
            thresholdBaseValue: 0,
            thresholdLevelAnchor: 0,
            thresholdLevelBonusPerDelta: 0,
            thresholdMaxHpRatioPercent: 0,
            thresholdCapMaxHpRatioPercent: 0,
            soulFractureDurationTu: 0,
            healMultiplierPercent: 0,
            shieldGainMultiplierPercent: 0,
            appliedStatusDurationTu: 0,
            durationTu: 0,
            tickIntervalTu: 0,
            effectTags: Array.Empty<StringName>(),
            parameters: null,
            effectCategories: new[] { new StringName("force_effect"), new StringName("mental_attack") }
        );

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
        SkillDefinition skill = BuildSkill("contract_legacy_params", Array.Empty<StringName>());
        var effect = TestResourceOwnership.Own(
            new CombatEffectDef(),
            "BattleEffectCategoryResolverContract.legacy-params-effect"
        );
        effect.@params = new Godot.Collections.Dictionary
        {
            ["barrier_categories"] = new Godot.Collections.Array<StringName>
            {
                new("spell"),
                new("force_effect"),
            },
        };

        var categories = BattleEffectCategoryResolver.ResolveCategories(
            skill,
            new[] { CombatEffectDefinition.FromResource(effect) }
        );

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
        SkillDefinition skill = TestSkillDefinitionProjection.BuildSkill(
            "mage_arcane_missile_detect_breath",
            displayName: "Misleading Contract Skill",
            combatProfile: TestSkillDefinitionProjection.BuildCombatProfile(
                "mage_arcane_missile_detect_breath"
            ),
            tags: new[]
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
            Array.Empty<CombatEffectDefinition>()
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

    private static SkillDefinition BuildSkill(StringName skillId, StringName[] deliveryCategories)
    {
        return TestSkillDefinitionProjection.BuildSkill(
            skillId,
            displayName: skillId.ToString(),
            combatProfile: TestSkillDefinitionProjection.BuildCombatProfile(
                skillId,
                deliveryCategories: deliveryCategories
            )
        );
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

}
