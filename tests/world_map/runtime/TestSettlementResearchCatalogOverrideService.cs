using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class TestSettlementResearchCatalogOverrideService : SettlementResearchService
{
    private GArray _catalog = new();

    public void setup(GArray catalog_data)
    {
        _catalog = DuplicateDictionaryArray(catalog_data ?? new GArray());
    }

    protected override GArray GetResearchRewardCatalogCore()
    {
        return DuplicateDictionaryArray(_catalog);
    }

    private static GArray DuplicateDictionaryArray(GArray value)
    {
        var result = new GArray();
        foreach (Variant entryValue in value)
        {
            if (entryValue.VariantType != Variant.Type.Dictionary)
                continue;
            GDictionary entry = entryValue.AsGodotDictionary();
            result.Add((GDictionary)entry.Duplicate(true));
        }
        return result;
    }
}
