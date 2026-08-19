#nullable enable

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Godot;

internal sealed class EquipmentClosureGenerationCatalog
{
    internal const string ProtocolId = "magic.equipment_closure.catalog/v1";

    private EquipmentClosureGenerationCatalog(
        IEnumerable<string> schemaDomains,
        IEnumerable<string> skillIds,
        IEnumerable<string> professionIds,
        IEnumerable<string> itemIds,
        IEnumerable<string> traitIds,
        IEnumerable<string> equipmentAbilityPackIds,
        IEnumerable<string> equipmentAbilityBindingIds,
        IEnumerable<string> gearSetIds,
        IEnumerable<string> recipeIds,
        IEnumerable<string> textureAssetIds
    )
    {
        SchemaDomains = Freeze(schemaDomains);
        SkillIds = Freeze(skillIds);
        ProfessionIds = Freeze(professionIds);
        ItemIds = Freeze(itemIds);
        TraitIds = Freeze(traitIds);
        EquipmentAbilityPackIds = Freeze(equipmentAbilityPackIds);
        EquipmentAbilityBindingIds = Freeze(equipmentAbilityBindingIds);
        GearSetIds = Freeze(gearSetIds);
        RecipeIds = Freeze(recipeIds);
        TextureAssetIds = Freeze(textureAssetIds);
    }

    internal IReadOnlyList<string> SchemaDomains { get; }
    internal IReadOnlyList<string> SkillIds { get; }
    internal IReadOnlyList<string> ProfessionIds { get; }
    internal IReadOnlyList<string> ItemIds { get; }
    internal IReadOnlyList<string> TraitIds { get; }
    internal IReadOnlyList<string> EquipmentAbilityPackIds { get; }
    internal IReadOnlyList<string> EquipmentAbilityBindingIds { get; }
    internal IReadOnlyList<string> GearSetIds { get; }
    internal IReadOnlyList<string> RecipeIds { get; }
    internal IReadOnlyList<string> TextureAssetIds { get; }

    internal static EquipmentClosureGenerationCatalog Build(
        ContentSnapshot snapshot,
        EngineAssetResolver engineAssets
    )
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(engineAssets);
        string[] schemaDomains =
        {
            "items",
            "traits",
            "equipment_abilities",
            "gear_sets",
            "recipes",
        };
        foreach (string domain in schemaDomains)
            _ = ContentJsonSchemaCatalog.Require(domain);

        return new EquipmentClosureGenerationCatalog(
            schemaDomains,
            Keys(snapshot.Skills),
            Keys(snapshot.Professions),
            Keys(snapshot.Items),
            Keys(snapshot.Traits),
            Keys(snapshot.EquipmentAbilityPacks),
            Keys(snapshot.EquipmentAbilityBindings),
            Keys(snapshot.GearSets),
            Keys(snapshot.Recipes),
            engineAssets.GetPublishedContentAssetIds<Texture2D>()
                .Select(value => value.ToString())
        );
    }

    internal string ToJson()
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (
            var writer = new Utf8JsonWriter(
                buffer,
                new JsonWriterOptions
                {
                    Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
                    Indented = true,
                }
            )
        )
        {
            writer.WriteStartObject();
            writer.WriteString("protocol", ProtocolId);
            WriteArray(writer, "schema_domains", SchemaDomains);
            WriteArray(writer, "skill_ids", SkillIds);
            WriteArray(writer, "profession_ids", ProfessionIds);
            WriteArray(writer, "item_ids", ItemIds);
            WriteArray(writer, "trait_ids", TraitIds);
            WriteArray(writer, "equipment_ability_pack_ids", EquipmentAbilityPackIds);
            WriteArray(writer, "equipment_ability_binding_ids", EquipmentAbilityBindingIds);
            WriteArray(writer, "gear_set_ids", GearSetIds);
            WriteArray(writer, "recipe_ids", RecipeIds);
            WriteArray(writer, "texture_asset_ids", TextureAssetIds);
            writer.WriteEndObject();
            writer.Flush();
        }
        return Encoding.UTF8.GetString(buffer.WrittenSpan)
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace("\r", "\n", StringComparison.Ordinal)
                .TrimEnd('\n')
            + "\n";
    }

    private static IEnumerable<string> Keys<T>(
        IReadOnlyDictionary<StringName, T> values
    ) => values.Keys.Select(value => value.ToString());

    private static IReadOnlyList<string> Freeze(IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        string[] ordered = values
            .Select(value => value ?? "")
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Generation catalog IDs must be non-empty.", nameof(values));
        return new ReadOnlyCollection<string>(ordered);
    }

    private static void WriteArray(
        Utf8JsonWriter writer,
        string propertyName,
        IReadOnlyList<string> values
    )
    {
        writer.WritePropertyName(propertyName);
        writer.WriteStartArray();
        foreach (string value in values)
            writer.WriteStringValue(value);
        writer.WriteEndArray();
    }
}
