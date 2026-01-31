using Godot;
using System.Collections.Generic;

public partial class PlayerController : CharacterBody3D
{
	// ... (exports omitted for brevity in diff, but preserved in file)
	[Export]
	public float Speed = 5.0f;
	[Export]
	public float SprintSpeed = 10.0f;
	[Export]
	public float BaseFov = 75.0f;
	[Export]
	public float SprintFov = 85.0f;
	[Export]
	public float FovChangeSpeed = 5.0f;

	[ExportGroup("Head Bob")]
	[Export] public float BobFreq = 2.0f;
	[Export] public float BobAmp = 0.08f;
	[Export] public float BobSprintMultiplier = 2.0f;
	
	// State for alerts
	private HashSet<Node> _alertSources = new HashSet<Node>();
	
	// ...

	public void SetAlert(bool active, Node source)
	{
		int preCount = _alertSources.Count;
		
		if (active)
		{
			_alertSources.Add(source);
		}
		else
		{
			_alertSources.Remove(source);
		}

		if (_alertSources.Count != preCount)
		{
			// GD.Print($"Alert Sources Changed: {_alertSources.Count} (Source: {source.Name} set {active})");
		}

		bool isDetected = _alertSources.Count > 0;
		
		if (_detectionLabel != null) _detectionLabel.Visible = isDetected;
		if (_detectionOverlay != null) _detectionOverlay.Visible = isDetected;
	}
	// ...

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

	[Export] public float MaxSanity = 100.0f;
	[Export] public float SanityDrainRate = 5.0f; 
	[Export] public float SanityRegenRate = 10.0f; 
	
	public float CurrentSanity { get; private set; } = 100.0f;
	public bool IsDead { get; private set; } = false;

	private Camera3D _camera;
	private ShapeCast3D _interactionCast;
	private Inventory _inventory;
	private PlayerInput _input;
	
	// Mask Visuals
	private ColorRect _gasMaskOverlay;
	private ColorRect _xRayOverlay;
	private ColorRect _stealthOverlay;
	private ColorRect _sanityOverlay;
	private Label _detectionLabel;     
	private ColorRect _detectionOverlay; 
	
	private XRayManager _xRayManager;
	private Label _interactionLabel;
	private ProgressBar _sanityBar;
	private Label _gameOverLabel;
	
	// Interaction / Visuals
	private ShaderMaterial _outlineMaterial;
	private GeometryInstance3D _currentOutlineObj;

	private RigidBody3D _heldBody;
	private Vector3 _defaultCamPos;
	private float _bobTime = 0.0f;

	public override void _Ready()
	{
		CurrentSanity = MaxSanity;
		Input.MouseMode = Input.MouseModeEnum.Captured;
		_camera = GetNode<Camera3D>("Camera3D");
		_defaultCamPos = _camera.Position;
		_interactionCast = _camera.GetNode<ShapeCast3D>("ShapeCast3D");
		_inventory = GetNode<Inventory>("Inventory");
		_input = GetNode<PlayerInput>("PlayerInput");
		
		// Find Overlay Rects
		_gasMaskOverlay = GetNode<ColorRect>("../UI/GasMaskOverlay");
		_xRayOverlay = GetNode<ColorRect>("../UI/XRayOverlay");
		_stealthOverlay = GetNode<ColorRect>("../UI/StealthOverlay");
		_sanityOverlay = GetNode<ColorRect>("../UI/SanityOverlay");
		_detectionOverlay = GetNodeOrNull<ColorRect>("../UI/DetectionOverlay"); 
		
		_interactionLabel = GetNode<Label>("../UI/InteractionLabel");
		_sanityBar = GetNode<ProgressBar>("../UI/SanityBar");
		_gameOverLabel = GetNode<Label>("../UI/GameOverLabel");
		_detectionLabel = GetNodeOrNull<Label>("../UI/DetectionLabel"); 
		
		_xRayManager = GetNode<XRayManager>("../XRayManager");

		// Setup Outline Material
		_outlineMaterial = new ShaderMaterial();
		_outlineMaterial.Shader = GD.Load<Shader>("res://Shaders/outline.gdshader");

		// Connect Signals
		_input.LookInput += OnLookInput;
		_input.ToggleMouseCapture += OnToggleMouseCapture;
		_input.Interact += OnInteract;
		_input.UseItem += OnUseItem; 
		_input.DropItem += OnDropItem; 
		_input.SlotSelected += OnSlotSelected;
		_input.ScrollSlot += OnScrollSlot;
		
		_inventory.ItemUsed += OnInventoryItemUsed;
		
		// Fix "Too Close" issue
		_interactionCast.AddException(this);
	}
	
