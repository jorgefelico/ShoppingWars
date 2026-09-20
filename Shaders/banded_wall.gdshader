shader_type spatial;
    render_mode cull_back, diffuse_lambert, specular_schlick_ggx;

    uniform vec4 color_base : source_color = vec4(0.82, 0.84, 0.87, 1.0);
    uniform vec4 color_top : source_color = vec4(0.11, 0.12, 0.15, 1.0);
    uniform vec4 color_stripe : source_color = vec4(0.18, 0.49, 0.20, 1.0);
    uniform vec4 color_trim : source_color = vec4(0.09, 0.10, 0.12, 1.0);

    uniform float trim_height = 1.2;     // 1.2m above floor
    uniform float stripe_start = 5.0;    // 5m above floor
    uniform float stripe_height = 2.0;   // 2m tall stripe
    uniform float top_start = 9.0;       // 9m above floor
    uniform float roughness_val = 0.85;

    varying float world_y;

    void vertex() {
        // Calculates the true physical height in meters above the floor (Y = 0)
        world_y = (MODEL_MATRIX * vec4(VERTEX, 1.0)).y;
    }

    void fragment() {
        float h = world_y;

        vec3 col = color_base.rgb;

        if (h < trim_height) {
            col = color_trim.rgb;
        } else if (h >= stripe_start && h < (stripe_start + stripe_height)) {
            col = color_stripe.rgb;
        } else if (h >= top_start) {
            col = color_top.rgb;
        }

        ALBEDO = col;
        ROUGHNESS = roughness_val;
        SPECULAR = 0.2;
    }