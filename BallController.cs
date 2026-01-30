using Godot;

public partial class BallController : CharacterBody3D
{
	[Export]
	public float Speed = 5.0f;
	[Export]
	public float JumpVelocity = 6.0f;

	// Get the gravity from the project settings to be synced with RigidBody nodes.
	public float gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();

	public override void _PhysicsProcess(double delta)
	{
		Vector3 velocity = Velocity;

		// Add the gravity.
		if (!IsOnFloor())
			velocity.Y -= gravity * (float)delta;

		// Handle Jump.
		if (Input.IsKeyPressed(Key.Space) && IsOnFloor())
			velocity.Y = JumpVelocity;

		// Get the input direction and handle the movement/deceleration.
		Vector2 inputDir = Vector2.Zero;
		
		if (Input.IsKeyPressed(Key.W)) inputDir.Y -= 1;
		if (Input.IsKeyPressed(Key.S)) inputDir.Y += 1;
		if (Input.IsKeyPressed(Key.A)) inputDir.X -= 1;
		if (Input.IsKeyPressed(Key.D)) inputDir.X += 1;
		
		inputDir = inputDir.Normalized();
		
		// Move relative to global coordinates (could be improved to be camera-relative)
		Vector3 direction = new Vector3(inputDir.X, 0, inputDir.Y).Normalized();

		if (direction != Vector3.Zero)
		{
			velocity.X = direction.X * Speed;
			velocity.Z = direction.Z * Speed;
		}
		else
		{
			velocity.X = Mathf.MoveToward(Velocity.X, 0, Speed);
			velocity.Z = Mathf.MoveToward(Velocity.Z, 0, Speed);
		}

		Velocity = velocity;
		MoveAndSlide();
	}
}
