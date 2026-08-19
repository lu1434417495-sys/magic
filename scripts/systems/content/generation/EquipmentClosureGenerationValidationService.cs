#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Godot;

internal static class EquipmentClosureGenerationCrossDomainRules
{
    internal const string ExistingIdCollision =
        "equipment_closure.cross_domain.existing_id_collision";
    internal const string MissingTextureAsset =
        "equipment_closure.cross_domain.item_texture_asset_missing";
    internal const string MissingItemTrait =
        "equipment_closure.cross_domain.item_trait_missing";
    internal const string MissingItemSkill =
        "equipment_closure.cross_domain.item_skill_missing";
    internal const string MissingItemProfession =
        "equipment_closure.cross_domain.item_profession_missing";
    internal const string ItemTraitContract =
        "equipment_closure.cross_domain.item_trait_contract";
    internal const string SkillBookContract =
        "equipment_closure.cross_domain.skill_book_contract";
    internal const string EquipmentAbilityRegistry =
        "equipment_closure.cross_domain.equipment_ability_registry";
    internal const string MissingGearSetItem =
        "equipment_closure.cross_domain.gear_set_item_missing";
    internal const string MissingGearSetTrait =
        "equipment_closure.cross_domain.gear_set_trait_missing";
    internal const string GearSetContract =
        "equipment_closure.cross_domain.gear_set_contract";
    internal const string MissingRecipeItem =
        "equipment_closure.cross_domain.recipe_item_missing";
}

internal sealed class EquipmentClosureGenerationValidationService
{
    private readonly ContentSnapshot _processSnapshot;
    private readonly IEquipmentClosureGenerationBattleSimulationGate _battleSimulationGate;
    private readonly IReadOnlySet<StringName> _textureAssetIds;
    private readonly bool _allowExistingIdReplacement;

    internal EquipmentClosureGenerationValidationService(
        ContentSnapshot processSnapshot,
        IEquipmentClosureGenerationBattleSimulationGate battleSimulationGate,
        IReadOnlySet<StringName> textureAssetIds,
        bool allowExistingIdReplacement = false
    )
    {
        _processSnapshot = processSnapshot
            ?? throw new ArgumentNullException(nameof(processSnapshot));
        _battleSimulationGate = battleSimulationGate
            ?? throw new ArgumentNullException(nameof(battleSimulationGate));
        _textureAssetIds = new HashSet<StringName>(
            textureAssetIds ?? throw new ArgumentNullException(nameof(textureAssetIds))
        );
        _allowExistingIdReplacement = allowExistingIdReplacement;
    }

    internal EquipmentClosureGenerationValidationReport Validate(
        EquipmentClosureGenerationSourceSet sources,
        IContentJsonSourceReader sourceReader
    )
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(sourceReader);
        sources.Validate();

        var reports = new List<EquipmentClosureGenerationValidationStageReport>();
        EquipmentClosureImportSet imports = ImportSchema(sources, sourceReader);
        IReadOnlyList<ContentJsonDiagnostic> schemaDiagnostics = imports.Diagnostics;
        reports.Add(new EquipmentClosureGenerationValidationStageReport(
            EquipmentClosureGenerationValidationStageKind.Schema,
            imports.EntryCount,
            schemaDiagnostics,
            CountMetrics(imports)
        ));
        if (schemaDiagnostics.Count > 0)
            return new EquipmentClosureGenerationValidationReport(reports);

        IReadOnlyList<ContentJsonDiagnostic> domainDiagnostics =
            ValidateDomainLocal(imports);
        reports.Add(new EquipmentClosureGenerationValidationStageReport(
            EquipmentClosureGenerationValidationStageKind.Domain,
            imports.EntryCount,
            domainDiagnostics,
            CountMetrics(imports)
        ));
        if (domainDiagnostics.Count > 0)
            return new EquipmentClosureGenerationValidationReport(reports);

        EquipmentClosureProjectionResult projection = ProjectAndValidateCrossDomain(imports);
        reports.Add(new EquipmentClosureGenerationValidationStageReport(
            EquipmentClosureGenerationValidationStageKind.CrossDomain,
            imports.EntryCount,
            projection.Diagnostics,
            CountMetrics(imports)
        ));
        if (projection.Diagnostics.Count > 0 || projection.Content == null)
            return new EquipmentClosureGenerationValidationReport(reports);

