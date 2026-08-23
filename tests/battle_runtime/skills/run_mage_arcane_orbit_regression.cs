using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class run_mage_arcane_orbit_regression : LifecycleTestSceneTree
{
    private const string SkillPath = "mage_arcane_orbit";
    private static readonly StringName SkillId = "mage_arcane_orbit";
    private static readonly StringName ReadyStatusId = "arcane_orbit_ready";
    private static readonly StringName ShotSkillId = "test_arcane_orbit_three_w_shot";
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            SkillDefinition skill = LoadSkillDefinition();
            TestAuthoredSchemaAndTypedContract(skill);
            TestLevelCurveAndDescriptions(skill);
            TestCanonicalPreviewAndRecastRefill(skill);
            TestThreeWMainWeaponDiceReactionAndCriticalIsolation(skill);
            TestOriginalMissStillConsumesOneOrb(skill);
            TestNonConfiguredRangedWeaponDoesNotTrigger(skill);
            TestAiScoresOnlyMarginalBowAndCrossbowThreat(skill);
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        RequestTestExit(_test.Finish("Mage arcane orbit regression"));
    }

    private void TestAuthoredSchemaAndTypedContract(SkillDefinition skill)
    {
        CombatSkillDefinition combat = skill?.CombatProfile;
        CombatRangedWeaponReactionDefinition reaction = combat?.RangedWeaponReaction;
        _test.True(skill != null && combat != null, "轨道法珠必须投影为 immutable skill definition。" );
        _test.True(reaction != null, "轨道法珠必须投影 typed ranged weapon reaction profile。" );
        if (skill == null || combat == null || reaction == null)
            return;

        _test.Eq(skill.MaxLevel, 7, "轨道法珠最高等级必须为7。" );
        _test.Eq(skill.NonCoreMaxLevel, 5, "非核心最高等级必须为5。" );
        _test.Eq(combat.TargetFilterKind, BattleTargetFilter.Self, "施法目标必须是自身。" );
        _test.Eq(combat.TargetSelectionModeKind, BattleTargetSelectionMode.Self, "不得要求选择敌人。" );
        _test.Eq(combat.ApCost, 1, "基础AP消耗必须为1。" );
        _test.Eq(combat.MpCost, 100, "基础法力消耗必须为100。" );
        _test.Eq(combat.CooldownTu, 240, "基础冷却必须为240TU。" );
        _test.Eq(combat.AttackResolutionModeKind, CombatSkillAttackResolutionMode.DirectEffect, "布置法珠本身不进行攻击检定。" );
        _test.Eq(reaction.AttackDefenseModeKind, CombatSkillAttackDefenseMode.Touch, "反击必须攻击接触AC。" );
        _test.True(reaction.SupportsWeaponFamily("bow"), "弓必须触发轨道法珠。" );
        _test.True(reaction.SupportsWeaponFamily("crossbow"), "弩必须触发轨道法珠。" );
        _test.False(reaction.SupportsWeaponFamily("thrown"), "投掷武器不得触发轨道法珠。" );
        _test.True(reaction.TriggerOnHit && reaction.TriggerOnMiss, "原攻击命中和未命中都必须触发。" );
        _test.False(reaction.AllowCritical, "法珠反击必须禁止暴击。" );
        _test.Eq(reaction.DamageTag, new StringName("force"), "复制骰必须转为力场伤害。" );
        _test.True(skill.Description.Contains("主武器骰"), "描述必须披露只复制主武器骰。" );
        _test.True(skill.Description.Contains("不能暴击"), "描述必须披露反击不能暴击。" );
        _test.True(skill.Description.Contains("不重新检查距离或视线"), "描述必须披露沿用已成立弹道。" );
        _test.True(skill.Description.Contains("补满"), "描述必须披露重施补珠规则。" );
    }

    private void TestLevelCurveAndDescriptions(SkillDefinition skill)
    {
        int[] expectedOrbs = { 2, 2, 2, 3, 3, 3, 3, 4 };
        int[] expectedDuration = { 60, 60, 75, 75, 75, 75, 90, 90 };
        int[] expectedBonus = { 0, 1, 1, 1, 2, 2, 2, 3 };
        int[] expectedMp = { 100, 100, 100, 100, 100, 90, 90, 90 };
        int[] expectedCooldown = { 240, 240, 240, 210, 210, 210, 210, 180 };
        CombatRangedWeaponReactionDefinition reaction =
            skill.CombatProfile.RangedWeaponReaction;
        for (int level = 0; level <= 7; level++)
        {
            SkillEffectiveCombatDefinition effective =
                SkillEffectiveCombatDefinition.BuildUncached(skill, level);
            CombatEffectDefinition readiness = FindReadinessEffect(skill, level);
            _test.True(readiness != null, $"L{level}必须恰有一个法珠状态。" );
            _test.Eq(readiness?.Power ?? -1, expectedOrbs[level], $"L{level}法珠数量不符。" );
            _test.Eq(readiness?.DurationTu ?? -1, expectedDuration[level], $"L{level}持续时间不符。" );
            _test.Eq(reaction.GetAttackRollBonus(level), expectedBonus[level], $"L{level}反击命中加值不符。" );
            _test.Eq(effective.ResourceCosts.ApCost, 1, $"L{level}AP必须保持1。" );
            _test.Eq(effective.ResourceCosts.MpCost, expectedMp[level], $"L{level}法力消耗不符。" );
            _test.Eq(effective.ResourceCosts.CooldownTu, expectedCooldown[level], $"L{level}冷却不符。" );
            _test.True(expectedDuration[level] <= 90, $"L{level}持续时间不得超过90TU。" );
            _test.True(expectedCooldown[level] >= 180, $"L{level}冷却不得低于180TU。" );
            string description = SkillLevelDescriptionFormatter.BuildLevelDescription(
                skill,
                level,
                new Godot.Collections.Dictionary()
            );
            _test.True(description.Contains($"{expectedOrbs[level]}枚"), $"L{level}描述必须显示法珠数。" );
            _test.True(description.Contains($"{expectedDuration[level]}TU"), $"L{level}描述必须显示持续时间。" );
            _test.True(description.Contains("主武器骰W倍率"), $"L{level}描述必须披露动态伤害公式。" );
        }
    }

    private void TestCanonicalPreviewAndRecastRefill(SkillDefinition skill)
    {
        SkillDefinition shot = BuildThreeWShotSkill();
        BattleUnitState mage = BuildMage("orbit_preview_mage", Vector2I.Zero, 3);
        using BattleTestFixture fixture = CreateFixture(skill, shot, mage);
        fixture.State.active_unit_id = mage.unit_id;
        BattleCommand command = BuildSelfCommand(mage.unit_id);
        try
        {
            BattlePreview preview = fixture.Runtime.PreviewCommand(command);
            _test.True(preview.allowed, "轨道法珠自我施法必须通过 canonical preview。" );
            BattleRangedWeaponReactionPreviewData reactionPreview =
                preview.RangedWeaponReactionPreviewTyped;
            _test.True(reactionPreview != null, "canonical preview必须投影反应专用事实。" );
            _test.Eq(reactionPreview?.OrbCount ?? -1, 3, "L3预览必须显示3枚法珠。" );
            _test.Eq(reactionPreview?.DurationTu ?? -1, 75, "L3预览必须显示75TU。" );
            _test.Eq(reactionPreview?.AttackRollBonus ?? -1, 1, "L3预览必须显示+1。" );
            _test.True(reactionPreview?.SummaryText.Contains("独立接触攻击") == true, "预览必须披露独立接触攻击。" );
            using GodotProjectionLease<Godot.Collections.Dictionary> lease =
                BattlePreviewProjection.BuildLease(preview);
            Godot.Collections.Dictionary projected =
                lease.Value["ranged_weapon_reaction_preview"].AsGodotDictionary();
            _test.Eq(projected["orb_count"].AsInt32(), 3, "Godot preview projection必须保留法珠数。" );
            _test.False(projected["allow_critical"].AsBool(), "Godot preview projection必须保留禁暴击。" );

            fixture.Runtime.IssueCommand(command);
            BattleStatusEffectState armed = mage.GetStatusEffect(ReadyStatusId);
            _test.Eq(armed?.stacks ?? -1, 3, "首次施放必须获得3枚法珠。" );
            _test.Eq(armed?.duration ?? -1, 75, "首次施放必须获得75TU持续时间。" );

            mage.EraseStatusEffect(ReadyStatusId);
            Ready(mage, 3, 1, 10);
            mage.ResetTurnStateForTurnStartTyped();
            mage.SetCooldownTyped(SkillId, 0);
            mage.SetCombatResources(
                mage.GetCurrentHp(),
                mp: 100,
                stamina: mage.GetCurrentStamina(),
                aura: mage.GetCurrentAura(),
                ap: 2,
                movePoints: mage.GetCurrentMovePoints()
            );
            fixture.State.active_unit_id = mage.unit_id;
            fixture.State.PhaseKind = BattlePhaseKind.UnitActing;
            BattleCommand recastCommand = BuildSelfCommand(mage.unit_id);
            try
            {
                BattlePreview recastPreview = fixture.Runtime.PreviewCommand(recastCommand);
                _test.True(
                    recastPreview.allowed,
                    $"重施前置条件必须通过 canonical preview。logs={string.Join(" | ", recastPreview.LogLinesTyped)}"
                );
                fixture.Runtime.IssueCommand(recastCommand);
            }
            finally
            {
                BattleTestFixture.DisposeBattleCommand(recastCommand);
            }
            BattleStatusEffectState refilled = mage.GetStatusEffect(ReadyStatusId);
            _test.Eq(refilled?.stacks ?? -1, 3, "重施必须补满至当前等级上限而非叠加第二实例。" );
            _test.Eq(refilled?.duration ?? -1, 75, "重施必须刷新完整持续时间。" );
            _test.Eq(mage.GetSortedStatusEffectIdsTyped().Count(id => id == ReadyStatusId), 1, "重施后只能有一个法珠状态实例。" );
        }
        finally
        {
            BattleTestFixture.DisposeBattleCommand(command);
        }
    }

    private void TestThreeWMainWeaponDiceReactionAndCriticalIsolation(
        SkillDefinition skill
    )
    {
        SkillDefinition shot = BuildThreeWShotSkill();
        BattleUnitState mage = BuildMage("orbit_three_w_mage", Vector2I.Zero, 3);
        BattleUnitState archer = BuildRangedAttacker(
            "orbit_three_w_archer",
            "bow",
            new Vector2I(2, 0),
            shot,
            diceCount: 2,
            diceSides: 6,
            flatBonus: 9
        );
        using BattleTestFixture fixture = CreateFixture(skill, shot, mage, archer);
        fixture.Runtime.ConfigureDamageResolverForTests(new FixedHitMaxDamageResolver());
        fixture.Runtime.ConfigureHitResolverForTests(new FixedHitResolver());
        Ready(mage, 3, 3, 75);
        int archerHpBefore = archer.GetCurrentHp();
        using var batch = new BattleEventBatch();
        AttackCheckInput forcedCritical = new(
            forceCriticalOnHit: true,
            skillId: shot.SkillId
        );
        AttackEffectResolutionResult original = fixture.Runtime._damage_resolver.ResolveAttackEffects(
            archer,
            mage,
            shot.CombatProfile.EffectDefinitions,
            forcedCritical,
            new AttackContext
            {
                BattleState = fixture.State,
                SkillId = shot.SkillId,
                EventBatch = batch,
            }
        );

        _test.True(original.CriticalHit, "负向控制：原3W弓击必须确实按暴击结算。" );
        _test.Eq(
            archerHpBefore - archer.GetCurrentHp(),
            36,
            "2D6武器的3W反击必须仅为6D6；不得复制9点固定加值、2W附加骰或原攻击暴击额外骰。"
        );
        _test.Eq(mage.GetStatusEffect(ReadyStatusId)?.stacks ?? -1, 2, "一个独立攻击检定只能消耗1枚法珠。" );
        _test.True(LogsContain(batch.log_lines, "6D6"), "战斗日志必须显示快照后的6D6反击骰。" );
        _test.True(LogsContain(batch.log_lines, "不能暴击"), "战斗日志必须披露反击禁暴击。" );
    }

    private void TestOriginalMissStillConsumesOneOrb(SkillDefinition skill)
    {
        SkillDefinition shot = BuildThreeWShotSkill();
        BattleUnitState mage = BuildMage("orbit_miss_mage", Vector2I.Zero, 3);
        BattleUnitState archer = BuildRangedAttacker(
            "orbit_miss_archer",
            "crossbow",
            new Vector2I(2, 0),
            shot
        );
        using BattleTestFixture fixture = CreateFixture(skill, shot, mage, archer);
        fixture.Runtime.ConfigureDamageResolverForTests(new FixedHitMaxDamageResolver());
        fixture.Runtime.ConfigureHitResolverForTests(new FixedMissResolver());
        Ready(mage, 3, 3, 75);
        int mageHpBefore = mage.GetCurrentHp();
        int archerHpBefore = archer.GetCurrentHp();
        using var batch = new BattleEventBatch();
        AttackEffectResolutionResult original = fixture.Runtime._damage_resolver.ResolveAttackEffects(
            archer,
            mage,
            shot.CombatProfile.EffectDefinitions,
            new AttackCheckInput(skillId: shot.SkillId),
            new AttackContext
            {
                BattleState = fixture.State,
                SkillId = shot.SkillId,
                EventBatch = batch,
            }
        );
        _test.False(original.AttackSuccess, "负向控制：原弩击必须确实未命中。" );
        _test.Eq(mage.GetCurrentHp(), mageHpBefore, "未命中的原攻击不得造成伤害。" );
        _test.Eq(archer.GetCurrentHp(), archerHpBefore, "固定未命中的反击也不得造成伤害。" );
        _test.Eq(mage.GetStatusEffect(ReadyStatusId)?.stacks ?? -1, 2, "原攻击未命中仍必须触发并消耗1枚法珠。" );
        _test.True(LogsContain(batch.log_lines, "弩攻击触发"), "弩攻击必须进入正式反应日志。" );
    }

    private void TestNonConfiguredRangedWeaponDoesNotTrigger(SkillDefinition skill)
    {
        SkillDefinition shot = BuildThreeWShotSkill();
        BattleUnitState mage = BuildMage("orbit_filter_mage", Vector2I.Zero, 3);
        BattleUnitState thrower = BuildRangedAttacker(
            "orbit_filter_thrower",
            "thrown",
            new Vector2I(2, 0),
            shot
        );
        using BattleTestFixture fixture = CreateFixture(skill, shot, mage, thrower);
        fixture.Runtime.ConfigureDamageResolverForTests(new FixedHitMaxDamageResolver());
        fixture.Runtime.ConfigureHitResolverForTests(new FixedHitResolver());
        Ready(mage, 3, 3, 75);
        int throwerHpBefore = thrower.GetCurrentHp();
        fixture.Runtime._damage_resolver.ResolveAttackEffects(
            thrower,
            mage,
            shot.CombatProfile.EffectDefinitions,
            new AttackCheckInput(skillId: shot.SkillId),
            new AttackContext { BattleState = fixture.State, SkillId = shot.SkillId }
        );
        _test.Eq(mage.GetStatusEffect(ReadyStatusId)?.stacks ?? -1, 3, "投掷远程武器不得消耗法珠。" );
        _test.Eq(thrower.GetCurrentHp(), throwerHpBefore, "投掷远程武器不得受到法珠反击。" );
    }

    private void TestAiScoresOnlyMarginalBowAndCrossbowThreat(SkillDefinition skill)
    {
        SkillDefinition shot = BuildThreeWShotSkill();
        BattleUnitState mage = BuildMage("orbit_ai_mage", Vector2I.Zero, 3);
        BattleUnitState archer = BuildRangedAttacker(
            "orbit_ai_archer",
            "bow",
            new Vector2I(2, 0),
            shot,
            diceCount: 2,
            diceSides: 6
        );
        using BattleTestFixture fixture = CreateFixture(skill, shot, mage, archer);
        using var scoreService = new BattleAiScoreService();
        var definitions = new Dictionary<StringName, SkillDefinition>
        {
            [skill.SkillId] = skill,
            [shot.SkillId] = shot,
        };
        var context = new BattleAiContext
        {
            state = fixture.State,
            unit_state = mage,
            grid_service = fixture.Runtime._grid_service,
        };
        context.SetSkillDefinitions(definitions);
        BattleCommand command = BuildSelfCommand(mage.unit_id);
        try
        {
            BattleAiScoreInput againstBow = scoreService.BuildSkillScoreInput(
                context,
                skill,
                command,
                BuildSelfPreview(mage),
                skill.CombatProfile.EffectDefinitions,
                BuildAiMetadata()
            );
            _test.True(againstBow.estimated_damage > 0, "射程内存在弓手时AI必须估算边际反击伤害。" );
            _test.Eq(againstBow.estimated_control_count, 0, "AI不得把法珠计为普通控制。" );
            _test.Eq(againstBow.estimated_status_count, 0, "AI不得给法珠普通状态收益。" );
            _test.True(againstBow.effective_target_count > 0, "有弓威胁时自我施法候选必须有效。" );

            Ready(mage, 3, 3, 75);
            BattleAiScoreInput alreadyFull = scoreService.BuildSkillScoreInput(
                context,
                skill,
                command,
                BuildSelfPreview(mage),
                skill.CombatProfile.EffectDefinitions,
                BuildAiMetadata()
            );
            _test.Eq(alreadyFull.estimated_damage, 0, "满层且满持续时间时AI边际反击伤害必须为0。" );
            _test.Eq(alreadyFull.effective_target_count, 0, "无边际收益时自我施法候选不得伪装成有效控制。" );

            BattleStatusEffectState depleted = mage.GetStatusEffect(ReadyStatusId);
            depleted.stacks = 1;
            depleted.duration = 10;
            BattleAiScoreInput refill = scoreService.BuildSkillScoreInput(
                context,
                skill,
                command,
                BuildSelfPreview(mage),
                skill.CombatProfile.EffectDefinitions,
                BuildAiMetadata()
            );
            _test.True(refill.estimated_damage > 0, "法珠耗尽且临近结束时AI必须识别补珠边际收益。" );

            ApplyWeapon(archer, "thrown", "ranged", 4, 2, 6, 0);
            BattleAiScoreInput againstThrown = scoreService.BuildSkillScoreInput(
                context,
                skill,
                command,
                BuildSelfPreview(mage),
                skill.CombatProfile.EffectDefinitions,
                BuildAiMetadata()
            );
            _test.Eq(againstThrown.estimated_damage, 0, "投掷武器威胁不得进入反弓AI估值。" );
            _test.Eq(againstThrown.effective_target_count, 0, "只有投掷威胁时AI候选必须无效。" );
        }
        finally
        {
            BattleTestFixture.DisposeBattleCommand(command);
        }
    }

    private static SkillDefinition LoadSkillDefinition() =>
        TestSkillDefinitionProjection.LoadSkillDefinition(
            SkillPath,
            "mage_arcane_orbit:definition"
        );

    private static CombatEffectDefinition FindReadinessEffect(
        SkillDefinition skill,
        int level
    )
    {
        foreach (CombatEffectDefinition effect in skill.CombatProfile.EffectDefinitions)
        {
            if (
                effect.StatusId == ReadyStatusId
                && effect.IsUnlockedAtSkillLevel(level)
            )
            {
                return effect;
            }
        }
        return null;
    }

    private static SkillDefinition BuildThreeWShotSkill()
    {
        CombatEffectDef rawEffect = TestResourceOwnership.Own(
            new CombatEffectDef
            {
                effect_type = "damage",
                damage_tag = "physical_pierce",
                add_weapon_dice = true,
                requires_weapon = true,
                use_weapon_physical_damage_tag = true,
                resolve_as_weapon_attack = true,
                weapon_dice_multiplier = 3,
                bonus_weapon_dice_multiplier = 2,
            },
            "mage_arcane_orbit:three_w_effect"
        );
        CombatEffectDefinition effect = CombatEffectDefinition.FromDiagnosticFixture(
            rawEffect,
            "test.arcane_orbit.three_w_effect"
        );
        return TestSkillDefinitionProjection.BuildSkill(
            ShotSkillId,
            displayName: "Three W Shot",
            combatProfile: TestSkillDefinitionProjection.BuildCombatProfile(
                ShotSkillId,
                effects: new[] { effect },
                targetMode: "unit",
                targetTeamFilter: "enemy",
                targetSelectionMode: "single_unit",
                rangeValue: 4,
                attackResolutionMode: "fate_attack",
                requiredWeaponFamilies: new[]
                {
                    new StringName("bow"),
                    new StringName("crossbow"),
                    new StringName("thrown"),
                }
            )
        );
    }

    private static BattleTestFixture CreateFixture(
        SkillDefinition orbit,
        SkillDefinition shot,
        BattleUnitState mage,
        BattleUnitState hostile = null
    )
    {
        BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            "mage_arcane_orbit",
            new Vector2I(8, 4),
            new[] { mage },
            hostile != null ? new[] { hostile } : Array.Empty<BattleUnitState>()
        );
        var definitions = new Dictionary<StringName, SkillDefinition>
        {
            [orbit.SkillId] = orbit,
            [shot.SkillId] = shot,
        };
        fixture.Runtime.setup(null, definitions);
        fixture.Runtime.SetupStateForTests(fixture.State);
        return fixture;
    }

    private static BattleUnitState BuildMage(StringName id, Vector2I coord, int level)
    {
        BattleUnitState unit = BuildUnit(id, "player", coord, hp: 240);
        unit.AddKnownActiveSkill(SkillId);
        unit.SetKnownSkillLevelTyped(SkillId, level);
        unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 100);
        unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 100);
        return unit;
    }

    private static BattleUnitState BuildRangedAttacker(
        StringName id,
        StringName family,
        Vector2I coord,
        SkillDefinition shot,
        int diceCount = 1,
        int diceSides = 8,
        int flatBonus = 0
    )
    {
        BattleUnitState unit = BuildUnit(id, "enemy", coord, hp: 240);
        unit.AddKnownActiveSkill(shot.SkillId);
        unit.SetKnownSkillLevelTyped(shot.SkillId, 1);
        ApplyWeapon(unit, family, "ranged", 4, diceCount, diceSides, flatBonus);
        return unit;
    }

    private static BattleUnitState BuildUnit(
        StringName id,
        StringName faction,
        Vector2I coord,
        int hp
    )
    {
        var unit = new BattleUnitState
        {
            unit_id = id,
            source_member_id = id,
            display_name = id.ToString(),
            faction_id = faction,
        }.WithCombatResourcesForTest(
            hp: hp,
            mp: 100,
            stamina: 100,
            ap: 2,
            movePoints: BattleUnitState.DefaultMovePointsPerTurn,
            isAlive: true
        );
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, hp);
        unit.attribute_snapshot.SetValue(AttributeService.MP_MAX, 100);
        unit.attribute_snapshot.SetValue(AttributeService.STAMINA_MAX, 100);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 10);
        unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 0);
        unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 0);
        unit.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Mp));
        unit.SetAnchorCoord(coord);
        return unit;
    }

    private static void ApplyWeapon(
        BattleUnitState unit,
        StringName family,
        StringName rangeType,
        int attackRange,
        int diceCount,
        int diceSides,
        int flatBonus
    )
    {
        unit.ApplyWeaponProjectionTyped(
            new WeaponProjection
            {
                weapon_profile_kind = "equipped",
                weapon_item_id = $"arcane_orbit_test_{family}",
                weapon_profile_type_id = $"arcane_orbit_test_{family}",
                weapon_range_type = rangeType,
                weapon_family = family,
                weapon_current_grip = "two_handed",
                weapon_attack_range = attackRange,
                weapon_uses_two_hands = true,
                weapon_one_handed_dice = new WeaponDice
                {
                    dice_count = diceCount,
                    dice_sides = diceSides,
                    flat_bonus = flatBonus,
                },
                weapon_two_handed_dice = new WeaponDice
                {
                    dice_count = diceCount,
                    dice_sides = diceSides,
                    flat_bonus = flatBonus,
                },
                weapon_physical_damage_tag = "physical_pierce",
            }
        );
    }

    private static void Ready(
        BattleUnitState mage,
        int skillLevel,
        int stacks,
        int duration
    )
    {
        mage.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = ReadyStatusId,
                source_unit_id = mage.unit_id,
                source_skill_id = SkillId,
                source_skill_level = skillLevel,
                power = stacks,
                stacks = stacks,
                duration = duration,
                stack_behavior = "add",
                stack_limit = stacks,
            }
        );
    }

    private static BattleCommand BuildSelfCommand(StringName unitId)
    {
        var command = new BattleCommand
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = unitId,
            skill_entry_id = BattleSkillEntryIds.KnownSkill(SkillId),
            skill_id = SkillId,
            target_unit_id = unitId,
        };
        command.AddTargetUnitId(unitId);
        return command;
    }

    private static BattlePreview BuildSelfPreview(BattleUnitState unit)
    {
        var preview = new BattlePreview { allowed = true };
        preview.AddTargetUnitId(unit.unit_id);
        preview.AddTargetCoord(unit.GetAnchorCoord());
        return preview;
    }

    private static IReadOnlyDictionary<string, object> BuildAiMetadata() =>
        new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["desired_min_distance"] = 0,
            ["desired_max_distance"] = 4,
        };

    private static bool LogsContain(IEnumerable<string> lines, string needle)
    {
        foreach (string line in lines ?? Array.Empty<string>())
        {
            if (line.Contains(needle, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private sealed class FixedMissResolver : FixedHitResolver
    {
        public override AttackResolutionMetadata ResolveAttackMetadata(
            BattleUnitState sourceUnit,
            BattleUnitState targetUnit,
            AttackCheckInput attackCheck,
            AttackContext attackContext
        ) =>
            BuildFixedAttackMetadata(
                attackCheck,
                attackContext,
                "miss",
                false,
                false,
                true
            );
    }
}
