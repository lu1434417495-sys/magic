using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_wyrmbreak_weapon_ability_regression : LifecycleTestSceneTree
{
    private static readonly StringName WyrmbreakItemId = "weapon_unique_sword_wyrmbreak_007";
    private static readonly StringName DragonSlayerTraitId =
        "weapon.sword.wyrmbreak.dragon_slayer";
    private static readonly StringName BoneStealingFuryTraitId =
        "weapon.sword.wyrmbreak.bone_stealing_fury";
    private static readonly StringName DragonSoulExtensionTraitId =
        "weapon.sword.wyrmbreak.dragon_soul_extension";
    private static readonly StringName DragonSoulBurstTraitId =
        "weapon.sword.wyrmbreak.dragon_soul_burst";
    private static readonly StringName DragonSlayerBindingId =
        "binding.weapon.sword.wyrmbreak.dragon_slayer";
    private static readonly StringName BoneStealingFuryBindingId =
        "binding.weapon.sword.wyrmbreak.bone_stealing_fury";
    private static readonly StringName DragonSoulExtensionBindingId =
        "binding.weapon.sword.wyrmbreak.dragon_soul_extension";
    private static readonly StringName DragonSoulBurstBindingId =
        "binding.weapon.sword.wyrmbreak.dragon_soul_burst";
    private static readonly StringName DragonSoulExtensionSkillId =
        "weapon_sword_wyrmbreak_dragon_soul_extension";
    private static readonly StringName DragonSoulBurstSkillId =
        "weapon_sword_wyrmbreak_dragon_soul_burst";
    private static readonly StringName DragonSoulExtensionGrantId =
        "grant.wyrmbreak.dragon_soul_extension.skill";
    private static readonly StringName DragonSoulBurstGrantId =
        "grant.wyrmbreak.dragon_soul_burst.skill";
    private static readonly StringName DragonSoulFuryStateKey = "dragon_soul_fury";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestWyrmbreakProjectsRealContentOntoBattleUnitAndClearsOnUnequip();
            TestDragonSlayerAddsFireDamageAndBypassesFireHalfButNotImmune();
            TestBoneStealingFuryChargesOnlyOnNonDragonWeaponHits();
            TestDragonSoulExtensionConsumesThreeFuryAsRangeTwoWeaponAttack();
            TestDragonSoulBurstConsumesFiveFuryAndResolvesRealGroundLineDamage();
            RequestTestExit(_test.Finish("Wyrmbreak weapon ability regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Wyrmbreak weapon ability regression"));
        }
    }

    private void TestWyrmbreakProjectsRealContentOntoBattleUnitAndClearsOnUnequip()
    {
        using WyrmbreakFixture fixture = WyrmbreakFixture.Build(new GArray());
        _test.True(fixture.ItemDefs.ContainsKey(WyrmbreakItemId), "真实物品内容应包含龙骨断剑。");
        _test.True(fixture.TraitDefs.ContainsKey(DragonSlayerTraitId), "真实 trait 应包含屠龙。");
        _test.True(fixture.TraitDefs.ContainsKey(BoneStealingFuryTraitId), "真实 trait 应包含窃骨之怒。");
        _test.True(fixture.TraitDefs.ContainsKey(DragonSoulExtensionTraitId), "真实 trait 应包含龙魂延伸。");
        _test.True(fixture.TraitDefs.ContainsKey(DragonSoulBurstTraitId), "真实 trait 应包含龙魂爆发。");
        _test.True(fixture.Bindings.ContainsKey(DragonSlayerBindingId), "真实装备能力应包含屠龙 binding。");
        _test.True(
            fixture.Bindings.ContainsKey(BoneStealingFuryBindingId),
            "真实装备能力应包含窃骨之怒 binding。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(DragonSoulExtensionBindingId),
            "真实装备能力应包含龙魂延伸 binding。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(DragonSoulBurstBindingId),
            "真实装备能力应包含龙魂爆发 binding。"
        );
        _test.True(
            fixture.SkillDefs.ContainsKey(DragonSoulExtensionSkillId),
            "真实技能内容应包含龙魂延伸装备技能。"
        );
        _test.True(
            fixture.SkillDefs.ContainsKey(DragonSoulBurstSkillId),
            "真实技能内容应包含龙魂爆发装备技能。"
        );

        ItemDef rawWyrmbreak = ResourceLoader.Load<ItemDef>(
            "res://data/configs/items/weapon_unique_longsword_wyrmbreak.tres"
        );
        _test.True(rawWyrmbreak != null, "龙骨断剑原始资源应能加载。");
        if (rawWyrmbreak != null)
        {
            _test.Eq(
                rawWyrmbreak.base_item_id,
                new StringName("weapon_type_longsword_base"),
                "龙骨断剑应继承 longsword 模板。"
            );
        }

        BattleUnitState baseline = fixture.BuildUnitWithoutWeapon("baseline");
        BattleUnitState equipped = fixture.BuildWyrmbreakUnit("projection");
        _test.Eq(equipped.weapon_item_id, WyrmbreakItemId, "龙骨断剑装备后 unit 应保留真实 item_id。");
        _test.Eq(equipped.weapon_profile_type_id, new StringName("longsword"), "龙骨断剑应投影为 longsword。");
        _test.True(equipped.weapon_is_versatile, "龙骨断剑应保留 versatile 投影。");
        _test.Eq(equipped.weapon_attack_range, 1, "龙骨断剑基础攻击距离应为 1。");
        _test.Eq(equipped.weapon_one_handed_dice?.dice_count ?? 0, 1, "龙骨断剑单手应为 1D8+5。");
        _test.Eq(equipped.weapon_one_handed_dice?.dice_sides ?? 0, 8, "龙骨断剑单手应为 1D8+5。");
        _test.Eq(equipped.weapon_one_handed_dice?.flat_bonus ?? 0, 5, "龙骨断剑单手应为 1D8+5。");
        _test.Eq(equipped.weapon_two_handed_dice?.dice_sides ?? 0, 10, "龙骨断剑双手应为 1D10+5。");
        _test.Eq(equipped.weapon_two_handed_dice?.flat_bonus ?? 0, 5, "龙骨断剑双手应为 1D10+5。");
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            DragonSlayerTraitId,
            DragonSlayerBindingId,
            "eq_wyrmbreak_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            BoneStealingFuryTraitId,
            BoneStealingFuryBindingId,
            "eq_wyrmbreak_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            DragonSoulExtensionTraitId,
            DragonSoulExtensionBindingId,
            "eq_wyrmbreak_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            DragonSoulBurstTraitId,
            DragonSoulBurstBindingId,
            "eq_wyrmbreak_projection"
        );
        AssertDragonSoulExtensionSkillConfig(fixture);
        AssertDragonSoulBurstSkillConfig(fixture);

        BattleSkillAvailabilityView initialView = BuildEquipmentSkillView(
            fixture,
            equipped,
            BuildState("wyrmbreak_projection_availability", equipped, null)
        );
        _test.True(
            TryFindSkillEntry(initialView, DragonSoulExtensionSkillId, out BattleAvailableSkillEntry extensionEntry),
            "装备龙骨断剑后 unit 应有龙魂延伸技能入口。"
        );
        _test.True(
            TryFindSkillEntry(initialView, DragonSoulBurstSkillId, out BattleAvailableSkillEntry burstEntry),
            "装备龙骨断剑后 unit 应有龙魂爆发技能入口。"
        );
        if (extensionEntry != null)
            _test.False(extensionEntry.IsSelectable, "0 层龙魂怒气时龙魂延伸应存在但不可选。");
        if (burstEntry != null)
            _test.False(burstEntry.IsSelectable, "0 层龙魂怒气时龙魂爆发应存在但不可选。");

        equipped.GetEquipmentView().ClearSlot("main_hand");
        fixture.Runtime._unit_factory.RefreshBattleUnit(equipped);
        _test.Eq(equipped.weapon_item_id, new StringName(""), "移除龙骨断剑后 weapon_item_id 应清空。");
        _test.Eq(
            equipped.weapon_profile_type_id,
            baseline.weapon_profile_type_id,
            "移除龙骨断剑后 weapon_profile_type_id 应回到装备前状态。"
        );
        _test.Eq(
            equipped.weapon_attack_range,
            baseline.weapon_attack_range,
            "移除龙骨断剑后攻击距离应回到装备前状态。"
        );
        _test.Eq(equipped.equipment_ability_sources.Count, 0, "移除龙骨断剑后装备能力源应清空。");
    }

    private void AssertDragonSoulExtensionSkillConfig(WyrmbreakFixture fixture)
    {
        SkillDefinition skill = fixture.SkillDefs[DragonSoulExtensionSkillId];
        CombatSkillDefinition combat = skill.CombatProfile;
        _test.True(combat != null, "龙魂延伸应有 combat_profile。");
        if (combat == null)
            return;
        _test.Eq(combat.TargetMode, new StringName("unit"), "龙魂延伸应是单位目标武器攻击。");
        _test.Eq(combat.TargetTeamFilter, new StringName("enemy"), "龙魂延伸应只选择敌人。");
        _test.Eq(combat.RangeValue, 2, "龙魂延伸应把本次武器攻击距离落成 2 格。");
        _test.Eq(combat.ApCost, 1, "龙魂延伸应消耗 1 AP。");
        _test.Eq(combat.EffectDefinitions.Count, 1, "龙魂延伸应只有一个武器伤害 effect。");
        CombatEffectDefinition damage = combat.EffectDefinitions[0];
        _test.True(damage.RequiresWeapon, "龙魂延伸伤害应要求装备武器。");
        _test.True(damage.AddWeaponDice, "龙魂延伸伤害应使用武器骰。");
        _test.True(damage.UseWeaponPhysicalDamageTag, "龙魂延伸应使用当前武器物理伤害标签。");
        _test.True(damage.ResolveAsWeaponAttack, "龙魂延伸应按武器攻击结算装备 after-hit。");
    }

    private void AssertDragonSoulBurstSkillConfig(WyrmbreakFixture fixture)
    {
        SkillDefinition skill = fixture.SkillDefs[DragonSoulBurstSkillId];
        CombatSkillDefinition combat = skill.CombatProfile;
        _test.True(combat != null, "龙魂爆发应有 combat_profile。");
        if (combat == null)
            return;
        _test.Eq(combat.TargetMode, new StringName("ground"), "龙魂爆发应使用地面目标。");
        _test.Eq(combat.TargetTeamFilter, new StringName("enemy"), "龙魂爆发应只影响敌人。");
        _test.Eq(combat.AreaPattern, new StringName("line"), "龙魂爆发应落成线形范围。");
        _test.Eq(combat.AreaValue, 4, "龙魂爆发 20 尺应落成 4 格线。");
        _test.Eq(combat.ApCost, 1, "龙魂爆发应消耗 1 AP。");
        _test.Eq(combat.EffectDefinitions.Count, 1, "龙魂爆发应只有一个 fire damage effect。");
        CombatEffectDefinition damage = combat.EffectDefinitions[0];
        _test.Eq(damage.DamageTag, new StringName("fire"), "龙魂爆发应造成 fire 伤害。");
        _test.Eq(damage.DiceCount, 6, "龙魂爆发基础伤害应为 6D6。");
        _test.Eq(damage.DiceSides, 6, "龙魂爆发基础伤害应为 6D6。");
        _test.Eq(damage.SaveDc, 16, "龙魂爆发应使用敏捷 DC16。");
        _test.Eq(damage.SaveAbility, new StringName("agility"), "龙魂爆发应使用 agility 豁免。");
        _test.True(damage.SavePartialOnSuccess, "龙魂爆发豁免成功应减半。");
        _test.True(
            ContainsStringName(damage.MitigationBypassDamageTags, "fire"),
            "龙魂爆发应声明 fire mitigation bypass。"
        );
        _test.True(
            ContainsStringName(damage.MitigationBypassTiers, "half"),
            "龙魂爆发只应绕过 half 抗性，不应绕过 immune。"
        );
        _test.Eq(damage.BonusCondition, new StringName("target_creature_type"), "龙魂爆发对龙额外骰应走目标生物分类条件。");
        _test.Eq(damage.BonusConditionCreatureTypeTag, new StringName("dragon"), "龙魂爆发额外骰应只匹配 dragon。");
        _test.Eq(damage.BonusDamageDiceCount, 2, "龙魂爆发对 dragon 应额外 2D6。");
        _test.Eq(damage.BonusDamageDiceSides, 6, "龙魂爆发对 dragon 应额外 2D6。");
    }

    private void TestDragonSlayerAddsFireDamageAndBypassesFireHalfButNotImmune()
    {
        int plainDragonDamage = MeasureBasicAttackDamage(
            new[] { new StringName("dragon") },
            "",
            stripAbilitySources: true
        );
        int dragonDamage = MeasureBasicAttackDamage(new[] { new StringName("dragon") }, "");
        int dragonHalfDamage = MeasureBasicAttackDamage(new[] { new StringName("dragon") }, "half");
        int dragonImmuneDamage = MeasureBasicAttackDamage(new[] { new StringName("dragon") }, "immune");
        int dragonbornDamage = MeasureBasicAttackDamage(new[] { new StringName("dragonborn") }, "");

        _test.True(dragonDamage > plainDragonDamage, "命中 dragon 时屠龙应追加火焰伤害。");
        _test.True(dragonbornDamage > plainDragonDamage, "命中 dragonborn 时屠龙应追加较小火焰伤害。");
        _test.True(dragonDamage > dragonbornDamage, "dragon 的屠龙额外伤害应高于 dragonborn。");
        _test.Eq(dragonHalfDamage, dragonDamage, "屠龙火焰伤害应绕过 fire half 抗性。");
        _test.Eq(dragonImmuneDamage, plainDragonDamage, "屠龙火焰伤害不应绕过 fire immune。");
    }

    private void TestBoneStealingFuryChargesOnlyOnNonDragonWeaponHits()
    {
        using WyrmbreakFixture fixture = WyrmbreakFixture.Build(new GArray());
        BattleUnitState holder = fixture.BuildWyrmbreakUnit("fury_charge");
        BattleUnitState dragon = BuildTarget("fury_dragon", new Vector2I(1, 0), "dragon");
        dragon.current_hp = 300;
        dragon.attribute_snapshot.SetValue(AttributeService.HP_MAX, 300);
        BattleUnitState dragonborn = BuildTarget("fury_dragonborn", new Vector2I(1, 0), "dragonborn");
        dragonborn.current_hp = 300;
        dragonborn.attribute_snapshot.SetValue(AttributeService.HP_MAX, 300);
        BattleUnitState humanoid = BuildTarget("fury_humanoid", new Vector2I(1, 0), "humanoid");
        humanoid.current_hp = 500;
        humanoid.attribute_snapshot.SetValue(AttributeService.HP_MAX, 500);

        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            holder,
            dragon,
            "wyrmbreak_fury_dragon",
            previewCommand: false
        );
        _test.Eq(GetAbilityState(holder, BoneStealingFuryBindingId, DragonSoulFuryStateKey), 0, "命中 dragon 不应充能。");

        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            holder,
            dragonborn,
            "wyrmbreak_fury_dragonborn",
            previewCommand: false
        );
        _test.Eq(GetAbilityState(holder, BoneStealingFuryBindingId, DragonSoulFuryStateKey), 1, "dragonborn 不等于 dragon，应按非龙充能。");

        for (int hit = 0; hit < 5; hit++)
        {
            WeaponAbilityCommandTestSupport.IssueBasicAttack(
                fixture.Runtime,
                holder,
                humanoid,
                $"wyrmbreak_fury_humanoid_{hit}",
                previewCommand: false
            );
        }
        _test.Eq(GetAbilityState(holder, BoneStealingFuryBindingId, DragonSoulFuryStateKey), 5, "龙魂怒气应最多 5 层。");
    }

    private void TestDragonSoulExtensionConsumesThreeFuryAsRangeTwoWeaponAttack()
    {
        using WyrmbreakFixture fixture = WyrmbreakFixture.Build(new GArray());
        BattleUnitState holder = fixture.BuildWyrmbreakUnit("extension");
        SetAbilityState(holder, BoneStealingFuryBindingId, DragonSoulFuryStateKey, 3);
        BattleUnitState target = BuildTarget("extension_target", new Vector2I(2, 0), "humanoid");
        target.current_hp = 100;
        target.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        BattleState state = BuildState("wyrmbreak_extension", holder, target);
        BattleSkillAvailabilityView view = BuildEquipmentSkillView(fixture, holder, state);
        _test.True(
            TryFindSkillEntry(view, DragonSoulExtensionSkillId, out BattleAvailableSkillEntry entry),
            "3 层龙魂怒气时应解析龙魂延伸入口。"
        );
        _test.True(entry?.IsSelectable == true, "3 层龙魂怒气时龙魂延伸应可选。");
        if (entry == null)
            return;

        WeaponAbilityCommandTestSupport.PrimeActionResources(holder, ap: 2);
        fixture.Runtime.SetupStateForTests(state);
        int before = target.current_hp;
        BattleCommand command = WeaponAbilityCommandTestSupport.BuildUnitSkillCommand(
            holder,
            target,
            entry,
            DragonSoulExtensionSkillId
        );
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.True(preview.allowed, $"龙魂延伸 range=2 的真实命令 preview 应允许。logs={JoinLogs(preview.LogLinesTyped)}");
        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        _test.True(target.current_hp < before, $"龙魂延伸应通过真实武器攻击造成伤害。logs={JoinLogs(batch?.LogLinesTyped)}");
        _test.Eq(
            GetAbilityState(holder, BoneStealingFuryBindingId, DragonSoulFuryStateKey),
            1,
            "龙魂延伸应消耗 3 层龙魂怒气，并因真实武器命中非 dragon 目标重新获得 1 层。"
        );
    }

    private void TestDragonSoulBurstConsumesFiveFuryAndResolvesRealGroundLineDamage()
    {
        int dragonDamage = MeasureBurstDamage(new[] { new StringName("dragon") }, "");
        int dragonHalfDamage = MeasureBurstDamage(new[] { new StringName("dragon") }, "half");
        int dragonImmuneDamage = MeasureBurstDamage(new[] { new StringName("dragon") }, "immune");
        int humanoidDamage = MeasureBurstDamage(new[] { new StringName("humanoid") }, "");

        _test.True(dragonDamage > humanoidDamage, "龙魂爆发对 dragon 应获得额外 2D6。");
        _test.Eq(dragonHalfDamage, dragonDamage, "龙魂爆发 fire 伤害应绕过 fire half 抗性。");
        _test.Eq(dragonImmuneDamage, 0, "龙魂爆发 fire 伤害不应绕过 fire immune。");
    }

    private static int MeasureBasicAttackDamage(
        IReadOnlyList<StringName> targetTags,
        StringName fireMitigation,
        bool stripAbilitySources = false
    )
    {
        using WyrmbreakFixture fixture = WyrmbreakFixture.Build(new GArray());
        BattleUnitState attacker = fixture.BuildWyrmbreakUnit(
            stripAbilitySources ? "plain_attack" : $"attack_{fireMitigation}"
        );
        if (stripAbilitySources)
            attacker.equipment_ability_sources.Clear();
        BattleUnitState target = BuildTarget("attack_target", new Vector2I(1, 0), targetTags);
        target.current_hp = 200;
        target.attribute_snapshot.SetValue(AttributeService.HP_MAX, 200);
        if (fireMitigation != "")
            target.damage_resistances["fire"] = fireMitigation;
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            attacker,
            target,
            $"wyrmbreak_attack_{fireMitigation}_{stripAbilitySources}",
            previewCommand: false
        );
        return 200 - target.current_hp;
    }

    private static int MeasureBurstDamage(IReadOnlyList<StringName> targetTags, StringName fireMitigation)
    {
        using WyrmbreakFixture fixture = WyrmbreakFixture.Build(new FixedFailedSaveDamageResolver());
        BattleUnitState holder = fixture.BuildWyrmbreakUnit($"burst_{fireMitigation}");
        SetAbilityState(holder, BoneStealingFuryBindingId, DragonSoulFuryStateKey, 5);
        BattleUnitState target = BuildTarget("burst_target", new Vector2I(2, 0), targetTags);
        target.current_hp = 200;
        target.attribute_snapshot.SetValue(AttributeService.HP_MAX, 200);
        if (fireMitigation != "")
            target.damage_resistances["fire"] = fireMitigation;
        BattleState state = BuildState("wyrmbreak_burst", holder, target);
        BattleSkillAvailabilityView view = BuildEquipmentSkillView(fixture, holder, state);
        if (!TryFindSkillEntry(view, DragonSoulBurstSkillId, out BattleAvailableSkillEntry entry))
            throw new InvalidOperationException("missing dragon soul burst entry.");
        if (!entry.IsSelectable)
            throw new InvalidOperationException($"dragon soul burst entry disabled: {entry.DisabledReason}");
        WeaponAbilityCommandTestSupport.PrimeActionResources(holder, ap: 2);
        fixture.Runtime.SetupStateForTests(state);
        BattleCommand command = new()
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = holder.unit_id,
            skill_entry_id = entry.EntryRef.SkillEntryId,
            skill_id = DragonSoulBurstSkillId,
            target_coord = target.coord,
        };
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        if (preview?.allowed != true)
            throw new InvalidOperationException($"dragon soul burst preview blocked: {JoinLogs(preview?.LogLinesTyped)}");
        fixture.Runtime.IssueCommand(command);
        int damage = 200 - target.current_hp;
        int remaining = GetAbilityState(holder, BoneStealingFuryBindingId, DragonSoulFuryStateKey);
        if (remaining != 0)
            throw new InvalidOperationException($"dragon soul burst should consume 5 fury, remaining={remaining}.");
        return damage;
    }

    private static BattleSkillAvailabilityView BuildEquipmentSkillView(
        WyrmbreakFixture fixture,
        BattleUnitState unit,
        BattleState state
    )
    {
        BattleSkillAvailabilityService service = new(fixture.SkillDefs, fixture.Bindings);
        return service.BuildView(
            new BattleSkillAvailabilityQuery
            {
                User = unit,
                IncludeKnownSkills = false,
                IncludeEquipmentSkills = true,
                Consumer = BattleSkillAvailabilityConsumer.ManualSelection,
                WorldStep = 0,
                BattleState = state,
            }
        );
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

    private static int GetAbilityState(
        BattleUnitState unit,
        StringName bindingId,
        StringName stateKey
    )
    {
        StringName key = FindChargeKey(unit, bindingId, stateKey);
        return key == "" ? 0 : unit.GetPerBattleChargeTyped(key, 0);
    }

    private static void SetAbilityState(
        BattleUnitState unit,
        StringName bindingId,
        StringName stateKey,
        int value
    )
    {
        StringName key = FindChargeKey(unit, bindingId, stateKey);
        if (key == "")
            throw new InvalidOperationException($"missing ability state source for {bindingId}/{stateKey}");
        unit.SetPerBattleChargeTyped(key, value);
    }

    private static StringName FindChargeKey(
        BattleUnitState unit,
        StringName bindingId,
        StringName stateKey
    )
    {
        string suffix = $"|{stateKey}";
        foreach (StringName key in unit.GetPerBattleChargesTyped().Keys)
        {
            if (key.ToString().EndsWith(suffix, StringComparison.Ordinal))
                return key;
        }
        BattleEquipmentAbilitySourceState source = FindSource(unit, bindingId);
        StringName sourceKey = source?.SourceEquipmentInstanceId ?? new StringName("");
        if (sourceKey == "")
            sourceKey = source?.EquipmentDefId ?? new StringName("");
        if (sourceKey == "")
            sourceKey = source?.EffectiveInstanceKey ?? new StringName("");
        return sourceKey == ""
            ? new StringName("")
            : new StringName($"equipment_ability|state|{sourceKey}|{stateKey}");
    }

    private static bool ContainsStringName(IReadOnlyList<StringName> values, StringName expected)
    {
        foreach (StringName value in values ?? Array.Empty<StringName>())
            if (value == expected)
                return true;
        return false;
    }

    private static string JoinLogs(IEnumerable<string> values) =>
        values == null ? "" : string.Join(" | ", values);

    private static BattleState BuildState(
        StringName battleId,
        BattleUnitState attacker,
        BattleUnitState target
    )
    {
        BattleState state = WeaponAbilityCommandTestSupport.BuildFlatState(
            battleId,
            attacker,
            target,
            mapSize: new Vector2I(6, 6)
        );
        return state;
    }

    private static BattleUnitState BuildTarget(StringName unitId, Vector2I coord, StringName tag) =>
        BuildTarget(unitId, coord, new[] { tag });

    private static BattleUnitState BuildTarget(
        StringName unitId,
        Vector2I coord,
        IReadOnlyList<StringName> tags
    )
    {
        BattleUnitState unit = new()
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = "enemy",
            is_alive = true,
            current_hp = 30,
        };
        unit.SetAnchorCoord(coord);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 14);
        unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 0);
        unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 0);
        unit.attribute_snapshot.SetValue(
            UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Agility),
            10
        );
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 30);
        foreach (StringName tag in tags ?? Array.Empty<StringName>())
        {
            if (tag != "")
                unit.creature_type_tags.Add(tag);
        }
        unit.SetEquipmentView(new EquipmentState());
        return unit;
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
        if (!unit.effective_trait_ids.Contains(traitId))
            throw new InvalidOperationException($"unit missing trait {traitId}.");
        BattleEquipmentAbilitySourceState source = FindSource(unit, bindingId);
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

    private static BattleEquipmentAbilitySourceState FindSource(
        BattleUnitState unit,
        StringName bindingId
    )
    {
        foreach (BattleEquipmentAbilitySourceState source in unit?.equipment_ability_sources ?? new List<BattleEquipmentAbilitySourceState>())
        {
            if (source?.AbilityIds?.Contains(bindingId) == true)
                return source;
        }
        return null;
    }

    private sealed class WyrmbreakFixture : IDisposable
    {
        private readonly ItemContentRegistry _itemRegistry;
        private readonly ProgressionContentRegistry _progressionRegistry;
        private readonly PartyState _partyState;

        private WyrmbreakFixture(
            ItemContentRegistry itemRegistry,
            ProgressionContentRegistry progressionRegistry,
            PartyState partyState,
            BattleRuntimeModule runtime
        )
        {
            _itemRegistry = itemRegistry;
            _progressionRegistry = progressionRegistry;
            _partyState = partyState;
            Runtime = runtime;
            ItemDefs = itemRegistry.GetItemDefsTyped();
            SkillDefs = progressionRegistry.GetSkillDefinitionsTyped();
            TraitDefs = progressionRegistry.GetTraitDefsTyped();
            Bindings = progressionRegistry.GetEquipmentAbilityBindingDefinitionsTyped();
        }

        internal BattleRuntimeModule Runtime { get; }
        internal IReadOnlyDictionary<StringName, ItemDefinition> ItemDefs { get; }
        internal IReadOnlyDictionary<StringName, SkillDefinition> SkillDefs { get; }
        internal IReadOnlyDictionary<StringName, TraitDefinition> TraitDefs { get; }
        internal IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> Bindings { get; }

        internal static WyrmbreakFixture Build(GArray damageRolls)
        {
            return Build(new FixedRollDamageResolver(damageRolls));
        }

        internal static WyrmbreakFixture Build(BattleDamageResolver damageResolver)
        {
            ItemContentRegistry itemRegistry = new();
            ProgressionContentRegistry progressionRegistry = new();
            PartyState partyState = BuildPartyState("hero");
            CharacterManagementModule characterManagement = new();
            characterManagement.setup(
                partyState,
                progressionRegistry.GetSkillDefinitionsTyped(),
                progressionRegistry.GetProfessionDefsTyped(),
                progressionRegistry.GetAchievementDefsTyped(),
                itemRegistry.GetItemDefsTyped(),
                progressionRegistry.GetQuestDefsTyped(),
                progressionRegistry.GetTraitDefsTyped(),
                null,
                new ProgressionIdentityCatalogData()
            );

            BattleRuntimeModule runtime = new();
            runtime.setup(
                characterManagement,
                progressionRegistry.GetSkillDefinitionsTyped(),
                item_defs: itemRegistry.GetItemDefsTyped(),
                trait_defs: progressionRegistry.GetTraitDefsTyped(),
                equipment_ability_bindings: progressionRegistry.GetEquipmentAbilityBindingDefinitionsTyped()
            );
            runtime.ConfigureDamageResolverForTests(damageResolver ?? new FixedRollDamageResolver(new GArray()));
            runtime.ConfigureHitResolverForTests(new FixedHitResolver(10));
            return new WyrmbreakFixture(itemRegistry, progressionRegistry, partyState, runtime);
        }

        internal BattleUnitState BuildUnitWithoutWeapon(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            return BuildSingleAllyUnit(label);
        }

        internal BattleUnitState BuildWyrmbreakUnit(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            member.equipment_state.SetEquippedEntry(
                "main_hand",
                WyrmbreakItemId,
                new GStringNameArray { "main_hand" },
                EquipmentInstanceState.CreateInstance(
                    WyrmbreakItemId,
                    $"eq_wyrmbreak_{label}"
                )
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
            _itemRegistry?.Dispose();
            _progressionRegistry?.Dispose();
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
