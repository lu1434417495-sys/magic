#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

internal static class BattleSimJsonContentDomains
{
    internal const int SchemaVersion = 1;
    internal const string ProfileDomainId = "battle_sim_profiles";
    internal const string ScenarioDomainId = "battle_sim_scenarios";
    internal const string ProfileDirectory = "res://data/configs/json/battle_sim/profiles";
    internal const string ScenarioDirectory = "res://data/configs/json/battle_sim/scenarios";

    internal static ContentJsonSchemaDomainRegistration ProfileSchemaRegistration { get; } =
        new(
            ProfileDomainId,
            SchemaVersion,
            typeof(BattleSimProfileJsonDocumentDto),
            "Magic BattleSim profile JSON authoring schema",
            "Strict profile and closed override-patch contract.",
            "res://data/schemas/content/battle_sim_profiles.schema.json",
            "/data/configs/json/battle_sim/profiles/**/*.json"
        );

    internal static ContentJsonSchemaDomainRegistration ScenarioSchemaRegistration { get; } =
        new(
            ScenarioDomainId,
            SchemaVersion,
            typeof(BattleSimScenarioJsonDocumentDto),
            "Magic BattleSim scenario JSON authoring schema",
            "Strict scenario and unit contract projected to immutable definitions.",
            "res://data/schemas/content/battle_sim_scenarios.schema.json",
            "/data/configs/json/battle_sim/scenarios/**/*.json"
        );

    internal static IReadOnlyList<ContentJsonSchemaDomainRegistration> SchemaRegistrations { get; } =
        Array.AsReadOnly(new[] { ProfileSchemaRegistration, ScenarioSchemaRegistration });

    internal static JsonContentDomainDescriptor<BattleSimProfileJsonDto, BattleSimProfileImportModel>
        CreateProfileDescriptor(string directory, IContentJsonSourceReader reader) =>
        new(
            ProfileDomainId,
            SchemaVersion,
            "profile_id",
            directory,
            reader,
            new ContentJsonNullabilityPolicy(Array.Empty<string>()),
            BattleSimJsonImportParser.ParseProfile,
            BattleSimJsonImportParser.NormalizeProfile,
            BattleSimImportValidator.ValidateProfile
        );

    internal static JsonContentDomainDescriptor<BattleSimScenarioJsonDto, BattleSimScenarioImportModel>
        CreateScenarioDescriptor(string directory, IContentJsonSourceReader reader) =>
        new(
            ScenarioDomainId,
            SchemaVersion,
            "scenario_id",
            directory,
            reader,
            new ContentJsonNullabilityPolicy(Array.Empty<string>()),
            BattleSimJsonImportParser.ParseScenario,
            BattleSimJsonImportParser.NormalizeScenario,
            BattleSimImportValidator.ValidateScenario
        );

    internal static IEnumerable<IContentJsonOfflineValidationDomain> CreateOfflineDomains()
    {
        yield return new OfflineProfileDomain();
        yield return new OfflineScenarioDomain();
    }

    private sealed class OfflineProfileDomain : IContentJsonOfflineValidationDomain
    {
        public string DomainId => ProfileDomainId;
        public ContentJsonOfflineValidationReport Validate(string sourceDirectory, IContentJsonSourceReader sourceReader)
        {
            ContentImportBatch<BattleSimProfileImportModel> batch = CreateProfileDescriptor(sourceDirectory, sourceReader).Import();
            return new ContentJsonOfflineValidationReport(DomainId, batch.Entries.Count, batch.Diagnostics);
        }
    }

    private sealed class OfflineScenarioDomain : IContentJsonOfflineValidationDomain
    {
        public string DomainId => ScenarioDomainId;
        public ContentJsonOfflineValidationReport Validate(string sourceDirectory, IContentJsonSourceReader sourceReader)
        {
            ContentImportBatch<BattleSimScenarioImportModel> batch = CreateScenarioDescriptor(sourceDirectory, sourceReader).Import();
            return new ContentJsonOfflineValidationReport(DomainId, batch.Entries.Count, batch.Diagnostics);
        }
    }
}

