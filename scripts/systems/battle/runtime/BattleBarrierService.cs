using System;
using System.Collections.Generic;
using Godot;

internal readonly record struct BattleBarrierInteractionResult(
    bool Blocked,
    bool Applied,
    string PreviewText = "",
    bool WouldBreakLayer = false
)
{
}

internal sealed class BattleBarrierPreviewSession
{
    private readonly Dictionary<StringName, BattleBarrierInstanceState> _barriers;

    internal BattleBarrierPreviewSession(
        IReadOnlyList<StringName> orderedBarrierKeys,
        Dictionary<StringName, BattleBarrierInstanceState> barriers
    )
    {
        OrderedBarrierKeys = orderedBarrierKeys ?? System.Array.Empty<StringName>();
        _barriers = barriers ?? new Dictionary<StringName, BattleBarrierInstanceState>();
    }

    internal IReadOnlyList<StringName> OrderedBarrierKeys { get; }

    internal bool TryGetBarrier(
        StringName barrierKey,
        out BattleBarrierInstanceState barrier
    ) => _barriers.TryGetValue(barrierKey, out barrier);

    internal void PutBarrier(StringName barrierKey, BattleBarrierInstanceState barrier)
    {
        if (barrierKey != "" && barrier != null)
            _barriers[barrierKey] = barrier;
    }
}

internal readonly record struct BattleBarrierPassageResult(bool Applied, bool Stopped)
{
}

internal readonly record struct BattleBarrierCoordClipResult(
    IReadOnlyList<Vector2I> AllowedCoords,
    IReadOnlyList<Vector2I> BlockedCoords
)
{
}

internal readonly record struct BattleGroundEffectBarrierClipResult(
    BattleBarrierCoordClipResult UnitEffects,
    BattleBarrierCoordClipResult TerrainEffects,
    IReadOnlyList<Vector2I> VisibleCoords,
    bool Applied
)
{
}

internal readonly record struct BattleLayeredBarrierApplyResult(
    bool Applied,
    StringName BarrierInstanceId,
    IReadOnlyList<string> LogLines,
    StringName ErrorCode
)
{
    internal static BattleLayeredBarrierApplyResult Empty() =>
        new(false, "", System.Array.Empty<string>(), "");

    internal static BattleLayeredBarrierApplyResult Failure(
        StringName errorCode,
        string message
    ) =>
        new(
            false,
            "",
            string.IsNullOrEmpty(message) ? System.Array.Empty<string>() : new[] { message },
            errorCode
        );
}

internal class BattleBarrierService
{
    private const int DEFAULT_DURATION_TU = 120;
    private const int DEFAULT_SAVE_DC = 16;
    private static readonly StringName VerticalMeteorSwarmProfileId = "meteor_swarm";

    private readonly record struct BarrierApplyParams(
        StringName ProfileId,
        int RadiusCellsOverride,
        StringName AreaPatternOverride,
        int DurationTuOverride,
        int SaveDcOverride
    )
    {
        public static BarrierApplyParams FromEffect(CombatEffectDefinition effectDefinition)
        {
            return new BarrierApplyParams(
                effectDefinition?.GetStringNameParamTyped("profile_id", "") ?? new StringName(""),
                effectDefinition?.GetIntParamTyped("radius_cells", 0) ?? 0,
                effectDefinition?.GetStringNameParamTyped("area_pattern", "") ?? new StringName(""),
                effectDefinition?.GetIntParamTyped("duration_tu", 0) ?? 0,
                effectDefinition?.GetIntParamTyped("save_dc", DEFAULT_SAVE_DC) ?? DEFAULT_SAVE_DC
            );
        }
    }

    private WeakReference<BattleRuntimeModule> _runtimeRef;
    private IReadOnlyDictionary<StringName, BarrierProfileDefinition> _profileDefinitions;
    private BattleBarrierOutcomeResolver _outcomeResolver = new();
    private bool _disposed;

    internal void Setup(
        BattleRuntimeModule runtime,
        IReadOnlyDictionary<StringName, BarrierProfileDefinition> profileDefinitions
    )
    {
        ArgumentNullException.ThrowIfNull(profileDefinitions);
        _runtimeRef = runtime != null ? new WeakReference<BattleRuntimeModule>(runtime) : null;
        _profileDefinitions = profileDefinitions;
        _outcomeResolver.Setup(runtime);
    }

