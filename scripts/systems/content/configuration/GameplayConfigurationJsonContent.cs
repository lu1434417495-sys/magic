#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
#if !CONTENT_JSON_OFFLINE
using Godot;
#endif

internal static class GameplayConfigurationJsonDomain
{
    internal const int SchemaVersion = 1;
    internal const string DomainId = "gameplay_configuration";
    internal const string DirectoryPath = "res://data/configs/json/gameplay_configuration";

    internal static ContentJsonSchemaDomainRegistration SchemaRegistration { get; } =
        new(
            DomainId,
            SchemaVersion,
            typeof(GameplayConfigurationJsonDocumentDto),
            "Magic gameplay configuration JSON schema",
            "Process-scoped achievements, settlement shops, new-game party, and generation BattleSim fixture roles.",
            "res://data/schemas/content/gameplay_configuration.schema.json",
            "/data/configs/json/gameplay_configuration/**/*.json"
        );

    internal static JsonContentDomainDescriptor<
        GameplayConfigurationJsonDto,
        GameplayConfigurationJsonDto
    > CreateDescriptor(string sourceDirectory, IContentJsonSourceReader sourceReader) =>
        new(
            DomainId,
            SchemaVersion,
            "configuration_id",
            sourceDirectory,
            sourceReader,
            new ContentJsonNullabilityPolicy(Array.Empty<string>()),
            Parse,
            static (_, dto) => ContentImportStageResult<GameplayConfigurationJsonDto>.Success(dto),
            Validate
        );

    internal static IContentJsonOfflineValidationDomain CreateOfflineDomain() =>
        new OfflineDomain();

    private static ContentImportStageResult<GameplayConfigurationJsonDto> Parse(
        JsonContentEntryContext context,
        string json
    ) => ContentJsonStrictDtoParser.Parse(
        context,
        json,
        GameplayConfigurationJsonSerializerContext.Default.GameplayConfigurationJsonDto,
        GameplayConfigurationJsonRules.InvalidDto
    );

    private static IReadOnlyList<ContentJsonDiagnostic> Validate(
        JsonContentEntryContext context,
        GameplayConfigurationJsonDto dto
    ) => GameplayConfigurationJsonValidator.Validate(context, dto);

    private sealed class OfflineDomain : IContentJsonOfflineValidationDomain
    {
        public string DomainId => GameplayConfigurationJsonDomain.DomainId;

        public ContentJsonOfflineValidationReport Validate(
            string sourceDirectory,
            IContentJsonSourceReader sourceReader
        )
        {
            ContentImportBatch<GameplayConfigurationJsonDto> batch = CreateDescriptor(
                sourceDirectory,
                sourceReader
            ).Import();
            return new ContentJsonOfflineValidationReport(
                DomainId,
                batch.Entries.Count,
                batch.Diagnostics
            );
        }
    }
}

internal static class GameplayConfigurationJsonRules
{
    internal const string InvalidDto = "gameplay_configuration.dto.invalid";
    internal const string IdRequired = "gameplay_configuration.id.required";
    internal const string DuplicateId = "gameplay_configuration.id.duplicate";
    internal const string InvalidValue = "gameplay_configuration.value.invalid";
    internal const string InvalidReference = "gameplay_configuration.reference.invalid";
    internal const string SingletonRequired = "gameplay_configuration.singleton.required";
}

