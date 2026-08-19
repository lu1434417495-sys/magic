using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

internal sealed record ValidationDomainResult(string Domain, string Label, IReadOnlyList<string> Errors)
{
    public int ErrorCount => Errors?.Count ?? 0;
}

internal sealed class ValidationRunReport
{
    public string Label { get; init; } = "";
    public bool Ok { get; init; }
    public int ErrorCount { get; init; }
    public IReadOnlyList<ValidationDomainResult> Domains { get; init; } =
        Array.Empty<ValidationDomainResult>();
}

internal sealed record QuestValidationEntry(string Source, QuestDefinition QuestDefinition);

internal static class ContentValidationRunner
{
    public static ValidationRunReport BuildRunReport(
        string label,
        IEnumerable<ValidationDomainResult> domainResults
    )
    {
        List<ValidationDomainResult> normalized = new();
        int errorCount = 0;
        bool ok = true;
        if (domainResults != null)
        {
            foreach (ValidationDomainResult domainResult in domainResults)
            {
                if (domainResult == null)
                    continue;
                normalized.Add(domainResult);
                errorCount += domainResult.ErrorCount;
                if (domainResult.ErrorCount > 0)
                    ok = false;
            }
        }

        return new ValidationRunReport
        {
            Label = label ?? "validation",
            Ok = ok,
            ErrorCount = errorCount,
            Domains = normalized,
        };
    }

    public static string FormatReport(ValidationRunReport report)
    {
        string label = report?.Label ?? "validation";
        List<string> lines =
        [
            $"Validation report: {label} | {(report?.Ok == true ? "PASS" : "FAIL")} | errors={report?.ErrorCount ?? 0}",
        ];
        if (report?.Domains != null)
        {
            foreach (ValidationDomainResult domainResult in report.Domains)
            {
                if (domainResult == null)
                    continue;
                lines.Add(
                    $"[{domainResult.Domain}] source={domainResult.Label} errors={domainResult.ErrorCount}"
                );
                foreach (string error in domainResult.Errors)
                    lines.Add($"  - {error}");
            }
        }
        return string.Join("\n", lines);
    }

    public static ValidationDomainResult ValidateSkillDirectory(
        string directoryPath,
        bool includeProgressionSkillChecks = false
    )
    {
        using SkillContentRegistry registry = new(
            new TestContentResourceLoader(),
            loadDefaultContent: false
        );
        registry.LoadFromDirectory(directoryPath);
        List<string> errors = ToStringList(registry.Validate());
        if (includeProgressionSkillChecks)
        {
            using ProgressionContentRegistry progressionRegistry = new(
                new TestContentResourceLoader(),
                loadDefaultContent: false
            );
            try
            {
                progressionRegistry.ReplaceDefinitionsForValidation(
                    new ProgressionDefinitionSources
                    {
                        SkillDefinitions = registry.GetSkillDefinitionsTyped(),
                    }
                );
                AppendUniqueErrors(errors, progressionRegistry.CollectValidationErrors());
            }
            catch (System.IO.InvalidDataException exception)
            {
                AppendUniqueErrors(
                    errors,
                    new[] { $"Skill projection rejected content: {exception.Message}" }
                );
            }
        }
        return BuildDomainResult("skill", directoryPath, errors);
    }

