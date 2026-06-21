using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;

internal static class GodotRefCountedDisposer
{
    private static readonly List<GodotObject> BorrowedResourceKeepAlive = new();
    private static readonly HashSet<ulong> BorrowedResourceKeepAliveIds = new();

    internal static void DisposeIfValid<T>(T value)
        where T : RefCounted
    {
        if (value == null || !GodotObject.IsInstanceValid(value))
            return;
        GC.SuppressFinalize(value);
        DisposeTyped(value);
    }

    internal static void DisposeAll<T>(IEnumerable<T> values)
        where T : RefCounted
    {
        if (values == null)
            return;
        foreach (T value in values)
            DisposeIfValid(value);
    }

    internal static void DisposeAll<[MustBeVariant] T>(Godot.Collections.Array<T> values)
        where T : RefCounted
    {
        if (values == null)
            return;
        Godot.Collections.Array rawValues = (Godot.Collections.Array)values;
        foreach (Variant rawValue in rawValues)
        {
            if (rawValue.VariantType != Variant.Type.Object)
                continue;
            if (rawValue.AsGodotObject() is T value)
                DisposeIfValid(value);
        }
    }

    internal static void SuppressBorrowedResourceGraph(GodotObject value)
    {
        SuppressBorrowedResourceGraph(value, new HashSet<ulong>());
    }

    internal static void KeepBorrowedResourceGraphAlive(GodotObject value)
    {
        KeepBorrowedResourceGraphAlive(value, new HashSet<ulong>());
    }

    internal static void KeepBorrowedResourceGraphsAlive<T>(IEnumerable<T> values)
        where T : GodotObject
    {
        if (values == null)
            return;
        var seen = new HashSet<ulong>();
        foreach (T value in values)
            KeepBorrowedResourceGraphAlive(value, seen);
    }

    internal static void KeepBorrowedResourceGraphsAlive(Godot.Collections.Dictionary values)
    {
        if (values == null)
            return;
        var seen = new HashSet<ulong>();
        foreach (Variant value in values.Values)
            KeepBorrowedVariantAlive(value, seen);
    }

    internal static void SuppressKeptBorrowedResourceFinalizers()
    {
        var seen = new HashSet<ulong>();
        foreach (GodotObject value in BorrowedResourceKeepAlive)
            SuppressBorrowedResourceGraph(value, seen);
    }

    internal static void SuppressBorrowedResourceGraphs<T>(IEnumerable<T> values)
        where T : GodotObject
    {
        if (values == null)
            return;
        var seen = new HashSet<ulong>();
        foreach (T value in values)
            SuppressBorrowedResourceGraph(value, seen);
    }

    internal static void SuppressBorrowedResourceGraphs(Godot.Collections.Dictionary values)
    {
        if (values == null)
            return;
        var seen = new HashSet<ulong>();
        foreach (Variant value in values.Values)
            SuppressBorrowedVariant(value, seen);
    }

    private static void SuppressBorrowedResourceGraph(GodotObject value, HashSet<ulong> seen)
    {
        if (value == null)
            return;
        GC.SuppressFinalize(value);
        if (!GodotObject.IsInstanceValid(value))
            return;
        ulong instanceId = value.GetInstanceId();
        if (instanceId != 0 && !seen.Add(instanceId))
            return;
        if (value is not Resource)
            return;

        Type currentType = value.GetType();
        while (
            currentType != null
            && currentType != typeof(Resource)
            && currentType != typeof(RefCounted)
            && currentType != typeof(GodotObject)
        )
        {
            foreach (
                FieldInfo field in currentType.GetFields(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly
                )
            )
            {
                SuppressBorrowedObject(ReadFieldValue(field, value), seen);
            }
            foreach (
                PropertyInfo property in currentType.GetProperties(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly
                )
            )
            {
                if (!property.CanRead || property.GetIndexParameters().Length > 0)
                    continue;
                SuppressBorrowedObject(ReadPropertyValue(property, value), seen);
            }
            currentType = currentType.BaseType;
        }
    }

    private static void KeepBorrowedResourceGraphAlive(GodotObject value, HashSet<ulong> seen)
    {
        if (value == null || !GodotObject.IsInstanceValid(value))
            return;
        ulong instanceId = value.GetInstanceId();
        if (instanceId != 0 && !seen.Add(instanceId))
            return;
        if (instanceId == 0 || BorrowedResourceKeepAliveIds.Add(instanceId))
            BorrowedResourceKeepAlive.Add(value);
        if (value is not Resource)
            return;

        Type currentType = value.GetType();
        while (
            currentType != null
            && currentType != typeof(Resource)
            && currentType != typeof(RefCounted)
            && currentType != typeof(GodotObject)
        )
        {
            foreach (
                FieldInfo field in currentType.GetFields(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly
                )
            )
            {
                KeepBorrowedObjectAlive(ReadFieldValue(field, value), seen);
            }
            foreach (
                PropertyInfo property in currentType.GetProperties(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly
                )
            )
            {
                if (!property.CanRead || property.GetIndexParameters().Length > 0)
                    continue;
                KeepBorrowedObjectAlive(ReadPropertyValue(property, value), seen);
            }
            currentType = currentType.BaseType;
        }
    }