[Description("Strict process-scoped gameplay configuration document.")]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class GameplayConfigurationJsonDocumentDto
{
    [JsonPropertyName("schema"), JsonRequired]
    [ContentJsonSchemaConst(GameplayConfigurationJsonDomain.SchemaVersion)]
    public int Schema { get; init; }

    [JsonPropertyName("domain"), JsonRequired]
    [ContentJsonSchemaConst(GameplayConfigurationJsonDomain.DomainId)]
    public string Domain { get; init; } = "";

    [JsonPropertyName("family"), JsonRequired]
    public string Family { get; init; } = "";

    [JsonPropertyName("templates"), JsonRequired]
    [ContentJsonSchemaPartialObjectValues(typeof(ContentJsonTemplateReferenceSchemaDto))]
    public IReadOnlyDictionary<string, GameplayConfigurationJsonDto> Templates { get; init; } =
        new ReadOnlyDictionary<string, GameplayConfigurationJsonDto>(
            new Dictionary<string, GameplayConfigurationJsonDto>()
        );

    [JsonPropertyName("entries"), JsonRequired]
    [ContentJsonSchemaEntryControlMembers(
        typeof(ContentJsonTemplateReferenceSchemaDto),
        "configuration_id",
        "template"
    )]
    public IReadOnlyList<GameplayConfigurationJsonDto> Entries { get; init; } =
        Array.Empty<GameplayConfigurationJsonDto>();
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class GameplayConfigurationJsonDto
{
    [JsonPropertyName("configuration_id"), JsonRequired]
    public string ConfigurationId { get; init; } = "";

    [JsonPropertyName("achievements"), JsonRequired]
    public IReadOnlyList<GameplayAchievementJsonDto> Achievements { get; init; } =
        Array.Empty<GameplayAchievementJsonDto>();

    [JsonPropertyName("settlement_shops"), JsonRequired]
    public IReadOnlyList<SettlementShopJsonDto> SettlementShops { get; init; } =
        Array.Empty<SettlementShopJsonDto>();

    [JsonPropertyName("new_game_party"), JsonRequired]
    public NewGamePartyJsonDto NewGameParty { get; init; } = new();

    [JsonPropertyName("battle_skill_roles"), JsonRequired]
    public BattleSkillRolesJsonDto BattleSkillRoles { get; init; } = new();

    [JsonPropertyName("skill_generation_battle_sim"), JsonRequired]
    public SkillGenerationBattleSimFixtureJsonDto SkillGenerationBattleSim { get; init; } = new();

    [JsonPropertyName("equipment_generation_battle_sim"), JsonRequired]
    public EquipmentGenerationBattleSimFixtureJsonDto EquipmentGenerationBattleSim { get; init; } = new();
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class GameplayAchievementJsonDto
{
    [JsonPropertyName("achievement_id"), JsonRequired] public string AchievementId { get; init; } = "";
    [JsonPropertyName("display_name"), JsonRequired] public string DisplayName { get; init; } = "";
    [JsonPropertyName("description"), JsonRequired] public string Description { get; init; } = "";
    [JsonPropertyName("event_type"), JsonRequired] public string EventType { get; init; } = "";
    [JsonPropertyName("subject_id"), JsonRequired] public string SubjectId { get; init; } = "";
    [JsonPropertyName("threshold"), JsonRequired] public int Threshold { get; init; }
    [JsonPropertyName("rewards"), JsonRequired] public IReadOnlyList<GameplayAchievementRewardJsonDto> Rewards { get; init; } = Array.Empty<GameplayAchievementRewardJsonDto>();
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class GameplayAchievementRewardJsonDto
{
    [JsonPropertyName("reward_type"), JsonRequired]
    [ContentJsonSchemaStableStringValues(typeof(GameplayAchievementRewardTypeValues))]
    public string RewardType { get; init; } = "";
    [JsonPropertyName("target_id"), JsonRequired] public string TargetId { get; init; } = "";
    [JsonPropertyName("target_label"), JsonRequired] public string TargetLabel { get; init; } = "";
    [JsonPropertyName("amount"), JsonRequired] public int Amount { get; init; }
    [JsonPropertyName("reason_text"), JsonRequired] public string ReasonText { get; init; } = "";
}

internal sealed class GameplayAchievementRewardTypeValues : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values { get; } = Array.AsReadOnly(
        new[] { "attribute_delta", "attribute_progress", "skill_unlock", "skill_mastery", "knowledge_unlock" }
    );

    internal static GameplayAchievementRewardKind ToKind(string value) => value switch
    {
        "attribute_delta" => GameplayAchievementRewardKind.AttributeDelta,
        "attribute_progress" => GameplayAchievementRewardKind.AttributeProgress,
        "skill_unlock" => GameplayAchievementRewardKind.SkillUnlock,
        "skill_mastery" => GameplayAchievementRewardKind.SkillMastery,
        "knowledge_unlock" => GameplayAchievementRewardKind.KnowledgeUnlock,
        _ => GameplayAchievementRewardKind.Unknown,
    };
}

internal enum GameplayAchievementRewardKind
{
    Unknown = 0,
    AttributeDelta,
    AttributeProgress,
    SkillUnlock,
    SkillMastery,
    KnowledgeUnlock,
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class SettlementShopJsonDto
{
    [JsonPropertyName("interaction_script_id"), JsonRequired] public string InteractionScriptId { get; init; } = "";
    [JsonPropertyName("shop_id"), JsonRequired] public string ShopId { get; init; } = "";
    [JsonPropertyName("title"), JsonRequired] public string Title { get; init; } = "";
    [JsonPropertyName("refresh_interval_steps"), JsonRequired] public int RefreshIntervalSteps { get; init; }
    [JsonPropertyName("guaranteed_items"), JsonRequired] public IReadOnlyList<SettlementShopItemJsonDto> GuaranteedItems { get; init; } = Array.Empty<SettlementShopItemJsonDto>();
    [JsonPropertyName("random_pool"), JsonRequired] public IReadOnlyList<SettlementShopItemJsonDto> RandomPool { get; init; } = Array.Empty<SettlementShopItemJsonDto>();
    [JsonPropertyName("max_random_items"), JsonRequired] public int MaxRandomItems { get; init; }
    [JsonPropertyName("unique_equipment_offer_chance_percent"), JsonRequired] public int UniqueEquipmentOfferChancePercent { get; init; }
    [JsonPropertyName("default_price_basis_points"), JsonRequired] public int DefaultPriceBasisPoints { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class SettlementShopItemJsonDto
{
    [JsonPropertyName("item_id"), JsonRequired] public string ItemId { get; init; } = "";
    [JsonPropertyName("min_quantity"), JsonRequired] public int MinQuantity { get; init; }
    [JsonPropertyName("max_quantity"), JsonRequired] public int MaxQuantity { get; init; }
    [JsonPropertyName("weight"), JsonRequired] public int Weight { get; init; }
    [JsonPropertyName("price_basis_points"), JsonRequired] public int PriceBasisPoints { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class NewGamePartyJsonDto
{
    [JsonPropertyName("gold"), JsonRequired] public int Gold { get; init; }
    [JsonPropertyName("leader_member_id"), JsonRequired] public string LeaderMemberId { get; init; } = "";
    [JsonPropertyName("main_character_member_id"), JsonRequired] public string MainCharacterMemberId { get; init; } = "";
    [JsonPropertyName("active_member_ids"), JsonRequired] public IReadOnlyList<string> ActiveMemberIds { get; init; } = Array.Empty<string>();
    [JsonPropertyName("reserve_member_ids"), JsonRequired] public IReadOnlyList<string> ReserveMemberIds { get; init; } = Array.Empty<string>();
    [JsonPropertyName("members"), JsonRequired] public IReadOnlyList<NewGameMemberJsonDto> Members { get; init; } = Array.Empty<NewGameMemberJsonDto>();
    [JsonPropertyName("starting_weapon_rules"), JsonRequired] public IReadOnlyList<NewGameStartingWeaponRuleJsonDto> StartingWeaponRules { get; init; } = Array.Empty<NewGameStartingWeaponRuleJsonDto>();
    [JsonPropertyName("starting_weapon_fallback_item_id"), JsonRequired] public string StartingWeaponFallbackItemId { get; init; } = "";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class NewGameMemberJsonDto
{
    [JsonPropertyName("member_id"), JsonRequired] public string MemberId { get; init; } = "";
    [JsonPropertyName("display_name"), JsonRequired] public string DisplayName { get; init; } = "";
    [JsonPropertyName("faction_id"), JsonRequired] public string FactionId { get; init; } = "";
    [JsonPropertyName("portrait_id"), JsonRequired] public string PortraitId { get; init; } = "";
    [JsonPropertyName("control_mode"), JsonRequired] public string ControlMode { get; init; } = "";
    [JsonPropertyName("race_id"), JsonRequired] public string RaceId { get; init; } = "";
    [JsonPropertyName("subrace_id"), JsonRequired] public string SubraceId { get; init; } = "";
    [JsonPropertyName("age_years"), JsonRequired] public int AgeYears { get; init; }
    [JsonPropertyName("age_profile_id"), JsonRequired] public string AgeProfileId { get; init; } = "";
    [JsonPropertyName("natural_age_stage_id"), JsonRequired] public string NaturalAgeStageId { get; init; } = "";
    [JsonPropertyName("effective_age_stage_id"), JsonRequired] public string EffectiveAgeStageId { get; init; } = "";
    [JsonPropertyName("body_size_category"), JsonRequired] public string BodySizeCategory { get; init; } = "";
    [JsonPropertyName("current_mp"), JsonRequired] public int CurrentMp { get; init; }
    [JsonPropertyName("storage_space"), JsonRequired] public int StorageSpace { get; init; }
    [JsonPropertyName("base_attributes"), JsonRequired] public IReadOnlyDictionary<string, int> BaseAttributes { get; init; } = new ReadOnlyDictionary<string, int>(new Dictionary<string, int>());
    [JsonPropertyName("starting_skill_ids"), JsonRequired] public IReadOnlyList<string> StartingSkillIds { get; init; } = Array.Empty<string>();
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class NewGameStartingWeaponRuleJsonDto
{
    [JsonPropertyName("required_skill_tags_any"), JsonRequired] public IReadOnlyList<string> RequiredSkillTagsAny { get; init; } = Array.Empty<string>();
    [JsonPropertyName("item_id"), JsonRequired] public string ItemId { get; init; } = "";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class SkillGenerationBattleSimFixtureJsonDto
{
    [JsonPropertyName("ground_benchmark_skill_id"), JsonRequired] public string GroundBenchmarkSkillId { get; init; } = "";
    [JsonPropertyName("multi_target_benchmark_skill_id"), JsonRequired] public string MultiTargetBenchmarkSkillId { get; init; } = "";
    [JsonPropertyName("ranged_unit_benchmark_skill_id"), JsonRequired] public string RangedUnitBenchmarkSkillId { get; init; } = "";
    [JsonPropertyName("melee_brain_id"), JsonRequired] public string MeleeBrainId { get; init; } = "";
    [JsonPropertyName("mage_brain_id"), JsonRequired] public string MageBrainId { get; init; } = "";
    [JsonPropertyName("ranged_brain_id"), JsonRequired] public string RangedBrainId { get; init; } = "";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class EquipmentGenerationBattleSimFixtureJsonDto
{
    [JsonPropertyName("melee_brain_id"), JsonRequired] public string MeleeBrainId { get; init; } = "";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class BattleSkillRolesJsonDto
{
    [JsonPropertyName("basic_attack_skill_id"), JsonRequired]
    public string BasicAttackSkillId { get; init; } = "";
}

internal static class GameplayConfigurationJsonValidator
{
    private static readonly HashSet<string> BaseAttributeIds = new(
        new[] { "strength", "agility", "constitution", "perception", "intelligence", "willpower" },
        StringComparer.Ordinal
    );

    internal static IReadOnlyList<ContentJsonDiagnostic> Validate(
        JsonContentEntryContext context,
        GameplayConfigurationJsonDto dto
    )
    {
        var diagnostics = new List<ContentJsonDiagnostic>();
        RequireId(context, diagnostics, dto.ConfigurationId, "/configuration_id");
        Duplicate(context, diagnostics, dto.Achievements.Select(x => x.AchievementId), "/achievements");
        for (int i = 0; i < dto.Achievements.Count; i++)
        {
            GameplayAchievementJsonDto value = dto.Achievements[i];
            string pointer = $"/achievements/{i}";
            RequireId(context, diagnostics, value.AchievementId, pointer + "/achievement_id");
            RequireText(context, diagnostics, value.DisplayName, pointer + "/display_name");
            RequireText(context, diagnostics, value.Description, pointer + "/description");
            RequireId(context, diagnostics, value.EventType, pointer + "/event_type");
            if (value.Threshold <= 0) Invalid(context, diagnostics, pointer + "/threshold", "must be positive");
            for (int j = 0; j < value.Rewards.Count; j++)
            {
                GameplayAchievementRewardJsonDto reward = value.Rewards[j];
                string rewardPointer = $"{pointer}/rewards/{j}";
                RequireId(context, diagnostics, reward.RewardType, rewardPointer + "/reward_type");
                RequireId(context, diagnostics, reward.TargetId, rewardPointer + "/target_id");
                RequireText(context, diagnostics, reward.TargetLabel, rewardPointer + "/target_label");
                GameplayAchievementRewardKind rewardKind =
                    GameplayAchievementRewardTypeValues.ToKind(reward.RewardType);
                if (
                    !string.IsNullOrWhiteSpace(reward.RewardType)
                    && rewardKind == GameplayAchievementRewardKind.Unknown
                )
                    Invalid(
                        context,
                        diagnostics,
                        rewardPointer + "/reward_type",
                        "unsupported reward type"
                    );
                if (
                    rewardKind
                        is GameplayAchievementRewardKind.AttributeDelta
                            or GameplayAchievementRewardKind.SkillMastery
                    && reward.Amount <= 0
                )
                    Invalid(
                        context,
                        diagnostics,
                        rewardPointer + "/amount",
                        "must be positive for this reward type"
                    );
            }
        }

        Duplicate(context, diagnostics, dto.SettlementShops.Select(x => x.InteractionScriptId), "/settlement_shops");
        for (int i = 0; i < dto.SettlementShops.Count; i++)
        {
            SettlementShopJsonDto shop = dto.SettlementShops[i];
            string pointer = $"/settlement_shops/{i}";
            RequireId(context, diagnostics, shop.InteractionScriptId, pointer + "/interaction_script_id");
            RequireId(context, diagnostics, shop.ShopId, pointer + "/shop_id");
            RequireText(context, diagnostics, shop.Title, pointer + "/title");
            if (shop.RefreshIntervalSteps <= 0) Invalid(context, diagnostics, pointer + "/refresh_interval_steps", "must be positive");
            if (shop.MaxRandomItems < 0) Invalid(context, diagnostics, pointer + "/max_random_items", "must be non-negative");
            if (shop.UniqueEquipmentOfferChancePercent is < 0 or > 100) Invalid(context, diagnostics, pointer + "/unique_equipment_offer_chance_percent", "must be 0..100");
            if (shop.DefaultPriceBasisPoints <= 0) Invalid(context, diagnostics, pointer + "/default_price_basis_points", "must be positive");
            ValidateShopItems(context, diagnostics, shop.GuaranteedItems, pointer + "/guaranteed_items", false);
            ValidateShopItems(context, diagnostics, shop.RandomPool, pointer + "/random_pool", true);
        }

        NewGamePartyJsonDto party = dto.NewGameParty;
        if (party.Gold < 0) Invalid(context, diagnostics, "/new_game_party/gold", "must be non-negative");
        RequireId(context, diagnostics, party.LeaderMemberId, "/new_game_party/leader_member_id");
        RequireId(context, diagnostics, party.MainCharacterMemberId, "/new_game_party/main_character_member_id");
        Duplicate(context, diagnostics, party.Members.Select(x => x.MemberId), "/new_game_party/members");
        var memberIds = new HashSet<string>(party.Members.Select(x => x.MemberId), StringComparer.Ordinal);
        if (!memberIds.Contains(party.LeaderMemberId)) Invalid(context, diagnostics, "/new_game_party/leader_member_id", "must reference a member");
        if (!memberIds.Contains(party.MainCharacterMemberId)) Invalid(context, diagnostics, "/new_game_party/main_character_member_id", "must reference a member");
        for (int i = 0; i < party.Members.Count; i++)
        {
            NewGameMemberJsonDto member = party.Members[i];
            string pointer = $"/new_game_party/members/{i}";
            RequireId(context, diagnostics, member.MemberId, pointer + "/member_id");
            RequireText(context, diagnostics, member.DisplayName, pointer + "/display_name");
            RequireId(context, diagnostics, member.FactionId, pointer + "/faction_id");
            RequireId(context, diagnostics, member.RaceId, pointer + "/race_id");
            RequireId(context, diagnostics, member.SubraceId, pointer + "/subrace_id");
            if (member.AgeYears < 0 || member.CurrentMp < 0 || member.StorageSpace < 0) Invalid(context, diagnostics, pointer, "numeric values must be non-negative");
            foreach (string key in member.BaseAttributes.Keys)
                if (!BaseAttributeIds.Contains(key)) Invalid(context, diagnostics, pointer + "/base_attributes/" + key, "unsupported base attribute");
            foreach (string attributeId in BaseAttributeIds)
                if (!member.BaseAttributes.ContainsKey(attributeId)) Invalid(context, diagnostics, pointer + "/base_attributes", $"missing {attributeId}");
            for (int j = 0; j < member.StartingSkillIds.Count; j++) RequireId(context, diagnostics, member.StartingSkillIds[j], $"{pointer}/starting_skill_ids/{j}");
        }
        foreach (string memberId in party.ActiveMemberIds.Concat(party.ReserveMemberIds))
            if (!memberIds.Contains(memberId)) Invalid(context, diagnostics, "/new_game_party", $"unknown member {memberId}");
        for (int i = 0; i < party.StartingWeaponRules.Count; i++)
        {
            NewGameStartingWeaponRuleJsonDto rule = party.StartingWeaponRules[i];
            string pointer = $"/new_game_party/starting_weapon_rules/{i}";
            RequireId(context, diagnostics, rule.ItemId, pointer + "/item_id");
            if (rule.RequiredSkillTagsAny.Count == 0) Invalid(context, diagnostics, pointer + "/required_skill_tags_any", "must not be empty");
            for (int j = 0; j < rule.RequiredSkillTagsAny.Count; j++) RequireId(context, diagnostics, rule.RequiredSkillTagsAny[j], $"{pointer}/required_skill_tags_any/{j}");
        }
        RequireId(context, diagnostics, party.StartingWeaponFallbackItemId, "/new_game_party/starting_weapon_fallback_item_id");
        RequireId(
            context,
            diagnostics,
            dto.BattleSkillRoles.BasicAttackSkillId,
            "/battle_skill_roles/basic_attack_skill_id"
        );
        ValidateFixtureIds(context, diagnostics, dto.SkillGenerationBattleSim, dto.EquipmentGenerationBattleSim);
        return diagnostics;
    }

    private static void ValidateShopItems(JsonContentEntryContext context, List<ContentJsonDiagnostic> diagnostics, IReadOnlyList<SettlementShopItemJsonDto> items, string pointer, bool requireWeight)
    {
        Duplicate(context, diagnostics, items.Select(x => x.ItemId), pointer);
        for (int i = 0; i < items.Count; i++)
        {
            SettlementShopItemJsonDto item = items[i];
            string itemPointer = $"{pointer}/{i}";
            RequireId(context, diagnostics, item.ItemId, itemPointer + "/item_id");
            if (item.MinQuantity <= 0 || item.MaxQuantity < item.MinQuantity) Invalid(context, diagnostics, itemPointer, "quantity range is invalid");
            if ((requireWeight && item.Weight <= 0) || (!requireWeight && item.Weight < 0)) Invalid(context, diagnostics, itemPointer + "/weight", "weight is invalid");
            if (item.PriceBasisPoints <= 0) Invalid(context, diagnostics, itemPointer + "/price_basis_points", "must be positive");
        }
    }

    private static void ValidateFixtureIds(JsonContentEntryContext context, List<ContentJsonDiagnostic> diagnostics, SkillGenerationBattleSimFixtureJsonDto skill, EquipmentGenerationBattleSimFixtureJsonDto equipment)
    {
        RequireId(context, diagnostics, skill.GroundBenchmarkSkillId, "/skill_generation_battle_sim/ground_benchmark_skill_id");
        RequireId(context, diagnostics, skill.MultiTargetBenchmarkSkillId, "/skill_generation_battle_sim/multi_target_benchmark_skill_id");
        RequireId(context, diagnostics, skill.RangedUnitBenchmarkSkillId, "/skill_generation_battle_sim/ranged_unit_benchmark_skill_id");
        RequireId(context, diagnostics, skill.MeleeBrainId, "/skill_generation_battle_sim/melee_brain_id");
        RequireId(context, diagnostics, skill.MageBrainId, "/skill_generation_battle_sim/mage_brain_id");
        RequireId(context, diagnostics, skill.RangedBrainId, "/skill_generation_battle_sim/ranged_brain_id");
        RequireId(context, diagnostics, equipment.MeleeBrainId, "/equipment_generation_battle_sim/melee_brain_id");
    }

    private static void Duplicate(JsonContentEntryContext context, List<ContentJsonDiagnostic> diagnostics, IEnumerable<string> values, string pointer)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string value in values)
            if (!string.IsNullOrWhiteSpace(value) && !seen.Add(value))
                diagnostics.Add(Diagnostic(context, GameplayConfigurationJsonRules.DuplicateId, $"Duplicate ID '{value}'.", pointer));
    }

    private static void RequireId(JsonContentEntryContext context, List<ContentJsonDiagnostic> diagnostics, string value, string pointer)
    {
        if (string.IsNullOrWhiteSpace(value)) diagnostics.Add(Diagnostic(context, GameplayConfigurationJsonRules.IdRequired, "Stable ID is required.", pointer));
    }

    private static void RequireText(JsonContentEntryContext context, List<ContentJsonDiagnostic> diagnostics, string value, string pointer)
    {
        if (string.IsNullOrWhiteSpace(value)) diagnostics.Add(Diagnostic(context, GameplayConfigurationJsonRules.InvalidValue, "Non-blank text is required.", pointer));
    }

    private static void Invalid(JsonContentEntryContext context, List<ContentJsonDiagnostic> diagnostics, string pointer, string message) =>
        diagnostics.Add(Diagnostic(context, GameplayConfigurationJsonRules.InvalidValue, message, pointer));

    private static ContentJsonDiagnostic Diagnostic(JsonContentEntryContext context, string rule, string message, string pointer) =>
        new(rule, message, context.SourceLabel, context.JsonPointer + pointer);
}

#if !CONTENT_JSON_OFFLINE
internal sealed class GameplayConfigurationContentRegistry
{
    private GameplayConfigurationDefinition? _definition;
    private readonly List<string> _validationErrors = new();

    internal void Rebuild()
    {
        _definition = null;
        _validationErrors.Clear();
        ContentImportBatch<GameplayConfigurationJsonDto> batch = GameplayConfigurationJsonDomain
            .CreateDescriptor(
                GameplayConfigurationJsonDomain.DirectoryPath,
                new GodotContentJsonSourceReader()
            )
            .Import();
        foreach (ContentJsonDiagnostic diagnostic in batch.Diagnostics)
            _validationErrors.Add(diagnostic.ToString());
        if (batch.Diagnostics.Count > 0)
            return;
        if (batch.Entries.Count != 1)
        {
            _validationErrors.Add(
                $"Gameplay configuration requires exactly one entry, found {batch.Entries.Count}."
            );
            return;
        }
        _definition = Project(batch.Entries[0].Import);
    }

    internal GameplayConfigurationDefinition GetDefinition() =>
        _definition ?? throw new InvalidOperationException(
            "Gameplay configuration is unavailable because content import did not publish one definition."
        );

    internal IReadOnlyList<string> GetValidationErrors() => _validationErrors.ToArray();

    private static GameplayConfigurationDefinition Project(GameplayConfigurationJsonDto dto)
    {
        IReadOnlyDictionary<StringName, AchievementDefinition> achievements =
            new ReadOnlyDictionary<StringName, AchievementDefinition>(
                dto.Achievements.ToDictionary(
                    value => new StringName(value.AchievementId),
                    value => new AchievementDefinition(
                        value.AchievementId,
                        value.DisplayName,
                        value.Description,
                        value.EventType,
                        value.SubjectId,
                        value.Threshold,
                        value.Rewards.Select(reward => new AchievementRewardDefinition(
                            reward.RewardType,
                            reward.TargetId,
                            reward.TargetLabel,
                            reward.Amount,
                            reward.ReasonText
                        )).ToArray()
                    )
                )
            );
        SettlementShopDefinition[] shops = dto.SettlementShops.Select(shop =>
            new SettlementShopDefinition(
                shop.InteractionScriptId,
                shop.ShopId,
                shop.Title,
                shop.RefreshIntervalSteps,
                shop.GuaranteedItems.Select(ProjectShopItem).ToArray(),
                shop.RandomPool.Select(ProjectShopItem).ToArray(),
                shop.MaxRandomItems,
                shop.UniqueEquipmentOfferChancePercent,
                shop.DefaultPriceBasisPoints
            )
        ).ToArray();
        NewGamePartyJsonDto party = dto.NewGameParty;
        var partyDefinition = new NewGamePartyDefinition(
            party.Gold,
            party.LeaderMemberId,
            party.MainCharacterMemberId,
            party.ActiveMemberIds.Select(x => new StringName(x)).ToArray(),
            party.ReserveMemberIds.Select(x => new StringName(x)).ToArray(),
            party.Members.Select(member => new NewGameMemberDefinition(
                member.MemberId,
                member.DisplayName,
                member.FactionId,
                member.PortraitId,
                member.ControlMode,
                member.RaceId,
                member.SubraceId,
                member.AgeYears,
                member.AgeProfileId,
                member.NaturalAgeStageId,
                member.EffectiveAgeStageId,
                member.BodySizeCategory,
                member.CurrentMp,
                member.StorageSpace,
                member.BaseAttributes.ToDictionary(x => new StringName(x.Key), x => x.Value),
                member.StartingSkillIds.Select(x => new StringName(x)).ToArray()
            )).ToArray(),
            party.StartingWeaponRules.Select(rule => new NewGameStartingWeaponRuleDefinition(
                Array.AsReadOnly(rule.RequiredSkillTagsAny.Select(x => new StringName(x)).ToArray()),
                rule.ItemId
            )).ToArray(),
            party.StartingWeaponFallbackItemId
        );
        SkillGenerationBattleSimFixtureJsonDto skillFixture = dto.SkillGenerationBattleSim;
        EquipmentGenerationBattleSimFixtureJsonDto equipmentFixture = dto.EquipmentGenerationBattleSim;
        var battleSkillRoles = new BattleSkillRoleDefinition(
            dto.BattleSkillRoles.BasicAttackSkillId
        );
        return new GameplayConfigurationDefinition(
            dto.ConfigurationId,
            achievements,
            shops,
            partyDefinition,
            battleSkillRoles,
            new SkillGenerationBattleSimFixtureDefinition(
                battleSkillRoles.BasicAttackSkillId,
                skillFixture.GroundBenchmarkSkillId,
                skillFixture.MultiTargetBenchmarkSkillId,
                skillFixture.RangedUnitBenchmarkSkillId,
                skillFixture.MeleeBrainId,
                skillFixture.MageBrainId,
                skillFixture.RangedBrainId
            ),
            new EquipmentGenerationBattleSimFixtureDefinition(
                battleSkillRoles.BasicAttackSkillId,
                equipmentFixture.MeleeBrainId
            )
        );
    }

    private static SettlementShopItemDefinition ProjectShopItem(SettlementShopItemJsonDto item) =>
        new(item.ItemId, item.MinQuantity, item.MaxQuantity, item.Weight, item.PriceBasisPoints);
}
#endif

[JsonSerializable(typeof(GameplayConfigurationJsonDto))]
internal partial class GameplayConfigurationJsonSerializerContext : JsonSerializerContext { }
