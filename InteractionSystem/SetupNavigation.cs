using Godot;
using System.Collections.Generic;

public partial class SetupNavigation : Node
{
	[Export] public bool BakeOnStartup = true;
	[Export] public float CellSize = 0.25f;
	[Export] public float AgentRadius = 0.5f;

	private NavigationRegion3D _navRegion;

	public override void _Ready()
	{
		if (BakeOnStartup)
		{
			CallDeferred(MethodName.SetupAndBake);
		}
	}

	private void SetupAndBake()
	{
		SceneTree tree = GetTree();
		Node root = tree.CurrentScene;
		
		_navRegion = root.GetNodeOrNull<NavigationRegion3D>("NavigationRegion3D");
		if (_navRegion == null)
		{
			// Fallback: Create one if not in scene
			_navRegion = new NavigationRegion3D();
			_navRegion.Name = "NavigationRegion3D";
			root.AddChild(_navRegion);
			GD.Print("SetupNavigation: Created new NavigationRegion3D.");
		}
		else
		{
			GD.Print("SetupNavigation: Found existing NavigationRegion3D.");
		}

		if (_navRegion.NavigationMesh == null)
		{
			var navMesh = new NavigationMesh();
			navMesh.CellSize = CellSize;
			navMesh.AgentRadius = AgentRadius;
			navMesh.AgentHeight = 1.8f;
			navMesh.AgentMaxClimb = 0.5f;
			navMesh.GeometryParsedGeometryType = NavigationMesh.ParsedGeometryType.MeshInstances; 
			// Use SourceGeometryModeEnum.GroupsWithChildren if we reparent things under it (Default)
			// Or RootNode if we prefer. 
			// If we put static objects UNDER the Region in the scene, the Default (RootNode of the Region?) NO.
			// Default 'SourceGeometryMode' for Region is: "Root Node Children" (implied).
			// Let's explicitly set it to parse its children.
			
			// Actually, let's stick to Parsing STATIC COLLIDERS for robustness with CSG if they bake collisions
			// But MeshInstances is better for CSG visual baking.
			
			_navRegion.NavigationMesh = navMesh;
		}

		_navRegion.BakeFinished += OnBakeFinished;
		GD.Print("SetupNavigation: Baking NavigationMesh...");
		_navRegion.BakeNavigationMesh(true);
	}
	
	private void OnBakeFinished()
	{
		GD.Print("SetupNavigation: Navigation Mesh Baked Successfully!");
	}
}
