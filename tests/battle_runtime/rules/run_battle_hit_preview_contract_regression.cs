using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_battle_hit_preview_contract_regression : LifecycleTestSceneTree
{
    private const string TestWorldConfig = "res://data/configs/world_map/test_world_map_config.tres";

    private static readonly StringName BLACK_CONTRACT_PUSH_SKILL_ID = "black_contract_push";
    private static readonly StringName ACTION_TITHE_VARIANT_ID = "action_tithe";
    private static readonly StringName WARRIOR_HEAVY_STRIKE_SKILL_ID = "warrior_heavy_strike";
    private static readonly StringName MYRIAD_BLADES_SKILL_ID = "warrior_myriad_blades_unity";

    private readonly TestHarness _test = new();
    private bool _ownsInstalledGameSession;
    private ContentSnapshot _contentSnapshot;

    public override void _Initialize()
    {
        ProcessFrame += RunOnFirstProcessFrame;
    }

    private void RunOnFirstProcessFrame()
    {
        ProcessFrame -= RunOnFirstProcessFrame;
        _contentSnapshot = GameSessionTestFactory.GetProcessSnapshot();
        RunAsync();
    }

    private async void RunAsync()
    {
        _TestForceHitSkillRuntimePreviewIsGuaranteed();
        _TestMyriadBladesLevelTenPreviewIsGuaranteed();
        await _TestSingleHitSkillHudSurfacesRuntimePreview();
        RequestTestExit(_test.Finish("Battle hit preview contract regression"));
    }

    private void _TestForceHitSkillRuntimePreviewIsGuaranteed()
    {
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions = _contentSnapshot.Skills;
        skillDefinitions.TryGetValue(
            BLACK_CONTRACT_PUSH_SKILL_ID,
            out SkillDefinition skillDefinition
        );
        _test.True(
            skillDefinition != null && skillDefinition.CombatProfile != null,
            "黑契推进预览前置：技能定义应存在。"
        );
        if (skillDefinition == null || skillDefinition.CombatProfile == null)
            return;

        var runtime = new BattleRuntimeModule();
        BattleState state = null;
        BattleUnitState caster = null;
        BattleUnitState target = null;
        BattleCommand command = null;
        BattlePreview preview = null;
        try
        {
            runtime.setup(
                null,
                skillDefinitions,
                new Dictionary<StringName, EnemyTemplateDefinition>(),
                new Dictionary<StringName, EnemyAiBrainDefinition>(),
                null
            );
            state = _BuildState("preview_contract_force_hit");
            caster = _BuildUnit(
                "contract_caster",
                "黑契使徒",
                "player",
                new Vector2I(1, 1),
                new List<StringName> { BLACK_CONTRACT_PUSH_SKILL_ID },
                2
            );
            target = _BuildUnit(
                "contract_target",
                "高闪避敌人",
                "enemy",
                new Vector2I(2, 1),
                new List<StringName>(),
                2
            );
            target.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass), 999);
            _AddUnitToRuntimeState(runtime, state, caster, false);
            _AddUnitToRuntimeState(runtime, state, target, true);
            state.phase = new StringName("unit_acting");
            state.active_unit_id = caster.unit_id;
            runtime.SetupStateForTests(state);

            command = _BuildSkillCommand(
                caster.unit_id,
                BLACK_CONTRACT_PUSH_SKILL_ID,
                target,
                ACTION_TITHE_VARIANT_ID
            );
            preview = runtime.PreviewCommand(command);
            _test.True(preview != null && preview.allowed, "黑契推进应能对合法目标生成 preview。");
            AttackPreviewData hitPreview = preview?.hit_preview;
            _test.Eq(hitPreview?.HitRatePercent ?? 0, 100, "黑契推进 hit_rate_percent 应为 100。");
            _test.Eq(hitPreview?.SuccessRatePercent ?? 0, 100, "黑契推进 success_rate_percent 应为 100。");
            _test.Eq(hitPreview?.StageSuccessRates?.Count ?? 0, 1, "黑契推进 stage_success_rates 长度应为 1。");
            if (hitPreview?.StageSuccessRates?.Count >= 1)
                _test.Eq(hitPreview.StageSuccessRates[0], 100, "黑契推进 stage_success_rates[0] 应为 100。");
            _test.True(hitPreview?.ForceHitNoCrit ?? false, "黑契推进 preview 应标记 force_hit_no_crit。");
            _test.True(hitPreview?.CritLocked ?? false, "黑契推进 preview 应标记 crit_locked。");
            _test.True(preview?.FatePreviewTyped?.ForceHitNoCrit ?? false, "黑契推进 preview 应携带 force-hit fate 预览。");
        }
        finally
        {
            BattleTestFixture.DisposeBattleFixture(runtime, state, command, preview, caster, target);
        }
    }

    private void _TestMyriadBladesLevelTenPreviewIsGuaranteed()
    {
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions = _contentSnapshot.Skills;
        skillDefinitions.TryGetValue(MYRIAD_BLADES_SKILL_ID, out SkillDefinition skillDefinition);
        _test.True(skillDefinition?.CombatProfile != null, "万刃归一预览前置：技能定义应存在。");
        if (skillDefinition?.CombatProfile == null)
            return;

        var runtime = new BattleRuntimeModule();
        BattleState state = null;
        BattleUnitState caster = null;
        BattleUnitState target = null;
        BattleCommand command = null;
        BattlePreview levelNineBasePreview = null;
        BattlePreview levelNineStackPreview = null;
        BattlePreview levelTenPreview = null;
        BattleEventBatch executionBatch = null;
        try
        {
            runtime.setup(
                null,
                skillDefinitions,
                new Dictionary<StringName, EnemyTemplateDefinition>(),
                new Dictionary<StringName, EnemyAiBrainDefinition>(),
                null
            );
            state = _BuildState("preview_myriad_blades_level_ten");
            caster = _BuildUnit(
                "myriad_blades_caster",
                "万刃归一使用者",
                "player",
                new Vector2I(1, 1),
                new List<StringName> { MYRIAD_BLADES_SKILL_ID },
                2
            );
            caster.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Aura));
            caster.SetCurrentAura(1000);
            caster.attribute_snapshot.SetValue("aura_max", 1000);
            caster.SetKnownSkillLevelTyped(MYRIAD_BLADES_SKILL_ID, 9);
            target = _BuildUnit(
                "myriad_blades_target",
                "万刃归一目标",
                "enemy",
                new Vector2I(2, 1),
                new List<StringName>(),
                2
            );
            target.attribute_snapshot.SetValue(
                AttributeService.ToStringName(AttributeIdKind.ArmorClass),
                25
            );
            target.SetCurrentHp(10000);
            target.attribute_snapshot.SetValue("hp_max", 10000);
            _AddUnitToRuntimeState(runtime, state, caster, false);
            _AddUnitToRuntimeState(runtime, state, target, true);
            state.phase = new StringName("unit_acting");
            state.active_unit_id = caster.unit_id;
            runtime.SetupStateForTests(state);

            command = _BuildSkillCommand(caster.unit_id, MYRIAD_BLADES_SKILL_ID, target);
            levelNineBasePreview = runtime.PreviewCommand(command);
            _test.True(
                levelNineBasePreview?.allowed == true,
                $"9级万刃归一在斗气足够时应允许预览。{string.Join(" | ", levelNineBasePreview?.log_lines ?? new System.Collections.ObjectModel.ReadOnlyCollection<string>(new List<string>()))}"
            );
            _test.False(
                levelNineBasePreview?.hit_preview?.ForceHitNoCrit ?? true,
                "9级万刃归一仍应进行攻击检定。"
            );

            caster.SetStatusEffect(
                new BattleStatusEffectState
                {
                    status_id = "melee_combo_stack",
                    source_unit_id = caster.unit_id,
                    stack_behavior = "add",
                    stack_limit = 0,
                    power = 10,
                    stacks = 10,
                    duration = 180,
                }
            );
            levelNineStackPreview = runtime.PreviewCommand(command);
            _test.Eq(
                (levelNineStackPreview?.hit_preview?.SuccessRatePercent ?? 0)
                    - (levelNineBasePreview?.hit_preview?.SuccessRatePercent ?? 0),
                10,
                "10层近战连击应使9级万刃归一命中率提高10个百分点。"
            );

            caster.SetKnownSkillLevelTyped(MYRIAD_BLADES_SKILL_ID, 10);
            target.attribute_snapshot.SetValue(
                AttributeService.ToStringName(AttributeIdKind.ArmorClass),
                999
            );
            levelTenPreview = runtime.PreviewCommand(command);
            _test.True(levelTenPreview?.allowed == true, "10级万刃归一应允许预览。");
            _test.Eq(
                levelTenPreview?.hit_preview?.SuccessRatePercent ?? 0,
                100,
                "10级万刃归一应显示100%成功率。"
            );
            _test.True(
                levelTenPreview?.hit_preview?.ForceHitNoCrit ?? false,
                "10级万刃归一预览应标记必中。"
            );
            _test.True(
                levelTenPreview?.hit_preview?.CritLocked ?? false,
                "10级万刃归一预览应锁定重击。"
            );

            int hpBefore = target.GetCurrentHp();
            executionBatch = runtime.IssueCommand(command);
            _test.True(executionBatch != null, "10级万刃归一应完成正式技能结算。");
            _test.True(
                target.GetCurrentHp() < hpBefore,
                "10级万刃归一面对极高护甲目标仍应必定命中并造成有效伤害。"
            );
            _test.Eq(caster.GetCurrentAura(), 0, "万刃归一结算后应消耗1000斗气。");
        }
        finally
        {
            BattleTestFixture.DisposeBattleFixture(
                runtime,
                state,
                command,
                levelNineBasePreview,
                levelNineStackPreview,
                levelTenPreview,
                executionBatch,
                caster,
                target
            );
        }
    }

    private async Task _TestSingleHitSkillHudSurfacesRuntimePreview()
    {
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions = _contentSnapshot.Skills;
        skillDefinitions.TryGetValue(
            WARRIOR_HEAVY_STRIKE_SKILL_ID,
            out SkillDefinition skillDefinition
        );
        _test.True(
            skillDefinition != null && skillDefinition.CombatProfile != null,
            "重击 HUD 预览前置：技能定义应存在。"
        );
        if (skillDefinition == null || skillDefinition.CombatProfile == null)
            return;

        GameSession gameSession = await _InstallTestGameSession();
        if (gameSession == null)
            return;
        var runtime = new BattleRuntimeModule();
        BattleHudAdapter adapter = null;
        BattleState state = null;
        BattleUnitState attacker = null;
        BattleUnitState target = null;
        BattleCommand command = null;
        BattlePreview preview = null;
        BattlePreview critLockedPreview = null;
        try
        {
            runtime.setup(
                null,
                skillDefinitions,
                new Dictionary<StringName, EnemyTemplateDefinition>(),
                new Dictionary<StringName, EnemyAiBrainDefinition>(),
                null
            );
            var trapDamageResolver = new TrapDamageResolver();
            BattleTestFixture.ConfigureDamageResolverForTests(runtime, trapDamageResolver);
            state = _BuildState("preview_contract_single_hit");
            attacker = _BuildUnit(
                "heavy_strike_user",
                "重击战士",
                "player",
                new Vector2I(1, 1),
                new List<StringName> { WARRIOR_HEAVY_STRIKE_SKILL_ID },
                3
            );
            attacker.SetCurrentStamina(30);
            attacker.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.AttackBonus), 80);
            target = _BuildUnit(
                "heavy_strike_target",
                "高闪避木桩",
                "enemy",
                new Vector2I(2, 1),
                new List<StringName>(),
                2
            );
            target.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass), 70);
            _AddUnitToRuntimeState(runtime, state, attacker, false);
            _AddUnitToRuntimeState(runtime, state, target, true);
            state.phase = new StringName("unit_acting");
            state.active_unit_id = attacker.unit_id;
            runtime.SetupStateForTests(state);

            command = _BuildSkillCommand(
                attacker.unit_id,
                WARRIOR_HEAVY_STRIKE_SKILL_ID,
                target
            );
            preview = runtime.PreviewCommand(command);
            _test.True(
                preview != null && preview.hit_preview != null && !preview.hit_preview.IsEmpty,
                "重击 runtime preview 应暴露命中摘要。"
            );
            _test.True((preview?.hit_preview?.HitRatePercent ?? 0) > 0, "重击 runtime preview 应暴露有效命中率。");
            _test.Eq(preview?.hit_preview?.Stages?.Count ?? 0, 1, "重击 runtime preview 应暴露单段命中预览。");
            _test.True(preview?.FatePreviewTyped?.UsesFateAttack ?? false, "重击 runtime preview 应暴露 fate 预览 payload。");
            _test.True((preview?.FatePreviewTyped?.CritGateDie ?? 0) > 0, "重击 runtime fate preview 应暴露暴击门。");
            _test.True((preview?.FatePreviewTyped?.FumbleLowEnd ?? 0) > 0, "重击 runtime fate preview 应暴露大失败区间。");
            _test.Eq(trapDamageResolver.ResolveEffectsCalls, 0, "runtime preview 不应通过 BattleDamageResolver.ResolveEffects() 偷取伤害结果。");
            _test.Eq(preview?.DamagePreviewTyped?.MinDamage ?? 0, 2, "runtime preview 应暴露非暴击基础伤害下限。");
            _test.Eq(preview?.DamagePreviewTyped?.MaxDamage ?? 0, 10, "runtime preview 应暴露非暴击基础伤害上限。");

            adapter = new BattleHudAdapter();
            adapter.SetupRuntimeContext(new BattleHudSessionContext(gameSession));
            BattleHudSnapshot snapshot = adapter.BuildSnapshot(
                state,
                target.GetAnchorCoord(),
                WARRIOR_HEAVY_STRIKE_SKILL_ID,
                skillDefinition.DisplayName,
                "",
                new Godot.Collections.Array<Vector2I>(),
                1,
                new Godot.Collections.Array<StringName>(),
                new StringName(""),
                "",
                preview
            );
            IReadOnlyList<int> snapshotStageRates = snapshot.SelectedSkillHitStageRates;
            IReadOnlyList<AttackPreviewStage> previewStages =
                preview?.hit_preview?.Stages is List<AttackPreviewStage> stages
                    ? stages
                    : Array.Empty<AttackPreviewStage>();
            _test.Eq(snapshotStageRates.Count, previewStages.Count, "HUD snapshot 应保留普通单段技能的阶段命中率数组长度。");
            for (int i = 0; i < Mathf.Min(snapshotStageRates.Count, previewStages.Count); i++)
            {
                _test.Eq(snapshotStageRates[i], previewStages[i].SuccessRatePercent, $"HUD snapshot stage rate[{i}] 应与 runtime 一致。");
            }
            _test.Eq(snapshot.SelectedSkillDamageMin, 2, "HUD snapshot 应暴露非暴击基础伤害下限。");
            _test.Eq(snapshot.SelectedSkillDamageMax, 10, "HUD snapshot 应暴露非暴击基础伤害上限。");
            IReadOnlyList<BattleHudFateBadgeSnapshot> fateBadges = snapshot.SelectedSkillFateBadges;
            _test.True(fateBadges.Count >= 3, "HUD snapshot 应保留普通 fate 攻击的 fate badges。");
            _test.True(BadgesContainText(fateBadges, "暴击门"), "HUD fate badges 应包含暴击门。");
            _test.True(BadgesContainText(fateBadges, "大失败"), "HUD fate badges 应包含大失败区间。");
            string tooltip = snapshot.SelectedSkillPreviewTooltipText;
            _test.True(tooltip.Contains("命运判定概览"), "HUD tooltip 应包含 runtime fate 预览说明。");

            BattleHoverSnapshot hoverPreview = adapter.BuildHoverPreview(
                state,
                target.GetAnchorCoord(),
                WARRIOR_HEAVY_STRIKE_SKILL_ID,
                new StringName(""),
                new Godot.Collections.Array<Vector2I> { target.GetAnchorCoord() },
                preview
            );
            IReadOnlyList<BattleHudFateBadgeSnapshot> hoverFateBadges = hoverPreview.FateBadges;
            _test.True(hoverFateBadges.Count >= 3, "HUD hover preview 应保留普通 fate 攻击的 fate badges。");

            critLockedPreview = BuildCritLockedPreview();
            BattleHudSnapshot critLockedSnapshot = adapter.BuildSnapshot(
                state,
                target.GetAnchorCoord(),
                WARRIOR_HEAVY_STRIKE_SKILL_ID,
                skillDefinition.DisplayName,
                "",
                new Godot.Collections.Array<Vector2I>(),
                1,
                new Godot.Collections.Array<StringName>(),
                new StringName(""),
                "",
                critLockedPreview
            );
            IReadOnlyList<BattleHudFateBadgeSnapshot> critLockedBadges =
                critLockedSnapshot.SelectedSkillFateBadges;
            _test.True(BadgesContainText(critLockedBadges, "禁暴击"), "HUD snapshot 暴击锁定时应显示禁暴击。");
            _test.False(BadgesContainText(critLockedBadges, "暴击门"), "HUD snapshot 暴击锁定时不应显示暴击门。");
            _test.False(BadgesContainText(critLockedBadges, "高位大成功"), "HUD snapshot 暴击锁定时不应显示高位大成功。");
            _test.True(BadgesContainText(critLockedBadges, "大失败"), "HUD snapshot 暴击锁定时仍应显示大失败区间。");
            string critLockedTooltip = critLockedSnapshot.SelectedSkillPreviewTooltipText;
            _test.True(critLockedTooltip.Contains("暴击：已封锁"), "HUD tooltip 暴击锁定时应说明暴击已封锁。");

            BattleHoverSnapshot critLockedHoverPreview = adapter.BuildHoverPreview(
                state,
                target.GetAnchorCoord(),
                WARRIOR_HEAVY_STRIKE_SKILL_ID,
                new StringName(""),
                new Godot.Collections.Array<Vector2I> { target.GetAnchorCoord() },
                critLockedPreview
            );
            IReadOnlyList<BattleHudFateBadgeSnapshot> critLockedHoverBadges =
                critLockedHoverPreview.FateBadges;
            _test.True(BadgesContainText(critLockedHoverBadges, "禁暴击"), "HUD hover 暴击锁定时应显示禁暴击。");
            _test.False(BadgesContainText(critLockedHoverBadges, "暴击门"), "HUD hover 暴击锁定时不应显示暴击门。");
            critLockedBadges = null;
            critLockedSnapshot = null;
            critLockedHoverBadges = null;
            critLockedHoverPreview = null;
            BattleTestFixture.DisposeBattlePreview(critLockedPreview);
            critLockedPreview = null;

            BattleHudSnapshot snapshotWithoutRuntimePreview = adapter.BuildSnapshot(
                state,
                target.GetAnchorCoord(),
                WARRIOR_HEAVY_STRIKE_SKILL_ID,
                skillDefinition.DisplayName,
                "",
                new Godot.Collections.Array<Vector2I>(),
                1,
                new Godot.Collections.Array<StringName>(),
                new StringName(""),
                "",
                null
            );
            _test.Eq(
                snapshotWithoutRuntimePreview.SelectedSkillHitStageRates.Count,
                0,
                "HUD snapshot 未传 runtime preview 时不应自算阶段命中率。"
            );
            _test.Eq(
                snapshotWithoutRuntimePreview.SelectedSkillDamageMin,
                0,
                "HUD snapshot 未传 runtime preview 时不应自算伤害下限。"
            );
            _test.Eq(
                snapshotWithoutRuntimePreview.SelectedSkillDamageMax,
                0,
                "HUD snapshot 未传 runtime preview 时不应自算伤害上限。"
            );
            _test.Eq(
                snapshotWithoutRuntimePreview.SelectedSkillFateBadges.Count,
                0,
                "HUD snapshot 未传 runtime preview 时不应自算 fate badges。"
            );
        }
        finally
        {
            BattleTestFixture.DisposeBattleFixture(runtime, state, command, preview, critLockedPreview, attacker, target);
            adapter?.Dispose();
            if (gameSession != null)
            {
                if (GodotObject.IsInstanceValid(gameSession))
                {
                    gameSession.ClearPersistedGame();
                    if (_ownsInstalledGameSession)
                        gameSession.QueueFree();
                }
                _ownsInstalledGameSession = false;
            }
            await ToSignal(this, SceneTree.SignalName.ProcessFrame);
        }
    }

    private async Task<GameSession> _InstallTestGameSession()
    {
        _ownsInstalledGameSession = false;
        foreach (Node child in Root.GetChildren())
        {
            if (child.Name == "GameSession")
            {
                if (child is GameSession existingSession)
                {
                    existingSession.ClearPersistedGame();
                    int reuseError = existingSession.CreateNewSave(TestWorldConfig);
                    _test.Eq(
                        reuseError,
                        (int)Error.Ok,
                        "HUD 预览回归应能复用测试 GameSession 内容上下文。"
                    );
                    return reuseError == (int)Error.Ok ? existingSession : null;
                }
                else
                {
                    child.QueueFree();
                }
            }
        }
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
        var gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        gameSession.Name = "GameSession";
        int createError = gameSession.CreateNewSave(TestWorldConfig);
        _test.Eq(
            createError,
            (int)Error.Ok,
            "HUD 预览回归应能创建测试 GameSession 内容上下文。"
        );
        if (createError != (int)Error.Ok)
        {
            gameSession.Dispose();
            return null;
        }
        _ownsInstalledGameSession = true;
        Root.AddChild(gameSession);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
        return gameSession;
    }

    private BattleState _BuildState(StringName battleId)
    {
        var state = new BattleState();
        state.battle_id = battleId;
        state.map_size = new Vector2I(4, 3);
        state.terrain_profile_id = new StringName("default");
        state.timeline = new BattleTimelineState();
        state.ClearCells();
        for (int y = 0; y < 3; y++)
        {
            for (int x = 0; x < 4; x++)
            {
                state.SetCell(new Vector2I(x, y), _BuildCell(new Vector2I(x, y)));
            }
        }
        state.RebuildCellColumns();
        state.ClearUnits();
        state.ally_unit_ids = new Godot.Collections.Array<StringName>();
        state.enemy_unit_ids = new Godot.Collections.Array<StringName>();
        return state;
    }

    private BattleCellState _BuildCell(Vector2I coord)
    {
        var cell = new BattleCellState();
        cell.coord = coord;
        cell.stack_layer = 0;
        cell.base_height = 0;
        cell.base_terrain = BattleTerrainRules.ToStringName(BattleTerrainKind.Land);
        cell.RecalculateRuntimeValues();
        return cell;
    }

    private BattleUnitState _BuildUnit(
        StringName unitId,
        string displayName,
        StringName factionId,
        Vector2I coord,
        List<StringName> skillIds,
        int currentAp
    )
    {
        var unit = new BattleUnitState();
        unit.unit_id = unitId;
        unit.display_name = displayName;
        unit.faction_id = factionId;
        unit.control_mode = new StringName("manual");
        unit.SetCurrentHp(40);
        unit.SetCurrentMp(4);
        unit.SetCurrentAp(currentAp);
        unit.SetCurrentStamina(30);
        unit.SetCurrentAura(0);
        unit.SetAnchorCoord(coord);
        unit.attribute_snapshot.SetValue(new StringName("hp_max"), 40);
        unit.attribute_snapshot.SetValue(new StringName("mp_max"), 4);
        unit.attribute_snapshot.SetValue(new StringName("stamina_max"), 30);
        unit.attribute_snapshot.SetValue(new StringName("action_points"), Mathf.Max(currentAp, 1));
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.AttackBonus), 12);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass), 10);
        unit.ApplyWeaponProjectionTyped(new WeaponProjection
        {
            weapon_profile_kind = "equipped",
            weapon_item_id = "hit_preview_test_blade",
            weapon_profile_type_id = "test_blade",
            weapon_family = "sword",
            weapon_current_grip = "one_handed",
            weapon_range_type = "melee",
            weapon_attack_range = 1,
            weapon_one_handed_dice = new WeaponDice { dice_count = 1, dice_sides = 4, flat_bonus = 0 },
            weapon_uses_two_hands = false,
            weapon_physical_damage_tag = "physical_slash",
        });
        unit.SetKnownActiveSkillIds(skillIds);
        foreach (StringName skillId in unit.GetKnownActiveSkillsViewTyped())
        {
            unit.SetKnownSkillLevelTyped(skillId, 1);
        }
        return unit;
    }

    private void _AddUnitToRuntimeState(BattleRuntimeModule runtime, BattleState state, BattleUnitState unit, bool isEnemy)
    {
        state.SetUnit(unit);
        if (isEnemy)
            state.enemy_unit_ids.Add(unit.unit_id);
        else
            state.ally_unit_ids.Add(unit.unit_id);
        bool placed = runtime._grid_service.PlaceUnit(state, unit, unit.GetAnchorCoord(), true);
        _test.True(placed, "preview contract 测试单位应成功放入战场。");
    }

    private BattleCommand _BuildSkillCommand(
        StringName unitId,
        StringName skillId,
        BattleUnitState targetUnit,
        StringName variantId = null
    )
    {
        variantId ??= new StringName("");
        var command = new BattleCommand();
        command.command_type = BattleTypedNames.ToStringName(BattleCommandKind.Skill);
        command.unit_id = unitId;
        command.skill_id = skillId;
        command.skill_entry_id = BattleSkillEntryIds.KnownSkill(skillId);
        command.skill_variant_id = variantId;
        command.target_unit_id = targetUnit?.unit_id ?? new StringName("");
        command.target_coord = targetUnit?.GetAnchorCoord() ?? new Vector2I(-1, -1);
        return command;
    }

    private static BattlePreview BuildCritLockedPreview()
    {
        return new BattlePreview
        {
            hit_preview = new AttackPreviewData
            {
                SummaryText = "预计命中率 50%",
                SuccessRatePercent = 50,
                Stages = new List<AttackPreviewStage>
                {
                    new AttackPreviewStage(50, 50, 50, 11, 11, "50%"),
                },
                FatePreview = new BattleFatePreviewData
                {
                    UsesFateAttack = true,
                    CritLocked = true,
                    CritGateDie = 20,
                    FumbleLowEnd = 1,
                    CritThreshold = 17,
                },
            },
        };
    }

    private static bool BadgesContainText(
        IEnumerable<BattleHudFateBadgeSnapshot> badges,
        string fragment
    )
    {
        if (badges == null || string.IsNullOrEmpty(fragment))
            return false;
        foreach (BattleHudFateBadgeSnapshot badge in badges)
        {
            string text = badge?.Text ?? "";
            if (text.Contains(fragment))
                return true;
        }
        return false;
    }

}
