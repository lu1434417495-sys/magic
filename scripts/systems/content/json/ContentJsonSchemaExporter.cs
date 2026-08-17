using System;
using System.Buffers;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Unicode;

internal sealed class ContentJsonSchemaExporter
{
    private const string SchemaDraft = "https://json-schema.org/draft/2020-12/schema";
    private readonly NullabilityInfoContext _nullability = new();
    private readonly SortedDictionary<string, JsonObject> _definitions =
        new(StringComparer.Ordinal);
    private readonly Dictionary<Type, string> _definitionKeys = new();
    private readonly Dictionary<string, string> _definitionOwners =
        new(StringComparer.Ordinal);

    internal string Export(ContentJsonSchemaDomainRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        _definitions.Clear();
        _definitionKeys.Clear();
        _definitionOwners.Clear();

        JsonObject documentReference = BuildObjectReference(registration.DocumentDtoType);
        var root = new JsonObject
        {
            ["$schema"] = SchemaDraft,
            ["$id"] =
                $"urn:magic:content-schema:{registration.DomainId}:v{registration.SchemaVersion}",
            ["title"] = registration.Title,
            ["description"] = registration.Description,
            ["$ref"] = documentReference["$ref"]?.GetValue<string>(),
        };

        var definitions = new JsonObject();
        foreach ((string key, JsonObject schema) in _definitions)
            definitions[key] = schema;
        root["$defs"] = definitions;

        var buffer = new ArrayBufferWriter<byte>();
        using (
            var writer = new Utf8JsonWriter(
                buffer,
                new JsonWriterOptions
                {
                    Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
                    Indented = true,
                    SkipValidation = false,
                }
            )
        )
        {
            root.WriteTo(writer);
            writer.Flush();
        }

        string generated = Encoding.UTF8.GetString(buffer.WrittenSpan)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);
        return generated.TrimEnd('\n') + "\n";
    }

    private JsonObject BuildObjectReference(Type type)
    {
        string definitionKey = EnsureDefinition(type);
        return new JsonObject { ["$ref"] = $"#/$defs/{EscapeJsonPointer(definitionKey)}" };
    }

    private string EnsureDefinition(Type type)
    {
        if (_definitionKeys.TryGetValue(type, out string existingKey))
            return existingKey;
        if (!type.IsClass || type == typeof(string) || type.IsGenericTypeDefinition)
        {
            throw new InvalidOperationException(
                $"JSON Schema object DTO '{DisplayType(type)}' must be a closed class type."
            );
        }

        string key = SanitizeDefinitionKey(type.Name);
        ReserveDefinitionKey(key, $"DTO '{DisplayType(type)}'");
        _definitionKeys.Add(type, key);
        _definitions[key] = new JsonObject();

        ContentJsonSchemaClosedKindAttribute closedKind =
            type.GetCustomAttribute<ContentJsonSchemaClosedKindAttribute>();
        _definitions[key] = closedKind == null
            ? BuildObjectDefinition(type)
            : BuildClosedKindDefinition(type, closedKind, key);
        return key;
    }

    private JsonObject BuildObjectDefinition(Type type)
    {
        IReadOnlyList<SerializableProperty> properties = GetSerializableProperties(type);
        var schema = new JsonObject();
        AddDescription(schema, type.GetCustomAttribute<DescriptionAttribute>()?.Description);
        schema["type"] = "object";
        schema["additionalProperties"] = AllowsUnmappedMembers(type);

        var propertySchemas = new JsonObject();
        var required = new JsonArray();
        foreach (SerializableProperty property in properties)
        {
            propertySchemas[property.JsonName] = BuildPropertySchema(property);
            if (property.IsRequired)
                required.Add(property.JsonName);
        }
        schema["properties"] = propertySchemas;
        if (required.Count > 0)
            schema["required"] = required;
        return schema;
    }

    private JsonObject BuildClosedKindDefinition(
        Type type,
        ContentJsonSchemaClosedKindAttribute attribute,
        string definitionKey
    )
    {
        IContentJsonSchemaClosedKindSpec spec = CreateProvider<IContentJsonSchemaClosedKindSpec>(
            attribute.SpecType,
            $"closed-kind DTO '{DisplayType(type)}'"
        );
        IReadOnlyList<SerializableProperty> properties = GetSerializableProperties(type);
        SerializableProperty discriminator = RequireProperty(
            type,
            properties,
            spec.DiscriminatorPropertyName,
            "discriminator"
        );
        SerializableProperty payload = RequireProperty(
            type,
            properties,
            spec.PayloadPropertyName,
            "payload"
        );
        if (!discriminator.IsRequired || !payload.IsRequired)
        {
            throw new InvalidOperationException(
                $"Closed-kind DTO '{DisplayType(type)}' discriminator and payload properties "
                    + "must both be required."
            );
        }
        ValidateClosedKindCarrierProperties(type, discriminator, payload);

        IReadOnlyList<ContentJsonSchemaClosedKindBranch> branches = ValidateBranches(type, spec);
        var oneOf = new JsonArray();
        var mapping = new JsonObject();
        foreach (ContentJsonSchemaClosedKindBranch branch in branches)
        {
            string branchKey =
                $"{definitionKey}__{SanitizeDefinitionKey(branch.Kind)}";
            ReserveDefinitionKey(
                branchKey,
                $"closed-kind branch '{DisplayType(type)}:{branch.Kind}'"
            );

            _definitions[branchKey] = BuildClosedKindBranchDefinition(
                type,
                properties,
                discriminator.JsonName,
                payload.JsonName,
                branch
            );
            string reference = $"#/$defs/{EscapeJsonPointer(branchKey)}";
            oneOf.Add(new JsonObject { ["$ref"] = reference });
            mapping[branch.Kind] = reference;
        }

        var schema = new JsonObject();
        AddDescription(schema, type.GetCustomAttribute<DescriptionAttribute>()?.Description);
        schema["oneOf"] = oneOf;
        schema["discriminator"] = new JsonObject
        {
            ["propertyName"] = discriminator.JsonName,
            ["mapping"] = mapping,
        };
        return schema;
    }

    private JsonObject BuildClosedKindBranchDefinition(
        Type ownerType,
        IReadOnlyList<SerializableProperty> properties,
        string discriminatorName,
        string payloadName,
        ContentJsonSchemaClosedKindBranch branch
    )
    {
        var schema = new JsonObject
        {
            ["type"] = "object",
            ["additionalProperties"] = AllowsUnmappedMembers(ownerType),
        };
        var propertySchemas = new JsonObject();
        var required = new JsonArray();
        foreach (SerializableProperty property in properties)
        {
            if (property.JsonName == discriminatorName)
            {
                propertySchemas[property.JsonName] = new JsonObject
                {
                    ["type"] = "string",
                    ["const"] = branch.Kind,
                };
            }
            else if (property.JsonName == payloadName)
            {
                propertySchemas[property.JsonName] = BuildObjectReference(branch.PayloadDtoType);
            }
            else
            {
                propertySchemas[property.JsonName] = BuildPropertySchema(property);
            }

            if (property.IsRequired)
                required.Add(property.JsonName);
        }
        schema["properties"] = propertySchemas;
        schema["required"] = required;
        return schema;
    }

    private JsonObject BuildPropertySchema(SerializableProperty property)
    {
        NullabilityInfo nullability = _nullability.Create(property.Property);
        JsonObject schema = BuildValueSchema(
            property.Property.PropertyType,
            nullability,
            property.Property
        );
        AddDescription(schema, property.Description);

        ContentJsonSchemaConstAttribute constant =
            property.Property.GetCustomAttribute<ContentJsonSchemaConstAttribute>();
        if (constant != null)
        {
            schema["const"] = constant.Value switch
            {
                string text => JsonValue.Create(text),
                int number => JsonValue.Create(number),
                long number => JsonValue.Create(number),
                bool flag => JsonValue.Create(flag),
                _ => throw new InvalidOperationException(
                    $"JSON Schema const on '{property.Property.DeclaringType?.Name}."
                        + $"{property.Property.Name}' has unsupported CLR type "
                        + $"'{constant.Value.GetType().Name}'."
                ),
            };
        }
        return schema;
    }

    private JsonObject BuildValueSchema(
        Type type,
        NullabilityInfo nullability,
        PropertyInfo declaringProperty = null
    )
    {
        Type nullableValueType = Nullable.GetUnderlyingType(type);
        if (nullableValueType != null)
        {
            return WrapNullable(
                BuildNonNullableValueSchema(nullableValueType, nullability, declaringProperty)
            );
        }

        if (!type.IsValueType)
        {
            if (nullability.ReadState == NullabilityState.Unknown)
            {
                throw new InvalidOperationException(
                    $"Reference property '{declaringProperty?.DeclaringType?.Name}."
                        + $"{declaringProperty?.Name}' has unknown nullable metadata. "
                        + "Enable nullable annotations for the DTO file and declare the property "
                        + "as nullable or non-nullable explicitly."
                );
            }

            JsonObject nonNullable = BuildNonNullableValueSchema(
                type,
                nullability,
                declaringProperty
            );
            return nullability.ReadState == NullabilityState.Nullable
                ? WrapNullable(nonNullable)
                : nonNullable;
        }

        return BuildNonNullableValueSchema(type, nullability, declaringProperty);
    }

    private JsonObject BuildNonNullableValueSchema(
        Type type,
        NullabilityInfo nullability,
        PropertyInfo declaringProperty
    )
    {
        ContentJsonSchemaStableStringValuesAttribute stableStrings =
            declaringProperty?.GetCustomAttribute<ContentJsonSchemaStableStringValuesAttribute>()
            ?? type.GetCustomAttribute<ContentJsonSchemaStableStringValuesAttribute>();
        if (stableStrings != null)
            return BuildStableStringSchema(stableStrings, declaringProperty, type);

        if (type == typeof(string) || type == typeof(char))
            return new JsonObject { ["type"] = "string" };
        if (type == typeof(bool))
            return new JsonObject { ["type"] = "boolean" };
        if (
            type == typeof(byte)
            || type == typeof(sbyte)
            || type == typeof(short)
            || type == typeof(ushort)
            || type == typeof(int)
            || type == typeof(uint)
            || type == typeof(long)
            || type == typeof(ulong)
        )
        {
            return new JsonObject { ["type"] = "integer" };
        }
        if (
            type == typeof(float)
            || type == typeof(double)
            || type == typeof(decimal)
        )
        {
            return new JsonObject { ["type"] = "number" };
        }
        if (type.IsEnum)
            return BuildEnumSchema(type, declaringProperty);

        if (TryGetDictionaryValueType(type, out Type dictionaryValueType))
        {
            NullabilityInfo valueNullability = RequireGenericNullability(
                nullability,
                1,
                declaringProperty,
                "dictionary value"
            );
            return new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = BuildValueSchema(
                    dictionaryValueType,
                    valueNullability,
                    declaringProperty
                ),
            };
        }
        if (TryGetArrayElementType(type, out Type elementType))
        {
            NullabilityInfo elementNullability = type.IsArray
                ? nullability.ElementType
                : RequireGenericNullability(
                    nullability,
                    0,
                    declaringProperty,
                    "array element"
                );
            if (elementNullability == null)
            {
                throw new InvalidOperationException(
                    $"Array property '{declaringProperty?.DeclaringType?.Name}."
                        + $"{declaringProperty?.Name}' has no element nullability metadata."
                );
            }
            return new JsonObject
            {
                ["type"] = "array",
                ["items"] = BuildValueSchema(elementType, elementNullability, declaringProperty),
            };
        }

        if (!type.IsClass)
        {
            throw new InvalidOperationException(
                $"JSON Schema exporter does not support value type '{DisplayType(type)}'. "
                    + "Declare a stable business-string provider or use a supported primitive."
            );
        }
        return BuildObjectReference(type);
    }

    private JsonObject BuildStableStringSchema(
        ContentJsonSchemaStableStringValuesAttribute attribute,
        PropertyInfo property,
        Type valueType
    )
    {
        if (valueType != typeof(string) && !valueType.IsEnum)
        {
            throw new InvalidOperationException(
                $"Stable business-string metadata on '{property?.DeclaringType?.Name}."
                    + $"{property?.Name}' must target string or enum, not '{DisplayType(valueType)}'."
            );
        }

        IContentJsonSchemaStableStringValues provider =
            CreateProvider<IContentJsonSchemaStableStringValues>(
                attribute.ProviderType,
                $"stable business string '{property?.DeclaringType?.Name}.{property?.Name}'"
            );
        IReadOnlyList<string> values = ValidateStringValues(
            provider.Values,
            $"stable business string '{property?.DeclaringType?.Name}.{property?.Name}'"
        );
        return new JsonObject
        {
            ["type"] = "string",
            ["enum"] = ToJsonArray(values),
        };
    }

    private static JsonObject BuildEnumSchema(Type enumType, PropertyInfo property)
    {
        JsonConverterAttribute converter =
            property?.GetCustomAttribute<JsonConverterAttribute>()
            ?? enumType.GetCustomAttribute<JsonConverterAttribute>();
        Type converterType = converter?.ConverterType;
        bool exactStringEnumConverter =
            converterType != null
            && converterType.IsGenericType
            && converterType.GetGenericTypeDefinition() == typeof(JsonStringEnumConverter<>)
            && converterType.GetGenericArguments()[0] == enumType;
        if (!exactStringEnumConverter)
        {
            throw new InvalidOperationException(
                $"Enum property '{property?.DeclaringType?.Name}.{property?.Name}' must declare "
                    + "JsonStringEnumConverter<TEnum> or stable business-string metadata so the "
                    + "exporter never guesses wire values."
            );
        }

        return new JsonObject
        {
            ["type"] = "string",
            ["enum"] = ToJsonArray(Enum.GetNames(enumType)),
        };
    }

    private void ValidateClosedKindCarrierProperties(
        Type ownerType,
        SerializableProperty discriminator,
        SerializableProperty payload
    )
    {
        NullabilityInfo discriminatorNullability = _nullability.Create(discriminator.Property);
        if (
            discriminator.Property.PropertyType != typeof(string)
            || discriminatorNullability.ReadState != NullabilityState.NotNull
        )
        {
            throw new InvalidOperationException(
                $"Closed-kind DTO '{DisplayType(ownerType)}' discriminator property "
                    + $"'{discriminator.JsonName}' must be a non-nullable string carrier."
            );
        }

        NullabilityInfo payloadNullability = _nullability.Create(payload.Property);
        if (
            payload.Property.PropertyType != typeof(object)
            || payloadNullability.ReadState != NullabilityState.NotNull
        )
        {
            throw new InvalidOperationException(
                $"Closed-kind DTO '{DisplayType(ownerType)}' payload property "
                    + $"'{payload.JsonName}' must be a non-nullable object carrier for delayed "
                    + "kind-specific DTO parsing."
            );
        }
    }

    private static IReadOnlyList<SerializableProperty> GetSerializableProperties(Type type)
    {
        var properties = new List<SerializableProperty>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (
            PropertyInfo property in type.GetProperties(
                BindingFlags.Public | BindingFlags.Instance
            )
        )
        {
            if (property.GetMethod == null || property.GetMethod.IsStatic)
                continue;
            if (property.GetIndexParameters().Length > 0)
                continue;
            JsonIgnoreAttribute ignore = property.GetCustomAttribute<JsonIgnoreAttribute>();
            if (ignore?.Condition == JsonIgnoreCondition.Always)
                continue;
            if (property.GetCustomAttribute<JsonExtensionDataAttribute>() != null)
            {
                throw new InvalidOperationException(
                    $"JSON Schema DTO '{DisplayType(type)}' cannot declare JsonExtensionData."
                );
            }

            JsonPropertyNameAttribute name =
                property.GetCustomAttribute<JsonPropertyNameAttribute>();
            if (name == null || string.IsNullOrWhiteSpace(name.Name))
            {
                throw new InvalidOperationException(
                    $"JSON Schema DTO property '{DisplayType(type)}.{property.Name}' must declare "
                        + "an explicit JsonPropertyName."
                );
            }
            if (!names.Add(name.Name))
            {
                throw new InvalidOperationException(
                    $"JSON Schema DTO '{DisplayType(type)}' declares duplicate JSON property "
                        + $"name '{name.Name}'."
                );
            }

            bool required =
                property.GetCustomAttribute<JsonRequiredAttribute>() != null
                || property.GetCustomAttribute<RequiredMemberAttribute>() != null;
            properties.Add(
                new SerializableProperty(
                    property,
                    name.Name,
                    required,
                    property.GetCustomAttribute<DescriptionAttribute>()?.Description
                )
            );
        }

        properties.Sort((left, right) =>
            StringComparer.Ordinal.Compare(left.JsonName, right.JsonName)
        );
        return properties.AsReadOnly();
    }

    private static SerializableProperty RequireProperty(
        Type ownerType,
        IReadOnlyList<SerializableProperty> properties,
        string propertyName,
        string role
    )
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            throw new InvalidOperationException(
                $"Closed-kind DTO '{DisplayType(ownerType)}' {role} property name is required."
            );
        }
        SerializableProperty match = properties.SingleOrDefault(property =>
            property.JsonName == propertyName
        );
        return match
            ?? throw new InvalidOperationException(
                $"Closed-kind DTO '{DisplayType(ownerType)}' has no reflected {role} property "
                    + $"named '{propertyName}'."
            );
    }

    private static IReadOnlyList<ContentJsonSchemaClosedKindBranch> ValidateBranches(
        Type ownerType,
        IContentJsonSchemaClosedKindSpec spec
    )
    {
        if (spec.Branches == null || spec.Branches.Count == 0)
        {
            throw new InvalidOperationException(
                $"Closed-kind DTO '{DisplayType(ownerType)}' must declare at least one branch."
            );
        }

        var kinds = new HashSet<string>(StringComparer.Ordinal);
        var branches = new List<ContentJsonSchemaClosedKindBranch>(spec.Branches.Count);
        foreach (ContentJsonSchemaClosedKindBranch branch in spec.Branches)
        {
            if (branch == null || string.IsNullOrWhiteSpace(branch.Kind))
            {
                throw new InvalidOperationException(
                    $"Closed-kind DTO '{DisplayType(ownerType)}' contains an empty branch kind."
                );
            }
            if (!kinds.Add(branch.Kind))
            {
                throw new InvalidOperationException(
                    $"Closed-kind DTO '{DisplayType(ownerType)}' duplicates kind '{branch.Kind}'."
                );
            }
            if (branch.PayloadDtoType == null)
            {
                throw new InvalidOperationException(
                    $"Closed-kind DTO '{DisplayType(ownerType)}' kind '{branch.Kind}' has no "
                        + "payload DTO type."
                );
            }
            branches.Add(branch);
        }

        branches.Sort((left, right) => StringComparer.Ordinal.Compare(left.Kind, right.Kind));
        return branches.AsReadOnly();
    }

    private static bool AllowsUnmappedMembers(Type type)
    {
        JsonUnmappedMemberHandlingAttribute attribute =
            type.GetCustomAttribute<JsonUnmappedMemberHandlingAttribute>();
        return attribute?.UnmappedMemberHandling != JsonUnmappedMemberHandling.Disallow;
    }

    private static NullabilityInfo RequireGenericNullability(
        NullabilityInfo nullability,
        int index,
        PropertyInfo property,
        string role
    )
    {
        if (nullability.GenericTypeArguments.Length <= index)
        {
            throw new InvalidOperationException(
                $"Collection property '{property?.DeclaringType?.Name}.{property?.Name}' has no "
                    + $"{role} nullability metadata."
            );
        }
        return nullability.GenericTypeArguments[index];
    }

    private static bool TryGetArrayElementType(Type type, out Type elementType)
    {
        if (type.IsArray)
        {
            elementType = type.GetElementType();
            return elementType != null;
        }

        Type listInterface = FindGenericInterface(type, typeof(IReadOnlyList<>))
            ?? FindGenericInterface(type, typeof(IList<>));
        if (listInterface != null)
        {
            elementType = listInterface.GetGenericArguments()[0];
            return true;
        }

        elementType = null;
        return false;
    }

    private static bool TryGetDictionaryValueType(Type type, out Type valueType)
    {
        Type dictionaryInterface = FindGenericInterface(type, typeof(IReadOnlyDictionary<,>))
            ?? FindGenericInterface(type, typeof(IDictionary<,>));
        if (dictionaryInterface == null)
        {
            valueType = null;
            return false;
        }

        Type[] arguments = dictionaryInterface.GetGenericArguments();
        if (arguments[0] != typeof(string))
        {
            throw new InvalidOperationException(
                $"JSON Schema dictionary '{DisplayType(type)}' must use string keys."
            );
        }
        valueType = arguments[1];
        return true;
    }

    private static Type FindGenericInterface(Type type, Type openGenericType)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == openGenericType)
            return type;
        return type.GetInterfaces()
            .FirstOrDefault(candidate =>
                candidate.IsGenericType
                && candidate.GetGenericTypeDefinition() == openGenericType
            );
    }

    private static JsonObject WrapNullable(JsonObject valueSchema)
    {
        return new JsonObject
        {
            ["anyOf"] = new JsonArray(
                valueSchema,
                new JsonObject { ["type"] = "null" }
            ),
        };
    }

    private static JsonArray ToJsonArray(IEnumerable<string> values)
    {
        var array = new JsonArray();
        foreach (string value in values)
            array.Add(value);
        return array;
    }

    private static IReadOnlyList<string> ValidateStringValues(
        IReadOnlyList<string> values,
        string ownerLabel
    )
    {
        if (values == null || values.Count == 0)
            throw new InvalidOperationException($"{ownerLabel} must declare at least one value.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var copy = new List<string>(values.Count);
        foreach (string value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException($"{ownerLabel} contains an empty value.");
            if (!seen.Add(value))
                throw new InvalidOperationException($"{ownerLabel} duplicates value '{value}'.");
            copy.Add(value);
        }
        return copy.AsReadOnly();
    }

    private static TProvider CreateProvider<TProvider>(Type providerType, string ownerLabel)
        where TProvider : class
    {
        if (!typeof(TProvider).IsAssignableFrom(providerType))
        {
            throw new InvalidOperationException(
                $"Provider '{DisplayType(providerType)}' for {ownerLabel} must implement "
                    + $"'{typeof(TProvider).Name}'."
            );
        }
        return Activator.CreateInstance(providerType, nonPublic: true) as TProvider
            ?? throw new InvalidOperationException(
                $"Provider '{DisplayType(providerType)}' for {ownerLabel} could not be created."
            );
    }

    private void ReserveDefinitionKey(string key, string owner)
    {
        if (_definitionOwners.TryGetValue(key, out string existingOwner))
        {
            throw new InvalidOperationException(
                $"JSON Schema definition key '{key}' collides between {existingOwner} and {owner}."
            );
        }
        _definitionOwners.Add(key, owner);
    }

    private static void AddDescription(JsonObject schema, string description)
    {
        if (!string.IsNullOrWhiteSpace(description))
            schema["description"] = description;
    }

    private static string SanitizeDefinitionKey(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (char character in value)
        {
            builder.Append(char.IsLetterOrDigit(character) || character == '_'
                ? character
                : '_');
        }
        string key = builder.ToString();
        return string.IsNullOrWhiteSpace(key)
            ? throw new InvalidOperationException("JSON Schema definition key is empty.")
            : key;
    }

    private static string EscapeJsonPointer(string value) =>
        value.Replace("~", "~0", StringComparison.Ordinal)
            .Replace("/", "~1", StringComparison.Ordinal);

    private static string DisplayType(Type type) => type?.FullName ?? "<null>";

    private sealed record SerializableProperty(
        PropertyInfo Property,
        string JsonName,
        bool IsRequired,
        string Description
    );
}
