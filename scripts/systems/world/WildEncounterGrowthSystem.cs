using Godot;
using System.Collections.Generic;

public sealed class WildEncounterGrowthSystem
{
    public bool ApplyStepAdvance(
        IEnumerable<EncounterAnchorData> encounterAnchors,
        int old_step,
        int new_step,
        IReadOnlyDictionary<StringName, WildEncounterRosterDef> encounterRosters
    )
    {
        if (encounterAnchors == null || encounterRosters == null || encounterRosters.Count == 0)
        {
            return false;
        }
        if (new_step <= old_step)
        {
            return false;
        }

        var changed = false;
        foreach (EncounterAnchorData encounter in encounterAnchors)
        {
            if (
                encounter == null
                || encounter.encounter_kind != EncounterAnchorData.ENCOUNTER_KIND_SETTLEMENT()
            )
            {
                continue;
            }

            var roster = GetRoster(encounterRosters, encounter.encounter_profile_id);
            if (roster == null)
            {
                continue;
            }

            var interval = Mathf.Max(roster.growth_step_interval, 1);
            var relativeOldStep = Mathf.Max(old_step - encounter.suppressed_until_step, 0);
            var relativeNewStep = Mathf.Max(new_step - encounter.suppressed_until_step, 0);
            var oldCycles = relativeOldStep / interval;
            var newCycles = relativeNewStep / interval;
            var stageGain = Mathf.Max(newCycles - oldCycles, 0);
            if (stageGain <= 0)
            {
                continue;
            }

            var maxStage = roster.get_max_stage();
            var nextStage = Mathf.Min(encounter.growth_stage + stageGain, maxStage);
            if (nextStage == encounter.growth_stage)
            {
                continue;
            }
            encounter.growth_stage = nextStage;
            changed = true;
        }
        return changed;
    }

    public bool ApplyBattleVictory(
        EncounterAnchorData encounter_anchor,
        int world_step,
        IReadOnlyDictionary<StringName, WildEncounterRosterDef> encounterRosters
    )
    {
        if (
            encounter_anchor == null
            || encounter_anchor.encounter_kind != EncounterAnchorData.ENCOUNTER_KIND_SETTLEMENT()
        )
        {
            return false;
        }
        if (encounterRosters == null || encounterRosters.Count == 0)
        {
            return false;
        }

        var roster = GetRoster(encounterRosters, encounter_anchor.encounter_profile_id);
        if (roster == null)
        {
            return false;
        }

        var initialStage = Mathf.Max(roster.initial_stage, 0);
        encounter_anchor.growth_stage = Mathf.Max(encounter_anchor.growth_stage - 1, initialStage);
        encounter_anchor.suppressed_until_step = Mathf.Max(
            encounter_anchor.suppressed_until_step,
            Mathf.Max(world_step, 0)
                + Mathf.Max(roster.suppression_steps_on_victory, 0)
        );
        return true;
    }

    private static WildEncounterRosterDef GetRoster(
        IReadOnlyDictionary<StringName, WildEncounterRosterDef> encounterRosters,
        StringName encounterProfileId
    )
    {
        if (encounterProfileId == "" || !encounterRosters.ContainsKey(encounterProfileId))
        {
            return null;
        }
        return encounterRosters[encounterProfileId];
    }
}
