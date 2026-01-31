shader_type canvas_item;

uniform sampler2D SCREEN_TEXTURE : hint_screen_texture, filter_linear_mipmap;
uniform float intensity = 0.0; // 0.0 = no effect, 1.0 = full dizzy

void fragment() {
	vec2 uv = SCREEN_UV;
	
	// Distortion params
	float wave_speed = 2.0;
	float wave_freq = 10.0;
	float wave_amp = 0.02 * intensity; // Max deviation
	
	// 1. Wavy Distortion
	float off_x = sin(uv.y * wave_freq + TIME * wave_speed) * wave_amp;
	float off_y = cos(uv.x * wave_freq + TIME * wave_speed * 0.8) * wave_amp;
	
	vec2 distorted_uv = uv + vec2(off_x, off_y);
	
	// 2. Chromatic Aberration (Color Split)
	// Higher intensity = more split
	float split_amount = 0.02 * intensity;
	
	float r = texture(SCREEN_TEXTURE, distorted_uv + vec2(split_amount, 0.0)).r;
	float g = texture(SCREEN_TEXTURE, distorted_uv).g;
	float b = texture(SCREEN_TEXTURE, distorted_uv - vec2(split_amount, 0.0)).b;
	
	COLOR = vec4(r, g, b, 1.0);
	
	// 3. Optional pulsing vignette or blur for low sanity could go here
	// Dark vignette at edges
	float d = distance(uv, vec2(0.5));
	float vignette = smoothstep(0.8, 0.4, d * (1.0 + intensity * 0.5));
	COLOR.rgb *= mix(1.0, vignette, intensity * 0.8);
}
