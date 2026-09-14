shader_type canvas_item;
render_mode unshaded;

// Board UVs have integer cell centers and half-integer edges, exactly matching
// the painted surfaces and picking diamonds. Widths are in viewport pixels.
void fragment() {
    vec2 edge_distance = 0.5 - abs(fract(UV + 0.5) - 0.5);
    vec2 pixels_per_uv = vec2(
        length(vec2(dFdx(UV.x), dFdy(UV.x))),
        length(vec2(dFdx(UV.y), dFdy(UV.y))));
    vec2 distance_px = edge_distance / max(pixels_per_uv, vec2(0.00001));
    float distance_to_edge = min(distance_px.x, distance_px.y);
    float dark = (1.0 - smoothstep(0.8, 1.55, distance_to_edge)) * 0.70;
    float light = (1.0 - smoothstep(1.8, 2.6, distance_to_edge)) * 0.34 * (1.0 - dark);
    float alpha = dark + light;
    vec3 color = (vec3(0.22, 0.23, 0.18) * dark + vec3(1.0, 0.92, 0.74) * light)
        / max(alpha, 0.00001);
    COLOR = vec4(color, alpha);
}
