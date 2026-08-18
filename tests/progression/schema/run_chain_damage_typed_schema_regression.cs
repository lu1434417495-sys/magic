using System;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_chain_damage_typed_schema_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestValidTypedDefinitionPassesChainValidation();
        TestFiniteTargetLimitRemainsSupported();
        TestLegacyParamsAreRejected();
        TestInvalidTypedFieldsAreRejected();
        TestChainFieldsOnNonChainEffectAreRejected();
        TestChainLevelWindowsRejectOverlapAndAllowLateUnlock();
        RequestTestExit(_test.Finish("Chain damage typed schema regression"));
    }

    private void TestValidTypedDefinitionPassesChainValidation()
    {
        using CombatEffectDef chain = BuildChainEffect();
        using CombatSkillDef profile = BuildProfile(chain);
        using SkillDef skill = BuildSkill(profile, maxLevel: 7);
        string errors = FormatErrors(Validate(profile, skill));
        _test.False(
            errors.Contains("chain_", StringComparison.Ordinal)
                || errors.Contains("chain_damage", StringComparison.Ordinal),
            $"完整 typed chain_damage 定义不应产生连锁校验错误。errors={errors}"
        );
    }

    private void TestFiniteTargetLimitRemainsSupported()
    {
        using CombatEffectDef chain = BuildChainEffect();
        chain.chain_max_total_targets = 3;
        using CombatSkillDef profile = BuildProfile(chain);
        using SkillDef skill = BuildSkill(profile, maxLevel: 7);
        string errors = FormatErrors(Validate(profile, skill));
        _test.False(
            errors.Contains("chain_max_total_targets", StringComparison.Ordinal),
            $"通用 chain_damage 仍应允许显式有限上限。errors={errors}"
        );
    }

    private void TestLegacyParamsAreRejected()
    {
        using CombatEffectDef chain = BuildChainEffect();
        chain.@params = new GDictionary
        {
            ["base_chain_radius"] = 1,
            ["wet_chain_radius"] = 2,
            ["bonus_terrain_effect_id"] = "wet",
        };
        using CombatSkillDef profile = BuildProfile(chain);
        using SkillDef skill = BuildSkill(profile, maxLevel: 7);
        string errors = FormatErrors(Validate(profile, skill));
        _test.True(
            errors.Contains("params.base_chain_radius is unsupported; use CombatEffectDef.chain_base_hop_range"),
            $"旧基础半径参数必须被拒绝。errors={errors}"
        );
        _test.True(
            errors.Contains("params.wet_chain_radius is unsupported; use CombatEffectDef.chain_conductive_hop_range"),
            $"旧湿地半径参数必须被拒绝。errors={errors}"
        );
        _test.True(
            errors.Contains("params.bonus_terrain_effect_id is unsupported; use CombatEffectDef.chain_conductive_terrain_effect_ids"),
            $"旧导电地形参数必须被拒绝。errors={errors}"
        );
    }

    private void TestInvalidTypedFieldsAreRejected()
    {
        using CombatEffectDef chain = BuildChainEffect();
        chain.chain_base_hop_range = 0;
        chain.chain_conductive_hop_range = -1;
        chain.chain_max_total_targets = 1;
        chain.chain_backlash_hop_range_bonus = -1;
        chain.prevent_repeat_target = false;
        chain.chain_conductive_status_ids.Add("shocked");
        using CombatSkillDef profile = BuildProfile(chain);
        using SkillDef skill = BuildSkill(profile, maxLevel: 7);
        string errors = FormatErrors(Validate(profile, skill));
        foreach (
            string expected in new[]
            {
                "chain_base_hop_range must be >= 1",
                "chain_conductive_hop_range must be >= chain_base_hop_range",
                "chain_max_total_targets must be 0 for unlimited or >= 2",
                "chain_backlash_hop_range_bonus must be >= 0",
                "chain_damage requires prevent_repeat_target=true",
                "chain_conductive_status_ids[1] duplicates shocked",
            }
        )
            _test.True(errors.Contains(expected), $"应报告 {expected}。errors={errors}");
    }

    private void TestChainFieldsOnNonChainEffectAreRejected()
    {
        using var damage = new CombatEffectDef
        {
            effect_type = "damage",
            effect_target_team_filter = "enemy",
            dice_count = 1,
            dice_sides = 6,
            chain_base_hop_range = 1,
        };
        using CombatSkillDef profile = BuildProfile(damage);
        using SkillDef skill = BuildSkill(profile, maxLevel: 3);
        string errors = FormatErrors(Validate(profile, skill));
        _test.True(
            errors.Contains(
                "chain fields are only supported on chain_damage effects.",
                StringComparison.Ordinal
            ),
            $"非 chain_damage effect 携带 typed chain 字段必须被精确拒绝。errors={errors}"
        );
    }

    private void TestChainLevelWindowsRejectOverlapAndAllowLateUnlock()
    {
        using CombatEffectDef overlapLeft = BuildChainEffect(0, 2);
        using CombatEffectDef overlapRight = BuildChainEffect(2, -1);
        using CombatSkillDef overlapProfile = BuildProfile(overlapLeft, overlapRight);
        using SkillDef overlapSkill = BuildSkill(overlapProfile, maxLevel: 3);
        string overlapErrors = FormatErrors(Validate(overlapProfile, overlapSkill));
        _test.True(
            overlapErrors.Contains("chain_damage level windows must not overlap"),
            $"重叠连锁等级窗口必须被拒绝。errors={overlapErrors}"
        );

        using CombatEffectDef lateUnlock = BuildChainEffect(2, -1);
        using CombatSkillDef lateUnlockProfile = BuildProfile(lateUnlock);
        using SkillDef lateUnlockSkill = BuildSkill(lateUnlockProfile, maxLevel: 3);
        string lateUnlockErrors = FormatErrors(
            Validate(lateUnlockProfile, lateUnlockSkill)
        );
        _test.Eq(
            lateUnlockErrors,
            "",
            $"generic chain_damage 应允许到指定等级才解锁；未激活等级由 runtime 返回 Empty。errors={lateUnlockErrors}"
        );
    }

    private static CombatEffectDef BuildChainEffect(
        int minSkillLevel = 0,
        int maxSkillLevel = -1
    )
    {
        return new CombatEffectDef
        {
            effect_type = "chain_damage",
            effect_target_team_filter = "any",
            min_skill_level = minSkillLevel,
            max_skill_level = maxSkillLevel,
            prevent_repeat_target = true,
            chain_base_hop_range = 1,
            chain_conductive_hop_range = 2,
            chain_max_total_targets = 0,
            chain_conductive_status_ids = new Godot.Collections.Array<StringName> { "shocked" },
            chain_conductive_terrain_effect_ids = new Godot.Collections.Array<StringName> { "wet" },
            chain_backlash_hop_range_bonus = 1,
        };
    }

    private static CombatSkillDef BuildProfile(params CombatEffectDef[] effects)
    {
        var profile = new CombatSkillDef
        {
            skill_id = "chain_damage_schema_probe",
            projectile_kind = "magical",
            target_team_filter = "enemy",
            range_value = 5,
            target_selection_mode = "single_unit",
        };
        foreach (CombatEffectDef effect in effects ?? Array.Empty<CombatEffectDef>())
            profile.effect_defs.Add(effect);
        return profile;
    }

    private static SkillDef BuildSkill(CombatSkillDef profile, int maxLevel) => new()
    {
        skill_id = "chain_damage_schema_probe",
        max_level = maxLevel,
        combat_profile = profile,
    };

    private static GStringArray Validate(CombatSkillDef profile, SkillDef skill)
    {
        var errors = new GStringArray();
        var validator = new SkillCombatProfileValidator(
            new SkillDamageEffectValidator(),
            new SkillExecuteEffectValidator()
        );
        validator.AppendCombatProfileValidationErrors(
            errors,
            "chain_damage_schema_probe",
            profile,
            skill
        );
        return errors;
    }

    private static string FormatErrors(GStringArray errors) =>
        string.Join(" | ", errors ?? new GStringArray());
}
