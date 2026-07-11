using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_tremor_weapon_ability_regression : LifecycleTestSceneTree
{
    private static readonly StringName TremorItemId = "weapon_unique_hammer_tremor_102";
    private static readonly StringName ShockwaveTraitId = "weapon.hammer.tremor.shockwave";
    private static readonly StringName GeologicResonanceTraitId =
        "weapon.hammer.tremor.geologic_resonance";
    private static readonly StringName StoneOathTraitId = "weapon.hammer.tremor.stone_oath";
    private static readonly StringName ShockwaveBindingId =
        "binding.weapon.hammer.tremor.shockwave";
    private static readonly StringName ShockwaveSkillId = "weapon_hammer_tremor_shockwave";
    private static readonly StringName ShockwaveGrantId = "grant.tremor.shockwave.skill";
    private static readonly StringName ProneStatusId = "prone";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestTremorProjectsRealContentAndClearsOnUnequip();
            TestShockwaveIsProjectedAsEquipmentGrantedSkill();
            TestShockwaveUsesSingleDamageSaveForHalfDamageAndProne();
            RequestTestExit(_test.Finish("Tremor weapon ability regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Tremor weapon ability regression"));
        }
    }

    private void TestTremorProjectsRealContentAndClearsOnUnequip()
    {
        using TremorFixture fixture = TremorFixture.Build(new GArray());
        _test.True(fixture.ItemDefs.ContainsKey(TremorItemId), "真实物品内容应包含地动。");
        _test.True(fixture.TraitDefs.ContainsKey(ShockwaveTraitId), "真实 trait 内容应包含震击。");
        _test.True(
            fixture.TraitDefs.ContainsKey(GeologicResonanceTraitId),
            "真实 trait 内容应包含地脉共鸣展示 trait。"
        );
        _test.True(
            fixture.TraitDefs.ContainsKey(StoneOathTraitId),
            "真实 trait 内容应包含磐石之誓展示 trait。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(ShockwaveBindingId),
            "真实装备能力内容应包含震击 binding。"
        );
        _test.True(
            fixture.SkillDefs.ContainsKey(ShockwaveSkillId),
            "真实技能内容应包含震击装备技能。"
        );
        if (!fixture.ItemDefs.ContainsKey(TremorItemId))
            return;

        ItemDef rawTremor = ResourceLoader.Load<ItemDef>(
            "res://data/configs/items/weapon_unique_maul_tremor.tres"
        );
        _test.True(rawTremor != null, "地动原始资源应能加载。");
        if (rawTremor != null)
        {
            _test.Eq(
                rawTremor.base_item_id,
                new StringName("weapon_type_maul_base"),
                "地动应继承 maul 模板。"
            );
            _test.Eq(rawTremor.trait_ids.Count, 3, "地动应固定声明三个 weapon trait。");
            _test.True(rawTremor.trait_ids.Contains(ShockwaveTraitId), "地动应声明震击 trait。");
            _test.True(
                rawTremor.trait_ids.Contains(GeologicResonanceTraitId),
                "地动应声明地脉共鸣 trait。"
            );
            _test.True(rawTremor.trait_ids.Contains(StoneOathTraitId), "地动应声明磐石之誓 trait。");
        }

        BattleUnitState baseline = fixture.BuildUnitWithoutWeapon("baseline");
        BattleUnitState equipped = fixture.BuildTremorUnit("projection");
        _test.Eq(equipped.weapon_item_id, TremorItemId, "地动装备后 unit 应保留真实 item_id。");
        _test.Eq(equipped.weapon_profile_type_id, new StringName("maul"), "地动应投影为 maul。");
        _test.Eq(equipped.weapon_family, new StringName("hammer"), "地动应保留 hammer 家族。");
        _test.True(equipped.weapon_uses_two_hands, "地动应占用双手。");
        _test.Eq(equipped.weapon_two_handed_dice?.dice_count ?? 0, 2, "地动应为 2D6+2。");
        _test.Eq(equipped.weapon_two_handed_dice?.dice_sides ?? 0, 6, "地动应为 2D6+2。");
        _test.Eq(equipped.weapon_two_handed_dice?.flat_bonus ?? 0, 2, "地动应为 2D6+2。");
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            ShockwaveTraitId,
            ShockwaveBindingId,
            "eq_tremor_projection"
        );
        _test.True(
            equipped.effective_trait_ids.Contains(GeologicResonanceTraitId),
            "地脉共鸣应作为展示 trait 投影到战斗单位。"
        );
        _test.True(
            equipped.effective_trait_ids.Contains(StoneOathTraitId),
            "磐石之誓应作为展示 trait 投影到战斗单位。"
        );

        equipped.GetEquipmentView().ClearSlot("main_hand");
        fixture.Runtime._unit_factory.RefreshBattleUnit(equipped);
        _test.Eq(equipped.weapon_item_id, new StringName(""), "移除地动后 weapon_item_id 应清空。");
        _test.Eq(
            equipped.weapon_profile_type_id,
            baseline.weapon_profile_type_id,
            "移除地动后 weapon_profile_type_id 应回到装备前状态。"
        );
        _test.Eq(equipped.equipment_ability_sources.Count, 0, "移除地动后装备能力源应清空。");
        _test.Eq(
            equipped.effective_trait_instances.Count,
            baseline.effective_trait_instances.Count,
            "移除地动后装备 trait 实例应回到装备前状态。"
        );
    }

    private void TestShockwaveIsProjectedAsEquipmentGrantedSkill()
    {
        using TremorFixture fixture = TremorFixture.Build(new GArray());
        _test.True(
            fixture.SkillDefs.TryGetValue(ShockwaveSkillId, out SkillDefinition shockwave),
            "震击应是 SkillDef，而不是 trait 自己承担主动动作。"
        );
        if (shockwave == null)
            return;

        CombatSkillDefinition combat = shockwave.CombatProfile;
        _test.True(combat != null, "震击技能应有 combat_profile。");
        if (combat == null)
            return;
        _test.Eq(combat.TargetMode, new StringName("ground"), "震击应攻击地面。");
        _test.Eq(combat.TargetTeamFilter, new StringName("any"), "震击目标过滤应允许所有生物。");
        _test.Eq(combat.RangeValue, 1, "震击应只能打击近身地面。");
        _test.Eq(combat.AreaPattern, new StringName("radius"), "震击应使用半径范围。");
        _test.Eq(combat.AreaValue, 2, "10尺半径应落成当前系统半径 2 格。");
        _test.Eq(combat.ApCost, 1, "震击应消耗 1 AP。");
        _test.Eq(combat.EffectDefinitions.Count, 1, "震击应由单个 damage effect 表达一次豁免。");

        CombatEffectDefinition damage = combat.EffectDefinitions[0];
        _test.Eq(damage.EffectType, new StringName("damage"), "震击 effect 应是 damage。");
        _test.Eq(damage.DamageTag, new StringName("thunder"), "震击伤害标签应是 thunder。");
        _test.Eq(damage.DiceCount, 2, "震击伤害应是 2D6。");
        _test.Eq(damage.DiceSides, 6, "震击伤害应是 2D6。");
        _test.Eq(damage.SaveDc, 14, "震击豁免 DC 应是 14。");
        _test.Eq(damage.SaveAbility, new StringName("constitution"), "震击应使用 constitution 豁免。");
        _test.True(damage.SavePartialOnSuccess, "震击豁免成功应半伤。");
        _test.Eq(damage.SaveFailureStatusId, ProneStatusId, "震击同一次豁免失败应附带 prone。");
        _test.Eq(damage.DurationTu, 50, "震击 prone 应持续 50TU。");

        _test.True(
            fixture.Bindings.TryGetValue(ShockwaveBindingId, out EquipmentAbilityBindingDefinition binding),
            "震击 binding 应存在。"
        );
        if (binding != null)
        {
            _test.Eq(binding.GrantedActions.Count, 1, "震击 binding 应授予一个装备技能入口。");
            if (binding.GrantedActions.Count > 0)
            {
                EquipmentGrantedActionDefinition grant = binding.GrantedActions[0];
                _test.Eq(grant.SkillId, ShockwaveSkillId, "震击 grant 应指向真实 SkillDef。");
                _test.Eq(grant.SkillLevel, 1, "震击 grant 等级应为 1。");
            }
        }

        BattleUnitState equipped = fixture.BuildTremorUnit("shockwave_skill");
        BattleSkillAvailabilityView view = BuildEquipmentSkillView(fixture, equipped);
        _test.True(
            TryFindSkillEntry(view, ShockwaveSkillId, out BattleAvailableSkillEntry entry),
            "装备地动后，unit 的可用技能应包含装备授予的震击。"
        );
        if (entry != null)
        {
            _test.Eq(
                entry.EntryRef.SourceKind,
                BattleSkillEntrySourceKind.EquipmentSkill,
                "震击技能入口来源应是 equipment_skill。"
            );
            _test.Eq(entry.EquipmentBindingId, ShockwaveBindingId, "震击技能入口应携带 binding id。");
            _test.Eq(
                entry.EquipmentGrantedActionId,
                ShockwaveGrantId,
                "震击技能入口应携带 grant id。"
            );
            _test.True(entry.IsSelectable, "装备地动后震击应可选。");
        }

        equipped.GetEquipmentView().ClearSlot("main_hand");
        fixture.Runtime._unit_factory.RefreshBattleUnit(equipped);
        BattleSkillAvailabilityView unequippedView = BuildEquipmentSkillView(fixture, equipped);
        _test.False(
            TryFindSkillEntry(unequippedView, ShockwaveSkillId, out _),
            "卸下地动后震击技能入口不应残留。"
        );
    }

    private void TestShockwaveUsesSingleDamageSaveForHalfDamageAndProne()
    {
        using TremorFixture fixture = TremorFixture.Build(
            new GArray { 4, 4, 4, 4, 4, 4, 4, 4 },
            saveRollOverride: 10
        );
        BattleUnitState holder = fixture.BuildTremorUnit("shockwave_issue");
        holder.SetAnchorCoord(new Vector2I(2, 2));
        holder.attribute_snapshot.SetValue(AttributeService.CONSTITUTION_MODIFIER, 100);
        BattleUnitState failedEnemy = BuildTarget(
            "shockwave_failed_enemy",
            new Vector2I(3, 2),
            "enemy",
            constitutionModifier: -100
        );
        BattleUnitState successAlly = BuildTarget(
            "shockwave_success_ally",
            new Vector2I(2, 3),
            "player",
            constitutionModifier: 100
        );
        BattleUnitState outsideEnemy = BuildTarget(
            "shockwave_outside_enemy",
            new Vector2I(6, 6),
            "enemy",
            constitutionModifier: -100
        );

        BattleSkillAvailabilityView view = BuildEquipmentSkillView(fixture, holder);
        _test.True(
            TryFindSkillEntry(view, ShockwaveSkillId, out BattleAvailableSkillEntry entry),
            "实际施放震击前应能解析装备技能入口。"
        );
        if (entry == null)
            return;

        WeaponAbilityCommandTestSupport.PrimeActionResources(holder, ap: 2);
        fixture.Runtime.SetupStateForTests(
            BuildState("tremor_shockwave_issue", holder, failedEnemy, successAlly, outsideEnemy)
        );
        int failedBefore = failedEnemy.current_hp;
        int successBefore = successAlly.current_hp;
        int outsideBefore = outsideEnemy.current_hp;

        BattleCommand command = new()
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = holder.unit_id,
            skill_entry_id = entry.EntryRef.SkillEntryId,
            skill_id = ShockwaveSkillId,
            target_coord = holder.coord,
        };
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.True(
            preview?.allowed == true,
            $"震击 preview 应允许执行。logs={JoinLogs(preview?.LogLinesTyped)}"
        );
        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        _test.True(batch != null, "震击 IssueCommand 应返回事件 batch。");

        int failedDamage = failedBefore - failedEnemy.current_hp;
        int successDamage = successBefore - successAlly.current_hp;
        _test.True(failedDamage > 0, "震击豁免失败目标应受到 thunder 伤害。");
        _test.True(successDamage > 0, "震击豁免成功目标仍应受到半伤。");
        _test.Eq(
            successDamage,
            failedDamage / 2,
            "震击豁免成功目标应使用同一次伤害豁免得到半伤。"
        );
        BattleStatusEffectState prone = failedEnemy.GetStatusEffect(ProneStatusId);
        _test.True(prone != null, "震击豁免失败目标应获得 prone。");
        _test.Eq(prone?.duration ?? -1, 50, "震击附带 prone 应持续 50TU。");
        _test.False(successAlly.HasStatusEffect(ProneStatusId), "震击豁免成功目标不应 prone。");
        _test.Eq(
            outsideEnemy.current_hp,
            outsideBefore,
            "震击范围外目标不应受到伤害。"
        );
        _test.False(
            outsideEnemy.HasStatusEffect(ProneStatusId),
            "震击范围外目标不应获得 prone。"
        );
    }

    private static BattleSkillAvailabilityView BuildEquipmentSkillView(
        TremorFixture fixture,
        BattleUnitState unit
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

    private static BattleUnitState BuildTarget(
        StringName unitId,
        Vector2I coord,
        StringName factionId,
        int constitutionModifier
    )
    {
        BattleUnitState unit = new()
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
            is_alive = true,
            current_hp = 100,
        };
        unit.SetAnchorCoord(coord);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 14);
        unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 0);
        unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 0);
        unit.attribute_snapshot.SetValue(AttributeService.CONSTITUTION_MODIFIER, constitutionModifier);
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        unit.SetEquipmentView(new EquipmentState());
        return unit;
    }

    private static BattleState BuildState(
        StringName battleId,
        BattleUnitState holder,
        BattleUnitState failedEnemy,
        BattleUnitState successAlly,
        BattleUnitState outsideEnemy
    )
    {
        BattleState state = new()
        {
            battle_id = battleId,
            map_size = new Vector2I(8, 8),
        };
        state.PhaseKind = BattlePhaseKind.UnitActing;
        state.active_unit_id = holder.unit_id;
        AddPlainCells(state);
        AddUnit(state, holder, ally: true);
        AddUnit(state, successAlly, ally: true);
        AddUnit(state, failedEnemy, ally: false);
        AddUnit(state, outsideEnemy, ally: false);
        return state;
    }

    private static void AddPlainCells(BattleState state)
    {
        if (state == null)
            return;
        for (int x = 0; x < state.map_size.X; x++)
        {
            for (int y = 0; y < state.map_size.Y; y++)
            {
                BattleCellState cell = new();
                cell.SetCoord(new Vector2I(x, y));
                state.SetCell(cell);
            }
        }
    }

    private static void AddUnit(BattleState state, BattleUnitState unit, bool ally)
    {
        if (state == null || unit == null)
            return;
        state.SetUnit(unit);
        unit.RefreshFootprint();
        foreach (Vector2I coord in unit.occupied_coords)
        {
            state.GetCell(coord)?.SetOccupant(unit.unit_id);
        }
        if (ally)
            state.ally_unit_ids.Add(unit.unit_id);
        else
            state.enemy_unit_ids.Add(unit.unit_id);
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

    private static string JoinLogs(IEnumerable<string> values) =>
        values == null ? "" : string.Join(" | ", values);

    private sealed class TremorFixture : IDisposable
    {
        private readonly ItemContentRegistry _itemRegistry;
        private readonly ProgressionContentRegistry _progressionRegistry;
        private readonly PartyState _partyState;

        private TremorFixture(
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

        internal static TremorFixture Build(GArray damageRolls, int? saveRollOverride = null)
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
            BattleDamageResolver damageResolver = saveRollOverride.HasValue
                ? new FixedSaveRollDamageResolver(damageRolls, saveRollOverride.Value)
                : new FixedRollDamageResolver(damageRolls);
            runtime.ConfigureDamageResolverForTests(damageResolver);
            runtime.ConfigureHitResolverForTests(new FixedHitResolver(10));
            return new TremorFixture(itemRegistry, progressionRegistry, partyState, runtime);
        }

        internal BattleUnitState BuildUnitWithoutWeapon(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            return BuildSingleAllyUnit(label);
        }

        internal BattleUnitState BuildTremorUnit(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            member.equipment_state.SetEquippedEntry(
                "main_hand",
                TremorItemId,
                new GStringNameArray { "main_hand", "off_hand" },
                EquipmentInstanceState.CreateInstance(TremorItemId, $"eq_tremor_{label}")
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

    private sealed partial class FixedSaveRollDamageResolver : FixedRollDamageResolver
    {
        private readonly int _saveRollOverride;

        internal FixedSaveRollDamageResolver(GArray damageRolls, int saveRollOverride)
            : base(damageRolls)
        {
            _saveRollOverride = Math.Clamp(saveRollOverride, 1, 20);
        }

        internal override AttackEffectResolutionResult ResolveEffects(
            BattleUnitState source_unit,
            BattleUnitState target_unit,
            IEnumerable<CombatEffectDefinition> effect_definitions,
            DamageResolutionContext damage_context
        )
        {
            GDictionary fixedContext =
                damage_context?.RawContext?.Duplicate(true) ?? new GDictionary();
            fixedContext["save_roll_override"] = _saveRollOverride;
            return base.ResolveEffects(
                source_unit,
                target_unit,
                effect_definitions,
                DamageResolutionContext.FromDictionary(fixedContext)
            );
        }
    }
}
