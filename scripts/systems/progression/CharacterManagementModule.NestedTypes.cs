using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

// Partial slice of CharacterManagementModule — nested private DTO/reward/quest preview data types + CharacterQuestDataReader.
// Pure physical split: same class, no behavior change. See CharacterManagementModule.cs.
public sealed partial class CharacterManagementModule
{

    private sealed class PendingCharacterRewardEntryData
    {
        public readonly bool Exists;
        public readonly StringName EntryType;
        public readonly StringName TargetId;
        public readonly int Amount;
        public readonly string TargetLabel;
        public readonly string ReasonText;
        public readonly StringName SourceType;
        public readonly StringName MasterySourceType;

        private PendingCharacterRewardEntryData(
            bool exists,
            StringName entryType,
            StringName targetId,
            int amount,
            string targetLabel,
            string reasonText,
            StringName sourceType,
            StringName masterySourceType
        )
        {
            Exists = exists;
            EntryType = entryType;
            TargetId = targetId;
            Amount = amount;
            TargetLabel = targetLabel ?? "";
            ReasonText = reasonText ?? "";
            SourceType = sourceType;
            MasterySourceType = masterySourceType != "" ? masterySourceType : sourceType;
        }

        public static PendingCharacterRewardEntryData FromDictionary(
            GDictionary data,
            StringName defaultEntryType = default,
            StringName defaultSourceType = default
        )
        {
            if (data == null || data.Count == 0)
                return Missing();
            var entryType = CharacterQuestDataReader.ReadStringName(data, "entry_type");
            if (entryType == "")
                entryType = defaultEntryType;
            CharacterQuestDataReader.TryReadInt(data, "amount", out var amount);
            var sourceType = CharacterQuestDataReader.ReadStringName(data, "source_type");
            if (sourceType == "")
                sourceType = defaultSourceType;
            var masterySourceType = CharacterQuestDataReader.ReadStringName(
                data,
                "mastery_source_type"
            );
            if (masterySourceType == "")
                masterySourceType = sourceType;
            return new PendingCharacterRewardEntryData(
                true,
                entryType,
                CharacterQuestDataReader.ReadStringName(data, "target_id"),
                amount,
                CharacterQuestDataReader.ReadString(data, "target_label"),
                CharacterQuestDataReader.ReadString(data, "reason_text"),
                sourceType,
                masterySourceType
            );
        }

        public static PendingCharacterRewardEntryData FromEntry(
            PendingCharacterRewardEntry entry,
            StringName defaultEntryType,
            StringName defaultSourceType
        )
        {
            if (entry == null)
                return Missing();
            var entryType = entry.entry_type != "" ? entry.entry_type : defaultEntryType;
            return new PendingCharacterRewardEntryData(
                true,
                entryType,
                entry.target_id,
                entry.amount,
                entry.target_label,
                entry.reason_text,
                defaultSourceType,
                entry.mastery_source_type != "" ? entry.mastery_source_type : defaultSourceType
            );
        }

        private static PendingCharacterRewardEntryData Missing() =>
            new(false, "", "", 0, "", "", "", "");

        public PendingCharacterRewardEntry ToRewardEntry()
        {
            if (!Exists)
                return null;
            return new PendingCharacterRewardEntry
            {
                entry_type = EntryType,
                target_id = TargetId,
                target_label = TargetLabel,
                amount = Amount,
                reason_text = ReasonText,
            };
        }
    }

    private static string _resolve_quest_reward_warehouse_error_code(string warehouse_error_code) =>
        warehouse_error_code == "warehouse_blocked_swap"
            ? "reward_overflow"
            : "quest_reward_commit_failed";

    private sealed class QuestSubmitItemPreviewData
    {
        public readonly bool Ok;
        public readonly string ErrorCode;
        public readonly StringName ObjectiveId;
        public readonly StringName ItemId;
        public readonly int TargetValue;
        public readonly int RequiredQuantity;

        private QuestSubmitItemPreviewData(
            bool ok,
            string errorCode,
            StringName objectiveId,
            StringName itemId,
            int targetValue,
            int requiredQuantity
        )
        {
            Ok = ok;
            ErrorCode = errorCode ?? "";
            ObjectiveId = objectiveId;
            ItemId = itemId;
            TargetValue = Mathf.Max(targetValue, 0);
            RequiredQuantity = Mathf.Max(requiredQuantity, 0);
        }

