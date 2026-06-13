using System.Collections.Generic;
using Godot;

internal interface IBattleAiScoreContext
{
    BattleState state { get; }
    BattleUnitState unit_state { get; }
    BattleGridService grid_service { get; }
    IReadOnlyDictionary<StringName, SkillDef> skill_defs { get; }
    ISkillCatalog skill_catalog { get; }
}
