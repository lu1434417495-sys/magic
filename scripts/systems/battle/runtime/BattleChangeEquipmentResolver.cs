using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

internal sealed class BattleChangeEquipmentResult
{
    internal bool Allowed;
    internal string ErrorCode = "";
    internal string Message = "";
    internal StringName Operation = "";
    internal StringName SlotId = "";
    internal StringName TargetUnitId = "";
    internal StringName ItemId = "";
    internal StringName InstanceId = "";
    internal List<StringName> OccupiedSlotIds = new();

    internal int ApBefore;
    internal int ApAfter;
    internal int HpBefore;
    internal int HpAfter;
    internal int HpMaxBefore;
    internal int HpMaxAfter;
    internal bool HpClamped;
    internal StringName WeaponProfileKind = "";
    internal StringName WeaponItemId = "";
    internal StringName WeaponProfileTypeId = "";
    internal StringName WeaponCurrentGrip = "";
    internal int WeaponAttackRange;
    internal bool WeaponUsesTwoHands;
    internal StringName WeaponPhysicalDamageTag = "";

    internal BattleChangeEquipmentResult Clone()
    {
        return new BattleChangeEquipmentResult
        {
            Allowed = Allowed,
            ErrorCode = ErrorCode,
            Message = Message,
            Operation = Operation,
            SlotId = SlotId,
            TargetUnitId = TargetUnitId,
            ItemId = ItemId,
            InstanceId = InstanceId,
            OccupiedSlotIds = CloneStringNameList(OccupiedSlotIds),
            ApBefore = ApBefore,
            ApAfter = ApAfter,
            HpBefore = HpBefore,
            HpAfter = HpAfter,
            HpMaxBefore = HpMaxBefore,
            HpMaxAfter = HpMaxAfter,
            HpClamped = HpClamped,
            WeaponProfileKind = WeaponProfileKind,
            WeaponItemId = WeaponItemId,
            WeaponProfileTypeId = WeaponProfileTypeId,
            WeaponCurrentGrip = WeaponCurrentGrip,
            WeaponAttackRange = WeaponAttackRange,
            WeaponUsesTwoHands = WeaponUsesTwoHands,
            WeaponPhysicalDamageTag = WeaponPhysicalDamageTag,
        };
    }

    private static List<StringName> CloneStringNameList(IReadOnlyList<StringName> values)
    {
        var clone = new List<StringName>();
        foreach (StringName value in values ?? Array.Empty<StringName>())
        {
            clone.Add(ProgressionDataUtils.to_string_name(value));
        }
        return clone;
    }
}

internal readonly struct ChangeEquipmentRuleResult
{
    internal readonly bool Allowed;
    internal readonly string ErrorCode;
    internal readonly string Message;
    internal readonly IReadOnlyList<StringName> OccupiedSlotIds;

    internal ChangeEquipmentRuleResult(
        bool allowed,
        string errorCode = "",
        string message = "",
        IReadOnlyList<StringName> occupiedSlotIds = null
    )
    {
        Allowed = allowed;
        ErrorCode = errorCode ?? "";
        Message = message ?? "";
        OccupiedSlotIds = occupiedSlotIds != null
            ? new List<StringName>(occupiedSlotIds)
            : new List<StringName>();
    }

}

// 翻译自 battle_change_equipment_resolver.gd（2026-05-25，战斗换装 C# 迁移）。
// runtime 耦合：战斗实体/视图使用 C# runtime state；command 为 C# BattleCommand。
internal class BattleChangeEquipmentResolver
{
    private const int CHANGE_EQUIPMENT_AP_COST = 2;

    private WeakReference<BattleRuntimeModule> _runtimeRef;

    private BattleRuntimeModule _runtime
    {
        get => ResolveWeakRef(_runtimeRef);
        set => _runtimeRef = value != null ? new WeakReference<BattleRuntimeModule>(value) : null;
    }

    internal void Setup(BattleRuntimeModule runtime)
    {
        _runtime = runtime;
    }

    internal void Dispose()
    {
        _runtime = null;
    }

    internal void PreviewCommand(BattleUnitReadView active_unit, BattleCommand command, BattlePreview preview)
    {
        PreviewChangeEquipmentCommand(active_unit, command, preview);
    }

    internal void HandleCommand(BattleUnitState active_unit, BattleCommand command, BattleEventBatch batch)
    {
        HandleChangeEquipmentCommand(active_unit, command, batch);
    }

    internal int GetUnitHpMax(BattleUnitState unit_state)
    {
        return ComputeUnitHpMax(unit_state);
    }

    internal int GetUnitStaminaMax(BattleUnitState unit_state)
    {
        return ComputeUnitStaminaMax(unit_state);
    }

    private void PreviewChangeEquipmentCommand(
        BattleUnitReadView activeUnit,
        BattleCommand command,
        BattlePreview preview
    )
    {
        if (preview == null)
        {
            return;
        }
        BattleChangeEquipmentResult validation = ValidateChangeEquipmentCommand(activeUnit, command);
        if (!validation.Allowed)
        {
            preview.AddLogLine(
                string.IsNullOrEmpty(validation.Message) ? "换装命令无效。" : validation.Message
            );
            return;
        }

        EquipmentState equipmentView = activeUnit.DuplicateEquipmentView();
        WarehouseState backpackView = RuntimeState()?.GetPartyBackpackView()?.DuplicateState();
        BattleChangeEquipmentResult applyResult = ApplyChangeEquipmentToViews(
            command,
            validation,
            equipmentView,
            backpackView
        );
        if (applyResult.Allowed)
        {
            preview.allowed = true;
            preview.AddLogLine(
                string.IsNullOrEmpty(applyResult.Message) ? "换装可执行。" : applyResult.Message
            );
        }
        else
        {
            preview.AddLogLine(
                string.IsNullOrEmpty(applyResult.Message) ? "换装命令无效。" : applyResult.Message
            );
        }
    }

