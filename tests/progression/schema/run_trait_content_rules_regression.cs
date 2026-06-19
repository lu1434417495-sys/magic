using System.Collections.Generic;
using Godot;

public partial class run_trait_content_rules_regression : SceneTree
{
    private readonly TestHarness _test = new();

    private static readonly StringName[] EffectIds =
    {
        "darkvision",
        "superior_darkvision",
        "fey_ancestry",
        "brave",
        "halfling_luck",
        "savage_attacks",
        "relentless_endurance",
        "gnome_cunning",
        "dwarven_resilience",
        "duergar_resilience",
        "human_versatility",
        "small_body",
        "fleet_of_foot",
        "dragon_breath",
        "racial_spell_grant",
        "damage_resistance",
        "save_advantage",
        "civil_militia",
        "keen_senses",
        "trance",
        "elven_weapon_training",
        "drow_weapon_training",
        "dwarven_combat_training",
        "shield_dwarf_armor_training",
        "dwarven_toughness",
        "menacing",
        "halfling_nimbleness",
        "naturally_stealthy",
        "mask_of_the_wild",
        "stonecunning",
        "forest_gnome_magic",
        "deep_gnome_camouflage",
        "artificers_lore",
        "duergar_magic",
        "githyanki_martial_prodigy",
        "astral_knowledge",
        "githyanki_psionics",
        "infernal_legacy",
        "asmodeus_legacy",
        "mephistopheles_legacy",
        "zariel_legacy",
        "drow_magic",
        "draconic_ancestry",
    };

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestEffectMappingCoversCurrentRaceTraitEffects();
        TestPolicyMappingsRejectUnknownValues();
        TestSourceKindAllowedUsesTraitDefDeclaration();
        TestRollSchemaValidation();

