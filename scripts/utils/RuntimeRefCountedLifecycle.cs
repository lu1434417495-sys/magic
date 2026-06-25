public partial class PartyState
{
    public PartyState()
    {
        RuntimeStateLifecycle.MarkValueGraphFinalizerless(this, GetType().Name);
    }
}
