using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_rustanchor_weapon_ability_regression : LifecycleTestSceneTree
{
    private static readonly StringName ItemId = "weapon_unique_greataxe_rustanchor";
    private static readonly StringName SunkAnchorTraitId = "weapon.axe.rustanchor.sunk_anchor_stance";
    private static readonly StringName RustChainTraitId = "weapon.axe.rustanchor.rust_chain_bite";
    private static readonly StringName NoReturnTraitId = "weapon.axe.rustanchor.no_return_chop";
    private static readonly StringName SunkAnchorBindingId = "binding.weapon.axe.rustanchor.sunk_anchor_stance";
    private static readonly StringName RustChainBindingId = "binding.weapon.axe.rustanchor.rust_chain_bite";
    private static readonly StringName NoReturnBindingId = "binding.weapon.axe.rustanchor.no_return_chop";
    private static readonly StringName SunkAnchorSkillId = "weapon_axe_rustanchor_sunk_anchor_stance";
    private static readonly StringName SunkAnchorGrantId = "grant.rustanchor.sunk_anchor_stance.skill";
    private static readonly StringName SunkAnchorStatusId = "rustanchor_sunk_anchor";
    private static readonly StringName RustChainStatusId = "rustanchor_rust_chain";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestRustanchorProjectsContentAndSkillEntry();
            TestSunkAnchorSkillAppliesStatusBlocksForcedMoveAndReducesDamage();
            TestRustChainAndNoReturnChopAfterHit();
            RequestTestExit(_test.Finish("Rustanchor weapon ability regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Rustanchor weapon ability regression"));
        }
    }

    private void TestRustanchorProjectsContentAndSkillEntry()
    {
        using RustanchorFixture fixture = RustanchorFixture.Build(new GArray());
        _test.True(fixture.ItemDefs.ContainsKey(ItemId), "真实物品内容应包含锈锚。");
        foreach (StringName traitId in new[] { SunkAnchorTraitId, RustChainTraitId, NoReturnTraitId })
            _test.True(fixture.TraitDefs.ContainsKey(traitId), $"锈锚应包含 trait {traitId}。");
        foreach (StringName bindingId in new[] { SunkAnchorBindingId, RustChainBindingId, NoReturnBindingId })
            _test.True(fixture.Bindings.ContainsKey(bindingId), $"锈锚应包含 binding {bindingId}。");
        _test.True(fixture.SkillDefs.ContainsKey(SunkAnchorSkillId), "沉锚守势应落成真实 SkillDef，而不是 trait 文本。");

        ItemDef rawItem = ResourceLoader.Load<ItemDef>(
            "res://data/configs/items/weapon_unique_greataxe_rustanchor.tres"
        );
        _test.True(rawItem != null, "锈锚原始资源应能加载。");
        if (rawItem != null)
        {
            _test.Eq(rawItem.item_id, ItemId, "锈锚 item_id 不应带源表数字。");
            _test.Eq(rawItem.display_name, "锈锚", "锈锚显示名应匹配设计。");
            _test.Eq(rawItem.base_item_id, new StringName("weapon_type_greataxe_base"), "锈锚应继承 greataxe。");
            _test.Eq(rawItem.base_price, 38000, "锈锚价格应为 38000。");
            _test.Eq(rawItem.trait_ids.Count, 3, "锈锚应有且只有 3 个新特性。");
            foreach (StringName traitId in new[] { SunkAnchorTraitId, RustChainTraitId, NoReturnTraitId })
                _test.True(rawItem.trait_ids.Contains(traitId), $"锈锚 item 应声明 {traitId}。");

            WeaponProfileDef profile = rawItem.weapon_profile as WeaponProfileDef;
            _test.True(profile != null, "锈锚应声明 weapon_profile。");
            if (profile != null)
            {
                _test.Eq(profile.weapon_type_id, new StringName("greataxe"), "锈锚 weapon_type_id 应为 greataxe。");
                _test.Eq(profile.family, new StringName("axe"), "锈锚 family 应为 axe。");
                _test.Eq(profile.range_type, new StringName("melee"), "锈锚应为 melee。");
                _test.Eq(profile.damage_tag, new StringName("physical_slash"), "锈锚应为斩击。");
                _test.Eq(profile.attack_range, 1, "锈锚攻击距离应为 1。");
                _test.Eq(profile.two_handed_dice?.dice_count ?? 0, 1, "锈锚应为 1D12+1。");
                _test.Eq(profile.two_handed_dice?.dice_sides ?? 0, 12, "锈锚应为 1D12+1。");
                _test.Eq(profile.two_handed_dice?.flat_bonus ?? 0, 1, "锈锚应为 1D12+1。");
                _test.True(ContainsStringName(profile.GetPropertiesTyped(), "two_handed"), "锈锚应声明 two_handed。");
                _test.True(ContainsStringName(profile.GetPropertiesTyped(), "heavy"), "锈锚应声明 heavy。");
            }
        }

        if (fixture.SkillDefs.TryGetValue(SunkAnchorSkillId, out SkillDefinition skill))
            AssertSunkAnchorSkillDefinition(skill, fixture);

        BattleUnitState baseline = fixture.BuildUnitWithoutWeapon("baseline");
        BattleUnitState equipped = fixture.BuildRustanchorUnit("projection");
        _test.Eq(equipped.weapon_item_id, ItemId, "锈锚装备后 unit 应保留 item_id。");
        _test.Eq(equipped.weapon_profile_type_id, new StringName("greataxe"), "锈锚应投影为 greataxe。");
        _test.Eq(equipped.weapon_family, new StringName("axe"), "锈锚应投影为 axe family。");
        _test.Eq(equipped.weapon_physical_damage_tag, new StringName("physical_slash"), "锈锚应为挥砍伤害。");
        _test.Eq(equipped.weapon_attack_range, 1, "锈锚投影攻击距离应为 1。");
        _test.True(equipped.weapon_uses_two_hands, "锈锚应占用双手。");
        _test.Eq(equipped.weapon_two_handed_dice?.dice_count ?? 0, 1, "锈锚应投影 1D12+1。");
        _test.Eq(equipped.weapon_two_handed_dice?.dice_sides ?? 0, 12, "锈锚应投影 1D12+1。");
        _test.Eq(equipped.weapon_two_handed_dice?.flat_bonus ?? 0, 1, "锈锚应投影 1D12+1。");
        AssertUnitHasTraitAndAbilitySource(equipped, SunkAnchorTraitId, SunkAnchorBindingId, "eq_rustanchor_projection");
        AssertUnitHasTraitAndAbilitySource(equipped, RustChainTraitId, RustChainBindingId, "eq_rustanchor_projection");
        AssertUnitHasTraitAndAbilitySource(equipped, NoReturnTraitId, NoReturnBindingId, "eq_rustanchor_projection");

        equipped.GetEquipmentView().ClearSlot("main_hand");
        fixture.Runtime._unit_factory.RefreshBattleUnit(equipped);
        _test.Eq(equipped.weapon_item_id, new StringName(""), "移除锈锚后 weapon_item_id 应清空。");
        _test.Eq(equipped.weapon_profile_type_id, baseline.weapon_profile_type_id, "移除锈锚后武器 profile 应恢复。");
        _test.Eq(equipped.equipment_ability_sources.Count, 0, "移除锈锚后装备能力源应清空。");
    }

    private void TestSunkAnchorSkillAppliesStatusBlocksForcedMoveAndReducesDamage()
    {
        using RustanchorFixture fixture = RustanchorFixture.Build(new GArray());
        BattleUnitState holder = fixture.BuildRustanchorUnit("sunk_anchor");
        holder.SetAnchorCoord(new Vector2I(1, 1));
        BattleUnitState enemy = BuildEnemy("rustanchor_sunk_anchor_enemy", new Vector2I(0, 1), hp: 80);
        PrimeSunkAnchorResources(holder);
        BattleState state = WeaponAbilityCommandTestSupport.BuildFlatState(
            "rustanchor_sunk_anchor",
            holder,
            enemy,
            mapSize: new Vector2I(4, 3)
        );
        fixture.Runtime.SetupStateForTests(state);

        BattleAvailableSkillEntry entry = FindRequiredEquipmentSkill(
            fixture,
            holder,
            SunkAnchorSkillId,
            state
        );
        _test.True(entry.IsSelectable, "体力充足且未冷却时沉锚守势应可选。");
        _test.Eq(entry.EntryRef.SourceKind, BattleSkillEntrySourceKind.EquipmentSkill, "沉锚守势来源应是 equipment_skill。");
        _test.Eq(entry.EquipmentBindingId, SunkAnchorBindingId, "沉锚守势入口应携带 binding id。");
        _test.Eq(entry.EquipmentGrantedActionId, SunkAnchorGrantId, "沉锚守势入口应携带 grant id。");

        int staminaBefore = holder.current_stamina;
        BattleCommand command = WeaponAbilityCommandTestSupport.BuildUnitSkillCommand(
            holder,
            holder,
            entry,
            SunkAnchorSkillId
        );
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.True(preview?.allowed == true, $"沉锚守势 self 技能 preview 应允许。logs={JoinLogs(preview?.LogLinesTyped)}");
        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        _test.True(batch != null, "沉锚守势 IssueCommand 应返回事件 batch。");
        _test.Eq(holder.current_ap, 1, "沉锚守势应消耗 1AP。");
        _test.Eq(holder.current_stamina, staminaBefore - 60, "沉锚守势应消耗 60 体力。");
        _test.Eq(holder.GetCooldownTyped(SunkAnchorSkillId), 180, "沉锚守势应设置 180TU 冷却。");

        BattleStatusEffectState status = holder.GetStatusEffect(SunkAnchorStatusId);
        _test.True(status != null, "沉锚守势施放后应给持有者写入沉锚状态。");
        if (status != null)
        {
            _test.Eq(status.duration, 120, "沉锚状态应持续 120TU。");
            _test.True(status.forced_move_immune, "沉锚状态应使用 typed forced_move_immune 字段。");
            _test.Eq(status.move_point_capacity_delta, -1, "沉锚状态应让移动点上限 -1，而不是归零。");
            _test.Eq(holder.GetMovePointCapacity(), 1, "普通 2 点移动力目标沉锚后上限应为 1。");
        }

        _test.Eq(
            SumDamageReduction(fixture, enemy, holder, state, "physical_slash"),
            3,
            "沉锚期间受到 physical_slash 伤害应固定减免 3。"
        );
        _test.Eq(
            SumDamageReduction(fixture, enemy, holder, state, "physical_blunt"),
            3,
            "沉锚期间受到 physical_blunt 伤害应固定减免 3。"
        );
        _test.Eq(
            SumDamageReduction(fixture, enemy, holder, state, "fire"),
            0,
            "沉锚不应减免非物理伤害。"
        );

        CombatEffectDefinition knockback = TestSkillDefinitionProjection.BuildEffect(
            "forced_move",
            forcedMoveMode: "knockback",
            forcedMoveDistance: 1
        );
        int blockedSteps = fixture.Runtime._special_skill_resolver.ApplyForcedMoveEffect(
            enemy,
            holder,
            knockback,
            new BattleEventBatch(),
            BattleForcedMoveContext.Empty
        );
        _test.Eq(blockedSteps, 0, "敌方强制位移应被沉锚状态拦截。");
        _test.Eq(holder.coord, new Vector2I(1, 1), "被沉锚拦截后坐标不应变化。");

        holder.EraseStatusEffect(SunkAnchorStatusId);
        holder.ClampCurrentMovePointsToCapacity();
        _test.Eq(
            SumDamageReduction(fixture, enemy, holder, state, "physical_slash"),
            0,
            "沉锚状态移除后物理减伤不应残留。"
        );
        int movedSteps = fixture.Runtime._special_skill_resolver.ApplyForcedMoveEffect(
            enemy,
            holder,
            knockback,
            new BattleEventBatch(),
            BattleForcedMoveContext.Empty
        );
        _test.Eq(movedSteps, 1, "没有沉锚状态时同一条合法强制位移应能推动 1 格。");
        _test.Eq(holder.coord, new Vector2I(2, 1), "移除沉锚后锈锚持有者应被推动到合法相邻格。");
    }

    private void TestRustChainAndNoReturnChopAfterHit()
    {
        using RustanchorFixture fixture = RustanchorFixture.Build(new GArray());
        BattleUnitState attacker = fixture.BuildRustanchorUnit("after_hit");
        BattleUnitState target = BuildEnemy("rustanchor_chain_target", new Vector2I(1, 0), hp: 100);

        BattleEquipmentAbilityAfterHitResult first = ResolveAfterHit(
            fixture,
            attacker,
            target,
            "rustanchor_first_hit",
            weaponHpDamage: 1,
            saveRollOverride: 1
        );
        BattleStatusEffectState chain = target.GetStatusEffect(RustChainStatusId);
        _test.True(chain != null, "锈链入肉真实武器 HP 伤害后 STR DC16 失败应施加锈链。");
        if (chain != null)
        {
            _test.Eq(chain.duration, 60, "锈链应持续 60TU。");
            _test.Eq(chain.move_point_capacity_delta, -1, "锈链应让移动点上限 -1。");
            _test.True(chain.counts_as_debuff, "锈链应被标记为负面状态。");
            _test.True(chain.dispellable_harmful_magic, "锈链应可按有害魔法驱散规则处理。");
            _test.Eq(target.GetMovePointCapacity(), 1, "普通 2 点移动力目标被锈链后上限应为 1。");
        }
        _test.False(first.HasBonusDamageDice(NoReturnBindingId, 1, 8), "刚被锈链命中的同一次 after-hit 不应提前触发不归斩。");

        BattleEquipmentAbilityAfterHitResult second = ResolveAfterHit(
            fixture,
            attacker,
            target,
            "rustanchor_second_hit",
            weaponHpDamage: 1,
            saveRollOverride: 20
        );
        _test.True(second.HasBonusDamageDice(NoReturnBindingId, 1, 8), "攻击已有锈链的目标应追加 1D8 钝击伤害。");

        BattleUnitState slowedTarget = BuildEnemy("rustanchor_slow_target", new Vector2I(1, 0), hp: 100);
        slowedTarget.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = "slow",
                display_label = "迟缓",
                stacks = 1,
                duration = 60,
            }
        );
        BattleEquipmentAbilityAfterHitResult slowedResult = ResolveAfterHit(
            fixture,
            attacker,
            slowedTarget,
            "rustanchor_slow_hit",
            weaponHpDamage: 1,
            saveRollOverride: 20
        );
        _test.True(slowedResult.HasBonusDamageDice(NoReturnBindingId, 1, 8), "攻击已有迟缓的目标也应触发不归斩。");

        BattleUnitState cleanTarget = BuildEnemy("rustanchor_no_damage_target", new Vector2I(1, 0), hp: 100);
        ResolveAfterHit(
            fixture,
            attacker,
            cleanTarget,
            "rustanchor_no_hp_damage",
            weaponHpDamage: 0,
            saveRollOverride: 1
        );
        _test.False(cleanTarget.HasStatusEffect(RustChainStatusId), "未造成真实武器 HP 伤害时不应施加锈链。");
    }

    private void AssertSunkAnchorSkillDefinition(
        SkillDefinition skill,
        RustanchorFixture fixture
    )
    {
        CombatSkillDefinition combat = skill.CombatProfile;
        _test.True(combat != null, "沉锚守势技能应有 combat_profile。");
        if (combat == null)
            return;
        _test.Eq(combat.TargetMode, new StringName("unit"), "沉锚守势应选择单位目标。");
        _test.Eq(combat.TargetTeamFilter, new StringName("self"), "沉锚守势只能选择自己。");
        _test.Eq(combat.RangeValue, 0, "沉锚守势不应有远程选择范围。");
        _test.Eq(combat.ApCost, 1, "沉锚守势应消耗 1AP。");
        _test.Eq(combat.StaminaCost, 60, "沉锚守势应消耗 60 体力。");
        _test.Eq(combat.CooldownTu, 180, "沉锚守势冷却应为 180TU。");
        _test.True(ContainsStringName(combat.RequiredWeaponFamilies, "axe"), "沉锚守势应要求 axe family。");
        _test.Eq(combat.EffectDefinitions.Count, 0, "沉锚守势的状态写入应由装备 after-skill reaction 承担。");

        _test.True(
            fixture.Bindings.TryGetValue(SunkAnchorBindingId, out EquipmentAbilityBindingDefinition binding),
            "沉锚守势 binding 应存在。"
        );
        if (binding == null)
            return;
        _test.Eq(binding.GrantedActions.Count, 1, "沉锚守势 binding 应授予一个装备技能入口。");
        EquipmentGrantedActionDefinition grant =
            binding.GrantedActions.Count > 0 ? binding.GrantedActions[0] : null;
        _test.Eq(grant?.SkillId ?? new StringName(""), SunkAnchorSkillId, "沉锚守势 grant 应指向真实 SkillDef。");
        _test.Eq(grant?.GrantedActionId ?? new StringName(""), SunkAnchorGrantId, "沉锚守势 grant id 应稳定。");
        _test.Eq(
            grant?.UsagePeriodKind ?? EquipmentAbilityUsagePeriodKind.PerBattle,
            EquipmentAbilityUsagePeriodKind.None,
            "沉锚守势使用节奏应由技能冷却承担。"
        );
        ApplyStatusActionPayloadDefinition payload = FindApplyStatusPayload(binding, SunkAnchorStatusId);
        _test.True(payload != null, "沉锚守势 after-skill reaction 应投影 apply_status payload。");
        if (payload != null)
        {
            _test.Eq(payload.TargetSelector, new StringName("source"), "沉锚状态应写入来源持有者。");
            _test.Eq(payload.DurationTu, 120, "沉锚状态 payload 应持续 120TU。");
            _test.Eq(payload.MovePointCapacityDelta, -1, "沉锚状态 payload 应让移动点上限 -1。");
            _test.True(payload.ForcedMoveImmune, "沉锚状态 payload 应投影通用 forced_move_immune 字段。");
        }
    }

    private static ApplyStatusActionPayloadDefinition FindApplyStatusPayload(
        EquipmentAbilityBindingDefinition binding,
        StringName statusId
    )
    {
        foreach (EquipmentAbilityReactionDefinition reaction in binding?.Reactions ?? Array.Empty<EquipmentAbilityReactionDefinition>())
        {
            foreach (EquipmentAbilityActionDefinition action in reaction?.Actions ?? Array.Empty<EquipmentAbilityActionDefinition>())
            {
                if (
                    action?.PayloadDefinition is ApplyStatusActionPayloadDefinition payload
                    && payload.StatusId == statusId
                )
                {
                    return payload;
                }
            }
        }
        return null;
    }

    private static BattleEquipmentAbilityAfterHitResult ResolveAfterHit(
        RustanchorFixture fixture,
        BattleUnitState attacker,
        BattleUnitState target,
        StringName battleId,
        int weaponHpDamage,
        int saveRollOverride
    )
    {
        BattleState state = WeaponAbilityCommandTestSupport.BuildFlatState(
            battleId,
            attacker,
            target,
            mapSize: new Vector2I(5, 2)
        );
        fixture.Runtime.SetupStateForTests(state);
        target.attribute_snapshot.SetValue(UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Strength), 10);
        target.attribute_snapshot.SetValue(AttributeSnapshot.ToStringName(AttributeSnapshotIdKind.StrengthModifier), 0);
        return fixture.Runtime.GetEquipmentAbilityRuntimeService().ResolveAfterHit(
            new BattleEquipmentAbilityAfterHitContext
            {
                SourceUnit = attacker,
                TargetUnit = target,
                BattleState = state,
                AttackSucceeded = true,
                WeaponHpDamage = weaponHpDamage,
                SaveContext = BattleSaveContext.WithSaveRollOverride(saveRollOverride),
            }
        );
    }

    private static int SumDamageReduction(
        RustanchorFixture fixture,
        BattleUnitState source,
        BattleUnitState target,
        BattleState state,
        StringName damageTag
    )
    {
        int total = 0;
        foreach (BattleEquipmentAbilityDamageReductionResult result in fixture.Runtime.GetEquipmentAbilityRuntimeService().CollectDamageReductions(
            new BattleEquipmentAbilityDamageReductionContext
            {
                SourceUnit = source,
                TargetUnit = target,
                BattleState = state,
                DamageTag = damageTag,
                AttackSucceeded = true,
            }
        ))
        {
            total += result?.Amount ?? 0;
        }
        return total;
    }

    private static BattleAvailableSkillEntry FindRequiredEquipmentSkill(
        RustanchorFixture fixture,
        BattleUnitState holder,
        StringName skillId,
        BattleState state
    )
    {
        BattleSkillAvailabilityView view = BuildEquipmentSkillAvailability(fixture, holder, state);
        if (!TryFindSkillEntry(view, skillId, out BattleAvailableSkillEntry entry))
            throw new InvalidOperationException($"missing equipment skill {skillId}.");
        return entry;
    }

    private static BattleSkillAvailabilityView BuildEquipmentSkillAvailability(
        RustanchorFixture fixture,
        BattleUnitState holder,
        BattleState state
    )
    {
        BattleSkillAvailabilityService service = new(fixture.SkillDefs, fixture.Bindings);
        return service.BuildView(
            new BattleSkillAvailabilityQuery
            {
                User = holder,
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

    private static BattleUnitState BuildEnemy(StringName unitId, Vector2I coord, int hp)
    {
        BattleUnitState unit = new()
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = "enemy",
            is_alive = true,
            current_hp = hp,
        };
        unit.SetAnchorCoord(coord);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 14);
        unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 0);
        unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 0);
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, hp);
        unit.attribute_snapshot.SetValue(UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Strength), 10);
        unit.attribute_snapshot.SetValue(AttributeSnapshot.ToStringName(AttributeSnapshotIdKind.StrengthModifier), 0);
        unit.SetEquipmentView(new EquipmentState());
        return unit;
    }

    private static void PrimeSunkAnchorResources(BattleUnitState unit)
    {
        if (unit == null)
            return;
        unit.SetCombatResources(80, 0, 100, 0, 2, 2);
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 80);
        unit.attribute_snapshot.SetValue(AttributeService.STAMINA_MAX, 100);
        unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 20);
        unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 20);
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

    private static bool ContainsStringName(IEnumerable<StringName> values, StringName expected)
    {
        foreach (StringName value in values ?? Array.Empty<StringName>())
            if (value == expected)
                return true;
        return false;
    }

    private static string JoinLogs(IEnumerable<string> values) =>
        values == null ? "" : string.Join(" | ", values);

    private sealed class RustanchorFixture : IDisposable
    {
        private readonly ItemContentRegistry _itemRegistry;
        private readonly ProgressionContentRegistry _progressionRegistry;
        private readonly PartyState _partyState;

        private RustanchorFixture(
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
        internal IReadOnlyDictionary<StringName, ItemDef> ItemDefs { get; }
        internal IReadOnlyDictionary<StringName, SkillDefinition> SkillDefs { get; }
        internal IReadOnlyDictionary<StringName, TraitDef> TraitDefs { get; }
        internal IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> Bindings { get; }

        internal static RustanchorFixture Build(GArray damageRolls)
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
            runtime.ConfigureDamageResolverForTests(new FixedRollDamageResolver(damageRolls));
            runtime.ConfigureHitResolverForTests(new FixedHitResolver(10));
            return new RustanchorFixture(itemRegistry, progressionRegistry, partyState, runtime);
        }

        internal BattleUnitState BuildUnitWithoutWeapon(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            return BuildSingleAllyUnit(label);
        }

        internal BattleUnitState BuildRustanchorUnit(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            member.equipment_state.SetEquippedEntry(
                "main_hand",
                ItemId,
                new GStringNameArray { "main_hand", "off_hand" },
                EquipmentInstanceState.CreateInstance(ItemId, $"eq_rustanchor_{label}")
            );
            BattleUnitState unit = BuildSingleAllyUnit(label);
            unit.SetAnchorCoord(Vector2I.Zero);
            unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 20);
            unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 20);
            unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 80);
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
                throw new InvalidOperationException($"{label} scenario should build exactly one ally unit.");
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
