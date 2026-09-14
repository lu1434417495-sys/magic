using System.Collections.Generic;
using Godot;

public enum WarehouseStateItemIssueKind
{
    None = 0,
    MissingWarehouseState = 1,
    NullStack = 2,
    InvalidStackFields = 3,
    UnknownStackItem = 4,
    EquipmentStoredInStack = 5,
    StackQuantityOverMaxStack = 6,
    NullEquipmentInstance = 7,
    MissingEquipmentInstanceItemId = 8,
    UnknownEquipmentInstanceItem = 9,
    NonEquipmentStoredAsInstance = 10,
}

public readonly struct WarehouseStateItemIssue
{
    public WarehouseStateItemIssue(
        WarehouseStateItemIssueKind kind,
        string message,
        int entryIndex
    )
    {
        Kind = kind;
        Message = message;
        EntryIndex = entryIndex;
    }

    public WarehouseStateItemIssueKind Kind { get; }
    public string Message { get; }

    /// <summary>问题条目在其所属集合中的下标；<c>MissingWarehouseState</c> 为 -1。</summary>
    public int EntryIndex { get; }
}

/// <summary>读档修复的结果计数，用于日志留痕。</summary>
public readonly struct WarehouseStateRepairResult
{
    public WarehouseStateRepairResult(
        int clampedStackCount,
        int droppedStackCount,
        int droppedInstanceCount
    )
    {
        ClampedStackCount = clampedStackCount;
        DroppedStackCount = droppedStackCount;
        DroppedInstanceCount = droppedInstanceCount;
    }

    public int ClampedStackCount { get; }
    public int DroppedStackCount { get; }
    public int DroppedInstanceCount { get; }

    public bool Changed => ClampedStackCount > 0 || DroppedStackCount > 0 || DroppedInstanceCount > 0;
}

public static class WarehouseStateItemValidator
{
    public static List<string> Validate(
        WarehouseState warehouseState,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefs,
        string contextPath = "warehouse_state"
    )
    {
        var errors = new List<string>();
        foreach (WarehouseStateItemIssue issue in Collect(warehouseState, itemDefs, contextPath))
            errors.Add(issue.Message);
        return errors;
    }

    public static List<WarehouseStateItemIssue> Collect(
        WarehouseState warehouseState,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefs,
        string contextPath = "warehouse_state"
    )
    {
        var issues = new List<WarehouseStateItemIssue>();

        if (warehouseState == null)
        {
            _add(
                issues,
                WarehouseStateItemIssueKind.MissingWarehouseState,
                $"{contextPath} is missing."
            );
            return issues;
        }

        _validate_stacks(warehouseState, itemDefs, contextPath, issues);

        _validate_equipment_instances(warehouseState, itemDefs, contextPath, issues);

        return issues;
    }

    /// <summary>
    /// 结构性违规：物品被放进了错误的集合，或条目本身不成形。
    /// 这类状态在 runtime 里走不通 —— 例如装备躺在 <c>stacks</c> 里时，
    /// 丢弃路由按 <c>IsEquipment()</c> 走实例删除却找不到实例，
    /// 反向情况下 <c>RemoveItemTyped</c> 只扫 <c>stacks</c> 也找不到，
    /// 两边都会留下永久占格、删不掉的条目，所以读档时整条丢弃（见 <see cref="RepairForLoad"/>）。
    ///
    /// 缺失物品定义（内容被删/改名）和超量堆叠不算结构性：前者按既定策略宽容保留，
    /// 后者钳制到上限而不是丢整条。
    /// </summary>
    public static bool IsStructuralViolation(WarehouseStateItemIssueKind kind) =>
        kind switch
        {
            WarehouseStateItemIssueKind.None => false,
            WarehouseStateItemIssueKind.UnknownStackItem => false,
            WarehouseStateItemIssueKind.UnknownEquipmentInstanceItem => false,
            WarehouseStateItemIssueKind.StackQuantityOverMaxStack => false,
            _ => true,
        };

