using Godot;
using System;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class SpySequenceDamageResolver : BattleDamageResolver
{
    public int sequence_preview_call_count = 0;

    public override GDictionary preview_damage_sequence(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        Godot.Collections.Array effect_defs,
        GDictionary damage_context = null,
        GDictionary options = null
    )
    {
        return preview_damage_sequence_typed(
            source_unit,
            target_unit,
            effect_defs,
            damage_context,
            options
        ).ToDictionary();
    }

    internal override BattleDamagePreviewResult preview_damage_sequence_typed(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        Godot.Collections.Array effect_defs,
        GDictionary damage_context = null,
        GDictionary options = null
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
            damageEvents: new GArray(),
            diagnostics: new GArray()
        );
    }
}
