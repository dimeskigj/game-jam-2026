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
		_navRegion = GetNodeOrNull<NavigationRegion3D>("../RuntimeNavRegion");
		
		if (_navRegion == null)
		{
			// Try root fallback
			foreach(Node child in GetTree().CurrentScene.GetChildren())
			{
				if (child is NavigationRegion3D nr) { _navRegion = nr; break; }
			}
		}

		if (_navRegion == null)
		{
			GD.PrintErr("Navigation: No NavigationRegion3D found! Cannot bake.");
			return;
		}

		GD.Print("Navigation: Starting ROBUST runtime bake...");
		
		// 1. Configure the Navigation Mesh Resource
		var navMesh = new NavigationMesh();
		navMesh.CellSize = 0.25f;
		navMesh.AgentRadius = 1.0f;
		navMesh.AgentHeight = 1.0f; // Friendly height
		navMesh.AgentMaxClimb = 0.5f;
		navMesh.AgentMaxSlope = 50.0f;
		
		// STRATEGY: Parse STATIC COLLIDERS only.
		// Walls/Floors = StaticBody3D (Layer 1).
		// Doors = RigidBody3D (Layer 3 or 4) -> Ignored by 'StaticColliders' type.
		// Pickups = RigidBody3D -> Ignored.
		// This ensures the navmesh is walkable THROUGH doors.
		
		navMesh.GeometryParsedGeometryType = NavigationMesh.ParsedGeometryType.StaticColliders;
		navMesh.GeometryCollisionMask = 1; // Only parse Layer 1 (World)
		
		// 2. Create Source Geometry Data
		var sourceGeometry = new NavigationMeshSourceGeometryData3D();
		
		// 3. Parse the entire Current Scene
		Node rootNode = GetTree().CurrentScene;
		NavigationServer3D.ParseSourceGeometryData(navMesh, sourceGeometry, rootNode);
		
		// SAFETY CHECK: If we found no geometry, maybe the world uses Meshes but no collision?
		// Try parsing meshes if data is empty? (Hard to check count on SourceGeometry in C# easily without internals)
		// Instead, we just trust the Static Colliders strategy first.
		
		GD.Print("Navigation: Geometry Parsed (Static Colliders). Baking...");
		
		// 4. Bake
		NavigationServer3D.BakeFromSourceGeometryData(navMesh, sourceGeometry);
		
		if (navMesh.GetPolygonCount() == 0)
		{
			GD.PrintErr("Navigation: Bake yielded 0 polygons using StaticColliders. Please ensure floors/walls have StaticBody3D with collision shapes.");
		}
		
		// 5. Assign to region
		_navRegion.NavigationMesh = navMesh;
		
		GD.Print("========================================");
		GD.Print("Navigation: BAKING COMPLETE!");
		GD.Print("Navigation Mesh Polygons: " + navMesh.GetPolygonCount());
		GD.Print("========================================");
	}
}
