using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_rustoath_weapon_ability_regression : LifecycleTestSceneTree
{
    private static readonly StringName RustoathItemId = "weapon_unique_sword_rustoath_006";
    private static readonly StringName RustCorrosionTraitId =
        "weapon.sword.rustoath.rust_corrosion";
    private static readonly StringName ArmorCorrosionTraitId =
        "weapon.sword.rustoath.armor_corrosion";
    private static readonly StringName RottenBladeTraitId =
        "weapon.sword.rustoath.rotten_blade";
    private static readonly StringName RustPowderStormTraitId =
        "weapon.sword.rustoath.rust_powder_storm";
    private static readonly StringName RustCorrosionBindingId =
        "binding.weapon.sword.rustoath.rust_corrosion";
    private static readonly StringName ArmorCorrosionBindingId =
        "binding.weapon.sword.rustoath.armor_corrosion";
    private static readonly StringName RottenBladeBindingId =
        "binding.weapon.sword.rustoath.rotten_blade";
    private static readonly StringName RustPowderStormBindingId =
        "binding.weapon.sword.rustoath.rust_powder_storm";
    private static readonly StringName RustPowderStormSkillId =
        "weapon_sword_rustoath_rust_powder_storm";
    private static readonly StringName RustPowderStormGrantId =
        "grant.rustoath.rust_powder_storm.skill";
    private static readonly StringName RustCorrosionStatusId = "rustoath_rust_corrosion";
    private static readonly StringName ArmorCrackedStatusId = "rustoath_armor_cracked";
    private static readonly StringName IronScaleMailId = "iron_scale_mail";
    private static readonly StringName LeatherJerkinId = "leather_jerkin";

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
            TestRustoathProjectsRealContentOntoBattleUnitAndClearsOnUnequip();
            TestRustCorrosionStacksToFiveOnHit();
            TestArmorCorrosionConsumesMetalArmorDurabilityOnceAtFiveStacks();
            TestRottenBladeAddsAcidDiceOnlyAfterArmorCracked();
            TestRustPowderStormRequiresConsumesFiveRustStacks();
            RequestTestExit(_test.Finish("Rustoath weapon ability regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Rustoath weapon ability regression"));
        }
    }

    private void TestRustoathProjectsRealContentOntoBattleUnitAndClearsOnUnequip()
    {
        using RustoathFixture fixture = RustoathFixture.Build(new GArray());
        _test.True(fixture.ItemDefs.ContainsKey(RustoathItemId), "真实物品内容应包含锈蚀之誓。");
        _test.True(fixture.TraitDefs.ContainsKey(RustCorrosionTraitId), "真实 trait 应包含锈毒。");
        _test.True(fixture.TraitDefs.ContainsKey(ArmorCorrosionTraitId), "真实 trait 应包含护甲锈蚀。");
        _test.True(fixture.TraitDefs.ContainsKey(RottenBladeTraitId), "真实 trait 应包含腐朽之刃。");
        _test.True(fixture.TraitDefs.ContainsKey(RustPowderStormTraitId), "真实 trait 应包含锈粉风暴。");
        _test.True(
            fixture.Bindings.ContainsKey(RustCorrosionBindingId),
            "真实装备能力内容应包含锈毒 binding。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(ArmorCorrosionBindingId),
            "真实装备能力内容应包含护甲锈蚀 binding。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(RottenBladeBindingId),
            "真实装备能力内容应包含腐朽之刃 binding。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(RustPowderStormBindingId),
            "真实装备能力内容应包含锈粉风暴 binding。"
        );
        _test.True(
            fixture.SkillDefs.ContainsKey(RustPowderStormSkillId),
            "真实技能内容应包含锈粉风暴装备技能。"
        );
        AssertRustoathArmorCorrosionBindingShape(fixture);

        _test.True(
            fixture.ItemDefs.TryGetValue(RustoathItemId, out ItemDefinition rustoathDef),
            "锈蚀之誓应能从 typed item registry 读取。"
        );
        if (rustoathDef != null)
        {
            _test.Eq(
                rustoathDef.GetEquipmentTypeIdNormalized(),
                new StringName("weapon"),
                "锈蚀之誓 registry 投影应是武器装备。"
            );
        }

        // 机制文本落在 trait（锈毒）里，物品说明只保留风味文字。
        if (fixture.TraitDefs.TryGetValue(RustCorrosionTraitId, out TraitDefinition rustCorrosionTrait)
            && rustCorrosionTrait != null)
        {
            _test.True(
                rustCorrosionTrait.Description.Contains("5层"),
                "锈毒 trait 文本应描述当前 5 层阈值。"
            );
            _test.False(
                rustCorrosionTrait.Description.Contains("AC-3"),
                "锈毒 trait 文本不应保留已经废弃的 AC-3。"
            );
        }

        BattleUnitState baseline = fixture.BuildUnitWithoutWeapon("baseline");
        BattleUnitState equipped = fixture.BuildRustoathUnit("projection");
        BattleWeaponProjectionValues baselineWeapon =
            baseline.GetWeaponProjectionReadViewTyped().Values;
        BattleWeaponProjectionValues equippedWeapon =
            equipped.GetWeaponProjectionReadViewTyped().Values;

        _test.Eq(equippedWeapon.ItemId, RustoathItemId, "锈蚀之誓装备后 unit 应保留真实 item_id。");
        _test.Eq(
            equippedWeapon.ProfileTypeId,
            new StringName("shortsword"),
            "锈蚀之誓应投影为 shortsword。"
        );
        _test.Eq(equippedWeapon.AttackRange, 1, "锈蚀之誓攻击距离应为 1。");
        _test.False(equippedWeapon.UsesTwoHands, "锈蚀之誓应是单手武器。");
        _test.Eq(equippedWeapon.OneHandedDice.DiceCount, 1, "锈蚀之誓应是 1D6。");
        _test.Eq(equippedWeapon.OneHandedDice.DiceSides, 6, "锈蚀之誓应是 1D6。");
        _test.Eq(equippedWeapon.OneHandedDice.FlatBonus, 2, "锈蚀之誓应有 +2 固定伤害。");
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            RustCorrosionTraitId,
            RustCorrosionBindingId,
            "eq_rustoath_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            ArmorCorrosionTraitId,
            ArmorCorrosionBindingId,
            "eq_rustoath_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            RottenBladeTraitId,
            RottenBladeBindingId,
            "eq_rustoath_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            RustPowderStormTraitId,
            RustPowderStormBindingId,
            "eq_rustoath_projection"
        );

        equipped.GetEquipmentView().ClearSlot("main_hand");
        fixture.Runtime._unit_factory.RefreshBattleUnit(equipped);
        BattleWeaponProjectionValues removedWeapon =
            equipped.GetWeaponProjectionReadViewTyped().Values;
        _test.Eq(removedWeapon.ItemId, new StringName(""), "移除锈蚀之誓后 weapon_item_id 应清空。");
        _test.Eq(
            removedWeapon.ProfileTypeId,
            baselineWeapon.ProfileTypeId,
            "移除锈蚀之誓后 weapon_profile_type_id 应回到装备前状态。"
        );
        _test.Eq(
            equipped.GetEquipmentAbilitySourcesReadViewTyped().Count,
            0,
            "移除锈蚀之誓后装备能力源应清空。"
        );
        _test.Eq(
            equipped.GetEffectiveTraitInstanceCountTyped(),
            baseline.GetEffectiveTraitInstanceCountTyped(),
            "移除锈蚀之誓后装备 trait 实例应回到装备前状态。"
        );
    }

    private void TestRustCorrosionStacksToFiveOnHit()
    {
        using RustoathFixture fixture = RustoathFixture.Build(new GArray());
        _test.False(
            BattleStatusSemanticTable.HasSemantic(RustCorrosionStatusId),
            "锈蚀状态语义应由锈蚀之誓装备配置提供，不应硬编码在全局状态表。"
        );
        BattleUnitState attacker = fixture.BuildRustoathUnit("rust_stacks");
        BattleUnitState target = BuildTarget("rust_stack_target", new Vector2I(1, 0));
        target.SetCurrentHp(100);
        target.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);

        for (int hit = 1; hit <= 6; hit++)
        {
            WeaponAbilityCommandTestSupport.IssueBasicAttack(
                fixture.Runtime,
                attacker,
                target,
                $"rustoath_rust_stacks_{hit}"
            );
            BattleStatusEffectState rust = target.GetStatusEffect(RustCorrosionStatusId);
            _test.True(rust != null, $"第 {hit} 次命中后目标应获得锈蚀。");
            if (rust == null)
                continue;
            _test.Eq(rust.stacks, Math.Min(hit, 5), $"第 {hit} 次命中后锈蚀最多叠到 5 层。");
            _test.Eq(rust.duration, 180, "锈蚀应使用 180TU 持续时间并在命中时刷新。");
            _test.Eq(rust.stack_behavior, new StringName("add"), "锈蚀应由配置声明 add 叠层。");
            _test.Eq(rust.stack_limit, 5, "锈蚀应由配置声明最多 5 层。");
            _test.Eq(rust.display_label, "锈蚀", "锈蚀显示名应来自装备配置。");
            _test.True(rust.counts_as_debuff, "锈蚀应由配置声明为 debuff。");
        }
    }

    private void TestArmorCorrosionConsumesMetalArmorDurabilityOnceAtFiveStacks()
    {
        using RustoathFixture fixture = RustoathFixture.Build(new GArray());
        _test.False(
            BattleStatusSemanticTable.HasSemantic(ArmorCrackedStatusId),
            "护甲锈裂状态语义应由锈蚀之誓装备配置提供，不应硬编码在全局状态表。"
        );
        BattleUnitState attacker = fixture.BuildRustoathUnit("armor_corrosion");
        BattleUnitState metalTarget = BuildTarget(
            "metal_armor_target",
            new Vector2I(1, 0),
            IronScaleMailId,
            "eq_iron_scale_mail",
            durability: 30
        );
        metalTarget.SetCurrentHp(200);
        metalTarget.attribute_snapshot.SetValue(AttributeService.HP_MAX, 200);

        for (int hit = 1; hit <= 4; hit++)
        {
            WeaponAbilityCommandTestSupport.IssueBasicAttack(
                fixture.Runtime,
                attacker,
                metalTarget,
                $"rustoath_armor_corrosion_metal_{hit}"
            );
            _test.Eq(
                metalTarget.GetStatusEffect(RustCorrosionStatusId)?.stacks ?? 0,
                hit,
                $"第 {hit} 次真实基础攻击后锈蚀层数应为 {hit}。"
            );
            _test.Eq(
                metalTarget.GetEquipmentView().GetEquippedInstance("body").current_durability,
                30,
                $"第 {hit} 层锈蚀时金属护甲耐久应保持 30。"
            );
        }

        BattleEventBatch fifth = WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            attacker,
            metalTarget,
            "rustoath_armor_corrosion_metal_5"
        );
        _test.Eq(
            metalTarget.GetStatusEffect(RustCorrosionStatusId)?.stacks ?? 0,
            5,
            "第五次命中后护甲锈蚀 reaction 应能读到目标 5 层锈蚀。"
        );
        _test.Eq(
            metalTarget.GetEquipmentView().GetEquippedItemId("body"),
            IronScaleMailId,
            "第五次命中时目标 body 槽仍应装备铁鳞甲。"
        );
        _test.Eq(
            metalTarget.GetEquipmentView().GetEquippedInstance("body")?.current_durability ?? -1,
            6,
            "第五次命中应在读取后真实修改 body 护甲实例耐久。"
        );
        _test.True(
            HasLogLineContaining(fifth, "耐久 30 -> 6"),
            "第五次真实基础攻击的战斗日志应包含护甲耐久 30 -> 6。"
        );
        _test.Eq(
            metalTarget.GetEquipmentView().GetEquippedInstance("body").current_durability,
            6,
            "铁鳞甲实例耐久应被真实修改。"
        );
        _test.True(
            metalTarget.HasStatusEffect(ArmorCrackedStatusId),
            "扣过金属护甲耐久后目标应获得护甲锈裂标记。"
        );
        BattleStatusEffectState cracked = metalTarget.GetStatusEffect(ArmorCrackedStatusId);
        _test.Eq(cracked?.stack_behavior ?? new StringName(""), new StringName("refresh"), "护甲锈裂应由配置声明刷新叠层。");
        _test.Eq(cracked?.stack_limit ?? 0, 1, "护甲锈裂应由配置声明最多 1 层。");
        _test.Eq(cracked?.display_label ?? "", "护甲锈裂", "护甲锈裂显示名应来自装备配置。");
        _test.True(cracked?.counts_as_debuff == true, "护甲锈裂应由配置声明为 debuff。");

        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            attacker,
            metalTarget,
            "rustoath_armor_corrosion_metal_6"
        );
        _test.Eq(
            metalTarget.GetEquipmentView().GetEquippedInstance("body").current_durability,
            6,
            "第六次命中不应继续扣同一件护甲。"
        );

        BattleUnitState leatherTarget = BuildTarget(
            "leather_armor_target",
            new Vector2I(1, 0),
            LeatherJerkinId,
            "eq_leather_jerkin",
            durability: 30
        );
        leatherTarget.SetCurrentHp(200);
        leatherTarget.attribute_snapshot.SetValue(AttributeService.HP_MAX, 200);
        for (int hit = 1; hit <= 5; hit++)
        {
            WeaponAbilityCommandTestSupport.IssueBasicAttack(
                fixture.Runtime,
                attacker,
                leatherTarget,
                $"rustoath_armor_corrosion_leather_{hit}"
            );
        }
        _test.Eq(
            leatherTarget.GetEquipmentView().GetEquippedInstance("body").current_durability,
            30,
            "非金属护甲达到 5 层锈蚀也不应扣耐久。"
        );
        _test.False(
            leatherTarget.HasStatusEffect(ArmorCrackedStatusId),
            "非金属护甲目标不应获得护甲锈裂标记。"
        );
    }

    private void TestRottenBladeAddsAcidDiceOnlyAfterArmorCracked()
    {
        using RustoathFixture fixture = RustoathFixture.Build(new GArray());
        BattleUnitState attacker = fixture.BuildRustoathUnit("rotten_blade");
        BattleUnitState uncrackedTarget = BuildTarget("rotten_blade_uncracked", new Vector2I(1, 0));
        uncrackedTarget.SetCurrentHp(100);
        uncrackedTarget.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);

        int uncrackedHpBefore = uncrackedTarget.GetCurrentHp();
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            attacker,
            uncrackedTarget,
            "rustoath_rotten_blade_uncracked"
        );
        int uncrackedDamage = uncrackedHpBefore - uncrackedTarget.GetCurrentHp();
        _test.False(
            uncrackedTarget.HasStatusEffect(ArmorCrackedStatusId),
            "没有护甲锈裂标记的真实基础攻击不应自己生成护甲锈裂。"
        );

        BattleUnitState crackedTarget = BuildTarget("rotten_blade_cracked", new Vector2I(1, 0));
        crackedTarget.SetCurrentHp(100);
        crackedTarget.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        crackedTarget.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = ArmorCrackedStatusId,
                source_unit_id = attacker.unit_id,
                stacks = 1,
                power = 1,
                duration = 10000,
            }
        );
        int crackedHpBefore = crackedTarget.GetCurrentHp();
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            attacker,
            crackedTarget,
            "rustoath_rotten_blade_cracked"
        );
        int crackedDamage = crackedHpBefore - crackedTarget.GetCurrentHp();
        _test.True(
            crackedDamage > uncrackedDamage,
            "护甲锈裂目标被真实基础攻击命中时，腐朽之刃应通过伤害结算追加 acid 附伤。"
        );
    }

    private void TestRustPowderStormRequiresConsumesFiveRustStacks()
    {
        using RustoathFixture fixture = RustoathFixture.Build(new GArray());
        BattleUnitState holder = fixture.BuildRustoathUnit("rust_powder_storm");
        BattleUnitState target = BuildTarget("storm_target", new Vector2I(1, 0));
        target.SetCurrentHp(100);
        target.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);

        _test.True(
            fixture.SkillDefs.TryGetValue(RustPowderStormSkillId, out SkillDefinition stormSkill),
            "锈粉风暴应是装备授予 SkillDef。"
        );
        if (stormSkill == null)
            return;
        CombatSkillDefinition combat = stormSkill.CombatProfile;
        _test.True(combat != null, "锈粉风暴应有 combat_profile。");
        _test.Eq(combat.ApCost, 1, "锈粉风暴应作为 action 消耗 1AP。");
        _test.Eq(combat.TargetMode, new StringName("unit"), "锈粉风暴当前应选择单位目标。");
        _test.True(
            HasRequiredTargetStatusGate(combat, RustCorrosionStatusId, 5),
            "锈粉风暴效果应通过 typed 字段要求目标至少 5 层锈蚀。"
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
            TryFindSkillEntry(availability, RustPowderStormSkillId, out BattleAvailableSkillEntry entry),
            "装备锈蚀之誓后应出现锈粉风暴装备技能入口。"
        );
        if (entry == null)
            return;
        _test.Eq(
            entry.EntryRef.SourceKind,
            BattleSkillEntrySourceKind.EquipmentSkill,
            "锈粉风暴入口应是一等化 EquipmentSkill。"
        );
        _test.Eq(entry.EquipmentGrantedActionId, RustPowderStormGrantId, "锈粉风暴入口应保留 grant id。");

        target.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = RustCorrosionStatusId,
                source_unit_id = holder.unit_id,
                stacks = 4,
                power = 4,
                duration = 180,
            }
        );
        int blockedHpBefore = target.GetCurrentHp();
        BattleEventBatch blocked = WeaponAbilityCommandTestSupport.IssueUnitSkill(
            fixture.Runtime,
            holder,
            target,
            entry,
            RustPowderStormSkillId,
            "rustoath_rust_powder_storm_blocked",
            previewCommand: false
        );
        _test.True(blocked != null, "不足 5 层时锈粉风暴 IssueCommand 应返回 batch。");
        _test.Eq(target.GetCurrentHp(), blockedHpBefore, "不足 5 层锈蚀时锈粉风暴不应造成伤害。");
        _test.True(
            target.HasStatusEffect(RustCorrosionStatusId),
            "不足 5 层时锈粉风暴不应消耗锈蚀。"
        );
        _test.Eq(
            target.GetStatusEffect(RustCorrosionStatusId)?.stacks ?? 0,
            4,
            "不足 5 层时锈粉风暴不应改写锈蚀层数。"
        );

        target.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = RustCorrosionStatusId,
                source_unit_id = holder.unit_id,
                stacks = 5,
                power = 5,
                duration = 180,
            }
        );
        int appliedHpBefore = target.GetCurrentHp();
        BattleEventBatch applied = WeaponAbilityCommandTestSupport.IssueUnitSkill(
            fixture.Runtime,
            holder,
            target,
            entry,
            RustPowderStormSkillId,
            "rustoath_rust_powder_storm_applied",
            previewCommand: false
        );
        _test.True(applied != null, "5 层锈蚀时锈粉风暴 IssueCommand 应返回 batch。");
        _test.True(target.GetCurrentHp() < appliedHpBefore, "5 层锈蚀时锈粉风暴应通过真实技能命令造成 acid 伤害。");
        _test.False(
            target.HasStatusEffect(RustCorrosionStatusId),
            "锈粉风暴成功后应消耗目标全部锈蚀。"
        );
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

    private static bool HasLogLineContaining(BattleEventBatch batch, string expected)
    {
        foreach (string line in batch?.LogLinesTyped ?? Array.Empty<string>())
            if (!string.IsNullOrEmpty(line) && line.Contains(expected))
                return true;
        return false;
    }

    private void AssertRustoathArmorCorrosionBindingShape(RustoathFixture fixture)
    {
        _test.True(
            fixture.Bindings.TryGetValue(ArmorCorrosionBindingId, out EquipmentAbilityBindingDefinition binding),
            "护甲锈蚀 binding 应被 typed registry 投影。"
        );
        if (binding == null)
            return;
        _test.Eq(binding.Reactions.Count, 1, "护甲锈蚀 binding 应只有一个 after-hit reaction。");
        if (binding.Reactions.Count != 1)
            return;
        EquipmentAbilityReactionDefinition reaction = binding.Reactions[0];
        _test.Eq(reaction.Priority, 20, "护甲锈蚀 reaction priority 应晚于锈毒。");
        _test.Eq(
            reaction.ConditionGroup?.Conditions?.Count ?? 0,
            3,
            "护甲锈蚀 reaction 应投影出 3 个条件。"
        );
        _test.True(
            HasStatusStackCompare(reaction.ConditionGroup, RustCorrosionStatusId, "gte", 5),
            "护甲锈蚀条件应读取目标 rustoath_rust_corrosion >= 5。"
        );
        _test.True(
            HasStatusStackCompare(reaction.ConditionGroup, ArmorCrackedStatusId, "lt", 1),
            "护甲锈蚀条件应读取目标 rustoath_armor_cracked < 1。"
        );
        _test.True(
            HasEquipmentTagCondition(
                reaction.ConditionGroup,
                "target",
                "body",
                new StringName[] { "armor", "metal" }
            ),
            "护甲锈蚀条件应要求目标 body 装备同时具有 armor 与 metal 标签。"
        );
    }

    private static bool HasStatusStackCompare(
        EquipmentConditionGroupDefinition group,
        StringName statusId,
        StringName compare,
        int intLiteral
    )
    {
        foreach (EquipmentAbilityConditionDefinition condition in group?.Conditions ?? Array.Empty<EquipmentAbilityConditionDefinition>())
        {
            if (
                condition?.PayloadDefinition is CompareFactConditionPayloadDefinition payload
                && payload.Compare == compare
                && payload.Left?.FactId == "status_stacks"
                && payload.Left?.Subject == "target"
                && payload.Left?.StatusId == statusId
                && payload.Right?.QueryKind == "literal"
                && payload.Right?.IntLiteral == intLiteral
            )
            {
                return true;
            }
        }
        return false;
    }

    private static bool HasEquipmentTagCondition(
        EquipmentConditionGroupDefinition group,
        StringName subject,
        StringName equipmentSelector,
        IReadOnlyList<StringName> requiredTags
    )
    {
        foreach (EquipmentAbilityConditionDefinition condition in group?.Conditions ?? Array.Empty<EquipmentAbilityConditionDefinition>())
        {
            if (
                condition?.PayloadDefinition is HasEquipmentTagConditionPayloadDefinition payload
                && payload.Subject == subject
                && payload.EquipmentSelector == equipmentSelector
                && HasAllTags(payload.AllTags, requiredTags)
            )
            {
                return true;
            }
        }
        return false;
    }

    private static bool HasAllTags(
        IReadOnlyList<StringName> actualTags,
        IReadOnlyList<StringName> requiredTags
    )
    {
        foreach (StringName requiredTag in requiredTags)
        {
            bool matched = false;
            foreach (StringName actualTag in actualTags ?? Array.Empty<StringName>())
            {
                if (actualTag == requiredTag)
                {
                    matched = true;
                    break;
                }
            }
            if (!matched)
                return false;
        }
        return true;
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
            BattleEquipmentAbilitySourceReadView source in
            unit.GetEquipmentAbilitySourcesReadViewTyped()
        )
        {
            if (source?.AbilityIds?.Contains(bindingId) == true)
                return source;
        }
        return null;
    }

    private static BattleUnitState BuildTarget(
        StringName unitId,
        Vector2I coord,
        StringName bodyItemId = default,
        StringName bodyInstanceId = default,
        int durability = 0
    )
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
        EquipmentState equipment = new();
        if (bodyItemId != default && bodyItemId != "")
        {
            EquipmentInstanceState instance =
                EquipmentInstanceState.CreateInstance(bodyItemId, bodyInstanceId);
            instance.rarity = (int)EquipmentInstanceState.RarityTier.COMMON;
            instance.current_durability = durability;
            equipment.SetEquippedEntry(
                "body",
                bodyItemId,
                new GStringNameArray { "body" },
                instance
            );
        }
        unit.SetEquipmentView(equipment);
        return unit;
    }

    private sealed class RustoathFixture : IDisposable
    {
        private readonly CharacterManagementModule _characterManagement;
        private readonly PartyState _partyState;

        private RustoathFixture(
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

        internal static RustoathFixture Build(GArray damageRolls)
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
            return new RustoathFixture(characterManagement, partyState, runtime, snapshot);
        }

        internal BattleUnitState BuildUnitWithoutWeapon(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            return BuildSingleAllyUnit(label);
        }

        internal BattleUnitState BuildRustoathUnit(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            member.equipment_state.SetEquippedEntry(
                "main_hand",
                RustoathItemId,
                new GStringNameArray { "main_hand" },
                EquipmentInstanceState.CreateInstance(RustoathItemId, $"eq_rustoath_{label}")
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
