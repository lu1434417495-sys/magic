using System;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

// 翻译自 battle_change_equipment_resolver.gd（2026-05-25，战斗换装 C# 迁移）。
// runtime 耦合：战斗实体/视图使用 C# runtime state；command 为 C# BattleCommand。
[GlobalClass]
public partial class BattleChangeEquipmentResolver : RefCounted
{
    private const int CHANGE_EQUIPMENT_AP_COST = 2;

    private WeakReference<BattleRuntimeModule> _runtimeRef;

    private BattleRuntimeModule _runtime
    {
        get => ResolveWeakRef(_runtimeRef);
        set => _runtimeRef = value != null ? new WeakReference<BattleRuntimeModule>(value) : null;
    }

    public void setup(GodotObject runtime)
    {
        _runtime = runtime as BattleRuntimeModule;
    }

    public void dispose()
    {
        _runtime = null;
    }

    public void preview_command(GodotObject active_unit, BattleCommand command, GodotObject preview)
    {
        PreviewChangeEquipmentCommand(active_unit as BattleUnitState, command, preview as BattlePreview);
    }

    public void handle_command(GodotObject active_unit, BattleCommand command, GodotObject batch)
    {
        HandleChangeEquipmentCommand(active_unit as BattleUnitState, command, batch as BattleEventBatch);
    }

    public GDictionary build_result(
        bool allowed,
        string error_code,
        string message,
        BattleCommand command
    )
    {
        return BuildChangeEquipmentResult(allowed, error_code, message, command);
    }

    public void append_report(
        GodotObject batch,
        GodotObject active_unit,
        GDictionary result,
        bool success
    )
    {
        AppendChangeEquipmentReport(batch as BattleEventBatch, active_unit as BattleUnitState, result, success);
    }

    public int get_unit_hp_max(GodotObject unit_state)
    {
        return GetUnitHpMax(unit_state);
    }

    public int get_unit_stamina_max(GodotObject unit_state)
    {
        return GetUnitStaminaMax(unit_state);
    }

    private void PreviewChangeEquipmentCommand(
        BattleUnitState activeUnit,
        BattleCommand command,
        BattlePreview preview
    )
    {
        if (preview == null)
        {
            return;
        }
        GDictionary validation = ValidateChangeEquipmentCommand(activeUnit, command);
        if (!GdInterop.GetBool(validation, "allowed", false))
        {
            preview.log_lines.Add(GdInterop.GetString(validation, "message", "换装命令无效。"));
            return;
        }

        EquipmentState equipmentView = activeUnit.get_equipment_view()?.duplicate_state();
        WarehouseState backpackView = RuntimeState()?.get_party_backpack_view()?.duplicate_state();
        GDictionary applyResult = ApplyChangeEquipmentToViews(
            command,
            validation,
            equipmentView,
            backpackView
        );
        if (GdInterop.GetBool(applyResult, "allowed", false))
        {
            preview.allowed = true;
            preview.log_lines.Add(GdInterop.GetString(applyResult, "message", "换装可执行。"));
        }
        else
        {
            preview.log_lines.Add(GdInterop.GetString(applyResult, "message", "换装命令无效。"));
        }
    }

    private void HandleChangeEquipmentCommand(
        BattleUnitState activeUnit,
        BattleCommand command,
        BattleEventBatch batch
    )
    {
        GDictionary validation = ValidateChangeEquipmentCommand(activeUnit, command);
        if (!GdInterop.GetBool(validation, "allowed", false))
        {
            AppendChangeEquipmentReport(batch, activeUnit, validation, false);
            return;
        }

        EquipmentState equipmentView = activeUnit.get_equipment_view()?.duplicate_state();
        WarehouseState backpackView = RuntimeState()?.get_party_backpack_view()?.duplicate_state();
        GDictionary applyResult = ApplyChangeEquipmentToViews(
            command,
            validation,
            equipmentView,
            backpackView
        );
        if (!GdInterop.GetBool(applyResult, "allowed", false))
        {
            AppendChangeEquipmentReport(batch, activeUnit, applyResult, false);
            return;
        }

        int apBefore = activeUnit.current_ap;
        activeUnit.set_equipment_view(equipmentView);
        RuntimeState()?.set_party_backpack_view(backpackView);
        RefreshChangeEquipmentProjection(activeUnit, applyResult);
        activeUnit.current_ap = Math.Max(activeUnit.current_ap - CHANGE_EQUIPMENT_AP_COST, 0);
        applyResult["ap_before"] = apBefore;
        applyResult["ap_after"] = activeUnit.current_ap;
        _runtime?._record_action_issued(
            activeUnit,
            BattleCommand.TYPE_CHANGE_EQUIPMENT(),
            CHANGE_EQUIPMENT_AP_COST
        );
        if (batch != null && !batch.changed_unit_ids.Contains(activeUnit.unit_id))
            batch.changed_unit_ids.Add(activeUnit.unit_id);
        AppendChangeEquipmentReport(batch, activeUnit, applyResult, true);
    }

