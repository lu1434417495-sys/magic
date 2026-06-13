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

    private readonly GameContentCatalog _contentCatalog;
    private readonly Dictionary<EffectiveCombatProfileCacheKey, SkillEffectiveCombatProfile> _effectiveCombatProfileCache =
        new();
    private long _effectiveCombatProfileCacheRevision = long.MinValue;

    internal SkillCatalog(GameContentCatalog contentCatalog)
    {
        _contentCatalog = contentCatalog;
    }

    public long GetRevision() => _contentCatalog?.GetRevision() ?? 0;

    public IReadOnlyDictionary<StringName, SkillDef> GetSkillDefsTyped() =>
        _contentCatalog?.GetSkillDefsTyped() ?? EmptySkillDefs;

    public bool HasSkill(StringName skillId) =>
        skillId != "" && GetSkillDefsTyped().ContainsKey(skillId);

    public bool TryGetSkillDef(StringName skillId, out SkillDef skillDef)
    {
        skillDef = null;
        return skillId != "" && GetSkillDefsTyped().TryGetValue(skillId, out skillDef);
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
        EnsureEffectiveCombatProfileCacheRevision();
        var key = new EffectiveCombatProfileCacheKey(skillId, skillLevel);
        if (_effectiveCombatProfileCache.TryGetValue(key, out SkillEffectiveCombatProfile cached))
        {
            return cached;
        }

        SkillEffectiveCombatProfile resolved = BuildEffectiveCombatProfile(skillId, skillLevel);
        _effectiveCombatProfileCache[key] = resolved;
        return resolved;
    }

    public CombatSkillResourceCosts GetEffectiveResourceCostValues(
        StringName skillId,
        int skillLevel
    )
    {
        return GetEffectiveCombatProfile(skillId, skillLevel).ResourceCosts;
    }

    public int GetEffectiveAttackRollBonus(StringName skillId, int skillLevel)
    {
        return GetEffectiveCombatProfile(skillId, skillLevel).AttackRollBonus;
    }

    public StringName GetEffectiveAreaPattern(StringName skillId, int skillLevel)
    {
        return GetEffectiveCombatProfile(skillId, skillLevel).AreaPattern;
    }

    public int GetEffectiveAreaValue(StringName skillId, int skillLevel)
    {
        return GetEffectiveCombatProfile(skillId, skillLevel).AreaValue;
    }

    public int GetEffectiveRangeValue(StringName skillId, int skillLevel)
    {
        return GetEffectiveCombatProfile(skillId, skillLevel).RangeValue;
    }

    public int GetEffectiveMaxTargetCount(StringName skillId, int skillLevel)
    {
        return GetEffectiveCombatProfile(skillId, skillLevel).MaxTargetCount;
    }

    public IReadOnlyList<CombatCastVariantDef> GetUnlockedCastVariants(
        StringName skillId,
        int skillLevel
    )
    {
        return GetEffectiveCombatProfile(skillId, skillLevel).UnlockedCastVariants;
    }

    private void EnsureEffectiveCombatProfileCacheRevision()
    {
        long currentRevision = GetRevision();
        if (_effectiveCombatProfileCacheRevision == currentRevision)
        {
            return;
        }
        _effectiveCombatProfileCache.Clear();
        _effectiveCombatProfileCacheRevision = currentRevision;
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
}
