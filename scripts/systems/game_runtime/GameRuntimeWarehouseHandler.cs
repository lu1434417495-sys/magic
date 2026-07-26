using System;
using System.Collections.Generic;
using Godot;
using Godot.Collections;
using PlainDictionary = System.Collections.Generic.Dictionary<string, object>;
using PlainList = System.Collections.Generic.List<object>;

public sealed class GameRuntimeWarehouseHandler
{
    private static readonly string RuntimeUnavailableMessage = "运行时尚未初始化。";

    private WeakReference<IGameRuntimeWarehousePort> _portRef;

    private IGameRuntimeWarehousePort _port
    {
        get => ResolveWeakRef(_portRef);
        set =>
            _portRef =
                value != null ? new WeakReference<IGameRuntimeWarehousePort>(value) : null;
    }

    internal void Setup(IGameRuntimeWarehousePort port)
    {
        _port = port;
    }

    public void Dispose()
    {
        _port = null;
    }

    internal Dictionary GetWarehouseWindowData()
    {
        if (!HasRuntime())
            return new Dictionary();
        WarehouseCommandContextSnapshot context = CaptureContext();
        if (!context.HasParty || !context.WarehouseReady)
            return new Dictionary();
        return BuildWarehouseWindowData();
    }

    internal System.Collections.Generic.IReadOnlyDictionary<string, object> GetWarehouseWindowDataSnapshotPlain()
    {
        if (!HasRuntime())
            return new PlainDictionary(StringComparer.Ordinal);
        WarehouseCommandContextSnapshot context = CaptureContext();
        if (!context.HasParty || !context.WarehouseReady)
            return new PlainDictionary(StringComparer.Ordinal);
        return BuildWarehouseWindowDataSnapshotPlain();
    }

    internal RuntimeCommandResult CommandOpenPartyWarehouseTyped()
    {
        if (!HasRuntime())
            return RuntimeUnavailableTypedResult();
        WarehouseCommandContextSnapshot context = CaptureContext();
        if (!context.HasParty)
            return CommandErrorTyped("当前不存在队伍数据。");
        if (context.IsBattleActive)
            return CommandErrorTyped("当前处于战斗中，不能打开共享仓库。");

        if (context.ModalKind == RuntimeModalKind.Settlement)
        {
            OpenPartyWarehouseWindow("据点服务");
            UpdateStatus("已从据点窗口打开共享仓库。");
        }
        else
        {
            OpenPartyWarehouseWindow("队伍管理");
            UpdateStatus("已打开共享仓库。");
        }
        return CommandOkTyped();
    }

    internal RuntimeCommandResult CommandDiscardOneTyped(
        StringName itemId,
        StringName instanceId = default
    )
    {
        if (!HasRuntime())
            return RuntimeUnavailableTypedResult();
        WarehouseCommandContextSnapshot context = CaptureContext();
        if (context.ModalKind != RuntimeModalKind.Warehouse)
            return CommandErrorTyped("共享仓库当前未打开。");
        if (!context.WarehouseReady)
            return CommandErrorTyped("共享仓库服务尚未准备完成。");

        WarehouseDiscardMutationResult result = _port.DiscardOneAndStage(itemId, instanceId);
        if (!result.Success)
        {
            string failureMessage =
                result.FailureKind == WarehouseDiscardFailureKind.StageFailed
                    ? string.Format(
                        "已从共享仓库丢弃 1 件 {0}，但队伍状态同步失败，操作已回滚。",
                        result.ItemName
                    )
                    : BuildDiscardFailureMessage(result);
            UpdateStatus(failureMessage);
            return result.FailureKind == WarehouseDiscardFailureKind.StageFailed
                ? RuntimeCommandResult.Failure(
                    failureMessage,
                    RuntimeCommandCode.PersistenceFailure
                )
                : CommandErrorTyped(failureMessage);
        }

        string successMessage = string.Format(
            "已从共享仓库丢弃 1 件 {0}。",
            result.ItemName
        );
        UpdateStatus(successMessage);
        return CommandOkTyped(successMessage);
    }

