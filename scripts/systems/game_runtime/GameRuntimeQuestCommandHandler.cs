using System;
using Godot;
using Godot.Collections;

[GlobalClass]
public partial class GameRuntimeQuestCommandHandler : RefCounted
{
    private static readonly StringName RuntimeUnavailableMessage = "运行时尚未初始化。";
    private static readonly StringName InvalidQuestDisplayNameMessage =
        "任务配置缺少 display_name，当前无法执行命令。";

    private WeakReference<GodotObject> _runtimeRef;

    private GodotObject _runtime
    {
        get => ResolveWeakRef(_runtimeRef);
        set => _runtimeRef = value != null ? new WeakReference<GodotObject>(value) : null;
    }

    public void Setup(GodotObject runtime)
    {
        _runtime = runtime;
    }

    public void setup(GodotObject runtime)
    {
        Setup(runtime);
    }

    public new void Dispose()
    {
        _runtime = null;
    }

    public void dispose()
    {
        Dispose();
    }

    public Dictionary command_accept_quest(StringName questId, bool allowReaccept) =>
        CommandAcceptQuest(questId, allowReaccept);

    public Dictionary command_progress_quest(
        StringName questId,
        StringName objectiveId,
        int progressDelta,
        Dictionary payload
    ) => CommandProgressQuest(questId, objectiveId, progressDelta, payload);

    public Dictionary command_complete_quest(StringName questId) => CommandCompleteQuest(questId);

    public Dictionary command_submit_quest_item(StringName questId, StringName objectiveId) =>
        CommandSubmitQuestItem(questId, objectiveId);

    public Dictionary command_claim_quest(StringName questId) => CommandClaimQuest(questId);

    public Dictionary CommandAcceptQuest(StringName questId, bool allowReaccept = false)
    {
        if (!HasRuntime())
            return RuntimeUnavailableError();
        var characterManagement = GetCharacterManagement();
        if (characterManagement == null)
            return CommandError("运行时尚未初始化。");
        if (questId == "")
            return CommandError("任务 ID 不能为空。");
        var questData = GetQuestDefData(questId);
        if (questData == null || questData.Count == 0)
            return CommandError(string.Format("未找到任务 {0}。", questId));
        var questLabel = ResolveQuestLabel(questId, questData);
        if (string.IsNullOrEmpty(questLabel))
            return InvalidQuestDisplayNameError();
        var partyState = GetPartyState();
        if (partyState != null && partyState.Call("has_active_quest", questId).AsBool())
            return CommandError(string.Format("任务《{0}》已在进行中，不能重复接取。", questLabel));
        if (partyState != null && partyState.Call("has_claimable_quest", questId).AsBool())
            return CommandError(
                string.Format("任务《{0}》已完成，奖励待领取，当前不可再次接取。", questLabel)
            );
        var hasCompleted =
            partyState != null && partyState.Call("has_completed_quest", questId).AsBool();
        var isRepeatable = GdInterop.GetBool(questData, "is_repeatable");
        var effectiveAllowReaccept = allowReaccept || (hasCompleted && isRepeatable);
        if (hasCompleted && !effectiveAllowReaccept)
            return CommandError(string.Format("任务《{0}》已完成，当前不可再次接取。", questLabel));
        if (
            !characterManagement
                .Call("accept_quest", questId, GetWorldStep(), effectiveAllowReaccept)
                .AsBool()
        )
            return CommandError(string.Format("当前无法接取任务《{0}》。", questLabel));
        SetPartyState(characterManagement.Call("get_party_state").AsGodotObject());
        var persistError = PersistPartyState();
        var message =
            hasCompleted && effectiveAllowReaccept
                ? string.Format("已重新接取任务《{0}》。", questLabel)
                : string.Format("已接取任务《{0}》。", questLabel);
        if (persistError != Error.Ok)
        {
            message = string.Format("{0} 但队伍状态持久化失败。", message);
            UpdateStatus(message);
            return CommandError(message);
        }
        UpdateStatus(message);
        return CommandOk(message);
    }

