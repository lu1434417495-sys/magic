using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class WildEncounterRosterDef : Resource
{
    [Export]
    public StringName profile_id { get; set; } = "";

    [Export]
    public string display_name { get; set; } = "";

    [Export]
    public int initial_stage { get; set; }

    [Export]
    public int growth_step_interval { get; set; } = 1;

    [Export]
    public int suppression_steps_on_victory { get; set; }

    [Export]
    public Godot.Collections.Array<WildEncounterRosterStageDef> stages { get; set; } = new();

    internal int GetMaxStage()
    {
        int maxStage = -1;
        foreach (WildEncounterRosterStageDef stageDef in stages)
        {
            if (stageDef != null)
            {
                maxStage = Mathf.Max(maxStage, stageDef.stage);
            }
        }
        return maxStage;
    }

    internal WildEncounterRosterDefinition ToDefinition()
    {
        var stageDefinitions = new List<WildEncounterRosterStageDefinition>();
        foreach (WildEncounterRosterStageDef stageDef in stages)
        {
            if (stageDef != null)
                stageDefinitions.Add(stageDef.ToDefinition());
        }
        return new WildEncounterRosterDefinition(
            profile_id,
            display_name,
            initial_stage,
            growth_step_interval,
            suppression_steps_on_victory,
            stageDefinitions
        );
    }

    internal IReadOnlyList<WildEncounterRosterUnitEntryDef> GetStageUnitEntriesTyped(int stage)
    {
        int bestStage = -1;
        IReadOnlyList<WildEncounterRosterUnitEntryDef> bestEntries =
            new List<WildEncounterRosterUnitEntryDef>();
        foreach (WildEncounterRosterStageDef stageDef in stages)
        {
            if (stageDef == null || stageDef.stage > stage || stageDef.stage < bestStage)
            {
                continue;
            }
            bestStage = stageDef.stage;
            bestEntries = stageDef.unit_entries ?? new Godot.Collections.Array<WildEncounterRosterUnitEntryDef>();
        }
        return bestEntries;
    }

    internal Godot.Collections.Array<string> ValidateSchemaTyped(
        IReadOnlySet<StringName> knownTemplateIds
    )
    {
        var errors = new Godot.Collections.Array<string>();

        if (profile_id == "")
        {
            errors.Add("Wild encounter roster is missing profile_id.");
            return errors;
        }

        if (display_name.StripEdges().Length == 0)
        {
            errors.Add($"Wild encounter roster {profile_id} is missing display_name.");
        }
        if (initial_stage < 0)
        {
            errors.Add($"Wild encounter roster {profile_id} initial_stage must be >= 0.");
        }
        if (growth_step_interval <= 0)
        {
            errors.Add($"Wild encounter roster {profile_id} growth_step_interval must be >= 1.");
        }
        if (suppression_steps_on_victory < 0)
        {
            errors.Add(
                $"Wild encounter roster {profile_id} suppression_steps_on_victory must be >= 0."
            );
        }
        if (stages.Count == 0)
        {
            errors.Add($"Wild encounter roster {profile_id} must declare at least one stage.");
            return errors;
        }

        var seenStageIds = new HashSet<int>();
        int maxDeclaredStage = -1;
        foreach (WildEncounterRosterStageDef stageDef in stages)
        {
            if (stageDef == null)
            {
                errors.Add($"Wild encounter roster {profile_id} contains a non-stage resource.");
                continue;
            }
            int stageIndex = stageDef.stage;
            if (stageIndex < 0)
            {
                errors.Add($"Wild encounter roster {profile_id} declares an invalid stage index.");
            }
            else if (!seenStageIds.Add(stageIndex))
            {
                errors.Add(
                    $"Wild encounter roster {profile_id} declares duplicate stage {stageIndex}."
                );
            }

            maxDeclaredStage = Mathf.Max(maxDeclaredStage, stageIndex);
            if (stageDef.unit_entries == null || stageDef.unit_entries.Count == 0)
            {
                errors.Add(
                    $"Wild encounter roster {profile_id} stage {stageIndex} must declare at least one unit entry."
                );
                continue;
            }

            foreach (WildEncounterRosterUnitEntryDef unitEntry in stageDef.unit_entries)
            {
                if (unitEntry == null)
                {
                    errors.Add(
                        $"Wild encounter roster {profile_id} stage {stageIndex} contains a non-unit-entry resource."
                    );
                    continue;
                }

                StringName templateId = unitEntry.template_id;
                if (templateId == "")
                {
                    errors.Add(
                        $"Wild encounter roster {profile_id} stage {stageIndex} contains a unit entry without template_id."
                    );
                }
                else if (knownTemplateIds != null && !knownTemplateIds.Contains(templateId))
                {
                    errors.Add(
                        $"Wild encounter roster {profile_id} stage {stageIndex} references missing template {templateId}."
                    );
                }

                if (unitEntry.count <= 0)
                {
                    errors.Add(
                        $"Wild encounter roster {profile_id} stage {stageIndex} template {templateId} must have count >= 1."
                    );
                }
            }
        }

        if (!seenStageIds.Contains(initial_stage))
        {
            errors.Add(
                $"Wild encounter roster {profile_id} initial_stage {initial_stage} does not match any declared stage (max declared: {maxDeclaredStage})."
            );
        }
        return errors;
    }

}
