using System;
using Godot;

public partial class MainMenu : Control
{
    [Export] private Button HostButton;
    [Export] private Button JoinButton;
    [Export] private Button SoloButton;
    [Export] private Label StatusLabel;

    public override void _Ready()
    {
        HostButton.Pressed += OnHostPressed;
        JoinButton.Pressed += OnJoinPressed;
        SoloButton.Pressed += OnSoloPressed;

        StatusLabel.Text = "Main Menu Ready";
    }

    private void OnJoinPressed()
    {
         StatusLabel.Text = "Opening Steam Overlay / Joining...";
         if(!SteamManager.Instance.IsSteamInitialized) return;
        SteamManager.Instance.OpenFriendsInviteOverlay();
    }


    private void OnHostPressed()
    {
        StatusLabel.Text = "Creating Steam Lobby...";
        if(!SteamManager.Instance.IsSteamInitialized) return;
        SteamManager.Instance.HostLobby();
    }

    private void OnSoloPressed()
    {
        StatusLabel.Text = "Starting Solo Game...";
        GetTree().ChangeSceneToFile("res://Scenes/world.tscn");
    }

}
