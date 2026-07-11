using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public enum ContingencyReleaseModeKind
{
    Unknown = 0,
    BurstRelease,
    SequentialRelease,
}

public class ContingencyMatrixSetupState
{
    private static readonly string[] PayloadKeys =
    {
        "setup_id",
        "display_name",
        "enabled",
        "charged",
        "source_skill_id",
        "source_skill_level",
        "matrix_load",
        "reserved_mp_max",
        "material_costs",
        "trigger",
        "release_mode",
        "stored_spells",
    };

    public StringName SetupId { get; private set; } = "";
    public string DisplayName { get; private set; } = "";
    public bool Enabled { get; private set; }
    public bool Charged { get; private set; }
    public StringName SourceSkillId { get; private set; } = "";
    public int SourceSkillLevel { get; private set; }
    public int MatrixLoad { get; private set; }
    public int ReservedMpMax { get; private set; }
    public ContingencyTriggerState Trigger { get; private set; }
    public ContingencyReleaseModeKind ReleaseModeKind { get; private set; } =
        ContingencyReleaseModeKind.Unknown;
    public StringName ReleaseMode { get; private set; } = "";

    private readonly List<ContingencyStoredSpellEntryState> _storedSpells = new();
    private readonly List<ContingencyMaterialCostState> _materialCosts = new();

    public IReadOnlyList<ContingencyStoredSpellEntryState> StoredSpells => _storedSpells;
    public IReadOnlyList<ContingencyMaterialCostState> MaterialCosts => _materialCosts;

    internal ContingencyMatrixSetupState WithChargeState(
        bool charged,
        int reservedMpMax,
        IReadOnlyList<ContingencyMaterialCostState> materialCosts
    )
    {
        if (!Enabled && charged)
            return null;
        if (!charged && reservedMpMax != 0)
            return null;
        if (charged && reservedMpMax <= 0)
            return null;

        var costCopies = new List<ContingencyMaterialCostState>();
        foreach (
            ContingencyMaterialCostState cost in materialCosts
                ?? System.Array.Empty<ContingencyMaterialCostState>()
        )
        {
            ContingencyMaterialCostState copy = cost?.DuplicateState();
            if (copy == null)
                return null;
            costCopies.Add(copy);
        }
        if (!charged && costCopies.Count != 0)
            return null;

        ContingencyMatrixSetupState state = DuplicateState();
        state.Charged = charged;
        state.ReservedMpMax = reservedMpMax;
        state._materialCosts.Clear();
        state._materialCosts.AddRange(costCopies);
        return state;
    }

    public ContingencyMatrixSetupState DuplicateState()
    {
        var state = new ContingencyMatrixSetupState
        {
            SetupId = SetupId,
            DisplayName = DisplayName,
            Enabled = Enabled,
            Charged = Charged,
            SourceSkillId = SourceSkillId,
            SourceSkillLevel = SourceSkillLevel,
            MatrixLoad = MatrixLoad,
            ReservedMpMax = ReservedMpMax,
            Trigger = Trigger?.DuplicateState(),
            ReleaseModeKind = ReleaseModeKind,
            ReleaseMode = ReleaseMode,
        };
        foreach (ContingencyMaterialCostState cost in _materialCosts)
            state._materialCosts.Add(cost?.DuplicateState());
        foreach (ContingencyStoredSpellEntryState spell in _storedSpells)
            state._storedSpells.Add(spell?.DuplicateState());
        return state;
    }

    internal Dictionary<string, object> BuildSnapshotPlain()
    {
        var costs = new List<object>();
        foreach (ContingencyMaterialCostState cost in _materialCosts)
        {
            costs.Add(
                cost != null
                    ? new Dictionary<string, object>(System.StringComparer.Ordinal)
                    {
                        ["item_id"] = cost.ItemId.ToString(),
                        ["quantity"] = cost.Quantity,
                    }
                    : new Dictionary<string, object>(System.StringComparer.Ordinal)
            );
        }

        var spells = new List<object>();
        foreach (ContingencyStoredSpellEntryState spell in _storedSpells)
        {
            spells.Add(
                spell?.BuildSnapshotPlain()
                    ?? new Dictionary<string, object>(System.StringComparer.Ordinal)
            );
        }

        return new Dictionary<string, object>(System.StringComparer.Ordinal)
        {
            ["setup_id"] = SetupId.ToString(),
            ["display_name"] = DisplayName,
            ["enabled"] = Enabled,
            ["charged"] = Charged,
            ["source_skill_id"] = SourceSkillId.ToString(),
            ["source_skill_level"] = SourceSkillLevel,
            ["matrix_load"] = MatrixLoad,
            ["reserved_mp_max"] = ReservedMpMax,
            ["material_costs"] = costs,
            ["trigger"] =
                Trigger?.BuildSnapshotPlain()
                ?? new Dictionary<string, object>(System.StringComparer.Ordinal),
            ["release_mode"] = ReleaseMode.ToString(),
            ["stored_spells"] = spells,
        };
    }

    internal GodotProjectionLease<GDictionary> ToDictionaryLease() =>
        RuntimePlainPayload.ProjectDictionaryLease(
            BuildSnapshotPlain(),
            "ContingencyMatrixSetupState.ToDictionary",
            LifetimeDomain.Request,
            "ContingencyMatrixSetupState.ToDictionary"
        );

