using Godot;

[GlobalClass]
public partial class WildEncounterRosterDef : Resource
{
    [Export] public StringName profile_id { get; set; } = "";
    [Export] public string display_name { get; set; } = "";
    [Export] public int initial_stage { get; set; }
    [Export] public int growth_step_interval { get; set; } = 1;
    [Export] public int suppression_steps_on_victory { get; set; }
    [Export] public Godot.Collections.Array<Godot.Collections.Dictionary> stages { get; set; } = new();

    public int get_max_stage()
    {
        int maxStage = -1;
        foreach (var sv in stages) { if (sv != null) maxStage = Mathf.Max(maxStage, sv.ContainsKey("stage") ? sv["stage"].AsInt32() : -1); }
        return maxStage;
    }

    public Godot.Collections.Array<Godot.Collections.Dictionary> get_stage_unit_entries(int stage)
    {
        int bestStage = -1;
        var bestEntries = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (var stageData in stages)
        {
            if (stageData == null) continue;
            int stageIndex = stageData.ContainsKey("stage") ? stageData["stage"].AsInt32() : initial_stage;
            if (stageIndex > stage || stageIndex < bestStage) continue;
            var entries = new Godot.Collections.Array<Godot.Collections.Dictionary>();
            if (stageData.ContainsKey("unit_entries") && stageData["unit_entries"].VariantType == Variant.Type.Array)
            {
                foreach (var ev in stageData["unit_entries"].AsGodotArray())
                    if (ev.VariantType == Variant.Type.Dictionary) entries.Add(ev.AsGodotDictionary().Duplicate(true));
            }
            bestStage = stageIndex; bestEntries = entries;
        }
        return bestEntries;
    }

    public Godot.Collections.Array<string> validate_schema(Godot.Collections.Dictionary knownTemplates = null)
    {
        var errors = new Godot.Collections.Array<string>();
        if (profile_id == "") { errors.Add("Wild encounter roster is missing profile_id."); return errors; }
        if (display_name.StripEdges().Length == 0) errors.Add($"Wild encounter roster {profile_id} is missing display_name.");
        if (initial_stage < 0) errors.Add($"Wild encounter roster {profile_id} initial_stage must be >= 0.");
        if (growth_step_interval <= 0) errors.Add($"Wild encounter roster {profile_id} growth_step_interval must be >= 1.");
        if (suppression_steps_on_victory < 0) errors.Add($"Wild encounter roster {profile_id} suppression_steps_on_victory must be >= 0.");
        if (stages.Count == 0) { errors.Add($"Wild encounter roster {profile_id} must declare at least one stage."); return errors; }

        var seenStageIds = new Godot.Collections.Dictionary(); int maxDeclaredStage = -1;
        foreach (var stageData in stages)
        {
            if (stageData == null) { errors.Add($"Wild encounter roster {profile_id} contains a non-Dictionary stage."); continue; }
            var rawStage = stageData.ContainsKey("stage") ? stageData["stage"] : Variant.From(-1);
            if (rawStage.VariantType != Variant.Type.Int) { errors.Add($"Wild encounter roster {profile_id} stage field must be an int, got {rawStage}."); continue; }
            int stageIndex = rawStage.AsInt32();
            if (stageIndex < 0) errors.Add($"Wild encounter roster {profile_id} declares an invalid stage index.");
            else if (seenStageIds.ContainsKey(stageIndex)) errors.Add($"Wild encounter roster {profile_id} declares duplicate stage {stageIndex}.");
            else seenStageIds[stageIndex] = true;
            maxDeclaredStage = Mathf.Max(maxDeclaredStage, stageIndex);

            var unitEntries = stageData.ContainsKey("unit_entries") ? stageData["unit_entries"] : default;
            if (unitEntries.VariantType != Variant.Type.Array || unitEntries.AsGodotArray().Count == 0)
            { errors.Add($"Wild encounter roster {profile_id} stage {stageIndex} must declare at least one unit entry."); continue; }
            foreach (var ev in unitEntries.AsGodotArray())
            {
                if (ev.VariantType != Variant.Type.Dictionary) { errors.Add($"Wild encounter roster {profile_id} stage {stageIndex} contains a non-Dictionary unit entry."); continue; }
                var entryData = ev.AsGodotDictionary();
                var templateId = entryData.ContainsKey("template_id") ? ProgressionDataUtils.to_string_name(entryData["template_id"]) : new StringName("");
                var rawCount = entryData.ContainsKey("count") ? entryData["count"] : Variant.From(0);
                if (rawCount.VariantType != Variant.Type.Int) { errors.Add($"Wild encounter roster {profile_id} stage {stageIndex} unit entry count must be an int, got {rawCount}."); continue; }
                int count = rawCount.AsInt32();
                if (templateId == "") errors.Add($"Wild encounter roster {profile_id} stage {stageIndex} contains a unit entry without template_id.");
                else if (knownTemplates != null && !knownTemplates.ContainsKey(templateId))
                    errors.Add($"Wild encounter roster {profile_id} stage {stageIndex} references missing template {templateId}.");
                if (count <= 0) errors.Add($"Wild encounter roster {profile_id} stage {stageIndex} template {templateId} must have count >= 1.");
            }
        }
        if (!seenStageIds.ContainsKey(initial_stage))
            errors.Add($"Wild encounter roster {profile_id} initial_stage {initial_stage} does not match any declared stage (max declared: {maxDeclaredStage}).");
        return errors;
    }
}
