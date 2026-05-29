using Godot;

[GlobalClass]
public partial class QuestState : RefCounted
{
    public static readonly StringName STATUS_INACTIVE = "inactive";

    public static readonly StringName STATUS_ACTIVE = "active";

    public static readonly StringName STATUS_COMPLETED = "completed";

    public static readonly StringName STATUS_REWARDED = "rewarded";

    public static readonly StringName STATUS_FAILED = "failed";

    private static readonly Godot.Collections.Array REQUIRED_SERIALIZED_FIELDS = new()
    {
        "quest_id",
        "status_id",
        "objective_progress",
        "accepted_at_world_step",
        "completed_at_world_step",
        "reward_claimed_at_world_step",
        "last_progress_context",
    };

    public StringName quest_id = "";

    public StringName status_id = STATUS_INACTIVE;

    public Godot.Collections.Dictionary objective_progress = new();

    public int accepted_at_world_step = -1;

    public int completed_at_world_step = -1;

    public int reward_claimed_at_world_step = -1;

    public Godot.Collections.Dictionary last_progress_context = new();

    public bool is_active() => status_id == STATUS_ACTIVE;

    public bool is_completed() => status_id == STATUS_COMPLETED || status_id == STATUS_REWARDED;

    public bool is_terminal() => status_id == STATUS_REWARDED || status_id == STATUS_FAILED;

    public int get_objective_progress(StringName objectiveId)
    {
        return Mathf.Max(
            objective_progress.ContainsKey(objectiveId)
                ? (int)(long)objective_progress[objectiveId]
                : 0,
            0
        );
    }

    public int record_objective_progress(
        StringName objectiveId,
        int delta,
        int targetValue = 0,
        Godot.Collections.Dictionary context = null
    )
    {
        if (objectiveId == "" || delta <= 0 || targetValue <= 0 || !is_active())
            return get_objective_progress(objectiveId);

        int nextValue = Mathf.Min(get_objective_progress(objectiveId) + delta, targetValue);

        objective_progress[objectiveId] = nextValue;

        last_progress_context = context?.Duplicate(true) ?? new Godot.Collections.Dictionary();

        return nextValue;
    }

    public int record_objective_progress(StringName objectiveId, int delta)
    {
        return record_objective_progress(objectiveId, delta, 0, null);
    }

    public bool is_objective_complete(StringName objectiveId, int targetValue = 0)
    {
        return objectiveId != ""
            && targetValue > 0
            && get_objective_progress(objectiveId) >= targetValue;
    }

    public bool is_objective_complete(StringName objectiveId)
    {
        return is_objective_complete(objectiveId, 0);
    }

    public bool has_completed_all_objectives(QuestDef questDef)
    {
        if (questDef == null)
            return false;

        foreach (var objData in questDef.objective_defs)
        {
            var objId = ProgressionDataUtils.to_string_name(
                objData.ContainsKey("objective_id") ? objData["objective_id"] : default
            );

            if (
                !objData.ContainsKey("target_value")
                || objData["target_value"].VariantType != Variant.Type.Int
            )
                return false;

            int target = objData["target_value"].AsInt32();

            if (objId == "" || !is_objective_complete(objId, target))
                return false;
        }

        return true;
    }

    public void mark_accepted(int worldStep = -1)
    {
        status_id = STATUS_ACTIVE;
        accepted_at_world_step = worldStep;

        if (completed_at_world_step < accepted_at_world_step)
            completed_at_world_step = -1;

        if (reward_claimed_at_world_step < accepted_at_world_step)
            reward_claimed_at_world_step = -1;
    }

    public void mark_completed(int worldStep = -1)
    {
        status_id = STATUS_COMPLETED;
        completed_at_world_step = worldStep;
    }

    public void mark_reward_claimed(int worldStep = -1)
    {
        status_id = STATUS_REWARDED;
        reward_claimed_at_world_step = worldStep;
    }

    public void mark_failed()
    {
        status_id = STATUS_FAILED;
    }

