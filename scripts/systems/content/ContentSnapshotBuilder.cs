using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Godot;

internal sealed class ContentSnapshotBuilder
{
    private static readonly string[] BattleSimProfilePaths =
    {
        "res://data/configs/battle_sim/profiles/baseline.tres",
        "res://data/configs/battle_sim/profiles/mist_controller_aggressive.tres",
        "res://data/configs/battle_sim/profiles/ranged_suppressor_cautious.tres",
        "res://data/configs/battle_sim/profiles/pinning_shot_blocked.tres",
    };

    private readonly IContentResourceLoader _loader;

    internal ContentSnapshotBuilder(IContentResourceLoader loader)
    {
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
    }

    internal ContentSnapshot Build(long epoch)
    {
        if (epoch <= 0)
            throw new ArgumentOutOfRangeException(nameof(epoch), epoch, "Snapshot epoch must be positive.");

        using var progression = new ProgressionContentRegistry(_loader);
        using var barrier = new BarrierContentRegistry(_loader);
        using var items = new ItemContentRegistry(_loader);
        using var recipes = new RecipeContentRegistry(_loader);
        using var specialProfiles = new BattleSpecialProfileRegistry(_loader);
        using var enemies = new EnemyContentRegistry(_loader, loadDefaultContent: false);
        using var battleEncounters = new BattleEncounterContentRegistry(_loader);
        var faith = new FaithContentRegistry(_loader);

        items.Rebuild();
        recipes.Setup(items.GetItemDefsTyped());
        faith.Rebuild();
        specialProfiles.Rebuild(progression.GetSkillDefinitionsTyped());

        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions =
            progression.GetSkillDefinitionsTyped();
        IReadOnlyDictionary<StringName, TraitDefinition> traitDefinitions =
            progression.GetTraitDefsTyped();
        IReadOnlyDictionary<StringName, BarrierProfileDefinition> barrierDefinitions =
            barrier.GetProfileDefsTyped();
        var itemDefinitionIndex = new Dictionary<StringName, ItemDefinition>(
            items.GetItemDefsTyped()
        );
        foreach (
            (StringName itemId, ItemDefinition definition) in SkillBookItemFactory
                .BuildGeneratedItemDefinitions(skillDefinitions, itemDefinitionIndex)
        )
        {
            itemDefinitionIndex[itemId] = definition;
        }
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions =
            new ReadOnlyDictionary<StringName, ItemDefinition>(itemDefinitionIndex);
        enemies.Rebuild(
            new EnemyContentValidationContext(itemDefinitions, skillDefinitions)
        );
        EnemyContentDefinitionGraph enemyDefinitions = enemies.ProjectDefinitions(
            itemDefinitions
        );
        battleEncounters.Rebuild(
            enemyDefinitions.EncounterRosters,
            enemyDefinitions.EnemyTemplates
        );
        IReadOnlyList<string> battleEncounterValidationErrors =
            battleEncounters.ValidateTyped();
        IReadOnlyDictionary<StringName, BattleEncounterDefinition> battleEncounterDefinitions =
            battleEncounterValidationErrors.Count == 0
                ? battleEncounters.ProjectDefinitions()
                : new ReadOnlyDictionary<StringName, BattleEncounterDefinition>(
                    new Dictionary<StringName, BattleEncounterDefinition>()
                );
        IReadOnlyDictionary<StringName, BattleSimProfileDefinition> simulationProfileDefinitions =
            BattleSimProfileAuthoringLoader.LoadDefinitions(_loader, BattleSimProfilePaths);
        IReadOnlyDictionary<string, WorldGenerationDefinition> worldGenerations =
            BuildWorldGenerations();

        var validationErrors = new List<string>();
        AppendErrors(validationErrors, progression.ValidateTyped());
        AppendErrors(validationErrors, barrier.ValidateTyped());
        AppendErrors(validationErrors, items.ValidateTyped());
        AppendErrors(validationErrors, recipes.ValidateTyped());
        AppendErrors(validationErrors, faith.GetValidationErrors());
        AppendErrors(validationErrors, specialProfiles.ValidateTyped());
        AppendErrors(validationErrors, enemies.ValidateTyped());
        AppendErrors(validationErrors, battleEncounterValidationErrors);
        AppendErrors(
            validationErrors,
            ItemTraitContentValidator.Validate(itemDefinitions, traitDefinitions)
        );
        AppendErrors(
            validationErrors,
            SkillBookItemContentValidator.Validate(itemDefinitions, skillDefinitions)
        );
        AppendErrors(
            validationErrors,
            BarrierSkillContentValidator.Validate(skillDefinitions, barrierDefinitions)
        );
        AppendErrors(
            validationErrors,
            QuestContentValidator.ValidateTyped(
                progression.GetQuestDefsTyped(),
                itemDefinitions,
                skillDefinitions,
                enemyDefinitions.EnemyTemplates,
                progression.GetQuestRegistrationErrorsTyped()
            )
        );
        AppendWorldValidationErrors(
            validationErrors,
            worldGenerations,
            battleEncounterDefinitions.Keys.ToArray()
        );
        ThrowIfInvalid(validationErrors);

        return new ContentSnapshot(
            epoch,
            skillDefinitions,
            traitDefinitions,
            progression.GetProfessionDefsTyped(),
            progression.GetAchievementDefsTyped(),
            progression.GetQuestDefsTyped(),
            progression.GetRaceDefsTyped(),
            progression.GetSubraceDefsTyped(),
            progression.GetAgeProfileDefsTyped(),
            progression.GetBloodlineDefsTyped(),
            progression.GetBloodlineStageDefsTyped(),
            progression.GetAscensionDefsTyped(),
            progression.GetAscensionStageDefsTyped(),
            progression.GetStageAdvancementDefsTyped(),
            faith.GetFaithDeityDefsTyped(),
            barrierDefinitions,
            progression.GetContingencySetupTemplatesTyped(),
            itemDefinitions,
            recipes.GetRecipeDefsTyped(),
            progression.GetEquipmentAbilityPackDefinitionsTyped(),
            progression.GetEquipmentAbilityBindingDefinitionsTyped(),
            worldGenerations,
            specialProfiles.BuildRuntimeProfileView(),
            enemyDefinitions.EnemyTemplates,
            enemyDefinitions.EnemyBrains,
            enemyDefinitions.EncounterRosters,
            battleEncounterDefinitions,
            simulationProfileDefinitions
        );
    }

