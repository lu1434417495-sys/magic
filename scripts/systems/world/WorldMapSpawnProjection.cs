using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

internal static class WorldMapSpawnProjection
{
    internal static GDictionary Project(WorldMapSpawnSystem.WorldBuildData worldBuild)
    {
        worldBuild ??= new WorldMapSpawnSystem.WorldBuildData();
        return new GDictionary
        {
            ["map_seed"] = worldBuild.MapSeed,
            ["settlements"] = ProjectSettlements(worldBuild.Settlements),
            ["world_npcs"] = ProjectWorldNpcs(worldBuild.WorldNpcs),
            ["encounter_anchors"] = ProjectEncounterAnchors(worldBuild.EncounterAnchors),
            ["world_events"] = ProjectWorldEvents(worldBuild.WorldEvents),
            ["mounted_submaps"] = ProjectMountedSubmaps(worldBuild.MountedSubmaps),
            ["active_submap_id"] = "",
            ["submap_return_stack"] = new GArray(),
            ["world_step"] = 0,
            ["next_equipment_instance_serial"] = 1,
            ["player_start_coord"] = worldBuild.PlayerStartCoord,
            ["player_start_settlement_id"] = worldBuild.PlayerStartSettlementId,
            ["player_start_settlement_name"] = worldBuild.PlayerStartSettlementName,
        };
    }

    private static GArray ProjectSettlements(
        IEnumerable<WorldMapSpawnSystem.SettlementInstanceData> settlements
    )
    {
        var result = new GArray();
        if (settlements == null)
            return result;
        foreach (WorldMapSpawnSystem.SettlementInstanceData settlement in settlements)
            if (settlement != null)
                result.Add(Project(settlement));
        return result;
    }

    private static GArray ProjectWorldNpcs(
        IEnumerable<WorldMapSpawnSystem.WorldNpcInstanceData> worldNpcs
    )
    {
        var result = new GArray();
        if (worldNpcs == null)
            return result;
        foreach (WorldMapSpawnSystem.WorldNpcInstanceData worldNpc in worldNpcs)
            if (worldNpc != null)
                result.Add(Project(worldNpc));
        return result;
    }

    private static GArray ProjectEncounterAnchors(
        IEnumerable<EncounterAnchorData> encounterAnchors
    )
    {
        var result = new GArray();
        if (encounterAnchors == null)
            return result;
        foreach (EncounterAnchorData encounterAnchor in encounterAnchors)
            if (encounterAnchor != null)
                result.Add(encounterAnchor);
        return result;
    }

    private static GArray ProjectWorldEvents(
        IEnumerable<WorldMapSpawnSystem.WorldEventInstanceData> worldEvents
    )
    {
        var result = new GArray();
        if (worldEvents == null)
            return result;
        foreach (WorldMapSpawnSystem.WorldEventInstanceData worldEvent in worldEvents)
            if (worldEvent != null)
                result.Add(Project(worldEvent));
        return result;
    }

    private static GDictionary ProjectMountedSubmaps(
        IEnumerable<WorldMapSpawnSystem.MountedSubmapInstanceData> mountedSubmaps
    )
    {
        var result = new GDictionary();
        if (mountedSubmaps == null)
            return result;
        foreach (WorldMapSpawnSystem.MountedSubmapInstanceData mountedSubmap in mountedSubmaps)
        {
            if (mountedSubmap == null || string.IsNullOrEmpty(mountedSubmap.SubmapId))
                continue;
            result[mountedSubmap.SubmapId] = Project(mountedSubmap);
        }
        return result;
    }

