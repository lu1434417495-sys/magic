using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_echo_weapon_ability_regression : LifecycleTestSceneTree
{
    private static readonly StringName EchoItemId = "weapon_unique_axe_echo_095";
    private static readonly StringName EchoThrowTraitId = "weapon.axe.echo.echo_throw";
    private static readonly StringName EchoCutTraitId = "weapon.axe.echo.echo_cut";
    private static readonly StringName EchoThrowBindingId = "binding.weapon.axe.echo.echo_throw";
    private static readonly StringName EchoCutBindingId = "binding.weapon.axe.echo.echo_cut";
    private static readonly StringName EchoThrowSkillId = "weapon_axe_echo_throw";
    private static readonly StringName EchoThrowGrantId = "grant.echo.echo_throw.skill";
    private static readonly StringName ReverberationStatusId = "echo_reverberation";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestEchoProjectsRealContentAndEquipmentSkill();
            TestEchoThrowUsesCasterLineAndStacksForDamagedTargets();
            TestEchoCutConsumesAllReverberationOnWeaponHit();
            RequestTestExit(_test.Finish("Echo weapon ability regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Echo weapon ability regression"));
        }
    }

    private void TestEchoProjectsRealContentAndEquipmentSkill()
    {
        using EchoFixture fixture = EchoFixture.Build(new GArray());
        _test.True(fixture.ItemDefs.ContainsKey(EchoItemId), "真实物品内容应包含回音。");
        _test.True(fixture.TraitDefs.ContainsKey(EchoThrowTraitId), "真实 trait 内容应包含回音投掷。");
        _test.True(fixture.TraitDefs.ContainsKey(EchoCutTraitId), "真实 trait 内容应包含回声斩。");
        _test.True(
            fixture.Bindings.ContainsKey(EchoThrowBindingId),
            "真实装备能力内容应包含回音投掷 binding。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(EchoCutBindingId),
            "真实装备能力内容应包含回声斩 binding。"
        );
        _test.True(
            fixture.SkillDefs.ContainsKey(EchoThrowSkillId),
            "回音投掷应落成真实 SkillDef，而不是 trait 文本。"
        );

        using ItemDef rawItem = ResourceLoader.Load<ItemDef>(
            "res://data/configs/items/weapon_unique_handaxe_echo.tres"
        );
        _test.True(rawItem != null, "回音原始资源应能加载。");
        if (rawItem != null)
        {
            _test.Eq(rawItem.display_name, "回音", "回音显示名应匹配设计。");
            _test.Eq(
                rawItem.base_item_id,
                new StringName("weapon_type_handaxe_base"),
                "回音应继承 handaxe 模板。"
            );
            _test.Eq(rawItem.base_price, 36000, "回音价格应为 36000。");
            _test.True(rawItem.trait_ids.Contains(EchoThrowTraitId), "回音物品应声明回音投掷。");
            _test.True(rawItem.trait_ids.Contains(EchoCutTraitId), "回音物品应声明回声斩。");
            _test.False(
                ContainsText(rawItem.description, "洞穴")
                    || ContainsText(rawItem.description, "沉默")
                    || ContainsText(rawItem.description, "undead")
                    || ContainsText(rawItem.description, "construct")
                    || ContainsText(rawItem.description, "cave")
                    || ContainsText(rawItem.description, "silence"),
                "玩家说明不应保留旧洞穴/沉默设定或英文目标类型。"
            );
        }

        if (fixture.SkillDefs.TryGetValue(EchoThrowSkillId, out SkillDefinition echoThrow))
        {
            AssertEchoThrowSkillDefinition(echoThrow, fixture);
        }

        BattleUnitState baseline = fixture.BuildUnitWithoutWeapon("baseline");
        BattleUnitState equipped = fixture.BuildEchoUnit("projection");
        _test.Eq(equipped.weapon_item_id, EchoItemId, "回音装备后 unit 应保留真实 item_id。");
        _test.Eq(equipped.weapon_profile_type_id, new StringName("handaxe"), "回音应投影为 handaxe。");
        _test.Eq(equipped.weapon_family, new StringName("axe"), "回音应投影为 axe family。");
        _test.Eq(
            equipped.weapon_physical_damage_tag,
            new StringName("physical_slash"),
            "回音应为挥砍伤害。"
        );
        _test.Eq(equipped.weapon_attack_range, 1, "回音近战攻击距离应为 1。");
        _test.False(equipped.weapon_uses_two_hands, "回音应是单手手斧。");
        _test.Eq(equipped.weapon_one_handed_dice?.dice_count ?? 0, 1, "回音单手应为 1D6+1。");
        _test.Eq(equipped.weapon_one_handed_dice?.dice_sides ?? 0, 6, "回音单手应为 1D6+1。");
        _test.Eq(equipped.weapon_one_handed_dice?.flat_bonus ?? 0, 1, "回音单手应为 1D6+1。");
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            EchoThrowTraitId,
            EchoThrowBindingId,
            "eq_echo_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            EchoCutTraitId,
            EchoCutBindingId,
            "eq_echo_projection"
        );

        BattleSkillAvailabilityView view = BuildEquipmentSkillAvailability(fixture, equipped, null);
        _test.True(
            TryFindSkillEntry(view, EchoThrowSkillId, out BattleAvailableSkillEntry entry),
            "装备回音后，unit 的可用技能应包含回音投掷。"
        );
        if (entry != null)
        {
            _test.Eq(
                entry.EntryRef.SourceKind,
                BattleSkillEntrySourceKind.EquipmentSkill,
                "回音投掷来源应是 equipment_skill。"
            );
            _test.Eq(entry.EquipmentBindingId, EchoThrowBindingId, "回音投掷入口应携带 binding id。");
            _test.Eq(entry.EquipmentGrantedActionId, EchoThrowGrantId, "回音投掷入口应携带 grant id。");
        }

        equipped.GetEquipmentView().ClearSlot("main_hand");
        fixture.Runtime._unit_factory.RefreshBattleUnit(equipped);
        _test.Eq(equipped.weapon_item_id, new StringName(""), "移除回音后 weapon_item_id 应清空。");
        _test.Eq(
            equipped.weapon_profile_type_id,
            baseline.weapon_profile_type_id,
            "移除回音后武器 profile 应回到装备前状态。"
        );
        _test.Eq(equipped.equipment_ability_sources.Count, 0, "移除回音后装备能力源应清空。");
    }

    private void TestEchoThrowUsesCasterLineAndStacksForDamagedTargets()
    {
        using EchoFixture fixture = EchoFixture.Build(new GArray { 4, 4 });
        BattleUnitState holder = fixture.BuildEchoUnit("throw");
        holder.SetAnchorCoord(new Vector2I(0, 1));
        holder.SetCombatResources(80, 0, 100, 0, 2, 2);
        holder.attribute_snapshot.SetValue(AttributeService.STAMINA_MAX, 100);

        BattleUnitState firstEnemy = BuildEnemy("echo_line_first", new Vector2I(1, 1), 30);
        BattleUnitState secondEnemy = BuildEnemy("echo_line_second", new Vector2I(3, 1), 30);
        BattleUnitState offLineEnemy = BuildEnemy("echo_line_off", new Vector2I(1, 2), 30);
        BattleState state = BuildState(
            "echo_throw_line",
            holder,
            new[] { firstEnemy, secondEnemy, offLineEnemy },
            new Vector2I(7, 4)
        );
        fixture.Runtime.SetupStateForTests(state);

        BattleSkillAvailabilityView view = BuildEquipmentSkillAvailability(fixture, holder, state);
        _test.True(
            TryFindSkillEntry(view, EchoThrowSkillId, out BattleAvailableSkillEntry entry),
            "回音投掷应能解析为装备技能入口。"
        );
        if (entry == null)
            return;

        SkillDefinition skill = fixture.SkillDefs[EchoThrowSkillId];
        CombatCastVariantDefinition variant =
            fixture.Runtime._skill_resolution_rules.ResolveGroundCastVariantDefinition(
                skill,
                holder,
                ""
            );
        BattleCommand diagonalCommand = BuildGroundSkillCommand(holder, entry, new Vector2I(3, 2));
        BattleGroundSkillValidationResult diagonalValidation =
            fixture.Runtime.ValidateGroundSkillCommandResultTyped(
                holder,
                skill,
                variant,
                diagonalCommand
            );
        _test.False(diagonalValidation.Allowed, "回音投掷只能选择与使用者同行或同列的目标格。");

        BattleCommand command = BuildGroundSkillCommand(holder, entry, new Vector2I(4, 1));
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.True(
            preview?.allowed == true,
            $"回音投掷选择直线目标格时 preview 应允许。logs={JoinLogs(preview?.LogLinesTyped)}"
        );
        _test.True(ContainsCoord(preview.TargetCoordsTyped, new Vector2I(1, 1)), "预览应包含第 1 格路径。");
        _test.True(ContainsCoord(preview.TargetCoordsTyped, new Vector2I(2, 1)), "预览应包含第 2 格路径。");
        _test.True(ContainsCoord(preview.TargetCoordsTyped, new Vector2I(3, 1)), "预览应包含第 3 格路径。");
        _test.True(ContainsCoord(preview.TargetCoordsTyped, new Vector2I(4, 1)), "预览应包含目标格。");
        _test.False(ContainsCoord(preview.TargetCoordsTyped, holder.coord), "预览不应包含使用者所在格。");
        _test.False(ContainsCoord(preview.TargetCoordsTyped, new Vector2I(1, 2)), "预览不应包含偏离直线的格。");
        _test.True(ContainsStringName(preview.TargetUnitIdsTyped, firstEnemy.unit_id), "预览应包含直线路径第一个敌人。");
        _test.True(ContainsStringName(preview.TargetUnitIdsTyped, secondEnemy.unit_id), "预览应包含直线路径第二个敌人。");
        _test.False(ContainsStringName(preview.TargetUnitIdsTyped, offLineEnemy.unit_id), "预览不应包含偏离直线的敌人。");

        int staminaBefore = holder.current_stamina;
        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        _test.True(batch != null, "回音投掷 IssueCommand 应返回事件 batch。");
        _test.Eq(firstEnemy.current_hp, 25, "直线路径第一个敌人应受到 1D6+1 挥砍伤害。");
        _test.Eq(secondEnemy.current_hp, 25, "直线路径第二个敌人应受到 1D6+1 挥砍伤害。");
        _test.Eq(offLineEnemy.current_hp, 30, "偏离直线的敌人不应受伤。");
        _test.Eq(holder.current_stamina, staminaBefore - 40, "回音投掷应消耗 40 体力。");
        _test.Eq(holder.current_ap, 1, "回音投掷应消耗 1 AP。");
        _test.Eq(holder.GetCooldownTyped(EchoThrowSkillId), 90, "回音投掷应设置 90TU 冷却。");

        BattleStatusEffectState reverberation = holder.GetStatusEffect(ReverberationStatusId);
        _test.True(reverberation != null, "回音投掷造成 HP 伤害后，持有者应获得余音。");
        if (reverberation != null)
        {
            _test.Eq(reverberation.stacks, 2, "余音层数应等于实际受伤敌人数量，上限 5。");
            _test.Eq(reverberation.duration, 60, "余音应持续 60TU。");
            _test.Eq(reverberation.source_unit_id, holder.unit_id, "余音应记录持有者来源。");
        }
    }

    private void TestEchoCutConsumesAllReverberationOnWeaponHit()
    {
        using EchoFixture fixture = EchoFixture.Build(new GArray { 4, 2, 2, 2 });
        BattleUnitState attacker = fixture.BuildEchoUnit("cut");
        attacker.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = ReverberationStatusId,
                source_unit_id = attacker.unit_id,
                stacks = 3,
                duration = 60,
                display_label = "余音",
                stack_behavior = "add",
                stack_limit = 5,
                undispellable = true,
                counts_as_debuff_override = true,
                counts_as_debuff = false,
            }
        );
        BattleUnitState target = BuildEnemy("echo_cut_target", new Vector2I(1, 0), 50);

        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            attacker,
            target,
            "echo_cut_consumes_all",
            previewCommand: false
        );

        _test.Eq(
            target.current_hp,
            39,
            "3 层余音的下一次真实命中应造成武器 1D6+1 与 3D6 雷鸣伤害。"
        );
        _test.False(attacker.HasStatusEffect(ReverberationStatusId), "真实武器命中后应一次性消耗全部余音。");
    }

    private void AssertEchoThrowSkillDefinition(SkillDefinition skill, EchoFixture fixture)
    {
        CombatSkillDefinition combat = skill.CombatProfile;
        _test.True(combat != null, "回音投掷技能应有 combat_profile。");
        if (combat == null)
            return;
        _test.Eq(combat.TargetMode, new StringName("ground"), "回音投掷应选择地面格。");
        _test.Eq(combat.TargetTeamFilter, new StringName("enemy"), "回音投掷只应影响敌方。");
        _test.Eq(combat.RangeValue, 6, "回音投掷射程应为 6 格。");
        _test.Eq(combat.AreaPattern, new StringName("line"), "回音投掷应使用直线范围。");
        _test.Eq(combat.AreaValue, 6, "回音投掷直线最多覆盖 6 格。");
        _test.Eq(combat.AreaOriginMode, new StringName("caster"), "回音投掷范围起点应是使用者。");
        _test.Eq(
            combat.AreaDirectionMode,
            new StringName("target_vector"),
            "回音投掷方向应由目标格决定。"
        );
        _test.Eq(
            combat.AttackResolutionMode,
            new StringName("direct_effect"),
            "回音投掷应作为直接效果结算，不触发普通武器命中。"
        );
        _test.Eq(combat.ApCost, 1, "回音投掷应消耗 1 AP。");
        _test.Eq(combat.StaminaCost, 40, "回音投掷应消耗 40 体力。");
        _test.Eq(combat.CooldownTu, 90, "回音投掷应有 90TU 冷却。");
        _test.Eq(combat.EffectDefinitions.Count, 1, "回音投掷应只有一个伤害 effect。");
        if (combat.EffectDefinitions.Count > 0)
        {
            CombatEffectDefinition damage = combat.EffectDefinitions[0];
            _test.Eq(damage.EffectType, new StringName("damage"), "回音投掷 effect 应是 damage。");
            _test.Eq(damage.DamageTag, new StringName("physical_slash"), "回音投掷应造成挥砍伤害。");
            _test.Eq(damage.DiceCount, 1, "回音投掷伤害应是 1D6+1。");
            _test.Eq(damage.DiceSides, 6, "回音投掷伤害应是 1D6+1。");
            _test.Eq(damage.DiceBonus, 1, "回音投掷伤害应是 1D6+1。");
        }

        _test.True(
            fixture.Bindings.TryGetValue(EchoThrowBindingId, out EquipmentAbilityBindingDefinition binding),
            "回音投掷 binding 应存在。"
        );
        if (binding != null)
        {
            _test.Eq(binding.GrantedActions.Count, 1, "回音投掷 binding 应授予一个装备技能入口。");
            if (binding.GrantedActions.Count > 0)
            {
                EquipmentGrantedActionDefinition grant = binding.GrantedActions[0];
                _test.Eq(grant.SkillId, EchoThrowSkillId, "回音投掷 grant 应指向真实 SkillDef。");
                _test.Eq(grant.GrantedActionId, EchoThrowGrantId, "回音投掷 grant id 应稳定。");
                _test.Eq(
                    grant.UsagePeriodKind,
                    EquipmentAbilityUsagePeriodKind.None,
                    "回音投掷使用限制应由技能消耗和冷却承担。"
                );
            }
        }
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

    private static BattleSkillAvailabilityView BuildEquipmentSkillAvailability(
        EchoFixture fixture,
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

    private static BattleState BuildState(
        StringName battleId,
        BattleUnitState holder,
        IReadOnlyList<BattleUnitState> enemies,
        Vector2I mapSize
    )
    {
        BattleState state = new()
        {
            battle_id = battleId,
            map_size = mapSize,
        };
        state.PhaseKind = BattlePhaseKind.UnitActing;
        state.active_unit_id = holder.unit_id;
        AddPlainCells(state);
        state.SetUnit(holder);
        SetUnitOccupants(state, holder);
        state.ally_unit_ids.Add(holder.unit_id);
        foreach (BattleUnitState enemy in enemies ?? Array.Empty<BattleUnitState>())
        {
            state.SetUnit(enemy);
            SetUnitOccupants(state, enemy);
            state.enemy_unit_ids.Add(enemy.unit_id);
        }
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

    private static void SetUnitOccupants(BattleState state, BattleUnitState unit)
    {
        if (state == null || unit == null)
            return;
        unit.RefreshFootprint();
        foreach (Vector2I coord in unit.occupied_coords)
        {
            BattleCellState cell = state.GetCell(coord);
            cell?.SetOccupant(unit.unit_id);
        }
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
            body_size = 1,
            body_size_category = "small",
        };
        unit.SetAnchorCoord(coord);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 14);
        unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 0);
        unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 0);
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

    private static bool ContainsCoord(IEnumerable<Vector2I> coords, Vector2I expected)
    {
        foreach (Vector2I coord in coords ?? Array.Empty<Vector2I>())
        {
            if (coord == expected)
                return true;
        }
        return false;
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

    private static bool ContainsText(string value, string needle) =>
        !string.IsNullOrEmpty(value)
        && !string.IsNullOrEmpty(needle)
        && value.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;

    private static string JoinLogs(IEnumerable<string> values) =>
        values == null ? "" : string.Join(" | ", values);

    private sealed class EchoFixture : IDisposable
    {
        private readonly ItemContentRegistry _itemRegistry;
        private readonly ProgressionContentRegistry _progressionRegistry;
        private readonly PartyState _partyState;

        private EchoFixture(
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

        internal static EchoFixture Build(GArray damageRolls)
        {
            ItemContentRegistry itemRegistry = new(new TestContentResourceLoader());
            ProgressionContentRegistry progressionRegistry = new(new TestContentResourceLoader());
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
            return new EchoFixture(itemRegistry, progressionRegistry, partyState, runtime);
        }

        internal BattleUnitState BuildUnitWithoutWeapon(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            return BuildSingleAllyUnit(label);
        }

        internal BattleUnitState BuildEchoUnit(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            member.equipment_state.SetEquippedEntry(
                "main_hand",
                EchoItemId,
                new GStringNameArray { "main_hand" },
                EquipmentInstanceState.CreateInstance(EchoItemId, $"eq_echo_{label}")
            );
            BattleUnitState unit = BuildSingleAllyUnit(label);
            unit.body_size = 1;
            unit.body_size_category = "small";
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
