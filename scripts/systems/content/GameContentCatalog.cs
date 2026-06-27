using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

/// <summary>
/// 正式内容的组合根读入口。catalog 自己持有 typed 内容快照，由所属 <see cref="GameSession"/>
/// 在刷新内容后调用 <see cref="Rebuild"/> 重建；getter 返回 catalog 自己缓存的只读视图，
/// 而不是每次转发回 GameSession 重建 typed index。
///
/// 两条防御性不变量：
/// 1. typed 字典 getter 返回 <see cref="ReadOnlyDictionary{TKey, TValue}"/> 包装，下游即便
///    downcast 也拿不到内部可变 <see cref="Dictionary{TKey, TValue}"/>，不能改写 catalog 快照。
/// 2. <see cref="ClearSessionBinding"/>（owning root dispose 时调用）会同时清空 typed 快照并
///    自增 revision，使任何仍持有旧 catalog 引用的下游读到的是空内容而非 stale 快照，并可用
///    revision 变化察觉失效。
/// </summary>
public sealed class GameContentCatalog
{
    private WeakReference<GameSession> _sessionRef;
    private long _revision;
    private SkillCatalog _skillCatalog;

    private ProgressionContentRegistry _progressionContentRegistry;
    private ProgressionIdentityCatalogData _progressionIdentityCatalog;
    private IReadOnlyDictionary<StringName, SkillDefinition> _skillDefinitions;
    private IReadOnlyDictionary<StringName, TraitDef> _traitDefs;
    private IReadOnlyDictionary<StringName, ProfessionDef> _professionDefs;
    private IReadOnlyDictionary<StringName, AchievementDef> _achievementDefs;
    private IReadOnlyDictionary<StringName, QuestDef> _questDefs;
    private IReadOnlyDictionary<StringName, ItemDef> _itemDefs;
    private IReadOnlyDictionary<StringName, RecipeDef> _recipeDefs;
    private IReadOnlyDictionary<StringName, EnemyTemplateDef> _enemyTemplates;
    private IReadOnlyDictionary<StringName, EnemyAiBrainDef> _enemyAiBrains;
    private IReadOnlyDictionary<StringName, WildEncounterRosterDef> _wildEncounterRosters;
    private GDictionary _battleSpecialProfileSnapshot;
    private readonly Dictionary<string, object> _battleSpecialProfileRuntimeSnapshot = new(
        StringComparer.Ordinal
    );
    private IBattleSpecialProfileView _battleSpecialProfileView;

    public GameContentCatalog()
    {
        ResetSnapshot();
    }

    internal void BindSession(GameSession session)
    {
        _sessionRef = session != null ? new WeakReference<GameSession>(session) : null;
    }

    /// <summary>
    /// 由 owning <see cref="GameRoot"/> 在 dispose 时调用：解除 session 绑定，清空 typed 快照，
    /// 并自增 revision。此后任何仍持有该 catalog 引用的下游读到的是空内容而不是 stale 快照。
    /// </summary>
    internal void ClearSessionBinding()
    {
        _sessionRef = null;
        ResetSnapshot();
        _revision++;
    }

