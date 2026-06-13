using System.Collections.Generic;
using Godot;

internal sealed class BattleSimOverrideApplyResult
{
    internal BattleSimOverrideApplyResult(
        IReadOnlyDictionary<StringName, SkillDef> skillDefs,
        IReadOnlyDictionary<StringName, EnemyAiBrainDef> enemyAiBrains,
        BattleAiScoreProfile aiScoreProfile,
        IReadOnlyList<string> errors
    )
    {
        SkillDefs = skillDefs ?? new Dictionary<StringName, SkillDef>();
        EnemyAiBrains = enemyAiBrains ?? new Dictionary<StringName, EnemyAiBrainDef>();
        AiScoreProfile = aiScoreProfile;
        Errors = errors ?? System.Array.Empty<string>();
    }

    internal IReadOnlyDictionary<StringName, SkillDef> SkillDefs { get; }

    internal IReadOnlyDictionary<StringName, EnemyAiBrainDef> EnemyAiBrains { get; }

    internal BattleAiScoreProfile AiScoreProfile { get; }

    internal IReadOnlyList<string> Errors { get; }
}