    internal void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _outcomeResolver?.Dispose();
        _profileDefinitions = null;
        _outcomeResolver = null;
        _runtimeRef = null;
    }

    internal BattleLayeredBarrierApplyResult ApplyLayeredBarrierEffectResult(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        SkillDefinition skillDefinition,
        CombatEffectDefinition effectDefinition,
        BattleEventBatch batch
    )
    {
        var runtime = _ResolveRuntime();
        if (
            runtime == null
            || runtime._state == null
            || sourceUnit == null
            || effectDefinition == null
        )
            return BattleLayeredBarrierApplyResult.Empty();
        BarrierApplyParams effectParams = BarrierApplyParams.FromEffect(effectDefinition);
        var profileId = effectParams.ProfileId;
        if (
            _profileDefinitions == null
            || !_profileDefinitions.TryGetValue(
                profileId,
                out BarrierProfileDefinition profile
            )
        )
        {
            string profileLabel = profileId != "" ? profileId.ToString() : "<empty>";
            string message =
                $"Layered barrier profile '{profileLabel}' is unavailable; the effect cannot be applied.";
            GameLog.Error(message, "battle.barrier.profile_missing", "battle");
            _AppendLog(batch, message);
            return BattleLayeredBarrierApplyResult.Failure(
                "barrier_profile_missing",
                message
            );
        }

        var anchorUnit = targetUnit != null ? targetUnit : sourceUnit;
        var radiusCells = Mathf.Max(
            effectParams.RadiusCellsOverride > 0
                ? effectParams.RadiusCellsOverride
                : profile.RadiusCells,
            1
        );
        var areaPattern =
            effectParams.AreaPatternOverride != ""
                ? effectParams.AreaPatternOverride
                : profile.AreaPattern;
        if (areaPattern == "")
            areaPattern = profile.AreaPattern;
        var durationTu = effectDefinition.DurationTu;
        if (durationTu <= 0)
            durationTu = Mathf.Max(
                effectParams.DurationTuOverride > 0
                    ? effectParams.DurationTuOverride
                    : profile.DurationTu,
                0
            );
        if (durationTu <= 0)
            durationTu = DEFAULT_DURATION_TU;
        var saveDc = _ResolveBarrierSaveDc(sourceUnit, effectDefinition, effectParams);
        var instanceId = _BuildBarrierInstanceId(sourceUnit, skillDefinition, profile);
        var instance = new BattleBarrierInstanceState();
        instance.BarrierInstanceId = instanceId;
        instance.ProfileId = profile.ProfileId;
        instance.DisplayName = profile.DisplayName;
        instance.SourceUnitId = sourceUnit.unit_id;
        instance.SourceSkillId = skillDefinition != null ? skillDefinition.SkillId : "";
        instance.AnchorMode = profile.AnchorModeKind;
        instance.AnchorCoord = anchorUnit.coord;
        instance.RadiusCells = radiusCells;
        instance.AreaPattern = areaPattern;
        instance.RemainingTu = durationTu;
        instance.CreatedTu = _GetCurrentTu();
        instance.SaveDc = saveDc;
        instance.CatchAllProjectedEffects = profile.CatchAllProjectedEffects;
        instance.SetLayers(_BuildLayers(profile, saveDc));

        _PutBarrier(instanceId, instance);
        _AppendChangedCoords(batch, _GetBarrierCoords(instance));
        var line =
            $"{sourceUnit.display_name} 创造{_GetBarrierLabel(instance)}，固定在 ({anchorUnit.coord.X}, {anchorUnit.coord.Y})，半径 {radiusCells} 格。";
        _AppendLog(batch, line);
        return new BattleLayeredBarrierApplyResult(true, instanceId, new[] { line }, "");
    }

    internal void AdvanceBarrierDurations(int elapsedTu, BattleEventBatch batch)
    {
        var runtime = _ResolveRuntime();
        if (runtime == null || runtime._state == null || elapsedTu <= 0)
            return;
        var expiredIds = new StringNameList();
        foreach (StringName barrierKey in _SortedBarrierKeys())
        {
            if (!TryReadBarrier(barrierKey, out BattleBarrierInstanceState barrier))
                continue;
            var remaining = barrier.RemainingTu - elapsedTu;
            barrier.RemainingTu = remaining;
            _PutBarrier(barrierKey, barrier);
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
        SkillDefinition skillDefinition,
        IEnumerable<CombatEffectDefinition> effectDefinitions,
        BattleEventBatch batch
    )
    {
        if (sourceUnit == null || targetUnit == null)
            return new BattleBarrierInteractionResult(false, false);
        return _ResolveProjectedEffectBarrierInteractionResult(
            sourceUnit,
            sourceUnit.coord,
            targetUnit.coord,
            targetUnit.display_name,
            skillDefinition,
            effectDefinitions,
            batch,
            commit: true
        );
    }

    internal BattleBarrierInteractionResult PreviewSkillBarrierInteractionResult(
        BattleUnitReadView sourceUnit,
        BattleUnitReadView targetUnit,
        SkillDefinition skillDefinition,
        IEnumerable<CombatEffectDefinition> effectDefinitions,
        BattleBarrierPreviewSession previewSession = null
    )
    {
        if (!sourceUnit.IsValid || !targetUnit.IsValid)
            return new BattleBarrierInteractionResult(false, false);
        return _ResolveProjectedEffectBarrierInteractionResult(
            sourceUnit.UnsafeUnitForReadOnlyRules,
            sourceUnit.Coord,
            targetUnit.Coord,
            targetUnit.DisplayName,
            skillDefinition,
            effectDefinitions,
            batch: null,
            commit: false,
            previewSession: previewSession
        );
    }

    internal BattleBarrierPreviewSession BeginSkillBarrierPreviewSession()
    {
        IReadOnlyList<StringName> orderedBarrierKeys = _SortedBarrierKeys();
        var barriers = new Dictionary<StringName, BattleBarrierInstanceState>();
        foreach (StringName barrierKey in orderedBarrierKeys)
        {
            if (TryReadBarrier(barrierKey, out BattleBarrierInstanceState barrier))
                barriers[barrierKey] = barrier;
        }
        return new BattleBarrierPreviewSession(orderedBarrierKeys, barriers);
    }

    internal BattleBarrierInteractionResult ResolveSkillBarrierInteractionFromCoordResult(
        BattleUnitState sourceUnit,
        Vector2I effectOriginCoord,
        BattleUnitState targetUnit,
        SkillDefinition skillDefinition,
        IEnumerable<CombatEffectDefinition> effectDefinitions,
        BattleEventBatch batch
    )
    {
        if (sourceUnit == null || targetUnit == null)
            return new BattleBarrierInteractionResult(false, false);
        return _ResolveProjectedEffectBarrierInteractionResult(
            sourceUnit,
            effectOriginCoord,
            targetUnit.coord,
            targetUnit.display_name,
            skillDefinition,
            effectDefinitions,
            batch,
            commit: true
        );
    }

    internal BattleBarrierInteractionResult ResolveGroundBarrierInteractionResult(
        BattleUnitState sourceUnit,
        Vector2I targetCoord,
        SkillDefinition skillDefinition,
        IEnumerable<CombatEffectDefinition> effectDefinitions,
        BattleEventBatch batch
    )
    {
        return _ResolveProjectedEffectBarrierInteractionResult(
            sourceUnit,
            sourceUnit?.coord ?? new Vector2I(-1, -1),
            targetCoord,
            $"({targetCoord.X}, {targetCoord.Y})",
            skillDefinition,
            effectDefinitions,
            batch,
            commit: true
        );
    }

    internal BattleGroundEffectBarrierClipResult ResolveGroundEffectBarrierClipResult(
        BattleUnitState sourceUnit,
        SkillDefinition skillDefinition,
        IReadOnlyList<CombatEffectDefinition> unitEffectDefinitions,
        IReadOnlyList<CombatEffectDefinition> terrainEffectDefinitions,
        IReadOnlyList<Vector2I> effectCoords,
        BattleEventBatch batch
    )
    {
        return _ResolveGroundEffectBarrierClipResult(
            sourceUnit,
            sourceUnit?.coord ?? new Vector2I(-1, -1),
            sourceUnit?.display_name ?? "",
            skillDefinition,
            unitEffectDefinitions,
            terrainEffectDefinitions,
            effectCoords,
            batch,
            commit: true
        );
    }

    internal BattleGroundEffectBarrierClipResult PreviewGroundEffectBarrierClipResult(
        BattleUnitReadView sourceUnit,
        SkillDefinition skillDefinition,
        IReadOnlyList<CombatEffectDefinition> unitEffectDefinitions,
        IReadOnlyList<CombatEffectDefinition> terrainEffectDefinitions,
        IReadOnlyList<Vector2I> effectCoords
    )
    {
        return _ResolveGroundEffectBarrierClipResult(
            sourceUnit.IsValid ? sourceUnit.UnsafeUnitForReadOnlyRules : null,
            sourceUnit.IsValid ? sourceUnit.Coord : new Vector2I(-1, -1),
            sourceUnit.DisplayName,
            skillDefinition,
            unitEffectDefinitions,
            terrainEffectDefinitions,
            effectCoords,
            batch: null,
            commit: false
        );
    }

    internal BattleGroundEffectBarrierClipResult PreviewGroundEffectBarrierClipResultAtCoord(
        BattleUnitState sourceUnit,
        Vector2I effectOriginCoord,
        SkillDefinition skillDefinition,
        IReadOnlyList<CombatEffectDefinition> unitEffectDefinitions,
        IReadOnlyList<CombatEffectDefinition> terrainEffectDefinitions,
        IReadOnlyList<Vector2I> effectCoords
    )
    {
        return _ResolveGroundEffectBarrierClipResult(
            sourceUnit,
            effectOriginCoord,
            sourceUnit?.display_name ?? "",
            skillDefinition,
            unitEffectDefinitions,
            terrainEffectDefinitions,
            effectCoords,
            batch: null,
            commit: false
        );
    }

    internal BattleGroundEffectBarrierClipResult PreviewGroundEffectBarrierClipResultAtCoord(
        BattleUnitReadView sourceUnit,
        Vector2I effectOriginCoord,
        SkillDefinition skillDefinition,
        IReadOnlyList<CombatEffectDefinition> unitEffectDefinitions,
        IReadOnlyList<CombatEffectDefinition> terrainEffectDefinitions,
        IReadOnlyList<Vector2I> effectCoords
    )
    {
        return _ResolveGroundEffectBarrierClipResult(
            sourceUnit.IsValid ? sourceUnit.UnsafeUnitForReadOnlyRules : null,
            effectOriginCoord,
            sourceUnit.DisplayName,
            skillDefinition,
            unitEffectDefinitions,
            terrainEffectDefinitions,
            effectCoords,
            batch: null,
            commit: false
        );
    }

    private BattleGroundEffectBarrierClipResult _ResolveGroundEffectBarrierClipResult(
        BattleUnitState sourceUnit,
        Vector2I sourceCoord,
        string sourceDisplayName,
        SkillDefinition skillDefinition,
        IReadOnlyList<CombatEffectDefinition> unitEffectDefinitions,
        IReadOnlyList<CombatEffectDefinition> terrainEffectDefinitions,
        IReadOnlyList<Vector2I> effectCoords,
        BattleEventBatch batch,
        bool commit
    )
    {
        IReadOnlyList<CombatEffectDefinition> normalizedUnitEffects =
            unitEffectDefinitions ?? System.Array.Empty<CombatEffectDefinition>();
        IReadOnlyList<CombatEffectDefinition> normalizedTerrainEffects =
            terrainEffectDefinitions ?? System.Array.Empty<CombatEffectDefinition>();
        List<Vector2I> normalizedEffectCoords = _SortUniqueCoords(effectCoords);
        bool hasUnitEffects = normalizedUnitEffects.Count > 0;
        bool hasTerrainEffects = normalizedTerrainEffects.Count > 0;
        var unitAllowedCoords = hasUnitEffects
            ? new List<Vector2I>(normalizedEffectCoords)
            : new List<Vector2I>();
        var terrainAllowedCoords = hasTerrainEffects
            ? new List<Vector2I>(normalizedEffectCoords)
            : new List<Vector2I>();
        var unitBlockedCoords = new HashSet<Vector2I>();
        var terrainBlockedCoords = new HashSet<Vector2I>();
        bool applied = false;

        BattleRuntimeModule runtime = _ResolveRuntime();
        if (
            runtime != null
            && runtime._state != null
            && sourceUnit != null
            && !_IsProjectedBarrierExempt(skillDefinition)
        )
        {
            IReadOnlyList<StringName> projectedWeaponCategories = hasUnitEffects
                ? runtime
                    .GetEquipmentAbilityRuntimeService()
                    .CollectProjectedWeaponEffectCategories(
                        sourceUnit,
                        normalizedUnitEffects,
                        skillDefinition
                    )
                : System.Array.Empty<StringName>();
            IReadOnlyList<StringName> unitCategories = hasUnitEffects
                ? BattleEffectCategoryResolver.ResolveCategories(
                    skillDefinition,
                    normalizedUnitEffects,
                    projectedWeaponCategories
                )
                : System.Array.Empty<StringName>();
            IReadOnlyList<StringName> terrainCategories = hasTerrainEffects
                ? BattleEffectCategoryResolver.ResolveCategories(
                    skillDefinition,
                    normalizedTerrainEffects
                )
                : System.Array.Empty<StringName>();

            foreach (StringName barrierKey in _SortedBarrierKeys())
            {
                if (!TryReadBarrier(barrierKey, out BattleBarrierInstanceState barrier))
                    continue;
                BattleBarrierLayerState activeLayer = _GetActiveLayer(barrier);
                if (activeLayer == null)
                    continue;

                IReadOnlyList<Vector2I> barrierCoords = _GetBarrierCoords(barrier);
                List<Vector2I> crossingUnitCoords = _CollectCrossingCoords(
                    sourceCoord,
                    unitAllowedCoords,
                    barrierCoords
                );
                List<Vector2I> crossingTerrainCoords = _CollectCrossingCoords(
                    sourceCoord,
                    terrainAllowedCoords,
                    barrierCoords
                );
                if (crossingUnitCoords.Count == 0 && crossingTerrainCoords.Count == 0)
                    continue;

                bool breaksActiveLayer = _SkillBreaksLayer(skillDefinition, activeLayer);
                bool breaksDeeperLayer =
                    !breaksActiveLayer && _SkillBreaksAnyRemainingLayer(skillDefinition, barrier);
                BattleBarrierLayerState unitBlockingLayer = null;
                BattleBarrierLayerState terrainBlockingLayer = null;
                List<Vector2I> blockedUnitCoords = new();
                List<Vector2I> blockedTerrainCoords = new();

                if (breaksActiveLayer || breaksDeeperLayer)
                {
                    blockedUnitCoords.AddRange(crossingUnitCoords);
                    blockedTerrainCoords.AddRange(crossingTerrainCoords);
                }
                else
                {
                    if (crossingUnitCoords.Count > 0)
                    {
                        unitBlockingLayer = _FindFirstBlockingLayer(barrier, unitCategories);
                        if (unitBlockingLayer == null && barrier.CatchAllProjectedEffects)
                            unitBlockingLayer = activeLayer;
                        if (unitBlockingLayer != null)
                            blockedUnitCoords.AddRange(crossingUnitCoords);
                    }
                    if (crossingTerrainCoords.Count > 0)
                    {
                        terrainBlockingLayer = _FindFirstBlockingLayer(barrier, terrainCategories);
                        if (terrainBlockingLayer == null && barrier.CatchAllProjectedEffects)
                            terrainBlockingLayer = activeLayer;
                        if (terrainBlockingLayer != null)
                            blockedTerrainCoords.AddRange(crossingTerrainCoords);
                    }
                }

                if (blockedUnitCoords.Count == 0 && blockedTerrainCoords.Count == 0)
                    continue;

                _RemoveCoords(unitAllowedCoords, blockedUnitCoords);
                _RemoveCoords(terrainAllowedCoords, blockedTerrainCoords);
                _AddCoords(unitBlockedCoords, blockedUnitCoords);
                _AddCoords(terrainBlockedCoords, blockedTerrainCoords);
                applied = true;

                if (!commit)
                    continue;

                int blockedCoordCount = _CountUniqueCoords(
                    blockedUnitCoords,
                    blockedTerrainCoords
                );
                string sourceLabel = string.IsNullOrEmpty(sourceDisplayName)
                    ? "施法者"
                    : sourceDisplayName;
                string skillLabel = skillDefinition != null
                    ? skillDefinition.DisplayName
                    : "效果";
                if (breaksActiveLayer)
                {
                    _BreakActiveLayer(barrierKey, barrier, activeLayer, batch);
                    _AppendLog(
                        batch,
                        $"{sourceLabel} 的 {skillLabel} 破解了{_GetBarrierLabel(barrier)}，但本次跨界的 {blockedCoordCount} 个地格仍被阻挡。"
                    );
                    continue;
                }
                if (breaksDeeperLayer)
                {
                    _AppendLog(
                        batch,
                        $"{sourceLabel} 试图破解{_GetBarrierLabel(barrier)}，但必须先处理外层 {_GetLayerLabel(activeLayer)}；本次跨界的 {blockedCoordCount} 个地格被阻挡。"
                    );
                    continue;
                }

                _AppendGroundEffectBlockLogs(
                    batch,
                    sourceLabel,
                    skillLabel,
                    barrier,
                    unitBlockingLayer,
                    blockedUnitCoords,
                    terrainBlockingLayer,
                    blockedTerrainCoords
                );
            }
        }

        var visibleCoords = new List<Vector2I>();
        if (hasUnitEffects || hasTerrainEffects)
        {
            visibleCoords.AddRange(unitAllowedCoords);
            visibleCoords.AddRange(terrainAllowedCoords);
        }
        else
        {
            visibleCoords.AddRange(normalizedEffectCoords);
        }
        return new BattleGroundEffectBarrierClipResult(
            new BattleBarrierCoordClipResult(
                _SortUniqueCoords(unitAllowedCoords),
                _SortUniqueCoords(unitBlockedCoords)
            ),
            new BattleBarrierCoordClipResult(
                _SortUniqueCoords(terrainAllowedCoords),
                _SortUniqueCoords(terrainBlockedCoords)
            ),
            _SortUniqueCoords(visibleCoords),
            applied
        );
    }

    private BattleBarrierInteractionResult _ResolveProjectedEffectBarrierInteractionResult(
        BattleUnitState sourceUnit,
        Vector2I effectOriginCoord,
        Vector2I targetCoord,
        string targetLabel,
        SkillDefinition skillDefinition,
        IEnumerable<CombatEffectDefinition> effectDefinitions,
        BattleEventBatch batch,
        bool commit,
        BattleBarrierPreviewSession previewSession = null
    )
    {
        if (_IsProjectedBarrierExempt(skillDefinition))
            return new BattleBarrierInteractionResult(false, false);
        var runtime = _ResolveRuntime();
        if (
            runtime == null
            || runtime._state == null
            || sourceUnit == null
        )
            return new BattleBarrierInteractionResult(false, false);
        IReadOnlyList<CombatEffectDefinition> normalizedEffects =
            effectDefinitions as IReadOnlyList<CombatEffectDefinition>
            ?? new List<CombatEffectDefinition>(
                effectDefinitions ?? System.Array.Empty<CombatEffectDefinition>()
            );
        IReadOnlyList<StringName> projectedWeaponCategories = runtime
            .GetEquipmentAbilityRuntimeService()
            .CollectProjectedWeaponEffectCategories(
                sourceUnit,
                normalizedEffects,
                skillDefinition
            );
        IReadOnlyList<StringName> categories = BattleEffectCategoryResolver.ResolveCategories(
            skillDefinition,
            normalizedEffects,
            projectedWeaponCategories
        );
        IReadOnlyList<StringName> barrierKeys =
            previewSession?.OrderedBarrierKeys ?? _SortedBarrierKeys();
        foreach (StringName barrierKey in barrierKeys)
        {
            BattleBarrierInstanceState barrier;
            if (previewSession != null)
            {
                if (!previewSession.TryGetBarrier(barrierKey, out barrier))
                    continue;
            }
            else if (!TryReadBarrier(barrierKey, out barrier))
            {
                continue;
            }
            if (!_ProjectedEffectCrossesBarrier(effectOriginCoord, targetCoord, barrier))
                continue;
            var activeLayer = _GetActiveLayer(barrier);
            if (activeLayer == null)
                continue;
            if (_SkillBreaksLayer(skillDefinition, activeLayer))
            {
                string activeBreakerPreviewText =
                    $"{sourceUnit.display_name} 的 {(skillDefinition != null ? skillDefinition.DisplayName : "效果")} 会破解{_GetBarrierLabel(barrier)}的 {_GetLayerLabel(activeLayer)}，但本次跨界效果仍被阻挡。";
                if (commit)
                    _BreakActiveLayer(barrierKey, barrier, activeLayer, batch);
                else if (previewSession != null)
                {
                    List<BattleBarrierLayerState> previewLayers = barrier.GetLayersTyped();
                    foreach (BattleBarrierLayerState previewLayer in previewLayers)
                    {
                        if (previewLayer?.LayerId != activeLayer.LayerId)
                            continue;
                        previewLayer.Broken = true;
                        break;
                    }
                    barrier.SetLayers(previewLayers);
                    previewSession.PutBarrier(barrierKey, barrier);
                }
                return new BattleBarrierInteractionResult(
                    true,
                    commit,
                    activeBreakerPreviewText,
                    WouldBreakLayer: true
                );
            }
            if (_SkillBreaksAnyRemainingLayer(skillDefinition, barrier))
            {
                string deeperBreakerPreviewText =
                    $"{sourceUnit.display_name} 试图破解{_GetBarrierLabel(barrier)}，但必须先处理外层 {_GetLayerLabel(activeLayer)}。";
                if (commit)
                    _AppendLog(batch, deeperBreakerPreviewText);
                return new BattleBarrierInteractionResult(
                    true,
                    commit,
                    deeperBreakerPreviewText
                );
            }
            var blockingLayer = _FindFirstBlockingLayer(barrier, categories);
            if (
                blockingLayer == null
                && barrier.CatchAllProjectedEffects
            )
                blockingLayer = activeLayer;
            if (blockingLayer == null)
                continue;
            string blockingPreviewText =
                $"{sourceUnit.display_name} 的 {(skillDefinition != null ? skillDefinition.DisplayName : "效果")} 被{_GetBarrierLabel(barrier)}的 {_GetLayerLabel(blockingLayer)} 阻挡，无法影响 {targetLabel}。";
            if (commit)
                _AppendLog(batch, blockingPreviewText);
            return new BattleBarrierInteractionResult(true, commit, blockingPreviewText);
        }
        return new BattleBarrierInteractionResult(false, false);
    }

    private static bool _IsProjectedBarrierExempt(SkillDefinition skillDefinition)
    {
        // meteor_swarm is a vertically falling disaster resolved by its dedicated profile,
        // so a horizontal projected-effect barrier does not clip its impact plan.
        return skillDefinition?.CombatProfile?.SpecialResolutionProfileId
            == VerticalMeteorSwarmProfileId;
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
        _PutBarrier(barrierKey, barrier);
        _AppendChangedCoords(batch, _GetBarrierCoords(barrier));
        _AppendLog(batch, $"{_GetBarrierLabel(barrier)} 的 {_GetLayerLabel(activeLayer)} 被破解。");
    }

    private int _ResolveBarrierSaveDc(
        BattleUnitState sourceUnit,
        CombatEffectDefinition effectDefinition,
        BarrierApplyParams effectParams
    )
    {
        var resolvedDc = BattleSaveResolver.ResolveSaveDc(sourceUnit, effectDefinition);
        if (resolvedDc > 0)
            return resolvedDc;
        var paramDc = effectParams.SaveDcOverride;
        return Mathf.Max(paramDc, 1);
    }

    private StringName _BuildBarrierInstanceId(
        BattleUnitState sourceUnit,
        SkillDefinition skillDefinition,
        BarrierProfileDefinition profile
    )
    {
        var sourceId = sourceUnit != null ? sourceUnit.unit_id.ToString() : "unknown";
        var skillId =
            skillDefinition != null
                ? skillDefinition.SkillId.ToString()
                : profile.ProfileId.ToString();
        return new StringName(
            $"{skillId}:{sourceId}:{_GetCurrentTu()}:{_GetBarrierStoreCount() + 1}"
        );
    }

    private List<BattleBarrierLayerState> _BuildLayers(
        BarrierProfileDefinition profile,
        int saveDc
    )
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

    private static BattleBarrierLayerState _BuildLayerState(
        BarrierLayerDefinition layerDef,
        int saveDc
    )
    {
        var layer = new BattleBarrierLayerState
        {
            LayerId = layerDef.LayerId,
            DisplayName = layerDef.DisplayName,
            Order = layerDef.Order,
            Broken = false,
        };
        layer.SetBlockedCategories(layerDef.BlockedCategories);
        layer.SetBreakerSkillIds(layerDef.BreakerSkillIds);

        var outcomes = new List<BattleBarrierOutcomeState>();
        foreach (BarrierOutcomeDefinition outcomeDef in layerDef.PassageOutcomes)
        {
            if (outcomeDef == null)
                continue;
            outcomes.Add(BattleBarrierOutcomeState.FromDefinition(outcomeDef, saveDc));
        }
        layer.SetPassageOutcomes(outcomes);
        return layer;
    }

    private bool _SkillBreaksLayer(SkillDefinition skillDefinition, BattleBarrierLayerState layer)
    {
        if (skillDefinition == null || layer == null)
            return false;
        foreach (StringName breakerSkillId in layer.BreakerSkillIds)
        {
            if (breakerSkillId == skillDefinition.SkillId)
                return true;
        }
        return false;
    }

    private bool _SkillBreaksAnyRemainingLayer(
        SkillDefinition skillDefinition,
        BattleBarrierInstanceState barrier
    )
    {
        if (skillDefinition == null || barrier == null)
            return false;
        foreach (BattleBarrierLayerState layer in barrier.GetLayersTyped())
        {
            if (layer == null || layer.Broken)
                continue;
            if (_SkillBreaksLayer(skillDefinition, layer))
                return true;
        }
        return false;
    }

    private static List<Vector2I> _CollectCrossingCoords(
        Vector2I sourceCoord,
        IEnumerable<Vector2I> candidateCoords,
        IReadOnlyList<Vector2I> barrierCoords
    )
    {
        var result = new List<Vector2I>();
        foreach (Vector2I coord in candidateCoords ?? System.Array.Empty<Vector2I>())
        {
            if (
                BattleBarrierGeometryService.LineCrossesBarrierArea(
                    sourceCoord,
                    coord,
                    barrierCoords
                )
            )
            {
                result.Add(coord);
            }
        }
        return result;
    }

    private static void _RemoveCoords(
        List<Vector2I> sourceCoords,
        IEnumerable<Vector2I> removedCoords
    )
    {
        if (sourceCoords == null || sourceCoords.Count == 0)
            return;
        var removedLookup = new HashSet<Vector2I>(
            removedCoords ?? System.Array.Empty<Vector2I>()
        );
        if (removedLookup.Count == 0)
            return;
        sourceCoords.RemoveAll(removedLookup.Contains);
    }

    private static void _AddCoords(
        HashSet<Vector2I> destination,
        IEnumerable<Vector2I> sourceCoords
    )
    {
        if (destination == null)
            return;
        foreach (Vector2I coord in sourceCoords ?? System.Array.Empty<Vector2I>())
        {
            destination.Add(coord);
        }
    }

    private static int _CountUniqueCoords(
        IEnumerable<Vector2I> firstCoords,
        IEnumerable<Vector2I> secondCoords
    )
    {
        var result = new HashSet<Vector2I>();
        _AddCoords(result, firstCoords);
        _AddCoords(result, secondCoords);
        return result.Count;
    }

    private static List<Vector2I> _SortUniqueCoords(IEnumerable<Vector2I> coords)
    {
        var seen = new HashSet<Vector2I>();
        var result = new List<Vector2I>();
        foreach (Vector2I coord in coords ?? System.Array.Empty<Vector2I>())
        {
            if (seen.Add(coord))
                result.Add(coord);
        }
        result.Sort((left, right) =>
            left.Y != right.Y ? left.Y.CompareTo(right.Y) : left.X.CompareTo(right.X)
        );
        return result;
    }

    private void _AppendGroundEffectBlockLogs(
        BattleEventBatch batch,
        string sourceLabel,
        string skillLabel,
        BattleBarrierInstanceState barrier,
        BattleBarrierLayerState unitBlockingLayer,
        IReadOnlyList<Vector2I> blockedUnitCoords,
        BattleBarrierLayerState terrainBlockingLayer,
        IReadOnlyList<Vector2I> blockedTerrainCoords
    )
    {
        bool sameBlockingLayer =
            unitBlockingLayer != null
            && terrainBlockingLayer != null
            && unitBlockingLayer.LayerId == terrainBlockingLayer.LayerId;
        if (sameBlockingLayer)
        {
            _AppendGroundEffectBlockLog(
                batch,
                sourceLabel,
                skillLabel,
                barrier,
                unitBlockingLayer,
                _CountUniqueCoords(blockedUnitCoords, blockedTerrainCoords),
                ""
            );
            return;
        }
        if (unitBlockingLayer != null && blockedUnitCoords.Count > 0)
        {
            _AppendGroundEffectBlockLog(
                batch,
                sourceLabel,
                skillLabel,
                barrier,
                unitBlockingLayer,
                _CountUniqueCoords(blockedUnitCoords, System.Array.Empty<Vector2I>()),
                "单位效果"
            );
        }
        if (terrainBlockingLayer != null && blockedTerrainCoords.Count > 0)
        {
            _AppendGroundEffectBlockLog(
                batch,
                sourceLabel,
                skillLabel,
                barrier,
                terrainBlockingLayer,
                _CountUniqueCoords(blockedTerrainCoords, System.Array.Empty<Vector2I>()),
                "地形效果"
            );
        }
    }

    private void _AppendGroundEffectBlockLog(
        BattleEventBatch batch,
        string sourceLabel,
        string skillLabel,
        BattleBarrierInstanceState barrier,
        BattleBarrierLayerState blockingLayer,
        int blockedCoordCount,
        string effectScope
    )
    {
        if (blockingLayer == null || blockedCoordCount <= 0)
            return;
        string scopeLabel = string.IsNullOrEmpty(effectScope) ? "" : $"上的{effectScope}";
        _AppendLog(
            batch,
            $"{_GetBarrierLabel(barrier)}的 {_GetLayerLabel(blockingLayer)} 阻挡了 {blockedCoordCount} 个地格{scopeLabel}，{sourceLabel} 的 {skillLabel} 只能影响其余区域。"
        );
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

        return _GetBattleState()?.TryGetLayeredBarrierField(barrierKey, out barrier) == true;
    }

    private BattleState _GetBattleState()
    {
        var runtime = _ResolveRuntime();
        if (runtime == null || runtime._state == null)
            return null;
        return runtime._state;
    }

    private int _GetBarrierStoreCount()
    {
        return _GetBattleState()?.LayeredBarrierFieldCount ?? 0;
    }

    private void _PutBarrier(StringName barrierKey, BattleBarrierInstanceState barrier)
    {
        _GetBattleState()?.PutLayeredBarrierField(barrierKey, barrier);
    }

    private void _RemoveBarrier(StringName barrierKey)
    {
        BattleState state = _GetBattleState();
        state?.RemoveLayeredBarrierFieldPayload(barrierKey);
    }

    private IReadOnlyList<StringName> _SortedBarrierKeys()
    {
        return _GetBattleState()?.LayeredBarrierStore.SortedKeys() ?? System.Array.Empty<StringName>();
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
