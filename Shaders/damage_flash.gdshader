shader_type canvas_item;

uniform float intensity : hint_range(0.0, 1.0) = 0.0;
uniform vec4 flash_color : source_color = vec4(0.85, 0.04, 0.04, 1.0);
uniform float vignette_power : hint_range(0.5, 4.0) = 1.6;
uniform float base_flash : hint_range(0.0, 1.0) = 0.25;

void fragment() {
	vec2 uv = SCREEN_UV;
	vec2 dist = (uv - vec2(0.5)) * 2.0;
	float d = dot(dist, dist);
	float vignette = clamp(d * 0.75, 0.0, 1.0);
	float alpha = intensity * (base_flash + (1.0 - base_flash) * pow(vignette, vignette_power));
	COLOR = vec4(flash_color.rgb, clamp(alpha, 0.0, 0.9) * flash_color.a);
}
