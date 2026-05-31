using Godot;
using System.Collections.Generic;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

[GlobalClass]
public partial class QuestProgressService : RefCounted
{
    public static readonly StringName EVENT_ACCEPT = "accept";
    public static readonly StringName EVENT_PROGRESS = "progress";
    public static readonly StringName EVENT_COMPLETE = "complete";

    private PartyState _party_state = new();
    private GDictionary _quest_defs = new();

    public void setup(PartyState partyState, GDictionary questDefs)
    {
        _party_state = partyState ?? new PartyState();
        _quest_defs = questDefs ?? new GDictionary();
    }

    public void set_party_state(PartyState partyState, GDictionary questDefs)
    {
        setup(partyState, questDefs ?? _quest_defs);
    }

    public PartyState get_party_state() => _party_state;

    public GDictionary get_quest_defs() => _quest_defs;

    public Godot.Collections.Array<QuestState> get_active_quests()
    {
        return _party_state?.get_active_quests() ?? new Godot.Collections.Array<QuestState>();
    }

    public Godot.Collections.Array<QuestState> get_claimable_quests()
    {
        return _party_state?.get_claimable_quests() ?? new Godot.Collections.Array<QuestState>();
    }

    public GStringNameArray get_claimable_quest_ids()
    {
        return _party_state?.get_claimable_quest_ids() ?? new GStringNameArray();
    }

    public GStringNameArray get_completed_quest_ids()
    {
        return _party_state?.get_completed_quest_ids() ?? new GStringNameArray();
    }

    public bool accept_quest(StringName questId, int worldStep = -1, bool allowReaccept = false)
    {
        if (_party_state == null || questId == "")
            return false;
        if (_quest_defs.Count > 0 && !HasExactStringNameKey(_quest_defs, questId))
            return false;
        if (_party_state.has_active_quest(questId))
            return false;
        if (_party_state.has_claimable_quest(questId))
            return false;
        if (_party_state.has_completed_quest(questId) && !allowReaccept)
            return false;
        if (allowReaccept && _party_state.has_completed_quest(questId))
            _party_state.completed_quest_ids.Remove(questId);

        QuestState questState = new() { quest_id = questId };
        questState.mark_accepted(worldStep);
        _party_state.set_active_quest_state(questState);
        return true;
    }

    public bool complete_quest(StringName questId, int worldStep = -1)
    {
        if (_party_state == null || questId == "")
            return false;
        if (_party_state.has_claimable_quest(questId))
            return false;
        if (_party_state.has_completed_quest(questId))
            return false;
        return _party_state.mark_quest_claimable(questId, worldStep);
    }

    public bool record_progress(
        StringName questId,
        StringName objectiveId,
        int delta,
        int targetValue = 0,
        GDictionary context = null
    )
    {
        if (_party_state == null || questId == "" || objectiveId == "" || delta <= 0)
            return false;

        QuestState questState = _party_state.get_active_quest_state(questId);
        if (questState == null || !questState.is_active())
            return false;

        int resolvedTarget =
            targetValue > 0
                ? targetValue
                : ResolveObjectiveTargetValue(FindObjectiveDef(questId, objectiveId));
        if (resolvedTarget <= 0)
            return false;

        questState.record_objective_progress(objectiveId, delta, resolvedTarget, context);
        QuestDef questDef = GetQuestDefObject(questId);
        if (questDef != null && questState.has_completed_all_objectives(questDef))
            questState.mark_completed(GetWorldStep());
        return true;
    }

    public bool mark_completed(StringName questId)
    {
        return complete_quest(questId, GetWorldStep());
    }

    public bool claim_reward(StringName questId, GDictionary claimContext = null)
    {
        if (_party_state == null || questId == "")
            return false;
        return _party_state.mark_quest_reward_claimed(questId, GetWorldStep());
    }

    public Godot.Collections.Array<GDictionary> get_quest_progress_events(StringName questId)
    {
        Godot.Collections.Array<GDictionary> result = new();
        if (_party_state == null)
            return result;
        QuestState questState = _party_state.get_quest_state(questId);
        if (questState != null)
            result.Add(questState.to_dict());
        return result;
    }

    public GDictionary apply_quest_progress_events(GArray eventOptions)
    {
        return apply_quest_progress_events(eventOptions, -1);
    }

