using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_achievement_schema_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestValidRoundTrips();
        TestAchievementDefRejectsSchemaDefaults();
        TestAchievementRewardDefRejectsSchemaDefaults();
        TestAchievementDefAcceptsEmptyRewardsArray();

        if (_failures.Count == 0)
        {
            GD.Print("Achievement schema regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Achievement schema regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void TestValidRoundTrips()
    {
        AchievementRewardDef reward = BuildValidReward();
        AchievementRewardDef restoredReward = AchievementRewardDef.from_dict(reward.to_dict());
        AssertTrue(restoredReward != null, "AchievementRewardDef valid to_dict payload should round-trip.");
        if (restoredReward != null)
        {
            AssertEq(restoredReward.reward_type, reward.reward_type, "AchievementRewardDef should preserve reward_type.");
            AssertEq(restoredReward.target_id, reward.target_id, "AchievementRewardDef should preserve target_id.");
            AssertEq(restoredReward.amount, reward.amount, "AchievementRewardDef should preserve amount.");
        }

        AchievementDef achievement = BuildValidAchievement();
        AchievementDef restoredAchievement = AchievementDef.from_dict(achievement.to_dict());
        AssertTrue(restoredAchievement != null, "AchievementDef valid to_dict payload should round-trip.");
        if (restoredAchievement == null)
        {
            return;
        }

        AssertEq(restoredAchievement.achievement_id, achievement.achievement_id, "AchievementDef should preserve achievement_id.");
        AssertEq(restoredAchievement.event_type, achievement.event_type, "AchievementDef should preserve event_type.");
        AssertEq(restoredAchievement.threshold, achievement.threshold, "AchievementDef should preserve threshold.");
        AssertEq(restoredAchievement.rewards.Count, 1, "AchievementDef should preserve rewards.");
        if (restoredAchievement.rewards.Count > 0)
        {
            AssertEq(
                restoredAchievement.rewards[0].target_id,
                reward.target_id,
                "AchievementDef should preserve nested reward payload."
            );
        }
    }

    private void TestAchievementDefRejectsSchemaDefaults()
    {
        AssertTrue(
            AchievementDef.from_dict(new GDictionary()) == null,
            "AchievementDef.from_dict should reject empty Dictionary payloads."
        );

        GDictionary missingThreshold = BuildValidAchievementPayload();
        missingThreshold.Remove("threshold");
        AssertTrue(
            AchievementDef.from_dict(missingThreshold) == null,
            "AchievementDef should reject payloads missing threshold."
        );

        GDictionary extraField = BuildValidAchievementPayload();
        extraField["legacy_subject"] = "charge";
        AssertTrue(
            AchievementDef.from_dict(extraField) == null,
            "AchievementDef should reject payloads with non-current fields."
        );

        GDictionary wrongRewards = BuildValidAchievementPayload();
        wrongRewards["rewards"] = new GDictionary();
        AssertTrue(
            AchievementDef.from_dict(wrongRewards) == null,
            "AchievementDef should reject non-Array rewards."
        );

        GDictionary invalidRewardEntry = BuildValidAchievementPayload();
        GDictionary rewardPayload = BuildValidRewardPayload();
        rewardPayload.Remove("target_id");
        invalidRewardEntry["rewards"] = new GArray { rewardPayload };
        AssertTrue(
            AchievementDef.from_dict(invalidRewardEntry) == null,
            "AchievementDef should reject invalid reward entries."
        );

        GDictionary emptyEventType = BuildValidAchievementPayload();
        emptyEventType["event_type"] = "";
        AssertTrue(
            AchievementDef.from_dict(emptyEventType) == null,
            "AchievementDef should reject empty event_type."
        );

        GDictionary badSubjectId = BuildValidAchievementPayload();
        badSubjectId["subject_id"] = default(Variant);
        AssertTrue(
            AchievementDef.from_dict(badSubjectId) == null,
            "AchievementDef should reject non-string subject_id."
        );
    }

    private void TestAchievementRewardDefRejectsSchemaDefaults()
    {
        AssertTrue(
            AchievementRewardDef.from_dict(new GDictionary()) == null,
            "AchievementRewardDef.from_dict should reject empty Dictionary payloads."
        );

        GDictionary missingTargetId = BuildValidRewardPayload();
        missingTargetId.Remove("target_id");
        AssertTrue(
            AchievementRewardDef.from_dict(missingTargetId) == null,
            "AchievementRewardDef should reject payloads missing target_id."
        );

        GDictionary extraField = BuildValidRewardPayload();
        extraField["legacy_amount"] = 1;
        AssertTrue(
            AchievementRewardDef.from_dict(extraField) == null,
            "AchievementRewardDef should reject payloads with non-current fields."
        );

        GDictionary stringAmount = BuildValidRewardPayload();
        stringAmount["amount"] = "1";
        AssertTrue(
            AchievementRewardDef.from_dict(stringAmount) == null,
            "AchievementRewardDef should reject string amount."
        );

        GDictionary emptyAmount = BuildValidRewardPayload();
        emptyAmount["amount"] = 0;
        AssertTrue(
            AchievementRewardDef.from_dict(emptyAmount) != null,
            "AchievementRewardDef should accept zero amount for skill_unlock."
        );

        GDictionary zeroAttributeDelta = BuildValidRewardPayload();
        zeroAttributeDelta["reward_type"] = "attribute_delta";
        zeroAttributeDelta["amount"] = 0;
        AssertTrue(
            AchievementRewardDef.from_dict(zeroAttributeDelta) == null,
            "AchievementRewardDef should reject zero amount for attribute_delta."
        );
    }

    private void TestAchievementDefAcceptsEmptyRewardsArray()
    {
        GDictionary payload = BuildValidAchievementPayload();
        payload["rewards"] = new GArray();
        AchievementDef restored = AchievementDef.from_dict(payload);
        AssertTrue(restored != null, "AchievementDef should accept explicit empty rewards array.");
        if (restored != null)
        {
            AssertTrue(restored.rewards.Count == 0, "AchievementDef should preserve empty rewards array.");
        }
    }

    private static AchievementDef BuildValidAchievement()
    {
        AchievementDef achievement = new()
        {
            achievement_id = "schema_round_trip",
            display_name = "Schema Round Trip",
            description = "Valid achievement schema payload.",
            event_type = "skill_learned",
            subject_id = "charge",
            threshold = 1,
        };
        achievement.rewards.Add(BuildValidReward());
        return achievement;
    }

    private static AchievementRewardDef BuildValidReward()
    {
        return new AchievementRewardDef
        {
            reward_type = "skill_unlock",
            target_id = "charge",
            target_label = "Charge",
            amount = 1,
            reason_text = "Schema reward.",
        };
    }

    private static GDictionary BuildValidAchievementPayload()
    {
        return BuildValidAchievement().to_dict();
    }

    private static GDictionary BuildValidRewardPayload()
    {
        return BuildValidReward().to_dict();
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
        {
            _failures.Add($"{message} | actual={actual} expected={expected}");
        }
    }
}
