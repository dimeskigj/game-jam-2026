using Godot;
using System.Collections.Generic;

public partial class EnemyAI : CharacterBody3D
{
	[Export] public float PatrolSpeed = 3.0f;
	[Export] public float ChaseSpeed = 6.0f;
	[Export] public float DetectionRange = 10.0f;
	[Export] public float ChaseLoseTime = 3.0f;
	
	[ExportCategory("Patrol")]
	[Export] public Godot.Collections.Array<Node3D> Waypoints;
	[Export] public PatrolType MovementType = PatrolType.Loop;
	[Export] public float WaitTimeAtWaypoint = 1.0f;

	public enum PatrolType { Loop, PingPong }

	private enum State { Patrol, Chase, Investigate }
	private State _currentState = State.Patrol;
	
	private int _currentWaypointIndex = 0;
	private int _patrolDirection = 1;
	private float _waitTimer = 0.0f;
	private bool _isWaiting = false;
	private float _chaseTimer = 0.0f;
	
	private PlayerController _targetPlayer;
	private Vector3 _investigateTarget;
	
	private NavigationAgent3D _navAgent;
	private Area3D _visionArea;
	private RayCast3D _obstacleRay;
	private AnimationPlayer _animPlayer;
	private bool _mapReady = false;
	private float _investigateTimer = 0.0f;
	private const float MaxInvestigateTime = 15.0f;

	public override void _EnterTree()
	{
		AddToGroup("Enemies");
	}

	public override void _Ready()
	{
		_navAgent = GetNode<NavigationAgent3D>("NavigationAgent3D");
		_obstacleRay = GetNodeOrNull<RayCast3D>("ObstacleRay");
		_visionArea = GetNodeOrNull<Area3D>("VisionArea");

		// Find animation player recursively
		_animPlayer = FindFirstNodeOfType<AnimationPlayer>(this);
		
		Node model = GetNodeOrNull("Model");
		if (model != null)
		{
			// DEBUG: Print Structure
			PrintNodeTree(model, "");
		}
		
		if (_animPlayer != null)
		{
			var anims = _animPlayer.GetAnimationList();
			GD.Print($"EnemyAI: Main AnimationPlayer found at {_animPlayer.GetPath()}. Anims: {string.Join(", ", anims)}");
			
			// Try to ensure looping on idle if possible (though libraries might be read-only)
			// We just play them.
			
			// FIX ROOT MOTION (Force In-Place)
			foreach(var animName in anims)
			{
				var anim = _animPlayer.GetAnimation(animName);
				MakeAnimationInPlace(anim, animName);
			}
		}
		else
		{
			GD.PrintErr("EnemyAI: Critical - No AnimationPlayer found in Model!");
		}
		
		if (_visionArea != null)
		{
			_visionArea.BodyEntered += OnBodyEnteredVision;
			_visionArea.BodyExited += OnBodyExitedVision;
		}

		// Improve navigation parameters
		_navAgent.PathDesiredDistance = 1.0f;
		_navAgent.TargetDesiredDistance = 1.0f;
		_navAgent.PathMaxDistance = 15.0f; 
		_navAgent.AvoidanceEnabled = false;
		_navAgent.Radius = 0.5f;
		_navAgent.NeighborDistance = 10.0f;
		_navAgent.MaxNeighbors = 10;
		_navAgent.VelocityComputed += OnVelocityComputed;

		// Defer setup to ensuring physics is ready
		CallDeferred(MethodName.ActorSetup);
	}
	
	// Removed InjectAnimations logic as scene now has libraries linked.

	private async void ActorSetup()
	{
		// ... existing setup ...
		await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
		await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

		_mapReady = true;
		GD.Print("EnemyAI: ActorSetup complete. Navigation ready.");
		
		if (Waypoints != null && Waypoints.Count > 0)
		{
			_currentWaypointIndex = 0;
			_navAgent.TargetPosition = Waypoints[0].GlobalPosition;
		}
	}
	// ...

