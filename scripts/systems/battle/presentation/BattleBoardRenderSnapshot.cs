using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

internal sealed class BattleBoardCellSnapshot
{
    private readonly ReadOnlyCollection<StringName> _propIds;
    private readonly ReadOnlyCollection<StringName> _terrainOverlayIds;

    internal BattleBoardCellSnapshot(
        Vector2I coord,
        int height,
        StringName baseTerrain,
        IEnumerable<StringName> propIds,
        IEnumerable<StringName> terrainOverlayIds
    )
    {
        Coord = coord;
        Height = height;
        BaseTerrain = baseTerrain;
        _propIds = new List<StringName>(propIds ?? Array.Empty<StringName>()).AsReadOnly();
        _terrainOverlayIds = new List<StringName>(
            terrainOverlayIds ?? Array.Empty<StringName>()
        ).AsReadOnly();
    }

    internal Vector2I Coord { get; }
    internal int Height { get; }
    internal StringName BaseTerrain { get; }
    internal IReadOnlyList<StringName> PropIds => _propIds;
    internal IReadOnlyList<StringName> TerrainOverlayIds => _terrainOverlayIds;
}

internal sealed class BattleBoardEdgeSnapshot
{
    private readonly ReadOnlyCollection<int> _dropFaceLayerHeights;

    internal BattleBoardEdgeSnapshot(
        Vector2I originCoord,
        Vector2I neighborCoord,
        Vector2I direction,
        IEnumerable<int> dropFaceLayerHeights,
        BattleEdgeRenderKind featureRenderKind,
        int featureLayers,
        int fromHeight
    )
    {
        OriginCoord = originCoord;
        NeighborCoord = neighborCoord;
        Direction = direction;
        _dropFaceLayerHeights = new List<int>(
            dropFaceLayerHeights ?? Array.Empty<int>()
        ).AsReadOnly();
        FeatureRenderKind = featureRenderKind;
        FeatureLayers = Math.Max(featureLayers, 0);
        FromHeight = fromHeight;
    }

    internal Vector2I OriginCoord { get; }
    internal Vector2I NeighborCoord { get; }
    internal Vector2I Direction { get; }
    internal IReadOnlyList<int> DropFaceLayerHeights => _dropFaceLayerHeights;
    internal BattleEdgeRenderKind FeatureRenderKind { get; }
    internal int FeatureLayers { get; }
    internal int FromHeight { get; }
    internal bool HasDropFace => _dropFaceLayerHeights.Count > 0;
    internal bool HasFeatureFace =>
        FeatureRenderKind == BattleEdgeRenderKind.Wall && FeatureLayers > 0;
}

internal sealed class BattleBoardUnitSnapshot
{
    private readonly ReadOnlyCollection<Vector2I> _occupiedCoords;

    internal BattleBoardUnitSnapshot(
        StringName unitId,
        string displayName,
        StringName factionId,
        bool isAlive,
        Vector2I anchorCoord,
        IEnumerable<Vector2I> occupiedCoords,
        string battleSpriteTexturePath,
        int currentHp,
        int maxHp
    )
    {
        UnitId = unitId;
        DisplayName = displayName ?? "";
        FactionId = factionId;
        IsAlive = isAlive;
        AnchorCoord = anchorCoord;
        _occupiedCoords = new List<Vector2I>(
            occupiedCoords ?? Array.Empty<Vector2I>()
        ).AsReadOnly();
        BattleSpriteTexturePath = battleSpriteTexturePath ?? "";
        CurrentHp = Math.Max(currentHp, 0);
        MaxHp = Math.Max(maxHp, 1);
    }

    internal StringName UnitId { get; }
    internal string DisplayName { get; }
    internal StringName FactionId { get; }
    internal bool IsAlive { get; }
    internal Vector2I AnchorCoord { get; }
    internal IReadOnlyList<Vector2I> OccupiedCoords => _occupiedCoords;
    internal string BattleSpriteTexturePath { get; }
    internal int CurrentHp { get; }
    internal int MaxHp { get; }

    internal bool OccupiesCoord(Vector2I coord)
    {
        foreach (Vector2I occupiedCoord in _occupiedCoords)
        {
            if (occupiedCoord == coord)
                return true;
        }
        return false;
    }
}

public sealed class BattleBoardUnitUpdateSnapshot
{
    private readonly ReadOnlyCollection<StringName> _requestedUnitIds;
    private readonly ReadOnlyCollection<StringName> _allyUnitIds;
    private readonly ReadOnlyDictionary<StringName, BattleBoardUnitSnapshot> _units;

