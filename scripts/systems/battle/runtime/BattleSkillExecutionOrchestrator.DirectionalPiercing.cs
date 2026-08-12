using System;
using System.Collections.Generic;
using Godot;

internal sealed partial class BattleSkillExecutionOrchestrator
{
    internal bool TryPreviewDirectionalPiercingSkill(
        BattleUnitReadView activeUnit,
        BattleCommand command,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        BattlePreview preview
    )
    {
        if (!BattleDirectionalPiercingRules.IsDirectionalPiercingSkill(skillDefinition))
            return false;
        if (preview == null)
            return true;

        preview.allowed = false;
        preview.ClearTargetCoords();
        preview.ClearTargetUnitIds();
        preview.ClearDamagePreview();
        preview.hit_preview = null;

        BattleGroundSkillValidationResult validation =
            Runtime?.ValidateGroundSkillCommandResultTyped(
                activeUnit,
                skillDefinition,
                castVariantDefinition,
                command
            ) ?? BattleGroundSkillValidationResult.Denied("地面技能目标无效。" );
        if (!validation.Allowed || validation.TargetCoords.Count != 1)
        {
            preview.AddLogLine(
                string.IsNullOrEmpty(validation.Message)
                    ? "贯穿方向无效。"
                    : validation.Message
            );
            return true;
        }
        BattleUnitState sourceUnit = Runtime?._state?.GetAliveUnit(activeUnit.UnitId);
        BattleDirectionalPiercingPlan plan = BattleDirectionalPiercingRules.BuildPlan(
            Runtime?._state,
            Runtime?.GetGridService(),
            sourceUnit,
            skillDefinition,
            validation.TargetCoords[0],
            BattleRangeService.GetEffectiveSkillRange(activeUnit, skillDefinition)
        );
        foreach (Vector2I pathCoord in plan.PathCoords)
            preview.AddTargetCoord(pathCoord);
        foreach (BattleUnitState targetUnit in plan.Targets)
            preview.AddTargetUnitId(targetUnit.unit_id);
        if (!plan.Allowed)
        {
            preview.AddLogLine(plan.Message);
            return true;
        }

        var firstTarget = new List<BattleUnitReadView>
        {
            new(plan.Targets[0]),
        };
        preview.hit_preview = _build_unit_skill_hit_preview(
            activeUnit,
            firstTarget,
            skillDefinition,
            castVariantDefinition
        );
        IReadOnlyList<CombatEffectDefinition> baseEffects =
            Runtime?.CollectGroundUnitEffectDefinitionsTyped(
                skillDefinition,
                castVariantDefinition,
                activeUnit
            ) ?? Array.Empty<CombatEffectDefinition>();
        int skillLevel = activeUnit.GetKnownSkillLevel(skillDefinition.SkillId);
        int baseDamagePercent = skillDefinition.CombatProfile.DirectionalPiercing
            .GetBaseDamagePercent(skillLevel);
        IReadOnlyList<CombatEffectDefinition> firstTargetEffects =
            BuildDirectionalPiercingEffects(baseEffects, baseDamagePercent / 100.0);
        preview.SetDamagePreview(
            BattleDamagePreviewRangeService.BuildSkillDamagePreview(
                activeUnit,
                firstTargetEffects
            )
        );

        int staminaCost = BattleDirectionalPiercingRules.CalculateStaminaCost(
            skillDefinition.CombatProfile.DirectionalPiercing,
            plan.FullRange,
            activeUnit.GetAttributeValue(new StringName("strength_modifier"))
        );
        string channelLabel = plan.LockedHeightSign switch
        {
            > 0 => "同层/+1层",
            < 0 => "同层/-1层",
            _ => "尚未锁定，仅命中同层",
        };
        preview.AddLogLine(
            $"方向 {FormatDirection(plan.Direction)}：贯穿 {plan.PathCoords.Count}/{plan.FullRange} 格，命中通道 {channelLabel}，预计攻击 {plan.Targets.Count} 个单位。"
        );
        preview.AddLogLine(
            $"体力按完整弓射程 {plan.FullRange} 与力量调整值计算，本次消耗 {staminaCost}；友军与中立单位同样会被攻击。"
        );
        preview.AddLogLine(
            $"首个成功命中的目标造成 {baseDamagePercent}% 武器伤害；此后每次成功命中令后续伤害倍率降低 {skillDefinition.CombatProfile.DirectionalPiercing.SuccessfulHitDecayPercent}%，最低为 {skillDefinition.CombatProfile.DirectionalPiercing.MinimumDamagePercent}%。"
        );
        foreach (BattleDirectionalPiercingSkippedTarget skipped in plan.SkippedTargets)
        {
            preview.AddLogLine(
                $"跳过 {skipped.UnitId}：相对高度 {FormatSigned(skipped.HeightDelta)}，{FormatSkipReason(skipped.Reason)}。"
            );
        }
        if (plan.BlockedBeforeCoord != new Vector2I(-1, -1))
        {
            preview.AddLogLine(
                $"投射路径在 ({plan.BlockedBeforeCoord.X}, {plan.BlockedBeforeCoord.Y}) 前被阻挡。"
            );
        }
        if (preview.hit_preview != null && !preview.hit_preview.IsEmpty)
            preview.AddLogLine(preview.hit_preview.SummaryText);
        _append_damage_preview_line(preview);
        preview.allowed = true;
        return true;
    }

