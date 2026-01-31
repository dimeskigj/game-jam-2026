using Godot;

public partial class Drawer : RigidBody3D
{
	[Export]
	public string KeyName { get; set; } = "Small Key";
	
	[Export]
	private bool _isLocked = false; 
	public bool IsLocked => _isLocked;
	
	[Export]
	public Vector3 SlideAxis { get; set; } = Vector3.Back; // Default pull direction
	
	[Export]
	public float SlideDistance { get; set; } = 0.5f;

	private bool _isOpen = false;
	private bool _isBusy = false;
	private Vector3 _closedPos;
	private Vector3 _openPos;

	public override void _Ready()
	{
		Freeze = true; 
		_closedPos = Position;
		// Calculate open position relative to current rotation
		// Transform.Basis * Axis gives axis in parent space
		_openPos = _closedPos + (Transform.Basis * (SlideAxis.Normalized() * SlideDistance));
	}

	public string Interact(Inventory inventory)
	{
		if (_isBusy) return "";

		InventoryItem selectedItem = inventory.items[inventory.selectedSlot];
		
		if (selectedItem != null && selectedItem.Name == KeyName)
		{
			_isLocked = !_isLocked;
			if (_isLocked)
			{
				if (_isOpen) CloseDrawer();
				return $"Drawer Locked with {KeyName}.";
			}
			else
			{
				return $"Drawer Unlocked with {KeyName}.";
			}
		}
		else
		{
			if (_isLocked)
			{
				return $"Locked! You need the {KeyName}";
			}
			else
			{
				if (_isOpen) CloseDrawer();
				else OpenDrawer();
				return "";
			}
		}
	}

	private void OpenDrawer()
	{
		_isBusy = true;
		_isOpen = true;
		Tween tween = CreateTween();
		tween.TweenProperty(this, "position", _openPos, 0.5f)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		tween.TweenCallback(Callable.From(() => { _isBusy = false; }));
	}

	private void CloseDrawer()
	{
		_isBusy = true;
		_isOpen = false;
		Tween tween = CreateTween();
		tween.TweenProperty(this, "position", _closedPos, 0.5f)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		tween.TweenCallback(Callable.From(() => { _isBusy = false; }));
	}
}
