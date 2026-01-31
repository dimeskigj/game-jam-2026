using Godot;

public partial class MainMenu : Control
{
	[Export]
	public string StartScenePath = "res://node_3d.tscn";

	private Button _startButton;

	public override void _Ready()
	{
		_startButton = GetNode<Button>("CenterContainer/VBoxContainer/StartButton");
		_startButton.Pressed += OnStartButtonPressed;
		
		// Make sure mouse is visible for the menu
		Input.MouseMode = Input.MouseModeEnum.Visible;
	}

	private void OnStartButtonPressed()
	{
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
