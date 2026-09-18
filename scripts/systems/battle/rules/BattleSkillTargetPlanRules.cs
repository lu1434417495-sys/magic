using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;
using static BattleSkillTargetPlanRules;

internal static class BattleSkillTargetPlanRules
{
    internal static bool _is_unit_valid_for_effect(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        StringName target_team_filter,
        bool allow_dead_targets = false
    )
    {
        bool madnessAnyTeam = source_unit?.ai_blackboard?.madness_target_any_team == true;
        return BattleTargetTeamRules.IsUnitValidForFilter(
            source_unit,
            target_unit,
            target_team_filter,
            new BattleTargetTeamRules.TargetFilterOptions(
                AllowDeadTargets: allow_dead_targets,
                MadnessTargetAnyTeam: madnessAnyTeam
            )
        );
    }

    internal static bool _is_unit_valid_for_effect(
        BattleUnitReadView source_unit,
        BattleUnitReadView target_unit,
        StringName target_team_filter,
        bool allow_dead_targets = false
    )
    {
        bool madnessAnyTeam = source_unit.IsValid && source_unit.MadnessTargetAnyTeam;
        return BattleTargetTeamRules.IsUnitValidForFilter(
            source_unit,
            target_unit,
            target_team_filter,
            new BattleTargetTeamRules.TargetFilterOptions(
                AllowDeadTargets: allow_dead_targets,
                MadnessTargetAnyTeam: madnessAnyTeam
            )
        );
    }

    internal static IReadOnlyDictionary<CombatEffectDefinition, IReadOnlyList<BattleUnitState>>
        BuildUnitEffectTargetPlan(
            BattleUnitState sourceUnit,
            SkillDefinition skillDefinition,
            IReadOnlyList<CombatEffectDefinition> effectDefinitions,
            IReadOnlyList<BattleUnitState> candidateUnits
        )
    {
        var plan = new Dictionary<CombatEffectDefinition, IReadOnlyList<BattleUnitState>>();
        foreach (
            CombatEffectDefinition effectDefinition in effectDefinitions
                ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (effectDefinition == null || plan.ContainsKey(effectDefinition))
            {
                continue;
            }
            StringName targetFilter = ResolveEffectTargetFilter(
                skillDefinition,
                effectDefinition
            );
            var eligibleUnits = new List<BattleUnitState>();
            foreach (BattleUnitState candidateUnit in candidateUnits ?? Array.Empty<BattleUnitState>())
            {
                if (
                    candidateUnit != null
                    && _is_unit_valid_for_effect(
                        sourceUnit,
                        candidateUnit,
                        targetFilter,
                        allow_dead_targets: BattleEffectTargetRequirementRules
                            .AllowsDeadUnitTarget(effectDefinition)
                    )
                    && BattleEffectTargetRequirementRules.IsSatisfied(
                        effectDefinition,
                        candidateUnit
                    )
                )
                {
                    eligibleUnits.Add(candidateUnit);
                }
            }
            plan[effectDefinition] = BattleCombatEffectTargetRules.SelectTargets(
                effectDefinition,
                sourceUnit,
                eligibleUnits
            );
        }
        return plan;
    }

    internal static IReadOnlyDictionary<CombatEffectDefinition, IReadOnlyList<BattleUnitReadView>>
        BuildUnitEffectTargetPlan(
            BattleUnitReadView sourceUnit,
            SkillDefinition skillDefinition,
            IReadOnlyList<CombatEffectDefinition> effectDefinitions,
            IReadOnlyList<BattleUnitReadView> candidateUnits
        )
    {
        var plan = new Dictionary<CombatEffectDefinition, IReadOnlyList<BattleUnitReadView>>();
        foreach (
            CombatEffectDefinition effectDefinition in effectDefinitions
                ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (effectDefinition == null || plan.ContainsKey(effectDefinition))
            {
                continue;
            }
            StringName targetFilter = ResolveEffectTargetFilter(
                skillDefinition,
                effectDefinition
            );
            var eligibleUnits = new List<BattleUnitReadView>();
            foreach (
                BattleUnitReadView candidateUnit in candidateUnits
                    ?? Array.Empty<BattleUnitReadView>()
            )
            {
                if (
                    candidateUnit.IsValid
                    && _is_unit_valid_for_effect(
                        sourceUnit,
                        candidateUnit,
                        targetFilter,
                        allow_dead_targets: BattleEffectTargetRequirementRules
                            .AllowsDeadUnitTarget(effectDefinition)
                    )
                    && BattleEffectTargetRequirementRules.IsSatisfied(
                        effectDefinition,
                        candidateUnit
                    )
                )
                {
                    eligibleUnits.Add(candidateUnit);
                }
            }
            plan[effectDefinition] = BattleCombatEffectTargetRules.SelectTargets(
                effectDefinition,
                sourceUnit,
                eligibleUnits
            );
        }
        return plan;
    }

    internal static IReadOnlyList<BattleUnitState> CollectPlannedTargets(
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        IReadOnlyDictionary<CombatEffectDefinition, IReadOnlyList<BattleUnitState>> plan
    )
    {
        var result = new List<BattleUnitState>();
        var seenIds = new HashSet<StringName>();
        foreach (
            CombatEffectDefinition effectDefinition in effectDefinitions
                ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (
                effectDefinition == null
                || plan == null
                || !plan.TryGetValue(effectDefinition, out IReadOnlyList<BattleUnitState> targets)
            )
            {
                continue;
            }
            foreach (BattleUnitState target in targets ?? Array.Empty<BattleUnitState>())
            {
                if (target != null && seenIds.Add(target.unit_id))
                {
                    result.Add(target);
                }
            }
        }
        return result;
    }

