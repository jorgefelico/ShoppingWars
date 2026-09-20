using Godot;

public static class SettingsManager
{
    private const string SettingsFilePath = "user://settings.cfg";

    public enum GraphicsPreset
    {
        Low,
        Medium,
        High,
        Ultra,
        Custom
    }

    public enum AntiAliasingMode
    {
        Off,
        Fxaa,
        Taa,
        TaaAndSmaa
    }

    public enum ShadowQualityLevel
    {
        Low,     // 1024 atlas, soft shadow filter low
        Medium,  // 2048 atlas, soft shadow filter medium
        High,    // 2048 atlas, soft shadow filter high
        Ultra    // 4096 atlas, soft shadow filter ultra
    }

    public enum QualityLevel
    {
        Off,
        Low,
        Medium,
        High
    }

    public static float MasterVolume { get; private set; } = 1.0f;
    public static float MusicVolume { get; private set; } = 0.8f;
    public static float SfxVolume { get; private set; } = 1.0f;
    public static float MouseSensitivity { get; private set; } = 1.0f;
    public static float Fov { get; private set; } = 75.0f;
    public static bool IsFullscreen { get; private set; } = false;
    public static bool IsVsync { get; private set; } = true;

    // Graphics Settings
    public static GraphicsPreset CurrentPreset { get; private set; } = GraphicsPreset.Medium;
    public static AntiAliasingMode CurrentAntiAliasing { get; private set; } = AntiAliasingMode.Taa;
    public static ShadowQualityLevel CurrentShadowQuality { get; private set; } = ShadowQualityLevel.Medium;
    public static QualityLevel CurrentSsao { get; private set; } = QualityLevel.Low;
    public static QualityLevel CurrentSsil { get; private set; } = QualityLevel.Off;
    public static QualityLevel CurrentSsr { get; private set; } = QualityLevel.Medium;
    public static QualityLevel CurrentVolumetricFog { get; private set; } = QualityLevel.Low;

    private static bool _isInitialized = false;

    public static void Initialize()
    {
        EnsureAudioBuses();
        if (!_isInitialized)
        {
            _isInitialized = true;
            LoadSettings();
        }
        ApplyAllSettings();
    }

    public static void EnsureAudioBuses()
    {
        int musicBusIdx = AudioServer.GetBusIndex("Music");
        if (musicBusIdx == -1)
        {
            AudioServer.AddBus();
            musicBusIdx = AudioServer.BusCount - 1;
            AudioServer.SetBusName(musicBusIdx, "Music");
            AudioServer.SetBusSend(musicBusIdx, "Master");
        }

        int sfxBusIdx = AudioServer.GetBusIndex("SFX");
        if (sfxBusIdx == -1)
        {
            AudioServer.AddBus();
            sfxBusIdx = AudioServer.BusCount - 1;
            AudioServer.SetBusName(sfxBusIdx, "SFX");
            AudioServer.SetBusSend(sfxBusIdx, "Master");
        }
    }

    public static void SetMasterVolume(float linear)
    {
        MasterVolume = Mathf.Clamp(linear, 0f, 1f);
        ApplyBusVolume("Master", MasterVolume);
        SaveSettings();
    }

    public static void SetMusicVolume(float linear)
    {
        MusicVolume = Mathf.Clamp(linear, 0f, 1f);
        ApplyBusVolume("Music", MusicVolume);
        SaveSettings();
    }

    public static void SetSfxVolume(float linear)
    {
        SfxVolume = Mathf.Clamp(linear, 0f, 1f);
        ApplyBusVolume("SFX", SfxVolume);
        SaveSettings();
    }

    private static void ApplyBusVolume(string busName, float linear)
    {
        int busIdx = AudioServer.GetBusIndex(busName);
        if (busIdx == -1) return;

        if (linear <= 0.001f)
        {
            AudioServer.SetBusMute(busIdx, true);
        }
        else
        {
            AudioServer.SetBusMute(busIdx, false);
            AudioServer.SetBusVolumeDb(busIdx, Mathf.LinearToDb(linear));
        }
    }

    public static void SetMouseSensitivity(float multiplier)
    {
        MouseSensitivity = Mathf.Clamp(multiplier, 0.2f, 3.0f);
        SaveSettings();
    }

