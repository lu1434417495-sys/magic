using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

/// <summary>
/// 正式内容的组合根读入口。catalog 借用 process <see cref="ContentSnapshot"/> 的不可变
/// typed 字典；不会保留任何 authored Resource。
///
/// 两条防御性不变量：
/// 1. 非 AI 内容只来自构建期冻结的 process snapshot，session/catalog 不再复制或重建 registry。
/// 2. <see cref="ClearSessionBinding"/>（owning root dispose 时调用）会同时清空 typed 引用并
///    自增 revision，使任何仍持有旧 catalog 引用的下游读到的是空内容而非 stale 快照，并可用
///    revision 变化察觉失效。
/// </summary>
public sealed class GameContentCatalog
{
    private WeakReference<GameSession> _sessionRef;
    private long _revision;
    private SkillCatalog _skillCatalog;
    private long _snapshotEpoch;
    private ProgressionIdentityCatalogData _progressionIdentityCatalog;
    private IReadOnlyDictionary<StringName, SkillDefinition> _skillDefinitions;
    private IReadOnlyDictionary<StringName, TraitDefinition> _traitDefs;
    private IReadOnlyDictionary<StringName, ProfessionDefinition> _professionDefs;
    private IReadOnlyDictionary<StringName, AchievementDefinition> _achievementDefs;
    private IReadOnlyDictionary<StringName, QuestDefinition> _questDefs;
    private int _equipmentAbilityContentRevision;
    private IReadOnlyDictionary<StringName, EquipmentAbilityContentPackDefinition> _equipmentAbilityPacks;
    private IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> _equipmentAbilityBindings;
    private IReadOnlyDictionary<StringName, BarrierProfileDefinition> _barrierProfileDefinitions;
    private IReadOnlyDictionary<StringName, ItemDefinition> _itemDefinitions;
    private IReadOnlyDictionary<StringName, RecipeDefinition> _recipeDefinitions;
    private IReadOnlyDictionary<StringName, EnemyTemplateDefinition> _enemyTemplateDefinitions;
    private IReadOnlyDictionary<StringName, EnemyAiBrainDefinition> _enemyBrainDefinitions;
    private IReadOnlyDictionary<StringName, WildEncounterRosterDefinition> _encounterRosterDefinitions;
    private IReadOnlyDictionary<StringName, BattleSimProfileDefinition> _battleSimProfiles;
    private IBattleSpecialProfileView _battleSpecialProfileView;

    public GameContentCatalog()
    {
        ResetSnapshot();
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
    /// Bind the catalog once to the session's immutable process snapshot.
    /// </summary>
    internal void BindSnapshot(
        GameSession session,
        ContentSnapshot snapshot
    )
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(snapshot);

        _sessionRef = new WeakReference<GameSession>(session);
        _snapshotEpoch = snapshot.Epoch;
        _progressionIdentityCatalog = snapshot.IdentityCatalog;
        _skillDefinitions = snapshot.Skills;
        _traitDefs = snapshot.Traits;
        _professionDefs = snapshot.Professions;
        _achievementDefs = snapshot.Achievements;
        _questDefs = snapshot.Quests;
        _equipmentAbilityContentRevision = checked((int)snapshot.Epoch);
        _equipmentAbilityPacks = snapshot.EquipmentAbilityPacks;
        _equipmentAbilityBindings = snapshot.EquipmentAbilityBindings;
        _barrierProfileDefinitions = snapshot.BarrierProfiles;
        _itemDefinitions = snapshot.Items;
        _recipeDefinitions = snapshot.Recipes;
        _enemyTemplateDefinitions = snapshot.EnemyTemplates;
        _enemyBrainDefinitions = snapshot.EnemyBrains;
        _encounterRosterDefinitions = snapshot.EncounterRosters;
        _battleSimProfiles = snapshot.BattleSimProfiles;
        _battleSpecialProfileView = snapshot.BattleSpecialProfiles;
        _revision++;
    }

