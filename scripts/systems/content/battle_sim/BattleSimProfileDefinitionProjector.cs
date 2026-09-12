#nullable enable

using System;
using System.Collections.Generic;

internal static class BattleSimProfileDefinitionProjector
{
    internal static BattleSimProfileDefinition Project(BattleSimProfileImportModel source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var patches = new List<BattleSimOverridePatchDefinition>(source.OverridePatches.Count);
        foreach (BattleSimOverridePatchImportModel patch in source.OverridePatches)
        {
            patches.Add(new BattleSimOverridePatchDefinition(
                patch.TargetType,
                patch.TargetId,
                patch.StateId,
                patch.ActionId,
                patch.Path,
                patch.Value
            ));
        }
        return new BattleSimProfileDefinition(
            source.ProfileId,
            source.DisplayName,
            source.Description,
            EnemyContentDefinitionProjector.ProjectScoreProfile(source.AiScoreProfile),
            patches
        );
    }
}
