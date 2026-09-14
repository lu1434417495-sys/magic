using System;
using System.Linq;
using Godot;

public partial class run_first_promotion_closed_loop_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();
    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        var runner = new GameTextCommandRunner();
        try
        {
            runner.initialize();
            Check(runner, "game new test");
            var session = runner.GetSession();
            var runtime = session.GetRuntimeFacadeTyped();
            var manager = runtime.GetCharacterManagement();
            StringName memberId = "player_sword_01";
            var member = manager.GetMemberState(memberId);
            _test.Eq(member.progression.character_level, 0, "Normal new game begins at character level zero.");
            foreach (StringName skillId in new StringName[] { "warrior_heavy_strike", "warrior_guard", "warrior_backstep" })
                if (member.progression.GetSkillProgress(skillId)?.is_learned != true)
                    _test.True(manager.LearnSkill(memberId, skillId), $"Production learning accepts {skillId}.");
            // Accelerate training through the production reward boundary; this is not elapsed-playtime evidence.
            var mastery = manager.GrantBattleMastery(memberId, "warrior_heavy_strike", 100000);
            _test.False(mastery.needs_promotion_modal, "The domain delta leaves automatic presentation to the runtime scheduler.");
            _test.False(member.progression.GetSkillProgress("warrior_heavy_strike").is_core, "Training alone does not assign a core.");
            runtime.advance(0);
            _test.Eq(runtime.GetActiveModalKind(), RuntimeModalKind.Promotion, "New eligibility automatically opens a prompt.");
            var issued = CurrentRequest(runtime, "warrior_heavy_strike");
            Check(runner, "promotion defer");
            _test.Eq(runtime.GetActiveModalKind(), RuntimeModalKind.None, "Deferring returns to the world.");
            Check(runner, "promotion open player_sword_01");
            _test.False(runtime.CommandSubmitPromotionChoiceTyped(memberId, "warrior", issued).Ok, "Reopened window rejects a previous prompt token.");
            Check(runner, "promotion choose warrior warrior_heavy_strike");
            member = runtime.GetCharacterManagement().GetMemberState(memberId);
            _test.Eq(member.progression.character_level, 1, "First promotion completes through the actual command and persistence path.");
            _test.Eq(member.progression.GetSkillProgress("warrior_heavy_strike").skill_level, 3, "Heavy strike stays at its original base milestone.");
            _test.True(member.progression.GetSkillProgress("warrior_heavy_strike").is_core, "Only confirmed growth skill becomes core.");
            _test.False(member.progression.GetSkillProgress("warrior_guard").is_core, "Supporting learned skill is not silently made core.");
            _test.True(runtime.GetActiveModalKind() != RuntimeModalKind.Promotion, "Successful promotion closes the prompt and may present achievement rewards.");
            _test.False(runtime.CommandSubmitPromotionChoiceTyped(memberId, "warrior", issued).Ok, "Replayed submission cannot level again.");
            _test.True(runtime.GetActiveModalKind() != RuntimeModalKind.Promotion, "Replay cannot resurrect a closed modal.");
            ClaimRewards(runner, runtime);

            manager = runtime.GetCharacterManagement();
            manager.GrantBattleMastery(memberId, "warrior_guard", 100000);
            _test.Eq(runtime.PersistPartyState(), (int)Error.Ok, "Save pending opportunity without saving a prompt.");
            string saveId = session.GetGameSessionTyped().GetActiveSaveId();
            _test.True(session.LoadGameTyped(saveId).Ok, "New save schema reloads successfully.");
            runtime = session.GetRuntimeFacadeTyped();
            ClaimRewards(runner, runtime);
            runtime.advance(0);
            _test.Eq(runtime.GetActiveModalKind(), RuntimeModalKind.Promotion, "Loading rediscovers and presents a remaining opportunity.");
            _test.Eq(CurrentRequest(runtime, "warrior_guard").TargetRank, 2, "Reload queries second promotion from skill and history facts.");
            Check(runner, "promotion defer");

            Check(runner, "battle start single");
            if (runtime.GetActiveModalKind() == RuntimeModalKind.BattleStartConfirm)
                Check(runner, "battle confirm");
            _test.True(session.GetGameSessionTyped().IsBattleSaveLocked(), "Battle holds the normal save lock.");
            Check(runner, "promotion open player_sword_01");
            var battle = runtime.GetBattleRuntime().GetState();
            _test.True(battle.timeline.frozen, "Opening a battle promotion freezes the timeline.");
            Check(runner, "promotion defer");
            _test.False(battle.timeline.frozen, "Deferring restores the battle timeline.");
            Check(runner, "promotion open player_sword_01");
            Check(runner, "promotion choose warrior warrior_guard");
            member = runtime.GetCharacterManagement().GetMemberState(memberId);
            _test.Eq(member.progression.character_level, 2, "Second promotion accepts the old 3/5 core plus the new milestone skill.");
            _test.Eq(member.progression.GetUsedGrowthTriggerIds().Count, 2, "Each promotion consumes one distinct growth skill.");
            var battleMember = runtime.GetBattleRuntime()._find_unit_by_member_id(memberId);
            _test.Eq(battleMember.GetKnownSkillLockHitBonusTyped("warrior_guard"), 1, "Battle projection grants the completed skill bonus from history.");
            _test.Eq(battleMember.GetKnownSkillLockHitBonusTyped("warrior_heavy_strike"), 1, "The original growth bonus survives later promotions.");
            _test.Eq(member.progression.GetSkillProgress("warrior_heavy_strike").skill_level, 3, "The original core never needed retraining to 5.");
            _test.False(battle.timeline.frozen, "Successful battle promotion also resumes time.");
            _test.True(session.GetGameSessionTyped().IsBattleSaveLocked(), "Promotion does not bypass the battle save lock.");
            Check(runner, "battle finish player");
            _test.False(session.GetGameSessionTyped().IsBattleSaveLocked(), "Normal battle completion releases the save lock.");
            _test.True(session.LoadGameTyped(saveId).Ok, "Battle writeback saves the completed history.");
            runtime = session.GetRuntimeFacadeTyped();
            member = runtime.GetCharacterManagement().GetMemberState(memberId);
            _test.Eq(member.progression.character_level, 2, "Both promotions persist through battle writeback and reload.");
            TestPersistenceFailureDoesNotRepeatGrowth(runner);
        }
        catch (Exception error)
        {
            _test.Fail(error.ToString());
        }
        finally
        {
            runner.Dispose(true);
            RequestTestExit(_test.Finish("First promotion closed loop regression"));
        }
    }

    private void TestPersistenceFailureDoesNotRepeatGrowth(GameTextCommandRunner runner)
    {
        Check(runner, "game new test");
        var session = runner.GetSession();
        var runtime = session.GetRuntimeFacadeTyped();
        var manager = runtime.GetCharacterManagement();
        StringName memberId = "player_sword_01";
        foreach (StringName skillId in new StringName[] { "warrior_heavy_strike", "warrior_guard", "warrior_backstep" })
            if (manager.GetMemberState(memberId).progression.GetSkillProgress(skillId)?.is_learned != true)
                _test.True(manager.LearnSkill(memberId, skillId), "Learn the required warrior skills for the persistence failure fixture.");
        manager.GrantBattleMastery(memberId, "warrior_heavy_strike", 100000);
        ClaimRewards(runner, runtime);
        runtime.advance(0);
        _test.Eq(runtime.GetActiveModalKind(), RuntimeModalKind.Promotion,
            "The persistence failure fixture uses the automatically issued prompt.");
        var request = CurrentRequest(runtime, "warrior_heavy_strike");
        // Inject a real persistence rejection through the existing save lock, without disk damage.
        session.GetGameSessionTyped().SetBattleSaveLock(true);
        try
        {
            var result = runtime.CommandSubmitPromotionChoiceTyped(memberId, "warrior", request);
            _test.False(result.Ok, "A failed disk persistence is not reported as a saved promotion.");
            _test.Eq(result.Code, RuntimeCommandCode.PersistenceFailure, "Persistence failure has a typed result.");
        }
        finally
        {
            session.GetGameSessionTyped().SetBattleSaveLock(false);
        }
        var progress = manager.GetMemberState(memberId).progression;
        int hpGrowth = progress.unit_base_attributes.GetAttributeValue(AttributeService.HP_MAX);
        _test.Eq(progress.character_level, 1, "A published promotion remains applied exactly once in memory.");
        _test.True(runtime.GetActiveModalKind() != RuntimeModalKind.Promotion, "Persistence failure does not leave a reusable promotion prompt.");
        _test.False(runtime.CommandSubmitPromotionChoiceTyped(memberId, "warrior", request).Ok, "Retrying the old choice cannot reroll growth.");
        _test.Eq(manager.GetMemberState(memberId).progression.unit_base_attributes.GetAttributeValue(AttributeService.HP_MAX), hpGrowth, "Repeated submission preserves the original HP roll.");
        _test.Eq(runtime.PersistPartyState(), (int)Error.Ok, "The current party state can be saved after storage becomes available.");
        string saveId = session.GetGameSessionTyped().GetActiveSaveId();
        _test.True(session.LoadGameTyped(saveId).Ok, "The retried save reloads successfully.");
        _test.Eq(session.GetRuntimeFacadeTyped().GetCharacterManagement().GetMemberState(memberId).progression.character_level,
            1, "The retried save contains exactly one completed promotion.");
    }

    private void ClaimRewards(GameTextCommandRunner runner, GameRuntimeFacade runtime)
    {
        for (int count = 0; count < 20 && runtime.GetActiveModalKind() == RuntimeModalKind.Reward; count++)
            Check(runner, "reward confirm");
        _test.True(runtime.GetActiveModalKind() != RuntimeModalKind.Reward, "Existing achievement rewards can be claimed without re-opening promotion.");
    }

    private void Check(GameTextCommandRunner runner, string command)
    {
        var result = runner.ExecuteLine(command);
        _test.True(result.ok && !result.skipped, $"{command}: {result.message}");
        if (!result.ok) throw new InvalidOperationException($"Command failed: {command}: {result.message}");
    }

    private static PromotionCommitRequest CurrentRequest(GameRuntimeFacade runtime, StringName skillId)
    {
        var plain = runtime.GetCurrentPromotionPromptSnapshotPlain();
        var choices = (System.Collections.Generic.IEnumerable<object>)plain["choices"];
        foreach (System.Collections.Generic.IReadOnlyDictionary<string, object> choice in choices)
        {
            var request = PromotionCommitRequest.FromPlainPayload(
                (System.Collections.Generic.IReadOnlyDictionary<string, object>)choice["selection"]);
            if ((string)choice["profession_id"] == "warrior" && request.GrowthTriggerSkillId == skillId)
                return request;
        }
        throw new InvalidOperationException($"No warrior offer for {skillId}.");
    }
}