	private void UpdateAnimation(Vector3 velocity)
	{
		if (_animPlayer == null) return;
		
		Vector2 horizontalVel = new Vector2(velocity.X, velocity.Z);
		float speed = horizontalVel.Length();
		
		string targetAnim = "";
		
		// Find available animations from list
		var animList = _animPlayer.GetAnimationList();
		
		string runAnim = "";
		string walkAnim = "";
		string idleAnim = "";
		
		// Map animations based on naming conventions in the specific scene libraries
		foreach(var a in animList)
		{
			string lower = a.ToLower();
			if (lower.Contains("run")) runAnim = a;
			else if (lower.Contains("walk")) walkAnim = a;
			else if (lower.Contains("idle") || lower.Contains("mixamo")) idleAnim = a;
		}
		
		if (speed > 4.0f && !string.IsNullOrEmpty(runAnim))
		{
			targetAnim = runAnim;
		}
		else if (speed > 0.1f && !string.IsNullOrEmpty(walkAnim))
		{
			targetAnim = walkAnim;
		}
		else if (!string.IsNullOrEmpty(idleAnim))
		{
			targetAnim = idleAnim;
		}
		
		if (!string.IsNullOrEmpty(targetAnim))
		{
			if (_animPlayer.CurrentAnimation != targetAnim)
			{
				_animPlayer.Play(targetAnim, 0.2f);
			}
		}
	}



	public override void _PhysicsProcess(double delta)
	{
		if (!_mapReady) return;
		
		Vector3 velocity = Velocity;
		
		// Apply gravity
		if (!IsOnFloor())
			velocity.Y -= 9.8f * (float)delta;
		
		// Check vision when not chasing
		if (_currentState == State.Patrol || _currentState == State.Investigate)
		{
			CheckVision();
		}
		
		// Process current state
		switch (_currentState)
		{
			case State.Patrol:
				ProcessPatrol(ref velocity, (float)delta);
				break;
			case State.Chase:
				ProcessChase(ref velocity, (float)delta);
				break;
			case State.Investigate:
				ProcessInvestigate(ref velocity, (float)delta);
				break;
		}
		
		// Instead of MoveAndSlide, we pass desired velocity to avoidance
		if (_navAgent.AvoidanceEnabled)
		{
			_navAgent.Velocity = velocity;
		}
		else
		{
			Velocity = velocity;
			MoveAndSlide();
			
			// Handle door collisions
			HandleDoorCollisions();
			
			// Push items gently
			ApplyKickForce();
			
			// Non-avoidance logic (face direction, animations)
			Vector3 flatVelocity = new Vector3(velocity.X, 0, velocity.Z);
			if (flatVelocity.LengthSquared() > 0.01f)
			{
				Vector3 lookTarget = GlobalPosition + flatVelocity.Normalized();
				LookAt(lookTarget, Vector3.Up);
			}
			UpdateAnimation(velocity);
		}
	}

	private void OnVelocityComputed(Vector3 safeVelocity)
	{
		if (!_navAgent.AvoidanceEnabled) return; // Ignore callback if avoidance is off

		Velocity = safeVelocity;
		MoveAndSlide();
		
		// Handle door collisions
		HandleDoorCollisions();
		
		// Push items gently
		ApplyKickForce();
		
		// Face movement direction - use safe velocity for better rotation
		Vector3 flatVelocity = new Vector3(safeVelocity.X, 0, safeVelocity.Z);
		if (flatVelocity.LengthSquared() > 0.01f)
		{
			Vector3 lookTarget = GlobalPosition + flatVelocity.Normalized();
			LookAt(lookTarget, Vector3.Up);
		}
		
		UpdateAnimation(safeVelocity);
	}

