using static BattleSkillTargetPlanRules;
using static BattleGroundEffectClipRules;
using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

internal sealed partial class BattleSkillPreviewService
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

        BattleUnitState sourceUnit = Runtime?.GetStateForReadOnlyRules()?.GetAliveUnit(activeUnit.UnitId);
        BattleUnitState primaryTarget = ResolveSingleCommandTarget(command);
        BattleSequentialLineHitPlan plan = BattleSequentialLineHitRules.BuildPlan(
            Runtime?.GetStateForReadOnlyRules(),
            Runtime?.GetGridService(),
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
        BattleLayeredBarrierService layeredBarrierService = Runtime?.GetLayeredBarrierService();
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
            $"后续每段累计承受 -{followUpPenalty} 攻击检定；任一段落空，或遇到友军、战场边界或屏障时立即停止。"
        );
        preview.AddLogLine(
            $"消耗 {costs.ApCost}AP/{costs.MpCost}法力，冷却 {costs.CooldownTu}TU。{plan.StopMessage}"
        );
        preview.AddLogLine(preview.hit_preview.SummaryText);
        _append_damage_preview_line(preview);
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
                Runtime?.GetStateForReadOnlyRules(),
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

        BattleUnitState sourceUnit = Runtime?.GetStateForReadOnlyRules()?.GetAliveUnit(activeUnit.UnitId);
        BattleUnitState primaryTarget = ResolveSingleCommandTarget(command);
        BattleLineThroughAttackPlan plan = BattleLineThroughAttackRules.BuildPlan(
            Runtime?.GetStateForReadOnlyRules(),
            Runtime?.GetGridService(),
            sourceUnit,
            primaryTarget,
            skillDefinition,
            Runtime?.IsMovementBlocked(sourceUnit) == true
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
            Runtime?.GetLayeredBarrierService();
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
                Runtime?.GetStateForReadOnlyRules(),
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

    private BattleUnitState ResolveSingleCommandTarget(BattleCommand command)
    {
        StringName targetUnitId = command?.target_unit_id ?? new StringName("");
        if (targetUnitId == "" && command?.TargetUnitIdsTyped?.Count == 1)
            targetUnitId = command.TargetUnitIdsTyped[0];
        return Runtime?.GetStateForReadOnlyRules()?.GetAliveUnit(targetUnitId);
    }

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
            Runtime?.ValidateGroundSkillCommandResult(
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
        BattleUnitState sourceUnit = Runtime?.GetStateForReadOnlyRules()?.GetAliveUnit(activeUnit.UnitId);
        BattleDirectionalPiercingPlan plan = BattleDirectionalPiercingRules.BuildPlan(
            Runtime?.GetStateForReadOnlyRules(),
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
            Runtime?.CollectGroundUnitEffectDefinitions(
                skillDefinition,
                castVariantDefinition,
                activeUnit
            ) ?? Array.Empty<CombatEffectDefinition>();
        int skillLevel = activeUnit.GetKnownSkillLevel(skillDefinition.SkillId);
        int baseDamagePercent = skillDefinition.CombatProfile.DirectionalPiercing
            .GetBaseDamagePercent(skillLevel);
        IReadOnlyList<CombatEffectDefinition> firstTargetEffects =
            BattleDirectionalPiercingRules.BuildDirectionalPiercingEffects(baseEffects, baseDamagePercent / 100.0);
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
        if (preview.hit_preview != null && !preview.hit_preview.IsEmpty)
            preview.AddLogLine(preview.hit_preview.SummaryText);
        _append_damage_preview_line(preview);
        preview.allowed = true;
        return true;
    }

    private static string FormatSigned(int value) => value > 0 ? $"+{value}" : value.ToString();

    private static string FormatSkipReason(BattleDirectionalPiercingSkipReason reason) =>
        reason switch
        {
            BattleDirectionalPiercingSkipReason.HeightOutOfRange => "超过允许的高度差",
            BattleDirectionalPiercingSkipReason.OppositeHeightChannel => "与自动锁定的高度通道相反",
            _ => "不符合贯穿条件",
        };

    internal AttackPreviewData _build_unit_skill_hit_preview(
        BattleUnitReadView active_unit,
        IReadOnlyList<BattleUnitReadView> target_units,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition
    )
    {
        if (
            !active_unit.IsValid
            || skillDefinition == null
            || target_units == null
            || target_units.Count != 1
        )
        {
            return null;
        }
        BattleUnitReadView targetUnit = target_units[0];
        if (!targetUnit.IsValid)
        {
            return null;
        }
        List<CombatEffectDefinition> effectDefinitions = CollectUnitSkillEffectDefinitions(
            skillDefinition,
            castVariantDefinition,
            active_unit
        );
        BattleRepeatAttackResolver repeatAttackResolver = Runtime?.GetRepeatAttackResolver();
        CombatEffectDefinition repeatAttackEffect =
            repeatAttackResolver?.get_repeat_attack_effect_def(effectDefinitions);
        BattleAttackCheckPolicyService attackPolicy = Runtime?.GetAttackCheckPolicyService();
        BattleSkillResolutionRules skillResolutionRules = Runtime?.GetSkillResolutionRules();
        if (attackPolicy == null || skillResolutionRules == null)
        {
            return null;
        }
        if (repeatAttackEffect == null)
        {
            if (
                !skillResolutionRules.ShouldResolveUnitSkillAsFateAttack(
                    active_unit,
                    targetUnit,
                    skillDefinition,
                    effectDefinitions
                )
            )
            {
                return null;
            }
            BattleAttackCheckPolicyContext attackContext = attackPolicy.BuildSkillDefinitionAttackContext(
                Runtime?.GetStateForReadOnlyRules(),
                active_unit,
                targetUnit,
                skillDefinition,
                new StringName("skill_attack_preview"),
                new StringName("hud_preview"),
                skillResolutionRules.IsForceHitNoCritSkill(skillDefinition, active_unit)
            );
            return attackPolicy.BuildAttackPreview(attackContext);
        }
        List<BattleRepeatAttackStageSpec> stageSpecs =
            BattleRepeatAttackResolver.BuildStageSpecsFromRepeatAttackEffect(
            active_unit,
            skillDefinition,
            repeatAttackEffect,
            -1,
            true
        );
        BattleAttackCheckPolicyContext repeatContext = attackPolicy.BuildRepeatAttackStageContext(
            Runtime?.GetStateForReadOnlyRules(),
            active_unit,
            targetUnit,
            skillDefinition,
            default,
            new StringName("repeat_attack_preview"),
            new StringName("hud_preview")
        );
        return attackPolicy.BuildRepeatAttackPreview(repeatContext, stageSpecs);
    }

    internal void _append_damage_preview_line(BattlePreview preview)
    {
        if (preview == null || preview.DamagePreviewTyped == null)
        {
            return;
        }
        string damagePreviewText = preview.DamagePreviewTyped.Value.SummaryText;
        if (string.IsNullOrEmpty(damagePreviewText))
        {
            return;
        }
        preview.AddLogLine(damagePreviewText);
    }

    internal List<CombatEffectDefinition> CollectUnitSkillEffectDefinitions(
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant,
        BattleUnitReadView active_unit
    )
    {
        return Runtime?.GetSkillResolutionRules() != null
            ? Runtime.GetSkillResolutionRules().CollectUnitSkillEffectDefinitions(
                skillDefinition,
                castVariant,
                active_unit
            )
            : new List<CombatEffectDefinition>();
    }

    internal IReadOnlyList<BattleUnitReadView> CollectUnitsInCoordsReadView(
        IReadOnlyList<Vector2I> effectCoords
    )
    {
        var units = new List<BattleUnitReadView>();
        foreach (BattleUnitState unitState in CollectUnitsInCoords(effectCoords))
        {
            units.Add(new BattleUnitReadView(unitState));
        }
        return units;
    }

    private BattleState RtState() => Runtime?.GetStateForReadOnlyRules();

    private IReadOnlyList<BattleUnitState> CollectUnitsInCoords(IReadOnlyList<Vector2I> coords)
    {
        var units = new List<BattleUnitState>();
        var seen = new HashSet<StringName>();
        foreach (Vector2I coord in coords ?? Array.Empty<Vector2I>())
        {
            BattleUnitState unit = Runtime?.GetGridService()?.GetUnitAtCoord(RtState(), coord);
            if (unit != null && unit.IsAlive() && seen.Add(unit.unit_id)) units.Add(unit);
        }
        return units;
    }

    internal BattleGroundEffectBarrierClipContext PreviewGroundEffectBarrierClipContext(
        BattleUnitReadView activeUnit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        IReadOnlyList<Vector2I> targetCoords,
        IReadOnlyList<Vector2I> rawEffectCoords = null
    )
    {
        IReadOnlyList<CombatEffectDefinition> unitEffectDefinitions =
            Runtime?.CollectGroundUnitEffectDefinitions(
                skillDefinition,
                castVariantDefinition,
                activeUnit
            ) ?? Array.Empty<CombatEffectDefinition>();
        IReadOnlyList<CombatEffectDefinition> terrainEffectDefinitions =
            Runtime?.CollectGroundTerrainEffectDefinitions(
                skillDefinition,
                castVariantDefinition,
                activeUnit
            ) ?? Array.Empty<CombatEffectDefinition>();
        IReadOnlyList<Vector2I> normalizedRawEffectCoords =
            rawEffectCoords
            ?? Runtime?.BuildGroundEffectCoords(
                skillDefinition,
                targetCoords ?? Array.Empty<Vector2I>(),
                activeUnit.IsValid ? activeUnit.Coord : new Vector2I(-1, -1),
                activeUnit,
                castVariantDefinition
            )
            ?? Array.Empty<Vector2I>();
        BattleGroundEffectBarrierClipResult clipResult = Runtime?.GetLayeredBarrierService()
            ?.PreviewGroundEffectBarrierClipResult(
                activeUnit,
                skillDefinition,
                unitEffectDefinitions,
                terrainEffectDefinitions,
                normalizedRawEffectCoords,
                castVariantDefinition
            ) ?? BuildUnclippedGroundEffectBarrierResult(
                unitEffectDefinitions,
                terrainEffectDefinitions,
                normalizedRawEffectCoords
            );
        return BuildGroundEffectBarrierClipContext(
            unitEffectDefinitions,
            terrainEffectDefinitions,
            normalizedRawEffectCoords,
            clipResult
        );
    }
}
