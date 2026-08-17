#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

internal readonly record struct SkillImportIdentifier
{
    private SkillImportIdentifier(string value)
    {
        Value = value;
    }

    internal string Value { get; }

    internal static bool TryCreate(string? value, out SkillImportIdentifier identifier)
    {
        identifier = default;
        if (!SkillJsonImportValueRules.IsSnakeCaseId(value))
            return false;

        identifier = new SkillImportIdentifier(value!);
        return true;
    }

    public override string ToString() => Value ?? "";
}

internal enum SkillImportType
{
    Active,
    Passive,
}

internal enum SkillImportLearnSource
{
    Book,
    Innate,
    Internal,
    Player,
    Profession,
    Race,
    Subrace,
    Ascension,
    Bloodline,
}

internal enum CombatSkillImportTargetMode
{
    Unit,
    Ground,
}

internal enum CombatSkillImportTargetTeamFilter
{
    Self,
    Ally,
    Enemy,
    Any,
}

internal enum CombatSkillImportRangePattern
{
    Single,
    Diamond,
    Square,
    Line,
}

internal enum CombatSkillImportAreaPattern
{
    Single,
    Self,
    Diamond,
    Square,
    Line,
}

internal enum CombatEffectImportKind
{
    LayeredBarrier,
}

internal interface ICombatEffectPayloadImportModel { }

internal sealed class LayeredBarrierEffectPayloadImportModel
    : ICombatEffectPayloadImportModel
{
    internal LayeredBarrierEffectPayloadImportModel(
        CombatSkillImportAreaPattern areaPattern,
        SkillImportIdentifier profileId,
        int radiusCells,
        int saveDc
    )
    {
        AreaPattern = areaPattern;
        ProfileId = profileId;
        RadiusCells = radiusCells;
        SaveDc = saveDc;
    }

    internal CombatSkillImportAreaPattern AreaPattern { get; }
    internal SkillImportIdentifier ProfileId { get; }
    internal int RadiusCells { get; }
    internal int SaveDc { get; }
}

internal static class CombatEffectImportClosedSpec
{
    internal const string LayeredBarrierKindValue = "layered_barrier";

    internal static IReadOnlyList<string> LayeredBarrierRequiredPayloadPropertyNames { get; } =
        new ReadOnlyCollection<string>(
            new[] { "area_pattern", "profile_id", "radius_cells", "save_dc" }
        );

    internal static bool TryParseKind(string? value, out CombatEffectImportKind result)
    {
        if (string.Equals(value, LayeredBarrierKindValue, StringComparison.Ordinal))
        {
            result = CombatEffectImportKind.LayeredBarrier;
            return true;
        }

        result = default;
        return false;
    }

    internal static bool IsPayloadCompatible(
        CombatEffectImportKind kind,
        ICombatEffectPayloadImportModel payload
    ) =>
        kind switch
        {
            CombatEffectImportKind.LayeredBarrier =>
                payload is LayeredBarrierEffectPayloadImportModel,
            _ => false,
        };
}

internal static class SkillJsonImportValueRules
{
    internal static bool IsSnakeCaseId(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 128 || !IsLowerAscii(value[0]))
            return false;

        bool previousWasUnderscore = false;
        for (int index = 1; index < value.Length; index += 1)
        {
            char character = value[index];
            if (character == '_')
            {
                if (previousWasUnderscore)
                    return false;
                previousWasUnderscore = true;
                continue;
            }

            if (!IsLowerAscii(character) && !char.IsAsciiDigit(character))
                return false;
            previousWasUnderscore = false;
        }

