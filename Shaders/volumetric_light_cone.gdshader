shader_type spatial;
render_mode blend_add, depth_draw_opaque, cull_back, unshaded;

uniform vec4 color : source_color = vec4(1.0, 0.8, 0.4, 0.2);
uniform float brightness : hint_range(0.0, 10.0) = 1.0;
uniform float falloff : hint_range(0.1, 10.0) = 4.0;
uniform float edge_softness : hint_range(0.1, 10.0) = 2.0;
uniform float attenuation : hint_range(0.1, 10.0) = 2.0;

void fragment() {
    // Basic fresnel-like effect for softness at edges
    float d = dot(NORMAL, VIEW);
    float softness = pow(max(0.0, d), edge_softness);
    
    // Vertical falloff (assuming Y is the length of the cone)
    // In Godot, CylinderMesh/ConeMesh usually has Y up.
    // We'll map UV.y which goes from 0 to 1 along the height.
    float vertical_falloff = pow(1.0 - UV.y, attenuation);
    
    float final_alpha = color.a * softness * vertical_falloff * brightness;
    
    ALBEDO = color.rgb;
    ALPHA = clamp(final_alpha, 0.0, 1.0);
}