    internal RuntimeCommandResult CommandDiscardAllTyped(StringName itemId)
    {
        if (!HasRuntime())
            return RuntimeUnavailableTypedResult();
        WarehouseCommandContextSnapshot context = CaptureContext();
        if (context.ModalKind != RuntimeModalKind.Warehouse)
            return CommandErrorTyped("共享仓库当前未打开。");
        if (!context.WarehouseReady)
            return CommandErrorTyped("共享仓库服务尚未准备完成。");

        WarehouseDiscardMutationResult result = _port.DiscardAllAndStage(itemId);
        if (!result.Success)
        {
            string failureMessage =
                result.FailureKind == WarehouseDiscardFailureKind.StageFailed
                    ? string.Format(
                        "已从共享仓库丢弃全部 {0}，但队伍状态同步失败，操作已回滚。",
                        result.ItemName
                    )
                    : BuildDiscardFailureMessage(result);
            UpdateStatus(failureMessage);
            if (
                result.FailureKind
                == WarehouseDiscardFailureKind.UnsupportedDiscardAllEquipment
            )
            {
                return RuntimeCommandResult.Failure(
                    failureMessage,
                    RuntimeCommandCode.InvalidArgument
                );
            }
            return result.FailureKind == WarehouseDiscardFailureKind.StageFailed
                ? RuntimeCommandResult.Failure(
                    failureMessage,
                    RuntimeCommandCode.PersistenceFailure
                )
                : CommandErrorTyped(failureMessage);
        }

        string successMessage = string.Format(
            "已从共享仓库丢弃全部 {0}，共 {1} 件。",
            result.ItemName,
            result.RemovedQuantity
        );
        UpdateStatus(successMessage);
        return CommandOkTyped(successMessage);
    }

    internal RuntimeCommandResult CommandUseItemTyped(
        StringName itemId,
        StringName memberId = default,
        PartyItemUseService.PartyItemUseOptions options = null
    )
    {
        if (!HasRuntime())
            return RuntimeUnavailableTypedResult();
        WarehouseCommandContextSnapshot context = CaptureContext();
        if (context.ModalKind != RuntimeModalKind.Warehouse)
            return CommandErrorTyped("共享仓库当前未打开。");
        WarehouseUseMutationResult result = _port.UseItemAndStage(itemId, memberId, options);
        if (!result.Success)
        {
            if (result.FailureKind == WarehouseUseFailureKind.MissingTargetMember)
                return CommandErrorTyped(BuildWarehouseUseFailureMessage(result));
            string failureMessage =
                result.FailureKind == WarehouseUseFailureKind.StageFailed
                    ? string.Format(
                        "已让 {0} 使用 {1}，学会 {2}，但队伍状态同步失败，操作已回滚。",
                        result.MemberName,
                        result.ItemName,
                        result.SkillName
                    )
                    : BuildWarehouseUseFailureMessage(result);
            UpdateStatus(failureMessage);
            return result.FailureKind == WarehouseUseFailureKind.StageFailed
                ? RuntimeCommandResult.Failure(
                    failureMessage,
                    RuntimeCommandCode.PersistenceFailure
                )
                : CommandErrorTyped(failureMessage);
        }

        string successMessage = string.Format(
            "已让 {0} 使用 {1}，学会 {2}。",
            result.MemberName,
            result.ItemName,
            result.SkillName
        );
        UpdateStatus(successMessage);
        return CommandOkTyped(successMessage);
    }

    internal RuntimeCommandResult CommandAddItemTyped(
        StringName itemId,
        int quantity
    )
    {
        if (!HasRuntime())
            return RuntimeUnavailableTypedResult();
        WarehouseCommandContextSnapshot context = CaptureContext();
        if (!context.HasParty)
            return CommandErrorTyped("当前不存在队伍数据。");
        if (context.IsBattleActive)
            return CommandErrorTyped("当前处于战斗中，不能直接改动共享仓库。");
        if (quantity <= 0)
            return RuntimeCommandResult.Failure(
                "加入数量必须大于 0。",
                RuntimeCommandCode.InvalidArgument
            );
        if (!context.WarehouseReady)
            return CommandErrorTyped("共享仓库服务尚未准备完成。");

        WarehouseAddMutationResult result = _port.AddItemAndStage(itemId, quantity);
        string successMessage = string.Format(
            "已向共享仓库加入 {0} 件 {1}。",
            result.AddedQuantity,
            result.ItemName
        );
        if (result.RemainingQuantity > 0)
            successMessage = string.Format(
                "已向共享仓库加入 {0} 件 {1}，仍有 {2} 件未能放入。",
                result.AddedQuantity,
                result.ItemName,
                result.RemainingQuantity
            );
        if (!result.Success)
        {
            if (result.FailureKind == WarehouseAddFailureKind.MutationFailed)
                return CommandErrorTyped(
                    string.Format("{0} 当前无法加入共享仓库。", result.ItemName)
                );
            string failureMessage =
                result.FailureKind == WarehouseAddFailureKind.StageFailed
                    ? string.Format("{0} 但队伍状态同步失败，操作已回滚。", successMessage)
                    : string.Format("{0} 当前无法加入共享仓库。", result.ItemName);
            UpdateStatus(failureMessage);
            return result.FailureKind == WarehouseAddFailureKind.StageFailed
                ? RuntimeCommandResult.Failure(
                    failureMessage,
                    RuntimeCommandCode.PersistenceFailure
                )
                : CommandErrorTyped(failureMessage);
        }

        UpdateStatus(successMessage);
        return CommandOkTyped(successMessage);
    }

