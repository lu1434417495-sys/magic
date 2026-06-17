using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_world_map_system_surface_regression : SceneTree
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
        // deferred 调用里的未捕获异常会被 Godot 吞掉导致 Quit 永不执行，
        // 必须显式兜底，避免 headless 测试挂死。
        try
        {
            TestStagecoachModalAcceptsOnlyFormalTargetPayload();
            TestPartyWarehouseUseRequestDelegatesToTypedRuntimeBoundary();
        }
        catch (System.Exception ex)
        {
            _test.Fail($"未捕获异常：{ex}");
        }

        Quit(_test.Finish("World map system surface regression"));
    }

    private void TestStagecoachModalAcceptsOnlyFormalTargetPayload()
    {
        GameRuntimeFacade runtime = BuildRuntime();
        WorldMapRuntimeProxy proxy = new();
        proxy.Setup(runtime);
        WorldMapSystem system = new()
        {
            _runtime = runtime,
            _runtime_proxy = proxy,
        };
        try
        {
            runtime.UpdateStatus("unchanged");
            system._on_stagecoach_service_modal_action_requested(
                "spring_village_01",
                "service:stagecoach",
                new GDictionary { ["settlement_id"] = "legacy_destination" }
            );
            _test.Eq(
                runtime._current_status_message,
                "unchanged",
                "Stagecoach modal payload 只有 settlement_id 时不应触发旅行命令。"
            );

            system._on_stagecoach_service_modal_action_requested(
                "spring_village_01",
                "service:stagecoach",
                new GDictionary { ["target_settlement_id"] = "north_outpost" }
            );
            _test.Eq(
                runtime._current_status_message,
                "当前没有打开驿站路线窗口。",
                "Stagecoach modal 使用 target_settlement_id 时应委托正式旅行命令。"
            );
        }
        finally
        {
            proxy.Dispose();
            runtime.Dispose();
            system.Free();
        }
    }

    private void TestPartyWarehouseUseRequestDelegatesToTypedRuntimeBoundary()
    {
        GameTextCommandRunner runner = new();
        runner.initialize();
        try
        {
            GameTextCommandResult newGameResult = runner.ExecuteLine("game new test");
            _test.True(newGameResult.ok, $"world map system regression 应能创建测试世界。message={newGameResult.message}");

            HeadlessGameTestSession session = runner.GetSession();
            GameRuntimeFacade runtime = session?.GetRuntimeFacadeTyped();
            GameSession gameSession = session?.GetGameSessionTyped();
            _test.True(runtime != null, "world map system regression 应拿到 typed runtime。");
            _test.True(gameSession != null, "world map system regression 应拿到 typed game session。");
            if (runtime == null || gameSession == null)
                return;

            StringName memberId = "player_sword_01";
            BookSkillPickData bookSkill = PickImmediateBookSkillForMember(runtime, gameSession, memberId);
            _test.True(
                bookSkill.SkillId != "" && bookSkill.ItemId != "",
                "world map system regression 应能找到无需 confirm 的技能书目标。"
            );
            if (bookSkill.SkillId == "" || bookSkill.ItemId == "")
                return;

            GameRuntimeFacade.RuntimeCommandResult addResult =
                runtime.CommandWarehouseAddItemTyped(bookSkill.ItemId, 1);
            _test.True(
                addResult.Ok,
                $"world map system regression 预置仓库库存应成功。message={addResult.Message}"
            );
            GameRuntimeFacade.RuntimeCommandResult openPartyResult = runtime.CommandOpenPartyTyped();
            _test.True(
                openPartyResult.Ok,
                $"world map system regression 的 party open 应成功。message={openPartyResult.Message}"
            );
            GameRuntimeFacade.RuntimeCommandResult selectPartyResult =
                runtime.CommandSelectPartyMemberTyped(memberId);
            _test.True(
                selectPartyResult.Ok,
                $"world map system regression 的 party select 应成功。message={selectPartyResult.Message}"
            );
            GameRuntimeFacade.RuntimeCommandResult openWarehouseResult =
                runtime.CommandOpenPartyWarehouseTyped();
            _test.True(
                openWarehouseResult.Ok,
                $"world map system regression 的 open warehouse 应成功。message={openWarehouseResult.Message}"
            );

            WorldMapRuntimeProxy proxy = new();
            WorldMapSystem system = new()
            {
                _runtime = runtime,
                _runtime_proxy = proxy,
            };
            proxy.Setup(runtime, system);
            try
            {
                system._on_party_warehouse_use_requested(bookSkill.ItemId, memberId);
                UnitSkillProgress skillProgress = gameSession
                    .GetPartyState()
                    ?.GetMemberState(memberId)
                    ?.progression
                    ?.GetSkillProgress(bookSkill.SkillId);
                _test.True(
                    skillProgress != null && skillProgress.is_learned,
                    "WorldMapSystem 的 warehouse use 回调应通过正式链让目标成员学会技能。"
                );
                _test.Eq(
                    runtime.GetPartyWarehouseService()?.CountItem(bookSkill.ItemId) ?? -1,
                    0,
                    "WorldMapSystem 的 warehouse use 回调应消耗技能书库存。"
                );
            }
            finally
            {
                proxy.Dispose();
                system.Free();
            }
        }
        finally
        {
            runner.Dispose(true);
        }
    }

    private static GameRuntimeFacade BuildRuntime()
    {
        GameRuntimeFacade runtime = new();
        runtime._settlement_command_handler.SetupRuntime(runtime);
        return runtime;
    }

    private static BookSkillPickData PickImmediateBookSkillForMember(
        GameRuntimeFacade runtime,
        GameSession gameSession,
        StringName memberId
    )
    {
        PartyState partyState = gameSession?.GetPartyState();
        PartyMemberState memberState = partyState?.GetMemberState(memberId);
        if (memberState?.progression == null)
            return new BookSkillPickData();

        IReadOnlyDictionary<StringName, SkillDef> skillDefs = gameSession.GetSkillDefsTyped();
        IReadOnlyDictionary<StringName, ItemDef> itemDefs = gameSession.GetItemDefsTyped();
        var sortedSkillIds = new List<StringName>(skillDefs.Keys);
        sortedSkillIds.Sort((left, right) => string.CompareOrdinal(left.ToString(), right.ToString()));
        foreach (StringName skillId in sortedSkillIds)
        {
            if (!skillDefs.TryGetValue(skillId, out SkillDef skillDef) || skillDef == null)
                continue;
            if (skillDef.learn_source != "book")
                continue;
            UnitSkillProgress skillProgress = memberState.progression.GetSkillProgress(skillId);
            if (skillProgress != null && skillProgress.is_learned)
                continue;
            if (runtime._character_management.GetPracticeSkillLearnStatusTyped(memberId, skillId).NeedsReplacement)
                continue;
            StringName itemId = new($"skill_book_{skillId}");
            if (!itemDefs.ContainsKey(itemId))
                continue;
            return new BookSkillPickData { SkillId = skillId, ItemId = itemId };
        }
        return new BookSkillPickData();
    }
}
