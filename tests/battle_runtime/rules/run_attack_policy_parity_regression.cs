using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_attack_policy_parity_regression : SceneTree
{
    private readonly GStringArray _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        var hitResolver = new BattleHitResolver();
        var policy = new BattleAttackCheckPolicyService();
        policy.setup(null, hitResolver, null);
        var battleState = new BattleState();
        var activeUnit = new BattleUnitState
        {
            unit_id = "caster",
            coord = new Vector2I(1, 1),
        };
        activeUnit.known_skill_level_map[new StringName("parity_skill")] = 3;
        var targetUnit = new BattleUnitState
        {
            unit_id = "target",
            coord = new Vector2I(3, 1),
        };
        SkillDef skillDef = BuildParitySkill();
        CombatEffectDef repeatEffect = BuildRepeatEffect();
        List<BattleRepeatAttackStageSpec> repeatStageSpecs =
            BattleRepeatAttackResolver.build_stage_specs_from_repeat_attack_effect(
                activeUnit,
                skillDef,
                repeatEffect,
                -1,
                true
            );
        BattleAttackCheckPolicyContext repeatPreviewContext =
            policy.build_repeat_attack_stage_context(
                battleState,
                activeUnit,
                targetUnit,
                skillDef,
                default,
                "repeat_attack_preview",
                "hud_preview"
            );
        BattleRepeatAttackStageSpec stageSpec =
            BattleRepeatAttackResolver.build_stage_spec_from_repeat_attack_effect(
                activeUnit,
                skillDef,
                repeatEffect,
                2,
                0,
                true
            );
        BattleAttackCheckPolicyContext stageContext = policy.build_repeat_attack_stage_context(
            battleState,
            activeUnit,
            targetUnit,
            skillDef,
            stageSpec,
            "repeat_attack_stage_check",
            "execute"
        );
        BattleAttackCheckPolicyContext attackContext = policy.build_attack_context(
            battleState,
            activeUnit,
            targetUnit,
            skillDef,
            "skill_attack_check",
            "execute",
            false
        );
        BattleAttackCheckPolicyContext previewContext = policy.build_attack_context(
            battleState,
            activeUnit,
            targetUnit,
            skillDef,
            "skill_attack_preview",
            "hud_preview",
            false
        );

        AssertAttackCheckEq(
            policy.build_attack_check(attackContext, 0, 0),
            hitResolver.build_skill_attack_check(activeUnit, targetUnit, skillDef, 0, 0),
            "policy build_attack_check 应与 BattleHitResolver 零漂移。"
        );
        AssertPreviewEq(
            policy.build_attack_preview(previewContext),
            hitResolver.build_skill_attack_preview(
                battleState,
                activeUnit,
                targetUnit,
                skillDef,
                false
            ),
            "policy build_attack_preview 应与 BattleHitResolver 零漂移。"
        );
        AssertPreviewEq(
            policy.build_repeat_attack_preview(repeatPreviewContext, repeatStageSpecs),
            hitResolver.build_repeat_attack_preview(
                battleState,
                activeUnit,
                targetUnit,
                skillDef,
                repeatEffect,
                -1
            ),
            "policy build_repeat_attack_preview 应与 BattleHitResolver 零漂移。"
        );
        AssertAttackCheckEq(
            policy.build_fate_aware_repeat_attack_stage_hit_check(stageContext),
            hitResolver.build_fate_aware_repeat_attack_stage_hit_check(
                battleState,
                activeUnit,
                targetUnit,
                skillDef,
                repeatEffect,
                2
            ),
            "policy repeat stage fate-aware check 应与 BattleHitResolver 零漂移。"
        );

        if (_failures.Count == 0)
        {
            GD.Print("Attack policy parity regression: PASS");
            return 0;
        }
        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Attack policy parity regression: FAIL ({_failures.Count})");
        return 1;
    }

    private static SkillDef BuildParitySkill()
    {
        var combatProfile = new CombatSkillDef
        {
            skill_id = "parity_skill",
            attack_roll_bonus = -2,
        };
        return new SkillDef
        {
            skill_id = "parity_skill",
            combat_profile = combatProfile,
        };
    }

    private static CombatEffectDef BuildRepeatEffect()
    {
        return new CombatEffectDef
        {
            effect_type = "repeat_attack_until_fail",
            @params = new GDictionary
            {
                ["base_attack_bonus"] = 1,
                ["follow_up_attack_penalty"] = 2,
                ["penalty_free_stages_by_level"] = new GDictionary { [3] = 1 },
            },
        };
    }

    private void AssertAttackCheckEq(AttackCheckInput actual, AttackCheckInput expected, string message)
    {
        string actualText = AttackCheckText(actual);
        string expectedText = AttackCheckText(expected);
        if (!string.Equals(actualText, expectedText, StringComparison.Ordinal))
        {
            _failures.Add($"{message} actual={actualText} expected={expectedText}");
        }
    }

    private void AssertPreviewEq(AttackPreviewData actual, AttackPreviewData expected, string message)
    {
        if (actual == null && expected == null) return;
        if (actual == null || expected == null)
        {
            _failures.Add($"{message} | actual={actual} expected={expected}");
            return;
        }
        if (actual.SummaryText != expected.SummaryText)
            _failures.Add($"{message} SummaryText actual={actual.SummaryText} expected={expected.SummaryText}");
        if (actual.HitRatePercent != expected.HitRatePercent)
            _failures.Add($"{message} HitRatePercent actual={actual.HitRatePercent} expected={expected.HitRatePercent}");
        if (actual.SuccessRatePercent != expected.SuccessRatePercent)
            _failures.Add($"{message} SuccessRatePercent actual={actual.SuccessRatePercent} expected={expected.SuccessRatePercent}");
        if (actual.BaseHitRatePercent != expected.BaseHitRatePercent)
            _failures.Add($"{message} BaseHitRatePercent actual={actual.BaseHitRatePercent} expected={expected.BaseHitRatePercent}");
        if (actual.StageCount != expected.StageCount)
            _failures.Add($"{message} StageCount actual={actual.StageCount} expected={expected.StageCount}");
        for (int i = 0; i < Math.Min(actual.StageCount, expected.StageCount); i++)
        {
            if (actual.Stages[i].SuccessRatePercent != expected.Stages[i].SuccessRatePercent)
                _failures.Add($"{message} Stage[{i}] SuccessRatePercent actual={actual.Stages[i].SuccessRatePercent} expected={expected.Stages[i].SuccessRatePercent}");
        }
    }

    private static string AttackCheckText(AttackCheckInput value) =>
        string.Join(
            "|",
            value.AttackerBaseAttackBonus,
            value.AttackerAttackBonus,
            value.AttackerBab,
            value.TargetArmorClass,
            value.SkillAttackBonus,
            value.LockedSkillHitBonus,
            value.SituationalAttackBonus,
            value.SituationalAttackPenalty,
            value.RequiredRoll,
            value.DisplayRequiredRoll,
            value.HitRatePercent,
            value.SuccessRatePercent,
            value.BaseHitRatePercent,
            value.NaturalOneAutoMiss,
            value.NaturalTwentyAutoHit,
            value.CritThreshold,
            value.FumbleLowEnd,
            value.CritLocked,
            value.CritGateDie,
            value.ForceHitNoCrit,
            value.SkillId,
            value.FollowUpAttackPenalty,
            value.ExponentialPenalty,
            value.IsDisadvantage,
            value.Invalid,
            value.ErrorId,
            value.ErrorMessage,
            value.PreviewText
        );

    private static string StableDictionary(GDictionary dictionary)
    {
        if (dictionary == null)
        {
            return "{}";
        }
        var parts = new List<string>();
        foreach (Variant key in dictionary.Keys)
        {
            parts.Add($"{StableVariant(key)}:{StableVariant(dictionary[key])}");
        }
        parts.Sort(StringComparer.Ordinal);
        return "{" + string.Join(",", parts) + "}";
    }

    private static string StableArray(GArray array)
    {
        if (array == null)
        {
            return "[]";
        }
        var parts = new List<string>();
        foreach (Variant value in array)
        {
            parts.Add(StableVariant(value));
        }
        return "[" + string.Join(",", parts) + "]";
    }

    private static string StableVariant(Variant value)
    {
        return value.VariantType switch
        {
            Variant.Type.Nil => "nil",
            Variant.Type.Bool => value.AsBool() ? "true" : "false",
            Variant.Type.Int => value.AsInt64().ToString(),
            Variant.Type.Float => value.AsDouble().ToString("R"),
            Variant.Type.String => $"s:{value.AsString()}",
            Variant.Type.StringName => $"sn:{value.AsStringName()}",
            Variant.Type.Dictionary => StableDictionary(value.AsGodotDictionary()),
            Variant.Type.Array => StableArray(value.AsGodotArray()),
            _ => value.ToString(),
        };
    }
}
