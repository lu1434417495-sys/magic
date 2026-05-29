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

    public SettlementServiceResult from_dictionary(GDictionary payload)
    {
        if (payload == null)
            return null;
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
        pending_character_rewards = DuplicateDictionaryArray(
            payload["pending_character_rewards"].AsGodotArray()
        );
        quest_progress_events = DuplicateDictionaryArray(
            payload["quest_progress_events"].AsGodotArray()
        );
        service_side_effects = DuplicateDictionary(
            payload["service_side_effects"].AsGodotDictionary()
        );
        return this;
    }

    private static bool HasValidSerializedPayload(GDictionary payload)
    {
        if (!HasExactFields(payload, RequiredSerializedFields))
        {
            return false;
        }
        if (!IsBoolValue(payload["success"]))
        {
            return false;
        }
        if (!IsStringValue(payload["message"]))
        {
            return false;
        }
        if (!IsBoolValue(payload["persist_party_state"]))
        {
            return false;
        }
        if (!IsBoolValue(payload["persist_world_data"]))
        {
            return false;
        }
        if (!IsBoolValue(payload["persist_player_coord"]))
        {
            return false;
        }
        if (!TryAsDictionary(payload["inventory_delta"], out _))
        {
            return false;
        }
        if (!IsIntValue(payload["gold_delta"]))
        {
            return false;
        }
        if (!IsPendingCharacterRewardArray(payload, "pending_character_rewards"))
        {
            return false;
        }
        if (!IsQuestProgressEventArray(payload, "quest_progress_events"))
        {
            return false;
        }
        return TryAsDictionary(payload["service_side_effects"], out _);
    }

    private static bool HasExactFields(GDictionary payload, string[] expectedFields)
    {
        if (payload.Count != expectedFields.Length)
        {
            return false;
        }
        var expectedLookup = new System.Collections.Generic.HashSet<string>(expectedFields);
        var seenLookup = new System.Collections.Generic.HashSet<string>();
        foreach (object keyValue in payload.Keys)
        {
            if (!TryAsString(keyValue, out string key))
            {
                return false;
            }
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
        foreach (object keyValue in payload.Keys)
        {
            if (!TryAsString(keyValue, out string key))
            {
                return false;
            }
            if (!allowedLookup.Contains(key) || seenLookup.Contains(key))
            {
                return false;
            }
            seenLookup.Add(key);
        }
        return true;
    }

    private static bool IsPendingCharacterRewardArray(GDictionary payload, string key)
    {
        if (payload == null || !payload.ContainsKey(key))
        {
            return false;
        }
        if (!TryAsArray(payload[key], out GArray values))
        {
            return false;
        }
        foreach (object entryValue in values)
        {
            if (!TryAsDictionary(entryValue, out GDictionary entryData))
            {
                return false;
            }
            if (!IsPendingCharacterRewardPayload(entryData))
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
        if (!TryAsArray(entryData["entries"], out GArray entries))
        {
            return false;
        }
        foreach (object rewardEntryValue in entries)
        {
            if (!TryAsDictionary(rewardEntryValue, out GDictionary rewardEntryData))
            {
                return false;
            }
            if (
                !HasExactFields(
                    rewardEntryData,
                    PendingCharacterRewardEntryFields
                )
            )
            {
                return false;
            }
        }
        return PendingCharacterReward.from_dict(entryData) != null;
    }

    private static bool IsQuestProgressEventArray(GDictionary payload, string key)
    {
        if (payload == null || !payload.ContainsKey(key))
        {
            return false;
        }
        if (!TryAsArray(payload[key], out GArray values))
        {
            return false;
        }
        foreach (object entryValue in values)
        {
            if (!TryAsDictionary(entryValue, out GDictionary eventData))
            {
                return false;
            }
            if (!IsQuestProgressEventPayload(eventData))
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
        if (
            !eventData.ContainsKey("event_type")
            || !IsNonEmptyStringNameField(eventData, "event_type")
        )
        {
            return false;
        }
        StringName eventType = ProgressionDataUtils.to_string_name(eventData["event_type"]);
        if (
            eventData.ContainsKey("world_step")
            && (
                !TryAsInt(eventData["world_step"], out int worldStep)
                || worldStep < 0
            )
        )
        {
            return false;
        }
        foreach (string boolField in new[] { "allow_reaccept", "auto_accept" })
        {
            if (
                eventData.ContainsKey(boolField)
                && !IsBoolValue(eventData[boolField])
            )
            {
                return false;
            }
        }
        foreach (string optionalIdField in new[] { "action_id", "settlement_id", "member_id" })
        {
            if (
                eventData.ContainsKey(optionalIdField)
                && !IsStringNameField(eventData, optionalIdField)
            )
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
        if (
            !eventData.ContainsKey("progress_delta")
            || !TryAsInt(eventData["progress_delta"], out int progressDelta)
            || progressDelta <= 0
        )
        {
            return false;
        }
        if (
            eventData.ContainsKey("target_value")
            && (
                !TryAsInt(eventData["target_value"], out int targetValue)
                || targetValue <= 0
            )
        )
        {
            return false;
        }
        if (eventData.ContainsKey("quest_id") || eventData.ContainsKey("objective_id"))
        {
            return IsRequiredIdField(eventData, "quest_id")
                && IsRequiredIdField(eventData, "objective_id");
        }
        return IsRequiredIdField(eventData, "objective_type")
            && IsRequiredIdField(eventData, "target_id");
    }

    private static bool IsRequiredIdField(GDictionary payload, string fieldName)
    {
        return IsNonEmptyStringNameField(payload, fieldName);
    }

    private static bool IsNonEmptyStringNameField(GDictionary payload, string fieldName)
    {
        if (!IsStringNameField(payload, fieldName))
        {
            return false;
        }
        return !string.IsNullOrEmpty(payload[fieldName].ToString().Trim());
    }

    private static bool IsStringNameField(GDictionary payload, string fieldName)
    {
        if (payload == null || !payload.ContainsKey(fieldName))
        {
            return false;
        }
        return IsStringNameValue(payload[fieldName]);
    }

    private static GDictionary DuplicateDictionary(GDictionary value)
    {
        return value?.Duplicate(true) ?? new GDictionary();
    }

    private static GArray DuplicateDictionaryArray(GArray value)
    {
        var result = new GArray();
        if (value == null)
        {
            return result;
        }
        foreach (GDictionary entryData in Dictionaries(value))
        {
            result.Add(entryData.Duplicate(true));
        }
        return result;
    }

    private static System.Collections.Generic.IEnumerable<GDictionary> Dictionaries(GArray values)
    {
        if (values == null)
        {
            yield break;
        }
        foreach (object rawValue in values)
        {
            if (TryAsDictionary(rawValue, out GDictionary value))
            {
                yield return value;
            }
        }
    }

    private static bool TryAsArray(object rawValue, out GArray value)
    {
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Array)
        {
            value = variant.AsGodotArray();
            return true;
        }
        if (rawValue is GArray array)
        {
            value = array;
            return true;
        }
        value = new GArray();
        return false;
    }

    private static bool TryAsDictionary(object rawValue, out GDictionary value)
    {
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Dictionary)
        {
            value = variant.AsGodotDictionary();
            return true;
        }
        if (rawValue is GDictionary dictionary)
        {
            value = dictionary;
            return true;
        }
        value = new GDictionary();
        return false;
    }

    private static bool TryAsString(object rawValue, out string value)
    {
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.String)
        {
            value = variant.AsString();
            return true;
        }
        if (rawValue is string stringValue)
        {
            value = stringValue;
            return true;
        }
        value = "";
        return false;
    }

    private static bool IsStringValue(object rawValue)
    {
        return TryAsString(rawValue, out _);
    }

    private static bool IsStringNameValue(object rawValue)
    {
        if (rawValue is Variant variant)
        {
            return variant.VariantType == Variant.Type.String
                || variant.VariantType == Variant.Type.StringName;
        }
        return rawValue is string or StringName;
    }

    private static bool IsBoolValue(object rawValue)
    {
        return rawValue switch
        {
            Variant variant => variant.VariantType == Variant.Type.Bool,
            bool => true,
            _ => false,
        };
    }

    private static bool IsIntValue(object rawValue)
    {
        return TryAsInt(rawValue, out _);
    }

    private static bool TryAsInt(object rawValue, out int value)
    {
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Int)
        {
            value = variant.AsInt32();
            return true;
        }
        if (rawValue is int intValue)
        {
            value = intValue;
            return true;
        }
        value = 0;
        return false;
    }
}
