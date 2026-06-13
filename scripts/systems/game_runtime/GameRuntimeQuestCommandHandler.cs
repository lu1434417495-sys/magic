using System;
using System.Collections.Generic;
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

    public void Dispose()
    {
        _runtime = null;
    }

    internal GameRuntimeFacade.RuntimeCommandResult CommandAcceptQuestTyped(
        StringName questId,
        bool allowReaccept = false
    )
    {
        if (!HasRuntime())
            return RuntimeUnavailableTypedResult();
        var characterManagement = GetCharacterManagement();
        if (characterManagement == null)
            return CommandErrorTyped("运行时尚未初始化。");
        if (questId == "")
            return CommandErrorTyped("任务 ID 不能为空。");
        QuestCommandDefData questDef = GetQuestCommandDefData(questId);
        if (!questDef.Exists)
            return GameRuntimeFacade.RuntimeCommandResult.Failure(
                string.Format("未找到任务 {0}。", questId),
                GameRuntimeFacade.RuntimeCommandCode.NotFound
            );
        string questLabel = questDef.DisplayName;
        if (string.IsNullOrEmpty(questLabel))
            return InvalidQuestDisplayNameTypedError();
        var partyState = GetPartyState();
        if (partyState != null && partyState.HasActiveQuest(questId))
            return CommandErrorTyped(
                string.Format("任务《{0}》已在进行中，不能重复接取。", questLabel)
            );
        if (partyState != null && partyState.HasClaimableQuest(questId))
            return CommandErrorTyped(
                string.Format("任务《{0}》已完成，奖励待领取，当前不可再次接取。", questLabel)
            );
        var hasCompleted = partyState != null && partyState.HasCompletedQuest(questId);
        var isRepeatable = questDef.IsRepeatable;
        var effectiveAllowReaccept = allowReaccept || (hasCompleted && isRepeatable);
        if (hasCompleted && !effectiveAllowReaccept)
            return CommandErrorTyped(
                string.Format("任务《{0}》已完成，当前不可再次接取。", questLabel)
            );
        if (!characterManagement.AcceptQuest(questId, GetWorldStep(), effectiveAllowReaccept))
            return CommandErrorTyped(string.Format("当前无法接取任务《{0}》。", questLabel));
        SetPartyState(characterManagement.GetPartyState());
        var persistError = PersistPartyState();
        var message =
            hasCompleted && effectiveAllowReaccept
                ? string.Format("已重新接取任务《{0}》。", questLabel)
                : string.Format("已接取任务《{0}》。", questLabel);
        if (persistError != Error.Ok)
        {
            message = string.Format("{0} 但队伍状态持久化失败。", message);
            UpdateStatus(message);
            return GameRuntimeFacade.RuntimeCommandResult.Failure(
                message,
                GameRuntimeFacade.RuntimeCommandCode.PersistenceFailure
            );
        }
        UpdateStatus(message);
        return CommandOkTyped(message);
    }

    internal GameRuntimeFacade.RuntimeCommandResult CommandProgressQuestTyped(
        StringName questId,
        StringName objectiveId,
        int progressDelta = 1,
        Dictionary payload = null
    ) =>
        CommandProgressQuestTyped(
            questId,
            objectiveId,
            progressDelta,
            QuestProgressCommandPayloadData.FromDictionary(payload, GetWorldStep())
        );

    internal GameRuntimeFacade.RuntimeCommandResult CommandProgressQuestTyped(
        StringName questId,
        StringName objectiveId,
        int progressDelta,
        QuestProgressCommandPayloadData progressPayload
    )
    {
        if (!HasRuntime())
            return RuntimeUnavailableTypedResult();
        if (GetCharacterManagement() == null)
            return CommandErrorTyped("运行时尚未初始化。");
        if (questId == "" || objectiveId == "")
            return CommandErrorTyped("任务 ID 和目标 ID 不能为空。");
        QuestCommandDefData questDef = GetQuestCommandDefData(questId);
        if (!questDef.Exists)
            return GameRuntimeFacade.RuntimeCommandResult.Failure(
                string.Format("未找到任务 {0}。", questId),
                GameRuntimeFacade.RuntimeCommandCode.NotFound
            );
        string questLabel = questDef.DisplayName;
        if (string.IsNullOrEmpty(questLabel))
            return InvalidQuestDisplayNameTypedError();
        var characterManagement = GetCharacterManagement();
        if (!progressPayload.IsValid)
            return CommandErrorTyped(
                string.Format("当前无法推进任务《{0}》的目标 {1}。", questLabel, objectiveId)
            );
        QuestProgressApplyResultData progressSummary =
            characterManagement.ApplyDirectQuestProgressTyped(
                questId,
                objectiveId,
                Mathf.Max(progressDelta, 0),
                progressPayload.WorldStep,
                progressPayload.HasTargetValue,
                progressPayload.TargetValue,
                progressPayload.BuildContextData()
            );
        SetPartyState(characterManagement.GetPartyState());
        bool hasProgressed = progressSummary.ContainsProgressedQuest(questId);
        if (!hasProgressed)
            return CommandErrorTyped(
                string.Format("当前无法推进任务《{0}》的目标 {1}。", questLabel, objectiveId)
            );
        var persistError = PersistPartyState();
        var message = string.Format("已推进任务《{0}》的目标 {1}。", questLabel, objectiveId);
        if (persistError != Error.Ok)
        {
            message = string.Format("{0} 但队伍状态持久化失败。", message);
            UpdateStatus(message);
            return GameRuntimeFacade.RuntimeCommandResult.Failure(
                message,
                GameRuntimeFacade.RuntimeCommandCode.PersistenceFailure
            );
        }
        UpdateStatus(message);
        return CommandOkTyped(message);
    }

    internal GameRuntimeFacade.RuntimeCommandResult CommandCompleteQuestTyped(StringName questId)
    {
        if (!HasRuntime())
            return RuntimeUnavailableTypedResult();
        var characterManagement = GetCharacterManagement();
        if (characterManagement == null)
            return CommandErrorTyped("运行时尚未初始化。");
        if (questId == "")
            return CommandErrorTyped("任务 ID 不能为空。");
        QuestCommandDefData questDef = GetQuestCommandDefData(questId);
        if (!questDef.Exists)
            return GameRuntimeFacade.RuntimeCommandResult.Failure(
                string.Format("未找到任务 {0}。", questId),
                GameRuntimeFacade.RuntimeCommandCode.NotFound
            );
        string questLabel = questDef.DisplayName;
        if (string.IsNullOrEmpty(questLabel))
            return InvalidQuestDisplayNameTypedError();
        if (!characterManagement.CompleteQuest(questId, GetWorldStep()))
            return CommandErrorTyped(string.Format("当前无法完成任务《{0}》。", questLabel));
        SetPartyState(characterManagement.GetPartyState());
        var persistError = PersistPartyState();
        var message = string.Format("已完成任务《{0}》，奖励待领取。", questLabel);
        if (persistError != Error.Ok)
        {
            message = string.Format("{0} 但队伍状态持久化失败。", message);
            UpdateStatus(message);
            return GameRuntimeFacade.RuntimeCommandResult.Failure(
                message,
                GameRuntimeFacade.RuntimeCommandCode.PersistenceFailure
            );
        }
        UpdateStatus(message);
        return CommandOkTyped(message);
    }

    internal GameRuntimeFacade.RuntimeCommandResult CommandSubmitQuestItemTyped(
        StringName questId,
        StringName objectiveId = default
    )
    {
        if (!HasRuntime())
            return RuntimeUnavailableTypedResult();
        var characterManagement = GetCharacterManagement();
        if (characterManagement == null)
            return CommandErrorTyped("运行时尚未初始化。");
        if (questId == "")
            return CommandErrorTyped("任务 ID 不能为空。");
        QuestCommandDefData questDef = GetQuestCommandDefData(questId);
        if (!questDef.Exists)
            return GameRuntimeFacade.RuntimeCommandResult.Failure(
                string.Format("未找到任务 {0}。", questId),
                GameRuntimeFacade.RuntimeCommandCode.NotFound
            );
        string questLabel = questDef.DisplayName;
        if (string.IsNullOrEmpty(questLabel))
            return InvalidQuestDisplayNameTypedError();
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
                    return CommandErrorTyped("任务 ID 不能为空。");
                case "quest_not_active":
                    return CommandErrorTyped(string.Format("当前没有进行中的任务《{0}》。", questLabel));
                case "quest_def_missing":
                    return CommandErrorTyped(
                        string.Format("任务《{0}》缺少目标配置，当前无法提交。", questLabel)
                    );
                case "invalid_submit_item_objective":
                    return CommandErrorTyped(
                        string.Format(
                            "任务《{0}》包含无效的物资提交目标，当前无法提交。",
                            questLabel
                        )
                    );
                case "objective_already_complete":
                    return CommandErrorTyped(
                        string.Format("任务《{0}》的物资目标已完成，无需重复提交。", questLabel)
                    );
                case "submit_item_missing_inventory":
                    return CommandErrorTyped(
                        string.Format(
                            "共享仓库缺少{0} x{1}，无法提交给任务《{2}》。",
                            missingItemLabel,
                            requiredQuantity,
                            questLabel
                        )
                    );
                case "submit_item_commit_failed":
                    return CommandErrorTyped(
                        string.Format("当前无法从共享仓库扣除任务《{0}》所需物资。", questLabel)
                    );
                case "quest_progress_failed":
                    return CommandErrorTyped(
                        string.Format("共享仓库扣除已回滚，当前无法推进任务《{0}》。", questLabel)
                    );
                default:
                    return CommandErrorTyped(
                        string.Format("任务《{0}》当前没有可提交的物资目标。", questLabel)
                    );
            }
        }
        SetPartyState(characterManagement.GetPartyState());
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
            return GameRuntimeFacade.RuntimeCommandResult.Failure(
                message,
                GameRuntimeFacade.RuntimeCommandCode.PersistenceFailure
            );
        }
        UpdateStatus(message);
        return CommandOkTyped(message);
    }

    internal GameRuntimeFacade.RuntimeCommandResult CommandClaimQuestTyped(StringName questId)
    {
        if (!HasRuntime())
            return RuntimeUnavailableTypedResult();
        var characterManagement = GetCharacterManagement();
        if (characterManagement == null)
            return CommandErrorTyped("运行时尚未初始化。");
        if (questId == "")
            return CommandErrorTyped("任务 ID 不能为空。");
        QuestCommandDefData questDef = GetQuestCommandDefData(questId);
        if (!questDef.Exists)
            return GameRuntimeFacade.RuntimeCommandResult.Failure(
                string.Format("未找到任务 {0}。", questId),
                GameRuntimeFacade.RuntimeCommandCode.NotFound
            );
        string questLabel = questDef.DisplayName;
        if (string.IsNullOrEmpty(questLabel))
            return InvalidQuestDisplayNameTypedError();
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
                    return CommandErrorTyped(
                        string.Format("当前没有可领取的任务《{0}》奖励。", questLabel)
                    );
                case "quest_def_missing":
                    return CommandErrorTyped(
                        string.Format("任务《{0}》缺少奖励配置，当前无法领取。", questLabel)
                    );
                case "invalid_quest_display_name":
                    return InvalidQuestDisplayNameTypedError();
                case "invalid_gold_amount":
                    return CommandErrorTyped(
                        string.Format(
                            "任务《{0}》包含无效的金币奖励配置，当前无法领取。",
                            questLabel
                        )
                    );
                case "invalid_item_reward":
                    return CommandErrorTyped(
                        string.Format(
                            "任务《{0}》包含无效的物品奖励配置，当前无法领取。",
                            questLabel
                        )
                    );
                case "invalid_item_display_name":
                    return CommandErrorTyped(
                        string.Format(
                            "任务《{0}》引用的物品奖励缺少 display_name，当前无法领取。",
                            questLabel
                        )
                    );
                case "invalid_pending_character_reward":
                    return CommandErrorTyped(
                        string.Format(
                            "任务《{0}》包含无效的角色奖励配置，当前无法领取。",
                            questLabel
                        )
                    );
                case "item_reward_missing_def":
                    return CommandErrorTyped(
                        string.Format(
                            "任务《{0}》引用了缺失的物品奖励配置，当前无法领取。",
                            questLabel
                        )
                    );
                case "reward_overflow":
                    return CommandErrorTyped(
                        string.Format(
                            "共享仓库空间不足，领取任务《{0}》奖励会溢出，当前无法领取。",
                            questLabel
                        )
                    );
                case "quest_reward_commit_failed":
                    return CommandErrorTyped(
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
                    return CommandErrorTyped(
                        string.Format(
                            "任务《{0}》包含暂不支持的奖励类型：{1}。",
                            questLabel,
                            unsupportedText
                        )
                    );
                default:
                    return CommandErrorTyped(string.Format("当前无法领取任务《{0}》奖励。", questLabel));
            }
        }
        SetPartyState(characterManagement.GetPartyState());
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
            return GameRuntimeFacade.RuntimeCommandResult.Failure(
                message,
                GameRuntimeFacade.RuntimeCommandCode.PersistenceFailure
            );
        }
        UpdateStatus(message);
        return CommandOkTyped(message);
    }

    private bool HasRuntime()
    {
        return _runtime != null;
    }

    private GameRuntimeFacade.RuntimeCommandResult CommandOkTyped(string message = "")
    {
        return GameRuntimeFacade.RuntimeCommandResult.Success(message ?? "");
    }

    private GameRuntimeFacade.RuntimeCommandResult CommandErrorTyped(string message)
    {
        return GameRuntimeFacade.RuntimeCommandResult.Failure(
            message ?? "",
            GameRuntimeFacade.RuntimeCommandCode.InvalidState
        );
    }

    private GameRuntimeFacade.RuntimeCommandResult RuntimeUnavailableTypedResult()
    {
        return GameRuntimeFacade.RuntimeCommandResult.Failure(
            RuntimeUnavailableMessage,
            GameRuntimeFacade.RuntimeCommandCode.RuntimeUnavailable
        );
    }

    private GameRuntimeFacade.RuntimeCommandResult InvalidQuestDisplayNameTypedError()
    {
        return CommandErrorTyped(InvalidQuestDisplayNameMessage);
    }

    private CharacterManagementModule GetCharacterManagement()
    {
        return HasRuntime() ? _runtime.GetCharacterManagement() : null;
    }

    private PartyState GetPartyState()
    {
        return HasRuntime() ? _runtime.GetPartyState() : null;
    }

    private void SetPartyState(PartyState partyState)
    {
        if (HasRuntime())
            _runtime.SetPartyState(partyState);
    }

    private int GetWorldStep()
    {
        return HasRuntime() ? _runtime.GetWorldStep() : 0;
    }

    private Error PersistPartyState()
    {
        return HasRuntime()
            ? (Error)_runtime.PersistPartyState()
            : Error.Unavailable;
    }

    private void UpdateStatus(string message)
    {
        if (HasRuntime())
            _runtime.UpdateStatus(message);
    }

    private string GetItemDisplayName(StringName itemId)
    {
        return HasRuntime()
            ? _runtime.GetItemDisplayName(itemId)
            : itemId.ToString();
    }

    private QuestCommandDefData GetQuestCommandDefData(StringName questId) =>
        QuestCommandDefData.FromQuestDef(HasRuntime() ? _runtime.GetQuestDef(questId) : null);

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

    internal static QuestCommandDefData FromQuestDef(QuestDef questDef)
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

