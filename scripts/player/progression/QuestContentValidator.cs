using Godot;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class QuestContentValidator : RefCounted
{
    public static Godot.Collections.Array<string> validate(
        GDictionary quest_defs,
        GDictionary item_defs,
        GDictionary skill_defs,
        GDictionary enemy_templates,
        Godot.Collections.Array<string> registration_errors
    )
    {
        var questEntries = new Godot.Collections.Array<GDictionary>();
        foreach (string questKey in ProgressionDataUtils.sorted_string_keys(quest_defs))
        {
            var questId = new StringName(questKey);
            questEntries.Add(
                new GDictionary
                {
                    { "source", $"quest_defs::{questId}" },
                    { "quest_def", GetContentObject<QuestDef>(quest_defs, questId) },
                }
            );
        }

        return validate_entries(
            "quest_defs",
            questEntries,
            item_defs,
            skill_defs,
            enemy_templates,
            registration_errors
        );
    }

    public static Godot.Collections.Array<string> validate_entries(
        string label,
        Godot.Collections.Array<GDictionary> quest_entries,
        GDictionary item_defs,
        GDictionary skill_defs,
        GDictionary enemy_templates
    )
    {
        return validate_entries(
            label,
            quest_entries,
            item_defs,
            skill_defs,
            enemy_templates,
            new Godot.Collections.Array<string>()
        );
    }

    private static Godot.Collections.Array<string> validate_entries(
        string label,
        Godot.Collections.Array<GDictionary> quest_entries,
        GDictionary item_defs,
        GDictionary skill_defs,
        GDictionary enemy_templates,
        Godot.Collections.Array<string> registration_errors
    )
    {
        var errors = new Godot.Collections.Array<string>();
        foreach (string registrationError in registration_errors)
            errors.Add(registrationError);

        if (item_defs.Count == 0)
            errors.Add(
                $"{label} validation requires non-empty item_defs (pass allow_missing_reference_tables=true to skip)."
            );
        if (skill_defs.Count == 0)
            errors.Add(
                $"{label} validation requires non-empty skill_defs (pass allow_missing_reference_tables=true to skip)."
            );
        if (enemy_templates.Count == 0)
            errors.Add(
                $"{label} validation requires non-empty enemy_templates (pass allow_missing_reference_tables=true to skip)."
            );

        var seenQuestIds = new GDictionary();
        var supportedProviderIds = ResolveProviderIds(new GDictionary());

        foreach (var entry in quest_entries)
        {
            if (entry == null)
                continue;

            string sourceLabel = DictString(entry, "source", label);
            var questDef = GetObject<QuestDef>(entry, "quest_def");
            if (questDef == null)
            {
                errors.Add($"Quest entry {sourceLabel} failed to cast to QuestDef.");
                continue;
            }

            if (questDef.quest_id == "")
            {
                errors.Add($"Quest entry {sourceLabel} is missing quest_id.");
                continue;
            }

            if (seenQuestIds.ContainsKey(questDef.quest_id))
            {
                errors.Add($"Duplicate quest_id registered: {questDef.quest_id}");
                continue;
            }
            seenQuestIds[questDef.quest_id] = true;

            foreach (string schemaError in questDef.validate_schema())
                errors.Add($"Quest {questDef.quest_id}: {schemaError}");

            AppendProviderReferenceErrors(errors, questDef, supportedProviderIds);
            AppendObjectiveReferenceErrors(errors, questDef, item_defs, enemy_templates);
            AppendRewardReferenceErrors(errors, questDef, item_defs, skill_defs);
        }

        return errors;
    }

    private static void AppendProviderReferenceErrors(
        Godot.Collections.Array<string> errors,
        QuestDef questDef,
        GDictionary supportedProviderIds
    )
    {
        if (questDef.provider_interaction_id == "")
        {
            errors.Add($"Quest {questDef.quest_id} is missing provider_interaction_id.");
            return;
        }

        if (!supportedProviderIds.ContainsKey(questDef.provider_interaction_id))
            errors.Add(
                $"Quest {questDef.quest_id} references missing provider_interaction_id {questDef.provider_interaction_id}."
            );
    }

    private static void AppendObjectiveReferenceErrors(
        Godot.Collections.Array<string> errors,
        QuestDef questDef,
        GDictionary itemDefs,
        GDictionary enemyTemplates
    )
    {
        foreach (var objectiveData in questDef.objective_defs)
        {
            if (objectiveData == null)
                continue;

            var objectiveId = DictStringName(objectiveData, "objective_id");
            var objectiveType = DictStringName(objectiveData, "objective_type");
            var targetId = DictStringName(objectiveData, "target_id");

            if (objectiveType == QuestDef.OBJECTIVE_SUBMIT_ITEM())
            {
                if (
                    targetId != ""
                    && itemDefs.Count > 0
                    && !HasContentId(itemDefs, targetId)
                )
                    errors.Add(
                        $"Quest {questDef.quest_id} submit_item objective {objectiveId} references missing item {targetId}."
                    );
            }
            else if (objectiveType == QuestDef.OBJECTIVE_DEFEAT_ENEMY())
            {
                if (
                    targetId != ""
                    && enemyTemplates.Count > 0
                    && !HasContentId(enemyTemplates, targetId)
                )
                    errors.Add(
                        $"Quest {questDef.quest_id} defeat_enemy objective {objectiveId} references missing enemy {targetId}."
                    );
            }
        }
    }

    private static void AppendRewardReferenceErrors(
        Godot.Collections.Array<string> errors,
        QuestDef questDef,
        GDictionary itemDefs,
        GDictionary skillDefs
    )
    {
        foreach (var rewardData in questDef.reward_entries)
        {
            if (rewardData == null)
                continue;

            var rewardType = DictStringName(rewardData, "reward_type");
            if (rewardType == QuestDef.REWARD_ITEM())
            {
                var rewardItemId = QuestDef.get_reward_item_id(rewardData);
                if (
                    rewardItemId != ""
                    && itemDefs.Count > 0
                    && !HasContentId(itemDefs, rewardItemId)
                )
                    errors.Add(
                        $"Quest {questDef.quest_id} reward references missing item {rewardItemId}."
                    );
            }
            else if (rewardType == QuestDef.REWARD_PENDING_CHARACTER_REWARD())
            {
                AppendPendingCharacterRewardReferenceErrors(
                    errors,
                    questDef,
                    rewardData,
                    skillDefs
                );
            }
        }
    }

    private static void AppendPendingCharacterRewardReferenceErrors(
        Godot.Collections.Array<string> errors,
        QuestDef questDef,
        GDictionary rewardData,
        GDictionary skillDefs
    )
    {
        if (!TryGetArray(rewardData, "entries", out var entries))
            return;

        foreach (var entryValue in entries)
        {
            if (entryValue.VariantType != Variant.Type.Dictionary)
                continue;

            var entryData = entryValue.AsGodotDictionary();
            var entryType = DictStringName(entryData, "entry_type");
            var targetId = DictStringName(entryData, "target_id");

            if (
                entryType == ""
                || !PendingCharacterRewardContentRules.is_supported_entry_type(entryType)
            )
                continue;

            if (PendingCharacterRewardContentRules.requires_skill_target(entryType))
            {
                if (
                    targetId != ""
                    && skillDefs.Count > 0
                    && !HasContentId(skillDefs, targetId)
                )
                    errors.Add(
                        $"Quest {questDef.quest_id} pending_character_reward references missing skill {targetId}."
                    );
            }

            if (
                PendingCharacterRewardContentRules.is_attribute_progress_entry(entryType)
                && targetId != ""
                && !PendingCharacterRewardContentRules.is_valid_attribute_progress_target(targetId)
            )
            {
                errors.Add(
                    $"Quest {questDef.quest_id} pending_character_reward attribute_progress references unsupported attribute {targetId}."
                );
            }

            if (
                PendingCharacterRewardContentRules.is_attribute_delta_entry(entryType)
                && targetId != ""
                && !PendingCharacterRewardContentRules.is_valid_attribute_progress_target(targetId)
                && targetId != "hp_max"
            )
            {
                errors.Add(
                    $"Quest {questDef.quest_id} pending_character_reward attribute_delta references unsupported attribute {targetId}."
                );
            }
        }
    }

    private static GDictionary ResolveProviderIds(GDictionary providerIds)
    {
        if (providerIds.Count == 0)
        {
            var supportedProviderIds = new GDictionary();
            foreach (StringName providerId in QuestProviderContentRules.SupportedProviderIds())
            {
                supportedProviderIds[providerId] = true;
            }
            return supportedProviderIds;
        }

        var normalized = new GDictionary();
        foreach (var key in providerIds.Keys)
        {
            var normalizedKey = ProgressionDataUtils.to_string_name(key);
            if (normalizedKey != "")
                normalized[normalizedKey] = providerIds[key];
        }
        return normalized;
    }

    private static T GetContentObject<T>(GDictionary source, StringName contentId)
        where T : GodotObject
    {
        if (source == null)
            return null;
        if (source.ContainsKey(contentId))
            return source[contentId].AsGodotObject() as T;

        string stringKey = contentId;
        if (source.ContainsKey(stringKey))
            return source[stringKey].AsGodotObject() as T;

        return null;
    }

    private static bool HasContentId(GDictionary source, StringName contentId)
    {
        if (source == null)
            return false;
        if (source.ContainsKey(contentId))
            return true;

        string stringKey = contentId;
        return source.ContainsKey(stringKey);
    }

    private static T GetObject<T>(GDictionary source, string key)
        where T : GodotObject
    {
        if (source == null || !source.ContainsKey(key))
            return null;
        return source[key].AsGodotObject() as T;
    }

    private static string DictString(GDictionary source, string key, string fallback = "")
    {
        if (source == null || !source.ContainsKey(key))
            return fallback;
        var value = source[key];
        return value.VariantType == Variant.Type.Nil ? fallback : value.AsString();
    }

    private static StringName DictStringName(GDictionary source, string key)
    {
        if (source == null || !source.ContainsKey(key))
            return "";
        return ProgressionDataUtils.to_string_name(source[key]);
    }

    private static bool TryGetArray(GDictionary source, string key, out Godot.Collections.Array value)
    {
        value = new Godot.Collections.Array();
        if (source == null || !source.ContainsKey(key))
            return false;
        var option = source[key];
        if (option.VariantType != Variant.Type.Array)
            return false;
        value = option.AsGodotArray();
        return true;
    }
}