    public static void SetFov(float fov)
    {
        Fov = Mathf.Clamp(fov, 70f, 110f);
        var player = PlayerController.Instance;
        if (GodotObject.IsInstanceValid(player) && GodotObject.IsInstanceValid(player.Camera))
        {
            player.Camera.Fov = Fov;
        }
        SaveSettings();
    }

    public static void SetFullscreen(bool fullscreen)
    {
        IsFullscreen = fullscreen;
        DisplayServer.WindowSetMode(fullscreen 
            ? DisplayServer.WindowMode.Fullscreen 
            : DisplayServer.WindowMode.Windowed);
        SaveSettings();
    }

    public static void SetVsync(bool vsync)
    {
        IsVsync = vsync;
        DisplayServer.WindowSetVsyncMode(vsync 
            ? DisplayServer.VSyncMode.Enabled 
            : DisplayServer.VSyncMode.Disabled);
        SaveSettings();
    }

    public static void SetGraphicsPreset(GraphicsPreset preset)
    {
        CurrentPreset = preset;
        switch (preset)
        {
            case GraphicsPreset.Low:
                CurrentAntiAliasing = AntiAliasingMode.Fxaa;
                CurrentShadowQuality = ShadowQualityLevel.Low;
                CurrentSsao = QualityLevel.Off;
                CurrentSsil = QualityLevel.Off;
                CurrentSsr = QualityLevel.Low;
                CurrentVolumetricFog = QualityLevel.Off;
                break;
            case GraphicsPreset.Medium:
                CurrentAntiAliasing = AntiAliasingMode.Taa;
                CurrentShadowQuality = ShadowQualityLevel.Medium;
                CurrentSsao = QualityLevel.Low;
                CurrentSsil = QualityLevel.Off;
                CurrentSsr = QualityLevel.Medium;
                CurrentVolumetricFog = QualityLevel.Low;
                break;
            case GraphicsPreset.High:
                CurrentAntiAliasing = AntiAliasingMode.TaaAndSmaa;
                CurrentShadowQuality = ShadowQualityLevel.High;
                CurrentSsao = QualityLevel.High;
                CurrentSsil = QualityLevel.Medium;
                CurrentSsr = QualityLevel.High;
                CurrentVolumetricFog = QualityLevel.High;
                break;
            case GraphicsPreset.Ultra:
                CurrentAntiAliasing = AntiAliasingMode.TaaAndSmaa;
                CurrentShadowQuality = ShadowQualityLevel.Ultra;
                CurrentSsao = QualityLevel.High;
                CurrentSsil = QualityLevel.High;
                CurrentSsr = QualityLevel.High;
                CurrentVolumetricFog = QualityLevel.High;
                break;
        }

        ApplyGraphicsQuality();
        SaveSettings();
    }

    public static void SetAntiAliasing(AntiAliasingMode mode)
    {
        CurrentAntiAliasing = mode;
        CurrentPreset = GraphicsPreset.Custom;
        ApplyGraphicsQuality();
        SaveSettings();
    }

    public static void SetShadowQuality(ShadowQualityLevel level)
    {
        CurrentShadowQuality = level;
        CurrentPreset = GraphicsPreset.Custom;
        ApplyGraphicsQuality();
        SaveSettings();
    }

    public static void SetSsaoQuality(QualityLevel level)
    {
        CurrentSsao = level;
        CurrentPreset = GraphicsPreset.Custom;
        ApplyGraphicsQuality();
        SaveSettings();
    }

    public static void SetSsrQuality(QualityLevel level)
    {
        CurrentSsr = level;
        CurrentPreset = GraphicsPreset.Custom;
        ApplyGraphicsQuality();
        SaveSettings();
    }

    public static void SetVolumetricFog(QualityLevel level)
    {
        CurrentVolumetricFog = level;
        CurrentPreset = GraphicsPreset.Custom;
        ApplyGraphicsQuality();
        SaveSettings();
    }

