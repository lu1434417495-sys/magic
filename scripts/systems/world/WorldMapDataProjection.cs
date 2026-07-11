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

    internal static GodotProjectionLease<GDictionary> ProjectLease(
        WorldMapSubmapReturnStackEntry entry
    ) =>
        RuntimePlainPayload.ProjectDictionaryLease(
            entry?.BuildSaveSnapshotPlain()
                ?? new Dictionary<string, object>(System.StringComparer.Ordinal),
            "WorldMapDataProjection.submap_return",
            LifetimeDomain.Request,
            "WorldMapDataProjection.submap_return"
        );

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

    internal static GodotProjectionLease<GDictionary> ProjectLease(
        EncounterAnchorData encounterAnchor
    ) =>
        RuntimePlainPayload.ProjectDictionaryLease(
            encounterAnchor?.BuildSaveSnapshotPlain()
                ?? new Dictionary<string, object>(System.StringComparer.Ordinal),
            "WorldMapDataProjection.encounter_anchor",
            LifetimeDomain.Request,
            "WorldMapDataProjection.encounter_anchor"
        );

    internal static GodotProjectionLease<GDictionary> ProjectLease(
        WorldMapResourceNodeData resourceNode
    ) =>
        RuntimePlainPayload.ProjectDictionaryLease(
            resourceNode != null && resourceNode.Exists
                ? resourceNode.BuildSaveSnapshotPlain()
                : new Dictionary<string, object>(System.StringComparer.Ordinal),
            "WorldMapDataProjection.resource_node",
            LifetimeDomain.Request,
            "WorldMapDataProjection.resource_node"
        );
}
