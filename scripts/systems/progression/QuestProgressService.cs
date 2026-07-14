using Godot;
using System.Collections.Generic;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

internal enum QuestProgressEventKind
{
    Unknown = 0,
    Accept,
    Progress,
    Complete,
}

public sealed class QuestProgressService
{
    private static readonly StringName EventAccept = "accept";
    private static readonly StringName EventProgress = "progress";
    private static readonly StringName EventComplete = "complete";
    private static readonly IReadOnlyList<QuestObjectiveDefData> EmptyObjectiveDefs =
        new List<QuestObjectiveDefData>();

    private PartyState _party_state = new();
    private bool _has_quest_def_catalog;
    private Dictionary<StringName, QuestDefinition> _quest_def_index = new();
    private Dictionary<StringName, IReadOnlyList<QuestObjectiveDefData>> _objective_defs_by_quest_id =
        new();

    internal static StringName ToStringName(QuestProgressEventKind kind)
    {
        return kind switch
        {
            QuestProgressEventKind.Accept => EventAccept,
            QuestProgressEventKind.Progress => EventProgress,
            QuestProgressEventKind.Complete => EventComplete,
            _ => "",
        };
    }

    internal static QuestProgressEventKind ToEventKind(StringName eventType)
    {
        if (eventType == EventAccept)
            return QuestProgressEventKind.Accept;
        if (eventType == EventProgress)
            return QuestProgressEventKind.Progress;
        if (eventType == EventComplete)
            return QuestProgressEventKind.Complete;
        return QuestProgressEventKind.Unknown;
    }

    public void Setup(
        PartyState partyState,
        IReadOnlyDictionary<StringName, QuestDefinition> questDefs
    ) => Setup(partyState, questDefs, questDefs != null && questDefs.Count > 0);

    public void Setup(
        PartyState partyState,
        IReadOnlyDictionary<StringName, QuestDefinition> questDefs,
        bool hasQuestDefCatalog
    )
    {
        _party_state = partyState ?? new PartyState();
        _has_quest_def_catalog = hasQuestDefCatalog;
        _quest_def_index = CloneQuestDefIndex(questDefs);
        _objective_defs_by_quest_id = BuildObjectiveDefIndex(_quest_def_index);
    }

    public void SetPartyState(
        PartyState partyState,
        IReadOnlyDictionary<StringName, QuestDefinition> questDefs
    )
    {
        Setup(
            partyState,
            questDefs ?? _quest_def_index,
            questDefs != null ? questDefs.Count > 0 : _has_quest_def_catalog
        );
    }

    public void Dispose()
    {
        _party_state = null;
        _has_quest_def_catalog = false;
        _quest_def_index.Clear();
        _objective_defs_by_quest_id.Clear();
    }

    public PartyState GetPartyState() => _party_state;

    public List<QuestState> GetActiveQuestsTyped()
    {
        return _party_state?.GetActiveQuestsTyped() ?? new List<QuestState>();
    }

    public List<QuestState> GetClaimableQuestsTyped()
    {
        return _party_state?.GetClaimableQuestsTyped() ?? new List<QuestState>();
    }

    public List<StringName> GetClaimableQuestIdsTyped()
    {
        return _party_state?.GetClaimableQuestIdsTyped() ?? new List<StringName>();
    }

    public List<StringName> GetCompletedQuestIdsTyped()
    {
        return _party_state?.GetCompletedQuestIdsTyped() ?? new List<StringName>();
    }

    public bool AcceptQuest(StringName questId, int worldStep = -1, bool allowReaccept = false)
    {
        if (_party_state == null || questId == "")
            return false;
        if (_has_quest_def_catalog && !_quest_def_index.ContainsKey(questId))
            return false;
        if (_party_state.HasActiveQuest(questId))
            return false;
        if (_party_state.HasClaimableQuest(questId))
            return false;
        if (_party_state.HasCompletedQuest(questId) && !allowReaccept)
            return false;
        if (allowReaccept && _party_state.HasCompletedQuest(questId))
            _party_state.completed_quest_ids.Remove(questId);

        QuestState questState = new() { quest_id = questId };
        questState.MarkAccepted(worldStep);
        _party_state.SetActiveQuestState(questState);
        return true;
    }

