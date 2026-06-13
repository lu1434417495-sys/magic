using System.Collections.Generic;
using Godot;

/// <summary>
/// 技能内容的只读门面。它不扫描 <c>data/configs/skills</c>，而是从所属
/// <see cref="GameContentCatalog"/> 的 typed 快照读取，并按 catalog revision 缓存 derived effective
/// combat profile，因此天然随 catalog 的 clear/rebuild 失效。
///
/// 查询 missing skill / 无 combat profile 时返回安全默认值（<c>false</c> / <c>null</c> /
/// <see cref="CombatSkillResourceCosts.Zero"/> / <c>0</c> / 空集合），而不是回退到旧的 string-key 查找。
/// </summary>
public interface ISkillCatalog
{
    /// <summary>底层 <see cref="GameContentCatalog"/> 快照版本号，供下游做有效性 / 版本校验。</summary>
    long GetRevision();

    /// <summary>当前 typed skill 快照只读视图（与 catalog 共享同一份防御性只读包装）。</summary>
    IReadOnlyDictionary<StringName, SkillDef> GetSkillDefsTyped();

    bool HasSkill(StringName skillId);

    bool TryGetSkillDef(StringName skillId, out SkillDef skillDef);

    /// <summary>技能的战斗剖面；技能不存在或无 combat profile 时返回 <c>null</c>。</summary>
    CombatSkillDef GetCombatProfileTyped(StringName skillId);

    /// <summary>
    /// 技能在指定等级下的有效战斗配置。结果按 catalog revision + skill id + level 缓存；
    /// 技能不存在或无 combat profile 时返回安全默认快照。
    /// </summary>
    SkillEffectiveCombatProfile GetEffectiveCombatProfile(StringName skillId, int skillLevel);

    CombatSkillResourceCosts GetEffectiveResourceCostValues(StringName skillId, int skillLevel);

    int GetEffectiveAttackRollBonus(StringName skillId, int skillLevel);

    StringName GetEffectiveAreaPattern(StringName skillId, int skillLevel);

    int GetEffectiveAreaValue(StringName skillId, int skillLevel);

    int GetEffectiveRangeValue(StringName skillId, int skillLevel);

    int GetEffectiveMaxTargetCount(StringName skillId, int skillLevel);

    IReadOnlyList<CombatCastVariantDef> GetUnlockedCastVariants(StringName skillId, int skillLevel);
}
