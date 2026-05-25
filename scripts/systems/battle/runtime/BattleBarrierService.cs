using System;
using Godot;
using Godot.Collections;

[GlobalClass]
public partial class BattleBarrierService : RefCounted
{
    private const int DEFAULT_DURATION_TU = 120;
    private const int DEFAULT_SAVE_DC = 16;

    private WeakReference<GodotObject> _runtimeRef;
    private BarrierContentRegistry _contentRegistry = new();
    private BattleEffectCategoryResolver _categoryResolver = new();
    private BattleBarrierGeometryService _geometryService = new();
    private BattleBarrierOutcomeResolver _outcomeResolver = new();

    public void Setup(GodotObject runtime)
    {
        _runtimeRef = runtime != null ? new WeakReference<GodotObject>(runtime) : null;
        _outcomeResolver.Setup(runtime);
    }

    public new void Dispose()
    {
        _outcomeResolver.Dispose();
        _runtimeRef = null;
    }

    public Dictionary ApplyLayeredBarrierEffect(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        SkillDef skillDef,
        CombatEffectDef effectDef,
        BattleEventBatch batch
    )
    {
        var result = new Dictionary
        {
            ["applied"] = false,
            ["barrier_instance_id"] = "",
            ["log_lines"] = new Godot.Collections.Array(),
        };
        var runtime = _ResolveRuntime();
        if (runtime == null || runtime.Get("_state").As<BattleState>() == null || sourceUnit == null || effectDef == null)
            return result;
        var effectParams = effectDef.@params != null ? effectDef.@params.Duplicate(true) : new Dictionary();
        var profileId = ProgressionDataUtils.to_string_name(DictionaryGet(effectParams, "profile_id", ""));
        var profile = _contentRegistry.get_profile_def(profileId);
        if (profile == null)
            return result;

        var anchorUnit = targetUnit != null ? targetUnit : sourceUnit;
        var radiusCells = Mathf.Max((int)DictionaryGet(effectParams, "radius_cells", profile.radius_cells), 1);
        var areaPattern = ProgressionDataUtils.to_string_name(DictionaryGet(effectParams, "area_pattern", profile.area_pattern));
        if (areaPattern == "")
            areaPattern = profile.area_pattern;
        var durationTu = effectDef.duration_tu;
        if (durationTu <= 0)
            durationTu = Mathf.Max((int)DictionaryGet(effectParams, "duration_tu", profile.duration_tu), 0);
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
        _AppendChangedCoords(batch, _GetBarrierCoords(barrier));
        var line = $"{sourceUnit.display_name} 创造{_GetBarrierLabel(barrier)}，固定在 ({anchorUnit.coord.X}, {anchorUnit.coord.Y})，半径 {radiusCells} 格。";
        _AppendLog(batch, line);
        result["applied"] = true;
        result["barrier_instance_id"] = instanceId;
        result["log_lines"] = new Godot.Collections.Array { line };
        return result;
    }