internal sealed class QuestProgressCommandPayloadData
{
    public bool IsValid { get; }
    public int WorldStep { get; }
    public bool HasTargetValue { get; }
    public int TargetValue { get; }
    public string ActionId { get; }
    public StringName MemberId { get; }
    public StringName EnemyTemplateId { get; }
    public string SettlementId { get; }
    public StringName SourceType { get; }
    public StringName SourceId { get; }

    private QuestProgressCommandPayloadData(
        bool isValid,
        int worldStep,
        bool hasTargetValue,
        int targetValue,
        string actionId,
        StringName memberId,
        StringName enemyTemplateId,
        string settlementId,
        StringName sourceType,
        StringName sourceId
    )
    {
        IsValid = isValid;
        WorldStep = worldStep;
        HasTargetValue = hasTargetValue;
        TargetValue = targetValue;
        ActionId = actionId ?? "";
        MemberId = memberId;
        EnemyTemplateId = enemyTemplateId;
        SettlementId = settlementId ?? "";
        SourceType = sourceType;
        SourceId = sourceId;
    }

    internal static QuestProgressCommandPayloadData FromDictionary(
        Dictionary payload,
        int defaultWorldStep
    )
    {
        if (payload == null || payload.Count == 0)
        {
            return new QuestProgressCommandPayloadData(
                defaultWorldStep >= 0,
                defaultWorldStep,
                false,
                0,
                "",
                "",
                "",
                "",
                "",
                ""
            );
        }

        int worldStep = defaultWorldStep;
        if (payload.ContainsKey("world_step"))
        {
            if (!TryReadInt(payload, "world_step", out worldStep))
                return Invalid();
        }
        if (worldStep < 0)
            return Invalid();

        bool hasTargetValue = payload.ContainsKey("target_value");
        int targetValue = 0;
        if (hasTargetValue)
        {
            if (!TryReadInt(payload, "target_value", out targetValue))
                return Invalid();
            if (targetValue <= 0)
                return Invalid();
        }

        if (payload.ContainsKey("action_id") && !IsStringField(payload, "action_id"))
            return Invalid();
        if (payload.ContainsKey("settlement_id") && !IsStringField(payload, "settlement_id"))
            return Invalid();
        if (payload.ContainsKey("member_id") && !IsStringField(payload, "member_id"))
            return Invalid();
        if (payload.ContainsKey("enemy_template_id") && !IsStringField(payload, "enemy_template_id"))
            return Invalid();
        if (payload.ContainsKey("source_type") && !IsStringField(payload, "source_type"))
            return Invalid();
        if (payload.ContainsKey("source_id") && !IsStringField(payload, "source_id"))
            return Invalid();

        return new QuestProgressCommandPayloadData(
            true,
            worldStep,
            hasTargetValue,
            targetValue,
            QuestCommandDataReader.ReadString(payload, "action_id"),
            QuestCommandDataReader.ReadStringName(payload, "member_id"),
            QuestCommandDataReader.ReadStringName(payload, "enemy_template_id"),
            QuestCommandDataReader.ReadString(payload, "settlement_id"),
            QuestCommandDataReader.ReadStringName(payload, "source_type"),
            QuestCommandDataReader.ReadStringName(payload, "source_id")
        );
    }

