using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Godot;

internal sealed class BattleDamagePreviewWorkingSet
{
    private readonly List<BattleFatalInterceptPreviewBranch> _continuationBranches = new();

    private BattleDamagePreviewWorkingSet(
        BattleUnitState sourcePreview,
        BattleUnitState targetPreview,
        BattleState battleState
    )
    {
        SourcePreview = sourcePreview;
        TargetPreview = targetPreview;
        BattleState = battleState;
    }

    internal BattleUnitState SourcePreview { get; private set; }
    internal BattleUnitState TargetPreview { get; private set; }
    internal BattleState BattleState { get; private set; }
    internal IReadOnlyList<BattleFatalInterceptPreviewBranch> ContinuationBranches =>
        _continuationBranches;
    internal bool HasContinuationState => _continuationBranches.Count > 0;

    internal static BattleDamagePreviewWorkingSet CreateDetached(
        BattleUnitState source,
        BattleUnitState target,
        BattleState battleState = null
    )
    {
        if (source == null || target == null)
        {
            return null;
        }

        // Performance contract: clone each unit once for the whole multi-effect preview
        // sequence. Callers must reuse this detached working set instead of cloning per hit.
        // A detached state is also required so equipment reactions cannot reach canonical
        // neighbours through a target selector while previewing nested actions.
        BattleDetachedPreviewState detached =
            BattleDetachedPreviewState.Create(battleState, source, target);
        BattleUnitState sourcePreview = detached.GetUnit(source.unit_id);
        BattleUnitState targetPreview = detached.GetUnit(target.unit_id);
        return sourcePreview != null && targetPreview != null
            ? new BattleDamagePreviewWorkingSet(sourcePreview, targetPreview, detached.State)
            : null;
    }

    internal static BattleDamagePreviewWorkingSet FromDetachedState(
        BattleUnitState sourcePreview,
        BattleUnitState targetPreview,
        BattleState battleState
    ) =>
        sourcePreview != null && targetPreview != null && battleState != null
            ? new BattleDamagePreviewWorkingSet(sourcePreview, targetPreview, battleState)
            : null;

    internal void ReplaceContinuationBranches(
        IEnumerable<BattleFatalInterceptPreviewBranch> branches
    )
    {
        _continuationBranches.Clear();
        var indexBySignature = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (
            BattleFatalInterceptPreviewBranch branch
            in branches ?? Array.Empty<BattleFatalInterceptPreviewBranch>()
        )
        {
            int probability = Math.Clamp(branch?.ProbabilityBasisPoints ?? 0, 0, 10000);
            if (probability <= 0 || branch?.TargetUnit == null || branch.BattleState == null)
                continue;
            string signature = BattleDamagePreviewBranchSignature.Build(branch);
            if (indexBySignature.TryGetValue(signature, out int existingIndex))
            {
                BattleFatalInterceptPreviewBranch existing =
                    _continuationBranches[existingIndex];
                _continuationBranches[existingIndex] = CopyBranchWithProbability(
                    existing,
                    Math.Clamp(
                        existing.ProbabilityBasisPoints + probability,
                        0,
                        10000
                    )
                );
                continue;
            }

            indexBySignature[signature] = _continuationBranches.Count;
            _continuationBranches.Add(CopyBranchWithProbability(branch, probability));
        }

        int totalProbability = 0;
        foreach (BattleFatalInterceptPreviewBranch branch in _continuationBranches)
        {
            totalProbability = Math.Clamp(
                totalProbability + branch.ProbabilityBasisPoints,
                0,
                10000
            );
        }
        if (totalProbability < 10000)
        {
            int remainder = 10000 - totalProbability;
            if (_continuationBranches.Count > 0)
            {
                BattleFatalInterceptPreviewBranch first = _continuationBranches[0];
                _continuationBranches[0] = CopyBranchWithProbability(
                    first,
                    Math.Clamp(first.ProbabilityBasisPoints + remainder, 0, 10000)
                );
            }
        }

        if (
            _continuationBranches.Count == 1
            && _continuationBranches[0].ProbabilityBasisPoints >= 10000
        )
        {
            BattleFatalInterceptPreviewBranch deterministic = _continuationBranches[0];
            BattleState = deterministic.BattleState;
            SourcePreview = deterministic.SourceUnit ?? SourcePreview;
            TargetPreview = deterministic.TargetUnit;
        }
    }

