using System;
using System.Collections.Generic;
using Godot;

internal interface IGameRuntimeWarehousePort
{
    WarehouseCommandContextSnapshot CaptureWarehouseCommandContext();
    WarehouseWindowSnapshot CaptureWarehouseWindowSnapshot();
    void OpenWarehouse(string entryLabel);
    void CloseWarehouseAndPresentPendingReward(string statusMessage);
    void UpdateWarehouseStatus(string message);
    WarehouseDiscardMutationResult DiscardOneAndStage(
        StringName itemId,
        StringName instanceId
    );
    WarehouseDiscardMutationResult DiscardAllAndStage(StringName itemId);
    WarehouseUseMutationResult UseItemAndStage(
        StringName itemId,
        StringName memberId,
        PartyItemUseService.PartyItemUseOptions options
    );
    WarehouseAddMutationResult AddItemAndStage(StringName itemId, int quantity);
}

internal readonly struct WarehouseCommandContextSnapshot
{
    public WarehouseCommandContextSnapshot(
        bool hasParty,
        bool warehouseReady,
        bool isBattleActive,
        RuntimeModalKind modalKind
    )
    {
        HasParty = hasParty;
        WarehouseReady = warehouseReady;
        IsBattleActive = isBattleActive;
        ModalKind = modalKind;
    }

    public bool HasParty { get; }
    public bool WarehouseReady { get; }
    public bool IsBattleActive { get; }
    public RuntimeModalKind ModalKind { get; }
}

internal sealed class WarehouseTargetMemberSnapshot
{
    public WarehouseTargetMemberSnapshot(
        StringName memberId,
        string displayName,
        string rosterRole
    )
    {
        MemberId = memberId;
        DisplayName = displayName ?? "";
        RosterRole = rosterRole ?? "";
    }

    public StringName MemberId { get; }
    public string DisplayName { get; }
    public string RosterRole { get; }
}

internal sealed class WarehouseInventoryEntrySnapshot
{
    public StringName ItemId { get; init; }
    public string DisplayName { get; init; } = "";
    public string Description { get; init; } = "";
    public string Icon { get; init; } = "";
    public int Quantity { get; init; }
    public int TotalQuantity { get; init; }
    public bool IsStackable { get; init; }
    public int StackLimit { get; init; }
    public StringName ItemCategory { get; init; }
    public bool IsSkillBook { get; init; }
    public StringName GrantedSkillId { get; init; }
    public string GrantedSkillName { get; init; } = "";
    public StringName StorageMode { get; init; }
    public StringName InstanceId { get; init; }
    public int Rarity { get; init; }
    public int CurrentDurability { get; init; }
    public bool HasEquipmentInstance { get; init; }
}

internal sealed class WarehouseWindowSnapshot
{
    private readonly IReadOnlyList<WarehouseTargetMemberSnapshot> _targetMembers;
    private readonly IReadOnlyList<WarehouseInventoryEntrySnapshot> _entries;

    public static WarehouseWindowSnapshot Empty { get; } =
        new(
            false,
            0,
            0,
            0,
            false,
            "",
            "",
            Array.Empty<WarehouseTargetMemberSnapshot>(),
            Array.Empty<WarehouseInventoryEntrySnapshot>()
        );

    public WarehouseWindowSnapshot(
        bool available,
        int totalCapacity,
        int usedSlots,
        int freeSlots,
        bool isOverCapacity,
        string entryLabel,
        StringName defaultTargetMemberId,
        IEnumerable<WarehouseTargetMemberSnapshot> targetMembers,
        IEnumerable<WarehouseInventoryEntrySnapshot> entries
    )
    {
        Available = available;
        TotalCapacity = totalCapacity;
        UsedSlots = usedSlots;
        FreeSlots = freeSlots;
        IsOverCapacity = isOverCapacity;
        EntryLabel = entryLabel ?? "";
        DefaultTargetMemberId = defaultTargetMemberId;
        _targetMembers = Array.AsReadOnly(
            targetMembers != null
                ? new List<WarehouseTargetMemberSnapshot>(targetMembers)
                    .FindAll(value => value != null)
                    .ToArray()
                : Array.Empty<WarehouseTargetMemberSnapshot>()
        );
        _entries = Array.AsReadOnly(
            entries != null
                ? new List<WarehouseInventoryEntrySnapshot>(entries)
                    .FindAll(value => value != null)
                    .ToArray()
                : Array.Empty<WarehouseInventoryEntrySnapshot>()
        );
    }