    // Negative schema fixtures remain Godot Resources so they can construct states that the
    // fail-closed JSON parser rejects before normalization. This test-only boundary must not be
    // used by SkillContentRegistry or any production content-loading path.
    public static ValidationDomainResult ValidateSkillResourceFixtureDirectory(
        string directoryPath,
        bool includeProgressionSkillChecks = false
    )
    {
        var imports = new Dictionary<StringName, SkillImportModel>();
        var errors = new List<string>();
        using DirAccess directory = DirAccess.Open(directoryPath);
        if (directory == null)
            return BuildDomainResult("skill", directoryPath, [$"Cannot open skill fixture directory: {directoryPath}"]);

        var fileNames = new List<string>();
        directory.ListDirBegin();
        for (string entryName = directory.GetNext(); entryName != ""; entryName = directory.GetNext())
        {
            if (!directory.CurrentIsDir() && entryName.EndsWith(".tres", StringComparison.Ordinal))
                fileNames.Add(entryName);
        }
        directory.ListDirEnd();
        fileNames.Sort(StringComparer.Ordinal);

        foreach (string fileName in fileNames)
        {
            string sourcePath = $"{directoryPath.TrimEnd('/')}/{fileName}";
            using SkillDef skill = ResourceLoader.Load<SkillDef>(sourcePath, cacheMode: ResourceLoader.CacheMode.IgnoreDeep);
            if (skill == null)
            {
                errors.Add($"skill.tres.invalid_resource {sourcePath}: Skill Resource could not be loaded.");
                continue;
            }
            var context = new JsonContentEntryContext("skill", "", sourcePath, "");
            ContentImportStageResult<SkillImportModel> result = SkillResourceProjectionAdapter.TryAdapt(context, skill);
            foreach (ContentJsonDiagnostic diagnostic in result.Diagnostics)
                errors.Add($"{diagnostic.RuleId} {diagnostic.SourceLabel}{diagnostic.JsonPointer}: {diagnostic.Message}");
            if (!result.HasValue)
                continue;
            StringName skillId = result.Value.SkillId.Value;
            if (!imports.TryAdd(skillId, result.Value))
                errors.Add($"Duplicate skill_id registered: {skillId}");
        }

        var validator = new SkillImportModelValidator();
        AppendUniqueErrors(errors, validator.ValidateBatchMessages(imports));
        if (includeProgressionSkillChecks)
        {
            using ProgressionContentRegistry progressionRegistry = new(
                new TestContentResourceLoader(),
                loadDefaultContent: false
            );
            progressionRegistry.ReplaceDefinitionsForValidation(
                new ProgressionDefinitionSources
                {
                    SkillDefinitions = SkillDefinitionProjector.ProjectIndex(imports.Values),
                }
            );
            AppendUniqueErrors(errors, progressionRegistry.CollectValidationErrors());
        }
        return BuildDomainResult("skill", directoryPath, errors);
    }

    public static ValidationDomainResult ValidateProfessionDirectory(
        string directoryPath,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions
    )
    {
        using ProfessionContentRegistry registry = new(
            new TestContentResourceLoader(),
            loadDefaultContent: false
        );
        registry.Setup(skillDefinitions, directoryPath);
        return BuildDomainResult("profession", directoryPath, registry.Validate());
    }

    public static ValidationDomainResult ValidateIdentityContent(
        string label,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions = null
    )
    {
        return ValidateIdentityDirectories(
            label,
            ["res://data/configs/races"],
            ["res://data/configs/subraces"],
            ["res://data/configs/age_profiles"],
            ["res://data/configs/bloodlines"],
            ["res://data/configs/ascensions"],
            ["res://data/configs/stage_advancements"],
            skillDefinitions ?? new Dictionary<StringName, SkillDefinition>()
        );
    }

    public static ValidationDomainResult ValidateIdentityDirectories(
        string label,
        string[] raceDirectories,
        string[] subraceDirectories,
        string[] ageProfileDirectories,
        string[] bloodlineDirectories,
        string[] ascensionDirectories,
        string[] stageAdvancementDirectories,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions = null
    )
    {
        using RaceContentRegistry raceRegistry = BuildRaceRegistry(raceDirectories);
        using SubraceContentRegistry subraceRegistry = BuildSubraceRegistry(subraceDirectories);
        using TraitContentRegistry traitRegistry = BuildTraitRegistry();
        using AgeContentRegistry ageRegistry = BuildAgeRegistry(ageProfileDirectories);
        using BloodlineContentRegistry bloodlineRegistry =
            BuildBloodlineRegistry(bloodlineDirectories);
        using AscensionContentRegistry ascensionRegistry =
            BuildAscensionRegistry(ascensionDirectories);
        using StageAdvancementContentRegistry stageAdvancementRegistry =
            BuildStageAdvancementRegistry(stageAdvancementDirectories);

        List<string> errors = new();
        AppendUniqueErrors(errors, raceRegistry.Validate());
        AppendUniqueErrors(errors, subraceRegistry.Validate());
        AppendUniqueErrors(errors, traitRegistry.Validate());
        AppendUniqueErrors(errors, ageRegistry.Validate());
        AppendUniqueErrors(errors, bloodlineRegistry.Validate());
        AppendUniqueErrors(errors, ascensionRegistry.Validate());
        AppendUniqueErrors(errors, stageAdvancementRegistry.Validate());

        using ProgressionContentRegistry progressionRegistry = new(
            new TestContentResourceLoader(),
            loadDefaultContent: false
        );
        PrepareIdentityPhase2Registry(
            progressionRegistry,
            skillDefinitions ?? new Dictionary<StringName, SkillDefinition>(),
            raceRegistry,
            subraceRegistry,
            traitRegistry,
            ageRegistry,
            bloodlineRegistry,
            ascensionRegistry,
            stageAdvancementRegistry
        );
        GStringArray phase2Errors = new();
        progressionRegistry.AppendIdentityPhase2ValidationErrors(phase2Errors);
        AppendUniqueErrors(errors, phase2Errors);
        return BuildDomainResult("identity", label, errors);
    }

