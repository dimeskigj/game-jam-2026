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
		// 1. Standard XRayable items (Overlay only)
		var nodes = GetTree().GetNodesInGroup("XRayable");
		
		foreach (var node in nodes)
		{
			// Apply to the node itself if it's a visual
			if (node is GeometryInstance3D geoInstance)
			{
				geoInstance.MaterialOverlay = enabled ? XRayOverlayMaterial : null;
			}
			
			// ALSO check for common child visuals
			var childVis = node.GetNodeOrNull<GeometryInstance3D>("Mesh") 
						?? node.GetNodeOrNull<GeometryInstance3D>("Visual") 
						?? node.GetNodeOrNull<GeometryInstance3D>("MeshInstance3D");
		   
			if (childVis != null && childVis != node) 
			{
				childVis.MaterialOverlay = enabled ? XRayOverlayMaterial : null;
			}
		}

		// 2. Invisible XRay items (Toggle Visibility)
		var hiddenNodes = GetTree().GetNodesInGroup("XRayInvisible");
		foreach (var node in hiddenNodes)
		{
			if (node is Node3D n3d)
			{
				n3d.Visible = enabled;
			}
			else if (node is CanvasItem ci)
			{
				ci.Visible = enabled;
			}
		}
	}
}