    private static BattleFatalInterceptPreviewBranch CopyBranchWithProbability(
        BattleFatalInterceptPreviewBranch branch,
        int probabilityBasisPoints
    ) =>
        new()
        {
            ProbabilityBasisPoints = Math.Clamp(probabilityBasisPoints, 0, 10000),
            BattleState = branch.BattleState,
            SourceUnit = branch.SourceUnit,
            TargetUnit = branch.TargetUnit,
            Intercepted = branch.Intercepted,
            WinningBindingId = branch.WinningBindingId,
            WinningInterceptId = branch.WinningInterceptId,
        };
}

internal static class BattleDamagePreviewBranchSignature
{
    internal static string Build(BattleFatalInterceptPreviewBranch branch)
    {
        var builder = new StringBuilder(512);
        BattleState state = branch?.BattleState;
        if (state == null)
            return "state:null";
        BattleEnvironmentSnapshot environment = state.GetEnvironmentSnapshot();
        AppendText(builder, state.battle_id.ToString());
        AppendInt(builder, environment?.WorldStep ?? -1);
        var units = new List<BattleUnitState>();
        foreach (BattleUnitState unit in state.GetUnitsTyped())
        {
            if (unit != null)
                units.Add(unit);
        }
        units.Sort(
            (left, right) => string.CompareOrdinal(
                left.unit_id.ToString(),
                right.unit_id.ToString()
            )
        );
        foreach (BattleUnitState unit in units)
        {
            AppendText(builder, unit.unit_id.ToString());
            AppendValue(builder, unit.BuildPlainSnapshotDetached());
            var charges = new List<KeyValuePair<StringName, int>>(
                unit.GetPerBattleChargesTyped()
            );
            charges.Sort(
                (left, right) => string.CompareOrdinal(
                    left.Key.ToString(),
                    right.Key.ToString()
                )
            );
            builder.Append("charges[");
            foreach (KeyValuePair<StringName, int> charge in charges)
            {
                AppendText(builder, charge.Key.ToString());
                AppendInt(builder, charge.Value);
            }
            builder.Append(']');
        }
        return builder.ToString();
    }

    private static void AppendValue(StringBuilder builder, object value)
    {
        switch (value)
        {
            case null:
                builder.Append("null;");
                return;
            case bool flag:
                builder.Append(flag ? "b1;" : "b0;");
                return;
            case byte or sbyte or short or ushort or int or uint or long or ulong:
                builder.Append('i').Append(value).Append(';');
                return;
            case float or double or decimal:
                builder.Append('f').Append(value).Append(';');
                return;
            case StringName name:
                AppendText(builder, name.ToString());
                return;
            case string text:
                AppendText(builder, text);
                return;
            case Vector2I coord:
                builder.Append('v').Append(coord.X).Append(',').Append(coord.Y).Append(';');
                return;
            case IReadOnlyDictionary<string, object> typed:
                AppendTypedDictionary(builder, typed);
                return;
            case IDictionary dictionary:
                AppendDictionary(builder, dictionary);
                return;
            case IEnumerable sequence:
                builder.Append("array[");
                foreach (object entry in sequence)
                    AppendValue(builder, entry);
                builder.Append(']');
                return;
            default:
                AppendText(builder, value.ToString());
                return;
        }
    }

    private static void AppendTypedDictionary(
        StringBuilder builder,
        IReadOnlyDictionary<string, object> values
    )
    {
        var keys = new List<string>(values?.Keys ?? Array.Empty<string>());
        keys.Sort(StringComparer.Ordinal);
        builder.Append("map{");
        foreach (string key in keys)
        {
            AppendText(builder, key);
            values.TryGetValue(key, out object value);
            AppendValue(builder, value);
        }
        builder.Append('}');
    }