    private void ResetSnapshot()
    {
        _snapshotEpoch = 0;
        _progressionIdentityCatalog = new ProgressionIdentityCatalogData();
        _skillDefinitions = EmptyTyped<SkillDefinition>();
        _traitDefs = EmptyTyped<TraitDefinition>();
        _professionDefs = EmptyTyped<ProfessionDefinition>();
        _achievementDefs = EmptyTyped<AchievementDefinition>();
        _questDefs = EmptyTyped<QuestDefinition>();
        _equipmentAbilityContentRevision = 0;
        _equipmentAbilityPacks = EmptyTyped<EquipmentAbilityContentPackDefinition>();
        _equipmentAbilityBindings = EmptyTyped<EquipmentAbilityBindingDefinition>();
        _barrierProfileDefinitions = EmptyTyped<BarrierProfileDefinition>();
        _itemDefinitions = EmptyTyped<ItemDefinition>();
        _recipeDefinitions = EmptyTyped<RecipeDefinition>();
        _enemyTemplateDefinitions = EmptyTyped<EnemyTemplateDefinition>();
        _enemyBrainDefinitions = EmptyTyped<EnemyAiBrainDefinition>();
        _encounterRosterDefinitions = EmptyTyped<WildEncounterRosterDefinition>();
        _battleSimProfiles = EmptyTyped<BattleSimProfileDefinition>();
        _battleSpecialProfileView = BattleSpecialProfileRuntimeView.Empty;
    }

    /// <summary>catalog 绑定版本号；每次 snapshot bind 或 <see cref="ClearSessionBinding"/>
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

    public ProgressionIdentityCatalogData GetProgressionIdentityCatalogTyped() =>
        _progressionIdentityCatalog;

    internal long GetSnapshotEpoch() => _snapshotEpoch;

    public IReadOnlyDictionary<StringName, SkillDefinition> GetSkillDefinitionsTyped() =>
        _skillDefinitions;

    public IReadOnlyDictionary<StringName, TraitDefinition> GetTraitDefsTyped() => _traitDefs;

    /// <summary>
    /// 技能内容门面。门面只持有本 catalog 引用、每次查询都读当前 typed 快照与 revision，
    /// derived effective profile 由门面按 revision 缓存，因此随
    /// snapshot rebind / <see cref="ClearSessionBinding"/> 自动失效，无需每次重建；
    /// 跨调用返回同一实例。
    /// </summary>
    public ISkillCatalog GetSkillCatalogTyped() => _skillCatalog ??= new SkillCatalog(this);

    public IReadOnlyDictionary<StringName, ProfessionDefinition> GetProfessionDefsTyped() =>
        _professionDefs;

    public IReadOnlyDictionary<StringName, AchievementDefinition> GetAchievementDefsTyped() =>
        _achievementDefs;

    public IReadOnlyDictionary<StringName, QuestDefinition> GetQuestDefsTyped() => _questDefs;

    public int GetEquipmentAbilityContentRevision() => _equipmentAbilityContentRevision;

    public IReadOnlyDictionary<StringName, EquipmentAbilityContentPackDefinition> GetEquipmentAbilityPackDefinitionsTyped() =>
        _equipmentAbilityPacks;

    public IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> GetEquipmentAbilityBindingDefinitionsTyped() =>
        _equipmentAbilityBindings;

    public IReadOnlyDictionary<StringName, BarrierProfileDefinition> GetBarrierProfileDefinitionsTyped() =>
        _barrierProfileDefinitions;

    public QuestDefinition GetQuestDefTyped(StringName questId)
    {
        if (questId == "")
            return null;
        return _questDefs.TryGetValue(questId, out QuestDefinition questDef) ? questDef : null;
    }

    public IReadOnlyDictionary<StringName, ItemDefinition> GetItemDefsTyped() =>
        _itemDefinitions;

    public IReadOnlyDictionary<StringName, RecipeDefinition> GetRecipeDefsTyped() =>
        _recipeDefinitions;

    internal IReadOnlyDictionary<StringName, EnemyTemplateDefinition> GetEnemyTemplateDefinitions() =>
        _enemyTemplateDefinitions;

    internal IReadOnlyDictionary<StringName, EnemyAiBrainDefinition> GetEnemyAiBrainDefinitions() =>
        _enemyBrainDefinitions;

    internal IReadOnlyDictionary<StringName, WildEncounterRosterDefinition> GetEncounterRosterDefinitions() =>
        _encounterRosterDefinitions;

    internal IReadOnlyDictionary<StringName, BattleSimProfileDefinition> GetBattleSimProfiles() =>
        _battleSimProfiles;

    internal IBattleSpecialProfileView GetBattleSpecialProfileView() =>
        _battleSpecialProfileView ?? BattleSpecialProfileRuntimeView.Empty;

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
