using Godot;

public partial class CellRoomSetup : Node3D
{
	public override void _Ready()
	{
		GD.Print("CellRoomSetup: Generating collision for all meshes in scene...");
		GenerateCollisionRecursive(this);
	}

	private void GenerateCollisionRecursive(Node node)
	{
		if (node is MeshInstance3D meshInstance)
		{
			// Skip transparent/helper meshes like outline overlays if necessary, 
			// but generally we want collision on static geometry.
			// Skip if it already has a static body child or parent is a dynamic object like Player
			if (ShouldGenerateCollision(meshInstance))
			{
				bool hasStaticBody = false;
				foreach(var child in meshInstance.GetChildren())
				{
					if (child is StaticBody3D)
					{
						hasStaticBody = true;
						break;
					}
				}

				if (!hasStaticBody)
				{
					meshInstance.CreateTrimeshCollision();
					GD.Print($"Generated collision for {meshInstance.Name} ({meshInstance.GetPath()})");
				}
			}
		}

		foreach (Node child in node.GetChildren())
		{
			GenerateCollisionRecursive(child);
		}
	}

	private bool ShouldGenerateCollision(MeshInstance3D mesh)
	{
		// Don't generate collision for the player model or other specific entities
		// if they are just visuals.
		Node parent = mesh.GetParent();
		if (parent != null && (parent.Name == "Player" || parent is CharacterBody3D || parent is RigidBody3D))
		{
			return false;
		}
		
		// If it's a door visual, the Door script usually handles collision via a predefined box/shape,
		// but if it's the visual mesh, we usually don't want trimesh collision interfering 
		// unless it's strictly static. RigidBody3D doors have their own shapes.
		if (parent is RigidBody3D) return false;
		
		// Heuristic: If it looks like a visual effect (PlaneMesh with small size?), maybe check material
		// But for now, assume all static meshes need collision.
		return true;
	}
}