    public Dictionary CommandProgressQuest(
        StringName questId,
        StringName objectiveId,
        int progressDelta = 1,
        Dictionary payload = null
    )
    {
        if (!HasRuntime())
            return RuntimeUnavailableError();
        if (GetCharacterManagement() == null)
            return CommandError("运行时尚未初始化。");
        if (questId == "" || objectiveId == "")
            return CommandError("任务 ID 和目标 ID 不能为空。");
        var questData = GetQuestDefData(questId);
        if (questData == null || questData.Count == 0)
            return CommandError(string.Format("未找到任务 {0}。", questId));
        var questLabel = ResolveQuestLabel(questId, questData);
        if (string.IsNullOrEmpty(questLabel))
            return InvalidQuestDisplayNameError();
        var eventData = new Dictionary
        {
            ["event_type"] = "progress",
            ["quest_id"] = questId.ToString(),
            ["objective_id"] = objectiveId.ToString(),
            ["progress_delta"] = Mathf.Max(progressDelta, 0),
            ["world_step"] = GetWorldStep(),
        };
        if (payload != null)
        {
            foreach (var key in payload.Keys)
                eventData[key] = payload[key];
        }
        var summary = ApplyQuestProgressEventsToParty(
            new Godot.Collections.Array { eventData },
            "quest"
        );
        var progressedQuestIds = GdInterop.GetArray(summary, "progressed_quest_ids");
        bool hasProgressed = false;
        foreach (var id in progressedQuestIds)
        {
            if (id.AsStringName() == questId)
            {
                hasProgressed = true;
                break;
            }
        }
        if (!hasProgressed)
            return CommandError(
                string.Format("当前无法推进任务《{0}》的目标 {1}。", questLabel, objectiveId)
            );
        var persistError = PersistPartyState();
        var message = string.Format("已推进任务《{0}》的目标 {1}。", questLabel, objectiveId);
        if (persistError != Error.Ok)
        {
            message = string.Format("{0} 但队伍状态持久化失败。", message);
            UpdateStatus(message);
            return CommandError(message);
        }
        UpdateStatus(message);
        return CommandOk(message);
    }

    public Dictionary CommandCompleteQuest(StringName questId)
    {
        if (!HasRuntime())
            return RuntimeUnavailableError();
        var characterManagement = GetCharacterManagement();
        if (characterManagement == null)
            return CommandError("运行时尚未初始化。");
        if (questId == "")
            return CommandError("任务 ID 不能为空。");
        var questData = GetQuestDefData(questId);
        if (questData == null || questData.Count == 0)
            return CommandError(string.Format("未找到任务 {0}。", questId));
        var questLabel = ResolveQuestLabel(questId, questData);
        if (string.IsNullOrEmpty(questLabel))
            return InvalidQuestDisplayNameError();
        if (!characterManagement.Call("complete_quest", questId, GetWorldStep()).AsBool())
            return CommandError(string.Format("当前无法完成任务《{0}》。", questLabel));
        SetPartyState(characterManagement.Call("get_party_state").AsGodotObject());
        var persistError = PersistPartyState();
        var message = string.Format("已完成任务《{0}》，奖励待领取。", questLabel);
        if (persistError != Error.Ok)
        {
            message = string.Format("{0} 但队伍状态持久化失败。", message);
            UpdateStatus(message);
            return CommandError(message);
        }
        UpdateStatus(message);
        return CommandOk(message);
    }

