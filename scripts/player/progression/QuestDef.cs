using System.Collections.Generic;
using Godot;

internal enum QuestObjectiveKind
{
    Unknown = 0,
    SubmitItem,
    DefeatEnemy,
    SettlementAction,
}

internal enum QuestRewardKind
{
    Unknown = 0,
    Gold,
    Item,
    PendingCharacterReward,
}

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
        "provider_kind",
        "provider_interaction_id",
        "listing_channels",
        "listing_settlement_ids",
        "tags",
        "accept_requirements",
        "objective_defs",
        "reward_entries",
        "is_repeatable",
        "danger_tier_override",
        "accept_dialogue_text",
        "accept_feedback_success",
        "accept_feedback_failure",
        "accept_confirmation_text",
    };

    internal static StringName ToStringName(QuestObjectiveKind kind)
    {
        return kind switch
        {
            QuestObjectiveKind.SubmitItem => ObjectiveSubmitItem,
            QuestObjectiveKind.DefeatEnemy => ObjectiveDefeatEnemy,
            QuestObjectiveKind.SettlementAction => ObjectiveSettlementAction,
            _ => new StringName(""),
        };
    }

    internal static QuestObjectiveKind ToObjectiveKind(StringName value)
    {
        if (value == ObjectiveSubmitItem)
            return QuestObjectiveKind.SubmitItem;
        if (value == ObjectiveDefeatEnemy)
            return QuestObjectiveKind.DefeatEnemy;
        if (value == ObjectiveSettlementAction)
            return QuestObjectiveKind.SettlementAction;
        return QuestObjectiveKind.Unknown;
    }

    internal static StringName ToStringName(QuestRewardKind kind)
    {
        return kind switch
        {
            QuestRewardKind.Gold => RewardGold,
            QuestRewardKind.Item => RewardItem,
            QuestRewardKind.PendingCharacterReward => RewardPendingCharacterReward,
            _ => new StringName(""),
        };
    }

    internal static QuestRewardKind ToRewardKind(StringName value)
    {
        if (value == RewardGold)
            return QuestRewardKind.Gold;
        if (value == RewardItem)
            return QuestRewardKind.Item;
        if (value == RewardPendingCharacterReward)
            return QuestRewardKind.PendingCharacterReward;
        return QuestRewardKind.Unknown;
    }

    [Export]
    public StringName quest_id { get; set; } = "";

    [Export]
    public string display_name { get; set; } = "";

    [Export(PropertyHint.MultilineText)]
    public string description { get; set; } = "";

    [Export]
    public StringName provider_interaction_id { get; set; } = "";

    [Export]
    public Godot.Collections.Array<StringName> tags { get; set; } = new();

    [Export]
    public Godot.Collections.Array<Godot.Collections.Dictionary> accept_requirements { get; set; } =
        new();

    [Export]
    public Godot.Collections.Array<Godot.Collections.Dictionary> objective_defs { get; set; } =
        new();

    [Export]
    public Godot.Collections.Array<Godot.Collections.Dictionary> reward_entries { get; set; } =
        new();

    [Export]
    public bool is_repeatable { get; set; }

    // 0 = 自动推导危险度；1..5 = 作者显式覆盖星级。派生结果只用于投影，不进存档。
    [Export(PropertyHint.Range, "0,5")]
    public int danger_tier_override { get; set; }

    [Export]
    public StringName provider_kind { get; set; } = "";

    [Export]
    public Godot.Collections.Array<StringName> listing_channels { get; set; } = new();

    // 值为 SettlementConfig.settlement_id（运行时据点 record 的 template_id）。
    // 悬赏板任务必须绑定至少一个据点；其它渠道当前不消费该字段，配置即报错。
    [Export]
    public Godot.Collections.Array<StringName> listing_settlement_ids { get; set; } = new();

    [Export(PropertyHint.MultilineText)]
    public string accept_dialogue_text { get; set; } = "";

    [Export]
    public string accept_feedback_success { get; set; } = "";

    [Export]
    public string accept_feedback_failure { get; set; } = "";

    [Export(PropertyHint.MultilineText)]
    public string accept_confirmation_text { get; set; } = "";

    internal Godot.Collections.Array<StringName> TagsBorrowed => tags;
    internal Godot.Collections.Array<Godot.Collections.Dictionary> AcceptRequirementsBorrowed =>
        accept_requirements;
    internal Godot.Collections.Array<Godot.Collections.Dictionary> ObjectiveDefsBorrowed =>
        objective_defs;
    internal Godot.Collections.Array<Godot.Collections.Dictionary> RewardEntriesBorrowed =>
        reward_entries;
    internal Godot.Collections.Array<StringName> ListingChannelsBorrowed => listing_channels;
    internal Godot.Collections.Array<StringName> ListingSettlementIdsBorrowed =>
        listing_settlement_ids;

    internal sealed class AcceptRequirementEntryData
    {
        public readonly StringName RequirementType;
        public readonly StringName QuestId;

        private AcceptRequirementEntryData(StringName requirementType, StringName questId)
        {
            RequirementType = requirementType;
            QuestId = questId;
        }

        public static AcceptRequirementEntryData FromDictionary(
            Godot.Collections.Dictionary requirementData
        ) =>
            new(
                DictStringName(requirementData, "requirement_type"),
                DictStringName(requirementData, "quest_id")
            );
    }

    internal sealed class ObjectiveEntryData
    {
        public readonly StringName ObjectiveId;
        public readonly StringName ObjectiveType;
        public readonly StringName TargetId;
        public readonly bool HasStrictTargetValue;
        public readonly int TargetValue;

        private ObjectiveEntryData(
            StringName objectiveId,
            StringName objectiveType,
            StringName targetId,
            bool hasStrictTargetValue,
            int targetValue
        )
        {
            ObjectiveId = objectiveId;
            ObjectiveType = objectiveType;
            TargetId = targetId;
            HasStrictTargetValue = hasStrictTargetValue;
            TargetValue = targetValue;
        }

        public static ObjectiveEntryData FromDictionary(Godot.Collections.Dictionary objectiveData)
        {
            bool hasStrictTargetValue = TryGetStrictInt(
                objectiveData,
                "target_value",
                out int targetValue
            );
            return new ObjectiveEntryData(
                DictStringName(objectiveData, "objective_id"),
                DictStringName(objectiveData, "objective_type"),
                DictStringName(objectiveData, "target_id"),
                hasStrictTargetValue,
                hasStrictTargetValue ? targetValue : 0
            );
        }
    }

    internal sealed class RewardEntryData
    {
        public readonly StringName RewardType;
        public readonly bool HasStrictGoldAmount;
        public readonly int GoldAmount;
        public readonly StringName ItemId;
        public readonly bool HasStrictItemQuantity;
        public readonly int ItemQuantity;
        public readonly StringName PendingRewardMemberId;
        public readonly IReadOnlyList<PendingRewardEntryData> PendingRewardEntries;

        private RewardEntryData(
            StringName rewardType,
            bool hasStrictGoldAmount,
            int goldAmount,
            StringName itemId,
            bool hasStrictItemQuantity,
            int itemQuantity,
            StringName pendingRewardMemberId,
            IReadOnlyList<PendingRewardEntryData> pendingRewardEntries
        )
        {
            RewardType = rewardType;
            HasStrictGoldAmount = hasStrictGoldAmount;
            GoldAmount = goldAmount;
            ItemId = itemId;
            HasStrictItemQuantity = hasStrictItemQuantity;
            ItemQuantity = itemQuantity;
            PendingRewardMemberId = pendingRewardMemberId;
            PendingRewardEntries = pendingRewardEntries ?? System.Array.Empty<PendingRewardEntryData>();
        }

        public static RewardEntryData FromDictionary(Godot.Collections.Dictionary rewardData)
        {
            bool hasStrictGoldAmount = TryGetStrictInt(rewardData, "amount", out int goldAmount);
            bool hasStrictItemQuantity = TryGetStrictInt(
                rewardData,
                "quantity",
                out int itemQuantity
            );
            return new RewardEntryData(
                DictStringName(rewardData, "reward_type"),
                hasStrictGoldAmount,
                hasStrictGoldAmount ? goldAmount : 0,
                DictStringName(rewardData, "item_id"),
                hasStrictItemQuantity,
                hasStrictItemQuantity ? itemQuantity : 0,
                DictStringName(rewardData, "member_id"),
                CollectPendingRewardEntries(rewardData)
            );
        }
    }

    internal sealed class PendingRewardEntryData
    {
        public readonly bool IsDictionaryEntry;
        public readonly StringName EntryType;
        public readonly StringName TargetId;
        public readonly bool HasStrictAmount;
        public readonly int Amount;

        private PendingRewardEntryData(
            bool isDictionaryEntry,
            StringName entryType,
            StringName targetId,
            bool hasStrictAmount,
            int amount
        )
        {
            IsDictionaryEntry = isDictionaryEntry;
            EntryType = entryType;
            TargetId = targetId;
            HasStrictAmount = hasStrictAmount;
            Amount = amount;
        }

        public static PendingRewardEntryData FromDictionary(Godot.Collections.Dictionary entryData)
        {
            bool hasStrictAmount = TryGetStrictInt(entryData, "amount", out int amount);
            return new PendingRewardEntryData(
                true,
                DictStringName(entryData, "entry_type"),
                DictStringName(entryData, "target_id"),
                hasStrictAmount,
                hasStrictAmount ? amount : 0
            );
        }

        public static PendingRewardEntryData InvalidNonDictionary() =>
            new(false, "", "", false, 0);
    }

    public bool IsEmpty()
    {
        return quest_id == "";
    }

    public Godot.Collections.Array<string> ValidateSchema()
    {
        var errors = new Godot.Collections.Array<string>();
        if (quest_id == "")
            errors.Add("QuestDef 缺少 quest_id。");
        if (display_name.StripEdges().Length == 0)
            errors.Add($"QuestDef {(string)quest_id} 缺少 display_name。");
        if (provider_kind == "")
            errors.Add($"QuestDef {(string)quest_id} 的 provider_kind 不能为空。");
        if (listing_channels == null || listing_channels.Count == 0)
            errors.Add($"QuestDef {(string)quest_id} 的 listing_channels 不能为空数组。");
        foreach (StringName channel in listing_channels)
        {
            if (channel == "")
                errors.Add($"QuestDef {(string)quest_id} 的 listing_channels 包含空值。");
        }
        foreach (StringName settlementId in listing_settlement_ids)
        {
            if (settlementId == "")
                errors.Add(
                    $"QuestDef {(string)quest_id} 的 listing_settlement_ids 包含空值。"
                );
        }
        if (objective_defs.Count == 0)
            errors.Add($"QuestDef {(string)quest_id} 至少需要一个 objective_def。");
        if (danger_tier_override < 0 || danger_tier_override > 5)
            errors.Add(
                $"QuestDef {(string)quest_id} 的 danger_tier_override 必须在 0..5 之间（0 表示自动推导）。"
            );

        var seenObjectiveIds = new System.Collections.Generic.HashSet<StringName>();
        foreach (ObjectiveEntryData objective in GetObjectiveEntriesTyped())
        {
            var objectiveId = objective.ObjectiveId;
            var objectiveType = objective.ObjectiveType;
            if (objectiveId == "")
            {
                errors.Add($"QuestDef {(string)quest_id} 存在空 objective_id。");
                continue;
            }
            if (!seenObjectiveIds.Add(objectiveId))
            {
                errors.Add(
                    $"QuestDef {(string)quest_id} 存在重复 objective_id {(string)objectiveId}。"
                );
                continue;
            }

            if (objectiveType == "")
            {
                errors.Add(
                    $"QuestDef {(string)quest_id} 的 objective {(string)objectiveId} 缺少 objective_type。"
                );
            }
            else if (!GetSupportedObjectiveTypes().Contains(objectiveType))
            {
                errors.Add(
                    $"QuestDef {(string)quest_id} 的 objective {(string)objectiveId} 使用了不支持的 objective_type {(string)objectiveType}。"
                );
            }

            if (!objective.HasStrictTargetValue)
            {
                errors.Add(
                    $"QuestDef {(string)quest_id} 的 objective {(string)objectiveId} 必须显式提供 int target_value。"
                );
                continue;
            }
            int targetValue = objective.TargetValue;
            if (targetValue <= 0)
                errors.Add(
                    $"QuestDef {(string)quest_id} 的 objective {(string)objectiveId} 必须有正 target_value。"
                );

            if (objectiveType == ObjectiveSubmitItem)
            {
                var submitItemId = objective.TargetId;
                if (submitItemId == "")
                    errors.Add(
                        $"QuestDef {(string)quest_id} 的 submit_item objective {(string)objectiveId} 缺少 target_id。"
                    );
            }
            else if (objectiveType == ObjectiveSettlementAction)
            {
                var settlementActionId = objective.TargetId;
                if (settlementActionId == "")
                    errors.Add(
                        $"QuestDef {(string)quest_id} 的 settlement_action objective {(string)objectiveId} 缺少 target_id（settlement action 必须显式指定 action_id）。"
                    );
            }
        }

        foreach (RewardEntryData reward in GetRewardEntriesTyped())
        {
            var rewardType = reward.RewardType;
            if (rewardType == "")
            {
                errors.Add($"QuestDef {(string)quest_id} 存在缺少 reward_type 的 reward_entry。");
                continue;
            }
            if (!GetSupportedRewardTypes().Contains(rewardType))
            {
                errors.Add(
                    $"QuestDef {(string)quest_id} 使用了不支持的 reward_type {(string)rewardType}。"
                );
                continue;
            }

            if (rewardType == RewardGold)
            {
                if (!reward.HasStrictGoldAmount || reward.GoldAmount <= 0)
                    errors.Add($"QuestDef {(string)quest_id} 的 gold reward 必须有正 amount。");
            }
            else if (rewardType == RewardItem)
            {
                var rewardItemId = reward.ItemId;
                if (rewardItemId == "")
                    errors.Add($"QuestDef {(string)quest_id} 的 item reward 缺少 item_id。");
                if (!reward.HasStrictItemQuantity || reward.ItemQuantity <= 0)
                    errors.Add($"QuestDef {(string)quest_id} 的 item reward 必须有正 quantity。");
            }
            else if (rewardType == RewardPendingCharacterReward)
            {
                foreach (string error in ValidatePendingCharacterReward(quest_id, reward))
                    errors.Add(error);
            }
        }
        return errors;
    }

    internal IReadOnlyList<ObjectiveEntryData> GetObjectiveEntriesTyped()
    {
        var result = new System.Collections.Generic.List<ObjectiveEntryData>();
        foreach (var objectiveData in objective_defs)
            result.Add(ObjectiveEntryData.FromDictionary(objectiveData));
        return result;
    }

    internal IReadOnlyList<AcceptRequirementEntryData> GetAcceptRequirementEntriesTyped()
    {
        var result = new System.Collections.Generic.List<AcceptRequirementEntryData>();
        foreach (var requirementData in accept_requirements)
            result.Add(AcceptRequirementEntryData.FromDictionary(requirementData));
        return result;
    }

    internal IReadOnlyList<RewardEntryData> GetRewardEntriesTyped()
    {
        var result = new System.Collections.Generic.List<RewardEntryData>();
        foreach (var rewardData in reward_entries)
            result.Add(RewardEntryData.FromDictionary(rewardData));
        return result;
    }

    public static QuestDef FromDictionary(Godot.Collections.Dictionary payload)
    {
        if (payload == null)
            return null;
        if (!HasExactSerializedFields(payload))
            return null;

        var questIdValue = ReadRequiredStringName(payload, "quest_id");
        var providerInteractionIdValue = ReadRequiredStringName(payload, "provider_interaction_id");
        if (questIdValue == "" || providerInteractionIdValue == "")
            return null;

        if (!TryGetStrictString(payload, "display_name", out string displayNameValue))
            return null;
        if (displayNameValue.StripEdges().Length == 0)
            return null;

        if (!TryGetStrictString(payload, "description", out string descriptionValue))
            return null;

        if (!TryGetArray(payload, "tags", out Godot.Collections.Array tagSource))
            return null;
        var tagValues = new Godot.Collections.Array<StringName>();
        foreach (var rawTagValue in tagSource)
        {
            if (!TryAsStringLike(rawTagValue, out string rawTagText))
                return null;
            string parsedTagText = rawTagText.StripEdges();
            if (parsedTagText.Length == 0)
                return null;
            var parsedTagValue = new StringName(parsedTagText);
            if (parsedTagValue == "")
                return null;
            tagValues.Add(parsedTagValue);
        }

        if (
            !TryReadDictionaryArray(
                payload,
                "accept_requirements",
                false,
                out var acceptRequirementValues
            )
        )
            return null;
        if (!TryReadDictionaryArray(payload, "objective_defs", true, out var objectiveDefValues))
            return null;
        if (!TryReadDictionaryArray(payload, "reward_entries", false, out var rewardEntryValues))
            return null;
        if (!TryReadBoolField(payload, "is_repeatable", out bool isRepeatableValue))
            return null;
        if (!TryGetStrictInt(payload, "danger_tier_override", out int dangerTierOverrideValue))
            return null;

        var questDef = new QuestDef
        {
            quest_id = questIdValue,
            display_name = displayNameValue,
            description = descriptionValue,
            provider_interaction_id = providerInteractionIdValue,
            tags = tagValues,
            accept_requirements = acceptRequirementValues,
            objective_defs = objectiveDefValues,
            reward_entries = rewardEntryValues,
            is_repeatable = isRepeatableValue,
            danger_tier_override = dangerTierOverrideValue,
            provider_kind = ReadStringName(payload, "provider_kind"),
            listing_channels = ReadStringNameArray(payload, "listing_channels"),
            listing_settlement_ids = ReadStringNameArray(payload, "listing_settlement_ids"),
            accept_dialogue_text = ReadString(payload, "accept_dialogue_text"),
            accept_feedback_success = ReadString(payload, "accept_feedback_success"),
            accept_feedback_failure = ReadString(payload, "accept_feedback_failure"),
            accept_confirmation_text = ReadString(payload, "accept_confirmation_text"),
        };
        return questDef.ValidateSchema().Count == 0 ? questDef : null;
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

    private static StringName ReadRequiredStringName(
        Godot.Collections.Dictionary payload,
        string key
    )
    {
        if (payload == null || !payload.ContainsKey(key))
            return "";
        if (!TryReadStringName(payload, key, out StringName parsedValue))
            return "";
        return parsedValue;
    }

    private static StringName ReadStringName(Godot.Collections.Dictionary payload, string key)
    {
        if (payload == null || !payload.ContainsKey(key))
            return "";
        Variant value = payload[key];
        return value.VariantType switch
        {
            Variant.Type.StringName => value.AsStringName(),
            Variant.Type.String => new StringName(value.AsString()),
            _ => new StringName(""),
        };
    }

    private static string ReadString(Godot.Collections.Dictionary payload, string key)
    {
        if (payload == null || !payload.ContainsKey(key))
            return "";
        Variant value = payload[key];
        return value.VariantType switch
        {
            Variant.Type.String => value.AsString(),
            Variant.Type.StringName => value.AsStringName().ToString(),
            _ => "",
        };
    }

    private static Godot.Collections.Array<StringName> ReadStringNameArray(
        Godot.Collections.Dictionary payload,
        string key
    )
    {
        var result = new Godot.Collections.Array<StringName>();
        if (!TryGetArray(payload, key, out Godot.Collections.Array source))
            return result;
        foreach (var rawValue in source)
        {
            if (!TryAsStringLike(rawValue, out string rawText))
            {
                result.Clear();
                return result;
            }
            string parsedText = rawText.StripEdges();
            if (parsedText.Length == 0)
            {
                result.Clear();
                return result;
            }
            result.Add(new StringName(parsedText));
        }
        return result;
    }

    private static Godot.Collections.Array<StringName> GetSupportedObjectiveTypes()
    {
        return new Godot.Collections.Array<StringName>
        {
            ObjectiveSubmitItem,
            ObjectiveDefeatEnemy,
            ObjectiveSettlementAction,
        };
    }

    private static Godot.Collections.Array<StringName> GetSupportedRewardTypes()
    {
        return new Godot.Collections.Array<StringName>
        {
            RewardGold,
            RewardItem,
            RewardPendingCharacterReward,
        };
    }

    private static Godot.Collections.Array<string> ValidatePendingCharacterReward(
        StringName questIdValue,
        RewardEntryData reward
    )
    {
        var errors = new Godot.Collections.Array<string>();
        string questIdText = (string)questIdValue;
        var memberId = reward.PendingRewardMemberId;
        if (memberId == "")
            errors.Add($"QuestDef {questIdText} 的 pending_character_reward 缺少 member_id。");

        if (reward.PendingRewardEntries.Count == 0)
        {
            errors.Add(
                $"QuestDef {questIdText} 的 pending_character_reward 至少需要一条 entries。"
            );
            return errors;
        }

        foreach (PendingRewardEntryData entry in reward.PendingRewardEntries)
        {
            if (!entry.IsDictionaryEntry)
            {
                errors.Add(
                    $"QuestDef {questIdText} 的 pending_character_reward 包含非 Dictionary entry。"
                );
                continue;
            }
            var entryType = entry.EntryType;
            var targetId = entry.TargetId;
            if (!entry.HasStrictAmount)
            {
                errors.Add(
                    $"QuestDef {questIdText} 的 pending_character_reward entry amount 必须是 int。"
                );
                continue;
            }
            int amount = entry.Amount;
            if (entryType == "")
            {
                errors.Add(
                    $"QuestDef {questIdText} 的 pending_character_reward entry 缺少 entry_type。"
                );
            }
            else if (!PendingCharacterRewardContentRules.IsSupportedEntryType(entryType))
            {
                errors.Add(
                    $"QuestDef {questIdText} has unsupported pending_character_reward entry_type {(string)entryType}. Supported: {PendingCharacterRewardContentRules.ValidEntryTypeLabel()}."
                );
            }
            if (targetId == "")
                errors.Add(
                    $"QuestDef {questIdText} 的 pending_character_reward entry 缺少 target_id。"
                );
            if (amount == 0)
                errors.Add(
                    $"QuestDef {questIdText} 的 pending_character_reward entry amount 不能为 0。"
                );
        }
        return errors;
    }

    private static IReadOnlyList<PendingRewardEntryData> CollectPendingRewardEntries(
        Godot.Collections.Dictionary rewardData
    )
    {
        var result = new System.Collections.Generic.List<PendingRewardEntryData>();
        if (!TryGetArray(rewardData, "entries", out Godot.Collections.Array entries))
            return result;
        foreach (var entryValue in entries)
        {
            if (!TryAsDictionary(entryValue, out Godot.Collections.Dictionary entryData))
            {
                result.Add(PendingRewardEntryData.InvalidNonDictionary());
                continue;
            }
            result.Add(PendingRewardEntryData.FromDictionary(entryData));
        }
        return result;
    }

    private static bool TryReadDictionaryArray(
        Godot.Collections.Dictionary payload,
        string key,
        bool requireNonEmpty,
        out Godot.Collections.Array<Godot.Collections.Dictionary> result
    )
    {
        result = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        if (payload == null || !payload.ContainsKey(key))
            return false;
        if (!TryGetArray(payload, key, out Godot.Collections.Array source))
            return false;
        if (requireNonEmpty && source.Count == 0)
            return false;
        foreach (var item in source)
        {
            if (!TryAsDictionary(item, out Godot.Collections.Dictionary itemData))
                return false;
            result.Add(itemData.Duplicate(true));
        }
        return true;
    }

    private static StringName DictStringName(Godot.Collections.Dictionary dict, string key)
    {
        return TryReadStringName(dict, key, out StringName value) ? value : new StringName("");
    }

    private static bool TryReadStringName(
        Godot.Collections.Dictionary payload,
        string key,
        out StringName value
    )
    {
        if (TryGetExactValue(payload, key, out object rawValue)
            && TryAsStringLike(rawValue, out string rawText))
        {
            string text = rawText.StripEdges();
            if (text.Length > 0)
            {
                value = new StringName(text);
                return value != "";
            }
        }
        value = "";
        return false;
    }

    private static bool TryGetStrictString(
        Godot.Collections.Dictionary payload,
        string key,
        out string value
    )
    {
        if (TryGetExactValue(payload, key, out object rawValue)
            && TryAsStrictString(rawValue, out value))
        {
            return true;
        }
        value = "";
        return false;
    }

    private static bool TryGetStrictInt(
        Godot.Collections.Dictionary payload,
        string key,
        out int value
    )
    {
        if (TryGetExactValue(payload, key, out object rawValue)
            && TryAsStrictInt(rawValue, out value))
        {
            return true;
        }
        value = 0;
        return false;
    }

    private static bool TryReadBoolField(
        Godot.Collections.Dictionary payload,
        string key,
        out bool value
    )
    {
        if (TryGetExactValue(payload, key, out object rawValue) && TryAsBool(rawValue, out value))
        {
            return true;
        }
        value = false;
        return false;
    }

    private static bool TryGetArray(
        Godot.Collections.Dictionary payload,
        string key,
        out Godot.Collections.Array value
    )
    {
        if (TryGetExactValue(payload, key, out object rawValue) && TryAsArray(rawValue, out value))
        {
            return true;
        }
        value = new Godot.Collections.Array();
        return false;
    }

    private static bool TryAsStringLike(object rawValue, out string value)
    {
        if (rawValue is Variant variant)
        {
            if (variant.VariantType == Variant.Type.String)
            {
                value = variant.AsString();
                return true;
            }
            if (variant.VariantType == Variant.Type.StringName)
            {
                value = variant.AsStringName().ToString();
                return true;
            }
            value = "";
            return false;
        }
        if (rawValue is string stringValue)
        {
            value = stringValue;
            return true;
        }
        if (rawValue is StringName stringNameValue)
        {
            value = stringNameValue.ToString();
            return true;
        }
        value = "";
        return false;
    }

    private static bool TryAsStrictString(object rawValue, out string value)
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

    private static bool TryAsStrictInt(object rawValue, out int value)
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

    private static bool TryAsBool(object rawValue, out bool value)
    {
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Bool)
        {
            value = variant.AsBool();
            return true;
        }
        if (rawValue is bool boolValue)
        {
            value = boolValue;
            return true;
        }
        value = false;
        return false;
    }

    private static bool TryAsArray(object rawValue, out Godot.Collections.Array value)
    {
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Array)
        {
            value = variant.AsGodotArray();
            return true;
        }
        if (rawValue is Godot.Collections.Array array)
        {
            value = array;
            return true;
        }
        value = new Godot.Collections.Array();
        return false;
    }

    private static bool TryAsDictionary(
        object rawValue,
        out Godot.Collections.Dictionary value
    )
    {
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Dictionary)
        {
            value = variant.AsGodotDictionary();
            return true;
        }
        if (rawValue is Godot.Collections.Dictionary dictionary)
        {
            value = dictionary;
            return true;
        }
        value = new Godot.Collections.Dictionary();
        return false;
    }

    private static bool TryGetExactValue(
        Godot.Collections.Dictionary payload,
        string key,
        out object value
    )
    {
        if (payload != null && payload.ContainsKey(key))
        {
            value = payload[key];
            return true;
        }
        value = null;
        return false;
    }
}
