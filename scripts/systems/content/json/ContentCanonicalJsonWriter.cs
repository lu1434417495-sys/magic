using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

internal sealed class ContentCanonicalJsonWriter
{
    private static readonly JsonDocumentOptions StrictDocumentOptions = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
    };

    internal string Write<T>(
        T model,
        ContentCanonicalJsonObjectSchema<T> schema,
        bool indented = true
    )
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(schema);

        var buffer = new ArrayBufferWriter<byte>();
        using (
            var jsonWriter = new Utf8JsonWriter(
                buffer,
                new JsonWriterOptions
                {
                    Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
                    Indented = indented,
                    SkipValidation = false,
                }
            )
        )
        {
            schema.Write(jsonWriter, model);
            jsonWriter.Flush();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    internal bool JsonEqualsIgnoringObjectPropertyOrder(string leftJson, string rightJson)
    {
        ArgumentNullException.ThrowIfNull(leftJson);
        ArgumentNullException.ThrowIfNull(rightJson);

        using JsonDocument left = JsonDocument.Parse(leftJson, StrictDocumentOptions);
        using JsonDocument right = JsonDocument.Parse(rightJson, StrictDocumentOptions);
        return JsonElementsEqual(left.RootElement, right.RootElement);
    }

    private static bool JsonElementsEqual(JsonElement left, JsonElement right)
    {
        if (left.ValueKind != right.ValueKind)
            return false;

        switch (left.ValueKind)
        {
            case JsonValueKind.Object:
                return JsonObjectsEqual(left, right);
            case JsonValueKind.Array:
                return JsonArraysEqual(left, right);
            case JsonValueKind.String:
                return string.Equals(left.GetString(), right.GetString(), StringComparison.Ordinal);
            case JsonValueKind.Number:
                // Both parity operands are writer output, so the same finite value has the same
                // round-trip number token. This intentionally does not broaden equality to lossy
                // numeric coercions such as decimal-versus-double conversion.
                return string.Equals(
                    left.GetRawText(),
                    right.GetRawText(),
                    StringComparison.Ordinal
                );
            case JsonValueKind.True:
            case JsonValueKind.False:
            case JsonValueKind.Null:
                return true;
            default:
                throw new JsonException($"Unsupported JSON token kind '{left.ValueKind}'.");
        }
    }

    private static bool JsonObjectsEqual(JsonElement left, JsonElement right)
    {
        Dictionary<string, JsonElement> leftProperties = IndexObject(left);
        Dictionary<string, JsonElement> rightProperties = IndexObject(right);
        if (leftProperties.Count != rightProperties.Count)
            return false;

        foreach ((string propertyName, JsonElement leftValue) in leftProperties)
        {
            if (
                !rightProperties.TryGetValue(propertyName, out JsonElement rightValue)
                || !JsonElementsEqual(leftValue, rightValue)
            )
            {
                return false;
            }
        }

        return true;
    }

    private static Dictionary<string, JsonElement> IndexObject(JsonElement element)
    {
        var properties = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!properties.TryAdd(property.Name, property.Value))
            {
                throw new JsonException(
                    $"Canonical JSON comparison rejects duplicate property '{property.Name}'."
                );
            }
        }

        return properties;
    }

    private static bool JsonArraysEqual(JsonElement left, JsonElement right)
    {
        JsonElement.ArrayEnumerator leftItems = left.EnumerateArray();
        JsonElement.ArrayEnumerator rightItems = right.EnumerateArray();
        while (true)
        {
            bool hasLeft = leftItems.MoveNext();
            bool hasRight = rightItems.MoveNext();
            if (hasLeft != hasRight)
                return false;
            if (!hasLeft)
                return true;
            if (!JsonElementsEqual(leftItems.Current, rightItems.Current))
                return false;
        }
    }
}

internal sealed class ContentCanonicalJsonObjectSchema<T>
{
    private readonly IReadOnlyList<ContentCanonicalJsonProperty<T>> _properties;