    private void RefreshChangeEquipmentProjection(BattleUnitState activeUnit, GDictionary result)
    {
        if (activeUnit == null)
        {
            return;
        }
        int hpBefore = GdInterop.GetInt(activeUnit, "current_hp");
        int hpMaxBefore = GetUnitHpMax(activeUnit);
        if (
            !GdInterop.IsEmpty(activeUnit.source_member_id)
            && _runtime?.GetCharacterGatewayTyped() != null
        )
        {
            _runtime._unit_factory?.refresh_equipment_projection(activeUnit);
        }
        int hpMaxAfter = GetUnitHpMax(activeUnit);
        bool hpClamped = false;
        if (hpMaxAfter > 0 && hpMaxAfter < hpMaxBefore && hpBefore > hpMaxAfter)
        {
            activeUnit.Set("current_hp", hpMaxAfter);
            hpClamped = true;
        }
        if (GdInterop.GetInt(activeUnit, "current_hp") < 0)
        {
            activeUnit.Set("current_hp", 0);
            hpClamped = true;
        }
        activeUnit.Set("is_alive", GdInterop.GetInt(activeUnit, "current_hp") > 0);
        result["hp_before"] = hpBefore;
        result["hp_after"] = GdInterop.GetInt(activeUnit, "current_hp");
        result["hp_max_before"] = hpMaxBefore;
        result["hp_max_after"] = hpMaxAfter;
        result["hp_clamped"] = hpClamped;
        result["weapon_profile_kind"] = GdInterop.GetString(activeUnit, "weapon_profile_kind");
        result["weapon_item_id"] = GdInterop.GetString(activeUnit, "weapon_item_id");
        result["weapon_profile_type_id"] = GdInterop.GetString(
            activeUnit,
            "weapon_profile_type_id"
        );
        result["weapon_current_grip"] = GdInterop.GetString(activeUnit, "weapon_current_grip");
        result["weapon_attack_range"] = GdInterop.GetInt(activeUnit, "weapon_attack_range");
        result["weapon_uses_two_hands"] = GdInterop.GetBool(activeUnit, "weapon_uses_two_hands");
        result["weapon_physical_damage_tag"] = GdInterop.GetString(
            activeUnit,
            "weapon_physical_damage_tag"
        );
    }

    private int GetUnitHpMax(GodotObject unitState)
    {
        AttributeSnapshot snapshot = (unitState as BattleUnitState)?.attribute_snapshot;
        if (unitState == null || snapshot == null)
        {
            return 0;
        }
        return Math.Max(snapshot.get_value(AttributeService.HP_MAX), 1);
    }

    private int GetUnitStaminaMax(GodotObject unitState)
    {
        AttributeSnapshot snapshot = (unitState as BattleUnitState)?.attribute_snapshot;
        if (unitState == null || snapshot == null)
        {
            return 0;
        }
        return Math.Max(snapshot.get_value(AttributeService.STAMINA_MAX), 0);
    }

