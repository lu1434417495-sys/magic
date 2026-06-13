shader_type canvas_item;

// Desaturates the skill icon so unusable skills read as "grayed out".
// Driven entirely by the modulate color set on the TextureRect, so the same
// material instance can be shared across every disabled slot.

void fragment() {
    vec4 tex = texture(TEXTURE, UV);
    float gray = dot(tex.rgb, vec3(0.299, 0.587, 0.114));
    COLOR = vec4(vec3(gray), tex.a) * COLOR;
}
