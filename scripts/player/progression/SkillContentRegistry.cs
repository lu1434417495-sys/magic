using System.Collections.Generic;
using Godot;
using Godot.Collections;
using VT = Godot.Variant.Type;

public class SkillContentRegistry : System.IDisposable
{
    private const string SkillConfigDirectory = "res://data/configs/skills";
    internal const int TuGranularity = 5;

    private static readonly StringName[] PracticeTrackTags = { "meditation", "cultivation" };

    public Dictionary _skill_defs { get; set; } = new();
    private readonly IContentResourceLoader _resourceLoader;
    private readonly List<string> _validationErrors = new();
    public Array<string> _validation_errors
    {
        get => ToGodotStringArray(_validationErrors);
        set
        {
            _validationErrors.Clear();
            if (value == null)
                return;
            foreach (string error in value)
                _validationErrors.Add(error);
        }
    }
    private bool _disposed;
    private readonly SkillDamageEffectValidator _damageEffectValidator = new();
    private readonly SkillExecuteEffectValidator _executeEffectValidator = new();
    private readonly SkillCombatProfileValidator _combatProfileValidator;

    internal SkillContentRegistry(IContentResourceLoader resourceLoader)
        : this(resourceLoader, loadDefaultContent: true) { }

    internal SkillContentRegistry(
        IContentResourceLoader resourceLoader,
        bool loadDefaultContent
    )
    {
        _combatProfileValidator = new SkillCombatProfileValidator(
            _damageEffectValidator,
            _executeEffectValidator
        );
        _resourceLoader = resourceLoader
            ?? throw new System.ArgumentNullException(nameof(resourceLoader));
        if (loadDefaultContent)
            Rebuild();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        System.GC.SuppressFinalize(this);
        DisposeManagedRegistry();
    }

