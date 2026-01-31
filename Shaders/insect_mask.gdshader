shader_type canvas_item;

uniform sampler2D SCREEN_TEXTURE : hint_screen_texture, filter_linear_mipmap;
uniform float distortion_strength : hint_range(0.0, 0.2) = 0.08;

// Pseudo-random noise function
float random(vec2 uv) {
	return fract(sin(dot(uv, vec2(12.9898, 78.233))) * 43758.5453123);
}

float noise(vec2 uv) {
	vec2 i = floor(uv);
	vec2 f = fract(uv);
	float a = random(i);
	float b = random(i + vec2(1.0, 0.0));
	float c = random(i + vec2(0.0, 1.0));
	float d = random(i + vec2(1.0, 1.0));
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
}

void fragment() {
	vec2 uv = SCREEN_UV;
	float t = TIME;
	
	// INFERNO NOISE LAYERS - Much faster and chaotic
	// Added horizontal movement to simulate swirling drafts
	float n1 = noise(uv * 4.0 + vec2(sin(t)*0.2, -t * 4.0)); 
	float n2 = noise(uv * 8.0 + vec2(cos(t * 1.5)*0.1, -t * 8.0));
	float n3 = noise(uv * 16.0 + vec2(0.0, -t * 12.0));
	
	float combined_noise = (n1 + n2 * 0.5 + n3 * 0.25) / 1.75;
	
	// EXTREME HEAT DISTORTION
	// Wavy pattern added to distortion to simulate rising heat waves
	float wave = sin(uv.y * 20.0 - t * 10.0) * 0.005;
	float dist_amount = (combined_noise - 0.5) * distortion_strength;
	vec2 distorted_uv = uv + vec2(dist_amount + wave, dist_amount);
	
	// Get screen texture
	vec4 screen = texture(SCREEN_TEXTURE, distorted_uv);
	
	// HELLFIRE COLOR PALETTE
	vec3 dark_char = vec3(0.05, 0.0, 0.0);
	vec3 blood_red = vec3(0.8, 0.05, 0.0);
	vec3 blaze_orange = vec3(1.0, 0.4, 0.0);
	vec3 white_hot = vec3(1.0, 1.0, 0.8);
	
	// FLAME SHAPE & INTENSITY
	float d = distance(uv, vec2(0.5));
	// Mask that covers almost everything, only very center is somewhat clear
	float vignette = smoothstep(0.05, 0.7, d); 
	
	// Boost noise contrast to create sharp "tongues" of fire
	float fire_shape = pow(combined_noise, 1.2); 
	fire_shape = clamp(fire_shape * (vignette + 0.3) * 1.5, 0.0, 1.0);

	// COLOR GRADE based on fire intensity
	vec3 fire_color = mix(dark_char, blood_red, smoothstep(0.0, 0.3, fire_shape));
	fire_color = mix(fire_color, blaze_orange, smoothstep(0.3, 0.6, fire_shape));
	fire_color = mix(fire_color, white_hot, smoothstep(0.6, 1.0, fire_shape));
	
	// Pulsating red glow overlay
	float pulse = 0.2 + 0.1 * sin(t * 8.0);
	vec3 ambient_glow = vec3(0.8, 0.2, 0.0) * pulse;
	
	// Final Blend - Screen is tinted red/orange and overlaid with fire
	vec3 final = mix(screen.rgb * vec3(1.0, 0.7, 0.5), fire_color, fire_shape * 0.9);
	final += ambient_glow;
	
	COLOR = vec4(final, 1.0);
}
