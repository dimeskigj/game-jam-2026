using Godot;

public partial class Radio : StaticBody3D
{
    [Export] public AudioStream MusicStream;
    
    private AudioStreamPlayer3D _audioPlayer;
    private bool _isOn = false;
    private MeshInstance3D _lightMesh;
    private StandardMaterial3D _lightMaterial;
    
    public override void _Ready()
    {
        // Create audio player
        _audioPlayer = new AudioStreamPlayer3D();
        AddChild(_audioPlayer);
        _audioPlayer.Stream = MusicStream;
        _audioPlayer.MaxDistance = 20.0f;
        _audioPlayer.UnitSize = 5.0f;
        
        // Find the light indicator mesh
        _lightMesh = GetNodeOrNull<MeshInstance3D>("Light");
        if (_lightMesh != null)
        {
            _lightMaterial = _lightMesh.GetActiveMaterial(0) as StandardMaterial3D;
        }
        
        // Start with radio off
        SetRadioState(false);
    }
    
    public void Interact(Inventory inventory)
    {
        ToggleRadio();
    }
    
    private void ToggleRadio()
    {
        _isOn = !_isOn;
        SetRadioState(_isOn);
        GD.Print($"Radio toggled: {(_isOn ? "ON" : "OFF")}");
    }
    
    private void SetRadioState(bool on)
    {
        _isOn = on;
        
        if (_isOn)
        {
            _audioPlayer.Play();
            // Make the light glow
            if (_lightMaterial != null)
            {
                _lightMaterial.EmissionEnabled = true;
                _lightMaterial.Emission = new Color(0, 1, 0, 1);
                _lightMaterial.EmissionEnergyMultiplier = 2.0f;
            }
        }
        else
        {
            _audioPlayer.Stop();
            // Turn off the light
            if (_lightMaterial != null)
            {
                _lightMaterial.EmissionEnabled = false;
            }
        }
    }
}
