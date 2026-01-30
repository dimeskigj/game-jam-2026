shader_type canvas_item;

uniform sampler2D SCREEN_TEXTURE : hint_screen_texture, filter_linear_mipmap;

void fragment() {
    vec2 uv = SCREEN_UV;
    
    // Distortion (Fish eye lens-ish)
    vec2 d = uv - 0.5;
    float r = length(d);
    float bind = length(d);
    
    // Vignette (Strong)
    vec3 color = texture(SCREEN_TEXTURE, uv).rgb;
    float vig = 1.0 - smoothstep(0.4, 0.7, r);
    color *= vig;
    
    // Sepia/Dark Tint
    vec3 sepia = vec3(1.2, 1.0, 0.8);
    float gray = dot(color, vec3(0.299, 0.587, 0.114));
    vec3 mixed = mix(color, vec3(gray) * sepia, 0.5);
    
    // Breathing/Pulse effect (using time if we passed it, but static is okay for now)
    
    COLOR = vec4(mixed * 0.8, 1.0);
}
