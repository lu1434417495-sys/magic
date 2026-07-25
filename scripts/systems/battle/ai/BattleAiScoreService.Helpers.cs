using System;
using System.Collections.Generic;
using Godot;

public partial class BattleAiScoreService
{
    private static void AppendUniqueStringName(List<StringName> targetIds, StringName unitId)
    {
        if (targetIds == null || IsEmpty(unitId))
        {
            return;
        }
        if (!targetIds.Contains(unitId))
        {
            targetIds.Add(unitId);
        }
    }

    private static List<StringName> CopyStringNameArray(IEnumerable<StringName> values)
    {
        var result = new List<StringName>();
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
        int bestDistance = 999999;
        foreach (
            Vector2I sourceCoord in gridService.GetFootprintCoords(
                anchorCoord,
                actor.GetFootprintSize()
            )
        )
        {
            foreach (
                Vector2I targetCoord in targetUnit.GetOccupiedCoordsReadViewTyped()
            )
            {
                bestDistance = Math.Min(
                    bestDistance,
                    gridService.GetDistance(sourceCoord, targetCoord)
                );
            }
        }
        return bestDistance < 999999 ? bestDistance : -1;
    }

    private int DistanceFromAnchorToUnitCached(
        IBattleAiScoreContext context,
        Vector2I anchorCoord,
        BattleUnitState targetUnit
    )
    {
        BattleUnitState actor = ContextUnitState(context);
        if (!_decisionScopeActive || actor == null || targetUnit == null)
        {
            return DistanceFromAnchorToUnit(context, anchorCoord, targetUnit);
        }
        Vector2I actorFootprintSize = actor.GetFootprintSize();
        Vector2I footprintSize = new(
            Math.Max(actorFootprintSize.X, 1),
            Math.Max(actorFootprintSize.Y, 1)
        );
        var key = new AnchorDistanceCacheKey(
            ProgressionDataUtils.to_string_name(actor.unit_id),
            anchorCoord,
            footprintSize,
            ProgressionDataUtils.to_string_name(targetUnit.unit_id)
        );
        if (_anchorDistanceCache.TryGetValue(key, out int cachedDistance))
        {
            return cachedDistance;
        }
        int distance = DistanceFromAnchorToUnit(context, anchorCoord, targetUnit);
        _anchorDistanceCache[key] = distance;
        return distance;
    }

