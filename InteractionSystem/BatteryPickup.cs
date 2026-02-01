using Godot;

public partial class BatteryPickup : RigidBody3D
{
	[Export] public float Amount = 25.0f;

	public override void _Ready()
	{
		if (GlobalSceneManager.Instance != null && GlobalSceneManager.Instance.CollectedItemPaths.Contains(GetPath().ToString()))
		{
			QueueFree();
		}
	}

	public void Interact(PlayerController player)
	{
		player.AddBattery(Amount);
		if (GlobalSceneManager.Instance != null)
		{
			GlobalSceneManager.Instance.CollectedItemPaths.Add(GetPath().ToString());
		}
		QueueFree();
	}
}
