using Godot;
using System;
using System.Collections.Generic;

public static class QuestProviderContentRules
{
    public static readonly StringName PROVIDER_CONTRACT_BOARD = "service_contract_board";
    public static readonly StringName PROVIDER_BOUNTY_REGISTRY = "service_bounty_registry";

    public static readonly IReadOnlySet<StringName> SUPPORTED_PROVIDER_IDS =
        new HashSet<StringName>
    {
        PROVIDER_CONTRACT_BOARD,
        PROVIDER_BOUNTY_REGISTRY,
    };

    public static bool IsSupportedProviderId(StringName value) =>
        SUPPORTED_PROVIDER_IDS.Contains(value);

    public static IReadOnlySet<StringName> SupportedProviderIds() => SUPPORTED_PROVIDER_IDS;

    public static bool is_supported_provider_id(StringName value) => IsSupportedProviderId(value);

    public static string SupportedProviderLabel()
    {
        var labels = new List<string>();
        foreach (var key in SUPPORTED_PROVIDER_IDS)
            labels.Add(key.ToString());
        labels.Sort(StringComparer.Ordinal);
        return string.Join(", ", labels);
    }

    public static string supported_provider_label() => SupportedProviderLabel();

    public static StringName NormalizeStringName(StringName value) => value;

    public static StringName normalize_string_name(StringName value) => NormalizeStringName(value);
}
