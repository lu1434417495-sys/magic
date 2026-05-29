using Godot;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class AchievementRewardDef : RefCounted
{
    private static readonly StringName TypeKnowledgeUnlock = "knowledge_unlock";
    private static readonly StringName TypeSkillUnlock = "skill_unlock";
    private static readonly StringName TypeSkillMastery = "skill_mastery";
    private static readonly StringName TypeAttributeDelta = "attribute_delta";

    public StringName reward_type = "";
    public StringName target_id = "";
    public string target_label = "";
    public int amount;
    public string reason_text = "";

    public static StringName TYPE_KNOWLEDGE_UNLOCK() => TypeKnowledgeUnlock;

    public static StringName TYPE_SKILL_UNLOCK() => TypeSkillUnlock;

    public static StringName TYPE_SKILL_MASTERY() => TypeSkillMastery;

    public static StringName TYPE_ATTRIBUTE_DELTA() => TypeAttributeDelta;

    public bool is_empty()
    {
        if (reward_type == "" || target_id == "")
            return true;
        if (
            (reward_type == TypeAttributeDelta || reward_type == TypeSkillMastery)
            && amount == 0
        )
            return true;
        return false;
    }

    public GDictionary to_dict()
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

    public static AchievementRewardDef from_dict(GDictionary payload)
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
        if (!PendingCharacterRewardContentRules.is_supported_entry_type(rewardType))
            return null;

        string targetLabel = payload["target_label"].AsString();
        string reasonText = payload["reason_text"].AsString();
        if (string.IsNullOrEmpty(targetLabel) || string.IsNullOrEmpty(reasonText))
            return null;

        if (payload["amount"].Obj is not long amountLong)
            return null;
        int parsedAmount = (int)amountLong;
        if (
            (rewardType == TypeAttributeDelta || rewardType == TypeSkillMastery)
            && parsedAmount <= 0
        )
            return null;

        return new AchievementRewardDef
        {
            reward_type = rewardType,
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
