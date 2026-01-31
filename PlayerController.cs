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
	private ShapeCast3D _interactionCast;
	private Inventory _inventory;
	private PlayerInput _input;
	
	// Mask Visuals
	private ColorRect _gasMaskOverlay;
	private ColorRect _xRayOverlay;
	private ColorRect _stealthOverlay;
	private XRayManager _xRayManager;
	private Label _interactionLabel;
	
	// Interaction / Visuals
	private ShaderMaterial _outlineMaterial;
	private GeometryInstance3D _currentOutlineObj;

	private RigidBody3D _heldBody;

	public override void _Ready()
	{
		Input.MouseMode = Input.MouseModeEnum.Captured;
		_camera = GetNode<Camera3D>("Camera3D");
		_interactionCast = _camera.GetNode<ShapeCast3D>("ShapeCast3D");
		_inventory = GetNode<Inventory>("Inventory");
		_input = GetNode<PlayerInput>("PlayerInput");
		
		// Find Overlay Rects
		_gasMaskOverlay = GetNode<ColorRect>("../UI/GasMaskOverlay");
		_xRayOverlay = GetNode<ColorRect>("../UI/XRayOverlay");
		_stealthOverlay = GetNode<ColorRect>("../UI/StealthOverlay");
		_interactionLabel = GetNode<Label>("../UI/InteractionLabel");
		_xRayManager = GetNode<XRayManager>("../XRayManager");

		// Setup Outline Material
		_outlineMaterial = new ShaderMaterial();
		_outlineMaterial.Shader = GD.Load<Shader>("res://Shaders/outline.gdshader");

		// Connect Signals
		_input.LookInput += OnLookInput;
		_input.ToggleMouseCapture += OnToggleMouseCapture;
		_input.Interact += OnInteract;
		_input.UseItem += OnUseItem; 
		_input.DropItem += OnDropItem; // New signal
		_input.SlotSelected += OnSlotSelected;
		_input.ScrollSlot += OnScrollSlot;
		
		_inventory.ItemUsed += OnInventoryItemUsed;
		
		// Fix "Too Close" issue: Exclude player body from the cast so we don't hit ourselves
		_interactionCast.AddException(this);
	}
	
	private void OnDropItem()
	{
		// 1. Remove from Inventory
		InventoryItem removedItem = _inventory.RemoveItem(_inventory.selectedSlot);
		
		if (removedItem != null && removedItem.PickupScene != null)
		{
			// 2. Spawn Logic
			Node3D spawnNode = removedItem.PickupScene.Instantiate<Node3D>();
			GetParent().AddChild(spawnNode);
			
			// Position in front of camera
			spawnNode.GlobalPosition = _camera.GlobalPosition - _camera.GlobalTransform.Basis.Z * 2.0f;
			
			// If RigidBody, add impulse
			if (spawnNode is RigidBody3D rb)
			{
				rb.LinearVelocity = -_camera.GlobalTransform.Basis.Z * 5.0f;
			}
		}
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
		else if (effect == MaskEffect.Invisibility)
		{
			_stealthOverlay.Visible = true;
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
		_stealthOverlay.Visible = false;
	}

	public override void _PhysicsProcess(double delta)
	{
		// Interaction UI & Outline
		UpdateInteractionPrompt();
		
		// 1. Mouse Capture State Check (Visuals) - Managed via signals, but grabbed logic is here
		if (_input.LeftClickHeld && Input.MouseMode == Input.MouseModeEnum.Captured)
		{
			if (_heldBody == null)
			{
				// Try Grab (Dragging objects)
				if (_interactionCast.IsColliding())
				{
					// ShapeCast can hit multiple, we take the first logic one
					var collider = _interactionCast.GetCollider(0);
					
					if (collider is RigidBody3D rb && !(collider is Pickup)) 
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
			Vector3 grabDirection = targetPos - currentPos; 
			
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

	private void UpdateInteractionPrompt()
	{
		_interactionLabel.Text = "";
		_interactionLabel.Visible = false;
		
		// Clear previous outline
		if (_currentOutlineObj != null && IsInstanceValid(_currentOutlineObj))
		{
			_currentOutlineObj.MaterialOverlay = null;
			_currentOutlineObj = null;
		}

		if (_interactionCast.IsColliding())
		{
			// Loop through ALL collisions to find a valid interactable.
			// ShapeCast can hit the floor/walls first even if an item is "closer" or overlapping.
			// prioritizes Pickups over static geometry.
			
			Node bestInteractable = null;
			
			for (int i = 0; i < _interactionCast.GetCollisionCount(); i++)
			{
				var collider = _interactionCast.GetCollider(i) as Node;
				if (collider == null) continue;

				if (collider is Pickup || (collider is RigidBody3D && collider.IsInGroup("Interactable")))
				{
					bestInteractable = collider;
					break; // Found high priority item
				}
				else if (collider is Door)
				{
					bestInteractable = collider;
					// Don't break yet, maybe there's a smaller pickup in front of the door?
					// Actually, door is usually big. Let's stick with finding the first "logical" interactable.
					break; 
				}
				else if (collider.GetParent() is Pickup)
				{
					bestInteractable = collider;
					break;
				}
			}

			if (bestInteractable is Pickup pickup)
			{
				_interactionLabel.Text = $"Right Click to Pickup {pickup.ItemResource?.Name}";
				_interactionLabel.Visible = true;
				HighlightObject(pickup);
			}
			else if (bestInteractable is Node nodePickup && nodePickup.GetParent() is Pickup parentPickup)
			{
				_interactionLabel.Text = $"Right Click to Pickup {parentPickup.ItemResource?.Name}";
				_interactionLabel.Visible = true;
				HighlightObject(nodePickup as GeometryInstance3D);
			}
			else if (bestInteractable is Door door)
			{
				_interactionLabel.Text = "Right Click to Open/Close";
				_interactionLabel.Visible = true;
			}
		}
	}
	
	private void HighlightObject(Node obj)
	{
		if (obj == null) return;
		
		GeometryInstance3D mesh = null;
		if (obj is GeometryInstance3D g) mesh = g;
		else 
		{
			// Try finding a child mesh
			foreach(Node child in obj.GetChildren())
			{
				if (child is GeometryInstance3D childMesh)
				{
					mesh = childMesh;
					break;
				}
			}
		}
		
		if (mesh != null)
		{
			_currentOutlineObj = mesh;
			_currentOutlineObj.MaterialOverlay = _outlineMaterial;
		}
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
		if (_interactionCast.IsColliding())
		{
			// Same priority logic as prompt
			Node bestInteractable = null;
			for (int i = 0; i < _interactionCast.GetCollisionCount(); i++)
			{
				var collider = _interactionCast.GetCollider(i) as Node;
				if (collider == null) continue;

				if (collider is Pickup || (collider is RigidBody3D && collider.IsInGroup("Interactable")))
				{
					bestInteractable = collider;
					break; 
				}
				else if (collider is Door)
				{
					bestInteractable = collider;
					break;
				}
				else if (collider.GetParent() is Pickup)
				{
					bestInteractable = collider;
					break;
				}
			}

			if (bestInteractable is Pickup pickup)
			{
				pickup.Interact(_inventory);
			}
			else if (bestInteractable is Node nodePickup && nodePickup.GetParent() is Pickup parentPickup)
			{
				parentPickup.Interact(_inventory);
			}
			else if (bestInteractable is Door door)
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