    public bool CompleteQuest(StringName questId, int worldStep = -1)
    {
        if (_party_state == null || questId == "")
            return false;
        if (_party_state.HasClaimableQuest(questId))
            return false;
        if (_party_state.HasCompletedQuest(questId))
            return false;
        return _party_state.MarkQuestClaimable(questId, worldStep);
    }

    public bool RecordProgress(
        StringName questId,
        StringName objectiveId,
        int delta,
        int targetValue = 0,
        QuestProgressContext context = null
    )
    {
        if (_party_state == null || questId == "" || objectiveId == "" || delta <= 0)
            return false;

        QuestState questState = _party_state.GetActiveQuestState(questId);
        if (questState == null || !questState.IsActive())
            return false;

        int resolvedTarget =
            targetValue > 0
                ? targetValue
                : ResolveObjectiveTargetValue(FindObjectiveDef(questId, objectiveId));
        if (resolvedTarget <= 0)
            return false;

        questState.RecordObjectiveProgress(objectiveId, delta, resolvedTarget, context);
        QuestDefinition questDef = GetQuestDefObject(questId);
        if (questDef != null && questState.HasCompletedAllObjectives(questDef))
            questState.MarkCompleted(GetWorldStep());
        return true;
    }

    public bool MarkCompleted(StringName questId)
    {
        return CompleteQuest(questId, GetWorldStep());
    }

    public bool ClaimReward(StringName questId, GDictionary claimContext = null)
    {
        if (_party_state == null || questId == "")
            return false;
        return _party_state.MarkQuestRewardClaimed(questId, GetWorldStep());
    }

    public Godot.Collections.Array<GDictionary> GetQuestProgressEvents(StringName questId)
    {
        Godot.Collections.Array<GDictionary> result = new();
        if (_party_state == null)
            return result;
        QuestState questState = _party_state.GetQuestState(questId);
        if (questState != null)
            result.Add(QuestProgressResultProjection.ProjectQuestState(questState));
        return result;
    }

    internal QuestProgressApplyResultData ApplyDirectProgressTyped(
        StringName questId,
        StringName objectiveId,
        int progressDelta,
        int worldStep,
        bool hasTargetValue,
        int targetValue,
        QuestProgressEventContextData contextData
    )
    {
        QuestProgressEventData eventData = QuestProgressEventData.CreateProgress(
            questId,
            objectiveId,
            progressDelta,
            worldStep,
            hasTargetValue,
            targetValue,
            contextData
        );
        return ApplyQuestProgressEventsTyped(new[] { eventData });
    }

    internal QuestProgressApplyResultData ApplyQuestProgressEventsTyped(
        IEnumerable<QuestProgressEventData> eventOptions
    )
    {
        QuestProgressApplyResultData summary = new();
        if (_party_state == null || eventOptions == null)
            return summary;

        foreach (QuestProgressEventData eventData in eventOptions)
        {
            if (eventData == null || !eventData.IsValid)
                continue;

            if (eventData.EventType == EventAccept)
            {
                if (
                    eventData.QuestId != ""
                    && AcceptQuest(
                        eventData.QuestId,
                        eventData.WorldStep,
                        eventData.AllowReaccept
                    )
                )
                    summary.AppendAcceptedQuestId(eventData.QuestId);
            }
            else if (eventData.EventType == EventComplete)
            {
                if (eventData.QuestId == "")
                    continue;
                if (
                    !_party_state.HasActiveQuest(eventData.QuestId)
                    && eventData.AutoAccept
                )
                {
                    if (
                        AcceptQuest(
                            eventData.QuestId,
                            eventData.WorldStep,
                            eventData.AllowReaccept
                        )
                    )
                        summary.AppendAcceptedQuestId(eventData.QuestId);
                }
                if (CompleteQuest(eventData.QuestId, eventData.WorldStep))
                    summary.AppendClaimableQuestId(eventData.QuestId);
            }
            else if (eventData.EventType == EventProgress)
            {
                GStringNameArray progressedQuestIds = ApplyProgressEvent(eventData);
                foreach (StringName progressedQuestId in progressedQuestIds)
                    summary.AppendProgressedQuestId(progressedQuestId);

                foreach (
                    StringName claimableQuestId in MaybeCompleteQuestsAfterProgress(
                        eventData.WorldStep,
                        progressedQuestIds
                    )
                )
                    summary.AppendClaimableQuestId(claimableQuestId);
            }
        }
        return summary;
    }

