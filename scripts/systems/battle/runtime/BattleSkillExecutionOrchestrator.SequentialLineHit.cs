using System;
using System.Collections.Generic;
using Godot;

internal sealed partial class BattleSkillExecutionOrchestrator
{
    internal bool TryPreviewSequentialLineHitSkill(
        BattleUnitReadView activeUnit,
        BattleCommand command,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        BattlePreview preview
    )
    {
        if (!BattleSequentialLineHitRules.IsSequentialLineHitSkill(skillDefinition))
            return false;
        if (preview == null)
            return true;

        preview.allowed = false;
        preview.ClearTargetCoords();
        preview.ClearTargetUnitIds();
        preview.ClearDamagePreview();
        preview.hit_preview = null;

        BattleUnitState sourceUnit = Runtime?._state?.GetAliveUnit(activeUnit.UnitId);
        BattleUnitState primaryTarget = ResolveSingleCommandTarget(command);
        BattleSequentialLineHitPlan plan = BattleSequentialLineHitRules.BuildPlan(
            Runtime?._state,
            Runtime?.GetGridService(),
            Runtime?._layered_barrier_service,
            sourceUnit,
            primaryTarget,
            skillDefinition
        );
        foreach (Vector2I coord in plan.PathCoords)
            preview.AddTargetCoord(coord);
        foreach (BattleUnitState target in plan.Targets)
            preview.AddTargetUnitId(target.unit_id);
        if (!plan.Allowed)
        {
            preview.AddLogLine(plan.Message);
            return true;
        }

        IReadOnlyList<CombatEffectDefinition> baseEffects =
            CollectUnitSkillEffectDefinitions(
                skillDefinition,
                castVariantDefinition,
                activeUnit
            );
        CombatSequentialLineHitDefinition profile =
            skillDefinition.CombatProfile.SequentialLineHit;
        int skillLevel = activeUnit.GetKnownSkillLevel(skillDefinition.SkillId);
        int followUpPenalty = profile.GetFollowUpAttackPenalty(skillLevel);
        int continuationRange = profile.GetContinuationRange(skillLevel);
        CombatSkillResourceCosts costs =
            skillDefinition.CombatProfile.GetEffectiveResourceCostValues(skillLevel);
        BattleAttackCheckPolicyService attackPolicy = Runtime?.GetAttackCheckPolicyService();
        BattleLayeredBarrierService layeredBarrierService = Runtime?._layered_barrier_service;
        BattleBarrierPreviewSession barrierSession =
            layeredBarrierService?.BeginSkillBarrierPreviewSession();
        var stages = new List<AttackPreviewStage>();
        int reachBasisPoints = 10000;
        for (int targetIndex = 0; targetIndex < plan.Targets.Count; targetIndex++)
        {
            BattleUnitReadView targetView = new(plan.Targets[targetIndex]);
            BattleBarrierInteractionResult barrierResult =
                layeredBarrierService?.PreviewSkillBarrierInteractionResult(
                    activeUnit,
                    targetView,
                    skillDefinition,
                    baseEffects,
                    barrierSession,
                    castVariantDefinition
                ) ?? new BattleBarrierInteractionResult(false, false);
            AttackPreviewData targetPreview =
                barrierResult.Blocked
                    ? BuildBlockedSequentialLineHitPreview(barrierResult.PreviewText)
                    : BuildSequentialLineHitAttackPreview(
                        attackPolicy,
                        activeUnit,
                        targetView,
                        skillDefinition,
                        -targetIndex * followUpPenalty
                    );
            if (barrierResult.Blocked && !string.IsNullOrEmpty(barrierResult.PreviewText))
                preview.AddLogLine(barrierResult.PreviewText);
            AttackPreviewStage stage = BuildSequentialLineHitStage(
                targetPreview,
                reachBasisPoints
            );
            stages.Add(stage);
            reachBasisPoints = Mathf.Clamp(
                Mathf.RoundToInt(
                    reachBasisPoints * (stage.HitRatePercent / 100.0f)
                ),
                0,
                10000
            );
        }

        AttackPreviewStage primaryStage =
            stages.Count > 0
                ? stages[0]
                : new AttackPreviewStage(0, 0, 0, 21, 20, "0%");
        preview.hit_preview = new AttackPreviewData
        {
            Source = "sequential_line_hit",
            SummaryText = $"首段预计命中率 {primaryStage.HitRatePercent}%",
            Stages = stages,
            HitRatePercent = primaryStage.HitRatePercent,
            SuccessRatePercent = primaryStage.SuccessRatePercent,
            BaseHitRatePercent = primaryStage.BaseHitRatePercent,
        };

        var potentialEffects = new List<CombatEffectDefinition>();
        foreach (BattleUnitState _ in plan.Targets)
            potentialEffects.AddRange(baseEffects);
        preview.SetDamagePreview(
            BattleDamagePreviewRangeService.BuildSkillDamagePreview(
                activeUnit,
                potentialEffects
            )
        );
        preview.allowed = true;
        preview.AddLogLine(
            $"选择该正交方向上的首个敌人；每次命中后沿原方向继续最多 {continuationRange} 格，最多攻击 {plan.Targets.Count}/{skillDefinition.CombatProfile.GetEffectiveMaxTargetCount(skillLevel)} 个目标。"
        );
        preview.AddLogLine(
            $"后续每段累计承受 -{followUpPenalty} 攻击检定；任一段落空，或遇到友军、阻挡视线的边、墙体或屏障时立即停止。"
        );
        preview.AddLogLine(
            $"消耗 {costs.ApCost}AP/{costs.MpCost}法力，冷却 {costs.CooldownTu}TU。{plan.StopMessage}"
        );
        preview.AddLogLine(preview.hit_preview.SummaryText);
        _append_damage_preview_line(preview);
        return true;
    }

