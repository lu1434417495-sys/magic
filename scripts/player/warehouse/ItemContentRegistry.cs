using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class ItemContentRegistry : RefCounted
{
    private const string ItemConfigDirectory = "res://data/configs/items";
    private const string ItemTemplateDirectory = "res://data/configs/items_templates";

    private static bool _defaultSnapshotBuilt;
    private static readonly Dictionary<StringName, ItemDef> _defaultItemDefs = new();
    private static readonly List<string> _defaultValidationErrors = new();

    private readonly Dictionary<StringName, ItemDef> _itemDefs = new();
    private readonly Dictionary<StringName, ItemDef> _templateDefs = new();
    private readonly Dictionary<StringName, ItemDef> _resolvedTemplateCache = new();
    private readonly List<string> _validationErrors = new();
    private bool _hasBuilt;

    public ItemContentRegistry()
    {
    }

    public ItemContentRegistry(bool autoRebuild)
    {
    }

    public void rebuild()
    {
        EnsureDefaultSnapshotBuilt();
        _itemDefs.Clear();
        _templateDefs.Clear();
        _resolvedTemplateCache.Clear();
        _validationErrors.Clear();
        foreach (var entry in _defaultItemDefs)
            _itemDefs[entry.Key] = entry.Value;
        foreach (string error in _defaultValidationErrors)
            _validationErrors.Add(error);
        _hasBuilt = true;
    }

    public void rebuild_from_directories(Godot.Collections.Array itemDirectories, Godot.Collections.Array templateDirectories)
    {
        _itemDefs.Clear();
        _templateDefs.Clear();
        _resolvedTemplateCache.Clear();
        _validationErrors.Clear();
        _hasBuilt = true;

        foreach (var templateDirectory in templateDirectories)
        {
            string templatePath = templateDirectory.AsString();
            if (string.IsNullOrEmpty(templatePath))
                continue;
            ScanTemplateDirectory(templatePath);
        }

        ResolveAllTemplates();

        foreach (var itemDirectory in itemDirectories)
        {
            string itemPath = itemDirectory.AsString();
            if (string.IsNullOrEmpty(itemPath))
                continue;
            ScanDirectory(itemPath);
        }
    }

    public Godot.Collections.Dictionary get_item_defs()
    {
        EnsureBuilt();
        var result = new Godot.Collections.Dictionary();
        foreach (var entry in _itemDefs)
            result[entry.Key] = entry.Value;
        return result;
    }

    public Godot.Collections.Array<string> validate()
    {
        EnsureBuilt();
        var result = new Godot.Collections.Array<string>();
        foreach (string error in _validationErrors)
            result.Add(error);
        return result;
    }

    private void EnsureBuilt()
    {
        if (!_hasBuilt)
            rebuild();
    }

    private static void EnsureDefaultSnapshotBuilt()
    {
        if (_defaultSnapshotBuilt)
            return;

        var registry = new ItemContentRegistry(false);
        registry.rebuild_from_directories(
            new Godot.Collections.Array { ItemConfigDirectory },
            new Godot.Collections.Array { ItemTemplateDirectory }
        );

        _defaultItemDefs.Clear();
        foreach (var entry in registry._itemDefs)
            _defaultItemDefs[entry.Key] = entry.Value;

        _defaultValidationErrors.Clear();
        foreach (string error in registry._validationErrors)
            _defaultValidationErrors.Add(error);

        _defaultSnapshotBuilt = true;
    }

    private void ScanTemplateDirectory(string directoryPath)
    {
        if (!DirAccess.DirExistsAbsolute(ProjectSettings.GlobalizePath(directoryPath)))
            return;

        using var directory = DirAccess.Open(directoryPath);
        if (directory == null)
        {
            _validationErrors.Add($"ItemContentRegistry could not open templates {directoryPath}.");
            return;
        }

        directory.ListDirBegin();
        while (true)
        {
            string entryName = directory.GetNext();
            if (string.IsNullOrEmpty(entryName))
                break;
            if (entryName == "." || entryName == "..")
                continue;

            string entryPath = $"{directoryPath}/{entryName}";
            if (directory.CurrentIsDir())
            {
                ScanTemplateDirectory(entryPath);
                continue;
            }
            if (!entryName.EndsWith(".tres") && !entryName.EndsWith(".res"))
                continue;
            RegisterTemplateResource(entryPath);
        }
        directory.ListDirEnd();
    }

    private void RegisterTemplateResource(string resourcePath)
    {
        var resource = ResourceLoader.Load<Resource>(resourcePath);
        if (resource == null)
        {
            _validationErrors.Add($"Failed to load item template {resourcePath}.");
            return;
        }
        if (resource is not ItemDef templateDef)
        {
            _validationErrors.Add($"Item template {resourcePath} is not an ItemDef.");
            return;
        }

        if (templateDef.item_id == "")
        {
            _validationErrors.Add($"Item template {resourcePath} is missing item_id.");
            return;
        }
        if (_templateDefs.ContainsKey(templateDef.item_id))
        {
            _validationErrors.Add($"Duplicate item template id: {(string)templateDef.item_id}");
            return;
        }
        _templateDefs[templateDef.item_id] = templateDef;
    }

    private void ResolveAllTemplates()
    {
        foreach (var entry in _templateDefs)
        {
            if (_resolvedTemplateCache.ContainsKey(entry.Key))
                continue;
            var resolved = ResolveWithTemplateChain(entry.Value, _templateDefs, new List<StringName>(), _resolvedTemplateCache, _validationErrors);
            if (resolved != null)
                _resolvedTemplateCache[entry.Key] = resolved;
        }
    }

    private void ScanDirectory(string directoryPath)
    {
        if (!DirAccess.DirExistsAbsolute(ProjectSettings.GlobalizePath(directoryPath)))
        {
            _validationErrors.Add($"ItemContentRegistry could not find {directoryPath}.");
            return;
        }

        using var directory = DirAccess.Open(directoryPath);
        if (directory == null)
        {
            _validationErrors.Add($"ItemContentRegistry could not open {directoryPath}.");
            return;
        }

        directory.ListDirBegin();
        while (true)
        {
            string entryName = directory.GetNext();
            if (string.IsNullOrEmpty(entryName))
                break;
            if (entryName == "." || entryName == "..")
                continue;

            string entryPath = $"{directoryPath}/{entryName}";
            if (directory.CurrentIsDir())
            {
                ScanDirectory(entryPath);
                continue;
            }
            if (!entryName.EndsWith(".tres") && !entryName.EndsWith(".res"))
                continue;
            RegisterItemResource(entryPath);
        }
        directory.ListDirEnd();
    }

    private void RegisterItemResource(string resourcePath)
    {
        var resource = ResourceLoader.Load<Resource>(resourcePath);
        if (resource == null)
        {
            _validationErrors.Add($"Failed to load item config {resourcePath}.");
            return;
        }
        if (resource is not ItemDef rawDef)
        {
            _validationErrors.Add($"Item config {resourcePath} is not an ItemDef.");
            return;
        }

        if (rawDef.item_id == "")
        {
            _validationErrors.Add($"Item config {resourcePath} is missing item_id.");
            return;
        }
        if (_templateDefs.ContainsKey(rawDef.item_id))
        {
            _validationErrors.Add($"Item config {resourcePath} reuses template id {(string)rawDef.item_id}; templates and instances must use distinct ids.");
            return;
        }

        var itemDef = ResolveWithTemplateChain(rawDef, _templateDefs, new List<StringName>(), _resolvedTemplateCache, _validationErrors);
        if (itemDef == null)
            return;

        var itemTags = itemDef.get_tags();
        var itemCraftingGroups = itemDef.get_crafting_groups();
        if (_itemDefs.ContainsKey(itemDef.item_id))
        {
            _validationErrors.Add($"Duplicate item_id registered: {(string)itemDef.item_id}");
            return;
        }

        var rawCategory = itemDef.item_category;
        if (rawCategory != "" && !ItemDef.get_valid_item_categories().Contains(rawCategory))
        {
            _validationErrors.Add($"Item {(string)itemDef.item_id} declares unsupported item_category {(string)rawCategory}; expected one of misc / equipment / skill_book.");
            return;
        }
        if (itemDef.is_stackable && itemDef.max_stack <= 0)
        {
            _validationErrors.Add($"Item {(string)itemDef.item_id} must have max_stack >= 1.");
            return;
        }
        if (itemDef.base_price > 0 && itemDef.sellable && itemDef.buy_price <= 0)
        {
            _validationErrors.Add($"Sellable item {(string)itemDef.item_id} must declare explicit buy_price.");
            return;
        }
        if (itemDef.base_price > 0 && itemDef.sellable && itemDef.sell_price <= 0)
        {
            _validationErrors.Add($"Sellable item {(string)itemDef.item_id} must declare explicit sell_price.");
            return;
        }
        if (itemTags.Contains(new StringName("material")) && itemCraftingGroups.Count == 0)
        {
            _validationErrors.Add($"Material item {(string)itemDef.item_id} must declare at least one crafting_group.");
            return;
        }
        if (itemTags.Contains(new StringName("quest_item")) && itemDef.get_quest_groups().Count == 0)
        {
            _validationErrors.Add($"Quest item {(string)itemDef.item_id} must declare at least one quest_group.");
            return;
        }
        if (itemDef.get_item_category_normalized() == new StringName("skill_book") && itemDef.granted_skill_id == "")
        {
            _validationErrors.Add($"Skill book item {(string)itemDef.item_id} must declare granted_skill_id.");
            return;
        }

        if (itemDef.has_equipment_category())
        {
            if (itemDef.is_stackable || itemDef.get_effective_max_stack() != 1)
            {
                _validationErrors.Add($"Equipment item {(string)itemDef.item_id} must be non-stackable.");
                return;
            }
            if (itemDef.equipment_slot_ids.Count == 0)
            {
                _validationErrors.Add($"Equipment item {(string)itemDef.item_id} must declare at least one slot.");
                return;
            }
            foreach (string rawSlotId in itemDef.equipment_slot_ids)
            {
                if (EquipmentRules.is_valid_slot(ProgressionDataUtils.to_string_name(Variant.From(rawSlotId))))
                    continue;
                _validationErrors.Add($"Equipment item {(string)itemDef.item_id} declares invalid slot {rawSlotId}.");
                return;
            }
            if (!itemDef.has_valid_equipment_type())
            {
                _validationErrors.Add($"Equipment item {(string)itemDef.item_id} must declare equipment_type_id as weapon, armor, or accessory.");
                return;
            }
            if (itemDef.is_weapon() && !ValidateWeaponProfile(itemDef, itemTags))
                return;

            for (int modifierIndex = 0; modifierIndex < itemDef.attribute_modifiers.Count; modifierIndex++)
            {
                var modifier = itemDef.attribute_modifiers[modifierIndex];
                string modifierLabel = $"Item {(string)itemDef.item_id} attribute_modifiers[{modifierIndex}]";
                if (modifier == null)
                {
                    _validationErrors.Add($"{modifierLabel} must be an AttributeModifier (got null).");
                    return;
                }
                if (modifier.attribute_id == "")
                {
                    _validationErrors.Add($"{modifierLabel}.attribute_id must be non-empty.");
                    return;
                }
                if (!AttributeModifier.is_valid_mode(Variant.From(modifier.mode)))
                {
                    _validationErrors.Add($"{modifierLabel}.mode uses unsupported value {(string)modifier.mode}.");
                    return;
                }
            }

            if (itemDef.occupied_slot_ids.Count > 0)
            {
                if (itemDef.equipment_slot_ids.Count != 1)
                {
                    _validationErrors.Add($"Equipment item {(string)itemDef.item_id} declares occupied_slot_ids but equipment_slot_ids must be exactly 1 entry slot.");
                    return;
                }

                var seenOccupied = new HashSet<StringName>();
                var entrySlotId = ProgressionDataUtils.to_string_name(Variant.From(itemDef.equipment_slot_ids[0]));
                bool containsEntrySlot = false;
                foreach (string rawSlotId in itemDef.occupied_slot_ids)
                {
                    var slotId = ProgressionDataUtils.to_string_name(Variant.From(rawSlotId));
                    if (!EquipmentRules.is_valid_slot(slotId))
                    {
                        _validationErrors.Add($"Equipment item {(string)itemDef.item_id} declares invalid occupied_slot {rawSlotId}.");
                        return;
                    }
                    if (seenOccupied.Contains(slotId))
                    {
                        _validationErrors.Add($"Equipment item {(string)itemDef.item_id} declares duplicate occupied_slot {(string)slotId}.");
                        return;
                    }
                    seenOccupied.Add(slotId);
                    if (slotId == entrySlotId)
                        containsEntrySlot = true;
                }
                if (!containsEntrySlot)
                {
                    _validationErrors.Add($"Equipment item {(string)itemDef.item_id} occupied_slot_ids must include the entry_slot {(string)entrySlotId}.");
                    return;
                }
            }
        }

        _itemDefs[itemDef.item_id] = itemDef;
    }

    private bool ValidateWeaponProfile(ItemDef itemDef, Godot.Collections.Array<StringName> itemTags)
    {
        var resolvedProfile = GetWeaponProfile(itemDef);
        if (itemDef.weapon_profile == null)
        {
            _validationErrors.Add($"Weapon item {(string)itemDef.item_id} must declare weapon_profile.");
            return false;
        }
        if (resolvedProfile == null)
        {
            _validationErrors.Add($"Weapon item {(string)itemDef.item_id} must declare weapon_profile as WeaponProfileDef.");
            return false;
        }
        if (resolvedProfile.weapon_type_id == "")
        {
            _validationErrors.Add($"Weapon item {(string)itemDef.item_id} weapon_profile.weapon_type_id must be non-empty.");
            return false;
        }
        if (resolvedProfile.family == "")
        {
            _validationErrors.Add($"Weapon item {(string)itemDef.item_id} weapon_profile.family must be non-empty.");
            return false;
        }
        if (resolvedProfile.range_type == "")
        {
            _validationErrors.Add($"Weapon item {(string)itemDef.item_id} weapon_profile.range_type must be non-empty.");
            return false;
        }
        if (resolvedProfile.damage_tag == "")
        {
            _validationErrors.Add($"Weapon item {(string)itemDef.item_id} weapon_profile.damage_tag must be non-empty.");
            return false;
        }
        if (resolvedProfile.attack_range <= 0)
        {
            _validationErrors.Add($"Weapon item {(string)itemDef.item_id} weapon_profile.attack_range must be >= 1 (got {resolvedProfile.attack_range}).");
            return false;
        }
        if (resolvedProfile.one_handed_dice == null && resolvedProfile.two_handed_dice == null)
        {
            _validationErrors.Add($"Weapon item {(string)itemDef.item_id} weapon_profile must declare at least one of one_handed_dice or two_handed_dice.");
            return false;
        }

        var diceErrors = new Godot.Collections.Array<string>();
        if (resolvedProfile.one_handed_dice != null)
        {
            foreach (string error in WeaponDamageDiceDef.validate_dice($"Weapon item {(string)itemDef.item_id} weapon_profile.one_handed_dice", resolvedProfile.one_handed_dice))
                diceErrors.Add(error);
        }
        if (resolvedProfile.two_handed_dice != null)
        {
            foreach (string error in WeaponDamageDiceDef.validate_dice($"Weapon item {(string)itemDef.item_id} weapon_profile.two_handed_dice", resolvedProfile.two_handed_dice))
                diceErrors.Add(error);
        }
        if (diceErrors.Count > 0)
        {
            foreach (string diceError in diceErrors)
                _validationErrors.Add(diceError);
            return false;
        }
        if (itemTags.Contains(new StringName("melee")) && itemDef.get_weapon_physical_damage_tag() == "")
        {
            _validationErrors.Add($"Melee weapon item {(string)itemDef.item_id} must declare one valid weapon_profile.damage_tag.");
            return false;
        }
        return true;
    }

    public static ItemDef resolve_with_template_chain(
        ItemDef item_def,
        Godot.Collections.Dictionary template_defs,
        Godot.Collections.Array visited,
        Godot.Collections.Dictionary cache,
        Godot.Collections.Array errors
    )
    {
        return ResolveWithTemplateChain(item_def, template_defs, visited, cache, errors);
    }

    private static ItemDef ResolveWithTemplateChain(
        ItemDef itemDef,
        IReadOnlyDictionary<StringName, ItemDef> templateDefs,
        List<StringName> visited,
        Dictionary<StringName, ItemDef> cache,
        List<string> errors
    )
    {
        if (itemDef == null)
        {
            errors.Add("resolve_with_template_chain received null item_def.");
            return null;
        }

        var currentId = itemDef.item_id;
        if (visited.Contains(currentId))
        {
            string chainText = string.Join(", ", visited) + $" -> {(string)currentId}";
            errors.Add($"Item template inheritance cycle detected at {(string)currentId} (chain: {chainText}).");
            return null;
        }

        if (itemDef.base_item_id == "")
            return itemDef;

        var templateId = itemDef.base_item_id;
        if (!templateDefs.ContainsKey(templateId))
        {
            errors.Add($"Item {(string)currentId} references missing template {(string)templateId}.");
            return null;
        }

        ItemDef resolvedTemplate;
        if (cache.ContainsKey(templateId))
        {
            resolvedTemplate = cache[templateId];
        }
        else
        {
            var nextVisited = new List<StringName>(visited) { currentId };
            resolvedTemplate = ResolveWithTemplateChain(templateDefs[templateId], templateDefs, nextVisited, cache, errors);
            if (resolvedTemplate != null)
                cache[templateId] = resolvedTemplate;
        }

        return resolvedTemplate == null ? null : merge_with_template(resolvedTemplate, itemDef);
    }

    private static ItemDef ResolveWithTemplateChain(
        ItemDef itemDef,
        Godot.Collections.Dictionary templateDefs,
        Godot.Collections.Array visited,
        Godot.Collections.Dictionary cache,
        Godot.Collections.Array errors
    )
    {
        if (itemDef == null)
        {
            errors.Add("resolve_with_template_chain received null item_def.");
            return null;
        }

        var currentId = itemDef.item_id;
        if (ArrayHasStringName(visited, currentId))
        {
            string chainText = ArrayString(visited) + $" -> {(string)currentId}";
            errors.Add($"Item template inheritance cycle detected at {(string)currentId} (chain: {chainText}).");
            return null;
        }

        if (itemDef.base_item_id == "")
            return itemDef;

        var templateId = itemDef.base_item_id;
        if (!templateDefs.ContainsKey(templateId))
        {
            errors.Add($"Item {(string)currentId} references missing template {(string)templateId}.");
            return null;
        }

        ItemDef resolvedTemplate;
        if (cache.ContainsKey(templateId))
        {
            resolvedTemplate = DictItemDef(cache, templateId);
        }
        else
        {
            var nextVisited = new Godot.Collections.Array(visited);
            nextVisited.Add(currentId);
            resolvedTemplate = ResolveWithTemplateChain(DictItemDef(templateDefs, templateId), templateDefs, nextVisited, cache, errors);
            if (resolvedTemplate != null)
                cache[templateId] = resolvedTemplate;
        }

        return resolvedTemplate == null ? null : merge_with_template(resolvedTemplate, itemDef);
    }

    public static ItemDef merge_with_template(ItemDef template, ItemDef instance)
    {
        var merged = new ItemDef
        {
            item_id = instance.item_id,
            base_item_id = "",
            display_name = instance.display_name != "" ? instance.display_name : template.display_name,
            description = instance.description != "" ? instance.description : template.description,
            icon = instance.icon != "" ? instance.icon : template.icon,
            equipment_type_id = instance.equipment_type_id != "" ? instance.equipment_type_id : template.equipment_type_id,
            weapon_profile = WeaponProfileDef.merge(GetWeaponProfile(template), GetWeaponProfile(instance)),
            granted_skill_id = instance.granted_skill_id != "" ? instance.granted_skill_id : template.granted_skill_id,
            item_category = instance.item_category != "" ? instance.item_category : template.item_category,
            base_price = instance.base_price != 0 ? instance.base_price : template.base_price,
            buy_price = instance.buy_price != 0 ? instance.buy_price : template.buy_price,
            sell_price = instance.sell_price != 0 ? instance.sell_price : template.sell_price,
            max_dex_bonus = instance.max_dex_bonus >= 0 ? instance.max_dex_bonus : template.max_dex_bonus,
            is_stackable = instance.is_stackable,
            max_stack = instance.max_stack,
            sellable = instance.sellable,
            equipment_slot_ids = instance.equipment_slot_ids.Count > 0 ? DuplicateStringArray(instance.equipment_slot_ids) : DuplicateStringArray(template.equipment_slot_ids),
            occupied_slot_ids = instance.occupied_slot_ids.Count > 0 ? DuplicateStringArray(instance.occupied_slot_ids) : DuplicateStringArray(template.occupied_slot_ids),
            equip_requirement = instance.equip_requirement ?? template.equip_requirement,
            tags = MergeStringNameArray(template.tags, instance.tags),
            crafting_groups = MergeStringNameArray(template.crafting_groups, instance.crafting_groups),
            quest_groups = MergeStringNameArray(template.quest_groups, instance.quest_groups),
            attribute_modifiers = MergeAttributeModifiers(template.attribute_modifiers, instance.attribute_modifiers, instance.item_id),
        };
        return merged;
    }

    private static Godot.Collections.Array<StringName> MergeStringNameArray(Godot.Collections.Array<StringName> templateValues, Godot.Collections.Array<StringName> instanceValues)
    {
        var seen = new HashSet<StringName>();
        var merged = new Godot.Collections.Array<StringName>();
        foreach (var value in templateValues)
        {
            var normalized = ProgressionDataUtils.to_string_name(Variant.From(value));
            if (normalized == "" || seen.Contains(normalized))
                continue;
            seen.Add(normalized);
            merged.Add(normalized);
        }
        foreach (var value in instanceValues)
        {
            var normalized = ProgressionDataUtils.to_string_name(Variant.From(value));
            if (normalized == "" || seen.Contains(normalized))
                continue;
            seen.Add(normalized);
            merged.Add(normalized);
        }
        return merged;
    }

    private static Godot.Collections.Array<string> DuplicateStringArray(Godot.Collections.Array<string> sourceValues)
    {
        var copied = new Godot.Collections.Array<string>();
        foreach (string value in sourceValues)
            copied.Add(value);
        return copied;
    }

    private static WeaponProfileDef GetWeaponProfile(ItemDef itemDef)
    {
        return itemDef?.weapon_profile as WeaponProfileDef;
    }

    private static Godot.Collections.Array<AttributeModifier> MergeAttributeModifiers(
        Godot.Collections.Array<AttributeModifier> templateMods,
        Godot.Collections.Array<AttributeModifier> instanceMods,
        StringName finalItemId
    )
    {
        var merged = new Godot.Collections.Array<AttributeModifier>();
        foreach (var sourceMod in templateMods)
        {
            if (sourceMod == null)
                continue;
            var copy = sourceMod.Duplicate(true) as AttributeModifier;
            copy.source_id = finalItemId;
            merged.Add(copy);
        }
        foreach (var sourceMod in instanceMods)
        {
            if (sourceMod == null)
                continue;
            var copy = sourceMod.Duplicate(true) as AttributeModifier;
            copy.source_id = finalItemId;
            merged.Add(copy);
        }
        return merged;
    }

    private static bool ArrayHasStringName(Godot.Collections.Array values, StringName expected)
    {
        foreach (var value in values)
        {
            if (ProgressionDataUtils.to_string_name(value) == expected)
                return true;
        }
        return false;
    }

    private static string ArrayString(Godot.Collections.Array values)
    {
        var parts = new List<string>();
        foreach (var value in values)
            parts.Add(value.AsString());
        return "[" + string.Join(", ", parts) + "]";
    }

    private static ItemDef DictItemDef(Godot.Collections.Dictionary values, StringName key)
    {
        if (!values.ContainsKey(key))
            return null;
        var value = values[key];
        return value.VariantType == Variant.Type.Object ? value.AsGodotObject() as ItemDef : null;
    }
}
