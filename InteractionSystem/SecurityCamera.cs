using Godot;
using System.Collections.Generic;

public partial class SecurityCamera : Node3D
{
	[Export] public Node3D CameraPivot; // The logical pivot for detection (holds cones, areas)
	[Export] public Node3D ModelGeometry; // The visual mesh to sync (e.g. group1)
	
	[Export] public float RotationSpeed = 1.0f;
	[Export] public float SweepAngle = 45.0f;
	[Export] public float FOV = 60.0f;
	[Export] public float Range = 10.0f;

	private SpotLight3D _viewCone;
	private Area3D _detectionArea;
	private RayCast3D _losRay;
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
		if (CameraPivot != null) _initialRotationY = CameraPivot.Rotation.Y;

		_cachedEnemies = GetTree().GetNodesInGroup("Enemies");
		
		// 1. Establish Pivot
		if (CameraPivot == null)
		{
			CameraPivot = GetNodeOrNull<Node3D>("Pivot");
			if (CameraPivot == null) CameraPivot = GetNodeOrNull<Node3D>("CameraHead");
		}
		
		// 2. Establish Model
		if (ModelGeometry == null)
		{
			Node found = FindChild("group1", true, false);
			if (found is Node3D n3d) ModelGeometry = n3d;
		}

		// 3. Find Detection Nodes
		Node searchRoot = CameraPivot != null ? CameraPivot : this;
		
		// Utility to find node in Pivot OR Self (if not reparented yet)
		_viewCone = searchRoot.GetNodeOrNull<SpotLight3D>("ViewCone") ?? GetNodeOrNull<SpotLight3D>("ViewCone");
		_detectionArea = searchRoot.GetNodeOrNull<Area3D>("DetectionArea") ?? GetNodeOrNull<Area3D>("DetectionArea");
		_losRay = searchRoot.GetNodeOrNull<RayCast3D>("LOSRay") ?? GetNodeOrNull<RayCast3D>("LOSRay");
		_visionConeMesh = searchRoot.GetNodeOrNull<MeshInstance3D>("VisionConeMesh") ?? GetNodeOrNull<MeshInstance3D>("VisionConeMesh"); 
		
		// 4. Reparent and Align
		if (CameraPivot != null)
		{
			_initialRotationY = CameraPivot.Rotation.Y;
			// Snap to Pivot (Position 0, Rotation 0) to ensure alignment
			ReparentToPivot(_viewCone);
			ReparentToPivot(_detectionArea);
			ReparentToPivot(_losRay);
			ReparentToPivot(_visionConeMesh);
		}
		else
		{
			// If no Pivot exists, we treat 'this' as the pivot essentially, 
			// but we can't rotate 'this' if it's the root of the scene (usually static).
			// We'll warn properly.
			GD.PrintErr("SecurityCamera: No Pivot Node found! Create a Node3D named 'Pivot' and assign it or use the Inspector."); 
		}
		
		// 5. Fix Vision Cone Visibility (Double Sided)
		if (_visionConeMesh != null)
		{
			// Try to set Cull Mode to Disabled so it is visible from inside
			var mat = _visionConeMesh.GetActiveMaterial(0) as StandardMaterial3D;
			if (mat == null && _visionConeMesh.Mesh is PrimitiveMesh primMesh && primMesh.Material is StandardMaterial3D primMat)
			{
				mat = primMat;
			}
			
			if (mat != null)
			{
				mat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
			}
			else
			{
				// Create a new material if none exists/accessible easily (fallback)
				var newMat = new StandardMaterial3D();
				newMat.AlbedoColor = new Color(1, 0, 0, 0.3f);
				newMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
				newMat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
				_visionConeMesh.MaterialOverride = newMat;
			}
		}

		if (_detectionArea != null)
		{
			if (!_detectionArea.IsConnected(Area3D.SignalName.BodyEntered, new Callable(this, MethodName.OnBodyEntered)))
				_detectionArea.BodyEntered += OnBodyEntered;
			if (!_detectionArea.IsConnected(Area3D.SignalName.BodyExited, new Callable(this, MethodName.OnBodyExited)))
				_detectionArea.BodyExited += OnBodyExited;
		}

