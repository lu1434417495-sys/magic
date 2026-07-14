using System.Collections.Generic;
using Godot;

internal static class BattleValidationResultProjection
{
    internal static GodotProjectionLease<Godot.Collections.Dictionary> ProjectUnitSkillLease(
        BattleUnitSkillValidationResult result
    )
    {
        var payload = new Dictionary<string, object>(System.StringComparer.Ordinal)
        {
            ["allowed"] = result.Allowed,
            ["message"] = result.Message ?? "",
            ["target_unit_ids"] = ToPlainList(result.TargetUnitIds),
            ["target_units"] = BuildUnitSnapshotsPlain(result.TargetUnits),
            ["random_chain_candidate_unit_ids"] = ToPlainList(
                result.RandomChainCandidateUnitIds
            ),
            ["preview_coords"] = ToPlainList(result.PreviewCoords),
        };
        return ProjectLease(payload, "unit-skill");
    }

    internal static GodotProjectionLease<Godot.Collections.Dictionary> ProjectGroundSkillLease(
        BattleGroundSkillValidationResult result
    )
    {
        var payload = new Dictionary<string, object>(System.StringComparer.Ordinal)
        {
            ["allowed"] = result.Allowed,
            ["message"] = result.Message ?? "",
            ["target_coords"] = ToPlainList(result.TargetCoords),
            ["resolved_anchor_coord"] = result.ResolvedAnchorCoord,
        };
        if (result.HasPreviewCoords)
        {
            payload["preview_coords"] = ToPlainList(result.PreviewCoords);
        }
        if (result.Direction != Vector2I.Zero)
        {
            payload["direction"] = result.Direction;
        }
        if (result.Distance > 0)
        {
            payload["distance"] = result.Distance;
        }
        return ProjectLease(payload, "ground-skill");
    }

    internal static GodotProjectionLease<Godot.Collections.Dictionary> ProjectTargetCollectionLease(
        BattleTargetCollectionResult result
    )
    {
        if (result == null)
            return ProjectLease(new Dictionary<string, object>(), "target-collection-empty");
        return ProjectLease(
            new Dictionary<string, object>(System.StringComparer.Ordinal)
            {
                ["handled"] = result.Handled,
                ["target_coords"] = ToPlainList(result.TargetCoords),
            },
            "target-collection"
        );
    }

    private static GodotProjectionLease<Godot.Collections.Dictionary> ProjectLease(
        IReadOnlyDictionary<string, object> payload,
        string operation
    ) =>
        RuntimePlainPayload.ProjectDictionaryLease(
            payload,
            "battle-validation-result",
            LifetimeDomain.Request,
            $"BattleValidationResultProjection.{operation}"
        );

    private static List<object> ToPlainList<T>(IReadOnlyList<T> values)
    {
        var result = new List<object>();
        foreach (T value in values ?? System.Array.Empty<T>())
            result.Add(value);
        return result;
    }

    private static List<object> BuildUnitSnapshotsPlain(
        IReadOnlyList<BattleUnitState> units
    )
    {
        var result = new List<object>();
        if (units == null)
            return result;
        foreach (BattleUnitState unit in units)
        {
            if (unit != null)
                result.Add(unit.BuildSnapshotPlain());
        }
        return result;
    }
}
