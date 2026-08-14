using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

internal sealed class BattleSkillPreviewService
{

    private WeakReference<IBattleSkillPreviewRuntimePort> _runtimeRef;
    private BattleSkillExecutionOrchestrator _owner;
    private BattleSkillTargetValidationService _targetValidationService;

    private IBattleSkillPreviewRuntimePort _runtime
    {
        get =>
            _runtimeRef != null
            && _runtimeRef.TryGetTarget(out IBattleSkillPreviewRuntimePort runtime)
                ? runtime
                : null;
        set =>
            _runtimeRef =
                value != null ? new WeakReference<IBattleSkillPreviewRuntimePort>(value) : null;
    }

    private IBattleSkillPreviewRuntimePort Runtime => _runtime;

    internal void Setup(
        IBattleSkillPreviewRuntimePort runtime,
        BattleSkillExecutionOrchestrator owner,
        BattleSkillTargetValidationService targetValidationService
    )
    {
        _runtime = runtime;
        _owner = owner;
        _targetValidationService = targetValidationService;
    }

    internal void DisposeRuntime()
    {
        _runtime = null;
        _owner = null;
        _targetValidationService = null;
    }

    internal void _preview_skill_command(
        BattleUnitReadView active_unit,
        BattleCommand command,
        BattlePreview preview
    )
    {
        using BattleAiTraceSpan trace = new("preview:skill.orchestrator");
        _preview_skill_command_impl(active_unit, command, preview);
    }

    internal void _preview_skill_command_impl(
        BattleUnitReadView active_unit,
        BattleCommand command,
        BattlePreview preview
    )
    {
        SkillDefinition skillDefinition = Runtime?.GetSkillDefinition(command.skill_id);
        if (skillDefinition?.CombatProfile == null)
        {
            preview.AddLogLine("技能或目标无效。");
            return;
        }
        IBattleSkillPreviewRuntimePort runtime = _runtime;
        bool isMeteorSwarm =
            skillDefinition.CombatProfile.SpecialResolutionProfileId
            == new StringName("meteor_swarm");
        if (runtime != null && isMeteorSwarm)
        {
            BattleSpecialProfileGateResult gateResult;
            using (new BattleAiTraceSpan("preview:skill.meteor_gate"))
            {
                gateResult = runtime.PreviewSpecialProfileSkill(
                    skillDefinition,
                    command,
                    active_unit
                );
            }
            preview.special_profile_gate_result = gateResult;
            if (gateResult == null || !gateResult.Allowed)
            {
                if (
                    gateResult != null
                    && !string.IsNullOrEmpty(gateResult.PlayerMessage)
                )
                {
                    preview.AddLogLine(gateResult.PlayerMessage);
                }
                else
                {
                    preview.AddLogLine("该禁咒配置未通过校验，暂时无法施放。");
                }
                return;
            }
            string blockReason = _owner._get_skill_command_block_reason(active_unit, skillDefinition, null);
            if (!string.IsNullOrEmpty(blockReason))
            {
                preview.AddLogLine(blockReason);
                return;
            }
            if (runtime.PopulateMeteorSwarmPreview(active_unit, command, skillDefinition, preview))
            {
                return;
            }
            preview.allowed = false;
            preview.AddLogLine("该禁咒结算尚未接入。");
            return;
        }
        BattleSkillResolutionPolicy policy;
        bool routesToUnitTargeting;
        using (new BattleAiTraceSpan("preview:skill.resolve_options"))
        {
            bool allowRepeat = skillDefinition.CombatProfile.AllowRepeatTarget;
            policy = Runtime?.GetSkillResolutionRules()
                ?.BuildSkillResolutionPolicy(
                    skillDefinition,
                    active_unit,
                    command != null ? command.skill_variant_id : new StringName(""),
                    _targetValidationService._normalize_target_unit_ids(command, allowRepeat)
                );
            routesToUnitTargeting = policy?.RoutesToUnitTargeting == true;
        }
        string optionBlockReason;
        using (new BattleAiTraceSpan("preview:skill.option_block"))
            optionBlockReason = policy?.OptionErrorMessage ?? "技能或目标无效。";
        if (!string.IsNullOrEmpty(optionBlockReason))
        {
            preview.AddLogLine(optionBlockReason);
            return;
        }

        if (routesToUnitTargeting)
        {
            _preview_unit_skill_command(
                active_unit,
                command,
                skillDefinition,
                policy?.UnitExecutionCastVariantDefinition,
                preview
            );
            return;
        }

        bool routesToGroundTargeting = !routesToUnitTargeting
            && policy?.GroundCastVariantDefinition != null;
        if (routesToGroundTargeting)
        {
            _preview_ground_skill_command(
                active_unit,
                command,
                skillDefinition,
                policy.GroundCastVariantDefinition,
                preview
            );
            return;
        }

        preview.AddLogLine("技能或目标无效。");
    }

    internal void _preview_unit_skill_command(
        BattleUnitReadView active_unit,
        BattleCommand command,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        BattlePreview preview
    )
    {
        using BattleAiTraceSpan trace = new("preview:unit_skill");
        _preview_unit_skill_command_impl(
            active_unit,
            command,
            preview,
            skillDefinition,
            castVariantDefinition
        );
    }

