using System;
using System.Collections.Generic;
using Godot;

public partial class run_promotion_auto_prompt_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();
    private static readonly StringName MemberId = "player_sword_01";
    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        var runner = new GameTextCommandRunner();
        try
        {
            runner.initialize();
            TestWorldQueueAndNewOpportunities(runner);
            TestReload(runner);
            TestMultipleMembers(runner);
            TestBattle(runner);
        }
        catch (Exception error) { _test.Fail(error.ToString()); }
        finally
        {
            runner.Dispose(true);
            RequestTestExit(_test.Finish("Promotion automatic prompt regression"));
        }
    }

    private GameRuntimeFacade NewCharacter(GameTextCommandRunner runner)
    {
        Check(runner, "game new test");
        var runtime = runner.GetSession().GetRuntimeFacadeTyped();
        var manager = runtime.GetCharacterManagement();
        foreach (StringName id in new StringName[] { "warrior_heavy_strike", "warrior_guard", "warrior_backstep" })
            if (manager.GetMemberState(MemberId).progression.GetSkillProgress(id)?.is_learned != true)
                _test.True(manager.LearnSkill(MemberId, id), "Learn through the character owner.");
        manager.GrantBattleMastery(MemberId, "warrior_heavy_strike", 899);
        runtime.advance(0);
        _test.Eq(runtime.GetPromotionReadyMemberCount(), 0, "Below the milestone there is no notification.");
        return runtime;
    }

    private void TestWorldQueueAndNewOpportunities(GameTextCommandRunner runner)
    {
        var runtime = NewCharacter(runner);
        Check(runner, "party open");
        runtime.GetCharacterManagement().GrantBattleMastery(MemberId, "warrior_heavy_strike", 1);
        runtime.advance(0);
        _test.Eq(runtime.GetActiveModalKind(), RuntimeModalKind.Party, "A new opportunity must not replace an existing window.");
        _test.Eq(runtime.GetPromotionReadyMemberCount(), 1, "A blocked prompt still exposes a persistent notification.");
        _test.True(runtime.GetStatusText().Contains("可晋升"), "Text consumers also see the notification.");
        _test.True(runtime.CommandCloseActiveModalTyped().Ok, "Close the preceding modal.");
        _test.Eq(runtime.GetActiveModalKind(), RuntimeModalKind.Promotion, "The queued opportunity opens automatically at the next safe boundary.");
        var first = Request(runtime, "warrior_heavy_strike");
        runtime.advance(0);
        _test.Eq(Request(runtime, "warrior_heavy_strike").PromptId, first.PromptId, "Idle frames do not replace a displayed request.");
        Check(runner, "promotion defer");
        for (int i = 0; i < 5; i++) runtime.advance(0);
        _test.Eq(runtime.GetActiveModalKind(), RuntimeModalKind.None, "Deferring acknowledges this batch without reopening every frame.");
        runtime.UpdateStatus("普通操作已完成。");
        _test.True(runtime.GetStatusText().Contains("可晋升"), "An unrelated status update cannot erase the remaining opportunity.");
        _test.Eq(runtime.GetCharacterManagement().GetMemberState(MemberId).progression.character_level, 0, "Closing consumes no growth.");

        runtime.GetCharacterManagement().GrantBattleMastery(MemberId, "warrior_guard", 2700);
        runtime.advance(0);
        _test.Eq(runtime.GetActiveModalKind(), RuntimeModalKind.Promotion, "A different newly qualified trigger opens even while an older one was deferred.");
        Check(runner, "promotion choose warrior warrior_heavy_strike");
        ClaimRewards(runner, runtime);
        runtime.advance(0);
        _test.Eq(runtime.GetActiveModalKind(), RuntimeModalKind.Promotion, "A newly unlocked next rank is presented automatically.");
        _test.Eq(Request(runtime, "warrior_guard").TargetRank, 2, "The next request reflects the committed rank.");
        _test.False(runtime.CommandSubmitPromotionChoiceTyped(MemberId, "warrior", first).Ok, "Old auto-prompt requests remain invalid.");
        Check(runner, "promotion choose warrior warrior_guard");
        ClaimRewards(runner, runtime);
        runtime.advance(0);
        _test.Eq(runtime.GetPromotionReadyMemberCount(), 0, "The reminder disappears when no legal opportunity remains.");
    }

    private void TestReload(GameTextCommandRunner runner)
    {
        var runtime = NewCharacter(runner);
        runtime.GetCharacterManagement().GrantBattleMastery(MemberId, "warrior_heavy_strike", 1);
        runtime.advance(0);
        var previous = Request(runtime, "warrior_heavy_strike");
        Check(runner, "promotion defer");
        _test.Eq(runtime.PersistPartyState(), (int)Error.Ok, "Save the remaining domain opportunity.");
        var session = runner.GetSession();
        _test.True(session.LoadGameTyped(session.GetGameSessionTyped().GetActiveSaveId()).Ok, "Reload current save.");
        runtime = session.GetRuntimeFacadeTyped();
        ClaimRewards(runner, runtime);
        runtime.advance(0);
        _test.Eq(runtime.GetActiveModalKind(), RuntimeModalKind.Promotion, "Reload restores an automatic reminder without a persisted pending queue.");
        _test.False(Request(runtime, "warrior_heavy_strike").PromptId == previous.PromptId, "Reload issues a fresh request token.");
        Check(runner, "promotion defer");
    }

    private void TestBattle(GameTextCommandRunner runner)
    {
        var runtime = NewCharacter(runner);
        Check(runner, "battle start single");
        if (runtime.GetActiveModalKind() == RuntimeModalKind.BattleStartConfirm)
            Check(runner, "battle confirm");
        var batch = new BattleEventBatch();
        // Accelerated mastery reaches the real post-resolution batch boundary.
        batch.AddProgressionDelta(runtime.GetCharacterManagement().GrantBattleMastery(MemberId, "warrior_heavy_strike", 1));
        runtime.ApplyBattleBatch(batch);
        _test.Eq(runtime.GetActiveModalKind(), RuntimeModalKind.Promotion, "A battle mastery batch opens promotion automatically.");
        var state = runtime.GetBattleRuntime().GetState();
        _test.True(state.timeline.frozen, "The automatic window pauses battle time.");
        _test.True(runtime.GetGameSession().IsBattleSaveLocked(), "Opening preserves the battle save lock.");
        _test.True(runtime.CommandCloseActiveModalTyped().Ok, "The automatic window can be closed.");
        _test.False(state.timeline.frozen, "Closing resumes battle time.");
        runtime.advance(0);
        _test.Eq(runtime.GetActiveModalKind(), RuntimeModalKind.None, "The same opportunity does not trap the battle in a popup loop.");
        _test.Eq(runtime.GetPromotionReadyMemberCount(), 1, "The battle reminder remains after closing.");
        Check(runner, "promotion open player_sword_01");
        Check(runner, "promotion choose warrior warrior_heavy_strike");
        _test.False(state.timeline.frozen, "Successful promotion resumes battle time.");
        _test.True(runtime.GetGameSession().IsBattleSaveLocked(), "Promotion still cannot save during battle.");
        Check(runner, "battle finish player");
    }

    private void TestMultipleMembers(GameTextCommandRunner runner)
    {
        var runtime = NewCharacter(runner);
        var party = runtime.GetPartyState();
        var companion = party.GetMemberState(MemberId).DuplicateState();
        companion.member_id = "z_promotion_companion";
        companion.progression.unit_id = companion.member_id;
        companion.display_name = "晋升同伴";
        companion.equipment_state = new EquipmentState();
        party.SetMemberState(companion);
        party.reserve_member_ids.Add(companion.member_id);
        var dead = companion.DuplicateState();
        dead.member_id = "z_promotion_dead";
        dead.progression.unit_id = dead.member_id;
        dead.current_hp = 0;
        party.SetMemberState(dead);
        party.reserve_member_ids.Add(dead.member_id);
        runtime.SetPartyState(party);
        foreach (StringName id in new[] { MemberId, companion.member_id, dead.member_id })
            runtime.GetCharacterManagement().GrantBattleMastery(id, "warrior_heavy_strike", 1);
        runtime.advance(0);
        _test.Eq(runtime.GetPromotionReadyMemberCount(), 2, "Living eligible members are counted; a dead member is excluded.");
        _test.Eq((string)runtime.GetCurrentPromotionPromptSnapshotPlain()["member_id"], MemberId.ToString(),
            "Simultaneous opportunities start in stable member order.");
        Check(runner, "promotion defer");
        _test.Eq((string)runtime.GetCurrentPromotionPromptSnapshotPlain()["member_id"], companion.member_id.ToString(),
            "Deferring one member does not swallow another member's queued prompt.");
        Check(runner, "promotion defer");
        runtime.advance(0);
        _test.Eq(runtime.GetActiveModalKind(), RuntimeModalKind.None, "All shown members can return to play.");
        _test.Eq(runtime.GetPromotionReadyMemberCount(), 2, "Both deferred opportunities remain visible.");
    }

    private void ClaimRewards(GameTextCommandRunner runner, GameRuntimeFacade runtime)
    {
        for (int i = 0; i < 20 && runtime.GetActiveModalKind() == RuntimeModalKind.Reward; i++)
            Check(runner, "reward confirm");
    }

    private void Check(GameTextCommandRunner runner, string command)
    {
        var result = runner.ExecuteLine(command);
        _test.True(result.ok && !result.skipped, $"{command}: {result.message}");
        if (!result.ok) throw new InvalidOperationException(result.message);
    }

    private static PromotionCommitRequest Request(GameRuntimeFacade runtime, StringName skillId)
    {
        var snapshot = runtime.GetCurrentPromotionPromptSnapshotPlain();
        foreach (IReadOnlyDictionary<string, object> choice in (IEnumerable<object>)snapshot["choices"])
        {
            var request = PromotionCommitRequest.FromPlainPayload((IReadOnlyDictionary<string, object>)choice["selection"]);
            if ((string)choice["profession_id"] == "warrior" && request.GrowthTriggerSkillId == skillId) return request;
        }
        throw new InvalidOperationException("Missing warrior promotion request.");
    }
}
