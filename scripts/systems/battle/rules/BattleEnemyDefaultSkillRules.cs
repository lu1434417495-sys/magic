using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// 敌方单位没有配置任何主动技能时的兜底：优先 gameplay configuration 指定的基础攻击，
/// 否则按技能 id 的序数顺序取第一个可以对敌方单体使用的主动技能。不按具体技能 id 做特例。
/// </summary>
internal static class BattleEnemyDefaultSkillRules
{
    internal static IReadOnlyList<StringName> PickDefaultSkillIds(
        StringName basicAttackSkillId,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions
    )
    {
        if (skillDefinitions == null || skillDefinitions.Count == 0)
            return Array.Empty<StringName>();
        if (
            basicAttackSkillId != null
            && basicAttackSkillId != ""
            && skillDefinitions.TryGetValue(basicAttackSkillId, out SkillDefinition basicAttack)
            && IsValidDefaultSkill(basicAttack)
        )
        {
            return new[] { basicAttackSkillId };
        }

        var sortedSkillIds = new List<string>();
        foreach (StringName skillId in skillDefinitions.Keys)
        {
            if (skillId != null && skillId != "")
                sortedSkillIds.Add(skillId.ToString());
        }
        sortedSkillIds.Sort(StringComparer.Ordinal);
        foreach (string skillId in sortedSkillIds)
        {
            var normalizedSkillId = new StringName(skillId);
            if (
                skillDefinitions.TryGetValue(normalizedSkillId, out SkillDefinition skillDefinition)
                && IsValidDefaultSkill(skillDefinition)
            )
            {
                return new[] { normalizedSkillId };
            }
        }
        return Array.Empty<StringName>();
    }

    private static bool IsValidDefaultSkill(SkillDefinition skillDefinition)
    {
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        return skillDefinition != null
            && skillDefinition.SkillTypeKind == SkillTypeKind.Active
            && skillDefinition.CanUseInCombat()
            && combatProfile != null
            && combatProfile.TargetModeKind == BattleTargetMode.Unit
            && combatProfile.TargetFilterKind == BattleTargetFilter.Enemy;
    }
}
