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
        StringName sourceUnitId = GdInterop.GetStringName(payload, "source_unit_id");
        StringName targetUnitId = GdInterop.GetStringName(payload, "target_unit_id");
        StringName sourceFactionId = GdInterop.GetStringName(payload, "source_faction_id");
        StringName targetFactionId = GdInterop.GetStringName(payload, "target_faction_id");
        StringName relationName = GdInterop.GetStringName(payload, "relation");
        return new BattleContributionEvent
        {
            source_unit_id = sourceUnitId,
            source_member_id = GdInterop.GetStringName(payload, "source_member_id"),
            source_faction_id = sourceFactionId,
            target_unit_id = targetUnitId,
            target_faction_id = targetFactionId,
            skill_id = GdInterop.GetStringName(payload, "skill_id"),
            relation = ParseRelation(
                relationName,
                sourceUnitId,
                sourceFactionId,
                targetUnitId,
                targetFactionId
            ),
            origin_kind = ParseOriginKind(GdInterop.GetStringName(payload, "origin_kind")),
            hp_damage_applied = Math.Max(GdInterop.GetInt(payload, "hp_damage_applied", 0), 0),
            hp_healing_applied = Math.Max(GdInterop.GetInt(payload, "hp_healing_applied", 0), 0),
            caused_defeat = GdInterop.GetBool(payload, "caused_defeat", false),
        };
    }

    private static BattleContributionRelation ResolveRelation(
        StringName sourceUnitId,
        StringName sourceFactionId,
        StringName targetUnitId,
        StringName targetFactionId
    )
    {
        if (!GdInterop.IsEmpty(sourceUnitId) && sourceUnitId == targetUnitId)
        {
            return BattleContributionRelation.Self;
        }
        if (GdInterop.IsEmpty(sourceFactionId) || GdInterop.IsEmpty(targetFactionId))
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
}