    private bool _handle_directional_piercing_skill_command(
        BattleUnitState activeUnit,
        BattleCommand command,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        BattleEventBatch batch
    )
    {
        BattleGroundSkillValidationResult validation =
            Runtime?.ValidateGroundSkillCommandResultTyped(
                activeUnit,
                skillDefinition,
                castVariantDefinition,
                command
            ) ?? BattleGroundSkillValidationResult.Denied("地面技能目标无效。" );
        if (!validation.Allowed || validation.TargetCoords.Count != 1)
        {
            batch?.AddLogLine(
                string.IsNullOrEmpty(validation.Message)
                    ? "贯穿方向无效。"
                    : validation.Message
            );
            return false;
        }

        BattleDirectionalPiercingPlan plan = BattleDirectionalPiercingRules.BuildPlan(
            Runtime?._state,
            Runtime?.GetGridService(),
            activeUnit,
            skillDefinition,
            validation.TargetCoords[0],
            BattleRangeService.GetEffectiveSkillRange(activeUnit, skillDefinition)
        );
        if (!plan.Allowed)
        {
            batch?.AddLogLine(plan.Message);
            return false;
        }
        if (!_consume_skill_costs(activeUnit, skillDefinition, castVariantDefinition, batch))
            return false;

        CombatSkillResourceCosts costs = _get_effective_skill_resource_costs(
            activeUnit,
            skillDefinition
        );
        _record_action_issued(
            activeUnit,
            BattleTypedNames.ToStringName(BattleCommandKind.Skill),
            costs.ApCost
        );
        _append_changed_unit_id(batch, activeUnit.unit_id);
        if (ResolveSpellReactionsAfterCost(activeUnit, skillDefinition, batch).Interrupted)
            return false;

        IReadOnlyList<CombatEffectDefinition> baseEffects =
            Runtime?.CollectGroundUnitEffectDefinitionsTyped(
                skillDefinition,
                castVariantDefinition,
                activeUnit
            ) ?? Array.Empty<CombatEffectDefinition>();
        int skillLevel = _get_unit_skill_level(activeUnit, skillDefinition.SkillId);
        CombatDirectionalPiercingDefinition profile =
            skillDefinition.CombatProfile.DirectionalPiercing;
        int baseDamagePercent = profile.GetBaseDamagePercent(skillLevel);
        int successfulHitCount = 0;

        foreach (BattleUnitState targetUnit in plan.Targets)
        {
            if (targetUnit == null || !targetUnit.IsAlive())
                continue;
            int decayPercent = BattleDirectionalPiercingRules
                .GetDamagePercentAfterSuccessfulHits(profile, successfulHitCount);
            double combinedMultiplier = baseDamagePercent / 100.0 * decayPercent / 100.0;
            IReadOnlyList<CombatEffectDefinition> targetEffects =
                BuildDirectionalPiercingEffects(baseEffects, combinedMultiplier);
            bool attackSucceeded = false;
            batch?.AddLogLine(
                $"{targetUnit.display_name} 承受 {baseDamagePercent}%×{decayPercent}% 的贯穿武器伤害。"
            );
            _apply_unit_skill_result(
                activeUnit,
                targetUnit,
                skillDefinition,
                castVariantDefinition,
                targetEffects,
                batch,
                resolution_sink: result => attackSucceeded = result.AttackSuccess,
                force_weapon_attack_resolution: true
            );
            if (attackSucceeded)
                successfulHitCount++;
        }
        batch?.AddLogLine(
            $"{activeUnit.display_name} 的{_format_skill_variant_label(skillDefinition, castVariantDefinition)}沿 {FormatDirection(plan.Direction)} 贯穿，完成 {plan.Targets.Count} 次独立武器攻击。"
        );
        return true;
    }

    internal static IReadOnlyList<CombatEffectDefinition> BuildDirectionalPiercingEffects(
        IEnumerable<CombatEffectDefinition> effects,
        double multiplier
    )
    {
        var result = new List<CombatEffectDefinition>();
        foreach (CombatEffectDefinition effect in effects ?? Array.Empty<CombatEffectDefinition>())
        {
            if (effect == null)
                continue;
            result.Add(
                effect.EffectKind == BattleEffectKind.Damage
                    ? effect.WithPreResistanceDamageMultiplier(
                        effect.PreResistanceDamageMultiplier * Math.Max(multiplier, 0.0)
                    )
                    : effect
            );
        }
        return result.AsReadOnly();
    }

    private static string FormatDirection(Vector2I direction)
    {
        if (direction == Vector2I.Up)
            return "上";
        if (direction == Vector2I.Down)
            return "下";
        if (direction == Vector2I.Left)
            return "左";
        if (direction == Vector2I.Right)
            return "右";
        return "未知";
    }

    private static string FormatSigned(int value) => value > 0 ? $"+{value}" : value.ToString();

    private static string FormatSkipReason(BattleDirectionalPiercingSkipReason reason) =>
        reason switch
        {
            BattleDirectionalPiercingSkipReason.HeightOutOfRange => "超过允许的高度差",
            BattleDirectionalPiercingSkipReason.OppositeHeightChannel => "与自动锁定的高度通道相反",
            _ => "不符合贯穿条件",
        };
}