    internal ContentCanonicalJsonObjectSchema(
        params ContentCanonicalJsonProperty<T>[] propertiesInDeclarationOrder
    )
    {
        ArgumentNullException.ThrowIfNull(propertiesInDeclarationOrder);
        var properties = new List<ContentCanonicalJsonProperty<T>>(
            propertiesInDeclarationOrder.Length
        );
        var propertyNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (ContentCanonicalJsonProperty<T> property in propertiesInDeclarationOrder)
        {
            if (property == null)
            {
                throw new ArgumentException(
                    "Canonical JSON schema properties cannot contain null.",
                    nameof(propertiesInDeclarationOrder)
                );
            }
            if (!propertyNames.Add(property.JsonName))
            {
                throw new ArgumentException(
                    $"Canonical JSON schema declares duplicate property '{property.JsonName}'.",
                    nameof(propertiesInDeclarationOrder)
                );
            }
            properties.Add(property);
        }

        _properties = properties.AsReadOnly();
    }

    internal void Write(Utf8JsonWriter writer, T model)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(model);

        writer.WriteStartObject();
        foreach (ContentCanonicalJsonProperty<T> property in _properties)
            property.Write(writer, model);
        writer.WriteEndObject();
    }
}

internal abstract class ContentCanonicalJsonProperty<TObject>
{
    private protected ContentCanonicalJsonProperty(string jsonName)
    {
        if (string.IsNullOrWhiteSpace(jsonName))
            throw new ArgumentException("Canonical JSON property name is required.", nameof(jsonName));
        JsonName = jsonName;
    }

    internal string JsonName { get; }

    internal static ContentCanonicalJsonProperty<TObject> Required<TValue>(
        string jsonName,
        Func<TObject, TValue> getter,
        ContentCanonicalJsonValueSchema<TValue> valueSchema
    ) => new RequiredProperty<TValue>(jsonName, getter, valueSchema);

    internal static ContentCanonicalJsonProperty<TObject> Optional<TValue>(
        string jsonName,
        Func<TObject, TValue> getter,
        TValue defaultValue,
        ContentCanonicalJsonValueSchema<TValue> valueSchema,
        IEqualityComparer<TValue> comparer = null
    ) => new OptionalProperty<TValue>(jsonName, getter, defaultValue, valueSchema, comparer);

    internal abstract void Write(Utf8JsonWriter writer, TObject model);

    private sealed class RequiredProperty<TValue> : ContentCanonicalJsonProperty<TObject>
    {
        private readonly Func<TObject, TValue> _getter;
        private readonly ContentCanonicalJsonValueSchema<TValue> _valueSchema;

        internal RequiredProperty(
            string jsonName,
            Func<TObject, TValue> getter,
            ContentCanonicalJsonValueSchema<TValue> valueSchema
        )
            : base(jsonName)
        {
            _getter = getter ?? throw new ArgumentNullException(nameof(getter));
            _valueSchema = valueSchema ?? throw new ArgumentNullException(nameof(valueSchema));
        }

        internal override void Write(Utf8JsonWriter writer, TObject model)
        {
            writer.WritePropertyName(JsonName);
            _valueSchema.Write(writer, _getter(model));
        }
    }

    private sealed class OptionalProperty<TValue> : ContentCanonicalJsonProperty<TObject>
    {
        private readonly IEqualityComparer<TValue> _comparer;
        private readonly TValue _defaultValue;
        private readonly Func<TObject, TValue> _getter;
        private readonly ContentCanonicalJsonValueSchema<TValue> _valueSchema;

        internal OptionalProperty(
            string jsonName,
            Func<TObject, TValue> getter,
            TValue defaultValue,
            ContentCanonicalJsonValueSchema<TValue> valueSchema,
            IEqualityComparer<TValue> comparer
        )
            : base(jsonName)
        {
            _getter = getter ?? throw new ArgumentNullException(nameof(getter));
            _valueSchema = valueSchema ?? throw new ArgumentNullException(nameof(valueSchema));
            _defaultValue = defaultValue;
            _comparer = comparer ?? EqualityComparer<TValue>.Default;
        }

