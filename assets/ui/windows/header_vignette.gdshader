shader_type canvas_item;
render_mode unshaded;

// A small illustration inside the modal header; never samples or replaces the world.
void fragment() {
	vec4 artwork = texture(TEXTURE, UV);
	float edge = smoothstep(0.0, 0.36, UV.x) * (1.0 - smoothstep(0.42, 1.0, UV.y));
	COLOR = vec4(artwork.rgb, edge * 0.48);
}
