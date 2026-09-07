using Godot;

public enum GamePhase
{
    Lobby,
    ShoppingTransition,
    Shopping,
    BattleTransition,
    BattleRoyale,
    GameOver
}

public partial class GameManager : Node
{
    public static GameManager Instance { get; private set; }
    [Export] public float ShoppingTransitionDuration = 10.0f;
    [Export] public float ShoppingDuration = 30.0f;
    [Export] public float BattleTransitionDuration = 10.0f;
    [Export] public float BattleDuration = 60.0f;
    [Export] private LightmapGI _lightmap;
    [Export] private WorldEnvironment _worldEnvironment;
    [Export] public AudioStream BattleRoyaleSound;
    [Export] public AudioStream TransitionChime;
    [Export] public AudioStream ShoppingTransitionSound;
    [Export] public AudioStream BattleTransitionSound;
    [Export] public AudioStream LobbyMusic;
    [Export] public AudioStream BattleRoyaleMusic;
    [Export] public Godot.Collections.Array<AudioStream> LobbyPlaylist = new();
    [Export] public Godot.Collections.Array<AudioStream> BattleRoyalePlaylist = new();
    [Export] public bool ShufflePlaylists = true;
    [Export] public float MusicVolumeDb = -6.0f;
    private AudioStreamPlayer _audioPlayer;
    private AudioStreamPlayer _musicPlayer;
    private Tween _musicTween;
    private Godot.Collections.Array<AudioStream> _activePlaylist;
    private int _currentTrackIndex = -1;
    public GamePhase CurrentPhase { get; private set; } = GamePhase.Lobby;
    public float TimeRemaining { get; private set; }
    public string WinnerName { get; private set; } = "";
    public bool IsDraw { get; private set; } = false;
    private float _syncTimer = 0f;
    private const float SyncInterval = 0.25f; // Sync state 4 times per second

    public override void _Ready()
    {
        Instance = this;

        _audioPlayer = new AudioStreamPlayer();
        _audioPlayer.Bus = "Master";
        AddChild(_audioPlayer);

        if (TransitionChime == null)
        {
            string[] candidatePaths = {
                "res://Sounds/store_chime.wav",
            };
            foreach (var path in candidatePaths)
            {
                if (ResourceLoader.Exists(path) || FileAccess.FileExists(path))
                {
                    TransitionChime = GD.Load<AudioStream>(path);
                    break;
                }
            }
        }

        // Populate default Lobby/Shopping playlist if empty
        if (LobbyPlaylist.Count == 0)
        {
            if (LobbyMusic != null) LobbyPlaylist.Add(LobbyMusic);
            AddTrackIfValid(LobbyPlaylist, "res://Sounds/Prime_Time_Jackpot.mp3");
            AddTrackIfValid(LobbyPlaylist, "res://Sounds/Velvet_Sunday.mp3");
        }

        // Populate default Battle Royale playlist if empty
        if (BattleRoyalePlaylist.Count == 0)
        {
            if (BattleRoyaleMusic != null) BattleRoyalePlaylist.Add(BattleRoyaleMusic);
            AddTrackIfValid(BattleRoyalePlaylist, "res://Sounds/Overdrive_Takedown.mp3");
        }

        foreach (var stream in LobbyPlaylist) DisableStreamLoop(stream);
        foreach (var stream in BattleRoyalePlaylist) DisableStreamLoop(stream);

        _musicPlayer = new AudioStreamPlayer();
        _musicPlayer.Bus = "Master";
        _musicPlayer.VolumeDb = MusicVolumeDb;
        _musicPlayer.Finished += OnMusicTrackFinished;
        AddChild(_musicPlayer);

        PlayMusicForPhase(CurrentPhase);

        if (!Multiplayer.IsServer())
        {
            // Client requests current authoritative state from host immediately on load
            if (Multiplayer.HasMultiplayerPeer())
            {
                RpcId(1, nameof(RpcRequestSyncState));
            }
        }
    }

    public override void _ExitTree()
    {
        if (_musicPlayer != null)
        {
            _musicPlayer.Finished -= OnMusicTrackFinished;
        }
        if (_musicTween != null && _musicTween.IsValid())
        {
            _musicTween.Kill();
        }
    }

