using Godot;
using System;

public partial class Health : Node
{
    [Export] public int MaxHealth = 100;
    [Export] HealthBar HealthBar;
    public int CurrentHealth;
    public bool IsDead = false;

    public event Action Died;
    public event Action<int, int> HealthChanged;

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
            if (HealthBar != null && GodotObject.IsInstanceValid(HealthBar))
            {
                HealthBar.QueueFree();
            }
            HealthBar = null;
        }
        else
        {
            if (HealthBar != null && GodotObject.IsInstanceValid(HealthBar))
            {
                HealthBar.Refresh(CurrentHealth, MaxHealth);
            }
        }
    }

    public void TakeDamage(int amount)
    {
        if (!Multiplayer.IsServer()) return;
        int newHealth = Mathf.Clamp(CurrentHealth - amount, 0, MaxHealth);
        bool isDead = newHealth <= 0;
        Rpc(nameof(RpcSyncHealth), newHealth, isDead);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    private void RpcSyncHealth(int health, bool isDead)
    {
        CurrentHealth = health;
        bool justDied = isDead && !IsDead;
        IsDead = isDead;

        if (HealthBar != null && GodotObject.IsInstanceValid(HealthBar))
        {
            HealthBar.Refresh(CurrentHealth, MaxHealth);
        }

        HealthChanged?.Invoke(CurrentHealth, MaxHealth);

        if (justDied)
        {
            Died?.Invoke();
        }
    }
}

