using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class SettlementServiceResult : RefCounted
{
    private static readonly string[] RequiredSerializedFields =
    {
        "success",
        "message",
        "persist_party_state",
        "persist_world_data",
        "persist_player_coord",
        "inventory_delta",
        "gold_delta",
        "pending_character_rewards",
        "quest_progress_events",
        "service_side_effects",
    };

    private static readonly string[] PendingCharacterRewardFields =
    {
        "reward_id",
        "member_id",
        "member_name",
        "source_type",
        "source_id",
        "source_label",
        "summary_text",
        "entries",
    };

    private static readonly string[] PendingCharacterRewardEntryFields =
    {
        "entry_type",
        "target_id",
        "target_label",
        "amount",
        "reason_text",
    };

    private static readonly string[] QuestProgressEventAllowedFields =
    {
        "event_type",
        "quest_id",
        "objective_id",
        "objective_type",
        "target_id",
        "target_value",
        "progress_delta",
        "world_step",
        "allow_reaccept",
        "auto_accept",
        "action_id",
        "settlement_id",
        "member_id",
    };

    private static readonly string[] QuestProgressEventAcceptAllowedFields =
    {
        "event_type",
        "quest_id",
        "world_step",
        "allow_reaccept",
    };

    private static readonly string[] QuestProgressEventCompleteAllowedFields =
    {
        "event_type",
        "quest_id",
        "world_step",
        "auto_accept",
        "allow_reaccept",
    };

    private static readonly string[] QuestProgressEventProgressAllowedFields =
    {
        "event_type",
        "quest_id",
        "objective_id",
        "objective_type",
        "target_id",
        "target_value",
        "progress_delta",
        "world_step",
        "allow_reaccept",
        "auto_accept",
        "action_id",
        "settlement_id",
        "member_id",
    };

    private static readonly StringName QuestProgressEventAccept = "accept";
    private static readonly StringName QuestProgressEventComplete = "complete";
    private static readonly StringName QuestProgressEventProgress = "progress";

    public bool success;
    public string message = "";
    public bool persist_party_state;
    public bool persist_world_data;
    public bool persist_player_coord;
    public GDictionary inventory_delta = new();
    public int gold_delta;
    public GArray pending_character_rewards = new();
    public GArray quest_progress_events = new();
    public GDictionary service_side_effects = new();

    public SettlementServiceResult set_pending_character_rewards(GArray rewards)
    {
        pending_character_rewards = DuplicateDictionaryArray(rewards);
        return this;
    }

    public SettlementServiceResult set_service_side_effects(GDictionary effects)
    {
        service_side_effects = DuplicateDictionary(effects);
        return this;
    }

    public GDictionary to_dictionary()
    {
        return new GDictionary
        {
            ["success"] = success,
            ["message"] = message,
            ["persist_party_state"] = persist_party_state,
            ["persist_world_data"] = persist_world_data,
            ["persist_player_coord"] = persist_player_coord,
            ["inventory_delta"] = DuplicateDictionary(inventory_delta),
            ["gold_delta"] = gold_delta,
            ["pending_character_rewards"] = DuplicateDictionaryArray(pending_character_rewards),
            ["quest_progress_events"] = DuplicateDictionaryArray(quest_progress_events),
            ["service_side_effects"] = DuplicateDictionary(service_side_effects),
        };
    }

    public SettlementServiceResult from_dictionary(Variant data)
    {
        if (data.VariantType != Variant.Type.Dictionary)
        {
            return null;
        }
        GDictionary payload = data.AsGodotDictionary();
        if (!HasValidSerializedPayload(payload))
        {
            return null;
        }

        success = payload["success"].AsBool();
        message = payload["message"].AsString();
        persist_party_state = payload["persist_party_state"].AsBool();
        persist_world_data = payload["persist_world_data"].AsBool();
        persist_player_coord = payload["persist_player_coord"].AsBool();
        inventory_delta = DuplicateDictionary(payload["inventory_delta"].AsGodotDictionary());
        gold_delta = payload["gold_delta"].AsInt32();
        pending_character_rewards = DuplicateDictionaryArray(payload["pending_character_rewards"].AsGodotArray());
        quest_progress_events = DuplicateDictionaryArray(payload["quest_progress_events"].AsGodotArray());
        service_side_effects = DuplicateDictionary(payload["service_side_effects"].AsGodotDictionary());
        return this;
    }

    private static bool HasValidSerializedPayload(GDictionary payload)
    {
        if (!HasExactFields(payload, RequiredSerializedFields))
        {
            return false;
        }
        if (payload["success"].VariantType != Variant.Type.Bool)
        {
            return false;
        }
        if (payload["message"].VariantType != Variant.Type.String)
        {
            return false;
        }
        if (payload["persist_party_state"].VariantType != Variant.Type.Bool)
        {
            return false;
        }
        if (payload["persist_world_data"].VariantType != Variant.Type.Bool)
        {
            return false;
        }
        if (payload["persist_player_coord"].VariantType != Variant.Type.Bool)
        {
            return false;
        }
        if (payload["inventory_delta"].VariantType != Variant.Type.Dictionary)
        {
            return false;
        }
        if (payload["gold_delta"].VariantType != Variant.Type.Int)
        {
            return false;
        }
        if (!IsPendingCharacterRewardArray(payload["pending_character_rewards"]))
        {
            return false;
        }
        if (!IsQuestProgressEventArray(payload["quest_progress_events"]))
        {
            return false;
        }
        return payload["service_side_effects"].VariantType == Variant.Type.Dictionary;
    }

    private static bool HasExactFields(GDictionary payload, string[] expectedFields)
    {
        if (payload.Count != expectedFields.Length)
        {
            return false;
        }
        var expectedLookup = new System.Collections.Generic.HashSet<string>(expectedFields);
        var seenLookup = new System.Collections.Generic.HashSet<string>();
        foreach (Variant keyVariant in payload.Keys)
        {
            if (keyVariant.VariantType != Variant.Type.String)
            {
                return false;
            }
            string key = keyVariant.AsString();
            if (!expectedLookup.Contains(key) || seenLookup.Contains(key))
            {
                return false;
            }
            seenLookup.Add(key);
        }
        return seenLookup.Count == expectedLookup.Count;
    }

    private static bool HasAllowedFields(GDictionary payload, string[] allowedFields)
    {
        var allowedLookup = new System.Collections.Generic.HashSet<string>(allowedFields);
        var seenLookup = new System.Collections.Generic.HashSet<string>();
        foreach (Variant keyVariant in payload.Keys)
        {
            if (keyVariant.VariantType != Variant.Type.String)
            {
                return false;
            }
            string key = keyVariant.AsString();
            if (!allowedLookup.Contains(key) || seenLookup.Contains(key))
            {
                return false;
            }
            seenLookup.Add(key);
        }
        return true;
    }

    private static bool IsPendingCharacterRewardArray(Variant value)
    {
        if (value.VariantType != Variant.Type.Array)
        {
            return false;
        }
        foreach (Variant entryVariant in value.AsGodotArray())
        {
            if (entryVariant.VariantType != Variant.Type.Dictionary)
            {
                return false;
            }
            if (!IsPendingCharacterRewardPayload(entryVariant.AsGodotDictionary()))
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsPendingCharacterRewardPayload(GDictionary entryData)
    {
        if (!HasExactFields(entryData, PendingCharacterRewardFields))
        {
            return false;
        }
        if (entryData["entries"].VariantType != Variant.Type.Array)
        {
            return false;
        }
        foreach (Variant rewardEntryVariant in entryData["entries"].AsGodotArray())
        {
            if (rewardEntryVariant.VariantType != Variant.Type.Dictionary)
            {
                return false;
            }
            if (!HasExactFields(rewardEntryVariant.AsGodotDictionary(), PendingCharacterRewardEntryFields))
            {
                return false;
            }
        }
        return PendingCharacterReward.from_dict(entryData) != null;
    }

    private static bool IsQuestProgressEventArray(Variant value)
    {
        if (value.VariantType != Variant.Type.Array)
        {
            return false;
        }
        foreach (Variant entryVariant in value.AsGodotArray())
        {
            if (entryVariant.VariantType != Variant.Type.Dictionary)
            {
                return false;
            }
            if (!IsQuestProgressEventPayload(entryVariant.AsGodotDictionary()))
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsQuestProgressEventPayload(GDictionary eventData)
    {
        if (!HasAllowedFields(eventData, QuestProgressEventAllowedFields))
        {
            return false;
        }
        if (!eventData.ContainsKey("event_type") || !IsNonEmptyStringNameValue(eventData["event_type"]))
        {
            return false;
        }
        StringName eventType = ProgressionDataUtils.to_string_name(eventData["event_type"]);
        if (eventData.ContainsKey("world_step")
            && (eventData["world_step"].VariantType != Variant.Type.Int || eventData["world_step"].AsInt32() < 0))
        {
            return false;
        }
        foreach (string boolField in new[] { "allow_reaccept", "auto_accept" })
        {
            if (eventData.ContainsKey(boolField) && eventData[boolField].VariantType != Variant.Type.Bool)
            {
                return false;
            }
        }
        foreach (string optionalIdField in new[] { "action_id", "settlement_id", "member_id" })
        {
            if (eventData.ContainsKey(optionalIdField) && !IsStringNameValue(eventData[optionalIdField]))
            {
                return false;
            }
        }

        if (eventType == QuestProgressEventAccept)
        {
            return HasAllowedFields(eventData, QuestProgressEventAcceptAllowedFields)
                && IsRequiredIdField(eventData, "quest_id");
        }
        if (eventType == QuestProgressEventComplete)
        {
            return HasAllowedFields(eventData, QuestProgressEventCompleteAllowedFields)
                && IsRequiredIdField(eventData, "quest_id");
        }
        if (eventType == QuestProgressEventProgress)
        {
            return HasAllowedFields(eventData, QuestProgressEventProgressAllowedFields)
                && IsValidProgressEventPayload(eventData);
        }
        return false;
    }

    private static bool IsValidProgressEventPayload(GDictionary eventData)
    {
        if (!eventData.ContainsKey("progress_delta")
            || eventData["progress_delta"].VariantType != Variant.Type.Int
            || eventData["progress_delta"].AsInt32() <= 0)
        {
            return false;
        }
        if (eventData.ContainsKey("target_value")
            && (eventData["target_value"].VariantType != Variant.Type.Int || eventData["target_value"].AsInt32() <= 0))
        {
            return false;
        }
        if (eventData.ContainsKey("quest_id") || eventData.ContainsKey("objective_id"))
        {
            return IsRequiredIdField(eventData, "quest_id") && IsRequiredIdField(eventData, "objective_id");
        }
        return IsRequiredIdField(eventData, "objective_type") && IsRequiredIdField(eventData, "target_id");
    }

    private static bool IsRequiredIdField(GDictionary payload, string fieldName)
    {
        return payload.ContainsKey(fieldName) && IsNonEmptyStringNameValue(payload[fieldName]);
    }

    private static bool IsNonEmptyStringNameValue(Variant value)
    {
        return IsStringNameValue(value) && !string.IsNullOrEmpty(value.ToString().Trim());
    }

    private static bool IsStringNameValue(Variant value)
    {
        return value.VariantType == Variant.Type.String || value.VariantType == Variant.Type.StringName;
    }

    private static GDictionary DuplicateDictionary(GDictionary value)
    {
        return value?.Duplicate(true) ?? new GDictionary();
    }

    private static GArray DuplicateDictionaryArray(Variant value)
    {
        if (value.VariantType != Variant.Type.Array)
        {
            return new GArray();
        }
        return DuplicateDictionaryArray(value.AsGodotArray());
    }

    private static GArray DuplicateDictionaryArray(GArray value)
    {
        var result = new GArray();
        if (value == null)
        {
            return result;
        }
        foreach (Variant entryVariant in value)
        {
            if (entryVariant.VariantType == Variant.Type.Dictionary)
            {
                result.Add(entryVariant.AsGodotDictionary().Duplicate(true));
            }
        }
        return result;
    }
}
