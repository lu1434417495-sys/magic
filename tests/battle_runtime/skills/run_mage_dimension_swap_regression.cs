using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_mage_dimension_swap_regression : LifecycleTestSceneTree
{
    private const string SkillPath =
        "mage_dimension_swap";
    private static readonly StringName SkillId = "mage_dimension_swap";
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            SkillDefinition skill = LoadSkill();
            TestAuthoredContractAndLevels(skill);
            TestSchemaRejectsInvalidSwap(skill);
            TestMasteryAmounts(skill);
            TestCanonicalPreviewAndAllyExecution(skill);
            TestInvalidDestinationRejectsBeforeCost(skill);
            TestEnemyImmunityConsumesCastWithoutSwap(skill);
            TestForcedMoveImmunityOnlyBlocksHostileSwap(skill);
            TestBarrierBoundaryRejectsBeforeCost(skill);
            TestAiClassificationAndThreatScore(skill);
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        RequestTestExit(_test.Finish("Mage dimension swap regression"));
    }

    private void TestAuthoredContractAndLevels(SkillDefinition skill)
    {
        CombatSkillDefinition combat = skill?.CombatProfile;
        _test.True(combat != null, "移形换位正式资源与 combat_profile 应可加载。");
        if (combat == null)
            return;
        _test.Eq(skill.SkillId, SkillId, "技能 ID 应保持 mage_dimension_swap。");
        _test.Eq(combat.TargetMode, new StringName("unit"), "必须选择单位目标。");
        _test.Eq(combat.TargetTeamFilter, new StringName("any"), "应允许友军和敌军目标。");
        _test.True(combat.RequiresLos, "换位必须要求可见目标。");
        _test.Eq(combat.MasteryTriggerModeKind, CombatSkillMasteryTriggerMode.EffectApplied, "只有成功交换才给熟练度。");
        _test.Eq(combat.MasteryAmountModeKind, CombatSkillMasteryAmountMode.PerTargetRank, "熟练度应按目标阶级结算。");
        _test.Eq(combat.MasteryBaseAmount, 10, "每次成功换位的基础熟练度应为 10。");
        _test.Eq(skill.MasteryCurve.Count, 7, "熟练度曲线应覆盖七次升级。");
        _test.Eq(skill.MasteryCurve[0], 100, "首级阈值应为 100。");
        _test.Eq(skill.MasteryCurve[6], 2300, "末级阈值应为 2300。");
        _test.Eq(combat.EffectDefinitions.Count, 1, "资源应只声明一个换位效果。");
        CombatEffectDefinition effect = combat.EffectDefinitions[0];
        _test.Eq(effect.EffectKind, BattleEffectKind.PositionSwap, "效果必须投影为 typed position_swap。");
        _test.True(effect.ExcludeSource, "换位不能以自己为目标。");
        _test.Eq(effect.SaveDcModeKind, BattleSaveDcMode.CasterSpell, "敌方换位应使用施法者法术 DC。");
        _test.Eq(effect.SaveAbility, new StringName("willpower"), "敌方应进行意志豁免。");

        AssertLevel(combat, 0, 3, 2, 80, 16, 180);
        AssertLevel(combat, 1, 3, 2, 80, 14, 180);
        AssertLevel(combat, 2, 4, 2, 80, 14, 180);
        AssertLevel(combat, 3, 4, 2, 80, 14, 150);
        AssertLevel(combat, 4, 4, 2, 70, 14, 150);
        AssertLevel(combat, 5, 5, 2, 70, 14, 150);
        AssertLevel(combat, 6, 5, 2, 70, 12, 150);
        AssertLevel(combat, 7, 5, 2, 70, 12, 120);
        string levelSeven = SkillLevelDescriptionFormatter.BuildLevelDescription(
            skill,
            7,
            new GDictionary()
        );
        _test.True(levelSeven.Contains("70法力/12体力"), "7级文本应显示最终资源消耗。");
        _test.True(levelSeven.Contains("120TU"), "7级文本应显示最终冷却。");
    }

    private void TestSchemaRejectsInvalidSwap(SkillDefinition skill)
    {
        using var effect = new CombatEffectDef
        {
            effect_type = BattleTypedNames.EffectPositionSwap,
            effect_target_team_filter = "enemy",
            save_dc_mode = "caster_spell",
            save_dc_source_ability = "intelligence",
            save_ability = "willpower",
            save_tag = "magic",
        };
        using var profile = new CombatSkillDef
        {
            skill_id = "invalid_position_swap",
            target_mode = "unit",
            target_team_filter = "enemy",
            target_selection_mode = "single_unit",
            requires_los = false,
        };
        profile.effect_defs.Add(effect);
        using var skillDef = new SkillDef
        {
            skill_id = "invalid_position_swap",
            combat_profile = profile,
        };
        var errors = new GStringArray();
        new SkillCombatProfileValidator(
            new SkillDamageEffectValidator(),
            new SkillExecuteEffectValidator()
        ).AppendCombatProfileValidationErrors(errors, skillDef.skill_id, profile, skillDef);
        _test.True(ErrorsContain(errors, "target_team_filter any"), "非 any 的换位目标筛选必须被拒绝。");
        _test.True(ErrorsContain(errors, "requires_los=true"), "不要求视线的换位必须被拒绝。");
        _test.True(ErrorsContain(errors, "exclude_source=true"), "允许自选的换位必须被拒绝。");
        _test.True(skill?.CombatProfile?.MasteryBaseAmount == 10, "正式资源应保持有效，负控不得污染投影。");
    }

    private void TestMasteryAmounts(SkillDefinition skill)
    {
        using var mastery = new BattleSkillMasteryService();
        BattleUnitState source = BuildCaster("mastery_source", new Vector2I(1, 1));
        BattleUnitState ally = BuildUnit("mastery_ally", "player", new Vector2I(2, 1));
        BattleUnitState normal = BuildUnit("mastery_normal", "enemy", new Vector2I(3, 1));
        BattleUnitState elite = BuildUnit("mastery_elite", "enemy", new Vector2I(4, 1));
        BattleUnitState boss = BuildUnit("mastery_boss", "enemy", new Vector2I(5, 1));
        elite.attribute_snapshot.SetValue("fortune_mark_target", 1);
        boss.attribute_snapshot.SetValue("boss_target", 1);
        _test.Eq(mastery.ResolveTargetMasteryAmount(source, ally, skill), 10, "友军成功换位应给 10 熟练度。");
        _test.Eq(mastery.ResolveTargetMasteryAmount(source, normal, skill), 10, "普通敌人应给 10 熟练度。");
        _test.Eq(mastery.ResolveTargetMasteryAmount(source, elite, skill), 20, "精英敌人应给 20 熟练度。");
        _test.Eq(mastery.ResolveTargetMasteryAmount(source, boss, skill), 30, "Boss 应给 30 熟练度。");
        DisposeUnits(source, ally, normal, elite, boss);
    }

    private void TestCanonicalPreviewAndAllyExecution(SkillDefinition skill)
    {
        BattleUnitState caster = BuildCaster("ally_swap_caster", new Vector2I(1, 1));
        BattleUnitState ally = BuildUnit("ally_swap_target", "player", new Vector2I(3, 1));
        var gateway = new MasteryGatewayStub();
        using BattleTestFixture fixture = CreateFixture(skill, caster, ally, gateway);
        BattleCommand command = BuildCommand(caster, ally);
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.True(preview?.allowed == true, $"友军换位应可预览。logs={JoinLogs(preview)}");
        BattlePositionSwapPreviewData swap = preview?.PositionSwapPreviewTyped;
        _test.True(swap != null, "canonical preview 应包含 typed 换位数据。");
        _test.Eq(swap?.SourceTo ?? new Vector2I(-1, -1), new Vector2I(3, 1), "预览应公开施法者落点。");
        _test.Eq(swap?.TargetTo ?? new Vector2I(-1, -1), new Vector2I(1, 1), "预览应公开目标落点。");
        _test.False(swap?.RequiresEnemySave ?? true, "友军换位无需豁免。");
        _test.Eq(swap?.SwapProbabilityBasisPoints ?? 0, 10000, "友军换位成功率应为 100%。");
        _test.Eq(caster.GetAnchorCoord(), new Vector2I(1, 1), "预览不得修改正式坐标。");

        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        _test.Eq(caster.GetAnchorCoord(), new Vector2I(3, 1), "执行后施法者应到友军原位置。");
        _test.Eq(ally.GetAnchorCoord(), new Vector2I(1, 1), "执行后友军应到施法者原位置。");
        _test.Eq(caster.GetCurrentAp(), 0, "0级应消耗 2 AP。");
        _test.Eq(caster.GetCurrentMp(), 20, "0级应消耗 80 法力。");
        _test.Eq(caster.GetCurrentStamina(), 84, "0级应消耗 16 体力。");
        _test.Eq(caster.GetCooldownTyped(SkillId), 180, "0级应进入 180TU 冷却。");
        _test.Eq(gateway.Grants.Count, 1, "一次成功换位只能提交一笔熟练度。");
        if (gateway.Grants.Count == 1)
            _test.Eq(gateway.Grants[0].MasteryAmount, 10, "友军成功换位应提交 10 熟练度。");
        batch?.Dispose();
        Dispose(command, preview);
    }

    private void TestInvalidDestinationRejectsBeforeCost(SkillDefinition skill)
    {
        BattleUnitState caster = BuildCaster("blocked_caster", new Vector2I(1, 1));
        BattleUnitState target = BuildUnit("large_target", "player", new Vector2I(4, 1));
        target.SetBodySizeCategory("large");
        BattleUnitState blocker = BuildUnit("destination_blocker", "enemy", new Vector2I(2, 2));
        using BattleTestFixture fixture = CreateFixture(skill, caster, target, null, blocker);
        AssertRejectedWithoutCost(fixture, caster, target, "大体型目标的目标占位被第三方占用");
    }

    private void TestEnemyImmunityConsumesCastWithoutSwap(SkillDefinition skill)
    {
        BattleUnitState caster = BuildCaster("save_caster", new Vector2I(1, 1));
        BattleUnitState enemy = BuildUnit("save_enemy", "enemy", new Vector2I(3, 1));
        enemy.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = "magic_swap_immunity",
                stacks = 1,
                duration = 60,
                save_immunity_tags = new List<StringName> { "magic" },
            }
        );
        var gateway = new MasteryGatewayStub();
        using BattleTestFixture fixture = CreateFixture(skill, caster, enemy, gateway);
        BattleCommand command = BuildCommand(caster, enemy);
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.True(preview?.allowed == true, "敌方魔法豁免免疫不应让施法前目标失效。");
        _test.Eq(preview?.PositionSwapPreviewTyped?.SwapProbabilityBasisPoints ?? -1, 0, "免疫目标的交换概率应为 0。");
        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        _test.Eq(caster.GetAnchorCoord(), new Vector2I(1, 1), "豁免成功时施法者不移动。");
        _test.Eq(enemy.GetAnchorCoord(), new Vector2I(3, 1), "豁免成功时敌人不移动。");
        _test.Eq(caster.GetCurrentMp(), 20, "敌方豁免成功仍应支付法力。");
        _test.Eq(caster.GetCooldownTyped(SkillId), 180, "敌方豁免成功仍应进入冷却。");
        _test.Eq(gateway.Grants.Count, 0, "未交换不得获得熟练度。");
        _test.True(batch?.LogLinesTyped.Any(line => line.Contains("免疫位置交换")) == true, "日志应公开免疫结果。");
        batch?.Dispose();
        Dispose(command, preview);
    }

    private void TestForcedMoveImmunityOnlyBlocksHostileSwap(SkillDefinition skill)
    {
        BattleUnitState enemyCaster = BuildCaster("forced_enemy_caster", new Vector2I(1, 1));
        BattleUnitState immuneEnemy = BuildUnit("forced_enemy", "enemy", new Vector2I(3, 1));
        AddForcedMoveImmunity(immuneEnemy);
        using (BattleTestFixture fixture = CreateFixture(skill, enemyCaster, immuneEnemy))
            AssertRejectedWithoutCost(fixture, enemyCaster, immuneEnemy, "敌方强制位移免疫");

        BattleUnitState allyCaster = BuildCaster("forced_ally_caster", new Vector2I(1, 1));
        BattleUnitState immuneAlly = BuildUnit("forced_ally", "player", new Vector2I(3, 1));
        AddForcedMoveImmunity(immuneAlly);
        using BattleTestFixture allyFixture = CreateFixture(skill, allyCaster, immuneAlly);
        BattleCommand command = BuildCommand(allyCaster, immuneAlly);
        BattlePreview preview = allyFixture.Runtime.PreviewCommand(command);
        _test.True(preview?.allowed == true, "友军自愿换位应忽略强制位移免疫。");
        BattleEventBatch batch = allyFixture.Runtime.IssueCommand(command);
        _test.Eq(allyCaster.GetAnchorCoord(), new Vector2I(3, 1), "友军免疫不应阻止自愿换位。");
        batch?.Dispose();
        Dispose(command, preview);
    }

    private void TestBarrierBoundaryRejectsBeforeCost(SkillDefinition skill)
    {
        BattleUnitState caster = BuildCaster("barrier_caster", new Vector2I(1, 1));
        BattleUnitState ally = BuildUnit("barrier_ally", "player", new Vector2I(3, 1));
        using BattleTestFixture fixture = CreateFixture(skill, caster, ally);
        var barrier = new BattleBarrierInstanceState
        {
            BarrierInstanceId = "swap_test_barrier",
            ProfileId = "swap_test_barrier",
            DisplayName = "测试屏障",
            SourceUnitId = "other_unit",
            AnchorCoord = ally.GetAnchorCoord(),
            RadiusCells = 0,
            AreaPattern = "diamond",
            RemainingTu = 100,
        };
        fixture.State.PutLayeredBarrierFieldPayload(
            barrier.BarrierInstanceId,
            barrier.ToRuntimeDict()
        );
        AssertRejectedWithoutCost(fixture, caster, ally, "屏障边界");
    }

    private void TestAiClassificationAndThreatScore(SkillDefinition skill)
    {
        BattleAiSkillAffordanceRecord affordance =
            new BattleAiSkillAffordanceClassifier().ClassifySkill(skill, 0);
        _test.True(affordance.is_generatable, "AI 应能为 typed 换位生成候选。");
        _test.Eq(affordance.team_intent, new StringName("mixed"), "换位应同时具有支援和敌对意图。");
        _test.True(affordance.effect_roles.Contains(new StringName("position_swap")), "AI affordance 应公开 position_swap 角色。");

        BattleUnitState caster = BuildCaster("ai_caster", new Vector2I(1, 1));
        BattleUnitState endangeredAlly = BuildUnit("ai_ally", "player", new Vector2I(2, 1));
        BattleUnitState threat = BuildUnit("ai_threat", "enemy", new Vector2I(3, 1));
        ApplyThreatWeapon(threat);
        using BattleTestFixture fixture = CreateFixture(skill, caster, endangeredAlly, null, threat);
        BattleCommand command = BuildCommand(caster, endangeredAlly);
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        var context = new BattleAiContext
        {
            state = fixture.State,
            unit_state = caster,
            grid_service = fixture.Runtime.GetGridService(),
        };
        context.SetSkillDefinitions(
            new Dictionary<StringName, SkillDefinition> { [SkillId] = skill }
        );
        using var scoreService = new BattleAiScoreService();
        BattleAiScoreInput score = scoreService.BuildSkillScoreInput(
            context,
            skill,
            command,
            preview,
            skill.CombatProfile.EffectDefinitions
        );
        _test.True(score.position_swap_utility_score > 0, "把受威胁友军换出近战范围应获得正换位效用。");
        _test.Eq(score.position_swap_success_probability_basis_points, 10000, "友军换位评分应按 100% 成功率计算。");
        _test.Eq(score.effective_target_count, 1, "成功可执行的换位候选应计一个有效目标。");
        Dispose(command, preview);
    }

    private void AssertRejectedWithoutCost(
        BattleTestFixture fixture,
        BattleUnitState caster,
        BattleUnitState target,
        string label
    )
    {
        BattleCommand command = BuildCommand(caster, target);
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.True(preview != null && !preview.allowed, $"{label}必须在预览阶段拒绝。logs={JoinLogs(preview)}");
        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        _test.Eq(caster.GetCurrentAp(), 2, $"{label}不得扣 AP。");
        _test.Eq(caster.GetCurrentMp(), 100, $"{label}不得扣法力。");
        _test.Eq(caster.GetCurrentStamina(), 100, $"{label}不得扣体力。");
        _test.Eq(caster.GetCooldownTyped(SkillId), 0, $"{label}不得启动冷却。");
        batch?.Dispose();
        Dispose(command, preview);
    }

    private void AssertLevel(
        CombatSkillDefinition combat,
        int level,
        int range,
        int ap,
        int mp,
        int stamina,
        int cooldown
    )
    {
        CombatSkillResourceCosts costs = combat.GetEffectiveResourceCostValues(level);
        _test.Eq(combat.GetEffectiveRangeValue(level), range, $"{level}级射程错误。");
        _test.Eq(costs.ApCost, ap, $"{level}级 AP 错误。");
        _test.Eq(costs.MpCost, mp, $"{level}级法力错误。");
        _test.Eq(costs.StaminaCost, stamina, $"{level}级体力错误。");
        _test.Eq(costs.CooldownTu, cooldown, $"{level}级冷却错误。");
    }

    private static SkillDefinition LoadSkill() =>
        TestSkillDefinitionProjection.LoadSkillDefinition(
            SkillPath,
            "mage_dimension_swap_regression"
        );

    private static BattleTestFixture CreateFixture(
        SkillDefinition skill,
        BattleUnitState caster,
        BattleUnitState target,
        IBattleRuntimeCharacterGateway gateway = null,
        params BattleUnitState[] otherUnits
    )
    {
        var allies = new List<BattleUnitState>();
        var enemies = new List<BattleUnitState>();
        foreach (BattleUnitState unit in new[] { caster, target }.Concat(otherUnits ?? Array.Empty<BattleUnitState>()))
        {
            if (unit.faction_id == caster.faction_id)
                allies.Add(unit);
            else
                enemies.Add(unit);
        }
        if (enemies.Count == 0)
            enemies.Add(BuildUnit($"fixture_dummy_{caster.unit_id}", "enemy", new Vector2I(7, 4)));
        BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            $"dimension_swap_{caster.unit_id}",
            new Vector2I(9, 6),
            allies,
            enemies
        );
        fixture.Runtime.setup(
            gateway,
            new Dictionary<StringName, SkillDefinition> { [SkillId] = skill }
        );
        fixture.Runtime.SetupStateForTests(fixture.State);
        fixture.State.active_unit_id = caster.unit_id;
        return fixture;
    }

    private static BattleUnitState BuildCaster(StringName id, Vector2I coord)
    {
        BattleUnitState caster = BuildUnit(id, "player", coord);
        caster.source_member_id = $"member_{id}";
        caster.AddKnownActiveSkill(SkillId);
        caster.SetKnownSkillLevelTyped(SkillId, 0, preserveZero: true);
        caster.UnlockCombatResource("mp");
        caster.attribute_snapshot.SetValue(AttributeService.INTELLIGENCE_MODIFIER, 3);
        caster.attribute_snapshot.SetValue("spell_proficiency_bonus", 2);
        return caster;
    }

    private static BattleUnitState BuildUnit(StringName id, StringName faction, Vector2I coord)
    {
        BattleUnitState unit = BattleTestFixture.BuildUnit(
            id,
            faction,
            coord,
            currentAp: 2,
            currentHp: 50
        );
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 50);
        unit.attribute_snapshot.SetValue(AttributeService.MP_MAX, 100);
        unit.attribute_snapshot.SetValue(AttributeService.STAMINA_MAX, 100);
        unit.SetCurrentHp(50);
        unit.SetCurrentAp(2);
        unit.SetCurrentMp(100);
        unit.SetCurrentStamina(100);
        return unit;
    }

    private static BattleCommand BuildCommand(BattleUnitState caster, BattleUnitState target) =>
        new()
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = caster.unit_id,
            skill_entry_id = BattleSkillEntryIds.KnownSkill(SkillId),
            skill_id = SkillId,
            target_unit_id = target.unit_id,
            target_coord = target.GetAnchorCoord(),
        };

    private static void AddForcedMoveImmunity(BattleUnitState unit) =>
        unit.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = "forced_move_immunity",
                stacks = 1,
                duration = 60,
                forced_move_immune = true,
            }
        );

    private static void ApplyThreatWeapon(BattleUnitState unit) =>
        unit.ApplyWeaponProjectionTyped(
            new WeaponProjection
            {
                weapon_profile_kind = "equipped",
                weapon_item_id = "dimension_swap_test_sword",
                weapon_profile_type_id = "longsword",
                weapon_range_type = "melee",
                weapon_family = "sword",
                weapon_current_grip = "one_handed",
                weapon_attack_range = 1,
                weapon_one_handed_dice = new WeaponDice { dice_count = 1, dice_sides = 8 },
                weapon_physical_damage_tag = "physical_slash",
            }
        );

    private static bool ErrorsContain(IEnumerable<string> errors, string needle) =>
        (errors ?? Array.Empty<string>()).Any(
            error => error?.Contains(needle, StringComparison.Ordinal) == true
        );

    private static string JoinLogs(BattlePreview preview) =>
        string.Join(" | ", preview?.LogLinesTyped ?? Array.Empty<string>());

    private static void Dispose(BattleCommand command, BattlePreview preview = null)
    {
        BattleTestFixture.DisposeBattlePreview(preview);
        BattleTestFixture.DisposeBattleCommand(command);
    }

    private static void DisposeUnits(params BattleUnitState[] units)
    {
        foreach (BattleUnitState unit in units ?? Array.Empty<BattleUnitState>())
            BattleTestFixture.DisposeBattleUnit(unit);
    }

    private sealed class MasteryGatewayStub : IBattleRuntimeCharacterGateway
    {
        internal List<CharacterMasteryChangeFact> Grants { get; } = new();
        public PartyState GetPartyState() => null;
        public IReadOnlyDictionary<StringName, ItemDefinition> GetItemDefsTyped() => new Dictionary<StringName, ItemDefinition>();
        public bool HasItemDefCatalog() => false;
        public ItemDefinition GetItemDef(StringName itemId) => null;
        public PartyMemberState GetMemberState(StringName memberId) => null;
        public AttributeSnapshot GetMemberAttributeSnapshotForEquipmentView(StringName memberId, EquipmentState equipmentView) => null;
        public WeaponProjection GetMemberWeaponProjectionForEquipmentViewTyped(StringName memberId, EquipmentState equipmentView) => new();
        public BattleEffectiveTraitProjection BuildEffectiveTraitProjectionForEquipmentView(StringName memberId, EquipmentState equipmentView) => BattleEffectiveTraitProjection.Empty;
        public PassiveSourceContext BuildPassiveSourceContext(StringName memberId, UnitProgress progressionState) => null;
        public CharacterProgressionDelta PromoteProfession(StringName memberId, StringName professionId, PromotionCommitRequest selection) => new() { member_id = memberId };
        public BattleResourceCommitResult CommitBattleResources(StringName memberId, int currentHp, int currentMp, int currentAura) => BattleResourceCommitResult.Success(memberId);
        public ContingencyConsumedCommitResult ValidateContingencyConsumedSetups(StringName memberId, IReadOnlyCollection<StringName> consumedSetupIds) => ContingencyConsumedCommitResult.Success(memberId, consumedSetupIds?.Count ?? 0);
        public ContingencyConsumedCommitResult CommitContingencyConsumedSetups(StringName memberId, IReadOnlyCollection<StringName> consumedSetupIds) => ContingencyConsumedCommitResult.Success(memberId, consumedSetupIds?.Count ?? 0);
        public void CommitBattleDeath(StringName memberId) { }
        public int FlushAfterBattle() => (int)Error.Ok;
        public CharacterProgressionDelta GrantBattleMastery(StringName memberId, StringName skillId, int amount) => RecordGrant(memberId, skillId, amount, "battle");
        public CharacterProgressionDelta GrantSkillMasteryFromSource(StringName memberId, StringName skillId, int amount, StringName sourceType, string sourceLabel, string reasonText, bool emitAchievementEvent) => RecordGrant(memberId, skillId, amount, sourceType);
        public IReadOnlyList<StringName> RecordAchievementEvent(StringName memberId, StringName eventType, int amount) => Array.Empty<StringName>();
        public IReadOnlyList<StringName> RecordAchievementEvent(StringName memberId, StringName eventType, int amount, StringName subjectId, GDictionary meta) => Array.Empty<StringName>();
        public PendingCharacterReward BuildPendingSkillMasteryReward(StringName memberId, StringName sourceType, string sourceLabel, IEnumerable<PendingCharacterRewardEntry> entryOptions, string summaryText) => null;

        private CharacterProgressionDelta RecordGrant(StringName memberId, StringName skillId, int amount, StringName sourceType)
        {
            var change = new CharacterMasteryChangeFact(skillId, skillId.ToString(), amount, sourceType, sourceType.ToString(), "");
            Grants.Add(change);
            var delta = new CharacterProgressionDelta { member_id = memberId };
            delta.AddMasteryChange(change);
            return delta;
        }
    }
}