        return !previousWasUnderscore;
    }

    internal static bool TryParseSkillType(string? value, out SkillImportType result)
    {
        switch (value)
        {
            case "active":
                result = SkillImportType.Active;
                return true;
            case "passive":
                result = SkillImportType.Passive;
                return true;
            default:
                result = default;
                return false;
        }
    }

    internal static bool TryParseLearnSource(string? value, out SkillImportLearnSource result)
    {
        switch (value)
        {
            case "book": result = SkillImportLearnSource.Book; return true;
            case "innate": result = SkillImportLearnSource.Innate; return true;
            case "internal": result = SkillImportLearnSource.Internal; return true;
            case "player": result = SkillImportLearnSource.Player; return true;
            case "profession": result = SkillImportLearnSource.Profession; return true;
            case "race": result = SkillImportLearnSource.Race; return true;
            case "subrace": result = SkillImportLearnSource.Subrace; return true;
            case "ascension": result = SkillImportLearnSource.Ascension; return true;
            case "bloodline": result = SkillImportLearnSource.Bloodline; return true;
            default:
                result = default;
                return false;
        }
    }

    internal static bool TryParseTargetMode(
        string? value,
        out CombatSkillImportTargetMode result
    )
    {
        switch (value)
        {
            case "unit": result = CombatSkillImportTargetMode.Unit; return true;
            case "ground": result = CombatSkillImportTargetMode.Ground; return true;
            default:
                result = default;
                return false;
        }
    }

    internal static bool TryParseTargetTeamFilter(
        string? value,
        out CombatSkillImportTargetTeamFilter result
    )
    {
        switch (value)
        {
            case "self": result = CombatSkillImportTargetTeamFilter.Self; return true;
            case "ally": result = CombatSkillImportTargetTeamFilter.Ally; return true;
            case "enemy": result = CombatSkillImportTargetTeamFilter.Enemy; return true;
            case "any": result = CombatSkillImportTargetTeamFilter.Any; return true;
            default:
                result = default;
                return false;
        }
    }

    internal static bool TryParseRangePattern(
        string? value,
        out CombatSkillImportRangePattern result
    )
    {
        switch (value)
        {
            case "single": result = CombatSkillImportRangePattern.Single; return true;
            case "diamond": result = CombatSkillImportRangePattern.Diamond; return true;
            case "square": result = CombatSkillImportRangePattern.Square; return true;
            case "line": result = CombatSkillImportRangePattern.Line; return true;
            default:
                result = default;
                return false;
        }
    }

    internal static bool TryParseAreaPattern(
        string? value,
        out CombatSkillImportAreaPattern result
    )
    {
        switch (value)
        {
            case "single": result = CombatSkillImportAreaPattern.Single; return true;
            case "self": result = CombatSkillImportAreaPattern.Self; return true;
            case "diamond": result = CombatSkillImportAreaPattern.Diamond; return true;
            case "square": result = CombatSkillImportAreaPattern.Square; return true;
            case "line": result = CombatSkillImportAreaPattern.Line; return true;
            default:
                result = default;
                return false;
        }
    }

    internal static bool TryParseEffectKind(
        string? value,
        out CombatEffectImportKind result
    ) => CombatEffectImportClosedSpec.TryParseKind(value, out result);

    private static bool IsLowerAscii(char value) => value is >= 'a' and <= 'z';
}

internal sealed class SkillImportModel
{
    internal SkillImportModel(
        SkillImportIdentifier skillId,
        string displayName,
        string description,
        SkillImportType skillType,
        int maxLevel,
        SkillImportLearnSource learnSource,
        IEnumerable<SkillImportIdentifier> tags,
        CombatSkillImportModel? combatProfile
    )
    {
        ArgumentNullException.ThrowIfNull(displayName);
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(tags);

        SkillId = skillId;
        DisplayName = displayName;
        Description = description;
        SkillType = skillType;
        MaxLevel = maxLevel;
        LearnSource = learnSource;
        Tags = new ReadOnlyCollection<SkillImportIdentifier>(
            new List<SkillImportIdentifier>(tags)
        );
        CombatProfile = combatProfile;
    }

    internal SkillImportIdentifier SkillId { get; }
    internal string DisplayName { get; }
    internal string Description { get; }
    internal SkillImportType SkillType { get; }
    internal int MaxLevel { get; }
    internal SkillImportLearnSource LearnSource { get; }
    internal IReadOnlyList<SkillImportIdentifier> Tags { get; }
    internal CombatSkillImportModel? CombatProfile { get; }
}

internal sealed class CombatSkillImportModel
{
    internal CombatSkillImportModel(
        SkillImportIdentifier skillId,
        CombatSkillImportTargetMode targetMode,
        CombatSkillImportTargetTeamFilter targetTeamFilter,
        CombatSkillImportRangePattern rangePattern,
        int rangeValue,
        CombatSkillImportAreaPattern areaPattern,
        int apCost,
        int mpCost,
        int cooldownTu,
        IEnumerable<CombatEffectImportModel> effectDefs,
        IEnumerable<KeyValuePair<int, SkillLevelOverrideImportModel>> levelOverrides
    )
    {
        ArgumentNullException.ThrowIfNull(effectDefs);
        ArgumentNullException.ThrowIfNull(levelOverrides);

        SkillId = skillId;
        TargetMode = targetMode;
        TargetTeamFilter = targetTeamFilter;
        RangePattern = rangePattern;
        RangeValue = rangeValue;
        AreaPattern = areaPattern;
        ApCost = apCost;
        MpCost = mpCost;
        CooldownTu = cooldownTu;
        EffectDefs = new ReadOnlyCollection<CombatEffectImportModel>(
            new List<CombatEffectImportModel>(effectDefs)
        );
        var levelOverrideCopy = new SortedDictionary<int, SkillLevelOverrideImportModel>();
        foreach (KeyValuePair<int, SkillLevelOverrideImportModel> pair in levelOverrides)
            levelOverrideCopy.Add(pair.Key, pair.Value);
        LevelOverrides = new ReadOnlyDictionary<int, SkillLevelOverrideImportModel>(
            levelOverrideCopy
        );
    }

