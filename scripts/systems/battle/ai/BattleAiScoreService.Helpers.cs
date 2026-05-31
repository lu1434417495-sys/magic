using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

public partial class BattleAiScoreService
{
    private static void AppendUniqueStringName(GStringNameArray targetIds, StringName unitId)
    {
        if (IsEmpty(unitId))
        {
            return;
        }
        if (!targetIds.Contains(unitId))
        {
            targetIds.Add(unitId);
        }
    }

    private static GStringNameArray CopyStringNameArray(GArray values)
    {
        var result = new GStringNameArray();
        if (values == null)
        {
            return result;
        }
        foreach (var item in values)
        {
            StringName normalized = ProgressionDataUtils.to_string_name(item);
            if (!IsEmpty(normalized))
            {
                result.Add(normalized);
            }
        }
        return result;
    }

    private static GStringNameArray CopyStringNameArray(IEnumerable<StringName> values)
    {
        var result = new GStringNameArray();
        if (values == null)
        {
            return result;
        }
        foreach (StringName value in values)
        {
            StringName normalized = ProgressionDataUtils.to_string_name(value);
            if (!IsEmpty(normalized))
            {
                result.Add(normalized);
            }
        }
        return result;
    }

    private static int DistanceFromAnchorToUnit(
        IBattleAiScoreContext context,
        Vector2I anchorCoord,
        BattleUnitState targetUnit
    )
    {
        BattleUnitState actor = ContextUnitState(context);
        BattleGridService gridService = ContextGridService(context);
        if (actor == null || gridService == null || targetUnit == null)
        {
            return -1;
        }
        actor.refresh_footprint();
        targetUnit.refresh_footprint();
        int bestDistance = 999999;
        foreach (
            Vector2I sourceCoord in gridService.get_footprint_coords(
                anchorCoord,
                actor.footprint_size
            )
        )
        {
            foreach (Vector2I targetCoord in targetUnit.occupied_coords)
            {
                bestDistance = Math.Min(
                    bestDistance,
                    gridService.get_distance(sourceCoord, targetCoord)
                );
            }
        }
        return bestDistance < 999999 ? bestDistance : -1;
    }

    private int BuildPositionObjectiveScore(
        StringName positionObjectiveKind,
        int distanceValue,
        int desiredMinDistance,
        int desiredMaxDistance,
        int currentDistanceValue = -1
    )
    {
        if (distanceValue < 0 || desiredMinDistance < 0 || desiredMaxDistance < 0)
        {
            return 0;
        }
        if (positionObjectiveKind == "distance_band_progress")
        {
            return BuildDistanceBandProgressScore(
                distanceValue,
                desiredMinDistance,
                desiredMaxDistance,
                currentDistanceValue
            );
        }
        if (positionObjectiveKind == "distance_floor")
        {
            if (distanceValue < desiredMinDistance)
            {
                return -(
                    (desiredMinDistance - distanceValue) * _scoreProfile.position_undershoot_penalty
                );
            }
            return _scoreProfile.position_base_score
                + (distanceValue - desiredMinDistance) * _scoreProfile.position_distance_step;
        }
        if (distanceValue >= desiredMinDistance && distanceValue <= desiredMaxDistance)
        {
            return Math.Max(
                _scoreProfile.position_base_score
                    - distanceValue * _scoreProfile.position_distance_step,
                0
            );
        }
        if (distanceValue < desiredMinDistance)
        {
            return -(
                (desiredMinDistance - distanceValue) * _scoreProfile.position_undershoot_penalty
            );
        }
        return -((distanceValue - desiredMaxDistance) * _scoreProfile.position_overshoot_penalty);
    }

    private int BuildDistanceBandProgressScore(
        int distanceValue,
        int desiredMinDistance,
        int desiredMaxDistance,
        int currentDistanceValue
    )
    {
        int candidateGap = BuildDistanceGap(distanceValue, desiredMinDistance, desiredMaxDistance);
        if (candidateGap < 0)
        {
            return 0;
        }
        int currentGap = BuildDistanceGap(
            currentDistanceValue,
            desiredMinDistance,
            desiredMaxDistance
        );
        if (currentGap < 0)
        {
            return BuildDistanceBandAbsoluteScore(
                distanceValue,
                desiredMinDistance,
                desiredMaxDistance
            );
        }
        if (currentGap == 0)
        {
            return BuildDistanceBandAbsoluteScore(
                distanceValue,
                desiredMinDistance,
                desiredMaxDistance
            );
        }
        if (candidateGap < currentGap)
        {
            int progressSteps = currentGap - candidateGap;
            return _scoreProfile.position_base_score
                + progressSteps * _scoreProfile.position_distance_step;
        }
        if (candidateGap == currentGap)
        {
            return -_scoreProfile.position_distance_step;
        }
        return -((candidateGap - currentGap) * _scoreProfile.position_overshoot_penalty);
    }

