using System;
using System.Collections.Generic;
using Godot;

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
        IReadOnlyList<BattleSaveSource> sources
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
        string errorCode = "",
        IReadOnlyDictionary<string, object> damageOutcome = null,
        IReadOnlyDictionary<string, object> damageResult = null,
        BattleDamagePreviewSaveEstimate saveEstimate = null,
        IReadOnlyList<BattleDamagePreviewSaveEstimate> saveEstimates = null,
        IReadOnlyList<object> damageEvents = null,
        IReadOnlyList<object> diagnostics = null,
        BattleUnitState sourcePreviewAfter = null,
        BattleUnitState targetPreviewAfter = null
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
            LethalProbabilityBasisPoints = Math.Max(lethalProbabilityBasisPoints, 0),
            ErrorCode = errorCode ?? "",
            DamageOutcome = CloneTraceDictionary(damageOutcome),
            DamageResult = CloneTraceDictionary(damageResult),
            SaveEstimate = saveEstimate ?? BattleDamagePreviewSaveEstimate.None(preSaveDamage),
            SaveEstimates = saveEstimates ?? Array.Empty<BattleDamagePreviewSaveEstimate>(),
            DamageEvents = CloneTraceObjectList(damageEvents),
            Diagnostics = CloneTraceObjectList(diagnostics),
            SourcePreviewAfter = sourcePreviewAfter,
            TargetPreviewAfter = targetPreviewAfter,
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
