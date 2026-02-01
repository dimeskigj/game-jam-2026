using Godot;

public partial class BatteryPickup : RigidBody3D
{
	[Export] public float Amount = 25.0f;

	public void Interact(PlayerController player)
	{
		player.AddBattery(Amount);
		QueueFree();
	}
}
