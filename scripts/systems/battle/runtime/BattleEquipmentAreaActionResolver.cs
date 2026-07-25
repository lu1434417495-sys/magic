using System;
using System.Collections.Generic;
using Godot;

internal sealed class BattleEquipmentAreaActionResolver
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

    internal void ResolveScheduleAreaEffectAction(
        EquipmentAbilityBindingDefinition binding,
        EquipmentAbilityActionDefinition action,
        ScheduleAreaEffectActionPayloadDefinition payload,
        BattleEquipmentAbilityOnKillContext context
    )
    {
        BattleUnitState anchorUnit = BattleEquipmentAbilityRuntimeService.ResolveSubject(
            payload?.AnchorSelector ?? "",
            context.SourceUnit,
            context.DefeatedUnit
        );
        _runtime?._delayed_area_effect_system?.ScheduleFromEquipmentAction(
            context.SourceUnit,
            anchorUnit,
            binding,
            action,
            payload
        );
    }

    internal void ResolveApplyBattleTerrainEffectAfterCheckAction(
        EquipmentAbilityBindingDefinition binding,
        EquipmentAbilityReactionDefinition reaction,
        EquipmentAbilityActionDefinition action,
        ApplyBattleTerrainEffectAfterCheckActionPayloadDefinition payload,
        BattleEquipmentAbilityAfterHitContext context,
        BattleEquipmentAbilityAfterHitResult result
    )
    {
        BattleState state = context?.BattleState ?? _runtime?.GetState();
        BattleGridService gridService = _runtime?.GetGridService();
        BattleTerrainEffectSystem terrainEffectSystem = _runtime?._terrain_effect_system;
        BattleUnitState sourceUnit = context?.SourceUnit;
        BattleUnitState anchorUnit = BattleEquipmentAbilityRuntimeService.ResolveSubject(
            payload?.AnchorSelector == "" ? new StringName("attack_target") : payload?.AnchorSelector ?? "",
            sourceUnit,
            context?.TargetUnit
        );
        if (
            state == null
            || gridService == null
            || terrainEffectSystem == null
            || sourceUnit == null
            || anchorUnit == null
            || payload == null
            || payload.TerrainEffectId == ""
            || payload.MoveCostDelta <= 0
            || context.WeaponHpDamage <= 0
        )
        {
            return;
        }

        Vector2I coord = anchorUnit.GetAnchorCoord();
        BattleCellState cell = gridService.GetCellState(state, coord);
        if (cell == null || CellHasActiveTerrainEffect(cell, payload.TerrainEffectId))
            return;

        int naturalRoll = ResolveAbilityCheckD20();
        int modifier = sourceUnit.attribute_snapshot?.GetValue(payload.CheckAttributeModifierId) ?? 0;
        int total = naturalRoll + modifier;
        bool passed = AbilityCheckPasses(naturalRoll, total, payload);
        result?.AddRoll(
            new BattleEquipmentAbilityRollResult
            {
                BindingId = binding?.BindingId ?? "",
                ReactionId = reaction?.ReactionId ?? "",
                ActionId = action?.ActionId ?? "",
                RolledValue = total,
                Compare = payload.CheckCompare,
                Threshold = payload.CheckThreshold,
                Passed = passed,
            }
        );
        if (!passed)
            return;

        CombatEffectDefinition effectDefinition = BuildBattleTerrainEffectDefinition(payload);
        StringName fieldInstanceId = BuildTerrainEffectInstanceId(
            binding,
            action,
            coord
        );
        if (
            terrainEffectSystem.UpsertTimedTerrainEffectFromDefinition(
                coord,
                sourceUnit,
                null,
                effectDefinition,
                fieldInstanceId
            )
        )
        {
            state.MarkMovementGeometryChanged();
        }
    }

    private int ResolveAbilityCheckD20()
    {
        if (_owner.ForcedAbilityCheckRollValuesForTests.Count > 0)
            return _owner.ForcedAbilityCheckRollValuesForTests.Dequeue();
        return TrueRandomSeedService.RandiRange(1, 20);
    }

    private static bool AbilityCheckPasses(
        int naturalRoll,
        int total,
        ApplyBattleTerrainEffectAfterCheckActionPayloadDefinition payload
    )
    {
        if (payload?.NaturalTwentyAutoSuccess == true && naturalRoll == 20)
            return true;
        if (payload?.NaturalOneAutoFailure == true && naturalRoll == 1)
            return false;
        return BattleEquipmentAbilityRuntimeService.CompareInt(total, payload?.CheckCompare ?? "", payload?.CheckThreshold ?? 0);
    }

    private static bool CellHasActiveTerrainEffect(
        BattleCellState cell,
        StringName terrainEffectId
    )
    {
        foreach (
            BattleTerrainEffectState effectState in cell?.timed_terrain_effects
                ?? new List<BattleTerrainEffectState>()
        )
        {
            if (
                effectState?.effect_id == terrainEffectId
                && BattleTerrainEffectSystem.IsTerrainEffectActive(effectState)
            )
            {
                return true;
            }
        }
        return false;
    }

    private static StringName BuildTerrainEffectInstanceId(
        EquipmentAbilityBindingDefinition binding,
        EquipmentAbilityActionDefinition action,
        Vector2I coord
    )
    {
        return new StringName(
            $"{binding?.BindingId ?? new StringName("")}:{action?.ActionId ?? new StringName("")}:{coord.X}:{coord.Y}"
        );
    }

    private static CombatEffectDefinition BuildBattleTerrainEffectDefinition(
        ApplyBattleTerrainEffectAfterCheckActionPayloadDefinition payload
    )
    {
        StringName targetTeamFilter = payload?.TargetTeamFilter == ""
            ? new StringName("any")
            : payload?.TargetTeamFilter ?? "any";
        StringName stackBehavior = payload?.StackBehavior == ""
            ? new StringName("ignore_existing")
            : payload?.StackBehavior ?? "ignore_existing";
        return new CombatEffectDefinition(
            effectType: "terrain_effect",
            effectTargetTeamFilter: targetTeamFilter,
            statusId: "",
            saveFailureStatusId: "",
            terrainEffectId: payload?.TerrainEffectId ?? "",
            terrainReplaceTo: "",
            heightDelta: 0,
            requiresWeapon: false,
            addWeaponDice: false,
            preventRepeatTarget: false,
            forcedMoveMode: "",
            minSkillLevel: 0,
            maxSkillLevel: 0,
            damageTag: "",
            damageRatioPercent: 0,
            preResistanceDamageMultiplier: 1.0,
            bonusCondition: "",
            hpRatioThresholdPercent: 0,
            damageCategory: "",
            drBypassTag: "",
            diceCount: 0,
            diceSides: 0,
            diceBonus: 0,
            bonusDamageDiceCount: 0,
            bonusDamageDiceSides: 0,
            bonusDamageDiceBonus: 0,
            saveDc: 0,
            saveDcMode: "",
            saveDcSourceAbility: "",
            saveAbility: "",
            savePartialOnSuccess: false,
            saveTag: "",
            thresholdBaseValue: 0,
            thresholdLevelAnchor: 0,
            thresholdLevelBonusPerDelta: 0,
            thresholdMaxHpRatioPercent: 0,
            thresholdCapMaxHpRatioPercent: 0,
            soulFractureDurationTu: 0,
            healMultiplierPercent: 0,
            shieldGainMultiplierPercent: 0,
            appliedStatusDurationTu: 0,
            durationTu: 0,
            tickIntervalTu: 0,
            effectTags: Array.Empty<StringName>(),
            tickEffectType: "none",
            lifetimePolicy: "battle",
            moveCostDelta: Math.Max(payload?.MoveCostDelta ?? 0, 0),
            renderOverlayId: payload?.RenderOverlayId ?? "",
            overlayPriority: payload?.OverlayPriority ?? 0,
            displayName: payload?.DisplayName ?? "",
            stackBehavior: stackBehavior,
            parameters: new Dictionary<string, object>(StringComparer.Ordinal)
        );
    }

    internal bool ResolveApplyEdgeFeatureAction(
        BattleEquipmentAbilityRuntimeService.ActiveEquipmentAbilityBinding activeBinding,
        EquipmentAbilityBindingDefinition binding,
        EquipmentAbilityReactionDefinition reaction,
        EquipmentAbilityActionDefinition action,
        ApplyEdgeFeatureActionPayloadDefinition payload,
        BattleEquipmentAbilityAfterHitContext context
    )
    {
        BattleState state = context?.BattleState ?? _runtime?.GetState();
        BattleGridService gridService = _runtime?.GetGridService();
        BattleUnitState fromUnit = BattleEquipmentAbilityRuntimeService.ResolveSubject(
            payload?.FromSelector == "" ? new StringName("source") : payload?.FromSelector ?? "",
            context?.SourceUnit,
            context?.TargetUnit
        );
        BattleUnitState toUnit = BattleEquipmentAbilityRuntimeService.ResolveSubject(
            payload?.ToSelector == "" ? new StringName("attack_target") : payload?.ToSelector ?? "",
            context?.SourceUnit,
            context?.TargetUnit
        );
        if (
            state == null
            || gridService == null
            || payload == null
            || payload.DurationTu <= 0
            || fromUnit == null
            || toUnit == null
            || fromUnit.unit_id == toUnit.unit_id
        )
        {
            return false;
        }

        if (
            !TryResolveAdjacentEdgeBetweenUnits(
                fromUnit,
                toUnit,
                out Vector2I fromCoord,
                out Vector2I toCoord
            )
        )
        {
            return false;
        }
        if (
            !BattleTemporaryEdgeFeatureState.TryNormalizeEdge(
                fromCoord,
                toCoord,
                out Vector2I originCoord,
                out Vector2I direction
            )
        )
        {
            return false;
        }
        if (payload.RequireAdjacent && gridService.GetDistance(fromCoord, toCoord) != 1)
        {
            return false;
        }
        BattleEdgeFaceState existingFace = gridService.GetEdgeFace(state, fromCoord, toCoord);
        StringName stateTag = ProgressionDataUtils.to_string_name(payload.StateTag);
        if (
            existingFace != null
            && existingFace.HasFeatureFace()
            && existingFace.feature_state_tag != stateTag
        )
        {
            return false;
        }
        if (
            !BattleEquipmentAbilityStateResolver.TryConsumeOnceScope(
                activeBinding.Source,
                binding,
                reaction,
                action,
                context.SourceUnit
            )
        )
        {
            return false;
        }

        int currentTu = Math.Max(state.timeline?.current_tu ?? 0, 0);
        BattleEdgeFeatureState featureState = BuildEdgeFeatureState(payload, stateTag);
        if (featureState == null || featureState.IsEmpty())
            return false;

        return state.PutTemporaryEdgeFeature(
            new BattleTemporaryEdgeFeatureState
            {
                OriginCoord = originCoord,
                Direction = direction,
                SourceUnitId = context.SourceUnit?.unit_id ?? "",
                SourceEquipmentInstanceId = activeBinding.Source?.SourceEquipmentInstanceId ?? "",
                BindingId = binding?.BindingId ?? "",
                ActionId = action?.ActionId ?? "",
                CreatedAtTu = currentTu,
                ExpiresAtTu = currentTu + payload.DurationTu,
                Feature = featureState,
            },
            payload.RefreshExisting,
            payload.MaxActiveEdges
        );
    }

    private static BattleEdgeFeatureState BuildEdgeFeatureState(
        ApplyEdgeFeatureActionPayloadDefinition payload,
        StringName stateTag
    )
    {
        if (payload == null)
            return null;
        var featureState = new BattleEdgeFeatureState
        {
            feature_kind = payload.FeatureKind,
            render_kind = payload.RenderKind,
            render_layers = Math.Max(payload.RenderLayers, 0),
            blocks_move = payload.BlocksMove,
            blocks_occupancy = payload.BlocksOccupancy,
            blocks_los = payload.BlocksLos,
            interaction_kind = payload.InteractionKind == "" ? new StringName("none") : payload.InteractionKind,
            state_tag = stateTag,
        };
        if (
            BattleEdgeFeatureState.ToFeatureKind(featureState.feature_kind)
                == BattleEdgeFeatureKind.Unknown
            || BattleEdgeFeatureState.ToRenderKind(featureState.render_kind)
                == BattleEdgeRenderKind.Unknown
            || BattleEdgeFeatureState.ToInteractionKind(featureState.interaction_kind)
                == BattleEdgeInteractionKind.Unknown
        )
        {
            return null;
        }
        return featureState;
    }

    private static bool TryResolveAdjacentEdgeBetweenUnits(
        BattleUnitState fromUnit,
        BattleUnitState toUnit,
        out Vector2I fromCoord,
        out Vector2I toCoord
    )
    {
        fromCoord = Vector2I.Zero;
        toCoord = Vector2I.Zero;
        if (fromUnit == null || toUnit == null)
            return false;
        foreach (
            Vector2I sourceCoord in fromUnit.GetOccupiedCoordsReadViewTyped()
        )
        {
            foreach (
                Vector2I targetCoord in toUnit.GetOccupiedCoordsReadViewTyped()
            )
            {
                if (Math.Abs(sourceCoord.X - targetCoord.X) + Math.Abs(sourceCoord.Y - targetCoord.Y) != 1)
                    continue;
                fromCoord = sourceCoord;
                toCoord = targetCoord;
                return true;
            }
        }
        return false;
    }
}
