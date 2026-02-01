using Godot;

public partial class SceneDoor : RigidBody3D
{
	[Export]
	public string TargetScenePath { get; set; } = "res://node_3d.tscn";
	
	[Export]
	public AudioStream[] OpenSounds { get; set; }
	
	[Export]
	public string DoorName { get; set; } = "Door";

	public override void _Ready()
	{
		Freeze = true; // Ensure it's not movable, act as static body for interaction
	}

	public void Interact()
	{
		GD.Print($"Interacting with SceneDoor to {TargetScenePath}");
		if (OpenSounds != null && OpenSounds.Length > 0)
		{
			// Capture position BEFORE freeing node during scene change
			Vector3 soundPosition = GlobalPosition;
			SFXManager.Instance.PlayRandomSound3D(OpenSounds, soundPosition);
		}
		GlobalSceneManager.Instance.LoadScene(TargetScenePath);
	}
}
