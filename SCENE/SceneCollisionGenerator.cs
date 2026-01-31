using Godot;

public partial class SceneCollisionGenerator : Node
{
	[Export] public bool AutoGenerateOnReady = true;

	public override void _Ready()
	{
		if (AutoGenerateOnReady)
		{
			GD.Print("SceneCollisionGenerator: Generating collisions for buildings...");
			GenerateCollisions(GetParent());
		}
	}

	private void GenerateCollisions(Node node)
	{
		if (node is MeshInstance3D meshInstance)
		{
			// Create trimesh collision creates a StaticBody3D child with a ConcavePolygonShape3D
			meshInstance.CreateTrimeshCollision();
			GD.Print($"  Generated collision for mesh: {meshInstance.Name}");
		}

		foreach (Node child in node.GetChildren())
		{
			GenerateCollisions(child);
		}
	}
}
