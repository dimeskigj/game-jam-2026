shader_type canvas_item;

uniform sampler2D SCREEN_TEXTURE : hint_screen_texture, filter_linear_mipmap;
uniform vec4 color : source_color = vec4(0.0, 0.5, 1.0, 0.1); // Reduced alpha from 0.2 to 0.1
uniform float scanline_count = 50.0;

void fragment() {
	vec2 uv = SCREEN_UV;
	
	// Scanline effect - reduced intensity
	float s = sin(uv.y * scanline_count + TIME * 10.0);
	vec4 scanline = vec4(vec3(s * 0.05), 1.0); // Reduced scanline intensity
	
	// Basic color tint - getting screen texture correctly
	vec4 screen_color = texture(SCREEN_TEXTURE, uv);
	
	// Mix screen color with tint, but keep it subtle
	COLOR = mix(screen_color, color, 0.15); // Reduced mix strength
	COLOR += scanline * 0.1; // Reduced scanline add
	
	// Vignette (dark corners)
	float d = length(uv - 0.5) * 2.0;
	COLOR.rgb *= (1.0 - d * 0.4); // Darken corners slightly
}
