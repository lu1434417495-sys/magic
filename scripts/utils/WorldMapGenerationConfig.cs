using Godot;

[GlobalClass]
public partial class WorldMapGenerationConfig : Resource
{
    [Export]
    public int seed { get; set; } = 20260403;

    [Export]
    public Vector2I world_size_in_chunks { get; set; } = new(2, 2);

    [Export]
    public Vector2I chunk_size { get; set; } = new(8, 8);

    [Export]
    public Vector2I player_start_coord { get; set; } = new(1, 1);

    [Export]
    public int player_vision_range { get; set; } = 4;

    [Export]
    public bool procedural_generation_enabled { get; set; } = false;

    [Export]
    public int procedural_wild_spawn_chunk_chance_denominator { get; set; } = 2;

    [Export]
    public bool inject_default_main_world_content { get; set; } = false;

    [Export]
    public int procedural_village_count { get; set; } = 1;

    [Export]
    public int procedural_town_count { get; set; } = 1;

    [Export]
    public int procedural_city_count { get; set; } = 1;

    [Export]
    public int procedural_capital_count { get; set; } = 1;

    [Export]
    public int procedural_world_stronghold_count { get; set; } = 1;

    [Export]
    public int procedural_metropolis_count { get; set; } = 0;

    [Export]
    public int village_spacing_cells { get; set; } = 80;

    [Export]
    public int town_spacing_cells { get; set; } = 110;

    [Export]
    public int city_spacing_cells { get; set; } = 150;

    [Export]
    public int capital_spacing_cells { get; set; } = 220;

    [Export]
    public int world_stronghold_spacing_cells { get; set; } = 280;

    [Export]
    public int metropolis_spacing_cells { get; set; } = 340;

    [Export]
    public bool guarantee_starting_wild_encounter { get; set; } = false;

    [Export]
    public int starting_wild_spawn_min_distance { get; set; } = 3;

    [Export]
    public int starting_wild_spawn_max_distance { get; set; } = 4;

    [Export]
    public Godot.Collections.Array<Resource> settlement_library { get; set; } = new();

    [Export]
    public Godot.Collections.Array<Resource> facility_library { get; set; } = new();

    [Export]
    public Godot.Collections.Array<Resource> settlement_distribution { get; set; } = new();

    [Export]
    public Godot.Collections.Array<Resource> wild_monster_distribution { get; set; } = new();

    [Export]
    public Godot.Collections.Array<Resource> mounted_submaps { get; set; } = new();

    [Export]
    public Godot.Collections.Array<Resource> world_events { get; set; } = new();

    internal Godot.Collections.Array<Resource> SettlementLibraryProjectionBorrowed =>
        settlement_library;
    internal Godot.Collections.Array<Resource> FacilityLibraryProjectionBorrowed =>
        facility_library;
    internal Godot.Collections.Array<Resource> SettlementDistributionProjectionBorrowed =>
        settlement_distribution;
    internal Godot.Collections.Array<Resource> WildMonsterDistributionProjectionBorrowed =>
        wild_monster_distribution;
    internal Godot.Collections.Array<Resource> MountedSubmapsProjectionBorrowed =>
        mounted_submaps;
    internal Godot.Collections.Array<Resource> WorldEventsProjectionBorrowed => world_events;

    internal WorldGenerationDefinition ToDefinition(
        string canonicalPath,
        IContentResourceLoader loader
    ) => WorldGenerationDefinition.FromResource(canonicalPath, this, loader);

    public Vector2I GetWorldSizeCells()
    {
        return new Vector2I(
            world_size_in_chunks.X * chunk_size.X,
            world_size_in_chunks.Y * chunk_size.Y
        );
    }

    public int GetTargetSettlementCount(int tier)
    {
        switch (tier)
        {
            case (int)SettlementConfig.SettlementTier.VILLAGE:

                return Mathf.Max(procedural_village_count, 1);

            case (int)SettlementConfig.SettlementTier.TOWN:

                return Mathf.Max(procedural_town_count, 0);

            case (int)SettlementConfig.SettlementTier.CITY:

                return Mathf.Max(procedural_city_count, 0);

            case (int)SettlementConfig.SettlementTier.CAPITAL:

                return Mathf.Max(procedural_capital_count, 0);

            case (int)SettlementConfig.SettlementTier.WORLD_STRONGHOLD:

                return Mathf.Max(procedural_world_stronghold_count, 0);

            case (int)SettlementConfig.SettlementTier.METROPOLIS:

                return Mathf.Max(procedural_metropolis_count, 0);

            default:

                return 0;
        }
    }

    public int GetSettlementSpacingCells(int tier)
    {
        switch (tier)
        {
            case (int)SettlementConfig.SettlementTier.VILLAGE:

                return village_spacing_cells;

            case (int)SettlementConfig.SettlementTier.TOWN:

                return town_spacing_cells;

            case (int)SettlementConfig.SettlementTier.CITY:

                return city_spacing_cells;

            case (int)SettlementConfig.SettlementTier.CAPITAL:

                return capital_spacing_cells;

            case (int)SettlementConfig.SettlementTier.WORLD_STRONGHOLD:

                return world_stronghold_spacing_cells;

            case (int)SettlementConfig.SettlementTier.METROPOLIS:

                return metropolis_spacing_cells;

            default:

                return 64;
        }
    }
}
