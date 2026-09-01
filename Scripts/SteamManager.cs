using Godot;

public partial class SteamManager : Node
{
    public static SteamManager Instance { get; private set; }
    public bool IsSteamInitialized { get; private set; }

    public event System.Action<ulong, string> OnInviteReceived;

    private GodotObject _steam;

    public override void _Ready()
    {
        Instance = this;

        if (Engine.HasSingleton("Steam"))
        {
            _steam = Engine.GetSingleton("Steam");
            GD.Print("[Steam] Found GodotSteam Engine Singleton!");

            Variant initRes = _steam.Call("steamInit");
            GD.Print($"[Steam] steamInit result: {initRes}");

            // Connect GodotSteam signals
            _steam.Connect("lobby_created", Callable.From<long, ulong>(OnLobbyCreated));
            _steam.Connect("join_requested", Callable.From<ulong, ulong>(OnLobbyJoinRequested));
            _steam.Connect("lobby_joined", Callable.From<ulong, long, bool, long>(OnLobbyJoined));

            string name = (string)_steam.Call("getPersonaName");
            ulong steamId = (ulong)_steam.Call("getSteamID");
            GD.Print($"[Steam] GodotSteam Initialized successfully! User: {name} (SteamID: {steamId})");
            IsSteamInitialized = true;
        }
        else
        {
            GD.PrintErr("[Steam] GodotSteam Engine Singleton 'Steam' not found! Make sure GDExtension plugin is enabled.");
        }
    }

    public override void _Process(double delta)
    {
        if (IsSteamInitialized && _steam != null)
        {
            _steam.Call("run_callbacks");
        }
    }

    public void HostLobby()
    {
        if (!IsSteamInitialized || _steam == null)
        {
            GD.PrintErr("[Steam] Cannot host lobby: Steam not initialized.");
            return;
        }

        GD.Print("[Steam] Creating Steam Friends-Only Lobby...");

        if (ClassDB.ClassExists("SteamMultiplayerPeer"))
        {
            MultiplayerPeer peer = (MultiplayerPeer)ClassDB.Instantiate("SteamMultiplayerPeer");
            Error err = (Error)(int)peer.Call("create_host", 0);
            if (err == Error.Ok)
            {
                Multiplayer.MultiplayerPeer = peer;
                GD.Print("[Steam] Native GodotSteam SteamMultiplayerPeer server assigned to Multiplayer.MultiplayerPeer.");
            }
            else
            {
                GD.PrintErr($"[Steam] SteamMultiplayerPeer create_host failed with error: {err}");
                return;
            }
        }
        else
        {
            GD.PrintErr("[Steam] SteamMultiplayerPeer class not found in ClassDB!");
            return;
        }

        // 1 = LOBBY_TYPE_FRIENDS_ONLY
        _steam.Call("createLobby", 1, 4);
    }

    public void OpenFriendsInviteOverlay()
    {
        if (!IsSteamInitialized || _steam == null) return;
        _steam.Call("activateGameOverlay", "friends");
    }

    public void JoinLobbyById(ulong lobbyId)
    {
        if (!IsSteamInitialized || _steam == null) return;
        GD.Print($"[Steam] Joining Lobby directly: {lobbyId}");
        _steam.Call("joinLobby", lobbyId);
    }

    private void OnLobbyCreated(long status, ulong lobbyId)
    {
        if (status != 1) // 1 = k_EResultOK in Steamworks / GodotSteam
        {
            GD.PrintErr($"[Steam] Lobby creation failed with status: {status}");
            return;
        }

        GD.Print($"[Steam] Lobby Created Successfully! ID: {lobbyId}");

        ulong mySteamId = (ulong)_steam.Call("getSteamID");
        _steam.Call("setLobbyData", lobbyId, "HostSteamID", mySteamId.ToString());

        NetworkManager.Instance?.LoadLevel("res://Scenes/world.tscn");
    }

    private void OnLobbyJoinRequested(ulong lobbyId, ulong friendSteamId)
    {
        string friendName = (string)_steam.Call("getFriendPersonaName", friendSteamId);
        GD.Print($"[Steam] Invite received from {friendName} ({friendSteamId}) for Lobby: {lobbyId}");

        OnInviteReceived?.Invoke(lobbyId, friendName);
    }

    private void OnLobbyJoined(ulong lobbyId, long permissions, bool locked, long response)
    {
        ulong mySteamId = (ulong)_steam.Call("getSteamID");
        ulong hostSteamId = (ulong)_steam.Call("getLobbyOwner", lobbyId);

        GD.Print($"[Steam] Entered Lobby: {lobbyId}. Host Steam ID: {hostSteamId}");

        if (mySteamId != hostSteamId)
        {
            GD.Print($"[Steam] Client joined Steam Lobby! Connecting P2P to Host Steam ID: {hostSteamId}...");

            if (ClassDB.ClassExists("SteamMultiplayerPeer"))
            {
                MultiplayerPeer peer = (MultiplayerPeer)ClassDB.Instantiate("SteamMultiplayerPeer");
                Error err = (Error)(int)peer.Call("create_client", hostSteamId, 0);
                if (err == Error.Ok)
                {
                    Multiplayer.MultiplayerPeer = peer;
                    GD.Print("[Steam] Native GodotSteam SteamMultiplayerPeer client assigned to Multiplayer.MultiplayerPeer.");
                }
                else
                {
                    GD.PrintErr($"[Steam] SteamMultiplayerPeer create_client failed with error: {err}");
                    return;
                }
            }
            else
            {
                GD.PrintErr("[Steam] SteamMultiplayerPeer class not found in ClassDB!");
                return;
            }
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest)
        {
            if (IsSteamInitialized && _steam != null)
            {
                _steam.Call("steamShutdown");
            }
        }
    }
    
    public string GetPersonaName()
    {
        if(IsSteamInitialized && _steam != null)
        {
            return (string)_steam.Call("getPersonaName");
        }
        return "Player";
    }
}