    internal static IEnumerable<QuestProgressEventData> ReadEventOptions(GArray eventOptions)
    {
        if (eventOptions == null)
            yield break;
        foreach (Variant eventValue in eventOptions)
            yield return QuestProgressEventData.FromVariant(eventValue);
    }

    private GStringNameArray ApplyProgressEvent(QuestProgressEventData eventData)
    {
        GStringNameArray progressedQuestIds = new();
        StringName questId = eventData.QuestId;
        int progressDelta = eventData.ProgressDelta;
        if (progressDelta <= 0)
            return progressedQuestIds;

        if (questId != "")
        {
            QuestState questState = _party_state.GetActiveQuestState(questId);
            if (questState == null && eventData.AutoAccept)
            {
                if (
                    AcceptQuest(questId, eventData.WorldStep, eventData.AllowReaccept)
                )
                    questState = _party_state.GetActiveQuestState(questId);
            }
            if (questState == null)
                return progressedQuestIds;

            StringName objectiveId = eventData.ObjectiveId;
            if (objectiveId == "")
                return progressedQuestIds;

            int targetValue = ResolveEventTargetValue(eventData, questId, objectiveId);
            if (targetValue <= 0)
                return progressedQuestIds;

            questState.RecordObjectiveProgress(
                objectiveId,
                progressDelta,
                targetValue,
                eventData.BuildContext()
            );
            progressedQuestIds.Add(questId);
            return progressedQuestIds;
        }

        foreach (QuestActiveObjectiveMatch match in FindMatchingActiveObjectives(eventData))
        {
            QuestState questState = match.QuestState;
            QuestObjectiveDefData objectiveDef = match.ObjectiveDef;
            if (questState == null || !objectiveDef.Exists)
                continue;

            StringName objectiveId = objectiveDef.ObjectiveId;
            if (objectiveId == "")
                continue;

            int targetValue = ResolveObjectiveTargetValue(objectiveDef);
            if (targetValue <= 0)
                continue;

            questState.RecordObjectiveProgress(
                objectiveId,
                progressDelta,
                targetValue,
                eventData.BuildContext()
            );
            AppendUniqueStringName(progressedQuestIds, questState.quest_id);
        }
        return progressedQuestIds;
    }

    private int ResolveEventTargetValue(
        QuestProgressEventData eventData,
        StringName questId,
        StringName objectiveId
    )
    {
        if (objectiveId == "")
            return 0;
        if (eventData.HasTargetValue)
            return eventData.TargetValue;
        return ResolveObjectiveTargetValue(FindObjectiveDef(questId, objectiveId));
    }

    private static int ResolveObjectiveTargetValue(QuestObjectiveDefData objectiveDef) =>
        objectiveDef != null && objectiveDef.Exists ? objectiveDef.TargetValue : 0;

    private QuestObjectiveDefData FindObjectiveDef(StringName questId, StringName objectiveId)
    {
        if (questId == "" || objectiveId == "")
            return QuestObjectiveDefData.Empty;

        foreach (QuestObjectiveDefData objectiveDef in GetObjectiveDefs(questId))
        {
            if (objectiveDef.ObjectiveId == objectiveId)
                return objectiveDef;
        }
        return QuestObjectiveDefData.Empty;
    }

