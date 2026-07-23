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
        TestStartingEquipmentMatchesRandomSkillWithTypedLookup();
        TestMpStartingSkillGrantsBasicMeditationAndRandomManaPool();

        RequestTestExit(_test.Finish("GameSession random start skill regression"));
    }

    private void TestStartingEquipmentMatchesRandomSkillWithTypedLookup()
    {
        GameSession gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        try
        {
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

            SkillDefinition randomSkillDefinition = FindRandomStartingSkillDefinition(gameSession, memberState);
            _test.True(randomSkillDefinition != null, "新建主角应记录一条 player 来源的随机起始技能。");
            if (randomSkillDefinition == null)
                return;

            StringName expectedItemId = ResolveExpectedStartingWeaponItemId(gameSession, randomSkillDefinition);
            StringName equippedItemId = memberState.equipment_state.GetEquippedItemId("main_hand");
            _test.Eq(
                equippedItemId,
                expectedItemId,
                $"随机起始技能类型应匹配主手基础装备。 skill_id={randomSkillDefinition.SkillId}"
            );
            _test.True(
                memberState.equipment_state.GetEquippedInstanceId("main_hand") != "",
                "随机起始装备应写入持久装备实例 ID。"
            );
            if (equippedItemId == "ash_shortbow" || equippedItemId == "militia_light_crossbow")
            {
                _test.Eq(
                    memberState.equipment_state.GetEquippedItemId("off_hand"),
                    equippedItemId,
                    "双手远程起始武器应同步占用副手。"
                );
            }
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

            CombatSkillResourceCosts startingCosts = arcaneMissile
                .CombatProfile.GetEffectiveResourceCostValues(0);
            _test.Eq(startingCosts.StaminaCost, 15, "奥术飞弹应保留 15 点体力消耗。");

            AssertManaPoolRoll(skillDefinitions, arcaneMissile, 0);
            AssertManaPoolRoll(skillDefinitions, arcaneMissile, 40);
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
        _test.Eq(resultingManaPool, rolledManaPool, "初始法力值应采用 0–40 闭区间随机结果。");
        _test.Eq(
            progression.unit_base_attributes.GetAttributeValue("mp_max"),
            rolledManaPool,
            "随机法力值应写入角色法力池上限。"
        );
        _test.Eq(
            memberState.GetCurrentMp(),
            rolledManaPool,
            "新角色当前法力应与随机法力池上限一致。"
        );
        _test.True(
            progression.HasCombatResourceUnlocked(
                CombatResourceIds.ToStringName(CombatResourceIdKind.Mp)
            ),
            "即使随机法力值为 0，耗蓝法术仍应解锁 MP 资源。"
        );
    }

    private static SkillDefinition FindRandomStartingSkillDefinition(
        GameSession gameSession,
        PartyMemberState memberState
    )
    {
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions =
            gameSession.GetContentCatalogTyped().GetSkillDefinitionsTyped();
        foreach (StringName skillId in GetSortedSkillIds(skillDefinitions))
        {
            UnitSkillProgress skillProgress = memberState.progression?.GetSkillProgress(skillId);
            if (skillProgress == null || !skillProgress.is_learned)
                continue;
            if (
                skillProgress.granted_source_type
                != UnitSkillProgress.ToStringName(UnitSkillGrantSourceType.Player)
            )
                continue;
            if (skillProgress.granted_source_id != "")
                continue;
            if (
                !skillDefinitions.TryGetValue(skillId, out SkillDefinition skillDefinition)
                || skillDefinition == null
            )
                continue;
            return skillDefinition;
        }
        return null;
    }

    private static StringName ResolveExpectedStartingWeaponItemId(
        GameSession gameSession,
        SkillDefinition skillDefinition
    )
    {
        var candidates = new List<StringName>();
        if (SkillMatches(skillDefinition, "crossbow", "crossbow"))
            candidates.Add("militia_light_crossbow");
        if (SkillMatches(skillDefinition, new[] { "archer", "bow" }, "archer_"))
            candidates.Add("ash_shortbow");
        if (SkillMatches(skillDefinition, new[] { "mage", "magic", "spell" }, "mage_"))
            candidates.Add("oak_quarterstaff");
        if (SkillMatches(skillDefinition, new[] { "priest", "faith", "heal" }, "priest_", "saint_"))
            candidates.Add("watchman_mace");
        if (SkillMatches(skillDefinition, new[] { "warrior", "melee", "shield" }, "warrior_"))
            candidates.Add("steel_longsword");
        candidates.Add("steel_longsword");
        return FirstValidWeaponItemId(gameSession, candidates);
    }

    private static bool SkillMatches(
        SkillDefinition skillDefinition,
        string tagId,
        params string[] skillIdPrefixes
    ) => SkillMatches(skillDefinition, new[] { tagId }, skillIdPrefixes);

    private static bool SkillMatches(
        SkillDefinition skillDefinition,
        IEnumerable<string> tagIds,
        params string[] skillIdPrefixes
    )
    {
        if (skillDefinition == null)
            return false;
        foreach (string tagId in tagIds)
        {
            if (skillDefinition.HasTag(tagId))
                return true;
        }

        string skillIdText = skillDefinition.SkillId.ToString();
        foreach (string prefix in skillIdPrefixes)
        {
            if (skillIdText.StartsWith(prefix))
                return true;
        }
        return false;
    }

    private static StringName FirstValidWeaponItemId(
        GameSession gameSession,
        IEnumerable<StringName> candidates
    )
    {
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions =
            gameSession.GetItemDefsTyped();
        foreach (StringName itemId in candidates)
        {
            if (itemId == "")
                continue;
            if (
                !itemDefinitions.TryGetValue(itemId, out ItemDefinition itemDefinition)
                || itemDefinition == null
            )
                continue;
            if (itemDefinition.IsWeapon())
                return itemId;
        }
        return new StringName();
    }

    private static List<StringName> GetSortedSkillIds(
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions
    )
    {
        var sortedSkillIds = new List<StringName>(skillDefinitions.Keys);
        sortedSkillIds.Sort((left, right) => string.CompareOrdinal(left.ToString(), right.ToString()));
        return sortedSkillIds;
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
