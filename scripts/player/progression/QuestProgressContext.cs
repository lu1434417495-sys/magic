using System;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public sealed class QuestProgressContext
{
    private static readonly StringName MemberIdKey = "member_id";
    private static readonly StringName ActionIdKey = "action_id";
    private static readonly StringName EnemyTemplateIdKey = "enemy_template_id";
    private static readonly StringName SettlementIdKey = "settlement_id";
    private static readonly StringName SourceTypeKey = "source_type";
    private static readonly StringName SourceIdKey = "source_id";
    private static readonly StringName ItemIdKey = "item_id";
    private static readonly StringName SubmittedQuantityKey = "submitted_quantity";

    public StringName MemberId { get; init; } = "";
    public string ActionId { get; init; } = "";
    public StringName EnemyTemplateId { get; init; } = "";
    public string SettlementId { get; init; } = "";
    public StringName SourceType { get; init; } = "";
    public StringName SourceId { get; init; } = "";
    public StringName ItemId { get; init; } = "";
    public int SubmittedQuantity { get; init; }

    public int Count =>
        (MemberId == "" ? 0 : 1)
        + (string.IsNullOrEmpty(ActionId) ? 0 : 1)
        + (EnemyTemplateId == "" ? 0 : 1)
        + (string.IsNullOrEmpty(SettlementId) ? 0 : 1)
        + (SourceType == "" ? 0 : 1)
        + (SourceId == "" ? 0 : 1)
        + (ItemId == "" ? 0 : 1)
        + (SubmittedQuantity > 0 ? 1 : 0);

    public QuestProgressContext DuplicateState() =>
        new()
        {
            MemberId = MemberId,
            ActionId = ActionId,
            EnemyTemplateId = EnemyTemplateId,
            SettlementId = SettlementId,
            SourceType = SourceType,
            SourceId = SourceId,
            ItemId = ItemId,
            SubmittedQuantity = SubmittedQuantity,
        };

    public GDictionary ToDictionary()
    {
        var payload = new GDictionary();
        if (MemberId != "")
            payload[MemberIdKey.ToString()] = MemberId;
        if (!string.IsNullOrEmpty(ActionId))
            payload[ActionIdKey.ToString()] = ActionId;
        if (EnemyTemplateId != "")
            payload[EnemyTemplateIdKey.ToString()] = EnemyTemplateId;
        if (!string.IsNullOrEmpty(SettlementId))
            payload[SettlementIdKey.ToString()] = SettlementId;
        if (SourceType != "")
            payload[SourceTypeKey.ToString()] = SourceType;
        if (SourceId != "")
            payload[SourceIdKey.ToString()] = SourceId;
        if (ItemId != "")
            payload[ItemIdKey.ToString()] = ItemId;
        if (SubmittedQuantity > 0)
            payload[SubmittedQuantityKey.ToString()] = SubmittedQuantity;
        return payload;
    }

    public static QuestProgressContext Empty() => new();

    public static QuestProgressContext FromDictionary(GDictionary payload)
    {
        if (payload == null)
            throw new ArgumentException("last_progress_context payload is required", nameof(payload));

        StringName memberId = "";
        string actionId = "";
        StringName enemyTemplateId = "";
        string settlementId = "";
        StringName sourceType = "";
        StringName sourceId = "";
        StringName itemId = "";
        int submittedQuantity = 0;
        var seenKeys = new System.Collections.Generic.HashSet<StringName>();

        foreach (Variant rawKey in payload.Keys)
        {
            StringName key = ProgressionDataUtils.to_string_name(rawKey);
            if (key == "")
                throw new ArgumentException("last_progress_context contains an empty key");
            if (!seenKeys.Add(key))
                throw new ArgumentException($"last_progress_context contains duplicate key {key}");
            Variant value = payload[rawKey];
            if (key == MemberIdKey)
                memberId = ReadStringName(value, key);
            else if (key == ActionIdKey)
                actionId = ReadString(value, key);
            else if (key == EnemyTemplateIdKey)
                enemyTemplateId = ReadStringName(value, key);
            else if (key == SettlementIdKey)
                settlementId = ReadString(value, key);
            else if (key == SourceTypeKey)
                sourceType = ReadStringName(value, key);
            else if (key == SourceIdKey)
                sourceId = ReadStringName(value, key);
            else if (key == ItemIdKey)
                itemId = ReadStringName(value, key);
            else if (key == SubmittedQuantityKey)
                submittedQuantity = ReadPositiveInt(value, key);
            else
                throw new ArgumentException($"last_progress_context contains unsupported key {key}");
        }

        return new QuestProgressContext
        {
            MemberId = memberId,
            ActionId = actionId,
            EnemyTemplateId = enemyTemplateId,
            SettlementId = settlementId,
            SourceType = sourceType,
            SourceId = sourceId,
            ItemId = itemId,
            SubmittedQuantity = submittedQuantity,
        };
    }

    private static StringName ReadStringName(Variant value, StringName key)
    {
        if (value.VariantType != Variant.Type.String && value.VariantType != Variant.Type.StringName)
            throw new ArgumentException($"last_progress_context[{key}] must be a string");
        return ProgressionDataUtils.to_string_name(value);
    }

    private static string ReadString(Variant value, StringName key)
    {
        if (value.VariantType != Variant.Type.String && value.VariantType != Variant.Type.StringName)
            throw new ArgumentException($"last_progress_context[{key}] must be a string");
        return value.AsString().StripEdges();
    }

    private static int ReadPositiveInt(Variant value, StringName key)
    {
        if (value.VariantType != Variant.Type.Int)
            throw new ArgumentException($"last_progress_context[{key}] must be an int");
        int parsed = value.AsInt32();
        if (parsed <= 0)
            throw new ArgumentException($"last_progress_context[{key}] must be positive");
        return parsed;
    }
}
