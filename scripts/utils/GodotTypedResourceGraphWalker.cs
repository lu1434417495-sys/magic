using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

internal static class GodotTypedResourceGraphWalker
{
    private const int MaxDepth = 32;
    private static readonly Type GenericGodotArrayType =
        typeof(Godot.Collections.Array).Assembly.GetType("Godot.Collections.IGenericGodotArray");
    private static readonly PropertyInfo GenericGodotArrayUnderlyingArrayProperty =
        GenericGodotArrayType?.GetProperty(
            "UnderlyingArray",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );

    internal static void Visit(Resource root, Action<GodotObject> visitor)
    {
        if (visitor == null)
            return;
        VisitWrappers(root, wrapper =>
        {
            if (wrapper is GodotObject godotObject)
                visitor(godotObject);
        });
    }

    internal static void VisitWrappers(Resource root, Action<object> visitor)
    {
        VisitValueGraph(root, visitor);
    }

    internal static void VisitValueGraph(object root, Action<object> visitor)
    {
        if (root == null || visitor == null)
            return;
        VisitValue(root, visitor, new HashSet<object>(GodotWrapperReferenceComparer.Instance), 0);
    }

    private static void VisitObject(
        GodotObject obj,
        Action<object> visitor,
        HashSet<object> visited,
        int depth
    )
    {
        if (obj == null || depth > MaxDepth)
            return;
        if (!visited.Add(obj))
            return;

        visitor(obj);
        VisitKnownChildren(obj, visitor, visited, depth + 1);
        VisitProjectGodotWrapperMembers(obj, visitor, visited, depth + 1);
    }

