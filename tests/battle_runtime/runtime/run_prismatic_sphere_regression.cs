using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_prismatic_sphere_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestPrismaticSphereCreatesOrderedLayers();
            TestPrismaticSphereBlocksDeeperBreakersUntilOuterLayerBreaks();
            TestProjectedEffectBarrierGeometryRespectsBoundary();
            TestDeathWardWithoutLastStandDoesNotBlockFatalPhysicalDamage();
            TestGreenLayerInstantDeathUsesFatalDamageChain();
            TestPetrifiedBlocksTurnUntilSelfSaveSucceeds();
            TestVioletLayerTeleportsNonSummonsAndRemovesSummons();
            TestCleanseHarmfulRemovesMadnessButNotPetrified();
            TestDispelMagicRemovesMagicStatusesByRelation();
            Quit(_test.Finish("Prismatic sphere regression"));
        }
        catch (Exception ex)
        {
            GD.PushError($"Prismatic sphere regression crashed: {ex}");
            Quit(1);
        }
    }

    private void TestPrismaticSphereCreatesOrderedLayers()
    {
        Fixture fixture = BuildRuntimeWithSphere();
        BattleBarrierInstanceState barrier = FirstBarrier(fixture.State);
        _test.True(barrier != null && !barrier.IsEmpty, "虹光法球应写入 battle_state.layered_barrier_fields。");
        _test.Eq(ActiveLayerId(barrier), new StringName("red"), "新建虹光法球的第一活动层应为红色层。");
        _test.Eq(barrier.Layers.Count, 7, "虹光法球应包含 7 层。");
    }

    private void TestPrismaticSphereBlocksDeeperBreakersUntilOuterLayerBreaks()
    {
        Fixture fixture = BuildRuntimeWithSphere();
        BattleRuntimeModule runtime = fixture.Runtime;
        BattleState state = fixture.State;
        BattleUnitState caster = fixture.Caster;
        BattleUnitState enemy = fixture.Enemy;
        var batch = new BattleEventBatch();

        SkillDef magicMissile = BuildSkill("mage_arcane_missile", "奥术飞弹", "mage", "magic");
        BattleBarrierInteractionResult blockedResult =
            runtime._layered_barrier_service.ResolveSkillBarrierInteractionResult(
                enemy,
                caster,
                magicMissile,
                Array.Empty<CombatEffectDef>(),
                batch
            );
        _test.True(blockedResult.Blocked, "外层仍在时，蓝色层破解法术应被阻挡。");
        _test.Eq(ActiveLayerId(FirstBarrier(state)), new StringName("red"), "错误顺序的破解不应破坏红色层。");

        SkillDef coneOfCold = BuildSkill("mage_cone_of_cold", "寒冰锥", "mage", "magic", "freeze");
        BattleBarrierInteractionResult breakResult =
            runtime._layered_barrier_service.ResolveSkillBarrierInteractionResult(
                enemy,
                caster,
                coneOfCold,
                Array.Empty<CombatEffectDef>(),
                batch
            );
        _test.True(breakResult.Blocked, "正确破解法术应被法球消耗。");
        _test.Eq(ActiveLayerId(FirstBarrier(state)), new StringName("orange"), "寒冰锥应只破坏最外侧红色层。");

        SkillDef gustOfWind = BuildSkill("mage_gust_of_wind", "强风术", "mage", "magic", "air");
        BattleBarrierInteractionResult orangeBreakResult =
            runtime._layered_barrier_service.ResolveSkillBarrierInteractionResult(
                enemy,
                caster,
                gustOfWind,
                Array.Empty<CombatEffectDef>(),
                batch
            );
        _test.True(orangeBreakResult.Blocked, "强风术应被虹光法球消耗以破解橙色层。");
        _test.Eq(ActiveLayerId(FirstBarrier(state)), new StringName("yellow"), "强风术应在红层破除后破解橙色层。");

        SkillDef disintegrate = BuildSkill("mage_spell_disjunction", "裂解术", "mage", "magic", "arcane");
        BattleBarrierInteractionResult yellowBreakResult =
            runtime._layered_barrier_service.ResolveSkillBarrierInteractionResult(
                enemy,
                caster,
                disintegrate,
                Array.Empty<CombatEffectDef>(),
                batch
            );
        _test.True(yellowBreakResult.Blocked, "裂解术应被虹光法球消耗以破解黄色层。");
        _test.Eq(ActiveLayerId(FirstBarrier(state)), new StringName("green"), "裂解术应在橙层破除后破解黄色层。");

        SkillDef passwall = BuildSkill("mage_passwall", "穿墙术", "mage", "magic", "earth");
        BattleBarrierInteractionResult greenBreakResult =
            runtime._layered_barrier_service.ResolveSkillBarrierInteractionResult(
                enemy,
                caster,
                passwall,
                Array.Empty<CombatEffectDef>(),
                batch
            );
        _test.True(greenBreakResult.Blocked, "穿墙术应被虹光法球消耗以破解绿色层。");
        _test.Eq(ActiveLayerId(FirstBarrier(state)), new StringName("blue"), "穿墙术应在黄层破除后破解绿色层。");

        BattleBarrierInteractionResult blueBreakResult =
            runtime._layered_barrier_service.ResolveSkillBarrierInteractionResult(
                enemy,
                caster,
                magicMissile,
                Array.Empty<CombatEffectDef>(),
                batch
            );
        _test.True(blueBreakResult.Blocked, "奥术飞弹应被虹光法球消耗以破解蓝色层。");
        _test.Eq(ActiveLayerId(FirstBarrier(state)), new StringName("indigo"), "奥术飞弹应在绿层破除后破解蓝色层。");

        SkillDef continualLight = BuildSkill("mage_continual_light", "恒光术", "mage", "magic", "radiant");
        BattleBarrierInteractionResult indigoBreakResult =
            runtime._layered_barrier_service.ResolveSkillBarrierInteractionResult(
                enemy,
                caster,
                continualLight,
                Array.Empty<CombatEffectDef>(),
                batch
            );
        _test.True(indigoBreakResult.Blocked, "恒光术应被虹光法球消耗以破解靛色层。");
        _test.Eq(ActiveLayerId(FirstBarrier(state)), new StringName("violet"), "恒光术应在蓝层破除后破解靛色层。");

        SkillDef dispelMagic = BuildSkill("mage_dispel_magic", "解除魔法", "mage", "magic", "dispel");
        BattleBarrierInteractionResult violetBreakResult =
            runtime._layered_barrier_service.ResolveSkillBarrierInteractionResult(
                enemy,
                caster,
                dispelMagic,
                Array.Empty<CombatEffectDef>(),
                batch
            );
        _test.True(violetBreakResult.Blocked, "解除魔法应被虹光法球消耗以破解紫色层。");
        _test.Eq(ActiveLayerId(FirstBarrier(state)), new StringName(""), "解除魔法应在靛层破除后破解最后的紫色层。");
    }

    private void TestProjectedEffectBarrierGeometryRespectsBoundary()
    {
        List<Vector2I> barrierCoords = DiamondArea(new Vector2I(2, 2), 2);
        _test.False(
            BattleBarrierGeometryService.LineCrossesBarrierArea(
                new Vector2I(2, 2),
                new Vector2I(3, 2),
                barrierCoords
            ),
            "法球内部到内部的投射效果不应被屏障拦截。"
        );
        _test.True(
            BattleBarrierGeometryService.LineCrossesBarrierArea(
                new Vector2I(2, 2),
                new Vector2I(5, 2),
                barrierCoords
            ),
            "法球内部到外部的投射效果应被屏障拦截。"
        );
        _test.True(
            BattleBarrierGeometryService.LineCrossesBarrierArea(
                new Vector2I(5, 2),
                new Vector2I(2, 2),
                barrierCoords
            ),
            "法球外部到内部的投射效果应被屏障拦截。"
        );
        _test.True(
            BattleBarrierGeometryService.LineCrossesBarrierArea(
                new Vector2I(5, 2),
                new Vector2I(-1, 2),
                barrierCoords
            ),
            "法球外部到外部但线段穿过屏障时应被拦截。"
        );
        _test.False(
            BattleBarrierGeometryService.LineCrossesBarrierArea(
                new Vector2I(5, 4),
                new Vector2I(6, 4),
                barrierCoords
            ),
            "法球外部到外部且未穿过屏障时不应被拦截。"
        );
    }

    private void TestDeathWardWithoutLastStandDoesNotBlockFatalPhysicalDamage()
    {
        BattleDamageResolver resolver = new();
        BattleUnitState source = BuildUnit(
            "plain_fatal_source",
            "普通致命攻击者",
            "enemy",
            new Vector2I(0, 0)
        );
        BattleUnitState target = BuildUnit(
            "spellward_only_target",
            "仅有负能量免疫目标",
            "player",
            new Vector2I(1, 0)
        );
        target.current_hp = 8;
        target.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 8);
        SetStatus(target, "death_ward", new GDictionary { ["damage_tag"] = "negative_energy" });

        CombatEffectDef damageEffect = new()
        {
            effect_type = "damage",
            power = 99,
            damage_tag = "physical_slash",
        };
        GDictionary result = AttackEffectResolutionResultReader.BuildGodotPayload(resolver.ResolveEffects(source, target, new GArray { damageEffect }));
        _test.Eq(
            result != null && result.ContainsKey("damage") ? result["damage"].AsInt32() : -1,
            99,
            "非 Last Stand 来源的 death_ward 不应吞掉普通致命 HP 伤害。"
        );
        _test.Eq(target.current_hp, 0, "非 Last Stand 来源的 death_ward 遭遇普通致命伤害时应正常归零。");
        _test.False(target.is_alive, "非 Last Stand 来源的 death_ward 不应阻止死亡状态。");
    }

    private void TestGreenLayerInstantDeathUsesFatalDamageChain()
    {
        Fixture fixture = BuildRuntimeWithSphere();
        BattleRuntimeModule runtime = fixture.Runtime;
        BattleState state = fixture.State;
        BattleUnitState enemy = fixture.Enemy;
        SkillDef lastStandSkill = ResourceLoader.Load<SkillDef>(
            "res://data/configs/skills/warrior_last_stand.tres"
        );
        _test.True(
            lastStandSkill != null && lastStandSkill.combat_profile != null,
            "绿色层即死回归需要 warrior_last_stand 技能资源。"
        );
        if (lastStandSkill == null || lastStandSkill.combat_profile == null)
        {
            return;
        }

        runtime.GetDamageResolver().SetSkillDefs(
            new Dictionary<StringName, SkillDef>
            {
                [(StringName)"warrior_last_stand"] = lastStandSkill
            }
        );
        enemy.current_hp = 8;
        SetStatus(
            enemy,
            "death_ward",
            sourceSkillId: "warrior_last_stand",
            sourceSkillLevel: 7
        );
        SetStatus(enemy, "staggered");
        MarkLayersBroken(state, "red", "orange", "yellow", "blue", "indigo", "violet");
        SetLayerSaveRollOverride(state, "green", 1);

        BattleBarrierInteractionResult result =
            runtime._layered_barrier_service.ResolveUnitBoundaryCrossingResult(
                enemy,
                new Vector2I(5, 2),
                new Vector2I(4, 2),
                new BattleEventBatch()
            );
        _test.False(result.Blocked, "不屈抵消绿色层即死后，穿越不应因死亡终止。");
        _test.True(enemy.is_alive && enemy.current_hp > 0, "绿色层即死应触发现有免死链并把目标救回正 HP。");
        _test.False(enemy.HasStatusEffect("death_ward"), "绿色层即死触发不屈后应消耗 death_ward。");
        _test.False(enemy.HasStatusEffect("staggered"), "Lv5+ 不屈触发后仍应清理负面状态。");
        _test.True(enemy.HasStatusEffect("last_stand_active"), "Lv7 不屈触发后应保留 last_stand_active。");
    }

    private void TestPetrifiedBlocksTurnUntilSelfSaveSucceeds()
    {
        Fixture fixture = BuildRuntimeWithSphere();
        BattleRuntimeModule runtime = fixture.Runtime;
        BattleUnitState target = fixture.Enemy;
        var batch = new BattleEventBatch();
        var petrified = new BattleStatusEffectState
        {
            status_id = "petrified",
            source_unit_id = "caster",
            power = 1,
            stacks = 1,
            duration = -1,
            self_save_ability = "constitution",
            self_save_dc = 15,
            self_save_roll_override = 1,
            self_save_tag = "constitution",
        };
        target.SetStatusEffect(petrified);

        BattleTurnControlStatusResult failResult =
            runtime._skill_turn_resolver.ResolveTurnControlStatusResult(target, batch);
        _test.True(failResult.SkipTurn, "石化自检失败应跳过行动。");
        _test.True(target.HasStatusEffect("petrified"), "石化失败后状态应保留。");

        BattleStatusEffectState entry = target.GetStatusEffect("petrified");
        entry.self_save_roll_override = 20;
        target.SetStatusEffect(entry);
        BattleTurnControlStatusResult successResult =
            runtime._skill_turn_resolver.ResolveTurnControlStatusResult(target, batch);
        _test.False(successResult.SkipTurn, "石化自检成功应允许本次行动继续。");
        _test.False(target.HasStatusEffect("petrified"), "石化自检成功应解除石化。");
    }

    private void TestVioletLayerTeleportsNonSummonsAndRemovesSummons()
    {
        Fixture fixture = BuildRuntimeWithSphere();
        BattleRuntimeModule runtime = fixture.Runtime;
        BattleState state = fixture.State;
        BattleUnitState enemy = fixture.Enemy;
        var batch = new BattleEventBatch();
        MarkLayersBroken(state, "red", "orange", "yellow", "green", "blue", "indigo");
        SetLayerSaveRollOverride(state, "violet", 1);

        BattleBarrierInteractionResult result =
            runtime._layered_barrier_service.ResolveUnitBoundaryCrossingResult(
                enemy,
                new Vector2I(5, 2),
                new Vector2I(4, 2),
                batch
            );
        _test.True(result.Blocked, "紫色层放逐应终止本次穿越。");
        _test.True(enemy.is_alive, "非召唤物被紫色层命中后应保留存活状态。");
        _test.False(
            CoordInsideBarrier(enemy.coord, FirstBarrier(state)),
            "非召唤物应被传送到法球外合法坐标。"
        );

        BattleUnitState summon = BuildUnit("summon", "召唤物", "enemy", new Vector2I(6, 2));
        summon.ai_blackboard.summoned = true;
        AddUnit(runtime, state, summon, true);
        BattleBarrierInteractionResult summonResult =
            runtime._layered_barrier_service.ResolveUnitBoundaryCrossingResult(
                summon,
                new Vector2I(6, 2),
                new Vector2I(4, 2),
                batch
            );
        _test.True(summonResult.Blocked, "召唤物被放逐也应终止穿越。");
        _test.False(summon.is_alive, "召唤物应被紫色层直接移除。");
    }

    private void TestCleanseHarmfulRemovesMadnessButNotPetrified()
    {
        BattleUnitState source = BuildUnit("source", "施法者", "player", Vector2I.Zero);
        BattleUnitState target = BuildUnit("target", "目标", "player", new Vector2I(1, 0));
        SetStatus(target, "madness");
        SetStatus(target, "petrified");
        var cleanse = new CombatEffectDef { effect_type = "cleanse_harmful" };
        var resolver = new BattleDamageResolver();
        resolver.ResolveEffects(source, target, new GArray { cleanse });
        _test.False(target.HasStatusEffect("madness"), "cleanse_harmful 应解除 madness。");
        _test.True(target.HasStatusEffect("petrified"), "cleanse_harmful 不应解除 petrified。");
    }

    private void TestDispelMagicRemovesMagicStatusesByRelation()
    {
        BattleUnitState source = BuildUnit("source", "施法者", "player", Vector2I.Zero);
        BattleUnitState ally = BuildUnit("ally", "友方", "player", new Vector2I(1, 0));
        BattleUnitState enemy = BuildUnit("enemy", "敌方", "enemy", new Vector2I(2, 0));
        var dispel = new CombatEffectDef
        {
            effect_type = "dispel_magic",
            power = 1,
            remove_beneficial_from_enemies = true,
            remove_harmful_from_allies = true,
            max_status_removed = 1,
        };
        var resolver = new BattleDamageResolver();

        SetStatus(ally, "blind");
        SetStatus(ally, "petrified");
        GDictionary allyResult = AttackEffectResolutionResultReader.BuildGodotPayload(resolver.ResolveEffects(source, ally, new GArray { dispel }));
        _test.True(DictBool(allyResult, "applied"), "解除魔法命中友方时应能移除可驱散减益。");
        _test.False(ally.HasStatusEffect("blind"), "解除魔法应移除友方 blind。");
        _test.True(ally.HasStatusEffect("petrified"), "解除魔法不应移除 petrified。");
        _test.True(
            DictArrayHasStringName(allyResult, "removed_status_effect_ids", "blind"),
            "解除魔法结果应报告被移除的友方状态。"
        );

        SetStatus(enemy, "magic_shield");
        SetStatus(enemy, "attack_up");
        SetStatus(enemy, "marked");
        GDictionary enemyResult = AttackEffectResolutionResultReader.BuildGodotPayload(resolver.ResolveEffects(source, enemy, new GArray { dispel }));
        _test.True(DictBool(enemyResult, "applied"), "解除魔法命中敌方时应能移除可驱散增益。");
        _test.False(enemy.HasStatusEffect("magic_shield"), "解除魔法应优先移除敌方高优先级魔法增益。");
        _test.True(enemy.HasStatusEffect("attack_up"), "单次解除魔法只应移除配置数量内的敌方增益。");
        _test.True(enemy.HasStatusEffect("marked"), "解除魔法不应移除敌方身上的有害状态。");
        _test.True(
            DictArrayHasStringName(enemyResult, "removed_status_effect_ids", "magic_shield"),
            "解除魔法结果应报告被移除的敌方状态。"
        );
    }

    private static Fixture BuildRuntimeWithSphere()
    {
        var runtime = new BattleRuntimeModule();
        runtime.setup();
        BattleState state = BuildState(new Vector2I(7, 5));
        runtime.SetupStateForTests(state);
        BattleUnitState caster = BuildUnit("caster", "施法者", "player", new Vector2I(2, 2));
        BattleUnitState enemy = BuildUnit("enemy", "敌人", "enemy", new Vector2I(5, 2));
        AddUnit(runtime, state, caster, false);
        AddUnit(runtime, state, enemy, true);
        SkillDef skill = BuildSkill("mage_prismatic_sphere", "虹光法球", "mage", "magic");
        var effect = new CombatEffectDef
        {
            effect_type = "layered_barrier",
            duration_tu = 120,
            save_dc = 15,
            save_dc_mode = "static",
            save_ability = "willpower",
            save_tag = "magic",
            @params = new GDictionary
            {
                ["area_pattern"] = "diamond",
                ["profile_id"] = "prismatic_sphere",
                ["radius_cells"] = 2,
            },
        };
        runtime._layered_barrier_service.ApplyLayeredBarrierEffectResult(
            caster,
            caster,
            skill,
            effect,
            new BattleEventBatch()
        );
        return new Fixture(runtime, state, caster, enemy);
    }

    private static BattleState BuildState(Vector2I mapSize)
    {
        var state = new BattleState
        {
            battle_id = "prismatic_sphere_regression",
            phase = "unit_acting",
            map_size = mapSize,
            timeline = new BattleTimelineState(),
        };
        for (int y = 0; y < mapSize.Y; y++)
        {
            for (int x = 0; x < mapSize.X; x++)
            {
                var cell = new BattleCellState
                {
                    coord = new Vector2I(x, y),
                    base_terrain = BattleTerrainRules.ToStringName(BattleTerrainKind.Land),
                    base_height = 4,
                };
                cell.RecalculateRuntimeValues();
                state.SetCell(cell.coord, cell);
            }
        }
        state.RebuildCellColumns();
        return state;
    }

    private static BattleUnitState BuildUnit(
        StringName unitId,
        string displayName,
        StringName factionId,
        Vector2I coord
    )
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = displayName,
            faction_id = factionId,
            control_mode = "manual",
            current_hp = 120,
            current_mp = 120,
            current_stamina = 40,
            current_ap = 2,
            is_alive = true,
        };
        unit.SetAnchorCoord(coord);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 120);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.MpMax), 120);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.StaminaMax), 40);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass), AttributeService.BASE_ARMOR_CLASS);
        unit.attribute_snapshot.SetValue("constitution", 10);
        unit.attribute_snapshot.SetValue("willpower", 10);
        unit.attribute_snapshot.SetValue("intelligence", 14);
        unit.attribute_snapshot.SetValue("constitution_modifier", 0);
        unit.attribute_snapshot.SetValue("willpower_modifier", 0);
        unit.attribute_snapshot.SetValue("intelligence_modifier", 2);
        return unit;
    }

    private static SkillDef BuildSkill(StringName skillId, string displayName, params StringName[] tags)
    {
        var skill = new SkillDef
        {
            skill_id = skillId,
            display_name = displayName,
            icon_id = skillId,
        };
        skill.SetTags(tags);
        return skill;
    }

    private static void AddUnit(
        BattleRuntimeModule runtime,
        BattleState state,
        BattleUnitState unit,
        bool isEnemy
    )
    {
        if (state == null || unit == null)
        {
            return;
        }
        state.SetUnit(unit);
        if (isEnemy)
        {
            state.enemy_unit_ids.Add(unit.unit_id);
        }
        else
        {
            state.ally_unit_ids.Add(unit.unit_id);
        }
        runtime?._grid_service.PlaceUnit(state, unit, unit.coord, true);
    }

    private static void SetStatus(
        BattleUnitState unitState,
        StringName statusId,
        GDictionary parameters = null,
        StringName sourceSkillId = default,
        int? sourceSkillLevel = null
    )
    {
        var status = new BattleStatusEffectState
        {
            status_id = statusId,
            source_unit_id = "source",
            power = 1,
            stacks = 1,
            duration = -1,
            @params = parameters?.Duplicate(true) ?? new GDictionary(),
            source_skill_id = sourceSkillId,
            source_skill_level = sourceSkillLevel,
        };
        unitState.SetStatusEffect(status);
    }

    private static BattleBarrierInstanceState FirstBarrier(BattleState state)
    {
        GDictionary barrierFields = state?.ProjectLayeredBarrierFields() ?? new GDictionary();
        if (barrierFields.Count == 0)
        {
            return new BattleBarrierInstanceState();
        }
        foreach (var rawKey in barrierFields.Keys)
        {
            return BattleBarrierInstanceState.FromRuntimeDict(
                barrierFields[rawKey].AsGodotDictionary()
            );
        }
        return new BattleBarrierInstanceState();
    }

    private static StringName FirstBarrierKey(BattleState state)
    {
        GDictionary barrierFields = state?.ProjectLayeredBarrierFields() ?? new GDictionary();
        if (barrierFields.Count == 0)
        {
            return "";
        }
        foreach (var rawKey in barrierFields.Keys)
        {
            return ProgressionDataUtils.to_string_name(rawKey);
        }
        return "";
    }

    private static void StoreFirstBarrier(BattleState state, BattleBarrierInstanceState barrier)
    {
        StringName key = FirstBarrierKey(state);
        if (state == null || key == "" || barrier == null)
        {
            return;
        }
        state.PutLayeredBarrierFieldPayload(key, barrier.ToRuntimeDict());
    }

    private static StringName ActiveLayerId(BattleBarrierInstanceState barrier)
    {
        if (barrier == null)
        {
            return "";
        }
        foreach (BattleBarrierLayerState layer in barrier.Layers)
        {
            if (layer != null && !layer.Broken)
            {
                return layer.LayerId;
            }
        }
        return "";
    }

    private static void MarkLayersBroken(BattleState state, params StringName[] layerIds)
    {
        BattleBarrierInstanceState barrier = FirstBarrier(state);
        var targetLayerIds = new HashSet<StringName>(layerIds ?? Array.Empty<StringName>());
        var layers = new List<BattleBarrierLayerState>();
        foreach (BattleBarrierLayerState layer in barrier.GetLayersTyped())
        {
            if (layer != null && targetLayerIds.Contains(layer.LayerId))
            {
                layer.Broken = true;
            }
            layers.Add(layer);
        }
        barrier.SetLayers(layers);
        StoreFirstBarrier(state, barrier);
    }

    private static void SetLayerSaveRollOverride(
        BattleState state,
        StringName layerId,
        int roll
    )
    {
        BattleBarrierInstanceState barrier = FirstBarrier(state);
        var layers = new List<BattleBarrierLayerState>();
        foreach (BattleBarrierLayerState layer in barrier.GetLayersTyped())
        {
            if (layer != null && layer.LayerId == layerId)
            {
                layer.HasSaveRollOverride = true;
                layer.SaveRollOverride = roll;
            }
            layers.Add(layer);
        }
        barrier.SetLayers(layers);
        StoreFirstBarrier(state, barrier);
    }

    private static bool CoordInsideBarrier(Vector2I coord, BattleBarrierInstanceState barrier)
    {
        if (barrier == null || barrier.IsEmpty)
        {
            return false;
        }
        return BattleBarrierGeometryService.CoordInsideBarrier(
            coord,
            DiamondArea(barrier.AnchorCoord, barrier.RadiusCells)
        );
    }

    private static List<Vector2I> DiamondArea(Vector2I center, int radius)
    {
        var coords = new List<Vector2I>();
        for (int y = center.Y - radius; y <= center.Y + radius; y++)
        {
            for (int x = center.X - radius; x <= center.X + radius; x++)
            {
                Vector2I coord = new(x, y);
                if (Math.Abs(coord.X - center.X) + Math.Abs(coord.Y - center.Y) <= radius)
                {
                    coords.Add(coord);
                }
            }
        }
        return coords;
    }

    private static bool DictBool(GDictionary dictionary, string key, bool fallback = false)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
        {
            return fallback;
        }
        return dictionary[key].AsBool();
    }

    private static bool DictArrayHasStringName(
        GDictionary dictionary,
        string key,
        StringName expected
    )
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
        {
            return false;
        }
        GArray values = dictionary[key].AsGodotArray();
        foreach (var value in values)
        {
            if (ProgressionDataUtils.to_string_name(value) == expected)
            {
                return true;
            }
        }
        return false;
    }

    private readonly record struct Fixture(
        BattleRuntimeModule Runtime,
        BattleState State,
        BattleUnitState Caster,
        BattleUnitState Enemy
    );
}
