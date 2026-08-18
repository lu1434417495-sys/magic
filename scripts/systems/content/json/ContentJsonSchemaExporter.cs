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
    internal const string NonBlankStringPattern = @"\S";
    private readonly NullabilityInfoContext _nullability = new();
    private readonly SortedDictionary<string, JsonObject> _definitions =
        new(StringComparer.Ordinal);
    private readonly Dictionary<Type, string> _definitionKeys = new();
    private readonly Dictionary<
        (Type Entry, Type Control, string EntryId, string TemplateControl),
        string
    > _entryControlDefinitionKeys = new();
    private readonly Dictionary<Type, string> _partialDefinitionKeys = new();
    private readonly Dictionary<(Type Entry, Type Control), string>
        _partialControlDefinitionKeys = new();
    private readonly Dictionary<string, string> _definitionOwners =
        new(StringComparer.Ordinal);

    internal string Export(ContentJsonSchemaDomainRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        _definitions.Clear();
        _definitionKeys.Clear();
        _entryControlDefinitionKeys.Clear();
        _partialDefinitionKeys.Clear();
        _partialControlDefinitionKeys.Clear();
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

    private JsonObject BuildPartialClosedKindDefinition(
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
        var anyOf = new JsonArray();
        foreach (ContentJsonSchemaClosedKindBranch branch in branches)
        {
            string branchKey =
                $"{definitionKey}__{SanitizeDefinitionKey(branch.Kind)}";
            ReserveDefinitionKey(
                branchKey,
                $"partial closed-kind branch '{DisplayType(type)}:{branch.Kind}'"
            );
            _definitions[branchKey] = BuildPartialClosedKindBranchDefinition(
                type,
                properties,
                discriminator.JsonName,
                payload.JsonName,
                branch
            );
            anyOf.Add(
                new JsonObject
                {
                    ["$ref"] = $"#/$defs/{EscapeJsonPointer(branchKey)}",
                }
            );
        }

        var schema = new JsonObject();
        AddDescription(schema, type.GetCustomAttribute<DescriptionAttribute>()?.Description);
        schema["anyOf"] = anyOf;
        return schema;
    }

    private JsonObject BuildPartialClosedKindBranchDefinition(
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
                string payloadKey = EnsurePartialDefinition(branch.PayloadDtoType);
                propertySchemas[property.JsonName] = new JsonObject
                {
                    ["$ref"] = $"#/$defs/{EscapeJsonPointer(payloadKey)}",
                };
            }
            else
            {
                propertySchemas[property.JsonName] = BuildPartialPropertySchema(property);
            }
        }
        schema["properties"] = propertySchemas;
        return schema;
    }

    private JsonObject BuildPropertySchema(SerializableProperty property)
    {
        NullabilityInfo nullability = _nullability.Create(property.Property);
        ContentJsonSchemaEntryControlMembersAttribute entryControl =
            property.Property.GetCustomAttribute<ContentJsonSchemaEntryControlMembersAttribute>();
        ContentJsonSchemaPartialObjectValuesAttribute partialObjectValues =
            property.Property.GetCustomAttribute<ContentJsonSchemaPartialObjectValuesAttribute>();
        ContentJsonSchemaScalarOrStringArrayDictionaryValuesAttribute scalarOrStringArrayValues =
            property.Property.GetCustomAttribute<ContentJsonSchemaScalarOrStringArrayDictionaryValuesAttribute>();
        ContentJsonSchemaDisallowExplicitNullAttribute disallowExplicitNull =
            property.Property.GetCustomAttribute<ContentJsonSchemaDisallowExplicitNullAttribute>();
        int schemaShapeAttributeCount = (entryControl != null ? 1 : 0)
            + (partialObjectValues != null ? 1 : 0)
            + (scalarOrStringArrayValues != null ? 1 : 0)
            + (disallowExplicitNull != null ? 1 : 0);
        if (schemaShapeAttributeCount > 1)
        {
            throw new InvalidOperationException(
                $"JSON Schema property '{property.Property.DeclaringType?.Name}."
                    + $"{property.Property.Name}' cannot combine multiple schema-only shape "
                    + "metadata attributes."
            );
        }

        JsonObject schema = entryControl != null
            ? BuildEntryControlCollectionSchema(property.Property, nullability, entryControl)
            : partialObjectValues != null
                ? BuildPartialObjectValuesSchema(
                    property.Property,
                    nullability,
                    partialObjectValues
                )
            : scalarOrStringArrayValues != null
                ? BuildScalarOrStringArrayDictionarySchema(
                    property.Property,
                    nullability
                )
            : disallowExplicitNull != null
                ? BuildExplicitNonNullPropertySchema(property.Property, nullability)
                : BuildValueSchema(
                    property.Property.PropertyType,
                    nullability,
                    property.Property
                );
        AddDescription(schema, property.Description);
        ApplyPropertyConst(schema, property.Property);
        ApplyPropertyStringConstraints(schema, property.Property);
        return schema;
    }

    private JsonObject BuildPartialObjectValuesSchema(
        PropertyInfo property,
        NullabilityInfo nullability,
        ContentJsonSchemaPartialObjectValuesAttribute attribute
    )
    {
        if (!TryGetDictionaryValueType(property.PropertyType, out Type valueType))
        {
            throw new InvalidOperationException(
                $"Partial-object-values schema metadata on '{property.DeclaringType?.Name}."
                    + $"{property.Name}' requires a string-keyed dictionary property."
            );
        }
        NullabilityInfo valueNullability = RequireGenericNullability(
            nullability,
            1,
            property,
            "dictionary value"
        );
        if (
            valueType.IsValueType
            || valueType == typeof(string)
            || valueNullability.ReadState != NullabilityState.NotNull
        )
        {
            throw new InvalidOperationException(
                $"Partial-object-values schema metadata on '{property.DeclaringType?.Name}."
                    + $"{property.Name}' requires non-null class DTO dictionary values."
            );
        }

        string partialDefinitionKey = attribute.RootControlDtoType == null
            ? EnsurePartialDefinition(valueType)
            : EnsurePartialControlDefinition(
                valueType,
                attribute.RootControlDtoType
            );
        var dictionarySchema = new JsonObject
        {
            ["type"] = "object",
            ["additionalProperties"] = new JsonObject
            {
                ["$ref"] = $"#/$defs/{EscapeJsonPointer(partialDefinitionKey)}",
            },
        };

        if (property.PropertyType.IsValueType)
            return dictionarySchema;
        if (nullability.ReadState == NullabilityState.Unknown)
        {
            throw new InvalidOperationException(
                $"Reference property '{property.DeclaringType?.Name}.{property.Name}' has unknown "
                    + "nullable metadata. Enable nullable annotations for the DTO file and declare "
                    + "the property as nullable or non-nullable explicitly."
            );
        }
        return nullability.ReadState == NullabilityState.Nullable
            ? WrapNullable(dictionarySchema)
            : dictionarySchema;
    }

    private static JsonObject BuildScalarOrStringArrayDictionarySchema(
        PropertyInfo property,
        NullabilityInfo nullability
    )
    {
        if (
            !TryGetDictionaryValueType(property.PropertyType, out Type valueType)
            || valueType != typeof(JsonElement)
        )
        {
            throw new InvalidOperationException(
                $"Scalar-or-string-array dictionary schema metadata on "
                    + $"'{property.DeclaringType?.Name}.{property.Name}' requires a "
                    + "string-keyed dictionary whose value carrier is JsonElement."
            );
        }
        if (property.PropertyType.IsValueType)
        {
            throw new InvalidOperationException(
                $"Scalar-or-string-array dictionary schema metadata on "
                    + $"'{property.DeclaringType?.Name}.{property.Name}' requires a "
                    + "dictionary reference type."
            );
        }
        if (nullability.ReadState == NullabilityState.Unknown)
        {
            throw new InvalidOperationException(
                $"Reference property '{property.DeclaringType?.Name}.{property.Name}' has "
                    + "unknown nullable metadata. Enable nullable annotations for the DTO file "
                    + "and declare the property as nullable or non-nullable explicitly."
            );
        }

        JsonObject dictionarySchema = new()
        {
            ["type"] = "object",
            ["additionalProperties"] = new JsonObject
            {
                ["anyOf"] = new JsonArray(
                    new JsonObject { ["type"] = "boolean" },
                    new JsonObject
                    {
                        ["type"] = "integer",
                        ["minimum"] = long.MinValue,
                        ["maximum"] = long.MaxValue,
                    },
                    new JsonObject
                    {
                        ["type"] = "number",
                        ["minimum"] = -double.MaxValue,
                        ["maximum"] = double.MaxValue,
                    },
                    new JsonObject { ["type"] = "string" },
                    new JsonObject
                    {
                        ["type"] = "array",
                        ["items"] = new JsonObject { ["type"] = "string" },
                    }
                ),
            },
        };
        return nullability.ReadState == NullabilityState.Nullable
            ? WrapNullable(dictionarySchema)
            : dictionarySchema;
    }

    private string EnsurePartialDefinition(Type type)
    {
        if (_partialDefinitionKeys.TryGetValue(type, out string existingKey))
            return existingKey;
        if (!type.IsClass || type == typeof(string) || type.IsGenericTypeDefinition)
        {
            throw new InvalidOperationException(
                $"Partial JSON Schema DTO '{DisplayType(type)}' must be a closed class type."
            );
        }
        string key = SanitizeDefinitionKey($"{type.Name}__partial");
        ReserveDefinitionKey(key, $"partial DTO '{DisplayType(type)}'");
        _partialDefinitionKeys.Add(type, key);
        _definitions[key] = new JsonObject();
        ContentJsonSchemaClosedKindAttribute closedKind =
            type.GetCustomAttribute<ContentJsonSchemaClosedKindAttribute>();
        _definitions[key] = closedKind == null
            ? BuildPartialObjectDefinition(type)
            : BuildPartialClosedKindDefinition(type, closedKind, key);
        return key;
    }

    private JsonObject BuildPartialObjectDefinition(Type type)
    {
        IReadOnlyList<SerializableProperty> properties = GetSerializableProperties(type);
        var schema = new JsonObject();
        AddDescription(schema, type.GetCustomAttribute<DescriptionAttribute>()?.Description);
        schema["type"] = "object";
        schema["additionalProperties"] = AllowsUnmappedMembers(type);

        var propertySchemas = new JsonObject();
        foreach (SerializableProperty property in properties)
            propertySchemas[property.JsonName] = BuildPartialPropertySchema(property);
        schema["properties"] = propertySchemas;
        return schema;
    }

    private JsonObject BuildPartialPropertySchema(SerializableProperty property)
    {
        ContentJsonSchemaEntryControlMembersAttribute entryControl =
            property.Property.GetCustomAttribute<ContentJsonSchemaEntryControlMembersAttribute>();
        ContentJsonSchemaPartialObjectValuesAttribute partialObjectValues =
            property.Property.GetCustomAttribute<ContentJsonSchemaPartialObjectValuesAttribute>();
        ContentJsonSchemaScalarOrStringArrayDictionaryValuesAttribute scalarOrStringArrayValues =
            property.Property.GetCustomAttribute<ContentJsonSchemaScalarOrStringArrayDictionaryValuesAttribute>();
        ContentJsonSchemaDisallowExplicitNullAttribute disallowExplicitNullAttribute =
            property.Property.GetCustomAttribute<ContentJsonSchemaDisallowExplicitNullAttribute>();
        int schemaShapeAttributeCount = (entryControl != null ? 1 : 0)
            + (partialObjectValues != null ? 1 : 0)
            + (scalarOrStringArrayValues != null ? 1 : 0)
            + (disallowExplicitNullAttribute != null ? 1 : 0);
        if (schemaShapeAttributeCount > 1)
        {
            throw new InvalidOperationException(
                $"JSON Schema property '{property.Property.DeclaringType?.Name}."
                    + $"{property.Property.Name}' cannot combine multiple schema-only shape "
                    + "metadata attributes."
            );
        }
        if (entryControl != null || partialObjectValues != null)
        {
            throw new InvalidOperationException(
                $"Recursive partial DTO '{property.Property.DeclaringType?.Name}."
                    + $"{property.Property.Name}' cannot contain document-level schema metadata."
            );
        }

        NullabilityInfo nullability = _nullability.Create(property.Property);
        if (scalarOrStringArrayValues != null)
        {
            JsonObject dictionarySchema = BuildScalarOrStringArrayDictionarySchema(
                property.Property,
                nullability
            );
            AddDescription(dictionarySchema, property.Description);
            ApplyPropertyConst(dictionarySchema, property.Property);
            ApplyPropertyStringConstraints(dictionarySchema, property.Property);
            return dictionarySchema;
        }
        bool disallowExplicitNull = disallowExplicitNullAttribute != null;
        JsonObject schema = BuildPartialValueSchema(
            property.Property.PropertyType,
            nullability,
            property.Property,
            disallowExplicitNull
        );
        AddDescription(schema, property.Description);
        ApplyPropertyConst(schema, property.Property);
        ApplyPropertyStringConstraints(schema, property.Property);
        return schema;
    }

    private void ApplyPropertyStringConstraints(JsonObject schema, PropertyInfo property)
    {
        if (property.GetCustomAttribute<ContentJsonSchemaNonBlankStringAttribute>() == null)
            return;

        NullabilityInfo nullability = _nullability.Create(property);
        if (
            property.PropertyType != typeof(string)
            || nullability.ReadState != NullabilityState.NotNull
        )
        {
            throw new InvalidOperationException(
                $"ContentJsonSchemaNonBlankString on '{property.DeclaringType?.Name}."
                    + $"{property.Name}' requires a non-nullable string carrier."
            );
        }
        schema["pattern"] = NonBlankStringPattern;
    }

    private static void ApplyPropertyConst(JsonObject schema, PropertyInfo property)
    {
        ContentJsonSchemaConstAttribute constant =
            property.GetCustomAttribute<ContentJsonSchemaConstAttribute>();
        if (constant == null)
            return;

        schema["const"] = constant.Value switch
        {
            string text => JsonValue.Create(text),
            int number => JsonValue.Create(number),
            long number => JsonValue.Create(number),
            bool flag => JsonValue.Create(flag),
            _ => throw new InvalidOperationException(
                $"JSON Schema const on '{property.DeclaringType?.Name}.{property.Name}' has "
                    + $"unsupported CLR type '{constant.Value.GetType().Name}'."
            ),
        };
    }

    private JsonObject BuildPartialValueSchema(
        Type type,
        NullabilityInfo nullability,
        PropertyInfo declaringProperty,
        bool disallowExplicitNull
    )
    {
        Type nullableValueType = Nullable.GetUnderlyingType(type);
        if (nullableValueType != null)
        {
            JsonObject valueSchema = BuildPartialNonNullableValueSchema(
                nullableValueType,
                nullability,
                declaringProperty
            );
            return disallowExplicitNull ? valueSchema : WrapNullable(valueSchema);
        }

        if (!type.IsValueType)
        {
            if (nullability.ReadState == NullabilityState.Unknown)
            {
                throw new InvalidOperationException(
                    $"Reference property '{declaringProperty?.DeclaringType?.Name}."
                        + $"{declaringProperty?.Name}' has unknown nullable metadata. Enable "
                        + "nullable annotations for the DTO file and declare the property as "
                        + "nullable or non-nullable explicitly."
                );
            }
            if (disallowExplicitNull && nullability.ReadState != NullabilityState.Nullable)
            {
                throw new InvalidOperationException(
                    $"ContentJsonSchemaDisallowExplicitNull on "
                        + $"'{declaringProperty?.DeclaringType?.Name}."
                        + $"{declaringProperty?.Name}' requires a nullable CLR carrier whose "
                        + "missing value remains optional in JSON."
                );
            }

            JsonObject valueSchema = BuildPartialNonNullableValueSchema(
                type,
                nullability,
                declaringProperty
            );
            return nullability.ReadState == NullabilityState.Nullable
                && !disallowExplicitNull
                ? WrapNullable(valueSchema)
                : valueSchema;
        }

        if (disallowExplicitNull)
        {
            throw new InvalidOperationException(
                $"ContentJsonSchemaDisallowExplicitNull on "
                    + $"'{declaringProperty?.DeclaringType?.Name}."
                    + $"{declaringProperty?.Name}' requires a nullable CLR carrier whose missing "
                    + "value remains optional in JSON."
            );
        }
        return BuildPartialNonNullableValueSchema(type, nullability, declaringProperty);
    }

    private JsonObject BuildPartialNonNullableValueSchema(
        Type type,
        NullabilityInfo nullability,
        PropertyInfo declaringProperty
    )
    {
        ContentJsonSchemaStableStringValuesAttribute stableStrings =
            declaringProperty?.GetCustomAttribute<ContentJsonSchemaStableStringValuesAttribute>()
            ?? type.GetCustomAttribute<ContentJsonSchemaStableStringValuesAttribute>();
        if (stableStrings != null)
            return BuildStableStringSchema(stableStrings, declaringProperty, type, nullability);

        if (
            type == typeof(string)
            || type == typeof(char)
            || type == typeof(bool)
            || type.IsPrimitive
            || type == typeof(decimal)
            || type.IsEnum
        )
        {
            return BuildNonNullableValueSchema(type, nullability, declaringProperty);
        }

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
                ["additionalProperties"] = BuildPartialValueSchema(
                    dictionaryValueType,
                    valueNullability,
                    declaringProperty,
                    disallowExplicitNull: false
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
                $"JSON Schema exporter does not support partial value type "
                    + $"'{DisplayType(type)}'."
            );
        }
        string definitionKey = EnsurePartialDefinition(type);
        return new JsonObject
        {
            ["$ref"] = $"#/$defs/{EscapeJsonPointer(definitionKey)}",
        };
    }

    private JsonObject BuildExplicitNonNullPropertySchema(
        PropertyInfo property,
        NullabilityInfo nullability
    )
    {
        Type propertyType = property.PropertyType;
        Type nullableValueType = Nullable.GetUnderlyingType(propertyType);
        if (nullableValueType != null)
            return BuildNonNullableValueSchema(nullableValueType, nullability, property);

        if (!propertyType.IsValueType && nullability.ReadState == NullabilityState.Nullable)
            return BuildNonNullableValueSchema(propertyType, nullability, property);

        throw new InvalidOperationException(
            $"ContentJsonSchemaDisallowExplicitNull on '{property.DeclaringType?.Name}."
                + $"{property.Name}' requires a nullable CLR carrier whose missing value remains "
                + "optional in JSON."
        );
    }

    private JsonObject BuildEntryControlCollectionSchema(
        PropertyInfo property,
        NullabilityInfo nullability,
        ContentJsonSchemaEntryControlMembersAttribute attribute
    )
    {
        if (!TryGetArrayElementType(property.PropertyType, out Type entryType))
        {
            throw new InvalidOperationException(
                $"Entry-control schema metadata on '{property.DeclaringType?.Name}."
                    + $"{property.Name}' requires an array or list property."
            );
        }

        NullabilityInfo elementNullability = property.PropertyType.IsArray
            ? nullability.ElementType
            : RequireGenericNullability(nullability, 0, property, "array element");
        if (
            elementNullability == null
            || elementNullability.ReadState != NullabilityState.NotNull
        )
        {
            throw new InvalidOperationException(
                $"Entry-control schema metadata on '{property.DeclaringType?.Name}."
                    + $"{property.Name}' requires non-null entry elements."
            );
        }

        string definitionKey = EnsureEntryControlDefinition(entryType, attribute);
        var arraySchema = new JsonObject
        {
            ["type"] = "array",
            ["items"] = new JsonObject
            {
                ["$ref"] = $"#/$defs/{EscapeJsonPointer(definitionKey)}",
            },
        };

        if (property.PropertyType.IsValueType)
            return arraySchema;
        if (nullability.ReadState == NullabilityState.Unknown)
        {
            throw new InvalidOperationException(
                $"Reference property '{property.DeclaringType?.Name}.{property.Name}' has unknown "
                    + "nullable metadata. Enable nullable annotations for the DTO file and declare "
                    + "the property as nullable or non-nullable explicitly."
            );
        }
        return nullability.ReadState == NullabilityState.Nullable
            ? WrapNullable(arraySchema)
            : arraySchema;
    }

    private string EnsureEntryControlDefinition(
        Type entryType,
        ContentJsonSchemaEntryControlMembersAttribute attribute
    )
    {
        Type controlType = attribute.ControlDtoType;
        var identity = (
            Entry: entryType,
            Control: controlType,
            EntryId: attribute.EntryIdPropertyName,
            TemplateControl: attribute.TemplateControlPropertyName
        );
        if (_entryControlDefinitionKeys.TryGetValue(identity, out string existingKey))
            return existingKey;
        if (
            !entryType.IsClass
            || entryType == typeof(string)
            || !controlType.IsClass
            || controlType == typeof(string)
        )
        {
            throw new InvalidOperationException(
                "Entry-control schema composition requires closed class DTO types."
            );
        }
        if (AllowsUnmappedMembers(entryType) || AllowsUnmappedMembers(controlType))
        {
            throw new InvalidOperationException(
                $"Entry-control schema composition for '{DisplayType(entryType)}' and "
                    + $"'{DisplayType(controlType)}' requires both DTOs to disallow unmapped members."
            );
        }

        IReadOnlyList<SerializableProperty> entryProperties = GetSerializableProperties(entryType);
        IReadOnlyList<SerializableProperty> controlProperties =
            GetSerializableProperties(controlType);
        if (
            string.Equals(
                attribute.EntryIdPropertyName,
                attribute.TemplateControlPropertyName,
                StringComparison.Ordinal
            )
        )
        {
            throw new InvalidOperationException(
                "Entry-control schema entry ID and template control property names must differ."
            );
        }
        SerializableProperty entryId = entryProperties.SingleOrDefault(property =>
            property.JsonName == attribute.EntryIdPropertyName
        )
            ?? throw new InvalidOperationException(
                $"Entry-control schema composition for '{DisplayType(entryType)}' has no entry ID "
                    + $"property named '{attribute.EntryIdPropertyName}'."
            );
        SerializableProperty templateControl = controlProperties.SingleOrDefault(property =>
            property.JsonName == attribute.TemplateControlPropertyName
        )
            ?? throw new InvalidOperationException(
                $"Entry-control schema composition for '{DisplayType(controlType)}' has no "
                    + $"template control property named '{attribute.TemplateControlPropertyName}'."
            );
        ValidateEntryControlStringCarrier(entryType, entryId, "entry ID");
        ValidateEntryControlStringCarrier(controlType, templateControl, "template control");
        if (
            templateControl.Property.GetCustomAttribute<ContentJsonSchemaNonBlankStringAttribute>()
                == null
        )
        {
            throw new InvalidOperationException(
                $"Entry-control schema template control property '{DisplayType(controlType)}."
                    + $"{templateControl.Property.Name}' must declare "
                    + $"{nameof(ContentJsonSchemaNonBlankStringAttribute)}."
            );
        }

        string metadataKey = SanitizeDefinitionKey(
            $"{attribute.EntryIdPropertyName}_{attribute.TemplateControlPropertyName}"
        );
        string key = SanitizeDefinitionKey(
            $"{entryType.Name}__authoring_{metadataKey}_with_{controlType.Name}"
        );
        ReserveDefinitionKey(
            key,
            $"entry-control composition '{DisplayType(entryType)}+{DisplayType(controlType)}' "
                + $"using '{attribute.EntryIdPropertyName}'/'{attribute.TemplateControlPropertyName}'"
        );
        _entryControlDefinitionKeys.Add(identity, key);
        _definitions[key] = new JsonObject();
        _definitions[key] = BuildEntryControlDefinition(entryType, controlType, attribute);
        return key;
    }

    private void ValidateEntryControlStringCarrier(
        Type ownerType,
        SerializableProperty property,
        string role
    )
    {
        NullabilityInfo nullability = _nullability.Create(property.Property);
        if (
            property.Property.PropertyType != typeof(string)
            || nullability.ReadState != NullabilityState.NotNull
        )
        {
            throw new InvalidOperationException(
                $"Entry-control schema {role} property '{DisplayType(ownerType)}."
                    + $"{property.Property.Name}' must be a non-nullable string carrier."
            );
        }
    }

    private string EnsurePartialControlDefinition(Type entryType, Type controlType)
    {
        var pair = (Entry: entryType, Control: controlType);
        if (_partialControlDefinitionKeys.TryGetValue(pair, out string existingKey))
            return existingKey;
        if (
            !entryType.IsClass
            || entryType == typeof(string)
            || !controlType.IsClass
            || controlType == typeof(string)
        )
        {
            throw new InvalidOperationException(
                "Partial-control schema composition requires closed class DTO types."
            );
        }
        if (entryType.GetCustomAttribute<ContentJsonSchemaClosedKindAttribute>() != null)
        {
            throw new InvalidOperationException(
                $"Partial-control schema composition does not support a closed-kind root DTO "
                    + $"'{DisplayType(entryType)}'."
            );
        }
        if (AllowsUnmappedMembers(entryType) || AllowsUnmappedMembers(controlType))
        {
            throw new InvalidOperationException(
                $"Partial-control schema composition for '{DisplayType(entryType)}' and "
                    + $"'{DisplayType(controlType)}' requires both DTOs to disallow unmapped members."
            );
        }

        string key = SanitizeDefinitionKey(
            $"{entryType.Name}__partial_with_{controlType.Name}"
        );
        ReserveDefinitionKey(
            key,
            $"partial-control composition '{DisplayType(entryType)}+{DisplayType(controlType)}'"
        );
        _partialControlDefinitionKeys.Add(pair, key);
        _definitions[key] = new JsonObject();
        _definitions[key] = BuildPartialControlDefinition(entryType, controlType);
        return key;
    }

    private JsonObject BuildPartialControlDefinition(Type entryType, Type controlType)
    {
        IReadOnlyList<SerializableProperty> entryProperties = GetSerializableProperties(entryType);
        IReadOnlyList<SerializableProperty> controlProperties =
            GetSerializableProperties(controlType);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (SerializableProperty property in entryProperties.Concat(controlProperties))
        {
            if (!names.Add(property.JsonName))
            {
                throw new InvalidOperationException(
                    $"Partial-control schema composition for '{DisplayType(entryType)}' and "
                        + $"'{DisplayType(controlType)}' duplicates JSON property "
                        + $"'{property.JsonName}'."
                );
            }
        }

        var schema = new JsonObject();
        AddDescription(
            schema,
            entryType.GetCustomAttribute<DescriptionAttribute>()?.Description
        );
        schema["type"] = "object";
        schema["additionalProperties"] = false;
        var propertySchemas = new JsonObject();
        foreach (SerializableProperty property in entryProperties)
            propertySchemas[property.JsonName] = BuildPartialPropertySchema(property);

        var required = new JsonArray();
        foreach (SerializableProperty property in controlProperties)
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

    private JsonObject BuildEntryControlDefinition(
        Type entryType,
        Type controlType,
        ContentJsonSchemaEntryControlMembersAttribute attribute
    )
    {
        string fullEntryKey = EnsureDefinition(entryType);
        string metadataKey = SanitizeDefinitionKey(
            $"{attribute.EntryIdPropertyName}_{attribute.TemplateControlPropertyName}"
        );
        string templatedEntryKey = SanitizeDefinitionKey(
            $"{entryType.Name}__templated_{metadataKey}_with_{controlType.Name}"
        );
        ReserveDefinitionKey(
            templatedEntryKey,
            $"templated entry composition '{DisplayType(entryType)}+{DisplayType(controlType)}' "
                + $"using '{attribute.EntryIdPropertyName}'/'{attribute.TemplateControlPropertyName}'"
        );
        _definitions[templatedEntryKey] = BuildTemplatedEntryControlDefinition(
            entryType,
            controlType,
            attribute
        );

        var schema = new JsonObject();
        AddDescription(
            schema,
            entryType.GetCustomAttribute<DescriptionAttribute>()?.Description
        );
        schema["oneOf"] = new JsonArray(
            new JsonObject
            {
                ["$ref"] = $"#/$defs/{EscapeJsonPointer(fullEntryKey)}",
            },
            new JsonObject
            {
                ["$ref"] = $"#/$defs/{EscapeJsonPointer(templatedEntryKey)}",
            }
        );
        return schema;
    }

    private JsonObject BuildTemplatedEntryControlDefinition(
        Type entryType,
        Type controlType,
        ContentJsonSchemaEntryControlMembersAttribute attribute
    )
    {
        IReadOnlyList<SerializableProperty> entryProperties = GetSerializableProperties(entryType);
        IReadOnlyList<SerializableProperty> controlProperties =
            GetSerializableProperties(controlType);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (
            SerializableProperty property in entryProperties.Concat(controlProperties)
        )
        {
            if (!names.Add(property.JsonName))
            {
                throw new InvalidOperationException(
                    $"Entry-control schema composition for '{DisplayType(entryType)}' and "
                        + $"'{DisplayType(controlType)}' duplicates JSON property "
                        + $"'{property.JsonName}'."
                );
            }
        }

        var schema = new JsonObject();
        AddDescription(
            schema,
            entryType.GetCustomAttribute<DescriptionAttribute>()?.Description
        );
        schema["type"] = "object";
        schema["additionalProperties"] = false;
        var propertySchemas = new JsonObject();
        foreach (SerializableProperty property in entryProperties)
            propertySchemas[property.JsonName] = BuildPartialPropertySchema(property);
        foreach (SerializableProperty property in controlProperties)
        {
            propertySchemas[property.JsonName] = BuildPropertySchema(property);
        }
        schema["properties"] = propertySchemas;
        schema["required"] = new JsonArray(
            attribute.EntryIdPropertyName,
            attribute.TemplateControlPropertyName
        );
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
            return BuildStableStringSchema(stableStrings, declaringProperty, type, nullability);

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
        Type valueType,
        NullabilityInfo nullability
    )
    {
        bool isCollection = TryGetArrayElementType(valueType, out Type elementType);
        Type stableValueType = isCollection ? elementType : valueType;
        if (stableValueType != typeof(string) && !stableValueType.IsEnum)
        {
            throw new InvalidOperationException(
                $"Stable business-string metadata on '{property?.DeclaringType?.Name}."
                    + $"{property?.Name}' must target string, enum, or a collection of those, "
                    + $"not '{DisplayType(valueType)}'."
            );
        }
        if (isCollection)
        {
            NullabilityInfo elementNullability = valueType.IsArray
                ? nullability.ElementType
                : RequireGenericNullability(nullability, 0, property, "array element");
            if (
                elementNullability == null
                || elementNullability.ReadState != NullabilityState.NotNull
            )
            {
                throw new InvalidOperationException(
                    $"Stable business-string collection '{property?.DeclaringType?.Name}."
                        + $"{property?.Name}' requires non-null elements."
                );
            }
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
        JsonObject scalarSchema = new()
        {
            ["type"] = "string",
            ["enum"] = ToJsonArray(values),
        };
        return isCollection
            ? new JsonObject
            {
                ["type"] = "array",
                ["items"] = scalarSchema,
            }
            : scalarSchema;
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
