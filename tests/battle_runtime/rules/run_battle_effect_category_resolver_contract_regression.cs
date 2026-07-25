using System;
using System.Collections.Generic;
using Godot;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_battle_effect_category_resolver_contract_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestResolverTypeIsPlainStaticCSharp();
        TestCategoryFieldsAreFormalSchema();
        TestResolverUsesExplicitDeliveryAndEffectCategories();
        TestTypedProjectileKindsDeriveInteractionCategories();
        TestCastVariantProjectileOverrideWins();
        TestResolverIgnoresLegacyParamsBarrierCategories();
        TestResolverDoesNotGuessFromSkillIdOrTags();

        RequestTestExit(_test.Finish("Battle effect category resolver contract regression"));
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
        _test.Eq(
            combatProfile.projectile_kind,
            new StringName("none"),
            "CombatSkillDef 必须以 none 作为 projectile_kind 的安全默认值。"
        );
        var castVariant = TestResourceOwnership.Own(
            new CombatCastVariantDef(),
            "BattleEffectCategoryResolverContract.cast-variant"
        );
        _test.Eq(
            castVariant.projectile_kind_override,
            new StringName(""),
            "CombatCastVariantDef 的空 projectile_kind_override 必须表示继承技能定义。"
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
            new[] { new StringName("spell"), new StringName("location") }
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
            ContainsCategory(categories, "location"),
            "Resolver 必须包含 explicit delivery category location。"
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

    private void TestTypedProjectileKindsDeriveInteractionCategories()
    {
        IReadOnlyList<StringName> magical = BattleEffectCategoryResolver.ResolveCategories(
            BuildSkill(
                "contract_magical_projectile",
                Array.Empty<StringName>(),
                projectileKind: "magical"
            ),
            Array.Empty<CombatEffectDefinition>()
        );
        _test.True(
            ContainsCategory(magical, "projectile")
                && ContainsCategory(magical, "magical_projectile"),
            "magical projectile_kind 必须派生 projectile 与 magical_projectile。"
        );
        _test.False(
            ContainsCategory(magical, "nonmagical_projectile"),
            "magical projectile_kind 不得同时派生 nonmagical_projectile。"
        );

        IReadOnlyList<StringName> nonmagical = BattleEffectCategoryResolver.ResolveCategories(
            BuildSkill(
                "contract_nonmagical_projectile",
                Array.Empty<StringName>(),
                projectileKind: "nonmagical"
            ),
            Array.Empty<CombatEffectDefinition>()
        );
        _test.True(
            ContainsCategory(nonmagical, "projectile")
                && ContainsCategory(nonmagical, "nonmagical_projectile"),
            "nonmagical projectile_kind 必须派生 projectile 与 nonmagical_projectile。"
        );
        _test.False(
            ContainsCategory(nonmagical, "magical_projectile"),
            "nonmagical projectile_kind 不得同时派生 magical_projectile。"
        );

        IReadOnlyList<StringName> none = BattleEffectCategoryResolver.ResolveCategories(
            BuildSkill("contract_non_projectile", Array.Empty<StringName>()),
            Array.Empty<CombatEffectDefinition>()
        );
        _test.False(
            ContainsCategory(none, "projectile"),
            "none projectile_kind 不得派生任何投射类别。"
        );
    }

    private void TestCastVariantProjectileOverrideWins()
    {
        SkillDefinition magicalBase = BuildSkill(
            "contract_variant_disables_projectile",
            Array.Empty<StringName>(),
            projectileKind: "magical"
        );
        CombatCastVariantDefinition disableProjectile =
            TestSkillDefinitionProjection.BuildCastVariant(
                "melee_variant",
                0,
                Array.Empty<CombatEffectDefinition>(),
                projectileKindOverride: "none"
            );
        IReadOnlyList<StringName> disabled = BattleEffectCategoryResolver.ResolveCategories(
            magicalBase,
            Array.Empty<CombatEffectDefinition>(),
            castVariantDefinition: disableProjectile
        );
        _test.False(
            ContainsCategory(disabled, "projectile"),
            "cast variant 的 none override 必须关闭技能级投射分类。"
        );

        SkillDefinition nonProjectileBase = BuildSkill(
            "contract_variant_enables_projectile",
            Array.Empty<StringName>()
        );
        CombatCastVariantDefinition magicalVariant =
            TestSkillDefinitionProjection.BuildCastVariant(
                "magical_projectile_variant",
                0,
                Array.Empty<CombatEffectDefinition>(),
                projectileKindOverride: "magical"
            );
        IReadOnlyList<StringName> enabled = BattleEffectCategoryResolver.ResolveCategories(
            nonProjectileBase,
            Array.Empty<CombatEffectDefinition>(),
            castVariantDefinition: magicalVariant
        );
        _test.True(
            ContainsCategory(enabled, "projectile")
                && ContainsCategory(enabled, "magical_projectile"),
            "cast variant 的 magical override 必须覆盖技能级 none。"
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
            new[]
            {
                CombatEffectDefinition.FromResource(
                    effect,
                    "test.battle_effect_category.legacy_params"
                ),
            }
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
            ContainsCategory(categories, "magical_projectile"),
            "Resolver 不应从 skill_id 文本推断 magical_projectile。"
        );
        _test.False(
            ContainsCategory(categories, "projectile"),
            "Resolver 不应从 skill_id 或 tags 推断 projectile。"
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

    private static SkillDefinition BuildSkill(
        StringName skillId,
        StringName[] deliveryCategories,
        StringName projectileKind = default
    )
    {
        return TestSkillDefinitionProjection.BuildSkill(
            skillId,
            displayName: skillId.ToString(),
            combatProfile: TestSkillDefinitionProjection.BuildCombatProfile(
                skillId,
                deliveryCategories: deliveryCategories,
                projectileKind: projectileKind
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
