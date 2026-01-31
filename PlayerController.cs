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

	[Export]
	public float DuckSpeed = 2.5f;
	[Export]
	public float CrouchOffset = 0.5f;
	[Export]
	public float CrouchTransitionSpeed = 10.0f;

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
	public float PushForce = 1.0f;
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
	private ColorRect _insectOverlay;
	private ColorRect _sanityOverlay;
	private Label _detectionLabel;     
	private ColorRect _detectionOverlay; 
	
	private XRayManager _xRayManager;
	private Label _interactionLabel;
	private Label _statusLabel;
	private ProgressBar _sanityBar;
	private Label _gameOverLabel;
	private Control _noteOverlay;
	private Label _noteLabel;
	private bool _isReadingNote = false;
	
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
		_gasMaskOverlay = GetNodeOrNull<ColorRect>("../UI/GasMaskOverlay");
		_xRayOverlay = GetNodeOrNull<ColorRect>("../UI/XRayOverlay");
		_stealthOverlay = GetNodeOrNull<ColorRect>("../UI/StealthOverlay");
		_sanityOverlay = GetNodeOrNull<ColorRect>("../UI/SanityOverlay");
		_detectionOverlay = GetNodeOrNull<ColorRect>("../UI/DetectionOverlay"); 
		
		_interactionLabel = GetNodeOrNull<Label>("../UI/InteractionLabel");
		_statusLabel = GetNodeOrNull<Label>("../UI/StatusLabel");
		_sanityBar = GetNodeOrNull<ProgressBar>("../UI/SanityBar");
		_gameOverLabel = GetNodeOrNull<Label>("../UI/GameOverLabel");
		
		_noteOverlay = GetNodeOrNull<Control>("../UI/NoteOverlay");
		_noteLabel = GetNodeOrNull<Label>("../UI/NoteOverlay/NotePanel/NoteText");
		if (_noteOverlay != null) _noteOverlay.Visible = false;

		_detectionLabel = GetNodeOrNull<Label>("../UI/DetectionLabel"); 
		
		_xRayManager = GetNodeOrNull<XRayManager>("../XRayManager");

		// Setup Outline Material
		_outlineMaterial = new ShaderMaterial();
		_outlineMaterial.Shader = GD.Load<Shader>("res://Shaders/outline.gdshader");

		// Setup Detection Overlay Shader
		if (_detectionOverlay != null)
		{
			var alertMat = new ShaderMaterial();
			alertMat.Shader = GD.Load<Shader>("res://Shaders/alert_overlay.gdshader");
			_detectionOverlay.Material = alertMat;
			_detectionOverlay.Visible = false; // Start hidden
		}

		// Ensure interaction cast sees Layer 1 (Static), Layer 2 (Pickups), Layer 3 (Doors)
		_interactionCast.CollisionMask = 1 | 2 | 4; 

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
		
		if (effect == MaskEffect.Gas && _gasMaskOverlay != null) _gasMaskOverlay.Visible = true;
		else if (effect == MaskEffect.XRay)
		{
			if (_xRayOverlay != null) _xRayOverlay.Visible = true;
			if (_xRayManager != null) _xRayManager.ToggleXRay(true);
		}
		else if (effect == MaskEffect.Invisibility && _stealthOverlay != null) _stealthOverlay.Visible = true;
	}

	public void UnequipMask()
	{
		if (CurrentMaskEffect == MaskEffect.XRay && _xRayManager != null) _xRayManager.ToggleXRay(false);
		
		CurrentMaskEffect = MaskEffect.None;
		if (_gasMaskOverlay != null) _gasMaskOverlay.Visible = false;
		if (_xRayOverlay != null) _xRayOverlay.Visible = false;
		if (_stealthOverlay != null) _stealthOverlay.Visible = false;
	}

	// SetAlert replaced SetDetected
	private float _alertIntensity = 0.0f;
	[Export] public float AlertFadeSpeed = 5.0f;
	
	private void UpdateAlertOverlay(double delta)
	{
		bool isDetected = _alertSources.Count > 0;
		float target = isDetected ? 1.0f : 0.0f;
		
		// Smoothly interpolate intensity
		_alertIntensity = Mathf.MoveToward(_alertIntensity, target, AlertFadeSpeed * (float)delta);
		
		if (_detectionOverlay != null)
		{
			// Only show if there is some intensity
			_detectionOverlay.Visible = _alertIntensity > 0.01f;
			
			if (_detectionOverlay.Visible)
			{
				if (_detectionOverlay.Material is ShaderMaterial mat)
				{
					mat.SetShaderParameter("intensity", _alertIntensity);
				}
				else
				{
					// Fallback if shader assignment failed (though we do it in Ready)
					_detectionOverlay.Color = new Color(1, 0, 0, _alertIntensity * 0.3f);
				}
			}
		}
		
		if (_detectionLabel != null)
		{
			_detectionLabel.Visible = isDetected; 
		}
	}

	// Notification State
	private double _notificationTime = 0.0;
	private string _notificationText = "";

	public override void _PhysicsProcess(double delta)
	{
		if (_notificationTime > 0) _notificationTime -= delta;

		UpdateAlertOverlay(delta);
		UpdateSanity(delta);
		if (IsDead) return;
		if (_isReadingNote) return; // Stop movement while reading

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

		// Sprinting and Ducking logic
		bool isDucking = _input.DuckPressed;
		bool isSprinting = Input.IsKeyPressed(Key.Shift) && inputDir != Vector2.Zero && !isDucking;
		
		float targetSpeed = Speed;
		if (isSprinting) targetSpeed = SprintSpeed;
		else if (isDucking) targetSpeed = DuckSpeed;

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
		
		ApplyKickForce();

		HandleHeadBob(delta, isSprinting, isDucking);
	}

	private void ApplyKickForce()
	{
		var spaceState = GetWorld3D().DirectSpaceState;
		var query = new PhysicsShapeQueryParameters3D();
		var shape = new SphereShape3D();
		shape.Radius = 1.0f; 
		query.ShapeRid = shape.GetRid();
		query.Transform = GlobalTransform;
		query.CollisionMask = 2; // Layer 2: Pickups

		var results = spaceState.IntersectShape(query);
		foreach (var result in results)
		{
			var collider = (Variant)result["collider"];
			if (collider.As<Node>() is RigidBody3D rb)
			{
				Vector3 pushDirection = (rb.GlobalPosition - GlobalPosition).Normalized();
				pushDirection.Y = 0;
				rb.ApplyImpulse(pushDirection * PushForce * 0.2f * rb.Mass);
			}
		}
	}

	private void UpdateInteractionPrompt()
	{
		if (_interactionLabel != null)
		{
			_interactionLabel.Text = "";
			_interactionLabel.Visible = false;
		}

		if (_statusLabel != null)
		{
			if (_notificationTime > 0)
			{
				_statusLabel.Text = _notificationText;
				_statusLabel.Visible = true;
			}
			else
			{
				_statusLabel.Visible = false;
				_statusLabel.Text = "";
			}
		}
		
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
				else if (collider is Drawer)
				{
					bestInteractable = collider;
					break;
				}
			}

			if (bestInteractable is Pickup pickup)
			{
				if (_interactionLabel != null)
				{
					_interactionLabel.Text = $"Right Click to Pickup {pickup.ItemResource?.Name}";
					_interactionLabel.Visible = true;
				}
				HighlightObject(pickup);
			}
			else if (bestInteractable is Node nodePickup && nodePickup.GetParent() is Pickup parentPickup)
			{
				if (_interactionLabel != null)
				{
					_interactionLabel.Text = $"Right Click to Pickup {parentPickup.ItemResource?.Name}";
					_interactionLabel.Visible = true;
				}
				HighlightObject(nodePickup as GeometryInstance3D);
			}
			else if (bestInteractable is Door door)
			{
				InventoryItem heldItem = _inventory.items[_inventory.selectedSlot];
				bool hasKeyInHand = heldItem != null && heldItem.Name == door.KeyName;

				if (door.IsLocked)
				{
					if (hasKeyInHand)
					{
						if (_interactionLabel != null) _interactionLabel.Text = "Right Click to Unlock";
					}
					else
					{
						if (_interactionLabel != null) _interactionLabel.Text = "Right Click (Locked)";
					}
				}
				else
				{
					if (hasKeyInHand)
					{
						if (_interactionLabel != null) _interactionLabel.Text = "Right Click to Lock";
					}
					else
					{
						if (_interactionLabel != null) _interactionLabel.Text = "Right Click to Open/Close";
					}
				}
				if (_interactionLabel != null) _interactionLabel.Visible = true;
			}
			else if (bestInteractable is Radio radio)
			{
				if (_interactionLabel != null)
				{
					_interactionLabel.Text = "Right Click to Toggle Radio";
					_interactionLabel.Visible = true;
				}
				HighlightObject(radio);
			}
			else if (bestInteractable is Drawer drawer)
			{
				InventoryItem heldItem = _inventory.items[_inventory.selectedSlot];
				bool hasKeyInHand = heldItem != null && heldItem.Name == drawer.KeyName;

				if (drawer.IsLocked)
				{
					if (hasKeyInHand) 
					{
						if (_interactionLabel != null) _interactionLabel.Text = "Right Click to Unlock Drawer";
					}
					else 
					{
						if (_interactionLabel != null) _interactionLabel.Text = "Right Click (Locked)";
					}
				}
				else
				{
					if (hasKeyInHand) 
					{
						if (_interactionLabel != null) _interactionLabel.Text = "Right Click to Lock Drawer";
					}
					else 
					{
						if (_interactionLabel != null) _interactionLabel.Text = "Right Click to Open/Close Drawer";
					}
				}
				if (_interactionLabel != null) _interactionLabel.Visible = true;
				HighlightObject(drawer);
			}
			else if (bestInteractable is Note note)
			{
				if (_interactionLabel != null)
				{
					_interactionLabel.Text = "Right Click to Read";
					_interactionLabel.Visible = true;
				}
				HighlightObject(note);
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

	private void ShowNotification(string text)
	{
		_notificationText = text;
		_notificationTime = 2.0; // 2 seconds
	}

	private void OnInteract()
	{
		if (_isReadingNote)
		{
			CloseNote();
			return;
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
				else if (collider is Drawer)
				{
					bestInteractable = collider;
					break;
				}
			}

			if (bestInteractable is Pickup pickup) pickup.Interact(_inventory);
			else if (bestInteractable is Node nodePickup && nodePickup.GetParent() is Pickup parentPickup) parentPickup.Interact(_inventory);
			else if (bestInteractable is Door door)
			{
				string msg = door.Interact(_inventory);
				if (!string.IsNullOrEmpty(msg)) ShowNotification(msg);
			}
			else if (bestInteractable is Radio radio) radio.Interact(_inventory);
			else if (bestInteractable is Drawer drawer)
			{
				string msg = drawer.Interact(_inventory);
				if (!string.IsNullOrEmpty(msg)) ShowNotification(msg);
			}
			else if (bestInteractable is Note note)
			{
				ShowNote(note.NoteText);
			}
		}
	}

	private void ShowNote(string text)
	{
		_isReadingNote = true;
		if (_noteLabel != null) _noteLabel.Text = text;
		if (_noteOverlay != null) _noteOverlay.Visible = true;
		if (_interactionLabel != null) _interactionLabel.Visible = false;
	}

	private void CloseNote()
	{
		_isReadingNote = false;
		if (_noteOverlay != null) _noteOverlay.Visible = false;
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
			if (_gameOverLabel != null) _gameOverLabel.Visible = true;
			Input.MouseMode = Input.MouseModeEnum.Visible;
			GD.Print("GAME OVER: Sanity Depleted.");
		}
	}

	private void HandleHeadBob(double delta, bool isSprinting, bool isDucking)
	{
		float horizontalVelocity = new Vector3(Velocity.X, 0, Velocity.Z).Length();
		bool onFloor = IsOnFloor();
		
		if (!onFloor || horizontalVelocity < 0.1f)
		{
			_bobTime = 0.0f;
			Vector3 idleTargetPos = _defaultCamPos;
			if (isDucking) idleTargetPos.Y -= CrouchOffset;
			
			_camera.Position = _camera.Position.Lerp(idleTargetPos, (float)delta * CrouchTransitionSpeed);
			return;
		}

		float multiplier = isSprinting ? BobSprintMultiplier : 1.0f;
		_bobTime += (float)delta * horizontalVelocity * multiplier;
		
		Vector3 targetPos = _defaultCamPos;
		
		if (isDucking)
		{
			targetPos.Y -= CrouchOffset;
		}
		
		targetPos.Y += Mathf.Sin(_bobTime * BobFreq) * BobAmp * multiplier;
		targetPos.X += Mathf.Cos(_bobTime * BobFreq * 0.5f) * BobAmp * 0.5f * multiplier;
		
		_camera.Position = _camera.Position.Lerp(targetPos, (float)delta * CrouchTransitionSpeed);
	}
}
