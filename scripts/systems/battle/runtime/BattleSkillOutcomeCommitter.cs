using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

internal class BattleSkillOutcomeCommitter
{
    private BattleRuntimeModule _runtime;

    internal void Setup(BattleRuntimeModule runtime)
    {
        _runtime = runtime;
    }

    internal void Dispose()
    {
        _runtime = null;
    }

    internal bool CommitCommonOutcome(BattleCommonSkillOutcome outcome, BattleEventBatch batch)
    {
        if (_runtime == null || outcome == null || batch == null)
        {
            return false;
        }

        foreach (StringName unitId in outcome.changed_unit_ids)
        {
            _runtime.AppendChangedUnitId(batch, unitId);
        }
        foreach (Vector2I coord in outcome.changed_coords)
        {
            _runtime.AppendChangedCoord(batch, coord);
        }
        foreach (string message in outcome.log_lines)
        {
            _runtime.AppendBatchLog(batch, message);
        }
        foreach (GDictionary reportEntry in outcome.report_entries)
        {
            if (reportEntry == null || reportEntry.Count == 0)
            {
                continue;
            }
            _runtime.AppendReportEntry(batch, reportEntry);
        }

        CommitStatusTurnTiming(outcome);
        CommitDefeatedUnits(outcome, batch);
        BattleUnitState sourceUnit = GetUnit(outcome.source_unit_id);
        if (sourceUnit != null)
        {
            CommitTargetContributions(outcome, sourceUnit);
        }
        return true;
    }

    internal bool CommitMeteorSwarmResult(MeteorSwarmCommitResult result, BattleEventBatch batch)
    {
        if (_runtime == null || result == null || batch == null)
        {
            return false;
        }

        return CommitCommonOutcome(BuildCommonOutcomeFromMeteorResult(result), batch);
    }

    private void CommitTargetContributions(
        BattleCommonSkillOutcome outcome,
        BattleUnitState sourceUnit
    )
    {
        if (sourceUnit == null)
        {
            return;
        }

        if (outcome.target_results == null || outcome.target_results.Count == 0)
        {
            if (
                outcome.total_damage > 0
                || outcome.total_healing > 0
                || outcome.defeated_unit_ids.Count > 0
            )
            {
                GameLog.Error(
                    "BattleSkillOutcomeCommitter missing target_results for contribution rating.",
                    "battle.commit.missing_target_results",
                    "battle"
                );
            }
            return;
        }

        foreach (BattleCommonSkillTargetResult result in outcome.target_results)
        {
            BattleUnitState targetUnit = GetUnit(result.TargetUnitId);
            if (targetUnit == null)
            {
                continue;
            }

            _runtime.RecordBattleContributionResult(
                sourceUnit,
                targetUnit,
                result.Damage,
                result.Healing,
                result.Defeated,
                new StringName("special"),
                outcome.skill_id
            );
        }
    }

    private void CommitStatusTurnTiming(BattleCommonSkillOutcome outcome)
    {
        if (outcome.status_effect_ids_by_unit_id == null)
        {
            return;
        }

        foreach (var entry in outcome.status_effect_ids_by_unit_id)
        {
            StringName unitId = entry.Key;
            BattleUnitState unitState = GetUnit(unitId);
            if (unitState == null)
            {
                continue;
            }

            GArray statusIds = new();
            foreach (StringName statusId in entry.Value)
            {
                if (!IsEmpty(statusId) && !statusIds.Contains(statusId))
                {
                    statusIds.Add(statusId);
                }
            }

            _runtime.MarkAppliedStatusesForTurnTiming(unitState, statusIds);
        }
    }

    private int CommitDefeatedUnits(BattleCommonSkillOutcome outcome, BattleEventBatch batch)
    {
        BattleUnitState sourceUnit = GetUnit(outcome.source_unit_id);
        int defeatedCount = 0;
        foreach (StringName defeatedUnitId in outcome.defeated_unit_ids)
        {
            BattleUnitState defeatedUnit = GetUnit(defeatedUnitId);
            if (defeatedUnit == null)
            {
                continue;
            }

            defeatedCount += 1;
            _runtime.HandleUnitDefeatedByRuntimeEffect(
                defeatedUnit,
                sourceUnit,
                batch,
                $"{defeatedUnit.display_name} 被击倒。",
                new BattleDefeatHandlingOptions(recordEnemyDefeatedAchievement: true)
            );
        }
        return defeatedCount;
    }

    private BattleUnitState GetUnit(StringName unitId)
    {
        if (_runtime == null || IsEmpty(unitId))
        {
            return null;
        }

        BattleState state = _runtime._state;
        if (state == null || !state.TryGetUnitTyped(unitId, out BattleUnitState unitState))
        {
            return null;
        }
        return unitState;
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || value == "";
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
            outcome.AddChangedUnitId(unitId);
        }
        foreach (Vector2I coord in result.changed_coords)
        {
            outcome.AddChangedCoord(coord);
        }
        foreach (StringName defeatedUnitId in result.defeated_unit_ids)
        {
            outcome.AddDefeatedUnitId(defeatedUnitId);
        }
        foreach (MeteorSwarmTargetOutcome targetOutcome in result.target_outcomes)
        {
            if (targetOutcome == null)
            {
                continue;
            }

            outcome.AddChangedUnitId(targetOutcome.target_unit_id);
            outcome.AddTargetResult(
                targetOutcome.target_unit_id,
                targetOutcome.total_damage,
                targetOutcome.total_healing,
                targetOutcome.defeated
            );
            outcome.AddStatusEffectIds(
                targetOutcome.target_unit_id,
                targetOutcome.status_effect_ids
            );
            if (targetOutcome.defeated)
            {
                outcome.AddDefeatedUnitId(targetOutcome.target_unit_id);
            }
        }
        foreach (string message in result.log_lines)
        {
            outcome.log_lines.Add(message);
        }
        foreach (MeteorSwarmReportEntry reportEntry in result.GetReportEntriesTyped())
        {
            outcome.report_entries.Add(MeteorSwarmProjection.Project(reportEntry));
        }
        return outcome;
    }
}