        public static QuestSubmitItemPreviewData Success(
            StringName objectiveId,
            StringName itemId,
            int targetValue,
            int requiredQuantity
        ) =>
            new(true, "", objectiveId, itemId, targetValue, requiredQuantity);

        public static QuestSubmitItemPreviewData Failed(
            string errorCode,
            StringName objectiveId = default,
            StringName itemId = default,
            int targetValue = 0,
            int requiredQuantity = 0
        ) =>
            new(false, errorCode, objectiveId, itemId, targetValue, requiredQuantity);
    }

    private sealed class QuestObjectiveDefData
    {
        public readonly bool Exists;
        public readonly StringName ObjectiveId;
        public readonly StringName ObjectiveType;
        public readonly StringName TargetId;
        public readonly int TargetValue;

        private QuestObjectiveDefData(
            bool exists,
            StringName objectiveId,
            StringName objectiveType,
            StringName targetId,
            int targetValue
        )
        {
            Exists = exists;
            ObjectiveId = objectiveId;
            ObjectiveType = objectiveType;
            TargetId = targetId;
            TargetValue = Mathf.Max(targetValue, 0);
        }

        public static QuestObjectiveDefData FromVariant(Variant value)
        {
            if (value.VariantType != Variant.Type.Dictionary)
                return Empty();
            return FromDictionary(value.AsGodotDictionary());
        }

        public static QuestObjectiveDefData FromDictionary(GDictionary data)
        {
            if (data == null || data.Count == 0)
                return Empty();
            return new QuestObjectiveDefData(
                true,
                CharacterQuestDataReader.ReadStringName(data, "objective_id"),
                CharacterQuestDataReader.ReadStringName(data, "objective_type"),
                CharacterQuestDataReader.ReadStringName(data, "target_id"),
                CharacterQuestDataReader.TryReadInt(data, "target_value", out var targetValue)
                    ? targetValue
                    : 0
            );
        }

        public static QuestObjectiveDefData FromQuestObjectiveEntry(
            QuestDef.ObjectiveEntryData entry
        )
        {
            if (entry == null)
                return Empty();
            return new QuestObjectiveDefData(
                true,
                entry.ObjectiveId,
                entry.ObjectiveType,
                entry.TargetId,
                entry.HasStrictTargetValue ? entry.TargetValue : 0
            );
        }

        private static QuestObjectiveDefData Empty() => new(false, "", "", "", 0);
    }

    private sealed class QuestRewardData
    {
        public readonly bool Found;
        public readonly string ErrorCode;
        public readonly string DisplayName;
        public readonly IReadOnlyList<QuestRewardEntryData> RewardEntries;

        private QuestRewardData(
            bool found,
            string errorCode,
            string displayName,
            IReadOnlyList<QuestRewardEntryData> rewardEntries
        )
        {
            Found = found;
            ErrorCode = errorCode ?? "";
            DisplayName = displayName ?? "";
            RewardEntries = rewardEntries ?? new List<QuestRewardEntryData>();
        }

        public static QuestRewardData Missing() =>
            new(false, "quest_def_missing", "", new List<QuestRewardEntryData>());

        public static QuestRewardData FromDictionary(GDictionary questData)
        {
            string displayName = CharacterQuestDataReader.ReadTrimmedString(
                questData,
                "display_name"
            );
            string errorCode = displayName.Length == 0 ? "invalid_quest_display_name" : "";
            return new QuestRewardData(
                true,
                errorCode,
                displayName,
                QuestRewardEntryData.FromArray(
                    CharacterQuestDataReader.ReadArray(questData, "reward_entries")
                )
            );
        }

        public static QuestRewardData FromQuestDef(QuestDef questDef)
        {
            if (questDef == null)
                return Missing();
            string displayName = (questDef.display_name ?? "").StripEdges();
            string errorCode = displayName.Length == 0 ? "invalid_quest_display_name" : "";
            var rewardEntries = new List<QuestRewardEntryData>();
            foreach (QuestDef.RewardEntryData rewardEntry in questDef.GetRewardEntriesTyped())
                rewardEntries.Add(QuestRewardEntryData.FromQuestRewardEntry(rewardEntry));
            return new QuestRewardData(true, errorCode, displayName, rewardEntries);
        }
    }