        internal override void Write(Utf8JsonWriter writer, TObject model)
        {
            TValue value = _getter(model);

            // Default omission is safe at the writer stage only because converter output has no
            // template. If a later task extracts a template, every template-owned key must be made
            // explicit on each child entry (even when equal to this DTO default) before parity is
            // rechecked; otherwise a missing key would silently inherit the template value.
            if (_comparer.Equals(value, _defaultValue))
                return;

            writer.WritePropertyName(JsonName);
            _valueSchema.Write(writer, value);
        }
    }
}

internal abstract class ContentCanonicalJsonValueSchema<T>
{
    internal abstract void Write(Utf8JsonWriter writer, T value);
}

internal static class ContentCanonicalJsonValue
{
    internal static ContentCanonicalJsonValueSchema<string> Text { get; } =
        new TextValueSchema();

    internal static ContentCanonicalJsonValueSchema<bool> Boolean { get; } =
        new BooleanValueSchema();

    internal static ContentCanonicalJsonValueSchema<int> Int32 { get; } =
        new Int32ValueSchema();

    internal static ContentCanonicalJsonValueSchema<long> Int64 { get; } =
        new Int64ValueSchema();

    internal static ContentCanonicalJsonValueSchema<float> Single { get; } =
        new SingleValueSchema();

    internal static ContentCanonicalJsonValueSchema<double> Double { get; } =
        new DoubleValueSchema();

    internal static ContentCanonicalJsonValueSchema<TValue> StableBusinessString<TValue>(
        Func<TValue, string> selector
    ) => new StableBusinessStringValueSchema<TValue>(selector);

    internal static ContentCanonicalJsonValueSchema<TObject> Object<TObject>(
        ContentCanonicalJsonObjectSchema<TObject> schema
    ) => new ObjectValueSchema<TObject>(schema);

    internal static ContentCanonicalJsonValueSchema<IReadOnlyList<TElement>> Array<TElement>(
        ContentCanonicalJsonValueSchema<TElement> elementSchema
    ) => new ArrayValueSchema<TElement>(elementSchema);

    internal static ContentCanonicalJsonValueSchema<TMap> OrderedObjectMap<
        TMap,
        TKey,
        TValue
    >(
        Func<TMap, IEnumerable<KeyValuePair<TKey, TValue>>> entrySelector,
        Func<TKey, string> canonicalKeySelector,
        IComparer<string> canonicalKeyComparer,
        ContentCanonicalJsonValueSchema<TValue> valueSchema
    ) =>
        new OrderedObjectMapValueSchema<TMap, TKey, TValue>(
            entrySelector,
            canonicalKeySelector,
            canonicalKeyComparer,
            valueSchema
        );

    internal static ContentCanonicalJsonValueSchema<TValue> ClosedUnion<TValue, TKind>(
        Func<TValue, TKind> kindSelector,
        params ContentCanonicalJsonUnionCase<TValue, TKind>[] cases
    ) => new ClosedUnionValueSchema<TValue, TKind>(kindSelector, cases);

    internal static ContentCanonicalJsonValueSchema<TValue> NullableReference<TValue>(
        ContentCanonicalJsonValueSchema<TValue> valueSchema
    )
        where TValue : class => new NullableReferenceValueSchema<TValue>(valueSchema);

    internal static ContentCanonicalJsonValueSchema<TValue?> NullableValue<TValue>(
        ContentCanonicalJsonValueSchema<TValue> valueSchema
    )
        where TValue : struct => new NullableValueValueSchema<TValue>(valueSchema);

    private sealed class TextValueSchema : ContentCanonicalJsonValueSchema<string>
    {
        internal override void Write(Utf8JsonWriter writer, string value)
        {
            if (value == null)
                throw new JsonException("Canonical JSON string value cannot be null.");
            writer.WriteStringValue(value);
        }
    }

    private sealed class BooleanValueSchema : ContentCanonicalJsonValueSchema<bool>
    {
        internal override void Write(Utf8JsonWriter writer, bool value) =>
            writer.WriteBooleanValue(value);
    }

    private sealed class Int32ValueSchema : ContentCanonicalJsonValueSchema<int>
    {
        internal override void Write(Utf8JsonWriter writer, int value) =>
            writer.WriteNumberValue(value);
    }

