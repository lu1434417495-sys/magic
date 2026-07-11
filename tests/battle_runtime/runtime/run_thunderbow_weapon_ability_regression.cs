using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_thunderbow_weapon_ability_regression : LifecycleTestSceneTree
{
    private static readonly StringName ThunderbowItemId =
        "weapon_unique_bow_thunderbow_156";
    private static readonly StringName ThunderArrowTraitId =
        "weapon.bow.thunderbow.thunder_arrow";
    private static readonly StringName StoredThunderShotTraitId =
        "weapon.bow.thunderbow.stored_thunder_shot";
    private static readonly StringName DeafenedResonanceTraitId =
        "weapon.bow.thunderbow.deafened_resonance";
    private static readonly StringName ThunderArrowBindingId =
        "binding.weapon.bow.thunderbow.thunder_arrow";
    private static readonly StringName StoredThunderShotBindingId =
        "binding.weapon.bow.thunderbow.stored_thunder_shot";
    private static readonly StringName StoredThunderShotSkillId =
        "weapon_bow_thunderbow_stored_thunder_shot";
    private static readonly StringName StoredThunderShotGrantId =
        "grant.thunderbow.stored_thunder_shot.skill";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestThunderbowContentProjectsWeaponTraitsAndEquipmentSkill();
            TestThunderArrowAddsThunderDamageOnRealWeaponHit();
            TestThunderArrowStunsOnFailedConSaveAndSkipsOnSuccess();
            TestStoredThunderShotExecutesWeaponAttackAndUsesLongCooldown();
            RequestTestExit(_test.Finish("Thunderbow weapon ability regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Thunderbow weapon ability regression"));
        }
    }

    private void TestThunderbowContentProjectsWeaponTraitsAndEquipmentSkill()
    {
        using ThunderbowFixture fixture = ThunderbowFixture.Build(new GArray());
        _test.True(fixture.ItemDefs.ContainsKey(ThunderbowItemId), "真实物品内容应包含雷鸣弓。");
        _test.True(fixture.TraitDefs.ContainsKey(ThunderArrowTraitId), "真实 trait 应包含雷鸣矢。");
        _test.True(fixture.TraitDefs.ContainsKey(StoredThunderShotTraitId), "真实 trait 应包含蓄雷矢。");
        _test.True(fixture.TraitDefs.ContainsKey(DeafenedResonanceTraitId), "真实 trait 应包含震鸣共振。");
        _test.True(fixture.Bindings.ContainsKey(ThunderArrowBindingId), "真实装备能力内容应包含雷鸣矢 binding。");
        _test.True(
            fixture.Bindings.ContainsKey(StoredThunderShotBindingId),
            "真实装备能力内容应包含蓄雷矢 binding。"
        );
        _test.True(
            fixture.SkillDefs.ContainsKey(StoredThunderShotSkillId),
            "蓄雷矢应落成真实 SkillDef，而不是 trait 文本。"
        );

        using ItemDef rawItem = ResourceLoader.Load<ItemDef>(
            "res://data/configs/items/weapon_unique_longbow_thunderbow.tres"
        );
        _test.True(rawItem != null, "雷鸣弓原始资源应能加载。");
        if (rawItem != null)
        {
            _test.Eq(rawItem.item_id, ThunderbowItemId, "雷鸣弓 item_id 应保留设计源编号。");
            _test.Eq(rawItem.display_name, "雷鸣弓", "雷鸣弓显示名应匹配设计源。");
            _test.Eq(
                rawItem.base_item_id,
                new StringName("weapon_type_longbow_base"),
                "雷鸣弓应继承 longbow 模板。"
            );
            _test.Eq(rawItem.base_price, 58000, "雷鸣弓基础价格应为 58000。");
            _test.Eq(rawItem.trait_ids.Count, 3, "雷鸣弓应只声明 3 个已落地特性。");
            _test.True(rawItem.trait_ids.Contains(ThunderArrowTraitId), "雷鸣弓应声明雷鸣矢。");
            _test.True(rawItem.trait_ids.Contains(StoredThunderShotTraitId), "雷鸣弓应声明蓄雷矢。");
            _test.True(rawItem.trait_ids.Contains(DeafenedResonanceTraitId), "雷鸣弓应声明震鸣共振。");

            WeaponProfileDef rawProfile = rawItem.weapon_profile as WeaponProfileDef;
            _test.True(rawProfile != null, "雷鸣弓应声明 weapon_profile 覆写。");
            if (rawProfile != null)
            {
                _test.False(
                    rawProfile.HasAttackRangeOverride(),
                    "雷鸣弓不应覆写攻击射程，应继承 longbow 基础射程 4。"
                );
                _test.Eq(rawProfile.two_handed_dice?.dice_count ?? 0, 1, "雷鸣弓双手伤害应为 1D8+2。");
                _test.Eq(rawProfile.two_handed_dice?.dice_sides ?? 0, 8, "雷鸣弓双手伤害应为 1D8+2。");
                _test.Eq(rawProfile.two_handed_dice?.flat_bonus ?? 0, 2, "雷鸣弓双手伤害应为 1D8+2。");
                _test.True(
                    ContainsStringName(rawProfile.GetPropertiesTyped(), "heavy"),
                    "雷鸣弓应声明 heavy 属性。"
                );
            }
        }

        if (fixture.SkillDefs.TryGetValue(StoredThunderShotSkillId, out SkillDefinition skill))
        {
            AssertStoredThunderShotSkillDefinition(skill, fixture);
        }

        BattleUnitState baseline = fixture.BuildUnitWithoutWeapon("baseline");
        _test.False(HasDamageMitigation(baseline, "thunder"), "未装备雷鸣弓时不应拥有 thunder immune。");

        BattleUnitState equipped = fixture.BuildThunderbowUnit("projection");
        _test.Eq(equipped.weapon_item_id, ThunderbowItemId, "雷鸣弓装备后 unit 应保留真实 item_id。");
        _test.Eq(equipped.weapon_profile_type_id, new StringName("longbow"), "雷鸣弓应投影为 longbow。");
        _test.Eq(equipped.weapon_family, new StringName("bow"), "雷鸣弓应投影为 bow family。");
        _test.Eq(equipped.weapon_attack_range, 4, "雷鸣弓应继承 longbow 基础射程 4。");
        _test.True(equipped.weapon_uses_two_hands, "雷鸣弓应占用双手。");
        _test.Eq(equipped.weapon_physical_damage_tag, new StringName("physical_pierce"), "雷鸣弓应造成穿刺物理伤害。");
        _test.Eq(equipped.weapon_two_handed_dice?.dice_count ?? 0, 1, "雷鸣弓应投影 1D8+2。");
        _test.Eq(equipped.weapon_two_handed_dice?.dice_sides ?? 0, 8, "雷鸣弓应投影 1D8+2。");
        _test.Eq(equipped.weapon_two_handed_dice?.flat_bonus ?? 0, 2, "雷鸣弓应投影 1D8+2。");
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            ThunderArrowTraitId,
            ThunderArrowBindingId,
            "eq_thunderbow_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            StoredThunderShotTraitId,
            StoredThunderShotBindingId,
            "eq_thunderbow_projection"
        );
        _test.True(equipped.effective_trait_ids.Contains(DeafenedResonanceTraitId), "震鸣共振应投影为固定装备 trait。");
        _test.Eq(
            GetDamageMitigation(equipped, "thunder"),
            new StringName("immune"),
            "震鸣共振应投影 thunder damage immune。"
        );

        BattleState state = WeaponAbilityCommandTestSupport.BuildFlatState(
            "thunderbow_skill_view",
            equipped,
            BuildEnemy("thunderbow_view_target", new Vector2I(4, 0), hp: 20)
        );
        fixture.Runtime.SetupStateForTests(state);
        BattleSkillAvailabilityView view = BuildEquipmentSkillAvailability(fixture, equipped, state);
        _test.True(
            TryFindSkillEntry(view, StoredThunderShotSkillId, out BattleAvailableSkillEntry entry),
            "装备雷鸣弓后，unit 的可用技能应包含蓄雷矢。"
        );
        if (entry != null)
        {
            _test.True(entry.IsSelectable, "资源充足且未冷却时蓄雷矢应可选。");
            _test.Eq(entry.EntryRef.SourceKind, BattleSkillEntrySourceKind.EquipmentSkill, "蓄雷矢来源应是 equipment_skill。");
            _test.Eq(entry.EquipmentBindingId, StoredThunderShotBindingId, "蓄雷矢入口应携带 binding id。");
            _test.Eq(entry.EquipmentGrantedActionId, StoredThunderShotGrantId, "蓄雷矢入口应携带 grant id。");
            _test.Eq(entry.EquipmentUsagePeriodKind, EquipmentAbilityUsagePeriodKind.None, "蓄雷矢不应配置每日次数限制。");
            _test.Eq(entry.EquipmentMaxUsesPerPeriod, 0, "蓄雷矢次数上限应交给技能冷却而不是装备 usage。");
        }

        equipped.GetEquipmentView().ClearSlot("main_hand");
        fixture.Runtime._unit_factory.RefreshBattleUnit(equipped);
        _test.Eq(equipped.weapon_item_id, new StringName(""), "移除雷鸣弓后 weapon_item_id 应清空。");
        _test.Eq(
            equipped.weapon_profile_type_id,
            baseline.weapon_profile_type_id,
            "移除雷鸣弓后武器 profile 应恢复。"
        );
        _test.Eq(equipped.equipment_ability_sources.Count, 0, "移除雷鸣弓后装备能力源应清空。");
        _test.False(HasDamageMitigation(equipped, "thunder"), "移除雷鸣弓后 thunder immune 不应残留。");
    }

    private void TestThunderArrowAddsThunderDamageOnRealWeaponHit()
    {
        using ThunderbowFixture fixture = ThunderbowFixture.Build(new GArray { 4, 3 });
        BattleUnitState attacker = fixture.BuildThunderbowUnit("thunder_damage");
        BattleUnitState target = BuildEnemy("thunder_damage_target", new Vector2I(1, 0), hp: 100);
        target.attribute_snapshot.SetValue(AttributeService.CONSTITUTION_MODIFIER, 100);

        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            attacker,
            target,
            "thunderbow_thunder_damage",
            previewCommand: false
        );
        int thunderDamage = 100 - target.current_hp;

        using ThunderbowFixture plainFixture = ThunderbowFixture.Build(new GArray { 4, 3 });
        BattleUnitState plainAttacker = plainFixture.BuildThunderbowUnit("plain_damage");
        plainAttacker.equipment_ability_sources.Clear();
        BattleUnitState plainTarget = BuildEnemy("plain_thunder_damage_target", new Vector2I(1, 0), hp: 100);
        plainTarget.attribute_snapshot.SetValue(AttributeService.CONSTITUTION_MODIFIER, 100);
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            plainFixture.Runtime,
            plainAttacker,
            plainTarget,
            "thunderbow_plain_damage",
            previewCommand: false
        );
        int plainDamage = 100 - plainTarget.current_hp;

        _test.Eq(plainDamage, 6, "固定骰 4 时，雷鸣弓基础武器伤害应为 1D8+2。");
        _test.Eq(
            thunderDamage,
            9,
            "雷鸣矢应在真实命中后额外造成固定骰 3 的 1D6 thunder，且不吞掉武器伤害。"
        );
    }

    private void TestThunderArrowStunsOnFailedConSaveAndSkipsOnSuccess()
    {
        using ThunderbowFixture fixture = ThunderbowFixture.Build(new GArray());
        BattleUnitState attacker = fixture.BuildThunderbowUnit("stun");

        BattleUnitState failedTarget = BuildEnemy("thunder_stun_failed", new Vector2I(1, 0), hp: 30);
        BattleState failedState = WeaponAbilityCommandTestSupport.BuildFlatState(
            "thunderbow_stun_failed",
            attacker,
            failedTarget
        );
        fixture.Runtime.SetupStateForTests(failedState);
        fixture.Runtime.GetEquipmentAbilityRuntimeService().ResolveAfterHit(
            new BattleEquipmentAbilityAfterHitContext
            {
                SourceUnit = attacker,
                TargetUnit = failedTarget,
                BattleState = failedState,
                AttackSucceeded = true,
                ApplyDamageDiceActions = false,
                SaveContext = BattleSaveContext.WithSaveRollOverride(1),
            }
        );

        BattleStatusEffectState stunned = failedTarget.GetStatusEffect("stunned");
        _test.True(stunned != null, "雷鸣矢应在 DC14 constitution 豁免失败后施加 stunned。");
        _test.Eq(stunned?.duration ?? -1, 60, "stunned 应持续 60 TU。");

        BattleUnitState successTarget = BuildEnemy("thunder_stun_success", new Vector2I(1, 0), hp: 30);
        BattleState successState = WeaponAbilityCommandTestSupport.BuildFlatState(
            "thunderbow_stun_success",
            attacker,
            successTarget
        );
        fixture.Runtime.SetupStateForTests(successState);
        fixture.Runtime.GetEquipmentAbilityRuntimeService().ResolveAfterHit(
            new BattleEquipmentAbilityAfterHitContext
            {
                SourceUnit = attacker,
                TargetUnit = successTarget,
                BattleState = successState,
                AttackSucceeded = true,
                ApplyDamageDiceActions = false,
                SaveContext = BattleSaveContext.WithSaveRollOverride(20),
            }
        );

        _test.False(successTarget.HasStatusEffect("stunned"), "DC14 constitution 豁免成功时雷鸣矢不应施加 stunned。");
    }

    private void TestStoredThunderShotExecutesWeaponAttackAndUsesLongCooldown()
    {
        using ThunderbowFixture fixture = ThunderbowFixture.Build(new GArray { 4, 2, 2, 2, 3 });
        BattleUnitState holder = fixture.BuildThunderbowUnit("stored_shot");
        holder.SetAnchorCoord(Vector2I.Zero);
        PrimeStoredThunderResources(holder);
        BattleUnitState target = BuildEnemy("stored_shot_target", new Vector2I(4, 0), hp: 100);
        target.attribute_snapshot.SetValue(AttributeService.CONSTITUTION_MODIFIER, 100);
        BattleState state = WeaponAbilityCommandTestSupport.BuildFlatState(
            "thunderbow_stored_shot",
            holder,
            target,
            mapSize: new Vector2I(6, 3)
        );
        fixture.Runtime.SetupStateForTests(state);
        BattleAvailableSkillEntry entry = FindRequiredEquipmentSkill(
            fixture,
            holder,
            StoredThunderShotSkillId,
            state
        );
        BattleCommand command = WeaponAbilityCommandTestSupport.BuildUnitSkillCommand(
            holder,
            target,
            entry,
            StoredThunderShotSkillId
        );
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.True(preview?.allowed == true, $"蓄雷矢 preview 应允许。logs={JoinLogs(preview)}");

        int hpBefore = target.current_hp;
        fixture.Runtime.IssueCommand(command);

        _test.Eq(
            hpBefore - target.current_hp,
            15,
            "蓄雷矢应结算武器 1D8+2、3D6 lightning，并触发雷鸣矢 1D6 thunder。"
        );
        _test.Eq(holder.current_stamina, 40, "蓄雷矢应消耗 60 体力。");
        _test.Eq(holder.GetCooldownTyped(StoredThunderShotSkillId), 300, "蓄雷矢应设置 300TU 冷却。");
    }

    private void AssertStoredThunderShotSkillDefinition(
        SkillDefinition skill,
        ThunderbowFixture fixture
    )
    {
        CombatSkillDefinition combat = skill.CombatProfile;
        _test.True(combat != null, "蓄雷矢技能应有 combat_profile。");
        if (combat == null)
            return;
        _test.Eq(combat.TargetMode, new StringName("unit"), "蓄雷矢应选择单位。");
        _test.Eq(combat.TargetTeamFilter, new StringName("enemy"), "蓄雷矢只能选择敌人。");
        _test.Eq(combat.RangeValue, 4, "蓄雷矢射程应跟随当前 longbow 基础射程 4。");
        _test.True(combat.RequiresLos, "蓄雷矢应要求 LOS。");
        _test.Eq(combat.ApCost, 1, "蓄雷矢应消耗 1AP。");
        _test.Eq(combat.StaminaCost, 60, "蓄雷矢应消耗 60 体力。");
        _test.Eq(combat.CooldownTu, 300, "蓄雷矢冷却必须是 300TU。");
        _test.Eq(
            combat.AttackResolutionModeKind,
            CombatSkillAttackResolutionMode.Auto,
            "蓄雷矢应走攻击检定，不能配置成 direct_effect。"
        );
        _test.True(ContainsStringName(combat.RequiredWeaponFamilies, "bow"), "蓄雷矢应要求 bow family。");
        _test.Eq(combat.EffectDefinitions.Count, 2, "蓄雷矢应包含武器伤害和 3D6 lightning 两个 effect。");

        bool hasWeaponDamage = false;
        bool hasLightningDamage = false;
        foreach (CombatEffectDefinition effect in combat.EffectDefinitions)
        {
            if (
                effect?.EffectKind == BattleEffectKind.Damage
                && effect.AddWeaponDice
                && effect.RequiresWeapon
                && effect.UseWeaponPhysicalDamageTag
                && effect.ResolveAsWeaponAttack
            )
            {
                hasWeaponDamage = true;
            }
            if (
                effect?.EffectKind == BattleEffectKind.Damage
                && effect.DamageTag == "lightning"
                && effect.DiceCount == 3
                && effect.DiceSides == 6
            )
            {
                hasLightningDamage = true;
            }
        }
        _test.True(hasWeaponDamage, "蓄雷矢应进行真实武器攻击。");
        _test.True(hasLightningDamage, "蓄雷矢命中应额外造成 3D6 lightning。");

        _test.True(
            fixture.Bindings.TryGetValue(StoredThunderShotBindingId, out EquipmentAbilityBindingDefinition binding),
            "蓄雷矢 binding 应存在。"
        );
        if (binding != null)
        {
            _test.Eq(binding.GrantedActions.Count, 1, "蓄雷矢 binding 应授予一个装备技能入口。");
            EquipmentGrantedActionDefinition grant =
                binding.GrantedActions.Count > 0 ? binding.GrantedActions[0] : null;
            _test.Eq(grant?.SkillId ?? new StringName(""), StoredThunderShotSkillId, "蓄雷矢 grant 应指向真实 SkillDef。");
            _test.Eq(grant?.GrantedActionId ?? new StringName(""), StoredThunderShotGrantId, "蓄雷矢 grant id 应稳定。");
            _test.Eq(
                grant?.UsagePeriodKind ?? EquipmentAbilityUsagePeriodKind.PerBattle,
                EquipmentAbilityUsagePeriodKind.None,
                "蓄雷矢使用节奏应由技能冷却承担。"
            );
            _test.Eq(grant?.MaxUsesPerPeriod ?? -1, 0, "蓄雷矢不应声明每周期次数上限。");
        }
    }

    private static BattleAvailableSkillEntry FindRequiredEquipmentSkill(
        ThunderbowFixture fixture,
        BattleUnitState holder,
        StringName skillId,
        BattleState state
    )
    {
        BattleSkillAvailabilityView view = BuildEquipmentSkillAvailability(fixture, holder, state);
        if (!TryFindSkillEntry(view, skillId, out BattleAvailableSkillEntry entry))
            throw new InvalidOperationException($"missing equipment skill {skillId}.");
        if (!entry.IsSelectable)
            throw new InvalidOperationException($"equipment skill {skillId} disabled: {entry.DisabledReason}.");
        return entry;
    }

    private static BattleSkillAvailabilityView BuildEquipmentSkillAvailability(
        ThunderbowFixture fixture,
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

    private static void PrimeStoredThunderResources(BattleUnitState unit)
    {
        if (unit == null)
            return;
        unit.SetCombatResources(80, 0, 100, 0, 2, 2);
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 80);
        unit.attribute_snapshot.SetValue(AttributeService.STAMINA_MAX, 100);
        unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 20);
        unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 20);
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
            weapon_range_type = "melee",
        };
        unit.SetAnchorCoord(coord);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 14);
        unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 0);
        unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 0);
        unit.attribute_snapshot.SetValue(AttributeService.CONSTITUTION_MODIFIER, 0);
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, hp);
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

    private static bool ContainsStringName(IEnumerable<StringName> values, StringName expected)
    {
        foreach (StringName value in values ?? Array.Empty<StringName>())
        {
            if (value == expected)
                return true;
        }
        return false;
    }

    private static bool HasDamageMitigation(BattleUnitState unit, StringName damageTag)
    {
        if (unit?.damage_resistances == null)
            return false;
        return unit.damage_resistances.ContainsKey(damageTag.ToString())
            || unit.damage_resistances.ContainsKey(damageTag);
    }

    private static StringName GetDamageMitigation(BattleUnitState unit, StringName damageTag)
    {
        if (unit?.damage_resistances == null)
            return "";
        if (unit.damage_resistances.TryGetValue(damageTag, out StringName value))
            return ProgressionDataUtils.to_string_name(value);
        if (unit.damage_resistances.TryGetValue(new StringName(damageTag.ToString()), out value))
            return ProgressionDataUtils.to_string_name(value);
        return "";
    }

    private static string JoinLogs(BattlePreview preview) =>
        string.Join(" | ", preview?.LogLinesTyped ?? Array.Empty<string>());

    private sealed class ThunderbowFixture : IDisposable
    {
        private readonly ItemContentRegistry _itemRegistry;
        private readonly ProgressionContentRegistry _progressionRegistry;
        private readonly PartyState _partyState;

        private ThunderbowFixture(
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
        internal IReadOnlyDictionary<StringName, TraitDefinition> TraitDefs { get; }
        internal IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> Bindings { get; }

        internal static ThunderbowFixture Build(GArray damageRolls)
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
            return new ThunderbowFixture(itemRegistry, progressionRegistry, partyState, runtime);
        }

        internal BattleUnitState BuildUnitWithoutWeapon(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            return BuildSingleAllyUnit(label);
        }

        internal BattleUnitState BuildThunderbowUnit(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            member.equipment_state.SetEquippedEntry(
                "main_hand",
                ThunderbowItemId,
                new GStringNameArray { "main_hand", "off_hand" },
                EquipmentInstanceState.CreateInstance(
                    ThunderbowItemId,
                    $"eq_thunderbow_{label}"
                )
            );
            BattleUnitState unit = BuildSingleAllyUnit(label);
            unit.SetAnchorCoord(Vector2I.Zero);
            unit.SetCombatResources(80, 0, 100, 0, 2, 2);
            unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 80);
            unit.attribute_snapshot.SetValue(AttributeService.STAMINA_MAX, 100);
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
