shader_type canvas_item;

uniform float intensity : hint_range(0.0, 1.0) = 0.0;
uniform vec4 color : source_color = vec4(1.0, 0.0, 0.0, 1.0); // Red
uniform float frequency = 2.0;
uniform float vignette_amount = 0.7;

void fragment() {
    float time = TIME * frequency;
    
    // Vignette
    vec2 uv = UV;
    vec2 center = vec2(0.5, 0.5);
    float dist = distance(uv, center);
    float vignette = smoothstep(0.2, vignette_amount, dist);
    
    // Pulsing
    float pulse = (sin(time) * 0.5 + 0.5) * 0.2 + 0.8;
    
    // Scanlines
    float scanline = sin(UV.y * 100.0 + time * 5.0) * 0.05 + 0.95;
    
    vec4 final_color = color * vignette * pulse * scanline;
    
    // Apply intensity
    COLOR = vec4(final_color.rgb, final_color.a * intensity * 0.8);
}
