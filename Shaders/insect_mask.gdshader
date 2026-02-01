shader_type canvas_item;

uniform sampler2D SCREEN_TEXTURE : hint_screen_texture, filter_linear;
uniform float distortion_strength : hint_range(0.0, 0.5) = 0.05;
uniform float cell_count : hint_range(10.0, 200.0) = 50.0;
uniform vec4 color_tint : source_color = vec4(1.0, 0.9, 0.0, 0.1);

void fragment() {
    vec2 uv = SCREEN_UV;
    float t = TIME;
    
    // Divide screen into a staggered grid (Compound Eye / Ommatidia)
    // This is mathematically smooth and won't look "pixelated"
    vec2 grid_uv = uv * cell_count;
    grid_uv.x += mod(floor(grid_uv.y), 2.0) * 0.5;
    
    vec2 f = fract(grid_uv) - 0.5;
    float dist = length(f);
    
    // Create a smooth convex lens effect for each individual cell
    // We add a tiny bit of animation to make it feel "organic"
    float animated_dist = distortion_strength * (1.0 + 0.1 * sin(t * 2.0 + grid_uv.x + grid_uv.y));
    vec2 offset = f * animated_dist / cell_count;
    
    // Sample screen with distortion
    vec4 screen = texture(SCREEN_TEXTURE, uv + offset);
    
    // Darken the edges of each cell to create the honeycomb structure
    float border = smoothstep(0.5, 0.4, dist);
    screen.rgb *= border;
    
    // Add a center highlight for each eye
    float highlight = smoothstep(0.15, 0.0, dist);
    screen.rgb += vec3(1.0) * highlight * 0.1 * border;
    
    // Apply insect vision tint
    screen.rgb = mix(screen.rgb, color_tint.rgb, color_tint.a);
    
    COLOR = screen;
}
