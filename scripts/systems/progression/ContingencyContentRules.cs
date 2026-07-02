using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public readonly record struct ContingencyTemplateStoredSpellInfo(
    StringName StoredSkillId,
    int MaxCastLevel
);

public static class ContingencyContentRules
{
    public static readonly StringName ChargeMaterialItemId = "special_contingency_gem";
    public const int ChargeMaterialQuantity = 1;
    public const int ReservedMpPerMatrixLoad = 2;

    public static int ResolveReservedMpMax(int matrixLoad) =>
        Mathf.Max(matrixLoad * ReservedMpPerMatrixLoad, 1);

    public static IReadOnlyList<ContingencyTemplateStoredSpellInfo> GetTemplateStoredSpellsTyped(
        ContingencySetupTemplateDef template
    )
    {
        var result = new List<ContingencyTemplateStoredSpellInfo>();
        if (template?.stored_spells == null)
            return result;
        foreach (GDictionary authoredSpell in template.stored_spells)
        {
            StringName storedSkillId = ReadStringName(authoredSpell, "stored_skill_id");
            if (storedSkillId == "")
                return new List<ContingencyTemplateStoredSpellInfo>();
            int maxCastLevel = ReadInt(authoredSpell, "max_cast_level", 1);
            result.Add(
                new ContingencyTemplateStoredSpellInfo(storedSkillId, Mathf.Max(maxCastLevel, 1))
            );
        }
        return result;
    }

    // Stamps the per-member dynamic fields (source_skill_level, per-spell cast_level)
    // onto an authored template. The produced payload goes through the single schema
    // authority ContingencyMatrixSetupState.FromDictionary; this builder adds no
    // validation of its own beyond structural reads.
    public static GDictionary BuildSetupPayloadFromTemplate(
        ContingencySetupTemplateDef template,
        int sourceSkillLevel,
        IReadOnlyDictionary<StringName, int> castLevelsByStoredSkillId
    )
    {
        if (template == null)
            return null;

        var storedSpells = new GArray();
        foreach (GDictionary authoredSpell in template.stored_spells ?? new())
        {
            StringName storedSkillId = ReadStringName(authoredSpell, "stored_skill_id");
            if (storedSkillId == "")
                return null;
            int castLevel = 1;
            if (
                castLevelsByStoredSkillId != null
                && castLevelsByStoredSkillId.TryGetValue(storedSkillId, out int resolvedLevel)
            )
                castLevel = Mathf.Max(resolvedLevel, 1);
            storedSpells.Add(
                new GDictionary
                {
                    ["stored_skill_id"] = storedSkillId,
                    ["cast_level"] = castLevel,
                    ["order"] = ReadInt(authoredSpell, "order", 1),
                    ["target_resolver"] = ReadDictionary(authoredSpell, "target_resolver"),
                    ["parameter_bindings"] = ReadDictionary(authoredSpell, "parameter_bindings"),
                    ["fallback_policy"] = ReadStringName(authoredSpell, "fallback_policy"),
                }
            );
        }

        return new GDictionary
        {
            ["setup_id"] = template.template_id,
            ["display_name"] = template.display_name ?? "",
            ["enabled"] = true,
            ["charged"] = false,
            ["source_skill_id"] = template.source_skill_id,
            ["source_skill_level"] = Mathf.Max(sourceSkillLevel, 1),
            ["matrix_load"] = template.matrix_load,
            ["reserved_mp_max"] = 0,
            ["material_costs"] = new GArray(),
            ["trigger"] = NormalizeAuthoredStringValues(
                template.trigger?.Duplicate(true) ?? new GDictionary()
            ),
            ["release_mode"] = template.release_mode,
            ["stored_spells"] = storedSpells,
        };
    }

    // Trigger payloads persist verbatim and downstream snapshot readers use the
    // exact-String contract, so authored StringName values must be flattened here.
    private static GDictionary NormalizeAuthoredStringValues(GDictionary source)
    {
        if (source == null)
            return new GDictionary();
        var keys = new List<Variant>();
        foreach (Variant key in source.Keys)
            keys.Add(key);
        foreach (Variant key in keys)
        {
            Variant value = source[key];
            if (value.VariantType == Variant.Type.StringName)
                source[key] = value.AsStringName().ToString();
            else if (value.VariantType == Variant.Type.Dictionary)
                source[key] = NormalizeAuthoredStringValues(value.AsGodotDictionary());
        }
        return source;
    }

    private static StringName ReadStringName(GDictionary source, string key)
    {
        if (source == null || !source.ContainsKey(key))
            return "";
        return ProgressionDataUtils.to_string_name(source[key]);
    }

    private static int ReadInt(GDictionary source, string key, int fallback)
    {
        if (source == null || !source.ContainsKey(key))
            return fallback;
        Variant value = source[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static GDictionary ReadDictionary(GDictionary source, string key)
    {
        if (source == null || !source.ContainsKey(key))
            return new GDictionary();
        Variant value = source[key];
        return value.VariantType == Variant.Type.Dictionary
            ? value.AsGodotDictionary().Duplicate(true)
            : new GDictionary();
    }
}
