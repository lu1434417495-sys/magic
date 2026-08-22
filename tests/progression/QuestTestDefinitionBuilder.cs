using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

internal sealed class QuestTestDefinitionBuilder
{
    internal StringName quest_id = "";
    internal string display_name = "";
    internal string description = "";
    internal StringName provider_interaction_id = "";
    internal GArray tags = new();
    internal GArray accept_requirements = new();
    internal GArray objective_defs = new();
    internal GArray reward_entries = new();
    internal bool is_repeatable = false;
    internal StringName failure_policy = "terminal";
    internal int danger_tier_override = 0;
    internal StringName provider_kind = "service_contract_board";
    internal GArray listing_channels = new() { "contract_board" };
    internal GArray listing_settlement_ids = new();
    internal string accept_dialogue_text = "";
    internal string accept_feedback_success = "";
    internal string accept_feedback_failure = "";
    internal string accept_confirmation_text = "";

    internal QuestDefinition ToDefinition(string sourceLabel = "quest_test_fixture") =>
        QuestDefinition.FromImport(ToImport(), sourceLabel);

    internal QuestImportModel ToImport()
    {
        var requirements = new List<QuestAcceptRequirementImportModel>();
        foreach (Variant raw in accept_requirements)
        {
            GDictionary value = raw.AsGodotDictionary();
            requirements.Add(new QuestAcceptRequirementImportModel(
                Text(value, "requirement_type"),
                Text(value, "quest_id")
            ));
        }
        var objectives = new List<QuestObjectiveImportModel>();
        foreach (Variant raw in objective_defs)
        {
            GDictionary value = raw.AsGodotDictionary();
            bool hasGrowth = value.ContainsKey("encounter_growth_stage");
            bool validGrowth = hasGrowth && value["encounter_growth_stage"].VariantType == Variant.Type.Int;
            objectives.Add(new QuestObjectiveImportModel(
                Text(value, "objective_id"),
                Text(value, "objective_type"),
                Text(value, "target_id"),
                NullableInt(value, "target_value"),
                Text(value, "encounter_profile_id"),
                Text(value, "encounter_display_name"),
                validGrowth ? value["encounter_growth_stage"].AsInt32() : null,
                hasGrowth && !validGrowth
            ));
        }
        var rewards = new List<QuestRewardImportModel>();
        foreach (Variant raw in reward_entries)
        {
            GDictionary value = raw.AsGodotDictionary();
            var pendingEntries = new List<QuestPendingRewardImportModel>();
            if (value.ContainsKey("entries") && value["entries"].VariantType == Variant.Type.Array)
            {
                foreach (Variant pendingRaw in value["entries"].AsGodotArray())
                {
                    if (pendingRaw.VariantType != Variant.Type.Dictionary)
                    {
                        pendingEntries.Add(new QuestPendingRewardImportModel(false, "", "", null));
                        continue;
                    }
                    GDictionary pending = pendingRaw.AsGodotDictionary();
                    pendingEntries.Add(new QuestPendingRewardImportModel(
                        true,
                        Text(pending, "entry_type"),
                        Text(pending, "target_id"),
                        NullableInt(pending, "amount")
                    ));
                }
            }
            rewards.Add(new QuestRewardImportModel(
                Text(value, "reward_type"),
                NullableInt(value, "amount"),
                Text(value, "item_id"),
                NullableInt(value, "quantity"),
                Text(value, "member_id"),
                pendingEntries
            ));
        }
        return new QuestImportModel(
            quest_id.ToString(), display_name, description, provider_interaction_id.ToString(),
            Names(tags), requirements, objectives, rewards, is_repeatable,
            failure_policy.ToString(), provider_kind.ToString(), Names(listing_channels),
            Names(listing_settlement_ids), accept_dialogue_text, accept_feedback_success,
            accept_feedback_failure, accept_confirmation_text, danger_tier_override
        );
    }

    private static IReadOnlyList<string> Names(GArray source)
    {
        var values = new List<string>();
        foreach (Variant value in source) values.Add(value.AsString());
        return values;
    }

    private static string Text(GDictionary source, string key) =>
        source.ContainsKey(key) ? source[key].AsString() : "";

    private static int? NullableInt(GDictionary source, string key) =>
        source.ContainsKey(key) && source[key].VariantType == Variant.Type.Int
            ? source[key].AsInt32()
            : null;
}
