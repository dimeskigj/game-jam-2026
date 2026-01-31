using Godot;

public partial class MainMenu : Control
{
	[Export]
	public string StartScenePath = "res://node_3d.tscn";
	[Export]
	public string DefaultServerIP = "127.0.0.1";

	private Button _startButton;
	private LineEdit _nameEdit;
	private ColorPickerButton _colorBtn;
	private LineEdit _ipEdit;
	private NetworkManager _net;

	public override void _Ready()
	{
		_net = GetNode<NetworkManager>("/root/NetworkManager");
		_startButton = GetNode<Button>("CenterContainer/VBoxContainer/StartButton");
		_nameEdit = GetNode<LineEdit>("CenterContainer/VBoxContainer/InputsGroup/NameEdit");
		_colorBtn = GetNode<ColorPickerButton>("CenterContainer/VBoxContainer/InputsGroup/HBoxContainer/ColorBtn");
		_ipEdit = GetNode<LineEdit>("CenterContainer/VBoxContainer/InputsGroup/IPEdit");

		_startButton.Pressed += OnStartButtonPressed;
		_ipEdit.PlaceholderText = DefaultServerIP;
		
		Input.MouseMode = Input.MouseModeEnum.Visible;
	}

	private void OnStartButtonPressed()
	{
		// Set player data in NetworkManager
		_net.PlayerName = string.IsNullOrWhiteSpace(_nameEdit.Text) ? "Player" : _nameEdit.Text;
		_net.PlayerColor = _colorBtn.Color;

		// IMPORTANT: Load the map FIRST, then connect.
		string address = string.IsNullOrWhiteSpace(_ipEdit.Text) ? DefaultServerIP : _ipEdit.Text;
		
		_net.TargetIP = address;
		
		// Change scene to game map. GameRoot.cs _Ready will handle the connection.
		GetTree().ChangeSceneToFile(StartScenePath);
	}

	public override void _Input(InputEvent @event)
	{
		if (@event.IsActionPressed("ui_cancel") || ( @event is InputEventKey k && k.Pressed && k.Keycode == Key.Escape))
		{
			GetTree().Quit();
		}
	}
}
