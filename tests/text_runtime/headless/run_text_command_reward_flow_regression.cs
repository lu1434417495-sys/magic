using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_text_command_reward_flow_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestTextCommandsUseTypedRewardFlowBoundary();

        Quit(_test.Finish("Text command reward flow regression"));
    }

    private void TestTextCommandsUseTypedRewardFlowBoundary()
    {
        GameTextCommandRunner runner = new();
        runner.initialize();
        try
        {
            AssertCommandOk(runner.ExecuteLine("game new test"), "game new test 应成功。");

            HeadlessGameTestSession session = runner.GetSession();
            GameRuntimeFacade runtime = session?.GetRuntimeFacadeTyped();
            GameSession gameSession = session?.GetGameSessionTyped();
            _test.True(runtime != null, "reward flow 文本回归应拿到 typed runtime。");
            _test.True(gameSession != null, "reward flow 文本回归应拿到 typed game session。");
            if (runtime == null || gameSession == null)
                return;

            GameTextCommandResult partyOpenResult = runner.ExecuteLine("party open");
            AssertCommandOk(partyOpenResult, "party open 应成功。");
            _test.Eq(
                SnapshotString(partyOpenResult.snapshot, "modal", "id"),
                "party",
                "party open 后应进入 party modal。"
            );

            GameTextCommandResult partyCloseResult = runner.ExecuteLine("close");
            AssertCommandOk(partyCloseResult, "close 应通过 typed reward-flow 路由关闭 party modal。");
            _test.Eq(
                SnapshotString(partyCloseResult.snapshot, "modal", "id"),
                "",
                "close party 后应清空 modal。"
            );

            runtime.SetPendingWorldPromotionPromptState(
                new GDictionary
                {
                    ["member_id"] = "player_sword_01",
                    ["choices"] = new GArray(),
                }
            );
            _test.True(
                runtime.PresentPendingRewardIfReady(),
                "存在 world promotion prompt 时应进入 promotion modal。"
            );

            GameTextCommandResult promotionResult = runner.ExecuteLine("promotion choose warrior");
            _test.False(
                promotionResult.ok,
                "promotion choose 缺失职业选项时应返回正式失败。"
            );
            _test.Eq(
                promotionResult.code,
                GameRuntimeFacade.RuntimeCommandCode.InvalidState,
                "promotion choose 缺失职业选项时应返回 InvalidState code。"
            );
            _test.Eq(
                SnapshotString(promotionResult.snapshot, "modal", "id"),
                "promotion",
                "promotion choose 失败后应仍停留在 promotion modal。"
            );

            runtime.ClearPendingWorldPromotionPromptState();
            runtime.SetRuntimeActiveModalKind(RuntimeModalKind.None);

            gameSession.GetPartyState().pending_character_rewards.Add(BuildPendingReward());
            _test.True(
                runtime.PresentPendingRewardIfReady(),
                "存在待领奖励时应进入 reward modal。"
            );

            GameTextCommandResult blockedCloseResult = runner.ExecuteLine("close");
            _test.False(
                blockedCloseResult.ok,
                "reward modal 不应允许通过 close 跳过。"
            );
            _test.Eq(
                blockedCloseResult.code,
                GameRuntimeFacade.RuntimeCommandCode.InvalidState,
                "reward modal close 应返回 InvalidState code。"
            );
            _test.Eq(
                SnapshotString(blockedCloseResult.snapshot, "modal", "id"),
                "reward",
                "reward modal close 失败后应仍停留在 reward modal。"
            );

            GameTextCommandResult rewardConfirmResult = runner.ExecuteLine("reward confirm");
            AssertCommandOk(rewardConfirmResult, "reward confirm 应成功。");
            _test.Eq(
                SnapshotString(rewardConfirmResult.snapshot, "modal", "id"),
                "",
                "reward confirm 后应清空 modal。"
            );
            _test.Eq(
                SnapshotInt(rewardConfirmResult.snapshot, "party", "pending_reward_count"),
                0,
                "reward confirm 后待处理奖励数量应归零。"
            );
        }
        finally
        {
            runner.Dispose(true);
        }
    }

    private static PendingCharacterReward BuildPendingReward()
    {
        PendingCharacterRewardEntry entry = new()
        {
            entry_type = "skill_mastery",
            target_id = "test_skill",
            target_label = "测试技能",
            amount = 1,
            reason_text = "测试奖励",
        };
        return new PendingCharacterReward
        {
            reward_id = "text_reward",
            member_id = "player_sword_01",
            member_name = "主角",
            source_type = "text_reward",
            source_id = "text_reward",
            source_label = "文本奖励",
            summary_text = "文本奖励",
            entries = new Godot.Collections.Array<PendingCharacterRewardEntry> { entry },
        };
    }

    private static string SnapshotString(GDictionary snapshot, string topLevelKey, string nestedKey) =>
        DictString(Dict(snapshot, topLevelKey), nestedKey, "");

    private static int SnapshotInt(GDictionary snapshot, string topLevelKey, string nestedKey) =>
        DictInt(Dict(snapshot, topLevelKey), nestedKey, 0);

    private static GDictionary Dict(GDictionary dictionary, string key) =>
        dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsGodotDictionary()
            : new GDictionary();

    private static string DictString(GDictionary dictionary, string key, string fallback) =>
        dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsString()
            : fallback;

    private static int DictInt(GDictionary dictionary, string key, int fallback) =>
        dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsInt32()
            : fallback;

    private void AssertCommandOk(GameTextCommandResult result, string message)
    {
        _test.True(result != null && result.ok, $"{message} message={result?.message}");
    }
}