    private sealed class Int64ValueSchema : ContentCanonicalJsonValueSchema<long>
    {
        internal override void Write(Utf8JsonWriter writer, long value) =>
            writer.WriteNumberValue(value);
    }

    private sealed class SingleValueSchema : ContentCanonicalJsonValueSchema<float>
    {
        internal override void Write(Utf8JsonWriter writer, float value)
        {
            if (!float.IsFinite(value))
                throw new JsonException("Canonical JSON float value must be finite.");
            writer.WriteNumberValue(value);
        }
    }

    private sealed class DoubleValueSchema : ContentCanonicalJsonValueSchema<double>
    {
        internal override void Write(Utf8JsonWriter writer, double value)
        {
            if (!double.IsFinite(value))
                throw new JsonException("Canonical JSON double value must be finite.");
            writer.WriteNumberValue(value);
        }
    }

    private sealed class StableBusinessStringValueSchema<TValue>
        : ContentCanonicalJsonValueSchema<TValue>
    {
        private readonly Func<TValue, string> _selector;

        internal StableBusinessStringValueSchema(Func<TValue, string> selector)
        {
            _selector = selector ?? throw new ArgumentNullException(nameof(selector));
        }

        internal override void Write(Utf8JsonWriter writer, TValue value)
        {
            string businessString = _selector(value);
            if (businessString == null)
            {
                throw new JsonException(
                    "Canonical JSON business-string selector returned null."
                );
            }
            writer.WriteStringValue(businessString);
        }
    }

    private sealed class ObjectValueSchema<TObject>
        : ContentCanonicalJsonValueSchema<TObject>
    {
        private readonly ContentCanonicalJsonObjectSchema<TObject> _schema;

        internal ObjectValueSchema(ContentCanonicalJsonObjectSchema<TObject> schema)
        {
            _schema = schema ?? throw new ArgumentNullException(nameof(schema));
        }

        internal override void Write(Utf8JsonWriter writer, TObject value) =>
            _schema.Write(writer, value);
    }

    private sealed class ArrayValueSchema<TElement>
        : ContentCanonicalJsonValueSchema<IReadOnlyList<TElement>>
    {
        private readonly ContentCanonicalJsonValueSchema<TElement> _elementSchema;

        internal ArrayValueSchema(ContentCanonicalJsonValueSchema<TElement> elementSchema)
        {
            _elementSchema =
                elementSchema ?? throw new ArgumentNullException(nameof(elementSchema));
        }

        internal override void Write(Utf8JsonWriter writer, IReadOnlyList<TElement> value)
        {
            if (value == null)
                throw new JsonException("Canonical JSON array value cannot be null.");

            writer.WriteStartArray();
            for (int index = 0; index < value.Count; index++)
                _elementSchema.Write(writer, value[index]);
            writer.WriteEndArray();
        }
    }

