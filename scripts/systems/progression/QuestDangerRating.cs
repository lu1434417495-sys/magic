using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// 任务危险度星级的纯派生结果。星级只用于投影展示，
/// 不写入 PartyState 或存档；作者可用 danger_tier_override 覆盖。
/// </summary>
public sealed class QuestDangerRatingResult
{
    public static readonly StringName SourceOverride = "override";
    public static readonly StringName SourceDerived = "derived";
    public static readonly StringName SourceUnrated = "unrated";

    public bool IsRated { get; }
    public int Stars { get; }
    public StringName Source { get; }
    public IReadOnlyList<StringName> MissingTargetIds { get; }

    private QuestDangerRatingResult(
        bool isRated,
        int stars,
        StringName source,
        IReadOnlyList<StringName> missingTargetIds
    )
    {
        IsRated = isRated;
        Stars = stars;
        Source = source;
        MissingTargetIds = missingTargetIds ?? Array.Empty<StringName>();
    }

    internal static QuestDangerRatingResult Overridden(int stars) =>
        new(true, stars, SourceOverride, Array.Empty<StringName>());

    internal static QuestDangerRatingResult Derived(int stars) =>
        new(true, stars, SourceDerived, Array.Empty<StringName>());

    internal static QuestDangerRatingResult Unrated(
        IReadOnlyList<StringName> missingTargetIds = null
    ) => new(false, 0, SourceUnrated, missingTargetIds);
}

/// <summary>
/// 危险度公式参数与星级阈值的唯一 owner。阈值基于 2026-07 现有悬赏内容离线标定：
/// raider 2.4 / pack 3.2 → 1★，harrier 6.9 / alpha 7.5 → 2★，
/// beast 8 / vanguard 10 / shaman·weaver 10.6 → 3★，
/// dread_alpha 13 / abyss 12.7 → 4★，rift_warden 26.6 → 5★。
/// UI 不得自行硬编码任何权重或阈值。
/// </summary>
internal static class QuestDangerRatingPolicy
{
    internal const int MinOverride = 0;
    internal const int MaxOverride = 5;

    private static readonly double[] StarUpperBounds = { 4.0, 8.0, 12.0, 20.0 };

    internal static double RankWeight(EnemyTargetRankKind rankKind) =>
        rankKind switch
        {
            EnemyTargetRankKind.Elite => 1.5,
            EnemyTargetRankKind.Boss => 2.5,
            _ => 1.0,
        };

    internal static int ToStars(double questThreat)
    {
        for (int index = 0; index < StarUpperBounds.Length; index++)
        {
            if (questThreat < StarUpperBounds[index])
                return index + 1;
        }
        return 5;
    }

    internal static bool IsValidOverride(int value) =>
        value >= MinOverride && value <= MaxOverride;
}

/// <summary>
/// 纯函数 resolver：从 QuestDefinition 与敌人模板推导 1-5 星危险度。
/// V1 只对全部目标均为可解析 defeat_enemy 的任务自动计算；
/// 存在空 target_id、缺失模板或非战斗目标时返回未评级（除非作者 override）。
/// 公式：objective_threat = max(level+1,1) × rank_weight × √target_value × √enemy_count，
/// quest_threat = Σ objective_threat。enemy_count 只以平方根表达同场压力，避免重复计数。
/// </summary>
internal static class QuestDangerRatingResolver
{
    internal static QuestDangerRatingResult Resolve(
        QuestDefinition questDefinition,
        IReadOnlyDictionary<StringName, EnemyTemplateDefinition> enemyTemplates
    )
    {
        if (questDefinition == null)
            return QuestDangerRatingResult.Unrated();

        int overrideTier = questDefinition.DangerTierOverride;
        if (overrideTier > 0 && QuestDangerRatingPolicy.IsValidOverride(overrideTier))
            return QuestDangerRatingResult.Overridden(overrideTier);

        IReadOnlyList<QuestObjectiveDefinition> objectives = questDefinition.Objectives;
        if (objectives == null || objectives.Count == 0)
            return QuestDangerRatingResult.Unrated();

        double questThreat = 0.0;
        var missingTargetIds = new List<StringName>();
        bool derivable = true;
        foreach (QuestObjectiveDefinition objective in objectives)
        {
            if (objective == null || objective.ObjectiveKind != QuestObjectiveKind.DefeatEnemy)
            {
                derivable = false;
                continue;
            }
            StringName targetId = objective.TargetId;
            if (targetId == "")
            {
                derivable = false;
                continue;
            }
            if (
                enemyTemplates == null
                || !enemyTemplates.TryGetValue(targetId, out EnemyTemplateDefinition template)
                || template == null
            )
            {
                derivable = false;
                missingTargetIds.Add(targetId);
                continue;
            }
            questThreat += ObjectiveThreat(objective, template);
        }

        if (!derivable)
            return QuestDangerRatingResult.Unrated(missingTargetIds);
        return QuestDangerRatingResult.Derived(QuestDangerRatingPolicy.ToStars(questThreat));
    }

    private static double ObjectiveThreat(
        QuestObjectiveDefinition objective,
        EnemyTemplateDefinition template
    )
    {
        double levelTerm = Math.Max(template.CreatureLevel + 1, 1);
        double rankWeight = QuestDangerRatingPolicy.RankWeight(template.TargetRankKind);
        double targetTerm = Math.Sqrt(Math.Max(objective.TargetValue, 1));
        double pressureTerm = Math.Sqrt(Math.Max(template.EnemyCount, 1));
        return levelTerm * rankWeight * targetTerm * pressureTerm;
    }

    internal static string BuildStarsLabel(QuestDangerRatingResult result)
    {
        if (result == null || !result.IsRated)
            return "危险度：未评级";
        int stars = Math.Clamp(result.Stars, 1, QuestDangerRatingPolicy.MaxOverride);
        return "危险度：" + new string('★', stars)
            + new string('☆', QuestDangerRatingPolicy.MaxOverride - stars);
    }
}