    private GDictionary ValidateChangeEquipmentCommand(
        BattleUnitState activeUnit,
        BattleCommand command
    )
    {
        GDictionary result = BuildChangeEquipmentResult(
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
        result["operation"] = operation.ToString();
        result["slot_id"] = slotId.ToString();
        result["target_unit_id"] = ResolveChangeEquipmentTargetUnitId(activeUnit, command)
            .ToString();
        result["item_id"] = ResolveChangeEquipmentItemId(command).ToString();
        result["instance_id"] = ResolveChangeEquipmentInstanceId(command).ToString();
        result["occupied_slot_ids"] = StringifyStringNameArray(
            ResolveChangeEquipmentOccupiedSlots(command, slotId)
        );

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
        if (
            operation != BattleCommand.EQUIPMENT_OPERATION_EQUIP()
            && operation != BattleCommand.EQUIPMENT_OPERATION_UNEQUIP()
        )
        {
            return WithChangeEquipmentError(result, "operation_invalid", "换装操作无效。");
        }
        if (!EquipmentRules.is_valid_slot(slotId))
        {
            return WithChangeEquipmentError(result, "slot_invalid", $"装备槽无效：{slotId}。");
        }

        EquipmentState equipmentView = activeUnit.get_equipment_view();
        if (equipmentView == null)
        {
            return WithChangeEquipmentError(
                result,
                "equipment_view_unavailable",
                "战斗内装备状态不可用。"
            );
        }
        WarehouseState backpackView = state.get_party_backpack_view();
        if (backpackView == null)
        {
            return WithChangeEquipmentError(
                result,
                "backpack_view_unavailable",
                "战斗内背包状态不可用。"
            );
        }

        if (operation == BattleCommand.EQUIPMENT_OPERATION_EQUIP())
        {
            StringName instanceId = ResolveChangeEquipmentInstanceId(command);
            if (GdInterop.IsEmpty(instanceId))
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
            EquipmentInstanceState backpackInstance =
                backpackView.equipment_instances[backpackIndex];
            StringName resolvedItemId = ProgressionDataUtils.to_string_name(
                backpackInstance.item_id
            );
            StringName commandItemId = ResolveChangeEquipmentItemId(command);
            if (!GdInterop.IsEmpty(commandItemId) && commandItemId != resolvedItemId)
            {
                return WithChangeEquipmentError(
                    result,
                    "equipment_instance_item_mismatch",
                    "装备实例与命令物品不一致。"
                );
            }
            result["item_id"] = resolvedItemId.ToString();
            GDictionary itemRule = ResolveChangeEquipmentItemRule(
                resolvedItemId,
                slotId,
                command,
                activeUnit
            );
            if (!GdInterop.GetBool(itemRule, "allowed", false))
            {
                return WithChangeEquipmentError(
                    result,
                    GdInterop.GetString(itemRule, "error_code", "item_not_equipment"),
                    GdInterop.GetString(itemRule, "message", "装备实例不能放入该槽位。"),
                    new GDictionary
                    {
                        ["occupied_slot_ids"] = GdInterop.GetArray(itemRule, "occupied_slot_ids"),
                    }
                );
            }
            result["occupied_slot_ids"] = GdInterop.GetArray(itemRule, "occupied_slot_ids");
        }
        else
        {
            StringName entrySlot = ProgressionDataUtils.to_string_name(
                equipmentView.get_entry_slot_for_slot(slotId)
            );
            if (GdInterop.IsEmpty(entrySlot))
            {
                return WithChangeEquipmentError(
                    result,
                    "slot_empty",
                    $"{EquipmentRules.get_slot_label(slotId)} 当前没有已装备物品。"
                );
            }
            StringName equippedInstanceId = ProgressionDataUtils.to_string_name(
                equipmentView.get_equipped_instance_id(slotId)
            );
            StringName commandInstanceId = ResolveChangeEquipmentInstanceId(command);
            if (
                !GdInterop.IsEmpty(commandInstanceId)
                && !GdInterop.IsEmpty(equippedInstanceId)
                && commandInstanceId != equippedInstanceId
            )
            {
                return WithChangeEquipmentError(
                    result,
                    "equipment_instance_item_mismatch",
                    "装备实例与当前槽位不一致。"
                );
            }
            result["item_id"] = ProgressionDataUtils
                .to_string_name(equipmentView.get_equipped_item_id(slotId))
                .ToString();
            result["instance_id"] = equippedInstanceId.ToString();
            result["occupied_slot_ids"] = StringifyStringNameArray(
                ProgressionDataUtils.to_string_name_array(
                    equipmentView.get_occupied_slot_ids_for_entry(entrySlot)
                )
            );
        }

        result["allowed"] = true;
        result["error_code"] = "";
        result["message"] = BuildChangeEquipmentSuccessMessage(activeUnit, result);
        return result;
    }

    private GDictionary ApplyChangeEquipmentToViews(
        BattleCommand command,
        GDictionary validation,
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

        StringName operation = ProgressionDataUtils.to_string_name(
            validation.GetValueOrDefault("operation", "")
        );
        StringName slotId = ProgressionDataUtils.to_string_name(
            validation.GetValueOrDefault("slot_id", "")
        );
        StringName itemId = ProgressionDataUtils.to_string_name(
            validation.GetValueOrDefault("item_id", "")
        );
        StringName instanceId = ProgressionDataUtils.to_string_name(
            validation.GetValueOrDefault("instance_id", "")
        );
        GDictionary result = (GDictionary)validation.Duplicate(true);

        if (operation == BattleCommand.EQUIPMENT_OPERATION_EQUIP())
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
            var backpackInstances = backpackView.equipment_instances;
            EquipmentInstanceState newInstance = backpackInstances[backpackIndex];
            itemId = ProgressionDataUtils.to_string_name(newInstance.Get("item_id"));
            backpackInstances.RemoveAt(backpackIndex);

            GStringNameArray occupiedSlots = ProgressionDataUtils.to_string_name_array(
                validation.GetValueOrDefault("occupied_slot_ids", new GArray())
            );
            if (occupiedSlots.Count == 0)
            {
                occupiedSlots = new GStringNameArray { slotId };
            }
            var displacedEntrySlots = new GDictionary();
            foreach (StringName occupiedSlotId in occupiedSlots)
            {
                StringName existingEntrySlot = ProgressionDataUtils.to_string_name(
                    equipmentView.get_entry_slot_for_slot(occupiedSlotId)
                );
                if (
                    GdInterop.IsEmpty(existingEntrySlot)
                    || displacedEntrySlots.ContainsKey(existingEntrySlot)
                )
                {
                    continue;
                }
                displacedEntrySlots[existingEntrySlot] = true;
                EquipmentInstanceState displacedInstance = equipmentView.pop_equipped_instance(
                    existingEntrySlot
                );
                if (displacedInstance != null)
                {
                    if (
                        BackpackHasEquipmentInstance(
                            backpackView,
                            ProgressionDataUtils.to_string_name(
                                displacedInstance.Get("instance_id")
                            )
                        )
                    )
                    {
                        return WithChangeEquipmentError(
                            result,
                            "equipment_instance_already_in_backpack",
                            $"战斗背包中已存在装备实例 {displacedInstance.Get("instance_id")}。"
                        );
                    }
                    backpackInstances.Add(displacedInstance);
                }
            }
            equipmentView.set_equipped_entry(slotId, itemId, occupiedSlots, newInstance);
            result["item_id"] = itemId.ToString();
            result["instance_id"] = instanceId.ToString();
        }
        else
        {
            StringName entrySlot = ProgressionDataUtils.to_string_name(
                equipmentView.get_entry_slot_for_slot(slotId)
            );
            if (GdInterop.IsEmpty(entrySlot))
            {
                return WithChangeEquipmentError(
                    result,
                    "slot_empty",
                    $"{EquipmentRules.get_slot_label(slotId)} 当前没有已装备物品。"
                );
            }
            EquipmentInstanceState removedInstance = equipmentView.pop_equipped_instance(entrySlot);
            if (removedInstance == null)
            {
                return WithChangeEquipmentError(
                    result,
                    "slot_empty",
                    $"{EquipmentRules.get_slot_label(slotId)} 当前没有已装备物品。"
                );
            }
            StringName removedInstanceId = ProgressionDataUtils.to_string_name(
                removedInstance.Get("instance_id")
            );
            if (
                !GdInterop.IsEmpty(removedInstanceId)
                && BackpackHasEquipmentInstance(backpackView, removedInstanceId)
            )
            {
                return WithChangeEquipmentError(
                    result,
                    "equipment_instance_already_in_backpack",
                    $"战斗背包中已存在装备实例 {removedInstanceId}。"
                );
            }
            backpackView.equipment_instances.Add(removedInstance);
            result["item_id"] = ProgressionDataUtils
                .to_string_name(removedInstance.Get("item_id"))
                .ToString();
            result["instance_id"] = removedInstanceId.ToString();
        }

        GDictionary ownershipResult = ValidateChangeEquipmentInstanceOwnership(
            equipmentView,
            backpackView
        );
        if (!GdInterop.GetBool(ownershipResult, "allowed", false))
        {
            return WithChangeEquipmentError(
                result,
                GdInterop.GetString(
                    ownershipResult,
                    "error_code",
                    "equipment_instance_write_failed"
                ),
                GdInterop.GetString(ownershipResult, "message", "装备实例写入失败。")
            );
        }
        GDictionary capacityResult = ValidateChangeEquipmentBackpackCapacity(backpackView);
        if (!GdInterop.GetBool(capacityResult, "allowed", false))
        {
            return WithChangeEquipmentError(
                result,
                GdInterop.GetString(capacityResult, "error_code", "backpack_capacity_exceeded"),
                GdInterop.GetString(capacityResult, "message", "战斗背包容量不足。")
            );
        }

        result["allowed"] = true;
        result["error_code"] = "";
        result["message"] = BuildChangeEquipmentSuccessMessage(null, result);
        return result;
    }

