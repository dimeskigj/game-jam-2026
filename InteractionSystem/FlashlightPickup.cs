using Godot;

public partial class FlashlightPickup : RigidBody3D
{
	public void Interact(PlayerController player)
	{
		player.CollectFlashlight();
		QueueFree();
	}
}