    private sealed class QuestRewardEntryData
    {
        public readonly bool Exists;
        public readonly StringName RewardType;
        public readonly int Amount;
        public readonly StringName ItemId;
        public readonly int Quantity;
        public readonly StringName MemberId;
        public readonly StringName SourceType;
        public readonly StringName SourceId;
        public readonly string SourceLabel;
        public readonly string SummaryText;
        public readonly StringName RewardId;
        private readonly List<PendingCharacterRewardEntry> _entries;

        private QuestRewardEntryData(
            bool exists,
            StringName rewardType,
            int amount,
            StringName itemId,
            int quantity,
            StringName memberId,
            StringName sourceType,
            StringName sourceId,
            string sourceLabel,
            string summaryText,
            StringName rewardId,
            IEnumerable<PendingCharacterRewardEntry> entries
        )
        {
            Exists = exists;
            RewardType = rewardType;
            Amount = Mathf.Max(amount, 0);
            ItemId = itemId;
            Quantity = Mathf.Max(quantity, 0);
            MemberId = memberId;
            SourceType = sourceType;
            SourceId = sourceId;
            SourceLabel = sourceLabel ?? "";
            SummaryText = summaryText ?? "";
            RewardId = rewardId;
            _entries = ClonePendingCharacterRewardEntryList(entries);
        }

        internal List<PendingCharacterRewardEntry> CloneEntries() =>
            DuplicatePendingCharacterRewardEntries(_entries);

        public static IReadOnlyList<QuestRewardEntryData> FromArray(GArray rewardEntries)
        {
            var result = new List<QuestRewardEntryData>();
            if (rewardEntries == null)
                return result;
            foreach (Variant rewardEntry in rewardEntries)
                result.Add(FromVariant(rewardEntry));
            return result;
        }

        public static QuestRewardEntryData FromVariant(Variant value)
        {
            if (value.VariantType != Variant.Type.Dictionary)
                return Missing();
            return FromDictionary(value.AsGodotDictionary());
        }

        public static QuestRewardEntryData FromDictionary(GDictionary data)
        {
            if (data == null || data.Count == 0)
                return Missing();
            CharacterQuestDataReader.TryReadInt(data, "amount", out var amount);
            CharacterQuestDataReader.TryReadInt(data, "quantity", out var quantity);
            return new QuestRewardEntryData(
                true,
                CharacterQuestDataReader.ReadStringName(data, "reward_type"),
                amount,
                CharacterQuestDataReader.ReadStringName(data, "item_id"),
                quantity,
                CharacterQuestDataReader.ReadStringName(data, "member_id"),
                CharacterQuestDataReader.ReadStringName(data, "source_type"),
                CharacterQuestDataReader.ReadStringName(data, "source_id"),
                CharacterQuestDataReader.ReadTrimmedString(data, "source_label"),
                CharacterQuestDataReader.ReadTrimmedString(data, "summary_text"),
                CharacterQuestDataReader.ReadStringName(data, "reward_id"),
                ProjectPendingRewardEntryDictionaries(
                    CharacterQuestDataReader.ReadArray(data, "entries")
                )
            );
        }

        public static QuestRewardEntryData FromQuestRewardEntry(QuestDef.RewardEntryData entry)
        {
            if (entry == null)
                return Missing();
            return new QuestRewardEntryData(
                true,
                entry.RewardType,
                entry.HasStrictGoldAmount ? entry.GoldAmount : 0,
                entry.ItemId,
                entry.HasStrictItemQuantity ? entry.ItemQuantity : 0,
                entry.PendingRewardMemberId,
                "",
                "",
                "",
                "",
                "",
                ProjectPendingRewardEntries(entry.PendingRewardEntries)
            );
        }

        private static QuestRewardEntryData Missing() =>
            new(false, "", 0, "", 0, "", "", "", "", "", "", new List<PendingCharacterRewardEntry>());