    private static void AppendDictionary(StringBuilder builder, IDictionary values)
    {
        var entries = new List<(string Key, object Value)>();
        foreach (DictionaryEntry entry in values ?? new System.Collections.Hashtable())
            entries.Add((entry.Key?.ToString() ?? "", entry.Value));
        entries.Sort((left, right) => string.CompareOrdinal(left.Key, right.Key));
        builder.Append("map{");
        foreach ((string key, object value) in entries)
        {
            AppendText(builder, key);
            AppendValue(builder, value);
        }
        builder.Append('}');
    }

    private static void AppendText(StringBuilder builder, string value)
    {
        string text = value ?? "";
        builder.Append('s').Append(text.Length).Append(':').Append(text).Append(';');
    }

    private static void AppendInt(StringBuilder builder, int value) =>
        builder.Append('i').Append(value).Append(';');
}

internal sealed class BattleDamagePreviewScoreResult
{
    internal bool Applied { get; private set; }
    internal StringName RollMode { get; private set; }
    internal StringName SaveMode { get; private set; }
    internal int PreSaveDamage { get; private set; }
    internal int PostSaveDamage { get; private set; }
    internal int HpDamage { get; private set; }
    internal int Damage { get; private set; }
    internal int IncomingBudgetDamage { get; private set; }
    internal int ShieldAbsorbed { get; private set; }
    internal bool ShieldBroken { get; private set; }
    internal int ShieldHpBefore { get; private set; }
    internal int ShieldHpAfter { get; private set; }
    internal bool StableLethal { get; private set; }
    internal int LethalProbabilityBasisPoints { get; private set; }
    internal int FatalInterceptProbabilityBasisPoints { get; private set; }
    internal int ExpectedSurvivalHp { get; private set; }
    internal string ErrorCode { get; private set; } = "";
    internal BattleDamagePreviewSaveEstimate SaveEstimate { get; private set; } =
        BattleDamagePreviewSaveEstimate.None(0);
    internal IReadOnlyList<object> Diagnostics { get; private set; } = Array.Empty<object>();

    internal static BattleDamagePreviewScoreResult Empty() => Create();

    internal static BattleDamagePreviewScoreResult Create(
        bool applied = false,
        StringName rollMode = default,
        StringName saveMode = default,
        int preSaveDamage = 0,
        int postSaveDamage = 0,
        int hpDamage = 0,
        int damage = 0,
        int incomingBudgetDamage = 0,
        int shieldAbsorbed = 0,
        bool shieldBroken = false,
        int shieldHpBefore = 0,
        int shieldHpAfter = 0,
        bool stableLethal = false,
        int lethalProbabilityBasisPoints = 0,
        int fatalInterceptProbabilityBasisPoints = 0,
        int expectedSurvivalHp = 0,
        string errorCode = "",
        BattleDamagePreviewSaveEstimate saveEstimate = null,
        IReadOnlyList<object> diagnostics = null
    )
    {
        return new BattleDamagePreviewScoreResult
        {
            Applied = applied,
            RollMode = rollMode,
            SaveMode = saveMode,
            PreSaveDamage = Math.Max(preSaveDamage, 0),
            PostSaveDamage = Math.Max(postSaveDamage, 0),
            HpDamage = Math.Max(hpDamage, 0),
            Damage = Math.Max(damage, 0),
            IncomingBudgetDamage = Math.Max(incomingBudgetDamage, 0),
            ShieldAbsorbed = Math.Max(shieldAbsorbed, 0),
            ShieldBroken = shieldBroken,
            ShieldHpBefore = Math.Max(shieldHpBefore, 0),
            ShieldHpAfter = Math.Max(shieldHpAfter, 0),
            StableLethal = stableLethal,
            LethalProbabilityBasisPoints = Math.Clamp(
                lethalProbabilityBasisPoints,
                0,
                10000
            ),
            FatalInterceptProbabilityBasisPoints = Math.Clamp(
                fatalInterceptProbabilityBasisPoints,
                0,
                10000
            ),
            ExpectedSurvivalHp = Math.Max(expectedSurvivalHp, 0),
            ErrorCode = errorCode ?? "",
            SaveEstimate = saveEstimate ?? BattleDamagePreviewSaveEstimate.None(preSaveDamage),
            Diagnostics = diagnostics ?? Array.Empty<object>(),
        };
    }
}