    public static ValidationDomainResult ValidateOfficialItemContent()
    {
        using TraitContentRegistry traitRegistry = new();
        using ItemContentRegistry registry = new();
        registry.Rebuild();
        List<string> combinedErrors = ToStringList(registry.Validate());
        AppendUniqueErrors(
            combinedErrors,
            ItemTraitContentValidator.Validate(
                registry.GetItemDefsTyped(),
                traitRegistry.GetTraitDefsTyped(),
                "official_items"
            )
        );
        return BuildDomainResult("item", "official_items", combinedErrors);
    }

    public static ValidationDomainResult ValidateRecipeDirectory(
        string directoryPath,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions
    )
    {
        var sourceReader = new GodotContentJsonSourceReader();
        ContentImportBatch<RecipeImportModel> batch =
            RecipeContentJsonAuthoringDomain.CreateImportDescriptor(
                directoryPath,
                sourceReader
            ).Import();
        var errors = new List<string>();
        foreach (ContentJsonDiagnostic diagnostic in batch.Diagnostics)
        {
            errors.Add(
                $"{diagnostic.RuleId} {diagnostic.SourceLabel}{diagnostic.JsonPointer}: {diagnostic.Message}"
            );
        }
        IReadOnlyList<ContentImportEntry<RecipeImportModel>> referenceEntries =
            batch.HasErrors
                ? ImportRecipeEntriesPerDocument(directoryPath, sourceReader)
                : batch.Entries;
        foreach (ContentImportEntry<RecipeImportModel> entry in referenceEntries)
        {
            foreach (RecipeIngredientImportModel input in entry.Import.Inputs)
            {
                var inputId = new StringName(input.ItemId);
                if (itemDefinitions != null && !itemDefinitions.ContainsKey(inputId))
                {
                    errors.Add(
                        $"Recipe {entry.Import.RecipeId} references missing input item {inputId}."
                    );
                }
            }
            var outputItemId = new StringName(entry.Import.OutputItemId);
            if (itemDefinitions != null && !itemDefinitions.ContainsKey(outputItemId))
            {
                errors.Add(
                    $"Recipe {entry.Import.RecipeId} references missing output item {outputItemId}."
                );
            }
        }
        return BuildDomainResult("recipe", directoryPath, errors);
    }

    private static IReadOnlyList<ContentImportEntry<RecipeImportModel>>
        ImportRecipeEntriesPerDocument(
            string directoryPath,
            IContentJsonSourceReader sourceReader
        )
    {
        var entries = new List<ContentImportEntry<RecipeImportModel>>();
        foreach (ContentJsonSourceText source in sourceReader.ReadUtf8Documents(directoryPath))
        {
            ContentImportBatch<RecipeImportModel> documentBatch =
                RecipeContentJsonAuthoringDomain.CreateImportDescriptor(
                    directoryPath,
                    new SingleContentJsonSourceReader(source)
                ).Import();
            entries.AddRange(documentBatch.Entries);
        }
        return entries;
    }

    private sealed class SingleContentJsonSourceReader : IContentJsonSourceReader
    {
        private readonly ContentJsonSourceText _source;

        internal SingleContentJsonSourceReader(ContentJsonSourceText source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }

        public IReadOnlyList<ContentJsonSourceText> ReadUtf8Documents(
            string directoryPath
        ) => new[] { _source };
    }

    public static ValidationDomainResult ValidateEnemySeed(
        string seedResourcePath,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions = null,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions = null
    )
    {
        using TestContentResourceLoader loader = new();
        using EnemyContentRegistry registry = new(loader, loadDefaultContent: false);
        registry.ConfigureSeedResource(seedResourcePath, rebuildNow: false, validateSeedDirCompleteness: false);
        RebuildEnemyRegistry(registry, itemDefinitions, skillDefinitions);
        return BuildDomainResult("enemy", seedResourcePath, registry.Validate());
    }

