using Godot;
using GDictionary = Godot.Collections.Dictionary;

public enum BattleContributionRelation
{
    Unknown = 0,
    Self = 1,
    Ally = 2,
    Enemy = 3,
    Neutral = 4,
}

public enum BattleContributionOriginKind
{
    Unknown = 0,
    Skill = 1,
    Chain = 2,
    Repeat = 3,
    Ground = 4,
    Terrain = 5,
    Charge = 6,
    Special = 7,
}

public sealed class BattleContributionEvent
{
    public StringName source_unit_id { get; init; } = "";
    public StringName source_member_id { get; init; } = "";
    public StringName source_faction_id { get; init; } = "";
    public StringName target_unit_id { get; init; } = "";
    public StringName target_faction_id { get; init; } = "";
    public StringName skill_id { get; init; } = "";
    public BattleContributionRelation relation { get; init; }
    public BattleContributionOriginKind origin_kind { get; init; }
    public int hp_damage_applied { get; init; }
    public int hp_healing_applied { get; init; }
    public bool caused_defeat { get; init; }

    public GDictionary to_dictionary()
    {
        return new GDictionary
        {
            ["source_unit_id"] = source_unit_id,
            ["source_member_id"] = source_member_id,
            ["source_faction_id"] = source_faction_id,
            ["target_unit_id"] = target_unit_id,
            ["target_faction_id"] = target_faction_id,
            ["skill_id"] = skill_id,
            ["relation"] = RelationToString(relation),
            ["origin_kind"] = OriginKindToString(origin_kind),
            ["hp_damage_applied"] = hp_damage_applied,
            ["hp_healing_applied"] = hp_healing_applied,
            ["caused_defeat"] = caused_defeat,
        };
    }

    public static StringName RelationToString(BattleContributionRelation relation)
    {
        return relation switch
        {
            BattleContributionRelation.Self => "self",
            BattleContributionRelation.Ally => "ally",
            BattleContributionRelation.Enemy => "enemy",
            BattleContributionRelation.Neutral => "neutral",
            _ => "unknown",
        };
    }

    public static StringName OriginKindToString(BattleContributionOriginKind originKind)
    {
        return originKind switch
        {
            BattleContributionOriginKind.Skill => "skill",
            BattleContributionOriginKind.Chain => "chain",
            BattleContributionOriginKind.Repeat => "repeat",
            BattleContributionOriginKind.Ground => "ground",
            BattleContributionOriginKind.Terrain => "terrain",
            BattleContributionOriginKind.Charge => "charge",
            BattleContributionOriginKind.Special => "special",
            _ => "unknown",
        };
    }
}
