#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
    Fixed,
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
    Radius,
    Cross,
    Line,
    Cone,
    NarrowCone,
    FrontArc,
}

internal enum CombatEffectImportKind
{
    BodySizeCategoryOverride,
    ChainDamage,
    Charge,
    CleanseHarmful,
    Damage,
    DispelMagic,
    EquipmentDurabilityDamage,
    EraseStatus,
    Execute,
    FixedRepeatAttack,
    ForcedMove,
    GradedSaveExecute,
    Heal,
    HealFatal,
    HeightDelta,
    LayeredBarrier,
    OnKillGainResources,
    PathStepAoe,
    PositionSwap,
    RepeatAttackUntilFail,
    Shield,
    SourceRetreat,
    StaminaRestore,
    Status,
    TerrainEffect,
    TerrainReplace,
    TerrainReplaceTo,
    VaultBehindTarget,
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

internal static partial class CombatEffectImportClosedSpec
{
    internal const string LayeredBarrierKindValue = "layered_barrier";

    internal static IReadOnlyList<string> LayeredBarrierRequiredPayloadPropertyNames { get; } =
        new ReadOnlyCollection<string>(
            new[] { "area_pattern", "profile_id", "radius_cells", "save_dc" }
        );

    internal static bool TryParseKind(string? value, out CombatEffectImportKind result)
    {
        return SkillFullCombatEffectClosedSpec.TryParseKind(value, out result);
    }

