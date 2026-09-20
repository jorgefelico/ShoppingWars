shader_type spatial;
render_mode cull_front, unshaded;

uniform float outline_width : hint_range(0.001, 0.1) = 0.0067;
uniform vec4 outline_color : source_color = vec4(1.0, 0.85, 0.1, 1.0);

void vertex()
{
	vec4 view_pos = VIEW_MATRIX * (MODEL_MATRIX * vec4(VERTEX, 1.0));
	vec3 view_normal = mat3(VIEW_MATRIX) * (mat3(MODEL_MATRIX) * NORMAL);
	
	float dist = max(-view_pos.z, 0.1);
	float factor = clamp(sqrt(dist) * 0.6, 0.85, 2.5);
	
	view_pos.xyz += normalize(view_normal) * (outline_width * factor);
	view_pos.z += 0.0005;
	
	POSITION = PROJECTION_MATRIX * view_pos;
}

void fragment()
{
	float pulse = 0.88 + 0.12 * sin(TIME * 6.0);
	ALBEDO = outline_color.rgb * pulse;
	ALPHA = outline_color.a;
}
