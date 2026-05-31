using System;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public static class BattleContributionEventBuilder
{
    public static BattleContributionEvent FromUnits(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        int damage,
        int healing,
        bool causedDefeat,
        StringName originKind,
        StringName skillId
    )
    {
        StringName sourceUnitId = sourceUnit?.unit_id ?? "";
        StringName targetUnitId = targetUnit?.unit_id ?? "";
        StringName sourceFactionId = sourceUnit?.faction_id ?? "";
        StringName targetFactionId = targetUnit?.faction_id ?? "";
        return new BattleContributionEvent
        {
            source_unit_id = sourceUnitId,
            source_member_id = sourceUnit?.source_member_id ?? "",
            source_faction_id = sourceFactionId,
            target_unit_id = targetUnitId,
            target_faction_id = targetFactionId,
            skill_id = skillId,
            relation = ResolveRelation(
                sourceUnitId,
                sourceFactionId,
                targetUnitId,
                targetFactionId
            ),
            origin_kind = ParseOriginKind(originKind),
            hp_damage_applied = Math.Max(damage, 0),
            hp_healing_applied = Math.Max(healing, 0),
            caused_defeat = causedDefeat,
        };
    }

    public static BattleContributionEvent FromDictionary(GDictionary payload)
    {
        StringName sourceUnitId = ReadStringName(payload, "source_unit_id");
        StringName targetUnitId = ReadStringName(payload, "target_unit_id");
        StringName sourceFactionId = ReadStringName(payload, "source_faction_id");
        StringName targetFactionId = ReadStringName(payload, "target_faction_id");
        StringName relationName = ReadStringName(payload, "relation");
        return new BattleContributionEvent
        {
            source_unit_id = sourceUnitId,
            source_member_id = ReadStringName(payload, "source_member_id"),
            source_faction_id = sourceFactionId,
            target_unit_id = targetUnitId,
            target_faction_id = targetFactionId,
            skill_id = ReadStringName(payload, "skill_id"),
            relation = ParseRelation(
                relationName,
                sourceUnitId,
                sourceFactionId,
                targetUnitId,
                targetFactionId
            ),
            origin_kind = ParseOriginKind(ReadStringName(payload, "origin_kind")),
            hp_damage_applied = Math.Max(ReadInt(payload, "hp_damage_applied"), 0),
            hp_healing_applied = Math.Max(ReadInt(payload, "hp_healing_applied"), 0),
            caused_defeat = ReadBool(payload, "caused_defeat"),
        };
    }

    private static BattleContributionRelation ResolveRelation(
        StringName sourceUnitId,
        StringName sourceFactionId,
        StringName targetUnitId,
        StringName targetFactionId
    )
    {
        if (!IsEmpty(sourceUnitId) && sourceUnitId == targetUnitId)
        {
            return BattleContributionRelation.Self;
        }
        if (IsEmpty(sourceFactionId) || IsEmpty(targetFactionId))
        {
            return BattleContributionRelation.Unknown;
        }
        return sourceFactionId == targetFactionId
            ? BattleContributionRelation.Ally
            : BattleContributionRelation.Enemy;
    }

    private static BattleContributionRelation ParseRelation(
        StringName relationName,
        StringName sourceUnitId,
        StringName sourceFactionId,
        StringName targetUnitId,
        StringName targetFactionId
    )
    {
        string text = relationName.ToString();
        return text switch
        {
            "self" => BattleContributionRelation.Self,
            "ally" => BattleContributionRelation.Ally,
            "enemy" => BattleContributionRelation.Enemy,
            "neutral" => BattleContributionRelation.Neutral,
            _ => ResolveRelation(sourceUnitId, sourceFactionId, targetUnitId, targetFactionId),
        };
    }

    private static BattleContributionOriginKind ParseOriginKind(StringName originKind)
    {
        string text = originKind.ToString();
        return text switch
        {
            "skill" => BattleContributionOriginKind.Skill,
            "chain" => BattleContributionOriginKind.Chain,
            "repeat" => BattleContributionOriginKind.Repeat,
            "ground" => BattleContributionOriginKind.Ground,
            "terrain" => BattleContributionOriginKind.Terrain,
            "charge" => BattleContributionOriginKind.Charge,
            "special" => BattleContributionOriginKind.Special,
            _ => BattleContributionOriginKind.Unknown,
        };
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || value == "";
    }

    private static StringName ReadStringName(
        GDictionary data,
        string key,
        StringName fallback = default
    )
    {
        var value = ReadValue(data, key);
        if (value.VariantType == Variant.Type.StringName)
            return value.AsStringName();
        if (value.VariantType == Variant.Type.String)
            return new StringName(value.AsString());
        return fallback ?? new StringName("");
    }

    private static int ReadInt(GDictionary data, string key, int fallback = 0)
    {
        var value = ReadValue(data, key);
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static bool ReadBool(GDictionary data, string key, bool fallback = false)
    {
        var value = ReadValue(data, key);
        return value.VariantType == Variant.Type.Bool ? value.AsBool() : fallback;
    }

    private static Variant ReadValue(GDictionary data, string key)
    {
        if (data == null)
            return default;
        if (data.ContainsKey(key))
            return data[key];
        var stringNameKey = new StringName(key);
        if (data.ContainsKey(stringNameKey))
            return data[stringNameKey];
        return default;
    }
}