    private static GDictionary Project(WorldMapSpawnSystem.SettlementInstanceData settlement)
    {
        if (settlement == null)
            return new GDictionary();
        return new GDictionary
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
            ["facilities"] = ProjectFacilities(settlement.Facilities),
            ["is_player_start"] = settlement.IsPlayerStart,
            ["settlement_state"] = Project(settlement.SettlementState),
            ["available_services"] = ProjectServices(settlement.AvailableServices),
            ["service_npcs"] = ProjectServiceNpcs(settlement.ServiceNpcs),
        };
    }

    private static GArray ProjectFacilities(
        IEnumerable<WorldMapSpawnSystem.FacilityInstanceData> facilities
    )
    {
        var result = new GArray();
        if (facilities == null)
            return result;
        foreach (WorldMapSpawnSystem.FacilityInstanceData facility in facilities)
            if (facility != null)
                result.Add(Project(facility));
        return result;
    }

    private static GDictionary Project(WorldMapSpawnSystem.FacilityInstanceData facility)
    {
        if (facility == null)
            return new GDictionary();
        return new GDictionary
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
            ["service_npcs"] = ProjectServiceNpcs(facility.ServiceNpcs),
        };
    }

    private static GArray ProjectServices(
        IEnumerable<WorldMapSpawnSystem.ServiceEntryData> services
    )
    {
        var result = new GArray();
        if (services == null)
            return result;
        foreach (WorldMapSpawnSystem.ServiceEntryData service in services)
            if (service != null)
                result.Add(Project(service));
        return result;
    }

    private static GDictionary Project(WorldMapSpawnSystem.ServiceEntryData service)
    {
        if (service == null)
            return new GDictionary();
        return new GDictionary
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

    private static GArray ProjectServiceNpcs(
        IEnumerable<WorldMapSpawnSystem.ServiceNpcInstanceData> serviceNpcs
    )
    {
        var result = new GArray();
        if (serviceNpcs == null)
            return result;
        foreach (WorldMapSpawnSystem.ServiceNpcInstanceData serviceNpc in serviceNpcs)
            if (serviceNpc != null)
                result.Add(Project(serviceNpc));
        return result;
    }

    private static GDictionary Project(WorldMapSpawnSystem.ServiceNpcInstanceData serviceNpc)
    {
        if (serviceNpc == null)
            return new GDictionary();
        return new GDictionary
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

    private static GDictionary Project(WorldMapSpawnSystem.SettlementStateData settlementState)
    {
        if (settlementState == null)
            return new GDictionary();
        return new GDictionary
        {
            ["visited"] = settlementState.Visited,
            ["reputation"] = settlementState.Reputation,
            ["active_conditions"] = new GArray(),
            ["cooldowns"] = new GDictionary(),
            ["shop_inventory_seed"] = settlementState.ShopInventorySeed,
            ["shop_last_refresh_step"] = settlementState.ShopLastRefreshStep,
            ["shop_states"] = new GDictionary(),
        };
    }

    private static GDictionary Project(WorldMapSpawnSystem.WorldNpcInstanceData worldNpc)
    {
        if (worldNpc == null)
            return new GDictionary();
        return new GDictionary
        {
            ["entity_id"] = worldNpc.EntityId,
            ["display_name"] = worldNpc.DisplayName,
            ["coord"] = worldNpc.Coord,
            ["kind"] = worldNpc.Kind,
            ["faction_id"] = worldNpc.FactionId,
            ["vision_range"] = worldNpc.VisionRange,
        };
    }

    private static GDictionary Project(WorldMapSpawnSystem.WorldEventInstanceData worldEvent)
    {
        if (worldEvent == null)
            return new GDictionary();
        return new GDictionary
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

    private static GDictionary Project(WorldMapSpawnSystem.MountedSubmapInstanceData mountedSubmap)
    {
        if (mountedSubmap == null)
            return new GDictionary();
        return new GDictionary
        {
            ["submap_id"] = mountedSubmap.SubmapId,
            ["display_name"] = mountedSubmap.DisplayName,
            ["generation_config_path"] = mountedSubmap.GenerationConfigPath,
            ["return_hint_text"] = mountedSubmap.ReturnHintText,
            ["is_generated"] = mountedSubmap.IsGenerated,
            ["player_coord"] = mountedSubmap.PlayerCoord,
            ["world_data"] = new GDictionary(),
        };
    }
}