    internal static bool IsPayloadCompatible(
        CombatEffectImportKind kind,
        ICombatEffectPayloadImportModel payload
    ) =>
        SkillFullCombatEffectClosedSpec.IsPayloadCompatible(kind, payload);
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
            case "fixed": result = CombatSkillImportRangePattern.Fixed; return true;
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
            case "radius": result = CombatSkillImportAreaPattern.Radius; return true;
            case "cross": result = CombatSkillImportAreaPattern.Cross; return true;
            case "line": result = CombatSkillImportAreaPattern.Line; return true;
            case "cone": result = CombatSkillImportAreaPattern.Cone; return true;
            case "narrow_cone": result = CombatSkillImportAreaPattern.NarrowCone; return true;
            case "front_arc": result = CombatSkillImportAreaPattern.FrontArc; return true;
            default:
                result = default;
                return false;
        }
    }

    internal static bool TryParsePendingCastBindingMode(
        string? value,
        out PendingCastBindingModeKind result
    )
    {
        switch (value)
        {
            case "soft_anchor": result = PendingCastBindingModeKind.SoftAnchor; return true;
            case "hard_anchor": result = PendingCastBindingModeKind.HardAnchor; return true;
            case "ground_bind": result = PendingCastBindingModeKind.GroundBind; return true;
            default:
                result = default;
                return false;
        }
    }

    internal static bool TryParseLevelOverrideAttackResolutionMode(
        string? value,
        out CombatSkillLevelOverrideAttackResolutionMode result
    )
    {
        switch (value)
        {
            case "auto": result = CombatSkillLevelOverrideAttackResolutionMode.Auto; return true;
            case "direct_effect": result = CombatSkillLevelOverrideAttackResolutionMode.DirectEffect; return true;
            case "fate_attack": result = CombatSkillLevelOverrideAttackResolutionMode.FateAttack; return true;
            case "force_hit_no_crit": result = CombatSkillLevelOverrideAttackResolutionMode.ForceHitNoCrit; return true;
            default:
                result = default;
                return false;
        }
    }

    internal static bool TryParseLevelOverrideAttackDefenseMode(
        string? value,
        out CombatSkillLevelOverrideAttackDefenseMode result
    )
    {
        switch (value)
        {
            case "normal": result = CombatSkillLevelOverrideAttackDefenseMode.Normal; return true;
            case "touch": result = CombatSkillLevelOverrideAttackDefenseMode.Touch; return true;
            case "flat_footed": result = CombatSkillLevelOverrideAttackDefenseMode.FlatFooted; return true;
            default:
                result = default;
                return false;
        }
    }

    internal static bool TryParseLevelOverrideAreaPattern(
        string? value,
        out CombatSkillLevelOverrideAreaPattern result
    )
    {
        switch (value)
        {
            case "single": result = CombatSkillLevelOverrideAreaPattern.Single; return true;
            case "self": result = CombatSkillLevelOverrideAreaPattern.Self; return true;
            case "diamond": result = CombatSkillLevelOverrideAreaPattern.Diamond; return true;
            case "square": result = CombatSkillLevelOverrideAreaPattern.Square; return true;
            case "radius": result = CombatSkillLevelOverrideAreaPattern.Radius; return true;
            case "cross": result = CombatSkillLevelOverrideAreaPattern.Cross; return true;
            case "line": result = CombatSkillLevelOverrideAreaPattern.Line; return true;
            case "cone": result = CombatSkillLevelOverrideAreaPattern.Cone; return true;
            case "narrow_cone": result = CombatSkillLevelOverrideAreaPattern.NarrowCone; return true;
            case "front_arc": result = CombatSkillLevelOverrideAreaPattern.FrontArc; return true;
            default:
                result = default;
                return false;
        }
    }

    internal static bool TryParseEffectKind(
        string? value,
        out CombatEffectImportKind result
    ) => CombatEffectImportClosedSpec.TryParseKind(value, out result);

    internal static string GetWireValue(SkillImportType value) => value switch
    {
        SkillImportType.Active => "active",
        SkillImportType.Passive => "passive",
        _ => throw Unknown(value),
    };

    internal static string GetWireValue(SkillImportLearnSource value) => value switch
    {
        SkillImportLearnSource.Book => "book",
        SkillImportLearnSource.Innate => "innate",
        SkillImportLearnSource.Internal => "internal",
        SkillImportLearnSource.Player => "player",
        SkillImportLearnSource.Profession => "profession",
        SkillImportLearnSource.Race => "race",
        SkillImportLearnSource.Subrace => "subrace",
        SkillImportLearnSource.Ascension => "ascension",
        SkillImportLearnSource.Bloodline => "bloodline",
        _ => throw Unknown(value),
    };

    internal static string GetWireValue(CombatSkillImportTargetMode value) => value switch
    {
        CombatSkillImportTargetMode.Unit => "unit",
        CombatSkillImportTargetMode.Ground => "ground",
        _ => throw Unknown(value),
    };

    internal static string GetWireValue(CombatSkillImportTargetTeamFilter value) => value switch
    {
        CombatSkillImportTargetTeamFilter.Self => "self",
        CombatSkillImportTargetTeamFilter.Ally => "ally",
        CombatSkillImportTargetTeamFilter.Enemy => "enemy",
        CombatSkillImportTargetTeamFilter.Any => "any",
        _ => throw Unknown(value),
    };

    internal static string GetWireValue(CombatSkillImportRangePattern value) => value switch
    {
        CombatSkillImportRangePattern.Single => "single",
        CombatSkillImportRangePattern.Fixed => "fixed",
        CombatSkillImportRangePattern.Diamond => "diamond",
        CombatSkillImportRangePattern.Square => "square",
        CombatSkillImportRangePattern.Line => "line",
        _ => throw Unknown(value),
    };

    internal static string GetWireValue(CombatSkillImportAreaPattern value) => value switch
    {
        CombatSkillImportAreaPattern.Single => "single",
        CombatSkillImportAreaPattern.Self => "self",
        CombatSkillImportAreaPattern.Diamond => "diamond",
        CombatSkillImportAreaPattern.Square => "square",
        CombatSkillImportAreaPattern.Radius => "radius",
        CombatSkillImportAreaPattern.Cross => "cross",
        CombatSkillImportAreaPattern.Line => "line",
        CombatSkillImportAreaPattern.Cone => "cone",
        CombatSkillImportAreaPattern.NarrowCone => "narrow_cone",
        CombatSkillImportAreaPattern.FrontArc => "front_arc",
        _ => throw Unknown(value),
    };

    internal static string GetWireValue(PendingCastBindingModeKind value) => value switch
    {
        PendingCastBindingModeKind.SoftAnchor => "soft_anchor",
        PendingCastBindingModeKind.HardAnchor => "hard_anchor",
        PendingCastBindingModeKind.GroundBind => "ground_bind",
        _ => throw Unknown(value),
    };

    internal static string GetWireValue(
        CombatSkillLevelOverrideAttackResolutionMode value
    ) => value switch
    {
        CombatSkillLevelOverrideAttackResolutionMode.Auto => "auto",
        CombatSkillLevelOverrideAttackResolutionMode.DirectEffect => "direct_effect",
        CombatSkillLevelOverrideAttackResolutionMode.FateAttack => "fate_attack",
        CombatSkillLevelOverrideAttackResolutionMode.ForceHitNoCrit => "force_hit_no_crit",
        _ => throw Unknown(value),
    };

    internal static string GetWireValue(
        CombatSkillLevelOverrideAttackDefenseMode value
    ) => value switch
    {
        CombatSkillLevelOverrideAttackDefenseMode.Normal => "normal",
        CombatSkillLevelOverrideAttackDefenseMode.Touch => "touch",
        CombatSkillLevelOverrideAttackDefenseMode.FlatFooted => "flat_footed",
        _ => throw Unknown(value),
    };

    internal static string GetWireValue(CombatSkillLevelOverrideAreaPattern value) => value switch
    {
        CombatSkillLevelOverrideAreaPattern.Single => "single",
        CombatSkillLevelOverrideAreaPattern.Self => "self",
        CombatSkillLevelOverrideAreaPattern.Diamond => "diamond",
        CombatSkillLevelOverrideAreaPattern.Square => "square",
        CombatSkillLevelOverrideAreaPattern.Radius => "radius",
        CombatSkillLevelOverrideAreaPattern.Cross => "cross",
        CombatSkillLevelOverrideAreaPattern.Line => "line",
        CombatSkillLevelOverrideAreaPattern.Cone => "cone",
        CombatSkillLevelOverrideAreaPattern.NarrowCone => "narrow_cone",
        CombatSkillLevelOverrideAreaPattern.FrontArc => "front_arc",
        _ => throw Unknown(value),
    };

    private static ArgumentOutOfRangeException Unknown<T>(T value) where T : struct =>
        new(nameof(value), value, "Unregistered skill import enum value.");

    private static bool IsLowerAscii(char value) => value is >= 'a' and <= 'z';
}

