using System.Collections.Generic;
using Godot;

/// <summary>
/// 回归：以固定合成技能验证 <see cref="ISkillCatalog"/> 的查询、等级覆盖、施法语义、
/// 效果投影、安全缺省值与 revision 失效行为；预期值独立写明，不依赖两套 getter 互相对拍。
/// </summary>
public partial class run_skill_catalog_query_regression : LifecycleTestSceneTree
{
    private static readonly StringName SyntheticSkillId = "skill_catalog_fixed_oracle";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        GameSession gameSession = CreateSyntheticCatalogSession();
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

            TestHasSkillAndTryGet(skillCatalog);
            TestRuntimeCombatEffectiveSemanticsUseFixedValues();
            TestEffectiveGettersUseFixedSyntheticSkill(skillCatalog);
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

    private void TestRuntimeCombatEffectiveSemanticsUseFixedValues()
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

        AssertFixedCastingSemantics(
            resourceCombat,
            runtimeCombat,
            level: 1,
            castingTimeTu: 3,
            maintenanceDc: 5,
            spellControlDc: 9,
            bindingMode: PendingCastBindingModeKind.HardAnchor,
            fumbleProtectionLimit: 1
        );
        AssertFixedCastingSemantics(
            resourceCombat,
            runtimeCombat,
            level: 2,
            castingTimeTu: 7,
            maintenanceDc: 11,
            spellControlDc: 13,
            bindingMode: PendingCastBindingModeKind.GroundBind,
            fumbleProtectionLimit: 2
        );
        AssertFixedCastingSemantics(
            resourceCombat,
            runtimeCombat,
            level: 4,
            castingTimeTu: 7,
            maintenanceDc: 11,
            spellControlDc: 13,
            bindingMode: PendingCastBindingModeKind.GroundBind,
            fumbleProtectionLimit: 3
        );

