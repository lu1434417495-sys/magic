using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

internal partial class BattleRatingSystem : RefCounted
{
    private static readonly StringName Empty = "";
    private static readonly StringName PlayerFaction = "player";
    private static readonly StringName EnemyDefeatedAchievement = "enemy_defeated";
    private static readonly StringName BattleWonAchievement = "battle_won";
    private static readonly StringName BattleRatingSourceType = "battle_rating";

    private WeakReference<BattleRuntimeModule> _runtimeRef;
    private BattleSkillMasteryService _mastery_service;
    private readonly BattleContributionLedger _contributionLedger = new();

    private BattleRuntimeModule _runtime
    {
        get => ResolveWeakRef(_runtimeRef);
        set => _runtimeRef = value != null ? new WeakReference<BattleRuntimeModule>(value) : null;
    }

    internal void Setup(BattleRuntimeModule runtime, BattleSkillMasteryService masteryService = null)
    {
        _runtime = runtime;
        _mastery_service = masteryService ?? new BattleSkillMasteryService();
    }

    internal void DisposeRuntime()
    {
        _runtime = null;
        _mastery_service = null;
        _contributionLedger.Clear();
    }

    internal void InitializeBattleRatingStats()
    {
        if (!_has_runtime())
        {
            return;
        }

        GetBattleRatingStatsTyped().Clear();
        GetPendingPostBattleCharacterRewards().Clear();
        _contributionLedger.Clear();
        BattleState state = GetState();
        if (state == null)
        {
            return;
        }

        foreach (StringName allyUnitId in state.GetAllyUnitIdsTyped())
        {
            if (!state.TryGetUnitTyped(allyUnitId, out BattleUnitState unitState))
            {
                continue;
            }
            if (unitState.ControlModeKind != BattleUnitControlMode.Manual)
            {
                continue;
            }
            StringName sourceMemberId = unitState.source_member_id;
            if (IsEmpty(sourceMemberId))
            {
                continue;
            }

            BattleRatingMemberStats stats = BattleRatingMemberStats.FromUnit(unitState);
            if (stats != null)
            {
                GetBattleRatingStatsTyped()[sourceMemberId] = stats;
            }
        }
    }

    internal void RecordSkillSuccess(BattleUnitState active_unit, StringName skill_id)
    {
        if (!_has_runtime())
        {
            return;
        }
        BattleRatingMemberStats stats = GetBattleRatingStatsForUnit(active_unit);
        if (stats == null || IsEmpty(skill_id))
        {
            return;
        }

        StringName masterySkillId =
            _mastery_service != null
                ? _mastery_service.ResolveMasteryRewardSkillId(active_unit, skill_id)
                : skill_id;
        stats.cast_counts.TryGetValue(masterySkillId, out int castCount);
        stats.cast_counts[masterySkillId] = castCount + 1;
        stats.successful_skill_count += 1;
    }

    internal void RecordSkillEffectResult(BattleUnitState source_unit, int damage, int healing, int kill_count)
    {
        if (!_has_runtime() || source_unit == null || IsEmpty(source_unit.source_member_id))
        {
            return;
        }
        BattleRatingMemberStats stats = GetBattleRatingStatsForUnit(source_unit);
        if (stats == null)
        {
            return;
        }
        stats.total_damage_done += Math.Max(damage, 0);
        stats.total_healing_done += Math.Max(healing, 0);
        stats.kill_count += Math.Max(kill_count, 0);
    }

    internal void RecordContributionFromUnits(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        int damage,
        int healing,
        bool causedDefeat,
        StringName originKind,
        StringName skillId
    )
    {
        if (!_has_runtime() || source_unit == null || target_unit == null)
        {
            return;
        }
        RecordContributionEvent(
            BattleContributionEventBuilder.FromUnits(
                source_unit,
                target_unit,
                damage,
                healing,
                causedDefeat,
                originKind,
                skillId
            )
        );
    }

    private void RecordContributionEvent(BattleContributionEvent contributionEvent)
    {
        if (
            !_has_runtime()
            || contributionEvent == null
            || IsEmpty(contributionEvent.SourceMemberId)
        )
        {
            return;
        }
        BattleRatingMemberStats stats = GetBattleRatingStatsForMember(
            contributionEvent.SourceMemberId
        );
        if (stats == null)
        {
            return;
        }

        _contributionLedger.Add(contributionEvent);
        ApplyContributionToStats(stats, contributionEvent);
    }

