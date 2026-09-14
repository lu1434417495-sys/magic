using System;
using System.Collections.Generic;
using Godot;

public partial class run_phoenix_rebirth_single_item_content_regression
    : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        try
        {
            ContentSnapshot snapshot = GameSessionTestFactory.GetProcessSnapshot();
            TestFrozenSetMemberSignatures(snapshot);
            TestItemTraitProjection(snapshot);
            TestRebirthRingContent(snapshot);
            TestCrownNirvanaContent(snapshot);
            TestGauntletContent(snapshot);
            TestFeetContent(snapshot);
            TestNecklaceContent(snapshot);
            TestAshRingContent(snapshot);
            TestBadgeAuraAndBlessingContent(snapshot);
            TestPhoenixEggContent(snapshot);
            TestPhoenixAppendReplacementLadder(snapshot);
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }

        RequestTestExit(
            _test.Finish("Phoenix Rebirth single-item content regression")
        );
    }

    private void TestItemTraitProjection(ContentSnapshot snapshot)
    {
        ItemDefinition badge = RequireItem(snapshot, "acc_phoenix_rebirth_badge");
        ItemDefinition egg = RequireItem(snapshot, "acc_phoenix_rebirth_trinket");
        ItemDefinition crown = RequireItem(snapshot, "armor_phoenix_rebirth_head");
        ItemDefinition gauntlet = RequireItem(snapshot, "armor_phoenix_rebirth_hands");
        ItemDefinition feet = RequireItem(snapshot, "armor_phoenix_rebirth_feet");
        ItemDefinition necklace = RequireItem(snapshot, "acc_phoenix_rebirth_necklace");
        ItemDefinition rebirthRing = RequireItem(snapshot, "acc_phoenix_rebirth_ring_1");
        ItemDefinition ashRing = RequireItem(snapshot, "acc_phoenix_rebirth_ring_2");
        if (
            badge == null
            || egg == null
            || crown == null
            || gauntlet == null
            || feet == null
            || necklace == null
            || rebirthRing == null
            || ashRing == null
        )
            return;

        _test.True(
            Contains(badge.TraitIds, "equipment.phoenix_rebirth.badge.fire_shelter"),
            "凤凰徽章应投影火焰庇护装备能力 trait。"
        );
        _test.True(
            Contains(egg.TraitIds, "equipment.phoenix_rebirth.egg.nirvana"),
            "凤凰蛋应投影月度涅槃装备能力 trait。"
        );
        _test.True(
            Contains(crown.TraitIds, "equipment.phoenix_rebirth.crown.nirvana"),
            "凤凰头冠应投影涅槃装备能力 trait。"
        );
        _test.False(
            Contains(crown.TraitIds, "equipment.phoenix_rebirth.head.fire_immunity"),
            "凤凰头冠单件不得再携带会覆盖fire half的常驻免疫。"
        );
        _test.True(
            Contains(gauntlet.TraitIds, "equipment.phoenix_rebirth.gauntlet.flames"),
            "凤凰护手应投影两个主动技能的装备能力 trait。"
        );
        _test.True(
            Contains(feet.TraitIds, "equipment.phoenix_rebirth.feet.fire_step"),
            "凤凰胫甲应投影火焰足迹与火焰冲锋装备能力 trait。"
        );
        _test.True(
            Contains(necklace.TraitIds, "equipment.phoenix_rebirth.necklace.nirvana_fire"),
            "涅槃之心项链应投影涅槃之火装备能力 trait。"
        );
        _test.True(
            Contains(rebirthRing.TraitIds, "equipment.phoenix_rebirth.rebirth_ring.life_rekindle"),
            "重生之戒应投影低血主动治疗装备能力trait。"
        );
        _test.True(
            Contains(ashRing.TraitIds, "equipment.phoenix_rebirth.ash_ring.fire"),
            "灰烬之戒应投影共享每日次数的装备能力 trait。"
        );
    }

    private void TestRebirthRingContent(ContentSnapshot snapshot)
    {
        EquipmentAbilityBindingDefinition binding = RequireBinding(
            snapshot,
            "binding.phoenix_rebirth.rebirth_ring.life_rekindle"
        );
        if (binding == null)
            return;
        _test.Eq(binding.FatalIntercepts.Count, 0, "重生之戒不得再参与致死拦截仲裁。");
        _test.Eq(binding.GrantedActions.Count, 1, "重生之戒应授予一个低血主动治疗。");
        EquipmentGrantedActionDefinition grant = FindGrant(
            binding,
            "grant.phoenix_rebirth.rebirth_ring.life_rekindle"
        );
        _test.True(grant != null, "重生之戒应注册生命重燃grant。");
        _test.Eq(
            grant?.UsagePeriodKind ?? EquipmentAbilityUsagePeriodKind.None,
            EquipmentAbilityUsagePeriodKind.PerBattle,
            "生命重燃应每场战斗刷新。"
        );
        _test.Eq(grant?.MaxUsesPerPeriod ?? 0, 1, "生命重燃每场战斗只能使用一次。");
        _test.True(grant?.AvailabilityConditions != null, "生命重燃必须由HP不高于50%的typed条件门禁。");

        SkillDefinition skill = RequireSkill(
            snapshot,
            "equipment_phoenix_rebirth_rebirth_ring_life_rekindle"
        );
        CombatSkillDefinition combat = skill?.CombatProfile;
        _test.True(combat != null, "生命重燃应有正式战斗配置。");
        if (combat == null)
            return;
        _test.Eq(combat.TargetMode, new StringName("unit"), "生命重燃应使用单位目标模式。");
        _test.Eq(combat.TargetTeamFilter, new StringName("self"), "生命重燃只能选择自身。");
        _test.Eq(combat.ApCost, 2, "生命重燃应消耗2 AP。");
        _test.Eq(combat.EffectDefinitions.Count, 1, "生命重燃应只包含一个治疗效果。");
        if (combat.EffectDefinitions.Count != 1)
            return;
        CombatEffectDefinition heal = combat.EffectDefinitions[0];
        _test.Eq(heal.EffectKind, BattleEffectKind.Heal, "生命重燃应走正式heal效果。");
        _test.Eq(heal.HealMissingHpPercent, 60, "生命重燃应恢复已损失生命的60%。");
        _test.Eq(heal.HealToHpPercentFloor, 0, "生命重燃不得退化成生命百分比地板治疗。");
        _test.Eq(heal.Power, 0, "生命重燃不得叠加固定治疗。");
        _test.Eq(heal.DiceCount, 0, "生命重燃不得叠加治疗骰。");
    }

    private void TestCrownNirvanaContent(ContentSnapshot snapshot)
    {
        EquipmentAbilityBindingDefinition crown = RequireBinding(
            snapshot,
            "binding.phoenix_rebirth.crown.nirvana"
        );
        if (crown == null)
            return;
        _test.Eq(crown.FatalIntercepts.Count, 1, "凤凰头冠应声明一个致死拦截。");
        if (crown.FatalIntercepts.Count != 1)
            return;
        EquipmentFatalInterceptDefinition fatal = crown.FatalIntercepts[0];
        _test.Eq(fatal.ResolutionOrder, 400, "凤凰头冠应以order 400参与凤凰致死仲裁。");
        _test.Eq(fatal.ProtectionPriority, 100, "凤凰头冠只应拦截普通致死伤害。");
        _test.Eq(
            fatal.UsagePeriodKind,
            EquipmentAbilityUsagePeriodKind.PerBattle,
            "凤凰头冠每场战斗只能正式尝试一次。"
        );
        _test.True(fatal.ConsumeOnAttempt, "凤凰头冠无论D100成功或失败都应消耗本场唯一尝试。");
        _test.True(fatal.RollGate != null, "凤凰头冠应投影正式D100概率门槛。");
        if (fatal.RollGate != null)
        {
            AssertDice(fatal.RollGate.Roll, 1, 100, "凤凰头冠涅槃检定");
            _test.Eq(fatal.RollGate.Compare, new StringName("lte"), "凤凰头冠应在D100小于等于门槛时成功。");
            _test.Eq(fatal.RollGate.Threshold, 25, "凤凰头冠涅槃成功门槛应锁定为D100<=25。");
        }
        _test.Eq(
            fatal.RecoveryKind,
            EquipmentFatalInterceptRecoveryKind.MaxHpPercent,
            "凤凰头冠应按最大生命百分比恢复。"
        );
        _test.Eq(fatal.RecoveryPercentBasisPoints, 2500, "凤凰头冠应恢复25%最大生命。");
        _test.Eq(fatal.SuccessActions.Count, 1, "凤凰头冠成功后应触发一次范围爆发。");
        if (fatal.SuccessActions.Count == 1)
        {
            TriggerSkillActionPayloadDefinition burst =
                fatal.SuccessActions[0].PayloadDefinition as TriggerSkillActionPayloadDefinition;
            _test.True(burst != null, "凤凰头冠爆发应投影typed trigger_skill payload。");
            _test.Eq(
                burst?.SkillId ?? new StringName(""),
                new StringName("equipment_phoenix_rebirth_crown_burst"),
                "凤凰头冠应触发正式内部爆发技能。"
            );
            _test.Eq(
                burst?.TargetSelector ?? new StringName(""),
                new StringName("source"),
                "凤凰头冠爆发应以佩戴者为圆心。"
            );
        }
        AssertAreaDamageSkill(
            snapshot,
            "equipment_phoenix_rebirth_crown_burst",
            3,
            3,
            10,
            "凤凰头冠爆发"
        );
    }

    private void TestGauntletContent(ContentSnapshot snapshot)
    {
        EquipmentAbilityBindingDefinition gauntlet = RequireBinding(
            snapshot,
            "binding.phoenix_rebirth.gauntlet.flames"
        );
        if (gauntlet == null)
            return;
        _test.Eq(gauntlet.GrantedActions.Count, 2, "凤凰护手应授予火焰打击与治愈之火。");
        EquipmentGrantedActionDefinition strikeGrant = FindGrant(
            gauntlet,
            "grant.phoenix_rebirth.gauntlet.fire_strike"
        );
        EquipmentGrantedActionDefinition healGrant = FindGrant(
            gauntlet,
            "grant.phoenix_rebirth.gauntlet.healing_flame"
        );
        _test.True(strikeGrant != null, "凤凰护手应授予火焰打击。");
        _test.True(healGrant != null, "凤凰护手应授予治愈之火。");
        _test.True(
            strikeGrant?.AvailabilityConditions != null,
            "火焰打击应由正式装备条件限制为主手未装备武器时可用。"
        );
        _test.Eq(
            strikeGrant?.UsagePeriodKind ?? EquipmentAbilityUsagePeriodKind.PerBattle,
            EquipmentAbilityUsagePeriodKind.None,
            "火焰打击不应消耗周期次数。"
        );
        _test.Eq(
            healGrant?.UsagePeriodKind ?? EquipmentAbilityUsagePeriodKind.None,
            EquipmentAbilityUsagePeriodKind.PerWorldDay,
            "治愈之火应按世界日刷新。"
        );
        _test.Eq(healGrant?.MaxUsesPerPeriod ?? 0, 3, "治愈之火每个世界日应可用三次。");

        SkillDefinition strikeSkill = RequireSkill(
            snapshot,
            "equipment_phoenix_rebirth_gauntlet_fire_strike"
        );
        CombatSkillDefinition strike = strikeSkill?.CombatProfile;
        _test.True(strike != null, "火焰打击应有正式战斗配置。");
        if (strike != null)
        {
            _test.Eq(strike.ApCost, 1, "火焰打击应消耗1 AP。");
            _test.Eq(strike.RangeValue, 1, "火焰打击应只能攻击相邻目标。");
            _test.True(
                strike.AttackResolutionModeKind != CombatSkillAttackResolutionMode.DirectEffect,
                "火焰打击应走命中检定而不是direct_effect自动命中。"
            );
            _test.Eq(strike.EffectDefinitions.Count, 1, "火焰打击应只有一个主伤害效果。");
            if (strike.EffectDefinitions.Count == 1)
            {
                CombatEffectDefinition damage = strike.EffectDefinitions[0];
                _test.False(damage.RequiresWeapon, "火焰打击不得被当前仅接受equipped/natural的武器门禁误拦截。");
                _test.True(damage.ResolveAsWeaponAttack, "火焰打击应标记为武器攻击来源。");
                _test.Eq(damage.DamageTag, new StringName("physical_blunt"), "火焰打击主段应为blunt。");
                _test.Eq(damage.DiceCount, 1, "火焰打击主段应为1D8。");
                _test.Eq(damage.DiceSides, 8, "火焰打击主段应为1D8。");
                _test.Eq(damage.ExtraDamageSegments.Count, 1, "火焰打击应有一个独立fire伤害段。");
                if (damage.ExtraDamageSegments.Count == 1)
                {
                    CombatDamageSegmentDefinition fire = damage.ExtraDamageSegments[0];
                    _test.Eq(fire.DamageTag, new StringName("fire"), "火焰打击附段应为fire。");
                    _test.Eq(fire.DiceCount, 1, "火焰打击附段应为1D10。");
                    _test.Eq(fire.DiceSides, 10, "火焰打击附段应为1D10。");
                    _test.True(
                        fire.DoubleDiceOnCritical,
                        "火焰打击的独立fire附段应显式随主攻击暴击追加同规格骰池。"
                    );
                }
            }
        }

        AssertSingleTargetHealSkill(
            snapshot,
            "equipment_phoenix_rebirth_gauntlet_healing_flame",
            1,
            1,
            2,
            10,
            excludeSource: true,
            label: "治愈之火"
        );
    }

    private void TestFeetContent(ContentSnapshot snapshot)
    {
        EquipmentAbilityBindingDefinition feet = RequireBinding(
            snapshot,
            "binding.phoenix_rebirth.feet.fire_step"
        );
        if (feet == null)
            return;

        _test.Eq(feet.MovementTrails.Count, 2, "凤凰胫甲应声明普通与冲锋两条互斥轨迹。 ");
        EquipmentMovementTrailDefinition passive = FindMovementTrail(
            feet,
            "phoenix_rebirth_fire_step"
        );
        EquipmentMovementTrailDefinition charge = FindMovementTrail(
            feet,
            "phoenix_rebirth_flame_charge_trail"
        );
        _test.True(passive != null, "凤凰胫甲应声明普通火焰足迹。 ");
        _test.True(charge != null, "凤凰胫甲应声明火焰冲锋轨迹。 ");
        if (passive != null)
        {
            _test.Eq(passive.ReplacementGroupId, new StringName("phoenix_rebirth_feet_trail"), "普通足迹应进入胫甲轨迹互斥组。 ");
            _test.Eq(passive.Priority, 100, "普通足迹优先级应为100。 ");
            _test.Eq(passive.RequiredSkillId, new StringName(""), "普通足迹应作用于普通移动。 ");
            _test.Eq(passive.DurationTu, 60, "普通足迹应持续60TU。 ");
            _test.Eq(passive.TargetTeamFilter, new StringName("any"), "普通足迹应影响任何阵营。 ");
            _test.Eq(passive.DamageTag, new StringName("fire"), "普通足迹应造成fire伤害。 ");
            AssertDice(passive.DamageDice, 1, 6, "普通火焰足迹");
        }
        if (charge != null)
        {
            _test.Eq(charge.ReplacementGroupId, new StringName("phoenix_rebirth_feet_trail"), "冲锋轨迹应与普通足迹互斥。 ");
            _test.Eq(charge.Priority, 200, "冲锋轨迹应以更高优先级替换普通足迹。 ");
            _test.Eq(charge.RequiredSkillId, new StringName("equipment_phoenix_rebirth_flame_charge"), "冲锋轨迹只应由正式火焰冲锋技能激活。 ");
            _test.Eq(charge.DurationTu, 60, "冲锋轨迹应持续60TU。 ");
            _test.Eq(charge.TargetTeamFilter, new StringName("any"), "冲锋轨迹应影响任何阵营。 ");
            _test.Eq(charge.DamageTag, new StringName("fire"), "冲锋轨迹应造成fire伤害。 ");
            AssertDice(charge.DamageDice, 2, 6, "火焰冲锋轨迹");
        }

        EquipmentGrantedActionDefinition grant = FindGrant(
            feet,
            "grant.phoenix_rebirth.feet.flame_charge"
        );
        _test.True(grant != null, "凤凰胫甲应授予正式火焰冲锋入口。 ");
        _test.Eq(
            grant?.SkillId ?? new StringName(""),
            new StringName("equipment_phoenix_rebirth_flame_charge"),
            "凤凰胫甲grant应指向正式火焰冲锋SkillDef。"
        );
        _test.Eq(
            grant?.UsagePeriodKind ?? EquipmentAbilityUsagePeriodKind.None,
            EquipmentAbilityUsagePeriodKind.PerWorldDay,
            "火焰冲锋应按世界日刷新。"
        );
        _test.Eq(grant?.MaxUsesPerPeriod ?? 0, 1, "火焰冲锋每日只能使用一次。 ");

        SkillDefinition skill = RequireSkill(
            snapshot,
            "equipment_phoenix_rebirth_flame_charge"
        );
        CombatSkillDefinition combat = skill?.CombatProfile;
        _test.True(combat != null, "火焰冲锋应有正式战斗配置。 ");
        if (combat == null)
            return;
        _test.Eq(combat.TargetMode, new StringName("ground"), "火焰冲锋应选择地面格。 ");
        _test.Eq(combat.TargetTeamFilter, new StringName("any"), "火焰冲锋地面目标不限制阵营。 ");
        _test.Eq(combat.ApCost, 2, "火焰冲锋应消耗2 AP。 ");
        _test.Eq(combat.MpCost, 0, "火焰冲锋不应消耗MP。 ");
        _test.Eq(combat.StaminaCost, 0, "火焰冲锋不应消耗体力。 ");
        _test.Eq(combat.CooldownTu, 0, "火焰冲锋节奏只由每日账本控制。 ");
        _test.Eq(combat.RangeMovePointCapacityMultiplier, 3, "火焰冲锋动态射程倍率应为3。 ");
        _test.Eq(combat.GetEffectiveRangeValue(1, 2), 6, "有效移动力容量2时火焰冲锋最大距离应为6格。 ");
        _test.Eq(combat.GetEffectiveRangeValue(1, 5), 15, "有效移动力容量5时火焰冲锋最大距离应为15格。 ");
        _test.Eq(combat.CastVariants.Count, 1, "火焰冲锋应只有一个直线冲锋变体。 ");
        if (combat.CastVariants.Count == 1)
        {
            CombatCastVariantDefinition variant = combat.CastVariants[0];
            _test.Eq(variant.VariantId, new StringName("flame_charge_line"), "火焰冲锋变体ID应稳定。 ");
            _test.True(
                variant.EffectDefinitions.Count == 1
                    && variant.EffectDefinitions[0].EffectKind == BattleEffectKind.Charge,
                "火焰冲锋变体应走正式charge执行链。"
            );
        }
    }

    private void TestNecklaceContent(ContentSnapshot snapshot)
    {
        EquipmentAbilityBindingDefinition necklace = RequireBinding(
            snapshot,
            "binding.phoenix_rebirth.necklace.nirvana_fire"
        );
        if (necklace == null)
            return;
        EquipmentGrantedActionDefinition grant = FindGrant(
            necklace,
            "grant.phoenix_rebirth.necklace.nirvana_fire"
        );
        _test.True(grant != null, "涅槃之心项链应授予涅槃之火。");
        _test.Eq(
            grant?.UsagePeriodKind ?? EquipmentAbilityUsagePeriodKind.None,
            EquipmentAbilityUsagePeriodKind.PerWorldDay,
            "涅槃之火应按世界日刷新。"
        );
        _test.Eq(grant?.MaxUsesPerPeriod ?? 0, 1, "涅槃之火每日只能使用一次。");
        SkillDefinition skill = RequireSkill(
            snapshot,
            "equipment_phoenix_rebirth_necklace_nirvana_fire"
        );
        CombatSkillDefinition combat = skill?.CombatProfile;
        _test.True(combat != null, "涅槃之火应有正式战斗配置。");
        if (combat == null)
            return;
        _test.Eq(combat.ApCost, 1, "涅槃之火应消耗1 AP。");
        _test.Eq(combat.RangeValue, 1, "涅槃之火射程应为1格。");
        _test.Eq(combat.EffectDefinitions.Count, 2, "涅槃之火应包含治疗与净化两个效果。");
        CombatEffectDefinition heal = FindEffect(combat, BattleEffectKind.Heal);
        CombatEffectDefinition dispel = FindEffect(combat, BattleEffectKind.DispelMagic);
        _test.True(heal != null, "涅槃之火应包含heal效果。");
        _test.True(dispel != null, "涅槃之火应包含正式dispel_magic效果。");
        _test.Eq(heal?.DiceCount ?? 0, 3, "涅槃之火治疗应为3D8。");
        _test.Eq(heal?.DiceSides ?? 0, 8, "涅槃之火治疗应为3D8。");
        _test.True(dispel?.RemoveHarmful == true, "涅槃之火净化应只移除有害效果。");
        _test.Eq(dispel?.MaxStatusRemoved ?? 0, 1, "涅槃之火最多移除一个可驱散状态。");
    }

    private void TestAshRingContent(ContentSnapshot snapshot)
    {
        EquipmentAbilityBindingDefinition ashRing = RequireBinding(
            snapshot,
            "binding.phoenix_rebirth.ash_ring.fire"
        );
        if (ashRing == null)
            return;
        _test.Eq(ashRing.GrantedActions.Count, 1, "灰烬之戒两分支必须共享同一个grant账本。");
        EquipmentGrantedActionDefinition grant = FindGrant(
            ashRing,
            "grant.phoenix_rebirth.ash_ring.fire"
        );
        _test.True(grant != null, "灰烬之戒应授予灰烬之火。");
        _test.Eq(
            grant?.UsagePeriodKind ?? EquipmentAbilityUsagePeriodKind.None,
            EquipmentAbilityUsagePeriodKind.PerWorldDay,
            "灰烬之火应按世界日刷新。"
        );
        _test.Eq(grant?.MaxUsesPerPeriod ?? 0, 1, "灰烬之火两个分支每日共享一次。");
        SkillDefinition skill = RequireSkill(snapshot, "equipment_phoenix_rebirth_ash_ring_fire");
        CombatSkillDefinition combat = skill?.CombatProfile;
        _test.True(combat != null, "灰烬之火应有正式战斗配置。");
        if (combat == null)
            return;
        _test.Eq(combat.TargetTeamFilter, new StringName("any"), "灰烬之火应由目标阵营选择治疗或攻击分支。");
        _test.Eq(combat.ApCost, 1, "灰烬之火应消耗1 AP。");
        _test.Eq(combat.RangeValue, 1, "灰烬之火两个分支射程均应为1格。");
        _test.Eq(combat.EffectDefinitions.Count, 2, "灰烬之火应包含治疗与fire伤害两个分支。");
        CombatEffectDefinition heal = FindEffect(combat, BattleEffectKind.Heal);
        CombatEffectDefinition damage = FindEffect(combat, BattleEffectKind.Damage);
        _test.Eq(heal?.EffectTargetTeamFilter ?? new StringName(""), new StringName("ally"), "治疗分支只应作用友方。");
        _test.Eq(heal?.DiceCount ?? 0, 2, "治疗分支应为2D10。");
        _test.Eq(heal?.DiceSides ?? 0, 10, "治疗分支应为2D10。");
        _test.Eq(damage?.EffectTargetTeamFilter ?? new StringName(""), new StringName("enemy"), "攻击分支只应作用敌人。");
        _test.Eq(damage?.DamageTag ?? new StringName(""), new StringName("fire"), "攻击分支应造成fire。");
        _test.Eq(damage?.DiceCount ?? 0, 3, "攻击分支应为3D10。");
        _test.Eq(damage?.DiceSides ?? 0, 10, "攻击分支应为3D10。");
    }

    private void TestBadgeAuraAndBlessingContent(ContentSnapshot snapshot)
    {
        EquipmentAbilityBindingDefinition badge = RequireBinding(
            snapshot,
            "binding.phoenix_rebirth.badge.fire_shelter"
        );
        if (badge == null)
            return;

        _test.Eq(badge.MitigationAuras.Count, 1, "凤凰徽章应声明一个动态减伤光环。");
        if (badge.MitigationAuras.Count == 1)
        {
            EquipmentMitigationAuraDefinition aura = badge.MitigationAuras[0];
            _test.Eq(aura.Radius, 2, "火焰庇护半径应为2格。");
            _test.Eq(aura.TargetTeamFilter, new StringName("ally"), "火焰庇护只作用友方且包含自身。");
            _test.Eq(aura.DamageTag, new StringName("fire"), "火焰庇护应查询fire伤害。");
            _test.Eq(aura.MitigationTier, new StringName("half"), "火焰庇护应使用half离散减伤。");
        }

        _test.Eq(badge.GrantedActions.Count, 1, "凤凰徽章应授予一个主动技能。");
        if (badge.GrantedActions.Count == 1)
        {
            EquipmentGrantedActionDefinition grant = badge.GrantedActions[0];
            _test.Eq(
                grant.SkillId,
                new StringName("equipment_phoenix_rebirth_badge_blessing"),
                "凤凰徽章应授予凤凰祝福。"
            );
            _test.Eq(
                grant.UsagePeriodKind,
                EquipmentAbilityUsagePeriodKind.PerWorldDay,
                "凤凰祝福应按世界日刷新。"
            );
            _test.Eq(grant.MaxUsesPerPeriod, 1, "凤凰祝福每日只能使用一次。");
        }

        SkillDefinition skill = RequireSkill(
            snapshot,
            "equipment_phoenix_rebirth_badge_blessing"
        );
        CombatSkillDefinition combat = skill?.CombatProfile;
        _test.True(combat != null, "凤凰祝福应有正式战斗配置。");
        if (combat != null)
        {
            _test.Eq(combat.ApCost, 2, "凤凰祝福应消耗2 AP。");
            _test.Eq(combat.RangeValue, 0, "凤凰祝福应以施放者为圆心。");
            _test.Eq(combat.AreaPattern, new StringName("radius"), "凤凰祝福应使用半径区域。");
            _test.Eq(combat.AreaValue, 2, "凤凰祝福半径应为2格。");
            _test.Eq(combat.EffectDefinitions.Count, 1, "凤凰祝福应只投影一个状态效果。");
            if (combat.EffectDefinitions.Count == 1)
            {
                CombatEffectDefinition effect = combat.EffectDefinitions[0];
                _test.Eq(effect.StatusId, new StringName("phoenix_blessing"), "凤凰祝福状态ID应稳定。");
                _test.Eq(effect.DurationTu, 120, "凤凰祝福应持续120 TU。");
                _test.Eq(effect.EffectTargetTeamFilter, new StringName("ally"), "凤凰祝福应快照范围内友方。");
                _test.Eq(effect.MaxAffectedTargets, 0, "凤凰祝福不应配置目标数量上限。");
                _test.False(effect.ExcludeSource, "凤凰祝福应包含施放者自身。");
            }
        }

        EquipmentAbilityBindingDefinition blessing = RequireBinding(
            snapshot,
            "binding.phoenix_rebirth.status.blessing"
        );
        if (blessing == null)
            return;
        _test.Eq(
            blessing.ActivationStatusId,
            new StringName("phoenix_blessing"),
            "祝福的附伤与致死恢复应由受益者状态激活。"
        );
        _test.Eq(blessing.FatalIntercepts.Count, 1, "祝福状态应声明一次致死恢复。");
        if (blessing.FatalIntercepts.Count == 1)
        {
            EquipmentFatalInterceptDefinition fatal = blessing.FatalIntercepts[0];
            _test.Eq(fatal.ResolutionOrder, 100, "祝福致死恢复应位于凤凰候选首位。");
            _test.Eq(fatal.ProtectionPriority, 100, "祝福只应拦截普通致死伤害。");
            _test.Eq(
                fatal.UsagePeriodKind,
                EquipmentAbilityUsagePeriodKind.PerBattle,
                "每名祝福受益者每场战斗只能恢复一次。"
            );
            AssertDice(fatal.RecoveryDice, 1, 10, "凤凰祝福致死恢复");
        }
    }

    private void TestPhoenixEggContent(ContentSnapshot snapshot)
    {
        EquipmentAbilityBindingDefinition egg = RequireBinding(
            snapshot,
            "binding.phoenix_rebirth.egg.nirvana"
        );
        if (egg == null)
            return;

        _test.Eq(
            egg.RequiredEffectiveTraitIds.Count,
            1,
            "凤凰蛋应声明且只声明一个10件套有效特性前置。"
        );
        _test.True(
            egg.RequiredEffectiveTraitIds.Contains(
                new StringName("gear_set.phoenix_rebirth.10.solar_rebirth")
            ),
            "凤凰蛋必须由凤凰重生10件套阈值解锁。"
        );
        _test.Eq(egg.FatalIntercepts.Count, 1, "凤凰蛋应声明一个致死拦截。");
        if (egg.FatalIntercepts.Count != 1)
            return;
        EquipmentFatalInterceptDefinition fatal = egg.FatalIntercepts[0];
        _test.Eq(fatal.ResolutionOrder, 500, "凤凰蛋应在凤凰候选中最后结算。");
        _test.Eq(fatal.ProtectionPriority, 900, "凤凰蛋应具有拦截律令死亡的最高致死保护优先级。");
        _test.Eq(
            fatal.UsagePeriodKind,
            EquipmentAbilityUsagePeriodKind.PerWorldMonth,
            "凤凰蛋应按世界月刷新。"
        );
        _test.Eq(
            fatal.RecoveryKind,
            EquipmentFatalInterceptRecoveryKind.MaxHpPercent,
            "凤凰蛋应按最大生命百分比恢复。"
        );
        _test.Eq(fatal.RecoveryPercentBasisPoints, 3000, "凤凰蛋应恢复30%最大生命。");
        _test.Eq(fatal.SuccessActions.Count, 2, "凤凰蛋成功后应依次执行形态与爆发。");
        if (fatal.SuccessActions.Count == 2)
        {
            EquipmentAbilityActionDefinition formAction = fatal.SuccessActions[0];
            EquipmentAbilityActionDefinition burstAction = fatal.SuccessActions[1];
            _test.Eq(formAction.Kind, new StringName("apply_status"), "凤凰蛋应先应用火焰化身。");
            _test.Eq(burstAction.Kind, new StringName("trigger_skill"), "凤凰蛋应在形态后触发爆发。");

            ApplyStatusActionPayloadDefinition form =
                formAction.PayloadDefinition as ApplyStatusActionPayloadDefinition;
            TriggerSkillActionPayloadDefinition burst =
                burstAction.PayloadDefinition as TriggerSkillActionPayloadDefinition;
            _test.True(form != null, "凤凰蛋形态动作应投影为typed apply_status payload。");
            _test.True(burst != null, "凤凰蛋爆发动作应投影为typed trigger_skill payload。");
            if (form != null)
            {
                _test.Eq(form.StatusId, new StringName("phoenix_form_egg"), "凤凰蛋形态状态ID应稳定。");
                _test.Eq(form.DurationTu, 120, "凤凰蛋火焰化身应持续120 TU。");
                _test.Eq(form.DamageTag, new StringName("fire"), "火焰化身免疫应限定fire。");
                _test.Eq(form.MitigationTier, new StringName("immune"), "火焰化身应免疫fire。");
            }
            if (burst != null)
            {
                _test.Eq(
                    burst.SkillId,
                    new StringName("equipment_phoenix_rebirth_egg_burst"),
                    "凤凰蛋应触发正式内部爆发技能。"
                );
                _test.Eq(burst.TargetSelector, new StringName("source"), "凤凰爆发应以复活者为圆心。");
                _test.True(burst.HandleTargetDefeat, "凤凰爆发应走标准击倒处理。");
            }
        }

        SkillDefinition burstSkill = RequireSkill(
            snapshot,
            "equipment_phoenix_rebirth_egg_burst"
        );
        CombatSkillDefinition burstCombat = burstSkill?.CombatProfile;
        _test.Eq(burstSkill?.LearnSource ?? new StringName(""), new StringName("internal"), "凤凰爆发不得进入学习路径。");
        _test.True(burstCombat != null, "凤凰爆发应有正式战斗配置。");
        if (burstCombat != null)
        {
            _test.Eq(burstCombat.AreaPattern, new StringName("radius"), "凤凰爆发应使用半径区域。");
            _test.Eq(burstCombat.AreaValue, 2, "凤凰爆发半径应为2格。");
            _test.Eq(burstCombat.EffectDefinitions.Count, 1, "凤凰爆发应只有一个伤害效果。");
            if (burstCombat.EffectDefinitions.Count == 1)
            {
                CombatEffectDefinition damage = burstCombat.EffectDefinitions[0];
                _test.Eq(damage.EffectTargetTeamFilter, new StringName("enemy"), "凤凰爆发只应伤害敌人。");
                _test.Eq(damage.DamageTag, new StringName("fire"), "凤凰爆发应造成fire伤害。");
                _test.Eq(damage.DiceCount, 3, "凤凰爆发应投掷3枚伤害骰。");
                _test.Eq(damage.DiceSides, 10, "凤凰爆发应使用D10。");
            }
        }

        EquipmentAbilityBindingDefinition eggForm = RequireBinding(
            snapshot,
            "binding.phoenix_rebirth.status.egg_form"
        );
        _test.Eq(
            eggForm?.ActivationStatusId ?? new StringName(""),
            new StringName("phoenix_form_egg"),
            "凤凰蛋附伤应由火焰化身状态激活。"
        );
    }

    private void TestPhoenixAppendReplacementLadder(ContentSnapshot snapshot)
    {
        AssertAppend(
            snapshot,
            "binding.gear_set.phoenix_rebirth.7.ember_wings",
            100,
            1,
            4,
            "余烬形态"
        );
        AssertAppend(
            snapshot,
            "binding.phoenix_rebirth.status.blessing",
            200,
            1,
            6,
            "凤凰祝福"
        );
        AssertAppend(
            snapshot,
            "binding.phoenix_rebirth.status.egg_form",
            300,
            1,
            10,
            "凤凰蛋形态"
        );
        AssertAppend(
            snapshot,
            "binding.gear_set.phoenix_rebirth.10.solar_rebirth",
            400,
            1,
            10,
            "金焰形态"
        );
    }

    private void TestFrozenSetMemberSignatures(ContentSnapshot snapshot)
    {
        if (
            !snapshot.GearSets.TryGetValue(
                "phoenix_rebirth_set",
                out GearSetDefinition gearSet
            )
        )
        {
            _test.Fail("真实内容快照缺少凤凰重生GearSetDefinition。");
            return;
        }

        FrozenItemSignature[] expected =
        {
            new(
                "armor_phoenix_rebirth_head",
                "head",
                "armor",
                22000,
                2,
                new[]
                {
                    "armor_ac_bonus|flat|2|0|equipment|armor_phoenix_rebirth_head",
                },
                new[]
                {
                    "equipment.phoenix_rebirth.head.fire_resistance",
                    "equipment.phoenix_rebirth.crown.nirvana",
                }
            ),
            new(
                "armor_phoenix_rebirth_body",
                "body",
                "armor",
                37000,
                2,
                new[]
                {
                    "armor_ac_bonus|flat|6|0|equipment|armor_phoenix_rebirth_body",
                    "hp_max|flat|20|0|equipment|armor_phoenix_rebirth_body",
                },
                new[] { "equipment.phoenix_rebirth.body.burning_plate" }
            ),
            new(
                "armor_phoenix_rebirth_hands",
                "hands",
                "armor",
                17000,
                -1,
                new[]
                {
                    "armor_ac_bonus|flat|1|0|equipment|armor_phoenix_rebirth_hands",
                    "attack_bonus|flat|1|0|equipment|armor_phoenix_rebirth_hands",
                },
                new[] { "equipment.phoenix_rebirth.gauntlet.flames" }
            ),
            new(
                "armor_phoenix_rebirth_feet",
                "feet",
                "armor",
                17000,
                -1,
                new[]
                {
                    "armor_ac_bonus|flat|1|0|equipment|armor_phoenix_rebirth_feet",
                },
                new[] { "equipment.phoenix_rebirth.feet.fire_step" }
            ),
            new(
                "acc_phoenix_rebirth_cloak",
                "cloak",
                "accessory",
                12000,
                -1,
                Array.Empty<string>(),
                new[]
                {
                    "equipment.phoenix_rebirth.cloak.fire_resistance",
                    "equipment.phoenix_rebirth.cloak.constitution_save",
                    "equipment.phoenix_rebirth.cloak.fatal_ember",
                }
            ),
            new(
                "acc_phoenix_rebirth_necklace",
                "necklace",
                "accessory",
                10000,
                -1,
                Array.Empty<string>(),
                new[] { "equipment.phoenix_rebirth.necklace.nirvana_fire" }
            ),
            new(
                "acc_phoenix_rebirth_ring_1",
                "ring_1",
                "accessory",
                8000,
                -1,
                new[]
                {
                    "hp_max|flat|10|0|equipment|acc_phoenix_rebirth_ring_1",
                },
                new[] { "equipment.phoenix_rebirth.rebirth_ring.life_rekindle" }
            ),
            new(
                "acc_phoenix_rebirth_ring_2",
                "ring_2",
                "accessory",
                8000,
                -1,
                Array.Empty<string>(),
                new[]
                {
                    "equipment.phoenix_rebirth.ash_ring.fire_resistance",
                    "equipment.phoenix_rebirth.ash_ring.constitution_save",
                    "equipment.phoenix_rebirth.ash_ring.fire",
                }
            ),
            new(
                "acc_phoenix_rebirth_trinket",
                "special_trinket",
                "accessory",
                10000,
                -1,
                new[]
                {
                    "mp_max|flat|15|0|equipment|acc_phoenix_rebirth_trinket",
                },
                new[] { "equipment.phoenix_rebirth.egg.nirvana" }
            ),
            new(
                "acc_phoenix_rebirth_badge",
                "badge",
                "accessory",
                9000,
                -1,
                Array.Empty<string>(),
                new[]
                {
                    "equipment.phoenix_rebirth.badge.constitution_save",
                    "equipment.phoenix_rebirth.badge.fire_shelter",
                }
            ),
        };

        _test.Eq(gearSet.MemberItemIds.Count, expected.Length, "凤凰重生应冻结为10件正式成员。");
        foreach (FrozenItemSignature signature in expected)
        {
            _test.True(
                Contains(gearSet.MemberItemIds, signature.ItemId),
                $"GearSetDefinition应逐项包含{signature.ItemId}。"
            );
            ItemDefinition item = RequireItem(snapshot, signature.ItemId);
            if (item == null)
                continue;
            _test.True(
                Contains(item.Tags, "phoenix_rebirth_set"),
                $"{signature.ItemId}应保留套装tag，避免membership/tag双来源漂移。"
            );
            _test.True(
                Contains(item.Tags, "world_unique_equipment"),
                $"{signature.ItemId}必须逐件标记world_unique_equipment。"
            );
            _test.Eq(item.EquipmentSlotIds.Count, 1, $"{signature.ItemId}应冻结为单一装备槽。");
            if (item.EquipmentSlotIds.Count == 1)
                _test.Eq(item.EquipmentSlotIds[0], signature.SlotId, $"{signature.ItemId}装备槽发生漂移。");
            _test.Eq(item.EquipmentTypeId, signature.EquipmentTypeId, $"{signature.ItemId}装备类型发生漂移。");
            _test.Eq(item.BasePrice, signature.BasePrice, $"{signature.ItemId}基础价格发生漂移。");
            _test.Eq(item.MaxDexBonus, signature.MaxDexBonus, $"{signature.ItemId}max dex配置发生漂移。");
            AssertExactStrings(
                BuildAttributeModifierSignatures(item.AttributeModifiers),
                signature.AttributeModifiers,
                $"{signature.ItemId} typed基础属性"
            );
            AssertExactStringNames(
                item.TraitIds,
                signature.TraitIds,
                $"{signature.ItemId}固定trait集合"
            );
        }
    }

    private void AssertAreaDamageSkill(
        ContentSnapshot snapshot,
        StringName skillId,
        int expectedRadius,
        int expectedDiceCount,
        int expectedDiceSides,
        string label
    )
    {
        SkillDefinition skill = RequireSkill(snapshot, skillId);
        CombatSkillDefinition combat = skill?.CombatProfile;
        _test.True(combat != null, $"{label}应有正式战斗配置。");
        if (combat == null)
            return;
        _test.Eq(combat.RangeValue, 0, $"{label}应以施放者为圆心。");
        _test.Eq(combat.AreaPattern, new StringName("radius"), $"{label}应使用半径区域。");
        _test.Eq(combat.AreaValue, expectedRadius, $"{label}半径不正确。");
        CombatEffectDefinition damage = FindEffect(combat, BattleEffectKind.Damage);
        _test.True(damage != null, $"{label}应包含damage效果。");
        _test.Eq(damage?.EffectTargetTeamFilter ?? new StringName(""), new StringName("enemy"), $"{label}只应伤害敌人。");
        _test.Eq(damage?.DamageTag ?? new StringName(""), new StringName("fire"), $"{label}应造成fire伤害。");
        _test.Eq(damage?.DiceCount ?? 0, expectedDiceCount, $"{label}骰子数量不正确。");
        _test.Eq(damage?.DiceSides ?? 0, expectedDiceSides, $"{label}骰面不正确。");
    }

    private void AssertSingleTargetHealSkill(
        ContentSnapshot snapshot,
        StringName skillId,
        int expectedApCost,
        int expectedRange,
        int expectedDiceCount,
        int expectedDiceSides,
        bool excludeSource,
        string label
    )
    {
        SkillDefinition skill = RequireSkill(snapshot, skillId);
        CombatSkillDefinition combat = skill?.CombatProfile;
        _test.True(combat != null, $"{label}应有正式战斗配置。");
        if (combat == null)
            return;
        _test.Eq(combat.TargetMode, new StringName("unit"), $"{label}应选择单位目标。");
        _test.Eq(combat.TargetTeamFilter, new StringName("ally"), $"{label}只应选择友方。");
        _test.Eq(combat.ApCost, expectedApCost, $"{label}AP消耗不正确。");
        _test.Eq(combat.RangeValue, expectedRange, $"{label}射程不正确。");
        CombatEffectDefinition heal = FindEffect(combat, BattleEffectKind.Heal);
        _test.True(heal != null, $"{label}应包含heal效果。");
        _test.Eq(heal?.DiceCount ?? 0, expectedDiceCount, $"{label}骰子数量不正确。");
        _test.Eq(heal?.DiceSides ?? 0, expectedDiceSides, $"{label}骰面不正确。");
        _test.Eq(heal?.ExcludeSource ?? false, excludeSource, $"{label}来源排除规则不正确。");
    }

    private static EquipmentGrantedActionDefinition FindGrant(
        EquipmentAbilityBindingDefinition binding,
        StringName grantId
    )
    {
        foreach (
            EquipmentGrantedActionDefinition grant
            in binding?.GrantedActions ?? Array.Empty<EquipmentGrantedActionDefinition>()
        )
        {
            if (grant?.GrantedActionId == grantId)
                return grant;
        }
        return null;
    }

    private static EquipmentMovementTrailDefinition FindMovementTrail(
        EquipmentAbilityBindingDefinition binding,
        StringName trailId
    )
    {
        foreach (
            EquipmentMovementTrailDefinition trail
            in binding?.MovementTrails ?? Array.Empty<EquipmentMovementTrailDefinition>()
        )
        {
            if (trail?.TrailId == trailId)
                return trail;
        }
        return null;
    }

    private static CombatEffectDefinition FindEffect(
        CombatSkillDefinition combat,
        BattleEffectKind effectKind
    )
    {
        foreach (
            CombatEffectDefinition effect
            in combat?.EffectDefinitions ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (effect?.EffectKind == effectKind)
                return effect;
        }
        return null;
    }

    private void AssertAppend(
        ContentSnapshot snapshot,
        StringName bindingId,
        int expectedPriority,
        int expectedDiceCount,
        int expectedDiceSides,
        string label
    )
    {
        EquipmentAbilityBindingDefinition binding = RequireBinding(snapshot, bindingId);
        if (binding == null)
            return;
        AddDamageDiceActionPayloadDefinition append = FindAppend(binding);
        _test.True(append != null, $"{label}应声明攻击附伤。");
        if (append == null)
            return;
        _test.Eq(
            append.ReplacementGroupId,
            new StringName("phoenix_attack_append"),
            $"{label}应加入凤凰附伤互斥组。"
        );
        _test.Eq(append.ReplacementPriority, expectedPriority, $"{label}互斥优先级不正确。");
        AssertDice(append.Dice, expectedDiceCount, expectedDiceSides, label);
    }

    private static AddDamageDiceActionPayloadDefinition FindAppend(
        EquipmentAbilityBindingDefinition binding
    )
    {
        foreach (
            EquipmentAbilityReactionDefinition reaction
            in binding?.Reactions ?? Array.Empty<EquipmentAbilityReactionDefinition>()
        )
        {
            foreach (
                EquipmentAbilityActionDefinition action
                in reaction?.Actions ?? Array.Empty<EquipmentAbilityActionDefinition>()
            )
            {
                if (action?.PayloadDefinition is AddDamageDiceActionPayloadDefinition append)
                    return append;
            }
        }
        return null;
    }

    private void AssertDice(
        DiceExpressionDefinition dice,
        int expectedCount,
        int expectedSides,
        string label
    )
    {
        _test.True(dice != null, $"{label}应投影typed骰子表达式。");
        if (dice == null)
            return;
        _test.Eq(dice.Terms.Count, 1, $"{label}应只有一个骰子项。");
        if (dice.Terms.Count != 1)
            return;
        _test.Eq(dice.Terms[0].DiceCount, expectedCount, $"{label}骰子数量不正确。");
        _test.Eq(dice.Terms[0].DiceSides, expectedSides, $"{label}骰面不正确。");
    }

    private ItemDefinition RequireItem(ContentSnapshot snapshot, StringName itemId)
    {
        if (snapshot.Items.TryGetValue(itemId, out ItemDefinition item))
            return item;
        _test.Fail($"真实内容快照缺少物品 {itemId}。");
        return null;
    }

    private SkillDefinition RequireSkill(ContentSnapshot snapshot, StringName skillId)
    {
        if (snapshot.Skills.TryGetValue(skillId, out SkillDefinition skill))
            return skill;
        _test.Fail($"真实内容快照缺少技能 {skillId}。");
        return null;
    }

    private EquipmentAbilityBindingDefinition RequireBinding(
        ContentSnapshot snapshot,
        StringName bindingId
    )
    {
        if (
            snapshot.EquipmentAbilityBindings.TryGetValue(
                bindingId,
                out EquipmentAbilityBindingDefinition binding
            )
        )
        {
            return binding;
        }
        _test.Fail($"真实内容快照缺少装备能力 binding {bindingId}。");
        return null;
    }

    private static bool Contains(IReadOnlyList<StringName> values, StringName expected)
    {
        foreach (StringName value in values ?? Array.Empty<StringName>())
        {
            if (value == expected)
                return true;
        }
        return false;
    }

    private void AssertExactStringNames(
        IReadOnlyList<StringName> actual,
        IReadOnlyList<string> expected,
        string label
    )
    {
        var actualStrings = new List<string>();
        foreach (StringName value in actual ?? Array.Empty<StringName>())
            actualStrings.Add(value.ToString());
        AssertExactStrings(actualStrings, expected, label);
    }

    private void AssertExactStrings(
        IReadOnlyList<string> actual,
        IReadOnlyList<string> expected,
        string label
    )
    {
        var actualSorted = new List<string>(actual ?? Array.Empty<string>());
        var expectedSorted = new List<string>(expected ?? Array.Empty<string>());
        actualSorted.Sort(StringComparer.Ordinal);
        expectedSorted.Sort(StringComparer.Ordinal);
        _test.Eq(actualSorted.Count, expectedSorted.Count, $"{label}数量发生漂移。");
        int sharedCount = Math.Min(actualSorted.Count, expectedSorted.Count);
        for (int index = 0; index < sharedCount; index++)
        {
            _test.Eq(
                actualSorted[index],
                expectedSorted[index],
                $"{label}[{index}]发生漂移。"
            );
        }
    }

    private static IReadOnlyList<string> BuildAttributeModifierSignatures(
        IReadOnlyList<AttributeModifierDefinition> modifiers
    )
    {
        var result = new List<string>();
        foreach (
            AttributeModifierDefinition modifier in modifiers
                ?? Array.Empty<AttributeModifierDefinition>()
        )
        {
            if (modifier == null)
            {
                result.Add("<null>");
                continue;
            }
            result.Add(
                $"{modifier.AttributeId}|{modifier.Mode}|{modifier.Value}|{modifier.ValuePerRank}|{modifier.SourceType}|{modifier.SourceId}"
            );
        }
        return result;
    }

    private sealed class FrozenItemSignature
    {
        internal FrozenItemSignature(
            StringName itemId,
            string slotId,
            StringName equipmentTypeId,
            int basePrice,
            int maxDexBonus,
            IReadOnlyList<string> attributeModifiers,
            IReadOnlyList<string> traitIds
        )
        {
            ItemId = itemId;
            SlotId = slotId ?? "";
            EquipmentTypeId = equipmentTypeId;
            BasePrice = basePrice;
            MaxDexBonus = maxDexBonus;
            AttributeModifiers = attributeModifiers ?? Array.Empty<string>();
            TraitIds = traitIds ?? Array.Empty<string>();
        }

        internal StringName ItemId { get; }
        internal string SlotId { get; }
        internal StringName EquipmentTypeId { get; }
        internal int BasePrice { get; }
        internal int MaxDexBonus { get; }
        internal IReadOnlyList<string> AttributeModifiers { get; }
        internal IReadOnlyList<string> TraitIds { get; }
    }
}