    internal static QuestProgressCommandPayloadData FromNamedArgs(
        IReadOnlyDictionary<string, string> namedArgs,
        int defaultWorldStep
    )
    {
        if (namedArgs == null || namedArgs.Count == 0)
        {
            return new QuestProgressCommandPayloadData(
                defaultWorldStep >= 0,
                defaultWorldStep,
                false,
                0,
                "",
                "",
                "",
                "",
                "",
                ""
            );
        }

        int worldStep = defaultWorldStep;
        if (namedArgs.TryGetValue("world_step", out string worldStepText))
        {
            if (!TryParseExactInt(worldStepText, out worldStep))
                return Invalid();
        }
        if (worldStep < 0)
            return Invalid();

        bool hasTargetValue = namedArgs.ContainsKey("target_value");
        int targetValue = 0;
        if (hasTargetValue)
        {
            if (!TryParseExactInt(namedArgs["target_value"], out targetValue))
                return Invalid();
            if (targetValue <= 0)
                return Invalid();
        }

        return new QuestProgressCommandPayloadData(
            true,
            worldStep,
            hasTargetValue,
            targetValue,
            ReadNamedArg(namedArgs, "action_id"),
            new StringName(ReadNamedArg(namedArgs, "member_id")),
            new StringName(ReadNamedArg(namedArgs, "enemy_template_id")),
            ReadNamedArg(namedArgs, "settlement_id"),
            new StringName(ReadNamedArg(namedArgs, "source_type")),
            new StringName(ReadNamedArg(namedArgs, "source_id"))
        );
    }

