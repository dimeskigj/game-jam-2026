using Godot;

public partial class BallController : CharacterBody3D
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

	// Get the gravity from the project settings to be synced with RigidBody nodes.
	public float gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();

	private Camera3D _camera;
	private RayCast3D _interactionRay;
	private Inventory _inventory;

	public override void _Ready()
	{
		Input.MouseMode = Input.MouseModeEnum.Captured;
		_camera = GetNode<Camera3D>("Camera3D");
		_interactionRay = _camera.GetNode<RayCast3D>("RayCast3D");
		_inventory = GetNode<Inventory>("Inventory");
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseMotion mouseMotion && Input.MouseMode == Input.MouseModeEnum.Captured)
		{
			RotateY(-mouseMotion.Relative.X * MouseSensitivity);
			
			if (_camera != null)
			{
				Vector3 camRot = _camera.Rotation;
				camRot.X -= mouseMotion.Relative.Y * MouseSensitivity;
				camRot.X = Mathf.Clamp(camRot.X, Mathf.DegToRad(-89), Mathf.DegToRad(89));
				_camera.Rotation = camRot;
			}
		}
		
		// Mouse Capture
		if (Input.IsMouseButtonPressed(MouseButton.Left))
		{
			Input.MouseMode = Input.MouseModeEnum.Captured;
		}
		if (Input.IsKeyPressed(Key.Escape))
		{
			Input.MouseMode = Input.MouseModeEnum.Visible;
		}

		// Inventory Interaction
		if (Input.IsMouseButtonPressed(MouseButton.Right) && _interactionRay.IsColliding())
		{
			var collider = _interactionRay.GetCollider();
			if (collider is Pickup pickup)
			{
				pickup.Interact(_inventory);
			}
			else if (collider is Node node && node.GetParent() is Pickup parentPickup)
			{
				// In case we hit a child of the pickup (like the mesh)
				parentPickup.Interact(_inventory);
			}
		}

		// Use Item
		if (Input.IsKeyPressed(Key.E))
		{
			_inventory.UseCurrentItem();
		}
		
		// Slot Selection (Numbers 1-8)
		if (@event is InputEventKey keyEvent && keyEvent.Pressed)
		{
			if (keyEvent.Keycode >= Key.Key1 && keyEvent.Keycode <= Key.Key8)
			{
				_inventory.SelectSlot((int)(keyEvent.Keycode - Key.Key1));
			}
		} 
	}

	public override void _PhysicsProcess(double delta)
	{
		Vector3 velocity = Velocity;

		// Add the gravity.
		if (!IsOnFloor())
			velocity.Y -= gravity * (float)delta;
		
		// Handle Jump.
		// Note: Using Key.Space directly as per previous code style
		if (Input.IsKeyPressed(Key.Space) && IsOnFloor())
			velocity.Y = JumpVelocity;

		// Get the input direction
		Vector2 inputDir = Vector2.Zero;
		
		if (Input.IsKeyPressed(Key.W)) inputDir.Y -= 1;
		if (Input.IsKeyPressed(Key.S)) inputDir.Y += 1;
		if (Input.IsKeyPressed(Key.A)) inputDir.X -= 1;
		if (Input.IsKeyPressed(Key.D)) inputDir.X += 1;
		
		inputDir = inputDir.Normalized();
		
		// Move relative to the character's forward direction
		Vector3 direction = (Transform.Basis * new Vector3(inputDir.X, 0, inputDir.Y)).Normalized();

		if (direction != Vector3.Zero)
		{
			// Apply acceleration (Humanlike movement)
			velocity.X = Mathf.MoveToward(velocity.X, direction.X * Speed, Acceleration * (float)delta);
			velocity.Z = Mathf.MoveToward(velocity.Z, direction.Z * Speed, Acceleration * (float)delta);
		}
		else
		{
			// Apply friction/deceleration
			velocity.X = Mathf.MoveToward(velocity.X, 0, Friction * (float)delta);
			velocity.Z = Mathf.MoveToward(velocity.Z, 0, Friction * (float)delta);
		}

		Velocity = velocity;
		MoveAndSlide();
	}
}