public sealed record BattleWeightedStatusOutcomePreviewData(
    StringName OutcomeId,
    StringName StatusId,
    string DisplayName,
    int Weight,
    int TotalWeight,
    int ConditionalProbabilityBasisPoints,
    int ApplicationProbabilityBasisPoints,
    int DurationTu,
    int Power,
    int AttackRollPenalty,
    bool LockCounterattack,
    bool LockGuard,
    bool LockDodgeBonus,
    bool LockCrit
);

public sealed class BattleDamagePreviewSaveEstimate
{
    public bool HasSave { get; private set; }
    public int DamageBeforeSave { get; private set; }
    public int DamageAfterSave { get; private set; }
    public int DamageAfterSaveEstimate { get; private set; }
    public int DamageAfterSaveWorst { get; private set; }
    public int DamageOnSaveFailure { get; private set; }
    public int DamageOnSaveSuccess { get; private set; }
    public bool SavePartialOnSuccess { get; private set; }
    public int SaveSuccessProbabilityBasisPoints { get; private set; }
    public int SaveSuccessRatePercent { get; private set; }
    public int SaveFailureProbabilityBasisPoints { get; private set; }
    public int Dc { get; private set; }
    public string Ability { get; private set; } = "";
    public string SaveTag { get; private set; } = "";
    public string AdvantageState { get; private set; } = "";
    public int AbilityValue { get; private set; }
    public int AbilityModifier { get; private set; }
    public int Bonus { get; private set; }
    public bool Immune { get; private set; }
    public IReadOnlyList<BattleSaveSource> Sources { get; private set; } =
        Array.Empty<BattleSaveSource>();
    public IReadOnlyList<BattleWeightedStatusOutcomePreviewData> SaveFailureStatusOutcomes
    {
        get;
        private set;
    } = Array.Empty<BattleWeightedStatusOutcomePreviewData>();

    public static BattleDamagePreviewSaveEstimate Create(
        bool hasSave,
        int damageBeforeSave,
        int damageAfterSave,
        int damageAfterSaveEstimate,
        int damageAfterSaveWorst,
        int damageOnSaveFailure,
        int damageOnSaveSuccess,
        bool savePartialOnSuccess,
        int saveSuccessProbabilityBasisPoints,
        int saveSuccessRatePercent,
        int saveFailureProbabilityBasisPoints,
        int dc,
        string ability,
        string saveTag,
        string advantageState,
        int abilityValue,
        int abilityModifier,
        int bonus,
        bool immune,
        IReadOnlyList<BattleSaveSource> sources,
        IReadOnlyList<BattleWeightedStatusOutcomePreviewData> saveFailureStatusOutcomes = null
    )
    {
        return new BattleDamagePreviewSaveEstimate
        {
            HasSave = hasSave,
            DamageBeforeSave = Math.Max(damageBeforeSave, 0),
            DamageAfterSave = Math.Max(damageAfterSave, 0),
            DamageAfterSaveEstimate = Math.Max(damageAfterSaveEstimate, 0),
            DamageAfterSaveWorst = Math.Max(damageAfterSaveWorst, 0),
            DamageOnSaveFailure = Math.Max(damageOnSaveFailure, 0),
            DamageOnSaveSuccess = Math.Max(damageOnSaveSuccess, 0),
            SavePartialOnSuccess = savePartialOnSuccess,
            SaveSuccessProbabilityBasisPoints = Math.Max(saveSuccessProbabilityBasisPoints, 0),
            SaveSuccessRatePercent = Math.Max(saveSuccessRatePercent, 0),
            SaveFailureProbabilityBasisPoints = Math.Max(saveFailureProbabilityBasisPoints, 0),
            Dc = dc,
            Ability = ability ?? "",
            SaveTag = saveTag ?? "",
            AdvantageState = advantageState ?? "",
            AbilityValue = abilityValue,
            AbilityModifier = abilityModifier,
            Bonus = bonus,
            Immune = immune,
            Sources = sources ?? Array.Empty<BattleSaveSource>(),
            SaveFailureStatusOutcomes = saveFailureStatusOutcomes != null
                ? new List<BattleWeightedStatusOutcomePreviewData>(
                    saveFailureStatusOutcomes
                ).AsReadOnly()
                : Array.Empty<BattleWeightedStatusOutcomePreviewData>(),
        };
    }

