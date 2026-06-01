using System;
using System.Collections.Generic;
using Godot;
using Godot.Collections;

public readonly record struct BattleBarrierInteractionResult(bool Blocked, bool Applied)
{
    public Dictionary ToDictionary() => new() { ["blocked"] = Blocked, ["applied"] = Applied };
}

public readonly record struct BattleBarrierPassageResult(bool Applied, bool Stopped)
{
    public Dictionary ToDictionary() => new() { ["applied"] = Applied, ["stopped"] = Stopped };
}

public readonly record struct BattleLayeredBarrierApplyResult(
    bool Applied,
    StringName BarrierInstanceId,
    IReadOnlyList<string> LogLines
)
{
    public static BattleLayeredBarrierApplyResult Empty() =>
        new(false, "", System.Array.Empty<string>());

    public Dictionary ToDictionary()
    {
        var logLines = new Godot.Collections.Array();
        foreach (string line in LogLines ?? System.Array.Empty<string>())
        {
            logLines.Add(line);
        }
        return new Dictionary
        {
            ["applied"] = Applied,
            ["barrier_instance_id"] = BarrierInstanceId.ToString(),
            ["log_lines"] = logLines,
        };
    }
}

[GlobalClass]
public partial class BattleBarrierService : RefCounted
{
    private const int DEFAULT_DURATION_TU = 120;
    private const int DEFAULT_SAVE_DC = 16;

    private WeakReference<BattleRuntimeModule> _runtimeRef;
    private BarrierContentRegistry _contentRegistry = new();
    private BattleEffectCategoryResolver _categoryResolver = new();
    private BattleBarrierGeometryService _geometryService = new();
    private BattleBarrierOutcomeResolver _outcomeResolver = new();
    private bool _disposed;

    public void Setup(BattleRuntimeModule runtime)
    {
        _runtimeRef = runtime != null ? new WeakReference<BattleRuntimeModule>(runtime) : null;
        _outcomeResolver.Setup(runtime);
    }

