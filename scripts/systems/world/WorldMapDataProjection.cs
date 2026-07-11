using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

internal static class WorldMapDataProjection
{
    internal static GodotProjectionLease<GDictionary> ProjectLease(
        WorldRuntimeData worldData
    ) =>
        RuntimePlainPayload.ProjectDictionaryLease(
            worldData?.BuildSaveSnapshotPlain()
                ?? new Dictionary<string, object>(System.StringComparer.Ordinal),
            "WorldMapDataProjection.world_data",
            LifetimeDomain.Request,
            "WorldMapDataProjection.world_data"
        );

    internal static GDictionary Project(WorldMapSubmapReturnStackEntry entry)
    {
        if (entry == null)
            return new GDictionary();
        return new GDictionary
        {
            ["map_id"] = entry.MapId,
            ["coord"] = entry.Coord,
        };
    }

    internal static GodotProjectionLease<GDictionary> ProjectLease(
        WorldMapSettlementRecordData settlement
    ) =>
        RuntimePlainPayload.ProjectDictionaryLease(
            settlement?.BuildSaveSnapshotPlain()
                ?? new Dictionary<string, object>(System.StringComparer.Ordinal),
            "WorldMapDataProjection.settlement",
            LifetimeDomain.Request,
            "WorldMapDataProjection.settlement"
        );

    internal static GodotProjectionLease<Godot.Collections.Array> ProjectSettlementRecordsLease(
        IEnumerable<WorldMapSettlementRecordData> settlements
    )
    {
        var root = new Godot.Collections.Array();
        GodotProjectionLease<Godot.Collections.Array> lease =
            GodotProjectionLease<Godot.Collections.Array>.CreateOwnedRoot(
                root,
                "WorldMapDataProjection.settlements",
                LifetimeDomain.Request,
                "WorldMapDataProjection.settlements"
            );
        try
        {
            if (settlements != null)
            {
                foreach (WorldMapSettlementRecordData settlement in settlements)
                {
                    root.Add(
                        RuntimePlainPayload.ProjectDictionaryInto(
                            lease,
                            settlement?.BuildSaveSnapshotPlain()
                                ?? new Dictionary<string, object>(
                                    System.StringComparer.Ordinal
                                ),
                            "WorldMapDataProjection.settlement"
                        )
                    );
                }
            }
            return lease;
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    internal static GodotProjectionLease<GDictionary> ProjectLease(WorldMapNpcData npc) =>
        RuntimePlainPayload.ProjectDictionaryLease(
            npc != null && !npc.IsEmpty
                ? npc.BuildSaveSnapshotPlain()
                : new Dictionary<string, object>(System.StringComparer.Ordinal),
            "WorldMapDataProjection.npc",
            LifetimeDomain.Request,
            "WorldMapDataProjection.npc"
        );

    internal static GodotProjectionLease<GDictionary> ProjectLease(WorldMapEventData worldEvent) =>
        RuntimePlainPayload.ProjectDictionaryLease(
            worldEvent?.BuildSaveSnapshotPlain()
                ?? new Dictionary<string, object>(System.StringComparer.Ordinal),
            "WorldMapDataProjection.world_event",
            LifetimeDomain.Request,
            "WorldMapDataProjection.world_event"
        );

    internal static GDictionary Project(EncounterAnchorData encounterAnchor)
    {
        if (encounterAnchor == null)
            return new GDictionary();
        return new GDictionary
        {
            ["entity_id"] = encounterAnchor.entity_id.ToString(),
            ["display_name"] = encounterAnchor.display_name,
            ["world_coord"] = encounterAnchor.world_coord,
            ["faction_id"] = encounterAnchor.faction_id.ToString(),
            ["enemy_roster_template_id"] = encounterAnchor.enemy_roster_template_id.ToString(),
            ["region_tag"] = encounterAnchor.region_tag.ToString(),
            ["vision_range"] = encounterAnchor.vision_range,
            ["is_cleared"] = encounterAnchor.is_cleared,
            ["encounter_kind"] = encounterAnchor.encounter_kind.ToString(),
            ["encounter_profile_id"] = encounterAnchor.encounter_profile_id.ToString(),
            ["growth_stage"] = encounterAnchor.growth_stage,
            ["suppressed_until_step"] = encounterAnchor.suppressed_until_step,
        };
    }

    internal static GDictionary Project(WorldMapResourceNodeData resourceNode) =>
        resourceNode != null && resourceNode.Exists
            ? resourceNode.ToDictionary()
            : new GDictionary();
}