internal static class BattleSimJsonRules
{
    internal const string InvalidProfileDto = "battle_sim.profile.dto.invalid";
    internal const string InvalidScenarioDto = "battle_sim.scenario.dto.invalid";
    internal const string InvalidPayload = "battle_sim.payload.invalid";
    internal const string UnknownKind = "battle_sim.kind.unknown";
    internal const string IdRequired = "battle_sim.id.required";
    internal const string IdMismatch = "battle_sim.id.mismatch";
    internal const string ValueOutOfRange = "battle_sim.value.out_of_range";
    internal const string DuplicateId = "battle_sim.id.duplicate";
}

[Description("BattleSim profile document envelope.")]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class BattleSimProfileJsonDocumentDto
{
    [JsonPropertyName("schema"), JsonRequired, ContentJsonSchemaConst(BattleSimJsonContentDomains.SchemaVersion)] public int Schema { get; init; }
    [JsonPropertyName("domain"), JsonRequired, ContentJsonSchemaConst(BattleSimJsonContentDomains.ProfileDomainId)] public string Domain { get; init; } = "";
    [JsonPropertyName("family"), JsonRequired] public string Family { get; init; } = "";
    [JsonPropertyName("templates"), JsonRequired, ContentJsonSchemaPartialObjectValues(typeof(ContentJsonTemplateReferenceSchemaDto))]
    public IReadOnlyDictionary<string, BattleSimProfileJsonDto> Templates { get; init; } = EmptyMap<BattleSimProfileJsonDto>.Value;
    [JsonPropertyName("entries"), JsonRequired, ContentJsonSchemaEntryControlMembers(typeof(ContentJsonTemplateReferenceSchemaDto), "profile_id", "template")]
    public IReadOnlyList<BattleSimProfileJsonDto> Entries { get; init; } = Array.Empty<BattleSimProfileJsonDto>();
}