    internal SkillImportIdentifier SkillId { get; }
    internal CombatSkillImportTargetMode TargetMode { get; }
    internal CombatSkillImportTargetTeamFilter TargetTeamFilter { get; }
    internal CombatSkillImportRangePattern RangePattern { get; }
    internal int RangeValue { get; }
    internal CombatSkillImportAreaPattern AreaPattern { get; }
    internal int ApCost { get; }
    internal int MpCost { get; }
    internal int CooldownTu { get; }
    internal IReadOnlyList<CombatEffectImportModel> EffectDefs { get; }
    internal IReadOnlyDictionary<int, SkillLevelOverrideImportModel> LevelOverrides { get; }
}

internal sealed class CombatEffectImportModel
{
    internal CombatEffectImportModel(
        CombatEffectImportKind kind,
        int minSkillLevel,
        int maxSkillLevel,
        int power,
        int durationTu,
        ICombatEffectPayloadImportModel payload
    )
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (!CombatEffectImportClosedSpec.IsPayloadCompatible(kind, payload))
        {
            throw new ArgumentException(
                "Combat effect kind and typed payload are incompatible.",
                nameof(payload)
            );
        }

        Kind = kind;
        MinSkillLevel = minSkillLevel;
        MaxSkillLevel = maxSkillLevel;
        Power = power;
        DurationTu = durationTu;
        Payload = payload;
    }

    internal CombatEffectImportKind Kind { get; }
    internal int MinSkillLevel { get; }
    internal int MaxSkillLevel { get; }
    internal int Power { get; }
    internal int DurationTu { get; }
    internal ICombatEffectPayloadImportModel Payload { get; }
}

internal sealed class SkillLevelOverrideImportModel
{
    internal SkillLevelOverrideImportModel(
        int level,
        int? apCost,
        int? mpCost,
        int? cooldownTu
    )
    {
        Level = level;
        ApCost = apCost;
        MpCost = mpCost;
        CooldownTu = cooldownTu;
    }