    internal void _preview_unit_skill_command_impl(
        BattleUnitReadView active_unit,
        BattleCommand command,
        BattlePreview preview,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition
    )
    {
        if (preview == null)
        {
            return;
        }
        preview.ClearSourceRetreatPath();
        preview.ClearSourceAdvancePath();
        preview.ClearForcedMovePreview();
        preview.ClearPositionSwapPreview();
        preview.ClearSaveBranchPreview();
        preview.ClearShieldPreview();
        preview.ClearEquipmentDurabilityPreview();
        preview.ClearStatusContributionPreviews();
        castVariantDefinition ??= Runtime?.GetSkillResolutionRules()
            ?.ResolveUnitCastVariantDefinition(
                skillDefinition,
                active_unit,
                command != null ? command.skill_variant_id : new StringName("")
            );
        string blockReason = _owner._get_skill_command_block_reason(
            active_unit,
            skillDefinition,
            castVariantDefinition
        );
        if (!string.IsNullOrEmpty(blockReason))
        {
            preview.AddLogLine(blockReason);
            return;
        }
        if (
            _owner.TryPreviewSequentialLineHitSkill(
                active_unit,
                command,
                skillDefinition,
                castVariantDefinition,
                preview
            )
        )
        {
            return;
        }
        if (
            _owner.TryPreviewLineThroughAttackSkill(
                active_unit,
                command,
                skillDefinition,
                castVariantDefinition,
                preview
            )
        )
        {
            return;
        }
        BattleWindupQuote? windupQuote = null;
        if (skillDefinition?.CombatProfile?.Windup != null)
        {
            if (
                !BattleWindupRules.TryBuildQuote(
                    active_unit,
                    skillDefinition,
                    command?.windup_tier ?? 0,
                    out BattleWindupQuote quote,
                    out string windupBlockReason
                )
            )
            {
                preview.AddLogLine(windupBlockReason);
                return;
            }
            windupQuote = quote;
        }

        BattleUnitSkillPreviewValidationResult validation;
        using (new BattleAiTraceSpan("preview:unit_skill.validate_targets"))
        {
            validation = _targetValidationService._validate_unit_skill_preview_targets_result(
                active_unit,
                command,
                skillDefinition,
                castVariantDefinition
            );
        }
        if (
            validation.Allowed
            && BattleTargetSlotCostRules.UsesOrderedTargetSlots(skillDefinition)
        )
        {
            string targetSlotCostBlockReason =
                _owner._get_target_slot_cost_block_reason(
                    active_unit,
                    skillDefinition,
                    validation.TargetUnits.Count
                );
            if (!string.IsNullOrEmpty(targetSlotCostBlockReason))
            {
                preview.allowed = false;
                preview.AddLogLine(targetSlotCostBlockReason);
                return;
            }
        }
        var previewTargetUnits = new List<BattleUnitReadView>();
        var previewTargetUnitIds = new List<StringName>();
        var barrierBlockLines = new List<string>();
        IReadOnlyList<CombatEffectDefinition> previewEffectDefinitions =
            Array.Empty<CombatEffectDefinition>();
        bool hasDeterministicTargets = validation.TargetUnits.Count > 0;
        bool isRandomChain =
            skillDefinition?.CombatProfile?.TargetSelectionModeKind
            == BattleTargetSelectionMode.RandomChain;
        bool hasRandomChainCandidates =
            isRandomChain && validation.RandomChainCandidateUnitIds.Count > 0;
        if (validation.Allowed && (hasDeterministicTargets || hasRandomChainCandidates))
        {
            previewEffectDefinitions = _owner.CollectUnitSkillEffectDefinitions(
                    skillDefinition,
                    castVariantDefinition,
                    active_unit
                );
            if (windupQuote is BattleWindupQuote resolvedWindupQuote)
            {
                previewEffectDefinitions = BattleWindupRules.ApplyWeaponDiceMultiplier(
                    previewEffectDefinitions,
                    resolvedWindupQuote.WeaponDiceMultiplier
                );
            }
            BattleLayeredBarrierService layeredBarrierService =
                Runtime?.GetLayeredBarrierService();
            if (hasDeterministicTargets)
            {
                BattleBarrierPreviewSession barrierPreviewSession =
                    layeredBarrierService?.BeginSkillBarrierPreviewSession();
                if (BattleTargetSlotCostRules.UsesOrderedTargetSlots(skillDefinition))
                {
                    foreach (BattleUnitReadView targetUnit in validation.TargetUnits)
                    {
                        IReadOnlyDictionary<
                            CombatEffectDefinition,
                            IReadOnlyList<BattleUnitReadView>
                        > targetPlan = _owner.BuildUnitEffectTargetPlan(
                            active_unit,
                            skillDefinition,
                            previewEffectDefinitions,
                            new[] { targetUnit }
                        );
                        IReadOnlyList<CombatEffectDefinition> targetEffects =
                            BattleSkillExecutionOrchestrator.CollectPlannedEffectsForTarget(
                                previewEffectDefinitions,
                                targetPlan,
                                targetUnit.UnitId
                            );
                        if (targetEffects.Count == 0)
                            continue;
                        BattleBarrierInteractionResult barrierResult =
                            layeredBarrierService?.PreviewSkillBarrierInteractionResult(
                                active_unit,
                                targetUnit,
                                skillDefinition,
                                targetEffects,
                                barrierPreviewSession,
                                castVariantDefinition
                            ) ?? new BattleBarrierInteractionResult(false, false);
                        if (barrierResult.Blocked)
                        {
                            if (!string.IsNullOrEmpty(barrierResult.PreviewText))
                                barrierBlockLines.Add(barrierResult.PreviewText);
                            continue;
                        }
                        previewTargetUnits.Add(targetUnit);
                        previewTargetUnitIds.Add(targetUnit.UnitId);
                    }
                }
                else
                {
                    IReadOnlyDictionary<
                        CombatEffectDefinition,
                        IReadOnlyList<BattleUnitReadView>
                    > targetPlan = _owner.BuildUnitEffectTargetPlan(
                        active_unit,
                        skillDefinition,
                        previewEffectDefinitions,
                        validation.TargetUnits
                    );
                    IReadOnlyList<BattleUnitReadView> plannedTargets =
                        BattleSkillExecutionOrchestrator.CollectPlannedTargets(
                            previewEffectDefinitions,
                            targetPlan
                        );
                    foreach (BattleUnitReadView targetUnit in plannedTargets)
                    {
                        IReadOnlyList<CombatEffectDefinition> targetEffects =
                            BattleSkillExecutionOrchestrator.CollectPlannedEffectsForTarget(
                                previewEffectDefinitions,
                                targetPlan,
                                targetUnit.UnitId
                            );
                        BattleBarrierInteractionResult barrierResult =
                            layeredBarrierService?.PreviewSkillBarrierInteractionResult(
                                active_unit,
                                targetUnit,
                                skillDefinition,
                                targetEffects,
                                barrierPreviewSession,
                                castVariantDefinition
                            ) ?? new BattleBarrierInteractionResult(false, false);
                        if (barrierResult.Blocked)
                        {
                            if (!string.IsNullOrEmpty(barrierResult.PreviewText))
                                barrierBlockLines.Add(barrierResult.PreviewText);
                            continue;
                        }
                        previewTargetUnits.Add(targetUnit);
                        previewTargetUnitIds.Add(targetUnit.UnitId);
                    }
                }
            }
            else
            {
                BattleState state = _owner.RtState();
                BattleStateReadView stateView =
                    state != null ? state.AsReadView() : default;
                var breakerBlockedTargetUnits = new List<BattleUnitReadView>();
                foreach (StringName candidateUnitId in validation.RandomChainCandidateUnitIds)
                {
                    BattleUnitReadView targetUnit = stateView.GetUnit(candidateUnitId);
                    if (!targetUnit.IsValid)
                        continue;
                    BattleBarrierInteractionResult barrierResult =
                        layeredBarrierService?.PreviewSkillBarrierInteractionResult(
                            active_unit,
                            targetUnit,
                            skillDefinition,
                            previewEffectDefinitions,
                            castVariantDefinition: castVariantDefinition
                        ) ?? new BattleBarrierInteractionResult(false, false);
                    if (barrierResult.Blocked)
                    {
                        if (!string.IsNullOrEmpty(barrierResult.PreviewText))
                            barrierBlockLines.Add(barrierResult.PreviewText);
                        if (barrierResult.WouldBreakLayer)
                            breakerBlockedTargetUnits.Add(targetUnit);
                        continue;
                    }
                    previewTargetUnits.Add(targetUnit);
                    previewTargetUnitIds.Add(targetUnit.UnitId);
                }
                int maxHitsPerTarget = Math.Max(
                    skillDefinition?.CombatProfile?.MaxHitsPerTarget ?? 0,
                    1
                );
                foreach (BattleUnitReadView targetUnit in breakerBlockedTargetUnits)
                {
                    bool canBeAffectedAfterBreaker = false;
                    foreach (BattleUnitReadView breakerTarget in breakerBlockedTargetUnits)
                    {
                        if (
                            breakerTarget.UnitId == targetUnit.UnitId
                            && maxHitsPerTarget <= 1
                        )
                        {
                            continue;
                        }
                        BattleBarrierPreviewSession barrierPreviewSession =
                            layeredBarrierService?.BeginSkillBarrierPreviewSession();
                        BattleBarrierInteractionResult breakerResult =
                            layeredBarrierService?.PreviewSkillBarrierInteractionResult(
                                active_unit,
                                breakerTarget,
                                skillDefinition,
                                previewEffectDefinitions,
                                barrierPreviewSession,
                                castVariantDefinition
                            ) ?? new BattleBarrierInteractionResult(false, false);
                        if (!breakerResult.WouldBreakLayer)
                            continue;
                        BattleBarrierInteractionResult followUpResult =
                            layeredBarrierService?.PreviewSkillBarrierInteractionResult(
                                active_unit,
                                targetUnit,
                                skillDefinition,
                                previewEffectDefinitions,
                                barrierPreviewSession,
                                castVariantDefinition
                            ) ?? new BattleBarrierInteractionResult(false, false);
                        if (followUpResult.Blocked)
                            continue;
                        canBeAffectedAfterBreaker = true;
                        break;
                    }
                    if (!canBeAffectedAfterBreaker)
                        continue;
                    previewTargetUnits.Add(targetUnit);
                    previewTargetUnitIds.Add(targetUnit.UnitId);
                }
            }
        }
        bool hasPreviewImpactTargets = previewTargetUnits.Count > 0;
        IReadOnlyList<Vector2I> previewCoords = validation.PreviewCoords;
        if (
            hasDeterministicTargets
            && previewTargetUnits.Count != validation.TargetUnits.Count
        )
        {
            previewCoords = _collect_unit_skill_preview_coords(
                active_unit,
                skillDefinition,
                previewTargetUnits
            );
        }
        using (new BattleAiTraceSpan("preview:unit_skill.copy_validation"))
        {
            preview.allowed = validation.Allowed;
            preview.SetTargetUnitIds(
                isRandomChain ? Array.Empty<StringName>() : previewTargetUnitIds
            );
            preview.SetRandomChainCandidateUnitIds(validation.RandomChainCandidateUnitIds);
            preview.SetRandomChainImpactCandidateUnitIds(
                isRandomChain ? previewTargetUnitIds : Array.Empty<StringName>()
            );
            preview.ClearTargetCoords();
            foreach (Vector2I previewCoord in previewCoords)
                preview.AddTargetCoord(previewCoord);
        }
        if (
            preview.allowed
            && previewTargetUnits.Count == 1
        )
        {
            AppendPositionSwapPreview(
                preview,
                active_unit,
                previewTargetUnits[0],
                skillDefinition,
                castVariantDefinition
            );
            AppendSourceRetreatPreview(
                preview,
                active_unit,
                previewTargetUnits[0],
                command,
                skillDefinition,
                castVariantDefinition
            );
            AppendAirbornePullPreview(
                preview,
                active_unit,
                previewTargetUnits[0],
                command,
                skillDefinition,
                castVariantDefinition
            );
            AppendApproachAttackPreview(
                preview,
                active_unit,
                previewTargetUnits[0],
                skillDefinition
            );
        }
        if (preview.allowed)
        {
            preview.hit_preview = null;
            preview.ClearDamagePreview();
            preview.ClearSaveBranchPreview();
            if (hasPreviewImpactTargets)
            {
                using (new BattleAiTraceSpan("preview:unit_skill.hit_preview"))
                {
                    IReadOnlyList<BattleUnitReadView> hitPreviewTargets =
                        BattleTargetSlotCostRules.UsesOrderedTargetSlots(
                            skillDefinition
                        )
                        && previewTargetUnits.Count > 0
                            ? new[] { previewTargetUnits[0] }
                            : previewTargetUnits;
                    preview.hit_preview = _owner._build_unit_skill_hit_preview(
                        active_unit,
                        hitPreviewTargets,
                        skillDefinition,
                        castVariantDefinition
                    );
                }
                using (new BattleAiTraceSpan("preview:unit_skill.damage_preview"))
                {
                    preview.SetDamagePreview(
                        BuildUnitSkillDamagePreviewTyped(
                            active_unit,
                            skillDefinition,
                            castVariantDefinition,
                            windupQuote
                        )
                    );
                    preview.SetSaveBranchPreview(
                        BuildUnitSkillSaveBranchPreview(
                            active_unit,
                            previewTargetUnits,
                            skillDefinition,
                            castVariantDefinition
                        )
                    );
                }
            }
            preview.SetShieldPreview(
                BuildShieldPreviewTyped(
                    active_unit,
                    skillDefinition,
                    previewEffectDefinitions,
                    previewTargetUnitIds
                )
            );
            preview.SetEquipmentDurabilityPreview(
                BuildEquipmentDurabilityPreviewTyped(
                    active_unit,
                    skillDefinition,
                    previewEffectDefinitions,
                    previewTargetUnitIds
                )
            );
            preview.SetRangedWeaponReactionPreview(
                BattleRangedWeaponReactionPreviewBuilder.Build(
                    active_unit,
                    skillDefinition,
                    previewEffectDefinitions
                )
            );
            foreach (
                BattleStatusContributionPreviewData statusPreview
                in BuildStatusContributionPreviewsTyped(
                    active_unit,
                    skillDefinition,
                    previewEffectDefinitions,
                    previewTargetUnitIds
                )
            )
            {
                preview.AddStatusContributionPreview(statusPreview);
            }
            using BattleAiTraceSpan logLinesTrace = new("preview:unit_skill.log_lines");
            string skillLabel = _owner._format_skill_variant_label(skillDefinition, castVariantDefinition);
            foreach (string barrierBlockLine in barrierBlockLines)
                preview.AddLogLine(barrierBlockLine);
            if (windupQuote is BattleWindupQuote quotedWindup)
            {
                preview.AddLogLine(
                    $"蓄力 {quotedWindup.Tier} 挡：{quotedWindup.TotalWindupTu} TU，{quotedWindup.TotalStaminaCost} 体力，伤害 {quotedWindup.WeaponDiceMultiplier}W；开始后不能主动取消。"
                );
            }
            if (preview.ShieldPreviewTyped is BattleShieldPreviewData shieldPreview)
            {
                preview.AddLogLine(shieldPreview.SummaryText);
            }
            if (
                preview.PositionSwapPreviewTyped
                is BattlePositionSwapPreviewData positionSwapPreview
            )
            {
                preview.AddLogLine(positionSwapPreview.SummaryText);
            }
            if (
                preview.EquipmentDurabilityPreviewTyped
                is BattleEquipmentDurabilityPreviewData durabilityPreview
            )
            {
                preview.AddLogLine(durabilityPreview.SummaryText);
            }
            if (
                preview.RangedWeaponReactionPreviewTyped
                is BattleRangedWeaponReactionPreviewData rangedWeaponReactionPreview
            )
            {
                preview.AddLogLine(rangedWeaponReactionPreview.SummaryText);
            }
            foreach (
                BattleStatusContributionPreviewData statusPreview
                in preview.StatusContributionPreviewsTyped
            )
            {
                preview.AddLogLine(statusPreview.SummaryText);
            }
            if (BattleTargetSlotCostRules.UsesOrderedTargetSlots(skillDefinition))
            {
                int targetSlotCount = validation.TargetUnits.Count;
                int skillLevel = active_unit.GetKnownSkillLevel(skillDefinition.SkillId);
                CombatSkillResourceCosts selectedCosts =
                    _owner._get_effective_skill_resource_costs(
                        active_unit,
                        skillDefinition,
                        targetSlotCount
                    );
                int mpPerSlot = Math.Max(
                    skillDefinition.CombatProfile.GetEffectiveMpCostPerTargetSlot(
                        skillLevel
                    ),
                    0
                );
                int staminaPerSlot = Math.Max(
                    skillDefinition.CombatProfile.GetEffectiveStaminaCostPerTargetSlot(
                        skillLevel
                    ),
                    0
                );
                preview.AddLogLine(
                    $"本次编排 {targetSlotCount} 发：单发 {mpPerSlot} 法力/{staminaPerSlot} 体力，合计 {selectedCosts.MpCost} 法力/{selectedCosts.StaminaCost} 体力。"
                );
            }
            if (isRandomChain)
            {
                int configuredAttackCount =
                    skillDefinition?.CombatProfile?.GetEffectiveRandomChainAttackCount(
                        active_unit.GetKnownSkillLevel(skillDefinition.SkillId)
                    ) ?? 0;
                string attackCountText =
                    configuredAttackCount > 0
                        ? $"，将执行 {configuredAttackCount} 次独立攻击"
                        : "";
                preview.AddLogLine(
                    $"{active_unit.DisplayName} 可用 {skillLabel} 从 {preview.RandomChainCandidateUnitIdsTyped.Count} 个候选单位中随机连击{attackCountText}；按当前屏障状态，其中 {previewTargetUnits.Count} 个单位可受到影响。"
                );
                if (previewTargetUnits.Count == 0)
                {
                    preview.AddLogLine("本次随机连击没有单位会受到影响。");
                }
                else
                {
                    _owner._append_damage_preview_line(preview);
                }
                return;
            }
            if (previewTargetUnits.Count == 0)
            {
                preview.AddLogLine(
                    $"{active_unit.DisplayName} 仍可使用 {skillLabel}，但本次没有单位会受到影响。"
                );
                return;
            }
            if (previewTargetUnits.Count == 1)
            {
                BattleUnitReadView targetUnit = previewTargetUnits[0];
                if (targetUnit.IsValid)
                {
                    preview.AddLogLine(
                        $"{active_unit.DisplayName} 可对 {targetUnit.DisplayName} 使用 {skillLabel}。"
                    );
                    if (preview.hit_preview != null && !preview.hit_preview.IsEmpty)
                    {
                        preview.AddLogLine(preview.hit_preview.SummaryText);
                    }
                    _owner._append_damage_preview_line(preview);
                    return;
                }
            }
            preview.AddLogLine(
                $"{active_unit.DisplayName} 可对 {preview.TargetUnitIdsTyped.Count} 个单位使用 {skillLabel}。"
            );
            if (preview.hit_preview != null && !preview.hit_preview.IsEmpty)
            {
                preview.AddLogLine(preview.hit_preview.SummaryText);
            }
            _owner._append_damage_preview_line(preview);
            return;
        }
        preview.AddLogLine(
            string.IsNullOrEmpty(validation.Message) ? "技能或目标无效。" : validation.Message
        );
    }

