using System;
using Godot;

public sealed class GameRuntimeRewardFlowHandler
{
    private static readonly string RuntimeUnavailableMessage = "运行时尚未初始化。";
    private static readonly string InvalidPromotionChoiceMessage =
        "晋升提交无效，当前选择仍需确认。";

    private WeakReference<IGameRuntimeRewardFlowPort> _portRef;

    private IGameRuntimeRewardFlowPort _port
    {
        get => ResolveWeakRef(_portRef);
        set =>
            _portRef =
                value != null ? new WeakReference<IGameRuntimeRewardFlowPort>(value) : null;
    }

    internal void Setup(IGameRuntimeRewardFlowPort port)
    {
        _port = port;
    }

    public void Dispose()
    {
        _port = null;
    }

    internal GameRuntimeFacade.RuntimeCommandResult CommandConfirmPendingRewardTyped()
    {
        if (!HasRuntime())
            return RuntimeUnavailableTypedResult();
        if (GetActiveReward() == null && !PresentPendingRewardIfReady())
            return CommandErrorTyped("当前没有待确认的角色奖励。");
        if (GetActiveReward() == null)
            return CommandErrorTyped("当前没有待确认的角色奖励。");
        OnCharacterRewardConfirmed();
        return CommandOkTyped();
    }

    internal GameRuntimeFacade.RuntimeCommandResult CommandChoosePromotionTyped(
        StringName professionId
    )
    {
        if (!HasRuntime())
            return RuntimeUnavailableTypedResult();
        GameRuntimePromotionPromptContext prompt = _port.GetCurrentPromotionPrompt();
        if (prompt.IsEmpty)
            return CommandErrorTyped("当前没有待确认的职业晋升选择。");
        if (
            prompt.TryGetChoice(
                professionId,
                out GameRuntimePromotionChoiceContext promotionChoice
            )
            && OnPromotionChoiceSubmitted(
                prompt.MemberId,
                promotionChoice.ProfessionId,
                promotionChoice.Selection
            )
        )
            return CommandOkTyped();
        if (promotionChoice != null)
            return CommandErrorTyped(InvalidPromotionChoiceMessage);
        return CommandErrorTyped(string.Format("当前晋升列表中不存在职业 {0}。", professionId));
    }

    internal GameRuntimeFacade.RuntimeCommandResult CommandSubmitPromotionChoiceTyped(
        StringName memberId,
        StringName professionId,
        PromotionSelectionData selection
    )
    {
        if (!HasRuntime())
            return RuntimeUnavailableTypedResult();
        if (!OnPromotionChoiceSubmitted(memberId, professionId, selection))
            return CommandErrorTyped(InvalidPromotionChoiceMessage);
        return CommandOkTyped();
    }

    internal GameRuntimeFacade.RuntimeCommandResult CommandCancelPromotionChoiceTyped()
    {
        if (!HasRuntime())
            return RuntimeUnavailableTypedResult();
        OnPromotionChoiceCancelled();
        return CommandOkTyped();
    }

    internal GameRuntimeFacade.RuntimeCommandResult CommandConfirmActiveRewardTyped()
    {
        if (!HasRuntime())
            return RuntimeUnavailableTypedResult();
        OnCharacterRewardConfirmed();
        return CommandOkTyped();
    }

    internal GameRuntimeFacade.RuntimeCommandResult CommandCloseActiveModalTyped()
    {
        if (!HasRuntime())
            return RuntimeUnavailableTypedResult();
        switch (GetActiveModalKind())
        {
            case RuntimeModalKind.Settlement:
                CloseDismissibleModal(RuntimeModalKind.Settlement);
                return CommandOkTyped();
            case RuntimeModalKind.ContractBoard:
                CloseDismissibleModal(RuntimeModalKind.ContractBoard);
                return CommandOkTyped();
            case RuntimeModalKind.BountyBoard:
                CloseDismissibleModal(RuntimeModalKind.BountyBoard);
                return CommandOkTyped();
            case RuntimeModalKind.NpcQuestOffer:
                CloseDismissibleModal(RuntimeModalKind.NpcQuestOffer);
                return CommandOkTyped();
            case RuntimeModalKind.Shop:
                CloseDismissibleModal(RuntimeModalKind.Shop);
                return CommandOkTyped();
            case RuntimeModalKind.Forge:
                CloseDismissibleModal(RuntimeModalKind.Forge);
                return CommandOkTyped();
            case RuntimeModalKind.Stagecoach:
                CloseDismissibleModal(RuntimeModalKind.Stagecoach);
                return CommandOkTyped();
            case RuntimeModalKind.CharacterInfo:
                OnCharacterInfoWindowClosed();
                return CommandOkTyped();
            case RuntimeModalKind.Party:
                CloseDismissibleModal(RuntimeModalKind.Party);
                return CommandOkTyped();
            case RuntimeModalKind.Warehouse:
                CloseDismissibleModal(RuntimeModalKind.Warehouse);
                return CommandOkTyped();
            case RuntimeModalKind.SubmapConfirm:
                CloseDismissibleModal(RuntimeModalKind.SubmapConfirm);
                return CommandOkTyped();
            case RuntimeModalKind.BattleStartConfirm:
                return CommandErrorTyped("当前战斗开始确认必须点击\"开始战斗\"。");
            case RuntimeModalKind.Promotion:
                return CommandErrorTyped("当前晋升选择必须确认后才能继续。");
            case RuntimeModalKind.Reward:
                return CommandErrorTyped("当前角色奖励必须确认后才能继续。");
            default:
                return CommandErrorTyped("当前没有可关闭的窗口。");
        }
    }

