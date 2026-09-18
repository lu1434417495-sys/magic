using System;
using System.Collections.Generic;
using Godot;

// Immutable numeric projection: no runtime units or borrowed content owners are retained.
internal sealed class EncounterChallengeCatalog
{
    private readonly Dictionary<StringName, SortedDictionary<int, double>> _stages = new();

    internal EncounterChallengeCatalog(
        IReadOnlyDictionary<StringName, BattleEncounterDefinition> encounters,
        IReadOnlyDictionary<StringName, WildEncounterRosterDefinition> rosters,
        IReadOnlyDictionary<StringName, EnemyTemplateDefinition> templates
    )
    {
        ArgumentNullException.ThrowIfNull(encounters);
        ArgumentNullException.ThrowIfNull(rosters);
        ArgumentNullException.ThrowIfNull(templates);
        foreach ((StringName id, BattleEncounterDefinition encounter) in encounters)
        {
            if (!rosters.TryGetValue(encounter.RosterProfileId, out var roster))
                continue;
            var stages = new SortedDictionary<int, double>();
            foreach (var stage in roster.Stages)
            {
                double maximum = 0;
                bool hasEnemy = false;
                foreach (var entry in stage.UnitEntries)
                {
                    if (entry.Count <= 0)
                        continue;
                    hasEnemy = true;
                    maximum = Math.Max(maximum, templates.TryGetValue(entry.TemplateId, out var template)
                        ? template.ChallengeRating : double.PositiveInfinity);
                }
                stages[stage.Stage] = hasEnemy ? maximum : double.PositiveInfinity;
            }
            _stages[id] = stages;
        }
    }

    internal double GetMaxChallengeRating(StringName encounterProfileId, int stage)
    {
        double maximum = double.PositiveInfinity;
        if (!_stages.TryGetValue(encounterProfileId, out var stages))
            return maximum;
        foreach ((int candidate, double rating) in stages)
        {
            if (candidate > stage)
                break;
            maximum = rating;
        }
        return maximum;
    }

    internal int GetHighestAllowedStage(StringName encounterProfileId, int requestedStage, double limit)
    {
        int result = -1;
        if (!_stages.TryGetValue(encounterProfileId, out var stages))
            return result;
        foreach ((int stage, double rating) in stages)
        {
            if (stage > requestedStage)
                break;
            if (rating <= limit)
                result = stage;
        }
        return result;
    }
}
