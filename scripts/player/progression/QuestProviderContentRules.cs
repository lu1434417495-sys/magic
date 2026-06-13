using Godot;
using System.Collections.Generic;

internal enum QuestProviderKind
{
    Unknown = 0,
    ContractBoard,
    BountyRegistry,
}

public static class QuestProviderContentRules
{
    private static readonly StringName ProviderContractBoard = "service_contract_board";
    private static readonly StringName ProviderBountyRegistry = "service_bounty_registry";

    public static bool IsSupportedProviderId(StringName value) =>
        ToProviderKind(value) != QuestProviderKind.Unknown;

    public static IReadOnlyList<StringName> SupportedProviderIds() =>
        new List<StringName> { ProviderContractBoard, ProviderBountyRegistry };

    public static string SupportedProviderLabel()
    {
        return "service_bounty_registry, service_contract_board";
    }

    internal static QuestProviderKind ToProviderKind(StringName value)
    {
        if (value == ProviderContractBoard)
            return QuestProviderKind.ContractBoard;
        if (value == ProviderBountyRegistry)
            return QuestProviderKind.BountyRegistry;
        return QuestProviderKind.Unknown;
    }

    internal static StringName ToStringName(QuestProviderKind kind)
    {
        return kind switch
        {
            QuestProviderKind.ContractBoard => ProviderContractBoard,
            QuestProviderKind.BountyRegistry => ProviderBountyRegistry,
            _ => "",
        };
    }
}
