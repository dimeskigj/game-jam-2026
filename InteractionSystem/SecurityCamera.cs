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
	
	// private Godot.Collections.Array<Node> _cachedEnemies;
	private float _alertCooldown = 0.0f;

	private PlayerController _detectedPlayer;
	private bool _isAlerted = false;

	public override void _Ready()
	{
		_initialRotationY = Rotation.Y;
		if (CameraPivot != null) _initialRotationY = CameraPivot.Rotation.Y;

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

		// 3. Find Visual Nodes (Light/Mesh)
		Node searchRoot = CameraPivot != null ? CameraPivot : this;
		
		_viewCone = searchRoot.GetNodeOrNull<SpotLight3D>("ViewCone") ?? GetNodeOrNull<SpotLight3D>("ViewCone");
		_visionConeMesh = searchRoot.GetNodeOrNull<MeshInstance3D>("VisionConeMesh") ?? GetNodeOrNull<MeshInstance3D>("VisionConeMesh"); 
		
		// 4. Reparent for Alignment
		if (CameraPivot != null)
		{
			_initialRotationY = CameraPivot.Rotation.Y;
			ReparentToPivot(_viewCone);
			ReparentToPivot(_visionConeMesh);
		}
		
		// 5. Setup Visual Cone (Transparency)
		if (_visionConeMesh != null)
		{
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
				var newMat = new StandardMaterial3D();
				newMat.AlbedoColor = new Color(1, 0, 0, 0.3f);
				newMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
				newMat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
				_visionConeMesh.MaterialOverride = newMat;
			}
		}

		if (_viewCone != null)
		{
			_viewCone.SpotAngle = FOV / 2.0f;
			_viewCone.SpotRange = Range;
		}
		
		// Explicitly disable any physics processing just in case logic lingers
		SetPhysicsProcess(true); 
	}

	private void ReparentToPivot(Node3D node)
	{
		if (node == null || CameraPivot == null) return;
		if (node.GetParent() == CameraPivot) return;

		Transform3D global = node.GlobalTransform;
		node.GetParent()?.RemoveChild(node);
		CameraPivot.AddChild(node);
		node.GlobalTransform = global;
	}

	public override void _PhysicsProcess(double delta)
	{
		// 1. Sweep Movement (Visual Only)
		_time += (float)delta * RotationSpeed;
		float angleOffset = Mathf.Sin(_time) * SweepAngle;
		
		float baseRot = Mathf.RadToDeg(_initialRotationY);
		float targetY = baseRot + angleOffset;

		// Apply to Pivot
		if (CameraPivot != null)
		{
			Vector3 rot = CameraPivot.RotationDegrees;
			CameraPivot.RotationDegrees = new Vector3(rot.X, targetY, rot.Z);
		}
		
		// Sync Model
		if (ModelGeometry != null && ModelGeometry != CameraPivot && !IsChildOf(ModelGeometry, CameraPivot))
		{
			Vector3 rot = ModelGeometry.RotationDegrees;
			ModelGeometry.RotationDegrees = new Vector3(rot.X, targetY, rot.Z);
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
	
	// Deprecated Detection Methods Removed to ensure "Just Visual" state.
}