[Description("BattleSim scenario document envelope.")]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class BattleSimScenarioJsonDocumentDto
{
    [JsonPropertyName("schema"), JsonRequired, ContentJsonSchemaConst(BattleSimJsonContentDomains.SchemaVersion)] public int Schema { get; init; }
    [JsonPropertyName("domain"), JsonRequired, ContentJsonSchemaConst(BattleSimJsonContentDomains.ScenarioDomainId)] public string Domain { get; init; } = "";
    [JsonPropertyName("family"), JsonRequired] public string Family { get; init; } = "";
    [JsonPropertyName("templates"), JsonRequired, ContentJsonSchemaPartialObjectValues(typeof(ContentJsonTemplateReferenceSchemaDto))]
    public IReadOnlyDictionary<string, BattleSimScenarioJsonDto> Templates { get; init; } = EmptyMap<BattleSimScenarioJsonDto>.Value;
    [JsonPropertyName("entries"), JsonRequired, ContentJsonSchemaEntryControlMembers(typeof(ContentJsonTemplateReferenceSchemaDto), "scenario_id", "template")]
    public IReadOnlyList<BattleSimScenarioJsonDto> Entries { get; init; } = Array.Empty<BattleSimScenarioJsonDto>();
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class BattleSimProfileJsonDto
{
    [JsonPropertyName("profile_id"), JsonRequired] public string ProfileId { get; init; } = "";
    [JsonPropertyName("display_name"), JsonRequired] public string DisplayName { get; init; } = "";
    [JsonPropertyName("description"), JsonRequired] public string Description { get; init; } = "";
    [JsonPropertyName("ai_score_profile"), JsonRequired] public BattleAiScoreProfileJsonDto AiScoreProfile { get; init; } = new();
    [JsonPropertyName("override_patches"), JsonRequired] public IReadOnlyList<BattleSimOverridePatchJsonDto> OverridePatches { get; init; } = Array.Empty<BattleSimOverridePatchJsonDto>();
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[ContentJsonSchemaClosedKind(typeof(BattleSimOverridePatchClosedKindSpec))]
internal sealed class BattleSimOverridePatchJsonDto
{
    [JsonPropertyName("kind"), JsonRequired] public string Kind { get; init; } = "";
    [JsonPropertyName("payload"), JsonRequired] public object Payload { get; init; } = null!;
}

internal sealed class BattleSimOverridePatchClosedKindSpec : IContentJsonSchemaClosedKindSpec
{
    public string DiscriminatorPropertyName => "kind";
    public string PayloadPropertyName => "payload";
    public IReadOnlyList<ContentJsonSchemaClosedKindBranch> Branches { get; } = Array.AsReadOnly(new[]
    {
        new ContentJsonSchemaClosedKindBranch("skill", typeof(BattleSimTargetPatchPayloadJsonDto)),
        new ContentJsonSchemaClosedKindBranch("brain", typeof(BattleSimTargetPatchPayloadJsonDto)),
        new ContentJsonSchemaClosedKindBranch("action", typeof(BattleSimActionPatchPayloadJsonDto)),
        new ContentJsonSchemaClosedKindBranch("ai_score_profile", typeof(BattleSimScorePatchPayloadJsonDto)),
        new ContentJsonSchemaClosedKindBranch("faction_ai_score_profile", typeof(BattleSimTargetPatchPayloadJsonDto)),
    });
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class BattleSimTargetPatchPayloadJsonDto
{
    [JsonPropertyName("target_id"), JsonRequired] public string TargetId { get; init; } = "";
    [JsonPropertyName("path"), JsonRequired] public string Path { get; init; } = "";
    [JsonPropertyName("value"), JsonRequired] public object Value { get; init; } = null!;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class BattleSimActionPatchPayloadJsonDto
{
    [JsonPropertyName("brain_id"), JsonRequired] public string BrainId { get; init; } = "";
    [JsonPropertyName("state_id"), JsonRequired] public string StateId { get; init; } = "";
    [JsonPropertyName("action_id"), JsonRequired] public string ActionId { get; init; } = "";
    [JsonPropertyName("path"), JsonRequired] public string Path { get; init; } = "";
    [JsonPropertyName("value"), JsonRequired] public object Value { get; init; } = null!;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class BattleSimScorePatchPayloadJsonDto
{
    [JsonPropertyName("path"), JsonRequired] public string Path { get; init; } = "";
    [JsonPropertyName("value"), JsonRequired] public object Value { get; init; } = null!;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class BattleSimCoordJsonDto
{
    [JsonPropertyName("x"), JsonRequired] public int X { get; init; }
    [JsonPropertyName("y"), JsonRequired] public int Y { get; init; }
    internal BattleSimCoordImportModel ToImport() => new(X, Y);
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class BattleSimScenarioJsonDto
{
    [JsonPropertyName("scenario_id"), JsonRequired] public string ScenarioId { get; init; } = "";
    [JsonPropertyName("display_name"), JsonRequired] public string DisplayName { get; init; } = "";
    [JsonPropertyName("description"), JsonRequired] public string Description { get; init; } = "";
    [JsonPropertyName("map_size"), JsonRequired] public BattleSimCoordJsonDto MapSize { get; init; } = new();
    [JsonPropertyName("terrain_profile_id"), JsonRequired] public string TerrainProfileId { get; init; } = "";
    [JsonPropertyName("use_formal_terrain_generation"), JsonRequired] public bool UseFormalTerrainGeneration { get; init; }
    [JsonPropertyName("world_coord"), JsonRequired] public BattleSimCoordJsonDto WorldCoord { get; init; } = new();
    [JsonPropertyName("ally_units"), JsonRequired] public IReadOnlyList<BattleSimUnitJsonDto> AllyUnits { get; init; } = Array.Empty<BattleSimUnitJsonDto>();
    [JsonPropertyName("enemy_units"), JsonRequired] public IReadOnlyList<BattleSimUnitJsonDto> EnemyUnits { get; init; } = Array.Empty<BattleSimUnitJsonDto>();
    [JsonPropertyName("cell_overrides"), JsonRequired] public IReadOnlyList<BattleSimCellOverrideJsonDto> CellOverrides { get; init; } = Array.Empty<BattleSimCellOverrideJsonDto>();
    [JsonPropertyName("timeline_ticks_per_step"), JsonRequired] public int TimelineTicksPerStep { get; init; }
    [JsonPropertyName("tu_per_tick"), JsonRequired] public int TuPerTick { get; init; }
    [JsonPropertyName("max_iterations"), JsonRequired] public int MaxIterations { get; init; }
    [JsonPropertyName("manual_policy"), JsonRequired] public string ManualPolicy { get; init; } = "";
    [JsonPropertyName("trace_enabled"), JsonRequired] public bool TraceEnabled { get; init; }
    [JsonPropertyName("seeds"), JsonRequired] public IReadOnlyList<int> Seeds { get; init; } = Array.Empty<int>();
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class BattleSimUnitJsonDto
{
    [JsonPropertyName("unit_id"), JsonRequired] public string UnitId { get; init; } = "";
    [JsonPropertyName("source_member_id"), JsonRequired] public string SourceMemberId { get; init; } = "";
    [JsonPropertyName("display_name"), JsonRequired] public string DisplayName { get; init; } = "";
    [JsonPropertyName("faction_id"), JsonRequired] public string FactionId { get; init; } = "";
    [JsonPropertyName("control_mode"), JsonRequired] public string ControlMode { get; init; } = "";
    [JsonPropertyName("ai_brain_id"), JsonRequired] public string AiBrainId { get; init; } = "";
    [JsonPropertyName("ai_state_id"), JsonRequired] public string AiStateId { get; init; } = "";
    [JsonPropertyName("coord"), JsonRequired] public BattleSimCoordJsonDto Coord { get; init; } = new();
    [JsonPropertyName("body_size"), JsonRequired] public int BodySize { get; init; }
    [JsonPropertyName("body_size_category"), JsonRequired] public string BodySizeCategory { get; init; } = "";
    [JsonPropertyName("current_hp"), JsonRequired] public int CurrentHp { get; init; }
    [JsonPropertyName("current_mp"), JsonRequired] public int CurrentMp { get; init; }
    [JsonPropertyName("current_stamina"), JsonRequired] public int CurrentStamina { get; init; }
    [JsonPropertyName("current_aura"), JsonRequired] public int CurrentAura { get; init; }
    [JsonPropertyName("current_ap"), JsonRequired] public int CurrentAp { get; init; }
    [JsonPropertyName("current_move_points"), JsonRequired] public int CurrentMovePoints { get; init; }
    [JsonPropertyName("action_threshold"), JsonRequired] public int ActionThreshold { get; init; }
    [JsonPropertyName("attribute_overrides"), JsonRequired] public IReadOnlyDictionary<string, int> AttributeOverrides { get; init; } = EmptyMap<int>.Value;
    [JsonPropertyName("skill_ids"), JsonRequired] public IReadOnlyList<string> SkillIds { get; init; } = Array.Empty<string>();
    [JsonPropertyName("skill_level_map"), JsonRequired] public IReadOnlyDictionary<string, int> SkillLevelMap { get; init; } = EmptyMap<int>.Value;
    [JsonPropertyName("movement_tags"), JsonRequired] public IReadOnlyList<string> MovementTags { get; init; } = Array.Empty<string>();
    [JsonPropertyName("unlocked_combat_resource_ids"), JsonRequired] public IReadOnlyList<string> UnlockedCombatResourceIds { get; init; } = Array.Empty<string>();
    [JsonPropertyName("base_attributes"), JsonRequired] public IReadOnlyDictionary<string, int> BaseAttributes { get; init; } = EmptyMap<int>.Value;
    [JsonPropertyName("weapon_projection")] public BattleSimWeaponProjectionJsonDto? WeaponProjection { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class BattleSimWeaponProjectionJsonDto
{
    [JsonPropertyName("weapon_profile_kind"), JsonRequired] public string WeaponProfileKind { get; init; } = "";
    [JsonPropertyName("weapon_item_id"), JsonRequired] public string WeaponItemId { get; init; } = "";
    [JsonPropertyName("weapon_profile_type_id"), JsonRequired] public string WeaponProfileTypeId { get; init; } = "";
    [JsonPropertyName("weapon_range_type"), JsonRequired] public string WeaponRangeType { get; init; } = "";
    [JsonPropertyName("weapon_family"), JsonRequired] public string WeaponFamily { get; init; } = "";
    [JsonPropertyName("weapon_current_grip"), JsonRequired] public string WeaponCurrentGrip { get; init; } = "";
    [JsonPropertyName("weapon_attack_range"), JsonRequired] public int WeaponAttackRange { get; init; }
    [JsonPropertyName("weapon_one_handed_dice"), JsonRequired] public BattleSimDiceJsonDto WeaponOneHandedDice { get; init; } = new();
    [JsonPropertyName("weapon_two_handed_dice"), JsonRequired] public BattleSimDiceJsonDto WeaponTwoHandedDice { get; init; } = new();
    [JsonPropertyName("weapon_is_versatile"), JsonRequired] public bool WeaponIsVersatile { get; init; }
    [JsonPropertyName("weapon_uses_two_hands"), JsonRequired] public bool WeaponUsesTwoHands { get; init; }
    [JsonPropertyName("weapon_physical_damage_tag"), JsonRequired] public string WeaponPhysicalDamageTag { get; init; } = "";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class BattleSimDiceJsonDto
{
    [JsonPropertyName("dice_count"), JsonRequired] public int DiceCount { get; init; }
    [JsonPropertyName("dice_sides"), JsonRequired] public int DiceSides { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class BattleSimCellOverrideJsonDto
{
    [JsonPropertyName("coord"), JsonRequired] public BattleSimCoordJsonDto Coord { get; init; } = new();
    [JsonPropertyName("base_terrain")] public string? BaseTerrain { get; init; }
    [JsonPropertyName("base_height")] public int? BaseHeight { get; init; }
    [JsonPropertyName("height_offset")] public int? HeightOffset { get; init; }
    [JsonPropertyName("flow_direction")] public BattleSimCoordJsonDto? FlowDirection { get; init; }
    [JsonPropertyName("terrain_effect_ids")] public IReadOnlyList<string>? TerrainEffectIds { get; init; }
    [JsonPropertyName("prop_ids")] public IReadOnlyList<string>? PropIds { get; init; }
}

internal sealed record BattleSimProfileImportModel(
    string ProfileId,
    string DisplayName,
    string Description,
    BattleAiScoreProfileJsonDto AiScoreProfile,
    IReadOnlyList<BattleSimOverridePatchImportModel> OverridePatches
);

internal sealed record BattleSimOverridePatchImportModel(
    string TargetType,
    string TargetId,
    string StateId,
    string ActionId,
    string Path,
    object Value
);

internal sealed record BattleSimCoordImportModel(int X, int Y);

internal sealed record BattleSimScenarioImportModel(
    string ScenarioId,
    string DisplayName,
    string Description,
    BattleSimCoordImportModel MapSize,
    string TerrainProfileId,
    bool UseFormalTerrainGeneration,
    BattleSimCoordImportModel WorldCoord,
    IReadOnlyList<BattleSimUnitImportModel> AllyUnits,
    IReadOnlyList<BattleSimUnitImportModel> EnemyUnits,
    IReadOnlyList<BattleSimCellOverrideImportModel> CellOverrides,
    int TimelineTicksPerStep,
    int TuPerTick,
    int MaxIterations,
    string ManualPolicy,
    bool TraceEnabled,
    IReadOnlyList<int> Seeds
);

internal sealed record BattleSimUnitImportModel(
    string UnitId,
    string SourceMemberId,
    string DisplayName,
    string FactionId,
    string ControlMode,
    string AiBrainId,
    string AiStateId,
    BattleSimCoordImportModel Coord,
    int BodySize,
    string BodySizeCategory,
    int CurrentHp,
    int CurrentMp,
    int CurrentStamina,
    int CurrentAura,
    int CurrentAp,
    int CurrentMovePoints,
    int ActionThreshold,
    IReadOnlyDictionary<string, int> AttributeOverrides,
    IReadOnlyList<string> SkillIds,
    IReadOnlyDictionary<string, int> SkillLevelMap,
    IReadOnlyList<string> MovementTags,
    IReadOnlyList<string> UnlockedCombatResourceIds,
    IReadOnlyDictionary<string, int> BaseAttributes,
    BattleSimWeaponProjectionImportModel? WeaponProjection
);

internal sealed record BattleSimWeaponProjectionImportModel(
    string WeaponProfileKind,
    string WeaponItemId,
    string WeaponProfileTypeId,
    string WeaponRangeType,
    string WeaponFamily,
    string WeaponCurrentGrip,
    int WeaponAttackRange,
    int OneHandedDiceCount,
    int OneHandedDiceSides,
    int TwoHandedDiceCount,
    int TwoHandedDiceSides,
    bool WeaponIsVersatile,
    bool WeaponUsesTwoHands,
    string WeaponPhysicalDamageTag
);

internal sealed record BattleSimCellOverrideImportModel(
    BattleSimCoordImportModel Coord,
    string? BaseTerrain,
    int? BaseHeight,
    int? HeightOffset,
    BattleSimCoordImportModel? FlowDirection,
    IReadOnlyList<string>? TerrainEffectIds,
    IReadOnlyList<string>? PropIds
);

internal static class BattleSimJsonImportParser
{
    internal static ContentImportStageResult<BattleSimProfileJsonDto> ParseProfile(JsonContentEntryContext context, string json) =>
        ContentJsonStrictDtoParser.Parse(context, json, BattleSimJsonSerializerContext.Default.BattleSimProfileJsonDto, BattleSimJsonRules.InvalidProfileDto);

    internal static ContentImportStageResult<BattleSimScenarioJsonDto> ParseScenario(JsonContentEntryContext context, string json) =>
        ContentJsonStrictDtoParser.Parse(context, json, BattleSimJsonSerializerContext.Default.BattleSimScenarioJsonDto, BattleSimJsonRules.InvalidScenarioDto);

    internal static ContentImportStageResult<BattleSimProfileImportModel> NormalizeProfile(JsonContentEntryContext context, BattleSimProfileJsonDto dto)
    {
        var diagnostics = new List<ContentJsonDiagnostic>();
        var patches = new List<BattleSimOverridePatchImportModel>();
        for (int index = 0; index < dto.OverridePatches.Count; index++)
        {
            BattleSimOverridePatchJsonDto patch = dto.OverridePatches[index];
            string pointer = $"/override_patches/{index}";
            if (!TryPayload(context, patch.Payload, pointer, diagnostics, out string json)) continue;
            JsonContentEntryContext nested = Nested(context, pointer + "/payload");
            if (patch.Kind is "skill" or "brain" or "faction_ai_score_profile")
            {
                if (TryParse(nested, json, BattleSimJsonSerializerContext.Default.BattleSimTargetPatchPayloadJsonDto, out BattleSimTargetPatchPayloadJsonDto? payload, out var errors)
                    && TryScalar(context, payload!.Value, pointer + "/payload/value", diagnostics, out object value))
                    patches.Add(new BattleSimOverridePatchImportModel(patch.Kind, payload.TargetId, "", "", payload.Path, value));
                else diagnostics.AddRange(errors);
            }
            else if (patch.Kind == "action")
            {
                if (TryParse(nested, json, BattleSimJsonSerializerContext.Default.BattleSimActionPatchPayloadJsonDto, out BattleSimActionPatchPayloadJsonDto? payload, out var errors)
                    && TryScalar(context, payload!.Value, pointer + "/payload/value", diagnostics, out object value))
                    patches.Add(new BattleSimOverridePatchImportModel(patch.Kind, payload.BrainId, payload.StateId, payload.ActionId, payload.Path, value));
                else diagnostics.AddRange(errors);
            }
            else if (patch.Kind == "ai_score_profile")
            {
                if (TryParse(nested, json, BattleSimJsonSerializerContext.Default.BattleSimScorePatchPayloadJsonDto, out BattleSimScorePatchPayloadJsonDto? payload, out var errors)
                    && TryScalar(context, payload!.Value, pointer + "/payload/value", diagnostics, out object value))
                    patches.Add(new BattleSimOverridePatchImportModel(patch.Kind, "", "", "", payload.Path, value));
                else diagnostics.AddRange(errors);
            }
            else AddUnknown(context, diagnostics, pointer + "/kind");
        }
        return diagnostics.Count == 0
            ? ContentImportStageResult<BattleSimProfileImportModel>.Success(new BattleSimProfileImportModel(dto.ProfileId, dto.DisplayName, dto.Description, dto.AiScoreProfile, new ReadOnlyCollection<BattleSimOverridePatchImportModel>(patches)))
            : ContentImportStageResult<BattleSimProfileImportModel>.Failure(diagnostics);
    }

    internal static ContentImportStageResult<BattleSimScenarioImportModel> NormalizeScenario(JsonContentEntryContext _, BattleSimScenarioJsonDto dto) =>
        ContentImportStageResult<BattleSimScenarioImportModel>.Success(
            new BattleSimScenarioImportModel(
                dto.ScenarioId, dto.DisplayName, dto.Description, dto.MapSize.ToImport(), dto.TerrainProfileId,
                dto.UseFormalTerrainGeneration, dto.WorldCoord.ToImport(), dto.AllyUnits.Select(Unit).ToArray(),
                dto.EnemyUnits.Select(Unit).ToArray(), dto.CellOverrides.Select(Cell).ToArray(), dto.TimelineTicksPerStep,
                dto.TuPerTick, dto.MaxIterations, dto.ManualPolicy, dto.TraceEnabled, dto.Seeds.ToArray()
            )
        );

    private static BattleSimUnitImportModel Unit(BattleSimUnitJsonDto value)
    {
        BattleSimWeaponProjectionJsonDto? weapon = value.WeaponProjection;
        BattleSimWeaponProjectionImportModel? projectedWeapon = weapon == null ? null : new(
            weapon.WeaponProfileKind, weapon.WeaponItemId, weapon.WeaponProfileTypeId, weapon.WeaponRangeType,
            weapon.WeaponFamily, weapon.WeaponCurrentGrip, weapon.WeaponAttackRange,
            weapon.WeaponOneHandedDice.DiceCount, weapon.WeaponOneHandedDice.DiceSides,
            weapon.WeaponTwoHandedDice.DiceCount, weapon.WeaponTwoHandedDice.DiceSides,
            weapon.WeaponIsVersatile, weapon.WeaponUsesTwoHands, weapon.WeaponPhysicalDamageTag
        );
        return new BattleSimUnitImportModel(
            value.UnitId, value.SourceMemberId, value.DisplayName, value.FactionId, value.ControlMode,
            value.AiBrainId, value.AiStateId, value.Coord.ToImport(), value.BodySize, value.BodySizeCategory,
            value.CurrentHp, value.CurrentMp, value.CurrentStamina, value.CurrentAura, value.CurrentAp,
            value.CurrentMovePoints, value.ActionThreshold, value.AttributeOverrides, value.SkillIds,
            value.SkillLevelMap, value.MovementTags, value.UnlockedCombatResourceIds, value.BaseAttributes,
            projectedWeapon
        );
    }

    private static BattleSimCellOverrideImportModel Cell(BattleSimCellOverrideJsonDto value) =>
        new(value.Coord.ToImport(), value.BaseTerrain, value.BaseHeight, value.HeightOffset, value.FlowDirection?.ToImport(), value.TerrainEffectIds, value.PropIds);

    private static bool TryScalar(JsonContentEntryContext context, object raw, string pointer, List<ContentJsonDiagnostic> diagnostics, out object value)
    {
        if (raw is JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String: value = element.GetString() ?? ""; return true;
                case JsonValueKind.Number when element.TryGetInt64(out long integer): value = integer; return true;
                case JsonValueKind.Number: value = element.GetDouble(); return true;
                case JsonValueKind.True: value = true; return true;
                case JsonValueKind.False: value = false; return true;
            }
        }
        diagnostics.Add(new ContentJsonDiagnostic(BattleSimJsonRules.InvalidPayload, "Override patch value must be a scalar.", context.SourceLabel, $"{context.JsonPointer}{pointer}"));
        value = 0L;
        return false;
    }

    private static bool TryPayload(JsonContentEntryContext context, object raw, string pointer, List<ContentJsonDiagnostic> diagnostics, out string json)
    {
        if (raw is JsonElement payload && payload.ValueKind == JsonValueKind.Object) { json = payload.GetRawText(); return true; }
        diagnostics.Add(new ContentJsonDiagnostic(BattleSimJsonRules.InvalidPayload, "Closed-kind payload must be a JSON object.", context.SourceLabel, $"{context.JsonPointer}{pointer}/payload"));
        json = ""; return false;
    }

    private static bool TryParse<T>(JsonContentEntryContext context, string json, JsonTypeInfo<T> typeInfo, out T? result, out IReadOnlyList<ContentJsonDiagnostic> diagnostics) where T : class
    {
        ContentImportStageResult<T> parsed = ContentJsonStrictDtoParser.Parse(context, json, typeInfo, BattleSimJsonRules.InvalidPayload);
        result = parsed.HasValue ? parsed.Value : null; diagnostics = parsed.Diagnostics; return parsed.HasValue;
    }

    private static JsonContentEntryContext Nested(JsonContentEntryContext context, string pointer) => new(context.DomainId, context.EntryId, context.SourceLabel, $"{context.JsonPointer}{pointer}");
    private static void AddUnknown(JsonContentEntryContext context, List<ContentJsonDiagnostic> diagnostics, string pointer) => diagnostics.Add(new ContentJsonDiagnostic(BattleSimJsonRules.UnknownKind, "BattleSim closed-kind discriminator is not registered.", context.SourceLabel, $"{context.JsonPointer}{pointer}"));
}

internal static class BattleSimImportValidator
{
    internal static IReadOnlyList<ContentJsonDiagnostic> ValidateProfile(JsonContentEntryContext context, BattleSimProfileImportModel import)
    {
        var diagnostics = new List<ContentJsonDiagnostic>();
        Identity(context, import.ProfileId, "/profile_id", diagnostics);
        return diagnostics;
    }

    internal static IReadOnlyList<ContentJsonDiagnostic> ValidateScenario(JsonContentEntryContext context, BattleSimScenarioImportModel import)
    {
        var diagnostics = new List<ContentJsonDiagnostic>();
        Identity(context, import.ScenarioId, "/scenario_id", diagnostics);
        if (import.MapSize.X <= 0 || import.MapSize.Y <= 0) Add(context, diagnostics, BattleSimJsonRules.ValueOutOfRange, "map_size must be positive.", "/map_size");
        if (import.TimelineTicksPerStep <= 0) Add(context, diagnostics, BattleSimJsonRules.ValueOutOfRange, "timeline_ticks_per_step must be positive.", "/timeline_ticks_per_step");
        if (import.TuPerTick <= 0) Add(context, diagnostics, BattleSimJsonRules.ValueOutOfRange, "tu_per_tick must be positive.", "/tu_per_tick");
        if (import.MaxIterations <= 0) Add(context, diagnostics, BattleSimJsonRules.ValueOutOfRange, "max_iterations must be positive.", "/max_iterations");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (BattleSimUnitImportModel unit in import.AllyUnits.Concat(import.EnemyUnits))
            if (string.IsNullOrWhiteSpace(unit.UnitId) || !ids.Add(unit.UnitId)) Add(context, diagnostics, BattleSimJsonRules.DuplicateId, "unit_id must be non-empty and unique across both rosters.", "/ally_units");
        return diagnostics;
    }

    private static void Identity(JsonContentEntryContext context, string id, string pointer, List<ContentJsonDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(id)) Add(context, diagnostics, BattleSimJsonRules.IdRequired, "Content ID is required.", pointer);
        else if (!string.Equals(id, context.EntryId, StringComparison.Ordinal)) Add(context, diagnostics, BattleSimJsonRules.IdMismatch, "Content ID must match the envelope entry ID.", pointer);
    }
    private static void Add(JsonContentEntryContext context, List<ContentJsonDiagnostic> target, string rule, string message, string pointer) => target.Add(new ContentJsonDiagnostic(rule, message, context.SourceLabel, $"{context.JsonPointer}{pointer}"));
}

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata, PropertyNamingPolicy = JsonKnownNamingPolicy.Unspecified)]
[JsonSerializable(typeof(BattleSimProfileJsonDto))]
[JsonSerializable(typeof(BattleSimScenarioJsonDto))]
[JsonSerializable(typeof(BattleSimTargetPatchPayloadJsonDto))]
[JsonSerializable(typeof(BattleSimActionPatchPayloadJsonDto))]
[JsonSerializable(typeof(BattleSimScorePatchPayloadJsonDto))]
internal partial class BattleSimJsonSerializerContext : JsonSerializerContext { }
