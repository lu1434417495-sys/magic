using System;

public sealed class CombatApproachAttackDefinition
{
    public CombatApproachAttackDefinition(int maximumPathHeightDeltaFromOrigin)
    {
        MaximumPathHeightDeltaFromOrigin = Math.Max(
            maximumPathHeightDeltaFromOrigin,
            0
        );
    }

    public int MaximumPathHeightDeltaFromOrigin { get; }

    public static CombatApproachAttackDefinition FromDiagnosticFixture(
        CombatApproachAttackDef source
    ) =>
        source == null
            ? null
            : new CombatApproachAttackDefinition(
                source.maximum_path_height_delta_from_origin
            );
}
