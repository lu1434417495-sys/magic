using Godot;

[GlobalClass]
public partial class AchievementRewardDef : RefCounted
{
    private static readonly GDScript PENDING_CHARACTER_REWARD_CONTENT_RULES = GD.Load<GDScript>("res://scripts/player/progression/pending_character_reward_content_rules.gd");

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

    public bool is_empty() => reward_type == new StringName("") || target_id == new StringName("") || amount == 0;

    public Godot.Collections.Dictionary to_dict()
    {
        return new Godot.Collections.Dictionary
        {
            ["reward_type"] = reward_type.ToString(),
            ["target_id"] = target_id.ToString(),
            ["target_label"] = target_label,
            ["amount"] = amount,
            ["reason_text"] = reason_text,
        };
    }

    public static AchievementRewardDef from_dict(Variant data)
    {
        if (data.VariantType != Variant.Type.Dictionary) return null;
        Godot.Collections.Dictionary payload = data.AsGodotDictionary();
        if (!HasExactFields(payload, new Godot.Collections.Array<string> { "reward_type", "target_id", "target_label", "amount", "reason_text" })) return null;
        StringName rewardType = ParseStringName(payload["reward_type"], false);
        StringName targetId = ParseStringName(payload["target_id"], false);
        if (rewardType == new StringName("") || targetId == new StringName("")) return null;
        if (!(bool)PENDING_CHARACTER_REWARD_CONTENT_RULES.Call("is_supported_entry_type", rewardType)) return null;
        if (payload["target_label"].VariantType != Variant.Type.String || payload["reason_text"].VariantType != Variant.Type.String) return null;
        Variant amountValue = payload["amount"];
        if (amountValue.VariantType != Variant.Type.Int || amountValue.AsInt32() == 0) return null;
        return new AchievementRewardDef
        {
            reward_type = rewardType,
            target_id = targetId,
            target_label = payload["target_label"].AsString(),
            amount = amountValue.AsInt32(),
            reason_text = payload["reason_text"].AsString(),
        };
    }

    private static bool HasExactFields(Godot.Collections.Dictionary data, Godot.Collections.Array<string> expectedFields)
    {
        if (data.Count != expectedFields.Count) return false;
        foreach (string fieldName in expectedFields)
        {
            if (!data.ContainsKey(fieldName)) return false;
        }
        return true;
    }

    private static StringName ParseStringName(Variant value, bool allowEmpty)
    {
        if (value.VariantType != Variant.Type.String && value.VariantType != Variant.Type.StringName) return "";
        StringName parsed = ProgressionDataUtils.to_string_name(value);
        if (parsed == new StringName("") && !allowEmpty) return "";
        return parsed;
    }
}
