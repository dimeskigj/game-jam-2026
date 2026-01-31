using Godot;
using System.Collections.Generic;

public partial class PlayerController : CharacterBody3D
{
	[Export]
	public float Speed = 5.0f;
	[Export]
	public float SprintSpeed = 10.0f;
	[Export]
	public float JumpVelocity = 6.0f;
	[Export]
	public float BaseFov = 75.0f;
	[Export]
	public float SprintFov = 85.0f;
	[Export]
	public float FovChangeSpeed = 5.0f;
	
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

	[Export] public float MaxSanity = 100.0f;
	[Export] public float SanityDrainRate = 5.0f; 
	[Export] public float SanityRegenRate = 10.0f; 

	// Synced Properties for Multiplayer
	[Export]
	public string PlayerName
	{
		get => _playerName;
		set
		{
			_playerName = value;
			UpdateVisuals();
		}
	}
	private string _playerName = "Player";

	[Export]
	public Vector3 PlayerColorVec
	{
		get => new Vector3(_playerColor.R, _playerColor.G, _playerColor.B);
		set
		{
			_playerColor = new Color(value.X, value.Y, value.Z);
			UpdateVisuals();
		}
	}
	private Color _playerColor = Colors.White;

	// State
	public float CurrentSanity { get; private set; } = 100.0f;
	public bool IsDead { get; private set; } = false;
	public MaskEffect CurrentMaskEffect { get; private set; } = MaskEffect.None;
	
	private HashSet<Node> _alertSources = new HashSet<Node>();
	private Vector3 _velocity = Vector3.Zero;
	public float gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();

	// Components
	private Camera3D _camera;
	private ShapeCast3D _interactionCast;
	private Inventory _inventory;
	private PlayerInput _input;
	private Label3D _nameLabel;
	private GeometryInstance3D _mesh;

	// UI (Only for local player)
	private ColorRect _gasMaskOverlay;
	private ColorRect _xRayOverlay;
	private ColorRect _stealthOverlay;
	private ColorRect _sanityOverlay;
	private Label _detectionLabel;     
	private ColorRect _detectionOverlay; 
	private Label _interactionLabel;
	private ProgressBar _sanityBar;
	private Label _gameOverLabel;
	private XRayManager _xRayManager;
	
	// Visual FX
	private ShaderMaterial _outlineMaterial;
	private GeometryInstance3D _currentOutlineObj;
	private RigidBody3D _heldBody;

	public override void _Ready()
	{
		AddToGroup("Players");
		
		_camera = GetNode<Camera3D>("Camera3D");
		_interactionCast = _camera.GetNode<ShapeCast3D>("ShapeCast3D");
		_inventory = GetNode<Inventory>("Inventory");
		_input = GetNode<PlayerInput>("PlayerInput");
		_nameLabel = GetNode<Label3D>("Label3D");
		_mesh = GetNode<GeometryInstance3D>("MeshInstance3D");

		CurrentSanity = MaxSanity;
		UpdateVisuals();

		// Multiplayer Authority setup
		int id = 1;
		if (int.TryParse(Name, out id))
		{
			SetMultiplayerAuthority(id);
		}

		if (IsMultiplayerAuthority())
		{
			_camera.Current = true;
			Input.MouseMode = Input.MouseModeEnum.Captured;

			// Get data from NetworkManager
			var net = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
			if (net != null)
			{
				PlayerName = net.PlayerName;
				var c = net.PlayerColor;
				PlayerColorVec = new Vector3(c.R, c.G, c.B);
			}

			// Hook up UI
			SetupLocalUI();

			// Connect Signals
			_input.LookInput += OnLookInput;
			_input.ToggleMouseCapture += OnToggleMouseCapture;
			_input.Interact += OnInteract;
			_input.UseItem += OnUseItem; 
			_input.DropItem += OnDropItem; 
			_input.SlotSelected += OnSlotSelected;
			_input.ScrollSlot += OnScrollSlot;
			
			_inventory.ItemUsed += OnInventoryItemUsed;

			// Outline Material
			_outlineMaterial = new ShaderMaterial();
			_outlineMaterial.Shader = GD.Load<Shader>("res://Shaders/outline.gdshader");
			
			_interactionCast.AddException(this);
		}
		else
		{
			_camera.Current = false;
		}
	}

	private void SetupLocalUI()
	{
		var ui = GetTree().Root.GetNodeOrNull<InventoryUI>("Node3D/UI");
		if (ui == null) return;

		ui.SetInventory(_inventory);
		
		_gasMaskOverlay = ui.GetNode<ColorRect>("GasMaskOverlay");
		_xRayOverlay = ui.GetNode<ColorRect>("XRayOverlay");
		_stealthOverlay = ui.GetNode<ColorRect>("StealthOverlay");
		_sanityOverlay = ui.GetNode<ColorRect>("SanityOverlay");
		_detectionOverlay = ui.GetNodeOrNull<ColorRect>("DetectionOverlay"); 
		_interactionLabel = ui.GetNode<Label>("InteractionLabel");
		_sanityBar = ui.GetNode<ProgressBar>("SanityBar");
		_gameOverLabel = ui.GetNode<Label>("GameOverLabel");
		_detectionLabel = ui.GetNodeOrNull<Label>("DetectionLabel"); 

		_xRayManager = GetTree().Root.GetNodeOrNull<XRayManager>("Node3D/XRayManager");
	}