    public void OnCharacterInfoWindowClosed()
    {
        if (!HasRuntime())
            return;
        ClearActiveCharacterInfoContext();
        SetActiveModalKind(RuntimeModalKind.None);
        UpdateStatus("已关闭人物信息窗。");
        PresentPendingRewardIfReady();
    }

    public bool OnPromotionChoiceSubmitted(
        StringName memberId,
        StringName professionId,
        PromotionSelectionData selection
    )
    {
        if (!HasRuntime())
            return false;
        if (IsBattleActive())
        {
            if (
                !PromotionPromptContainsChoice(
                    _port.GetPendingBattlePromotionPrompt(),
                    memberId,
                    professionId,
                    selection
                )
            )
            {
                RejectInvalidPromotionChoice();
                return false;
            }
            var batch = SubmitBattlePromotionChoice(memberId, professionId, selection);
            ApplyBattleBatch(batch);
            if (!BattlePromotionBatchApplied(batch, memberId, professionId))
            {
                RejectInvalidPromotionChoice();
                return false;
            }
            if (!BattlePromotionBatchNeedsFollowUp(batch, memberId))
            {
                ClearPendingPromotionPrompt();
                SetActiveModalKind(RuntimeModalKind.None);
            }
            return true;
        }

        GameRuntimePromotionPromptContext prompt = _port.GetPendingWorldPromotionPrompt();
        if (prompt.IsEmpty)
        {
            RejectInvalidPromotionChoice();
            return false;
        }
        if (!PromotionPromptContainsChoice(prompt, memberId, professionId, selection))
        {
            RejectInvalidPromotionChoice();
            return false;
        }
        var delta = PromoteProfession(memberId, professionId, selection);
        if (!PromotionDeltaApplied(delta, memberId, professionId))
        {
            RejectInvalidPromotionChoice();
            return false;
        }
        ClearPendingWorldPromotionPrompt();
        SetActiveModalKind(RuntimeModalKind.None);
        SyncPartyStateFromCharacterManagement();
        var persistError = PersistPartyState();
        if (delta.needs_promotion_modal)
        {
            SetPendingWorldPromotionPrompt(
                _port.BuildPromotionPrompt(delta, "确认后将在世界地图立即生效。")
            );
            SetActiveModalKind(RuntimeModalKind.Promotion);
            if (persistError == Error.Ok)
                UpdateStatus(
                    string.Format(
                        "{0} 完成晋升后还有后续抉择待确认。",
                        GetMemberDisplayName(memberId)
                    )
                );
            else
                UpdateStatus(
                    string.Format(
                        "{0} 的晋升已应用，但队伍状态持久化失败。",
                        GetMemberDisplayName(memberId)
                    )
                );
            return true;
        }

        if (persistError == Error.Ok)
            UpdateStatus(string.Format("{0} 完成职业晋升。", GetMemberDisplayName(memberId)));
        else
            UpdateStatus(
                string.Format(
                    "{0} 完成职业晋升，但队伍状态持久化失败。",
                    GetMemberDisplayName(memberId)
                )
            );
        PresentPendingRewardIfReady();
        return true;
    }

    public void OnPromotionChoiceCancelled()
    {
        if (!HasRuntime())
            return;
        if (IsBattleActive())
        {
            if (_port.GetPendingBattlePromotionPrompt().IsEmpty)
            {
                UpdateStatus("当前晋升选择无法取消。");
                return;
            }
            SetActiveModalKind(RuntimeModalKind.Promotion);
            UpdateStatus("当前晋升选择必须确认后才能继续战斗。");
            return;
        }

        if (_port.GetPendingWorldPromotionPrompt().IsEmpty)
        {
            UpdateStatus("当前晋升选择无法取消。");
            return;
        }
        SetActiveModalKind(RuntimeModalKind.Promotion);
        UpdateStatus("当前晋升选择必须确认后才能继续结算奖励。");
    }

