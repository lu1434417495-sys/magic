# 通用人物/装备特性系统 — 实现细则（可直接落地）

更新日期：`2026-06-19`

本文件是 [`trait_system_plan.md`](trait_system_plan.md) 的历史实现细则，记录当时拟采用的类型、方法体、映射表、roll 算法、序列化与校验逻辑。当前实现以 [`../../design/progression/trait_system.md`](../../design/progression/trait_system.md)、源码和测试为准。

约定：所有 `StringName <-> enum` 映射沿用 `RaceTraitDef.ToEffectKind/ToStringName` 与 `TraitTriggerContentRules` 的静态映射风格；所有 strict payload 校验沿用 `EquipmentInstanceState._get_payload_validation_error` 风格；RNG 注入沿用 `EquipmentDropService` 的 `Func<int,int,int>` 风格。

字典使用约定：

- 本系统正式代码除存档序列化和 UI 适配边界外，不允许使用 `Dictionary<StringName, T>`、`IReadOnlyDictionary<StringName, T>` 或 `Godot.Collections.Dictionary` 作为 trait 数据模型、私有索引、runtime payload、服务 DTO 或 `.tres` 动态参数。
- `TraitDef.@params` 禁止出现；effect-specific 配置必须落成显式 typed 字段，例如 `highest_roll_compare_key`、`vision_range`、`proficiency_choice_count`。
- `TraitInstanceState.roll_values` 在内存中是 `Array<TraitRollValueState>`；只有 `TraitInstanceState.ToDictionary()/FromDictionary()` 和 battle/party/equipment save payload 边界把 `roll_values` 投成 dictionary。
- `EffectiveTraitSet` 不维护 `_byKey` / `_byTrait` dictionary 索引；typed 查询使用列表扫描，避免把 dictionary 作为正式 runtime cache。
- `BattleUnitState.effective_trait_instances` 在内存中是 `Array<BattleEffectiveTraitInstanceState>`；只有 `BattleUnitState.ToDictionary()/FromDictionary()` save 边界把它序列化成 dictionary 数组。
- 不使用读取 C# 或 `.tres` 文本的回归来约束 dictionary 边界。静态依赖约束归 architecture analyzer 与代码检视；资源和运行时契约分别由 trait registry、instance schema、effective-set 与 battle owner 行为回归覆盖。

---

## 1. Typed enum 与 TraitContentRules

`scripts/player/progression/TraitContentRules.cs`：

```csharp
using Godot;

internal enum TraitEffectKind
{
    Unknown = 0,
    // 第一版接 typed 触发 handler 的三个：
    HalflingLuck,
    SavageAttacks,
    RelentlessEndurance,
    // 从 RaceTraitEffectKind 迁移、第一版仅作分类 / 属性 / 静态投影归属的效果，
    // 按需逐条加入（Darkvision、DamageResistance、SaveAdvantage ...）。迁移时一次性补全。
}

internal enum TraitStackPolicyKind
{
    Unknown = 0,
    UniqueByTrait,
    HighestRoll,
    Additive,
    StackByInstance,
}

internal enum TraitSourceKind
{
    Unknown = 0,
    Identity,
    Character,
    EquipmentFixed,
    EquipmentRoll,
}

internal enum TraitRollValueType
{
    Unknown = 0,
    Int,
    StringName,
    Bool,
}

internal enum TraitChargeScopeKind
{
    Unknown = 0,
    None,
    PerTurn,
    PerBattle,
}

internal enum TraitChargeResetTimingKind
{
    Unknown = 0,
    None,
    BattleStart,
    TurnStart,
}

public static class TraitContentRules
{
    // effect_type
    private static readonly StringName EffectHalflingLuck = "halfling_luck";
    private static readonly StringName EffectSavageAttacks = "savage_attacks";
    private static readonly StringName EffectRelentlessEndurance = "relentless_endurance";

    // stack_policy
    private static readonly StringName StackUniqueByTrait = "unique_by_trait";
    private static readonly StringName StackHighestRoll = "highest_roll";
    private static readonly StringName StackAdditive = "additive";
    private static readonly StringName StackByInstance = "stack_by_instance";

    // source_type
    private static readonly StringName SourceIdentity = "identity";
    private static readonly StringName SourceCharacter = "character";
    private static readonly StringName SourceEquipmentFixed = "equipment_fixed";
    private static readonly StringName SourceEquipmentRoll = "equipment_roll";

    // roll value type
    private static readonly StringName RollInt = "int";
    private static readonly StringName RollStringName = "string_name";
    private static readonly StringName RollBool = "bool";

    // charge policy；trigger_type 只表示行为触发时机，不参与 charge 生命周期判断。
    private static readonly StringName ChargeNone = "none";
    private static readonly StringName ChargePerTurn = "per_turn";
    private static readonly StringName ChargePerBattle = "per_battle";
    private static readonly StringName ResetNone = "none";
    private static readonly StringName ResetBattleStart = "battle_start";
    private static readonly StringName ResetTurnStart = "turn_start";

    public static bool IsValidEffectType(StringName value) => ToEffectKind(value) != TraitEffectKind.Unknown;
    public static bool IsValidStackPolicy(StringName value) => ToStackPolicyKind(value) != TraitStackPolicyKind.Unknown;
    public static bool IsValidSourceType(StringName value) => ToSourceKind(value) != TraitSourceKind.Unknown;
    public static bool IsValidRollValueType(StringName value) => ToRollValueType(value) != TraitRollValueType.Unknown;
    public static bool IsValidChargeScope(StringName value) => ToChargeScopeKind(value) != TraitChargeScopeKind.Unknown;
    public static bool IsValidChargeResetTiming(StringName value) => ToChargeResetTimingKind(value) != TraitChargeResetTimingKind.Unknown;

    internal static TraitEffectKind ToEffectKind(StringName value)
    {
        if (value == EffectHalflingLuck) return TraitEffectKind.HalflingLuck;
        if (value == EffectSavageAttacks) return TraitEffectKind.SavageAttacks;
        if (value == EffectRelentlessEndurance) return TraitEffectKind.RelentlessEndurance;
        return TraitEffectKind.Unknown;
    }

    internal static StringName ToStringName(TraitEffectKind kind) => kind switch
    {
        TraitEffectKind.HalflingLuck => EffectHalflingLuck,
        TraitEffectKind.SavageAttacks => EffectSavageAttacks,
        TraitEffectKind.RelentlessEndurance => EffectRelentlessEndurance,
        _ => "",
    };

    internal static TraitStackPolicyKind ToStackPolicyKind(StringName value)
    {
        if (value == StackUniqueByTrait) return TraitStackPolicyKind.UniqueByTrait;
        if (value == StackHighestRoll) return TraitStackPolicyKind.HighestRoll;
        if (value == StackAdditive) return TraitStackPolicyKind.Additive;
        if (value == StackByInstance) return TraitStackPolicyKind.StackByInstance;
        return TraitStackPolicyKind.Unknown;
    }

    internal static StringName ToStringName(TraitStackPolicyKind kind) => kind switch
    {
        TraitStackPolicyKind.UniqueByTrait => StackUniqueByTrait,
        TraitStackPolicyKind.HighestRoll => StackHighestRoll,
        TraitStackPolicyKind.Additive => StackAdditive,
        TraitStackPolicyKind.StackByInstance => StackByInstance,
        _ => "",
    };

    internal static TraitSourceKind ToSourceKind(StringName value)
    {
        if (value == SourceIdentity) return TraitSourceKind.Identity;
        if (value == SourceCharacter) return TraitSourceKind.Character;
        if (value == SourceEquipmentFixed) return TraitSourceKind.EquipmentFixed;
        if (value == SourceEquipmentRoll) return TraitSourceKind.EquipmentRoll;
        return TraitSourceKind.Unknown;
    }

    internal static StringName ToStringName(TraitSourceKind kind) => kind switch
    {
        TraitSourceKind.Identity => SourceIdentity,
        TraitSourceKind.Character => SourceCharacter,
        TraitSourceKind.EquipmentFixed => SourceEquipmentFixed,
        TraitSourceKind.EquipmentRoll => SourceEquipmentRoll,
        _ => "",
    };

    internal static TraitRollValueType ToRollValueType(StringName value)
    {
        if (value == RollInt) return TraitRollValueType.Int;
        if (value == RollStringName) return TraitRollValueType.StringName;
        if (value == RollBool) return TraitRollValueType.Bool;
        return TraitRollValueType.Unknown;
    }

    internal static TraitChargeScopeKind ToChargeScopeKind(StringName value)
    {
        if (value == ChargeNone) return TraitChargeScopeKind.None;
        if (value == ChargePerTurn) return TraitChargeScopeKind.PerTurn;
        if (value == ChargePerBattle) return TraitChargeScopeKind.PerBattle;
        return TraitChargeScopeKind.Unknown;
    }

    internal static StringName ToStringName(TraitChargeScopeKind kind) => kind switch
    {
        TraitChargeScopeKind.None => ChargeNone,
        TraitChargeScopeKind.PerTurn => ChargePerTurn,
        TraitChargeScopeKind.PerBattle => ChargePerBattle,
        _ => "",
    };

    internal static TraitChargeResetTimingKind ToChargeResetTimingKind(StringName value)
    {
        if (value == ResetNone) return TraitChargeResetTimingKind.None;
        if (value == ResetBattleStart) return TraitChargeResetTimingKind.BattleStart;
        if (value == ResetTurnStart) return TraitChargeResetTimingKind.TurnStart;
        return TraitChargeResetTimingKind.Unknown;
    }

    internal static StringName ToStringName(TraitChargeResetTimingKind kind) => kind switch
    {
        TraitChargeResetTimingKind.None => ResetNone,
        TraitChargeResetTimingKind.BattleStart => ResetBattleStart,
        TraitChargeResetTimingKind.TurnStart => ResetTurnStart,
        _ => "",
    };

    internal static bool IsSourceKindAllowed(TraitDef def, TraitSourceKind kind)
    {
        if (def == null || kind == TraitSourceKind.Unknown || def.allowed_source_kinds == null)
            return false;
        StringName value = ToStringName(kind);
        foreach (StringName raw in def.allowed_source_kinds)
            if (ProgressionDataUtils.to_string_name(raw) == value)
                return true;
        return false;
    }

    // trait attribute modifier 的 source_type（trace 用），与 TraitSourceKind 区分命名空间。
    internal static StringName ToAttributeSourceType(TraitSourceKind kind) => kind switch
    {
        TraitSourceKind.Identity => "trait_identity",
        TraitSourceKind.Character => "trait_character",
        TraitSourceKind.EquipmentFixed => "trait_equipment_fixed",
        TraitSourceKind.EquipmentRoll => "trait_equipment_roll",
        _ => "trait_unknown",
    };
}
```