    private void HandleChangeEquipmentCommand(
        BattleUnitState activeUnit,
        BattleCommand command,
        BattleEventBatch batch
    )
    {
        BattleChangeEquipmentResult validation = ValidateChangeEquipmentCommand(activeUnit, command);
        if (!validation.Allowed)
        {
            AppendChangeEquipmentReport(batch, activeUnit, validation, false);
            return;
        }

        EquipmentState equipmentView = activeUnit.GetEquipmentView()?.DuplicateState();
        WarehouseState backpackView = RuntimeState()?.GetPartyBackpackView()?.DuplicateState();
        BattleChangeEquipmentResult applyResult = ApplyChangeEquipmentToViews(
            command,
            validation,
            equipmentView,
            backpackView
        );
        if (!applyResult.Allowed)
        {
            AppendChangeEquipmentReport(batch, activeUnit, applyResult, false);
            return;
        }

        int apBefore = activeUnit.current_ap;
        activeUnit.SetEquipmentView(equipmentView);
        RuntimeState()?.SetPartyBackpackView(backpackView);
        RefreshChangeEquipmentProjection(activeUnit, applyResult);
        activeUnit.SetCurrentAp(activeUnit.current_ap - CHANGE_EQUIPMENT_AP_COST);
        applyResult.ApBefore = apBefore;
        applyResult.ApAfter = activeUnit.current_ap;
        _runtime?._record_action_issued(
            activeUnit,
            BattleTypedNames.ToStringName(BattleCommandKind.ChangeEquipment),
            CHANGE_EQUIPMENT_AP_COST
        );
        if (batch != null && !batch.ContainsChangedUnitId(activeUnit.unit_id))
            batch.AddChangedUnitId(activeUnit.unit_id);
        AppendChangeEquipmentReport(batch, activeUnit, applyResult, true);
    }

    private void RefreshChangeEquipmentProjection(
        BattleUnitState activeUnit,
        BattleChangeEquipmentResult result
    )
    {
        if (activeUnit == null)
        {
            return;
        }
        int hpBefore = activeUnit.current_hp;
        int hpMaxBefore = ComputeUnitHpMax(activeUnit);
        if (
            !StringNameIsEmpty(activeUnit.source_member_id)
            && _runtime?.GetCharacterGatewayTyped() != null
        )
        {
            _runtime._unit_factory?.RefreshEquipmentProjection(activeUnit);
        }
        int hpMaxAfter = ComputeUnitHpMax(activeUnit);
        bool hpClamped = false;
        if (hpMaxAfter > 0 && hpMaxAfter < hpMaxBefore && hpBefore > hpMaxAfter)
        {
            activeUnit.SetCurrentHp(hpMaxAfter);
            hpClamped = true;
        }
        if (activeUnit.current_hp < 0)
        {
            activeUnit.MarkDead();
            hpClamped = true;
        }
        result.HpBefore = hpBefore;
        result.HpAfter = activeUnit.current_hp;
        result.HpMaxBefore = hpMaxBefore;
        result.HpMaxAfter = hpMaxAfter;
        result.HpClamped = hpClamped;
        result.WeaponProfileKind = activeUnit.weapon_profile_kind;
        result.WeaponItemId = activeUnit.weapon_item_id;
        result.WeaponProfileTypeId = activeUnit.weapon_profile_type_id;
        result.WeaponCurrentGrip = activeUnit.weapon_current_grip;
        result.WeaponAttackRange = activeUnit.weapon_attack_range;
        result.WeaponUsesTwoHands = activeUnit.weapon_uses_two_hands;
        result.WeaponPhysicalDamageTag = activeUnit.weapon_physical_damage_tag;
    }

    private int ComputeUnitHpMax(BattleUnitState unitState)
    {
        AttributeSnapshot snapshot = unitState?.attribute_snapshot;
        if (unitState == null || snapshot == null)
        {
            return 0;
        }
        return Math.Max(snapshot.GetValue(AttributeService.HP_MAX), 1);
    }

    private int ComputeUnitStaminaMax(BattleUnitState unitState)
    {
        AttributeSnapshot snapshot = unitState?.attribute_snapshot;
        if (unitState == null || snapshot == null)
        {
            return 0;
        }
        return Math.Max(snapshot.GetValue(AttributeService.STAMINA_MAX), 0);
    }

    private BattleChangeEquipmentResult ValidateChangeEquipmentCommand(
        BattleUnitState activeUnit,
        BattleCommand command
    )
    {
        BattleChangeEquipmentResult result = BuildChangeEquipmentResult(
            false,
            "invalid_command",
            "换装命令无效。",
            command
        );
        BattleState state = RuntimeState();
        if (state == null || activeUnit == null || command == null)
        {
            return result;
        }

        StringName operation = GetChangeEquipmentOperation(command);
        StringName slotId = GetChangeEquipmentSlotId(command);
        BattleEquipmentOperationKind operationKind =
            BattleTypedNames.ToEquipmentOperationKind(operation);
        result.Operation = operation;
        result.SlotId = slotId;
        result.TargetUnitId = ResolveChangeEquipmentTargetUnitId(activeUnit, command);
        result.ItemId = ResolveChangeEquipmentItemId(command);
        result.InstanceId = ResolveChangeEquipmentInstanceId(command);
        result.OccupiedSlotIds = ResolveChangeEquipmentOccupiedSlots(command, slotId);

        StringName targetUnitId = ResolveChangeEquipmentTargetUnitId(activeUnit, command);
        if (state.active_unit_id != activeUnit.unit_id)
        {
            return WithChangeEquipmentError(
                result,
                "target_not_self",
                "只能为当前行动单位自己换装。"
            );
        }
        if (targetUnitId != activeUnit.unit_id)
        {
            return WithChangeEquipmentError(
                result,
                "target_not_self",
                "只能为当前行动单位自己换装。"
            );
        }
        if (activeUnit.current_ap < CHANGE_EQUIPMENT_AP_COST)
        {
            return WithChangeEquipmentError(
                result,
                "ap_insufficient",
                $"AP不足，换装需要 {CHANGE_EQUIPMENT_AP_COST} 点 AP。"
            );
        }
        if (operationKind == BattleEquipmentOperationKind.Unknown)
        {
            return WithChangeEquipmentError(result, "operation_invalid", "换装操作无效。");
        }
        if (!EquipmentRules.IsValidSlot(slotId))
        {
            return WithChangeEquipmentError(result, "slot_invalid", $"装备槽无效：{slotId}。");
        }

        EquipmentState equipmentView = activeUnit.GetEquipmentView();
        if (equipmentView == null)
        {
            return WithChangeEquipmentError(
                result,
                "equipment_view_unavailable",
                "战斗内装备状态不可用。"
            );
        }
        WarehouseState backpackView = state.GetPartyBackpackView();
        if (backpackView == null)
        {
            return WithChangeEquipmentError(
                result,
                "backpack_view_unavailable",
                "战斗内背包状态不可用。"
            );
        }

        if (operationKind == BattleEquipmentOperationKind.Equip)
        {
            StringName instanceId = ResolveChangeEquipmentInstanceId(command);
            if (StringNameIsEmpty(instanceId))
            {
                return WithChangeEquipmentError(
                    result,
                    "equipment_instance_required",
                    "装备命令缺少装备实例。"
                );
            }
            int backpackIndex = FindBackpackEquipmentInstanceIndex(backpackView, instanceId);
            if (backpackIndex < 0)
            {
                return WithChangeEquipmentError(
                    result,
                    "equipment_instance_not_found",
                    $"战斗背包中找不到装备实例 {instanceId}。"
                );
            }
            EquipmentInstanceState backpackInstance = backpackView.GetEquipmentInstanceAt(
                backpackIndex
            );
            StringName resolvedItemId = ProgressionDataUtils.to_string_name(
                backpackInstance.item_id
            );
            StringName commandItemId = ResolveChangeEquipmentItemId(command);
            if (!StringNameIsEmpty(commandItemId) && commandItemId != resolvedItemId)
            {
                return WithChangeEquipmentError(
                    result,
                    "equipment_instance_item_mismatch",
                    "装备实例与命令物品不一致。"
                );
            }
            result.ItemId = resolvedItemId;
            ChangeEquipmentRuleResult itemRule = ResolveChangeEquipmentItemRule(
                resolvedItemId,
                slotId,
                command,
                activeUnit
            );
            if (!itemRule.Allowed)
            {
                return WithChangeEquipmentError(
                    result,
                    string.IsNullOrEmpty(itemRule.ErrorCode)
                        ? "item_not_equipment"
                        : itemRule.ErrorCode,
                    string.IsNullOrEmpty(itemRule.Message)
                        ? "装备实例不能放入该槽位。"
                        : itemRule.Message,
                    itemRule.OccupiedSlotIds
                );
            }
            result.OccupiedSlotIds = new List<StringName>(itemRule.OccupiedSlotIds);
        }
        else
        {
            StringName entrySlot = ProgressionDataUtils.to_string_name(
                equipmentView.GetEntrySlotForSlot(slotId)
            );
            if (StringNameIsEmpty(entrySlot))
            {
                return WithChangeEquipmentError(
                    result,
                    "slot_empty",
                    $"{EquipmentRules.GetSlotLabel(slotId)} 当前没有已装备物品。"
                );
            }
            StringName equippedInstanceId = ProgressionDataUtils.to_string_name(
                equipmentView.GetEquippedInstanceId(slotId)
            );
            StringName commandInstanceId = ResolveChangeEquipmentInstanceId(command);
            if (
                !StringNameIsEmpty(commandInstanceId)
                && !StringNameIsEmpty(equippedInstanceId)
                && commandInstanceId != equippedInstanceId
            )
            {
                return WithChangeEquipmentError(
                    result,
                    "equipment_instance_item_mismatch",
                    "装备实例与当前槽位不一致。"
                );
            }
            result.ItemId = ProgressionDataUtils.to_string_name(
                equipmentView.GetEquippedItemId(slotId)
            );
            result.InstanceId = equippedInstanceId;
            result.OccupiedSlotIds = new List<StringName>(
                equipmentView.GetOccupiedSlotIdsForEntryTyped(entrySlot)
            );
        }

        result.Allowed = true;
        result.ErrorCode = "";
        result.Message = BuildChangeEquipmentSuccessMessage(activeUnit, result);
        return result;
    }

