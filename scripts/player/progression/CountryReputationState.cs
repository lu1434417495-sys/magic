using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public sealed class CountryReputationState
{
    private readonly Dictionary<StringName, int> _values = new();

    public IReadOnlyDictionary<StringName, int> ValuesTyped => _values;
    public int Count => _values.Count;

    public int Get(StringName countryId)
    {
        StringName normalizedId = NormalizeCountryId(countryId);
        return normalizedId != "" && _values.TryGetValue(normalizedId, out int value)
            ? value
            : 0;
    }

    public bool ContainsCountry(StringName countryId)
    {
        StringName normalizedId = NormalizeCountryId(countryId);
        return normalizedId != "" && _values.ContainsKey(normalizedId);
    }

    public int Set(StringName countryId, int value)
    {
        StringName normalizedId = RequireCountryId(countryId);
        int clampedValue = SocialStandingRules.ClampCountryReputation(value);
        _values[normalizedId] = clampedValue;
        return clampedValue;
    }

    public int Add(StringName countryId, int delta)
    {
        StringName normalizedId = RequireCountryId(countryId);
        int nextValue = SocialStandingRules.ClampCountryReputation(
            (long)Get(normalizedId) + delta
        );
        _values[normalizedId] = nextValue;
        return nextValue;
    }

    public CountryReputationState DuplicateState()
    {
        var result = new CountryReputationState();
        foreach (StringName countryId in GetSortedCountryIds())
            result._values[countryId] = _values[countryId];
        return result;
    }

    internal Dictionary<string, object> BuildSaveSnapshotPlain()
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (StringName countryId in GetSortedCountryIds())
            result[countryId.ToString()] = _values[countryId];
        return result;
    }

    public GDictionary ToDictionary()
    {
        var result = new GDictionary();
        foreach (StringName countryId in GetSortedCountryIds())
            result[countryId.ToString()] = _values[countryId];
        return result;
    }

    public static CountryReputationState FromDictionary(GDictionary payload)
    {
        if (payload == null)
        {
            throw new ArgumentException(
                "country reputation payload is required",
                nameof(payload)
            );
        }

        var result = new CountryReputationState();
        foreach (Variant rawKey in payload.Keys)
        {
            if (
                rawKey.VariantType != Variant.Type.String
                && rawKey.VariantType != Variant.Type.StringName
            )
            {
                throw new ArgumentException(
                    "country reputation keys must be strings or StringNames"
                );
            }
            StringName countryId = NormalizeCountryId(
                ProgressionDataUtils.to_string_name(rawKey)
            );
            if (countryId == "")
                throw new ArgumentException("country reputation contains an empty country id");
            if (result._values.ContainsKey(countryId))
            {
                throw new ArgumentException(
                    $"country reputation contains duplicate country id {countryId}"
                );
            }

            Variant rawValue = payload[rawKey];
            if (rawValue.VariantType != Variant.Type.Int)
                throw new ArgumentException($"country reputation[{countryId}] is not an int");
            long value = rawValue.AsInt64();
            if (!SocialStandingRules.IsValidCountryReputation(value))
            {
                throw new ArgumentException(
                    $"country reputation[{countryId}] must be between "
                        + $"{SocialStandingRules.MinCountryReputation} and "
                        + $"{SocialStandingRules.MaxCountryReputation}"
                );
            }
            result._values[countryId] = (int)value;
        }
        return result;
    }

    private static StringName RequireCountryId(StringName countryId)
    {
        StringName normalizedId = NormalizeCountryId(countryId);
        if (normalizedId == "")
            throw new ArgumentException("country id is required", nameof(countryId));
        return normalizedId;
    }

    private static StringName NormalizeCountryId(StringName countryId)
    {
        string normalized = countryId.ToString().Trim();
        return normalized.Length == 0 ? new StringName("") : new StringName(normalized);
    }

    private List<StringName> GetSortedCountryIds()
    {
        var result = new List<StringName>(_values.Keys);
        result.Sort((a, b) => string.CompareOrdinal(a.ToString(), b.ToString()));
        return result;
    }
}