        private static List<PendingCharacterRewardEntry> ProjectPendingRewardEntries(
            IReadOnlyList<QuestDef.PendingRewardEntryData> entries
        )
        {
            var result = new List<PendingCharacterRewardEntry>();
            if (entries == null)
                return result;
            foreach (QuestDef.PendingRewardEntryData entry in entries)
            {
                if (entry == null || !entry.IsDictionaryEntry)
                    continue;
                result.Add(
                    new PendingCharacterRewardEntry
                    {
                        entry_type = entry.EntryType,
                        target_id = entry.TargetId,
                        amount = entry.HasStrictAmount ? entry.Amount : 0,
                    }
                );
            }
            return result;
        }

        private static List<PendingCharacterRewardEntry> ProjectPendingRewardEntryDictionaries(
            GArray entries
        )
        {
            var result = new List<PendingCharacterRewardEntry>();
            if (entries == null)
                return result;
            foreach (Variant entryValue in entries)
            {
                if (!entryValue.TryAsDictionary(out GDictionary entryData))
                    continue;
                PendingCharacterRewardEntryData entry = PendingCharacterRewardEntryData.FromDictionary(
                    entryData
                );
                PendingCharacterRewardEntry rewardEntry = entry.ToRewardEntry();
                if (rewardEntry != null)
                    result.Add(rewardEntry);
            }
            return result;
        }
    }

    private sealed class QuestRewardPreviewData
    {
        public readonly bool Ok;
        public readonly string ErrorCode;
        public readonly int GoldDelta;
        private readonly List<Dictionary<string, object>> _itemRewards;
        private readonly List<StringName> _warehouseDepositItemIds;
        private readonly List<PendingCharacterReward> _pendingCharacterRewards;
        private readonly List<StringName> _unsupportedRewardTypes;

        private QuestRewardPreviewData(
            bool ok,
            string errorCode,
            int goldDelta,
            GArray itemRewards,
            IReadOnlyList<StringName> warehouseDepositItemIds,
            IEnumerable<PendingCharacterReward> pendingCharacterRewards,
            GStringNameArray unsupportedRewardTypes
        )
        {
            Ok = ok;
            ErrorCode = errorCode ?? "";
            GoldDelta = Mathf.Max(goldDelta, 0);
            _itemRewards = RuntimePlainPayload.NormalizeDictionaryArray(
                itemRewards,
                "CharacterManagementModule.QuestRewardPreviewData.item_rewards"
            );
            _warehouseDepositItemIds =
                warehouseDepositItemIds != null
                    ? CloneStringNameList(warehouseDepositItemIds)
                    : new List<StringName>();
            _pendingCharacterRewards = DuplicatePendingCharacterRewards(pendingCharacterRewards);
            _unsupportedRewardTypes =
                unsupportedRewardTypes != null
                    ? CloneStringNameList(unsupportedRewardTypes)
                    : new List<StringName>();
        }

        internal GArray CloneItemRewards() =>
            RuntimePlainPayload.ProjectDictionaryArray(
                _itemRewards,
                "CharacterManagementModule.QuestRewardPreviewData.CloneItemRewards"
            );

        public List<StringName> CloneWarehouseDepositItemIds() =>
            CloneStringNameList(_warehouseDepositItemIds);

        internal List<PendingCharacterReward> ClonePendingCharacterRewards() =>
            DuplicatePendingCharacterRewards(_pendingCharacterRewards);

        public GStringNameArray CloneUnsupportedRewardTypes() =>
            ToStringNameArray(_unsupportedRewardTypes);

        public static QuestRewardPreviewData Success(
            int goldDelta,
            GArray itemRewards,
            IReadOnlyList<StringName> warehouseDepositItemIds,
            IEnumerable<PendingCharacterReward> pendingCharacterRewards
        ) =>
            new(
                true,
                "",
                goldDelta,
                itemRewards,
                warehouseDepositItemIds,
                pendingCharacterRewards,
                new GStringNameArray()
            );

        public static QuestRewardPreviewData Failed(
            string errorCode,
            GStringNameArray unsupportedRewardTypes = null
        ) =>
            new(
                false,
                errorCode,
                0,
                new GArray(),
                new GStringNameArray(),
                new List<PendingCharacterReward>(),
                unsupportedRewardTypes
            );

