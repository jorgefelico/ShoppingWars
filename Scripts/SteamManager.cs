using Godot;
using Steamworks;

public partial class SteamManager : Node
{
    public static SteamManager Instance { get; private set; }
    public bool IsSteamInitialized { get; private set; }

    protected Callback<LobbyCreated_t> _lobbyCreated;
    protected Callback<GameLobbyJoinRequested_t> _lobbyJoinRequested;
    protected Callback<LobbyEnter_t> _lobbyEntered;

    public override void _Ready()
    {
        Instance = this;

        try
        {
            if (!Packsize.Test())
            {
                GD.PrintErr("[Steam] Packsize Test failed!");
                return;
            }

            if (!DllCheck.Test())
            {
                GD.PrintErr("[Steam] DllCheck Test failed!");
                return;
            }

            IsSteamInitialized = SteamAPI.Init();

            if (IsSteamInitialized)
            {
                string name = SteamFriends.GetPersonaName();
                GD.Print($"[Steam] Initialized successfully! Logged in as: {name}");

                // Register steam overlay & lobby callbacks
                _lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
                _lobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnLobbyJoinRequested);
                _lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
            }
            else
            {
                GD.PrintErr("[Steam] SteamAPI_Init() failed! Is Steam running?");
            }
        }
        catch (System.Exception e)
        {
            GD.PrintErr($"[Steam] Error initializing Steam: {e.Message}");
        }
    }

    public override void _Process(double delta)
    {
        if (IsSteamInitialized)
        {
            SteamAPI.RunCallbacks();
        }
    }

    public void HostLobby()
    {
        if (!IsSteamInitialized) return;
        GD.Print("[Steam] Creating Steam Friends-Only Lobby...");
        ENetMultiplayerPeer peer = new ENetMultiplayerPeer();
        peer.CreateServer(7000,4);
        Multiplayer.MultiplayerPeer = peer;
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, 4);
    }

    public void OpenFriendsInviteOverlay()
    {
        if (!IsSteamInitialized) return;
        SteamFriends.ActivateGameOverlay("friends");
    }

    private void OnLobbyCreated(LobbyCreated_t param)
        {
            if (param.m_eResult != EResult.k_EResultOK)
            {
                GD.PrintErr("[Steam] Lobby creation failed: ", param.m_eResult);
                return;
            }

            CSteamID lobbyId = new CSteamID(param.m_ulSteamIDLobby);
            GD.Print($"[Steam] Lobby Created Successfully! ID: {lobbyId}");

            // Load into World Scene
            NetworkManager.Instance?.LoadLevel("res://Scenes/world.tscn");
        }

        private void OnLobbyJoinRequested(GameLobbyJoinRequested_t param)
        {
            GD.Print($"[Steam] Accepting invite to Lobby: {param.m_steamIDLobby}");
            SteamMatchmaking.JoinLobby(param.m_steamIDLobby);
        }

         private void OnLobbyEntered(LobbyEnter_t param)
        {
            GD.Print($"[Steam] Entered Lobby: {param.m_ulSteamIDLobby}");
            GetTree().ChangeSceneToFile("res://Scenes/world.tscn");
        }

        public override void _Notification(int what)
        {
            if (what == NotificationWMCloseRequest)
            {
                if (IsSteamInitialized)
                {
                    SteamAPI.Shutdown();
                }
            }
        }
}