`TraitTriggerKind` 复用现有 `TraitTriggerContentRules`，不重复定义。Phase 1 迁移 race trait 资源时，`TraitEffectKind` / `ToEffectKind()` / `ToStringName(TraitEffectKind)` 必须一次性覆盖当前 `RaceTraitEffectKind` 的全部值；未接 handler 的效果也要能被 content registry 识别。落码前补一张 `RaceTraitEffectKind -> TraitEffectKind/StringName` parity 表，并用内容回归断言迁移前后 trait 数量一致。

---

## 2. TraitDef 与子资源

`scripts/player/progression/TraitDef.cs`：

```csharp
using Godot;

[GlobalClass]
public partial class TraitDef : Resource
{
    [Export] public StringName trait_id { get; set; } = "";
    [Export] public string display_name { get; set; } = "";
    [Export(PropertyHint.MultilineText)] public string description { get; set; } = "";
    [Export] public Godot.Collections.Array<StringName> categories { get; set; } = new();
    [Export] public Godot.Collections.Array<StringName> allowed_source_kinds { get; set; } = new();

    [Export] public StringName effect_type { get; set; } = "";
    [Export] public StringName trigger_type { get; set; } = "passive"; // 行为触发时机，不是 charge reset policy。
    [Export] public StringName stack_policy { get; set; } = "unique_by_trait";
    [Export] public StringName charge_scope { get; set; } = "none";
    [Export] public StringName charge_reset_timing { get; set; } = "none";

    [Export] public StringName highest_roll_compare_key { get; set; } = "";
    [Export] public int vision_range { get; set; } = 0;
    [Export] public int proficiency_choice_count { get; set; } = 0;
    [Export] public Godot.Collections.Array<AttributeModifier> attribute_modifiers { get; set; } = new();
    [Export] public Godot.Collections.Array<TraitRollValueSchemaEntry> roll_value_schema { get; set; } = new();

    internal TraitEffectKind EffectKind => TraitContentRules.ToEffectKind(effect_type);
    internal TraitTriggerKind TriggerKind => TraitTriggerContentRules.ToTriggerKind(trigger_type);
    internal TraitStackPolicyKind StackPolicyKind => TraitContentRules.ToStackPolicyKind(stack_policy);
    internal TraitChargeScopeKind ChargeScopeKind => TraitContentRules.ToChargeScopeKind(charge_scope);
    internal TraitChargeResetTimingKind ChargeResetTimingKind => TraitContentRules.ToChargeResetTimingKind(charge_reset_timing);

    // highest_roll 比较用的 roll key；缺省取第一个 Int schema entry。
    internal StringName GetHighestRollCompareKey()
    {
        if (highest_roll_compare_key != "")
            return highest_roll_compare_key;
        foreach (TraitRollValueSchemaEntry entry in roll_value_schema)
            if (entry != null && entry.ValueTypeKind == TraitRollValueType.Int)
                return entry.key;
        return "";
    }

    internal TraitRollValueSchemaEntry FindSchemaEntry(StringName key)
    {
        foreach (TraitRollValueSchemaEntry entry in roll_value_schema)
            if (entry != null && entry.key == key)
                return entry;
        return null;
    }
}
```

`scripts/player/progression/TraitRollValueSchemaEntry.cs`：

```csharp
using Godot;

[GlobalClass]
public partial class TraitRollValueSchemaEntry : Resource
{
    [Export] public StringName key { get; set; } = "";
    [Export] public StringName value_type { get; set; } = "int";
    [Export] public int min_value { get; set; }
    [Export] public int max_value { get; set; }
    [Export] public Godot.Collections.Array<StringName> allowed_values { get; set; } = new();

    internal TraitRollValueType ValueTypeKind => TraitContentRules.ToRollValueType(value_type);

    // 自洽校验：供 validator 调用，返回错误串列表（空=合法）。
    internal void AppendSchemaErrors(System.Collections.Generic.List<string> errors, string ownerLabel)
    {
        if (key == "")
            errors.Add($"{ownerLabel}: roll_value_schema entry missing key.");
        switch (ValueTypeKind)
        {
            case TraitRollValueType.Int:
                if (min_value > max_value)
                    errors.Add($"{ownerLabel}.{key}: min_value {min_value} > max_value {max_value}.");
                break;
            case TraitRollValueType.StringName:
                if (allowed_values.Count == 0)
                    errors.Add($"{ownerLabel}.{key}: string_name roll needs non-empty allowed_values.");
                break;
            case TraitRollValueType.Bool:
                break;
            default:
                errors.Add($"{ownerLabel}.{key}: unsupported value_type {value_type}.");
                break;
        }
    }
}
```

`scripts/player/warehouse/TraitRollGroupDef.cs` 与 `TraitRollGroupEntryDef.cs`：

```csharp
using Godot;

[GlobalClass]
public partial class TraitRollGroupDef : Resource
{
    [Export] public StringName group_id { get; set; } = "";
    [Export] public int roll_count { get; set; } = 1;
    [Export] public Godot.Collections.Array<TraitRollGroupEntryDef> entries { get; set; } = new();
}

[GlobalClass]
public partial class TraitRollGroupEntryDef : Resource
{
    [Export] public StringName trait_id { get; set; } = "";
    [Export] public int weight { get; set; } = 1;
    [Export] public StringName exclusive_group { get; set; } = "";
}
```

---

## 3. TraitInstanceState

`scripts/player/progression/TraitInstanceState.cs`，镜像 `EquipmentInstanceState` 的严格 payload 风格：

