#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using Godot;

public partial class run_item_json_migration_regression : LifecycleTestSceneTree
{
    private const string TrackedItemsPath = "res://data/configs/json/items/items.json";
    private const string TrackedManifestPath =
        "res://data/configs/json/items/item_icon_migration.csv";
    private const string TrackedSchemaPath = "res://data/schemas/content/items.schema.json";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        try
        {
            string trackedJson = ReadUtf8(TrackedItemsPath);
            ContentImportBatch<ItemImportModel> imported =
                ItemContentJsonAuthoringDomain.CreateImportDescriptor(
                    ItemContentJsonAuthoringDomain.ProductionDirectory,
                    new GodotContentJsonSourceReader()
                ).Import();

            AssertTrackedContent(trackedJson, imported);
            AssertPureContracts();
            AssertImportDiagnostics(imported);
            AssertCanonicalArtifacts(trackedJson, imported);
            AssertProductionRegistry(imported);
        }
        catch (Exception exception)
        {
            _test.Fail($"Unexpected item JSON regression exception: {exception}");
        }

        RequestTestExit(_test.Finish("Item JSON content regression"));
    }

    private void AssertTrackedContent(
        string trackedJson,
        ContentImportBatch<ItemImportModel> imported
    )
    {
        _test.Eq(
            imported.Diagnostics.Count,
            0,
            $"tracked item JSON should be valid. errors={FormatDiagnostics(imported.Diagnostics)}"
        );
        _test.Eq(imported.Entries.Count, 129, "tracked JSON must expose the pinned 129 items");
        _test.False(
            trackedJson.Contains("base_item_id", StringComparison.Ordinal),
            "flat item JSON must not retain base_item_id"
        );
        _test.False(
            trackedJson.Contains("\"icon\"", StringComparison.Ordinal),
            "flat item JSON must not retain the legacy icon path field"
        );
        _test.True(
            trackedJson.Contains("\"icon_asset_id\"", StringComparison.Ordinal),
            "flat item JSON must carry icon_asset_id"
        );

        int defaultIconCount = imported.Entries.Count(entry =>
            entry.Import.IconAssetId == ItemIconMigrationRules.DefaultIconAssetId
        );
        int emptyIconCount = imported.Entries.Count(entry =>
            entry.Import.IconAssetId.Length == 0
        );
        _test.Eq(defaultIconCount, 109, "default icon asset mapping count should be pinned");
        _test.Eq(emptyIconCount, 20, "empty icon IDs should remain empty");

        string[] manifestLines = ReadUtf8(TrackedManifestPath)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        _test.Eq(
            manifestLines.Length,
            130,
            "icon migration manifest should contain one header plus one row per item"
        );
        _test.Eq(
            manifestLines[0].TrimEnd('\r'),
            "item_id,legacy_icon_path,icon_asset_id",
            "icon migration manifest header"
        );
        var importedIds = new HashSet<string>(
            imported.Entries.Select(entry => entry.Import.ItemId),
            StringComparer.Ordinal
        );
        var manifestIds = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 1; index < manifestLines.Length; index += 1)
        {
            string[] fields = manifestLines[index].TrimEnd('\r').Split(',');
            _test.Eq(fields.Length, 3, $"icon manifest row {index} shape");
            if (fields.Length != 3)
                continue;
            manifestIds.Add(fields[0]);
            _test.True(
                fields[1].Length == 0 || fields[1] == "res://icon.svg",
                $"icon manifest row {index} legacy path"
            );
            _test.Eq(
                fields[2],
                fields[1].Length == 0 ? "" : ItemIconMigrationRules.DefaultIconAssetId,
                $"icon manifest row {index} mapping"
            );
        }
        _test.True(
            manifestIds.SetEquals(importedIds),
            "icon migration manifest IDs must equal the production item IDs"
        );
    }

    private void AssertCanonicalArtifacts(
        string trackedJson,
        ContentImportBatch<ItemImportModel> imported
    )
    {
        if (imported.HasErrors)
            return;
        IReadOnlyList<ItemImportModel> imports = imported.Entries
            .Select(entry => entry.Import)
            .ToArray();
        string canonical = ItemImportCanonicalJson.WriteDocument(
            new ContentCanonicalJsonWriter(),
            "all_items",
            imports
        );
        _test.Eq(trackedJson, canonical, "tracked items JSON must be canonical byte-exact");

        string schema = new ContentJsonSchemaExporter().Export(
            ItemContentJsonAuthoringDomain.SchemaRegistration
        );
        _test.Eq(
            ReadUtf8(TrackedSchemaPath),
            schema,
            "tracked item schema must be generated byte-exact"
        );
    }

    private void AssertPureContracts()
    {
        AssertTypeGraphHasNoResource(typeof(ItemJsonDto), new HashSet<Type>());
        AssertTypeGraphHasNoResource(typeof(ItemImportModel), new HashSet<Type>());
    }

    private void AssertImportDiagnostics(ContentImportBatch<ItemImportModel> imported)
    {
        if (imported.Entries.Count == 0)
            return;

        ItemImportModel sample = imported.Entries[0].Import;
        string canonical = ItemImportCanonicalJson.WriteEntry(
            new ContentCanonicalJsonWriter(),
            sample
        );
        JsonObject unknownMember = JsonNode.Parse(canonical)?.AsObject()
            ?? throw new InvalidOperationException("Could not parse canonical item fixture.");
        unknownMember["unexpected_member"] = true;
        var context = new JsonContentEntryContext(
            ItemContentJsonAuthoringDomain.DomainId,
            sample.ItemId,
            "invalid-item.json#unknown-member",
            "/imports/0"
        );
        ContentImportStageResult<ItemJsonDto> strict = ItemJsonImportParser.Parse(
            context,
            unknownMember.ToJsonString()
        );
        _test.False(strict.HasValue, "unknown item JSON members must fail closed");
        _test.Eq(strict.Diagnostics.Count, 1, "unknown item member diagnostic count");
        if (strict.Diagnostics.Count == 1)
            _test.Eq(
                strict.Diagnostics[0].RuleId,
                ItemJsonImportParser.InvalidDtoRule,
                "unknown item member rule ID"
            );

        JsonObject nullTags = JsonNode.Parse(canonical)?.AsObject()
            ?? throw new InvalidOperationException("Could not parse canonical item fixture.");
        nullTags["tags"] = null;
        ContentImportStageResult<ItemJsonDto> parsed = ItemJsonImportParser.Parse(
            context,
            nullTags.ToJsonString()
        );
        _test.True(parsed.HasValue, "explicit null tags should reach shape normalization");
        if (!parsed.HasValue)
            return;
        ContentImportStageResult<ItemImportModel> normalized = ItemJsonImportParser.Normalize(
            context,
            parsed.Value
        );
        _test.False(normalized.HasValue, "explicit null item tags must fail normalization");
        _test.Eq(normalized.Diagnostics.Count, 1, "null item tags diagnostic count");
        if (normalized.Diagnostics.Count == 1)
        {
            _test.Eq(
                normalized.Diagnostics[0].RuleId,
                ItemJsonImportParser.InvalidShapeRule,
                "null item tags rule ID"
            );
            _test.Eq(
                normalized.Diagnostics[0].JsonPointer,
                "/imports/0/tags",
                "null item tags pointer"
            );
        }
    }

    private void AssertProductionRegistry(ContentImportBatch<ItemImportModel> imported)
    {
        using var registry = new ItemContentRegistry();
        registry.Rebuild();
        _test.Eq(
            registry.ValidateTyped().Count,
            0,
            $"production registry should load flat JSON. errors={string.Join(" | ", registry.ValidateTyped())}"
        );
        IReadOnlyDictionary<StringName, ItemDefinition> definitions =
            registry.GetItemDefsTyped();
        _test.Eq(definitions.Count, 129, "production registry should expose 129 JSON items");
        if (imported.HasErrors || definitions.Count != imported.Entries.Count)
            return;

        foreach (ContentImportEntry<ItemImportModel> entry in imported.Entries)
        {
            var itemId = new StringName(entry.Import.ItemId);
            _test.True(definitions.TryGetValue(itemId, out ItemDefinition? actual), $"registry item {itemId}");
            if (actual == null)
                continue;
            ItemDefinition expected = ItemDefinitionProjector.Project(entry.Import);
            _test.Eq(
                RuntimeFingerprint(actual),
                RuntimeFingerprint(expected),
                $"production registry projection parity {itemId}"
            );
        }
    }

    private static string RuntimeFingerprint(ItemDefinition item)
    {
        var text = new StringBuilder();
        text.Append(item.ItemId).Append('|')
            .Append(item.DisplayName).Append('|')
            .Append(item.Description).Append('|')
            .Append(item.IconAssetId).Append('|')
            .Append(item.IsStackable).Append('|')
            .Append(item.BasePrice).Append('|')
            .Append(item.BuyPrice).Append('|')
            .Append(item.SellPrice).Append('|')
            .Append(item.Sellable).Append('|')
            .Append(item.MaxStack).Append('|')
            .Append(item.ItemCategory).Append('|')
            .AppendJoin(',', item.Tags).Append('|')
            .AppendJoin(',', item.CraftingGroups).Append('|')
            .AppendJoin(',', item.QuestGroups).Append('|')
            .AppendJoin(',', item.TraitIds).Append('|')
            .AppendJoin(',', item.EquipmentSlotIds).Append('|')
            .AppendJoin(',', item.OccupiedSlotIds).Append('|')
            .Append(item.GrantedSkillId).Append('|')
            .Append(item.EquipmentTypeId).Append('|')
            .Append(item.MaxDexBonus);
        foreach (TraitRollGroupDefinition group in item.TraitRollGroups)
        {
            text.Append("|g:").Append(group.GroupId).Append(':').Append(group.RollCount);
            foreach (TraitRollGroupEntryDefinition entry in group.Entries)
                text.Append(':').Append(entry.TraitId).Append('/').Append(entry.Weight)
                    .Append('/').Append(entry.ExclusiveGroup);
        }
        foreach (AttributeModifierDefinition modifier in item.AttributeModifiers)
        {
            text.Append("|m:").Append(modifier.AttributeId).Append('/').Append(modifier.Mode)
                .Append('/').Append(modifier.Value).Append('/').Append(modifier.ValuePerRank)
                .Append('/').Append(modifier.SourceType).Append('/').Append(modifier.SourceId);
        }
        if (item.EquipRequirement != null)
        {
            text.Append("|r:").AppendJoin(',', item.EquipRequirement.RequiredProfessionIds)
                .Append('/').Append(item.EquipRequirement.MinBodySize)
                .Append('/').Append(item.EquipRequirement.MaxBodySize);
            foreach (EquipmentAttributeRequirementDefinition attribute in item.EquipRequirement.AttributeRequirements)
                text.Append(':').Append(attribute.AttributeId).Append('/').Append(attribute.MinValue);
        }
        if (item.WeaponProfile != null)
        {
            WeaponProfileDefinition profile = item.WeaponProfile;
            text.Append("|w:").Append(profile.WeaponTypeId).Append('/')
                .Append(profile.TrainingGroup).Append('/').Append(profile.RangeType)
                .Append('/').Append(profile.Family).Append('/').Append(profile.DamageTag)
                .Append('/').Append(profile.AttackRange).Append('/')
                .Append(DiceFingerprint(profile.OneHandedDice)).Append('/')
                .Append(DiceFingerprint(profile.TwoHandedDice)).Append('/')
                .AppendJoin(',', profile.Properties);
        }
        return text.ToString();
    }

    private static string DiceFingerprint(WeaponDamageDiceDefinition? dice) =>
        dice == null ? "-" : $"{dice.DiceCount}d{dice.DiceSides}+{dice.FlatBonus}";

    private void AssertTypeGraphHasNoResource(Type type, HashSet<Type> visited)
    {
        if (!visited.Add(type))
            return;
        _test.False(
            typeof(Resource).IsAssignableFrom(type),
            $"item DTO/import contract must not contain Godot Resource type {type.FullName}"
        );
        foreach (PropertyInfo property in type.GetProperties(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        ))
        {
            Type propertyType = Unwrap(property.PropertyType);
            if (propertyType.Namespace == typeof(string).Namespace || propertyType.IsPrimitive)
                continue;
            if (propertyType.Assembly == typeof(ItemImportModel).Assembly)
                AssertTypeGraphHasNoResource(propertyType, visited);
        }
    }

    private static Type Unwrap(Type type)
    {
        if (type.IsArray)
            return type.GetElementType() ?? type;
        if (type.IsGenericType)
            return type.GetGenericArguments().Last();
        return Nullable.GetUnderlyingType(type) ?? type;
    }

    private static string ReadUtf8(string path)
    {
        using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null)
            throw new InvalidOperationException($"Could not open tracked item artifact {path}.");
        return file.GetAsText(skipCr: false);
    }

    private static string FormatDiagnostics(IReadOnlyList<ContentJsonDiagnostic> diagnostics) =>
        string.Join(
            " | ",
            diagnostics.Select(diagnostic =>
                $"{diagnostic.RuleId}@{diagnostic.SourceLabel}{diagnostic.JsonPointer}: {diagnostic.Message}"
            )
        );
}
