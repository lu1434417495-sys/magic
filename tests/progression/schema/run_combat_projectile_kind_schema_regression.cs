using System;
using Godot;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_combat_projectile_kind_schema_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestTypedValuesRoundTrip();
        TestInvalidBaseAndVariantKindsAreRejected();
        TestDerivedAndRemovedCategoriesAreRejected();
        TestMageAndRangedTagsDoNotInferProjectileKind();

        RequestTestExit(_test.Finish("Combat projectile kind schema regression"));
    }

    private void TestTypedValuesRoundTrip()
    {
        foreach (
            CombatProjectileKind kind in new[]
            {
                CombatProjectileKind.None,
                CombatProjectileKind.Nonmagical,
                CombatProjectileKind.Magical,
                CombatProjectileKind.CurrentWeapon,
            }
        )
        {
            StringName id = CombatProjectileContentRules.ToProjectileKindId(kind);
            _test.Eq(
                CombatProjectileContentRules.ToProjectileKind(id),
                kind,
                $"{id} projectile kind 应无损往返。"
            );
        }
        _test.Eq(
            CombatProjectileContentRules.ToProjectileKind(""),
            CombatProjectileKind.Inherit,
            "空 projectile kind 只可表达 cast variant 继承。"
        );
    }

    private void TestInvalidBaseAndVariantKindsAreRejected()
    {
        using CombatSkillDef invalidBase = BuildProfile();
        invalidBase.projectile_kind = "laser";
        string baseErrors = FormatErrors(Validate(invalidBase));
        _test.True(
            baseErrors.Contains("unsupported projectile_kind laser"),
            $"未知 projectile_kind 必须在加载期失败。errors={baseErrors}"
        );

        using CombatSkillDef emptyBase = BuildProfile();
        emptyBase.projectile_kind = "";
        string emptyErrors = FormatErrors(Validate(emptyBase));
        _test.True(
            emptyErrors.Contains("unsupported projectile_kind"),
            $"技能级空 projectile_kind 不得被当成继承。errors={emptyErrors}"
        );

        using CombatSkillDef invalidVariant = BuildProfile();
        invalidVariant.cast_variants.Add(
            new CombatCastVariantDef
            {
                variant_id = "laser_variant",
                projectile_kind_override = "laser",
            }
        );
        string variantErrors = FormatErrors(Validate(invalidVariant));
        _test.True(
            variantErrors.Contains("unsupported projectile_kind_override laser"),
            $"未知 projectile_kind_override 必须在加载期失败。errors={variantErrors}"
        );
    }

    private void TestDerivedAndRemovedCategoriesAreRejected()
    {
        foreach (
            StringName category in new[]
            {
                new StringName("projectile"),
                new StringName("magical_projectile"),
                new StringName("nonmagical_projectile"),
            }
        )
        {
            using CombatSkillDef profile = BuildProfile();
            profile.delivery_categories.Add(category);
            string errors = FormatErrors(Validate(profile));
            _test.True(
                errors.Contains($"cannot author derived projectile category {category}"),
                $"派生类别 {category} 不得由内容直接写入。errors={errors}"
            );
        }
        using CombatSkillDef effectProfile = BuildProfile();
        effectProfile.effect_defs.Add(
            new CombatEffectDef
            {
                effect_type = "damage",
                damage_tag = "physical_blunt",
                effect_categories = new Godot.Collections.Array<StringName>
                {
                    "projectile",
                },
            }
        );
        string effectErrors = FormatErrors(Validate(effectProfile));
        _test.True(
            effectErrors.Contains("effect_categories[0] cannot author derived projectile category projectile"),
            $"effect_categories 也不得绕过 typed projectile owner。errors={effectErrors}"
        );

        foreach (
            StringName category in new[]
            {
                new StringName("magical_missile"),
                new StringName("nonmagical_missile"),
            }
        )
        {
            using CombatSkillDef profile = BuildProfile();
            profile.delivery_categories.Add(category);
            string errors = FormatErrors(Validate(profile));
            _test.True(
                errors.Contains($"uses removed projectile category {category}"),
                $"移除的类别 {category} 不得继续被接受。errors={errors}"
            );
        }
    }

    private void TestMageAndRangedTagsDoNotInferProjectileKind()
    {
        var required = CombatEffectCategoryContentRules.RequiredDeliveryCategories(
            isMageMagic: true,
            isDragonBreath: false
        );
        _test.True(Contains(required, "spell"), "法师魔法技能仍必须显式具备 spell 投送类别。");
        _test.False(
            Contains(required, "projectile")
                || Contains(required, "magical_projectile")
                || Contains(required, "nonmagical_projectile"),
            "职业、魔法、射程和伤害标签不得再推断投射物类别。"
        );
    }

    private static CombatSkillDef BuildProfile() => new()
    {
        skill_id = "projectile_schema_probe",
        projectile_kind = "none",
    };

    private static GStringArray Validate(CombatSkillDef profile)
    {
        var errors = new GStringArray();
        var validator = new SkillCombatProfileValidator(
            new SkillDamageEffectValidator(),
            new SkillExecuteEffectValidator()
        );
        validator.AppendCombatProfileValidationErrors(
            errors,
            "projectile_schema_probe",
            profile
        );
        return errors;
    }

    private static bool Contains(
        System.Collections.Generic.IEnumerable<StringName> values,
        StringName expected
    )
    {
        foreach (StringName value in values)
        {
            if (value == expected)
                return true;
        }
        return false;
    }

    private static string FormatErrors(GStringArray errors) =>
        string.Join(" | ", errors ?? new GStringArray());
}