```csharp
using Godot;

public partial class TraitInstanceState : RefCounted
{
    private const string SAVE_PAYLOAD_LABEL = "trait instance payload";

    private static readonly string[] REQUIRED_FIELDS =
    {
        "trait_instance_id", "trait_id", "source_type", "source_id",
        "rank", "stacks", "roll_values",
    };

    public StringName trait_instance_id = "";
    public StringName trait_id = "";
    public StringName source_type = "";
    public StringName source_id = "";
    public int rank = 1;
    public int stacks = 1;
    public Godot.Collections.Array<TraitRollValueState> roll_values = new(); // runtime typed entries；save 边界投成 Dictionary。

    internal TraitSourceKind SourceKind => TraitContentRules.ToSourceKind(source_type);

    public static TraitInstanceState Create(
        StringName traitInstanceId,
        StringName traitId,
        TraitSourceKind sourceKind,
        StringName sourceId,
        int rank = 1,
        int stacks = 1,
        Godot.Collections.Dictionary rollValues = null
    )
    {
        return new TraitInstanceState
        {
            trait_instance_id = ProgressionDataUtils.to_string_name(traitInstanceId),
            trait_id = ProgressionDataUtils.to_string_name(traitId),
            source_type = TraitContentRules.ToStringName(sourceKind),
            source_id = ProgressionDataUtils.to_string_name(sourceId),
            rank = Mathf.Max(rank, 1),
            stacks = Mathf.Max(stacks, 1),
            roll_values = NormalizeRollValues(rollValues),
        };
    }

    public static Godot.Collections.Dictionary NormalizeRollValues(Godot.Collections.Dictionary source)
    {
        var normalized = new Godot.Collections.Dictionary();
        if (source == null) return normalized;
        foreach (Variant key in source.Keys)
        {
            StringName normalizedKey = ProgressionDataUtils.to_string_name(key);
            if (normalizedKey == "") continue;
            normalized[normalizedKey] = source[key];
        }
        return normalized;
    }

    // ---- typed roll readers（不直接回读 dictionary）----
    public int GetIntRoll(StringName key, int fallback = 0)
    {
        if (!roll_values.ContainsKey(key)) return fallback;
        Variant v = roll_values[key];
        return v.VariantType == Variant.Type.Int ? v.AsInt32() : fallback;
    }

    public StringName GetStringNameRoll(StringName key, StringName fallback = default)
    {
        StringName fb = fallback ?? "";
        if (!roll_values.ContainsKey(key)) return fb;
        Variant v = roll_values[key];
        return v.VariantType is Variant.Type.String or Variant.Type.StringName
            ? ProgressionDataUtils.to_string_name(v) : fb;
    }

    public bool GetBoolRoll(StringName key, bool fallback = false)
    {
        if (!roll_values.ContainsKey(key)) return fallback;
        Variant v = roll_values[key];
        return v.VariantType == Variant.Type.Bool ? v.AsBool() : fallback;
    }

    public Godot.Collections.Dictionary ToDictionary() => new()
    {
        { "trait_instance_id", (string)trait_instance_id },
        { "trait_id", (string)trait_id },
        { "source_type", (string)source_type },
        { "source_id", (string)source_id },
        { "rank", rank },
        { "stacks", stacks },
        { "roll_values", roll_values.Duplicate(true) },
    };

    public TraitInstanceState DuplicateState() => new()
    {
        trait_instance_id = trait_instance_id,
        trait_id = trait_id,
        source_type = source_type,
        source_id = source_id,
        rank = rank,
        stacks = stacks,
            roll_values = NormalizeRollValues(roll_values),
    };

    public static TraitInstanceState FromDictionary(Godot.Collections.Dictionary data)
    {
        string err = GetPayloadValidationError(data);
        if (err.Length > 0)
        {
            GameLog.Error(err, "trait.validation_failed", "progression");
            return null;
        }
        return new TraitInstanceState
        {
            trait_instance_id = new StringName(data["trait_instance_id"].AsString().StripEdges()),
            trait_id = new StringName(data["trait_id"].AsString().StripEdges()),
            source_type = new StringName(data["source_type"].AsString().StripEdges()),
            source_id = new StringName(data["source_id"].AsString().StripEdges()),
            rank = data["rank"].AsInt32(),
            stacks = data["stacks"].AsInt32(),
            roll_values = NormalizeRollValues(data["roll_values"].AsGodotDictionary()),
        };
    }

    public string ValidateAgainstDef(TraitDef def)
    {
        if (def == null)
            return $"Trait instance {trait_instance_id}: missing TraitDef for {trait_id}.";
        var normalized = NormalizeRollValues(roll_values);
        var expected = new System.Collections.Generic.HashSet<string>();
        foreach (TraitRollValueSchemaEntry schema in def.roll_value_schema)
        {
            if (schema == null || schema.key == "") continue;
            expected.Add(schema.key.ToString());
            if (!normalized.ContainsKey(schema.key))
                return $"Trait instance {trait_instance_id}: missing roll key {schema.key}.";
            Variant value = normalized[schema.key];
            switch (schema.ValueTypeKind)
            {
                case TraitRollValueType.Int:
                    if (value.VariantType != Variant.Type.Int)
                        return $"Trait instance {trait_instance_id}: roll {schema.key} must be int.";
                    int n = value.AsInt32();
                    if (n < schema.min_value || n > schema.max_value)
                        return $"Trait instance {trait_instance_id}: roll {schema.key} out of range.";
                    break;
                case TraitRollValueType.StringName:
                    StringName s = ProgressionDataUtils.to_string_name(value);
                    bool allowed = false;
                    foreach (StringName option in schema.allowed_values)
                        if (ProgressionDataUtils.to_string_name(option) == s)
                        { allowed = true; break; }
                    if (!allowed)
                        return $"Trait instance {trait_instance_id}: roll {schema.key} value {s} is not allowed.";
                    break;
                case TraitRollValueType.Bool:
                    if (value.VariantType != Variant.Type.Bool)
                        return $"Trait instance {trait_instance_id}: roll {schema.key} must be bool.";
                    break;
                default:
                    return $"Trait instance {trait_instance_id}: unsupported schema type for {schema.key}.";
            }
        }
        foreach (Variant rawKey in normalized.Keys)
            if (!expected.Contains(ProgressionDataUtils.to_string_name(rawKey).ToString()))
                return $"Trait instance {trait_instance_id}: unexpected roll key {rawKey}.";
        roll_values = normalized;
        return "";
    }

    public static string GetPayloadValidationError(Godot.Collections.Dictionary data)
    {
        if (data == null)
            return $"Corrupt {SAVE_PAYLOAD_LABEL}: expected Dictionary.";
        foreach (string fn in REQUIRED_FIELDS)
            if (!data.ContainsKey(fn))
                return $"Corrupt {SAVE_PAYLOAD_LABEL}: missing required field '{fn}'.";
        if (data.Count != REQUIRED_FIELDS.Length)
            return $"Corrupt {SAVE_PAYLOAD_LABEL}: expected exactly current trait instance fields.";
        foreach (var key in data.Keys)
            if (key.VariantType != Variant.Type.String
                || !System.Array.Exists(REQUIRED_FIELDS, f => f == key.AsString()))
                return $"Corrupt {SAVE_PAYLOAD_LABEL}: unsupported field '{key}'.";

        if (data["trait_instance_id"].VariantType != Variant.Type.String)
            return $"Corrupt {SAVE_PAYLOAD_LABEL}: trait_instance_id must be String.";
        if (data["trait_id"].VariantType != Variant.Type.String
            || data["trait_id"].AsString().StripEdges().Length == 0)
            return $"Corrupt {SAVE_PAYLOAD_LABEL}: trait_id is required.";
        if (data["source_type"].VariantType != Variant.Type.String
            || !TraitContentRules.IsValidSourceType(new StringName(data["source_type"].AsString().StripEdges())))
            return $"Corrupt {SAVE_PAYLOAD_LABEL}: invalid source_type.";
        if (data["source_id"].VariantType != Variant.Type.String)
            return $"Corrupt {SAVE_PAYLOAD_LABEL}: source_id must be String.";
        if (data["rank"].VariantType != Variant.Type.Int || data["rank"].AsInt32() < 1)
            return $"Corrupt {SAVE_PAYLOAD_LABEL}: rank must be int >= 1.";
        if (data["stacks"].VariantType != Variant.Type.Int || data["stacks"].AsInt32() < 1)
            return $"Corrupt {SAVE_PAYLOAD_LABEL}: stacks must be int >= 1.";
        if (data["roll_values"].VariantType != Variant.Type.Dictionary)
            return $"Corrupt {SAVE_PAYLOAD_LABEL}: roll_values must be Dictionary.";
        // character 实例必须有非空 instance_id；identity 来源不持久化 TraitInstanceState（见聚合层）。
        TraitSourceKind kind = TraitContentRules.ToSourceKind(new StringName(data["source_type"].AsString().StripEdges()));
        if ((kind == TraitSourceKind.Character || kind == TraitSourceKind.EquipmentRoll)
            && data["trait_instance_id"].AsString().StripEdges().Length == 0)
            return $"Corrupt {SAVE_PAYLOAD_LABEL}: trait_instance_id is required for {kind}.";
        return "";
    }
}
```

`FromDictionary()` 只做 payload shape 校验与 key normalize；业务层在 registry 可用时必须继续调用 `ValidateAgainstDef(def)`，确保 `roll_values` 的 key、类型、范围与 `TraitDef.roll_value_schema` 完全一致。typed reader 不做 string/StringName 双查；所有边界入口都先 normalize。

容器端（数组）通用序列化助手，放在各宿主类内或一个 `TraitInstanceCollection` 静态类：

```csharp
internal static class TraitInstanceCollection
{
    internal static Godot.Collections.Array ToPayloadArray(
        Godot.Collections.Array<TraitInstanceState> instances)
    {
        var arr = new Godot.Collections.Array();
        foreach (TraitInstanceState inst in instances)
            if (inst != null) arr.Add(inst.ToDictionary());
        return arr;
    }

    // 任一元素非法即整体失败（返回 null），不静默丢弃。expectedKind 用于断言来源归属。
    internal static Godot.Collections.Array<TraitInstanceState> FromPayloadArray(
        Variant payload, TraitSourceKind expectedKind)
    {
        var result = new Godot.Collections.Array<TraitInstanceState>();
        if (payload.VariantType != Variant.Type.Array) return null;
        foreach (Variant entry in payload.AsGodotArray())
        {
            if (entry.VariantType != Variant.Type.Dictionary) return null;
            TraitInstanceState inst = TraitInstanceState.FromDictionary(entry.AsGodotDictionary());
            if (inst == null || inst.SourceKind != expectedKind) return null;
            result.Add(inst);
        }
        return result;
    }

    internal static Godot.Collections.Array<TraitInstanceState> Duplicate(
        Godot.Collections.Array<TraitInstanceState> instances)
    {
        var result = new Godot.Collections.Array<TraitInstanceState>();
        foreach (TraitInstanceState inst in instances)
            if (inst != null) result.Add(inst.DuplicateState());
        return result;
    }
}
```

