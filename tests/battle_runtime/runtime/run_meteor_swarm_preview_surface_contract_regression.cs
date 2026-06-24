using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GIntArray = Godot.Collections.Array<int>;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

public partial class run_meteor_swarm_preview_surface_contract_regression : SceneTree
{
    private readonly TestHarness _test = new();
    private GDictionary _skillDefsProviderPayload = new();

    public override void _Initialize()
    {
        TestMeteorNumericSummaryRoundTripsFormalSaveSourcePayload();
        TestPreviewHudAndAiShareTypedFacts();
        Quit(_test.Finish("Meteor swarm preview surface contract regression"));
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

        MeteorSwarmNumericSummary roundTripped = MeteorSwarmNumericSummary.FromDictionary(
            MeteorSwarmProjection.Project(summary)
        );

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
        Fixture setup = BuildRuntimeFixture(new Vector2I(9, 9), new[] { enemyCenter, allyInner });
        SkillDef skillDef = GetSkill(setup.SkillDefs, "mage_meteor_swarm");
        BattleCommand command = BuildCommand(setup.Caster, new Vector2I(4, 4));
        BattlePreview preview = setup.Runtime.PreviewCommand(command);
        _test.True(preview != null && preview.allowed, "陨星雨 preview surface 合同前置应可用。");
        _test.True(preview.special_profile_preview_facts != null, "preview 必须暴露 special_profile_preview_facts。");
        if (preview == null || preview.special_profile_preview_facts == null)
            return;
        GDictionary factsPayload = MeteorSwarmProjection.Project(
            preview.special_profile_preview_facts
        );
        string previewFactId = factsPayload.GetValueOrDefault("preview_fact_id", "").As<string>() ?? "";
        _test.True(!string.IsNullOrEmpty(previewFactId), "preview facts 必须带稳定 preview_fact_id。");
        _test.Eq(preview.hit_preview?.Source ?? "", "special_profile_preview_facts", "preview.hit_preview 应标记 special facts 来源。");
        _test.Eq(preview.hit_preview?.Source ?? "", preview.hit_preview?.Source ?? "", "preview source 应稳定。");
        _test.Eq(preview.target_coords.Count, 49, "preview surface 必须暴露同一份 7x7 target coords。");
        _test.True(
            factsPayload.GetValueOrDefault("target_numeric_summary", new GArray()).AsGodotArray().Count >= 2,
            "preview facts 应携带全目标数值摘要。"
        );
        _test.True(
            preview.special_profile_preview_facts.GetFriendlyFireNumericSummary().Count == 1,
            "preview facts 应携带全量友伤数值摘要。"
        );

        var hud = new BattleHudAdapter();
        GDictionary snapshot = hud.BuildSnapshot(
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
        GDictionary hitPreviewPayload = snapshot
            .GetValueOrDefault("selected_skill_hit_preview_payload", new Variant())
            .AsGodotDictionary();
        _test.Eq(
            DictString(hitPreviewPayload, "source", ""),
            "special_profile_preview_facts",
            "HUD hit payload 应消费 special facts。"
        );
        GDictionary hudFacts = MeteorSwarmProjection.Project(
            preview.special_profile_preview_facts
        );
        _test.Eq(
            hudFacts.GetValueOrDefault("preview_fact_id", "").As<string>() ?? "",
            previewFactId,
            "HUD 必须和 runtime preview 共用同一 preview_fact_id。"
        );
        _test.Eq(
            snapshot.GetValueOrDefault("selected_skill_hit_preview_text", "").As<string>() ?? "",
            preview.hit_preview?.SummaryText ?? "",
            "HUD 应显示 runtime 提供的 summary text。"
        );

        var aiContext = new BattleAiContext();
        aiContext.state = setup.Runtime.GetState();
        aiContext.unit_state = setup.Caster;
        aiContext.grid_service = setup.Runtime.GetGridService();
        aiContext.SetSkillDefs(setup.SkillDefIndex);
        var scoreService = new BattleAiScoreService();
        var scoreInput = scoreService.BuildSkillScoreInput(
            aiContext,
            skillDef,
            command,
            preview,
            Array.Empty<CombatEffectDef>(),
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
        var progressionRegistry = new ProgressionContentRegistry();
        IReadOnlyDictionary<StringName, SkillDef> typedSkillDefs =
            progressionRegistry.GetSkillDefsTyped();
        var specialRegistry = new BattleSpecialProfileRegistry();
        specialRegistry.Rebuild(typedSkillDefs);
        _test.True(specialRegistry.Validate().Count == 0, "正式 special profile registry 应可用于 preview surface fixture。");
        var runtime = new BattleRuntimeModule();
        runtime.setup(
            null,
            typedSkillDefs,
            new Dictionary<StringName, EnemyTemplateDef>(),
            new Dictionary<StringName, EnemyAiBrainDef>(),
            null,
            null,
            new Dictionary<StringName, ItemDef>(),
            null,
            default,
            specialRegistry.GetSnapshot()
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
        foreach (Variant unitValue in state.Units())
        {
            BattleUnitState unitState = unitValue.AsGodotObject() as BattleUnitState;
            _test.True(
                runtime._grid_service.PlaceUnit(state, unitState, unitState.coord, true),
                $"单位应能放入 preview surface 棋盘：{unitState?.unit_id}"
            );
        }
        runtime.SetupStateForTests(state);
        return new Fixture
        {
            Runtime = runtime,
            Caster = caster,
            SkillDefIndex = typedSkillDefs,
            SkillDefs = ProjectSkillDefs(typedSkillDefs),
        };
    }

    private static GDictionary ProjectSkillDefs(
        IReadOnlyDictionary<StringName, SkillDef> skillDefs
    )
    {
        GDictionary result = new();
        if (skillDefs == null)
            return result;
        foreach ((StringName skillId, SkillDef skillDef) in skillDefs)
        {
            if (skillId == "" || skillDef == null)
                continue;
            result[skillId] = skillDef;
        }
        return result;
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
            skill_id = "mage_meteor_swarm",
            target_coord = anchorCoord,
        };
        command.AddTargetCoord(anchorCoord);
        return command;
    }

    private static SkillDef GetSkill(GDictionary skillDefs, StringName skillId)
    {
        if (skillDefs == null || !skillDefs.ContainsKey(skillId))
            return null;
        return skillDefs[skillId].AsGodotObject() as SkillDef;
    }

    private static string DictString(GDictionary dictionary, string key, string fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        Variant value = dictionary[key];
        return value.VariantType == Variant.Type.String ? value.AsString() : fallback;
    }

    private sealed class Fixture
    {
        public BattleRuntimeModule Runtime;
        public BattleUnitState Caster;
        public IReadOnlyDictionary<StringName, SkillDef> SkillDefIndex;
        public GDictionary SkillDefs;
    }
}
