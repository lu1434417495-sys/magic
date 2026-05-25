using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class BattleSkillOutcomeCommitter : RefCounted
{
    private static readonly StringName Empty = new("");
    private GodotObject _runtime;

    public void setup(GodotObject runtime)
    {
        _runtime = runtime;
    }

    public void dispose()
    {
        _runtime = null;
    }

    public bool commit_common_outcome(BattleCommonSkillOutcome outcome, BattleEventBatch batch)
    {
        if (_runtime == null || outcome == null || batch == null)
        {
            return false;
        }

        foreach (StringName unitId in outcome.changed_unit_ids)
        {
            _runtime.Call("append_changed_unit_id", batch, unitId);
        }
        foreach (Vector2I coord in outcome.changed_coords)
        {
            _runtime.Call("append_changed_coord", batch, coord);
        }
        foreach (string message in outcome.log_lines)
        {
            _runtime.Call("append_batch_log", batch, message);
        }
        foreach (GDictionary reportEntry in outcome.report_entries)
        {
            if (reportEntry == null || reportEntry.Count == 0)
            {
                continue;
            }
            _runtime.Call("_append_report_entry_to_batch", batch, reportEntry);
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

    private void CommitTargetContributions(BattleCommonSkillOutcome outcome, BattleUnitState sourceUnit)
    {
        if (sourceUnit == null)
        {
            return;
        }

        if (outcome.target_results == null || outcome.target_results.Count == 0)
        {
            if (outcome.total_damage > 0 || outcome.total_healing > 0 || outcome.defeated_unit_ids.Count > 0)
            {
                GD.PushError("BattleSkillOutcomeCommitter missing target_results for contribution rating.");
            }
            return;
        }

        foreach (GDictionary result in outcome.target_results)
        {
            if (result == null)
            {
                continue;
            }

            BattleUnitState targetUnit = GetUnit(GdInterop.GetStringName(result, "target_unit_id", Empty));
            if (targetUnit == null)
            {
                continue;
            }

            _runtime.Call(
                "record_battle_contribution_result",
                sourceUnit,
                targetUnit,
                GdInterop.GetInt(result, "damage", 0),
                GdInterop.GetInt(result, "healing", 0),
                GdInterop.GetBool(result, "defeated", false),
                new StringName("special"),
                outcome.skill_id);
        }
    }

    private void CommitStatusTurnTiming(BattleCommonSkillOutcome outcome)
    {
        if (outcome.status_effect_ids_by_unit_id == null)
        {
            return;
        }

        foreach (Variant unitIdVariant in outcome.status_effect_ids_by_unit_id.Keys)
        {
            StringName unitId = GdInterop.ToStringName(unitIdVariant, Empty);
            BattleUnitState unitState = GetUnit(unitId);
            if (unitState == null)
            {
                continue;
            }

            Godot.Collections.Array<StringName> statusIds = new();
            GArray rawStatusIds = GdInterop.GetArray(outcome.status_effect_ids_by_unit_id, unitIdVariant);
            foreach (Variant statusVariant in rawStatusIds)
            {
                StringName statusId = GdInterop.ToStringName(statusVariant, Empty);
                if (!GdInterop.IsEmpty(statusId) && !statusIds.Contains(statusId))
                {
                    statusIds.Add(statusId);
                }
            }

            _runtime.Call("mark_applied_statuses_for_turn_timing", unitState, statusIds);
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
            _runtime.Call(
                "handle_unit_defeated_by_runtime_effect",
                defeatedUnit,
                sourceUnit,
                batch,
                $"{defeatedUnit.display_name} 被击倒。",
                new GDictionary { ["record_enemy_defeated_achievement"] = true });
        }
        return defeatedCount;
    }

    private BattleUnitState GetUnit(StringName unitId)
    {
        if (_runtime == null || GdInterop.IsEmpty(unitId))
        {
            return null;
        }

        GodotObject state = _runtime.Call("get_state").AsGodotObject();
        if (state == null)
        {
            return null;
        }
        return GdInterop.GetObject(GdInterop.GetDictionary(state, "units"), unitId) as BattleUnitState;
    }
}