    private void AppendPositionSwapPreview(
        BattlePreview preview,
        BattleUnitReadView sourceUnit,
        BattleUnitReadView targetUnit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition
    )
    {
        IReadOnlyList<CombatEffectDefinition> effectDefinitions =
            _owner.CollectUnitSkillEffectDefinitions(
                skillDefinition,
                castVariantDefinition,
                sourceUnit
            );
        CombatEffectDefinition effect = BattlePositionSwapRules.FindEffect(
            effectDefinitions
        );
        if (effect == null)
            return;
        BattlePositionSwapPlan plan = BattlePositionSwapRules.BuildPlan(
            _owner.RtState(),
            Runtime?.GetGridService(),
            Runtime?.GetLayeredBarrierService(),
            sourceUnit,
            targetUnit
        );
        if (!plan.Allowed)
            return;

        BattleSaveProbabilityResult probability = plan.RequiresEnemySave
            ? BattleSaveResolver.EstimateSaveSuccessProbabilityResult(
                sourceUnit.UnsafeUnitForReadOnlyRules,
                targetUnit.UnsafeUnitForReadOnlyRules,
                effect,
                BattleSaveContext.ForSkill(skillDefinition.SkillId)
            )
            : BattleSaveProbabilityResult.Empty("");
        int swapProbabilityBasisPoints =
            plan.RequiresEnemySave
                ? probability.FailureProbabilityBasisPoints
                : 10000;
        string saveText = plan.RequiresEnemySave
            ? $"；意志豁免成功率 {probability.SuccessProbabilityBasisPoints / 100.0:0.#}%"
            : "；友军自愿换位，无需豁免";
        preview.SetPositionSwapPreview(
            new BattlePositionSwapPreviewData
            {
                SourceUnitId = sourceUnit.UnitId,
                TargetUnitId = targetUnit.UnitId,
                SourceFrom = plan.SourceFrom,
                SourceTo = plan.SourceTo,
                TargetFrom = plan.TargetFrom,
                TargetTo = plan.TargetTo,
                RequiresEnemySave = plan.RequiresEnemySave,
                SaveDc = plan.RequiresEnemySave ? probability.Dc : 0,
                SaveAbility = plan.RequiresEnemySave ? probability.Ability : "",
                SaveTag = plan.RequiresEnemySave ? probability.SaveTag : "",
                SaveSuccessProbabilityBasisPoints =
                    plan.RequiresEnemySave
                        ? probability.SuccessProbabilityBasisPoints
                        : 0,
                SwapProbabilityBasisPoints = swapProbabilityBasisPoints,
                SummaryText =
                    $"交换位置：施法者 ({plan.SourceFrom.X}, {plan.SourceFrom.Y}) → ({plan.SourceTo.X}, {plan.SourceTo.Y})，目标 ({plan.TargetFrom.X}, {plan.TargetFrom.Y}) → ({plan.TargetTo.X}, {plan.TargetTo.Y}){saveText}。",
            }
        );
        preview.resolved_anchor_coord = plan.SourceTo;
        preview.move_cost = 0;
    }

