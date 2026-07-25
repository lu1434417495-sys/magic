using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

internal sealed class SettlementActionValidationResult
{
    private readonly Dictionary<string, object> _serviceEntry;

    internal bool Ok { get; }
    internal string Message { get; }
    internal IReadOnlyDictionary<string, object> ServiceEntryPlain =>
        RuntimePlainPayload.CloneDictionary(_serviceEntry);

    private SettlementActionValidationResult(
        bool ok,
        string message,
        IReadOnlyDictionary<string, object> serviceEntry
    )
    {
        Ok = ok;
        Message = message ?? "";
        _serviceEntry = RuntimePlainPayload.CloneDictionary(serviceEntry);
    }

    internal static SettlementActionValidationResult Success(
        GDictionary serviceEntry = null
    ) =>
        new(
            true,
            "",
            RuntimePlainPayload.NormalizeDictionary(
                serviceEntry ?? new GDictionary(),
                "SettlementActionValidationResult.serviceEntry"
            )
        );

    internal static SettlementActionValidationResult Success(
        IReadOnlyDictionary<string, object> serviceEntry
    ) => new(true, "", serviceEntry);

    internal static SettlementActionValidationResult Failure(string message) =>
        new(false, message, null);
}

internal sealed class SettlementServiceEntryResolution
{
    private readonly Dictionary<string, object> _serviceEntry;

    internal IReadOnlyDictionary<string, object> ServiceEntryPlain =>
        RuntimePlainPayload.CloneDictionary(_serviceEntry);
    internal bool IsEnabled { get; }
    internal string DisabledReason { get; }
    internal bool Found => _serviceEntry.Count != 0;

    private SettlementServiceEntryResolution(
        GDictionary serviceEntry,
        bool isEnabled,
        string disabledReason
    )
    {
        _serviceEntry = RuntimePlainPayload.NormalizeDictionary(
            serviceEntry ?? new GDictionary(),
            "SettlementServiceEntryResolution.serviceEntry"
        );
        IsEnabled = isEnabled;
        DisabledReason = disabledReason ?? "";
    }

    internal static SettlementServiceEntryResolution Missing() =>
        new(null, false, "");

    internal static SettlementServiceEntryResolution FromServiceData(
        GDictionary serviceEntry,
        SettlementServiceMetadata metadata
    ) =>
        new(
            serviceEntry,
            metadata?.IsEnabled ?? false,
            metadata?.DisabledReason ?? ""
        );
}

internal static class SettlementActionValidationPolicy
{
    internal static SettlementActionValidationResult ValidateResolvedService(
        SettlementServiceEntryResolution resolution,
        bool requiresEnabledService,
        string unknownServiceMessage,
        string disabledServiceMessage
    )
    {
        ArgumentNullException.ThrowIfNull(resolution);
        if (!resolution.Found)
            return SettlementActionValidationResult.Failure(unknownServiceMessage);
        if (requiresEnabledService && !resolution.IsEnabled)
            return SettlementActionValidationResult.Failure(disabledServiceMessage);
        return SettlementActionValidationResult.Success(resolution.ServiceEntryPlain);
    }
}
