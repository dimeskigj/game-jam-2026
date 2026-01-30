using Godot;
using System.Collections.Generic;

public partial class XRayManager : Node
{
    [Export]
    public Material XRayOverlayMaterial;

    private List<MeshInstance3D> _xrayItems = new List<MeshInstance3D>();

    public override void _Ready()
    {
        // Find all nodes in "XRayable" group
        // Note: For dynamic items, we should update this list or use signals.
        // For Game Jam, getting them on Ready is fine, assuming static or pre-placed items.
        // Or we can fetch group whenever we toggle.
    }

    public void ToggleXRay(bool enabled)
    {
        var nodes = GetTree().GetNodesInGroup("XRayable");
        
        foreach (var node in nodes)
        {
            // 1. Apply to the node itself if it's a visual
            if (node is GeometryInstance3D geoInstance)
            {
                geoInstance.MaterialOverlay = enabled ? XRayOverlayMaterial : null;
            }
            
            // 2. ALSO check for common child visuals (common in simple prefabs/pickups)
            // This handles the case where the parent is a container (Node3D or empty MeshInstance)
            var childVis = node.GetNodeOrNull<GeometryInstance3D>("Mesh") 
                        ?? node.GetNodeOrNull<GeometryInstance3D>("Visual") 
                        ?? node.GetNodeOrNull<GeometryInstance3D>("MeshInstance3D");
           
            if (childVis != null && childVis != node) // Ensure we don't double set if named same
            {
                childVis.MaterialOverlay = enabled ? XRayOverlayMaterial : null;
            }
        }
    }
}
