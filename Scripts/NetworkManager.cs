using Godot;
using System.Collections.Generic;

public partial class NetworkManager : Node
{
    public static NetworkManager Instance { get; private set; }
    [Export] public PackedScene PlayerScene;

    private readonly Dictionary<long, (Vector3 pos, string name)> _spawnedPlayers = new();

    public override void _Ready()
    {
        Instance = this;
        if (PlayerScene == null)
        {
            PlayerScene = GD.Load<PackedScene>("res://Prefabs/player.tscn");
        }

        Multiplayer.PeerConnected += OnPeerConnected;
        Multiplayer.PeerDisconnected += OnPeerDisconnected;
        Multiplayer.ConnectedToServer += OnConnectedToServer;
        Multiplayer.ConnectionFailed += OnConnectionFailed;
    }

    private void OnConnectionFailed()
    {
        GD.PrintErr("[NetworkManager] Connection Failed to Host!");
    }

    private async void OnConnectedToServer()
    {
        GD.Print($"[NetworkManager] Connected to Host server! My Peer ID: {Multiplayer.GetUniqueId()}");
        
        Error err = GetTree().ChangeSceneToFile("res://Scenes/StoreInterior.tscn");
        if (err != Error.Ok)
        {
            GD.PrintErr($"[NetworkManager] Failed to change scene to StoreInterior: {err}");
            return;
        }

        // Wait until CurrentScene is actually StoreInterior and is fully ready in tree
        while (GetTree().CurrentScene == null || 
               GetTree().CurrentScene.SceneFilePath != "res://Scenes/StoreInterior.tscn" || 
               !GetTree().CurrentScene.IsInsideTree() || 
               !GetTree().CurrentScene.IsNodeReady())
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        // Additional frames to ensure complete scene initialization
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        string myName = SteamManager.Instance?.GetPersonaName() ?? $"Player {Multiplayer.GetUniqueId()}";
        GD.Print($"[NetworkManager] Client loaded StoreInterior scene! Sending RpcClientReady to Host with name '{myName}'...");
        RpcId(1, nameof(RpcClientReady), myName);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
    private void RpcClientReady(string clientPlayerName)
    {
        long senderId = Multiplayer.GetRemoteSenderId();
        GD.Print($"[NetworkManager] Received RpcClientReady from Client Sender ID: {senderId} ('{clientPlayerName}')");

        if (!Multiplayer.IsServer()) return;

        // 1. Send all currently existing players (including Host) to the new client
        foreach (var kvp in _spawnedPlayers)
        {
            Node existingNode = GetTree().CurrentScene?.GetNodeOrNull(kvp.Key.ToString());
            Vector3 currentPos = (existingNode is CharacterBody3D cb) ? cb.GlobalPosition : kvp.Value.pos;
            string existingName = (existingNode is PlayerController pc && !string.IsNullOrEmpty(pc.PlayerName)) ? pc.PlayerName : kvp.Value.name;
            RpcId(senderId, nameof(RpcSpawnPlayer), kvp.Key, currentPos, existingName);
        }

        // 2. Determine spawn point for the new player
        Vector3 newPlayerSpawnPos = Vector3.Zero;
        Node3D spawnPointsNode = GetTree().CurrentScene?.GetNodeOrNull<Node3D>("SpawnPoints");
        if (spawnPointsNode != null && spawnPointsNode.GetChildCount() > 0)
        {
            int count = spawnPointsNode.GetChildCount();
            Marker3D spawnPoint = spawnPointsNode.GetChild<Marker3D>((int)senderId % count);
            newPlayerSpawnPos = spawnPoint.GlobalPosition;
        }

        // 3. Spawn the new player for EVERYONE (host + all connected clients including senderId)
        Rpc(nameof(RpcSpawnPlayer), senderId, newPlayerSpawnPos, clientPlayerName);

        // 4. Sync current Groomba state to new client
        Groomba groomba = GetTree().CurrentScene?.GetNodeOrNull<Groomba>("Groomba") ?? 
                          GetTree().CurrentScene?.GetNodeOrNull<Groomba>("WorldStuff/Groomba");
        if (groomba != null)
        {
            groomba.RpcId(senderId, nameof(PatrolEnemy.RpcSyncPatrolState), (int)groomba.PatrolState);
        }

        // 5. Sync match state to player
        GameManager.Instance?.SyncStateToPlayer(senderId);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    public void RpcSpawnPlayer(long peerId, Vector3 spawnPosition, string playerName)
    {
        if (GetTree().CurrentScene == null)
        {
            GD.PrintErr($"[NetworkManager] Cannot spawn Player {peerId}: CurrentScene is null!");
            return;
        }

        Node existing = GetTree().CurrentScene.GetNodeOrNull(peerId.ToString());
        if (existing != null)
        {
            GD.Print($"[NetworkManager] Player {peerId} already exists in scene. Updating name and position.");
            if (existing is PlayerController existingPc)
            {
                existingPc.PlayerName = playerName;
            }
            return;
        }

        GD.Print($"[NetworkManager] ---> Spawning Player ID: {peerId} ('{playerName}') at {spawnPosition} (Local Peer: {Multiplayer.GetUniqueId()}) <---");
        CharacterBody3D player = (CharacterBody3D)PlayerScene.Instantiate();
        player.Name = peerId.ToString();
        player.SetMultiplayerAuthority((int)peerId);
        player.Position = spawnPosition;

        if (player is PlayerController pc)
        {
            pc.PlayerName = playerName;
            pc.SyncPosition = spawnPosition;
        }

        GetTree().CurrentScene.AddChild(player);

        if (Multiplayer.IsServer())
        {
            _spawnedPlayers[peerId] = (spawnPosition, playerName);
        }

        GD.Print($"[NetworkManager] Added Player {peerId} to CurrentScene successfully!");
    }

    private void OnPeerDisconnected(long id)
    {
        GD.Print($"[NetworkManager] Peer Disconnected: {id}");
        if (Multiplayer.IsServer())
        {
            _spawnedPlayers.Remove(id);
        }
        Node player = GetTree().CurrentScene?.GetNodeOrNull(id.ToString());
        if (player != null)
        {
            player.QueueFree();
            GD.Print($"[NetworkManager] Removed Player node {id} on disconnect.");
        }
    }

    private void OnPeerConnected(long id)
    {
        GD.Print($"[NetworkManager] Peer Connected Event Fired! Peer ID: {id}. Waiting for RpcClientReady...");
    }

    public async void LoadLevel(string scenePath)
    {
        if (Multiplayer.IsServer())
        {
            _spawnedPlayers.Clear();
            GetTree().ChangeSceneToFile(scenePath);
            
            while (GetTree().CurrentScene == null || 
                   GetTree().CurrentScene.SceneFilePath != scenePath || 
                   !GetTree().CurrentScene.IsInsideTree() || 
                   !GetTree().CurrentScene.IsNodeReady())
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            Vector3 hostSpawnPos = Vector3.Zero;
            Node3D spawnPointsNode = GetTree().CurrentScene.GetNodeOrNull<Node3D>("SpawnPoints");
            if (spawnPointsNode != null && spawnPointsNode.GetChildCount() > 0)
            {
                int count = spawnPointsNode.GetChildCount();
                Marker3D spawnPoint = spawnPointsNode.GetChild<Marker3D>((int)Multiplayer.GetUniqueId() % count);
                hostSpawnPos = spawnPoint.GlobalPosition;
            }

            string hostName = SteamManager.Instance?.GetPersonaName() ?? $"Player {Multiplayer.GetUniqueId()}";
            RpcSpawnPlayer(Multiplayer.GetUniqueId(), hostSpawnPos, hostName);
        }
    }
}