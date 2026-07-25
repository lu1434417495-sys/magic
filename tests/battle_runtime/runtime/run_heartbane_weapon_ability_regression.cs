using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_heartbane_weapon_ability_regression : LifecycleTestSceneTree
{
    private static readonly StringName HeartbaneItemId = "weapon_unique_sword_heartbane_004";
    private static readonly StringName HeartbreakStingTraitId =
        "weapon.sword.heartbane.heartbreak_sting";
    private static readonly StringName EmotionalRendTraitId =
        "weapon.sword.heartbane.emotional_rend";
    private static readonly StringName HeartCravingTraitId =
        "weapon.sword.heartbane.heart_craving";
    private static readonly StringName HeartbreakBurstTraitId =
        "weapon.sword.heartbane.heartbreak_burst";
    private static readonly StringName HeartbreakStingBindingId =
        "binding.weapon.sword.heartbane.heartbreak_sting";
    private static readonly StringName EmotionalRendBindingId =
        "binding.weapon.sword.heartbane.emotional_rend";
    private static readonly StringName HeartCravingBindingId =
        "binding.weapon.sword.heartbane.heart_craving";
    private static readonly StringName HeartbreakBurstBindingId =
        "binding.weapon.sword.heartbane.heartbreak_burst";
    private static readonly StringName HeartbreakBurstSkillId =
        "weapon_sword_heartbane_heartbreak_burst";
    private static readonly StringName HeartbreakBurstGrantId =
        "grant.heartbane.heartbreak_burst.skill";
    private static readonly StringName EmotionalRendStatusId = "heartbane_emotional_rend";
    private static readonly StringName StunnedStatusId = "stunned";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        ProcessFrame += RunOnFirstProcessFrame;
    }

    private void RunOnFirstProcessFrame()
    {
        ProcessFrame -= RunOnFirstProcessFrame;
        Run();
    }

    private void Run()
    {
        try
        {
            TestHeartbaneProjectsRealContentOntoBattleUnitAndClearsOnUnequip();
            TestHeartbreakStingAddsPsychicDiceOnlyOnCriticalHit();
            TestHeartCravingAddsAttackRollBonusAtThirtyPercentHp();
            TestEmotionalRendIsVisibleTuBuffAndSourceBoundPenalty();
            TestHeartbreakBurstRequiresConsumesRendAndUsesPerBattleCharge();
            RequestTestExit(_test.Finish("Heartbane weapon ability regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Heartbane weapon ability regression"));
        }
    }

    private void TestHeartbaneProjectsRealContentOntoBattleUnitAndClearsOnUnequip()
    {
        using HeartbaneFixture fixture = HeartbaneFixture.Build(new GArray());
        _test.True(fixture.ItemDefs.ContainsKey(HeartbaneItemId), "真实物品内容应包含噬心者。");
        _test.True(
            fixture.TraitDefs.ContainsKey(HeartbreakStingTraitId),
            "真实 trait 内容应包含心碎之刺。"
        );
        _test.True(
            fixture.TraitDefs.ContainsKey(EmotionalRendTraitId),
            "真实 trait 内容应包含情感撕裂。"
        );
        _test.True(
            fixture.TraitDefs.ContainsKey(HeartCravingTraitId),
            "真实 trait 内容应包含心脏渴求。"
        );
        _test.True(
            fixture.TraitDefs.ContainsKey(HeartbreakBurstTraitId),
            "真实 trait 内容应包含心碎爆发。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(HeartbreakStingBindingId),
            "真实装备能力内容应包含心碎之刺 binding。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(EmotionalRendBindingId),
            "真实装备能力内容应包含情感撕裂 binding。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(HeartCravingBindingId),
            "真实装备能力内容应包含心脏渴求 binding。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(HeartbreakBurstBindingId),
            "真实装备能力内容应包含心碎爆发 binding。"
        );
        _test.True(
            fixture.SkillDefs.ContainsKey(HeartbreakBurstSkillId),
            "真实技能内容应包含心碎爆发装备技能。"
        );
        if (!fixture.ItemDefs.ContainsKey(HeartbaneItemId))
            return;

        ItemDef rawHeartbane = ResourceLoader.Load<ItemDef>(
            "res://data/configs/items/weapon_unique_rapier_heartbane.tres"
        );
        _test.True(rawHeartbane != null, "噬心者原始资源应能加载。");
        if (rawHeartbane != null)
        {
            _test.Eq(
                rawHeartbane.base_item_id,
                new StringName("weapon_type_rapier_base"),
                "噬心者原始资源应声明继承 rapier 模板。"
            );
        }

        // 机制文本落在 trait（心碎之刺）里，物品说明只保留风味文字。
        if (fixture.TraitDefs.TryGetValue(HeartbreakStingTraitId, out TraitDefinition heartbreakStingTrait)
            && heartbreakStingTrait != null)
        {
            _test.True(
                heartbreakStingTrait.Description.Contains("2D6 psychic"),
                "心碎之刺 trait 文本应描述改造后的骰子附伤，而不是百分比伤害。"
            );
        }

        BattleUnitState baseline = fixture.BuildUnitWithoutWeapon("baseline");
        BattleUnitState equipped = fixture.BuildHeartbaneUnit("projection");
        BattleWeaponProjectionValues equippedWeapon =
            equipped.GetWeaponProjectionReadViewTyped().Values;

        _test.Eq(equippedWeapon.ItemId, HeartbaneItemId, "噬心者装备后 unit 应保留真实 item_id。");
        _test.Eq(equippedWeapon.ProfileTypeId, new StringName("rapier"), "噬心者应投影为 rapier。");
        _test.Eq(equippedWeapon.AttackRange, 1, "噬心者攻击距离应为 1。");
        _test.False(equippedWeapon.UsesTwoHands, "噬心者不应占用双手。");
        _test.Eq(equippedWeapon.OneHandedDice.DiceCount, 1, "噬心者单手骰数量应为 1。");
        _test.Eq(equippedWeapon.OneHandedDice.DiceSides, 8, "噬心者单手骰面应为 D8。");
        _test.Eq(equippedWeapon.OneHandedDice.FlatBonus, 3, "噬心者单手骰固定加值应为 +3。");
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            HeartbreakStingTraitId,
            HeartbreakStingBindingId,
            "eq_heartbane_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            EmotionalRendTraitId,
            EmotionalRendBindingId,
            "eq_heartbane_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            HeartCravingTraitId,
            HeartCravingBindingId,
            "eq_heartbane_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            HeartbreakBurstTraitId,
            HeartbreakBurstBindingId,
            "eq_heartbane_projection"
        );

        equipped.GetEquipmentView().ClearSlot("main_hand");
        fixture.Runtime._unit_factory.RefreshBattleUnit(equipped);
        equippedWeapon = equipped.GetWeaponProjectionReadViewTyped().Values;
        _test.Eq(equippedWeapon.ItemId, new StringName(""), "移除噬心者后 weapon_item_id 应清空。");
        _test.Eq(
            equipped.GetEquipmentAbilitySourcesReadViewTyped().Count,
            0,
            "移除噬心者后装备能力源应清空。"
        );
        _test.Eq(
            equipped.GetEffectiveTraitInstanceCountTyped(),
            baseline.GetEffectiveTraitInstanceCountTyped(),
            "移除噬心者后装备 trait 实例应回到装备前状态。"
        );
    }

    private void TestHeartbreakStingAddsPsychicDiceOnlyOnCriticalHit()
    {
        using HeartbaneFixture ordinaryFixture = HeartbaneFixture.Build(new GArray());
        BattleUnitState ordinaryAttacker = ordinaryFixture.BuildHeartbaneUnit("ordinary");
        BattleUnitState ordinaryTarget = BuildTarget("ordinary_target", new Vector2I(1, 0));
        ordinaryTarget.SetCurrentHp(120);
        ordinaryTarget.attribute_snapshot.SetValue(AttributeService.HP_MAX, 120);
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            ordinaryFixture.Runtime,
            ordinaryAttacker,
            ordinaryTarget,
            "heartbane_sting_ordinary",
            previewCommand: false
        );
        int ordinaryDamage = 120 - ordinaryTarget.GetCurrentHp();

        using HeartbaneFixture plainOrdinaryFixture = HeartbaneFixture.Build(new GArray());
        BattleUnitState plainOrdinaryAttacker =
            plainOrdinaryFixture.BuildHeartbaneUnit("ordinary_plain");
        plainOrdinaryAttacker.ClearEquipmentAbilityProjectionTyped();
        BattleUnitState plainOrdinaryTarget = BuildTarget(
            "ordinary_plain_target",
            new Vector2I(1, 0)
        );
        plainOrdinaryTarget.SetCurrentHp(120);
        plainOrdinaryTarget.attribute_snapshot.SetValue(AttributeService.HP_MAX, 120);
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            plainOrdinaryFixture.Runtime,
            plainOrdinaryAttacker,
            plainOrdinaryTarget,
            "heartbane_sting_ordinary_plain",
            previewCommand: false
        );
        int plainOrdinaryDamage = 120 - plainOrdinaryTarget.GetCurrentHp();
        _test.Eq(
            ordinaryDamage,
            plainOrdinaryDamage,
            "心碎之刺不应在非暴击真实基础攻击中增加 HP 伤害。"
        );

        using HeartbaneFixture criticalFixture = HeartbaneFixture.Build(new GArray());
        criticalFixture.Runtime.ConfigureHitResolverForTests(new FixedCriticalHitResolver());
        BattleUnitState criticalAttacker = criticalFixture.BuildHeartbaneUnit("critical");
        BattleUnitState criticalTarget = BuildTarget("critical_target", new Vector2I(1, 0));
        criticalTarget.SetCurrentHp(120);
        criticalTarget.attribute_snapshot.SetValue(AttributeService.HP_MAX, 120);
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            criticalFixture.Runtime,
            criticalAttacker,
            criticalTarget,
            "heartbane_sting_critical",
            previewCommand: false
        );
        int criticalDamage = 120 - criticalTarget.GetCurrentHp();

        using HeartbaneFixture plainCriticalFixture = HeartbaneFixture.Build(new GArray());
        plainCriticalFixture.Runtime.ConfigureHitResolverForTests(new FixedCriticalHitResolver());
        BattleUnitState plainCriticalAttacker =
            plainCriticalFixture.BuildHeartbaneUnit("critical_plain");
        plainCriticalAttacker.ClearEquipmentAbilityProjectionTyped();
        BattleUnitState plainCriticalTarget = BuildTarget(
            "critical_plain_target",
            new Vector2I(1, 0)
        );
        plainCriticalTarget.SetCurrentHp(120);
        plainCriticalTarget.attribute_snapshot.SetValue(AttributeService.HP_MAX, 120);
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            plainCriticalFixture.Runtime,
            plainCriticalAttacker,
            plainCriticalTarget,
            "heartbane_sting_critical_plain",
            previewCommand: false
        );
        int plainCriticalDamage = 120 - plainCriticalTarget.GetCurrentHp();
        _test.True(
            criticalDamage > plainCriticalDamage,
            "心碎之刺应在暴击真实基础攻击中追加 psychic HP 伤害。"
        );
    }

    private void TestHeartCravingAddsAttackRollBonusAtThirtyPercentHp()
    {
        using HeartbaneFixture fixture = HeartbaneFixture.Build(new GArray());
        BattleUnitState attacker = fixture.BuildHeartbaneUnit("heart_craving");
        BattleUnitState threshold = BuildTarget("threshold_target", new Vector2I(1, 0));
        threshold.attribute_snapshot.SetValue(AttributeService.HP_MAX, 40);
        threshold.SetCurrentHp(12);
        BattleUnitState aboveThreshold = BuildTarget("above_threshold_target", new Vector2I(1, 0));
        aboveThreshold.attribute_snapshot.SetValue(AttributeService.HP_MAX, 40);
        aboveThreshold.SetCurrentHp(13);

        BattleAttackCheckPolicyService attackPolicy =
            fixture.Runtime.GetAttackCheckPolicyService();
        SkillDefinition attackSkill = TestSkillDefinitionProjection.BuildSkill("fixture_basic_attack");
        BattleAttackRollModifierBundle thresholdBundle = attackPolicy.BuildModifierBundle(
            attackPolicy.BuildSkillDefinitionAttackContext(
                null,
                attacker,
                threshold,
                attackSkill,
                "skill_attack_check",
                "heartbane_test",
                force_hit_no_crit: false
            )
        );
        BattleAttackRollModifierBundle aboveBundle = attackPolicy.BuildModifierBundle(
            attackPolicy.BuildSkillDefinitionAttackContext(
                null,
                attacker,
                aboveThreshold,
                attackSkill,
                "skill_attack_check",
                "heartbane_test",
                force_hit_no_crit: false
            )
        );

        _test.Eq(thresholdBundle.TotalBonus, 3, "心脏渴求应在目标 HP 等于 30% 时提供 +3。");
        _test.True(
            HasModifier(thresholdBundle, HeartCravingBindingId, 3),
            "心脏渴求 +3 应在 modifier breakdown 中标明装备能力来源。"
        );
        _test.Eq(aboveBundle.TotalBonus, 0, "目标 HP 高于 30% 时不应触发心脏渴求。");
    }

    private void TestEmotionalRendIsVisibleTuBuffAndSourceBoundPenalty()
    {
        using HeartbaneFixture fixture = HeartbaneFixture.Build(new GArray());
        _test.False(
            BattleStatusSemanticTable.HasSemantic(EmotionalRendStatusId),
            "情感撕裂状态语义应由噬心者装备配置提供，不应硬编码在全局状态表。"
        );
        BattleUnitState holder = fixture.BuildHeartbaneUnit("rend_holder");
        BattleUnitState target = BuildTarget("rend_target", new Vector2I(1, 0));
        target.SetCurrentHp(120);
        target.attribute_snapshot.SetValue(AttributeService.HP_MAX, 120);
        BattleUnitState bystander = BuildTarget("bystander", new Vector2I(2, 0));

        for (int hit = 1; hit <= 3; hit++)
        {
            WeaponAbilityCommandTestSupport.IssueBasicAttack(
                fixture.Runtime,
                holder,
                target,
                $"heartbane_emotional_rend_{hit}",
                previewCommand: false
            );
            BattleStatusEffectState rend = target.GetStatusEffect(EmotionalRendStatusId);
            _test.True(rend != null, $"第 {hit} 次命中后目标应获得情感撕裂 buff。");
            if (rend == null)
                continue;
            _test.Eq(rend.stacks, hit, $"情感撕裂第 {hit} 次命中后层数应为 {hit}。");
            _test.Eq(rend.duration, 60, "情感撕裂应使用 TU 持续，命中刷新到 60TU。");
            _test.Eq(rend.stack_behavior, new StringName("add"), "情感撕裂应由配置声明 add 叠层。");
            _test.Eq(rend.stack_limit, 3, "情感撕裂应由配置声明最多 3 层。");
            _test.Eq(rend.display_label, "情感撕裂", "情感撕裂显示名应来自装备配置。");
            _test.True(rend.counts_as_debuff, "情感撕裂应由配置声明为 debuff。");
            _test.True(
                BattleStatusSemanticTable.IsDispellableHarmfulStatusEntry(rend),
                "情感撕裂应由配置声明为可驱散 harmful magic。"
            );
            _test.Eq(rend.attack_roll_penalty, -1, "情感撕裂不应使用普通全局 attack_roll_penalty。");
            _test.Eq(
                rend.source_bound_attack_roll_penalty,
                2,
                "情感撕裂应声明只对来源生效的攻击检定惩罚。"
            );
            _test.Eq(rend.source_unit_id, holder.unit_id, "情感撕裂 buff 应记录持有者来源。");
        }

        BattleAttackCheckPolicyService attackPolicy =
            fixture.Runtime.GetAttackCheckPolicyService();
        SkillDefinition attackSkill = TestSkillDefinitionProjection.BuildSkill("fixture_basic_attack");
        BattleAttackRollModifierBundle againstHolder = attackPolicy.BuildModifierBundle(
            attackPolicy.BuildSkillDefinitionAttackContext(
                null,
                target,
                holder,
                attackSkill,
                "skill_attack_check",
                "heartbane_rend_test",
                force_hit_no_crit: false
            )
        );
        BattleAttackRollModifierBundle againstBystander = attackPolicy.BuildModifierBundle(
            attackPolicy.BuildSkillDefinitionAttackContext(
                null,
                target,
                bystander,
                attackSkill,
                "skill_attack_check",
                "heartbane_rend_test",
                force_hit_no_crit: false
            )
        );
        BattleAttackRollModifierBundle unrelatedAttacker = attackPolicy.BuildModifierBundle(
            attackPolicy.BuildSkillDefinitionAttackContext(
                null,
                bystander,
                holder,
                attackSkill,
                "skill_attack_check",
                "heartbane_rend_test",
                force_hit_no_crit: false
            )
        );

        _test.Eq(
            againstHolder.GetEffectiveModifierDelta(),
            -2,
            "3 层情感撕裂应只在目标攻击持有者时提供 -2。"
        );
        _test.True(
            HasModifier(againstHolder, EmotionalRendStatusId, -2),
            "情感撕裂 -2 应进入 modifier breakdown。"
        );
        _test.Eq(
            againstBystander.GetEffectiveModifierDelta(),
            0,
            "情感撕裂目标攻击其他单位时不应吃 -2。"
        );
        _test.Eq(
            unrelatedAttacker.GetEffectiveModifierDelta(),
            0,
            "没有情感撕裂 buff 的单位攻击持有者不应吃 -2。"
        );
    }

    private void TestHeartbreakBurstRequiresConsumesRendAndUsesPerBattleCharge()
    {
        using HeartbaneFixture fixture = HeartbaneFixture.Build(new GArray());
        BattleUnitState holder = fixture.BuildHeartbaneUnit("burst_holder");
        BattleUnitState target = BuildTarget("burst_target", new Vector2I(1, 0));
        target.SetCurrentHp(100);
        target.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);

        _test.True(
            fixture.SkillDefs.TryGetValue(HeartbreakBurstSkillId, out SkillDefinition burstSkill),
            "心碎爆发应是装备授予 SkillDef。"
        );
        if (burstSkill == null)
            return;
        CombatSkillDefinition combat = burstSkill.CombatProfile;
        _test.True(combat != null, "心碎爆发应有战斗配置。");
        _test.Eq(combat.ApCost, 1, "心碎爆发应作为 action 消耗 1AP。");
        _test.Eq(
            combat.AttackResolutionModeKind,
            CombatSkillAttackResolutionMode.DirectEffect,
            "心碎爆发是状态引爆型装备技能，应使用 direct_effect 而不是命中检定。"
        );
        _test.Eq(
            combat.CooldownTu,
            0,
            "心碎爆发每场战斗限制应由装备 per-battle charge 表达，不靠技能冷却。"
        );
        _test.True(
            HasRequiredTargetStatusGate(combat, EmotionalRendStatusId, 3),
            "心碎爆发效果应通过 typed 字段要求目标至少 3 层情感撕裂。"
        );

        BattleSkillAvailabilityService availabilityService =
            new(fixture.SkillDefs, fixture.Bindings);
        BattleSkillAvailabilityView availability =
            availabilityService.BuildView(
                new BattleSkillAvailabilityQuery
                {
                    User = holder,
                    IncludeEquipmentSkills = true,
                    IncludeKnownSkills = false,
                    Consumer = BattleSkillAvailabilityConsumer.ManualSelection,
                    WorldStep = 0,
                }
            );
        _test.True(
            TryFindSkillEntry(availability, HeartbreakBurstSkillId, out BattleAvailableSkillEntry entry),
            "装备噬心者后应出现心碎爆发装备技能入口。"
        );
        if (entry == null)
            return;
        _test.Eq(
            entry.EntryRef.SourceKind,
            BattleSkillEntrySourceKind.EquipmentSkill,
            "心碎爆发入口应是一等化 EquipmentSkill。"
        );
        _test.Eq(
            entry.EquipmentGrantedActionId,
            HeartbreakBurstGrantId,
            "心碎爆发入口应保留稳定 grant id。"
        );
        _test.Eq(
            entry.EquipmentUsagePeriodKind,
            EquipmentAbilityUsagePeriodKind.PerBattle,
            "心碎爆发应使用 battle-local per_battle 次数。"
        );
        _test.Eq(entry.EquipmentMaxUsesPerPeriod, 1, "心碎爆发每场战斗限 1 次。");

        int blockedHpBefore = target.GetCurrentHp();
        BattleEventBatch blocked = WeaponAbilityCommandTestSupport.IssueUnitSkill(
            fixture.Runtime,
            holder,
            target,
            entry,
            HeartbreakBurstSkillId,
            "heartbane_burst_blocked",
            previewCommand: false
        );
        _test.True(blocked != null, "没有 3 层情感撕裂时心碎爆发 IssueCommand 应返回 batch。");
        _test.True(
            HasLogLineContaining(blocked, EmotionalRendStatusId.ToString()),
            $"没有 3 层情感撕裂时心碎爆发应被目标状态门禁拦截。logs={JoinLogs(blocked)}"
        );
        _test.Eq(target.GetCurrentHp(), blockedHpBefore, "没有 3 层情感撕裂时，心碎爆发不应造成伤害。");
        _test.False(target.HasStatusEffect(StunnedStatusId), "没有 3 层情感撕裂时，心碎爆发不应震慑。");
        BattleSkillAvailabilityView afterBlocked =
            availabilityService.BuildView(
                new BattleSkillAvailabilityQuery
                {
                    User = holder,
                    IncludeEquipmentSkills = true,
                    IncludeKnownSkills = false,
                    Consumer = BattleSkillAvailabilityConsumer.ManualSelection,
                    WorldStep = 0,
                }
            );
        _test.True(
            TryFindSkillEntry(afterBlocked, HeartbreakBurstSkillId, out BattleAvailableSkillEntry afterBlockedEntry),
            "目标状态门禁失败后仍应保留心碎爆发入口用于 UI 展示。"
        );
        _test.True(
            afterBlockedEntry?.IsSelectable == true,
            $"目标状态门禁失败不应消耗心碎爆发每场战斗次数。logs={JoinLogs(blocked)}"
        );

        target.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = EmotionalRendStatusId,
                source_unit_id = holder.unit_id,
                stacks = 3,
                power = 3,
                duration = 60,
                source_bound_attack_roll_penalty = 2,
            }
        );

        int appliedHpBefore = target.GetCurrentHp();
        BattleEventBatch applied = WeaponAbilityCommandTestSupport.IssueUnitSkill(
            fixture.Runtime,
            holder,
            target,
            entry,
            HeartbreakBurstSkillId,
            "heartbane_burst_applied",
            previewCommand: false
        );
        _test.True(applied != null, "3 层情感撕裂时心碎爆发 IssueCommand 应返回 batch。");
        _test.True(
            target.GetCurrentHp() < appliedHpBefore,
            $"心碎爆发应通过真实技能命令造成 psychic 伤害。logs={JoinLogs(applied)}"
        );
        _test.True(target.HasStatusEffect(StunnedStatusId), $"心碎爆发应施加 stunned。logs={JoinLogs(applied)}");
        _test.False(
            target.HasStatusEffect(EmotionalRendStatusId),
            $"心碎爆发应消耗目标全部情感撕裂层数。logs={JoinLogs(applied)}"
        );

        BattleSkillAvailabilityView exhausted =
            availabilityService.BuildView(
                new BattleSkillAvailabilityQuery
                {
                    User = holder,
                    IncludeEquipmentSkills = true,
                    IncludeKnownSkills = false,
                    Consumer = BattleSkillAvailabilityConsumer.ManualSelection,
                    WorldStep = 0,
                }
            );
        _test.True(
            TryFindSkillEntry(exhausted, HeartbreakBurstSkillId, out BattleAvailableSkillEntry exhaustedEntry),
            "次数耗尽后仍应保留心碎爆发入口用于 UI 展示。"
        );
        _test.False(exhaustedEntry.IsSelectable, "心碎爆发每场战斗用过后应禁用。");
    }

    private static bool HasRequiredTargetStatusGate(
        CombatSkillDefinition combat,
        StringName statusId,
        int minStacks
    )
    {
        foreach (CombatEffectDefinition effect in combat?.EffectDefinitions ?? Array.Empty<CombatEffectDefinition>())
        {
            if (
                effect != null
                && effect.RequiredTargetStatusId == statusId
                && effect.RequiredTargetStatusMinStacks == minStacks
            )
            {
                return true;
            }
        }
        return false;
    }

    private static bool HasModifier(
        BattleAttackRollModifierBundle bundle,
        StringName sourceId,
        int delta
    )
    {
        foreach (BattleAttackRollModifierSpec spec in bundle?.Breakdown ?? Array.Empty<BattleAttackRollModifierSpec>())
        {
            if (spec.source_id == sourceId && spec.modifier_delta == delta)
                return true;
        }
        return false;
    }

    private static string JoinLogs(BattleEventBatch batch) =>
        string.Join(" | ", batch?.LogLinesTyped ?? Array.Empty<string>());

    private static bool HasLogLineContaining(BattleEventBatch batch, string expected)
    {
        foreach (string line in batch?.LogLinesTyped ?? Array.Empty<string>())
        {
            if (!string.IsNullOrEmpty(line) && line.Contains(expected))
                return true;
        }
        return false;
    }

    private static bool TryFindSkillEntry(
        BattleSkillAvailabilityView view,
        StringName skillId,
        out BattleAvailableSkillEntry result
    )
    {
        result = null;
        foreach (BattleAvailableSkillEntry entry in view?.SkillEntries ?? Array.Empty<BattleAvailableSkillEntry>())
        {
            if (entry?.EntryRef.SkillId == skillId)
            {
                result = entry;
                return true;
            }
        }
        return false;
    }

    private static void AssertUnitHasTraitAndAbilitySource(
        BattleUnitState unit,
        StringName traitId,
        StringName bindingId,
        StringName expectedInstanceId
    )
    {
        if (unit == null)
            throw new InvalidOperationException("unit is null.");
        if (!unit.HasEffectiveTrait(traitId))
            throw new InvalidOperationException($"unit missing trait {traitId}.");
        BattleEquipmentAbilitySourceReadView source = FindSource(unit, bindingId);
        if (source == null)
            throw new InvalidOperationException($"unit missing equipment ability source {bindingId}.");
        if (source.SourceKind != EquipmentAbilitySourceKind.PlayerPersistentEquipment)
            throw new InvalidOperationException($"{bindingId} should come from persistent equipment.");
        if (source.SourceEquipmentInstanceId != expectedInstanceId)
        {
            throw new InvalidOperationException(
                $"{bindingId} expected instance {expectedInstanceId}, got {source.SourceEquipmentInstanceId}."
            );
        }
    }

    private static BattleEquipmentAbilitySourceReadView FindSource(
        BattleUnitState unit,
        StringName bindingId
    )
    {
        foreach (
            BattleEquipmentAbilitySourceReadView source
            in unit.GetEquipmentAbilitySourcesReadViewTyped()
        )
        {
            if (source?.AbilityIds?.Contains(bindingId) == true)
                return source;
        }
        return null;
    }

    private static BattleUnitState BuildTarget(StringName unitId, Vector2I coord)
    {
        BattleUnitState unit = new BattleUnitState()
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = "enemy",
        }.WithCombatResourcesForTest(
            hp: 30,
            isAlive: true
        );
        unit.SetAnchorCoord(coord);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 14);
        unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 0);
        unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 0);
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 30);
        unit.SetEquipmentView(new EquipmentState());
        return unit;
    }

    private sealed class HeartbaneFixture : IDisposable
    {
        private readonly CharacterManagementModule _characterManagement;
        private readonly PartyState _partyState;

        private HeartbaneFixture(
            CharacterManagementModule characterManagement,
            PartyState partyState,
            BattleRuntimeModule runtime,
            ContentSnapshot snapshot
        )
        {
            _characterManagement = characterManagement;
            _partyState = partyState;
            Runtime = runtime;
            ItemDefs = snapshot.Items;
            SkillDefs = snapshot.Skills;
            TraitDefs = snapshot.Traits;
            Bindings = snapshot.EquipmentAbilityBindings;
        }

        internal BattleRuntimeModule Runtime { get; }
        internal IReadOnlyDictionary<StringName, ItemDefinition> ItemDefs { get; }
        internal IReadOnlyDictionary<StringName, SkillDefinition> SkillDefs { get; }
        internal IReadOnlyDictionary<StringName, TraitDefinition> TraitDefs { get; }
        internal IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> Bindings { get; }

        internal static HeartbaneFixture Build(GArray damageRolls)
        {
            ContentSnapshot snapshot = GameSessionTestFactory.GetProcessSnapshot();
            PartyState partyState = BuildPartyState("hero");
            CharacterManagementModule characterManagement = new();
            characterManagement.setup(
                partyState,
                snapshot.Skills,
                snapshot.Professions,
                snapshot.Achievements,
                snapshot.Items,
                snapshot.Quests,
                snapshot.Traits,
                null,
                new ProgressionIdentityCatalogData()
            );

            BattleRuntimeModule runtime = new();
            runtime.setup(
                characterManagement,
                snapshot.Skills,
                item_defs: snapshot.Items,
                trait_defs: snapshot.Traits,
                equipment_ability_bindings: snapshot.EquipmentAbilityBindings
            );
            runtime.ConfigureDamageResolverForTests(new FixedRollDamageResolver(damageRolls));
            runtime.ConfigureHitResolverForTests(new FixedHitResolver(10));
            return new HeartbaneFixture(characterManagement, partyState, runtime, snapshot);
        }

        internal BattleUnitState BuildUnitWithoutWeapon(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            return BuildSingleAllyUnit(label);
        }

        internal BattleUnitState BuildHeartbaneUnit(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            member.equipment_state.SetEquippedEntry(
                "main_hand",
                HeartbaneItemId,
                new GStringNameArray { "main_hand" },
                EquipmentInstanceState.CreateInstance(HeartbaneItemId, $"eq_heartbane_{label}")
            );
            BattleUnitState unit = BuildSingleAllyUnit(label);
            unit.SetAnchorCoord(Vector2I.Zero);
            unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 0);
            unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 0);
            return unit;
        }

        public void Dispose()
        {
            Runtime?.dispose();
            _characterManagement?.Dispose();
        }

        private BattleUnitState BuildSingleAllyUnit(string label)
        {
            IReadOnlyList<BattleUnitState> units =
                Runtime._unit_factory.BuildAllyUnits(_partyState, new GDictionary());
            if (units.Count != 1)
            {
                throw new InvalidOperationException(
                    $"{label} scenario should build exactly one ally unit."
                );
            }
            return units[0];
        }

        private static PartyState BuildPartyState(StringName memberId)
        {
            PartyState partyState = new();
            PartyMemberState memberState = new()
            {
                member_id = memberId,
                display_name = memberId.ToString(),
                progression = new UnitProgress(),
                equipment_state = new EquipmentState(),
            };
            partyState.SetMemberState(memberState);
            partyState.active_member_ids.Add(memberId);
            partyState.leader_member_id = memberId;
            return partyState;
        }
    }
}