    private BattleChangeEquipmentResult ValidateChangeEquipmentCommand(
        BattleUnitReadView activeUnit,
        BattleCommand command
    )
    {
        BattleChangeEquipmentResult result = BuildChangeEquipmentResult(
            false,
            "invalid_command",
            "换装命令无效。",
            command
        );
        BattleState state = RuntimeState();
        if (state == null || !activeUnit.IsValid || command == null)
        {
            return result;
        }

        StringName operation = GetChangeEquipmentOperation(command);
        StringName slotId = GetChangeEquipmentSlotId(command);
        BattleEquipmentOperationKind operationKind =
            BattleTypedNames.ToEquipmentOperationKind(operation);
        result.Operation = operation;
        result.SlotId = slotId;
        result.TargetUnitId = ResolveChangeEquipmentTargetUnitId(activeUnit, command);
        result.ItemId = ResolveChangeEquipmentItemId(command);
        result.InstanceId = ResolveChangeEquipmentInstanceId(command);
        result.OccupiedSlotIds = ResolveChangeEquipmentOccupiedSlots(command, slotId);

        StringName targetUnitId = ResolveChangeEquipmentTargetUnitId(activeUnit, command);
        if (state.active_unit_id != activeUnit.UnitId)
        {
            return WithChangeEquipmentError(
                result,
                "target_not_self",
                "只能为当前行动单位自己换装。"
            );
        }
        if (targetUnitId != activeUnit.UnitId)
        {
            return WithChangeEquipmentError(
                result,
                "target_not_self",
                "只能为当前行动单位自己换装。"
            );
        }
        if (activeUnit.CurrentAp < CHANGE_EQUIPMENT_AP_COST)
        {
            return WithChangeEquipmentError(
                result,
                "ap_insufficient",
                $"AP不足，换装需要 {CHANGE_EQUIPMENT_AP_COST} 点 AP。"
            );
        }
        if (operationKind == BattleEquipmentOperationKind.Unknown)
        {
            return WithChangeEquipmentError(result, "operation_invalid", "换装操作无效。");
        }
        if (!EquipmentRules.IsValidSlot(slotId))
        {
            return WithChangeEquipmentError(result, "slot_invalid", $"装备槽无效：{slotId}。");
        }

        EquipmentState equipmentView = activeUnit.DuplicateEquipmentView();
        if (equipmentView == null)
        {
            return WithChangeEquipmentError(
                result,
                "equipment_view_unavailable",
                "战斗内装备状态不可用。"
            );
        }
        WarehouseState backpackView = state.GetPartyBackpackView();
        if (backpackView == null)
        {
            return WithChangeEquipmentError(
                result,
                "backpack_view_unavailable",
                "战斗内背包状态不可用。"
            );
        }

        if (operationKind == BattleEquipmentOperationKind.Equip)
        {
            StringName instanceId = ResolveChangeEquipmentInstanceId(command);
            if (StringNameIsEmpty(instanceId))
            {
                return WithChangeEquipmentError(
                    result,
                    "equipment_instance_required",
                    "装备命令缺少装备实例。"
                );
            }
            int backpackIndex = FindBackpackEquipmentInstanceIndex(backpackView, instanceId);
            if (backpackIndex < 0)
            {
                return WithChangeEquipmentError(
                    result,
                    "equipment_instance_not_found",
                    $"战斗背包中找不到装备实例 {instanceId}。"
                );
            }
            EquipmentInstanceState backpackInstance = backpackView.GetEquipmentInstanceAt(
                backpackIndex
            );
            StringName resolvedItemId = ProgressionDataUtils.to_string_name(
                backpackInstance.item_id
            );
            StringName commandItemId = ResolveChangeEquipmentItemId(command);
            if (!StringNameIsEmpty(commandItemId) && commandItemId != resolvedItemId)
            {
                return WithChangeEquipmentError(
                    result,
                    "equipment_instance_item_mismatch",
                    "装备实例与命令物品不一致。"
                );
            }
            result.ItemId = resolvedItemId;
            ChangeEquipmentRuleResult itemRule = ResolveChangeEquipmentItemRule(
                resolvedItemId,
                slotId,
                command,
                activeUnit
            );
            if (!itemRule.Allowed)
            {
                return WithChangeEquipmentError(
                    result,
                    string.IsNullOrEmpty(itemRule.ErrorCode)
                        ? "item_not_equipment"
                        : itemRule.ErrorCode,
                    string.IsNullOrEmpty(itemRule.Message)
                        ? "装备实例不能放入该槽位。"
                        : itemRule.Message,
                    itemRule.OccupiedSlotIds
                );
            }
            result.OccupiedSlotIds = new List<StringName>(itemRule.OccupiedSlotIds);
        }
        else
        {
            StringName entrySlot = ProgressionDataUtils.to_string_name(
                equipmentView.GetEntrySlotForSlot(slotId)
            );
            if (StringNameIsEmpty(entrySlot))
            {
                return WithChangeEquipmentError(
                    result,
                    "slot_empty",
                    $"{EquipmentRules.GetSlotLabel(slotId)} 当前没有已装备物品。"
                );
            }
            StringName equippedInstanceId = ProgressionDataUtils.to_string_name(
                equipmentView.GetEquippedInstanceId(slotId)
            );
            StringName commandInstanceId = ResolveChangeEquipmentInstanceId(command);
            if (
                !StringNameIsEmpty(commandInstanceId)
                && !StringNameIsEmpty(equippedInstanceId)
                && commandInstanceId != equippedInstanceId
            )
            {
                return WithChangeEquipmentError(
                    result,
                    "equipment_instance_item_mismatch",
                    "装备实例与当前槽位不一致。"
                );
            }
            result.ItemId = ProgressionDataUtils.to_string_name(
                equipmentView.GetEquippedItemId(slotId)
            );
            result.InstanceId = equippedInstanceId;
            result.OccupiedSlotIds = new List<StringName>(
                equipmentView.GetOccupiedSlotIdsForEntryTyped(entrySlot)
            );
        }

        result.Allowed = true;
        result.ErrorCode = "";
        result.Message = BuildChangeEquipmentSuccessMessage(activeUnit, result);
        return result;
    }

