using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

internal sealed record BattleStatusTickAdvanceStep(
    BattleStatusSourceIdentity Source,
    int DueTu,
    int IntervalTu,
    int DiceCount,
    int DiceSides,
    int FlatDamage,
    StringName DamageTag
);

internal static class BattleStatusTickAdvanceRules
{
    internal static IReadOnlyList<BattleStatusTickAdvanceStep> BuildPlan(
        BattleUnitState target,
        CombatEffectDefinition effect,
        int currentTu,
        IReadOnlyDictionary<StringName, SkillDefinition> skills
    )
    {
        if (target?.IsAlive() != true
            || effect?.Payload is not AdvanceStatusTicksEffectPayloadDefinition config
            || config.MaxTicks is < 1 or > 32 || config.MaxSources is < 1 or > 8)
            return Array.Empty<BattleStatusTickAdvanceStep>();
        BattleStatusEffectState status = target.GetStatusEffect(effect.StatusId);
        if (status?.HasSourceContributionsTyped() != true)
            return Array.Empty<BattleStatusTickAdvanceStep>();
        // Near-expiry sources first, then chronological ticks. Counts are shared
        // across sources; extra sources never multiply the action's tick budget.
        var sources = status.GetSourceContributionsTyped()
            .Where(c => c.IsValid && c.DurationTu > 0 && c.TickIntervalTu > 0
                && HasSourceTag(c, config.RequiredSourceTag, skills)
                && NextDue(c, currentTu) <= (long)currentTu + c.DurationTu)
            .OrderBy(c => c.DurationTu)
            .ThenBy(c => c.Identity.StableKey, StringComparer.Ordinal)
            .Take(config.MaxSources);
        var steps = new List<BattleStatusTickAdvanceStep>();
        foreach (var source in sources)
        {
            long next = NextDue(source, currentTu);
            long expires = (long)currentTu + source.DurationTu;
            for (int n = 0; n < config.MaxTicks && next <= expires && next <= int.MaxValue - source.TickIntervalTu; n++, next += source.TickIntervalTu)
            {
                bool hasDice = source.TimelineDamageDiceCount > 0 && source.TimelineDamageDiceSides > 0;
                steps.Add(new(source.Identity, (int)next, source.TickIntervalTu,
                    hasDice ? source.TimelineDamageDiceCount : 0,
                    hasDice ? source.TimelineDamageDiceSides : 0,
                    hasDice ? Math.Max(source.TimelineDamageFlatBonus, 0) : Math.Max(Math.Max(source.Power, source.Stacks), 1),
                    source.DamageTag));
            }
        }
        return steps.OrderBy(s => s.DueTu)
            .ThenBy(s => s.Source.StableKey, StringComparer.Ordinal)
            .Take(config.MaxTicks).ToArray();
    }

    private static long NextDue(BattleStatusSourceContributionState source, int currentTu) =>
        source.NextTickAtTu > currentTu ? source.NextTickAtTu : (long)currentTu + source.TickIntervalTu;

    private static bool HasSourceTag(BattleStatusSourceContributionState source,
        StringName requiredTag, IReadOnlyDictionary<StringName, SkillDefinition> skills)
    {
        if (requiredTag == "")
            return false;
        if (source.Identity.Kind == BattleStatusSourceKind.Skill)
            return skills != null && skills.TryGetValue(source.Identity.SourceDefinitionId, out var skill)
                && skill.HasTag(requiredTag);
        return source.DamageTag == requiredTag;
    }

    internal static bool ConsumeTick(BattleUnitState target, StringName statusId,
        BattleStatusTickAdvanceStep step, int currentTu)
    {
        BattleStatusEffectState status = target?.GetStatusEffect(statusId);
        var source = status?.GetSourceContributionTyped(step.Source);
        if (source?.IsValid != true || source.DurationTu <= 0
            || source.TickIntervalTu != step.IntervalTu
            || NextDue(source, currentTu) != step.DueTu
            || step.DueTu > (long)currentTu + source.DurationTu)
            return false;
        // Commit before damage/reactions so reentrant effects cannot consume it twice.
        source.NextTickAtTu = step.DueTu + step.IntervalTu;
        status.SetSourceContributionTyped(source);
        status.RebuildSourceContributionAggregateTyped();
        target.SetStatusEffect(status);
        return true;
    }
}
