using System;
using Godot;

internal static class BattleContributionEventBuilder
{
    internal static BattleContributionEvent FromUnits(
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
            SourceUnitId = sourceUnitId,
            SourceMemberId = sourceUnit?.source_member_id ?? "",
            SourceFactionId = sourceFactionId,
            TargetUnitId = targetUnitId,
            TargetFactionId = targetFactionId,
            SkillId = skillId,
            Relation = ResolveRelation(
                sourceUnitId,
                sourceFactionId,
                targetUnitId,
                targetFactionId
            ),
            OriginKind = ParseOriginKind(originKind),
            HpDamageApplied = Math.Max(damage, 0),
            HpHealingApplied = Math.Max(healing, 0),
            CausedDefeat = causedDefeat,
        };
    }

    internal static BattleContributionEvent FromDictionary(Godot.Collections.Dictionary payload)
    {
        StringName sourceUnitId = ReadStringName(payload, "source_unit_id");
        StringName targetUnitId = ReadStringName(payload, "target_unit_id");
        StringName sourceFactionId = ReadStringName(payload, "source_faction_id");
        StringName targetFactionId = ReadStringName(payload, "target_faction_id");
        StringName relationName = ReadStringName(payload, "relation");
        return new BattleContributionEvent
        {
            SourceUnitId = sourceUnitId,
            SourceMemberId = ReadStringName(payload, "source_member_id"),
            SourceFactionId = sourceFactionId,
            TargetUnitId = targetUnitId,
            TargetFactionId = targetFactionId,
            SkillId = ReadStringName(payload, "skill_id"),
            Relation = ParseRelation(
                relationName,
                sourceUnitId,
                sourceFactionId,
                targetUnitId,
                targetFactionId
            ),
            OriginKind = ParseOriginKind(ReadStringName(payload, "origin_kind")),
            HpDamageApplied = Math.Max(ReadInt(payload, "hp_damage_applied"), 0),
            HpHealingApplied = Math.Max(ReadInt(payload, "hp_healing_applied"), 0),
            CausedDefeat = ReadBool(payload, "caused_defeat"),
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
        Godot.Collections.Dictionary data,
        string key,
        StringName fallback = default
    )
    {
        if (!HasKey(data, key))
            return fallback ?? new StringName("");
        return ProgressionDataUtils.to_string_name(data[key]);
    }

    private static int ReadInt(Godot.Collections.Dictionary data, string key, int fallback = 0)
    {
        if (!HasKey(data, key))
            return fallback;
        return data[key].AsInt32();
    }

    private static bool ReadBool(Godot.Collections.Dictionary data, string key, bool fallback = false)
    {
        if (!HasKey(data, key))
            return fallback;
        return data[key].AsBool();
    }

    private static bool HasKey(Godot.Collections.Dictionary data, string key)
    {
        if (data == null)
            return false;
        return data.ContainsKey(key);
    }
}
