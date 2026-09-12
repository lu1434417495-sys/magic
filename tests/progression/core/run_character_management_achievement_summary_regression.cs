using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class run_character_management_achievement_summary_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestAchievementSummarySortsActiveProgressWithTypedEntries();
        TestAchievementPendingRewardSummaryTextUsesStringMetaOrDescription();

        RequestTestExit(_test.Finish("Character management achievement summary regression"));
    }

    private void TestAchievementSummarySortsActiveProgressWithTypedEntries()
    {
        AchievementDefinition gamma = MakeAchievement("gamma", "Gamma", 20);
        AchievementDefinition aardvark = MakeAchievement("aardvark", "Aardvark", 10);
        AchievementDefinition alpha = MakeAchievement("alpha", "Alpha", 10);
        AchievementDefinition beta = MakeAchievement("beta", "Beta", 8);
        AchievementDefinition unlockedOld = MakeAchievement("unlocked_old", "Old Unlock", 1);
        AchievementDefinition unlockedRecent = MakeAchievement("unlocked_recent", "Recent Unlock", 1);
        AchievementDefinition zeroProgress = MakeAchievement("zero_progress", "Zero", 5);

        PartyState party = BuildPartyWithMember("hero");
        UnitProgress progression = party.GetMemberState("hero").progression;
        SetAchievementProgress(progression, gamma.AchievementId, 12);
        SetAchievementProgress(progression, aardvark.AchievementId, 5);
        SetAchievementProgress(progression, alpha.AchievementId, 5);
        SetAchievementProgress(progression, beta.AchievementId, 4);
        SetAchievementProgress(progression, zeroProgress.AchievementId, 0);
        SetAchievementUnlocked(progression, unlockedOld.AchievementId, 10);
        SetAchievementUnlocked(progression, unlockedRecent.AchievementId, 12);

        CharacterManagementModule manager = BuildManager(
            party,
            gamma,
            aardvark,
            alpha,
            beta,
            unlockedOld,
            unlockedRecent,
            zeroProgress
        );

        using GodotProjectionLease<GDictionary> summaryLease =
            manager.GetMemberAchievementSummaryLease("hero");
        GDictionary summary = summaryLease.Value;
        _test.Eq(ReadInt(summary, "unlocked_count"), 2, "summary should count unlocked achievements.");
        _test.Eq(ReadInt(summary, "in_progress_count"), 4, "summary should count active progress only.");
        _test.Eq(
            ReadString(summary, "recent_unlocked_name"),
            "Recent Unlock",
            "summary should keep the most recent unlocked achievement name."
        );

        GArray activeEntries = ReadArray(summary, "active_progress_entries");
        _test.Eq(activeEntries.Count, 4, "summary should expose four active progress rows.");
        AssertEntry(activeEntries, 0, "Gamma", 12, 20);
        AssertEntry(activeEntries, 1, "Aardvark", 5, 10);
        AssertEntry(activeEntries, 2, "Alpha", 5, 10);
        AssertEntry(activeEntries, 3, "Beta", 4, 8);
    }

    private void TestAchievementPendingRewardSummaryTextUsesStringMetaOrDescription()
    {
        AchievementDefinition customSummary = MakeAchievement(
            "custom_summary",
            "Custom Summary",
            1,
            rewards: [MakeAttributeReward("custom reason")]
        );
        AchievementDefinition defaultSummary = MakeAchievement(
            "default_summary",
            "Default Summary",
            1,
            "Achievement description fallback.",
            [MakeAttributeReward("default reason")]
        );

        PartyState party = BuildPartyWithMember("hero");
        CharacterManagementModule manager = BuildManager(party, customSummary, defaultSummary);

        _test.True(
            manager.UnlockAchievement(
                "hero",
                customSummary.AchievementId,
                new GDictionary { ["summary_text"] = "Custom achievement summary." }
            ),
            "custom summary achievement should unlock."
        );
        PendingCharacterReward reward = party.GetNextPendingCharacterReward();
        _test.True(reward != null, "custom summary unlock should queue a pending reward.");
        if (reward != null)
            _test.Eq(
                reward.summary_text,
                "Custom achievement summary.",
                "string summary_text meta should be preserved."
            );

        party.pending_character_rewards.Clear();
        _test.True(
            manager.UnlockAchievement("hero", defaultSummary.AchievementId),
            "default summary achievement should unlock."
        );
        reward = party.GetNextPendingCharacterReward();
        _test.True(reward != null, "default summary unlock should queue a pending reward.");
        if (reward != null)
            _test.Eq(
                reward.summary_text,
                "Achievement description fallback.",
                "missing summary_text meta should fall back to achievement description."
            );
    }

    private static CharacterManagementModule BuildManager(
        PartyState party,
        params AchievementDefinition[] achievementDefs
    )
    {
        Dictionary<StringName, AchievementDefinition> achievementDefinitions = new();
        foreach (AchievementDefinition achievementDefinition in achievementDefs)
        {
            if (achievementDefinition != null)
                achievementDefinitions[achievementDefinition.AchievementId] = achievementDefinition;
        }

        CharacterManagementModule manager = new();
        manager.setup(
            party,
            new Dictionary<StringName, SkillDefinition>(),
            new Dictionary<StringName, ProfessionDefinition>(),
            achievementDefinitions,
            new Dictionary<StringName, ItemDefinition>(),
            new Dictionary<StringName, QuestDefinition>(),
            null,
            new ProgressionIdentityCatalogData()
        );
        return manager;
    }

    private static PartyState BuildPartyWithMember(string memberId)
    {
        PartyState party = new();
        PartyMemberState member = new()
        {
            member_id = memberId,
            display_name = memberId,
        };
        party.SetMemberState(member);
        party.active_member_ids.Add(member.member_id);
        return party;
    }

    private static AchievementDefinition MakeAchievement(
        StringName achievementId,
        string displayName,
        int threshold,
        string description = null,
        IReadOnlyList<AchievementRewardDefinition> rewards = null
    ) =>
        new(
            achievementId,
            displayName,
            description ?? $"{displayName} description",
            "test_event",
            "",
            threshold,
            rewards ?? System.Array.Empty<AchievementRewardDefinition>()
        );

    private static AchievementRewardDefinition MakeAttributeReward(string reasonText) =>
        new(
            "attribute_delta",
            UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Strength),
            "Strength",
            1,
            reasonText
        );

    private static void SetAchievementProgress(
        UnitProgress progression,
        StringName achievementId,
        int currentValue
    )
    {
        progression.SetAchievementProgressState(
            new AchievementProgressState
            {
                achievement_id = achievementId,
                current_value = currentValue,
            }
        );
    }

    private static void SetAchievementUnlocked(
        UnitProgress progression,
        StringName achievementId,
        int unlockedAt
    )
    {
        progression.SetAchievementProgressState(
            new AchievementProgressState
            {
                achievement_id = achievementId,
                current_value = 1,
                is_unlocked = true,
                unlocked_at_unix_time = unlockedAt,
            }
        );
    }

    private void AssertEntry(
        GArray activeEntries,
        int index,
        string expectedName,
        int expectedCurrent,
        int expectedThreshold
    )
    {
        if (index >= activeEntries.Count)
        {
            _test.Fail($"missing active achievement entry at index {index}.");
            return;
        }
        Variant entryValue = activeEntries[index];
        if (entryValue.VariantType != Variant.Type.Dictionary)
        {
            _test.Fail($"active achievement entry {index} should be a dictionary.");
            return;
        }
        GDictionary entry = entryValue.AsGodotDictionary();
        _test.Eq(ReadString(entry, "display_name"), expectedName, $"entry {index} name should match.");
        _test.Eq(ReadInt(entry, "current_value"), expectedCurrent, $"entry {index} current should match.");
        _test.Eq(ReadInt(entry, "threshold"), expectedThreshold, $"entry {index} threshold should match.");
    }

    private static GArray ReadArray(GDictionary data, string key)
    {
        if (data == null || !data.ContainsKey(key))
            return new GArray();
        Variant value = data[key];
        return value.VariantType == Variant.Type.Array ? value.AsGodotArray() : new GArray();
    }

    private static int ReadInt(GDictionary data, string key)
    {
        if (data == null || !data.ContainsKey(key))
            return 0;
        Variant value = data[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : 0;
    }

    private static string ReadString(GDictionary data, string key)
    {
        if (data == null || !data.ContainsKey(key))
            return "";
        Variant value = data[key];
        return value.VariantType switch
        {
            Variant.Type.String => value.AsString(),
            Variant.Type.StringName => value.AsStringName().ToString(),
            _ => "",
        };
    }


}
