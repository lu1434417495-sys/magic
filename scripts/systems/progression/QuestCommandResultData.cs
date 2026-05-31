using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

internal sealed class QuestSubmitItemResultData
{
    public readonly bool Ok;
    public readonly string ErrorCode;
    public readonly StringName ItemId;
    public readonly string ObjectiveId;
    public readonly int TargetValue;
    public readonly int RequiredQuantity;
    public readonly int SubmittedQuantity;
    private readonly GStringNameArray _acceptedQuestIds;
    private readonly GStringNameArray _progressedQuestIds;
    private readonly GStringNameArray _claimableQuestIds;
    private readonly GStringNameArray _completedQuestIds;

    private QuestSubmitItemResultData(
        bool ok,
        string errorCode,
        StringName itemId,
        string objectiveId,
        int targetValue,
        int requiredQuantity,
        int submittedQuantity,
        GStringNameArray acceptedQuestIds,
        GStringNameArray progressedQuestIds,
        GStringNameArray claimableQuestIds,
        GStringNameArray completedQuestIds
    )
    {
        Ok = ok;
        ErrorCode = errorCode ?? "";
        ItemId = ProgressionDataUtils.to_string_name(itemId);
        ObjectiveId = objectiveId ?? "";
        TargetValue = Mathf.Max(targetValue, 0);
        RequiredQuantity = Mathf.Max(requiredQuantity, 0);
        SubmittedQuantity = Mathf.Max(submittedQuantity, 0);
        _acceptedQuestIds = CloneStringNameArray(acceptedQuestIds);
        _progressedQuestIds = CloneStringNameArray(progressedQuestIds);
        _claimableQuestIds = CloneStringNameArray(claimableQuestIds);
        _completedQuestIds = CloneStringNameArray(completedQuestIds);
    }

    public bool ContainsClaimableQuest(StringName questId) =>
        ContainsStringName(_claimableQuestIds, questId);

    public GDictionary ToDictionary() =>
        new()
        {
            ["ok"] = Ok,
            ["error_code"] = ErrorCode,
            ["objective_id"] = ObjectiveId,
            ["item_id"] = ItemId.ToString(),
            ["target_value"] = TargetValue,
            ["required_quantity"] = RequiredQuantity,
            ["submitted_quantity"] = SubmittedQuantity,
            ["accepted_quest_ids"] = CloneStringNameArray(_acceptedQuestIds),
            ["progressed_quest_ids"] = CloneStringNameArray(_progressedQuestIds),
            ["claimable_quest_ids"] = CloneStringNameArray(_claimableQuestIds),
            ["completed_quest_ids"] = CloneStringNameArray(_completedQuestIds),
        };

    public static QuestSubmitItemResultData Success(
        StringName itemId,
        string objectiveId,
        int targetValue,
        int requiredQuantity,
        int submittedQuantity,
        GStringNameArray acceptedQuestIds,
        GStringNameArray progressedQuestIds,
        GStringNameArray claimableQuestIds,
        GStringNameArray completedQuestIds
    ) =>
        new(
            true,
            "",
            itemId,
            objectiveId,
            targetValue,
            requiredQuantity,
            submittedQuantity,
            acceptedQuestIds,
            progressedQuestIds,
            claimableQuestIds,
            completedQuestIds
        );

    public static QuestSubmitItemResultData Failed(
        string errorCode,
        string objectiveId = "",
        StringName itemId = default,
        int targetValue = 0,
        int requiredQuantity = 0
    ) =>
        new(
            false,
            errorCode,
            itemId,
            objectiveId,
            targetValue,
            requiredQuantity,
            0,
            new GStringNameArray(),
            new GStringNameArray(),
            new GStringNameArray(),
            new GStringNameArray()
        );

    private static bool ContainsStringName(GStringNameArray values, StringName target)
    {
        if (values == null || target == "")
            return false;
        foreach (var value in values)
        {
            if (value == target)
                return true;
        }
        return false;
    }

    private static GStringNameArray CloneStringNameArray(GStringNameArray values)
    {
        var result = new GStringNameArray();
        if (values == null)
            return result;
        foreach (var value in values)
            result.Add(value);
        return result;
    }
}

