using Godot;

public partial class RadioactiveZone : Area3D
{
	private PlayerController _player;

	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;
	}

	private void OnBodyEntered(Node3D body)
	{
		if (body is PlayerController player)
		{
			_player = player;
			GD.Print("Entered Radioactive Zone!");
		}
	}

	private void OnBodyExited(Node3D body)
	{
		 if (body is PlayerController player && player == _player)
		{
			_player = null;
			GD.Print("Left Radioactive Zone!");
		}
	}

	public override void _Process(double delta)
	{
		if (_player != null)
		{
			if (_player.CurrentMaskEffect != MaskEffect.Gas)
			{
				// Damage Player
				// _player.TakeDamage(delta * 10);
				// For now, simple console spam or check periodically
				if (Engine.GetPhysicsFrames() % 60 == 0)
				{
					GD.Print("COUGH! COUGH! Radiation poisoning!");
				}
			}
		}
	}
}
