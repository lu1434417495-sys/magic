using Godot;

[GlobalClass]
public partial class QuestDef : Resource
{
    private static readonly StringName ObjectiveSubmitItem = "submit_item";
    private static readonly StringName ObjectiveDefeatEnemy = "defeat_enemy";
    private static readonly StringName ObjectiveSettlementAction = "settlement_action";
    private static readonly StringName RewardGold = "gold";
    private static readonly StringName RewardItem = "item";
    private static readonly StringName RewardPendingCharacterReward = "pending_character_reward";

    private static readonly string[] RequiredSerializedFields =
    {
        "quest_id",
        "display_name",
        "description",
        "provider_interaction_id",
        "tags",
        "accept_requirements",
        "objective_defs",
        "reward_entries",
        "is_repeatable",
    };

    public static StringName OBJECTIVE_SUBMIT_ITEM() => ObjectiveSubmitItem;
    public static StringName OBJECTIVE_DEFEAT_ENEMY() => ObjectiveDefeatEnemy;
    public static StringName OBJECTIVE_SETTLEMENT_ACTION() => ObjectiveSettlementAction;
    public static StringName REWARD_GOLD() => RewardGold;
    public static StringName REWARD_ITEM() => RewardItem;
    public static StringName REWARD_PENDING_CHARACTER_REWARD() => RewardPendingCharacterReward;

    [Export] public StringName quest_id { get; set; } = "";
    [Export] public string display_name { get; set; } = "";
    [Export(PropertyHint.MultilineText)] public string description { get; set; } = "";
    [Export] public StringName provider_interaction_id { get; set; } = "";
    [Export] public Godot.Collections.Array<StringName> tags { get; set; } = new();
    [Export] public Godot.Collections.Array<Godot.Collections.Dictionary> accept_requirements { get; set; } = new();
    [Export] public Godot.Collections.Array<Godot.Collections.Dictionary> objective_defs { get; set; } = new();
    [Export] public Godot.Collections.Array<Godot.Collections.Dictionary> reward_entries { get; set; } = new();
    [Export] public bool is_repeatable { get; set; }

    public bool is_empty()
    {
        return quest_id == "";
    }

    public Godot.Collections.Array<StringName> get_objective_ids()
    {
        var result = new Godot.Collections.Array<StringName>();
        foreach (var objectiveData in objective_defs)
        {
            var objectiveId = ProgressionDataUtils.to_string_name(GetDictValue(objectiveData, "objective_id"));
            if (objectiveId != "")
                result.Add(objectiveId);
        }
        return result;
    }

    public Godot.Collections.Dictionary get_objective_def(StringName objective_id)
    {
        foreach (var objectiveData in objective_defs)
        {
            if (ProgressionDataUtils.to_string_name(GetDictValue(objectiveData, "objective_id")) == objective_id)
                return objectiveData.Duplicate(true);
        }
        return new Godot.Collections.Dictionary();
    }

    public Godot.Collections.Array<string> validate_schema()
    {
        var errors = new Godot.Collections.Array<string>();
        if (quest_id == "")
            errors.Add("QuestDef 缺少 quest_id。");
        if (display_name.StripEdges().Length == 0)
            errors.Add($"QuestDef {(string)quest_id} 缺少 display_name。");
        if (objective_defs.Count == 0)
            errors.Add($"QuestDef {(string)quest_id} 至少需要一个 objective_def。");

        var seenObjectiveIds = new Godot.Collections.Dictionary();
        foreach (var objectiveData in objective_defs)
        {
            var objectiveId = ProgressionDataUtils.to_string_name(GetDictValue(objectiveData, "objective_id"));
            var objectiveType = ProgressionDataUtils.to_string_name(GetDictValue(objectiveData, "objective_type"));
            if (objectiveId == "")
            {
                errors.Add($"QuestDef {(string)quest_id} 存在空 objective_id。");
                continue;
            }
            if (seenObjectiveIds.ContainsKey(objectiveId))
            {
                errors.Add($"QuestDef {(string)quest_id} 存在重复 objective_id {(string)objectiveId}。");
                continue;
            }
            seenObjectiveIds[objectiveId] = true;

            if (objectiveType == "")
            {
                errors.Add($"QuestDef {(string)quest_id} 的 objective {(string)objectiveId} 缺少 objective_type。");
            }
            else if (!GetSupportedObjectiveTypes().Contains(objectiveType))
            {
                errors.Add($"QuestDef {(string)quest_id} 的 objective {(string)objectiveId} 使用了不支持的 objective_type {(string)objectiveType}。");
            }

            if (!objectiveData.ContainsKey("target_value") || objectiveData["target_value"].VariantType != Variant.Type.Int)
            {
                errors.Add($"QuestDef {(string)quest_id} 的 objective {(string)objectiveId} 必须显式提供 int target_value。");
                continue;
            }
            int targetValue = objectiveData["target_value"].AsInt32();
            if (targetValue <= 0)
                errors.Add($"QuestDef {(string)quest_id} 的 objective {(string)objectiveId} 必须有正 target_value。");

            if (objectiveType == ObjectiveSubmitItem)
            {
                var submitItemId = ProgressionDataUtils.to_string_name(GetDictValue(objectiveData, "target_id"));
                if (submitItemId == "")
                    errors.Add($"QuestDef {(string)quest_id} 的 submit_item objective {(string)objectiveId} 缺少 target_id。");
            }
            else if (objectiveType == ObjectiveSettlementAction)
            {
                var settlementActionId = ProgressionDataUtils.to_string_name(GetDictValue(objectiveData, "target_id"));
                if (settlementActionId == "")
                    errors.Add($"QuestDef {(string)quest_id} 的 settlement_action objective {(string)objectiveId} 缺少 target_id（settlement action 必须显式指定 action_id）。");
            }
        }

        foreach (var rewardData in reward_entries)
        {
            var rewardType = ProgressionDataUtils.to_string_name(GetDictValue(rewardData, "reward_type"));
            if (rewardType == "")
            {
                errors.Add($"QuestDef {(string)quest_id} 存在缺少 reward_type 的 reward_entry。");
                continue;
            }
            if (!GetSupportedRewardTypes().Contains(rewardType))
            {
                errors.Add($"QuestDef {(string)quest_id} 使用了不支持的 reward_type {(string)rewardType}。");
                continue;
            }

            if (rewardType == RewardGold)
            {
                if (!rewardData.ContainsKey("amount") || rewardData["amount"].VariantType != Variant.Type.Int || rewardData["amount"].AsInt32() <= 0)
                    errors.Add($"QuestDef {(string)quest_id} 的 gold reward 必须有正 amount。");
            }
            else if (rewardType == RewardItem)
            {
                var rewardItemId = get_reward_item_id(rewardData);
                if (rewardItemId == "")
                    errors.Add($"QuestDef {(string)quest_id} 的 item reward 缺少 item_id。");
                if (get_reward_quantity(rewardData) <= 0)
                    errors.Add($"QuestDef {(string)quest_id} 的 item reward 必须有正 quantity。");
            }
            else if (rewardType == RewardPendingCharacterReward)
            {
                foreach (string error in ValidatePendingCharacterReward(quest_id, rewardData))
                    errors.Add(error);
            }
        }
        return errors;
    }

