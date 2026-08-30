using Godot;

public partial class NetworkManager : Node
{
    static public NetworkManager Instance;
    [Export] PackedScene PlayerScene;

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

    private void SpawnPlayer(long id)
    {
        GD.Print($"[NetworkManager] ---> Spawning Player ID: {id} (IsServer: {Multiplayer.IsServer()}) <---");
        Node3D spawnPointsNode = GetTree().CurrentScene.GetNodeOrNull<Node3D>("SpawnPoints");
        CharacterBody3D player = (CharacterBody3D)PlayerScene.Instantiate();
        player.Name = id.ToString();

        player.SetMultiplayerAuthority((int)id);

        GetTree().CurrentScene.AddChild(player);

        if (spawnPointsNode != null && spawnPointsNode.GetChildCount() > 0)
        {
            int spawnPointsCount = spawnPointsNode.GetChildCount();
            Marker3D spawnPoint = spawnPointsNode.GetChild<Marker3D>((int)id % spawnPointsCount);
            player.GlobalPosition = spawnPoint.GlobalPosition;
            GD.Print($"[NetworkManager] Positioned Player {id} at SpawnPoint position: {spawnPoint.GlobalPosition}");
        }

        GD.Print($"[NetworkManager] Added Player {id} to CurrentScene successfully!");
    }

    private void OnConnectionFailed()
    {
        GD.PrintErr("[NetworkManager] Connection Failed to Host!");
    }

    private async void OnConnectedToServer()
    {
        GD.Print($"[NetworkManager] Connected to Host server! My Peer ID: {Multiplayer.GetUniqueId()}");
        GetTree().ChangeSceneToFile("res://Scenes/world.tscn");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        GD.Print("[NetworkManager] Client loaded world scene! Sending RpcClientReady to Host...");
        Rpc(nameof(RpcClientReady));
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
    private void RpcClientReady()
    {
        long senderId = Multiplayer.GetRemoteSenderId();
        GD.Print($"[NetworkManager] Received RpcClientReady from Client Sender ID: {senderId}");

        if (Multiplayer.IsServer())
        {
            SpawnPlayer(senderId);
            GameManager.Instance?.SyncStateToPlayer(senderId);
        }
    }

    private void OnPeerDisconnected(long id)
    {
        GD.Print($"[NetworkManager] Peer Disconnected: {id}");
    }

    private void OnPeerConnected(long id)
    {
        GD.Print($"[NetworkManager] Peer Connected Event Fired! Peer ID: {id}. Waiting for RpcClientReady...");
    }

    public async void LoadLevel(string scenePath)
    {
        if (Multiplayer.IsServer())
        {
            GetTree().ChangeSceneToFile(scenePath);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            SpawnPlayer(Multiplayer.GetUniqueId());

            foreach (int peerId in Multiplayer.GetPeers())
            {
                SpawnPlayer(peerId);
            }
        }
    }
}