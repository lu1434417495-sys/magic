using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class WildEncounterGrowthSystem : RefCounted
{
    public bool apply_step_advance(
        GDictionary world_data,
        int old_step,
        int new_step,
        GDictionary encounter_rosters
    )
    {
        if (world_data == null || encounter_rosters == null || encounter_rosters.Count == 0)
        {
            return false;
        }
        if (new_step <= old_step)
        {
            return false;
        }

        var changed = false;
        var encounterAnchors = GetEncounterAnchors(world_data);
        foreach (var encounterValue in encounterAnchors)
        {
            var encounter = encounterValue.AsGodotObject() as EncounterAnchorData;
            if (
                encounter == null
                || encounter.encounter_kind != EncounterAnchorData.ENCOUNTER_KIND_SETTLEMENT()
            )
            {
                continue;
            }

            var roster = GetRoster(encounter_rosters, encounter.encounter_profile_id);
            if (roster == null)
            {
                continue;
            }

            var interval = Mathf.Max(roster.Get("growth_step_interval").AsInt32(), 1);
            var relativeOldStep = Mathf.Max(old_step - encounter.suppressed_until_step, 0);
            var relativeNewStep = Mathf.Max(new_step - encounter.suppressed_until_step, 0);
            var oldCycles = relativeOldStep / interval;
            var newCycles = relativeNewStep / interval;
            var stageGain = Mathf.Max(newCycles - oldCycles, 0);
            if (stageGain <= 0)
            {
                continue;
            }

            var maxStage = encounter.growth_stage;
            if (roster is WildEncounterRosterDef rosterDef)
            {
                maxStage = rosterDef.get_max_stage();
            }
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

    public bool apply_battle_victory(
        EncounterAnchorData encounter_anchor,
        int world_step,
        GDictionary encounter_rosters
    )
    {
        if (
            encounter_anchor == null
            || encounter_anchor.encounter_kind != EncounterAnchorData.ENCOUNTER_KIND_SETTLEMENT()
        )
        {
            return false;
        }
        if (encounter_rosters == null || encounter_rosters.Count == 0)
        {
            return false;
        }

        var roster = GetRoster(encounter_rosters, encounter_anchor.encounter_profile_id);
        if (roster == null)
        {
            return false;
        }

        var initialStage = Mathf.Max(roster.Get("initial_stage").AsInt32(), 0);
        encounter_anchor.growth_stage = Mathf.Max(encounter_anchor.growth_stage - 1, initialStage);
        encounter_anchor.suppressed_until_step = Mathf.Max(
            encounter_anchor.suppressed_until_step,
            Mathf.Max(world_step, 0)
                + Mathf.Max(roster.Get("suppression_steps_on_victory").AsInt32(), 0)
        );
        return true;
    }

    private static GArray GetEncounterAnchors(GDictionary worldData)
    {
        if (!worldData.ContainsKey("encounter_anchors"))
        {
            return new GArray();
        }
        var anchors = worldData["encounter_anchors"];
        return anchors.VariantType == Variant.Type.Array ? anchors.AsGodotArray() : new GArray();
    }

    private static GodotObject GetRoster(
        GDictionary encounterRosters,
        StringName encounterProfileId
    )
    {
        if (!encounterRosters.ContainsKey(encounterProfileId))
        {
            return null;
        }
        return encounterRosters[encounterProfileId].AsGodotObject();
    }
}
