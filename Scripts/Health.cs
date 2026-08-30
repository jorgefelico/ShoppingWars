using Godot;

public partial class Health : Node
{
    [Export] int MaxHealth = 100;
    [Export] HealthBar HealthBar;
    public int CurrentHealth;
    public bool IsDead = false;

    public override void _EnterTree()
    {
        if (GetParent() is Node parent && int.TryParse(parent.Name, out int peerId))
        {
            SetMultiplayerAuthority(peerId);
        }
    }

    public override void _Ready()
    {
        CurrentHealth = MaxHealth;
        if (!IsMultiplayerAuthority())
        {
            HealthBar?.QueueFree();
        }
        else
        {
            HealthBar?.Refresh(CurrentHealth, MaxHealth);
        }
    }

    public void TakeDamage(int amount)
    {
        if(!Multiplayer.IsServer()) return;
        CurrentHealth = Mathf.Clamp(CurrentHealth - amount, 0, MaxHealth);
        if (CurrentHealth == 0) IsDead = true;
        Rpc(nameof(RpcSyncHealth), CurrentHealth, IsDead);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    private void RpcSyncHealth(int health, bool isDead)
    {
        CurrentHealth = health;
        IsDead = isDead;
        HealthBar?.Refresh(CurrentHealth, MaxHealth);
    }
}
