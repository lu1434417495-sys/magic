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
        return DictSkillDef(skillDefs, skillId);
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

    private static bool HasKey(GDictionary dictionary, string key)
    {
        if (dictionary == null || string.IsNullOrEmpty(key))
            return false;
        return dictionary.ContainsKey(key) || dictionary.ContainsKey(new StringName(key));
    }

    private static int DictInt(GDictionary dictionary, string key, int fallback)
    {
        if (dictionary == null || string.IsNullOrEmpty(key))
            return fallback;
        if (dictionary.ContainsKey(key))
            return dictionary[key].AsInt32();
        StringName stringNameKey = key;
        return dictionary.ContainsKey(stringNameKey) ? dictionary[stringNameKey].AsInt32() : fallback;
    }

    private static double DictDouble(GDictionary dictionary, string key, double fallback)
    {
        try
        {
            if (dictionary == null || string.IsNullOrEmpty(key))
                return fallback;
            if (dictionary.ContainsKey(key))
                return dictionary[key].AsDouble();
            StringName stringNameKey = key;
            return dictionary.ContainsKey(stringNameKey)
                ? dictionary[stringNameKey].AsDouble()
                : fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private static string DictString(GDictionary dictionary, string key, string fallback)
    {
        if (dictionary == null || string.IsNullOrEmpty(key))
            return fallback;
        if (dictionary.ContainsKey(key))
            return dictionary[key].ToString();
        StringName stringNameKey = key;
        return dictionary.ContainsKey(stringNameKey) ? dictionary[stringNameKey].ToString() : fallback;
    }

    private static StringName DictStringName(
        GDictionary dictionary,
        string key,
        StringName fallback
    )
    {
        if (dictionary == null || string.IsNullOrEmpty(key))
            return fallback;
        StringName normalized = dictionary.ContainsKey(key)
            ? ProgressionDataUtils.to_string_name(dictionary[key])
            : dictionary.ContainsKey(new StringName(key))
                ? ProgressionDataUtils.to_string_name(dictionary[new StringName(key)])
                : "";
        return IsEmpty(normalized) ? fallback : normalized;
    }

    private static Vector2I DictVector2I(GDictionary dictionary, string key, Vector2I fallback)
    {
        if (dictionary == null || string.IsNullOrEmpty(key))
            return fallback;
        if (dictionary.ContainsKey(key))
            return dictionary[key].AsVector2I();
        StringName stringNameKey = key;
        return dictionary.ContainsKey(stringNameKey) ? dictionary[stringNameKey].AsVector2I() : fallback;
    }

    private static SkillDef DictSkillDef(GDictionary dictionary, StringName key)
    {
        if (dictionary == null || IsEmpty(key))
            return null;
        if (dictionary.ContainsKey(key))
            return dictionary[key].As<SkillDef>();
        string textKey = key.ToString();
        return dictionary.ContainsKey(textKey) ? dictionary[textKey].As<SkillDef>() : null;
    }

    private static CombatEffectDef DictCombatEffectDef(GDictionary dictionary, string key)
    {
        if (dictionary == null || string.IsNullOrEmpty(key))
            return null;
        if (dictionary.ContainsKey(key))
            return dictionary[key].As<CombatEffectDef>();
        StringName stringNameKey = key;
        return dictionary.ContainsKey(stringNameKey)
            ? dictionary[stringNameKey].As<CombatEffectDef>()
            : null;
    }

    private static GArray DictArray(GDictionary dictionary, string key, GArray fallback)
    {
        if (dictionary == null || string.IsNullOrEmpty(key))
            return fallback;
        if (dictionary.ContainsKey(key))
            return dictionary[key].AsGodotArray();
        StringName stringNameKey = key;
        return dictionary.ContainsKey(stringNameKey)
            ? dictionary[stringNameKey].AsGodotArray()
            : fallback;
    }

    private static GDictionary DictDictionary(
        GDictionary dictionary,
        string key,
        GDictionary fallback
    )
    {
        if (dictionary == null || string.IsNullOrEmpty(key))
            return fallback;
        if (dictionary.ContainsKey(key))
            return dictionary[key].AsGodotDictionary();
        StringName stringNameKey = key;
        return dictionary.ContainsKey(stringNameKey)
            ? dictionary[stringNameKey].AsGodotDictionary()
            : fallback;
    }

    private static IEnumerable<GDictionary> ReadDictionaryItems(GArray values)
    {
        if (values == null)
            yield break;
        foreach (var value in values)
        {
            yield return value.AsGodotDictionary();
        }
    }

    private static IEnumerable<string> ReadStringItems(GArray values)
    {
        if (values == null)
            yield break;
        foreach (var value in values)
        {
            string text = value.ToString();
            if (!string.IsNullOrEmpty(text))
                yield return text;
        }
    }

    private static IEnumerable<(StringName key, int value)> ReadStringNameIntEntries(
        GDictionary dictionary
    )
    {
        if (dictionary == null)
            yield break;
        foreach (var key in dictionary.Keys)
        {
            StringName normalizedKey = ProgressionDataUtils.to_string_name(key);
            if (IsEmpty(normalizedKey))
                continue;
            if (dictionary.ContainsKey(key))
                yield return (normalizedKey, dictionary[key].AsInt32());
        }
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
