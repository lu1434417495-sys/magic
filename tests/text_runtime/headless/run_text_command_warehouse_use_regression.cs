using System.Collections.Generic;
using Godot;

public partial class run_text_command_warehouse_use_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    private sealed class BookSkillPickData
    {
        public StringName SkillId { get; set; }
        public StringName ItemId { get; set; }
    }

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestWarehouseAddAndDiscardCommandsBehavior();
        TestWarehouseUseCommandSupportsTypedOptionsPath();
        TestNewGameRandomBookSkillGrantUsesTypedSkillLookup();

        RequestTestExit(_test.Finish("Text command warehouse use regression"));
    }

    private void TestWarehouseUseCommandSupportsTypedOptionsPath()
    {
        GameTextCommandRunner runner = new();
        runner.initialize();
        try
        {
            GameTextCommandResult newGameResult = runner.ExecuteLine("game new test");
            _test.True(newGameResult.ok, $"headless 文本命令应能创建测试世界。message={newGameResult.message}");
            HeadlessGameTestSession session = runner.GetSession();
            _test.True(
                session.GetRuntimeFacadeTyped() != null,
                "HeadlessGameTestSession 应暴露 typed runtime getter。"
            );
            GameSession gameSession = session.GetGameSessionTyped();
            _test.True(gameSession != null, "HeadlessGameTestSession 应暴露 typed game session getter。");
            if (gameSession == null)
                return;

            StringName memberId = "player_sword_01";
            BookSkillPickData bookSkill = PickUnlearnedBookSkillForMember(gameSession, memberId);
            _test.True(bookSkill.SkillId != "" && bookSkill.ItemId != "", "应能为主角找到一个可生成技能书的未学会 book 技能。");
            if (bookSkill.SkillId == "" || bookSkill.ItemId == "")
                return;

            GameTextCommandResult addResult = runner.ExecuteLine(
                $"warehouse add {bookSkill.ItemId} 1"
            );
            _test.True(addResult.ok, $"warehouse add 应成功。message={addResult.message}");

            GameTextCommandResult openPartyResult = runner.ExecuteLine("party open");
            _test.True(
                openPartyResult.ok,
                $"party open 应成功打开队伍窗口。message={openPartyResult.message}"
            );
            GameTextCommandResult selectPartyResult = runner.ExecuteLine(
                $"party select {memberId}"
            );
            _test.True(
                selectPartyResult.ok,
                $"party select 应成功选中目标成员。message={selectPartyResult.message}"
            );
            GameTextCommandResult openWarehouseResult = runner.ExecuteLine("party warehouse");
            _test.True(
                openWarehouseResult.ok,
                $"party warehouse 应成功打开共享仓库。message={openWarehouseResult.message}"
            );

            GameTextCommandResult useResult = runner.ExecuteLine(
                $"warehouse use {bookSkill.ItemId} {memberId} confirm"
            );
            _test.True(useResult.ok, $"warehouse use confirm 应成功。message={useResult.message}");
            _test.Eq(
                useResult.code,
                GameRuntimeFacade.RuntimeCommandCode.Ok,
                "warehouse use confirm 成功时应返回 Ok code。"
            );

            UnitSkillProgress skillProgress = gameSession
                .GetPartyState()
                ?.GetMemberState(memberId)
                ?.progression
                ?.GetSkillProgress(bookSkill.SkillId);
            _test.True(
                skillProgress != null && skillProgress.is_learned,
                "warehouse use 命令应通过正式链让目标成员学会该技能。"
            );
            _test.Eq(
                session.GetRuntimeFacadeTyped()?.GetPartyWarehouseService()?.CountItem(bookSkill.ItemId) ?? -1,
                0,
                "warehouse use 成功后应消耗技能书库存。"
            );
        }
        finally
        {
            runner.Dispose(true);
        }
    }

    private void TestWarehouseAddAndDiscardCommandsBehavior()
    {
        GameTextCommandRunner runner = new();
        runner.initialize();
        try
        {
            _test.True(
                runner.ExecuteLine("game new test").ok,
                "warehouse add/discard 行为回归应能创建测试世界。"
            );
            _test.True(
                runner.ExecuteLine("party open").ok,
                "warehouse add/discard 行为回归应能打开队伍窗口。"
            );
            _test.True(
                runner.ExecuteLine("party warehouse").ok,
                "warehouse add/discard 行为回归应能打开共享仓库。"
            );

            HeadlessGameTestSession session = runner.GetSession();
            GameRuntimeFacade runtime = session?.GetRuntimeFacadeTyped();
            _test.True(runtime != null, "warehouse add/discard 行为回归应拿到 typed runtime。");
            if (runtime == null)
                return;

            GameTextCommandResult addResult = runner.ExecuteLine("warehouse add iron_ore 2");
            _test.True(addResult.ok, $"warehouse add iron_ore 2 应成功。message={addResult.message}");
            _test.Eq(
                runtime.GetPartyWarehouseService()?.CountItem("iron_ore") ?? -1,
                2,
                "warehouse add 成功后共享仓库应新增对应库存。"
            );

            GameTextCommandResult discardOneResult = runner.ExecuteLine("warehouse discard-one iron_ore");
            _test.True(
                discardOneResult.ok,
                $"warehouse discard-one iron_ore 应成功。message={discardOneResult.message}"
            );
            _test.Eq(
                runtime.GetPartyWarehouseService()?.CountItem("iron_ore") ?? -1,
                1,
                "warehouse discard-one 成功后共享仓库应扣减一件库存。"
            );

            GameTextCommandResult discardAllResult = runner.ExecuteLine("warehouse discard-all iron_ore");
            _test.True(
                discardAllResult.ok,
                $"warehouse discard-all iron_ore 应成功。message={discardAllResult.message}"
            );
            _test.Eq(
                runtime.GetPartyWarehouseService()?.CountItem("iron_ore") ?? -1,
                0,
                "warehouse discard-all 成功后共享仓库应清空该库存。"
            );
        }
        finally
        {
            runner.Dispose(true);
        }
    }

    private void TestNewGameRandomBookSkillGrantUsesTypedSkillLookup()
    {
        GameTextCommandRunner runner = new();
        runner.initialize();
        try
        {
            GameTextCommandResult newGameResult = runner.ExecuteLine("game new test");
            _test.True(newGameResult.ok, $"headless 文本命令应能创建测试世界。message={newGameResult.message}");

            GameSession gameSession = runner.GetSession()?.GetGameSessionTyped();
            _test.True(gameSession != null, "新游戏随机技能回归应能拿到 typed game session。");
            if (gameSession == null)
                return;

            PartyState partyState = gameSession.GetPartyState();
            StringName memberId = partyState?.GetResolvedMainCharacterMemberId() ?? new StringName();
            PartyMemberState memberState = partyState?.GetMemberState(memberId);
            _test.True(memberState?.progression != null, "新游戏随机技能回归应能读取主角成长数据。");
            if (memberState?.progression == null)
                return;

            IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions =
                gameSession.GetContentCatalogTyped().GetSkillDefinitionsTyped();
            List<StringName> extraLearnedBookSkillIds = GetLearnedBookSkillIds(
                skillDefinitions,
                memberState.progression,
                "warrior_heavy_strike"
            );
            _test.Eq(
                extraLearnedBookSkillIds.Count,
                1,
                "新游戏后主角应额外随机学会 1 个可由技能书学习的技能。"
            );
            if (extraLearnedBookSkillIds.Count != 1)
                return;

            StringName grantedSkillId = extraLearnedBookSkillIds[0];
            _test.True(
                skillDefinitions.TryGetValue(grantedSkillId, out SkillDefinition grantedSkillDefinition)
                    && grantedSkillDefinition != null,
                $"随机技能 {grantedSkillId} 应能解析回 SkillDefinition。"
            );
            UnitSkillProgress grantedSkillProgress = memberState.progression.GetSkillProgress(
                grantedSkillId
            );
            _test.True(
                grantedSkillProgress != null && grantedSkillProgress.is_learned,
                "随机技能应真正写入主角成长数据。"
            );
            if (grantedSkillDefinition == null || grantedSkillProgress == null)
                return;

            _test.Eq(
                grantedSkillProgress.granted_source_type,
                UnitSkillProgress.ToStringName(UnitSkillGrantSourceType.Player),
                "随机起始技能来源类型应为 player。"
            );
            _test.Eq(
                grantedSkillProgress.granted_source_id,
                new StringName(),
                "随机起始技能来源 id 应为空。"
            );
            _test.Eq(
                grantedSkillProgress.skill_level,
                gameSession.ResolveRandomStartSkillInitialLevel(grantedSkillDefinition),
                "随机技能等级应按技能层级规则初始化。"
            );
        }
        finally
        {
            runner.Dispose(true);
        }
    }

    private static BookSkillPickData PickUnlearnedBookSkillForMember(
        GameSession gameSession,
        StringName memberId
    )
    {
        PartyState partyState = gameSession?.GetPartyState();
        PartyMemberState memberState = partyState?.GetMemberState(memberId);
        if (memberState?.progression == null)
            return new BookSkillPickData();

        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions =
            gameSession.GetContentCatalogTyped().GetSkillDefinitionsTyped();
        IReadOnlyDictionary<StringName, ItemDef> itemDefs = gameSession.GetItemDefsTyped();
        var sortedSkillIds = new List<StringName>(skillDefinitions.Keys);
        sortedSkillIds.Sort((left, right) => string.CompareOrdinal(left.ToString(), right.ToString()));
        foreach (StringName skillId in sortedSkillIds)
        {
            if (
                !skillDefinitions.TryGetValue(skillId, out SkillDefinition skillDefinition)
                || skillDefinition == null
            )
                continue;
            if (skillDefinition.LearnSource != "book")
                continue;
            UnitSkillProgress skillProgress = memberState.progression.GetSkillProgress(skillId);
            if (skillProgress != null && skillProgress.is_learned)
                continue;
            StringName itemId = BuildSkillBookItemId(skillId);
            if (!itemDefs.ContainsKey(itemId))
                continue;
            return new BookSkillPickData { SkillId = skillId, ItemId = itemId };
        }
        return new BookSkillPickData();
    }

    private static List<StringName> GetLearnedBookSkillIds(
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        UnitProgress progression,
        StringName excludedSkillId
    )
    {
        var result = new List<StringName>();
        foreach (StringName skillId in GetSortedSkillIds(skillDefinitions))
        {
            if (skillId == excludedSkillId)
                continue;
            if (
                !skillDefinitions.TryGetValue(skillId, out SkillDefinition skillDefinition)
                || skillDefinition == null
            )
                continue;
            if (skillDefinition.LearnSource != "book")
                continue;
            UnitSkillProgress skillProgress = progression.GetSkillProgress(skillId);
            if (skillProgress == null || !skillProgress.is_learned)
                continue;
            result.Add(skillId);
        }
        return result;
    }

    private static List<StringName> GetSortedSkillIds(
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions
    )
    {
        var sortedSkillIds = new List<StringName>(skillDefinitions.Keys);
        sortedSkillIds.Sort((left, right) => string.CompareOrdinal(left.ToString(), right.ToString()));
        return sortedSkillIds;
    }

    private static StringName BuildSkillBookItemId(StringName skillId) =>
        new($"skill_book_{skillId}");
}
