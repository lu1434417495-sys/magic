using System;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class BattleRatingSystem : RefCounted
{
    private static readonly StringName Empty = "";
    private static readonly StringName ManualControlMode = "manual";
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

    public void setup(GodotObject runtime, BattleSkillMasteryService mastery_service = null)
    {
        _runtime = runtime as BattleRuntimeModule;
        _mastery_service = mastery_service ?? new BattleSkillMasteryService();
    }

    public void dispose()
    {
        _runtime = null;
        _mastery_service = null;
        _contributionLedger.Clear();
    }

    public void initialize_battle_rating_stats()
    {
        if (!_has_runtime())
        {
            return;
        }

        GetBattleRatingStats().Clear();
        GetPendingPostBattleCharacterRewards().Clear();
        _contributionLedger.Clear();
        GodotObject state = GetState();
        if (state == null)
        {
            return;
        }

        GArray allyUnitIds = GdInterop.GetArray(state, "ally_unit_ids");
        GDictionary units = GdInterop.GetDictionary(state, "units");
        foreach (var allyUnitIdValue in allyUnitIds)
        {
            BattleUnitState unitState =
                GdInterop.GetObject(units, allyUnitIdValue) as BattleUnitState;
            if (unitState == null)
            {
                continue;
            }
            if (unitState.control_mode != ManualControlMode)
            {
                continue;
            }
            StringName sourceMemberId = unitState.source_member_id;
            if (GdInterop.IsEmpty(sourceMemberId))
            {
                continue;
            }

            string memberName = unitState.display_name;
            GetBattleRatingStats()[sourceMemberId] = new GDictionary
            {
                ["member_id"] = sourceMemberId,
                ["member_name"] = string.IsNullOrEmpty(memberName)
                    ? sourceMemberId.ToString()
                    : memberName,
                ["cast_counts"] = new GDictionary(),
                ["successful_skill_count"] = 0,
                ["hostile_damage_done"] = 0,
                ["ally_healing_done"] = 0,
                ["enemy_kill_count"] = 0,
                ["friendly_fire_damage"] = 0,
                ["ally_defeat_count"] = 0,
                ["enemy_healing_done"] = 0,
                ["total_damage_done"] = 0,
                ["total_healing_done"] = 0,
                ["kill_count"] = 0,
            };
        }
    }

    public void record_skill_success(BattleUnitState active_unit, StringName skill_id)
    {
        if (!_has_runtime())
        {
            return;
        }
        GDictionary stats = _get_battle_rating_stats(active_unit);
        if (stats.Count == 0 || GdInterop.IsEmpty(skill_id))
        {
            return;
        }

        GDictionary castCounts = GdInterop.GetDictionary(stats, "cast_counts");
        StringName masterySkillId =
            _mastery_service != null
                ? _mastery_service.ResolveMasteryRewardSkillId(active_unit, skill_id)
                : skill_id;
        castCounts[masterySkillId] = GdInterop.GetInt(castCounts, masterySkillId, 0) + 1;
        stats["cast_counts"] = castCounts;
        stats["successful_skill_count"] = GdInterop.GetInt(stats, "successful_skill_count", 0) + 1;
        GetBattleRatingStats()[active_unit.source_member_id] = stats;
    }

    public void record_skill_effect_result(BattleUnitState source_unit, int damage, int healing, int kill_count)
    {
        if (!_has_runtime() || source_unit == null || GdInterop.IsEmpty(source_unit.source_member_id))
        {
            return;
        }
        GDictionary stats = _get_battle_rating_stats(source_unit);
        if (stats.Count == 0)
        {
            return;
        }
        stats["total_damage_done"] = GdInterop.GetInt(stats, "total_damage_done", 0) + Math.Max(damage, 0);
        stats["total_healing_done"] = GdInterop.GetInt(stats, "total_healing_done", 0) + Math.Max(healing, 0);
        stats["kill_count"] = GdInterop.GetInt(stats, "kill_count", 0) + Math.Max(kill_count, 0);
        GetBattleRatingStats()[source_unit.source_member_id] = stats;
    }

    public void record_contribution_from_units(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        int damage,
        int healing,
        bool caused_defeat,
        StringName origin_kind,
        StringName skill_id
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
                caused_defeat,
                origin_kind,
                skill_id
            )
        );
    }

    public void record_contribution_event_from_dictionary(GDictionary payload)
    {
        if (!_has_runtime() || payload == null || payload.Count == 0)
        {
            return;
        }
        RecordContributionEvent(BattleContributionEventBuilder.FromDictionary(payload));
    }

    private void RecordContributionEvent(BattleContributionEvent contributionEvent)
    {
        if (
            !_has_runtime()
            || contributionEvent == null
            || GdInterop.IsEmpty(contributionEvent.source_member_id)
        )
        {
            return;
        }
        GDictionary stats = _get_battle_rating_stats_by_member_id(
            contributionEvent.source_member_id
        );
        if (stats.Count == 0)
        {
            return;
        }

        _contributionLedger.Add(contributionEvent);
        ApplyContributionToStats(stats, contributionEvent);
        GetBattleRatingStats()[contributionEvent.source_member_id] = stats;
    }

    public GArray get_contribution_events()
    {
        return _contributionLedger.ToGodotArray();
    }

    public void record_enemy_defeated_achievement(
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
        if (GdInterop.IsEmpty(sourceMemberId))
        {
            return;
        }
        if (target_unit.faction_id == source_unit.faction_id)
        {
            return;
        }

        characterGateway.record_achievement_event(sourceMemberId, EnemyDefeatedAchievement, 1);
    }

    public void record_battle_won_achievements()
    {
        if (!_has_runtime())
        {
            return;
        }
        IBattleRatingCharacterGateway characterGateway = GetCharacterGateway();
        GodotObject state = GetState();
        if (
            state == null
            || GdInterop.GetStringName(state, "winner_faction_id") != PlayerFaction
            || characterGateway == null
        )
        {
            return;
        }

        GArray allyUnitIds = GdInterop.GetArray(state, "ally_unit_ids");
        GDictionary units = GdInterop.GetDictionary(state, "units");
        foreach (var allyUnitIdValue in allyUnitIds)
        {
            BattleUnitState unitState =
                GdInterop.GetObject(units, allyUnitIdValue) as BattleUnitState;
            if (unitState == null)
            {
                continue;
            }
            StringName sourceMemberId = unitState.source_member_id;
            if (GdInterop.IsEmpty(sourceMemberId))
            {
                continue;
            }
            characterGateway.record_achievement_event(sourceMemberId, BattleWonAchievement, 1);
        }
    }

    public void finalize_battle_rating_rewards()
    {
        if (!_has_runtime())
        {
            return;
        }
        GArray pendingRewards = GetPendingPostBattleCharacterRewards();
        pendingRewards.Clear();
        GodotObject state = GetState();
        IBattleRatingCharacterGateway characterGateway = GetCharacterGateway();
        if (state == null || characterGateway == null)
        {
            return;
        }

        bool playerVictory = GdInterop.GetStringName(state, "winner_faction_id") == PlayerFaction;
        foreach (var statsValue in GetBattleRatingStats().Values)
        {
            if (statsValue.VariantType != Variant.Type.Dictionary)
            {
                continue;
            }
            GDictionary stats = statsValue.AsGodotDictionary();
            int score = calculate_battle_rating_score(stats, playerVictory);

            StringName memberId = ToStringNameLoose(stats.GetValueOrDefault("member_id"));
            if (GdInterop.IsEmpty(memberId))
            {
                continue;
            }
            string memberName = GdInterop.GetString(stats, "member_name", memberId.ToString());
            string ratingLabel = resolve_battle_rating_label(score);
            GArray rewardEntries =
                _mastery_service != null
                    ? _mastery_service.BuildBattleRatingMasteryRewardEntries(
                        stats,
                        score,
                        ratingLabel
                    )
                    : new GArray();
            if (rewardEntries.Count == 0)
            {
                continue;
            }

            PendingCharacterReward reward = characterGateway.build_pending_skill_mastery_reward(
                memberId,
                BattleRatingSourceType,
                "战斗结算",
                rewardEntries,
                $"在战斗中，{memberName}{_resolve_battle_rating_summary_suffix(score)}。评分 {score}。"
            );
            if (reward != null && !reward.is_empty())
            {
                pendingRewards.Add(reward);
            }
        }
    }

    public int calculate_battle_rating_score(GDictionary stats, bool player_victory)
    {
        int successfulSkillCount = GdInterop.GetInt(stats, "successful_skill_count", 0);
        int hostileDamageDone = GdInterop.GetInt(stats, "hostile_damage_done", 0);
        int allyHealingDone = GdInterop.GetInt(stats, "ally_healing_done", 0);
        int enemyKillCount = GdInterop.GetInt(stats, "enemy_kill_count", 0);
        StringName memberId = ToStringNameLoose(stats.GetValueOrDefault("member_id"));
        bool survived = false;
        if (_has_runtime() && GetState() != null && !GdInterop.IsEmpty(memberId))
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

    public int ResolveBattleRatingMasteryAmount(int score)
    {
        _mastery_service ??= new BattleSkillMasteryService();
        return _mastery_service.ResolveBattleRatingMasteryAmount(score);
    }

    public string resolve_battle_rating_label(int score)
    {
        return _resolve_battle_rating_summary_suffix(score);
    }

    public GDictionary _get_battle_rating_stats(BattleUnitState active_unit)
    {
        if (!_has_runtime())
        {
            return new GDictionary();
        }
        if (active_unit == null || GdInterop.IsEmpty(active_unit.source_member_id))
        {
            return new GDictionary();
        }

        var statsValue = GetBattleRatingStats().GetValueOrDefault(active_unit.source_member_id);
        return statsValue.VariantType == Variant.Type.Dictionary
            ? statsValue.AsGodotDictionary().Duplicate(true)
            : new GDictionary();
    }

    private GDictionary _get_battle_rating_stats_by_member_id(StringName member_id)
    {
        if (!_has_runtime() || GdInterop.IsEmpty(member_id))
        {
            return new GDictionary();
        }

        var statsValue = GetBattleRatingStats().GetValueOrDefault(member_id);
        return statsValue.VariantType == Variant.Type.Dictionary
            ? statsValue.AsGodotDictionary().Duplicate(true)
            : new GDictionary();
    }

    public BattleUnitState _find_unit_by_member_id(StringName member_id)
    {
        if (!_has_runtime() || GetState() == null)
        {
            return null;
        }
        foreach (var unitStateValue in GdInterop.GetDictionary(GetState(), "units").Values)
        {
            BattleUnitState unitState =
                unitStateValue.VariantType == Variant.Type.Nil
                    ? null
                    : unitStateValue.AsGodotObject() as BattleUnitState;
            if (unitState != null && unitState.source_member_id == member_id)
            {
                return unitState;
            }
        }
        return null;
    }

    public string _resolve_battle_rating_summary_suffix(int score)
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

    public bool _has_runtime()
    {
        return _runtime != null;
    }

    private GDictionary GetBattleRatingStats()
    {
        BattleRuntimeModule runtime = _runtime;
        if (runtime == null)
        {
            return new GDictionary();
        }
        return runtime.get_battle_rating_stats() ?? new GDictionary();
    }

    private GArray GetPendingPostBattleCharacterRewards()
    {
        BattleRuntimeModule runtime = _runtime;
        if (runtime == null)
        {
            return new GArray();
        }
        return runtime.get_pending_post_battle_character_rewards() ?? new GArray();
    }

    private BattleState GetState()
    {
        BattleRuntimeModule runtime = _runtime;
        if (runtime == null)
        {
            return null;
        }
        return runtime.get_state();
    }

    private IBattleRatingCharacterGateway GetCharacterGateway()
    {
        if (_runtime is not BattleRuntimeModule runtime)
        {
            return null;
        }
        GodotObject gateway = runtime.get_character_gateway();
        if (gateway == null)
        {
            return null;
        }
        if (gateway is IBattleRatingCharacterGateway typedGateway)
        {
            return typedGateway;
        }
        GameLog.Error(
            $"BattleRatingSystem requires character gateway to implement {nameof(IBattleRatingCharacterGateway)}; got {gateway.GetType().Name}.",
            "battle.rating.invalid_gateway",
            "battle"
        );
        return null;
    }

    private static void ApplyContributionToStats(
        GDictionary stats,
        BattleContributionEvent contributionEvent
    )
    {
        bool isAllyOrSelf =
            contributionEvent.relation == BattleContributionRelation.Ally
            || contributionEvent.relation == BattleContributionRelation.Self;
        if (contributionEvent.relation == BattleContributionRelation.Enemy)
        {
            if (contributionEvent.hp_damage_applied > 0)
            {
                Increment(stats, "hostile_damage_done", contributionEvent.hp_damage_applied);
                stats["total_damage_done"] = GdInterop.GetInt(stats, "hostile_damage_done", 0);
            }
            if (contributionEvent.hp_healing_applied > 0)
            {
                Increment(stats, "enemy_healing_done", contributionEvent.hp_healing_applied);
            }
            if (contributionEvent.caused_defeat)
            {
                Increment(stats, "enemy_kill_count", 1);
                stats["kill_count"] = GdInterop.GetInt(stats, "enemy_kill_count", 0);
            }
            return;
        }

        if (isAllyOrSelf)
        {
            if (contributionEvent.hp_damage_applied > 0)
            {
                Increment(stats, "friendly_fire_damage", contributionEvent.hp_damage_applied);
            }
            if (contributionEvent.hp_healing_applied > 0)
            {
                Increment(stats, "ally_healing_done", contributionEvent.hp_healing_applied);
                stats["total_healing_done"] = GdInterop.GetInt(stats, "ally_healing_done", 0);
            }
            if (contributionEvent.caused_defeat)
            {
                Increment(stats, "ally_defeat_count", 1);
            }
        }
    }

    private static void Increment(GDictionary stats, string key, int amount)
    {
        if (amount <= 0)
        {
            return;
        }
        stats[key] = GdInterop.GetInt(stats, key, 0) + amount;
    }

    private static StringName ToStringNameLoose(object rawValue)
    {
        if (rawValue is string textValue)
        {
            string normalizedText = textValue.Trim();
            return string.IsNullOrEmpty(normalizedText) ? Empty : new StringName(normalizedText);
        }
        if (rawValue is StringName stringName)
        {
            return stringName;
        }
        if (rawValue is not Variant value)
        {
            return Empty;
        }
        if (value.VariantType == Variant.Type.Nil)
        {
            return Empty;
        }
        string text = value.ToString();
        if (text == "<null>")
        {
            return Empty;
        }
        string trimmed = text.Trim();
        return string.IsNullOrEmpty(trimmed) ? Empty : new StringName(trimmed);
    }

    private static T ResolveWeakRef<T>(WeakReference<T> weakRef)
        where T : GodotObject
    {
        if (
            weakRef == null
            || !weakRef.TryGetTarget(out T target)
            || !GodotObject.IsInstanceValid(target)
        )
        {
            return null;
        }
        return target;
    }
}
