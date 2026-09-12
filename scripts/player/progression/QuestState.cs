using Godot;
using System;
using System.Collections.Generic;

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
        "failed_at_world_step",
        "failure_reason_id",
        "last_progress_context",
    };

    public StringName quest_id = "";

    public StringName status_id { get; private set; } = StatusInactive;

    public QuestObjectiveProgressState objective_progress = new();

    public int accepted_at_world_step = -1;

    public int completed_at_world_step = -1;

    public int reward_claimed_at_world_step = -1;

    public int failed_at_world_step = -1;

    public StringName failure_reason_id { get; private set; } = "";

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

    public bool HasCompletedAllObjectives(QuestDefinition questDef)
    {
        if (questDef == null)
            return false;

        foreach (QuestObjectiveDefinition objective in questDef.Objectives)
        {
            if (
                objective == null
                || objective.ObjectiveId == ""
                || !IsObjectiveComplete(objective.ObjectiveId, objective.TargetValue)
            )
                return false;
        }

        return true;
    }

    public void MarkAccepted(int worldStep = -1)
    {
        status_id = StatusActive;
        accepted_at_world_step = worldStep;
        failed_at_world_step = -1;
        failure_reason_id = "";

        if (completed_at_world_step < accepted_at_world_step)
            completed_at_world_step = -1;

        if (reward_claimed_at_world_step < accepted_at_world_step)
            reward_claimed_at_world_step = -1;
    }

    public void MarkCompleted(int worldStep = -1)
    {
        status_id = StatusCompleted;
        completed_at_world_step = worldStep;
        failed_at_world_step = -1;
        failure_reason_id = "";
    }

    public void MarkRewardClaimed(int worldStep = -1)
    {
        status_id = StatusRewarded;
        reward_claimed_at_world_step = worldStep;
        failed_at_world_step = -1;
        failure_reason_id = "";
    }

    internal bool MarkFailed(
        int worldStep,
        StringName reasonId,
        QuestProgressContext context = null
    )
    {
        if (!IsActive() || reasonId == "" || worldStep < -1)
            return false;
        status_id = StatusFailed;
        completed_at_world_step = -1;
        reward_claimed_at_world_step = -1;
        failed_at_world_step = worldStep;
        failure_reason_id = reasonId;
        last_progress_context = context?.DuplicateState() ?? QuestProgressContext.Empty();
        return true;
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
            failed_at_world_step = failed_at_world_step,
            failure_reason_id = failure_reason_id,
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
            { "failed_at_world_step", failed_at_world_step },
            { "failure_reason_id", (string)failure_reason_id },
            { "last_progress_context", last_progress_context.ToDictionary() },
        };
    }

    internal Dictionary<string, object> BuildSnapshotPlain()
    {
        var objectiveProgress = new Dictionary<string, object>(StringComparer.Ordinal);
        if (objective_progress != null)
        {
            var entries = new List<KeyValuePair<StringName, int>>(
                objective_progress.ValuesTyped
            );
            entries.Sort(
                (left, right) =>
                    string.CompareOrdinal(left.Key.ToString(), right.Key.ToString())
            );
            foreach ((StringName objectiveId, int value) in entries)
                objectiveProgress[objectiveId.ToString()] = value;
        }

        var progressContext = new Dictionary<string, object>(StringComparer.Ordinal);
        QuestProgressContext context = last_progress_context;
        if (context != null)
        {
            if (context.MemberId != "")
                progressContext["member_id"] = context.MemberId.ToString();
            if (!string.IsNullOrEmpty(context.ActionId))
                progressContext["action_id"] = context.ActionId;
            if (context.EnemyTemplateId != "")
                progressContext["enemy_template_id"] = context.EnemyTemplateId.ToString();
            if (!string.IsNullOrEmpty(context.SettlementId))
                progressContext["settlement_id"] = context.SettlementId;
            if (context.SourceType != "")
                progressContext["source_type"] = context.SourceType.ToString();
            if (context.SourceId != "")
                progressContext["source_id"] = context.SourceId.ToString();
            if (context.ItemId != "")
                progressContext["item_id"] = context.ItemId.ToString();
            if (context.SubmittedQuantity > 0)
                progressContext["submitted_quantity"] = context.SubmittedQuantity;
        }

        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["quest_id"] = quest_id.ToString(),
            ["status_id"] = status_id.ToString(),
            ["objective_progress"] = objectiveProgress,
            ["accepted_at_world_step"] = accepted_at_world_step,
            ["completed_at_world_step"] = completed_at_world_step,
            ["reward_claimed_at_world_step"] = reward_claimed_at_world_step,
            ["failed_at_world_step"] = failed_at_world_step,
            ["failure_reason_id"] = failure_reason_id.ToString(),
            ["last_progress_context"] = progressContext,
        };
    }

    public static QuestState FromDictionary(Godot.Collections.Dictionary payload) =>
        FromDictionary(payload, out _);

    /// <paramref name="failureReason"/> 说明是哪个字段让解码失败（成功时为空）。
    public static QuestState FromDictionary(
        Godot.Collections.Dictionary payload,
        out string failureReason
    )
    {
        failureReason = DecodeInto(payload, out QuestState result);
        return result;
    }

    /// 返回空字符串表示解码成功；否则返回失败字段的说明。
    private static string DecodeInto(
        Godot.Collections.Dictionary payload,
        out QuestState result
    )
    {
        result = null;
        if (payload == null)
            return "quest: payload is null";

        if (!_has_exact_serialized_fields(payload))
            return "quest: field set does not match the current schema";

        var objProgVar = payload["objective_progress"];

        var ctxVar = payload["last_progress_context"];

        if (objProgVar.VariantType != Variant.Type.Dictionary)
            return "objective_progress: expected Dictionary, got " + objProgVar.VariantType;

        if (ctxVar.VariantType != Variant.Type.Dictionary)
            return "last_progress_context: expected Dictionary, got " + ctxVar.VariantType;

        var questId = _read_required_string_name(payload["quest_id"]);

        var statusId = _read_required_string_name(payload["status_id"]);

        if (questId == "")
            return "quest_id: must not be empty";
        if (!_is_valid_status_id(statusId))
            return $"status_id: '{statusId}' is not a known quest status";

        foreach (string stepField in WorldStepFields)
        {
            if (payload[stepField].VariantType != Variant.Type.Int)
                return $"{stepField}: expected Int, got {payload[stepField].VariantType}";
            if (payload[stepField].AsInt32() < -1)
                return $"{stepField}: {payload[stepField].AsInt32()} must be >= -1";
        }

        if (
            !_try_read_optional_string_name(
                payload["failure_reason_id"],
                out StringName failureReasonId
            )
        )
            return "failure_reason_id: not an optional string name";

        QuestObjectiveProgressState objProgValues;
        try
        {
            objProgValues = QuestObjectiveProgressState.FromDictionary(
                objProgVar.AsGodotDictionary()
            );
        }
        catch (ArgumentException exception)
        {
            return $"objective_progress: {exception.Message}";
        }

        QuestProgressContext progressContext;
        try
        {
            progressContext = QuestProgressContext.FromDictionary(
                ctxVar.AsGodotDictionary()
            );
        }
        catch (ArgumentException exception)
        {
            return $"last_progress_context: {exception.Message}";
        }

        int acceptedAt = payload["accepted_at_world_step"].AsInt32();

        int completedAt = payload["completed_at_world_step"].AsInt32();

        int rewardAt = payload["reward_claimed_at_world_step"].AsInt32();

        int failedAt = payload["failed_at_world_step"].AsInt32();

        QuestStatusKind statusKind = ToStatusKind(statusId);
        if (statusKind == QuestStatusKind.Inactive)
        {
            if (
                acceptedAt != -1
                || completedAt != -1
                || rewardAt != -1
                || failedAt != -1
                || failureReasonId != ""
            )
                return "status_id=inactive: world-step and failure fields must all be unset";
        }
        else if (statusKind == QuestStatusKind.Active)
        {
            if (
                completedAt != -1
                || rewardAt != -1
                || failedAt != -1
                || failureReasonId != ""
            )
                return "status_id=active: completion/reward/failure fields must all be unset";
        }
        else if (statusKind == QuestStatusKind.Completed)
        {
            if (rewardAt != -1 || failedAt != -1 || failureReasonId != "")
                return "status_id=completed: reward and failure fields must be unset";
        }
        else if (statusKind == QuestStatusKind.Rewarded)
        {
            if (
                completedAt < 0
                || rewardAt < 0
                || failedAt != -1
                || failureReasonId != ""
            )
                return "status_id=rewarded: needs completed/reward steps and no failure fields";
        }
        else if (statusKind == QuestStatusKind.Failed)
        {
            if (completedAt != -1 || rewardAt != -1 || failureReasonId == "")
                return "status_id=failed: needs failure_reason_id and no completion/reward steps";
        }

        result = new QuestState
        {
            quest_id = questId,
            status_id = statusId,
            objective_progress = objProgValues,
            accepted_at_world_step = acceptedAt,
            completed_at_world_step = completedAt,
            reward_claimed_at_world_step = rewardAt,
            failed_at_world_step = failedAt,
            failure_reason_id = failureReasonId,
            last_progress_context = progressContext,
        };
        return "";
    }

    private static readonly string[] WorldStepFields =
    {
        "accepted_at_world_step",
        "completed_at_world_step",
        "reward_claimed_at_world_step",
        "failed_at_world_step",
    };

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

    private static bool _try_read_optional_string_name(
        object rawValue,
        out StringName result
    )
    {
        result = "";
        if (rawValue is not Variant value)
            return false;
        if (
            value.VariantType != Variant.Type.String
            && value.VariantType != Variant.Type.StringName
        )
            return false;
        result = new StringName(value.AsString().StripEdges());
        return true;
    }

    private static bool _is_valid_status_id(StringName statusId)
    {
        return ToStatusKind(statusId) != QuestStatusKind.Unknown;
    }
}
