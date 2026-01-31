using Godot;

public partial class InsectSpawner : Node3D
{
	[Export] public PackedScene InsectScene;
	[Export] public int SpawnCount = 30;
	[Export] public float SpawnRadius = 1.0f;

	public override void _Ready()
	{
		if (InsectScene == null)
		{
			GD.PrintErr("InsectSpawner: No InsectScene assigned.");
			return;
		}

		for (int i = 0; i < SpawnCount; i++)
		{
			SpawnInsect();
		}
	}

	private void SpawnInsect()
	{
		Node3D insect = InsectScene.Instantiate<Node3D>();
		GetParent().CallDeferred("add_child", insect);
		
		// Random offset
		Vector3 offset = new Vector3(
			(float)GD.RandRange(-SpawnRadius, SpawnRadius),
			0.5f, // Slightly above ground
			(float)GD.RandRange(-SpawnRadius, SpawnRadius)
		);
		
		insect.GlobalPosition = GlobalPosition + offset;
	}
}
