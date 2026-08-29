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
    public GamePhase CurrentPhase { get; private set; } = GamePhase.Shopping;
    public float TimeRemaining { get; private set; }
    private float _syncTimer = 0f;
    private const float SyncInterval = 0.5f; // sync every sec

    public override void _Ready()
    {
        Instance = this;
        StartShoppingPhase();
    }

    public override void _Process(double delta)
    {
        if(!Multiplayer.IsServer()) return;
        if (CurrentPhase == GamePhase.Lobby) return;

        TimeRemaining -= (float)delta;

        _syncTimer += (float)delta;
        if(_syncTimer >= SyncInterval)
        {
            _syncTimer = 0f;
            Rpc(nameof(RpcSyncTime), TimeRemaining);
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

    private void StartLobbyPhase()
    {
        Rpc(nameof(RpcSyncPhase), (int)GamePhase.Lobby, TimeRemaining);
        GD.Print("[GameManager] PHASE 0: LOBBY STARTED!");
    }

    private void StartShoppingPhase()
    {
        Rpc(nameof(RpcSyncPhase), (int)GamePhase.Shopping, ShoppingDuration);
        GD.Print("[GameManager] PHASE 1: SHOPPING STARTED!");
    }

    private void StartBattleRoyalePhase()
    {
        Rpc(nameof(RpcSyncPhase), (int)GamePhase.BattleRoyale, BattleDuration);
        GD.Print("[GameManager] PHASE 2: BATTLE ROYALE STARTED!");
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    private void RpcSyncPhase(int phaseIndex, float remainingTime)
    {
        CurrentPhase = (GamePhase)phaseIndex;
        TimeRemaining = remainingTime;
        GD.Print($"[GameManager] Phase synced to: {CurrentPhase}");
    }

    public void SyncStateToPlayer(long peerId)
    {
        if(!Multiplayer.IsServer()) return;

        RpcId(peerId, nameof(RpcSyncPhase), (int)CurrentPhase, TimeRemaining);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    private void RpcSyncTime(float serverTimeRemaining)
    {
        TimeRemaining = serverTimeRemaining;
    }
}