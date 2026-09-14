#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

internal static class SkillJsonImportRules
{
    internal const string InvalidDto = "skill.dto.invalid_entry";
    internal const string RequiredMember = "skill.dto.required_member";
    internal const string InvalidId = "skill.dto.id.invalid_snake_case";
    internal const string NumberOutOfRange = "skill.dto.number.out_of_range";
    internal const string UnknownSkillType = "skill.dto.skill_type.unknown";
    internal const string UnknownLearnSource = "skill.dto.learn_source.unknown";
    internal const string UnknownTargetMode = "skill.dto.target_mode.unknown";
    internal const string UnknownTargetTeamFilter = "skill.dto.target_team_filter.unknown";
    internal const string UnknownRangePattern = "skill.dto.range_pattern.unknown";
    internal const string UnknownAreaPattern = "skill.dto.area_pattern.unknown";
    internal const string UnknownEffectKind = "skill.dto.effect_type.unknown";
    internal const string MissingEffectPayload = "skill.dto.effect_payload.required";
    internal const string InvalidEffectPayload = "skill.dto.effect_payload.invalid";
    internal const string SkillIdMismatch = "skill.dto.combat_profile.skill_id_mismatch";
    internal const string EmptyLevelOverride = "skill.dto.level_override.empty";
    internal const string DuplicateLevelKey = "skill.dto.level_key.duplicate";
    internal const string DuplicateDescriptionVariableKey =
        "skill.dto.level_description_variable.duplicate";
    internal const string UnknownPendingCastBindingMode =
        "skill.dto.level_override.pending_cast_binding_mode.unknown";
    internal const string UnknownAttackResolutionMode =
        "skill.dto.level_override.attack_resolution_mode.unknown";
    internal const string UnknownAttackDefenseMode =
        "skill.dto.level_override.attack_defense_mode.unknown";
    internal const string UnknownLevelOverrideAreaPattern =
        "skill.dto.level_override.area_pattern.unknown";
}

internal static partial class SkillJsonImportParser
{
    /// <summary>
    /// Synchronous sealed import boundary. Domain registration and authoring tools must register
    /// SkillImportModel as TDto, bind parsing to this method, and use an identity normalize stage;
    /// parser-only DTOs and their object/JsonElement carrier must never be exposed as a registration API.
    /// </summary>
    internal static ContentImportStageResult<SkillImportModel> Parse(
        JsonContentEntryContext context,
        string json
    )
    {
        ContentImportStageResult<SkillJsonDto> dtoResult = ParseDto(context, json);
        return dtoResult.HasValue
            ? Normalize(context, dtoResult.Value, allowResourceOnlyShape: false)
            : ContentImportStageResult<SkillImportModel>.Failure(dtoResult.Diagnostics);
    }

    private static ContentImportStageResult<SkillJsonDto> ParseDto(
        JsonContentEntryContext context,
        string json
    )
    {
        ArgumentNullException.ThrowIfNull(context);
        string sourceJson = json ?? "";

        ContentJsonDiagnostic? missingRequired = FindMissingRequiredMember(context, sourceJson);
        if (missingRequired != null)
            return ContentImportStageResult<SkillJsonDto>.Failure(missingRequired);

        return ContentJsonStrictDtoParser.Parse(
            context,
            sourceJson,
            SkillJsonImportSerializerContext.Default.SkillJsonDto,
            SkillJsonImportRules.InvalidDto
        );
    }

    private static ContentImportStageResult<SkillImportModel> Normalize(
        JsonContentEntryContext context,
        SkillJsonDto dto,
        bool allowResourceOnlyShape
    )
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(dto);

        var diagnostics = new List<ContentJsonDiagnostic>();
        int maxLevel = dto.MaxLevel ?? 1;
        TryIdentifier(dto.SkillId, context, "/skill_id", diagnostics, out SkillImportIdentifier skillId);
        RequireNonNull(dto.DisplayName, context, "/display_name", diagnostics);
        RequireNonNull(dto.Description, context, "/description", diagnostics);

        if (!SkillJsonImportValueRules.TryParseSkillType(dto.SkillType, out SkillImportType skillType))
        {
            diagnostics.Add(
                Diagnostic(
                    SkillJsonImportRules.UnknownSkillType,
                    "Skill type is not registered by the skill import contract.",
                    context,
                    "/skill_type"
                )
            );
        }

        if (
            !SkillJsonImportValueRules.TryParseLearnSource(
                dto.LearnSource,
                out SkillImportLearnSource learnSource
            )
        )
        {
            diagnostics.Add(
                Diagnostic(
                    SkillJsonImportRules.UnknownLearnSource,
                    "Skill learn source is not registered by the skill import contract.",
                    context,
                    "/learn_source"
                )
            );
        }

        bool hasDynamicMaxLevel = !string.IsNullOrWhiteSpace(dto.DynamicMaxLevelStatId);
        if (!allowResourceOnlyShape && maxLevel < 0 && !hasDynamicMaxLevel)
            AddRangeDiagnostic(context, "/max_level", diagnostics);

        var tags = new List<SkillImportIdentifier>();
        if (dto.Tags == null)
        {
            diagnostics.Add(Required(context, "/tags"));
        }
        else
        {
            for (int index = 0; index < dto.Tags.Count; index += 1)
            {
                if (
                    TryIdentifier(
                        dto.Tags[index],
                        context,
                        $"/tags/{index}",
                        diagnostics,
                        out SkillImportIdentifier tag
                    )
                )
                {
                    tags.Add(tag);
                }
            }
        }

        var levelDescriptionConfigs = new SortedDictionary<int, SkillDescriptionVariables>();
        if (dto.LevelDescriptionConfigs == null)
        {
            diagnostics.Add(Required(context, "/level_description_configs"));
        }

        else
        {
            foreach (
                KeyValuePair<string, IReadOnlyDictionary<string, string>> pair in dto.LevelDescriptionConfigs
            )
            {
                string escapedKey = EscapePointerToken(pair.Key ?? "");
                string pointer = $"/level_description_configs/{escapedKey}";
                if (
                    !TryParseCanonicalLevel(pair.Key, out int level)
                    || (!allowResourceOnlyShape && !hasDynamicMaxLevel && maxLevel >= 0 && level > maxLevel)
                )
                {
                    AddRangeDiagnostic(context, pointer, diagnostics);
                    continue;
                }
                if (pair.Value == null)
                {
                    diagnostics.Add(Required(context, pointer));
                    continue;
                }
                levelDescriptionConfigs.Add(level, new SkillDescriptionVariables(pair.Value));
            }
        }

        CombatSkillImportModel? combatProfile = null;
        if (dto.CombatProfile != null)
        {
            combatProfile = NormalizeCombatProfile(
                context,
                dto.CombatProfile,
                dto.SkillId,
                maxLevel,
                hasDynamicMaxLevel,
                allowResourceOnlyShape,
                diagnostics
            );
        }