internal sealed class SkillTypeSchemaValues : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values { get; } =
        Array.AsReadOnly(new[] { "active", "passive" });
}

internal sealed class SkillLearnSourceSchemaValues : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values { get; } = Array.AsReadOnly(
        new[]
        {
            "book", "innate", "internal", "player", "profession", "race", "subrace",
            "ascension", "bloodline",
        }
    );
}

internal sealed class SkillTargetModeSchemaValues : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values { get; } =
        Array.AsReadOnly(new[] { "unit", "ground" });
}

internal sealed class SkillTargetTeamFilterSchemaValues
    : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values { get; } =
        Array.AsReadOnly(new[] { "self", "ally", "enemy", "any" });
}

internal sealed class SkillRangePatternSchemaValues : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values { get; } =
        Array.AsReadOnly(new[] { "single", "fixed", "diamond", "square", "line" });
}

internal sealed class SkillAreaPatternSchemaValues : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values { get; } =
        Array.AsReadOnly(
            new[]
            {
                "single", "self", "diamond", "square", "radius", "cross", "line",
                "cone", "narrow_cone", "front_arc",
            }
        );
}

internal sealed class SkillPendingCastBindingModeSchemaValues
    : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values { get; } =
        Array.AsReadOnly(new[] { "soft_anchor", "hard_anchor", "ground_bind" });
}

internal sealed class SkillAttackResolutionModeSchemaValues
    : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values { get; } = Array.AsReadOnly(
        new[] { "auto", "direct_effect", "fate_attack", "force_hit_no_crit" }
    );
}

internal sealed class SkillAttackDefenseModeSchemaValues
    : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values { get; } =
        Array.AsReadOnly(new[] { "normal", "touch", "flat_footed" });
}

internal sealed class SkillLevelOverrideAreaPatternSchemaValues
    : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values { get; } = Array.AsReadOnly(
        new[]
        {
            "single", "self", "diamond", "square", "radius", "cross", "line", "cone",
            "narrow_cone", "front_arc",
        }
    );
}

internal sealed partial class SkillImportModel
{
    internal SkillImportIdentifier SkillId { get; }
    internal string DisplayName { get; }
    internal string Description { get; }
    internal SkillImportType SkillType { get; }
    internal int MaxLevel { get; }
    internal SkillImportLearnSource LearnSource { get; }
    internal IReadOnlyList<SkillImportIdentifier> Tags { get; }
    internal string LevelDescriptionTemplate { get; }
    internal IReadOnlyDictionary<int, SkillDescriptionVariables> LevelDescriptionConfigs { get; }
    internal CombatSkillImportModel? CombatProfile { get; }
}

internal sealed partial class CombatSkillImportModel
{
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
    internal IReadOnlyDictionary<int, CombatSkillLevelOverrideImportModel> LevelOverrides { get; }
}

internal sealed partial class CombatEffectImportModel
{
    internal CombatEffectImportModel(
        CombatEffectImportKind kind,
        int minSkillLevel,
        int maxSkillLevel,
        int power,
        int durationTu,
        ICombatEffectPayloadImportModel payload
    ) : this(kind, payload)
    {
        MinSkillLevel = minSkillLevel;
        MaxSkillLevel = maxSkillLevel;
        Power = power;
        DurationTu = durationTu;
    }

