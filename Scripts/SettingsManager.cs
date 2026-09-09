using Godot;

public static class SettingsManager
{
    private const string SettingsFilePath = "user://settings.cfg";

    public static float MasterVolume { get; private set; } = 1.0f;
    public static float MusicVolume { get; private set; } = 0.8f;
    public static float SfxVolume { get; private set; } = 1.0f;
    public static float MouseSensitivity { get; private set; } = 1.0f;
    public static float Fov { get; private set; } = 75.0f;
    public static bool IsFullscreen { get; private set; } = false;
    public static bool IsVsync { get; private set; } = true;

    private static bool _isInitialized = false;

    public static void Initialize()
    {
        if (_isInitialized) return;
        _isInitialized = true;

        EnsureAudioBuses();
        LoadSettings();
        ApplyAllSettings();
    }

    public static void EnsureAudioBuses()
    {
        // Check or create "Music" bus
        int musicBusIdx = AudioServer.GetBusIndex("Music");
        if (musicBusIdx == -1)
        {
            AudioServer.AddBus();
            musicBusIdx = AudioServer.BusCount - 1;
            AudioServer.SetBusName(musicBusIdx, "Music");
            AudioServer.SetBusSend(musicBusIdx, "Master");
        }

        // Check or create "SFX" bus
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
        if (PlayerController.Instance?.Camera != null)
        {
            PlayerController.Instance.Camera.Fov = Fov;
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

    public static void ApplyAllSettings()
    {
        EnsureAudioBuses();
        ApplyBusVolume("Master", MasterVolume);
        ApplyBusVolume("Music", MusicVolume);
        ApplyBusVolume("SFX", SfxVolume);

        if (PlayerController.Instance?.Camera != null)
        {
            PlayerController.Instance.Camera.Fov = Fov;
        }

        DisplayServer.WindowSetMode(IsFullscreen 
            ? DisplayServer.WindowMode.Fullscreen 
            : DisplayServer.WindowMode.Windowed);

        DisplayServer.WindowSetVsyncMode(IsVsync 
            ? DisplayServer.VSyncMode.Enabled 
            : DisplayServer.VSyncMode.Disabled);
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
    }
}
