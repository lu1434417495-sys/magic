using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

/// <summary>
/// <see cref="ISkillCatalog"/> 的默认实现。仅持有所属 <see cref="GameContentCatalog"/> 引用，
/// 不缓存可变 skill 字典、不扫描资源：每次查询都从 <see cref="GameContentCatalog.GetSkillDefsTyped"/>
/// 读取当前 typed 快照与 revision；derived effective profile 按 revision 缓存，因此随 catalog 的
/// clear/rebuild 自动失效，门面实例本身无需重建。
///
/// 所有 effective getter 都读取同一个 <see cref="SkillEffectiveCombatProfile"/> 缓存项，避免同一
/// skill/level 连续查询多个字段时重复合并 level override；技能不存在或无 combat profile 时返回安全默认值，
/// 不做旧 string-key fallback。
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

    private static readonly IReadOnlyDictionary<StringName, SkillDef> EmptySkillDefs =
        new ReadOnlyDictionary<StringName, SkillDef>(new Dictionary<StringName, SkillDef>());
    private static readonly IReadOnlyDictionary<StringName, SkillDefinition> EmptySkillDefinitions =
        new ReadOnlyDictionary<StringName, SkillDefinition>(
            new Dictionary<StringName, SkillDefinition>()
        );

    private readonly GameContentCatalog _contentCatalog;
    private readonly Dictionary<EffectiveCombatProfileCacheKey, SkillEffectiveCombatProfile> _effectiveCombatProfileCache =
        new();
    private readonly Dictionary<EffectiveCombatProfileCacheKey, SkillEffectiveCombatDefinition> _effectiveCombatDefinitionCache =
        new();
    private long _effectiveCombatCacheRevision = long.MinValue;

    internal SkillCatalog(GameContentCatalog contentCatalog)
    {
        _contentCatalog = contentCatalog;
    }

    public long GetRevision() => _contentCatalog?.GetRevision() ?? 0;

    public IReadOnlyDictionary<StringName, SkillDef> GetSkillDefsTyped() =>
        _contentCatalog?.GetSkillDefsTyped() ?? EmptySkillDefs;

    public IReadOnlyDictionary<StringName, SkillDefinition> GetSkillDefinitionsTyped() =>
        _contentCatalog?.GetSkillDefinitionsTyped() ?? EmptySkillDefinitions;

    public bool HasSkill(StringName skillId) =>
        skillId != "" && GetSkillDefsTyped().ContainsKey(skillId);

    public bool TryGetSkillDef(StringName skillId, out SkillDef skillDef)
    {
        skillDef = null;
        return skillId != "" && GetSkillDefsTyped().TryGetValue(skillId, out skillDef);
    }

    public bool TryGetSkillDefinition(StringName skillId, out SkillDefinition skillDefinition)
    {
        skillDefinition = null;
        return skillId != ""
            && GetSkillDefinitionsTyped().TryGetValue(skillId, out skillDefinition);
    }

    public CombatSkillDef GetCombatProfileTyped(StringName skillId)
    {
        return TryGetSkillDef(skillId, out SkillDef skillDef) ? skillDef?.combat_profile : null;
    }

    public SkillEffectiveCombatProfile GetEffectiveCombatProfile(
        StringName skillId,
        int skillLevel
    )
    {
        EnsureEffectiveCombatCacheRevision();
        var key = new EffectiveCombatProfileCacheKey(skillId, skillLevel);
        if (_effectiveCombatProfileCache.TryGetValue(key, out SkillEffectiveCombatProfile cached))
        {
            return cached;
        }

        SkillEffectiveCombatProfile resolved = BuildEffectiveCombatProfile(skillId, skillLevel);
        _effectiveCombatProfileCache[key] = resolved;
        return resolved;
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

    public IReadOnlyList<CombatCastVariantDef> GetUnlockedCastVariants(
        StringName skillId,
        int skillLevel
    )
    {
        return GetEffectiveCombatProfile(skillId, skillLevel).UnlockedCastVariants;
    }

    private void EnsureEffectiveCombatCacheRevision()
    {
        long currentRevision = GetRevision();
        if (_effectiveCombatCacheRevision == currentRevision)
        {
            return;
        }
        _effectiveCombatProfileCache.Clear();
        _effectiveCombatDefinitionCache.Clear();
        _effectiveCombatCacheRevision = currentRevision;
    }

    private SkillEffectiveCombatProfile BuildEffectiveCombatProfile(
        StringName skillId,
        int skillLevel
    )
    {
        if (!TryGetSkillDef(skillId, out SkillDef skillDef) || skillDef?.combat_profile == null)
        {
            return SkillEffectiveCombatProfileResolver.BuildMissing(skillLevel);
        }

        return SkillEffectiveCombatProfileResolver.BuildUncached(skillDef, skillLevel);
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
