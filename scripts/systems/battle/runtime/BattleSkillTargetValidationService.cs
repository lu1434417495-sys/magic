using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

internal sealed class BattleSkillTargetValidationService
{
    private WeakReference<BattleRuntimeModule> _runtimeRef;
    private BattleSkillExecutionOrchestrator _owner;
    private BattleRandomChainSkillService _randomChainSkillService;

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
        BattleRandomChainSkillService randomChainSkillService
    )
    {
        _runtime = runtime;
        _owner = owner;
        _randomChainSkillService = randomChainSkillService;
    }

    internal void DisposeRuntime()
    {
        _runtime = null;
        _owner = null;
        _randomChainSkillService = null;
    }

    internal BattleUnitSkillValidationResult _validate_unit_skill_targets_result(
        BattleUnitState active_unit,
        BattleCommand command,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition cast_variant = null,
        bool requireAp = true
    )
    {
        BattleState state = _owner.RtState();
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (
            state == null
            || active_unit == null
            || command == null
            || skillDefinition == null
            || combatProfile == null
        )
        {
            return BattleUnitSkillValidationResult.Denied("技能或目标无效。");
        }

        bool allowRepeat = combatProfile.AllowRepeatTarget;
        GStringNameArray targetUnitIds = _normalize_target_unit_ids(command, allowRepeat);
        int skillLevel = _owner._get_unit_skill_level(
            active_unit,
            skillDefinition.SkillId
        );
        int minTargetCount = 1;
        int maxTargetCount = 1;
        if (_is_multi_unit_skill(skillDefinition))
        {
            minTargetCount = Math.Max(combatProfile.MinTargetCount, 1);
            maxTargetCount = Math.Max(
                combatProfile.GetEffectiveMaxTargetCount(skillLevel),
                minTargetCount
            );
        }
        bool isRandomChain =
            combatProfile.TargetSelectionModeKind == BattleTargetSelectionMode.RandomChain;
        if (targetUnitIds.Count == 0 && !isRandomChain)
        {
            return BattleUnitSkillValidationResult.Denied("技能或目标无效。");
        }
        if (isRandomChain)
        {
            int maxHitsPerTarget = Math.Max(
                combatProfile.MaxHitsPerTarget,
                1
            );
            List<BattleUnitState> randomChainPool = _randomChainSkillService.BuildRandomChainTargetPool(
                active_unit,
                skillDefinition,
                cast_variant,
                new Dictionary<StringName, int>(),
                maxHitsPerTarget
            );
            if (randomChainPool.Count == 0)
            {
                return BattleUnitSkillValidationResult.Denied("没有可用的随机连击目标。");
            }
            var candidateUnitIds = new List<StringName>();
            foreach (BattleUnitState candidate in randomChainPool)
            {
                if (candidate != null)
                {
                    candidateUnitIds.Add(candidate.unit_id);
                }
            }
            return BattleUnitSkillValidationResult.AllowedResult(
                System.Array.Empty<StringName>(),
                System.Array.Empty<BattleUnitState>(),
                candidateUnitIds
            );
        }
        if (targetUnitIds.Count < minTargetCount)
        {
            return BattleUnitSkillValidationResult.Denied($"至少需要选择 {minTargetCount} 个单位目标。");
        }
        if (targetUnitIds.Count > maxTargetCount)
        {
            return BattleUnitSkillValidationResult.Denied($"最多只能选择 {maxTargetCount} 个单位目标。");
        }
        if (!_is_multi_unit_skill(skillDefinition) && targetUnitIds.Count != 1)
        {
            return BattleUnitSkillValidationResult.Denied("当前技能只允许选择 1 个单位目标。");
        }
        if (combatProfile.SelectionOrderModeKind != BattleTargetSelectionOrderMode.Manual)
        {
            targetUnitIds = _sort_target_unit_ids_for_execution(targetUnitIds);
        }

        var targetUnits = new List<BattleUnitState>();
        foreach (StringName targetUnitId in targetUnitIds)
        {
            state.TryGetUnitTyped(targetUnitId, out BattleUnitState targetUnit);
            string specialValidationMessage = _get_unit_skill_target_validation_message(
                active_unit,
                targetUnit,
                skillDefinition,
                cast_variant
            );
            if (!string.IsNullOrEmpty(specialValidationMessage))
            {
                return BattleUnitSkillValidationResult.Denied(specialValidationMessage);
            }
            if (
                targetUnit == null
                || !_can_skill_target_unit(
                    active_unit,
                    targetUnit,
                    skillDefinition,
                    requireAp,
                    cast_variant
                )
            )
            {
                return BattleUnitSkillValidationResult.Denied("技能目标超出范围或不满足筛选条件。");
            }
            targetUnits.Add(targetUnit);
        }

        string sourceRetreatValidationMessage = GetSourceRetreatValidationMessage(
            active_unit,
            targetUnits,
            command,
            skillDefinition,
            cast_variant
        );
        if (!string.IsNullOrEmpty(sourceRetreatValidationMessage))
        {
            return BattleUnitSkillValidationResult.Denied(
                sourceRetreatValidationMessage
            );
        }

        IReadOnlyList<Vector2I> emptyTargetCoords = Array.Empty<Vector2I>();
        BattleTargetCollectionResult collectedTargetCoords =
            Runtime?._target_collection_service.CollectCombatProfileTargetCoords(
                state,
                Runtime.GetGridService(),
                active_unit != null
                    ? active_unit.GetAnchorCoord()
                    : new Vector2I(-1, -1),
                combatProfile,
                emptyTargetCoords,
                active_unit,
                targetUnits,
                skillLevel
            ) ?? BattleTargetCollectionResult.UnhandledResult(emptyTargetCoords);
        List<Vector2I> previewCoords = BattleSkillExecutionOrchestrator.SortCoordsTyped(collectedTargetCoords.TargetCoords);
        AppendVaultDestinationPreviewCoord(
            previewCoords,
            active_unit,
            targetUnits,
            skillDefinition,
            cast_variant
        );
        return BattleUnitSkillValidationResult.AllowedResult(
            BattleSkillExecutionOrchestrator.ToStringNameList(targetUnitIds),
            targetUnits,
            null,
            previewCoords
        );
    }

    internal BattleUnitSkillPreviewValidationResult _validate_unit_skill_preview_targets_result(
        BattleUnitReadView active_unit,
        BattleCommand command,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition cast_variant = null
    )
    {
        BattleState state = _owner.RtState();
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (
            state == null
            || !active_unit.IsValid
            || command == null
            || skillDefinition == null
            || combatProfile == null
        )
        {
            return BattleUnitSkillPreviewValidationResult.Denied("技能或目标无效。");
        }

        bool allowRepeat = combatProfile.AllowRepeatTarget;
        GStringNameArray targetUnitIds = _normalize_target_unit_ids(command, allowRepeat);
        int skillLevel = active_unit.GetKnownSkillLevel(skillDefinition.SkillId);
        int minTargetCount = 1;
        int maxTargetCount = 1;
        if (_is_multi_unit_skill(skillDefinition))
        {
            minTargetCount = Math.Max(combatProfile.MinTargetCount, 1);
            maxTargetCount = Math.Max(
                combatProfile.GetEffectiveMaxTargetCount(skillLevel),
                minTargetCount
            );
        }
        bool isRandomChain =
            combatProfile.TargetSelectionModeKind == BattleTargetSelectionMode.RandomChain;
        if (targetUnitIds.Count == 0 && !isRandomChain)
        {
            return BattleUnitSkillPreviewValidationResult.Denied("技能或目标无效。");
        }
        if (isRandomChain)
        {
            int maxHitsPerTarget = Math.Max(
                combatProfile.MaxHitsPerTarget,
                1
            );
            List<BattleUnitReadView> randomChainPool = _randomChainSkillService.BuildRandomChainTargetPool(
                active_unit,
                skillDefinition,
                cast_variant,
                maxHitsPerTarget
            );
            if (randomChainPool.Count == 0)
            {
                return BattleUnitSkillPreviewValidationResult.Denied("没有可用的随机连击目标。");
            }
            var candidateUnitIds = new List<StringName>();
            foreach (BattleUnitReadView candidate in randomChainPool)
            {
                if (candidate.IsValid)
                {
                    candidateUnitIds.Add(candidate.UnitId);
                }
            }
            return BattleUnitSkillPreviewValidationResult.AllowedResult(
                System.Array.Empty<StringName>(),
                System.Array.Empty<BattleUnitReadView>(),
                candidateUnitIds
            );
        }
        if (targetUnitIds.Count < minTargetCount)
        {
            return BattleUnitSkillPreviewValidationResult.Denied($"至少需要选择 {minTargetCount} 个单位目标。");
        }
        if (targetUnitIds.Count > maxTargetCount)
        {
            return BattleUnitSkillPreviewValidationResult.Denied($"最多只能选择 {maxTargetCount} 个单位目标。");
        }
        if (!_is_multi_unit_skill(skillDefinition) && targetUnitIds.Count != 1)
        {
            return BattleUnitSkillPreviewValidationResult.Denied("当前技能只允许选择 1 个单位目标。");
        }
        if (combatProfile.SelectionOrderModeKind != BattleTargetSelectionOrderMode.Manual)
        {
            targetUnitIds = _sort_target_unit_ids_for_execution(targetUnitIds);
        }

        BattleStateReadView stateView = state.AsReadView();
        var targetUnits = new List<BattleUnitReadView>();
        foreach (StringName targetUnitId in targetUnitIds)
        {
            BattleUnitReadView targetUnit = stateView.GetUnit(targetUnitId);
            string specialValidationMessage = _get_unit_skill_target_validation_message(
                active_unit,
                targetUnit,
                skillDefinition,
                cast_variant
            );
            if (!string.IsNullOrEmpty(specialValidationMessage))
            {
                return BattleUnitSkillPreviewValidationResult.Denied(specialValidationMessage);
            }
            if (
                !targetUnit.IsValid
                || !_can_skill_target_unit(
                    active_unit,
                    targetUnit,
                    skillDefinition,
                    true,
                    cast_variant
                )
            )
            {
                return BattleUnitSkillPreviewValidationResult.Denied("技能目标超出范围或不满足筛选条件。");
            }
            targetUnits.Add(targetUnit);
        }

        string sourceRetreatValidationMessage = GetSourceRetreatValidationMessage(
            active_unit,
            targetUnits,
            command,
            skillDefinition,
            cast_variant
        );
        if (!string.IsNullOrEmpty(sourceRetreatValidationMessage))
        {
            return BattleUnitSkillPreviewValidationResult.Denied(
                sourceRetreatValidationMessage
            );
        }

        IReadOnlyList<Vector2I> emptyTargetCoords = Array.Empty<Vector2I>();
        BattleTargetCollectionResult collectedTargetCoords =
            Runtime?._target_collection_service.CollectCombatProfileTargetCoords(
                state,
                Runtime.GetGridService(),
                active_unit.Coord,
                combatProfile,
                emptyTargetCoords,
                active_unit,
                targetUnits,
                skillLevel
            ) ?? BattleTargetCollectionResult.UnhandledResult(emptyTargetCoords);
        List<Vector2I> previewCoords = BattleSkillExecutionOrchestrator.SortCoordsTyped(collectedTargetCoords.TargetCoords);
        AppendVaultDestinationPreviewCoord(
            previewCoords,
            active_unit,
            targetUnits,
            skillDefinition,
            cast_variant
        );
        return BattleUnitSkillPreviewValidationResult.AllowedResult(
            BattleSkillExecutionOrchestrator.ToStringNameList(targetUnitIds),
            targetUnits,
            null,
            previewCoords
        );
    }

    private string GetSourceRetreatValidationMessage(
        BattleUnitState activeUnit,
        IReadOnlyList<BattleUnitState> targetUnits,
        BattleCommand command,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant
    )
    {
        IReadOnlyList<CombatEffectDefinition> effectDefinitions =
            _owner.CollectUnitSkillEffectDefinitions(
                skillDefinition,
                castVariant,
                activeUnit
            );
        CombatEffectDefinition sourceRetreatEffect =
            BattleSourceRetreatRules.FindEffect(effectDefinitions);
        if (sourceRetreatEffect == null)
        {
            return command?.source_retreat_direction != Vector2I.Zero
                ? "当前技能不接受后撤方向。"
                : "";
        }
        if (targetUnits == null || targetUnits.Count != 1 || targetUnits[0] == null)
            return "后撤技能必须选择一个单位目标。";

        BattleSourceRetreatPlan plan = Runtime?._movement_service.BuildSourceRetreatPlan(
            activeUnit,
            targetUnits[0].GetAnchorCoord(),
            command?.source_retreat_direction ?? Vector2I.Zero,
            sourceRetreatEffect.SourceRetreatDistance
        );
        return plan?.Allowed == true
            ? ""
            : plan?.Message ?? "后撤方向无效。";
    }

    private string GetSourceRetreatValidationMessage(
        BattleUnitReadView activeUnit,
        IReadOnlyList<BattleUnitReadView> targetUnits,
        BattleCommand command,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant
    )
    {
        IReadOnlyList<CombatEffectDefinition> effectDefinitions =
            _owner.CollectUnitSkillEffectDefinitions(
                skillDefinition,
                castVariant,
                activeUnit
            );
        CombatEffectDefinition sourceRetreatEffect =
            BattleSourceRetreatRules.FindEffect(effectDefinitions);
        if (sourceRetreatEffect == null)
        {
            return command?.source_retreat_direction != Vector2I.Zero
                ? "当前技能不接受后撤方向。"
                : "";
        }
        if (
            targetUnits == null
            || targetUnits.Count != 1
            || !targetUnits[0].IsValid
        )
            return "后撤技能必须选择一个单位目标。";

        BattleSourceRetreatPlan plan = Runtime?._movement_service.BuildSourceRetreatPlan(
            activeUnit,
            targetUnits[0].Coord,
            command?.source_retreat_direction ?? Vector2I.Zero,
            sourceRetreatEffect.SourceRetreatDistance
        );
        return plan?.Allowed == true
            ? ""
            : plan?.Message ?? "后撤方向无效。";
    }

    internal GStringNameArray _normalize_target_unit_ids(
        BattleCommand command,
        bool allow_repeat = false
    )
    {
        var targetUnitIds = new GStringNameArray();
        if (command == null)
        {
            return targetUnitIds;
        }
        var seenIds = new HashSet<StringName>();
        StringName singleTargetId = ProgressionDataUtils.to_string_name(
            command.target_unit_id
        );
        if (!BattleSkillExecutionOrchestrator.StringNameIsEmpty(singleTargetId))
        {
            seenIds.Add(singleTargetId);
            targetUnitIds.Add(singleTargetId);
        }
        foreach (StringName targetUnitIdValue in command.TargetUnitIdsTyped)
        {
            StringName targetUnitId = ProgressionDataUtils.to_string_name(targetUnitIdValue);
            if (
                BattleSkillExecutionOrchestrator.StringNameIsEmpty(targetUnitId)
                || (!allow_repeat && seenIds.Contains(targetUnitId))
            )
            {
                continue;
            }
            seenIds.Add(targetUnitId);
            targetUnitIds.Add(targetUnitId);
        }
        return targetUnitIds;
    }

    internal GStringNameArray _sort_target_unit_ids_for_execution(GStringNameArray target_unit_ids)
    {
        BattleState state = _owner.RtState();
        if (state == null)
        {
            return (GStringNameArray)target_unit_ids.Duplicate();
        }
        var ids = new List<StringName>();
        foreach (StringName id in target_unit_ids)
        {
            ids.Add(id);
        }
        ids.Sort(
            (a, b) =>
            {
                state.TryGetUnitTyped(a, out BattleUnitState unitA);
                state.TryGetUnitTyped(b, out BattleUnitState unitB);
                if (unitA == null || unitB == null)
                {
                    return string.CompareOrdinal(a.ToString(), b.ToString());
                }
                Vector2I ca = unitA.GetAnchorCoord();
                Vector2I cb = unitB.GetAnchorCoord();
                if (ca.Y != cb.Y)
                    return ca.Y.CompareTo(cb.Y);
                if (ca.X != cb.X)
                    return ca.X.CompareTo(cb.X);
                return string.CompareOrdinal(a.ToString(), b.ToString());
            }
        );
        var sorted = new GStringNameArray();
        foreach (StringName id in ids)
        {
            sorted.Add(id);
        }
        return sorted;
    }

    internal bool _is_multi_unit_skill(SkillDefinition skillDefinition)
    {
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        return combatProfile != null
            && combatProfile.TargetSelectionModeKind == BattleTargetSelectionMode.MultiUnit;
    }

    internal bool _can_skill_target_unit(
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDefinition skillDefinition,
        bool require_ap = true,
        CombatCastVariantDefinition cast_variant = null
    )
    {
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (
            active_unit == null
            || target_unit == null
            || skillDefinition == null
            || combatProfile == null
        )
        {
            return false;
        }
        CombatSkillResourceCosts costs = _owner._get_effective_skill_resource_costs(
            active_unit,
            skillDefinition
        );
        if (
            require_ap
            && active_unit.GetCurrentAp() < costs.ApCost
        )
        {
            return false;
        }
        bool allowDeadTargets = SkillAllowsDeadUnitTargets(skillDefinition, cast_variant);
        if (
            !_owner._is_unit_valid_for_effect(
                active_unit,
                target_unit,
                combatProfile.TargetTeamFilter,
                allowDeadTargets
            )
        )
        {
            return false;
        }
        if (
            !string.IsNullOrEmpty(
                _get_unit_skill_target_validation_message(
                    active_unit,
                    target_unit,
                    skillDefinition,
                    cast_variant
                )
            )
        )
        {
            return false;
        }
        return Runtime?.GetGridService().GetDistanceBetweenUnits(active_unit, target_unit)
            <= _owner._get_effective_skill_range(active_unit, skillDefinition);
    }

    internal bool _can_skill_target_unit(
        BattleUnitReadView active_unit,
        BattleUnitReadView target_unit,
        SkillDefinition skillDefinition,
        bool require_ap = true,
        CombatCastVariantDefinition cast_variant = null
    )
    {
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (
            !active_unit.IsValid
            || !target_unit.IsValid
            || skillDefinition == null
            || combatProfile == null
        )
        {
            return false;
        }
        CombatSkillResourceCosts costs = _owner._get_effective_skill_resource_costs(
            active_unit,
            skillDefinition
        );
        if (require_ap && active_unit.CurrentAp < costs.ApCost)
        {
            return false;
        }
        bool allowDeadTargets = SkillAllowsDeadUnitTargets(skillDefinition, cast_variant);
        if (
            !_owner._is_unit_valid_for_effect(
                active_unit,
                target_unit,
                combatProfile.TargetTeamFilter,
                allowDeadTargets
            )
        )
        {
            return false;
        }
        if (
            !string.IsNullOrEmpty(
                _get_unit_skill_target_validation_message(
                    active_unit,
                    target_unit,
                    skillDefinition,
                    cast_variant
                )
            )
        )
        {
            return false;
        }
        return Runtime?.GetGridService().GetDistanceBetweenUnits(active_unit, target_unit)
            <= _owner._get_effective_skill_range(active_unit, skillDefinition);
    }

    private static bool SkillAllowsDeadUnitTargets(
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant
    )
    {
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (
            combatProfile == null
            || !BattleTargetTeamRules.IsBeneficialFilter(combatProfile.TargetTeamFilter)
        )
        {
            return false;
        }
        foreach (CombatEffectDefinition effect in combatProfile.EffectDefinitions ?? Array.Empty<CombatEffectDefinition>())
        {
            if (IsRevivingHealEffect(effect))
                return true;
        }
        foreach (CombatEffectDefinition effect in castVariant?.EffectDefinitions ?? Array.Empty<CombatEffectDefinition>())
        {
            if (IsRevivingHealEffect(effect))
                return true;
        }
        return false;
    }

    private static bool IsRevivingHealEffect(CombatEffectDefinition effect) =>
        effect?.EffectKind is BattleEffectKind.Heal or BattleEffectKind.HealFatal;

    internal BattleUnitSkillTargetAffordance GetUnitSkillTargetAffordance(
        BattleUnitState activeUnit,
        BattleUnitState targetUnit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant = null,
        bool requireAp = true
    )
    {
        bool allowed = _can_skill_target_unit(
            activeUnit,
            targetUnit,
            skillDefinition,
            requireAp,
            castVariant
        );
        if (allowed)
        {
            return BattleUnitSkillTargetAffordance.AllowedResult();
        }
        string reason = _get_unit_skill_target_validation_message(
            activeUnit,
            targetUnit,
            skillDefinition,
            castVariant
        );
        return BattleUnitSkillTargetAffordance.Denied(
            string.IsNullOrEmpty(reason) ? "技能目标超出范围或不满足筛选条件。" : reason
        );
    }

    internal string _get_unit_skill_target_validation_message(
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition cast_variant = null
    )
    {
        string vaultMessage = GetVaultBehindTargetValidationMessage(
            active_unit,
            target_unit,
            skillDefinition,
            cast_variant
        );
        if (!string.IsNullOrEmpty(vaultMessage))
        {
            return vaultMessage;
        }
        string bodySizeOverrideMessage = _get_body_size_category_override_validation_message(
            active_unit,
            target_unit,
            skillDefinition,
            cast_variant
        );
        if (!string.IsNullOrEmpty(bodySizeOverrideMessage))
        {
            return bodySizeOverrideMessage;
        }
        string executeMessage = GetExecuteTargetValidationMessage(
            active_unit,
            target_unit,
            skillDefinition,
            cast_variant
        );
        if (!string.IsNullOrEmpty(executeMessage))
        {
            return executeMessage;
        }
        if (!BattleTemporalStatusService.CanTargetTimeStasis(target_unit, skillDefinition))
        {
            return "目标处于时间静滞，只有时间系解控技能能够作用。";
        }
        string targetStatusRequirementMessage = GetTargetStatusRequirementValidationMessage(
            active_unit,
            target_unit,
            skillDefinition,
            cast_variant
        );
        if (!string.IsNullOrEmpty(targetStatusRequirementMessage))
        {
            return targetStatusRequirementMessage;
        }
        StringName skillId = skillDefinition?.SkillId ?? new StringName("");
        if (
            _owner._is_black_crown_seal_skill(skillId)
            && !_owner._is_black_crown_seal_target_eligible(active_unit, target_unit)
        )
        {
            return "黑冠封印只能对 boss 施放。";
        }
        if (_owner._is_doom_shift_skill(skillId))
        {
            if (target_unit == null || active_unit == null)
            {
                return "断命换位的目标无效。";
            }
            if (target_unit.unit_id == active_unit.unit_id)
            {
                return "断命换位不能以自己为目标。";
            }
        }
        if (
            _owner._is_crown_break_skill(skillId)
            && !_owner._is_crown_break_target_eligible(active_unit, target_unit)
        )
        {
            return "折冠只能对已被黑星烙印的 elite / boss 施放。";
        }
        if (
            _owner._is_doom_sentence_skill(skillId)
            && !_owner._is_doom_sentence_target_eligible(active_unit, target_unit)
        )
        {
            return "厄命宣判只能对 elite / boss 施放。";
        }
        return "";
    }

    internal string _get_unit_skill_target_validation_message(
        BattleUnitReadView active_unit,
        BattleUnitReadView target_unit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition cast_variant = null
    )
    {
        string vaultMessage = GetVaultBehindTargetValidationMessage(
            active_unit,
            target_unit,
            skillDefinition,
            cast_variant
        );
        if (!string.IsNullOrEmpty(vaultMessage))
        {
            return vaultMessage;
        }
        string bodySizeOverrideMessage = _get_body_size_category_override_validation_message(
            active_unit,
            target_unit,
            skillDefinition,
            cast_variant
        );
        if (!string.IsNullOrEmpty(bodySizeOverrideMessage))
        {
            return bodySizeOverrideMessage;
        }
        string executeMessage = GetExecuteTargetValidationMessage(
            active_unit,
            target_unit,
            skillDefinition,
            cast_variant
        );
        if (!string.IsNullOrEmpty(executeMessage))
        {
            return executeMessage;
        }
        if (
            target_unit.HasStatusEffect(BattleStatusSemanticTable.STATUS_TIME_STASIS)
            && !BattleTemporalStatusService.IsTemporalReleaseSkill(skillDefinition)
        )
        {
            return "目标处于时间静滞，只有时间系解控技能能够作用。";
        }
        string targetStatusRequirementMessage = GetTargetStatusRequirementValidationMessage(
            active_unit,
            target_unit,
            skillDefinition,
            cast_variant
        );
        if (!string.IsNullOrEmpty(targetStatusRequirementMessage))
        {
            return targetStatusRequirementMessage;
        }
        StringName skillId = skillDefinition?.SkillId ?? new StringName("");
        if (_owner._is_black_crown_seal_skill(skillId))
        {
            if (
                !_owner._is_unit_valid_for_effect(active_unit, target_unit, BattleTypedNames.TargetFilterEnemy)
                || !target_unit.IsBossTarget
            )
            {
                return "黑冠封印只能对 boss 施放。";
            }
        }
        if (_owner._is_doom_shift_skill(skillId))
        {
            if (!target_unit.IsValid || !active_unit.IsValid)
            {
                return "断命换位的目标无效。";
            }
            if (target_unit.UnitId == active_unit.UnitId)
            {
                return "断命换位不能以自己为目标。";
            }
        }
        if (_owner._is_crown_break_skill(skillId))
        {
            if (
                !_owner._is_unit_valid_for_effect(active_unit, target_unit, BattleTypedNames.TargetFilterEnemy)
                || !target_unit.HasStatusEffect("black_star_brand_elite")
            )
            {
                return "折冠只能对已被黑星烙印的 elite / boss 施放。";
            }
        }
        if (_owner._is_doom_sentence_skill(skillId))
        {
            if (
                !_owner._is_unit_valid_for_effect(active_unit, target_unit, BattleTypedNames.TargetFilterEnemy)
                || !target_unit.IsEliteOrBossTarget
            )
            {
                return "厄命宣判只能对 elite / boss 施放。";
            }
        }
        return "";
    }


    private string GetVaultBehindTargetValidationMessage(
        BattleUnitState activeUnit,
        BattleUnitState targetUnit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant
    )
    {
        if (!HasVaultBehindTargetEffect(skillDefinition, castVariant, activeUnit))
            return "";
        BattleVaultBehindTargetPlan plan = BattleVaultBehindTargetRules.BuildPlan(
            _owner.RtState(),
            Runtime?.GetGridService(),
            Runtime?._layered_barrier_service,
            activeUnit,
            targetUnit
        );
        return plan.Allowed ? "" : plan.Message;
    }

    private string GetVaultBehindTargetValidationMessage(
        BattleUnitReadView activeUnit,
        BattleUnitReadView targetUnit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant
    )
    {
        if (!HasVaultBehindTargetEffect(skillDefinition, castVariant, activeUnit))
            return "";
        BattleVaultBehindTargetPlan plan = BattleVaultBehindTargetRules.BuildPlan(
            _owner.RtState(),
            Runtime?.GetGridService(),
            Runtime?._layered_barrier_service,
            activeUnit,
            targetUnit
        );
        return plan.Allowed ? "" : plan.Message;
    }

    private bool HasVaultBehindTargetEffect(
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant,
        BattleUnitState activeUnit
    )
    {
        foreach (
            CombatEffectDefinition effectDefinition in _owner.CollectUnitSkillEffectDefinitions(
                skillDefinition,
                castVariant,
                activeUnit
            )
        )
        {
            if (effectDefinition?.EffectKind == BattleEffectKind.VaultBehindTarget)
                return true;
        }
        return false;
    }

    private bool HasVaultBehindTargetEffect(
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant,
        BattleUnitReadView activeUnit
    )
    {
        foreach (
            CombatEffectDefinition effectDefinition in _owner.CollectUnitSkillEffectDefinitions(
                skillDefinition,
                castVariant,
                activeUnit
            )
        )
        {
            if (effectDefinition?.EffectKind == BattleEffectKind.VaultBehindTarget)
                return true;
        }
        return false;
    }

    private void AppendVaultDestinationPreviewCoord(
        List<Vector2I> previewCoords,
        BattleUnitState activeUnit,
        IReadOnlyList<BattleUnitState> targetUnits,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant
    )
    {
        if (
            previewCoords == null
            || targetUnits == null
            || targetUnits.Count != 1
            || !HasVaultBehindTargetEffect(skillDefinition, castVariant, activeUnit)
        )
            return;
        BattleVaultBehindTargetPlan plan = BattleVaultBehindTargetRules.BuildPlan(
            _owner.RtState(),
            Runtime?.GetGridService(),
            Runtime?._layered_barrier_service,
            activeUnit,
            targetUnits[0]
        );
        if (plan.Allowed && !previewCoords.Contains(plan.Destination))
            previewCoords.Add(plan.Destination);
        previewCoords.Sort(
            (left, right) =>
                left.Y != right.Y ? left.Y.CompareTo(right.Y) : left.X.CompareTo(right.X)
        );
    }

    private void AppendVaultDestinationPreviewCoord(
        List<Vector2I> previewCoords,
        BattleUnitReadView activeUnit,
        IReadOnlyList<BattleUnitReadView> targetUnits,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant
    )
    {
        if (
            previewCoords == null
            || targetUnits == null
            || targetUnits.Count != 1
            || !HasVaultBehindTargetEffect(skillDefinition, castVariant, activeUnit)
        )
            return;
        BattleVaultBehindTargetPlan plan = BattleVaultBehindTargetRules.BuildPlan(
            _owner.RtState(),
            Runtime?.GetGridService(),
            Runtime?._layered_barrier_service,
            activeUnit,
            targetUnits[0]
        );
        if (plan.Allowed && !previewCoords.Contains(plan.Destination))
            previewCoords.Add(plan.Destination);
        previewCoords.Sort(
            (left, right) =>
                left.Y != right.Y ? left.Y.CompareTo(right.Y) : left.X.CompareTo(right.X)
        );
    }

    private string GetTargetStatusRequirementValidationMessage(
        BattleUnitState activeUnit,
        BattleUnitState targetUnit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant
    )
    {
        if (activeUnit == null || targetUnit == null || skillDefinition == null)
            return "";

        bool sawRelevantRequirement = false;
        CombatEffectDefinition firstFailedRequirement = null;
        bool allowDeadTargets = SkillAllowsDeadUnitTargets(skillDefinition, castVariant);
        foreach (
            CombatEffectDefinition effectDefinition in _owner.CollectUnitSkillEffectDefinitions(
                skillDefinition,
                castVariant,
                activeUnit
            )
        )
        {
            if (effectDefinition == null)
                continue;
            if (
                !_owner._is_unit_valid_for_effect(
                    activeUnit,
                    targetUnit,
                    _owner.ResolveEffectTargetFilter(skillDefinition, effectDefinition),
                    allowDeadTargets
                )
            )
            {
                continue;
            }

            StringName requiredStatusId = ProgressionDataUtils.to_string_name(
                effectDefinition.RequiredTargetStatusId
            );
            if (requiredStatusId == "")
                return "";

            sawRelevantRequirement = true;
            firstFailedRequirement ??= effectDefinition;
            if (TargetStatusRequirementPasses(
                activeUnit,
                targetUnit,
                effectDefinition,
                requiredStatusId
            ))
            {
                return "";
            }
        }

        return sawRelevantRequirement
            ? BuildTargetStatusRequirementMessage(firstFailedRequirement)
            : "";
    }

    private string GetTargetStatusRequirementValidationMessage(
        BattleUnitReadView activeUnit,
        BattleUnitReadView targetUnit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant
    )
    {
        if (!activeUnit.IsValid || !targetUnit.IsValid || skillDefinition == null)
            return "";

        bool sawRelevantRequirement = false;
        CombatEffectDefinition firstFailedRequirement = null;
        bool allowDeadTargets = SkillAllowsDeadUnitTargets(skillDefinition, castVariant);
        foreach (
            CombatEffectDefinition effectDefinition in _owner.CollectUnitSkillEffectDefinitions(
                skillDefinition,
                castVariant,
                activeUnit
            )
        )
        {
            if (effectDefinition == null)
                continue;
            if (
                !_owner._is_unit_valid_for_effect(
                    activeUnit,
                    targetUnit,
                    _owner.ResolveEffectTargetFilter(skillDefinition, effectDefinition),
                    allowDeadTargets
                )
            )
            {
                continue;
            }

            StringName requiredStatusId = ProgressionDataUtils.to_string_name(
                effectDefinition.RequiredTargetStatusId
            );
            if (requiredStatusId == "")
                return "";

            sawRelevantRequirement = true;
            firstFailedRequirement ??= effectDefinition;
            if (TargetStatusRequirementPasses(
                activeUnit,
                targetUnit,
                effectDefinition,
                requiredStatusId
            ))
            {
                return "";
            }
        }

        return sawRelevantRequirement
            ? BuildTargetStatusRequirementMessage(firstFailedRequirement)
            : "";
    }

    private static bool TargetStatusRequirementPasses(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        CombatEffectDefinition effectDefinition,
        StringName requiredStatusId
    )
    {
        BattleStatusEffectState statusEntry = targetUnit?.GetStatusEffect(requiredStatusId);
        if (statusEntry == null)
            return false;
        int requiredStacks = Math.Max(effectDefinition?.RequiredTargetStatusMinStacks ?? 0, 1);
        if (Math.Max(statusEntry.stacks, 0) < requiredStacks)
            return false;
        return TargetStatusSourceRequirementPasses(
            effectDefinition,
            statusEntry.source_unit_id,
            sourceUnit?.unit_id ?? new StringName("")
        );
    }

    private static bool TargetStatusRequirementPasses(
        BattleUnitReadView sourceUnit,
        BattleUnitReadView targetUnit,
        CombatEffectDefinition effectDefinition,
        StringName requiredStatusId
    )
    {
        if (!targetUnit.HasStatusEffect(requiredStatusId))
            return false;
        int requiredStacks = Math.Max(effectDefinition?.RequiredTargetStatusMinStacks ?? 0, 1);
        if (Math.Max(targetUnit.GetStatusStacks(requiredStatusId), 0) < requiredStacks)
            return false;
        return TargetStatusSourceRequirementPasses(
            effectDefinition,
            targetUnit.GetStatusSourceUnitId(requiredStatusId),
            sourceUnit.UnitId
        );
    }

    private static bool TargetStatusSourceRequirementPasses(
        CombatEffectDefinition effectDefinition,
        StringName statusSourceUnitId,
        StringName sourceUnitId
    )
    {
        StringName sourceSelector = ProgressionDataUtils.to_string_name(
            effectDefinition?.RequiredTargetStatusSourceSelector ?? new StringName("")
        );
        if (sourceSelector == "")
            return true;
        if (
            sourceSelector == "source"
            || sourceSelector == "attacker"
            || sourceSelector == "owner"
            || sourceSelector == "caster"
        )
        {
            return sourceUnitId != "" && ProgressionDataUtils.to_string_name(statusSourceUnitId) == sourceUnitId;
        }
        return false;
    }

    private static string BuildTargetStatusRequirementMessage(
        CombatEffectDefinition effectDefinition
    )
    {
        StringName requiredStatusId = ProgressionDataUtils.to_string_name(
            effectDefinition?.RequiredTargetStatusId ?? new StringName("")
        );
        int requiredStacks = Math.Max(effectDefinition?.RequiredTargetStatusMinStacks ?? 0, 1);
        return $"目标缺少所需状态 {requiredStatusId} 至少 {requiredStacks} 层。";
    }

    private string GetExecuteTargetValidationMessage(
        BattleUnitState activeUnit,
        BattleUnitState targetUnit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant
    )
    {
        var lookup = FindSingleExecuteEffect(
            Runtime?._skill_resolution_rules?.CollectUnitSkillEffectDefinitions(
                skillDefinition,
                castVariant,
                activeUnit
            ) ?? new List<CombatEffectDefinition>()
        );
        if (!string.IsNullOrEmpty(lookup.ErrorMessage))
        {
            return lookup.ErrorMessage;
        }
        if (lookup.Effect == null)
        {
            return "";
        }
        if (targetUnit == null)
        {
            return "律令死亡目标无效。";
        }
        if (!targetUnit.IsAlive())
        {
            return "";
        }
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (
            combatProfile == null
            || !_owner._is_unit_valid_for_effect(activeUnit, targetUnit, combatProfile.TargetTeamFilter)
        )
        {
            return "";
        }
        BattleExecutePlan plan = BattleExecutionRules.BuildExecutePlan(
            activeUnit,
            targetUnit,
            BattleExecutionRuleParams.FromEffect(lookup.Effect, skillDefinition.SkillId)
        );
        return plan.CanExecute
            ? ""
            : $"{targetUnit.display_name} 当前生命高于律令死亡阈值。";
    }

    private string GetExecuteTargetValidationMessage(
        BattleUnitReadView activeUnit,
        BattleUnitReadView targetUnit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant
    )
    {
        var lookup = FindSingleExecuteEffect(
            Runtime?._skill_resolution_rules?.CollectUnitSkillEffectDefinitions(
                skillDefinition,
                castVariant,
                activeUnit
            ) ?? new List<CombatEffectDefinition>()
        );
        if (!string.IsNullOrEmpty(lookup.ErrorMessage))
        {
            return lookup.ErrorMessage;
        }
        if (lookup.Effect == null)
        {
            return "";
        }
        if (!targetUnit.IsValid)
        {
            return "律令死亡目标无效。";
        }
        if (!targetUnit.IsAlive)
        {
            return "";
        }
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (
            combatProfile == null
            || !_owner._is_unit_valid_for_effect(activeUnit, targetUnit, combatProfile.TargetTeamFilter)
        )
        {
            return "";
        }
        BattleExecutePlan plan = BattleExecutionRules.BuildExecutePlan(
            activeUnit,
            targetUnit,
            BattleExecutionRuleParams.FromEffect(lookup.Effect, skillDefinition.SkillId)
        );
        return plan.CanExecute
            ? ""
            : $"{targetUnit.DisplayName} 当前生命高于律令死亡阈值。";
    }

    internal (CombatEffectDefinition Effect, string ErrorMessage) FindSingleExecuteEffect(
        IReadOnlyList<CombatEffectDefinition> effectDefinitions
    )
    {
        CombatEffectDefinition found = null;
        foreach (
            CombatEffectDefinition effectDefinition in effectDefinitions
                ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (effectDefinition?.EffectKind != BattleEffectKind.Execute)
            {
                continue;
            }
            if (found != null)
            {
                return (null, "律令死亡效果配置无效。");
            }
            found = effectDefinition;
        }
        return (found, "");
    }

    internal string _get_body_size_category_override_validation_message(
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition cast_variant = null
    )
    {
        BattleState state = _owner.RtState();
        if (state == null || target_unit == null || skillDefinition == null)
        {
            return "";
        }
        BattleGridService gridService = Runtime?.GetGridService();
        if (gridService == null)
        {
            return "";
        }
        foreach (
            CombatEffectDefinition effectDefinition in _owner.CollectUnitSkillEffectDefinitions(
                skillDefinition,
                cast_variant,
                active_unit
            )
        )
        {
            if (
                effectDefinition == null
                || effectDefinition.EffectKind != BattleEffectKind.BodySizeCategoryOverride
            )
            {
                continue;
            }
            StringName targetCategory = effectDefinition.BodySizeCategory;
            if (!BodySizeContentRules.IsValidBodySizeCategory(targetCategory))
            {
                continue;
            }
            Vector2I targetFootprint = BodySizeContentRules.GetFootprintForCategory(targetCategory);
            if (
                !gridService
                    .CanPlaceFootprint(
                        state,
                        target_unit.GetAnchorCoord(),
                        targetFootprint,
                        target_unit.unit_id,
                        target_unit
                    )
            )
            {
                return $"{target_unit.display_name} 周围空间不足，无法改变体型。";
            }
        }
        return "";
    }

    internal string _get_body_size_category_override_validation_message(
        BattleUnitReadView active_unit,
        BattleUnitReadView target_unit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition cast_variant = null
    )
    {
        BattleState state = _owner.RtState();
        if (state == null || !target_unit.IsValid || skillDefinition == null)
        {
            return "";
        }
        BattleGridService gridService = Runtime?.GetGridService();
        if (gridService == null)
        {
            return "";
        }
        foreach (
            CombatEffectDefinition effectDefinition in _owner.CollectUnitSkillEffectDefinitions(
                skillDefinition,
                cast_variant,
                active_unit
            )
        )
        {
            if (
                effectDefinition == null
                || effectDefinition.EffectKind != BattleEffectKind.BodySizeCategoryOverride
            )
            {
                continue;
            }
            StringName targetCategory = effectDefinition.BodySizeCategory;
            if (!BodySizeContentRules.IsValidBodySizeCategory(targetCategory))
            {
                continue;
            }
            Vector2I targetFootprint = BodySizeContentRules.GetFootprintForCategory(targetCategory);
            if (
                !gridService
                    .CanPlaceFootprint(
                        state,
                        target_unit.Coord,
                        targetFootprint,
                        target_unit.UnitId,
                        target_unit
                    )
            )
            {
                return $"{target_unit.DisplayName} 周围空间不足，无法改变体型。";
            }
        }
        return "";
    }


}