    public static BattleDamagePreviewSaveEstimate None(int damageBeforeSave)
    {
        int normalizedDamage = Math.Max(damageBeforeSave, 0);
        return Create(
            false,
            normalizedDamage,
            normalizedDamage,
            normalizedDamage,
            normalizedDamage,
            normalizedDamage,
            normalizedDamage,
            false,
            0,
            0,
            10000,
            0,
            "",
            "",
            "",
            0,
            0,
            0,
            false,
            Array.Empty<BattleSaveSource>()
        );
    }

    internal Dictionary<string, object> ToTraceDictionary()
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["has_save"] = HasSave,
            ["damage_before_save"] = DamageBeforeSave,
            ["damage_after_save"] = DamageAfterSave,
            ["damage_after_save_estimate"] = DamageAfterSaveEstimate,
            ["damage_after_save_worst"] = DamageAfterSaveWorst,
        };
        if (!HasSave)
        {
            return result;
        }
        result["damage_on_save_failure"] = DamageOnSaveFailure;
        result["damage_on_save_success"] = DamageOnSaveSuccess;
        result["save_partial_on_success"] = SavePartialOnSuccess;
        result["save_success_probability_basis_points"] = SaveSuccessProbabilityBasisPoints;
        result["save_success_rate_percent"] = SaveSuccessRatePercent;
        result["save_failure_probability_basis_points"] = SaveFailureProbabilityBasisPoints;
        result["dc"] = Dc;
        result["ability"] = Ability ?? "";
        result["save_tag"] = SaveTag ?? "";
        result["advantage_state"] = AdvantageState ?? "";
        result["ability_value"] = AbilityValue;
        result["ability_modifier"] = AbilityModifier;
        result["bonus"] = Bonus;
        result["immune"] = Immune;
        result["sources"] = BuildTraceSaveSourceList(Sources);
        result["save_failure_status_outcomes"] =
            BuildTraceWeightedStatusOutcomeList(SaveFailureStatusOutcomes);
        return result;
    }

    private static List<object> BuildTraceWeightedStatusOutcomeList(
        IReadOnlyList<BattleWeightedStatusOutcomePreviewData> outcomes
    )
    {
        var result = new List<object>();
        foreach (
            BattleWeightedStatusOutcomePreviewData outcome in
                outcomes ?? Array.Empty<BattleWeightedStatusOutcomePreviewData>()
        )
        {
            if (outcome == null)
                continue;
            result.Add(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["outcome_id"] = outcome.OutcomeId,
                    ["status_id"] = outcome.StatusId,
                    ["display_name"] = outcome.DisplayName ?? "",
                    ["weight"] = outcome.Weight,
                    ["total_weight"] = outcome.TotalWeight,
                    ["conditional_probability_basis_points"] =
                        outcome.ConditionalProbabilityBasisPoints,
                    ["application_probability_basis_points"] =
                        outcome.ApplicationProbabilityBasisPoints,
                    ["duration_tu"] = outcome.DurationTu,
                    ["power"] = outcome.Power,
                    ["attack_roll_penalty"] = outcome.AttackRollPenalty,
                    ["lock_counterattack"] = outcome.LockCounterattack,
                    ["lock_guard"] = outcome.LockGuard,
                    ["lock_dodge_bonus"] = outcome.LockDodgeBonus,
                    ["lock_crit"] = outcome.LockCrit,
                }
            );
        }
        return result;
    }

    private static List<object> BuildTraceSaveSourceList(IReadOnlyList<BattleSaveSource> sources)
    {
        var result = new List<object>();
        if (sources == null)
        {
            return result;
        }
        foreach (BattleSaveSource source in sources)
        {
            result.Add(source.ToTraceDictionary());
        }
        return result;
    }
}