    public void OpenPartyWarehouseWindow(string entryLabel)
    {
        if (!HasRuntime())
            return;
        if (CaptureContext().IsBattleActive)
            return;
        _port.OpenWarehouse(entryLabel);
    }

    public void OnPartyWarehouseWindowClosed()
    {
        if (!HasRuntime())
            return;

        _port.CloseWarehouseAndPresentPendingReward("已关闭共享仓库。");
    }

    private string BuildWarehouseUseFailureMessage(WarehouseUseMutationResult result)
    {
        switch (result.FailureKind)
        {
            case WarehouseUseFailureKind.MissingTargetMember:
                return "当前没有可使用技能书的目标角色。";
            case WarehouseUseFailureKind.MissingItemDefinition:
                return string.Format("{0} 的物品定义缺失，当前无法使用。", result.ItemName);
            case WarehouseUseFailureKind.ItemNotUsable:
                return string.Format("{0} 当前不是可使用的技能书。", result.ItemName);
            case WarehouseUseFailureKind.MissingMember:
                return string.Format("当前找不到可使用 {0} 的目标角色。", result.ItemName);
            case WarehouseUseFailureKind.MissingInventory:
                return string.Format("{0} 当前没有可使用的库存。", result.ItemName);
            case WarehouseUseFailureKind.MissingSkillDefinition:
                return string.Format("{0} 对应的技能定义缺失，当前无法使用。", result.ItemName);
            case WarehouseUseFailureKind.LearnFailed:
                return string.Format(
                    "{0} 当前无法让 {1} 学会，可能已学会或未满足前置条件。",
                    result.ItemName,
                    result.MemberName
                );
            case WarehouseUseFailureKind.PracticeReplacementConfirmationRequired:
                if (result.SkillId != "")
                {
                    return string.Format(
                        "{0} 会替换 {1} 当前的同系练功技能 {2}，新技能预计为 {3} 级；确认后才会消耗技能书。",
                        result.ItemName,
                        result.MemberName,
                        result.ExistingSkillName,
                        result.PredictedLevel
                    );
                }
                return string.Format(
                    "{0} 需要确认练功技能替换后才能使用。",
                    result.ItemName
                );
            case WarehouseUseFailureKind.ConsumeFailed:
                return string.Format("{0} 已触发学习，但库存扣减失败。", result.ItemName);
            case WarehouseUseFailureKind.ServiceUnavailable:
                return "当前技能书服务尚未准备完成。";
            default:
                return string.Format("{0} 当前无法使用。", result.ItemName);
        }
    }

