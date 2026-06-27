using Godot;
using System.Collections.Generic;
using GDictionary = Godot.Collections.Dictionary;

public sealed class BattleSaveBranchPreviewData
{
    private static readonly HashSet<string> KnownKeys =
        new(System.StringComparer.Ordinal)
        {
            "kind",
            "branch",
            "save_tag",
            "save_ability",
            "save_dc",
            "save_advantage_state",
            "save_success_chance_basis_points",
            "hit_chance_basis_points",
            "threshold",
            "current_hp",
            "max_hp",
            "failure_branch_text",
            "success_branch_text",
            "summary_text",
        };

    public StringName Kind { get; init; } = "";
    public StringName Branch { get; init; } = "";
    public StringName SaveTag { get; init; } = "";
    public StringName SaveAbility { get; init; } = "";
    public int SaveDc { get; init; }
    public StringName SaveAdvantageState { get; init; } = "";
    public int SaveSuccessChanceBasisPoints { get; init; }
    public int HitChanceBasisPoints { get; init; }
    public int Threshold { get; init; }
    public int CurrentHp { get; init; }
    public int MaxHp { get; init; }
    public string FailureBranchText { get; init; } = "";
    public string SuccessBranchText { get; init; } = "";
    public string SummaryText { get; init; } = "";
    public IReadOnlyDictionary<string, object> ResidualValues { get; init; } =
        new Dictionary<string, object>(System.StringComparer.Ordinal);

    internal bool IsEmpty =>
        Kind == ""
        && string.IsNullOrEmpty(SummaryText)
        && (ResidualValues == null || ResidualValues.Count == 0);

    internal BattleSaveBranchPreviewData Clone() =>
        new()
        {
            Kind = Kind,
            Branch = Branch,
            SaveTag = SaveTag,
            SaveAbility = SaveAbility,
            SaveDc = SaveDc,
            SaveAdvantageState = SaveAdvantageState,
            SaveSuccessChanceBasisPoints = SaveSuccessChanceBasisPoints,
            HitChanceBasisPoints = HitChanceBasisPoints,
            Threshold = Threshold,
            CurrentHp = CurrentHp,
            MaxHp = MaxHp,
            FailureBranchText = FailureBranchText ?? "",
            SuccessBranchText = SuccessBranchText ?? "",
            SummaryText = SummaryText ?? "",
            ResidualValues = CopyResidualValues(ResidualValues),
        };

    internal GDictionary ToDictionary()
    {
        if (IsEmpty)
            return new GDictionary();

        GDictionary result = new();
        if (HasKnownPayload())
        {
            result["kind"] = Kind;
            result["branch"] = Branch;
            result["save_tag"] = SaveTag;
            result["save_ability"] = SaveAbility;
            result["save_dc"] = SaveDc;
            result["save_advantage_state"] = SaveAdvantageState;
            result["save_success_chance_basis_points"] = SaveSuccessChanceBasisPoints;
            result["hit_chance_basis_points"] = HitChanceBasisPoints;
            result["threshold"] = Threshold;
            result["current_hp"] = CurrentHp;
            result["max_hp"] = MaxHp;
            result["failure_branch_text"] = FailureBranchText ?? "";
            result["success_branch_text"] = SuccessBranchText ?? "";
            result["summary_text"] = SummaryText ?? "";
        }

        foreach (KeyValuePair<string, object> entry in ResidualValues ?? EmptyResidualValues())
        {
            if (string.IsNullOrEmpty(entry.Key) || KnownKeys.Contains(entry.Key))
                continue;
            result[entry.Key] = ToVariant(entry.Value);
        }

        RuntimeStateLifecycle.MarkValueGraphFinalizerless(
            result,
            "BattleSaveBranchPreviewData.ToDictionary"
        );
        return result;
    }

    internal static BattleSaveBranchPreviewData FromDictionary(GDictionary source)
    {
        if (source == null || source.Count == 0)
            return null;
        return new BattleSaveBranchPreviewData
        {
            Kind = ReadStringName(source, "kind"),
            Branch = ReadStringName(source, "branch"),
            SaveTag = ReadStringName(source, "save_tag"),
            SaveAbility = ReadStringName(source, "save_ability"),
            SaveDc = ReadInt(source, "save_dc"),
            SaveAdvantageState = ReadStringName(source, "save_advantage_state"),
            SaveSuccessChanceBasisPoints = ReadInt(
                source,
                "save_success_chance_basis_points"
            ),
            HitChanceBasisPoints = ReadInt(source, "hit_chance_basis_points"),
            Threshold = ReadInt(source, "threshold"),
            CurrentHp = ReadInt(source, "current_hp"),
            MaxHp = ReadInt(source, "max_hp"),
            FailureBranchText = ReadString(source, "failure_branch_text"),
            SuccessBranchText = ReadString(source, "success_branch_text"),
            SummaryText = ReadString(source, "summary_text"),
            ResidualValues = ReadResidualValues(source),
        };
    }