---

## 4. TraitContentRegistry

`scripts/player/progression/TraitContentRegistry.cs`，镜像 `RaceTraitContentRegistry`：

```csharp
using Godot;

[GlobalClass]
public partial class TraitContentRegistry : IdentityContentRegistryBase
{
    private const string TraitConfigDirectoryPath = "res://data/configs/traits";

    private readonly System.Collections.Generic.List<TraitDef> _trait_defs = new();

    public TraitContentRegistry()
    {
        _registry_label = "TraitContentRegistry";
        Rebuild();
    }

    public void Rebuild() => LoadFromDirectories(new Godot.Collections.Array<string> { TraitConfigDirectoryPath });

    public void LoadFromDirectories(Godot.Collections.Array<string> directoryPaths)
    {
        _trait_defs.Clear();
        _validation_errors.Clear();
        foreach (var directoryPath in directoryPaths)
            _scan_directory(directoryPath);
        foreach (var e in _collect_validation_errors())
            _validation_errors.Add(e);
    }

    public System.Collections.Generic.IReadOnlyList<TraitDef> GetTraitDefsTyped()
        => new System.Collections.Generic.List<TraitDef>(_trait_defs);

    public TraitDef GetTraitDef(StringName traitId)
        => _trait_defs.Find(def => def != null && def.trait_id == ProgressionDataUtils.to_string_name(traitId));

    public bool HasTrait(StringName traitId)
        => GetTraitDef(traitId) != null;

    protected override void ClearRegistryData() => _trait_defs.Clear();

    protected override void _register_resource(string resourcePath)
    {
        var resource = GD.Load<Resource>(resourcePath);
        if (resource == null)
        { _validation_errors.Add($"Failed to load trait config {resourcePath}."); return; }
        if (resource is not TraitDef traitDef)
        { _validation_errors.Add($"Trait config {resourcePath} is not a TraitDef."); return; }
        if (traitDef.trait_id == "")
        { _validation_errors.Add($"Trait config {resourcePath} is missing trait_id."); return; }
        if (_trait_defs.ContainsKey(traitDef.trait_id))
        { _validation_errors.Add($"Duplicate trait_id registered: {traitDef.trait_id}"); return; }
        _trait_defs[traitDef.trait_id] = traitDef;
    }

    private Godot.Collections.Array<string> _collect_validation_errors()
    {
        var errors = new Godot.Collections.Array<string>();
        foreach (var key in _sorted_registry_keys(_trait_defs.Keys))
            _append_trait_errors(errors, new StringName(key), _trait_defs[new StringName(key)]);
        return errors;
    }

    private void _append_trait_errors(Godot.Collections.Array<string> errors, StringName traitId, TraitDef def)
    {
        var owner = $"Trait {traitId}";
        _append_string_name_field_error(errors, owner, "trait_id", def.trait_id);
        _append_string_field_error(errors, owner, "display_name", def.display_name);
        if (!TraitContentRules.IsValidEffectType(def.effect_type))
            errors.Add($"{owner} uses unsupported effect_type {def.effect_type}.");
        if (TraitTriggerContentRules.ToTriggerKind(def.trigger_type) == TraitTriggerKind.Unknown)
            errors.Add($"{owner} uses unsupported trigger_type {def.trigger_type}.");
        if (!TraitContentRules.IsValidStackPolicy(def.stack_policy))
            errors.Add($"{owner} uses unsupported stack_policy {def.stack_policy}.");
        if (!TraitContentRules.IsValidChargeScope(def.charge_scope))
            errors.Add($"{owner} uses unsupported charge_scope {def.charge_scope}.");
        if (!TraitContentRules.IsValidChargeResetTiming(def.charge_reset_timing))
            errors.Add($"{owner} uses unsupported charge_reset_timing {def.charge_reset_timing}.");
        if (def.allowed_source_kinds == null || def.allowed_source_kinds.Count == 0)
            errors.Add($"{owner} must declare at least one allowed_source_kind.");
        else
        {
            foreach (StringName rawSource in def.allowed_source_kinds)
                if (!TraitContentRules.IsValidSourceType(rawSource))
                    errors.Add($"{owner} uses unsupported allowed_source_kind {rawSource}.");
        }
        if (TraitContentRules.IsSourceKindAllowed(def, TraitSourceKind.Identity)
            && def.attribute_modifiers != null && def.attribute_modifiers.Count > 0)
            errors.Add($"{owner} allows identity source but defines attribute_modifiers; identity attributes remain owned by identity defs.");

        var schemaErrors = new System.Collections.Generic.List<string>();
        var seenKeys = new System.Collections.Generic.HashSet<string>();
        foreach (TraitRollValueSchemaEntry entry in def.roll_value_schema)
        {
            if (entry == null) { errors.Add($"{owner} has null roll_value_schema entry."); continue; }
            if (!seenKeys.Add(entry.key.ToString()))
                errors.Add($"{owner} duplicate roll key {entry.key}.");
            entry.AppendSchemaErrors(schemaErrors, owner);
        }
        foreach (string e in schemaErrors) errors.Add(e);

        // highest_roll 必须能解析比较 key。
        if (def.StackPolicyKind == TraitStackPolicyKind.HighestRoll && def.GetHighestRollCompareKey() == "")
            errors.Add($"{owner} stack_policy=highest_roll but no Int roll key / highest_roll_compare_key.");
    }
}
```

`GameContentCatalog` 注册 `trait_defs` bucket，并在 progression content validation 中校验：身份 def（`RaceDef`/`SubraceDef`/`BloodlineDef`/`BloodlineStageDef`/`AscensionDef`/`AscensionStageDef`）的 `trait_ids` 全部存在于 `TraitContentRegistry` 且允许 `identity`；`ItemDef.trait_ids` 全部允许 `equipment_fixed`；roll group entry 的 `trait_id` 全部允许 `equipment_roll`；`PartyMemberState.trait_instances` 使用的 trait 必须允许 `character`。引用的身份 `TraitDef` 不得带 `attribute_modifiers`（数值单一真相）。

Progression owner 接入清单：

- `ProgressionContentRegistry` 增加 `_traitDefs` / `_traitDefIndex` / `_traitContentRegistry`，并纳入 `Rebuild()` / dispose / typed getter。
- `ValidateTyped`、definition bucket、validation source replacement 和 `ContentValidationRunner` 都要把 `trait_defs` 当正式内容源。
- `GameContentCatalog` 暴露 trait defs snapshot/getter/revision；`GameSession`、`CharacterManagementModule.setup`、headless fixture 和 sim fixture 通过显式参数注入 trait catalog。
- invalid fixtures 覆盖 unsupported effect/trigger/stack/charge/source、duplicate roll key、identity attribute double-count、item source mismatch、roll_count 互斥不可满足。

---

## 5. EquipmentTraitRollService

`scripts/systems/inventory/EquipmentTraitRollService.cs`，RNG 注入镜像 `EquipmentDropService`：

