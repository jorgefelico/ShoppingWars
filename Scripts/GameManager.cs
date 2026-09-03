using Godot;

public enum GamePhase
{
    Lobby,
    Shopping,
    BattleRoyale
}

public partial class GameManager : Node
{
    public static GameManager Instance { get; private set; }
    [Export] public float ShoppingDuration = 30.0f;
    [Export] public float BattleDuration = 60.0f;
    public GamePhase CurrentPhase { get; private set; } = GamePhase.Lobby;
    public float TimeRemaining { get; private set; }
    private float _syncTimer = 0f;
    private const float SyncInterval = 0.25f; // Sync state 4 times per second

    public override void _Ready()
    {
        Instance = this;

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
        if (CurrentPhase == GamePhase.Lobby) return;

        if (Multiplayer.IsServer())
        {
            TimeRemaining -= (float)delta;

            _syncTimer += (float)delta;
            if (_syncTimer >= SyncInterval)
            {
                _syncTimer = 0f;
                if (Multiplayer.HasMultiplayerPeer())
                {
                    Rpc(nameof(RpcSyncState), (int)CurrentPhase, TimeRemaining);
                }
            }

            if (TimeRemaining <= 0f)
            {
                TimeRemaining = 0f;

                if (CurrentPhase == GamePhase.Shopping)
                {
                    StartBattleRoyalePhase();
                }
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

    public void StartShoppingPhase()
    {
        if (!Multiplayer.IsServer()) return;

        if (Multiplayer.HasMultiplayerPeer())
        {
            Rpc(nameof(RpcSyncState), (int)GamePhase.Shopping, ShoppingDuration);
        } else
        {
            RpcSyncState((int)GamePhase.Shopping, ShoppingDuration);
        }
        GD.Print("[GameManager] PHASE 1: SHOPPING STARTED!");
    }

    public void StartBattleRoyalePhase()
    {
        if (!Multiplayer.IsServer()) return;

        if (Multiplayer.HasMultiplayerPeer())
        {
            Rpc(nameof(RpcSyncState), (int)GamePhase.BattleRoyale, BattleDuration);
        } else
        {
            RpcSyncState((int)GamePhase.BattleRoyale, BattleDuration);
        }
        GD.Print("[GameManager] PHASE 2: BATTLE ROYALE STARTED!");
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
    private void RpcRequestSyncState()
    {
        if (!Multiplayer.IsServer()) return;
        long senderId = Multiplayer.GetRemoteSenderId();
        RpcId(senderId, nameof(RpcSyncState), (int)CurrentPhase, TimeRemaining);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    private void RpcSyncState(int phaseIndex, float serverTimeRemaining)
    {
        GamePhase newPhase = (GamePhase)phaseIndex;
        if (CurrentPhase != newPhase)
        {
            CurrentPhase = newPhase;
            EmitSignal(SignalName.GamePhaseChanged);
            GD.Print($"[GameManager] Phase synced to: {CurrentPhase}");
        }

        TimeRemaining = serverTimeRemaining;
    }

    public void SyncStateToPlayer(long peerId)
    {
        if (!Multiplayer.IsServer()) return;

        RpcId(peerId, nameof(RpcSyncState), (int)CurrentPhase, TimeRemaining);
    }

    [Signal]
    public delegate void GamePhaseChangedEventHandler();
}