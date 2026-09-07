using Godot;

public enum GamePhase
{
    Lobby,
    Shopping,
    BattleRoyale,
    GameOver
}

public partial class GameManager : Node
{
    public static GameManager Instance { get; private set; }
    [Export] public float ShoppingDuration = 30.0f;
    [Export] public float BattleDuration = 60.0f;
    [Export] private LightmapGI _lightmap;
    [Export] private WorldEnvironment _worldEnvironment;
    [Export] public AudioStream BattleRoyaleSound;
    private AudioStreamPlayer _audioPlayer;
    public GamePhase CurrentPhase { get; private set; } = GamePhase.Lobby;
    public float TimeRemaining { get; private set; }
    public string WinnerName { get; private set; } = "";
    public bool IsDraw { get; private set; } = false;
    private float _syncTimer = 0f;
    private const float SyncInterval = 0.25f; // Sync state 4 times per second

    public override void _Ready()
    {
        Instance = this;

        _audioPlayer = new AudioStreamPlayer();
        _audioPlayer.Bus = "Master";
        AddChild(_audioPlayer);

        if (!Multiplayer.IsServer())
        {
            // Client requests current authoritative state from host immediately on load
            if (Multiplayer.HasMultiplayerPeer())
            {
                RpcId(1, nameof(RpcRequestSyncState));
            }
        }
    }

    public override void _Process(double delta)
    {
        if (CurrentPhase == GamePhase.Lobby || CurrentPhase == GamePhase.GameOver) return;

        if (Multiplayer.IsServer())
        {
            TimeRemaining -= (float)delta;

            _syncTimer += (float)delta;
            if (_syncTimer >= SyncInterval)
            {
                _syncTimer = 0f;
                if (Multiplayer.HasMultiplayerPeer())
                {
                    Rpc(nameof(RpcSyncState), (int)CurrentPhase, TimeRemaining, WinnerName, IsDraw);
                }
            }

            if (CurrentPhase == GamePhase.BattleRoyale)
            {
                CheckBattleRoyaleOutcome();
            }
            else if (CurrentPhase == GamePhase.Shopping && TimeRemaining <= 0f)
            {
                TimeRemaining = 0f;
                StartBattleRoyalePhase();
            }
        }
        else
        {
            // On clients, smoothly decrement local timer between server updates
            if (TimeRemaining > 0f)
            {
                TimeRemaining = Mathf.Max(0f, TimeRemaining - (float)delta);
            }
        }
    }

    private void CheckBattleRoyaleOutcome()
    {
        if (!Multiplayer.IsServer()) return;

        var allPlayers = GetAllPlayers();
        if (allPlayers.Count == 0) return;

        var livingPlayers = GetLivingPlayers();

        // If match started with 2+ players, check if only 1 remains (Last Man Standing)
        if (allPlayers.Count >= 2)
        {
            if (livingPlayers.Count == 1)
            {
                GD.Print($"[GameManager] BattleRoyale Last Shopper Standing: '{livingPlayers[0].PlayerName}' won! (Total players: {allPlayers.Count})");
                EndGame(livingPlayers[0].PlayerName, false);
                return;
            }
            else if (livingPlayers.Count == 0)
            {
                GD.Print($"[GameManager] BattleRoyale All Shoppers Eliminated (Draw). Total players: {allPlayers.Count}");
                EndGame("", true);
                return;
            }
        }
        else if (allPlayers.Count == 1)
        {
            // Solo play / test mode
            if (livingPlayers.Count == 0)
            {
                EndGame("", true);
                return;
            }
        }

        // Check if Battle Royale timer ran out
        if (TimeRemaining <= 0f)
        {
            TimeRemaining = 0f;

            if (livingPlayers.Count == 1)
            {
                EndGame(livingPlayers[0].PlayerName, false);
            }
            else if (livingPlayers.Count > 1)
            {
                // Winner decided by highest remaining health
                PlayerController highestHpPlayer = livingPlayers[0];
                bool tied = false;

                for (int i = 1; i < livingPlayers.Count; i++)
                {
                    Health curH = livingPlayers[i].Health ?? livingPlayers[i].GetNodeOrNull<Health>("Health");
                    Health topH = highestHpPlayer.Health ?? highestHpPlayer.GetNodeOrNull<Health>("Health");
                    int curHp = curH != null ? curH.CurrentHealth : 0;
                    int topHp = topH != null ? topH.CurrentHealth : 0;
                    if (curHp > topHp)
                    {
                        highestHpPlayer = livingPlayers[i];
                        tied = false;
                    }
                    else if (curHp == topHp)
                    {
                        tied = true;
                    }
                }

                if (tied)
                {
                    EndGame("TIE", true);
                }
                else
                {
                    EndGame(highestHpPlayer.PlayerName, false);
                }
            }
            else
            {
                EndGame("", true);
            }
        }
    }

    public System.Collections.Generic.List<PlayerController> GetAllPlayers()
    {
        var list = new System.Collections.Generic.List<PlayerController>();
        if (Engine.GetMainLoop() is SceneTree tree)
        {
            foreach (Node node in tree.GetNodesInGroup("Players"))
            {
                if (node is PlayerController pc && GodotObject.IsInstanceValid(pc) && pc.IsInsideTree())
                {
                    list.Add(pc);
                }
            }
        }
        return list;
    }

