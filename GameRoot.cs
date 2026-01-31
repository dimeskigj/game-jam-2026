using Godot;

public partial class GameRoot : Node3D
{
	public override void _Ready()
	{
		var net = GetNodeOrNull<NetworkManager>("/root/NetworkManager");
		if (net != null)
		{
			// If we are a client and have a target IP, connect now.
			if (!Multiplayer.IsServer() && !string.IsNullOrEmpty(net.TargetIP))
			{
				GD.Print($"Attempting connection to {net.TargetIP}...");
				net.StartClient(net.TargetIP);
				net.TargetIP = ""; // Clear it so we don't reconnect on reload
				
				// Optional: Add a timeout check
				GetTree().CreateTimer(5.0).Timeout += () => {
					if (GetTree().GetNodesInGroup("Players").Count == 0) {
						GD.PrintErr("Connection timeout or failed to spawn player!");
					}
				};
			}
		}
	}
}