		if (_viewCone != null)
		{
			_viewCone.SpotAngle = FOV / 2.0f;
			_viewCone.SpotRange = Range;
		}
	}

	private void ReparentToPivot(Node3D node)
	{
		if (node == null || CameraPivot == null) return;
		
		// If already child, do nothing (we trust the editor position)
		if (node.GetParent() == CameraPivot) return;

		// Move to pivot while keeping Global Position/Rotation
		Transform3D global = node.GlobalTransform;
		node.GetParent()?.RemoveChild(node);
		CameraPivot.AddChild(node);
		node.GlobalTransform = global;
	}

	public override void _PhysicsProcess(double delta)
	{
		// 1. Sweep Movement
		_time += (float)delta * RotationSpeed;
		float angleOffset = Mathf.Sin(_time) * SweepAngle;
		
		float baseRot = Mathf.RadToDeg(_initialRotationY);
		float targetY = baseRot + angleOffset;

		// Apply to Pivot (Detection + Logic)
		if (CameraPivot != null)
		{
			Vector3 rot = CameraPivot.RotationDegrees;
			// Only rotate Y
			CameraPivot.RotationDegrees = new Vector3(rot.X, targetY, rot.Z);
		}
		
		// Sync Model (Visual Mesh) - Only if it's NOT a child of Pivot (to avoid double rotation)
		// If ModelGeometry is 'group1' and Pivot is a sibling node, we rotate both?
		// Better: If ModelGeometry is separate, rotate it too.
		if (ModelGeometry != null && ModelGeometry != CameraPivot && !IsChildOf(ModelGeometry, CameraPivot))
		{
			Vector3 rot = ModelGeometry.RotationDegrees;
			ModelGeometry.RotationDegrees = new Vector3(rot.X, targetY, rot.Z);
		}
		
		if (_alertCooldown > 0) _alertCooldown -= (float)delta;
		
		if (_detectedPlayer != null)
		{
			CheckDetection(delta);
		}
	}
	
	private bool IsChildOf(Node node, Node possibleParent)
	{
		Node p = node.GetParent();
		while (p != null)
		{
			if (p == possibleParent) return true;
			p = p.GetParent();
		}
		return false;
	}
	
	[Export] public float AlertLingerDuration = 0.5f;
	private float _lastSeenTimer = 0.0f;

	private void CheckDetection(double delta)
	{
		if (_detectedPlayer == null) return;
		
		bool currentlySeen = false;

		// 1. Invisibility Check
		if (_detectedPlayer.CurrentMaskEffect != MaskEffect.Invisibility)
		{
			// 2. FOV Check 
			// Determine the "Forward" direction of the camera.
			// If we have a ViewCone (SpotLight), its -Z is the direction of the light.
			Vector3 forwardDirection = Vector3.Forward; // Default
			Vector3 cameraOrigin = GlobalPosition;
			
			if (_viewCone != null)
			{
				forwardDirection = -_viewCone.GlobalTransform.Basis.Z;
				cameraOrigin = _viewCone.GlobalPosition;
			}
			else if (CameraPivot != null)
			{
				forwardDirection = -CameraPivot.GlobalTransform.Basis.Z;
				cameraOrigin = CameraPivot.GlobalPosition;
			}
			else
			{
				forwardDirection = -GlobalTransform.Basis.Z;
			}

			Vector3 toPlayer = (_detectedPlayer.GlobalPosition + Vector3.Up * 0.5f) - cameraOrigin;
			
			// AngleTo is always positive (0-180)
			float angleToPlayer = Mathf.RadToDeg(forwardDirection.AngleTo(toPlayer));
			
			// DEBUG
			//GD.Print($"Camera Angle: {angleToPlayer:F1}");

			if (angleToPlayer <= FOV / 2.0f)
			{
				// 3. Line of Sight Check
				var spaceState = GetWorld3D().DirectSpaceState;
				Vector3 fromPos = cameraOrigin; 
				fromPos += forwardDirection * 0.2f; 
				
				Vector3[] testPoints = new Vector3[]
				{
					_detectedPlayer.GlobalPosition + Vector3.Up * 1.5f, // Head
					_detectedPlayer.GlobalPosition + Vector3.Up * 0.5f  // Waist
				};
				
				var exclusions = new Godot.Collections.Array<Rid>();
				if (_cachedEnemies != null)
				{
					foreach (var enemyNode in _cachedEnemies)
					{
						if (enemyNode is CollisionObject3D colObj) exclusions.Add(colObj.GetRid());
					}
				}

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
							currentlySeen = true;
							break; 
						}
					}
				}
			}
		}

		if (currentlySeen)
		{
			// Reset linger timer
			_lastSeenTimer = 0.0f;
			
			if (!_isAlerted)
			{
				GD.Print("Camera: Line of Sight Confirmed! Alerting.");
				_isAlerted = true;
				_detectedPlayer.SetAlert(true, this);
			}
			else
			{
				// Ensure it's true (refresh)
				_detectedPlayer.SetAlert(true, this);
			}
			
			if (_alertCooldown <= 0)
			{
				TriggerAlert();
				_alertCooldown = 0.5f; 
			}
		}
		else
		{
			// Not seen this frame
			if (_isAlerted)
			{
				_lastSeenTimer += (float)delta;
				if (_lastSeenTimer > AlertLingerDuration)
				{
					// Time is up, clear alert
					ClearAlert();
				}
			}
		}
	}


	
	private void ClearAlert()
	{
		if (_isAlerted)
		{
			_isAlerted = false;
			if (_detectedPlayer != null)
				_detectedPlayer.SetAlert(false, this);
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