    private Dictionary BuildWarehouseWindowData()
    {
        WarehouseWindowSnapshot snapshot = _port.CaptureWarehouseWindowSnapshot();
        if (!snapshot.Available)
            return new Dictionary();
        var inventoryEntries = new Godot.Collections.Array();
        foreach (WarehouseInventoryEntrySnapshot entry in snapshot.Entries)
        {
            inventoryEntries.Add(BuildWarehouseInventoryEntry(entry));
        }
        var targetMembers = new Godot.Collections.Array();
        foreach (WarehouseTargetMemberSnapshot member in snapshot.TargetMembers)
        {
            targetMembers.Add(
                new Dictionary
                {
                    ["member_id"] = member.MemberId.ToString(),
                    ["display_name"] = member.DisplayName,
                    ["roster_role"] = member.RosterRole,
                }
            );
        }

        var summaryText = string.Format(
            "容量 {0} 格  |  已用 {1} 格  |  空余 {2} 格",
            snapshot.TotalCapacity,
            snapshot.UsedSlots,
            snapshot.FreeSlots
        );
        var statusText =
            "当前版本支持查看、丢弃和让指定角色使用技能书。非装备物品会优先补满同类堆栈，装备则按实例独立占格。";
        if (snapshot.IsOverCapacity)
            statusText = string.Format(
                "仓库当前超容 {0} 格。已存物品不会被删除，但此时不能继续新增条目，只能整理和移除。",
                snapshot.UsedSlots - snapshot.TotalCapacity
            );

        return new Dictionary
        {
            ["title"] = "共享仓库",
            ["meta"] = string.Format(
                "入口：{0}  |  规则：全队共享、按堆栈/实例占格、不计重量。",
                snapshot.EntryLabel
            ),
            ["summary_text"] = summaryText,
            ["status_text"] = statusText,
            ["target_members"] = targetMembers,
            ["default_target_member_id"] = snapshot.DefaultTargetMemberId.ToString(),
            ["entries"] = inventoryEntries,
        };
    }

    private System.Collections.Generic.IReadOnlyDictionary<string, object> BuildWarehouseWindowDataSnapshotPlain()
    {
        WarehouseWindowSnapshot snapshot = _port.CaptureWarehouseWindowSnapshot();
        if (!snapshot.Available)
            return new PlainDictionary(StringComparer.Ordinal);
        var inventoryEntries = new PlainList();
        foreach (WarehouseInventoryEntrySnapshot entry in snapshot.Entries)
        {
            inventoryEntries.Add(BuildWarehouseInventoryEntrySnapshotPlain(entry));
        }
        var targetMembers = new PlainList();
        foreach (WarehouseTargetMemberSnapshot member in snapshot.TargetMembers)
        {
            targetMembers.Add(
                new PlainDictionary(StringComparer.Ordinal)
                {
                    ["member_id"] = member.MemberId.ToString(),
                    ["display_name"] = member.DisplayName,
                    ["roster_role"] = member.RosterRole,
                }
            );
        }

        string summaryText = string.Format(
            "容量 {0} 格  |  已用 {1} 格  |  空余 {2} 格",
            snapshot.TotalCapacity,
            snapshot.UsedSlots,
            snapshot.FreeSlots
        );
        string statusText =
            "当前版本支持查看、丢弃和让指定角色使用技能书。非装备物品会优先补满同类堆栈，装备则按实例独立占格。";
        if (snapshot.IsOverCapacity)
        {
            statusText = string.Format(
                "仓库当前超容 {0} 格。已存物品不会被删除，但此时不能继续新增条目，只能整理和移除。",
                snapshot.UsedSlots - snapshot.TotalCapacity
            );
        }

        return new PlainDictionary(StringComparer.Ordinal)
        {
            ["title"] = "共享仓库",
            ["meta"] = string.Format(
                "入口：{0}  |  规则：全队共享、按堆栈/实例占格、不计重量。",
                snapshot.EntryLabel
            ),
            ["summary_text"] = summaryText,
            ["status_text"] = statusText,
            ["target_members"] = targetMembers,
            ["default_target_member_id"] = snapshot.DefaultTargetMemberId.ToString(),
            ["entries"] = inventoryEntries,
        };
    }

    private static Dictionary BuildWarehouseInventoryEntry(
        WarehouseInventoryEntrySnapshot entry
    )
    {
        if (entry == null)
            return new Dictionary();

        var result = new Dictionary
        {
            ["item_id"] = entry.ItemId.ToString(),
            ["display_name"] = entry.DisplayName,
            ["description"] = entry.Description,
            ["icon"] = entry.Icon,
            ["quantity"] = entry.Quantity,
            ["total_quantity"] = entry.TotalQuantity,
            ["is_stackable"] = entry.IsStackable,
            ["stack_limit"] = entry.StackLimit,
            ["item_category"] = entry.ItemCategory.ToString(),
            ["is_skill_book"] = entry.IsSkillBook,
            ["granted_skill_id"] = entry.GrantedSkillId.ToString(),
            ["granted_skill_name"] = entry.GrantedSkillName,
            ["storage_mode"] = entry.StorageMode.ToString(),
        };
        if (entry.HasEquipmentInstance)
        {
            result["instance_id"] = entry.InstanceId.ToString();
            result["rarity"] = entry.Rarity;
            result["current_durability"] = entry.CurrentDurability;
        }
        return result;
    }

