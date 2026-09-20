using Godot;
using System.Collections.Generic;
public partial class SteamManager : Node
{
    public static SteamManager Instance { get; private set; }
    public bool IsSteamInitialized { get; private set; }
    public ulong CurrentLobbyId { get; private set; } = 0;

    public event System.Action<ulong, string> OnInviteReceived;

    // Rich presence key advertising which Steam lobby this player is hosting.
    // Friends read it via getFriendRichPresence so the main menu can list
    // joinable friend lobbies in-game (this GodotSteam build does not expose
    // GetLobbyByIndex, so lobby list search results cannot be enumerated).
    private const string LobbyPresenceKey = "lobby";

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
            _steam.Connect("lobby_invite", Callable.From<ulong, ulong, ulong>(OnLobbyInviteReceived));
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

    public void JoinLobbyById(ulong lobbyId)
    {
        if (!IsSteamInitialized || _steam == null) return;
        GD.Print($"[Steam] Joining Lobby directly: {lobbyId}");
        _steam.Call("joinLobby", lobbyId);
    }

    /// <summary>
    /// Scans the friend list for players advertising an active hosted lobby via
    /// rich presence. Returns (steamId, personaName, lobbyId) per hit.
    /// </summary>
    public List<(ulong SteamId, string Name, ulong LobbyId)> GetFriendsWithActiveLobbies()
    {
        var results = new List<(ulong SteamId, string Name, ulong LobbyId)>();
        if (!IsSteamInitialized || _steam == null) return results;

        int friendCount = (int)_steam.Call("getFriendCount", 0x7F); // k_EFriendFlag_All
        for (int i = 0; i < friendCount; i++)
        {
            ulong friendId = (ulong)_steam.Call("getFriendByIndex", i, 0x7F);
            string name = (string)_steam.Call("getFriendPersonaName", friendId);
            if (string.IsNullOrEmpty(name)) continue;

            // getFriendRichPresence issues RequestFriendRichPresence internally first.
            string lobbyIdStr = (string)_steam.Call("getFriendRichPresence", friendId, LobbyPresenceKey);
            if (!string.IsNullOrEmpty(lobbyIdStr) && ulong.TryParse(lobbyIdStr, out ulong lobbyId) && lobbyId != 0)
            {
                results.Add((friendId, name, lobbyId));
                GD.Print($"[Steam] Friend with active lobby: {name} -> {lobbyId}");
            }
        }
        return results;
    }

    public void ClearLobbyPresence()
    {
        if (!IsSteamInitialized || _steam == null) return;
        _steam.Call("clearRichPresence");
    }

    public void LeaveCurrentLobby()
    {
        if (IsSteamInitialized && _steam != null)
        {
            if (CurrentLobbyId != 0)
            {
                GD.Print($"[Steam] Leaving Steam Lobby: {CurrentLobbyId}");
                _steam.Call("leaveLobby", CurrentLobbyId);
                CurrentLobbyId = 0;
            }
            ClearLobbyPresence();
        }
    }

    public void Shutdown()
    {
        if (IsSteamInitialized && _steam != null)
        {
            LeaveCurrentLobby();
            GD.Print("[Steam] Shutting down Steamworks API...");
            _steam.Call("steamShutdown");
            IsSteamInitialized = false;
        }
    }

    private void OnLobbyCreated(long status, ulong lobbyId)
    {
        if (status != 1) // 1 = k_EResultOK in Steamworks / GodotSteam
        {
            GD.PrintErr($"[Steam] Lobby creation failed with status: {status}");
            return;
        }

        CurrentLobbyId = lobbyId;
        GD.Print($"[Steam] Lobby Created Successfully! ID: {lobbyId}");

        ulong mySteamId = (ulong)_steam.Call("getSteamID");
        _steam.Call("setLobbyData", lobbyId, "HostSteamID", mySteamId.ToString());

        // Advertise this lobby to friends via rich presence so they can find it
        // from the in-game "Join A Friend" list.
        _steam.Call("setRichPresence", LobbyPresenceKey, lobbyId.ToString());
        GD.Print($"[Steam] Rich presence set: {LobbyPresenceKey}={lobbyId}");

        NetworkManager.Instance?.LoadLevel("res://Scenes/StoreInterior.tscn");
    }

    // Emitted from Steamworks' LobbyInvite_t: "Someone has invited you to join a Lobby."
    private void OnLobbyInviteReceived(ulong inviterSteamId, ulong lobbyId, ulong gameId)
    {
        string friendName = (string)_steam.Call("getFriendPersonaName", inviterSteamId);
        if (string.IsNullOrEmpty(friendName))
        {
            friendName = "A Friend";
        }
        GD.Print($"[Steam] Lobby invite received from {friendName} ({inviterSteamId}) for Lobby: {lobbyId}");

        OnInviteReceived?.Invoke(lobbyId, friendName);
    }

    private void OnLobbyJoined(ulong lobbyId, long permissions, bool locked, long response)
    {
        CurrentLobbyId = lobbyId;
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
            Shutdown();
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

    public bool IsOnline()
    {
        if(IsSteamInitialized && _steam != null)
        {
            return (bool)_steam.Call("getSteamID");
        }
        return false;
    }
}