    public override void _Process(double delta)
    {
        if (CurrentPhase == GamePhase.Lobby || CurrentPhase == GamePhase.GameOver) return;

        if (Multiplayer.IsServer())
        {
            TimeRemaining -= (float)delta;

            _syncTimer += (float)delta;
            if (_syncTimer >= SyncInterval)
            {
                _syncTimer = 0f;
                if (Multiplayer.HasMultiplayerPeer())
                {
                    Rpc(nameof(RpcSyncState), (int)CurrentPhase, TimeRemaining, WinnerName, IsDraw);
                }
            }

            if (CurrentPhase == GamePhase.ShoppingTransition && TimeRemaining <= 0f)
            {
                TimeRemaining = 0f;
                StartShoppingPhase();
            }
            else if (CurrentPhase == GamePhase.Shopping && TimeRemaining <= 0f)
            {
                TimeRemaining = 0f;
                StartBattleTransition();
            }
            else if (CurrentPhase == GamePhase.BattleTransition && TimeRemaining <= 0f)
            {
                TimeRemaining = 0f;
                StartBattleRoyalePhase();
            }
            else if (CurrentPhase == GamePhase.BattleRoyale)
            {
                CheckBattleRoyaleOutcome();
            }
        }
        else
        {
            // On clients, smoothly decrement local timer between server updates
            if (TimeRemaining > 0f)
            {
                TimeRemaining = Mathf.Max(0f, TimeRemaining - (float)delta);
            }
        }
    }

    private void CheckBattleRoyaleOutcome()
    {
        if (!Multiplayer.IsServer()) return;

        var allPlayers = GetAllPlayers();
        if (allPlayers.Count == 0) return;

        var livingPlayers = GetLivingPlayers();

        // If match started with 2+ players, check if only 1 remains (Last Man Standing)
        if (allPlayers.Count >= 2)
        {
            if (livingPlayers.Count == 1)
            {
                GD.Print($"[GameManager] BattleRoyale Last Shopper Standing: '{livingPlayers[0].PlayerName}' won! (Total players: {allPlayers.Count})");
                EndGame(livingPlayers[0].PlayerName, false);
                return;
            }
            else if (livingPlayers.Count == 0)
            {
                GD.Print($"[GameManager] BattleRoyale All Shoppers Eliminated (Draw). Total players: {allPlayers.Count}");
                EndGame("", true);
                return;
            }
        }
        else if (allPlayers.Count == 1)
        {
            // Solo play / test mode
            if (livingPlayers.Count == 0)
            {
                EndGame("", true);
                return;
            }
        }

        // Check if Battle Royale timer ran out
        if (TimeRemaining <= 0f)
        {
            TimeRemaining = 0f;

            if (livingPlayers.Count == 1)
            {
                EndGame(livingPlayers[0].PlayerName, false);
            }
            else if (livingPlayers.Count > 1)
            {
                // Winner decided by highest remaining health
                PlayerController highestHpPlayer = livingPlayers[0];
                bool tied = false;

                for (int i = 1; i < livingPlayers.Count; i++)
                {
                    Health curH = livingPlayers[i].Health ?? livingPlayers[i].GetNodeOrNull<Health>("Health");
                    Health topH = highestHpPlayer.Health ?? highestHpPlayer.GetNodeOrNull<Health>("Health");
                    int curHp = curH != null ? curH.CurrentHealth : 0;
                    int topHp = topH != null ? topH.CurrentHealth : 0;
                    if (curHp > topHp)
                    {
                        highestHpPlayer = livingPlayers[i];
                        tied = false;
                    }
                    else if (curHp == topHp)
                    {
                        tied = true;
                    }
                }

                if (tied)
                {
                    EndGame("TIE", true);
                }
                else
                {
                    EndGame(highestHpPlayer.PlayerName, false);
                }
            }
            else
            {
                EndGame("", true);
            }
        }
    }

    public System.Collections.Generic.List<PlayerController> GetAllPlayers()
    {
        var list = new System.Collections.Generic.List<PlayerController>();
        if (Engine.GetMainLoop() is SceneTree tree)
        {
            foreach (Node node in tree.GetNodesInGroup("Players"))
            {
                if (node is PlayerController pc && GodotObject.IsInstanceValid(pc) && pc.IsInsideTree())
                {
                    list.Add(pc);
                }
            }
        }
        return list;
    }