    internal static IReadOnlyList<BattleUnitReadView> CollectPlannedTargets(
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        IReadOnlyDictionary<CombatEffectDefinition, IReadOnlyList<BattleUnitReadView>> plan
    )
    {
        var result = new List<BattleUnitReadView>();
        var seenIds = new HashSet<StringName>();
        foreach (
            CombatEffectDefinition effectDefinition in effectDefinitions
                ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (
                effectDefinition == null
                || plan == null
                || !plan.TryGetValue(effectDefinition, out IReadOnlyList<BattleUnitReadView> targets)
            )
            {
                continue;
            }
            foreach (
                BattleUnitReadView target in targets
                    ?? Array.Empty<BattleUnitReadView>()
            )
            {
                if (target.IsValid && seenIds.Add(target.UnitId))
                {
                    result.Add(target);
                }
            }
        }
        return result;
    }

    internal static IReadOnlyList<BattleUnitState> CollectPlannedTargetsOrValidatedTargets(
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        IReadOnlyDictionary<CombatEffectDefinition, IReadOnlyList<BattleUnitState>> plan,
        IReadOnlyList<BattleUnitState> validatedTargets
    )
    {
        if (effectDefinitions == null || effectDefinitions.Count == 0)
        {
            return validatedTargets ?? Array.Empty<BattleUnitState>();
        }
        return CollectPlannedTargets(effectDefinitions, plan);
    }

    internal static IReadOnlyList<CombatEffectDefinition> CollectPlannedEffectsForTarget(
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        IReadOnlyDictionary<CombatEffectDefinition, IReadOnlyList<BattleUnitState>> plan,
        StringName targetUnitId
    )
    {
        var result = new List<CombatEffectDefinition>();
        foreach (
            CombatEffectDefinition effectDefinition in effectDefinitions
                ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (
                effectDefinition == null
                || plan == null
                || !plan.TryGetValue(effectDefinition, out IReadOnlyList<BattleUnitState> targets)
            )
            {
                continue;
            }
            foreach (BattleUnitState target in targets ?? Array.Empty<BattleUnitState>())
            {
                if (target?.unit_id == targetUnitId)
                {
                    result.Add(effectDefinition);
                    break;
                }
            }
        }
        return result;
    }

    internal static IReadOnlyList<CombatEffectDefinition> CollectPlannedEffectsForTarget(
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        IReadOnlyDictionary<CombatEffectDefinition, IReadOnlyList<BattleUnitReadView>> plan,
        StringName targetUnitId
    )
    {
        var result = new List<CombatEffectDefinition>();
        foreach (
            CombatEffectDefinition effectDefinition in effectDefinitions
                ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (
                effectDefinition == null
                || plan == null
                || !plan.TryGetValue(effectDefinition, out IReadOnlyList<BattleUnitReadView> targets)
            )
            {
                continue;
            }
            foreach (
                BattleUnitReadView target in targets
                    ?? Array.Empty<BattleUnitReadView>()
            )
            {
                if (target.IsValid && target.UnitId == targetUnitId)
                {
                    result.Add(effectDefinition);
                    break;
                }
            }
        }
        return result;
    }

    internal static string _format_skill_variant_label(
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant
    )
    {
        if (skillDefinition == null)
        {
            return "";
        }
        if (
            castVariant == null
            || string.IsNullOrEmpty(castVariant.DisplayName)
        )
        {
            return skillDefinition.DisplayName;
        }
        return $"{skillDefinition.DisplayName}·{castVariant.DisplayName}";
    }

    internal static StringName ResolveEffectTargetFilter(SkillDefinition skill, CombatEffectDefinition effect) => BattleTargetTeamRules.ResolveEffectTargetFilter(skill, effect);

    internal static List<Vector2I> SortCoordsTyped(IEnumerable<Vector2I> coords)
    {
        var result = new List<Vector2I>(coords ?? Array.Empty<Vector2I>());
        result.Sort((a, b) => a.Y == b.Y ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));
        return result;
    }

    internal static bool StringNameIsEmpty(StringName value)
    {
        return value == null || value.ToString().Length == 0;
    }

    internal static int GetUnitMaxHp(BattleUnitState unitState)
    {
        if (unitState == null)
        {
            return 0;
        }
        int snapshotMaxHp =
            unitState.attribute_snapshot?.GetValue(AttributeService.ToStringName(AttributeIdKind.HpMax)) ?? 0;
        return Math.Max(snapshotMaxHp, unitState.GetCurrentHp());
    }

    internal static IReadOnlyList<CombatEffectDefinition> BuildLineThroughAttackEffects(
        IEnumerable<CombatEffectDefinition> effects,
        int weaponDiceMultiplier
    )
    {
        var result = new List<CombatEffectDefinition>();
        foreach (
            CombatEffectDefinition effect in effects ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (effect == null)
                continue;
            result.Add(
                effect.EffectKind == BattleEffectKind.Damage
                    ? effect.WithWeaponDiceMultiplier(Math.Max(weaponDiceMultiplier, 1))
                    : effect
            );
        }
        return result.AsReadOnly();
    }

    internal static string FormatSignedBonus(int value) =>
        value > 0 ? $"+{value}" : value.ToString();

    internal static string FormatDirection(Vector2I direction)
    {
        if (direction == Vector2I.Up)
            return "上";
        if (direction == Vector2I.Down)
            return "下";
        if (direction == Vector2I.Left)
            return "左";
        if (direction == Vector2I.Right)
            return "右";
        return "未知";
    }
}
