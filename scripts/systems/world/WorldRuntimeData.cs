using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

internal sealed class WorldRuntimeData
{
    private const string WorldMapSeedKey = "map_seed";
    private const string WorldEquipmentInstanceSerialKey = "next_equipment_instance_serial";

    private readonly List<WorldMapSubmapReturnStackEntry> _submapReturnStack = new();
    private readonly List<WorldMapSettlementRecordData> _settlements = new();
    private readonly List<WorldMapEventData> _worldEvents = new();
    private readonly List<EncounterAnchorData> _encounterAnchors = new();
    private readonly Dictionary<string, WorldMapMountedSubmapData> _mountedSubmaps =
        new(System.StringComparer.Ordinal);
    private readonly List<WorldMapNpcData> _worldNpcs = new();
    private readonly RuntimePayloadStore _fogStates = new();

    public long MapSeed { get; private set; } = 1;
    public int WorldStep { get; private set; }
    public int NextEquipmentInstanceSerial { get; private set; } = 1;
    public string ActiveSubmapId { get; private set; } = "";
    public Vector2I PlayerStartCoord { get; private set; } = Vector2I.Zero;
    public bool HasPlayerStartCoord { get; private set; }
    public string PlayerStartSettlementId { get; private set; } = "";
    public bool HasPlayerStartSettlementId { get; private set; }
    public string PlayerStartSettlementName { get; private set; } = "";
    public bool HasPlayerStartSettlementName { get; private set; }
    public bool HasFogStates { get; private set; }
    public bool HasWorldNpcs { get; private set; }

    public IReadOnlyList<WorldMapSettlementRecordData> Settlements => _settlements;
    public IReadOnlyList<EncounterAnchorData> EncounterAnchors => _encounterAnchors;

    private WorldRuntimeData() { }

    internal static WorldRuntimeData Empty() => new();

    internal static WorldRuntimeData FromDictionary(GDictionary data)
    {
        if (data == null)
        {
            return null;
        }
        WorldRuntimeData result = new();
        result.MapSeed = ReadLong(data, WorldMapSeedKey, 1L);
        result.WorldStep = ReadInt(data, "world_step", 0);
        result.NextEquipmentInstanceSerial = ReadInt(
            data,
            WorldEquipmentInstanceSerialKey,
            1
        );
        result.ActiveSubmapId = ReadString(data, "active_submap_id");
        result.HasWorldNpcs = data.ContainsKey("world_npcs");
        result.HasPlayerStartCoord = data.ContainsKey("player_start_coord");
        result.PlayerStartCoord = ReadVector2I(data, "player_start_coord", Vector2I.Zero);
        result.HasPlayerStartSettlementId = data.ContainsKey("player_start_settlement_id");
        result.PlayerStartSettlementId = ReadString(data, "player_start_settlement_id");
        result.HasPlayerStartSettlementName = data.ContainsKey("player_start_settlement_name");
        result.PlayerStartSettlementName = ReadString(data, "player_start_settlement_name");
        result.HasFogStates = data.ContainsKey("fog_states");
        if (result.HasFogStates)
        {
            result._fogStates.ReplaceWithPayload(ReadDictionary(data, "fog_states"));
        }

        if (!ReadReturnStack(result._submapReturnStack, ReadArray(data, "submap_return_stack")))
            return null;
        if (!ReadSettlements(result._settlements, ReadArray(data, "settlements")))
            return null;
        if (!ReadWorldEvents(result._worldEvents, ReadArray(data, "world_events")))
            return null;
        if (!ReadEncounterAnchors(result._encounterAnchors, ReadArray(data, "encounter_anchors")))
            return null;
        if (!ReadMountedSubmaps(result._mountedSubmaps, ReadDictionary(data, "mounted_submaps")))
            return null;
        if (!ReadWorldNpcs(result._worldNpcs, ReadArray(data, "world_npcs")))
            return null;
        return result;
    }

    internal GDictionary ToDictionary()
    {
        GDictionary result = new()
        {
            [WorldMapSeedKey] = MapSeed,
            ["world_step"] = WorldStep,
            [WorldEquipmentInstanceSerialKey] = NextEquipmentInstanceSerial,
            ["active_submap_id"] = ActiveSubmapId,
            ["submap_return_stack"] = ProjectReturnStack(),
            ["settlements"] = ProjectSettlements(),
            ["world_events"] = ProjectWorldEvents(),
            ["encounter_anchors"] = ProjectEncounterAnchors(),
            ["mounted_submaps"] = ProjectMountedSubmaps(),
        };
        if (HasWorldNpcs)
        {
            result["world_npcs"] = ProjectWorldNpcs();
        }
        if (HasPlayerStartCoord)
        {
            result["player_start_coord"] = PlayerStartCoord;
        }
        if (HasPlayerStartSettlementId)
        {
            result["player_start_settlement_id"] = PlayerStartSettlementId;
        }
        if (HasPlayerStartSettlementName)
        {
            result["player_start_settlement_name"] = PlayerStartSettlementName;
        }
        if (HasFogStates)
        {
            result["fog_states"] = _fogStates.ProjectPayload();
        }
        return result;
    }

