using Godot;

internal static class BattleContributionEventProjection
{
    internal static Godot.Collections.Dictionary Project(BattleContributionEvent contributionEvent)
    {
        if (contributionEvent == null)
            return new Godot.Collections.Dictionary();
        return new Godot.Collections.Dictionary
        {
            ["source_unit_id"] = contributionEvent.SourceUnitId,
            ["source_member_id"] = contributionEvent.SourceMemberId,
            ["source_faction_id"] = contributionEvent.SourceFactionId,
            ["target_unit_id"] = contributionEvent.TargetUnitId,
            ["target_faction_id"] = contributionEvent.TargetFactionId,
            ["skill_id"] = contributionEvent.SkillId,
            ["relation"] = BattleContributionEvent.RelationToString(
                contributionEvent.Relation
            ),
            ["origin_kind"] = BattleContributionEvent.OriginKindToString(
                contributionEvent.OriginKind
            ),
            ["hp_damage_applied"] = contributionEvent.HpDamageApplied,
            ["hp_healing_applied"] = contributionEvent.HpHealingApplied,
            ["caused_defeat"] = contributionEvent.CausedDefeat,
        };
    }
}