        private static List<PendingCharacterReward> DuplicatePendingCharacterRewards(
            IEnumerable<PendingCharacterReward> rewards
        )
        {
            var result = new List<PendingCharacterReward>();
            if (rewards == null)
                return result;
            foreach (PendingCharacterReward reward in rewards)
                if (reward != null && !reward.IsEmpty())
                    result.Add(reward.DuplicateState());
            return result;
        }
    }

    private sealed class QuestItemRewardPreviewData
    {
        public readonly bool Ok;
        public readonly string ErrorCode;
        private readonly StringName _itemId;
        private readonly string _displayName;
        private readonly int _quantity;
        private readonly List<StringName> _warehouseDepositItemIds;

        private QuestItemRewardPreviewData(
            bool ok,
            string errorCode,
            StringName itemId,
            string displayName,
            int quantity,
            IReadOnlyList<StringName> warehouseDepositItemIds
        )
        {
            Ok = ok;
            ErrorCode = ok
                ? ""
                : string.IsNullOrEmpty(errorCode)
                    ? "invalid_item_reward"
                    : errorCode;
            _itemId = itemId;
            _displayName = displayName ?? "";
            _quantity = Mathf.Max(quantity, 0);
            _warehouseDepositItemIds =
                warehouseDepositItemIds != null
                    ? CloneStringNameList(warehouseDepositItemIds)
                    : new List<StringName>();
        }

        internal GDictionary CloneItemReward()
        {
            if (_itemId == "" || _displayName.Length == 0 || _quantity <= 0)
                return new GDictionary();

            return new GDictionary
            {
                ["item_id"] = _itemId.ToString(),
                ["display_name"] = _displayName,
                ["quantity"] = _quantity,
            };
        }

        public List<StringName> CloneWarehouseDepositItemIds() =>
            CloneStringNameList(_warehouseDepositItemIds);

        public static QuestItemRewardPreviewData Success(
            StringName itemId,
            string displayName,
            int quantity,
            IReadOnlyList<StringName> warehouseDepositItemIds
        ) =>
            new(true, "", itemId, displayName, quantity, warehouseDepositItemIds);

        public static QuestItemRewardPreviewData Failed(string errorCode) =>
            new(false, errorCode, "", "", 0, new List<StringName>());
    }

    private sealed class QuestPendingCharacterRewardPreviewData
    {
        public readonly bool Ok;
        public readonly string ErrorCode;
        public readonly PendingCharacterReward PendingReward;

        private QuestPendingCharacterRewardPreviewData(
            bool ok,
            string errorCode,
            PendingCharacterReward pendingReward
        )
        {
            Ok = ok;
            ErrorCode = ok
                ? ""
                : string.IsNullOrEmpty(errorCode)
                    ? "invalid_pending_character_reward"
                    : errorCode;
            PendingReward = pendingReward;
        }

        public static QuestPendingCharacterRewardPreviewData Success(
            PendingCharacterReward pendingReward
        ) =>
            new(true, "", pendingReward);

        public static QuestPendingCharacterRewardPreviewData Failed(string errorCode) =>
            new(false, errorCode, null);
    }

    private static class CharacterQuestDataReader
    {
        internal static string ReadString(GDictionary data, string key)
        {
            if (!TryGet(data, key, out Variant value))
                return "";
            return value.VariantType switch
            {
                Variant.Type.String => value.AsString(),
                Variant.Type.StringName => value.AsStringName().ToString(),
                _ => "",
            };
        }

        internal static bool TryReadString(GDictionary data, string key, out string result)
        {
            if (!TryGet(data, key, out Variant value))
            {
                result = "";
                return false;
            }
            if (value.VariantType == Variant.Type.String)
            {
                result = value.AsString();
                return true;
            }
            if (value.VariantType == Variant.Type.StringName)
            {
                result = value.AsStringName().ToString();
                return true;
            }
            result = "";
            return false;
        }

        internal static string ReadTrimmedString(GDictionary data, string key) =>
            ReadString(data, key).StripEdges();

