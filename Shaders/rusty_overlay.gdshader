shader_type canvas_item;

// Reading the screen content allows us to manipulate the game look directly
uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_nearest;

// Pixelation
uniform float pixel_size : hint_range(1.0, 16.0) = 3.0; // Higher = more pixelated

// Effects
uniform float grain_amount : hint_range(0.0, 1.0) = 0.15;
uniform float vignette_intensity = 0.6;
uniform float vignette_opacity : hint_range(0.0, 1.0) = 0.4;
uniform vec4 tint_color : source_color = vec4(0.45, 0.35, 0.2, 0.1); // Slightly brownish/rusty

float random(vec2 uv) {
    return fract(sin(dot(uv, vec2(12.9898, 78.233))) * 43758.5453123);
}

void fragment() {
    // 1. Pixelation
    // We calculate UVs that "snap" to a grid based on pixel_size
    vec2 size = 1.0 / SCREEN_PIXEL_SIZE;
    vec2 grid_uv = round(SCREEN_UV * (size / pixel_size)) / (size / pixel_size);
    
    // Read scene from these snapped UVs
    vec4 scene_col = texture(screen_texture, grid_uv);
    
    // 2. Grain/Noise (animated by TIME)
    float noise = random(grid_uv + vec2(TIME * 5.0, 0.0));
    
    // 3. Vignette
    vec2 uv = SCREEN_UV;
    uv *=  1.0 - uv.yx;
    float vig = uv.x * uv.y * 15.0;
    vig = pow(vig, vignette_intensity); // 0 at edge, 1 at center-ish
    vig = clamp(vig, 0.0, 1.0);
    float inverse_vig = 1.0 - vig; // 1 at edge, 0 at center
    
    // 4. Composition
    vec3 final_rgb = scene_col.rgb;
    
    // Tinting (mix with rusty color)
    final_rgb = mix(final_rgb, tint_color.rgb, tint_color.a);
    
    // Add Grain (roughness)
    final_rgb += (noise - 0.5) * grain_amount;
    
    // Apply Vignette (darken edges)
    final_rgb = mix(final_rgb, vec3(0.0), inverse_vig * vignette_opacity);
    
    COLOR = vec4(final_rgb, 1.0);
}
