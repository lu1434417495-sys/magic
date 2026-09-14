shader_type canvas_item;
render_mode unshaded;
uniform vec4 marker_color : source_color = vec4(0.4,0.82,0.88,1.0);
uniform float fill_alpha = 0.20;
uniform float line_width_px = 2.6;
void fragment() {
    vec2 p = abs(UV * 2.0 - 1.0);
    float d = p.x + p.y;
    float pixel = max(length(vec2(dFdx(d), dFdy(d))), 0.0001);
    float distance_px = abs(d - 0.96) / pixel;
    float stroke = 1.0 - smoothstep(line_width_px * 0.5, line_width_px * 0.5 + 1.0, distance_px);
    float keyline = 1.0 - smoothstep(line_width_px * 0.5 + 1.0, line_width_px * 0.5 + 2.0, distance_px);
    float inside = (1.0 - smoothstep(0.96-pixel, 0.96, d)) * fill_alpha;
    float alpha = max(inside, keyline * 0.85);
    vec3 color = mix(marker_color.rgb * 0.26, marker_color.rgb, max(stroke, inside / max(alpha, 0.0001)));
    COLOR = vec4(color, max(alpha, stroke) * marker_color.a);
}