public sealed class BattleDamagePreviewResult
{
    public bool Applied { get; private set; }
    public StringName RollMode { get; private set; }
    public StringName SaveMode { get; private set; }
    public int PreSaveDamage { get; private set; }
    public int PostSaveDamage { get; private set; }
    public int HpDamage { get; private set; }
    public int Damage { get; private set; }
    public int IncomingBudgetDamage { get; private set; }
    public int ShieldAbsorbed { get; private set; }
    public bool ShieldBroken { get; private set; }
    public int ShieldHpBefore { get; private set; }
    public int ShieldHpAfter { get; private set; }
    public bool StableLethal { get; private set; }
    public int LethalProbabilityBasisPoints { get; private set; }
    public int FatalInterceptProbabilityBasisPoints { get; private set; }
    public int ExpectedSurvivalHp { get; private set; }
    public string ErrorCode { get; private set; } = "";
    public IReadOnlyDictionary<string, object> DamageOutcome { get; private set; } =
        new Dictionary<string, object>(StringComparer.Ordinal);
    public IReadOnlyDictionary<string, object> DamageResult { get; private set; } =
        new Dictionary<string, object>(StringComparer.Ordinal);
    public BattleDamagePreviewSaveEstimate SaveEstimate { get; private set; } =
        BattleDamagePreviewSaveEstimate.None(0);
    public IReadOnlyList<BattleDamagePreviewSaveEstimate> SaveEstimates { get; private set; } =
        Array.Empty<BattleDamagePreviewSaveEstimate>();
    public IReadOnlyList<object> DamageEvents { get; private set; } = Array.Empty<object>();
    public IReadOnlyList<object> Diagnostics { get; private set; } = Array.Empty<object>();
    public BattleUnitState SourcePreviewAfter { get; private set; }
    public BattleUnitState TargetPreviewAfter { get; private set; }
    internal BattleFatalInterceptPreviewResult FatalInterceptPreview { get; private set; }
    internal IReadOnlyList<BattleEquipmentAbilityActionPreviewResult> EquipmentActionPreviews
    {
        get;
        private set;
    } = Array.Empty<BattleEquipmentAbilityActionPreviewResult>();

    public static BattleDamagePreviewResult Empty() => Create();