```csharp
using System;
using System.Collections.Generic;
using Godot;

public class EquipmentTraitRollService
{
    private readonly TraitContentRegistry _traitRegistry;
    private RandomNumberGenerator _rng;
    private Func<int, int, int> _rollRange;       // [min,max] 闭区间整数
    private Func<float> _rollUnit;                // [0,1) 浮点（加权抽取用）

    public EquipmentTraitRollService(TraitContentRegistry traitRegistry, RandomNumberGenerator rng = null)
    {
        _traitRegistry = traitRegistry;
        ConfigureRng(rng);
    }

    private void ConfigureRng(RandomNumberGenerator rng)
    {
        _rng = rng ?? _make_default_rng();
        _rollRange = _rng.RandiRange;
        _rollUnit = _rng.Randf;
    }

    private static RandomNumberGenerator _make_default_rng()
    {
        var r = new RandomNumberGenerator();
        r.Randomize();
        return r;
    }

    internal void SetRollHooksForTesting(Func<int, int, int> rollRange, Func<float> rollUnit)
    {
        _rollRange = rollRange ?? _rng.RandiRange;
        _rollUnit = rollUnit ?? _rng.Randf;
    }

    // ===== mint：真正新建实例时调用一次 =====
    public void MintWithRolls(EquipmentInstanceState instance, ItemDef itemDef)
    {
        if (instance == null || itemDef == null) return;
        if (instance.instance_id == "")
        {
            GameLog.Error("Cannot mint equipment trait rolls before instance_id is stable.",
                "equipment.trait_roll.missing_instance_id", "inventory");
            return;
        }
        instance.trait_instances = new Godot.Collections.Array<TraitInstanceState>();
        int serial = 0;
        foreach (TraitRollGroupDef group in itemDef.trait_roll_groups)
        {
            if (group == null) continue;
            foreach (TraitRollGroupEntryDef hit in _rollGroup(group))
            {
                serial++;
                StringName instId = new StringName($"{(string)instance.instance_id}_t{serial:D2}");
                TraitDef def = _traitRegistry.GetTraitDef(hit.trait_id);
                Godot.Collections.Dictionary rollValues = _rollValuesFor(def);
                instance.trait_instances.Add(TraitInstanceState.Create(
                    instId, hit.trait_id, TraitSourceKind.EquipmentRoll,
                    instance.instance_id, rank: 1, stacks: 1, rollValues: rollValues));
            }
        }
    }

    // ===== rehydrate / clone：零 RNG =====
    // 仅校验既有 trait_instances 引用合法 + 来源正确；不抽取、不改写 roll_values。
    public bool ValidateRehydrated(EquipmentInstanceState instance)
    {
        if (instance == null) return false;
        foreach (TraitInstanceState inst in instance.trait_instances)
        {
            if (inst == null || inst.SourceKind != TraitSourceKind.EquipmentRoll) return false;
            if (!_traitRegistry.HasTrait(inst.trait_id)) return false;
        }
        return true;
    }

    // 从一个 group 加权无放回抽取 roll_count 个，命中互斥组后剔除该组其余条目。
    // validator 已保证 roll_count <= 互斥约束后的最大可命中数；这里不静默 clamp。
    private List<TraitRollGroupEntryDef> _rollGroup(TraitRollGroupDef group)
    {
        var hits = new List<TraitRollGroupEntryDef>();
        var pool = new List<TraitRollGroupEntryDef>();
        foreach (TraitRollGroupEntryDef e in group.entries)
            if (e != null && e.weight > 0 && _traitRegistry.HasTrait(e.trait_id))
                pool.Add(e);

        int target = group.roll_count;
        if (target < 0 || target > pool.Count)
        {
            GameLog.Error($"Invalid trait roll group {group.group_id}: roll_count {target} exceeds pool size.",
                "equipment.trait_roll.invalid_group", "inventory");
            return hits;
        }

        while (hits.Count < target && pool.Count > 0)
        {
            TraitRollGroupEntryDef pick = _weightedPick(pool);
            if (pick == null) break;
            hits.Add(pick);
            pool.Remove(pick);
            // 同 exclusive_group 的其余条目移出池。
            if (pick.exclusive_group != "")
            {
                pool.RemoveAll(e => e.exclusive_group == pick.exclusive_group);
                if (pool.Count + hits.Count < target)
                {
                    GameLog.Error($"Invalid trait roll group {group.group_id}: exclusive_group makes roll_count unsatisfiable.",
                        "equipment.trait_roll.invalid_exclusive_group", "inventory");
                    return new List<TraitRollGroupEntryDef>();
                }
            }
        }
        return hits;
    }

    private TraitRollGroupEntryDef _weightedPick(List<TraitRollGroupEntryDef> pool)
    {
        long total = 0;
        foreach (TraitRollGroupEntryDef e in pool) total += e.weight;
        if (total <= 0) return null;
        // _rollUnit() ∈ [0,1) -> 落点 ∈ [0,total)
        long point = (long)(_rollUnit() * total);
        if (point >= total) point = total - 1;
        long acc = 0;
        foreach (TraitRollGroupEntryDef e in pool)
        {
            acc += e.weight;
            if (point < acc) return e;
        }
        return pool[pool.Count - 1];
    }

    private Godot.Collections.Dictionary _rollValuesFor(TraitDef def)
    {
        var rolls = new Godot.Collections.Dictionary();
        if (def == null) return rolls;
        foreach (TraitRollValueSchemaEntry entry in def.roll_value_schema)
        {
            if (entry == null) continue;
            switch (entry.ValueTypeKind)
            {
                case TraitRollValueType.Int:
                    rolls[entry.key] = _rollRange(entry.min_value, entry.max_value);
                    break;
                case TraitRollValueType.StringName:
                    if (entry.allowed_values.Count > 0)
                        rolls[entry.key] = (string)entry.allowed_values[_rollRange(0, entry.allowed_values.Count - 1)];
                    break;
                case TraitRollValueType.Bool:
                    rolls[entry.key] = _rollRange(0, 1) == 1;
                    break;
            }
        }
        return rolls;
    }
}
```

创建点接入：

- `EquipmentDropService.RollItemInstances`：只有在 transient 已分配稳定 `instance_id` 时，才能在 `instances.Add(instance)` 前调用 `_traitRollService.MintWithRolls(instance, itemDef)`；如果当前 drop transient 仍为空 id，则先不 mint，交给 commit/warehouse add 的稳定 id 路径 mint。
- `PartyWarehouseService` 直接创建（`:828`）：id allocator 产出稳定 `instance_id` 后 mint。
- `GameSession.CreateInstance`：按调用语境区分。新装备实例且已有稳定 id 才 mint；加载、克隆、预览、AI 投影一律不 mint。
- `EquipmentInstanceState.FromDictionary` / `FromTransientLootDictionary` / `DuplicateState`：解析/拷贝 `trait_instances`，不调用 mint。
- `BattleAiMutationGuard` 实例构造：调用 `DuplicateState()` 拷贝 `trait_instances`，零 RNG。
- `BattleSimFormalCombatFixture` / `HeadlessGameTestSession`：注入预 roll 实例，或用固定种子 `RandomNumberGenerator` 显式 mint。

`ItemDef.MergeWithTemplate()` trait 继承：

- `trait_ids` 用 merge + 去重，模板顺序优先，实例新增项追加。
- `trait_roll_groups` 用 `group_id` 作 key；实例同 key 替换模板 group，新 key 追加。
- 第一版不把空数组解释为清空继承 trait。需要清空语义时先加显式字段，再改 validator 和 tests。

---

## 6. 宿主状态序列化改动

### EquipmentInstanceState

```csharp
// 字段
public Godot.Collections.Array<TraitInstanceState> trait_instances = new();

// CreateInstance / CreateTransientInstance：trait_instances 留空（由 mint 服务填充）。

// ToDictionary 增加：
//   { "trait_instances", TraitInstanceCollection.ToPayloadArray(trait_instances) }

// requiredFields：{ "instance_id","item_id","rarity","current_durability","trait_instances" }
//   精确 count 校验由 4 改 5。

// _get_payload_validation_error 增加：
//   if (payload["trait_instances"].VariantType != Variant.Type.Array)
//       return $"Corrupt {payloadLabel}: trait_instances must be Array.";
//   （逐元素严格性由 FromDictionary 时的 TraitInstanceCollection.FromPayloadArray 兜底）

// _from_dict 构造增加：
//   trait_instances = TraitInstanceCollection.FromPayloadArray(payload["trait_instances"], TraitSourceKind.EquipmentRoll)
//   若为 null 则整体失败（返回 null + GameLog.Error）。

// DuplicateState 增加：
//   trait_instances = TraitInstanceCollection.Duplicate(trait_instances)
```

### PartyMemberState

```csharp
// 字段
public Godot.Collections.Array<TraitInstanceState> trait_instances = new();

// TO_DICT_FIELDS 末尾追加 "trait_instances"
// ToDictionary 增加 { "trait_instances", TraitInstanceCollection.ToPayloadArray(trait_instances) }
// FromDictionary 增加（与现有 _parse_* 同级，任一失败即整体失败）：
//   var traits = TraitInstanceCollection.FromPayloadArray(data["trait_instances"], TraitSourceKind.Character);
//   if (traits == null) return null;
// DuplicateState/clone 路径：TraitInstanceCollection.Duplicate(trait_instances)
```

### 版本

- `PartyState.version` 3 → 4。
- `SaveSerializer._save_version` 7 → 8。
- `GameSession.SaveVersion` 7 → 8。
- `_save_index_version` 不动。

---

## 7. EffectiveTraitSet / EffectiveTraitInstance

`scripts/systems/progression/EffectiveTrait.cs`：

```csharp
using System.Collections.Generic;
using Godot;

public sealed class EffectiveTraitInstance
{
    public StringName TraitId;
    public TraitDef TraitDef;                 // 运行时引用，不序列化
    public TraitInstanceState TraitInstance;  // identity 来源为 null
    public TraitSourceKind SourceKind;
    public StringName SourceId;
    public StringName EffectiveInstanceKey;   // charge key 即用它
    public TraitStackPolicyKind StackPolicy;
    public TraitChargeScopeKind ChargeScope;
    public TraitChargeResetTimingKind ChargeResetTiming;
    public int Rank = 1;
    public int Stacks = 1;
    public Godot.Collections.Array<TraitRollValueState> RollValues = new();
}

public sealed class EffectiveTraitSet
{
    private readonly List<EffectiveTraitInstance> _instances;

    public EffectiveTraitSet(List<EffectiveTraitInstance> instances)
    {
        _instances = instances ?? new();
    }

    public IReadOnlyList<EffectiveTraitInstance> Instances => _instances;

    public bool TryGetByKey(StringName key, out EffectiveTraitInstance inst);

    public IReadOnlyList<EffectiveTraitInstance> GetByTraitId(StringName traitId);

    // 派生投影：去重 + 排序，仅 UI/trace。
    public IReadOnlyList<StringName> DeriveTraitIds()
    {
        // 遍历 _instances 去重 + 排序，不构造 dictionary index。
    }

    // 战斗 typed state：稳定排序保证 clone / save 可复现。
    public Godot.Collections.Array<BattleEffectiveTraitInstanceState> ToBattleEffectiveInstances();
}
```

