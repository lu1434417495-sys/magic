using System.Collections.Generic;
using Godot;

internal sealed class BattleSimOverrideApplyResult
{
    internal BattleSimOverrideApplyResult(
        IReadOnlyDictionary<StringName, SkillDef> skillDefs,
        IReadOnlyDictionary<StringName, EnemyAiBrainDef> enemyAiBrains,
        BattleAiScoreProfile aiScoreProfile,
        IReadOnlyDictionary<StringName, BattleAiScoreProfile> factionAiScoreProfiles,
        IReadOnlyList<string> errors
    )
    {
        SkillDefs = skillDefs ?? new Dictionary<StringName, SkillDef>();
        EnemyAiBrains = enemyAiBrains ?? new Dictionary<StringName, EnemyAiBrainDef>();
        AiScoreProfile = aiScoreProfile;
        FactionAiScoreProfiles =
            factionAiScoreProfiles ?? new Dictionary<StringName, BattleAiScoreProfile>();
        Errors = errors ?? System.Array.Empty<string>();
    }

    internal IReadOnlyDictionary<StringName, SkillDef> SkillDefs { get; }

    internal IReadOnlyDictionary<StringName, EnemyAiBrainDef> EnemyAiBrains { get; }

    internal BattleAiScoreProfile AiScoreProfile { get; }

    internal IReadOnlyDictionary<StringName, BattleAiScoreProfile> FactionAiScoreProfiles { get; }

    internal IReadOnlyList<string> Errors { get; }
}