    internal CombatEffectImportModel(
        CombatEffectImportKind kind,
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
        Payload = payload;
    }

    internal CombatEffectImportKind Kind { get; }
    internal int MinSkillLevel { get; init; }
    internal int MaxSkillLevel { get; init; } = -1;
    internal int Power { get; init; }
    internal int DurationTu { get; init; }
    internal ICombatEffectPayloadImportModel Payload { get; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed partial class SkillJsonDto
{
    private string? _description;
    private string? _skillType;
    private string? _learnSource;
    private IReadOnlyList<string>? _tags;
    private string? _levelDescriptionTemplate;
    private IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? _levelDescriptionConfigs;

    [JsonPropertyName("skill_id")]
    [JsonRequired]
    public string SkillId { get; init; } = null!;

    [JsonPropertyName("display_name")]
    [JsonRequired]
    public string DisplayName { get; init; } = null!;

    [JsonPropertyName("description")]
    public string Description { get => _description ?? ""; init => _description = value; }

    [JsonPropertyName("skill_type")]
    [ContentJsonSchemaStableStringValues(typeof(SkillTypeSchemaValues))]
    public string SkillType { get => _skillType ?? "active"; init => _skillType = value; }

    [JsonPropertyName("max_level")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? MaxLevel { get; init; }

    [JsonPropertyName("learn_source")]
    [ContentJsonSchemaStableStringValues(typeof(SkillLearnSourceSchemaValues))]
    public string LearnSource { get => _learnSource ?? "book"; init => _learnSource = value; }

    [JsonPropertyName("tags")]
    public IReadOnlyList<string> Tags { get => _tags ?? Array.Empty<string>(); init => _tags = value; }

    [JsonPropertyName("level_description_template")]
    public string LevelDescriptionTemplate
    {
        get => _levelDescriptionTemplate ?? "";
        init => _levelDescriptionTemplate = value;
    }

    [JsonPropertyName("level_description_configs")]
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> LevelDescriptionConfigs
    {
        get => _levelDescriptionConfigs ?? EmptyLevelDescriptionConfigs;
        init => _levelDescriptionConfigs = value;
    }

    [JsonPropertyName("combat_profile")]
    public CombatSkillJsonDto? CombatProfile { get; init; }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> EmptyLevelDescriptionConfigs { get; } =
        new ReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>(
            new Dictionary<string, IReadOnlyDictionary<string, string>>()
        );
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed partial class CombatSkillJsonDto
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
    [ContentJsonSchemaStableStringValues(typeof(SkillTargetModeSchemaValues))]
    public string TargetMode { get => _targetMode ?? "unit"; init => _targetMode = value; }

    [JsonPropertyName("target_team_filter")]
    [ContentJsonSchemaStableStringValues(typeof(SkillTargetTeamFilterSchemaValues))]
    public string TargetTeamFilter { get => _targetTeamFilter ?? "enemy"; init => _targetTeamFilter = value; }

    [JsonPropertyName("range_pattern")]
    [ContentJsonSchemaStableStringValues(typeof(SkillRangePatternSchemaValues))]
    public string RangePattern { get => _rangePattern ?? "single"; init => _rangePattern = value; }

    [JsonPropertyName("range_value")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? RangeValue { get; init; }

    [JsonPropertyName("area_pattern")]
    [ContentJsonSchemaStableStringValues(typeof(SkillAreaPatternSchemaValues))]
    public string AreaPattern { get => _areaPattern ?? "single"; init => _areaPattern = value; }

    [JsonPropertyName("ap_cost")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? ApCost { get; init; }

    [JsonPropertyName("mp_cost")]
    [ContentJsonSchemaDisallowExplicitNull]
    [Description(
        "Base mana-point cost for one cast. The canonical JSON field is mp_cost; mana_cost is not supported."
    )]
    public int? MpCost { get; init; }

    [JsonPropertyName("cooldown_tu")]
    [ContentJsonSchemaDisallowExplicitNull]
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
internal sealed partial class CombatEffectJsonDto
{
    [JsonPropertyName("effect_type")]
    [JsonRequired]
    public string EffectType { get; init; } = null!;

    [JsonPropertyName("min_skill_level")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? MinSkillLevel { get; init; }

    [JsonPropertyName("max_skill_level")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? MaxSkillLevel { get; init; }

    [JsonPropertyName("power")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? Power { get; init; }

    [JsonPropertyName("duration_tu")]
    [ContentJsonSchemaDisallowExplicitNull]
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
    public IReadOnlyList<ContentJsonSchemaClosedKindBranch> Branches =>
        SkillFullCombatEffectClosedSpec.SchemaBranches;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class LayeredBarrierEffectPayloadJsonDto
{
    [JsonPropertyName("area_pattern")]
    [JsonRequired]
    [ContentJsonSchemaStableStringValues(typeof(SkillAreaPatternSchemaValues))]
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
    [ContentJsonSchemaDisallowExplicitNull]
    public int? ApCost { get; init; }

    [JsonPropertyName("mp_cost")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? MpCost { get; init; }

    [JsonPropertyName("stamina_cost")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? StaminaCost { get; init; }

    [JsonPropertyName("mp_cost_per_target_slot")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? MpCostPerTargetSlot { get; init; }

    [JsonPropertyName("stamina_cost_per_target_slot")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? StaminaCostPerTargetSlot { get; init; }

    [JsonPropertyName("aura_cost")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? AuraCost { get; init; }

    [JsonPropertyName("cooldown_tu")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? CooldownTu { get; init; }

    [JsonPropertyName("casting_time_tu")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? CastingTimeTu { get; init; }

    [JsonPropertyName("casting_maintenance_dc")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? CastingMaintenanceDc { get; init; }

    [JsonPropertyName("casting_spell_control_dc")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? CastingSpellControlDc { get; init; }

    [JsonPropertyName("pending_cast_binding_mode")]
    [ContentJsonSchemaStableStringValues(typeof(SkillPendingCastBindingModeSchemaValues))]
    [ContentJsonSchemaDisallowExplicitNull]
    public string? PendingCastBindingMode { get; init; }

    [JsonPropertyName("attack_roll_bonus")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? AttackRollBonus { get; init; }

    [JsonPropertyName("attack_resolution_mode")]
    [ContentJsonSchemaStableStringValues(typeof(SkillAttackResolutionModeSchemaValues))]
    [ContentJsonSchemaDisallowExplicitNull]
    public string? AttackResolutionMode { get; init; }

    [JsonPropertyName("attack_defense_mode")]
    [ContentJsonSchemaStableStringValues(typeof(SkillAttackDefenseModeSchemaValues))]
    [ContentJsonSchemaDisallowExplicitNull]
    public string? AttackDefenseMode { get; init; }

    [JsonPropertyName("area_value")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? AreaValue { get; init; }

    [JsonPropertyName("range_value")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? RangeValue { get; init; }

    [JsonPropertyName("area_pattern")]
    [ContentJsonSchemaStableStringValues(typeof(SkillLevelOverrideAreaPatternSchemaValues))]
    [ContentJsonSchemaDisallowExplicitNull]
    public string? AreaPattern { get; init; }

    [JsonPropertyName("max_target_count")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? MaxTargetCount { get; init; }

    [JsonPropertyName("random_chain_attack_count")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? RandomChainAttackCount { get; init; }
}

[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Metadata,
    PropertyNamingPolicy = JsonKnownNamingPolicy.Unspecified
)]
[JsonSerializable(typeof(SkillJsonDto))]
[JsonSerializable(typeof(CombatSkillJsonDto))]
[JsonSerializable(typeof(CombatEffectJsonDto))]
[JsonSerializable(typeof(EmptyCombatEffectPayloadJsonDto))]
[JsonSerializable(typeof(StatusEffectPayloadJsonDto))]
[JsonSerializable(typeof(HealEffectPayloadJsonDto))]
[JsonSerializable(typeof(EquipmentDurabilityDamageEffectPayloadJsonDto))]
[JsonSerializable(typeof(RepeatAttackUntilFailEffectPayloadJsonDto))]
[JsonSerializable(typeof(LayeredBarrierEffectPayloadJsonDto))]
[JsonSerializable(typeof(GradedSaveExecuteEffectPayloadJsonDto))]
[JsonSerializable(typeof(DispelMagicEffectPayloadJsonDto))]
[JsonSerializable(typeof(OnKillGainResourcesEffectPayloadJsonDto))]
[JsonSerializable(typeof(CombatEffectSlotWeightJsonDto))]
[JsonSerializable(typeof(CombatDamageSegmentJsonDto))]
[JsonSerializable(typeof(CombatTargetDamageMultiplierRuleJsonDto))]
[JsonSerializable(typeof(CombatWeightedStatusOutcomeJsonDto))]
[JsonSerializable(typeof(SkillLevelOverrideJsonDto))]
[JsonSerializable(typeof(CombatCastVariantPayloadJsonDto))]
internal partial class SkillJsonImportSerializerContext : JsonSerializerContext { }