    private IReadOnlyList<QuestObjectiveDefData> GetObjectiveDefs(StringName questId)
    {
        if (questId == "")
            return EmptyObjectiveDefs;
        return _objective_defs_by_quest_id.TryGetValue(questId, out var objectiveDefs)
            ? objectiveDefs
            : EmptyObjectiveDefs;
    }

    private List<QuestActiveObjectiveMatch> FindMatchingActiveObjectives(
        QuestProgressEventData eventData
    )
    {
        var matches = new List<QuestActiveObjectiveMatch>();
        StringName objectiveType = eventData.ObjectiveType;
        StringName targetId = eventData.TargetId;
        if (objectiveType == "")
            return matches;

        foreach (QuestState questState in GetActiveQuestsTyped())
        {
            if (questState == null || questState.quest_id == "")
                continue;
            if (!_quest_def_index.ContainsKey(questState.quest_id))
                continue;

            foreach (QuestObjectiveDefData objectiveDef in GetObjectiveDefs(questState.quest_id))
            {
                if (!objectiveDef.Exists)
                    continue;

                if (objectiveDef.ObjectiveType != objectiveType)
                    continue;

                StringName objectiveTargetId = objectiveDef.TargetId;
                if (objectiveTargetId != "" && targetId != "" && objectiveTargetId != targetId)
                    continue;
                if (objectiveTargetId != "" && targetId == "")
                    continue;

                matches.Add(new QuestActiveObjectiveMatch(questState, objectiveDef));
            }
        }
        return matches;
    }

    private GStringNameArray MaybeCompleteQuestsAfterProgress(
        int worldStep,
        GStringNameArray progressedQuestIds
    )
    {
        GStringNameArray claimableQuestIds = new();
        foreach (StringName questId in progressedQuestIds)
        {
            QuestState questState = _party_state.GetActiveQuestState(questId);
            QuestDefinition questDef = GetQuestDefObject(questId);
            if (questState == null || questDef == null)
                continue;
            if (
                questState.HasCompletedAllObjectives(questDef)
                && CompleteQuest(questId, worldStep)
            )
                claimableQuestIds.Add(questId);
        }
        return claimableQuestIds;
    }

    private static void AppendUniqueStringName(GStringNameArray target, StringName value)
    {
        if (target == null || value == "" || target.Contains(value))
            return;
        target.Add(value);
    }

    private QuestDefinition GetQuestDefObject(StringName questId)
    {
        return questId != "" && _quest_def_index.TryGetValue(questId, out QuestDefinition questDef)
            ? questDef
            : null;
    }

    private static Dictionary<StringName, QuestDefinition> CloneQuestDefIndex(
        IReadOnlyDictionary<StringName, QuestDefinition> questDefs
    )
    {
        Dictionary<StringName, QuestDefinition> questDefIndex = new();
        if (questDefs == null)
            return questDefIndex;

        foreach ((StringName questId, QuestDefinition questDef) in questDefs)
        {
            if (questId == "" || questDef == null || questDef.QuestId == "")
                continue;
            questDefIndex[questId] = questDef;
        }
        return questDefIndex;
    }

    private static Dictionary<StringName, IReadOnlyList<QuestObjectiveDefData>> BuildObjectiveDefIndex(
        IReadOnlyDictionary<StringName, QuestDefinition> questDefIndex
    )
    {
        Dictionary<StringName, IReadOnlyList<QuestObjectiveDefData>> result = new();
        if (questDefIndex == null)
            return result;

        foreach ((StringName questId, QuestDefinition questDef) in questDefIndex)
            result[questId] = CollectObjectiveDefs(questDef);
        return result;
    }

    private static IReadOnlyList<QuestObjectiveDefData> CollectObjectiveDefs(QuestDefinition questDef)
    {
        if (questDef == null)
            return EmptyObjectiveDefs;

        List<QuestObjectiveDefData> result = new();
        foreach (QuestObjectiveDefinition entry in questDef.Objectives)
        {
            QuestObjectiveDefData objectiveDef = QuestObjectiveDefData.FromDefinition(entry);
            if (objectiveDef.Exists)
                result.Add(objectiveDef);
        }
        return result.Count > 0 ? result : EmptyObjectiveDefs;
    }

