shader_type spatial;
render_mode blend_add, depth_draw_never, cull_disabled, unshaded, shadows_disabled;

uniform vec4 cone_color : source_color = vec4(1.0, 0.95, 0.7, 0.22);
uniform float falloff_power : hint_range(0.5, 4.0) = 1.6;
uniform float end_fade_amount : hint_range(0.1, 1.0) = 0.6;
uniform float edge_softness : hint_range(0.0, 1.0) = 0.35;
uniform float center_transparency : hint_range(0.0, 1.0) = 0.06;
uniform float proximity_fade_distance : hint_range(0.1, 4.0) = 1.2;
uniform float noise_strength : hint_range(0.0, 1.0) = 0.15;
uniform bool invert_falloff = false;
uniform sampler2D depth_texture : hint_depth_texture, filter_linear_mipmap;

void fragment() {
    // 1. Length falloff: bright near lens, softly fading down the length
    float v = invert_falloff ? (1.0 - UV.y) : UV.y;
    float length_fade = pow(clamp(v, 0.0, 1.0), falloff_power);

    // 2. Wide smooth dissolve at the end of the cone so there is NO sharp circular edge
    length_fade *= smoothstep(0.0, end_fade_amount, v);

    // 3. Silhouette edge blur: soften the sharp geometric contours along the sides
    float dot_nv = clamp(abs(dot(NORMAL, VIEW)), 0.0, 1.0);
    float edge_blur = smoothstep(0.0, max(edge_softness, 0.001), dot_nv);
    float fresnel = pow(1.0 - dot_nv, 1.2);
    float view_factor = mix(center_transparency, 1.0, fresnel) * edge_blur;

    // 4. Proximity depth fade: soft cubic blend against floors, walls, and shelves
    float depth = texture(depth_texture, SCREEN_UV).x;
    vec3 ndc = vec3(SCREEN_UV * 2.0 - 1.0, depth);
    vec4 view = INV_PROJECTION_MATRIX * vec4(ndc, 1.0);
    view.xyz /= (abs(view.w) > 0.00001 ? view.w : 0.00001);
    float linear_depth = -view.z;
    float obj_depth = -VERTEX.z;
    float diff = linear_depth - obj_depth;
    float prox_fade = smoothstep(0.0, max(proximity_fade_distance, 0.001), diff);

    // 5. Subtle volumetric dust / air haze variation
    vec3 world_pos = (INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).xyz;
    float dust_noise = sin(world_pos.x * 1.5 + TIME * 0.15) * cos(world_pos.y * 1.5) * sin(world_pos.z * 1.5);
    float dust = 1.0 + dust_noise * noise_strength;

    // Combine into final smooth alpha
    float alpha = cone_color.a * length_fade * view_factor * prox_fade * dust;

    ALBEDO = cone_color.rgb;
    ALPHA = clamp(alpha, 0.0, 1.0);
}
