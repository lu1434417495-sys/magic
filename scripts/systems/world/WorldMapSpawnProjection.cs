using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

internal static class WorldMapSpawnProjection
{
    internal static Dictionary<string, object> BuildSnapshotPlain(
        WorldMapSpawnSystem.WorldBuildData worldBuild
    )
    {
        worldBuild ??= new WorldMapSpawnSystem.WorldBuildData();
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["map_seed"] = worldBuild.MapSeed,
            ["settlements"] = BuildSettlementsPlain(worldBuild.Settlements),
            ["world_npcs"] = BuildWorldNpcsPlain(worldBuild.WorldNpcs),
            ["encounter_anchors"] = BuildEncounterAnchorsPlain(
                worldBuild.EncounterAnchors
            ),
            ["resource_nodes"] = BuildResourceNodesPlain(worldBuild.ResourceNodes),
            ["world_events"] = BuildWorldEventsPlain(worldBuild.WorldEvents),
            ["mounted_submaps"] = BuildMountedSubmapsPlain(worldBuild.MountedSubmaps),
            ["active_submap_id"] = "",
            ["submap_return_stack"] = new List<object>(),
            ["world_step"] = 0,
            ["next_equipment_instance_serial"] = 1,
            ["player_start_coord"] = worldBuild.PlayerStartCoord,
            ["player_start_settlement_id"] = worldBuild.PlayerStartSettlementId,
            ["player_start_settlement_name"] = worldBuild.PlayerStartSettlementName,
        };
    }

    internal static GodotProjectionLease<GDictionary> ProjectLease(
        WorldMapSpawnSystem.WorldBuildData worldBuild,
        string ownerId
    ) =>
        RuntimePlainPayload.ProjectDictionaryLease(
            BuildSnapshotPlain(worldBuild),
            ownerId,
            LifetimeDomain.Request,
            ownerId
        );

    private static List<object> BuildSettlementsPlain(
        IEnumerable<WorldMapSpawnSystem.SettlementInstanceData> settlements
    )
    {
        var result = new List<object>();
        if (settlements == null)
            return result;
        foreach (WorldMapSpawnSystem.SettlementInstanceData settlement in settlements)
            if (settlement != null)
                result.Add(BuildPlain(settlement));
        return result;
    }

    private static List<object> BuildWorldNpcsPlain(
        IEnumerable<WorldMapSpawnSystem.WorldNpcInstanceData> worldNpcs
    )
    {
        var result = new List<object>();
        if (worldNpcs == null)
            return result;
        foreach (WorldMapSpawnSystem.WorldNpcInstanceData worldNpc in worldNpcs)
            if (worldNpc != null)
                result.Add(BuildPlain(worldNpc));
        return result;
    }

    private static List<object> BuildEncounterAnchorsPlain(
        IEnumerable<EncounterAnchorData> encounterAnchors
    )
    {
        var result = new List<object>();
        if (encounterAnchors == null)
            return result;
        foreach (EncounterAnchorData encounterAnchor in encounterAnchors)
            if (encounterAnchor != null)
                result.Add(encounterAnchor.BuildSaveSnapshotPlain());
        return result;
    }

    private static List<object> BuildResourceNodesPlain(
        IEnumerable<WorldMapResourceNodeData> resourceNodes
    )
    {
        var result = new List<object>();
        if (resourceNodes == null)
            return result;
        foreach (WorldMapResourceNodeData resourceNode in resourceNodes)
            if (resourceNode != null && resourceNode.Exists)
                result.Add(resourceNode.BuildSaveSnapshotPlain());
        return result;
    }

    private static List<object> BuildWorldEventsPlain(
        IEnumerable<WorldMapSpawnSystem.WorldEventInstanceData> worldEvents
    )
    {
        var result = new List<object>();
        if (worldEvents == null)
            return result;
        foreach (WorldMapSpawnSystem.WorldEventInstanceData worldEvent in worldEvents)
            if (worldEvent != null)
                result.Add(BuildPlain(worldEvent));
        return result;
    }

    private static Dictionary<string, object> BuildMountedSubmapsPlain(
        IEnumerable<WorldMapSpawnSystem.MountedSubmapInstanceData> mountedSubmaps
    )
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        if (mountedSubmaps == null)
            return result;
        foreach (WorldMapSpawnSystem.MountedSubmapInstanceData mountedSubmap in mountedSubmaps)
        {
            if (mountedSubmap == null || string.IsNullOrEmpty(mountedSubmap.SubmapId))
                continue;
            result[mountedSubmap.SubmapId] = BuildPlain(mountedSubmap);
        }
        return result;
    }

    private static Dictionary<string, object> BuildPlain(
        WorldMapSpawnSystem.SettlementInstanceData settlement
    )
    {
        if (settlement == null)
            return EmptyDictionary();
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["entity_id"] = settlement.EntityId,
            ["template_id"] = settlement.TemplateId,
            ["settlement_id"] = settlement.SettlementId,
            ["display_name"] = settlement.DisplayName,
            ["tier"] = settlement.Tier,
            ["tier_name"] = settlement.TierName,
            ["faction_id"] = settlement.FactionId,
            ["origin"] = settlement.Origin,
            ["footprint_size"] = settlement.FootprintSize,
            ["facilities"] = BuildFacilitiesPlain(settlement.Facilities),
            ["is_player_start"] = settlement.IsPlayerStart,
            ["settlement_state"] = BuildPlain(settlement.SettlementState),
            ["available_services"] = BuildServicesPlain(settlement.AvailableServices),
            ["service_npcs"] = BuildServiceNpcsPlain(settlement.ServiceNpcs),
        };
    }

    private static List<object> BuildFacilitiesPlain(
        IEnumerable<WorldMapSpawnSystem.FacilityInstanceData> facilities
    )
    {
        var result = new List<object>();
        if (facilities == null)
            return result;
        foreach (WorldMapSpawnSystem.FacilityInstanceData facility in facilities)
            if (facility != null)
                result.Add(BuildPlain(facility));
        return result;
    }

    private static Dictionary<string, object> BuildPlain(
        WorldMapSpawnSystem.FacilityInstanceData facility
    )
    {
        if (facility == null)
            return EmptyDictionary();
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["template_id"] = facility.TemplateId,
            ["facility_id"] = facility.FacilityId,
            ["display_name"] = facility.DisplayName,
            ["category"] = facility.Category,
            ["interaction_type"] = facility.InteractionType,
            ["slot_id"] = facility.SlotId,
            ["slot_tag"] = facility.SlotTag,
            ["local_coord"] = facility.LocalCoord,
            ["world_coord"] = facility.WorldCoord,
            ["settlement_id"] = facility.SettlementId,
            ["service_npcs"] = BuildServiceNpcsPlain(facility.ServiceNpcs),
        };
    }

    private static List<object> BuildServicesPlain(
        IEnumerable<WorldMapSpawnSystem.ServiceEntryData> services
    )
    {
        var result = new List<object>();
        if (services == null)
            return result;
        foreach (WorldMapSpawnSystem.ServiceEntryData service in services)
            if (service != null)
                result.Add(BuildPlain(service));
        return result;
    }

    private static Dictionary<string, object> BuildPlain(
        WorldMapSpawnSystem.ServiceEntryData service
    )
    {
        if (service == null)
            return EmptyDictionary();
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["settlement_id"] = service.SettlementId,
            ["facility_id"] = service.FacilityId,
            ["facility_template_id"] = service.FacilityTemplateId,
            ["facility_name"] = service.FacilityName,
            ["npc_id"] = service.NpcId,
            ["npc_template_id"] = service.NpcTemplateId,
            ["npc_name"] = service.NpcName,
            ["service_type"] = service.ServiceType,
            ["action_id"] = service.ActionId,
            ["interaction_script_id"] = service.InteractionScriptId,
        };
    }

    private static List<object> BuildServiceNpcsPlain(
        IEnumerable<WorldMapSpawnSystem.ServiceNpcInstanceData> serviceNpcs
    )
    {
        var result = new List<object>();
        if (serviceNpcs == null)
            return result;
        foreach (WorldMapSpawnSystem.ServiceNpcInstanceData serviceNpc in serviceNpcs)
            if (serviceNpc != null)
                result.Add(BuildPlain(serviceNpc));
        return result;
    }

    private static Dictionary<string, object> BuildPlain(
        WorldMapSpawnSystem.ServiceNpcInstanceData serviceNpc
    )
    {
        if (serviceNpc == null)
            return EmptyDictionary();
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["template_id"] = serviceNpc.TemplateId,
            ["npc_id"] = serviceNpc.NpcId,
            ["display_name"] = serviceNpc.DisplayName,
            ["service_type"] = serviceNpc.ServiceType,
            ["interaction_script_id"] = serviceNpc.InteractionScriptId,
            ["local_slot_id"] = serviceNpc.LocalSlotId,
            ["facility_id"] = serviceNpc.FacilityId,
            ["facility_template_id"] = serviceNpc.FacilityTemplateId,
            ["facility_name"] = serviceNpc.FacilityName,
            ["settlement_id"] = serviceNpc.SettlementId,
        };
    }

    private static Dictionary<string, object> BuildPlain(
        WorldMapSpawnSystem.SettlementStateData settlementState
    )
    {
        if (settlementState == null)
            return EmptyDictionary();
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["visited"] = settlementState.Visited,
            ["reputation"] = settlementState.Reputation,
            ["active_conditions"] = new List<object>(),
            ["cooldowns"] = EmptyDictionary(),
            ["shop_inventory_seed"] = settlementState.ShopInventorySeed,
            ["shop_last_refresh_step"] = settlementState.ShopLastRefreshStep,
            ["shop_states"] = EmptyDictionary(),
        };
    }

    private static Dictionary<string, object> BuildPlain(
        WorldMapSpawnSystem.WorldNpcInstanceData worldNpc
    )
    {
        if (worldNpc == null)
            return EmptyDictionary();
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["entity_id"] = worldNpc.EntityId,
            ["display_name"] = worldNpc.DisplayName,
            ["coord"] = worldNpc.Coord,
            ["kind"] = worldNpc.Kind,
            ["faction_id"] = worldNpc.FactionId,
            ["vision_range"] = worldNpc.VisionRange,
        };
    }

    private static Dictionary<string, object> BuildPlain(
        WorldMapSpawnSystem.WorldEventInstanceData worldEvent
    )
    {
        if (worldEvent == null)
            return EmptyDictionary();
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["event_id"] = worldEvent.EventId,
            ["display_name"] = worldEvent.DisplayName,
            ["world_coord"] = worldEvent.WorldCoord,
            ["event_type"] = worldEvent.EventType,
            ["target_submap_id"] = worldEvent.TargetSubmapId,
            ["discovery_condition_id"] = worldEvent.DiscoveryConditionId,
            ["prompt_title"] = worldEvent.PromptTitle,
            ["prompt_text"] = worldEvent.PromptText,
            ["is_discovered"] = worldEvent.IsDiscovered,
        };
    }

    private static Dictionary<string, object> BuildPlain(
        WorldMapSpawnSystem.MountedSubmapInstanceData mountedSubmap
    )
    {
        if (mountedSubmap == null)
            return EmptyDictionary();
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["submap_id"] = mountedSubmap.SubmapId,
            ["display_name"] = mountedSubmap.DisplayName,
            ["generation_config_path"] = mountedSubmap.GenerationConfigPath,
            ["return_hint_text"] = mountedSubmap.ReturnHintText,
            ["is_generated"] = mountedSubmap.IsGenerated,
            ["player_coord"] = mountedSubmap.PlayerCoord,
            ["world_data"] = EmptyDictionary(),
        };
    }

    private static Dictionary<string, object> EmptyDictionary() =>
        new(StringComparer.Ordinal);
}