    private sealed class QuestActiveObjectiveMatch
    {
        public readonly QuestState QuestState;
        public readonly QuestObjectiveDefData ObjectiveDef;

        public QuestActiveObjectiveMatch(QuestState questState, QuestObjectiveDefData objectiveDef)
        {
            QuestState = questState;
            ObjectiveDef = objectiveDef ?? QuestObjectiveDefData.Empty;
        }
    }

    internal sealed class QuestProgressEventData
    {
        public readonly bool IsValid;
        public readonly StringName EventType;
        public readonly StringName QuestId;
        public readonly StringName ObjectiveId;
        public readonly StringName ObjectiveType;
        public readonly StringName TargetId;
        public readonly int WorldStep;
        public readonly bool AllowReaccept;
        public readonly bool AutoAccept;
        public readonly int ProgressDelta;
        public readonly bool HasTargetValue;
        public readonly int TargetValue;
        public readonly QuestProgressEventContextData ContextData;
        public readonly StringName EncounterId;
        public readonly StringName EncounterKind;

        private QuestProgressEventData(
            bool isValid,
            StringName eventType,
            StringName questId,
            StringName objectiveId,
            StringName objectiveType,
            StringName targetId,
            int worldStep,
            bool allowReaccept,
            bool autoAccept,
            int progressDelta,
            bool hasTargetValue,
            int targetValue,
            QuestProgressEventContextData contextData,
            StringName encounterId,
            StringName encounterKind
        )
        {
            IsValid = isValid;
            EventType = eventType;
            QuestId = questId;
            ObjectiveId = objectiveId;
            ObjectiveType = objectiveType;
            TargetId = targetId;
            WorldStep = worldStep;
            AllowReaccept = allowReaccept;
            AutoAccept = autoAccept;
            ProgressDelta = progressDelta;
            HasTargetValue = hasTargetValue;
            TargetValue = targetValue;
            ContextData = contextData ?? new QuestProgressEventContextData();
            EncounterId = encounterId;
            EncounterKind = encounterKind;
        }

        public static QuestProgressEventData FromVariant(Variant value)
        {
            if (value.VariantType != Variant.Type.Dictionary)
                return Invalid();
            return FromDictionary(value.AsGodotDictionary());
        }

        public static QuestProgressEventData CreateProgress(
            StringName questId,
            StringName objectiveId,
            int progressDelta,
            int worldStep,
            bool hasTargetValue,
            int targetValue,
            QuestProgressEventContextData contextData
        )
        {
            if (questId == "" || objectiveId == "" || progressDelta <= 0 || worldStep < 0)
                return Invalid();
            if (hasTargetValue && targetValue <= 0)
                return Invalid();

            return new QuestProgressEventData(
                true,
                EventProgress,
                questId,
                objectiveId,
                "",
                "",
                worldStep,
                false,
                false,
                progressDelta,
                hasTargetValue,
                Mathf.Max(targetValue, 0),
                contextData,
                "",
                ""
            );
        }

        public static QuestProgressEventData CreateProgressByObjectiveTarget(
            StringName objectiveType,
            StringName targetId,
            int progressDelta,
            int worldStep,
            StringName enemyTemplateId = default,
            StringName encounterId = default,
            StringName encounterKind = default
        )
        {
            if (objectiveType == "" || targetId == "" || progressDelta <= 0 || worldStep < 0)
                return Invalid();

            return new QuestProgressEventData(
                true,
                EventProgress,
                "",
                "",
                objectiveType,
                targetId,
                worldStep,
                false,
                false,
                progressDelta,
                false,
                0,
                new QuestProgressEventContextData { EnemyTemplateId = enemyTemplateId },
                encounterId,
                encounterKind
            );
        }