    public GDictionary apply_quest_progress_events(GArray eventOptions, int unusedWorldStep)
    {
        GStringNameArray acceptedQuestIds = new();
        GStringNameArray progressedSummaryQuestIds = new();
        GStringNameArray claimableSummaryQuestIds = new();
        GStringNameArray completedQuestIds = new();
        GDictionary summary = new()
        {
            ["accepted_quest_ids"] = acceptedQuestIds,
            ["progressed_quest_ids"] = progressedSummaryQuestIds,
            ["claimable_quest_ids"] = claimableSummaryQuestIds,
            ["completed_quest_ids"] = completedQuestIds,
        };
        if (_party_state == null || eventOptions == null)
            return summary;

        foreach (Variant eventValue in eventOptions)
        {
            QuestProgressEventData eventData = QuestProgressEventData.FromVariant(eventValue);
            if (!eventData.IsValid)
                continue;

            if (eventData.EventType == EVENT_ACCEPT)
            {
                if (
                    eventData.QuestId != ""
                    && accept_quest(
                        eventData.QuestId,
                        eventData.WorldStep,
                        eventData.AllowReaccept
                    )
                )
                    AppendUniqueStringName(acceptedQuestIds, eventData.QuestId);
            }
            else if (eventData.EventType == EVENT_COMPLETE)
            {
                if (eventData.QuestId == "")
                    continue;
                if (
                    !_party_state.has_active_quest(eventData.QuestId)
                    && eventData.AutoAccept
                )
                {
                    if (
                        accept_quest(
                            eventData.QuestId,
                            eventData.WorldStep,
                            eventData.AllowReaccept
                        )
                    )
                        AppendUniqueStringName(acceptedQuestIds, eventData.QuestId);
                }
                if (complete_quest(eventData.QuestId, eventData.WorldStep))
                    AppendUniqueStringName(claimableSummaryQuestIds, eventData.QuestId);
            }
            else if (eventData.EventType == EVENT_PROGRESS)
            {
                GStringNameArray progressedQuestIds = ApplyProgressEvent(eventData);
                foreach (StringName progressedQuestId in progressedQuestIds)
                    AppendUniqueStringName(progressedSummaryQuestIds, progressedQuestId);

                foreach (
                    StringName claimableQuestId in MaybeCompleteQuestsAfterProgress(
                        eventData.WorldStep,
                        progressedQuestIds
                    )
                )
                    AppendUniqueStringName(claimableSummaryQuestIds, claimableQuestId);
            }
        }
        return summary;
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
            QuestState questState = _party_state.get_active_quest_state(questId);
            if (questState == null && eventData.AutoAccept)
            {
                if (
                    accept_quest(questId, eventData.WorldStep, eventData.AllowReaccept)
                )
                    questState = _party_state.get_active_quest_state(questId);
            }
            if (questState == null)
                return progressedQuestIds;

            StringName objectiveId = eventData.ObjectiveId;
            if (objectiveId == "")
                return progressedQuestIds;

            int targetValue = ResolveEventTargetValue(eventData, questId, objectiveId);
            if (targetValue <= 0)
                return progressedQuestIds;

            questState.record_objective_progress(
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

            questState.record_objective_progress(
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

    private List<QuestObjectiveDefData> GetObjectiveDefs(StringName questId)
    {
        var result = new List<QuestObjectiveDefData>();
        if (questId == "" || !TryGetExactStringNameKey(_quest_defs, questId, out var questDef))
            return result;
        if (questDef.VariantType == Variant.Type.Dictionary)
        {
            GDictionary questDefDict = questDef.AsGodotDictionary();
            foreach (Variant objectiveValue in QuestProgressDataReader.ReadArray(
                questDefDict,
                "objective_defs"
            ))
            {
                QuestObjectiveDefData objectiveDef =
                    QuestObjectiveDefData.FromVariant(objectiveValue);
                if (objectiveDef.Exists)
                    result.Add(objectiveDef);
            }
            return result;
        }

        if (questDef.AsGodotObject() is not QuestDef questDefObject)
            return result;

        foreach (GDictionary entry in questDefObject.objective_defs)
        {
            QuestObjectiveDefData objectiveDef = QuestObjectiveDefData.FromDictionary(entry);
            if (objectiveDef.Exists)
                result.Add(objectiveDef);
        }
        return result;
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

        foreach (QuestState questState in get_active_quests())
        {
            if (questState == null || questState.quest_id == "")
                continue;
            if (!HasExactStringNameKey(_quest_defs, questState.quest_id))
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
            QuestState questState = _party_state.get_active_quest_state(questId);
            QuestDef questDef = GetQuestDefObject(questId);
            if (questState == null || questDef == null)
                continue;
            if (
                questState.has_completed_all_objectives(questDef)
                && complete_quest(questId, worldStep)
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

    private QuestDef GetQuestDefObject(StringName questId)
    {
        if (questId == "" || !TryGetExactStringNameKey(_quest_defs, questId, out var questDef))
            return null;
        return questDef.AsGodotObject() as QuestDef;
    }

    private static bool HasExactStringNameKey(GDictionary dictionary, StringName key) =>
        TryGetExactStringNameKey(dictionary, key, out _);

    private static bool TryGetExactStringNameKey(
        GDictionary dictionary,
        StringName key,
        out Variant value
    )
    {
        if (dictionary == null || key == "")
        {
            value = default;
            return false;
        }
        foreach (Variant rawKey in dictionary.Keys)
        {
            if (rawKey.VariantType != Variant.Type.StringName)
                continue;
            if (rawKey.AsStringName() != key)
                continue;
            value = dictionary[rawKey];
            return true;
        }
        value = default;
        return false;
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

    private sealed class QuestProgressEventData
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
        private readonly GDictionary _sourceData;

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
            GDictionary sourceData
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
            _sourceData = sourceData != null ? sourceData.Duplicate(true) : new GDictionary();
        }

        public static QuestProgressEventData FromVariant(Variant value)
        {
            if (value.VariantType != Variant.Type.Dictionary)
                return Invalid();
            return FromDictionary(value.AsGodotDictionary());
        }

        private static QuestProgressEventData FromDictionary(GDictionary data)
        {
            if (data == null || data.Count == 0)
                return Invalid();

            StringName eventType = QuestProgressDataReader.ReadStringName(data, "event_type");
            if (eventType != EVENT_ACCEPT && eventType != EVENT_PROGRESS && eventType != EVENT_COMPLETE)
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
            if (eventType == EVENT_PROGRESS)
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
                data
            );
        }

        public GDictionary BuildContext()
        {
            GDictionary context = QuestProgressDataReader.ReadDictionary(_sourceData, "context");
            foreach (
                string key in new[]
                {
                    "member_id",
                    "action_id",
                    "enemy_template_id",
                    "settlement_id",
                    "source_type",
                    "source_id",
                }
            )
            {
                if (QuestProgressDataReader.TryGet(_sourceData, key, out Variant value))
                    context[key] = value;
            }
            return context;
        }

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
                new GDictionary()
            );
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

        public static StringName ReadStringName(GDictionary data, string key)
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

        public static bool TryReadInt(GDictionary data, string key, out int result)
        {
            if (!TryGet(data, key, out Variant value) || value.VariantType != Variant.Type.Int)
            {
                result = 0;
                return false;
            }
            result = value.AsInt32();
            return true;
        }

        public static bool TryReadBool(GDictionary data, string key, out bool result)
        {
            if (!TryGet(data, key, out Variant value) || value.VariantType != Variant.Type.Bool)
            {
                result = false;
                return false;
            }
            result = value.AsBool();
            return true;
        }

        public static bool HasDictionary(GDictionary data, string key)
        {
            return TryGet(data, key, out Variant value)
                && value.VariantType == Variant.Type.Dictionary;
        }

        public static GDictionary ReadDictionary(GDictionary data, string key)
        {
            if (!TryGet(data, key, out Variant value))
                return new GDictionary();
            return value.VariantType == Variant.Type.Dictionary
                ? value.AsGodotDictionary().Duplicate(true)
                : new GDictionary();
        }

        public static GArray ReadArray(GDictionary data, string key)
        {
            if (!TryGet(data, key, out Variant value))
                return new GArray();
            return value.VariantType == Variant.Type.Array
                ? value.AsGodotArray()
                : new GArray();
        }
    }

    private int GetWorldStep()
    {
        // PartyState does not track a world step; quest steps are supplied explicitly
        // by callers, so the parameterless fallbacks resolve to step 0.
        return 0;
    }
}
