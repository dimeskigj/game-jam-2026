using Godot;

public partial class Pickup : RigidBody3D
{
	[Export]
	public InventoryItem ItemResource;

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