    private BattleChangeEquipmentResult ApplyChangeEquipmentToViews(
        BattleCommand command,
        BattleChangeEquipmentResult validation,
        EquipmentState equipmentView,
        WarehouseState backpackView
    )
    {
        if (equipmentView == null || backpackView == null)
        {
            return BuildChangeEquipmentResult(
                false,
                "state_unavailable",
                "战斗内换装状态不可用。",
                command
            );
        }

        StringName operation = validation.Operation;
        StringName slotId = validation.SlotId;
        StringName itemId = validation.ItemId;
        StringName instanceId = validation.InstanceId;
        BattleEquipmentOperationKind operationKind =
            BattleTypedNames.ToEquipmentOperationKind(operation);
        BattleChangeEquipmentResult result = validation.Clone();

        if (operationKind == BattleEquipmentOperationKind.Equip)
        {
            int backpackIndex = FindBackpackEquipmentInstanceIndex(backpackView, instanceId);
            if (backpackIndex < 0)
            {
                return WithChangeEquipmentError(
                    result,
                    "equipment_instance_not_found",
                    $"战斗背包中找不到装备实例 {instanceId}。"
                );
            }
            EquipmentInstanceState newInstance = backpackView.GetEquipmentInstanceAt(backpackIndex);
            itemId = newInstance.item_id;
            backpackView.RemoveEquipmentInstanceAt(backpackIndex);

            IReadOnlyList<StringName> occupiedSlots = validation.OccupiedSlotIds;
            if (occupiedSlots.Count == 0)
            {
                occupiedSlots = new List<StringName> { slotId };
            }
            var displacedEntrySlots = new HashSet<StringName>();
            foreach (StringName occupiedSlotId in occupiedSlots)
            {
                StringName existingEntrySlot = ProgressionDataUtils.to_string_name(
                    equipmentView.GetEntrySlotForSlot(occupiedSlotId)
                );
                if (
                    StringNameIsEmpty(existingEntrySlot)
                    || displacedEntrySlots.Contains(existingEntrySlot)
                )
                {
                    continue;
                }
                displacedEntrySlots.Add(existingEntrySlot);
                EquipmentInstanceState displacedInstance = equipmentView.PopEquippedInstance(
                    existingEntrySlot
                );
                if (displacedInstance != null)
                {
                    if (
                        BackpackHasEquipmentInstance(
                            backpackView,
                            displacedInstance.instance_id
                        )
                    )
                    {
                        return WithChangeEquipmentError(
                            result,
                            "equipment_instance_already_in_backpack",
                            $"战斗背包中已存在装备实例 {displacedInstance.instance_id}。"
                        );
                    }
                    backpackView.AddEquipmentInstance(displacedInstance);
                }
            }
            equipmentView.SetEquippedEntry(slotId, itemId, occupiedSlots, newInstance);
            result.ItemId = itemId;
            result.InstanceId = instanceId;
            result.OccupiedSlotIds = new List<StringName>(occupiedSlots);
        }
        else
        {
            StringName entrySlot = ProgressionDataUtils.to_string_name(
                equipmentView.GetEntrySlotForSlot(slotId)
            );
            if (StringNameIsEmpty(entrySlot))
            {
                return WithChangeEquipmentError(
                    result,
                    "slot_empty",
                    $"{EquipmentRules.GetSlotLabel(slotId)} 当前没有已装备物品。"
                );
            }
            EquipmentInstanceState removedInstance = equipmentView.PopEquippedInstance(entrySlot);
            if (removedInstance == null)
            {
                return WithChangeEquipmentError(
                    result,
                    "slot_empty",
                    $"{EquipmentRules.GetSlotLabel(slotId)} 当前没有已装备物品。"
                );
            }
            StringName removedInstanceId = removedInstance.instance_id;
            if (
                !StringNameIsEmpty(removedInstanceId)
                && BackpackHasEquipmentInstance(backpackView, removedInstanceId)
            )
            {
                return WithChangeEquipmentError(
                    result,
                    "equipment_instance_already_in_backpack",
                    $"战斗背包中已存在装备实例 {removedInstanceId}。"
                );
            }
            backpackView.AddEquipmentInstance(removedInstance);
            result.ItemId = removedInstance.item_id;
            result.InstanceId = removedInstanceId;
        }

        ChangeEquipmentRuleResult ownershipResult = ValidateChangeEquipmentInstanceOwnership(
            equipmentView,
            backpackView
        );
        if (!ownershipResult.Allowed)
        {
            return WithChangeEquipmentError(
                result,
                string.IsNullOrEmpty(ownershipResult.ErrorCode)
                    ? "equipment_instance_write_failed"
                    : ownershipResult.ErrorCode,
                string.IsNullOrEmpty(ownershipResult.Message)
                    ? "装备实例写入失败。"
                    : ownershipResult.Message
            );
        }
        ChangeEquipmentRuleResult capacityResult = ValidateChangeEquipmentBackpackCapacity(backpackView);
        if (!capacityResult.Allowed)
        {
            return WithChangeEquipmentError(
                result,
                string.IsNullOrEmpty(capacityResult.ErrorCode)
                    ? "backpack_capacity_exceeded"
                    : capacityResult.ErrorCode,
                string.IsNullOrEmpty(capacityResult.Message)
                    ? "战斗背包容量不足。"
                    : capacityResult.Message
            );
        }

        result.Allowed = true;
        result.ErrorCode = "";
        result.Message = BuildChangeEquipmentSuccessMessage(null, result);
        return result;
    }

