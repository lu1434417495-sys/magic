using System.Collections.Generic;
using Godot;

/// <summary>
/// 回归：验证 <see cref="ISkillCatalog"/> 门面只是 <see cref="GameContentCatalog"/> typed 快照之上的
/// 薄读层——每个 effective getter 都应与直接调用 <c>skillDef.combat_profile.GetEffective*</c> 对拍一致，
/// 命中 / 未命中语义正确，并且不依赖任何旧 string-key fallback。
/// </summary>
public partial class run_skill_catalog_query_regression : SceneTree
{
    private static readonly StringName[] SampleSkillIds =
    {
        "basic_attack",
        "charge",
        "archer_multishot",
        "mage_meteor_swarm",
    };

    private static readonly int[] SampleSkillLevels = { 0, 1, 2, 3, 5 };

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        GameSession gameSession = new();
        try
        {
            GameContentCatalog contentCatalog = gameSession.GetContentCatalogTyped();
            _test.True(contentCatalog != null, "GameSession 应通过 GameRoot 暴露 GameContentCatalog。");
            if (contentCatalog == null)
                return;

            ISkillCatalog skillCatalog = contentCatalog.GetSkillCatalogTyped();
            _test.True(skillCatalog != null, "GameContentCatalog 应暴露 ISkillCatalog 门面。");
            if (skillCatalog == null)
                return;

            TestSkillCatalogIsStableFacade(contentCatalog, skillCatalog);
            TestHasSkillAndTryGet(skillCatalog);
            TestRuntimeDefinitionsProjectSkillResource(contentCatalog, skillCatalog);
            TestEffectiveGettersMatchCombatProfile(skillCatalog);
            TestMissingSkillReturnsSafeDefaults(skillCatalog);
            TestEffectiveCacheInvalidatesWithCatalogRevision(contentCatalog, skillCatalog);
        }
        finally
        {
            gameSession.DisposeOwnedRuntimeResources();
            gameSession.Dispose();
        }