    public static ContingencyMatrixSetupState FromDictionary(GDictionary payload)
    {
        if (!ContingencySchemaUtils.HasExactKeys(payload, PayloadKeys))
            return null;
        if (!ContingencySchemaUtils.TryReadStringName(payload, "setup_id", false, out StringName setupId))
            return null;
        if (!ContingencySchemaUtils.TryReadStrictString(payload, "display_name", out string displayName))
            return null;
        if (displayName.StripEdges().Length == 0)
            return null;
        if (!ContingencySchemaUtils.TryReadBool(payload, "enabled", out bool enabled))
            return null;
        if (!ContingencySchemaUtils.TryReadBool(payload, "charged", out bool charged))
            return null;
        if (!ContingencySchemaUtils.TryReadStringName(payload, "source_skill_id", false, out StringName sourceSkillId))
            return null;
        if (
            !ContingencySchemaUtils.TryReadInt(
                payload,
                "source_skill_level",
                out int sourceSkillLevel
            )
            || sourceSkillLevel <= 0
        )
            return null;
        if (!ContingencySchemaUtils.TryReadInt(payload, "matrix_load", out int matrixLoad) || matrixLoad <= 0)
            return null;
        if (
            !ContingencySchemaUtils.TryReadInt(
                payload,
                "reserved_mp_max",
                out int reservedMpMax
            )
            || reservedMpMax < 0
        )
            return null;
        if (!ContingencySchemaUtils.TryReadArray(payload, "material_costs", out GArray costPayloads))
            return null;
        List<ContingencyMaterialCostState> materialCosts = ParseMaterialCosts(costPayloads);
        if (materialCosts == null)
            return null;
        if (!ContingencySchemaUtils.TryReadDictionary(payload, "trigger", out GDictionary triggerPayload))
            return null;
        ContingencyTriggerState trigger = ContingencyTriggerState.FromDictionary(triggerPayload);
        if (trigger == null)
            return null;
        if (!ContingencySchemaUtils.TryReadStringName(payload, "release_mode", false, out StringName releaseMode))
            return null;
        ContingencyReleaseModeKind releaseKind = ToReleaseModeKind(releaseMode);
        if (releaseKind == ContingencyReleaseModeKind.Unknown)
            return null;
        if (!ContingencySchemaUtils.TryReadArray(payload, "stored_spells", out GArray spellPayloads))
            return null;
        List<ContingencyStoredSpellEntryState> storedSpells = ParseStoredSpells(spellPayloads);
        if (storedSpells == null || storedSpells.Count == 0)
            return null;
        if (!enabled && charged)
            return null;
        if (!charged && (reservedMpMax != 0 || materialCosts.Count != 0))
            return null;
        if (charged && reservedMpMax <= 0)
            return null;

        ContingencyMatrixSetupState state = new()
        {
            SetupId = setupId,
            DisplayName = displayName,
            Enabled = enabled,
            Charged = charged,
            SourceSkillId = sourceSkillId,
            SourceSkillLevel = sourceSkillLevel,
            MatrixLoad = matrixLoad,
            ReservedMpMax = reservedMpMax,
            Trigger = trigger,
            ReleaseModeKind = releaseKind,
            ReleaseMode = releaseMode,
        };
        state._materialCosts.AddRange(materialCosts);
        state._storedSpells.AddRange(storedSpells);
        return state;
    }

    internal static StringName ToStringName(ContingencyReleaseModeKind kind)
    {
        return kind switch
        {
            ContingencyReleaseModeKind.BurstRelease => "burst_release",
            ContingencyReleaseModeKind.SequentialRelease => "sequential_release",
            _ => new StringName(""),
        };
    }

    private static ContingencyReleaseModeKind ToReleaseModeKind(StringName mode)
    {
        if (mode == "burst_release")
            return ContingencyReleaseModeKind.BurstRelease;
        if (mode == "sequential_release")
            return ContingencyReleaseModeKind.SequentialRelease;
        return ContingencyReleaseModeKind.Unknown;
    }

    private static List<ContingencyMaterialCostState> ParseMaterialCosts(GArray payloads)
    {
        List<ContingencyMaterialCostState> result = new();
        foreach (Variant rawValue in payloads)
        {
            if (rawValue.VariantType != Variant.Type.Dictionary)
                return null;
            ContingencyMaterialCostState cost =
                ContingencyMaterialCostState.FromDictionary(rawValue.AsGodotDictionary());
            if (cost == null)
                return null;
            result.Add(cost);
        }
        return result;
    }

    private static List<ContingencyStoredSpellEntryState> ParseStoredSpells(GArray payloads)
    {
        List<ContingencyStoredSpellEntryState> result = new();
        HashSet<int> seenOrders = new();
        foreach (Variant rawValue in payloads)
        {
            if (rawValue.VariantType != Variant.Type.Dictionary)
                return null;
            ContingencyStoredSpellEntryState spell =
                ContingencyStoredSpellEntryState.FromDictionary(rawValue.AsGodotDictionary());
            if (spell == null || !seenOrders.Add(spell.Order))
                return null;
            result.Add(spell);
        }
        result.Sort((left, right) => left.Order.CompareTo(right.Order));
        return result;
    }
}
