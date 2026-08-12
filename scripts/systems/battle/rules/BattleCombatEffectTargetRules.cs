using System;
using System.Collections.Generic;
using Godot;

internal static class BattleCombatEffectTargetRules
{
    private static readonly StringName HpMaxAttributeId =
        AttributeService.ToStringName(AttributeIdKind.HpMax);

    internal static int ResolveHealToHpPercentFloorAmount(
        BattleUnitState targetUnit,
        int hpPercentFloor
    )
    {
        if (targetUnit == null || hpPercentFloor <= 0 || hpPercentFloor > 100)
        {
            return 0;
        }
        int maxHp = targetUnit.attribute_snapshot?.GetValue(HpMaxAttributeId) ?? 0;
        if (maxHp <= 0)
        {
            return 0;
        }
        long floorHp = ((long)maxHp * hpPercentFloor + 99L) / 100L;
        long missingToFloor = floorHp - Math.Max(targetUnit.GetCurrentHp(), 0);
        return (int)Math.Clamp(missingToFloor, 0L, int.MaxValue);
    }

    internal static int ResolveHealMissingHpPercentAmount(
        BattleUnitState targetUnit,
        int missingHpPercent
    )
    {
        if (targetUnit == null || missingHpPercent <= 0 || missingHpPercent > 100)
        {
            return 0;
        }
        int maxHp = targetUnit.attribute_snapshot?.GetValue(HpMaxAttributeId) ?? 0;
        if (maxHp <= 0)
        {
            return 0;
        }
        long missingHp = Math.Max((long)maxHp - Math.Max(targetUnit.GetCurrentHp(), 0), 0L);
        return (int)Math.Clamp(missingHp * missingHpPercent / 100L, 0L, int.MaxValue);
    }

    internal static int GetHpPercentBasisPoints(BattleUnitState unit)
    {
        if (unit == null)
        {
            return 10000;
        }
        int maxHp = unit.attribute_snapshot?.GetValue(HpMaxAttributeId) ?? 0;
        return ResolveHpPercentBasisPoints(unit.GetCurrentHp(), maxHp);
    }

    internal static int GetHpPercentBasisPoints(BattleUnitReadView unit)
    {
        return unit.IsValid
            ? ResolveHpPercentBasisPoints(
                unit.CurrentHp,
                unit.GetAttributeValue(HpMaxAttributeId)
            )
            : 10000;
    }

    internal static IReadOnlyList<BattleUnitState> SelectTargets(
        CombatEffectDefinition effectDefinition,
        BattleUnitState sourceUnit,
        IReadOnlyList<BattleUnitState> candidates
    )
    {
        var selected = new List<BattleUnitState>();
        if (effectDefinition == null)
        {
            return selected;
        }
        StringName sourceUnitId = sourceUnit?.unit_id ?? "";
        foreach (BattleUnitState candidate in candidates ?? Array.Empty<BattleUnitState>())
        {
            if (
                candidate == null
                || (
                    effectDefinition.ExcludeSource
                    && sourceUnitId != ""
                    && candidate.unit_id == sourceUnitId
                )
            )
            {
                continue;
            }
            selected.Add(candidate);
        }
        ApplyOrderAndLimit(effectDefinition, selected);
        return selected;
    }

    internal static IReadOnlyList<BattleUnitReadView> SelectTargets(
        CombatEffectDefinition effectDefinition,
        BattleUnitReadView sourceUnit,
        IReadOnlyList<BattleUnitReadView> candidates
    )
    {
        var selected = new List<BattleUnitReadView>();
        if (effectDefinition == null)
        {
            return selected;
        }
        StringName sourceUnitId = sourceUnit.IsValid ? sourceUnit.UnitId : "";
        foreach (
            BattleUnitReadView candidate in candidates
                ?? Array.Empty<BattleUnitReadView>()
        )
        {
            if (
                !candidate.IsValid
                || (
                    effectDefinition.ExcludeSource
                    && sourceUnitId != ""
                    && candidate.UnitId == sourceUnitId
                )
            )
            {
                continue;
            }
            selected.Add(candidate);
        }
        ApplyOrderAndLimit(effectDefinition, selected);
        return selected;
    }

    private static int ResolveHpPercentBasisPoints(int currentHp, int maxHp)
    {
        if (maxHp <= 0)
        {
            return 10000;
        }
        long basisPoints = (long)Math.Max(currentHp, 0) * 10000L / maxHp;
        return (int)Math.Clamp(basisPoints, 0L, 10000L);
    }

    private static void ApplyOrderAndLimit(
        CombatEffectDefinition effectDefinition,
        List<BattleUnitState> selected
    )
    {
        if (effectDefinition.TargetOrderKind == CombatEffectTargetOrder.Unknown)
        {
            selected.Clear();
            return;
        }
        if (effectDefinition.MaxAffectedTargets > 0)
        {
            if (
                effectDefinition.TargetOrderKind
                != CombatEffectTargetOrder.LowestHpPercentThenUnitId
            )
            {
                selected.Clear();
                return;
            }
            selected.Sort(CompareTargets);
            if (selected.Count > effectDefinition.MaxAffectedTargets)
            {
                selected.RemoveRange(
                    effectDefinition.MaxAffectedTargets,
                    selected.Count - effectDefinition.MaxAffectedTargets
                );
            }
        }
    }

    private static void ApplyOrderAndLimit(
        CombatEffectDefinition effectDefinition,
        List<BattleUnitReadView> selected
    )
    {
        if (effectDefinition.TargetOrderKind == CombatEffectTargetOrder.Unknown)
        {
            selected.Clear();
            return;
        }
        if (effectDefinition.MaxAffectedTargets > 0)
        {
            if (
                effectDefinition.TargetOrderKind
                != CombatEffectTargetOrder.LowestHpPercentThenUnitId
            )
            {
                selected.Clear();
                return;
            }
            selected.Sort(CompareTargets);
            if (selected.Count > effectDefinition.MaxAffectedTargets)
            {
                selected.RemoveRange(
                    effectDefinition.MaxAffectedTargets,
                    selected.Count - effectDefinition.MaxAffectedTargets
                );
            }
        }
    }

    private static int CompareTargets(BattleUnitState left, BattleUnitState right)
    {
        int hpPercentOrder = GetHpPercentBasisPoints(left).CompareTo(
            GetHpPercentBasisPoints(right)
        );
        return hpPercentOrder != 0
            ? hpPercentOrder
            : string.Compare(
                left?.unit_id.ToString() ?? "",
                right?.unit_id.ToString() ?? "",
                StringComparison.Ordinal
            );
    }

    private static int CompareTargets(BattleUnitReadView left, BattleUnitReadView right)
    {
        int hpPercentOrder = GetHpPercentBasisPoints(left).CompareTo(
            GetHpPercentBasisPoints(right)
        );
        return hpPercentOrder != 0
            ? hpPercentOrder
            : string.Compare(
                left.UnitId.ToString(),
                right.UnitId.ToString(),
                StringComparison.Ordinal
            );
    }
}
