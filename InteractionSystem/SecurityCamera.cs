using Godot;
using System.Collections.Generic;

public partial class SecurityCamera : Node3D
{
	[Export] public float RotationSpeed = 1.0f;
	[Export] public float SweepAngle = 45.0f;
	[Export] public float FOV = 60.0f;
	[Export] public float Range = 10.0f;

	private SpotLight3D _viewCone;
	private Area3D _detectionArea;
	private CollisionShape3D _detectionShape;
	private RayCast3D _losRay;
	private MeshInstance3D _cameraBody;

	private float _time = 0.0f;
	private float _initialRotationY;
	
	// Optimization: Cache enemies list
	private Godot.Collections.Array<Node> _cachedEnemies;
	private float _alertCooldown = 0.0f;

	// Alert Logic
	private PlayerController _detectedPlayer;
	private bool _isAlerted = false;

	public override void _Ready()
	{
		_initialRotationY = Rotation.Y;
		
		_cachedEnemies = GetTree().GetNodesInGroup("Enemies");
		
		// Setup Nodes if not present
		_viewCone = GetNodeOrNull<SpotLight3D>("ViewCone");
		_detectionArea = GetNodeOrNull<Area3D>("DetectionArea");
		_losRay = GetNodeOrNull<RayCast3D>("LOSRay");
		_cameraBody = GetNodeOrNull<GeometryInstance3D>("CameraBody") as MeshInstance3D;
		
		if (_detectionArea != null)
		{
			_detectionArea.BodyEntered += OnBodyEntered;
			_detectionArea.BodyExited += OnBodyExited;
		}

		if (_viewCone != null)
		{
			_viewCone.SpotAngle = FOV / 2.0f;
			_viewCone.SpotRange = Range;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		// 1. Sweep Movement
		_time += (float)delta * RotationSpeed;
		float angleOffset = Mathf.Sin(_time) * SweepAngle;
		RotationDegrees = new Vector3(RotationDegrees.X, Mathf.RadToDeg(_initialRotationY) + angleOffset, RotationDegrees.Z);

		// Keep checking cache occasionally? No, assume static for jam.
		if (_alertCooldown > 0) _alertCooldown -= (float)delta;

		// 2. Detection Logic
		if (_detectedPlayer != null)
		{
			CheckDetection();
		}
	}

	private void CheckDetection()
	{
		// 1. Invisibility Check
		if (_detectedPlayer.CurrentMaskEffect == MaskEffect.Invisibility)
		{
			if (_isAlerted)
			{
				_detectedPlayer.SetAlert(false, this);
				_isAlerted = false;
			}
			return;
		}
		
		// 2. FOV / Angle Check
		// Ensure player is actually IN FRONT of the camera and within the cone angle
		Vector3 toPlayer = (_detectedPlayer.GlobalPosition + Vector3.Up) - GlobalPosition;
		Vector3 forward = -GlobalTransform.Basis.Z; 
		
		float angleToPlayer = Mathf.RadToDeg(forward.AngleTo(toPlayer));
		
		// If angle is strictly outside FOV (half angle), we don't see them
		if (angleToPlayer > FOV / 2.0f)
		{
			// Player is in the box, but outside the cone angle (side of view)
			if (_isAlerted)
			{
				_isAlerted = false;
				_detectedPlayer.SetAlert(false, this);
			}
			return; 
		}

		// 3. Line of Sight Check
		var spaceState = GetWorld3D().DirectSpaceState;
		
		// Move start point forward but reduce offset to 0.2 to be safe
		Vector3 fromPos = GlobalPosition + forward * 0.2f; 
		
		// Check multiple points on player to ensure we don't miss over head/under feet
		Vector3[] testPoints = new Vector3[]
		{
			_detectedPlayer.GlobalPosition + Vector3.Up * 0.5f, // Center?
			_detectedPlayer.GlobalPosition + Vector3.Up * 1.0f, // Head?
			_detectedPlayer.GlobalPosition + Vector3.Up * 0.2f  // Feet?
		};
		
		// Build exclusions once
		var exclusions = new Godot.Collections.Array<Rid>();
		if (_cachedEnemies != null)
		{
			foreach (var enemyNode in _cachedEnemies)
			{
				if (enemyNode is CollisionObject3D colObj) exclusions.Add(colObj.GetRid());
			}
		}

		bool seen = false;
		string blockerDebug = "";

		foreach(Vector3 targetPos in testPoints)
		{
			var query = PhysicsRayQueryParameters3D.Create(fromPos, targetPos);
			query.Exclude = exclusions;
			var result = spaceState.IntersectRay(query);
			
			if (result.Count > 0)
			{
				Node3D collider = result["collider"].Obj as Node3D;
				if (collider == _detectedPlayer)
				{
					seen = true;
					break; // Saw them!
				}
				else
				{
					blockerDebug = collider.Name;
				}
			}
		}

		if (seen)
		{
			if (!_isAlerted)
			{
				GD.Print("Camera: Line of Sight Confirmed! Alerting.");
				_isAlerted = true;
			}
			
			// CONTINUOUS ALERT UPDATE
			_detectedPlayer.SetAlert(true, this);
			
			if (_alertCooldown <= 0)
			{
				TriggerAlert();
				_alertCooldown = 0.5f; 
			}
		}
		else
		{
			// Only print blocker if we expected to see them (i.e. angle is good, but blocked)
			if (blockerDebug != "" && !_isAlerted)
			{
				GD.Print($"Camera Sight Blocked by: {blockerDebug}");
			}
			
			if (_isAlerted)
			{
				_isAlerted = false;
				_detectedPlayer.SetAlert(false, this);
			}
		}
	}


	private void TriggerAlert()
	{
		// _detectedPlayer.SetAlert(true, this); // Handled in CheckDetection now
		
		if (_cachedEnemies == null) return;

		foreach(var node in _cachedEnemies)
		{
			if (node is EnemyAI enemy)
			{
				if (enemy.GlobalPosition.DistanceTo(GlobalPosition) < 30.0f)
				{
					enemy.AlertToLocation(_detectedPlayer.GlobalPosition);
				}
			}
		}
	}

	private void OnBodyEntered(Node3D body)
	{
		if (body is PlayerController player)
		{
			GD.Print("Camera: Player entered detection area.");
			_detectedPlayer = player;
		}
	}

	private void OnBodyExited(Node3D body)
	{
		if (body is PlayerController player && player == _detectedPlayer)
		{
			GD.Print("Camera: Player exited detection area.");
			_detectedPlayer.SetAlert(false, this);
			_detectedPlayer = null;
			_isAlerted = false;
		}
	}
}
