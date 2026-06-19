using Godot;
using GDictionary = Godot.Collections.Dictionary;

public enum SettlementResearchRewardKind
{
    Unknown = 0,
    KnowledgeUnlock,
    SkillUnlock,
}

public sealed class SettlementResearchRewardEntry
{
    private SettlementResearchRewardEntry(
        StringName researchId,
        SettlementResearchRewardKind kind,
        StringName targetId,
        string targetLabel,
        string reasonText
    )
    {
        ResearchId = researchId;
        Kind = kind;
        TargetId = targetId;
        TargetLabel = targetLabel ?? "";
        ReasonText = reasonText ?? "";
    }

    public StringName ResearchId { get; }
    public SettlementResearchRewardKind Kind { get; }
    public StringName TargetId { get; }
    public string TargetLabel { get; }
    public string ReasonText { get; }

    public StringName EntryType => Kind switch
    {
        SettlementResearchRewardKind.KnowledgeUnlock =>
            PendingCharacterRewardContentRules.ToStringName(
                PendingCharacterRewardEntryKind.KnowledgeUnlock
            ),
        SettlementResearchRewardKind.SkillUnlock =>
            PendingCharacterRewardContentRules.ToStringName(
                PendingCharacterRewardEntryKind.SkillUnlock
            ),
        _ => "",
    };

    public static SettlementResearchRewardEntry Create(
        StringName researchId,
        SettlementResearchRewardKind kind,
        StringName targetId,
        string targetLabel,
        string reasonText
    ) => new(researchId, kind, targetId, targetLabel, reasonText);

    internal static SettlementResearchRewardEntry FromDictionary(
        GDictionary data,
        string schemaLabel,
        out string error
    )
    {
        error = "";
        if (data == null)
        {
            error = $"{schemaLabel} 必须是 Dictionary。";
            return null;
        }
        string[] requiredFields =
        {
            "research_id",
            "entry_type",
            "target_id",
            "target_label",
            "reason_text",
        };
        foreach (string fieldName in requiredFields)
        {
            if (!TryReadRequiredString(data, fieldName, out string _))
            {
                error = $"{schemaLabel}.{fieldName} 必须显式提供非空 String。";
                return null;
            }
        }
        SettlementResearchRewardKind kind = ParseKind(ReadString(data, "entry_type"));
        if (kind == SettlementResearchRewardKind.Unknown)
        {
            error = $"{schemaLabel}.entry_type 必须是支持的 research reward kind。";
            return null;
        }
        return new SettlementResearchRewardEntry(
            new StringName(ReadString(data, "research_id").StripEdges()),
            kind,
            new StringName(ReadString(data, "target_id").StripEdges()),
            ReadString(data, "target_label").StripEdges(),
            ReadString(data, "reason_text").StripEdges()
        );
    }

    internal GDictionary ToDictionary() =>
        new()
        {
            ["research_id"] = ResearchId.ToString(),
            ["entry_type"] = EntryType.ToString(),
            ["target_id"] = TargetId.ToString(),
            ["target_label"] = TargetLabel,
            ["reason_text"] = ReasonText,
        };

    private static SettlementResearchRewardKind ParseKind(string value)
    {
        string normalized = (value ?? "").StripEdges();
        if (
            normalized
            == PendingCharacterRewardContentRules
                .ToStringName(PendingCharacterRewardEntryKind.KnowledgeUnlock)
                .ToString()
        )
            return SettlementResearchRewardKind.KnowledgeUnlock;
        if (
            normalized
            == PendingCharacterRewardContentRules
                .ToStringName(PendingCharacterRewardEntryKind.SkillUnlock)
                .ToString()
        )
            return SettlementResearchRewardKind.SkillUnlock;
        return SettlementResearchRewardKind.Unknown;
    }

    private static bool TryReadRequiredString(
        GDictionary data,
        string key,
        out string value
    )
    {
        value = ReadString(data, key).StripEdges();
        return value.Length > 0;
    }

    private static string ReadString(GDictionary data, string key)
    {
        if (data == null || !data.ContainsKey(key))
            return "";
        Variant value = data[key];
        return value.VariantType == Variant.Type.String ? value.AsString() : "";
    }
}
