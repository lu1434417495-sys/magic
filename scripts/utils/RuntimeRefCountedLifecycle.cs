public partial class PartyState
{
    public PartyState()
    {
        RuntimeStateLifecycle.MarkFinalizerless(this, GetType().Name);
    }
}