        internal static StringName ReadStringName(GDictionary data, string key)
        {
            if (!TryGet(data, key, out Variant value))
                return "";
            return value.VariantType switch
            {
                Variant.Type.StringName => value.AsStringName(),
                Variant.Type.String => new StringName(value.AsString().StripEdges()),
                _ => new StringName(""),
            };
        }

        internal static bool TryReadInt(GDictionary data, string key, out int result)
        {
            if (!TryGet(data, key, out Variant value) || value.VariantType != Variant.Type.Int)
            {
                result = 0;
                return false;
            }
            result = value.AsInt32();
            return true;
        }

        internal static GArray ReadArray(GDictionary data, string key)
        {
            if (!TryGet(data, key, out Variant value))
                return new GArray();
            return value.VariantType == Variant.Type.Array ? value.AsGodotArray() : new GArray();
        }

        internal static GStringNameArray ReadStringNameArray(GDictionary data, string key)
        {
            GStringNameArray result = new();
            foreach (Variant value in ReadArray(data, key))
            {
                if (value.VariantType == Variant.Type.StringName)
                    result.Add(value.AsStringName());
                else if (value.VariantType == Variant.Type.String)
                    result.Add(new StringName(value.AsString()));
            }
            return result;
        }

        private static bool TryGet(GDictionary data, string key, out Variant value)
        {
            if (data == null || string.IsNullOrEmpty(key))
            {
                value = default;
                return false;
            }
            foreach (Variant rawKey in data.Keys)
            {
                if (rawKey.VariantType == Variant.Type.String && rawKey.AsString() == key)
                {
                    value = data[rawKey];
                    return true;
                }
                if (
                    rawKey.VariantType == Variant.Type.StringName
                    && rawKey.AsStringName().ToString() == key
                )
                {
                    value = data[rawKey];
                    return true;
                }
            }
            value = default;
            return false;
        }
    }

    private GArray _pending_character_rewards_to_dicts(
        IEnumerable<PendingCharacterReward> rewards
    )
    {
        var reward_dicts = new GArray();
        if (rewards == null)
            return reward_dicts;
        foreach (PendingCharacterReward reward in rewards)
        {
            if (reward == null || reward.IsEmpty())
                continue;
            reward_dicts.Add(PendingCharacterRewardPayload.Project(reward));
        }
        return reward_dicts;
    }

    private List<PendingCharacterRewardEntry> _normalize_pending_character_entries(
        IEnumerable<PendingCharacterRewardEntry> entry_options
    )
    {
        var entries = new List<PendingCharacterRewardEntry>();
        if (entry_options == null)
            return entries;
        foreach (PendingCharacterRewardEntry entry_option in entry_options)
        {
            PendingCharacterRewardEntry entry =
                _normalize_pending_character_entry(
                    PendingCharacterRewardEntryData.FromEntry(entry_option, default, default)
                );
            if (entry != null && !entry.IsEmpty())
                entries.Add(entry);
        }
        return entries;
    }

    private bool _has_unsupported_pending_character_entry_type(
        IEnumerable<PendingCharacterRewardEntry> entry_options
    )
    {
        if (entry_options == null)
            return false;
        foreach (PendingCharacterRewardEntry entry_option in entry_options)
        {
            PendingCharacterRewardEntryData entry_data =
                PendingCharacterRewardEntryData.FromEntry(entry_option, default, default);
            if (
                entry_data.Exists
                && _is_unsupported_pending_character_entry(
                    entry_data.EntryType,
                    entry_data.TargetId
                )
            )
                return true;
        }
        return false;
    }

    private bool _has_unsupported_pending_character_entry_object(
        IEnumerable<PendingCharacterRewardEntry> entries
    )
    {
        foreach (var entry in entries)
            if (
                entry != null
                && _is_unsupported_pending_character_entry(entry.entry_type, entry.target_id)
            )
                return true;
        return false;
    }

    private static bool _is_unsupported_pending_character_entry(
        StringName entry_type,
        StringName target_id
    )
    {
        if (entry_type == "")
            return false;
        if (!PendingCharacterRewardContentRules.IsSupportedEntryType(entry_type))
            return true;
        if (
            PendingCharacterRewardContentRules.IsAttributeProgressEntry(entry_type)
            && !PendingCharacterRewardContentRules.IsValidAttributeProgressTarget(target_id)
        )
            return true;
        return false;
    }

