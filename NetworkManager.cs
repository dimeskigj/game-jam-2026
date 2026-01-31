using Godot;
using System;
using System.Linq;

public partial class NetworkManager : Node
{
	[Export] public int Port = 10567;
	[Export] public int MaxPlayers = 32;

	public string PlayerName = "Player";
	public Color PlayerColor = Colors.White;
	public string TargetIP = "";

	public override void _Ready()
	{
		Multiplayer.PeerConnected += (id) => GD.Print($"Multiplayer signal: PeerConnected {id}");
		Multiplayer.PeerDisconnected += (id) => GD.Print($"Multiplayer signal: PeerDisconnected {id}");
		Multiplayer.ConnectionFailed += () => GD.Print("Multiplayer signal: ConnectionFailed");
		Multiplayer.ConnectedToServer += () => GD.Print("Multiplayer signal: ConnectedToServer");

		Multiplayer.PeerConnected += OnPeerConnected;
		Multiplayer.PeerDisconnected += OnPeerDisconnected;

		// Check for headless/server flag
		if (DisplayServer.GetName() == "headless" || OS.GetCmdlineArgs().Any(arg => arg == "--server"))
		{
			GD.Print("Starting dedicated server...");
			StartServer();
			// Dedicated server doesn't need a menu, load the game map immediately.
			// We use CallDeferred to ensure the scene tree is ready to switch.
			CallDeferred(nameof(LoadGameMap));
		}
	}

	private void LoadGameMap()
	{
		GetTree().ChangeSceneToFile("res://node_3d.tscn");
		GD.Print("Loaded Game Map");
	}

	[Export]
	public PackedScene PlayerScene = GD.Load<PackedScene>("res://Player.tscn");

	private void OnPeerConnected(long id)
	{
		if (Multiplayer.IsServer())
		{
			GD.Print($"Peer connected: {id}");
			// Avoid spawning multiple players for the same peer
			var map = GetTree().Root.GetNodeOrNull("Node3D");
			if (map != null && map.HasNode(id.ToString())) return;
			
			CallDeferred(nameof(SpawnPlayer), id);
		}
	}

	private void SpawnPlayer(long id)
	{
		var map = GetTree().Root.GetNodeOrNull("Node3D");
		if (map == null)
		{
			GD.PrintErr("Map 'Node3D' not found! Cannot spawn player.");
			return;
		}

		var player = PlayerScene.Instantiate<CharacterBody3D>();
		player.Name = id.ToString();
		player.Position = new Vector3(0, 2, 0); // Spawn above floor
		map.AddChild(player);
		GD.Print($"Spawned player for {id} at {player.Position}");
	}

	private void OnPeerDisconnected(long id)
	{
		GD.Print($"Peer disconnected: {id}");
		var player = GetTree().Root.GetNodeOrNull($"Node3D/{id}");
		if (player != null)
		{
			player.QueueFree();
		}
	}

	public void StartServer()
	{
		var peer = new ENetMultiplayerPeer();
		// Bind to all available interfaces (0.0.0.0) is the default for CreateServer if IP is not specified, 
		// but sometimes explicitly setting the bind address helps.
		// For ENet in Godot 4, CreateServer uses specific syntax if you want to bind to a specific IP, 
		// but defaulting usually binds to wildcard.
		// The issue might be the client connecting to the wrong default.
		var error = peer.CreateServer(Port, MaxPlayers);
		if (error != Error.Ok)
		{
			GD.PrintErr("Failed to start server: " + error);
			return;
		}
		Multiplayer.MultiplayerPeer = peer;
		GD.Print($"Server started on port {Port}");
	}

	public void StartClient(string address)
	{
		var peer = new ENetMultiplayerPeer();
		var error = peer.CreateClient(address, Port);
		if (error != Error.Ok)
		{
			GD.PrintErr("Failed to connect to server: " + error);
			return;
		}
		Multiplayer.MultiplayerPeer = peer;
		GD.Print($"Connecting to {address}...");
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	public void SetupPlayer(string name, Color color)
	{
		// This will be called on the player object or via a central manager
		GD.Print($"Setting up player: {name} with color {color}");
	}
}
