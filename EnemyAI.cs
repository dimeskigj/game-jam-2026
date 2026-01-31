using Godot;
using System.Collections.Generic;

public partial class EnemyAI : CharacterBody3D
{
	[Export] public float PatrolSpeed = 3.0f;
	[Export] public float ChaseSpeed = 6.0f;
	[Export] public float RotationSpeed = 10.0f; 
	[Export] public float ModelRotationOffset = 0.0f; // Adjusted to 0 since model internal rotation is handled

	[ExportCategory("Detection")]
	[Export] public float DetectionRange = 10.0f;
	
	[ExportCategory("Patrol")]
	[Export] public Godot.Collections.Array<Node3D> Waypoints;
	[Export] public PatrolType MovementType = PatrolType.Loop;
	[Export] public float WaitTimeAtWaypoint = 1.0f;

	public enum PatrolType
	{
		Loop,
		PingPong
	}

	private int _currentWaypointIndex = 0;
	private int _patrolDirection = 1; // 1 for forward, -1 for backward
	private float _waitTimer = 0.0f;
	private bool _isWaiting = false;

	private PlayerController _targetPlayer;
	
	private enum State { Patrol, Chase, Investigate }
	private State _currentState = State.Patrol;
	private Vector3 _investigateTarget;

	// Nodes
	private AnimationPlayer _animPlayer;
	private Area3D _visionArea;
	private GeometryInstance3D _visualMesh; // For Debug Color