    internal BattleBoardUnitUpdateSnapshot(
        StringName battleId,
        StringName activeUnitId,
        bool replacesAllUnits,
        IEnumerable<StringName> requestedUnitIds,
        IEnumerable<StringName> allyUnitIds,
        IDictionary<StringName, BattleBoardUnitSnapshot> units
    )
    {
        BattleId = battleId;
        ActiveUnitId = activeUnitId;
        ReplacesAllUnits = replacesAllUnits;
        _requestedUnitIds = new List<StringName>(
            requestedUnitIds ?? Array.Empty<StringName>()
        ).AsReadOnly();
        _allyUnitIds = new List<StringName>(
            allyUnitIds ?? Array.Empty<StringName>()
        ).AsReadOnly();
        _units = new ReadOnlyDictionary<StringName, BattleBoardUnitSnapshot>(
            new Dictionary<StringName, BattleBoardUnitSnapshot>(
                units ?? new Dictionary<StringName, BattleBoardUnitSnapshot>()
            )
        );
    }

    internal StringName BattleId { get; }
    internal StringName ActiveUnitId { get; }
    internal bool ReplacesAllUnits { get; }
    internal IReadOnlyList<StringName> RequestedUnitIds => _requestedUnitIds;
    internal IReadOnlyList<StringName> AllyUnitIds => _allyUnitIds;
    internal IReadOnlyDictionary<StringName, BattleBoardUnitSnapshot> Units => _units;
}

public sealed class BattleBoardRenderSnapshot
{
    private readonly ReadOnlyDictionary<Vector2I, BattleBoardCellSnapshot> _cells;
    private readonly ReadOnlyDictionary<StringName, BattleBoardUnitSnapshot> _units;
    private readonly ReadOnlyCollection<BattleBoardEdgeSnapshot> _edges;
    private readonly ReadOnlyCollection<Vector2I> _objectiveMarkerCoords;
    private readonly ReadOnlyCollection<StringName> _allyUnitIds;

    internal BattleBoardRenderSnapshot(
        StringName battleId,
        Vector2I mapSize,
        StringName terrainProfileId,
        StringName activeUnitId,
        IDictionary<Vector2I, BattleBoardCellSnapshot> cells,
        IDictionary<StringName, BattleBoardUnitSnapshot> units,
        IEnumerable<BattleBoardEdgeSnapshot> edges,
        IEnumerable<Vector2I> objectiveMarkerCoords,
        IEnumerable<StringName> allyUnitIds
    )
    {
        BattleId = battleId;
        MapSize = mapSize;
        TerrainProfileId = terrainProfileId;
        ActiveUnitId = activeUnitId;
        _cells = new ReadOnlyDictionary<Vector2I, BattleBoardCellSnapshot>(
            new Dictionary<Vector2I, BattleBoardCellSnapshot>(
                cells ?? new Dictionary<Vector2I, BattleBoardCellSnapshot>()
            )
        );
        _units = new ReadOnlyDictionary<StringName, BattleBoardUnitSnapshot>(
            new Dictionary<StringName, BattleBoardUnitSnapshot>(
                units ?? new Dictionary<StringName, BattleBoardUnitSnapshot>()
            )
        );
        _edges = new List<BattleBoardEdgeSnapshot>(
            edges ?? Array.Empty<BattleBoardEdgeSnapshot>()
        ).AsReadOnly();
        _objectiveMarkerCoords = new List<Vector2I>(
            objectiveMarkerCoords ?? Array.Empty<Vector2I>()
        ).AsReadOnly();
        _allyUnitIds = new List<StringName>(
            allyUnitIds ?? Array.Empty<StringName>()
        ).AsReadOnly();
    }

    private BattleBoardRenderSnapshot(
        StringName battleId,
        Vector2I mapSize,
        StringName terrainProfileId,
        StringName activeUnitId,
        ReadOnlyDictionary<Vector2I, BattleBoardCellSnapshot> cells,
        IDictionary<StringName, BattleBoardUnitSnapshot> units,
        ReadOnlyCollection<BattleBoardEdgeSnapshot> edges,
        ReadOnlyCollection<Vector2I> objectiveMarkerCoords,
        IEnumerable<StringName> allyUnitIds
    )
    {
        BattleId = battleId;
        MapSize = mapSize;
        TerrainProfileId = terrainProfileId;
        ActiveUnitId = activeUnitId;
        _cells = cells;
        _units = new ReadOnlyDictionary<StringName, BattleBoardUnitSnapshot>(
            new Dictionary<StringName, BattleBoardUnitSnapshot>(units)
        );
        _edges = edges;
        _objectiveMarkerCoords = objectiveMarkerCoords;
        _allyUnitIds = new List<StringName>(
            allyUnitIds ?? Array.Empty<StringName>()
        ).AsReadOnly();
    }

