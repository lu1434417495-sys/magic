using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public enum BattleStatusSourceKind
{
    Unknown = 0,
    Skill,
    EquipmentAbility,
    TerrainEffect,
    RuntimeEffect,
}

internal static class BattleStatusSourceKindRules
{
    internal static StringName ToStringName(BattleStatusSourceKind kind) =>
        kind switch
        {
            BattleStatusSourceKind.Skill => "skill",
            BattleStatusSourceKind.EquipmentAbility => "equipment_ability",
            BattleStatusSourceKind.TerrainEffect => "terrain_effect",
            BattleStatusSourceKind.RuntimeEffect => "runtime_effect",
            _ => "",
        };

    internal static BattleStatusSourceKind FromStringName(StringName value) =>
        ProgressionDataUtils.to_string_name(value).ToString() switch
        {
            "skill" => BattleStatusSourceKind.Skill,
            "equipment_ability" => BattleStatusSourceKind.EquipmentAbility,
            "terrain_effect" => BattleStatusSourceKind.TerrainEffect,
            "runtime_effect" => BattleStatusSourceKind.RuntimeEffect,
            _ => BattleStatusSourceKind.Unknown,
        };
}

public readonly record struct BattleStatusSourceIdentity(
    BattleStatusSourceKind Kind,
    StringName SourceUnitId,
    StringName SourceDefinitionId
)
{
    internal bool IsValid =>
        Kind != BattleStatusSourceKind.Unknown
        && ProgressionDataUtils.to_string_name(SourceDefinitionId) != "";

    internal StringName KindId => BattleStatusSourceKindRules.ToStringName(Kind);

    internal string StableKey =>
        $"{KindId}:{ProgressionDataUtils.to_string_name(SourceUnitId)}:{ProgressionDataUtils.to_string_name(SourceDefinitionId)}";

    internal static BattleStatusSourceIdentity Skill(
        StringName sourceUnitId,
        StringName skillId
    ) =>
        new(
            BattleStatusSourceKind.Skill,
            ProgressionDataUtils.to_string_name(sourceUnitId),
            ProgressionDataUtils.to_string_name(skillId)
        );

    internal static BattleStatusSourceIdentity EquipmentAbility(
        StringName sourceUnitId,
        StringName bindingId
    ) =>
        new(
            BattleStatusSourceKind.EquipmentAbility,
            ProgressionDataUtils.to_string_name(sourceUnitId),
            ProgressionDataUtils.to_string_name(bindingId)
        );

    internal static BattleStatusSourceIdentity TerrainEffect(
        StringName sourceUnitId,
        StringName effectId
    ) =>
        new(
            BattleStatusSourceKind.TerrainEffect,
            ProgressionDataUtils.to_string_name(sourceUnitId),
            ProgressionDataUtils.to_string_name(effectId)
        );

    internal static BattleStatusSourceIdentity RuntimeEffect(
        StringName sourceUnitId,
        StringName definitionId
    ) =>
        new(
            BattleStatusSourceKind.RuntimeEffect,
            ProgressionDataUtils.to_string_name(sourceUnitId),
            ProgressionDataUtils.to_string_name(definitionId)
        );
}

internal sealed class BattleStatusSourceContributionState
{
    private static readonly HashSet<string> SchemaFields = new(StringComparer.Ordinal)
    {
        "source_kind",
        "source_unit_id",
        "source_definition_id",
        "power",
        "stacks",
        "duration_tu",
        "tick_interval_tu",
        "next_tick_at_tu",
        "timeline_damage_dice_count",
        "timeline_damage_dice_sides",
        "timeline_damage_flat_bonus",
        "damage_tag",
    };

    internal BattleStatusSourceIdentity Identity { get; set; }
    internal int Power { get; set; }
    internal int Stacks { get; set; }
    internal int DurationTu { get; set; } = -1;
    internal int TickIntervalTu { get; set; }
    internal int NextTickAtTu { get; set; }
    internal int TimelineDamageDiceCount { get; set; }
    internal int TimelineDamageDiceSides { get; set; }
    internal int TimelineDamageFlatBonus { get; set; }
    internal StringName DamageTag { get; set; } = "";

    internal bool IsValid => Identity.IsValid && Stacks > 0;

    internal BattleStatusSourceContributionState Duplicate() =>
        new()
        {
            Identity = Identity,
            Power = Power,
            Stacks = Stacks,
            DurationTu = DurationTu,
            TickIntervalTu = TickIntervalTu,
            NextTickAtTu = NextTickAtTu,
            TimelineDamageDiceCount = TimelineDamageDiceCount,
            TimelineDamageDiceSides = TimelineDamageDiceSides,
            TimelineDamageFlatBonus = TimelineDamageFlatBonus,
            DamageTag = DamageTag,
        };