    private GDictionary BuildChangeEquipmentResult(
        bool allowed,
        string errorCode,
        string message,
        BattleCommand command
    )
    {
        StringName slotId = GetChangeEquipmentSlotId(command);
        return new GDictionary
        {
            ["allowed"] = allowed,
            ["error_code"] = errorCode,
            ["message"] = message,
            ["operation"] = GetChangeEquipmentOperation(command).ToString(),
            ["slot_id"] = slotId.ToString(),
            ["slot_label"] = EquipmentRules.get_slot_label(slotId),
            ["target_unit_id"] = "",
            ["item_id"] = ResolveChangeEquipmentItemId(command).ToString(),
            ["instance_id"] = ResolveChangeEquipmentInstanceId(command).ToString(),
            ["occupied_slot_ids"] = StringifyStringNameArray(
                ResolveChangeEquipmentOccupiedSlots(command, slotId)
            ),
        };
    }

    private GDictionary WithChangeEquipmentError(
        GDictionary result,
        string errorCode,
        string message
    )
    {
        return WithChangeEquipmentError(result, errorCode, message, new GDictionary());
    }

    private GDictionary WithChangeEquipmentError(
        GDictionary result,
        string errorCode,
        string message,
        GDictionary extraFields
    )
    {
        GDictionary output = (GDictionary)result.Duplicate(true);
        foreach (var key in extraFields.Keys)
        {
            output[key] =
                key.ToString() == "occupied_slot_ids"
                    ? StringifyVariantArray(extraFields[key])
                    : extraFields[key];
        }
        output["allowed"] = false;
        output["error_code"] = errorCode;
        output["message"] = message;
        return output;
    }

