using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Godot;

internal sealed class ContentSnapshotBuilder
{
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
        using var barrier = new BarrierContentRegistry();
        using var items = new ItemContentRegistry();
        using var gearSets = new GearSetContentRegistry();
        using var recipes = new RecipeContentRegistry();
        using var specialProfiles = new BattleSpecialProfileRegistry();
        using var enemies = new EnemyContentRegistry(loadDefaultContent: false);
        using var battleEncounters = new BattleEncounterContentRegistry();
        var faith = new FaithContentRegistry();

        items.Rebuild();
        gearSets.Rebuild();
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
        IReadOnlyDictionary<StringName, GearSetDefinition> gearSetDefinitions =
            gearSets.GetDefinitionsTyped();
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
        var battleSimProfiles = new BattleSimProfileContentRegistry();
        battleSimProfiles.Rebuild();
        IReadOnlyDictionary<StringName, BattleSimProfileDefinition> simulationProfileDefinitions =
            battleSimProfiles.GetDefinitions();
        var worlds = new WorldContentRegistry();
        worlds.Rebuild();
        IReadOnlyDictionary<StringName, WorldPresetDefinition> worldPresets = worlds.GetPresets();
        IReadOnlyDictionary<StringName, WorldGenerationDefinition> worldGenerations =
            worlds.GetGenerations();

        var validationErrors = new List<string>();
        AppendErrors(validationErrors, progression.ValidateTyped());
        AppendErrors(validationErrors, barrier.ValidateTyped());
        AppendErrors(validationErrors, items.ValidateTyped());
        AppendErrors(
            validationErrors,
            gearSets.ValidateTyped(
                itemDefinitions,
                traitDefinitions,
                progression.GetEquipmentAbilityBindingDefinitionsTyped()
            )
        );
        AppendErrors(validationErrors, recipes.ValidateTyped());
        AppendErrors(validationErrors, faith.GetValidationErrors());
        AppendErrors(validationErrors, specialProfiles.ValidateTyped());
        AppendErrors(validationErrors, enemies.ValidateTyped());
        AppendErrors(validationErrors, battleEncounterValidationErrors);
        AppendErrors(validationErrors, battleSimProfiles.GetValidationErrors());
        AppendErrors(validationErrors, worlds.GetValidationErrors());
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
                progression.GetQuestRegistrationErrorsTyped(),
                battleEncounterDefinitions,
                enemyDefinitions.EncounterRosters
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
            gearSetDefinitions,
            recipes.GetRecipeDefsTyped(),
            progression.GetEquipmentAbilityPackDefinitionsTyped(),
            progression.GetEquipmentAbilityBindingDefinitionsTyped(),
            worldPresets,
            worldGenerations,
            specialProfiles.BuildRuntimeProfileView(),
            enemyDefinitions.EnemyTemplates,
            enemyDefinitions.EnemyBrains,
            enemyDefinitions.EncounterRosters,
            battleEncounterDefinitions,
            simulationProfileDefinitions
        );
    }

    private static void AppendWorldValidationErrors(
        ICollection<string> errors,
        IReadOnlyDictionary<StringName, WorldGenerationDefinition> worldGenerations,
        IReadOnlyCollection<StringName> battleEncounterIds
    )
    {
        var validator = new WorldMapContentValidator();
        foreach ((StringName generationId, WorldGenerationDefinition definition) in worldGenerations)
        {
            AppendErrors(
                errors,
                validator.ValidateGenerationConfigTyped(
                    definition,
                    generationId.ToString(),
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
