using System;
using System.Collections.Generic;
using Godot;
using Godot.Collections;

internal readonly record struct BattleBarrierInteractionResult(bool Blocked, bool Applied)
{
}

internal readonly record struct BattleBarrierPassageResult(bool Applied, bool Stopped)
{
}

internal readonly record struct BattleLayeredBarrierApplyResult(
    bool Applied,
    StringName BarrierInstanceId,
    IReadOnlyList<string> LogLines
)
{
    internal static BattleLayeredBarrierApplyResult Empty() =>
        new(false, "", System.Array.Empty<string>());
}

internal class BattleBarrierService
{
    private const int DEFAULT_DURATION_TU = 120;
    private const int DEFAULT_SAVE_DC = 16;

    private readonly record struct BarrierApplyParams(
        StringName ProfileId,
        int RadiusCellsOverride,
        StringName AreaPatternOverride,
        int DurationTuOverride,
        int SaveDcOverride
    )
    {
        public static BarrierApplyParams FromEffect(CombatEffectDef effectDef)
        {
            return new BarrierApplyParams(
                effectDef?.GetStringNameParamTyped("profile_id", "") ?? new StringName(""),
                effectDef?.GetIntParamTyped("radius_cells", 0) ?? 0,
                effectDef?.GetStringNameParamTyped("area_pattern", "") ?? new StringName(""),
                effectDef?.GetIntParamTyped("duration_tu", 0) ?? 0,
                effectDef?.GetIntParamTyped("save_dc", DEFAULT_SAVE_DC) ?? DEFAULT_SAVE_DC
            );
        }
    }

    private WeakReference<BattleRuntimeModule> _runtimeRef;
    private BarrierContentRegistry _contentRegistry = new();
    private BattleBarrierOutcomeResolver _outcomeResolver = new();
    private bool _disposed;

    internal void Setup(BattleRuntimeModule runtime)
    {
        _runtimeRef = runtime != null ? new WeakReference<BattleRuntimeModule>(runtime) : null;
        _outcomeResolver.Setup(runtime);
    }

