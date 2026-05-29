using Godot;

[GlobalClass]
public partial class QuestProviderContentRules : RefCounted
{
    public static readonly StringName PROVIDER_CONTRACT_BOARD = "service_contract_board";
    public static readonly StringName PROVIDER_BOUNTY_REGISTRY = "service_bounty_registry";

    public static readonly Godot.Collections.Dictionary SUPPORTED_PROVIDER_IDS = new()
    {
        { PROVIDER_CONTRACT_BOARD, true },
        { PROVIDER_BOUNTY_REGISTRY, true },
    };

    public static bool IsSupportedProviderId(StringName value) =>
        SUPPORTED_PROVIDER_IDS.ContainsKey(value);

    public static Godot.Collections.Dictionary SupportedProviderIds()
    {
        var d = new Godot.Collections.Dictionary();
        foreach (var k in SUPPORTED_PROVIDER_IDS.Keys)
            d[k] = SUPPORTED_PROVIDER_IDS[k];
        return d;
    }

    public static bool is_supported_provider_id(StringName value) => IsSupportedProviderId(value);

    public static Godot.Collections.Dictionary supported_provider_ids() => SupportedProviderIds();

    public static string SupportedProviderLabel()
    {
        var labels = new System.Collections.Generic.List<string>();
        foreach (var key in SUPPORTED_PROVIDER_IDS.Keys)
            labels.Add((string)(StringName)key);
        labels.Sort();
        return string.Join(", ", labels);
    }

    public static string supported_provider_label() => SupportedProviderLabel();

    public static StringName NormalizeStringName(StringName value) => value;

    public static StringName normalize_string_name(StringName value) => NormalizeStringName(value);
}