    public Godot.Collections.Dictionary to_dict()
    {
        return new Godot.Collections.Dictionary
        {
            { "quest_id", (string)quest_id },
            { "display_name", display_name },
            { "description", description },
            { "provider_interaction_id", (string)provider_interaction_id },
            { "tags", ProgressionDataUtils.string_name_array_to_string_array(tags) },
            { "accept_requirements", DuplicateDictionaryArray(accept_requirements) },
            { "objective_defs", DuplicateDictionaryArray(objective_defs) },
            { "reward_entries", DuplicateDictionaryArray(reward_entries) },
            { "is_repeatable", is_repeatable },
        };
    }

    public static QuestDef from_dict(Variant data)
    {
        if (data.VariantType != Variant.Type.Dictionary)
            return null;
        var payload = data.AsGodotDictionary();
        if (!HasExactSerializedFields(payload))
            return null;

        var questIdValue = ReadRequiredStringName(payload["quest_id"]);
        var providerInteractionIdValue = ReadRequiredStringName(payload["provider_interaction_id"]);
        if (questIdValue == "" || providerInteractionIdValue == "")
            return null;

        if (payload["display_name"].VariantType != Variant.Type.String)
            return null;
        string displayNameValue = payload["display_name"].AsString();
        if (displayNameValue.StripEdges().Length == 0)
            return null;

        if (payload["description"].VariantType != Variant.Type.String)
            return null;

        if (payload["tags"].VariantType != Variant.Type.Array)
            return null;
        var tagValues = new Godot.Collections.Array<StringName>();
        foreach (var tagVariant in payload["tags"].AsGodotArray())
        {
            var tagValue = ReadRequiredStringName(tagVariant);
            if (tagValue == "")
                return null;
            tagValues.Add(tagValue);
        }

        if (!TryReadDictionaryArray(payload["accept_requirements"], false, out var acceptRequirementValues))
            return null;
        if (!TryReadDictionaryArray(payload["objective_defs"], true, out var objectiveDefValues))
            return null;
        if (!TryReadDictionaryArray(payload["reward_entries"], false, out var rewardEntryValues))
            return null;
        if (payload["is_repeatable"].VariantType != Variant.Type.Bool)
            return null;

        var questDef = new QuestDef
        {
            quest_id = questIdValue,
            display_name = displayNameValue,
            description = payload["description"].AsString(),
            provider_interaction_id = providerInteractionIdValue,
            tags = tagValues,
            accept_requirements = acceptRequirementValues,
            objective_defs = objectiveDefValues,
            reward_entries = rewardEntryValues,
            is_repeatable = payload["is_repeatable"].AsBool(),
        };
        return questDef.validate_schema().Count == 0 ? questDef : null;
    }

    public static StringName get_reward_item_id(Godot.Collections.Dictionary reward_data)
    {
        if (!reward_data.ContainsKey("item_id"))
            return "";
        var itemIdVariant = reward_data["item_id"];
        if (itemIdVariant.VariantType != Variant.Type.String && itemIdVariant.VariantType != Variant.Type.StringName)
            return "";
        return ProgressionDataUtils.to_string_name(itemIdVariant);
    }