    /// <summary>
    /// 从当前 owning session 的正式内容缓存重建 catalog 自己的 typed 快照。
    /// 由 <see cref="GameSession"/> 在刷新 progression / item / recipe / enemy /
    /// battle special profile 内容后显式调用。
    /// </summary>
    internal void Rebuild(GameSession session)
    {
        BindSession(session);
        if (session == null)
        {
            ResetSnapshot();
            _revision++;
            return;
        }

        _progressionContentRegistry = session.GetProgressionContentRegistry();
        _progressionIdentityCatalog =
            session.GetProgressionIdentityCatalogTyped() ?? new ProgressionIdentityCatalogData();
        _skillDefinitions = SnapshotTyped(session.GetSkillDefinitionsTyped());
        _traitDefs = SnapshotTyped(session.GetTraitDefsTyped());
        _professionDefs = SnapshotTyped(session.GetProfessionDefsTyped());
        _achievementDefs = SnapshotTyped(session.GetAchievementDefsTyped());
        _questDefs = SnapshotTyped(session.GetQuestDefsTyped());
        _itemDefs = SnapshotTyped(session.GetItemDefsTyped());
        _recipeDefs = SnapshotTyped(session.GetRecipeDefsTyped());
        _enemyTemplates = SnapshotTyped(session.GetEnemyTemplatesTyped());
        _enemyAiBrains = SnapshotTyped(session.GetEnemyAiBrainsTyped());
        _wildEncounterRosters = SnapshotTyped(session.GetWildEncounterRostersTyped());
        _battleSpecialProfileSnapshot = RegisterBattleSpecialProfileSnapshot(
            session.GetBattleSpecialProfileRegistrySnapshot() ?? new GDictionary(),
            "GameContentCatalog.Rebuild"
        );
        ReplaceBattleSpecialProfileRuntimeSnapshot(
            session.GetBattleSpecialProfileRegistryRuntimeSnapshot() ?? new GDictionary(),
            "GameContentCatalog.Rebuild.runtime"
        );
        _battleSpecialProfileView =
            session.GetBattleSpecialProfileRuntimeView() ?? BattleSpecialProfileRuntimeView.Empty;
        _revision++;
    }

    private void ResetSnapshot()
    {
        _progressionContentRegistry = null;
        _progressionIdentityCatalog = new ProgressionIdentityCatalogData();
        _skillDefinitions = EmptyTyped<SkillDefinition>();
        _traitDefs = EmptyTyped<TraitDef>();
        _professionDefs = EmptyTyped<ProfessionDef>();
        _achievementDefs = EmptyTyped<AchievementDef>();
        _questDefs = EmptyTyped<QuestDef>();
        _itemDefs = EmptyTyped<ItemDef>();
        _recipeDefs = EmptyTyped<RecipeDef>();
        _enemyTemplates = EmptyTyped<EnemyTemplateDef>();
        _enemyAiBrains = EmptyTyped<EnemyAiBrainDef>();
        _wildEncounterRosters = EmptyTyped<WildEncounterRosterDef>();
        _battleSpecialProfileSnapshot = RegisterBattleSpecialProfileSnapshot(
            new GDictionary(),
            "GameContentCatalog.ResetSnapshot"
        );
        ReplaceBattleSpecialProfileRuntimeSnapshot(
            new GDictionary(),
            "GameContentCatalog.ResetSnapshot.runtime"
        );
        _battleSpecialProfileView = BattleSpecialProfileRuntimeView.Empty;
    }

    /// <summary>catalog 快照版本号；每次 <see cref="Rebuild"/> 或 <see cref="ClearSessionBinding"/>
    /// 自增，供下游做有效性 / 版本校验。</summary>
    public long GetRevision() => _revision;

    public bool HasSessionTyped() => TryGetSession(out _);

    public GameSession GetSessionTyped()
    {
        return TryGetSession(out GameSession session) ? session : null;
    }

    public bool IsBoundToSession(GameSession session)
    {
        return session != null
            && TryGetSession(out GameSession bound)
            && ReferenceEquals(bound, session);
    }

    public ProgressionContentRegistry GetProgressionContentRegistryTyped() =>
        _progressionContentRegistry;

    public ProgressionIdentityCatalogData GetProgressionIdentityCatalogTyped() =>
        _progressionIdentityCatalog;

    public IReadOnlyDictionary<StringName, SkillDefinition> GetSkillDefinitionsTyped() =>
        _skillDefinitions;

    public IReadOnlyDictionary<StringName, TraitDef> GetTraitDefsTyped() => _traitDefs;

    /// <summary>
    /// 技能内容门面。门面只持有本 catalog 引用、每次查询都读当前 typed 快照与 revision，
    /// derived effective profile 由门面按 revision 缓存，因此随
    /// <see cref="Rebuild"/> / <see cref="ClearSessionBinding"/> 自动失效，无需每次重建；
    /// 跨调用返回同一实例。
    /// </summary>
    public ISkillCatalog GetSkillCatalogTyped() => _skillCatalog ??= new SkillCatalog(this);