    private static object ReadFieldValue(FieldInfo field, object owner)
    {
        try
        {
            return field.GetValue(owner);
        }
        catch
        {
            return null;
        }
    }

    private static object ReadPropertyValue(PropertyInfo property, object owner)
    {
        try
        {
            return property.GetValue(owner);
        }
        catch
        {
            return null;
        }
    }

    private static void SuppressBorrowedObject(object value, HashSet<ulong> seen)
    {
        switch (value)
        {
            case null:
                return;
            case GodotObject godotObject:
                SuppressBorrowedResourceGraph(godotObject, seen);
                return;
            case Variant variant:
                SuppressBorrowedVariant(variant, seen);
                return;
            case Godot.Collections.Array array:
                foreach (Variant entry in array)
                    SuppressBorrowedVariant(entry, seen);
                return;
            case Godot.Collections.Dictionary dictionary:
                foreach (Variant entry in dictionary.Values)
                    SuppressBorrowedVariant(entry, seen);
                return;
        }
    }

    private static void KeepBorrowedObjectAlive(object value, HashSet<ulong> seen)
    {
        switch (value)
        {
            case null:
                return;
            case GodotObject godotObject:
                KeepBorrowedResourceGraphAlive(godotObject, seen);
                return;
            case Variant variant:
                KeepBorrowedVariantAlive(variant, seen);
                return;
            case Godot.Collections.Array array:
                foreach (Variant entry in array)
                    KeepBorrowedVariantAlive(entry, seen);
                return;
            case Godot.Collections.Dictionary dictionary:
                foreach (Variant entry in dictionary.Values)
                    KeepBorrowedVariantAlive(entry, seen);
                return;
        }
    }

    private static void SuppressBorrowedVariant(Variant value, HashSet<ulong> seen)
    {
        if (value.VariantType == Variant.Type.Object)
        {
            SuppressBorrowedResourceGraph(value.AsGodotObject(), seen);
            return;
        }
        if (value.VariantType == Variant.Type.Array)
        {
            foreach (Variant entry in value.AsGodotArray())
                SuppressBorrowedVariant(entry, seen);
            return;
        }
        if (value.VariantType == Variant.Type.Dictionary)
        {
            foreach (Variant entry in value.AsGodotDictionary().Values)
                SuppressBorrowedVariant(entry, seen);
        }
    }

    private static void KeepBorrowedVariantAlive(Variant value, HashSet<ulong> seen)
    {
        if (value.VariantType == Variant.Type.Object)
        {
            KeepBorrowedResourceGraphAlive(value.AsGodotObject(), seen);
            return;
        }
        if (value.VariantType == Variant.Type.Array)
        {
            foreach (Variant entry in value.AsGodotArray())
                KeepBorrowedVariantAlive(entry, seen);
            return;
        }
        if (value.VariantType == Variant.Type.Dictionary)
        {
            foreach (Variant entry in value.AsGodotDictionary().Values)
                KeepBorrowedVariantAlive(entry, seen);
        }
    }

    private static void DisposeTyped(RefCounted value)
    {
        switch (value)
        {
            case WarehouseState warehouseState:
                warehouseState.Dispose();
                return;
            case RecipeContentRegistry recipeContentRegistry:
                recipeContentRegistry.Dispose();
                return;
            case ItemContentRegistry itemContentRegistry:
                itemContentRegistry.Dispose();
                return;
            case EquipmentInstanceState equipmentInstanceState:
                equipmentInstanceState.Dispose();
                return;
            case PendingCharacterReward pendingCharacterReward:
                pendingCharacterReward.Dispose();
                return;
            case BattleSpecialProfileRegistry battleSpecialProfileRegistry:
                battleSpecialProfileRegistry.Dispose();
                return;
            case BarrierContentRegistry barrierContentRegistry:
                barrierContentRegistry.Dispose();
                return;
            case EquipmentState equipmentState:
                equipmentState.Dispose();
                return;
            case UnitProgress unitProgress:
                unitProgress.Dispose();
                return;
            case UnitProfessionProgress unitProfessionProgress:
                unitProfessionProgress.Dispose();
                return;
            case ProgressionContentRegistry progressionContentRegistry:
                progressionContentRegistry.Dispose();
                return;
            case PartyState partyState:
                partyState.Dispose();
                return;
            case IdentityContentRegistryBase identityContentRegistry:
                identityContentRegistry.Dispose();
                return;
            case ProfessionContentRegistry professionContentRegistry:
                professionContentRegistry.Dispose();
                return;
            case PartyMemberState partyMemberState:
                partyMemberState.Dispose();
                return;
            case TraitInstanceState traitInstanceState:
                traitInstanceState.Dispose();
                return;
            case SkillContentRegistry skillContentRegistry:
                skillContentRegistry.Dispose();
                return;
            case EnemyContentRegistry enemyContentRegistry:
                enemyContentRegistry.Dispose();
                return;
            default:
                value.Dispose();
                return;
        }
    }
}