    private void AppendSourceRetreatPreview(
        BattlePreview preview,
        BattleUnitReadView sourceUnit,
        BattleUnitReadView targetUnit,
        BattleCommand command,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition
    )
    {
        IReadOnlyList<CombatEffectDefinition> effectDefinitions =
            _owner.CollectUnitSkillEffectDefinitions(
                skillDefinition,
                castVariantDefinition,
                sourceUnit
            );
        CombatEffectDefinition sourceRetreatEffect =
            BattleSourceRetreatRules.FindEffect(effectDefinitions);
        if (sourceRetreatEffect == null)
            return;

        BattleSourceRetreatPlan plan = Runtime?.BuildSourceRetreatPlan(
            sourceUnit,
            targetUnit.Coord,
            command?.source_retreat_direction ?? Vector2I.Zero,
            sourceRetreatEffect.SourceRetreatDistance
        );
        if (plan?.Allowed != true)
            return;

        preview.SetSourceRetreatPath(plan.Path);
        preview.resolved_anchor_coord = plan.FinalCoord;
        preview.move_cost = 0;
        preview.AddLogLine(
            plan.ReachableDistance < plan.RequestedDistance
                ? $"预计沿选定方向后撤 {plan.ReachableDistance} 格，并在阻挡前停下。"
                : $"预计沿选定方向后撤 {plan.ReachableDistance} 格。"
        );
    }

    private void AppendApproachAttackPreview(
        BattlePreview preview,
        BattleUnitReadView sourceUnit,
        BattleUnitReadView targetUnit,
        SkillDefinition skillDefinition
    )
    {
        if (!BattleApproachAttackRules.IsApproachAttackSkill(skillDefinition))
            return;

        BattleApproachAttackPlan plan = Runtime?.BuildApproachAttackPlan(sourceUnit, targetUnit, skillDefinition);
        if (plan?.Allowed != true)
            return;

        preview.SetSourceAdvancePath(plan.Path);
        preview.resolved_anchor_coord = plan.FinalCoord;
        preview.move_cost = 0;
        preview.AddLogLine(
            $"预计沿直线推进 {plan.AdvanceDistance} 格至武器射程内；所有路径格均须与起始格同高，且不消耗移动力。"
        );
    }

    private void AppendAirbornePullPreview(
        BattlePreview preview,
        BattleUnitReadView sourceUnit,
        BattleUnitReadView targetUnit,
        BattleCommand command,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition
    )
    {
        IReadOnlyList<CombatEffectDefinition> effectDefinitions =
            _owner.CollectUnitSkillEffectDefinitions(
                skillDefinition,
                castVariantDefinition,
                sourceUnit
            );
        CombatEffectDefinition effect = BattleAirbornePullRules.FindEffect(
            effectDefinitions
        );
        if (effect == null)
            return;
        BattleAirbornePullPlan plan = BattleAirbornePullRules.BuildPlan(
            _owner.RtState(),
            Runtime?.GetGridService(),
            Runtime?.GetLayeredBarrierService(),
            sourceUnit,
            targetUnit,
            effect,
            command?.forced_move_destination_coord ?? new Vector2I(-1, -1)
        );
        if (!plan.Allowed)
            return;
        preview.SetForcedMovePreview(
            new BattleForcedMovePreviewData
            {
                Mode = BattleTypedNames.ToStringName(BattleForcedMoveMode.AirbornePull),
                TargetUnitId = targetUnit.UnitId,
                SourceCoord = plan.SourceCoord,
                DestinationCoord = plan.DestinationCoord,
                Distance = plan.Distance,
                MaximumDistance = plan.MaximumDistance,
                TargetBodySize = plan.TargetBodySize,
                MaximumTargetBodySize = plan.MaximumTargetBodySize,
                IgnoresIntermediateUnits = true,
                IgnoresHeightDifference = true,
                AppliesLandingContact = true,
            }
        );
        string followUpSummary = BuildForcedMoveAppliedEffectPreviewSummary(effectDefinitions);
        preview.AddLogLine(
            $"预计将 {targetUnit.DisplayName} 升至空中并牵引 {plan.Distance} 格至 ({plan.DestinationCoord.X}, {plan.DestinationCoord.Y})；不经过中间单位格且不受高低差限制，落地时结算一次地形接触{followUpSummary}。"
        );
    }

    private static string BuildForcedMoveAppliedEffectPreviewSummary(
        IEnumerable<CombatEffectDefinition> effectDefinitions
    )
    {
        var parts = new List<string>();
        foreach (CombatEffectDefinition effectDefinition in effectDefinitions
            ?? Array.Empty<CombatEffectDefinition>())
        {
            if (
                effectDefinition == null
                || effectDefinition.TriggerEventKind
                    != CombatEffectTriggerEvent.ForcedMoveApplied
            )
            {
                continue;
            }
            if (effectDefinition.EffectKind == BattleEffectKind.EraseStatus)
            {
                StringName statusId = effectDefinition.StatusId != ""
                    ? effectDefinition.StatusId
                    : effectDefinition.TriggerStatusId;
                if (statusId != "")
                    parts.Add($"消耗状态“{statusId}”");
            }
            else if (
                effectDefinition.EffectKind == BattleEffectKind.Status
                || effectDefinition.EffectKind == BattleEffectKind.ApplyStatus
            )
            {
                parts.Add(
                    effectDefinition.DurationTu > 0
                        ? $"施加 {effectDefinition.DurationTu}TU 状态“{effectDefinition.StatusId}”"
                        : $"施加状态“{effectDefinition.StatusId}”"
                );
            }
        }
        return parts.Count > 0 ? $"，并在牵引成功后{string.Join("、", parts)}" : "";
    }

    private IReadOnlyList<Vector2I> _collect_unit_skill_preview_coords(
        BattleUnitReadView activeUnit,
        SkillDefinition skillDefinition,
        IReadOnlyList<BattleUnitReadView> targetUnits
    )
    {
        BattleState state = _owner.RtState();
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (state == null || !activeUnit.IsValid || combatProfile == null)
            return Array.Empty<Vector2I>();
        IReadOnlyList<Vector2I> emptyTargetCoords = Array.Empty<Vector2I>();
        BattleTargetCollectionResult collectedTargetCoords =
            Runtime?.CollectCombatProfileTargetCoords(
                state,
                activeUnit.Coord,
                combatProfile,
                emptyTargetCoords,
                activeUnit,
                targetUnits ?? Array.Empty<BattleUnitReadView>(),
                activeUnit.GetKnownSkillLevel(skillDefinition.SkillId)
            ) ?? BattleTargetCollectionResult.UnhandledResult(emptyTargetCoords);
        return BattleSkillExecutionOrchestrator.SortCoordsTyped(collectedTargetCoords.TargetCoords);
    }

    internal void _preview_ground_skill_command(
        BattleUnitReadView active_unit,
        BattleCommand command,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        BattlePreview preview
    )
    {
        using BattleAiTraceSpan trace = new("preview:ground_skill");
        _preview_ground_skill_command_impl(
            active_unit,
            command,
            preview,
            skillDefinition,
            castVariantDefinition
        );
    }

