using System;
using System.Collections.Generic;
using Godot;

internal static class BattleEquipmentAbilityBonusDamageReplacementRules
{
    internal static void AddCandidateBundle(
        List<BattleEquipmentAbilityBonusDamageDiceResult> results,
        IReadOnlyList<BattleEquipmentAbilityBonusDamageDiceResult> candidateBundle
    )
    {
        if (results == null || candidateBundle == null || candidateBundle.Count == 0)
            return;

        var candidates = new List<BattleEquipmentAbilityBonusDamageDiceResult>(
            candidateBundle.Count
        );
        foreach (BattleEquipmentAbilityBonusDamageDiceResult candidate in candidateBundle)
        {
            if (candidate != null)
                candidates.Add(candidate);
        }
        if (candidates.Count == 0)
            return;

        BattleEquipmentAbilityBonusDamageDiceResult leader = candidates[0];
        StringName groupId = ProgressionDataUtils.to_string_name(
            leader.ReplacementGroupId
        );
        if (groupId == "")
        {
            results.AddRange(candidates);
            return;
        }

        if (!IsSingleActionBundle(candidates, leader, groupId))
        {
            foreach (BattleEquipmentAbilityBonusDamageDiceResult candidate in candidates)
            {
                AddCandidateBundle(
                    results,
                    new[] { candidate }
                );
            }
            return;
        }

        BattleEquipmentAbilityBonusDamageDiceResult incumbent = null;
        foreach (BattleEquipmentAbilityBonusDamageDiceResult existing in results)
        {
            if (
                existing != null
                && ProgressionDataUtils.to_string_name(existing.ReplacementGroupId)
                    == groupId
            )
            {
                incumbent = existing;
                break;
            }
        }
        if (incumbent == null)
        {
            results.AddRange(candidates);
            return;
        }

        int comparison = CompareCandidates(leader, incumbent);
        if (comparison >= 0)
            return;

        results.RemoveAll(
            value =>
                value != null
                && ProgressionDataUtils.to_string_name(value.ReplacementGroupId)
                    == groupId
        );
        results.AddRange(candidates);
    }

    private static bool IsSingleActionBundle(
        IReadOnlyList<BattleEquipmentAbilityBonusDamageDiceResult> candidates,
        BattleEquipmentAbilityBonusDamageDiceResult leader,
        StringName groupId
    )
    {
        foreach (BattleEquipmentAbilityBonusDamageDiceResult candidate in candidates)
        {
            if (
                candidate.BindingId != leader.BindingId
                || candidate.ActionId != leader.ActionId
                || candidate.ReplacementPriority != leader.ReplacementPriority
                || ProgressionDataUtils.to_string_name(candidate.ReplacementGroupId)
                    != groupId
            )
            {
                return false;
            }
        }
        return true;
    }

    private static int CompareCandidates(
        BattleEquipmentAbilityBonusDamageDiceResult left,
        BattleEquipmentAbilityBonusDamageDiceResult right
    )
    {
        int priorityComparison = right.ReplacementPriority.CompareTo(
            left.ReplacementPriority
        );
        if (priorityComparison != 0)
            return priorityComparison;
        int bindingComparison = string.CompareOrdinal(
            left.BindingId.ToString(),
            right.BindingId.ToString()
        );
        if (bindingComparison != 0)
            return bindingComparison;
        return string.CompareOrdinal(
            left.ActionId.ToString(),
            right.ActionId.ToString()
        );
    }
}
