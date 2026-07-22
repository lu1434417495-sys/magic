using Godot;
using System.Collections.Generic;

public sealed class WildEncounterGrowthSystem
{
    internal bool ApplyStepAdvance(
        IEnumerable<EncounterAnchorData> encounterAnchors,
        int old_step,
        int new_step,
        IReadOnlyDictionary<StringName, BattleEncounterDefinition> battleEncounters,
        IReadOnlyDictionary<StringName, WildEncounterRosterDefinition> encounterRosters
    )
    {
        if (
            encounterAnchors == null
            || battleEncounters == null
            || battleEncounters.Count == 0
            || encounterRosters == null
            || encounterRosters.Count == 0
        )
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
                || encounter.encounter_kind != EncounterAnchorData.ToStringName(EncounterAnchorKind.Settlement)
            )
            {
                continue;
            }

            var roster = GetRoster(
                battleEncounters,
                encounterRosters,
                encounter.encounter_profile_id
            );
            if (roster == null)
            {
                continue;
            }

            var interval = Mathf.Max(roster.GrowthStepInterval, 1);
            var relativeOldStep = Mathf.Max(old_step - encounter.suppressed_until_step, 0);
            var relativeNewStep = Mathf.Max(new_step - encounter.suppressed_until_step, 0);
            var oldCycles = relativeOldStep / interval;
            var newCycles = relativeNewStep / interval;
            var stageGain = Mathf.Max(newCycles - oldCycles, 0);
            if (stageGain <= 0)
            {
                continue;
            }

            var maxStage = roster.GetMaxStage();
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

    internal bool ApplyBattleSuppression(
        EncounterAnchorData encounter_anchor,
        int world_step,
        IReadOnlyDictionary<StringName, BattleEncounterDefinition> battleEncounters,
        IReadOnlyDictionary<StringName, WildEncounterRosterDefinition> encounterRosters
    )
    {
        if (
            encounter_anchor == null
            || encounter_anchor.encounter_kind != EncounterAnchorData.ToStringName(EncounterAnchorKind.Settlement)
        )
        {
            return false;
        }
        if (
            battleEncounters == null
            || battleEncounters.Count == 0
            || encounterRosters == null
            || encounterRosters.Count == 0
        )
        {
            return false;
        }

        BattleEncounterDefinition battleEncounter = GetBattleEncounter(
            battleEncounters,
            encounter_anchor.encounter_profile_id
        );
        if (battleEncounter == null || battleEncounter.WorldResolution.SuppressionSteps <= 0)
        {
            return false;
        }
        WildEncounterRosterDefinition roster = GetRoster(
            encounterRosters,
            battleEncounter.RosterProfileId
        );
        if (roster == null)
        {
            return false;
        }

        var initialStage = Mathf.Max(roster.InitialStage, 0);
        encounter_anchor.growth_stage = Mathf.Max(encounter_anchor.growth_stage - 1, initialStage);
        encounter_anchor.suppressed_until_step = Mathf.Max(
            encounter_anchor.suppressed_until_step,
            Mathf.Max(world_step, 0)
                + Mathf.Max(battleEncounter.WorldResolution.SuppressionSteps, 0)
        );
        return true;
    }

    private static WildEncounterRosterDefinition GetRoster(
        IReadOnlyDictionary<StringName, BattleEncounterDefinition> battleEncounters,
        IReadOnlyDictionary<StringName, WildEncounterRosterDefinition> encounterRosters,
        StringName battleEncounterId
    )
    {
        BattleEncounterDefinition battleEncounter = GetBattleEncounter(
            battleEncounters,
            battleEncounterId
        );
        return battleEncounter == null
            ? null
            : GetRoster(encounterRosters, battleEncounter.RosterProfileId);
    }

    private static BattleEncounterDefinition GetBattleEncounter(
        IReadOnlyDictionary<StringName, BattleEncounterDefinition> battleEncounters,
        StringName battleEncounterId
    )
    {
        if (
            battleEncounters == null
            || battleEncounterId == ""
            || !battleEncounters.TryGetValue(
                battleEncounterId,
                out BattleEncounterDefinition battleEncounter
            )
        )
        {
            return null;
        }
        return battleEncounter;
    }

    private static WildEncounterRosterDefinition GetRoster(
        IReadOnlyDictionary<StringName, WildEncounterRosterDefinition> encounterRosters,
        StringName rosterProfileId
    )
    {
        if (
            encounterRosters == null
            || rosterProfileId == ""
            || !encounterRosters.TryGetValue(
                rosterProfileId,
                out WildEncounterRosterDefinition roster
            )
        )
        {
            return null;
        }
        return roster;
    }
}