    public static List<string> CollectStructuralViolations(
        WarehouseState warehouseState,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefs,
        string contextPath = "warehouse_state"
    )
    {
        var violations = new List<string>();
        foreach (WarehouseStateItemIssue issue in Collect(warehouseState, itemDefs, contextPath))
        {
            if (IsStructuralViolation(issue.Kind))
                violations.Add(issue.Message);
        }
        return violations;
    }

    private static void _add(
        List<WarehouseStateItemIssue> issues,
        WarehouseStateItemIssueKind kind,
        string message,
        int entryIndex = -1
    )
    {
        issues.Add(new WarehouseStateItemIssue(kind, message, entryIndex));
    }

    /// <summary>
    /// 读档修复：超量堆叠钳制回 <c>max_stack</c>（多的丢弃），结构性违规条目整条丢弃。
    ///
    /// 丢弃而不是拒绝加载，是因为这类条目在 runtime 里本来就走不通 —— 放错集合的物品
    /// 既删不掉也用不了（丢弃路由按 <c>IsEquipment()</c> 走，永远找不到对应条目），
    /// 留着只是一个永久占格的幽灵。缺失物品定义按既定策略宽容保留。
    ///
    /// 分类口径与 <see cref="IsStructuralViolation"/> 共用 <see cref="Collect"/> 的结果，
    /// 避免修复和校验各写一套判断。
    /// </summary>
    public static WarehouseStateRepairResult RepairForLoad(
        WarehouseState warehouseState,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefs
    )
    {
        if (warehouseState == null)
            return new WarehouseStateRepairResult(0, 0, 0);

        IReadOnlyList<WarehouseStackState> stacks = warehouseState.GetStacksTyped();
        IReadOnlyList<EquipmentInstanceState> instances =
            warehouseState.GetEquipmentInstancesTyped();

        var droppedStackIndexes = new HashSet<int>();
        var droppedInstanceIndexes = new HashSet<int>();
        int clampedStackCount = 0;

        foreach (WarehouseStateItemIssue issue in Collect(warehouseState, itemDefs))
        {
            switch (issue.Kind)
            {
                case WarehouseStateItemIssueKind.StackQuantityOverMaxStack:
                {
                    WarehouseStackState stack = stacks[issue.EntryIndex];
                    ItemDefinition itemDef = _get_item_def(
                        itemDefs,
                        ProgressionDataUtils.to_string_name(stack.item_id)
                    );
                    stack.quantity = itemDef.GetEffectiveMaxStack();
                    clampedStackCount += 1;
                    break;
                }
                case WarehouseStateItemIssueKind.NullStack:
                case WarehouseStateItemIssueKind.InvalidStackFields:
                case WarehouseStateItemIssueKind.EquipmentStoredInStack:
                    droppedStackIndexes.Add(issue.EntryIndex);
                    break;
                case WarehouseStateItemIssueKind.NullEquipmentInstance:
                case WarehouseStateItemIssueKind.MissingEquipmentInstanceItemId:
                case WarehouseStateItemIssueKind.NonEquipmentStoredAsInstance:
                    droppedInstanceIndexes.Add(issue.EntryIndex);
                    break;
                default:
                    break;
            }
        }

        if (droppedStackIndexes.Count > 0)
        {
            var keptStacks = new List<WarehouseStackState>();
            for (int i = 0; i < stacks.Count; i++)
            {
                if (!droppedStackIndexes.Contains(i))
                    keptStacks.Add(stacks[i]);
            }
            warehouseState.ReplaceStacks(keptStacks);
        }

        if (droppedInstanceIndexes.Count > 0)
        {
            var keptInstances = new List<EquipmentInstanceState>();
            for (int i = 0; i < instances.Count; i++)
            {
                if (!droppedInstanceIndexes.Contains(i))
                    keptInstances.Add(instances[i]);
            }
            warehouseState.ReplaceEquipmentInstances(keptInstances);
        }

        return new WarehouseStateRepairResult(
            clampedStackCount,
            droppedStackIndexes.Count,
            droppedInstanceIndexes.Count
        );
    }

