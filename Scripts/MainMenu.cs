using System;
using Godot;

public partial class MainMenu : Control
{
    [Export] private Button HostButton;
    [Export] private Button JoinButton;
    [Export] private Button QuitButton;
    [Export] private VBoxContainer InviteContainer;
    [Export] private Label StatusLabel;

    public override void _Ready()
    {
        if (HostButton != null) HostButton.Pressed += OnHostPressed;
        if (JoinButton != null) JoinButton.Pressed += OnJoinPressed;

        if (QuitButton == null) QuitButton = GetNodeOrNull<Button>("VBoxContainer/Quit");
        if (QuitButton != null) QuitButton.Pressed += OnQuitPressed;

        if (SteamManager.Instance != null)
        {
            SteamManager.Instance.OnInviteReceived += OnInviteReceived;
        }

        if (StatusLabel != null) StatusLabel.Text = "Main Menu Ready";
    }

    public override void _ExitTree()
    {
        if (SteamManager.Instance != null)
        {
            SteamManager.Instance.OnInviteReceived -= OnInviteReceived;
        }
    }

    private void OnQuitPressed()
    {
        GetTree().Quit();
    }

    private void OnInviteReceived(ulong lobbyId, string friendName)
    {
        if (StatusLabel != null) StatusLabel.Text = $"📩 Game Invite Received from {friendName}!";

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
            if (StatusLabel != null) StatusLabel.Text = $"Joining {friendName}'s match...";
            SteamManager.Instance?.JoinLobbyById(targetLobby);
        };

        InviteContainer.AddChild(inviteBtn);
    }

    private void OnJoinPressed()
    {
        if (StatusLabel != null) StatusLabel.Text = "Opening Steam Overlay / Joining...";
        if (!SteamManager.Instance.IsSteamInitialized) return;
        SteamManager.Instance.OpenFriendsInviteOverlay();
    }

    private void OnHostPressed()
    {
        GD.Print(SteamManager.Instance.IsOnline());
        if (StatusLabel != null) StatusLabel.Text = "Creating Steam Lobby...";
        if (!SteamManager.Instance.IsSteamInitialized || !SteamManager.Instance.IsOnline()) return;
        SteamManager.Instance.HostLobby();
    }
}
