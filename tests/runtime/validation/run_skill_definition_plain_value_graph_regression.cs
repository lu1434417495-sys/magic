using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_skill_definition_plain_value_graph_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestAllowedGodotValuesBecomePlainFrozenValues();
        TestMathAllowlistProjectionRoundTrip();
        TestAllDefinitionGraphsDefensivelyDeepFreezeSyntheticInput();
        TestMalformedGodotValuesReportFullSkillPaths();
        TestStrictDictionaryAndPackedValueRejection();
        TestSyntheticIllegalObjectAndCycleRejection();
        TestResourceDefaultsAreEffectiveWithoutWritingBack();
        TestFingerprintAndLevelDescriptionRemainStable();

        RequestTestExit(_test.Finish("Skill definition plain value graph regression"));
    }

    private void TestMathAllowlistProjectionRoundTrip()
    {
        var mathValues = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["vector2"] = new Vector2(1.25f, -2.5f),
            ["vector2i"] = new Vector2I(2, -3),
            ["rect2"] = new Rect2(new Vector2(1.0f, 2.0f), new Vector2(3.0f, 4.0f)),
            ["rect2i"] = new Rect2I(new Vector2I(5, 6), new Vector2I(7, 8)),
            ["vector3"] = new Vector3(1.0f, 2.0f, 3.0f),
            ["vector3i"] = new Vector3I(4, 5, 6),
            ["transform2d"] = Transform2D.Identity,
            ["vector4"] = new Vector4(1.0f, 2.0f, 3.0f, 4.0f),
            ["vector4i"] = new Vector4I(5, 6, 7, 8),
            ["plane"] = new Plane(new Vector3(0.0f, 1.0f, 0.0f), 2.5f),
            ["quaternion"] = Quaternion.Identity,
            ["aabb"] = new Aabb(new Vector3(1.0f, 2.0f, 3.0f), new Vector3(4.0f, 5.0f, 6.0f)),
            ["basis"] = Basis.Identity,
            ["transform3d"] = Transform3D.Identity,
            ["projection"] = Projection.Identity,
            ["color"] = new Color(0.1f, 0.2f, 0.3f, 0.4f),
        };
        IReadOnlyDictionary<string, object> normalized =
            ContentValueNormalizer.NormalizeDictionary(mathValues, "skill.math.params");
        Dictionary<string, object> cloned = RuntimePlainPayload.CloneDictionary(normalized);
        AssertMathValuesEqual(mathValues, cloned, "plain clone");
        LifecycleAuditSnapshot baseline = LifecycleAuditRegistry.Shared.CaptureSnapshot();
        using (
            GodotProjectionLease<GDictionary> lease = RuntimePlainPayload.ProjectDictionaryLease(
                normalized,
                "skill-math-round-trip",
                LifetimeDomain.Request,
                "skill.math.params"
            )
        )
        {
            LifecycleAuditSnapshot active = LifecycleAuditRegistry.Shared.CaptureSnapshot();
            _test.Eq(
                active.ActiveLeaseCount,
                baseline.ActiveLeaseCount + 1,
                "Math projection should register one request lease."
            );
            _test.Eq(
                active.ActiveOwnerCount,
                baseline.ActiveOwnerCount + 1,
                "Math-only projection should own only its root dictionary."
            );

            Dictionary<string, object> nonStrict = RuntimePlainPayload.NormalizeDictionary(
                lease.Value,
                "skill.math.non_strict"
            );
            Dictionary<string, object> strict = RuntimePlainPayload.NormalizeDictionaryStrict(
                lease.Value,
                "skill.math.strict"
            );
            Dictionary<string, object> restored = RuntimePlainPayload.RestoreSaveDictionary(
                lease.Value,
                "skill.math.restore"
            );
            IReadOnlyDictionary<string, object> renormalized =
                ContentValueNormalizer.NormalizeDictionary(restored, "skill.math.renormalized");

            AssertMathValuesEqual(mathValues, nonStrict, "non-strict normalize");
            AssertMathValuesEqual(mathValues, strict, "strict normalize");
            AssertMathValuesEqual(mathValues, restored, "save restore");
            AssertMathValuesEqual(mathValues, renormalized, "content renormalize");
        }

        LifecycleAuditSnapshot after = LifecycleAuditRegistry.Shared.CaptureSnapshot();
        _test.Eq(
            after.ActiveOwnerCount,
            baseline.ActiveOwnerCount,
            "Math projection owners should return to baseline."
        );
        _test.Eq(
            after.ActiveLeaseCount,
            baseline.ActiveLeaseCount,
            "Math projection lease should return to baseline."
        );
        _test.Eq(
            after.ActiveScopeCount,
            baseline.ActiveScopeCount,
            "Math projection scopes should return to baseline."
        );
        _test.Eq(
            after.ActiveContentBorrowerCount,
            baseline.ActiveContentBorrowerCount,
            "Math projection borrowers should return to baseline."
        );
    }

    private void TestAllowedGodotValuesBecomePlainFrozenValues()
    {
        GDictionary root = new();
        GDictionary nested = new();
        GArray list = new();
        try
        {
            nested["name"] = new StringName("nested_name");
            list.Add(9);
            list.Add(nested);
            root["nil"] = default(Variant);
            root["bool"] = true;
            root["int"] = 7;
            root["float"] = 1.5;
            root["string"] = "text";
            root["string_name"] = new StringName("content_id");
            root["vector2i"] = new Vector2I(2, 3);
            root["vector3"] = new Vector3(1.0f, 2.0f, 3.0f);
            root["color"] = new Color(0.1f, 0.2f, 0.3f, 1.0f);
            root["list"] = list;

            IReadOnlyDictionary<string, object> normalized =
                ContentValueNormalizer.NormalizeDictionary(root, "skill.allowed.params");

            _test.True(normalized["nil"] == null, "Nil should normalize to null.");
            _test.Eq(normalized["bool"], true, "Bool should stay a plain bool.");
            _test.Eq(normalized["int"], 7L, "Integral Variant should normalize to Int64.");
            _test.Eq(normalized["float"], 1.5d, "Float Variant should normalize to Double.");
            _test.Eq(normalized["string"], "text", "String should stay a string.");
            _test.Eq(
                normalized["string_name"],
                new StringName("content_id"),
                "StringName should remain the approved Godot value type."
            );
            _test.Eq(
                normalized["vector2i"],
                new Vector2I(2, 3),
                "Approved math values should remain value types."
            );
            _test.False(
                ContainsGodotContainerOrVariant(normalized),
                "Normalized content graphs must not retain Variant or Godot collection wrappers."
            );
        }
        finally
        {
            root.Clear();
            list.Clear();
            nested.Clear();
            nested.Dispose();
            list.Dispose();
            root.Dispose();
        }
    }

    private void TestAllDefinitionGraphsDefensivelyDeepFreezeSyntheticInput()
    {
        var nestedList = new List<object>
        {
            3,
            new Dictionary<string, object> { ["inner"] = "original" },
        };
        var valueSource = new Dictionary<string, object>
        {
            ["number"] = 4,
            ["nested"] = nestedList,
        };
        var levelSource = new Dictionary<int, IReadOnlyDictionary<string, object>>
        {
            [1] = valueSource,
        };

        SkillDefinition skill = TestSkillDefinitionProjection.BuildSkill(
            "plain_graph_skill",
            levelDescriptionConfigs: levelSource
        );
        ContingencyAutomationDefinition contingency =
            TestSkillDefinitionProjection.BuildContingencyAutomation(
                allowedParameterBindings: valueSource
            );
        CombatSkillDefinition combat = TestSkillDefinitionProjection.BuildCombatProfile(
            "plain_graph_skill",
            levelOverrides: levelSource
        );
        CombatEffectDefinition effect = TestSkillDefinitionProjection.BuildEffect(
            "status",
            parameters: valueSource
        );
        CombatCastVariantDefinition castVariant = TestSkillDefinitionProjection.BuildCastVariant(
            "plain_graph_variant",
            0,
            Array.Empty<CombatEffectDefinition>(),
            parameters: valueSource
        );

        valueSource["number"] = 99;
        nestedList[0] = 88;
        ((Dictionary<string, object>)nestedList[1])["inner"] = "changed";
        levelSource[1] = new Dictionary<string, object> { ["number"] = -1 };

        AssertFrozenGraph(skill.LevelDescriptionConfigs[1], "SkillDefinition");
        AssertFrozenGraph(contingency.AllowedParameterBindings, "ContingencyAutomationDefinition");
        AssertFrozenGraph(combat.LevelOverrides[1], "CombatSkillDefinition");
        AssertFrozenGraph(effect.Parameters, "CombatEffectDefinition");
        AssertFrozenGraph(castVariant.Parameters, "CombatCastVariantDefinition");

        bool mapMutationRejected = false;
        try
        {
            ((IDictionary<string, object>)effect.Parameters)["new"] = 1L;
        }
        catch (NotSupportedException)
        {
            mapMutationRejected = true;
        }
        _test.True(mapMutationRejected, "Top-level normalized maps must reject mutation.");

        bool listMutationRejected = false;
        try
        {
            ((IList<object>)effect.Parameters["nested"])[0] = 12L;
        }
        catch (NotSupportedException)
        {
            listMutationRejected = true;
        }
        _test.True(listMutationRejected, "Nested normalized lists must reject mutation.");
    }

    private void TestMalformedGodotValuesReportFullSkillPaths()
    {
        using (
            var effectScope = new NativeLeaseScope(
                "skill-plain-value-effect-path",
                LifetimeDomain.Request
            )
        )
        {
            Resource illegalEffectValue = effectScope.Own(
                new Resource(),
                "plain-value-effect-illegal-object"
            );
            GDictionary effectNested = effectScope.Own(
                new GDictionary { ["bad"] = illegalEffectValue },
                "plain-value-effect-nested"
            );
            GArray effectList = effectScope.Own(
                new GArray { effectNested },
                "plain-value-effect-list"
            );
            GDictionary effectParams = effectScope.Own(
                new GDictionary { ["outer"] = effectList },
                "plain-value-effect-params"
            );
            CombatEffectDef effect = effectScope.Own(
                new CombatEffectDef { effect_type = "damage", @params = effectParams },
                "plain-value-effect"
            );
            GArray effectDefsOwner = effectScope.Own(
                new GArray(),
                "plain-value-effect-defs-owner"
            );
            var effectDefs = new Godot.Collections.Array<CombatEffectDef>(effectDefsOwner);
            effectDefs.Add(effect);
            CombatSkillDef effectCombat = effectScope.Own(
                new CombatSkillDef { effect_defs = effectDefs },
                "plain-value-effect-combat"
            );
            SkillDef effectSkill = effectScope.Own(
                new SkillDef { skill_id = "charge", combat_profile = effectCombat },
                "plain-value-effect-skill"
            );

            AssertInvalidDataPath(
                () => SkillDefinition.FromResource(effectSkill),
                "skill.charge.combat_profile.effect_defs[0].params.outer[0].bad",
                "Nested effect Object rejection should identify the complete authored skill path."
            );
        }

        using (
            var variantScope = new NativeLeaseScope(
                "skill-plain-value-variant-path",
                LifetimeDomain.Request
            )
        )
        {
            Resource illegalVariantValue = variantScope.Own(
                new Resource(),
                "plain-value-variant-illegal-object"
            );
            GDictionary variantParams = variantScope.Own(
                new GDictionary { ["nested"] = illegalVariantValue },
                "plain-value-variant-params"
            );
            CombatCastVariantDef variant = variantScope.Own(
                new CombatCastVariantDef { variant_id = "wide", @params = variantParams },
                "plain-value-cast-variant"
            );
            GArray variantsOwner = variantScope.Own(
                new GArray(),
                "plain-value-cast-variants-owner"
            );
            var variants = new Godot.Collections.Array<CombatCastVariantDef>(variantsOwner);
            variants.Add(variant);
            CombatSkillDef variantCombat = variantScope.Own(
                new CombatSkillDef { cast_variants = variants },
                "plain-value-variant-combat"
            );
            SkillDef variantSkill = variantScope.Own(
                new SkillDef { skill_id = "teleport", combat_profile = variantCombat },
                "plain-value-variant-skill"
            );

            AssertInvalidDataPath(
                () => SkillDefinition.FromResource(variantSkill),
                "skill.teleport.combat_profile.cast_variants[0].params.nested",
                "Cast-variant Object rejection should identify the complete authored skill path."
            );
        }
    }

    private void TestStrictDictionaryAndPackedValueRejection()
    {
        GDictionary nonStringKey = new() { [1] = "bad" };
        GDictionary emptyKey = new() { [""] = "bad" };
        GDictionary packedValue = new() { ["packed"] = new byte[] { 1, 2 } };
        try
        {
            AssertInvalidDataPath(
                () => ContentValueNormalizer.NormalizeDictionary(
                    nonStringKey,
                    "skill.strict.params"
                ),
                "skill.strict.params",
                "Non-string dictionary keys must be rejected."
            );
            AssertInvalidDataPath(
                () => ContentValueNormalizer.NormalizeDictionary(
                    emptyKey,
                    "skill.strict.params"
                ),
                "skill.strict.params",
                "Empty dictionary keys must be rejected."
            );
            AssertInvalidDataPath(
                () => ContentValueNormalizer.NormalizeDictionary(
                    packedValue,
                    "skill.strict.params"
                ),
                "skill.strict.params.packed",
                "Packed arrays must be rejected instead of silently stringified."
            );
        }
        finally
        {
            nonStringKey.Clear();
            emptyKey.Clear();
            packedValue.Clear();
            nonStringKey.Dispose();
            emptyKey.Dispose();
            packedValue.Dispose();
        }
    }

    private void TestSyntheticIllegalObjectAndCycleRejection()
    {
        var illegal = new Dictionary<string, object>
        {
            ["outer"] = new List<object>
            {
                new Dictionary<string, object> { ["bad"] = new object() },
            },
        };
        AssertInvalidDataPath(
            () => ContentValueNormalizer.NormalizeDictionary(
                illegal,
                "CombatEffectDefinition.Parameters"
            ),
            "CombatEffectDefinition.Parameters.outer[0].bad",
            "Synthetic illegal objects must be rejected with the full nested path."
        );

        var cycle = new Dictionary<string, object>();
        cycle["self"] = cycle;
        AssertInvalidDataPath(
            () => ContentValueNormalizer.NormalizeDictionary(cycle, "synthetic.cycle"),
            "synthetic.cycle.self",
            "Synthetic cycles must fail deterministically instead of recursing indefinitely."
        );

        AssertInvalidDataPath(
            () => ContentValueNormalizer.NormalizeDictionary(
                new DuplicateKeyReadOnlyDictionary(),
                "synthetic.duplicate"
            ),
            "synthetic.duplicate",
            "Duplicate normalized synthetic keys must be rejected."
        );

        var managedStringNameKeys = new Dictionary<StringName, object>
        {
            ["content_id"] = 1,
        };
        AssertInvalidDataPath(
            () => ContentValueNormalizer.NormalizeValue(
                managedStringNameKeys,
                "synthetic.string_name_keys"
            ),
            "synthetic.string_name_keys",
            "Managed maps must use string keys even though raw Godot dictionaries also accept StringName keys."
        );
    }

    private void TestResourceDefaultsAreEffectiveWithoutWritingBack()
    {
        using (
            var scope = new NativeLeaseScope(
                "skill-plain-value-defaults",
                LifetimeDomain.Request
            )
        )
        {
            CombatSkillDef rawCombat = scope.Own(
                new CombatSkillDef { skill_id = "" },
                "plain-value-default-combat"
            );
            SkillDef rawSkill = scope.Own(
                new SkillDef
                {
                    skill_id = "default_probe",
                    icon_id = "",
                    combat_profile = rawCombat,
                },
                "plain-value-default-skill"
            );

            SkillDefinition definition = SkillDefinition.FromResource(rawSkill);
            _test.True(definition != null, "Default probe should project a SkillDefinition.");
            _test.Eq(
                definition?.IconId ?? default,
                new StringName("default_probe"),
                "Missing icon_id should use skill_id in the typed projection."
            );
            _test.Eq(
                definition?.CombatProfile?.SkillId ?? default,
                new StringName("default_probe"),
                "Missing combat_profile.skill_id should use the parent skill id in the typed projection."
            );
            _test.True(rawSkill.icon_id == "", "Projection must not write icon_id back.");
            _test.True(
                rawCombat.skill_id == "",
                "Projection must not write combat_profile.skill_id back."
            );
        }
    }

    private void TestFingerprintAndLevelDescriptionRemainStable()
    {
        var config = new Dictionary<string, object>
        {
            ["power"] = 4,
            ["status"] = new StringName("burning"),
        };
        SkillDefinition skill = TestSkillDefinitionProjection.BuildSkill(
            "plain_graph_description",
            levelDescriptionTemplate: "伤害{power}，状态{status}",
            levelDescriptionConfigs:
                new Dictionary<int, IReadOnlyDictionary<string, object>> { [0] = config }
        );

        string fingerprintBefore = BuildFingerprint(skill.LevelDescriptionConfigs[0]);
        string descriptionBefore;
        using (GDictionary context = new())
        {
            descriptionBefore = SkillLevelDescriptionFormatter.BuildLevelDescription(
                skill,
                0,
                context
            );
        }

        config["power"] = 99;
        config["status"] = new StringName("changed");

        string fingerprintAfter = BuildFingerprint(skill.LevelDescriptionConfigs[0]);
        string descriptionAfter;
        using (GDictionary context = new())
        {
            descriptionAfter = SkillLevelDescriptionFormatter.BuildLevelDescription(
                skill,
                0,
                context
            );
        }

        _test.Eq(
            fingerprintAfter,
            fingerprintBefore,
            "Skill plain-value fingerprint must not change when caller-owned inputs mutate."
        );
        _test.Eq(descriptionBefore, "伤害4，状态burning", "Level description output stays stable.");
        _test.Eq(
            descriptionAfter,
            descriptionBefore,
            "Level descriptions must read the frozen definition graph."
        );
    }

    private void AssertFrozenGraph(
        IReadOnlyDictionary<string, object> graph,
        string ownerLabel
    )
    {
        _test.Eq(graph["number"], 4L, $"{ownerLabel} should copy integral values.");
        _test.True(
            graph["nested"] is IReadOnlyList<object> values
                && values.Count == 2
                && Equals(values[0], 3L)
                && values[1] is IReadOnlyDictionary<string, object> nested
                && Equals(nested["inner"], "original"),
            $"{ownerLabel} should recursively copy and freeze nested list/map values."
        );
    }

    private void AssertMathValuesEqual(
        IReadOnlyDictionary<string, object> expected,
        IReadOnlyDictionary<string, object> actual,
        string label
    )
    {
        _test.Eq(actual.Count, expected.Count, $"{label} should preserve the math value count.");
        foreach (KeyValuePair<string, object> entry in expected)
        {
            _test.True(
                actual.TryGetValue(entry.Key, out object actualValue),
                $"{label} should preserve key '{entry.Key}'."
            );
            _test.True(
                actualValue?.GetType() == entry.Value.GetType(),
                $"{label} should preserve the CLR type for '{entry.Key}'."
            );
            _test.Eq(
                actualValue,
                entry.Value,
                $"{label} should round-trip '{entry.Key}' without value loss."
            );
        }
    }

    private void AssertInvalidDataPath(Action action, string expectedPath, string message)
    {
        try
        {
            action();
            _test.Fail($"{message} Expected InvalidDataException.");
        }
        catch (InvalidDataException exception)
        {
            _test.True(
                exception.Message.Contains(expectedPath, StringComparison.Ordinal),
                $"{message} actual={exception.Message}"
            );
        }
        catch (Exception exception)
        {
            _test.Fail($"{message} Expected InvalidDataException, got {exception.GetType().Name}.");
        }
    }

    private static bool ContainsGodotContainerOrVariant(object value)
    {
        if (value is Variant or GDictionary or GArray or GodotObject)
            return true;
        if (value is IReadOnlyDictionary<string, object> dictionary)
            return dictionary.Values.Any(ContainsGodotContainerOrVariant);
        if (value is IReadOnlyList<object> list)
            return list.Any(ContainsGodotContainerOrVariant);
        return false;
    }

    private static string BuildFingerprint(object value)
    {
        var builder = new StringBuilder();
        AppendFingerprint(builder, value);
        return builder.ToString();
    }

    private static void AppendFingerprint(StringBuilder builder, object value)
    {
        switch (value)
        {
            case null:
                builder.Append("null");
                return;
            case IReadOnlyDictionary<string, object> dictionary:
                builder.Append('{');
                foreach (string key in dictionary.Keys.OrderBy(key => key, StringComparer.Ordinal))
                {
                    builder.Append(key).Append(':');
                    AppendFingerprint(builder, dictionary[key]);
                    builder.Append(';');
                }
                builder.Append('}');
                return;
            case IReadOnlyList<object> list:
                builder.Append('[');
                foreach (object entry in list)
                {
                    AppendFingerprint(builder, entry);
                    builder.Append(';');
                }
                builder.Append(']');
                return;
            case StringName stringName:
                builder.Append("sn:").Append(stringName.ToString());
                return;
            case IFormattable formattable:
                builder.Append(value.GetType().Name)
                    .Append(':')
                    .Append(formattable.ToString(null, CultureInfo.InvariantCulture));
                return;
            default:
                builder.Append(value.GetType().Name).Append(':').Append(value);
                return;
        }
    }

    private sealed class DuplicateKeyReadOnlyDictionary
        : IReadOnlyDictionary<string, object>
    {
        public object this[string key] => 1;
        public IEnumerable<string> Keys => new[] { "same", "same" };
        public IEnumerable<object> Values => new object[] { 1, 2 };
        public int Count => 2;

        public bool ContainsKey(string key) => key == "same";

        public bool TryGetValue(string key, out object value)
        {
            value = 1;
            return key == "same";
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            yield return new KeyValuePair<string, object>("same", 1);
            yield return new KeyValuePair<string, object>("same", 2);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