        EquipmentClosureGenerationBattleSimulationGateResult simulation =
            _battleSimulationGate.Evaluate(projection.Content, _processSnapshot)
            ?? throw new InvalidOperationException(
                "Equipment closure BattleSim gate returned a null result."
            );
        reports.Add(new EquipmentClosureGenerationValidationStageReport(
            EquipmentClosureGenerationValidationStageKind.BattleSimulation,
            simulation.SampledItemCount,
            simulation.Diagnostics,
            simulation.Metrics
        ));
        return new EquipmentClosureGenerationValidationReport(reports);
    }

    private static EquipmentClosureImportSet ImportSchema(
        EquipmentClosureGenerationSourceSet sources,
        IContentJsonSourceReader sourceReader
    ) => new(
        ItemContentJsonAuthoringDomain
            .CreateSchemaImportDescriptor(sources.ItemsDirectory, sourceReader)
            .Import(),
        TraitContentJsonAuthoringDomain
            .CreateSchemaImportDescriptor(sources.TraitsDirectory, sourceReader)
            .Import(),
        EquipmentAbilityContentJsonAuthoringDomain
            .CreateSchemaImportDescriptor(
                sources.EquipmentAbilitiesDirectory,
                sourceReader
            )
            .Import(),
        GearSetContentJsonAuthoringDomain
            .CreateSchemaImportDescriptor(sources.GearSetsDirectory, sourceReader)
            .Import(),
        RecipeContentJsonAuthoringDomain
            .CreateSchemaImportDescriptor(sources.RecipesDirectory, sourceReader)
            .Import()
    );

    private static IReadOnlyList<ContentJsonDiagnostic> ValidateDomainLocal(
        EquipmentClosureImportSet imports
    )
    {
        var diagnostics = new List<ContentJsonDiagnostic>();
        var itemValidator = new ItemImportModelValidator();
        foreach (ContentImportEntry<ItemImportModel> entry in imports.Items.Entries)
            diagnostics.AddRange(
                itemValidator.ValidateDomainLocal(entry.Context, entry.Import)
            );

        var traitValidator = new TraitImportModelValidator();
        foreach (ContentImportEntry<TraitImportModel> entry in imports.Traits.Entries)
            diagnostics.AddRange(
                traitValidator.ValidateDomainLocal(entry.Context, entry.Import)
            );

        foreach (
            ContentImportEntry<EquipmentAbilityContentPackImportModel> entry
            in imports.EquipmentAbilities.Entries
        )
        {
            EquipmentAbilityImportVocabularyValidator.Validate(
                entry.Context,
                entry.Import,
                diagnostics
            );
        }

        foreach (ContentImportEntry<GearSetImportModel> entry in imports.GearSets.Entries)
            diagnostics.AddRange(
                GearSetContentJsonAuthoringDomain.ValidateDomainLocal(
                    entry.Context,
                    entry.Import
                )
            );
        foreach (ContentImportEntry<RecipeImportModel> entry in imports.Recipes.Entries)
            diagnostics.AddRange(
                RecipeContentJsonAuthoringDomain.ValidateDomainLocal(
                    entry.Context,
                    entry.Import
                )
            );
        return new ReadOnlyCollection<ContentJsonDiagnostic>(diagnostics);
    }

    private EquipmentClosureProjectionResult ProjectAndValidateCrossDomain(
        EquipmentClosureImportSet imports
    )
    {
        var diagnostics = new List<ContentJsonDiagnostic>();
        Dictionary<StringName, JsonContentEntryContext> itemContexts = Contexts(
            imports.Items.Entries,
            value => value.ItemId
        );
        Dictionary<StringName, JsonContentEntryContext> traitContexts = Contexts(
            imports.Traits.Entries,
            value => value.TraitId
        );
        Dictionary<StringName, JsonContentEntryContext> packContexts = Contexts(
            imports.EquipmentAbilities.Entries,
            value => value.pack_id
        );
        Dictionary<StringName, JsonContentEntryContext> gearSetContexts = Contexts(
            imports.GearSets.Entries,
            value => value.GearSetId
        );
        Dictionary<StringName, JsonContentEntryContext> recipeContexts = Contexts(
            imports.Recipes.Entries,
            value => value.RecipeId
        );

        var candidateItems = Project(
            imports.Items.Entries,
            value => new StringName(value.ItemId),
            ItemDefinitionProjector.Project
        );
        var candidateTraits = Project(
            imports.Traits.Entries,
            value => new StringName(value.TraitId),
            TraitDefinitionProjector.Project
        );
        var candidateGearSets = Project(
            imports.GearSets.Entries,
            value => new StringName(value.GearSetId),
            GearSetDefinitionProjector.Project
        );
        var candidateRecipes = Project(
            imports.Recipes.Entries,
            value => new StringName(value.RecipeId),
            RecipeDefinitionProjector.Project
        );

        ValidateCollisions(
            itemContexts,
            _processSnapshot.Items,
            "/item_id",
            diagnostics
        );
        ValidateCollisions(
            traitContexts,
            _processSnapshot.Traits,
            "/trait_id",
            diagnostics
        );
        ValidateCollisions(
            packContexts,
            _processSnapshot.EquipmentAbilityPacks,
            "/pack_id",
            diagnostics
        );
        ValidateCollisions(
            gearSetContexts,
            _processSnapshot.GearSets,
            "/gear_set_id",
            diagnostics
        );
        ValidateCollisions(
            recipeContexts,
            _processSnapshot.Recipes,
            "/recipe_id",
            diagnostics
        );

        IReadOnlyDictionary<StringName, ItemDefinition> combinedItems = Combine(
            _processSnapshot.Items,
            candidateItems
        );
        IReadOnlyDictionary<StringName, TraitDefinition> combinedTraits = Combine(
            _processSnapshot.Traits,
            candidateTraits
        );
        ValidateItemReferences(imports.Items.Entries, combinedTraits, diagnostics);
        AppendOwnedContractDiagnostics(
            WithoutErrorsContaining(
                ItemTraitContentValidator.Validate(
                    candidateItems,
                    combinedTraits,
                    "generated_items"
                ),
                "references missing trait"
            ),
            EquipmentClosureGenerationCrossDomainRules.ItemTraitContract,
            "generated_items.",
            itemContexts,
            diagnostics
        );
        AppendOwnedContractDiagnostics(
            WithoutErrorsContaining(
                SkillBookItemContentValidator.Validate(
                    candidateItems,
                    _processSnapshot.Skills
                ),
                "references missing skill"
            ),
            EquipmentClosureGenerationCrossDomainRules.SkillBookContract,
            "item ",
            itemContexts,
            diagnostics
        );

        List<EquipmentAbilityContentPackImportModel> abilityImports =
            imports.EquipmentAbilities.Entries.Select(value => value.Import).ToList();
        using var abilityRegistry = new EquipmentAbilityContentRegistry();
        var abilityContext = new EquipmentAbilityContentValidationContext
        {
            KnownTraitIds = new HashSet<StringName>(combinedTraits.Keys),
            KnownSkillIds = new HashSet<StringName>(_processSnapshot.Skills.Keys),
            WindupSkillIds = new HashSet<StringName>(
                _processSnapshot.Skills.Values
                    .Where(value => value?.CombatProfile?.Windup != null)
                    .Select(value => value.SkillId)
            ),
            KnownStatusIds =
                EquipmentAbilityStatusDeclarationCatalog.CollectExternalStatusDeclarations(
                    combinedTraits.Values,
                    _processSnapshot.Skills.Values
                ),
        };
        EquipmentAbilityRegistryBuildResult abilityBuild = abilityRegistry.Rebuild(
            abilityImports,
            abilityContext
        );
        if (!abilityBuild.Success)
            AppendEquipmentAbilityDiagnostics(
                abilityBuild.Errors,
                imports.EquipmentAbilities.Entries,
                diagnostics
            );

        IReadOnlyDictionary<StringName, EquipmentAbilityContentPackDefinition>
            candidatePacks = abilityBuild.Success
                ? abilityRegistry.GetPackDefinitionsTyped()
                : Empty<StringName, EquipmentAbilityContentPackDefinition>();
        IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition>
            candidateBindings = abilityBuild.Success
                ? abilityRegistry.GetBindingDefinitionsTyped()
                : Empty<StringName, EquipmentAbilityBindingDefinition>();
        ValidateBindingCollisions(
            imports.EquipmentAbilities.Entries,
            candidateBindings,
            diagnostics
        );

        IReadOnlyDictionary<StringName, EquipmentAbilityContentPackDefinition>
            combinedPacks = Combine(_processSnapshot.EquipmentAbilityPacks, candidatePacks);
        IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition>
            combinedBindings = Combine(
                _processSnapshot.EquipmentAbilityBindings,
                candidateBindings
            );

        AppendOwnedContractDiagnostics(
            WithoutErrorsContaining(
                GearSetContentRegistry.ValidateDefinitionsTyped(
                    candidateGearSets,
                    combinedItems,
                    combinedTraits,
                    combinedBindings
                ),
                "references missing item",
                "references missing trait"
            ),
            EquipmentClosureGenerationCrossDomainRules.GearSetContract,
            "Gear set ",
            gearSetContexts,
            diagnostics
        );
        ValidateGearSetReferences(
            imports.GearSets.Entries,
            combinedItems,
            combinedTraits,
            diagnostics
        );
        ValidateRecipeReferences(imports.Recipes.Entries, combinedItems, diagnostics);

        if (diagnostics.Count > 0)
            return new EquipmentClosureProjectionResult(null, diagnostics);

        var content = new EquipmentClosureGenerationProjectedContent
        {
            CandidateItems = candidateItems,
            CandidateTraits = candidateTraits,
            CandidateEquipmentAbilityPacks = candidatePacks,
            CandidateEquipmentAbilityBindings = candidateBindings,
            CandidateGearSets = candidateGearSets,
            CandidateRecipes = candidateRecipes,
            CombinedItems = combinedItems,
            CombinedTraits = combinedTraits,
            CombinedEquipmentAbilityPacks = combinedPacks,
            CombinedEquipmentAbilityBindings = combinedBindings,
            CombinedGearSets = Combine(_processSnapshot.GearSets, candidateGearSets),
            CombinedRecipes = Combine(_processSnapshot.Recipes, candidateRecipes),
            ItemContexts = new ReadOnlyDictionary<StringName, JsonContentEntryContext>(itemContexts),
            TraitContexts = new ReadOnlyDictionary<StringName, JsonContentEntryContext>(traitContexts),
            EquipmentAbilityPackContexts =
                new ReadOnlyDictionary<StringName, JsonContentEntryContext>(packContexts),
            GearSetContexts = new ReadOnlyDictionary<StringName, JsonContentEntryContext>(gearSetContexts),
            RecipeContexts = new ReadOnlyDictionary<StringName, JsonContentEntryContext>(recipeContexts),
        };
        return new EquipmentClosureProjectionResult(content, diagnostics);
    }

    private void ValidateCollisions<T>(
        IReadOnlyDictionary<StringName, JsonContentEntryContext> contexts,
        IReadOnlyDictionary<StringName, T> existing,
        string relativePointer,
        ICollection<ContentJsonDiagnostic> diagnostics
    )
    {
        if (_allowExistingIdReplacement)
            return;
        foreach ((StringName id, JsonContentEntryContext context) in contexts)
        {
            if (!existing.ContainsKey(id))
                continue;
            diagnostics.Add(Diagnostic(
                EquipmentClosureGenerationCrossDomainRules.ExistingIdCollision,
                $"Generated {context.DomainId} ID already exists in the process catalog.",
                context,
                relativePointer,
                "a new stable ID",
                id.ToString()
            ));
        }
    }

    private void ValidateItemReferences(
        IReadOnlyList<ContentImportEntry<ItemImportModel>> entries,
        IReadOnlyDictionary<StringName, TraitDefinition> traits,
        ICollection<ContentJsonDiagnostic> diagnostics
    )
    {
        foreach (ContentImportEntry<ItemImportModel> entry in entries)
        {
            ItemImportModel item = entry.Import;
            if (
                item.IconAssetId.Length > 0
                && !_textureAssetIds.Contains(new StringName(item.IconAssetId))
            )
            {
                diagnostics.Add(Diagnostic(
                    EquipmentClosureGenerationCrossDomainRules.MissingTextureAsset,
                    "Item icon_asset_id is not a published texture asset ID.",
                    entry.Context,
                    "/icon_asset_id",
                    "a published Texture2D asset ID",
                    item.IconAssetId
                ));
            }
            for (int index = 0; index < item.TraitIds.Count; index += 1)
                ValidateItemTrait(
                    item.TraitIds[index],
                    entry.Context,
                    $"/trait_ids/{index}",
                    traits,
                    diagnostics
                );
            for (int groupIndex = 0; groupIndex < item.TraitRollGroups.Count; groupIndex += 1)
            {
                ItemTraitRollGroupImportModel group = item.TraitRollGroups[groupIndex];
                for (int entryIndex = 0; entryIndex < group.Entries.Count; entryIndex += 1)
                {
                    ValidateItemTrait(
                        group.Entries[entryIndex].TraitId,
                        entry.Context,
                        $"/trait_roll_groups/{groupIndex}/entries/{entryIndex}/trait_id",
                        traits,
                        diagnostics
                    );
                }
            }
            if (
                item.GrantedSkillId.Length > 0
                && !_processSnapshot.Skills.ContainsKey(new StringName(item.GrantedSkillId))
            )
            {
                diagnostics.Add(Diagnostic(
                    EquipmentClosureGenerationCrossDomainRules.MissingItemSkill,
                    "Item granted_skill_id is not present in the process skill catalog.",
                    entry.Context,
                    "/granted_skill_id",
                    "an existing skill ID",
                    item.GrantedSkillId
                ));
            }
            IReadOnlyList<string> professions =
                item.EquipRequirement?.RequiredProfessionIds ?? Array.Empty<string>();
            for (int index = 0; index < professions.Count; index += 1)
            {
                string professionId = professions[index];
                if (_processSnapshot.Professions.ContainsKey(new StringName(professionId)))
                    continue;
                diagnostics.Add(Diagnostic(
                    EquipmentClosureGenerationCrossDomainRules.MissingItemProfession,
                    "Item equip requirement references a missing profession.",
                    entry.Context,
                    $"/equip_requirement/required_profession_ids/{index}",
                    "an existing profession ID",
                    professionId
                ));
            }
        }
    }

    private static void ValidateItemTrait(
        string traitId,
        JsonContentEntryContext context,
        string pointer,
        IReadOnlyDictionary<StringName, TraitDefinition> traits,
        ICollection<ContentJsonDiagnostic> diagnostics
    )
    {
        if (traits.ContainsKey(new StringName(traitId)))
            return;
        diagnostics.Add(Diagnostic(
            EquipmentClosureGenerationCrossDomainRules.MissingItemTrait,
            "Item references a missing trait.",
            context,
            pointer,
            "an existing or same-closure trait ID",
            traitId
        ));
    }

    private void ValidateBindingCollisions(
        IReadOnlyList<ContentImportEntry<EquipmentAbilityContentPackImportModel>> entries,
        IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> candidateBindings,
        ICollection<ContentJsonDiagnostic> diagnostics
    )
    {
        if (_allowExistingIdReplacement)
            return;
        foreach (ContentImportEntry<EquipmentAbilityContentPackImportModel> entry in entries)
        {
            for (int index = 0; index < entry.Import.bindings.Count; index += 1)
            {
                string bindingId = entry.Import.bindings[index].binding_id;
                if (
                    candidateBindings.ContainsKey(new StringName(bindingId))
                    && _processSnapshot.EquipmentAbilityBindings.ContainsKey(
                        new StringName(bindingId)
                    )
                )
                {
                    diagnostics.Add(Diagnostic(
                        EquipmentClosureGenerationCrossDomainRules.ExistingIdCollision,
                        "Generated equipment ability binding ID already exists.",
                        entry.Context,
                        $"/bindings/{index}/binding_id",
                        "a new stable binding ID",
                        bindingId
                    ));
                }
            }
        }
    }

    private static void ValidateGearSetReferences(
        IReadOnlyList<ContentImportEntry<GearSetImportModel>> entries,
        IReadOnlyDictionary<StringName, ItemDefinition> items,
        IReadOnlyDictionary<StringName, TraitDefinition> traits,
        ICollection<ContentJsonDiagnostic> diagnostics
    )
    {
        foreach (ContentImportEntry<GearSetImportModel> entry in entries)
        {
            GearSetImportModel gearSet = entry.Import;
            for (int index = 0; index < gearSet.MemberItemIds.Count; index += 1)
                ValidateGearSetItem(
                    gearSet.MemberItemIds[index],
                    entry.Context,
                    $"/member_item_ids/{index}",
                    items,
                    diagnostics
                );
            ValidateGearSetItem(
                gearSet.UsageAnchorItemId,
                entry.Context,
                "/usage_anchor_item_id",
                items,
                diagnostics
            );
            for (int thresholdIndex = 0; thresholdIndex < gearSet.Thresholds.Count; thresholdIndex += 1)
            {
                GearSetThresholdImportModel threshold = gearSet.Thresholds[thresholdIndex];
                for (int itemIndex = 0; itemIndex < threshold.MandatoryMemberItemIds.Count; itemIndex += 1)
                    ValidateGearSetItem(
                        threshold.MandatoryMemberItemIds[itemIndex],
                        entry.Context,
                        $"/thresholds/{thresholdIndex}/mandatory_member_item_ids/{itemIndex}",
                        items,
                        diagnostics
                    );
                for (int traitIndex = 0; traitIndex < threshold.GrantedTraitIds.Count; traitIndex += 1)
                {
                    string traitId = threshold.GrantedTraitIds[traitIndex];
                    string pointer = $"/thresholds/{thresholdIndex}/granted_trait_ids/{traitIndex}";
                    if (!traits.TryGetValue(new StringName(traitId), out TraitDefinition? trait))
                    {
                        diagnostics.Add(Diagnostic(
                            EquipmentClosureGenerationCrossDomainRules.MissingGearSetTrait,
                            "Gear-set threshold references a missing trait.",
                            entry.Context,
                            pointer,
                            "an existing or same-closure trait ID",
                            traitId
                        ));
                    }
                }
            }
        }
    }

    private static void ValidateGearSetItem(
        string itemId,
        JsonContentEntryContext context,
        string pointer,
        IReadOnlyDictionary<StringName, ItemDefinition> items,
        ICollection<ContentJsonDiagnostic> diagnostics
    )
    {
        if (items.ContainsKey(new StringName(itemId)))
            return;
        diagnostics.Add(Diagnostic(
            EquipmentClosureGenerationCrossDomainRules.MissingGearSetItem,
            "Gear set references a missing item.",
            context,
            pointer,
            "an existing or same-closure item ID",
            itemId
        ));
    }

    private static void ValidateRecipeReferences(
        IReadOnlyList<ContentImportEntry<RecipeImportModel>> entries,
        IReadOnlyDictionary<StringName, ItemDefinition> items,
        ICollection<ContentJsonDiagnostic> diagnostics
    )
    {
        foreach (ContentImportEntry<RecipeImportModel> entry in entries)
        {
            for (int index = 0; index < entry.Import.Inputs.Count; index += 1)
            {
                ValidateRecipeItem(
                    entry.Import.Inputs[index].ItemId,
                    entry.Context,
                    $"/inputs/{index}/item_id",
                    items,
                    diagnostics
                );
            }
            ValidateRecipeItem(
                entry.Import.OutputItemId,
                entry.Context,
                "/output_item_id",
                items,
                diagnostics
            );
        }
    }

    private static void ValidateRecipeItem(
        string itemId,
        JsonContentEntryContext context,
        string pointer,
        IReadOnlyDictionary<StringName, ItemDefinition> items,
        ICollection<ContentJsonDiagnostic> diagnostics
    )
    {
        if (items.ContainsKey(new StringName(itemId)))
            return;
        diagnostics.Add(Diagnostic(
            EquipmentClosureGenerationCrossDomainRules.MissingRecipeItem,
            "Recipe references a missing item.",
            context,
            pointer,
            "an existing or same-closure item ID",
            itemId
        ));
    }

    private static void AppendEquipmentAbilityDiagnostics(
        IReadOnlyList<string> errors,
        IReadOnlyList<ContentImportEntry<EquipmentAbilityContentPackImportModel>> entries,
        ICollection<ContentJsonDiagnostic> diagnostics
    )
    {
        foreach (string error in errors)
        {
            string stable = error ?? "";
            int firstSpace = stable.IndexOf(' ');
            string code = firstSpace > 0 ? stable[..firstSpace] : "EQA_UNKNOWN";
            JsonContentEntryContext context = ResolveEquipmentContext(stable, entries);
            diagnostics.Add(new ContentJsonDiagnostic(
                $"{EquipmentClosureGenerationCrossDomainRules.EquipmentAbilityRegistry}.{code.ToLowerInvariant()}",
                stable,
                context.SourceLabel,
                ResolveEquipmentPointer(stable, context, entries)
            ));
        }
    }

    private static void AppendOwnedContractDiagnostics(
        IReadOnlyList<string> errors,
        string ruleId,
        string ownerPrefix,
        IReadOnlyDictionary<StringName, JsonContentEntryContext> contexts,
        ICollection<ContentJsonDiagnostic> diagnostics
    )
    {
        foreach (string rawError in errors)
        {
            string error = rawError ?? "";
            (JsonContentEntryContext context, string ownerLabel) =
                ResolveOwnedContractContext(error, ownerPrefix, contexts);
            diagnostics.Add(new ContentJsonDiagnostic(
                ruleId,
                error,
                context.SourceLabel,
                context.JsonPointer + ResolveOwnedContractPointer(error, ownerLabel),
                "the production snapshot business contract",
                error
            ));
        }
    }

    private static IReadOnlyList<string> WithoutErrorsContaining(
        IReadOnlyList<string> errors,
        params string[] fragments
    ) => errors
        .Where(error => !fragments.Any(fragment =>
            (error ?? "").Contains(fragment, StringComparison.Ordinal)
        ))
        .ToList();

    private static (JsonContentEntryContext Context, string OwnerLabel)
        ResolveOwnedContractContext(
            string error,
            string ownerPrefix,
            IReadOnlyDictionary<StringName, JsonContentEntryContext> contexts
        )
    {
        foreach (
            (StringName id, JsonContentEntryContext context) in contexts
                .OrderByDescending(pair => pair.Key.ToString().Length)
                .ThenBy(pair => pair.Key.ToString(), StringComparer.Ordinal)
        )
        {
            string idText = id.ToString();
            string[] labels = ownerPrefix == "item "
                ? new[] { $"Skill book item {idText}", $"Item {idText}" }
                : new[] { ownerPrefix + idText };
            foreach (string label in labels)
            {
                if (error.StartsWith(label, StringComparison.Ordinal))
                    return (context, label);
            }
        }
        JsonContentEntryContext fallback = contexts.Count > 0
            ? contexts.OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal).First().Value
            : new JsonContentEntryContext(
                "equipment_closure",
                "<batch>",
                "equipment_closure#<batch>",
                ""
            );
        return (fallback, "");
    }

    private static string ResolveOwnedContractPointer(
        string error,
        string ownerLabel
    )
    {
        if (ownerLabel.Length == 0 || !error.StartsWith(ownerLabel, StringComparison.Ordinal))
            return "";
        string suffix = error[ownerLabel.Length..];
        int space = suffix.IndexOf(' ');
        if (space >= 0)
            suffix = suffix[..space];
        if (!suffix.StartsWith(".", StringComparison.Ordinal))
            return "";
        return suffix
            .Replace(".", "/", StringComparison.Ordinal)
            .Replace("[", "/", StringComparison.Ordinal)
            .Replace("]", "", StringComparison.Ordinal)
            .TrimEnd('.', ':');
    }

    private static JsonContentEntryContext ResolveEquipmentContext(
        string error,
        IReadOnlyList<ContentImportEntry<EquipmentAbilityContentPackImportModel>> entries
    )
    {
        foreach (ContentImportEntry<EquipmentAbilityContentPackImportModel> entry in entries)
        {
            if (error.Contains($"packs[{entry.Import.pack_id}]", StringComparison.Ordinal))
                return entry.Context;
            foreach (EquipmentAbilityBindingImportModel binding in entry.Import.bindings)
            {
                if (
                    binding != null
                    && error.Contains($"bindings[{binding.binding_id}]", StringComparison.Ordinal)
                )
                {
                    return entry.Context;
                }
            }
        }
        return entries.Count > 0
            ? entries[0].Context
            : new JsonContentEntryContext(
                EquipmentAbilityContentJsonAuthoringDomain.DomainId,
                "<batch>",
                "equipment_abilities#<batch>",
                ""
            );
    }

    private static string ResolveEquipmentPointer(
        string error,
        JsonContentEntryContext context,
        IReadOnlyList<ContentImportEntry<EquipmentAbilityContentPackImportModel>> entries
    )
    {
        foreach (ContentImportEntry<EquipmentAbilityContentPackImportModel> entry in entries)
        {
            if (entry.Context != context)
                continue;
            for (int index = 0; index < entry.Import.bindings.Count; index += 1)
            {
                EquipmentAbilityBindingImportModel binding = entry.Import.bindings[index];
                if (
                    binding != null
                    && error.Contains($"bindings[{binding.binding_id}]", StringComparison.Ordinal)
                )
                {
                    return context.JsonPointer + $"/bindings/{index}";
                }
            }
        }
        return context.JsonPointer;
    }

    private static ContentJsonDiagnostic Diagnostic(
        string ruleId,
        string message,
        JsonContentEntryContext context,
        string relativePointer,
        string expected,
        string actual
    ) => new(
        ruleId,
        message,
        context.SourceLabel,
        context.JsonPointer + relativePointer,
        expected,
        actual
    );

    private static Dictionary<StringName, JsonContentEntryContext> Contexts<T>(
        IReadOnlyList<ContentImportEntry<T>> entries,
        Func<T, string> key
    )
        where T : notnull
    {
        var result = new Dictionary<StringName, JsonContentEntryContext>();
        foreach (ContentImportEntry<T> entry in entries)
            result.Add(new StringName(key(entry.Import)), entry.Context);
        return result;
    }

    private static IReadOnlyDictionary<StringName, TDefinition> Project<TImport, TDefinition>(
        IReadOnlyList<ContentImportEntry<TImport>> entries,
        Func<TImport, StringName> key,
        Func<TImport, TDefinition> projector
    )
        where TImport : notnull
        where TDefinition : class
    {
        var result = new Dictionary<StringName, TDefinition>();
        foreach (ContentImportEntry<TImport> entry in entries)
            result.Add(key(entry.Import), projector(entry.Import));
        return new ReadOnlyDictionary<StringName, TDefinition>(result);
    }

    private static IReadOnlyDictionary<StringName, T> Combine<T>(
        IReadOnlyDictionary<StringName, T> existing,
        IReadOnlyDictionary<StringName, T> candidates
    )
    {
        var result = new Dictionary<StringName, T>(existing);
        foreach ((StringName key, T value) in candidates)
            result[key] = value;
        return new ReadOnlyDictionary<StringName, T>(result);
    }

    private static IReadOnlyDictionary<TKey, TValue> Empty<TKey, TValue>()
        where TKey : notnull => new ReadOnlyDictionary<TKey, TValue>(
            new Dictionary<TKey, TValue>()
        );

    private static IReadOnlyDictionary<string, object> CountMetrics(
        EquipmentClosureImportSet imports
    ) => new ReadOnlyDictionary<string, object>(new Dictionary<string, object>
    {
        ["item_count"] = imports.Items.Entries.Count,
        ["trait_count"] = imports.Traits.Entries.Count,
        ["equipment_ability_pack_count"] = imports.EquipmentAbilities.Entries.Count,
        ["gear_set_count"] = imports.GearSets.Entries.Count,
        ["recipe_count"] = imports.Recipes.Entries.Count,
    });

    private sealed record EquipmentClosureProjectionResult(
        EquipmentClosureGenerationProjectedContent? Content,
        IReadOnlyList<ContentJsonDiagnostic> Diagnostics
    )
    {
        internal EquipmentClosureProjectionResult(
            EquipmentClosureGenerationProjectedContent? content,
            IEnumerable<ContentJsonDiagnostic> diagnostics
        ) : this(
            content,
            new ReadOnlyCollection<ContentJsonDiagnostic>(diagnostics.ToList())
        ) { }
    }

    private sealed class EquipmentClosureImportSet
    {
        internal EquipmentClosureImportSet(
            ContentImportBatch<ItemImportModel> items,
            ContentImportBatch<TraitImportModel> traits,
            ContentImportBatch<EquipmentAbilityContentPackImportModel> equipmentAbilities,
            ContentImportBatch<GearSetImportModel> gearSets,
            ContentImportBatch<RecipeImportModel> recipes
        )
        {
            Items = items;
            Traits = traits;
            EquipmentAbilities = equipmentAbilities;
            GearSets = gearSets;
            Recipes = recipes;
            Diagnostics = new ReadOnlyCollection<ContentJsonDiagnostic>(
                items.Diagnostics
                    .Concat(traits.Diagnostics)
                    .Concat(equipmentAbilities.Diagnostics)
                    .Concat(gearSets.Diagnostics)
                    .Concat(recipes.Diagnostics)
                    .ToList()
            );
        }

        internal ContentImportBatch<ItemImportModel> Items { get; }
        internal ContentImportBatch<TraitImportModel> Traits { get; }
        internal ContentImportBatch<EquipmentAbilityContentPackImportModel>
            EquipmentAbilities { get; }
        internal ContentImportBatch<GearSetImportModel> GearSets { get; }
        internal ContentImportBatch<RecipeImportModel> Recipes { get; }
        internal IReadOnlyList<ContentJsonDiagnostic> Diagnostics { get; }
        internal int EntryCount => Items.Entries.Count + Traits.Entries.Count
            + EquipmentAbilities.Entries.Count + GearSets.Entries.Count
            + Recipes.Entries.Count;
    }
}