    public new void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _contentRegistry?.Dispose();
        _categoryResolver?.Dispose();
        _geometryService?.Dispose();
        _outcomeResolver?.Dispose();
        _contentRegistry = null;
        _categoryResolver = null;
        _geometryService = null;
        _outcomeResolver = null;
        _runtimeRef = null;
        base.Dispose();
    }

    public Dictionary ApplyLayeredBarrierEffect(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        SkillDef skillDef,
        CombatEffectDef effectDef,
        BattleEventBatch batch
    )
    {
        return ApplyLayeredBarrierEffectResult(sourceUnit, targetUnit, skillDef, effectDef, batch)
            .ToDictionary();
    }

    public BattleLayeredBarrierApplyResult ApplyLayeredBarrierEffectResult(
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
        var effectParams =
            effectDef.@params != null ? effectDef.@params.Duplicate(true) : new Dictionary();
        var profileId = DictStringName(effectParams, "profile_id", "");
        var profile = _contentRegistry.get_profile_def(profileId);
        if (profile == null)
            return BattleLayeredBarrierApplyResult.Empty();

        var anchorUnit = targetUnit != null ? targetUnit : sourceUnit;
        var radiusCells = Mathf.Max(
            DictInt(effectParams, "radius_cells", profile.radius_cells),
            1
        );
        var areaPattern = DictStringName(effectParams, "area_pattern", profile.area_pattern);
        if (areaPattern == "")
            areaPattern = profile.area_pattern;
        var durationTu = effectDef.duration_tu;
        if (durationTu <= 0)
            durationTu = Mathf.Max(
                DictInt(effectParams, "duration_tu", profile.duration_tu),
                0
            );
        if (durationTu <= 0)
            durationTu = DEFAULT_DURATION_TU;
        var saveDc = _ResolveBarrierSaveDc(sourceUnit, effectDef, effectParams);
        var instanceId = _BuildBarrierInstanceId(sourceUnit, skillDef, profile);
        var instance = new BattleBarrierInstanceState();
        instance.barrier_instance_id = instanceId;
        instance.profile_id = profile.profile_id;
        instance.display_name = profile.display_name;
        instance.source_unit_id = sourceUnit.unit_id;
        instance.source_skill_id = skillDef != null ? skillDef.skill_id : "";
        instance.anchor_mode = profile.anchor_mode;
        instance.anchor_coord = anchorUnit.coord;
        instance.radius_cells = radiusCells;
        instance.area_pattern = areaPattern;
        instance.remaining_tu = durationTu;
        instance.created_tu = _GetCurrentTu();
        instance.save_dc = saveDc;
        instance.catch_all_projected_effects = profile.catch_all_projected_effects;
        instance.layers = _BuildLayers(profile, saveDc);

        var barrier = instance.to_runtime_dict();
        _GetBarrierStore()[instanceId] = barrier;
        _AppendChangedCoords(batch, _GetBarrierCoords(instance));
        var line =
            $"{sourceUnit.display_name} 创造{_GetBarrierLabel(instance)}，固定在 ({anchorUnit.coord.X}, {anchorUnit.coord.Y})，半径 {radiusCells} 格。";
        _AppendLog(batch, line);
        return new BattleLayeredBarrierApplyResult(true, instanceId, new[] { line });
    }

    public void AdvanceBarrierDurations(int elapsedTu, BattleEventBatch batch)
    {
        var runtime = _ResolveRuntime();
        if (runtime == null || runtime._state == null || elapsedTu <= 0)
            return;
        var store = _GetBarrierStore();
        var expiredIds = new Godot.Collections.Array<StringName>();
        foreach (StringName barrierKey in _SortedBarrierKeys())
        {
            if (!TryReadBarrier(barrierKey, out BattleBarrierInstanceState barrier))
                continue;
            var remaining = barrier.remaining_tu - elapsedTu;
            barrier.remaining_tu = remaining;
            store[barrierKey] = barrier.to_runtime_dict();
            if (remaining <= 0)
                expiredIds.Add(barrierKey);
        }
        foreach (StringName barrierId in expiredIds)
        {
            TryReadBarrier(barrierId, out BattleBarrierInstanceState barrier);
            _AppendChangedCoords(batch, _GetBarrierCoords(barrier));
            store.Remove(barrierId);
            _AppendLog(batch, $"{_GetBarrierLabel(barrier)} {barrierId} 消散。");
        }
    }

    public Dictionary ResolveUnitBoundaryCrossing(
        BattleUnitState unitState,
        Vector2I fromCoord,
        Vector2I toCoord,
        BattleEventBatch batch
    )
    {
        return ResolveUnitBoundaryCrossingResult(unitState, fromCoord, toCoord, batch)
            .ToDictionary();
    }

    public BattleBarrierInteractionResult ResolveUnitBoundaryCrossingResult(
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
            var fromFootprint = runtime._grid_service.get_footprint_coords(
                fromCoord,
                unitState.footprint_size
            );
            var toFootprint = runtime._grid_service.get_footprint_coords(
                toCoord,
                unitState.footprint_size
            );
            var transition = _geometryService.ClassifyFootprintTransition(
                runtime._state,
                (Godot.Collections.Array)fromFootprint,
                (Godot.Collections.Array)toFootprint,
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

    public Dictionary ResolveSkillBarrierInteraction(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        SkillDef skillDef,
        Godot.Collections.Array effectDefs,
        BattleEventBatch batch
    )
    {
        return ResolveSkillBarrierInteractionResult(
                sourceUnit,
                targetUnit,
                skillDef,
                effectDefs,
                batch
            )
            .ToDictionary();
    }

    public BattleBarrierInteractionResult ResolveSkillBarrierInteractionResult(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        SkillDef skillDef,
        Godot.Collections.Array effectDefs,
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

    public Dictionary ResolveGroundBarrierInteraction(
        BattleUnitState sourceUnit,
        Vector2I targetCoord,
        SkillDef skillDef,
        Godot.Collections.Array effectDefs,
        BattleEventBatch batch
    )
    {
        return ResolveGroundBarrierInteractionResult(
                sourceUnit,
                targetCoord,
                skillDef,
                effectDefs,
                batch
            )
            .ToDictionary();
    }

    public BattleBarrierInteractionResult ResolveGroundBarrierInteractionResult(
        BattleUnitState sourceUnit,
        Vector2I targetCoord,
        SkillDef skillDef,
        Godot.Collections.Array effectDefs,
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
        Godot.Collections.Array effectDefs,
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
            var categories = _categoryResolver.ResolveCategories(skillDef, effectDefs);
            var blockingLayer = _FindFirstBlockingLayer(barrier, categories);
            if (
                blockingLayer == null
                && barrier.catch_all_projected_effects
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
            if (layer == null || layer.broken)
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
        var layerId = activeLayer.layer_id;
        List<BattleBarrierLayerState> layers = barrier.GetLayersTyped();
        foreach (BattleBarrierLayerState layer in layers)
        {
            if (layer == null || layer.layer_id != layerId)
                continue;
            layer.broken = true;
            break;
        }
        barrier.SetLayersTyped(layers);
        _GetBarrierStore()[barrierKey] = barrier.to_runtime_dict();
        _AppendChangedCoords(batch, _GetBarrierCoords(barrier));
        _AppendLog(batch, $"{_GetBarrierLabel(barrier)} 的 {_GetLayerLabel(activeLayer)} 被破解。");
    }

    private int _ResolveBarrierSaveDc(
        BattleUnitState sourceUnit,
        CombatEffectDef effectDef,
        Dictionary effectParams
    )
    {
        var resolvedDc = BattleSaveResolver.resolve_save_dc(sourceUnit, effectDef);
        if (resolvedDc > 0)
            return resolvedDc;
        var paramDc = DictInt(effectParams, "save_dc", DEFAULT_SAVE_DC);
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
            $"{skillId}:{sourceId}:{_GetCurrentTu()}:{_GetBarrierStore().Count + 1}"
        );
    }

    private Godot.Collections.Array _BuildLayers(BarrierProfileDef profile, int saveDc)
    {
        var layers = new Godot.Collections.Array();
        foreach (var layerDef in profile.get_ordered_layers())
        {
            if (layerDef == null)
                continue;
            layers.Add(layerDef.to_runtime_dict(saveDc));
        }
        return layers;
    }

    private bool _SkillBreaksLayer(SkillDef skillDef, BattleBarrierLayerState layer)
    {
        if (skillDef == null || layer == null)
            return false;
        foreach (StringName breakerSkillId in layer.breaker_skill_ids)
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
            if (layer == null || layer.broken)
                continue;
            if (_SkillBreaksLayer(skillDef, layer))
                return true;
        }
        return false;
    }

    private BattleBarrierLayerState _FindFirstBlockingLayer(
        BattleBarrierInstanceState barrier,
        Godot.Collections.Array<StringName> categories
    )
    {
        var categoryLookup = new HashSet<StringName>();
        foreach (StringName category in categories)
            categoryLookup.Add(category);
        foreach (BattleBarrierLayerState layer in barrier?.GetLayersTyped() ?? new List<BattleBarrierLayerState>())
        {
            if (layer == null || layer.broken)
                continue;
            foreach (StringName category in layer.blocked_categories)
            {
                if (categoryLookup.Contains(category))
                    return layer;
            }
        }
        return null;
    }

    private BattleBarrierLayerState _GetActiveLayer(BattleBarrierInstanceState barrier)
    {
        foreach (BattleBarrierLayerState layer in barrier?.GetLayersTyped() ?? new List<BattleBarrierLayerState>())
        {
            if (layer != null && !layer.broken)
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
        var runtime = _ResolveRuntime();
        return _geometryService.line_crosses_barrier_area(
            runtime?._state,
            sourceCoord,
            targetCoord,
            _GetBarrierCoords(barrier)
        );
    }

    private bool _IsCoordInsideBarrier(Vector2I coord, BattleBarrierInstanceState barrier)
    {
        return _geometryService.coord_inside_barrier(coord, _GetBarrierCoords(barrier));
    }

    private Godot.Collections.Array _GetBarrierCoords(BattleBarrierInstanceState barrier)
    {
        var coords = new Godot.Collections.Array();
        var runtime = _ResolveRuntime();
        if (
            runtime == null
            || runtime._state == null
            || barrier == null
            || barrier.IsEmpty
        )
            return coords;
        var radius = Mathf.Max(barrier.radius_cells, 0);
        return (Godot.Collections.Array)runtime._grid_service.get_area_coords(
            runtime._state,
            barrier.anchor_coord,
            barrier.area_pattern,
            radius,
            Vector2I.Zero
        );
    }

    private bool _IsBarrierCreator(BattleUnitState unitState, BattleBarrierInstanceState barrier)
    {
        return unitState != null
            && barrier != null
            && unitState.unit_id == barrier.source_unit_id;
    }

    private bool TryReadBarrier(StringName barrierKey, out BattleBarrierInstanceState barrier)
    {
        barrier = null;
        var store = _GetBarrierStore();
        if (store == null || barrierKey == "")
        {
            return false;
        }

        if (store.ContainsKey(barrierKey))
        {
            barrier = BattleBarrierInstanceState.from_runtime_dict(
                store[barrierKey].AsGodotDictionary()
            );
        }
        else if (store.ContainsKey(barrierKey.ToString()))
        {
            barrier = BattleBarrierInstanceState.from_runtime_dict(
                store[barrierKey.ToString()].AsGodotDictionary()
            );
        }
        else
        {
            return false;
        }
        return barrier != null && !barrier.IsEmpty;
    }

    private Dictionary _GetBarrierStore()
    {
        var runtime = _ResolveRuntime();
        if (runtime == null || runtime._state == null)
            return new Dictionary();
        var state = runtime._state;
        if (state.layered_barrier_fields == null)
            state.layered_barrier_fields = new Dictionary();
        return state.layered_barrier_fields;
    }

    private Godot.Collections.Array _SortedBarrierKeys()
    {
        var keys = new Godot.Collections.Array<StringName>();
        foreach (string keyText in ProgressionDataUtils.sorted_string_keys(_GetBarrierStore()))
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
        if (!string.IsNullOrEmpty(barrier.display_name))
            return barrier.display_name;
        string profileId = barrier.profile_id.ToString();
        return !string.IsNullOrEmpty(profileId) ? profileId : "屏障";
    }

    private string _GetLayerLabel(BattleBarrierLayerState layer)
    {
        if (layer == null)
            return "屏障层";
        if (!string.IsNullOrEmpty(layer.display_name))
            return layer.display_name;
        string layerId = layer.layer_id.ToString();
        return !string.IsNullOrEmpty(layerId) ? layerId : "屏障层";
    }

    private void _AppendChangedCoords(BattleEventBatch batch, Godot.Collections.Array coords)
    {
        var runtime = _ResolveRuntime();
        if (runtime == null || batch == null)
            return;
        runtime._append_changed_coords(batch, coords);
    }

    private void _AppendLog(BattleEventBatch batch, string line)
    {
        if (batch == null || string.IsNullOrEmpty(line))
            return;
        batch.log_lines.Add(line);
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

    private static int DictInt(Dictionary dictionary, string key, int fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        return dictionary[key].AsInt32();
    }

    private static StringName DictStringName(Dictionary dictionary, string key, StringName fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        StringName value = ProgressionDataUtils.to_string_name(dictionary[key]);
        return value != "" ? value : fallback;
    }
}
