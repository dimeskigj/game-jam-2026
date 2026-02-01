using Godot;
using System;

public partial class GlobalSceneManager : Node
{
	public static GlobalSceneManager Instance { get; private set; }

	private Control _loadingScreen;
	private ProgressBar _progressBar;
	private ResourceLoader.ThreadLoadStatus _loadStatus;
	private Godot.Collections.Array _progress;
	private string _targetScenePath;
	private bool _isLoading = false;
	
	// Persistent State
	public InventoryItem[] SavedInventory = new InventoryItem[8];
	public bool HasFlashlight = false;
	public float FlashlightBattery = 0.0f;
	
	// Track collected world items by their unique node path
	public System.Collections.Generic.HashSet<string> CollectedItemPaths = new System.Collections.Generic.HashSet<string>();

	public override void _Ready()
	{
		Instance = this;
		ProcessMode = ProcessModeEnum.Always; // Run even when game is paused/loading
	}

	public void LoadScene(string scenePath)
	{
		if (_isLoading) return;
		
		_targetScenePath = scenePath;
		_isLoading = true;
		
		// Create/Show Loading Screen
		if (_loadingScreen == null)
		{
			// Instantiate the loading screen scene
			var scene = GD.Load<PackedScene>("res://scenes/LoadingScreen.tscn");
			if (scene != null)
			{
				_loadingScreen = scene.Instantiate<Control>();
				GetTree().Root.AddChild(_loadingScreen);
				_progressBar = _loadingScreen.GetNodeOrNull<ProgressBar>("ProgressBar");
			}
		}
		else
		{
			_loadingScreen.Visible = true;
			_loadingScreen.Modulate = new Color(1, 1, 1, 1);
		}
		
		if (_progressBar != null) _progressBar.Value = 0;

		// Start background loading
		Error err = ResourceLoader.LoadThreadedRequest(_targetScenePath);
		if (err != Error.Ok)
		{
			GD.PrintErr($"Failed to start loading scene: {_targetScenePath}");
			_isLoading = false;
			_loadingScreen.Visible = false;
			return;
		}
		
		_progress = new Godot.Collections.Array();
	}

	public override void _Process(double delta)
	{
		if (!_isLoading) return;

		_loadStatus = ResourceLoader.LoadThreadedGetStatus(_targetScenePath, _progress);
		
		if (_progressBar != null && _progress.Count > 0)
		{
			_progressBar.Value = (double)_progress[0] * 100.0;
		}

		if (_loadStatus == ResourceLoader.ThreadLoadStatus.Loaded)
		{
			// Loading complete
			var packedScene = ResourceLoader.LoadThreadedGet(_targetScenePath) as PackedScene;
			if (packedScene != null)
			{
				GetTree().ChangeSceneToPacked(packedScene);
			}
			
			// Hide loading screen effectively
			// We can fade it out or just hide it. Let's wait a frame to ensure scene is ready.
			CallDeferred(nameof(HideLoadingScreen));
			_isLoading = false;
		}
		else if (_loadStatus == ResourceLoader.ThreadLoadStatus.Failed || _loadStatus == ResourceLoader.ThreadLoadStatus.InvalidResource)
		{
			GD.PrintErr("Loading failed!");
			_isLoading = false;
			if (_loadingScreen != null) _loadingScreen.Visible = false;
		}
	}

	private void HideLoadingScreen()
	{
		if (_loadingScreen != null)
		{
			Tween tween = CreateTween();
			tween.TweenProperty(_loadingScreen, "modulate:a", 0.0f, 0.5f);
			tween.TweenCallback(Callable.From(() => _loadingScreen.Visible = false));
		}
	}
}
