using System;
using System.Collections.Generic;
using Godot;

public partial class ManagerAnnouncer : Node
{
    private static ManagerAnnouncer _instance;
    public static ManagerAnnouncer Instance
    {
        get => GodotObject.IsInstanceValid(_instance) ? _instance : null;
        private set => _instance = value;
    }

    [Signal]
    public delegate void ManagerAnnouncedEventHandler(string line, int emotionIndex, float duration);

    [Export] public AudioStream IntercomChime;
    [Export] public bool EnableVoiceAudio = true;
    [Export] public float DefaultAnnouncementDuration = 4.5f;
    [Export] public float MinimumIntervalBetweenAnnouncements = 3.5f;

    private float _announcementTimer = 0f;
    private int _currentPriority = -1;
    private float _interAnnouncementBreather = 0f;
    private readonly RandomNumberGenerator _rng = new();
    private bool _firstBloodOccurred = false;
    private int _recentKillsInWindow = 0;
    private float _killStreakWindow = 0f;

    private readonly List<QueuedAnnouncement> _queue = new();
    private AudioStreamPlayer _voicePlayer;
    private AudioStreamPlayer _chimePlayer;

    private class QueuedAnnouncement
    {
        public string Text;
        public ManagerEmotion Emotion;
        public string AudioClipPath;
        public float Duration;
        public int Priority;
        public float TimeEnqueued;
    }

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _ExitTree()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    public override void _Ready()
    {
        Instance = this;
        _rng.Randomize();

        SettingsManager.EnsureAudioBuses();

        _voicePlayer = new AudioStreamPlayer { Name = "VoicePlayer", Bus = "SFX", VolumeDb = 3.5f };
        AddChild(_voicePlayer);

        _chimePlayer = new AudioStreamPlayer { Name = "ChimePlayer", Bus = "SFX", VolumeDb = 0.5f };
        AddChild(_chimePlayer);

        if (IntercomChime == null)
        {
            string chimePath = ResolveAudioFile("res://Sounds/store_chime.wav");
            if (chimePath != null)
            {
                IntercomChime = GD.Load<AudioStream>(chimePath);
            }
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // Press F2 to instantly test Mr. Henderson voice announcement
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo && keyEvent.Keycode == Key.F2)
        {
            AnnouncePhase(GamePhase.Lobby);
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (_announcementTimer > 0f)
        {
            _announcementTimer -= dt;
            if (_announcementTimer <= 0f)
            {
                _currentPriority = -1;
                _interAnnouncementBreather = 0.35f; // Small natural pause between lines
            }
        }
        else if (_interAnnouncementBreather > 0f)
        {
            _interAnnouncementBreather -= dt;
            if (_interAnnouncementBreather <= 0f)
            {
                ProcessNextQueuedAnnouncement();
            }
        }

        if (_killStreakWindow > 0f)
        {
            _killStreakWindow -= dt;
            if (_killStreakWindow <= 0f)
            {
                _recentKillsInWindow = 0;
            }
        }
    }

    public void Announce(string text, ManagerEmotion emotion = ManagerEmotion.Neutral, string audioClipPath = null, float duration = 4.5f, int priority = 1)
    {
        // 1. If currently playing an announcement:
        if (_announcementTimer > 0f)
        {
            // If incoming is Critical (priority 4) and currently playing is strictly lower priority (< 4),
            // interrupt it cleanly so critical match announcements (e.g. Combat Permitted or Game Over) are immediate!
            if (priority >= 4 && _currentPriority < 4)
            {
                GD.Print($"[Mr. Henderson] Critical announcement (P{priority}) interrupting lower priority (P{_currentPriority}): \"{text}\"");
                StopCurrentAudio();
                PlayAnnouncement(text, emotion, audioClipPath, duration, priority);
                return;
            }

            // Otherwise, do not interrupt! Enqueue it to play when the current line finishes.
            EnqueueAnnouncement(text, emotion, audioClipPath, duration, priority);
            return;
        }

        // 2. Nothing is currently playing, play immediately
        PlayAnnouncement(text, emotion, audioClipPath, duration, priority);
    }

    private void PlayAnnouncement(string text, ManagerEmotion emotion, string audioClipPath, float duration, int priority)
    {
        string resolvedVoicePath = ResolveAudioFile(audioClipPath);
        AudioStream voiceStream = null;

        if (EnableVoiceAudio && !string.IsNullOrEmpty(resolvedVoicePath))
        {
            voiceStream = GD.Load<AudioStream>(resolvedVoicePath);
        }

        // Calculate accurate display and lockout duration based on voice length if available
        float effectiveDuration = duration;
        if (voiceStream != null)
        {
            float voiceLen = (float)voiceStream.GetLength();
            if (voiceLen > 0.5f)
            {
                // 0.45s chime delay + voice duration + 0.35s natural trailing margin
                effectiveDuration = Mathf.Max(duration, voiceLen + 0.8f);
            }
        }

        _announcementTimer = effectiveDuration;
        _currentPriority = priority;

        bool useSpatialSpeakers = GameManager.Instance != null && GameManager.Instance.HasActiveCeilingSpeakers;

        // Play chime on 3D ceiling speakers (or direct 2D fallback if no speakers in scene)
        if (IntercomChime != null)
        {
            if (useSpatialSpeakers)
            {
                GameManager.Instance.PlaySoundOnSpeakers(IntercomChime, volumeDb: 4.0f, duckMusic: true);
            }
            else
            {
                if (_chimePlayer != null && GodotObject.IsInstanceValid(_chimePlayer))
                {
                    _chimePlayer.Stream = IntercomChime;
                    _chimePlayer.Play();
                }
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.DuckMusicForSound(IntercomChime, 0.5f);
                }
            }
        }

        // Play voice audio on 3D ceiling speakers (or direct 2D fallback if no speakers in scene)
        if (voiceStream != null)
        {
            int assignedPriority = priority;
            GetTree().CreateTimer(0.45f).Timeout += () =>
            {
                if (GodotObject.IsInstanceValid(this) && _announcementTimer > 0f && _currentPriority == assignedPriority)
                {
                    if (useSpatialSpeakers && GameManager.Instance != null)
                    {
                        GameManager.Instance.PlaySoundOnSpeakers(voiceStream, volumeDb: 6.0f, duckMusic: true);
                    }
                    else
                    {
                        if (_voicePlayer != null && GodotObject.IsInstanceValid(_voicePlayer))
                        {
                            _voicePlayer.Stream = voiceStream;
                            _voicePlayer.Play();
                        }
                        if (GameManager.Instance != null)
                        {
                            GameManager.Instance.DuckMusicForSound(voiceStream, 0.5f);
                        }
                    }
                    GD.Print($"[Mr. Henderson] Playing voice audio {(useSpatialSpeakers ? "via Spatial Ceiling Speakers" : "via Direct Audio")}: {resolvedVoicePath}");
                }
            };
        }
        else if (!string.IsNullOrEmpty(audioClipPath))
        {
            GD.Print($"[Mr. Henderson] Voice file not found: {audioClipPath} (playing chime only)");
        }

        EmitSignal(SignalName.ManagerAnnounced, text, (int)emotion, effectiveDuration);
        GD.Print($"[Mr. Henderson] ({emotion}) [P{priority}] \"{text}\" ({effectiveDuration:0.0}s)");
    }

