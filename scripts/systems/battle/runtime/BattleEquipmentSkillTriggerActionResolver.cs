using System;
using System.Collections.Generic;
using Godot;

internal sealed class BattleEquipmentSkillTriggerActionResolver
{
    private const int MaxDetachedFatalTriggerSkillPreviewDepth = 1;
    private BattleRuntimeModule _runtime;
    private BattleEquipmentAbilityRuntimeService _owner;

    internal void Setup(BattleRuntimeModule runtime, BattleEquipmentAbilityRuntimeService owner)
    {
        _runtime = runtime;
        _owner = owner;
    }

    internal void DisposeRuntime()
    {
        _runtime = null;
        _owner = null;
    }

    internal bool ResolveTriggerSkillAction(
        BattleEquipmentAbilityRuntimeService.ActiveEquipmentAbilityBinding activeBinding,
        EquipmentAbilityBindingDefinition binding,
        EquipmentAbilityActionDefinition action,
        TriggerSkillActionPayloadDefinition payload,
        BattleUnitState sourceUnit,
        BattleUnitState contextTarget,
        BattleState battleState,
        BattleEventBatch batch,
        BattleSaveContext saveContext,
        Action<BattleEquipmentAbilityTriggeredSkillResult> addResult
    )
    {
        BattleState state = battleState ?? _runtime?.GetState();
        if (_owner.DamageResolver == null || state == null || sourceUnit == null || payload == null)
            return false;
        SkillDefinition skillDefinition = _runtime?.GetSkillDefinitionTyped(payload.SkillId);
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (combatProfile?.Windup != null)
        {
            batch?.AddLogLine("蓄力技能不能通过装备 trigger_skill 自动触发。");
            return false;
        }
        BattleUnitState anchorUnit = _owner.ResolveEquipmentActionTarget(
            payload.TargetSelector,
            sourceUnit,
            contextTarget,
            activeBinding,
            binding,
            "",
            "",
            state
        );
        if (combatProfile == null || anchorUnit == null)
            return false;

        IReadOnlyList<BattleUnitState> targets = CollectTriggeredSkillTargets(
            state,
            sourceUnit,
            anchorUnit,
            skillDefinition,
            payload.SkillLevel
        );
        if (targets.Count == 0)
            return false;
        if (!string.IsNullOrWhiteSpace(payload.ActivationLog))
            batch?.AddLogLine(payload.ActivationLog);

        bool resolvedAny = false;
        foreach (BattleUnitState targetUnit in targets)
        {
            IReadOnlyList<CombatEffectDefinition> effects = FilterTriggeredSkillEffects(
                skillDefinition,
                sourceUnit,
                targetUnit,
                payload.SkillLevel
            );
            if (effects.Count == 0)
                continue;
            AttackEffectResolutionResult resolution = _owner.DamageResolver.ResolveEffects(
                sourceUnit,
                targetUnit,
                effects,
                DamageResolutionContext
                    .Create(
                        criticalHit: false,
                        attackSuccess: false,
                        secondaryHitSuccess: false,
                        skillId: skillDefinition.SkillId,
                        sourceSkillLevel: Math.Max(payload.SkillLevel, 1),
                        saveRollOverrides: saveContext.SaveRollOverrides
                    )
                    .WithBattleState(state)
                    .WithDamageOriginKind(BattleDamageOriginKind.EquipmentTriggeredSkill)
                    .WithDamageApplicationHookContext(
                        batch,
                        BattleEffectOrigin.EquipmentAbility()
                    )
            );
            resolvedAny = true;
            addResult?.Invoke(
                new BattleEquipmentAbilityTriggeredSkillResult
                {
                    BindingId = binding?.BindingId ?? new StringName(""),
                    ActionId = action?.ActionId ?? new StringName(""),
                    TargetUnitId = targetUnit.unit_id,
                    MergeIntoParentResult = payload.MergeIntoParentResult,
                    Resolution = resolution,
                }
            );
            batch?.AddChangedUnitId(targetUnit.unit_id);
            foreach (Vector2I coord in targetUnit.GetOccupiedCoordsTyped())
                batch?.AddChangedCoord(coord);
            AppendTriggeredSkillSaveLogs(batch, targetUnit, payload.SaveLogLabel, resolution);

            if (payload.HandleTargetDefeat && targetUnit.IsAlive() != true)
            {
                _runtime?.HandleUnitDefeatedByRuntimeEffect(
                    targetUnit,
                    sourceUnit,
                    batch,
                    $"{targetUnit.display_name} 被击倒。",
                    new BattleDefeatHandlingOptions(
                        collectLoot: false,
                        recordEnemyDefeatedAchievement: false,
                        killProvenance: BattleKillProvenance.None
                    )
                );
            }
        }
        return resolvedAny;
    }