    internal bool TrySetSettlementState(string settlementId, WorldMapSettlementStateData state)
    {
        if (string.IsNullOrEmpty(settlementId) || state == null)
        {
            return false;
        }
        for (int index = 0; index < _settlements.Count; index++)
        {
            WorldMapSettlementRecordData settlement = _settlements[index];
            if (settlement == null || settlement.SettlementId != settlementId)
            {
                continue;
            }
            GDictionary payload = settlement.DuplicateSourcePayload();
            payload["settlement_state"] = state.ToDictionary();
            _settlements[index] = WorldMapSettlementRecordData.FromDictionary(payload);
            return true;
        }
        return false;
    }

    internal bool MarkSettlementVisited(string settlementId)
    {
        WorldMapSettlementStateData current = GetSettlementStateData(settlementId);
        if (current == null || current.Visited)
        {
            return false;
        }
        return TrySetSettlementState(
            settlementId,
            WorldMapSettlementStateData.Create(true, current.Reputation, current.ActiveConditions)
        );
    }

    internal bool RemoveEncounterAnchorById(StringName encounterId)
    {
        if (encounterId == "")
        {
            return false;
        }
        for (int index = 0; index < _encounterAnchors.Count; index++)
        {
            EncounterAnchorData encounterAnchor = _encounterAnchors[index];
            if (encounterAnchor == null || encounterAnchor.entity_id != encounterId)
            {
                continue;
            }
            _encounterAnchors.RemoveAt(index);
            return true;
        }
        return false;
    }

    internal void SetWorldStep(int worldStep)
    {
        WorldStep = worldStep;
    }

    internal WorldMapSettlementStateData GetSettlementStateData(string settlementId)
    {
        if (string.IsNullOrEmpty(settlementId))
        {
            return WorldMapSettlementStateData.Create(false, 0, System.Array.Empty<string>());
        }
        foreach (WorldMapSettlementRecordData settlement in _settlements)
        {
            if (settlement != null && settlement.SettlementId == settlementId)
            {
                return WorldMapSettlementStateData.FromDictionary(
                    settlement.GetSettlementStateDictionary()
                );
            }
        }
        return WorldMapSettlementStateData.Create(false, 0, System.Array.Empty<string>());
    }

    private GArray ProjectReturnStack()
    {
        GArray result = new();
        foreach (WorldMapSubmapReturnStackEntry entry in _submapReturnStack)
        {
            result.Add(WorldMapDataProjection.Project(entry));
        }
        return result;
    }

    private GArray ProjectSettlements()
    {
        GArray result = new();
        foreach (WorldMapSettlementRecordData settlement in _settlements)
        {
            result.Add(WorldMapDataProjection.Project(settlement));
        }
        return result;
    }

    private GArray ProjectWorldEvents()
    {
        GArray result = new();
        foreach (WorldMapEventData worldEvent in _worldEvents)
        {
            result.Add(WorldMapDataProjection.Project(worldEvent));
        }
        return result;
    }

    private GArray ProjectEncounterAnchors()
    {
        GArray result = new();
        foreach (EncounterAnchorData encounterAnchor in _encounterAnchors)
        {
            if (encounterAnchor != null)
            {
                result.Add(WorldMapDataProjection.Project(encounterAnchor));
            }
        }
        return result;
    }

    private GDictionary ProjectMountedSubmaps()
    {
        GDictionary result = new();
        foreach (KeyValuePair<string, WorldMapMountedSubmapData> entry in _mountedSubmaps)
        {
            WorldMapMountedSubmapData submap = entry.Value;
            if (submap == null || string.IsNullOrEmpty(entry.Key))
            {
                continue;
            }
            result[entry.Key] = new GDictionary
            {
                ["submap_id"] = entry.Key,
                ["display_name"] = submap.DisplayName,
                ["generation_config_path"] = submap.GenerationConfigPath,
                ["return_hint_text"] = submap.ReturnHintText,
                ["is_generated"] = submap.IsGenerated,
                ["player_coord"] = submap.PlayerCoord,
                ["world_data"] = submap.ProjectWorldDataPayload(),
            };
        }
        return result;
    }

    private GArray ProjectWorldNpcs()
    {
        GArray result = new();
        foreach (WorldMapNpcData npc in _worldNpcs)
        {
            result.Add(WorldMapDataProjection.Project(npc));
        }
        return result;
    }