        _test.True(runtimeCombat.HasSpellFateControl(), "control_roll 应启用 spell fate control。");
        _test.True(
            runtimeCombat.UsesGroundAnchorDriftBacklash(),
            "ground_anchor_drift 应启用落点漂移 backlash。"
        );
        _test.Eq(
            runtimeCombat.SpellFateModeKind,
            CombatSpellFateMode.ControlRoll,
            "spell_fate_mode 应投影为 ControlRoll。"
        );
        _test.Eq(
            runtimeCombat.BacklashModeKind,
            CombatSkillBacklashMode.GroundAnchorDrift,
            "backlash_mode 应投影为 GroundAnchorDrift。"
        );
        _test.Eq(
            runtimeCombat.AreaOriginModeKind,
            CombatAreaOriginMode.AnchorCoord,
            "area_origin_mode 应投影为 AnchorCoord。"
        );
        _test.Eq(
            runtimeCombat.AreaDirectionModeKind,
            CombatAreaDirectionMode.CasterFacing,
            "area_direction_mode 应投影为 CasterFacing。"
        );
        AssertRuntimeEffectDefinitionMatchesFixedFixture(runtimeCombat.EffectDefinitions[0]);
    }

    private void TestEffectiveGettersUseFixedSyntheticSkill(ISkillCatalog skillCatalog)
    {
        _test.True(skillCatalog.HasSkill(SyntheticSkillId), "synthetic fixed-oracle skill 应存在。");
        _test.True(
            skillCatalog.TryGetSkillDefinition(SyntheticSkillId, out SkillDefinition skillDefinition)
                && skillDefinition?.CombatProfile != null,
            "synthetic fixed-oracle skill 应能通过正式 catalog 查询取得 combat profile。"
        );
        if (skillDefinition?.CombatProfile == null)
            return;

        _test.Eq(skillDefinition.DisplayName, "Fixed Oracle Skill", "catalog 应返回固定显示名。");
        AssertFixedCatalogEffectiveDefinition(
            skillCatalog,
            level: 1,
            expectedCosts: new CombatSkillResourceCosts(2, 30, 4, 5, 60),
            attackRollBonus: 1,
            rangeValue: 2,
            areaValue: 1,
            maxTargetCount: 2,
            areaPattern: "diamond",
            castingTimeTu: 3,
            castingMaintenanceDc: 5,
            castingSpellControlDc: 9,
            bindingMode: PendingCastBindingModeKind.HardAnchor
        );
        AssertFixedCatalogEffectiveDefinition(
            skillCatalog,
            level: 3,
            expectedCosts: new CombatSkillResourceCosts(3, 20, 6, 7, 40),
            attackRollBonus: 4,
            rangeValue: 5,
            areaValue: 2,
            maxTargetCount: 4,
            areaPattern: "radius",
            castingTimeTu: 7,
            castingMaintenanceDc: 11,
            castingSpellControlDc: 13,
            bindingMode: PendingCastBindingModeKind.GroundBind
        );
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
        _ = skillCatalog.GetEffectiveCombatDefinition("basic_attack", 1);
        long beforeRevision = skillCatalog.GetRevision();

        contentCatalog.ClearSessionBinding();

        _test.True(
            skillCatalog.GetRevision() > beforeRevision,
            "content catalog clear 后 skill catalog revision 应前进。"
        );
        SkillEffectiveCombatDefinition afterDefinition =
            skillCatalog.GetEffectiveCombatDefinition("basic_attack", 1);
        _test.True(
            !afterDefinition.HasCombatProfile,
            "catalog revision 变化后查询结果不应残留旧 SkillDefinition/combat profile。"
        );
    }

    private void AssertFixedCastingSemantics(
        CombatSkillDef resourceCombat,
        CombatSkillDefinition runtimeCombat,
        int level,
        int castingTimeTu,
        int maintenanceDc,
        int spellControlDc,
        PendingCastBindingModeKind bindingMode,
        int fumbleProtectionLimit
    )
    {
        _test.Eq(
            resourceCombat.GetEffectiveCastingTimeTu(level),
            castingTimeTu,
            $"Resource@L{level} casting_time_tu 应命中固定 oracle。"
        );
        _test.Eq(
            runtimeCombat.GetEffectiveCastingTimeTu(level),
            castingTimeTu,
            $"Runtime@L{level} casting_time_tu 应命中固定 oracle。"
        );
        _test.Eq(
            resourceCombat.GetEffectiveCastingMaintenanceDc(level),
            maintenanceDc,
            $"Resource@L{level} casting_maintenance_dc 应命中固定 oracle。"
        );
        _test.Eq(
            runtimeCombat.GetEffectiveCastingMaintenanceDc(level),
            maintenanceDc,
            $"Runtime@L{level} casting_maintenance_dc 应命中固定 oracle。"
        );
        _test.Eq(
            resourceCombat.GetEffectiveCastingSpellControlDc(level),
            spellControlDc,
            $"Resource@L{level} casting_spell_control_dc 应命中固定 oracle。"
        );
        _test.Eq(
            runtimeCombat.GetEffectiveCastingSpellControlDc(level),
            spellControlDc,
            $"Runtime@L{level} casting_spell_control_dc 应命中固定 oracle。"
        );
        _test.Eq(
            resourceCombat.GetEffectivePendingCastBindingMode(level),
            bindingMode,
            $"Resource@L{level} pending_cast_binding_mode 应命中固定 oracle。"
        );
        _test.Eq(
            runtimeCombat.GetEffectivePendingCastBindingMode(level),
            bindingMode,
            $"Runtime@L{level} pending_cast_binding_mode 应命中固定 oracle。"
        );
        _test.Eq(
            resourceCombat.GetFumbleProtectionLimit(level),
            fumbleProtectionLimit,
            $"Resource@L{level} fumble protection 应命中固定 oracle。"
        );
        _test.Eq(
            runtimeCombat.GetFumbleProtectionLimit(level),
            fumbleProtectionLimit,
            $"Runtime@L{level} fumble protection 应命中固定 oracle。"
        );
        _test.True(resourceCombat.HasCastingTime(level), $"Resource@L{level} 应有施法时间。");
        _test.True(runtimeCombat.HasCastingTime(level), $"Runtime@L{level} 应有施法时间。");
    }

    private void AssertRuntimeEffectDefinitionMatchesFixedFixture(CombatEffectDefinition actual)
    {
        _test.True(actual != null, "CombatEffectDefinition 固定 fixture 应完成投影。");
        if (actual == null)
            return;
        _test.Eq(actual.DamageTag, new StringName("fire"), "damage_tag 应为 fire。");
        _test.Eq(actual.DamageRatioPercent, 75, "damage_ratio_percent 应为 75。");
        _test.Eq(
            actual.PreResistanceDamageMultiplier,
            1.5,
            "pre_resistance_damage_multiplier 应为 1.5。"
        );
        _test.Eq(actual.DamageCategory, new StringName("elemental"), "damage_category 应为 elemental。");
        _test.Eq(actual.DrBypassTag, new StringName("magic"), "dr_bypass_tag 应为 magic。");
        _test.Eq(actual.DiceCount, 3, "dice_count 应为 3。");
        _test.Eq(actual.DiceSides, 8, "dice_sides 应为 8。");
        _test.Eq(actual.DiceBonus, 2, "dice_bonus 应为 2。");
        _test.Eq(actual.SaveDc, 14, "save_dc 应为 14。");
        _test.Eq(actual.SaveDcMode, new StringName("caster_spell"), "save_dc_mode 应为 caster_spell。");
        _test.Eq(
            actual.SaveDcSourceAbility,
            new StringName("intelligence"),
            "save_dc_source_ability 应为 intelligence。"
        );
        _test.Eq(actual.SaveAbility, new StringName("agility"), "save_ability 应为 agility。");
        _test.True(actual.SavePartialOnSuccess, "save_partial_on_success 应为 true。");
        _test.Eq(actual.SaveTag, new StringName("fireball"), "save_tag 应为 fireball。");
        _test.Eq(actual.AppliedStatusDurationTu, 40, "applied_status_duration_tu 应为 40。");
        _test.Eq(actual.DurationTu, 60, "duration_tu 应为 60。");
        _test.Eq(actual.TickIntervalTu, 10, "tick_interval_tu 应为 10。");
        _test.Eq(actual.EffectTags.Count, 2, "effect_tags 应有两个固定值。");
        if (actual.EffectTags.Count == 2)
        {
            _test.Eq(actual.EffectTags[0], new StringName("fire"), "effect_tags[0] 应为 fire。");
            _test.Eq(actual.EffectTags[1], new StringName("dot"), "effect_tags[1] 应为 dot。");
        }
    }

    private void AssertFixedCatalogEffectiveDefinition(
        ISkillCatalog skillCatalog,
        int level,
        CombatSkillResourceCosts expectedCosts,
        int attackRollBonus,
        int rangeValue,
        int areaValue,
        int maxTargetCount,
        StringName areaPattern,
        int castingTimeTu,
        int castingMaintenanceDc,
        int castingSpellControlDc,
        PendingCastBindingModeKind bindingMode
    )
    {
        SkillEffectiveCombatDefinition effectiveDefinition =
            skillCatalog.GetEffectiveCombatDefinition(SyntheticSkillId, level);
        _test.True(effectiveDefinition?.HasCombatProfile == true, $"synthetic skill@L{level} 应有 combat profile。");
        if (effectiveDefinition == null)
            return;

        _test.Eq(effectiveDefinition.SkillLevel, level, $"synthetic skill 应保留 L{level}。");
        AssertCostsEq(
            effectiveDefinition.ResourceCosts,
            expectedCosts,
            $"synthetic skill@L{level} effective definition 消耗应命中固定 oracle。"
        );
        _test.Eq(effectiveDefinition.AttackRollBonus, attackRollBonus, $"synthetic skill@L{level} attack bonus。");
        _test.Eq(effectiveDefinition.RangeValue, rangeValue, $"synthetic skill@L{level} range。");
        _test.Eq(effectiveDefinition.AreaValue, areaValue, $"synthetic skill@L{level} area value。");
        _test.Eq(effectiveDefinition.MaxTargetCount, maxTargetCount, $"synthetic skill@L{level} max targets。");
        _test.Eq(effectiveDefinition.AreaPattern, areaPattern, $"synthetic skill@L{level} area pattern。");
        _test.Eq(effectiveDefinition.CastingTimeTu, castingTimeTu, $"synthetic skill@L{level} casting time。");
        _test.Eq(
            effectiveDefinition.CastingMaintenanceDc,
            castingMaintenanceDc,
            $"synthetic skill@L{level} maintenance dc。"
        );
        _test.Eq(
            effectiveDefinition.CastingSpellControlDc,
            castingSpellControlDc,
            $"synthetic skill@L{level} spell control dc。"
        );
        _test.Eq(
            effectiveDefinition.PendingCastBindingMode,
            bindingMode,
            $"synthetic skill@L{level} pending binding。"
        );
        _test.Eq(effectiveDefinition.UnlockedCastVariants.Count, 0, $"synthetic skill@L{level} 不应有 cast variant。");

        AssertCostsEq(
            skillCatalog.GetEffectiveResourceCostValues(SyntheticSkillId, level),
            expectedCosts,
            $"catalog cost getter@L{level} 应命中固定 oracle。"
        );
        _test.Eq(
            skillCatalog.GetEffectiveAttackRollBonus(SyntheticSkillId, level),
            attackRollBonus,
            $"catalog attack getter@L{level} 应命中固定 oracle。"
        );
        _test.Eq(
            skillCatalog.GetEffectiveRangeValue(SyntheticSkillId, level),
            rangeValue,
            $"catalog range getter@L{level} 应命中固定 oracle。"
        );
        _test.Eq(
            skillCatalog.GetEffectiveAreaValue(SyntheticSkillId, level),
            areaValue,
            $"catalog area getter@L{level} 应命中固定 oracle。"
        );
        _test.Eq(
            skillCatalog.GetEffectiveMaxTargetCount(SyntheticSkillId, level),
            maxTargetCount,
            $"catalog max-target getter@L{level} 应命中固定 oracle。"
        );
        _test.Eq(
            skillCatalog.GetEffectiveAreaPattern(SyntheticSkillId, level),
            areaPattern,
            $"catalog area-pattern getter@L{level} 应命中固定 oracle。"
        );
        _test.Eq(
            skillCatalog.GetUnlockedCastVariantDefinitions(SyntheticSkillId, level).Count,
            0,
            $"catalog variant getter@L{level} 应返回固定空集合。"
        );
    }

    private static GameSession CreateSyntheticCatalogSession()
    {
        var levelOverrides = new Dictionary<int, IReadOnlyDictionary<string, object>>
        {
            [3] = new Dictionary<string, object>
            {
                ["ap_cost"] = 3,
                ["mp_cost"] = 20,
                ["stamina_cost"] = 6,
                ["aura_cost"] = 7,
                ["cooldown_tu"] = 40,
                ["attack_roll_bonus"] = 4,
                ["range_value"] = 5,
                ["area_value"] = 2,
                ["max_target_count"] = 4,
                ["area_pattern"] = new StringName("radius"),
                ["casting_time_tu"] = 7,
                ["casting_maintenance_dc"] = 11,
                ["casting_spell_control_dc"] = 13,
                ["pending_cast_binding_mode"] = new StringName("ground_bind"),
            },
        };
        CombatSkillDefinition combatProfile = TestSkillDefinitionProjection.BuildCombatProfile(
            SyntheticSkillId,
            targetMode: "ground",
            targetTeamFilter: "enemy",
            rangeValue: 2,
            apCost: 2,
            mpCost: 30,
            staminaCost: 4,
            auraCost: 5,
            cooldownTu: 60,
            castingTimeTu: 3,
            castingMaintenanceDc: 5,
            castingSpellControlDc: 9,
            pendingCastBindingMode: "hard_anchor",
            attackRollBonus: 1,
            areaPattern: "diamond",
            areaValue: 1,
            maxTargetCount: 2,
            levelOverrides: levelOverrides
        );
        SkillDefinition skillDefinition = TestSkillDefinitionProjection.BuildSkill(
            SyntheticSkillId,
            displayName: "Fixed Oracle Skill",
            combatProfile: combatProfile,
            maxLevel: 3
        );
        return GameSessionTestFactory.CreateSyntheticFromProcessSnapshot(
            seed => seed.Skills = CopyWithEntry(seed.Skills, SyntheticSkillId, skillDefinition)
        );
    }

    private static IReadOnlyDictionary<StringName, T> CopyWithEntry<T>(
        IReadOnlyDictionary<StringName, T> source,
        StringName key,
        T value
    )
        where T : class
    {
        var copy = source == null
            ? new Dictionary<StringName, T>()
            : new Dictionary<StringName, T>(source);
        copy[key] = value;
        return copy;
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