    public void AdvanceBarrierDurations(int elapsedTu, BattleEventBatch batch)
    {
        var runtime = _ResolveRuntime();
        if (runtime == null || runtime.Get("_state").As<BattleState>() == null || elapsedTu <= 0)
            return;
        var store = _GetBarrierStore();
        var expiredIds = new Godot.Collections.Array<StringName>();
        foreach (StringName barrierKey in _SortedBarrierKeys())
        {
            var barrier = DictionaryGet(store, barrierKey, new Dictionary()).AsGodotDictionary();
            if (barrier.Count == 0)
                continue;
            var remaining = (int)DictionaryGet(barrier, "remaining_tu", 0) - elapsedTu;
            barrier["remaining_tu"] = remaining;
            store[barrierKey] = barrier;
            if (remaining <= 0)
                expiredIds.Add(barrierKey);
        }
        foreach (StringName barrierId in expiredIds)
        {
            var barrier = DictionaryGet(store, barrierId, new Dictionary()).AsGodotDictionary();
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
        var result = new Dictionary
        {
            ["blocked"] = false,
            ["applied"] = false,
        };
        var runtime = _ResolveRuntime();
        if (runtime == null || runtime.Get("_state").As<BattleState>() == null || unitState == null || !unitState.is_alive)
            return result;
        foreach (StringName barrierKey in _SortedBarrierKeys())
        {
            var barrier = DictionaryGet(_GetBarrierStore(), barrierKey, new Dictionary()).AsGodotDictionary();
            if (barrier.Count == 0 || _IsBarrierCreator(unitState, barrier))
                continue;
            var barrierCoords = _GetBarrierCoords(barrier);
            var gridService = runtime.Get("_grid_service").AsGodotObject();
            var state = runtime.Get("_state").As<BattleState>();
            var fromFootprint = gridService.Call("get_footprint_coords", fromCoord, unitState.footprint_size).AsGodotArray();
            var toFootprint = gridService.Call("get_footprint_coords", toCoord, unitState.footprint_size).AsGodotArray();
            var transition = _geometryService.classify_footprint_transition(state, fromFootprint, toFootprint, barrierCoords);
            if (!DictionaryGet(transition, "crosses_boundary", false).AsBool())
                continue;
            var passageResult = _ApplyBarrierPassage(unitState, barrier, batch);
            result["applied"] = DictionaryGet(result, "applied", false).AsBool() || DictionaryGet(passageResult, "applied", false).AsBool();
            if (DictionaryGet(passageResult, "stopped", false).AsBool())
            {
                result["blocked"] = true;
                return result;
            }
        }
        return result;
    }

    public Dictionary ResolveSkillBarrierInteraction(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        SkillDef skillDef,
        Godot.Collections.Array effectDefs,
        BattleEventBatch batch
    )
    {
        if (sourceUnit == null || targetUnit == null)
            return new Dictionary { ["blocked"] = false, ["applied"] = false };
        return _ResolveProjectedEffectBarrierInteraction(
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
        return _ResolveProjectedEffectBarrierInteraction(
            sourceUnit,
            targetCoord,
            $"({targetCoord.X}, {targetCoord.Y})",
            skillDef,
            effectDefs,
            batch
        );
    }

    private Dictionary _ResolveProjectedEffectBarrierInteraction(
        BattleUnitState sourceUnit,
        Vector2I targetCoord,
        string targetLabel,
        SkillDef skillDef,
        Godot.Collections.Array effectDefs,
        BattleEventBatch batch
    )
    {
        var result = new Dictionary
        {
            ["blocked"] = false,
            ["applied"] = false,
        };
        var runtime = _ResolveRuntime();
        if (runtime == null || runtime.Get("_state").As<BattleState>() == null || sourceUnit == null)
            return result;
        foreach (StringName barrierKey in _SortedBarrierKeys())
        {
            var barrier = DictionaryGet(_GetBarrierStore(), barrierKey, new Dictionary()).AsGodotDictionary();
            if (barrier.Count == 0)
                continue;
            if (!_ProjectedEffectCrossesBarrier(sourceUnit.coord, targetCoord, barrier))
                continue;
            var activeLayer = _GetActiveLayer(barrier);
            if (activeLayer.Count == 0)
                continue;
            if (_SkillBreaksLayer(skillDef, activeLayer))
            {
                _BreakActiveLayer(barrierKey, barrier, activeLayer, batch);
                result["blocked"] = true;
                result["applied"] = true;
                return result;
            }
            if (_SkillBreaksAnyRemainingLayer(skillDef, barrier))
            {
                _AppendLog(batch, $"{sourceUnit.display_name} 试图破解{_GetBarrierLabel(barrier)}，但必须先处理外层 {_GetLayerLabel(activeLayer)}。");
                result["blocked"] = true;
                result["applied"] = true;
                return result;
            }
            var categories = _categoryResolver.resolve_categories(skillDef, effectDefs);
            var blockingLayer = _FindFirstBlockingLayer(barrier, categories);
            if (blockingLayer.Count == 0 && DictionaryGet(barrier, "catch_all_projected_effects", false).AsBool())
                blockingLayer = activeLayer;
            if (blockingLayer.Count == 0)
                continue;
            _AppendLog(batch, $"{sourceUnit.display_name} 的 {(skillDef != null ? skillDef.display_name : "效果")} 被{_GetBarrierLabel(barrier)}的 {_GetLayerLabel(blockingLayer)} 阻挡，无法影响 {targetLabel}。");
            result["blocked"] = true;
            result["applied"] = true;
            return result;
        }
        return result;
    }

    private Dictionary _ApplyBarrierPassage(BattleUnitState unitState, Dictionary barrier, BattleEventBatch batch)
    {
        var result = new Dictionary
        {
            ["applied"] = false,
            ["stopped"] = false,
        };
        if (unitState == null || barrier.Count == 0)
            return result;
        _AppendLog(batch, $"{unitState.display_name} 穿过{_GetBarrierLabel(barrier)}，依次承受未破除的色层。");
        foreach (Variant layerVariant in DictionaryGet(barrier, "layers", new Godot.Collections.Array()).AsGodotArray())
        {
            var layer = layerVariant.VariantType == Variant.Type.Dictionary ? layerVariant.AsGodotDictionary() : new Dictionary();
            if (layer.Count == 0 || DictionaryGet(layer, "broken", false).AsBool())
                continue;
            var layerResult = _outcomeResolver.ApplyPassageOutcomes(unitState, barrier, layer, batch);
            result["applied"] = true;
            if (DictionaryGet(layerResult, "stopped", false).AsBool() || !unitState.is_alive)
            {
                result["stopped"] = true;
                return result;
            }
        }
        return result;
    }

    private void _BreakActiveLayer(StringName barrierKey, Dictionary barrier, Dictionary activeLayer, BattleEventBatch batch)
    {
        var layerId = ProgressionDataUtils.to_string_name(DictionaryGet(activeLayer, "layer_id", ""));
        var layers = DictionaryGet(barrier, "layers", new Godot.Collections.Array()).AsGodotArray();
        for (int index = 0; index < layers.Count; index++)
        {
            var layer = layers[index].VariantType == Variant.Type.Dictionary ? layers[index].AsGodotDictionary() : new Dictionary();
            if (ProgressionDataUtils.to_string_name(DictionaryGet(layer, "layer_id", "")) != layerId)
                continue;
            layer["broken"] = true;
            layers[index] = layer;
            break;
        }
        barrier["layers"] = layers;
        _GetBarrierStore()[barrierKey] = barrier;
        _AppendChangedCoords(batch, _GetBarrierCoords(barrier));
        _AppendLog(batch, $"{_GetBarrierLabel(barrier)} 的 {_GetLayerLabel(activeLayer)} 被破解。");
    }

    private int _ResolveBarrierSaveDc(BattleUnitState sourceUnit, CombatEffectDef effectDef, Dictionary effectParams)
    {
        var resolvedDc = BattleSaveResolver.resolve_save_dc(sourceUnit, effectDef);
        if (resolvedDc > 0)
            return resolvedDc;
        var paramDc = (int)DictionaryGet(effectParams, "save_dc", DEFAULT_SAVE_DC);
        return Mathf.Max(paramDc, 1);
    }

    private StringName _BuildBarrierInstanceId(BattleUnitState sourceUnit, SkillDef skillDef, BarrierProfileDef profile)
    {
        var sourceId = sourceUnit != null ? sourceUnit.unit_id.ToString() : "unknown";
        var skillId = skillDef != null ? skillDef.skill_id.ToString() : profile.profile_id.ToString();
        return new StringName($"{skillId}:{sourceId}:{_GetCurrentTu()}:{_GetBarrierStore().Count + 1}");
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

    private bool _SkillBreaksLayer(SkillDef skillDef, Dictionary layer)
    {
        if (skillDef == null)
            return false;
        foreach (Variant rawId in DictionaryGet(layer, "breaker_skill_ids", new Godot.Collections.Array()).AsGodotArray())
        {
            if (ProgressionDataUtils.to_string_name(rawId) == skillDef.skill_id)
                return true;
        }
        return false;
    }

    private bool _SkillBreaksAnyRemainingLayer(SkillDef skillDef, Dictionary barrier)
    {
        if (skillDef == null)
            return false;
        foreach (Variant layerVariant in DictionaryGet(barrier, "layers", new Godot.Collections.Array()).AsGodotArray())
        {
            var layer = layerVariant.VariantType == Variant.Type.Dictionary ? layerVariant.AsGodotDictionary() : new Dictionary();
            if (layer.Count == 0 || DictionaryGet(layer, "broken", false).AsBool())
                continue;
            if (_SkillBreaksLayer(skillDef, layer))
                return true;
        }
        return false;
    }

    private Dictionary _FindFirstBlockingLayer(Dictionary barrier, Godot.Collections.Array<StringName> categories)
    {
        var categoryLookup = new Dictionary();
        foreach (StringName category in categories)
            categoryLookup[category] = true;
        foreach (Variant layerVariant in DictionaryGet(barrier, "layers", new Godot.Collections.Array()).AsGodotArray())
        {
            var layer = layerVariant.VariantType == Variant.Type.Dictionary ? layerVariant.AsGodotDictionary() : new Dictionary();
            if (layer.Count == 0 || DictionaryGet(layer, "broken", false).AsBool())
                continue;
            foreach (Variant rawCategory in DictionaryGet(layer, "blocked_categories", new Godot.Collections.Array()).AsGodotArray())
            {
                var category = ProgressionDataUtils.to_string_name(rawCategory);
                if (categoryLookup.ContainsKey(category))
                    return layer;
            }
        }
        return new Dictionary();
    }

    private Dictionary _GetActiveLayer(Dictionary barrier)
    {
        foreach (Variant layerVariant in DictionaryGet(barrier, "layers", new Godot.Collections.Array()).AsGodotArray())
        {
            var layer = layerVariant.VariantType == Variant.Type.Dictionary ? layerVariant.AsGodotDictionary() : new Dictionary();
            if (layer.Count > 0 && !DictionaryGet(layer, "broken", false).AsBool())
                return layer;
        }
        return new Dictionary();
    }

    private bool _ProjectedEffectCrossesBarrier(Vector2I sourceCoord, Vector2I targetCoord, Dictionary barrier)
    {
        var runtime = _ResolveRuntime();
        return _geometryService.line_crosses_barrier_area(
            runtime != null ? runtime.Get("_state").As<BattleState>() : null,
            sourceCoord,
            targetCoord,
            _GetBarrierCoords(barrier)
        );
    }

    private bool _IsCoordInsideBarrier(Vector2I coord, Dictionary barrier)
    {
        return _geometryService.coord_inside_barrier(coord, _GetBarrierCoords(barrier));
    }

    private Godot.Collections.Array _GetBarrierCoords(Dictionary barrier)
    {
        var coords = new Godot.Collections.Array();
        var runtime = _ResolveRuntime();
        if (runtime == null || runtime.Get("_state").As<BattleState>() == null || barrier.Count == 0)
            return coords;
        var anchor = DictionaryGet(barrier, "anchor_coord", Vector2I.Zero).AsVector2I();
        var pattern = ProgressionDataUtils.to_string_name(DictionaryGet(barrier, "area_pattern", "diamond"));
        var radius = Mathf.Max((int)DictionaryGet(barrier, "radius_cells", 0), 0);
        var gridService = runtime.Get("_grid_service").AsGodotObject();
        var state = runtime.Get("_state").As<BattleState>();
        return gridService.Call("get_area_coords", state, anchor, pattern, radius, Vector2I.Zero).AsGodotArray();
    }

    private bool _IsBarrierCreator(BattleUnitState unitState, Dictionary barrier)
    {
        return unitState != null && unitState.unit_id == ProgressionDataUtils.to_string_name(DictionaryGet(barrier, "source_unit_id", ""));
    }

    private Dictionary _GetBarrierStore()
    {
        var runtime = _ResolveRuntime();
        if (runtime == null || runtime.Get("_state").As<BattleState>() == null)
            return new Dictionary();
        var state = runtime.Get("_state").As<BattleState>();
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
        if (runtime == null || runtime.Get("_state").As<BattleState>() == null || runtime.Get("_state").As<BattleState>().timeline == null)
            return 0;
        return runtime.Get("_state").As<BattleState>().timeline.Get("current_tu").AsInt32();
    }

    private string _GetBarrierLabel(Dictionary barrier)
    {
        return DictionaryGet(barrier, "display_name", DictionaryGet(barrier, "profile_id", "屏障")).AsString();
    }

    private string _GetLayerLabel(Dictionary layer)
    {
        return DictionaryGet(layer, "display_name", DictionaryGet(layer, "layer_id", "屏障层")).AsString();
    }

    private void _AppendChangedCoords(BattleEventBatch batch, Godot.Collections.Array coords)
    {
        var runtime = _ResolveRuntime();
        if (runtime == null || batch == null)
            return;
        runtime.Call("_append_changed_coords", batch, coords);
    }

    private void _AppendLog(BattleEventBatch batch, string line)
    {
        if (batch == null || string.IsNullOrEmpty(line))
            return;
        batch.log_lines.Add(line);
    }

    private GodotObject _ResolveRuntime()
    {
        if (_runtimeRef == null || !_runtimeRef.TryGetTarget(out GodotObject target) || !GodotObject.IsInstanceValid(target))
            return null;
        return target;
    }

    private static Variant DictionaryGet(Dictionary dictionary, Variant key, Variant fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        return dictionary[key];
    }
}
