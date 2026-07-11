using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Godot;

public partial class run_attribute_trait_modifier_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestRuntimeModifierContractsArePlain();
        TestTraitAttributeModifiersApplyAndPreserveSource();
        RequestTestExit(_test.Finish("Attribute trait modifier regression"));
    }

    private void TestRuntimeModifierContractsArePlain()
    {
        _test.False(
            typeof(GodotObject).IsAssignableFrom(typeof(AttributeModifierDefinition)),
            "Runtime attribute modifier definitions must remain plain CLR values."
        );

        foreach (
            string fieldName in new[]
            {
                "_trait_attribute_modifiers",
                "_equipment_state",
                "_passive_state",
                "_temporary_effects",
            }
        )
        {
            FieldInfo field = typeof(AttributeService).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic
            );
            _test.Eq(
                field?.FieldType,
                typeof(IReadOnlyList<AttributeModifierDefinition>),
                $"AttributeService {fieldName} should expose the plain runtime modifier contract."
            );
        }
    }

    private void TestTraitAttributeModifiersApplyAndPreserveSource()
    {
        UnitProgress progress = new()
        {
            unit_id = "trait_attr",
            display_name = "Trait Attr",
        };
        progress.unit_base_attributes.SetAttributeValue("strength", 10);

        AttributeService service = new();
        service.SetupContext(
            new AttributeSourceContext
            {
                unit_progress = progress,
                trait_attribute_modifiers = new AttributeModifierDefinition[]
                {
                    new(
                        "strength",
                        AttributeModifier.ToStringName(AttributeModifierMode.Flat),
                        3,
                        0,
                        "trait_character",
                        "additive_power"
                    ),
                },
            }
        );

        AttributeSnapshot snapshot = service.GetSnapshot();
        _test.Eq(
            snapshot.GetValue("strength"),
            13,
            "AttributeService should apply trait attribute modifiers."
        );
        _test.True(
            HasModifierEntry(service, "strength", "trait_character", "additive_power"),
            "Trait modifier entries should preserve trait source_type and effective source_id."
        );
    }

    private static bool HasModifierEntry(
        AttributeService service,
        StringName attributeId,
        StringName sourceType,
        StringName sourceId
    )
    {
        MethodInfo method = typeof(AttributeService).GetMethod(
            "CollectAllModifierEntries",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        if (method == null)
            return false;

        if (method.Invoke(service, null) is not IEnumerable entries)
            return false;

        foreach (object entry in entries)
        {
            if (
                ReadStringName(entry, "AttributeId") == attributeId
                && ReadStringName(entry, "SourceType") == sourceType
                && ReadStringName(entry, "SourceId") == sourceId
            )
                return true;
        }
        return false;
    }

    private static StringName ReadStringName(object entry, string propertyName)
    {
        if (entry == null)
            return "";
        PropertyInfo property = entry.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );
        object value = property?.GetValue(entry);
        return value is StringName stringName ? stringName : new StringName(value?.ToString() ?? "");
    }
}
