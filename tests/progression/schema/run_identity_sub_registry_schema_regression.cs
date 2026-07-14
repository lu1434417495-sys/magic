using Godot;

public partial class run_identity_sub_registry_schema_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestIdentityCatalogLoadsTypedContent();
        RequestTestExit(_test.Finish("Identity sub registry schema regression"));
    }

    private void TestIdentityCatalogLoadsTypedContent()
    {
        using var registry = new ProgressionContentRegistry(new TestContentResourceLoader());
        ProgressionIdentityCatalogData catalog = registry.GetIdentityCatalogTyped();

        _test.True(
            catalog.RaceDefs.Count > 0,
            "identity catalog 应从正式配置加载 race definitions。"
        );
        _test.True(
            catalog.SubraceDefs.Count > 0,
            "identity catalog 应从正式配置加载 subrace definitions。"
        );
        _test.True(
            catalog.AgeProfileDefs.Count > 0,
            "identity catalog 应从正式配置加载 age profile definitions。"
        );
        _test.True(
            catalog.StageAdvancementDefs.Count > 0,
            "identity catalog 应从正式配置加载 stage advancement definitions。"
        );
    }
}