    private void AppendChangeEquipmentReport(
        BattleEventBatch batch,
        BattleUnitState activeUnit,
        GDictionary result,
        bool success
    )
    {
        bool hasUnit = activeUnit != null;
        var reportEntry = new GDictionary
        {
            ["entry_type"] = "change_equipment",
            ["type"] = "change_equipment",
            ["ok"] = success,
            ["error_code"] = success
                ? ""
                : GdInterop.GetString(result, "error_code", "change_equipment_failed"),
            ["reason_id"] = GdInterop.GetString(result, "operation", ""),
            ["event_tags"] = new GArray { "equipment", "change_equipment" },
            ["unit_id"] = hasUnit ? GdInterop.GetStringName(activeUnit, "unit_id").ToString() : "",
            ["target_unit_id"] = GdInterop.GetString(result, "target_unit_id", ""),
            ["operation"] = GdInterop.GetString(result, "operation", ""),
            ["slot_id"] = GdInterop.GetString(result, "slot_id", ""),
            ["slot_label"] = GdInterop.GetString(result, "slot_label", ""),
            ["item_id"] = GdInterop.GetString(result, "item_id", ""),
            ["instance_id"] = GdInterop.GetString(result, "instance_id", ""),
            ["ap_cost"] = success ? CHANGE_EQUIPMENT_AP_COST : 0,
            ["ap_before"] = GdInterop.GetInt(result, "ap_before", 0),
            ["ap_after"] = GdInterop.GetInt(
                result,
                "ap_after",
                hasUnit ? GdInterop.GetInt(activeUnit, "current_ap") : 0
            ),
            ["current_ap"] = hasUnit ? GdInterop.GetInt(activeUnit, "current_ap") : 0,
            ["hp_before"] = GdInterop.GetInt(
                result,
                "hp_before",
                hasUnit ? GdInterop.GetInt(activeUnit, "current_hp") : 0
            ),
            ["hp_after"] = GdInterop.GetInt(
                result,
                "hp_after",
                hasUnit ? GdInterop.GetInt(activeUnit, "current_hp") : 0
            ),
            ["hp_max_before"] = GdInterop.GetInt(result, "hp_max_before", GetUnitHpMax(activeUnit)),
            ["hp_max_after"] = GdInterop.GetInt(result, "hp_max_after", GetUnitHpMax(activeUnit)),
            ["hp_clamped"] = GdInterop.GetBool(result, "hp_clamped", false),
            ["weapon_profile_kind"] = GdInterop.GetString(
                result,
                "weapon_profile_kind",
                hasUnit ? GdInterop.GetString(activeUnit, "weapon_profile_kind") : ""
            ),
            ["weapon_item_id"] = GdInterop.GetString(
                result,
                "weapon_item_id",
                hasUnit ? GdInterop.GetString(activeUnit, "weapon_item_id") : ""
            ),
            ["weapon_profile_type_id"] = GdInterop.GetString(
                result,
                "weapon_profile_type_id",
                hasUnit ? GdInterop.GetString(activeUnit, "weapon_profile_type_id") : ""
            ),
            ["weapon_current_grip"] = GdInterop.GetString(
                result,
                "weapon_current_grip",
                hasUnit ? GdInterop.GetString(activeUnit, "weapon_current_grip") : ""
            ),
            ["weapon_attack_range"] = GdInterop.GetInt(
                result,
                "weapon_attack_range",
                hasUnit ? GdInterop.GetInt(activeUnit, "weapon_attack_range") : 0
            ),
            ["weapon_uses_two_hands"] = GdInterop.GetBool(
                result,
                "weapon_uses_two_hands",
                hasUnit && GdInterop.GetBool(activeUnit, "weapon_uses_two_hands")
            ),
            ["weapon_physical_damage_tag"] = GdInterop.GetString(
                result,
                "weapon_physical_damage_tag",
                hasUnit ? GdInterop.GetString(activeUnit, "weapon_physical_damage_tag") : ""
            ),
            ["text"] = GdInterop.GetString(result, "message", "换装命令无效。"),
        };
        _runtime?._append_report_entry_to_batch(batch, reportEntry);
    }

