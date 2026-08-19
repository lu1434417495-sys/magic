#nullable enable

using System;

public partial class run_skill_generation_schema_guidance_regression
    : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            string schema = new ContentJsonSchemaExporter().Export(
                ContentJsonSchemaCatalog.Require("skills")
            );
            AssertContains(
                schema,
                "The canonical JSON field is mp_cost; mana_cost is not supported.",
                "schema should prevent the observed mana_cost field hallucination"
            );
            AssertContains(
                schema,
                "Use freeze for cold damage; cold is not valid.",
                "schema should name the canonical cold-damage identifier"
            );
            AssertContains(
                schema,
                "Force damage must explicitly include force_effect",
                "schema should state the force effect-category dependency"
            );
            AssertContains(
                schema,
                "do not invent a per-skill identifier.",
                "schema should explain that save_tag is a closed semantic context"
            );
            AssertContains(
                schema,
                "basic=60, intermediate=120, advanced=180, ultimate=240.",
                "schema should expose every attribute-growth budget"
            );
        }
        catch (Exception exception)
        {
            _test.Fail(
                $"Unexpected generated-skill schema guidance exception: {exception}"
            );
        }

        RequestTestExit(_test.Finish("Generated-skill schema guidance regression"));
    }

    private void AssertContains(string value, string expected, string message)
    {
        _test.True(
            value.Contains(expected, StringComparison.Ordinal),
            message
        );
    }
}
