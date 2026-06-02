using Godot;

public sealed class BattleSpecialProfileCommitAdapter
{
    private BattleSkillOutcomeCommitter _committer;

    public void Setup(BattleSkillOutcomeCommitter committer)
    {
        _committer = committer;
    }

    public void Dispose()
    {
        _committer = null;
    }

    public bool CommitMeteorSwarmResult(MeteorSwarmCommitResult result, BattleEventBatch batch)
    {
        if (result == null || batch == null)
        {
            return false;
        }
        if (_committer == null)
        {
            return false;
        }

        BattleCommonSkillOutcome outcome = BuildCommonOutcomeFromMeteorResult(result);
        return _committer.commit_common_outcome(outcome, batch);
    }

    private static BattleCommonSkillOutcome BuildCommonOutcomeFromMeteorResult(
        MeteorSwarmCommitResult result
    )
    {
        BattleCommonSkillOutcome outcome = new();
        if (result.plan != null)
        {
            outcome.source_unit_id = result.plan.source_unit_id;
            outcome.skill_id = result.plan.skill_id;
        }

        outcome.total_damage = result.total_damage;
        outcome.total_healing = result.total_healing;
        foreach (StringName unitId in result.changed_unit_ids)
        {
            outcome.add_changed_unit_id(unitId);
        }
        foreach (Vector2I coord in result.changed_coords)
        {
            outcome.add_changed_coord(coord);
        }
        foreach (StringName defeatedUnitId in result.defeated_unit_ids)
        {
            outcome.add_defeated_unit_id(defeatedUnitId);
        }
        foreach (MeteorSwarmTargetOutcome targetOutcome in result.target_outcomes)
        {
            if (targetOutcome == null)
            {
                continue;
            }

            outcome.add_changed_unit_id(targetOutcome.target_unit_id);
            outcome.add_target_result(
                targetOutcome.target_unit_id,
                targetOutcome.total_damage,
                targetOutcome.total_healing,
                targetOutcome.defeated
            );
            outcome.add_status_effect_ids(
                targetOutcome.target_unit_id,
                targetOutcome.status_effect_ids
            );
            if (targetOutcome.defeated)
            {
                outcome.add_defeated_unit_id(targetOutcome.target_unit_id);
            }
        }
        foreach (string message in result.log_lines)
        {
            outcome.log_lines.Add(message);
        }
        foreach (var reportEntryValue in result.report_entries)
        {
            outcome.report_entries.Add(reportEntryValue.AsGodotDictionary().Duplicate(true));
        }
        return outcome;
    }
}