    private int BuildPositionObjectiveScore(
        BattlePositionObjectiveKind positionObjectiveKind,
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
        if (positionObjectiveKind == BattlePositionObjectiveKind.DistanceBandProgress)
        {
            return BuildDistanceBandProgressScore(
                distanceValue,
                desiredMinDistance,
                desiredMaxDistance,
                currentDistanceValue
            );
        }
        if (positionObjectiveKind == BattlePositionObjectiveKind.DistanceFloor)
        {
            if (distanceValue < desiredMinDistance)
            {
                return -(
                    (desiredMinDistance - distanceValue) * _scoreProfile.PositionUndershootPenalty
                );
            }
            return _scoreProfile.PositionBaseScore
                + (distanceValue - desiredMinDistance) * _scoreProfile.PositionDistanceStep;
        }
        if (distanceValue >= desiredMinDistance && distanceValue <= desiredMaxDistance)
        {
            return Math.Max(
                _scoreProfile.PositionBaseScore
                    - distanceValue * _scoreProfile.PositionDistanceStep,
                0
            );
        }
        if (distanceValue < desiredMinDistance)
        {
            return -(
                (desiredMinDistance - distanceValue) * _scoreProfile.PositionUndershootPenalty
            );
        }
        return -((distanceValue - desiredMaxDistance) * _scoreProfile.PositionOvershootPenalty);
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
            return _scoreProfile.PositionBaseScore
                + progressSteps * _scoreProfile.PositionDistanceStep;
        }
        if (candidateGap == currentGap)
        {
            return -_scoreProfile.PositionDistanceStep;
        }
        return -((candidateGap - currentGap) * _scoreProfile.PositionOvershootPenalty);
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
                _scoreProfile.PositionBaseScore
                    - distanceValue * _scoreProfile.PositionDistanceStep,
                0
            );
        }
        if (distanceValue < desiredMinDistance)
        {
            return -(
                (desiredMinDistance - distanceValue) * _scoreProfile.PositionUndershootPenalty
            );
        }
        return -((distanceValue - desiredMaxDistance) * _scoreProfile.PositionOvershootPenalty);
    }

    private int ResolveActionBaseScore(StringName actionKind, ScoreBuildMetadata metadata)
    {
        if (metadata != null && metadata.HasActionBaseScore)
        {
            return metadata.ActionBaseScore;
        }
        return _scoreProfile != null ? _scoreProfile.GetActionBaseScore(actionKind) : 0;
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

    private static IReadOnlyDictionary<StringName, SkillDefinition> ContextSkillDefinitions(
        IBattleAiScoreContext context
    ) => context?.skill_definitions ?? new Dictionary<StringName, SkillDefinition>();

    private static ISkillCatalog ContextSkillCatalog(IBattleAiScoreContext context) =>
        context?.skill_catalog;

    private static BattleUnitState GetUnit(BattleState state, StringName unitId)
    {
        if (state == null || IsEmpty(unitId))
        {
            return null;
        }
        return state.TryGetUnitTyped(unitId, out BattleUnitState unitState) ? unitState : null;
    }

    private static SkillDefinition GetSkillDefinition(
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        StringName skillId
    )
    {
        if (skillDefinitions == null || IsEmpty(skillId))
        {
            return null;
        }
        return skillDefinitions.TryGetValue(skillId, out SkillDefinition skillDefinition)
            ? skillDefinition
            : null;
    }

    private static SkillDefinition ResolveScoreInputSkillDefinition(
        BattleAiScoreInput scoreInput,
        IBattleAiScoreContext context
    )
    {
        return GetSkillDefinition(
            ContextSkillDefinitions(context),
            ProgressionDataUtils.to_string_name(scoreInput?.skill_id ?? new StringName(""))
        );
    }

    private static List<StringName> DuplicateStringNameArray(IEnumerable<StringName> values)
    {
        var result = new List<StringName>();
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

    private static bool HasMetadataKey(IReadOnlyDictionary<string, object> metadata, string key)
    {
        return metadata != null && !string.IsNullOrEmpty(key) && metadata.ContainsKey(key);
    }

    private static int MetadataInt(
        IReadOnlyDictionary<string, object> metadata,
        string key,
        int fallback
    )
    {
        if (!HasMetadataKey(metadata, key))
            return fallback;
        object value = metadata[key];
        return value switch
        {
            int intValue => intValue,
            long longValue => (int)longValue,
            float floatValue => (int)floatValue,
            double doubleValue => (int)doubleValue,
            string text when int.TryParse(text, out int parsed) => parsed,
            _ => fallback,
        };
    }

    private static string MetadataString(
        IReadOnlyDictionary<string, object> metadata,
        string key,
        string fallback
    )
    {
        if (!HasMetadataKey(metadata, key))
            return fallback;
        return metadata[key]?.ToString() ?? fallback;
    }

    private static StringName MetadataStringName(
        IReadOnlyDictionary<string, object> metadata,
        string key,
        StringName fallback
    )
    {
        if (!HasMetadataKey(metadata, key))
            return fallback;
        StringName normalized = ProgressionDataUtils.to_string_name(metadata[key]);
        return IsEmpty(normalized) ? fallback : normalized;
    }

    private static Vector2I MetadataVector2I(
        IReadOnlyDictionary<string, object> metadata,
        string key,
        Vector2I fallback
    )
    {
        if (!HasMetadataKey(metadata, key))
            return fallback;
        return metadata[key] is Vector2I coord ? coord : fallback;
    }

    private static CombatEffectDefinition MetadataCombatEffectDefinition(
        IReadOnlyDictionary<string, object> metadata,
        string key
    )
    {
        if (!HasMetadataKey(metadata, key))
            return null;
        return metadata[key] is CombatEffectDefinition effectDefinition ? effectDefinition : null;
    }

    private static List<StringName> ReadMetadataStringNameList(
        IReadOnlyDictionary<string, object> metadata,
        string key
    )
    {
        if (!HasMetadataKey(metadata, key))
            return new List<StringName>();
        return metadata[key] is IEnumerable<StringName> values
            ? CopyStringNameArray(values)
            : new List<StringName>();
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