    public static string GetEstimatedVramString()
    {
        return CurrentPreset switch
        {
            GraphicsPreset.Low => "💾 Est. VRAM: ~1.5 GB • Budget GPUs / Laptops (4GB-6GB VRAM)",
            GraphicsPreset.Medium => "💾 Est. VRAM: ~2.8 GB • Recommended for Standard GPUs (6GB-8GB VRAM)",
            GraphicsPreset.High => "💾 Est. VRAM: ~4.2 GB • Recommended for Enthusiast GPUs (8GB-12GB VRAM)",
            GraphicsPreset.Ultra => "💾 Est. VRAM: ~6.5 GB • Recommended for High-End GPUs (16GB+ VRAM)",
            _ => "💾 Est. VRAM: ~3.0 GB • Custom Configuration"
        };
    }

    public static string GetAntiAliasingLabel(AntiAliasingMode mode) => mode switch
    {
        AntiAliasingMode.Off => "⚡ AA: OFF",
        AntiAliasingMode.Fxaa => "⚡ AA: FXAA (Fast)",
        AntiAliasingMode.Taa => "⚡ AA: TAA (Crisp)",
        AntiAliasingMode.TaaAndSmaa => "⚡ AA: TAA + SMAA (Ultra)",
        _ => "⚡ AA: Custom"
    };

    public static string GetShadowQualityLabel(ShadowQualityLevel level) => level switch
    {
        ShadowQualityLevel.Low => "🌑 Shadows: LOW (1K)",
        ShadowQualityLevel.Medium => "🌑 Shadows: MED (2K)",
        ShadowQualityLevel.High => "🌑 Shadows: HIGH (2K Soft)",
        ShadowQualityLevel.Ultra => "🌑 Shadows: ULTRA (4K Soft)",
        _ => "🌑 Shadows: Custom"
    };

    public static string GetSsrQualityLabel(QualityLevel level) => level switch
    {
        QualityLevel.Off => "🪞 Floor Wax SSR: OFF",
        QualityLevel.Low => "🪞 Floor Wax SSR: LOW (32)",
        QualityLevel.Medium => "🪞 Floor Wax SSR: MED (64)",
        QualityLevel.High => "🪞 Floor Wax SSR: HIGH (112)",
        _ => "🪞 Floor Wax SSR: Custom"
    };

    public static string GetVolumetricFogLabel(QualityLevel level) => level switch
    {
        QualityLevel.Off => "🌫️ God-Rays: OFF",
        QualityLevel.Low => "🌫️ God-Rays: LOW",
        QualityLevel.Medium => "🌫️ God-Rays: MED",
        QualityLevel.High => "🌫️ God-Rays: HIGH",
        _ => "🌫️ God-Rays: Custom"
    };

    public static Color GetPresetColor(GraphicsPreset preset) => preset switch
    {
        GraphicsPreset.Low => new Color(0.35f, 0.85f, 0.45f),
        GraphicsPreset.Medium => new Color(0.3f, 0.85f, 1.0f),
        GraphicsPreset.High => new Color(1.0f, 0.85f, 0.25f),
        GraphicsPreset.Ultra => new Color(0.9f, 0.45f, 1.0f),
        _ => new Color(0.75f, 0.80f, 0.90f)
    };

    public static void ApplyAllSettings()
    {
        EnsureAudioBuses();
        ApplyBusVolume("Master", MasterVolume);
        ApplyBusVolume("Music", MusicVolume);
        ApplyBusVolume("SFX", SfxVolume);

        var player = PlayerController.Instance;
        if (GodotObject.IsInstanceValid(player) && GodotObject.IsInstanceValid(player.Camera))
        {
            player.Camera.Fov = Fov;
        }

        DisplayServer.WindowSetMode(IsFullscreen 
            ? DisplayServer.WindowMode.Fullscreen 
            : DisplayServer.WindowMode.Windowed);

        DisplayServer.WindowSetVsyncMode(IsVsync 
            ? DisplayServer.VSyncMode.Enabled 
            : DisplayServer.VSyncMode.Disabled);

        ApplyGraphicsQuality();
    }

