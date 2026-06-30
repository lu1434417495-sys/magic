using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public enum EquipmentAbilitySourceKind
{
    Unknown = 0,
    PlayerPersistentEquipment,
    EnemyBattleOnlyEquipment,
}

public sealed class BattleEquipmentAbilitySourceState
{
    private static readonly StringName SourceKindPlayerPersistentEquipment =
        "player_persistent_equipment";
    private static readonly StringName SourceKindEnemyBattleOnlyEquipment =
        "enemy_battle_only_equipment";

    private static readonly string[] RequiredFields =
    {
        "effective_instance_key",
        "equipment_def_id",
        "source_equipment_instance_id",
        "source_kind",
        "ability_ids",
    };

    public StringName EffectiveInstanceKey { get; set; } = "";
    public StringName EquipmentDefId { get; set; } = "";
    public StringName SourceEquipmentInstanceId { get; set; } = "";
    public EquipmentAbilitySourceKind SourceKind { get; set; } =
        EquipmentAbilitySourceKind.Unknown;
    public List<StringName> AbilityIds { get; set; } = new();

    public BattleEquipmentAbilitySourceState DuplicateState()
    {
        return new BattleEquipmentAbilitySourceState
        {
            EffectiveInstanceKey = EffectiveInstanceKey,
            EquipmentDefId = EquipmentDefId,
            SourceEquipmentInstanceId = SourceEquipmentInstanceId,
            SourceKind = SourceKind,
            AbilityIds = new List<StringName>(AbilityIds ?? new List<StringName>()),
        };
    }

    internal GDictionary ToDictionary()
    {
        return new GDictionary
        {
            ["effective_instance_key"] = EffectiveInstanceKey.ToString(),
            ["equipment_def_id"] = EquipmentDefId.ToString(),
            ["source_equipment_instance_id"] = SourceEquipmentInstanceId.ToString(),
            ["source_kind"] = ToStringName(SourceKind).ToString(),
            ["ability_ids"] = AbilityIdsToPayloadArray(AbilityIds),
        };
    }

    internal static BattleEquipmentAbilitySourceState FromDictionary(GDictionary payload)
    {
        if (!IsValidPayload(payload))
            return null;

        return new BattleEquipmentAbilitySourceState
        {
            EffectiveInstanceKey = ToStringName(payload["effective_instance_key"]),
            EquipmentDefId = ToStringName(payload["equipment_def_id"]),
            SourceEquipmentInstanceId = ToStringName(payload["source_equipment_instance_id"]),
            SourceKind = ToSourceKind(ToStringName(payload["source_kind"])),
            AbilityIds = AbilityIdsFromPayloadArray(payload["ability_ids"].AsGodotArray()),
        };
    }

    internal static StringName ToStringName(EquipmentAbilitySourceKind kind)
    {
        return kind switch
        {
            EquipmentAbilitySourceKind.PlayerPersistentEquipment =>
                SourceKindPlayerPersistentEquipment,
            EquipmentAbilitySourceKind.EnemyBattleOnlyEquipment =>
                SourceKindEnemyBattleOnlyEquipment,
            _ => "",
        };
    }

    internal static EquipmentAbilitySourceKind ToSourceKind(StringName value)
    {
        if (value == SourceKindPlayerPersistentEquipment)
            return EquipmentAbilitySourceKind.PlayerPersistentEquipment;
        if (value == SourceKindEnemyBattleOnlyEquipment)
            return EquipmentAbilitySourceKind.EnemyBattleOnlyEquipment;
        return EquipmentAbilitySourceKind.Unknown;
    }

    private static bool IsValidPayload(GDictionary payload)
    {
        if (payload == null || payload.Count != RequiredFields.Length)
            return false;
        foreach (string fieldName in RequiredFields)
            if (!payload.ContainsKey(fieldName))
                return false;
        foreach (Variant rawKey in payload.Keys)
            if (rawKey.VariantType != Variant.Type.String || !ContainsField(rawKey.AsString()))
                return false;

        foreach (
            string fieldName in new[]
            {
                "effective_instance_key",
                "equipment_def_id",
                "source_equipment_instance_id",
                "source_kind",
            }
        )
        {
            if (!IsStringNameField(payload, fieldName))
                return false;
        }

        if (payload["ability_ids"].VariantType != Variant.Type.Array)
            return false;

        StringName effectiveInstanceKey = ToStringName(payload["effective_instance_key"]);
        StringName equipmentDefId = ToStringName(payload["equipment_def_id"]);
        StringName sourceEquipmentInstanceId = ToStringName(
            payload["source_equipment_instance_id"]
        );
        EquipmentAbilitySourceKind sourceKind = ToSourceKind(ToStringName(payload["source_kind"]));
        List<StringName> abilityIds = AbilityIdsFromPayloadArray(
            payload["ability_ids"].AsGodotArray()
        );

        if (effectiveInstanceKey == "" || equipmentDefId == "")
            return false;
        if (sourceKind == EquipmentAbilitySourceKind.Unknown)
            return false;
        if (abilityIds == null || abilityIds.Count == 0)
            return false;
        if (
            sourceKind == EquipmentAbilitySourceKind.PlayerPersistentEquipment
            && sourceEquipmentInstanceId == ""
        )
            return false;
        if (
            sourceKind == EquipmentAbilitySourceKind.EnemyBattleOnlyEquipment
            && sourceEquipmentInstanceId != ""
        )
            return false;

        return true;
    }

    private static GArray AbilityIdsToPayloadArray(IEnumerable<StringName> abilityIds)
    {
        GArray result = new();
        if (abilityIds == null)
            return result;
        foreach (StringName abilityId in abilityIds)
        {
            StringName normalized = ProgressionDataUtils.to_string_name(abilityId);
            if (normalized != "")
                result.Add(normalized.ToString());
        }
        return result;
    }

    private static List<StringName> AbilityIdsFromPayloadArray(GArray values)
    {
        if (values == null)
            return null;
        List<StringName> result = new();
        HashSet<StringName> seen = new();
        foreach (Variant value in values)
        {
            if (!IsStringNamePayloadType(value.VariantType.ToString()))
                return null;
            StringName normalized = ToStringName(value);
            if (normalized == "" || !seen.Add(normalized))
                return null;
            result.Add(normalized);
        }
        return result;
    }

    private static bool ContainsField(string value)
    {
        foreach (string fieldName in RequiredFields)
            if (fieldName == value)
                return true;
        return false;
    }

    private static bool IsStringNameField(GDictionary data, string key)
    {
        return data != null
            && data.ContainsKey(key)
            && IsStringNamePayloadType(data[key].VariantType.ToString());
    }

    private static bool IsStringNamePayloadType(string valueType)
    {
        return valueType == "String" || valueType == "StringName";
    }

    private static StringName ToStringName(Variant value)
    {
        return ProgressionDataUtils.to_string_name(value);
    }
}