    internal QuestProgressEventContextData BuildContextData() =>
        new()
        {
            ActionId = ActionId,
            MemberId = MemberId,
            EnemyTemplateId = EnemyTemplateId,
            SettlementId = SettlementId,
            SourceType = SourceType,
            SourceId = SourceId,
        };

    private static QuestProgressCommandPayloadData Invalid() =>
        new(false, -1, false, 0, "", "", "", "", "", "");

    private static string ReadNamedArg(IReadOnlyDictionary<string, string> namedArgs, string key) =>
        namedArgs != null && namedArgs.TryGetValue(key, out string value) ? value ?? "" : "";

    private static bool TryParseExactInt(string text, out int result) =>
        int.TryParse(text ?? "", out result);

    private static bool TryReadInt(Dictionary data, string key, out int result)
    {
        if (!QuestCommandDataReader.TryGetValue(data, key, out Variant value))
        {
            result = 0;
            return false;
        }
        if (value.VariantType != Variant.Type.Int)
        {
            result = 0;
            return false;
        }
        result = value.AsInt32();
        return true;
    }

    private static bool IsStringField(Dictionary data, string key)
    {
        if (!QuestCommandDataReader.TryGetValue(data, key, out Variant value))
            return false;
        return value.VariantType == Variant.Type.String;
    }
}

