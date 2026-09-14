shader_type canvas_item;
render_mode unshaded;
void fragment() {
    vec3 stone = texture(TEXTURE, UV).rgb;
    float value = dot(stone, vec3(0.299,0.587,0.114));
    vec3 paint = mix(vec3(value), stone, 0.45);
    COLOR = vec4(mix(vec3(0.38,0.36,0.28), paint * vec3(0.65,0.66,0.62), 0.65), 1.0);
}