	private void CheckVision()
	{
		if (_visionArea == null) return;
		
		var bodies = _visionArea.GetOverlappingBodies();
		foreach (var body in bodies)
		{
			if (body is PlayerController player)
			{
				if (player.CurrentMaskEffect == MaskEffect.Invisibility) continue;
				if (player.IsDead) continue;
				
				// Raycast line of sight check
				var spaceState = GetWorld3D().DirectSpaceState;
				var query = PhysicsRayQueryParameters3D.Create(
					GlobalPosition + Vector3.Up * 1.5f,
					player.GlobalPosition + Vector3.Up * 0.5f
				);
				query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
				
				var result = spaceState.IntersectRay(query);
				if (result.Count > 0 && result["collider"].Obj == player)
				{
					_targetPlayer = player;
					_currentState = State.Chase;
					_chaseTimer = 0.0f;
					_targetPlayer.SetAlert(true, this);
					GD.Print("Enemy spotted player!");
					return;
				}
			}
		}
	}

	private void ProcessPatrol(ref Vector3 velocity, float delta)
	{
		if (Waypoints == null || Waypoints.Count == 0)
		{
			GD.Print("Enemy: No waypoints assigned!");
			velocity.X = 0;
			velocity.Z = 0;
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
				GD.Print($"Enemy: Moving to waypoint {_currentWaypointIndex}");
			}
			return;
		}
		
		Node3D target = Waypoints[_currentWaypointIndex];
		Vector3 targetPos = target.GlobalPosition;
		_navAgent.TargetPosition = targetPos;
		
		float distanceToTarget = GlobalPosition.DistanceTo(targetPos);
		bool navFinished = _navAgent.IsNavigationFinished();
		
		// Debug output every 60 frames (about once per second)
		if (Engine.GetPhysicsFrames() % 60 == 0)
		{
			GD.Print($"Enemy Patrol Debug:");
			GD.Print($"  Position: {GlobalPosition}");
			GD.Print($"  Target: {targetPos}");
			GD.Print($"  Distance: {distanceToTarget:F2}");
			GD.Print($"  Nav Finished: {navFinished}");
			GD.Print($"  Is On Floor: {IsOnFloor()}");
		}
		
