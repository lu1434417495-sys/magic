shader_type canvas_item;
render_mode unshaded;

uniform sampler2D terrain_data : filter_nearest, repeat_disable;
uniform vec2 map_size = vec2(1.0);

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float noise(vec2 p) {
    vec2 i = floor(p); vec2 f = fract(p); f = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash(i), hash(i + vec2(1,0)), f.x),
        mix(hash(i + vec2(0,1)), hash(i + vec2(1,1)), f.x), f.y);
}
vec4 cell(vec2 p) {
    if (any(lessThan(p, vec2(0))) || any(greaterThanEqual(p, map_size))) return vec4(0);
    return texture(terrain_data, (p + vec2(0.5)) / map_size);
}
vec3 neighbor(vec2 p, vec4 center) {
    vec4 value = cell(p);
    // Never blend a different terrace or the outside of the playable board into this surface.
    return abs(value.a - center.a) < 0.02 ? value.rgb : center.rgb;
}
void fragment() {
    vec2 p = UV;
    vec4 center = cell(floor(p + vec2(0.5)));
    vec2 base = floor(p);
    vec2 f = smoothstep(vec2(0.13), vec2(0.87), fract(p));
    vec3 terrain = mix(mix(neighbor(base, center), neighbor(base + vec2(1,0), center), f.x),
        mix(neighbor(base + vec2(0,1), center), neighbor(base + vec2(1,1), center), f.x), f.y);
    float grain = noise(p * 9.0);
    vec3 paint = texture(TEXTURE, p / 7.5).rgb * vec3(0.96, 1.0, 1.04);
    float vegetation = terrain.g * (0.25 + 0.27 * noise(p * 3.0));
    paint = mix(paint, paint * vec3(0.59, 0.72, 0.47), vegetation);
    paint = mix(paint, paint * vec3(0.67, 0.64, 0.55), terrain.b * 0.67);
    float water_field = terrain.r + (grain - 0.5) * 0.055;
    float bank = smoothstep(0.14, 0.44, water_field);
    paint = mix(paint, paint * vec3(0.62, 0.72, 0.68), bank * 0.48);
    float wet = smoothstep(0.43, 0.55, water_field);
    vec3 water = mix(vec3(0.30,0.55,0.52), vec3(0.13,0.34,0.36), smoothstep(0.53, 1.0, water_field));
    water += (texture(TEXTURE, p / 3.5 + vec2(TIME * 0.001, 0)).rgb - vec3(0.65)) * 0.16;
    float ripple = pow(0.5 + 0.5 * sin(p.x * 28.0 + p.y * 37.0 + noise(p * 4.0) * 7.0 - TIME * 0.7), 16.0);
    water += vec3(0.13,0.20,0.18) * ripple * (0.3 + grain * 0.7);
    float foam = smoothstep(0.43, 0.49, water_field) * (1.0 - smoothstep(0.49,0.57,water_field));
    paint = mix(paint, water, wet);
    paint = mix(paint, vec3(0.73,0.79,0.67), foam * (0.22 + grain * 0.35));
    COLOR = vec4(paint, 1.0);
}
