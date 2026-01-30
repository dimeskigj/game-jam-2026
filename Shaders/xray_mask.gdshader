shader_type canvas_item;

uniform sampler2D SCREEN_TEXTURE : hint_screen_texture, filter_linear_mipmap;
uniform float scanline_count : hint_range(0, 1000) = 150.0;

void fragment() {
    vec2 uv = SCREEN_UV;
    vec3 color = texture(SCREEN_TEXTURE, uv).rgb;
    
    // Green Tint
    color = mix(color, vec3(0.0, 1.0, 0.2), 0.3);
    
    // Scanlines
    float scanline = sin(uv.y * scanline_count * 3.14159);
    color *= (1.0 + 0.1 * scanline); // Subtle darkening
    
    // Vignette
    float d = distance(uv, vec2(0.5, 0.5));
    color *= smoothstep(0.8, 0.4, d * (1.0 + color.g * 0.2));
    
    // Boost Green channel
    color.g = 0.8;
    color.r *= 0.8;
    color.b *= color.b * 1.1;
    
    COLOR = vec4(color, 0.5);
}