    private sealed class OrderedObjectMapValueSchema<TMap, TKey, TValue>
        : ContentCanonicalJsonValueSchema<TMap>
    {
        private readonly Func<TMap, IEnumerable<KeyValuePair<TKey, TValue>>> _entrySelector;
        private readonly Func<TKey, string> _canonicalKeySelector;
        private readonly IComparer<string> _canonicalKeyComparer;
        private readonly ContentCanonicalJsonValueSchema<TValue> _valueSchema;

        internal OrderedObjectMapValueSchema(
            Func<TMap, IEnumerable<KeyValuePair<TKey, TValue>>> entrySelector,
            Func<TKey, string> canonicalKeySelector,
            IComparer<string> canonicalKeyComparer,
            ContentCanonicalJsonValueSchema<TValue> valueSchema
        )
        {
            _entrySelector = entrySelector ?? throw new ArgumentNullException(nameof(entrySelector));
            _canonicalKeySelector =
                canonicalKeySelector
                ?? throw new ArgumentNullException(nameof(canonicalKeySelector));
            _canonicalKeyComparer =
                canonicalKeyComparer
                ?? throw new ArgumentNullException(nameof(canonicalKeyComparer));
            _valueSchema = valueSchema ?? throw new ArgumentNullException(nameof(valueSchema));
        }

        internal override void Write(Utf8JsonWriter writer, TMap value)
        {
            if (value is null)
                throw new JsonException("Canonical JSON object-map value cannot be null.");

            IEnumerable<KeyValuePair<TKey, TValue>> sourceEntries =
                _entrySelector(value)
                ?? throw new JsonException(
                    "Canonical JSON object-map entry selector returned null."
                );
            var entries = new List<CanonicalObjectMapEntry<TValue>>();
            var propertyNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (KeyValuePair<TKey, TValue> sourceEntry in sourceEntries)
            {
                string canonicalKey = _canonicalKeySelector(sourceEntry.Key);
                if (canonicalKey == null)
                {
                    throw new JsonException(
                        "Canonical JSON object-map key selector returned null."
                    );
                }
                if (!propertyNames.Add(canonicalKey))
                {
                    throw new JsonException(
                        $"Canonical JSON object-map contains duplicate property '{canonicalKey}'."
                    );
                }
                entries.Add(new CanonicalObjectMapEntry<TValue>(canonicalKey, sourceEntry.Value));
            }

            try
            {
                entries.Sort(
                    (left, right) =>
                    {
                        int result = _canonicalKeyComparer.Compare(
                            left.CanonicalKey,
                            right.CanonicalKey
                        );
                        return result != 0
                            ? result
                            : StringComparer.Ordinal.Compare(
                                left.CanonicalKey,
                                right.CanonicalKey
                            );
                    }
                );
            }
            catch (InvalidOperationException exception)
            {
                throw new JsonException(
                    "Canonical JSON object-map key comparison failed.",
                    exception.InnerException ?? exception
                );
            }

            writer.WriteStartObject();
            foreach (CanonicalObjectMapEntry<TValue> entry in entries)
            {
                writer.WritePropertyName(entry.CanonicalKey);
                _valueSchema.Write(writer, entry.Value);
            }
            writer.WriteEndObject();
        }
    }

    private sealed class ClosedUnionValueSchema<TValue, TKind>
        : ContentCanonicalJsonValueSchema<TValue>
    {
        private readonly Func<TValue, TKind> _kindSelector;
        private readonly IReadOnlyDictionary<
            TKind,
            ContentCanonicalJsonUnionCase<TValue, TKind>
        > _cases;

        internal ClosedUnionValueSchema(
            Func<TValue, TKind> kindSelector,
            ContentCanonicalJsonUnionCase<TValue, TKind>[] cases
        )
        {
            _kindSelector = kindSelector ?? throw new ArgumentNullException(nameof(kindSelector));
            ArgumentNullException.ThrowIfNull(cases);
            if (cases.Length == 0)
            {
                throw new ArgumentException(
                    "Canonical JSON closed union requires at least one case.",
                    nameof(cases)
                );
            }

            var caseMap = new Dictionary<
                TKind,
                ContentCanonicalJsonUnionCase<TValue, TKind>
            >();
            foreach (ContentCanonicalJsonUnionCase<TValue, TKind> unionCase in cases)
            {
                if (unionCase == null)
                {
                    throw new ArgumentException(
                        "Canonical JSON closed union cases cannot contain null.",
                        nameof(cases)
                    );
                }
                if (!caseMap.TryAdd(unionCase.Kind, unionCase))
                {
                    throw new ArgumentException(
                        $"Canonical JSON closed union declares duplicate kind '{unionCase.Kind}'.",
                        nameof(cases)
                    );
                }
            }
            _cases = new System.Collections.ObjectModel.ReadOnlyDictionary<
                TKind,
                ContentCanonicalJsonUnionCase<TValue, TKind>
            >(caseMap);
        }

        internal override void Write(Utf8JsonWriter writer, TValue value)
        {
            if (value is null)
                throw new JsonException("Canonical JSON closed-union value cannot be null.");

            TKind kind = _kindSelector(value);
            if (
                !_cases.TryGetValue(
                    kind,
                    out ContentCanonicalJsonUnionCase<TValue, TKind> unionCase
                )
            )
            {
                throw new JsonException(
                    $"Canonical JSON closed union has no registered schema for kind '{kind}'."
                );
            }
            unionCase.Write(writer, value);
        }
    }

