using System.Collections.Generic;
using Godot;

/// <summary>
/// 回归：验证 <see cref="ISkillCatalog"/> 门面只是 <see cref="GameContentCatalog"/> typed 快照之上的
/// 薄读层——每个 effective getter 都应与直接调用 <c>skillDef.combat_profile.GetEffective*</c> 对拍一致，
/// 命中 / 未命中语义正确，并且不依赖任何旧 string-key fallback。
/// </summary>
public partial class run_skill_catalog_query_regression : LifecycleTestSceneTree
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
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        GameSession gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
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
            TestRuntimeCombatEffectiveSemanticsMirrorResource();
            TestEffectiveGettersMatchCombatProfile(skillCatalog);
            TestMissingSkillReturnsSafeDefaults(skillCatalog);
            TestEffectiveCacheInvalidatesWithCatalogRevision(contentCatalog, skillCatalog);
        }
        finally
        {
            gameSession.DisposeOwnedRuntimeResources();
            gameSession.Dispose();
        }

        RequestTestExit(_test.Finish("Skill catalog query regression"));
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
            ReferenceEquals(skillCatalog.GetSkillDefinitionsTyped(), contentCatalog.GetSkillDefinitionsTyped()),
            "skill catalog 应直接读 content catalog 的 SkillDefinition 快照视图，而不是另建副本。"
        );
    }

    private void TestHasSkillAndTryGet(ISkillCatalog skillCatalog)
    {
        _test.True(skillCatalog.HasSkill("basic_attack"), "skill catalog 应命中 basic_attack。");
        _test.True(
            skillCatalog.TryGetSkillDefinition("basic_attack", out SkillDefinition basicAttack)
                && basicAttack != null
                && basicAttack.SkillId == "basic_attack",
            "TryGetSkillDefinition 应取回 basic_attack 的 SkillDefinition。"
        );

        StringName missingId = "skill_catalog_regression_missing_id";
        _test.True(
            !skillCatalog.HasSkill(missingId),
            "skill catalog 不应命中不存在的技能 id。"
        );
        _test.True(
            !skillCatalog.TryGetSkillDefinition(missingId, out SkillDefinition missingDef) && missingDef == null,
            "TryGetSkillDefinition 对不存在的技能 id 应返回 false 且 out 为 null。"
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
            contentCatalog.GetSkillDefinitionsTyped().Count,
            "SkillDefinition 快照数量应与 content catalog runtime skill definition 快照一致。"
        );

        const string sampleSkillId = "mage_meteor_swarm";
        SkillDefinition catalogSkill = null;
        SkillDefinition runtimeSkill = null;
        _test.True(
            contentCatalog.GetSkillDefinitionsTyped().TryGetValue(
                sampleSkillId,
                out catalogSkill
            )
                && catalogSkill != null
                && skillCatalog.TryGetSkillDefinition(sampleSkillId, out runtimeSkill)
                && runtimeSkill != null,
            $"{sampleSkillId} 应存在于 SkillDefinition runtime 快照。"
        );
        if (catalogSkill == null || runtimeSkill == null)
            return;

        _test.Eq(
            runtimeSkill.SkillId,
            catalogSkill.SkillId,
            "SkillCatalog 应返回 content catalog 中的 SkillDefinition skill id。"
        );
        _test.Eq(
            runtimeSkill.DisplayName,
            catalogSkill.DisplayName,
            "SkillCatalog 应返回 content catalog 中的 SkillDefinition 显示名。"
        );
        _test.Eq(
            runtimeSkill.Tags.Count,
            catalogSkill.Tags.Count,
            "SkillCatalog 应返回 content catalog 中的 SkillDefinition tags。"
        );
        _test.True(
            runtimeSkill.CombatProfile != null,
            "带 combat_profile 的技能应投影 CombatSkillDefinition。"
        );
        if (runtimeSkill.CombatProfile == null || catalogSkill.CombatProfile == null)
            return;

        CombatSkillDefinition runtimeCombat = runtimeSkill.CombatProfile;
        CombatSkillDefinition catalogCombat = catalogSkill.CombatProfile;
        const int sampleLevel = 3;

        AssertCostsEq(
            runtimeCombat.GetEffectiveResourceCostValues(sampleLevel),
            catalogCombat.GetEffectiveResourceCostValues(sampleLevel),
            "CombatSkillDefinition 的有效消耗应与 catalog SkillDefinition 对拍一致。"
        );
        _test.Eq(
            runtimeCombat.GetEffectiveAttackRollBonus(sampleLevel),
            catalogCombat.GetEffectiveAttackRollBonus(sampleLevel),
            "CombatSkillDefinition 的有效命中加值应与 catalog SkillDefinition 对拍一致。"
        );
        _test.Eq(
            runtimeCombat.GetEffectiveRangeValue(sampleLevel),
            catalogCombat.GetEffectiveRangeValue(sampleLevel),
            "CombatSkillDefinition 的有效射程应与 catalog SkillDefinition 对拍一致。"
        );
        _test.Eq(
            runtimeCombat.GetEffectiveAreaValue(sampleLevel),
            catalogCombat.GetEffectiveAreaValue(sampleLevel),
            "CombatSkillDefinition 的有效范围值应与 catalog SkillDefinition 对拍一致。"
        );
        _test.Eq(
            runtimeCombat.GetEffectiveAreaPattern(sampleLevel),
            catalogCombat.GetEffectiveAreaPattern(sampleLevel),
            "CombatSkillDefinition 的有效范围模式应与 catalog SkillDefinition 对拍一致。"
        );
        AssertRuntimeVariantsMatch(
            runtimeCombat.GetUnlockedCastVariants(sampleLevel),
            catalogCombat.GetUnlockedCastVariants(sampleLevel),
            "CombatSkillDefinition 的已解锁施法变体应与 catalog SkillDefinition 对拍一致。"
        );
    }

    private void TestRuntimeCombatEffectiveSemanticsMirrorResource()
    {
        var levelTwoOverrides = TestResourceOwnership.OwnWrapper(
            new Godot.Collections.Dictionary
            {
                ["casting_time_tu"] = 7,
                ["casting_maintenance_dc"] = 11,
                ["casting_spell_control_dc"] = 13,
                ["pending_cast_binding_mode"] = "ground_bind",
            },
            "skill-catalog-query-level-two-overrides"
        );
        var rawLevelOverrides = TestResourceOwnership.OwnWrapper(
            new Godot.Collections.Dictionary { [Variant.From(2.0)] = levelTwoOverrides },
            "skill-catalog-query-level-overrides"
        );
        CombatEffectDef resourceEffect = TestResourceOwnership.Own(
            new CombatEffectDef
            {
                effect_type = "damage",
                effect_target_team_filter = "enemy",
                damage_tag = "fire",
                damage_ratio_percent = 75,
                pre_resistance_damage_multiplier = 1.5,
                damage_category = "elemental",
                dr_bypass_tag = "magic",
                dice_count = 3,
                dice_sides = 8,
                dice_bonus = 2,
                save_dc = 14,
                save_dc_mode = "caster_spell",
                save_dc_source_ability = "intelligence",
                save_ability = "agility",
                save_partial_on_success = true,
                save_tag = "fireball",
                status_id = "burning",
                applied_status_duration_tu = 40,
                duration_tu = 60,
                tick_interval_tu = 10,
                effect_tags = TestResourceOwnership.OwnWrapper(
                    new Godot.Collections.Array<StringName> { "fire", "dot" },
                    "skill-catalog-query-effect-tags"
                ),
            },
            "skill-catalog-query-effect-resource"
        );
        var effectDefs = TestResourceOwnership.OwnWrapper(
            new Godot.Collections.Array<CombatEffectDef> { resourceEffect },
            "skill-catalog-query-effect-defs"
        );
        CombatSkillDef resourceCombat = TestResourceOwnership.Own(
            new CombatSkillDef
            {
                skill_id = "skill_catalog_runtime_semantics",
                casting_time_tu = 3,
                casting_maintenance_dc = 5,
                casting_spell_control_dc = 9,
                pending_cast_binding_mode = "hard_anchor",
                spell_fate_mode = "control_roll",
                spell_critical_mode = "mp_refund",
                spell_critical_mp_refund_percent = 50,
                fumble_protection_curve = new[] { 0, 1, 2, 3 },
                fumble_protection_extra_mp_percent = 25,
                backlash_mode = "ground_anchor_drift",
                backlash_target_filter = "any",
                backlash_offset_radius = 2,
                area_origin_mode = "anchor_coord",
                area_direction_mode = "caster_facing",
                level_overrides = rawLevelOverrides,
                effect_defs = effectDefs,
            },
            "skill-catalog-query-combat-resource"
        );

        CombatSkillDefinition runtimeCombat = CombatSkillDefinition.FromResource(
            resourceCombat,
            "skill_catalog_runtime_semantics",
            "test.skill_catalog.runtime_semantics.combat_profile"
        );
        _test.True(runtimeCombat != null, "CombatSkillDefinition 应能从合成 Resource 投影。");
        if (runtimeCombat == null)
            return;

        foreach (int level in new[] { 1, 2, 4 })
        {
            _test.Eq(
                runtimeCombat.GetEffectiveCastingTimeTu(level),
                resourceCombat.GetEffectiveCastingTimeTu(level),
                $"CombatSkillDefinition@L{level} casting_time_tu 应与 Resource 对拍。"
            );
            _test.Eq(
                runtimeCombat.GetEffectiveCastingMaintenanceDc(level),
                resourceCombat.GetEffectiveCastingMaintenanceDc(level),
                $"CombatSkillDefinition@L{level} casting_maintenance_dc 应与 Resource 对拍。"
            );
            _test.Eq(
                runtimeCombat.GetEffectiveCastingSpellControlDc(level),
                resourceCombat.GetEffectiveCastingSpellControlDc(level),
                $"CombatSkillDefinition@L{level} casting_spell_control_dc 应与 Resource 对拍。"
            );
            _test.Eq(
                runtimeCombat.GetEffectivePendingCastBindingMode(level),
                resourceCombat.GetEffectivePendingCastBindingMode(level),
                $"CombatSkillDefinition@L{level} pending_cast_binding_mode 应与 Resource 对拍。"
            );
            _test.Eq(
                runtimeCombat.HasCastingTime(level),
                resourceCombat.HasCastingTime(level),
                $"CombatSkillDefinition@L{level} HasCastingTime 应与 Resource 对拍。"
            );
            _test.Eq(
                runtimeCombat.GetFumbleProtectionLimit(level),
                resourceCombat.GetFumbleProtectionLimit(level),
                $"CombatSkillDefinition@L{level} fumble protection limit 应与 Resource 对拍。"
            );
        }

        _test.Eq(
            runtimeCombat.HasSpellFateControl(),
            resourceCombat.HasSpellFateControl(),
            "CombatSkillDefinition spell fate control 语义应与 Resource 对拍。"
        );
        _test.Eq(
            runtimeCombat.UsesGroundAnchorDriftBacklash(),
            resourceCombat.UsesGroundAnchorDriftBacklash(),
            "CombatSkillDefinition backlash 语义应与 Resource 对拍。"
        );
        _test.Eq(
            runtimeCombat.SpellFateModeKind,
            resourceCombat.SpellFateModeKind,
            "CombatSkillDefinition spell fate enum 应与 Resource 对拍。"
        );
        _test.Eq(
            runtimeCombat.BacklashModeKind,
            resourceCombat.BacklashModeKind,
            "CombatSkillDefinition backlash enum 应与 Resource 对拍。"
        );
        _test.Eq(
            runtimeCombat.AreaOriginModeKind,
            resourceCombat.AreaOriginModeKind,
            "CombatSkillDefinition area origin enum 应与 Resource 对拍。"
        );
        _test.Eq(
            runtimeCombat.AreaDirectionModeKind,
            resourceCombat.AreaDirectionModeKind,
            "CombatSkillDefinition area direction enum 应与 Resource 对拍。"
        );
        AssertRuntimeEffectDefinitionMatchesResource(
            runtimeCombat.EffectDefinitions[0],
            resourceEffect,
            "CombatEffectDefinition 应保留伤害/豁免/状态持续字段。"
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
            if (!skillCatalog.TryGetSkillDefinition(skillId, out SkillDefinition skillDefinition) || skillDefinition == null)
            {
                _test.Fail($"无法取回样本技能 {skillId} 的 SkillDefinition。");
                continue;
            }

            CombatSkillDefinition profile = skillDefinition.CombatProfile;
            _test.True(profile != null, $"样本技能 {skillId} 应带 combat_profile。");
            if (profile == null)
                continue;

            foreach (int level in SampleSkillLevels)
            {
                SkillEffectiveCombatDefinition effectiveDefinition =
                    skillCatalog.GetEffectiveCombatDefinition(skillId, level);
                _test.True(
                    effectiveDefinition != null,
                    $"{skillId}@L{level} 的聚合 runtime effective definition 不应为 null。"
                );
                _test.True(
                    ReferenceEquals(
                        effectiveDefinition,
                        skillCatalog.GetEffectiveCombatDefinition(skillId, level)
                    ),
                        $"{skillId}@L{level} 的聚合 runtime effective definition 应被缓存复用。"
                );
                _test.True(
                    ReferenceEquals(
                        effectiveDefinition.SkillDefinition,
                        skillCatalog.GetSkillDefinitionsTyped()[skillId]
                    ),
                    $"{skillId}@L{level} 的 runtime effective definition 应引用 catalog SkillDefinition。"
                );
                _test.Eq(
                    effectiveDefinition.SkillLevel,
                    level,
                    $"{skillId}@L{level} 的 runtime effective definition 应保留请求等级。"
                );
                AssertCostsEq(
                    effectiveDefinition.ResourceCosts,
                    profile.GetEffectiveResourceCostValues(level),
                    $"{skillId}@L{level} 的 runtime 聚合有效消耗应与 combat profile 对拍一致。"
                );
                AssertEffectiveGetterFacadeMatchesProfile(
                    skillCatalog,
                    skillId,
                    level,
                    effectiveDefinition
                );
                AssertRuntimeVariantsMatch(
                    effectiveDefinition.UnlockedCastVariants,
                    profile.GetUnlockedCastVariants(level),
                    $"{skillId}@L{level} 的 runtime 已解锁施法变体应对拍一致。"
                );
                AssertRuntimeEffectiveDefinitionMatchesProfile(
                    effectiveDefinition,
                    profile,
                    $"{skillId}@L{level} 的 runtime effective combat definition"
                );
            }
        }
    }

    private void TestMissingSkillReturnsSafeDefaults(ISkillCatalog skillCatalog)
    {
        StringName missingId = "skill_catalog_regression_missing_id";
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
        IReadOnlyList<CombatCastVariantDefinition> variants =
            skillCatalog.GetUnlockedCastVariantDefinitions(missingId, 1);
        _test.True(variants != null, "不存在技能的 GetUnlockedCastVariantDefinitions 不应返回 null。");
        _test.Eq(
            variants?.Count ?? -1,
            0,
            "不存在技能的 GetUnlockedCastVariantDefinitions 应返回空列表。"
        );
        SkillEffectiveCombatDefinition missingDefinition =
            skillCatalog.GetEffectiveCombatDefinition(missingId, 1);
        _test.True(
            missingDefinition != null,
            "不存在技能的聚合 runtime effective definition 不应返回 null。"
        );
        _test.True(
            !missingDefinition.HasCombatProfile,
            "不存在技能的聚合 runtime effective definition 不应带 combat profile。"
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
        SkillEffectiveCombatDefinition beforeDefinition =
            skillCatalog.GetEffectiveCombatDefinition("basic_attack", 1);
        long beforeRevision = skillCatalog.GetRevision();

        contentCatalog.ClearSessionBinding();

        _test.True(
            skillCatalog.GetRevision() > beforeRevision,
            "content catalog clear 后 skill catalog revision 应前进。"
        );
        SkillEffectiveCombatDefinition afterDefinition =
            skillCatalog.GetEffectiveCombatDefinition("basic_attack", 1);
        _test.True(
            !ReferenceEquals(beforeDefinition, afterDefinition),
            "catalog revision 变化后 runtime effective definition cache 应失效并重建。"
        );
        _test.True(
            !afterDefinition.HasCombatProfile,
            "catalog clear 后 runtime effective definition 不应返回旧 SkillDefinition/combat profile。"
        );
    }

    private void AssertEffectiveGetterFacadeMatchesProfile(
        ISkillCatalog skillCatalog,
        StringName skillId,
        int level,
        SkillEffectiveCombatDefinition effectiveDefinition
    )
    {
        AssertCostsEq(
            skillCatalog.GetEffectiveResourceCostValues(skillId, level),
            effectiveDefinition.ResourceCosts,
            $"{skillId}@L{level} 的消耗 getter 应读取聚合 runtime effective definition。"
        );
        _test.Eq(
            skillCatalog.GetEffectiveAttackRollBonus(skillId, level),
            effectiveDefinition.AttackRollBonus,
            $"{skillId}@L{level} 的命中 getter 应读取聚合 runtime effective definition。"
        );
        _test.Eq(
            skillCatalog.GetEffectiveRangeValue(skillId, level),
            effectiveDefinition.RangeValue,
            $"{skillId}@L{level} 的射程 getter 应读取聚合 runtime effective definition。"
        );
        _test.Eq(
            skillCatalog.GetEffectiveAreaValue(skillId, level),
            effectiveDefinition.AreaValue,
            $"{skillId}@L{level} 的范围值 getter 应读取聚合 runtime effective definition。"
        );
        _test.Eq(
            skillCatalog.GetEffectiveMaxTargetCount(skillId, level),
            effectiveDefinition.MaxTargetCount,
            $"{skillId}@L{level} 的最大目标数 getter 应读取聚合 runtime effective definition。"
        );
        _test.Eq(
            skillCatalog.GetEffectiveAreaPattern(skillId, level),
            effectiveDefinition.AreaPattern,
            $"{skillId}@L{level} 的范围模式 getter 应读取聚合 runtime effective definition。"
        );
        _test.True(
            ReferenceEquals(
                skillCatalog.GetUnlockedCastVariantDefinitions(skillId, level),
                effectiveDefinition.UnlockedCastVariants
            ),
            $"{skillId}@L{level} 的施法变体 getter 应读取聚合 runtime effective definition。"
        );
    }

    private void AssertRuntimeEffectiveDefinitionMatchesProfile(
        SkillEffectiveCombatDefinition actual,
        CombatSkillDefinition expected,
        string message
    )
    {
        _test.Eq(
            actual.CastingTimeTu,
            expected.GetEffectiveCastingTimeTu(actual.SkillLevel),
            $"{message} casting_time_tu 应对拍一致。"
        );
        _test.Eq(
            actual.CastingMaintenanceDc,
            expected.GetEffectiveCastingMaintenanceDc(actual.SkillLevel),
            $"{message} casting_maintenance_dc 应对拍一致。"
        );
        _test.Eq(
            actual.CastingSpellControlDc,
            expected.GetEffectiveCastingSpellControlDc(actual.SkillLevel),
            $"{message} casting_spell_control_dc 应对拍一致。"
        );
        _test.Eq(
            actual.PendingCastBindingMode,
            expected.GetEffectivePendingCastBindingMode(actual.SkillLevel),
            $"{message} pending_cast_binding_mode 应对拍一致。"
        );
        _test.Eq(
            actual.FumbleProtectionLimit,
            expected.GetFumbleProtectionLimit(actual.SkillLevel),
            $"{message} fumble protection limit 应对拍一致。"
        );
        _test.Eq(
            actual.HasSpellFateControl,
            expected.HasSpellFateControl(),
            $"{message} spell fate control 应对拍一致。"
        );
        _test.Eq(
            actual.UsesGroundAnchorDriftBacklash,
            expected.UsesGroundAnchorDriftBacklash(),
            $"{message} backlash mode 应对拍一致。"
        );
    }

    private void AssertRuntimeEffectDefinitionMatchesResource(
        CombatEffectDefinition actual,
        CombatEffectDef expected,
        string message
    )
    {
        _test.Eq(actual.DamageTag, expected.damage_tag, $"{message} damage_tag");
        _test.Eq(
            actual.DamageRatioPercent,
            expected.damage_ratio_percent,
            $"{message} damage_ratio_percent"
        );
        _test.Eq(
            actual.PreResistanceDamageMultiplier,
            expected.pre_resistance_damage_multiplier,
            $"{message} pre_resistance_damage_multiplier"
        );
        _test.Eq(actual.DamageCategory, expected.damage_category, $"{message} damage_category");
        _test.Eq(actual.DrBypassTag, expected.dr_bypass_tag, $"{message} dr_bypass_tag");
        _test.Eq(actual.DiceCount, expected.dice_count, $"{message} dice_count");
        _test.Eq(actual.DiceSides, expected.dice_sides, $"{message} dice_sides");
        _test.Eq(actual.DiceBonus, expected.dice_bonus, $"{message} dice_bonus");
        _test.Eq(actual.SaveDc, expected.save_dc, $"{message} save_dc");
        _test.Eq(actual.SaveDcMode, expected.save_dc_mode, $"{message} save_dc_mode");
        _test.Eq(
            actual.SaveDcSourceAbility,
            expected.save_dc_source_ability,
            $"{message} save_dc_source_ability"
        );
        _test.Eq(actual.SaveAbility, expected.save_ability, $"{message} save_ability");
        _test.Eq(
            actual.SavePartialOnSuccess,
            expected.save_partial_on_success,
            $"{message} save_partial_on_success"
        );
        _test.Eq(actual.SaveTag, expected.save_tag, $"{message} save_tag");
        _test.Eq(
            actual.AppliedStatusDurationTu,
            expected.applied_status_duration_tu,
            $"{message} applied_status_duration_tu"
        );
        _test.Eq(actual.DurationTu, expected.duration_tu, $"{message} duration_tu");
        _test.Eq(actual.TickIntervalTu, expected.tick_interval_tu, $"{message} tick_interval_tu");
        _test.Eq(actual.EffectTags.Count, expected.effect_tags.Count, $"{message} effect_tags count");
        for (int i = 0; i < actual.EffectTags.Count && i < expected.effect_tags.Count; i++)
            _test.Eq(actual.EffectTags[i], expected.effect_tags[i], $"{message} effect_tags[{i}]");
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
        IReadOnlyList<CombatCastVariantDefinition> expected,
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
                expected[i]?.VariantId ?? "",
                $"{message} index={i} variant_id"
            );
            _test.Eq(
                actual[i]?.MinSkillLevel ?? -1,
                expected[i]?.MinSkillLevel ?? -1,
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
