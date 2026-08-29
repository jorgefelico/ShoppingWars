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
        Node3D spawnPointsNode = GetTree().CurrentScene.GetNodeOrNull<Node3D>("SpawnPoints");
        CharacterBody3D player = (CharacterBody3D)PlayerScene.Instantiate();
        player.Name = id.ToString();

        player.SetMultiplayerAuthority((int)id);

        if (spawnPointsNode != null && spawnPointsNode.GetChildCount() > 0)
        {
            int spawnPointsCount = spawnPointsNode.GetChildCount();
            Marker3D spawnPoint = spawnPointsNode.GetChild<Marker3D>((int)id % spawnPointsCount);
            player.GlobalPosition = spawnPoint.GlobalPosition;
        }

        GetTree().CurrentScene.AddChild(player);
    }

    private void OnConnectionFailed()
    {
        GD.PrintErr("[NetworkManager] Nothing implemented for OnConnectionFailed");
    }

    private void OnConnectedToServer()
    {
        GD.Print("[NetworkManager] Successfully connected to host server!");
        GetTree().ChangeSceneToFile("res://Scenes/world.tscn");
    }

    private void OnPeerDisconnected(long id)
    {
        GD.PrintErr("[NetworkManager] Nothing implemented for OnPeerDisconnecteed");
    }

    private async void OnPeerConnected(long id)
    {
        if (Multiplayer.IsServer())
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            SpawnPlayer(id);
        }
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