    private sealed record CanonicalObjectMapEntry<TValue>(string CanonicalKey, TValue Value);

    private sealed class NullableReferenceValueSchema<TValue>
        : ContentCanonicalJsonValueSchema<TValue>
        where TValue : class
    {
        private readonly ContentCanonicalJsonValueSchema<TValue> _valueSchema;

        internal NullableReferenceValueSchema(ContentCanonicalJsonValueSchema<TValue> valueSchema)
        {
            _valueSchema = valueSchema ?? throw new ArgumentNullException(nameof(valueSchema));
        }

        internal override void Write(Utf8JsonWriter writer, TValue value)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }
            _valueSchema.Write(writer, value);
        }
    }

    private sealed class NullableValueValueSchema<TValue>
        : ContentCanonicalJsonValueSchema<TValue?>
        where TValue : struct
    {
        private readonly ContentCanonicalJsonValueSchema<TValue> _valueSchema;

        internal NullableValueValueSchema(ContentCanonicalJsonValueSchema<TValue> valueSchema)
        {
            _valueSchema = valueSchema ?? throw new ArgumentNullException(nameof(valueSchema));
        }

        internal override void Write(Utf8JsonWriter writer, TValue? value)
        {
            if (!value.HasValue)
            {
                writer.WriteNullValue();
                return;
            }
            _valueSchema.Write(writer, value.Value);
        }
    }
}

internal abstract class ContentCanonicalJsonUnionCase<TValue, TKind>
{
    private protected ContentCanonicalJsonUnionCase(TKind kind)
    {
        Kind = kind;
    }

    internal TKind Kind { get; }

    internal static ContentCanonicalJsonUnionCase<TValue, TKind> Create<TCase>(
        TKind kind,
        ContentCanonicalJsonValueSchema<TCase> valueSchema
    )
        where TCase : TValue => new TypedUnionCase<TCase>(kind, valueSchema);

    internal abstract void Write(Utf8JsonWriter writer, TValue value);

    private sealed class TypedUnionCase<TCase>
        : ContentCanonicalJsonUnionCase<TValue, TKind>
        where TCase : TValue
    {
        private readonly ContentCanonicalJsonValueSchema<TCase> _valueSchema;

        internal TypedUnionCase(
            TKind kind,
            ContentCanonicalJsonValueSchema<TCase> valueSchema
        )
            : base(kind)
        {
            _valueSchema = valueSchema ?? throw new ArgumentNullException(nameof(valueSchema));
        }

        internal override void Write(Utf8JsonWriter writer, TValue value)
        {
            if (value is not TCase typedValue)
            {
                throw new JsonException(
                    $"Canonical JSON closed-union kind '{Kind}' received an incompatible payload."
                );
            }
            _valueSchema.Write(writer, typedValue);
        }
    }
}

internal static class ContentCanonicalJsonKey
{
    internal static IComparer<string> InvariantInt32Order { get; } =
        new InvariantInt32KeyComparer();

    internal static string InvariantInt32(int value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private sealed class InvariantInt32KeyComparer : IComparer<string>
    {
        public int Compare(string left, string right)
        {
            if (
                !int.TryParse(
                    left,
                    NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture,
                    out int leftValue
                )
                || !int.TryParse(
                    right,
                    NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture,
                    out int rightValue
                )
            )
            {
                throw new JsonException(
                    "Canonical JSON Int32 object-map comparer received a non-Int32 key."
                );
            }
            if (
                !string.Equals(left, InvariantInt32(leftValue), StringComparison.Ordinal)
                || !string.Equals(right, InvariantInt32(rightValue), StringComparison.Ordinal)
            )
            {
                throw new JsonException(
                    "Canonical JSON Int32 object-map comparer received a non-canonical key."
                );
            }

            int result = leftValue.CompareTo(rightValue);
            return result != 0 ? result : StringComparer.Ordinal.Compare(left, right);
        }
    }
}