internal static class QuestCommandDataReader
{
    internal static int ReadInt(GDictionary data, string key)
    {
        if (!TryGet(data, key, out Variant value))
            return 0;
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : 0;
    }

    internal static string ReadString(GDictionary data, string key)
    {
        if (!TryGet(data, key, out Variant value))
            return "";
        return value.VariantType switch
        {
            Variant.Type.String => value.AsString(),
            _ => "",
        };
    }

    internal static string ReadTrimmedString(GDictionary data, string key) =>
        ReadString(data, key).Trim();

    internal static StringName ReadStringName(GDictionary data, string key)
    {
        if (!TryGet(data, key, out Variant value))
            return "";
        return value.VariantType switch
        {
            Variant.Type.String => new StringName(value.AsString()),
            _ => new StringName(""),
        };
    }

    internal static GArray ReadArray(GDictionary data, string key)
    {
        if (!TryGet(data, key, out Variant value))
            return new GArray();
        return value.VariantType == Variant.Type.Array ? value.AsGodotArray() : new GArray();
    }

    internal static bool ContainsStringName(GArray values, StringName target)
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

    internal static System.Collections.Generic.IEnumerable<GDictionary> ReadDictionaryItems(
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

    private static bool TryGet(GDictionary data, string key, out Variant value)
    {
        value = default;
        if (data == null || string.IsNullOrEmpty(key))
        {
            return false;
        }
        if (data.ContainsKey(key))
        {
            value = data[key];
            return true;
        }
        return false;
    }

    internal static bool TryGetValue(GDictionary data, string key, out Variant value) =>
        TryGet(data, key, out value);
}
