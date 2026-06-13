using Godot;
using System;
using System.Collections.Generic;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class SpySequenceDamageResolver : BattleDamageResolver
{
    public int sequence_preview_call_count = 0;

    internal override BattleDamagePreviewResult preview_damage_sequence_typed(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        Godot.Collections.Array effect_defs,
        GDictionary damage_context = null,
        BattleDamagePreviewOptions options = null
    )
    {
        sequence_preview_call_count += 1;
        return BattleDamagePreviewResult.Create(
            applied: true,
            hpDamage: 7,
            damage: 7,
            postSaveDamage: 7,
            incomingBudgetDamage: 7,
            shieldAbsorbed: 0,
            shieldBroken: false,
            stableLethal: false,
            lethalProbabilityBasisPoints: 0,
            saveEstimates: Array.Empty<BattleDamagePreviewSaveEstimate>(),
            damageEvents: new List<object>(),
            diagnostics: new List<object>()
        );
    }
}
