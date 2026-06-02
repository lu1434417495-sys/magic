using Godot;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_faith_service_reward_regression : SceneTree
{
    private static readonly StringName FortunaDeityId = "fortuna";
    private static readonly StringName FortuneMarkedStatId = "fortune_marked";
    private static readonly StringName FaithLuckBonusStatId = "faith_luck_bonus";

    private readonly GStringArray _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        TestFortunaRankRewardUsesTypedRewardEntryFields();

        if (_failures.Count == 0)
        {
            GD.Print("Faith service reward regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Faith service reward regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestFortunaRankRewardUsesTypedRewardEntryFields()
    {
        PartyState partyState = BuildPartyState();
        var faithService = new FaithService();

        FaithDevotionResult result = faithService.ExecuteDevotion(
            partyState,
            "hero",
            FortunaDeityId
        );
        PendingCharacterReward reward = partyState.get_next_pending_character_reward();

        AssertTrue(result.Success, "Fortuna rank 1 应能生成 pending reward。");
        AssertEq(result.TargetRank, 1, "首次 devotion 应指向 rank 1。");
        AssertTrue(reward != null, "成功 devotion 后应排入 pending reward。");
        AssertEq(
            reward != null ? reward.source_type : "",
            FaithService.SourceTypeFaithRankReward,
            "pending reward source type 应来自 faith rank。"
        );
        AssertTrue(
            reward != null && HasRewardEntry(reward, "attribute_delta", FaithLuckBonusStatId, 1),
            "Fortuna rank 1 reward entry 应指向 faith_luck_bonus +1。"
        );
    }

    private static PartyState BuildPartyState()
    {
        var partyState = new PartyState
        {
            leader_member_id = "hero",
            main_character_member_id = "hero",
            active_member_ids = new GStringNameArray { "hero" },
        };
        partyState.set_gold(50000);
        partyState.set_member_state(BuildPartyMemberState());
        return partyState;
    }

    private static PartyMemberState BuildPartyMemberState()
    {
        var memberState = new PartyMemberState
        {
            member_id = "hero",
            display_name = "Hero",
        };
        memberState.progression.unit_id = "hero";
        memberState.progression.display_name = "Hero";
        memberState.progression.character_level = 30;
        memberState.progression.unit_base_attributes.custom_stats[FortuneMarkedStatId] = 1;
        memberState.progression.unit_base_attributes.custom_stats[FaithLuckBonusStatId] = 0;
        return memberState;
    }

    private static bool HasRewardEntry(
        PendingCharacterReward reward,
        StringName entryType,
        StringName targetId,
        int amount
    )
    {
        if (reward == null)
            return false;
        foreach (PendingCharacterRewardEntry entry in reward.entries)
        {
            if (
                entry != null
                && entry.entry_type == entryType
                && entry.target_id == targetId
                && entry.amount == amount
            )
            {
                return true;
            }
        }
        return false;
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!Equals(actual, expected))
        {
            _failures.Add($"{message} expected={expected} actual={actual}");
        }
    }

    private void AssertTrue(bool value, string message)
    {
        if (!value)
        {
            _failures.Add(message);
        }
    }
}