    public void OnCharacterRewardConfirmed()
    {
        if (!HasRuntime() || GetActiveReward() == null)
            return;
        var reward = GetActiveReward();
        ClearActiveReward();
        SetActiveModalKind(RuntimeModalKind.None);

        var delta = ApplyPendingCharacterRewardToParty(reward);
        SyncPartyStateFromCharacterManagement();
        var persistError = PersistPartyState();
        if (delta != null && delta.needs_promotion_modal)
        {
            SetPendingWorldPromotionPrompt(
                _port.BuildPromotionPrompt(delta, "确认后将在世界地图立即生效。")
            );
            SetActiveModalKind(RuntimeModalKind.Promotion);
            if (persistError == Error.Ok)
                UpdateStatus(
                    string.Format("{0} 的角色奖励已入账，职业晋升待确认。", reward.member_name)
                );
            else
                UpdateStatus(
                    string.Format(
                        "{0} 的角色奖励已入账，但队伍状态持久化失败。",
                        reward.member_name
                    )
                );
            return;
        }

        if (
            delta == null
            || (
                delta.MasteryChangesTyped.Count == 0
                && delta.KnowledgeChangesTyped.Count == 0
                && delta.AttributeChangesTyped.Count == 0
            )
        )
        {
            if (persistError == Error.Ok)
                UpdateStatus(
                    string.Format("{0} 的本批奖励当前没有可入账项目。", reward.member_name)
                );
            else
                UpdateStatus(
                    string.Format("{0} 的奖励处理完成，但队伍状态持久化失败。", reward.member_name)
                );
        }
        else
        {
            if (persistError == Error.Ok)
                UpdateStatus(string.Format("{0} 的角色奖励已结算。", reward.member_name));
            else
                UpdateStatus(
                    string.Format(
                        "{0} 的角色奖励已结算，但队伍状态持久化失败。",
                        reward.member_name
                    )
                );
        }
        PresentPendingRewardIfReady();
    }

    internal void EnqueuePendingCharacterRewardsTyped(
        System.Collections.Generic.IEnumerable<PendingCharacterReward> rewardOptions
    )
    {
        if (!HasRuntime())
            return;
        _port.EnqueueCharacterRewards(rewardOptions);
    }

    public bool PresentPendingRewardIfReady()
    {
        var activeModalKind = GetActiveModalKind();
        if (!HasRuntime() || IsBattleActive())
            return false;
        if (!_port.GetPendingWorldPromotionPrompt().IsEmpty)
        {
            if (activeModalKind != RuntimeModalKind.Promotion)
            {
                SetActiveModalKind(RuntimeModalKind.Promotion);
                return true;
            }
            return false;
        }
        if (GetActiveReward() != null)
        {
            if (activeModalKind != RuntimeModalKind.Reward)
            {
                SetActiveModalKind(RuntimeModalKind.Reward);
                return true;
            }
            return false;
        }
        if (
            activeModalKind == RuntimeModalKind.Settlement
            || activeModalKind == RuntimeModalKind.ContractBoard
            || activeModalKind == RuntimeModalKind.BountyBoard
            || activeModalKind == RuntimeModalKind.NpcQuestOffer
            || activeModalKind == RuntimeModalKind.Shop
            || activeModalKind == RuntimeModalKind.Forge
            || activeModalKind == RuntimeModalKind.Stagecoach
            || activeModalKind == RuntimeModalKind.CharacterInfo
            || activeModalKind == RuntimeModalKind.Party
            || activeModalKind == RuntimeModalKind.Warehouse
            || activeModalKind == RuntimeModalKind.SubmapConfirm
            || activeModalKind == RuntimeModalKind.BattleStartConfirm
            || activeModalKind == RuntimeModalKind.GameOver
        )
            return false;
        SetActiveReward(_port.GetNextPendingReward());
        if (GetActiveReward() == null)
            return false;
        SetActiveModalKind(RuntimeModalKind.Reward);
        return true;
    }

    private bool HasRuntime()
    {
        return _port != null;
    }

