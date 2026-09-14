using System;
using System.Collections.Generic;
using PlainDictionary = System.Collections.Generic.Dictionary<string, object>;
using PlainList = System.Collections.Generic.List<object>;

// Detached window input for PartyWarehouseWindow. Presentation text lives here instead of
// inside WarehouseWindowSnapshot so the warehouse domain snapshot stays free of UI copy.
internal sealed record WarehouseWindowData(
    string Title,
    string Meta,
    string SummaryText,
    string StatusText,
    WarehouseWindowSnapshot Snapshot
)
{
    internal static WarehouseWindowData Empty { get; } =
        new("共享仓库", "", "", "", WarehouseWindowSnapshot.Empty);

    // The plain snapshot is the stable headless surface; it is projected one way from this
    // typed owner and must never be parsed back into window state.
    internal IReadOnlyDictionary<string, object> BuildSnapshotPlain()
    {
        if (Snapshot == null || !Snapshot.Available)
            return new PlainDictionary(StringComparer.Ordinal);

        var entries = new PlainList();
        foreach (WarehouseInventoryEntrySnapshot entry in Snapshot.Entries)
            entries.Add(BuildEntrySnapshotPlain(entry));

        var targetMembers = new PlainList();
        foreach (WarehouseTargetMemberSnapshot member in Snapshot.TargetMembers)
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

        return new PlainDictionary(StringComparer.Ordinal)
        {
            ["title"] = Title,
            ["meta"] = Meta,
            ["summary_text"] = SummaryText,
            ["status_text"] = StatusText,
            ["target_members"] = targetMembers,
            ["default_target_member_id"] = Snapshot.DefaultTargetMemberId.ToString(),
            ["entries"] = entries,
        };
    }

    private static IReadOnlyDictionary<string, object> BuildEntrySnapshotPlain(
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
}
