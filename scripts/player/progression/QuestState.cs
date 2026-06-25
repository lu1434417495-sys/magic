using Godot;
using System;

internal enum QuestStatusKind
{
    Unknown = 0,
    Inactive,
    Active,
    Completed,
    Rewarded,
    Failed,
}

public class QuestState
{
    private static readonly StringName StatusInactive = "inactive";

    private static readonly StringName StatusActive = "active";

    private static readonly StringName StatusCompleted = "completed";

    private static readonly StringName StatusRewarded = "rewarded";

    private static readonly StringName StatusFailed = "failed";

    private static readonly string[] REQUIRED_SERIALIZED_FIELDS =
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

    public StringName status_id = StatusInactive;

    public QuestObjectiveProgressState objective_progress = new();

    public int accepted_at_world_step = -1;

    public int completed_at_world_step = -1;

    public int reward_claimed_at_world_step = -1;

    public QuestProgressContext last_progress_context { get; private set; } =
        QuestProgressContext.Empty();

    public bool IsActive() => status_id == StatusActive;

    public bool IsCompleted() => status_id == StatusCompleted || status_id == StatusRewarded;

    public bool IsTerminal() => status_id == StatusRewarded || status_id == StatusFailed;

    internal static StringName ToStringName(QuestStatusKind kind)
    {
        return kind switch
        {
            QuestStatusKind.Inactive => StatusInactive,
            QuestStatusKind.Active => StatusActive,
            QuestStatusKind.Completed => StatusCompleted,
            QuestStatusKind.Rewarded => StatusRewarded,
            QuestStatusKind.Failed => StatusFailed,
            _ => "",
        };
    }

    internal static QuestStatusKind ToStatusKind(StringName statusId)
    {
        if (statusId == StatusInactive)
            return QuestStatusKind.Inactive;
        if (statusId == StatusActive)
            return QuestStatusKind.Active;
        if (statusId == StatusCompleted)
            return QuestStatusKind.Completed;
        if (statusId == StatusRewarded)
            return QuestStatusKind.Rewarded;
        if (statusId == StatusFailed)
            return QuestStatusKind.Failed;
        return QuestStatusKind.Unknown;
    }

    public int GetObjectiveProgress(StringName objectiveId)
    {
        return objective_progress.Get(objectiveId);
    }

    public int RecordObjectiveProgress(
        StringName objectiveId,
        int delta,
        int targetValue = 0,
        QuestProgressContext context = null
    )
    {
        if (objectiveId == "" || delta <= 0 || targetValue <= 0 || !IsActive())
            return GetObjectiveProgress(objectiveId);

        int nextValue = Mathf.Min(GetObjectiveProgress(objectiveId) + delta, targetValue);

        objective_progress.Set(objectiveId, nextValue);

        last_progress_context = context?.DuplicateState() ?? QuestProgressContext.Empty();

        return nextValue;
    }

    public int RecordObjectiveProgress(StringName objectiveId, int delta)
    {
        return RecordObjectiveProgress(objectiveId, delta, 0, null);
    }

    public bool IsObjectiveComplete(StringName objectiveId, int targetValue = 0)
    {
        return objectiveId != ""
            && targetValue > 0
            && GetObjectiveProgress(objectiveId) >= targetValue;
    }

    public bool IsObjectiveComplete(StringName objectiveId)
    {
        return IsObjectiveComplete(objectiveId, 0);
    }

    public bool HasCompletedAllObjectives(QuestDef questDef)
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

            if (objId == "" || !IsObjectiveComplete(objId, target))
                return false;
        }

        return true;
    }

    public void MarkAccepted(int worldStep = -1)
    {
        status_id = StatusActive;
        accepted_at_world_step = worldStep;

        if (completed_at_world_step < accepted_at_world_step)
            completed_at_world_step = -1;

        if (reward_claimed_at_world_step < accepted_at_world_step)
            reward_claimed_at_world_step = -1;
    }

    public void MarkCompleted(int worldStep = -1)
    {
        status_id = StatusCompleted;
        completed_at_world_step = worldStep;
    }

    public void MarkRewardClaimed(int worldStep = -1)
    {
        status_id = StatusRewarded;
        reward_claimed_at_world_step = worldStep;
    }

    public void MarkFailed()
    {
        status_id = StatusFailed;
    }

    public QuestState DuplicateState()
    {
        return new QuestState
        {
            quest_id = quest_id,
            status_id = status_id,
            objective_progress =
                objective_progress?.DuplicateState() ?? new QuestObjectiveProgressState(),
            accepted_at_world_step = accepted_at_world_step,
            completed_at_world_step = completed_at_world_step,
            reward_claimed_at_world_step = reward_claimed_at_world_step,
            last_progress_context =
                last_progress_context?.DuplicateState() ?? QuestProgressContext.Empty(),
        };
    }

    public Godot.Collections.Dictionary ToDictionary()
    {
        return new Godot.Collections.Dictionary
        {
            { "quest_id", (string)quest_id },
            { "status_id", (string)status_id },
            {
                "objective_progress",
                objective_progress.ToDictionary()
            },
            { "accepted_at_world_step", accepted_at_world_step },
            { "completed_at_world_step", completed_at_world_step },
            { "reward_claimed_at_world_step", reward_claimed_at_world_step },
            { "last_progress_context", last_progress_context.ToDictionary() },
        };
    }

    public static QuestState FromDictionary(Godot.Collections.Dictionary payload)
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

        QuestObjectiveProgressState objProgValues;
        try
        {
            objProgValues = QuestObjectiveProgressState.FromDictionary(
                objProgVar.AsGodotDictionary()
            );
        }
        catch (ArgumentException)
        {
            return null;
        }

        int acceptedAt = payload["accepted_at_world_step"].AsInt32();

        int completedAt = payload["completed_at_world_step"].AsInt32();

        int rewardAt = payload["reward_claimed_at_world_step"].AsInt32();

        QuestStatusKind statusKind = ToStatusKind(statusId);
        if (statusKind == QuestStatusKind.Inactive)
        {
            if (acceptedAt != -1 || completedAt != -1 || rewardAt != -1)
                return null;
        }
        else if (statusKind == QuestStatusKind.Active)
        {
            if (completedAt != -1 || rewardAt != -1)
                return null;
        }
        else if (statusKind == QuestStatusKind.Completed)
        {
            if (rewardAt != -1)
                return null;
        }
        else if (statusKind == QuestStatusKind.Rewarded)
        {
            if (completedAt < 0 || rewardAt < 0)
                return null;
        }
        else if (statusKind == QuestStatusKind.Failed)
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
            last_progress_context = QuestProgressContext.FromDictionary(
                ctxVar.AsGodotDictionary()
            ),
        };
    }

    private static bool _has_exact_serialized_fields(Godot.Collections.Dictionary payload)
    {
        if (payload.Count != REQUIRED_SERIALIZED_FIELDS.Length)
            return false;
        foreach (string fn in REQUIRED_SERIALIZED_FIELDS)
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
        return ToStatusKind(statusId) != QuestStatusKind.Unknown;
    }
}