    public IReadOnlyDictionary<StringName, ProfessionDef> GetProfessionDefsTyped() =>
        _professionDefs;

    public IReadOnlyDictionary<StringName, AchievementDef> GetAchievementDefsTyped() =>
        _achievementDefs;

    public IReadOnlyDictionary<StringName, QuestDef> GetQuestDefsTyped() => _questDefs;

    public QuestDef GetQuestDefTyped(StringName questId)
    {
        if (questId == "")
            return null;
        return _questDefs.TryGetValue(questId, out QuestDef questDef) ? questDef : null;
    }

    public IReadOnlyDictionary<StringName, ItemDef> GetItemDefsTyped() => _itemDefs;

    public IReadOnlyDictionary<StringName, RecipeDef> GetRecipeDefsTyped() => _recipeDefs;

    public IReadOnlyDictionary<StringName, EnemyTemplateDef> GetEnemyTemplatesTyped() =>
        _enemyTemplates;

    public IReadOnlyDictionary<StringName, EnemyAiBrainDef> GetEnemyAiBrainsTyped() =>
        _enemyAiBrains;

    public IReadOnlyDictionary<StringName, WildEncounterRosterDef> GetWildEncounterRostersTyped() =>
        _wildEncounterRosters;

    public GDictionary GetBattleSpecialProfileRegistrySnapshot() =>
        RegisterBattleSpecialProfileSnapshot(
            _battleSpecialProfileSnapshot.Duplicate(true),
            "GameContentCatalog.GetBattleSpecialProfileRegistrySnapshot"
        );

    public GDictionary GetBattleSpecialProfileRegistryRuntimeSnapshot() =>
        RuntimePlainPayload.ProjectDictionary(
            _battleSpecialProfileRuntimeSnapshot,
            "GameContentCatalog.GetBattleSpecialProfileRegistryRuntimeSnapshot"
        );

    internal IBattleSpecialProfileView GetBattleSpecialProfileView() =>
        _battleSpecialProfileView ?? BattleSpecialProfileRuntimeView.Empty;

    private static GDictionary RegisterBattleSpecialProfileSnapshot(
        GDictionary snapshot,
        string reason
    )
    {
        if (snapshot == null)
            return new GDictionary();
        GodotContentOwnership.RegisterDerivedWrapper(
            snapshot,
            $"battle_special_profile_snapshot:{RuntimeHelpers.GetHashCode(snapshot)}",
            reason
        );
        return snapshot;
    }

    private void ReplaceBattleSpecialProfileRuntimeSnapshot(
        GDictionary snapshot,
        string reason
    )
    {
        _battleSpecialProfileRuntimeSnapshot.Clear();
        Dictionary<string, object> normalized =
            RuntimePlainPayload.NormalizeDictionary(snapshot ?? new GDictionary(), reason);
        foreach (KeyValuePair<string, object> entry in normalized)
        {
            _battleSpecialProfileRuntimeSnapshot[entry.Key] = entry.Value;
        }
    }

    private static IReadOnlyDictionary<StringName, T> SnapshotTyped<T>(
        IReadOnlyDictionary<StringName, T> source
    )
    {
        return source == null
            ? EmptyTyped<T>()
            : new ReadOnlyDictionary<StringName, T>(new Dictionary<StringName, T>(source));
    }

    private static IReadOnlyDictionary<StringName, T> EmptyTyped<T>()
    {
        return new ReadOnlyDictionary<StringName, T>(new Dictionary<StringName, T>());
    }

    private bool TryGetSession(out GameSession session)
    {
        session = null;
        return _sessionRef != null
            && _sessionRef.TryGetTarget(out session)
            && session != null
            && GodotObject.IsInstanceValid(session);
    }
}