    private void DisposeManagedRegistry()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _skill_defs.Clear();
        _validationErrors.Clear();
    }

    public void Rebuild()
    {
        LoadFromDirectory(SkillConfigDirectory);
    }

    public void LoadFromDirectory(string directoryPath)
    {
        _skill_defs.Clear();
        _validationErrors.Clear();
        ScanDirectory(directoryPath);
        AppendArray(_validationErrors, CollectValidationErrors());
    }

    internal Dictionary DuplicateSkillResourceBucketForProgressionRegistry()
    {
        return _skill_defs.Duplicate();
    }

    private IReadOnlyDictionary<StringName, SkillDef> BuildSkillDefIndex()
    {
        var result = new System.Collections.Generic.Dictionary<StringName, SkillDef>();
        foreach (Variant key in _skill_defs.Keys)
        {
            if (key.VariantType != VT.StringName)
                continue;
            StringName skillId = key.AsStringName();
            if (skillId == default || skillId == (StringName)"")
                continue;
            SkillDef skillDef = GetTyped<SkillDef>(_skill_defs, skillId);
            if (skillDef == null)
                continue;
            result[skillId] = skillDef;
        }
        return result;
    }

    internal IReadOnlyDictionary<StringName, SkillDefinition> GetSkillDefinitionsTyped()
    {
        return SkillDefinition.ProjectIndex(BuildSkillDefIndex());
    }

    public Array<string> Validate()
    {
        var copy = new Array<string>();
        foreach (string error in _validationErrors)
            copy.Add(error);
        return copy;
    }

    public void AppendEffectValidationErrors(
        Array<string> errors,
        StringName skillId,
        CombatEffectDef effectDef,
        string contextLabel
    ) => _combatProfileValidator.AppendEffectValidationErrors(errors, skillId, effectDef, contextLabel);

    internal void AppendTemporalReleaseSkillValidationErrors(
        Array<string> errors,
        StringName skillId,
        CombatSkillDef combatProfile
    ) =>
        _executeEffectValidator.AppendTemporalReleaseSkillValidationErrors(
            errors,
            skillId,
            combatProfile
        );

    private void ScanDirectory(string directoryPath)
    {
        if (!DirAccess.DirExistsAbsolute(ProjectSettings.GlobalizePath(directoryPath)))
        {
            _validationErrors.Add($"SkillContentRegistry could not find {directoryPath}.");
            return;
        }

        DirAccess directory = DirAccess.Open(directoryPath);
        if (directory == null)
        {
            _validationErrors.Add($"SkillContentRegistry could not open {directoryPath}.");
            return;
        }

        try
        {
            directory.ListDirBegin();
            while (true)
            {
                string entryName = directory.GetNext();
                if (string.IsNullOrEmpty(entryName))
                    break;
                if (entryName == "." || entryName == "..")
                    continue;

                string entryPath = $"{directoryPath}/{entryName}";
                if (directory.CurrentIsDir())
                {
                    ScanDirectory(entryPath);
                    continue;
                }
                if (!entryName.EndsWith(".tres") && !entryName.EndsWith(".res"))
                    continue;
                RegisterSkillResource(entryPath);
            }
            directory.ListDirEnd();
        }
        finally
        {
            GodotObjectLifecycle.DisposeGodotObject(directory);
        }
    }

    private void RegisterSkillResource(string resourcePath)
    {
        Resource resource = _resourceLoader.LoadCanonical<Resource>(resourcePath);
        if (resource == null)
        {
            _validationErrors.Add($"Failed to load skill config {resourcePath}.");
            return;
        }
        if (resource is not SkillDef skillDef)
        {
            _validationErrors.Add($"Skill config {resourcePath} is not a SkillDef.");
            return;
        }
        if (skillDef.skill_id == "")
        {
            _validationErrors.Add($"Skill config {resourcePath} is missing skill_id.");
            return;
        }
        if (_skill_defs.ContainsKey(skillDef.skill_id))
        {
            _validationErrors.Add($"Duplicate skill_id registered: {skillDef.skill_id}");
            return;
        }

        _skill_defs[skillDef.skill_id] = skillDef;
    }

    private Array<string> CollectValidationErrors()
    {
        var errors = new Array<string>();
        foreach (string skillKey in ProgressionDataUtils.sorted_string_keys(_skill_defs))
        {
            var skillId = new StringName(skillKey);
            var skillDef = GetTyped<SkillDef>(_skill_defs, skillId);
            if (skillDef == null)
                continue;
            AppendSkillValidationErrors(errors, skillId, skillDef);
        }
        return errors;
    }

    private void AppendSkillValidationErrors(
        Array<string> errors,
        StringName skillId,
        SkillDef skillDef
    )
    {
        if (skillDef == null)
            return;

        if (skillDef.display_name.StripEdges().Length == 0)
            errors.Add($"Skill {skillId} is missing display_name.");
        if (skillDef.max_level < 0 && skillDef.dynamic_max_level_stat_id == "")
            errors.Add($"Skill {skillId} must have max_level >= 0.");
        if (skillDef.non_core_max_level < 0)
            errors.Add($"Skill {skillId} non_core_max_level must be >= 0.");
        if (
            skillDef.non_core_max_level > skillDef.max_level
            && skillDef.max_level >= 0
            && skillDef.dynamic_max_level_stat_id == ""
        )
            errors.Add($"Skill {skillId} non_core_max_level must be <= max_level.");
        if (
            skillDef.mastery_curve.Length != skillDef.max_level
            && skillDef.max_level >= 0
            && skillDef.dynamic_max_level_stat_id == ""
        )
            errors.Add($"Skill {skillId} mastery_curve size must match max_level.");
        AppendDynamicMaxLevelValidationErrors(errors, skillId, skillDef);
        foreach (int masteryThreshold in skillDef.mastery_curve)
        {
            if (masteryThreshold <= 0)
            {
                errors.Add($"Skill {skillId} has a non-positive mastery threshold.");
                break;
            }
        }

        if (skillDef.SkillTypeKind == SkillTypeKind.Unknown)
            errors.Add($"Skill {skillId} uses unsupported skill_type {skillDef.skill_type}.");
        if (skillDef.SkillTypeKind == SkillTypeKind.Active && skillDef.combat_profile == null)
            errors.Add($"Skill {skillId} is active but missing combat_profile.");
        AppendPracticeSkillValidationErrors(errors, skillId, skillDef);
        AppendAttributeGrowthValidationErrors(errors, skillId, skillDef);
        AppendRawIntRequirementEntryErrors(
            errors,
            skillId,
            skillDef.SkillLevelRequirementEntriesTyped,
            "skill_level_requirements",
            "skill_id"
        );
        AppendRawIntRequirementEntryErrors(
            errors,
            skillId,
            skillDef.AttributeRequirementEntriesTyped,
            "attribute_requirements",
            "attribute_id"
        );
        foreach (
            string error in SkillLevelDescriptionContentRules.CollectValidationErrors(
                skillId,
                skillDef
            )
        )
        {
            errors.Add(error);
        }
        _combatProfileValidator.AppendPhantasmalKillLevelDescriptionValidationErrors(errors, skillId, skillDef);
        _combatProfileValidator.AppendPhantasmalKillCombatProfileValidationErrors(errors, skillId, skillDef);

        if (skillDef.combat_profile != null)
            _combatProfileValidator.AppendCombatProfileValidationErrors(errors, skillId, skillDef.combat_profile, skillDef);
    }

    private void AppendPracticeSkillValidationErrors(
        Array<string> errors,
        StringName skillId,
        SkillDef skillDef
    )
    {
        int trackCount = 0;
        foreach (StringName trackTag in PracticeTrackTags)
        {
            if (skillDef.HasTag(trackTag))
                trackCount++;
        }

        if (trackCount == 0)
        {
            if (skillDef.PracticeTierKind != SkillPracticeTierKind.None)
                errors.Add(
                    $"Skill {skillId} practice_tier requires meditation or cultivation tag."
                );
            return;
        }

        if (trackCount != 1)
            errors.Add($"Skill {skillId} must use exactly one practice track tag.");
        if (skillDef.TagsTyped.Count != 1)
            errors.Add(
                $"Skill {skillId} practice tags must be exclusive; tags must contain only meditation or cultivation."
            );
        if (
            skillDef.PracticeTierKind
            is SkillPracticeTierKind.None
                or SkillPracticeTierKind.Unknown
        )
            errors.Add(
                $"Skill {skillId} practice_tier must be one of basic, intermediate, advanced, ultimate."
            );
    }

    private void AppendDynamicMaxLevelValidationErrors(
        Array<string> errors,
        StringName skillId,
        SkillDef skillDef
    )
    {
        bool hasDynamicStat = skillDef.dynamic_max_level_stat_id != "";
        if (!hasDynamicStat)
        {
            if (skillDef.dynamic_max_level_base != 0)
                errors.Add(
                    $"Skill {skillId} dynamic_max_level_base requires dynamic_max_level_stat_id."
                );
            if (skillDef.dynamic_max_level_per_stat != 0)
                errors.Add(
                    $"Skill {skillId} dynamic_max_level_per_stat requires dynamic_max_level_stat_id."
                );
            return;
        }

        if (skillDef.dynamic_max_level_base <= 0)
            errors.Add($"Skill {skillId} dynamic_max_level_base must be >= 1.");
        if (skillDef.dynamic_max_level_per_stat == 0)
            errors.Add(
                $"Skill {skillId} dynamic_max_level_per_stat must not be 0 when dynamic_max_level_stat_id is set."
            );
    }

    private static void AppendRawIntRequirementEntryErrors(
        Array<string> errors,
        StringName skillId,
        IReadOnlyList<SkillDef.IntRequirementEntryData> entries,
        string contextLabel,
        string idLabel
    )
    {
        foreach (SkillDef.IntRequirementEntryData entry in entries ?? System.Array.Empty<SkillDef.IntRequirementEntryData>())
        {
            if (!entry.HasStringLikeKey)
            {
                errors.Add(
                    $"Skill {skillId} has a non-string {idLabel} key {entry.RawKeyLabel} in {contextLabel}."
                );
                continue;
            }
            if (!entry.HasNonEmptyKey)
            {
                errors.Add($"Skill {skillId} has an empty {idLabel} in {contextLabel}.");
                continue;
            }
            if (!entry.HasStrictIntAmount)
            {
                errors.Add(
                    $"Skill {skillId} requires integer value for {entry.RequirementId} in {contextLabel}."
                );
            }
        }
    }

    public void AppendAttributeGrowthValidationErrors(
        Array<string> errors,
        StringName skillId,
        SkillDef skillDef
    )
    {
        if (skillDef.AttributeGrowthProgressEntriesTyped.Count == 0 && skillDef.growth_tier == "")
            return;
        if (!AttributeGrowthContentRules.IsValidGrowthTier(skillDef.growth_tier))
        {
            errors.Add($"Skill {skillId} uses unsupported growth_tier {skillDef.growth_tier}.");
            return;
        }

        int progressTotal = 0;
        foreach (
            SkillDef.AttributeGrowthProgressEntryData entry in skillDef.AttributeGrowthProgressEntriesTyped
        )
        {
            if (!entry.HasStrictStringKey || !entry.HasNonEmptyKey)
            {
                errors.Add(
                    $"Skill {skillId} attribute_growth_progress key {entry.RawKeyLabel} must be a non-empty String."
                );
                continue;
            }
            var attributeId = entry.AttributeId;
            if (!entry.HasStrictIntAmount)
            {
                errors.Add(
                    $"Skill {skillId} attribute_growth_progress for {attributeId} must be a positive int."
                );
                continue;
            }
            int amount = entry.Amount;
            if (!AttributeGrowthContentRules.IsValidAttributeId(attributeId))
                errors.Add(
                    $"Skill {skillId} attribute_growth_progress references invalid attribute {attributeId}."
                );
            if (amount <= 0)
                errors.Add(
                    $"Skill {skillId} attribute_growth_progress for {attributeId} must be a positive int."
                );
            progressTotal += amount;
        }

        int expectedTotal = AttributeGrowthContentRules.GetTierBudget(skillDef.growth_tier);
        if (progressTotal != expectedTotal)
            errors.Add(
                $"Skill {skillId} attribute_growth_progress total must equal {expectedTotal} for growth_tier {skillDef.growth_tier}."
            );
    }

    internal static bool IsValidTuValue(int value)
    {
        if (value < 0)
            return false;
        if (value == 0)
            return true;
        return value % TuGranularity == 0;
    }

    internal static void RequireStringName(
        Array<string> errors,
        StringName skillId,
        string fieldLabel,
        StringName actual,
        StringName expected
    )
    {
        if (actual != expected)
        {
            errors.Add($"Skill {skillId} {fieldLabel} must be {expected}.");
        }
    }

    internal static void RequireInt(
        Array<string> errors,
        StringName skillId,
        string fieldLabel,
        int actual,
        int expected
    )
    {
        if (actual != expected)
        {
            errors.Add($"Skill {skillId} {fieldLabel} must be {expected}.");
        }
    }

    internal static void RequireBool(
        Array<string> errors,
        StringName skillId,
        string fieldLabel,
        bool actual,
        bool expected
    )
    {
        if (actual != expected)
        {
            errors.Add($"Skill {skillId} {fieldLabel} must be {expected.ToString().ToLowerInvariant()}.");
        }
    }

    internal static void RequireRange(
        Array<string> errors,
        StringName skillId,
        string fieldLabel,
        int actual,
        int minimum,
        int maximum
    )
    {
        if (actual < minimum || actual > maximum)
        {
            errors.Add($"Skill {skillId} {fieldLabel} must be between {minimum} and {maximum}.");
        }
    }

    internal static void RequireStringNameParam(
        Array<string> errors,
        StringName skillId,
        Dictionary parameters,
        string contextLabel,
        string paramName,
        StringName expected
    )
    {
        if (!TryGetParameter(parameters, paramName, out object rawValue))
            return;
        StringName actual = ProgressionDataUtils.to_string_name(rawValue);
        if (actual != expected)
        {
            errors.Add(
                $"Skill {skillId} effect {contextLabel} params.{paramName} must be {expected}."
            );
        }
    }

    internal static void RequirePositiveIntParam(
        Array<string> errors,
        StringName skillId,
        Dictionary parameters,
        string contextLabel,
        string paramName
    )
    {
        if (
            TryReadStrictIntParam(errors, skillId, parameters, contextLabel, paramName, out int value)
            && value <= 0
        )
        {
            errors.Add(
                $"Skill {skillId} effect {contextLabel} params.{paramName} must be a positive int."
            );
        }
    }

    internal static void RequireNonNegativeIntParam(
        Array<string> errors,
        StringName skillId,
        Dictionary parameters,
        string contextLabel,
        string paramName
    )
    {
        if (
            TryReadStrictIntParam(errors, skillId, parameters, contextLabel, paramName, out int value)
            && value < 0
        )
        {
            errors.Add(
                $"Skill {skillId} effect {contextLabel} params.{paramName} must be >= 0."
            );
        }
    }

    internal static void RequireIntRangeParam(
        Array<string> errors,
        StringName skillId,
        Dictionary parameters,
        string contextLabel,
        string paramName,
        int minimum,
        int maximum
    )
    {
        if (
            TryReadStrictIntParam(errors, skillId, parameters, contextLabel, paramName, out int value)
            && (value < minimum || value > maximum)
        )
        {
            errors.Add(
                $"Skill {skillId} effect {contextLabel} params.{paramName} must be between {minimum} and {maximum}."
            );
        }
    }

    internal static void RequirePositiveTuParam(
        Array<string> errors,
        StringName skillId,
        Dictionary parameters,
        string contextLabel,
        string paramName
    )
    {
        if (
            TryReadStrictIntParam(errors, skillId, parameters, contextLabel, paramName, out int value)
            && (value <= 0 || !IsValidTuValue(value))
        )
        {
            errors.Add(
                $"Skill {skillId} effect {contextLabel} params.{paramName} must be a positive multiple of {TuGranularity}."
            );
        }
    }

    private static bool TryReadStrictIntParam(
        Array<string> errors,
        StringName skillId,
        Dictionary parameters,
        string contextLabel,
        string paramName,
        out int value
    )
    {
        if (!TryGetParameter(parameters, paramName, out object rawValue))
        {
            value = 0;
            return false;
        }
        if (TryStrictInt(rawValue, out value))
            return true;

        errors.Add(
            $"Skill {skillId} effect {contextLabel} params.{paramName} must be an int."
        );
        return false;
    }

    internal static string ParameterKeyLabel(Variant rawKey)
    {
        return rawKey.VariantType switch
        {
            VT.String => rawKey.AsString(),
            VT.StringName => rawKey.AsStringName().ToString(),
            _ => rawKey.ToString(),
        };
    }

    internal static int DictInt(Dictionary dictionary, string key, int fallback = 0)
    {
        if (!TryGetParameter(dictionary, key, out object value))
            return fallback;
        if (value is Variant variant)
        {
            return variant.VariantType switch
            {
                Variant.Type.Int => variant.AsInt32(),
                Variant.Type.Float => (int)variant.AsDouble(),
                Variant.Type.Bool => variant.AsBool() ? 1 : 0,
                Variant.Type.String => int.TryParse(variant.AsString(), out int parsed)
                    ? parsed
                    : 0,
                Variant.Type.StringName => int.TryParse(
                    variant.AsStringName().ToString(),
                    out int parsed
                )
                    ? parsed
                    : 0,
                _ => 0,
            };
        }
        return value switch
        {
            int intValue => intValue,
            long longValue => (int)longValue,
            float floatValue => (int)floatValue,
            double doubleValue => (int)doubleValue,
            bool boolValue => boolValue ? 1 : 0,
            string stringValue => int.TryParse(stringValue, out int parsed) ? parsed : 0,
            StringName stringName => int.TryParse(stringName.ToString(), out int parsed)
                ? parsed
                : 0,
            _ => 0,
        };
    }

    internal static bool TryReadLevelOverrideInt(
        Array<string> errors,
        StringName skillId,
        object overrideLevelKey,
        Dictionary overrideDict,
        string fieldName,
        out int value
    )
    {
        if (!TryGetParameter(overrideDict, fieldName, out object rawValue))
        {
            value = 0;
            return false;
        }
        if (TryStrictInt(rawValue, out value))
        {
            return true;
        }
        errors.Add(
            $"Skill {skillId} combat_profile level override {overrideLevelKey}.{fieldName} must be an int."
        );
        return false;
    }

    internal static string DictString(Dictionary dictionary, string key, string fallback = "")
    {
        if (!TryGetParameter(dictionary, key, out object value))
            return fallback;
        return value is Variant variant ? variant.AsString() : value?.ToString() ?? "";
    }

    internal static StringName DictStringName(
        Dictionary dictionary,
        string key,
        StringName fallback = default
    )
    {
        return TryGetParameter(dictionary, key, out object value)
            ? ProgressionDataUtils.to_string_name(value)
            : fallback;
    }

    internal static bool TryAsArray(object rawValue, out Array value)
    {
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Array)
        {
            value = variant.AsGodotArray();
            return true;
        }
        if (rawValue is Array array)
        {
            value = array;
            return true;
        }
        value = new Array();
        return false;
    }

    internal static bool TryAsDictionary(object rawValue, out Dictionary value)
    {
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Dictionary)
        {
            value = variant.AsGodotDictionary();
            return true;
        }
        if (rawValue is Dictionary dictionary)
        {
            value = dictionary;
            return true;
        }
        value = new Dictionary();
        return false;
    }

    private static bool TryStrictBool(object rawValue, out bool value)
    {
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Bool)
        {
            value = variant.AsBool();
            return true;
        }
        if (rawValue is bool boolValue)
        {
            value = boolValue;
            return true;
        }
        value = false;
        return false;
    }

    internal static bool TryStrictInt(object rawValue, out int value)
    {
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Int)
        {
            value = variant.AsInt32();
            return true;
        }
        if (rawValue is int intValue)
        {
            value = intValue;
            return true;
        }
        value = 0;
        return false;
    }

    private static bool TryStrictString(object rawValue, out string value)
    {
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.String)
        {
            value = variant.AsString();
            return true;
        }
        if (rawValue is string stringValue)
        {
            value = stringValue;
            return true;
        }
        value = "";
        return false;
    }

    internal static bool TryGetParameter(Dictionary dictionary, string key, out object value)
    {
        if (dictionary != null && dictionary.ContainsKey(key))
        {
            value = dictionary[key];
            return true;
        }
        value = null;
        return false;
    }

    internal static bool TryGetDictionaryValue(Dictionary dictionary, object key, out object value)
    {
        Variant variantKey = ToVariantKey(key);
        if (dictionary != null && dictionary.ContainsKey(variantKey))
        {
            value = dictionary[variantKey];
            return true;
        }
        value = null;
        return false;
    }

    private static Variant ToVariantKey(object key)
    {
        return key switch
        {
            Variant variant => variant,
            StringName stringName => Variant.From(stringName),
            string text => Variant.From(text),
            int intValue => Variant.From(intValue),
            long longValue => Variant.From(longValue),
            float floatValue => Variant.From(floatValue),
            double doubleValue => Variant.From(doubleValue),
            bool boolValue => Variant.From(boolValue),
            Vector2I coord => Variant.From(coord),
            _ => Variant.From(key?.ToString() ?? ""),
        };
    }

    private static T GetTyped<T>(Dictionary dictionary, StringName key)
        where T : class
    {
        if (dictionary.ContainsKey(key))
            return dictionary[key].AsGodotObject() as T;
        return null;
    }

    private static void AppendArray(List<string> target, Array<string> source)
    {
        foreach (string value in source)
            target.Add(value);
    }

    private static Array<string> ToGodotStringArray(IEnumerable<string> values)
    {
        var result = new Array<string>();
        if (values == null)
            return result;
        foreach (string value in values)
            result.Add(value);
        return result;
    }
}