    private void EnqueueAnnouncement(string text, ManagerEmotion emotion, string audioClipPath, float duration, int priority)
    {
        // Don't queue low-priority flavor lines (kills) to avoid stale backlog
        if (priority <= 1)
        {
            return;
        }

        // Check for duplicates
        foreach (var q in _queue)
        {
            if (q.Text == text)
            {
                return;
            }
        }

        // Cap queue size at 3
        if (_queue.Count >= 3)
        {
            int lowestIdx = 0;
            for (int i = 1; i < _queue.Count; i++)
            {
                if (_queue[i].Priority < _queue[lowestIdx].Priority)
                {
                    lowestIdx = i;
                }
            }

            if (priority > _queue[lowestIdx].Priority)
            {
                _queue.RemoveAt(lowestIdx);
            }
            else
            {
                return; // Drop incoming
            }
        }

        var item = new QueuedAnnouncement
        {
            Text = text,
            Emotion = emotion,
            AudioClipPath = audioClipPath,
            Duration = duration,
            Priority = priority,
            TimeEnqueued = (float)Time.GetTicksMsec() / 1000f
        };

        // Insert sorted by priority descending
        int insertIdx = _queue.Count;
        for (int i = 0; i < _queue.Count; i++)
        {
            if (priority > _queue[i].Priority)
            {
                insertIdx = i;
                break;
            }
        }
        _queue.Insert(insertIdx, item);
        GD.Print($"[Mr. Henderson] Enqueued announcement [P{priority}]: \"{text}\" (Queue size: {_queue.Count})");
    }