	private void UpdateVisuals()
	{
		if (_nameLabel != null) _nameLabel.Text = _playerName;
		if (_mesh != null)
		{
			var material = new StandardMaterial3D();
			material.AlbedoColor = _playerColor;
			_mesh.MaterialOverride = material;
		}
	}

	public void SetAlert(bool active, Node source)
	{
		if (!IsMultiplayerAuthority()) return;

		if (active) _alertSources.Add(source);
		else _alertSources.Remove(source);

		bool isDetected = _alertSources.Count > 0;
		if (_detectionLabel != null) _detectionLabel.Visible = isDetected;
		if (_detectionOverlay != null) _detectionOverlay.Visible = isDetected;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!IsMultiplayerAuthority()) return;

		UpdateSanity(delta);
		if (IsDead) return;

		UpdateInteractionPrompt();
		HandlePhysicsObjectGrab();

		Vector3 velocity = Velocity;
		if (!IsOnFloor()) velocity.Y -= gravity * (float)delta;

		if (_input.JumpPressed && IsOnFloor()) velocity.Y = JumpVelocity;

		Vector2 inputDir = _input.MoveInput;
		Vector3 moveDirection = (Transform.Basis * new Vector3(inputDir.X, 0, inputDir.Y)).Normalized();

		bool isSprinting = Input.IsKeyPressed(Key.Shift) && inputDir != Vector2.Zero;
		float targetSpeed = isSprinting ? SprintSpeed : Speed;

		// FOV
		float targetFov = isSprinting ? SprintFov : BaseFov;
		_camera.Fov = (float)Mathf.Lerp(_camera.Fov, targetFov, FovChangeSpeed * (float)delta);

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
	}

	private void HandlePhysicsObjectGrab()
	{
		if (_input.LeftClickHeld && Input.MouseMode == Input.MouseModeEnum.Captured)
		{
			if (_heldBody == null)
			{
				if (_interactionCast.IsColliding())
				{
					for (int i = 0; i < _interactionCast.GetCollisionCount(); i++)
					{
						var collider = _interactionCast.GetCollider(i);
						if (collider is RigidBody3D rb && !(collider is Pickup)) 
						{
							_heldBody = rb;
							break;
						}
					}
				}
			}
		}
		else
		{
			_heldBody = null;
		}

		if (_heldBody != null && IsInstanceValid(_heldBody))
		{
			Vector3 targetPos = _camera.GlobalTransform.Origin - _camera.GlobalTransform.Basis.Z * HoldDistance;
			Vector3 currentPos = _heldBody.GlobalTransform.Origin;
			Vector3 grabDirection = targetPos - currentPos; 
			_heldBody.LinearVelocity = grabDirection * GrabPower;
		}
	}

	private void UpdateInteractionPrompt()
	{
		if (_interactionLabel == null) return;

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
				else if (collider.GetParent() is Pickup)
				{
					bestInteractable = collider.GetParent();
					break;
				}
			}

			if (bestInteractable is Pickup pickup)
			{
				_interactionLabel.Text = $"Right Click to Pickup {pickup.ItemResource?.Name}";
				_interactionLabel.Visible = true;
				HighlightObject(pickup);
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
		if (!IsMultiplayerAuthority()) return;
		if (Input.MouseMode != Input.MouseModeEnum.Captured) return;

		RotateY(-relative.X * MouseSensitivity);
		Vector3 camRot = _camera.Rotation;
		camRot.X -= relative.Y * MouseSensitivity;
		camRot.X = Mathf.Clamp(camRot.X, Mathf.DegToRad(-89), Mathf.DegToRad(89));
		_camera.Rotation = camRot;
	}

	private void OnToggleMouseCapture(bool captured)
	{
		if (!IsMultiplayerAuthority()) return;
		Input.MouseMode = captured ? Input.MouseModeEnum.Captured : Input.MouseModeEnum.Visible;
	}

	private void OnInteract()
	{
		if (!IsMultiplayerAuthority()) return;

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
				else if (collider.GetParent() is Pickup)
				{
					bestInteractable = collider.GetParent();
					break;
				}
			}

			if (bestInteractable is Pickup pickup) pickup.Interact(_inventory);
			else if (bestInteractable is Door door) door.Interact(_inventory);
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

	private void OnInventoryItemUsed(InventoryItem item)
	{
		if (item.Type == ItemType.Mask)
		{
			if (CurrentMaskEffect == item.Effect) UnequipMask();
			else EquipMask(item.Effect);
		}
	}

	public void EquipMask(MaskEffect effect)
	{
		UnequipMask();
		CurrentMaskEffect = effect;
		
		if (_gasMaskOverlay != null && effect == MaskEffect.Gas) _gasMaskOverlay.Visible = true;
		else if (_xRayOverlay != null && effect == MaskEffect.XRay)
		{
			_xRayOverlay.Visible = true;
			_xRayManager?.ToggleXRay(true);
		}
		else if (_stealthOverlay != null && effect == MaskEffect.Invisibility) _stealthOverlay.Visible = true;
	}

	public void UnequipMask()
	{
		if (CurrentMaskEffect == MaskEffect.XRay) _xRayManager?.ToggleXRay(false);
		
		CurrentMaskEffect = MaskEffect.None;
		if (_gasMaskOverlay != null) _gasMaskOverlay.Visible = false;
		if (_xRayOverlay != null) _xRayOverlay.Visible = false;
		if (_stealthOverlay != null) _stealthOverlay.Visible = false;
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
		}
	}
}
