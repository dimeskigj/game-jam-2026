using Godot;

public partial class FlashlightPickup : RigidBody3D
{
	public override void _Ready()
	{
		if (GlobalSceneManager.Instance != null && GlobalSceneManager.Instance.CollectedItemPaths.Contains(GetPath().ToString()))
		{
			QueueFree();
		}
	}

	public void Interact(PlayerController player)
	{
		player.CollectFlashlight();
		if (GlobalSceneManager.Instance != null)
		{
			GlobalSceneManager.Instance.CollectedItemPaths.Add(GetPath().ToString());
		}
		QueueFree();
	}
}
