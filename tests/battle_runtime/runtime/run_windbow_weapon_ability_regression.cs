using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_windbow_weapon_ability_regression : LifecycleTestSceneTree
{
    private static readonly StringName WindbowItemId = "weapon_unique_bow_windbow_151";
    private static readonly StringName WindArrowRangeTraitId =
        "weapon.bow.windbow.wind_arrow_range";
    private static readonly StringName WindWhisperTraitId =
        "weapon.bow.windbow.wind_whisper";
    private static readonly StringName WindGuidedShotTraitId =
        "weapon.bow.windbow.wind_guided_shot";
    private static readonly StringName GalePushTraitId =
        "weapon.bow.windbow.gale_push";
    private static readonly StringName WindGuidedShotBindingId =
        "binding.weapon.bow.windbow.wind_guided_shot";
    private static readonly StringName GalePushBindingId =
        "binding.weapon.bow.windbow.gale_push";
    private static readonly StringName GalePushSkillId = "weapon_bow_windbow_gale_push";
    private static readonly StringName GalePushGrantId = "grant.windbow.gale_push.skill";
    private static readonly StringName Perception = "perception";
    private static readonly StringName PerceptionModifier = "perception_modifier";
    private static readonly StringName WindPushSaveTag = "strength";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestWindbowContentProjectsTraitsModifierAndEquipmentSkill();
            TestWindGuidedShotAddsFullPerceptionModifierToBasicWeaponAttack();
            TestGalePushSkillConfigAndForcedMoveSaveGate();
            RequestTestExit(_test.Finish("Windbow weapon ability regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Windbow weapon ability regression"));
        }
    }

    private void TestWindbowContentProjectsTraitsModifierAndEquipmentSkill()
    {
        using WindbowFixture fixture = WindbowFixture.Build();
        _test.True(fixture.ItemDefs.ContainsKey(WindbowItemId), "真实物品内容应包含风之弓。");
        foreach (
            StringName traitId in new[]
            {
                WindArrowRangeTraitId,
                WindWhisperTraitId,
                WindGuidedShotTraitId,
                GalePushTraitId,
            }
        )
        {
            _test.True(fixture.TraitDefs.ContainsKey(traitId), $"风之弓应包含 trait {traitId}。");
        }
        _test.True(
            fixture.Bindings.ContainsKey(WindGuidedShotBindingId),
            "真实装备能力内容应包含风引射击 binding。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(GalePushBindingId),
            "真实装备能力内容应包含风压推射 binding。"
        );
        _test.True(
            fixture.SkillDefs.ContainsKey(GalePushSkillId),
            "风压推射应落成真实 SkillDef，而不是 trait 文本。"
        );

        ItemDef rawWindbow = ResourceLoader.Load<ItemDef>(
            "res://data/configs/items/weapon_unique_longbow_windbow.tres"
        );
        _test.True(rawWindbow != null, "风之弓原始资源应能加载。");
        if (rawWindbow != null)
        {
            _test.Eq(rawWindbow.item_id, WindbowItemId, "风之弓内部 item_id 应保留设计源 id。");
            _test.Eq(rawWindbow.display_name, "风之弓", "风之弓显示名应匹配方案。");
            _test.Eq(
                rawWindbow.base_item_id,
                new StringName("weapon_type_longbow_base"),
                "风之弓应继承 longbow 模板。"
            );
            _test.Eq(rawWindbow.base_price, 52000, "风之弓基础价格应为 52000。");
            _test.Eq(rawWindbow.trait_ids.Count, 4, "风之弓应有且只有 4 个已落地特性。");
            foreach (
                StringName traitId in new[]
                {
                    WindArrowRangeTraitId,
                    WindWhisperTraitId,
                    WindGuidedShotTraitId,
                    GalePushTraitId,
                }
            )
            {
                _test.True(rawWindbow.trait_ids.Contains(traitId), $"风之弓 item 应声明 {traitId}。");
            }

            WeaponProfileDef rawProfile = rawWindbow.weapon_profile as WeaponProfileDef;
            _test.True(rawProfile != null, "风之弓应声明 weapon_profile 覆写。");
            if (rawProfile != null)
            {
                _test.Eq(rawProfile.attack_range, 6, "风之弓攻击距离应为 6，而不是源文 10。");
                _test.Eq(rawProfile.two_handed_dice?.dice_count ?? 0, 1, "风之弓应为 1D8+2。");
                _test.Eq(rawProfile.two_handed_dice?.dice_sides ?? 0, 8, "风之弓应为 1D8+2。");
                _test.Eq(rawProfile.two_handed_dice?.flat_bonus ?? 0, 2, "风之弓应为 1D8+2。");
                _test.True(
                    ContainsStringName(rawProfile.GetPropertiesTyped(), "heavy"),
                    "风之弓应声明 heavy 属性。"
                );
            }
        }

        if (fixture.SkillDefs.TryGetValue(GalePushSkillId, out SkillDefinition skill))
        {
            AssertGalePushSkillDefinition(skill, fixture);
        }

        BattleUnitState baseline = fixture.BuildUnitWithoutWeapon("baseline", perception: 12);
        _test.Eq(baseline.attribute_snapshot.GetValue(Perception), 12, "基准单位感知应为 12。");
        _test.Eq(
            baseline.attribute_snapshot.GetValue(PerceptionModifier),
            1,
            "未装备风之弓时感知调整值应仅为基础 +1。"
        );

        BattleUnitState equipped = fixture.BuildWindbowUnit("projection", perception: 12);
        _test.Eq(equipped.weapon_item_id, WindbowItemId, "风之弓装备后 unit 应保留真实 item_id。");
        _test.Eq(equipped.weapon_profile_type_id, new StringName("longbow"), "风之弓应投影为 longbow。");
        _test.Eq(equipped.weapon_family, new StringName("bow"), "风之弓应投影为 bow family。");
        _test.Eq(equipped.weapon_attack_range, 6, "风之弓投影攻击距离应为 6。");
        _test.True(equipped.weapon_uses_two_hands, "风之弓应占用双手。");
        _test.Eq(equipped.weapon_two_handed_dice?.dice_count ?? 0, 1, "风之弓应投影 1D8+2。");
        _test.Eq(equipped.weapon_two_handed_dice?.dice_sides ?? 0, 8, "风之弓应投影 1D8+2。");
        _test.Eq(equipped.weapon_two_handed_dice?.flat_bonus ?? 0, 2, "风之弓应投影 1D8+2。");
        foreach (
            StringName traitId in new[]
            {
                WindArrowRangeTraitId,
                WindWhisperTraitId,
                WindGuidedShotTraitId,
                GalePushTraitId,
            }
        )
        {
            _test.True(equipped.effective_trait_ids.Contains(traitId), $"装备后应投影 {traitId}。");
        }
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            WindGuidedShotTraitId,
            WindGuidedShotBindingId,
            "eq_windbow_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            GalePushTraitId,
            GalePushBindingId,
            "eq_windbow_projection"
        );
        _test.Eq(
            equipped.attribute_snapshot.GetValue(Perception),
            12,
            "风语不应提高基础感知。"
        );
        _test.Eq(
            equipped.attribute_snapshot.GetValue(PerceptionModifier),
            4,
            "风语应通过装备 trait 给 perception_modifier +3。"
        );

        BattleSkillAvailabilityView view = BuildEquipmentSkillAvailability(fixture, equipped, null);
        _test.True(
            TryFindSkillEntry(view, GalePushSkillId, out BattleAvailableSkillEntry entry),
            "装备风之弓后，unit 的可用技能应包含风压推射。"
        );
        if (entry != null)
        {
            _test.Eq(entry.EntryRef.SourceKind, BattleSkillEntrySourceKind.EquipmentSkill, "风压推射来源应是 equipment_skill。");
            _test.Eq(entry.EquipmentBindingId, GalePushBindingId, "风压推射入口应携带 binding id。");
            _test.Eq(entry.EquipmentGrantedActionId, GalePushGrantId, "风压推射入口应携带 grant id。");
        }

        equipped.GetEquipmentView().ClearSlot("main_hand");
        fixture.Runtime._unit_factory.RefreshBattleUnit(equipped);
        _test.Eq(equipped.weapon_item_id, new StringName(""), "移除风之弓后 weapon_item_id 应清空。");
        _test.Eq(
            equipped.weapon_profile_type_id,
            baseline.weapon_profile_type_id,
            "移除风之弓后武器 profile 应恢复。"
        );
        _test.Eq(equipped.equipment_ability_sources.Count, 0, "移除风之弓后装备能力源应清空。");
        _test.Eq(
            equipped.attribute_snapshot.GetValue(PerceptionModifier),
            baseline.attribute_snapshot.GetValue(PerceptionModifier),
            "移除风之弓后 perception_modifier +3 不应残留。"
        );
    }

    private void TestWindGuidedShotAddsFullPerceptionModifierToBasicWeaponAttack()
    {
        using WindbowFixture fixture = WindbowFixture.Build();
        BattleUnitState attacker = fixture.BuildWindbowUnit("attack_bonus", perception: 12);
        BattleUnitState target = BuildTarget("wind_guided_target", new Vector2I(2, 0));
        SkillDefinition weaponAttackSkill = BuildWeaponAttackSkill();
        BattleAttackCheckPolicyService attackPolicy = fixture.Runtime.GetAttackCheckPolicyService();

        BattleAttackRollModifierBundle bundle = attackPolicy.BuildModifierBundle(
            attackPolicy.BuildSkillDefinitionAttackContext(
                null,
                attacker,
                target,
                weaponAttackSkill,
                "skill_attack_check",
                "windbow_guided_test",
                force_hit_no_crit: false
            )
        );
        _test.Eq(
            bundle.TotalBonus,
            4,
            "风引射击应把完整 perception_modifier=4 加到普通武器攻击检定。"
        );
        _test.True(
            HasModifier(bundle, WindGuidedShotBindingId, 4),
            "风引射击的 +4 应在 modifier breakdown 中标明装备能力来源。"
        );

        BattleUnitState unguided = fixture.BuildWindbowUnit("unguided", perception: 12);
        unguided.equipment_ability_sources.Clear();
        AttackCheckInput guidedCheck = attackPolicy.BuildAttackCheck(
            attackPolicy.BuildSkillDefinitionAttackContext(
                null,
                attacker,
                target,
                weaponAttackSkill,
                "skill_attack_check",
                "windbow_guided_test",
                force_hit_no_crit: false
            ),
            0,
            0
        );
        AttackCheckInput unguidedCheck = attackPolicy.BuildAttackCheck(
            attackPolicy.BuildSkillDefinitionAttackContext(
                null,
                unguided,
                target,
                weaponAttackSkill,
                "skill_attack_check",
                "windbow_unguided_test",
                force_hit_no_crit: false
            ),
            0,
            0
        );
        _test.Eq(guidedCheck.SituationalAttackBonus, 4, "风引射击应进入实际 AttackCheckInput。");
        _test.Eq(
            guidedCheck.RequiredRoll,
            unguidedCheck.RequiredRoll - 4,
            "风引射击应让命中所需点数降低完整 perception_modifier。"
        );
    }

    private void TestGalePushSkillConfigAndForcedMoveSaveGate()
    {
        using WindbowFixture fixture = WindbowFixture.Build();
        if (!fixture.SkillDefs.TryGetValue(GalePushSkillId, out SkillDefinition skill))
            return;
        CombatEffectDefinition pushEffect = FindForcedMoveEffect(skill);
        _test.True(pushEffect != null, "风压推射 SkillDef 应包含 forced_move effect。");
        if (pushEffect == null)
            return;

        BattleUnitState holder = fixture.BuildWindbowUnit("gale_push", perception: 12);
        holder.SetAnchorCoord(new Vector2I(1, 1));
        BattleUnitState target = BuildTarget("gale_push_target", new Vector2I(2, 1));
        BattleState state = WeaponAbilityCommandTestSupport.BuildFlatState(
            "windbow_gale_push",
            holder,
            target,
            mapSize: new Vector2I(6, 4)
        );
        fixture.Runtime.SetupStateForTests(state);

        BattleSkillAvailabilityView view = BuildEquipmentSkillAvailability(fixture, holder, state);
        _test.True(
            TryFindSkillEntry(view, GalePushSkillId, out BattleAvailableSkillEntry entry),
            "风压推射应能解析为装备技能入口。"
        );
        _test.True(entry?.IsSelectable == true, "资源充足且未冷却时风压推射应可选。");

        BattleStatusEffectState saveImmune = new()
        {
            status_id = "fixture_wind_push_immunity",
            duration = -1,
        };
        saveImmune.save_immunity_tags.Add(WindPushSaveTag);
        target.SetStatusEffect(saveImmune);

        int blockedSteps = fixture.Runtime._special_skill_resolver.ApplyForcedMoveEffect(
            holder,
            target,
            pushEffect,
            new BattleEventBatch(),
            BattleForcedMoveContext.Empty
        );
        _test.Eq(blockedSteps, 0, "目标对 wind_push save 免疫时，风压推射不应推动。");
        _test.Eq(target.coord, new Vector2I(2, 1), "save 成功或免疫后目标坐标不应变化。");

        target.EraseStatusEffect(saveImmune.status_id);
        CombatEffectDefinition plainKnockback = TestSkillDefinitionProjection.BuildEffect(
            "forced_move",
            forcedMoveMode: "knockback",
            forcedMoveDistance: 2
        );
        int movedSteps = fixture.Runtime._special_skill_resolver.ApplyForcedMoveEffect(
            holder,
            target,
            plainKnockback,
            new BattleEventBatch(),
            BattleForcedMoveContext.Empty
        );
        _test.Eq(movedSteps, 2, "未被 save gate 阻断时，合法强制位移应推动最多 2 格。");
        _test.Eq(target.coord, new Vector2I(4, 1), "推动方向应沿使用者到目标的主轴方向远离使用者。");
    }

    private void AssertGalePushSkillDefinition(
        SkillDefinition skill,
        WindbowFixture fixture
    )
    {
        CombatSkillDefinition combat = skill.CombatProfile;
        _test.True(combat != null, "风压推射技能应有 combat_profile。");
        if (combat == null)
            return;
        _test.Eq(combat.TargetMode, new StringName("unit"), "风压推射应选择单位。");
        _test.Eq(combat.TargetTeamFilter, new StringName("enemy"), "风压推射只能选择敌人。");
        _test.Eq(combat.RangeValue, 6, "风压推射射程应为 6。");
        _test.True(combat.RequiresLos, "风压推射应要求 LOS。");
        _test.Eq(combat.ApCost, 1, "风压推射应消耗 1AP。");
        _test.Eq(combat.StaminaCost, 12, "风压推射应消耗 12 体力。");
        _test.Eq(combat.CooldownTu, 60, "风压推射冷却必须是 60TU。");
        _test.Eq(
            combat.AttackResolutionMode,
            new StringName("direct_effect"),
            "风压推射不应走武器伤害或命中检定。"
        );
        _test.Eq(combat.EffectDefinitions.Count, 1, "风压推射应只有一个 forced_move effect。");

        CombatEffectDefinition effect = FindForcedMoveEffect(skill);
        _test.True(effect != null, "风压推射应声明 forced_move effect。");
        if (effect != null)
        {
            _test.Eq(effect.ForcedMoveMode, new StringName("knockback"), "风压推射应沿远离使用者方向推动。");
            _test.Eq(effect.ForcedMoveDistance, 2, "风压推射最多推动 2 格。");
            _test.Eq(effect.SaveDc, 14, "风压推射应使用 DC14。");
            _test.Eq(effect.SaveAbility, new StringName("strength"), "风压推射应使用 strength 调整值。");
            _test.Eq(effect.SaveTag, WindPushSaveTag, "风压推射应使用 wind_push save tag。");
        }

        _test.True(
            fixture.Bindings.TryGetValue(GalePushBindingId, out EquipmentAbilityBindingDefinition binding),
            "风压推射 binding 应存在。"
        );
        if (binding != null)
        {
            _test.Eq(binding.GrantedActions.Count, 1, "风压推射 binding 应授予一个装备技能入口。");
            if (binding.GrantedActions.Count > 0)
            {
                EquipmentGrantedActionDefinition grant = binding.GrantedActions[0];
                _test.Eq(grant.SkillId, GalePushSkillId, "风压推射 grant 应指向真实 SkillDef。");
                _test.Eq(grant.GrantedActionId, GalePushGrantId, "风压推射 grant id 应稳定。");
                _test.Eq(grant.UsagePeriodKind, EquipmentAbilityUsagePeriodKind.None, "风压推射使用次数由技能冷却承担。");
            }
        }
        _test.True(
            BindingHasDynamicAttackRollBonus(fixture.Bindings, WindGuidedShotBindingId),
            "风引射击必须由 attack_roll_bonus 的 attribute_modifier_id=perception_modifier 配置声明。"
        );
    }

    private static SkillDefinition BuildWeaponAttackSkill()
    {
        CombatEffectDefinition weaponDamage = TestSkillDefinitionProjection.BuildEffect(
            "damage",
            effectTargetTeamFilter: "enemy",
            requiresWeapon: true,
            addWeaponDice: true,
            useWeaponPhysicalDamageTag: true,
            resolveAsWeaponAttack: true
        );
        CombatSkillDefinition combat = TestSkillDefinitionProjection.BuildCombatProfile(
            "fixture_windbow_weapon_attack",
            effects: new[] { weaponDamage },
            targetMode: "unit",
            targetTeamFilter: "enemy",
            rangeValue: 6,
            attackResolutionMode: "auto"
        );
        return TestSkillDefinitionProjection.BuildSkill(
            "fixture_windbow_weapon_attack",
            combatProfile: combat
        );
    }

    private static CombatEffectDefinition FindForcedMoveEffect(SkillDefinition skill)
    {
        foreach (CombatEffectDefinition effect in skill?.CombatProfile?.EffectDefinitions ?? Array.Empty<CombatEffectDefinition>())
        {
            if (effect?.EffectKind == BattleEffectKind.ForcedMove)
                return effect;
        }
        return null;
    }

    private static BattleUnitState BuildTarget(StringName unitId, Vector2I coord)
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
        unit.attribute_snapshot.SetValue(AttributeService.STRENGTH_MODIFIER, 0);
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 30);
        unit.SetEquipmentView(new EquipmentState());
        return unit;
    }

    private static BattleSkillAvailabilityView BuildEquipmentSkillAvailability(
        WindbowFixture fixture,
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

    private static bool BindingHasDynamicAttackRollBonus(
        IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> bindings,
        StringName bindingId
    )
    {
        PropertyInfo attributeModifierIdProperty =
            typeof(AttackRollBonusActionPayloadDefinition).GetProperty("AttributeModifierId");
        if (attributeModifierIdProperty == null)
            return false;
        if (bindings == null || !bindings.TryGetValue(bindingId, out EquipmentAbilityBindingDefinition binding))
            return false;
        foreach (EquipmentAbilityReactionDefinition reaction in binding?.Reactions ?? Array.Empty<EquipmentAbilityReactionDefinition>())
        {
            foreach (EquipmentAbilityActionDefinition action in reaction.Actions ?? Array.Empty<EquipmentAbilityActionDefinition>())
            {
                if (action?.Kind != "attack_roll_bonus"
                    || action.PayloadDefinition is not AttackRollBonusActionPayloadDefinition payload)
                {
                    continue;
                }
                StringName attributeModifierId =
                    (StringName)(attributeModifierIdProperty.GetValue(payload) ?? new StringName(""));
                if (
                    attributeModifierId == PerceptionModifier
                    && payload.RequireWeaponDamage
                    && payload.TargetSelector == "attack_target"
                )
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static bool HasModifier(
        BattleAttackRollModifierBundle bundle,
        StringName sourceId,
        int modifierDelta
    )
    {
        foreach (BattleAttackRollModifierSpec spec in bundle?.Breakdown ?? Array.Empty<BattleAttackRollModifierSpec>())
        {
            if (
                spec.source_domain == "equipment_ability"
                && spec.source_id == sourceId
                && spec.modifier_delta == modifierDelta
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
        {
            if (value == expected)
                return true;
        }
        return false;
    }

    private sealed class WindbowFixture : IDisposable
    {
        private readonly ItemContentRegistry _itemRegistry;
        private readonly ProgressionContentRegistry _progressionRegistry;
        private readonly PartyState _partyState;

        private WindbowFixture(
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

        internal static WindbowFixture Build()
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
            runtime.ConfigureDamageResolverForTests(new FixedRollDamageResolver(new GArray { 4 }));
            runtime.ConfigureHitResolverForTests(new FixedHitResolver(10));
            return new WindbowFixture(itemRegistry, progressionRegistry, partyState, runtime);
        }

        internal BattleUnitState BuildUnitWithoutWeapon(string label, int perception)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            member.progression.unit_base_attributes.SetAttributeValue(Perception, perception);
            return BuildSingleAllyUnit(label);
        }

        internal BattleUnitState BuildWindbowUnit(string label, int perception)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            member.progression.unit_base_attributes.SetAttributeValue(Perception, perception);
            member.equipment_state.SetEquippedEntry(
                "main_hand",
                WindbowItemId,
                new GStringNameArray { "main_hand", "off_hand" },
                EquipmentInstanceState.CreateInstance(
                    WindbowItemId,
                    $"eq_windbow_{label}"
                )
            );
            BattleUnitState unit = BuildSingleAllyUnit(label);
            unit.SetAnchorCoord(Vector2I.Zero);
            unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 0);
            unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 0);
            unit.SetCombatResources(80, 0, 100, 0, 2, 2);
            unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 80);
            unit.attribute_snapshot.SetValue(AttributeService.STAMINA_MAX, 100);
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
            UnitProgress progress = new()
            {
                unit_id = memberId,
                display_name = memberId.ToString(),
            };
            foreach (
                StringName attributeId in new[]
                {
                    new StringName("strength"),
                    new StringName("agility"),
                    new StringName("constitution"),
                    new StringName("perception"),
                    new StringName("intelligence"),
                    new StringName("willpower"),
                }
            )
            {
                progress.unit_base_attributes.SetAttributeValue(attributeId, 10);
            }
            PartyMemberState memberState = new()
            {
                member_id = memberId,
                display_name = memberId.ToString(),
                progression = progress,
                equipment_state = new EquipmentState(),
            };
            partyState.SetMemberState(memberState);
            partyState.active_member_ids.Add(memberId);
            partyState.leader_member_id = memberId;
            return partyState;
        }
    }
}
