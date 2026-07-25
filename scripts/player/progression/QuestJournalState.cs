using System;
using System.Collections.Generic;
using Godot;

internal sealed class QuestJournalState
{
    private readonly Dictionary<StringName, QuestState> _active = new();
    private readonly Dictionary<StringName, QuestState> _claimable = new();
    private readonly Dictionary<StringName, QuestState> _failed = new();
    private readonly HashSet<StringName> _rewarded = new();

    internal QuestJournalState DuplicateState()
    {
        var copy = new QuestJournalState();
        CopyStates(_active, copy._active);
        CopyStates(_claimable, copy._claimable);
        CopyStates(_failed, copy._failed);
        copy._rewarded.UnionWith(_rewarded);
        return copy;
    }

    internal List<QuestState> GetActiveQuests() => CloneSortedStates(_active);

    internal List<QuestState> GetClaimableQuests() => CloneSortedStates(_claimable);

    internal List<QuestState> GetFailedQuests() => CloneSortedStates(_failed);

    internal List<StringName> GetRewardedQuestIds() => CloneSortedIds(_rewarded);

    internal List<StringName> GetActiveQuestIds() => CloneSortedIds(_active.Keys);

    internal List<StringName> GetClaimableQuestIds() => CloneSortedIds(_claimable.Keys);

    internal List<StringName> GetFailedQuestIds() => CloneSortedIds(_failed.Keys);

    internal QuestState GetActiveQuest(StringName questId) =>
        CloneState(_active, questId);

    internal QuestState GetClaimableQuest(StringName questId) =>
        CloneState(_claimable, questId);

    internal QuestState GetFailedQuest(StringName questId) =>
        CloneState(_failed, questId);

    internal QuestState GetQuest(StringName questId)
    {
        QuestState questState = GetActiveQuest(questId);
        if (questState != null)
            return questState;
        questState = GetClaimableQuest(questId);
        return questState ?? GetFailedQuest(questId);
    }

    internal bool HasActiveQuest(StringName questId) =>
        questId != "" && _active.ContainsKey(questId);

    internal bool HasClaimableQuest(StringName questId) =>
        questId != "" && _claimable.ContainsKey(questId);

    internal bool HasFailedQuest(StringName questId) =>
        questId != "" && _failed.ContainsKey(questId);

    internal bool HasRewardedQuest(StringName questId) =>
        questId != "" && _rewarded.Contains(questId);

    internal bool SetState(QuestState questState)
    {
        if (questState == null || questState.quest_id == "")
            return false;

        return QuestState.ToStatusKind(questState.status_id) switch
        {
            QuestStatusKind.Active => SetActiveQuest(questState),
            QuestStatusKind.Completed => SetClaimableQuest(questState),
            QuestStatusKind.Failed => SetFailedQuest(questState),
            QuestStatusKind.Rewarded => AddRewardedQuest(questState.quest_id),
            _ => false,
        };
    }

    internal bool SetActiveQuest(QuestState questState) =>
        SetQuestInStage(_active, questState, QuestStatusKind.Active);

    internal bool SetClaimableQuest(QuestState questState) =>
        SetQuestInStage(_claimable, questState, QuestStatusKind.Completed);

    internal bool SetFailedQuest(QuestState questState) =>
        SetQuestInStage(_failed, questState, QuestStatusKind.Failed);

    internal bool AddRewardedQuest(StringName questId)
    {
        if (questId == "")
            return false;
        RemoveEverywhere(questId);
        return _rewarded.Add(questId);
    }

    internal bool TryAcceptNewQuest(StringName questId, int worldStep)
    {
        if (questId == "" || ContainsQuest(questId))
            return false;
        _active[questId] = CreateActiveQuest(questId, worldStep);
        return true;
    }

    internal bool TryRestartRewardedQuest(StringName questId, int worldStep)
    {
        if (questId == "" || !_rewarded.Remove(questId))
            return false;
        _active[questId] = CreateActiveQuest(questId, worldStep);
        return true;
    }

    internal bool TryRestartFailedQuest(StringName questId, int worldStep)
    {
        if (questId == "" || !_failed.Remove(questId))
            return false;
        _active[questId] = CreateActiveQuest(questId, worldStep);
        return true;
    }

