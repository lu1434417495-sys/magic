using System;
using System.Collections.Generic;
using Godot;

internal interface IBattleAiScoreContext
{
    BattleState state { get; }
    BattleUnitState unit_state { get; }
    BattleGridService grid_service { get; }
    StringName basic_attack_skill_id { get; }
    IReadOnlyDictionary<StringName, SkillDefinition> skill_definitions { get; }
    IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> equipment_ability_bindings { get; }
    IReadOnlyDictionary<StringName, ItemDefinition> item_definitions { get; }
    IReadOnlyDictionary<StringName, BarrierProfileDefinition> barrier_profile_definitions { get; }
    ISkillCatalog skill_catalog { get; }
    Func<
        BattleUnitState,
        SkillDefinition,
        BattleSkillCastBlockReasonKind
    > skill_cast_block_reason_callback { get; }
}
