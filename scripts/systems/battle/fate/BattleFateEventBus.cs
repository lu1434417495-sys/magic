using System;
using Godot;

internal sealed class BattleFateEventBus : IDisposable
{
    public delegate void EventDispatchedEventHandler(BattleFateEventPayload payload);

    public event EventDispatchedEventHandler EventDispatched;

    public void Dispatch(BattleFateEventPayload payload)
    {
        if (payload == null || payload.EventType == "")
            return;

        EventDispatched?.Invoke(payload);
    }

    public void Dispose()
    {
        EventDispatched = null;
    }
}