    private PendingCharacterRewardEntry _normalize_pending_character_entry(
        PendingCharacterRewardEntryData entry_data
    )
    {
        if (entry_data == null || !entry_data.Exists)
            return null;
        var entry_type = entry_data.EntryType;
        var target_id = entry_data.TargetId;
        var amount = entry_data.Amount;
        if (entry_type == "" || target_id == "" || amount == 0)
            return null;
        if (!PendingCharacterRewardContentRules.IsSupportedEntryType(entry_type))
            return null;
        if (
            PendingCharacterRewardContentRules.IsAttributeProgressEntry(entry_type)
            && !PendingCharacterRewardContentRules.IsValidAttributeProgressTarget(target_id)
        )
            return null;
        var entry = new PendingCharacterRewardEntry
        {
            entry_type = entry_type,
            target_id = target_id,
            amount = amount,
            target_label = entry_data.TargetLabel,
            reason_text = entry_data.ReasonText,
        };
        if (string.IsNullOrEmpty(entry.target_label))
            entry.target_label = _resolve_reward_target_label(entry.entry_type, entry.target_id, "");
        return entry;
    }

    private PendingCharacterReward _build_achievement_pending_reward(
        PartyMemberState member_state,
        AchievementDef achievement_def,
        GDictionary meta
    )
    {
        if (member_state == null || achievement_def == null)
            return null;
        var reward = new PendingCharacterReward
        {
            reward_id = _build_reward_id(member_state.member_id, achievement_def.achievement_id),
            member_id = member_state.member_id,
            member_name = !string.IsNullOrEmpty(member_state.display_name)
                ? member_state.display_name
                : (string)member_state.member_id,
            source_type = RewardTypeAchievement,
            source_id = achievement_def.achievement_id,
            source_label = !string.IsNullOrEmpty(achievement_def.display_name)
                ? achievement_def.display_name
                : (string)achievement_def.achievement_id,
            summary_text = CharacterQuestDataReader.TryReadString(
                meta,
                "summary_text",
                out var summary_text
            )
                ? summary_text
                : achievement_def.description,
            entries = _build_achievement_reward_entries(achievement_def),
        };
        return reward.IsEmpty() ? null : reward;
    }

    private List<PendingCharacterRewardEntry> _build_achievement_reward_entries(
        AchievementDef achievement_def
    )
    {
        var entries = new List<PendingCharacterRewardEntry>();
        if (achievement_def == null)
            return entries;
        foreach (AchievementRewardDef reward_def in achievement_def.rewards)
        {
            if (reward_def == null || reward_def.IsEmpty())
                continue;
            if (!PendingCharacterRewardContentRules.IsSupportedEntryType(reward_def.reward_type))
            {
                GameLog.Error(
                    $"Achievement {(string)achievement_def.achievement_id} has unsupported pending reward entry_type {(string)reward_def.reward_type}.",
                    "progression.reward.unsupported_type",
                    "progression"
                );
                return new List<PendingCharacterRewardEntry>();
            }
            var entry = new PendingCharacterRewardEntry
            {
                entry_type = reward_def.reward_type,
                target_id = reward_def.target_id,
                target_label = _resolve_reward_target_label(
                    reward_def.reward_type,
                    reward_def.target_id,
                    reward_def.target_label
                ),
                amount = reward_def.amount,
                reason_text = !string.IsNullOrEmpty(reward_def.reason_text)
                    ? reward_def.reason_text
                    : achievement_def.display_name,
            };
            if (!entry.IsEmpty())
                entries.Add(entry);
        }
        return entries;
    }

    private List<AchievementDef> _get_matching_achievement_defs(
        StringName event_type,
        StringName subject_id
    )
    {
        var matches = new List<AchievementDef>();
        foreach (StringName achievement_id in SortedContentKeys(_achievement_def_index))
        {
            var achievement_def = GetAchievementDef(achievement_id);
            if (achievement_def != null && achievement_def.MatchesEvent(event_type, subject_id))
                matches.Add(achievement_def);
        }
        return matches;
    }
}
