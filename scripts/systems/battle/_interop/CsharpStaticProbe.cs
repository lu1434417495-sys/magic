using Godot;

[GlobalClass]
public partial class CsharpStaticProbe : RefCounted
{
    public static readonly StringName STATIC_NAME = "hello_static";
    public const int STATIC_INT = 42;

    public StringName instance_value { get; set; } = "hello_instance";

    public static StringName get_type_move() => "move";
    public static int get_magic_int() => 42;
    public StringName instance_get_type_move() => "move";
}
