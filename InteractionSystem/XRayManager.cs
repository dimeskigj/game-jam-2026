using Godot;
using System.Collections.Generic;

public partial class XRayManager : Node
{
    [Export]
    public Material XRayOverlayMaterial;

    private List<MeshInstance3D> _xrayItems = new List<MeshInstance3D>();

    public override void _Ready()
    {
        GD.Print("XRayManager: Initialized and ready.");
    }

    public void ToggleXRay(bool enabled)
    {
        GD.Print($"XRayManager: Toggling X-Ray {(enabled ? "ON" : "OFF")}");
        
        // 1. Standard XRayable items (recursive search for visuals)
        var nodes = GetTree().GetNodesInGroup("XRayable");
        GD.Print($"XRayManager: Found {nodes.Count} XRayable nodes.");
        foreach (var node in nodes)
        {
            ApplyXRayRecursively(node, enabled);
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

    private void ApplyXRayRecursively(Node node, bool enabled)
    {
        if (node is GeometryInstance3D geoInstance)
        {
            geoInstance.MaterialOverlay = enabled ? XRayOverlayMaterial : null;
        }

        foreach (Node child in node.GetChildren())
        {
            ApplyXRayRecursively(child, enabled);
        }
    }
}