    private IReadOnlyDictionary<string, WorldGenerationDefinition> BuildWorldGenerations()
    {
        var definitions = new Dictionary<string, WorldGenerationDefinition>(StringComparer.Ordinal);
        foreach (WorldPresetRegistry.WorldPresetInfo preset in WorldPresetRegistry.ListPresetsTyped())
        {
            if (preset == null || string.IsNullOrWhiteSpace(preset.GenerationConfigPath))
                throw new InvalidDataException("World preset entries must declare generation config paths.");

            string canonicalPath = ContentPathCanonicalizer.Canonicalize(
                preset.GenerationConfigPath
            );
            WorldMapGenerationConfig resource = _loader.LoadCanonical<WorldMapGenerationConfig>(
                canonicalPath
            );
            WorldGenerationDefinition definition = WorldGenerationDefinition.FromResource(
                canonicalPath,
                resource,
                _loader
            );
            IndexWorldGeneration(definitions, definition);
        }
        return new ReadOnlyDictionary<string, WorldGenerationDefinition>(definitions);
    }

    private static void IndexWorldGeneration(
        IDictionary<string, WorldGenerationDefinition> definitions,
        WorldGenerationDefinition definition
    )
    {
        ArgumentNullException.ThrowIfNull(definition);
        string canonicalPath = ContentPathCanonicalizer.Canonicalize(definition.CanonicalPath);
        if (definitions.ContainsKey(canonicalPath))
            return;
        definitions.Add(canonicalPath, definition);
        foreach (MountedSubmapDefinition mountedSubmap in definition.MountedSubmaps)
        {
            if (mountedSubmap?.Generation != null)
                IndexWorldGeneration(definitions, mountedSubmap.Generation);
        }
    }

    private static void AppendWorldValidationErrors(
        ICollection<string> errors,
        IReadOnlyDictionary<string, WorldGenerationDefinition> worldGenerations,
        IReadOnlyCollection<StringName> battleEncounterIds
    )
    {
        var validator = new WorldMapContentValidator();
        foreach ((string path, WorldGenerationDefinition definition) in worldGenerations)
        {
            AppendErrors(
                errors,
                validator.ValidateGenerationConfigTyped(
                    definition,
                    path,
                    battleEncounterIds
                )
            );
        }
    }

    private static void AppendErrors(ICollection<string> target, IEnumerable<string> source)
    {
        if (source == null)
            return;
        foreach (string error in source)
        {
            if (!string.IsNullOrWhiteSpace(error))
                target.Add(error.Trim());
        }
    }

    private static void ThrowIfInvalid(IEnumerable<string> validationErrors)
    {
        string[] errors = validationErrors
            .Where(error => !string.IsNullOrWhiteSpace(error))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(error => error, StringComparer.Ordinal)
            .ToArray();
        if (errors.Length == 0)
            return;
        throw new InvalidDataException(
            $"Process content validation failed with {errors.Length} error(s):\n"
                + string.Join("\n", errors)
        );
    }

}
