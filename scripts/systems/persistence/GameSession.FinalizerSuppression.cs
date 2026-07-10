using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GDictionaryArray = Godot.Collections.Array<Godot.Collections.Dictionary>;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

// Partial slice of GameSession — Dispose graph + Godot-object finalizer suppression for shutdown/drain.
// Pure physical split: same class, no behavior change. See GameSession.cs.
public partial class GameSession
{

    private static void DisposeCapturedPartyState(GDictionary runtimeState)
    {
        if (runtimeState == null || !runtimeState.ContainsKey("party_state"))
            return;
        if (PartyState.TryReadPartyPayload(runtimeState["party_state"], out PartyState partyState))
            DisposePartyStateGraph(partyState);
        runtimeState["party_state"] = default(Variant);
    }

    private static void DisposePartyStateGraph(
        PartyState state,
        PartyState preservedState = null
    )
    {
        if (state == null || ReferenceEquals(state, preservedState))
            return;

        var disposed = new HashSet<GodotObject>();
        foreach (PartyMemberState memberState in state.GetMemberStates())
            DisposePartyMemberStateGraph(memberState, disposed);
        DisposeWarehouseStateGraph(state.warehouse_state, disposed);
        state.member_states.Clear();
        state.active_member_ids.Clear();
        state.reserve_member_ids.Clear();
        state.pending_character_rewards.Clear();
        state.active_quests.Clear();
        state.claimable_quests.Clear();
        state.completed_quest_ids.Clear();
        RuntimeStateLifecycle.MarkValueGraphFinalizerless(state, "GameSession.DisposePartyStateGraph");
    }

    private static void DisposePartyMemberStateGraph(
        PartyMemberState memberState,
        HashSet<GodotObject> disposed
    )
    {
        if (memberState == null)
            return;
        DisposeUnitProgressGraph(memberState.progression, disposed);
        DisposeEquipmentStateGraph(memberState.equipment_state, disposed);
        foreach (
            ContingencyMatrixSetupState setup in memberState.ReleaseContingencySetupsForDispose()
        )
        {
            DisposeContingencySetupGraph(setup, disposed);
        }
        memberState.trait_instances.Clear();
        memberState.active_stage_advancement_modifier_ids.Clear();
        RuntimeStateLifecycle.MarkValueGraphFinalizerless(
            memberState,
            "GameSession.DisposePartyMemberStateGraph"
        );
    }

    private static void DisposeContingencySetupGraph(
        ContingencyMatrixSetupState setup,
        HashSet<GodotObject> disposed
    )
    {
        // Contingency setup state is plain C#; this method remains as the
        // ownership boundary for callers that release setup lists during teardown.
    }

    private static void DisposeUnitProgressGraph(
        UnitProgress progress,
        HashSet<GodotObject> disposed
    )
    {
        if (progress == null)
            return;
        foreach (UnitProfessionProgress professionProgress in progress.ProfessionsTyped.Values)
            DisposeProfessionProgressGraph(professionProgress, disposed);
        progress.SetPendingProfessionChoices(null);
    }

    private static void DisposeProfessionProgressGraph(
        UnitProfessionProgress professionProgress,
        HashSet<GodotObject> disposed
    )
    {
        if (professionProgress == null)
            return;
        professionProgress.promotion_history.Clear();
        professionProgress.core_skill_ids.Clear();
        professionProgress.granted_skill_ids.Clear();
    }

    private static void DisposeEquipmentStateGraph(
        EquipmentState equipmentState,
        HashSet<GodotObject> disposed
    )
    {
        if (equipmentState == null)
            return;
        foreach (StringName entrySlotId in equipmentState.GetEntrySlotIdsTyped())
            equipmentState.ClearEntrySlot(entrySlotId);
    }

    private static void DisposeWarehouseStateGraph(
        WarehouseState warehouseState,
        HashSet<GodotObject> disposed
    )
    {
        if (warehouseState == null)
            return;
        warehouseState.stacks.Clear();
        warehouseState.equipment_instances.Clear();
    }

    private static void RegisterStaticContentOwnership(Resource resource)
    {
        if (resource == null)
            return;
        if (!string.IsNullOrEmpty(resource.ResourcePath))
        {
            GodotContentOwnership.RegisterBorrowedContent(
                resource,
                $"GameSession.static-content:{resource.ResourcePath}"
            );
            return;
        }

        GodotContentOwnership.RegisterDerivedContent(
            resource,
            $"GameSession.pathless-static:{resource.GetType().Name}:{resource.GetInstanceId()}",
            "GameSession.RegisterStaticContentOwnership"
        );
    }