    private static void _validate_stacks(
        WarehouseState warehouseState,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefs,
        string contextPath,
        List<WarehouseStateItemIssue> issues
    )
    {
        IReadOnlyList<WarehouseStackState> stacks = warehouseState.GetStacksTyped();
        for (int i = 0; i < stacks.Count; i++)
        {
            WarehouseStackState stack = stacks[i];

            var stackPath = $"{contextPath}.stacks[{i}]";

            if (stack == null)
            {
                _add(issues, WarehouseStateItemIssueKind.NullStack, $"{stackPath} is null.", i);
                continue;
            }

            var itemId = ProgressionDataUtils.to_string_name(stack.item_id);

            int quantity = stack.quantity;

            if (itemId == "" || quantity <= 0)
            {
                _add(
                    issues,
                    WarehouseStateItemIssueKind.InvalidStackFields,
                    $"{stackPath} must have non-empty item_id and positive quantity.",
                    i
                );
                continue;
            }

            ItemDefinition itemDef = _get_item_def(itemDefs, itemId);

            if (itemDef == null)
            {
                _add(
                    issues,
                    WarehouseStateItemIssueKind.UnknownStackItem,
                    $"{stackPath} has unknown item_id '{itemId}'.",
                    i
                );
                continue;
            }

            if (itemDef.IsEquipment())
            {
                _add(
                    issues,
                    WarehouseStateItemIssueKind.EquipmentStoredInStack,
                    $"{stackPath} stores equipment item '{itemId}' in stacks; equipment must use equipment_instances.",
                    i
                );
                continue;
            }

            int maxStack = itemDef.GetEffectiveMaxStack();

            if (quantity > maxStack)
                _add(
                    issues,
                    WarehouseStateItemIssueKind.StackQuantityOverMaxStack,
                    $"{stackPath} quantity {quantity} exceeds max_stack {maxStack} for item_id '{itemId}'.",
                    i
                );
        }
    }

    private static void _validate_equipment_instances(
        WarehouseState warehouseState,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefs,
        string contextPath,
        List<WarehouseStateItemIssue> issues
    )
    {
        IReadOnlyList<EquipmentInstanceState> equipmentInstances =
            warehouseState.GetEquipmentInstancesTyped();
        for (int i = 0; i < equipmentInstances.Count; i++)
        {
            EquipmentInstanceState instance = equipmentInstances[i];

            var instancePath = $"{contextPath}.equipment_instances[{i}]";

            if (instance == null)
            {
                _add(
                    issues,
                    WarehouseStateItemIssueKind.NullEquipmentInstance,
                    $"{instancePath} is null.",
                    i
                );
                continue;
            }

            var itemId = ProgressionDataUtils.to_string_name(instance.item_id);

            if (itemId == "")
            {
                _add(
                    issues,
                    WarehouseStateItemIssueKind.MissingEquipmentInstanceItemId,
                    $"{instancePath} must have non-empty item_id.",
                    i
                );
                continue;
            }

            ItemDefinition itemDef = _get_item_def(itemDefs, itemId);

            if (itemDef == null)
            {
                _add(
                    issues,
                    WarehouseStateItemIssueKind.UnknownEquipmentInstanceItem,
                    $"{instancePath} has unknown item_id '{itemId}'.",
                    i
                );
                continue;
            }

            if (!itemDef.IsEquipment())
            {
                _add(
                    issues,
                    WarehouseStateItemIssueKind.NonEquipmentStoredAsInstance,
                    $"{instancePath} stores non-equipment item '{itemId}' in equipment_instances.",
                    i
                );
            }
        }
    }

    private static ItemDefinition _get_item_def(
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefs,
        StringName itemId
    )
    {
        return itemDefs != null && itemDefs.TryGetValue(itemId, out ItemDefinition itemDef)
            ? itemDef
            : null;
    }
}