    private static void VisitKnownChildren(
        GodotObject obj,
        Action<object> visitor,
        HashSet<object> visited,
        int depth
    )
    {
        if (depth > MaxDepth)
            return;

        switch (obj)
        {
            case SkillDef skill:
                VisitValue(skill.combat_profile, visitor, visited, depth);
                VisitValue(skill.contingency_automation_profile, visitor, visited, depth);
                VisitValue(skill.AttributeModifiersTyped, visitor, visited, depth);
                break;
            case CombatSkillDef combat:
                VisitValue(combat.level_overrides, visitor, visited, depth);
                VisitValue(combat.effect_defs, visitor, visited, depth);
                VisitValue(combat.passive_effect_defs, visitor, visited, depth);
                VisitValue(combat.cast_variants, visitor, visited, depth);
                break;
            case CombatCastVariantDef variant:
                VisitValue(variant.effect_defs, visitor, visited, depth);
                VisitValue(variant.@params, visitor, visited, depth);
                break;
            case CombatEffectDef effect:
                VisitValue(effect.effect_categories, visitor, visited, depth);
                VisitValue(effect.@params, visitor, visited, depth);
                break;
            case EnemyContentSeed seed:
                VisitValue(seed.enemy_ai_brains, visitor, visited, depth);
                VisitValue(seed.enemy_templates, visitor, visited, depth);
                VisitValue(seed.wild_encounter_rosters, visitor, visited, depth);
                break;
            case EnemyAiBrainDef brain:
                VisitValue(brain.score_profile, visitor, visited, depth);
                VisitValue(brain.states, visitor, visited, depth);
                VisitValue(brain.transition_rules, visitor, visited, depth);
                break;
            case EnemyAiStateDef state:
                VisitValue(state.actions, visitor, visited, depth);
                VisitValue(state.generation_slots, visitor, visited, depth);
                break;
            case EnemyAiTransitionRuleDef rule:
                VisitValue(rule.from_state_ids, visitor, visited, depth);
                VisitValue(rule.conditions, visitor, visited, depth);
                break;
            case EnemyAiGenerationSlotDef slot:
                VisitValue(slot.allowed_affordances, visitor, visited, depth);
                VisitValue(slot.action_families, visitor, visited, depth);
                break;
            case EnemyTemplateDef template:
                VisitValue(template.tags, visitor, visited, depth);
                VisitValue(template.save_advantage_tags, visitor, visited, depth);
                VisitValue(template.base_attribute_overrides, visitor, visited, depth);
                VisitValue(template.skill_ids, visitor, visited, depth);
                VisitValue(template.skill_level_map, visitor, visited, depth);
                VisitValue(template.attribute_overrides, visitor, visited, depth);
                VisitObject(template.battle_sprite_texture, visitor, visited, depth);
                VisitValue(template.drop_entries, visitor, visited, depth);
                break;
            case WildEncounterRosterDef roster:
                VisitValue(roster.stages, visitor, visited, depth);
                break;
            case WildEncounterRosterStageDef stage:
                VisitValue(stage.unit_entries, visitor, visited, depth);
                break;
            case ItemDef item:
                VisitValue(item.tags, visitor, visited, depth);
                VisitValue(item.crafting_groups, visitor, visited, depth);
                VisitValue(item.quest_groups, visitor, visited, depth);
                VisitValue(item.trait_ids, visitor, visited, depth);
                VisitValue(item.trait_roll_groups, visitor, visited, depth);
                VisitValue(item.equipment_slot_ids, visitor, visited, depth);
                VisitValue(item.attribute_modifiers, visitor, visited, depth);
                VisitValue(item.occupied_slot_ids, visitor, visited, depth);
                VisitObject(item.equip_requirement, visitor, visited, depth);
                VisitObject(item.weapon_profile, visitor, visited, depth);
                break;
            case WeaponProfileDef weapon:
                VisitValue(weapon.properties, visitor, visited, depth);
                VisitObject(weapon.one_handed_dice, visitor, visited, depth);
                VisitObject(weapon.two_handed_dice, visitor, visited, depth);
                break;
            case TraitRollGroupDef traitRollGroup:
                VisitValue(traitRollGroup.entries, visitor, visited, depth);
                break;
            case BattleSpecialProfileManifest manifest:
                VisitObject(manifest.profile_resource, visitor, visited, depth);
                VisitValue(manifest.presentation_metadata, visitor, visited, depth);
                break;
            case MeteorSwarmProfile meteor:
                VisitValue(meteor.impact_components, visitor, visited, depth);
                break;
            case BarrierProfileDef barrier:
                VisitValue(barrier.layers, visitor, visited, depth);
                break;
            case BarrierLayerDef layer:
                VisitValue(layer.passage_outcomes, visitor, visited, depth);
                break;
            case WorldMapGenerationConfig worldConfig:
                VisitValue(worldConfig.settlement_library, visitor, visited, depth);
                VisitValue(worldConfig.facility_library, visitor, visited, depth);
                VisitValue(worldConfig.settlement_distribution, visitor, visited, depth);
                VisitValue(worldConfig.wild_monster_distribution, visitor, visited, depth);
                VisitValue(worldConfig.mounted_submaps, visitor, visited, depth);
                VisitValue(worldConfig.world_events, visitor, visited, depth);
                break;
            case WorldMapSettlementBundle settlementBundle:
                VisitValue(settlementBundle.settlement_library, visitor, visited, depth);
                VisitValue(settlementBundle.facility_library, visitor, visited, depth);
                break;
            case WorldMapWildSpawnBundle wildSpawnBundle:
                VisitValue(wildSpawnBundle.wild_monster_distribution, visitor, visited, depth);
                break;
            case SettlementConfig settlement:
                VisitValue(settlement.facility_slots, visitor, visited, depth);
                VisitValue(settlement.guaranteed_facility_ids, visitor, visited, depth);
                VisitValue(settlement.optional_facility_pool, visitor, visited, depth);
                break;
            case FacilityConfig facility:
                VisitValue(facility.allowed_slot_tags, visitor, visited, depth);
                VisitValue(facility.bound_service_npcs, visitor, visited, depth);
                break;
            case WildSpawnRule wildSpawnRule:
                VisitValue(wildSpawnRule.chunk_coords, visitor, visited, depth);
                break;
            case WorldMapSettlementNamePool namePool:
                VisitValue(namePool.settlement_display_names, visitor, visited, depth);
                break;
        }
    }

