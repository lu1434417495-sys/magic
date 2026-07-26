using System.Collections.Generic;
using Godot;

internal interface IMisfortuneGuidanceBattleQuery
{
    IReadOnlyDictionary<StringName, int> GetCalamityByMemberIdSnapshot();

    bool HasMisfortuneReason(StringName memberId, StringName reasonId);
}
