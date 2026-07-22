using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_enemy_template_runtime_start_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        try
        {
            TestFormalTemplatesResolveStableIds();
            TestWolfTemplatesSpawnWithPositiveStaminaPool();
            TestBattleStartUsesBuildContextItemDefsForEnemyWeaponProjection();
            TestEnemyTemplateSaveAdvantageTagsProjectToBattleUnit();
            TestEnemyTemplateDamageResistancesProjectToBattleUnit();
            TestEnemyTemplateDerivesHpAndAttackFromFormulaWhenNotOverridden();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }

        RequestTestExit(_test.Finish("Enemy template runtime start regression"));
    }

    private void TestFormalTemplatesResolveStableIds()
    {
        AssertTemplateStart(
            "encounter_wolf",
            "wolf_pack",
            "荒狼群",
            expectedEnemyCount: 2,
            expectedBrainId: "melee_aggressor",
            expectedStateId: "engage",
            requiredSkillIds: new[] { "basic_attack" }
        );
        AssertTemplateStart(
            "encounter_vanguard",
            "wolf_vanguard",
            "荒狼先锋",
            expectedEnemyCount: 1,
            expectedBrainId: "frontline_bulwark",
            expectedStateId: "engage",
            requiredSkillIds: new[] { "warrior_heavy_strike", "basic_attack" }
        );
        AssertTemplateStart(
            "encounter_harrier",
            "mist_harrier",
            "雾沼猎压者",
            expectedEnemyCount: 1,
            expectedBrainId: "ranged_suppressor",
            expectedStateId: "pressure",
            requiredSkillIds: new[] { "archer_suppressive_fire", "archer_pinning_shot" }
        );
        AssertTemplateStart(
            "encounter_weaver",
            "mist_weaver",
            "雾沼织咒者",
            expectedEnemyCount: 1,
            expectedBrainId: "healer_controller",
            expectedStateId: "pressure",
            requiredSkillIds: new[] { "mage_temporal_rewind", "mage_glacial_prison" }
        );
        AssertTemplateStart(
            "encounter_red_dragon",
            "red_dragon",
            "红龙",
            expectedEnemyCount: 1,
            expectedBrainId: "dragon_tyrant",
            expectedStateId: "engage",
            requiredSkillIds: new[] { "dragon_breath_fire_cone", "dragon_breath_fire_line", "basic_attack" }
        );
    }

    private void TestWolfTemplatesSpawnWithPositiveStaminaPool()
    {
        string[] templateIds =
        {
            "wolf_pack",
            "wolf_raider",
            "wolf_alpha",
            "wolf_vanguard",
        };
        foreach (string templateId in templateIds)
        {
            using BattleRuntimeScope runtimeScope = BuildRuntimeWithEnemyContent();
            BattleRuntimeModule runtime = runtimeScope.Runtime;
            BattleState state = null;
            try
            {
                state = StartTemplateBattle(
                    runtime,
                    $"encounter_{templateId}_stamina",
                    templateId,
                    templateId,
                    seed: 106
                );
                _test.True(
                    state != null && !state.IsEmpty(),
                    $"{templateId} 模板应能正式生成战斗状态。"
                );
                if (state == null || state.IsEmpty())
                {
                    continue;
                }
                _test.True(
                    state.enemy_unit_ids.Count > 0,
                    $"{templateId} 模板应至少生成一个敌方单位。"
                );
                foreach (StringName enemyUnitId in state.enemy_unit_ids)
                {
                    BattleUnitState enemyUnit = GetUnit(state, enemyUnitId);
                    _test.True(
                        enemyUnit != null,
                        $"{templateId} 模板生成的敌方单位应存在于 battle state 中。"
                    );
                    if (enemyUnit == null)
                    {
                        continue;
                    }
                    _test.True(
                        enemyUnit.attribute_snapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.StaminaMax)) > 0,
                        $"{templateId} 模板生成的敌方单位 stamina_max 应为正值。"
                    );
                    _test.True(
                        enemyUnit.current_stamina > 0,
                        $"{templateId} 模板生成的敌方单位 current_stamina 应为正值，避免技能链因资源池为 0 直接失效。"
                    );
                }
            }
            finally
            {
                runtime.SetupStateForTests(null);
                BattleTestFixture.DisposeBattleState(state);
            }
        }
    }

    private void TestBattleStartUsesBuildContextItemDefsForEnemyWeaponProjection()
    {
        using var gameSessionScope = new GameSessionScope();
        GameSession gameSession = gameSessionScope.Session;
        StringName templateId = "runtime_start_custom_enemy_template";
        var itemDefs = new Dictionary<StringName, ItemDefinition>(
            gameSession.GetItemDefsTyped()
        );
        ItemDefinition customWeapon = MakeWeapon(
            "runtime_start_custom_enemy_halberd",
            "halberd",
            "physical_pierce",
            2,
            MakeWeaponDice(1, 10, 1),
            MakeWeaponDice(1, 12, 1),
            new StringName[] { "reach", "versatile" }
        );
        itemDefs[customWeapon.ItemId] = customWeapon;

        var enemyTemplates = new Dictionary<StringName, EnemyTemplateDefinition>(
            gameSession.GetEnemyTemplateDefinitions()
        );
        EnemyTemplateDef customTemplate = BuildCustomEnemyTemplate(
            templateId,
            customWeapon.ItemId
        );
        enemyTemplates[templateId] = customTemplate.ToDefinition(itemDefs);

        using EncounterRosterBuilder encounterBuilder = BuildEncounterRosterBuilder(enemyTemplates);
        using var runtime = new BattleRuntimeModule();
        runtime.setup(
            null,
            gameSession.GetSkillDefinitionsTyped(),
            enemyTemplates,
            gameSession.GetEnemyAiBrainDefinitions(),
            encounterBuilder,
            null,
            itemDefs
        );
        runtime.ConfigureHitResolverForTests(new FixedHitResolver(10));

        BattleState state = null;
        try
        {
            state = StartTemplateBattle(
                runtime,
                "encounter_runtime_start_custom_weapon",
                templateId,
                "自定义敌方长戟兵",
                seed: 121
            );
            _test.True(state != null && !state.IsEmpty(), "自定义敌方模板应能正式生成战斗状态。");
            if (state == null || state.IsEmpty() || state.enemy_unit_ids.Count == 0)
            {
                return;
            }

            BattleUnitState enemyUnit = GetUnit(state, state.enemy_unit_ids[0]);
            _test.True(enemyUnit != null, "自定义敌方模板生成的单位应存在于 battle state 中。");
            if (enemyUnit == null)
            {
                return;
            }

            _test.Eq(
                enemyUnit.weapon_profile_kind,
                BattleUnitState.ToStringName(BattleWeaponProfileKind.Equipped),
                "敌方模板 attack_equipment_item_id 应使用 build context item_defs 投影正式武器，而不是回退成 unarmed。"
            );
            _test.Eq(
                enemyUnit.weapon_item_id,
                customWeapon.ItemId,
                "敌方模板 attack_equipment_item_id 应保留传入 item_defs 中的自定义武器 ID。"
            );
            _test.Eq(
                enemyUnit.weapon_attack_range,
                2,
                "敌方模板自定义武器射程应来自 build context item_defs。"
            );
            _test.Eq(
                enemyUnit.weapon_physical_damage_tag,
                new StringName("physical_pierce"),
                "敌方模板自定义武器伤害标签应来自 build context item_defs。"
            );
        }
        finally
        {
            runtime.SetupStateForTests(null);
            BattleTestFixture.DisposeBattleState(state);
        }
    }

    private void TestEnemyTemplateSaveAdvantageTagsProjectToBattleUnit()
    {
        using var gameSessionScope = new GameSessionScope();
        GameSession gameSession = gameSessionScope.Session;
        StringName templateId = "runtime_start_illusion_immune_enemy_template";
        var itemDefs = new Dictionary<StringName, ItemDefinition>(
            gameSession.GetItemDefsTyped()
        );
        ItemDefinition customWeapon = MakeWeapon(
            "runtime_start_illusion_immune_enemy_blade",
            "illusion_blade",
            "physical_slash",
            1,
            MakeWeaponDice(1, 6, 0),
            null,
            Array.Empty<StringName>()
        );
        itemDefs[customWeapon.ItemId] = customWeapon;

        EnemyTemplateDef template = BuildCustomEnemyTemplate(templateId, customWeapon.ItemId);
        template.save_immunity_tags = new GStringNameArray { "illusion" };
        var enemyTemplates = new Dictionary<StringName, EnemyTemplateDefinition>
        {
            [templateId] = template.ToDefinition(itemDefs),
        };
        using EncounterRosterBuilder builder = BuildEncounterRosterBuilder(enemyTemplates);
        EncounterAnchorData anchor = BuildEncounterAnchor(
            "encounter_runtime_start_illusion_immune_enemy",
            templateId,
            "幻象免疫敌人"
        );
        using GodotProjectionLease<GArray> enemyUnitsLease = builder.BuildEnemyUnitsLease(
            anchor,
            gameSession.GetContentCatalogTyped().GetSkillDefinitionsTyped(),
            enemyTemplates,
            gameSession.GetEnemyAiBrainDefinitions(),
            itemDefs
        );
        GArray enemyUnits = enemyUnitsLease.Value;
        _test.Eq(enemyUnits.Count, 1, "自定义敌方模板应生成一个敌方单位。");
        BattleUnitState enemyUnit = enemyUnits.Count > 0
            && BattleUnitState.TryReadUnitPayload(enemyUnits[0], out BattleUnitState parsedEnemyUnit)
                ? parsedEnemyUnit
                : null;
        _test.True(enemyUnit != null, "自定义敌方模板生成的单位应可读取。");
        if (enemyUnit == null)
        {
            return;
        }

        _test.True(
            enemyUnit.save_immunity_tags.Contains(new StringName("illusion")),
            "EnemyTemplateDef.save_immunity_tags 应投影到 BattleUnitState.save_immunity_tags。"
        );

        BattleSaveResult saveResult = BattleSaveResolver.ResolveSaveResult(
            null,
            enemyUnit,
            MakeIllusionSaveEffect(),
            BattleSaveContext.WithSaveRollOverride(1)
        );
        _test.True(
            saveResult.Immune,
            "投影出的 illusion 免疫标签应让 illusion 豁免在掷骰前免疫。"
        );
    }

    private void TestEnemyTemplateDerivesHpAndAttackFromFormulaWhenNotOverridden()
    {
        using var gameSessionScope = new GameSessionScope();
        GameSession gameSession = gameSessionScope.Session;
        StringName templateId = "runtime_start_formula_dragonling_template";
        var itemDefs = new Dictionary<StringName, ItemDefinition>(
            gameSession.GetItemDefsTyped()
        );

        EnemyTemplateDef template = TestResourceOwnership.Own(
            new EnemyTemplateDef
            {
                template_id = templateId,
                display_name = "公式幼龙",
                brain_id = "melee_aggressor",
                enemy_count = 1,
                body_size = BattleUnitState.BodySizeLarge,
                creature_level = 10,
                hit_die_sides = 12,
                action_threshold = BattleUnitState.DefaultActionThreshold,
                tags = new GStringNameArray { "dragon", "beast" },
                natural_weapon_damage_tag = "physical_pierce",
                natural_weapon_attack_range = 2,
                skill_ids = new GStringNameArray { "basic_attack" },
                base_attribute_overrides = new GDictionary
                {
                    ["strength"] = 18,
                    ["agility"] = 8,
                    ["constitution"] = 16,
                    ["perception"] = 12,
                    ["intelligence"] = 12,
                    ["willpower"] = 14,
                },
            },
            "EnemyTemplateRuntimeStart.BuildFormulaDragonling"
        );
        var enemyTemplates = new Dictionary<StringName, EnemyTemplateDefinition>
        {
            [templateId] = template.ToDefinition(itemDefs),
        };
        using EncounterRosterBuilder builder = BuildEncounterRosterBuilder(enemyTemplates);
        EncounterAnchorData anchor = BuildEncounterAnchor(
            "encounter_runtime_start_formula_dragonling",
            templateId,
            "公式幼龙"
        );
        using GodotProjectionLease<GArray> enemyUnitsLease = builder.BuildEnemyUnitsLease(
            anchor,
            gameSession.GetContentCatalogTyped().GetSkillDefinitionsTyped(),
            enemyTemplates,
            gameSession.GetEnemyAiBrainDefinitions(),
            itemDefs
        );
        GArray enemyUnits = enemyUnitsLease.Value;
        _test.Eq(enemyUnits.Count, 1, "公式幼龙模板应生成一个敌方单位。");
        BattleUnitState enemyUnit = enemyUnits.Count > 0
            && BattleUnitState.TryReadUnitPayload(enemyUnits[0], out BattleUnitState parsedEnemyUnit)
                ? parsedEnemyUnit
                : null;
        _test.True(enemyUnit != null, "公式幼龙生成的单位应可读取。");
        if (enemyUnit == null)
        {
            return;
        }

        _test.Eq(
            enemyUnit.current_hp,
            520,
            "无 hp_max override 时应按 首级满骰 + 后续均值 的公式派生出 520 HP(×4格)。"
        );
        var snapshot = enemyUnit.attribute_snapshot as AttributeSnapshot;
        _test.True(snapshot != null, "公式幼龙单位应携带 attribute snapshot。");
        if (snapshot != null)
        {
            _test.Eq(
                snapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.HpMax)),
                520,
                "快照 hp_max 应等于公式派生值 520。"
            );
            _test.Eq(
                snapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.AttackBonus)),
                4,
                "无 attack_bonus override 时近战应派生力量修正 +4。"
            );
        }
    }

    private void TestEnemyTemplateDamageResistancesProjectToBattleUnit()
    {
        using var gameSessionScope = new GameSessionScope();
        GameSession gameSession = gameSessionScope.Session;
        StringName templateId = "runtime_start_damage_resist_enemy_template";
        var itemDefs = new Dictionary<StringName, ItemDefinition>(
            gameSession.GetItemDefsTyped()
        );
        ItemDefinition customWeapon = MakeWeapon(
            "runtime_start_damage_resist_enemy_blade",
            "damage_resist_blade",
            "physical_slash",
            1,
            MakeWeaponDice(1, 6, 0),
            null,
            Array.Empty<StringName>()
        );
        itemDefs[customWeapon.ItemId] = customWeapon;

        EnemyTemplateDef template = BuildCustomEnemyTemplate(templateId, customWeapon.ItemId);
        template.damage_resistances = new GDictionary
        {
            [new StringName("physical_pierce")] = new StringName("half"),
            [new StringName("fire")] = new StringName("double"),
        };
        var enemyTemplates = new Dictionary<StringName, EnemyTemplateDefinition>
        {
            [templateId] = template.ToDefinition(itemDefs),
        };
        using EncounterRosterBuilder builder = BuildEncounterRosterBuilder(enemyTemplates);
        EncounterAnchorData anchor = BuildEncounterAnchor(
            "encounter_runtime_start_damage_resist_enemy",
            templateId,
            "抗性敌人"
        );
        using GodotProjectionLease<GArray> enemyUnitsLease = builder.BuildEnemyUnitsLease(
            anchor,
            gameSession.GetContentCatalogTyped().GetSkillDefinitionsTyped(),
            enemyTemplates,
            gameSession.GetEnemyAiBrainDefinitions(),
            itemDefs
        );
        GArray enemyUnits = enemyUnitsLease.Value;
        _test.Eq(enemyUnits.Count, 1, "自定义抗性敌方模板应生成一个敌方单位。");
        BattleUnitState enemyUnit = enemyUnits.Count > 0
            && BattleUnitState.TryReadUnitPayload(enemyUnits[0], out BattleUnitState parsedEnemyUnit)
                ? parsedEnemyUnit
                : null;
        _test.True(enemyUnit != null, "自定义抗性敌方模板生成的单位应可读取。");
        if (enemyUnit == null)
        {
            return;
        }

        _test.Eq(
            enemyUnit.damage_resistances.Get(new StringName("physical_pierce")),
            new StringName("half"),
            "EnemyTemplateDef.damage_resistances 应投影到 BattleUnitState.damage_resistances。"
        );
        _test.Eq(
            enemyUnit.damage_resistances.Get(new StringName("fire")),
            new StringName("double"),
            "EnemyTemplateDef.damage_resistances 易伤条目应投影到 BattleUnitState.damage_resistances。"
        );
    }

    private void AssertTemplateStart(
        StringName encounterId,
        StringName templateId,
        string displayName,
        int expectedEnemyCount,
        StringName expectedBrainId,
        StringName expectedStateId,
        string[] requiredSkillIds
    )
    {
        using BattleRuntimeScope runtimeScope = BuildRuntimeWithEnemyContent();
        BattleRuntimeModule runtime = runtimeScope.Runtime;
        BattleState state = null;
        try
        {
            state = StartTemplateBattle(runtime, encounterId, templateId, displayName, seed: 101);
            _test.True(
                state != null && !state.IsEmpty(),
                $"{templateId} 正式 battle start 应能创建基于敌方模板的战斗状态。"
            );
            if (state == null || state.IsEmpty())
            {
                return;
            }
            _test.Eq(
                state.enemy_unit_ids.Count,
                expectedEnemyCount,
                $"{templateId} 模板生成的敌方单位数量应符合配置。"
            );
            if (state.enemy_unit_ids.Count == 0)
            {
                return;
            }
            BattleUnitState enemyUnit = GetUnit(state, state.enemy_unit_ids[0]);
            _test.True(
                enemyUnit != null,
                $"{templateId} 模板生成的首个敌方单位应存在于 battle state 中。"
            );
            if (enemyUnit == null)
            {
                return;
            }
            _test.Eq(
                enemyUnit.ai_brain_id,
                expectedBrainId,
                $"{templateId} 应绑定 {expectedBrainId} brain，而不是回落到默认敌人。"
            );
            _test.Eq(
                enemyUnit.ai_state_id,
                expectedStateId,
                $"{templateId} 应写入 {expectedStateId} 初始 AI 状态。"
            );
            foreach (string skillId in requiredSkillIds)
            {
                _test.True(
                    enemyUnit.known_active_skill_ids.Contains(new StringName(skillId)),
                    $"{templateId} 模板应为敌人注入 {skillId} 技能。"
                );
            }
        }
        finally
        {
            runtime.SetupStateForTests(null);
            BattleTestFixture.DisposeBattleState(state);
        }
    }

    private static BattleRuntimeScope BuildRuntimeWithEnemyContent()
    {
        var gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        IReadOnlyDictionary<StringName, EnemyTemplateDefinition> enemyTemplates =
            gameSession.GetEnemyTemplateDefinitions();
        EncounterRosterBuilder encounterBuilder = BuildEncounterRosterBuilder(enemyTemplates);
        var runtime = new BattleRuntimeModule();
        try
        {
            runtime.setup(
                null,
                gameSession.GetSkillDefinitionsTyped(),
                enemyTemplates,
                gameSession.GetEnemyAiBrainDefinitions(),
                encounterBuilder
            );
            runtime.ConfigureHitResolverForTests(new FixedHitResolver(10));
            return new BattleRuntimeScope(runtime, encounterBuilder, gameSession);
        }
        catch
        {
            runtime.Dispose();
            encounterBuilder.Dispose();
            gameSession.Dispose();
            throw;
        }
    }

    private static EncounterRosterBuilder BuildEncounterRosterBuilder(
        IReadOnlyDictionary<StringName, EnemyTemplateDefinition> enemyTemplates
    )
    {
        var encounters = new Dictionary<StringName, BattleEncounterDefinition>();
        var rosters = new Dictionary<StringName, WildEncounterRosterDefinition>();
        if (enemyTemplates != null)
        {
            foreach (
                KeyValuePair<StringName, EnemyTemplateDefinition> entry in enemyTemplates
            )
            {
                StringName templateId = entry.Key;
                EnemyTemplateDefinition template = entry.Value;
                if (templateId == "" || template == null)
                {
                    continue;
                }

                StringName rosterProfileId = BuildRosterProfileId(templateId);
                StringName encounterProfileId = BuildEncounterProfileId(templateId);
                rosters[rosterProfileId] = new WildEncounterRosterDefinition(
                    rosterProfileId,
                    template.DisplayName,
                    0,
                    0,
                    new[]
                    {
                        new WildEncounterRosterStageDefinition(
                            0,
                            new[]
                            {
                                new WildEncounterRosterUnitEntryDefinition(
                                    templateId,
                                    Mathf.Max(template.EnemyCount, 1),
                                    template.DisplayName
                                ),
                            }
                        ),
                    }
                );
                encounters[encounterProfileId] = new BattleEncounterDefinition(
                    encounterProfileId,
                    template.DisplayName,
                    rosterProfileId,
                    BattleEliminationObjectiveDefinition.Instance,
                    new BattleEncounterWorldResolutionDefinition(
                        BattleWorldResolutionMode.Clear,
                        BattleWorldResolutionMode.Preserve,
                        BattleWorldResolutionMode.Preserve,
                        0
                    )
                );
            }
        }

        var builder = new EncounterRosterBuilder();
        builder.Setup(encounters, rosters, enemyTemplates);
        return builder;
    }

    private static StringName BuildEncounterProfileId(StringName templateId) =>
        new($"test_enemy_template_runtime_start_encounter_{templateId}");

    private static StringName BuildRosterProfileId(StringName templateId) =>
        new($"test_enemy_template_runtime_start_roster_{templateId}");

    private static BattleState StartTemplateBattle(
        BattleRuntimeModule runtime,
        StringName encounterId,
        StringName templateId,
        string displayName,
        int seed
    )
    {
        EncounterAnchorData anchor = BuildEncounterAnchor(encounterId, templateId, displayName);
        return runtime.StartBattle(
            anchor,
            seed,
            BattleEliminationObjectiveDefinition.Instance,
            new GDictionary
            {
                ["ally_member_ids"] = new GStringNameArray { "ally_a", "ally_b" },
                ["default_active_skill_ids"] = new GStringNameArray { "warrior_heavy_strike" },
                ["validate_spawn_reachability"] = false,
            }
        );
    }

    private static EncounterAnchorData BuildEncounterAnchor(
        StringName encounterId,
        StringName templateId,
        string displayName
    )
    {
        return new EncounterAnchorData
        {
            entity_id = encounterId,
            display_name = displayName,
            world_coord = Vector2I.Zero,
            faction_id = "hostile",
            region_tag = "mistwood",
            vision_range = 4,
            encounter_kind = EncounterAnchorData.ToStringName(EncounterAnchorKind.Single),
            encounter_profile_id = BuildEncounterProfileId(templateId),
        };
    }

    private static CombatEffectDefinition MakeIllusionSaveEffect() =>
        TestSkillDefinitionProjection.BuildEffect(
            "damage",
            saveDcMode: BattleSaveContentRules.ToStringName(BattleSaveDcMode.Static),
            saveDc: 12,
            saveAbility: BattleSaveContentRules.ToStringName(BattleSaveTagKind.Willpower),
            saveTag: BattleSaveContentRules.ToStringName(BattleSaveTagKind.Illusion)
        );

    private static BattleUnitState GetUnit(BattleState state, StringName unitId)
    {
        return state != null && state.TryGetUnitTyped(unitId, out BattleUnitState unitState)
            ? unitState
            : null;
    }

    private static EnemyTemplateDef BuildCustomEnemyTemplate(
        StringName templateId,
        StringName attackEquipmentItemId
    )
    {
        return TestResourceOwnership.Own(
            new EnemyTemplateDef
            {
                template_id = templateId,
                display_name = "自定义敌方长戟兵",
                brain_id = "melee_aggressor",
                enemy_count = 1,
                body_size = BattleUnitState.BodySizeMedium,
                action_threshold = BattleUnitState.DefaultActionThreshold,
                attack_equipment_item_id = attackEquipmentItemId,
                tags = new GStringNameArray(),
                skill_ids = new GStringNameArray { "basic_attack" },
                base_attribute_overrides = new GDictionary
                {
                    ["strength"] = 10,
                    ["agility"] = 10,
                    ["constitution"] = 10,
                    ["perception"] = 10,
                    ["intelligence"] = 10,
                    ["willpower"] = 10,
                },
            },
            "EnemyTemplateRuntimeStart.BuildCustomEnemyTemplate"
        );
    }

    private static void SetSaveAdvantageTags(
        EnemyTemplateDef template,
        params StringName[] saveAdvantageTags
    )
    {
        var tags = new GStringNameArray();
        foreach (StringName tag in saveAdvantageTags ?? Array.Empty<StringName>())
        {
            tags.Add(tag);
        }

        var property = typeof(EnemyTemplateDef).GetProperty("save_advantage_tags");
        property?.SetValue(template, tags);
    }

    private static ItemDefinition MakeWeapon(
        StringName itemId,
        StringName weaponTypeId,
        StringName damageTag,
        int attackRange,
        WeaponDamageDiceDef oneHandedDice,
        WeaponDamageDiceDef twoHandedDice,
        StringName[] properties
    )
    {
        var itemDef = new ItemDef
        {
            item_id = itemId,
            CategoryKind = ItemCategoryKind.Equipment,
            EquipmentTypeKind = ItemEquipmentTypeKind.Weapon,
            equipment_slot_ids = new Godot.Collections.Array<string> { "main_hand" },
            is_stackable = false,
            max_stack = 1,
        };
        var profile = new WeaponProfileDef
        {
            weapon_type_id = weaponTypeId,
            training_group = "martial",
            range_type = "melee",
            family = "polearm",
            damage_tag = damageTag,
            attack_range = attackRange,
            one_handed_dice = oneHandedDice,
            two_handed_dice = twoHandedDice,
            properties_mode = (int)WeaponProfileDef.PropertyMergeMode.REPLACE,
        };
        foreach (StringName property in properties ?? Array.Empty<StringName>())
        {
            if (property != "")
            {
                profile.properties.Add(property);
            }
        }
        itemDef.weapon_profile = profile;
        return TestResourceOwnership
            .Own(itemDef, "EnemyTemplateRuntimeStart.MakeWeapon")
            .ToDefinition();
    }

    private static WeaponDamageDiceDef MakeWeaponDice(int count, int sides, int bonus)
    {
        return new WeaponDamageDiceDef
        {
            dice_count = count,
            dice_sides = sides,
            flat_bonus = bonus,
        };
    }

    private sealed class BattleRuntimeScope : IDisposable
    {
        private readonly EncounterRosterBuilder _encounterBuilder;
        private readonly GameSession _gameSession;

        internal BattleRuntimeScope(
            BattleRuntimeModule runtime,
            EncounterRosterBuilder encounterBuilder,
            GameSession gameSession
        )
        {
            Runtime = runtime;
            _encounterBuilder = encounterBuilder;
            _gameSession = gameSession;
        }

        internal BattleRuntimeModule Runtime { get; }

        public void Dispose()
        {
            try
            {
                BattleTestFixture.DisposeBattleFixture(Runtime, Runtime?._state);
            }
            finally
            {
                _encounterBuilder?.Dispose();
                _gameSession?.Dispose();
            }
        }
    }

    private sealed class GameSessionScope : IDisposable
    {
        internal GameSessionScope()
        {
            Session = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        }

        internal GameSession Session { get; }

        public void Dispose()
        {
            Session?.Dispose();
        }
    }
}
