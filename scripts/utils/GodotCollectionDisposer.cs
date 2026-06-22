using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

internal static class GodotCollectionDisposer
{
    internal static void DisposeWrapperOnly(GArray values)
    {
        if (values == null)
            return;
        GC.SuppressFinalize(values);
        values.Dispose();
    }

    internal static void DisposeWrapperOnly(GDictionary values)
    {
        if (values == null)
            return;
        GC.SuppressFinalize(values);
        values.Dispose();
    }

    internal static void DisposeWrapperOnly<[MustBeVariant] T>(Godot.Collections.Array<T> values)
    {
        if (values == null)
            return;
        GC.SuppressFinalize(values);
        GArray rawValues = (GArray)values;
        GC.SuppressFinalize(rawValues);
        rawValues.Dispose();
    }

    internal static void DisposeOwnedCollectionWrapper(GArray values)
    {
        if (values == null)
            return;
        values.Clear();
        DisposeWrapperOnly(values);
    }

    internal static void DisposeOwnedCollectionWrapper(GDictionary values)
    {
        if (values == null)
            return;
        values.Clear();
        DisposeWrapperOnly(values);
    }

    internal static void DisposeOwnedCollectionWrapper<[MustBeVariant] T>(
        Godot.Collections.Array<T> values
    )
    {
        if (values == null)
            return;
        values.Clear();
        DisposeWrapperOnly(values);
    }

    internal static void DisposeOwnedPayloadTree(
        GArray values,
        bool suppressObjectFinalizers = false
    )
    {
        if (values == null)
            return;
        foreach (Variant value in values)
        {
            try
            {
                DisposeVariantPayload(value, suppressObjectFinalizers);
            }
            finally
            {
                value.Dispose();
            }
        }
        DisposeOwnedCollectionWrapper(values);
    }

    internal static void DisposeOwnedPayloadTree(
        GDictionary values,
        bool suppressObjectFinalizers = false
    )
    {
        if (values == null)
            return;
        foreach (Variant value in values.Values)
        {
            try
            {
                DisposeVariantPayload(value, suppressObjectFinalizers);
            }
            finally
            {
                value.Dispose();
            }
        }
        DisposeOwnedCollectionWrapper(values);
    }

    internal static void DisposeOwnedPayloadTree<[MustBeVariant] T>(
        Godot.Collections.Array<T> values,
        bool suppressObjectFinalizers = false
    )
    {
        if (values == null)
            return;
        GArray rawValues = (GArray)values;
        foreach (Variant value in rawValues)
        {
            try
            {
                DisposeVariantPayload(value, suppressObjectFinalizers);
            }
            finally
            {
                value.Dispose();
            }
        }
        values.Clear();
        DisposeWrapperOnly(values);
    }

    internal static void SuppressObjectFinalizer(GodotObject value)
    {
        if (value != null)
            GC.SuppressFinalize(value);
    }

    internal static void DisposeRefCountedOnce<T>(T value, HashSet<ulong> disposedInstanceIds)
        where T : RefCounted
    {
        if (value == null || !GodotObject.IsInstanceValid(value))
            return;
        ulong instanceId = value.GetInstanceId();
        if (instanceId != 0 && disposedInstanceIds != null && !disposedInstanceIds.Add(instanceId))
        {
            GC.SuppressFinalize(value);
            return;
        }
        GodotRefCountedDisposer.DisposeIfValid(value);
    }

    private static void DisposeVariantPayload(Variant value, bool suppressObjectFinalizers)
    {
        switch (value.VariantType)
        {
            case Variant.Type.Dictionary:
                DisposeOwnedPayloadTree(value.AsGodotDictionary(), suppressObjectFinalizers);
                return;
            case Variant.Type.Array:
                DisposeOwnedPayloadTree(value.AsGodotArray(), suppressObjectFinalizers);
                return;
            case Variant.Type.Object when suppressObjectFinalizers:
                SuppressObjectFinalizer(value.AsGodotObject());
                return;
        }
    }
}
