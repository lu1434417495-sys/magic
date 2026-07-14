using System.Collections.Generic;
using Godot;

/// <summary>
/// 技能内容的只读运行时门面。它不扫描 <c>data/configs/skills</c>，而是从所属
/// <see cref="GameContentCatalog"/> 的 plain C# <see cref="SkillDefinition"/> 快照读取，并按 catalog
/// revision 缓存 derived effective combat definition，因此天然随 catalog 的 clear/rebuild 失效。
/// </summary>
public interface ISkillCatalog
{
    /// <summary>底层 <see cref="GameContentCatalog"/> 快照版本号，供下游做有效性 / 版本校验。</summary>
    long GetRevision();

    /// <summary>当前 plain C# runtime skill definition 快照。</summary>
    IReadOnlyDictionary<StringName, SkillDefinition> GetSkillDefinitionsTyped();

    bool HasSkill(StringName skillId);

    bool TryGetSkillDefinition(StringName skillId, out SkillDefinition skillDefinition);

    /// <summary>
    /// 技能在指定等级下的 plain C# 有效战斗配置。结果按 catalog revision + skill id + level 缓存；
    /// 技能不存在或无 combat profile 时返回安全默认快照。
    /// </summary>
    SkillEffectiveCombatDefinition GetEffectiveCombatDefinition(StringName skillId, int skillLevel);

    CombatSkillResourceCosts GetEffectiveResourceCostValues(StringName skillId, int skillLevel);

    int GetEffectiveAttackRollBonus(StringName skillId, int skillLevel);

    StringName GetEffectiveAreaPattern(StringName skillId, int skillLevel);

    int GetEffectiveAreaValue(StringName skillId, int skillLevel);

    int GetEffectiveRangeValue(StringName skillId, int skillLevel);

    int GetEffectiveMaxTargetCount(StringName skillId, int skillLevel);

    IReadOnlyList<CombatCastVariantDefinition> GetUnlockedCastVariantDefinitions(
        StringName skillId,
        int skillLevel
    );
}
