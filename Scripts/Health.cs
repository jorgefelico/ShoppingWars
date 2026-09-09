using Godot;
using System;

public partial class Health : Node
{
    [Export] public int MaxHealth = 150;
    [Export] HealthBar HealthBar;
    public int CurrentHealth = 150;
    public bool IsDead = false;

    public event Action Died;
    public event Action<int, int> HealthChanged;
    public event Action<int> Damaged;

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
        if (Multiplayer.HasMultiplayerPeer() && !Multiplayer.IsServer()) return;
        int newHealth = Mathf.Clamp(CurrentHealth - amount, 0, MaxHealth);
        bool isDead = newHealth <= 0;
        if (Multiplayer.HasMultiplayerPeer())
        {
            Rpc(nameof(RpcSyncHealth), newHealth, isDead);
        }
        else
        {
            RpcSyncHealth(newHealth, isDead);
        }
    }

    public void Heal(int amount)
    {
        if (Multiplayer.HasMultiplayerPeer() && !Multiplayer.IsServer()) return;
        int newHealth = Mathf.Clamp(CurrentHealth + amount, 0, MaxHealth);
        if (Multiplayer.HasMultiplayerPeer())
        {
            Rpc(nameof(RpcSyncHealth), newHealth, false);
        }
        else
        {
            RpcSyncHealth(newHealth, false);
        }
    }

    public void ResetHealth()
    {
        if (Multiplayer.HasMultiplayerPeer() && !Multiplayer.IsServer()) return;
        if (Multiplayer.HasMultiplayerPeer())
        {
            Rpc(nameof(RpcSyncHealth), MaxHealth, false);
        }
        else
        {
            RpcSyncHealth(MaxHealth, false);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    private void RpcSyncHealth(int health, bool isDead)
    {
        int damageTaken = CurrentHealth - health;
        CurrentHealth = health;
        bool justDied = isDead && !IsDead;
        IsDead = isDead;

        if (HealthBar != null && GodotObject.IsInstanceValid(HealthBar))
        {
            HealthBar.Refresh(CurrentHealth, MaxHealth);
        }

        HealthChanged?.Invoke(CurrentHealth, MaxHealth);

        if (damageTaken > 0)
        {
            Damaged?.Invoke(damageTaken);
        }

        if (justDied)
        {
            Died?.Invoke();
        }
    }
}