    public static void ApplyGraphicsQuality()
    {
        var root = Engine.GetMainLoop() as SceneTree;
        if (root?.Root == null) return;

        var rid = root.Root.GetViewportRid();

        // 1. Anti-Aliasing & Viewport Settings
        switch (CurrentAntiAliasing)
        {
            case AntiAliasingMode.Off:
                root.Root.Msaa3D = Viewport.Msaa.Disabled;
                root.Root.ScreenSpaceAA = Viewport.ScreenSpaceAAEnum.Disabled;
                root.Root.UseTaa = false;
                break;
            case AntiAliasingMode.Fxaa:
                root.Root.Msaa3D = Viewport.Msaa.Disabled;
                root.Root.ScreenSpaceAA = Viewport.ScreenSpaceAAEnum.Fxaa;
                root.Root.UseTaa = false;
                break;
            case AntiAliasingMode.Taa:
                root.Root.Msaa3D = Viewport.Msaa.Disabled;
                root.Root.ScreenSpaceAA = Viewport.ScreenSpaceAAEnum.Disabled;
                root.Root.UseTaa = true;
                break;
            case AntiAliasingMode.TaaAndSmaa:
                root.Root.Msaa3D = Viewport.Msaa.Disabled;
                root.Root.ScreenSpaceAA = Viewport.ScreenSpaceAAEnum.Smaa;
                root.Root.UseTaa = true;
                break;
        }

        root.Root.UseDebanding = true;
        root.Root.UseOcclusionCulling = true;

        // 2. Shadows
        int shadowAtlasSize = CurrentShadowQuality switch
        {
            ShadowQualityLevel.Low => 1024,
            ShadowQualityLevel.Medium => 2048,
            ShadowQualityLevel.High => 2048,
            ShadowQualityLevel.Ultra => 4096,
            _ => 2048
        };
        RenderingServer.ViewportSetPositionalShadowAtlasSize(rid, shadowAtlasSize, true);

        // 3. WorldEnvironment settings in current scene
        ApplyEnvironmentSettings(root);
    }

    public static void ApplyEnvironmentSettings(SceneTree tree = null)
    {
        tree ??= Engine.GetMainLoop() as SceneTree;
        if (tree?.CurrentScene == null) return;

        WorldEnvironment worldEnv = tree.CurrentScene.GetNodeOrNull<WorldEnvironment>("WorldStuff/WorldEnvironment")
                                 ?? tree.CurrentScene.FindChild("WorldEnvironment", true, false) as WorldEnvironment;

        if (worldEnv?.Environment == null) return;
        var env = worldEnv.Environment;

        // SSAO
        switch (CurrentSsao)
        {
            case QualityLevel.Off:
                env.SsaoEnabled = false;
                break;
            case QualityLevel.Low:
                env.SsaoEnabled = true;
                env.SsaoRadius = 1.2f;
                env.SsaoIntensity = 1.5f;
                env.SsaoDetail = 0.4f;
                break;
            case QualityLevel.Medium:
                env.SsaoEnabled = true;
                env.SsaoRadius = 1.3f;
                env.SsaoIntensity = 1.8f;
                env.SsaoDetail = 0.6f;
                break;
            case QualityLevel.High:
                env.SsaoEnabled = true;
                env.SsaoRadius = 1.4f;
                env.SsaoIntensity = 2.2f;
                env.SsaoDetail = 0.85f;
                break;
        }

        // SSIL
        switch (CurrentSsil)
        {
            case QualityLevel.Off:
                env.SsilEnabled = false;
                break;
            case QualityLevel.Low:
            case QualityLevel.Medium:
                env.SsilEnabled = true;
                env.SsilRadius = 4.0f;
                env.SsilIntensity = 1.0f;
                break;
            case QualityLevel.High:
                env.SsilEnabled = true;
                env.SsilRadius = 5.0f;
                env.SsilIntensity = 1.4f;
                break;
        }

        // SSR (Screen Space Reflections)
        switch (CurrentSsr)
        {
            case QualityLevel.Off:
                env.SsrEnabled = false;
                break;
            case QualityLevel.Low:
                env.SsrEnabled = true;
                env.SsrMaxSteps = 32;
                env.SsrFadeIn = 0.06f;
                env.SsrFadeOut = 2.0f;
                env.SsrDepthTolerance = 0.2f;
                break;
            case QualityLevel.Medium:
                env.SsrEnabled = true;
                env.SsrMaxSteps = 64;
                env.SsrFadeIn = 0.05f;
                env.SsrFadeOut = 2.2f;
                env.SsrDepthTolerance = 0.2f;
                break;
            case QualityLevel.High:
                env.SsrEnabled = true;
                env.SsrMaxSteps = 112;
                env.SsrFadeIn = 0.04f;
                env.SsrFadeOut = 2.5f;
                env.SsrDepthTolerance = 0.2f;
                break;
        }

        // Volumetric Fog
        switch (CurrentVolumetricFog)
        {
            case QualityLevel.Off:
                env.VolumetricFogEnabled = false;
                break;
            case QualityLevel.Low:
                env.VolumetricFogEnabled = true;
                env.VolumetricFogDensity = 0.0018f;
                break;
            case QualityLevel.Medium:
            case QualityLevel.High:
                env.VolumetricFogEnabled = true;
                env.VolumetricFogDensity = 0.003f;
                break;
        }
    }

