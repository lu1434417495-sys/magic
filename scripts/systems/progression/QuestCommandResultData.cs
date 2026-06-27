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
    private readonly List<StringName> _acceptedQuestIds;
    private readonly List<StringName> _progressedQuestIds;
    private readonly List<StringName> _claimableQuestIds;
    private readonly List<StringName> _completedQuestIds;

    private QuestSubmitItemResultData(
        bool ok,
        string errorCode,
        StringName itemId,
        string objectiveId,
        int targetValue,
        int requiredQuantity,
        int submittedQuantity,
        IEnumerable<StringName> acceptedQuestIds,
        IEnumerable<StringName> progressedQuestIds,
        IEnumerable<StringName> claimableQuestIds,
        IEnumerable<StringName> completedQuestIds
    )
    {
        Ok = ok;
        ErrorCode = errorCode ?? "";
        ItemId = ProgressionDataUtils.to_string_name(itemId);
        ObjectiveId = objectiveId ?? "";
        TargetValue = Mathf.Max(targetValue, 0);
        RequiredQuantity = Mathf.Max(requiredQuantity, 0);
        SubmittedQuantity = Mathf.Max(submittedQuantity, 0);
        _acceptedQuestIds = CloneStringNameList(acceptedQuestIds);
        _progressedQuestIds = CloneStringNameList(progressedQuestIds);
        _claimableQuestIds = CloneStringNameList(claimableQuestIds);
        _completedQuestIds = CloneStringNameList(completedQuestIds);
    }

    public bool ContainsClaimableQuest(StringName questId) =>
        ContainsStringName(_claimableQuestIds, questId);

    internal GStringNameArray CloneAcceptedQuestIds() => ToStringNameArray(_acceptedQuestIds);

    internal GStringNameArray CloneProgressedQuestIds() => ToStringNameArray(_progressedQuestIds);

    internal GStringNameArray CloneClaimableQuestIds() => ToStringNameArray(_claimableQuestIds);

    internal GStringNameArray CloneCompletedQuestIds() => ToStringNameArray(_completedQuestIds);

    public static QuestSubmitItemResultData Success(
        StringName itemId,
        string objectiveId,
        int targetValue,
        int requiredQuantity,
        int submittedQuantity,
        IEnumerable<StringName> acceptedQuestIds,
        IEnumerable<StringName> progressedQuestIds,
        IEnumerable<StringName> claimableQuestIds,
        IEnumerable<StringName> completedQuestIds
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

    private static bool ContainsStringName(List<StringName> values, StringName target)
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

    private static List<StringName> CloneStringNameList(IEnumerable<StringName> values)
    {
        var result = new List<StringName>();
        if (values == null)
            return result;
        foreach (var value in values)
            result.Add(value);
        return result;
    }

    private static GStringNameArray ToStringNameArray(IEnumerable<StringName> values)
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
    private readonly List<Dictionary<string, object>> _itemRewards;
    private readonly List<Dictionary<string, object>> _pendingCharacterRewards;
    private readonly List<StringName> _unsupportedRewardTypes;

    private QuestClaimResultData(
        bool ok,
        string errorCode,
        int goldDelta,
        GArray itemRewards,
        GArray pendingCharacterRewards,
        IEnumerable<StringName> unsupportedRewardTypes
    )
    {
        Ok = ok;
        ErrorCode = errorCode ?? "";
        GoldDelta = Mathf.Max(goldDelta, 0);
        _itemRewards = RuntimePlainPayload.NormalizeDictionaryArray(
            itemRewards,
            "QuestClaimResultData.item_rewards"
        );
        _pendingCharacterRewards = RuntimePlainPayload.NormalizeDictionaryArray(
            pendingCharacterRewards,
            "QuestClaimResultData.pending_character_rewards"
        );
        _unsupportedRewardTypes = CloneStringNameList(unsupportedRewardTypes);
    }

    internal GArray CloneItemRewards() =>
        RuntimePlainPayload.ProjectDictionaryArray(
            _itemRewards,
            "QuestClaimResultData.CloneItemRewards"
        );

    internal GArray ClonePendingCharacterRewards() =>
        RuntimePlainPayload.ProjectDictionaryArray(
            _pendingCharacterRewards,
            "QuestClaimResultData.ClonePendingCharacterRewards"
        );

    public GStringNameArray CloneUnsupportedRewardTypes() =>
        CloneStringNameArray(_unsupportedRewardTypes);

    public string BuildRewardSummaryText()
    {
        var rewardParts = new List<string>();
        if (GoldDelta > 0)
            rewardParts.Add($"{GoldDelta} 金");
        foreach (Dictionary<string, object> rewardData in _itemRewards)
        {
            int quantity = ReadInt(rewardData, "quantity");
            string label = ReadTrimmedString(rewardData, "display_name");
            if (quantity <= 0 || label.Length == 0)
                continue;
            rewardParts.Add($"{label} x{quantity}");
        }
        foreach (Dictionary<string, object> rewardData in _pendingCharacterRewards)
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
        IEnumerable<StringName> unsupportedRewardTypes = null
    ) =>
        new(
            false,
            errorCode,
            0,
            new GArray(),
            new GArray(),
            unsupportedRewardTypes
        );

    private static List<StringName> CloneStringNameList(IEnumerable<StringName> values)
    {
        var result = new List<StringName>();
        if (values == null)
            return result;
        foreach (var value in values)
            result.Add(value);
        return result;
    }

    private static GStringNameArray CloneStringNameArray(IEnumerable<StringName> values)
    {
        var result = new GStringNameArray();
        if (values == null)
            return result;
        foreach (var value in values)
            result.Add(value);
        return result;
    }

    private static int ReadInt(IReadOnlyDictionary<string, object> data, string key)
    {
        if (data == null || !data.TryGetValue(key, out object rawValue))
            return 0;
        return rawValue switch
        {
            Variant value when value.VariantType == Variant.Type.Int => value.AsInt32(),
            int intValue => intValue,
            long longValue => (int)longValue,
            _ => 0,
        };
    }

    private static string ReadTrimmedString(IReadOnlyDictionary<string, object> data, string key)
    {
        if (data == null || !data.TryGetValue(key, out object rawValue))
            return "";
        return rawValue switch
        {
            Variant value when value.VariantType == Variant.Type.String => value.AsString().Trim(),
            Variant value when value.VariantType == Variant.Type.StringName =>
                value.AsStringName().ToString().Trim(),
            string stringValue => stringValue.Trim(),
            StringName stringNameValue => stringNameValue.ToString().Trim(),
            _ => "",
        };
    }
}