`TraitDef` 不进入 battle state。开战投影时把 behavior/charge 分发需要的 typed 字段反规范化进 `BattleEffectiveTraitInstanceState`；进行中的 battle save/load 以该 typed state 为行为真相源，不在恢复时查询 `TraitContentRegistry`。party/equipment 非战斗存档仍通过 registry 校验实例 trait。

---

## 8. CharacterTraitService

`scripts/systems/progression/CharacterTraitService.cs`。聚合顺序 `identity -> character -> equipment`，由 `CharacterManagementModule` setup 并提供 def 查询。

```csharp
using System.Collections.Generic;
using Godot;

public sealed class CharacterTraitService
{
    public interface IIdentityDefGateway
    {
        RaceDef GetRaceDef(StringName memberId);
        SubraceDef GetSubraceDef(StringName memberId);
        BloodlineDef GetBloodlineDef(StringName memberId);
        BloodlineStageDef GetBloodlineStageDef(StringName memberId);
        AscensionDef GetAscensionDef(StringName memberId);
        AscensionStageDef GetAscensionStageDef(StringName memberId);
        PartyMemberState GetMemberState(StringName memberId);
        EquipmentState GetEquipmentState(StringName memberId);     // 可被 override 替换
        ItemDef GetItemDef(StringName itemId);
        IReadOnlyList<StringName> GetEquippedEntrySlotIds(StringName memberId);
    }

    private readonly TraitContentRegistry _traitRegistry;
    private readonly IIdentityDefGateway _gateway;

    public CharacterTraitService(TraitContentRegistry traitRegistry, IIdentityDefGateway gateway)
    {
        _traitRegistry = traitRegistry;
        _gateway = gateway;
    }

    public EffectiveTraitSet BuildEffectiveTraits(StringName memberId, EquipmentState equipmentOverride = null)
    {
        var raw = new List<EffectiveTraitInstance>();
        _collectIdentity(memberId, raw);
        _collectCharacter(memberId, raw);
        _collectEquipment(memberId, equipmentOverride, raw);
        return new EffectiveTraitSet(_applyStackPolicies(raw));
    }

    public List<AttributeModifier> ResolveTraitAttributeModifiers(EffectiveTraitSet set)
    {
        var result = new List<AttributeModifier>();
        foreach (EffectiveTraitInstance eff in set.Instances)
        {
            if (eff.TraitDef == null) continue;
            StringName srcType = TraitContentRules.ToAttributeSourceType(eff.SourceKind);
            foreach (AttributeModifier baseMod in eff.TraitDef.attribute_modifiers)
            {
                if (baseMod == null || baseMod.attribute_id == "") continue;
                // v1 不把 roll_values 隐式应用到 AttributeModifier；需要随机属性时先加显式 binding schema。
                result.Add(new AttributeModifier
                {
                    attribute_id = baseMod.attribute_id,
                    mode = baseMod.mode,
                    // additive 把 stacks 折进 rank；其余 stacks=1。
                    value = baseMod.GetValueForRank(eff.Rank) * Mathf.Max(eff.Stacks, 1),
                    value_per_rank = 0,
                    source_type = srcType,
                    source_id = eff.EffectiveInstanceKey,  // trace 回溯到具体实例
                });
            }
        }
        return result;
    }

    // ---- identity：镜像 RaceTraitResolver（race+subrace，可被 ascension 抑制）
    //      + AscensionTraitResolver（bloodline+ascension，始终）----
    private void _collectIdentity(StringName memberId, List<EffectiveTraitInstance> raw)
    {
        AscensionDef asc = _gateway.GetAscensionDef(memberId);
        bool suppressRace = asc != null && asc.suppresses_original_race_traits;

        if (!suppressRace)
        {
            _addIdentityDef(raw, _gateway.GetRaceDef(memberId)?.trait_ids, RaceSourceId(_gateway.GetRaceDef(memberId)));
            _addIdentityDef(raw, _gateway.GetSubraceDef(memberId)?.trait_ids, SubraceSourceId(_gateway.GetSubraceDef(memberId)));
        }
        _addIdentityDef(raw, _gateway.GetBloodlineDef(memberId)?.trait_ids, _gateway.GetBloodlineDef(memberId)?.bloodline_id);
        _addIdentityDef(raw, _gateway.GetBloodlineStageDef(memberId)?.trait_ids, _gateway.GetBloodlineStageDef(memberId)?.stage_id);
        _addIdentityDef(raw, asc?.trait_ids, asc?.ascension_id);
        _addIdentityDef(raw, _gateway.GetAscensionStageDef(memberId)?.trait_ids, _gateway.GetAscensionStageDef(memberId)?.stage_id);
    }

    private static StringName RaceSourceId(RaceDef d) => d != null ? d.race_id : "";
    private static StringName SubraceSourceId(SubraceDef d) => d != null ? d.subrace_id : "";

    private void _addIdentityDef(List<EffectiveTraitInstance> raw,
        Godot.Collections.Array<StringName> traitIds, StringName sourceId)
    {
        if (traitIds == null || sourceId == null || sourceId == "") return;
        foreach (StringName rawId in traitIds)
        {
            StringName traitId = ProgressionDataUtils.to_string_name(rawId);
            TraitDef def = _traitRegistry.GetTraitDef(traitId);
            if (def == null) continue; // validator 已保证存在；运行时缺失则跳过（不静默 fallback id）
            if (!TraitContentRules.IsSourceKindAllowed(def, TraitSourceKind.Identity)) continue;
            raw.Add(new EffectiveTraitInstance
            {
                TraitId = traitId,
                TraitDef = def,
                TraitInstance = null,
                SourceKind = TraitSourceKind.Identity,
                SourceId = sourceId,
                EffectiveInstanceKey = _rawKey(TraitSourceKind.Identity, sourceId, traitId, null),
                StackPolicy = def.StackPolicyKind,
                ChargeScope = def.ChargeScopeKind,
                ChargeResetTiming = def.ChargeResetTimingKind,
                Rank = 1,
                Stacks = 1,
                RollValues = new Godot.Collections.Dictionary(),
            });
        }
    }

    private void _collectCharacter(StringName memberId, List<EffectiveTraitInstance> raw)
    {
        PartyMemberState member = _gateway.GetMemberState(memberId);
        if (member == null) return;
        foreach (TraitInstanceState inst in member.trait_instances)
            _addInstance(raw, inst, TraitSourceKind.Character);
    }

    private void _collectEquipment(StringName memberId, EquipmentState equipmentOverride, List<EffectiveTraitInstance> raw)
    {
        EquipmentState equip = equipmentOverride ?? _gateway.GetEquipmentState(memberId);
        if (equip == null) return;
        foreach (StringName entrySlot in equip.GetEntrySlotIdsTyped())
        {
            var entry = equip.GetEntry(entrySlot);
            if (entry == null || entry.instance_id == "") continue;
            EquipmentInstanceState inst = entry.equipment_instance; // 装备实例（含 roll trait_instances）
            ItemDef itemDef = _gateway.GetItemDef(entry.item_id);
            if (itemDef == null) continue;

            // 固定特性：由 ItemDef.trait_ids 派生（不复制进实例）。
            foreach (StringName rawId in itemDef.trait_ids)
            {
                StringName traitId = ProgressionDataUtils.to_string_name(rawId);
                TraitDef def = _traitRegistry.GetTraitDef(traitId);
                if (def == null) continue;
                if (!TraitContentRules.IsSourceKindAllowed(def, TraitSourceKind.EquipmentFixed)) continue;
                raw.Add(new EffectiveTraitInstance
                {
                    TraitId = traitId,
                    TraitDef = def,
                    TraitInstance = null,
                    SourceKind = TraitSourceKind.EquipmentFixed,
                    // item_id 是模板元数据；source_id/key 必须用装备实例 id，避免两件同模板装备碰撞。
                    SourceId = entry.instance_id,
                    EffectiveInstanceKey = _rawKey(TraitSourceKind.EquipmentFixed, entry.instance_id, traitId, null),
                    StackPolicy = def.StackPolicyKind,
                    ChargeScope = def.ChargeScopeKind,
                    ChargeResetTiming = def.ChargeResetTimingKind,
                    Rank = 1, Stacks = 1,
                    RollValues = new Godot.Collections.Dictionary(),
                });
            }
            // 随机特性：来自实例 trait_instances。
            if (inst != null)
                foreach (TraitInstanceState ti in inst.trait_instances)
                    _addInstance(raw, ti, TraitSourceKind.EquipmentRoll);
        }
    }

    private void _addInstance(List<EffectiveTraitInstance> raw, TraitInstanceState inst, TraitSourceKind expected)
    {
        if (inst == null || inst.SourceKind != expected) return;
        TraitDef def = _traitRegistry.GetTraitDef(inst.trait_id);
        if (def == null) return;
        if (!TraitContentRules.IsSourceKindAllowed(def, expected)) return;
        string rollError = inst.ValidateAgainstDef(def);
        if (rollError.Length > 0)
        {
            GameLog.Error(rollError, "trait.roll_values.invalid", "progression");
            return;
        }
        raw.Add(new EffectiveTraitInstance
        {
            TraitId = inst.trait_id,
            TraitDef = def,
            TraitInstance = inst,
            SourceKind = expected,
            SourceId = inst.source_id,
            EffectiveInstanceKey = _rawKey(expected, inst.source_id, inst.trait_id, inst.trait_instance_id),
            StackPolicy = def.StackPolicyKind,
            ChargeScope = def.ChargeScopeKind,
            ChargeResetTiming = def.ChargeResetTimingKind,
            Rank = Mathf.Max(inst.rank, 1),
            Stacks = Mathf.Max(inst.stacks, 1),
            RollValues = TraitInstanceState.NormalizeRollValues(inst.roll_values),
        });
    }

    // raw key（stack 收敛前）：identity -> identity::{source_id}::{trait_id}；
    // equipment_fixed -> equipment_fixed::{equipment_instance_id}::{trait_id}；
    // character/equipment_roll -> trait_instance_id（稳定持久）。
    private static StringName _rawKey(TraitSourceKind kind, StringName sourceId, StringName traitId, StringName instanceId)
    {
        if ((kind == TraitSourceKind.Character || kind == TraitSourceKind.EquipmentRoll)
            && instanceId != null && instanceId != "")
            return instanceId;
        return new StringName($"{(string)TraitContentRules.ToStringName(kind)}::{(string)sourceId}::{(string)traitId}");
    }

    // 按 trait_id + stack policy 收敛，产出最终 EffectiveInstanceKey（charge key）。
    private List<EffectiveTraitInstance> _applyStackPolicies(List<EffectiveTraitInstance> raw)
    {
        var groups = new List<List<EffectiveTraitInstance>>();
        var order = new List<StringName>();
        foreach (var e in raw)
        {
            int index = order.IndexOf(e.TraitId);
            if (index < 0)
            {
                order.Add(e.TraitId);
                groups.Add(new List<EffectiveTraitInstance>());
                index = groups.Count - 1;
            }
            groups[index].Add(e);
        }

        var result = new List<EffectiveTraitInstance>();
        for (int index = 0; index < order.Count; index++)
        {
            StringName traitId = order[index];
            var list = groups[index];
            TraitStackPolicyKind policy = list[0].StackPolicy;
            switch (policy)
            {
                case TraitStackPolicyKind.UniqueByTrait:
                    result.Add(_collapseToTraitId(list[0], traitId, stacks: 1));
                    break;
                case TraitStackPolicyKind.HighestRoll:
                    result.Add(_collapseToTraitId(_pickHighestRoll(list), traitId, stacks: 1));
                    break;
                case TraitStackPolicyKind.Additive:
                    int sumStacks = 0;
                    foreach (var e in list) sumStacks += Mathf.Max(e.Stacks, 1);
                    result.Add(_collapseToTraitId(list[0], traitId, stacks: sumStacks));
                    break;
                case TraitStackPolicyKind.StackByInstance:
                    foreach (var e in list) result.Add(e); // 各自保留 raw key
                    break;
                default:
                    result.Add(_collapseToTraitId(list[0], traitId, stacks: 1));
                    break;
            }
        }
        return result;
    }

    private static EffectiveTraitInstance _collapseToTraitId(EffectiveTraitInstance src, StringName traitId, int stacks)
    {
        return new EffectiveTraitInstance
        {
            TraitId = traitId,
            TraitDef = src.TraitDef,
            TraitInstance = src.TraitInstance,
            SourceKind = src.SourceKind,
            SourceId = src.SourceId,
            EffectiveInstanceKey = traitId, // 单触发单元，charge key = trait_id（与旧行为兼容）
            StackPolicy = src.StackPolicy,
            ChargeScope = src.ChargeScope,
            ChargeResetTiming = src.ChargeResetTiming,
            Rank = src.Rank,
            Stacks = Mathf.Max(stacks, 1),
            RollValues = src.RollValues != null ? src.RollValues.Duplicate(true) : new Godot.Collections.Dictionary(),
        };
    }

    private static EffectiveTraitInstance _pickHighestRoll(List<EffectiveTraitInstance> list)
    {
        StringName compareKey = list[0].TraitDef.GetHighestRollCompareKey();
        EffectiveTraitInstance best = list[0];
        int bestVal = best.TraitInstance?.GetIntRoll(compareKey, int.MinValue) ?? int.MinValue;
        foreach (var e in list)
        {
            int v = e.TraitInstance?.GetIntRoll(compareKey, int.MinValue) ?? int.MinValue;
            // 平局用 raw key ordinal 决定，保证确定性。
            if (v > bestVal || (v == bestVal &&
                string.CompareOrdinal(e.EffectiveInstanceKey.ToString(), best.EffectiveInstanceKey.ToString()) < 0))
            { best = e; bestVal = v; }
        }
        return best;
    }
}
```

