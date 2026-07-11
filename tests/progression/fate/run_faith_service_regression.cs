using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_faith_service_regression : LifecycleTestSceneTree
{
    private static readonly StringName HeroId = "hero";
    private static readonly StringName FortunaDeityId = "fortuna";
    private static readonly StringName MisfortuneDeityId = "misfortune_black_crown";
    private static readonly StringName FaithLuckBonusStatId = "faith_luck_bonus";
    private static readonly StringName FortuneMarkedStatId = "fortune_marked";
    private static readonly StringName DoomMarkedStatId = "doom_marked";
    private static readonly StringName DoomAuthorityStatId = "doom_authority";
    private static readonly StringName CalamityCapacityBonusStatId = "calamity_capacity_bonus";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestResult exitCode = Run();
        RequestTestExit(exitCode);
    }

    private TestResult Run()
    {
        TestFortunaConfigMatchesStoryAcceptance();
        TestFortunaRankUpAppliesFaithLuckBonusUntilCap();
        TestMisfortuneConfigMatchesStoryAcceptance();
        TestMisfortuneRankUpAppliesDoomAuthorityUntilCap();
        TestFaithRankValidationRejectsUnsupportedRewardEntries();

        return _test.Finish("FaithService regression");
    }

    private void TestFortunaConfigMatchesStoryAcceptance()
    {
        var faithService = new FaithService();
        _test.True(faithService.Validate().Count == 0, "FaithService 默认配置应能通过基础校验。");

        FaithDeityDef fortunaDef = faithService.GetFaithDeityDef(FortunaDeityId);
        _test.True(fortunaDef != null, "应能加载 Fortuna FaithDeityDef。");
        if (fortunaDef == null)
            return;

        int[] expectedGold = { 500, 2000, 4500, 8000, 14000 };
        int[] expectedLevel = { 0, 8, 14, 20, 28 };
        StringName[] expectedAchievements =
        {
            "",
            "fortuna_guidance_true",
            "fortuna_guidance_devout",
            "fortuna_guidance_exalted",
            "fortuna_guidance_blessed",
        };

        _test.Eq(fortunaDef.GetMaxRank(), 5, "Fortuna 应保留 5 阶骨架。");
        for (int index = 0; index < 5; index++)
        {
            int rankIndex = index + 1;
            FaithRankDef rankDef = fortunaDef.GetRankDef(rankIndex);
            _test.True(rankDef != null, $"Fortuna 应存在 rank {rankIndex} 配置。");
            if (rankDef == null)
                continue;

            _test.Eq(rankDef.required_gold, expectedGold[index], $"Fortuna rank {rankIndex} required_gold 错误。");
            _test.Eq(rankDef.required_level, expectedLevel[index], $"Fortuna rank {rankIndex} required_level 错误。");
            if (rankIndex == 1)
            {
                _test.Eq(
                    rankDef.required_custom_stat_id,
                    FortuneMarkedStatId,
                    "Fortuna rank 1 应使用 fortune_marked 占位门票。"
                );
                _test.Eq(rankDef.required_custom_stat_min_value, 1, "Fortuna rank 1 应要求 fortune_marked == 1。");
            }
            else
            {
                _test.Eq(
                    rankDef.required_achievement_id,
                    expectedAchievements[index],
                    $"Fortuna rank {rankIndex} guidance achievement 占位 id 错误。"
                );
            }

            IReadOnlyList<FaithRankRewardEntrySpec> rewardEntries = rankDef.GetRewardEntrySpecs();
            _test.Eq(rewardEntries.Count, 1, $"Fortuna rank {rankIndex} 应只有一条骨架奖励。");
            if (rewardEntries.Count == 0)
                continue;
            _test.Eq(
                rewardEntries[0].TargetId,
                FaithLuckBonusStatId,
                $"Fortuna rank {rankIndex} 奖励应写入 faith_luck_bonus。"
            );
            _test.Eq(rewardEntries[0].Amount, 1, $"Fortuna rank {rankIndex} 奖励应固定 +1。");
        }
    }

    private void TestFortunaRankUpAppliesFaithLuckBonusUntilCap()
    {
        PartyState partyState = BuildPartyState();
        var manager = new CharacterManagementModule();
        manager.setup(partyState);
        var faithService = new FaithService();

        for (int targetRank = 1; targetRank <= 5; targetRank++)
        {
            FaithDevotionResult devotionResult = faithService.ExecuteDevotion(
                partyState,
                HeroId,
                FortunaDeityId
            );
            _test.True(devotionResult.Success, $"Fortuna rank {targetRank} 应能成功进入 pending reward 队列。");
            if (!devotionResult.Success)
                return;
            _test.Eq(devotionResult.TargetRank, targetRank, "Fortuna 每次只应提升 1 阶。");

            PendingCharacterReward pendingReward = partyState.GetNextPendingCharacterReward();
            _test.True(pendingReward != null, $"Fortuna rank {targetRank} 成功后应排入 pending reward。");
            if (pendingReward == null)
                return;

            CharacterProgressionDelta delta = manager.ApplyPendingCharacterReward(pendingReward);
            _test.Eq(
                partyState.GetMemberState(HeroId).GetFaithLuckBonus(),
                targetRank,
                $"Fortuna rank {targetRank} 结算后应把 faith_luck_bonus 写到正确值。"
            );
            _test.Eq(
                delta.AttributeChangesTyped.Count,
                1,
                $"Fortuna rank {targetRank} 只应产生一条 attribute delta。"
            );
            if (delta.AttributeChangesTyped.Count > 0)
            {
                _test.Eq(
                    delta.AttributeChangesTyped[0].AttributeId,
                    FaithLuckBonusStatId,
                    $"Fortuna rank {targetRank} 的 delta 应指向 faith_luck_bonus。"
                );
            }
        }

        FaithDevotionResult capResult = faithService.ExecuteDevotion(
            partyState,
            HeroId,
            FortunaDeityId
        );
        _test.False(capResult.Success, "达到 rank 5 后不应继续升级。");
        _test.Eq(capResult.ErrorCode, "max_rank_reached", "达到上限后应返回 max_rank_reached。");
        _test.Eq(
            partyState.GetMemberState(HeroId).GetFaithLuckBonus(),
            5,
            "达到上限后 faith_luck_bonus 不应继续增加。"
        );
        _test.True(partyState.GetNextPendingCharacterReward() == null, "达到上限后不应新增 pending reward。");
        manager.Dispose();
    }

    private void TestMisfortuneConfigMatchesStoryAcceptance()
    {
        var faithService = new FaithService();
        FaithDeityDef misfortuneDef = faithService.GetFaithDeityDef(MisfortuneDeityId);
        _test.True(misfortuneDef != null, "应能加载 Misfortune FaithDeityDef。");
        if (misfortuneDef == null)
            return;

        int[] expectedGold = { 500, 2000, 4500, 8000, 14000 };
        int[] expectedLevel = { 0, 8, 14, 20, 28 };
        StringName[] expectedAchievements =
        {
            "",
            "misfortune_guidance_true",
            "misfortune_guidance_devout",
            "misfortune_guidance_exalted",
            "misfortune_guidance_blessed",
        };
        StringName[] expectedPlaceholders =
        {
            "black_star_brand",
            CalamityCapacityBonusStatId,
            "crown_break",
            CalamityCapacityBonusStatId,
            "doom_sentence",
        };

        _test.Eq(misfortuneDef.GetMaxRank(), 5, "Misfortune 应保留 5 阶骨架。");
        _test.Eq(misfortuneDef.rank_progress_stat_id, DoomAuthorityStatId, "Misfortune 应使用 doom_authority 作为 rank progress stat。");
        for (int index = 0; index < 5; index++)
        {
            int rankIndex = index + 1;
            FaithRankDef rankDef = misfortuneDef.GetRankDef(rankIndex);
            _test.True(rankDef != null, $"Misfortune 应存在 rank {rankIndex} 配置。");
            if (rankDef == null)
                continue;

            _test.Eq(rankDef.required_gold, expectedGold[index], $"Misfortune rank {rankIndex} required_gold 错误。");
            _test.Eq(rankDef.required_level, expectedLevel[index], $"Misfortune rank {rankIndex} required_level 错误。");
            if (rankIndex == 1)
            {
                _test.Eq(
                    rankDef.required_custom_stat_id,
                    DoomMarkedStatId,
                    "Misfortune rank 1 应使用 doom_marked 占位门票。"
                );
                _test.Eq(rankDef.required_custom_stat_min_value, 1, "Misfortune rank 1 应要求 doom_marked == 1。");
            }
            else
            {
                _test.Eq(
                    rankDef.required_achievement_id,
                    expectedAchievements[index],
                    $"Misfortune rank {rankIndex} guidance achievement 占位 id 错误。"
                );
            }

            _test.Eq(rankDef.GetRewardEntrySpecs().Count, 2, $"Misfortune rank {rankIndex} 应保留 1 条属性奖励和 1 条占位奖励。");
            _test.True(
                HasRewardEntry(rankDef, "attribute_delta", DoomAuthorityStatId, 1),
                $"Misfortune rank {rankIndex} 应包含 doom_authority +1。"
            );

            StringName placeholderEntryType = expectedPlaceholders[index] == CalamityCapacityBonusStatId
                ? "attribute_delta"
                : "knowledge_unlock";
            _test.True(
                HasRewardEntry(rankDef, placeholderEntryType, expectedPlaceholders[index], 1),
                $"Misfortune rank {rankIndex} 的占位奖励配置错误。"
            );
        }
    }

    private void TestMisfortuneRankUpAppliesDoomAuthorityUntilCap()
    {
        PartyState partyState = BuildPartyState();
        var manager = new CharacterManagementModule();
        manager.setup(partyState);
        var faithService = new FaithService();
        _test.True(
            faithService.GetFaithDeityDef(MisfortuneDeityId) != null,
            $"Misfortune FaithService lookup 应在 manager setup 后仍可用。 registered={string.Join(",", faithService.GetFaithDeityDefs().Keys.Select(key => key.ToString()))} validation={string.Join(" | ", faithService.Validate())}"
        );
        var expectedKnowledgeUnlocks = new Dictionary<int, StringName>
        {
            [1] = "black_star_brand",
            [3] = "crown_break",
            [5] = "doom_sentence",
        };
        var expectedCalamityBonusByRank = new Dictionary<int, int>
        {
            [2] = 1,
            [4] = 2,
        };

        for (int targetRank = 1; targetRank <= 5; targetRank++)
        {
            FaithDevotionResult devotionResult = faithService.ExecuteDevotion(
                partyState,
                HeroId,
                MisfortuneDeityId
            );
            _test.True(
                devotionResult.Success,
                $"Misfortune rank {targetRank} 应能成功进入 pending reward 队列。"
                    + $" error={devotionResult.ErrorCode}"
                    + $" missing_stat={devotionResult.MissingCustomStatId}"
                    + $" missing_achievement={devotionResult.MissingAchievementId}"
                    + $" current={devotionResult.CurrentRank}"
                    + $" target={devotionResult.TargetRank}"
            );
            if (!devotionResult.Success)
                return;
            _test.Eq(devotionResult.TargetRank, targetRank, "Misfortune 每次只应提升 1 阶。");

            PendingCharacterReward pendingReward = partyState.GetNextPendingCharacterReward();
            _test.True(pendingReward != null, $"Misfortune rank {targetRank} 成功后应排入 pending reward。");
            if (pendingReward == null)
                return;

            manager.ApplyPendingCharacterReward(pendingReward);
            _test.Eq(
                GetCustomStat(partyState, DoomAuthorityStatId),
                targetRank,
                $"Misfortune rank {targetRank} 结算后应把 doom_authority 写到正确值。"
            );
            if (expectedKnowledgeUnlocks.TryGetValue(targetRank, out StringName placeholderKnowledge))
            {
                _test.True(
                    partyState.GetMemberState(HeroId).progression.HasKnowledge(placeholderKnowledge),
                    $"Misfortune rank {targetRank} 应把技能占位写入 known_knowledge_ids。"
                );
            }
            if (expectedCalamityBonusByRank.TryGetValue(targetRank, out int calamityBonus))
            {
                _test.Eq(
                    GetCustomStat(partyState, CalamityCapacityBonusStatId),
                    calamityBonus,
                    $"Misfortune rank {targetRank} 结算后应累计 calamity 上限占位。"
                );
            }
        }

        FaithDevotionResult capResult = faithService.ExecuteDevotion(
            partyState,
            HeroId,
            MisfortuneDeityId
        );
        _test.False(capResult.Success, "达到 Misfortune rank 5 后不应继续升级。");
        _test.Eq(capResult.ErrorCode, "max_rank_reached", "Misfortune 达到上限后应返回 max_rank_reached。");
        _test.Eq(GetCustomStat(partyState, DoomAuthorityStatId), 5, "达到上限后 doom_authority 不应继续增加。");
        _test.True(partyState.GetNextPendingCharacterReward() == null, "Misfortune 达到上限后不应新增 pending reward。");
        manager.Dispose();
    }

    private void TestFaithRankValidationRejectsUnsupportedRewardEntries()
    {
        var validRank = new FaithRankDef
        {
            rank_index = 1,
            rank_name = "合法阶位",
            reward_entries = new()
            {
                new GDictionary
                {
                    ["entry_type"] = "attribute_delta",
                    ["target_id"] = FaithLuckBonusStatId,
                    ["amount"] = 1,
                },
            },
        };
        _test.True(validRank.Validate().Count == 0, "faith custom stat 的 attribute_delta 奖励应保持合法。");

        var invalidRank = new FaithRankDef
        {
            rank_index = 1,
            rank_name = "非法阶位",
            reward_entries = new()
            {
                new GDictionary
                {
                    ["entry_type"] = "skill_level",
                    ["target_id"] = "charge",
                    ["amount"] = 1,
                },
            },
        };
        _test.True(invalidRank.Validate().Count > 0, "FaithRankDef.validate 应拒绝 unsupported reward entry_type。");
    }

    private static PartyState BuildPartyState()
    {
        var partyState = new PartyState
        {
            leader_member_id = HeroId,
            main_character_member_id = HeroId,
            active_member_ids = new GStringNameArray { HeroId },
        };
        partyState.SetGold(50000);
        partyState.SetMemberState(BuildPartyMemberState());
        return partyState;
    }

    private static PartyMemberState BuildPartyMemberState()
    {
        var memberState = new PartyMemberState
        {
            member_id = HeroId,
            display_name = "Hero",
        };
        memberState.progression.unit_id = HeroId;
        memberState.progression.display_name = "Hero";
        memberState.progression.character_level = 30;
        var levelAnchor = new UnitProfessionProgress
        {
            profession_id = "faith_test_level_anchor",
            rank = 30,
        };
        memberState.progression.SetProfessionProgress(levelAnchor);
        memberState.progression.unit_base_attributes.custom_stats[FortuneMarkedStatId] = 1;
        memberState.progression.unit_base_attributes.custom_stats[FaithLuckBonusStatId] = 0;
        memberState.progression.unit_base_attributes.custom_stats[DoomMarkedStatId] = 1;
        memberState.progression.unit_base_attributes.custom_stats[DoomAuthorityStatId] = 0;
        memberState.progression.unit_base_attributes.custom_stats[CalamityCapacityBonusStatId] = 0;

        foreach (
            StringName achievementId in new StringName[]
            {
                "fortuna_guidance_true",
                "fortuna_guidance_devout",
                "fortuna_guidance_exalted",
                "fortuna_guidance_blessed",
                "misfortune_guidance_true",
                "misfortune_guidance_devout",
                "misfortune_guidance_exalted",
                "misfortune_guidance_blessed",
            }
        )
        {
            var progressState = new AchievementProgressState
            {
                achievement_id = achievementId,
                current_value = 1,
                is_unlocked = true,
            };
            memberState.progression.SetAchievementProgressState(progressState);
        }

        return memberState;
    }

    private static bool HasRewardEntry(
        FaithRankDef rankDef,
        StringName entryType,
        StringName targetId,
        int amount
    )
    {
        return rankDef != null
            && rankDef
                .GetRewardEntrySpecs()
                .Any(entry =>
                    entry.EntryType == entryType
                    && entry.TargetId == targetId
                    && entry.Amount == amount
                );
    }

    private static int GetCustomStat(PartyState partyState, StringName statId)
    {
        PartyMemberState memberState = partyState?.GetMemberState(HeroId);
        return memberState?.progression?.unit_base_attributes?.GetAttributeValue(statId) ?? 0;
    }

}
