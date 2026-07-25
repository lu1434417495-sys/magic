using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Godot;

public partial class run_direct_field_write_guard_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();
    private static readonly string[] BattleUnitWeaponFieldNames =
    {
        "weapon_profile_kind",
        "weapon_item_id",
        "weapon_profile_type_id",
        "weapon_range_type",
        "weapon_family",
        "weapon_current_grip",
        "weapon_attack_range",
        "weapon_one_handed_dice",
        "weapon_two_handed_dice",
        "weapon_is_versatile",
        "weapon_uses_two_hands",
        "weapon_physical_damage_tag",
    };
    private static readonly string[] BattleUnitCombatResourceFieldNames =
    {
        "current_hp",
        "current_mp",
        "current_stamina",
        "current_aura",
        "current_ap",
        "current_move_points",
        "stamina_recovery_progress",
        "is_alive",
    };
    private static readonly string[] BattleUnitGeometryFieldNames =
    {
        "coord",
        "body_size",
        "body_size_category",
        "footprint_size",
        "occupied_coords",
    };

    private static readonly Dictionary<string, HashSet<string>> ProtectedOwnerFields =
        new(StringComparer.Ordinal)
        {
            ["PartyMemberState"] = NewFieldSet(
                "current_hp",
                "current_mp",
                "current_aura",
                "is_dead",
                "race_id",
                "subrace_id",
                "age_years",
                "birth_at_world_step",
                "age_profile_id",
                "natural_age_stage_id",
                "effective_age_stage_id",
                "effective_age_stage_source_type",
                "effective_age_stage_source_id",
                "body_size",
                "body_size_category",
                "bloodline_id",
                "bloodline_stage_id",
                "ascension_id",
                "ascension_stage_id",
                "ascension_started_at_world_step",
                "original_race_id_before_ascension",
                "biological_age_years",
                "astral_memory_years",
                "versatility_pick",
                "active_stage_advancement_modifier_ids"
            ),
            ["BattleUnitState"] = NewFieldSet(
                "current_hp",
                "current_mp",
                "current_stamina",
                "current_aura",
                "current_ap",
                "current_move_points",
                "stamina_recovery_progress",
                "is_alive",
                "coord",
                "body_size",
                "body_size_category",
                "footprint_size",
                "occupied_coords",
                "status_effects",
                "known_active_skill_ids",
                "versatility_pick",
                "weapon_profile_kind",
                "weapon_item_id",
                "weapon_profile_type_id",
                "weapon_range_type",
                "weapon_family",
                "weapon_current_grip",
                "weapon_attack_range",
                "weapon_one_handed_dice",
                "weapon_two_handed_dice",
                "weapon_is_versatile",
                "weapon_uses_two_hands",
                "weapon_physical_damage_tag"
            ),
            ["BattleCellState"] = NewFieldSet("coord"),
            ["PartyState"] = NewFieldSet("member_states"),
        };

    private static readonly Regex MemberWritePattern = new(
        @"(?<receiver>\b(?:this|[A-Za-z_][A-Za-z0-9_]*)(?:\s*\.\s*[A-Za-z_][A-Za-z0-9_]*(?:\s*\([^;\n{}]*\))?)*)\s*\.\s*(?<field>"
            + ProtectedFieldPattern
            + @")\s*(?<op>\+\+|--|\+=|-=|\*=|/=|%=|=(?!=))",
        RegexOptions.Compiled
    );

    private static readonly Regex ObjectInitializerFieldWritePattern = new(
        @"(?<![.\w])(?<field>"
            + ProtectedFieldPattern
            + @")\s*(?<op>\+\+|--|\+=|-=|\*=|/=|%=|=(?!=))",
        RegexOptions.Compiled
    );

    private static readonly Regex ExplicitOwnerInitializerPattern = new(
        @"\bnew\s+(?<type>PartyMemberState|BattleUnitState|BattleCellState|PartyState)\s*(?:\([^;\n{}]*\))?\s*(?<brace>\{)?",
        RegexOptions.Compiled
    );

    private static readonly Regex TargetTypedOwnerInitializerPattern = new(
        @"\b(?<type>PartyMemberState|BattleUnitState|BattleCellState|PartyState)\s+[_A-Za-z][A-Za-z0-9_]*\s*=\s*new\s*(?:\([^;\n{}]*\))?\s*(?<brace>\{)?",
        RegexOptions.Compiled
    );

    private static readonly Regex ExplicitDeclarationPattern = new(
        @"(?:^|[;\s\(\{,])(?<type>[A-Z][A-Za-z0-9_]*(?:<[^>\n;=(){}]+>)?)\s+(?<name>[_A-Za-z][A-Za-z0-9_]*)\s*(?=[=;,\)])",
        RegexOptions.Compiled
    );

    private static readonly Regex VarNewDeclarationPattern = new(
        @"\bvar\s+(?<name>[_A-Za-z][A-Za-z0-9_]*)\s*=\s*new\s+(?<type>[A-Z][A-Za-z0-9_]*)\b",
        RegexOptions.Compiled
    );

    private static readonly Regex AsDeclarationPattern = new(
        @"\bvar\s+(?<name>[_A-Za-z][A-Za-z0-9_]*)\s*=\s*[^;\n]+?\s+as\s+(?<type>[A-Z][A-Za-z0-9_]*)\b",
        RegexOptions.Compiled
    );

    private static readonly Regex IsDeclarationPattern = new(
        @"\bis\s+(?<type>[A-Z][A-Za-z0-9_]*)\s+(?<name>[_A-Za-z][A-Za-z0-9_]*)\b",
        RegexOptions.Compiled
    );

    private static readonly Regex OutDeclarationPattern = new(
        @"\bout\s+(?<type>[A-Z][A-Za-z0-9_]*)\s+(?<name>[_A-Za-z][A-Za-z0-9_]*)\b",
        RegexOptions.Compiled
    );

    private static readonly Regex ForeachDeclarationPattern = new(
        @"\bforeach\s*\(\s*(?<type>[A-Z][A-Za-z0-9_]*)\s+(?<name>[_A-Za-z][A-Za-z0-9_]*)\b",
        RegexOptions.Compiled
    );

    private static readonly Regex MemberDeclarationPattern = new(
        @"(?:(?:public|private|protected|internal|static|readonly|sealed|partial|new|required|volatile)\s+)*(?<type>[A-Z][A-Za-z0-9_]*(?:<[^>\n;=(){}]+>)?)\s+(?<name>[_A-Za-z][A-Za-z0-9_]*)\s*(?=[=;{])",
        RegexOptions.Compiled
    );

    private static readonly Regex MethodDeclarationPattern = new(
        @"(?:(?:public|private|protected|internal|static|virtual|override|sealed|partial|new)\s+)*(?<type>[A-Z][A-Za-z0-9_]*(?:<[^>\n;=(){}]+>)?)\s+(?<name>[_A-Za-z][A-Za-z0-9_]*)\s*\(",
        RegexOptions.Compiled
    );

    private static readonly Regex ClassDeclarationPattern = new(
        @"\bclass\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\b",
        RegexOptions.Compiled
    );

    private const string ProtectedFieldPattern =
        "current_hp|current_mp|current_stamina|current_aura|current_ap|current_move_points|stamina_recovery_progress|is_alive|is_dead|coord|body_size|body_size_category|footprint_size|occupied_coords|status_effects|known_active_skill_ids|member_states|race_id|subrace_id|age_years|birth_at_world_step|age_profile_id|natural_age_stage_id|effective_age_stage_id|effective_age_stage_source_type|effective_age_stage_source_id|bloodline_id|bloodline_stage_id|ascension_id|ascension_stage_id|ascension_started_at_world_step|original_race_id_before_ascension|biological_age_years|astral_memory_years|versatility_pick|active_stage_advancement_modifier_ids|weapon_profile_kind|weapon_item_id|weapon_profile_type_id|weapon_range_type|weapon_family|weapon_current_grip|weapon_attack_range|weapon_one_handed_dice|weapon_two_handed_dice|weapon_is_versatile|weapon_uses_two_hands|weapon_physical_damage_tag";

    private const string BattleUnitGeometryOwnerPath =
        "scripts/systems/battle/core/BattleUnitGeometryState.cs";

    private static readonly Regex ExternalFootprintRefreshPattern = new(
        @"\.\s*RefreshFootprint\s*\(",
        RegexOptions.Compiled
    );

    public override void _Initialize()
    {
        Run();
    }

    private void Run()
    {
        TestScannerAllowsNonOwnerProjectionFields();
        TestScannerRejectsProtectedOwnerWrites();
        TestScannerRejectsProtectedOwnerObjectInitializers();
        TestScannerResolvesProtectedOwnerThroughMemberChain();
        TestScannerAllowsOwnerInternalWrites();
        TestScannerRejectsExternalFootprintRefresh();
        TestRepositoryScripts();

        RequestTestExit(_test.Finish("Direct field write guard regression"));
    }

    private void TestScannerAllowsNonOwnerProjectionFields()
    {
        string source = """
            public sealed class AttributeSourceContext
            {
                public StringName versatility_pick = "";
            }

            public sealed class BattleAiUnitSnapshot
            {
                public int current_hp;
                public int current_mp;
                public int current_stamina;
                public int current_aura;
                public int current_ap;
                public int current_move_points;
                public int stamina_recovery_progress;
                public bool is_alive;
                public Vector2I coord;
                public int body_size;
                public StringName body_size_category;
                public Vector2I footprint_size;
                public Vector2IList occupied_coords;
            }

            public sealed class WeaponProjection
            {
                public StringName weapon_profile_kind;
                public StringName weapon_item_id;
                public StringName weapon_profile_type_id;
                public StringName weapon_range_type;
                public StringName weapon_family;
                public StringName weapon_current_grip;
                public int weapon_attack_range;
                public WeaponDice weapon_one_handed_dice;
                public WeaponDice weapon_two_handed_dice;
                public bool weapon_is_versatile;
                public bool weapon_uses_two_hands;
                public StringName weapon_physical_damage_tag;
            }

            public sealed class Probe
            {
                public void Run()
                {
                    AttributeSourceContext context = new();
                    context.versatility_pick = "agility";
                    var snapshot = new BattleAiUnitSnapshot();
                    snapshot.current_hp = 10;
                    snapshot.current_mp = 9;
                    snapshot.current_stamina = 8;
                    snapshot.current_aura = 7;
                    snapshot.current_ap = 6;
                    snapshot.current_move_points = 5;
                    snapshot.stamina_recovery_progress = 4;
                    snapshot.is_alive = true;
                    snapshot.coord = Vector2I.Zero;
                    snapshot.body_size = 2;
                    snapshot.body_size_category = "medium";
                    snapshot.footprint_size = Vector2I.One;
                    snapshot.occupied_coords = new();
                    var snapshotFromInitializer = new BattleAiUnitSnapshot
                    {
                        current_hp = 10,
                        current_mp = 9,
                        current_stamina = 8,
                        current_aura = 7,
                        current_ap = 6,
                        current_move_points = 5,
                        stamina_recovery_progress = 4,
                        is_alive = true,
                        coord = Vector2I.Zero,
                        body_size = 2,
                        body_size_category = "medium",
                        footprint_size = Vector2I.One,
                        occupied_coords = new(),
                    };
                    WeaponProjection projection = new();
                    projection.weapon_profile_kind = "equipped";
                    projection.weapon_item_id = "test_weapon";
                    projection.weapon_profile_type_id = "test_profile";
                    projection.weapon_range_type = "melee";
                    projection.weapon_family = "sword";
                    projection.weapon_current_grip = "one_handed";
                    projection.weapon_attack_range = 1;
                    projection.weapon_one_handed_dice = new();
                    projection.weapon_two_handed_dice = new();
                    projection.weapon_is_versatile = true;
                    projection.weapon_uses_two_hands = false;
                    projection.weapon_physical_damage_tag = "physical_slash";

                    WeaponProjection projectionFromInitializer = new()
                    {
                        weapon_profile_kind = "natural",
                        weapon_item_id = "",
                        weapon_profile_type_id = "claw",
                        weapon_range_type = "melee",
                        weapon_family = "claw",
                        weapon_current_grip = "one_handed",
                        weapon_attack_range = 1,
                        weapon_one_handed_dice = new(),
                        weapon_two_handed_dice = new(),
                        weapon_is_versatile = false,
                        weapon_uses_two_hands = false,
                        weapon_physical_damage_tag = "physical_slash",
                    };
                }
            }
            """;

        List<string> violations = FindViolationsForSource("tests/synthetic/non_owner.cs", source);
        _test.Eq(
            violations.Count,
            0,
            "非 owner projection/DTO 的同名字段写入不应触发 direct field guard。"
        );
    }

    private void TestScannerRejectsProtectedOwnerWrites()
    {
        string source = """
            public sealed class Probe
            {
                public void Run()
                {
                    PartyMemberState member = new();
                    member.versatility_pick = "strength";
                    var unit = new BattleUnitState();
                    unit.current_hp = 1;
                    unit.current_mp = 2;
                    unit.current_stamina = 3;
                    unit.current_aura = 4;
                    unit.current_ap = 5;
                    unit.current_move_points = 6;
                    unit.stamina_recovery_progress = 7;
                    unit.is_alive = true;
                    unit.coord = Vector2I.Zero;
                    unit.body_size = 2;
                    unit.body_size_category = "medium";
                    unit.footprint_size = Vector2I.One;
                    unit.occupied_coords = new();
                    unit.weapon_profile_kind = "equipped";
                    unit.weapon_item_id = "test_weapon";
                    unit.weapon_profile_type_id = "test_profile";
                    unit.weapon_range_type = "melee";
                    unit.weapon_family = "sword";
                    unit.weapon_current_grip = "one_handed";
                    unit.weapon_attack_range = 1;
                    unit.weapon_one_handed_dice = new();
                    unit.weapon_two_handed_dice = new();
                    unit.weapon_is_versatile = true;
                    unit.weapon_uses_two_hands = false;
                    unit.weapon_physical_damage_tag = "physical_slash";
                    PartyState party = new();
                    party.member_states = new();
                }
            }
            """;

        List<string> violations = FindViolationsForSource("tests/synthetic/protected_owner.cs", source);
        _test.Eq(
            violations.Count,
            27,
            $"受保护 owner 字段直接写入应被识别。violations={string.Join("\n", violations)}"
        );
        _test.True(
            ContainsViolation(violations, "PartyMemberState.versatility_pick"),
            "PartyMemberState.versatility_pick 直接写入应被 guard 拦截。"
        );
        foreach (string fieldName in BattleUnitCombatResourceFieldNames)
        {
            _test.True(
                ContainsViolation(
                    violations,
                    $"BattleUnitState.{fieldName}"
                ),
                $"BattleUnitState.{fieldName} 直接写入应被 guard 拦截。"
            );
        }
        foreach (string fieldName in BattleUnitGeometryFieldNames)
        {
            _test.True(
                ContainsViolation(
                    violations,
                    $"BattleUnitState.{fieldName}"
                ),
                $"BattleUnitState.{fieldName} 直接写入应被 guard 拦截。"
            );
        }
        _test.True(
            ContainsViolation(violations, "PartyState.member_states"),
            "PartyState.member_states 直接写入应被 guard 拦截。"
        );
        foreach (string fieldName in BattleUnitWeaponFieldNames)
        {
            _test.True(
                ContainsViolation(
                    violations,
                    $"BattleUnitState.{fieldName}"
                ),
                $"BattleUnitState.{fieldName} 直接写入应被 guard 拦截。"
            );
        }
    }

    private void TestScannerRejectsProtectedOwnerObjectInitializers()
    {
        string source = """
            public sealed class Probe
            {
                public void Run()
                {
                    var unit = new BattleUnitState
                    {
                        current_hp = 1,
                        current_mp = 2,
                        current_stamina = 3,
                        current_aura = 4,
                        current_ap = 5,
                        current_move_points = 6,
                        stamina_recovery_progress = 7,
                        is_alive = true,
                        coord = Vector2I.Zero,
                        body_size = 2,
                        body_size_category = "medium",
                        footprint_size = Vector2I.One,
                        occupied_coords = new(),
                        weapon_profile_kind = "equipped",
                        weapon_item_id = "test_weapon",
                        weapon_profile_type_id = "test_profile",
                        weapon_range_type = "melee",
                        weapon_family = "sword",
                        weapon_current_grip = "one_handed",
                        weapon_attack_range = 1,
                        weapon_one_handed_dice = new(),
                        weapon_two_handed_dice = new(),
                        weapon_is_versatile = true,
                        weapon_uses_two_hands = false,
                        weapon_physical_damage_tag = "physical_slash",
                    };
                    PartyMemberState member = new()
                    {
                        current_mp = 2,
                    };
                }
            }
            """;

        List<string> violations = FindViolationsForSource("tests/synthetic/object_initializer.cs", source);
        _test.Eq(
            violations.Count,
            26,
            $"受保护 owner 字段 object initializer 写入应被识别。violations={string.Join("\n", violations)}"
        );
        foreach (string fieldName in BattleUnitCombatResourceFieldNames)
        {
            _test.True(
                ContainsViolation(
                    violations,
                    $"BattleUnitState.{fieldName}"
                ),
                $"BattleUnitState.{fieldName} object initializer 写入应被 guard 拦截。"
            );
        }
        foreach (string fieldName in BattleUnitGeometryFieldNames)
        {
            _test.True(
                ContainsViolation(
                    violations,
                    $"BattleUnitState.{fieldName}"
                ),
                $"BattleUnitState.{fieldName} object initializer 写入应被 guard 拦截。"
            );
        }
        _test.True(
            ContainsViolation(violations, "PartyMemberState.current_mp"),
            "target-typed PartyMemberState.current_mp object initializer 写入应被 guard 拦截。"
        );
        foreach (string fieldName in BattleUnitWeaponFieldNames)
        {
            _test.True(
                ContainsViolation(
                    violations,
                    $"BattleUnitState.{fieldName}"
                ),
                $"BattleUnitState.{fieldName} object initializer 写入应被 guard 拦截。"
            );
        }
    }

    private void TestScannerResolvesProtectedOwnerThroughMemberChain()
    {
        string source = """
            public sealed class ProbeContext
            {
                public BattleUnitState unit_state = new();
            }

            public sealed class Probe
            {
                public void Run()
                {
                    ProbeContext context = new();
                    context.unit_state.current_hp = 1;
                }
            }
            """;

        List<string> violations = FindViolationsForSource("tests/synthetic/member_chain.cs", source);
        _test.Eq(
            violations.Count,
            1,
            $"成员链解析到受保护 owner 时应拦截。violations={string.Join("\n", violations)}"
        );
        _test.True(
            ContainsViolation(violations, "BattleUnitState.current_hp"),
            "context.unit_state.current_hp 应按 BattleUnitState.current_hp 处理。"
        );
    }

    private void TestScannerAllowsOwnerInternalWrites()
    {
        string source = """
            public partial class BattleUnitState
            {
                public BattleUnitState Restore()
                {
                    this.current_hp = 1;
                    current_mp = 2;
                    return new BattleUnitState
                    {
                        current_hp = 3,
                    };
                }
            }
            """;

        List<string> violations = FindViolationsForSource("tests/synthetic/owner_internal.cs", source);
        _test.Eq(
            violations.Count,
            0,
            "owner 类型内部维护自身字段不应触发 direct field guard。"
        );
    }

    private void TestScannerRejectsExternalFootprintRefresh()
    {
        string source = """
            public sealed class Probe
            {
                public void Run(BattleUnitState unit)
                {
                    unit.RefreshFootprint();
                }
            }
            """;

        List<string> violations = FindExternalFootprintRefreshViolations(
            "tests/synthetic/external_footprint_refresh.cs",
            source
        );
        _test.Eq(
            violations.Count,
            1,
            "BattleUnitGeometryState 之外不得重新引入 RefreshFootprint 查询式调用。"
        );
        _test.Eq(
            FindExternalFootprintRefreshViolations(
                BattleUnitGeometryOwnerPath,
                source
            ).Count,
            0,
            "BattleUnitGeometryState owner 文件内部允许维护自身投影。"
        );
    }

    private void TestRepositoryScripts()
    {
        string repoRoot = ProjectSettings.GlobalizePath("res://");
        var violations = new List<string>();
        foreach (string path in Directory.EnumerateFiles(
            Path.Combine(repoRoot, "scripts"),
            "*.cs",
            SearchOption.AllDirectories
        ))
        {
            string repoPath = Path.GetRelativePath(repoRoot, path).Replace('\\', '/');
            string source = File.ReadAllText(path);
            violations.AddRange(FindViolationsForSource(repoPath, source));
            violations.AddRange(FindExternalFootprintRefreshViolations(repoPath, source));
        }

        if (violations.Count > 0)
        {
            _test.Fail("Direct field write guard failed:\n" + string.Join("\n", violations));
        }
    }

    private static List<string> FindExternalFootprintRefreshViolations(
        string repoPath,
        string source
    )
    {
        var violations = new List<string>();
        if (
            string.Equals(
                repoPath,
                BattleUnitGeometryOwnerPath,
                StringComparison.Ordinal
            )
        )
            return violations;

        string[] lines = SanitizeSource(source).Replace("\r\n", "\n").Split('\n');
        for (int index = 0; index < lines.Length; index++)
        {
            if (!ExternalFootprintRefreshPattern.IsMatch(lines[index]))
                continue;
            violations.Add(
                $"{repoPath}:{index + 1}: 禁止在 BattleUnitGeometryState owner 外调用 RefreshFootprint；读路径必须保持纯只读。"
            );
        }
        return violations;
    }

    private static List<string> FindViolationsForSource(string repoPath, string source)
    {
        string sanitizedSource = SanitizeSource(source);
        string[] sanitizedLines = sanitizedSource.Replace("\r\n", "\n").Split('\n');
        var memberTypes = BuildMemberTypeIndex(sanitizedLines);
        var methodReturnTypes = BuildMethodReturnTypeIndex(sanitizedLines);
        var violations = new List<string>();
        var symbols = new List<ScopedSymbol>();
        var classStack = new List<ClassScope>();
        var objectInitializerStack = new List<ObjectInitializerScope>();
        string pendingClassName = "";
        string pendingObjectInitializerType = "";
        int braceDepth = 0;

        for (int index = 0; index < sanitizedLines.Length; index++)
        {
            string line = sanitizedLines[index];
            PopClosedClasses(classStack, braceDepth);
            PopClosedObjectInitializers(objectInitializerStack, braceDepth);
            pendingClassName = UpdateClassStackBeforeLine(
                line,
                pendingClassName,
                classStack,
                braceDepth
            );
            string currentClass = classStack.Count > 0 ? classStack[^1].TypeName : "";
            pendingObjectInitializerType = UpdateObjectInitializerStackBeforeLine(
                line,
                pendingObjectInitializerType,
                objectInitializerStack,
                braceDepth
            );
            AddScopedSymbols(line, symbols, braceDepth);
            RemoveOutOfScopeSymbols(symbols, braceDepth);

            foreach (Match match in MemberWritePattern.Matches(line))
            {
                string fieldName = match.Groups["field"].Value;
                string receiver = NormalizeMemberAccess(match.Groups["receiver"].Value);
                string receiverType = ResolveReceiverType(
                    receiver,
                    currentClass,
                    symbols,
                    memberTypes,
                    methodReturnTypes
                );
                if (!IsProtectedOwnerField(receiverType, fieldName))
                    continue;
                if (receiver == "this" && receiverType == currentClass)
                    continue;
                violations.Add(
                    $"{repoPath}:{index + 1}: 禁止直接写 {receiverType}.{fieldName} ({receiver})，请改用 owner 类型接口。"
                );
            }

            if (objectInitializerStack.Count > 0)
            {
                string ownerType = objectInitializerStack[^1].OwnerType;
                if (ownerType != currentClass)
                {
                    foreach (Match match in ObjectInitializerFieldWritePattern.Matches(line))
                    {
                        string fieldName = match.Groups["field"].Value;
                        if (!IsProtectedOwnerField(ownerType, fieldName))
                            continue;
                        violations.Add(
                            $"{repoPath}:{index + 1}: 禁止通过 object initializer 写 {ownerType}.{fieldName}，请改用 owner 类型接口。"
                        );
                    }
                }
            }

            braceDepth += CountChar(line, '{') - CountChar(line, '}');
            if (braceDepth < 0)
                braceDepth = 0;
            PopClosedClasses(classStack, braceDepth);
            PopClosedObjectInitializers(objectInitializerStack, braceDepth);
        }

        return violations;
    }

    private static Dictionary<string, Dictionary<string, string>> BuildMemberTypeIndex(
        string[] lines
    )
    {
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        var classStack = new List<ClassScope>();
        string pendingClassName = "";
        int braceDepth = 0;

        foreach (string line in lines)
        {
            PopClosedClasses(classStack, braceDepth);
            pendingClassName = UpdateClassStackBeforeLine(
                line,
                pendingClassName,
                classStack,
                braceDepth
            );
            string currentClass = classStack.Count > 0 ? classStack[^1].TypeName : "";
            if (!string.IsNullOrEmpty(currentClass))
            {
                foreach (Match match in MemberDeclarationPattern.Matches(line))
                {
                    string memberType = SimplifyTypeName(match.Groups["type"].Value);
                    string memberName = match.Groups["name"].Value;
                    if (string.IsNullOrEmpty(memberType) || string.IsNullOrEmpty(memberName))
                        continue;
                    if (!result.TryGetValue(currentClass, out var members))
                    {
                        members = new Dictionary<string, string>(StringComparer.Ordinal);
                        result[currentClass] = members;
                    }
                    members[memberName] = memberType;
                }
            }

            braceDepth += CountChar(line, '{') - CountChar(line, '}');
            if (braceDepth < 0)
                braceDepth = 0;
            PopClosedClasses(classStack, braceDepth);
        }

        return result;
    }

    private static Dictionary<string, string> BuildMethodReturnTypeIndex(string[] lines)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string line in lines)
        {
            foreach (Match match in MethodDeclarationPattern.Matches(line))
            {
                string returnType = SimplifyTypeName(match.Groups["type"].Value);
                string methodName = match.Groups["name"].Value;
                if (!string.IsNullOrEmpty(returnType) && !string.IsNullOrEmpty(methodName))
                    result[methodName] = returnType;
            }
        }
        return result;
    }

    private static string UpdateClassStackBeforeLine(
        string line,
        string pendingClassName,
        List<ClassScope> classStack,
        int braceDepth
    )
    {
        Match classMatch = ClassDeclarationPattern.Match(line);
        if (classMatch.Success)
            pendingClassName = classMatch.Groups["name"].Value;

        if (!string.IsNullOrEmpty(pendingClassName) && line.Contains("{"))
        {
            classStack.Add(new ClassScope(pendingClassName, braceDepth + 1));
            return "";
        }

        return pendingClassName;
    }

    private static void PopClosedClasses(List<ClassScope> classStack, int braceDepth)
    {
        while (classStack.Count > 0 && braceDepth < classStack[^1].BodyDepth)
            classStack.RemoveAt(classStack.Count - 1);
    }

    private static string UpdateObjectInitializerStackBeforeLine(
        string line,
        string pendingObjectInitializerType,
        List<ObjectInitializerScope> objectInitializerStack,
        int braceDepth
    )
    {
        if (!string.IsNullOrEmpty(pendingObjectInitializerType) && line.Contains('{'))
        {
            objectInitializerStack.Add(
                new ObjectInitializerScope(pendingObjectInitializerType, braceDepth + 1)
            );
            pendingObjectInitializerType = "";
        }

        string ownerType = ResolveOwnerInitializerType(line, out bool opensOnLine);
        if (string.IsNullOrEmpty(ownerType))
            return pendingObjectInitializerType;

        if (opensOnLine)
        {
            objectInitializerStack.Add(new ObjectInitializerScope(ownerType, braceDepth + 1));
            return pendingObjectInitializerType;
        }

        return ownerType;
    }

    private static string ResolveOwnerInitializerType(string line, out bool opensOnLine)
    {
        opensOnLine = false;
        Match match = TargetTypedOwnerInitializerPattern.Match(line);
        if (!match.Success)
            match = ExplicitOwnerInitializerPattern.Match(line);
        if (!match.Success)
            return "";

        opensOnLine = match.Groups["brace"].Success;
        if (!opensOnLine && line[match.Index..].Contains(';'))
            return "";
        return match.Groups["type"].Value;
    }

    private static void PopClosedObjectInitializers(
        List<ObjectInitializerScope> objectInitializerStack,
        int braceDepth
    )
    {
        while (
            objectInitializerStack.Count > 0
            && braceDepth < objectInitializerStack[^1].BodyDepth
        )
            objectInitializerStack.RemoveAt(objectInitializerStack.Count - 1);
    }

    private static void AddScopedSymbols(string line, List<ScopedSymbol> symbols, int scopeDepth)
    {
        AddMatchesAsSymbols(line, ExplicitDeclarationPattern, symbols, scopeDepth);
        AddMatchesAsSymbols(line, VarNewDeclarationPattern, symbols, scopeDepth);
        AddMatchesAsSymbols(line, AsDeclarationPattern, symbols, scopeDepth);
        AddMatchesAsSymbols(line, IsDeclarationPattern, symbols, scopeDepth);
        AddMatchesAsSymbols(line, OutDeclarationPattern, symbols, scopeDepth);
        AddMatchesAsSymbols(line, ForeachDeclarationPattern, symbols, scopeDepth);
    }

    private static void AddMatchesAsSymbols(
        string line,
        Regex pattern,
        List<ScopedSymbol> symbols,
        int scopeDepth
    )
    {
        foreach (Match match in pattern.Matches(line))
        {
            string name = match.Groups["name"].Value;
            string type = SimplifyTypeName(match.Groups["type"].Value);
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(type))
                continue;
            symbols.Add(new ScopedSymbol(name, type, scopeDepth));
        }
    }

    private static void RemoveOutOfScopeSymbols(List<ScopedSymbol> symbols, int braceDepth)
    {
        for (int index = symbols.Count - 1; index >= 0; index--)
        {
            if (symbols[index].ScopeDepth > braceDepth)
                symbols.RemoveAt(index);
        }
    }

    private static string ResolveReceiverType(
        string receiver,
        string currentClass,
        List<ScopedSymbol> symbols,
        Dictionary<string, Dictionary<string, string>> memberTypes,
        Dictionary<string, string> methodReturnTypes
    )
    {
        if (string.IsNullOrEmpty(receiver))
            return "";

        List<string> parts = SplitMemberAccess(receiver);
        if (parts.Count == 0)
            return "";

        string currentType = ResolveFirstReceiverType(
            parts[0],
            currentClass,
            symbols,
            methodReturnTypes
        );
        if (string.IsNullOrEmpty(currentType))
            return "";

        for (int index = 1; index < parts.Count; index++)
        {
            string part = parts[index];
            string memberName = ExtractMemberName(part);
            if (string.IsNullOrEmpty(memberName))
                return "";
            if (part.Contains("("))
            {
                currentType = methodReturnTypes.TryGetValue(memberName, out string returnType)
                    ? returnType
                    : "";
            }
            else if (
                memberTypes.TryGetValue(currentType, out var members)
                && members.TryGetValue(memberName, out string memberType)
            )
            {
                currentType = memberType;
            }
            else
            {
                currentType = "";
            }
            if (string.IsNullOrEmpty(currentType))
                return "";
        }

        return currentType;
    }

    private static string ResolveFirstReceiverType(
        string receiverPart,
        string currentClass,
        List<ScopedSymbol> symbols,
        Dictionary<string, string> methodReturnTypes
    )
    {
        if (receiverPart == "this")
            return currentClass;
        string memberName = ExtractMemberName(receiverPart);
        if (string.IsNullOrEmpty(memberName))
            return "";
        if (receiverPart.Contains("("))
        {
            return methodReturnTypes.TryGetValue(memberName, out string returnType)
                ? returnType
                : "";
        }
        for (int index = symbols.Count - 1; index >= 0; index--)
        {
            ScopedSymbol symbol = symbols[index];
            if (symbol.Name == memberName)
                return symbol.TypeName;
        }
        return "";
    }

    private static List<string> SplitMemberAccess(string expression)
    {
        var result = new List<string>();
        var builder = new StringBuilder();
        int parenDepth = 0;
        foreach (char c in expression)
        {
            if (c == '(')
                parenDepth++;
            else if (c == ')' && parenDepth > 0)
                parenDepth--;

            if (c == '.' && parenDepth == 0)
            {
                string part = builder.ToString().Trim();
                if (part.Length > 0)
                    result.Add(part);
                builder.Clear();
                continue;
            }

            builder.Append(c);
        }
        string finalPart = builder.ToString().Trim();
        if (finalPart.Length > 0)
            result.Add(finalPart);
        return result;
    }

    private static string ExtractMemberName(string part)
    {
        int parenIndex = part.IndexOf('(');
        string name = parenIndex >= 0 ? part[..parenIndex] : part;
        name = name.Trim();
        int genericIndex = name.IndexOf('<');
        if (genericIndex >= 0)
            name = name[..genericIndex];
        return name.Trim();
    }

    private static string NormalizeMemberAccess(string value) =>
        Regex.Replace(value ?? "", @"\s+", "");

    private static string SimplifyTypeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";
        string typeName = value.Trim();
        int genericIndex = typeName.IndexOf('<');
        if (genericIndex >= 0)
            typeName = typeName[..genericIndex];
        int namespaceIndex = typeName.LastIndexOf('.');
        if (namespaceIndex >= 0)
            typeName = typeName[(namespaceIndex + 1)..];
        return typeName.Trim();
    }

    private static bool IsProtectedOwnerField(string ownerType, string fieldName) =>
        !string.IsNullOrEmpty(ownerType)
        && !string.IsNullOrEmpty(fieldName)
        && ProtectedOwnerFields.TryGetValue(ownerType, out HashSet<string> fields)
        && fields.Contains(fieldName);

    private static bool ContainsViolation(List<string> violations, string needle)
    {
        foreach (string violation in violations)
            if (violation.Contains(needle, StringComparison.Ordinal))
                return true;
        return false;
    }

    private static string SanitizeSource(string source)
    {
        if (string.IsNullOrEmpty(source))
            return "";

        var builder = new StringBuilder(source.Length);
        bool inLineComment = false;
        bool inBlockComment = false;
        bool inString = false;
        bool inVerbatimString = false;
        bool inChar = false;

        for (int index = 0; index < source.Length; index++)
        {
            char current = source[index];
            char next = index + 1 < source.Length ? source[index + 1] : '\0';

            if (inLineComment)
            {
                if (current == '\n')
                {
                    inLineComment = false;
                    builder.Append('\n');
                }
                else
                {
                    builder.Append(' ');
                }
                continue;
            }

            if (inBlockComment)
            {
                if (current == '*' && next == '/')
                {
                    builder.Append("  ");
                    index++;
                    inBlockComment = false;
                }
                else
                {
                    builder.Append(current == '\n' ? '\n' : ' ');
                }
                continue;
            }

            if (inString)
            {
                if (current == '\\' && next != '\0')
                {
                    builder.Append("  ");
                    index++;
                    continue;
                }
                if (current == '"')
                    inString = false;
                builder.Append(current == '\n' ? '\n' : ' ');
                continue;
            }

            if (inVerbatimString)
            {
                if (current == '"' && next == '"')
                {
                    builder.Append("  ");
                    index++;
                    continue;
                }
                if (current == '"')
                    inVerbatimString = false;
                builder.Append(current == '\n' ? '\n' : ' ');
                continue;
            }

            if (inChar)
            {
                if (current == '\\' && next != '\0')
                {
                    builder.Append("  ");
                    index++;
                    continue;
                }
                if (current == '\'')
                    inChar = false;
                builder.Append(current == '\n' ? '\n' : ' ');
                continue;
            }

            if (current == '/' && next == '/')
            {
                builder.Append("  ");
                index++;
                inLineComment = true;
                continue;
            }
            if (current == '/' && next == '*')
            {
                builder.Append("  ");
                index++;
                inBlockComment = true;
                continue;
            }
            if (current == '@' && next == '"')
            {
                builder.Append("  ");
                index++;
                inVerbatimString = true;
                continue;
            }
            if (current == '$' && next == '"')
            {
                builder.Append("  ");
                index++;
                inString = true;
                continue;
            }
            if (
                current == '$'
                && next == '@'
                && index + 2 < source.Length
                && source[index + 2] == '"'
            )
            {
                builder.Append("   ");
                index += 2;
                inVerbatimString = true;
                continue;
            }
            if (
                current == '@'
                && next == '$'
                && index + 2 < source.Length
                && source[index + 2] == '"'
            )
            {
                builder.Append("   ");
                index += 2;
                inVerbatimString = true;
                continue;
            }
            if (current == '"')
            {
                builder.Append(' ');
                inString = true;
                continue;
            }
            if (current == '\'')
            {
                builder.Append(' ');
                inChar = true;
                continue;
            }

            builder.Append(current);
        }

        return builder.ToString();
    }

    private static int CountChar(string value, char needle)
    {
        int count = 0;
        foreach (char c in value)
            if (c == needle)
                count++;
        return count;
    }

    private static HashSet<string> NewFieldSet(params string[] values) =>
        new(values, StringComparer.Ordinal);

    private readonly struct ScopedSymbol
    {
        public readonly string Name;
        public readonly string TypeName;
        public readonly int ScopeDepth;

        public ScopedSymbol(string name, string typeName, int scopeDepth)
        {
            Name = name;
            TypeName = typeName;
            ScopeDepth = scopeDepth;
        }
    }

    private readonly struct ClassScope
    {
        public readonly string TypeName;
        public readonly int BodyDepth;

        public ClassScope(string typeName, int bodyDepth)
        {
            TypeName = typeName;
            BodyDepth = bodyDepth;
        }
    }

    private readonly struct ObjectInitializerScope
    {
        public readonly string OwnerType;
        public readonly int BodyDepth;

        public ObjectInitializerScope(string ownerType, int bodyDepth)
        {
            OwnerType = ownerType;
            BodyDepth = bodyDepth;
        }
    }
}