    private static void VisitValue(
        object value,
        Action<object> visitor,
        HashSet<object> visited,
        int depth
    )
    {
        if (value == null || depth > MaxDepth)
            return;

        if (value is Variant variant)
        {
            VisitVariant(variant, visitor, visited, depth + 1);
            return;
        }

        if (value is GodotObject godotObject)
        {
            VisitObject(godotObject, visitor, visited, depth + 1);
            return;
        }

        if (IsGodotCollectionWrapper(value))
        {
            if (!visited.Add(value))
                return;
            visitor(value);
            VisitCollectionItems(value, visitor, visited, depth + 1);
            return;
        }

        switch (value)
        {
            case IDictionary dictionary:
                foreach (DictionaryEntry entry in dictionary)
                {
                    VisitValue(entry.Key, visitor, visited, depth + 1);
                    VisitValue(entry.Value, visitor, visited, depth + 1);
                }
                return;
            case IEnumerable enumerable when value is not string:
                foreach (object item in enumerable)
                    VisitValue(item, visitor, visited, depth + 1);
                return;
        }

        if (ShouldReflectStructuredValue(value.GetType()))
            VisitStructuredMembers(value, visitor, visited, depth + 1);
    }

    private static void VisitCollectionItems(
        object collection,
        Action<object> visitor,
        HashSet<object> visited,
        int depth
    )
    {
        if (depth > MaxDepth)
            return;

        if (TryGetUnderlyingGodotArray(collection, out Godot.Collections.Array underlyingArray))
        {
            VisitCollectionItems(underlyingArray, visitor, visited, depth);
            return;
        }

        if (collection is GDictionary dictionary)
        {
            foreach (Variant key in dictionary.Keys)
            {
                VisitValue(key, visitor, visited, depth + 1);
                VisitValue(dictionary[key], visitor, visited, depth + 1);
            }
            return;
        }

        if (collection is Godot.Collections.Array godotArray)
        {
            for (int index = 0; index < godotArray.Count; index++)
            {
                try
                {
                    VisitValue(godotArray[index], visitor, visited, depth + 1);
                }
                catch (InvalidCastException)
                {
                    // Typed Godot arrays can contain base Resource values during validation tests.
                    // The lifecycle walker should keep draining known wrappers instead of failing
                    // through the generic IEnumerable<T> cast path.
                }
            }
            return;
        }

        if (collection is IDictionary nonGenericDictionary)
        {
            foreach (DictionaryEntry entry in nonGenericDictionary)
            {
                VisitValue(entry.Key, visitor, visited, depth + 1);
                VisitValue(entry.Value, visitor, visited, depth + 1);
            }
            return;
        }

        if (collection is IEnumerable enumerable and not string)
        {
            foreach (object item in enumerable)
                VisitValue(item, visitor, visited, depth + 1);
        }
    }

    private static void VisitProjectGodotWrapperMembers(
        GodotObject obj,
        Action<object> visitor,
        HashSet<object> visited,
        int depth
    )
    {
        if (obj == null || depth > MaxDepth)
            return;
        Type type = obj.GetType();
        if (type.Assembly != typeof(GodotTypedResourceGraphWalker).Assembly)
            return;

        VisitMembers(
            obj,
            type,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            visitor,
            visited,
            depth
        );
    }

    private static void VisitStructuredMembers(
        object value,
        Action<object> visitor,
        HashSet<object> visited,
        int depth
    )
    {
        if (value == null || depth > MaxDepth)
            return;
        Type type = value.GetType();
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;
        if (type.Assembly == typeof(GodotTypedResourceGraphWalker).Assembly)
            flags |= BindingFlags.NonPublic;
        VisitMembers(value, type, flags, visitor, visited, depth);
    }

