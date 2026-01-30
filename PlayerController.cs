using Godot;

public partial class PlayerController : CharacterBody3D
{
	[Export]
	public float Speed = 5.0f;
	[Export]
	public float JumpVelocity = 6.0f;
	[Export]
	public float MouseSensitivity = 0.002f;
	[Export]
	public float Acceleration = 20.0f;
	[Export]
	public float Friction = 10.0f;
	[Export]
	public float GrabPower = 10.0f;
	[Export]
	public float HoldDistance = 3.0f;

	public float gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();

	public MaskEffect CurrentMaskEffect { get; private set; } = MaskEffect.None;

	private Camera3D _camera;
	private RayCast3D _interactionRay;
	private Inventory _inventory;
	private PlayerInput _input;
	
	// Mask Visuals
	private ColorRect _gasMaskOverlay;
	private ColorRect _xRayOverlay;
	private XRayManager _xRayManager;

	private RigidBody3D _heldBody;

	public override void _Ready()
	{
		Input.MouseMode = Input.MouseModeEnum.Captured;
		_camera = GetNode<Camera3D>("Camera3D");
		_interactionRay = _camera.GetNode<RayCast3D>("RayCast3D");
		_inventory = GetNode<Inventory>("Inventory");
		_input = GetNode<PlayerInput>("PlayerInput");
		
		// Find Overlay Rects (Assuming they will be added to UI)
		_gasMaskOverlay = GetNode<ColorRect>("../UI/GasMaskOverlay");
		_xRayOverlay = GetNode<ColorRect>("../UI/XRayOverlay");
		_xRayManager = GetNode<XRayManager>("../XRayManager");

		// Connect Signals
		_input.LookInput += OnLookInput;
		_input.ToggleMouseCapture += OnToggleMouseCapture;
		_input.Interact += OnInteract;
		_input.UseItem += OnUseItem; // Consuming/Generic usage (E key)
		_input.SlotSelected += OnSlotSelected;
		_input.ScrollSlot += OnScrollSlot;
		
		_inventory.ItemUsed += OnInventoryItemUsed;
	}
	
	private void OnInventoryItemUsed(InventoryItem item)
	{
		if (item.Type == ItemType.Mask)
		{
			// Toggle or Swap?
			if (CurrentMaskEffect == item.Effect)
			{
				UnequipMask();
			}
			else
			{
				EquipMask(item.Effect);
			}
		}
	}

	public void EquipMask(MaskEffect effect)
	{
		UnequipMask(); // Clear current first
		CurrentMaskEffect = effect;
		GD.Print($"Equipped Mask: {effect}");
		
		if (effect == MaskEffect.Gas)
		{
			_gasMaskOverlay.Visible = true;
		}
		else if (effect == MaskEffect.XRay)
		{
			_xRayOverlay.Visible = true;
			_xRayManager.ToggleXRay(true);
		}
	}

	public void UnequipMask()
	{
		if (CurrentMaskEffect == MaskEffect.XRay)
		{
			_xRayManager.ToggleXRay(false);
		}
		
		CurrentMaskEffect = MaskEffect.None;
		_gasMaskOverlay.Visible = false;
		_xRayOverlay.Visible = false;
	}

	public override void _PhysicsProcess(double delta)
	{
		// 1. Mouse Capture State Check (Visuals) - Managed via signals, but grabbed logic is here
		if (_input.LeftClickHeld && Input.MouseMode == Input.MouseModeEnum.Captured)
		{
			if (_heldBody == null)
			{
				// Try Grab
				if (_interactionRay.IsColliding())
				{
					var collider = _interactionRay.GetCollider();
					if (collider is RigidBody3D rb)
					{
						_heldBody = rb;
					}
				}
			}
		}
		else
		{
			_heldBody = null;
		}

		// 2. Physics Grabbing Apply Force
		if (_heldBody != null)
		{
			Vector3 targetPos = _camera.GlobalTransform.Origin - _camera.GlobalTransform.Basis.Z * HoldDistance;
			Vector3 currentPos = _heldBody.GlobalTransform.Origin;
			Vector3 grabDirection = targetPos - currentPos; // Renamed to avoid CS0136
			
			_heldBody.LinearVelocity = grabDirection * GrabPower;
		}

		// 3. Movement
		Vector3 velocity = Velocity;

		if (!IsOnFloor())
			velocity.Y -= gravity * (float)delta;
		
		if (_input.JumpPressed && IsOnFloor())
			velocity.Y = JumpVelocity;

		Vector2 inputDir = _input.MoveInput;
		Vector3 moveDirection = (Transform.Basis * new Vector3(inputDir.X, 0, inputDir.Y)).Normalized();

		if (moveDirection != Vector3.Zero)
		{
			velocity.X = Mathf.MoveToward(velocity.X, moveDirection.X * Speed, Acceleration * (float)delta);
			velocity.Z = Mathf.MoveToward(velocity.Z, moveDirection.Z * Speed, Acceleration * (float)delta);
		}
		else
		{
			velocity.X = Mathf.MoveToward(velocity.X, 0, Friction * (float)delta);
			velocity.Z = Mathf.MoveToward(velocity.Z, 0, Friction * (float)delta);
		}

		Velocity = velocity;
		MoveAndSlide();
	}

	// --- Signal Handlers ---

	private void OnLookInput(Vector2 relative)
	{
		if (Input.MouseMode != Input.MouseModeEnum.Captured) return;
		
		RotateY(-relative.X * MouseSensitivity);
		if (_camera != null)
		{
			Vector3 camRot = _camera.Rotation;
			camRot.X -= relative.Y * MouseSensitivity;
			camRot.X = Mathf.Clamp(camRot.X, Mathf.DegToRad(-89), Mathf.DegToRad(89));
			_camera.Rotation = camRot;
		}
	}

	private void OnToggleMouseCapture(bool captured)
	{
		Input.MouseMode = captured ? Input.MouseModeEnum.Captured : Input.MouseModeEnum.Visible;
	}

	private void OnInteract()
	{
		if (_interactionRay.IsColliding())
		{
			var collider = _interactionRay.GetCollider();
            // Using dynamic dispatch or checking types manually as interacting logic
            // Since we moved Door/Pickup to InteractionSystem, we should check interactables.
            // For now, casting to known types or interface if created.
			if (collider is Pickup pickup)
			{
				pickup.Interact(_inventory);
			}
			else if (collider is Node nodePickup && nodePickup.GetParent() is Pickup parentPickup)
			{
				parentPickup.Interact(_inventory);
			}
			else if (collider is Door door)
			{
				door.Interact(_inventory);
			}
		}
	}

	private void OnUseItem()
	{
		_inventory.UseCurrentItem();
	}

	private void OnSlotSelected(int slot)
	{
		_inventory.SelectSlot(slot);
	}

	private void OnScrollSlot(int direction)
	{
		int newSlot = _inventory.selectedSlot + direction;
		if (newSlot < 0) newSlot = Inventory.MaxSlots - 1;
		if (newSlot >= Inventory.MaxSlots) newSlot = 0;
		_inventory.SelectSlot(newSlot);
	}
}
