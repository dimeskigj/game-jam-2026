using Godot;

public partial class CellRoomSetup : Node3D
{
	public override void _Ready()
	{
		GD.Print("CellRoomSetup: Generating collision for room models...");
		
		// Find the room model by attempting to find the known node, or searching
		Node room = GetNodeOrNull("TOP ROOM DONE");
		if (room != null)
		{
			AddCollisionToMesh(room);
		}
		else
		{
			GD.Print("CellRoomSetup: 'TOP ROOM DONE' node not found. Searching for other models...");
			foreach(Node child in GetChildren())
			{
				if (child is Node3D && child.Name.ToString().Contains("ROOM"))
				{
					AddCollisionToMesh(child);
				}
			}
		}
	}

	private void AddCollisionToMesh(Node node)
	{
		if (node is MeshInstance3D meshInstance)
		{
			// Only generate if it doesn't already have a static body child
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
				GD.Print($"Generated collision for {meshInstance.Name}");
			}
		}

		foreach (Node child in node.GetChildren())
		{
			AddCollisionToMesh(child);
		}
	}
}
