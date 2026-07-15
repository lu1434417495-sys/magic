using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_starfell_weapon_ability_regression : LifecycleTestSceneTree
{
    private static readonly StringName StarfellItemId =
        "weapon_unique_sword_starfell_016";
    private static readonly StringName MeteorForceTraitId =
        "weapon.sword.starfell.meteor_force";
    private static readonly StringName StarMapGuidanceTraitId =
        "weapon.sword.starfell.star_map_guidance";
    private static readonly StringName StarfallTraitId =
        "weapon.sword.starfell.starfall";
    private static readonly StringName CosmicDreadTraitId =
        "weapon.sword.starfell.cosmic_dread";
    private static readonly StringName StarfallSkillId =
        "weapon_sword_starfell_starfall";
    private static readonly StringName StarfallGrantId =
        "grant.starfell.starfall.skill";
    private static readonly StringName MeteorForceBindingId =
        "binding.weapon.sword.starfell.meteor_force";
    private static readonly StringName StarMapGuidanceBindingId =
        "binding.weapon.sword.starfell.star_map_guidance";
    private static readonly StringName StarfallBindingId =
        "binding.weapon.sword.starfell.starfall";
    private static readonly StringName CosmicDreadBindingId =
        "binding.weapon.sword.starfell.cosmic_dread";

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
            TestStarfellProjectsRealContentOntoBattleUnit();
            TestStarfallDefinesDualDamageSegmentsAndTargetMultiplier();
            TestStarfallResolverAppliesDualSegmentsAndDoubleTargets();
            RequestTestExit(_test.Finish("Starfell weapon ability regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Starfell weapon ability regression"));
        }
    }

    private void TestStarfellProjectsRealContentOntoBattleUnit()
    {
        using StarfellFixture fixture = StarfellFixture.Build();
        _test.True(fixture.ItemDefs.ContainsKey(StarfellItemId), "真实物品内容应包含群星之末。");
        _test.True(
            fixture.TraitDefs.ContainsKey(MeteorForceTraitId),
            "真实 trait 内容应包含陨星之力。"
        );
        _test.True(
            fixture.TraitDefs.ContainsKey(StarMapGuidanceTraitId),
            "真实 trait 内容应包含星图指引。"
        );
        _test.True(
            fixture.TraitDefs.ContainsKey(StarfallTraitId),
            "真实 trait 内容应包含星坠。"
        );
        _test.True(
            fixture.TraitDefs.ContainsKey(CosmicDreadTraitId),
            "真实 trait 内容应包含宇宙恐惧。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(MeteorForceBindingId),
            "真实装备能力内容应包含陨星之力 binding。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(StarMapGuidanceBindingId),
            "真实装备能力内容应包含星图指引 binding。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(StarfallBindingId),
            "真实装备能力内容应包含星坠 binding。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(CosmicDreadBindingId),
            "真实装备能力内容应包含宇宙恐惧 binding。"
        );
        _test.True(
            fixture.SkillDefs.ContainsKey(StarfallSkillId),
            "真实技能内容应包含星坠装备技能。"
        );
        if (!fixture.ItemDefs.ContainsKey(StarfellItemId))
            return;

        ItemDef rawStarfell = ResourceLoader.Load<ItemDef>(
            "res://data/configs/items/weapon_unique_greatsword_starfell.tres"
        );
        _test.True(rawStarfell != null, "群星之末原始资源应能加载。");
        if (rawStarfell != null)
        {
            _test.Eq(
                rawStarfell.base_item_id,
                new StringName("weapon_type_greatsword_base"),
                "群星之末原始资源应继承 greatsword 模板。"
            );
        }

        BattleUnitState equipped = fixture.BuildStarfellUnit("projection");
        _test.Eq(equipped.weapon_item_id, StarfellItemId, "群星之末装备后 unit 应保留真实 item_id。");
        _test.Eq(
            equipped.weapon_profile_type_id,
            new StringName("greatsword"),
            "群星之末应投影为 greatsword。"
        );
        _test.True(equipped.weapon_uses_two_hands, "群星之末应占用双手。");
        _test.Eq(
            equipped.weapon_two_handed_dice?.dice_count ?? 0,
            2,
            "群星之末双手骰数量应为 2。"
        );
        _test.Eq(
            equipped.weapon_two_handed_dice?.dice_sides ?? 0,
            6,
            "群星之末双手骰面应为 D6。"
        );
        _test.Eq(
            equipped.weapon_two_handed_dice?.flat_bonus ?? 0,
            4,
            "群星之末双手骰固定加值应为 +4。"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            MeteorForceTraitId,
            MeteorForceBindingId,
            "eq_starfell_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            StarMapGuidanceTraitId,
            StarMapGuidanceBindingId,
            "eq_starfell_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            StarfallTraitId,
            StarfallBindingId,
            "eq_starfell_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            CosmicDreadTraitId,
            CosmicDreadBindingId,
            "eq_starfell_projection"
        );
    }

    private void TestStarfallDefinesDualDamageSegmentsAndTargetMultiplier()
    {
        using StarfellFixture fixture = StarfellFixture.Build();
        _test.True(
            fixture.SkillDefs.TryGetValue(StarfallSkillId, out SkillDefinition starfall),
            "星坠应是 SkillDef，而不是 trait 自己承担主动动作。"
        );
        if (starfall == null)
            return;
        CombatSkillDefinition combat = starfall.CombatProfile;
        _test.True(combat != null, "星坠技能应有 combat_profile。");
        if (combat == null)
            return;

        _test.Eq(combat.TargetMode, new StringName("ground"), "星坠应使用地面目标。");
        _test.Eq(combat.TargetTeamFilter, new StringName("enemy"), "星坠应只影响敌方目标。");
        _test.Eq(combat.AreaPattern, new StringName("radius"), "星坠应使用当前系统 radius 范围。");
        _test.Eq(combat.AreaValue, 2, "星坠 10 尺半径应落成当前系统半径 2 格。");
        _test.Eq(combat.ApCost, 1, "星坠应消耗 1 AP。");
        _test.Eq(combat.EffectDefinitions.Count, 1, "星坠应使用一个 damage effect 承载同一次豁免。");

        CombatEffectDefinition damage = combat.EffectDefinitions.Count > 0
            ? combat.EffectDefinitions[0]
            : null;
        _test.True(damage != null, "星坠 damage effect 应存在。");
        if (damage == null)
            return;

        _test.Eq(damage.EffectType, new StringName("damage"), "星坠 effect 应是 damage。");
        _test.Eq(damage.DamageTag, new StringName("force"), "星坠主伤害段应是 force。");
        _test.Eq(damage.DiceCount, 3, "星坠 force 段应是 3D6。");
        _test.Eq(damage.DiceSides, 6, "星坠 force 段应是 3D6。");
        _test.Eq(damage.SaveDc, 16, "星坠豁免 DC 应是 16。");
        _test.Eq(damage.SaveAbility, new StringName("agility"), "星坠敏捷豁免应落成 agility。");
        _test.True(damage.SavePartialOnSuccess, "星坠豁免成功应减半。");
        _test.Eq(damage.ExtraDamageSegments.Count, 1, "星坠应额外声明一个 fire 伤害段。");

        CombatDamageSegmentDefinition fireSegment = damage.ExtraDamageSegments.Count > 0
            ? damage.ExtraDamageSegments[0]
            : null;
        _test.True(fireSegment != null, "星坠 fire 伤害段应存在。");
        if (fireSegment != null)
        {
            _test.Eq(fireSegment.DamageTag, new StringName("fire"), "星坠额外伤害段应是 fire。");
            _test.Eq(fireSegment.DiceCount, 3, "星坠 fire 段应是 3D6。");
            _test.Eq(fireSegment.DiceSides, 6, "星坠 fire 段应是 3D6。");
        }

        _test.Eq(
            damage.TargetDamageMultiplierRules.Count,
            2,
            "星坠应分别声明建筑与非魔法构造物双倍伤害规则。"
        );
        CombatTargetDamageMultiplierRuleDefinition buildingMultiplier =
            FindMultiplierRule(damage.TargetDamageMultiplierRules, "building");
        CombatTargetDamageMultiplierRuleDefinition constructMultiplier =
            FindMultiplierRule(damage.TargetDamageMultiplierRules, "construct");
        _test.True(buildingMultiplier != null, "星坠建筑目标倍率规则应存在。");
        _test.True(constructMultiplier != null, "星坠非魔法构造物目标倍率规则应存在。");
        if (buildingMultiplier != null)
        {
            _test.Eq(buildingMultiplier.MultiplierPercent, 200, "星坠建筑目标倍率应是 200%。");
            _test.False(
                ContainsStringName(buildingMultiplier.ExcludedCreatureTypeTags, "magical"),
                "星坠建筑目标倍率不应排除 magical 建筑。"
            );
        }
        if (constructMultiplier != null)
        {
            _test.Eq(constructMultiplier.MultiplierPercent, 200, "星坠构造物目标倍率应是 200%。");
            _test.True(
                ContainsStringName(constructMultiplier.ExcludedCreatureTypeTags, "magical"),
                "星坠目标倍率应排除 magical 构造物。"
            );
            _test.True(
                ContainsStringName(constructMultiplier.ExcludedCreatureTypeTags, "building"),
                "星坠构造物目标倍率应排除 building，避免双标签目标叠成4倍。"
            );
        }

        _test.True(
            fixture.Bindings.TryGetValue(StarfallBindingId, out EquipmentAbilityBindingDefinition binding),
            "星坠 binding 应存在。"
        );
        if (binding != null)
        {
            _test.Eq(binding.GrantedActions.Count, 1, "星坠 binding 应授予一个装备技能入口。");
            if (binding.GrantedActions.Count > 0)
            {
                EquipmentGrantedActionDefinition grant = binding.GrantedActions[0];
                _test.Eq(grant.SkillId, StarfallSkillId, "星坠 grant 应指向真实 SkillDef。");
                _test.Eq(grant.UsagePeriodKind, EquipmentAbilityUsagePeriodKind.PerBattle, "星坠应每场战斗1次。");
                _test.Eq(grant.MaxUsesPerPeriod, 1, "星坠应每场战斗1次。");
                _test.Eq(grant.GrantedActionId, StarfallGrantId, "星坠 grant id 应稳定。");
            }
        }
    }

    private void TestStarfallResolverAppliesDualSegmentsAndDoubleTargets()
    {
        using StarfellFixture fixture = StarfellFixture.Build();
        CombatEffectDefinition damage =
            fixture.SkillDefs[StarfallSkillId].CombatProfile.EffectDefinitions[0];

        DamageRun ordinary = ResolveStarfallDamage(damage, new[] { new StringName("humanoid") });
        _test.Eq(ordinary.DamageEvents.Length, 2, "普通目标应产生 force 与 fire 两个伤害事件。");
        _test.True(ordinary.HasDamageTag("force"), "星坠应结算 force 伤害事件。");
        _test.True(ordinary.HasDamageTag("fire"), "星坠应结算 fire 伤害事件。");
        _test.Eq(ordinary.TotalDamage, 15, "固定骰下普通目标应受到 3D6 force + 3D6 fire。");

        DamageRun construct = ResolveStarfallDamage(damage, new[] { new StringName("construct") });
        _test.Eq(construct.TotalDamage, ordinary.TotalDamage * 2, "非魔法构造物应受到星坠双倍伤害。");

        DamageRun building = ResolveStarfallDamage(damage, new[] { new StringName("building") });
        _test.Eq(building.TotalDamage, ordinary.TotalDamage * 2, "建筑应受到星坠双倍伤害。");

        DamageRun magicalBuilding = ResolveStarfallDamage(
            damage,
            new[] { new StringName("building"), new StringName("magical") }
        );
        _test.Eq(magicalBuilding.TotalDamage, ordinary.TotalDamage * 2, "魔法建筑仍应受到星坠双倍伤害。");

        DamageRun magicalConstruct = ResolveStarfallDamage(
            damage,
            new[] { new StringName("construct"), new StringName("magical") }
        );
        _test.Eq(
            magicalConstruct.TotalDamage,
            ordinary.TotalDamage,
            "带 magical 标签的构造物不应走非魔法构造物双倍伤害。"
        );
    }

    private static DamageRun ResolveStarfallDamage(
        CombatEffectDefinition effect,
        IReadOnlyList<StringName> targetTags
    )
    {
        FixedRollDamageResolver resolver = new(new GArray { 2, 2, 2, 3, 3, 3 });
        BattleUnitState source = BuildUnit("starfall_source", "player");
        BattleUnitState target = BuildUnit("starfall_target", "enemy");
        target.creature_type_tags.Clear();
        foreach (StringName tag in targetTags ?? Array.Empty<StringName>())
        {
            if (tag != "" && !target.creature_type_tags.Contains(tag))
                target.creature_type_tags.Add(tag);
        }
        AttackEffectResolutionResult result = resolver.ResolveEffects(
            source,
            target,
            new[] { effect },
            DamageResolutionContext.FromDictionary(
                new GDictionary { ["save_roll_override"] = 1 }
            )
        );
        return new DamageRun(
            result.DamageEvents ?? Array.Empty<DamageEventResult>(),
            result.Damage
        );
    }

    private static BattleUnitState BuildUnit(StringName unitId, StringName factionId)
    {
        BattleUnitState unit = new()
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
            control_mode = "manual",
            current_hp = 200,
            current_ap = 2,
            current_stamina = 20,
            is_alive = true,
        };
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 200);
        unit.attribute_snapshot.SetValue(AttributeService.ACTION_POINTS, 2);
        unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 10);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 0);
        unit.attribute_snapshot.SetValue("agility", 10);
        return unit;
    }

    private static bool ContainsStringName(IReadOnlyList<StringName> values, StringName needle)
    {
        if (needle == "")
            return false;
        foreach (StringName value in values ?? Array.Empty<StringName>())
        {
            if (value == needle)
                return true;
        }
        return false;
    }

    private static CombatTargetDamageMultiplierRuleDefinition FindMultiplierRule(
        IReadOnlyList<CombatTargetDamageMultiplierRuleDefinition> rules,
        StringName tag
    )
    {
        foreach (CombatTargetDamageMultiplierRuleDefinition rule in rules ?? Array.Empty<CombatTargetDamageMultiplierRuleDefinition>())
        {
            if (ContainsStringName(rule?.AnyCreatureTypeTags, tag))
                return rule;
        }
        return null;
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
        foreach (BattleEquipmentAbilitySourceState source in unit.equipment_ability_sources)
        {
            if (source?.AbilityIds?.Contains(bindingId) == true)
                return source;
        }
        return null;
    }

    private sealed record DamageRun(DamageEventResult[] DamageEvents, int TotalDamage)
    {
        internal bool HasDamageTag(StringName damageTag)
        {
            foreach (DamageEventResult damageEvent in DamageEvents ?? Array.Empty<DamageEventResult>())
            {
                if (damageEvent.DamageTag == damageTag)
                    return true;
            }
            return false;
        }
    }

    private sealed class StarfellFixture : IDisposable
    {
        private readonly CharacterManagementModule _characterManagement;
        private readonly PartyState _partyState;

        private StarfellFixture(
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

        internal static StarfellFixture Build()
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
            runtime.ConfigureDamageResolverForTests(new FixedRollDamageResolver(new GArray()));
            runtime.ConfigureHitResolverForTests(new FixedHitResolver(10));
            return new StarfellFixture(
                characterManagement,
                partyState,
                runtime,
                snapshot
            );
        }

        internal BattleUnitState BuildStarfellUnit(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            member.equipment_state.SetEquippedEntry(
                "main_hand",
                StarfellItemId,
                new GStringNameArray { "main_hand", "off_hand" },
                EquipmentInstanceState.CreateInstance(
                    StarfellItemId,
                    $"eq_starfell_{label}"
                )
            );
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

        public void Dispose()
        {
            Runtime?.dispose();
            _characterManagement?.Dispose();
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