    private string BuildChangeEquipmentSuccessMessage(GodotObject activeUnit, GDictionary result)
    {
        string unitName =
            activeUnit != null
            && !string.IsNullOrEmpty(GdInterop.GetString(activeUnit, "display_name"))
                ? GdInterop.GetString(activeUnit, "display_name")
                : GdInterop.GetString(result, "target_unit_id", "");
        if (string.IsNullOrEmpty(unitName))
        {
            unitName = "当前单位";
        }
        StringName operation = ProgressionDataUtils.to_string_name(
            result.GetValueOrDefault("operation", "")
        );
        string slotLabel = GdInterop.GetString(
            result,
            "slot_label",
            EquipmentRules.get_slot_label(
                ProgressionDataUtils.to_string_name(result.GetValueOrDefault("slot_id", ""))
            )
        );
        string itemId = GdInterop.GetString(result, "item_id", "");
        string instanceId = GdInterop.GetString(result, "instance_id", "");
        if (operation == BattleCommand.EQUIPMENT_OPERATION_EQUIP())
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
        return ProgressionDataUtils.to_string_name(command.equipment_operation);
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
        if (!GdInterop.IsEmpty(explicitTarget))
        {
            return explicitTarget;
        }
        return activeUnit != null
            ? activeUnit.unit_id
            : new StringName("");
    }

    private StringName ResolveChangeEquipmentItemId(BattleCommand command)
    {
        if (command == null)
        {
            return "";
        }
        StringName itemId = ProgressionDataUtils.to_string_name(command.equipment_item_id);
        if (!GdInterop.IsEmpty(itemId))
        {
            return itemId;
        }
        GDictionary instancePayload = command.equipment_instance ?? new GDictionary();
        return ProgressionDataUtils.to_string_name(
            instancePayload.GetValueOrDefault("item_id", "")
        );
    }

    private StringName ResolveChangeEquipmentInstanceId(BattleCommand command)
    {
        if (command == null)
        {
            return "";
        }
        StringName instanceId = ProgressionDataUtils.to_string_name(command.equipment_instance_id);
        if (!GdInterop.IsEmpty(instanceId))
        {
            return instanceId;
        }
        GDictionary instancePayload = command.equipment_instance ?? new GDictionary();
        return ProgressionDataUtils.to_string_name(
            instancePayload.GetValueOrDefault("instance_id", "")
        );
    }

    private GStringNameArray ResolveChangeEquipmentOccupiedSlots(
        BattleCommand command,
        StringName slotId
    )
    {
        var occupiedSlots = new GStringNameArray();
        if (command != null)
        {
            occupiedSlots = EquipmentRules.normalize_slot_ids(command.equipment_occupied_slot_ids);
        }
        StringName normSlot = ProgressionDataUtils.to_string_name(slotId);
        if (occupiedSlots.Count == 0 && EquipmentRules.is_valid_slot(normSlot))
        {
            occupiedSlots.Add(normSlot);
        }
        else if (EquipmentRules.is_valid_slot(normSlot) && !occupiedSlots.Contains(normSlot))
        {
            occupiedSlots.Insert(0, normSlot);
        }
        return occupiedSlots;
    }

    private GDictionary ResolveChangeEquipmentItemRule(
        StringName itemId,
        StringName slotId,
        BattleCommand command,
        BattleUnitState activeUnit
    )
    {
        StringName normItem = ProgressionDataUtils.to_string_name(itemId);
        StringName normSlot = ProgressionDataUtils.to_string_name(slotId);
        GStringNameArray fallbackOccupied = ResolveChangeEquipmentOccupiedSlots(command, normSlot);
        var result = new GDictionary
        {
            ["allowed"] = true,
            ["error_code"] = "",
            ["message"] = "",
            ["occupied_slot_ids"] = StringifyStringNameArray(fallbackOccupied),
        };
        ItemDef itemDef = GetChangeEquipmentItemDef(normItem);
        if (itemDef == null)
        {
            if (HasChangeEquipmentItemCatalog())
            {
                result["allowed"] = false;
                result["error_code"] = "item_not_found";
                result["message"] = $"找不到装备定义：{normItem}。";
            }
            return result;
        }
        if (!itemDef.is_equipment())
        {
            result["allowed"] = false;
            result["error_code"] = "item_not_equipment";
            result["message"] = $"{normItem} 不是可装备物品。";
            return result;
        }
        GStringNameArray allowedSlots = itemDef.get_equipment_slot_ids();
        if (!allowedSlots.Contains(normSlot))
        {
            result["allowed"] = false;
            result["error_code"] = "slot_not_allowed";
            result["message"] =
                $"{normItem} 不能装备到 {EquipmentRules.get_slot_label(normSlot)}。";
            return result;
        }
        GDictionary requirementRule = ResolveChangeEquipmentRequirementRule(
            activeUnit,
            itemDef,
            normItem
        );
        if (!GdInterop.GetBool(requirementRule, "allowed", true))
        {
            return requirementRule;
        }
        GStringNameArray occupiedSlots = ProgressionDataUtils.to_string_name_array(
            itemDef.get_final_occupied_slot_ids(normSlot)
        );
        if (occupiedSlots.Count == 0)
        {
            occupiedSlots = new GStringNameArray { normSlot };
        }
        else if (!occupiedSlots.Contains(normSlot))
        {
            occupiedSlots.Insert(0, normSlot);
        }
        result["occupied_slot_ids"] = StringifyStringNameArray(occupiedSlots);
        return result;
    }