    internal void _preview_ground_skill_command_impl(
        BattleUnitReadView active_unit,
        BattleCommand command,
        BattlePreview preview,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition
    )
    {
        if (preview == null)
        {
            return;
        }
        preview.ClearSaveBranchPreview();
        preview.ClearShieldPreview();
        preview.ClearEquipmentDurabilityPreview();
        preview.ClearForcedMovePreview();
        preview.ClearDamagePreview();
        preview.ClearStatusContributionPreviews();
        castVariantDefinition ??= Runtime?.GetSkillResolutionRules()
            ?.ResolveGroundCastVariantDefinition(
                skillDefinition,
                active_unit,
                command != null ? command.skill_variant_id : new StringName("")
            );
        if (
            _owner.TryPreviewDirectionalPiercingSkill(
                active_unit,
                command,
                skillDefinition,
                castVariantDefinition,
                preview
            )
        )
        {
            return;
        }
        string blockReason = _owner._get_skill_command_block_reason(
            active_unit,
            skillDefinition,
            castVariantDefinition
        );
        if (!string.IsNullOrEmpty(blockReason))
        {
            preview.AddLogLine(blockReason);
            return;
        }
        BattleGroundSkillValidationResult validation;
        using (new BattleAiTraceSpan("preview:ground_skill.validate"))
        {
            validation =
                Runtime?.ValidateGroundSkillCommandResult(
                    active_unit,
                    skillDefinition,
                    castVariantDefinition,
                    command
                ) ?? BattleGroundSkillValidationResult.Denied("地面技能目标无效。");
        }
        IReadOnlyList<Vector2I> previewCoords;
        bool allowed;
        IReadOnlyList<CombatEffectDefinition> previewUnitEffectDefinitions;
        IReadOnlyList<Vector2I> previewUnitEffectCoords;
        using (new BattleAiTraceSpan("preview:ground_skill.preview_coords"))
        {
            preview.ClearTargetCoords();
            if (validation.HasPreviewCoords)
            {
                previewCoords = validation.PreviewCoords;
            }
            else
            {
                Vector2I sourceCoord = active_unit.IsValid
                    ? active_unit.Coord
                    : new Vector2I(-1, -1);
                IReadOnlyList<Vector2I> builtCoords =
                    Runtime?.BuildGroundEffectCoords(
                        skillDefinition,
                        validation.TargetCoords,
                        sourceCoord,
                        active_unit,
                        castVariantDefinition
                    ) ?? Array.Empty<Vector2I>();
                previewCoords = builtCoords;
            }
            preview.resolved_anchor_coord = validation.ResolvedAnchorCoord;
            allowed = validation.Allowed;
            bool chargePathPreview = false;
            if (allowed && Runtime?.GetChargeResolver() != null)
            {
                CombatEffectDefinition pathStepAoeEffect = Runtime.GetChargeResolver()
                    .GetChargePathStepAoeEffectDefinition(
                        castVariantDefinition,
                        skillDefinition,
                        active_unit
                    );
                if (pathStepAoeEffect != null)
                {
                    chargePathPreview = true;
                    previewCoords = Runtime.GetChargeResolver().BuildChargeStepAoePreviewCoords(
                        active_unit,
                        skillDefinition,
                        validation.Direction,
                        validation.Distance,
                        pathStepAoeEffect,
                        castVariantDefinition
                    );
                }
            }
            if (chargePathPreview)
            {
                previewUnitEffectDefinitions =
                    Runtime?.CollectGroundUnitEffectDefinitions(
                        skillDefinition,
                        castVariantDefinition,
                        active_unit
                    ) ?? Array.Empty<CombatEffectDefinition>();
                previewUnitEffectCoords = previewCoords;
            }
            else
            {
                BattleSkillExecutionOrchestrator.GroundEffectBarrierClipContext barrierClip =
                    _owner.PreviewGroundEffectBarrierClipContext(
                        active_unit,
                        skillDefinition,
                        castVariantDefinition,
                        validation.TargetCoords,
                        previewCoords
                    );
                previewCoords = barrierClip.VisibleEffectCoords;
                previewUnitEffectDefinitions = barrierClip.UnitEffectDefinitions;
                previewUnitEffectCoords = barrierClip.UnitEffectCoords;
            }
            foreach (Vector2I targetCoord in previewCoords)
                preview.AddTargetCoord(targetCoord);
        }
        using (new BattleAiTraceSpan("preview:ground_skill.collect_unit_ids"))
        {
            IReadOnlyList<StringName> previewUnitIds =
                Runtime?.CollectGroundPreviewUnitIds(
                    active_unit,
                    skillDefinition,
                    previewUnitEffectDefinitions,
                    previewUnitEffectCoords
                ) ?? Array.Empty<StringName>();
            preview.SetTargetUnitIds(previewUnitIds);
        }
        if (allowed && Runtime?.GetChargeResolver() != null)
        {
            using (new BattleAiTraceSpan("preview:ground_skill.path_step_aoe"))
            {
                CombatEffectDefinition pathStepAoeEffect = Runtime.GetChargeResolver()
                    .GetChargePathStepAoeEffectDefinition(
                        castVariantDefinition,
                        skillDefinition,
                        active_unit
                    );
                if (pathStepAoeEffect != null)
                {
                    StringName pathStepTargetFilter =
                        Runtime?.GetSkillResolutionRules()?.ResolveEffectTargetFilter(
                            skillDefinition,
                            pathStepAoeEffect
                        ) ?? new StringName("");
                    foreach (
                        BattleUnitReadView targetUnit in _owner.CollectUnitsInCoordsReadView(
                            preview.TargetCoordsTyped
                        )
                    )
                    {
                        if (
                            !_owner._is_unit_valid_for_effect(
                                active_unit,
                                targetUnit,
                                pathStepTargetFilter
                            )
                        )
                        {
                            continue;
                        }
                        if (preview.ContainsTargetUnitId(targetUnit.UnitId))
                        {
                            continue;
                        }
                        preview.AddTargetUnitId(targetUnit.UnitId);
                    }
                }
            }
        }
        preview.allowed = allowed;
        if (preview.allowed)
        {
            AppendGroundWindPushPreview(
                preview,
                active_unit,
                skillDefinition,
                previewUnitEffectDefinitions,
                validation.TargetCoords
            );
            preview.SetDamagePreview(
                BattleDamagePreviewRangeService.BuildSkillDamagePreview(
                    active_unit,
                    previewUnitEffectDefinitions
                )
            );
            preview.SetShieldPreview(
                BuildShieldPreviewTyped(
                    active_unit,
                    skillDefinition,
                    previewUnitEffectDefinitions,
                    preview.TargetUnitIdsTyped
                )
            );
            preview.SetSaveBranchPreview(
                BuildGroundSkillGradedSaveExecutePreview(
                    active_unit,
                    skillDefinition,
                    castVariantDefinition,
                    preview.TargetUnitIdsTyped
                )
            );
            foreach (
                BattleStatusContributionPreviewData statusPreview
                in BuildStatusContributionPreviewsTyped(
                    active_unit,
                    skillDefinition,
                    previewUnitEffectDefinitions,
                    preview.TargetUnitIdsTyped
                )
            )
            {
                preview.AddStatusContributionPreview(statusPreview);
            }
            IReadOnlyList<CombatEffectDefinition> terrainEffectDefinitions =
                Runtime?.CollectGroundTerrainEffectDefinitions(
                    skillDefinition,
                    castVariantDefinition,
                    active_unit
                ) ?? Array.Empty<CombatEffectDefinition>();
            foreach (CombatEffectDefinition terrainEffect in terrainEffectDefinitions)
            {
                if (
                    terrainEffect?.TerrainContactModeKind
                    != CombatTerrainContactMode.InterruptMovementOnFailedSave
                )
                {
                    continue;
                }
                preview.SetTerrainContactPreview(
                    new BattleTerrainContactPreviewData(
                        terrainEffect.TerrainContactMode,
                        terrainEffect.SaveDc,
                        terrainEffect.SaveAbility,
                        terrainEffect.TerrainEffectiveTriggerCount,
                        terrainEffect.DurationTu,
                        terrainEffect.TerrainRecheckFromInside,
                        terrainEffect.TerrainRequiresGroundContact
                    )
                );
                break;
            }
        }
        using (new BattleAiTraceSpan("preview:ground_skill.log_lines"))
        {
            if (preview.allowed)
            {
                BattleTerrainContactPreviewData terrainContact =
                    preview.TerrainContactPreviewTyped;
                preview.AddLogLine(
                    terrainContact != null
                        ? $"{active_unit.DisplayName} 可使用 {_owner._format_skill_variant_label(skillDefinition, castVariantDefinition)}，布置 {preview.TargetCoordsTyped.Count} 格地面机关：敏捷豁免 DC {terrainContact.SaveDc}，失败拦停，可有效阻挡 {terrainContact.EffectiveTriggerCount} 次，持续 {terrainContact.DurationTu}TU。"
                        : $"{active_unit.DisplayName} 可使用 {_owner._format_skill_variant_label(skillDefinition, castVariantDefinition)}，预计影响 {preview.TargetCoordsTyped.Count} 个地格、{preview.TargetUnitIdsTyped.Count} 个单位。"
                );
                if (preview.ShieldPreviewTyped is BattleShieldPreviewData shieldPreview)
                {
                    preview.AddLogLine(shieldPreview.SummaryText);
                }
                if (preview.ForcedMovePreviewTyped is BattleForcedMovePreviewData forcedMovePreview)
                {
                    preview.AddLogLine(forcedMovePreview.SummaryText);
                }
                _owner._append_damage_preview_line(preview);
                foreach (
                    BattleStatusContributionPreviewData statusPreview
                    in preview.StatusContributionPreviewsTyped
                )
                {
                    preview.AddLogLine(statusPreview.SummaryText);
                }
            }
            else
            {
                preview.AddLogLine(
                    string.IsNullOrEmpty(validation.Message)
                        ? "地面技能目标无效。"
                        : validation.Message
                );
            }
        }
    }

