using Godot;

public partial class Health : Node
{
    [Export] int MaxHealth = 100;
    [Export] HealthBar HealthBar;
    public int CurrentHealth;
    public bool IsDead = false;

    public override void _Ready()
    {
        CurrentHealth = MaxHealth;
        HealthBar?.Refresh(CurrentHealth, MaxHealth);
    }

    public void TakeDamage(int amount)
    {
        GD.Print("Take damage");
        CurrentHealth = Mathf.Clamp(CurrentHealth - amount, 0, MaxHealth);
        if (CurrentHealth == 0) IsDead = true;
        HealthBar?.Refresh(CurrentHealth, MaxHealth);
    }
}