    internal BattleChangeEquipmentResult BuildChangeEquipmentResult(
        bool allowed,
        string errorCode,
        string message,
        BattleCommand command
    )
    {
        StringName slotId = GetChangeEquipmentSlotId(command);
        return new BattleChangeEquipmentResult
        {
            Allowed = allowed,
            ErrorCode = errorCode ?? "",
            Message = message ?? "",
            Operation = GetChangeEquipmentOperation(command),
            SlotId = slotId,
            TargetUnitId = "",
            ItemId = ResolveChangeEquipmentItemId(command),
            InstanceId = ResolveChangeEquipmentInstanceId(command),
            OccupiedSlotIds = ResolveChangeEquipmentOccupiedSlots(command, slotId),
        };
    }

    private BattleChangeEquipmentResult WithChangeEquipmentError(
        BattleChangeEquipmentResult result,
        string errorCode,
        string message
    )
    {
        return WithChangeEquipmentError(result, errorCode, message, null);
    }

    private BattleChangeEquipmentResult WithChangeEquipmentError(
        BattleChangeEquipmentResult result,
        string errorCode,
        string message,
        IReadOnlyList<StringName> occupiedSlotIds
    )
    {
        BattleChangeEquipmentResult output = result?.Clone() ?? new BattleChangeEquipmentResult();
        if (occupiedSlotIds != null)
        {
            output.OccupiedSlotIds = new List<StringName>(occupiedSlotIds);
        }
        output.Allowed = false;
        output.ErrorCode = errorCode ?? "";
        output.Message = message ?? "";
        return output;
    }

    internal void AppendChangeEquipmentReport(
        BattleEventBatch batch,
        BattleUnitState activeUnit,
        BattleChangeEquipmentResult result,
        bool success
    )
    {
        result ??= new BattleChangeEquipmentResult();
        bool hasUnit = activeUnit != null;
        int hpBefore = result.HpBefore != 0 || !hasUnit ? result.HpBefore : activeUnit.current_hp;
        int hpAfter = result.HpAfter != 0 || !hasUnit ? result.HpAfter : activeUnit.current_hp;
        int hpMaxBefore =
            result.HpMaxBefore != 0 || !hasUnit ? result.HpMaxBefore : ComputeUnitHpMax(activeUnit);
        int hpMaxAfter =
            result.HpMaxAfter != 0 || !hasUnit ? result.HpMaxAfter : ComputeUnitHpMax(activeUnit);
        var reportEntry = new GDictionary
        {
            ["type"] = "change_equipment",
            ["ok"] = success,
            ["error_code"] = success ? "" : (result.ErrorCode ?? "change_equipment_failed"),
            ["event_tags"] = new GArray { "equipment", "change_equipment" },
            ["unit_id"] = hasUnit ? activeUnit.unit_id.ToString() : "",
            ["target_unit_id"] = result.TargetUnitId.ToString(),
            ["operation"] = result.Operation.ToString(),
            ["slot_id"] = result.SlotId.ToString(),
            ["slot_label"] = EquipmentRules.GetSlotLabel(result.SlotId),
            ["item_id"] = result.ItemId.ToString(),
            ["instance_id"] = result.InstanceId.ToString(),
            ["ap_cost"] = success ? CHANGE_EQUIPMENT_AP_COST : 0,
            ["ap_before"] = result.ApBefore,
            ["ap_after"] = success ? result.ApAfter : (hasUnit ? activeUnit.current_ap : 0),
            ["hp_before"] = hpBefore,
            ["hp_after"] = hpAfter,
            ["hp_max_before"] = hpMaxBefore,
            ["hp_max_after"] = hpMaxAfter,
            ["hp_clamped"] = result.HpClamped,
            ["weapon_profile_kind"] = !StringNameIsEmpty(result.WeaponProfileKind)
                ? result.WeaponProfileKind.ToString()
                : (hasUnit ? activeUnit.weapon_profile_kind.ToString() : ""),
            ["weapon_item_id"] = !StringNameIsEmpty(result.WeaponItemId)
                ? result.WeaponItemId.ToString()
                : (hasUnit ? activeUnit.weapon_item_id.ToString() : ""),
            ["weapon_profile_type_id"] = !StringNameIsEmpty(result.WeaponProfileTypeId)
                ? result.WeaponProfileTypeId.ToString()
                : (hasUnit ? activeUnit.weapon_profile_type_id.ToString() : ""),
            ["weapon_current_grip"] = !StringNameIsEmpty(result.WeaponCurrentGrip)
                ? result.WeaponCurrentGrip.ToString()
                : (hasUnit ? activeUnit.weapon_current_grip.ToString() : ""),
            ["weapon_attack_range"] = result.WeaponAttackRange != 0 || !hasUnit
                ? result.WeaponAttackRange
                : activeUnit.weapon_attack_range,
            ["weapon_uses_two_hands"] = result.WeaponUsesTwoHands
                || (hasUnit && activeUnit.weapon_uses_two_hands),
            ["weapon_physical_damage_tag"] = !StringNameIsEmpty(result.WeaponPhysicalDamageTag)
                ? result.WeaponPhysicalDamageTag.ToString()
                : (hasUnit ? activeUnit.weapon_physical_damage_tag.ToString() : ""),
            ["text"] = string.IsNullOrEmpty(result.Message) ? "换装命令无效。" : result.Message,
        };
        _runtime?._append_report_entry_to_batch(batch, reportEntry);
    }