    private void AppendGroundWindPushPreview(
        BattlePreview preview,
        BattleUnitReadView sourceUnit,
        SkillDefinition skillDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        IReadOnlyList<Vector2I> targetCoords
    )
    {
        CombatEffectDefinition windPushEffect = BattleWindPushRules.FindEffect(
            effectDefinitions
        );
        BattleState state = _owner.RtState();
        if (
            preview == null
            || windPushEffect == null
            || state == null
            || !state.TryGetUnitTyped(sourceUnit.UnitId, out BattleUnitState mutableSource)
            || mutableSource == null
        )
        {
            return;
        }
        Vector2I direction =
            targetCoords != null && targetCoords.Count > 0
                ? targetCoords[0] - sourceUnit.Coord
                : Vector2I.Zero;
        BattleForcedMovePreviewData windPreview = BattleWindPushRules.BuildPreview(
            state,
            Runtime?.GetGridService(),
            Runtime?.GetLayeredBarrierService(),
            mutableSource,
            windPushEffect,
            preview.TargetUnitIdsTyped,
            direction,
            skillDefinition?.SkillId ?? new StringName("")
        );
        if (windPreview == null)
            return;
        preview.SetForcedMovePreview(windPreview);
        if (BattleWindPushRules.IsPureWindPushEffectSet(effectDefinitions))
        {
            preview.SetTargetUnitIds(
                windPreview.Targets
                    .Where(target =>
                        target != null
                        && target.CanMoveOnFailedSave
                        && target.SaveFailureProbabilityBasisPoints > 0
                    )
                    .Select(target => target.TargetUnitId)
            );
        }
    }