    internal StringName BattleId { get; }
    internal Vector2I MapSize { get; }
    internal StringName TerrainProfileId { get; }
    internal StringName ActiveUnitId { get; }
    internal IReadOnlyDictionary<Vector2I, BattleBoardCellSnapshot> Cells => _cells;
    internal IReadOnlyDictionary<StringName, BattleBoardUnitSnapshot> Units => _units;
    internal IReadOnlyList<BattleBoardEdgeSnapshot> Edges => _edges;
    internal IReadOnlyList<Vector2I> ObjectiveMarkerCoords => _objectiveMarkerCoords;
    internal IReadOnlyList<StringName> AllyUnitIds => _allyUnitIds;
    internal bool IsEmpty => _cells.Count == 0;

    internal bool ContainsCell(Vector2I coord) => _cells.ContainsKey(coord);

    internal BattleBoardCellSnapshot GetCell(Vector2I coord) =>
        _cells.TryGetValue(coord, out BattleBoardCellSnapshot cell) ? cell : null;

    internal BattleBoardUnitSnapshot GetUnit(StringName unitId) =>
        _units.TryGetValue(unitId, out BattleBoardUnitSnapshot unit) ? unit : null;

    internal BattleBoardRenderSnapshot ApplyUnitUpdate(
        BattleBoardUnitUpdateSnapshot update
    )
    {
        if (update == null)
            return this;
        if (update.BattleId != BattleId)
        {
            throw new InvalidOperationException(
                $"Battle board unit update belongs to '{update.BattleId}', expected '{BattleId}'."
            );
        }

        var nextUnits = update.ReplacesAllUnits
            ? new Dictionary<StringName, BattleBoardUnitSnapshot>()
            : new Dictionary<StringName, BattleBoardUnitSnapshot>(_units);
        foreach (StringName unitId in update.RequestedUnitIds)
            nextUnits.Remove(unitId);
        foreach (
            (StringName unitId, BattleBoardUnitSnapshot unit) in update.Units
        )
            nextUnits[unitId] = unit;

        return new BattleBoardRenderSnapshot(
            BattleId,
            MapSize,
            TerrainProfileId,
            update.ActiveUnitId,
            _cells,
            nextUnits,
            _edges,
            _objectiveMarkerCoords,
            update.AllyUnitIds
        );
    }
}

internal sealed class BattleBoardSnapshotBuilder
{
    private static readonly StringName HpMaxAttributeId = "hp_max";
    private readonly BattleEdgeService _edgeService = new();

    internal BattleBoardRenderSnapshot Build(BattleState battleState)
    {
        if (battleState == null)
            return null;

        var cells = new Dictionary<Vector2I, BattleBoardCellSnapshot>();
        foreach (BattleCellState cell in battleState.Cells())
        {
            if (cell == null)
                continue;
            cells[cell.coord] = BuildCell(cell);
        }

        var units = new Dictionary<StringName, BattleBoardUnitSnapshot>();
        foreach ((StringName unitId, BattleUnitState unit) in battleState.UnitEntries())
        {
            if (unitId == "" || unit == null)
                continue;
            units[unitId] = BuildUnit(unit);
        }

        var edges = new List<BattleBoardEdgeSnapshot>();
        foreach (BattleEdgeFaceState edge in _edgeService.GetAllEdgeFaces(battleState))
        {
            if (edge == null || !edge.HasAnyFace())
                continue;
            edges.Add(
                new BattleBoardEdgeSnapshot(
                    edge.origin_coord,
                    edge.neighbor_coord,
                    edge.direction,
                    edge.drop_face_layer_heights,
                    edge.FeatureRenderKind,
                    edge.feature_layers,
                    edge.from_height
                )
            );
        }

        return new BattleBoardRenderSnapshot(
            battleState.battle_id,
            battleState.map_size,
            battleState.terrain_profile_id,
            battleState.active_unit_id,
            cells,
            units,
            edges,
            BuildObjectiveMarkerCoords(new BattleStateReadView(battleState).ObjectiveProgress),
            battleState.GetAllyUnitIdsTyped()
        );
    }