    public static void SaveSettings()
    {
        var config = new ConfigFile();
        config.SetValue("Audio", "MasterVolume", MasterVolume);
        config.SetValue("Audio", "MusicVolume", MusicVolume);
        config.SetValue("Audio", "SfxVolume", SfxVolume);
        config.SetValue("Controls", "MouseSensitivity", MouseSensitivity);
        config.SetValue("Camera", "Fov", Fov);
        config.SetValue("Display", "Fullscreen", IsFullscreen);
        config.SetValue("Display", "Vsync", IsVsync);
        config.SetValue("Graphics", "Preset", (int)CurrentPreset);
        config.SetValue("Graphics", "AntiAliasing", (int)CurrentAntiAliasing);
        config.SetValue("Graphics", "ShadowQuality", (int)CurrentShadowQuality);
        config.SetValue("Graphics", "Ssao", (int)CurrentSsao);
        config.SetValue("Graphics", "Ssil", (int)CurrentSsil);
        config.SetValue("Graphics", "Ssr", (int)CurrentSsr);
        config.SetValue("Graphics", "VolumetricFog", (int)CurrentVolumetricFog);
        config.Save(SettingsFilePath);
    }

    public static void LoadSettings()
    {
        var config = new ConfigFile();
        Error err = config.Load(SettingsFilePath);
        if (err != Error.Ok) return;

        MasterVolume = Mathf.Clamp((float)config.GetValue("Audio", "MasterVolume", 1.0f), 0f, 1f);
        MusicVolume = Mathf.Clamp((float)config.GetValue("Audio", "MusicVolume", 0.8f), 0f, 1f);
        SfxVolume = Mathf.Clamp((float)config.GetValue("Audio", "SfxVolume", 1.0f), 0f, 1f);
        MouseSensitivity = Mathf.Clamp((float)config.GetValue("Controls", "MouseSensitivity", 1.0f), 0.2f, 3.0f);
        Fov = Mathf.Clamp((float)config.GetValue("Camera", "Fov", 75.0f), 70f, 110f);
        IsFullscreen = (bool)config.GetValue("Display", "Fullscreen", false);
        IsVsync = (bool)config.GetValue("Display", "Vsync", true);

        CurrentPreset = (GraphicsPreset)Mathf.Clamp((int)config.GetValue("Graphics", "Preset", (int)GraphicsPreset.Medium), 0, (int)GraphicsPreset.Custom);
        CurrentAntiAliasing = (AntiAliasingMode)Mathf.Clamp((int)config.GetValue("Graphics", "AntiAliasing", (int)AntiAliasingMode.Taa), 0, (int)AntiAliasingMode.TaaAndSmaa);
        CurrentShadowQuality = (ShadowQualityLevel)Mathf.Clamp((int)config.GetValue("Graphics", "ShadowQuality", (int)ShadowQualityLevel.Medium), 0, (int)ShadowQualityLevel.Ultra);
        CurrentSsao = (QualityLevel)Mathf.Clamp((int)config.GetValue("Graphics", "Ssao", (int)QualityLevel.Low), 0, (int)QualityLevel.High);
        CurrentSsil = (QualityLevel)Mathf.Clamp((int)config.GetValue("Graphics", "Ssil", (int)QualityLevel.Off), 0, (int)QualityLevel.High);
        CurrentSsr = (QualityLevel)Mathf.Clamp((int)config.GetValue("Graphics", "Ssr", (int)QualityLevel.Medium), 0, (int)QualityLevel.High);
        CurrentVolumetricFog = (QualityLevel)Mathf.Clamp((int)config.GetValue("Graphics", "VolumetricFog", (int)QualityLevel.Low), 0, (int)QualityLevel.High);
    }
}
