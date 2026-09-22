shader_type spatial;

// Ensures both sides of the flag are visible and lighting renders correctly
render_mode depth_draw_always, cull_disabled;

uniform sampler2D flagTexture;
uniform float wave_speed = 3.0;
uniform float wave_frequency = 4.0;
uniform float wave_amplitude = 0.15;

void vertex() {
    // UV.x is 0.0 at the left edge (flagpole) and 1.0 at the right edge
    float distance_from_left = UV.x; 
    
    // Calculate the wave timing based on horizontal position
    float wave_time = TIME * wave_speed - UV.x * wave_frequency;
    
    // Y-axis displacement moves the vertices up and down
    float wave_y = sin(wave_time) * wave_amplitude;
    
    // X-axis displacement keeps the flag's length consistent so it doesn't stretch
    float wave_x = (cos(wave_time) - 1.0) * (wave_amplitude * 0.5);
    
    // Apply the vertical wave and the length correction, anchored to the left
    VERTEX.y += wave_y * distance_from_left;
    VERTEX.x += wave_x * distance_from_left;
}

void fragment() {
    vec4 sampleColor = texture(flagTexture, UV);
    ALBEDO = sampleColor.rgb;
}