        internal static QuestProgressEventData FromDictionary(GDictionary data)
        {
            if (data == null || data.Count == 0)
                return Invalid();

            StringName eventType = QuestProgressDataReader.ReadStringName(data, "event_type");
            if (eventType != EventAccept && eventType != EventProgress && eventType != EventComplete)
                return Invalid();
            if (!QuestProgressDataReader.TryReadInt(data, "world_step", out int worldStep))
                return Invalid();
            if (
                QuestProgressDataReader.HasKey(data, "allow_reaccept")
                && !QuestProgressDataReader.TryReadBool(data, "allow_reaccept", out _)
            )
                return Invalid();
            if (
                QuestProgressDataReader.HasKey(data, "auto_accept")
                && !QuestProgressDataReader.TryReadBool(data, "auto_accept", out _)
            )
                return Invalid();
            if (
                QuestProgressDataReader.HasKey(data, "context")
                && !QuestProgressDataReader.HasDictionary(data, "context")
            )
                return Invalid();

            StringName questId = QuestProgressDataReader.ReadStringName(data, "quest_id");
            StringName objectiveId = QuestProgressDataReader.ReadStringName(data, "objective_id");
            StringName objectiveType = QuestProgressDataReader.ReadStringName(
                data,
                "objective_type"
            );
            StringName targetId = QuestProgressDataReader.ReadStringName(data, "target_id");
            bool allowReaccept = QuestProgressDataReader.TryReadBool(
                data,
                "allow_reaccept",
                out bool parsedAllowReaccept
            ) && parsedAllowReaccept;
            bool autoAccept = QuestProgressDataReader.TryReadBool(
                data,
                "auto_accept",
                out bool parsedAutoAccept
            ) && parsedAutoAccept;
            bool hasTargetValue = QuestProgressDataReader.HasKey(data, "target_value");
            int targetValue = 0;
            if (
                hasTargetValue
                && !QuestProgressDataReader.TryReadInt(data, "target_value", out targetValue)
            )
                return Invalid();
            if (hasTargetValue && targetValue <= 0)
                return Invalid();

            int progressDelta = 0;
            if (eventType == EventProgress)
            {
                if (!QuestProgressDataReader.TryReadInt(data, "progress_delta", out progressDelta))
                    return Invalid();
                if (progressDelta <= 0)
                    return Invalid();
                if (
                    QuestProgressDataReader.HasKey(data, "quest_id")
                    || QuestProgressDataReader.HasKey(data, "objective_id")
                )
                {
                    if (questId == "" || objectiveId == "")
                        return Invalid();
                }
                else if (objectiveType == "" || targetId == "")
                {
                    return Invalid();
                }
            }
            else if (questId == "")
            {
                return Invalid();
            }
            QuestProgressEventContextData contextData = ReadContextData(data);
            StringName encounterId = QuestProgressDataReader.ReadStringName(
                data,
                "encounter_id"
            );
            StringName encounterKind = QuestProgressDataReader.ReadStringName(
                data,
                "encounter_kind"
            );

            return new QuestProgressEventData(
                true,
                eventType,
                questId,
                objectiveId,
                objectiveType,
                targetId,
                worldStep,
                allowReaccept,
                autoAccept,
                progressDelta,
                hasTargetValue,
                Mathf.Max(targetValue, 0),
                contextData,
                encounterId,
                encounterKind
            );
        }

        internal QuestProgressContext BuildContext() =>
            new()
            {
                MemberId = ContextData.MemberId,
                ActionId = ContextData.ActionId,
                EnemyTemplateId = ContextData.EnemyTemplateId,
                SettlementId = ContextData.SettlementId,
                SourceType = ContextData.SourceType,
                SourceId = ContextData.SourceId,
                ItemId = ContextData.ItemId,
                SubmittedQuantity = ContextData.SubmittedQuantity,
            };