    private bool _handle_sequential_line_hit_skill_command(
        BattleUnitState activeUnit,
        BattleCommand command,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        BattleEventBatch batch
    )
    {
        BattleUnitState primaryTarget = ResolveSingleCommandTarget(command);
        BattleSequentialLineHitPlan plan = BattleSequentialLineHitRules.BuildPlan(
            Runtime?._state,
            Runtime?.GetGridService(),
            Runtime?._layered_barrier_service,
            activeUnit,
            primaryTarget,
            skillDefinition
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
            effectDefinitions
            ?? CollectUnitSkillEffectDefinitions(
                skillDefinition,
                castVariantDefinition,
                activeUnit
            );
        int skillLevel = _get_unit_skill_level(activeUnit, skillDefinition.SkillId);
        int followUpPenalty = skillDefinition.CombatProfile.SequentialLineHit
            .GetFollowUpAttackPenalty(skillLevel);
        for (int targetIndex = 0; targetIndex < plan.Targets.Count; targetIndex++)
        {
            BattleUnitState target = plan.Targets[targetIndex];
            if (target?.IsAlive() != true)
            {
                batch?.AddLogLine("下一个目标已经失效，力场长矛停止续行。");
                break;
            }
            int stageAttackPenalty = -targetIndex * followUpPenalty;
            bool attackSucceeded = false;
            batch?.AddLogLine(
                $"{target.display_name} 承受第 {targetIndex + 1} 段力场攻击（阶段攻击检定{FormatSignedBonus(stageAttackPenalty)}）。"
            );
            _apply_unit_skill_result(
                activeUnit,
                target,
                skillDefinition,
                castVariantDefinition,
                baseEffects,
                batch,
                flat_attack_bonus: stageAttackPenalty,
                resolution_sink: result => attackSucceeded = result.AttackSuccess
            );
            if (!attackSucceeded)
            {
                batch?.AddLogLine("本段攻击未命中，力场长矛立即消散。");
                break;
            }
            if (!activeUnit.IsAlive())
            {
                batch?.AddLogLine("施术者已倒下，力场长矛停止续行。");
                break;
            }
        }
        return true;
    }

    private AttackPreviewData BuildSequentialLineHitAttackPreview(
        BattleAttackCheckPolicyService attackPolicy,
        BattleUnitReadView activeUnit,
        BattleUnitReadView targetUnit,
        SkillDefinition skillDefinition,
        int flatAttackBonus
    )
    {
        if (attackPolicy == null || !activeUnit.IsValid || !targetUnit.IsValid)
            return new AttackPreviewData();
        BattleAttackCheckPolicyContext context =
            attackPolicy.BuildSkillDefinitionAttackContext(
                Runtime?._state,
                activeUnit,
                targetUnit,
                skillDefinition,
                new StringName("sequential_line_hit_preview"),
                new StringName("hud_preview"),
                false
            );
        return attackPolicy.BuildAttackPreview(context, flatAttackBonus, 0);
    }

    private static AttackPreviewStage BuildSequentialLineHitStage(
        AttackPreviewData preview,
        int reachBasisPoints
    )
    {
        AttackPreviewStage sourceStage =
            preview?.Stages?.Count > 0
                ? preview.Stages[0]
                : new AttackPreviewStage(0, 0, 0, 21, 20, "0%");
        return new AttackPreviewStage(
            sourceStage.HitRatePercent,
            sourceStage.SuccessRatePercent,
            sourceStage.BaseHitRatePercent,
            sourceStage.RequiredRoll,
            sourceStage.DisplayRequiredRoll,
            sourceStage.PreviewText,
            Mathf.Clamp(reachBasisPoints, 0, 10000),
            100
        );
    }

    private static AttackPreviewData BuildBlockedSequentialLineHitPreview(string reason)
    {
        string previewText = string.IsNullOrEmpty(reason) ? "0%（被屏障阻挡）" : reason;
        return new AttackPreviewData
        {
            SummaryText = previewText,
            Stages = new List<AttackPreviewStage>
            {
                new(0, 0, 0, 21, 20, previewText),
            },
            HitRatePercent = 0,
            SuccessRatePercent = 0,
            BaseHitRatePercent = 0,
        };
    }
}
