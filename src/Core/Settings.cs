using Godot;

namespace Gearheart.Core;

/// <summary>
/// Player settings that are not part of a save: sound volumes and fullscreen.
/// Kept in user://settings.cfg (a Godot ConfigFile), so a new world or a save version bump never resets them.
/// Sounds go through two audio buses under Master: "Effects" (one-shots and ambience) and
/// "Car" (engine, drill, boost, wind and turbo loops), so the engine can be turned down on its own.
/// </summary>
public static class Settings
{
    public const string EffectsBus = "Effects", CarBus = "Car";

    /// <summary>Where the settings live. The pause-menu bot uses its own file so CI runs do not share volumes.</summary>
    public static string Path => GameSession.PauseTest ? "user://settings_pausetest.cfg" : "user://settings.cfg";

    private static bool _loaded;
    private static float _master = 1f, _effects = 1f, _car = 1f;
    private static bool _fullscreen;

    /// <summary>Master volume, 0..1.</summary>
    public static float Master { get { Load(); return _master; } set { _master = Mathf.Clamp(value, 0f, 1f); ApplyVolumes(); } }

    /// <summary>Effects volume (pickaxe, crashes, jingles, crickets), 0..1.</summary>
    public static float Effects { get { Load(); return _effects; } set { _effects = Mathf.Clamp(value, 0f, 1f); ApplyVolumes(); } }

    /// <summary>Engine and car loops volume, 0..1.</summary>
    public static float Car { get { Load(); return _car; } set { _car = Mathf.Clamp(value, 0f, 1f); ApplyVolumes(); } }

    public static bool Fullscreen
    {
        get { Load(); return _fullscreen; }
        set { _fullscreen = value; ApplyWindow(); }
    }

    /// <summary>Makes sure the buses exist and the saved settings are applied. Safe to call from every scene.</summary>
    public static void Apply()
    {
        Load();
        EnsureBus(EffectsBus);
        EnsureBus(CarBus);
        EnsureCaveEcho();
        ApplyVolumes();
        ApplyWindow();
    }

    private static void Load()
    {
        if (_loaded) return;
        _loaded = true;
        var cfg = new ConfigFile();
        if (cfg.Load(Path) != Error.Ok) return;
        _master = Mathf.Clamp((float)cfg.GetValue("audio", "master", 1f), 0f, 1f);
        _effects = Mathf.Clamp((float)cfg.GetValue("audio", "effects", 1f), 0f, 1f);
        _car = Mathf.Clamp((float)cfg.GetValue("audio", "car", 1f), 0f, 1f);
        _fullscreen = (bool)cfg.GetValue("video", "fullscreen", false);
    }

    /// <summary>Forget what was loaded and read the file again (the bot checks the round trip with this).</summary>
    public static void Reload()
    {
        _loaded = false;
        Load();
    }

    public static Error Save()
    {
        var cfg = new ConfigFile();
        cfg.SetValue("audio", "master", _master);
        cfg.SetValue("audio", "effects", _effects);
        cfg.SetValue("audio", "car", _car);
        cfg.SetValue("video", "fullscreen", _fullscreen);
        return cfg.Save(Path);
    }

    private static void EnsureBus(string name)
    {
        if (AudioServer.GetBusIndex(name) >= 0) return;
        AudioServer.AddBus();
        int i = AudioServer.BusCount - 1;
        AudioServer.SetBusName(i, name);
        AudioServer.SetBusSend(i, "Master");
    }

    private static AudioEffectReverb _cave;
    private static bool _caveOn;

    /// <summary>A reverb on the Effects bus, off and dry until <see cref="SetCaveEcho"/> turns it up underground.</summary>
    private static void EnsureCaveEcho()
    {
        int i = AudioServer.GetBusIndex(EffectsBus);
        if (i < 0) return;
        for (int e = 0; e < AudioServer.GetBusEffectCount(i); e++)
            if (AudioServer.GetBusEffect(i, e) is AudioEffectReverb found)
            {
                _cave = found;
                return;
            }
        _cave = new AudioEffectReverb
        {
            RoomSize = 0.62f,
            Damping = 0.35f,
            Spread = 0.8f,
            Hipass = 0.12f,
            PredelayMsec = 70f,
            PredelayFeedback = 0.3f,
            Dry = 1f,
            Wet = 0f,
        };
        AudioServer.AddBusEffect(i, _cave);
        AudioServer.SetBusEffectEnabled(i, AudioServer.GetBusEffectCount(i) - 1, false);
        _caveOn = false;
    }

    /// <summary>The cave echo's wet level now (for the bot log).</summary>
    public static float CaveEchoWet => _cave?.Wet ?? 0f;

    /// <summary>
    /// Underground the effects ring: <paramref name="amount"/> 0 (surface, echo off) .. 1 (the Deep).
    /// Not a setting, so it is not saved; it follows where you are.
    /// </summary>
    public static void SetCaveEcho(float amount)
    {
        if (_cave == null) return;
        amount = Mathf.Clamp(amount, 0f, 1f);
        _cave.Wet = 0.3f * amount;
        bool on = amount > 0.01f;
        if (on == _caveOn) return;
        _caveOn = on;
        int i = AudioServer.GetBusIndex(EffectsBus);
        if (i < 0) return;
        for (int e = 0; e < AudioServer.GetBusEffectCount(i); e++)
            if (AudioServer.GetBusEffect(i, e) == _cave) AudioServer.SetBusEffectEnabled(i, e, on);
    }

    private static void ApplyVolumes()
    {
        SetBus("Master", _master);
        SetBus(EffectsBus, _effects);
        SetBus(CarBus, _car);
    }

    private static void SetBus(string name, float linear)
    {
        int i = AudioServer.GetBusIndex(name);
        if (i < 0) return;
        // A slider feels even when it works on loudness, not amplitude: square it before converting.
        AudioServer.SetBusVolumeDb(i, linear <= 0.001f ? -80f : Mathf.LinearToDb(linear * linear));
        AudioServer.SetBusMute(i, linear <= 0.001f);
    }

    /// <summary>The bus volume in dB as the audio server has it now (for the bot log).</summary>
    public static float BusDb(string name)
    {
        int i = AudioServer.GetBusIndex(name);
        return i < 0 ? float.NaN : AudioServer.GetBusVolumeDb(i);
    }

    private static void ApplyWindow()
    {
        if (DisplayServer.GetName() == "headless") return;
        var mode = DisplayServer.WindowGetMode();
        bool isFull = mode is DisplayServer.WindowMode.Fullscreen or DisplayServer.WindowMode.ExclusiveFullscreen;
        if (_fullscreen && !isFull) DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
        else if (!_fullscreen && isFull) DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
    }
}
