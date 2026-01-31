using Godot;

public partial class SceneCollisionGenerator : Node
{
	[Export] public bool AutoGenerateOnReady = true;

	public override void _Ready()
	{
		if (AutoGenerateOnReady)
		{
			GD.Print("SceneCollisionGenerator: Generating collisions for city models...");
			GenerateCollisions(this);
		}
	}

	private void GenerateCollisions(Node node)
	{
		if (node is MeshInstance3D meshInstance)
		{
			// Skip if this mesh is part of a node that shouldn't have city collision
			if (!ShouldSkip(meshInstance))
			{
				meshInstance.CreateTrimeshCollision();
				GD.Print($"  Generated collision for mesh: {meshInstance.Name}");
			}
		}

		foreach (Node child in node.GetChildren())
		{
			GenerateCollisions(child);
		}
	}

	private bool ShouldSkip(Node node)
	{
		Node current = node;
		while (current != null)
		{
			if (current.IsInGroup("NoCollision") || 
				current.IsInGroup("Enemies") || 
				current is PhysicsBody3D || 
				current is PlayerController || 
				current.Name.ToString().Contains("KAMERA"))
			{
				return true;
			}
			current = current.GetParent();
		}
		return false;
	}
}
