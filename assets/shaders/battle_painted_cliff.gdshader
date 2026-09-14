shader_type canvas_item;
render_mode unshaded;
varying vec4 stone_tint;
void vertex() { stone_tint = COLOR; }
void fragment() {
    COLOR = vec4(texture(TEXTURE, UV).rgb * stone_tint.rgb, stone_tint.a);
}
