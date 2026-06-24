using System;

public partial class EquipmentInstanceState
{
    public EquipmentInstanceState()
    {
        RuntimeStateLifecycle.MarkFinalizerless(this, GetType().Name);
    }
}

public partial class PartyState
{
    public PartyState()
    {
        RuntimeStateLifecycle.MarkFinalizerless(this, GetType().Name);
    }
}

public partial class PendingProfessionChoice
{
    public PendingProfessionChoice()
    {
        RuntimeStateLifecycle.MarkFinalizerless(this, GetType().Name);
    }
}

public partial class ProfessionPromotionRecord
{
    public ProfessionPromotionRecord()
    {
        RuntimeStateLifecycle.MarkFinalizerless(this, GetType().Name);
    }
}

public partial class QuestState
{
    public QuestState()
    {
        RuntimeStateLifecycle.MarkFinalizerless(this, GetType().Name);
    }
}

public partial class TraitInstanceState
{
    public TraitInstanceState()
    {
        RuntimeStateLifecycle.MarkFinalizerless(this, GetType().Name);
    }
}

public partial class TraitRollValueState
{
    public TraitRollValueState()
    {
        RuntimeStateLifecycle.MarkFinalizerless(this, GetType().Name);
    }
}
