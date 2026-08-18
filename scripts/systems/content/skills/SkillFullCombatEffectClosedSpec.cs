#nullable enable

using System;
using System.Collections.Generic;

internal static class SkillFullCombatEffectClosedSpec
{
    internal static IReadOnlyList<ContentJsonSchemaClosedKindBranch> SchemaBranches { get; } =
        Array.AsReadOnly(
            new[]
            {
                new ContentJsonSchemaClosedKindBranch(
                    CombatEffectImportClosedSpec.LayeredBarrierKindValue,
                    typeof(LayeredBarrierEffectPayloadJsonDto)
                ),
            }
        );

    internal static bool TryParseKind(string? value, out CombatEffectImportKind result)
    {
        if (string.Equals(
            value,
            CombatEffectImportClosedSpec.LayeredBarrierKindValue,
            StringComparison.Ordinal
        ))
        {
            result = CombatEffectImportKind.LayeredBarrier;
            return true;
        }

        result = default;
        return false;
    }

    internal static bool IsPayloadCompatible(
        CombatEffectImportKind kind,
        ICombatEffectPayloadImportModel payload
    ) =>
        kind == CombatEffectImportKind.LayeredBarrier
        && payload is LayeredBarrierEffectPayloadImportModel;
}
