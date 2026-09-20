shader_type spatial;
render_mode cull_front, unshaded;

uniform float outline_width : hint_range(0.0005, 0.03) = 0.0028;
uniform vec4 outline_color : source_color = vec4(0.06, 0.06, 0.09, 1.0);
uniform float min_distance : hint_range(0.1, 5.0) = 0.5;
uniform float max_distance : hint_range(5.0, 50.0) = 25.0;

void vertex()
{
	vec4 view_pos = VIEW_MATRIX * (MODEL_MATRIX * vec4(VERTEX, 1.0));
	vec3 view_normal = mat3(VIEW_MATRIX) * (mat3(MODEL_MATRIX) * NORMAL);
	
	// Sub-linear distance damping preserves fine comic linework without bloat down aisles
	float dist = max(-view_pos.z, 0.1);
	float factor = clamp(sqrt(dist) * 0.55, 0.75, 2.2);
	
	view_pos.xyz += normalize(view_normal) * (outline_width * factor);
	view_pos.z += 0.0005;
	
	POSITION = PROJECTION_MATRIX * view_pos;
}

void fragment()
{
	ALBEDO = outline_color.rgb;
	ALPHA = outline_color.a;
}
