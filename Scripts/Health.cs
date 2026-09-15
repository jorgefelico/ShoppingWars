using Godot;
using System;

public partial class Health : Node
{
    [Export] public int MaxHealth = 200;
    [Export] HealthBar HealthBar;
    public int CurrentHealth = 200;
    public bool IsDead = false;
    [Export] public float DamageGraceDuration = 0.22f;
    private double _lastDamageTime = -10.0;

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
        if (IsDead) return;

        double now = Time.GetTicksMsec() / 1000.0;
        if (now - _lastDamageTime < DamageGraceDuration)
        {
            // Rapid consecutive hit within grace window deals dampened damage to prevent instant multi-projectile deletion
            amount = Mathf.Max(1, (int)(amount * 0.50f));
        }
        else
        {
            _lastDamageTime = now;
        }

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
            if (GetParent() is Node3D parent3D && GodotObject.IsInstanceValid(parent3D))
            {
                FloatingDamageNumber.Spawn(this, parent3D.GlobalPosition, damageTaken);
            }
        }

        if (justDied)
        {
            Died?.Invoke();
        }
    }
}

