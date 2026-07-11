using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_lunareclipse_weapon_ability_regression : LifecycleTestSceneTree
{
    private static readonly StringName ItemId = "weapon_unique_battleaxe_lunareclipse";
    private static readonly StringName MoonPhaseCycleTraitId =
        "weapon.battleaxe.lunareclipse.moon_phase_cycle";
    private static readonly StringName FullMoonJudgmentTraitId =
        "weapon.battleaxe.lunareclipse.full_moon_judgment";
    private static readonly StringName EclipseShadowstepTraitId =
        "weapon.battleaxe.lunareclipse.eclipse_shadowstep";
    private static readonly StringName MoonPhaseCycleBindingId =
        "binding.weapon.battleaxe.lunareclipse.moon_phase_cycle";
    private static readonly StringName FullMoonJudgmentBindingId =
        "binding.weapon.battleaxe.lunareclipse.full_moon_judgment";
    private static readonly StringName EclipseShadowstepBindingId =
        "binding.weapon.battleaxe.lunareclipse.eclipse_shadowstep";
    private static readonly StringName EclipseShadowstepSkillId =
        "weapon_axe_lunareclipse_eclipse_shadowstep";
    private static readonly StringName EclipseShadowstepGrantId =
        "grant.lunareclipse.eclipse_shadowstep.skill";
    private static readonly StringName MoonPhaseStatusId = "lunareclipse_moon_phase";
    private static readonly StringName DodgeBonusStatusId = "dodge_bonus_up";
    private static readonly StringName HeavyArmorItemId = "test_lunareclipse_heavy_armor";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestLunareclipseProjectsContentAndSkillEntry();
            TestMoonPhaseCycleTriggersFullMoonJudgmentAndRefreshesOneStack();
            TestEclipseShadowstepBlinksThroughBlockedPathAndHeavyArmorBlocksEntry();
            RequestTestExit(_test.Finish("Lunareclipse weapon ability regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Lunareclipse weapon ability regression"));
        }
    }

    private void TestLunareclipseProjectsContentAndSkillEntry()
    {
        using LunareclipseFixture fixture = LunareclipseFixture.Build(new GArray());
        _test.True(fixture.ItemDefs.ContainsKey(ItemId), "真实物品内容应包含月蚀。");
        foreach (
            StringName traitId in new[]
            {
                MoonPhaseCycleTraitId,
                FullMoonJudgmentTraitId,
                EclipseShadowstepTraitId,
            }
        )
        {
            _test.True(fixture.TraitDefs.ContainsKey(traitId), $"月蚀应包含 trait {traitId}。");
        }
        foreach (
            StringName bindingId in new[]
            {
                MoonPhaseCycleBindingId,
                FullMoonJudgmentBindingId,
                EclipseShadowstepBindingId,
            }
        )
        {
            _test.True(fixture.Bindings.ContainsKey(bindingId), $"月蚀应包含 binding {bindingId}。");
        }
        _test.True(
            fixture.SkillDefs.ContainsKey(EclipseShadowstepSkillId),
            "月蚀影步应落成真实 SkillDef，而不是 trait 文本。"
        );

        using ItemDef rawItem = ResourceLoader.Load<ItemDef>(
            "res://data/configs/items/weapon_unique_battleaxe_lunareclipse.tres"
        );
        _test.True(rawItem != null, "月蚀原始资源应能加载。");
        if (rawItem != null)
        {
            _test.Eq(rawItem.item_id, ItemId, "月蚀 item_id 不应带源表数字。");
            _test.Eq(rawItem.display_name, "月蚀", "月蚀显示名应匹配设计。");
            _test.Eq(
                rawItem.base_item_id,
                new StringName("weapon_type_battleaxe_base"),
                "月蚀应继承 battleaxe 模板。"
            );
            _test.Eq(rawItem.base_price, 56000, "月蚀价格应为 56000。");
            _test.Eq(rawItem.trait_ids.Count, 3, "月蚀应有且只有 3 个新特性。");
            foreach (
                StringName traitId in new[]
                {
                    MoonPhaseCycleTraitId,
                    FullMoonJudgmentTraitId,
                    EclipseShadowstepTraitId,
                }
            )
            {
                _test.True(rawItem.trait_ids.Contains(traitId), $"月蚀 item 应声明 {traitId}。");
            }

            WeaponProfileDef profile = rawItem.weapon_profile as WeaponProfileDef;
            _test.True(profile != null, "月蚀应声明 weapon_profile。");
            if (profile != null)
            {
                _test.Eq(profile.one_handed_dice?.dice_count ?? 0, 1, "月蚀单手应为 1D8+2。");
                _test.Eq(profile.one_handed_dice?.dice_sides ?? 0, 8, "月蚀单手应为 1D8+2。");
                _test.Eq(profile.one_handed_dice?.flat_bonus ?? 0, 2, "月蚀单手应为 1D8+2。");
                _test.Eq(profile.two_handed_dice?.dice_count ?? 0, 1, "月蚀双手应为 1D10+2。");
                _test.Eq(profile.two_handed_dice?.dice_sides ?? 0, 10, "月蚀双手应为 1D10+2。");
                _test.Eq(profile.two_handed_dice?.flat_bonus ?? 0, 2, "月蚀双手应为 1D10+2。");
                _test.True(
                    ContainsStringName(profile.GetPropertiesTyped(), "versatile"),
                    "月蚀应保留 versatile。"
                );
            }
        }

        if (fixture.SkillDefs.TryGetValue(EclipseShadowstepSkillId, out SkillDefinition skill))
        {
            AssertEclipseShadowstepSkillDefinition(skill, fixture);
        }

        BattleUnitState baseline = fixture.BuildUnitWithoutWeapon("baseline");
        BattleUnitState equipped = fixture.BuildLunareclipseUnit("projection", equipHeavyArmor: false);
        _test.Eq(equipped.weapon_item_id, ItemId, "月蚀装备后 unit 应保留真实 item_id。");
        _test.Eq(equipped.weapon_profile_type_id, new StringName("battleaxe"), "月蚀应投影为 battleaxe。");
        _test.Eq(equipped.weapon_family, new StringName("axe"), "月蚀应投影为 axe family。");
        _test.Eq(equipped.weapon_physical_damage_tag, new StringName("physical_slash"), "月蚀应为斩击伤害。");
        _test.Eq(equipped.weapon_attack_range, 1, "月蚀攻击距离应为 1。");
        _test.True(equipped.weapon_is_versatile, "月蚀应保留 versatile 投影。");
        _test.Eq(equipped.weapon_one_handed_dice?.dice_count ?? 0, 1, "月蚀单手应投影 1D8+2。");
        _test.Eq(equipped.weapon_one_handed_dice?.dice_sides ?? 0, 8, "月蚀单手应投影 1D8+2。");
        _test.Eq(equipped.weapon_one_handed_dice?.flat_bonus ?? 0, 2, "月蚀单手应投影 1D8+2。");
        _test.Eq(equipped.weapon_two_handed_dice?.dice_sides ?? 0, 10, "月蚀双手应投影 1D10+2。");
        _test.Eq(equipped.weapon_two_handed_dice?.flat_bonus ?? 0, 2, "月蚀双手应投影 1D10+2。");
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            MoonPhaseCycleTraitId,
            MoonPhaseCycleBindingId,
            "eq_lunareclipse_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            FullMoonJudgmentTraitId,
            FullMoonJudgmentBindingId,
            "eq_lunareclipse_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            EclipseShadowstepTraitId,
            EclipseShadowstepBindingId,
            "eq_lunareclipse_projection"
        );

        equipped.GetEquipmentView().ClearSlot("main_hand");
        fixture.Runtime._unit_factory.RefreshBattleUnit(equipped);
        _test.Eq(equipped.weapon_item_id, new StringName(""), "移除月蚀后 weapon_item_id 应清空。");
        _test.Eq(
            equipped.weapon_profile_type_id,
            baseline.weapon_profile_type_id,
            "移除月蚀后武器 profile 应回到装备前状态。"
        );
        _test.Eq(equipped.equipment_ability_sources.Count, 0, "移除月蚀后装备能力源应清空。");
    }

    private void TestMoonPhaseCycleTriggersFullMoonJudgmentAndRefreshesOneStack()
    {
        using LunareclipseFixture fixture = LunareclipseFixture.Build(
            new GArray { 4, 4, 4, 4, 3, 5 }
        );
        BattleUnitState attacker = fixture.BuildLunareclipseUnit("moon_phase", equipHeavyArmor: false);
        BattleUnitState target = BuildEnemy("lunareclipse_moon_target", new Vector2I(1, 0), hp: 100);

        for (int hit = 1; hit <= 3; hit++)
        {
            WeaponAbilityCommandTestSupport.IssueBasicAttack(
                fixture.Runtime,
                attacker,
                target,
                $"lunareclipse_moon_phase_hit_{hit}",
                previewCommand: false
            );
            BattleStatusEffectState moonPhase = attacker.GetStatusEffect(MoonPhaseStatusId);
            _test.True(moonPhase != null, $"第 {hit} 次真实武器 HP 伤害后应获得月相。");
            if (moonPhase != null)
            {
                _test.Eq(moonPhase.stacks, hit, $"第 {hit} 次命中后月相层数应为 {hit}。");
                _test.Eq(moonPhase.duration, 180, "月相应持续 180TU。");
                _test.Eq(moonPhase.source_unit_id, attacker.unit_id, "月相应记录持有者来源。");
            }
        }
        _test.Eq(target.current_hp, 82, "前三次命中应各造成 1D8+2，不应提前触发盈月裁断。");

        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            attacker,
            target,
            "lunareclipse_full_moon_judgment_hit",
            previewCommand: false
        );
        _test.Eq(
            target.current_hp,
            68,
            "第 4 次命中应造成武器 1D8+2 与盈月裁断 2D8 光耀伤害。"
        );
        BattleStatusEffectState refreshed = attacker.GetStatusEffect(MoonPhaseStatusId);
        _test.True(refreshed != null, "盈月裁断消耗后，同一次真实伤害应重新获得月相。");
        if (refreshed != null)
        {
            _test.Eq(refreshed.stacks, 1, "盈月裁断应先消耗 3 层，再由月相轮转刷新为 1 层。");
            _test.Eq(refreshed.duration, 180, "刷新后的月相仍应持续 180TU。");
        }
    }

    private void TestEclipseShadowstepBlinksThroughBlockedPathAndHeavyArmorBlocksEntry()
    {
        using LunareclipseFixture fixture = LunareclipseFixture.Build(new GArray());
        BattleUnitState holder = fixture.BuildLunareclipseUnit("shadowstep", equipHeavyArmor: false);
        holder.SetCombatResources(80, 0, 100, 0, 2, 2);
        holder.attribute_snapshot.SetValue(AttributeService.STAMINA_MAX, 100);
        holder.SetAnchorCoord(Vector2I.Zero);
        BattleUnitState pathBlocker = BuildEnemy("lunareclipse_path_blocker", new Vector2I(1, 0), hp: 40);
        BattleState state = WeaponAbilityCommandTestSupport.BuildFlatState(
            "lunareclipse_shadowstep",
            holder,
            pathBlocker,
            mapSize: new Vector2I(5, 3)
        );
        fixture.Runtime.SetupStateForTests(state);

        BattleAvailableSkillEntry entry = FindRequiredEquipmentSkill(
            fixture,
            holder,
            EclipseShadowstepSkillId,
            state
        );
        _test.True(entry.IsSelectable, "未穿重甲且资源充足时月蚀影步应可选。");
        _test.Eq(entry.EntryRef.SourceKind, BattleSkillEntrySourceKind.EquipmentSkill, "月蚀影步来源应是 equipment_skill。");
        _test.Eq(entry.EquipmentBindingId, EclipseShadowstepBindingId, "月蚀影步入口应携带 binding id。");
        _test.Eq(entry.EquipmentGrantedActionId, EclipseShadowstepGrantId, "月蚀影步入口应携带 grant id。");

        BattleCommand command = BuildGroundSkillCommand(holder, entry, new Vector2I(3, 0));
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.True(
            preview?.allowed == true,
            $"月蚀影步应允许 blink 穿过中间被占用格，只验证落点。logs={JoinLogs(preview?.LogLinesTyped)}"
        );
        int staminaBefore = holder.current_stamina;
        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        _test.True(batch != null, "月蚀影步 IssueCommand 应返回事件 batch。");
        _test.Eq(holder.coord, new Vector2I(3, 0), "月蚀影步应将持有者闪现到目标地格。");
        _test.Eq(holder.current_ap, 1, "月蚀影步应消耗 1AP。");
        _test.Eq(holder.current_stamina, staminaBefore - 45, "月蚀影步应消耗 45 体力。");
        _test.Eq(holder.GetCooldownTyped(EclipseShadowstepSkillId), 120, "月蚀影步应设置 120TU 冷却。");
        BattleStatusEffectState dodge = holder.GetStatusEffect(DodgeBonusStatusId);
        _test.True(dodge != null, "月蚀影步后应获得 dodge_bonus_up。");
        if (dodge != null)
        {
            _test.Eq(dodge.power, 2, "月蚀影步应通过 power=2 表达闪避 AC +4。");
            _test.Eq(dodge.duration, 60, "月蚀影步闪避提升应持续 60TU。");
        }

        BattleUnitState heavyHolder = fixture.BuildLunareclipseUnit(
            "shadowstep_heavy",
            equipHeavyArmor: true
        );
        heavyHolder.SetCombatResources(80, 0, 100, 0, 2, 2);
        BattleState heavyState = WeaponAbilityCommandTestSupport.BuildFlatState(
            "lunareclipse_shadowstep_heavy",
            heavyHolder,
            BuildEnemy("lunareclipse_heavy_dummy", new Vector2I(2, 0), hp: 40),
            mapSize: new Vector2I(5, 3)
        );
        fixture.Runtime.SetupStateForTests(heavyState);
        BattleAvailableSkillEntry heavyEntry = FindRequiredEquipmentSkill(
            fixture,
            heavyHolder,
            EclipseShadowstepSkillId,
            heavyState
        );
        _test.False(heavyEntry.IsSelectable, "穿重甲时月蚀影步入口不应生效。");
        _test.Eq(
            heavyEntry.DisabledReason,
            new StringName("equipment_skill_availability_blocked"),
            "重甲阻止月蚀影步应走通用可用性 blocked reason。"
        );
    }

    private void AssertEclipseShadowstepSkillDefinition(
        SkillDefinition skill,
        LunareclipseFixture fixture
    )
    {
        CombatSkillDefinition combat = skill.CombatProfile;
        _test.True(combat != null, "月蚀影步技能应有 combat_profile。");
        if (combat == null)
            return;
        _test.Eq(combat.TargetMode, new StringName("ground"), "月蚀影步应选择地面格。");
        _test.Eq(combat.RangeValue, 3, "月蚀影步应选择 3 格内地面格。");
        _test.Eq(combat.ApCost, 1, "月蚀影步应消耗 1AP。");
        _test.Eq(combat.StaminaCost, 45, "月蚀影步体力消耗应为 45。");
        _test.Eq(combat.CooldownTu, 120, "月蚀影步冷却应为 120TU。");
        _test.True(ContainsStringName(combat.RequiredWeaponFamilies, "axe"), "月蚀影步应要求 axe family。");
        _test.True(HasForcedMoveBlink(combat, 3), "月蚀影步应通过 forced_move blink 位移 3 格。");
        _test.True(HasSelfDodgeStatus(combat, 2, 60), "月蚀影步应给自己 dodge_bonus_up power=2 持续 60TU。");

        _test.True(
            fixture.Bindings.TryGetValue(
                EclipseShadowstepBindingId,
                out EquipmentAbilityBindingDefinition binding
            ),
            "月蚀影步 binding 应存在。"
        );
        if (binding == null)
            return;
        _test.Eq(binding.GrantedActions.Count, 1, "月蚀影步 binding 应授予一个装备技能入口。");
        EquipmentGrantedActionDefinition grant =
            binding.GrantedActions.Count > 0 ? binding.GrantedActions[0] : null;
        _test.Eq(grant?.SkillId ?? new StringName(""), EclipseShadowstepSkillId, "月蚀影步 grant 应指向真实 SkillDef。");
        _test.Eq(grant?.GrantedActionId ?? new StringName(""), EclipseShadowstepGrantId, "月蚀影步 grant id 应稳定。");
        _test.Eq(
            grant?.UsagePeriodKind ?? EquipmentAbilityUsagePeriodKind.PerBattle,
            EquipmentAbilityUsagePeriodKind.None,
            "月蚀影步使用节奏应由技能冷却承担。"
        );
        _test.True(grant?.AvailabilityConditions != null, "月蚀影步 grant 应声明重甲可用性条件。");
    }

    private static BattleCommand BuildGroundSkillCommand(
        BattleUnitState unit,
        BattleAvailableSkillEntry entry,
        Vector2I coord
    ) =>
        new()
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = unit?.unit_id ?? new StringName(""),
            skill_entry_id = entry?.EntryRef.SkillEntryId ?? new StringName(""),
            skill_id = entry?.EntryRef.SkillId ?? new StringName(""),
            target_coord = coord,
        };

    private static BattleAvailableSkillEntry FindRequiredEquipmentSkill(
        LunareclipseFixture fixture,
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
        LunareclipseFixture fixture,
        BattleUnitState holder,
        BattleState state
    )
    {
        BattleSkillAvailabilityService service = new(
            fixture.SkillDefs,
            fixture.Bindings,
            fixture.ItemDefs
        );
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
        foreach (
            BattleAvailableSkillEntry entry in view?.SkillEntries
                ?? Array.Empty<BattleAvailableSkillEntry>()
        )
        {
            if (entry?.EntryRef.SkillId == skillId)
            {
                result = entry;
                return true;
            }
        }
        return false;
    }

    private static bool HasForcedMoveBlink(CombatSkillDefinition combat, int distance)
    {
        foreach (
            CombatEffectDefinition effect in combat?.EffectDefinitions
                ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (
                effect?.EffectType == "forced_move"
                && effect.ForcedMoveMode == "blink"
                && effect.ForcedMoveDistance == distance
            )
            {
                return true;
            }
        }
        return false;
    }

    private static bool HasSelfDodgeStatus(
        CombatSkillDefinition combat,
        int power,
        int durationTu
    )
    {
        foreach (
            CombatEffectDefinition effect in combat?.EffectDefinitions
                ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (
                effect?.EffectType == "status"
                && effect.EffectTargetTeamFilter == "self"
                && effect.StatusId == DodgeBonusStatusId
                && effect.Power == power
                && effect.DurationTu == durationTu
            )
            {
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
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 10);
        unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 0);
        unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 0);
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, hp);
        unit.SetEquipmentView(new EquipmentState());
        unit.creature_type_tags.Add("humanoid");
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
        foreach (
            BattleEquipmentAbilitySourceState source in unit?.equipment_ability_sources
                ?? new List<BattleEquipmentAbilitySourceState>()
        )
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

    private sealed class LunareclipseFixture : IDisposable
    {
        private readonly ItemContentRegistry _itemRegistry;
        private readonly ProgressionContentRegistry _progressionRegistry;
        private readonly PartyState _partyState;
        private readonly Dictionary<StringName, ItemDefinition> _itemDefs;

        private LunareclipseFixture(
            ItemContentRegistry itemRegistry,
            ProgressionContentRegistry progressionRegistry,
            PartyState partyState,
            BattleRuntimeModule runtime,
            Dictionary<StringName, ItemDefinition> itemDefs
        )
        {
            _itemRegistry = itemRegistry;
            _progressionRegistry = progressionRegistry;
            _partyState = partyState;
            _itemDefs = itemDefs;
            Runtime = runtime;
            ItemDefs = _itemDefs;
            SkillDefs = progressionRegistry.GetSkillDefinitionsTyped();
            TraitDefs = progressionRegistry.GetTraitDefsTyped();
            Bindings = progressionRegistry.GetEquipmentAbilityBindingDefinitionsTyped();
        }

        internal BattleRuntimeModule Runtime { get; }
        internal IReadOnlyDictionary<StringName, ItemDefinition> ItemDefs { get; }
        internal IReadOnlyDictionary<StringName, SkillDefinition> SkillDefs { get; }
        internal IReadOnlyDictionary<StringName, TraitDefinition> TraitDefs { get; }
        internal IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> Bindings { get; }

        internal static LunareclipseFixture Build(GArray damageRolls)
        {
            ItemContentRegistry itemRegistry = new();
            ProgressionContentRegistry progressionRegistry = new();
            Dictionary<StringName, ItemDefinition> itemDefs = new(itemRegistry.GetItemDefsTyped());
            itemDefs[HeavyArmorItemId] = BuildHeavyArmorItem();
            PartyState partyState = BuildPartyState("hero");
            CharacterManagementModule characterManagement = new();
            characterManagement.setup(
                partyState,
                progressionRegistry.GetSkillDefinitionsTyped(),
                progressionRegistry.GetProfessionDefsTyped(),
                progressionRegistry.GetAchievementDefsTyped(),
                itemDefs,
                progressionRegistry.GetQuestDefsTyped(),
                progressionRegistry.GetTraitDefsTyped(),
                null,
                new ProgressionIdentityCatalogData()
            );

            BattleRuntimeModule runtime = new();
            runtime.setup(
                characterManagement,
                progressionRegistry.GetSkillDefinitionsTyped(),
                item_defs: itemDefs,
                trait_defs: progressionRegistry.GetTraitDefsTyped(),
                equipment_ability_bindings: progressionRegistry.GetEquipmentAbilityBindingDefinitionsTyped()
            );
            runtime.ConfigureDamageResolverForTests(new FixedRollDamageResolver(damageRolls));
            runtime.ConfigureHitResolverForTests(new FixedHitResolver(10));
            return new LunareclipseFixture(
                itemRegistry,
                progressionRegistry,
                partyState,
                runtime,
                itemDefs
            );
        }

        internal BattleUnitState BuildUnitWithoutWeapon(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            return BuildSingleAllyUnit(label);
        }

        internal BattleUnitState BuildLunareclipseUnit(string label, bool equipHeavyArmor)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            member.equipment_state.SetEquippedEntry(
                "main_hand",
                ItemId,
                new GStringNameArray { "main_hand" },
                EquipmentInstanceState.CreateInstance(ItemId, $"eq_lunareclipse_{label}")
            );
            if (equipHeavyArmor)
            {
                member.equipment_state.SetEquippedEntry(
                    "body",
                    HeavyArmorItemId,
                    new GStringNameArray { "body" },
                    EquipmentInstanceState.CreateInstance(HeavyArmorItemId, $"eq_heavy_{label}")
                );
            }
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
            _itemDefs?.Clear();
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

        private static ItemDefinition BuildHeavyArmorItem()
        {
            ItemDef rawItem = TestResourceOwnership.Own(
                new ItemDef
                {
                    item_id = HeavyArmorItemId,
                    display_name = "测试重甲",
                    is_stackable = false,
                    max_stack = 1,
                    item_category = "equipment",
                    equipment_type_id = "armor",
                    equipment_slot_ids = new Godot.Collections.Array<string> { "body" },
                    tags = new Godot.Collections.Array<StringName>
                    {
                        "armor",
                        "body",
                        "metal",
                        "heavy_armor",
                    },
                    max_dex_bonus = 0,
                },
                "LunareclipseFixture.BuildHeavyArmorItem"
            );
            return rawItem.ToDefinition();
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