    public static ValidationDomainResult ValidateEnemySeedWithDirectoryCompleteness(
        string seedResourcePath,
        string templateDirectory,
        string brainDirectory,
        string rosterDirectory,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions = null,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions = null
    )
    {
        using TestContentResourceLoader loader = new();
        using EnemyContentRegistry registry = new(loader, loadDefaultContent: false);
        registry.ConfigureDirectories(templateDirectory, brainDirectory, rosterDirectory, false);
        registry.ConfigureSeedResource(seedResourcePath, rebuildNow: false, validateSeedDirCompleteness: true);
        RebuildEnemyRegistry(registry, itemDefinitions, skillDefinitions);
        return BuildDomainResult("enemy", seedResourcePath, registry.Validate());
    }

    private static void RebuildEnemyRegistry(
        EnemyContentRegistry registry,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions
    )
    {
        if (itemDefinitions != null && skillDefinitions != null)
        {
            registry.Rebuild(
                new EnemyContentValidationContext(itemDefinitions, skillDefinitions)
            );
            return;
        }
        registry.Rebuild();
    }

    public static ValidationDomainResult ValidateBattleSpecialProfileRegistry(
        string label,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        string manifestDirectory = ""
    )
    {
        using TestContentResourceLoader loader = new();
        using BattleSpecialProfileRegistry registry = new(loader);
        if (!string.IsNullOrEmpty(manifestDirectory))
            registry.SetManifestDirectory(manifestDirectory);
        registry.Rebuild(skillDefinitions);
        return BuildDomainResult("battle_special_profile", label, registry.Validate());
    }

    public static ValidationDomainResult ValidateWorldPresets(
        IEnumerable<StringName> battleEncounterIds = null
    )
    {
        using TestContentResourceLoader loader = new();
        WorldMapContentValidator validator = new();
        var errors = new List<string>();
        foreach (WorldPresetRegistry.WorldPresetInfo preset in WorldPresetRegistry.ListPresetsTyped())
        {
            string resourcePath = preset?.GenerationConfigPath ?? "";
            try
            {
                string canonicalPath = ContentPathCanonicalizer.Canonicalize(resourcePath);
                WorldMapGenerationConfig source = loader.LoadCanonical<WorldMapGenerationConfig>(
                    canonicalPath
                );
                WorldGenerationDefinition definition = source.ToDefinition(canonicalPath, loader);
                AppendUniqueErrors(
                    errors,
                    validator.ValidateGenerationConfigTyped(
                        definition,
                        canonicalPath,
                        battleEncounterIds
                    )
                );
            }
            catch (Exception exception)
            {
                AppendUniqueErrors(
                    errors,
                    new[]
                    {
                        $"World preset {resourcePath} projection failed: {exception.Message}",
                    }
                );
            }
        }
        return BuildDomainResult(
            "world",
            "world_presets",
            errors
        );
    }

    public static ValidationDomainResult ValidateWorldGenerationConfig(
        string label,
        WorldGenerationDefinition generationDefinition,
        IEnumerable<StringName> battleEncounterIds = null
    )
    {
        WorldMapContentValidator validator = new();
        return BuildDomainResult(
            "world",
            label,
            validator.ValidateGenerationConfigTyped(
                generationDefinition,
                label,
                battleEncounterIds
            )
        );
    }

    public static ValidationDomainResult ValidateQuestEntries(
        string label,
        IReadOnlyList<QuestValidationEntry> questEntries,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        IReadOnlyDictionary<StringName, EnemyTemplateDefinition> enemyTemplates
    )
    {
        List<string> errors = new();
        Dictionary<StringName, QuestDefinition> questDefs = new();
        HashSet<StringName> seenQuestIds = new();
        if (questEntries != null)
        {
            foreach (QuestValidationEntry entry in questEntries)
            {
                string sourceLabel = string.IsNullOrEmpty(entry.Source) ? label : entry.Source;
                QuestDefinition questDef = entry.QuestDefinition;
                if (questDef == null)
                {
                    errors.Add($"Quest entry {sourceLabel} failed to cast to QuestDefinition.");
                    continue;
                }
                if (questDef.QuestId == "")
                {
                    errors.Add($"Quest entry {sourceLabel} is missing quest_id.");
                    continue;
                }
                if (!seenQuestIds.Add(questDef.QuestId))
                {
                    errors.Add($"Duplicate quest_id registered: {questDef.QuestId}");
                    continue;
                }
                questDefs[questDef.QuestId] = questDef;
            }
        }

        errors.AddRange(
            QuestContentValidator.ValidateTyped(
                questDefs,
                itemDefinitions ?? new Dictionary<StringName, ItemDefinition>(),
                skillDefinitions ?? new Dictionary<StringName, SkillDefinition>(),
                enemyTemplates ?? new Dictionary<StringName, EnemyTemplateDefinition>(),
                Array.Empty<string>()
            )
        );
        return BuildDomainResult("quest", label, errors);
    }

