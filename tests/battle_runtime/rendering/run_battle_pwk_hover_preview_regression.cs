using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_battle_pwk_hover_preview_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestHighHpHoverHasNoBranchHitChanceOrDamageText();
        TestLowHpHoverShowsBranchTextAndHitChance();
        TestHudDoesNotParseLogLinesOrDamagePreviewText();
        RequestTestExit(_test.Finish("Battle PWK hover preview regression"));
    }

    private void TestHighHpHoverHasNoBranchHitChanceOrDamageText()
    {
        SkillDefinition skill = MakeExecuteSkill();
        BattleUnitState source = MakeUnit("pwk_source", "player", new Vector2I(0, 0), 100, 100);
        BattleUnitState target = MakeUnit("healthy_target", "enemy", new Vector2I(1, 0), 100, 21);

        using BattleTestFixture fixture = CreateFixture("pwk_high_hp_hover", skill, source, target);
        BattlePreview preview = fixture.Runtime.PreviewCommand(MakeUnitCommand(source, target, skill));
        PoisonPreview(preview);

        GDictionary hover = new BattleHudAdapter().BuildHoverPreview(
            fixture.State,
            target.coord,
            skill.SkillId,
            "",
            new Godot.Collections.Array<Vector2I>(),
            preview
        );

        _test.False(DictBool(hover, "hover_is_valid_target"), "高 HP PWK hover 应被标记为非法目标。");
        _test.Eq(DictDictionary(hover, "save_branch_preview").Count, 0, "高 HP PWK hover 不应输出分支预览。");
        _test.Eq(DictString(hover, "save_branch_preview_text"), "", "高 HP PWK hover 不应输出命中率文本。");
        _test.Eq(DictString(hover, "damage_text"), "", "高 HP PWK hover 不应输出伤害文本。");

        BattleTestFixture.DisposeBattlePreview(preview);
    }

    private void TestLowHpHoverShowsBranchTextAndHitChance()
    {
        SkillDefinition skill = MakeExecuteSkill();
        BattleUnitState source = MakeUnit("pwk_source", "player", new Vector2I(0, 0), 100, 100);
        BattleUnitState target = MakeUnit("weak_target", "enemy", new Vector2I(1, 0), 100, 20);

        using BattleTestFixture fixture = CreateFixture("pwk_low_hp_hover", skill, source, target);
        BattlePreview preview = fixture.Runtime.PreviewCommand(MakeUnitCommand(source, target, skill));
        GDictionary branchPreview = preview.save_branch_preview;

        _test.True(preview.allowed, $"低 HP PWK preview 应允许。 log={string.Join(" | ", preview.log_lines)}");
        _test.Eq(DictStringName(branchPreview, "kind"), new StringName("execute"), "低 HP PWK preview 应输出 execute 分支。");
        _test.True(DictInt(branchPreview, "hit_chance_basis_points") > 0, "低 HP PWK preview 应输出玩家命中率。");

        GDictionary hover = new BattleHudAdapter().BuildHoverPreview(
            fixture.State,
            target.coord,
            skill.SkillId,
            "",
            new Godot.Collections.Array<Vector2I> { target.coord },
            preview
        );

        string branchText = DictString(hover, "save_branch_preview_text");
        _test.True(branchText.Contains("命中率"), "低 HP PWK hover 应展示命中率。");
        _test.True(branchText.Contains("死亡律令"), "低 HP PWK hover 应展示失败分支。");
        _test.True(branchText.Contains("灵魂裂解"), "低 HP PWK hover 应展示成功分支。");

        BattleTestFixture.DisposeBattlePreview(preview);
    }

    private void TestHudDoesNotParseLogLinesOrDamagePreviewText()
    {
        SkillDefinition skill = MakeExecuteSkill();
        BattleUnitState source = MakeUnit("pwk_source", "player", new Vector2I(0, 0), 100, 100);
        BattleUnitState target = MakeUnit("weak_target", "enemy", new Vector2I(1, 0), 100, 20);

        using BattleTestFixture fixture = CreateFixture("pwk_poison_hover", skill, source, target);
        BattlePreview preview = fixture.Runtime.PreviewCommand(MakeUnitCommand(source, target, skill));
        PoisonPreview(preview);

        var adapter = new BattleHudAdapter();
        GDictionary hover = adapter.BuildHoverPreview(
            fixture.State,
            target.coord,
            skill.SkillId,
            "",
            new Godot.Collections.Array<Vector2I> { target.coord },
            preview
        );
        GDictionary snapshot = adapter.BuildSnapshot(
            fixture.State,
            target.coord,
            skill.SkillId,
            skill.DisplayName,
            "",
            new Godot.Collections.Array<Vector2I>(),
            1,
            new Godot.Collections.Array<StringName> { target.unit_id },
            "",
            "遭遇",
            preview
        );

        _test.True(
            DictString(hover, "save_branch_preview_text").Contains("命中率"),
            "hover 应使用结构化 save branch 文本。"
        );
        _test.False(
            DictString(hover, "save_branch_preview_text").Contains("POISON_LOG"),
            "hover 不应从 log_lines 解析 PWK 文案。"
        );
        _test.False(
            DictString(hover, "damage_text").Contains("999"),
            "hover 不应用 damage preview 伪造 PWK 文案。"
        );
        _test.True(
            DictString(snapshot, "selected_skill_save_branch_preview_text").Contains("命中率"),
            "HUD snapshot 应投影结构化 save branch 文本。"
        );
        _test.False(
            DictString(snapshot, "selected_skill_damage_preview_text").Contains("999"),
            "HUD snapshot 不应用 damage preview 伪造 PWK 文案。"
        );

        var overlay = new BattleHoverPreviewOverlay();
        Root.AddChild(overlay);
        overlay._Ready();
        overlay.ApplyPreview(hover);
        Label hitSummary = overlay.GetNode<Label>("HoverLayout/HitSummaryLabel");
        Label damageLabel = overlay.GetNode<Label>("HoverLayout/DamageRangeLabel");
        _test.True(hitSummary.Text.Contains("命中率"), "hover overlay 应优先渲染 save branch 文本。");
        _test.False(damageLabel.Text.Contains("999"), "hover overlay 不应渲染伪造 damage preview 文本。");
        overlay.QueueFree();

        BattleTestFixture.DisposeBattlePreview(preview);
    }

    private static BattleTestFixture CreateFixture(
        StringName battleId,
        SkillDefinition skill,
        BattleUnitState source,
        BattleUnitState target
    )
    {
        BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            battleId,
            new Vector2I(4, 2),
            new[] { source },
            new[] { target }
        );
        fixture.Runtime.setup(
            null,
            new Dictionary<StringName, SkillDefinition> { [skill.SkillId] = skill }
        );
        fixture.Runtime.SetupStateForTests(fixture.State);
        return fixture;
    }

    private static SkillDefinition MakeExecuteSkill()
    {
        CombatEffectDefinition effect = TestSkillDefinitionProjection.BuildEffect(
            "execute",
            effectTargetTeamFilter: "enemy",
            saveDcMode: "caster_spell",
            saveDc: 0,
            saveDcSourceAbility: "intelligence",
            saveAbility: "willpower",
            saveTag: "execute",
            damageTag: "negative_energy",
            thresholdMaxHpRatioPercent: 20,
            thresholdLevelAnchor: 17,
            thresholdLevelBonusPerDelta: 5,
            thresholdCapMaxHpRatioPercent: 50,
            soulFractureDurationTu: 60,
            healMultiplierPercent: 50,
            shieldGainMultiplierPercent: 50
        );
        return TestSkillDefinitionProjection.BuildSkill(
            "test_power_word_kill_hover",
            displayName: "测试律令死亡",
            maxLevel: 20,
            nonCoreMaxLevel: 20,
            combatProfile: TestSkillDefinitionProjection.BuildCombatProfile(
                "test_power_word_kill_hover",
                effects: new[] { effect },
                targetMode: "unit",
                targetTeamFilter: "enemy",
                targetSelectionMode: "single_unit",
                rangeValue: 5,
                apCost: 1,
                mpCost: 0
            )
        );
    }

    private static BattleUnitState MakeUnit(
        StringName unitId,
        StringName factionId,
        Vector2I coord,
        int maxHp,
        int currentHp
    )
    {
        BattleUnitState unit = BattleTestFixture.BuildUnit(
            unitId,
            factionId,
            coord,
            currentAp: 2,
            currentHp: currentHp
        );
        unit.current_stamina = 20;
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), maxHp);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ActionPoints), 2);
        unit.attribute_snapshot.SetValue("willpower", 10);
        unit.attribute_snapshot.SetValue("willpower_modifier", 0);
        unit.attribute_snapshot.SetValue("intelligence", 14);
        unit.attribute_snapshot.SetValue("intelligence_modifier", 2);
        unit.attribute_snapshot.SetValue("spell_proficiency_bonus", 2);
        unit.known_active_skill_ids.Add("test_power_word_kill_hover");
        unit.known_skill_level_map[new StringName("test_power_word_kill_hover")] = 17;
        return unit;
    }

    private static BattleCommand MakeUnitCommand(
        BattleUnitState source,
        BattleUnitState target,
        SkillDefinition skill
    )
    {
        var command = new BattleCommand
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = source.unit_id,
            skill_id = skill.SkillId,
            skill_entry_id = BattleSkillEntryIds.KnownSkill(skill.SkillId),
            target_unit_id = target.unit_id,
            target_coord = target.coord,
        };
        command.AddTargetUnitId(target.unit_id);
        return command;
    }

    private static void PoisonPreview(BattlePreview preview)
    {
        preview?.AddLogLine("POISON_LOG");
        preview?.SetDamagePreview(
            new BattleDamagePreviewRangeService.SkillDamagePreview(
                true,
                999,
                999,
                new List<BattleDamagePreviewRangeService.DamageEffectRange>()
            )
        );
    }

    private static GDictionary DictDictionary(GDictionary data, string key)
    {
        if (data == null || !data.ContainsKey(key))
        {
            return new GDictionary();
        }
        Variant value = data[key];
        return value.VariantType == Variant.Type.Dictionary ? value.AsGodotDictionary() : new GDictionary();
    }

    private static string DictString(GDictionary data, string key)
    {
        return data != null && data.ContainsKey(key) ? data[key].AsString() : "";
    }

    private static StringName DictStringName(GDictionary data, string key)
    {
        return data != null && data.ContainsKey(key) ? data[key].AsStringName() : new StringName("");
    }

    private static int DictInt(GDictionary data, string key)
    {
        return data != null && data.ContainsKey(key) ? data[key].AsInt32() : 0;
    }

    private static bool DictBool(GDictionary data, string key)
    {
        return data != null && data.ContainsKey(key) && data[key].AsBool();
    }
}
