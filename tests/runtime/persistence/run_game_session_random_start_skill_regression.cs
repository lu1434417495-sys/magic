using System.Collections.Generic;
using Godot;

public partial class run_game_session_random_start_skill_regression : LifecycleTestSceneTree
{
    private const string TestWorldConfig = "res://data/configs/world_map/test_world_map_config.tres";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestStartingEquipmentMatchesRandomSkillWithTypedLookup();

        RequestTestExit(_test.Finish("GameSession random start skill regression"));
    }

    private void TestStartingEquipmentMatchesRandomSkillWithTypedLookup()
    {
        GameSession gameSession = new();
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
}
