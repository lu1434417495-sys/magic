using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_attack_policy_parity_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        var hitResolver = new BattleHitResolver();
        var policy = new BattleAttackCheckPolicyService();
        policy.Setup(null, hitResolver, null);
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
        targetUnit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass), 12);
        SkillDefinition skillDefinition = BuildParitySkill();
        CombatEffectDefinition repeatEffectDefinition = BuildRepeatEffect();
        List<BattleRepeatAttackStageSpec> repeatStageSpecs =
            BattleRepeatAttackResolver.BuildStageSpecsFromRepeatAttackEffect(
                activeUnit,
                skillDefinition,
                repeatEffectDefinition,
                -1,
                true
            );
        BattleAttackCheckPolicyContext repeatPreviewContext =
            policy.BuildRepeatAttackStageContext(
                battleState,
                activeUnit,
                targetUnit,
                skillDefinition,
                default,
                "repeat_attack_preview",
                "hud_preview"
            );
        BattleRepeatAttackStageSpec stageSpec =
            BattleRepeatAttackResolver.BuildStageSpecFromRepeatAttackEffect(
                activeUnit,
                skillDefinition,
                repeatEffectDefinition,
                2,
                0,
                true
            );
        BattleAttackCheckPolicyContext stageContext = policy.BuildRepeatAttackStageContext(
            battleState,
            activeUnit,
            targetUnit,
            skillDefinition,
            stageSpec,
            "repeat_attack_stage_check",
            "execute"
        );
        BattleAttackCheckPolicyContext attackContext = policy.BuildSkillDefinitionAttackContext(
            battleState,
            activeUnit,
            targetUnit,
            skillDefinition,
            "skill_attack_check",
            "execute",
            false
        );
        BattleAttackCheckPolicyContext previewContext = policy.BuildSkillDefinitionAttackContext(
            battleState,
            activeUnit,
            targetUnit,
            skillDefinition,
            "skill_attack_preview",
            "hud_preview",
            false
        );
        BattleAttackCheckPolicyContext dtoAttackContext = policy.BuildSkillDefinitionAttackContext(
            battleState,
            activeUnit,
            targetUnit,
            skillDefinition,
            "skill_attack_check",
            "execute",
            false
        );
        BattleAttackCheckPolicyContext dtoPreviewContext = policy.BuildSkillDefinitionAttackContext(
            battleState,
            activeUnit,
            targetUnit,
            skillDefinition,
            "skill_attack_preview",
            "hud_preview",
            false
        );

        AssertAttackCheckEq(
            policy.BuildAttackCheck(attackContext, 0, 0),
            hitResolver.BuildSkillAttackCheck(activeUnit, targetUnit, skillDefinition, 0, 0),
            "policy build_attack_check 应与 BattleHitResolver 零漂移。"
        );
        AssertPreviewEq(
            policy.BuildAttackPreview(previewContext),
            hitResolver.BuildSkillAttackPreview(
                battleState,
                activeUnit,
                targetUnit,
                skillDefinition,
                false
            ),
            "policy build_attack_preview 应与 BattleHitResolver 零漂移。"
        );
        AssertAttackCheckEq(
            policy.BuildAttackCheck(dtoAttackContext, 0, 0),
            hitResolver.BuildSkillDefinitionAttackCheck(activeUnit, targetUnit, skillDefinition, 0, 0),
            "DTO policy build_attack_check 应与 BattleHitResolver 零漂移。"
        );
        AssertPreviewEq(
            policy.BuildAttackPreview(dtoPreviewContext),
            hitResolver.BuildSkillDefinitionAttackPreview(
                battleState,
                activeUnit,
                targetUnit,
                skillDefinition,
                false
            ),
            "DTO policy build_attack_preview 应与 BattleHitResolver 零漂移。"
        );
        AssertPreviewEq(
            policy.BuildRepeatAttackPreview(repeatPreviewContext, repeatStageSpecs),
            hitResolver.BuildRepeatAttackPreview(
                battleState,
                activeUnit,
                targetUnit,
                skillDefinition,
                repeatEffectDefinition,
                -1
            ),
            "policy build_repeat_attack_preview 应与 BattleHitResolver 零漂移。"
        );
        AssertAttackCheckEq(
            policy.BuildFateAwareRepeatAttackStageHitCheck(stageContext),
            hitResolver.BuildFateAwareRepeatAttackStageHitCheck(
                battleState,
                activeUnit,
                targetUnit,
                skillDefinition,
                repeatEffectDefinition,
                2
            ),
            "policy repeat stage fate-aware check 应与 BattleHitResolver 零漂移。"
        );

        RequestTestExit(_test.Finish("Attack policy parity regression"));
    }

    private static SkillDefinition BuildParitySkill()
    {
        return TestSkillDefinitionProjection.BuildSkill(
            "parity_skill",
            combatProfile: TestSkillDefinitionProjection.BuildCombatProfile(
                "parity_skill",
                attackRollBonus: -2
            )
        );
    }

    private static CombatEffectDefinition BuildRepeatEffect()
    {
        return TestSkillDefinitionProjection.BuildEffect(
            "repeat_attack_until_fail",
            parameters: new Dictionary<string, Variant>
            {
                ["base_attack_bonus"] = 1,
                ["follow_up_attack_penalty"] = 2,
                ["penalty_free_stages_by_level"] = new GDictionary { [3] = 1 },
            }
        );
    }

    private void AssertAttackCheckEq(AttackCheckInput actual, AttackCheckInput expected, string message)
    {
        string actualText = AttackCheckText(actual);
        string expectedText = AttackCheckText(expected);
        if (!string.Equals(actualText, expectedText, StringComparison.Ordinal))
        {
            _test.Fail($"{message} actual={actualText} expected={expectedText}");
        }
    }

    private void AssertPreviewEq(AttackPreviewData actual, AttackPreviewData expected, string message)
    {
        if (actual == null && expected == null) return;
        if (actual == null || expected == null)
        {
            _test.Fail($"{message} | actual={actual} expected={expected}");
            return;
        }
        if (actual.SummaryText != expected.SummaryText)
            _test.Fail($"{message} SummaryText actual={actual.SummaryText} expected={expected.SummaryText}");
        if (actual.HitRatePercent != expected.HitRatePercent)
            _test.Fail($"{message} HitRatePercent actual={actual.HitRatePercent} expected={expected.HitRatePercent}");
        if (actual.SuccessRatePercent != expected.SuccessRatePercent)
            _test.Fail($"{message} SuccessRatePercent actual={actual.SuccessRatePercent} expected={expected.SuccessRatePercent}");
        if (actual.BaseHitRatePercent != expected.BaseHitRatePercent)
            _test.Fail($"{message} BaseHitRatePercent actual={actual.BaseHitRatePercent} expected={expected.BaseHitRatePercent}");
        if (actual.StageCount != expected.StageCount)
            _test.Fail($"{message} StageCount actual={actual.StageCount} expected={expected.StageCount}");
        for (int i = 0; i < Math.Min(actual.StageCount, expected.StageCount); i++)
        {
            if (actual.Stages[i].SuccessRatePercent != expected.Stages[i].SuccessRatePercent)
                _test.Fail($"{message} Stage[{i}] SuccessRatePercent actual={actual.Stages[i].SuccessRatePercent} expected={expected.Stages[i].SuccessRatePercent}");
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