    private static int BuildDistanceGap(
        int distanceValue,
        int desiredMinDistance,
        int desiredMaxDistance
    )
    {
        if (distanceValue < 0 || desiredMinDistance < 0 || desiredMaxDistance < 0)
        {
            return -1;
        }
        if (distanceValue < desiredMinDistance)
        {
            return desiredMinDistance - distanceValue;
        }
        if (distanceValue > desiredMaxDistance)
        {
            return distanceValue - desiredMaxDistance;
        }
        return 0;
    }

    private int BuildDistanceBandAbsoluteScore(
        int distanceValue,
        int desiredMinDistance,
        int desiredMaxDistance
    )
    {
        if (distanceValue >= desiredMinDistance && distanceValue <= desiredMaxDistance)
        {
            return Math.Max(
                _scoreProfile.position_base_score
                    - distanceValue * _scoreProfile.position_distance_step,
                0
            );
        }
        if (distanceValue < desiredMinDistance)
        {
            return -(
                (desiredMinDistance - distanceValue) * _scoreProfile.position_undershoot_penalty
            );
        }
        return -((distanceValue - desiredMaxDistance) * _scoreProfile.position_overshoot_penalty);
    }

    private int ResolveActionBaseScore(StringName actionKind, ScoreBuildMetadata metadata)
    {
        if (metadata != null && metadata.HasActionBaseScore)
        {
            return metadata.ActionBaseScore;
        }
        return _scoreProfile != null ? _scoreProfile.get_action_base_score(actionKind) : 0;
    }

    private static int ResolveActionTargetCount(BattleAiScoreInput scoreInput)
    {
        if (scoreInput == null)
        {
            return 0;
        }
        if (scoreInput.target_count > 0)
        {
            return scoreInput.target_count;
        }
        if (scoreInput.target_unit_ids.Count > 0)
        {
            return scoreInput.target_unit_ids.Count;
        }
        if (scoreInput.target_coords.Count > 0)
        {
            return scoreInput.target_coords.Count;
        }
        return 0;
    }

    private static BattleState ContextState(IBattleAiScoreContext context) => context?.state;

    private static BattleUnitState ContextUnitState(IBattleAiScoreContext context) =>
        context?.unit_state;

    private static BattleGridService ContextGridService(IBattleAiScoreContext context) =>
        context?.grid_service;

    private static GDictionary ContextSkillDefs(IBattleAiScoreContext context) =>
        context?.skill_defs ?? new GDictionary();

    private static Dictionary<string, object> ContextScoreProjectionCache(
        IBattleAiScoreContext context
    )
    {
        if (context == null)
        {
            return new Dictionary<string, object>();
        }
        context.score_projection_cache ??= new Dictionary<string, object>();
        return context.score_projection_cache;
    }

    private static BattleUnitState GetUnit(BattleState state, StringName unitId)
    {
        if (state == null || IsEmpty(unitId))
        {
            return null;
        }
        return state.TryGetUnitTyped(unitId, out BattleUnitState unitState) ? unitState : null;
    }

    private static SkillDef GetSkillDef(GDictionary skillDefs, StringName skillId)
    {
        if (skillDefs == null || IsEmpty(skillId))
        {
            return null;
        }
        return DictObject(skillDefs, skillId) as SkillDef;
    }

    private static GArray ToUntypedArray(Godot.Collections.Array<GDictionary> values)
    {
        var result = new GArray();
        if (values == null)
        {
            return result;
        }
        foreach (GDictionary value in values)
        {
            result.Add(value);
        }
        return result;
    }

    private static GStringNameArray DuplicateStringNameArray(GStringNameArray values)
    {
        var result = new GStringNameArray();
        if (values == null)
        {
            return result;
        }
        foreach (StringName value in values)
        {
            result.Add(value);
        }
        return result;
    }

    private static bool HasKey(GDictionary dictionary, object key)
    {
        return TryRead(dictionary, key, out _);
    }

