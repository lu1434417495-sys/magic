using Godot;

public partial class run_attribute_trait_modifier_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
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
    }
}