    private static bool ReadReturnStack(
        List<WorldMapSubmapReturnStackEntry> target,
        GArray values
    )
    {
        foreach (Variant value in values)
        {
            if (value.VariantType != Variant.Type.Dictionary)
                return false;
            target.Add(WorldMapSubmapReturnStackEntry.FromDictionary(value.AsGodotDictionary()));
        }
        return true;
    }

    private static bool ReadSettlements(List<WorldMapSettlementRecordData> target, GArray values)
    {
        foreach (Variant value in values)
        {
            if (value.VariantType != Variant.Type.Dictionary)
                return false;
            GDictionary payload = value.AsGodotDictionary();
            if (
                payload.ContainsKey("settlement_state")
                && payload["settlement_state"].VariantType != Variant.Type.Dictionary
            )
                return false;
            WorldMapSettlementRecordData settlement =
                WorldMapSettlementRecordData.FromDictionary(payload);
            if (settlement != null)
            {
                target.Add(settlement);
            }
        }
        return true;
    }

    private static bool ReadWorldEvents(List<WorldMapEventData> target, GArray values)
    {
        foreach (Variant value in values)
        {
            if (value.VariantType != Variant.Type.Dictionary)
                return false;
            WorldMapEventData worldEvent = WorldMapEventData.FromDictionary(
                value.AsGodotDictionary()
            );
            if (worldEvent != null)
            {
                target.Add(worldEvent);
            }
        }
        return true;
    }

    private static bool ReadEncounterAnchors(List<EncounterAnchorData> target, GArray values)
    {
        foreach (Variant value in values)
        {
            if (value.VariantType != Variant.Type.Dictionary)
                return false;
            EncounterAnchorData encounterAnchor = EncounterAnchorData.FromDictionary(
                value.AsGodotDictionary()
            );
            if (encounterAnchor != null)
            {
                target.Add(encounterAnchor);
            }
        }
        return true;
    }

    private static bool ReadMountedSubmaps(
        Dictionary<string, WorldMapMountedSubmapData> target,
        GDictionary values
    )
    {
        if (values == null)
        {
            return true;
        }
        foreach (Variant key in values.Keys)
        {
            Variant value = values[key];
            if (value.VariantType != Variant.Type.Dictionary)
                return false;
            string submapId = VariantText(key);
            WorldMapMountedSubmapData submap = WorldMapMountedSubmapData.FromDictionary(
                value.AsGodotDictionary()
            );
            if (!string.IsNullOrEmpty(submapId) && submap != null && submap.Exists)
            {
                target[submapId] = submap;
            }
        }
        return true;
    }

    private static bool ReadWorldNpcs(List<WorldMapNpcData> target, GArray values)
    {
        foreach (Variant value in values)
        {
            if (value.VariantType != Variant.Type.Dictionary)
                return false;
            WorldMapNpcData npc = WorldMapNpcData.FromDictionary(value.AsGodotDictionary());
            if (npc != null && !npc.IsEmpty)
            {
                target.Add(npc);
            }
        }
        return true;
    }

    private static GArray ReadArray(GDictionary data, string key)
    {
        if (data == null || !data.ContainsKey(key))
            return new GArray();
        Variant value = data[key];
        return value.VariantType == Variant.Type.Array ? value.AsGodotArray() : new GArray();
    }

    private static GDictionary ReadDictionary(GDictionary data, string key)
    {
        if (data == null || !data.ContainsKey(key))
            return new GDictionary();
        Variant value = data[key];
        return value.VariantType == Variant.Type.Dictionary
            ? value.AsGodotDictionary()
            : new GDictionary();
    }

    private static string ReadString(GDictionary data, string key, string fallback = "")
    {
        if (data == null || !data.ContainsKey(key))
            return fallback ?? "";
        return VariantText(data[key], fallback);
    }

    private static int ReadInt(GDictionary data, string key, int fallback)
    {
        if (data == null || !data.ContainsKey(key))
            return fallback;
        Variant value = data[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static long ReadLong(GDictionary data, string key, long fallback)
    {
        if (data == null || !data.ContainsKey(key))
            return fallback;
        Variant value = data[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt64() : fallback;
    }

    private static Vector2I ReadVector2I(GDictionary data, string key, Vector2I fallback)
    {
        if (data == null || !data.ContainsKey(key))
            return fallback;
        Variant value = data[key];
        return value.VariantType == Variant.Type.Vector2I ? value.AsVector2I() : fallback;
    }

    private static string VariantText(Variant value, string fallback = "")
    {
        return value.VariantType switch
        {
            Variant.Type.String => value.AsString(),
            Variant.Type.StringName => value.AsStringName().ToString(),
            _ => fallback ?? "",
        };
    }
}