    private static RaceContentRegistry BuildRaceRegistry(string[] directoryPaths)
    {
        RaceContentRegistry registry = new(
            new TestContentResourceLoader(),
            loadDefaultContent: false
        );
        registry.LoadFromDirectories(ToGodotStringArray(directoryPaths));
        return registry;
    }

    private static SubraceContentRegistry BuildSubraceRegistry(string[] directoryPaths)
    {
        SubraceContentRegistry registry = new(
            new TestContentResourceLoader(),
            loadDefaultContent: false
        );
        registry.LoadFromDirectories(ToGodotStringArray(directoryPaths));
        return registry;
    }

    private static TraitContentRegistry BuildTraitRegistry() => new();

    private static AgeContentRegistry BuildAgeRegistry(string[] directoryPaths)
    {
        AgeContentRegistry registry = new(
            new TestContentResourceLoader(),
            loadDefaultContent: false
        );
        registry.LoadFromDirectories(ToGodotStringArray(directoryPaths));
        return registry;
    }

    private static BloodlineContentRegistry BuildBloodlineRegistry(string[] directoryPaths)
    {
        BloodlineContentRegistry registry = new(
            new TestContentResourceLoader(),
            loadDefaultContent: false
        );
        registry.LoadFromDirectories(ToGodotStringArray(directoryPaths));
        return registry;
    }

    private static AscensionContentRegistry BuildAscensionRegistry(string[] directoryPaths)
    {
        AscensionContentRegistry registry = new(
            new TestContentResourceLoader(),
            loadDefaultContent: false
        );
        registry.LoadFromDirectories(ToGodotStringArray(directoryPaths));
        return registry;
    }

    private static StageAdvancementContentRegistry BuildStageAdvancementRegistry(
        string[] directoryPaths
    )
    {
        StageAdvancementContentRegistry registry = new(
            new TestContentResourceLoader(),
            loadDefaultContent: false
        );
        registry.LoadFromDirectories(ToGodotStringArray(directoryPaths));
        return registry;
    }

    private static void PrepareIdentityPhase2Registry(
        ProgressionContentRegistry progressionRegistry,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        RaceContentRegistry raceRegistry,
        SubraceContentRegistry subraceRegistry,
        TraitContentRegistry traitRegistry,
        AgeContentRegistry ageRegistry,
        BloodlineContentRegistry bloodlineRegistry,
        AscensionContentRegistry ascensionRegistry,
        StageAdvancementContentRegistry stageAdvancementRegistry
    )
    {
        progressionRegistry.ReplaceDefinitionsForValidation(
            new ProgressionDefinitionSources
            {
                SkillDefinitions = skillDefinitions,
                RaceDefinitions = raceRegistry.GetRaceDefsTyped(),
                SubraceDefinitions = subraceRegistry.GetSubraceDefsTyped(),
                TraitDefinitions = traitRegistry.GetTraitDefsTyped(),
                AgeProfileDefinitions = ageRegistry.GetAgeProfileDefsTyped(),
                BloodlineDefinitions = bloodlineRegistry.GetBloodlineDefsTyped(),
                BloodlineStageDefinitions = bloodlineRegistry.GetBloodlineStageDefsTyped(),
                AscensionDefinitions = ascensionRegistry.GetAscensionDefsTyped(),
                AscensionStageDefinitions = ascensionRegistry.GetAscensionStageDefsTyped(),
                StageAdvancementDefinitions =
                    stageAdvancementRegistry.GetStageAdvancementDefsTyped(),
            }
        );
    }

