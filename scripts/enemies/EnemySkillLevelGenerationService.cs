using System;
using System.Collections.Generic;
using Godot;

internal static class EnemySkillLevelGenerationService
{
    private const int LegacyCoreLevel = 3;
    private static readonly StringName BasicAttackSkillId = "basic_attack";

    internal static void ApplyGeneratedLevels(
        BattleUnitState unitState,
        EnemyTemplateDefinition template,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        long generationSeed,
        int unitIndex
    )
    {
        if (unitState == null)
            return;

        ApplyConfiguredLevels(unitState, template);
        if (template?.GeneratedCoreSkillCount <= 0)
            return;

        ApplyGeneratedCoreLevels(
            unitState,
            template,
            skillDefinitions,
            generationSeed,
            unitIndex
        );
    }

    internal static int ResolveCoreSkillLevel(SkillDefinition skillDefinition)
    {
        if (skillDefinition == null || skillDefinition.MaxLevel <= 0)
            return 0;
        if (skillDefinition.NonCoreMaxLevel > 0)
        {
            return Math.Min(
                skillDefinition.NonCoreMaxLevel,
                skillDefinition.MaxLevel
            );
        }
        return Math.Min(skillDefinition.MaxLevel, LegacyCoreLevel);
    }

    private static void ApplyConfiguredLevels(
        BattleUnitState unitState,
        EnemyTemplateDefinition template
    )
    {
        foreach (StringName rawSkillId in unitState.GetKnownActiveSkillsViewTyped())
        {
            StringName skillId = new(rawSkillId.ToString());
            int configuredLevel = template?.GetSkillLevelTyped(skillId, 1) ?? 1;
            unitState.SetKnownSkillLevelTyped(skillId, Math.Max(configuredLevel, 1));
        }
    }

    private static void ApplyGeneratedCoreLevels(
        BattleUnitState unitState,
        EnemyTemplateDefinition template,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        long generationSeed,
        int unitIndex
    )
    {
        List<StringName> candidates = CollectCoreCandidates(template, skillDefinitions);
        var random = new RuntimeRandom(
            BuildUnitSeed(generationSeed, template.TemplateId, unitIndex)
        );
        Shuffle(candidates, random);

        int selectedCount = Math.Min(template.GeneratedCoreSkillCount, candidates.Count);
        var selectedCoreSkills = new HashSet<StringName>();
        for (int index = 0; index < selectedCount; index++)
            selectedCoreSkills.Add(candidates[index]);

        foreach (StringName skillId in candidates)
        {
            SkillDefinition skillDefinition = skillDefinitions[skillId];
            int coreLevel = ResolveCoreSkillLevel(skillDefinition);
            int absoluteMax = Math.Max(skillDefinition.MaxLevel, coreLevel);
            int generatedLevel;
            if (selectedCoreSkills.Contains(skillId))
            {
                generatedLevel = random.RandiRange(coreLevel, absoluteMax);
            }
            else
            {
                generatedLevel = random.RandiRange(1, Math.Max(coreLevel - 1, 1));
            }
            unitState.SetKnownSkillLevelTyped(skillId, generatedLevel);
        }
    }

    private static List<StringName> CollectCoreCandidates(
        EnemyTemplateDefinition template,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions
    )
    {
        var candidates = new List<StringName>();
        if (template == null || skillDefinitions == null)
            return candidates;

        foreach (StringName rawSkillId in template.SkillIds)
        {
            StringName skillId = new(rawSkillId.ToString());
            if (
                skillId == ""
                || skillId == BasicAttackSkillId
                || !skillDefinitions.TryGetValue(skillId, out SkillDefinition skillDefinition)
                || ResolveCoreSkillLevel(skillDefinition) <= 0
            )
            {
                continue;
            }
            candidates.Add(skillId);
        }
        return candidates;
    }

    private static void Shuffle(List<StringName> values, RuntimeRandom random)
    {
        for (int index = values.Count - 1; index > 0; index--)
        {
            int swapIndex = random.RandiRange(0, index);
            (values[index], values[swapIndex]) = (values[swapIndex], values[index]);
        }
    }

    private static long BuildUnitSeed(
        long generationSeed,
        StringName templateId,
        int unitIndex
    )
    {
        unchecked
        {
            const long offset = 1469598103934665603L;
            const long prime = 1099511628211L;
            long hash = offset ^ generationSeed;
            foreach (char value in templateId.ToString())
            {
                hash ^= value;
                hash *= prime;
            }
            hash ^= unitIndex;
            hash *= prime;
            return hash;
        }
    }
}