    internal Dictionary<string, object> BuildSnapshotPlain() =>
        new(StringComparer.Ordinal)
        {
            ["source_kind"] = Identity.KindId.ToString(),
            ["source_unit_id"] = Identity.SourceUnitId.ToString(),
            ["source_definition_id"] = Identity.SourceDefinitionId.ToString(),
            ["power"] = Math.Max(Power, 0),
            ["stacks"] = Math.Max(Stacks, 1),
            ["duration_tu"] = DurationTu,
            ["tick_interval_tu"] = Math.Max(TickIntervalTu, 0),
            ["next_tick_at_tu"] = Math.Max(NextTickAtTu, 0),
            ["timeline_damage_dice_count"] = Math.Max(TimelineDamageDiceCount, 0),
            ["timeline_damage_dice_sides"] = Math.Max(TimelineDamageDiceSides, 0),
            ["timeline_damage_flat_bonus"] = Math.Max(TimelineDamageFlatBonus, 0),
            ["damage_tag"] = DamageTag.ToString(),
        };

    internal static bool TryFromDictionary(
        GDictionary payload,
        out BattleStatusSourceContributionState contribution
    )
    {
        contribution = null;
        if (payload == null || payload.Count != SchemaFields.Count)
            return false;
        foreach (Variant rawKey in payload.Keys)
        {
            if (
                rawKey.VariantType is not Variant.Type.String and not Variant.Type.StringName
                || !SchemaFields.Contains(rawKey.ToString())
            )
            {
                return false;
            }
        }
        if (
            !TryReadStringName(payload, "source_kind", out StringName sourceKindId)
            || !TryReadStringName(payload, "source_unit_id", out StringName sourceUnitId)
            || !TryReadStringName(payload, "source_definition_id", out StringName sourceDefinitionId)
            || !TryReadInt(payload, "power", out int power)
            || !TryReadInt(payload, "stacks", out int stacks)
            || !TryReadInt(payload, "duration_tu", out int durationTu)
            || !TryReadInt(payload, "tick_interval_tu", out int tickIntervalTu)
            || !TryReadInt(payload, "next_tick_at_tu", out int nextTickAtTu)
            || !TryReadInt(payload, "timeline_damage_dice_count", out int diceCount)
            || !TryReadInt(payload, "timeline_damage_dice_sides", out int diceSides)
            || !TryReadInt(payload, "timeline_damage_flat_bonus", out int flatBonus)
            || !TryReadStringName(payload, "damage_tag", out StringName damageTag)
        )
        {
            return false;
        }
        BattleStatusSourceKind kind = BattleStatusSourceKindRules.FromStringName(sourceKindId);
        var identity = new BattleStatusSourceIdentity(kind, sourceUnitId, sourceDefinitionId);
        if (
            !identity.IsValid
            || power < 0
            || stacks <= 0
            || durationTu < -1
            || tickIntervalTu < 0
            || nextTickAtTu < 0
            || diceCount < 0
            || diceSides < 0
            || flatBonus < 0
            || (diceCount == 0) != (diceSides == 0)
            || (nextTickAtTu > 0 && tickIntervalTu <= 0)
        )
        {
            return false;
        }
        contribution = new BattleStatusSourceContributionState
        {
            Identity = identity,
            Power = power,
            Stacks = stacks,
            DurationTu = durationTu,
            TickIntervalTu = tickIntervalTu,
            NextTickAtTu = nextTickAtTu,
            TimelineDamageDiceCount = diceCount,
            TimelineDamageDiceSides = diceSides,
            TimelineDamageFlatBonus = flatBonus,
            DamageTag = damageTag,
        };
        return true;
    }

    private static bool TryReadStringName(
        GDictionary payload,
        string key,
        out StringName value
    )
    {
        value = "";
        if (!payload.ContainsKey(key))
            return false;
        Variant raw = payload[key];
        if (raw.VariantType is not Variant.Type.String and not Variant.Type.StringName)
            return false;
        value = ProgressionDataUtils.to_string_name(raw);
        return true;
    }

    private static bool TryReadInt(GDictionary payload, string key, out int value)
    {
        value = 0;
        if (!payload.ContainsKey(key) || payload[key].VariantType != Variant.Type.Int)
            return false;
        value = payload[key].AsInt32();
        return true;
    }
}
