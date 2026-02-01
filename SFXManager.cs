using Godot;
using System;

public partial class SFXManager : Node
{
	public static SFXManager Instance { get; private set; }

	public override void _Ready()
	{
		Instance = this;
		// Ensure this node stays around regardless of scene changes
		ProcessMode = ProcessModeEnum.Always;
	}

	public void PlaySound3D(AudioStream stream, Vector3 globalPosition, float volumeDb = 0f, float pitchVariation = 0.1f)
	{
		if (stream == null) return;

		AudioStreamPlayer3D player = new AudioStreamPlayer3D();
		AddChild(player); // Player is a child of the Autoload, so it persists across scene changes
		
		player.Stream = stream;
		player.GlobalPosition = globalPosition;
		player.VolumeDb = volumeDb;
		player.Bus = "SFX";
		
		// Add some pitch variety
		player.PitchScale = 1.0f + (float)GD.RandRange(-pitchVariation, pitchVariation);
		
		player.Finished += () => player.QueueFree();
		player.Play();
	}

	public void PlayRandomSound3D(AudioStream[] streams, Vector3 globalPosition, float volumeDb = 0f, float pitchVariation = 0.1f)
	{
		if (streams == null || streams.Length == 0) return;
		int index = (int)(GD.Randi() % (uint)streams.Length);
		PlaySound3D(streams[index], globalPosition, volumeDb, pitchVariation);
	}
}