    public Dictionary CommandSubmitQuestItem(StringName questId, StringName objectiveId = default)
    {
        if (!HasRuntime())
            return RuntimeUnavailableError();
        var characterManagement = GetCharacterManagement();
        if (characterManagement == null)
            return CommandError("运行时尚未初始化。");
        if (questId == "")
            return CommandError("任务 ID 不能为空。");
        var questData = GetQuestDefData(questId);
        if (questData == null || questData.Count == 0)
            return CommandError(string.Format("未找到任务 {0}。", questId));
        var questLabel = ResolveQuestLabel(questId, questData);
        if (string.IsNullOrEmpty(questLabel))
            return InvalidQuestDisplayNameError();
        var submitResult = characterManagement
            .Call("submit_item_objective", questId, objectiveId, GetWorldStep())
            .AsGodotDictionary();
        if (!GdInterop.GetBool(submitResult, "ok"))
        {
            var missingItemId = GdInterop.GetStringName(submitResult, "item_id");
            var missingItemLabel = GetItemDisplayName(missingItemId);
            var requiredQuantity = Mathf.Max(
                GdInterop.GetInt(submitResult, "required_quantity"),
                0
            );
            var errorCode = GdInterop.GetString(submitResult, "error_code");
            switch (errorCode)
            {
                case "invalid_quest_id":
                    return CommandError("任务 ID 不能为空。");
                case "quest_not_active":
                    return CommandError(string.Format("当前没有进行中的任务《{0}》。", questLabel));
                case "quest_def_missing":
                    return CommandError(
                        string.Format("任务《{0}》缺少目标配置，当前无法提交。", questLabel)
                    );
                case "invalid_submit_item_objective":
                    return CommandError(
                        string.Format(
                            "任务《{0}》包含无效的物资提交目标，当前无法提交。",
                            questLabel
                        )
                    );
                case "objective_already_complete":
                    return CommandError(
                        string.Format("任务《{0}》的物资目标已完成，无需重复提交。", questLabel)
                    );
                case "submit_item_missing_inventory":
                    return CommandError(
                        string.Format(
                            "共享仓库缺少{0} x{1}，无法提交给任务《{2}》。",
                            missingItemLabel,
                            requiredQuantity,
                            questLabel
                        )
                    );
                case "submit_item_commit_failed":
                    return CommandError(
                        string.Format("当前无法从共享仓库扣除任务《{0}》所需物资。", questLabel)
                    );
                case "quest_progress_failed":
                    return CommandError(
                        string.Format("共享仓库扣除已回滚，当前无法推进任务《{0}》。", questLabel)
                    );
                default:
                    return CommandError(
                        string.Format("任务《{0}》当前没有可提交的物资目标。", questLabel)
                    );
            }
        }
        SetPartyState(characterManagement.Call("get_party_state").AsGodotObject());
        var itemId = GdInterop.GetStringName(submitResult, "item_id");
        var itemLabel = GetItemDisplayName(itemId);
        var submittedQuantity = Mathf.Max(
            GdInterop.GetInt(submitResult, "submitted_quantity"),
            0
        );
        var claimableQuestIds = GdInterop.GetArray(submitResult, "claimable_quest_ids");
        var message = string.Format(
            "已为任务《{0}》提交 {1} x{2}。",
            questLabel,
            itemLabel,
            submittedQuantity
        );
        if (claimableQuestIds.Contains(questId))
            message = string.Format(
                "已为任务《{0}》提交 {1} x{2}，奖励待领取。",
                questLabel,
                itemLabel,
                submittedQuantity
            );
        var persistError = PersistPartyState();
        if (persistError != Error.Ok)
        {
            message = string.Format("{0} 但队伍状态持久化失败。", message);
            UpdateStatus(message);
            return CommandError(message);
        }
        UpdateStatus(message);
        var result = CommandOk(message);
        result["objective_id"] = GdInterop.GetString(submitResult, "objective_id");
        result["item_id"] = itemId.ToString();
        result["submitted_quantity"] = submittedQuantity;
        return result;
    }