	private void OnDropItem()
	{
		InventoryItem removedItem = _inventory.RemoveItem(_inventory.selectedSlot);
		
		if (removedItem != null && removedItem.PickupScene != null)
		{
			Node3D spawnNode = removedItem.PickupScene.Instantiate<Node3D>();
			GetParent().AddChild(spawnNode);
			spawnNode.GlobalPosition = _camera.GlobalPosition - _camera.GlobalTransform.Basis.Z * 2.0f;
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
			if (CurrentMaskEffect == item.Effect)
				UnequipMask();
			else
				EquipMask(item.Effect);
		}
	}

	public void EquipMask(MaskEffect effect)
	{
		UnequipMask();
		CurrentMaskEffect = effect;
		GD.Print($"Equipped Mask: {effect}");
		
		if (effect == MaskEffect.Gas) _gasMaskOverlay.Visible = true;
		else if (effect == MaskEffect.XRay)
		{
			_xRayOverlay.Visible = true;
			_xRayManager.ToggleXRay(true);
		}
		else if (effect == MaskEffect.Invisibility) _stealthOverlay.Visible = true;
	}

	public void UnequipMask()
	{
		if (CurrentMaskEffect == MaskEffect.XRay) _xRayManager.ToggleXRay(false);
		
		CurrentMaskEffect = MaskEffect.None;
		_gasMaskOverlay.Visible = false;
		_xRayOverlay.Visible = false;
		_stealthOverlay.Visible = false;
	}

	// SetAlert replaced SetDetected