    private string BuildChangeEquipmentSuccessMessage(
        BattleUnitState activeUnit,
        BattleChangeEquipmentResult result
    )
    {
        string unitName =
            activeUnit != null && !string.IsNullOrEmpty(activeUnit.display_name)
                ? activeUnit.display_name
                : result?.TargetUnitId.ToString() ?? "";
        if (string.IsNullOrEmpty(unitName))
        {
            unitName = "当前单位";
        }
        StringName operation = result?.Operation ?? new StringName("");
        BattleEquipmentOperationKind operationKind =
            BattleTypedNames.ToEquipmentOperationKind(operation);
        string slotLabel = EquipmentRules.GetSlotLabel(result?.SlotId ?? new StringName(""));
        string itemId = result?.ItemId.ToString() ?? "";
        string instanceId = result?.InstanceId.ToString() ?? "";
        if (operationKind == BattleEquipmentOperationKind.Equip)
        {
            return $"{unitName} 换装：{slotLabel} 装备 {itemId}（实例 {instanceId}），消耗 {CHANGE_EQUIPMENT_AP_COST} AP。";
        }
        return $"{unitName} 换装：卸下 {slotLabel} 的 {itemId}（实例 {instanceId}），消耗 {CHANGE_EQUIPMENT_AP_COST} AP。";
    }

    private string BuildChangeEquipmentSuccessMessage(
        BattleUnitReadView activeUnit,
        BattleChangeEquipmentResult result
    )
    {
        string unitName =
            activeUnit.IsValid && !string.IsNullOrEmpty(activeUnit.DisplayName)
                ? activeUnit.DisplayName
                : result?.TargetUnitId.ToString() ?? "";
        if (string.IsNullOrEmpty(unitName))
        {
            unitName = "当前单位";
        }
        StringName operation = result?.Operation ?? new StringName("");
        BattleEquipmentOperationKind operationKind =
            BattleTypedNames.ToEquipmentOperationKind(operation);
        string slotLabel = EquipmentRules.GetSlotLabel(result?.SlotId ?? new StringName(""));
        string itemId = result?.ItemId.ToString() ?? "";
        string instanceId = result?.InstanceId.ToString() ?? "";
        if (operationKind == BattleEquipmentOperationKind.Equip)
        {
            return $"{unitName} 换装：{slotLabel} 装备 {itemId}（实例 {instanceId}），消耗 {CHANGE_EQUIPMENT_AP_COST} AP。";
        }
        return $"{unitName} 换装：卸下 {slotLabel} 的 {itemId}（实例 {instanceId}），消耗 {CHANGE_EQUIPMENT_AP_COST} AP。";
    }

    private StringName GetChangeEquipmentOperation(BattleCommand command)
    {
        if (command == null)
        {
            return "";
        }
        return BattleTypedNames.ToStringName(command.EquipmentOperationKind);
    }

    private StringName GetChangeEquipmentSlotId(BattleCommand command)
    {
        if (command == null)
        {
            return "";
        }
        return ProgressionDataUtils.to_string_name(command.equipment_slot_id);
    }

    private StringName ResolveChangeEquipmentTargetUnitId(
        BattleUnitState activeUnit,
        BattleCommand command
    )
    {
        if (command == null)
        {
            return "";
        }
        StringName explicitTarget = ProgressionDataUtils.to_string_name(command.target_unit_id);
        if (!StringNameIsEmpty(explicitTarget))
        {
            return explicitTarget;
        }
        return activeUnit != null
            ? activeUnit.unit_id
            : new StringName("");
    }

    private StringName ResolveChangeEquipmentTargetUnitId(
        BattleUnitReadView activeUnit,
        BattleCommand command
    )
    {
        if (command == null)
        {
            return "";
        }
        StringName explicitTarget = ProgressionDataUtils.to_string_name(command.target_unit_id);
        if (!StringNameIsEmpty(explicitTarget))
        {
            return explicitTarget;
        }
        return activeUnit.IsValid ? activeUnit.UnitId : new StringName("");
    }

    private StringName ResolveChangeEquipmentItemId(BattleCommand command)
    {
        if (command == null)
        {
            return "";
        }
        StringName itemId = ProgressionDataUtils.to_string_name(command.equipment_item_id);
        if (!StringNameIsEmpty(itemId))
        {
            return itemId;
        }
        return "";
    }

    private StringName ResolveChangeEquipmentInstanceId(BattleCommand command)
    {
        if (command == null)
        {
            return "";
        }
        StringName instanceId = ProgressionDataUtils.to_string_name(command.equipment_instance_id);
        if (!StringNameIsEmpty(instanceId))
        {
            return instanceId;
        }
        return "";
    }

    private List<StringName> ResolveChangeEquipmentOccupiedSlots(
        BattleCommand command,
        StringName slotId
    )
    {
        var occupiedSlots = new List<StringName>();
        if (command != null)
        {
            occupiedSlots = new List<StringName>(
                EquipmentRules.NormalizeSlotIdsTyped(command.EquipmentOccupiedSlotIdsTyped)
            );
        }
        StringName normSlot = ProgressionDataUtils.to_string_name(slotId);
        if (occupiedSlots.Count == 0 && EquipmentRules.IsValidSlot(normSlot))
        {
            occupiedSlots.Add(normSlot);
        }
        else if (EquipmentRules.IsValidSlot(normSlot) && !occupiedSlots.Contains(normSlot))
        {
            occupiedSlots.Insert(0, normSlot);
        }
        return occupiedSlots;
    }

    private ChangeEquipmentRuleResult ResolveChangeEquipmentItemRule(
        StringName itemId,
        StringName slotId,
        BattleCommand command,
        BattleUnitState activeUnit
    )
    {
        StringName normItem = ProgressionDataUtils.to_string_name(itemId);
        StringName normSlot = ProgressionDataUtils.to_string_name(slotId);
        List<StringName> fallbackOccupied = ResolveChangeEquipmentOccupiedSlots(command, normSlot);
        ItemDef itemDef = GetChangeEquipmentItemDef(normItem);
        if (itemDef == null)
        {
            if (HasChangeEquipmentItemCatalog())
            {
                return new ChangeEquipmentRuleResult(
                    false,
                    "item_not_found",
                    $"找不到装备定义：{normItem}。",
                    fallbackOccupied
                );
            }
            return new ChangeEquipmentRuleResult(true, occupiedSlotIds: fallbackOccupied);
        }
        if (!itemDef.IsEquipment())
        {
            return new ChangeEquipmentRuleResult(
                false,
                "item_not_equipment",
                $"{normItem} 不是可装备物品。",
                fallbackOccupied
            );
        }
        List<StringName> allowedSlots = itemDef.GetEquipmentSlotIdsTyped();
        if (!allowedSlots.Contains(normSlot))
        {
            return new ChangeEquipmentRuleResult(
                false,
                "slot_not_allowed",
                $"{normItem} 不能装备到 {EquipmentRules.GetSlotLabel(normSlot)}。",
                fallbackOccupied
            );
        }
        ChangeEquipmentRuleResult requirementRule = ResolveChangeEquipmentRequirementRule(
            activeUnit,
            itemDef,
            normItem
        );
        if (!requirementRule.Allowed)
        {
            return requirementRule;
        }
        var occupiedSlots = new List<StringName>(itemDef.GetFinalOccupiedSlotIdsTyped(normSlot));
        if (occupiedSlots.Count == 0)
        {
            occupiedSlots = new List<StringName> { normSlot };
        }
        else if (!occupiedSlots.Contains(normSlot))
        {
            occupiedSlots.Insert(0, normSlot);
        }
        return new ChangeEquipmentRuleResult(true, occupiedSlotIds: occupiedSlots);
    }

