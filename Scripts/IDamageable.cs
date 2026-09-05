using Godot;

public interface IDamageable
{
    void TakeDamage(int amount, Node3D source = null);
}