    public Godot.Collections.Dictionary to_dict()
    {
        return new Godot.Collections.Dictionary
        {
            { "quest_id", (string)quest_id },
            { "status_id", (string)status_id },
            {
                "objective_progress",
                ProgressionDataUtils.string_name_int_map_to_string_dict(objective_progress)
            },
            { "accepted_at_world_step", accepted_at_world_step },
            { "completed_at_world_step", completed_at_world_step },
            { "reward_claimed_at_world_step", reward_claimed_at_world_step },
            { "last_progress_context", last_progress_context.Duplicate(true) },
        };
    }

    public static QuestState from_dict(Godot.Collections.Dictionary payload)
    {
        if (payload == null)
            return null;

        if (!_has_exact_serialized_fields(payload))
            return null;

        var objProgVar = payload["objective_progress"];

        var ctxVar = payload["last_progress_context"];

        if (objProgVar.VariantType != Variant.Type.Dictionary)
            return null;

        if (ctxVar.VariantType != Variant.Type.Dictionary)
            return null;

        var questId = _read_required_string_name(payload["quest_id"]);

        var statusId = _read_required_string_name(payload["status_id"]);

        if (questId == "" || !_is_valid_status_id(statusId))
            return null;

        if (
            payload["accepted_at_world_step"].VariantType != Variant.Type.Int
            || payload["accepted_at_world_step"].AsInt32() < -1
            || payload["completed_at_world_step"].VariantType != Variant.Type.Int
            || payload["completed_at_world_step"].AsInt32() < -1
            || payload["reward_claimed_at_world_step"].VariantType != Variant.Type.Int
            || payload["reward_claimed_at_world_step"].AsInt32() < -1
        )
            return null;

        var objProgValues = new Godot.Collections.Dictionary();

        foreach (var objIdValue in objProgVar.AsGodotDictionary().Keys)
        {
            var objId = _read_required_string_name(objIdValue);

            if (objId == "")
                return null;

            var progVar = objProgVar.AsGodotDictionary()[objIdValue];

            if (progVar.VariantType != Variant.Type.Int || progVar.AsInt32() < 0)
                return null;

            objProgValues[objId] = (long)progVar;
        }

        int acceptedAt = payload["accepted_at_world_step"].AsInt32();

        int completedAt = payload["completed_at_world_step"].AsInt32();

        int rewardAt = payload["reward_claimed_at_world_step"].AsInt32();

        if (statusId == STATUS_INACTIVE)
        {
            if (acceptedAt != -1 || completedAt != -1 || rewardAt != -1)
                return null;
        }
        else if (statusId == STATUS_ACTIVE)
        {
            if (completedAt != -1 || rewardAt != -1)
                return null;
        }
        else if (statusId == STATUS_COMPLETED)
        {
            if (rewardAt != -1)
                return null;
        }
        else if (statusId == STATUS_REWARDED)
        {
            if (completedAt < 0 || rewardAt < 0)
                return null;
        }
        else if (statusId == STATUS_FAILED)
        {
            if (rewardAt != -1)
                return null;
        }

        return new QuestState
        {
            quest_id = questId,
            status_id = statusId,
            objective_progress = objProgValues,
            accepted_at_world_step = acceptedAt,
            completed_at_world_step = completedAt,
            reward_claimed_at_world_step = rewardAt,
            last_progress_context = ctxVar.AsGodotDictionary().Duplicate(true),
        };
    }

    private static bool _has_exact_serialized_fields(Godot.Collections.Dictionary payload)
    {
        if (payload.Count != REQUIRED_SERIALIZED_FIELDS.Count)
            return false;
        foreach (var fn in REQUIRED_SERIALIZED_FIELDS)
        {
            if (!payload.ContainsKey(fn))
                return false;
        }
        return true;
    }

    private static StringName _read_required_string_name(object rawValue)
    {
        if (rawValue is not Variant value)
        {
            return new StringName("");
        }
        if (value.VariantType != Variant.Type.String && value.VariantType != Variant.Type.StringName)
            return new StringName("");

        string text = value.AsString().StripEdges();

        return text.Length > 0 ? new StringName(text) : new StringName("");
    }

    private static bool _is_valid_status_id(StringName statusId)
    {
        return statusId == STATUS_INACTIVE
            || statusId == STATUS_ACTIVE
            || statusId == STATUS_COMPLETED
            || statusId == STATUS_REWARDED
            || statusId == STATUS_FAILED;
    }
}