	public override void _Ready()
	{
		// ... existing code ...
		// Truncated for diff validity, rely on existing code context
		_animPlayer = GetNodeOrNull<AnimationPlayer>("Model/kit_player/AnimationPlayer");
		if (_animPlayer == null)
		{
			foreach (Node child in GetNode("Model").GetChildren())
			{
				if (child is AnimationPlayer ap) { _animPlayer = ap; break; }
				foreach (Node grandChild in child.GetChildren())
				{
					if (grandChild is AnimationPlayer ap2) { _animPlayer = ap2; break; }
				}
			}
		}

		if (_animPlayer != null)
		{
			GD.Print("Enemy AnimationPlayer found. Available animations:");
			foreach (string animName in _animPlayer.GetAnimationList())
			{
				GD.Print("- " + animName);
			}
		}
		else 
		{
			GD.PrintErr("Enemy AnimationPlayer NOT found!");
		}

		_visionArea = GetNodeOrNull<Area3D>("VisionArea");
		// Fallback for visual debugging
		_visualMesh = GetNodeOrNull<GeometryInstance3D>("Model/Body"); 
		
		if (_visionArea != null)
		{
			_visionArea.BodyEntered += OnBodyEnteredVision;
			_visionArea.BodyExited += OnBodyExitedVision;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		Vector3 velocity = Velocity;

		// Gravity
		if (!IsOnFloor())
			velocity.Y -= 9.8f * (float)delta;
			
		// Continuous Vision Check
		// This ensures we spot the player if they decloak while already inside our vision area
		if (_currentState == State.Patrol || _currentState == State.Investigate)
		{
			CheckVision();
		}

		switch (_currentState)
		{
			case State.Patrol:
				ProcessPatrol(ref velocity, (float)delta);
				break;
			case State.Chase:
				ProcessChase(ref velocity);
				break;
			case State.Investigate:
				ProcessInvestigate(ref velocity);
				break;
		}

		Velocity = velocity;
		MoveAndSlide();

		// Face movement direction
		if (velocity.LengthSquared() > 0.1f)
		{
			Vector3 lookTarget = GlobalPosition + velocity.Normalized();
			LookAt(lookTarget, Vector3.Up);
		}
		
		// Apply Visual Offset Correction
		if (GetNodeOrNull<Node3D>("Model") is Node3D modelNode)
		{
			modelNode.RotationDegrees = new Vector3(0, ModelRotationOffset, 0);
		}
		
		UpdateAnimation(velocity);
	}
	
	private void CheckVision()
	{
		if (_visionArea == null) return;
		
		var bodies = _visionArea.GetOverlappingBodies();
		foreach(var body in bodies)
		{
			if (body is PlayerController player)
			{
				if (player.CurrentMaskEffect == MaskEffect.Invisibility) continue;
				if (player.IsDead) continue;

				// Raycast Check
				var spaceState = GetWorld3D().DirectSpaceState;
				var query = PhysicsRayQueryParameters3D.Create(GlobalPosition + Vector3.Up * 1.5f, player.GlobalPosition + Vector3.Up * 0.5f);
				query.Exclude = new Godot.Collections.Array<Rid> { GetRid() }; 
				
				var result = spaceState.IntersectRay(query);

				if (result.Count > 0)
				{
					Node3D hitNode = result["collider"].Obj as Node3D;
					if (hitNode == player)
					{
						_targetPlayer = player;
						_currentState = State.Chase;
						_targetPlayer.SetAlert(true, this);
						GD.Print("ENEMY SPOTTED PLAYER! Switching to Chase.");
						return; // Found target, stop checking
					}
				}
			}
		}
	}

	private void ProcessPatrol(ref Vector3 velocity, float delta)
	{
		// If we were chasing and lost target (handled in Chase), ensure we reset detected?
		// Logic is in ProcessChase
		
		if (Waypoints == null || Waypoints.Count == 0)
		{
			velocity.X = Mathf.MoveToward(velocity.X, 0, PatrolSpeed * delta);
			velocity.Z = Mathf.MoveToward(velocity.Z, 0, PatrolSpeed * delta);
			return;
		}

		if (_isWaiting)
		{
			velocity.X = 0;
			velocity.Z = 0;
			_waitTimer -= delta;
			if (_waitTimer <= 0)
			{
				_isWaiting = false;
				AdvanceWaypoint();
			}
			return;
		}

		Node3D target = Waypoints[_currentWaypointIndex];
		Vector3 direction = (target.GlobalPosition - GlobalPosition);
		direction.Y = 0; // Keep movement planner on plane

		if (direction.Length() < 1.0f)
		{
			// Reached waypoint
			_isWaiting = true;
			_waitTimer = WaitTimeAtWaypoint;
			velocity.X = 0;
			velocity.Z = 0;
		}
		else
		{
			direction = direction.Normalized();
			velocity.X = direction.X * PatrolSpeed;
			velocity.Z = direction.Z * PatrolSpeed;
		}
	}

	private void AdvanceWaypoint()
	{
		if (Waypoints.Count <= 1) return;

		if (MovementType == PatrolType.Loop)
		{
			_currentWaypointIndex = (_currentWaypointIndex + 1) % Waypoints.Count;
		}
		else if (MovementType == PatrolType.PingPong)
		{
			if (_patrolDirection == 1)
			{
				if (_currentWaypointIndex >= Waypoints.Count - 1)
				{
					_patrolDirection = -1;
					_currentWaypointIndex--;
				}
				else
				{
					_currentWaypointIndex++;
				}
			}
			else
			{
				if (_currentWaypointIndex <= 0)
				{
					_patrolDirection = 1;
					_currentWaypointIndex++;
				}
				else
				{
					_currentWaypointIndex--;
				}
			}
		}
	}

	private void ProcessChase(ref Vector3 velocity)
	{
		if (_targetPlayer == null)
		{
			_currentState = State.Patrol;
			return;
		}

		if (_targetPlayer.CurrentMaskEffect == MaskEffect.Invisibility)
		{
			_targetPlayer.SetAlert(false, this);
			_targetPlayer = null;
			_currentState = State.Patrol;
			return;
		}

		// Ensure we signal we see them
		_targetPlayer.SetAlert(true, this);

		Vector3 direction = (_targetPlayer.GlobalPosition - GlobalPosition);
		float distance = direction.Length();
		direction.Y = 0;
		direction = direction.Normalized();

		velocity.X = direction.X * ChaseSpeed;
		velocity.Z = direction.Z * ChaseSpeed;
		
		// If player too far, lose aggro
		// Force minimum of 15.0f if DetectionRange is 0 (broken inspector)
		float chaseLimit = Mathf.Max(DetectionRange, 10.0f) * 1.5f;
		
		if (distance > chaseLimit)
		{
			// Debug why we lost aggro
			// GD.Print($"Enemy Lost Aggro: Dist {distance:F1} > Limit {chaseLimit:F1}");
			_targetPlayer.SetAlert(false, this);
			_targetPlayer = null;
			_currentState = State.Patrol;
		}
	}

	private void UpdateAnimation(Vector3 velocity)
	{
		if (_animPlayer == null) return;

		Vector2 horizontalVel = new Vector2(velocity.X, velocity.Z);
		string animToPlay = "idle"; // Default lowercase for kit_player

		if (horizontalVel.Length() > 0.1f)
		{
			// The kit_player usually has "run" or "walk". 
			// We'll try "run" for chase, "walk" for patrol if available.
			if ((_currentState == State.Chase || _currentState == State.Investigate) && _animPlayer.HasAnimation("run"))
				animToPlay = "run";
			else if (_animPlayer.HasAnimation("walk"))
				animToPlay = "walk";
			else if (_animPlayer.HasAnimation("run"))
				animToPlay = "run";
		}
		
		// Fallback if specific lowercase names don't exist, try Title Case
		if (!_animPlayer.HasAnimation(animToPlay))
		{
			string titleCase = char.ToUpper(animToPlay[0]) + animToPlay.Substring(1);
			if (_animPlayer.HasAnimation(titleCase))
				animToPlay = titleCase;
		}

		// Use Get().AsString() to avoid Linkage/MissingMethodException between string/StringName types in different Godot versions
		// This bypasses the C# property and uses the dynamic property system.
		string currentAnim = _animPlayer.Get("current_animation").AsString();
		
		if (currentAnim != animToPlay)
		{
			if (_animPlayer.HasAnimation(animToPlay))
			{
				_animPlayer.Play(animToPlay, 0.2f);
			}
		}
	}

	private void OnBodyEnteredVision(Node3D body)
	{
		GD.Print($"Body entered vision: {body.Name} ({body.GetType().Name})");

		if (body is PlayerController player)
		{
			if (player.CurrentMaskEffect == MaskEffect.Invisibility) return;

			GD.Print("Body is PlayerController. Checking Raycast...");
			
			// Check Line of Sight
			var spaceState = GetWorld3D().DirectSpaceState;
			// Aim at player position. PlayerController (CharacterBody3D) pivot is usually feet.
			// Let's aim slightly up to avoid ground checking or low obstacles.
			var query = PhysicsRayQueryParameters3D.Create(GlobalPosition + Vector3.Up * 1.5f, player.GlobalPosition + Vector3.Up * 0.5f);
			
			// Add exceptions
			query.Exclude = new Godot.Collections.Array<Rid> { GetRid() }; 
			
			var result = spaceState.IntersectRay(query);

			if (result.Count > 0)
			{
				Node3D hitNode = result["collider"].Obj as Node3D;
				// GD.Print($"Raycast hit: {hitNode?.Name}");
				
				if (hitNode == player)
				{
					_targetPlayer = player;
					_currentState = State.Chase;
					GD.Print("ENEMY SPOTTED PLAYER! Chasing...");
				}
				else
				{
					GD.Print("Raycast blocked by: " + hitNode?.Name);
				}
			}
			else
			{
				// If nothing hit (open air?), it usually means we didn't hit anything, so we see nothing?
				// Or did we mess up the query? IntersectRay returns nothing if it doesn't hit a collider.
				// If we don't hit a collider, we theoretically don't see the player (unless the player has no collider, which is impossible for CharacterBody3D)
				// So count == 0 means blocked? No, count == 0 means "ray went to infinity" or "hit nothing matching mask".
				// IF we aim AT the player, and hit nothing, it implies the player is not hittable?
				// BUT player usually has collision layer.
				// We need to ensure Raycast Mask includes Player.
				GD.Print("Raycast hit nothing. Is player collider masked check?");
			}
		}
	}
	
	public void AlertToLocation(Vector3 location)
	{
		// If we are already chasing the player, we have better info (visual contact)
		if (_currentState == State.Chase) return;

		// Update investigation target
		_investigateTarget = location;
		
		// If already investigating, just update target (no print spam)
		if (_currentState != State.Investigate)
		{
			GD.Print($"Enemy alerted to location: {location}");
			_currentState = State.Investigate;
		}
	}

	private void ProcessInvestigate(ref Vector3 velocity)
	{
		Vector3 direction = (_investigateTarget - GlobalPosition);
		direction.Y = 0;

		if (direction.Length() < 1.0f)
		{
			// Reached investigation point.
			// Look around? Just go back to patrol for now.
			GD.Print("Enemy reached alert location. Resuming patrol.");
			_currentState = State.Patrol;
			velocity.X = 0;
			velocity.Z = 0;
		}
		else
		{
			// Run there? Or Walk? Let's Run.
			direction = direction.Normalized();
			velocity.X = direction.X * ChaseSpeed;
			velocity.Z = direction.Z * ChaseSpeed;
		}
	}
	
	private void OnBodyExitedVision(Node3D body)
	{
		// Keep chasing logic handled in ProcessChase
	}
}
