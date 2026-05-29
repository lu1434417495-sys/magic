using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public interface IBattleAiScoreContext
{
    BattleState state { get; }
    BattleUnitState unit_state { get; }
    BattleGridService grid_service { get; }
    GDictionary skill_defs { get; }
    Dictionary<string, object> score_projection_cache { get; set; }
}
