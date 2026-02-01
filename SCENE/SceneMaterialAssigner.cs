using Godot;
using System;

[Tool]
public partial class SceneMaterialAssigner : Node3D
{
	// Keeping these purely to avoid breaking scene references, 
	// but we won't strictly enforce them if they aren't needed.
	[Export] public Texture2D ConcreteNormal;
	[Export] public Texture2D FacadeNormal;
	[Export] public Texture2D KruschevNormal;

	public override void _Ready()
	{
		CallDeferred(MethodName.ApplyProcessing);
	}

	private void ApplyProcessing()
	{
		// Recursively process all children
		foreach (Node child in GetChildren())
		{
			ProcessNodeRecursive(child);
		}
	}

	private void ProcessNodeRecursive(Node node)
	{
		if (node is MeshInstance3D meshInstance)
		{
			// 1. Physics Collision (CRITICAL: Always apply)
			if (!HasStaticBodyChild(meshInstance))
			{
				try { meshInstance.CreateTrimeshCollision(); } catch {}
			}

			// 2. Visuals: We do NOT override materials anymore.
			// This preserves the exact look from the Editor (photo textures)
			// which the user requested.
			// The normal map injection logic has been removed to ensure stability.
		}

		foreach (Node child in node.GetChildren())
		{
			ProcessNodeRecursive(child);
		}
	}

	private bool HasStaticBodyChild(Node node)
	{
		foreach (Node child in node.GetChildren())
		{
			if (child is StaticBody3D) return true;
		}
		return false;
	}
}
