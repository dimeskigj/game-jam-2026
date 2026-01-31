using Godot;

// Ensure this script inherits RigidBody3D, as Pickups are physics objects.
public partial class Pickup : RigidBody3D
{
	[Export]
	public InventoryItem ItemResource;

	public override void _Ready()
	{
		Mass = 0.5f; // Lightweight but stable
	}

	public void Interact(Inventory inventory)
	{
		if (ItemResource != null)
		{
			if (inventory.AddItem(ItemResource))
			{
				QueueFree(); // Destroy pickup if added successfully
			}
			else
			{
				GD.Print("Inventory Full!");
			}
		}
	}
}