    private GameRuntimeFacade.RuntimeCommandResult RuntimeUnavailableTypedResult()
    {
        return GameRuntimeFacade.RuntimeCommandResult.Failure(
            RuntimeUnavailableMessage,
            GameRuntimeFacade.RuntimeCommandCode.RuntimeUnavailable
        );
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

    private void RejectInvalidPromotionChoice()
    {
        SetActiveModalKind(RuntimeModalKind.Promotion);
        UpdateStatus(InvalidPromotionChoiceMessage);
    }

    private bool PromotionPromptContainsChoice(
        GameRuntimePromotionPromptContext prompt,
        StringName memberId,
        StringName professionId,
        PromotionSelectionData selection
    )
    {
        return prompt != null && prompt.ContainsChoice(memberId, professionId, selection);
    }

    private bool BattlePromotionBatchApplied(
        BattleEventBatch batch,
        StringName memberId,
        StringName professionId
    )
    {
        if (batch == null)
            return false;
        foreach (CharacterProgressionDelta delta in batch.ProgressionDeltasTyped)
        {
            if (PromotionDeltaApplied(delta, memberId, professionId))
                return true;
        }
        return false;
    }

    private bool BattlePromotionBatchNeedsFollowUp(BattleEventBatch batch, StringName memberId)
    {
        if (batch == null)
            return false;
        foreach (CharacterProgressionDelta delta in batch.ProgressionDeltasTyped)
        {
            if (delta != null && delta.member_id == memberId && delta.needs_promotion_modal)
                return true;
        }
        return false;
    }

    private bool PromotionDeltaApplied(
        CharacterProgressionDelta delta,
        StringName memberId,
        StringName professionId
    )
    {
        if (delta == null)
            return false;
        if (delta.member_id != memberId)
            return false;
        if (delta.needs_promotion_modal)
            return true;
        return delta.HasChangedProfessionId(professionId);
    }

    private PendingCharacterReward GetActiveReward()
    {
        if (!HasRuntime())
            return null;
        return _port.GetActiveReward();
    }

    private RuntimeModalKind GetActiveModalKind()
    {
        return HasRuntime() ? _port.GetActiveModalKind() : RuntimeModalKind.None;
    }

    private void SetActiveModalKind(RuntimeModalKind modalKind)
    {
        if (HasRuntime())
            _port.SetActiveModalKind(modalKind);
    }

    private void UpdateStatus(string message)
    {
        if (HasRuntime())
            _port.UpdateStatus(message);
    }

    private bool IsBattleActive()
    {
        if (!HasRuntime())
            return false;
        return _port.IsBattleActive();
    }

    private void ClearActiveCharacterInfoContext()
    {
        if (HasRuntime())
            _port.ClearActiveCharacterInfoContext();
    }

    private void CloseDismissibleModal(RuntimeModalKind modalKind)
    {
        if (HasRuntime())
            _port.CloseDismissibleModal(modalKind);
    }

    private void ClearPendingPromotionPrompt()
    {
        if (HasRuntime())
            _port.ClearPendingBattlePromotionPrompt();
    }

    private void ClearPendingWorldPromotionPrompt()
    {
        if (HasRuntime())
            _port.ClearPendingWorldPromotionPrompt();
    }

    private BattleEventBatch SubmitBattlePromotionChoice(
        StringName memberId,
        StringName professionId,
        PromotionSelectionData selection
    )
    {
        if (!HasRuntime())
            return null;
        return _port.SubmitBattlePromotionChoice(memberId, professionId, selection);
    }

    private void ApplyBattleBatch(BattleEventBatch batch)
    {
        if (HasRuntime() && batch != null)
            _port.ApplyBattleBatch(batch);
    }

    private CharacterProgressionDelta PromoteProfession(
        StringName memberId,
        StringName professionId,
        PromotionSelectionData selection
    )
    {
        if (!HasRuntime())
            return null;
        return _port.PromoteProfession(memberId, professionId, selection);
    }

    private void SyncPartyStateFromCharacterManagement()
    {
        if (HasRuntime())
            _port.SyncPartyStateFromCharacterManagement();
    }

    private Error PersistPartyState()
    {
        if (!HasRuntime())
            return Error.Unavailable;
        return _port.PersistPartyState();
    }

    private void SetPendingWorldPromotionPrompt(GameRuntimePromotionPromptContext prompt)
    {
        if (HasRuntime())
            _port.SetPendingWorldPromotionPrompt(prompt);
    }

    private string GetMemberDisplayName(StringName memberId)
    {
        if (!HasRuntime())
            return memberId.ToString();
        return _port.GetMemberDisplayName(memberId);
    }

    private void ClearActiveReward()
    {
        if (HasRuntime())
            _port.ClearActiveReward();
    }

    private CharacterProgressionDelta ApplyPendingCharacterRewardToParty(
        PendingCharacterReward reward
    )
    {
        if (!HasRuntime())
            return null;
        return _port.ApplyPendingCharacterReward(reward);
    }

    private void SetActiveReward(PendingCharacterReward reward)
    {
        if (HasRuntime())
            _port.SetActiveReward(reward);
    }

    private static IGameRuntimeRewardFlowPort ResolveWeakRef(
        WeakReference<IGameRuntimeRewardFlowPort> weakRef
    )
    {
        if (weakRef == null || !weakRef.TryGetTarget(out IGameRuntimeRewardFlowPort target))
            return null;
        return target;
    }
}
