using Godot;

public partial class SceneDoor : RigidBody3D
{
	[Export]
	public string TargetScenePath { get; set; } = "res://node_3d.tscn";
	
	[Export]
	public string DoorName { get; set; } = "Door";

	public override void _Ready()
	{
		Freeze = true; // Ensure it's not movable, act as static body for interaction
	}

	public void Interact()
	{
		GD.Print($"Interacting with SceneDoor to {TargetScenePath}");
		GlobalSceneManager.Instance.LoadScene(TargetScenePath);
	}
}