    internal void RecordEnemyDefeatedAchievement(
        BattleUnitState source_unit,
        BattleUnitState target_unit
    )
    {
        if (!_has_runtime())
        {
            return;
        }
        IBattleRatingCharacterGateway characterGateway = GetCharacterGateway();
        if (source_unit == null || target_unit == null || characterGateway == null)
        {
            return;
        }
        StringName sourceMemberId = source_unit.source_member_id;
        if (IsEmpty(sourceMemberId))
        {
            return;
        }
        if (target_unit.faction_id == source_unit.faction_id)
        {
            return;
        }

        characterGateway.RecordAchievementEvent(sourceMemberId, EnemyDefeatedAchievement, 1);
    }

    internal void RecordBattleWonAchievements()
    {
        if (!_has_runtime())
        {
            return;
        }
        IBattleRatingCharacterGateway characterGateway = GetCharacterGateway();
        BattleState state = GetState();
        if (
            state == null
            || state.winner_faction_id != PlayerFaction
            || characterGateway == null
        )
        {
            return;
        }

        foreach (StringName allyUnitId in state.GetAllyUnitIdsTyped())
        {
            if (!state.TryGetUnitTyped(allyUnitId, out BattleUnitState unitState))
            {
                continue;
            }
            StringName sourceMemberId = unitState.source_member_id;
            if (IsEmpty(sourceMemberId))
            {
                continue;
            }
            characterGateway.RecordAchievementEvent(sourceMemberId, BattleWonAchievement, 1);
        }
    }

    internal void FinalizeBattleRatingRewards()
    {
        if (!_has_runtime())
        {
            return;
        }
        List<PendingCharacterReward> pendingRewards = GetPendingPostBattleCharacterRewards();
        pendingRewards.Clear();
        BattleState state = GetState();
        IBattleRatingCharacterGateway characterGateway = GetCharacterGateway();
        if (state == null || characterGateway == null)
        {
            return;
        }

        bool playerVictory = state.winner_faction_id == PlayerFaction;
        foreach (BattleRatingMemberStats stats in GetBattleRatingStatsTyped().Values)
        {
            if (stats == null)
            {
                continue;
            }
            int score = CalculateBattleRatingScore(stats, playerVictory);

            StringName memberId = stats.member_id;
            if (IsEmpty(memberId))
            {
                continue;
            }
            string memberName = string.IsNullOrEmpty(stats.member_name)
                ? memberId.ToString()
                : stats.member_name;
            string ratingLabel = resolve_battle_rating_label(score);
            List<PendingCharacterRewardEntry> rewardEntries =
                _mastery_service != null
                    ? _mastery_service.BuildBattleRatingMasteryRewardEntries(
                        stats,
                        score,
                        ratingLabel
                    )
                    : new List<PendingCharacterRewardEntry>();
            if (rewardEntries.Count == 0)
            {
                continue;
            }

            PendingCharacterReward reward = characterGateway.BuildPendingSkillMasteryReward(
                memberId,
                BattleRatingSourceType,
                "战斗结算",
                rewardEntries,
                $"在战斗中，{memberName}{_resolve_battle_rating_summary_suffix(score)}。评分 {score}。"
            );
            if (reward != null && !reward.IsEmpty())
            {
                pendingRewards.Add(reward);
            }
        }
    }

    private int CalculateBattleRatingScore(BattleRatingMemberStats stats, bool player_victory)
    {
        if (stats == null)
        {
            return 0;
        }
        int successfulSkillCount = stats.successful_skill_count;
        int hostileDamageDone = stats.hostile_damage_done;
        int allyHealingDone = stats.ally_healing_done;
        int enemyKillCount = stats.enemy_kill_count;
        StringName memberId = stats.member_id;
        bool survived = false;
        if (_has_runtime() && GetState() != null && !IsEmpty(memberId))
        {
            BattleUnitState unitState = _find_unit_by_member_id(memberId);
            survived = unitState != null && unitState.is_alive;
        }

        int score = 0;
        if (successfulSkillCount > 0)
        {
            score += 1;
        }
        score += Math.Min(successfulSkillCount, 3);
        if (hostileDamageDone > 0 || allyHealingDone > 0)
        {
            score += 1;
        }
        if (enemyKillCount > 0)
        {
            score += 1;
        }
        if (player_victory)
        {
            score += 1;
        }
        if (survived)
        {
            score += 1;
        }
        return score;
    }

    internal int ResolveBattleRatingMasteryAmount(int score)
    {
        _mastery_service ??= new BattleSkillMasteryService();
        return _mastery_service.ResolveBattleRatingMasteryAmount(score);
    }

    internal string resolve_battle_rating_label(int score)
    {
        return _resolve_battle_rating_summary_suffix(score);
    }

