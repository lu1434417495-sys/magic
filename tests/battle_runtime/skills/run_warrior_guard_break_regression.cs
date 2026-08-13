using System;
using System.Collections.Generic;
using Godot;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_warrior_guard_break_regression : LifecycleTestSceneTree
{
    private static readonly StringName SkillId = "warrior_guard_break";
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            SkillDefinition skill = TestSkillDefinitionProjection.LoadSkillDefinition(
                "res://data/configs/skills/warrior_guard_break.tres",
                "warrior_guard_break_regression"
            );
            TestAuthoredContract(skill);
            TestLevelScaling(skill);
            TestMeleeNaturalWeaponGate(skill);
            TestCanonicalPreviewKeepsUnarmoredTargetLegal(skill);
            TestDurabilityResolutionUsesRaritySaveAndDestroysAtZero(skill);
            TestEnemyBattleEquipmentIsBattleOnly();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        RequestTestExit(_test.Finish("Warrior guard break regression"));
    }

    private void TestAuthoredContract(SkillDefinition skill)
    {
        _test.True(skill != null, "摧甲击正式资源应可加载。");
        if (skill?.CombatProfile == null)
        {
            _test.Fail("摧甲击应包含 combat_profile。");
            return;
        }

        CombatSkillDefinition combat = skill.CombatProfile;
        _test.Eq(skill.SkillId, SkillId, "摧甲击 skill_id 应保持稳定。");
        _test.Eq(skill.DisplayName, "摧甲击", "技能应使用批准后的显示名称。");
        _test.Eq(skill.MaxLevel, 5, "摧甲击核心上限应为5级。");
        _test.Eq(skill.NonCoreMaxLevel, 3, "摧甲击非核心上限应为3级。");
        _test.Eq(skill.GrowthTier, new StringName("intermediate"), "摧甲击应属于中阶成长档。");
        _test.Eq(ReadGrowth(skill, "strength"), 60, "摧甲击应提供60点力量成长。");
        _test.Eq(ReadGrowth(skill, "perception"), 60, "摧甲击应提供60点感知成长。");
        _test.True(combat.AllowsNaturalWeapon, "摧甲击应允许近战型天生武器。");
        _test.Eq(combat.TargetMode, new StringName("unit"), "摧甲击应选择单个单位。");
        _test.Eq(combat.TargetTeamFilter, new StringName("enemy"), "摧甲击只能选择敌方单位。");
        _test.Eq(combat.RangeValue, 0, "摧甲击应使用当前武器射程而非固定射程。");
        _test.Eq(combat.MasteryTriggerMode, new StringName("damage_dealt"), "熟练度应以实际造成伤害为触发。");
        _test.Eq(combat.MasteryAmountMode, new StringName("per_target_rank"), "熟练度应按目标等级结算。");

        int weaponDamageCount = 0;
        int durabilityCount = 0;
        foreach (CombatEffectDefinition effect in combat.EffectDefinitions)
        {
            if (effect?.EffectKind == BattleEffectKind.Damage)
            {
                weaponDamageCount += 1;
                _test.Eq(effect.Power, 0, "摧甲击不得附加固定物理伤害。");
                _test.Eq(effect.DiceCount, 0, "摧甲击不得附加脱离武器骰的额外伤害骰。");
                _test.True(effect.AddWeaponDice, "摧甲击伤害必须读取当前武器骰。");
                _test.True(effect.RequiresWeapon, "摧甲击伤害必须通过正式武器门禁。");
                _test.True(effect.UseWeaponPhysicalDamageTag, "摧甲击应沿用当前武器物理标签。");
                _test.True(effect.ResolveAsWeaponAttack, "摧甲击应走标准武器攻击链。");
            }
            else if (effect?.EffectKind == BattleEffectKind.EquipmentDurabilityDamage)
            {
                durabilityCount += 1;
                _test.Eq(effect.SaveDcMode, new StringName("static"), "耐久效果应使用静态豁免DC。");
                _test.Eq(effect.SaveDc, 12, "耐久效果应使用DC12。");
                _test.Eq(effect.SaveAbility, new StringName("strength"), "耐久效果应进行力量豁免。");
                _test.True(effect.RequireDamageApplied, "耐久效果只应在武器攻击命中后触发。");
                IReadOnlyList<StringName> slots = effect.GetStringNameListParamTyped("target_slots");
                _test.Eq(slots.Count, 1, "耐久效果只应指定一个装备槽。");
                if (slots.Count == 1)
                    _test.Eq(slots[0], new StringName("body"), "耐久效果只应损伤身体护甲。");
            }
        }
        _test.Eq(weaponDamageCount, 1, "摧甲击应只有一次标准武器伤害。");
        _test.Eq(durabilityCount, 3, "摧甲击应以三段互斥耐久效果表达等级成长。");
    }

    private void TestLevelScaling(SkillDefinition skill)
    {
        AssertLevel(skill, 0, stamina: 20, cooldownTu: 60, attackBonus: 0, durability: 12);
        AssertLevel(skill, 1, stamina: 20, cooldownTu: 60, attackBonus: 1, durability: 12);
        AssertLevel(skill, 2, stamina: 18, cooldownTu: 60, attackBonus: 1, durability: 12);
        AssertLevel(skill, 3, stamina: 18, cooldownTu: 60, attackBonus: 1, durability: 16);
        AssertLevel(skill, 4, stamina: 18, cooldownTu: 50, attackBonus: 1, durability: 16);
        AssertLevel(skill, 5, stamina: 18, cooldownTu: 50, attackBonus: 2, durability: 20);
    }

    private void AssertLevel(
        SkillDefinition skill,
        int level,
        int stamina,
        int cooldownTu,
        int attackBonus,
        int durability
    )
    {
        CombatSkillDefinition combat = skill?.CombatProfile;
        if (combat == null)
            return;
        CombatSkillResourceCosts costs = combat.GetEffectiveResourceCostValues(level);
        _test.Eq(costs.ApCost, 1, $"摧甲击{level}级应消耗1AP。");
        _test.Eq(costs.StaminaCost, stamina, $"摧甲击{level}级体力消耗应正确。");
        _test.Eq(costs.CooldownTu, cooldownTu, $"摧甲击{level}级冷却应正确。");
        _test.Eq(combat.GetEffectiveAttackRollBonus(level), attackBonus, $"摧甲击{level}级命中加值应正确。");

        int activeDurabilityCount = 0;
        foreach (CombatEffectDefinition effect in combat.EffectDefinitions)
        {
            if (
                effect?.EffectKind == BattleEffectKind.EquipmentDurabilityDamage
                && IsActiveAtLevel(effect, level)
            )
            {
                activeDurabilityCount += 1;
                _test.Eq(effect.Power, durability, $"摧甲击{level}级耐久损失应正确。");
            }
        }
        _test.Eq(activeDurabilityCount, 1, $"摧甲击{level}级应只有一个生效的耐久段。");
    }

    private void TestMeleeNaturalWeaponGate(SkillDefinition skill)
    {
        var runtime = new BattleRuntimeModule();
        runtime.setup(null, new Dictionary<StringName, SkillDefinition> { [SkillId] = skill });
        BattleUnitState caster = BuildCaster("guard_break_weapon_gate", Vector2I.Zero, 0);

        ApplyWeapon(caster, "equipped", "melee", 1, "sword");
        _test.Eq(runtime.GetSkillCastBlockReason(caster, skill), BattleSkillCastBlockReasonKind.None, "装备近战武器应能施放摧甲击。");

        caster.SetUnarmedWeaponProjectionTyped(
            "physical_blunt",
            new WeaponDice { dice_count = 1, dice_sides = 4 },
            1
        );
        _test.Eq(runtime.GetSkillCastBlockReason(caster, skill), BattleSkillCastBlockReasonKind.MeleeWeaponRequired, "普通徒手不得绕过摧甲击武器门禁。");

        ApplyWeapon(caster, "natural", "melee", 3, "claw");
        _test.Eq(runtime.GetSkillCastBlockReason(caster, skill), BattleSkillCastBlockReasonKind.None, "明确标记为melee的触及型天生武器应能施放摧甲击。");
        _test.Eq(BattleRangeService.GetEffectiveSkillRange(caster, skill), 3, "摧甲击应采用近战型天生武器的真实射程。");

        ApplyWeapon(caster, "natural", "ranged", 5, "spit");
        _test.Eq(runtime.GetSkillCastBlockReason(caster, skill), BattleSkillCastBlockReasonKind.MeleeWeaponRequired, "明确标记为ranged的天生武器不得施放摧甲击。");

        BattleTestFixture.DisposeBattleUnit(caster);
        runtime.Dispose();
    }

    private void TestCanonicalPreviewKeepsUnarmoredTargetLegal(SkillDefinition skill)
    {
        BattleUnitState unarmoredCaster = BuildCaster("guard_break_unarmored_preview_caster", new Vector2I(1, 1), 0);
        ApplyWeapon(unarmoredCaster, "natural", "melee", 2, "bite");
        BattleUnitState unarmoredTarget = BuildUnit("guard_break_unarmored_preview_target", "enemy", new Vector2I(2, 1));
        using (BattleTestFixture fixture = CreateFixture(skill, unarmoredCaster, unarmoredTarget))
        {
            BattleCommand command = BuildCommand(unarmoredCaster, unarmoredTarget);
            BattlePreview preview = fixture.Runtime.PreviewCommand(command);
            _test.True(preview?.allowed == true, "没有身体护甲的敌人仍应是合法目标。");
            _test.True(preview?.EquipmentDurabilityPreviewTyped?.HasEffect == true, "无甲预览仍应显示耐久效果分支。");
            _test.False(preview?.EquipmentDurabilityPreviewTyped?.HasMatchingEquipment ?? true, "无甲预览应明确显示耐久收益为零。");
            BattleTestFixture.DisposeBattleCommand(command);
        }

        BattleUnitState armoredCaster = BuildCaster("guard_break_armored_preview_caster", new Vector2I(1, 1), 0);
        ApplyWeapon(armoredCaster, "natural", "melee", 2, "bite");
        BattleUnitState armoredTarget = BuildUnit("guard_break_armored_preview_target", "enemy", new Vector2I(2, 1));
        EquipBodyArmor(armoredTarget, rarity: 2, currentDurability: 120, "guard_break_preview_rare_armor");
        using (BattleTestFixture fixture = CreateFixture(skill, armoredCaster, armoredTarget))
        {
            BattleUnitState runtimeTarget = fixture.State.GetUnit(armoredTarget.unit_id);
            _test.Eq(runtimeTarget?.GetEquipmentView().GetEquippedItemId("body") ?? new StringName(""), new StringName("leather_jerkin"), "穿甲预览夹具应保留目标身体护甲。");
            _test.Eq(runtimeTarget?.GetEquipmentView().GetEntrySlotIdsTyped().Count ?? 0, 1, "穿甲预览夹具应暴露一个装备入口。");
            int durabilityBefore = runtimeTarget.GetEquipmentView().GetEquippedInstance("body").current_durability;
            var durabilityResolver = new BattleEquipmentDurabilityResolver();
            CombatEffectDefinition activeDurabilityEffect = FindActiveDurabilityEffect(skill, 0);
            _test.Eq(
                ProgressionDataUtils.to_string_name(activeDurabilityEffect.GetStringNameParamTyped("equipment_slot_override")),
                new StringName(""),
                "未配置的装备槽覆盖经边界规范化后应保持为空。"
            );
            _test.Eq(activeDurabilityEffect.EquipmentDurabilitySlotWeights.Count, 0, "摧甲击不应配置随机槽权重。");
            BattleDamageResolver.EquipmentDurabilitySelectionResult manualSelection =
                durabilityResolver.SelectEquipmentForDurabilityDamage(
                    new BattleDamageResolver.EquipmentDurabilitySelectionQuery
                    {
                        TargetUnit = runtimeTarget,
                        TargetSlots = new[] { new StringName("body") },
                        ConsumeRandom = false,
                    }
                );
            _test.Eq(manualSelection.Candidates.Count, 1, "非随机耐久选择应枚举身体护甲候选。");
            BattleEquipmentDurabilityPreviewData directPreview = durabilityResolver.BuildPreview(
                fixture.State.GetUnit(armoredCaster.unit_id),
                runtimeTarget,
                activeDurabilityEffect,
                SkillId
            );
            _test.True(directPreview?.HasMatchingEquipment == true, "共享耐久预览规则应直接识别身体护甲。");
            _test.Eq(directPreview?.CandidateCount ?? -1, 1, "共享耐久预览应保留身体护甲候选数量。");
            BattleCommand command = BuildCommand(armoredCaster, armoredTarget);
            BattlePreview armoredPreview = fixture.Runtime.PreviewCommand(command);
            BattleEquipmentDurabilityPreviewData durabilityPreview = armoredPreview?.EquipmentDurabilityPreviewTyped;
            _test.True(armoredPreview?.allowed == true, "穿甲敌人应是合法目标。");
            _test.Eq(durabilityPreview?.TargetUnitId ?? new StringName(""), armoredTarget.unit_id, "耐久预览应读取命令指定的目标单位。");
            _test.True(durabilityPreview?.HasMatchingEquipment == true, "穿甲预览应识别身体护甲。");
            _test.Eq(durabilityPreview?.EquipmentRaritySaveBonus ?? -1, 4, "rare护甲预览应显示+4稀有度豁免。");
            _test.Eq(durabilityPreview?.DurabilityLossOnFailedSave ?? -1, 12, "0级预览应显示失败损失12耐久。");
            _test.Eq(runtimeTarget.GetEquipmentView().GetEquippedInstance("body").current_durability, durabilityBefore, "canonical preview 不得改写装备耐久。");
            BattleTestFixture.DisposeBattleCommand(command);
        }
    }

    private void TestDurabilityResolutionUsesRaritySaveAndDestroysAtZero(SkillDefinition skill)
    {
        CombatEffectDefinition effect = FindActiveDurabilityEffect(skill, 0);
        _test.True(effect != null, "摧甲击0级耐久效果应存在。");
        if (effect == null)
            return;

        BattleUnitState source = BuildUnit("guard_break_resolution_source", "player", Vector2I.Zero);
        BattleUnitState commonTarget = BuildUnit("guard_break_common_target", "enemy", Vector2I.One);
        EquipBodyArmor(commonTarget, rarity: 0, currentDurability: 12, "guard_break_common_armor");
        var resolver = new BattleEquipmentDurabilityResolver();
        EquipmentDurabilityDamageEffectResult destroyed = resolver.ApplyEquipmentDurabilityDamageEffect(
            source,
            commonTarget,
            effect,
            DamageResolutionContext.FromDictionary(
                new Godot.Collections.Dictionary
                {
                    ["attack_success"] = true,
                    ["save_roll_override"] = 1,
                    ["equipment_slot_override"] = "body",
                }
            ),
            totalDamage: 1,
            totalShieldAbsorbed: 0
        );
        _test.True(destroyed.HasEvent, "命中且豁免失败应生成耐久事件。");
        _test.Eq(destroyed.DurabilityLoss, 12, "0级摧甲击应损失12耐久。");
        _test.True(destroyed.Destroyed, "耐久归零应真正摧毁护甲。");
        _test.Eq(commonTarget.GetEquipmentView().GetEquippedItemId("body"), new StringName(""), "摧毁后身体槽应立即清空。");

        BattleUnitState rareTarget = BuildUnit("guard_break_rare_target", "enemy", Vector2I.One);
        EquipBodyArmor(rareTarget, rarity: 2, currentDurability: 120, "guard_break_rare_armor");
        EquipmentDurabilityDamageEffectResult saved = resolver.ApplyEquipmentDurabilityDamageEffect(
            source,
            rareTarget,
            effect,
            DamageResolutionContext.FromDictionary(
                new Godot.Collections.Dictionary
                {
                    ["attack_success"] = true,
                    ["save_roll_override"] = 8,
                    ["equipment_slot_override"] = "body",
                }
            ),
            totalDamage: 1,
            totalShieldAbsorbed: 0
        );
        _test.True(saved.HasEvent && saved.SaveResult.HasSave, "rare护甲应进行正式力量豁免。");
        _test.Eq(saved.SaveResult.Result.EquipmentRarityBonus, 4, "rare护甲应获得+4裂解豁免加值。");
        _test.True(saved.SaveResult.Success, "基础8点加rare +4达到DC12时应豁免成功。");
        _test.Eq(rareTarget.GetEquipmentView().GetEquippedInstance("body").current_durability, 120, "豁免成功不得损失耐久。");

        BattleUnitState unarmored = BuildUnit("guard_break_unarmored_resolution", "enemy", Vector2I.One);
        EquipmentDurabilityDamageEffectResult noEquipment = resolver.ApplyEquipmentDurabilityDamageEffect(
            source,
            unarmored,
            effect,
            DamageResolutionContext.FromDictionary(
                new Godot.Collections.Dictionary { ["attack_success"] = true }
            ),
            totalDamage: 1,
            totalShieldAbsorbed: 0
        );
        _test.False(noEquipment.HasEvent, "无甲目标仍可被攻击，但耐久分支应无事件且无收益。");

        BattleTestFixture.DisposeBattleUnit(source);
        BattleTestFixture.DisposeBattleUnit(commonTarget);
        BattleTestFixture.DisposeBattleUnit(rareTarget);
        BattleTestFixture.DisposeBattleUnit(unarmored);
    }

    private void TestEnemyBattleEquipmentIsBattleOnly()
    {
        using GameSession gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        IReadOnlyDictionary<StringName, EnemyTemplateDefinition> templates =
            gameSession.GetEnemyTemplateDefinitions();
        IReadOnlyDictionary<StringName, ItemDefinition> items = gameSession.GetItemDefsTyped();
        _test.True(templates.TryGetValue("militia", out EnemyTemplateDefinition template), "正式民兵模板应存在。");
        if (template == null)
            return;

        EquipmentState equipment = EnemyBattleEquipmentProjectionService.BuildEquipmentState(
            template,
            "guard_break_enemy_equipment",
            items
        );
        EquipmentInstanceState armor = equipment.GetEquippedInstance("body");
        _test.True(armor != null, "民兵应投影战斗专用身体护甲实例。");
        _test.Eq(armor?.current_durability ?? -1, 56, "common敌方护甲应以56耐久进入战斗。");

        AttributeSnapshot armoredSnapshot = EnemyBattleEquipmentProjectionService.BuildAttributeSnapshot(
            template,
            equipment,
            items
        );
        AttributeSnapshot unarmoredSnapshot = EnemyBattleEquipmentProjectionService.BuildAttributeSnapshot(
            template,
            new EquipmentState(),
            items
        );
        _test.True(
            armoredSnapshot.GetValue(AttributeService.ARMOR_CLASS)
                > unarmoredSnapshot.GetValue(AttributeService.ARMOR_CLASS),
            "敌方身体护甲应通过正式装备属性链提高AC。"
        );

        var enemy = BuildUnit("guard_break_enemy_equipment", "enemy", Vector2I.Zero);
        enemy.enemy_template_id = template.TemplateId;
        enemy.SetEquipmentView(equipment);
        EquipmentDurabilityCommitResult commit = new BattleEquipmentDurabilityResolver()
            .ApplyEquipmentDurabilityDamageToSelection(
                new EquipmentDurabilityDirectCommitRequest
                {
                    TargetUnit = enemy,
                    TargetEquipment = new EquipmentAbilityEquipmentTargetRef
                    {
                        UnitId = enemy.unit_id,
                        EntrySlotId = "body",
                        SlotId = "body",
                        ItemId = armor.item_id,
                        EquipmentInstanceId = armor.instance_id,
                        Rarity = armor.rarity,
                        CurrentDurability = armor.current_durability,
                        OccupiedSlotIds = new[] { new StringName("body") },
                    },
                    DurabilityLoss = 56,
                    SourceKey = "guard_break_test",
                    ActionId = "guard_break_test",
                }
            );
        _test.True(commit.Destroyed, "敌方战斗专用护甲归零时也应真正摧毁。");
        _test.Eq(enemy.GetEquipmentView().GetEquippedItemId("body"), new StringName(""), "敌方护甲摧毁后应从战斗装备状态移除。");

        EquipmentState freshBattleEquipment = EnemyBattleEquipmentProjectionService.BuildEquipmentState(
            template,
            "guard_break_enemy_equipment_fresh_battle",
            items
        );
        _test.Eq(freshBattleEquipment.GetEquippedInstance("body")?.current_durability ?? -1, 56, "敌方耐久损失不得写回内容定义或下一场战斗。");
        BattleTestFixture.DisposeBattleUnit(enemy);
    }

    private static BattleTestFixture CreateFixture(
        SkillDefinition skill,
        BattleUnitState caster,
        BattleUnitState target
    )
    {
        BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            "warrior_guard_break_preview",
            new Vector2I(6, 4),
            new[] { caster },
            new[] { target }
        );
        fixture.Runtime.setup(
            null,
            new Dictionary<StringName, SkillDefinition> { [SkillId] = skill }
        );
        fixture.State.active_unit_id = caster.unit_id;
        fixture.Runtime.SetupStateForTests(fixture.State);
        return fixture;
    }

    private static BattleUnitState BuildCaster(StringName id, Vector2I coord, int level)
    {
        BattleUnitState caster = BuildUnit(id, "player", coord);
        caster.AddKnownActiveSkill(SkillId);
        caster.SetKnownSkillLevelTyped(SkillId, level, preserveZero: true);
        return caster;
    }

    private static BattleUnitState BuildUnit(StringName id, StringName faction, Vector2I coord)
    {
        var unit = new BattleUnitState
        {
            unit_id = id,
            source_member_id = faction == new StringName("player") ? id : "",
            display_name = id.ToString(),
            faction_id = faction,
            control_mode = faction == new StringName("player") ? "manual" : "ai",
        }.WithCombatResourcesForTest(hp: 100, stamina: 100, ap: 2, isAlive: true);
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 10);
        unit.attribute_snapshot.SetValue(AttributeService.STAMINA_MAX, 100);
        unit.attribute_snapshot.SetValue("strength", 10);
        unit.SetAnchorCoord(coord);
        return unit;
    }

    private static void ApplyWeapon(
        BattleUnitState unit,
        StringName profileKind,
        StringName rangeType,
        int attackRange,
        StringName family
    )
    {
        unit.ApplyWeaponProjectionTyped(
            new WeaponProjection
            {
                weapon_profile_kind = profileKind,
                weapon_item_id = profileKind == new StringName("equipped") ? $"guard_break_{family}" : "",
                weapon_profile_type_id = $"guard_break_{family}",
                weapon_range_type = rangeType,
                weapon_family = family,
                weapon_current_grip = "one_handed",
                weapon_attack_range = attackRange,
                weapon_one_handed_dice = new WeaponDice { dice_count = 1, dice_sides = 6 },
                weapon_physical_damage_tag = "physical_slash",
            }
        );
    }

    private void EquipBodyArmor(
        BattleUnitState unit,
        int rarity,
        int currentDurability,
        StringName instanceId
    )
    {
        var equipment = new EquipmentState();
        EquipmentInstanceState instance = EquipmentInstanceState.CreateInstance(
            "leather_jerkin",
            instanceId
        );
        instance.rarity = rarity;
        instance.current_durability = currentDurability;
        _test.True(
            equipment.SetEquippedEntry(
                "body",
                "leather_jerkin",
                new GStringNameArray { "body" },
                instance
            ),
            "测试身体护甲应能写入装备状态。"
        );
        unit.SetEquipmentView(equipment);
    }

    private static BattleCommand BuildCommand(BattleUnitState caster, BattleUnitState target)
    {
        var command = new BattleCommand
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = caster.unit_id,
            skill_entry_id = BattleSkillEntryIds.KnownSkill(SkillId),
            skill_id = SkillId,
            target_unit_id = target.unit_id,
            target_coord = target.GetAnchorCoord(),
        };
        command.AddTargetUnitId(target.unit_id);
        command.AddTargetCoord(target.GetAnchorCoord());
        return command;
    }

    private static CombatEffectDefinition FindActiveDurabilityEffect(
        SkillDefinition skill,
        int level
    )
    {
        foreach (CombatEffectDefinition effect in skill?.CombatProfile?.EffectDefinitions ?? Array.Empty<CombatEffectDefinition>())
        {
            if (effect?.EffectKind == BattleEffectKind.EquipmentDurabilityDamage && IsActiveAtLevel(effect, level))
                return effect;
        }
        return null;
    }

    private static bool IsActiveAtLevel(CombatEffectDefinition effect, int level) =>
        effect != null
        && level >= Math.Max(effect.MinSkillLevel, 0)
        && (effect.MaxSkillLevel < 0 || level <= effect.MaxSkillLevel);

    private static int ReadGrowth(SkillDefinition skill, StringName attributeId) =>
        skill.AttributeGrowthProgress.TryGetValue(attributeId, out int value) ? value : 0;
}
