using System;
using Godot;
using Godot.Collections;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public sealed class GameRuntimeQuestCommandHandler
{
    private static readonly StringName RuntimeUnavailableMessage = "运行时尚未初始化。";
    private static readonly StringName InvalidQuestDisplayNameMessage =
        "任务配置缺少 display_name，当前无法执行命令。";

    private WeakReference<GameRuntimeFacade> _runtimeRef;

    private GameRuntimeFacade _runtime
    {
        get => ResolveWeakRef(_runtimeRef);
        set => _runtimeRef = value != null ? new WeakReference<GameRuntimeFacade>(value) : null;
    }

    public void Setup(GameRuntimeFacade runtime)
    {
        _runtime = runtime;
    }

    public void setup(GameRuntimeFacade runtime)
    {
        Setup(runtime);
    }

    public void Dispose()
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
        QuestCommandDefData questDef = GetQuestCommandDefData(questId);
        if (!questDef.Exists)
            return CommandError(string.Format("未找到任务 {0}。", questId));
        string questLabel = questDef.DisplayName;
        if (string.IsNullOrEmpty(questLabel))
            return InvalidQuestDisplayNameError();
        var partyState = GetPartyState();
        if (partyState != null && partyState.has_active_quest(questId))
            return CommandError(string.Format("任务《{0}》已在进行中，不能重复接取。", questLabel));
        if (partyState != null && partyState.has_claimable_quest(questId))
            return CommandError(
                string.Format("任务《{0}》已完成，奖励待领取，当前不可再次接取。", questLabel)
            );
        var hasCompleted = partyState != null && partyState.has_completed_quest(questId);
        var isRepeatable = questDef.IsRepeatable;
        var effectiveAllowReaccept = allowReaccept || (hasCompleted && isRepeatable);
        if (hasCompleted && !effectiveAllowReaccept)
            return CommandError(string.Format("任务《{0}》已完成，当前不可再次接取。", questLabel));
        if (!characterManagement.accept_quest(questId, GetWorldStep(), effectiveAllowReaccept))
            return CommandError(string.Format("当前无法接取任务《{0}》。", questLabel));
        SetPartyState(characterManagement.get_party_state());
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
        QuestCommandDefData questDef = GetQuestCommandDefData(questId);
        if (!questDef.Exists)
            return CommandError(string.Format("未找到任务 {0}。", questId));
        string questLabel = questDef.DisplayName;
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
        QuestProgressSummaryData progressSummary = QuestProgressSummaryData.FromDictionary(summary);
        bool hasProgressed = progressSummary.ContainsProgressedQuest(questId);
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
        QuestCommandDefData questDef = GetQuestCommandDefData(questId);
        if (!questDef.Exists)
            return CommandError(string.Format("未找到任务 {0}。", questId));
        string questLabel = questDef.DisplayName;
        if (string.IsNullOrEmpty(questLabel))
            return InvalidQuestDisplayNameError();
        if (!characterManagement.complete_quest(questId, GetWorldStep()))
            return CommandError(string.Format("当前无法完成任务《{0}》。", questLabel));
        SetPartyState(characterManagement.get_party_state());
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
        QuestCommandDefData questDef = GetQuestCommandDefData(questId);
        if (!questDef.Exists)
            return CommandError(string.Format("未找到任务 {0}。", questId));
        string questLabel = questDef.DisplayName;
        if (string.IsNullOrEmpty(questLabel))
            return InvalidQuestDisplayNameError();
        QuestSubmitItemResultData submitData = characterManagement.SubmitItemObjectiveTyped(
            questId,
            objectiveId,
            GetWorldStep()
        );
        if (!submitData.Ok)
        {
            var missingItemId = submitData.ItemId;
            var missingItemLabel = GetItemDisplayName(missingItemId);
            var requiredQuantity = Mathf.Max(submitData.RequiredQuantity, 0);
            var errorCode = submitData.ErrorCode;
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
        SetPartyState(characterManagement.get_party_state());
        var itemId = submitData.ItemId;
        var itemLabel = GetItemDisplayName(itemId);
        var submittedQuantity = Mathf.Max(submitData.SubmittedQuantity, 0);
        var message = string.Format(
            "已为任务《{0}》提交 {1} x{2}。",
            questLabel,
            itemLabel,
            submittedQuantity
        );
        if (submitData.ContainsClaimableQuest(questId))
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
        result["objective_id"] = submitData.ObjectiveId;
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
        QuestCommandDefData questDef = GetQuestCommandDefData(questId);
        if (!questDef.Exists)
            return CommandError(string.Format("未找到任务 {0}。", questId));
        string questLabel = questDef.DisplayName;
        if (string.IsNullOrEmpty(questLabel))
            return InvalidQuestDisplayNameError();
        QuestClaimResultData claimData = characterManagement.ClaimQuestRewardTyped(
            questId,
            GetWorldStep()
        );
        if (!claimData.Ok)
        {
            var errorCode = claimData.ErrorCode;
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
                        claimData.CloneUnsupportedRewardTypes()
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
        SetPartyState(characterManagement.get_party_state());
        var persistError = PersistPartyState();
        var goldDelta = claimData.GoldDelta;
        var rewardSummary = claimData.BuildRewardSummaryText();
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
        result["item_rewards"] = claimData.CloneItemRewards();
        result["pending_character_rewards"] = claimData.ClonePendingCharacterRewards();
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
        return _runtime.build_command_ok(message);
    }

    private Dictionary CommandError(string message)
    {
        if (!HasRuntime())
            return new Dictionary { ["ok"] = false, ["message"] = message };
        return _runtime.build_command_error(message);
    }

    private Dictionary RuntimeUnavailableError()
    {
        return new Dictionary { ["ok"] = false, ["message"] = RuntimeUnavailableMessage };
    }

    private Dictionary InvalidQuestDisplayNameError()
    {
        return CommandError(InvalidQuestDisplayNameMessage);
    }

    private CharacterManagementModule GetCharacterManagement()
    {
        return HasRuntime() ? _runtime.get_character_management() : null;
    }

    private PartyState GetPartyState()
    {
        return HasRuntime() ? _runtime.get_party_state() : null;
    }

    private void SetPartyState(PartyState partyState)
    {
        if (HasRuntime())
            _runtime.set_party_state(partyState);
    }

    private int GetWorldStep()
    {
        return HasRuntime() ? _runtime.get_world_step() : 0;
    }

    private Error PersistPartyState()
    {
        return HasRuntime()
            ? (Error)_runtime.persist_party_state()
            : Error.Unavailable;
    }

    private void UpdateStatus(string message)
    {
        if (HasRuntime())
            _runtime.update_status(message);
    }

    private string GetItemDisplayName(StringName itemId)
    {
        return HasRuntime()
            ? _runtime.get_item_display_name(itemId)
            : itemId.ToString();
    }

    private Dictionary ApplyQuestProgressEventsToParty(
        Godot.Collections.Array eventOptions,
        string sourceDomain = "quest"
    )
    {
        return HasRuntime()
            ? _runtime.apply_quest_progress_events_to_party(eventOptions, sourceDomain)
            : new Dictionary();
    }

    private QuestCommandDefData GetQuestCommandDefData(StringName questId) =>
        QuestCommandDefData.FromQuestDef(HasRuntime() ? _runtime._get_quest_def(questId) : null);

    private Godot.Collections.Array<String> StringNameArrayToStringArray(
        Godot.Collections.Array<StringName> values
    )
    {
        var result = new Godot.Collections.Array<String>();
        if (values == null)
            return result;
        foreach (var value in values)
            result.Add(value.ToString());
        return result;
    }

    private static GameRuntimeFacade ResolveWeakRef(WeakReference<GameRuntimeFacade> weakRef)
    {
        if (weakRef == null || !weakRef.TryGetTarget(out GameRuntimeFacade target))
            return null;
        return target;
    }
}

internal sealed class QuestCommandDefData
{
    public readonly bool Exists;
    public readonly string DisplayName;
    public readonly bool IsRepeatable;

    private QuestCommandDefData(bool exists, string displayName, bool isRepeatable)
    {
        Exists = exists;
        DisplayName = displayName ?? "";
        IsRepeatable = isRepeatable;
    }

    public static QuestCommandDefData FromQuestDef(QuestDef questDef)
    {
        if (questDef == null || questDef.quest_id == "")
            return new QuestCommandDefData(false, "", false);
        return new QuestCommandDefData(
            true,
            questDef.display_name?.Trim() ?? "",
            questDef.is_repeatable
        );
    }
}

internal sealed class QuestProgressSummaryData
{
    private readonly GArray _progressedQuestIds;

    private QuestProgressSummaryData(GArray progressedQuestIds)
    {
        _progressedQuestIds = progressedQuestIds != null
            ? progressedQuestIds.Duplicate(true)
            : new GArray();
    }

    public bool ContainsProgressedQuest(StringName questId) =>
        QuestCommandDataReader.ContainsStringName(_progressedQuestIds, questId);

    public static QuestProgressSummaryData FromDictionary(GDictionary data) =>
        new(QuestCommandDataReader.ReadArray(data, "progressed_quest_ids"));
}

internal static class QuestCommandDataReader
{
    public static int ReadInt(GDictionary data, object key)
    {
        if (!TryGet(data, key, out Variant value))
            return 0;
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : 0;
    }

    public static string ReadString(GDictionary data, object key)
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

    public static string ReadTrimmedString(GDictionary data, object key) =>
        ReadString(data, key).Trim();

    public static StringName ReadStringName(GDictionary data, object key)
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

    public static GArray ReadArray(GDictionary data, object key)
    {
        if (!TryGet(data, key, out Variant value))
            return new GArray();
        return value.VariantType == Variant.Type.Array ? value.AsGodotArray() : new GArray();
    }

    public static bool ContainsStringName(GArray values, StringName target)
    {
        if (values == null || target == "")
            return false;
        foreach (Variant value in values)
        {
            if (value.VariantType == Variant.Type.StringName && value.AsStringName() == target)
                return true;
            if (value.VariantType == Variant.Type.String && new StringName(value.AsString()) == target)
                return true;
        }
        return false;
    }

    public static System.Collections.Generic.IEnumerable<GDictionary> ReadDictionaryItems(
        GArray values
    )
    {
        if (values == null)
            yield break;
        foreach (Variant value in values)
        {
            if (value.VariantType == Variant.Type.Dictionary)
                yield return value.AsGodotDictionary();
        }
    }

    private static bool TryGet(GDictionary data, object key, out Variant value)
    {
        if (data == null)
        {
            value = default;
            return false;
        }
        Variant variantKey = key switch
        {
            Variant valueKey => valueKey,
            string stringKey => stringKey,
            StringName stringNameKey => stringNameKey,
            _ => default,
        };
        if (data.ContainsKey(variantKey))
        {
            value = data[variantKey];
            return true;
        }
        if (variantKey.VariantType == Variant.Type.String)
        {
            var stringNameKey = new StringName(variantKey.AsString());
            if (data.ContainsKey(stringNameKey))
            {
                value = data[stringNameKey];
                return true;
            }
        }
        else if (variantKey.VariantType == Variant.Type.StringName)
        {
            string stringKey = variantKey.AsStringName().ToString();
            if (data.ContainsKey(stringKey))
            {
                value = data[stringKey];
                return true;
            }
        }
        value = default;
        return false;
    }
}