    internal BattleBoardUnitUpdateSnapshot BuildUnitUpdate(
        BattleState battleState,
        IEnumerable<StringName> changedUnitIds
    )
    {
        if (battleState == null)
            return null;

        var requestedUnitIds = new HashSet<StringName>();
        if (changedUnitIds != null)
        {
            foreach (StringName unitId in changedUnitIds)
            {
                if (unitId != "")
                    requestedUnitIds.Add(unitId);
            }
        }
        bool replacesAllUnits = requestedUnitIds.Count == 0;
        if (replacesAllUnits)
        {
            foreach ((StringName unitId, BattleUnitState _) in battleState.UnitEntries())
            {
                if (unitId != "")
                    requestedUnitIds.Add(unitId);
            }
        }
        var units = new Dictionary<StringName, BattleBoardUnitSnapshot>();
        foreach (StringName unitId in requestedUnitIds)
        {
            BattleUnitState unit = battleState.GetUnit(unitId);
            if (unit != null)
                units[unitId] = BuildUnit(unit);
        }
        return new BattleBoardUnitUpdateSnapshot(
            battleState.battle_id,
            battleState.active_unit_id,
            replacesAllUnits,
            requestedUnitIds,
            battleState.GetAllyUnitIdsTyped(),
            units
        );
    }

    private static BattleBoardCellSnapshot BuildCell(BattleCellState cell) =>
        new(
            cell.coord,
            cell.current_height,
            cell.base_terrain,
            cell.prop_ids,
            ResolveTerrainOverlayIds(cell)
        );

    private static BattleBoardUnitSnapshot BuildUnit(BattleUnitState unit)
    {
        BattleUnitGeometryReadView geometry = unit.GetGeometryReadViewTyped();
        int currentHp = unit.GetCurrentHp();
        int maxHp =
            unit.attribute_snapshot != null
                ? unit.attribute_snapshot.GetValue(HpMaxAttributeId)
                : 0;
        return new BattleBoardUnitSnapshot(
            unit.unit_id,
            unit.display_name,
            unit.faction_id,
            unit.IsAlive(),
            geometry.AnchorCoord,
            geometry.OccupiedCoords,
            unit.battle_sprite_texture_path,
            currentHp,
            Math.Max(Math.Max(maxHp, currentHp), 1)
        );
    }

    private static IReadOnlyList<StringName> ResolveTerrainOverlayIds(BattleCellState cell)
    {
        var candidates = new List<(StringName OverlayId, int Priority)>();
        foreach (BattleTerrainEffectState effect in cell.timed_terrain_effects)
        {
            if (
                effect == null
                || !BattleTerrainEffectSystem.IsTerrainEffectActive(effect)
                || effect.render_overlay_id == ""
            )
            {
                continue;
            }
            candidates.Add((effect.render_overlay_id, effect.overlay_priority));
        }
        candidates.Sort(
            (left, right) =>
            {
                int priorityOrder = right.Priority.CompareTo(left.Priority);
                return priorityOrder != 0
                    ? priorityOrder
                    : string.CompareOrdinal(
                        left.OverlayId.ToString(),
                        right.OverlayId.ToString()
                    );
            }
        );
        var result = new List<StringName>();
        foreach ((StringName overlayId, int _) in candidates)
        {
            if (!result.Contains(overlayId))
                result.Add(overlayId);
        }
        return result.AsReadOnly();
    }

    private static IReadOnlyList<Vector2I> BuildObjectiveMarkerCoords(
        BattleObjectiveProgressSnapshot progress
    )
    {
        var result = new List<Vector2I>();
        if (progress == null || !progress.IsValid)
            return result.AsReadOnly();

        switch (progress.Mode)
        {
            case BattleObjectiveMode.Escape:
            case BattleObjectiveMode.Escort:
            case BattleObjectiveMode.Intercept:
                AddUniqueCoords(result, progress.ExitCoords);
                break;
            case BattleObjectiveMode.NodeOperation:
                foreach (BattleObjectiveNodeProgressSnapshot node in progress.OperationNodes)
                {
                    if (!node.IsCompleted && !result.Contains(node.Coord))
                        result.Add(node.Coord);
                }
                break;
            case BattleObjectiveMode.Control:
                foreach (
                    BattleObjectiveControlZoneProgressSnapshot zone in progress.ControlZones
                )
                    AddUniqueCoords(result, zone.Coords);
                break;
        }
        return result.AsReadOnly();
    }

    private static void AddUniqueCoords(
        List<Vector2I> destination,
        IEnumerable<Vector2I> coords
    )
    {
        foreach (Vector2I coord in coords ?? Array.Empty<Vector2I>())
        {
            if (!destination.Contains(coord))
                destination.Add(coord);
        }
    }
}
