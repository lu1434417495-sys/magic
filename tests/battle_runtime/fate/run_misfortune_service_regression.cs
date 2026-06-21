using System;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_misfortune_service_regression : SceneTree
{
    private static readonly StringName ReverseFortuneStatusId = "reverse_fortune";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestSkillGatesUseTypedRules();
            TestRuntimeTracksAllCalamityReasonsAndSnapshot();
            TestFirstCriticalFailGrantsReverseFortuneAndCapClamps();
        }
        finally
        {
            GodotSharpCleanup.CollectPendingFinalizers();
        }

        Quit(_test.Finish("MisfortuneService regression"));
    }

    private void TestSkillGatesUseTypedRules()
    {
        _test.True(
            !string.IsNullOrWhiteSpace(MisfortuneService.GetSkillSidecarMissingMessage("black_star_brand")),
            "black_star_brand sidecar 缺失时应提供非空反馈。"
        );
        _test.True(
            !string.IsNullOrWhiteSpace(MisfortuneService.GetSkillDefaultBlockMessage("doom_sentence")),
            "doom_sentence 默认阻断时应提供非空反馈。"
        );
        _test.True(
            !string.IsNullOrWhiteSpace(MisfortuneService.GetSkillDefaultBlockMessage("unknown_gate_skill")),
            "未知 gating skill 默认阻断时应提供非空反馈。"
        );
        _test.True(
            MisfortuneService.IsMisfortuneGatedSkill("black_crown_seal"),
            "正式 misfortune gated skill 应继续能被 typed rule 表识别。"
        );
        _test.False(
            MisfortuneService.IsMisfortuneGatedSkill("not_a_misfortune_skill"),
            "非 misfortune gated skill 不应被误判。"
        );
    }

    private void TestRuntimeTracksAllCalamityReasonsAndSnapshot()
    {
        BattleRuntimeModule runtime = BuildRuntime(-6, 2);
        try
        {
            BattleState state = runtime.GetState();
            BattleUnitState hero = GetRuntimeUnit(state, "hero");
            BattleUnitState buddy = GetRuntimeUnit(state, "buddy");
            if (hero == null || buddy == null)
            {
                _test.True(false, "全理由 case 前置构建失败。");
                return;
            }

            DispatchFateEvent(runtime, "ordinary_miss", "hero");
            runtime.MarkAppliedStatusesForTurnTiming(hero, new GArray { new StringName("stunned") });
            buddy.current_hp = 0;
            buddy.is_alive = false;
            runtime.ClearDefeatedUnit(buddy);
            hero.current_hp = 20;
            hero.current_ap = 1;
            state.phase = "unit_acting";
            state.active_unit_id = hero.unit_id;
            IssueAndDisposeCommand(runtime, BuildWaitCommand(hero.unit_id));
            runtime.GetFateRuntime()?.HandleMemberBossPhaseChanged("hero", "phase_2");
            DispatchFateEvent(runtime, "critical_fail", "hero");
            DispatchFateEvent(runtime, "ordinary_miss", "hero");

            _test.Eq(runtime.GetMemberCalamityCap("hero"), 6, "rank 2/4 bonus 与极低 hidden luck 组合后 calamity cap 应为 6。");
            _test.Eq(runtime.GetMemberCalamity("hero"), 6, "六类首次坏运事件后 calamity 应累计到 6。");
            _test.False(
                hero.HasStatusEffect(ReverseFortuneStatusId),
                "若第一条 calamity 事件不是大失败，则不应补发 reverse_fortune。"
            );

            var snapshotRuntime = new SnapshotTestRuntime
            {
                BattleState = state,
                BattleRuntime = runtime,
                ActiveBattleEncounterId = "misfortune_test_anchor",
                ActiveBattleEncounterName = "灾厄测试遭遇",
            };
            var builder = new GameRuntimeSnapshotBuilder();
            builder.Setup(snapshotRuntime);
            GDictionary snapshot = builder.BuildHeadlessSnapshot();
            builder.Dispose();

            _test.Eq(
                IntValue(Dict(Dict(snapshot, "battle"), "calamity_by_member_id"), "hero", -1),
                6,
                "battle snapshot 应暴露 hero 的当前 calamity。"
            );
        }
        finally
        {
            BattleTestFixture.DisposeBattleFixture(runtime, runtime.GetState());
        }
    }

    private void TestFirstCriticalFailGrantsReverseFortuneAndCapClamps()
    {
        BattleRuntimeModule runtime = BuildRuntime(0, 0);
        try
        {
            BattleState state = runtime.GetState();
            BattleUnitState hero = GetRuntimeUnit(state, "hero");
            BattleUnitState buddy = GetRuntimeUnit(state, "buddy");
            if (hero == null || buddy == null)
            {
                _test.True(false, "critical fail first case 前置构建失败。");
                return;
            }

            DispatchFateEvent(runtime, "critical_fail", "hero");
            _test.Eq(runtime.GetMemberCalamityCap("hero"), 3, "默认角色 calamity cap 应为 3。");
            _test.Eq(runtime.GetMemberCalamity("hero"), 1, "第一次 critical_fail 应先授予 1 点 calamity。");
            _test.True(
                hero.HasStatusEffect(ReverseFortuneStatusId),
                "第一次 calamity 事件就是大失败时应授予 reverse_fortune。"
            );
            BattleStatusEffectState reverseFortune = hero.GetStatusEffect(ReverseFortuneStatusId);
            _test.Eq(
                reverseFortune != null ? reverseFortune.duration : -1,
                60,
                "reverse_fortune 应维持 1 回合基准 duration。"
            );

            DispatchFateEvent(runtime, "ordinary_miss", "hero");
            runtime.MarkAppliedStatusesForTurnTiming(hero, new GArray { new StringName("fear") });
            runtime.GetFateRuntime()?.HandleMemberBossPhaseChanged("hero", "phase_2");
            buddy.current_hp = 0;
            buddy.is_alive = false;
            runtime.ClearDefeatedUnit(buddy);

            _test.Eq(runtime.GetMemberCalamity("hero"), 3, "超出默认上限后 calamity 不应继续增长。");
            _test.Eq(
                runtime.GetCalamityByMemberIdSnapshot().TryGetValue("hero", out int calamity)
                    ? calamity
                    : 0,
                3,
                "BattleRuntime.calamity_by_member_id 应与 MisfortuneService 计算结果保持同步。"
            );
        }
        finally
        {
            BattleTestFixture.DisposeBattleFixture(runtime, runtime.GetState());
        }
    }

    private static BattleRuntimeModule BuildRuntime(
        int hiddenLuckAtBirth,
        int calamityCapacityBonus
    )
    {
        var runtime = new BattleRuntimeModule();
        runtime.setup();

        BattleUnitState hero = null;
        BattleUnitState buddy = null;
        BattleUnitState boss = null;
        EncounterAnchorData encounterAnchor = null;
        bool returned = false;
        try
        {
            hero = BuildMemberUnit(
                "hero",
                "Hero",
                100,
                hiddenLuckAtBirth,
                calamityCapacityBonus
            );
            buddy = BuildMemberUnit("buddy", "Buddy", 80, 0, 0);
            boss = BuildEnemyUnit("boss_01", "Boss");
            encounterAnchor = new EncounterAnchorData
            {
                entity_id = "misfortune_test_anchor",
                display_name = "灾厄测试遭遇",
                world_coord = Vector2I.Zero,
                faction_id = "hostile",
                region_tag = "test_region",
                enemy_roster_template_id = "",
                encounter_profile_id = "",
                growth_stage = 0,
            };
            var context = new GDictionary
            {
                ["battle_map_size"] = new Vector2I(6, 6),
                ["ally_spawns"] = new GArray { new Vector2I(1, 1), new Vector2I(2, 1) },
                ["enemy_spawns"] = new GArray { new Vector2I(4, 4) },
                ["battle_party"] = new GArray { hero.ToDictionary(), buddy.ToDictionary() },
                ["enemy_units"] = new GArray { boss.ToDictionary() },
            };
            BattleState state = runtime.StartBattle(encounterAnchor, 101, context);
            BattleUnitState runtimeHero = GetRuntimeUnit(state, "hero");
            BattleUnitState runtimeBuddy = GetRuntimeUnit(state, "buddy");
            if (runtimeHero != null)
                runtime._grid_service.PlaceUnit(state, runtimeHero, new Vector2I(1, 1), true);
            if (runtimeBuddy != null)
                runtime._grid_service.PlaceUnit(state, runtimeBuddy, new Vector2I(2, 1), true);
            returned = true;
            return runtime;
        }
        finally
        {
            BattleTestFixture.DisposeBattleUnit(hero);
            BattleTestFixture.DisposeBattleUnit(buddy);
            BattleTestFixture.DisposeBattleUnit(boss);
            GodotSharpCleanup.DisposeGodotObject(encounterAnchor);
            if (!returned)
                BattleTestFixture.DisposeBattleFixture(runtime, runtime.GetState());
        }
    }

    private static BattleUnitState BuildMemberUnit(
        StringName memberId,
        string displayName,
        int hpMax,
        int hiddenLuckAtBirth,
        int calamityCapacityBonus
    )
    {
        return new BattleUnitState
        {
            unit_id = memberId,
            source_member_id = memberId,
            display_name = displayName,
            faction_id = "player",
            control_mode = "manual",
            attribute_snapshot = BuildAttributeSnapshot(
                hpMax,
                hiddenLuckAtBirth,
                calamityCapacityBonus
            ),
            current_hp = hpMax,
            current_mp = 0,
            current_stamina = 0,
            current_aura = 0,
            current_ap = 1,
            is_alive = true,
        };
    }

    private static BattleUnitState BuildEnemyUnit(StringName unitId, string displayName)
    {
        return new BattleUnitState
        {
            unit_id = unitId,
            display_name = displayName,
            faction_id = "hostile",
            control_mode = "ai",
            attribute_snapshot = BuildAttributeSnapshot(160, 0, 0),
            current_hp = 160,
            current_ap = 1,
            is_alive = true,
        };
    }

    private static AttributeSnapshot BuildAttributeSnapshot(
        int hpMax,
        int hiddenLuckAtBirth,
        int calamityCapacityBonus
    )
    {
        var snapshot = new AttributeSnapshot();
        snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), hpMax);
        snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.MpMax), 0);
        snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.StaminaMax), 0);
        snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.AuraMax), 0);
        snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ActionPoints), 1);
        snapshot.SetValue("hidden_luck_at_birth", hiddenLuckAtBirth);
        snapshot.SetValue("calamity_capacity_bonus", calamityCapacityBonus);
        SeedAttributeSnapshotBaseAttributesAndAc(snapshot);
        return snapshot;
    }

    private static void SeedAttributeSnapshotBaseAttributesAndAc(AttributeSnapshot snapshot)
    {
        if (snapshot == null)
            return;
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
            if (!snapshot.HasValue(attributeId))
                snapshot.SetValue(attributeId, 10);
        }
        if (!snapshot.HasValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass)))
        {
            int agilityModifier = AttributeSnapshot.CalculateScoreModifier(
                snapshot.GetValue("agility")
            );
            snapshot.SetValue(
                AttributeService.ToStringName(AttributeIdKind.ArmorClass),
                Math.Clamp(AttributeService.BASE_ARMOR_CLASS + agilityModifier, 1, 99)
            );
        }
    }

    private static BattleUnitState GetRuntimeUnit(BattleState state, StringName memberId)
    {
        if (state == null || !state.ContainsUnit(memberId))
            return null;
        return state.GetUnit(memberId);
    }

    private static BattleCommand BuildWaitCommand(StringName unitId)
    {
        return new BattleCommand { command_type = BattleTypedNames.ToStringName(BattleCommandKind.Wait), unit_id = unitId };
    }

    private static void IssueAndDisposeCommand(BattleRuntimeModule runtime, BattleCommand command)
    {
        BattleEventBatch batch = null;
        try
        {
            batch = runtime?.IssueCommand(command);
        }
        finally
        {
            BattleTestFixture.DisposeFixtureObject(batch);
            BattleTestFixture.DisposeBattleCommand(command);
        }
    }

    private static void DispatchFateEvent(
        BattleRuntimeModule runtime,
        StringName eventType,
        StringName memberId
    )
    {
        runtime.GetFateEventBus().Dispatch(
            BattleFateEventPayload.Create(
                eventType,
                runtime.GetState()?.battle_id ?? new StringName(""),
                memberId
            )
        );
    }

    private static GDictionary Dict(GDictionary source, string key)
    {
        if (
            source != null
            && source.ContainsKey(key)
            && source[key].VariantType == Variant.Type.Dictionary
        )
            return source[key].AsGodotDictionary();
        return new GDictionary();
    }

    private static int IntValue(GDictionary source, string key, int fallback = 0)
    {
        if (source == null || !source.ContainsKey(key))
            return fallback;
        Variant value = source[key];
        return value.VariantType == Variant.Type.Nil ? fallback : value.AsInt32();
    }
}
