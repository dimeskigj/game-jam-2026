using Godot;

// Ensure this script inherits RigidBody3D, as Pickups are physics objects.
public partial class Pickup : RigidBody3D
{
	[Export]
	public InventoryItem ItemResource;

	public override void _Ready()
	{
		Mass = 0.5f; // Lightweight but stable
		
		// Check if already collected
		if (GlobalSceneManager.Instance != null && GlobalSceneManager.Instance.CollectedItemPaths.Contains(GetPath().ToString()))
		{
			QueueFree();
		}
	}

	public bool Interact(Inventory inventory)
	{
		if (ItemResource != null)
		{
			if (inventory.AddItem(ItemResource))
			{
				if (GlobalSceneManager.Instance != null)
				{
					GlobalSceneManager.Instance.CollectedItemPaths.Add(GetPath().ToString());
				}
				QueueFree(); // Destroy pickup if added successfully
				return true;
			}
			else
			{
				GD.Print("Inventory Full!");
				return false;
			}
		}
		return false;
	}
}
