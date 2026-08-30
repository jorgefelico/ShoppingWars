using Godot;
using System;

public partial class TestShooter : Node3D
{
    [Export] PackedScene Product;
    [Export] float ShootFrequency = 4f;
    float timer = 0f;
    public override void _Process(double delta)
    {
        if(!Multiplayer.IsServer()) return;
        if(timer >= ShootFrequency)
        {
            Shoot();
            timer = 0f;
            return;
        }

        timer += (float)delta;
    }

    private void Shoot()
    {
        RigidBody3D instance = (RigidBody3D)Product.Instantiate();
        instance.Name = $"ShotApple_{Guid.NewGuid()}";
        instance.Transform = Transform;
        instance.Position = instance.Position + new Vector3(0, 1f, 0);
        GetTree().CurrentScene.AddChild(instance);
        instance.LinearVelocity = Vector3.Forward * 40;

        // Auto-cleanup test projectile after 4 seconds to prevent unbounded network object accumulation
        GetTree().CreateTimer(4.0f).Timeout += () =>
        {
            if (IsInstanceValid(instance))
            {
                instance.QueueFree();
            }
        };
    }

}
