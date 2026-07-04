using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_glory_weapon_ability_regression : SceneTree
{
    private static readonly StringName GloryItemId = "weapon_unique_sword_glory_261";
    private static readonly StringName CrowdGazeTraitId =
        "weapon.sword.glory.crowd_gaze";
    private static readonly StringName GlorySpotlightTraitId =
        "weapon.sword.glory.spotlight";
    private static readonly StringName CurtainCallTraitId =
        "weapon.sword.glory.curtain_call";
    private static readonly StringName LonelyDarkTraitId =
        "weapon.sword.glory.lonely_dark";
    private static readonly StringName CrowdGazeBindingId =
        "binding.weapon.sword.glory.crowd_gaze";
    private static readonly StringName GlorySpotlightBindingId =
        "binding.weapon.sword.glory.spotlight";
    private static readonly StringName CurtainCallBindingId =
        "binding.weapon.sword.glory.curtain_call";
    private static readonly StringName LonelyDarkBindingId =
        "binding.weapon.sword.glory.lonely_dark";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestGloryProjectsRealContentOntoBattleUnitAndClearsOnUnequip();
            TestCrowdGazeAndLonelyDarkUseAllNearbyLivingCreatures();
            TestSpotlightAddsRadiantDamageAndLonelyDarkSubtractsDamage();
            TestCurtainCallTriggersFreeWeaponAttackNearDefeatedEnemy();
            Quit(_test.Finish("Glory weapon ability regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            Quit(_test.Finish("Glory weapon ability regression"));
        }
    }

    private void TestGloryProjectsRealContentOntoBattleUnitAndClearsOnUnequip()
    {
        using GloryFixture fixture = GloryFixture.Build(new FixedRollDamageResolver(new GArray()));
        _test.True(fixture.ItemDefs.ContainsKey(GloryItemId), "真实物品内容应包含荣耀之刃。");
        _test.True(fixture.TraitDefs.ContainsKey(CrowdGazeTraitId), "真实 trait 应包含众目睽睽。");
        _test.True(fixture.TraitDefs.ContainsKey(GlorySpotlightTraitId), "真实 trait 应包含荣耀 spotlight。");
        _test.True(fixture.TraitDefs.ContainsKey(CurtainCallTraitId), "真实 trait 应包含谢幕斩。");
        _test.True(fixture.TraitDefs.ContainsKey(LonelyDarkTraitId), "真实 trait 应包含孤独之暗。");
        _test.True(fixture.Bindings.ContainsKey(CrowdGazeBindingId), "真实装备能力内容应包含众目睽睽 binding。");
        _test.True(fixture.Bindings.ContainsKey(GlorySpotlightBindingId), "真实装备能力内容应包含荣耀 spotlight binding。");
        _test.True(fixture.Bindings.ContainsKey(CurtainCallBindingId), "真实装备能力内容应包含谢幕斩 binding。");
        _test.True(fixture.Bindings.ContainsKey(LonelyDarkBindingId), "真实装备能力内容应包含孤独之暗 binding。");
        if (!fixture.ItemDefs.ContainsKey(GloryItemId))
            return;

        ItemDef rawGlory = ResourceLoader.Load<ItemDef>(
            "res://data/configs/items/weapon_unique_longsword_glory.tres"
        );
        _test.True(rawGlory != null, "荣耀之刃原始资源应能加载。");
        if (rawGlory != null)
        {
            _test.Eq(rawGlory.base_item_id, new StringName("weapon_type_longsword_base"), "荣耀之刃应继承 longsword 模板。");
            _test.Eq(rawGlory.base_price, 78000, "荣耀之刃价格应落成 78000。");
            _test.True(ContainsStringName(rawGlory.tags, "glory"), "荣耀之刃物品 tag 应包含 glory。");
        }

        BattleUnitState baseline = fixture.BuildUnitWithoutWeapon("baseline");
        BattleUnitState equipped = fixture.BuildGloryUnit("projection");

        _test.Eq(equipped.weapon_item_id, GloryItemId, "荣耀之刃装备后 unit 应保留真实 item_id。");
        _test.Eq(equipped.weapon_profile_type_id, new StringName("longsword"), "荣耀之刃应投影为 longsword。");
        _test.Eq(equipped.weapon_attack_range, 1, "荣耀之刃攻击距离应为 1。");
        _test.True(equipped.weapon_is_versatile, "荣耀之刃应保留 versatile。");
        _test.Eq(equipped.weapon_one_handed_dice?.dice_count ?? 0, 1, "荣耀之刃单手骰应为 1D8+3。");
        _test.Eq(equipped.weapon_one_handed_dice?.dice_sides ?? 0, 8, "荣耀之刃单手骰应为 1D8+3。");
        _test.Eq(equipped.weapon_one_handed_dice?.flat_bonus ?? 0, 3, "荣耀之刃单手骰固定加值应为 +3。");
        _test.Eq(equipped.weapon_two_handed_dice?.dice_count ?? 0, 1, "荣耀之刃双手骰应为 1D10+3。");
        _test.Eq(equipped.weapon_two_handed_dice?.dice_sides ?? 0, 10, "荣耀之刃双手骰应为 1D10+3。");
        _test.Eq(equipped.weapon_two_handed_dice?.flat_bonus ?? 0, 3, "荣耀之刃双手骰固定加值应为 +3。");
        AssertUnitHasTraitAndAbilitySource(equipped, CrowdGazeTraitId, CrowdGazeBindingId, "eq_glory_projection");
        AssertUnitHasTraitAndAbilitySource(equipped, GlorySpotlightTraitId, GlorySpotlightBindingId, "eq_glory_projection");
        AssertUnitHasTraitAndAbilitySource(equipped, CurtainCallTraitId, CurtainCallBindingId, "eq_glory_projection");
        AssertUnitHasTraitAndAbilitySource(equipped, LonelyDarkTraitId, LonelyDarkBindingId, "eq_glory_projection");
        AssertCurtainCallPayload(fixture.Bindings[CurtainCallBindingId]);
        AssertLonelyDarkPayload(fixture.Bindings[LonelyDarkBindingId]);

        equipped.GetEquipmentView().ClearSlot("main_hand");
        fixture.Runtime._unit_factory.RefreshBattleUnit(equipped);
        _test.Eq(equipped.weapon_item_id, new StringName(""), "移除荣耀之刃后 weapon_item_id 应清空。");
        _test.Eq(
            equipped.weapon_profile_type_id,
            baseline.weapon_profile_type_id,
            "移除荣耀之刃后 weapon_profile_type_id 应回到装备前状态。"
        );
        _test.Eq(equipped.equipment_ability_sources.Count, 0, "移除荣耀之刃后装备能力源应清空。");
    }

    private void TestCrowdGazeAndLonelyDarkUseAllNearbyLivingCreatures()
    {
        using GloryFixture fixture = GloryFixture.Build(new FixedRollDamageResolver(new GArray()));
        if (!fixture.Bindings.ContainsKey(CrowdGazeBindingId))
            return;
        BattleUnitState holder = fixture.BuildGloryUnit("crowd");
        BattleUnitState target = BuildEnemy("crowd_target", new Vector2I(1, 0));
        BattleUnitState enemyNear1 = BuildEnemy("crowd_enemy_1", new Vector2I(0, 1));
        BattleUnitState enemyNear2 = BuildEnemy("crowd_enemy_2", new Vector2I(3, 0));
        BattleUnitState allyNear = BuildAlly("crowd_ally", new Vector2I(0, 2));
        BattleState state = BuildState("glory_crowd", holder, target, enemyNear1, enemyNear2, allyNear);
        fixture.Runtime.SetupStateForTests(state);

        BattleAttackRollModifierBundle crowdBundle = BuildAttackBundle(
            fixture,
            holder,
            target,
            state,
            "glory_crowd_gaze"
        );
        _test.Eq(crowdBundle.GetEffectiveModifierDelta(), 3, "10 尺内 3 个存活生物时，众目睽睽应封顶 +3。");
        _test.True(HasModifierSum(crowdBundle, CrowdGazeBindingId, 3), "众目睽睽 +3 应在 modifier breakdown 中标明装备来源。");

        allyNear.MarkDead();
        fixture.Runtime._grid_service.ClearUnitOccupancy(state, allyNear);
        BattleAttackRollModifierBundle afterDeathBundle = BuildAttackBundle(
            fixture,
            holder,
            target,
            state,
            "glory_crowd_dead_ignored"
        );
        _test.Eq(afterDeathBundle.GetEffectiveModifierDelta(), 2, "死亡单位离场后，众目睽睽应只计 2 个存活生物。");

        BattleUnitState isolatedHolder = fixture.BuildGloryUnit("lonely");
        BattleUnitState isolatedTarget = BuildEnemy("lonely_target", new Vector2I(1, 0));
        BattleState lonelyState = BuildState("glory_lonely", isolatedHolder, isolatedTarget);
        fixture.Runtime.SetupStateForTests(lonelyState);
        BattleAttackRollModifierBundle lonelyBundle = BuildAttackBundle(
            fixture,
            isolatedHolder,
            isolatedTarget,
            lonelyState,
            "glory_lonely_dark"
        );
        _test.Eq(lonelyBundle.GetEffectiveModifierDelta(), -1, "只有一个 30 尺内生物时应同时有众目睽睽 +1 和孤独之暗 -2，净值 -1。");
        _test.True(HasModifier(lonelyBundle, LonelyDarkBindingId, -2), "孤独之暗 -2 应进入 modifier breakdown。");

        BattleUnitState farWitness = BuildAlly("far_witness", new Vector2I(7, 0));
        AddUnitToState(fixture.Runtime, lonelyState, farWitness);
        BattleAttackRollModifierBundle withFarWitness = BuildAttackBundle(
            fixture,
            isolatedHolder,
            isolatedTarget,
            lonelyState,
            "glory_lonely_far_witness"
        );
        _test.Eq(withFarWitness.GetEffectiveModifierDelta(), -1, "30 尺外生物不应解除孤独之暗。");
    }

    private void TestSpotlightAddsRadiantDamageAndLonelyDarkSubtractsDamage()
    {
        using GloryFixture crowdFixture = GloryFixture.Build(new FixedRollDamageResolver(new GArray { 1, 1, 4, 1 }));
        if (!crowdFixture.Bindings.ContainsKey(GlorySpotlightBindingId))
            return;
        BattleUnitState crowdHolder = crowdFixture.BuildGloryUnit("spotlight");
        BattleUnitState crowdTarget = BuildEnemy("spotlight_target", new Vector2I(1, 0), hp: 100);
        BattleState crowdState = BuildState(
            "glory_spotlight",
            crowdHolder,
            crowdTarget,
            BuildEnemy("spotlight_w1", new Vector2I(0, 2)),
            BuildEnemy("spotlight_w2", new Vector2I(2, 2)),
            BuildAlly("spotlight_w3", new Vector2I(3, 0)),
            BuildEnemy("spotlight_w4", new Vector2I(0, 3))
        );
        crowdFixture.Runtime.SetupStateForTests(crowdState);
        int spotlightDamage = IssueBasicAttackInCurrentState(
            crowdFixture.Runtime,
            crowdHolder,
            crowdTarget,
            "glory_spotlight_damage"
        );
        _test.Eq(spotlightDamage, 9, "5 个 30 尺内生物时应造成 1D8+3 加 2D6 radiant 固定伤害。");

        using GloryFixture plainFixture = GloryFixture.Build(new FixedRollDamageResolver(new GArray { 4, 1 }));
        BattleUnitState plainHolder = plainFixture.BuildGloryUnit("plain");
        BattleUnitState plainTarget = BuildEnemy("plain_target", new Vector2I(1, 0), hp: 100);
        plainHolder.equipment_ability_sources.Clear();
        plainFixture.Runtime.SetupStateForTests(BuildState("glory_plain", plainHolder, plainTarget));
        int plainDamage = IssueBasicAttackInCurrentState(
            plainFixture.Runtime,
            plainHolder,
            plainTarget,
            "glory_plain_damage"
        );
        _test.Eq(plainDamage, 7, "对照组荣耀之刃基础攻击固定骰应为 1D8+3。");

        using GloryFixture lonelyFixture = GloryFixture.Build(new FixedRollDamageResolver(new GArray { 6, 3 }));
        BattleUnitState lonelyHolder = lonelyFixture.BuildGloryUnit("lonely_damage");
        BattleUnitState lonelyTarget = BuildEnemy("lonely_damage_target", new Vector2I(1, 0), hp: 100);
        lonelyFixture.Runtime.SetupStateForTests(BuildState("glory_lonely_damage", lonelyHolder, lonelyTarget));
        int lonelyDamage = IssueBasicAttackInCurrentState(
            lonelyFixture.Runtime,
            lonelyHolder,
            lonelyTarget,
            "glory_lonely_damage"
        );
        _test.Eq(lonelyDamage, 6, "孤独之暗应从 1D8+3 中扣减 1D6 固定伤害。");
    }

    private void TestCurtainCallTriggersFreeWeaponAttackNearDefeatedEnemy()
    {
        using GloryFixture fixture = GloryFixture.Build(new FixedRollDamageResolver(new GArray { 5, 1 }));
        if (!fixture.Bindings.ContainsKey(CurtainCallBindingId))
            return;
        BattleUnitState holder = fixture.BuildGloryUnit("curtain");
        BattleUnitState defeated = BuildEnemy("curtain_defeated", new Vector2I(1, 0), hp: 0);
        BattleUnitState followTarget = BuildEnemy("curtain_follow_target", new Vector2I(2, 0), hp: 100);
        BattleUnitState farTarget = BuildEnemy("curtain_far_target", new Vector2I(5, 5), hp: 100);
        BattleState state = BuildState(
            "glory_curtain_call",
            holder,
            defeated,
            followTarget,
            farTarget,
            BuildAlly("curtain_w1", new Vector2I(0, 2)),
            BuildEnemy("curtain_w2", new Vector2I(2, 1)),
            BuildEnemy("curtain_w3", new Vector2I(3, 0)),
            BuildAlly("curtain_w4", new Vector2I(0, 3)),
            BuildEnemy("curtain_w5", new Vector2I(3, 3))
        );
        fixture.Runtime.SetupStateForTests(state);
        defeated.MarkDead();
        fixture.Runtime._grid_service.ClearUnitOccupancy(state, defeated);
        holder.current_ap = 1;

        using BattleEventBatch batch = new();
        fixture.Runtime.GetEquipmentAbilityRuntimeService().ResolveOnKill(
            new BattleEquipmentAbilityOnKillContext
            {
                SourceUnit = holder,
                DefeatedUnit = defeated,
                BattleState = state,
                Batch = batch,
            }
        );

        _test.Eq(holder.current_ap, 1, "谢幕斩应是无动作追击，不应消耗持有者 AP。");
        _test.True(followTarget.current_hp < 100, $"谢幕斩应攻击倒下目标 5 尺内的另一个敌人。 logs={JoinLogs(batch)}");
        _test.Eq(farTarget.current_hp, 100, "谢幕斩不应攻击倒下目标 5 尺外的敌人。");
        _test.True(ContainsStringName(batch.ChangedUnitIdsTyped, followTarget.unit_id), "谢幕斩应把追击目标写入 changed unit。");
    }

    private static void AssertCurtainCallPayload(EquipmentAbilityBindingDefinition binding)
    {
        EquipmentAbilityActionDefinition action = binding?.Reactions?[0]?.Actions?[0];
        if (action?.Kind != new StringName("immediate_weapon_attack"))
            throw new InvalidOperationException("谢幕斩 action kind 应为 immediate_weapon_attack。");
    }

    private static void AssertLonelyDarkPayload(EquipmentAbilityBindingDefinition binding)
    {
        IReadOnlyList<EquipmentAbilityActionDefinition> actions =
            binding?.Reactions?[0]?.Actions ?? Array.Empty<EquipmentAbilityActionDefinition>();
        if (actions.Count != 1 || actions[0]?.Kind != new StringName("attack_roll_bonus"))
            throw new InvalidOperationException("孤独之暗 before-hit 应配置 attack_roll_bonus。");
        IReadOnlyList<EquipmentAbilityActionDefinition> damageActions =
            binding?.Reactions?.Count > 1
                ? binding.Reactions[1]?.Actions ?? Array.Empty<EquipmentAbilityActionDefinition>()
                : Array.Empty<EquipmentAbilityActionDefinition>();
        if (damageActions.Count != 1 || damageActions[0]?.Kind != new StringName("add_damage_dice"))
            throw new InvalidOperationException("孤独之暗 after-hit 应配置可扣减的 add_damage_dice。");
        if (damageActions[0]?.PayloadDefinition is not AddDamageDiceActionPayloadDefinition dice)
            throw new InvalidOperationException("孤独之暗 damage action 应投影为 add_damage_dice payload。");
        if (!dice.Subtract)
            throw new InvalidOperationException("孤独之暗 damage payload 应声明 subtract=true。");
    }

    private static int IssueBasicAttackInCurrentState(
        BattleRuntimeModule runtime,
        BattleUnitState attacker,
        BattleUnitState target,
        StringName label
    )
    {
        WeaponAbilityCommandTestSupport.PrimeBasicAttack(attacker);
        ForceUnitActing(runtime?.GetState(), attacker);
        int beforeHp = target.current_hp;
        BattleCommand command = WeaponAbilityCommandTestSupport.BuildBasicAttackCommand(attacker, target);
        BattlePreview preview = runtime.PreviewCommand(command);
        if (preview?.allowed != true)
        {
            throw new InvalidOperationException(
                $"{label} basic_attack preview blocked: {JoinLogs(preview)}"
            );
        }
        BattleEventBatch batch = runtime.IssueCommand(command);
        if (batch == null)
            throw new InvalidOperationException($"{label} IssueCommand returned null.");
        return beforeHp - target.current_hp;
    }

    private static BattleAttackRollModifierBundle BuildAttackBundle(
        GloryFixture fixture,
        BattleUnitState attacker,
        BattleUnitState target,
        BattleState state,
        StringName traceSource
    )
    {
        BattleAttackCheckPolicyService attackPolicy = fixture.Runtime.GetAttackCheckPolicyService();
        SkillDefinition attackSkill = TestSkillDefinitionProjection.BuildSkill("fixture_basic_attack");
        return attackPolicy.BuildModifierBundle(
            attackPolicy.BuildSkillDefinitionAttackContext(
                state,
                attacker,
                target,
                attackSkill,
                "skill_attack_check",
                traceSource,
                force_hit_no_crit: false
            )
        );
    }

    private static BattleState BuildState(StringName battleId, params BattleUnitState[] units)
    {
        BattleUnitState holder = units != null && units.Length > 0 ? units[0] : null;
        BattleUnitState target = units != null && units.Length > 1 ? units[1] : null;
        BattleState state = WeaponAbilityCommandTestSupport.BuildFlatState(
            battleId,
            holder,
            target,
            mapSize: new Vector2I(10, 10)
        );
        for (int index = 2; index < (units?.Length ?? 0); index++)
        {
            AddUnitToState(null, state, units[index]);
        }
        return state;
    }

    private static BattleUnitState BuildEnemy(StringName unitId, Vector2I coord, int hp = 30) =>
        BuildUnit(unitId, "enemy", coord, hp);

    private static BattleUnitState BuildAlly(StringName unitId, Vector2I coord, int hp = 30) =>
        BuildUnit(unitId, "player", coord, hp);

    private static BattleUnitState BuildUnit(
        StringName unitId,
        StringName factionId,
        Vector2I coord,
        int hp
    )
    {
        BattleUnitState unit = new()
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
            control_mode = "manual",
            current_hp = hp,
            current_ap = 2,
            current_stamina = 30,
            is_alive = hp > 0,
        };
        unit.SetAnchorCoord(coord);
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, Math.Max(hp, 30));
        unit.attribute_snapshot.SetValue(AttributeService.ACTION_POINTS, 2);
        unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 20);
        unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 20);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 10);
        unit.creature_type_tags.Add("humanoid");
        return unit;
    }

    private static void AddUnitToState(
        BattleRuntimeModule runtime,
        BattleState state,
        BattleUnitState unit
    )
    {
        if (state == null || unit == null)
            return;
        state.SetUnit(unit);
        if (unit.faction_id == "player")
        {
            if (!state.ally_unit_ids.Contains(unit.unit_id))
                state.ally_unit_ids.Add(unit.unit_id);
        }
        else if (!state.enemy_unit_ids.Contains(unit.unit_id))
        {
            state.enemy_unit_ids.Add(unit.unit_id);
        }
        if (runtime != null)
        {
            if (!runtime._grid_service.PlaceUnit(state, unit, unit.coord, true))
                throw new InvalidOperationException($"unable to place unit {unit.unit_id} at {unit.coord}.");
            return;
        }
        SetUnitOccupants(state, unit);
    }

    private static void SetUnitOccupants(BattleState state, BattleUnitState unit)
    {
        if (state == null || unit == null)
            return;
        unit.RefreshFootprint();
        foreach (Vector2I coord in unit.occupied_coords)
            state.GetCell(coord)?.SetOccupant(unit.unit_id);
    }

    private static void ForceUnitActing(BattleState state, BattleUnitState unit)
    {
        if (state == null || unit == null)
            return;
        state.PhaseKind = BattlePhaseKind.UnitActing;
        state.active_unit_id = unit.unit_id;
    }

    private static bool HasModifier(
        BattleAttackRollModifierBundle bundle,
        StringName sourceId,
        int delta
    )
    {
        foreach (BattleAttackRollModifierSpec spec in bundle?.Breakdown ?? Array.Empty<BattleAttackRollModifierSpec>())
        {
            if (spec.source_id == sourceId && spec.modifier_delta == delta)
                return true;
        }
        return false;
    }

    private static bool HasModifierSum(
        BattleAttackRollModifierBundle bundle,
        StringName sourceId,
        int expectedTotal
    )
    {
        int total = 0;
        foreach (BattleAttackRollModifierSpec spec in bundle?.Breakdown ?? Array.Empty<BattleAttackRollModifierSpec>())
        {
            if (spec.source_id == sourceId)
                total += spec.modifier_delta;
        }
        return total == expectedTotal;
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
        if (source.SourceEquipmentInstanceId != expectedInstanceId)
            throw new InvalidOperationException(
                $"{bindingId} expected instance {expectedInstanceId}, got {source.SourceEquipmentInstanceId}."
            );
    }

    private static BattleEquipmentAbilitySourceState FindSource(
        BattleUnitState unit,
        StringName bindingId
    )
    {
        if (unit == null)
            return null;
        foreach (BattleEquipmentAbilitySourceState source in unit.equipment_ability_sources)
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

    private static string JoinLogs(BattleEventBatch batch) =>
        batch == null ? "" : string.Join(" | ", batch.LogLinesTyped);

    private static string JoinLogs(BattlePreview preview) =>
        preview == null ? "" : string.Join(" | ", preview.LogLinesTyped);

    private sealed class GloryFixture : IDisposable
    {
        private readonly ItemContentRegistry _itemRegistry;
        private readonly ProgressionContentRegistry _progressionRegistry;
        private readonly PartyState _partyState;

        private GloryFixture(
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

        internal static GloryFixture Build(BattleDamageResolver damageResolver)
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
            return new GloryFixture(itemRegistry, progressionRegistry, partyState, runtime);
        }

        internal BattleUnitState BuildGloryUnit(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            member.equipment_state.SetEquippedEntry(
                "main_hand",
                GloryItemId,
                new GStringNameArray { "main_hand" },
                EquipmentInstanceState.CreateInstance(GloryItemId, $"eq_glory_{label}")
            );
            IReadOnlyList<BattleUnitState> units =
                Runtime._unit_factory.BuildAllyUnits(_partyState, new GDictionary());
            if (units.Count != 1)
                throw new InvalidOperationException($"{label} scenario should build exactly one ally unit.");
            return units[0];
        }

        internal BattleUnitState BuildUnitWithoutWeapon(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            IReadOnlyList<BattleUnitState> units =
                Runtime._unit_factory.BuildAllyUnits(_partyState, new GDictionary());
            if (units.Count != 1)
                throw new InvalidOperationException($"{label} baseline should build exactly one ally unit.");
            return units[0];
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
            return partyState;
        }
    }
}
