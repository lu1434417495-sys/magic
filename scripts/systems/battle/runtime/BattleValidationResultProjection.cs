using System.Collections.Generic;
using Godot;

internal static class BattleValidationResultProjection
{
    internal static Godot.Collections.Dictionary ProjectUnitSkill(
        BattleUnitSkillValidationResult result
    ) =>
        new()
        {
            ["allowed"] = result.Allowed,
            ["message"] = result.Message ?? "",
            ["target_unit_ids"] = ToStringNameArray(result.TargetUnitIds),
            ["target_units"] = ToUnitArray(result.TargetUnits),
            ["random_chain_candidate_unit_ids"] = ToStringNameArray(
                result.RandomChainCandidateUnitIds
            ),
            ["preview_coords"] = ToVector2IArray(result.PreviewCoords),
        };

    internal static Godot.Collections.Dictionary ProjectGroundSkill(
        BattleGroundSkillValidationResult result
    )
    {
        var payload = new Godot.Collections.Dictionary
        {
            ["allowed"] = result.Allowed,
            ["message"] = result.Message ?? "",
            ["target_coords"] = ToVector2IArray(result.TargetCoords),
            ["resolved_anchor_coord"] = result.ResolvedAnchorCoord,
        };
        if (result.HasPreviewCoords)
        {
            payload["preview_coords"] = ToVector2IArray(result.PreviewCoords);
        }
        if (result.Direction != Vector2I.Zero)
        {
            payload["direction"] = result.Direction;
        }
        if (result.Distance > 0)
        {
            payload["distance"] = result.Distance;
        }
        return payload;
    }

    internal static Godot.Collections.Dictionary ProjectTargetCollection(
        BattleTargetCollectionResult result
    )
    {
        if (result == null)
            return new Godot.Collections.Dictionary();
        return new Godot.Collections.Dictionary
        {
            ["handled"] = result.Handled,
            ["target_coords"] = ToVector2IArray(result.TargetCoords),
        };
    }

    private static Godot.Collections.Array<StringName> ToStringNameArray(
        IReadOnlyList<StringName> ids
    )
    {
        var result = new Godot.Collections.Array<StringName>();
        if (ids == null)
            return result;
        foreach (StringName id in ids)
        {
            result.Add(id);
        }
        return result;
    }

    private static Godot.Collections.Array ToUnitArray(IReadOnlyList<BattleUnitState> units)
    {
        var result = new Godot.Collections.Array();
        if (units == null)
            return result;
        foreach (BattleUnitState unit in units)
        {
            if (unit != null)
            {
                result.Add(unit.ToDictionary());
            }
        }
        return result;
    }

    private static Godot.Collections.Array<Vector2I> ToVector2IArray(
        IReadOnlyList<Vector2I> coords
    )
    {
        var result = new Godot.Collections.Array<Vector2I>();
        if (coords == null)
            return result;
        foreach (Vector2I coord in coords)
        {
            result.Add(coord);
        }
        return result;
    }
}
