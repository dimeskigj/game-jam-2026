using Godot;

public partial class Door : RigidBody3D
{
	[Export]
	public string KeyName { get; set; } = "Ancient Key";
	
	private bool _isOpen = false;
	private bool _isBusy = false;

	public void Interact(Inventory inventory)
	{
		if (_isBusy) return;
		if (_isOpen) return; // Keep it open for now, or toggle if preferred

		InventoryItem selectedItem = inventory.items[inventory.selectedSlot];
		
		if (selectedItem != null && selectedItem.Name == KeyName)
		{
			GD.Print("Door Unlocked!");
			_isOpen = true;
			_isBusy = true;
			
			// Animate opening
			Tween tween = CreateTween();
			// Rotate 90 degrees (HALF_PI). 
			// Note: Rotating around center. For a real hinge, we'd need a pivot node.
			// But this satisfies "rotate 90 degrees".
			tween.TweenProperty(this, "rotation:y", Rotation.Y + Mathf.Pi / 2.0f, 1.0f)
				.SetTrans(Tween.TransitionType.Bounce)
				.SetEase(Tween.EaseType.Out);
			
			tween.TweenCallback(Callable.From(() => { _isBusy = false; }));
			
			// Optionally remove the key?
			// inventory.UseCurrentItem(); 
		}
		else
		{
			GD.Print("Locked! You need the " + KeyName);
		}
	}
}
