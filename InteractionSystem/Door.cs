using Godot;

public partial class Door : RigidBody3D
{
	[Export]
	public string KeyName { get; set; } = "Ancient Key";
	
	[Export]
	private bool _isLocked = false; 
	public bool IsLocked => _isLocked;
	
	private bool _isOpen = false;
	private bool _isBusy = false;
	private float _closedY;
	private float _openY;

	public override void _Ready()
	{
		Freeze = true; // Ensure it's not movable
		_closedY = Rotation.Y;
		_openY = _closedY + Mathf.Pi / 2.0f;
	}

	public string Interact(Inventory inventory)
	{
		if (_isBusy) return "";

		InventoryItem selectedItem = inventory.items[inventory.selectedSlot];
		
		if (selectedItem != null && selectedItem.Name == KeyName)
		{
			// Player has key -> Toggle Lock
			_isLocked = !_isLocked;
			if (_isLocked)
			{
				if (_isOpen) CloseDoor();
				return $"Door Locked with {KeyName}.";
			}
			else
			{
				return $"Door Unlocked with {KeyName}.";
			}
		}
		else
		{
			// No key (or wrong key)
			if (_isLocked)
			{
				return $"Locked! You need the {KeyName}";
			}
			else
			{
				// Unlocked -> Toggle Open/Close
				if (_isOpen) CloseDoor();
				else OpenDoor();
				return ""; // No message for standard open/close
			}
		}
	}

	public void EnemyInteract()
	{
		if (_isBusy) return;
		if (_isLocked) 
		{
			// GD.Print("Door is locked, enemy cannot enter.");
			return; 
		}
		if (!_isOpen) 
		{
			GD.Print("Enemy opening door.");
			OpenDoor();
		}
	}

	private void OpenDoor()
	{
		_isBusy = true;
		_isOpen = true;
		Tween tween = CreateTween();
		tween.TweenProperty(this, "rotation:y", _openY, 1.0f)
			.SetTrans(Tween.TransitionType.Bounce)
			.SetEase(Tween.EaseType.Out);
		tween.TweenCallback(Callable.From(() => { _isBusy = false; }));
	}

	private void CloseDoor()
	{
		_isBusy = true;
		_isOpen = false;
		Tween tween = CreateTween();
		tween.TweenProperty(this, "rotation:y", _closedY, 1.0f)
			.SetTrans(Tween.TransitionType.Bounce)
			.SetEase(Tween.EaseType.Out);
		tween.TweenCallback(Callable.From(() => { _isBusy = false; }));
	}
}
