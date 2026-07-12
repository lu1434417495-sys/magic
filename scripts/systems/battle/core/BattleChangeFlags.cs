using System;

[Flags]
internal enum BattleChangeFlags
{
    None = 0,
    UnitState = 1 << 0,
    UnitPlacement = 1 << 1,
    AffectedCoords = 1 << 2,
    CellState = 1 << 3,
    PropState = 1 << 4,
    Timeline = 1 << 5,
    Phase = 1 << 6,
    BattleLifecycle = 1 << 7,
    Modal = 1 << 8,
    Log = 1 << 9,
    Progression = 1 << 10,
    Report = 1 << 11,
}
