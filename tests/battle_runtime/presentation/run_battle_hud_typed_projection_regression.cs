using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_battle_hud_typed_projection_regression : LifecycleTestSceneTree
{
    private const string HudRootKeys =
        "header_title|header_subtitle|objective_progress|round_badge|mode_text|queue_entries|focus_unit|skill_title|selected_skill_variant_name|skill_subtitle|skill_slots|tile_text|selected_skill_hit_preview_text|selected_skill_hit_preview_payload|selected_skill_hit_badge_text|selected_skill_hit_stage_rates|selected_skill_damage_preview_text|selected_skill_damage_min|selected_skill_damage_max|selected_skill_save_branch_preview_payload|selected_skill_save_branch_preview_text|selected_skill_fate_preview_text|selected_skill_fate_badges|selected_skill_preview_tooltip_text|selected_skill_target_selection_mode|selected_skill_target_min_count|selected_skill_target_max_count|selected_skill_target_count|selected_skill_confirm_ready|selected_skill_auto_cast_ready|command_dock|hint_text|recent_battle_log_lines|equipment_panel|barriers|barrier_summary_text";
    private const string ObjectiveProgressKeys =
        "mode|title|progress_text|target_actor_id|target_unit_id|target_display_name|target_alive|target_secured|target_reached_exit|required_unit_ids|alive_required_unit_ids|reached_exit_unit_ids|required_unit_count|alive_required_unit_count|reached_exit_unit_count|exit_zone_id|exit_edge|exit_depth|exit_coords|current_tu|start_tu|deadline_tu|remaining_tu|enemy_unit_count|alive_enemy_unit_count|operation_nodes|operation_node_count|completed_operation_node_count|incomplete_operation_node_count|control_zones|control_zone_count|player_control_score|hostile_control_score|control_score_target";
    private const string HoverRootKeys =
        "hover_coord|hover_is_valid_target|has_selected_skill|hit_preview|hit_stage_rates|hit_badge_text|fate_badges|save_branch_preview|save_branch_preview_text|damage_min|damage_max|damage_text|target_unit";
    private const string QueueEntryKeys =
        "slot_index|name|glyph|portrait_key|primary_color|secondary_color|edge_color|hp_ratio|hp_text|ap_text|is_active|is_ready|is_enemy";
    private const string SkillSlotKeys =
        "index|is_empty|skill_entry_id|skill_id|source_kind|source_label_key|skill_level|is_battle_only|suppressed_source_keys|display_name|short_name|description|icon_key|hotkey|footer_text|is_selected|is_disabled|accent_color|accent_dark|edge_color|cooldown|disabled_reason";
    private const string FocusUnitKeys =
        "name|role_text|resource_info|glyph|portrait_key|primary_color|secondary_color|edge_color|hp_current|hp_max|mp_current|mp_max|stamina_current|stamina_max|aura_current|aura_max|ap_current|ap_max|move_current|move_max|status_effects";
    private const string EquipmentPanelKeys =
        "title|meta|active_unit_id|active_unit_name|ap_cost|can_change_equipment|disabled_reason|slots|backpack_entries|summary_text";
    private const string EquipmentSlotKeys =
        "slot_id|slot_label|is_filled|is_entry_slot|entry_slot_id|item_id|item_display_name|instance_id|occupied_slot_ids|occupied_slot_labels|can_unequip|disabled_reason";
    private const string BackpackEntryKeys =
        "instance_id|item_id|display_name|description|icon|allowed_slot_ids|allowed_slot_labels|default_slot_id|occupied_slot_ids_by_default|can_equip|disabled_reason";
    private const string BarrierKeys =
        "barrier_instance_id|profile_id|display_name|source_unit_id|source_skill_id|anchor_coord|radius_cells|area_pattern|remaining_tu|current_layer_id|current_layer_name|active_layer_count|broken_layer_count|total_layer_count|broken_layer_names|summary_text";
    private const string HoverTargetKeys =
        "unit_id|name|glyph|portrait_key|primary_color|edge_color|hp_current|hp_max|mp_current|mp_max|mp_visible|stamina_current|stamina_max|aura_current|aura_max|aura_visible|ap_current|ap_max|is_enemy|is_self|status_effects";

    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private async void Run()
    {
        try
        {
            TestFixedProjectionSchemaAndMutationIsolation();
            TestAdapterReadsStayManaged();
            TestBossObjectiveAdapterProjection();
            TestInterceptObjectiveAdapterProjection();
            TestDefenseObjectiveAdapterProjection();
            await TestPanelPresentationOwnership();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        RequestTestExit(_test.Finish("Battle HUD typed projection regression"));
    }

    private void TestFixedProjectionSchemaAndMutationIsolation()
    {
        var queueInput = new List<BattleHudQueueEntrySnapshot>
        {
            new()
            {
                SlotIndex = 0,
                Name = "施法者",
                Glyph = "施",
                PortraitKey = "caster",
                PrimaryColor = Colors.Blue,
                SecondaryColor = Colors.DarkBlue,
                EdgeColor = Colors.Gold,
                HpRatio = 0.75f,
                HpText = "HP 30/40",
                ApText = "AP 2",
                IsActive = true,
                IsReady = true,
                IsEnemy = false,
            },
            BattleHudQueueEntrySnapshot.Overflow("+2"),
        };
        var skillInput = new List<BattleHudSkillSlotSnapshot>
        {
            new(
                0,
                false,
                "known:test_skill",
                "test_skill",
                "known_skill",
                "skill_source.known",
                3,
                false,
                new[] { new StringName("equipment:test_blade") },
                "测试技能",
                "测试",
                "技能说明",
                "warrior_whirlwind_slash",
                "1",
                "AP 2",
                true,
                false,
                Colors.Orange,
                Colors.DarkOrange,
                Colors.Gold,
                0,
                ""
            ),
            new(1, true),
        };
        var recentLines = new List<string> { "第一条", "第二条" };
        var barrierInput = new List<BattleHudBarrierSnapshot>
        {
            BuildBarrierSnapshot(),
        };
        var equipmentSlots = new List<BattleHudEquipmentSlotSnapshot>
        {
            new(
                "main_hand",
                "主手",
                true,
                true,
                "main_hand",
                "test_blade",
                "测试长剑",
                "blade-1",
                new[] { "main_hand", "off_hand" },
                new[] { "主手", "副手" },
                true,
                ""
            ),
        };
        var backpackEntries = new List<BattleHudBackpackEntrySnapshot>
        {
            new(
                "blade-2",
                "test_blade",
                "备用长剑",
                "备用武器",
                "res://icon.png",
                new[] { "main_hand" },
                new[] { "主手" },
                "main_hand",
                new[] { "main_hand" },
                true,
                ""
            ),
        };
        var attackPreview = new AttackPreviewData
        {
            SummaryText = "命中 70%",
            Source = "runtime",
            HitRatePercent = 70,
            SuccessRatePercent = 65,
            BaseHitRatePercent = 75,
            Stages = new List<AttackPreviewStage>
            {
                new(70, 65, 75, 7, 7, "需要 7+"),
            },
        };
        attackPreview.SetAttackRollModifierBreakdown(
            new[]
            {
                new BattleAttackRollModifierSpec
                {
                    source_domain = "terrain",
                    source_id = "dust",
                    label = "尘土",
                    modifier_delta = -1,
                },
            }
        );

        BattleHudSnapshot snapshot = BuildHudSnapshot(
            queueInput,
            skillInput,
            recentLines,
            equipmentSlots,
            backpackEntries,
            BattlePresentationPayload.FromAttackPreview(attackPreview),
            barrierInput,
            barrierInput[0].SummaryText
        );

        queueInput.Clear();
        skillInput.Clear();
        recentLines[0] = "污染";
        equipmentSlots.Clear();
        backpackEntries.Clear();
        barrierInput.Clear();
        attackPreview.Stages[0] = new AttackPreviewStage(1, 1, 1, 20, 20, "污染");

        _test.Eq(snapshot.QueueEntries.Count, 2, "HUD typed snapshot should detach queue input.");
        _test.Eq(snapshot.SkillSlots.Count, 2, "HUD typed snapshot should detach skill input.");
        _test.Eq(snapshot.RecentBattleLogLines[0], "第一条", "HUD typed snapshot should detach log input.");
        _test.Eq(snapshot.EquipmentPanel.Slots.Count, 1, "HUD typed snapshot should detach equipment slots.");
        _test.Eq(snapshot.EquipmentPanel.BackpackEntries.Count, 1, "HUD typed snapshot should detach backpack entries.");
        _test.Eq(snapshot.Barriers.Count, 1, "HUD typed snapshot should detach barrier input.");

        LifecycleAuditSnapshot baseline = LifecycleAuditRegistry.Shared.CaptureSnapshot();
        for (int index = 0; index < 20; index++)
        {
            _ = snapshot.HeaderTitle;
            _ = snapshot.QueueEntries[0].Name;
            _ = snapshot.HitPreviewPayload.SummaryText;
        }
        AssertAuditBaseline(baseline, "managed HUD reads");

        using (GodotProjectionLease<GDictionary> lease = snapshot.BuildLease())
        {
            GDictionary root = lease.Value;
            _test.Eq(KeyOrder(root), HudRootKeys, "HUD root key order must match the frozen outward schema.");
            _test.Eq(
                KeyOrder(Dict(lease, root, "objective_progress")),
                ObjectiveProgressKeys,
                "objective progress schema must remain fixed."
            );
            _test.Eq(KeyOrder(Dict(lease, root, "round_badge")), "tu_text|ready_text", "round badge schema must remain fixed.");

            GArray queue = ArrayValue(lease, root, "queue_entries");
            _test.Eq(KeyOrder(DictionaryItem(lease, queue, 0, "queue normal")), QueueEntryKeys, "normal queue union schema must remain fixed.");
            _test.Eq(KeyOrder(DictionaryItem(lease, queue, 1, "queue overflow")), "is_overflow|overflow_text", "overflow queue union schema must remain fixed.");

            GArray skills = ArrayValue(lease, root, "skill_slots");
            _test.Eq(KeyOrder(DictionaryItem(lease, skills, 0, "skill filled")), SkillSlotKeys, "filled skill union schema must remain fixed.");
            _test.Eq(KeyOrder(DictionaryItem(lease, skills, 1, "skill empty")), "index|is_empty", "empty skill union schema must remain fixed.");

            GDictionary focus = Dict(lease, root, "focus_unit");
            _test.Eq(KeyOrder(focus), FocusUnitKeys, "focus unit schema must remain fixed.");
            GDictionary resourceInfo = Dict(lease, focus, "resource_info");
            _test.Eq(KeyOrder(Dict(lease, resourceInfo, "hp")), "current|max|ratio|label|visible", "resource line schema must remain fixed.");

            GDictionary equipment = Dict(lease, root, "equipment_panel");
            _test.Eq(KeyOrder(equipment), EquipmentPanelKeys, "equipment panel schema must remain fixed.");
            GArray projectedEquipmentSlots = ArrayValue(lease, equipment, "slots");
            GArray projectedBackpackEntries = ArrayValue(lease, equipment, "backpack_entries");
            GDictionary projectedEquipmentSlot = DictionaryItem(lease, projectedEquipmentSlots, 0, "equipment slot");
            GDictionary projectedBackpackEntry = DictionaryItem(lease, projectedBackpackEntries, 0, "backpack entry");
            _test.Eq(KeyOrder(projectedEquipmentSlot), EquipmentSlotKeys, "equipment slot schema must remain fixed.");
            _test.Eq(KeyOrder(projectedBackpackEntry), BackpackEntryKeys, "backpack entry schema must remain fixed.");
            GArray projectedBarriers = ArrayValue(lease, root, "barriers");
            GDictionary projectedBarrier = DictionaryItem(lease, projectedBarriers, 0, "barrier");
            _test.Eq(KeyOrder(projectedBarrier), BarrierKeys, "barrier HUD schema must remain fixed.");
            _test.Eq(projectedBarrier["remaining_tu"].AsInt32(), 90, "barrier HUD projection must retain remaining TU.");

            AssertArrayElementType(lease, root, "selected_skill_hit_stage_rates", Variant.Type.Int, "selected hit stage rates");
            AssertArrayElementType(lease, root, "recent_battle_log_lines", Variant.Type.String, "recent battle logs");
            GDictionary hitPreviewPayload = Dict(lease, root, "selected_skill_hit_preview_payload");
            AssertArrayElementType(lease, hitPreviewPayload, "stage_hit_rates", Variant.Type.Int, "attack stage hit rates");
            AssertArrayElementType(lease, hitPreviewPayload, "stage_preview_texts", Variant.Type.String, "attack stage preview texts");
            AssertArrayElementType(lease, hitPreviewPayload, "attack_roll_modifier_breakdown", Variant.Type.Dictionary, "attack modifier breakdown");
            AssertArrayElementType(lease, projectedEquipmentSlot, "occupied_slot_ids", Variant.Type.String, "equipment occupied ids");
            AssertArrayElementType(lease, projectedBackpackEntry, "allowed_slot_labels", Variant.Type.String, "backpack allowed labels");

            root["header_title"] = "污染";
            queue.Clear();
        }
        AssertAuditBaseline(baseline, "first HUD lease");

        using (GodotProjectionLease<GDictionary> repeatedLease = snapshot.BuildLease())
        {
            _test.Eq(repeatedLease.Value["header_title"].AsString(), "固定标题", "lease mutation must not mutate the typed snapshot.");
            _test.Eq(ArrayValue(repeatedLease, repeatedLease.Value, "queue_entries").Count, 2, "nested lease mutation must not mutate the typed snapshot.");
        }
        AssertAuditBaseline(baseline, "repeated HUD lease");

        BattleHoverSnapshot hover = BuildHoverSnapshot(withTarget: true);
        using (GodotProjectionLease<GDictionary> hoverLease = hover.BuildLease())
        {
            GDictionary root = hoverLease.Value;
            _test.Eq(KeyOrder(root), HoverRootKeys, "hover root key order must match the frozen outward schema.");
            _test.Eq(KeyOrder(Dict(hoverLease, root, "target_unit")), HoverTargetKeys, "filled hover target union schema must remain fixed.");
            AssertArrayElementType(hoverLease, root, "hit_stage_rates", Variant.Type.Int, "hover hit stage rates");
        }
        AssertAuditBaseline(baseline, "filled hover lease");

        using (GodotProjectionLease<GDictionary> emptyTargetLease = BuildHoverSnapshot(false).BuildLease())
            _test.Eq(Dict(emptyTargetLease, emptyTargetLease.Value, "target_unit").Count, 0, "empty hover target union must remain an empty dictionary.");
        AssertAuditBaseline(baseline, "empty hover lease");
    }

    private void TestAdapterReadsStayManaged()
    {
        BattleUnitState caster = BattleTestFixture.BuildUnit(
            "hud_typed_caster",
            "player",
            new Vector2I(3, 1),
            currentAp: 2
        );
        caster.source_member_id = "hud_member";
        BattleUnitState enemy = BattleTestFixture.BuildUnit(
            "hud_typed_enemy",
            "enemy",
            new Vector2I(2, 1)
        );
        using BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            "hud_typed_adapter",
            new Vector2I(4, 4),
            new[] { caster },
            new[] { enemy }
        );
        _test.True(
            fixture.State.InitializeObjective(
                new BattleEscapeObjectiveDefinition(
                    "right_exit",
                    BattleMapEdge.Right,
                    1
                )
            ),
            "escape objective should initialize for HUD projection."
        );
        using var adapter = new BattleHudAdapter();
        var barrier = new BattleBarrierInstanceState
        {
            BarrierInstanceId = "hud_prismatic_sphere",
            ProfileId = "prismatic_sphere",
            DisplayName = "虹光法球",
            SourceUnitId = caster.unit_id,
            SourceSkillId = "mage_prismatic_sphere",
            AnchorCoord = caster.GetAnchorCoord(),
            RadiusCells = 2,
            AreaPattern = "diamond",
            RemainingTu = 90,
        };
        barrier.SetLayers(
            new[]
            {
                new BattleBarrierLayerState
                {
                    LayerId = "red",
                    DisplayName = "红色层",
                    Order = 1,
                    Broken = true,
                },
                new BattleBarrierLayerState
                {
                    LayerId = "orange",
                    DisplayName = "橙色层",
                    Order = 2,
                    Broken = false,
                },
            }
        );
        fixture.State.PutLayeredBarrierField(barrier.BarrierInstanceId, barrier);
        BattleHudSnapshot detachedObjectiveSnapshot = adapter.BuildSnapshot(
            fixture.State,
            caster.GetAnchorCoord(),
            "",
            "",
            "",
            Array.Empty<Vector2I>(),
            1,
            Array.Empty<StringName>(),
            "",
            "测试遭遇",
            null
        );
        caster.SetAnchorCoord(new Vector2I(2, 1));
        _test.Eq(
            detachedObjectiveSnapshot.ObjectiveProgress.ReachedExitUnitCount,
            1,
            "HUD objective snapshot should remain detached from later unit movement."
        );
        BattleHudSnapshot movedSnapshot = adapter.BuildSnapshot(
            fixture.State,
            caster.GetAnchorCoord(),
            "",
            "",
            "",
            Array.Empty<Vector2I>(),
            1,
            Array.Empty<StringName>(),
            "",
            "测试遭遇",
            null
        );
        _test.Eq(
            movedSnapshot.ObjectiveProgress.ReachedExitUnitCount,
            0,
            "a new HUD snapshot should reflect current escape progress."
        );
        caster.SetAnchorCoord(new Vector2I(3, 1));

        LifecycleAuditSnapshot baseline = LifecycleAuditRegistry.Shared.CaptureSnapshot();
        for (int index = 0; index < 12; index++)
        {
            BattleHudSnapshot snapshot = adapter.BuildSnapshot(
                fixture.State,
                caster.GetAnchorCoord(),
                "",
                "",
                "",
                Array.Empty<Vector2I>(),
                1,
                Array.Empty<StringName>(),
                "",
                "测试遭遇",
                null
            );
            _test.Eq(snapshot.HeaderTitle, "测试遭遇", "adapter should return a named typed HUD snapshot.");
            _test.True(snapshot.CanonicalFacts is IReadOnlyDictionary<string, object>, "adapter canonical facts should remain managed/read-only.");
            _test.Eq(snapshot.ObjectiveProgress.Title, "逃离战场", "adapter should project the escape objective title.");
            _test.Eq(snapshot.ObjectiveProgress.ReachedExitUnitCount, 1, "adapter should project required-unit exit progress.");
            _test.Eq(snapshot.Barriers.Count, 1, "adapter should project active layered barriers.");
            _test.Eq(snapshot.Barriers[0].CurrentLayerId, "orange", "adapter should project the first unbroken layer.");
            _test.Eq(snapshot.Barriers[0].BrokenLayerCount, 1, "adapter should project broken layer count.");
            _test.True(snapshot.BarrierSummaryText.Contains("剩余 90 TU", StringComparison.Ordinal), "visible barrier summary should expose remaining TU.");
        }
        AssertAuditBaseline(baseline, "repeated adapter reads");
    }

    private void TestBossObjectiveAdapterProjection()
    {
        BattleUnitState ally = BattleTestFixture.BuildUnit(
            "hud_boss_ally",
            "player",
            new Vector2I(1, 1)
        );
        ally.source_member_id = "hud_boss_member";
        BattleUnitState boss = BattleTestFixture.BuildUnit(
            "hud_boss_target",
            "enemy",
            new Vector2I(3, 1)
        );
        boss.encounter_actor_id = "hud_boss_actor";
        boss.display_name = "红龙首领";
        using BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            "hud_boss_objective",
            new Vector2I(5, 4),
            new[] { ally },
            new[] { boss }
        );
        _test.True(
            fixture.State.InitializeObjective(
                new BattleBossObjectiveDefinition("hud_boss_actor")
            ),
            "boss objective should initialize for HUD projection."
        );
        using var adapter = new BattleHudAdapter();
        BattleHudSnapshot snapshot = adapter.BuildSnapshot(
            fixture.State,
            ally.GetAnchorCoord(),
            "",
            "",
            "",
            Array.Empty<Vector2I>(),
            0,
            Array.Empty<StringName>(),
            "",
            "首领遭遇",
            null
        );
        _test.Eq(snapshot.ObjectiveProgress.Title, "击败首领", "boss HUD 应显示目标标题。");
        _test.Eq(
            snapshot.ObjectiveProgress.TargetActorId,
            "hud_boss_actor",
            "boss HUD 应暴露稳定 actor id。"
        );
        _test.True(
            snapshot.ObjectiveProgress.ProgressText.Contains(
                "红龙首领：存活",
                StringComparison.Ordinal
            ),
            $"boss HUD 应显示当前首领状态，actual={snapshot.ObjectiveProgress.ProgressText}"
        );
    }

    private void TestInterceptObjectiveAdapterProjection()
    {
        BattleUnitState ally = BattleTestFixture.BuildUnit(
            "hud_intercept_ally",
            "player",
            new Vector2I(1, 1)
        );
        ally.source_member_id = "hud_intercept_member";
        BattleUnitState target = BattleTestFixture.BuildUnit(
            "hud_intercept_target",
            "enemy",
            new Vector2I(3, 1)
        );
        target.encounter_actor_id = "hud_intercept_actor";
        target.display_name = "迷雾信使";
        using BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            "hud_intercept_objective",
            new Vector2I(5, 4),
            new[] { ally },
            new[] { target }
        );
        _test.True(
            fixture.State.InitializeObjective(
                new BattleInterceptObjectiveDefinition(
                    "hud_intercept_actor",
                    "west_breakthrough",
                    BattleMapEdge.Left,
                    1
                )
            ),
            "intercept objective should initialize for HUD projection."
        );
        using var adapter = new BattleHudAdapter();
        BattleHudSnapshot snapshot = adapter.BuildSnapshot(
            fixture.State,
            ally.GetAnchorCoord(),
            "",
            "",
            "",
            Array.Empty<Vector2I>(),
            0,
            Array.Empty<StringName>(),
            "",
            "截击遭遇",
            null
        );

        _test.Eq(
            snapshot.ObjectiveProgress.Title,
            "截击目标",
            "intercept HUD 应显示目标标题。"
        );
        _test.Eq(
            snapshot.ObjectiveProgress.TargetActorId,
            "hud_intercept_actor",
            "intercept HUD 应暴露稳定 actor id。"
        );
        _test.Eq(
            snapshot.ObjectiveProgress.ExitEdge,
            "left",
            "intercept HUD 应暴露逃脱区边。"
        );
        _test.True(
            snapshot.ObjectiveProgress.ProgressText.Contains(
                "迷雾信使：突围中",
                StringComparison.Ordinal
            ),
            $"intercept HUD 应显示当前目标状态，actual={snapshot.ObjectiveProgress.ProgressText}"
        );
    }

    private void TestDefenseObjectiveAdapterProjection()
    {
        BattleUnitState ally = BattleTestFixture.BuildUnit(
            "hud_defense_ally",
            "player",
            Vector2I.Zero
        );
        ally.source_member_id = "hud_defense_member";
        BattleUnitState target = BattleTestFixture.BuildUnit(
            "hud_defense_target",
            "player",
            Vector2I.Right
        );
        target.encounter_actor_id = "hud_defense_actor";
        target.display_name = "迷雾守望者";
        BattleUnitState enemy = BattleTestFixture.BuildUnit(
            "hud_defense_enemy",
            "enemy",
            new Vector2I(3, 0)
        );
        using BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            "hud_defense_objective",
            new Vector2I(4, 2),
            new[] { ally, target },
            new[] { enemy }
        );
        fixture.State.timeline.current_tu = 40;
        _test.True(
            fixture.State.InitializeObjective(
                new BattleDefenseObjectiveDefinition(
                    "hud_defense_actor",
                    100
                )
            ),
            "defense objective should initialize for HUD projection."
        );
        using var adapter = new BattleHudAdapter();
        BattleHudSnapshot snapshot = adapter.BuildSnapshot(
            fixture.State,
            ally.GetAnchorCoord(),
            "",
            "",
            "",
            Array.Empty<Vector2I>(),
            0,
            Array.Empty<StringName>(),
            "",
            "防守遭遇",
            null
        );

        _test.Eq(
            snapshot.ObjectiveProgress.Title,
            "坚守防线",
            "defense HUD 应显示目标标题。"
        );
        _test.Eq(
            snapshot.ObjectiveProgress.TargetActorId,
            "hud_defense_actor",
            "defense HUD 应暴露稳定 actor id。"
        );
        _test.Eq(
            snapshot.ObjectiveProgress.StartTu,
            40,
            "defense HUD 应暴露冻结的开始 TU。"
        );
        _test.Eq(
            snapshot.ObjectiveProgress.DeadlineTu,
            140,
            "defense HUD 应暴露冻结的截止 TU。"
        );
        _test.Eq(
            snapshot.ObjectiveProgress.RemainingTu,
            100,
            "defense HUD 应显示剩余 TU。"
        );
        _test.True(
            snapshot.ObjectiveProgress.ProgressText.Contains(
                "迷雾守望者：坚守中 · 剩余 100 TU",
                StringComparison.Ordinal
            ),
            $"defense HUD 应显示目标与倒计时，actual={snapshot.ObjectiveProgress.ProgressText}"
        );
    }

    private async System.Threading.Tasks.Task TestPanelPresentationOwnership()
    {
        LifecycleAuditSnapshot prePanelBaseline =
            LifecycleAuditRegistry.Shared.CaptureSnapshot();
        const string panelScenePath = "res://scenes/ui/battle_map_panel.tscn";
        PackedScene scene = EngineAssetAccess.ResolveBorrowed<PackedScene>(panelScenePath);
        BattleMapPanel panel = scene.Instantiate<BattleMapPanel>();
        Root.AddChild(panel);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
        LifecycleAuditSnapshot readyBaseline =
            LifecycleAuditRegistry.Shared.CaptureSnapshot();

        var disabledSlot = new BattleHudSkillSlotSnapshot(
            index: 0,
            isEmpty: false,
            skillEntryId: "known:disabled_lifecycle_skill",
            skillId: "disabled_lifecycle_skill",
            displayName: "禁用技能",
            shortName: "禁",
            iconKey: "warrior_whirlwind_slash",
            hotkey: "1",
            footerText: "不可用",
            isDisabled: true,
            accentColor: Colors.Orange,
            accentDark: Colors.DarkOrange,
            edgeColor: Colors.Gold,
            disabledReason: "lifecycle fixture"
        );
        panel._apply_snapshot(
            BuildHudSnapshot(
                Array.Empty<BattleHudQueueEntrySnapshot>(),
                new[] { disabledSlot },
                Array.Empty<string>(),
                Array.Empty<BattleHudEquipmentSlotSnapshot>(),
                Array.Empty<BattleHudBackpackEntrySnapshot>(),
                BattlePresentationPayload.Empty,
                new[] { BuildBarrierSnapshot() },
                BuildBarrierSnapshot().SummaryText
            )
        );
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);

        _test.True(panel.barrier_status_label?.Visible == true, "panel should show the active barrier HUD line.");
        _test.True(panel.barrier_status_label?.Text.Contains("当前 橙色层", StringComparison.Ordinal) == true, "panel should render the current barrier layer.");

        TextureRect disabledIcon = FindDisabledSkillIcon(panel.skill_grid);
        ShaderMaterial material = disabledIcon?.Material as ShaderMaterial;
        Texture2D texture = disabledIcon?.Texture;
        Shader shader = material?.Shader;
        _test.True(disabledIcon != null, "disabled skill fixture should create a real TextureRect.");
        _test.True(disabledIcon?.Texture != null, "disabled TextureRect should retain its borrowed texture while active.");
        _test.True(disabledIcon?.Material is ShaderMaterial, "disabled TextureRect should retain the grayscale material while active.");
        _test.True(material != null, "grayscale material fixture should load.");
        _test.True(texture != null, "skill icon fixture should load.");
        _test.True(
            GodotWrapperOwnershipRegistry.IsBorrowedStaticContent(
                BattleMapPanel.BattleBoardSceneForTest()
            ),
            "path-backed battle board PackedScene should be registered borrowed."
        );
        _test.True(panel.HasPresentationLeaseForTest(), "panel should create one scene-lifetime presentation lease.");
        _test.True(GodotWrapperOwnershipRegistry.IsOwnedTransient(material), "pathless ShaderMaterial should be lease-owned.");
        _test.True(GodotWrapperOwnershipRegistry.IsBorrowedStaticContent(shader), "path-backed Shader should be registered borrowed.");
        _test.True(GodotWrapperOwnershipRegistry.IsBorrowedStaticContent(texture), "path-backed Texture2D should be registered borrowed.");

        LifecycleAuditSnapshot active = LifecycleAuditRegistry.Shared.CaptureSnapshot();
        _test.Eq(active.ActiveLeaseCount, readyBaseline.ActiveLeaseCount + 1, "panel presentation lease should be audited.");
        _test.Eq(active.ActiveOwnerCount, readyBaseline.ActiveOwnerCount + 1, "panel should own exactly the pathless material root.");
        _test.Eq(active.ActiveContentBorrowerCount, readyBaseline.ActiveContentBorrowerCount, "process-owned shader and texture assets should not create scene borrowers.");

        bool treeExitObserved = false;
        bool iconBindingsClearedBeforeFree = false;
        panel.TreeExiting += () =>
        {
            treeExitObserved = true;
            iconBindingsClearedBeforeFree =
                GodotObject.IsInstanceValid(disabledIcon)
                && disabledIcon.Material == null
                && disabledIcon.Texture == null;
        };
        panel.QueueFree();
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
        _test.True(treeExitObserved, "QueueFree should drive the real panel tree-exit path.");
        _test.True(
            iconBindingsClearedBeforeFree,
            "panel exit should detach disabled TextureRect Material/Texture before freeing its presentation owner."
        );
        _test.False(GodotObject.IsInstanceValid(material), "panel exit should dispose the pathless material.");
        _test.True(GodotObject.IsInstanceValid(shader), "panel exit must not dispose the borrowed shader.");
        _test.True(GodotObject.IsInstanceValid(texture), "panel exit must not dispose the borrowed texture.");
        AssertAuditBaseline(prePanelBaseline, "panel exit");
    }

    private static TextureRect FindDisabledSkillIcon(Node root)
    {
        if (root == null)
            return null;
        if (root is TextureRect textureRect && textureRect.Material is ShaderMaterial)
            return textureRect;
        foreach (Node child in root.GetChildren())
        {
            TextureRect found = FindDisabledSkillIcon(child);
            if (found != null)
                return found;
        }
        return null;
    }

    private static BattleHudSnapshot BuildHudSnapshot(
        IEnumerable<BattleHudQueueEntrySnapshot> queueEntries,
        IEnumerable<BattleHudSkillSlotSnapshot> skillSlots,
        IEnumerable<string> recentLines,
        IEnumerable<BattleHudEquipmentSlotSnapshot> equipmentSlots,
        IEnumerable<BattleHudBackpackEntrySnapshot> backpackEntries,
        BattlePresentationPayload hitPayload,
        IEnumerable<BattleHudBarrierSnapshot> barriers = null,
        string barrierSummaryText = ""
    )
    {
        BattleHudResourceLineSnapshot hp = new(30, 40, 0.75f, "HP", true);
        BattleHudResourceLineSnapshot hidden = new(0, 0, 0.0f, "", false);
        var resourceInfo = new BattleHudResourceInfoSnapshot(hp, hidden, hp, hidden, hp, hp);
        var focus = new BattleHudFocusUnitSnapshot(
            "施法者",
            "玩家 · 手动",
            resourceInfo,
            "施",
            "caster",
            Colors.Blue,
            Colors.DarkBlue,
            Colors.Gold,
            30,
            40,
            0,
            0,
            10,
            20,
            0,
            0,
            2,
            3,
            4,
            6
        );
        var equipment = new BattleHudEquipmentPanelSnapshot(
            "队伍共享背包（战斗局部）",
            "meta",
            "hud_typed_caster",
            "施法者",
            2,
            true,
            "",
            equipmentSlots,
            backpackEntries,
            "summary"
        );
        return new BattleHudSnapshot(
            "固定标题",
            "固定副标题",
            new BattleHudRoundBadgeSnapshot("TU 10", "READY 2"),
            "手动",
            queueEntries,
            focus,
            "测试技能",
            "默认形态",
            "技能副标题",
            skillSlots,
            "地格 (1, 1)",
            "命中 70%",
            hitPayload,
            "命中 65%",
            new[] { 65 },
            "伤害 3-8",
            3,
            8,
            BattlePresentationPayload.Empty,
            "",
            "未陷劣势",
            new[] { new BattleHudFateBadgeSnapshot("暴击门 d20", "gate", "tooltip") },
            "tooltip",
            "multi_unit",
            1,
            3,
            1,
            true,
            false,
            new BattleHudCommandDockSnapshot(true, true, false, false),
            "hint",
            recentLines,
            equipment,
            barriers,
            barrierSummaryText
        );
    }

    private static BattleHudBarrierSnapshot BuildBarrierSnapshot() =>
        new(
            "hud_prismatic_sphere",
            "prismatic_sphere",
            "虹光法球",
            "hud_typed_caster",
            "mage_prismatic_sphere",
            new Vector2I(1, 1),
            2,
            "diamond",
            90,
            "orange",
            "橙色层",
            1,
            1,
            2,
            new[] { "红色层" },
            "虹光法球 · 锚点 (1, 1) · 半径 2 · 当前 橙色层 · 已破 1/2（红色层） · 剩余 90 TU"
        );

    private static BattleHoverSnapshot BuildHoverSnapshot(bool withTarget)
    {
        BattleHoverTargetUnitSnapshot target = withTarget
            ? new BattleHoverTargetUnitSnapshot(
                "hover_target",
                "目标",
                "目",
                "target",
                Colors.Red,
                Colors.Gold,
                10,
                20,
                0,
                0,
                false,
                5,
                10,
                0,
                0,
                false,
                1,
                2,
                true,
                false
            )
            : null;
        return new BattleHoverSnapshot(
            new Vector2I(2, 2),
            true,
            true,
            BattlePresentationPayload.Empty,
            new[] { 65 },
            "命中 65%",
            Array.Empty<BattleHudFateBadgeSnapshot>(),
            BattlePresentationPayload.Empty,
            "",
            3,
            8,
            "伤害 3-8",
            target
        );
    }

    private void AssertArrayElementType(
        GodotProjectionLease<GDictionary> lease,
        GDictionary dictionary,
        string key,
        Variant.Type elementType,
        string label
    )
    {
        GArray array = ArrayValue(lease, dictionary, key);
        _test.True(array.Count > 0, $"{label} fixture must contain an element.");
        if (array.Count > 0)
            _test.Eq(array[0].VariantType, elementType, $"{label} element Variant type must remain fixed.");
    }

    private void AssertAuditBaseline(LifecycleAuditSnapshot baseline, string label)
    {
        LifecycleAuditSnapshot actual = LifecycleAuditRegistry.Shared.CaptureSnapshot();
        _test.Eq(actual.ActiveOwnerCount, baseline.ActiveOwnerCount, $"{label}: owner baseline");
        _test.Eq(actual.ActiveLeaseCount, baseline.ActiveLeaseCount, $"{label}: lease baseline");
        _test.Eq(actual.ActiveScopeCount, baseline.ActiveScopeCount, $"{label}: scope baseline");
        _test.Eq(actual.ActiveContentBorrowerCount, baseline.ActiveContentBorrowerCount, $"{label}: borrower baseline");
        _test.Eq(actual.ViolationCount, baseline.ViolationCount, $"{label}: lifecycle violations");
        _test.Eq(actual.UnknownCount, baseline.UnknownCount, $"{label}: unknown ownership");
        _test.Eq(actual.EscapedCount, baseline.EscapedCount, $"{label}: escaped ownership");
    }

    private static string KeyOrder(GDictionary dictionary)
    {
        var keys = new List<string>();
        foreach (Variant key in dictionary.Keys)
            keys.Add(key.AsString());
        return string.Join("|", keys);
    }

    private static GDictionary Dict(
        GodotProjectionLease<GDictionary> lease,
        GDictionary dictionary,
        string key
    )
    {
        if (
            dictionary == null
            || !dictionary.ContainsKey(key)
            || dictionary[key].VariantType != Variant.Type.Dictionary
        )
        {
            throw new InvalidOperationException($"Missing dictionary field: {key}.");
        }
        return lease.Own(
            dictionary[key].AsGodotDictionary(),
            $"BattleHudTypedProjectionRegression.read_dictionary:{key}"
        );
    }

    private static GArray ArrayValue(
        GodotProjectionLease<GDictionary> lease,
        GDictionary dictionary,
        string key
    )
    {
        if (
            dictionary == null
            || !dictionary.ContainsKey(key)
            || dictionary[key].VariantType != Variant.Type.Array
        )
        {
            throw new InvalidOperationException($"Missing array field: {key}.");
        }
        return lease.Own(
            dictionary[key].AsGodotArray(),
            $"BattleHudTypedProjectionRegression.read_array:{key}"
        );
    }

    private static GDictionary DictionaryItem(
        GodotProjectionLease<GDictionary> lease,
        GArray array,
        int index,
        string label
    )
    {
        if (
            array == null
            || index < 0
            || index >= array.Count
            || array[index].VariantType != Variant.Type.Dictionary
        )
        {
            throw new InvalidOperationException($"Missing dictionary item: {label}.");
        }
        return lease.Own(
            array[index].AsGodotDictionary(),
            $"BattleHudTypedProjectionRegression.read_item:{label}"
        );
    }
}
