using Godot;

public partial class InsectAI : CharacterBody3D
{
	[Export] public float Speed = 12.0f;
	[Export] public float Damage = 5.0f;
	[Export] public float DetectionRange = 8.0f; // Ignored if using Area3D size but good reference
	
	private enum State { Idle, Chase, Flee }
	private State _currentState = State.Idle;
	
	private Node3D _target;
	private Area3D _visionArea;
	
	// Idle wandering
	private Vector3 _wanderTarget;
	private float _wanderTimer = 0.0f;

	public override void _Ready()
	{
		_visionArea = GetNode<Area3D>("VisionArea");
		
		// Setup initial idle target
		PickNewWanderTarget();
	}

	public override void _PhysicsProcess(double delta)
	{
		CheckVision();
		
		Vector3 velocity = Velocity;
		
		// Apply gravity
		if (!IsOnFloor())
		{
			velocity.Y -= 9.8f * (float)delta;
		}
		
		switch (_currentState)
		{
			case State.Idle:
				ProcessIdle(ref velocity, delta);
				break;
			case State.Chase:
				ProcessChase(ref velocity, delta);
				break;
			case State.Flee:
				ProcessFlee(ref velocity, delta);
				break;
		}

		Velocity = velocity;
		MoveAndSlide();

		// Face movement direction
		Vector3 flatVel = new Vector3(velocity.X, 0, velocity.Z);
		if (flatVel.LengthSquared() > 0.1f)
		{
			Vector3 lookTarget = GlobalPosition + flatVel.Normalized();
			LookAt(lookTarget, Vector3.Up);
		}
	}
	
	private void CheckVision()
	{
		if (_currentState == State.Chase)
		{
			// Check if player put on the repellent mask while we are chasing
			if (_target != null && _target is PlayerController pc && pc.CurrentMaskEffect == MaskEffect.InsectRepellent)
			{
				_currentState = State.Flee;
				return;
			}
			return; 
		}
		
		if (_visionArea == null) return;
		
		var bodies = _visionArea.GetOverlappingBodies();
		foreach (var body in bodies)
		{
			if (body.IsInGroup("Player") || body.Name == "Ball") // Assuming player has "Player" group or is named Ball
			{
				Node3D playerNode = body as Node3D;
				
				// Repellent Check
				if (playerNode is PlayerController pc && pc.CurrentMaskEffect == MaskEffect.InsectRepellent)
				{
					_target = playerNode;
					_currentState = State.Flee;
					return;
				}
				
				// Optional: Check line of sight
				var spaceState = GetWorld3D().DirectSpaceState;
				var query = PhysicsRayQueryParameters3D.Create(GlobalPosition + Vector3.Up * 0.2f, playerNode.GlobalPosition + Vector3.Up * 0.5f);
				query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
				var result = spaceState.IntersectRay(query);
				
				bool hasLineOfSight = false;
				
				if (result.Count > 0)
				{
					var collider = result["collider"].As<Node>();
					if (collider == playerNode) hasLineOfSight = true;
				}
				
				if (hasLineOfSight)
				{
					_target = playerNode;
					_currentState = State.Chase;
					GD.Print("Insect spotted player!");
					return;
				}
			}
		}
	}
	
	private void ProcessIdle(ref Vector3 velocity, double delta)
	{
		_wanderTimer -= (float)delta;
		if (_wanderTimer <= 0)
		{
			PickNewWanderTarget();
		}
		
		Vector3 direction = (_wanderTarget - GlobalPosition).Normalized();
		direction.Y = 0;
		
		if (GlobalPosition.DistanceTo(_wanderTarget) < 0.5f)
		{
			velocity.X = 0;
			velocity.Z = 0;
		}
		else
		{
			velocity.X = direction.X * (Speed * 0.3f); // Wanders slower
			velocity.Z = direction.Z * (Speed * 0.3f);
		}
	}
	
	private void ProcessChase(ref Vector3 velocity, double delta)
	{
		if (_target == null)
		{
			_currentState = State.Idle;
			return;
		}
		
		// Re-check for mask while chasing
		if (_target is PlayerController pc && pc.CurrentMaskEffect == MaskEffect.InsectRepellent)
		{
			_currentState = State.Flee;
			return;
		}
		
		Vector3 direction = (_target.GlobalPosition - GlobalPosition).Normalized();
		direction.Y = 0;
		
		velocity.X = direction.X * Speed;
		velocity.Z = direction.Z * Speed;
		
		// If player gets too far, stop chasing
		if (GlobalPosition.DistanceTo(_target.GlobalPosition) > DetectionRange * 1.5f)
		{
			_currentState = State.Idle;
			_target = null;
		}
	}

	private void ProcessFlee(ref Vector3 velocity, double delta)
	{
		if (_target == null)
		{
			_currentState = State.Idle;
			return;
		}
		
		// Run AWAY from target
		Vector3 direction = (GlobalPosition - _target.GlobalPosition).Normalized();
		direction.Y = 0;
		
		velocity.X = direction.X * Speed * 1.5f; // Flee fast!
		velocity.Z = direction.Z * Speed * 1.5f;
		
		// If sufficiently far away, stop fleeing and go idle
		if (GlobalPosition.DistanceTo(_target.GlobalPosition) > DetectionRange * 2.0f)
		{
			_currentState = State.Idle;
			_target = null;
		}
		
		// Also check if mask was taken off
		if (_target is PlayerController pc && pc.CurrentMaskEffect != MaskEffect.InsectRepellent)
		{
			// If close enough, switch back to chase immediately? Or just idle.
			// Let's chase if close.
			if (GlobalPosition.DistanceTo(_target.GlobalPosition) < DetectionRange)
				_currentState = State.Chase;
			else
				_currentState = State.Idle;
		}
	}
	
	private void PickNewWanderTarget()
	{
		_wanderTimer = (float)GD.RandRange(2.0, 5.0);
		Vector3 randomOffset = new Vector3((float)GD.RandRange(-5.0, 5.0), 0, (float)GD.RandRange(-5.0, 5.0));
		_wanderTarget = GlobalPosition + randomOffset;
	}
}
