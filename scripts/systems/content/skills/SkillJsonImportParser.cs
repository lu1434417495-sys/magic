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

internal static class SkillJsonImportParser
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
            ? Normalize(context, dtoResult.Value)
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
        SkillJsonDto dto
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

        if (maxLevel < 0)
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
                if (!TryParseCanonicalLevel(pair.Key, out int level) || level > maxLevel)
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
                diagnostics
            );
        }

        if (diagnostics.Count > 0)
            return ContentImportStageResult<SkillImportModel>.Failure(diagnostics);

        return ContentImportStageResult<SkillImportModel>.Success(
            new SkillImportModel(
                skillId,
                dto.DisplayName,
                dto.Description,
                skillType,
                maxLevel,
                learnSource,
                tags,
                dto.LevelDescriptionTemplate,
                levelDescriptionConfigs,
                combatProfile
            )
        );
    }

    private static CombatSkillImportModel? NormalizeCombatProfile(
        JsonContentEntryContext context,
        CombatSkillJsonDto dto,
        string rootSkillId,
        int maxLevel,
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

        ValidateNonNegative(rangeValue, context, "/combat_profile/range_value", diagnostics);
        ValidateNonNegative(apCost, context, "/combat_profile/ap_cost", diagnostics);
        ValidateNonNegative(mpCost, context, "/combat_profile/mp_cost", diagnostics);
        ValidateNonNegative(cooldownTu, context, "/combat_profile/cooldown_tu", diagnostics);

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
                    index,
                    diagnostics
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
                if (!TryParseCanonicalLevel(pair.Key, out int level) || level > maxLevel)
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
                ValidateOptionalNonNegative(overrideDto.CooldownTu, context, $"{pointer}/cooldown_tu", diagnostics);
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

                if (!HasAnyLevelOverrideValue(overrideDto))
                {
                    diagnostics.Add(
                        Diagnostic(
                            SkillJsonImportRules.EmptyLevelOverride,
                            "A skill level override must override at least one registered field.",
                            context,
                            pointer
                        )
                    );
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

        if (diagnostics.Count != startingErrorCount)
            return null;

        return new CombatSkillImportModel(
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
            overrides
        );
    }

    private static CombatEffectImportModel? NormalizeEffect(
        JsonContentEntryContext context,
        CombatEffectJsonDto? dto,
        int index,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        string pointer = $"/combat_profile/effect_defs/{index}";
        if (dto == null)
        {
            diagnostics.Add(Required(context, pointer));
            return null;
        }

        int startingErrorCount = diagnostics.Count;
        int minSkillLevel = dto.MinSkillLevel ?? 0;
        int maxSkillLevel = dto.MaxSkillLevel ?? -1;
        int power = dto.Power ?? 0;
        int durationTu = dto.DurationTu ?? 0;
        ICombatEffectPayloadImportModel? payload = null;
        if (
            !SkillJsonImportValueRules.TryParseEffectKind(
                dto.EffectType,
                out CombatEffectImportKind kind
            )
        )
        {
            diagnostics.Add(
                Diagnostic(
                    SkillJsonImportRules.UnknownEffectKind,
                    "Combat effect type is not registered by the pilot closed-kind contract.",
                    context,
                    $"{pointer}/effect_type"
                )
            );
        }
        else
        {
            payload = NormalizeEffectPayload(context, dto, kind, index, diagnostics);
        }

        ValidateNonNegative(minSkillLevel, context, $"{pointer}/min_skill_level", diagnostics);
        if (maxSkillLevel < -1)
            AddRangeDiagnostic(context, $"{pointer}/max_skill_level", diagnostics);
        ValidateNonNegative(power, context, $"{pointer}/power", diagnostics);
        ValidateNonNegative(durationTu, context, $"{pointer}/duration_tu", diagnostics);

        return diagnostics.Count == startingErrorCount && payload != null
            ? new CombatEffectImportModel(
                kind,
                minSkillLevel,
                maxSkillLevel,
                power,
                durationTu,
                payload
            )
            : null;
    }

    private static ICombatEffectPayloadImportModel? NormalizeEffectPayload(
        JsonContentEntryContext context,
        CombatEffectJsonDto dto,
        CombatEffectImportKind kind,
        int effectIndex,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        string payloadPointer = $"/combat_profile/effect_defs/{effectIndex}/payload";
        if (dto.Payload is not JsonElement payload)
        {
            diagnostics.Add(
                Diagnostic(
                    SkillJsonImportRules.InvalidEffectPayload,
                    "Registered combat effect payload must be a JSON value consumed by the parser.",
                    context,
                    payloadPointer
                )
            );
            return null;
        }

        return kind switch
        {
            CombatEffectImportKind.LayeredBarrier => NormalizeLayeredBarrierPayload(
                context,
                payload,
                payloadPointer,
                diagnostics
            ),
            _ => throw new InvalidOperationException(
                $"Combat effect kind '{kind}' has no registered typed payload parser."
            ),
        };
    }

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

            if (
                combat.TryGetProperty("effect_defs", out JsonElement effects)
                && effects.ValueKind == JsonValueKind.Array
            )
            {
                string[] effectRequired =
                {
                    "effect_type",
                };
                int index = 0;
                foreach (JsonElement effect in effects.EnumerateArray())
                {
                    if (effect.ValueKind == JsonValueKind.Object)
                    {
                        missing = FindMissing(
                            effect,
                            effectRequired,
                            context,
                            $"/combat_profile/effect_defs/{index}"
                        );
                        if (missing != null)
                            return missing;
                        if (
                            effect.TryGetProperty("effect_type", out JsonElement effectType)
                            && effectType.ValueKind == JsonValueKind.String
                            && SkillJsonImportValueRules.TryParseEffectKind(
                                effectType.GetString(),
                                out _
                            )
                            && (
                                !effect.TryGetProperty("payload", out JsonElement payload)
                                || payload.ValueKind == JsonValueKind.Null
                            )
                        )
                        {
                            return Diagnostic(
                                SkillJsonImportRules.MissingEffectPayload,
                                "Registered combat effect kind requires a typed payload object.",
                                context,
                                $"/combat_profile/effect_defs/{index}/payload"
                            );
                        }
                        missing = FindExplicitNull(
                            effect,
                            new[]
                            {
                                "min_skill_level", "max_skill_level", "power", "duration_tu",
                            },
                            context,
                            $"/combat_profile/effect_defs/{index}"
                        );
                        if (missing != null)
                            return missing;
                    }
                    index += 1;
                }
            }

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
            return null;
        }

        return null;
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