    internal void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _contentRegistry?.Dispose();
        _outcomeResolver?.Dispose();
        _contentRegistry = null;
        _outcomeResolver = null;
        _runtimeRef = null;
    }

    internal BattleLayeredBarrierApplyResult ApplyLayeredBarrierEffectResult(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        SkillDef skillDef,
        CombatEffectDef effectDef,
        BattleEventBatch batch
    )
    {
        var runtime = _ResolveRuntime();
        if (
            runtime == null
            || runtime._state == null
            || sourceUnit == null
            || effectDef == null
        )
            return BattleLayeredBarrierApplyResult.Empty();
        BarrierApplyParams effectParams = BarrierApplyParams.FromEffect(effectDef);
        var profileId = effectParams.ProfileId;
        var profile = _contentRegistry.GetProfileDef(profileId);
        if (profile == null)
            return BattleLayeredBarrierApplyResult.Empty();

        var anchorUnit = targetUnit != null ? targetUnit : sourceUnit;
        var radiusCells = Mathf.Max(
            effectParams.RadiusCellsOverride > 0
                ? effectParams.RadiusCellsOverride
                : profile.radius_cells,
            1
        );
        var areaPattern =
            effectParams.AreaPatternOverride != ""
                ? effectParams.AreaPatternOverride
                : profile.area_pattern;
        if (areaPattern == "")
            areaPattern = profile.area_pattern;
        var durationTu = effectDef.duration_tu;
        if (durationTu <= 0)
            durationTu = Mathf.Max(
                effectParams.DurationTuOverride > 0
                    ? effectParams.DurationTuOverride
                    : profile.duration_tu,
                0
            );
        if (durationTu <= 0)
            durationTu = DEFAULT_DURATION_TU;
        var saveDc = _ResolveBarrierSaveDc(sourceUnit, effectDef, effectParams);
        var instanceId = _BuildBarrierInstanceId(sourceUnit, skillDef, profile);
        var instance = new BattleBarrierInstanceState();
        instance.BarrierInstanceId = instanceId;
        instance.ProfileId = profile.profile_id;
        instance.DisplayName = profile.display_name;
        instance.SourceUnitId = sourceUnit.unit_id;
        instance.SourceSkillId = skillDef != null ? skillDef.skill_id : "";
        instance.AnchorMode = profile.AnchorModeKind;
        instance.AnchorCoord = anchorUnit.coord;
        instance.RadiusCells = radiusCells;
        instance.AreaPattern = areaPattern;
        instance.RemainingTu = durationTu;
        instance.CreatedTu = _GetCurrentTu();
        instance.SaveDc = saveDc;
        instance.CatchAllProjectedEffects = profile.catch_all_projected_effects;
        instance.SetLayers(_BuildLayers(profile, saveDc));

        var barrier = instance.ToRuntimeDict();
        _PutBarrier(instanceId, barrier);
        _AppendChangedCoords(batch, _GetBarrierCoords(instance));
        var line =
            $"{sourceUnit.display_name} 创造{_GetBarrierLabel(instance)}，固定在 ({anchorUnit.coord.X}, {anchorUnit.coord.Y})，半径 {radiusCells} 格。";
        _AppendLog(batch, line);
        return new BattleLayeredBarrierApplyResult(true, instanceId, new[] { line });
    }

    internal void AdvanceBarrierDurations(int elapsedTu, BattleEventBatch batch)
    {
        var runtime = _ResolveRuntime();
        if (runtime == null || runtime._state == null || elapsedTu <= 0)
            return;
        var expiredIds = new Godot.Collections.Array<StringName>();
        foreach (StringName barrierKey in _SortedBarrierKeys())
        {
            if (!TryReadBarrier(barrierKey, out BattleBarrierInstanceState barrier))
                continue;
            var remaining = barrier.RemainingTu - elapsedTu;
            barrier.RemainingTu = remaining;
            _PutBarrier(barrierKey, barrier.ToRuntimeDict());
            if (remaining <= 0)
                expiredIds.Add(barrierKey);
        }
        foreach (StringName barrierId in expiredIds)
        {
            TryReadBarrier(barrierId, out BattleBarrierInstanceState barrier);
            _AppendChangedCoords(batch, _GetBarrierCoords(barrier));
            _RemoveBarrier(barrierId);
            _AppendLog(batch, $"{_GetBarrierLabel(barrier)} {barrierId} 消散。");
        }
    }

    internal BattleBarrierInteractionResult ResolveUnitBoundaryCrossingResult(
        BattleUnitState unitState,
        Vector2I fromCoord,
        Vector2I toCoord,
        BattleEventBatch batch
    )
    {
        bool applied = false;
        var runtime = _ResolveRuntime();
        if (
            runtime == null
            || runtime._state == null
            || unitState == null
            || !unitState.is_alive
        )
            return new BattleBarrierInteractionResult(false, false);
        foreach (StringName barrierKey in _SortedBarrierKeys())
        {
            if (!TryReadBarrier(barrierKey, out BattleBarrierInstanceState barrier))
                continue;
            if (_IsBarrierCreator(unitState, barrier))
                continue;
            var barrierCoords = _GetBarrierCoords(barrier);
            var fromFootprint = runtime._grid_service.GetFootprintCoords(
                fromCoord,
                unitState.footprint_size
            );
            var toFootprint = runtime._grid_service.GetFootprintCoords(
                toCoord,
                unitState.footprint_size
            );
            var transition = BattleBarrierGeometryService.ClassifyFootprintTransition(
                fromFootprint,
                toFootprint,
                barrierCoords
            );
            if (!transition.CrossesBoundary)
                continue;
            var passageResult = _ApplyBarrierPassage(unitState, barrier, batch);
            applied = applied || passageResult.Applied;
            if (passageResult.Stopped)
            {
                return new BattleBarrierInteractionResult(true, applied);
            }
        }
        return new BattleBarrierInteractionResult(false, applied);
    }

    internal BattleBarrierInteractionResult ResolveSkillBarrierInteractionResult(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        SkillDef skillDef,
        IEnumerable<CombatEffectDef> effectDefs,
        BattleEventBatch batch
    )
    {
        if (sourceUnit == null || targetUnit == null)
            return new BattleBarrierInteractionResult(false, false);
        return _ResolveProjectedEffectBarrierInteractionResult(
            sourceUnit,
            targetUnit.coord,
            targetUnit.display_name,
            skillDef,
            effectDefs,
            batch
        );
    }

    internal BattleBarrierInteractionResult ResolveGroundBarrierInteractionResult(
        BattleUnitState sourceUnit,
        Vector2I targetCoord,
        SkillDef skillDef,
        IEnumerable<CombatEffectDef> effectDefs,
        BattleEventBatch batch
    )
    {
        return _ResolveProjectedEffectBarrierInteractionResult(
            sourceUnit,
            targetCoord,
            $"({targetCoord.X}, {targetCoord.Y})",
            skillDef,
            effectDefs,
            batch
        );
    }

    private BattleBarrierInteractionResult _ResolveProjectedEffectBarrierInteractionResult(
        BattleUnitState sourceUnit,
        Vector2I targetCoord,
        string targetLabel,
        SkillDef skillDef,
        IEnumerable<CombatEffectDef> effectDefs,
        BattleEventBatch batch
    )
    {
        var runtime = _ResolveRuntime();
        if (
            runtime == null
            || runtime._state == null
            || sourceUnit == null
        )
            return new BattleBarrierInteractionResult(false, false);
        foreach (StringName barrierKey in _SortedBarrierKeys())
        {
            if (!TryReadBarrier(barrierKey, out BattleBarrierInstanceState barrier))
                continue;
            if (!_ProjectedEffectCrossesBarrier(sourceUnit.coord, targetCoord, barrier))
                continue;
            var activeLayer = _GetActiveLayer(barrier);
            if (activeLayer == null)
                continue;
            if (_SkillBreaksLayer(skillDef, activeLayer))
            {
                _BreakActiveLayer(barrierKey, barrier, activeLayer, batch);
                return new BattleBarrierInteractionResult(true, true);
            }
            if (_SkillBreaksAnyRemainingLayer(skillDef, barrier))
            {
                _AppendLog(
                    batch,
                    $"{sourceUnit.display_name} 试图破解{_GetBarrierLabel(barrier)}，但必须先处理外层 {_GetLayerLabel(activeLayer)}。"
                );
                return new BattleBarrierInteractionResult(true, true);
            }
            var categories = BattleEffectCategoryResolver.ResolveCategories(
                skillDef,
                effectDefs
            );
            var blockingLayer = _FindFirstBlockingLayer(barrier, categories);
            if (
                blockingLayer == null
                && barrier.CatchAllProjectedEffects
            )
                blockingLayer = activeLayer;
            if (blockingLayer == null)
                continue;
            _AppendLog(
                batch,
                $"{sourceUnit.display_name} 的 {(skillDef != null ? skillDef.display_name : "效果")} 被{_GetBarrierLabel(barrier)}的 {_GetLayerLabel(blockingLayer)} 阻挡，无法影响 {targetLabel}。"
            );
            return new BattleBarrierInteractionResult(true, true);
        }
        return new BattleBarrierInteractionResult(false, false);
    }

    private BattleBarrierPassageResult _ApplyBarrierPassage(
        BattleUnitState unitState,
        BattleBarrierInstanceState barrier,
        BattleEventBatch batch
    )
    {
        if (unitState == null || barrier == null || barrier.IsEmpty)
            return new BattleBarrierPassageResult(false, false);
        _AppendLog(
            batch,
            $"{unitState.display_name} 穿过{_GetBarrierLabel(barrier)}，依次承受未破除的色层。"
        );
        bool applied = false;
        foreach (BattleBarrierLayerState layer in barrier.GetLayersTyped())
        {
            if (layer == null || layer.Broken)
                continue;
            var layerResult = _outcomeResolver.ApplyPassageOutcomesResult(
                unitState,
                barrier,
                layer,
                batch
            );
            applied = true;
            if (layerResult.Stopped || !unitState.is_alive)
            {
                return new BattleBarrierPassageResult(applied, true);
            }
        }
        return new BattleBarrierPassageResult(applied, false);
    }

    private void _BreakActiveLayer(
        StringName barrierKey,
        BattleBarrierInstanceState barrier,
        BattleBarrierLayerState activeLayer,
        BattleEventBatch batch
    )
    {
        if (barrier == null || activeLayer == null)
            return;
        var layerId = activeLayer.LayerId;
        List<BattleBarrierLayerState> layers = barrier.GetLayersTyped();
        foreach (BattleBarrierLayerState layer in layers)
        {
            if (layer == null || layer.LayerId != layerId)
                continue;
            layer.Broken = true;
            break;
        }
        barrier.SetLayers(layers);
        _PutBarrier(barrierKey, barrier.ToRuntimeDict());
        _AppendChangedCoords(batch, _GetBarrierCoords(barrier));
        _AppendLog(batch, $"{_GetBarrierLabel(barrier)} 的 {_GetLayerLabel(activeLayer)} 被破解。");
    }

    private int _ResolveBarrierSaveDc(
        BattleUnitState sourceUnit,
        CombatEffectDef effectDef,
        BarrierApplyParams effectParams
    )
    {
        var resolvedDc = BattleSaveResolver.ResolveSaveDc(sourceUnit, effectDef);
        if (resolvedDc > 0)
            return resolvedDc;
        var paramDc = effectParams.SaveDcOverride;
        return Mathf.Max(paramDc, 1);
    }

    private StringName _BuildBarrierInstanceId(
        BattleUnitState sourceUnit,
        SkillDef skillDef,
        BarrierProfileDef profile
    )
    {
        var sourceId = sourceUnit != null ? sourceUnit.unit_id.ToString() : "unknown";
        var skillId =
            skillDef != null ? skillDef.skill_id.ToString() : profile.profile_id.ToString();
        return new StringName(
            $"{skillId}:{sourceId}:{_GetCurrentTu()}:{_GetBarrierStorePayload().Count + 1}"
        );
    }

    private List<BattleBarrierLayerState> _BuildLayers(BarrierProfileDef profile, int saveDc)
    {
        var layers = new List<BattleBarrierLayerState>();
        foreach (var layerDef in profile.GetOrderedLayers())
        {
            if (layerDef == null)
                continue;
            layers.Add(_BuildLayerState(layerDef, saveDc));
        }
        return layers;
    }

    private static BattleBarrierLayerState _BuildLayerState(BarrierLayerDef layerDef, int saveDc)
    {
        var layer = new BattleBarrierLayerState
        {
            LayerId = layerDef.layer_id,
            DisplayName = layerDef.display_name,
            Order = layerDef.order,
            Broken = false,
        };
        layer.SetBlockedCategories(layerDef.blocked_categories);
        layer.SetBreakerSkillIds(layerDef.breaker_skill_ids);

        var outcomes = new List<BattleBarrierOutcomeState>();
        foreach (BarrierOutcomeDef outcomeDef in layerDef.passage_outcomes)
        {
            if (outcomeDef == null)
            {
                continue;
            }
            int resolvedSaveDc = outcomeDef.save_dc;
            if (resolvedSaveDc <= 0)
            {
                resolvedSaveDc = Mathf.Max(saveDc, 0);
            }
            outcomes.Add(
                new BattleBarrierOutcomeState
                {
                    OutcomeKind = outcomeDef.OutcomeKind,
                    Amount = outcomeDef.amount,
                    DamageTag = outcomeDef.damage_tag,
                    HalfOnSuccess = outcomeDef.half_on_success,
                    SuccessAmount = outcomeDef.success_amount,
                    SuccessDamageTag = outcomeDef.success_damage_tag,
                    FatalDamage = Mathf.Max(outcomeDef.fatal_damage, 1),
                    StatusId = outcomeDef.status_id,
                    SaveAbility = outcomeDef.save_ability,
                    SaveTag = outcomeDef.save_tag,
                    SaveDc = resolvedSaveDc,
                }
            );
        }
        layer.SetPassageOutcomes(outcomes);
        return layer;
    }

    private bool _SkillBreaksLayer(SkillDef skillDef, BattleBarrierLayerState layer)
    {
        if (skillDef == null || layer == null)
            return false;
        foreach (StringName breakerSkillId in layer.BreakerSkillIds)
        {
            if (breakerSkillId == skillDef.skill_id)
                return true;
        }
        return false;
    }

    private bool _SkillBreaksAnyRemainingLayer(
        SkillDef skillDef,
        BattleBarrierInstanceState barrier
    )
    {
        if (skillDef == null || barrier == null)
            return false;
        foreach (BattleBarrierLayerState layer in barrier.GetLayersTyped())
        {
            if (layer == null || layer.Broken)
                continue;
            if (_SkillBreaksLayer(skillDef, layer))
                return true;
        }
        return false;
    }

    private BattleBarrierLayerState _FindFirstBlockingLayer(
        BattleBarrierInstanceState barrier,
        IEnumerable<StringName> categories
    )
    {
        var categoryLookup = new HashSet<StringName>();
        if (categories != null)
        {
            foreach (StringName category in categories)
                categoryLookup.Add(category);
        }
        foreach (BattleBarrierLayerState layer in barrier?.GetLayersTyped() ?? new List<BattleBarrierLayerState>())
        {
            if (layer == null || layer.Broken)
                continue;
            foreach (StringName category in layer.BlockedCategories)
            {
                if (categoryLookup.Contains(category))
                    return layer;
            }
        }
        return null;
    }

    private static Godot.Collections.Array<CombatEffectDef> DecodeEffectDefs(
        Godot.Collections.Array effectDefs
    )
    {
        return effectDefs != null
            ? new Godot.Collections.Array<CombatEffectDef>(effectDefs)
            : new Godot.Collections.Array<CombatEffectDef>();
    }

    private BattleBarrierLayerState _GetActiveLayer(BattleBarrierInstanceState barrier)
    {
        foreach (BattleBarrierLayerState layer in barrier?.GetLayersTyped() ?? new List<BattleBarrierLayerState>())
        {
            if (layer != null && !layer.Broken)
                return layer;
        }
        return null;
    }

    private bool _ProjectedEffectCrossesBarrier(
        Vector2I sourceCoord,
        Vector2I targetCoord,
        BattleBarrierInstanceState barrier
    )
    {
        return BattleBarrierGeometryService.LineCrossesBarrierArea(
            sourceCoord,
            targetCoord,
            _GetBarrierCoords(barrier)
        );
    }

    private bool _IsCoordInsideBarrier(Vector2I coord, BattleBarrierInstanceState barrier)
    {
        return BattleBarrierGeometryService.CoordInsideBarrier(coord, _GetBarrierCoords(barrier));
    }

    private List<Vector2I> _GetBarrierCoords(BattleBarrierInstanceState barrier)
    {
        var coords = new List<Vector2I>();
        var runtime = _ResolveRuntime();
        if (
            runtime == null
            || runtime._state == null
            || barrier == null
            || barrier.IsEmpty
        )
            return coords;
        var radius = Mathf.Max(barrier.RadiusCells, 0);
        foreach (
            Vector2I coord in runtime._grid_service.GetAreaCoords(
                runtime._state,
                barrier.AnchorCoord,
                barrier.AreaPattern,
                radius,
                Vector2I.Zero
            )
        )
        {
            coords.Add(coord);
        }
        return coords;
    }

    private bool _IsBarrierCreator(BattleUnitState unitState, BattleBarrierInstanceState barrier)
    {
        return unitState != null
            && barrier != null
            && unitState.unit_id == barrier.SourceUnitId;
    }

    private bool TryReadBarrier(StringName barrierKey, out BattleBarrierInstanceState barrier)
    {
        barrier = null;
        if (barrierKey == "")
        {
            return false;
        }

        BattleState state = _GetBattleState();
        if (state == null || !state.TryGetLayeredBarrierFieldPayload(barrierKey, out Dictionary payload))
            return false;
        barrier = BattleBarrierInstanceState.FromRuntimeDict(payload);
        return barrier != null && !barrier.IsEmpty;
    }

    private BattleState _GetBattleState()
    {
        var runtime = _ResolveRuntime();
        if (runtime == null || runtime._state == null)
            return null;
        return runtime._state;
    }

    private Dictionary _GetBarrierStorePayload()
    {
        return _GetBattleState()?.ProjectLayeredBarrierFields() ?? new Dictionary();
    }

    private void _PutBarrier(StringName barrierKey, Dictionary payload)
    {
        BattleState state = _GetBattleState();
        state?.PutLayeredBarrierFieldPayload(barrierKey, payload);
    }

    private void _RemoveBarrier(StringName barrierKey)
    {
        BattleState state = _GetBattleState();
        state?.RemoveLayeredBarrierFieldPayload(barrierKey);
    }

    private Godot.Collections.Array _SortedBarrierKeys()
    {
        var keys = new Godot.Collections.Array<StringName>();
        foreach (string keyText in ProgressionDataUtils.sorted_string_keys(_GetBarrierStorePayload()))
            keys.Add(new StringName(keyText));
        return (Godot.Collections.Array)keys;
    }

    private int _GetCurrentTu()
    {
        var runtime = _ResolveRuntime();
        if (
            runtime == null
            || runtime._state == null
            || runtime._state.timeline == null
        )
            return 0;
        return runtime._state.timeline.current_tu;
    }

    private string _GetBarrierLabel(BattleBarrierInstanceState barrier)
    {
        if (barrier == null)
            return "屏障";
        if (!string.IsNullOrEmpty(barrier.DisplayName))
            return barrier.DisplayName;
        string profileId = barrier.ProfileId.ToString();
        return !string.IsNullOrEmpty(profileId) ? profileId : "屏障";
    }

    private string _GetLayerLabel(BattleBarrierLayerState layer)
    {
        if (layer == null)
            return "屏障层";
        if (!string.IsNullOrEmpty(layer.DisplayName))
            return layer.DisplayName;
        string layerId = layer.LayerId.ToString();
        return !string.IsNullOrEmpty(layerId) ? layerId : "屏障层";
    }

    private void _AppendChangedCoords(BattleEventBatch batch, IEnumerable<Vector2I> coords)
    {
        var runtime = _ResolveRuntime();
        if (runtime == null || batch == null)
            return;
        var payload = new Godot.Collections.Array();
        foreach (Vector2I coord in coords ?? new List<Vector2I>())
        {
            payload.Add(coord);
        }
        runtime._append_changed_coords(batch, payload);
    }

    private void _AppendLog(BattleEventBatch batch, string line)
    {
        if (batch == null || string.IsNullOrEmpty(line))
            return;
        batch.AddLogLine(line);
    }

    private BattleRuntimeModule _ResolveRuntime()
    {
        if (
            _runtimeRef == null
            || !_runtimeRef.TryGetTarget(out BattleRuntimeModule target)
        )
            return null;
        return target;
    }

}