    public Dictionary CommandClaimQuest(StringName questId)
    {
        if (!HasRuntime())
            return RuntimeUnavailableError();
        var characterManagement = GetCharacterManagement();
        if (characterManagement == null)
            return CommandError("运行时尚未初始化。");
        if (questId == "")
            return CommandError("任务 ID 不能为空。");
        var questData = GetQuestDefData(questId);
        if (questData == null || questData.Count == 0)
            return CommandError(string.Format("未找到任务 {0}。", questId));
        var questLabel = ResolveQuestLabel(questId, questData);
        if (string.IsNullOrEmpty(questLabel))
            return InvalidQuestDisplayNameError();
        var claimResult = characterManagement
            .Call("claim_quest_reward", questId, GetWorldStep())
            .AsGodotDictionary();
        if (!GdInterop.GetBool(claimResult, "ok"))
        {
            var errorCode = GdInterop.GetString(claimResult, "error_code");
            switch (errorCode)
            {
                case "quest_not_claimable":
                    return CommandError(
                        string.Format("当前没有可领取的任务《{0}》奖励。", questLabel)
                    );
                case "quest_def_missing":
                    return CommandError(
                        string.Format("任务《{0}》缺少奖励配置，当前无法领取。", questLabel)
                    );
                case "invalid_quest_display_name":
                    return InvalidQuestDisplayNameError();
                case "invalid_gold_amount":
                    return CommandError(
                        string.Format(
                            "任务《{0}》包含无效的金币奖励配置，当前无法领取。",
                            questLabel
                        )
                    );
                case "invalid_item_reward":
                    return CommandError(
                        string.Format(
                            "任务《{0}》包含无效的物品奖励配置，当前无法领取。",
                            questLabel
                        )
                    );
                case "invalid_item_display_name":
                    return CommandError(
                        string.Format(
                            "任务《{0}》引用的物品奖励缺少 display_name，当前无法领取。",
                            questLabel
                        )
                    );
                case "invalid_pending_character_reward":
                    return CommandError(
                        string.Format(
                            "任务《{0}》包含无效的角色奖励配置，当前无法领取。",
                            questLabel
                        )
                    );
                case "item_reward_missing_def":
                    return CommandError(
                        string.Format(
                            "任务《{0}》引用了缺失的物品奖励配置，当前无法领取。",
                            questLabel
                        )
                    );
                case "reward_overflow":
                    return CommandError(
                        string.Format(
                            "共享仓库空间不足，领取任务《{0}》奖励会溢出，当前无法领取。",
                            questLabel
                        )
                    );
                case "quest_reward_commit_failed":
                    return CommandError(
                        string.Format("任务《{0}》奖励写入共享仓库失败，当前无法领取。", questLabel)
                    );
                case "unsupported_reward_types":
                    var unsupportedTypes = StringNameArrayToStringArray(
                        GdInterop.GetArray(claimResult, "unsupported_reward_types")
                    );
                    var unsupportedText =
                        unsupportedTypes.Count > 0
                            ? string.Join("。", unsupportedTypes)
                            : "未知奖励";
                    return CommandError(
                        string.Format(
                            "任务《{0}》包含暂不支持的奖励类型：{1}。",
                            questLabel,
                            unsupportedText
                        )
                    );
                default:
                    return CommandError(string.Format("当前无法领取任务《{0}》奖励。", questLabel));
            }
        }
        SetPartyState(characterManagement.Call("get_party_state").AsGodotObject());
        var persistError = PersistPartyState();
        var goldDelta = GdInterop.GetInt(claimResult, "gold_delta");
        var rewardSummary = BuildQuestClaimRewardSummaryText(claimResult);
        var message = string.Format("已领取任务《{0}》奖励。", questLabel);
        if (!string.IsNullOrEmpty(rewardSummary))
            message = string.Format("已领取任务《{0}》奖励，获得 {1}。", questLabel, rewardSummary);
        if (persistError != Error.Ok)
        {
            message = string.Format("{0} 但队伍状态持久化失败。", message);
            UpdateStatus(message);
            return CommandError(message);
        }
        UpdateStatus(message);
        var result = CommandOk(message);
        result["gold_delta"] = goldDelta;
        result["item_rewards"] = GdInterop.GetArray(claimResult, "item_rewards").Duplicate(true);
        result["pending_character_rewards"] = GdInterop.GetArray(claimResult, "pending_character_rewards").Duplicate(true);
        return result;
    }

