using System;
using System.Collections.Generic;
using Godot;

public partial class run_warrior_overhead_chop_regression : LifecycleTestSceneTree
{
    private static readonly StringName SkillId = "warrior_overhead_chop";
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            SkillDefinition skill = TestSkillDefinitionProjection.LoadSkillDefinition(
                "res://data/configs/skills/warrior_overhead_chop.tres",
                "warrior_overhead_chop_regression"
            );
            TestAuthoredContract(skill);
            TestGreatswordGate(skill);
            TestLevelFiveDamageEvents(skill, criticalHit: false);
            TestLevelFiveDamageEvents(skill, criticalHit: true);
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        RequestTestExit(_test.Finish("Warrior overhead chop regression"));
    }

    private void TestAuthoredContract(SkillDefinition skill)
    {
        CombatSkillDefinition combat = skill?.CombatProfile;
        _test.True(combat != null, "重剑斩正式资源应可加载。");
        if (combat == null)
            return;

        _test.Eq(skill.DisplayName, "重剑斩", "技能显示名称应为重剑斩。");
        _test.Eq(skill.MaxLevel, 5, "重剑斩核心上限应为5级。");
        _test.Eq(skill.NonCoreMaxLevel, 3, "重剑斩非核心上限应为3级。");
        _test.Eq(combat.RangeValue, 1, "重剑斩只能攻击相邻目标。");
        _test.Eq(combat.GetEffectiveAttackRollBonus(5), 1, "重剑斩应提供+1攻击检定。");
        _test.Eq(combat.GetEffectiveResourceCostValues(0).ApCost, 1, "重剑斩消耗1 AP。");
        _test.Eq(combat.GetEffectiveResourceCostValues(0).StaminaCost, 45, "0级重剑斩消耗45体力。");
        _test.Eq(
            combat.GetEffectiveResourceCostValues(3).StaminaCost,
            40,
            "3级重剑斩消耗40体力。"
        );
        _test.Eq(combat.GetEffectiveResourceCostValues(5).StaminaCost, 35, "5级重剑斩消耗35体力。");
        _test.Eq(combat.GetEffectiveResourceCostValues(0).CooldownTu, 60, "0级冷却60TU。");
        _test.Eq(combat.GetEffectiveResourceCostValues(3).CooldownTu, 45, "3级冷却45TU。");
        _test.Eq(combat.GetEffectiveResourceCostValues(5).CooldownTu, 30, "5级冷却30TU。");
        _test.Eq(combat.RequiredWeaponFamilies.Count, 0, "重剑斩不应使用宽泛的剑类家族门禁。");
        _test.Eq(
            combat.RequiredWeaponTypeIds.Count,
            1,
            "重剑斩只应绑定一个精确武器类型。"
        );
        _test.Eq(
            combat.RequiredWeaponTypeIds[0].ToString(),
            "greatsword",
            "重剑斩必须精确绑定巨剑类型。"
        );

        int[] expectedBaseMultipliers = { 1, 2, 2 };
        int[] expectedBonusMultipliers = { 0, 0, 0 };
        int[] expectedSkillDiceCounts = { 1, 1, 2 };
        _test.Eq(combat.EffectDefinitions.Count, 3, "重剑斩应有三个等级伤害档。");
        for (int index = 0; index < combat.EffectDefinitions.Count; index++)
        {
            CombatEffectDefinition effect = combat.EffectDefinitions[index];
            _test.Eq(
                effect.WeaponDiceMultiplier,
                expectedBaseMultipliers[index],
                $"重剑斩第{index + 1}档基础物理武器骰倍率不符。"
            );
            _test.Eq(
                effect.BonusWeaponDiceMultiplier,
                expectedBonusMultipliers[index],
                $"重剑斩第{index + 1}档受控目标追加武器骰倍率不符。"
            );
            _test.Eq(
                effect.DiceCount,
                expectedSkillDiceCounts[index],
                $"重剑斩第{index + 1}档技能伤害骰数量不符。"
            );
            _test.Eq(effect.DiceSides, 8, "重剑斩技能伤害骰应为d8。");
            _test.Eq(effect.BonusDamageDiceCount, 1, "硬控追加应固定为1颗伤害骰。");
            _test.Eq(effect.BonusDamageDiceSides, 6, "硬控追加应固定为1d6。");
            _test.True(effect.BonusDamageSeparateEvent, "硬控1d6必须独立结算。");
            _test.Eq(
                effect.BonusCondition.ToString(),
                "target_hard_controlled",
                "重剑斩追加伤害只应检查目标硬控状态。"
            );
        }
    }

    private void TestGreatswordGate(SkillDefinition skill)
    {
        using var runtime = BuildRuntime(skill);
        BattleUnitState caster = BuildReadyCaster("overhead_gate", Vector2I.Zero);
        try
        {
            ApplyWeapon(caster, "hammer", "warhammer");
            _test.Eq(
                runtime.GetSkillCastBlockReason(caster, skill),
                BattleSkillCastBlockReasonKind.RequiredWeaponTypeMissing,
                "锤不能施放重剑斩。"
            );
            ApplyWeapon(caster, "sword", "longsword");
            _test.Eq(
                runtime.GetSkillCastBlockReason(caster, skill),
                BattleSkillCastBlockReasonKind.RequiredWeaponTypeMissing,
                "同属剑类的长剑也不能施放重剑斩。"
            );
            ApplyWeapon(caster, "sword", "greatsword");
            _test.Eq(
                runtime.GetSkillCastBlockReason(caster, skill),
                BattleSkillCastBlockReasonKind.None,
                "正式巨剑投影使用sword家族时仍应通过精确类型门禁。"
            );
        }
        finally
        {
            BattleTestFixture.DisposeBattleUnit(caster);
        }
    }

    private void TestLevelFiveDamageEvents(SkillDefinition skill, bool criticalHit)
    {
        BattleUnitState caster = BuildReadyCaster(
            criticalHit ? "overhead_crit_caster" : "overhead_hit_caster",
            new Vector2I(1, 1)
        );
        BattleUnitState target = BuildUnit(
            criticalHit ? "overhead_crit_target" : "overhead_hit_target",
            "enemy",
            new Vector2I(2, 1),
            1000
        );
        target.SetStatusEffect(new BattleStatusEffectState
        {
            status_id = "prone",
            stacks = 1,
            duration = 100,
        });
        target.SetStatusEffect(new BattleStatusEffectState
        {
            status_id = "stunned",
            stacks = 1,
            duration = 100,
        });

        using BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            criticalHit ? "warrior_overhead_chop_crit" : "warrior_overhead_chop_hit",
            new Vector2I(5, 4),
            new[] { caster },
            new[] { target }
        );
        fixture.Runtime.setup(
            null,
            new Dictionary<StringName, SkillDefinition> { [SkillId] = skill }
        );
        fixture.Runtime.SetupStateForTests(fixture.State);
        var resolver = new CapturingMaxDamageResolver();
        fixture.Runtime.ConfigureDamageResolverForTests(resolver);
        fixture.Runtime.ConfigureHitResolverForTests(
            criticalHit ? new FixedCriticalHitResolver() : new FixedHitResolver()
        );
        var riderProbe = new NonPhysicalWeaponRiderProbe();
        resolver.SetEquipmentAbilityPorts(riderProbe, null);

        BattleCommand command = BuildCommand(caster, target);
        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        AttackEffectResolutionResult result = resolver.LastResult;
        DamageEventResult[] events = result.DamageEvents ?? Array.Empty<DamageEventResult>();
        _test.True(batch != null, "重剑斩应通过正式技能命令完成结算。");
        _test.True(result.Damage > 0, "重剑斩必须生成正数伤害数据。");
        _test.Eq(events.Length, 3, "多种硬控同时存在也只能产生主伤害、一次追加和一次附伤。");

        DamageEventResult primary = FindEvent(events, "physical_slash", 4);
        DamageEventResult controlledBonus = FindEvent(events, "physical_slash", 0);
        DamageEventResult fireRider = FindEvent(events, "fire", 0);
        _test.Eq(primary.WeaponDamageDice.Count, 4, "5级主伤害应复制2倍2d6基础物理武器骰。");
        _test.Eq(primary.DamageDice.Count, 2, "5级主伤害应附加2d8技能骰。");
        _test.Eq(
            primary.CriticalExtraWeaponDamageDice.Count,
            criticalHit ? 4 : 0,
            "5级主段暴击应额外投掷4d6。"
        );
        _test.Eq(
            primary.CriticalExtraDamageDice.Count,
            criticalHit ? 2 : 0,
            "5级主段暴击应额外投掷2d8。"
        );
        _test.True(controlledBonus.BonusConditionMet, "硬控追加伤害事件应标记条件成立。");
        _test.Eq(controlledBonus.WeaponDamageDice.Count, 0, "硬控追加不应再复制武器骰。");
        _test.Eq(controlledBonus.BonusDamageDice.Count, 1, "硬控追加应独立结算1d6。");
        _test.Eq(fireRider.WeaponDamageDice.Count, 0, "非物理武器附伤不能混入基础武器骰倍率。");
        _test.Eq(fireRider.BonusDamageDice.Count, 1, "非物理武器附伤只应保留自身的1d4。");
        _test.Eq(riderProbe.CallCount, 1, "一次重剑斩只应触发一次非物理武器附伤。");
        _test.Eq(
            controlledBonus.CriticalExtraBonusDamageDice.Count,
            criticalHit ? 1 : 0,
            "硬控追加伤害应独立继承本次攻击的重击。"
        );
        _test.Eq(caster.GetCurrentStamina(), 65, "5级重剑斩应消耗35体力。");
        _test.Eq(caster.GetCooldownTyped(SkillId), 30, "5级重剑斩应进入30TU冷却。");

        batch?.Dispose();
        BattleTestFixture.DisposeBattleCommand(command);
    }

    private static DamageEventResult FindEvent(
        DamageEventResult[] events,
        StringName damageTag,
        int weaponDiceCount
    )
    {
        foreach (DamageEventResult damageEvent in events)
        {
            if (
                damageEvent.DamageTag == damageTag
                && damageEvent.WeaponDamageDice.Count == weaponDiceCount
            )
            {
                return damageEvent;
            }
        }
        return default;
    }

    private static BattleRuntimeModule BuildRuntime(SkillDefinition skill)
    {
        var runtime = new BattleRuntimeModule();
        runtime.setup(
            null,
            new Dictionary<StringName, SkillDefinition> { [SkillId] = skill }
        );
        return runtime;
    }

    private static BattleUnitState BuildReadyCaster(StringName id, Vector2I coord)
    {
        BattleUnitState caster = BuildUnit(id, "player", coord, 100);
        caster.AddKnownActiveSkill(SkillId);
        caster.SetKnownSkillLevelTyped(SkillId, 5);
        caster.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 100);
        caster.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 100);
        ApplyWeapon(caster, "sword", "greatsword");
        return caster;
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
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, hp);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 1);
        unit.attribute_snapshot.SetValue(AttributeService.STAMINA_MAX, 100);
        unit.SetCurrentHp(hp);
        unit.SetCurrentAp(2);
        unit.SetCurrentStamina(100);
        unit.SetAnchorCoord(coord);
        return unit;
    }

    private static void ApplyWeapon(
        BattleUnitState unit,
        StringName family,
        StringName profileTypeId
    )
    {
        unit.ApplyWeaponProjectionTyped(new WeaponProjection
        {
            weapon_profile_kind = "equipped",
            weapon_item_id = $"overhead_test_{family}",
            weapon_profile_type_id = profileTypeId,
            weapon_range_type = "melee",
            weapon_family = family,
            weapon_current_grip = "two_handed",
            weapon_attack_range = 1,
            weapon_one_handed_dice = new WeaponDice(),
            weapon_two_handed_dice = new WeaponDice { dice_count = 2, dice_sides = 6 },
            weapon_uses_two_hands = true,
            weapon_physical_damage_tag = "physical_slash",
        });
    }

    private static BattleCommand BuildCommand(
        BattleUnitState caster,
        BattleUnitState target
    ) => new()
    {
        command_type = BattleTypedNames.ToStringName(BattleCommandKind.Skill),
        unit_id = caster.unit_id,
        skill_entry_id = BattleSkillEntryIds.KnownSkill(SkillId),
        skill_id = SkillId,
        target_unit_id = target.unit_id,
        target_coord = target.GetAnchorCoord(),
    };

    private sealed class CapturingMaxDamageResolver : FixedHitMaxDamageResolver
    {
        internal AttackEffectResolutionResult LastResult { get; private set; }

        internal override AttackEffectResolutionResult ResolveAttackEffects(
            BattleUnitState sourceUnit,
            BattleUnitState targetUnit,
            IEnumerable<CombatEffectDefinition> effectDefinitions,
            AttackCheckInput attackCheck,
            AttackContext attackContext = null
        )
        {
            LastResult = base.ResolveAttackEffects(
                sourceUnit,
                targetUnit,
                effectDefinitions,
                attackCheck,
                attackContext
            );
            return LastResult;
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
