shader_type spatial;
render_mode cull_back, diffuse_burley, specular_schlick_ggx;

uniform vec4 color_base : source_color = vec4(0.88, 0.90, 0.92, 1.0);
uniform vec4 color_top : source_color = vec4(0.16, 0.18, 0.21, 1.0);
uniform vec4 color_stripe : source_color = vec4(0.12, 0.48, 0.28, 1.0);
uniform vec4 color_trim : source_color = vec4(0.10, 0.11, 0.13, 1.0);
uniform vec4 color_groove : source_color = vec4(0.04, 0.05, 0.06, 1.0);

uniform float trim_height = 1.2;     // 1.2m above floor
uniform float stripe_start = 2.5;    // 2.5m above floor
uniform float stripe_height = 3.2;   // 3.2m tall stripe
uniform float top_start = 8.5;       // 8.5m above floor
uniform float panel_width = 4.0;     // 4m wide architectural drywall panels

varying vec3 world_pos;
varying vec3 world_norm;

void vertex() {
    vec4 wp = MODEL_MATRIX * vec4(VERTEX, 1.0);
    world_pos = wp.xyz;
    world_norm = normalize(mat3(MODEL_MATRIX) * NORMAL);
}

void fragment() {
    float h = world_pos.y;
    
    // Choose coordinate along wall based on normal direction
    float wall_u = abs(world_norm.x) > 0.5 ? world_pos.z : world_pos.x;
    
    // Architectural vertical panel seams every panel_width meters
    float panel_coord = mod(wall_u, panel_width);
    float seam_dist = min(panel_coord, panel_width - panel_coord);
    float seam_factor = smoothstep(0.0, 0.025, seam_dist); // 2.5cm seam groove
    
    // Horizontal reveal grooves at trim and stripe transitions
    float g1 = abs(h - trim_height);
    float g2 = abs(h - stripe_start);
    float g3 = abs(h - (stripe_start + stripe_height));
    float g4 = abs(h - top_start);
    float min_h_groove = min(min(g1, g2), min(g3, g4));
    float h_groove_factor = smoothstep(0.0, 0.02, min_h_groove); // 2cm transition groove
    
    float groove_mask = seam_factor * h_groove_factor;

    vec3 col = color_base.rgb;
    float rough = 0.65;
    float spec = 0.4;
    float metallic_val = 0.0;
    float rim_strength = 0.15;

    if (h < trim_height) {
        // Baseboard kick-plate (semi-gloss rubber/vinyl)
        col = color_trim.rgb;
        rough = 0.35;
        spec = 0.6;
        rim_strength = 0.25;
    } else if (h >= stripe_start && h < (stripe_start + stripe_height)) {
        // Accent brand stripe (vibrant satin department finish)
        col = color_stripe.rgb;
        rough = 0.32;
        spec = 0.65;
        rim_strength = 0.45;
    } else if (h >= top_start) {
        // Industrial roof deck
        col = color_top.rgb;
        rough = 0.88;
        spec = 0.2;
        rim_strength = 0.05;
    }

    // Apply architectural groove shadows
    col = mix(color_groove.rgb, col, groove_mask);
    rough = mix(0.9, rough, groove_mask);

    // Subtle painted eggshell micro-relief
    float micro_grain = sin(wall_u * 120.0) * cos(h * 120.0) * 0.015;
    col += vec3(micro_grain);

    // Soft Fresnel rim lighting for stylized volume
    float fresnel = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 3.0);

    ALBEDO = col;
    ROUGHNESS = clamp(rough, 0.05, 0.95);
    SPECULAR = spec;
    METALLIC = metallic_val;
    RIM = rim_strength;
    RIM_TINT = 0.4;
}