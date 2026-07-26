using System;

// Single field-set owner for the persisted WorldRuntimeData root. Record-level
// schemas remain with their typed record owners.
internal static class WorldRuntimeSaveSchema
{
    internal const string MapSeed = "map_seed";
    internal const string WorldStep = "world_step";
    internal const string NextEquipmentInstanceSerial = "next_equipment_instance_serial";
    internal const string ActiveSubmapId = "active_submap_id";
    internal const string SubmapReturnStack = "submap_return_stack";
    internal const string Settlements = "settlements";
    internal const string WorldEvents = "world_events";
    internal const string EncounterAnchors = "encounter_anchors";
    internal const string ResourceNodes = "resource_nodes";
    internal const string MountedSubmaps = "mounted_submaps";
    internal const string WorldNpcs = "world_npcs";
    internal const string PlayerStartCoord = "player_start_coord";
    internal const string PlayerStartSettlementId = "player_start_settlement_id";
    internal const string PlayerStartSettlementName = "player_start_settlement_name";
    internal const string FogStates = "fog_states";

    internal static readonly string[] RequiredFields =
    {
        MapSeed,
        WorldStep,
        NextEquipmentInstanceSerial,
        ActiveSubmapId,
        SubmapReturnStack,
        Settlements,
        WorldEvents,
        EncounterAnchors,
        ResourceNodes,
        MountedSubmaps,
    };

    internal static readonly string[] OptionalFields =
    {
        WorldNpcs,
        PlayerStartCoord,
        PlayerStartSettlementId,
        PlayerStartSettlementName,
        FogStates,
    };

    internal static readonly string[] RequiredArrayFields =
    {
        SubmapReturnStack,
        Settlements,
        WorldEvents,
        EncounterAnchors,
        ResourceNodes,
    };

    internal static readonly string[] OptionalStringFields =
    {
        PlayerStartSettlementId,
        PlayerStartSettlementName,
    };
}