`CharacterManagementModule.build_attribute_source_context()` 末尾：

```csharp
var effective = _character_trait_service.BuildEffectiveTraits(member_id, equipment_state_override);
context.trait_attribute_modifiers = _character_trait_service.ResolveTraitAttributeModifiers(effective);
```

> 注：`IIdentityDefGateway` 的实现就是 `CharacterManagementModule` 自己（它已有 `GetRaceDefForMember` 等私有方法），仅需暴露为接口或直接把 `CharacterManagementModule` 传给服务。`EquipmentEntryState` 字段名已核实：`item_id` / `instance_id` / `equipment_instance`（或 `GetEquipmentInstance()`）。

---

## 9. AttributeService 接入

`AttributeSourceContext` 新增字段：

```csharp
public List<AttributeModifier> trait_attribute_modifiers = new();
```

`AttributeService`：

```csharp
// 字段
private List<AttributeModifier> _trait_state = new();

// SetupContext 内：
_trait_state = CopyAttributeModifierList(_context.trait_attribute_modifiers);
_context.trait_attribute_modifiers = _trait_state;

// CollectAllModifierEntries 内追加（注意：用 sourceType="" 让每条 modifier 自带的 source_type/source_id 生效）：
AppendTraitModifierEntries(entries, _trait_state);

private static void AppendTraitModifierEntries(List<AttributeModifierEntry> entries, List<AttributeModifier> state)
{
    if (state == null || state.Count == 0) return;
    // sourceType/sourceId 传 ""，AppendModifierEntry 会回落到 modifier.source_type / source_id。
    AppendModifierEntries(entries, state, "", "", 1);
}
```

`AppendModifierEntry` 现有逻辑 `sourceType != "" ? sourceType : modifier.source_type` 已支持这种回落，无需改动。

---

## 10. BattleUnitState 改动（Phase 4a additive / 4b 删旧）

```csharp
// 新字段
public Godot.Collections.Array<BattleEffectiveTraitInstanceState> effective_trait_instances = new();
public GStringNameArray effective_trait_ids = new();

// 旧身份 trait 四字段不再作为 battle unit state 平行字段保留；
// effective_trait_ids 只能从 effective_trait_instances 派生。

// ToDictionary 增加：
["effective_trait_instances"] = EffectiveTraitInstancesToPayloadArray(effective_trait_instances),
["effective_trait_ids"] = StringNameArrayToStrings(effective_trait_ids),

// FromDictionary 增加严格解析：
GEffectiveTraitArray parsedEffective = EffectiveTraitInstancesFromPayloadArray(
    GetArray(payload, "effective_trait_instances"));
if (parsedEffective == null) return null;
GStringNameArray parsedEffectiveIds = _unique_string_name_array_from_payload(
    GetArray(payload, "effective_trait_ids"));
if (parsedEffectiveIds == null) return null;
// 一致性：effective_trait_ids 必须等于 typed state 去重派生集合（防止两字段不一致）。
if (!StringNameSetEquals(parsedEffectiveIds, DeriveEffectiveTraitIdsFromInstances(parsedEffective))) return null;

// Clone 增加：
effective_trait_instances = DuplicateEffectiveTraitInstances(effective_trait_instances),
effective_trait_ids = DuplicateStringNameArray(effective_trait_ids),
```

辅助（trait_def 不入 save payload；dictionary 只存在于 `ToDictionary()/FromDictionary()` save 边界，进入内存后必须是 typed state）：

```csharp
private static GEffectiveTraitArray EffectiveTraitInstancesFromPayloadArray(GArray raw)
{
    if (raw == null) return null;
    GEffectiveTraitArray result = new();
    var seenKeys = new HashSet<string>();
    foreach (Variant entry in raw)
    {
        if (entry.VariantType != Variant.Type.Dictionary) return null;
        GDictionary d = entry.AsGodotDictionary();
        var parsed = BattleEffectiveTraitInstanceState.FromDictionary(d);
        if (parsed == null) return null;
        if (!seenKeys.Add(parsed.effective_instance_key.ToString())) return null;
        result.Add(parsed);
    }
    return result;
}

public static GEffectiveTraitArray DuplicateEffectiveTraitInstances(GEffectiveTraitArray source)
{
    GEffectiveTraitArray result = new();
    if (source == null) return result;
    foreach (BattleEffectiveTraitInstanceState entry in source)
        result.Add(entry?.DuplicateState());
    return result;
}

private static GArray EffectiveTraitInstancesToPayloadArray(GEffectiveTraitArray source)
{
    GArray result = new();
    if (source == null) return result;
    foreach (BattleEffectiveTraitInstanceState entry in source)
        result.Add(entry.ToDictionary());
    return result;
}
```