    internal BattleEquipmentAbilityActionPreviewResult PreviewTriggerSkillAction(
        BattleEquipmentAbilityRuntimeService.ActiveEquipmentAbilityBinding activeBinding,
        EquipmentAbilityBindingDefinition binding,
        EquipmentAbilityActionDefinition action,
        TriggerSkillActionPayloadDefinition payload,
        BattleUnitState sourceUnit,
        BattleUnitState contextTarget,
        BattleState battleState,
        int triggerProbabilityBasisPoints,
        int detachedPreviewDepth = 0
    )
    {
        int probability = Math.Clamp(triggerProbabilityBasisPoints, 0, 10000);
        SkillDefinition skillDefinition = _runtime?.GetSkillDefinitionTyped(payload?.SkillId ?? "");
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        var resultBase = new
        {
            BindingId = binding?.BindingId ?? new StringName(""),
            ActionId = action?.ActionId ?? new StringName(""),
            ActionKind = action?.Kind ?? new StringName(""),
            SkillId = payload?.SkillId ?? new StringName(""),
        };
        if (detachedPreviewDepth >= MaxDetachedFatalTriggerSkillPreviewDepth)
        {
            return new BattleEquipmentAbilityActionPreviewResult
            {
                BindingId = resultBase.BindingId,
                ActionId = resultBase.ActionId,
                ActionKind = resultBase.ActionKind,
                TriggerSkillId = resultBase.SkillId,
                TriggerProbabilityBasisPoints = probability,
                Guaranteed = probability >= 10000,
                Conditional = probability > 0 && probability < 10000,
                Supported = false,
                UnsupportedReason = "trigger_skill_detached_preview_depth_limit",
            };
        }
        if (combatProfile == null)
        {
            return new BattleEquipmentAbilityActionPreviewResult
            {
                BindingId = resultBase.BindingId,
                ActionId = resultBase.ActionId,
                ActionKind = resultBase.ActionKind,
                TriggerSkillId = resultBase.SkillId,
                TriggerProbabilityBasisPoints = probability,
                Guaranteed = probability >= 10000,
                Conditional = probability > 0 && probability < 10000,
                Supported = false,
                UnsupportedReason = "trigger_skill_definition_unavailable",
            };
        }
        if (combatProfile.TargetModeKind == BattleTargetMode.Ground)
        {
            return new BattleEquipmentAbilityActionPreviewResult
            {
                BindingId = resultBase.BindingId,
                ActionId = resultBase.ActionId,
                ActionKind = resultBase.ActionKind,
                TriggerSkillId = resultBase.SkillId,
                TriggerProbabilityBasisPoints = probability,
                Guaranteed = probability >= 10000,
                Conditional = probability > 0 && probability < 10000,
                Supported = false,
                UnsupportedReason = "ground_trigger_skill_requires_full_battle_preview",
            };
        }

        BattleUnitState anchorUnit = _owner.ResolveEquipmentActionTarget(
            payload.TargetSelector,
            sourceUnit,
            contextTarget,
            activeBinding,
            binding,
            "",
            "",
            battleState
        );
        if (anchorUnit == null)
        {
            return new BattleEquipmentAbilityActionPreviewResult
            {
                BindingId = resultBase.BindingId,
                ActionId = resultBase.ActionId,
                ActionKind = resultBase.ActionKind,
                TriggerSkillId = resultBase.SkillId,
                TriggerProbabilityBasisPoints = probability,
                Guaranteed = probability >= 10000,
                Conditional = probability > 0 && probability < 10000,
                Supported = true,
                Applied = false,
            };
        }

        var damageEffects = new List<CombatEffectDefinition>();
        bool hasUnsupportedEffect = false;
        foreach (
            CombatEffectDefinition effect
            in FilterTriggeredSkillEffects(
                skillDefinition,
                sourceUnit,
                anchorUnit,
                payload.SkillLevel
            )
        )
        {
            if (effect == null)
                continue;
            if (effect.EffectKind == BattleEffectKind.Damage)
                damageEffects.Add(effect);
            else
                hasUnsupportedEffect = true;
        }
        if (hasUnsupportedEffect || damageEffects.Count == 0)
        {
            return new BattleEquipmentAbilityActionPreviewResult
            {
                BindingId = resultBase.BindingId,
                ActionId = resultBase.ActionId,
                ActionKind = resultBase.ActionKind,
                TriggerSkillId = resultBase.SkillId,
                TriggerProbabilityBasisPoints = probability,
                Guaranteed = probability >= 10000,
                Conditional = probability > 0 && probability < 10000,
                Supported = false,
                UnsupportedReason = "trigger_skill_effect_kind_not_supported",
            };
        }
        if (probability < 10000)
        {
            return new BattleEquipmentAbilityActionPreviewResult
            {
                BindingId = resultBase.BindingId,
                ActionId = resultBase.ActionId,
                ActionKind = resultBase.ActionKind,
                TriggerSkillId = resultBase.SkillId,
                TriggerProbabilityBasisPoints = probability,
                Guaranteed = false,
                Conditional = probability > 0,
                Supported = true,
                Applied = false,
            };
        }

        BattleDamagePreviewWorkingSet workingSet =
            BattleDamagePreviewWorkingSet.CreateDetached(sourceUnit, anchorUnit, battleState);
        var previews = new List<BattleDamagePreviewResult>();
        foreach (CombatEffectDefinition effect in damageEffects)
        {
            BattleDamagePreviewResult damagePreview = _owner.DamageResolver
                ?.PreviewDamageEffectOnWorkingSetTyped(
                    workingSet,
                    effect,
                    DamageResolutionContext
                        .ForSkill(skillDefinition.SkillId)
                        .WithSourceSkillLevel(Math.Max(payload.SkillLevel, 1))
                        .WithBattleState(workingSet?.BattleState)
                        .WithDamageOriginKind(BattleDamageOriginKind.EquipmentTriggeredSkill)
                        .WithDetachedPreviewDepth(detachedPreviewDepth + 1),
                    BattleDamagePreviewRollMode.Average,
                    BattleDamagePreviewSaveMode.Expected
                );
            if (damagePreview != null)
                previews.Add(damagePreview);
        }
        return new BattleEquipmentAbilityActionPreviewResult
        {
            BindingId = resultBase.BindingId,
            ActionId = resultBase.ActionId,
            ActionKind = resultBase.ActionKind,
            TriggerSkillId = resultBase.SkillId,
            TriggerProbabilityBasisPoints = probability,
            Guaranteed = true,
            Applied = previews.Count > 0,
            Supported = true,
            DamagePreviews = previews.AsReadOnly(),
        };
    }