    private static System.Collections.Generic.IReadOnlyDictionary<string, object>
        BuildWarehouseInventoryEntrySnapshotPlain(
            WarehouseInventoryEntrySnapshot entry
        )
    {
        if (entry == null)
            return new PlainDictionary(StringComparer.Ordinal);
        var result = new PlainDictionary(StringComparer.Ordinal)
        {
            ["item_id"] = entry.ItemId.ToString(),
            ["display_name"] = entry.DisplayName,
            ["description"] = entry.Description,
            ["icon"] = entry.Icon,
            ["quantity"] = entry.Quantity,
            ["total_quantity"] = entry.TotalQuantity,
            ["is_stackable"] = entry.IsStackable,
            ["stack_limit"] = entry.StackLimit,
            ["item_category"] = entry.ItemCategory.ToString(),
            ["is_skill_book"] = entry.IsSkillBook,
            ["granted_skill_id"] = entry.GrantedSkillId.ToString(),
            ["granted_skill_name"] = entry.GrantedSkillName,
            ["storage_mode"] = entry.StorageMode.ToString(),
        };
        if (entry.HasEquipmentInstance)
        {
            result["instance_id"] = entry.InstanceId.ToString();
            result["rarity"] = entry.Rarity;
            result["current_durability"] = entry.CurrentDurability;
        }
        return result;
    }

    private static string BuildDiscardFailureMessage(WarehouseDiscardMutationResult result)
    {
        switch (result.FailureKind)
        {
            case WarehouseDiscardFailureKind.UnsupportedDiscardAllEquipment:
                return string.Format(
                    "{0} 是独立装备实例，不能丢弃全部同类。请选择具体装备后使用“丢弃此装备”。",
                    result.ItemName
                );
            case WarehouseDiscardFailureKind.EquipmentInstanceIdRequired:
                return string.Format("请选择要丢弃的 {0} 装备实例。", result.ItemName);
            case WarehouseDiscardFailureKind.WarehouseMissingInstance:
                return string.Format(
                    "共享仓库中没有指定的 {0} 装备实例。",
                    result.ItemName
                );
            case WarehouseDiscardFailureKind.EquipmentInstanceItemMismatch:
                return string.Format("指定装备实例不属于 {0}。", result.ItemName);
            case WarehouseDiscardFailureKind.ItemNotEquipment:
                return string.Format(
                    "{0} 不是装备实例，无法按实例丢弃。",
                    result.ItemName
                );
            case WarehouseDiscardFailureKind.ItemNotFound:
                return string.Format("未找到物品定义 {0}。", result.ItemId);
            default:
                return string.Format("{0} 当前没有可丢弃的库存。", result.ItemName);
        }
    }

    private bool HasRuntime()
    {
        return _port != null;
    }

    private RuntimeCommandResult CommandOkTyped(string message = "")
    {
        return RuntimeCommandResult.Success(message ?? "");
    }

    private RuntimeCommandResult CommandErrorTyped(string message)
    {
        return RuntimeCommandResult.Failure(
            message ?? "",
            RuntimeCommandCode.InvalidState
        );
    }

    private RuntimeCommandResult RuntimeUnavailableTypedResult()
    {
        return RuntimeCommandResult.Failure(
            RuntimeUnavailableMessage,
            RuntimeCommandCode.RuntimeUnavailable
        );
    }

    private WarehouseCommandContextSnapshot CaptureContext() =>
        HasRuntime()
            ? _port.CaptureWarehouseCommandContext()
            : new WarehouseCommandContextSnapshot(
                false,
                false,
                false,
                RuntimeModalKind.None
            );

    private void UpdateStatus(string message)
    {
        if (HasRuntime())
            _port.UpdateWarehouseStatus(message);
    }

    private static IGameRuntimeWarehousePort ResolveWeakRef(
        WeakReference<IGameRuntimeWarehousePort> weakRef
    )
    {
        if (
            weakRef == null
            || !weakRef.TryGetTarget(out IGameRuntimeWarehousePort target)
        )
            return null;
        return target;
    }
}
