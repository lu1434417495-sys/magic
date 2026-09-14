using System;
using System.Collections.Generic;
using Godot;

public enum WorldVerticalBandKind
{
    Unknown = -1,
    All = 0,
    North = 1,
    South = 2,
}

public sealed class WildSpawnRuleDefinition
{
    private static readonly StringName HostileFactionId = "hostile";

    public WildSpawnRuleDefinition(
        string regionTag,
        WorldVerticalBandKind verticalBand,
        string monsterName,
        StringName encounterProfileId,
        StringName settlementEncounterProfileId,
        string settlementEncounterDisplayName,
        int densityPerChunk,
        int minDistanceToSettlement,
        int visionRange,
        IReadOnlyList<Vector2I> chunkCoords
    )
    {
        RegionTag = regionTag ?? throw new ArgumentNullException(nameof(regionTag));
        VerticalBand = verticalBand;
        MonsterName = monsterName ?? throw new ArgumentNullException(nameof(monsterName));
        EncounterProfileId = encounterProfileId;
        SettlementEncounterProfileId = settlementEncounterProfileId;
        SettlementEncounterDisplayName = settlementEncounterDisplayName ?? "";
        DensityPerChunk = densityPerChunk;
        MinDistanceToSettlement = minDistanceToSettlement;
        VisionRange = visionRange;
        ChunkCoords = WorldDefinitionProjection.FreezeValues(
            chunkCoords,
            nameof(chunkCoords)
        );
    }

    public string RegionTag { get; }
    public WorldVerticalBandKind VerticalBand { get; }
    public string MonsterName { get; }
    public StringName EncounterProfileId { get; }
    public StringName SettlementEncounterProfileId { get; }
    public string SettlementEncounterDisplayName { get; }
    public int DensityPerChunk { get; }
    public int MinDistanceToSettlement { get; }
    public int VisionRange { get; }
    public IReadOnlyList<Vector2I> ChunkCoords { get; }
    public StringName FactionId => HostileFactionId;

}