	public override void _PhysicsProcess(double delta)
	{
		UpdateSanity(delta);
		if (IsDead) return;

		UpdateInteractionPrompt();
		
		if (_input.LeftClickHeld && Input.MouseMode == Input.MouseModeEnum.Captured)
		{
			if (_heldBody == null)
			{
				if (_interactionCast.IsColliding())
				{
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

		if (_heldBody != null)
		{
			Vector3 targetPos = _camera.GlobalTransform.Origin - _camera.GlobalTransform.Basis.Z * HoldDistance;
			Vector3 currentPos = _heldBody.GlobalTransform.Origin;
			Vector3 grabDirection = targetPos - currentPos; 
			_heldBody.LinearVelocity = grabDirection * GrabPower;
		}

		Vector3 velocity = Velocity;
		if (!IsOnFloor()) velocity.Y -= gravity * (float)delta;
		if (_input.JumpPressed && IsOnFloor()) velocity.Y = JumpVelocity;

		Vector2 inputDir = _input.MoveInput;
		Vector3 moveDirection = (Transform.Basis * new Vector3(inputDir.X, 0, inputDir.Y)).Normalized();

		// Sprinting logic
		bool isSprinting = Input.IsKeyPressed(Key.Shift) && inputDir != Vector2.Zero;
		float targetSpeed = isSprinting ? SprintSpeed : Speed;

		// Camera FOV effect
		float targetFov = isSprinting ? SprintFov : BaseFov;
		if (_camera != null)
		{
			_camera.Fov = (float)Mathf.Lerp(_camera.Fov, targetFov, FovChangeSpeed * (float)delta);
		}

		if (moveDirection != Vector3.Zero)
		{
			velocity.X = Mathf.MoveToward(velocity.X, moveDirection.X * targetSpeed, Acceleration * (float)delta);
			velocity.Z = Mathf.MoveToward(velocity.Z, moveDirection.Z * targetSpeed, Acceleration * (float)delta);
		}
		else
		{
			velocity.X = Mathf.MoveToward(velocity.X, 0, Friction * (float)delta);
			velocity.Z = Mathf.MoveToward(velocity.Z, 0, Friction * (float)delta);
		}

		Velocity = velocity;
		MoveAndSlide();
		HandleHeadBob(delta, isSprinting);
	}

	private void UpdateInteractionPrompt()
	{
		_interactionLabel.Text = "";
		_interactionLabel.Visible = false;
		
		if (_currentOutlineObj != null && IsInstanceValid(_currentOutlineObj))
		{
			_currentOutlineObj.MaterialOverlay = null;
			_currentOutlineObj = null;
		}

		if (_interactionCast.IsColliding())
		{
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
				else if (collider is Radio)
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
			else if (bestInteractable is Radio radio)
			{
				_interactionLabel.Text = "Right Click to Toggle Radio";
				_interactionLabel.Visible = true;
				HighlightObject(radio);
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
				else if (collider is Radio)
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

			if (bestInteractable is Pickup pickup) pickup.Interact(_inventory);
			else if (bestInteractable is Node nodePickup && nodePickup.GetParent() is Pickup parentPickup) parentPickup.Interact(_inventory);
			else if (bestInteractable is Door door) door.Interact(_inventory);
			else if (bestInteractable is Radio radio) radio.Interact(_inventory);
		}
	}

	private void OnUseItem() => _inventory.UseCurrentItem();
	private void OnSlotSelected(int slot) => _inventory.SelectSlot(slot);
	private void OnScrollSlot(int direction)
	{
		int newSlot = _inventory.selectedSlot + direction;
		if (newSlot < 0) newSlot = Inventory.MaxSlots - 1;
		if (newSlot >= Inventory.MaxSlots) newSlot = 0;
		_inventory.SelectSlot(newSlot);
	}
	
	private void UpdateSanity(double delta)
	{
		if (IsDead) return;

		if (CurrentMaskEffect != MaskEffect.None) CurrentSanity -= SanityDrainRate * (float)delta;
		else CurrentSanity += SanityRegenRate * (float)delta;
		
		CurrentSanity = Mathf.Clamp(CurrentSanity, 0, MaxSanity);
		
		if (_sanityBar != null) _sanityBar.Value = CurrentSanity;
		
		if (_sanityOverlay != null)
		{
			float intensity = 1.0f - (CurrentSanity / MaxSanity);
			var material = _sanityOverlay.Material as ShaderMaterial;
			if (material != null) material.SetShaderParameter("intensity", intensity);
		}

		if (CurrentSanity <= 0)
		{
			IsDead = true;
			_gameOverLabel.Visible = true;
			Input.MouseMode = Input.MouseModeEnum.Visible;
			GD.Print("GAME OVER: Sanity Depleted.");
		}
	}

	private void HandleHeadBob(double delta, bool isSprinting)
	{
		float horizontalVelocity = new Vector3(Velocity.X, 0, Velocity.Z).Length();
		bool onFloor = IsOnFloor();
		
		if (!onFloor || horizontalVelocity < 0.1f)
		{
			_bobTime = 0.0f;
			_camera.Position = _camera.Position.Lerp(_defaultCamPos, (float)delta * 10.0f);
			return;
		}

		float multiplier = isSprinting ? BobSprintMultiplier : 1.0f;
		_bobTime += (float)delta * horizontalVelocity * multiplier;
		
		Vector3 targetPos = _defaultCamPos;
		targetPos.Y += Mathf.Sin(_bobTime * BobFreq) * BobAmp * multiplier;
		targetPos.X += Mathf.Cos(_bobTime * BobFreq * 0.5f) * BobAmp * 0.5f * multiplier;
		
		_camera.Position = _camera.Position.Lerp(targetPos, (float)delta * 10.0f);
	}
}