    private static int DictInt(GDictionary dictionary, object key, int fallback)
    {
        if (!TryRead(dictionary, key, out Variant value))
            return fallback;
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static double DictDouble(GDictionary dictionary, object key, double fallback)
    {
        if (!TryRead(dictionary, key, out Variant value))
            return fallback;
        return value.VariantType switch
        {
            Variant.Type.Int => value.AsInt64(),
            Variant.Type.Float => value.AsDouble(),
            _ => fallback,
        };
    }

    private static string DictString(GDictionary dictionary, object key, string fallback)
    {
        if (!TryRead(dictionary, key, out Variant value))
            return fallback;
        return value.VariantType switch
        {
            Variant.Type.String => value.AsString(),
            Variant.Type.StringName => value.AsStringName().ToString(),
            _ => fallback,
        };
    }

    private static StringName DictStringName(
        GDictionary dictionary,
        object key,
        StringName fallback
    )
    {
        if (!TryRead(dictionary, key, out Variant value))
            return fallback;
        return value.VariantType switch
        {
            Variant.Type.StringName => value.AsStringName(),
            Variant.Type.String => new StringName(value.AsString()),
            _ => fallback,
        };
    }

    private static Vector2I DictVector2I(GDictionary dictionary, object key, Vector2I fallback)
    {
        if (!TryRead(dictionary, key, out Variant value))
            return fallback;
        return value.VariantType == Variant.Type.Vector2I ? value.AsVector2I() : fallback;
    }

    private static GodotObject DictObject(GDictionary dictionary, object key)
    {
        if (!TryRead(dictionary, key, out Variant value))
            return null;
        return value.VariantType == Variant.Type.Object ? value.AsGodotObject() : null;
    }

    private static GArray DictArray(GDictionary dictionary, object key, GArray fallback)
    {
        if (!TryRead(dictionary, key, out Variant value))
            return fallback;
        return value.VariantType == Variant.Type.Array ? value.AsGodotArray() : fallback;
    }

    private static GDictionary DictDictionary(
        GDictionary dictionary,
        object key,
        GDictionary fallback
    )
    {
        if (!TryRead(dictionary, key, out Variant value))
            return fallback;
        return value.VariantType == Variant.Type.Dictionary ? value.AsGodotDictionary() : fallback;
    }

    private static IEnumerable<GDictionary> ReadDictionaryItems(GArray values)
    {
        if (values == null)
            yield break;
        foreach (Variant value in values)
        {
            if (value.VariantType == Variant.Type.Dictionary)
                yield return value.AsGodotDictionary();
        }
    }

    private static IEnumerable<string> ReadStringItems(GArray values)
    {
        if (values == null)
            yield break;
        foreach (Variant value in values)
        {
            if (value.VariantType == Variant.Type.String)
                yield return value.AsString();
            else if (value.VariantType == Variant.Type.StringName)
                yield return value.AsStringName().ToString();
        }
    }

    private static IEnumerable<(StringName key, int value)> ReadStringNameIntEntries(
        GDictionary dictionary
    )
    {
        if (dictionary == null)
            yield break;
        foreach (Variant key in dictionary.Keys)
        {
            StringName normalizedKey = key.VariantType switch
            {
                Variant.Type.StringName => key.AsStringName(),
                Variant.Type.String => new StringName(key.AsString()),
                _ => "",
            };
            if (IsEmpty(normalizedKey))
                continue;
            Variant value = dictionary[key];
            if (value.VariantType == Variant.Type.Int)
                yield return (normalizedKey, value.AsInt32());
        }
    }

    private static bool TryRead(GDictionary dictionary, object key, out Variant value)
    {
        value = default;
        if (dictionary == null || key == null)
            return false;
        Variant variantKey = key switch
        {
            Variant valueKey => valueKey,
            string stringKey => stringKey,
            StringName stringNameKey => stringNameKey,
            int intKey => intKey,
            long longKey => longKey,
            _ => default,
        };
        if (variantKey.VariantType == Variant.Type.Nil)
            return false;
        if (dictionary.ContainsKey(variantKey))
        {
            value = dictionary[variantKey];
            return value.VariantType != Variant.Type.Nil;
        }
        if (variantKey.VariantType == Variant.Type.String)
        {
            StringName stringNameKey = new(variantKey.AsString());
            if (dictionary.ContainsKey(stringNameKey))
            {
                value = dictionary[stringNameKey];
                return value.VariantType != Variant.Type.Nil;
            }
        }
        else if (variantKey.VariantType == Variant.Type.StringName)
        {
            string stringKey = variantKey.AsStringName().ToString();
            if (dictionary.ContainsKey(stringKey))
            {
                value = dictionary[stringKey];
                return value.VariantType != Variant.Type.Nil;
            }
        }
        return false;
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || string.IsNullOrEmpty(value.ToString());
    }

    private static int RoundToInt(double value)
    {
        return (int)Mathf.Round((float)value);
    }
}
