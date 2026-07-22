using Godot;

internal static class WorldSaveRoundTripE2e
{
    internal const string CharacterName = "E2E Save Walker";
    internal const int InitialWorldStep = 0;
    internal const int ExpectedWorldStep = InitialWorldStep + 1;

    private static readonly MoveCandidate[] MoveCandidates =
    {
        new(Vector2I.Right, Key.D),
        new(Vector2I.Down, Key.S),
        new(Vector2I.Left, Key.A),
        new(Vector2I.Up, Key.W),
    };

    internal readonly record struct MovePlan(Vector2I TargetCoord, Key Keycode);

    private readonly record struct MoveCandidate(Vector2I Direction, Key Keycode);

    internal static bool TryChooseSafeAdjacentMove(
        WorldMapGridSystem grid,
        WorldRuntimeData worldData,
        Vector2I origin,
        out MovePlan plan
    )
    {
        plan = default;
        if (grid == null || worldData == null)
            return false;

        WorldMapSettlementRecordData originSettlement = FindSettlement(worldData, origin);
        foreach (MoveCandidate candidate in MoveCandidates)
        {
            Vector2I target = origin + candidate.Direction;
            if (!grid.IsCellWalkable(target))
                continue;

            WorldMapSettlementRecordData targetSettlement = FindSettlement(worldData, target);
            if (targetSettlement != null && !ReferenceEquals(targetSettlement, originSettlement))
                continue;
            if (HasIncidentalWorldContent(worldData, target))
                continue;

            plan = new MovePlan(target, candidate.Keycode);
            return true;
        }

        return false;
    }

    private static WorldMapSettlementRecordData FindSettlement(
        WorldRuntimeData worldData,
        Vector2I coord
    )
    {
        foreach (WorldMapSettlementRecordData settlement in worldData.Settlements)
        {
            if (Contains(settlement, coord))
                return settlement;
        }
        return null;
    }

    private static bool HasIncidentalWorldContent(
        WorldRuntimeData worldData,
        Vector2I coord
    )
    {
        foreach (WorldMapEventData worldEvent in worldData.WorldEvents)
        {
            if (worldEvent != null && worldEvent.WorldCoord == coord)
                return true;
        }

        foreach (WorldMapResourceNodeData resourceNode in worldData.ResourceNodes)
        {
            if (resourceNode != null && resourceNode.Exists && resourceNode.WorldCoord == coord)
                return true;
        }

        foreach (EncounterAnchorData encounter in worldData.EncounterAnchors)
        {
            if (encounter != null && encounter.world_coord == coord)
                return true;
        }

        foreach (WorldMapNpcData npc in worldData.WorldNpcs)
        {
            if (npc != null && npc.Exists && npc.Coord == coord)
                return true;
        }

        return false;
    }

    private static bool Contains(WorldMapSettlementRecordData settlement, Vector2I coord)
    {
        if (settlement == null)
            return false;
        Vector2I offset = coord - settlement.Origin;
        return offset.X >= 0
            && offset.Y >= 0
            && offset.X < settlement.FootprintSize.X
            && offset.Y < settlement.FootprintSize.Y;
    }
}