    private BattleSaveBranchPreviewData BuildGroundSkillGradedSaveExecutePreview(
        BattleUnitReadView activeUnit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant,
        IReadOnlyList<StringName> targetUnitIds
    )
    {
        if (
            !activeUnit.IsValid
            || skillDefinition == null
            || targetUnitIds == null
            || targetUnitIds.Count == 0
        )
        {
            return null;
        }

        CombatEffectDefinition effectDefinition = FindFirstValidGradedSaveExecuteEffect(
            Runtime?.GetSkillResolutionRules()?.CollectGroundUnitEffectDefinitions(
                skillDefinition,
                castVariant,
                activeUnit
            ) ?? new List<CombatEffectDefinition>(),
            out PhantasmalKillExecutionProfile profile
        );
        if (effectDefinition == null)
        {
            return null;
        }

        BattleState state = _owner.RtState();
        if (
            state == null
            || !state.TryGetUnitTyped(activeUnit.UnitId, out BattleUnitState sourceUnit)
        )
        {
            return null;
        }

        StringName targetFilter =
            Runtime?.GetSkillResolutionRules()?.ResolveEffectTargetFilter(
                skillDefinition,
                effectDefinition
            ) ?? new StringName("");
        int targetCount = 0;
        int enemyTargetCount = 0;
        int friendlyTargetCount = 0;
        int affectedUnitCount = 0;
        int enemyAffectedCount = 0;
        int friendlyAffectedCount = 0;
        int immuneCount = 0;
        int enemyExecuteRiskCount = 0;
        int friendlyExecuteRiskCount = 0;
        int failureExecuteRiskCount = 0;
        int criticalFailureExecuteRiskCount = 0;
        int criticalSuccessExpectedCount = 0;
        int criticalSuccessExpectedBasisPoints = 0;
        int successAftershockExpectedBasisPoints = 0;
        int failureExpectedBasisPoints = 0;
        int criticalFailureExpectedBasisPoints = 0;

        foreach (StringName targetUnitId in targetUnitIds)
        {
            if (
                BattleSkillExecutionOrchestrator.StringNameIsEmpty(targetUnitId)
                || !state.TryGetUnitTyped(targetUnitId, out BattleUnitState targetUnit)
                || !_owner._is_unit_valid_for_effect(sourceUnit, targetUnit, targetFilter)
            )
            {
                continue;
            }

            bool friendly = targetUnit.faction_id == sourceUnit.faction_id;
            targetCount++;
            if (friendly)
            {
                friendlyTargetCount++;
            }
            else
            {
                enemyTargetCount++;
            }

            BattleGradedSaveGradeDistribution distribution =
                PhantasmalKillExecutionRules.EstimateGradeDistribution(
                    sourceUnit,
                    targetUnit,
                    effectDefinition,
                    BattleSaveContext.ForSkill(skillDefinition.SkillId)
                );
            if (distribution.ImmuneBasisPoints > 0)
            {
                immuneCount++;
                continue;
            }

            affectedUnitCount++;
            if (friendly)
            {
                friendlyAffectedCount++;
            }
            else
            {
                enemyAffectedCount++;
            }

            if (distribution.CriticalSuccessBasisPoints > 0)
            {
                criticalSuccessExpectedCount++;
                criticalSuccessExpectedBasisPoints += distribution.CriticalSuccessBasisPoints;
            }
            successAftershockExpectedBasisPoints += distribution.SuccessBasisPoints;
            failureExpectedBasisPoints += distribution.FailureBasisPoints;
            criticalFailureExpectedBasisPoints += distribution.CriticalFailureBasisPoints;

            int targetMaxHp = BattleSkillExecutionOrchestrator.GetUnitMaxHp(targetUnit);
            bool failureExecuteRisk =
                distribution.FailureBasisPoints > 0
                && targetUnit.GetCurrentHp()
                    <= PhantasmalKillExecutionRules.ResolveFailureExecuteThreshold(
                        profile,
                        targetMaxHp
                    );
            bool criticalFailureExecuteRisk =
                distribution.CriticalFailureBasisPoints > 0
                && targetUnit.GetCurrentHp()
                    <= PhantasmalKillExecutionRules.ResolveCriticalFailureExecuteThreshold(
                        profile,
                        targetMaxHp
                    );
            if (failureExecuteRisk)
            {
                failureExecuteRiskCount++;
            }
            if (criticalFailureExecuteRisk)
            {
                criticalFailureExecuteRiskCount++;
            }
            if (failureExecuteRisk || criticalFailureExecuteRisk)
            {
                if (friendly)
                {
                    friendlyExecuteRiskCount++;
                }
                else
                {
                    enemyExecuteRiskCount++;
                }
            }
        }

        if (targetCount == 0)
        {
            return null;
        }

        return new BattleSaveBranchPreviewData
        {
            Kind = new StringName("graded_save_execute"),
            SaveTag = effectDefinition.SaveTag,
            SaveAbility = effectDefinition.SaveAbility,
            SummaryText = BuildGroundGradedSaveExecuteSummaryText(
                enemyTargetCount,
                friendlyAffectedCount,
                friendlyExecuteRiskCount,
                enemyExecuteRiskCount,
                immuneCount,
                failureExecuteRiskCount,
                criticalFailureExecuteRiskCount,
                successAftershockExpectedBasisPoints
            ),
            ResidualValues = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["profile_id"] = profile.ProfileId,
                ["target_count"] = targetCount,
                ["enemy_target_count"] = enemyTargetCount,
                ["friendly_target_count"] = friendlyTargetCount,
                ["affected_unit_count"] = affectedUnitCount,
                ["enemy_affected_count"] = enemyAffectedCount,
                ["friendly_affected_count"] = friendlyAffectedCount,
                ["friendly_execute_risk_count"] = friendlyExecuteRiskCount,
                ["enemy_execute_risk_count"] = enemyExecuteRiskCount,
                ["immune_count"] = immuneCount,
                ["critical_success_expected_count"] = criticalSuccessExpectedCount,
                ["critical_success_expected_basis_points"] =
                    criticalSuccessExpectedBasisPoints,
                ["success_aftershock_expected_basis_points"] =
                    successAftershockExpectedBasisPoints,
                ["failure_expected_basis_points"] = failureExpectedBasisPoints,
                ["critical_failure_expected_basis_points"] =
                    criticalFailureExpectedBasisPoints,
                ["failure_execute_risk_count"] = failureExecuteRiskCount,
                ["critical_failure_execute_risk_count"] = criticalFailureExecuteRiskCount,
            },
        };
    }

    private static CombatEffectDefinition FindFirstValidGradedSaveExecuteEffect(
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        out PhantasmalKillExecutionProfile profile
    )
    {
        profile = default;
        foreach (
            CombatEffectDefinition effectDefinition in effectDefinitions
                ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (
                effectDefinition?.EffectKind == BattleEffectKind.GradedSaveExecute
                && PhantasmalKillExecutionRules.TryReadPhantasmalKillProfile(
                    effectDefinition,
                    out profile,
                    out _
                )
            )
            {
                return effectDefinition;
            }
        }
        return null;
    }

    private static string BuildGroundGradedSaveExecuteSummaryText(
        int enemyTargetCount,
        int friendlyAffectedCount,
        int friendlyExecuteRiskCount,
        int enemyExecuteRiskCount,
        int immuneCount,
        int failureExecuteRiskCount,
        int criticalFailureExecuteRiskCount,
        int successAftershockExpectedBasisPoints
    )
    {
        var parts = new List<string>
        {
            $"怪影杀戮：敌方 {enemyTargetCount}，友军 {friendlyAffectedCount}",
            $"处决风险 敌方 {enemyExecuteRiskCount} / 友军 {friendlyExecuteRiskCount}",
            $"分支风险 失败处决 {failureExecuteRiskCount} / 大失败处决 {criticalFailureExecuteRiskCount}",
        };
        if (successAftershockExpectedBasisPoints > 0)
        {
            parts.Add($"成功余悸期望 {successAftershockExpectedBasisPoints}bp");
        }
        if (immuneCount > 0)
        {
            parts.Add($"免疫/无效 {immuneCount}");
        }
        if (friendlyAffectedCount > 0 || friendlyExecuteRiskCount > 0)
        {
            parts.Add("友军误伤风险");
        }
        return string.Join(" · ", parts);
    }

    internal void AppendDamageResultLogLines(
        BattleEventBatch batch,
        string subject_label,
        string target_display_name,
        AttackEffectResolutionResult result
    )
    {
        Runtime?.AppendDamageResultLogLines(
            batch,
            subject_label,
            target_display_name,
            result
        );
    }

    internal BattleDamagePreviewRangeService.SkillDamagePreview? BuildUnitSkillDamagePreviewTyped(
        BattleUnitReadView active_unit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant,
        BattleWindupQuote? windupQuote = null
    )
    {
        if (!active_unit.IsValid || skillDefinition == null)
        {
            return null;
        }
        List<CombatEffectDefinition> effectDefinitions = Runtime?.GetSkillResolutionRules()
            ?.CollectUnitSkillEffectDefinitions(
                skillDefinition,
                castVariant,
                active_unit
            ) ?? new List<CombatEffectDefinition>();
        if (windupQuote is BattleWindupQuote resolvedWindupQuote)
        {
            effectDefinitions = new List<CombatEffectDefinition>(
                BattleWindupRules.ApplyWeaponDiceMultiplier(
                    effectDefinitions,
                    resolvedWindupQuote.WeaponDiceMultiplier
                )
            );
        }
        CombatEffectDefinition repeatAttackEffect = null;
        foreach (CombatEffectDefinition effectDefinition in effectDefinitions)
        {
            if (
                effectDefinition?.EffectKind
                is BattleEffectKind.RepeatAttackUntilFail
                    or BattleEffectKind.FixedRepeatAttack
            )
            {
                repeatAttackEffect = effectDefinition;
                break;
            }
        }
        if (repeatAttackEffect != null)
        {
            int stageCount =
                BattleRepeatAttackResolver.resolve_repeat_attack_preview_stage_count(
                    active_unit,
                    skillDefinition,
                    repeatAttackEffect
                );
            effectDefinitions = BattleRepeatAttackResolver.BuildRepeatAttackPreviewEffects(
                effectDefinitions,
                repeatAttackEffect,
                stageCount
            );
        }
        return BattleDamagePreviewRangeService.BuildSkillDamagePreview(
            active_unit,
            effectDefinitions
        );
    }

    private IReadOnlyList<BattleStatusContributionPreviewData>
        BuildStatusContributionPreviewsTyped(
            BattleUnitReadView activeUnit,
            SkillDefinition skillDefinition,
            IReadOnlyList<CombatEffectDefinition> effectDefinitions,
            IReadOnlyList<StringName> targetUnitIds
        )
    {
        var result = new List<BattleStatusContributionPreviewData>();
        BattleState state = _owner.RtState();
        if (
            state == null
            || !activeUnit.IsValid
            || skillDefinition == null
            || !state.TryGetUnitTyped(activeUnit.UnitId, out BattleUnitState sourceUnit)
        )
        {
            return result;
        }

        var relevantEffects = new List<CombatEffectDefinition>();
        foreach (
            CombatEffectDefinition effectDefinition
            in effectDefinitions ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (
                ResolveStatusContributionPreviewStatusId(
                    effectDefinition,
                    out _,
                    out _
                ) != ""
            )
            {
                relevantEffects.Add(effectDefinition);
            }
        }
        if (relevantEffects.Count == 0)
            return result;

        var targetUnits = new List<BattleUnitState>();
        foreach (StringName targetUnitId in targetUnitIds ?? Array.Empty<StringName>())
        {
            if (state.TryGetUnitTyped(targetUnitId, out BattleUnitState targetUnit))
                targetUnits.Add(targetUnit);
        }
        IReadOnlyDictionary<CombatEffectDefinition, IReadOnlyList<BattleUnitState>> targetPlan =
            _owner.BuildUnitEffectTargetPlan(
                sourceUnit,
                skillDefinition,
                relevantEffects,
                targetUnits
            );
        BattleStatusSourceIdentity sourceIdentity = BattleStatusSourceIdentity.Skill(
            activeUnit.UnitId,
            skillDefinition.SkillId
        );
        foreach (CombatEffectDefinition effectDefinition in relevantEffects)
        {
            StringName statusId = ResolveStatusContributionPreviewStatusId(
                effectDefinition,
                out bool appliesOnSaveFailure,
                out BattleStatusSemantic semantic
            );
            if (
                statusId == ""
                || !targetPlan.TryGetValue(
                    effectDefinition,
                    out IReadOnlyList<BattleUnitState> plannedTargets
                )
            )
            {
                continue;
            }
            foreach (BattleUnitState targetUnit in plannedTargets)
            {
                BattleStatusEffectState existing = targetUnit.GetStatusEffect(statusId);
                BattleStatusSourceContributionState previousContribution =
                    existing?.GetSourceContributionTyped(sourceIdentity);
                BattleStatusEffectState merged = BattleStatusSemanticTable.MergeStatus(
                    effectDefinition,
                    activeUnit.UnitId,
                    existing,
                    statusId,
                    sourceIdentity
                );
                BattleStatusSourceContributionState resultContribution =
                    merged?.GetSourceContributionTyped(sourceIdentity);
                if (merged == null || resultContribution == null)
                    continue;
                result.Add(
                    new BattleStatusContributionPreviewData(
                        targetUnit.unit_id,
                        targetUnit.display_name,
                        statusId,
                        sourceIdentity.KindId,
                        sourceIdentity.SourceDefinitionId,
                        appliesOnSaveFailure,
                        previousContribution == null,
                        Math.Max(previousContribution?.Stacks ?? 0, 0),
                        Math.Max(resultContribution.Stacks, 0),
                        Math.Max(semantic.MaxStacks, 0),
                        Math.Max(merged.stacks, 0),
                        merged.GetSourceContributionsTyped().Count,
                        resultContribution.DurationTu,
                        resultContribution.TickIntervalTu
                    )
                );
            }
        }
        return result;
    }

    private static StringName ResolveStatusContributionPreviewStatusId(
        CombatEffectDefinition effectDefinition,
        out bool appliesOnSaveFailure,
        out BattleStatusSemantic semantic
    )
    {
        appliesOnSaveFailure = false;
        semantic = default;
        if (effectDefinition == null)
            return "";
        StringName statusId = "";
        if (
            effectDefinition.EffectKind == BattleEffectKind.Damage
            && effectDefinition.SaveFailureStatusId != ""
        )
        {
            statusId = effectDefinition.SaveFailureStatusId;
            appliesOnSaveFailure = true;
        }
        else if (
            effectDefinition.EffectKind
            is BattleEffectKind.Status or BattleEffectKind.ApplyStatus
        )
        {
            statusId = effectDefinition.StatusId;
            appliesOnSaveFailure = effectDefinition.SaveDc > 0;
        }
        semantic = BattleStatusSemanticTable.GetSemantic(statusId);
        return semantic.StackingScope == BattleStatusStackingScope.SourceDefinition
            ? statusId
            : new StringName("");
    }

    private BattleSaveBranchPreviewData BuildUnitSkillSaveBranchPreview(
        BattleUnitReadView activeUnit,
        IReadOnlyList<BattleUnitReadView> targetUnits,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant
    )
    {
        if (!activeUnit.IsValid || skillDefinition == null || targetUnits == null || targetUnits.Count != 1)
        {
            return null;
        }

        BattleUnitReadView targetUnit = targetUnits[0];
        if (!targetUnit.IsValid)
        {
            return null;
        }

        IReadOnlyList<CombatEffectDefinition> effectDefinitions =
            Runtime?.GetSkillResolutionRules()?.CollectUnitSkillEffectDefinitions(
                skillDefinition,
                castVariant,
                activeUnit
            ) ?? new List<CombatEffectDefinition>();

        BattleState state = _owner.RtState();
        if (
            state == null
            || !state.TryGetUnitTyped(activeUnit.UnitId, out BattleUnitState sourceState)
            || !state.TryGetUnitTyped(targetUnit.UnitId, out BattleUnitState targetState)
        )
        {
            return null;
        }

        foreach (
            CombatEffectDefinition effectDefinition in
                effectDefinitions ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if ((effectDefinition?.SaveFailureStatusOutcomes?.Count ?? 0) == 0)
                continue;
            BattleSaveProbabilityResult weightedProbability =
                BattleSaveResolver.EstimateSaveSuccessProbabilityResult(
                    sourceState,
                    targetState,
                    effectDefinition,
                    BattleSaveContext.ForSkill(skillDefinition.SkillId)
                );
            if (!weightedProbability.HasSave)
                continue;
            int weightedSaveSuccessBps = Mathf.Clamp(
                weightedProbability.SuccessProbabilityBasisPoints,
                0,
                10000
            );
            int saveFailureBps = Mathf.Clamp(
                weightedProbability.FailureProbabilityBasisPoints,
                0,
                10000
            );
            IReadOnlyList<BattleWeightedStatusOutcomePreviewData> outcomes =
                BattleWeightedStatusOutcomeRules.BuildPreview(
                    effectDefinition.SaveFailureStatusOutcomes,
                    saveFailureBps
                );
            string outcomeText = string.Join(
                " / ",
                outcomes.Select(outcome =>
                    $"{outcome.DisplayName} {FormatBasisPointPercent(outcome.ConditionalProbabilityBasisPoints)}"
                )
            );
            return new BattleSaveBranchPreviewData
            {
                Kind = new StringName("weighted_status_on_save_failure"),
                Branch = new StringName("save_failure_random_status"),
                SaveTag = weightedProbability.SaveTag,
                SaveAbility = weightedProbability.Ability,
                SaveDc = weightedProbability.Dc,
                SaveAdvantageState = weightedProbability.AdvantageState,
                SaveSuccessChanceBasisPoints = weightedSaveSuccessBps,
                HitChanceBasisPoints = saveFailureBps,
                FailureBranchText = $"全额伤害并随机施加：{outcomeText}",
                SuccessBranchText = "半额伤害且不施加控制",
                SummaryText =
                    $"豁免成功 {FormatBasisPointPercent(weightedSaveSuccessBps)} · 失败：全伤 + 随机控制（{outcomeText}） · 成功：半伤、无控制",
                ResidualValues = BuildWeightedStatusOutcomePreviewPayload(outcomes),
            };
        }

        var lookup = _targetValidationService.FindSingleExecuteEffect(effectDefinitions);
        if (lookup.Effect == null || !string.IsNullOrEmpty(lookup.ErrorMessage))
        {
            return null;
        }

        BattleExecutePlan plan = BattleExecutionRules.BuildExecutePlan(
            activeUnit,
            targetUnit,
            BattleExecutionRuleParams.FromEffect(lookup.Effect, skillDefinition.SkillId)
        );
        if (!plan.CanExecute)
        {
            return null;
        }

        BattleSaveProbabilityResult probability =
            BattleSaveResolver.EstimateSaveSuccessProbabilityResult(
                sourceState,
                targetState,
                lookup.Effect,
                BattleSaveContext.ForSkill(skillDefinition.SkillId)
            );
        int saveSuccessBps = Mathf.Clamp(probability.SuccessProbabilityBasisPoints, 0, 10000);
        int hitChanceBps = probability.HasSave ? Mathf.Clamp(10000 - saveSuccessBps, 0, 10000) : 10000;
        string successBranchText = plan.SoulFractureParams.HasValue ? "灵魂裂解" : "抵抗";
        string summaryText =
            $"命中率 {FormatBasisPointPercent(hitChanceBps)} · 豁免失败：死亡律令 · 豁免成功：{successBranchText}";

        return new BattleSaveBranchPreviewData
        {
            Kind = new StringName("execute"),
            Branch = plan.Branch,
            SaveTag = probability.SaveTag,
            SaveAbility = probability.Ability,
            SaveDc = probability.Dc,
            SaveAdvantageState = probability.AdvantageState,
            SaveSuccessChanceBasisPoints = saveSuccessBps,
            HitChanceBasisPoints = hitChanceBps,
            Threshold = plan.Threshold,
            CurrentHp = plan.CurrentHp,
            MaxHp = plan.MaxHp,
            FailureBranchText = "死亡律令",
            SuccessBranchText = successBranchText,
            SummaryText = summaryText,
        };
    }

    private static IReadOnlyDictionary<string, object> BuildWeightedStatusOutcomePreviewPayload(
        IReadOnlyList<BattleWeightedStatusOutcomePreviewData> outcomes
    )
    {
        var outcomePayloads = new List<object>();
        foreach (
            BattleWeightedStatusOutcomePreviewData outcome in
                outcomes ?? Array.Empty<BattleWeightedStatusOutcomePreviewData>()
        )
        {
            if (outcome == null)
                continue;
            outcomePayloads.Add(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["outcome_id"] = outcome.OutcomeId.ToString(),
                    ["status_id"] = outcome.StatusId.ToString(),
                    ["display_name"] = outcome.DisplayName ?? "",
                    ["weight"] = outcome.Weight,
                    ["total_weight"] = outcome.TotalWeight,
                    ["conditional_probability_basis_points"] =
                        outcome.ConditionalProbabilityBasisPoints,
                    ["application_probability_basis_points"] =
                        outcome.ApplicationProbabilityBasisPoints,
                    ["duration_tu"] = outcome.DurationTu,
                    ["power"] = outcome.Power,
                    ["attack_roll_penalty"] = outcome.AttackRollPenalty,
                    ["lock_counterattack"] = outcome.LockCounterattack,
                    ["lock_guard"] = outcome.LockGuard,
                    ["lock_dodge_bonus"] = outcome.LockDodgeBonus,
                    ["lock_crit"] = outcome.LockCrit,
                }
            );
        }
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["save_failure_status_outcomes"] = outcomePayloads,
        };
    }

    private static string FormatBasisPointPercent(int basisPoints)
    {
        int clamped = Mathf.Clamp(basisPoints, 0, 10000);
        if (clamped % 100 == 0)
        {
            return $"{clamped / 100}%";
        }
        return $"{clamped / 100.0f:0.#}%";
    }

    private BattleShieldPreviewData BuildShieldPreviewTyped(
        BattleUnitReadView activeUnit,
        SkillDefinition skillDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        IReadOnlyList<StringName> targetUnitIds
    )
    {
        BattleState state = _owner.RtState();
        if (
            state == null
            || !activeUnit.IsValid
            || !state.TryGetUnitTyped(activeUnit.UnitId, out BattleUnitState sourceUnit)
        )
        {
            return null;
        }
        var targetUnits = new List<BattleUnitState>();
        foreach (StringName targetUnitId in targetUnitIds ?? Array.Empty<StringName>())
        {
            if (state.TryGetUnitTyped(targetUnitId, out BattleUnitState targetUnit))
            {
                targetUnits.Add(targetUnit);
            }
        }
        var shieldEffects = new List<CombatEffectDefinition>();
        foreach (
            CombatEffectDefinition effectDefinition
            in effectDefinitions ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (effectDefinition?.EffectKind == BattleEffectKind.Shield)
            {
                shieldEffects.Add(effectDefinition);
            }
        }
        IReadOnlyDictionary<CombatEffectDefinition, IReadOnlyList<BattleUnitState>> targetPlan =
            _owner.BuildUnitEffectTargetPlan(
                sourceUnit,
                skillDefinition,
                shieldEffects,
                targetUnits
            );
        IReadOnlyList<BattleUnitState> shieldTargetUnits =
            BattleSkillExecutionOrchestrator.CollectPlannedTargets(
                shieldEffects,
                targetPlan
            );
        return BattleShieldPreviewRules.BuildPreview(
            sourceUnit,
            skillDefinition,
            shieldEffects,
            shieldTargetUnits
        );
    }

    private BattleEquipmentDurabilityPreviewData BuildEquipmentDurabilityPreviewTyped(
        BattleUnitReadView activeUnit,
        SkillDefinition skillDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        IReadOnlyList<StringName> targetUnitIds
    )
    {
        BattleState state = _owner.RtState();
        if (
            state == null
            || !activeUnit.IsValid
            || !state.TryGetUnitTyped(activeUnit.UnitId, out BattleUnitState sourceUnit)
            || targetUnitIds == null
            || targetUnitIds.Count == 0
            || !state.TryGetUnitTyped(targetUnitIds[0], out BattleUnitState targetUnit)
        )
        {
            return null;
        }
        foreach (
            CombatEffectDefinition effectDefinition in effectDefinitions
                ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (effectDefinition?.EffectKind != BattleEffectKind.EquipmentDurabilityDamage)
                continue;
            return new BattleEquipmentDurabilityResolver().BuildPreview(
                sourceUnit,
                targetUnit,
                effectDefinition,
                skillDefinition?.SkillId ?? "",
                Runtime?.GetItemDefIndex()
            );
        }
        return null;
    }
}
