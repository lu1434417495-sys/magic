using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;

public partial class run_storms_eye_weapon_ability_regression : LifecycleTestSceneTree
{
    private static readonly StringName StormsEyeItemId =
        "weapon_unique_axe_storms_eye_091";
    private static readonly StringName LightningEdgeTraitId =
        "weapon.axe.storms_eye.lightning_edge";
    private static readonly StringName ThunderRiftTraitId =
        "weapon.axe.storms_eye.thunder_rift";
    private static readonly StringName CloudsplitterTraitId =
        "weapon.axe.storms_eye.cloudsplitter_heavy_chop";
    private static readonly StringName LightningEdgeBindingId =
        "binding.weapon.axe.storms_eye.lightning_edge";
    private static readonly StringName ThunderRiftBindingId =
        "binding.weapon.axe.storms_eye.thunder_rift";
    private static readonly StringName CloudsplitterBindingId =
        "binding.weapon.axe.storms_eye.cloudsplitter_heavy_chop";
    private static readonly StringName CloudsplitterSkillId =
        "weapon_axe_storms_eye_cloudsplitter_heavy_chop";
    private static readonly StringName CloudsplitterGrantId =
        "grant.storms_eye.cloudsplitter_heavy_chop.skill";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestStormsEyeProjectsContentAndGrantsCloudsplitter();
            TestStormsEyeAddsLightningOnHitAndThunderOnCritical();
            TestCloudsplitterDealsExtraThunderWhenTargetIsNotPushed();
            RequestTestExit(_test.Finish("Storm's Eye weapon ability regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Storm's Eye weapon ability regression"));
        }
    }

    private void TestStormsEyeProjectsContentAndGrantsCloudsplitter()
    {
        using StormsEyeFixture fixture = StormsEyeFixture.Build(Array.Empty<int>());
        _test.True(fixture.ItemDefs.ContainsKey(StormsEyeItemId), "真实物品内容应包含风暴之眼。");
        _test.True(fixture.TraitDefs.ContainsKey(LightningEdgeTraitId), "真实 trait 应包含雷刃。");
        _test.True(fixture.TraitDefs.ContainsKey(ThunderRiftTraitId), "真实 trait 应包含雷鸣裂击。");
        _test.True(fixture.TraitDefs.ContainsKey(CloudsplitterTraitId), "真实 trait 应包含裂云重劈。");
        _test.True(
            fixture.Bindings.ContainsKey(LightningEdgeBindingId),
            "真实装备能力内容应包含雷刃 binding。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(ThunderRiftBindingId),
            "真实装备能力内容应包含雷鸣裂击 binding。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(CloudsplitterBindingId),
            "真实装备能力内容应包含裂云重劈 binding。"
        );
        _test.True(
            fixture.SkillDefs.ContainsKey(CloudsplitterSkillId),
            "裂云重劈应落成真实 SkillDef，而不是 trait 文本。"
        );

        using TestContentResourceLoader contentLoader = new();
        ItemDef rawItem = contentLoader.LoadCanonical<ItemDef>(
            "res://data/configs/items/weapon_unique_battleaxe_storms_eye.tres"
        );
        _test.True(rawItem != null, "风暴之眼原始资源应能加载。");
        if (rawItem != null)
        {
            _test.Eq(rawItem.display_name, "风暴之眼", "风暴之眼显示名应匹配设计源。");
            _test.Eq(
                rawItem.base_item_id,
                new StringName("weapon_type_battleaxe_base"),
                "风暴之眼应继承 battleaxe 模板。"
            );
            _test.Eq(rawItem.base_price, 50000, "风暴之眼基础价格应为 50000。");
            _test.True(rawItem.trait_ids.Contains(LightningEdgeTraitId), "风暴之眼应声明雷刃。");
            _test.True(rawItem.trait_ids.Contains(ThunderRiftTraitId), "风暴之眼应声明雷鸣裂击。");
            _test.True(rawItem.trait_ids.Contains(CloudsplitterTraitId), "风暴之眼应声明裂云重劈。");
            WeaponProfileDef rawProfile = rawItem.weapon_profile as WeaponProfileDef;
            _test.True(rawProfile != null, "风暴之眼应声明武器 profile override。");
            if (rawProfile != null)
            {
                _test.Eq(rawProfile.one_handed_dice?.dice_count ?? 0, 1, "风暴之眼单手应为 1D8+1。");
                _test.Eq(rawProfile.one_handed_dice?.dice_sides ?? 0, 8, "风暴之眼单手应为 1D8+1。");
                _test.Eq(rawProfile.one_handed_dice?.flat_bonus ?? 0, 1, "风暴之眼单手应为 1D8+1。");
                _test.True(
                    ContainsStringName(rawProfile.GetPropertiesTyped(), "versatile"),
                    "风暴之眼应声明 versatile 属性。"
                );
            }
        }

        if (fixture.SkillDefs.TryGetValue(CloudsplitterSkillId, out SkillDefinition skill))
        {
            AssertCloudsplitterSkillDefinition(skill, fixture);
        }

        BattleUnitState baseline = fixture.BuildUnitWithoutWeapon("baseline");
        BattleUnitState equipped = fixture.BuildStormsEyeUnit("projection");
        _test.Eq(equipped.weapon_item_id, StormsEyeItemId, "风暴之眼装备后 unit 应保留真实 item_id。");
        _test.Eq(equipped.weapon_profile_type_id, new StringName("battleaxe"), "风暴之眼应投影为 battleaxe。");
        _test.Eq(equipped.weapon_family, new StringName("axe"), "风暴之眼应投影为 axe。");
        _test.Eq(equipped.weapon_physical_damage_tag, new StringName("physical_slash"), "风暴之眼应为挥砍伤害。");
        _test.Eq(equipped.weapon_attack_range, 1, "风暴之眼攻击距离应为 1。");
        _test.True(equipped.weapon_is_versatile, "风暴之眼应保留 versatile 投影。");
        _test.Eq(equipped.weapon_one_handed_dice?.dice_count ?? 0, 1, "风暴之眼单手应为 1D8+1。");
        _test.Eq(equipped.weapon_one_handed_dice?.dice_sides ?? 0, 8, "风暴之眼单手应为 1D8+1。");
        _test.Eq(equipped.weapon_one_handed_dice?.flat_bonus ?? 0, 1, "风暴之眼单手应为 1D8+1。");
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            LightningEdgeTraitId,
            LightningEdgeBindingId,
            "eq_storms_eye_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            ThunderRiftTraitId,
            ThunderRiftBindingId,
            "eq_storms_eye_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            CloudsplitterTraitId,
            CloudsplitterBindingId,
            "eq_storms_eye_projection"
        );

        equipped.GetEquipmentView().ClearSlot("main_hand");
        fixture.Runtime._unit_factory.RefreshBattleUnit(equipped);
        _test.Eq(equipped.weapon_item_id, new StringName(""), "移除风暴之眼后 weapon_item_id 应清空。");
        _test.Eq(
            equipped.weapon_profile_type_id,
            baseline.weapon_profile_type_id,
            "移除风暴之眼后武器 profile 应回到装备前状态。"
        );
        _test.Eq(equipped.equipment_ability_sources.Count, 0, "移除风暴之眼后装备能力源应清空。");
        _test.False(equipped.effective_trait_ids.Contains(LightningEdgeTraitId), "移除后雷刃不应残留。");
        _test.False(equipped.effective_trait_ids.Contains(ThunderRiftTraitId), "移除后雷鸣裂击不应残留。");
        _test.False(equipped.effective_trait_ids.Contains(CloudsplitterTraitId), "移除后裂云重劈不应残留。");
        BattleTestFixture.DisposeBattleUnit(equipped);
        BattleTestFixture.DisposeBattleUnit(baseline);
    }

    private void TestStormsEyeAddsLightningOnHitAndThunderOnCritical()
    {
        using StormsEyeFixture fixture = StormsEyeFixture.Build(new[] { 4, 3 });
        BattleUnitState attacker = fixture.BuildStormsEyeUnit("hit");
        BattleUnitState target = BuildEnemy("storms_eye_hit_target", new Vector2I(1, 0), hp: 100);
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            attacker,
            target,
            "storms_eye_lightning_hit",
            previewCommand: false
        );
        int lightningDamage = 100 - target.current_hp;

        using StormsEyeFixture plainFixture = StormsEyeFixture.Build(new[] { 4, 3 });
        BattleUnitState plainAttacker = plainFixture.BuildStormsEyeUnit("plain_hit");
        plainAttacker.equipment_ability_sources.Clear();
        BattleUnitState plainTarget = BuildEnemy("storms_eye_plain_hit_target", new Vector2I(1, 0), hp: 100);
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            plainFixture.Runtime,
            plainAttacker,
            plainTarget,
            "storms_eye_plain_hit",
            previewCommand: false
        );
        int plainDamage = 100 - plainTarget.current_hp;

        _test.Eq(
            lightningDamage - plainDamage,
            3,
            "雷刃应在真实武器命中后额外造成固定骰 3 的 1D6 lightning。"
        );

        BattleTestFixture.ConfigureHitResolverForTests(
            fixture.Runtime,
            new FixedCriticalHitResolver()
        );
        BattleState state = WeaponAbilityCommandTestSupport.BuildFlatState(
            "storms_eye_critical_bonus_dice",
            attacker,
            target
        );
        fixture.Runtime.SetupStateForTests(state);
        IReadOnlyList<BattleEquipmentAbilityBonusDamageDiceResult> criticalDice =
            fixture.Runtime.GetEquipmentAbilityRuntimeService().CollectBonusDamageDiceOnHit(
                new BattleEquipmentAbilityBonusDamageDiceContext
                {
                    SourceUnit = attacker,
                    TargetUnit = target,
                    BattleState = state,
                    AttackSucceeded = true,
                    CriticalHit = true,
                }
            );
        _test.True(
            HasBonusDamageDice(criticalDice, LightningEdgeBindingId, 1, 6, "lightning"),
            "暴击命中仍应包含雷刃的 1D6 lightning。"
        );
        _test.True(
            HasBonusDamageDice(criticalDice, ThunderRiftBindingId, 2, 6, "thunder"),
            "雷鸣裂击应在暴击命中时加入 2D6 thunder。"
        );
    }

    private void TestCloudsplitterDealsExtraThunderWhenTargetIsNotPushed()
    {
        // 固定骰顺序：武器4、基础雷鸣3+3、雷刃2、未推动雷鸣5+5。
        using StormsEyeFixture movedFixture =
            StormsEyeFixture.Build(new[] { 4, 3, 3, 2, 5, 5 });
        BattleUnitState movedHolder = movedFixture.BuildStormsEyeUnit("cloudsplitter_moved");
        BattleUnitState movedTarget = BuildEnemy("cloudsplitter_moved_target", new Vector2I(1, 0), hp: 150);
        int movedDamage = IssueCloudsplitter(
            movedFixture,
            movedHolder,
            movedTarget,
            "storms_eye_cloudsplitter_moved",
            new Vector2I(4, 3)
        );
        _test.Eq(
            movedTarget.coord,
            new Vector2I(2, 0),
            "裂云重劈目标有合法退路时应被推动 1 格。"
        );

        using StormsEyeFixture blockedFixture =
            StormsEyeFixture.Build(new[] { 4, 3, 3, 2, 5, 5 });
        BattleUnitState blockedHolder = blockedFixture.BuildStormsEyeUnit("cloudsplitter_blocked");
        BattleUnitState blockedTarget = BuildEnemy("cloudsplitter_blocked_target", new Vector2I(1, 0), hp: 150);
        int blockedDamage = IssueCloudsplitter(
            blockedFixture,
            blockedHolder,
            blockedTarget,
            "storms_eye_cloudsplitter_blocked",
            new Vector2I(2, 3)
        );
        _test.Eq(
            blockedTarget.coord,
            new Vector2I(1, 0),
            "裂云重劈目标被地图边界挡住时不应位移。"
        );
        _test.Eq(
            blockedDamage,
            movedDamage + 10,
            "裂云重劈没有实际推动目标时，应额外结算固定骰 5+5 的 2D6 thunder。"
        );
        _test.Eq(blockedHolder.current_stamina, 55, "裂云重劈应消耗 45 体力。");
        _test.Eq(blockedHolder.GetCooldownTyped(CloudsplitterSkillId), 120, "裂云重劈应设置 120TU 冷却。");
    }

    private void AssertCloudsplitterSkillDefinition(
        SkillDefinition skill,
        StormsEyeFixture fixture
    )
    {
        CombatSkillDefinition combat = skill.CombatProfile;
        _test.True(combat != null, "裂云重劈技能应有 combat_profile。");
        if (combat == null)
            return;
        _test.Eq(combat.TargetMode, new StringName("unit"), "裂云重劈应选择单位目标。");
        _test.Eq(combat.TargetTeamFilter, new StringName("enemy"), "裂云重劈只能选择敌人。");
        _test.Eq(combat.RangeValue, 1, "裂云重劈只能攻击相邻 1 格敌人。");
        _test.Eq(combat.ApCost, 1, "裂云重劈应消耗 1AP。");
        _test.Eq(combat.StaminaCost, 45, "裂云重劈应消耗 45 体力。");
        _test.Eq(combat.CooldownTu, 120, "裂云重劈冷却应为 120TU。");
        _test.Eq(combat.AttackRollBonus, 2, "裂云重劈攻击检定应有 +2。");
        _test.Eq(
            combat.AttackResolutionModeKind,
            CombatSkillAttackResolutionMode.Auto,
            "裂云重劈应走攻击检定，不能配置成 direct_effect。"
        );
        _test.True(ContainsStringName(combat.RequiredWeaponFamilies, "axe"), "裂云重劈应要求 axe family。");
        _test.Eq(combat.EffectDefinitions.Count, 3, "裂云重劈应包含武器伤害、雷鸣伤害和推动三个 effect。");

        bool hasWeaponDamage = false;
        bool hasThunderDamage = false;
        bool hasPush = false;
        foreach (CombatEffectDefinition effect in combat.EffectDefinitions)
        {
            if (effect?.EffectKind == BattleEffectKind.Damage && effect.AddWeaponDice)
                hasWeaponDamage = true;
            if (
                effect?.EffectKind == BattleEffectKind.Damage
                && effect.DamageTag == "thunder"
                && effect.DiceCount == 2
                && effect.DiceSides == 6
            )
            {
                hasThunderDamage = true;
            }
            if (
                effect?.EffectKind == BattleEffectKind.ForcedMove
                && effect.ForcedMoveModeKind == BattleForcedMoveMode.Knockback
                && effect.ForcedMoveDistance == 1
            )
            {
                hasPush = true;
            }
        }
        _test.True(hasWeaponDamage, "裂云重劈应造成普通武器伤害。");
        _test.True(hasThunderDamage, "裂云重劈命中应额外造成 2D6 thunder。");
        _test.True(hasPush, "裂云重劈命中后应尝试推动 1 格。");

        _test.True(
            fixture.Bindings.TryGetValue(CloudsplitterBindingId, out EquipmentAbilityBindingDefinition binding),
            "裂云重劈 binding 应存在。"
        );
        if (binding != null)
        {
            _test.Eq(binding.GrantedActions.Count, 1, "裂云重劈 binding 应授予一个装备技能。");
            EquipmentGrantedActionDefinition grant =
                binding.GrantedActions.Count > 0 ? binding.GrantedActions[0] : null;
            _test.Eq(grant?.SkillId ?? new StringName(""), CloudsplitterSkillId, "裂云重劈 grant 应指向真实 SkillDef。");
            _test.Eq(grant?.GrantedActionId ?? new StringName(""), CloudsplitterGrantId, "裂云重劈 grant id 应稳定。");
            _test.Eq(
                grant?.UsagePeriodKind ?? EquipmentAbilityUsagePeriodKind.PerBattle,
                EquipmentAbilityUsagePeriodKind.None,
                "裂云重劈使用节奏应由技能冷却承担。"
            );
        }
    }

    private static int IssueCloudsplitter(
        StormsEyeFixture fixture,
        BattleUnitState holder,
        BattleUnitState target,
        StringName battleId,
        Vector2I mapSize
    )
    {
        PrimeCloudsplitterResources(holder);
        BattleState state = WeaponAbilityCommandTestSupport.BuildFlatState(
            battleId,
            holder,
            target,
            mapSize: mapSize
        );
        fixture.Runtime.SetupStateForTests(state);
        BattleAvailableSkillEntry entry = FindRequiredEquipmentSkill(
            fixture,
            holder,
            CloudsplitterSkillId,
            state
        );
        int hpBefore = target.current_hp;
        BattleCommand command = WeaponAbilityCommandTestSupport.BuildUnitSkillCommand(
            holder,
            target,
            entry,
            CloudsplitterSkillId
        );
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        if (preview?.allowed != true)
        {
            throw new InvalidOperationException(
                $"cloudsplitter preview blocked: {JoinLogs(preview)}"
            );
        }
        fixture.Runtime.IssueCommand(command);
        return hpBefore - target.current_hp;
    }

    private static BattleAvailableSkillEntry FindRequiredEquipmentSkill(
        StormsEyeFixture fixture,
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
        StormsEyeFixture fixture,
        BattleUnitState holder,
        BattleState state
    )
    {
        BattleSkillAvailabilityService availabilityService =
            new(fixture.SkillDefs, fixture.Bindings);
        return availabilityService.BuildView(
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

    private static void PrimeCloudsplitterResources(BattleUnitState unit)
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
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, hp);
        unit.SetEquipmentView(new EquipmentState());
        return unit;
    }

    private static bool HasBonusDamageDice(
        IEnumerable<BattleEquipmentAbilityBonusDamageDiceResult> results,
        StringName bindingId,
        int diceCount,
        int diceSides,
        StringName damageType
    )
    {
        foreach (BattleEquipmentAbilityBonusDamageDiceResult result in results ?? Array.Empty<BattleEquipmentAbilityBonusDamageDiceResult>())
        {
            if (
                result?.BindingId == bindingId
                && result.DiceCount == diceCount
                && result.DiceSides == diceSides
                && result.DamageType == damageType
            )
            {
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

    private static string JoinLogs(BattlePreview preview) =>
        string.Join(" | ", preview?.LogLinesTyped ?? Array.Empty<string>());

    private sealed class StormsEyeFixture : IDisposable
    {
        private readonly TestContentResourceLoader _contentLoader;
        private readonly ItemContentRegistry _itemRegistry;
        private readonly ProgressionContentRegistry _progressionRegistry;
        private readonly CharacterManagementModule _characterManagement;
        private readonly PartyState _partyState;
        private bool _disposed;

        private StormsEyeFixture(
            TestContentResourceLoader contentLoader,
            ItemContentRegistry itemRegistry,
            ProgressionContentRegistry progressionRegistry,
            CharacterManagementModule characterManagement,
            PartyState partyState,
            BattleRuntimeModule runtime
        )
        {
            _contentLoader = contentLoader;
            _itemRegistry = itemRegistry;
            _progressionRegistry = progressionRegistry;
            _characterManagement = characterManagement;
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

        internal static StormsEyeFixture Build(IEnumerable<int> damageRolls)
        {
            TestContentResourceLoader contentLoader = new();
            ItemContentRegistry itemRegistry = null;
            ProgressionContentRegistry progressionRegistry = null;
            CharacterManagementModule characterManagement = null;
            BattleRuntimeModule runtime = null;
            try
            {
                itemRegistry = new ItemContentRegistry(contentLoader);
                progressionRegistry = new ProgressionContentRegistry(contentLoader);
                PartyState partyState = BuildPartyState("hero");
                characterManagement = new CharacterManagementModule();
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

                runtime = new BattleRuntimeModule();
                runtime.setup(
                    characterManagement,
                    progressionRegistry.GetSkillDefinitionsTyped(),
                    item_defs: itemRegistry.GetItemDefsTyped(),
                    trait_defs: progressionRegistry.GetTraitDefsTyped(),
                    equipment_ability_bindings: progressionRegistry.GetEquipmentAbilityBindingDefinitionsTyped()
                );
                using GArray damageRollPayload = new();
                foreach (int roll in damageRolls ?? Array.Empty<int>())
                    damageRollPayload.Add(roll);
                BattleTestFixture.ConfigureDamageResolverForTests(
                    runtime,
                    new FixedRollDamageResolver(damageRollPayload)
                );
                BattleTestFixture.ConfigureHitResolverForTests(runtime, new FixedHitResolver(10));
                return new StormsEyeFixture(
                    contentLoader,
                    itemRegistry,
                    progressionRegistry,
                    characterManagement,
                    partyState,
                    runtime
                );
            }
            catch
            {
                BattleTestFixture.DisposeRuntime(runtime);
                characterManagement?.Dispose();
                itemRegistry?.Dispose();
                progressionRegistry?.Dispose();
                contentLoader.Dispose();
                throw;
            }
        }

        internal BattleUnitState BuildUnitWithoutWeapon(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            return BuildSingleAllyUnit(label);
        }

        internal BattleUnitState BuildStormsEyeUnit(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            member.equipment_state.SetEquippedEntry(
                "main_hand",
                StormsEyeItemId,
                new StringName[] { "main_hand" },
                EquipmentInstanceState.CreateInstance(
                    StormsEyeItemId,
                    $"eq_storms_eye_{label}"
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
            if (_disposed)
                return;
            _disposed = true;
            BattleTestFixture.DisposeBattleFixture(Runtime, Runtime?.GetState());
            _characterManagement?.Dispose();
            _itemRegistry?.Dispose();
            _progressionRegistry?.Dispose();
            _contentLoader?.Dispose();
        }

        private BattleUnitState BuildSingleAllyUnit(string label)
        {
            IReadOnlyList<BattleUnitState> units =
                Runtime._unit_factory.BuildAllyUnits(_partyState, null);
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
