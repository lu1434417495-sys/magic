using Godot;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class AchievementRewardDef : RefCounted
{
    public StringName reward_type = "";
    public StringName target_id = "";
    public string target_label = "";
    public int amount;
    public string reason_text = "";

    internal PendingCharacterRewardEntryKind RewardKind
    {
        get => PendingCharacterRewardContentRules.ToEntryKind(reward_type);
        set => reward_type = PendingCharacterRewardContentRules.ToStringName(value);
    }

    public bool IsEmpty()
    {
        if (RewardKind == PendingCharacterRewardEntryKind.Unknown || target_id == "")
            return true;
        if (
            (
                RewardKind == PendingCharacterRewardEntryKind.AttributeDelta
                || RewardKind == PendingCharacterRewardEntryKind.SkillMastery
            )
            && amount == 0
        )
            return true;
        return false;
    }

    public GDictionary ToDictionary()
    {
        return new GDictionary
        {
            ["reward_type"] = reward_type.ToString(),
            ["target_id"] = target_id.ToString(),
            ["target_label"] = target_label,
            ["amount"] = amount,
            ["reason_text"] = reason_text,
        };
    }

    public static AchievementRewardDef FromDictionary(GDictionary payload)
    {
        if (
            !HasExactFields(
                payload,
                new Godot.Collections.Array<string>
                {
                    "reward_type",
                    "target_id",
                    "target_label",
                    "amount",
                    "reason_text",
                }
            )
        )
            return null;

        StringName rewardType = payload["reward_type"].AsString();
        StringName targetId = payload["target_id"].AsString();
        if (rewardType == "" || targetId == "")
            return null;
        PendingCharacterRewardEntryKind rewardKind =
            PendingCharacterRewardContentRules.ToEntryKind(rewardType);
        if (rewardKind == PendingCharacterRewardEntryKind.Unknown)
            return null;

        string targetLabel = payload["target_label"].AsString();
        string reasonText = payload["reason_text"].AsString();
        if (string.IsNullOrEmpty(targetLabel) || string.IsNullOrEmpty(reasonText))
            return null;

        if (payload["amount"].Obj is not long amountLong)
            return null;
        int parsedAmount = (int)amountLong;
        if (
            (
                rewardKind == PendingCharacterRewardEntryKind.AttributeDelta
                || rewardKind == PendingCharacterRewardEntryKind.SkillMastery
            )
            && parsedAmount <= 0
        )
            return null;

        return new AchievementRewardDef
        {
            RewardKind = rewardKind,
            target_id = targetId,
            target_label = targetLabel,
            amount = parsedAmount,
            reason_text = reasonText,
        };
    }

    private static bool HasExactFields(
        GDictionary data,
        Godot.Collections.Array<string> expectedFields
    )
    {
        if (data.Count != expectedFields.Count)
            return false;
        foreach (string fieldName in expectedFields)
        {
            if (!data.ContainsKey(fieldName))
                return false;
        }
        return true;
    }
}