    public bool Available { get; }
    public int TotalCapacity { get; }
    public int UsedSlots { get; }
    public int FreeSlots { get; }
    public bool IsOverCapacity { get; }
    public string EntryLabel { get; }
    public StringName DefaultTargetMemberId { get; }
    public IReadOnlyList<WarehouseTargetMemberSnapshot> TargetMembers => _targetMembers;
    public IReadOnlyList<WarehouseInventoryEntrySnapshot> Entries => _entries;
}

internal enum WarehouseDiscardFailureKind
{
    None = 0,
    UnsupportedDiscardAllEquipment = 1,
    MissingStock = 2,
    EquipmentInstanceIdRequired = 3,
    WarehouseMissingInstance = 4,
    EquipmentInstanceItemMismatch = 5,
    ItemNotEquipment = 6,
    ItemNotFound = 7,
    MutationFailed = 8,
    StageFailed = 9,
}

internal readonly struct WarehouseDiscardMutationResult
{
    public WarehouseDiscardMutationResult(
        bool success,
        WarehouseDiscardFailureKind failureKind,
        StringName itemId,
        string itemName,
        int removedQuantity
    )
    {
        Success = success;
        FailureKind = failureKind;
        ItemId = itemId;
        ItemName = itemName ?? itemId.ToString();
        RemovedQuantity = removedQuantity;
    }

    public bool Success { get; }
    public WarehouseDiscardFailureKind FailureKind { get; }
    public StringName ItemId { get; }
    public string ItemName { get; }
    public int RemovedQuantity { get; }
}

internal enum WarehouseUseFailureKind
{
    None = 0,
    MissingTargetMember = 1,
    MissingItemDefinition = 2,
    ItemNotUsable = 3,
    MissingMember = 4,
    MissingInventory = 5,
    MissingSkillDefinition = 6,
    LearnFailed = 7,
    PracticeReplacementConfirmationRequired = 8,
    ConsumeFailed = 9,
    ServiceUnavailable = 10,
    StageFailed = 11,
    Unknown = 12,
}

internal readonly struct WarehouseUseMutationResult
{
    public WarehouseUseMutationResult(
        bool success,
        WarehouseUseFailureKind failureKind,
        StringName itemId,
        string itemName,
        StringName memberId,
        string memberName,
        StringName skillId,
        string skillName,
        string existingSkillName,
        int predictedLevel
    )
    {
        Success = success;
        FailureKind = failureKind;
        ItemId = itemId;
        ItemName = itemName ?? itemId.ToString();
        MemberId = memberId;
        MemberName = memberName ?? memberId.ToString();
        SkillId = skillId;
        SkillName = skillName ?? skillId.ToString();
        ExistingSkillName = existingSkillName ?? "";
        PredictedLevel = predictedLevel;
    }

    public bool Success { get; }
    public WarehouseUseFailureKind FailureKind { get; }
    public StringName ItemId { get; }
    public string ItemName { get; }
    public StringName MemberId { get; }
    public string MemberName { get; }
    public StringName SkillId { get; }
    public string SkillName { get; }
    public string ExistingSkillName { get; }
    public int PredictedLevel { get; }
}

internal enum WarehouseAddFailureKind
{
    None = 0,
    MutationFailed = 1,
    StageFailed = 2,
}

internal readonly struct WarehouseAddMutationResult
{
    public WarehouseAddMutationResult(
        bool success,
        WarehouseAddFailureKind failureKind,
        StringName itemId,
        string itemName,
        int addedQuantity,
        int remainingQuantity
    )
    {
        Success = success;
        FailureKind = failureKind;
        ItemId = itemId;
        ItemName = itemName ?? itemId.ToString();
        AddedQuantity = addedQuantity;
        RemainingQuantity = remainingQuantity;
    }

    public bool Success { get; }
    public WarehouseAddFailureKind FailureKind { get; }
    public StringName ItemId { get; }
    public string ItemName { get; }
    public int AddedQuantity { get; }
    public int RemainingQuantity { get; }
}
