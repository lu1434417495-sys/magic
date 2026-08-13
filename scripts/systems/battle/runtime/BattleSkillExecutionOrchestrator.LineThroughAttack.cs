using System;
using System.Collections.Generic;
using Godot;

internal sealed partial class BattleSkillExecutionOrchestrator
{
    internal bool TryPreviewLineThroughAttackSkill(
        BattleUnitReadView activeUnit,
        BattleCommand command,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        BattlePreview preview
    )
    {
        if (!BattleLineThroughAttackRules.IsLineThroughAttackSkill(skillDefinition))
            return false;
        if (preview == null)
            return true;

        preview.allowed = false;
        preview.ClearTargetCoords();
        preview.ClearTargetUnitIds();
        preview.ClearSourceAdvancePath();
        preview.ClearDamagePreview();
        preview.hit_preview = null;

        BattleUnitState sourceUnit = Runtime?._state?.GetAliveUnit(activeUnit.UnitId);
        BattleUnitState primaryTarget = ResolveSingleCommandTarget(command);
        BattleLineThroughAttackPlan plan = BattleLineThroughAttackRules.BuildPlan(
            Runtime?._state,
            Runtime?.GetGridService(),
            Runtime?._layered_barrier_service,
            sourceUnit,
            primaryTarget,
            skillDefinition,
            Runtime?._movement_service?.IsMovementBlocked(sourceUnit) == true
        );
        foreach (Vector2I coord in plan.AnchorPath)
            preview.AddTargetCoord(coord);
        foreach (BattleUnitState target in plan.IntermediateTargets)
            preview.AddTargetUnitId(target.unit_id);
        if (plan.PrimaryTarget != null)
            preview.AddTargetUnitId(plan.PrimaryTarget.unit_id);
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
        CombatLineThroughAttackDefinition profile =
            skillDefinition.CombatProfile.LineThroughAttack;
        int skillLevel = activeUnit.GetKnownSkillLevel(skillDefinition.SkillId);
        int effectiveRange = skillDefinition.CombatProfile.GetEffectiveRangeValue(skillLevel);
        CombatSkillResourceCosts costs =
            skillDefinition.CombatProfile.GetEffectiveResourceCostValues(skillLevel);
        var stages = new List<AttackPreviewStage>();
        var hitRates = new List<int>();
        BattleAttackCheckPolicyService attackPolicy =
            Runtime?.GetAttackCheckPolicyService();
        BattleLayeredBarrierService layeredBarrierService =
            Runtime?._layered_barrier_service;
        BattleBarrierPreviewSession barrierSession =
            layeredBarrierService?.BeginSkillBarrierPreviewSession();
        foreach (BattleUnitState target in plan.IntermediateTargets)
        {
            BattleUnitReadView targetView = new(target);
            IReadOnlyList<CombatEffectDefinition> targetEffects =
                BuildLineThroughAttackEffects(
                    baseEffects,
                    profile.IntermediateWeaponDiceMultiplier
                );
            BattleBarrierInteractionResult barrierResult =
                layeredBarrierService?.PreviewSkillBarrierInteractionResult(
                    activeUnit,
                    targetView,
                    skillDefinition,
                    targetEffects,
                    barrierSession,
                    castVariantDefinition
                ) ?? new BattleBarrierInteractionResult(false, false);
            AttackPreviewData targetPreview =
                barrierResult.Blocked
                    ? BuildBlockedLineThroughAttackPreview(barrierResult.PreviewText)
                    : BuildLineThroughAttackHitPreview(
                        attackPolicy,
                        activeUnit,
                        targetView,
                        skillDefinition,
                        0
                    );
            if (barrierResult.Blocked && !string.IsNullOrEmpty(barrierResult.PreviewText))
                preview.AddLogLine(barrierResult.PreviewText);
            AttackPreviewStage stage = BuildLineThroughStage(
                targetPreview,
                profile.IntermediateWeaponDiceMultiplier,
                10000
            );
            stages.Add(stage);
            hitRates.Add(stage.HitRatePercent);
        }

        int successCap = profile.GetSuccessfulIntermediateHitBonusCap(skillLevel);
        double[] stateProbabilities = BuildCappedSuccessProbabilities(hitRates, successCap);
        double weightedPrimaryHitRate = 0.0;
        int weightedPrimaryBaseHitRate = 0;
        BattleUnitReadView primaryTargetView = new(plan.PrimaryTarget);
        IReadOnlyList<CombatEffectDefinition> maximumPrimaryEffects =
            BuildLineThroughAttackEffects(
                baseEffects,
                profile.GetPrimaryWeaponDiceMultiplier(skillLevel, successCap)
            );
        BattleBarrierInteractionResult primaryBarrierResult =
            layeredBarrierService?.PreviewSkillBarrierInteractionResult(
                activeUnit,
                primaryTargetView,
                skillDefinition,
                maximumPrimaryEffects,
                barrierSession,
                castVariantDefinition
            ) ?? new BattleBarrierInteractionResult(false, false);
        if (
            primaryBarrierResult.Blocked
            && !string.IsNullOrEmpty(primaryBarrierResult.PreviewText)
        )
        {
            preview.AddLogLine(primaryBarrierResult.PreviewText);
        }
        for (int successCount = 0; successCount <= successCap; successCount++)
        {
            int primaryBonus = profile.GetPrimaryAttackRollBonus(skillLevel, successCount);
            int primaryWeaponDice = profile.GetPrimaryWeaponDiceMultiplier(
                skillLevel,
                successCount
            );
            AttackPreviewData statePreview =
                primaryBarrierResult.Blocked
                    ? BuildBlockedLineThroughAttackPreview(
                        primaryBarrierResult.PreviewText
                    )
                    : BuildLineThroughAttackHitPreview(
                        attackPolicy,
                        activeUnit,
                        primaryTargetView,
                        skillDefinition,
                        primaryBonus
                    );
            int reachBasisPoints = Mathf.Clamp(
                Mathf.RoundToInt((float)(stateProbabilities[successCount] * 10000.0)),
                0,
                10000
            );
            AttackPreviewStage stateStage = BuildLineThroughStage(
                statePreview,
                primaryWeaponDice,
                reachBasisPoints
            );
            stages.Add(stateStage);
            weightedPrimaryHitRate +=
                stateProbabilities[successCount] * stateStage.HitRatePercent;
            weightedPrimaryBaseHitRate += Mathf.RoundToInt(
                (float)(stateProbabilities[successCount] * stateStage.BaseHitRatePercent)
            );
        }

        int aggregateHitRate = Mathf.Clamp(
            Mathf.RoundToInt((float)weightedPrimaryHitRate),
            0,
            100
        );
        preview.hit_preview = new AttackPreviewData
        {
            Source = "line_through_attack",
            SummaryText = $"终点主攻击预计命中率 {aggregateHitRate}%",
            Stages = stages,
            HitRatePercent = aggregateHitRate,
            SuccessRatePercent = aggregateHitRate,
            BaseHitRatePercent = Mathf.Clamp(weightedPrimaryBaseHitRate, 0, 100),
        };

        var potentialEffects = new List<CombatEffectDefinition>();
        foreach (BattleUnitState _ in plan.IntermediateTargets)
        {
            potentialEffects.AddRange(
                BuildLineThroughAttackEffects(
                    baseEffects,
                    profile.IntermediateWeaponDiceMultiplier
                )
            );
        }
        potentialEffects.AddRange(
            BuildLineThroughAttackEffects(
                baseEffects,
                profile.GetPrimaryWeaponDiceMultiplier(skillLevel, successCap)
            )
        );
        preview.SetDamagePreview(
            BattleDamagePreviewRangeService.BuildSkillDamagePreview(
                activeUnit,
                potentialEffects
            )
        );
        preview.SetSourceAdvancePath(plan.AnchorPath);
        preview.resolved_anchor_coord = plan.Destination;
        preview.move_cost = 0;
        preview.allowed = true;
        preview.AddLogLine(
            $"沿直线穿行 {plan.TravelDistance} 格，最多选择 {effectiveRange} 格内终点敌人；终点主攻击命中后落到其身后，位移不消耗移动力。"
        );
        preview.AddLogLine(
            $"途中 {plan.IntermediateTargets.Count} 名敌人各承受 {profile.IntermediateWeaponDiceMultiplier}W 独立标准武器攻击，未命中不会中止。"
        );
        preview.AddLogLine(
            $"终点基础 {profile.GetPrimaryWeaponDiceMultiplier(skillLevel)}W、攻击检定{FormatSignedBonus(profile.GetPrimaryAttackRollBonus(skillLevel))}；每个计入的途中成功命中再令终点 +{profile.SuccessfulIntermediateHitBonusWeaponDice}W、攻击检定+{profile.SuccessfulIntermediateHitAttackRollBonus}，本级最多计入 {successCap} 次。"
        );
        preview.AddLogLine(
            $"消耗 {costs.ApCost}AP/{costs.AuraCost}斗气，冷却 {costs.CooldownTu}TU；途中攻击不获得技能熟练度。"
        );
        preview.AddLogLine(preview.hit_preview.SummaryText);
        _append_damage_preview_line(preview);
        return true;
    }

