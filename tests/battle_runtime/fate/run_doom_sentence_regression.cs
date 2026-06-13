using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_doom_sentence_regression : SceneTree
{
    private static readonly StringName DOOM_SENTENCE_SKILL_ID = "doom_sentence";
    private static readonly StringName STATUS_DOOM_SENTENCE_VERDICT = "doom_sentence_verdict";
    private static readonly StringName STATUS_PINNED = "pinned";
    private static readonly StringName STATUS_SLOW = "slow";
    private static readonly StringName WARRIOR_HEAVY_STRIKE_SKILL_ID = "warrior_heavy_strike";
    private static readonly StringName FORTUNE_MARK_TARGET_STAT_ID = "fortune_mark_target";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestDoomSentenceAppliesVerdictAndTeamwideDamageAmp();
        TestDoomSentenceLocksMainSkillOnlyAfterTwoOtherDebuffs();
        TestDoomSentenceIsLimitedToOncePerBattle();
        TestDoomSentenceIsBlockedWhenCalamityCapCannotPayCost();

        GodotSharpCleanup.CollectPendingFinalizers();
        Quit(_test.Finish("Doom sentence regression"));
    }

    private void TestDoomSentenceAppliesVerdictAndTeamwideDamageAmp()
    {
        BattleRuntimeModule runtime = BuildRuntime();
        BattleState state = BuildSkillTestState("doom_sentence_success", new Vector2I(7, 3));
        BattleUnitState caster = BuildUnit("doom_sentence_caster", "黑冕使徒", "player", new Vector2I(1, 1), 3, "hero");
        EnableDoomSentenceCap(caster);
        caster.known_active_skill_ids = new GStringNameArray { DOOM_SENTENCE_SKILL_ID };
        caster.known_skill_level_map[DOOM_SENTENCE_SKILL_ID] = 1;
        BattleUnitState allyAttacker = BuildUnit("doom_sentence_ally", "协同输出", "player", new Vector2I(1, 2), 2);
        BattleUnitState boss = BuildUnit("doom_sentence_boss", "章末 Boss", "enemy", new Vector2I(2, 1), 2, "", true);
        boss.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass), 25);
        boss.known_active_skill_ids = new GStringNameArray { WARRIOR_HEAVY_STRIKE_SKILL_ID };
        boss.known_skill_level_map[WARRIOR_HEAVY_STRIKE_SKILL_ID] = 1;
        BattleUnitState allyTarget = BuildUnit("doom_sentence_victim", "被打击者", "player", new Vector2I(3, 1), 2);

        AddUnit(runtime, state, caster);
        AddUnit(runtime, state, allyAttacker);
        AddUnit(runtime, state, boss);
        AddUnit(runtime, state, allyTarget);
        state.ally_unit_ids = new GStringNameArray { caster.unit_id, allyAttacker.unit_id, allyTarget.unit_id };
        state.enemy_unit_ids = new GStringNameArray { boss.unit_id };
        state.active_unit_id = caster.unit_id;
        runtime._state = state;
        BeginRuntimeBattle(runtime);
        runtime.calamity_by_member_id["hero"] = 5;

        GDictionary baselineDamageResult = runtime.GetDamageResolver().ResolveEffects(
            allyAttacker,
            boss.clone(),
            new GArray { BuildDamageEffect() }
        );
        BattleCommand command = BuildUnitSkillCommand(caster.unit_id, DOOM_SENTENCE_SKILL_ID, boss);
        BattlePreview preview = runtime.PreviewCommand(command);
        _test.True(preview != null && preview.allowed, "满足条件时，厄命宣判预览应允许。");
        BattleEventBatch batch = runtime.IssueCommand(command);
        _test.True(boss.HasStatusEffect(STATUS_DOOM_SENTENCE_VERDICT), "厄命宣判成功后应写入 doom_sentence_verdict。");
        _test.Eq(runtime.GetMemberCalamity("hero"), 0, "厄命宣判成功施放后应扣除 5 点 calamity。");
        _test.True(runtime.IsUnitCounterattackLocked(boss), "厄命宣判应封锁目标反击。");
        _test.True(
            batch != null && HasReportEntryWithTag(batch.report_entries, "doom_sentence_applied", "doom_sentence"),
            $"厄命宣判成功后应补出带 doom_sentence 标签的结构化战报条目。 reports={batch?.report_entries}"
        );

        GDictionary amplifiedDamageResult = runtime.GetDamageResolver().ResolveEffects(
            allyAttacker,
            boss.clone(),
            new GArray { BuildDamageEffect() }
        );
        _test.True(
            ReadInt(amplifiedDamageResult, "damage") > ReadInt(baselineDamageResult, "damage"),
            $"厄命宣判应令全队对目标造成更高伤害。 baseline={baselineDamageResult} amplified={amplifiedDamageResult}"
        );
        runtime.dispose();
    }

    private void TestDoomSentenceLocksMainSkillOnlyAfterTwoOtherDebuffs()
    {
        BattleRuntimeModule runtime = BuildRuntime();
        BattleState state = BuildSkillTestState("doom_sentence_main_skill_lock", new Vector2I(7, 3));
        BattleUnitState caster = BuildUnit("doom_sentence_lock_caster", "黑冕使徒", "player", new Vector2I(1, 1), 3, "hero");
        EnableDoomSentenceCap(caster);
        caster.known_active_skill_ids = new GStringNameArray { DOOM_SENTENCE_SKILL_ID };
        caster.known_skill_level_map[DOOM_SENTENCE_SKILL_ID] = 1;
        BattleUnitState boss = BuildUnit("doom_sentence_lock_target", "受宣判精英", "enemy", new Vector2I(2, 1), 2, "", true);
        boss.control_mode = "manual";
        boss.known_active_skill_ids = new GStringNameArray { WARRIOR_HEAVY_STRIKE_SKILL_ID };
        boss.known_skill_level_map[WARRIOR_HEAVY_STRIKE_SKILL_ID] = 1;
        BattleUnitState allyTarget = BuildUnit("doom_sentence_lock_ally", "我方目标", "player", new Vector2I(3, 1), 2);

        AddUnit(runtime, state, caster);
        AddUnit(runtime, state, boss);
        AddUnit(runtime, state, allyTarget);
        state.ally_unit_ids = new GStringNameArray { caster.unit_id, allyTarget.unit_id };
        state.enemy_unit_ids = new GStringNameArray { boss.unit_id };
        state.active_unit_id = caster.unit_id;
        runtime._state = state;
        BeginRuntimeBattle(runtime);
        runtime.calamity_by_member_id["hero"] = 5;
        runtime.IssueCommand(BuildUnitSkillCommand(caster.unit_id, DOOM_SENTENCE_SKILL_ID, boss));

        state.active_unit_id = boss.unit_id;
        state.phase = "unit_acting";
        boss.current_ap = 2;
        SetStatus(boss, STATUS_SLOW, 60, caster.unit_id);
        BattleCommand mainSkillCommand = BuildUnitSkillCommand(boss.unit_id, WARRIOR_HEAVY_STRIKE_SKILL_ID, allyTarget);
        BattlePreview oneDebuffPreview = runtime.PreviewCommand(mainSkillCommand);
        _test.True(
            oneDebuffPreview != null && oneDebuffPreview.allowed,
            "只有 1 个其他 debuff 时，主技能仍应允许。"
        );

        SetStatus(boss, STATUS_PINNED, 60, caster.unit_id);
        BattlePreview blockedPreview = runtime.PreviewCommand(mainSkillCommand);
        _test.True(
            blockedPreview != null && !blockedPreview.allowed,
            "累计到 2 个其他 debuff 后，主技能应被宣判封锁。"
        );
        _test.True(
            blockedPreview != null && blockedPreview.log_lines.Count > 0,
            $"主技能被封锁时，preview 应回传阻断反馈。 log={blockedPreview?.log_lines}"
        );

        int apBeforeIssue = boss.current_ap;
        BattleEventBatch blockedBatch = runtime.IssueCommand(mainSkillCommand);
        _test.Eq(boss.current_ap, apBeforeIssue, "主技能被厄命宣判封锁时不应扣除 AP。");
        _test.True(
            blockedBatch != null && blockedBatch.log_lines.Count > 0,
            $"主技能被封锁时，issue 应回传阻断反馈。 log={blockedBatch?.log_lines}"
        );
        runtime.dispose();
    }

    private void TestDoomSentenceIsLimitedToOncePerBattle()
    {
        BattleRuntimeModule runtime = BuildRuntime();
        BattleState state = BuildSkillTestState("doom_sentence_once_per_battle", new Vector2I(8, 3));
        BattleUnitState caster = BuildUnit("doom_sentence_once_caster", "黑冕使徒", "player", new Vector2I(1, 1), 3, "hero");
        EnableDoomSentenceCap(caster);
        caster.control_mode = "manual";
        caster.known_active_skill_ids = new GStringNameArray { DOOM_SENTENCE_SKILL_ID };
        caster.known_skill_level_map[DOOM_SENTENCE_SKILL_ID] = 1;
        BattleUnitState firstElite = BuildUnit("doom_sentence_once_target_a", "首个精英", "enemy", new Vector2I(2, 1), 2, "", true);
        BattleUnitState secondElite = BuildUnit("doom_sentence_once_target_b", "第二个精英", "enemy", new Vector2I(4, 1), 2, "", true);

        AddUnit(runtime, state, caster);
        AddUnit(runtime, state, firstElite);
        AddUnit(runtime, state, secondElite);
        state.ally_unit_ids = new GStringNameArray { caster.unit_id };
        state.enemy_unit_ids = new GStringNameArray { firstElite.unit_id, secondElite.unit_id };
        state.active_unit_id = caster.unit_id;
        runtime._state = state;
        BeginRuntimeBattle(runtime);
        runtime.calamity_by_member_id["hero"] = 10;

        runtime.IssueCommand(BuildUnitSkillCommand(caster.unit_id, DOOM_SENTENCE_SKILL_ID, firstElite));
        _test.True(firstElite.HasStatusEffect(STATUS_DOOM_SENTENCE_VERDICT), "首次施放后应成功命中首个精英。");

        SkillDef skillDef = GetSkill(runtime.GetSkillDefIndexTyped(), DOOM_SENTENCE_SKILL_ID);
        _test.True(
            !string.IsNullOrWhiteSpace(runtime.GetSkillCastBlockReason(caster, skillDef)),
            "每战 1 次用尽后，技能应进入阻断状态并提供反馈。"
        );

        caster.current_ap = 3;
        BattleCommand secondCommand = BuildUnitSkillCommand(caster.unit_id, DOOM_SENTENCE_SKILL_ID, secondElite);
        BattlePreview blockedPreview = runtime.PreviewCommand(secondCommand);
        _test.True(blockedPreview != null && !blockedPreview.allowed, "同战第二次施放厄命宣判应被 preview 拒绝。");
        int calamityBeforeIssue = runtime.GetMemberCalamity("hero");
        int apBeforeIssue = caster.current_ap;
        BattleEventBatch blockedBatch = runtime.IssueCommand(secondCommand);
        _test.Eq(runtime.GetMemberCalamity("hero"), calamityBeforeIssue, "二次施放被拒绝时不应再扣 calamity。");
        _test.Eq(caster.current_ap, apBeforeIssue, "二次施放被拒绝时不应再扣 AP。");
        _test.True(!secondElite.HasStatusEffect(STATUS_DOOM_SENTENCE_VERDICT), "二次施放被拒绝后不应污染第二个目标状态。");
        _test.True(
            blockedBatch != null && blockedBatch.log_lines.Count > 0,
            $"二次施放被拒绝时应回传阻断反馈。 log={blockedBatch?.log_lines}"
        );
        runtime.dispose();
    }

    private void TestDoomSentenceIsBlockedWhenCalamityCapCannotPayCost()
    {
        BattleRuntimeModule runtime = BuildRuntime();
        BattleState state = BuildSkillTestState("doom_sentence_cap_block", new Vector2I(6, 3));
        BattleUnitState caster = BuildUnit("doom_sentence_cap_caster", "黑冕新信徒", "player", new Vector2I(1, 1), 3, "hero");
        caster.control_mode = "manual";
        caster.known_active_skill_ids = new GStringNameArray { DOOM_SENTENCE_SKILL_ID };
        caster.known_skill_level_map[DOOM_SENTENCE_SKILL_ID] = 1;
        BattleUnitState elite = BuildUnit("doom_sentence_cap_target", "精英目标", "enemy", new Vector2I(2, 1), 2, "", true);

        AddUnit(runtime, state, caster);
        AddUnit(runtime, state, elite);
        state.ally_unit_ids = new GStringNameArray { caster.unit_id };
        state.enemy_unit_ids = new GStringNameArray { elite.unit_id };
        state.active_unit_id = caster.unit_id;
        runtime._state = state;
        BeginRuntimeBattle(runtime);
        runtime.calamity_by_member_id["hero"] = 3;

        SkillDef skillDef = GetSkill(runtime.GetSkillDefIndexTyped(), DOOM_SENTENCE_SKILL_ID);
        _test.True(
            !string.IsNullOrWhiteSpace(runtime.GetSkillCastBlockReason(caster, skillDef)),
            "当本战 calamity 上限小于 5 时，技能应进入阻断状态并提供反馈。"
        );

        BattleCommand command = BuildUnitSkillCommand(caster.unit_id, DOOM_SENTENCE_SKILL_ID, elite);
        BattlePreview blockedPreview = runtime.PreviewCommand(command);
        _test.True(
            blockedPreview != null && !blockedPreview.allowed,
            "当本战 calamity 上限不足时，preview 应拒绝厄命宣判。"
        );
        _test.True(
            blockedPreview != null && blockedPreview.log_lines.Count > 0,
            $"preview 拒绝时应回传阻断反馈。 log={blockedPreview?.log_lines}"
        );

        int apBeforeIssue = caster.current_ap;
        BattleEventBatch blockedBatch = runtime.IssueCommand(command);
        _test.Eq(caster.current_ap, apBeforeIssue, "cap 不足时 issue 不应扣除 AP。");
        _test.True(!elite.HasStatusEffect(STATUS_DOOM_SENTENCE_VERDICT), "cap 不足时目标不应获得厄命宣判。");
        _test.True(
            blockedBatch != null && blockedBatch.log_lines.Count > 0,
            $"issue 拒绝时应回传阻断反馈。 log={blockedBatch?.log_lines}"
        );
        runtime.dispose();
    }

    private BattleRuntimeModule BuildRuntime()
    {
        var registry = new ProgressionContentRegistry();
        var runtime = new BattleRuntimeModule();
        runtime.setup(
            null,
            registry.GetSkillDefsTyped(),
            new Dictionary<StringName, EnemyTemplateDef>(),
            new Dictionary<StringName, EnemyAiBrainDef>()
        );
        runtime.ConfigureDamageResolverForTests(new DeterministicBattleDamageResolver());
        runtime.ConfigureHitResolverForTests(new FixedHitResolver(10));
        return runtime;
    }

    private void BeginRuntimeBattle(BattleRuntimeModule runtime)
    {
        if (runtime == null)
            return;
        runtime.calamity_by_member_id.Clear();
        runtime.GetFateRuntime().BeginBattle(runtime.calamity_by_member_id);
    }

    private BattleState BuildSkillTestState(StringName battleId, Vector2I mapSize)
    {
        var state = new BattleState();
        state.battle_id = battleId;
        state.phase = "unit_acting";
        state.map_size = mapSize;
        state.timeline = new BattleTimelineState();
        for (int y = 0; y < mapSize.Y; y++)
        {
            for (int x = 0; x < mapSize.X; x++)
            {
                Vector2I coord = new(x, y);
                state.cells[coord] = BuildCell(coord);
            }
        }
        state.cell_columns = BattleCellState.BuildColumnsFromSurfaceCells(state.cells);
        return state;
    }

    private BattleCellState BuildCell(Vector2I coord)
    {
        var cell = new BattleCellState();
        cell.coord = coord;
        cell.base_terrain = BattleTerrainRules.ToStringName(BattleTerrainKind.Land);
        cell.base_height = 4;
        cell.height_offset = 0;
        cell.RecalculateRuntimeValues();
        return cell;
    }

    private BattleUnitState BuildUnit(
        StringName unitId,
        string displayName,
        StringName factionId,
        Vector2I coord,
        int currentAp,
        StringName sourceMemberId = default,
        bool isEliteOrBoss = false
    )
    {
        var unit = new BattleUnitState();
        unit.unit_id = unitId;
        unit.source_member_id = sourceMemberId;
        unit.display_name = displayName;
        unit.faction_id = factionId;
        unit.control_mode = "manual";
        unit.current_ap = currentAp;
        unit.current_hp = 60;
        unit.current_mp = 4;
        unit.current_stamina = 40;
        unit.current_aura = 0;
        unit.is_alive = true;
        unit.SetAnchorCoord(coord);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 60);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.MpMax), 4);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.StaminaMax), 40);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.AuraMax), 2);
        unit.attribute_snapshot.SetValue("action_points", Mathf.Max(currentAp, 1));
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.AttackBonus), 12);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass), 4);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.AttackBonus), 6);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass), 4);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.AttackBonus), 60);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass), 10);
        unit.attribute_snapshot.SetValue("hidden_luck_at_birth", 0);
        unit.attribute_snapshot.SetValue("faith_luck_bonus", 0);
        unit.attribute_snapshot.SetValue(FORTUNE_MARK_TARGET_STAT_ID, isEliteOrBoss ? 1 : 0);
        ApplyTestEquippedWeapon(unit);
        return unit;
    }

    private static void ApplyTestEquippedWeapon(BattleUnitState unit)
    {
        if (unit == null)
            return;
        unit.ApplyWeaponProjection(new GDictionary
        {
            ["weapon_profile_kind"] = "equipped",
            ["weapon_item_id"] = "test_longsword",
            ["weapon_profile_type_id"] = "longsword",
            ["weapon_current_grip"] = "one_handed",
            ["weapon_attack_range"] = 1,
            ["weapon_one_handed_dice"] = new GDictionary
            {
                ["dice_count"] = 1,
                ["dice_sides"] = 8,
                ["flat_bonus"] = 0,
            },
            ["weapon_two_handed_dice"] = new GDictionary
            {
                ["dice_count"] = 1,
                ["dice_sides"] = 10,
                ["flat_bonus"] = 0,
            },
            ["weapon_is_versatile"] = true,
            ["weapon_uses_two_hands"] = false,
            ["weapon_physical_damage_tag"] = "physical_slash",
        });
    }

    private BattleCommand BuildUnitSkillCommand(StringName unitId, StringName skillId, BattleUnitState targetUnit)
    {
        var command = new BattleCommand();
        command.command_type = BattleTypedNames.ToStringName(BattleCommandKind.Skill);
        command.unit_id = unitId;
        command.skill_id = skillId;
        command.target_unit_id = targetUnit?.unit_id ?? default;
        command.target_coord = targetUnit?.coord ?? new Vector2I(-1, -1);
        return command;
    }

    private CombatEffectDef BuildDamageEffect()
    {
        var effect = new CombatEffectDef();
        effect.effect_type = "damage";
        effect.damage_tag = "physical_slash";
        effect.power = 12;
        return effect;
    }

    private void EnableDoomSentenceCap(BattleUnitState unitState)
    {
        if (unitState?.attribute_snapshot == null)
            return;
        unitState.attribute_snapshot.SetValue("hidden_luck_at_birth", -5);
        unitState.attribute_snapshot.SetValue("calamity_capacity_bonus", 2);
    }

    private void SetStatus(
        BattleUnitState unitState,
        StringName statusId,
        int durationTu,
        StringName sourceUnitId = default,
        int power = 1
    )
    {
        if (unitState == null || statusId == default)
            return;
        var statusEntry = new BattleStatusEffectState();
        statusEntry.status_id = statusId;
        statusEntry.source_unit_id = sourceUnitId;
        statusEntry.power = Mathf.Max(power, 1);
        statusEntry.stacks = 1;
        statusEntry.duration = durationTu;
        unitState.SetStatusEffect(statusEntry);
    }

    private void AddUnit(BattleRuntimeModule runtime, BattleState state, BattleUnitState unit)
    {
        state.units[unit.unit_id] = unit;
        runtime._grid_service.PlaceUnit(state, unit, unit.coord, true);
    }

    private static bool HasReportEntryWithTag(GArray entries, StringName reasonId, StringName eventTag)
    {
        if (entries == null)
            return false;
        foreach (Variant entryValue in entries)
        {
            if (entryValue.VariantType != Variant.Type.Dictionary)
                continue;
            GDictionary entry = entryValue.AsGodotDictionary();
            if (ProgressionDataUtils.to_string_name(entry.GetValueOrDefault("reason_id", "")) != reasonId)
                continue;
            GArray eventTags = entry.GetValueOrDefault("event_tags", new GArray()).AsGodotArray();
            foreach (Variant tagValue in eventTags)
            {
                if (ProgressionDataUtils.to_string_name(tagValue) == eventTag)
                    return true;
            }
        }
        return false;
    }

    private static int ReadInt(GDictionary data, string key, int fallback = 0)
    {
        if (data == null || string.IsNullOrEmpty(key) || !data.ContainsKey(key))
            return fallback;
        Variant value = data[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static SkillDef GetSkill(IReadOnlyDictionary<StringName, SkillDef> skillDefs, StringName skillId)
    {
        if (skillDefs == null || !skillDefs.TryGetValue(skillId, out SkillDef skillDef))
            return null;
        return skillDef;
    }
}