    internal bool TryRecordObjectiveProgress(
        StringName questId,
        StringName objectiveId,
        int delta,
        int targetValue,
        QuestProgressContext context,
        out QuestState updatedState
    )
    {
        updatedState = null;
        if (
            questId == ""
            || !_active.TryGetValue(questId, out QuestState questState)
            || questState == null
            || !questState.IsActive()
        )
            return false;

        questState.RecordObjectiveProgress(objectiveId, delta, targetValue, context);
        updatedState = questState.DuplicateState();
        return true;
    }

    internal bool TryMarkClaimable(StringName questId, int worldStep)
    {
        if (
            questId == ""
            || !_active.Remove(questId, out QuestState questState)
            || questState == null
        )
            return false;

        questState.MarkCompleted(worldStep);
        _claimable[questId] = questState;
        return true;
    }

    internal bool TryMarkRewarded(StringName questId, int worldStep)
    {
        if (
            questId == ""
            || !_claimable.Remove(questId, out QuestState questState)
            || questState == null
        )
            return false;

        questState.MarkRewardClaimed(worldStep);
        _rewarded.Add(questId);
        return true;
    }

    internal bool TryMarkFailed(
        StringName questId,
        int worldStep,
        StringName reasonId,
        QuestProgressContext context
    )
    {
        if (
            questId == ""
            || reasonId == ""
            || worldStep < -1
            || !_active.Remove(questId, out QuestState questState)
            || questState == null
        )
            return false;

        if (!questState.MarkFailed(worldStep, reasonId, context))
        {
            _active[questId] = questState;
            return false;
        }

        _failed[questId] = questState;
        return true;
    }

    internal bool RemoveActiveQuest(StringName questId) =>
        questId != "" && _active.Remove(questId);

    internal bool RemoveClaimableQuest(StringName questId) =>
        questId != "" && _claimable.Remove(questId);

    internal bool RemoveFailedQuest(StringName questId) =>
        questId != "" && _failed.Remove(questId);

    internal void Clear()
    {
        _active.Clear();
        _claimable.Clear();
        _failed.Clear();
        _rewarded.Clear();
    }

    private bool SetQuestInStage(
        Dictionary<StringName, QuestState> target,
        QuestState questState,
        QuestStatusKind requiredStatus
    )
    {
        if (
            questState == null
            || questState.quest_id == ""
            || QuestState.ToStatusKind(questState.status_id) != requiredStatus
        )
            return false;

        QuestState storedState = questState.DuplicateState();
        RemoveEverywhere(storedState.quest_id);
        target[storedState.quest_id] = storedState;
        return true;
    }

    private bool ContainsQuest(StringName questId)
    {
        return _active.ContainsKey(questId)
            || _claimable.ContainsKey(questId)
            || _failed.ContainsKey(questId)
            || _rewarded.Contains(questId);
    }

    private void RemoveEverywhere(StringName questId)
    {
        _active.Remove(questId);
        _claimable.Remove(questId);
        _failed.Remove(questId);
        _rewarded.Remove(questId);
    }

    private static QuestState CreateActiveQuest(StringName questId, int worldStep)
    {
        var questState = new QuestState { quest_id = questId };
        questState.MarkAccepted(worldStep);
        return questState;
    }

    private static QuestState CloneState(
        IReadOnlyDictionary<StringName, QuestState> source,
        StringName questId
    )
    {
        return questId != ""
            && source.TryGetValue(questId, out QuestState questState)
            && questState != null
            ? questState.DuplicateState()
            : null;
    }

    private static List<QuestState> CloneSortedStates(
        IReadOnlyDictionary<StringName, QuestState> source
    )
    {
        var result = new List<QuestState>();
        foreach (StringName questId in CloneSortedIds(source.Keys))
        {
            if (source.TryGetValue(questId, out QuestState questState) && questState != null)
                result.Add(questState.DuplicateState());
        }
        return result;
    }

    private static List<StringName> CloneSortedIds(IEnumerable<StringName> source)
    {
        var result = new List<StringName>(source ?? Array.Empty<StringName>());
        result.Sort(
            (left, right) =>
                string.CompareOrdinal(left.ToString(), right.ToString())
        );
        return result;
    }

    private static void CopyStates(
        IReadOnlyDictionary<StringName, QuestState> source,
        IDictionary<StringName, QuestState> target
    )
    {
        foreach ((StringName questId, QuestState questState) in source)
        {
            if (questId != "" && questState != null)
                target[questId] = questState.DuplicateState();
        }
    }
}
