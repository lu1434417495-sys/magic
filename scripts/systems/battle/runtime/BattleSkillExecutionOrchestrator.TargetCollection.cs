using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

// Partial slice of BattleSkillExecutionOrchestrator — unit/coord collection + ground cast target-shape validation.
// Pure physical split: same class, no behavior change. See BattleSkillExecutionOrchestrator.cs.
internal sealed partial class BattleSkillExecutionOrchestrator
{

    internal List<CombatEffectDefinition> CollectUnitSkillEffectDefinitions(
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant,
        BattleUnitState active_unit = null
    )
    {
        return Runtime?._skill_resolution_rules != null
            ? Runtime._skill_resolution_rules.CollectUnitSkillEffectDefinitions(
                skillDefinition,
                castVariant,
                active_unit
            )
            : new List<CombatEffectDefinition>();
    }

    internal List<CombatEffectDefinition> CollectUnitSkillEffectDefinitions(
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant,
        BattleUnitReadView active_unit
    )
    {
        return Runtime?._skill_resolution_rules != null
            ? Runtime._skill_resolution_rules.CollectUnitSkillEffectDefinitions(
                skillDefinition,
                castVariant,
                active_unit
            )
            : new List<CombatEffectDefinition>();
    }

    internal IReadOnlyList<BattleUnitState> CollectUnitsInCoords(
        IReadOnlyList<Vector2I> effectCoords
    )
    {
        var units = new List<BattleUnitState>();
        HashSet<StringName> seenUnitIds = new();
        BattleGridService gridService = Runtime?._grid_service;
        foreach (Vector2I effectCoord in effectCoords ?? Array.Empty<Vector2I>())
        {
            BattleUnitState targetUnit = gridService?.GetUnitAtCoord(
                Runtime?._state,
                effectCoord
            );
            if (
                targetUnit == null
                || !targetUnit.IsAlive()
                || seenUnitIds.Contains(targetUnit.unit_id)
            )
            {
                continue;
            }
            seenUnitIds.Add(targetUnit.unit_id);
            units.Add(targetUnit);
        }
        return units;
    }

    internal IReadOnlyList<BattleUnitReadView> CollectUnitsInCoordsReadView(
        IReadOnlyList<Vector2I> effectCoords
    )
    {
        var units = new List<BattleUnitReadView>();
        foreach (BattleUnitState unitState in CollectUnitsInCoords(effectCoords))
        {
            units.Add(new BattleUnitReadView(unitState));
        }
        return units;
    }

    internal IReadOnlyList<BattleUnitState> _collect_units_in_coords_typed(GVector2IArray effect_coords)
    {
        return CollectUnitsInCoords(ToVector2IList(effect_coords));
    }

    internal StringName ResolveEffectTargetFilter(
        SkillDefinition skillDefinition,
        CombatEffectDefinition effectDefinition
    )
    {
        StringName resolved =
            Runtime?._skill_resolution_rules?.ResolveEffectTargetFilter(
                skillDefinition,
                effectDefinition
            ) ?? new StringName("");
        if (!StringNameIsEmpty(resolved))
        {
            return resolved;
        }
        StringName effectTargetFilter = effectDefinition?.EffectTargetTeamFilter ?? new StringName("");
        if (!StringNameIsEmpty(effectTargetFilter))
        {
            return effectTargetFilter;
        }
        return skillDefinition?.CombatProfile?.TargetTeamFilter ?? new StringName("");
    }

