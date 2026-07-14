using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

/// <summary>
/// <see cref="ISkillCatalog"/> 的默认实现。仅持有所属 <see cref="GameContentCatalog"/> 引用，
/// 不缓存可变 skill 字典、不扫描资源：每次查询都从 <see cref="GameContentCatalog.GetSkillDefinitionsTyped"/>
/// 读取当前 runtime DTO 快照与 revision；derived effective definition 按 revision 缓存，因此随 catalog 的
/// clear/rebuild 自动失效，门面实例本身无需重建。
/// </summary>
public sealed class SkillCatalog : ISkillCatalog
{
    private readonly struct EffectiveCombatProfileCacheKey :
        System.IEquatable<EffectiveCombatProfileCacheKey>
    {
        public readonly StringName SkillId;
        public readonly int SkillLevel;

        public EffectiveCombatProfileCacheKey(StringName skillId, int skillLevel)
        {
            SkillId = skillId;
            SkillLevel = skillLevel;
        }

        public bool Equals(EffectiveCombatProfileCacheKey other) =>
            SkillId == other.SkillId && SkillLevel == other.SkillLevel;

        public override bool Equals(object obj) =>
            obj is EffectiveCombatProfileCacheKey other && Equals(other);

        public override int GetHashCode() => System.HashCode.Combine(SkillId, SkillLevel);
    }

    private static readonly IReadOnlyDictionary<StringName, SkillDefinition> EmptySkillDefinitions =
        new ReadOnlyDictionary<StringName, SkillDefinition>(
            new Dictionary<StringName, SkillDefinition>()
        );

    private readonly GameContentCatalog _contentCatalog;
    private readonly Dictionary<EffectiveCombatProfileCacheKey, SkillEffectiveCombatDefinition> _effectiveCombatDefinitionCache =
        new();
    private long _effectiveCombatCacheRevision = long.MinValue;

    internal SkillCatalog(GameContentCatalog contentCatalog)
    {
        _contentCatalog = contentCatalog;
    }

    public long GetRevision() => _contentCatalog?.GetRevision() ?? 0;

    public IReadOnlyDictionary<StringName, SkillDefinition> GetSkillDefinitionsTyped() =>
        _contentCatalog?.GetSkillDefinitionsTyped() ?? EmptySkillDefinitions;

    public bool HasSkill(StringName skillId) =>
        skillId != "" && GetSkillDefinitionsTyped().ContainsKey(skillId);

    public bool TryGetSkillDefinition(StringName skillId, out SkillDefinition skillDefinition)
    {
        skillDefinition = null;
        return skillId != ""
            && GetSkillDefinitionsTyped().TryGetValue(skillId, out skillDefinition);
    }

    public SkillEffectiveCombatDefinition GetEffectiveCombatDefinition(
        StringName skillId,
        int skillLevel
    )
    {
        EnsureEffectiveCombatCacheRevision();
        var key = new EffectiveCombatProfileCacheKey(skillId, skillLevel);
        if (
            _effectiveCombatDefinitionCache.TryGetValue(
                key,
                out SkillEffectiveCombatDefinition cached
            )
        )
        {
            return cached;
        }

        SkillEffectiveCombatDefinition resolved = BuildEffectiveCombatDefinition(
            skillId,
            skillLevel
        );
        _effectiveCombatDefinitionCache[key] = resolved;
        return resolved;
    }

    public CombatSkillResourceCosts GetEffectiveResourceCostValues(
        StringName skillId,
        int skillLevel
    )
    {
        return GetEffectiveCombatDefinition(skillId, skillLevel).ResourceCosts;
    }

    public int GetEffectiveAttackRollBonus(StringName skillId, int skillLevel)
    {
        return GetEffectiveCombatDefinition(skillId, skillLevel).AttackRollBonus;
    }

    public StringName GetEffectiveAreaPattern(StringName skillId, int skillLevel)
    {
        return GetEffectiveCombatDefinition(skillId, skillLevel).AreaPattern;
    }

    public int GetEffectiveAreaValue(StringName skillId, int skillLevel)
    {
        return GetEffectiveCombatDefinition(skillId, skillLevel).AreaValue;
    }

    public int GetEffectiveRangeValue(StringName skillId, int skillLevel)
    {
        return GetEffectiveCombatDefinition(skillId, skillLevel).RangeValue;
    }

    public int GetEffectiveMaxTargetCount(StringName skillId, int skillLevel)
    {
        return GetEffectiveCombatDefinition(skillId, skillLevel).MaxTargetCount;
    }

    public IReadOnlyList<CombatCastVariantDefinition> GetUnlockedCastVariantDefinitions(
        StringName skillId,
        int skillLevel
    )
    {
        return GetEffectiveCombatDefinition(skillId, skillLevel).UnlockedCastVariants;
    }

    private void EnsureEffectiveCombatCacheRevision()
    {
        long currentRevision = GetRevision();
        if (_effectiveCombatCacheRevision == currentRevision)
        {
            return;
        }
        _effectiveCombatDefinitionCache.Clear();
        _effectiveCombatCacheRevision = currentRevision;
    }

    private SkillEffectiveCombatDefinition BuildEffectiveCombatDefinition(
        StringName skillId,
        int skillLevel
    )
    {
        if (!TryGetSkillDefinition(skillId, out SkillDefinition skillDefinition))
        {
            return SkillEffectiveCombatDefinition.BuildMissing(skillLevel);
        }

        return SkillEffectiveCombatDefinition.BuildUncached(skillDefinition, skillLevel);
    }
}
