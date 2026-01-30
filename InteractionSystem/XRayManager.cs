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
            if (node is MeshInstance3D meshInstance)
            {
                if (enabled)
                {
                    meshInstance.MaterialOverlay = XRayOverlayMaterial;
                }
                else
                {
                    meshInstance.MaterialOverlay = null;
                }
            }
            // Logic for Pickups inheriting MeshInstance or having one
            else if (node is Node n)
            {
               var mesh = n.GetNodeOrNull<MeshInstance3D>("MeshInstance3D");
               if (mesh != null)
               {
                    mesh.MaterialOverlay = enabled ? XRayOverlayMaterial : null;
               }
            }
        }
    }
}
