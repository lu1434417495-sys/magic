using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GIntArray = Godot.Collections.Array<int>;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

public partial class run_meteor_swarm_preview_surface_contract_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();
    private GDictionary _skillDefsProviderPayload = new();
    private ContentSnapshot _contentSnapshot;

    public override void _Initialize()
    {
        ProcessFrame += RunOnFirstProcessFrame;
    }

    private void RunOnFirstProcessFrame()
    {
        ProcessFrame -= RunOnFirstProcessFrame;
        _contentSnapshot = GameSessionTestFactory.GetProcessSnapshot();

        TestMeteorNumericSummaryRoundTripsFormalSaveSourcePayload();
        TestPreviewHudAndAiShareTypedFacts();
        RequestTestExit(_test.Finish("Meteor swarm preview surface contract regression"));
    }

    private void TestMeteorNumericSummaryRoundTripsFormalSaveSourcePayload()
    {
        var saveEstimate = BattleDamagePreviewSaveEstimate.Create(
            hasSave: true,
            damageBeforeSave: 30,
            damageAfterSave: 15,
            damageAfterSaveEstimate: 15,
            damageAfterSaveWorst: 15,
            damageOnSaveFailure: 30,
            damageOnSaveSuccess: 15,
            savePartialOnSuccess: true,
            saveSuccessProbabilityBasisPoints: 5000,
            saveSuccessRatePercent: 50,
            saveFailureProbabilityBasisPoints: 5000,
            dc: 18,
            ability: "willpower",
            saveTag: "magic",
            advantageState: "normal",
            abilityValue: 14,
            abilityModifier: 2,
            bonus: 1,
            immune: false,
            sources: new[]
            {
                new BattleSaveSource("meteor_source", "status", "magic", "advantage")
            }
        );
        var summary = new MeteorSwarmNumericSummary
        {
            ComponentBreakdown = new List<MeteorSwarmComponentBreakdownEntry>
            {
                new MeteorSwarmComponentBreakdownEntry
                {
                    ComponentId = "center_direct",
                    RoleLabel = "center",
                    DamageTag = "fire",
                    SaveEstimate = saveEstimate,
                    WorstSaveEstimate = saveEstimate,
                }
            }
        };

        using GodotProjectionLease<GDictionary> summaryLease =
            MeteorSwarmProjection.BuildLease(summary);
        MeteorSwarmNumericSummary roundTripped =
            MeteorSwarmNumericSummary.FromDictionary(summaryLease.Value);

        _test.Eq(roundTripped.ComponentBreakdown.Count, 1, "formal meteor summary roundtrip 应保留 component。");
        BattleDamagePreviewSaveEstimate restoredEstimate = roundTripped.ComponentBreakdown[0].SaveEstimate;
        _test.Eq(restoredEstimate.Sources.Count, 1, "formal meteor summary roundtrip 应保留 save source 数量。");
        _test.Eq(restoredEstimate.Sources[0].SourceId, new StringName("meteor_source"), "formal meteor summary roundtrip 应保留 save source id。");
        _test.Eq(restoredEstimate.Sources[0].Tag, new StringName("magic"), "formal meteor summary roundtrip 应保留 save source tag。");
        _test.Eq(restoredEstimate.Sources[0].Mode, new StringName("advantage"), "formal meteor summary roundtrip 应保留 save source mode。");
    }

    private void TestPreviewHudAndAiShareTypedFacts()
    {
        BattleUnitState enemyCenter = BuildUnit("meteor_surface_enemy_center", "中心敌人", "enemy", new Vector2I(4, 4), 160);
        BattleUnitState allyInner = BuildUnit("meteor_surface_ally_inner", "内圈友军", "player", new Vector2I(5, 4), 160);
        using Fixture setup = BuildRuntimeFixture(new Vector2I(9, 9), new[] { enemyCenter, allyInner });
        SkillDefinition skillDefinition = GetSkillDefinition(
            setup.SkillDefinitionIndex,
            "mage_meteor_swarm"
        );
        BattleCommand command = BuildCommand(setup.Caster, new Vector2I(4, 4));
        BattlePreview preview = setup.Runtime.PreviewCommand(command);
        _test.True(
            preview != null && preview.allowed,
            $"陨星雨 preview surface 合同前置应可用。{DescribePreview(preview)}"
        );
        _test.True(
            preview?.special_profile_preview_facts != null,
            $"preview 必须暴露 special_profile_preview_facts。{DescribePreview(preview)}"
        );
        if (preview == null || preview.special_profile_preview_facts == null)
            return;
        using GodotProjectionLease<GDictionary> factsLease =
            MeteorSwarmProjection.BuildLease(preview.special_profile_preview_facts);
        GDictionary factsPayload = factsLease.Value;
        string previewFactId = factsPayload.GetValueOrDefault("preview_fact_id", "").As<string>() ?? "";
        _test.True(!string.IsNullOrEmpty(previewFactId), "preview facts 必须带稳定 preview_fact_id。");
        _test.Eq(preview.hit_preview?.Source ?? "", "special_profile_preview_facts", "preview.hit_preview 应标记 special facts 来源。");
        _test.Eq(preview.hit_preview?.Source ?? "", preview.hit_preview?.Source ?? "", "preview source 应稳定。");
        _test.Eq(preview.TargetCoordsTyped.Count, 49, "preview surface 必须暴露同一份 7x7 target coords。");
        _test.True(
            factsPayload.GetValueOrDefault("target_numeric_summary", new GArray()).AsGodotArray().Count >= 2,
            "preview facts 应携带全目标数值摘要。"
        );
        _test.True(
            preview.special_profile_preview_facts.GetFriendlyFireNumericSummary().Count == 1,
            "preview facts 应携带全量友伤数值摘要。"
        );

        var hud = new BattleHudAdapter();
        BattleHudSnapshot snapshot = hud.BuildSnapshot(
            setup.Runtime.GetState(),
            new Vector2I(4, 4),
            "mage_meteor_swarm",
            "陨星雨",
            "",
            new GVector2IArray { new Vector2I(4, 4) },
            1,
            new GStringNameArray(),
            "",
            "",
            preview
        );
        _test.Eq(
            snapshot.HitPreviewPayload.Source,
            "special_profile_preview_facts",
            "HUD hit payload 应消费 special facts。"
        );
        GDictionary hudFacts = factsPayload;
        _test.Eq(
            hudFacts.GetValueOrDefault("preview_fact_id", "").As<string>() ?? "",
            previewFactId,
            "HUD 必须和 runtime preview 共用同一 preview_fact_id。"
        );
        _test.Eq(
            snapshot.SelectedSkillHitPreviewText,
            preview.hit_preview?.SummaryText ?? "",
            "HUD 应显示 runtime 提供的 summary text。"
        );

        var aiContext = new BattleAiContext();
        aiContext.state = setup.Runtime.GetState();
        aiContext.unit_state = setup.Caster;
        aiContext.grid_service = setup.Runtime.GetGridService();
        aiContext.SetSkillDefinitions(setup.SkillDefinitionIndex);
        using var scoreService = new BattleAiScoreService();
        var scoreInput = scoreService.BuildSkillScoreInput(
            aiContext,
            skillDefinition,
            command,
            preview,
            Array.Empty<CombatEffectDefinition>(),
            new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["action_kind"] = "ground_skill",
                ["action_label"] = "陨星雨",
            }
        );
        _test.True(scoreInput != null, "AI score input 应能消费 special preview facts。");
        if (scoreInput == null)
            return;
        _test.Eq(
            scoreInput.special_profile_preview_facts?.preview_fact_id.ToString() ?? "",
            previewFactId,
            "AI 必须和 runtime preview 共用同一 preview_fact_id。"
        );
        _test.Eq(scoreInput.target_coords.Count, 49, "AI target coords 必须来自同一份 7x7 preview plan。");
        _test.True(scoreInput.enemy_target_count >= 1, "AI 应识别陨星雨敌方目标。");
        _test.True(scoreInput.estimated_enemy_damage > 0, "AI 应从 typed numeric summary 估算敌方伤害。");
        _test.True(scoreInput.estimated_friendly_fire_target_count == 1, "AI 应从 friendly_fire_numeric_summary 识别友伤目标。");
        _test.True(!string.IsNullOrEmpty(scoreInput.friendly_fire_reject_reason), "AI 应把 hard friendly fire 写入 reject reason。");
        _test.True(scoreInput.attack_roll_modifier_breakdown.Count >= 1, "AI trace payload 应暴露未来尘土命中修正 breakdown。");
    }

    private Fixture BuildRuntimeFixture(Vector2I mapSize, BattleUnitState[] extraUnits)
    {
        IReadOnlyDictionary<StringName, SkillDefinition> typedSkillDefinitions =
            _contentSnapshot.Skills;
        var runtime = new BattleRuntimeModule();
        runtime.setup(
            skill_definitions: typedSkillDefinitions,
            enemy_templates: new Dictionary<StringName, EnemyTemplateDefinition>(),
            enemy_ai_brains: new Dictionary<StringName, EnemyAiBrainDefinition>(),
            item_defs: new Dictionary<StringName, ItemDefinition>(),
            battle_special_profile_view: _contentSnapshot.BattleSpecialProfiles
        );
        runtime.ConfigureHitResolverForTests(new FixedHitResolver(10));
        BattleState state = BuildState(mapSize);
        BattleUnitState caster = BuildUnit("meteor_surface_caster", "陨星术者", "player", new Vector2I(4, 0), 180);
        caster.known_active_skill_ids.Add("mage_meteor_swarm");
        caster.known_skill_level_map[new StringName("mage_meteor_swarm")] = 9;
        caster.current_ap = 4;
        caster.current_mp = 200;
        caster.current_aura = 3;
        caster.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Mp));
        caster.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Aura));
        state.SetUnit(caster);
        state.ally_unit_ids.Add(caster.unit_id);
        foreach (BattleUnitState unit in extraUnits)
        {
            if (unit == null)
                continue;
            state.SetUnit(unit);
            if (unit.faction_id == caster.faction_id)
                state.ally_unit_ids.Add(unit.unit_id);
            else
                state.enemy_unit_ids.Add(unit.unit_id);
        }
        state.active_unit_id = caster.unit_id;
        foreach (BattleUnitState unitState in state.Units())
        {
            _test.True(
                runtime._grid_service.PlaceUnit(state, unitState, unitState.coord, true),
                $"单位应能放入 preview surface 棋盘：{unitState?.unit_id}"
            );
        }
        runtime.SetupStateForTests(state);
        return new Fixture
        {
            Runtime = runtime,
            State = state,
            Caster = caster,
            SkillDefinitionIndex = typedSkillDefinitions,
        };
    }

    private static BattleState BuildState(Vector2I mapSize)
    {
        var state = new BattleState
        {
            battle_id = "meteor_swarm_preview_surface_regression",
            phase = "unit_acting",
            map_size = mapSize,
            timeline = new BattleTimelineState(),
        };
        for (int y = 0; y < mapSize.Y; y++)
        {
            for (int x = 0; x < mapSize.X; x++)
            {
                Vector2I coord = new(x, y);
                var cell = new BattleCellState
                {
                    coord = coord,
                    passable = true,
                };
                state.SetCell(coord, cell);
            }
        }
        state.RebuildCellColumns();
        return state;
    }

    private static BattleUnitState BuildUnit(
        StringName unitId,
        string displayName,
        StringName factionId,
        Vector2I coord,
        int hp
    )
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = displayName,
            faction_id = factionId,
            coord = coord,
            is_alive = true,
            current_hp = hp,
        };
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), hp);
        SeedBaseAttributesAndDeriveAc(unit);
        unit.RefreshFootprint();
        return unit;
    }

    private static void SeedBaseAttributesAndDeriveAc(BattleUnitState unit)
    {
        StringName[] baseAttributes =
        {
            "strength",
            "agility",
            "constitution",
            "perception",
            "intelligence",
            "willpower",
        };
        foreach (StringName attributeId in baseAttributes)
        {
            if (!unit.attribute_snapshot.HasValue(attributeId))
                unit.attribute_snapshot.SetValue(attributeId, 10);
        }
        if (!unit.attribute_snapshot.HasValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass)))
        {
            int agilityModifier = AttributeSnapshot.CalculateScoreModifier(
                unit.attribute_snapshot.GetValue("agility")
            );
            unit.attribute_snapshot.SetValue(
                AttributeService.ToStringName(AttributeIdKind.ArmorClass),
                System.Math.Clamp(AttributeService.BASE_ARMOR_CLASS + agilityModifier, 1, 99)
            );
        }
    }

    private static BattleCommand BuildCommand(BattleUnitState caster, Vector2I anchorCoord)
    {
        var command = new BattleCommand
        {
            command_type = BattleTypedNames.ToStringName(BattleCommandKind.Skill),
            unit_id = caster.unit_id,
            skill_entry_id = BattleSkillEntryIds.KnownSkill("mage_meteor_swarm"),
            skill_id = "mage_meteor_swarm",
            target_coord = anchorCoord,
        };
        command.AddTargetCoord(anchorCoord);
        return command;
    }

    private static SkillDefinition GetSkillDefinition(
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        StringName skillId
    )
    {
        if (
            skillDefinitions == null
            || !skillDefinitions.TryGetValue(skillId, out SkillDefinition skillDefinition)
        )
            return null;
        return skillDefinition;
    }

    private static string DictString(GDictionary dictionary, string key, string fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        Variant value = dictionary[key];
        return value.VariantType == Variant.Type.String ? value.AsString() : fallback;
    }

    private static string DescribePreview(BattlePreview preview)
    {
        if (preview == null)
            return " preview=null";
        return
            $" allowed={preview.allowed}"
            + $" logs=[{string.Join(" | ", preview.LogLinesTyped)}]"
            + $" target_coords=[{string.Join(", ", preview.TargetCoordsTyped)}]"
            + $" target_units=[{string.Join(", ", preview.TargetUnitIdsTyped)}]";
    }

    private sealed class Fixture : IDisposable
    {
        public BattleRuntimeModule Runtime;
        public BattleState State;
        public BattleUnitState Caster;
        public IReadOnlyDictionary<StringName, SkillDefinition> SkillDefinitionIndex;

        public void Dispose()
        {
            BattleTestFixture.DisposeBattleFixture(Runtime, State);
            Runtime = null;
            State = null;
            Caster = null;
            SkillDefinitionIndex = null;
        }
    }
}
