using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

internal sealed class GodotProjectionPayloadOwner : IDisposable
{
    private readonly List<object> _payloads = new();

    internal GDictionary Dictionary() => Track(new GDictionary());

    internal GArray Array() => Track(new GArray());

    internal T Track<T>(T payload)
    {
        if (payload != null)
            _payloads.Add(payload);
        return payload;
    }

    public void Dispose()
    {
        for (int index = _payloads.Count - 1; index >= 0; index--)
            DisposePayload(_payloads[index]);
        _payloads.Clear();
    }

    private static void DisposePayload(object payload)
    {
        switch (payload)
        {
            case null:
                return;
            case Godot.Collections.Array<GDictionary> typedDictionaryArray:
                GodotCollectionDisposer.DisposeOwnedCollectionWrapper(typedDictionaryArray);
                return;
            case GDictionary dictionary:
                GodotCollectionDisposer.DisposeOwnedCollectionWrapper(dictionary);
                return;
            case GArray array:
                GodotCollectionDisposer.DisposeOwnedCollectionWrapper(array);
                return;
            case IDisposable disposable:
                GC.SuppressFinalize(payload);
                disposable.Dispose();
                return;
        }
    }
}
