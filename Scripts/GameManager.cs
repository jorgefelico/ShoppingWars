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

    [ExportGroup("Ceiling Speakers")]
    [Export] public bool UseCeilingSpeakers = true;
    [Export] public int CeilingSpeakerCount = 16;
    [Export] public float CeilingSpeakerHeight = 8.7f;
    [Export] public float SpeakerVolumeDb = -6.0f;
    [Export] public float SpeakerTransitionSoundVolumeDb = 4.0f;
    [Export] public float DuckedMusicVolumeDb = -34.0f;
    [Export] public float MusicDuckFadeDuration = 0.2f;
    [Export] public float MusicRestoreFadeDuration = 0.8f;
    [Export] public float SpeakerUnitSize = 12.0f;
    [Export] public float SpeakerMaxDistance = 50.0f;
    [Export] public float SpeakerPanningStrength = 0.7f;
    [Export] public int SpeakerPlacementSeed = 42;
    [Export] public PackedScene CeilingSpeakerPrefab;

    public float EffectiveMusicVolumeDb => (_ceilingSpeakers.Count > 0 && UseCeilingSpeakers) ? SpeakerVolumeDb : MusicVolumeDb;

    private AudioStreamPlayer _audioPlayer;
    private AudioStreamPlayer _musicPlayer;
    private readonly System.Collections.Generic.List<AudioStreamPlayer3D> _ceilingSpeakers = new();
    private readonly System.Collections.Generic.List<AudioStreamPlayer3D> _ceilingSpeakerSFX = new();
    private Node3D _ceilingSpeakersContainer;
    private Tween _musicTween;
    private Tween _duckTween;
    private bool _isMusicDucked = false;
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

        if (CeilingSpeakerPrefab == null)
        {
            if (ResourceLoader.Exists("res://Prefabs/CeilingSpeaker.tscn") || FileAccess.FileExists("res://Prefabs/CeilingSpeaker.tscn"))
            {
                CeilingSpeakerPrefab = GD.Load<PackedScene>("res://Prefabs/CeilingSpeaker.tscn");
            }
        }

        if (UseCeilingSpeakers)
        {
            InitializeCeilingSpeakers();
        }

        // Only create 2D music player if ceiling speakers are not used or not available
        if (_ceilingSpeakers.Count == 0)
        {
            _musicPlayer = new AudioStreamPlayer();
            _musicPlayer.Bus = "Master";
            _musicPlayer.VolumeDb = MusicVolumeDb;
            _musicPlayer.Finished += OnMusicTrackFinished;
            AddChild(_musicPlayer);
        }

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
        if (_ceilingSpeakers.Count > 0)
        {
            _ceilingSpeakers[0].Finished -= OnMusicTrackFinished;
        }
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
                    PlaySoundOnSpeakers(chime, SpeakerTransitionSoundVolumeDb, duckMusic: true);
                }
            }
            else if (newPhase == GamePhase.BattleTransition)
            {
                AudioStream chime = BattleTransitionSound ?? TransitionChime;
                if (chime != null)
                {
                    PlaySoundOnSpeakers(chime, SpeakerTransitionSoundVolumeDb, duckMusic: true);
                }
            }
            else if (newPhase == GamePhase.BattleRoyale && BattleRoyaleSound != null)
            {
                PlaySoundOnSpeakers(BattleRoyaleSound, SpeakerTransitionSoundVolumeDb, duckMusic: true);
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

        bool isMusicPlaying = (_ceilingSpeakers.Count > 0)
            ? _ceilingSpeakers[0].Playing
            : (_musicPlayer != null && _musicPlayer.Playing);

        // If transitioning between Lobby and Shopping (both use LobbyPlaylist) and a track is already playing, keep it playing smoothly!
        if (_activePlaylist == targetPlaylist && isMusicPlaying)
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
        if (track == null) return;
        if (_ceilingSpeakers.Count == 0 && _musicPlayer == null) return;

        bool isPlayingThisTrack = (_ceilingSpeakers.Count > 0)
            ? (_ceilingSpeakers[0].Stream == track && _ceilingSpeakers[0].Playing)
            : (_musicPlayer != null && _musicPlayer.Stream == track && _musicPlayer.Playing);

        if (isPlayingThisTrack)
        {
            if (_musicTween != null && _musicTween.IsValid())
            {
                _musicTween.Kill();
            }
            if (!_isMusicDucked)
            {
                SetMusicVolume(EffectiveMusicVolumeDb);
            }
            return;
        }

        if (_musicTween != null && _musicTween.IsValid())
        {
            _musicTween.Kill();
        }

        bool anyPlaying = (_ceilingSpeakers.Count > 0)
            ? _ceilingSpeakers[0].Playing
            : (_musicPlayer != null && _musicPlayer.Playing);

        if (anyPlaying && fadeDuration > 0f)
        {
            float halfFade = fadeDuration * 0.5f;
            _musicTween = CreateTween();
            _musicTween.SetParallel(true);

            if (_ceilingSpeakers.Count > 0)
            {
                foreach (var speaker in _ceilingSpeakers)
                {
                    if (GodotObject.IsInstanceValid(speaker) && speaker.IsInsideTree())
                    {
                        _musicTween.TweenProperty(speaker, "volume_db", -80.0f, halfFade);
                    }
                }
            }
            if (_musicPlayer != null && GodotObject.IsInstanceValid(_musicPlayer) && _musicPlayer.IsInsideTree())
            {
                _musicTween.TweenProperty(_musicPlayer, "volume_db", -80.0f, halfFade);
            }

            _musicTween.Chain().TweenCallback(Callable.From(() =>
            {
                StartSpeakersTrack(track);
                Tween inTween = CreateTween();
                inTween.SetParallel(true);
                float targetVol = _isMusicDucked ? DuckedMusicVolumeDb : EffectiveMusicVolumeDb;

                if (_ceilingSpeakers.Count > 0)
                {
                    foreach (var speaker in _ceilingSpeakers)
                    {
                        if (GodotObject.IsInstanceValid(speaker) && speaker.IsInsideTree())
                        {
                            inTween.TweenProperty(speaker, "volume_db", targetVol, halfFade);
                        }
                    }
                }
                if (_musicPlayer != null && GodotObject.IsInstanceValid(_musicPlayer) && _musicPlayer.IsInsideTree())
                {
                    inTween.TweenProperty(_musicPlayer, "volume_db", targetVol, halfFade);
                }
            }));
        }
        else
        {
            StartSpeakersTrack(track);
            SetMusicVolume(_isMusicDucked ? DuckedMusicVolumeDb : EffectiveMusicVolumeDb);
        }
    }

    private void StartSpeakersTrack(AudioStream track)
    {
        if (_ceilingSpeakers.Count > 0)
        {
            foreach (var speaker in _ceilingSpeakers)
            {
                if (GodotObject.IsInstanceValid(speaker))
                {
                    speaker.Stream = track;
                    speaker.Play();
                }
            }
        }
        if (_musicPlayer != null && _ceilingSpeakers.Count == 0 && GodotObject.IsInstanceValid(_musicPlayer))
        {
            _musicPlayer.Stream = track;
            _musicPlayer.Play();
        }
    }

    private void SetMusicVolume(float volumeDb)
    {
        foreach (var speaker in _ceilingSpeakers)
        {
            if (GodotObject.IsInstanceValid(speaker))
            {
                speaker.VolumeDb = volumeDb;
            }
        }
        if (_musicPlayer != null && GodotObject.IsInstanceValid(_musicPlayer))
        {
            _musicPlayer.VolumeDb = volumeDb;
        }
    }

    public void FadeOutMusic(float duration = 1.0f)
    {
        _isMusicDucked = false;
        if (_duckTween != null && _duckTween.IsValid())
        {
            _duckTween.Kill();
        }

        bool anyPlaying = (_ceilingSpeakers.Count > 0)
            ? _ceilingSpeakers[0].Playing
            : (_musicPlayer != null && _musicPlayer.Playing);

        if (!anyPlaying) return;

        if (_musicTween != null && _musicTween.IsValid())
        {
            _musicTween.Kill();
        }

        _musicTween = CreateTween();
        _musicTween.SetParallel(true);

        if (_ceilingSpeakers.Count > 0)
        {
            foreach (var speaker in _ceilingSpeakers)
            {
                if (GodotObject.IsInstanceValid(speaker) && speaker.IsInsideTree())
                {
                    _musicTween.TweenProperty(speaker, "volume_db", -80.0f, duration);
                }
            }
        }
        if (_musicPlayer != null && GodotObject.IsInstanceValid(_musicPlayer) && _musicPlayer.IsInsideTree())
        {
            _musicTween.TweenProperty(_musicPlayer, "volume_db", -80.0f, duration);
        }

        _musicTween.Chain().TweenCallback(Callable.From(() =>
        {
            foreach (var speaker in _ceilingSpeakers)
            {
                speaker.Stop();
                speaker.Stream = null;
            }
            if (_musicPlayer != null)
            {
                _musicPlayer.Stop();
                _musicPlayer.Stream = null;
            }
        }));
    }

    private void InitializeCeilingSpeakers()
    {
        if (!UseCeilingSpeakers) return;

        _ceilingSpeakers.Clear();
        _ceilingSpeakerSFX.Clear();

        // 1. Check if there are already speaker instances placed in the scene tree
        var existingSpeakers = GetTree().GetNodesInGroup("CeilingSpeakers");
        if (existingSpeakers.Count > 0)
        {
            foreach (var node in existingSpeakers)
            {
                AudioStreamPlayer3D player = node as AudioStreamPlayer3D ?? node.GetNodeOrNull<AudioStreamPlayer3D>("AudioPlayer");
                if (player != null && !_ceilingSpeakers.Contains(player))
                {
                    _ceilingSpeakers.Add(player);
                }

                AudioStreamPlayer3D sfx = node.GetNodeOrNull<AudioStreamPlayer3D>("SFXPlayer");
                if (sfx != null && !_ceilingSpeakerSFX.Contains(sfx))
                {
                    _ceilingSpeakerSFX.Add(sfx);
                }
            }
            if (_ceilingSpeakers.Count > 0)
            {
                SetupSpeakerPlayers();
                return;
            }
        }

        // 2. Locate or create CeilingSpeakers container in store Interior
        Node storeRoot = GetTree().CurrentScene ?? GetParent();
        Node interior = storeRoot?.FindChild("Interior", true, false) ?? storeRoot;
        if (interior == null) return;

        _ceilingSpeakersContainer = interior.FindChild("CeilingSpeakers", false, false) as Node3D;
        if (_ceilingSpeakersContainer == null)
        {
            _ceilingSpeakersContainer = new Node3D { Name = "CeilingSpeakers" };
            interior.AddChild(_ceilingSpeakersContainer);
        }

        // Check if container already has children
        foreach (Node child in _ceilingSpeakersContainer.GetChildren())
        {
            AudioStreamPlayer3D player = child as AudioStreamPlayer3D ?? child.GetNodeOrNull<AudioStreamPlayer3D>("AudioPlayer");
            if (player != null && !_ceilingSpeakers.Contains(player))
            {
                _ceilingSpeakers.Add(player);
            }

            AudioStreamPlayer3D sfx = child.GetNodeOrNull<AudioStreamPlayer3D>("SFXPlayer");
            if (sfx != null && !_ceilingSpeakerSFX.Contains(sfx))
            {
                _ceilingSpeakerSFX.Add(sfx);
            }
        }

        if (_ceilingSpeakers.Count > 0)
        {
            SetupSpeakerPlayers();
            return;
        }

        // 3. Generate randomized ceiling speakers throughout store interior
        SpawnRandomCeilingSpeakers();
    }

    private void SpawnRandomCeilingSpeakers()
    {
        RandomNumberGenerator rand = new();
        if (SpeakerPlacementSeed != 0)
        {
            rand.Seed = (ulong)SpeakerPlacementSeed;
        }
        else
        {
            rand.Randomize();
        }

        int cols = 4;
        int rows = 4;
        float xMin = -65.0f;
        float xMax = 65.0f;
        float zMin = -50.0f;
        float zMax = 50.0f;

        float cellWidth = (xMax - xMin) / cols;
        float cellDepth = (zMax - zMin) / rows;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                float cellCenterX = xMin + (c + 0.5f) * cellWidth;
                float cellCenterZ = zMin + (r + 0.5f) * cellDepth;

                float jitterX = rand.RandfRange(-cellWidth * 0.25f, cellWidth * 0.25f);
                float jitterZ = rand.RandfRange(-cellDepth * 0.25f, cellDepth * 0.25f);

                Vector3 pos = new Vector3(cellCenterX + jitterX, CeilingSpeakerHeight, cellCenterZ + jitterZ);

                Node3D speakerNode = null;
                AudioStreamPlayer3D player = null;
                AudioStreamPlayer3D sfxPlayer = null;

                if (CeilingSpeakerPrefab != null)
                {
                    speakerNode = CeilingSpeakerPrefab.Instantiate<Node3D>();
                    player = speakerNode.GetNodeOrNull<AudioStreamPlayer3D>("AudioPlayer") ?? (speakerNode as AudioStreamPlayer3D);
                    sfxPlayer = speakerNode.GetNodeOrNull<AudioStreamPlayer3D>("SFXPlayer");
                }

                if (speakerNode == null)
                {
                    speakerNode = CreateProceduralSpeakerNode(out player, out sfxPlayer);
                }

                speakerNode.Name = $"CeilingSpeaker_{r * cols + c + 1}";
                speakerNode.AddToGroup("CeilingSpeakers");
                _ceilingSpeakersContainer.AddChild(speakerNode);
                speakerNode.Position = pos;

                if (player != null)
                {
                    ConfigureSpeakerAudioPlayer(player);
                    _ceilingSpeakers.Add(player);
                }

                if (sfxPlayer != null)
                {
                    ConfigureSpeakerSFXPlayer(sfxPlayer);
                    _ceilingSpeakerSFX.Add(sfxPlayer);
                }
            }
        }

        SetupSpeakerPlayers();
        GD.Print($"[GameManager] Spawned {_ceilingSpeakers.Count} randomized ceiling speakers (SFX: {_ceilingSpeakerSFX.Count}) across store interior.");
    }

    private Node3D CreateProceduralSpeakerNode(out AudioStreamPlayer3D player, out AudioStreamPlayer3D sfxPlayer)
    {
        Node3D speakerRoot = new Node3D();

        MeshInstance3D stem = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.025f, BottomRadius = 0.025f, Height = 0.35f },
            Position = new Vector3(0, -0.175f, 0)
        };
        speakerRoot.AddChild(stem);

        MeshInstance3D housing = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.16f, BottomRadius = 0.32f, Height = 0.22f },
            Position = new Vector3(0, -0.44f, 0)
        };
        speakerRoot.AddChild(housing);

        MeshInstance3D grill = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.3f, BottomRadius = 0.3f, Height = 0.03f },
            Position = new Vector3(0, -0.55f, 0)
        };
        speakerRoot.AddChild(grill);

        player = new AudioStreamPlayer3D
        {
            Name = "AudioPlayer",
            Position = new Vector3(0, -0.56f, 0)
        };
        speakerRoot.AddChild(player);

        sfxPlayer = new AudioStreamPlayer3D
        {
            Name = "SFXPlayer",
            Position = new Vector3(0, -0.56f, 0)
        };
        speakerRoot.AddChild(sfxPlayer);

        return speakerRoot;
    }

    private void ConfigureSpeakerAudioPlayer(AudioStreamPlayer3D player)
    {
        player.Bus = "Master";
        player.AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance;
        player.UnitSize = SpeakerUnitSize;
        player.MaxDistance = SpeakerMaxDistance;
        player.PanningStrength = SpeakerPanningStrength;
        player.DopplerTracking = AudioStreamPlayer3D.DopplerTrackingEnum.Disabled;
        player.MaxPolyphony = 1;
        player.VolumeDb = EffectiveMusicVolumeDb;
    }

    private void ConfigureSpeakerSFXPlayer(AudioStreamPlayer3D sfxPlayer)
    {
        sfxPlayer.Bus = "Master";
        sfxPlayer.AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance;
        sfxPlayer.UnitSize = SpeakerUnitSize + 2.0f;
        sfxPlayer.MaxDistance = SpeakerMaxDistance + 10.0f;
        sfxPlayer.PanningStrength = SpeakerPanningStrength;
        sfxPlayer.DopplerTracking = AudioStreamPlayer3D.DopplerTrackingEnum.Disabled;
        sfxPlayer.MaxPolyphony = 2;
        sfxPlayer.VolumeDb = SpeakerTransitionSoundVolumeDb;
    }

    private void SetupSpeakerPlayers()
    {
        if (_ceilingSpeakers.Count == 0) return;

        foreach (var speaker in _ceilingSpeakers)
        {
            ConfigureSpeakerAudioPlayer(speaker);
        }

        foreach (var sfx in _ceilingSpeakerSFX)
        {
            ConfigureSpeakerSFXPlayer(sfx);
        }

        _ceilingSpeakers[0].Finished += OnMusicTrackFinished;
    }

    public void PlaySoundOnSpeakers(AudioStream sound, float volumeDb = float.NaN, bool duckMusic = false)
    {
        if (sound == null) return;

        float targetVol = float.IsNaN(volumeDb) ? SpeakerTransitionSoundVolumeDb : volumeDb;

        if (UseCeilingSpeakers && _ceilingSpeakerSFX.Count > 0)
        {
            foreach (var sfx in _ceilingSpeakerSFX)
            {
                if (GodotObject.IsInstanceValid(sfx) && sfx.IsInsideTree())
                {
                    sfx.VolumeDb = targetVol;
                    sfx.Stream = sound;
                    sfx.Play();
                }
            }
        }
        else if (UseCeilingSpeakers && _ceilingSpeakers.Count > 0)
        {
            // Fallback if SFXPlayer wasn't found on speakers
            foreach (var speaker in _ceilingSpeakers)
            {
                if (GodotObject.IsInstanceValid(speaker) && speaker.IsInsideTree())
                {
                    speaker.Stream = sound;
                    speaker.Play();
                }
            }
        }
        else if (_audioPlayer != null && GodotObject.IsInstanceValid(_audioPlayer) && _audioPlayer.IsInsideTree())
        {
            _audioPlayer.VolumeDb = targetVol;
            _audioPlayer.Stream = sound;
            _audioPlayer.Play();
        }

        if (duckMusic)
        {
            DuckMusicForSound(sound);
        }
    }

    public void DuckMusicForSound(AudioStream sound, float extraHoldTime = 0.25f)
    {
        if (sound == null) return;
        float soundLength = (float)sound.GetLength();
        if (soundLength <= 0.1f) soundLength = 3.0f;
        DuckMusic(soundLength + extraHoldTime);
    }

    public void DuckMusic(float duration)
    {
        _isMusicDucked = true;

        if (_duckTween != null && _duckTween.IsValid())
        {
            _duckTween.Kill();
        }

        _duckTween = CreateTween();
        _duckTween.SetParallel(true);

        if (_ceilingSpeakers.Count > 0)
        {
            foreach (var speaker in _ceilingSpeakers)
            {
                if (GodotObject.IsInstanceValid(speaker) && speaker.IsInsideTree())
                {
                    _duckTween.TweenProperty(speaker, "volume_db", DuckedMusicVolumeDb, MusicDuckFadeDuration);
                }
            }
        }
        if (_musicPlayer != null && GodotObject.IsInstanceValid(_musicPlayer) && _musicPlayer.IsInsideTree())
        {
            _duckTween.TweenProperty(_musicPlayer, "volume_db", DuckedMusicVolumeDb, MusicDuckFadeDuration);
        }

        float holdDuration = Mathf.Max(0.01f, duration - MusicDuckFadeDuration);
        _duckTween.Chain().TweenInterval(holdDuration);
        _duckTween.Chain().TweenCallback(Callable.From(() =>
        {
            RestoreMusicVolume(MusicRestoreFadeDuration);
        }));
    }

    public void RestoreMusicVolume(float fadeDuration = 0.8f)
    {
        _isMusicDucked = false;

        if (_duckTween != null && _duckTween.IsValid())
        {
            _duckTween.Kill();
        }

        _duckTween = CreateTween();
        _duckTween.SetParallel(true);

        float targetVol = EffectiveMusicVolumeDb;

        if (_ceilingSpeakers.Count > 0)
        {
            foreach (var speaker in _ceilingSpeakers)
            {
                if (GodotObject.IsInstanceValid(speaker) && speaker.IsInsideTree())
                {
                    _duckTween.TweenProperty(speaker, "volume_db", targetVol, fadeDuration);
                }
            }
        }
        if (_musicPlayer != null && GodotObject.IsInstanceValid(_musicPlayer) && _musicPlayer.IsInsideTree())
        {
            _duckTween.TweenProperty(_musicPlayer, "volume_db", targetVol, fadeDuration);
        }
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