    private ChangeEquipmentRuleResult ResolveChangeEquipmentItemRule(
        StringName itemId,
        StringName slotId,
        BattleCommand command,
        BattleUnitReadView activeUnit
    )
    {
        StringName normItem = ProgressionDataUtils.to_string_name(itemId);
        StringName normSlot = ProgressionDataUtils.to_string_name(slotId);
        List<StringName> fallbackOccupied = ResolveChangeEquipmentOccupiedSlots(command, normSlot);
        ItemDef itemDef = GetChangeEquipmentItemDef(normItem);
        if (itemDef == null)
        {
            if (HasChangeEquipmentItemCatalog())
            {
                return new ChangeEquipmentRuleResult(
                    false,
                    "item_not_found",
                    $"找不到装备定义：{normItem}。",
                    fallbackOccupied
                );
            }
            return new ChangeEquipmentRuleResult(true, occupiedSlotIds: fallbackOccupied);
        }
        if (!itemDef.IsEquipment())
        {
            return new ChangeEquipmentRuleResult(
                false,
                "item_not_equipment",
                $"{normItem} 不是可装备物品。",
                fallbackOccupied
            );
        }
        List<StringName> allowedSlots = itemDef.GetEquipmentSlotIdsTyped();
        if (!allowedSlots.Contains(normSlot))
        {
            return new ChangeEquipmentRuleResult(
                false,
                "slot_not_allowed",
                $"{normItem} 不能装备到 {EquipmentRules.GetSlotLabel(normSlot)}。",
                fallbackOccupied
            );
        }
        ChangeEquipmentRuleResult requirementRule = ResolveChangeEquipmentRequirementRule(
            activeUnit,
            itemDef,
            normItem
        );
        if (!requirementRule.Allowed)
        {
            return requirementRule;
        }
        var occupiedSlots = new List<StringName>(itemDef.GetFinalOccupiedSlotIdsTyped(normSlot));
        if (occupiedSlots.Count == 0)
        {
            occupiedSlots = new List<StringName> { normSlot };
        }
        else if (!occupiedSlots.Contains(normSlot))
        {
            occupiedSlots.Insert(0, normSlot);
        }
        return new ChangeEquipmentRuleResult(true, occupiedSlotIds: occupiedSlots);
    }

    private ChangeEquipmentRuleResult ResolveChangeEquipmentRequirementRule(
        BattleUnitState activeUnit,
        ItemDef itemDef,
        StringName itemId
    )
    {
        if (itemDef == null)
        {
            return new ChangeEquipmentRuleResult(true);
        }
        EquipmentRequirement equipReq = itemDef.equip_requirement as EquipmentRequirement;
        if (equipReq == null)
        {
            return new ChangeEquipmentRuleResult(true);
        }
        string itemLabel = GetChangeEquipmentItemDisplayName(itemDef, itemId);
        if (
            activeUnit == null
            || StringNameIsEmpty(activeUnit.source_member_id)
        )
        {
            return new ChangeEquipmentRuleResult(
                false,
                "item_not_equippable",
                BuildChangeEquipmentRequirementFailureMessage(itemLabel)
            );
        }
        IBattleRuntimeCharacterGateway characterGateway = _runtime?.GetCharacterGatewayTyped();
        if (characterGateway == null)
        {
            return new ChangeEquipmentRuleResult(
                false,
                "item_not_equippable",
                BuildChangeEquipmentRequirementFailureMessage(itemLabel)
            );
        }
        PartyMemberState memberState = characterGateway.GetMemberState(activeUnit.source_member_id);
        if (memberState == null)
        {
            return new ChangeEquipmentRuleResult(
                false,
                "item_not_equippable",
                BuildChangeEquipmentRequirementFailureMessage(itemLabel)
            );
        }
        if (equipReq.CheckResult(memberState).Allowed)
        {
            return new ChangeEquipmentRuleResult(true);
        }
        return new ChangeEquipmentRuleResult(
            false,
            "item_not_equippable",
            BuildChangeEquipmentRequirementFailureMessage(itemLabel)
        );
    }

    private ChangeEquipmentRuleResult ResolveChangeEquipmentRequirementRule(
        BattleUnitReadView activeUnit,
        ItemDef itemDef,
        StringName itemId
    )
    {
        if (itemDef == null)
        {
            return new ChangeEquipmentRuleResult(true);
        }
        EquipmentRequirement equipReq = itemDef.equip_requirement as EquipmentRequirement;
        if (equipReq == null)
        {
            return new ChangeEquipmentRuleResult(true);
        }
        string itemLabel = GetChangeEquipmentItemDisplayName(itemDef, itemId);
        if (
            !activeUnit.IsValid
            || StringNameIsEmpty(activeUnit.SourceMemberId)
        )
        {
            return new ChangeEquipmentRuleResult(
                false,
                "item_not_equippable",
                BuildChangeEquipmentRequirementFailureMessage(itemLabel)
            );
        }
        IBattleRuntimeCharacterGateway characterGateway = _runtime?.GetCharacterGatewayTyped();
        if (characterGateway == null)
        {
            return new ChangeEquipmentRuleResult(
                false,
                "item_not_equippable",
                BuildChangeEquipmentRequirementFailureMessage(itemLabel)
            );
        }
        PartyMemberState memberState = characterGateway.GetMemberState(activeUnit.SourceMemberId);
        if (memberState == null)
        {
            return new ChangeEquipmentRuleResult(
                false,
                "item_not_equippable",
                BuildChangeEquipmentRequirementFailureMessage(itemLabel)
            );
        }
        if (equipReq.CheckResult(memberState).Allowed)
        {
            return new ChangeEquipmentRuleResult(true);
        }
        return new ChangeEquipmentRuleResult(
            false,
            "item_not_equippable",
            BuildChangeEquipmentRequirementFailureMessage(itemLabel)
        );
    }

    private string BuildChangeEquipmentRequirementFailureMessage(string itemLabel)
    {
        return $"当前无法装备 {itemLabel}。";
    }