    internal GDictionary _get_battle_rating_stats(BattleUnitState active_unit)
    {
        if (!_has_runtime())
        {
            return new GDictionary();
        }
        if (active_unit == null || IsEmpty(active_unit.source_member_id))
        {
            return new GDictionary();
        }

        BattleRatingMemberStats stats = GetBattleRatingStatsForMember(active_unit.source_member_id);
        return stats?.ToDictionary() ?? new GDictionary();
    }

    private BattleRatingMemberStats GetBattleRatingStatsForUnit(BattleUnitState unitState)
    {
        if (unitState == null || IsEmpty(unitState.source_member_id))
        {
            return null;
        }
        return GetBattleRatingStatsForMember(unitState.source_member_id);
    }

    private BattleRatingMemberStats GetBattleRatingStatsForMember(StringName memberId)
    {
        if (!_has_runtime() || IsEmpty(memberId))
        {
            return null;
        }
        return GetBattleRatingStatsTyped().TryGetValue(memberId, out BattleRatingMemberStats stats)
            ? stats
            : null;
    }

    internal BattleUnitState _find_unit_by_member_id(StringName member_id)
    {
        if (!_has_runtime() || GetState() == null)
        {
            return null;
        }
        foreach (BattleUnitState unitState in GetState().GetUnitsTyped())
        {
            if (unitState != null && unitState.source_member_id == member_id)
            {
                return unitState;
            }
        }
        return null;
    }

    internal string _resolve_battle_rating_summary_suffix(int score)
    {
        if (score >= 6)
        {
            return "若有所悟";
        }
        if (score >= 4)
        {
            return "渐入佳境";
        }
        if (score >= 2)
        {
            return "有所体会";
        }
        return "尚需磨炼";
    }

    internal bool _has_runtime()
    {
        return _runtime != null;
    }

    private Dictionary<StringName, BattleRatingMemberStats> GetBattleRatingStatsTyped()
    {
        BattleRuntimeModule runtime = _runtime;
        if (runtime == null)
        {
            return new Dictionary<StringName, BattleRatingMemberStats>();
        }
        return runtime.GetBattleRatingStatsTyped();
    }

    private List<PendingCharacterReward> GetPendingPostBattleCharacterRewards()
    {
        BattleRuntimeModule runtime = _runtime;
        if (runtime == null)
        {
            return new List<PendingCharacterReward>();
        }
        return runtime.GetPendingPostBattleCharacterRewards()
            ?? new List<PendingCharacterReward>();
    }

    private BattleState GetState()
    {
        BattleRuntimeModule runtime = _runtime;
        if (runtime == null)
        {
            return null;
        }
        return runtime.GetState();
    }

    private IBattleRatingCharacterGateway GetCharacterGateway()
    {
        if (_runtime is not BattleRuntimeModule runtime)
        {
            return null;
        }
        IBattleRatingCharacterGateway typedGateway = runtime.GetCharacterGatewayTyped();
        if (typedGateway != null)
        {
            return typedGateway;
        }
        return null;
    }

    private static void ApplyContributionToStats(BattleRatingMemberStats stats, BattleContributionEvent contributionEvent)
    {
        bool isAllyOrSelf =
            contributionEvent.Relation == BattleContributionRelation.Ally
            || contributionEvent.Relation == BattleContributionRelation.Self;
        if (contributionEvent.Relation == BattleContributionRelation.Enemy)
        {
            if (contributionEvent.HpDamageApplied > 0)
            {
                stats.hostile_damage_done += contributionEvent.HpDamageApplied;
                stats.total_damage_done = stats.hostile_damage_done;
            }
            if (contributionEvent.HpHealingApplied > 0)
            {
                stats.enemy_healing_done += contributionEvent.HpHealingApplied;
            }
            if (contributionEvent.CausedDefeat)
            {
                stats.enemy_kill_count += 1;
                stats.kill_count = stats.enemy_kill_count;
            }
            return;
        }

        if (isAllyOrSelf)
        {
            if (contributionEvent.HpDamageApplied > 0)
            {
                stats.friendly_fire_damage += contributionEvent.HpDamageApplied;
            }
            if (contributionEvent.HpHealingApplied > 0)
            {
                stats.ally_healing_done += contributionEvent.HpHealingApplied;
                stats.total_healing_done = stats.ally_healing_done;
            }
            if (contributionEvent.CausedDefeat)
            {
                stats.ally_defeat_count += 1;
            }
        }
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || string.IsNullOrEmpty(value.ToString());
    }

    private static T ResolveWeakRef<T>(WeakReference<T> weakRef)
        where T : class
    {
        if (weakRef == null || !weakRef.TryGetTarget(out T target))
        {
            return null;
        }
        return target;
    }
}