    private static void VisitMembers(
        object source,
        Type type,
        BindingFlags flags,
        Action<object> visitor,
        HashSet<object> visited,
        int depth
    )
    {
        foreach (FieldInfo field in type.GetFields(flags))
        {
            if (field.IsStatic || !MayContainGodotWrapper(field.FieldType))
                continue;
            object value;
            try
            {
                value = field.GetValue(source);
            }
            catch (Exception)
            {
                continue;
            }
            VisitValue(value, visitor, visited, depth + 1);
        }

        // Do not inspect arbitrary property getters here. Several Godot-facing getters
        // synthesize collection wrappers on read, which would turn owner registration
        // into wrapper creation. Known exported properties are covered explicitly above.
    }

    private static bool MayContainGodotWrapper(Type type)
    {
        if (type == null || type == typeof(string) || type.IsPrimitive || type.IsEnum)
            return false;
        return typeof(GodotObject).IsAssignableFrom(type)
            || type == typeof(Variant)
            || typeof(IEnumerable).IsAssignableFrom(type)
            || IsGodotCollectionWrapperType(type)
            || ShouldReflectStructuredValue(type);
    }

    private static bool ShouldReflectStructuredValue(Type type)
    {
        if (type == null || type == typeof(string) || type.IsPrimitive || type.IsEnum)
            return false;
        if (typeof(Delegate).IsAssignableFrom(type))
            return false;
        if (type.Assembly == typeof(GodotTypedResourceGraphWalker).Assembly)
            return true;
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>);
    }

    private static void VisitVariant(
        Variant variant,
        Action<object> visitor,
        HashSet<object> visited,
        int depth
    )
    {
        switch (variant.VariantType)
        {
            case Variant.Type.Object:
                VisitObject(variant.AsGodotObject(), visitor, visited, depth + 1);
                break;
            case Variant.Type.Dictionary:
                VisitValue(variant.AsGodotDictionary(), visitor, visited, depth + 1);
                break;
            case Variant.Type.Array:
                VisitValue(variant.AsGodotArray(), visitor, visited, depth + 1);
                break;
        }
    }

    internal static bool IsGodotWrapper(object value)
    {
        return value is GodotObject || IsGodotCollectionWrapper(value);
    }

    private static bool IsGodotCollectionWrapper(object value)
    {
        return value != null && IsGodotCollectionWrapperType(value.GetType());
    }

    private static bool IsGodotCollectionWrapperType(Type type)
    {
        if (type == null)
            return false;
        if (typeof(Godot.Collections.Array).IsAssignableFrom(type))
            return true;
        if (typeof(Godot.Collections.Dictionary).IsAssignableFrom(type))
            return true;
        if (GenericGodotArrayType != null && GenericGodotArrayType.IsAssignableFrom(type))
            return true;
        string fullName = type.FullName ?? "";
        return fullName.StartsWith("Godot.Collections.Array", StringComparison.Ordinal)
            || fullName.StartsWith("Godot.Collections.Dictionary", StringComparison.Ordinal);
    }

    private static bool TryGetUnderlyingGodotArray(
        object value,
        out Godot.Collections.Array array
    )
    {
        array = null;
        if (
            value == null
            || GenericGodotArrayType == null
            || GenericGodotArrayUnderlyingArrayProperty == null
            || !GenericGodotArrayType.IsInstanceOfType(value)
        )
        {
            return false;
        }

        try
        {
            PropertyInfo concreteProperty = value
                .GetType()
                .GetProperty(
                    "Godot.Collections.IGenericGodotArray.UnderlyingArray",
                    BindingFlags.Instance | BindingFlags.NonPublic
                );
            array =
                (concreteProperty ?? GenericGodotArrayUnderlyingArrayProperty).GetValue(value)
                as Godot.Collections.Array;
            return array != null;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
