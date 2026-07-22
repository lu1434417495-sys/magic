using System;
using System.Collections.Generic;
using Godot;

public sealed class WildSpawnRuleDefinition
{
    private static readonly StringName HostileFactionId = "hostile";

    public WildSpawnRuleDefinition(
        string regionTag,
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
    public string MonsterName { get; }
    public StringName EncounterProfileId { get; }
    public StringName SettlementEncounterProfileId { get; }
    public string SettlementEncounterDisplayName { get; }
    public int DensityPerChunk { get; }
    public int MinDistanceToSettlement { get; }
    public int VisionRange { get; }
    public IReadOnlyList<Vector2I> ChunkCoords { get; }
    public StringName FactionId => HostileFactionId;

    internal static WildSpawnRuleDefinition FromResource(
        WildSpawnRule source,
        string path
    )
    {
        if (source == null)
            throw WorldDefinitionProjection.Invalid(path, "resource is null");
        return new WildSpawnRuleDefinition(
            WorldDefinitionProjection.RequireString(
                source.region_tag,
                path + ".region_tag"
            ).Trim(),
            WorldDefinitionProjection.RequireString(
                source.monster_name,
                path + ".monster_name"
            ),
            source.encounter_profile_id,
            source.settlement_encounter_profile_id,
            source.settlement_encounter_display_name,
            source.density_per_chunk,
            source.min_distance_to_settlement,
            source.vision_range,
            WorldDefinitionProjection.CopyValues(
                source.ChunkCoordsProjectionBorrowed,
                path + ".chunk_coords"
            )
        );
    }
}
