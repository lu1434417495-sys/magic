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

internal sealed record QuestValidationEntry(string Source, QuestDef QuestDef);

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
        using SkillContentRegistry registry = new();
        registry.LoadFromDirectory(directoryPath);
        List<string> errors = ToStringList(registry.Validate());
        if (includeProgressionSkillChecks)
        {
            using ProgressionContentRegistry progressionRegistry = new();
            progressionRegistry.ReplaceValidationSources(
                new GDictionary { ["skill_defs"] = ProjectSkillDefs(registry.GetSkillDefsTyped()) }
            );
            AppendUniqueErrors(errors, progressionRegistry.CollectValidationErrors());
        }
        return BuildDomainResult("skill", directoryPath, errors);
    }

    public static ValidationDomainResult ValidateProfessionDirectory(
        string directoryPath,
        GDictionary skillDefs
    )
    {
        using ProfessionContentRegistry registry = new();
        registry.Setup(skillDefs);
        registry.LoadFromDirectory(directoryPath);
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

        using ProgressionContentRegistry progressionRegistry = new();
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
        using TraitContentRegistry traitRegistry = new();
        return ValidateItemDirectories(
            "official_items",
            ["res://data/configs/items"],
            ["res://data/configs/items_templates"],
            traitDefs: ProjectTraitDefs(traitRegistry.GetTraitDefsTyped())
        );
    }

    public static ValidationDomainResult ValidateItemDirectories(
        string label,
        string[] itemDirectories,
        string[] templateDirectories = null,
        GDictionary skillDefs = null,
        GDictionary traitDefs = null
    )
    {
        using ItemContentRegistry registry = new();
        registry.RebuildFromDirectories(
            ToGodotArray(itemDirectories),
            ToGodotArray(templateDirectories ?? Array.Empty<string>())
        );
        List<string> combinedErrors = ToStringList(registry.Validate());
        if (skillDefs != null && skillDefs.Count > 0)
            AppendUniqueErrors(
                combinedErrors,
                ValidateSkillBookItems(ProjectItemDefs(registry.GetItemDefsTyped()), skillDefs)
            );
        if (traitDefs != null && traitDefs.Count > 0)
        {
            AppendUniqueErrors(
                combinedErrors,
                ItemTraitContentValidator.Validate(
                    registry.GetItemDefsTyped(),
                    BuildTraitDefIndex(traitDefs),
                    label
                )
            );
        }
        return BuildDomainResult("item", label, combinedErrors);
    }

    public static ValidationDomainResult ValidateRecipeDirectory(
        string directoryPath,
        GDictionary itemDefs
    )
    {
        using RecipeContentRegistry registry = new();
        registry.Setup(BuildItemDefIndex(itemDefs));
        registry.LoadFromDirectory(directoryPath);
        return BuildDomainResult("recipe", directoryPath, registry.Validate());
    }

    public static ValidationDomainResult ValidateEnemySeed(string seedResourcePath)
    {
        using EnemyContentRegistry registry = new();
        registry.ConfigureSeedResource(seedResourcePath, true, false);
        return BuildDomainResult("enemy", seedResourcePath, registry.Validate());
    }

    public static ValidationDomainResult ValidateEnemySeedWithDirectoryCompleteness(
        string seedResourcePath,
        string templateDirectory,
        string brainDirectory,
        string rosterDirectory
    )
    {
        using EnemyContentRegistry registry = new();
        registry.ConfigureDirectories(templateDirectory, brainDirectory, rosterDirectory, false);
        registry.ConfigureSeedResource(seedResourcePath, true, true);
        return BuildDomainResult("enemy", seedResourcePath, registry.Validate());
    }

    public static ValidationDomainResult ValidateBattleSpecialProfileRegistry(
        string label,
        IReadOnlyDictionary<StringName, SkillDef> skillDefs,
        string manifestDirectory = ""
    )
    {
        using BattleSpecialProfileRegistry registry = new();
        if (!string.IsNullOrEmpty(manifestDirectory))
            registry.SetManifestDirectory(manifestDirectory);
        registry.Rebuild(skillDefs);
        return BuildDomainResult("battle_special_profile", label, registry.Validate());
    }

    public static ValidationDomainResult ValidateWorldPresets(
        GDictionary enemyTemplates = null,
        GDictionary wildEncounterRosters = null
    )
    {
        WorldMapContentValidator validator = new();
        return BuildDomainResult(
            "world",
            "world_presets",
            validator.ValidateWorldPresets(
                enemyTemplates ?? new GDictionary(),
                wildEncounterRosters ?? new GDictionary()
            )
        );
    }

    public static ValidationDomainResult ValidateWorldGenerationConfig(
        string label,
        WorldMapGenerationConfig generationConfig,
        GDictionary enemyTemplates = null,
        GDictionary wildEncounterRosters = null
    )
    {
        WorldMapContentValidator validator = new();
        return BuildDomainResult(
            "world",
            label,
            validator.ValidateGenerationConfig(
                generationConfig,
                label,
                enemyTemplates ?? new GDictionary(),
                wildEncounterRosters ?? new GDictionary()
            )
        );
    }

    public static ValidationDomainResult ValidateQuestEntries(
        string label,
        IReadOnlyList<QuestValidationEntry> questEntries,
        IReadOnlyDictionary<StringName, ItemDef> itemDefs,
        IReadOnlyDictionary<StringName, SkillDef> skillDefs,
        IReadOnlyDictionary<StringName, EnemyTemplateDef> enemyTemplates
    )
    {
        List<string> errors = new();
        Dictionary<StringName, QuestDef> questDefs = new();
        HashSet<StringName> seenQuestIds = new();
        if (questEntries != null)
        {
            foreach (QuestValidationEntry entry in questEntries)
            {
                string sourceLabel = string.IsNullOrEmpty(entry.Source) ? label : entry.Source;
                QuestDef questDef = entry.QuestDef;
                if (questDef == null)
                {
                    errors.Add($"Quest entry {sourceLabel} failed to cast to QuestDef.");
                    continue;
                }
                if (questDef.quest_id == "")
                {
                    errors.Add($"Quest entry {sourceLabel} is missing quest_id.");
                    continue;
                }
                if (!seenQuestIds.Add(questDef.quest_id))
                {
                    errors.Add($"Duplicate quest_id registered: {questDef.quest_id}");
                    continue;
                }
                questDefs[questDef.quest_id] = questDef;
            }
        }

        errors.AddRange(
            QuestContentValidator.ValidateTyped(
                questDefs,
                itemDefs ?? new Dictionary<StringName, ItemDef>(),
                skillDefs ?? new Dictionary<StringName, SkillDef>(),
                enemyTemplates ?? new Dictionary<StringName, EnemyTemplateDef>(),
                Array.Empty<string>()
            )
        );
        return BuildDomainResult("quest", label, errors);
    }

    private static RaceContentRegistry BuildRaceRegistry(string[] directoryPaths)
    {
        RaceContentRegistry registry = new();
        registry.LoadFromDirectories(ToGodotStringArray(directoryPaths));
        return registry;
    }

    private static SubraceContentRegistry BuildSubraceRegistry(string[] directoryPaths)
    {
        SubraceContentRegistry registry = new();
        registry.LoadFromDirectories(ToGodotStringArray(directoryPaths));
        return registry;
    }

    private static TraitContentRegistry BuildTraitRegistry(string[] directoryPaths)
    {
        TraitContentRegistry registry = new();
        registry.LoadFromDirectories(ToGodotStringArray(directoryPaths));
        return registry;
    }

    private static AgeContentRegistry BuildAgeRegistry(string[] directoryPaths)
    {
        AgeContentRegistry registry = new();
        registry.LoadFromDirectories(ToGodotStringArray(directoryPaths));
        return registry;
    }

    private static BloodlineContentRegistry BuildBloodlineRegistry(string[] directoryPaths)
    {
        BloodlineContentRegistry registry = new();
        registry.LoadFromDirectories(ToGodotStringArray(directoryPaths));
        return registry;
    }

    private static AscensionContentRegistry BuildAscensionRegistry(string[] directoryPaths)
    {
        AscensionContentRegistry registry = new();
        registry.LoadFromDirectories(ToGodotStringArray(directoryPaths));
        return registry;
    }

    private static StageAdvancementContentRegistry BuildStageAdvancementRegistry(
        string[] directoryPaths
    )
    {
        StageAdvancementContentRegistry registry = new();
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
            progressionRegistry.ReplaceValidationSources(
            new GDictionary
            {
                ["skill_defs"] = skillDefs?.Duplicate() ?? new GDictionary(),
                ["race_defs"] = ProjectDefs(raceRegistry.GetRaceDefsTyped()),
                ["subrace_defs"] = ProjectDefs(subraceRegistry.GetSubraceDefsTyped()),
                ["trait_defs"] = ProjectTraitDefs(traitRegistry.GetTraitDefsTyped()),
                ["age_profile_defs"] = ProjectDefs(ageRegistry.GetAgeProfileDefsTyped()),
                ["bloodline_defs"] = ProjectDefs(bloodlineRegistry.GetBloodlineDefsTyped()),
                ["bloodline_stage_defs"] = ProjectDefs(
                    bloodlineRegistry.GetBloodlineStageDefsTyped()
                ),
                ["ascension_defs"] = ProjectDefs(ascensionRegistry.GetAscensionDefsTyped()),
                ["ascension_stage_defs"] = ProjectDefs(
                    ascensionRegistry.GetAscensionStageDefsTyped()
                ),
                ["stage_advancement_defs"] = ProjectDefs(
                    stageAdvancementRegistry.GetStageAdvancementDefsTyped()
                ),
            }
        );
    }

    private static GDictionary ProjectDefs<T>(IReadOnlyDictionary<StringName, T> defs)
        where T : GodotObject
    {
        GDictionary result = new();
        if (defs == null)
            return result;
        foreach ((StringName id, T def) in defs)
        {
            if (id == "" || def == null)
                continue;
            result[id] = def;
        }
        return result;
    }

    private static GDictionary ProjectTraitDefs(IReadOnlyList<TraitDef> defs)
    {
        GDictionary result = new();
        if (defs == null)
            return result;
        foreach (TraitDef def in defs)
        {
            if (def == null || def.trait_id == "")
                continue;
            result[def.trait_id] = def;
        }
        return result;
    }

    private static List<string> ValidateSkillBookItems(GDictionary itemDefs, GDictionary skillDefs)
    {
        List<string> errors = new();
        foreach (string itemKey in SortedStringKeys(itemDefs))
        {
            ItemDef itemDef = DictGetByStringName<ItemDef>(itemDefs, itemKey);
            if (itemDef == null)
                continue;
            if (string.CompareOrdinal(itemDef.GetItemCategoryNormalized(), "skill_book") != 0)
                continue;
            if (itemDef.granted_skill_id == "")
                continue;
            SkillDef skillDef = DictGetByStringName<SkillDef>(
                skillDefs,
                itemDef.granted_skill_id.ToString()
            );
            if (skillDef == null)
            {
                errors.Add(
                    $"Skill book item {itemDef.item_id} references missing skill {itemDef.granted_skill_id}."
                );
                continue;
            }
            if (skillDef.LearnSourceKind != SkillLearnSourceKind.Book)
            {
                errors.Add(
                    $"Skill book item {itemDef.item_id} granted_skill_id {itemDef.granted_skill_id} learn_source must be book, got {skillDef.learn_source}."
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
            ItemDef occupyingItem = DictGetByStringName<ItemDef>(
                itemDefs,
                canonicalItemId.ToString()
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
            if (occupyingItem.granted_skill_id != skillDef.skill_id)
            {
                errors.Add(
                    $"Skill book item {canonicalItemId} occupies generated skill book id for skill {skillDef.skill_id} but grants {occupyingItem.granted_skill_id}."
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

    private static GDictionary ProjectItemDefs(IReadOnlyDictionary<StringName, ItemDef> itemDefs)
    {
        GDictionary result = new();
        if (itemDefs == null)
            return result;
        foreach ((StringName itemId, ItemDef itemDef) in itemDefs)
        {
            if (itemId == "" || itemDef == null)
                continue;
            result[itemId] = itemDef;
        }
        return result;
    }

    private static Dictionary<StringName, ItemDef> BuildItemDefIndex(GDictionary itemDefs)
    {
        Dictionary<StringName, ItemDef> result = new();
        if (itemDefs == null)
            return result;
        foreach (Variant rawKey in itemDefs.Keys)
        {
            if (rawKey.VariantType != Variant.Type.StringName)
                continue;
            StringName itemId = rawKey.AsStringName();
            if (itemId == "")
                continue;
            if (itemDefs[rawKey].AsGodotObject() is ItemDef itemDef)
                result[itemId] = itemDef;
        }
        return result;
    }

    private static Dictionary<StringName, TraitDef> BuildTraitDefIndex(GDictionary traitDefs)
    {
        Dictionary<StringName, TraitDef> result = new();
        if (traitDefs == null)
            return result;
        foreach (Variant rawKey in traitDefs.Keys)
        {
            if (rawKey.VariantType != Variant.Type.StringName)
                continue;
            StringName traitId = rawKey.AsStringName();
            if (traitId == "")
                continue;
            if (traitDefs[rawKey].AsGodotObject() is TraitDef traitDef)
                result[traitId] = traitDef;
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
