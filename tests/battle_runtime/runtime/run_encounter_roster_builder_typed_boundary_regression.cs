using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_encounter_roster_builder_typed_boundary_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestTypedEnemyUnitBuildMatchesPublicBoundary();
        TestEncounterBuilderUnlocksCasterMpResources();
        TestEnemyAttackEquipmentProjectsAbilitySourceAndCreatureTags();
        TestPlainLootPreviewMatchesTypedDefinitions();
        RequestTestExit(_test.Finish("Encounter roster builder typed boundary regression"));
    }

    private void TestTypedEnemyUnitBuildMatchesPublicBoundary()
    {
        using GameSession gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        using EncounterRosterBuilder builder = new();
        using BattleRuntimeModule runtime = new();
        builder.Setup(
            gameSession.GetBattleEncounterDefinitions(),
            gameSession.GetEncounterRosterDefinitions(),
            gameSession.GetEnemyTemplateDefinitions()
        );
        runtime.setup(
            null,
            gameSession.GetSkillDefinitionsTyped(),
            gameSession.GetEnemyTemplateDefinitions(),
            gameSession.GetEnemyAiBrainDefinitions(),
            builder,
            null,
            gameSession.GetItemDefsTyped()
        );

        EncounterAnchorData encounterAnchor = new()
        {
            entity_id = "mist_hollow_typed_boundary",
            display_name = "雾沼伏猎群",
            world_coord = new Vector2I(9, 9),
            faction_id = "hostile",
            region_tag = "south_wilds",
            vision_range = 2,
            encounter_kind = EncounterAnchorData.ToStringName(EncounterAnchorKind.Single),
            encounter_profile_id = "mist_hollow",
            growth_stage = 2,
            suppressed_until_step = 0,
        };

        using GodotProjectionLease<GArray> typedUnitsLease =
            builder.BuildEnemyUnitsFromDefinitionsLease(
            encounterAnchor,
            runtime.GetSkillDefinitionIndexTyped(),
            runtime.GetEnemyTemplateIndexTyped(),
            runtime.GetEnemyAiBrainIndexTyped(),
            runtime.BuildItemDefIndexSnapshotTyped()
        );
        using GodotProjectionLease<GArray> sessionUnitsLease = builder.BuildEnemyUnitsLease(
            encounterAnchor,
            gameSession.GetContentCatalogTyped().GetSkillDefinitionsTyped(),
            gameSession.GetEnemyTemplateDefinitions(),
            gameSession.GetEnemyAiBrainDefinitions(),
            gameSession.GetItemDefsTyped()
        );
        GArray typedUnits = typedUnitsLease.Value;
        GArray sessionUnits = sessionUnitsLease.Value;

        _test.Eq(typedUnits.Count, sessionUnits.Count, "不同 typed 输入源构建的 enemy unit 数量应一致。");
        _test.Eq(
            SummarizeUnits(typedUnits),
            SummarizeUnits(sessionUnits),
            "不同 typed 输入源构建的 enemy unit 结果应保持一致。"
        );
    }

    private void TestEncounterBuilderUnlocksCasterMpResources()
    {
        using GameSession gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        using EncounterRosterBuilder builder = new();
        builder.Setup(
            gameSession.GetBattleEncounterDefinitions(),
            gameSession.GetEncounterRosterDefinitions(),
            gameSession.GetEnemyTemplateDefinitions()
        );

        foreach (StringName templateId in new[] { new StringName("mist_beast"), new StringName("mist_weaver"), new StringName("wolf_shaman") })
        {
            BattleUnitState unit = BuildSingleTemplateUnit(builder, gameSession, templateId);
            _test.True(unit != null, $"{templateId} caster resource visibility 回归应能构建单位。");
            if (unit == null)
            {
                continue;
            }

            _test.True(unit.GetCurrentMp() > 0, $"{templateId} 模板应带有真实 MP 池。");
            _test.True(
                unit.HasCombatResourceUnlocked(CombatResourceIds.ToStringName(CombatResourceIdKind.Mp)),
                $"{templateId} 通过 EncounterRosterBuilder 入场时应解锁 MP 资源显示。"
            );
        }
    }

    private void TestEnemyAttackEquipmentProjectsAbilitySourceAndCreatureTags()
    {
        using EncounterRosterBuilder builder = new();
        StringName grantedSkillId = "enemy_flame_equipment_skill";
        ItemDef weapon = TestResourceOwnership.Own(
            MakeWeapon("enemy_flame_blade"),
            "EncounterRosterBuilderTypedBoundary.enemy_flame_blade"
        );
        weapon.trait_ids = new GStringNameArray { "trait.weapon.flame" };
        weapon.tags = new GStringNameArray { "blade" };
        ItemDefinition weaponDefinition = weapon.ToDefinition();
        var itemDefinitions = new Dictionary<StringName, ItemDefinition>
        {
            [weaponDefinition.ItemId] = weaponDefinition,
        };
        var traitDefs = new Dictionary<StringName, TraitDefinition>
        {
            ["trait.weapon.flame"] = new TraitDefinition(
                "trait.weapon.flame",
                "Flame Weapon",
                "Fixture trait.",
                [new StringName("weapon_feat")],
                [new StringName("equipment_fixed")],
                "halfling_luck",
                "on_natural_one",
                "stack_by_instance",
                "none",
                "none",
                "",
                0,
                0,
                System.Array.Empty<AttributeModifierDefinition>(),
                System.Array.Empty<StringName>(),
                System.Array.Empty<StringName>(),
                System.Array.Empty<StringName>(),
                System.Array.Empty<TraitDamageResistanceEntryDefinition>(),
                System.Array.Empty<TraitSaveBonusEntryDefinition>(),
                System.Array.Empty<TraitPassiveStatusEffectDefinition>(),
                System.Array.Empty<TraitRollValueSchemaEntryDefinition>()
            ),
        };
        var bindings = new Dictionary<StringName, EquipmentAbilityBindingDefinition>
        {
            ["binding.weapon.flame"] = new EquipmentAbilityBindingDefinition
            {
                BindingId = "binding.weapon.flame",
                TraitId = "trait.weapon.flame",
                AllowedSourceKinds = new HashSet<StringName> { "equipment_fixed" },
                RequiredTraitCategories = new HashSet<StringName> { "weapon_feat" },
                RequiredItemTags = new HashSet<StringName> { "blade" },
                SupportedEquipmentTypeIds = new HashSet<StringName> { "weapon" },
                GrantedActions = new EquipmentGrantedActionDefinition[]
                {
                    new()
                    {
                        GrantedActionId = "grant.enemy_flame_equipment_skill",
                        GrantedKind = EquipmentGrantedActionKind.Skill,
                        SkillId = grantedSkillId,
                        SkillLevel = 1,
                    },
                },
            },
        };
        EnemyTemplateDef template = BuildEnemyTemplate("flame_enemy", weapon.item_id);
        template.tags = new GStringNameArray { "undead" };
        var enemyTemplates = new Dictionary<StringName, EnemyTemplateDefinition>
        {
            [template.template_id] = template.ToDefinition(itemDefinitions),
        };
        SetupSingleTemplateEncounter(
            builder,
            "flame_enemy_encounter",
            template.template_id
        );

        using GodotProjectionLease<GArray> enemyUnitsLease =
            builder.BuildEnemyUnitsFromDefinitionsLease(
            BuildEncounterAnchor("flame_enemy_encounter", template.template_id),
            new Dictionary<StringName, SkillDefinition>(),
            enemyTemplates,
            new Dictionary<StringName, EnemyAiBrainDefinition>(),
            itemDefinitions,
            traitDefs: traitDefs,
            equipmentAbilityBindings: bindings
        );
        GArray enemyUnits = enemyUnitsLease.Value;

        _test.Eq(enemyUnits.Count, 1, "enemy template fixture 应生成一个敌方单位。");
        BattleUnitState unit = enemyUnits.Count > 0
            && BattleUnitState.TryReadUnitPayload(enemyUnits[0], out BattleUnitState parsed)
                ? parsed
                : null;
        _test.True(unit != null, "enemy template fixture 生成的 payload 应能读取为 BattleUnitState。");
        if (unit == null)
        {
            return;
        }

        template.tags = new GStringNameArray { "construct" };
        _test.True(
            BattleEquipmentAbilityProjectionService.UnitHasCreatureTypeTag(unit, "undead"),
            "creature type check 应读取 BattleUnitState.creature_type_tags，而不是回查敌人模板。"
        );
        _test.True(
            !BattleEquipmentAbilityProjectionService.UnitHasCreatureTypeTag(unit, "construct"),
            "模板后续变化不应改变已投影单位的 creature type check。"
        );
        _test.Eq(
            unit.GetEquipmentAbilitySourcesReadViewTyped().Count,
            1,
            "enemy attack equipment 应投影 battle-only equipment ability source。"
        );
        BattleEquipmentAbilitySourceReadView source =
            unit.GetEquipmentAbilitySourcesReadViewTyped()[0];
        _test.Eq(
            source.SourceKind,
            EquipmentAbilitySourceKind.EnemyBattleOnlyEquipment,
            "enemy equipment ability source 应标记为 battle-only。"
        );
        _test.Eq(
            source.SourceEquipmentInstanceId,
            new StringName(""),
            "enemy battle-only equipment source 不应携带持久装备 instance id。"
        );
        _test.Eq(
            source.EquipmentDefId,
            weapon.item_id,
            "enemy equipment ability source 应保留攻击装备 item id。"
        );
        _test.True(
            source.AbilityIds.Contains("binding.weapon.flame"),
            "enemy equipment ability source 应列出匹配绑定 id。"
        );

        SkillDefinition grantedSkill = TestSkillDefinitionProjection.BuildSkill(
            grantedSkillId,
            displayName: "enemy flame equipment skill",
            combatProfile: TestSkillDefinitionProjection.BuildCombatProfile(grantedSkillId)
        );
        BattleSkillAvailabilityService service = new(
            new Dictionary<StringName, SkillDefinition>
            {
                [grantedSkillId] = grantedSkill,
            },
            bindings
        );
        BattleSkillAvailabilityView view = service.BuildView(
            new BattleSkillAvailabilityQuery
            {
                User = unit,
                IncludeKnownSkills = false,
                IncludeEquipmentSkills = true,
                Consumer = BattleSkillAvailabilityConsumer.ManualSelection,
                WorldStep = 0,
            }
        );
        _test.True(
            TryFindSkillEntry(view, grantedSkillId, out BattleAvailableSkillEntry entry),
            "enemy battle-only equipment source 应生成装备技能入口。"
        );
        _test.True(entry?.IsSelectable == true, "enemy battle-only 装备技能首次应可用。");
        _test.True(
            EquipmentAbilityUsageRuntime.TryCommitUsage(unit, entry, worldStep: 0),
            "enemy battle-only 装备技能应能用 effective key 提交同回合使用。"
        );
        StringName expectedTurnUseKey = new(
            $"equipment_skill_turn_use:{source.EffectiveInstanceKey}:grant.enemy_flame_equipment_skill"
        );
        _test.True(
            unit.HasPerTurnChargeTyped(expectedTurnUseKey),
            $"enemy battle-only 装备技能应写入 effective key 同回合 charge。entry_key={entry?.EntryRef.SourceEquipmentEffectiveInstanceKey} source_key={source.EffectiveInstanceKey} charges={SummarizeCharges(unit)}"
        );
        BattleSkillAvailabilityView sameTurnView = service.BuildView(
            new BattleSkillAvailabilityQuery
            {
                User = unit,
                IncludeKnownSkills = false,
                IncludeEquipmentSkills = true,
                Consumer = BattleSkillAvailabilityConsumer.ManualSelection,
                WorldStep = 0,
            }
        );
        _test.True(
            TryFindSkillEntry(sameTurnView, grantedSkillId, out BattleAvailableSkillEntry sameTurnEntry),
            "enemy battle-only 装备技能提交后入口仍应可见。"
        );
        _test.False(sameTurnEntry?.IsSelectable ?? true, "enemy battle-only 装备技能同一行动回合不能再次使用。");
        _test.Eq(
            sameTurnEntry?.DisabledReason ?? new StringName(""),
            EquipmentAbilityUsageRuntime.PerActionTurnUseExhaustedReason,
            "enemy battle-only 装备技能同回合限制应使用 effective key 生效。"
        );
    }

    private void TestPlainLootPreviewMatchesTypedDefinitions()
    {
        using GameSession gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        using EncounterRosterBuilder builder = new();
        builder.Setup(
            gameSession.GetBattleEncounterDefinitions(),
            gameSession.GetEncounterRosterDefinitions(),
            gameSession.GetEnemyTemplateDefinitions()
        );

        EncounterAnchorData encounterAnchor = new()
        {
            entity_id = "wolf_den_typed_loot_boundary",
            display_name = "Wolf Den",
            world_coord = new Vector2I(4, 4),
            faction_id = "hostile",
            region_tag = "north_wilds",
            vision_range = 2,
            encounter_kind = EncounterAnchorData.ToStringName(EncounterAnchorKind.Settlement),
            encounter_profile_id = "wolf_den",
            growth_stage = 0,
            suppressed_until_step = 0,
        };

        IReadOnlyList<IReadOnlyDictionary<string, object>> plainLoot =
            builder.BuildLootEntriesPlain(encounterAnchor);
        IReadOnlyList<IReadOnlyDictionary<string, object>> explicitPlainLoot =
            builder.BuildLootEntriesPlain(
            encounterAnchor,
            gameSession.GetContentCatalogTyped().GetSkillDefinitionsTyped(),
            gameSession.GetEnemyTemplateDefinitions(),
            gameSession.GetEnemyAiBrainDefinitions(),
            gameSession.GetItemDefsTyped()
        );

        _test.Eq(plainLoot.Count, explicitPlainLoot.Count, "plain loot preview 数量应一致。");
        _test.Eq(
            SummarizeLoot(plainLoot),
            SummarizeLoot(explicitPlainLoot),
            "不同 typed definition 输入源的 plain loot preview 结果应保持一致。"
        );
    }

    private static string SummarizeUnits(GArray units)
    {
        List<string> values = new();
        foreach (Variant unitValue in units ?? new GArray())
        {
            if (!BattleUnitState.TryReadUnitPayload(unitValue, out BattleUnitState unit) || unit == null)
            {
                values.Add("null");
                continue;
            }
            BattleWeaponProjectionValues weapon =
                unit.GetWeaponProjectionReadViewTyped().Values;
            values.Add(
                $"{unit.enemy_template_id}|{unit.ai_brain_id}|{unit.ai_state_id}|{unit.display_name}|{weapon.ProfileKind}|{weapon.ItemId}|{unit.GetCurrentHp()}|{unit.GetCurrentStamina()}|{unit.GetKnownSkillLevelTyped("basic_attack", 0)}"
            );
        }
        return string.Join(" || ", values);
    }

    private static string SummarizeLoot(
        IReadOnlyList<IReadOnlyDictionary<string, object>> lootEntries
    )
    {
        List<string> values = new();
        foreach (
            IReadOnlyDictionary<string, object> entry in
            lootEntries ?? System.Array.Empty<IReadOnlyDictionary<string, object>>()
        )
        {
            if (entry == null)
            {
                values.Add("non_dict");
                continue;
            }
            values.Add(
                $"{PlainString(entry, "drop_source_kind")}|{PlainString(entry, "drop_source_id")}|{PlainString(entry, "drop_entry_id")}|{PlainString(entry, "item_id")}|{PlainInt(entry, "quantity", 0)}"
            );
        }
        return string.Join(" || ", values);
    }

    private static string PlainString(
        IReadOnlyDictionary<string, object> values,
        string key,
        string fallback = ""
    )
    {
        return values != null
            && values.TryGetValue(key, out object value)
            && value is string text
                ? text
                : fallback;
    }

    private static int PlainInt(
        IReadOnlyDictionary<string, object> values,
        string key,
        int fallback = 0
    )
    {
        return values != null
            && values.TryGetValue(key, out object value)
            && value is int number
                ? number
                : fallback;
    }

    private static bool TryFindSkillEntry(
        BattleSkillAvailabilityView view,
        StringName skillId,
        out BattleAvailableSkillEntry result
    )
    {
        result = null;
        foreach (BattleAvailableSkillEntry entry in view?.SkillEntries ?? new List<BattleAvailableSkillEntry>())
        {
            if (entry?.EntryRef.SkillId == skillId)
            {
                result = entry;
                return true;
            }
        }
        return false;
    }

    private static string SummarizeCharges(BattleUnitState unit)
    {
        List<string> values = new();
        foreach ((StringName key, int value) in unit?.GetPerTurnChargesTyped() ?? new Dictionary<StringName, int>())
            values.Add($"{key}:{value}");
        return string.Join(",", values);
    }

    private static BattleUnitState BuildSingleTemplateUnit(
        EncounterRosterBuilder builder,
        GameSession gameSession,
        StringName templateId
    )
    {
        StringName encounterProfileId = $"test_{templateId}_encounter";
        SetupSingleTemplateEncounter(builder, encounterProfileId, templateId);
        EncounterAnchorData encounterAnchor = new()
        {
            entity_id = $"{templateId}_typed_mp_unlock",
            display_name = templateId.ToString(),
            world_coord = new Vector2I(3, 3),
            faction_id = "hostile",
            region_tag = "typed_tests",
            vision_range = 2,
            encounter_kind = EncounterAnchorData.ToStringName(EncounterAnchorKind.Single),
            encounter_profile_id = encounterProfileId,
            growth_stage = 0,
            suppressed_until_step = 0,
        };

        using GodotProjectionLease<GArray> enemyUnitsLease = builder.BuildEnemyUnitsLease(
            encounterAnchor,
            gameSession.GetContentCatalogTyped().GetSkillDefinitionsTyped(),
            gameSession.GetEnemyTemplateDefinitions(),
            gameSession.GetEnemyAiBrainDefinitions(),
            gameSession.GetItemDefsTyped()
        );
        GArray enemyUnits = enemyUnitsLease.Value;
        return enemyUnits.Count > 0
            && BattleUnitState.TryReadUnitPayload(enemyUnits[0], out BattleUnitState unit)
                ? unit
                : null;
    }

    private static EncounterAnchorData BuildEncounterAnchor(StringName encounterId, StringName templateId)
    {
        return new EncounterAnchorData
        {
            entity_id = encounterId,
            display_name = templateId.ToString(),
            world_coord = new Vector2I(3, 3),
            faction_id = "hostile",
            region_tag = "typed_tests",
            vision_range = 2,
            encounter_kind = EncounterAnchorData.ToStringName(EncounterAnchorKind.Single),
            encounter_profile_id = encounterId,
            growth_stage = 0,
            suppressed_until_step = 0,
        };
    }

    private static void SetupSingleTemplateEncounter(
        EncounterRosterBuilder builder,
        StringName encounterProfileId,
        StringName templateId
    )
    {
        StringName rosterProfileId = $"{encounterProfileId}_roster";
        WildEncounterRosterDefinition roster = new(
            rosterProfileId,
            templateId.ToString(),
            0,
            1,
            new[]
            {
                new WildEncounterRosterStageDefinition(
                    0,
                    new[]
                    {
                        new WildEncounterRosterUnitEntryDefinition(
                            templateId,
                            1,
                            templateId.ToString()
                        ),
                    }
                ),
            }
        );
        BattleEncounterDefinition encounter = new(
            encounterProfileId,
            templateId.ToString(),
            rosterProfileId,
            BattleEliminationObjectiveDefinition.Instance,
            new BattleEncounterWorldResolutionDefinition(
                BattleWorldResolutionMode.Clear,
                BattleWorldResolutionMode.Preserve,
                BattleWorldResolutionMode.Preserve,
                0
            )
        );
        builder.Setup(
            new Dictionary<StringName, BattleEncounterDefinition>
            {
                [encounterProfileId] = encounter,
            },
            new Dictionary<StringName, WildEncounterRosterDefinition>
            {
                [rosterProfileId] = roster,
            },
            new Dictionary<StringName, EnemyTemplateDefinition>()
        );
    }

    private static EnemyTemplateDef BuildEnemyTemplate(
        StringName templateId,
        StringName attackEquipmentItemId
    )
    {
        return new EnemyTemplateDef
        {
            template_id = templateId,
            display_name = templateId.ToString(),
            brain_id = "",
            enemy_count = 1,
            body_size = BattleUnitState.BodySizeMedium,
            action_threshold = BattleUnitState.DefaultActionThreshold,
            attack_equipment_item_id = attackEquipmentItemId,
            skill_ids = new GStringNameArray(),
            base_attribute_overrides = new GDictionary
            {
                ["strength"] = 10,
                ["agility"] = 10,
                ["constitution"] = 10,
                ["perception"] = 10,
                ["intelligence"] = 10,
                ["willpower"] = 10,
            },
        };
    }

    private static ItemDef MakeWeapon(StringName itemId)
    {
        return new ItemDef
        {
            item_id = itemId,
            CategoryKind = ItemCategoryKind.Equipment,
            EquipmentTypeKind = ItemEquipmentTypeKind.Weapon,
            equipment_slot_ids = new Godot.Collections.Array<string> { "main_hand" },
            is_stackable = false,
            max_stack = 1,
            weapon_profile = new WeaponProfileDef
            {
                weapon_type_id = "shortsword",
                training_group = "martial",
                range_type = "melee",
                family = "sword",
                damage_tag = ItemDef.ToStringName(WeaponPhysicalDamageTagKind.Slash),
                attack_range = 1,
                one_handed_dice = new WeaponDamageDiceDef
                {
                    dice_count = 1,
                    dice_sides = 6,
                    flat_bonus = 0,
                },
            },
        };
    }

    private static int DictInt(GDictionary dictionary, string key, int fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
        {
            return fallback;
        }
        Variant value = dictionary[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static string DictString(GDictionary dictionary, string key)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
        {
            return "";
        }
        Variant value = dictionary[key];
        return value.VariantType == Variant.Type.Nil ? "" : value.ToString();
    }

}