    private bool HasRuntime()
    {
        return _runtime != null;
    }

    private Dictionary CommandOk(string message = "")
    {
        if (!HasRuntime())
            return new Dictionary
            {
                ["ok"] = true,
                ["message"] = message,
                ["battle_refresh_mode"] = "",
            };
        return _runtime.Call("build_command_ok", message).AsGodotDictionary();
    }

    private Dictionary CommandError(string message)
    {
        if (!HasRuntime())
            return new Dictionary { ["ok"] = false, ["message"] = message };
        return _runtime.Call("build_command_error", message).AsGodotDictionary();
    }

    private Dictionary RuntimeUnavailableError()
    {
        return new Dictionary { ["ok"] = false, ["message"] = RuntimeUnavailableMessage };
    }

    private Dictionary InvalidQuestDisplayNameError()
    {
        return CommandError(InvalidQuestDisplayNameMessage);
    }

    private GodotObject GetCharacterManagement()
    {
        return HasRuntime() ? _runtime.Call("get_character_management").AsGodotObject() : null;
    }

    private GodotObject GetPartyState()
    {
        return HasRuntime() ? _runtime.Call("get_party_state").AsGodotObject() : null;
    }

    private void SetPartyState(GodotObject partyState)
    {
        if (HasRuntime())
            _runtime.Call("set_party_state", partyState);
    }

    private int GetWorldStep()
    {
        return HasRuntime() ? _runtime.Call("get_world_step").AsInt32() : 0;
    }

    private Error PersistPartyState()
    {
        return HasRuntime()
            ? (Error)_runtime.Call("persist_party_state").AsInt32()
            : Error.Unavailable;
    }

    private void UpdateStatus(string message)
    {
        if (HasRuntime())
            _runtime.Call("update_status", message);
    }

    private string GetItemDisplayName(StringName itemId)
    {
        return HasRuntime()
            ? _runtime.Call("get_item_display_name", itemId).AsString()
            : itemId.ToString();
    }

    private Dictionary ApplyQuestProgressEventsToParty(
        Godot.Collections.Array eventOptions,
        string sourceDomain = "quest"
    )
    {
        return HasRuntime()
            ? _runtime
                .Call("apply_quest_progress_events_to_party", eventOptions, sourceDomain)
                .AsGodotDictionary()
            : new Dictionary();
    }

    private Dictionary GetQuestDefData(StringName questId)
    {
        return HasRuntime()
            ? _runtime.Call("_get_quest_def_data", questId).AsGodotDictionary()
            : new Dictionary();
    }

    private string ResolveQuestLabel(StringName questId, Dictionary questData)
    {
        return HasRuntime()
            ? _runtime.Call("_resolve_quest_label", questId, questData).AsString()
            : "";
    }

    private string BuildQuestClaimRewardSummaryText(Dictionary claimResult)
    {
        return HasRuntime()
            ? _runtime.Call("_build_quest_claim_reward_summary_text", claimResult).AsString()
            : "";
    }

    private Godot.Collections.Array<String> StringNameArrayToStringArray(
        Godot.Collections.Array values
    )
    {
        return HasRuntime()
            ? _runtime.Call("_string_name_array_to_string_array", values).AsGodotArray<String>()
            : new Godot.Collections.Array<String>();
    }

    private static GodotObject ResolveWeakRef(WeakReference<GodotObject> weakRef)
    {
        if (
            weakRef == null
            || !weakRef.TryGetTarget(out GodotObject target)
            || !GodotObject.IsInstanceValid(target)
        )
            return null;
        return target;
    }
}
