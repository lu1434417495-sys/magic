using Godot;

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

internal sealed class BattleContributionEvent
{
    public StringName SourceUnitId { get; init; } = "";
    public StringName SourceMemberId { get; init; } = "";
    public StringName SourceFactionId { get; init; } = "";
    public StringName TargetUnitId { get; init; } = "";
    public StringName TargetFactionId { get; init; } = "";
    public StringName SkillId { get; init; } = "";
    public BattleContributionRelation Relation { get; init; }
    public BattleContributionOriginKind OriginKind { get; init; }
    public int HpDamageApplied { get; init; }
    public int HpHealingApplied { get; init; }
    public bool CausedDefeat { get; init; }

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
