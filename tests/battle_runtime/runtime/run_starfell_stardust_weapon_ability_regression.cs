using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_starfell_stardust_weapon_ability_regression : LifecycleTestSceneTree
{
    private static readonly StringName StarfellItemId = "weapon_unique_sword_starfell_016";
    private static readonly StringName StardustStatusId = "starfell_stardust";
    private static readonly StringName CosmicDreadStatusId = "starfell_cosmic_dread";
    private static readonly StringName StarMapSkillId = "weapon_sword_starfell_star_map_guidance";
    private static readonly StringName StarfallSkillId = "weapon_sword_starfell_starfall";
    private static readonly StringName MeteorForceBindingId =
        "binding.weapon.sword.starfell.meteor_force";
    private static readonly StringName StarMapBindingId =
        "binding.weapon.sword.starfell.star_map_guidance";
    private static readonly StringName CosmicDreadBindingId =
        "binding.weapon.sword.starfell.cosmic_dread";
    private static readonly StringName NextAttackAdvantageStateKey = "next_attack_advantage";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestMeteorForceLadderStacksAndCosmicDread();
            TestStarMapGuidanceConsumesStardustAndGrantsAdvantage();
            TestStarfallGateAndHighestStacksConsumption();
            RequestTestExit(_test.Finish("Starfell stardust weapon ability regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Starfell stardust weapon ability regression"));
        }
    }

    private void TestMeteorForceLadderStacksAndCosmicDread()
    {
        using StardustFixture fixture = StardustFixture.Build(BuildRolls(40));
        BattleUnitState holder = fixture.BuildStarfellUnit("ladder");
        BattleUnitState target = BuildEnemy("ladder_target", new Vector2I(1, 0), hp: 200);

        int[] expectedHitDamage = { 10, 13, 16, 19, 22, 31 };
        int[] expectedStacks = { 1, 2, 3, 4, 5, 5 };
        int previousHp = target.current_hp;
        for (int hit = 0; hit < expectedHitDamage.Length; hit++)
        {
            WeaponAbilityCommandTestSupport.IssueBasicAttack(
                fixture.Runtime,
                holder,
                target,
                $"starfell_ladder_hit_{hit + 1}",
                previewCommand: false
            );
            int damage = previousHp - target.current_hp;
            previousHp = target.current_hp;
            _test.Eq(
                damage,
                expectedHitDamage[hit],
                $"固定骰 3 下第 {hit + 1} 次命中伤害应为 {expectedHitDamage[hit]}（当次命中不吃新叠的星尘层）。"
            );
            BattleStatusEffectState stardust = target.GetStatusEffect(StardustStatusId);
            _test.True(stardust != null, $"第 {hit + 1} 次命中后目标应有星尘。");
            if (stardust != null)
            {
                _test.Eq(
                    stardust.stacks,
                    expectedStacks[hit],
                    $"第 {hit + 1} 次命中后星尘层数应为 {expectedStacks[hit]}（上限 5）。"
                );
                _test.Eq(stardust.duration, 60, "星尘应为 60TU 并在每次命中刷新。");
                _test.Eq(stardust.source_unit_id, holder.unit_id, "星尘应记录持有者来源。");
            }
            if (hit < 5)
            {
                _test.False(
                    target.HasStatusEffect(CosmicDreadStatusId),
                    $"目标星尘不足 5 层时第 {hit + 1} 次命中不应触发宇宙恐惧。"
                );
            }
        }

        BattleStatusEffectState dread = target.GetStatusEffect(CosmicDreadStatusId);
        _test.True(dread != null, "命中已有 5 层星尘的目标应施加宇宙恐惧。");
        if (dread != null)
        {
            _test.Eq(dread.duration, 30, "宇宙恐惧应持续 30TU。");
            _test.Eq(dread.source_unit_id, holder.unit_id, "宇宙恐惧应记录持有者来源。");
            _test.Eq(
                BattleStatusSemanticTable.GetAttackRollPenalty(dread),
                2,
                "宇宙恐惧应使目标攻击检定 -2。"
            );
        }
    }

    private void TestStarMapGuidanceConsumesStardustAndGrantsAdvantage()
    {
        using StardustFixture fixture = StardustFixture.Build(BuildRolls(16));
        BattleUnitState holder = fixture.BuildStarfellUnit("starmap");
        BattleUnitState target = BuildEnemy("starmap_target", new Vector2I(1, 0), hp: 200);

        for (int hit = 1; hit <= 2; hit++)
        {
            WeaponAbilityCommandTestSupport.IssueBasicAttack(
                fixture.Runtime,
                holder,
                target,
                $"starfell_starmap_hit_{hit}",
                previewCommand: false
            );
        }
        _test.Eq(
            target.GetStatusEffect(StardustStatusId)?.stacks ?? 0,
            2,
            "星图指引 fixture 应先积累 2 层星尘。"
        );

        BattleState state = fixture.Runtime.GetState();
        BattleSkillAvailabilityView readyView = BuildEquipmentSkillAvailability(
            fixture,
            holder,
            state
        );
        _test.True(
            TryFindSkillEntry(readyView, StarMapSkillId, out BattleAvailableSkillEntry starMapEntry),
            "星尘总数≥2 时应显示星图指引装备技能。"
        );
        _test.True(starMapEntry?.IsSelectable == true, "星尘总数≥2 时星图指引应可选择。");
        _test.True(
            TryFindSkillEntry(readyView, StarfallSkillId, out BattleAvailableSkillEntry starfallGateEntry),
            "星坠入口应保留给 UI 展示。"
        );
        _test.False(
            starfallGateEntry?.IsSelectable == true,
            "星尘总数不足 5 时星坠不应可用。"
        );

        _test.True(
            fixture.Runtime.CommitEquipmentSkillUsageIfNeeded(
                holder,
                BuildGrantedSkillCommand(holder.unit_id, starMapEntry)
            ),
            "使用星图指引装备技能应提交 granted skill 触发。"
        );
        _test.False(
            target.HasStatusEffect(StardustStatusId),
            "星图指引应消耗目标身上的 2 层星尘。"
        );
        _test.Eq(
            GetAbilityState(holder, StarMapBindingId, NextAttackAdvantageStateKey),
            1,
            "星图指引应写入下一次攻击 advantage 状态。"
        );

        BattleAttackCheckPolicyService attackPolicy = fixture.Runtime.GetAttackCheckPolicyService();
        SkillDefinition attackSkill = TestSkillDefinitionProjection.BuildSkill("starfell_fixture_attack");
        BattleAttackRollModifierBundle advantageBundle = attackPolicy.BuildModifierBundle(
            attackPolicy.BuildSkillDefinitionAttackContext(
                state,
                holder,
                target,
                attackSkill,
                "skill_attack_check",
                "starfell_starmap_advantage",
                force_hit_no_crit: false
            )
        );
        _test.True(
            HasAdvantageModifier(advantageBundle, StarMapBindingId),
            "星图指引应使下一次攻击检定获得 advantage。"
        );

        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            holder,
            target,
            "starfell_starmap_after_advantage",
            previewCommand: false
        );
        _test.Eq(
            GetAbilityState(holder, StarMapBindingId, NextAttackAdvantageStateKey),
            0,
            "星图指引 advantage 应在一次命中后清除。"
        );

        BattleSkillAvailabilityView emptyView = BuildEquipmentSkillAvailability(
            fixture,
            holder,
            fixture.Runtime.GetState()
        );
        _test.True(
            TryFindSkillEntry(emptyView, StarMapSkillId, out BattleAvailableSkillEntry poorEntry),
            "星尘不足时星图指引入口仍应保留给 UI 展示。"
        );
        _test.False(
            poorEntry?.IsSelectable == true,
            "星尘总数不足 2 时星图指引不应可用。"
        );
    }

    private void TestStarfallGateAndHighestStacksConsumption()
    {
        using StardustFixture fixture = StardustFixture.Build(BuildRolls(60));
        BattleUnitState holder = fixture.BuildStarfellUnit("starfall");
        BattleUnitState targetA = BuildEnemy("starfall_target_a", new Vector2I(1, 0), hp: 400);
        BattleUnitState targetB = BuildEnemy("starfall_target_b", new Vector2I(0, 1), hp: 200);
        BattleState state = BuildStateWithEnemies(
            fixture.Runtime,
            "starfell_starfall_gate",
            holder,
            targetA,
            targetB
        );

        for (int hit = 1; hit <= 5; hit++)
            IssueBasicAttackInCurrentState(fixture.Runtime, state, holder, targetA);
        _test.Eq(
            targetA.GetStatusEffect(StardustStatusId)?.stacks ?? 0,
            5,
            "星坠 fixture 目标 A 应先叠满 5 层星尘。"
        );

        IssueBasicAttackInCurrentState(fixture.Runtime, state, holder, targetB);
        _test.Eq(
            targetB.GetStatusEffect(StardustStatusId)?.stacks ?? 0,
            1,
            "星坠 fixture 目标 B 应有 1 层星尘。"
        );

        BattleSkillAvailabilityView readyView = BuildEquipmentSkillAvailability(
            fixture,
            holder,
            state
        );
        _test.True(
            TryFindSkillEntry(readyView, StarfallSkillId, out BattleAvailableSkillEntry starfallEntry),
            "星尘总数≥5 时应显示星坠装备技能。"
        );
        _test.True(starfallEntry?.IsSelectable == true, "星尘总数≥5 时星坠应可选择。");

        WeaponAbilityCommandTestSupport.PrimeActionResources(holder);
        state.PhaseKind = BattlePhaseKind.UnitActing;
        state.active_unit_id = holder.unit_id;
        BattleCommand starfallCommand = new()
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = holder.unit_id,
            skill_entry_id = starfallEntry?.EntryRef.SkillEntryId ?? new StringName(""),
            skill_id = StarfallSkillId,
            target_coord = new Vector2I(1, 1),
        };
        BattlePreview preview = fixture.Runtime.PreviewCommand(starfallCommand);
        if (preview?.allowed != true)
        {
            throw new InvalidOperationException(
                $"starfall preview blocked: {string.Join(" | ", preview?.LogLinesTyped ?? Array.Empty<string>())}"
            );
        }
        fixture.Runtime.IssueCommand(starfallCommand);

        _test.False(
            targetA.HasStatusEffect(StardustStatusId),
            "星坠应优先从层数最多的目标 A 扣除 5 层星尘。"
        );
        _test.Eq(
            targetB.GetStatusEffect(StardustStatusId)?.stacks ?? 0,
            1,
            "星坠扣满 5 层后不应继续扣目标 B 的星尘。"
        );

        BattleSkillAvailabilityView usedView = BuildEquipmentSkillAvailability(
            fixture,
            holder,
            state
        );
        _test.True(
            TryFindSkillEntry(usedView, StarfallSkillId, out BattleAvailableSkillEntry usedEntry),
            "星坠使用后入口仍应保留给 UI 展示。"
        );
        _test.False(
            usedEntry?.IsSelectable == true,
            "星坠每场战斗 1 次，施放后不应再次可用。"
        );
    }

    private static GArray BuildRolls(int count)
    {
        GArray rolls = new();
        for (int index = 0; index < count; index++)
            rolls.Add(3);
        return rolls;
    }

    private static BattleState BuildStateWithEnemies(
        BattleRuntimeModule runtime,
        StringName battleId,
        BattleUnitState holder,
        params BattleUnitState[] enemies
    )
    {
        BattleState state = WeaponAbilityCommandTestSupport.BuildFlatState(
            battleId,
            holder,
            enemies != null && enemies.Length > 0 ? enemies[0] : null
        );
        for (int index = 1; index < (enemies?.Length ?? 0); index++)
        {
            BattleUnitState enemy = enemies[index];
            state.SetUnit(enemy);
            enemy.RefreshFootprint();
            foreach (Vector2I coord in enemy.occupied_coords)
                state.GetCell(coord)?.SetOccupant(enemy.unit_id);
            state.enemy_unit_ids.Add(enemy.unit_id);
        }
        runtime.SetupStateForTests(state);
        return state;
    }

    private static void IssueBasicAttackInCurrentState(
        BattleRuntimeModule runtime,
        BattleState state,
        BattleUnitState attacker,
        BattleUnitState target
    )
    {
        WeaponAbilityCommandTestSupport.PrimeBasicAttack(attacker);
        state.PhaseKind = BattlePhaseKind.UnitActing;
        state.active_unit_id = attacker.unit_id;
        BattleCommand command = WeaponAbilityCommandTestSupport.BuildBasicAttackCommand(
            attacker,
            target
        );
        runtime.IssueCommand(command);
    }

    private static BattleCommand BuildGrantedSkillCommand(
        StringName unitId,
        BattleAvailableSkillEntry entry
    ) =>
        new()
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = unitId,
            skill_entry_id = entry?.EntryRef.SkillEntryId ?? "",
            skill_id = entry?.EntryRef.SkillId ?? "",
        };

    private static BattleSkillAvailabilityView BuildEquipmentSkillAvailability(
        StardustFixture fixture,
        BattleUnitState holder,
        BattleState state
    )
    {
        BattleSkillAvailabilityService service = new(fixture.SkillDefs, fixture.Bindings);
        return service.BuildView(
            new BattleSkillAvailabilityQuery
            {
                User = holder,
                IncludeEquipmentSkills = true,
                IncludeKnownSkills = false,
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

    private static bool HasAdvantageModifier(
        BattleAttackRollModifierBundle bundle,
        StringName sourceId
    )
    {
        foreach (BattleAttackRollModifierSpec spec in bundle?.Breakdown ?? Array.Empty<BattleAttackRollModifierSpec>())
        {
            if (spec.source_id == sourceId && spec.applies_to == "attack_advantage")
                return true;
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
        if (key == "")
            return 0;
        if (unit.HasPerBattleChargeTyped(key))
            return unit.GetPerBattleChargeTyped(key, 0);
        return unit.GetPerTurnChargeTyped(key, 0);
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
        foreach (StringName key in unit.GetPerTurnChargesTyped().Keys)
        {
            if (key.ToString().EndsWith(suffix, StringComparison.Ordinal))
                return key;
        }
        foreach (StringName key in unit.GetPerTurnChargeLimitsTyped().Keys)
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
        unit.attribute_snapshot.SetValue("agility", 10);
        unit.creature_type_tags.Add(new StringName("humanoid"));
        unit.SetEquipmentView(new EquipmentState());
        return unit;
    }

    private sealed class StardustFixture : IDisposable
    {
        private readonly ItemContentRegistry _itemRegistry;
        private readonly ProgressionContentRegistry _progressionRegistry;
        private readonly PartyState _partyState;

        private StardustFixture(
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
            SkillDefs = progressionRegistry.GetSkillDefinitionsTyped();
            Bindings = progressionRegistry.GetEquipmentAbilityBindingDefinitionsTyped();
        }

        internal BattleRuntimeModule Runtime { get; }
        internal IReadOnlyDictionary<StringName, SkillDefinition> SkillDefs { get; }
        internal IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> Bindings { get; }

        internal static StardustFixture Build(GArray damageRolls)
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
            return new StardustFixture(itemRegistry, progressionRegistry, partyState, runtime);
        }

        internal BattleUnitState BuildStarfellUnit(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            member.equipment_state.SetEquippedEntry(
                "main_hand",
                StarfellItemId,
                new GStringNameArray { "main_hand" },
                EquipmentInstanceState.CreateInstance(StarfellItemId, $"eq_starfell_{label}")
            );
            IReadOnlyList<BattleUnitState> units =
                Runtime._unit_factory.BuildAllyUnits(_partyState, new GDictionary());
            if (units.Count != 1)
            {
                throw new InvalidOperationException(
                    $"{label} scenario should build exactly one ally unit."
                );
            }
            BattleUnitState unit = units[0];
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
