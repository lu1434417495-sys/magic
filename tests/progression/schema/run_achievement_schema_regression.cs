using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_achievement_schema_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

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

        RequestTestExit(_test.Finish("Achievement schema regression"));
    }

    private void TestValidRoundTrips()
    {
        AchievementRewardDef reward = BuildValidReward();
        AchievementRewardDef restoredReward = AchievementRewardDef.FromDictionary(reward.ToDictionary());
        _test.True(restoredReward != null, "AchievementRewardDef valid to_dict payload should round-trip.");
        if (restoredReward != null)
        {
            _test.Eq(restoredReward.reward_type, reward.reward_type, "AchievementRewardDef should preserve reward_type.");
            _test.Eq(restoredReward.target_id, reward.target_id, "AchievementRewardDef should preserve target_id.");
            _test.Eq(restoredReward.amount, reward.amount, "AchievementRewardDef should preserve amount.");
        }

        AchievementDef achievement = BuildValidAchievement();
        AchievementDef restoredAchievement = AchievementDef.FromDictionary(achievement.ToDictionary());
        _test.True(restoredAchievement != null, "AchievementDef valid to_dict payload should round-trip.");
        if (restoredAchievement == null)
        {
            return;
        }

        _test.Eq(restoredAchievement.achievement_id, achievement.achievement_id, "AchievementDef should preserve achievement_id.");
        _test.Eq(restoredAchievement.event_type, achievement.event_type, "AchievementDef should preserve event_type.");
        _test.Eq(restoredAchievement.threshold, achievement.threshold, "AchievementDef should preserve threshold.");
        _test.Eq(restoredAchievement.rewards.Count, 1, "AchievementDef should preserve rewards.");
        if (restoredAchievement.rewards.Count > 0)
        {
            _test.Eq(
                restoredAchievement.rewards[0].target_id,
                reward.target_id,
                "AchievementDef should preserve nested reward payload."
            );
        }
    }

    private void TestAchievementDefRejectsSchemaDefaults()
    {
        _test.True(
            AchievementDef.FromDictionary(new GDictionary()) == null,
            "AchievementDef.from_dict should reject empty Dictionary payloads."
        );

        GDictionary missingThreshold = BuildValidAchievementPayload();
        missingThreshold.Remove("threshold");
        _test.True(
            AchievementDef.FromDictionary(missingThreshold) == null,
            "AchievementDef should reject payloads missing threshold."
        );

        GDictionary extraField = BuildValidAchievementPayload();
        extraField["legacy_subject"] = "charge";
        _test.True(
            AchievementDef.FromDictionary(extraField) == null,
            "AchievementDef should reject payloads with non-current fields."
        );

        GDictionary wrongRewards = BuildValidAchievementPayload();
        wrongRewards["rewards"] = new GDictionary();
        _test.True(
            AchievementDef.FromDictionary(wrongRewards) == null,
            "AchievementDef should reject non-Array rewards."
        );

        GDictionary invalidRewardEntry = BuildValidAchievementPayload();
        GDictionary rewardPayload = BuildValidRewardPayload();
        rewardPayload.Remove("target_id");
        invalidRewardEntry["rewards"] = new GArray { rewardPayload };
        _test.True(
            AchievementDef.FromDictionary(invalidRewardEntry) == null,
            "AchievementDef should reject invalid reward entries."
        );

        GDictionary emptyEventType = BuildValidAchievementPayload();
        emptyEventType["event_type"] = "";
        _test.True(
            AchievementDef.FromDictionary(emptyEventType) == null,
            "AchievementDef should reject empty event_type."
        );

        GDictionary badSubjectId = BuildValidAchievementPayload();
        badSubjectId["subject_id"] = default(Variant);
        _test.True(
            AchievementDef.FromDictionary(badSubjectId) == null,
            "AchievementDef should reject non-string subject_id."
        );
    }

    private void TestAchievementRewardDefRejectsSchemaDefaults()
    {
        _test.True(
            AchievementRewardDef.FromDictionary(new GDictionary()) == null,
            "AchievementRewardDef.from_dict should reject empty Dictionary payloads."
        );

        GDictionary missingTargetId = BuildValidRewardPayload();
        missingTargetId.Remove("target_id");
        _test.True(
            AchievementRewardDef.FromDictionary(missingTargetId) == null,
            "AchievementRewardDef should reject payloads missing target_id."
        );

        GDictionary extraField = BuildValidRewardPayload();
        extraField["legacy_amount"] = 1;
        _test.True(
            AchievementRewardDef.FromDictionary(extraField) == null,
            "AchievementRewardDef should reject payloads with non-current fields."
        );

        GDictionary stringAmount = BuildValidRewardPayload();
        stringAmount["amount"] = "1";
        _test.True(
            AchievementRewardDef.FromDictionary(stringAmount) == null,
            "AchievementRewardDef should reject string amount."
        );

        GDictionary emptyAmount = BuildValidRewardPayload();
        emptyAmount["amount"] = 0;
        _test.True(
            AchievementRewardDef.FromDictionary(emptyAmount) != null,
            "AchievementRewardDef should accept zero amount for skill_unlock."
        );

        GDictionary zeroAttributeDelta = BuildValidRewardPayload();
        zeroAttributeDelta["reward_type"] = "attribute_delta";
        zeroAttributeDelta["amount"] = 0;
        _test.True(
            AchievementRewardDef.FromDictionary(zeroAttributeDelta) == null,
            "AchievementRewardDef should reject zero amount for attribute_delta."
        );
    }

    private void TestAchievementDefAcceptsEmptyRewardsArray()
    {
        GDictionary payload = BuildValidAchievementPayload();
        payload["rewards"] = new GArray();
        AchievementDef restored = AchievementDef.FromDictionary(payload);
        _test.True(restored != null, "AchievementDef should accept explicit empty rewards array.");
        if (restored != null)
        {
            _test.True(restored.rewards.Count == 0, "AchievementDef should preserve empty rewards array.");
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
        return BuildValidAchievement().ToDictionary();
    }

    private static GDictionary BuildValidRewardPayload()
    {
        return BuildValidReward().ToDictionary();
    }
}
