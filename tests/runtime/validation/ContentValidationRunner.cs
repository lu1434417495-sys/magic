using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
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
                progressionRegistry.ReplaceSkillAuthoringResourcesForValidation(
                    registry.DuplicateSkillResourceBucketForProgressionRegistry()
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

    public static ValidationDomainResult ValidateProfessionDirectory(
        string directoryPath,
        GDictionary skillDefs
    )
    {
        using ProfessionContentRegistry registry = new(
            new TestContentResourceLoader(),
            loadDefaultContent: false
        );
        registry.Setup(ProjectSkillDefinitions(skillDefs), directoryPath);
        return BuildDomainResult("profession", directoryPath, registry.Validate());
    }

    public static ValidationDomainResult ValidateIdentityContent(
        string label,
        GDictionary skillDefs = null
    )
    {
        return ValidateIdentityDirectories(
            label,
            ["res://data/configs/races"],
            ["res://data/configs/subraces"],
            ["res://data/configs/traits"],
            ["res://data/configs/age_profiles"],
            ["res://data/configs/bloodlines"],
            ["res://data/configs/ascensions"],
            ["res://data/configs/stage_advancements"],
            skillDefs ?? new GDictionary()
        );
    }

    public static ValidationDomainResult ValidateIdentityDirectories(
        string label,
        string[] raceDirectories,
        string[] subraceDirectories,
        string[] traitDirectories,
        string[] ageProfileDirectories,
        string[] bloodlineDirectories,
        string[] ascensionDirectories,
        string[] stageAdvancementDirectories,
        GDictionary skillDefs = null
    )
    {
        using RaceContentRegistry raceRegistry = BuildRaceRegistry(raceDirectories);
        using SubraceContentRegistry subraceRegistry = BuildSubraceRegistry(subraceDirectories);
        using TraitContentRegistry traitRegistry = BuildTraitRegistry(traitDirectories);
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
            skillDefs ?? new GDictionary(),
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
        using TraitContentRegistry traitRegistry = new(new TestContentResourceLoader());
        return ValidateItemDirectories(
            "official_items",
            ["res://data/configs/items"],
            ["res://data/configs/items_templates"],
            traitDefinitions: traitRegistry.GetTraitDefsTyped()
        );
    }

    public static ValidationDomainResult ValidateItemDirectories(
        string label,
        string[] itemDirectories,
        string[] templateDirectories = null,
        GDictionary skillDefs = null,
        IReadOnlyDictionary<StringName, TraitDefinition> traitDefinitions = null
    )
    {
        using TestContentResourceLoader loader = new();
        using ItemContentRegistry registry = new(loader);
        registry.RebuildFromDirectories(
            ToGodotArray(itemDirectories),
            ToGodotArray(templateDirectories ?? Array.Empty<string>())
        );
        List<string> combinedErrors = ToStringList(registry.Validate());
        if (skillDefs != null && skillDefs.Count > 0)
            AppendUniqueErrors(
                combinedErrors,
                ValidateSkillBookItems(registry.GetItemDefsTyped(), skillDefs)
            );
        if (traitDefinitions != null && traitDefinitions.Count > 0)
        {
            AppendUniqueErrors(
                combinedErrors,
                ItemTraitContentValidator.Validate(
                    registry.GetItemDefsTyped(),
                    traitDefinitions,
                    label
                )
            );
        }
        return BuildDomainResult("item", label, combinedErrors);
    }

    public static ValidationDomainResult ValidateRecipeDirectory(
        string directoryPath,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions
    )
    {
        using TestContentResourceLoader loader = new();
        using RecipeContentRegistry registry = new(loader);
        registry.Setup(itemDefinitions);
        registry.LoadFromDirectory(directoryPath);
        return BuildDomainResult("recipe", directoryPath, registry.Validate());
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
        IEnumerable<StringName> enemyTemplateIds = null,
        IEnumerable<StringName> wildEncounterRosterIds = null
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
                        enemyTemplateIds,
                        wildEncounterRosterIds
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
        IEnumerable<StringName> enemyTemplateIds = null,
        IEnumerable<StringName> wildEncounterRosterIds = null
    )
    {
        WorldMapContentValidator validator = new();
        return BuildDomainResult(
            "world",
            label,
            validator.ValidateGenerationConfigTyped(
                generationDefinition,
                label,
                enemyTemplateIds,
                wildEncounterRosterIds
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

    private static TraitContentRegistry BuildTraitRegistry(string[] directoryPaths)
    {
        TraitContentRegistry registry = new(
            new TestContentResourceLoader(),
            loadDefaultContent: false
        );
        registry.LoadFromDirectories(ToGodotStringArray(directoryPaths));
        return registry;
    }

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
        GDictionary skillDefs,
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
                SkillDefinitions = ProjectSkillDefinitions(skillDefs),
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

    private static IReadOnlyDictionary<StringName, SkillDefinition> ProjectSkillDefinitions(
        GDictionary skillDefs
    )
    {
        var resources = new Dictionary<StringName, SkillDef>();
        if (skillDefs == null)
            return SkillDefinition.ProjectIndex(resources);
        foreach (Variant rawKey in skillDefs.Keys)
        {
            StringName skillId = ProgressionDataUtils.to_string_name(rawKey);
            if (skillId == "")
                continue;
            if (skillDefs[rawKey].AsGodotObject() is SkillDef skillDef)
                resources[skillId] = skillDef;
        }
        return SkillDefinition.ProjectIndex(resources);
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

    private static GArray ToGodotArray(IEnumerable<string> values)
    {
        GArray result = new();
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