internal sealed class QuestClaimResultData
{
    public readonly bool Ok;
    public readonly string ErrorCode;
    public readonly int GoldDelta;
    private readonly GArray _itemRewards;
    private readonly GArray _pendingCharacterRewards;
    private readonly GStringNameArray _unsupportedRewardTypes;

    private QuestClaimResultData(
        bool ok,
        string errorCode,
        int goldDelta,
        GArray itemRewards,
        GArray pendingCharacterRewards,
        GStringNameArray unsupportedRewardTypes
    )
    {
        Ok = ok;
        ErrorCode = errorCode ?? "";
        GoldDelta = Mathf.Max(goldDelta, 0);
        _itemRewards = itemRewards != null ? itemRewards.Duplicate(true) : new GArray();
        _pendingCharacterRewards =
            pendingCharacterRewards != null ? pendingCharacterRewards.Duplicate(true) : new GArray();
        _unsupportedRewardTypes = CloneStringNameArray(unsupportedRewardTypes);
    }

    public GArray CloneItemRewards() => _itemRewards.Duplicate(true);

    public GArray ClonePendingCharacterRewards() => _pendingCharacterRewards.Duplicate(true);

    public GStringNameArray CloneUnsupportedRewardTypes() =>
        CloneStringNameArray(_unsupportedRewardTypes);

    public GDictionary ToDictionary() =>
        new()
        {
            ["ok"] = Ok,
            ["error_code"] = ErrorCode,
            ["gold_delta"] = GoldDelta,
            ["item_rewards"] = CloneItemRewards(),
            ["pending_character_rewards"] = ClonePendingCharacterRewards(),
            ["unsupported_reward_types"] = CloneStringNameArray(_unsupportedRewardTypes),
        };

    public string BuildRewardSummaryText()
    {
        var rewardParts = new List<string>();
        if (GoldDelta > 0)
            rewardParts.Add($"{GoldDelta} 金");
        foreach (GDictionary rewardData in ReadDictionaryItems(_itemRewards))
        {
            int quantity = ReadInt(rewardData, "quantity");
            string label = ReadTrimmedString(rewardData, "display_name");
            if (quantity <= 0 || label.Length == 0)
                continue;
            rewardParts.Add($"{label} x{quantity}");
        }
        foreach (GDictionary rewardData in ReadDictionaryItems(_pendingCharacterRewards))
        {
            string memberName = ReadTrimmedString(rewardData, "member_name");
            rewardParts.Add(memberName.Length > 0 ? $"{memberName}的角色奖励" : "角色奖励");
        }
        return string.Join("、", rewardParts);
    }

    public static QuestClaimResultData Success(
        int goldDelta,
        GArray itemRewards,
        GArray pendingCharacterRewards
    ) =>
        new(
            true,
            "",
            goldDelta,
            itemRewards,
            pendingCharacterRewards,
            new GStringNameArray()
        );

    public static QuestClaimResultData Failed(
        string errorCode,
        GStringNameArray unsupportedRewardTypes = null
    ) =>
        new(
            false,
            errorCode,
            0,
            new GArray(),
            new GArray(),
            unsupportedRewardTypes
        );

    private static GStringNameArray CloneStringNameArray(GStringNameArray values)
    {
        var result = new GStringNameArray();
        if (values == null)
            return result;
        foreach (var value in values)
            result.Add(value);
        return result;
    }

    private static IEnumerable<GDictionary> ReadDictionaryItems(GArray values)
    {
        if (values == null)
            yield break;
        foreach (Variant value in values)
        {
            if (value.VariantType == Variant.Type.Dictionary)
                yield return value.AsGodotDictionary();
        }
    }

    private static int ReadInt(GDictionary data, string key)
    {
        if (data == null || !data.ContainsKey(key))
            return 0;
        Variant value = data[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : 0;
    }

    private static string ReadTrimmedString(GDictionary data, string key)
    {
        if (data == null || !data.ContainsKey(key))
            return "";
        Variant value = data[key];
        return value.VariantType switch
        {
            Variant.Type.String => value.AsString().Trim(),
            Variant.Type.StringName => value.AsStringName().ToString().Trim(),
            _ => "",
        };
    }
}