    private IReadOnlyList<BattleUnitState> CollectTriggeredSkillTargets(
        BattleState state,
        BattleUnitState sourceUnit,
        BattleUnitState anchorUnit,
        SkillDefinition skillDefinition,
        int skillLevel
    )
    {
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (state == null || sourceUnit == null || anchorUnit == null || combatProfile == null)
            return Array.Empty<BattleUnitState>();
        if (combatProfile.TargetModeKind != BattleTargetMode.Ground)
            return anchorUnit.IsAlive()
                ? new[] { anchorUnit }
                : Array.Empty<BattleUnitState>();

        BattleTargetCollectionResult collection =
            _runtime?._target_collection_service?.CollectCombatProfileTargetCoords(
                state,
                _runtime.GetGridService(),
                sourceUnit.GetAnchorCoord(),
                combatProfile,
                new[] { anchorUnit.GetAnchorCoord() },
                sourceUnit,
                targetUnits: null,
                skillLevel: Math.Max(skillLevel, 1)
            );
        if (collection?.Handled != true || collection.TargetCoords.Count == 0)
            return Array.Empty<BattleUnitState>();
        var affectedCoords = new HashSet<Vector2I>(collection.TargetCoords);
        var targets = new List<BattleUnitState>();
        foreach (BattleUnitState candidate in state.GetUnitsTyped())
        {
            if (
                candidate?.IsAlive() != true
                || !BattleTargetTeamRules.IsUnitValidForFilter(
                    sourceUnit,
                    candidate,
                    combatProfile.TargetTeamFilter
                )
            )
            {
                continue;
            }
            bool intersects = false;
            foreach (Vector2I coord in candidate.GetOccupiedCoordsTyped())
            {
                if (affectedCoords.Contains(coord))
                {
                    intersects = true;
                    break;
                }
            }
            if (intersects)
                targets.Add(candidate);
        }
        targets.Sort(
            (left, right) => string.CompareOrdinal(
                left?.unit_id.ToString() ?? "",
                right?.unit_id.ToString() ?? ""
            )
        );
        return targets;
    }

    private static IReadOnlyList<CombatEffectDefinition> FilterTriggeredSkillEffects(
        SkillDefinition skillDefinition,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        int skillLevel
    )
    {
        int normalizedLevel = Math.Max(skillLevel, 1);
        var effects = new List<CombatEffectDefinition>();
        foreach (CombatEffectDefinition effect in skillDefinition?.CombatProfile?.EffectDefinitions ?? Array.Empty<CombatEffectDefinition>())
        {
            if (effect == null)
                continue;
            int minLevel = Math.Max(effect.MinSkillLevel, 0);
            int maxLevel = effect.MaxSkillLevel;
            if (normalizedLevel < minLevel || (maxLevel >= 0 && normalizedLevel > maxLevel))
                continue;
            StringName targetFilter = BattleTargetTeamRules.ResolveEffectTargetFilter(
                skillDefinition,
                effect
            );
            if (!BattleTargetTeamRules.IsUnitValidForFilter(sourceUnit, targetUnit, targetFilter))
                continue;
            effects.Add(effect);
        }
        return effects.Count == 0 ? Array.Empty<CombatEffectDefinition>() : effects;
    }

    private static void AppendTriggeredSkillSaveLogs(
        BattleEventBatch batch,
        BattleUnitState targetUnit,
        string label,
        AttackEffectResolutionResult resolution
    )
    {
        if (batch == null || string.IsNullOrWhiteSpace(label))
            return;
        foreach (SaveResolutionResult saveResult in resolution.SaveResults ?? Array.Empty<SaveResolutionResult>())
        {
            if (!saveResult.HasSave)
                continue;
            string outcome = saveResult.Immune ? "免疫" : saveResult.Success ? "成功" : "失败";
            batch.AddLogLine($"{targetUnit?.display_name ?? "目标"} {label}：{outcome}。");
        }
    }
}