        private static QuestProgressEventData Invalid() =>
            new(
                false,
                "",
                "",
                "",
                "",
                "",
                0,
                false,
                false,
                0,
                false,
                0,
                new QuestProgressEventContextData(),
                "",
                ""
            );

        private static QuestProgressEventContextData ReadContextData(GDictionary data)
        {
            GDictionary context = QuestProgressDataReader.ReadDictionary(data, "context");
            int submittedQuantity =
                QuestProgressDataReader.TryReadInt(
                    context,
                    "submitted_quantity",
                    out int parsedSubmittedQuantity
                ) && parsedSubmittedQuantity > 0
                    ? parsedSubmittedQuantity
                    : 0;
            return new QuestProgressEventContextData
            {
                MemberId = QuestProgressDataReader.ReadStringName(data, "member_id"),
                ActionId = QuestProgressDataReader.ReadString(data, "action_id"),
                EnemyTemplateId = QuestProgressDataReader.ReadStringName(
                    data,
                    "enemy_template_id"
                ),
                SettlementId = QuestProgressDataReader.ReadString(data, "settlement_id"),
                SourceType = QuestProgressDataReader.ReadStringName(data, "source_type"),
                SourceId = QuestProgressDataReader.ReadStringName(data, "source_id"),
                ItemId = QuestProgressDataReader.ReadStringName(context, "item_id"),
                SubmittedQuantity = submittedQuantity,
            };
        }
    }

    private sealed class QuestObjectiveDefData
    {
        public static readonly QuestObjectiveDefData Empty = new(false, "", "", "", 0);

        public readonly bool Exists;
        public readonly StringName ObjectiveId;
        public readonly StringName ObjectiveType;
        public readonly StringName TargetId;
        public readonly int TargetValue;

        private QuestObjectiveDefData(
            bool exists,
            StringName objectiveId,
            StringName objectiveType,
            StringName targetId,
            int targetValue
        )
        {
            Exists = exists;
            ObjectiveId = objectiveId;
            ObjectiveType = objectiveType;
            TargetId = targetId;
            TargetValue = Mathf.Max(targetValue, 0);
        }

        public static QuestObjectiveDefData FromVariant(Variant value)
        {
            if (value.VariantType != Variant.Type.Dictionary)
                return Empty;
            return FromDictionary(value.AsGodotDictionary());
        }

        public static QuestObjectiveDefData FromDictionary(GDictionary data)
        {
            if (data == null || data.Count == 0)
                return Empty;
            return new QuestObjectiveDefData(
                true,
                QuestProgressDataReader.ReadStringName(data, "objective_id"),
                QuestProgressDataReader.ReadStringName(data, "objective_type"),
                QuestProgressDataReader.ReadStringName(data, "target_id"),
                QuestProgressDataReader.TryReadInt(data, "target_value", out int targetValue)
                    ? targetValue
                    : 0
            );
        }

        public static QuestObjectiveDefData FromDefinition(QuestObjectiveDefinition entry)
        {
            if (entry == null)
                return Empty;
            return new QuestObjectiveDefData(
                true,
                entry.ObjectiveId,
                entry.ObjectiveType,
                entry.TargetId,
                entry.TargetValue
            );
        }
    }

    private static class QuestProgressDataReader
    {
        public static bool HasKey(GDictionary data, string key)
        {
            return TryGet(data, key, out _);
        }

        public static bool TryGet(GDictionary data, string key, out Variant value)
        {
            if (data == null || string.IsNullOrEmpty(key))
            {
                value = default;
                return false;
            }
            foreach (Variant rawKey in data.Keys)
            {
                if (
                    rawKey.VariantType == Variant.Type.String
                    && rawKey.AsString() == key
                )
                {
                    value = data[rawKey];
                    return true;
                }
                if (
                    rawKey.VariantType == Variant.Type.StringName
                    && rawKey.AsStringName().ToString() == key
                )
                {
                    value = data[rawKey];
                    return true;
                }
            }
            value = default;
            return false;
        }