    internal int Level { get; }
    internal int? ApCost { get; }
    internal int? MpCost { get; }
    internal int? CooldownTu { get; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class SkillJsonDto
{
    private string? _description;
    private string? _skillType;
    private string? _learnSource;
    private IReadOnlyList<string>? _tags;

    [JsonPropertyName("skill_id")]
    [JsonRequired]
    public string SkillId { get; init; } = null!;

    [JsonPropertyName("display_name")]
    [JsonRequired]
    public string DisplayName { get; init; } = null!;

    [JsonPropertyName("description")]
    public string Description { get => _description ?? ""; init => _description = value; }

    [JsonPropertyName("skill_type")]
    public string SkillType { get => _skillType ?? "active"; init => _skillType = value; }

    [JsonPropertyName("max_level")]
    public int? MaxLevel { get; init; }

    [JsonPropertyName("learn_source")]
    public string LearnSource { get => _learnSource ?? "book"; init => _learnSource = value; }

    [JsonPropertyName("tags")]
    public IReadOnlyList<string> Tags { get => _tags ?? Array.Empty<string>(); init => _tags = value; }

    [JsonPropertyName("combat_profile")]
    public CombatSkillJsonDto? CombatProfile { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class CombatSkillJsonDto
{
    private string? _targetMode;
    private string? _targetTeamFilter;
    private string? _rangePattern;
    private string? _areaPattern;
    private IReadOnlyList<CombatEffectJsonDto>? _effectDefs;
    private IReadOnlyDictionary<string, SkillLevelOverrideJsonDto>? _levelOverrides;

    [JsonPropertyName("skill_id")]
    [JsonRequired]
    public string SkillId { get; init; } = null!;

    [JsonPropertyName("target_mode")]
    public string TargetMode { get => _targetMode ?? "unit"; init => _targetMode = value; }

    [JsonPropertyName("target_team_filter")]
    public string TargetTeamFilter { get => _targetTeamFilter ?? "enemy"; init => _targetTeamFilter = value; }

    [JsonPropertyName("range_pattern")]
    public string RangePattern { get => _rangePattern ?? "single"; init => _rangePattern = value; }

    [JsonPropertyName("range_value")]
    public int? RangeValue { get; init; }

    [JsonPropertyName("area_pattern")]
    public string AreaPattern { get => _areaPattern ?? "single"; init => _areaPattern = value; }

    [JsonPropertyName("ap_cost")]
    public int? ApCost { get; init; }

    [JsonPropertyName("mp_cost")]
    public int? MpCost { get; init; }

    [JsonPropertyName("cooldown_tu")]
    public int? CooldownTu { get; init; }

    [JsonPropertyName("effect_defs")]
    public IReadOnlyList<CombatEffectJsonDto> EffectDefs
    {
        get => _effectDefs ?? Array.Empty<CombatEffectJsonDto>();
        init => _effectDefs = value;
    }

    [JsonPropertyName("level_overrides")]
    public IReadOnlyDictionary<string, SkillLevelOverrideJsonDto> LevelOverrides
    {
        get => _levelOverrides ?? EmptyLevelOverrides;
        init => _levelOverrides = value;
    }

    private static IReadOnlyDictionary<string, SkillLevelOverrideJsonDto> EmptyLevelOverrides { get; } =
        new ReadOnlyDictionary<string, SkillLevelOverrideJsonDto>(
            new Dictionary<string, SkillLevelOverrideJsonDto>()
        );
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[ContentJsonSchemaClosedKind(typeof(SkillCombatEffectClosedKindSchemaSpec))]
internal sealed class CombatEffectJsonDto
{
    [JsonPropertyName("effect_type")]
    [JsonRequired]
    public string EffectType { get; init; } = null!;

    [JsonPropertyName("min_skill_level")]
    public int? MinSkillLevel { get; init; }

    [JsonPropertyName("max_skill_level")]
    public int? MaxSkillLevel { get; init; }

    [JsonPropertyName("power")]
    public int? Power { get; init; }

    [JsonPropertyName("duration_tu")]
    public int? DurationTu { get; init; }

    [JsonPropertyName("payload")]
    [JsonRequired]
    // Schema carrier only. System.Text.Json materializes it as JsonElement and
    // SkillJsonImportParser.Parse consumes it synchronously; neither type may escape.
    public object Payload { get; init; } = null!;
}

internal sealed class SkillCombatEffectClosedKindSchemaSpec
    : IContentJsonSchemaClosedKindSpec
{
    public string DiscriminatorPropertyName => "effect_type";
    public string PayloadPropertyName => "payload";
    public IReadOnlyList<ContentJsonSchemaClosedKindBranch> Branches { get; } =
        Array.AsReadOnly(
            new[]
            {
                new ContentJsonSchemaClosedKindBranch(
                    CombatEffectImportClosedSpec.LayeredBarrierKindValue,
                    typeof(LayeredBarrierEffectPayloadJsonDto)
                ),
            }
        );
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class LayeredBarrierEffectPayloadJsonDto
{
    [JsonPropertyName("area_pattern")]
    [JsonRequired]
    public string AreaPattern { get; init; } = null!;

    [JsonPropertyName("profile_id")]
    [JsonRequired]
    public string ProfileId { get; init; } = null!;

    [JsonPropertyName("radius_cells")]
    [JsonRequired]
    public int RadiusCells { get; init; }

    [JsonPropertyName("save_dc")]
    [JsonRequired]
    public int SaveDc { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class SkillLevelOverrideJsonDto
{
    [JsonPropertyName("ap_cost")]
    public int? ApCost { get; init; }

    [JsonPropertyName("mp_cost")]
    public int? MpCost { get; init; }

    [JsonPropertyName("cooldown_tu")]
    public int? CooldownTu { get; init; }
}

[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Metadata,
    PropertyNamingPolicy = JsonKnownNamingPolicy.Unspecified
)]
[JsonSerializable(typeof(SkillJsonDto))]
[JsonSerializable(typeof(CombatSkillJsonDto))]
[JsonSerializable(typeof(CombatEffectJsonDto))]
[JsonSerializable(typeof(LayeredBarrierEffectPayloadJsonDto))]
[JsonSerializable(typeof(SkillLevelOverrideJsonDto))]
internal partial class SkillJsonImportSerializerContext : JsonSerializerContext { }
