using Godot;

[Tool]
public partial class ApplyMaskMaterial : Node3D
{
	[Export] public Material MaskMaterial;

	public override void _Ready()
	{
		Apply();
	}

	public override void _Process(double delta)
	{
		// Continuous check in editor if needed, or just on Ready
		if (Engine.IsEditorHint())
		{
			// Optional: Apply occasionally if structure changes, but manual call is better
		}
	}

	public void Apply()
	{
		if (MaskMaterial == null) 
		{
			GD.Print("ApplyMaskMaterial: No material assigned.");
			return;
		}
		
		ApplyToNode(this);
	}

	private void ApplyToNode(Node node)
	{
		if (node is MeshInstance3D meshInstance)
		{
			// Override the material surface 0 usually
			meshInstance.MaterialOverride = MaskMaterial;
		}

		foreach (Node child in node.GetChildren())
		{
			ApplyToNode(child);
		}
	}
}
