using System.Collections.Generic;
using Godot;

public sealed class ContingencySetupMutationResult
{
    public bool Ok { get; init; }
    public string ErrorCode { get; init; } = "";
    public StringName MemberId { get; init; } = "";
    public StringName SetupId { get; init; } = "";
    public bool Charged { get; init; }
    public int ReservedMpMax { get; init; }
    public int EffectiveMpMax { get; init; }
    public IReadOnlyList<ContingencyMaterialCostState> MaterialCosts { get; init; } =
        System.Array.Empty<ContingencyMaterialCostState>();

    public static ContingencySetupMutationResult Success(
        StringName memberId,
        StringName setupId,
        bool charged,
        int reservedMpMax,
        int effectiveMpMax,
        IReadOnlyList<ContingencyMaterialCostState> materialCosts = null
    ) =>
        new()
        {
            Ok = true,
            MemberId = memberId,
            SetupId = setupId,
            Charged = charged,
            ReservedMpMax = Mathf.Max(reservedMpMax, 0),
            EffectiveMpMax = Mathf.Max(effectiveMpMax, 0),
            MaterialCosts = DuplicateCosts(materialCosts),
        };

    public static ContingencySetupMutationResult Failure(
        string errorCode,
        StringName memberId = default,
        StringName setupId = default
    ) =>
        new()
        {
            Ok = false,
            ErrorCode = errorCode ?? "",
            MemberId = memberId,
            SetupId = setupId,
        };

    private static IReadOnlyList<ContingencyMaterialCostState> DuplicateCosts(
        IReadOnlyList<ContingencyMaterialCostState> costs
    )
    {
        if (costs == null || costs.Count == 0)
            return System.Array.Empty<ContingencyMaterialCostState>();
        List<ContingencyMaterialCostState> result = new();
        foreach (ContingencyMaterialCostState cost in costs)
            if (cost != null)
                result.Add(cost.DuplicateState());
        return result;
    }
}
