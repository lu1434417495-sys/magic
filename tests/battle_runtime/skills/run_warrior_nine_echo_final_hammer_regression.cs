using System;
using System.Collections.Generic;
using Godot;

public partial class run_warrior_nine_echo_final_hammer_regression : LifecycleTestSceneTree
{
    private static readonly StringName SkillId = "warrior_nine_echo_final_hammer";
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            SkillDefinition skill = LoadSkill();
            TestAuthoredContract(skill);
            TestHammerGate(skill);
            TestForceHitAllowCritRule();
            TestMissContinuesRemainingAttacks(skill);
            TestFormalNineHitExecution(skill);
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        RequestTestExit(_test.Finish("Warrior nine echo final hammer regression"));
    }

    private void TestAuthoredContract(SkillDefinition skill)
    {
        CombatSkillDefinition combat = skill?.CombatProfile;
        _test.True(combat != null, "九响终槌正式资源应可加载。");
        if (combat == null)
            return;

        _test.Eq(skill.NonCoreMaxLevel, 9, "九响终槌非核心上限应为9级。");
        _test.Eq(skill.MaxLevel, 10, "九响终槌核心上限应为10级。");
        _test.Eq(combat.RangeValue, 3, "九响终槌应固定覆盖使用者周围3格。");
        _test.Eq(combat.WeaponRangePolicy.ToString(), "configured", "射程不应读取锤子射程。");
        _test.Eq(combat.TargetSelectionMode.ToString(), "random_chain", "应使用随机链目标模式。");
        _test.Eq(combat.MaxHitsPerTarget, 9, "同一目标最多可承受全部九响。");
        _test.True(combat.RandomChainContinueOnMiss, "任一响落空都不应终止后续攻击。");
        _test.Eq(combat.RequiredWeaponFamilies.Count, 1, "九响终槌只应绑定一个武器族。");
        _test.Eq(combat.RequiredWeaponFamilies[0].ToString(), "hammer", "九响终槌必须绑定锤。");

        int[] expectedAttackCounts = { 3, 3, 4, 4, 5, 6, 6, 7, 8, 8, 9 };
        for (int level = 0; level <= 10; level++)
        {
            _test.Eq(
                combat.GetEffectiveRandomChainAttackCount(level),
                expectedAttackCounts[level],
                $"九响终槌{level}级攻击次数不符。"
            );
        }

        CombatSkillResourceCosts costs = combat.GetEffectiveResourceCostValues(10);
        _test.Eq(costs.ApCost, 1, "九响终槌应消耗1 AP。");
        _test.Eq(costs.AuraCost, 800, "九响终槌应消耗800斗气。");
        _test.Eq(costs.CooldownTu, 200, "九响终槌应进入200TU冷却。");

        CombatEffectDefinition damage = FindDamageEffect(skill);
        _test.True(damage?.AddWeaponDice == true, "每一响应结算完整武器伤害骰。");
        _test.True(damage?.RequiresWeapon == true, "伤害模板应要求真实武器投影。");
        _test.True(
            damage?.UseWeaponPhysicalDamageTag == true,
            "伤害类型应跟随锤子的物理伤害类型。"
        );
        _test.True(damage?.ResolveAsWeaponAttack == true, "每一响应作为武器攻击独立检定。");
    }

    private void TestHammerGate(SkillDefinition skill)
    {
        using var runtime = BuildRuntime(skill);
        BattleUnitState caster = BuildUnit("nine_echo_gate", "player", Vector2I.Zero, 100);
        caster.UnlockCombatResource("aura");
        caster.SetCurrentAura(1000);

        ApplyWeapon(caster, "sword");
        _test.Eq(
            runtime.GetSkillCastBlockReason(caster, skill),
            BattleSkillCastBlockReasonKind.RequiredWeaponFamilyMissing,
            "装备非锤武器时必须禁止九响终槌。"
        );
        ApplyWeapon(caster, "hammer");
        _test.Eq(
            runtime.GetSkillCastBlockReason(caster, skill),
            BattleSkillCastBlockReasonKind.None,
            "装备锤且资源充足时应允许九响终槌。"
        );
    }

    private void TestForceHitAllowCritRule()
    {
        using var resolver = new BattleHitResolver();
        BattleUnitState source = BuildUnit("force_hit_source", "player", Vector2I.Zero, 100);
        BattleUnitState target = BuildUnit("force_hit_target", "enemy", Vector2I.One, 100);
        var impossibleCheck = new AttackCheckInput(
            requiredRoll: 21,
            displayRequiredRoll: 21,
            naturalOneAutoMiss: true,
            naturalTwentyAutoHit: false,
            skillId: SkillId
        );
        var lowRollContext = new AttackContext(new[] { 1 })
        {
            SkillId = SkillId,
            ForceHitAllowCrit = true,
        };
        AttackResolutionMetadata lowRoll = resolver.ResolveAttackMetadata(
            source,
            target,
            impossibleCheck,
            lowRollContext
        );
        _test.True(lowRoll.AttackSuccess, "必中可重击攻击在自然1时仍必须命中。");
        _test.False(lowRoll.CriticalHit, "自然1的必中攻击不应伪造重击。");

        var highRollContext = new AttackContext(new[] { 20 })
        {
            SkillId = SkillId,
            ForceHitAllowCrit = true,
        };
        AttackResolutionMetadata highRoll = resolver.ResolveAttackMetadata(
            source,
            target,
            impossibleCheck,
            highRollContext
        );
        _test.True(highRoll.AttackSuccess, "必中可重击攻击在高点数时必须命中。");
        _test.True(highRoll.CriticalHit, "必中不能锁掉本次攻击原本可触发的重击。");
    }

    private void TestFormalNineHitExecution(SkillDefinition skill)
    {
        BattleUnitState caster = BuildUnit(
            "nine_echo_runtime_caster",
            "player",
            new Vector2I(1, 1),
            100
        );
        BattleUnitState target = BuildUnit(
            "nine_echo_runtime_target",
            "enemy",
            new Vector2I(4, 1),
            1000
        );
        BattleUnitState outside = BuildUnit(
            "nine_echo_outside_target",
            "enemy",
            new Vector2I(5, 1),
            1000
        );
        caster.UnlockCombatResource("aura");
        caster.SetCurrentAura(1000);
        caster.AddKnownActiveSkill(SkillId);
        caster.SetKnownSkillLevelTyped(SkillId, 10);
        ApplyWeapon(caster, "hammer");

        using BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            "nine_echo_formal_command",
            new Vector2I(8, 4),
            new[] { caster },
            new[] { target, outside }
        );
        fixture.Runtime.setup(
            null,
            new Dictionary<StringName, SkillDefinition> { [SkillId] = skill }
        );
        fixture.Runtime.SetupStateForTests(fixture.State);
        var hitProbe = new NineEchoHitProbe();
        var damageProbe = new NineEchoDamageProbe();
        fixture.Runtime.ConfigureDamageResolverForTests(damageProbe);
        fixture.Runtime.ConfigureHitResolverForTests(hitProbe);
        var equipmentDamageProbe = new NonPhysicalWeaponRiderProbe();
        damageProbe.SetEquipmentAbilityPorts(equipmentDamageProbe, null);

        int targetHpBefore = target.GetCurrentHp();
        int outsideHpBefore = outside.GetCurrentHp();
        BattleCommand command = BuildCommand(caster, target);
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.True(preview?.allowed == true, "3格处的敌人应可启动九响终槌。");

        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        _test.True(batch != null, "九响终槌应通过正式技能命令完成结算。");
        _test.Eq(hitProbe.CallCount, 10, "九次普通锤击后应独立结算一次追加攻击。");
        _test.Eq(hitProbe.ForceHitAllowCritCount, 1, "只有追加攻击应获得必中可重击标记。");
        _test.Eq(damageProbe.AttackMultipliers.Count, 10, "十次攻击都应进入正式伤害管线。");
        for (int attackIndex = 0; attackIndex < 9; attackIndex++)
        {
            _test.Eq(
                damageProbe.WeaponDiceMultipliers[attackIndex],
                1,
                $"前九响第{attackIndex + 1}次必须保留为完整的常规武器攻击。"
            );
            _test.True(
                Math.Abs(damageProbe.AttackMultipliers[attackIndex] - 1.0) < 0.000001,
                $"前九响第{attackIndex + 1}次不应套用额外总伤害倍率。"
            );
        }
        _test.Eq(
            damageProbe.WeaponDiceMultipliers[9],
            3,
            "第十次独立追加攻击必须只复制三倍基础物理武器骰。"
        );
        _test.True(
            Math.Abs(damageProbe.AttackMultipliers[9] - 1.0) < 0.000001,
            "第十次攻击不应放大武器携带的非物理附伤。"
        );
        _test.Eq(
            equipmentDamageProbe.CallCount,
            10,
            "九次常规武器攻击与第十次追加攻击都应各触发一次武器非物理附伤查询。"
        );
        _test.Eq(
            damageProbe.FireDamageEventCount,
            10,
            "每次攻击的火焰附伤应各独立结算一次，不能在第十击被复制成三份。"
        );
        _test.True(target.GetCurrentHp() < targetHpBefore, "九响终槌必须产生真实HP伤害。");
        _test.Eq(outside.GetCurrentHp(), outsideHpBefore, "使用者4格外的敌人不应进入随机池。");
        _test.True(target.HasStatusEffect("staggered"), "同一目标第3次命中应获得踉跄。");
        _test.True(target.HasStatusEffect("prone"), "同一目标第6次命中应倒地。");
        BattleStatusEffectState stunned = target.GetStatusEffect("stunned");
        _test.True(stunned != null, "同一目标第9次命中应震晕。");
        _test.Eq(stunned?.duration ?? 0, 30, "九响震晕应持续30TU。");
        _test.True(stunned?.lock_counterattack == true, "九响震晕应封锁反击。");
        _test.True(stunned?.lock_guard == true, "九响震晕应封锁格挡。");
        _test.True(stunned?.lock_dodge_bonus == true, "九响震晕应封锁闪避加成。");
        _test.True(stunned?.lock_crit == true, "九响震晕应封锁重击。");
        _test.Eq(caster.GetCurrentAp(), 1, "九响终槌只应支付一次1 AP。");
        _test.Eq(caster.GetCurrentAura(), 200, "九响终槌只应支付一次800斗气。");
        _test.Eq(caster.GetCooldownTyped(SkillId), 200, "九响终槌应进入200TU冷却。");
        _test.True(
            ContainsLog(batch, "追加攻击必中"),
            "战斗日志应明确追加攻击是独立的必中可重击攻击。"
        );
        batch?.Dispose();
        BattleTestFixture.DisposeBattleCommand(command);
    }

    private void TestMissContinuesRemainingAttacks(SkillDefinition skill)
    {
        BattleUnitState caster = BuildUnit(
            "nine_echo_miss_caster",
            "player",
            new Vector2I(1, 1),
            100
        );
        BattleUnitState target = BuildUnit(
            "nine_echo_miss_target",
            "enemy",
            new Vector2I(4, 1),
            100
        );
        caster.UnlockCombatResource("aura");
        caster.SetCurrentAura(1000);
        caster.AddKnownActiveSkill(SkillId);
        caster.SetKnownSkillLevelTyped(SkillId, 0, preserveZero: true);
        ApplyWeapon(caster, "hammer");

        using BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            "nine_echo_miss_continuation",
            new Vector2I(7, 4),
            new[] { caster },
            new[] { target }
        );
        fixture.Runtime.setup(
            null,
            new Dictionary<StringName, SkillDefinition> { [SkillId] = skill }
        );
        fixture.Runtime.SetupStateForTests(fixture.State);
        var hitProbe = new FirstMissThenHitProbe();
        fixture.Runtime.ConfigureDamageResolverForTests(new FixedHitMaxDamageResolver());
        fixture.Runtime.ConfigureHitResolverForTests(hitProbe);

        int hpBefore = target.GetCurrentHp();
        BattleCommand command = BuildCommand(caster, target);
        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);

        _test.Eq(hitProbe.CallCount, 3, "0级三响在首响落空后仍应完成剩余两次攻击。");
        _test.True(target.GetCurrentHp() < hpBefore, "首响落空后续命中仍应造成真实HP伤害。");
        _test.False(
            target.HasStatusEffect("staggered"),
            "三次尝试仅两次命中时不应错误触发三响踉跄。"
        );
        batch?.Dispose();
        BattleTestFixture.DisposeBattleCommand(command);
    }

    private static SkillDefinition LoadSkill() =>
        TestSkillDefinitionProjection.LoadSkillDefinition(
            "res://data/configs/skills/warrior_nine_echo_final_hammer.tres",
            "warrior_nine_echo_final_hammer_regression"
        );

    private static BattleRuntimeModule BuildRuntime(SkillDefinition skill)
    {
        var runtime = new BattleRuntimeModule();
        runtime.setup(
            null,
            new Dictionary<StringName, SkillDefinition> { [SkillId] = skill }
        );
        return runtime;
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
            display_name = id.ToString(),
            faction_id = faction,
        };
        unit.SetCurrentHp(hp);
        unit.SetCurrentAp(2);
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, hp);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 1);
        unit.attribute_snapshot.SetValue("aura_max", 1000);
        unit.SetAnchorCoord(coord);
        return unit;
    }

    private static void ApplyWeapon(BattleUnitState unit, StringName family)
    {
        unit.ApplyWeaponProjectionTyped(
            new WeaponProjection
            {
                weapon_profile_kind = "equipped",
                weapon_item_id = $"nine_echo_test_{family}",
                weapon_profile_type_id = "test_hammer",
                weapon_range_type = "melee",
                weapon_family = family,
                weapon_current_grip = "two_handed",
                weapon_attack_range = 1,
                weapon_one_handed_dice = new WeaponDice(),
                weapon_two_handed_dice = new WeaponDice
                {
                    dice_count = 1,
                    dice_sides = 6,
                },
                weapon_uses_two_hands = true,
                weapon_physical_damage_tag = "physical_blunt",
            }
        );
    }

    private static BattleCommand BuildCommand(
        BattleUnitState caster,
        BattleUnitState target
    ) =>
        new()
        {
            command_type = BattleTypedNames.ToStringName(BattleCommandKind.Skill),
            unit_id = caster.unit_id,
            skill_entry_id = BattleSkillEntryIds.KnownSkill(SkillId),
            skill_id = SkillId,
            target_unit_id = target.unit_id,
            target_coord = target.GetAnchorCoord(),
        };

    private static CombatEffectDefinition FindDamageEffect(SkillDefinition skill)
    {
        foreach (
            CombatEffectDefinition effect in
                skill?.CombatProfile?.EffectDefinitions
                ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (effect?.EffectKind == BattleEffectKind.Damage)
                return effect;
        }
        return null;
    }

    private static bool ContainsLog(BattleEventBatch batch, string text)
    {
        if (batch == null)
            return false;
        foreach (string line in batch.log_lines)
        {
            if (line.Contains(text))
                return true;
        }
        return false;
    }

    private sealed class NineEchoHitProbe : FixedHitResolver
    {
        internal int CallCount { get; private set; }
        internal int ForceHitAllowCritCount { get; private set; }

        public override AttackResolutionMetadata ResolveAttackMetadata(
            BattleUnitState sourceUnit,
            BattleUnitState targetUnit,
            AttackCheckInput attackCheck,
            AttackContext attackContext
        )
        {
            CallCount += 1;
            bool terminal = attackContext?.ForceHitAllowCrit == true;
            if (terminal)
                ForceHitAllowCritCount += 1;
            return BuildFixedAttackMetadata(
                attackCheck,
                attackContext,
                terminal ? AttackResolutionCriticalHit : AttackResolutionHit,
                true,
                terminal,
                false
            );
        }
    }

    private sealed class FirstMissThenHitProbe : FixedHitResolver
    {
        internal int CallCount { get; private set; }

        public override AttackResolutionMetadata ResolveAttackMetadata(
            BattleUnitState sourceUnit,
            BattleUnitState targetUnit,
            AttackCheckInput attackCheck,
            AttackContext attackContext
        )
        {
            CallCount += 1;
            bool success = CallCount > 1;
            return BuildFixedAttackMetadata(
                attackCheck,
                attackContext,
                success ? AttackResolutionHit : "miss",
                success,
                false,
                !success
            );
        }
    }

    private sealed class NineEchoDamageProbe : FixedHitMaxDamageResolver
    {
        internal List<double> AttackMultipliers { get; } = new();
        internal List<int> WeaponDiceMultipliers { get; } = new();
        internal int FireDamageEventCount { get; private set; }

        internal override AttackEffectResolutionResult ResolveAttackEffects(
            BattleUnitState sourceUnit,
            BattleUnitState targetUnit,
            IEnumerable<CombatEffectDefinition> effectDefinitions,
            AttackCheckInput attackCheck,
            AttackContext attackContext = null
        )
        {
            double multiplier = 1.0;
            int weaponDiceMultiplier = 1;
            foreach (
                CombatEffectDefinition effectDefinition in
                    effectDefinitions ?? Array.Empty<CombatEffectDefinition>()
            )
            {
                if (effectDefinition?.EffectKind == BattleEffectKind.Damage)
                {
                    multiplier = effectDefinition.PreResistanceDamageMultiplier;
                    weaponDiceMultiplier = effectDefinition.WeaponDiceMultiplier;
                    break;
                }
            }
            AttackMultipliers.Add(multiplier);
            WeaponDiceMultipliers.Add(weaponDiceMultiplier);
            AttackEffectResolutionResult result = base.ResolveAttackEffects(
                sourceUnit,
                targetUnit,
                effectDefinitions,
                attackCheck,
                attackContext
            );
            foreach (DamageEventResult damageEvent in result.DamageEvents ?? Array.Empty<DamageEventResult>())
            {
                if (damageEvent.DamageTag == (StringName)"fire")
                    FireDamageEventCount++;
            }
            return result;
        }
    }

    private sealed class NonPhysicalWeaponRiderProbe : IBattleEquipmentDamageQuery
    {
        internal int CallCount { get; private set; }

        public IReadOnlyList<BattleEquipmentAbilityBonusDamageDiceResult>
            CollectBonusDamageDiceOnHit(BattleEquipmentAbilityBonusDamageDiceContext context)
        {
            CallCount++;
            return new[]
            {
                new BattleEquipmentAbilityBonusDamageDiceResult
                {
                    DiceCount = 1,
                    DiceSides = 4,
                    DamageType = "fire",
                    DamageTags = new StringName[] { "fire" },
                },
            };
        }

        public StringName ResolveDamageRollModeOverride(
            BattleEquipmentAbilityDamageRollModeContext context
        ) => context?.CurrentRollMode ?? "";

        public IReadOnlyList<BattleEquipmentAbilityDamageReductionResult> CollectDamageReductions(
            BattleEquipmentAbilityDamageReductionContext context
        ) => Array.Empty<BattleEquipmentAbilityDamageReductionResult>();
    }
}
