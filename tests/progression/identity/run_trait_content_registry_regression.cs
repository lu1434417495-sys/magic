using System.Collections.Generic;
using Godot;

public partial class run_trait_content_registry_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestOfficialTraitRegistryValidatesWithoutErrors();
        TestOfficialTraitRegistryUsesGenericIdentityDefs();
        TestInvalidTraitFixturesAreRejected();

        RequestTestExit(_test.Finish("Trait content registry regression"));
    }

    private void TestOfficialTraitRegistryValidatesWithoutErrors()
    {
        using TraitContentRegistry registry = new(new TestContentResourceLoader());
        List<string> errors = ToList(registry.Validate());
        _test.Eq(
            errors.Count,
            0,
            $"Official trait content should validate without errors: {Format(errors)}"
        );
    }

    private void TestOfficialTraitRegistryUsesGenericIdentityDefs()
    {
        using TraitContentRegistry registry = new(new TestContentResourceLoader());
        IReadOnlyDictionary<StringName, TraitDefinition> traitDefs =
            registry.GetTraitDefsTyped();

        foreach (TraitDefinition traitDef in traitDefs.Values)
        {
            if (traitDef == null)
                continue;
            StringName traitId = traitDef.TraitId;
            if (!TraitContentRules.IsSourceKindAllowed(traitDef, TraitSourceKind.Identity))
                continue;

            _test.Eq(
                traitDef.EffectKind != TraitEffectKind.Unknown,
                true,
                $"{traitId} should use a known generic effect type."
            );
            _test.Eq(
                traitDef.StackPolicyKind,
                TraitStackPolicyKind.UniqueByTrait,
                $"{traitId} identity trait should default to unique_by_trait."
            );
        }

        AssertTraitRuntimePolicy(
            traitDefs,
            TraitContentRules.ToStringName(TraitEffectKind.HalflingLuck),
            TraitTriggerKind.OnNaturalOne,
            TraitChargeScopeKind.PerTurn,
            TraitChargeResetTimingKind.TurnStart
        );
        AssertTraitRuntimePolicy(
            traitDefs,
            TraitContentRules.ToStringName(TraitEffectKind.SavageAttacks),
            TraitTriggerKind.OnCrit,
            TraitChargeScopeKind.None,
            TraitChargeResetTimingKind.None
        );
        AssertTraitRuntimePolicy(
            traitDefs,
            TraitContentRules.ToStringName(TraitEffectKind.RelentlessEndurance),
            TraitTriggerKind.OnFatalDamage,
            TraitChargeScopeKind.PerBattle,
            TraitChargeResetTimingKind.BattleStart
        );
    }

    private void TestInvalidTraitFixturesAreRejected()
    {
        using TraitContentRegistry registry = new(new TestContentResourceLoader());
        registry.LoadFromDirectories(
            new Godot.Collections.Array<string>
            {
                "res://tests/progression/fixtures/trait_registry_invalid",
            }
        );
        List<string> errors = ToList(registry.Validate());
        _test.True(errors.Count >= 3, $"invalid fixtures should produce errors: {Format(errors)}");
        _test.True(
            Contains(errors, "allowed_source_kind"),
            "missing/invalid source kind should be rejected."
        );
        _test.True(
            Contains(errors, "attribute_modifiers"),
            "identity attribute modifier should be rejected."
        );
        _test.True(
            Contains(errors, "attribute_id"),
            "trait attribute modifiers should reject empty attribute_id."
        );
        _test.True(
            Contains(errors, "mode uses unsupported"),
            "trait attribute modifiers should reject unsupported modes."
        );
        _test.True(
            Contains(errors, "dispatch"),
            "non-passive generic traits without runtime dispatch coverage should be rejected."
        );
        _test.True(
            Contains(errors, "roll_value_schema") && Contains(errors, "fixed"),
            "fixed-source traits should reject roll_value_schema."
        );
        _test.True(
            Contains(errors, "charge_scope"),
            "invalid charge scope should be rejected."
        );
        _test.True(
            Contains(errors, "missing_text_trait.display_name"),
            "missing display_name should be rejected."
        );
        _test.True(
            Contains(errors, "missing_text_trait.description"),
            "missing description should be rejected."
        );
        _test.True(
            Contains(errors, "invalid_save_tags_trait.save_advantage_tags")
                && Contains(errors, "duplicates save tag poison"),
            "trait save tag 列表应拒绝重复值。"
        );
        _test.True(
            Contains(errors, "invalid_save_tags_trait.save_disadvantage_tags")
                && Contains(errors, "not_a_save_tag"),
            "trait save tag 列表应拒绝未知值。"
        );
        _test.True(
            Contains(errors, "invalid_save_tags_trait.save_immunity_tags")
                && Contains(errors, "removed suffix syntax"),
            "trait save tag 列表应拒绝旧后缀写法。"
        );
        _test.True(
            Contains(
                errors,
                "invalid_save_tags_trait.passive_status_effects[0].save_immunity_tags"
            )
                && Contains(errors, "duplicates save tag magic"),
            "trait 嵌套 passive status save tag 列表也应复用重复值校验。"
        );
    }

    private static bool Contains(IEnumerable<string> errors, string needle)
    {
        foreach (string error in errors)
        {
            if ((error ?? "").Contains(needle))
                return true;
        }
        return false;
    }

    private void AssertTraitRuntimePolicy(
        IReadOnlyDictionary<StringName, TraitDefinition> traitDefs,
        StringName traitId,
        TraitTriggerKind triggerKind,
        TraitChargeScopeKind chargeScope,
        TraitChargeResetTimingKind chargeResetTiming
    )
    {
        TraitDefinition traitDef = FindTrait(traitDefs, traitId);
        _test.True(traitDef != null, $"{traitId} should exist.");
        if (traitDef == null)
            return;
        _test.Eq(
            traitDef.EffectKind,
            TraitContentRules.ToEffectKind(traitId),
            $"{traitId} effect kind should match its generic trait id."
        );
        _test.Eq(
            traitDef.TriggerKind,
            triggerKind,
            $"{traitId} trigger kind should be runtime-ready."
        );
        _test.Eq(
            traitDef.ChargeScopeKind,
            chargeScope,
            $"{traitId} charge scope should be runtime-ready."
        );
        _test.Eq(
            traitDef.ChargeResetTimingKind,
            chargeResetTiming,
            $"{traitId} charge reset timing should be runtime-ready."
        );
    }

    private static TraitDefinition FindTrait(
        IReadOnlyDictionary<StringName, TraitDefinition> traitDefs,
        StringName traitId
    )
    {
        return traitDefs != null
            && traitDefs.TryGetValue(traitId, out TraitDefinition traitDefinition)
            ? traitDefinition
            : null;
    }

    private static List<string> ToList(IEnumerable<string> values)
    {
        List<string> result = new();
        foreach (string value in values)
            result.Add(value);
        return result;
    }

    private static string Format(IEnumerable<string> values)
    {
        List<string> result = new();
        foreach (string value in values)
            result.Add(value ?? "");
        return result.Count == 0 ? "[]" : $"[{string.Join(" | ", result)}]";
    }
}
