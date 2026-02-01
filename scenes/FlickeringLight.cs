using Godot;
using System;

public partial class FlickeringLight : Node3D
{
	[Export] public float MinEnergy = 0.5f;
	[Export] public float MaxEnergy = 2.0f;
	[Export] public float FlickerSpeed = 0.1f;
	[Export] public float OffChance = 0.05f;
	
	private AudioStreamPlayer3D _audioPlayer;
	private AudioStreamGeneratorPlayback _playback;
	private OmniLight3D _light;
	private MeshInstance3D _bulbMesh;
	private StandardMaterial3D _bulbMat;
	
	private float _timer = 0.0f;
	private float _sampleRate = 44100.0f;
	private float _phase = 0.0f;
	private float _buzzFrequency = 100.0f; // Base hum

	public override void _Ready()
	{
		_light = GetNode<OmniLight3D>("Light");
		_bulbMesh = GetNodeOrNull<MeshInstance3D>("BulbMesh");
		_audioPlayer = GetNode<AudioStreamPlayer3D>("AudioStreamPlayer3D");
		
		if (_bulbMesh != null)
		{
			_bulbMat = _bulbMesh.Mesh.SurfaceGetMaterial(0)?.Duplicate() as StandardMaterial3D;
			if (_bulbMat == null) _bulbMat = new StandardMaterial3D();
			_bulbMesh.MaterialOverride = _bulbMat;
		}

		// Setup Procedural Audio
		var generator = new AudioStreamGenerator();
		generator.MixRate = 44100;
		generator.BufferLength = 0.1f; // Short buffer for low latency
		_audioPlayer.Stream = generator;
		_audioPlayer.Play();
		_playback = _audioPlayer.GetStreamPlayback() as AudioStreamGeneratorPlayback;
		
		_sampleRate = generator.MixRate;
	}

	public override void _Process(double delta)
	{
		_timer -= (float)delta;
		if (_timer <= 0)
		{
			_timer = GD.Randf() * FlickerSpeed;
			Flicker();
		}
		
		UpdateAudio();
	}

	private void Flicker()
	{
		if (_light == null) return;

		bool turnOff = GD.Randf() < OffChance;
		
		if (turnOff)
		{
			_light.LightEnergy = 0;
			if (_bulbMat != null) _bulbMat.EmissionEnergyMultiplier = 0;
			// Volume handled in UpdateAudio via LightEnergy
		}
		else
		{
			float energy = (float)GD.RandRange(MinEnergy, MaxEnergy);
			_light.LightEnergy = energy;
			if (_bulbMat != null) _bulbMat.EmissionEnergyMultiplier = energy;
		}
	}

	private void UpdateAudio()
	{
		if (_playback == null) return;

		int framesAvailable = _playback.GetFramesAvailable();
		if (framesAvailable < 1) return;

		// Calculate volume based on light energy (0 to MaxEnergy)
		// We want 0 energy -> 0 volume
		float currentEnergy = _light.LightEnergy;
		float volume = Mathf.Clamp(currentEnergy / MaxEnergy, 0, 1) * 0.5f; 

		// Fill buffer
		Vector2[] frames = new Vector2[framesAvailable];
		for (int i = 0; i < framesAvailable; i++)
		{
			// Simple Sawtooth/Buzz
			float sample = ((_phase % 1.0f) * 2.0f) - 1.0f;
			
			// Add some randomness for "crackle"
			if (GD.Randf() < 0.1f) sample += (GD.Randf() * 2.0f - 1.0f) * 0.5f;

			_phase += _buzzFrequency / _sampleRate;
			
			frames[i] = new Vector2(sample * volume, sample * volume);
		}

		_playback.PushBuffer(frames);
	}
}