    private GDictionary ResolveChangeEquipmentRequirementRule(
        BattleUnitState activeUnit,
        ItemDef itemDef,
        StringName itemId
    )
    {
        var result = new GDictionary
        {
            ["allowed"] = true,
            ["error_code"] = "",
            ["message"] = "",
            ["occupied_slot_ids"] = new GArray(),
        };
        if (itemDef == null)
        {
            return result;
        }
        EquipmentRequirement equipReq = itemDef.equip_requirement as EquipmentRequirement;
        if (equipReq == null)
        {
            return result;
        }
        string itemLabel = GetChangeEquipmentItemDisplayName(itemDef, itemId);
        if (
            activeUnit == null
            || GdInterop.IsEmpty(activeUnit.source_member_id)
        )
        {
            result["allowed"] = false;
            result["error_code"] = "item_not_equippable";
            result["message"] = BuildChangeEquipmentRequirementFailureMessage(itemLabel);
            return result;
        }
        IBattleRuntimeCharacterGateway characterGateway = _runtime?.GetCharacterGatewayTyped();
        if (characterGateway == null)
        {
            result["allowed"] = false;
            result["error_code"] = "item_not_equippable";
            result["message"] = BuildChangeEquipmentRequirementFailureMessage(itemLabel);
            return result;
        }
        PartyMemberState memberState = characterGateway.get_member_state(activeUnit.source_member_id);
        if (memberState == null)
        {
            result["allowed"] = false;
            result["error_code"] = "item_not_equippable";
            result["message"] = BuildChangeEquipmentRequirementFailureMessage(itemLabel);
            return result;
        }
        GDictionary reqResult = equipReq.Check(memberState) ?? new GDictionary();
        if (GdInterop.GetBool(reqResult, "allowed", true))
        {
            return result;
        }
        result["allowed"] = false;
        result["error_code"] = "item_not_equippable";
        result["message"] = BuildChangeEquipmentRequirementFailureMessage(itemLabel);
        return result;
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
        GDictionary defs = _runtime?.get_item_defs();
        if (GdInterop.IsEmpty(normalized) || defs == null || defs.Count == 0)
        {
            return null;
        }
        foreach (var key in defs.Keys)
        {
            if (key.VariantType != Variant.Type.StringName || key.AsStringName() != normalized)
            {
                continue;
            }
            return defs[key].AsGodotObject() as ItemDef;
        }
        return null;
    }

    private bool HasChangeEquipmentItemCatalog()
    {
        GDictionary itemDefs = _runtime?.get_item_defs();
        return itemDefs != null && itemDefs.Count > 0;
    }

    private GDictionary ValidateChangeEquipmentInstanceOwnership(
        EquipmentState equipmentView,
        WarehouseState backpackView
    )
    {
        var owners = new GDictionary();
        if (backpackView == null)
        {
            return new GDictionary
            {
                ["allowed"] = false,
                ["error_code"] = "backpack_view_unavailable",
                ["message"] = "战斗内背包状态不可用。",
            };
        }
        foreach (EquipmentInstanceState instance in backpackView.get_non_empty_instances())
        {
            StringName instanceId = ProgressionDataUtils.to_string_name(
                instance != null ? instance.instance_id : new StringName("")
            );
            StringName itemId = ProgressionDataUtils.to_string_name(
                instance != null ? instance.item_id : new StringName("")
            );
            GDictionary ownerResult = ClaimChangeEquipmentInstanceOwner(
                owners,
                instanceId,
                itemId,
                "backpack"
            );
            if (!GdInterop.GetBool(ownerResult, "allowed", false))
            {
                return ownerResult;
            }
        }
        if (equipmentView == null)
        {
            return new GDictionary
            {
                ["allowed"] = false,
                ["error_code"] = "equipment_view_unavailable",
                ["message"] = "战斗内装备状态不可用。",
            };
        }
        foreach (StringName entrySlotId in equipmentView.get_entry_slot_ids())
        {
            StringName itemId = ProgressionDataUtils.to_string_name(
                equipmentView.get_equipped_item_id(entrySlotId)
            );
            StringName instanceId = ProgressionDataUtils.to_string_name(
                equipmentView.get_equipped_instance_id(entrySlotId)
            );
            string ownerName = $"equipment:{entrySlotId}";
            GDictionary ownerResult = ClaimChangeEquipmentInstanceOwner(
                owners,
                instanceId,
                itemId,
                ownerName
            );
            if (!GdInterop.GetBool(ownerResult, "allowed", false))
            {
                return ownerResult;
            }
        }
        return new GDictionary
        {
            ["allowed"] = true,
            ["error_code"] = "",
            ["message"] = "",
        };
    }