投影写入（`BattleUnitFactory` 开战 / `RefreshBattleUnit` / `RefreshEquipmentProjection`）：

```csharp
EffectiveTraitSet set = characterTraitService.BuildEffectiveTraits(memberId, equipmentOverride);
unitState.effective_trait_instances = set.ToBattleEffectiveInstances();
unitState.effective_trait_ids = ToStringNameArray(set.DeriveTraitIds());
```

`BattleAiMutationGuard`：把 `effective_trait_instances` 纳入 stable diff / restore，并深拷贝 typed state（`DuplicateEffectiveTraitInstances`），零 RNG；不得把 effective trait 重新退回 `GDictionary` payload 做 AI 快照。

---

## 11. TraitTriggerHooks 改动

`GetUnitTraitIds(unitState)`（拼接旧四数组）替换为从 `effective_trait_instances` typed state 读取有效实例并按 trigger 过滤：

```csharp
private readonly struct UnitEffectiveTrait
{
    public readonly StringName TraitId;
    public readonly StringName EffectiveInstanceKey;
    public readonly TraitEffectKind EffectKind;
    public readonly TraitTriggerKind TriggerKind;
    public readonly TraitChargeScopeKind ChargeScope;
    public readonly TraitChargeResetTimingKind ChargeResetTiming;
    public readonly Godot.Collections.Array<TraitRollValueState> RollValues;
    // ctor 略
}

private static List<UnitEffectiveTrait> GetEffectiveInstances(BattleUnitState unit, TraitTriggerKind triggerKind)
{
    var result = new List<UnitEffectiveTrait>();
    if (unit == null) return result;
    foreach (Variant entry in unit.effective_trait_instances)
    {
        GDictionary d = entry.AsGodotDictionary();
        // effect_type / trigger_type 已反规范化进 payload —— 不查 TraitContentRegistry（sim 隔离）。
        if (TraitTriggerContentRules.ToTriggerKind(
                ProgressionDataUtils.to_string_name(d["trigger_type"])) != triggerKind)
            continue;
        result.Add(new UnitEffectiveTrait(
            ProgressionDataUtils.to_string_name(d["trait_id"]),
            ProgressionDataUtils.to_string_name(d["effective_instance_key"]),
            TraitContentRules.ToEffectKind(ProgressionDataUtils.to_string_name(d["effect_type"])),
            TraitTriggerContentRules.ToTriggerKind(ProgressionDataUtils.to_string_name(d["trigger_type"])),
            TraitContentRules.ToChargeScopeKind(ProgressionDataUtils.to_string_name(d["charge_scope"])),
            TraitContentRules.ToChargeResetTimingKind(ProgressionDataUtils.to_string_name(d["charge_reset_timing"])),
            d["roll_values"].AsGodotDictionary()));
    }
    return result;
}
```

分发（以 `OnNaturalOne` 为例，charge key 用 `EffectiveInstanceKey`）：

```csharp
public AttackTraitTriggerResult OnNaturalOne(BattleUnitState unit, int roll, int dieSize)
{
    foreach (UnitEffectiveTrait eff in GetEffectiveInstances(unit, TraitTriggerKind.OnNaturalOne))
    {
        if (eff.EffectKind != TraitEffectKind.HalflingLuck) continue;
        AttackTraitTriggerResult result = _handle_halfling_luck_typed(unit, roll, dieSize, eff.EffectiveInstanceKey);
        if (result.Triggered) return result;
    }
    return new AttackTraitTriggerResult(@event: TriggerOnNaturalOne);
}
```

`_handle_halfling_luck_typed` / `HandleRelentlessEnduranceTyped` 等：把内部 `GetTraitChargeKey(traitId)` 改为传入的 `effectiveInstanceKey`。`AttackTraitTriggerResult` 的 payload 字段不变，仅 `charge_key` 取值来源改变。

charge 初始化不能复用 `GetEffectiveInstances(unit, OnBattleStart/OnTurnStart)`，否则 `trigger_type=passive` 但需要 per-battle/per-turn charge 的 trait 会漏 seed。新增一个不按 trigger 过滤的枚举：

```csharp
private static List<UnitEffectiveTrait> GetChargeBearingInstances(
    BattleUnitState unit, TraitChargeResetTimingKind timing)
{
    var result = new List<UnitEffectiveTrait>();
    foreach (UnitEffectiveTrait eff in GetAllEffectiveInstances(unit))
    {
        if (eff.ChargeScope == TraitChargeScopeKind.None) continue;
        if (eff.ChargeResetTiming != timing) continue;
        result.Add(eff);
    }
    return result;
}
```

`OnBattleStartResult` 遍历 `GetChargeBearingInstances(unit, BattleStart)`，`OnTurnStartResult` 遍历 `GetChargeBearingInstances(unit, TurnStart)`。行为 handler 仍走 `GetEffectiveInstances(unit, triggerKind)`。

---

## 12. Charge 生命周期（中途换装）

`RefreshEquipmentProjection()` 重聚合后：

```csharp
// 重投影前快照旧有效 key 集合，重投影后对比：
var before = CollectEffectiveChargeKeys(unit);        // 旧 typed state 的 (key, charge_scope, reset_timing)
unit.effective_trait_instances = set.ToBattleEffectiveInstances();
unit.effective_trait_ids = ToStringNameArray(set.DeriveTraitIds());
var after = CollectEffectiveChargeKeys(unit);

// 新增的 charge-bearing key：按 charge policy 补种。
foreach (var k in after.Except(before)) traitTriggerHooks.SeedChargeForKey(unit, k);
// 移除的 key：清理 per_battle_charges / per_turn_charges 中对应条目。
foreach (var k in before.Except(after)) ClearChargeForKey(unit, k);
```

`SeedChargeForKey` 根据 payload 中的 `charge_scope` / `charge_reset_timing` 给单个 key 种对应 charge；它不检查 `trigger_type`。racial skill charge 仍由 passive 链维护，key 命名空间 `racial_skill_*` 与 trait charge（`effective_instance_key`）不重叠。

---

## 13. 落码顺序内的可编译切点

每个切点结束应能 `dotnet build magic.csproj` 通过：

1. 第 1、2、4 节（enum/rules、TraitDef、registry）——纯新增，独立可编译。
2. 第 3、6 节（TraitInstanceState + 宿主序列化 + 版本）——新增字段 + 测试。
3. 第 7、8、9 节（DTO、CharacterTraitService、AttributeService 接入）。
4. 第 5 节（roll service）+ 创建点接入。
5. 第 10、11、12 节（BattleUnitState 4a additive、TraitTriggerHooks、charge 生命周期），parity 绿后 4b 删旧字段。

Phase 5 清理额外切点：

- `.tres` 资源从 `RaceTraitDef.cs` / `script_class="RaceTraitDef"` 改到 `TraitDef.cs`，并移动到 `res://data/configs/traits`。
- `scripts/ui/CharacterCreationWindow.cs` 中 Human Versatility 等 race trait/effect 查询改为 `TraitDef` / `TraitContentRegistry` 或 identity summary。
- 删除旧 `race_trait_defs` bucket 前跑内容加载 parity，确认无旧路径引用。

## 已核实（无需再查）

- `EquipmentEntryState`：`item_id` / `instance_id` / `equipment_instance` / `GetEquipmentInstance()` 字段存在。
- `IdentityContentRegistryBase`：`_registry_label` / `_validation_errors` / `_scan_directory` / `_sorted_registry_keys` / `_append_string_name_field_error` / `_append_string_field_error` 均存在，调用签名一致。
- `GameLog.Error(message, code, category)` 三参调用与 `EquipmentInstanceState` 对齐。
- `AttributeService.AppendModifierEntry` 已有 `sourceType != "" ? sourceType : modifier.source_type` 回落，trait 接入用 `sourceType=""` 即可保留每条 modifier 自带 source。

## 待落码确认项（Codex 审查关注）

- `RandomNumberGenerator.Randf()` 在固定种子下的可复现性（battle-sim 多进程要求）；测试用 `SetRollHooksForTesting` 注入确定性序列绕开，生产掉落用世界 rng。
- `BattleUnitState` 是否存在除 `HasExactFields(payload, ToDictFields)` 外、针对 trait 字段的二级 strict 校验需在 4b 删旧字段时同步移除。
- `EquipmentState.GetEntry(entrySlot)` 与 `GetEntrySlotIdsTyped()` 在「未装备空槽」下的返回（`null` / 空 `instance_id`）已在 `_collectEquipment` 做空判，确认无其它哨兵值。
- roll-backed attribute modifiers 第一版不隐式支持；如果内容确实需要随机属性值，先设计显式 binding schema，再解除 validator 限制。
