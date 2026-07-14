using Godot;
using System.Collections.Generic;

public sealed class BattleSaveBranchPreviewData
{
    private Dictionary<string, object> _residualValues =
        new(System.StringComparer.Ordinal);
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
    public IReadOnlyDictionary<string, object> ResidualValues
    {
        get => CopyResidualValues(_residualValues);
        init => _residualValues = CopyResidualValues(value);
    }

    internal bool IsEmpty =>
        Kind == ""
        && string.IsNullOrEmpty(SummaryText)
        && _residualValues.Count == 0;

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
            ResidualValues = _residualValues,
        };

    internal bool HasKnownPayload()
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
            result[entry.Key] = RuntimePlainPayload.CloneValue(entry.Value);
        }
        return result;
    }

    internal static bool IsKnownKey(string key) =>
        !string.IsNullOrEmpty(key) && KnownKeys.Contains(key);
}
