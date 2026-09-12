shader_type canvas_item;
render_mode unshaded;

uniform float ui_start = 0.47;

void fragment() {
    float reading_area = smoothstep(ui_start - 0.16, ui_start + 0.04, UV.x);
    COLOR = vec4(0.012, 0.018, 0.026, 0.06 + reading_area * 0.56);
}
