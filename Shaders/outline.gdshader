shader_type spatial;
render_mode cull_front, unshaded;

uniform float outline_width : hint_range(0.001, 0.1) = 0.0067;
uniform vec4 outline_color : source_color = vec4(1.0, 0.85, 0.1, 1.0);

void vertex()
{
	vec3 world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	vec3 world_normal = normalize(MODEL_NORMAL_MATRIX * NORMAL);
	float dist = length(world_pos - CAMERA_POSITION_WORLD);
	world_pos += world_normal * (outline_width * dist);
	POSITION = PROJECTION_MATRIX * (VIEW_MATRIX * vec4(world_pos, 1.0));
}

void fragment()
{
	ALBEDO = outline_color.rgb;
	ALPHA = outline_color.a;
}
