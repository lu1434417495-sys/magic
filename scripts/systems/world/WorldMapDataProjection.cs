using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

internal static class WorldMapDataProjection
{
    internal static GDictionary Project(WorldRuntimeData worldData) =>
        worldData != null ? worldData.ToDictionary() : new GDictionary();

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

    internal static GDictionary Project(WorldMapSettlementRecordData settlement) =>
        settlement != null ? settlement.DuplicateSourcePayload() : new GDictionary();

    internal static Godot.Collections.Array<GDictionary> ProjectSettlementRecords(
        IEnumerable<WorldMapSettlementRecordData> settlements
    )
    {
        var result = new Godot.Collections.Array<GDictionary>();
        if (settlements == null)
            return result;
        foreach (WorldMapSettlementRecordData settlement in settlements)
            result.Add(Project(settlement));
        return result;
    }

    internal static GDictionary Project(WorldMapNpcData npc) =>
        npc != null && !npc.IsEmpty ? npc.DuplicateSourcePayload() : new GDictionary();

    internal static GDictionary Project(WorldMapEventData worldEvent) =>
        worldEvent != null ? worldEvent.DuplicateSourcePayload() : new GDictionary();

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