    private GDictionary ClaimChangeEquipmentInstanceOwner(
        GDictionary owners,
        StringName instanceId,
        StringName itemId,
        string ownerName
    )
    {
        if (GdInterop.IsEmpty(instanceId) || GdInterop.IsEmpty(itemId))
        {
            return new GDictionary
            {
                ["allowed"] = false,
                ["error_code"] = "equipment_instance_write_failed",
                ["message"] = $"装备实例写入失败：{ownerName} 存在空实例或空物品。",
            };
        }
        if (owners.ContainsKey(instanceId))
        {
            return new GDictionary
            {
                ["allowed"] = false,
                ["error_code"] = "equipment_instance_duplicate_owner",
                ["message"] = $"装备实例 {instanceId} 同时存在于多个位置。",
            };
        }
        owners[instanceId] = new GDictionary { ["item_id"] = itemId, ["owner"] = ownerName };
        return new GDictionary
        {
            ["allowed"] = true,
            ["error_code"] = "",
            ["message"] = "",
        };
    }

    private GDictionary ValidateChangeEquipmentBackpackCapacity(WarehouseState backpackView)
    {
        int capacity = GetChangeEquipmentBackpackCapacity();
        if (capacity < 0)
        {
            return new GDictionary
            {
                ["allowed"] = true,
                ["error_code"] = "",
                ["message"] = "",
            };
        }
        int usedSlots = GetChangeEquipmentBackpackUsedSlots(backpackView);
        if (usedSlots <= capacity)
        {
            return new GDictionary
            {
                ["allowed"] = true,
                ["error_code"] = "",
                ["message"] = "",
            };
        }
        return new GDictionary
        {
            ["allowed"] = false,
            ["error_code"] = "backpack_capacity_exceeded",
            ["message"] = $"战斗背包容量不足：需要 {usedSlots} 格，当前容量 {capacity} 格。",
        };
    }

    private int GetChangeEquipmentBackpackCapacity()
    {
        PartyState partyState = _runtime?.GetCharacterGatewayTyped()?.get_party_state();
        if (partyState == null)
        {
            return -1;
        }
        int totalCapacity = 0;
        foreach (Variant memberStateValue in partyState.member_states.Values)
        {
            PartyMemberState memberState = memberStateValue.AsGodotObject() as PartyMemberState;
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
                unitBaseAttributes.get_attribute_value(new StringName("storage_space")),
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
        return backpackView.get_non_empty_stacks().Count
            + backpackView.get_non_empty_instances().Count;
    }

    private int FindBackpackEquipmentInstanceIndex(WarehouseState backpackView, StringName instanceId)
    {
        StringName normalizedId = ProgressionDataUtils.to_string_name(instanceId);
        if (backpackView == null || GdInterop.IsEmpty(normalizedId))
        {
            return -1;
        }
        var instances = backpackView.equipment_instances;
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

    private static GStringArray StringifyStringNameArray(GStringNameArray values)
    {
        var result = new GStringArray();
        foreach (StringName value in values)
        {
            result.Add(value.ToString());
        }
        return result;
    }

    private static GStringArray StringifyVariantArray(object rawValues)
    {
        var result = new GStringArray();
        GArray values = rawValues switch
        {
            Variant value when value.VariantType == Variant.Type.Array => value.AsGodotArray(),
            GArray array => array,
            _ => null,
        };
        if (values == null)
        {
            return result;
        }
        foreach (var value in values)
        {
            result.Add(value.ToString());
        }
        return result;
    }

    private BattleState RuntimeState()
    {
        return _runtime?.get_state();
    }

    private static T ResolveWeakRef<T>(WeakReference<T> weakRef)
        where T : GodotObject
    {
        if (
            weakRef == null
            || !weakRef.TryGetTarget(out T target)
            || !GodotObject.IsInstanceValid(target)
        )
        {
            return null;
        }
        return target;
    }
}