    private void ProcessNextQueuedAnnouncement()
    {
        if (_announcementTimer > 0f || _queue.Count == 0) return;

        float now = (float)Time.GetTicksMsec() / 1000f;
        while (_queue.Count > 0)
        {
            var next = _queue[0];
            _queue.RemoveAt(0);

            // Drop non-critical announcements that have been waiting in queue for over 10 seconds
            if (next.Priority < 4 && (now - next.TimeEnqueued) > 10.0f)
            {
                GD.Print($"[Mr. Henderson] Dropping stale queued announcement: \"{next.Text}\"");
                continue;
            }

            PlayAnnouncement(next.Text, next.Emotion, next.AudioClipPath, next.Duration, next.Priority);
            break;
        }
    }

    public void StopCurrentAudio()
    {
        if (_voicePlayer != null && GodotObject.IsInstanceValid(_voicePlayer) && _voicePlayer.Playing)
        {
            _voicePlayer.Stop();
        }
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StopSpeakerSFX();
        }
        _announcementTimer = 0f;
        _currentPriority = -1;
    }

    public void AnnouncePhase(GamePhase phase)
    {
        switch (phase)
        {
            case GamePhase.Lobby:
                _firstBloodOccurred = false;
                AnnounceRandom(LobbyLines, priority: 4);
                break;

            case GamePhase.ShoppingTransition:
                _firstBloodOccurred = false;
                // Pre-shopping countdown (5s). Store doors open and ShoppingStartLines play when Shopping phase begins!
                break;

            case GamePhase.Shopping:
                _firstBloodOccurred = false;
                AnnounceRandom(ShoppingStartLines, priority: 4);
                break;

            case GamePhase.BattleTransition:
                AnnounceRandom(LockdownLines, priority: 4);
                break;

            case GamePhase.BattleRoyale:
                AnnounceRandom(BattleStartLines, priority: 4);
                break;
        }
    }

    public void AnnounceShoppingHalfway()
    {
        AnnounceRandom(ShoppingActiveLines, priority: 2);
    }

    public void AnnounceElimination(string killer, string victim, string weapon = null)
    {
        _recentKillsInWindow++;
        _killStreakWindow = 5.0f;

        // Check for multikills
        if (_recentKillsInWindow >= 3)
        {
            Announce("TRIPLE KILL! Someone check their shopper loyalty card, they are on a rampage!", ManagerEmotion.Megaphone, "res://Sounds/henderson_spree_triple_01.wav", 4.5f, priority: 3);
            return;
        }
        else if (_recentKillsInWindow == 2)
        {
            Announce("Two down! Now that's what I call a BOGO special: Buy One, Get One FREE COFFIN!", ManagerEmotion.Greedy, "res://Sounds/henderson_spree_double_01.wav", 4.5f, priority: 3);
            return;
        }

        // First blood
        if (!_firstBloodOccurred)
        {
            _firstBloodOccurred = true;
            AnnounceRandom(FirstBloodLines, priority: 3);
            return;
        }

        // Weapon-specific roasts
        if (!string.IsNullOrEmpty(weapon))
        {
            string lowerWeapon = weapon.ToLowerInvariant();
            if (lowerWeapon.Contains("watermelon"))
            {
                Announce("Elimination via watermelon! High in fiber, lethal in blunt force trauma!", ManagerEmotion.Smug, "res://Sounds/henderson_kill_watermelon.wav", 4.5f, priority: 1);
                return;
            }
            if (lowerWeapon.Contains("baguette"))
            {
                Announce("Did somebody just get beaten with a stale baguette?! Our bakery standards are out of control!", ManagerEmotion.Annoyed, "res://Sounds/henderson_kill_baguette.wav", 4.5f, priority: 1);
                return;
            }
            if (lowerWeapon.Contains("sledgehammer") || lowerWeapon.Contains("wrench") || lowerWeapon.Contains("drill"))
            {
                Announce("Sledgehammer to the skull! Clean, brutal, and completely voids our floor warranty!", ManagerEmotion.Panicked, "res://Sounds/henderson_kill_sledgehammer.wav", 4.5f, priority: 1);
                return;
            }
            if (lowerWeapon.Contains("fryingpan") || lowerWeapon.Contains("pot"))
            {
                Announce("CLANG! That cookware dropped a shopper! Five-star non-stick performance right there!", ManagerEmotion.Greedy, "res://Sounds/henderson_kill_fryingpan.wav", 4.5f, priority: 1);
                return;
            }
            if (lowerWeapon.Contains("propane"))
            {
                Announce("EXPLOSION IN HARDWARE! Who sold them a live propane tank?! That's a direct code violation!", ManagerEmotion.Panicked, "res://Sounds/henderson_kill_propanetank.wav", 4.5f, priority: 1);
                return;
            }
        }

        // Generic elimination
        AnnounceRandom(GenericKillLines, priority: 1);
    }

    public void AnnounceAmbientEvent(string eventId)
    {
        string id = eventId.ToLowerInvariant().Replace(" ", "").Replace("_", "");
        if (id.Contains("lightsout") || id.Contains("blackout"))
        {
            Announce("Gah! Who didn't pay the electric bill?! Emergency generators on! Groomba, switch to night-vision!", ManagerEmotion.Panicked, "res://Sounds/henderson_event_lightsout.wav", 4.5f, priority: 3);
        }
        else if (id.Contains("powersurge"))
        {
            Announce("Who plugged four commercial air fryers into the same surge protector?! Shield your eyes!", ManagerEmotion.Panicked, "res://Sounds/henderson_event_powersurge.wav", 4.5f, priority: 3);
        }
        else if (id.Contains("lowgravity"))
        {
            Announce("Atmospheric pressure malfunction in Aisle 6! We are floating, people! Watch the ceiling fans!", ManagerEmotion.Neutral, "res://Sounds/henderson_event_lowgravity.wav", 4.5f, priority: 3);
        }
        else if (id.Contains("speedfrenzy"))
        {
            Announce("Free espresso samples in the cafe! Everyone is moving at mach two! NO RUNNING IN THE AISLES!", ManagerEmotion.Megaphone, "res://Sounds/henderson_event_speedfrenzy.wav", 4.5f, priority: 3);
        }
        else if (id.Contains("densefog"))
        {
            Announce("Frozen foods condenser exploded! Thick fog across the floor! If you can't see the sales, feel for them!", ManagerEmotion.Neutral, "res://Sounds/henderson_event_densefog.wav", 4.5f, priority: 3);
        }
        else if (id.Contains("groomba"))
        {
            Announce("CODE RED! Maintenance has unchained the Groomba! Do NOT make eye contact! RUN!", ManagerEmotion.Panicked, "res://Sounds/henderson_event_groombarage.wav", 4.5f, priority: 3);
        }
        else if (id.Contains("clearance"))
        {
            Announce("BLUE LIGHT SPECIAL! Damage boosted by fifty percent! EVERYTHING MUST GO! INCLUDING YOUR RIVALS!", ManagerEmotion.Greedy, "res://Sounds/henderson_event_clearance.wav", 4.5f, priority: 3);
        }
        else if (id.Contains("sprinkler"))
        {
            Announce("Sprinkler malfunction! Floor is soaked! Be advised, slipping on wet tile voids your right to sue!", ManagerEmotion.Annoyed, "res://Sounds/henderson_event_sprinklers.wav", 4.5f, priority: 3);
        }
    }

    public void AnnounceZoneShrink()
    {
        AnnounceRandom(ZoneShrinkLines, priority: 2);
    }

    public void AnnounceVictory(string winnerName, bool isDraw)
    {
        if (isDraw)
        {
            Announce("Mutual destruction?! Everyone is eliminated?! Great, now who is going to restock Aisle 4?!", ManagerEmotion.Panicked, "res://Sounds/henderson_draw_01.wav", 6.0f, priority: 4);
        }
        else
        {
            Announce($"WE HAVE A WINNER! Congratulations {winnerName}! Please present your receipt at customer service to claim your survival voucher!", ManagerEmotion.Megaphone, "res://Sounds/henderson_victory_01.wav", 6.0f, priority: 4);
        }
    }

    public void AnnounceSpecialDrop()
    {
        Announce("Attention bargain hunters! Corporate just authorized a MANAGER'S SPECIAL on the center island! Go fight for it!", ManagerEmotion.Greedy, "res://Sounds/henderson_special_drop_01.wav", 5.0f, priority: 4);
    }

    public void AnnounceBounty(string playerName, int amount)
    {
        Announce($"Attention shoppers: {playerName} is taking ALL the discounts! A ${amount} STORE BOUNTY has been placed on their head! Terminate their discount!", ManagerEmotion.Greedy, "res://Sounds/henderson_bounty_start_01.wav", 5.0f, priority: 4);
    }

    private static readonly string[] SupportedAudioExtensions = { ".wav", ".mp3", ".ogg" };

    public static string ResolveAudioFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        // Normalize path prefix if needed
        string fullPath = path;
        if (!fullPath.StartsWith("res://"))
        {
            fullPath = "res://Sounds/" + fullPath;
        }

        // Check exact path first if it already has an extension
        if (ResourceLoader.Exists(fullPath) || FileAccess.FileExists(fullPath))
        {
            return fullPath;
        }

        // Strip extension if present to test all supported formats
        string basePath = fullPath;
        int dotIdx = fullPath.LastIndexOf('.');
        if (dotIdx > 0 && fullPath.Length - dotIdx <= 5)
        {
            basePath = fullPath.Substring(0, dotIdx);
        }

        foreach (string ext in SupportedAudioExtensions)
        {
            string candidate = basePath + ext;
            if (ResourceLoader.Exists(candidate) || FileAccess.FileExists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private void AnnounceRandom(HendersonDialogue[] pool, int priority)
    {
        if (pool == null || pool.Length == 0) return;

        List<int> recordedIndices = new();
        List<string> resolvedPaths = new();

        for (int i = 0; i < pool.Length; i++)
        {
            string resolved = ResolveAudioFile(pool[i].ClipBaseName);
            if (resolved != null)
            {
                recordedIndices.Add(i);
                resolvedPaths.Add(resolved);
            }
        }

        int chosenIndex;
        string chosenClip = null;

        if (recordedIndices.Count > 0)
        {
            int r = _rng.RandiRange(0, recordedIndices.Count - 1);
            chosenIndex = recordedIndices[r];
            chosenClip = resolvedPaths[r];
        }
        else
        {
            chosenIndex = _rng.RandiRange(0, pool.Length - 1);
        }

        var entry = pool[chosenIndex];
        Announce(entry.Text, entry.Emotion, chosenClip, DefaultAnnouncementDuration, priority);
    }

    private void AnnounceRandom(string[] lines, ManagerEmotion emotion, int priority, string clipPrefix = null)
    {
        if (lines == null || lines.Length == 0) return;

        List<int> existingAudioIndices = new();
        List<string> existingAudioPaths = new();

        if (!string.IsNullOrEmpty(clipPrefix))
        {
            for (int i = 0; i < lines.Length; i++)
            {
                string candidate = ResolveAudioFile($"{clipPrefix}_{i + 1:D2}");
                if (candidate != null)
                {
                    existingAudioIndices.Add(i);
                    existingAudioPaths.Add(candidate);
                }
            }
        }

        int index;
        string clip = null;
        if (existingAudioIndices.Count > 0)
        {
            int r = _rng.RandiRange(0, existingAudioIndices.Count - 1);
            index = existingAudioIndices[r];
            clip = existingAudioPaths[r];
        }
        else
        {
            index = _rng.RandiRange(0, lines.Length - 1);
            if (!string.IsNullOrEmpty(clipPrefix))
            {
                clip = ResolveAudioFile($"{clipPrefix}_{index + 1:D2}");
            }
        }

        string line = lines[index];
        Announce(line, emotion, clip, DefaultAnnouncementDuration, priority);
    }

    #region Dialogue Script Pools
    private static readonly HendersonDialogue[] LobbyLines = new[]
    {
        new HendersonDialogue(
            "Testing, one, two... is this thing on? Welcome to SuperMart #404. Fighting before the official countdown is strictly prohibited by corporate HR.",
            ManagerEmotion.Neutral,
            "henderson_lobby_01"
        ),
        new HendersonDialogue(
            "Reminder: Management is not responsible for any severed limbs, bruised egos, or broken eggs. Shop responsibly.",
            ManagerEmotion.Neutral,
            "henderson_lobby_02"
        ),
        new HendersonDialogue(
            "If you see a Groomba approaching your ankles, please refrain from kicking it. Groomba units have dental insurance; you do not.",
            ManagerEmotion.Neutral,
            "henderson_lobby_03"
        ),
        new HendersonDialogue(
            "Please prepare your carts at the starting line. And remember: a clean store is a compliant store!",
            ManagerEmotion.Neutral,
            "henderson_lobby_04"
        )
    };

    private static readonly HendersonDialogue[] ShoppingStartLines = new[]
    {
        new HendersonDialogue(
            "The doors are OPEN! You have thirty seconds of authorized retail therapy! Spend, spend, spend!",
            ManagerEmotion.Greedy,
            "henderson_phase_shopping_01"
        ),
        new HendersonDialogue(
            "Remember store policy: If you break it, you buy it! If you throw it, maintain proper ergonomic posture!",
            ManagerEmotion.Greedy,
            "henderson_phase_shopping_02"
        ),
        new HendersonDialogue(
            "Produce is fully stocked, aisles are waxed! Let the purchasing frenzy commence!",
            ManagerEmotion.Greedy,
            "henderson_phase_shopping_03"
        )
    };

    private static readonly HendersonDialogue[] ShoppingActiveLines = new[]
    {
        new HendersonDialogue(
            "Halfway through the shopping window! If your cart isn't overflowing, you're doing it wrong!",
            ManagerEmotion.Greedy,
            "henderson_phase_shopping_04"
        ),
        new HendersonDialogue(
            "Don't hoard all the produce! Share the fiber!",
            ManagerEmotion.Greedy,
            "henderson_phase_shopping_05"
        ),
        new HendersonDialogue(
            "Check out the clearance section in hardware! Heavy tools make great investments!",
            ManagerEmotion.Greedy,
            "henderson_phase_shopping_06"
        )
    };

    private static readonly HendersonDialogue[] LockdownLines = new[]
    {
        new HendersonDialogue(
            "Store lockdown in ten seconds! All registers are closing! Find a weapon—I mean, find cover!",
            ManagerEmotion.Panicked,
            "henderson_phase_lockdown_01"
        ),
        new HendersonDialogue(
            "Attention shoppers: The store is now locked. Anyone caught roaming without a melee implement is at a severe statistical disadvantage.",
            ManagerEmotion.Panicked,
            "henderson_phase_lockdown_02"
        ),
        new HendersonDialogue(
            "Shelves are sealed! No more refunds! What you're holding is all you've got!",
            ManagerEmotion.Panicked,
            "henderson_phase_lockdown_03"
        ),
        new HendersonDialogue(
            "Prepare for Battle Royale! And please... keep blood off the deli slicer. It’s a nightmare to clean.",
            ManagerEmotion.Panicked,
            "henderson_phase_lockdown_04"
        )
    };

    private static readonly HendersonDialogue[] BattleStartLines = new[]
    {
        new HendersonDialogue(
            "COMBAT PERMITTED! Let the clearance event of the century begin!",
            ManagerEmotion.Megaphone,
            "henderson_phase_battle_01"
        ),
        new HendersonDialogue(
            "Battle mode engaged! Everything is a weapon if you throw it hard enough!",
            ManagerEmotion.Megaphone,
            "henderson_phase_battle_02"
        ),
        new HendersonDialogue(
            "May the most cost-effective shopper survive!",
            ManagerEmotion.Megaphone,
            "henderson_phase_battle_03"
        )
    };

    private static readonly HendersonDialogue[] FirstBloodLines = new[]
    {
        new HendersonDialogue(
            "And there's first blood! Cleanup on Aisle 3! And somebody please log that as inventory loss!",
            ManagerEmotion.Annoyed,
            "henderson_kill_first_01"
        ),
        new HendersonDialogue(
            "First customer down! Remember to save your receipt for a posthumous tax deduction!",
            ManagerEmotion.Annoyed,
            "henderson_kill_first_02"
        ),
        new HendersonDialogue(
            "Ooh, that had to hurt. Security, bring a mop and a yellow caution cone.",
            ManagerEmotion.Annoyed,
            "henderson_kill_first_03"
        )
    };

    private static readonly HendersonDialogue[] GenericKillLines = new[]
    {
        new HendersonDialogue(
            "Customer eliminated! That's fine, but WHO IS PAYING FOR THAT DAMAGED MERCHANDISE?!",
            ManagerEmotion.Annoyed,
            "henderson_kill_generic_01"
        ),
        new HendersonDialogue(
            "Down goes another shopper! That cart is officially marked as unclaimed freight!",
            ManagerEmotion.Annoyed,
            "henderson_kill_generic_02"
        ),
        new HendersonDialogue(
            "Oouch! Direct hit to the dignity! Rest in bulk savings!",
            ManagerEmotion.Annoyed,
            "henderson_kill_generic_03"
        ),
        new HendersonDialogue(
            "Another one bites the linoleum! Keep it moving, folks, don't gawk!",
            ManagerEmotion.Annoyed,
            "henderson_kill_generic_04"
        ),
        new HendersonDialogue(
            "That's a permanent return without receipt! Out of the store!",
            ManagerEmotion.Annoyed,
            "henderson_kill_generic_05"
        )
    };

    private static readonly HendersonDialogue[] ZoneShrinkLines = new[]
    {
        new HendersonDialogue(
            "Notice: Outer aisles are now CLOSED for floor waxing! Anyone caught past the yellow hazard tape will be vaporized!",
            ManagerEmotion.Annoyed,
            "henderson_zone_shrink_01"
        ),
        new HendersonDialogue(
            "The perimeter is contracting! Move toward the center aisles or face automatic disciplinary termination!",
            ManagerEmotion.Annoyed,
            "henderson_zone_shrink_02"
        ),
        new HendersonDialogue(
            "Final zone constriction! It's checkout counter or bust! Squeeze together, shoppers!",
            ManagerEmotion.Annoyed,
            "henderson_zone_shrink_03"
        )
    };
    #endregion
}

public class HendersonDialogue
{
    public string Text { get; }
    public ManagerEmotion Emotion { get; }
    public string ClipBaseName { get; }

    public HendersonDialogue(string text, ManagerEmotion emotion, string clipBaseName)
    {
        Text = text;
        Emotion = emotion;
        ClipBaseName = clipBaseName;
    }
}