        Quit(_test.Finish("Trait content rules regression"));
    }

    private void TestEffectMappingCoversCurrentRaceTraitEffects()
    {
        foreach (StringName effectId in EffectIds)
        {
            TraitEffectKind kind = TraitContentRules.ToEffectKind(effectId);
            _test.True(
                kind != TraitEffectKind.Unknown,
                $"effect {effectId} should map to TraitEffectKind."
            );
            _test.Eq(
                effectId,
                TraitContentRules.ToStringName(kind),
                $"effect {effectId} should round-trip."
            );
        }
        _test.Eq(
            "",
            TraitContentRules.ToStringName(TraitEffectKind.Unknown),
            "Unknown effect should serialize to empty."
        );
        _test.False(
            TraitContentRules.IsValidEffectType("not_a_trait_effect"),
            "unknown effect string should be invalid."
        );
    }

    private void TestPolicyMappingsRejectUnknownValues()
    {
        _test.Eq(
            TraitStackPolicyKind.UniqueByTrait,
            TraitContentRules.ToStackPolicyKind("unique_by_trait"),
            "unique_by_trait stack policy."
        );
        _test.Eq(
            TraitStackPolicyKind.HighestRoll,
            TraitContentRules.ToStackPolicyKind("highest_roll"),
            "highest_roll stack policy."
        );
        _test.Eq(
            TraitStackPolicyKind.Additive,
            TraitContentRules.ToStackPolicyKind("additive"),
            "additive stack policy."
        );
        _test.Eq(
            TraitStackPolicyKind.StackByInstance,
            TraitContentRules.ToStackPolicyKind("stack_by_instance"),
            "stack_by_instance stack policy."
        );
        _test.False(
            TraitContentRules.IsValidStackPolicy("stack_everything"),
            "unknown stack policy should be invalid."
        );

        _test.Eq(
            TraitSourceKind.Identity,
            TraitContentRules.ToSourceKind("identity"),
            "identity source kind."
        );
        _test.Eq(
            TraitSourceKind.Character,
            TraitContentRules.ToSourceKind("character"),
            "character source kind."
        );
        _test.Eq(
            TraitSourceKind.EquipmentFixed,
            TraitContentRules.ToSourceKind("equipment_fixed"),
            "equipment_fixed source kind."
        );
        _test.Eq(
            TraitSourceKind.EquipmentRoll,
            TraitContentRules.ToSourceKind("equipment_roll"),
            "equipment_roll source kind."
        );
        _test.False(
            TraitContentRules.IsValidSourceType("loot_table"),
            "unknown source kind should be invalid."
        );

        _test.Eq(
            TraitChargeScopeKind.None,
            TraitContentRules.ToChargeScopeKind("none"),
            "none charge scope."
        );
        _test.Eq(
            TraitChargeScopeKind.PerTurn,
            TraitContentRules.ToChargeScopeKind("per_turn"),
            "per_turn charge scope."
        );
        _test.Eq(
            TraitChargeScopeKind.PerBattle,
            TraitContentRules.ToChargeScopeKind("per_battle"),
            "per_battle charge scope."
        );
        _test.False(
            TraitContentRules.IsValidChargeScope("per_scene"),
            "unknown charge scope should be invalid."
        );

        _test.Eq(
            TraitChargeResetTimingKind.None,
            TraitContentRules.ToChargeResetTimingKind("none"),
            "none reset timing."
        );
        _test.Eq(
            TraitChargeResetTimingKind.BattleStart,
            TraitContentRules.ToChargeResetTimingKind("battle_start"),
            "battle_start reset timing."
        );
        _test.Eq(
            TraitChargeResetTimingKind.TurnStart,
            TraitContentRules.ToChargeResetTimingKind("turn_start"),
            "turn_start reset timing."
        );
        _test.False(
            TraitContentRules.IsValidChargeResetTiming("round_start"),
            "unknown reset timing should be invalid."
        );

        _test.Eq(
            TraitRollValueType.Int,
            TraitContentRules.ToRollValueType("int"),
            "int roll type."
        );
        _test.Eq(
            TraitRollValueType.StringName,
            TraitContentRules.ToRollValueType("string_name"),
            "string_name roll type."
        );
        _test.Eq(
            TraitRollValueType.Bool,
            TraitContentRules.ToRollValueType("bool"),
            "bool roll type."
        );
        _test.False(
            TraitContentRules.IsValidRollValueType("float"),
            "unknown roll type should be invalid."
        );
    }

    private void TestSourceKindAllowedUsesTraitDefDeclaration()
    {
        TraitDef def = new();
        def.allowed_source_kinds.Add("identity");
        def.allowed_source_kinds.Add("equipment_roll");

        _test.True(
            TraitContentRules.IsSourceKindAllowed(def, TraitSourceKind.Identity),
            "identity source should be allowed."
        );
        _test.True(
            TraitContentRules.IsSourceKindAllowed(def, TraitSourceKind.EquipmentRoll),
            "equipment_roll source should be allowed."
        );
        _test.False(
            TraitContentRules.IsSourceKindAllowed(def, TraitSourceKind.Character),
            "character source should not be allowed."
        );
        _test.False(
            TraitContentRules.IsSourceKindAllowed(def, TraitSourceKind.Unknown),
            "unknown source should not be allowed."
        );
        _test.False(
            TraitContentRules.IsSourceKindAllowed(null, TraitSourceKind.Identity),
            "null trait def should not allow any source."
        );
    }

    private void TestRollSchemaValidation()
    {
        List<string> errors = new();
        TraitRollValueSchemaEntry badInt = new()
        {
            key = "amount",
            value_type = "int",
            min_value = 5,
            max_value = 3,
        };
        badInt.AppendSchemaErrors(errors, "Trait test_trait");
        _test.True(
            errors.Count == 1 && errors[0].Contains("min_value"),
            "invalid int range should report an error."
        );

        errors.Clear();
        TraitRollValueSchemaEntry badStringName = new()
        {
            key = "damage_tag",
            value_type = "string_name",
        };
        badStringName.AppendSchemaErrors(errors, "Trait test_trait");
        _test.True(
            errors.Count == 1 && errors[0].Contains("allowed_values"),
            "string_name roll needs allowed values."
        );
    }
}