    internal bool _is_unit_valid_for_effect(
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

    internal bool _is_unit_valid_for_effect(
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

    internal IReadOnlyDictionary<CombatEffectDefinition, IReadOnlyList<BattleUnitState>>
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

    internal IReadOnlyDictionary<CombatEffectDefinition, IReadOnlyList<BattleUnitReadView>>
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

    private static BattleTargetMode ResolveGroundCastTargetMode(
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition
    )
    {
        if (castVariantDefinition == null)
        {
            return BattleTargetMode.Unknown;
        }
        BattleTargetMode targetMode = castVariantDefinition.TargetModeKind;
        return targetMode != BattleTargetMode.Unknown
            ? targetMode
            : skillDefinition?.CombatProfile?.TargetModeKind ?? BattleTargetMode.Unknown;
    }

    private static int ResolveGroundRequiredCoordCount(
        CombatCastVariantDefinition castVariantDefinition
    )
    {
        return Math.Max(castVariantDefinition?.RequiredCoordCount ?? 0, 0);
    }

    private static CombatCastFootprintPattern ResolveGroundFootprintPattern(
        CombatCastVariantDefinition castVariantDefinition
    )
    {
        return castVariantDefinition?.FootprintPatternKind ?? CombatCastFootprintPattern.Unknown;
    }

    private static bool ValidateTargetCoordsShapeTyped(
        CombatCastFootprintPattern footprintPattern,
        IReadOnlyList<Vector2I> targetCoords
    )
    {
        if (footprintPattern == CombatCastFootprintPattern.Single)
        {
            return targetCoords != null && targetCoords.Count == 1;
        }
        if (footprintPattern == CombatCastFootprintPattern.Line2)
        {
            if (targetCoords == null || targetCoords.Count != 2)
            {
                return false;
            }
            Vector2I first = targetCoords[0];
            Vector2I second = targetCoords[1];
            return (first.X == second.X && Math.Abs(first.Y - second.Y) == 1)
                || (first.Y == second.Y && Math.Abs(first.X - second.X) == 1);
        }
        if (footprintPattern == CombatCastFootprintPattern.Square2)
        {
            if (targetCoords == null || targetCoords.Count != 4)
            {
                return false;
            }
            Vector2I firstCoord = targetCoords[0];
            int minX = firstCoord.X;
            int maxX = firstCoord.X;
            int minY = firstCoord.Y;
            int maxY = firstCoord.Y;
            var coordSet = new HashSet<Vector2I>();
            foreach (Vector2I coord in targetCoords)
            {
                minX = Math.Min(minX, coord.X);
                maxX = Math.Max(maxX, coord.X);
                minY = Math.Min(minY, coord.Y);
                maxY = Math.Max(maxY, coord.Y);
                coordSet.Add(coord);
            }
            if (maxX - minX != 1 || maxY - minY != 1)
            {
                return false;
            }
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    if (!coordSet.Contains(new Vector2I(x, y)))
                    {
                        return false;
                    }
                }
            }
            return true;
        }
        if (footprintPattern == CombatCastFootprintPattern.Unordered)
        {
            return targetCoords != null && targetCoords.Count > 0;
        }
        return false;
    }

    internal int _get_unit_skill_level(BattleUnitState unit_state, StringName skill_id)
    {
        if (unit_state == null || StringNameIsEmpty(skill_id))
        {
            return 0;
        }
        if (
            unit_state.unit_id == _scopedAutoCastUnitId
            && skill_id == _scopedAutoCastSkillId
            && _scopedAutoCastSkillLevel > 0
        )
        {
            return _scopedAutoCastSkillLevel;
        }
        if (
            unit_state.unit_id == _scopedCommandSkillUnitId
            && skill_id == _scopedCommandSkillId
            && _scopedCommandSkillLevel > 0
        )
        {
            return _scopedCommandSkillLevel;
        }
        if (unit_state.HasKnownSkillLevelTyped(skill_id))
        {
            return unit_state.GetKnownSkillLevelTyped(skill_id);
        }
        if (_runtime != null)
        {
            SkillDefinition skillDefinition = Runtime?.GetSkillDefinitionTyped(skill_id);
            if (
                skillDefinition != null
                && skillDefinition.MaxLevel == 0
                && StringNameIsEmpty(skillDefinition.DynamicMaxLevelStatId)
            )
            {
                return 0;
            }
        }
        return unit_state.KnowsActiveSkill(skill_id) ? 1 : 0;
    }

    internal string _format_skill_variant_label(
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
}
