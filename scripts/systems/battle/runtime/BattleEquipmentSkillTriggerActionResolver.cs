using System;
using System.Collections.Generic;
using Godot;

internal sealed class BattleEquipmentSkillTriggerActionResolver
{
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

    internal void ResolveTriggerSkillAction(
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
            return;
        SkillDefinition skillDefinition = _runtime?.GetSkillDefinitionTyped(payload.SkillId);
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
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
            return;

        IReadOnlyList<BattleUnitState> targets = CollectTriggeredSkillTargets(
            state,
            sourceUnit,
            anchorUnit,
            skillDefinition,
            payload.SkillLevel
        );
        if (targets.Count == 0)
            return;
        if (!string.IsNullOrWhiteSpace(payload.ActivationLog))
            batch?.AddLogLine(payload.ActivationLog);

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
                    .WithDamageApplicationHookContext(
                        batch,
                        _runtime?.CurrentEffectOriginForContingency
                            ?? BattleEffectOrigin.PlayerCommand()
                    )
            );
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

            if (payload.HandleTargetDefeat && targetUnit.is_alive != true)
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
            return anchorUnit.is_alive
                ? new[] { anchorUnit }
                : Array.Empty<BattleUnitState>();

        BattleTargetCollectionResult collection =
            _runtime?._target_collection_service?.CollectCombatProfileTargetCoords(
                state,
                _runtime.GetGridService(),
                sourceUnit.coord,
                combatProfile,
                new[] { anchorUnit.coord },
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
                candidate?.is_alive != true
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