    private string GetChangeEquipmentItemDisplayName(ItemDef itemDef, StringName itemId)
    {
        if (itemDef != null && !string.IsNullOrEmpty(itemDef.display_name))
        {
            return itemDef.display_name;
        }
        return itemId.ToString();
    }

    private ItemDef GetChangeEquipmentItemDef(StringName itemId)
    {
        StringName normalized = ProgressionDataUtils.to_string_name(itemId);
        IBattleRuntimeCharacterGateway characterGateway = _runtime?.GetCharacterGatewayTyped();
        if (StringNameIsEmpty(normalized) || characterGateway == null)
        {
            return null;
        }
        return characterGateway.GetItemDef(normalized);
    }

    private bool HasChangeEquipmentItemCatalog()
    {
        return _runtime?.GetCharacterGatewayTyped()?.HasItemDefCatalog() ?? false;
    }

    private ChangeEquipmentRuleResult ValidateChangeEquipmentInstanceOwnership(
        EquipmentState equipmentView,
        WarehouseState backpackView
    )
    {
        var owners = new Dictionary<StringName, string>();
        if (backpackView == null)
        {
            return new ChangeEquipmentRuleResult(
                false,
                "backpack_view_unavailable",
                "战斗内背包状态不可用。"
            );
        }
        foreach (EquipmentInstanceState instance in backpackView.GetNonEmptyEquipmentInstancesTyped())
        {
            StringName instanceId = ProgressionDataUtils.to_string_name(
                instance != null ? instance.instance_id : new StringName("")
            );
            StringName itemId = ProgressionDataUtils.to_string_name(
                instance != null ? instance.item_id : new StringName("")
            );
            ChangeEquipmentRuleResult ownerResult = ClaimChangeEquipmentInstanceOwner(
                owners,
                instanceId,
                itemId,
                "backpack"
            );
            if (!ownerResult.Allowed)
            {
                return ownerResult;
            }
        }
        if (equipmentView == null)
        {
            return new ChangeEquipmentRuleResult(
                false,
                "equipment_view_unavailable",
                "战斗内装备状态不可用。"
            );
        }
        foreach (StringName entrySlotId in equipmentView.GetEntrySlotIdsTyped())
        {
            StringName itemId = ProgressionDataUtils.to_string_name(
                equipmentView.GetEquippedItemId(entrySlotId)
            );
            StringName instanceId = ProgressionDataUtils.to_string_name(
                equipmentView.GetEquippedInstanceId(entrySlotId)
            );
            string ownerName = $"equipment:{entrySlotId}";
            ChangeEquipmentRuleResult ownerResult = ClaimChangeEquipmentInstanceOwner(
                owners,
                instanceId,
                itemId,
                ownerName
            );
            if (!ownerResult.Allowed)
            {
                return ownerResult;
            }
        }
        return new ChangeEquipmentRuleResult(true);
    }

    private ChangeEquipmentRuleResult ClaimChangeEquipmentInstanceOwner(
        Dictionary<StringName, string> owners,
        StringName instanceId,
        StringName itemId,
        string ownerName
    )
    {
        if (StringNameIsEmpty(instanceId) || StringNameIsEmpty(itemId))
        {
            return new ChangeEquipmentRuleResult(
                false,
                "equipment_instance_write_failed",
                $"装备实例写入失败：{ownerName} 存在空实例或空物品。"
            );
        }
        if (owners.ContainsKey(instanceId))
        {
            return new ChangeEquipmentRuleResult(
                false,
                "equipment_instance_duplicate_owner",
                $"装备实例 {instanceId} 同时存在于多个位置。"
            );
        }
        owners[instanceId] = ownerName;
        return new ChangeEquipmentRuleResult(true);
    }

    private ChangeEquipmentRuleResult ValidateChangeEquipmentBackpackCapacity(
        WarehouseState backpackView
    )
    {
        int capacity = GetChangeEquipmentBackpackCapacity();
        if (capacity < 0)
        {
            return new ChangeEquipmentRuleResult(true);
        }
        int usedSlots = GetChangeEquipmentBackpackUsedSlots(backpackView);
        if (usedSlots <= capacity)
        {
            return new ChangeEquipmentRuleResult(true);
        }
        return new ChangeEquipmentRuleResult(
            false,
            "backpack_capacity_exceeded",
            $"战斗背包容量不足：需要 {usedSlots} 格，当前容量 {capacity} 格。"
        );
    }

    private int GetChangeEquipmentBackpackCapacity()
    {
        PartyState partyState = _runtime?.GetCharacterGatewayTyped()?.GetPartyState();
        if (partyState == null)
        {
            return -1;
        }
        int totalCapacity = 0;
        foreach (PartyMemberState memberState in partyState.GetMemberStates())
        {
            UnitProgress progression = memberState?.progression;
            if (progression == null)
            {
                continue;
            }
            UnitBaseAttributes unitBaseAttributes = progression.unit_base_attributes;
            if (unitBaseAttributes == null)
            {
                continue;
            }
            totalCapacity += Math.Max(
                unitBaseAttributes.GetAttributeValue(new StringName("storage_space")),
                0
            );
        }
        return Math.Max(totalCapacity, 0);
    }

    private int GetChangeEquipmentBackpackUsedSlots(WarehouseState backpackView)
    {
        if (backpackView == null)
        {
            return 0;
        }
        return backpackView.GetNonEmptyStacksTyped().Count
            + backpackView.GetNonEmptyEquipmentInstancesTyped().Count;
    }

    private int FindBackpackEquipmentInstanceIndex(WarehouseState backpackView, StringName instanceId)
    {
        StringName normalizedId = ProgressionDataUtils.to_string_name(instanceId);
        if (backpackView == null || StringNameIsEmpty(normalizedId))
        {
            return -1;
        }
        IReadOnlyList<EquipmentInstanceState> instances = backpackView.GetEquipmentInstancesTyped();
        for (int index = 0; index < instances.Count; index++)
        {
            EquipmentInstanceState instance = instances[index];
            if (instance == null)
            {
                continue;
            }
            if (ProgressionDataUtils.to_string_name(instance.instance_id) == normalizedId)
            {
                return index;
            }
        }
        return -1;
    }

    private bool BackpackHasEquipmentInstance(WarehouseState backpackView, StringName instanceId)
    {
        return FindBackpackEquipmentInstanceIndex(backpackView, instanceId) >= 0;
    }

    private static bool StringNameIsEmpty(StringName value)
    {
        return value == null || string.IsNullOrEmpty(value.ToString());
    }

    private BattleState RuntimeState()
    {
        return _runtime?.GetState();
    }

    private static T ResolveWeakRef<T>(WeakReference<T> weakRef)
        where T : class
    {
        if (weakRef == null || !weakRef.TryGetTarget(out T target))
        {
            return null;
        }
        return target;
    }
}
