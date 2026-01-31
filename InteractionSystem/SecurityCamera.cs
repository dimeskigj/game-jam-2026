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
	private Node3D _rotationPivot;
	private MeshInstance3D _visionConeMesh;

	private float _time = 0.0f;
	private float _initialRotationY;
	
	private Godot.Collections.Array<Node> _cachedEnemies;
	private float _alertCooldown = 0.0f;

	private PlayerController _detectedPlayer;
	private bool _isAlerted = false;

	public override void _Ready()
	{
		_initialRotationY = Rotation.Y; 
		
		_cachedEnemies = GetTree().GetNodesInGroup("Enemies");
		
		// Setup Nodes
		_viewCone = GetNodeOrNull<SpotLight3D>("ViewCone");
		_detectionArea = GetNodeOrNull<Area3D>("DetectionArea");
		_losRay = GetNodeOrNull<RayCast3D>("LOSRay");
		_visionConeMesh = GetNodeOrNull<MeshInstance3D>("VisionConeMesh"); 
		
		if (_detectionArea != null)
		{
			_detectionArea.BodyEntered += OnBodyEntered;
			_detectionArea.BodyExited += OnBodyExited;
		}

		// FBX Handling: Find "group1" as pivot
		Node foundNode = FindChild("group1", true, false); 
		if (foundNode is Node3D n3d)
		{
			_rotationPivot = n3d;
			_initialRotationY = _rotationPivot.Rotation.Y; 
			GD.Print("SecurityCamera: Found pivot 'group1'. Using global rotation sync.");
		}
		else
		{
			_rotationPivot = this; 
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
		
		// Rotate Visual Model (Pivot)
		if (_rotationPivot != null)
		{
			float currentY = Mathf.RadToDeg(_initialRotationY) + angleOffset;
			Vector3 curRot = _rotationPivot.RotationDegrees;
			_rotationPivot.RotationDegrees = new Vector3(curRot.X, currentY, curRot.Z);
			
			// SYNC children by forcing their Global Rotation to match the pivot
			Vector3 pivotGlobalRot = _rotationPivot.GlobalRotationDegrees;
			
			if (_detectionArea != null) _detectionArea.GlobalRotationDegrees = pivotGlobalRot;
			if (_viewCone != null)      _viewCone.GlobalRotationDegrees = pivotGlobalRot;
			if (_losRay != null)        _losRay.GlobalRotationDegrees = pivotGlobalRot;
			
			// VisionConeMesh correction
			if (_visionConeMesh != null)
			{
				_visionConeMesh.GlobalRotationDegrees = pivotGlobalRot + new Vector3(-90, 0, 0);
			}
		}
		
		if (_alertCooldown > 0) _alertCooldown -= (float)delta;

		// 2. Detection Logic
		if (_detectedPlayer != null)
		{
			CheckDetection();
		}
	}

	private void CheckDetection()
	{
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
		Vector3 toPlayer = (_detectedPlayer.GlobalPosition + Vector3.Up) - GlobalPosition;
		Vector3 forward = -GlobalTransform.Basis.Z; 
		float angleToPlayer = Mathf.RadToDeg(forward.AngleTo(toPlayer));
		
		if (angleToPlayer > FOV / 2.0f)
		{
			if (_isAlerted)
			{
				_isAlerted = false;
				_detectedPlayer.SetAlert(false, this);
			}
			return; 
		}

		// 3. Line of Sight Check
		var spaceState = GetWorld3D().DirectSpaceState;
		
		// Use pivot's forward logic if possible, otherwise camera base.
		Vector3 fromPos = GlobalPosition;
		if (_rotationPivot != null) fromPos = _rotationPivot.GlobalPosition;
		
		fromPos += -GlobalTransform.Basis.Z * 0.2f; // Slight offset
		
		Vector3[] testPoints = new Vector3[]
		{
			_detectedPlayer.GlobalPosition + Vector3.Up * 0.5f,
			_detectedPlayer.GlobalPosition + Vector3.Up * 1.0f,
			_detectedPlayer.GlobalPosition + Vector3.Up * 0.2f 
		};
		
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
					break; 
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
			_detectedPlayer.SetAlert(true, this);
			
			if (_alertCooldown <= 0)
			{
				TriggerAlert();
				_alertCooldown = 0.5f; 
			}
		}
		else
		{
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
