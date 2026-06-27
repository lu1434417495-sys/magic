using System.Collections.Generic;
using Godot;

internal interface IBattleAiScoreContext
{
    BattleState state { get; }
    BattleUnitState unit_state { get; }
    BattleGridService grid_service { get; }
    IReadOnlyDictionary<StringName, SkillDefinition> skill_definitions { get; }
    ISkillCatalog skill_catalog { get; }
}
