shader_type spatial;
render_mode unshaded, cull_front;

uniform vec4 outline_color : source_color = vec4(1.0, 0.4, 0.0, 1.0); // Unity Orange
uniform float outline_width = 0.05;

void vertex() {
	// Grow the object by scaling it relative to its center (approx).
	// This works better for cubes/hard edges than Normal extrusion.
	VERTEX *= (1.0 + outline_width);
}

void fragment() {
	ALBEDO = outline_color.rgb;
}