    public static int get_reward_quantity(Godot.Collections.Dictionary reward_data)
    {
        if (!reward_data.ContainsKey("quantity") || reward_data["quantity"].VariantType != Variant.Type.Int)
            return 0;
        return reward_data["quantity"].AsInt32();
    }

    private static bool HasExactSerializedFields(Godot.Collections.Dictionary payload)
    {
        if (payload.Count != RequiredSerializedFields.Length)
            return false;
        foreach (string fieldName in RequiredSerializedFields)
        {
            if (!payload.ContainsKey(fieldName))
                return false;
        }
        return true;
    }

    private static StringName ReadRequiredStringName(Variant value)
    {
        if (value.VariantType != Variant.Type.String && value.VariantType != Variant.Type.StringName)
            return "";
        string text = value.AsString().StripEdges();
        if (text.Length == 0)
            return "";
        return new StringName(text);
    }

    private static Godot.Collections.Array<StringName> GetSupportedObjectiveTypes()
    {
        return new Godot.Collections.Array<StringName> { ObjectiveSubmitItem, ObjectiveDefeatEnemy, ObjectiveSettlementAction };
    }

    private static Godot.Collections.Array<StringName> GetSupportedRewardTypes()
    {
        return new Godot.Collections.Array<StringName> { RewardGold, RewardItem, RewardPendingCharacterReward };
    }

    private static Godot.Collections.Array<string> ValidatePendingCharacterReward(StringName questIdValue, Godot.Collections.Dictionary rewardData)
    {
        var errors = new Godot.Collections.Array<string>();
        string questIdText = (string)questIdValue;
        var memberId = ProgressionDataUtils.to_string_name(GetDictValue(rewardData, "member_id"));
        if (memberId == "")
            errors.Add($"QuestDef {questIdText} 的 pending_character_reward 缺少 member_id。");

        var entriesVariant = GetDictValue(rewardData, "entries", new Godot.Collections.Array());
        if (entriesVariant.VariantType != Variant.Type.Array || entriesVariant.AsGodotArray().Count == 0)
        {
            errors.Add($"QuestDef {questIdText} 的 pending_character_reward 至少需要一条 entries。");
            return errors;
        }

        foreach (var entryVariant in entriesVariant.AsGodotArray())
        {
            if (entryVariant.VariantType != Variant.Type.Dictionary)
            {
                errors.Add($"QuestDef {questIdText} 的 pending_character_reward 包含非 Dictionary entry。");
                continue;
            }
            var entryData = entryVariant.AsGodotDictionary();
            var entryType = ProgressionDataUtils.to_string_name(GetDictValue(entryData, "entry_type"));
            var targetId = ProgressionDataUtils.to_string_name(GetDictValue(entryData, "target_id"));
            if (!entryData.ContainsKey("amount") || entryData["amount"].VariantType != Variant.Type.Int)
            {
                errors.Add($"QuestDef {questIdText} 的 pending_character_reward entry amount 必须是 int。");
                continue;
            }
            int amount = entryData["amount"].AsInt32();
            if (entryType == "")
            {
                errors.Add($"QuestDef {questIdText} 的 pending_character_reward entry 缺少 entry_type。");
            }
            else if (!PendingCharacterRewardContentRules.is_supported_entry_type(entryType))
            {
                errors.Add($"QuestDef {questIdText} has unsupported pending_character_reward entry_type {(string)entryType}. Supported: {PendingCharacterRewardContentRules.valid_entry_type_label()}.");
            }
            if (targetId == "")
                errors.Add($"QuestDef {questIdText} 的 pending_character_reward entry 缺少 target_id。");
            if (amount == 0)
                errors.Add($"QuestDef {questIdText} 的 pending_character_reward entry amount 不能为 0。");
        }
        return errors;
    }

    private static bool TryReadDictionaryArray(Variant value, bool requireNonEmpty, out Godot.Collections.Array<Godot.Collections.Dictionary> result)
    {
        result = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        if (value.VariantType != Variant.Type.Array)
            return false;
        var source = value.AsGodotArray();
        if (requireNonEmpty && source.Count == 0)
            return false;
        foreach (var item in source)
        {
            if (item.VariantType != Variant.Type.Dictionary)
                return false;
            result.Add(item.AsGodotDictionary().Duplicate(true));
        }
        return true;
    }

    private static Godot.Collections.Array<Godot.Collections.Dictionary> DuplicateDictionaryArray(Godot.Collections.Array<Godot.Collections.Dictionary> source)
    {
        var result = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (var item in source)
            result.Add(item.Duplicate(true));
        return result;
    }

    private static Variant GetDictValue(Godot.Collections.Dictionary dict, Variant key)
    {
        return GetDictValue(dict, key, default);
    }

    private static Variant GetDictValue(Godot.Collections.Dictionary dict, Variant key, Variant defaultValue)
    {
        return dict.ContainsKey(key) ? dict[key] : defaultValue;
    }
}