    private bool IsSessionInTree()
    {
        try
        {
            return GodotObject.IsInstanceValid(this) && IsInsideTree();
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    private void SuppressOwnedContentFinalizerGraphsForShutdown(
        HashSet<GodotObject> visited
    )
    {
        if (visited == null)
            return;
        SuppressResourceValues(
            _progression_content_registry?.GetLoadedSkillResourcesForFinalizerDrain(),
            visited
        );
        SuppressResourceValues(_progression_content_registry?.GetProfessionDefsTyped(), visited);
        SuppressResourceValues(_progression_content_registry?.GetAchievementDefsTyped(), visited);
        SuppressResourceValues(_progression_content_registry?.GetQuestDefsTyped(), visited);
        SuppressResourceValues(
            _progression_content_registry?.GetContingencySetupTemplatesTyped(),
            visited
        );
        SuppressResourceValues(_progression_content_registry?.GetRaceDefsTyped(), visited);
        SuppressResourceValues(_progression_content_registry?.GetSubraceDefsTyped(), visited);
        SuppressResourceValues(_progression_content_registry?.GetTraitDefsTyped(), visited);
        SuppressResourceValues(_progression_content_registry?.GetAgeProfileDefsTyped(), visited);
        SuppressResourceValues(_progression_content_registry?.GetBloodlineDefsTyped(), visited);
        SuppressResourceValues(_progression_content_registry?.GetBloodlineStageDefsTyped(), visited);
        SuppressResourceValues(_progression_content_registry?.GetAscensionDefsTyped(), visited);
        SuppressResourceValues(_progression_content_registry?.GetAscensionStageDefsTyped(), visited);
        SuppressResourceValues(
            _progression_content_registry?.GetStageAdvancementDefsTyped(),
            visited
        );
        SuppressResourceValues(_item_content_registry?.GetItemDefsTyped(), visited);
        SuppressResourceValues(_recipe_content_registry?.GetRecipeDefsTyped(), visited);
        SuppressResourceValues(_enemy_content_registry?.GetEnemyTemplatesTyped(), visited);
        SuppressResourceValues(_enemy_content_registry?.GetEnemyAiBrainsTyped(), visited);
        SuppressResourceValues(_enemy_content_registry?.GetWildEncounterRostersTyped(), visited);
        SuppressResourceValues(_battle_special_profile_registry?.GetManifestsTyped(), visited);
        WorldMapContentValidator.SuppressCachedResourceFinalizersForShutdown();
    }

    internal void SuppressContentFinalizersForFinalizerDrain()
    {
        var visited = new HashSet<GodotObject>();
        SuppressOwnedContentFinalizerGraphsForShutdown(visited);
        SuppressGodotObjectFinalizerGraph(_generation_config, visited);
        SuppressResourceDictionaryProjectionFinalizers(_profession_defs, visited);
        SuppressResourceDictionaryProjectionFinalizers(_achievement_defs, visited);
        SuppressResourceDictionaryProjectionFinalizers(_quest_defs, visited);
        SuppressResourceDictionaryProjectionFinalizers(_item_defs, visited);
        SuppressResourceDictionaryProjectionFinalizers(_recipe_defs, visited);
        SuppressResourceDictionaryProjectionFinalizers(_enemy_templates, visited);
        SuppressResourceDictionaryProjectionFinalizers(_enemy_ai_brains, visited);
        SuppressResourceDictionaryProjectionFinalizers(_wild_encounter_rosters, visited);
    }

    private static void SuppressResourceValues<T>(
        IReadOnlyDictionary<StringName, T> resources,
        HashSet<GodotObject> visited
    )
    {
        if (resources == null || visited == null)
            return;
        foreach (T contentDef in resources.Values)
        {
            if (contentDef is GodotObject godotObject)
                SuppressGodotObjectFinalizerGraph(godotObject, visited);
        }
    }

    private static void SuppressResourceValues(
        IEnumerable<Resource> resources,
        HashSet<GodotObject> visited
    )
    {
        if (resources == null || visited == null)
            return;
        foreach (Resource contentDef in resources)
        {
            if (contentDef != null)
                SuppressGodotObjectFinalizerGraph(contentDef, visited);
        }
    }

    private void ClearSessionGodotObjectReferences()
    {
        _generation_config = null;
        ClearResourceDictionaryProjection(_profession_defs);
        ClearResourceDictionaryProjection(_achievement_defs);
        ClearResourceDictionaryProjection(_quest_defs);
        ClearResourceDictionaryProjection(_item_defs);
        ClearResourceDictionaryProjection(_recipe_defs);
        ClearResourceDictionaryProjection(_enemy_templates);
        ClearResourceDictionaryProjection(_enemy_ai_brains);
        ClearResourceDictionaryProjection(_wild_encounter_rosters);
        _skillDefinitionIndex.Clear();
        _profession_defs = new GDictionary();
        _achievement_defs = new GDictionary();
        _quest_defs = new GDictionary();
        _item_defs = new GDictionary();
        _recipe_defs = new GDictionary();
        _enemy_templates = new GDictionary();
        _enemy_ai_brains = new GDictionary();
        _wild_encounter_rosters = new GDictionary();
        _contentValidationSnapshotData = new ContentValidationSnapshotData();
        _professionDefIndex.Clear();
        _achievementDefIndex.Clear();
        _questDefIndex.Clear();
        _itemDefIndex.Clear();
        _recipeDefIndex.Clear();
        _enemyTemplateIndex.Clear();
        _enemyAiBrainIndex.Clear();
        _wildEncounterRosterIndex.Clear();
    }

    private static void ClearResourceDictionaryProjection(GDictionary projection)
    {
        if (projection == null)
            return;
        RegisterContentProjectionWrapper(
            projection,
            "GameSession.ClearResourceDictionaryProjection"
        );
        projection.Clear();
    }

    private static void SuppressResourceDictionaryProjectionFinalizers(
        GDictionary projection,
        HashSet<GodotObject> finalizerSuppressionVisited
    )
    {
        if (projection == null)
            return;
        RegisterContentProjectionWrapper(
            projection,
            "GameSession.SuppressResourceDictionaryProjectionFinalizers"
        );
        if (finalizerSuppressionVisited == null)
            return;
        foreach (Variant key in projection.Keys)
            SuppressGodotObjectFinalizersInValue(
                projection[key],
                finalizerSuppressionVisited,
                0
            );
    }

    private static GDictionary RegisterContentProjectionWrapper(GDictionary projection, string reason)
    {
        if (projection == null)
            return new GDictionary();
        GodotContentOwnership.RegisterDerivedWrapper(
            projection,
            $"GameSession.content_projection:{RuntimeHelpers.GetHashCode(projection)}",
            reason
        );
        return projection;
    }

    private static void SuppressGodotObjectFinalizerGraph(
        GodotObject root,
        HashSet<GodotObject> visited
    )
    {
        SuppressGodotObjectFinalizerGraph(root, visited, 0);
    }

    private static void SuppressGodotObjectFinalizerGraph(
        GodotObject root,
        HashSet<GodotObject> visited,
        int depth
    )
    {
        if (root == null || visited == null || depth > 8)
            return;
        try
        {
            if (!GodotObject.IsInstanceValid(root))
                return;
        }
        catch (ObjectDisposedException)
        {
            return;
        }
        if (!visited.Add(root))
            return;

        GC.SuppressFinalize(root);
        GC.KeepAlive(root);
        if (depth >= 8)
            return;

        const System.Reflection.BindingFlags flags =
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public;
        Type type = root.GetType();
        foreach (System.Reflection.FieldInfo field in type.GetFields(flags))
        {
            if (field.IsStatic || !MayContainGodotObject(field.FieldType))
                continue;
            object value;
            try
            {
                value = field.GetValue(root);
            }
            catch (Exception)
            {
                continue;
            }
            SuppressGodotObjectFinalizersInValue(value, visited, depth + 1);
        }
        foreach (System.Reflection.PropertyInfo property in type.GetProperties(flags))
        {
            if (
                !property.CanRead
                || property.GetIndexParameters().Length > 0
                || !MayContainGodotObject(property.PropertyType)
            )
                continue;
            object value;
            try
            {
                value = property.GetValue(root);
            }
            catch (Exception)
            {
                continue;
            }
            SuppressGodotObjectFinalizersInValue(value, visited, depth + 1);
        }
    }

    private static bool MayContainGodotObject(Type type)
    {
        if (type == null || type == typeof(string))
            return false;
        return typeof(GodotObject).IsAssignableFrom(type)
            || type == typeof(Variant)
            || typeof(System.Collections.IEnumerable).IsAssignableFrom(type);
    }

    private static void SuppressGodotObjectFinalizersInValue(
        object value,
        HashSet<GodotObject> visited,
        int depth
    )
    {
        if (value == null || depth > 8)
            return;
        switch (value)
        {
            case GodotObject godotObject:
                SuppressGodotObjectFinalizerGraph(godotObject, visited, depth);
                return;
            case Variant variant:
                SuppressGodotObjectFinalizersInVariant(variant, visited, depth);
                return;
            case GDictionary dictionary:
                foreach (Variant key in dictionary.Keys)
                    SuppressGodotObjectFinalizersInValue(dictionary[key], visited, depth + 1);
                return;
            case System.Collections.IEnumerable enumerable when value is not string:
                foreach (object entry in enumerable)
                    SuppressGodotObjectFinalizersInValue(entry, visited, depth + 1);
                return;
        }
    }

    private static void SuppressGodotObjectFinalizersInVariant(
        Variant value,
        HashSet<GodotObject> visited,
        int depth
    )
    {
        switch (value.VariantType)
        {
            case Variant.Type.Object:
                SuppressGodotObjectFinalizerGraph(value.AsGodotObject(), visited, depth + 1);
                break;
            case Variant.Type.Dictionary:
                SuppressGodotObjectFinalizersInValue(value.AsGodotDictionary(), visited, depth + 1);
                break;
            case Variant.Type.Array:
                SuppressGodotObjectFinalizersInValue(value.AsGodotArray(), visited, depth + 1);
                break;
        }
    }
}