    public static BattleDamagePreviewResult Create(
        bool applied = false,
        StringName rollMode = default,
        StringName saveMode = default,
        int preSaveDamage = 0,
        int postSaveDamage = 0,
        int hpDamage = 0,
        int damage = 0,
        int incomingBudgetDamage = 0,
        int shieldAbsorbed = 0,
        bool shieldBroken = false,
        int shieldHpBefore = 0,
        int shieldHpAfter = 0,
        bool stableLethal = false,
        int lethalProbabilityBasisPoints = 0,
        int fatalInterceptProbabilityBasisPoints = 0,
        int expectedSurvivalHp = 0,
        string errorCode = "",
        IReadOnlyDictionary<string, object> damageOutcome = null,
        IReadOnlyDictionary<string, object> damageResult = null,
        BattleDamagePreviewSaveEstimate saveEstimate = null,
        IReadOnlyList<BattleDamagePreviewSaveEstimate> saveEstimates = null,
        IReadOnlyList<object> damageEvents = null,
        IReadOnlyList<object> diagnostics = null,
        BattleUnitState sourcePreviewAfter = null,
        BattleUnitState targetPreviewAfter = null,
        BattleFatalInterceptPreviewResult fatalInterceptPreview = null,
        IReadOnlyList<BattleEquipmentAbilityActionPreviewResult> equipmentActionPreviews = null
    )
    {
        return new BattleDamagePreviewResult
        {
            Applied = applied,
            RollMode = rollMode,
            SaveMode = saveMode,
            PreSaveDamage = Math.Max(preSaveDamage, 0),
            PostSaveDamage = Math.Max(postSaveDamage, 0),
            HpDamage = Math.Max(hpDamage, 0),
            Damage = Math.Max(damage, 0),
            IncomingBudgetDamage = Math.Max(incomingBudgetDamage, 0),
            ShieldAbsorbed = Math.Max(shieldAbsorbed, 0),
            ShieldBroken = shieldBroken,
            ShieldHpBefore = Math.Max(shieldHpBefore, 0),
            ShieldHpAfter = Math.Max(shieldHpAfter, 0),
            StableLethal = stableLethal,
            LethalProbabilityBasisPoints = Math.Clamp(
                lethalProbabilityBasisPoints,
                0,
                10000
            ),
            FatalInterceptProbabilityBasisPoints = Math.Clamp(
                fatalInterceptProbabilityBasisPoints,
                0,
                10000
            ),
            ExpectedSurvivalHp = Math.Max(expectedSurvivalHp, 0),
            ErrorCode = errorCode ?? "",
            DamageOutcome = CloneTraceDictionary(damageOutcome),
            DamageResult = CloneTraceDictionary(damageResult),
            SaveEstimate = saveEstimate ?? BattleDamagePreviewSaveEstimate.None(preSaveDamage),
            SaveEstimates = saveEstimates ?? Array.Empty<BattleDamagePreviewSaveEstimate>(),
            DamageEvents = CloneTraceObjectList(damageEvents),
            Diagnostics = CloneTraceObjectList(diagnostics),
            SourcePreviewAfter = sourcePreviewAfter,
            TargetPreviewAfter = targetPreviewAfter,
            FatalInterceptPreview = fatalInterceptPreview,
            EquipmentActionPreviews = equipmentActionPreviews
                ?? Array.Empty<BattleEquipmentAbilityActionPreviewResult>(),
        };
    }

    private static List<object> CloneTraceObjectList(IEnumerable<object> values)
    {
        var result = new List<object>();
        if (values == null)
        {
            return result;
        }
        foreach (object value in values)
        {
            result.Add(CloneTraceObject(value));
        }
        return result;
    }

    private static object CloneTraceObject(object value)
    {
        return value switch
        {
            null => "",
            string text => text,
            StringName name => name,
            bool flag => flag,
            int intValue => intValue,
            long longValue => longValue,
            float floatValue => floatValue,
            double doubleValue => doubleValue,
            Vector2I coord => coord,
            IReadOnlyDictionary<string, object> dictionary => CloneTraceDictionary(dictionary),
            System.Collections.IDictionary dictionary => CloneUntypedTraceDictionary(dictionary),
            IEnumerable<object> values => CloneTraceObjectList(values),
            System.Collections.IEnumerable values when value is not string => CloneTraceEnumerable(values),
            _ => value,
        };
    }

    private static Dictionary<string, object> CloneTraceDictionary(
        IReadOnlyDictionary<string, object> source
    )
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        if (source == null)
        {
            return result;
        }
        foreach (KeyValuePair<string, object> entry in source)
        {
            if (!string.IsNullOrEmpty(entry.Key))
            {
                result[entry.Key] = CloneTraceObject(entry.Value);
            }
        }
        return result;
    }

    private static Dictionary<string, object> CloneUntypedTraceDictionary(
        System.Collections.IDictionary source
    )
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        if (source == null)
        {
            return result;
        }
        foreach (System.Collections.DictionaryEntry entry in source)
        {
            string key = entry.Key switch
            {
                null => "",
                StringName name => name.ToString(),
                _ => entry.Key.ToString(),
            };
            if (!string.IsNullOrEmpty(key))
            {
                result[key] = CloneTraceObject(entry.Value);
            }
        }
        return result;
    }

    private static List<object> CloneTraceEnumerable(System.Collections.IEnumerable values)
    {
        var result = new List<object>();
        if (values == null)
        {
            return result;
        }
        foreach (object value in values)
        {
            result.Add(CloneTraceObject(value));
        }
        return result;
    }
}
