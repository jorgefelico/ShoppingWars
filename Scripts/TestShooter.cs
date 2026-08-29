using Godot;
using System;

public partial class TestShooter : Node3D
{
    [Export] PackedScene Product;
    [Export] float ShootFrequency = 4f;
    float timer = 0f;

    public override void _Process(double delta)
    {
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
        instance.Transform = Transform;
        instance.Position = instance.Position + new Vector3(0,1f,0);
        GetTree().CurrentScene.AddChild(instance);
        instance.LinearVelocity = Vector3.Forward * 40;
    }

}
