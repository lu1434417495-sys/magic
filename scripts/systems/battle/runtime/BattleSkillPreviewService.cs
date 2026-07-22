using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

internal sealed class BattleSkillPreviewService
{
    private WeakReference<BattleRuntimeModule> _runtimeRef;
    private BattleSkillExecutionOrchestrator _owner;
    private BattleSkillTargetValidationService _targetValidationService;

    private BattleRuntimeModule _runtime
    {
        get =>
            _runtimeRef != null
            && _runtimeRef.TryGetTarget(out BattleRuntimeModule runtime)
                ? runtime
                : null;
        set =>
            _runtimeRef =
                value != null ? new WeakReference<BattleRuntimeModule>(value) : null;
    }

    private BattleRuntimeModule Runtime => _runtime;

    internal void Setup(
        BattleRuntimeModule runtime,
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
        AiTraceRecorder.Enter("preview:skill.orchestrator");
        _preview_skill_command_impl(active_unit, command, preview);
        AiTraceRecorder.Exit("preview:skill.orchestrator");
    }

    internal void _preview_skill_command_impl(
        BattleUnitReadView active_unit,
        BattleCommand command,
        BattlePreview preview
    )
    {
        SkillDefinition skillDefinition = Runtime?.GetSkillDefinitionTyped(command.skill_id);
        if (skillDefinition?.CombatProfile == null)
        {
            preview.AddLogLine("技能或目标无效。");
            return;
        }
        var runtime = _runtime as BattleRuntimeModule;
        bool isMeteorSwarm =
            skillDefinition.CombatProfile.SpecialResolutionProfileId
            == new StringName("meteor_swarm");
        if (runtime != null && isMeteorSwarm)
        {
            AiTraceRecorder.Enter("preview:skill.meteor_gate");
            BattleSpecialProfileGateResult gateResult = runtime._special_profile_gate != null
                ? runtime._special_profile_gate.PreviewSkill(
                    skillDefinition,
                    command,
                    active_unit,
                    runtime._state
                )
                : null;
            AiTraceRecorder.Exit("preview:skill.meteor_gate");
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
            if (runtime._meteor_swarm_resolver != null)
            {
                runtime._meteor_swarm_resolver.PopulatePreview(
                    active_unit,
                    command,
                    skillDefinition,
                    preview
                );
                return;
            }
            preview.allowed = false;
            preview.AddLogLine("该禁咒结算尚未接入。");
            return;
        }
        AiTraceRecorder.Enter("preview:skill.resolve_options");
        bool allowRepeat = skillDefinition.CombatProfile.AllowRepeatTarget;
        BattleSkillResolutionPolicy policy = Runtime?._skill_resolution_rules
            ?.BuildSkillResolutionPolicy(
                skillDefinition,
                active_unit,
                command != null ? command.skill_variant_id : new StringName(""),
                _targetValidationService._normalize_target_unit_ids(command, allowRepeat)
            );
        bool routesToUnitTargeting = policy?.RoutesToUnitTargeting == true;
        AiTraceRecorder.Exit("preview:skill.resolve_options");
        AiTraceRecorder.Enter("preview:skill.option_block");
        string optionBlockReason = policy?.OptionErrorMessage ?? "技能或目标无效。";
        AiTraceRecorder.Exit("preview:skill.option_block");
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
        AiTraceRecorder.Enter("preview:unit_skill");
        _preview_unit_skill_command_impl(
            active_unit,
            command,
            preview,
            skillDefinition,
            castVariantDefinition
        );
        AiTraceRecorder.Exit("preview:unit_skill");
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
        preview.ClearSaveBranchPreview();
        castVariantDefinition ??= Runtime?._skill_resolution_rules
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

        AiTraceRecorder.Enter("preview:unit_skill.validate_targets");
        BattleUnitSkillPreviewValidationResult validation = _targetValidationService._validate_unit_skill_preview_targets_result(
            active_unit,
            command,
            skillDefinition,
            castVariantDefinition
        );
        AiTraceRecorder.Exit("preview:unit_skill.validate_targets");
        var previewTargetUnits = new List<BattleUnitReadView>();
        var previewTargetUnitIds = new List<StringName>();
        var barrierBlockLines = new List<string>();
        bool hasDeterministicTargets = validation.TargetUnits.Count > 0;
        bool isRandomChain =
            skillDefinition?.CombatProfile?.TargetSelectionModeKind
            == BattleTargetSelectionMode.RandomChain;
        bool hasRandomChainCandidates =
            isRandomChain && validation.RandomChainCandidateUnitIds.Count > 0;
        if (validation.Allowed && (hasDeterministicTargets || hasRandomChainCandidates))
        {
            IReadOnlyList<CombatEffectDefinition> previewEffectDefinitions =
                _owner.CollectUnitSkillEffectDefinitions(
                    skillDefinition,
                    castVariantDefinition,
                    active_unit
                );
            BattleLayeredBarrierService layeredBarrierService =
                Runtime?._layered_barrier_service;
            if (hasDeterministicTargets)
            {
                BattleBarrierPreviewSession barrierPreviewSession =
                    layeredBarrierService?.BeginSkillBarrierPreviewSession();
                foreach (BattleUnitReadView targetUnit in validation.TargetUnits)
                {
                    BattleBarrierInteractionResult barrierResult =
                        layeredBarrierService?.PreviewSkillBarrierInteractionResult(
                            active_unit,
                            targetUnit,
                            skillDefinition,
                            previewEffectDefinitions,
                            barrierPreviewSession
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
                            previewEffectDefinitions
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
                                barrierPreviewSession
                            ) ?? new BattleBarrierInteractionResult(false, false);
                        if (!breakerResult.WouldBreakLayer)
                            continue;
                        BattleBarrierInteractionResult followUpResult =
                            layeredBarrierService?.PreviewSkillBarrierInteractionResult(
                                active_unit,
                                targetUnit,
                                skillDefinition,
                                previewEffectDefinitions,
                                barrierPreviewSession
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
        AiTraceRecorder.Enter("preview:unit_skill.copy_validation");
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
        {
            preview.AddTargetCoord(previewCoord);
        }
        AiTraceRecorder.Exit("preview:unit_skill.copy_validation");
        if (preview.allowed)
        {
            preview.hit_preview = null;
            preview.ClearDamagePreview();
            preview.ClearSaveBranchPreview();
            if (hasPreviewImpactTargets)
            {
                AiTraceRecorder.Enter("preview:unit_skill.hit_preview");
                preview.hit_preview = _owner._build_unit_skill_hit_preview(
                    active_unit,
                    previewTargetUnits,
                    skillDefinition,
                    castVariantDefinition
                );
                AiTraceRecorder.Exit("preview:unit_skill.hit_preview");
                AiTraceRecorder.Enter("preview:unit_skill.damage_preview");
                preview.SetDamagePreview(
                    BuildUnitSkillDamagePreviewTyped(
                        active_unit,
                        skillDefinition,
                        castVariantDefinition
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
                AiTraceRecorder.Exit("preview:unit_skill.damage_preview");
            }
            AiTraceRecorder.Enter("preview:unit_skill.log_lines");
            string skillLabel = _owner._format_skill_variant_label(skillDefinition, castVariantDefinition);
            foreach (string barrierBlockLine in barrierBlockLines)
                preview.AddLogLine(barrierBlockLine);
            if (isRandomChain)
            {
                preview.AddLogLine(
                    $"{active_unit.DisplayName} 可用 {skillLabel} 从 {preview.RandomChainCandidateUnitIdsTyped.Count} 个候选单位中随机连击；按当前屏障状态，其中 {previewTargetUnits.Count} 个单位可受到影响。"
                );
                if (previewTargetUnits.Count == 0)
                {
                    preview.AddLogLine("本次随机连击没有单位会受到影响。");
                }
                else
                {
                    _owner._append_damage_preview_line(preview);
                }
                AiTraceRecorder.Exit("preview:unit_skill.log_lines");
                return;
            }
            if (previewTargetUnits.Count == 0)
            {
                preview.AddLogLine(
                    $"{active_unit.DisplayName} 仍可使用 {skillLabel}，但本次没有单位会受到影响。"
                );
                AiTraceRecorder.Exit("preview:unit_skill.log_lines");
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
                    AiTraceRecorder.Exit("preview:unit_skill.log_lines");
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
            AiTraceRecorder.Exit("preview:unit_skill.log_lines");
            return;
        }
        preview.AddLogLine(
            string.IsNullOrEmpty(validation.Message) ? "技能或目标无效。" : validation.Message
        );
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
            Runtime?._target_collection_service.CollectCombatProfileTargetCoords(
                state,
                Runtime.GetGridService(),
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
        AiTraceRecorder.Enter("preview:ground_skill");
        _preview_ground_skill_command_impl(
            active_unit,
            command,
            preview,
            skillDefinition,
            castVariantDefinition
        );
        AiTraceRecorder.Exit("preview:ground_skill");
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
        castVariantDefinition ??= Runtime?._skill_resolution_rules
            ?.ResolveGroundCastVariantDefinition(
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
        AiTraceRecorder.Enter("preview:ground_skill.validate");
        BattleGroundSkillValidationResult validation =
            Runtime?.ValidateGroundSkillCommandResultTyped(
                active_unit,
                skillDefinition,
                castVariantDefinition,
                command
            ) ?? BattleGroundSkillValidationResult.Denied("地面技能目标无效。");
        AiTraceRecorder.Exit("preview:ground_skill.validate");
        AiTraceRecorder.Enter("preview:ground_skill.preview_coords");
        preview.ClearTargetCoords();
        IReadOnlyList<Vector2I> previewCoords;
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
                Runtime?.BuildGroundEffectCoordsTyped(
                    skillDefinition,
                    validation.TargetCoords,
                    sourceCoord,
                    active_unit,
                    castVariantDefinition
                ) ?? Array.Empty<Vector2I>();
            previewCoords = builtCoords;
        }
        preview.resolved_anchor_coord = validation.ResolvedAnchorCoord;
        bool allowed = validation.Allowed;
        IReadOnlyList<CombatEffectDefinition> previewUnitEffectDefinitions;
        IReadOnlyList<Vector2I> previewUnitEffectCoords;
        bool chargePathPreview = false;
        if (allowed && Runtime?._charge_resolver != null)
        {
            CombatEffectDefinition pathStepAoeEffect = Runtime._charge_resolver
                .GetChargePathStepAoeEffectDefinition(
                    castVariantDefinition,
                    skillDefinition,
                    active_unit
                );
            if (pathStepAoeEffect != null)
            {
                chargePathPreview = true;
                previewCoords = Runtime._charge_resolver.BuildChargeStepAoePreviewCoords(
                    active_unit,
                    skillDefinition,
                    validation.Direction,
                    validation.Distance,
                    pathStepAoeEffect
                );
            }
        }
        if (chargePathPreview)
        {
            previewUnitEffectDefinitions = Runtime?.CollectGroundUnitEffectDefinitionsTyped(
                    skillDefinition,
                    castVariantDefinition,
                    active_unit
                ) ?? Array.Empty<CombatEffectDefinition>();
            previewUnitEffectCoords = previewCoords;
        }
        else
        {
            BattleSkillExecutionOrchestrator.GroundEffectBarrierClipContext barrierClip = _owner.PreviewGroundEffectBarrierClipContext(
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
        {
            preview.AddTargetCoord(targetCoord);
        }
        AiTraceRecorder.Exit("preview:ground_skill.preview_coords");
        AiTraceRecorder.Enter("preview:ground_skill.collect_unit_ids");
        IReadOnlyList<StringName> previewUnitIds =
            Runtime?.CollectGroundPreviewUnitIdsTyped(
                active_unit,
                skillDefinition,
                previewUnitEffectDefinitions,
                previewUnitEffectCoords
            ) ?? Array.Empty<StringName>();
        preview.SetTargetUnitIds(previewUnitIds);
        AiTraceRecorder.Exit("preview:ground_skill.collect_unit_ids");
        if (allowed && Runtime?._charge_resolver != null)
        {
            AiTraceRecorder.Enter("preview:ground_skill.path_step_aoe");
            CombatEffectDefinition pathStepAoeEffect = Runtime._charge_resolver
                .GetChargePathStepAoeEffectDefinition(
                    castVariantDefinition,
                    skillDefinition,
                    active_unit
                );
            if (pathStepAoeEffect != null)
            {
                StringName pathStepTargetFilter =
                    Runtime?._skill_resolution_rules?.ResolveEffectTargetFilter(
                        skillDefinition,
                        pathStepAoeEffect
                    ) ?? new StringName("");
                foreach (
                    BattleUnitReadView targetUnit in _owner.CollectUnitsInCoordsReadView(
                        preview.TargetCoordsTyped
                    )
                )
                {
                    if (!_owner._is_unit_valid_for_effect(active_unit, targetUnit, pathStepTargetFilter))
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
            AiTraceRecorder.Exit("preview:ground_skill.path_step_aoe");
        }
        preview.allowed = allowed;
        if (preview.allowed)
        {
            preview.SetSaveBranchPreview(
                BuildGroundSkillGradedSaveExecutePreview(
                    active_unit,
                    skillDefinition,
                    castVariantDefinition,
                    preview.TargetUnitIdsTyped
                )
            );
        }
        AiTraceRecorder.Enter("preview:ground_skill.log_lines");
        if (preview.allowed)
        {
            preview.AddLogLine(
                $"{active_unit.DisplayName} 可使用 {_owner._format_skill_variant_label(skillDefinition, castVariantDefinition)}，预计影响 {preview.TargetCoordsTyped.Count} 个地格、{preview.TargetUnitIdsTyped.Count} 个单位。"
            );
        }
        else
        {
            preview.AddLogLine(
                string.IsNullOrEmpty(validation.Message)
                    ? "地面技能目标无效。"
                    : validation.Message
            );
        }
        AiTraceRecorder.Exit("preview:ground_skill.log_lines");
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
            Runtime?._skill_resolution_rules?.CollectGroundUnitEffectDefinitions(
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
            Runtime?._skill_resolution_rules?.ResolveEffectTargetFilter(
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
                && targetUnit.current_hp
                    <= PhantasmalKillExecutionRules.ResolveFailureExecuteThreshold(
                        profile,
                        targetMaxHp
                    );
            bool criticalFailureExecuteRisk =
                distribution.CriticalFailureBasisPoints > 0
                && targetUnit.current_hp
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
        Runtime?._report_formatter.AppendDamageResultLogLines(
            batch,
            subject_label,
            target_display_name,
            result
        );
    }

    internal BattleDamagePreviewRangeService.SkillDamagePreview? BuildUnitSkillDamagePreviewTyped(
        BattleUnitReadView active_unit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant
    )
    {
        if (!active_unit.IsValid || skillDefinition == null)
        {
            return null;
        }
        List<CombatEffectDefinition> effectDefinitions = Runtime?._skill_resolution_rules
            ?.CollectUnitSkillEffectDefinitions(
                skillDefinition,
                castVariant,
                active_unit
            ) ?? new List<CombatEffectDefinition>();
        return BattleDamagePreviewRangeService.BuildSkillDamagePreview(
            active_unit,
            effectDefinitions
        );
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

        var lookup = _targetValidationService.FindSingleExecuteEffect(
            Runtime?._skill_resolution_rules?.CollectUnitSkillEffectDefinitions(
                skillDefinition,
                castVariant,
                activeUnit
            ) ?? new List<CombatEffectDefinition>()
        );
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

        BattleState state = _owner.RtState();
        if (
            state == null
            || !state.TryGetUnitTyped(activeUnit.UnitId, out BattleUnitState sourceState)
            || !state.TryGetUnitTyped(targetUnit.UnitId, out BattleUnitState targetState)
        )
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

    private static string FormatBasisPointPercent(int basisPoints)
    {
        int clamped = Mathf.Clamp(basisPoints, 0, 10000);
        if (clamped % 100 == 0)
        {
            return $"{clamped / 100}%";
        }
        return $"{clamped / 100.0f:0.#}%";
    }
}