    private bool HasKnownPayload()
    {
        return Kind != ""
            || Branch != ""
            || SaveTag != ""
            || SaveAbility != ""
            || SaveDc != 0
            || SaveAdvantageState != ""
            || SaveSuccessChanceBasisPoints != 0
            || HitChanceBasisPoints != 0
            || Threshold != 0
            || CurrentHp != 0
            || MaxHp != 0
            || !string.IsNullOrEmpty(FailureBranchText)
            || !string.IsNullOrEmpty(SuccessBranchText)
            || !string.IsNullOrEmpty(SummaryText);
    }

    private static IReadOnlyDictionary<string, object> ReadResidualValues(GDictionary source)
    {
        Dictionary<string, object> result = new(System.StringComparer.Ordinal);
        if (source == null)
            return result;

        foreach (Variant keyVariant in source.Keys)
        {
            string key = KeyText(keyVariant);
            if (string.IsNullOrEmpty(key) || KnownKeys.Contains(key))
                continue;
            Variant value = source[keyVariant];
            if (TryReadSafeResidualValue(value, out object safeValue))
            {
                result[key] = safeValue;
            }
        }
        return result;
    }

    private static string KeyText(Variant key)
    {
        return key.VariantType switch
        {
            Variant.Type.StringName => key.AsStringName().ToString(),
            Variant.Type.String => key.AsString(),
            _ => "",
        };
    }

    private static bool TryReadSafeResidualValue(Variant value, out object safeValue)
    {
        safeValue = null;
        switch (value.VariantType)
        {
            case Variant.Type.Nil:
                return true;
            case Variant.Type.Bool:
                safeValue = value.AsBool();
                return true;
            case Variant.Type.Int:
                safeValue = value.AsInt32();
                return true;
            case Variant.Type.Float:
                safeValue = value.AsDouble();
                return true;
            case Variant.Type.String:
                safeValue = value.AsString();
                return true;
            case Variant.Type.StringName:
                safeValue = value.AsStringName();
                return true;
            case Variant.Type.Vector2I:
                safeValue = value.AsVector2I();
                return true;
            default:
                return false;
        }
    }

    private static Dictionary<string, object> CopyResidualValues(
        IReadOnlyDictionary<string, object> source
    )
    {
        Dictionary<string, object> result = new(System.StringComparer.Ordinal);
        if (source == null)
            return result;

        foreach (KeyValuePair<string, object> entry in source)
        {
            if (string.IsNullOrEmpty(entry.Key) || KnownKeys.Contains(entry.Key))
                continue;
            result[entry.Key] = entry.Value;
        }
        return result;
    }

    private static IReadOnlyDictionary<string, object> EmptyResidualValues() =>
        new Dictionary<string, object>(System.StringComparer.Ordinal);

    private static Variant ToVariant(object value)
    {
        return value switch
        {
            null => default,
            bool boolValue => Variant.From(boolValue),
            int intValue => Variant.From(intValue),
            long longValue => Variant.From(longValue),
            double doubleValue => Variant.From(doubleValue),
            float floatValue => Variant.From(floatValue),
            string stringValue => Variant.From(stringValue),
            StringName stringNameValue => Variant.From(stringNameValue),
            Vector2I vector2IValue => Variant.From(vector2IValue),
            _ => Variant.From(value.ToString() ?? ""),
        };
    }

    private static StringName ReadStringName(GDictionary source, string key)
    {
        return source != null && source.ContainsKey(key)
            ? ProgressionDataUtils.to_string_name(source[key])
            : "";
    }

    private static int ReadInt(GDictionary source, string key)
    {
        return source != null && source.ContainsKey(key) ? source[key].AsInt32() : 0;
    }

    private static string ReadString(GDictionary source, string key)
    {
        return source != null && source.ContainsKey(key) ? source[key].ToString() : "";
    }
}
