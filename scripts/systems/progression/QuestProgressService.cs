using Godot;
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
        if (_quest_defs.Count > 0 && !_quest_defs.ContainsKey(questId))
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

        foreach (var eventValue in eventOptions)
        {
            if (eventValue.VariantType != Variant.Type.Dictionary)
                continue;

            GDictionary eventData = eventValue.AsGodotDictionary().Duplicate(true);
            if (!IsValidQuestProgressEvent(eventData))
                continue;

            StringName eventType = ReadRequiredStringName(eventData, "event_type");
            StringName questId = ReadRequiredStringName(eventData, "quest_id");
            int eventWorldStep = GdInterop.GetInt(eventData, "world_step");

            if (eventType == EVENT_ACCEPT)
            {
                if (
                    questId != ""
                    && accept_quest(
                        questId,
                        eventWorldStep,
                        GdInterop.GetBool(eventData, "allow_reaccept")
                    )
                )
                    AppendUniqueStringName(acceptedQuestIds, questId);
            }
            else if (eventType == EVENT_COMPLETE)
            {
                if (questId == "")
                    continue;
                if (
                    !_party_state.has_active_quest(questId)
                    && GdInterop.GetBool(eventData, "auto_accept")
                )
                {
                    if (
                        accept_quest(
                            questId,
                            eventWorldStep,
                            GdInterop.GetBool(eventData, "allow_reaccept")
                        )
                    )
                        AppendUniqueStringName(acceptedQuestIds, questId);
                }
                if (complete_quest(questId, eventWorldStep))
                    AppendUniqueStringName(claimableSummaryQuestIds, questId);
            }
            else if (eventType == EVENT_PROGRESS)
            {
                GStringNameArray progressedQuestIds = ApplyProgressEvent(eventData, eventWorldStep);
                foreach (StringName progressedQuestId in progressedQuestIds)
                    AppendUniqueStringName(progressedSummaryQuestIds, progressedQuestId);

                foreach (
                    StringName claimableQuestId in MaybeCompleteQuestsAfterProgress(
                        eventData,
                        eventWorldStep,
                        progressedQuestIds
                    )
                )
                    AppendUniqueStringName(claimableSummaryQuestIds, claimableQuestId);
            }
        }
        return summary;
    }

    private GStringNameArray ApplyProgressEvent(GDictionary eventData, int worldStep)
    {
        GStringNameArray progressedQuestIds = new();
        StringName questId = GdInterop.GetStringName(eventData, "quest_id");
        int progressDelta = ResolveProgressDelta(eventData);
        if (progressDelta <= 0)
            return progressedQuestIds;

        if (questId != "")
        {
            QuestState questState = _party_state.get_active_quest_state(questId);
            if (questState == null && GdInterop.GetBool(eventData, "auto_accept"))
            {
                if (
                    accept_quest(questId, worldStep, GdInterop.GetBool(eventData, "allow_reaccept"))
                )
                    questState = _party_state.get_active_quest_state(questId);
            }
            if (questState == null)
                return progressedQuestIds;

            StringName objectiveId = GdInterop.GetStringName(eventData, "objective_id");
            if (objectiveId == "")
                return progressedQuestIds;

            int targetValue = ResolveEventTargetValue(eventData, questId, objectiveId);
            if (targetValue <= 0)
                return progressedQuestIds;

            questState.record_objective_progress(
                objectiveId,
                progressDelta,
                targetValue,
                BuildEventContext(eventData)
            );
            progressedQuestIds.Add(questId);
            return progressedQuestIds;
        }

        foreach (GDictionary matchEntry in FindMatchingActiveObjectives(eventData))
        {
            QuestState questState = matchEntry["quest_state"].AsGodotObject() as QuestState;
            GDictionary objectiveDef = matchEntry["objective_def"].AsGodotDictionary();
            if (questState == null || objectiveDef.Count == 0)
                continue;

            StringName objectiveId = GdInterop.GetStringName(objectiveDef, "objective_id");
            if (objectiveId == "")
                continue;

            int targetValue = ResolveObjectiveTargetValue(objectiveDef);
            if (targetValue <= 0)
                continue;

            questState.record_objective_progress(
                objectiveId,
                progressDelta,
                targetValue,
                BuildEventContext(eventData)
            );
            AppendUniqueStringName(progressedQuestIds, questState.quest_id);
        }
        return progressedQuestIds;
    }

    private bool DidProgressReachTarget(GDictionary eventData)
    {
        StringName questId = ReadRequiredStringName(eventData, "quest_id");
        StringName objectiveId = ReadRequiredStringName(eventData, "objective_id");
        if (questId == "" || objectiveId == "")
            return false;

        int targetValue = ResolveEventTargetValue(eventData, questId, objectiveId);
        if (targetValue <= 0)
            return false;

        QuestState questState = _party_state.get_active_quest_state(questId);
        return questState != null && questState.is_objective_complete(objectiveId, targetValue);
    }

    private static bool IsValidQuestProgressEvent(GDictionary eventData)
    {
        StringName eventType = ReadRequiredStringName(eventData, "event_type");
        if (eventType != EVENT_ACCEPT && eventType != EVENT_PROGRESS && eventType != EVENT_COMPLETE)
            return false;
        if (
            !eventData.ContainsKey("world_step")
            || eventData["world_step"].VariantType != Variant.Type.Int
        )
            return false;
        if (
            eventData.ContainsKey("allow_reaccept")
            && eventData["allow_reaccept"].VariantType != Variant.Type.Bool
        )
            return false;
        if (
            eventData.ContainsKey("auto_accept")
            && eventData["auto_accept"].VariantType != Variant.Type.Bool
        )
            return false;
        if (
            eventData.ContainsKey("context")
            && eventData["context"].VariantType != Variant.Type.Dictionary
        )
            return false;

        if (eventType == EVENT_ACCEPT || eventType == EVENT_COMPLETE)
            return ReadRequiredStringName(eventData, "quest_id") != "";
        if (eventType == EVENT_PROGRESS)
            return IsValidProgressEvent(eventData);
        return false;
    }

    private static bool IsValidProgressEvent(GDictionary eventData)
    {
        if (ResolveProgressDelta(eventData) <= 0)
            return false;
        if (
            eventData.ContainsKey("target_value")
            && (
                eventData["target_value"].VariantType != Variant.Type.Int
                || eventData["target_value"].AsInt32() <= 0
            )
        )
        {
            return false;
        }

        if (eventData.ContainsKey("quest_id") || eventData.ContainsKey("objective_id"))
        {
            return ReadRequiredStringName(eventData, "quest_id") != ""
                && ReadRequiredStringName(eventData, "objective_id") != "";
        }
        return ReadRequiredStringName(eventData, "objective_type") != ""
            && ReadRequiredStringName(eventData, "target_id") != "";
    }

    private static StringName ReadRequiredStringName(GDictionary eventData, string fieldName)
    {
        if (!eventData.ContainsKey(fieldName))
            return "";

        var value = eventData[fieldName];
        if (value.VariantType == Variant.Type.StringName)
            return value.AsStringName();
        if (value.VariantType == Variant.Type.String)
            return new StringName(value.AsString());
        return "";
    }

    private static int ResolveProgressDelta(GDictionary eventData)
    {
        if (!eventData.ContainsKey("progress_delta"))
            return 0;
        var progressDeltaValue = eventData["progress_delta"];
        if (progressDeltaValue.VariantType != Variant.Type.Int)
            return 0;
        int progressDelta = progressDeltaValue.AsInt32();
        return progressDelta > 0 ? progressDelta : 0;
    }

    private int ResolveEventTargetValue(
        GDictionary eventData,
        StringName questId,
        StringName objectiveId
    )
    {
        if (objectiveId == "")
            return 0;
        if (eventData.ContainsKey("target_value"))
        {
            var targetValueValue = eventData["target_value"];
            if (targetValueValue.VariantType != Variant.Type.Int)
                return 0;
            return Mathf.Max(targetValueValue.AsInt32(), 0);
        }
        return ResolveObjectiveTargetValue(FindObjectiveDef(questId, objectiveId));
    }

    private static int ResolveObjectiveTargetValue(GDictionary objectiveDef)
    {
        if (
            objectiveDef == null
            || objectiveDef.Count == 0
            || !objectiveDef.ContainsKey("target_value")
        )
            return 0;
        var targetValueValue = objectiveDef["target_value"];
        if (targetValueValue.VariantType != Variant.Type.Int)
            return 0;
        return Mathf.Max(targetValueValue.AsInt32(), 0);
    }

    private GDictionary FindObjectiveDef(StringName questId, StringName objectiveId)
    {
        if (questId == "" || objectiveId == "")
            return new GDictionary();

        foreach (var objectiveValue in GetObjectiveDefs(questId))
        {
            if (objectiveValue.VariantType != Variant.Type.Dictionary)
                continue;

            GDictionary objectiveDef = objectiveValue.AsGodotDictionary();
            if (
                GdInterop.GetStringName(objectiveDef, "objective_id") == objectiveId
            )
                return objectiveDef.Duplicate(true);
        }
        return new GDictionary();
    }

    private GArray GetObjectiveDefs(StringName questId)
    {
        if (questId == "" || !_quest_defs.ContainsKey(questId))
            return new GArray();

        var questDef = _quest_defs[questId];
        if (questDef.VariantType == Variant.Type.Dictionary)
        {
            GDictionary questDefDict = questDef.AsGodotDictionary();
            return
                questDefDict.ContainsKey("objective_defs")
                && questDefDict["objective_defs"].VariantType == Variant.Type.Array
                ? questDefDict["objective_defs"].AsGodotArray()
                : new GArray();
        }

        GodotObject questDefObject = questDef.AsGodotObject();
        if (questDefObject == null)
            return new GArray();

        var objectiveDefsValue = questDefObject.Get("objective_defs");
        return objectiveDefsValue.VariantType == Variant.Type.Array
            ? objectiveDefsValue.AsGodotArray()
            : new GArray();
    }

    private Godot.Collections.Array<GDictionary> FindMatchingActiveObjectives(GDictionary eventData)
    {
        Godot.Collections.Array<GDictionary> matches = new();
        StringName objectiveType = GdInterop.GetStringName(eventData, "objective_type");
        StringName targetId = GdInterop.GetStringName(eventData, "target_id");
        if (objectiveType == "")
            return matches;

        foreach (QuestState questState in get_active_quests())
        {
            if (questState == null || questState.quest_id == "")
                continue;
            if (!_quest_defs.ContainsKey(questState.quest_id))
                continue;

            foreach (var objectiveValue in GetObjectiveDefs(questState.quest_id))
            {
                if (objectiveValue.VariantType != Variant.Type.Dictionary)
                    continue;

                GDictionary objectiveDef = objectiveValue.AsGodotDictionary();
                if (GdInterop.GetStringName(objectiveDef, "objective_type") != objectiveType)
                    continue;

                StringName objectiveTargetId = GdInterop.GetStringName(objectiveDef, "target_id");
                if (objectiveTargetId != "" && targetId != "" && objectiveTargetId != targetId)
                    continue;
                if (objectiveTargetId != "" && targetId == "")
                    continue;

                matches.Add(
                    new GDictionary
                    {
                        ["quest_state"] = questState,
                        ["objective_def"] = objectiveDef.Duplicate(true),
                    }
                );
            }
        }
        return matches;
    }

    private GStringNameArray MaybeCompleteQuestsAfterProgress(
        GDictionary eventData,
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

    private static GDictionary BuildEventContext(GDictionary eventData)
    {
        GDictionary context = new();
        if (
            eventData.ContainsKey("context")
            && eventData["context"].VariantType == Variant.Type.Dictionary
        )
            context = eventData["context"].AsGodotDictionary().Duplicate(true);

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
            if (eventData.ContainsKey(key))
                context[key] = eventData[key];
        }
        return context;
    }

    private static void AppendUniqueStringName(GStringNameArray target, StringName value)
    {
        if (target == null || value == "" || target.Contains(value))
            return;
        target.Add(value);
    }

    private QuestDef GetQuestDefObject(StringName questId)
    {
        if (questId == "" || !_quest_defs.ContainsKey(questId))
            return null;
        return _quest_defs[questId].AsGodotObject() as QuestDef;
    }

    private int GetWorldStep()
    {
        if (_party_state != null && _party_state.HasMethod("get_world_step"))
            return _party_state.Call("get_world_step").AsInt32();
        return 0;
    }
}
