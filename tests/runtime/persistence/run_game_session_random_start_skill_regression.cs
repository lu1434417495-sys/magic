using System.Collections.Generic;
using Godot;

public partial class run_game_session_random_start_skill_regression : LifecycleTestSceneTree
{
    private const string TestWorldConfig = "res://data/configs/world_map/test_world_map_config.tres";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestStartingEquipmentMatchesSelectedRandomSkillThroughCreateNewSave();
        TestMpStartingSkillGrantsBasicMeditationAndRandomManaPool();
        TestFrostBoltRandomStartTierAndAffordableLevelCost();
        TestBoneChillRandomStartTierAndAffordableLevelCost();
        TestRandomStartCandidatesAreAffordableAndBoneChillGetsManaFloor();

        RequestTestExit(_test.Finish("GameSession random start skill regression"));
    }

    private void TestStartingEquipmentMatchesSelectedRandomSkillThroughCreateNewSave()
    {
        GameSession gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        try
        {
            gameSession.SetRandomStartingSkillSelectorForTests(_ => "mage_arcane_missile");
            Error createError = (Error)gameSession.CreateNewSave(TestWorldConfig);
            _test.Eq(createError, Error.Ok, "随机起始装备回归前置：应能创建测试存档。");
            if (createError != Error.Ok)
                return;

            PartyState partyState = gameSession.GetPartyState();
            PartyMemberState memberState = partyState?.GetMemberState(
                partyState.GetResolvedMainCharacterMemberId()
            );
            _test.True(memberState != null, "随机起始装备回归前置：应能取得新建主角。");
            if (memberState == null)
                return;

            _test.Eq(
                memberState.progression.GetSortedProfessionIdsTyped().Count,
                0,
                "默认角色不应预置任何职业进度。"
            );
            UnitSkillProgress starterSkillProgress = memberState.progression.GetSkillProgress(
                "warrior_heavy_strike"
            );
            _test.True(starterSkillProgress != null, "默认角色仍应保留初始重击技能。");
            if (starterSkillProgress != null)
            {
                _test.False(starterSkillProgress.is_core, "无职业角色的初始重击不应预置为职业核心技能。");
                _test.Eq(
                    starterSkillProgress.assigned_profession_id,
                    new StringName(),
                    "无职业角色的初始重击不应绑定战士职业。"
                );
                _test.Eq(
                    starterSkillProgress.granted_source_type,
                    UnitSkillProgress.ToStringName(UnitSkillGrantSourceType.Player),
                    "无职业角色的初始重击应记录为角色创建授予，而不是职业授予。"
                );
            }

            const string SelectedSkillId = "mage_arcane_missile";
            IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions = gameSession
                .GetContentCatalogTyped()
                .GetSkillDefinitionsTyped();
            SkillDefinition randomSkillDefinition = skillDefinitions[SelectedSkillId];
            UnitSkillProgress randomSkillProgress =
                memberState.progression.GetSkillProgress(SelectedSkillId);
            _test.True(
                randomSkillProgress != null && randomSkillProgress.is_learned,
                "随机起始技能应写入主角成长数据。"
            );
            if (randomSkillDefinition == null || randomSkillProgress == null)
                return;

            var learnedBookSkillIds = new List<StringName>();
            foreach (StringName skillId in memberState.progression.GetSortedSkillIdsTyped())
            {
                UnitSkillProgress progress = memberState.progression.GetSkillProgress(skillId);
                if (progress == null || !progress.is_learned)
                    continue;
                if (
                    skillDefinitions.TryGetValue(skillId, out SkillDefinition definition)
                    && definition?.LearnSourceKind == SkillLearnSourceKind.Book
                    && progress.granted_source_type
                        == UnitSkillProgress.ToStringName(UnitSkillGrantSourceType.Player)
                    && progress.granted_source_id == ""
                )
                {
                    learnedBookSkillIds.Add(skillId);
                }
            }
            _test.Eq(
                learnedBookSkillIds.Count,
                1,
                "CreateNewSave 应只授予 selector 选中的一个书籍来源技能。"
            );
            if (learnedBookSkillIds.Count == 1)
            {
                _test.Eq(
                    learnedBookSkillIds[0],
                    new StringName(SelectedSkillId),
                    "唯一授予的书籍来源技能应是 selector 指定的奥术飞弹。"
                );
            }

            int expectedInitialLevel = gameSession.ResolveRandomStartSkillInitialLevel(
                randomSkillDefinition
            );
            _test.Eq(
                randomSkillProgress.skill_level,
                expectedInitialLevel,
                "CreateNewSave 应把随机起始技能写入规则计算出的初始等级。"
            );
            _test.Eq(randomSkillProgress.current_mastery, 0, "随机起始技能不应预置当前熟练度。");
            _test.Eq(
                randomSkillProgress.total_mastery_earned,
                0,
                "随机起始技能不应伪造历史熟练度。"
            );

            _test.Eq(
                randomSkillProgress.granted_source_type,
                UnitSkillProgress.ToStringName(UnitSkillGrantSourceType.Player),
                "随机起始技能来源类型应为 player。"
            );
            _test.Eq(
                randomSkillProgress.granted_source_id,
                new StringName(),
                "随机起始技能来源 id 应为空。"
            );
            StringName equippedItemId = memberState.equipment_state.GetEquippedItemId("main_hand");
            _test.Eq(
                equippedItemId,
                new StringName("oak_quarterstaff"),
                "法师标签的奥术飞弹应通过真实 CreateNewSave 流程装备橡木长棍。"
            );
            _test.True(
                memberState.equipment_state.GetEquippedInstanceId("main_hand") != "",
                "随机起始装备应写入持久装备实例 ID。"
            );
        }
        finally
        {
            CleanupTestSession(gameSession);
        }
    }

    private void TestMpStartingSkillGrantsBasicMeditationAndRandomManaPool()
    {
        GameSession gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        try
        {
            IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions =
                gameSession.GetContentCatalogTyped().GetSkillDefinitionsTyped();
            _test.True(
                skillDefinitions.TryGetValue(
                    "mage_arcane_missile",
                    out SkillDefinition arcaneMissile
                ),
                "法力伴随授予回归前置：应加载奥术飞弹定义。"
            );
            if (arcaneMissile == null)
                return;

            CombatSkillResourceCosts startingCosts = BattleTargetSlotCostRules.Resolve(
                arcaneMissile.CombatProfile,
                0,
                1
            );
            _test.True(startingCosts.MpCost > 0, "法力伴随授予前置必须是真正消耗 MP 的技能。");

            AssertManaPoolRoll(skillDefinitions, arcaneMissile, 0);
            AssertManaPoolRoll(skillDefinitions, arcaneMissile, 40);
        }
        finally
        {
            CleanupTestSession(gameSession);
        }
    }

    private void TestFrostBoltRandomStartTierAndAffordableLevelCost()
    {
        GameSession gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        try
        {
            IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions =
                gameSession.GetContentCatalogTyped().GetSkillDefinitionsTyped();
            _test.True(
                skillDefinitions.TryGetValue(
                    "mage_frost_bolt",
                    out SkillDefinition frostBolt
                ),
                "随机起始等级回归前置：应加载霜击术定义。"
            );
            if (frostBolt == null)
                return;

            _test.Eq(
                frostBolt.LearnSourceKind,
                SkillLearnSourceKind.Book,
                "霜击术应继续作为书籍来源技能参与随机起始候选。"
            );
            int initialLevel = gameSession.ResolveRandomStartSkillInitialLevel(frostBolt);
            _test.Eq(initialLevel, 3, "advanced 成长档的霜击术随机起始等级应为3级。" );
            CombatSkillResourceCosts initialCosts = BattleTargetSlotCostRules.Resolve(
                frostBolt.CombatProfile,
                initialLevel,
                1
            );
            _test.Eq(initialCosts.ApCost, 1, "随机起始霜击术应保持1 AP消耗。" );
            _test.Eq(initialCosts.MpCost, 20, "随机起始3级霜击术应采用已解锁的20 MP消耗。" );
        }
        finally
        {
            CleanupTestSession(gameSession);
        }
    }

    private void TestBoneChillRandomStartTierAndAffordableLevelCost()
    {
        GameSession gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        try
        {
            IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions =
                gameSession.GetContentCatalogTyped().GetSkillDefinitionsTyped();
            _test.True(
                skillDefinitions.TryGetValue("mage_bone_chill", out SkillDefinition boneChill),
                "随机起始等级回归前置：应加载骨寒术定义。"
            );
            if (boneChill == null)
                return;

            int initialLevel = gameSession.ResolveRandomStartSkillInitialLevel(boneChill);
            _test.Eq(initialLevel, 0, "骨寒术在当前随机起始分档规则下应从0级开始。" );
            CombatSkillResourceCosts initialCosts = BattleTargetSlotCostRules.Resolve(
                boneChill.CombatProfile,
                initialLevel,
                1
            );
            _test.Eq(initialCosts.MpCost, 20, "随机起始0级骨寒术应采用20 MP消耗。" );
            _test.True(
                initialCosts.MpCost <= TrueRandomStartingManaPoolRoller.MaximumManaPool,
                "随机起始骨寒术必须可由允许的起始法力池支付。"
            );
        }
        finally
        {
            CleanupTestSession(gameSession);
        }
    }

    private void TestRandomStartCandidatesAreAffordableAndBoneChillGetsManaFloor()
    {
        GameSession gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        try
        {
            IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions =
                gameSession.GetContentCatalogTyped().GetSkillDefinitionsTyped();
            bool inspectedCandidates = false;
            gameSession.SetRandomStartingSkillSelectorForTests(
                candidateIds =>
                {
                    inspectedCandidates = true;
                    foreach (StringName candidateId in candidateIds)
                    {
                        SkillDefinition candidate = skillDefinitions[candidateId];
                        int initialLevel = gameSession.ResolveRandomStartSkillInitialLevel(
                            candidate
                        );
                        int mpCost = BattleTargetSlotCostRules.Resolve(
                            candidate.CombatProfile,
                            initialLevel,
                            1
                        ).MpCost;
                        _test.True(
                            mpCost <= TrueRandomStartingManaPoolRoller.MaximumManaPool,
                            $"随机起始候选{candidateId}的起始等级MP消耗不得超过40。"
                        );
                    }
                    return new StringName("mage_bone_chill");
                }
            );

            Error createError = (Error)gameSession.CreateNewSave(TestWorldConfig);
            _test.Eq(createError, Error.Ok, "选择骨寒术时应能创建测试存档。" );
            _test.True(inspectedCandidates, "随机起始选择器应收到已过滤的候选集合。" );
            if (createError != Error.Ok)
                return;
            PartyState partyState = gameSession.GetPartyState();
            PartyMemberState memberState = partyState?.GetMemberState(
                partyState.GetResolvedMainCharacterMemberId()
            );
            _test.True(
                memberState?.progression?.GetSkillProgress("mage_bone_chill")?.is_learned == true,
                "测试选择器指定的骨寒术应被授予。"
            );
            _test.True(memberState?.GetCurrentMp() >= 20, "骨寒术随机开局法力不得低于0级20 MP消耗。" );
            _test.True(memberState?.GetCurrentMp() <= 40, "随机开局法力池仍不得超过40。" );
        }
        finally
        {
            CleanupTestSession(gameSession);
        }
    }

    private void AssertManaPoolRoll(
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        SkillDefinition arcaneMissile,
        int rolledManaPool
    )
    {
        UnitProgress progression = new()
        {
            unit_id = $"mana_start_test_{rolledManaPool}",
            display_name = "Mana Start Test",
            unit_base_attributes = new UnitBaseAttributes(),
        };
        progression.SetSkillProgress(
            new UnitSkillProgress
            {
                skill_id = arcaneMissile.SkillId,
                is_learned = true,
                skill_level = 0,
                granted_source_type = UnitSkillProgress.ToStringName(
                    UnitSkillGrantSourceType.Player
                ),
            }
        );
        PartyMemberState memberState = new()
        {
            member_id = progression.unit_id,
            display_name = progression.display_name,
            progression = progression,
        };

        var supportService = new RandomStartingSkillResourceSupportService(
            skillDefinitions,
            new FixedManaPoolRoller(rolledManaPool)
        );
        int resultingManaPool = supportService.ApplyManaSupport(
            memberState,
            arcaneMissile
        );
        int startingMpCost = BattleTargetSlotCostRules.Resolve(
            arcaneMissile.CombatProfile,
            0,
            1
        ).MpCost;

        UnitSkillProgress meditationProgress = progression.GetSkillProgress(
            RandomStartingSkillResourceSupportService.BasicMeditationSkillId
        );
        _test.True(meditationProgress != null, "随机获得耗蓝法术时应同时授予基础冥想法。");
        if (meditationProgress != null)
        {
            _test.True(meditationProgress.is_learned, "基础冥想法应处于已学习状态。");
            _test.Eq(meditationProgress.skill_level, 0, "基础冥想法应从最低等级 0 开始。");
            _test.False(meditationProgress.is_core, "伴随授予的基础冥想法不应预置为职业核心技能。");
            _test.Eq(
                meditationProgress.granted_source_id,
                arcaneMissile.SkillId,
                "基础冥想法应记录触发伴随授予的随机法术。"
            );
        }
        int expectedManaPool = Mathf.Max(rolledManaPool, startingMpCost);
        _test.Eq(resultingManaPool, expectedManaPool, "初始法力值不得低于所抽技能的实际消耗。");
        _test.Eq(
            progression.unit_base_attributes.GetAttributeValue("mp_max"),
            expectedManaPool,
            "受技能消耗下限约束的法力值应写入角色法力池上限。"
        );
        _test.Eq(
            memberState.GetCurrentMp(),
            expectedManaPool,
            "新角色当前法力应与随机法力池上限一致。"
        );
        _test.True(
            progression.HasCombatResourceUnlocked(
                CombatResourceIds.ToStringName(CombatResourceIdKind.Mp)
            ),
            "即使原始随机法力值为0，耗蓝法术仍应解锁MP资源并获得可支付下限。"
        );
    }

    private static void CleanupTestSession(GameSession gameSession)
    {
        if (gameSession == null)
            return;

        gameSession.UnloadActiveWorld();
        gameSession.ClearPersistedGame();
        gameSession.Dispose();
    }

    private sealed class FixedManaPoolRoller : IRandomStartingManaPoolRoller
    {
        private readonly int _value;

        internal FixedManaPoolRoller(int value)
        {
            _value = value;
        }

        public int Roll() => _value;
    }
}