    public System.Collections.Generic.List<PlayerController> GetLivingPlayers()
    {
        var list = new System.Collections.Generic.List<PlayerController>();
        if (Engine.GetMainLoop() is SceneTree tree)
        {
            foreach (Node node in tree.GetNodesInGroup("Players"))
            {
                if (node is PlayerController pc && GodotObject.IsInstanceValid(pc) && pc.IsInsideTree())
                {
                    Health health = pc.Health ?? pc.GetNodeOrNull<Health>("Health");
                    if (health != null && !health.IsDead && health.CurrentHealth > 0)
                    {
                        list.Add(pc);
                    }
                }
            }
        }
        return list;
    }

    public void StartShoppingTransition()
    {
        if (!Multiplayer.IsServer()) return;

        WinnerName = "";
        IsDraw = false;

        if (Multiplayer.HasMultiplayerPeer())
        {
            Rpc(nameof(RpcSyncState), (int)GamePhase.ShoppingTransition, ShoppingTransitionDuration, "", false);
        }
        else
        {
            RpcSyncState((int)GamePhase.ShoppingTransition, ShoppingTransitionDuration, "", false);
        }
        GD.Print($"[GameManager] PRE-PHASE 1: SHOPPING TRANSITION STARTED ({ShoppingTransitionDuration}s)!");
    }

    public void StartShoppingPhase()
    {
        if (!Multiplayer.IsServer()) return;

        if (CurrentPhase == GamePhase.Lobby)
        {
            StartShoppingTransition();
            return;
        }

        WinnerName = "";
        IsDraw = false;

        if (Multiplayer.HasMultiplayerPeer())
        {
            Rpc(nameof(RpcSyncState), (int)GamePhase.Shopping, ShoppingDuration, "", false);
        }
        else
        {
            RpcSyncState((int)GamePhase.Shopping, ShoppingDuration, "", false);
        }
        GD.Print("[GameManager] PHASE 1: SHOPPING STARTED!");
    }

    public void StartBattleTransition()
    {
        if (!Multiplayer.IsServer()) return;

        if (Multiplayer.HasMultiplayerPeer())
        {
            Rpc(nameof(RpcSyncState), (int)GamePhase.BattleTransition, BattleTransitionDuration, "", false);
        }
        else
        {
            RpcSyncState((int)GamePhase.BattleTransition, BattleTransitionDuration, "", false);
        }
        GD.Print($"[GameManager] PRE-PHASE 2: BATTLE ROYALE TRANSITION STARTED ({BattleTransitionDuration}s)!");
    }

    public void StartBattleRoyalePhase()
    {
        if (!Multiplayer.IsServer()) return;

        if (CurrentPhase == GamePhase.Shopping)
        {
            StartBattleTransition();
            return;
        }

        if (Multiplayer.HasMultiplayerPeer())
        {
            Rpc(nameof(RpcSyncState), (int)GamePhase.BattleRoyale, BattleDuration, "", false);
        }
        else
        {
            RpcSyncState((int)GamePhase.BattleRoyale, BattleDuration, "", false);
        }
        GD.Print("[GameManager] PHASE 2: BATTLE ROYALE STARTED!");
    }