        SkillImportModel? model = SkillRootCombatJsonNormalizer.NormalizeSkill(
            context,
            dto,
            skillId,
            dto.DisplayName,
            dto.Description,
            skillType,
            maxLevel,
            learnSource,
            tags,
            dto.LevelDescriptionTemplate,
            levelDescriptionConfigs,
            combatProfile,
            diagnostics
        );

        if (diagnostics.Count > 0 || model == null)
            return ContentImportStageResult<SkillImportModel>.Failure(diagnostics);

        return ContentImportStageResult<SkillImportModel>.Success(model);
    }

    /// <summary>
    /// Test-fixture-only normalization entry used by validator golden inputs that intentionally
    /// exercise states rejected earlier by the production JSON boundary. It returns only the
    /// canonical plain CLR import model and is never registered as a content-domain API.
    /// </summary>
    internal static ContentImportStageResult<SkillImportModel> NormalizeDiagnosticFixture(
        JsonContentEntryContext context,
        SkillJsonDto dto
    ) => Normalize(context, dto, allowResourceOnlyShape: true);

    private static CombatSkillImportModel? NormalizeCombatProfile(
        JsonContentEntryContext context,
        CombatSkillJsonDto dto,
        string rootSkillId,
        int maxLevel,
        bool hasDynamicMaxLevel,
        bool allowResourceOnlyShape,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        int startingErrorCount = diagnostics.Count;
        int rangeValue = dto.RangeValue ?? 1;
        int apCost = dto.ApCost ?? 1;
        int mpCost = dto.MpCost ?? 0;
        int cooldownTu = dto.CooldownTu ?? 0;
        TryIdentifier(
            dto.SkillId,
            context,
            "/combat_profile/skill_id",
            diagnostics,
            out SkillImportIdentifier skillId
        );
        if (
            !allowResourceOnlyShape
            &&
            SkillJsonImportValueRules.IsSnakeCaseId(rootSkillId)
            && SkillJsonImportValueRules.IsSnakeCaseId(dto.SkillId)
            && !string.Equals(rootSkillId, dto.SkillId, StringComparison.Ordinal)
        )
        {
            diagnostics.Add(
                Diagnostic(
                    SkillJsonImportRules.SkillIdMismatch,
                    "Combat profile skill ID must equal the owning skill ID.",
                    context,
                    "/combat_profile/skill_id"
                )
            );
        }

        if (!SkillJsonImportValueRules.TryParseTargetMode(dto.TargetMode, out CombatSkillImportTargetMode targetMode))
        {
            diagnostics.Add(
                Diagnostic(
                    SkillJsonImportRules.UnknownTargetMode,
                    "Combat target mode is not registered by the skill import contract.",
                    context,
                    "/combat_profile/target_mode"
                )
            );
        }

        if (
            !SkillJsonImportValueRules.TryParseTargetTeamFilter(
                dto.TargetTeamFilter,
                out CombatSkillImportTargetTeamFilter targetTeamFilter
            )
        )
        {
            diagnostics.Add(
                Diagnostic(
                    SkillJsonImportRules.UnknownTargetTeamFilter,
                    "Combat target team filter is not registered by the skill import contract.",
                    context,
                    "/combat_profile/target_team_filter"
                )
            );
        }

        if (!SkillJsonImportValueRules.TryParseRangePattern(dto.RangePattern, out CombatSkillImportRangePattern rangePattern))
        {
            diagnostics.Add(
                Diagnostic(
                    SkillJsonImportRules.UnknownRangePattern,
                    "Combat range pattern is not registered by the skill import contract.",
                    context,
                    "/combat_profile/range_pattern"
                )
            );
        }

        if (!SkillJsonImportValueRules.TryParseAreaPattern(dto.AreaPattern, out CombatSkillImportAreaPattern areaPattern))
        {
            diagnostics.Add(
                Diagnostic(
                    SkillJsonImportRules.UnknownAreaPattern,
                    "Combat area pattern is not registered by the skill import contract.",
                    context,
                    "/combat_profile/area_pattern"
                )
            );
        }

        if (!allowResourceOnlyShape)
        {
            ValidateNonNegative(rangeValue, context, "/combat_profile/range_value", diagnostics);
            ValidateNonNegative(apCost, context, "/combat_profile/ap_cost", diagnostics);
            ValidateNonNegative(mpCost, context, "/combat_profile/mp_cost", diagnostics);
            ValidateNonNegative(cooldownTu, context, "/combat_profile/cooldown_tu", diagnostics);
        }

        var effects = new List<CombatEffectImportModel>();
        if (dto.EffectDefs == null)
        {
            diagnostics.Add(Required(context, "/combat_profile/effect_defs"));
        }
        else
        {
            for (int index = 0; index < dto.EffectDefs.Count; index += 1)
            {
                CombatEffectImportModel? effect = NormalizeEffect(
                    context,
                    dto.EffectDefs[index],
                    $"/combat_profile/effect_defs/{index}",
                    diagnostics,
                    validateNumericRanges: !allowResourceOnlyShape
                );
                if (effect != null)
                    effects.Add(effect);
            }
        }

        var overrides = new SortedDictionary<int, CombatSkillLevelOverrideImportModel>();
        if (dto.LevelOverrides == null)
        {
            diagnostics.Add(Required(context, "/combat_profile/level_overrides"));
        }
        else
        {
            foreach (KeyValuePair<string, SkillLevelOverrideJsonDto> pair in dto.LevelOverrides)
            {
                string escapedKey = EscapePointerToken(pair.Key ?? "");
                string pointer = $"/combat_profile/level_overrides/{escapedKey}";
                if (
                    !TryParseCanonicalLevel(pair.Key, out int level)
                    || (!allowResourceOnlyShape && !hasDynamicMaxLevel && maxLevel >= 0 && level > maxLevel)
                )
                {
                    AddRangeDiagnostic(context, pointer, diagnostics);
                    continue;
                }

                SkillLevelOverrideJsonDto? overrideDto = pair.Value;
                if (overrideDto == null)
                {
                    diagnostics.Add(Required(context, pointer));
                    continue;
                }

                int beforeOverride = diagnostics.Count;
                if (!allowResourceOnlyShape)
                {
                    ValidateOptionalNonNegative(overrideDto.ApCost, context, $"{pointer}/ap_cost", diagnostics);
                    ValidateOptionalNonNegative(overrideDto.MpCost, context, $"{pointer}/mp_cost", diagnostics);
                    ValidateOptionalNonNegative(
                        overrideDto.StaminaCost,
                        context,
                        $"{pointer}/stamina_cost",
                        diagnostics
                    );
                    ValidateOptionalNonNegative(
                        overrideDto.MpCostPerTargetSlot,
                        context,
                        $"{pointer}/mp_cost_per_target_slot",
                        diagnostics
                    );
                    ValidateOptionalNonNegative(
                        overrideDto.StaminaCostPerTargetSlot,
                        context,
                        $"{pointer}/stamina_cost_per_target_slot",
                        diagnostics
                    );
                    ValidateOptionalNonNegative(
                        overrideDto.AuraCost,
                        context,
                        $"{pointer}/aura_cost",
                        diagnostics
                    );
                    ValidateOptionalNonNegative(
                        overrideDto.CooldownTu,
                        context,
                        $"{pointer}/cooldown_tu",
                        diagnostics
                    );
                    ValidateOptionalNonNegative(
                        overrideDto.CastingTimeTu,
                        context,
                        $"{pointer}/casting_time_tu",
                        diagnostics
                    );
                    ValidateOptionalNonNegative(
                        overrideDto.CastingMaintenanceDc,
                        context,
                        $"{pointer}/casting_maintenance_dc",
                        diagnostics
                    );
                    ValidateOptionalNonNegative(
                        overrideDto.CastingSpellControlDc,
                        context,
                        $"{pointer}/casting_spell_control_dc",
                        diagnostics
                    );
                    ValidateOptionalNonNegative(
                        overrideDto.AreaValue,
                        context,
                        $"{pointer}/area_value",
                        diagnostics
                    );
                    ValidateOptionalNonNegative(
                        overrideDto.RangeValue,
                        context,
                        $"{pointer}/range_value",
                        diagnostics
                    );
                    ValidateOptionalPositive(
                        overrideDto.MaxTargetCount,
                        context,
                        $"{pointer}/max_target_count",
                        diagnostics
                    );
                    ValidateOptionalPositive(
                        overrideDto.RandomChainAttackCount,
                        context,
                        $"{pointer}/random_chain_attack_count",
                        diagnostics
                    );
                }

                PendingCastBindingModeKind? pendingCastBindingMode = null;
                if (overrideDto.PendingCastBindingMode != null)
                {
                    if (
                        SkillJsonImportValueRules.TryParsePendingCastBindingMode(
                            overrideDto.PendingCastBindingMode,
                            out PendingCastBindingModeKind parsedPendingCastBindingMode
                        )
                    )
                    {
                        pendingCastBindingMode = parsedPendingCastBindingMode;
                    }
                    else
                    {
                        diagnostics.Add(
                            Diagnostic(
                                SkillJsonImportRules.UnknownPendingCastBindingMode,
                                "Pending cast binding mode is not registered by the skill import contract.",
                                context,
                                $"{pointer}/pending_cast_binding_mode"
                            )
                        );
                    }
                }

                CombatSkillLevelOverrideAttackResolutionMode? attackResolutionMode = null;
                if (overrideDto.AttackResolutionMode != null)
                {
                    if (
                        SkillJsonImportValueRules.TryParseLevelOverrideAttackResolutionMode(
                            overrideDto.AttackResolutionMode,
                            out CombatSkillLevelOverrideAttackResolutionMode parsedAttackResolutionMode
                        )
                    )
                    {
                        attackResolutionMode = parsedAttackResolutionMode;
                    }
                    else
                    {
                        diagnostics.Add(
                            Diagnostic(
                                SkillJsonImportRules.UnknownAttackResolutionMode,
                                "Attack resolution mode is not registered by the skill import contract.",
                                context,
                                $"{pointer}/attack_resolution_mode"
                            )
                        );
                    }
                }

                CombatSkillLevelOverrideAttackDefenseMode? attackDefenseMode = null;
                if (overrideDto.AttackDefenseMode != null)
                {
                    if (
                        SkillJsonImportValueRules.TryParseLevelOverrideAttackDefenseMode(
                            overrideDto.AttackDefenseMode,
                            out CombatSkillLevelOverrideAttackDefenseMode parsedAttackDefenseMode
                        )
                    )
                    {
                        attackDefenseMode = parsedAttackDefenseMode;
                    }
                    else
                    {
                        diagnostics.Add(
                            Diagnostic(
                                SkillJsonImportRules.UnknownAttackDefenseMode,
                                "Attack defense mode is not registered by the skill import contract.",
                                context,
                                $"{pointer}/attack_defense_mode"
                            )
                        );
                    }
                }

                CombatSkillLevelOverrideAreaPattern? overrideAreaPattern = null;
                if (overrideDto.AreaPattern != null)
                {
                    if (
                        SkillJsonImportValueRules.TryParseLevelOverrideAreaPattern(
                            overrideDto.AreaPattern,
                            out CombatSkillLevelOverrideAreaPattern parsedAreaPattern
                        )
                    )
                    {
                        overrideAreaPattern = parsedAreaPattern;
                    }
                    else
                    {
                        diagnostics.Add(
                            Diagnostic(
                                SkillJsonImportRules.UnknownLevelOverrideAreaPattern,
                                "Level override area pattern is not registered by the skill import contract.",
                                context,
                                $"{pointer}/area_pattern"
                            )
                        );
                    }
                }

                if (diagnostics.Count == beforeOverride)
                {
                    overrides.Add(
                        level,
                        new CombatSkillLevelOverrideImportModel(
                            apCost: overrideDto.ApCost,
                            mpCost: overrideDto.MpCost,
                            staminaCost: overrideDto.StaminaCost,
                            mpCostPerTargetSlot: overrideDto.MpCostPerTargetSlot,
                            staminaCostPerTargetSlot: overrideDto.StaminaCostPerTargetSlot,
                            auraCost: overrideDto.AuraCost,
                            cooldownTu: overrideDto.CooldownTu,
                            castingTimeTu: overrideDto.CastingTimeTu,
                            castingMaintenanceDc: overrideDto.CastingMaintenanceDc,
                            castingSpellControlDc: overrideDto.CastingSpellControlDc,
                            pendingCastBindingMode: pendingCastBindingMode,
                            attackRollBonus: overrideDto.AttackRollBonus,
                            attackResolutionMode: attackResolutionMode,
                            attackDefenseMode: attackDefenseMode,
                            areaValue: overrideDto.AreaValue,
                            rangeValue: overrideDto.RangeValue,
                            areaPattern: overrideAreaPattern,
                            maxTargetCount: overrideDto.MaxTargetCount,
                            randomChainAttackCount: overrideDto.RandomChainAttackCount
                        )
                    );
                }
            }
        }

        CombatSkillImportModel? model =
            SkillRootCombatJsonNormalizer.NormalizeCombatSkill(
                context,
                dto,
                skillId,
                targetMode,
                targetTeamFilter,
                rangePattern,
                rangeValue,
                areaPattern,
                apCost,
                mpCost,
                cooldownTu,
                effects,
                overrides,
                (effectDto, pointer, targetDiagnostics) =>
                    NormalizeEffect(
                        context,
                        effectDto,
                        pointer,
                        targetDiagnostics,
                        validateNumericRanges: !allowResourceOnlyShape
                    ),
                requireCanonicalSquare2Payload: !allowResourceOnlyShape,
                diagnostics
            );

        if (diagnostics.Count != startingErrorCount || model == null)
            return null;

        return model;
    }

    private static CombatEffectImportModel? NormalizeEffect(
        JsonContentEntryContext context,
        CombatEffectJsonDto? dto,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics,
        bool validateNumericRanges
    ) => NormalizeFullCombatEffect(
        context,
        dto,
        pointer,
        diagnostics,
        validateNumericRanges
    );

    private static LayeredBarrierEffectPayloadImportModel? NormalizeLayeredBarrierPayload(
        JsonContentEntryContext context,
        JsonElement payload,
        string payloadPointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        if (payload.ValueKind == JsonValueKind.Object)
        {
            ContentJsonDiagnostic? missing = FindMissing(
                payload,
                CombatEffectImportClosedSpec.LayeredBarrierRequiredPayloadPropertyNames,
                context,
                payloadPointer
            );
            if (missing != null)
            {
                diagnostics.Add(missing);
                return null;
            }
        }

        var payloadContext = new JsonContentEntryContext(
            context.DomainId,
            context.EntryId,
            context.SourceLabel,
            $"{context.JsonPointer}{payloadPointer}"
        );
        ContentImportStageResult<LayeredBarrierEffectPayloadJsonDto> parsed =
            ContentJsonStrictDtoParser.Parse(
                payloadContext,
                payload.GetRawText(),
                SkillJsonImportSerializerContext.Default.LayeredBarrierEffectPayloadJsonDto,
                SkillJsonImportRules.InvalidEffectPayload
            );
        if (!parsed.HasValue)
        {
            diagnostics.AddRange(parsed.Diagnostics);
            return null;
        }

        int startingErrorCount = diagnostics.Count;
        LayeredBarrierEffectPayloadJsonDto value = parsed.Value;
        if (
            !SkillJsonImportValueRules.TryParseAreaPattern(
                value.AreaPattern,
                out CombatSkillImportAreaPattern areaPattern
            )
        )
        {
            diagnostics.Add(
                Diagnostic(
                    SkillJsonImportRules.UnknownAreaPattern,
                    "Layered barrier area pattern is not registered by the skill import contract.",
                    context,
                    $"{payloadPointer}/area_pattern"
                )
            );
        }

        TryIdentifier(
            value.ProfileId,
            context,
            $"{payloadPointer}/profile_id",
            diagnostics,
            out SkillImportIdentifier profileId
        );

        return diagnostics.Count == startingErrorCount
            ? new LayeredBarrierEffectPayloadImportModel(
                areaPattern,
                profileId,
                value.RadiusCells,
                value.SaveDc
            )
            : null;
    }

    private static ContentJsonDiagnostic? FindMissingRequiredMember(
        JsonContentEntryContext context,
        string json
    )
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;

            string[] rootRequired =
            {
                "skill_id", "display_name",
            };
            ContentJsonDiagnostic? missing = FindMissing(root, rootRequired, context, "");
            if (missing != null)
                return missing;
            missing = FindExplicitNull(
                root,
                new[]
                {
                    "description", "skill_type", "max_level", "learn_source", "tags",
                    "level_description_template", "level_description_configs",
                },
                context,
                ""
            );
            if (missing != null)
                return missing;
            missing = FindExpandedRootExplicitNull(root, context);
            if (missing != null)
                return missing;

            if (
                root.TryGetProperty(
                    "level_description_configs",
                    out JsonElement descriptionConfigs
                )
                && descriptionConfigs.ValueKind == JsonValueKind.Object
            )
            {
                missing = FindDuplicateCanonicalLevelKey(
                    descriptionConfigs,
                    context,
                    "/level_description_configs"
                );
                if (missing != null)
                    return missing;
                foreach (JsonProperty levelConfig in descriptionConfigs.EnumerateObject())
                {
                    string levelPointer =
                        $"/level_description_configs/{EscapePointerToken(levelConfig.Name)}";
                    if (levelConfig.Value.ValueKind == JsonValueKind.Null)
                        return Required(context, levelPointer);
                    if (levelConfig.Value.ValueKind != JsonValueKind.Object)
                        continue;
                    var seenVariableKeys = new HashSet<string>(StringComparer.Ordinal);
                    foreach (JsonProperty variable in levelConfig.Value.EnumerateObject())
                    {
                        if (!seenVariableKeys.Add(variable.Name))
                        {
                            return Diagnostic(
                                SkillJsonImportRules.DuplicateDescriptionVariableKey,
                                "A level description variable key must be unique within its level config.",
                                context,
                                $"{levelPointer}/{EscapePointerToken(variable.Name)}"
                            );
                        }
                        if (variable.Value.ValueKind == JsonValueKind.Null)
                        {
                            return Required(
                                context,
                                $"{levelPointer}/{EscapePointerToken(variable.Name)}"
                            );
                        }
                    }
                }
            }

            if (
                !root.TryGetProperty("combat_profile", out JsonElement combat)
                || combat.ValueKind != JsonValueKind.Object
            )
            {
                return null;
            }

            string[] combatRequired =
            {
                "skill_id",
            };
            missing = FindMissing(combat, combatRequired, context, "/combat_profile");
            if (missing != null)
                return missing;
            missing = FindExplicitNull(
                combat,
                new[]
                {
                    "target_mode", "target_team_filter", "range_pattern", "area_pattern",
                    "range_value", "ap_cost", "mp_cost", "cooldown_tu", "effect_defs",
                    "level_overrides",
                },
                context,
                "/combat_profile"
            );
            if (missing != null)
                return missing;
            missing = FindExpandedCombatExplicitNull(combat, context);
            if (missing != null)
                return missing;

            missing = FindEffectArrayContract(
                combat,
                "effect_defs",
                context,
                "/combat_profile/effect_defs"
            );
            if (missing != null)
                return missing;
            missing = FindEffectArrayContract(
                combat,
                "passive_effect_defs",
                context,
                "/combat_profile/passive_effect_defs"
            );
            if (missing != null)
                return missing;

            if (
                combat.TryGetProperty("level_overrides", out JsonElement overrides)
                && overrides.ValueKind == JsonValueKind.Object
            )
            {
                missing = FindDuplicateCanonicalLevelKey(
                    overrides,
                    context,
                    "/combat_profile/level_overrides"
                );
                if (missing != null)
                    return missing;
                foreach (JsonProperty levelOverride in overrides.EnumerateObject())
                {
                    string levelPointer =
                        $"/combat_profile/level_overrides/{EscapePointerToken(levelOverride.Name)}";
                    if (levelOverride.Value.ValueKind == JsonValueKind.Null)
                        return Required(context, levelPointer);
                    if (levelOverride.Value.ValueKind != JsonValueKind.Object)
                        continue;
                    missing = FindExplicitNull(
                        levelOverride.Value,
                        new[]
                        {
                            "ap_cost", "mp_cost", "stamina_cost",
                            "mp_cost_per_target_slot", "stamina_cost_per_target_slot",
                            "aura_cost", "cooldown_tu", "casting_time_tu",
                            "casting_maintenance_dc", "casting_spell_control_dc",
                            "pending_cast_binding_mode", "attack_roll_bonus",
                            "attack_resolution_mode", "attack_defense_mode", "area_value",
                            "range_value", "area_pattern", "max_target_count",
                            "random_chain_attack_count",
                        },
                        context,
                        levelPointer
                    );
                    if (missing != null)
                        return missing;
                }
            }
        }
        catch (JsonException)
        {
            // 这是 strict DTO 解析之前的预扫描（见 ParseDto）。JSON 根本解析不了时这里无话可说，
            // 紧接着的 ContentJsonStrictDtoParser.Parse 会带着 exception.Path 报 InvalidDto。
            // 返回 null 表示"本次预扫描没有发现缺失成员"，不会让格式错误逃逸。
            return null;
        }

        return null;
    }

    private static ContentJsonDiagnostic? FindExpandedRootExplicitNull(
        JsonElement root,
        JsonContentEntryContext context
    )
    {
        ContentJsonDiagnostic? missing = FindExplicitNull(
            root,
            new[]
            {
                "icon_id", "dynamic_max_level_stat_id", "mastery_curve",
                "learn_requirements", "unlock_mode", "knowledge_requirements",
                "skill_level_requirements", "attribute_requirements",
                "achievement_requirements", "upgrade_source_skill_ids",
                "retain_source_skills_on_unlock", "core_skill_transition_mode",
                "mastery_sources", "growth_tier", "attribute_growth_progress",
                "practice_tier", "attribute_modifiers",
            },
            context,
            ""
        );
        if (missing != null)
            return missing;

        missing = FindExplicitEmptyString(
            root,
            new[]
            {
                ("growth_tier", "skill.dto.growth_tier.unknown"),
                ("practice_tier", "skill.dto.practice_tier.unknown"),
            },
            context,
            ""
        );
        if (missing != null)
            return missing;

        if (
            root.TryGetProperty("attribute_modifiers", out JsonElement modifiers)
            && modifiers.ValueKind == JsonValueKind.Array
        )
        {
            int index = 0;
            foreach (JsonElement modifier in modifiers.EnumerateArray())
            {
                string pointer = $"/attribute_modifiers/{index}";
                if (modifier.ValueKind == JsonValueKind.Null)
                    return Required(context, pointer);
                if (modifier.ValueKind == JsonValueKind.Object)
                {
                    missing = FindExplicitNull(
                        modifier,
                        new[] { "attribute_id", "mode", "source_type", "source_id" },
                        context,
                        pointer
                    );
                    if (missing != null)
                        return missing;
                }
                index += 1;
            }
        }

        return FindNestedExplicitNull(
            root,
            "contingency_automation_profile",
            new[]
            {
                "min_contingency_skill_level", "effect_category", "tags",
                "allowed_target_resolvers", "allowed_parameter_bindings",
            },
            context,
            "/contingency_automation_profile"
        );
    }

    private static ContentJsonDiagnostic? FindExpandedCombatExplicitNull(
        JsonElement combat,
        JsonContentEntryContext context
    )
    {
        ContentJsonDiagnostic? missing = FindExplicitNull(
            combat,
            new[]
            {
                "excluded_target_creature_type_tags", "weapon_range_policy",
                "pending_cast_binding_mode", "attack_resolution_mode",
                "attack_defense_mode", "mastery_trigger_mode", "mastery_amount_mode",
                "mastery_base_amount", "spell_fate_mode", "spell_critical_mode",
                "fumble_protection_curve", "fumble_protection_extra_mp_percent",
                "backlash_mode", "backlash_target_filter", "area_origin_mode",
                "area_direction_mode", "ai_tags", "delivery_categories",
                "attack_roll_bonus_status_id", "projectile_kind",
                "special_resolution_profile_id", "target_selection_mode",
                "min_target_count", "max_target_count", "unit_target_resolution_mode",
                "selection_order_mode", "passive_effect_defs", "cast_variants",
                "required_weapon_families", "required_weapon_type_ids",
                "excluded_weapon_families", "excluded_weapon_type_ids",
                "mastery_low_hp_bonus_multiplier", "mastery_low_hp_threshold_percent",
            },
            context,
            "/combat_profile"
        );
        if (missing != null)
            return missing;

        missing = FindExplicitEmptyString(
            combat,
            new[]
            {
                ("weapon_range_policy", "skill.dto.weapon_range_policy.unknown"),
                ("attack_resolution_mode", "skill.dto.attack_resolution_mode.unknown"),
                ("spell_fate_mode", "skill.dto.spell_fate_mode.unknown"),
                ("spell_critical_mode", "skill.dto.spell_critical_mode.unknown"),
                ("backlash_mode", "skill.dto.backlash_mode.unknown"),
                ("backlash_target_filter", "skill.dto.backlash_target_filter.unknown"),
            },
            context,
            "/combat_profile"
        );
        if (missing != null)
            return missing;

        missing = FindNestedExplicitNull(
            combat,
            "windup_profile",
            new[]
            {
                "stamina_cost_per_tier", "weapon_dice_per_tier",
                "skill_level_tier_caps", "base_weapon_dice_multipliers",
            },
            context,
            "/combat_profile/windup_profile"
        );
        if (missing != null)
            return missing;
        missing = FindNestedExplicitNull(
            combat,
            "directional_piercing_profile",
            new[]
            {
                "base_damage_percent_curve", "successful_hit_decay_percent",
                "minimum_damage_percent", "stamina_flat_base",
                "stamina_range_square_coefficient", "stamina_strength_square_scale",
                "minimum_stamina_cost", "maximum_height_delta",
            },
            context,
            "/combat_profile/directional_piercing_profile"
        );
        if (missing != null)
            return missing;
        missing = FindNestedExplicitNull(
            combat,
            "line_through_attack_profile",
            new[]
            {
                "maximum_weapon_range", "intermediate_weapon_dice_multiplier",
                "primary_weapon_dice_multiplier_curve", "primary_attack_roll_bonus_curve",
                "successful_intermediate_hit_bonus_weapon_dice",
                "successful_intermediate_hit_attack_roll_bonus",
                "successful_intermediate_hit_bonus_cap_curve",
            },
            context,
            "/combat_profile/line_through_attack_profile"
        );
        if (missing != null)
            return missing;
        missing = FindNestedExplicitNull(
            combat,
            "sequential_line_hit_profile",
            new[]
            {
                "minimum_primary_distance_curve", "continuation_range_curve",
                "follow_up_attack_penalty_curve",
            },
            context,
            "/combat_profile/sequential_line_hit_profile"
        );
        if (missing != null)
            return missing;
        missing = FindNestedExplicitNull(
            combat,
            "spell_reaction_profile",
            new[]
            {
                "trigger_delivery_category", "reaction_skill_id", "readiness_status_id",
                "required_weapon_family", "save_ability", "save_tag", "base_save_dc",
                "hp_damage_divisor", "attack_roll_bonus_by_skill_level",
                "save_dc_bonus_by_skill_level", "require_hp_damage", "consume_on_trigger",
                "expire_on_owner_turn_start",
            },
            context,
            "/combat_profile/spell_reaction_profile"
        );
        if (missing != null)
            return missing;
        missing = FindNestedExplicitNull(
            combat,
            "ranged_weapon_reaction_profile",
            new[]
            {
                "readiness_status_id", "trigger_weapon_families", "damage_tag",
                "attack_defense_mode", "attack_roll_bonus_by_skill_level",
                "consume_status_stacks", "trigger_on_hit", "trigger_on_miss",
            },
            context,
            "/combat_profile/ranged_weapon_reaction_profile"
        );
        if (missing != null)
            return missing;

        if (
            combat.TryGetProperty("cast_variants", out JsonElement variants)
            && variants.ValueKind == JsonValueKind.Array
        )
        {
            int index = 0;
            foreach (JsonElement variant in variants.EnumerateArray())
            {
                string pointer = $"/combat_profile/cast_variants/{index}";
                if (variant.ValueKind == JsonValueKind.Null)
                    return Required(context, pointer);
                if (variant.ValueKind == JsonValueKind.Object)
                {
                    missing = FindMissing(variant, new[] { "variant_id" }, context, pointer);
                    if (missing != null)
                        return missing;
                    missing = FindExplicitEmptyString(
                        variant,
                        new[]
                        {
                            (
                                "projectile_kind_override",
                                "skill.dto.cast_variant.projectile_kind_override.unknown"
                            ),
                        },
                        context,
                        pointer
                    );
                    if (missing != null)
                        return missing;
                    missing = FindExplicitNull(
                        variant,
                        new[]
                        {
                            "display_name", "description", "target_mode",
                            "footprint_pattern", "required_coord_count",
                            "allowed_base_terrains", "projectile_kind_override", "effect_defs",
                            "payload",
                        },
                        context,
                        pointer
                    );
                    if (missing != null)
                        return missing;
                    missing = FindEffectArrayContract(
                        variant,
                        "effect_defs",
                        context,
                        $"{pointer}/effect_defs"
                    );
                    if (missing != null)
                        return missing;
                    if (
                        variant.TryGetProperty("payload", out JsonElement castPayload)
                        && castPayload.ValueKind == JsonValueKind.Object
                    )
                    {
                        missing = FindExplicitNull(
                            castPayload,
                            new[] { "square2_corner" },
                            context,
                            $"{pointer}/payload"
                        );
                        if (missing != null)
                            return missing;
                    }
                }
                index += 1;
            }
        }
        return null;
    }

    private static ContentJsonDiagnostic? FindEffectArrayContract(
        JsonElement parent,
        string propertyName,
        JsonContentEntryContext context,
        string pointer
    )
    {
        if (
            !parent.TryGetProperty(propertyName, out JsonElement effects)
            || effects.ValueKind != JsonValueKind.Array
        )
        {
            return null;
        }

        int index = 0;
        foreach (JsonElement effect in effects.EnumerateArray())
        {
            string effectPointer = $"{pointer}/{index}";
            if (effect.ValueKind == JsonValueKind.Null)
                return Required(context, effectPointer);
            if (effect.ValueKind == JsonValueKind.Object)
            {
                ContentJsonDiagnostic? missing = FindEffectObjectContract(
                    effect,
                    context,
                    effectPointer
                );
                if (missing != null)
                    return missing;
            }
            index += 1;
        }
        return null;
    }

    private static ContentJsonDiagnostic? FindEffectObjectContract(
        JsonElement effect,
        JsonContentEntryContext context,
        string pointer
    )
    {
        ContentJsonDiagnostic? issue = FindMissing(
            effect,
            new[] { "effect_type" },
            context,
            pointer
        );
        if (issue != null)
            return issue;

        if (
            effect.TryGetProperty("effect_type", out JsonElement effectType)
            && effectType.ValueKind == JsonValueKind.String
            && SkillJsonImportValueRules.TryParseEffectKind(
                effectType.GetString(),
                out CombatEffectImportKind kind
            )
        )
        {
            if (
                !effect.TryGetProperty("payload", out JsonElement payload)
                || payload.ValueKind == JsonValueKind.Null
            )
            {
                return Diagnostic(
                    SkillJsonImportRules.MissingEffectPayload,
                    "Registered combat effect kind requires a typed payload object.",
                    context,
                    $"{pointer}/payload"
                );
            }

            if (payload.ValueKind == JsonValueKind.Object)
            {
                issue = FindMissing(
                    payload,
                    RequiredPayloadPropertyNames(
                        SkillFullCombatEffectClosedSpec.GetPayloadShape(kind)
                    ),
                    context,
                    $"{pointer}/payload"
                );
                if (issue != null)
                    return issue;

                issue = FindExplicitEmptyString(
                    payload,
                    PayloadClosedScalarPropertyNames,
                    context,
                    $"{pointer}/payload"
                );
                if (issue != null)
                    return issue;

                if (
                    kind == CombatEffectImportKind.EquipmentDurabilityDamage
                    && payload.TryGetProperty("target_slots", out JsonElement targetSlots)
                    && targetSlots.ValueKind == JsonValueKind.Array
                    && targetSlots.GetArrayLength() == 0
                )
                {
                    return Required(context, $"{pointer}/payload/target_slots");
                }

                if (kind == CombatEffectImportKind.RepeatAttackUntilFail)
                {
                    issue = FindDuplicatePenaltyFreeStageLevel(
                        payload,
                        context,
                        $"{pointer}/payload"
                    );
                    if (issue != null)
                        return issue;
                }
            }
        }

        issue = FindExplicitEmptyString(
            effect,
            EffectClosedScalarPropertyNames,
            context,
            pointer
        );
        if (issue != null)
            return issue;

        issue = FindFirstExplicitNull(effect, context, pointer);
        if (issue != null)
            return issue;

        if (
            effect.TryGetProperty("extra_damage_segments", out JsonElement segments)
            && segments.ValueKind == JsonValueKind.Array
        )
        {
            int segmentIndex = 0;
            foreach (JsonElement segment in segments.EnumerateArray())
            {
                if (segment.ValueKind == JsonValueKind.Object)
                {
                    issue = FindExplicitEmptyString(
                        segment,
                        NestedDamageSegmentClosedScalarPropertyNames,
                        context,
                        $"{pointer}/extra_damage_segments/{segmentIndex}"
                    );
                    if (issue != null)
                        return issue;
                }
                segmentIndex += 1;
            }
        }

        if (
            effect.TryGetProperty(
                "save_failure_status_outcomes",
                out JsonElement outcomes
            )
            && outcomes.ValueKind == JsonValueKind.Array
        )
        {
            int index = 0;
            foreach (JsonElement outcome in outcomes.EnumerateArray())
            {
                string outcomePointer =
                    $"{pointer}/save_failure_status_outcomes/{index}";
                if (outcome.ValueKind == JsonValueKind.Object)
                {
                    issue = FindMissing(
                        outcome,
                        new[] { "status_effect" },
                        context,
                        outcomePointer
                    );
                    if (issue != null)
                        return issue;
                }
                if (
                    outcome.ValueKind == JsonValueKind.Object
                    && outcome.TryGetProperty(
                        "status_effect",
                        out JsonElement nestedEffect
                    )
                    && nestedEffect.ValueKind == JsonValueKind.Object
                )
                {
                    issue = FindEffectObjectContract(
                        nestedEffect,
                        context,
                        $"{outcomePointer}/status_effect"
                    );
                    if (issue != null)
                        return issue;
                }
                index += 1;
            }
        }

        return null;
    }

    private static IReadOnlyList<string> RequiredPayloadPropertyNames(
        CombatEffectPayloadShape shape
    ) =>
        shape switch
        {
            CombatEffectPayloadShape.LayeredBarrier =>
                CombatEffectImportClosedSpec.LayeredBarrierRequiredPayloadPropertyNames,
            CombatEffectPayloadShape.EquipmentDurabilityDamage =>
                new[] { "target_slots" },
            CombatEffectPayloadShape.GradedSaveExecute => GradedPayloadRequiredPropertyNames,
            _ => Array.Empty<string>(),
        };

    private static ContentJsonDiagnostic? FindFirstExplicitNull(
        JsonElement value,
        JsonContentEntryContext context,
        string pointer
    )
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in value.EnumerateObject())
            {
                string childPointer = $"{pointer}/{EscapePointerToken(property.Name)}";
                if (property.Value.ValueKind == JsonValueKind.Null)
                    return Required(context, childPointer);
                ContentJsonDiagnostic? nested = FindFirstExplicitNull(
                    property.Value,
                    context,
                    childPointer
                );
                if (nested != null)
                    return nested;
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            int index = 0;
            foreach (JsonElement item in value.EnumerateArray())
            {
                string childPointer = $"{pointer}/{index}";
                if (item.ValueKind == JsonValueKind.Null)
                    return Required(context, childPointer);
                ContentJsonDiagnostic? nested = FindFirstExplicitNull(
                    item,
                    context,
                    childPointer
                );
                if (nested != null)
                    return nested;
                index += 1;
            }
        }
        return null;
    }

    private static ContentJsonDiagnostic? FindDuplicatePenaltyFreeStageLevel(
        JsonElement payload,
        JsonContentEntryContext context,
        string pointer
    )
    {
        if (
            !payload.TryGetProperty(
                "penalty_free_stages_by_level",
                out JsonElement levels
            )
            || levels.ValueKind != JsonValueKind.Object
        )
        {
            return null;
        }

        var seen = new HashSet<int>();
        foreach (JsonProperty property in levels.EnumerateObject())
        {
            string keyPointer =
                $"{pointer}/penalty_free_stages_by_level/{EscapePointerToken(property.Name)}";
            if (!TryParseCanonicalNonNegativeInt(property.Name, out int level))
            {
                return Diagnostic(
                    SkillJsonImportRules.InvalidId,
                    "Penalty-free stage level key must be a canonical nonnegative integer.",
                    context,
                    keyPointer
                );
            }
            if (!seen.Add(level))
            {
                return Diagnostic(
                    SkillJsonImportRules.DuplicateLevelKey,
                    "Penalty-free stage level key must be unique.",
                    context,
                    keyPointer
                );
            }
        }
        return null;
    }

    private static bool TryParseCanonicalNonNegativeInt(string? value, out int result)
    {
        result = default;
        if (
            string.IsNullOrEmpty(value)
            || (value.Length > 1 && value[0] == '0')
            || !int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out result)
            || result < 0
        )
        {
            result = default;
            return false;
        }
        return true;
    }

    private static readonly IReadOnlyList<(string PropertyName, string RuleId)>
        EffectClosedScalarPropertyNames = Array.AsReadOnly(
            new[]
            {
                ("tick_effect_type", SkillJsonImportRules.InvalidEffectPayload),
                ("lifetime_policy", SkillJsonImportRules.InvalidEffectPayload),
                ("damage_tag", SkillJsonImportRules.InvalidEffectPayload),
                ("damage_category", SkillJsonImportRules.InvalidEffectPayload),
                ("shield_attribute_modifier_id", SkillJsonImportRules.InvalidEffectPayload),
                ("path_step_area_pattern", SkillJsonImportRules.InvalidEffectPayload),
                ("mitigation_tier", SkillJsonImportRules.InvalidEffectPayload),
                ("effect_target_team_filter", SkillJsonImportRules.InvalidEffectPayload),
                ("target_order", SkillJsonImportRules.InvalidEffectPayload),
                ("required_target_min_cognition", SkillJsonImportRules.InvalidEffectPayload),
                ("terrain_contact_mode", SkillJsonImportRules.InvalidEffectPayload),
                ("body_size_category", SkillJsonImportRules.InvalidEffectPayload),
                ("forced_move_mode", SkillJsonImportRules.InvalidEffectPayload),
                ("stack_behavior", SkillJsonImportRules.InvalidEffectPayload),
                ("bonus_condition", SkillJsonImportRules.InvalidEffectPayload),
                ("trigger_event", SkillJsonImportRules.InvalidEffectPayload),
                ("trigger_condition", SkillJsonImportRules.InvalidEffectPayload),
                ("save_dc_mode", SkillJsonImportRules.InvalidEffectPayload),
                ("save_dc_source_ability", SkillJsonImportRules.InvalidEffectPayload),
                ("save_ability", SkillJsonImportRules.InvalidEffectPayload),
                ("save_tag", SkillJsonImportRules.InvalidEffectPayload),
                ("required_target_status_source_selector", SkillJsonImportRules.InvalidEffectPayload),
                ("upkeep_resource", SkillJsonImportRules.InvalidEffectPayload),
            }
        );

    private static readonly IReadOnlyList<(string PropertyName, string RuleId)>
        PayloadClosedScalarPropertyNames = Array.AsReadOnly(
            new[]
            {
                ("area_pattern", SkillJsonImportRules.InvalidEffectPayload),
                ("cost_resource", SkillJsonImportRules.InvalidEffectPayload),
                ("grant_scope", SkillJsonImportRules.InvalidEffectPayload),
            }
        );

    private static readonly IReadOnlyList<(string PropertyName, string RuleId)>
        NestedDamageSegmentClosedScalarPropertyNames = Array.AsReadOnly(
            new[]
            {
                ("damage_tag", SkillJsonImportRules.InvalidEffectPayload),
            }
        );

    private static readonly IReadOnlyList<string> GradedPayloadRequiredPropertyNames =
        Array.AsReadOnly(
            new[]
            {
                "critical_failure_damage_dice_count",
                "critical_failure_damage_dice_sides",
                "critical_failure_execute_threshold_max_hp_percent",
                "critical_failure_frightened_duration_tu",
                "critical_failure_stunned_duration_tu",
                "failure_damage_dice_count",
                "failure_damage_dice_sides",
                "failure_execute_threshold_fixed",
                "failure_execute_threshold_max_hp_percent",
                "failure_frightened_duration_tu",
                "failure_reaction_lock_duration_tu",
                "profile_id",
                "success_aftershock_duration_tu",
            }
        );

    private static ContentJsonDiagnostic? FindExplicitEmptyString(
        JsonElement parent,
        IEnumerable<(string PropertyName, string RuleId)> fields,
        JsonContentEntryContext context,
        string pointer
    )
    {
        foreach ((string propertyName, string ruleId) in fields)
        {
            if (
                parent.TryGetProperty(propertyName, out JsonElement value)
                && value.ValueKind == JsonValueKind.String
                && value.GetString() == ""
            )
            {
                return Diagnostic(
                    ruleId,
                    "An explicit empty string is not a registered canonical authoring value; omit the member to use its default.",
                    context,
                    $"{pointer}/{propertyName}"
                );
            }
        }
        return null;
    }

    private static ContentJsonDiagnostic? FindNestedExplicitNull(
        JsonElement parent,
        string propertyName,
        IEnumerable<string> nestedPropertyNames,
        JsonContentEntryContext context,
        string pointer
    )
    {
        return parent.TryGetProperty(propertyName, out JsonElement nested)
            && nested.ValueKind == JsonValueKind.Object
            ? FindExplicitNull(nested, nestedPropertyNames, context, pointer)
            : null;
    }

    private static ContentJsonDiagnostic? FindDuplicateCanonicalLevelKey(
        JsonElement value,
        JsonContentEntryContext context,
        string parentPointer
    )
    {
        var seenLevels = new HashSet<int>();
        foreach (JsonProperty property in value.EnumerateObject())
        {
            if (
                TryParseCanonicalLevel(property.Name, out int level)
                && !seenLevels.Add(level)
            )
            {
                return Diagnostic(
                    SkillJsonImportRules.DuplicateLevelKey,
                    "A canonical skill level key must be unique within its map.",
                    context,
                    $"{parentPointer}/{EscapePointerToken(property.Name)}"
                );
            }
        }
        return null;
    }

    private static ContentJsonDiagnostic? FindMissing(
        JsonElement value,
        IEnumerable<string> propertyNames,
        JsonContentEntryContext context,
        string parentPointer
    )
    {
        foreach (string propertyName in propertyNames)
        {
            if (
                !value.TryGetProperty(propertyName, out JsonElement propertyValue)
                || propertyValue.ValueKind == JsonValueKind.Null
            )
                return Required(context, $"{parentPointer}/{propertyName}");
        }
        return null;
    }

    private static ContentJsonDiagnostic? FindExplicitNull(
        JsonElement value,
        IEnumerable<string> propertyNames,
        JsonContentEntryContext context,
        string parentPointer
    )
    {
        foreach (string propertyName in propertyNames)
        {
            if (
                value.TryGetProperty(propertyName, out JsonElement propertyValue)
                && propertyValue.ValueKind == JsonValueKind.Null
            )
            {
                return Required(context, $"{parentPointer}/{propertyName}");
            }
        }
        return null;
    }

    private static bool TryIdentifier(
        string? value,
        JsonContentEntryContext context,
        string relativePointer,
        List<ContentJsonDiagnostic> diagnostics,
        out SkillImportIdentifier identifier
    )
    {
        if (SkillImportIdentifier.TryCreate(value, out identifier))
            return true;

        diagnostics.Add(
            Diagnostic(
                SkillJsonImportRules.InvalidId,
                "Content ID must be canonical lower snake_case ASCII.",
                context,
                relativePointer
            )
        );
        return false;
    }

    private static void RequireNonNull(
        string? value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        if (value == null)
            diagnostics.Add(Required(context, pointer));
    }

    private static bool TryParseCanonicalLevel(string? value, out int level)
    {
        level = 0;
        if (
            string.IsNullOrEmpty(value)
            || !int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out level)
            || level < 0
        )
        {
            return false;
        }
        return string.Equals(value, level.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    private static void ValidateNonNegative(
        int value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        if (value < 0)
            AddRangeDiagnostic(context, pointer, diagnostics);
    }

    private static void ValidateOptionalNonNegative(
        int? value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        if (value < 0)
            AddRangeDiagnostic(context, pointer, diagnostics);
    }

    private static void ValidateOptionalPositive(
        int? value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        if (value.HasValue && value.Value <= 0)
            AddRangeDiagnostic(context, pointer, diagnostics);
    }

    private static bool HasAnyLevelOverrideValue(SkillLevelOverrideJsonDto value) =>
        value.ApCost != null
        || value.MpCost != null
        || value.StaminaCost != null
        || value.MpCostPerTargetSlot != null
        || value.StaminaCostPerTargetSlot != null
        || value.AuraCost != null
        || value.CooldownTu != null
        || value.CastingTimeTu != null
        || value.CastingMaintenanceDc != null
        || value.CastingSpellControlDc != null
        || value.PendingCastBindingMode != null
        || value.AttackRollBonus != null
        || value.AttackResolutionMode != null
        || value.AttackDefenseMode != null
        || value.AreaValue != null
        || value.RangeValue != null
        || value.AreaPattern != null
        || value.MaxTargetCount != null
        || value.RandomChainAttackCount != null;

    private static void AddRangeDiagnostic(
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    ) =>
        diagnostics.Add(
            Diagnostic(
                SkillJsonImportRules.NumberOutOfRange,
                "Numeric value is outside the skill JSON import range.",
                context,
                pointer
            )
        );

    private static ContentJsonDiagnostic Required(
        JsonContentEntryContext context,
        string pointer
    ) =>
        Diagnostic(
            SkillJsonImportRules.RequiredMember,
            "Skill JSON member is missing or null where a concrete value is required.",
            context,
            pointer
        );

    private static ContentJsonDiagnostic Diagnostic(
        string ruleId,
        string message,
        JsonContentEntryContext context,
        string relativePointer
    ) =>
        new(ruleId, message, context.SourceLabel, $"{context.JsonPointer}{relativePointer}");

    private static string EscapePointerToken(string token) =>
        token.Replace("~", "~0", StringComparison.Ordinal)
            .Replace("/", "~1", StringComparison.Ordinal);
}
