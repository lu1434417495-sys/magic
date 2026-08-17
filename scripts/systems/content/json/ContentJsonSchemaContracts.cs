using System;
using System.Collections.Generic;

[AttributeUsage(AttributeTargets.Property)]
internal sealed class ContentJsonSchemaConstAttribute : Attribute
{
    internal ContentJsonSchemaConstAttribute(object value)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    internal object Value { get; }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Enum)]
internal sealed class ContentJsonSchemaStableStringValuesAttribute : Attribute
{
    internal ContentJsonSchemaStableStringValuesAttribute(Type providerType)
    {
        ProviderType = providerType ?? throw new ArgumentNullException(nameof(providerType));
    }

    internal Type ProviderType { get; }
}

internal interface IContentJsonSchemaStableStringValues
{
    IReadOnlyList<string> Values { get; }
}

[AttributeUsage(AttributeTargets.Class)]
internal sealed class ContentJsonSchemaClosedKindAttribute : Attribute
{
    internal ContentJsonSchemaClosedKindAttribute(Type specType)
    {
        SpecType = specType ?? throw new ArgumentNullException(nameof(specType));
    }

    internal Type SpecType { get; }
}

internal interface IContentJsonSchemaClosedKindSpec
{
    string DiscriminatorPropertyName { get; }
    string PayloadPropertyName { get; }
    IReadOnlyList<ContentJsonSchemaClosedKindBranch> Branches { get; }
}

internal sealed record ContentJsonSchemaClosedKindBranch(string Kind, Type PayloadDtoType);

internal sealed class ContentJsonSchemaDomainRegistration
{
    internal ContentJsonSchemaDomainRegistration(
        string domainId,
        int schemaVersion,
        Type documentDtoType,
        string title,
        string description,
        string trackedSchemaPath,
        string contentFileMatch
    )
    {
        if (string.IsNullOrWhiteSpace(domainId))
            throw new ArgumentException("Schema domain ID is required.", nameof(domainId));
        if (schemaVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        DocumentDtoType = documentDtoType ?? throw new ArgumentNullException(nameof(documentDtoType));
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Schema title is required.", nameof(title));
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Schema description is required.", nameof(description));
        if (
            string.IsNullOrWhiteSpace(trackedSchemaPath)
            || !trackedSchemaPath.StartsWith("res://", StringComparison.Ordinal)
            || !trackedSchemaPath.EndsWith(".schema.json", StringComparison.Ordinal)
        )
        {
            throw new ArgumentException(
                "Tracked schema path must be a res:// path ending in .schema.json.",
                nameof(trackedSchemaPath)
            );
        }
        if (string.IsNullOrWhiteSpace(contentFileMatch))
            throw new ArgumentException("Content file match is required.", nameof(contentFileMatch));

        DomainId = domainId;
        SchemaVersion = schemaVersion;
        Title = title;
        Description = description;
        TrackedSchemaPath = trackedSchemaPath;
        ContentFileMatch = contentFileMatch;
    }

    internal string DomainId { get; }
    internal int SchemaVersion { get; }
    internal Type DocumentDtoType { get; }
    internal string Title { get; }
    internal string Description { get; }
    internal string TrackedSchemaPath { get; }
    internal string ContentFileMatch { get; }
}