    public void EndGame(string winnerName, bool isDraw)
    {
        if (!Multiplayer.IsServer()) return;
        if (CurrentPhase == GamePhase.GameOver) return;

        WinnerName = winnerName;
        IsDraw = isDraw;

        if (Multiplayer.HasMultiplayerPeer())
        {
            Rpc(nameof(RpcSyncGameOver), winnerName, isDraw);
        }
        else
        {
            RpcSyncGameOver(winnerName, isDraw);
        }
        GD.Print($"[GameManager] PHASE 3: GAME OVER! Winner: '{(isDraw ? "DRAW" : winnerName)}'");
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
    private void RpcRequestSyncState()
    {
        if (!Multiplayer.IsServer()) return;
        long senderId = Multiplayer.GetRemoteSenderId();
        RpcId(senderId, nameof(RpcSyncState), (int)CurrentPhase, TimeRemaining, WinnerName, IsDraw);
        AmbientEventManager.Instance?.SyncStateToClient(senderId);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    private void RpcSyncState(int phaseIndex, float serverTimeRemaining, string winnerName = "", bool isDraw = false)
    {
        GamePhase newPhase = (GamePhase)phaseIndex;
        WinnerName = winnerName;
        IsDraw = isDraw;

        if (CurrentPhase != newPhase)
        {
            CurrentPhase = newPhase;

            if (newPhase == GamePhase.Lobby)
            {
                AmbientEventManager.Instance?.ResetEvents();
            }

            if (newPhase == GamePhase.ShoppingTransition)
            {
                AudioStream chime = ShoppingTransitionSound ?? TransitionChime;
                if (chime != null)
                {
                    _audioPlayer.Stream = chime;
                    _audioPlayer.Play();
                }
            }
            else if (newPhase == GamePhase.BattleTransition)
            {
                AudioStream chime = BattleTransitionSound ?? TransitionChime;
                if (chime != null)
                {
                    _audioPlayer.Stream = chime;
                    _audioPlayer.Play();
                }
            }
            else if (newPhase == GamePhase.BattleRoyale && BattleRoyaleSound != null)
            {
                _audioPlayer.Stream = BattleRoyaleSound;
                _audioPlayer.Play();
            }

            if (newPhase == GamePhase.GameOver)
            {
                AmbientEventManager.Instance?.ResetEvents();
            }

            PlayMusicForPhase(newPhase);

            EmitSignal(SignalName.GamePhaseChanged);
            GD.Print($"[GameManager] Phase synced to: {CurrentPhase} (Winner: {WinnerName})");
        }

        TimeRemaining = serverTimeRemaining;
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    private void RpcSyncGameOver(string winnerName, bool isDraw)
    {
        WinnerName = winnerName;
        IsDraw = isDraw;
        CurrentPhase = GamePhase.GameOver;
        TimeRemaining = 0f;

        AmbientEventManager.Instance?.ResetEvents();
        PlayMusicForPhase(GamePhase.GameOver);

        EmitSignal(SignalName.GamePhaseChanged);
        EmitSignal(SignalName.GameOverDeclared, winnerName, isDraw);
        GD.Print($"[GameManager] GameOver Synced! Winner: '{winnerName}', IsDraw: {isDraw}");
    }

    private void AddTrackIfValid(Godot.Collections.Array<AudioStream> playlist, string resPath)
    {
        if (string.IsNullOrEmpty(resPath)) return;
        if (ResourceLoader.Exists(resPath) || FileAccess.FileExists(resPath))
        {
            try
            {
                AudioStream stream = GD.Load<AudioStream>(resPath);
                if (stream != null && !playlist.Contains(stream))
                {
                    DisableStreamLoop(stream);
                    playlist.Add(stream);
                }
            }
            catch (System.Exception ex)
            {
                GD.PrintErr($"[GameManager] Failed to load audio track '{resPath}': {ex.Message}");
            }
        }
    }

    private static void DisableStreamLoop(AudioStream stream)
    {
        if (stream is AudioStreamMP3 mp3)
        {
            mp3.Loop = false;
        }
        else if (stream is AudioStreamOggVorbis ogg)
        {
            ogg.Loop = false;
        }
    }

    private void OnMusicTrackFinished()
    {
        if (_activePlaylist == null || _activePlaylist.Count == 0) return;
        AdvancePlaylist(0.3f);
    }

    public void AdvancePlaylist(float fadeDuration = 0.5f)
    {
        if (_activePlaylist == null || _activePlaylist.Count == 0) return;

        if (_activePlaylist.Count == 1)
        {
            _currentTrackIndex = 0;
        }
        else if (ShufflePlaylists)
        {
            int nextIndex;
            int attempts = 0;
            do
            {
                nextIndex = (int)(GD.Randi() % (uint)_activePlaylist.Count);
                attempts++;
            } while (nextIndex == _currentTrackIndex && attempts < 10);
            _currentTrackIndex = nextIndex;
        }
        else
        {
            _currentTrackIndex = (_currentTrackIndex + 1) % _activePlaylist.Count;
        }

        AudioStream nextTrack = _activePlaylist[_currentTrackIndex];
        PlayMusicTrack(nextTrack, fadeDuration);
    }

    public void PlayMusicForPhase(GamePhase phase)
    {
        Godot.Collections.Array<AudioStream> targetPlaylist = null;
        float fade = 0.5f;

        switch (phase)
        {
            case GamePhase.Lobby:
            case GamePhase.ShoppingTransition:
            case GamePhase.Shopping:
            case GamePhase.BattleTransition:
                targetPlaylist = LobbyPlaylist;
                fade = 1.0f;
                break;

            case GamePhase.BattleRoyale:
                targetPlaylist = BattleRoyalePlaylist;
                fade = 0.4f;
                break;

            case GamePhase.GameOver:
                _activePlaylist = null;
                FadeOutMusic(1.5f);
                return;
        }

        if (targetPlaylist == null || targetPlaylist.Count == 0)
        {
            FadeOutMusic(0.5f);
            return;
        }

        // If transitioning between Lobby and Shopping (both use LobbyPlaylist) and a track is already playing, keep it playing smoothly!
        if (_activePlaylist == targetPlaylist && _musicPlayer != null && _musicPlayer.Playing)
        {
            return;
        }

        _activePlaylist = targetPlaylist;
        _currentTrackIndex = (ShufflePlaylists && _activePlaylist.Count > 1)
            ? (int)(GD.Randi() % (uint)_activePlaylist.Count)
            : 0;

        AudioStream selectedTrack = _activePlaylist[_currentTrackIndex];
        PlayMusicTrack(selectedTrack, fade);
    }

    public void PlayMusicTrack(AudioStream track, float fadeDuration = 0.5f)
    {
        if (track == null || _musicPlayer == null) return;

        if (_musicPlayer.Stream == track && _musicPlayer.Playing)
        {
            if (_musicTween != null && _musicTween.IsValid())
            {
                _musicTween.Kill();
            }
            _musicPlayer.VolumeDb = MusicVolumeDb;
            return;
        }

        if (_musicTween != null && _musicTween.IsValid())
        {
            _musicTween.Kill();
        }

        if (_musicPlayer.Playing && fadeDuration > 0f)
        {
            _musicTween = CreateTween();
            _musicTween.TweenProperty(_musicPlayer, "volume_db", -80.0f, fadeDuration * 0.5f);
            _musicTween.TweenCallback(Callable.From(() =>
            {
                _musicPlayer.Stream = track;
                _musicPlayer.Play();
                Tween inTween = CreateTween();
                inTween.TweenProperty(_musicPlayer, "volume_db", MusicVolumeDb, fadeDuration * 0.5f);
            }));
        }
        else
        {
            _musicPlayer.Stream = track;
            _musicPlayer.VolumeDb = MusicVolumeDb;
            _musicPlayer.Play();
        }
    }

    public void FadeOutMusic(float duration = 1.0f)
    {
        if (_musicPlayer == null || !_musicPlayer.Playing) return;

        if (_musicTween != null && _musicTween.IsValid())
        {
            _musicTween.Kill();
        }

        _musicTween = CreateTween();
        _musicTween.TweenProperty(_musicPlayer, "volume_db", -80.0f, duration);
        _musicTween.TweenCallback(Callable.From(() =>
        {
            _musicPlayer.Stop();
            _musicPlayer.Stream = null;
        }));
    }

    public void RestartGame()
    {
        if (!Multiplayer.IsServer()) return;
        GD.Print("[GameManager] Restarting Game match...");
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.RestartMatch();
        }
        else
        {
            GetTree().ChangeSceneToFile("res://Scenes/StoreInterior.tscn");
        }
    }

    public void SyncStateToPlayer(long peerId)
    {
        if (!Multiplayer.IsServer()) return;

        RpcId(peerId, nameof(RpcSyncState), (int)CurrentPhase, TimeRemaining, WinnerName, IsDraw);
        AmbientEventManager.Instance?.SyncStateToClient(peerId);
    }

    [Signal]
    public delegate void GamePhaseChangedEventHandler();

    [Signal]
    public delegate void GameOverDeclaredEventHandler(string winnerName, bool isDraw);
}