		if (navFinished || distanceToTarget < 1.0f)
		{
			GD.Print($"Enemy: Reached waypoint {_currentWaypointIndex}");
			_isWaiting = true;
			_waitTimer = WaitTimeAtWaypoint;
			velocity.X = 0;
			velocity.Z = 0;
		}
		else
		{
			Vector3 nextPos = _navAgent.GetNextPathPosition();
			Vector3 direction = (nextPos - GlobalPosition);
			direction.Y = 0;
			
			if (direction.LengthSquared() >= 0.01f)
			{
				direction = direction.Normalized();
				velocity.X = direction.X * PatrolSpeed;
				velocity.Z = direction.Z * PatrolSpeed;
			}
			else
			{
				velocity.X = 0;
				velocity.Z = 0;
			}
		}
	}

	private void ProcessChase(ref Vector3 velocity, float delta)
	{
		if (_targetPlayer == null)
		{
			_currentState = State.Patrol;
			return;
		}
		
		// Check if player is still visible
		bool canSeePlayer = IsPlayerVisible();
		
		if (canSeePlayer)
		{
			_chaseTimer = 0.0f;
			_targetPlayer.SetAlert(true, this);
			// Update last known position
			_investigateTarget = _targetPlayer.GlobalPosition;
		}
		else
		{
			_chaseTimer += delta;
			if (_chaseTimer >= ChaseLoseTime)
			{
				GD.Print($"Enemy: Lost sight for {ChaseLoseTime}s. Investigating last known pos: {_investigateTarget}");
				_targetPlayer.SetAlert(false, this);
				_targetPlayer = null;
				_currentState = State.Investigate;
				_investigateTimer = 0.0f;
				return;
			}
		}
		
		// Check for invisibility
		if (_targetPlayer.CurrentMaskEffect == MaskEffect.Invisibility)
		{
			GD.Print("Enemy: Player turned invisible. Returning to Patrol.");
			_targetPlayer.SetAlert(false, this);
			_targetPlayer = null;
			ReturnToPatrol();
			return;
		}
		
		// Check for locked door ahead
		if (_obstacleRay != null && _obstacleRay.IsColliding())
		{
			if (_obstacleRay.GetCollider() is Door door && door.IsLocked)
			{
				GD.Print("Enemy blocked by locked door. Investigating current location.");
				_investigateTarget = GlobalPosition;
				_targetPlayer.SetAlert(false, this);
				_targetPlayer = null;
				_currentState = State.Investigate;
				_investigateTimer = 0.0f;
				return;
			}
		}
		
		// Navigate to player
		_navAgent.TargetPosition = _targetPlayer.GlobalPosition;
		
		// Optional: If path is invalid, maybe look at player?
		
		Vector3 nextPos = _navAgent.GetNextPathPosition();
		Vector3 direction = (nextPos - GlobalPosition).Normalized();
		direction.Y = 0;
		
		velocity.X = direction.X * ChaseSpeed;
		velocity.Z = direction.Z * ChaseSpeed;
	}

	private void ProcessInvestigate(ref Vector3 velocity, float delta)
	{
		_navAgent.TargetPosition = _investigateTarget;
		_investigateTimer += delta;
		
		if (_navAgent.IsNavigationFinished() || _investigateTimer >= MaxInvestigateTime)
		{
			if (_investigateTimer >= MaxInvestigateTime)
				GD.Print("Enemy investigation timed out. Returning to Patrol.");
			else
				GD.Print("Enemy finished investigation. Returning to Patrol.");
				
			ReturnToPatrol();
			velocity.X = 0;
			velocity.Z = 0;
		}
		else
		{
			Vector3 nextPos = _navAgent.GetNextPathPosition();
			Vector3 direction = (nextPos - GlobalPosition).Normalized();
			direction.Y = 0;
			
			velocity.X = direction.X * ChaseSpeed;
			velocity.Z = direction.Z * ChaseSpeed;
		}
	}

	private void ReturnToPatrol()
	{
		_currentState = State.Patrol;
		_currentWaypointIndex = FindClosestWaypointIndex();
		if (Waypoints != null && Waypoints.Count > 0)
			_navAgent.TargetPosition = Waypoints[_currentWaypointIndex].GlobalPosition;
	}

	private int FindClosestWaypointIndex()
	{
		if (Waypoints == null || Waypoints.Count == 0) return 0;
		
		int closestIdx = 0;
		float minDist = float.MaxValue;
		
		for (int i = 0; i < Waypoints.Count; i++)
		{
			if (Waypoints[i] == null) continue;
			float dist = GlobalPosition.DistanceSquaredTo(Waypoints[i].GlobalPosition);
			if (dist < minDist)
			{
				minDist = dist;
				closestIdx = i;
			}
		}
		return closestIdx;
	}

	private bool IsPlayerVisible()
	{
		if (_targetPlayer == null || _visionArea == null) return false;
		
		var bodies = _visionArea.GetOverlappingBodies();
		bool inArea = false;
		foreach (var body in bodies)
		{
			if (body == _targetPlayer)
			{
				inArea = true;
				break;
			}
		}
		
		if (!inArea) return false;
		
		// Raycast check
		var spaceState = GetWorld3D().DirectSpaceState;
		var query = PhysicsRayQueryParameters3D.Create(
			GlobalPosition + Vector3.Up * 1.5f,
			_targetPlayer.GlobalPosition + Vector3.Up * 0.5f
		);
		query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
		
		// Mask: Layer 1 (World/Player) + Layer 3 (Doors). Exclude Layer 2 (Pickups).
		// 1 = Bit 0, 4 = Bit 2. Sum = 5.
		query.CollisionMask = 5; 
		
		var result = spaceState.IntersectRay(query);
		return result.Count > 0 && result["collider"].Obj == _targetPlayer;
	}

	private void HandleDoorCollisions()
	{
		for (int i = 0; i < GetSlideCollisionCount(); i++)
		{
			KinematicCollision3D collision = GetSlideCollision(i);
			if (collision.GetCollider() is Door door)
			{
				if (door.IsLocked)
				{
					// Stop chasing if we hit a locked door
					if (_currentState == State.Chase && _targetPlayer != null)
					{
						GD.Print("Enemy hit locked door - abandoning chase");
						_investigateTarget = GlobalPosition;
						_targetPlayer.SetAlert(false, this);
						_targetPlayer = null;
						_currentState = State.Investigate;
					}
				}
				else
				{
					// Try to open unlocked door
					door.EnemyInteract();
					
					// Apply gentle push
					if (door is RigidBody3D rb)
					{
						Vector3 pushDir = -collision.GetNormal();
						pushDir.Y = 0;
						rb.ApplyImpulse(pushDir * 0.5f * rb.Mass, collision.GetPosition() - rb.GlobalPosition);
					}
				}
			}
		}
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
				rb.ApplyImpulse(pushDirection * 0.2f * rb.Mass);
			}
		}
	}



	private void AdvanceWaypoint()
	{
		if (Waypoints.Count <= 1) return;
		
		if (MovementType == PatrolType.Loop)
		{
			_currentWaypointIndex = (_currentWaypointIndex + 1) % Waypoints.Count;
		}
		else // PingPong
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

	public void AlertToLocation(Vector3 location)
	{
		// Even if investigating, update the target to the new noise/alert
		if (_currentState == State.Chase) return; // Priority: Chase > Alert
		
		_investigateTarget = location;
		_currentState = State.Investigate;
		_investigateTimer = 0.0f;
		
		GD.Print($"Enemy alerted to location: {location}");
	}

	private void OnBodyEnteredVision(Node3D body)
	{
		if (body is PlayerController)
		{
			CheckVision();
		}
	}

	private void OnBodyExitedVision(Node3D body)
	{
		// Chase timeout handles this
	}

	private void MakeAnimationInPlace(Animation anim, string animName)
	{
		if (anim == null) return;
		
		GD.Print($"EnemyAI: Processing '{animName}' - Tracks: {anim.GetTrackCount()}");
		
		bool foundRoot = false;
		
		for(int i = 0; i < anim.GetTrackCount(); i++)
		{
			string path = anim.TrackGetPath(i).ToString();
			string lower = path.ToLower();
			var type = anim.TrackGetType(i);
			
			// DEBUG: Print all tracks to see what we are dealing with at runtime
			// GD.Print($"  Track {i}: {path} ({type})");
			
			if (lower.Contains("hips") || lower.Contains("root") || lower.Contains("pelvis"))
			{
				if (type == Animation.TrackType.Position3D)
				{
					// Found separate position track - Safe to disable
					anim.TrackSetEnabled(i, false);
					GD.Print($"EnemyAI: DISABLED Root Motion (Pos3D) for '{animName}' on track '{path}'");
					foundRoot = true;
				}
				else
				{
					// Found Hips on a non-Position3D track (likely Rotation, Scale, or Value).
					// Disabling Rotation would break the animation.
					// We only disable explicit translation tracks.
					GD.PushWarning($"EnemyAI: Found Hips on track '{path}' of type '{type}'. NOT disabling (safe).");
				}
			}
		}
		
		if (!foundRoot)
		{
			// Fallback: Disable Track 0 if it looks suspicious? No, too risky.
			GD.Print($"EnemyAI: WARNING - No Hips/Root position track identified for '{animName}'. Teleporting may occur.");
		}
			
		// Also ensure loop if it's Walk/Run/Idle
		string nameLower = animName.ToLower();
		if (nameLower.Contains("walk") || nameLower.Contains("run") || nameLower.Contains("idle"))
		{
			if (anim.LoopMode != Animation.LoopModeEnum.Linear)
			{
				anim.LoopMode = Animation.LoopModeEnum.Linear;
				GD.Print($"EnemyAI: Forced Looping for '{animName}'");
			}
		}
	}

	private void PrintNodeTree(Node node, string indent)
	{
		GD.Print($"{indent}- {node.Name} ({node.GetType().Name})");
		foreach (Node child in node.GetChildren())
		{
			PrintNodeTree(child, indent + "  ");
		}
	}

	private T FindFirstNodeOfType<T>(Node root) where T : Node
	{
		if (root is T t) return t;
		
		foreach (Node child in root.GetChildren())
		{
			T found = FindFirstNodeOfType<T>(child);
			if (found != null) return found;
		}
		return null;
	}
}
