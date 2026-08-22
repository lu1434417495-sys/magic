internal enum ProfessionReactivationMode
{
    Unknown = 0,
    Auto,
    Manual,
}

internal enum ProfessionDependencyVisibilityMode
{
    Unknown = 0,
    CountWhenHidden,
    IgnoreWhenHidden,
}

internal enum ProfessionBaseAttackProgression
{
    Unknown = 0,
    Full,
    ThreeQuarter,
    Half,
}

internal enum ProfessionGateCheckMode
{
    Unknown = 0,
    Historical,
    ActiveOnly,
}

internal enum ProfessionActiveConditionKind
{
    Unknown = 0,
    AttributeRange,
    ReputationRange,
}