        internal static StringName ReadStringName(GDictionary data, string key)
        {
            if (!TryGet(data, key, out Variant value))
                return "";
            return value.VariantType switch
            {
                Variant.Type.StringName => value.AsStringName(),
                Variant.Type.String => new StringName(value.AsString()),
                _ => new StringName(""),
            };
        }

        internal static string ReadString(GDictionary data, string key)
        {
            if (!TryGet(data, key, out Variant value))
                return "";
            return value.VariantType switch
            {
                Variant.Type.String => value.AsString(),
                Variant.Type.StringName => value.AsStringName().ToString(),
                _ => "",
            };
        }

        internal static bool TryReadInt(GDictionary data, string key, out int result)
        {
            if (!TryGet(data, key, out Variant value) || value.VariantType != Variant.Type.Int)
            {
                result = 0;
                return false;
            }
            result = value.AsInt32();
            return true;
        }

        internal static bool TryReadBool(GDictionary data, string key, out bool result)
        {
            if (!TryGet(data, key, out Variant value) || value.VariantType != Variant.Type.Bool)
            {
                result = false;
                return false;
            }
            result = value.AsBool();
            return true;
        }

        internal static bool HasDictionary(GDictionary data, string key)
        {
            return TryGet(data, key, out Variant value)
                && value.VariantType == Variant.Type.Dictionary;
        }

        internal static GDictionary ReadDictionary(GDictionary data, string key)
        {
            if (!TryGet(data, key, out Variant value))
                return new GDictionary();
            return value.VariantType == Variant.Type.Dictionary
                ? value.AsGodotDictionary().Duplicate(true)
                : new GDictionary();
        }

    }

    private int GetWorldStep()
    {
        // PartyState does not track a world step; quest steps are supplied explicitly
        // by callers, so the parameterless fallbacks resolve to step 0.
        return 0;
    }
}

internal sealed class QuestProgressApplyResultData
{
    private readonly List<StringName> _acceptedQuestIds = new();
    private readonly List<StringName> _progressedQuestIds = new();
    private readonly List<StringName> _claimableQuestIds = new();
    private readonly List<StringName> _completedQuestIds = new();

    public bool ContainsProgressedQuest(StringName questId) =>
        questId != "" && _progressedQuestIds.Contains(questId);

    public void AppendAcceptedQuestId(StringName questId) => AppendUnique(_acceptedQuestIds, questId);

    public void AppendProgressedQuestId(StringName questId) =>
        AppendUnique(_progressedQuestIds, questId);

    public void AppendClaimableQuestId(StringName questId) =>
        AppendUnique(_claimableQuestIds, questId);

    public void AppendCompletedQuestId(StringName questId) =>
        AppendUnique(_completedQuestIds, questId);

    public GStringNameArray CloneAcceptedQuestIds() => ToStringNameArray(_acceptedQuestIds);

    public GStringNameArray CloneProgressedQuestIds() => ToStringNameArray(_progressedQuestIds);

    public GStringNameArray CloneClaimableQuestIds() => ToStringNameArray(_claimableQuestIds);

    public GStringNameArray CloneCompletedQuestIds() => ToStringNameArray(_completedQuestIds);

    private static void AppendUnique(List<StringName> target, StringName questId)
    {
        if (target == null || questId == "" || target.Contains(questId))
            return;
        target.Add(questId);
    }

    private static GStringNameArray ToStringNameArray(IEnumerable<StringName> values)
    {
        GStringNameArray result = new();
        if (values == null)
            return result;
        foreach (StringName value in values)
            result.Add(value);
        return result;
    }
}

internal sealed class QuestProgressEventContextData
{
    public StringName MemberId { get; init; } = "";
    public string ActionId { get; init; } = "";
    public StringName EnemyTemplateId { get; init; } = "";
    public string SettlementId { get; init; } = "";
    public StringName SourceType { get; init; } = "";
    public StringName SourceId { get; init; } = "";
    public StringName ItemId { get; init; } = "";
    public int SubmittedQuantity { get; init; }
}