    public System.Collections.Generic.List<PlayerController> GetLivingPlayers()
    {
        var list = new System.Collections.Generic.List<PlayerController>();
        if (Engine.GetMainLoop() is SceneTree tree)
        {
            foreach (Node node in tree.GetNodesInGroup("Players"))
            {
                if (node is PlayerController pc && GodotObject.IsInstanceValid(pc) && pc.IsInsideTree())
                {
                    Health health = pc.Health ?? pc.GetNodeOrNull<Health>("Health");
                    if (health != null && !health.IsDead && health.CurrentHealth > 0)
                    {
                        list.Add(pc);
                    }
                }
            }
        }
        return list;
    }

    public void StartShoppingPhase()
    {
        if (!Multiplayer.IsServer()) return;

        WinnerName = "";
        IsDraw = false;

        if (Multiplayer.HasMultiplayerPeer())
        {
            Rpc(nameof(RpcSyncState), (int)GamePhase.Shopping, ShoppingDuration, "", false);
        }
        else
        {
            RpcSyncState((int)GamePhase.Shopping, ShoppingDuration, "", false);
        }
        GD.Print("[GameManager] PHASE 1: SHOPPING STARTED!");
    }

    public void StartBattleRoyalePhase()
    {
        if (!Multiplayer.IsServer()) return;

        if (Multiplayer.HasMultiplayerPeer())
        {
            Rpc(nameof(RpcSyncState), (int)GamePhase.BattleRoyale, BattleDuration, "", false);
        }
        else
        {
            RpcSyncState((int)GamePhase.BattleRoyale, BattleDuration, "", false);
        }
        GD.Print("[GameManager] PHASE 2: BATTLE ROYALE STARTED!");
    }

    public void EndGame(string winnerName, bool isDraw)
    {
        if (!Multiplayer.IsServer()) return;
        if (CurrentPhase == GamePhase.GameOver) return;

        WinnerName = winnerName;
        IsDraw = isDraw;

        if (Multiplayer.HasMultiplayerPeer())
        {
            Rpc(nameof(RpcSyncGameOver), winnerName, isDraw);
        }
        else
        {
            RpcSyncGameOver(winnerName, isDraw);
        }
        GD.Print($"[GameManager] PHASE 3: GAME OVER! Winner: '{(isDraw ? "DRAW" : winnerName)}'");
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
    private void RpcRequestSyncState()
    {
        if (!Multiplayer.IsServer()) return;
        long senderId = Multiplayer.GetRemoteSenderId();
        RpcId(senderId, nameof(RpcSyncState), (int)CurrentPhase, TimeRemaining, WinnerName, IsDraw);
        AmbientEventManager.Instance?.SyncStateToClient(senderId);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    private void RpcSyncState(int phaseIndex, float serverTimeRemaining, string winnerName = "", bool isDraw = false)
    {
        GamePhase newPhase = (GamePhase)phaseIndex;
        WinnerName = winnerName;
        IsDraw = isDraw;

        if (CurrentPhase != newPhase)
        {
            CurrentPhase = newPhase;

            if (newPhase == GamePhase.Lobby)
            {
                AmbientEventManager.Instance?.ResetEvents();
            }

            if (newPhase == GamePhase.BattleRoyale && BattleRoyaleSound != null)
            {
                _audioPlayer.Stream = BattleRoyaleSound;
                _audioPlayer.Play();
            }

            if (newPhase == GamePhase.GameOver)
            {
                AmbientEventManager.Instance?.ResetEvents();
            }

            EmitSignal(SignalName.GamePhaseChanged);
            GD.Print($"[GameManager] Phase synced to: {CurrentPhase} (Winner: {WinnerName})");
        }

        TimeRemaining = serverTimeRemaining;
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    private void RpcSyncGameOver(string winnerName, bool isDraw)
    {
        WinnerName = winnerName;
        IsDraw = isDraw;
        CurrentPhase = GamePhase.GameOver;
        TimeRemaining = 0f;

        AmbientEventManager.Instance?.ResetEvents();

        EmitSignal(SignalName.GamePhaseChanged);
        EmitSignal(SignalName.GameOverDeclared, winnerName, isDraw);
        GD.Print($"[GameManager] GameOver Synced! Winner: '{winnerName}', IsDraw: {isDraw}");
    }

    public void RestartGame()
    {
        if (!Multiplayer.IsServer()) return;
        GD.Print("[GameManager] Restarting Game match...");
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.RestartMatch();
        }
        else
        {
            GetTree().ChangeSceneToFile("res://Scenes/StoreInterior.tscn");
        }
    }

    public void SyncStateToPlayer(long peerId)
    {
        if (!Multiplayer.IsServer()) return;

        RpcId(peerId, nameof(RpcSyncState), (int)CurrentPhase, TimeRemaining, WinnerName, IsDraw);
        AmbientEventManager.Instance?.SyncStateToClient(peerId);
    }

    [Signal]
    public delegate void GamePhaseChangedEventHandler();

    [Signal]
    public delegate void GameOverDeclaredEventHandler(string winnerName, bool isDraw);
}