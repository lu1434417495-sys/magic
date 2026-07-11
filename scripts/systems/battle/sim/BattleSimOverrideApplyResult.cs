using System.Collections.Generic;
using Godot;

internal sealed class BattleSimOverrideApplyResult
{
    internal BattleSimOverrideApplyResult(
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        IReadOnlyDictionary<StringName, EnemyAiBrainDefinition> enemyAiBrains,
        BattleAiScoreProfileDefinition aiScoreProfile,
        IReadOnlyDictionary<StringName, BattleAiScoreProfileDefinition> factionAiScoreProfiles,
        IReadOnlyList<string> errors
    )
    {
        SkillDefinitions = skillDefinitions ?? new Dictionary<StringName, SkillDefinition>();
        EnemyAiBrains = enemyAiBrains ?? new Dictionary<StringName, EnemyAiBrainDefinition>();
        AiScoreProfile = aiScoreProfile;
        FactionAiScoreProfiles =
            factionAiScoreProfiles
            ?? new Dictionary<StringName, BattleAiScoreProfileDefinition>();
        Errors = errors ?? System.Array.Empty<string>();
    }

    internal IReadOnlyDictionary<StringName, SkillDefinition> SkillDefinitions { get; }

    internal IReadOnlyDictionary<StringName, EnemyAiBrainDefinition> EnemyAiBrains { get; }

    internal BattleAiScoreProfileDefinition AiScoreProfile { get; }

    internal IReadOnlyDictionary<StringName, BattleAiScoreProfileDefinition> FactionAiScoreProfiles { get; }

    internal IReadOnlyList<string> Errors { get; }
}
