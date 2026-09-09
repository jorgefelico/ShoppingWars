using System;
using Godot;

public partial class MainMenu : Control
{
    [Export] private Button HostButton;
    [Export] private Button JoinButton;
    [Export] private VBoxContainer InviteContainer;
    [Export] private Label StatusLabel;

    public override void _Ready()
    {
        HostButton.Pressed += OnHostPressed;
        JoinButton.Pressed += OnJoinPressed;

        if (SteamManager.Instance != null)
        {
            SteamManager.Instance.OnInviteReceived += OnInviteReceived;
        }

        StatusLabel.Text = "Main Menu Ready";
    }

    private void OnInviteReceived(ulong lobbyId, string friendName)
    {
        StatusLabel.Text = $"📩 Game Invite Received from {friendName}!";

        if (InviteContainer == null) return;

        // Clear any previous invite buttons
        foreach (Node child in InviteContainer.GetChildren())
        {
            child.QueueFree();
        }

        // Create an Accept Invite Button on Main Menu
        Button inviteBtn = new Button
        {
            Text = $"📩 ACCEPT INVITE FROM {friendName.ToUpper()} (CLICK TO JOIN)"
        };

        ulong targetLobby = lobbyId;
        inviteBtn.Pressed += () =>
        {
            StatusLabel.Text = $"Joining {friendName}'s match...";
            SteamManager.Instance?.JoinLobbyById(targetLobby);
        };

        InviteContainer.AddChild(inviteBtn);
    }

    private void OnJoinPressed()
    {
        StatusLabel.Text = "Opening Steam Overlay / Joining...";
        if (!SteamManager.Instance.IsSteamInitialized) return;
        SteamManager.Instance.OpenFriendsInviteOverlay();
    }

    private void OnHostPressed()
    {

        GD.Print( SteamManager.Instance.IsOnline());
        StatusLabel.Text = "Creating Steam Lobby...";
        if (!SteamManager.Instance.IsSteamInitialized || !SteamManager.Instance.IsOnline()) return;
        SteamManager.Instance.HostLobby();
    }
}
