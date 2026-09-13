shader_type spatial;
render_mode cull_front, unshaded;

uniform float outline_width : hint_range(0.0005, 0.03) = 0.0028;
uniform vec4 outline_color : source_color = vec4(0.06, 0.06, 0.09, 1.0);
uniform float min_distance : hint_range(0.1, 5.0) = 0.5;
uniform float max_distance : hint_range(5.0, 50.0) = 25.0;

void vertex()
{
	vec3 world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	vec3 world_normal = normalize(MODEL_NORMAL_MATRIX * NORMAL);
	float dist = length(world_pos - CAMERA_POSITION_WORLD);
	float factor = clamp(dist, min_distance, max_distance);
	world_pos += world_normal * (outline_width * factor);
	POSITION = PROJECTION_MATRIX * (VIEW_MATRIX * vec4(world_pos, 1.0));
}

void fragment()
{
	ALBEDO = outline_color.rgb;
	ALPHA = outline_color.a;
}