    private bool _handle_line_through_attack_skill_command(
        BattleUnitState activeUnit,
        BattleCommand command,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        BattleEventBatch batch
    )
    {
        BattleUnitState primaryTarget = ResolveSingleCommandTarget(command);
        BattleLineThroughAttackPlan plan = BattleLineThroughAttackRules.BuildPlan(
            Runtime?._state,
            Runtime?.GetGridService(),
            Runtime?._layered_barrier_service,
            activeUnit,
            primaryTarget,
            skillDefinition,
            Runtime?._movement_service?.IsMovementBlocked(activeUnit) == true
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
        CombatLineThroughAttackDefinition profile =
            skillDefinition.CombatProfile.LineThroughAttack;
        int skillLevel = _get_unit_skill_level(activeUnit, skillDefinition.SkillId);
        int successfulIntermediateHits = 0;

        foreach (BattleUnitState intermediateTarget in plan.IntermediateTargets)
        {
            if (intermediateTarget?.IsAlive() != true)
                continue;
            bool attackSucceeded = false;
            IReadOnlyList<CombatEffectDefinition> intermediateEffects =
                BuildLineThroughAttackEffects(
                    baseEffects,
                    profile.IntermediateWeaponDiceMultiplier
                );
            batch?.AddLogLine(
                $"{intermediateTarget.display_name} 承受 {profile.IntermediateWeaponDiceMultiplier}W 途中武器攻击（攻击检定无额外修正）。"
            );
            _apply_unit_skill_result(
                activeUnit,
                intermediateTarget,
                skillDefinition,
                castVariantDefinition,
                intermediateEffects,
                batch,
                flat_attack_bonus: 0,
                resolution_sink: result => attackSucceeded = result.AttackSuccess,
                force_weapon_attack_resolution: true,
                record_skill_mastery: false
            );
            if (attackSucceeded)
                successfulIntermediateHits++;
            if (!activeUnit.IsAlive())
            {
                batch?.AddLogLine("施术者在途中攻击结算中倒下，终点主攻击与位移取消。");
                return true;
            }
        }

        if (plan.PrimaryTarget?.IsAlive() != true)
        {
            batch?.AddLogLine("终点目标在途中攻击结算中倒下，终点主攻击与位移取消。");
            return true;
        }

        int primaryWeaponDice = profile.GetPrimaryWeaponDiceMultiplier(
            skillLevel,
            successfulIntermediateHits
        );
        int primaryAttackBonus = profile.GetPrimaryAttackRollBonus(
            skillLevel,
            successfulIntermediateHits
        );
        bool primaryAttackSucceeded = false;
        IReadOnlyList<CombatEffectDefinition> primaryEffects =
            BuildLineThroughAttackEffects(baseEffects, primaryWeaponDice);
        batch?.AddLogLine(
            $"{plan.PrimaryTarget.display_name} 承受终点主攻击：{primaryWeaponDice}W，攻击检定{FormatSignedBonus(primaryAttackBonus)}；途中成功命中 {successfulIntermediateHits} 次。"
        );
        _apply_unit_skill_result(
            activeUnit,
            plan.PrimaryTarget,
            skillDefinition,
            castVariantDefinition,
            primaryEffects,
            batch,
            flat_attack_bonus: primaryAttackBonus,
            resolution_sink: result => primaryAttackSucceeded = result.AttackSuccess,
            force_weapon_attack_resolution: true
        );
        if (primaryAttackSucceeded)
        {
            Runtime?._movement_service?.ExecuteLineThroughAttackLanding(
                activeUnit,
                plan,
                skillDefinition,
                batch
            );
        }
        else
        {
            batch?.AddLogLine("终点主攻击未命中，敌后位移不发生。");
        }
        return true;
    }

    internal static IReadOnlyList<CombatEffectDefinition> BuildLineThroughAttackEffects(
        IEnumerable<CombatEffectDefinition> effects,
        int weaponDiceMultiplier
    )
    {
        var result = new List<CombatEffectDefinition>();
        foreach (
            CombatEffectDefinition effect in effects ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (effect == null)
                continue;
            result.Add(
                effect.EffectKind == BattleEffectKind.Damage
                    ? effect.WithWeaponDiceMultiplier(Math.Max(weaponDiceMultiplier, 1))
                    : effect
            );
        }
        return result.AsReadOnly();
    }

    private BattleUnitState ResolveSingleCommandTarget(BattleCommand command)
    {
        StringName targetUnitId = command?.target_unit_id ?? new StringName("");
        if (targetUnitId == "" && command?.TargetUnitIdsTyped?.Count == 1)
            targetUnitId = command.TargetUnitIdsTyped[0];
        return Runtime?._state?.GetAliveUnit(targetUnitId);
    }

    private static string FormatSignedBonus(int value) =>
        value > 0 ? $"+{value}" : value.ToString();

    private static AttackPreviewStage BuildLineThroughStage(
        AttackPreviewData preview,
        int weaponDiceMultiplier,
        int reachBasisPoints
    )
    {
        AttackPreviewStage sourceStage =
            preview?.Stages?.Count > 0
                ? preview.Stages[0]
                : new AttackPreviewStage(0, 0, 0, 0, 0, "0%");
        return new AttackPreviewStage(
            sourceStage.HitRatePercent,
            sourceStage.SuccessRatePercent,
            sourceStage.BaseHitRatePercent,
            sourceStage.RequiredRoll,
            sourceStage.DisplayRequiredRoll,
            sourceStage.PreviewText,
            reachBasisPoints,
            Math.Max(weaponDiceMultiplier, 1) * 100
        );
    }

    private AttackPreviewData BuildLineThroughAttackHitPreview(
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
                new StringName("line_through_attack_preview"),
                new StringName("hud_preview"),
                false
            );
        return attackPolicy.BuildAttackPreview(context, flatAttackBonus, 0);
    }

    private static AttackPreviewData BuildBlockedLineThroughAttackPreview(string reason)
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

    private static double[] BuildCappedSuccessProbabilities(
        IReadOnlyList<int> hitRates,
        int successCap
    )
    {
        int cap = Math.Max(successCap, 0);
        var probabilities = new double[cap + 1];
        probabilities[0] = 1.0;
        foreach (int rawHitRate in hitRates ?? Array.Empty<int>())
        {
            double hitRate = Mathf.Clamp(rawHitRate, 0, 100) / 100.0;
            var next = new double[cap + 1];
            for (int successCount = 0; successCount <= cap; successCount++)
            {
                next[successCount] += probabilities[successCount] * (1.0 - hitRate);
                next[Math.Min(successCount + 1, cap)] +=
                    probabilities[successCount] * hitRate;
            }
            probabilities = next;
        }
        return probabilities;
    }
}