    private static List<string> ValidateSkillBookItems(
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions,
        GDictionary skillDefs
    )
    {
        List<string> errors = new();
        itemDefinitions ??= new Dictionary<StringName, ItemDefinition>();
        var sortedItemIds = new List<StringName>(itemDefinitions.Keys);
        sortedItemIds.Sort(
            (left, right) => string.CompareOrdinal(left.ToString(), right.ToString())
        );
        foreach (StringName itemId in sortedItemIds)
        {
            if (
                !itemDefinitions.TryGetValue(itemId, out ItemDefinition itemDefinition)
                || itemDefinition == null
            )
                continue;
            if (
                string.CompareOrdinal(
                    itemDefinition.GetItemCategoryNormalized(),
                    "skill_book"
                ) != 0
            )
                continue;
            if (itemDefinition.GrantedSkillId == "")
                continue;
            SkillDef skillDef = DictGetByStringName<SkillDef>(
                skillDefs,
                itemDefinition.GrantedSkillId.ToString()
            );
            if (skillDef == null)
            {
                errors.Add(
                    $"Skill book item {itemDefinition.ItemId} references missing skill {itemDefinition.GrantedSkillId}."
                );
                continue;
            }
            if (skillDef.LearnSourceKind != SkillLearnSourceKind.Book)
            {
                errors.Add(
                    $"Skill book item {itemDefinition.ItemId} granted_skill_id {itemDefinition.GrantedSkillId} learn_source must be book, got {skillDef.learn_source}."
                );
            }
        }
        foreach (string skillKey in SortedStringKeys(skillDefs))
        {
            SkillDef skillDef = DictGetByStringName<SkillDef>(skillDefs, skillKey);
            if (
                skillDef == null
                || skillDef.skill_id == ""
                || skillDef.LearnSourceKind != SkillLearnSourceKind.Book
            )
                continue;
            StringName canonicalItemId = BuildSkillBookItemId(skillDef.skill_id);
            itemDefinitions.TryGetValue(
                canonicalItemId,
                out ItemDefinition occupyingItem
            );
            if (occupyingItem == null)
                continue;
            if (string.CompareOrdinal(occupyingItem.GetItemCategoryNormalized(), "skill_book") != 0)
            {
                errors.Add(
                    $"Item {canonicalItemId} occupies generated skill book id for skill {skillDef.skill_id} but item_category must be skill_book."
                );
                continue;
            }
            if (occupyingItem.GrantedSkillId != skillDef.skill_id)
            {
                errors.Add(
                    $"Skill book item {canonicalItemId} occupies generated skill book id for skill {skillDef.skill_id} but grants {occupyingItem.GrantedSkillId}."
                );
            }
        }
        return errors;
    }

    private static GDictionary ProjectSkillDefs(IReadOnlyDictionary<StringName, SkillDef> skillDefs)
    {
        GDictionary result = new();
        if (skillDefs == null)
            return result;
        foreach ((StringName skillId, SkillDef skillDef) in skillDefs)
        {
            if (skillId == "" || skillDef == null)
                continue;
            result[skillId] = skillDef;
        }
        return result;
    }

    private static T DictGetByStringName<T>(GDictionary source, string key)
        where T : GodotObject
    {
        StringName stringNameKey = key;
        if (source != null && source.ContainsKey(stringNameKey))
            return source[stringNameKey].AsGodotObject() as T;
        return null;
    }

    private static StringName BuildSkillBookItemId(StringName skillId) => $"skill_book_{skillId}";

    private static List<string> SortedStringKeys(GDictionary source)
    {
        List<string> keys = new();
        if (source == null)
            return keys;
        foreach (Variant key in source.Keys)
            keys.Add(key.ToString());
        keys.Sort(StringComparer.Ordinal);
        return keys;
    }

    private static ValidationDomainResult BuildDomainResult(
        string domain,
        string label,
        IEnumerable<string> errorMessages
    )
    {
        return new ValidationDomainResult(domain, label, ToStringList(errorMessages));
    }

    private static List<string> ToStringList(IEnumerable<string> values)
    {
        List<string> result = new();
        if (values == null)
            return result;
        foreach (string value in values)
            result.Add(value ?? "");
        return result;
    }

    private static Godot.Collections.Array<string> ToGodotStringArray(IEnumerable<string> values)
    {
        Godot.Collections.Array<string> result = new();
        if (values == null)
            return result;
        foreach (string value in values)
            result.Add(value ?? "");
        return result;
    }

    private static void AppendUniqueErrors(List<string> errors, IEnumerable<string> additionalErrors)
    {
        if (errors == null || additionalErrors == null)
            return;
        foreach (string errorMessage in additionalErrors)
        {
            if (!errors.Contains(errorMessage))
                errors.Add(errorMessage);
        }
    }
}