        Quit(_test.Finish("Skill catalog query regression"));
    }

    private void TestSkillCatalogIsStableFacade(
        GameContentCatalog contentCatalog,
        ISkillCatalog skillCatalog
    )
    {
        _test.True(
            ReferenceEquals(contentCatalog.GetSkillCatalogTyped(), skillCatalog),
            "GetSkillCatalogTyped 跨调用应返回同一门面实例。"
        );
        _test.Eq(
            skillCatalog.GetRevision(),
            contentCatalog.GetRevision(),
            "skill catalog revision 应与底层 content catalog 一致。"
        );
        _test.True(
            ReferenceEquals(skillCatalog.GetSkillDefsTyped(), contentCatalog.GetSkillDefsTyped()),
            "skill catalog 应直接读 content catalog 的 typed skill 快照视图，而不是另建副本。"
        );
    }

    private void TestHasSkillAndTryGet(ISkillCatalog skillCatalog)
    {
        _test.True(skillCatalog.HasSkill("basic_attack"), "skill catalog 应命中 basic_attack。");
        _test.True(
            skillCatalog.TryGetSkillDef("basic_attack", out SkillDef basicAttack)
                && basicAttack != null
                && basicAttack.skill_id == "basic_attack",
            "TryGetSkillDef 应取回 basic_attack 的 SkillDef。"
        );

        StringName missingId = "skill_catalog_regression_missing_id";
        _test.True(
            !skillCatalog.HasSkill(missingId),
            "skill catalog 不应命中不存在的技能 id。"
        );
        _test.True(
            !skillCatalog.TryGetSkillDef(missingId, out SkillDef missingDef) && missingDef == null,
            "TryGetSkillDef 对不存在的技能 id 应返回 false 且 out 为 null。"
        );
    }

    private void TestRuntimeDefinitionsProjectSkillResource(
        GameContentCatalog contentCatalog,
        ISkillCatalog skillCatalog
    )
    {
        IReadOnlyDictionary<StringName, SkillDefinition> runtimeDefinitions =
            skillCatalog.GetSkillDefinitionsTyped();
        _test.True(
            runtimeDefinitions != null,
            "skill catalog 应暴露 plain C# SkillDefinition 运行时定义快照。"
        );
        _test.Eq(
            runtimeDefinitions?.Count ?? -1,
            contentCatalog.GetSkillDefsTyped().Count,
            "SkillDefinition 快照数量应与底层 SkillDef content 快照一致。"
        );

        const string sampleSkillId = "mage_meteor_swarm";
        _test.True(
            skillCatalog.TryGetSkillDef(sampleSkillId, out SkillDef resourceSkill)
                && resourceSkill != null,
            $"{sampleSkillId} 应存在于 Resource skill 快照。"
        );
        _test.True(
            skillCatalog.TryGetSkillDefinition(sampleSkillId, out SkillDefinition runtimeSkill)
                && runtimeSkill != null,
            $"{sampleSkillId} 应存在于 SkillDefinition runtime 快照。"
        );
        if (resourceSkill == null || runtimeSkill == null)
            return;

        _test.Eq(runtimeSkill.SkillId, resourceSkill.skill_id, "SkillDefinition 应保留 skill id。");
        _test.Eq(
            runtimeSkill.DisplayName,
            resourceSkill.display_name,
            "SkillDefinition 应保留显示名。"
        );
        _test.Eq(
            runtimeSkill.Tags.Count,
            resourceSkill.TagsTyped.Count,
            "SkillDefinition 应保留 tags 数量。"
        );
        _test.True(
            runtimeSkill.CombatProfile != null,
            "带 combat_profile 的技能应投影 CombatSkillDefinition。"
        );
        if (runtimeSkill.CombatProfile == null || resourceSkill.combat_profile == null)
            return;

        CombatSkillDefinition runtimeCombat = runtimeSkill.CombatProfile;
        CombatSkillDef resourceCombat = resourceSkill.combat_profile;
        const int sampleLevel = 3;

        AssertCostsEq(
            runtimeCombat.GetEffectiveResourceCostValues(sampleLevel),
            resourceCombat.GetEffectiveResourceCostValues(sampleLevel),
            "CombatSkillDefinition 的有效消耗应与 Resource combat_profile 对拍一致。"
        );
        _test.Eq(
            runtimeCombat.GetEffectiveAttackRollBonus(sampleLevel),
            resourceCombat.GetEffectiveAttackRollBonus(sampleLevel),
            "CombatSkillDefinition 的有效命中加值应与 Resource combat_profile 对拍一致。"
        );
        _test.Eq(
            runtimeCombat.GetEffectiveRangeValue(sampleLevel),
            resourceCombat.GetEffectiveRangeValue(sampleLevel),
            "CombatSkillDefinition 的有效射程应与 Resource combat_profile 对拍一致。"
        );
        _test.Eq(
            runtimeCombat.GetEffectiveAreaValue(sampleLevel),
            resourceCombat.GetEffectiveAreaValue(sampleLevel),
            "CombatSkillDefinition 的有效范围值应与 Resource combat_profile 对拍一致。"
        );
        _test.Eq(
            runtimeCombat.GetEffectiveAreaPattern(sampleLevel),
            resourceCombat.GetEffectiveAreaPattern(sampleLevel),
            "CombatSkillDefinition 的有效范围模式应与 Resource combat_profile 对拍一致。"
        );
        AssertRuntimeVariantsMatch(
            runtimeCombat.GetUnlockedCastVariants(sampleLevel),
            resourceCombat.GetUnlockedCastVariants(sampleLevel),
            "CombatSkillDefinition 的已解锁施法变体应与 Resource combat_profile 对拍一致。"
        );
    }

    private void TestEffectiveGettersMatchCombatProfile(ISkillCatalog skillCatalog)
    {
        foreach (StringName skillId in SampleSkillIds)
        {
            _test.True(
                skillCatalog.HasSkill(skillId),
                $"对拍样本技能 {skillId} 应存在于 catalog。"
            );
            if (!skillCatalog.TryGetSkillDef(skillId, out SkillDef skillDef) || skillDef == null)
            {
                _test.Fail($"无法取回样本技能 {skillId} 的 SkillDef。");
                continue;
            }

            CombatSkillDef profile = skillDef.combat_profile;
            _test.True(profile != null, $"样本技能 {skillId} 应带 combat_profile。");
            if (profile == null)
                continue;

            // 门面解析出的 combat profile 应与直接从 SkillDef 读到的是同一实例。
            _test.True(
                ReferenceEquals(skillCatalog.GetCombatProfileTyped(skillId), profile),
                $"{skillId} 的 GetCombatProfileTyped 应返回 SkillDef.combat_profile 同一实例。"
            );

            foreach (int level in SampleSkillLevels)
            {
                SkillEffectiveCombatProfile effectiveProfile =
                    skillCatalog.GetEffectiveCombatProfile(skillId, level);
                SkillEffectiveCombatDefinition effectiveDefinition =
                    skillCatalog.GetEffectiveCombatDefinition(skillId, level);
                _test.True(
                    effectiveProfile != null,
                    $"{skillId}@L{level} 的聚合 effective profile 不应为 null。"
                );
                _test.True(
                    effectiveDefinition != null,
                    $"{skillId}@L{level} 的聚合 runtime effective definition 不应为 null。"
                );
                _test.True(
                    ReferenceEquals(
                        effectiveProfile,
                        skillCatalog.GetEffectiveCombatProfile(skillId, level)
                    ),
                    $"{skillId}@L{level} 的聚合 effective profile 应被缓存复用。"
                );
                _test.True(
                    ReferenceEquals(
                        effectiveDefinition,
                        skillCatalog.GetEffectiveCombatDefinition(skillId, level)
                    ),
                    $"{skillId}@L{level} 的聚合 runtime effective definition 应被缓存复用。"
                );
                _test.True(
                    ReferenceEquals(effectiveProfile.SkillDef, skillDef),
                    $"{skillId}@L{level} 的 effective profile 应引用同一 SkillDef。"
                );
                _test.True(
                    ReferenceEquals(effectiveProfile.CombatProfile, profile),
                    $"{skillId}@L{level} 的 effective profile 应引用同一 CombatSkillDef。"
                );
                _test.True(
                    ReferenceEquals(
                        effectiveDefinition.SkillDefinition,
                        skillCatalog.GetSkillDefinitionsTyped()[skillId]
                    ),
                    $"{skillId}@L{level} 的 runtime effective definition 应引用 catalog SkillDefinition。"
                );
                _test.Eq(
                    effectiveProfile.SkillLevel,
                    level,
                    $"{skillId}@L{level} 的 effective profile 应保留请求等级。"
                );
                _test.Eq(
                    effectiveDefinition.SkillLevel,
                    level,
                    $"{skillId}@L{level} 的 runtime effective definition 应保留请求等级。"
                );
                AssertCostsEq(
                    effectiveProfile.ResourceCosts,
                    profile.GetEffectiveResourceCostValues(level),
                    $"{skillId}@L{level} 的聚合有效消耗应与 combat profile 对拍一致。"
                );
                AssertCostsEq(
                    effectiveDefinition.ResourceCosts,
                    profile.GetEffectiveResourceCostValues(level),
                    $"{skillId}@L{level} 的 runtime 聚合有效消耗应与 combat profile 对拍一致。"
                );
                _test.Eq(
                    effectiveProfile.AttackRollBonus,
                    profile.GetEffectiveAttackRollBonus(level),
                    $"{skillId}@L{level} 的聚合有效命中加值应对拍一致。"
                );
                _test.Eq(
                    effectiveProfile.RangeValue,
                    profile.GetEffectiveRangeValue(level),
                    $"{skillId}@L{level} 的聚合有效射程应对拍一致。"
                );
                _test.Eq(
                    effectiveProfile.AreaValue,
                    profile.GetEffectiveAreaValue(level),
                    $"{skillId}@L{level} 的聚合有效范围值应对拍一致。"
                );
                _test.Eq(
                    effectiveProfile.MaxTargetCount,
                    profile.GetEffectiveMaxTargetCount(level),
                    $"{skillId}@L{level} 的聚合有效最大目标数应对拍一致。"
                );
                _test.Eq(
                    effectiveProfile.AreaPattern,
                    profile.GetEffectiveAreaPattern(level),
                    $"{skillId}@L{level} 的聚合有效范围模式应对拍一致。"
                );
                AssertEffectiveGetterFacadeMatchesProfile(
                    skillCatalog,
                    skillId,
                    level,
                    effectiveProfile
                );
                AssertUnlockedVariantsMatch(
                    effectiveProfile.UnlockedCastVariants,
                    profile.GetUnlockedCastVariants(level),
                    $"{skillId}@L{level} 的已解锁施法变体应对拍一致。"
                );
                AssertRuntimeVariantsMatch(
                    effectiveDefinition.UnlockedCastVariants,
                    profile.GetUnlockedCastVariants(level),
                    $"{skillId}@L{level} 的 runtime 已解锁施法变体应对拍一致。"
                );
            }
        }
    }

    private void TestMissingSkillReturnsSafeDefaults(ISkillCatalog skillCatalog)
    {
        StringName missingId = "skill_catalog_regression_missing_id";
        _test.True(
            skillCatalog.GetCombatProfileTyped(missingId) == null,
            "不存在技能的 GetCombatProfileTyped 应返回 null。"
        );
        AssertCostsEq(
            skillCatalog.GetEffectiveResourceCostValues(missingId, 1),
            CombatSkillResourceCosts.Zero,
            "不存在技能的有效消耗应返回 CombatSkillResourceCosts.Zero。"
        );
        _test.Eq(
            skillCatalog.GetEffectiveAttackRollBonus(missingId, 1),
            0,
            "不存在技能的有效命中加值应返回 0。"
        );
        _test.Eq(
            skillCatalog.GetEffectiveRangeValue(missingId, 1),
            0,
            "不存在技能的有效射程应返回 0。"
        );
        _test.Eq(
            skillCatalog.GetEffectiveAreaValue(missingId, 1),
            0,
            "不存在技能的有效范围值应返回 0。"
        );
        _test.Eq(
            skillCatalog.GetEffectiveMaxTargetCount(missingId, 1),
            0,
            "不存在技能的有效最大目标数应返回 0。"
        );
        _test.Eq(
            skillCatalog.GetEffectiveAreaPattern(missingId, 1),
            "",
            "不存在技能的有效范围模式应返回空 StringName。"
        );
        IReadOnlyList<CombatCastVariantDef> variants =
            skillCatalog.GetUnlockedCastVariants(missingId, 1);
        _test.True(variants != null, "不存在技能的 GetUnlockedCastVariants 不应返回 null。");
        _test.Eq(
            variants?.Count ?? -1,
            0,
            "不存在技能的 GetUnlockedCastVariants 应返回空列表。"
        );
        SkillEffectiveCombatProfile missingProfile =
            skillCatalog.GetEffectiveCombatProfile(missingId, 1);
        SkillEffectiveCombatDefinition missingDefinition =
            skillCatalog.GetEffectiveCombatDefinition(missingId, 1);
        _test.True(missingProfile != null, "不存在技能的聚合 effective profile 不应返回 null。");
        _test.True(
            missingDefinition != null,
            "不存在技能的聚合 runtime effective definition 不应返回 null。"
        );
        _test.True(
            ReferenceEquals(missingProfile, skillCatalog.GetEffectiveCombatProfile(missingId, 1)),
            "不存在技能的聚合 effective profile 也应按 key 缓存。"
        );
        _test.True(
            !missingProfile.HasCombatProfile,
            "不存在技能的聚合 effective profile 不应带 combat profile。"
        );
        _test.True(
            !missingDefinition.HasCombatProfile,
            "不存在技能的聚合 runtime effective definition 不应带 combat profile。"
        );
        AssertCostsEq(
            missingProfile.ResourceCosts,
            CombatSkillResourceCosts.Zero,
            "不存在技能的聚合 effective profile 消耗应为 Zero。"
        );
        AssertCostsEq(
            missingDefinition.ResourceCosts,
            CombatSkillResourceCosts.Zero,
            "不存在技能的聚合 runtime effective definition 消耗应为 Zero。"
        );
    }

    private void TestEffectiveCacheInvalidatesWithCatalogRevision(
        GameContentCatalog contentCatalog,
        ISkillCatalog skillCatalog
    )
    {
        SkillEffectiveCombatProfile before =
            skillCatalog.GetEffectiveCombatProfile("basic_attack", 1);
        SkillEffectiveCombatDefinition beforeDefinition =
            skillCatalog.GetEffectiveCombatDefinition("basic_attack", 1);
        long beforeRevision = skillCatalog.GetRevision();

        contentCatalog.ClearSessionBinding();

        _test.True(
            skillCatalog.GetRevision() > beforeRevision,
            "content catalog clear 后 skill catalog revision 应前进。"
        );
        SkillEffectiveCombatProfile after =
            skillCatalog.GetEffectiveCombatProfile("basic_attack", 1);
        SkillEffectiveCombatDefinition afterDefinition =
            skillCatalog.GetEffectiveCombatDefinition("basic_attack", 1);
        _test.True(
            !ReferenceEquals(before, after),
            "catalog revision 变化后 effective profile cache 应失效并重建。"
        );
        _test.True(
            !ReferenceEquals(beforeDefinition, afterDefinition),
            "catalog revision 变化后 runtime effective definition cache 应失效并重建。"
        );
        _test.True(
            !after.HasCombatProfile,
            "catalog clear 后 effective profile 不应返回旧 SkillDef/combat profile。"
        );
        _test.True(
            !afterDefinition.HasCombatProfile,
            "catalog clear 后 runtime effective definition 不应返回旧 SkillDefinition/combat profile。"
        );
        AssertCostsEq(
            after.ResourceCosts,
            CombatSkillResourceCosts.Zero,
            "catalog clear 后 effective profile 消耗应回到安全默认值。"
        );
    }

    private void AssertEffectiveGetterFacadeMatchesProfile(
        ISkillCatalog skillCatalog,
        StringName skillId,
        int level,
        SkillEffectiveCombatProfile effectiveProfile
    )
    {
        AssertCostsEq(
            skillCatalog.GetEffectiveResourceCostValues(skillId, level),
            effectiveProfile.ResourceCosts,
            $"{skillId}@L{level} 的消耗 getter 应读取聚合 effective profile。"
        );
        _test.Eq(
            skillCatalog.GetEffectiveAttackRollBonus(skillId, level),
            effectiveProfile.AttackRollBonus,
            $"{skillId}@L{level} 的命中 getter 应读取聚合 effective profile。"
        );
        _test.Eq(
            skillCatalog.GetEffectiveRangeValue(skillId, level),
            effectiveProfile.RangeValue,
            $"{skillId}@L{level} 的射程 getter 应读取聚合 effective profile。"
        );
        _test.Eq(
            skillCatalog.GetEffectiveAreaValue(skillId, level),
            effectiveProfile.AreaValue,
            $"{skillId}@L{level} 的范围值 getter 应读取聚合 effective profile。"
        );
        _test.Eq(
            skillCatalog.GetEffectiveMaxTargetCount(skillId, level),
            effectiveProfile.MaxTargetCount,
            $"{skillId}@L{level} 的最大目标数 getter 应读取聚合 effective profile。"
        );
        _test.Eq(
            skillCatalog.GetEffectiveAreaPattern(skillId, level),
            effectiveProfile.AreaPattern,
            $"{skillId}@L{level} 的范围模式 getter 应读取聚合 effective profile。"
        );
        _test.True(
            ReferenceEquals(
                skillCatalog.GetUnlockedCastVariants(skillId, level),
                effectiveProfile.UnlockedCastVariants
            ),
            $"{skillId}@L{level} 的施法变体 getter 应读取聚合 effective profile。"
        );
    }

    private void AssertUnlockedVariantsMatch(
        IReadOnlyList<CombatCastVariantDef> actual,
        Godot.Collections.Array<CombatCastVariantDef> expected,
        string message
    )
    {
        _test.True(actual != null, message);
        _test.True(expected != null, message);
        if (actual == null || expected == null)
            return;
        _test.Eq(actual.Count, expected.Count, message);
        int count = System.Math.Min(actual.Count, expected.Count);
        for (int i = 0; i < count; i++)
        {
            _test.Eq(
                actual[i]?.variant_id ?? "",
                expected[i]?.variant_id ?? "",
                $"{message} index={i} variant_id"
            );
            _test.Eq(
                actual[i]?.min_skill_level ?? -1,
                expected[i]?.min_skill_level ?? -1,
                $"{message} index={i} min_skill_level"
            );
        }
    }

    private void AssertRuntimeVariantsMatch(
        IReadOnlyList<CombatCastVariantDefinition> actual,
        Godot.Collections.Array<CombatCastVariantDef> expected,
        string message
    )
    {
        _test.True(actual != null, message);
        _test.True(expected != null, message);
        if (actual == null || expected == null)
            return;
        _test.Eq(actual.Count, expected.Count, message);
        int count = System.Math.Min(actual.Count, expected.Count);
        for (int i = 0; i < count; i++)
        {
            _test.Eq(
                actual[i]?.VariantId ?? "",
                expected[i]?.variant_id ?? "",
                $"{message} index={i} variant_id"
            );
            _test.Eq(
                actual[i]?.MinSkillLevel ?? -1,
                expected[i]?.min_skill_level ?? -1,
                $"{message} index={i} min_skill_level"
            );
        }
    }


    private void AssertCostsEq(
        CombatSkillResourceCosts actual,
        CombatSkillResourceCosts expected,
        string message
    )
    {
        if (actual != expected)
            _test.Fail($"{message} actual={actual} expected={expected}");
    }
}
