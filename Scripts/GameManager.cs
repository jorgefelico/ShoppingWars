using Godot;

public enum GamePhase
{
    Lobby,
    Shopping,
    BattleRoyale
}

public partial class GameManager : Node
{
    public static GameManager Instance {get; private set;}
    [Export] public float ShoppingDuration = 30.0f;
    [Export] public float BattleDuration = 60.0f;
    public GamePhase CurrentPhase { get; private set; } = GamePhase.Shopping;
    public float TimeRemaining { get; private set; }

    public override void _Ready()
    {
        Instance = this;
        StartShoppingPhase();
    }

    public override void _Process(double delta)
    {
        if(CurrentPhase == GamePhase.Lobby) return;

        TimeRemaining -= (float)delta;
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
        CurrentPhase = GamePhase.Lobby;
        GD.Print("[GameManager] PHASE 0: LOBBY STARTED!");
    }

    private void StartShoppingPhase()
    {
        TimeRemaining = ShoppingDuration;
        CurrentPhase = GamePhase.Shopping;
        GD.Print("[GameManager] PHASE 1: SHOPPING STARTED!");
    }

    private void StartBattleRoyalePhase()
    {
        TimeRemaining = BattleDuration;
        CurrentPhase = GamePhase.BattleRoyale;
        GD.Print("[GameManager] PHASE 2: BATTLE ROYALE STARTED!");
    }
}